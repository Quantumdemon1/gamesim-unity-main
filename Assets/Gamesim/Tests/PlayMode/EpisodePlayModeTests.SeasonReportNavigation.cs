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
    }
}
