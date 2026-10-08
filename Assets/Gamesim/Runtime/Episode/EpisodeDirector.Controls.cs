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

        private bool padHints;

        /// <summary>
        /// Whether the house's hints - the prompt, the help card, the glyph chips - are in the pad's
        /// words: the device last pressed on was a pad (PLAN A, A4). A new scene starts on the
        /// keyboard's.
        /// </summary>
        public bool HintsForPad => padHints;

        /// <summary>A press on the other kind of device rewords the hints: the prompt on its next line, the HUD at once.</summary>
        private void NoteInputDevice()
        {
            var pad = HouseInput.PadUsed();
            if (!pad.HasValue || pad.Value == padHints) return;
            padHints = pad.Value;
            // The talk prompt is kept for the houseguest it names; worded afresh for the device.
            promptNpc = null;
            if (hud != null) hud.SetPadHints(padHints);
        }

        /// <summary>The settings' disclosure that shows every control, and hides them again.</summary>
        public const string ShowControlsCaption = "Show the controls";
        public const string HideControlsCaption = "Hide the controls";
        private bool controlsShown;

        /// <summary>
        /// The settings' CONTROLS section (PLAN A, A4): folded under one control, and open, a line
        /// for every action of the episode's maps - what it does, its keys and its pad buttons, read
        /// off the actions the house is reading now.
        /// </summary>
        private void ControlsSettings()
        {
            hud.Section("CONTROLS");
            hud.Disclosure(controlsShown ? HideControlsCaption : ShowControlsCaption, controlsShown,
                () =>
                {
                    controlsShown = !controlsShown;
                    Render();
                    // The keyboard stays on the control it pressed, under its new words.
                    hud.FocusWhenWired(controlsShown ? HideControlsCaption : ShowControlsCaption);
                });
            if (!controlsShown) return;
            string context = null;
            foreach (var line in ControlsPage.Lines(HouseActions))
            {
                if (line.Context != context) { context = line.Context; hud.Heading(context); }
                hud.NamedParagraph(line.Name, line.Text);
            }
        }

        /// <summary>The house's actions: the rig's, or the shared copy when there is no rig (HouseInput).</summary>
        private HouseCameraActions HouseActions => cameraRig != null ? cameraRig.Actions : HouseInput.Actions;

        /// <summary>
        /// Something Escape closes that is not a panel: the overview, your moves, a chip's card.
        /// The press closes it and stops there, rather than going on to open the pause menu.
        /// </summary>
        private bool ClosableChromeUp => overviewOpen || emoteMenuOpen || castMenuFor != null;

        /// <summary>
        /// The front door's screens - the main menu, the cast screen, the creator - none of them a
        /// panel. A pad walks them with the d-pad and the shoulders, so the house's camera and
        /// chrome buttons on those (follow, your moves, the overview) wait until they are gone,
        /// rather than toggling the house and redrawing its HUD underneath on every step.
        /// </summary>
        private bool FrontDoorUp => (mainMenu != null && mainMenu.IsShowing)
            || (castSelect != null && castSelect.IsShowing)
            || (characterCreator != null && characterCreator.IsShowing);

        /// <summary>
        /// Whether the house is held right now - nothing in it ticks: the settings' eyebrow says
        /// PAUSED while it is, which is every moment the settings are open over a running house.
        /// </summary>
        public bool IsHouseClockHeld => IsReady && !NpcCanAdvance;

        /// <summary>
        /// Whether the settings on screen were opened from the main menu: the front door, which has
        /// no house being played behind it to pause - on a fresh install, no season played at all.
        /// Set as the menu opens them, and let go when they close.
        /// </summary>
        private bool settingsFromFrontDoor;

        /// <summary>The settings' eyebrow: PAUSED while the panel holds the house's clock over the house the player was in.</summary>
        private string SettingsEyebrow() =>
            settingsOpen && !settingsFromFrontDoor && !blockedRecovery && IsHouseClockHeld ? PausedEyebrow : SettingsEyebrowCopy;

        /// <summary>
        /// Escape, or Start. Topmost first: the game surface, a challenge, the opening, a ceremony
        /// card, the tour, the main menu, the creator, the cast screen, the season report, then the
        /// panels. With nothing open at all it opens the pause menu - the settings, with the house's
        /// clock held - from either device (PLAN A, A3, the owner's decision 1): the keyboard used
        /// to do nothing there, and a first-time player looking for "pause" found nothing.
        /// </summary>
        private void MenuPressed() => BackOut(pauseWhenNothingIsOpen: true);

        /// <summary>
        /// The pad's B (PLAN A, A2): the same chain as Escape, overlay for overlay - a ceremony card
        /// and a competition's board and results read B themselves, as they read Escape - and at its
        /// foot it closes the top panel and does nothing at all when nothing is open. Whether the
        /// press was taken.
        /// </summary>
        private bool BackPressed() => BackOut(pauseWhenNothingIsOpen: false);

        /// <summary>The priority chain Escape, Start and B share. False only for B with nothing open: not taken.</summary>
        private bool BackOut(bool pauseWhenNothingIsOpen)
        {
            // The game surface consumes Escape / Start / B itself, including the dismissal frame.
            if (competitionScreen != null && competitionScreen.OwnsMenuInput) return true;
            if (challengeActive) { CancelChallenge(); return true; }
            // The opening before anything: it draws over every screen, and Escape underneath it
            // used to close panels and release the shot it was holding.
            if (OpeningOwnsHouse) { OpeningMenuPressed(); return true; }
            // A ceremony card reads Escape and B itself - it skips the reveal - so the press does
            // not also close the panels or open the settings underneath it.
            if (CeremonyOverlays.OnScreen) return true;
            // The tour offered outside the opening - an imported season - dims the house and
            // takes the pointer; Escape closes it, as the tour's own card says.
            if (TourIsUp) { tutorial.Skip(); return true; }
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
                // A panel closes. With nothing open at all, Escape and Start are the pause menu,
                // and B is nothing - not even a redraw; the overview, your moves or a chip's card
                // close first and the press stops there.
                bool wasOpen = IsPanelOpen, chromeUp = ClosableChromeUp;
                // An evicted houseguest walking out reads Escape and B as its skip, as a card
                // does: with nothing else open the press is the walk's, by either device.
                if (!wasOpen && !chromeUp && WalkOutHasThePress) return true;
                if (!wasOpen && !chromeUp && !pauseWhenNothingIsOpen) return false;
                ClosePanels();
                if (!wasOpen && !chromeUp) OpenSettings();
            }
            return true;
        }
    }
}
