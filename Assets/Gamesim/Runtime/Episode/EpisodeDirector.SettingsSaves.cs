using System;
using System.Collections.Generic;
using System.IO;
using Gamesim.Persistence;
using Gamesim.Presentation;
using UnityEngine.EventSystems;

namespace Gamesim.Episode
{
    /// <summary>
    /// The Settings frame's Saves and Advanced sections (menu slice 4, the review's change 2).
    ///
    /// <para>Saves lists every saved season in the save folder as a card, newest first: the running
    /// season's first, marked "Playing now", with its Save now; every other with Load and Delete, or
    /// Delete alone when it cannot be opened. Load asks first while a season is running ("Your current
    /// season is saved first") and loads at once from the main menu; it goes through the same calls
    /// a new season and an import switch seasons with (<see cref="SwitchToSeason"/>). Delete asks
    /// every time and goes through the store's one delete (EpisodeSaveStore.DeleteSeason). Listing
    /// reads and never writes (SeasonShelf).</para>
    ///
    /// <para>Advanced holds the tools that put back what is saved over what is played - going back
    /// to the last save, restoring the backup, setting the career record aside - each picked, then
    /// confirmed on a card whose safe choice has the keyboard and whose other button names the result.</para>
    /// </summary>
    public sealed partial class EpisodeDirector
    {
        /// <summary>
        /// The panel's words in plain English (the menu review's quick win 13). They spoke of a
        /// "validated backup", "receipt-free, six-active-cast social snapshots" and a cloud login
        /// and AI dialogue that "are not configured"; that the game needs no connection is said
        /// once, by the eyebrow at the top.
        /// </summary>
        public const string RecoverBackupCaption = "Recover backup (keeps a copy of this save)";
        public const string NewSeasonInNewSlotCaption = "New season in a new slot (keeps this season)";
        public const string ImportHeading = "IMPORT A WEB SAVE";
        public const string ImportLine = "Bring in a season saved by the web version of the game. Only simple saves come in: six houseguests still playing, saved between ceremonies, with no ceremony results to carry over. Any other save is set aside unchanged, never cut down to fit.";

        /// <summary>The seasons in the save folder, read again only where a file changed.</summary>
        private readonly SeasonShelf shelf = new SeasonShelf();
        /// <summary>The season whose Load or Delete is asking (its save's path), or null.</summary>
        private string savesLoadPick, savesDeletePick;
        /// <summary>The card a load or a delete failed on, and why: the line under it says so.</summary>
        private string savesFailurePath, savesFailure;
        /// <summary>The Advanced tool asking, or none.</summary>
        private AdvancedPick advancedPick;
        /// <summary>The pick whose confirm card has been drawn once: later renders keep the keyboard where the player put it.</summary>
        private string confirmDrawnFor;
        /// <summary>The cards the last Saves render drew under the running one, in order: each season's path and its first control.</summary>
        private readonly List<(string path, string control)> drawnSeasons = new List<(string, string)>();

        private enum AdvancedPick { None, GoBack, Restore }

        /// <summary>The cards Saves shows below the running season's, newest first, as the player sees them.</summary>
        public IReadOnlyList<SavedSeason> SavedSeasonsShown => shelf.Read(saveRoot);

        /// <summary>
        /// Whether a season of the player's is running: in the house, or kept behind the main menu for
        /// its Continue. Not in blocked recovery, and not behind a first launch's main menu, whose
        /// house holds a stand-in season nobody played (it is written nowhere until a season starts).
        /// </summary>
        private bool SeasonIsRunning => !blockedRecovery && engine != null && saves != null
            && (File.Exists(saves.SavePath) || File.Exists(saves.BackupPath)
                || mainMenu == null || !mainMenu.HasShown || mainMenu.OffersContinue);

        /// <summary>The picks and failure lines go as Settings open or turn to another section.</summary>
        private void ForgetSavesPicks()
        {
            savesLoadPick = savesDeletePick = null;
            savesFailurePath = savesFailure = null;
            advancedPick = AdvancedPick.None;
            confirmDrawnFor = null;
        }

        // ------------------------------------------------------------------ Saves

        /// <summary>
        /// Saves: the line about the last thing done, the season cards, a new season beside them, and
        /// a season from the web version. In blocked recovery, where there is no game menu, the way to
        /// the main menu too.
        /// </summary>
        private void SavesSection()
        {
            hud.Aside(message);
            drawnSeasons.Clear();
            bool running = SeasonIsRunning;
            string playing = saves != null ? saves.SavePath : null;
            int cards = 0;
            if (running) { RunningSeasonCard(); cards++; }
            foreach (var season in shelf.Read(saveRoot))
            {
                bool own = SamePath(season.Path, playing);
                if (running && own) continue;
                SeasonCardFor(season, own);
                cards++;
            }
            if (cards == 0) hud.Paragraph(SavesWords.NoSaves);
            hud.Action(NewSeasonInNewSlotCaption, NewSeason);
            // The game menu card has the way to the main menu; a save in recovery has no card.
            if (blockedRecovery) hud.Action(SettingsWords.MainMenuCaption, OpenMainMenu);
            hud.SectionHeading(ImportHeading);
            hud.Paragraph(ImportLine);
            hud.PathInput("Full path to exported JSON", ImportFile);
        }

        /// <summary>The running season's card, from memory: "Playing now", its Save now, and no Delete.</summary>
        private void RunningSeasonCard()
        {
            var state = projected ?? engine.Snapshot;
            DateTime? savedAt = WrittenAt(saves.SavePath);
            var season = SavedSeason.Of(state, saves.SavePath, savedAt);
            hud.SeasonCard(season.Key, Title(season), SavesWords.Details(season.Week, season.Left), SavedLine(savedAt), true,
                new[] { new EpisodeHud.SeasonControl { Caption = SavesWords.SaveNowCaption, Name = SavesWords.SaveNowCaption,
                    Description = SavesWords.SaveNowCaption + ", " + Title(season) + ", " + SavesWords.Week(season.Week), Press = SaveNow } });
            hud.MutedLine(SavesWords.PlayingLine);
        }

        /// <summary>
        /// A saved season's card: Load and Delete, or Delete alone when it cannot be opened. The save
        /// blocked recovery could not open is the running one's and has no Delete. The confirm card and
        /// any failure line go under the card that asked.
        /// </summary>
        private void SeasonCardFor(SavedSeason season, bool own)
        {
            var controls = new List<EpisodeHud.SeasonControl>();
            string title = Title(season);
            string said = season.Readable ? title + ", " + SavesWords.Week(season.Week) : title;
            var shown = season;
            // Held by another program for a moment: may be a good save, so no Delete, and one way to look again.
            if (season.InUse)
                controls.Add(new EpisodeHud.SeasonControl { Caption = SavesWords.TryAgainCaption, Name = SavesWords.ControlName(SavesWords.TryAgainCaption, season.Key),
                    Description = SavesWords.TryAgainCaption + ", " + said, Press = () => LookAgain(shown) });
            else if (season.Readable)
                controls.Add(new EpisodeHud.SeasonControl { Caption = SavesWords.LoadCaption, Name = SavesWords.ControlName(SavesWords.LoadCaption, season.Key),
                    Description = SavesWords.LoadCaption + " " + said, Press = () => PressLoad(shown) });
            if (!own && !season.InUse)
                controls.Add(new EpisodeHud.SeasonControl { Caption = SavesWords.DeleteCaption, Name = SavesWords.ControlName(SavesWords.DeleteCaption, season.Key),
                    Description = SavesWords.DeleteCaption + " " + said, Press = () => PressDelete(shown), Danger = true });
            hud.SeasonCard(season.Key, title, season.Readable ? SavesWords.Details(season.Week, season.Left) : null,
                SavedLine(season.SavedUtc), false, controls);
            if (controls.Count > 0) drawnSeasons.Add((season.Path, controls[0].Name));
            if (season.InUse) hud.MutedLine(SavesWords.InUseLine);
            // Only blocked recovery lists the running slot's own save: when it cannot be opened, Advanced mends it.
            else if (own && !season.Readable) hud.MutedLine(SavesWords.RecoveryLine);

            if (SamePath(savesLoadPick, season.Path))
                SavesConfirm("load:" + season.Path, SavesWords.LoadQuestion(season.PlayerName, season.Week),
                    SavesWords.KeepPlaying, () => { savesLoadPick = null; RenderAndFocus(SavesWords.ControlName(SavesWords.LoadCaption, season.Key)); },
                    SavesWords.SaveAndLoad, () => LoadSeasonFromShelf(shown));
            else if (SamePath(savesDeletePick, season.Path))
                SavesConfirm("delete:" + season.Path,
                    season.Readable ? SavesWords.DeleteQuestion(season.PlayerName, season.Week) : SavesWords.DeleteUnreadableQuestion,
                    SavesWords.KeepIt, () => { savesDeletePick = null; RenderAndFocus(SavesWords.ControlName(SavesWords.DeleteCaption, season.Key)); },
                    SavesWords.DeleteThisSeason, () => DeleteFromShelf(shown));
            if (SamePath(savesFailurePath, season.Path) && !string.IsNullOrEmpty(savesFailure))
                hud.MutedLine(savesFailure, SavesWords.FailureLineName);
        }

        /// <summary>A held save's Try again: the list looks again, with the keyboard on that card's first control as it now reads.</summary>
        private void LookAgain(SavedSeason season)
        {
            if (season == null) return;
            Render();
            int at = drawnSeasons.FindIndex(card => SamePath(card.path, season.Path));
            if (at >= 0) hud.FocusWhenWired(drawnSeasons[at].control);
        }

        /// <summary>A card's Load: at once from the main menu or with no season running; otherwise it asks first.</summary>
        private void PressLoad(SavedSeason season)
        {
            if (durableCommitInProgress || season == null) return;
            savesFailurePath = savesFailure = null;
            if (settingsFromMainMenu || !SeasonIsRunning) { LoadSeasonFromShelf(season); return; }
            ArmSaves(load: season.Path, delete: null);
            RenderAndFocus(null);
        }

        /// <summary>A card's Delete: always asks first.</summary>
        private void PressDelete(SavedSeason season)
        {
            if (durableCommitInProgress || season == null) return;
            savesFailurePath = savesFailure = null;
            ArmSaves(load: null, delete: season.Path);
            RenderAndFocus(null);
        }

        private void ArmSaves(string load, string delete)
        {
            savesLoadPick = load; savesDeletePick = delete;
            advancedPick = AdvancedPick.None; careerResetArmed = false;
            confirmDrawnFor = null;
        }

        /// <summary>
        /// Loads a listed season: the running one saved first (as Save now saves it) and nothing
        /// loaded when that fails; then the season read as Continue's season was read at launch
        /// (EpisodeSaveStore.TryLoad) and switched to as a new season or an import is
        /// (<see cref="SwitchToSeason"/>). A season that will not load changes nothing.
        /// </summary>
        private void LoadSeasonFromShelf(SavedSeason season)
        {
            if (durableCommitInProgress || season == null) return;
            string path = season.Path;
            if (SeasonIsRunning && !SaveSessionBeforeLeaving())
            {
                savesFailurePath = path;
                savesFailure = SavesWords.CouldNotSave + (string.IsNullOrEmpty(sessionSaveFailure) ? "" : " " + sessionSaveFailure);
                RenderAndFocus(null);
                return;
            }
            var store = new EpisodeSaveStore(path);
            if (!store.TryLoad(out var state, out var result))
            {
                savesLoadPick = null;
                savesFailurePath = path;
                // Its own line only: the store's words can point at Recover backup, which mends the running season, not this one.
                savesFailure = SavesWords.CouldNotLoad;
                RenderAndFocus(null);
                return;
            }
            ForgetSavesPicks();
            BeginSystemNotice();
            SuspendNpcWorldWithoutSaving();
            SwitchToSeason(store, state);
            message = result;
            Render();
            // As Continue hands a season back: its opening, if it is still the first night, or the tour.
            PlayOpening();
        }

        /// <summary>The second press of a Delete: the store's one delete, then the list without it and the keyboard on a neighbour.</summary>
        private void DeleteFromShelf(SavedSeason season)
        {
            if (durableCommitInProgress || season == null) return;
            savesDeletePick = null; confirmDrawnFor = null;
            var result = EpisodeSaveStore.DeleteSeason(saveRoot, season.Path, saves != null ? saves.SavePath : null);
            if (result != SeasonDeleteResult.Deleted)
            {
                savesFailurePath = season.Path;
                savesFailure = result == SeasonDeleteResult.InUse ? SavesWords.InUse
                    : result == SeasonDeleteResult.Playing ? SavesWords.PlayingNotDeleted : SavesWords.NotDeleted;
                RenderAndFocus(SavesWords.ControlName(SavesWords.DeleteCaption, season.Key));
                return;
            }
            message = season.Readable ? SavesWords.Deleted(season.PlayerName, season.Week) : SavesWords.DeletedUnreadable;
            // The card after it, or else the one before, or else the running season's Save now, or else the list's entry.
            int at = drawnSeasons.FindIndex(card => SamePath(card.path, season.Path));
            string focus = at >= 0 && at + 1 < drawnSeasons.Count ? drawnSeasons[at + 1].control
                : at > 0 ? drawnSeasons[at - 1].control
                : SeasonIsRunning ? SavesWords.SaveNowCaption : SettingsWords.TabName(SettingsSection.Saves);
            RenderAndFocus(focus);
        }

        // ------------------------------------------------------------------ Advanced

        /// <summary>
        /// Advanced: the tools that put back what is saved over what is played - the last save, the
        /// backup, the career record - each picked, then confirmed.
        /// </summary>
        private void AdvancedSection()
        {
            hud.Aside(message);
            bool running = SeasonIsRunning && !settingsFromMainMenu;
            hud.SettingAction(SavesWords.GoBackCaption, () => ArmAdvanced(AdvancedPick.GoBack));
            if (advancedPick == AdvancedPick.GoBack)
                SavesConfirm("go-back", running ? SavesWords.GoBackQuestion : SavesWords.GoBackQuietQuestion,
                    running ? SavesWords.KeepPlaying : SavesWords.NotNow, () => DisarmAdvanced(SavesWords.GoBackCaption),
                    SavesWords.GoBackResult, () => { advancedPick = AdvancedPick.None; confirmDrawnFor = null; LoadNow(); });
            // Not offered where there is no backup file: greyed, with the reason under it.
            bool backup = HasBackupFile;
            hud.SettingAction(RecoverBackupCaption, () => { if (HasBackupFile) ArmAdvanced(AdvancedPick.Restore); }, backup ? null : SavesWords.NoBackupWhy);
            if (advancedPick == AdvancedPick.Restore && backup)
                SavesConfirm("restore", SavesWords.RestoreQuestion, SavesWords.KeepMySave, () => DisarmAdvanced(RecoverBackupCaption),
                    SavesWords.RestoreResult, () => { advancedPick = AdvancedPick.None; confirmDrawnFor = null; RecoverBackup(); });
            CareerSettings();
        }

        private void ArmAdvanced(AdvancedPick pick)
        {
            if (durableCommitInProgress) return;
            savesLoadPick = savesDeletePick = null;
            careerResetArmed = false;
            advancedPick = pick;
            confirmDrawnFor = null;
            RenderAndFocus(null);
        }

        private void DisarmAdvanced(string row)
        {
            advancedPick = AdvancedPick.None;
            confirmDrawnFor = null;
            RenderAndFocus(row);
        }

        // ------------------------------------------------------------------ pieces

        /// <summary>
        /// The kit's confirm card for the pick <paramref name="id"/>. Its first drawing puts the
        /// keyboard on the safe choice; a later render of the same card (a body finishing its assembly
        /// renders the panel at any moment) leaves it where the player put it.
        /// </summary>
        private void SavesConfirm(string id, string question, string safeCaption, Action safe, string resultCaption, Action result)
        {
            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            string held = confirmDrawnFor == id && selected != null ? selected.name : null;
            hud.SettingsConfirm(question, safeCaption, safe, resultCaption, result);
            if (held != null) hud.FocusWhenWired(held);
            confirmDrawnFor = id;
        }

        /// <summary>Draws the panel again, with the keyboard on the control named <paramref name="focus"/> when one is named.</summary>
        private void RenderAndFocus(string focus)
        {
            Render();
            if (!string.IsNullOrEmpty(focus)) hud.FocusWhenWired(focus);
        }

        /// <summary>
        /// Saves the running season as Save now does, without drawing, before it is left for another;
        /// whether it reached the disk.
        /// </summary>
        private bool SaveSessionBeforeLeaving()
        {
            if (durableCommitInProgress || blockedRecovery) return false;
            SaveSession();
            return !blockedRecovery && !npcSaveSuspended && sessionSaveFailure == null;
        }

        /// <summary>Whether the running slot has a backup file to restore (its name only: whether it reads is the restore's own check).</summary>
        private bool HasBackupFile
        {
            get
            {
                try { return saves != null && File.Exists(saves.BackupPath); }
                catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is ArgumentException) { return false; }
            }
        }

        private static string Title(SavedSeason season) =>
            season.InUse ? SavesWords.InUseTitle : !season.Readable ? SavesWords.Unreadable : string.IsNullOrWhiteSpace(season.PlayerName) ? SavesWords.NoName : season.PlayerName;

        /// <summary>"saved 2 hours ago" by the Settings clock, or "not saved yet".</summary>
        private static string SavedLine(DateTime? savedUtc) =>
            savedUtc.HasValue ? SavesWords.SavedAgo(savedUtc.Value, SettingsWords.Now.ToUniversalTime()) : SavesWords.NotSavedYet;

        /// <summary>When the file was last written, or null when there is none (read only).</summary>
        private static DateTime? WrittenAt(string path)
        {
            try { return !string.IsNullOrEmpty(path) && File.Exists(path) ? File.GetLastWriteTimeUtc(path) : (DateTime?)null; }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is ArgumentException) { return null; }
        }

        private static bool SamePath(string a, string b)
        {
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return false;
            try { return string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase); }
            catch (Exception error) when (error is ArgumentException || error is NotSupportedException || error is PathTooLongException) { return false; }
        }
    }
}
