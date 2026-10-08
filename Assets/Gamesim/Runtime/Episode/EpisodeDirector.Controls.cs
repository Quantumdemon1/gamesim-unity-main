using Gamesim.House;
using Gamesim.Presentation;

namespace Gamesim.Episode
{
    /// <summary>
    /// The director's side of PLAN A's input slices: which actions it reads when it has no rig,
    /// and what Escape and Start do - the priority chain, and the pause menu at its foot.
    /// </summary>
    public sealed partial class EpisodeDirector
    {
        /// <summary>The settings' eyebrow while the house is held under them: the pause menu (PLAN A, A3).</summary>
        public const string PausedEyebrow = "PAUSED  ·  OFFLINE · NO ACCOUNT NEEDED";
        /// <summary>The settings' eyebrow otherwise.</summary>
        public const string SettingsEyebrowCopy = "OFFLINE · NO ACCOUNT NEEDED";

        /// <summary>The house's actions: the rig's, or the shared copy when there is no rig (HouseInput).</summary>
        private HouseCameraActions HouseActions => cameraRig != null ? cameraRig.Actions : HouseInput.Actions;

        /// <summary>
        /// Something Escape closes that is not a panel: the overview, your moves, a chip's card.
        /// The press closes it and stops there, rather than going on to open the pause menu.
        /// </summary>
        private bool ClosableChromeUp => overviewOpen || emoteMenuOpen || castMenuFor != null;

        /// <summary>
        /// Whether the house is held right now - nothing in it ticks: the settings' eyebrow says
        /// PAUSED while it is, which is every moment the settings are open over a running house.
        /// </summary>
        public bool IsHouseClockHeld => IsReady && !NpcCanAdvance;

        /// <summary>The settings' eyebrow: PAUSED while the panel holds the house's clock.</summary>
        private string SettingsEyebrow() => settingsOpen && !blockedRecovery && IsHouseClockHeld ? PausedEyebrow : SettingsEyebrowCopy;

        /// <summary>
        /// Escape, or Start. Topmost first: the game surface, a challenge, the opening, a ceremony
        /// card, the tour, the main menu, the creator, the cast screen, the season report, then the
        /// panels. With nothing open at all it opens the pause menu - the settings, with the house's
        /// clock held - from either device (PLAN A, A3, the owner's decision 1): the keyboard used
        /// to do nothing there, and a first-time player looking for "pause" found nothing.
        /// </summary>
        private void MenuPressed()
        {
            // The game surface consumes Escape / Start itself, including the dismissal frame.
            if (competitionScreen != null && competitionScreen.OwnsMenuInput) return;
            if (challengeActive) { CancelChallenge(); return; }
            // The opening before anything: it draws over every screen, and Escape underneath it
            // used to close panels and release the shot it was holding.
            if (OpeningOwnsHouse) { OpeningMenuPressed(); return; }
            // A ceremony card reads Escape itself - it skips the reveal - so the press does not
            // also close the panels or open the settings underneath it.
            if (CeremonyOverlays.OnScreen) return;
            // The tour offered outside the opening - an imported season - dims the house and
            // takes the pointer; Escape closes it, as the tour's own card says.
            if (TourIsUp) { tutorial.Skip(); return; }
            // Topmost first. The main menu sits above the cast screen, which sits above the
            // HUD; closing a panel underneath either of them would leave a screen on top of the
            // house with nothing behind it. The menu itself ignores Escape when there is no
            // season to go back to, because there is nowhere for it to close to.
            if (mainMenu != null && mainMenu.IsShowing) { if (SeasonInProgress) CloseMainMenu(); }
            // The creator draws above the cast screen, so it takes Escape first — otherwise
            // the screen underneath would close out from under the form on top of it.
            else if (characterCreator != null && characterCreator.IsShowing) characterCreator.Dismiss();
            else if (castSelect != null && castSelect.IsShowing) castSelect.Dismiss();
            // The report draws over the finale panel; Escape closes it and leaves the panel.
            else if (IsSeasonReportOpen) seasonReport.Close();
            else
            {
                // A panel closes. With nothing open at all, the press is the pause menu - from the
                // keyboard as from the pad; the overview, your moves or a chip's card close first
                // and the press stops there.
                bool wasOpen = IsPanelOpen, chromeUp = ClosableChromeUp;
                ClosePanels();
                if (!wasOpen && !chromeUp) OpenSettings();
            }
        }
    }
}
