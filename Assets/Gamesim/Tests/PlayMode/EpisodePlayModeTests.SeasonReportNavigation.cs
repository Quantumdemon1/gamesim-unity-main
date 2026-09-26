using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// The season report has ways out, and they work. A spectator who opened it once had none: its
    /// only exit was at the foot of three screens that nothing on the report could scroll, clicks
    /// fell through it to the HUD behind, and Escape closed the panel underneath instead.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        private static bool Inside(RectTransform outer, RectTransform inner)
        {
            var a = new Vector3[4]; var b = new Vector3[4];
            outer.GetWorldCorners(a); inner.GetWorldCorners(b);
            return b[0].x >= a[0].x - 1f && b[0].y >= a[0].y - 1f && b[2].x <= a[2].x + 1f && b[2].y <= a[2].y + 1f;
        }

        /// <summary>
        /// Every way on is at the top of the report, on screen the moment it opens and outside the
        /// part that scrolls; each one closes the report and goes where it says.
        /// </summary>
        [UnityTest]
        public IEnumerator Report_EveryWayOnIsAtTheTopAndEachOneLeads()
        {
            var went = new List<string>();
            Report().Show(Finished(), _ => null, () => went.Add("review"), null,
                () => went.Add("new season"), () => went.Add("menu"), () => went.Add("closed"));
            yield return null;
            Canvas.ForceUpdateCanvases();
            var screen = (RectTransform)Report().transform;
            var scroll = Report().GetComponentInChildren<ScrollRect>();
            foreach (string caption in new[] { SeasonReport.NewSeasonCaption, SeasonReport.ReviewCaption, SeasonReport.MainMenuCaption, SeasonReport.CloseCaption })
            {
                var buttons = ReportButtons(caption);
                Assert.That(buttons, Has.Length.EqualTo(1), caption + " is offered once.");
                var rect = (RectTransform)buttons[0].transform;
                Assert.That(Inside(screen, rect), Is.True, caption + " is on screen when the report opens.");
                Assert.That(rect.IsChildOf(scroll.content), Is.False, caption + " stays put while the season scrolls.");
            }

            ReportButtons(SeasonReport.NewSeasonCaption)[0].onClick.Invoke();
            Assert.That(Report().IsShowing, Is.False, "Leaving closes the report.");
            Assert.That(went, Is.EqualTo(new[] { "new season" }));

            Report().Show(Finished(), _ => null, () => went.Add("review"), null,
                () => went.Add("new season"), () => went.Add("menu"), () => went.Add("closed"));
            yield return null;
            ReportButtons(SeasonReport.MainMenuCaption)[0].onClick.Invoke();
            Report().Show(Finished(), _ => null, () => went.Add("review"), null,
                () => went.Add("new season"), () => went.Add("menu"), () => went.Add("closed"));
            yield return null;
            ReportButtons(SeasonReport.ReviewCaption)[0].onClick.Invoke();
            Report().Show(Finished(), _ => null, () => went.Add("review"), null,
                () => went.Add("new season"), () => went.Add("menu"), () => went.Add("closed"));
            yield return null;
            ReportButtons(SeasonReport.CloseCaption)[0].onClick.Invoke();
            Assert.That(went, Is.EqualTo(new[] { "new season", "menu", "review", "closed" }));
            Assert.That(Report().IsShowing, Is.False);
        }

        /// <summary>
        /// The report takes the mouse: a click anywhere on it lands on it rather than on the HUD
        /// behind, and the wheel over the season reaches the part that scrolls.
        /// </summary>
        [UnityTest]
        public IEnumerator Report_CatchesTheMouseAndTheWheelScrollsTheSeason()
        {
            Report().Show(Finished(), _ => null, null);
            yield return null;
            Canvas.ForceUpdateCanvases();
            var scroll = Report().GetComponentInChildren<ScrollRect>();
            Assert.That(scroll.content.rect.height, Is.GreaterThan(scroll.viewport.rect.height), "There is more season than one screen.");

            List<RaycastResult> Hits(Vector2 point)
            {
                var hits = new List<RaycastResult>();
                EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = point }, hits);
                return hits;
            }
            // A corner of the dimmed house around the card, and the middle of the season.
            foreach (var point in new[] { new Vector2(8f, 8f), new Vector2(Screen.width * .5f, Screen.height * .5f) })
            {
                var hits = Hits(point);
                Assert.That(hits, Is.Not.Empty, "Something takes a click at " + point + ".");
                Assert.That(hits[0].gameObject.transform.IsChildOf(Report().transform), Is.True,
                    "The report, not the HUD behind it, takes a click at " + point + ": " + hits[0].gameObject.name + ".");
            }
            var viewCorners = new Vector3[4];
            scroll.viewport.GetWorldCorners(viewCorners);
            var middle = RectTransformUtility.WorldToScreenPoint(null, (viewCorners[0] + viewCorners[2]) * .5f);
            var over = Hits(middle);
            Assert.That(over, Is.Not.Empty);
            Assert.That(over[0].gameObject.GetComponentInParent<ScrollRect>(), Is.SameAs(scroll), "The wheel over the season reaches its scroll.");
            Assert.That(scroll.verticalScrollbar, Is.Not.Null, "and a scrollbar says how much there is.");

            // A wheel turn, delivered as the input module would, moves it.
            float before = scroll.content.anchoredPosition.y;
            var wheel = new PointerEventData(EventSystem.current) { position = middle, scrollDelta = new Vector2(0f, -4f) };
            ExecuteEvents.ExecuteHierarchy(over[0].gameObject, wheel, ExecuteEvents.scrollHandler);
            yield return null;
            Assert.That(scroll.content.anchoredPosition.y, Is.GreaterThan(before + 1f), "The wheel scrolls the season.");
            Report().Hide();
        }

        /// <summary>
        /// Escape closes the report and leaves the finale's panel under it; closing panels closes it
        /// too; and "Review the season" puts the notebook in front rather than behind it.
        /// </summary>
        [UnityTest]
        public IEnumerator Report_EscapeClosesItAndReviewOpensTheNotebookInFront()
        {
            // The episode screen open under the report, as it is at the finale.
            WarpPlayer(director.StationPosition);
            Assert.That(director.TryOpenPhasePanel(), Is.True);
            yield return null;
            director.ShowSeasonReport();
            yield return null;
            Assert.That(director.IsSeasonReportOpen, Is.True);
            Assert.That(director.IsPanelOpen, Is.True, "A full-screen report is a panel: the house behind it waits.");

            testKeyboard = InputSystem.AddDevice<Keyboard>();
            InputSystem.QueueStateEvent(testKeyboard, new KeyboardState(Key.Escape));
            yield return null;
            InputSystem.QueueStateEvent(testKeyboard, new KeyboardState());
            yield return null; yield return null;
            Assert.That(director.IsSeasonReportOpen, Is.False, "Escape closes the report.");
            Assert.That(director.IsPhasePanelOpen, Is.True, "and only the report: the episode screen under it stays open.");
            director.ClosePanels();
            yield return null;

            director.ShowSeasonReport();
            yield return null;
            director.ClosePanels();
            Assert.That(director.IsSeasonReportOpen, Is.False, "Closing the panels closes the report with them.");

            director.ShowSeasonReport();
            yield return null;
            ReportButtons(SeasonReport.ReviewCaption)[0].onClick.Invoke();
            yield return null;
            Assert.That(director.IsSeasonReportOpen, Is.False, "The report steps aside for the notebook.");
            Assert.That(director.IsPanelOpen, Is.True, "and the notebook is open in front of the house.");
            director.ClosePanels();
        }

        /// <summary>
        /// Sorting the house table leaves the reader where they were and the keyboard on the chip
        /// it pressed. The redraw used to start the report again at its top, three screens above
        /// the table, and hand the keyboard to "Start a new season" - so a second Enter to sort
        /// again opened the cast screen - while Tab, walking a ring of thrown-away chips, did
        /// nothing at all.
        /// </summary>
        [UnityTest]
        public IEnumerator Report_SortingTheTableKeepsTheReadersPlaceAndTheKeyboard()
        {
            director.SuspendNpcAutonomyForDiagnostics();
            int newSeasons = 0;
            Report().Show(Finished(), _ => null, () => { }, null, () => newSeasons++, () => { }, () => { });
            yield return null; yield return null;
            var events = EventSystem.current;
            string hohCaption = SeasonReport.SortCaption(SeasonReport.CastSort.HohWins);
            events.SetSelectedGameObject(ReportButtons(hohCaption)[0].gameObject);
            yield return null; yield return null;
            var scroll = Report().GetComponentInChildren<ScrollRect>();
            float reading = scroll.content.anchoredPosition.y;
            Assert.That(reading, Is.GreaterThan(100f), "The keyboard on a chip at the table's foot has the report scrolled down to it - the case this is about.");

            // Enter on the chip, as the input module raises it.
            ExecuteEvents.Execute(events.currentSelectedGameObject, new BaseEventData(events), ExecuteEvents.submitHandler);
            yield return null; yield return null;
            Assert.That(Report().IsShowing, Is.True, "Sorting keeps the report up.");
            scroll = Report().GetComponentInChildren<ScrollRect>();
            Assert.That(scroll.content.anchoredPosition.y, Is.EqualTo(reading).Within(1f),
                "The redrawn report is still where the reader was, not back at the winner.");
            var selected = events.currentSelectedGameObject;
            Assert.That(selected != null && selected.activeInHierarchy && selected.transform.IsChildOf(Report().transform), Is.True,
                "The keyboard is on a live control of the report, not on one the redraw threw away.");
            Assert.That(selected.name, Is.EqualTo(hohCaption), "and on the chip it pressed.");
            Assert.That(Inside(scroll.viewport, (RectTransform)selected.transform), Is.True, "which is in view.");

            yield return PressKey(Key.Tab);
            Assert.That(events.currentSelectedGameObject != null ? events.currentSelectedGameObject.name : "nothing",
                Is.EqualTo(SeasonReport.SortCaption(SeasonReport.CastSort.VetoWins)), "Tab walks on from the chip to the next.");
            yield return PressKey(Key.Tab, shift: true);
            Assert.That(events.currentSelectedGameObject != null ? events.currentSelectedGameObject.name : "nothing",
                Is.EqualTo(hohCaption), "and Shift+Tab walks back.");

            ExecuteEvents.Execute(events.currentSelectedGameObject, new BaseEventData(events), ExecuteEvents.submitHandler);
            yield return null; yield return null;
            Assert.That(newSeasons, Is.Zero, "A second Enter sorts again; it does not start a new season.");
            Assert.That(Report().IsShowing, Is.True);
            Report().Hide();
        }

        /// <summary>
        /// The keyboard's chip comes into view once, when the keyboard moves onto it, and the wheel
        /// can then read back up the season with the chip still selected. The report used to pull
        /// itself back to a selected chip on every frame, so neither the wheel nor a drag could
        /// leave it until something else was clicked.
        /// </summary>
        [UnityTest]
        public IEnumerator Report_TheKeyboardsChipComesIntoViewOnceAndTheWheelCanLeaveIt()
        {
            director.SuspendNpcAutonomyForDiagnostics();
            Report().Show(Finished(), _ => null, null);
            yield return null; yield return null;
            var scroll = Report().GetComponentInChildren<ScrollRect>();
            Assert.That(scroll.content.anchoredPosition.y, Is.EqualTo(0f).Within(.5f), "The report opens at its top.");

            var chip = ReportButtons(SeasonReport.SortCaption(SeasonReport.CastSort.Placement))[0];
            EventSystem.current.SetSelectedGameObject(chip.gameObject);
            yield return null; yield return null;
            float revealedAt = scroll.content.anchoredPosition.y;
            Assert.That(revealedAt, Is.GreaterThan(100f), "The keyboard on a chip at the foot of the table scrolls the report down to it.");
            Assert.That(Inside(scroll.viewport, (RectTransform)chip.transform), Is.True, "and brings it into view.");

            // A wheel turn back up to reread the jury, delivered as the input module would.
            var corners = new Vector3[4];
            scroll.viewport.GetWorldCorners(corners);
            var middle = RectTransformUtility.WorldToScreenPoint(null, (corners[0] + corners[2]) * .5f);
            var wheel = new PointerEventData(EventSystem.current) { position = middle, scrollDelta = new Vector2(0f, 40f) };
            ExecuteEvents.ExecuteHierarchy(scroll.viewport.gameObject, wheel, ExecuteEvents.scrollHandler);
            yield return ForSeconds(.2f);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(chip.gameObject),
                "The chip is still selected - the case this is about.");
            Assert.That(scroll.content.anchoredPosition.y, Is.LessThan(revealedAt - 100f),
                "The wheel reads back up the season, and the selected chip does not drag it back down.");
            Assert.That(Inside(scroll.viewport, (RectTransform)chip.transform), Is.False, "The chip stays where the wheel left it, out of view.");
            Report().Hide();
        }

        /// <summary>
        /// Page Up and Page Down, Home and End, and a pad's right stick scroll the season, with the
        /// keyboard left where it was. The ring runs from the ways on at the top to the chips at
        /// the foot, so without these nothing between the two could be reached without a mouse -
        /// and the right stick, which the input module also reads as a step round the ring, walked
        /// the focus down onto the chips while it scrolled.
        /// </summary>
        [UnityTest]
        public IEnumerator Report_PageKeysAndTheRightStickScrollTheSeasonAndLeaveTheKeyboardBe()
        {
            director.SuspendNpcAutonomyForDiagnostics();
            // The right stick is the camera's too; behind a report, as behind any panel, it is not.
            cameraRig.ControlsEnabled = false;
            Report().Show(Finished(), _ => null, () => { }, null, () => { }, () => { }, () => { });
            yield return null; yield return null;
            Assert.That(ReportLabels(), Does.Contain(SeasonReport.ScrollHint), "The report says how to scroll it without a mouse.");
            var scroll = Report().GetComponentInChildren<ScrollRect>();
            var content = scroll.content;
            float window = scroll.viewport.rect.height, travel = content.rect.height - window;
            Assert.That(travel, Is.GreaterThan(window * .5f), "More than half a screen of season lies below the first - the case this is about.");
            var focus = EventSystem.current.currentSelectedGameObject;
            Assert.That(focus != null && focus.transform.IsChildOf(Report().transform) && !focus.transform.IsChildOf(content), Is.True,
                "The keyboard starts on a way on at the top of the report.");

            yield return PressKey(Key.PageDown);
            float paged = content.anchoredPosition.y;
            Assert.That(paged, Is.GreaterThan(Mathf.Min(travel, window * .5f) - 1f).And.LessThanOrEqualTo(travel + .5f), "Page Down reads on a page.");
            yield return PressKey(Key.End);
            Assert.That(content.anchoredPosition.y, Is.EqualTo(travel).Within(1f), "End goes to the foot of the season.");
            yield return PressKey(Key.PageUp);
            Assert.That(content.anchoredPosition.y, Is.LessThan(travel - 1f), "Page Up reads back a page.");
            yield return PressKey(Key.Home);
            Assert.That(content.anchoredPosition.y, Is.EqualTo(0f).Within(1f), "Home goes back to the top.");
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(focus), "and none of them moved the keyboard.");

            testGamepad = InputSystem.AddDevice<Gamepad>();
            InputSystem.QueueStateEvent(testGamepad, new GamepadState { rightStick = new Vector2(0f, -1f) });
            yield return Settle(() => content.anchoredPosition.y > 200f, 3f);
            float leaned = content.anchoredPosition.y;
            // Held a moment longer, past the input module's repeat delay, so a step round the ring
            // would have had every chance to happen.
            yield return ForSeconds(.6f);
            InputSystem.QueueStateEvent(testGamepad, new GamepadState());
            yield return null;
            Assert.That(leaned, Is.GreaterThan(200f), "Leaning the right stick down reads on down the season.");
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(focus),
                "and takes no step round the keyboard ring while it does.");

            float from = content.anchoredPosition.y;
            InputSystem.QueueStateEvent(testGamepad, new GamepadState { rightStick = new Vector2(0f, 1f) });
            yield return Settle(() => content.anchoredPosition.y < from - 100f, 3f);
            InputSystem.QueueStateEvent(testGamepad, new GamepadState());
            yield return null;
            Assert.That(content.anchoredPosition.y, Is.LessThan(from - 100f), "Leaning it up reads back up.");
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(focus));
            Report().Hide();
        }
    }
}
