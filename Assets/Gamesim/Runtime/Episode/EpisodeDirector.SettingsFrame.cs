using System;
using Gamesim.Presentation;
using Gamesim.Simulation;

namespace Gamesim.Episode
{
    /// <summary>
    /// Settings and saves as a frame of sections behind a game menu (menu slice 4, the review's
    /// change 2). The house's door - the rail's Settings row, Escape or Start with nothing open -
    /// opens a small card first: Resume, Save, Settings and the way to the main menu. Its Settings
    /// opens the frame: a list of sections (Saves, Sound, Display, Accessibility, Controls,
    /// Advanced) with one shown at a time, Saves first. The main menu and the creator's gear open
    /// the frame straight away, with no card, and a save that cannot be read (blocked recovery)
    /// shows it on Advanced, where the tools that mend a save are.
    ///
    /// <para>The ways out, by the shared helper (menu slice 2): the card's Close, Escape and B do
    /// what Resume does. The frame opened from the card goes "Back to the game menu" at its top left
    /// and its Close goes back to the house; opened from the main menu it goes "Back to the main
    /// menu", and nothing else; opened from the creator's gear its Close puts the creator back
    /// (EpisodeDirector.CreatorSettings.cs).</para>
    ///
    /// <para>What each section shows is its own method: SavesSection and AdvancedSection
    /// (EpisodeDirector.SettingsSaves.cs), SoundSection, DisplaySection, AccessibilitySection and
    /// ControlsSection (EpisodeDirector.SettingsPrefs.cs).</para>
    /// </summary>
    public sealed partial class EpisodeDirector
    {
        /// <summary>Whether the open Settings are showing the game menu card rather than the frame.</summary>
        private bool gameMenuShown;
        /// <summary>The section the frame shows.</summary>
        private SettingsSection settingsSection = SettingsSection.Saves;
        /// <summary>Whether the frame has been turned to Advanced for the blocked recovery on screen.</summary>
        private bool recoverySectionSet;
        /// <summary>When the card last saved the season, or null: the card says so.</summary>
        private DateTime? gameMenuSavedAt;
        /// <summary>Why the card's last Save did not save, or null.</summary>
        private string gameMenuSaveFailure;
        /// <summary>Whether the open frame was opened by the main menu (set as it opens): its way back goes there.</summary>
        private bool settingsFromMainMenu;
        /// <summary>Set by the main menu just before it opens Settings, and taken by the opening.</summary>
        private bool mainMenuDoorPending;

        /// <summary>Whether the game menu card is on screen.</summary>
        public bool IsGameMenuOpen => settingsOpen && gameMenuShown && !blockedRecovery;

        /// <summary>Whether the Settings frame - its sections - is on screen: opened, or blocked recovery's.</summary>
        public bool AreSettingsSectionsOpen => (settingsOpen && !gameMenuShown) || blockedRecovery;

        /// <summary>The section the frame shows (or would show, opened now).</summary>
        public SettingsSection SettingsSectionShown => settingsSection;

        /// <summary>The way back the frame shows at its top left, or null when it has none: from the card, from the main menu.</summary>
        public string SettingsWayBackCaption =>
            !AreSettingsSectionsOpen || blockedRecovery ? null
            : settingsFromMainMenu ? WayOut.BackTo(WayOut.MainMenu)
            // The creator's gear (and the frame it opened, come back to from a cast screen): Close is its way out.
            : settingsFromFrontDoor ? null
            : WayOut.BackTo(SettingsWords.GameMenuPlace);

        /// <summary>
        /// The Settings door. In the house - the rail's row, Escape or Start with nothing open - the
        /// game menu card; from the main menu or the creator's gear (which say so before they call
        /// this) and in blocked recovery, the frame on its first section.
        /// </summary>
        public void OpenSettings() => OpenSettingsOn(null);

        /// <summary>
        /// Settings on <paramref name="section"/>, as the card's Settings row and the list show it.
        /// Closed, they open straight on it - the frame, with the way back its door gives it (from the
        /// house, to the game menu) - in one render, as any panel opens; open, they turn to it.
        /// </summary>
        public void OpenSettingsAt(SettingsSection section)
        {
            if (settingsOpen || blockedRecovery) ShowSettingsSection(section);
            else OpenSettingsOn(section);
        }

        /// <summary>Opens Settings: on <paramref name="section"/> when one is named, otherwise by the door (the card in the house).</summary>
        private void OpenSettingsOn(SettingsSection? section)
        {
            PauseNpcSocialForPanel();
            ClosePanels();
            settingsFromMainMenu = mainMenuDoorPending && settingsFromFrontDoor;
            mainMenuDoorPending = false;
            settingsOpen = true;
            gameMenuShown = section == null && !settingsFromFrontDoor && !blockedRecovery;
            gameMenuSavedAt = null; gameMenuSaveFailure = null;
            ForgetSavesPicks();
            settingsSection = section ?? SettingsWords.OpeningSection(blockedRecovery);
            if (blockedRecovery && section != null) recoverySectionSet = true;
            player.SetInputEnabled(false);
            cameraRig.ControlsEnabled = false;
            RefreshDisplayRecordFromWindow();
            Render();
            // The card opens on its main button; the frame on its lit section.
            hud.FocusWhenWired(gameMenuShown ? EpisodeHud.GameMenuRowPrefix + SettingsWords.ResumeCaption : SettingsWords.TabName(settingsSection));
        }

        /// <summary>Turns the frame to <paramref name="section"/> (from the card too), with the keyboard on its entry in the list.</summary>
        public void ShowSettingsSection(SettingsSection section)
        {
            if (!settingsOpen && !blockedRecovery) return;
            gameMenuShown = false;
            ForgetSavesPicks();
            settingsSection = section;
            if (blockedRecovery) recoverySectionSet = true;
            Render();
            hud.FocusWhenWired(SettingsWords.TabName(section));
        }

        /// <summary>The frame opened from the card goes back to it, with the keyboard on its Settings row.</summary>
        public void BackToGameMenu()
        {
            if (!settingsOpen || blockedRecovery) return;
            gameMenuShown = true;
            Render();
            hud.FocusWhenWired(EpisodeHud.GameMenuRowPrefix + SettingsWords.SettingsCaption);
        }

        /// <summary>The frame the main menu opened goes back there (it closed onto the house before slice 4).</summary>
        private void BackToMainMenuFromSettings() => OpenMainMenu();

        /// <summary>Called by the main menu's Settings just before it opens them: their way back is "Back to the main menu".</summary>
        private void MarkSettingsFromMainMenu() => mainMenuDoorPending = true;

        /// <summary>Settings, as Render draws them: the card, or the frame on its section.</summary>
        private void Settings(EpisodeState state)
        {
            hud.SetActivityLayout(EpisodeHud.ActivityLayout.Settings);
            if (IsGameMenuOpen) { GameMenu(); return; }
            // A save that cannot be read opens the frame on Advanced, once: the player may turn it after.
            if (!blockedRecovery) recoverySectionSet = false;
            else if (!recoverySectionSet) { settingsSection = SettingsWords.OpeningSection(true); recoverySectionSet = true; }
            // A menu, not a beat of the week: its own tall panel and its own head (mockup language:
            // the glyph, the title, the eyebrow). That the game is offline is said once, by the
            // eyebrow over the title (SettingsEyebrow).
            hud.ScreenHeader(EpisodeHud.SettingsHeaderName, SettingsEyebrow(),
                blockedRecovery ? "SAVE RECOVERY" : "SETTINGS & SAVES", UiTheme.Icon("settings"));
            hud.SettingsSections(SettingsWords.Sections, settingsSection, ShowSettingsSection);
            string back = SettingsWayBackCaption;
            if (back != null)
            {
                if (settingsFromMainMenu) { hud.WayBack(back, BackToMainMenuFromSettings); hud.HidePanelClose(); }
                else hud.WayBack(back, BackToGameMenu);
            }
            hud.SectionHeading(SettingsWords.Heading(settingsSection));
            switch (settingsSection)
            {
                case SettingsSection.Saves: SavesSection(); break;
                case SettingsSection.Sound: SoundSection(); break;
                case SettingsSection.Display: DisplaySection(); break;
                case SettingsSection.Accessibility: AccessibilitySection(); break;
                case SettingsSection.Controls: ControlsSection(); break;
                default: AdvancedSection(); break;
            }
        }

        /// <summary>
        /// The game menu card: "PAUSED" over its title only while the house's clock is held, Resume
        /// (the one main button), Save (which stays and says when it saved), Settings, and the way to
        /// the main menu with what it keeps. Its Close, Escape and B resume, as Resume does.
        /// </summary>
        private void GameMenu()
        {
            hud.GameMenuCard(IsHouseClockHeld);
            hud.GameMenuRow(SettingsWords.ResumeCaption, ClosePanels, primary: true);
            hud.GameMenuRow(SettingsWords.SaveCaption, SaveFromGameMenu);
            if (gameMenuSaveFailure != null) hud.MutedLine(gameMenuSaveFailure, GameMenuSaveLineName);
            else if (gameMenuSavedAt.HasValue) hud.MutedLine(SettingsWords.SavedAtLine(gameMenuSavedAt.Value), GameMenuSaveLineName);
            hud.GameMenuRow(SettingsWords.SettingsCaption, () => ShowSettingsSection(SettingsWords.OpeningSection(false)));
            hud.GameMenuRow(SettingsWords.MainMenuCaption, OpenMainMenu);
            hud.MutedLine(SettingsWords.MainMenuKeepsLine);
        }

        /// <summary>The card's line about its last Save, so a test finds it by name.</summary>
        public const string GameMenuSaveLineName = "Game menu saved line";

        /// <summary>The card's Save: the season saved as Save now saves it, and the card stays, saying when or why not.</summary>
        private void SaveFromGameMenu()
        {
            bool busy = durableCommitInProgress;
            if (!busy) SaveNow();
            bool saved = !busy && !blockedRecovery && !npcSaveSuspended && sessionSaveFailure == null;
            gameMenuSavedAt = saved ? SettingsWords.Now : (DateTime?)null;
            // The refusal's own words where the save left no failure behind (a refused NPC tick sets only the message).
            gameMenuSaveFailure = saved ? null
                : busy ? "The season is being saved right now. Try again in a moment."
                : !string.IsNullOrEmpty(sessionSaveFailure) ? sessionSaveFailure
                : !string.IsNullOrEmpty(message) ? message
                : "The season could not be saved.";
            if (!settingsOpen) return;
            Render();
            hud.FocusWhenWired(EpisodeHud.GameMenuRowPrefix + SettingsWords.SaveCaption);
        }
    }
}
