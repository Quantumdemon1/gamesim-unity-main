using System.Collections;
using System.IO;
using System.Linq;
using Gamesim.Persistence;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// The front door.
    ///
    /// <para>These drive it explicitly rather than relying on it opening itself. A real launch opens
    /// at the menu; a run with an explicit save root — every test here, and the standalone
    /// verification — keeps the previous behaviour, because a menu nothing asked for would block all
    /// of them. That gate is worth stating in a test too, since it is the difference between a
    /// harness that runs and one that hangs on a screen.</para>
    ///
    /// <para>Quit is checked for presence and never clicked. In a batchmode editor it would end the
    /// run, and a test that stops the test runner does not report a result.</para>
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        private MainMenu Menu() => director.GetComponentInChildren<MainMenu>(true);

        [UnityTest]
        public IEnumerator MainMenu_DoesNotOpenItselfWhenASaveRootIsSupplied()
        {
            yield return null;
            Assert.That(Menu(), Is.Not.Null, "The director must attach a menu even when it does not open it.");
            Assert.That(Menu().IsShowing, Is.False,
                "A run with an explicit save root is a harness, and must reach the house directly.");
            Assert.That(director.IsPanelOpen, Is.False);
        }

        [UnityTest]
        public IEnumerator MainMenu_OffersContinueOnlyWhenASaveExists()
        {
            // A fresh fixture has a save path and no file on it.
            var store = new EpisodeSaveStore(director.SavePath);
            foreach (var path in new[] { store.SavePath, store.BackupPath })
                if (File.Exists(path)) File.Delete(path);

            director.OpenMainMenu();
            yield return null;
            yield return null;

            Assert.That(Menu().IsShowing, Is.True);
            Assert.That(CastButtons(MainMenu.ContinueCaption), Is.Empty,
                "Continue must not be offered when there is nothing on disk to continue.");
            Assert.That(CastButtons(MainMenu.NewSeasonCaption), Has.Length.EqualTo(1));
            Assert.That(CastButtons(MainMenu.SettingsCaption), Has.Length.EqualTo(1));
            Assert.That(CastButtons(MainMenu.QuitCaption), Has.Length.EqualTo(1));

            director.CloseMainMenu();
            yield return null;

            // Now write one, and it appears.
            new EpisodeSaveStore(director.SavePath).Save(director.Snapshot);
            director.OpenMainMenu();
            yield return null;
            yield return null;

            Assert.That(CastButtons(MainMenu.ContinueCaption), Has.Length.EqualTo(1),
                "Continue must be offered once a season exists on disk.");
        }

        [UnityTest]
        public IEnumerator MainMenu_ContinueHandsTheHouseBackWithoutTouchingTheSeason()
        {
            new EpisodeSaveStore(director.SavePath).Save(director.Snapshot);
            var before = director.Snapshot;
            string slot = director.SavePath;

            director.OpenMainMenu();
            yield return null;
            yield return null;

            var resume = CastButtons(MainMenu.ContinueCaption);
            Assert.That(resume, Has.Length.EqualTo(1));
            resume[0].onClick.Invoke();
            yield return null;
            yield return null;

            Assert.That(Menu().IsShowing, Is.False);
            Assert.That(director.IsPanelOpen, Is.False, "Continue returns to the house, not to a panel.");
            Assert.That(director.SavePath, Is.EqualTo(slot));
            Assert.That(director.Snapshot.sessionId, Is.EqualTo(before.sessionId));
            Assert.That(director.Snapshot.revision, Is.EqualTo(before.revision));
        }

        /// <summary>
        /// New Season hands off to the cast screen, which draws below the menu — so the menu has to
        /// step aside rather than sit on top of it, and cancelling has to land back on the menu
        /// rather than dumping the player into a house they did not ask to be in.
        /// </summary>
        [UnityTest]
        public IEnumerator MainMenu_NewSeasonOpensTheCastScreenAndCancellingReturnsHere()
        {
            var before = director.Snapshot;

            director.OpenMainMenu();
            yield return null;
            yield return null;

            CastButtons(MainMenu.NewSeasonCaption)[0].onClick.Invoke();
            yield return null;
            yield return null;

            Assert.That(Menu().IsShowing, Is.False, "The menu must step aside for the cast screen.");
            Assert.That(CastScreen().IsShowing, Is.True);
            Assert.That(CastButtons(CastSelect.StartCaption), Has.Length.EqualTo(1),
                "The cast screen's controls must be reachable, not buried under the menu.");

            CastButtons(CastSelect.CancelCaption)[0].onClick.Invoke();
            yield return null;
            yield return null;

            Assert.That(CastScreen().IsShowing, Is.False);
            Assert.That(Menu().IsShowing, Is.True, "Cancelling must return to where the player came from.");
            Assert.That(director.Snapshot.sessionId, Is.EqualTo(before.sessionId),
                "Nothing may be built by opening and backing out of the cast screen.");
        }

        [UnityTest]
        public IEnumerator MainMenu_StartingASeasonFromTheMenuLeavesNoScreenOnTop()
        {
            var before = director.Snapshot;

            director.OpenMainMenu();
            yield return null;
            yield return null;
            CastButtons(MainMenu.NewSeasonCaption)[0].onClick.Invoke();
            yield return null;
            yield return null;
            CastButtons(CastSelect.StartCaption)[0].onClick.Invoke();
            yield return null;
            yield return null;

            Assert.That(Menu().IsShowing, Is.False);
            Assert.That(CastScreen().IsShowing, Is.False);
            var fresh = director.Snapshot;
            Assert.That(fresh.sessionId, Is.Not.EqualTo(before.sessionId));
            Assert.That(fresh.contestants.Count, Is.EqualTo(SeasonBuilder.DefaultHouseSize));
            Assert.That(EpisodeValidation.TryValidate(fresh, out var error), Is.True, error);
        }

        [UnityTest]
        public IEnumerator MainMenu_SettingsOpensTheSettingsPanelAndOffersAWayBack()
        {
            director.OpenMainMenu();
            yield return null;
            yield return null;

            CastButtons(MainMenu.SettingsCaption)[0].onClick.Invoke();
            yield return null;
            yield return null;

            Assert.That(Menu().IsShowing, Is.False);
            Assert.That(director.IsPanelOpen, Is.True);
            Assert.That(CastButtons("Main menu"), Has.Length.EqualTo(1),
                "Settings must offer a way back to the menu, or it is a one-way door.");

            CastButtons("Main menu")[0].onClick.Invoke();
            yield return null;
            yield return null;
            Assert.That(Menu().IsShowing, Is.True);
        }

        /// <summary>
        /// Escape closes the menu when there is a season behind it, and the menu keeps the input it
        /// is given: nothing on the screen may take a raycast once it is hidden.
        /// </summary>
        [UnityTest]
        public IEnumerator MainMenu_HiddenMenuSwallowsNothing()
        {
            director.OpenMainMenu();
            yield return null;
            yield return null;
            Assert.That(CastButtons(MainMenu.NewSeasonCaption), Has.Length.EqualTo(1));

            director.CloseMainMenu();
            yield return null;

            var group = Menu().GetComponent<CanvasGroup>();
            Assert.That(group.blocksRaycasts, Is.False);
            Assert.That(group.interactable, Is.False);
            Assert.That(group.alpha, Is.EqualTo(0f));
            Assert.That(CastButtons(MainMenu.NewSeasonCaption), Is.Empty,
                "A hidden menu must offer no reachable controls.");
        }
    }
}
