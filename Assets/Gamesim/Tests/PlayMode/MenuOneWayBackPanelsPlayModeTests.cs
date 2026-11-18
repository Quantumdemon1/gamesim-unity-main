using System;
using System.Collections;
using System.Linq;
using Gamesim.Episode;
using Gamesim.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// Menu slice 2, part c: the in-season panels and pages have one way out each, drawn by the shared
    /// helper (WayOut). A panel over the house has "Close" at its top right, the key it answers to as
    /// a hint beside the word; a page reached from another page inside the panel has "Back to &lt;that
    /// page&gt;" at its top left as well, and Escape and the pad's B press that before they close the
    /// panel. An earlier week of the recap names where its way back returns; the overview, which had
    /// no control to leave by, has its Close.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        [UnityTest]
        public IEnumerator MenuWayOutPanels_APanelsCloseIsTheHelpersAtItsTopRight_WithEscapeBesideIt()
        {
            director.OpenJournal();
            yield return Frames(2);
            Canvas.ForceUpdateCanvases();
            var panel = ActiveRect(ModalRoot);
            var close = WayOutOnFrame(panel, WayOut.CloseObjectName);
            Assert.That(close, Is.Not.Null, "The notebook has the helper's Close.");
            Assert.That(WayOut.CaptionOf(close), Is.EqualTo("Close"), "Its caption is the one word, no key in it.");
            Assert.That(WayOut.KeyHintOf(close), Is.EqualTo(InputGlossary.BackKeyHint), "The key stands beside it as a hint.");
            Assert.That(director.GetComponentsInChildren<TMPro.TMP_Text>().Any(text => text.text == "Close  [Esc]"), Is.False,
                "No control says 'Close  [Esc]' any more.");
            var box = ScreenRect((RectTransform)close.transform);
            var frame = ScreenRect(panel);
            Assert.That(frame.xMax - box.xMax, Is.LessThan(40f), "Close stands at the panel's right edge: " + box + " in " + frame + ".");
            Assert.That(frame.yMax - box.yMax, Is.LessThan(40f), "and at its top.");
            Assert.That(WayOutOnFrame(panel, WayOut.BackObjectName), Is.Null, "The notebook's first page has no way back: it is not reached from a page.");
            close.onClick.Invoke();
            yield return null;
            Assert.That(director.IsPanelOpen, Is.False, "Close closes the panel.");
        }

        [UnityTest]
        public IEnumerator MenuWayOutPanels_AProfileGoesBackToTheDirectory_ByItsWayBackAndByEscape()
        {
            yield return SettleCast();
            director.ShowNotebookSection(EpisodeDirector.NotebookSection.People);
            yield return null;
            var actor = director.Snapshot.contestants.First(c => !c.isPlayer && c.status == Gamesim.Simulation.ContestantStatus.Active);
            ActiveRect(EpisodeHud.RosterRowPrefix + actor.name).GetComponent<Button>().onClick.Invoke();
            yield return Frames(2);
            Assert.That(director.ProfileId, Is.EqualTo(actor.id), "The row opens the profile.");
            AssertPageWayBack(EpisodeHud.ProfileBackCaption, "A houseguest's profile");

            yield return PressKey(Key.Escape);
            yield return null;
            Assert.That(director.ProfileId, Is.Null, "Escape goes back a page, as the way back does,");
            Assert.That(director.IsPanelOpen, Is.True, "and leaves the notebook open.");
            Assert.That(ActiveRect(EpisodeHud.RosterRowPrefix + actor.name), Is.Not.Null, "Back is the directory.");
            Assert.That(WayOutOnFrame(ActiveRect(ModalRoot), WayOut.BackObjectName), Is.Null, "The directory has no way back of its own.");
            Assert.That(WayOut.KeyHintOf(WayOutOnFrame(ActiveRect(ModalRoot), WayOut.CloseObjectName)), Is.EqualTo(InputGlossary.BackKeyHint),
                "With no way back, Escape is Close's again, and its hint says so.");
            director.ClosePanels();
            yield return null;
        }

        [UnityTest]
        public IEnumerator MenuWayOutPanels_DealsAndPromisesGoesBackToTheNotes_ByEscape()
        {
            director.ShowNotebookSection(EpisodeDirector.NotebookSection.Word);
            yield return Frames(2);
            Assert.That(director.ActiveSection, Is.EqualTo(EpisodeDirector.NotebookSection.Word), "Deals & promises is open.");
            AssertPageWayBack(EpisodeDirector.BackToYourNotesCaption, "Deals & promises");
            Assert.That(director.GetComponentsInChildren<TMPro.TMP_Text>().Any(text => text.text == "No commitments"), Is.False,
                "The page calls them deals and promises, never commitments.");

            yield return PressKey(Key.Escape);
            yield return null;
            Assert.That(director.IsPanelOpen, Is.True, "Escape leaves the notebook open");
            Assert.That(director.ActiveSection, Is.EqualTo(EpisodeDirector.NotebookSection.Notes), "and goes back to the notes.");
            director.ClosePanels();
            yield return null;
        }

        [UnityTest]
        public IEnumerator MenuWayOutPanels_AFreeTimeBeatGoesBackToTheBoard_ByEscape()
        {
            yield return InstallFreeTime(8, FreeTimeWaiting.Beat);
            yield return OpenStation();
            yield return Frames(2);
            ButtonWithCaption(EpisodeDirector.AnswerBeatCaption).onClick.Invoke();
            yield return Frames(2);
            Assert.That(director.FreeTimeBeatOpen, Is.Not.Null, "Answer opens the beat as the step.");
            AssertPageWayBack(EpisodeDirector.BackToFreeTimeCaption, "A beat opened from free time");
            int revision = director.Snapshot.revision;

            yield return PressKey(Key.Escape);
            yield return null;
            Assert.That(director.FreeTimeBeatOpen, Is.Null, "Escape goes back to the board, as the way back does,");
            Assert.That(director.IsPanelOpen && director.IsFreeTimeBoard, Is.True, "with the panel still open on it,");
            Assert.That(director.Snapshot.revision, Is.EqualTo(revision), "answering nothing.");
            director.ClosePanels();
            yield return null;
        }

        [UnityTest]
        public IEnumerator MenuWayOutPanels_AnEarlierWeekSaysWhereItsWayBackReturns_AndEscapePressesIt()
        {
            var screen = director.GetComponentInChildren<WeeklyRecapScreen>(true);
            director.ReviewWeek(1);
            yield return null;
            Assert.That(screen.IsOpen && screen.Browsing, Is.True, "An earlier week is open.");
            var back = RecapWayBack(screen);
            Assert.That(WayOut.CaptionOf(back), Is.EqualTo(WeeklyRecapScreen.BackToHouseCaption),
                "Reviewed from the house, it goes back to the house, and says so.");
            Assert.That(DirectorHasButton(WeeklyRecapScreen.BackCaption), Is.False, "Not 'Back to this week': no week is behind it.");
            yield return PressKey(Key.Escape);
            yield return null;
            Assert.That(screen.IsOpen, Is.False, "Escape presses it.");

            director.OpenJournal();
            yield return Frames(2);
            director.ReviewWeek(1);
            yield return null;
            back = RecapWayBack(screen);
            Assert.That(WayOut.CaptionOf(back), Is.EqualTo(WeeklyRecapScreen.BackToNotebookCaption), "From the notebook, back to the notebook.");
            yield return PressKey(Key.Escape);
            yield return Frames(2);
            Assert.That(screen.IsOpen, Is.False, "Escape leaves the earlier week");
            Assert.That(director.IsPanelOpen && director.ActiveSection != null, Is.True, "for the notebook it came from.");
            director.ClosePanels();
            yield return null;
        }

        [UnityTest]
        public IEnumerator MenuWayOutPanels_TheOverviewHasItsClose_OnTheBriefingAndOnTheMap()
        {
            yield return SettleCast();
            Assert.That(director.ToggleOverview(), Is.True);
            yield return Frames(2);
            Canvas.ForceUpdateCanvases();
            var sheet = ActiveRect(EpisodeHud.DashboardName);
            var close = WayOutOnFrame(sheet, WayOut.CloseObjectName);
            Assert.That(close, Is.Not.Null, "The briefing has a Close.");
            Assert.That(WayOut.KeyHintOf(close), Is.EqualTo(InputGlossary.BackKeyHint), "with Escape beside it.");
            var map = ButtonWithCaption(EpisodeDirector.ShowMapCaption);
            Assert.That(ScreenRect((RectTransform)map.transform).Overlaps(ScreenRect((RectTransform)close.transform)), Is.False,
                "'" + EpisodeDirector.ShowMapCaption + "' stands beside it, not under it.");
            map.onClick.Invoke();
            yield return Frames(2);
            Canvas.ForceUpdateCanvases();
            Assert.That(director.IsOverview && !director.IsBriefing, Is.True, "The map is up.");
            var column = ActiveRect(EpisodeHud.OverviewColumnName);
            close = WayOutOnFrame(column, WayOut.CloseObjectName);
            Assert.That(close, Is.Not.Null, "The map has its Close, on the Who Is Where card.");
            foreach (var words in column.GetComponentsInChildren<TMPro.TMP_Text>())
                if (!words.transform.IsChildOf(close.transform) && !string.IsNullOrWhiteSpace(words.text))
                    Assert.That(ScreenRect(words.rectTransform).Overlaps(ScreenRect((RectTransform)close.transform)), Is.False,
                        "'" + words.text + "' stands clear of Close.");
            close.onClick.Invoke();
            yield return Frames(2);
            Assert.That(director.IsOverview, Is.False, "Close leaves the overview.");
        }

        // ---------------------------------------------------------------- helpers

        /// <summary>The live way out of this name drawn on <paramref name="frame"/> itself, the newest if a rebuild left two.</summary>
        private static Button WayOutOnFrame(RectTransform frame, string name) => frame == null ? null
            : frame.GetComponentsInChildren<Button>().LastOrDefault(button => button.IsActive() && button.name == name);

        /// <summary>
        /// A page's way back: one on the panel, drawn by the helper at its top left, with this
        /// caption, Escape beside it, and Close at the top right without the hint - the key presses
        /// the way back now. Clear of every word of the panel's head.
        /// </summary>
        private Button AssertPageWayBack(string caption, string where)
        {
            Canvas.ForceUpdateCanvases();
            var panel = ActiveRect(ModalRoot);
            Assert.That(panel, Is.Not.Null, where + ": the panel is up.");
            var backs = panel.GetComponentsInChildren<Button>().Where(button => button.IsActive() && button.name == WayOut.BackObjectName).ToArray();
            Assert.That(backs, Has.Length.EqualTo(1), where + " has one way back.");
            var back = backs[0];
            Assert.That(WayOut.CaptionOf(back), Is.EqualTo(caption), where + ": its words.");
            Assert.That(back.transform.parent, Is.SameAs(panel), where + ": it stands on the panel, not in its scroll.");
            var box = ScreenRect((RectTransform)back.transform);
            var frame = ScreenRect(panel);
            Assert.That(box.xMin - frame.xMin, Is.LessThan(40f), where + ": at the panel's left: " + box + " in " + frame + ".");
            Assert.That(frame.yMax - box.yMax, Is.LessThan(40f), where + ": at its top.");
            Assert.That(WayOut.KeyHintOf(back), Is.EqualTo(InputGlossary.BackKeyHint), where + ": Escape beside the way back,");
            var close = WayOutOnFrame(panel, WayOut.CloseObjectName);
            if (close != null)
            {
                Assert.That(WayOut.KeyHintOf(close), Is.Null, where + ": and no longer beside Close.");
                Assert.That(ScreenRect((RectTransform)close.transform).Overlaps(box), Is.False, where + ": the two ways out never meet.");
            }
            Assert.That(back.navigation.mode, Is.Not.EqualTo(Navigation.Mode.None), where + ": the keyboard reaches it.");
            foreach (Transform child in panel)
            {
                if (!child.gameObject.activeInHierarchy || child == back.transform) continue;
                if (child.name != "Phase band" && child.name != EpisodeHud.NotebookHeaderName && child.name != EpisodeHud.DiaryHeaderName) continue;
                foreach (var words in child.GetComponentsInChildren<TMPro.TMP_Text>())
                    if (!string.IsNullOrWhiteSpace(words.text))
                        Assert.That(ScreenRect(words.rectTransform).Overlaps(box), Is.False, where + ": '" + words.text + "' in the head stands clear of the way back.");
            }
            return back;
        }

        /// <summary>The way back an earlier week of the recap draws, the newest.</summary>
        private static Button RecapWayBack(WeeklyRecapScreen screen) =>
            screen.GetComponentsInChildren<Button>().LastOrDefault(button => button.IsActive() && button.name == WayOut.BackObjectName);

        /// <summary>Enter on the control <paramref name="find"/> returns, found again after a frame, as a player's keyboard presses whatever is on screen.</summary>
        private IEnumerator KeyboardSubmitTo(Func<Button> find, string what)
        {
            var target = find();
            Assert.That(target, Is.Not.Null, "Expected a keyboard-reachable control: " + what);
            Assert.That(target.navigation.mode, Is.Not.EqualTo(Navigation.Mode.None), what + " must be in the keyboard ring.");
            EventSystem.current.SetSelectedGameObject(target.gameObject);
            yield return null;
            target = find();
            Assert.That(target, Is.Not.Null, what + " left the screen between selecting it and pressing it.");
            EventSystem.current.SetSelectedGameObject(target.gameObject);
            ExecuteEvents.Execute(target.gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
            yield return null;
            yield return null;
        }
    }
}
