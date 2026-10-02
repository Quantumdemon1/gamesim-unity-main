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

            director.OpenSettings();
            yield return null;
            ButtonWithCaption("Turn music off").onClick.Invoke();
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

            ButtonWithCaption("Turn music on").onClick.Invoke();
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

            director.OpenSettings();
            yield return null;
            ButtonWithCaption("Turn music off").onClick.Invoke();
            yield return null;
            audio.TickMusic(MusicFade.SeasonFadeOut);
            Assert.That(audio.SeasonPaused, Is.True, "Turned off, the bed fades and pauses where it is.");
            StopEverySource(audio);
            audio.RecoverFromAudioReset();
            Assert.That(audio.SeasonPaused, Is.False, "A reset loses a paused bed's place: it is stopped now, not paused,");
            ButtonWithCaption("Turn music on").onClick.Invoke();
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
            director.OpenSettings();
            yield return null;

            StopEverySource(audio);
            ButtonWithCaption("Turn music off").onClick.Invoke();
            yield return null;
            Assert.That(audio.SeasonLevel, Is.EqualTo(1f), "Turned off, the bed has not begun its fade");
            ButtonWithCaption("Turn music on").onClick.Invoke();
            yield return null;
            Assert.That(audio.CurrentMusic, Is.EqualTo(HouseAudio.Music.Season));
            Assert.That(audio.SeasonPlaying, Is.True, "when it is called back: marked playing, its stopped source plays again,");
            Assert.That(audio.SeasonLevel, Is.EqualTo(1f), "at the level the fade had reached.");

            StopEverySource(audio);
            ButtonWithCaption("Turn music off").onClick.Invoke();
            yield return null;
            audio.TickMusic(MusicFade.SeasonFadeOut);
            Assert.That(audio.SeasonLevel, Is.EqualTo(0f), "Turned off and left, the fade out ends");
            Assert.That(audio.SeasonPaused, Is.False, "on a stopped source, with no place to pause at: the bed is stopped,");
            ButtonWithCaption("Turn music on").onClick.Invoke();
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
