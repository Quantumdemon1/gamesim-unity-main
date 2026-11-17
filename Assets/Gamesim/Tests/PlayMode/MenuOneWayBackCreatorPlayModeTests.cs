using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Gamesim.Persistence;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// Menu slice 2 on the character creator (part a), on the built screen: the one way back at the
    /// top left, drawn by the shared helper; My Houseguests as a header button that opens a panel with
    /// its own Close over the step; the card that asks before a saved houseguest replaces unsaved
    /// work; the step row's numbers and marks; the warning that is a link; and one bright button in
    /// every state. A creator of its own on a scratch library, with an event system for focus. The
    /// words and rules themselves are pinned Unity-free in MenuOneWayBackCreatorTests.
    /// </summary>
    public sealed class MenuOneWayBackCreatorPlayModeTests
    {
        private GameObject owner, events;
        private string root;
        private int backs;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (owner != null) Object.Destroy(owner);
            if (events != null) Object.Destroy(events);
            yield return null;
            DeleteRoot();
        }

        /// <summary>Ends one creator before a test opens another: its scratch library goes with it.</summary>
        private IEnumerator Close()
        {
            if (owner != null) Object.Destroy(owner);
            owner = null;
            yield return null;
            DeleteRoot();
        }

        private void DeleteRoot()
        {
            if (root == null) return;
            string resolved = Path.GetFullPath(root);
            Assert.That(resolved.StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase), Is.True);
            Assert.That(Path.GetFileName(resolved), Does.StartWith("GamesimOneWayBackCreator-"));
            if (Directory.Exists(resolved)) Directory.Delete(resolved, true);
            root = null;
        }

        private CharacterCreator Make(int savedHouseguests)
        {
            if (EventSystem.current == null) events = new GameObject("One way back events", typeof(EventSystem));
            root = Path.Combine(Path.GetTempPath(), "GamesimOneWayBackCreator-" + Guid.NewGuid().ToString("N"));
            var library = new CharacterProfileStore(root);
            for (int i = 1; i <= savedHouseguests; i++)
            {
                var saved = CharacterDraft.FromAppearance(CastTemplates.Find("alex-chen"));
                saved.Name = "Saved Guest " + i;
                Assert.That(library.Save(CharacterProfile.FromDraft(Guid.NewGuid().ToString("N"), saved), out var error), Is.True, error);
            }
            owner = new GameObject("Menu one way back creator");
            var creator = CharacterCreator.Attach(owner);
            creator.ConfigureProfiles(library);
            backs = 0;
            return creator;
        }

        private CharacterCreator Open(bool quick = false, int savedHouseguests = 0, string name = "Way Back Robin")
        {
            var creator = Make(savedHouseguests);
            var draft = CharacterDraft.FromAppearance(CastTemplates.Find("emma-brown"));
            draft.Name = name;
            if (quick) creator.ShowQuick(new SeasonBuilder.Choice(), draft, _ => { }, () => backs++);
            else creator.Show(new SeasonBuilder.Choice(), draft, _ => { }, () => backs++);
            return creator;
        }

        private CharacterCreator OpenCastSlot()
        {
            var creator = Make(0);
            var draft = CharacterDraft.FromAppearance(CastTemplates.Find("emma-brown"));
            draft.Name = "Slot Robin";
            creator.ShowForCastSlot(new SeasonBuilder.Choice(), draft, _ => { }, () => backs++);
            return creator;
        }

        private static Button[] Buttons(Component screen, string caption) => screen.GetComponentsInChildren<Button>()
            .Where(button => button.gameObject.activeInHierarchy && button.GetComponentInChildren<TMP_Text>()?.text == caption).ToArray();

        private static void Click(Component screen, string caption)
        {
            var buttons = Buttons(screen, caption);
            Assert.That(buttons, Has.Length.EqualTo(1), caption);
            buttons[0].onClick.Invoke();
        }

        private static Bounds Box(Component screen, Transform item) =>
            RectTransformUtility.CalculateRelativeRectTransformBounds(screen.transform, item);

        private static Rect Frame(Component screen) => ((RectTransform)screen.transform).rect;

        private static RectTransform Named(Component screen, string name, bool button = false) => screen.GetComponentsInChildren<RectTransform>()
            .Single(rect => rect.name == name && (rect.GetComponent<Button>() != null) == button);

        private static Button[] Bright(Component screen) => screen.GetComponentsInChildren<Button>()
            .Where(button => button.gameObject.activeInHierarchy && button.GetComponent<Image>() != null
                && button.GetComponent<Image>().color == UiTheme.ActionBlue).ToArray();

        private static string[] StepNames(Component screen) =>
            Named(screen, "Setup steps").GetComponentsInChildren<Button>().Select(tab => tab.name).ToArray();

        private static Button Tab(Component screen, string step) =>
            Named(screen, "Setup steps").GetComponentsInChildren<Button>().Single(tab => tab.name == step);

        // ---------------------------------------------------------------- A5: one way out

        [Test]
        public void TheCreatorsWayBackIsTheHelpersWording()
        {
            Assert.That(CharacterCreator.BackCaption, Is.EqualTo(WayOut.BackToKeeping(WayOut.Cast, "draft")));
            Assert.That(WayOut.IsWayBack(CharacterCreator.CancelSlotCaption), Is.False, "Cancel this edit throws the edit away: it is no way back.");
            Assert.That(CharacterCreator.CreateCaption, Is.EqualTo("Create a houseguest"), "The cast screen offers making one by the one name.");
        }

        [UnityTest]
        public IEnumerator TheWayBackIsTheSharedHelpersAtTheTopLeftAndTheFooterHasNone()
        {
            var creator = Open();
            yield return null;
            Canvas.ForceUpdateCanvases();
            var back = Buttons(creator, CharacterCreator.BackCaption).Single();
            Assert.That(back.name, Is.EqualTo(WayOut.BackObjectName), "Drawn by WayOut.AddBack.");
            var frame = Frame(creator);
            var box = Box(creator, back.transform);
            Assert.That(box.min.x, Is.LessThan(frame.xMin + frame.width * .25f), "At the left.");
            Assert.That(box.max.y, Is.GreaterThan(frame.yMax - 100f), "At the top.");
            var steps = Box(creator, Named(creator, "Setup steps"));
            Assert.That(box.min.y, Is.GreaterThan(steps.max.y), "Above the step row.");
            var footer = Named(creator, "Fixed footer");
            Assert.That(footer.GetComponentsInChildren<Button>().Where(button => WayOut.IsWayBack(WayOut.CaptionOf(button))), Is.Empty,
                "No second way back in the footer.");
            Assert.That(creator.GetComponentsInChildren<Button>().Count(button => WayOut.IsWayBack(WayOut.CaptionOf(button))), Is.EqualTo(1),
                "One way back on the page.");
            Assert.That(Buttons(creator, CharacterCreator.CancelSlotCaption), Is.Empty);
            back.onClick.Invoke();
            Assert.That(creator.IsShowing, Is.False);
            Assert.That(backs, Is.EqualTo(1), "Back to the cast.");
        }

        [UnityTest]
        public IEnumerator EditingACastHouseguestKeepsCancelThisEditAsItsOnlyWayOutAtTheTopLeft()
        {
            var creator = OpenCastSlot();
            yield return null;
            Canvas.ForceUpdateCanvases();
            var cancel = Buttons(creator, CharacterCreator.CancelSlotCaption).Single();
            var frame = Frame(creator);
            var box = Box(creator, cancel.transform);
            Assert.That(box.min.x, Is.LessThan(frame.xMin + frame.width * .25f));
            Assert.That(box.max.y, Is.GreaterThan(frame.yMax - 100f));
            Assert.That(creator.GetComponentsInChildren<Button>().Where(button => WayOut.IsWayBack(WayOut.CaptionOf(button))), Is.Empty,
                "Leaving here throws the edit away, so no way back says otherwise.");
            Assert.That(Named(creator, "Studio title").GetComponent<TMP_Text>().text, Is.EqualTo("EDIT SLOT ROBIN"));
            creator.Dismiss(); // Escape and B
            Assert.That(creator.IsShowing, Is.False);
            Assert.That(backs, Is.EqualTo(1));
        }

        // ---------------------------------------------------------------- A1: the step row

        [UnityTest]
        public IEnumerator TheStepRowShowsEachModesRealStepsNumberedFromOne()
        {
            var creator = Open();
            yield return null;
            Assert.That(StepNames(creator), Is.EqualTo(new[] { "Appearance", "Identity", "Personality", "Review" }));
            var tabs = Named(creator, "Setup steps").GetComponentsInChildren<Button>();
            for (int i = 0; i < tabs.Length; i++)
            {
                Assert.That(tabs[i].GetComponentInChildren<TMP_Text>().text, Is.EqualTo(tabs[i].name), "The caption stays the step's name.");
                var number = tabs[i].GetComponentsInChildren<TMP_Text>().Single(text => text.name == "Step number text");
                Assert.That(number.text, Is.EqualTo((i + 1).ToString()));
            }
            Assert.That(tabs.Count(tab => tab.transform.Find("Glow") != null), Is.EqualTo(1), "Exactly one step is lit.");
            Click(creator, CharacterCreator.QuickCaption);
            yield return null;
            Assert.That(StepNames(creator), Is.EqualTo(new[] { "Quick setup", "Review" }), "Quick setup's own steps.");
            Assert.That(Tab(creator, "Review").GetComponentsInChildren<TMP_Text>().Single(text => text.name == "Step number text").text, Is.EqualTo("2"));
        }

        [UnityTest]
        public IEnumerator EachStepCarriesADoneNeedsAttentionOrNotVisitedMarkByShapeAndWords()
        {
            var creator = Open(name: "");
            yield return null;
            Assert.That(creator.MarkOf("Appearance"), Is.EqualTo(CreatorSteps.Mark.Done), "Seen, with nothing missing.");
            Assert.That(creator.MarkOf("Identity"), Is.EqualTo(CreatorSteps.Mark.NotVisited));
            Click(creator, "Review");
            yield return null;
            Assert.That(creator.MarkOf("Identity"), Is.EqualTo(CreatorSteps.Mark.NeedsAttention), "Passed with the name missing.");
            Assert.That(creator.MarkOf("Personality"), Is.EqualTo(CreatorSteps.Mark.NotVisited), "Passed, but never seen.");
            Assert.That(creator.MarkOf("Review"), Is.EqualTo(CreatorSteps.Mark.NeedsAttention));

            var sprites = new Dictionary<CreatorSteps.Mark, Sprite>();
            foreach (var step in new[] { "Appearance", "Identity", "Personality" })
            {
                var tab = Tab(creator, step);
                var mark = creator.MarkOf(step);
                var shape = tab.GetComponentsInChildren<Image>(true).Single(image => image.name == "Step mark");
                var word = tab.GetComponentsInChildren<TMP_Text>(true).Single(text => text.name == "Step mark word");
                Assert.That(word.text, Is.EqualTo(CreatorSteps.Word(mark)), step + " says its mark in words.");
                Assert.That(shape.sprite, Is.Not.Null, step);
                sprites[mark] = shape.sprite;
            }
            Assert.That(sprites.Keys, Is.EquivalentTo(new[] { CreatorSteps.Mark.Done, CreatorSteps.Mark.NeedsAttention, CreatorSteps.Mark.NotVisited }));
            Assert.That(sprites.Values.Distinct().Count(), Is.EqualTo(3), "Three shapes, not three colours of one.");
            Assert.That(Named(creator, "Setup steps").GetComponentsInChildren<Button>().Count(tab => tab.transform.Find("Glow") != null), Is.EqualTo(1),
                "Marks light no second step.");
        }

        // ---------------------------------------------------------------- A2: the library

        [UnityTest]
        public IEnumerator MyHouseguestsIsAHeaderButtonThatOpensAPanelWithItsOwnClose()
        {
            var creator = Open(savedHouseguests: 1);
            yield return null;
            Canvas.ForceUpdateCanvases();
            var header = Buttons(creator, CreatorSteps.Library).Single();
            Assert.That(header.transform.IsChildOf(Named(creator, "Setup steps")), Is.False, "Not a step.");
            Assert.That(Box(creator, header.transform).min.x, Is.GreaterThan(Frame(creator).center.x), "Beside the gear, at the top right.");
            header.onClick.Invoke();
            yield return null;
            Canvas.ForceUpdateCanvases();
            Assert.That(creator.LibraryOpen, Is.True);
            Assert.That(creator.Page, Is.EqualTo("Appearance"), "Over the step, which stays the one lit.");
            Assert.That(Tab(creator, "Appearance").transform.Find("Glow"), Is.Not.Null);
            var panel = Named(creator, CreatorSteps.Library);
            var close = panel.GetComponentsInChildren<Button>().Single(button => button.name == WayOut.CloseObjectName);
            Assert.That(WayOut.CaptionOf(close), Is.EqualTo(WayOut.CloseCaption));
            var panelBox = Box(creator, panel); var closeBox = Box(creator, close.transform);
            Assert.That(closeBox.max.x, Is.GreaterThan(panelBox.max.x - 40f), "At the panel's right,");
            Assert.That(closeBox.max.y, Is.GreaterThan(panelBox.max.y - 40f), "at its top.");
            Assert.That(creator.GetComponentsInChildren<Button>().Count(button => WayOut.CaptionOf(button) == WayOut.CloseCaption), Is.EqualTo(1));
            close.onClick.Invoke();
            yield return null;
            Assert.That(creator.LibraryOpen, Is.False);
            Assert.That(creator.Page, Is.EqualTo("Appearance"), "Closing shows the step underneath.");

            Click(creator, CreatorSteps.Library);
            yield return null;
            creator.Dismiss(); // Escape and B do what the Close does
            Assert.That(creator.LibraryOpen, Is.False);
            Assert.That(creator.IsShowing, Is.True, "The panel closed; the creator stayed.");
            Assert.That(backs, Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator TakingAHouseguestAsksFirstOnlyWhenTheDraftHasUnsavedChanges()
        {
            var creator = Open(savedHouseguests: 1);
            yield return null;
            Click(creator, CreatorSteps.Library);
            yield return null;
            Click(creator, "Saved Guest 1");
            yield return null;
            Assert.That(creator.AsksToReplace, Is.False, "An untouched draft is replaced at once.");
            Assert.That(creator.Draft.Name, Is.EqualTo("Saved Guest 1"));
            Assert.That(Named(creator, "Studio title").GetComponent<TMP_Text>().text, Is.EqualTo("EDIT SAVED GUEST 1"));

            creator.Draft.Name = "Changed Robin";
            Click(creator, "Saved Guest 1");
            yield return null;
            Assert.That(creator.AsksToReplace, Is.True, "Unsaved changes: it asks.");
            var question = creator.GetComponentsInChildren<TMP_Text>().Single(text => text.name == "Replace question");
            Assert.That(question.text, Is.EqualTo("Replace your draft with Saved Guest 1?"));
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.Not.Null);
            Assert.That(EventSystem.current.currentSelectedGameObject.name, Is.EqualTo(CreatorSteps.KeepCaption), "The safe answer has the focus.");
            Click(creator, CreatorSteps.KeepCaption);
            yield return null;
            Assert.That(creator.Draft.Name, Is.EqualTo("Changed Robin"), "Kept.");

            Click(creator, "Saved Guest 1");
            yield return null;
            creator.Dismiss(); // Escape keeps the draft too
            Assert.That(creator.AsksToReplace, Is.False);
            Assert.That(creator.Draft.Name, Is.EqualTo("Changed Robin"));
            Assert.That(creator.LibraryOpen, Is.True, "Only the card closed.");

            Click(creator, "Saved Guest 1");
            yield return null;
            Click(creator, CreatorSteps.ReplaceCaption);
            yield return null;
            Assert.That(creator.Draft.Name, Is.EqualTo("Saved Guest 1"), "Replaced.");
            Assert.That(Buttons(creator, CreatorSteps.CreateCaption), Has.Length.EqualTo(1), "The library makes one by the one name.");
        }

        // ---------------------------------------------------------------- A3: the warning is a link

        [UnityTest]
        public IEnumerator TheWarningIsALinkToTheStepWithTheCursorInTheField()
        {
            var creator = Open(name: "");
            yield return null;
            var link = Buttons(creator, "Name needed - go to Identity").Single();
            Assert.That(link.name, Is.EqualTo(CharacterCreator.ProblemLinkName));
            Assert.That(link.transform.IsChildOf(Named(creator, "Fixed footer")), Is.True, "Where the warning line was.");
            Assert.That(link.GetComponentsInChildren<Image>().Any(image => image.name == "Warning glyph"), Is.True, "Not told by colour alone.");
            link.onClick.Invoke();
            yield return null;
            Assert.That(creator.Page, Is.EqualTo("Identity"));
            Assert.That(EventSystem.current.currentSelectedGameObject.name, Is.EqualTo("Name field"), "The cursor is in the name.");

            yield return Close();
            creator = Open(quick: true, name: "");
            yield return null;
            Click(creator, "Review");
            yield return null;
            Click(creator, "Name needed - go to Quick setup");
            yield return null;
            Assert.That(creator.Page, Is.EqualTo(CharacterCreator.QuickPageCaption));
            Assert.That(EventSystem.current.currentSelectedGameObject.name, Is.EqualTo("Name field"));

            creator.Draft.Name = "Named Now";
            Click(creator, "Review");
            yield return null;
            Assert.That(creator.GetComponentsInChildren<Button>().Any(button => button.name == CharacterCreator.ProblemLinkName), Is.False,
                "Nothing missing, no link.");
        }

        // ---------------------------------------------------------------- A4: one main button

        [UnityTest]
        public IEnumerator EveryCreatorStateHasExactlyOneBrightButtonAtTheBottomRight()
        {
            var problems = new List<string>();
            void Check(CharacterCreator screen, string where)
            {
                Canvas.ForceUpdateCanvases();
                var bright = Bright(screen);
                if (bright.Length != 1) { problems.Add(where + ": " + bright.Length + " bright (" + string.Join(", ", bright.Select(b => b.name)) + ")"); return; }
                var frame = Frame(screen);
                var box = Box(screen, bright[0].transform);
                if (box.center.x < frame.center.x || box.center.y > frame.center.y) problems.Add(where + ": " + bright[0].name + " is not at the bottom right");
            }

            var creator = Open(savedHouseguests: 1);
            yield return null;
            foreach (var step in new[] { "Appearance", "Identity", "Personality", "Review" })
            {
                Click(creator, step); yield return null;
                Check(creator, "Detailed " + step);
            }
            Click(creator, CreatorSteps.Library); yield return null;
            Check(creator, "the library open");
            creator.Draft.Name = "Changed Robin";
            Click(creator, "Saved Guest 1"); yield return null;
            Assert.That(creator.AsksToReplace, Is.True);
            Check(creator, "the replace card");
            Click(creator, CreatorSteps.ReplaceCaption); yield return null;
            Check(creator, "a saved houseguest, its library");
            creator.Draft.Name = "";
            Click(creator, "Review"); yield return null;
            Check(creator, "Review with the name missing");
            Click(creator, "Appearance"); yield return null;
            Click(creator, CharacterCreator.QuickCaption); yield return null;
            foreach (var step in new[] { "Quick setup", "Review" })
            {
                Click(creator, step); yield return null;
                Check(creator, "Quick " + step);
            }

            yield return Close();
            creator = OpenCastSlot();
            yield return null;
            foreach (var step in new[] { "Appearance", "Identity", "Personality", "Review" })
            {
                Click(creator, step); yield return null;
                Check(creator, "cast edit " + step);
            }
            Assert.That(Bright(creator).Single().GetComponentInChildren<TMP_Text>().text, Is.EqualTo(CharacterCreator.ApplySlotCaption));
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
        }
    }
}
