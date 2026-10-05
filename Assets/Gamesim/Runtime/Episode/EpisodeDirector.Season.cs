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
                opening != null && opening.MusicHeld, opening != null && opening.MusicClosing));
        }

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

        private void Settings(EpisodeState state)
        {
            // A menu, not a beat of the week: its own tall panel and its own head (mockup
            // language: the glyph, the title, the eyebrow), with its rows under section labels.
            hud.SetActivityLayout(EpisodeHud.ActivityLayout.Settings);
            hud.ScreenHeader(EpisodeHud.SettingsHeaderName, "OFFLINE \u00b7 NO ACCOUNT NEEDED",
                blockedRecovery ? "SAVE RECOVERY" : "SETTINGS & SAVES", UiTheme.Icon("settings"));
            hud.Aside("Offline play is available. No credentials or online connection are required.");
            hud.Section("YOUR SAVE");
            // The slot's name, not its path: this panel used to print the full file path,
            // which on Windows contains the player's user name, and which they can do
            // nothing with. The name is the part that tells one slot from another.
            hud.Paragraph("Slot: " + Path.GetFileNameWithoutExtension(saves.SavePath));
            hud.Aside(message);
            hud.Action("Save now  [F5]", SaveNow);
            hud.Action("Reload current slot", LoadNow);
            hud.Action("Recover validated backup (preserve current file)", RecoverBackup);
            hud.Action("New season in a NEW slot (preserves this season)", NewSeason);
            hud.Action("Main menu", OpenMainMenu);
            hud.Section("SOUND");
            hud.Meter("Master volume", volumePercent, 100, muted ? UiTheme.Muted : UiTheme.Accent);
            hud.Action("Volume down", () => { volumePercent = Mathf.Max(0, volumePercent - 10); ApplyPreferences(); Render(); });
            hud.Action("Volume up", () => { volumePercent = Mathf.Min(100, volumePercent + 10); ApplyPreferences(); Render(); });
            hud.Action(muted ? "Turn sound on" : "Mute sound", () => { muted = !muted; ApplyPreferences(); Render(); });
            hud.Action(musicOn ? "Turn music off" : "Turn music on", () => { musicOn = !musicOn; ApplyPreferences(); Render(); });
            hud.Section("ACCESSIBILITY");
            hud.Action(reducedMotion ? "Enable character motion" : "Reduce character motion", () => { reducedMotion = !reducedMotion; ApplyPreferences(); Render(); });
            hud.Action(reducedAudio ? "Full sound" : "Reduce sound", () => { reducedAudio = !reducedAudio; ApplyPreferences(); Render(); });
            hud.Action(largeText ? "Use standard text" : "Use larger text", () => SetLargeText(!largeText));
            hud.Section("CEREMONIES");
            // The key ceremony and the live eviction reveal one name at a time; suspenseful holds a
            // beat on each, quick keeps them brisk. Either can be sped up or skipped while it plays.
            hud.Paragraph(ceremonyPace == CeremonyPace.Suspenseful
                ? "Keys and votes are revealed one at a time, with a pause before the last. Press Space to speed a reveal up, or Enter to skip to the result."
                : "Keys and votes are revealed quickly. Press Space to speed a reveal up, or Enter to skip to the result.");
            hud.Action(ceremonyPace == CeremonyPace.Suspenseful ? QuickCeremoniesCaption : SuspensefulCeremoniesCaption,
                () => { SetCeremonyPace(ceremonyPace == CeremonyPace.Suspenseful ? CeremonyPace.Quick : CeremonyPace.Suspenseful); Render(); });
            DisplaySettings();
            CareerSettings();
            hud.Paragraph("All dialogue and ceremony information is captioned. Mouse buttons and keyboard alternatives are available; precision competitions have an untimed assisted option.");
            hud.Heading("Import a supported web save");
            hud.Paragraph("Supports receipt-free, six-active-cast social snapshots. Complex in-progress web saves are rejected and archived unchanged, never silently simplified.");
            hud.PathInput("Full path to exported JSON", ImportFile);
            hud.Paragraph("Optional cloud login and generated AI dialogue are not configured. The local episode never waits for those services.");
        }

        public void SaveNow()
        {
            if (!blockedRecovery && !durableCommitInProgress)
            {
                // An explicit retry can save an unchanged session after an I/O failure.
                // A NEW failed tick must never fall through to a second, older write.
                if (npcSaveSuspended) TryAutosave();
                else if (FlushNpcWholeTicks() && !blockedRecovery && !npcSaveSuspended) TryAutosave();
            }
            Render();
        }

        private void TryAutosave()
        {
            if (TryPersistCandidate(engine.Snapshot, out _, out var failure)) message += "  ·  Saved locally.";
            else { message = failure; PauseNpcSocialForPanel(); }
        }

        public void LoadNow()
        {
            if (durableCommitInProgress) return;
            SuspendNpcWorldWithoutSaving();
            if (saves.TryLoad(out var state, out var result)) Install(state);
            message = result; Render();
        }

        public void RecoverBackup()
        {
            if (durableCommitInProgress) return;
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
            mainMenu.Show(canContinue, blockedRecovery ? message : null,
                CloseMainMenu, NewSeason, OpenSettingsFromMenu, QuitGame, CareerLine());
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
            OpenSettings();
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
            // The cast screen draws below the menu, so the menu has to step aside rather than sit on
            // top of it — and backing out has to land wherever the player came from, which is the
            // menu when they arrived through it and the settings panel when they did not.
            bool fromMenu = mainMenu != null && mainMenu.IsShowing;
            if (fromMenu) mainMenu.Hide();
            Action back = fromMenu ? (Action)OpenMainMenu : Render;
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
                    if (castSelect.PreferredCreatorMode == CharacterCreator.EntryMode.Quick)
                        characterCreator.ShowQuick(choice, draft, commit, returnToCast);
                    else characterCreator.Show(choice, draft, commit, returnToCast);
                });
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
        {
            if (durableCommitInProgress) return;
            SuspendNpcWorldWithoutSaving();
            bool installed = false;
            try
            {
                var seed = unchecked((uint)DateTime.UtcNow.Ticks);
                var nextStore = new EpisodeSaveStore(Path.Combine(saveRoot, "episode-" + Guid.NewGuid().ToString("N") + ".json"));
                var fresh = choice == null ? ContentCatalog.Create(seed) : SeasonBuilder.Create(choice, seed);
                fresh.competitionRulesVersion = CompetitionRules.Current;
                fresh.haveNotRulesStartWeek = 1;
                fresh.strategyRulesStartWeek = 1;
                // Every season the director starts plays under the story system from week one:
                // arcs, grudges, lore, bonds and production. Seasons built directly by tests and
                // the default scene engine stay off unless they switch it on themselves.
                EpisodeEngine.EnableStory(fresh);
                EpisodeEngine.EnableRead(fresh);
                EpisodeEngine.EnableLevers(fresh);
                EpisodeEngine.EnableWeek(fresh);
                EpisodeEngine.EnableEconomy(fresh);
                // NPC agency from week one, and with it the house's first impressions of each other
                // and of the player's persona (NPC-AGENCY-PLAN.md §2).
                EpisodeEngine.EnableAgency(fresh);
                // The finale rules (ENDGAME-PLAN §3): history questions, the five responses, the argument.
                EpisodeEngine.EnableFinale(fresh);
                // The commitment rules (ACTIONS-DEALS-ALLIANCES-PLAN R0, C0): study costs the window's
                // action, a whisper reaches who it is told to, a breach counts against whoever broke it.
                EpisodeEngine.EnableCommitments(fresh);
                CharacterAppearanceSnapshots.Materialize(fresh);
                fresh.sessionId = Guid.NewGuid().ToString("N");
                nextStore.Save(fresh); // Stage and validate on disk before replacing the current in-memory session.
                saves = nextStore; Install(fresh);
                if (SaveRootOverride == null) { PlayerPrefs.SetString("Gamesim.ActiveSave", Path.GetFileName(saves.SavePath)); PlayerPrefs.Save(); }
                message = SeasonMessage(fresh, choice);
                installed = true;
            }
            catch (Exception error) when (error is IOException || error is InvalidDataException || error is UnauthorizedAccessException || error is ArgumentException)
            {
                message = "New season could not be saved. Your current session and slot were preserved. "
                    + SaveJson.Explain(error);
                failed?.Invoke(message);
            }
            Render();
            // Setup is over however the season was started. The cast screen lets its live model go
            // when it starts one itself; a start from the creator leaves it asleep behind, holding
            // the last houseguest it showed - body, textures and target - for the whole season.
            if (installed && castSelect != null) castSelect.ReleasePreview();
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
            SuspendNpcWorldWithoutSaving();
            try
            {
                if (!File.Exists(path) || new FileInfo(path).Length > 8 * 1024 * 1024) { message = "Choose an existing JSON file smaller than eight MiB."; Render(); return; }
                if (WebSaveImporter.TryImport(File.ReadAllText(path), Path.Combine(saveRoot,"Imports"), out var state, out var result))
                {
                    var importedSlot = new EpisodeSaveStore(Path.Combine(saveRoot,"import-" + Guid.NewGuid().ToString("N") + ".json"));
                    importedSlot.Save(state); saves = importedSlot; Install(state);
                    if (SaveRootOverride == null) { PlayerPrefs.SetString("Gamesim.ActiveSave", Path.GetFileName(saves.SavePath)); PlayerPrefs.Save(); }
                }
                message = result;
            }
            catch (Exception error) when (error is IOException || error is InvalidDataException || error is UnauthorizedAccessException || error is ArgumentException)
            { message = "Import did not change your current slot. " + SaveJson.Explain(error); }
            Render();
        }

        private void Install(EpisodeState state)
        {
            // A season replaced under the opening ends it without recording anything: the beats
            // belonged to the season that is going.
            if (opening != null && opening.IsPlaying) opening.Cancel();
            ResetCeremonyTruth();
            ResetNpcSocialForLoad();
            ClosePanels(); engine = new EpisodeEngine(state); blockedRecovery = false;
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

        public bool LargeText => largeText;

        public void SetLargeText(bool enabled)
        {
            largeText = enabled;
            ApplyPreferences();
            Render();
        }

        private void ApplyPreferences()
        {
            audioBed?.SetMuted(muted);
            audioBed?.SetVolume(volumePercent / 100f);
            audioBed?.SetAmbienceEnabled(musicOn);
            audioBed?.SetReducedAudio(reducedAudio);
            ApplyMusic();
            cameraRig?.SetReducedMotion(reducedMotion);
            if (hud != null) { hud.FontScale = largeText ? 1.2f : 1; hud.ReducedMotion = reducedMotion; }
            if (sting != null) sting.FontScale = largeText ? 1.2f : 1;
            if (takeover != null) takeover.FontScale = largeText ? 1.2f : 1;
            if (voteReveal != null) voteReveal.FontScale = largeText ? 1.2f : 1;
            if (juryReveal != null) juryReveal.FontScale = largeText ? 1.2f : 1;
            if (competitionCard != null) competitionCard.FontScale = largeText ? 1.2f : 1;
            if (keyCeremony != null) keyCeremony.FontScale = largeText ? 1.2f : 1;
            if (skipChip != null) skipChip.FontScale = largeText ? 1.2f : 1;
            if (tutorial != null) { tutorial.FontScale = largeText ? 1.2f : 1; tutorial.ReducedMotion = reducedMotion; }
            if (opening != null) opening.FontScale = largeText ? 1.2f : 1;
            if (seasonReport != null) { seasonReport.FontScale = largeText ? 1.2f : 1; seasonReport.ReducedMotion = reducedMotion; }
            if (weeklyRecap != null) weeklyRecap.FontScale = largeText ? 1.2f : 1;
            if (castSelect != null) { castSelect.FontScale = largeText ? 1.2f : 1; castSelect.ReducedMotion = reducedMotion; }
            if (characterCreator != null) characterCreator.FontScale = largeText ? 1.2f : 1;
            if (mainMenu != null) mainMenu.FontScale = largeText ? 1.2f : 1;
            foreach (var visual in FindObjectsByType<CharacterPresentation>()) visual.SetReducedMotion(reducedMotion);
            ApplyDisplayPreferences();
            if (SaveRootOverride == null)
            { PlayerPrefs.SetInt("Gamesim.Muted", muted ? 1 : 0); PlayerPrefs.SetInt("Gamesim.ReducedMotion", reducedMotion ? 1 : 0); PlayerPrefs.SetInt("Gamesim.ReducedAudio", reducedAudio ? 1 : 0); PlayerPrefs.SetInt("Gamesim.LargeText", largeText ? 1 : 0); PlayerPrefs.SetInt("Gamesim.Volume", volumePercent); PlayerPrefs.SetInt("Gamesim.Music", musicOn ? 1 : 0); PlayerPrefs.SetInt("Gamesim.CeremonyPace", ceremonyPace == CeremonyPace.Quick ? 1 : 0); PlayerPrefs.Save(); }
        }
    }
}
