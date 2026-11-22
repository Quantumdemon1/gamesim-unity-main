using Gamesim.Presentation;

namespace Gamesim.Episode
{
    /// <summary>
    /// The finale page steps aside for the cast screen (<see cref="FinaleAside"/>).
    ///
    /// <para>"Start a new season" on the finale page - or on the season report over it - opened the
    /// cast screen with the page still open underneath: the episode panel drawn, the house held,
    /// <see cref="IsPanelOpen"/> true, behind a screen that had replaced it. The page closes now as
    /// the cast screen opens. Backing out of the cast screen without starting puts the page back as
    /// it was: the same season's finale page (not the report, which closed as its button was
    /// pressed), the juror whose reason was open still open, and the keyboard on "Start a new
    /// season", the control that left it. A season started there closes every panel as it installs
    /// (<see cref="Install"/>) and forgets the page set aside.</para>
    ///
    /// <para>The player's walking and the camera stay held while the page is aside, as they were
    /// under the page: the cast screen is setup, not the house. The house's keys - E, the notebook,
    /// the diary, save - wait while a front-door screen is up (Update's shortcut block reads
    /// <see cref="FrontDoorUp"/>): with the page closed, E at the episode station opened it again
    /// under the cast screen.</para>
    /// </summary>
    public sealed partial class EpisodeDirector
    {
        /// <summary>The finale page the cast screen has set aside, or none. View state, never saved.</summary>
        private FinaleAside finaleAside;
        /// <summary>Whether the next render of the finale page puts the keyboard on "Start a new season": once, as it comes back.</summary>
        private bool finaleFocusNewSeason;

        /// <summary>Whether the episode panel on screen is the finale page itself, with nothing of another panel over it.</summary>
        private bool FinalePageIsThePanel => phaseOpen && !settingsOpen && !journalOpen && !diaryOpen && !houseActivitiesOpen
            && focusedNpc == null && !sceneCardOpen && !askCardOpen && !juryHouseOverHouse && !blockedRecovery;

        /// <summary>
        /// Closes the finale page for the cast screen, remembering it. False, and nothing closed,
        /// when the page is not the panel on screen.
        /// </summary>
        private bool SetFinaleAsideForCast()
        {
            finaleAside = FinaleAside.For(Snapshot, FinalePageIsThePanel, finalePressedJurorId);
            if (!finaleAside.IsSet) return false;
            phaseOpen = false;
            juryQuestionsOpen = false;
            finaleFocusBack = false;
            finaleFocusNewSeason = false;
            Render();
            return true;
        }

        /// <summary>
        /// The cast screen's way back when it set the finale page aside: the page again, as it was,
        /// over the same finished season. Anything else - another season, a panel opened meanwhile -
        /// is drawn as it stands, as the way back always did.
        /// </summary>
        private void ReturnToFinaleFromCast()
        {
            var aside = finaleAside;
            finaleAside = default;
            if (!aside.ReturnsOver(Snapshot) || IsPanelOpen) { Render(); return; }
            phaseOpen = true;
            finalePressedJurorId = aside.PressedJurorId;
            finaleFocusNewSeason = true;
            if (player != null) player.SetInputEnabled(false);
            if (cameraRig != null) cameraRig.ControlsEnabled = false;
            Render();
        }

        /// <summary>A season installed from the cast screen: nothing of the old season's page is kept.</summary>
        private void ForgetFinaleAside()
        {
            finaleAside = default;
            finaleFocusNewSeason = false;
        }
    }
}
