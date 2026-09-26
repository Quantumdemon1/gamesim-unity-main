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

        /// <summary>
        /// Photographs the cast screen so its quality can be judged from a frame.
        ///
        /// <para>Every other check on this screen asks whether a control exists and does the right
        /// thing when pressed, which is why a screen can pass all of them and still be reported as
        /// low quality. None of them can see a ring that is the wrong colour, a card carrying five
        /// lines where the reference carries two, or a 1180-wide column of content adrift in a
        /// 1920-wide frame.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator CastSelect_CapturesTheScreenForReview()
        {
            if (!Application.isBatchMode) yield break;
            yield return OpenCastScreen();
            Canvas.ForceUpdateCanvases();
            yield return null;
            Assert.That(CastScreen(), Is.Not.Null);
            Assert.That(CastScreen().IsShowing, Is.True, "There is nothing to photograph if it never opened.");

            yield return WaitForCastFaces("cast-select");

            Canvas.ForceUpdateCanvases();
            yield return null;
            yield return CaptureFraming("cast-select");
            CastScreen().Dismiss();
            yield return null;
        }

        /// <summary>
        /// The same screen with a card picked - the state the frame above never shows, and the one
        /// the glass on this screen exists for: the halo, a single ring and "PLAYING AS" on one card
        /// among twelve. Review evidence, not proof; the proof is
        /// <c>CastSelect_CardsAreGlassAndOnlyTheChosenOneGlows</c>.
        /// </summary>
        [UnityTest]
        public IEnumerator CastSelect_CapturesThePickedCardForReview()
        {
            if (!Application.isBatchMode) yield break;
            yield return OpenCastScreen();
            Canvas.ForceUpdateCanvases();
            yield return null;
            Assert.That(CastScreen().IsShowing, Is.True, "There is nothing to photograph if it never opened.");
            yield return WaitForCastFaces("cast-select-picked, before the pick");

            var chosen = CastTemplates.In(CastTemplates.Roster.Regular).First();
            CastButtons(chosen.Name)[0].onClick.Invoke();
            yield return null;
            yield return null;
            // Again, and the picked houseguest's model: a frame taken while it builds shows an empty stage.
            yield return WaitForCastFaces("cast-select-picked");
            float built = Time.realtimeSinceStartup + 25f;
            while (!CastScreen().PreviewReady && Time.realtimeSinceStartup < built) yield return null;

            Canvas.ForceUpdateCanvases();
            yield return null;
            yield return CaptureFraming("cast-select-picked");
            CastScreen().Dismiss();
            yield return null;
        }

        /// <summary>
        /// Waits for the faces. A card with no glamour photo carries a RawImage that CastSelect.Update
        /// fills from CharacterPortraits as its render lands, so a frame taken on open would
        /// photograph an empty disc and read as proof the screen has no portrait.
        /// </summary>
        private IEnumerator WaitForCastFaces(string frame)
        {
            RawImage[] Faces() => CastScreen().GetComponentsInChildren<RawImage>(true)
                .Where(image => image.name == "Model portrait").ToArray();
            int wanted = Faces().Length;
            float deadline = Time.realtimeSinceStartup + 25f;
            while (Time.realtimeSinceStartup < deadline
                   && Faces().Count(image => image.texture != null) < wanted)
                yield return null;
            int landed = Faces().Count(image => image.texture != null);
            Debug.Log("[Gamesim] Cast screen (" + frame + ") - " + landed + " of " + wanted + " portraits rendered before capture.");
        }

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
        /// Every card shows that houseguest's own glamour photo - the web game's portrait - in its ring,
        /// on both rosters: not a render, not a stand-in, and nobody else's.
        /// </summary>
        [UnityTest]
        public IEnumerator CastSelect_EveryCardShowsThatHouseguestsGlamourPhoto()
        {
            yield return OpenCastScreen();
            foreach (var roster in new[] { CastTemplates.Roster.Regular, CastTemplates.Roster.AllStars })
            {
                CastButtons(CastTemplates.RosterName(roster))[0].onClick.Invoke();
                yield return null;
                foreach (var template in CastTemplates.In(roster))
                {
                    var card = CastScreen().GetComponentsInChildren<RectTransform>().Single(rect => rect.name == template.Name);
                    var photo = card.GetComponentsInChildren<RawImage>().SingleOrDefault(image => image.name == CastSelect.GlamourPhotoName);
                    Assert.That(photo, Is.Not.Null, template.Name + " has a glamour photo on their card.");
                    Assert.That(photo.texture, Is.Not.Null);
                    Assert.That(photo.texture.name, Is.EqualTo(template.Id), template.Name + " wears their own photo.");
                }
            }
        }

        /// <summary>
        /// The details wait for a pick, then show that houseguest - their nickname, home and words -
        /// and their live model, which appears nowhere else on the screen. Picking someone else
        /// changes the model; picking them again puts the details away.
        /// </summary>
        [UnityTest]
        public IEnumerator CastSelect_PickingAPortraitShowsTheirDetailsAndTheirModel()
        {
            yield return OpenCastScreen();
            RawImage Model() => CastScreen().GetComponentsInChildren<RawImage>().SingleOrDefault(image => image.name == CastSelect.LiveModelName);
            string[] Words() => CastScreen().GetComponentsInChildren<RectTransform>().Single(rect => rect.name == CastSelect.DetailPanelName)
                .GetComponentsInChildren<TMPro.TMP_Text>().Select(label => label.text).ToArray();
            Assert.That(Model(), Is.Null, "No model until a houseguest is picked.");
            Assert.That(Words(), Does.Contain("Select a houseguest"));

            var emma = CastTemplates.Find("emma-brown");
            CastButtons(emma.Name)[0].onClick.Invoke();
            yield return null;
            Assert.That(Words(), Does.Contain(emma.Archetype), "Her nickname.");
            Assert.That(Words(), Does.Contain(emma.Occupation));
            Assert.That(Words().Any(word => word.Contains(emma.Hometown)), Is.True, "Where she is from.");
            Assert.That(Words().Any(word => word.Contains(emma.Bio)), Is.True, "Her words, quoted.");
            Assert.That(Words(), Does.Contain(CastTemplates.CategoryDescription(emma.Category)), "What her kind of player is.");
            Assert.That(CastButtons("Play as Emma"), Has.Length.EqualTo(1));
            Assert.That(CastButtons(emma.Name), Has.Length.EqualTo(1), "The details add no second control with her name.");
            Assert.That(Model(), Is.Not.Null, "Her model stands in the details.");
            Assert.That(CastScreen().PreviewedId, Is.EqualTo(emma.Id));
            float built = Time.realtimeSinceStartup + 25f;
            while (!CastScreen().PreviewReady && Time.realtimeSinceStartup < built) yield return null;
            Assert.That(CastScreen().PreviewReady, Is.True, "Her model is built and drawn.");
            Assert.That(Model().texture, Is.Not.Null);

            var casey = CastTemplates.Find("casey-wilson");
            CastButtons(casey.Name)[0].onClick.Invoke();
            yield return null;
            Assert.That(CastScreen().PreviewedId, Is.EqualTo(casey.Id), "Picking someone else shows their model.");
            CastButtons(casey.Name)[0].onClick.Invoke();
            yield return null;
            Assert.That(Model(), Is.Null, "Picking them again puts the details away.");
            Assert.That(CastScreen().PreviewedId, Is.Null);
        }

        /// <summary>Play as - the details' commit - starts the season as that houseguest, like Start.</summary>
        [UnityTest]
        public IEnumerator CastSelect_PlayAsStartsTheSeasonAsThatHouseguest()
        {
            yield return OpenCastScreen();
            var will = CastTemplates.Find("dr-will-kirby");
            CastButtons(CastTemplates.RosterName(CastTemplates.Roster.AllStars))[0].onClick.Invoke();
            yield return null;
            CastButtons(will.Name)[0].onClick.Invoke();
            yield return null;
            var play = CastButtons("Play as Will");
            Assert.That(play, Has.Length.EqualTo(1), "An honorific is not a first name.");
            play[0].onClick.Invoke();
            yield return null;
            yield return null;
            Assert.That(CastScreen().IsShowing, Is.False);
            var fresh = director.Snapshot;
            Assert.That(fresh.Find(fresh.playerId).name, Is.EqualTo(will.Name));
            Assert.That(fresh.contestants.Count(person => person.name == will.Name), Is.EqualTo(1));
        }

        /// <summary>A category chip says what that kind of player is, in the web game's words.</summary>
        [UnityTest]
        public IEnumerator CastSelect_ACategoryChipExplainsItsKindOfPlayer()
        {
            yield return OpenCastScreen();
            string Line() => CastScreen().GetComponentsInChildren<TMPro.TMP_Text>().Single(label => label.name == "Category line").text;
            Assert.That(Line(), Does.StartWith("12 houseguests"), "With no filter, how the roster divides.");
            foreach (string kind in CastTemplates.Categories)
            {
                CastButtons(kind)[0].onClick.Invoke();
                yield return null;
                Assert.That(Line(), Does.Contain(CastTemplates.CategoryDescription(kind)), kind);
            }
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
