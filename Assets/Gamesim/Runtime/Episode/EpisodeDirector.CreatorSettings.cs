using System;
using System.Collections;
using Gamesim.Simulation;

namespace Gamesim.Episode
{
    /// <summary>
    /// The creator's Settings gear (menu quick win 12): Settings opened from the creator come back
    /// to the creator when they close, on the page it was on, with the draft as it stood. Only a
    /// plain close does: leaving Settings for the main menu, a new season or another season loaded
    /// goes where the player asked instead.
    /// </summary>
    public sealed partial class EpisodeDirector
    {
        private Action settingsBackToCreator;
        private EpisodeEngine settingsCreatorEngine;

        private void OpenSettingsFromCreator(Action back)
        {
            settingsBackToCreator = back;
            settingsCreatorEngine = engine;
            // Before any season of the player's: nothing in the house is held under these (A3).
            settingsFromFrontDoor = true;
            OpenSettings();
            ApplyMusic();
        }

        /// <summary>
        /// Called as Settings close (ClosePanelsInternal); <paramref name="render"/> is false for a
        /// close on the way elsewhere. The creator comes back on the next frame, and only if by then
        /// nothing else has the screen: a load or a new season installs its state just after the
        /// close, and the main menu or the cast screen goes up just after it.
        /// </summary>
        private void ReturnToCreatorAfterSettings(bool render)
        {
            var back = settingsBackToCreator;
            var from = settingsCreatorEngine;
            settingsBackToCreator = null;
            settingsCreatorEngine = null;
            if (back == null || !render || !isActiveAndEnabled) return;
            StartCoroutine(ReturnToCreatorNextFrame(back, from));
        }

        private IEnumerator ReturnToCreatorNextFrame(Action back, EpisodeEngine from)
        {
            yield return null;
            if (settingsOpen || blockedRecovery || from != engine) yield break;
            if ((mainMenu != null && mainMenu.IsShowing) || (castSelect != null && castSelect.IsShowing)
                || (characterCreator != null && characterCreator.IsShowing)) yield break;
            back();
            ApplyMusic();
        }
    }
}
