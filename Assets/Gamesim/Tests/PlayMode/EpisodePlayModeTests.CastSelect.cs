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

        [UnityTest]
        public IEnumerator CastSelect_FailedSlotCreationShowsItsReasonAndRetriesTheEditedCast()
        {
            var draft = CharacterDraft.FromAppearance(CastTemplates.Find("emma-brown"));
            draft.Name = "Library guest";
            var profile = CharacterProfile.FromDraft(System.Guid.NewGuid().ToString("N"), draft);
            Assert.That(CastScreen().ProfileStore.Save(profile, out var profileError), Is.True, profileError);

            yield return OpenCastScreen();
            CastButtons(CastTemplates.RosterName(CastTemplates.Roster.AllStars))[0].onClick.Invoke();
            CastButtons("More houseguests")[0].onClick.Invoke();
            CastButtons("Cast slots")[0].onClick.Invoke();
            CastButtons("Add Library guest to the cast")[0].onClick.Invoke();
            CastButtons("Add Library guest to the cast")[0].onClick.Invoke();
            CastButtons("Edit slot 1")[0].onClick.Invoke();
            CastButtons("Identity")[0].onClick.Invoke();
            Creator().GetComponentsInChildren<TMPro.TMP_InputField>()
                .Single(field => field.name == "Name field").text = "Retry guest";
            CastButtons(CharacterCreator.ApplySlotCaption)[0].onClick.Invoke();
            yield return null;
            Assert.That(CastScreen().IsShowing, Is.True);
            Assert.That(Creator().IsShowing, Is.False);

            director.SaveNow();
            var before = director.Snapshot;
            string previousPath = director.SavePath;
            byte[] previousBytes = File.ReadAllBytes(previousPath);
            string blockedRoot = Path.Combine(temporaryDirectory, "blocked-cast-slot-root");
            File.WriteAllText(blockedRoot, "test-owned file prevents creating a save directory");
            var rootField = director.GetType().GetField("saveRoot",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(rootField, Is.Not.Null);
            rootField.SetValue(director, blockedRoot);
            try
            {
                CastButtons(CastSelect.StartCaption)[0].onClick.Invoke();
                yield return null;
                yield return null;

                Assert.That(CastScreen().IsShowing, Is.True, "The failed default-newcomer start returns to its cast setup.");
                Assert.That(Creator().IsShowing, Is.False, "Editing an NPC must not make that NPC the player draft.");
                Assert.That(director.StatusMessage, Does.StartWith("New season could not be saved."));
                var visibleError = CastScreen().GetComponentsInChildren<TMPro.TMP_Text>()
                    .Single(label => label.gameObject.activeInHierarchy && label.text == director.StatusMessage);
                Assert.That(visibleError.transform.parent.name, Is.EqualTo("Fixed season footer"),
                    "The failure must be on the active modal beside retry, outside the scrolling cast list.");
                Assert.That(CastButtons(CastSelect.StartCaption), Has.Length.EqualTo(1));
                Assert.That(CastButtons("Edit slot 1"), Has.Length.EqualTo(1));
                Assert.That(CastButtons("Edit slot 2"), Has.Length.EqualTo(1));
                Assert.That(CastScreen().GetComponentsInChildren<TMPro.TMP_Text>()
                    .Any(label => label.text == "Slot 1: Retry guest"), Is.True);
                Assert.That(director.SavePath, Is.EqualTo(previousPath));
                AssertEquivalent(before, director.Snapshot);
                Assert.That(File.ReadAllBytes(previousPath), Is.EqualTo(previousBytes));
            }
            finally
            {
                rootField.SetValue(director, temporaryDirectory);
            }

            CastButtons(CastSelect.StartCaption)[0].onClick.Invoke();
            yield return null;
            yield return null;
            Assert.That(CastScreen().IsShowing, Is.False);
            var fresh = director.Snapshot;
            Assert.That(fresh.contestants, Has.Count.EqualTo(SeasonBuilder.DefaultHouseSize + 1));
            Assert.That(fresh.Find(fresh.playerId).name, Is.EqualTo("You"));
            Assert.That(fresh.Find("custom-1").name, Is.EqualTo("Retry guest"));
            Assert.That(fresh.Find("custom-2").name, Is.EqualTo("Library guest"));
            var allStars = CastTemplates.In(CastTemplates.Roster.AllStars).Select(template => template.Id).ToArray();
            Assert.That(fresh.contestants.Where(person => !person.isPlayer && !person.id.StartsWith("custom-"))
                .All(person => allStars.Contains(person.id)), Is.True, "Retry must retain the selected roster.");
            Assert.That(File.ReadAllBytes(previousPath), Is.EqualTo(previousBytes));
            Assert.That(CastScreen().ProfileStore.TryLoad(profile.id, out var savedProfile, out profileError), Is.True, profileError);
            Assert.That(savedProfile.name, Is.EqualTo("Library guest"), "Cast edits remain independent of the library profile.");
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
