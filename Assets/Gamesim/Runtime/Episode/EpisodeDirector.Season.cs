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
        /// state it is already in, so this does not restart the track on every render.</para>
        /// </summary>
        private void ApplyMusic()
        {
            if (audioBed == null) return;
            if (!musicOn) { audioBed.SetMusic(HouseAudio.Music.Silent); return; }

            bool setup = (mainMenu != null && mainMenu.IsShowing)
                         || (castSelect != null && castSelect.IsShowing)
                         || (characterCreator != null && characterCreator.IsShowing)
                         || (seasonReport != null && seasonReport.IsShowing);
            if (setup) { audioBed.SetMusic(HouseAudio.Music.Silent); return; }

            // The opening carries the theme, and the season takes over when it ends.
            bool titles = opening != null && opening.IsPlaying;
            audioBed.SetMusic(titles ? HouseAudio.Music.Theme : HouseAudio.Music.Season);
        }

        private void Settings(EpisodeState state)
        {
            hud.PanelTitle(blockedRecovery ? "SAVE RECOVERY" : "SETTINGS & SAVES", "Offline play is available. No credentials or online connection are required.");
            hud.Paragraph("Slot: " + saves.SavePath);
            hud.Paragraph(message);
            hud.Action("Save now  [F5]", SaveNow);
            hud.Action("Reload current slot", LoadNow);
            hud.Action("Recover validated backup (preserve current file)", RecoverBackup);
            hud.Action("New season in a NEW slot (preserves this season)", NewSeason);
            hud.Action("Main menu", OpenMainMenu);
            hud.Meter("Master volume", volumePercent, 100, muted ? UiTheme.Muted : UiTheme.Accent);
            hud.Action("Volume down", () => { volumePercent = Mathf.Max(0, volumePercent - 10); ApplyPreferences(); Render(); });
            hud.Action("Volume up", () => { volumePercent = Mathf.Min(100, volumePercent + 10); ApplyPreferences(); Render(); });
            hud.Action(muted ? "Turn sound on" : "Mute sound", () => { muted = !muted; ApplyPreferences(); Render(); });
            hud.Action(musicOn ? "Turn music off" : "Turn music on", () => { musicOn = !musicOn; ApplyPreferences(); Render(); });
            hud.Action(reducedMotion ? "Enable character motion" : "Reduce character motion", () => { reducedMotion = !reducedMotion; ApplyPreferences(); Render(); });
            hud.Action(reducedAudio ? "Full sound" : "Reduce sound", () => { reducedAudio = !reducedAudio; ApplyPreferences(); Render(); });
            hud.Action(largeText ? "Use standard text" : "Use larger text", () => { largeText = !largeText; ApplyPreferences(); Render(); });
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
            castSelect.Show(StartSeason, back, characterCreator == null ? (Action<SeasonBuilder.Choice, CharacterDraft>)null
                : (choice, draft) => characterCreator.Show(choice, draft, StartSeason, castSelect.Resume));
        }

        /// <summary>
        /// Stages and installs a season. A null choice builds the authored six-person scenario,
        /// which is what a headless caller and the pre-cast-screen behaviour both get.
        /// </summary>
        public void StartSeason(SeasonBuilder.Choice choice)
        {
            if (durableCommitInProgress) return;
            SuspendNpcWorldWithoutSaving();
            try
            {
                var seed = unchecked((uint)DateTime.UtcNow.Ticks);
                var nextStore = new EpisodeSaveStore(Path.Combine(saveRoot, "episode-" + Guid.NewGuid().ToString("N") + ".json"));
                var fresh = choice == null ? ContentCatalog.Create(seed) : SeasonBuilder.Create(choice, seed);
                fresh.sessionId = Guid.NewGuid().ToString("N");
                nextStore.Save(fresh); // Stage and validate on disk before replacing the current in-memory session.
                saves = nextStore; Install(fresh);
                if (SaveRootOverride == null) { PlayerPrefs.SetString("Gamesim.ActiveSave", Path.GetFileName(saves.SavePath)); PlayerPrefs.Save(); }
                message = SeasonMessage(fresh, choice);
            }
            catch (Exception error) when (error is IOException || error is InvalidDataException || error is UnauthorizedAccessException || error is ArgumentException)
            { message = "New season could not be saved. Your current session and slot were preserved. " + error.Message; }
            Render();
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
            catch (Exception error) when (error is IOException || error is InvalidDataException || error is UnauthorizedAccessException || error is ArgumentException) { message = "Import did not change your current slot: " + error.Message; }
            Render();
        }

        private void Install(EpisodeState state)
        {
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
            if (competitionCard != null) competitionCard.FontScale = largeText ? 1.2f : 1;
            if (keyCeremony != null) keyCeremony.FontScale = largeText ? 1.2f : 1;
            if (tutorial != null) tutorial.FontScale = largeText ? 1.2f : 1;
            if (opening != null) opening.FontScale = largeText ? 1.2f : 1;
            if (seasonReport != null) seasonReport.FontScale = largeText ? 1.2f : 1;
            if (castSelect != null) castSelect.FontScale = largeText ? 1.2f : 1;
            if (characterCreator != null) characterCreator.FontScale = largeText ? 1.2f : 1;
            if (mainMenu != null) mainMenu.FontScale = largeText ? 1.2f : 1;
            foreach (var visual in FindObjectsByType<CharacterPresentation>()) visual.SetReducedMotion(reducedMotion);
            ApplyDisplayPreferences();
            if (SaveRootOverride == null)
            { PlayerPrefs.SetInt("Gamesim.Muted", muted ? 1 : 0); PlayerPrefs.SetInt("Gamesim.ReducedMotion", reducedMotion ? 1 : 0); PlayerPrefs.SetInt("Gamesim.ReducedAudio", reducedAudio ? 1 : 0); PlayerPrefs.SetInt("Gamesim.LargeText", largeText ? 1 : 0); PlayerPrefs.SetInt("Gamesim.Volume", volumePercent); PlayerPrefs.SetInt("Gamesim.Music", musicOn ? 1 : 0); PlayerPrefs.Save(); }
        }
    }
}
