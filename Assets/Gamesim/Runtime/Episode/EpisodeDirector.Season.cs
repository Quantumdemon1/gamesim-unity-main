using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Gamesim.House;
using Gamesim.Persistence;
using Gamesim.Presentation;
using Gamesim.Simulation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Gamesim.Episode
{
    /// <summary>Saves, the main menu, preferences and starting a season: everything outside the episode itself.</summary>
    public sealed partial class EpisodeDirector
    {
        /// <summary>
        /// Decides what the music bed should be doing, from what is on screen.
        ///
        /// <para>The reference's rule, from <c>GameSim-Game-Flow_v2.md</c>: a theme over the opening,
        /// then background music through the season, and silence on the screens that are not the game
        /// — setup and the final stats. Expressed here as a question about state rather than a set of
        /// commands scattered through the code, so there is one place that decides and no way for two
        /// screens to disagree about what is playing.</para>
        ///
        /// <para>Safe to call from anywhere and often: <see cref="HouseAudio.SetMusic"/> ignores a
        /// state it is already in, so this does not restart the track on every render. The director
        /// asks it every frame as well (Update), because two of its inputs - the opening's loading
        /// gate and its closing fade - change in the middle of a beat, with nothing rendering; the
        /// question costs a handful of reads and a comparison.</para>
        /// </summary>
        private void ApplyMusic()
        {
            if (audioBed == null) return;

            bool setup = (mainMenu != null && mainMenu.IsShowing)
                         || (castSelect != null && castSelect.IsShowing)
                         || (characterCreator != null && characterCreator.IsShowing)
                         || (seasonReport != null && seasonReport.IsShowing);
            // A skipped show has no theme left to play: the skip hands over to the season's bed at
            // the press, not after the house has been put back behind black.
            bool show = opening != null && opening.IsPlaying && !opening.WasSkipped;
            audioBed.SetMusic(MusicFor(musicOn, setup, show, opening != null ? opening.CurrentBeat : null,
                opening != null && opening.MusicHeld, opening != null && opening.MusicClosing, CompetitionUnderWay));
            // The music steps back while the game menu card is open (the rooms' beds with it) and
            // under the big moments; the Settings frame is not the card, so its sliders are heard
            // at the level they set. A level only: the state asked for above is untouched.
            bool menuCard = IsGameMenuOpen;
            // And under the player's own dance, whose beat is the music while it is heard.
            audioBed.SetDuck(MusicFade.MusicDuckFor(menuCard, IsMusicDuckMoment, audioBed.ActivityDucksMusic), MusicFade.BedDuckFor(menuCard));
        }

        /// <summary>
        /// Whether one of the moments the music steps back for is on: the diary room, a vote, key or
        /// jury reveal running, or a ceremony staged in the house.
        /// </summary>
        private bool IsMusicDuckMoment =>
            IsDiaryOpen || IsCeremonyStaged || JuryRevealPlaying
            || (voteReveal != null && voteReveal.IsPlaying) || (keyCeremony != null && keyCeremony.IsPlaying);

        /// <summary>
        /// The music rule as a question: silence with the music off or on a setup screen, the theme
        /// under the intro, and the season's bed everywhere else - the house entry, the walk-in, the
        /// tour and the introductions included. The reference build plays its theme under the intro
        /// only and background music from the house entry on; played under all five beats, the
        /// theme's sixteen-second fanfare looped for minutes under the introductions.
        /// </summary>
        public static HouseAudio.Music MusicFor(bool musicOn, bool setupShowing, bool openingPlaying, string beat) =>
            MusicFor(musicOn, setupShowing, openingPlaying, beat, themeHeld: false, themeClosing: false);

        /// <summary>
        /// The same rule with the intro's two moments of silence in it, the reference build's:
        /// nothing while the loading gate waits for the house to be built (it starts its theme only
        /// once everything is ready), and the theme let go - a 1.5 s fade - once the intro's closing
        /// fade to black begins (its theme "fades on transition"). Both mean something only under the
        /// intro; the season's bed that follows cuts whatever is left of the theme and rises from
        /// silence.
        /// </summary>
        public static HouseAudio.Music MusicFor(bool musicOn, bool setupShowing, bool openingPlaying, string beat,
            bool themeHeld, bool themeClosing)
        {
            if (!musicOn || setupShowing) return HouseAudio.Music.Silent;
            if (!openingPlaying || beat != OpeningBeat.Intro) return HouseAudio.Music.Season;
            return themeHeld || themeClosing ? HouseAudio.Music.Silent : HouseAudio.Music.Theme;
        }

        /// <summary>
        /// The same rule with a competition in it: where the season's bed would play, a competition
        /// under way plays its own loop instead (<see cref="CompetitionUnderWay"/>). Everything else
        /// is as above: the switch and the setup screens are silence, and the intro has its theme.
        /// A departure from the reference build, whose bed plays until the game is over.
        /// </summary>
        public static HouseAudio.Music MusicFor(bool musicOn, bool setupShowing, bool openingPlaying, string beat,
            bool themeHeld, bool themeClosing, bool competition)
        {
            var music = MusicFor(musicOn, setupShowing, openingPlaying, beat, themeHeld, themeClosing);
            return competition && music == HouseAudio.Music.Season ? HouseAudio.Music.Competition : music;
        }

        /// <summary>
        /// Whether a competition is under way for the music: from the attempt opening - the walk to
        /// the stations, the game and its finish, a practice too - or a staged show starting, to the
        /// result card. Presentation only; it reads what is on screen and changes nothing.
        /// </summary>
        private bool CompetitionUnderWay => challengeActive || CompetitionShowHoldsTheCard;

        /// <summary>
        /// The tour is offered again only from the house's own Settings: from the front door or a
        /// save in recovery there is no house on screen for it to point at, and never while the
        /// opening has the house.
        /// </summary>
        private bool CanReplayTour => tutorial != null && !settingsFromFrontDoor && !blockedRecovery && !OpeningOwnsHouse;

        /// <summary>Closes Settings and plays the tour from its first step, whether or not it was seen.</summary>
        public void ReplayTour()
        {
            if (!CanReplayTour) return;
            ClosePanels();
            tutorial.Show(FindChrome);
        }

        public void SaveNow()
        {
            SaveSession();
            Render();
        }

        /// <summary>Save now's write, without the drawing: the Saves section's load saves the running season with it first.</summary>
        private void SaveSession()
        {
            if (!blockedRecovery && !durableCommitInProgress)
            {
                // After a failure this is the retry, and it writes exactly the session memory holds. Otherwise
                // the house's due seconds are installed first and everything is written once, whether or not a
                // pair is still walking. A refusal inside that flush has already written what was installed and
                // left its notice: it never falls through to a second write that would clear the notice and
                // resume a house that refuses again.
                if (npcSaveSuspended) TryAutosave();
                else { FlushNpcWholeTicks(); if (!blockedRecovery && !npcSaveSuspended) TryAutosave(); }
            }
        }

        private void TryAutosave()
        {
            // "Saved locally." after the status line, or in place of the line a failed save left: the way-out retry's rule.
            if (TryPersistTimed(engine.Snapshot, "save", out _, out var failure)) EndSessionSaveFailure();
            // Stopped without the panel's write: its retry would run inside the save that just failed.
            else { message = sessionSaveFailure = failure; BeginPersistenceNotice(failure); SuspendNpcWorldWithoutSaving(); }
        }

        public void LoadNow()
        {
            if (durableCommitInProgress) return;
            BeginSystemNotice();
            SuspendNpcWorldWithoutSaving();
            if (saves.TryLoad(out var state, out var result)) Install(state);
            message = result; Render();
        }

        public void RecoverBackup()
        {
            if (durableCommitInProgress) return;
            BeginSystemNotice();
            SuspendNpcWorldWithoutSaving();
            if (saves.TryRecoverBackup(out var state, out var result)) Install(state);
            message = result; Render();
        }

        /// <summary>
        /// Opens the front door.
        ///
        /// <para>Continue is offered from the <b>disk</b>, not from the fact that a season object
        /// exists: the director always has one in memory — it builds the authored scenario before it
        /// looks for a save — so asking the engine would offer "continue" on a fresh install and
        /// then continue a season the player never played.</para>
        /// </summary>
        public void OpenMainMenu()
        {
            if (mainMenu == null || durableCommitInProgress) return;
            PauseNpcSocialForPanel();
            ClosePanelsInternal(false);
            if (player != null) player.SetInputEnabled(false);
            if (cameraRig != null) cameraRig.ControlsEnabled = false;
            bool canContinue = saves != null && (File.Exists(saves.SavePath) || File.Exists(saves.BackupPath));
            // "Load a season" once any season is saved in the folder (menu slice 4): Settings on Saves.
            bool anySaved = EpisodeSaveStore.SeasonFiles(saveRoot).Count > 0;
            mainMenu.Show(canContinue, blockedRecovery ? message : null,
                CloseMainMenu, NewSeason, OpenSettingsFromMenu, QuitGame, CareerStats(), anySaved ? (Action)OpenSavesFromMenu : null, ContinueCardFacts(canContinue));
            Render();
        }

        /// <summary>Leaves the menu and hands the house back. Only reachable with a season running.</summary>
        public void CloseMainMenu()
        {
            if (mainMenu == null) return;
            mainMenu.Hide();
            ClosePanels();
            // Deferred from bootstrap: the opening's tour points at HUD chrome, and pointing at it
            // from behind a full-screen menu would have been a tour of something nobody could see.
            PlayOpening();
        }

        private void OpenSettingsFromMenu()
        {
            if (mainMenu != null) mainMenu.Hide();
            // Not the pause menu: nothing the player was in is held behind these (A3).
            settingsFromFrontDoor = true;
            // And their way back is the main menu's (menu slice 4, EpisodeDirector.SettingsFrame.cs).
            MarkSettingsFromMainMenu();
            OpenSettings();
        }

        /// <summary>
        /// The main menu's "Load a season": Settings on Saves, as its Settings row opens them - also in
        /// blocked recovery, whose frame would otherwise open on Advanced.
        /// </summary>
        private void OpenSavesFromMenu()
        {
            OpenSettingsFromMenu();
            if (blockedRecovery) ShowSettingsSection(SettingsSection.Saves);
        }

        /// <summary>
        /// Leaves the game.
        ///
        /// <para>In the editor this stops play instead of closing the application, because
        /// <see cref="Application.Quit"/> does nothing there and a Quit button that visibly does
        /// nothing is indistinguishable from a broken one.</para>
        /// </summary>
        public void QuitGame()
        {
            SuspendNpcWorldWithoutSaving();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        /// <summary>
        /// Opens the cast screen. Nothing is created or written until the player commits there, so
        /// backing out leaves the running season and its slot exactly as they were.
        /// </summary>
        public void NewSeason()
        {
            if (durableCommitInProgress) return;
            if (castSelect == null) { StartSeason(null); return; }
            // Settings the creator's gear opened no longer lead back to it (EpisodeDirector.CreatorSettings.cs).
            ForgetReturnToCreator();
            // The cast screen draws below the menu, so the menu has to step aside rather than sit on
            // top of it — and backing out has to land wherever the player came from, which is the
            // menu when they arrived through it and the settings panel when they did not.
            bool fromMenu = mainMenu != null && mainMenu.IsShowing;
            if (fromMenu) mainMenu.Hide();
            Action back = fromMenu ? (Action)OpenMainMenu : Render;
            // From a menu that offered no Continue there is no season to keep, and back is the
            // menu: the cast screen's back chip says that instead. Read after Hide, which keeps it.
            bool freshStart = fromMenu && !mainMenu.OffersContinue;
            // Where backing out lands, in the words of the screen's one way back (menu slice 2): the
            // menu, Settings and saves, the finale's page (its New season, or the report over it), or
            // the house with nothing open.
            string backTo = fromMenu ? WayOut.MainMenu : settingsOpen ? FrontDoorWords.SettingsPlace
                : IsPanelOpen ? FrontDoorWords.FinalePlace : WayOut.House;
            // The finale's page closes under the cast screen and its way back puts it back as it was
            // (EpisodeDirector.FinaleAside.cs). After the words above, which read the page as open.
            if (!fromMenu && SetFinaleAsideForCast()) back = ReturnToFinaleFromCast;
            // Each screen that can commit is named as the way back from a failed start, so the
            // player lands where they pressed it: see StartSeason(choice, failed).
            castSelect.Show(choice => StartSeason(choice, castSelect.Resume), back,
                characterCreator == null ? (Action<SeasonBuilder.Choice, CharacterDraft>)null
                : (choice, draft) =>
                {
                    Action<SeasonBuilder.Choice> commit = built => StartSeason(built, characterCreator.Resume);
                    Action returnToCast = () =>
                    {
                        castSelect.SetDraft(characterCreator.Draft, characterCreator.Mode);
                        castSelect.Resume();
                    };
                    characterCreator.ShowFromCast(choice, draft, commit, returnToCast,
                        castSelect.PreferredCreatorMode, castSelect.CreatorReturnPlace);
                }, freshStart, backTo);
            // Setup is one of the screens the music rule silences, and putting one up is not a
            // render: from the finale's panel, the report or settings the season's track played
            // on under the cast screen until something else happened to repaint.
            ApplyMusic();
        }

        /// <summary>
        /// Stages and installs a season. A null choice builds the authored six-person scenario,
        /// which is what a headless caller and the pre-cast-screen behaviour both get.
        ///
        /// <para>A choice that cannot be staged goes back to whichever screen this guesses made
        /// it. The screens themselves call the overload that says so instead.</para>
        /// </summary>
        public void StartSeason(SeasonBuilder.Choice choice) => StartSeason(choice, ScreenThatCommitted(choice));

        /// <summary>
        /// The same, naming the way back: <paramref name="failed"/> receives the reason when the
        /// season cannot be staged, and is the screen the player committed on, shown again with
        /// that reason and everything they had set on it intact. Nothing is shown when it is null.
        /// </summary>
        public void StartSeason(SeasonBuilder.Choice choice, Action<string> failed)
            => StartSeason(choice, failed, seed => choice == null ? ContentCatalog.Create(seed) : SeasonBuilder.Create(choice, seed));

        /// <summary>
        /// <b>Verification only:</b> the standalone profile's stress house, thirteen to sixteen
        /// houseguests from both rosters (<see cref="SeasonBuilder.CreateVerificationStressHouse"/>),
        /// started exactly as any season is. Internal, and called from nowhere but
        /// <see cref="PortVerification"/> under <c>--gamesim-verify --gamesim-stress-roster</c>: the
        /// cast screen, the creator and the importer cannot reach it.
        /// </summary>
        internal void StartVerificationStressSeason(int houseSize)
            => StartSeason(null, null, seed => SeasonBuilder.CreateVerificationStressHouse(houseSize, seed));

        private void StartSeason(SeasonBuilder.Choice choice, Action<string> failed, Func<uint, EpisodeState> build)
        {
            if (durableCommitInProgress) return;
            // The slot being left keeps every second the house lived in it. A failed write refuses the start
            // once, with its notice; the next start goes ahead with the old slot at its last save.
            if (!FlushForLeaving("slot") && !npcSlotRefused) { npcSlotRefused = true; failed?.Invoke(message); Render(); return; }
            SuspendNpcWorldWithoutSaving();
            bool installed = false;
            try
            {
                var seed = unchecked((uint)DateTime.UtcNow.Ticks);
                var nextStore = new EpisodeSaveStore(Path.Combine(saveRoot, "episode-" + Guid.NewGuid().ToString("N") + ".json"));
                var fresh = build(seed);
                // Every rule a fresh season plays under, from week one, in one place shared with the
                // balance harness: a rule the shipped game gains goes into ShippedRules.ApplyFresh, never
                // here (StressHouseTests reads this body and fails on a rule field set beside it). What the
                // player opted into on the cast screen - the conversation preview - is the choice's own, read there.
                ShippedRules.ApplyFresh(fresh, choice);
                CharacterAppearanceSnapshots.Materialize(fresh);
                fresh.sessionId = Guid.NewGuid().ToString("N");
                nextStore.Save(fresh); // Stage and validate on disk before replacing the current in-memory session.
                MarkSaveFeedback();
                SwitchToSeason(nextStore, fresh);
                message = SeasonMessage(fresh, choice);
                installed = true;
            }
            catch (Exception error) when (error is IOException || error is InvalidDataException || error is UnauthorizedAccessException || error is ArgumentException)
            {
                BeginSystemNotice();
                message = "New season could not be saved. Your current session and slot were preserved. "
                    + SaveJson.Explain(error);
                failed?.Invoke(message);
            }
            Render();
            // Setup is over however the season was started. The cast screen lets its live model go
            // when it starts one itself; a start from the creator leaves it asleep behind, holding
            // the last houseguest it showed - body, textures and target - for the whole season.
            if (installed && castSelect != null) castSelect.ReleasePreview();
            // Nor does the old season's finale page wait behind the cast screen for a way back.
            if (installed) ForgetFinaleAside();
            // The opening sequence, on the only path that reaches it.
            //
            // It has five authored beats, it owns the tutorial, and it is where a season's first
            // impressions are seeded - and it was unreachable for every player who cast their own
            // season. PlayOpening had two call sites: one gated on SaveRootOverride, which is
            // tests and the standalone verification, and one in CloseMainMenu, which NewSeason
            // steps around by calling mainMenu.Hide() directly. On a first launch there is no save,
            // so Continue is unavailable and New season is the only door - and it led straight past
            // the introduction to a house where every relationship was exactly zero.
            //
            // AFTER the catch and gated on success, not at the end of the try: the catch hands the
            // player back to the cast screen or the creator with the failure on it, and an opening
            // sequence is a canvas at sorting order 140 that would cover the screen they were
            // returned to. PlayOpening is a no-op in batchmode, so the headless suite - which calls
            // StartSeason directly from nine fixtures - is untouched.
            if (installed) PlayOpening();
        }

        /// <summary>
        /// The guess at who committed a choice, for callers that do not say: the creator when the
        /// choice carries a built houseguest, the cast screen otherwise.
        ///
        /// <para>It is only a guess, and for the cast screen's own starts it was wrong: every card
        /// that screen starts carries a built houseguest, so a failed "Play as" went to the creator
        /// - which was either never opened, leaving the player on no screen at all, or opened
        /// earlier and still holding a pending choice of its own, whose Start would then commit a
        /// season the player had moved on from. Both screens name themselves now.</para>
        /// </summary>
        private Action<string> ScreenThatCommitted(SeasonBuilder.Choice choice)
        {
            if (choice?.Authored != null && characterCreator != null) return characterCreator.Resume;
            if (choice != null && castSelect != null) return castSelect.Resume;
            return null;
        }

        private static string SeasonMessage(EpisodeState fresh, SeasonBuilder.Choice choice)
        {
            string head = "New season started in a new slot. Previous saves were retained.";
            if (choice == null) return head;
            var you = fresh.Find(fresh.playerId);
            return head + "  ·  " + fresh.contestants.Count + " houseguests, "
                   + CastTemplates.RosterName(choice.Roster).ToLowerInvariant()
                   + (you != null && choice.Authored != null
                       ? ". You built " + you.name + "."
                       : you != null && !string.IsNullOrEmpty(choice.PlayerTemplateId)
                           ? ". You are playing as " + you.name + "." : ".");
        }

        public void ImportFile(string path)
        {
            if (durableCommitInProgress) return;
            BeginSystemNotice();
            SuspendNpcWorldWithoutSaving();
            try
            {
                if (!File.Exists(path) || new FileInfo(path).Length > 8 * 1024 * 1024) { message = "Choose an existing JSON file smaller than eight MiB."; Render(); return; }
                if (WebSaveImporter.TryImport(File.ReadAllText(path), Path.Combine(saveRoot,"Imports"), out var state, out var result))
                {
                    // As a new season does: the slot being left gets the house's seconds, or refuses once.
                    if (!FlushForLeaving("slot") && !npcSlotRefused) { npcSlotRefused = true; Render(); return; }
                    var importedSlot = new EpisodeSaveStore(Path.Combine(saveRoot,"import-" + Guid.NewGuid().ToString("N") + ".json"));
                    importedSlot.Save(state); SwitchToSeason(importedSlot, state);
                }
                message = result;
            }
            catch (Exception error) when (error is IOException || error is InvalidDataException || error is UnauthorizedAccessException || error is ArgumentException)
            { message = "Import did not change your current slot. " + SaveJson.Explain(error); }
            Render();
        }

        /// <summary>
        /// The one switch to another season's save: a new season, an import, or a season loaded from
        /// the Saves list. The season is installed from <paramref name="store"/>, and the game opens on
        /// it next time - unless an isolated save root (a test or the verification) is in use, whose
        /// runs never change the player's own.
        /// </summary>
        private void SwitchToSeason(EpisodeSaveStore store, EpisodeState state)
        {
            saves = store; Install(state);
            if (SaveRootOverride == null) { PlayerPrefs.SetString("Gamesim.ActiveSave", Path.GetFileName(saves.SavePath)); PlayerPrefs.Save(); }
        }

        private void Install(EpisodeState state)
        {
            ResetNpcInvitationsForLoad();
            // A season replaced under the opening ends it without recording anything: the beats
            // belonged to the season that is going.
            if (opening != null && opening.IsPlaying) opening.Cancel();
            ResetCeremonyTruth();
            ResetNpcSocialForLoad();
            ClosePanels(); engine = new EpisodeEngine(state); blockedRecovery = false;
            // A load, a recovery or a new slot: the state installed is the disk's, and nothing of the house is
            // owed. Here and not in ResetNpcSocialForLoad, which the opening's stage also runs without a load.
            npcSaveCadence.Reset(); npcSlotRefused = false;
            // A finished season arriving by load, recovery or import is still a finished season.
            RecordCareer(state);
            // Before the placement loop below, which indexes the anchor arrays by body: the season
            // being installed can hold a different house from the one on screen, and until seasons
            // could differ in size this loop was indexing an array that always happened to match.
            SeatCast(state);
            // Load-time authored placement only, never a travel/pathfinding fallback.
            // Pending conversations walk back to their saved venue without new draws.
            if (initialNpcPositions != null)
                for (int i = 0; i < housemates.Length; i++)
                    if (housemates[i] != null) housemates[i].transform.SetPositionAndRotation(initialNpcPositions[i], initialNpcRotations[i]);
            if (player.Agent.isOnNavMesh) { player.Agent.Warp(initialPlayerPosition); player.Agent.ResetPath(); }
            Project();
        }

        /// <summary>Whether the text is larger than the standard size: the larger or the largest.</summary>
        public bool LargeText => textSize != TextSize.Standard;

        /// <summary>The text size in use (A8).</summary>
        public TextSize TextSize => textSize;

        /// <summary>How much the copy grows at the size in use: 1, 1.2 or 1.4.</summary>
        public float TextScale => TextSizes.Scale(textSize);

        /// <summary>The larger text, or the standard: what the toggle this replaced did, for the callers that ask for one of those two.</summary>
        public void SetLargeText(bool enabled) => SetTextSize(enabled ? TextSize.Larger : TextSize.Standard);

        public void SetTextSize(TextSize size)
        {
            textSize = size;
            ApplyPreferences();
            Render();
        }

        /// <summary>A channel's share of the master, as a percentage (A11).</summary>
        public int ChannelVolumePercent(SoundChannel channel) =>
            channel == SoundChannel.Music ? musicVolumePercent : channel == SoundChannel.Interface ? interfaceVolumePercent : effectsVolumePercent;

        /// <summary>Sets a channel's share of the master, 0 to 100, applies it and keeps it.</summary>
        public void SetChannelVolume(SoundChannel channel, int percent)
        {
            ApplyChannelVolume(channel, percent);
            Render();
        }

        // Slider callbacks keep their actual control alive throughout a drag or repeated key presses.
        private void SetMasterVolumeFromSlider(int percent)
        {
            volumePercent = SoundPictures.Clamp(percent);
            ApplyPreferences();
        }

        // Heard as a slider moves: the level only. Kept, scanned and logged once the value is chosen
        // (SetMasterVolumeFromSlider, ApplyChannelVolume) - a drag used to write the preferences,
        // find every body in the scene and log a line for each percent it passed through (PR-10).
        private void SetMasterVolumeLive(int percent)
        {
            volumePercent = SoundPictures.Clamp(percent);
            ApplyVolumesLive();
        }

        private void ApplyChannelVolumeLive(SoundChannel channel, int percent)
        {
            SetChannelShare(channel, percent);
            ApplyVolumesLive();
        }

        private void ApplyVolumesLive()
        {
            audioBed?.SetVolume(volumePercent / 100f);
            audioBed?.SetChannelVolumes(musicVolumePercent / 100f, effectsVolumePercent / 100f, interfaceVolumePercent / 100f);
        }

        private void SetChannelShare(SoundChannel channel, int percent)
        {
            percent = SoundPictures.Clamp(percent);
            if (channel == SoundChannel.Music) musicVolumePercent = percent;
            else if (channel == SoundChannel.Interface) interfaceVolumePercent = percent;
            else effectsVolumePercent = percent;
        }

        private void ApplyChannelVolume(SoundChannel channel, int percent)
        {
            SetChannelShare(channel, percent);
            ApplyPreferences();
        }

        /// <summary>The caption reading time in use (A10).</summary>
        public CaptionTime CaptionTimeSetting => captionTime;

        public void SetCaptionTime(CaptionTime time)
        {
            captionTime = time;
            ApplyPreferences();
            Render();
        }

        private void ApplyPreferences()
        {
            audioBed?.SetMuted(muted);
            audioBed?.SetVolume(volumePercent / 100f);
            audioBed?.SetReducedAudio(reducedAudio);
            audioBed?.SetChannelVolumes(musicVolumePercent / 100f, effectsVolumePercent / 100f, interfaceVolumePercent / 100f);
            ApplyMusic();
            cameraRig?.SetReducedMotion(reducedMotion);
            // Every surface that grows with the text, at the size chosen (A8): 1, 1.2 or 1.4.
            float scale = TextScale;
            if (hud != null) { hud.FontScale = scale; hud.ReducedMotion = reducedMotion; }
            if (sting != null) { sting.FontScale = scale; sting.CaptionTime = captionTime; }
            if (takeover != null) { takeover.FontScale = scale; takeover.CaptionTime = captionTime; }
            if (voteReveal != null) voteReveal.FontScale = scale;
            if (juryReveal != null) juryReveal.FontScale = scale;
            if (competitionCard != null) competitionCard.FontScale = scale;
            if (keyCeremony != null) keyCeremony.FontScale = scale;
            if (skipChip != null) skipChip.FontScale = scale;
            if (tutorial != null) { tutorial.FontScale = scale; tutorial.ReducedMotion = reducedMotion; }
            if (opening != null) opening.FontScale = scale;
            if (seasonReport != null) { seasonReport.FontScale = scale; seasonReport.ReducedMotion = reducedMotion; }
            if (weeklyRecap != null) weeklyRecap.FontScale = scale;
            if (castSelect != null) { castSelect.FontScale = scale; castSelect.ReducedMotion = reducedMotion; }
            if (characterCreator != null) characterCreator.FontScale = scale;
            if (mainMenu != null) mainMenu.FontScale = scale;
            // And the names over the houseguests' heads, which were fixed at one size.
            HouseNpc.NameScale = scale;
            foreach (var visual in FindObjectsByType<CharacterPresentation>()) visual.SetReducedMotion(reducedMotion);
            ApplyDisplayPreferences();
            // Kept in the session's store (A13): PlayerPrefs, the root's own file, or nowhere.
            var store = Preferences;
            store.SetInt("Gamesim.Muted", muted ? 1 : 0); store.SetInt("Gamesim.ReducedMotion", reducedMotion ? 1 : 0); store.SetInt("Gamesim.ReducedAudio", reducedAudio ? 1 : 0); store.SetInt("Gamesim.Volume", volumePercent); store.SetInt("Gamesim.Music", musicOn ? 1 : 0); store.SetInt("Gamesim.CeremonyPace", ceremonyPace == CeremonyPace.Quick ? 1 : 0);
            // The size as 0 to 2, and the boolean an earlier build reads beside it.
            store.SetInt(TextSizes.SizeKey, (int)textSize); store.SetInt(TextSizes.LegacyKey, TextSizes.LegacyRecord(textSize));
            store.SetInt(CaptionTiming.Key, (int)captionTime);
            store.SetInt(SoundPictures.MusicVolumeKey, musicVolumePercent); store.SetInt(SoundPictures.EffectsVolumeKey, effectsVolumePercent);
            store.SetInt(SoundPictures.InterfaceVolumeKey, interfaceVolumePercent);
            store.Save();
            // And a line in the player log for each value that changed since the last one logged.
            LogSettingChanges();
        }
    }
}
