using System.Collections;
using System.IO;
using System.Linq;
using Gamesim.Persistence;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// The cast screen, driven through its own controls.
    ///
    /// <para>The screen's whole reason to exist is that it decides nothing until the player says so.
    /// "New season" used to write a slot the instant it was clicked; now it opens a chooser, and the
    /// failure that would matter most is a cancel that has already replaced the running season —
    /// there is no undo for that. Most of what is here checks that nothing happened.</para>
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        private CastSelect CastScreen() => director.GetComponentInChildren<CastSelect>(true);

        /// <summary>Live, interactable buttons on the screen carrying exactly this caption.</summary>
        private Button[] CastButtons(string caption) =>
            director.GetComponentsInChildren<Button>(true)
                .Where(button => button.IsActive() && button.IsInteractable()
                    && button.GetComponentsInChildren<TMPro.TMP_Text>(true).Any(label => label.text == caption))
                .ToArray();

        private IEnumerator OpenCastScreen()
        {
            director.OpenSettings();
            yield return null;
            director.NewSeason();
            yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator CastSelect_OpeningTheScreenWritesNothing()
        {
            var before = director.Snapshot;
            string slot = director.SavePath;
            var bytes = File.Exists(slot) ? File.ReadAllBytes(slot) : null;

            yield return OpenCastScreen();

            Assert.That(CastScreen(), Is.Not.Null, "The director must attach a cast screen.");
            Assert.That(CastScreen().IsShowing, Is.True, "New season should open the cast screen.");
            Assert.That(director.SavePath, Is.EqualTo(slot), "Opening the screen must not change slots.");
            Assert.That(director.Snapshot.sessionId, Is.EqualTo(before.sessionId));
            Assert.That(director.Snapshot.contestants.Count, Is.EqualTo(before.contestants.Count));
            if (bytes != null) Assert.That(File.ReadAllBytes(slot), Is.EqualTo(bytes), "The slot on disk must be untouched.");
        }

        [UnityTest]
        public IEnumerator CastSelect_CancellingLeavesTheRunningSeasonExactlyAsItWas()
        {
            var before = director.Snapshot;
            string slot = director.SavePath;

            yield return OpenCastScreen();

            var cancel = CastButtons(CastSelect.CancelCaption);
            Assert.That(cancel, Has.Length.EqualTo(1), "Exactly one cancel control should be reachable.");
            cancel[0].onClick.Invoke();
            yield return null;
            yield return null;

            Assert.That(CastScreen().IsShowing, Is.False, "Cancel must close the screen.");
            Assert.That(director.SavePath, Is.EqualTo(slot));
            Assert.That(director.Snapshot.sessionId, Is.EqualTo(before.sessionId),
                "Cancelling must not replace the running season.");
            Assert.That(director.Snapshot.contestants.Count, Is.EqualTo(before.contestants.Count));
        }

        /// <summary>
        /// Escape closes the cast screen rather than the settings panel behind it. Closing the
        /// panel underneath would leave the screen on top of the house with nothing to go back to.
        /// </summary>
        [UnityTest]
        public IEnumerator CastSelect_EscapeClosesTheScreenAndNotThePanelUnderneath()
        {
            var before = director.Snapshot;
            yield return OpenCastScreen();
            Assert.That(director.IsPanelOpen, Is.True, "The settings panel is open behind the screen.");

            testKeyboard = InputSystem.AddDevice<Keyboard>();
            InputSystem.QueueStateEvent(testKeyboard, new KeyboardState(Key.Escape));
            yield return null;
            InputSystem.QueueStateEvent(testKeyboard, new KeyboardState());
            yield return null; yield return null;

            Assert.That(CastScreen().IsShowing, Is.False, "Escape must close the cast screen.");
            Assert.That(director.IsPanelOpen, Is.True,
                "The first Escape belongs to the screen; the panel behind it stays open.");
            Assert.That(director.Snapshot.sessionId, Is.EqualTo(before.sessionId));

            // And a second Escape, now that the screen is gone, closes the panel as it always did.
            InputSystem.QueueStateEvent(testKeyboard, new KeyboardState(Key.Escape));
            yield return null;
            InputSystem.QueueStateEvent(testKeyboard, new KeyboardState());
            yield return null; yield return null;
            Assert.That(director.IsPanelOpen, Is.False);
        }

        /// <summary>
        /// Committing builds a season of the default size in a new slot, and leaves the old one on
        /// disk. This is the one path that is allowed to write anything.
        /// </summary>
        [UnityTest]
        public IEnumerator CastSelect_StartingBuildsADefaultSizedSeasonInANewSlot()
        {
            var before = director.Snapshot;
            string previousSlot = director.SavePath;
            // Write the slot first. A fresh fixture has a save *path* but no file until something
            // saves, and "the previous slot is retained" is only a claim about a file that exists.
            new EpisodeSaveStore(previousSlot).Save(before);
            Assert.That(File.Exists(previousSlot), Is.True);

            yield return OpenCastScreen();

            var start = CastButtons(CastSelect.StartCaption);
            Assert.That(start, Has.Length.EqualTo(1), "Exactly one start control should be reachable.");
            start[0].onClick.Invoke();
            yield return null;
            yield return null;

            Assert.That(CastScreen().IsShowing, Is.False);
            Assert.That(director.SavePath, Is.Not.EqualTo(previousSlot), "A new season goes in a new slot.");
            Assert.That(File.Exists(previousSlot), Is.True, "The previous slot must be retained.");

            var fresh = director.Snapshot;
            Assert.That(fresh.sessionId, Is.Not.EqualTo(before.sessionId));
            Assert.That(fresh.contestants.Count, Is.EqualTo(SeasonBuilder.DefaultHouseSize));
            Assert.That(fresh.contestants.Count(c => c.isPlayer), Is.EqualTo(1));
            Assert.That(fresh.phase, Is.EqualTo(EpisodePhase.Social));
            Assert.That(fresh.week, Is.EqualTo(1));
            Assert.That(EpisodeValidation.TryValidate(fresh, out var error), Is.True, error);
        }

        /// <summary>
        /// Every houseguest in the built season gets a body, and none of the old season's bodies are
        /// left standing in the house as nameless extras.
        ///
        /// <para>The house was authored for six and the default season is eight, so this is the
        /// first time a season has ever installed a cast larger than the scene's own.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator CastSelect_ABuiltSeasonGivesEveryHouseguestABody()
        {
            yield return OpenCastScreen();
            CastButtons(CastSelect.StartCaption)[0].onClick.Invoke();
            yield return null;
            yield return null;
            yield return SettleCast();

            var state = director.Snapshot;
            var cast = state.contestants.Where(c => !c.isPlayer).Select(c => c.id).OrderBy(id => id).ToArray();
            // The player has a body too, and it is not one of the houseguest slots this fits.
            var bodies = SceneComponents<CharacterPresentation>()
                .Where(visual => visual.isActiveAndEnabled && visual.CharacterId != state.playerId)
                .Select(visual => visual.CharacterId)
                .OrderBy(id => id)
                .ToArray();

            CollectionAssert.AreEqual(cast, bodies,
                "The bodies in the house must be exactly the season's cast — no missing houseguest, "
                + "and nobody left over from the season before.");
        }

        /// <summary>
        /// The screen's card grid covers the whole roster, and every card is a real button rather
        /// than a decorated panel — the grid has to be usable without a mouse.
        /// </summary>
        [UnityTest]
        public IEnumerator CastSelect_EveryHouseguestOnTheRosterHasAReachableCard()
        {
            yield return OpenCastScreen();

            foreach (var template in CastTemplates.In(CastTemplates.Roster.Regular))
                Assert.That(CastButtons(template.Name), Has.Length.EqualTo(1),
                    template.Name + " has no reachable card on the default roster.");

            // And nobody from the other roster is on screen at the same time.
            foreach (var template in CastTemplates.In(CastTemplates.Roster.AllStars))
                Assert.That(CastButtons(template.Name), Is.Empty,
                    template.Name + " is an all-star and should not be on the regular roster.");
        }

        /// <summary>
        /// Picking a card makes that person the player, without putting two of them in the house.
        /// </summary>
        [UnityTest]
        public IEnumerator CastSelect_PickingACardMakesThatHouseguestThePlayer()
        {
            yield return OpenCastScreen();

            var chosen = CastTemplates.In(CastTemplates.Roster.Regular).First();
            CastButtons(chosen.Name)[0].onClick.Invoke();
            yield return null;
            yield return null;

            CastButtons(CastSelect.StartCaption)[0].onClick.Invoke();
            yield return null;
            yield return null;

            var fresh = director.Snapshot;
            var you = fresh.Find(fresh.playerId);
            Assert.That(you, Is.Not.Null);
            Assert.That(you.isPlayer, Is.True);
            Assert.That(you.name, Is.EqualTo(chosen.Name), "The picked card should be the player.");
            Assert.That(fresh.contestants.Count(c => c.name == chosen.Name), Is.EqualTo(1),
                "The picked houseguest must not also be cast as an NPC.");
            Assert.That(EpisodeValidation.TryValidate(fresh, out var error), Is.True, error);
        }

        /// <summary>
        /// Nothing on the screen takes a raycast once it is hidden. A screen that keeps eating
        /// clicks after it closes is indistinguishable from a frozen game.
        /// </summary>
        [UnityTest]
        public IEnumerator CastSelect_HiddenScreenSwallowsNothing()
        {
            yield return OpenCastScreen();
            CastScreen().Dismiss();
            yield return null;

            var group = CastScreen().GetComponent<CanvasGroup>();
            Assert.That(group.blocksRaycasts, Is.False);
            Assert.That(group.interactable, Is.False);
            Assert.That(group.alpha, Is.EqualTo(0f));
            Assert.That(CastButtons(CastSelect.StartCaption), Is.Empty,
                "A hidden screen must offer no reachable controls.");
        }
    }
}
