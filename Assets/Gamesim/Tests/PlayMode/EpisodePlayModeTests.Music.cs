using System.Collections;
using System.Collections.Generic;
using Gamesim.House;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// The music's fades as the house plays them - the reference build's handovers, on a clock the
    /// test steps.
    ///
    /// <para>The fades run on unscaled time, which Time.captureDeltaTime cannot pin, so every test
    /// here puts the house's audio on <see cref="HouseAudio.ManualMusicClock"/> and moves it by exact
    /// amounts with <see cref="HouseAudio.TickMusic"/>. Frames still pass - the director asks its
    /// music question every frame, and the opening moves on - but no fade moves unless the test
    /// says so. The rates themselves are pinned in EditMode by MusicFadeTests.</para>
    ///
    /// <para>Two more seams stand in for what a test cannot make happen on demand: a frame of the
    /// real clock, stalled for as long as the test likes, is <see cref="HouseAudio.TickMusicFrame"/>;
    /// and the engine resetting its audio - every source stopped, then a notification - is
    /// <see cref="StopEverySource"/> followed by <see cref="HouseAudio.RecoverFromAudioReset"/>.
    /// AudioSettings.Reset itself is never called: it would reset the audio of every test after the
    /// one that called it.</para>
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        /// <summary>The house's audio with its fades held for the test to step.</summary>
        private HouseAudio SteppedAudio()
        {
            var audio = director.GetComponent<HouseAudio>();
            Assert.That(audio, Is.Not.Null, "The director carries the house's audio.");
            audio.ManualMusicClock = true;
            return audio;
        }

        /// <summary>
        /// The theme waits for the house: silent for as long as the loading gate waits for the bodies
        /// to be built, however long that is, and then rising from silence to full over 1.5 s. The
        /// reference build gates its theme on everything being ready, so the music starts cleanly
        /// on the title rather than under a progress bar.
        /// </summary>
        [UnityTest]
        public IEnumerator Music_TheThemeWaitsForTheHouseThenRisesFromSilence()
        {
            var audio = SteppedAudio();
            var season = SequenceSeason();
            var stage = new MusicGateStage();
            var plan = SequencePlan(new List<string>(), season);
            plan.ReducedMotion = false;
            // Headless runs skip the movement, and with it the stage and its loading gate; this one
            // asks for both.
            plan.MotionInBatchmode = true;
            plan.Stage = stage;
            var sequence = Opening();
            sequence.Play(new string[0], plan);

            yield return SequenceWait(() => sequence.MusicHeld, 3f);
            Assert.That(sequence.MusicHeld, Is.True, "Nobody is built yet: the loading gate holds the theme.");
            yield return Frames(2);
            Assert.That(sequence.CurrentBeat, Is.EqualTo(OpeningBeat.Intro));
            Assert.That(audio.CurrentMusic, Is.EqualTo(HouseAudio.Music.Silent), "Nothing is asked for under the loading screen.");
            audio.TickMusic(MusicFade.ThemeFadeIn * 2f);
            Assert.That(audio.ThemeLevel, Is.EqualTo(0f), "However long the house takes to build, the theme stays silent.");

            stage.Readiness = 1f;
            yield return SequenceWait(() => !sequence.MusicHeld, 3f);
            Assert.That(sequence.MusicHeld, Is.False, "Everybody is built: the gate lets the theme go.");
            yield return Frames(2);
            Assert.That(sequence.CurrentBeat, Is.EqualTo(OpeningBeat.Intro), "Still the intro: the title is up.");
            Assert.That(audio.CurrentMusic, Is.EqualTo(HouseAudio.Music.Theme), "The theme once the house is ready.");
            Assert.That(audio.ThemeLevel, Is.EqualTo(0f), "It starts from silence");
            audio.TickMusic(MusicFade.ThemeFadeIn / 2f);
            Assert.That(audio.ThemeLevel, Is.EqualTo(0.5f).Within(1e-4f), "rises in a straight line");
            audio.TickMusic(MusicFade.ThemeFadeIn / 2f);
            Assert.That(audio.ThemeLevel, Is.EqualTo(1f), "and is at full 1.5 s after the gate let it go.");

            sequence.Skip();
            yield return null;
        }

        /// <summary>
        /// The close and the handover, the reference build's designed order: the theme fades out
        /// under the intro's fade to black, the house entry cuts whatever is left of it, and the
        /// season's bed rises from silence to full over 2 s.
        /// </summary>
        [UnityTest]
        public IEnumerator Music_TheThemeFadesUnderTheCloseAndTheBedRisesFromSilence()
        {
            var audio = SteppedAudio();
            var opening = director.Opening;
            director.PlayOpeningForVerification(holdUntilAdvanced: true);
            yield return Frames(2);
            Assert.That(audio.CurrentMusic, Is.EqualTo(HouseAudio.Music.Theme), "The theme under the titles.");
            Assert.That(audio.SeasonLevel, Is.EqualTo(0f), "The theme's arrival put the season's bed away.");
            audio.TickMusic(MusicFade.ThemeFadeIn);
            Assert.That(audio.ThemeLevel, Is.EqualTo(1f));

            // Through the title, the reveals and the group card, stopping on the fade to black.
            for (int i = 0; i < 40 && !opening.MusicClosing && opening.CurrentBeat == OpeningBeat.Intro; i++)
            {
                opening.Advance();
                yield return Frames(2);
            }
            Assert.That(opening.MusicClosing, Is.True, "The intro reached its fade to black.");
            Assert.That(opening.CurrentBeat, Is.EqualTo(OpeningBeat.Intro), "which is still the intro.");
            yield return null;
            Assert.That(audio.CurrentMusic, Is.EqualTo(HouseAudio.Music.Silent), "The theme is let go under the close");
            Assert.That(audio.ThemeLevel, Is.EqualTo(1f), "- let go, not cut:");
            audio.TickMusic(MusicFade.ThemeFadeOut / 2f);
            Assert.That(audio.ThemeLevel, Is.EqualTo(0.5f).Within(1e-4f), "it fades in a straight line, 1.5 s from full to nothing.");

            opening.Advance();
            yield return SequenceWait(() => opening.CurrentBeat == OpeningBeat.HouseEntry, 3f);
            yield return null;
            Assert.That(opening.CurrentBeat, Is.EqualTo(OpeningBeat.HouseEntry));
            Assert.That(audio.CurrentMusic, Is.EqualTo(HouseAudio.Music.Season), "The season's bed from the house entry on.");
            Assert.That(audio.ThemeLevel, Is.EqualTo(0f), "The house entry cuts whatever is left of the theme");
            Assert.That(audio.SeasonLevel, Is.EqualTo(0f), "and the bed starts from silence");
            audio.TickMusic(MusicFade.SeasonFadeIn / 2f);
            Assert.That(audio.SeasonLevel, Is.EqualTo(0.5f).Within(1e-4f), "rising in a straight line");
            audio.TickMusic(MusicFade.SeasonFadeIn / 2f);
            Assert.That(audio.SeasonLevel, Is.EqualTo(1f), "to full over 2 s.");

            opening.Skip();
            yield return null;
        }

        /// <summary>A skip is the same handover, at the press: the theme cut and the bed rising from silence.</summary>
        [UnityTest]
        public IEnumerator Music_ASkipCutsTheThemeAndTheBedRisesFromSilence()
        {
            var audio = SteppedAudio();
            director.PlayOpeningForVerification(holdUntilAdvanced: true);
            yield return Frames(2);
            audio.TickMusic(MusicFade.ThemeFadeIn);
            Assert.That(audio.ThemeLevel, Is.EqualTo(1f), "The theme is up under the titles.");

            director.SkipOpening();
            yield return null;
            Assert.That(director.Opening.IsPlaying, Is.True, "The skip stops at the introductions.");
            Assert.That(audio.CurrentMusic, Is.EqualTo(HouseAudio.Music.Season), "The skip hands over to the season's bed");
            Assert.That(audio.ThemeLevel, Is.EqualTo(0f), "cutting the theme");
            Assert.That(audio.SeasonLevel, Is.EqualTo(0f), "with the bed starting from silence");
            audio.TickMusic(MusicFade.SeasonFadeIn);
            Assert.That(audio.SeasonLevel, Is.EqualTo(1f), "and at full 2 s later.");

            director.Opening.Skip();
            yield return null;
        }

        /// <summary>
        /// "Turn music off" fades the bed out over 1.5 s and then pauses it, and "Turn music on"
        /// carries on where it paused, rising from silence over 2 s - the reference build's pause
        /// rather than a restart from the top. The preferences reach a fade in progress without
        /// moving it, and at full the bed is exactly as loud as it was before there were fades.
        /// </summary>
        [UnityTest]
        public IEnumerator Music_TurningItOffFadesAndPausesAndOnCarriesOnWhereItWas()
        {
            var audio = SteppedAudio();
            Assert.That(audio.CurrentMusic, Is.EqualTo(HouseAudio.Music.Season), "The house plays the season's bed.");
            audio.TickMusic(MusicFade.SeasonFadeIn);
            Assert.That(audio.SeasonLevel, Is.EqualTo(1f));
            Assert.That(audio.SeasonVolume, Is.EqualTo(audio.Volume * 0.45f).Within(1e-5f),
                "At full, the bed is as loud as it always was: 0.45 of the master volume.");
            // A place to keep: the bed has to have played some of itself.
            yield return SequenceWait(() => audio.SeasonSamples > 0, 3f);
            Assert.That(audio.SeasonSamples, Is.GreaterThan(0), "The bed is playing.");

            director.OpenSettingsAt(Gamesim.Presentation.SettingsSection.Sound);
            yield return null;
            PressSettingRow(Gamesim.Presentation.SettingRowWords.MusicRow, "Off");
            yield return null;
            Assert.That(audio.CurrentMusic, Is.EqualTo(HouseAudio.Music.Silent));
            Assert.That(audio.SeasonLevel, Is.EqualTo(1f), "Turning the music off does not cut it:");
            audio.TickMusic(MusicFade.SeasonFadeOut / 2f);
            Assert.That(audio.SeasonLevel, Is.EqualTo(0.5f).Within(1e-4f), "it fades,");
            Assert.That(audio.SeasonPaused, Is.False, "playing on while it does.");
            Assert.That(audio.SeasonVolume, Is.EqualTo(audio.Volume * 0.45f * 0.5f).Within(1e-5f), "The volume follows the fade.");

            // Reduced audio halves the music live, mid-fade, and hands it back, without moving the fade.
            audio.SetReducedAudio(true);
            Assert.That(audio.SeasonVolume, Is.EqualTo(audio.Volume * 0.45f * 0.5f * 0.5f).Within(1e-5f), "Reduced audio halves a fade in progress");
            Assert.That(audio.SeasonLevel, Is.EqualTo(0.5f).Within(1e-4f), "without jumping it.");
            audio.SetReducedAudio(false);
            Assert.That(audio.SeasonVolume, Is.EqualTo(audio.Volume * 0.45f * 0.5f).Within(1e-5f));

            audio.TickMusic(MusicFade.SeasonFadeOut / 2f);
            Assert.That(audio.SeasonLevel, Is.EqualTo(0f));
            Assert.That(audio.SeasonPaused, Is.True, "Silent 1.5 s after the press, the bed pauses where it is.");
            int paused = audio.SeasonSamples;
            Assert.That(paused, Is.GreaterThan(0), "It paused part of the way in.");

            PressSettingRow(Gamesim.Presentation.SettingRowWords.MusicRow, "On");
            yield return null;
            Assert.That(audio.CurrentMusic, Is.EqualTo(HouseAudio.Music.Season));
            Assert.That(audio.SeasonPaused, Is.False);
            Assert.That(audio.SeasonSamples, Is.GreaterThanOrEqualTo(paused),
                "Turned back on, the bed carries on from where it paused rather than starting over.");
            Assert.That(audio.SeasonLevel, Is.EqualTo(0f), "It comes back from silence");
            audio.TickMusic(MusicFade.SeasonFadeIn);
            Assert.That(audio.SeasonLevel, Is.EqualTo(1f), "to full over 2 s.");
            director.ClosePanels();
        }

        /// <summary>
        /// A frame stalled by a clip decoding, or by the house being placed, moves a fade by a
        /// twentieth of a second at most, so a voice that starts on the far side of one rises from
        /// silence instead of entering partway up its fade: after a 300 ms frame the theme is a
        /// thirtieth of the way up, not a fifth. An ordinary frame is still credited in full.
        /// </summary>
        [UnityTest]
        public IEnumerator Music_ALongFrameMovesAFadeATwentiethOfASecondAtMost()
        {
            var audio = SteppedAudio();
            director.PlayOpeningForVerification(holdUntilAdvanced: true);
            yield return Frames(2);
            Assert.That(audio.CurrentMusic, Is.EqualTo(HouseAudio.Music.Theme), "The theme under the titles,");
            Assert.That(audio.ThemeLevel, Is.EqualTo(0f), "from silence.");

            audio.TickMusicFrame(0.3f);
            Assert.That(audio.ThemeLevel, Is.EqualTo(MusicFade.LongestStep / MusicFade.ThemeFadeIn).Within(1e-5f),
                "A 300 ms frame moves the theme's fade by a twentieth of a second, not by all of the stall.");
            float level = audio.ThemeLevel;
            audio.TickMusicFrame(1f / 60f);
            Assert.That(audio.ThemeLevel, Is.EqualTo(level + 1f / 60f / MusicFade.ThemeFadeIn).Within(1e-5f),
                "An ordinary frame is credited in full.");

            director.SkipOpening();
            yield return null;
            Assert.That(audio.CurrentMusic, Is.EqualTo(HouseAudio.Music.Season), "The skip hands over to the season's bed");
            Assert.That(audio.SeasonLevel, Is.EqualTo(0f), "from silence,");
            audio.TickMusicFrame(0.25f);
            Assert.That(audio.SeasonLevel, Is.EqualTo(MusicFade.LongestStep / MusicFade.SeasonFadeIn).Within(1e-5f),
                "and a long frame moves the bed's fade a twentieth of a second too.");

            director.Opening.Skip();
            yield return null;
        }

        /// <summary>
        /// An audio reset - AudioSettings.Reset, or the output device changing: headphones out, a
        /// headset back - stops every source, and the engine says so afterwards. The bed comes back
        /// at the level its fade had reached, the house's air with it; and a bed that was paused has
        /// lost its place to the reset, and comes back from the top when the music is turned on
        /// rather than resuming nothing for the rest of the season.
        /// </summary>
        [UnityTest]
        public IEnumerator Music_AnAudioResetBringsTheBedBack()
        {
            var audio = SteppedAudio();
            Assert.That(audio.CurrentMusic, Is.EqualTo(HouseAudio.Music.Season), "The house plays the season's bed.");
            audio.TickMusic(MusicFade.SeasonFadeIn);
            yield return SequenceWait(() => audio.SeasonSamples > 0, 3f);
            Assert.That(audio.SeasonPlaying, Is.True, "The bed is playing.");
            bool air = audio.AmbiencePlaying;

            StopEverySource(audio);
            Assert.That(audio.SeasonPlaying, Is.False, "The reset stops the bed");
            audio.RecoverFromAudioReset();
            yield return null;
            Assert.That(audio.SeasonPlaying, Is.True, "and when the engine says so, the bed plays again");
            Assert.That(audio.SeasonLevel, Is.EqualTo(1f), "at the level its fade had reached,");
            Assert.That(audio.SeasonVolume, Is.EqualTo(audio.Volume * MusicFade.SeasonPeak).Within(1e-5f), "as loud as it was,");
            Assert.That(audio.AmbiencePlaying, Is.EqualTo(air), "with the house's air as it was.");

            director.OpenSettingsAt(Gamesim.Presentation.SettingsSection.Sound);
            yield return null;
            PressSettingRow(Gamesim.Presentation.SettingRowWords.MusicRow, "Off");
            yield return null;
            audio.TickMusic(MusicFade.SeasonFadeOut);
            Assert.That(audio.SeasonPaused, Is.True, "Turned off, the bed fades and pauses where it is.");
            StopEverySource(audio);
            audio.RecoverFromAudioReset();
            Assert.That(audio.SeasonPaused, Is.False, "A reset loses a paused bed's place: it is stopped now, not paused,");
            PressSettingRow(Gamesim.Presentation.SettingRowWords.MusicRow, "On");
            yield return null;
            Assert.That(audio.CurrentMusic, Is.EqualTo(HouseAudio.Music.Season));
            Assert.That(audio.SeasonPlaying, Is.True, "so turned back on it plays again from the top, where resuming would play nothing.");
            director.ClosePanels();
        }

        /// <summary>
        /// The theme under an audio reset: stopped with everything else, and started again, from the
        /// top, when the engine says so - its fade carrying on from where it had got to.
        /// </summary>
        [UnityTest]
        public IEnumerator Music_AnAudioResetStartsTheThemeAgain()
        {
            var audio = SteppedAudio();
            director.PlayOpeningForVerification(holdUntilAdvanced: true);
            yield return Frames(2);
            Assert.That(audio.CurrentMusic, Is.EqualTo(HouseAudio.Music.Theme), "The theme under the titles");
            audio.TickMusic(MusicFade.ThemeFadeIn / 2f);
            Assert.That(audio.ThemePlaying, Is.True, "is playing, halfway up its rise;");
            StopEverySource(audio);
            Assert.That(audio.ThemePlaying, Is.False, "a reset stops it,");
            audio.RecoverFromAudioReset();
            yield return null;
            Assert.That(audio.ThemePlaying, Is.True, "and the engine's word starts it again");
            Assert.That(audio.ThemeLevel, Is.EqualTo(0.5f).Within(1e-4f), "with its fade where it was.");

            director.SkipOpening();
            yield return null;
            director.Opening.Skip();
            yield return null;
        }

        /// <summary>
        /// A bed stopped behind the bookkeeping's back with no word from the engine - a reset while
        /// the audio was not listening - still comes back with the music switch. Called back
        /// mid-fade, a bed marked playing whose source is not starts again; and a fade out that ends
        /// on a stopped source stops the bed rather than pausing it, so "Turn music on" starts it
        /// again instead of resuming silence.
        /// </summary>
        [UnityTest]
        public IEnumerator Music_ABedStoppedBehindItsBackComesBackWithTheSwitch()
        {
            var audio = SteppedAudio();
            Assert.That(audio.CurrentMusic, Is.EqualTo(HouseAudio.Music.Season), "The house plays the season's bed.");
            audio.TickMusic(MusicFade.SeasonFadeIn);
            yield return SequenceWait(() => audio.SeasonSamples > 0, 3f);
            Assert.That(audio.SeasonPlaying, Is.True, "The bed is playing.");
            director.OpenSettingsAt(Gamesim.Presentation.SettingsSection.Sound);
            yield return null;

            StopEverySource(audio);
            PressSettingRow(Gamesim.Presentation.SettingRowWords.MusicRow, "Off");
            yield return null;
            Assert.That(audio.SeasonLevel, Is.EqualTo(1f), "Turned off, the bed has not begun its fade");
            PressSettingRow(Gamesim.Presentation.SettingRowWords.MusicRow, "On");
            yield return null;
            Assert.That(audio.CurrentMusic, Is.EqualTo(HouseAudio.Music.Season));
            Assert.That(audio.SeasonPlaying, Is.True, "when it is called back: marked playing, its stopped source plays again,");
            Assert.That(audio.SeasonLevel, Is.EqualTo(1f), "at the level the fade had reached.");

            StopEverySource(audio);
            PressSettingRow(Gamesim.Presentation.SettingRowWords.MusicRow, "Off");
            yield return null;
            audio.TickMusic(MusicFade.SeasonFadeOut);
            Assert.That(audio.SeasonLevel, Is.EqualTo(0f), "Turned off and left, the fade out ends");
            Assert.That(audio.SeasonPaused, Is.False, "on a stopped source, with no place to pause at: the bed is stopped,");
            PressSettingRow(Gamesim.Presentation.SettingRowWords.MusicRow, "On");
            yield return null;
            Assert.That(audio.SeasonPlaying, Is.True, "and turned back on it starts again, where a paused one would resume nothing.");
            director.ClosePanels();
        }

        /// <summary>
        /// The recordings are decoded when the audio starts up, not by the first Play(). Neither
        /// preloads, and a Play() that has to decode first stalls the frame the theme's fade begins
        /// on - and the next frame, credited with the stall, used to bring the theme in partway up.
        /// A second audio component starting up stands in for the house's, whose start-up is over
        /// by the time a test runs.
        /// </summary>
        [UnityTest]
        public IEnumerator Music_TheRecordingsAreDecodedBeforeTheFirstPlay()
        {
            var audio = SteppedAudio();
            Assert.That(audio.HasRecordedMusic, Is.True, "The project ships its own theme.");
            Assert.That(audio.ThemePlaying, Is.False, "The house plays its bed, not the theme.");
            var theme = Resources.Load<AudioClip>("Audio/Theme");
            Assert.That(theme, Is.Not.Null);
            theme.UnloadAudioData();
            Assert.That(theme.loadState, Is.EqualTo(AudioDataLoadState.Unloaded), "The theme's data let go, as it is before anything has played it:");

            var host = new GameObject("Second house audio");
            try
            {
                host.AddComponent<HouseAudio>();
                Assert.That(theme.loadState, Is.EqualTo(AudioDataLoadState.Loaded),
                    "starting the audio up decodes it, so the first Play() has nothing left to do.");
            }
            finally
            {
                UnityEngine.Object.Destroy(host);
            }
            yield return null;
        }

        /// <summary>
        /// The game menu card steps the music back to about a third, and the rooms' beds to half,
        /// over 0.3 s; the Settings frame it opens does not, so its sliders are heard at the level
        /// they set; and Resume brings both back over 0.6 s. The duck is a level beside the bed's own
        /// fade: the bed plays on, lower, and the music asked for never changes.
        /// </summary>
        [UnityTest]
        public IEnumerator Music_TheGameMenuLowersTheMusicAndItsSettingsDoNot()
        {
            var audio = SteppedAudio();
            Assert.That(audio.CurrentMusic, Is.EqualTo(HouseAudio.Music.Season), "The house plays the season's bed.");
            audio.TickMusic(MusicFade.SeasonFadeIn);
            float full = audio.Volume * MusicFade.SeasonPeak;
            Assert.That(audio.SeasonVolume, Is.EqualTo(full).Within(1e-5f), "Nothing holds the bed down in the house.");

            director.OpenSettings();
            yield return null;
            Assert.That(director.IsGameMenuOpen, Is.True, "The house's door opens the game menu card.");
            Assert.That(audio.MusicDuckLevel, Is.EqualTo(1f), "The music steps back on its clock, not at once:");
            audio.TickMusic(MusicFade.DuckIn / 2f);
            Assert.That(audio.MusicDuckLevel, Is.EqualTo(1f - (1f - MusicFade.PausedMusicDuck) / 2f).Within(1e-4f), "halfway down after 0.15 s,");
            audio.TickMusic(MusicFade.DuckIn / 2f);
            Assert.That(audio.SeasonVolume, Is.EqualTo(full * MusicFade.PausedMusicDuck).Within(1e-5f), "about a third 0.3 s after the card opened,");
            Assert.That(audio.BedDuckLevel, Is.EqualTo(MusicFade.PausedBedDuck), "and the rooms' beds at half.");
            Assert.That(audio.SeasonLevel, Is.EqualTo(1f), "The bed's own fade has not moved: it plays on, lower.");

            director.ShowSettingsSection(Gamesim.Presentation.SettingsSection.Sound);
            yield return null;
            Assert.That(director.AreSettingsSectionsOpen, Is.True, "The card's Settings opens the frame, on Sound here,");
            audio.TickMusic(MusicFade.DuckOut);
            Assert.That(audio.SeasonVolume, Is.EqualTo(full).Within(1e-5f), "where nothing is held down, so the sliders are judged at the true level,");
            Assert.That(audio.BedDuckLevel, Is.EqualTo(1f), "the rooms' beds included.");

            director.BackToGameMenu();
            yield return null;
            audio.TickMusic(MusicFade.DuckIn);
            Assert.That(audio.SeasonVolume, Is.EqualTo(full * MusicFade.PausedMusicDuck).Within(1e-5f), "Back on the card, the music steps back again.");

            director.ClosePanels();
            yield return null;
            Assert.That(director.IsGameMenuOpen, Is.False, "Resume closes the card");
            audio.TickMusic(MusicFade.DuckOut / 2f);
            Assert.That(audio.MusicDuckLevel, Is.GreaterThan(MusicFade.PausedMusicDuck).And.LessThan(1f), "and the music comes back up over 0.6 s,");
            audio.TickMusic(MusicFade.DuckOut / 2f);
            Assert.That(audio.SeasonVolume, Is.EqualTo(full).Within(1e-5f), "to where it was,");
            Assert.That(audio.BedDuckLevel, Is.EqualTo(1f), "with the rooms' beds.");
            Assert.That(audio.CurrentMusic, Is.EqualTo(HouseAudio.Music.Season), "The music asked for never changed.");
        }

        /// <summary>
        /// A competition plays its own loop: the briefing is still the season's bed, and the attempt
        /// opening brings the loop up from silence over a second while the bed falls over its 1.5 s
        /// and pauses where it is. Reduce sound halves the loop with the rest of the music. When the
        /// attempt ends the bed carries on from where it paused, rising over its 2 s, while the loop
        /// falls over 1.5 s and stops, to start from the top at the next competition. A practice,
        /// so nothing is committed, and the music draws nothing from the season.
        /// </summary>
        [UnityTest]
        public IEnumerator Music_ACompetitionPlaysItsOwnLoopAndTheBedCarriesOnAfter()
        {
            var audio = SteppedAudio();
            Assert.That(audio.HasRecordedCompetition, Is.True, "The project ships its competition loop.");
            Assert.That(audio.CurrentMusic, Is.EqualTo(HouseAudio.Music.Season), "The house plays the season's bed.");
            audio.TickMusic(MusicFade.SeasonFadeIn);
            // A place to keep: the bed has to have played some of itself.
            yield return SequenceWait(() => audio.SeasonSamples > 0, 3f);
            Assert.That(audio.SeasonSamples, Is.GreaterThan(0), "The bed is playing.");

            WarpPlayer(director.StationPosition);
            Assert.That(director.TryOpenPhasePanel(), Is.True);
            ButtonWithCaption("Begin the next competition").onClick.Invoke();
            yield return null;
            WarpPlayer(director.StationPosition);
            Assert.That(director.TryOpenPhasePanel(), Is.True);
            yield return null;
            Assert.That(audio.CurrentMusic, Is.EqualTo(HouseAudio.Music.Season), "The briefing is the bed's: the competition has not begun.");
            var before = director.Snapshot;

            ButtonWithCaption("Practice this competition").onClick.Invoke();
            yield return null;
            Assert.That(director.IsChallengeActive, Is.True, "The attempt is open.");
            Assert.That(audio.CurrentMusic, Is.EqualTo(HouseAudio.Music.Competition), "The attempt opening brings the competition's loop.");
            Assert.That(audio.CompetitionPlaying, Is.True);
            Assert.That(audio.CompetitionLevel, Is.EqualTo(0f), "It rises from silence");
            Assert.That(audio.SeasonLevel, Is.EqualTo(1f), "with the bed still at full: a crossfade, not a cut.");
            audio.TickMusic(MusicFade.CompetitionFadeIn);
            Assert.That(audio.CompetitionLevel, Is.EqualTo(1f), "Full a second later,");
            Assert.That(audio.SeasonLevel, Is.EqualTo(1f - MusicFade.CompetitionFadeIn / MusicFade.SeasonFadeOut).Within(1e-4f),
                "with the bed a second into its 1.5 s fall.");
            Assert.That(audio.CompetitionVolume, Is.EqualTo(audio.Volume * MusicFade.CompetitionPeak * audio.MusicDuckLevel).Within(1e-5f),
                "At full, the loop plays at the bed's gain.");
            audio.TickMusic(MusicFade.SeasonFadeOut);
            Assert.That(audio.SeasonLevel, Is.EqualTo(0f));
            Assert.That(audio.SeasonPaused, Is.True, "The bed pauses where it is under the competition.");
            int paused = audio.SeasonSamples;
            Assert.That(paused, Is.GreaterThan(0), "It paused part of the way in.");

            float full = audio.CompetitionVolume;
            Assert.That(full, Is.GreaterThan(0f));
            audio.SetReducedAudio(true);
            Assert.That(audio.CompetitionVolume, Is.EqualTo(full * 0.5f).Within(1e-5f), "Reduce sound halves the loop with the rest of the music.");
            audio.SetReducedAudio(false);
            Assert.That(audio.CompetitionVolume, Is.EqualTo(full).Within(1e-5f));

            yield return PressKey(UnityEngine.InputSystem.Key.Escape);
            yield return null;
            Assert.That(director.IsChallengeActive, Is.False, "Escape ends the practice, back at the briefing.");
            Assert.That(audio.CurrentMusic, Is.EqualTo(HouseAudio.Music.Season), "The attempt over, the bed is asked for again");
            Assert.That(audio.SeasonPaused, Is.False);
            Assert.That(audio.SeasonSamples, Is.GreaterThanOrEqualTo(paused), "and carries on from where it paused rather than the top,");
            Assert.That(audio.SeasonLevel, Is.EqualTo(0f), "from silence,");
            Assert.That(audio.CompetitionLevel, Is.EqualTo(1f), "under the loop, which is not cut.");
            audio.TickMusic(MusicFade.CompetitionFadeOut);
            Assert.That(audio.CompetitionLevel, Is.EqualTo(0f), "The loop is gone 1.5 s later");
            Assert.That(audio.CompetitionPlaying, Is.False, "and stopped, so the next competition starts it from the top,");
            Assert.That(audio.SeasonLevel, Is.EqualTo(MusicFade.CompetitionFadeOut / MusicFade.SeasonFadeIn).Within(1e-4f),
                "while the bed rises over its 2 s");
            audio.TickMusic(MusicFade.SeasonFadeIn);
            Assert.That(audio.SeasonLevel, Is.EqualTo(1f), "back to full.");
            Assert.That(director.Snapshot.revision, Is.EqualTo(before.revision), "A practice commits nothing,");
            Assert.That(director.Snapshot.randomState, Is.EqualTo(before.randomState), "and the music draws nothing from the season.");
        }

        /// <summary>
        /// The main menu plays its quiet title loop: from the top, rising from silence over 2 s, at
        /// 60 percent of the theme's gain, while the season's bed falls and pauses and the house's air
        /// goes quiet behind the menu. Start a new season leads to cast select, which is silent: the
        /// loop is let go, not cut, and the air stays off. And the intro's theme still starts from
        /// silence, cutting whatever is left of the loop. The season starting is stood in for by what
        /// the cast screen's commit does - the screen hidden and the opening begun - without staging
        /// a season.
        /// </summary>
        [UnityTest]
        public IEnumerator Music_TheMainMenuPlaysItsTitleLoopAndTheIntroStartsTheThemeFromSilence()
        {
            var audio = SteppedAudio();
            Assert.That(audio.HasRecordedTitle, Is.True, "The project ships its title loop.");
            Assert.That(audio.CurrentMusic, Is.EqualTo(HouseAudio.Music.Season), "The house plays the season's bed");
            audio.TickMusic(MusicFade.SeasonFadeIn);
            Assert.That(audio.SeasonLevel, Is.EqualTo(1f));
            Assert.That(audio.AmbienceEnabled, Is.True, "and its air:");
            Assert.That(audio.BedVoicesSounding, Is.GreaterThan(0), "a bed is sounding in the house.");

            director.OpenMainMenu();
            yield return null;
            Assert.That(Menu().IsShowing, Is.True);
            Assert.That(audio.CurrentMusic, Is.EqualTo(HouseAudio.Music.Title), "The main menu plays its title loop");
            Assert.That(audio.TitlePlaying, Is.True);
            Assert.That(audio.TitleLevel, Is.EqualTo(0f), "from silence,");
            Assert.That(audio.AmbienceEnabled, Is.False, "with the house's air off behind the menu");
            Assert.That(audio.BedVoicesSounding, Is.EqualTo(0), "and no bed sounding.");
            audio.TickMusic(MusicFade.TitleFadeIn / 2f);
            Assert.That(audio.TitleLevel, Is.EqualTo(0.5f).Within(1e-4f), "It rises in a straight line");
            audio.TickMusic(MusicFade.TitleFadeIn / 2f);
            Assert.That(audio.TitleLevel, Is.EqualTo(1f), "to full over 2 s,");
            Assert.That(audio.TitleVolume, Is.EqualTo(audio.Volume * MusicFade.ThemePeak * 0.6f * audio.MusicDuckLevel).Within(1e-5f),
                "at 60 percent of the theme's gain.");
            Assert.That(audio.SeasonLevel, Is.EqualTo(0f));
            Assert.That(audio.SeasonPaused, Is.True, "The season's bed has fallen and paused where it was.");

            var start = CastButtons(MainMenu.NewSeasonCaption);
            Assert.That(start, Has.Length.EqualTo(1), "The menu offers a new season.");
            start[0].onClick.Invoke();
            yield return null;
            Assert.That(CastScreen().IsShowing, Is.True, "Start leads to cast select,");
            Assert.That(audio.CurrentMusic, Is.EqualTo(HouseAudio.Music.Silent), "which is silent.");
            Assert.That(audio.TitleLevel, Is.EqualTo(1f), "The loop is let go, not cut:");
            audio.TickMusic(MusicFade.TitleFadeOut / 2f);
            Assert.That(audio.TitleLevel, Is.EqualTo(0.5f).Within(1e-4f), "it falls in a straight line.");
            Assert.That(audio.TitlePlaying, Is.True, "still sounding while it does.");
            Assert.That(audio.AmbienceEnabled, Is.False, "The air stays off behind cast select.");
            Assert.That(audio.BedVoicesSounding, Is.EqualTo(0));

            // What the cast screen's commit does, without staging a season: the screen goes, the opening begins.
            CastScreen().Hide();
            CastScreen().ReleasePreview();
            director.PlayOpeningForVerification(holdUntilAdvanced: true);
            yield return Frames(2);
            Assert.That(director.Opening.CurrentBeat, Is.EqualTo(OpeningBeat.Intro));
            Assert.That(audio.CurrentMusic, Is.EqualTo(HouseAudio.Music.Theme), "The intro has its theme,");
            Assert.That(audio.TitleLevel, Is.EqualTo(0f), "which cuts what was left of the title loop");
            Assert.That(audio.TitlePlaying, Is.False, "and stops it,");
            Assert.That(audio.ThemeLevel, Is.EqualTo(0f), "and starts from silence");
            audio.TickMusic(MusicFade.ThemeFadeIn / 2f);
            Assert.That(audio.ThemeLevel, Is.EqualTo(0.5f).Within(1e-4f), "rising as it always has.");
            Assert.That(audio.AmbienceEnabled, Is.True, "Past the front door, the house's air is on again.");

            director.Opening.Skip();
            yield return null;
        }

        /// <summary>
        /// What a reset of the audio system does to the house's sound, without the reset: every source
        /// the audio owns stopped, behind its bookkeeping's back.
        /// </summary>
        private static void StopEverySource(HouseAudio audio)
        {
            foreach (var source in audio.GetComponents<AudioSource>()) source.Stop();
        }

        /// <summary>
        /// A front door that is never built, in front of a house that is built only when the test
        /// says so: all these tests need of a stage is the loading gate before it. It refuses to
        /// place, so the intro that follows reveals everybody on cards.
        /// </summary>
        private sealed class MusicGateStage : OpeningSequence.IStage
        {
            /// <summary>How much of the house is built, 0 to 1. Nothing is, to begin with.</summary>
            public float Readiness;

            public float BodyReadiness => Readiness;
            public bool TryPlace() => false;
            public bool Placed => false;
            public bool Ready => true;
            public bool Failed => false;
            public void RestoreHome() { }
            public HouseCameraRig.Shot DoorShot => new HouseCameraRig.Shot { Seconds = 0.6f };
            public HouseCameraRig.Shot PushInShot => new HouseCameraRig.Shot { Seconds = 0.5f };
            public bool OnDeck(string id) => false;
            public bool ToDoor(string id) => false;
            public bool AtDoor(string id) => false;
            public void OpenDoor() { }
            public void CloseDoor() { }
            public bool ThroughDoor(string id) => false;
            public bool OnMark(string id) => false;
            public void Present(string id) { }
            public void SendOff(string id) { }
            public void StrikeSet() { }
            public void SendHome(string id) { }
            public bool AllHome => true;
            public void HoldForIntroductions() { }
        }
    }
}
