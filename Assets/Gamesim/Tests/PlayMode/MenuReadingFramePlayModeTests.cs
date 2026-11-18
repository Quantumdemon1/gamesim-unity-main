using System.Collections;
using System.Collections.Generic;
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
    /// Menu slice 3, the review's change 5: a READING panel - every page of the notebook - opens in
    /// one tall frame beside the rail, over a veil that dims the house and takes the pointer; a QUICK
    /// panel keeps exactly the shape it had. The frame is found through the HUD's own Close (the live
    /// panel, never a fading copy), and every case opens what it measures itself, so none depends on
    /// a case before it, on where the house's people stand, or on wall-clock time.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        [UnityTest]
        public IEnumerator MenuReadingFrame_TheNotebookOpensTallOverTheVeil_AtEveryTextSizeAndInTheCompactHud()
        {
            var hud = director.GetComponent<EpisodeHud>();
            var originalSize = director.TextSize;
            bool originalCompact = director.CompactHud;
            try
            {
                foreach (bool compact in new[] { false, true })
                foreach (var size in TextSizes.All)
                {
                    director.SetTextSize(size);
                    director.SetCompactHud(compact);
                    director.ClosePanels();
                    yield return null;
                    director.OpenJournal();
                    yield return Frames(2);
                    Canvas.ForceUpdateCanvases();
                    string at = (compact ? "compact HUD, " : "full HUD, ") + TextSizes.Name(size) + " text";

                    Assert.That(hud.FontScale, Is.EqualTo(TextSizes.Scale(size)).Within(.001f), at + ": the size is in force.");
                    Assert.That(hud.IsReadingFrame, Is.True, at + ": the notebook stands in the reading frame.");
                    Assert.That(hud.CurrentPanelKind, Is.EqualTo(PanelKind.Reading), at);
                    var panel = LiveReadingPanel(hud);
                    Assert.That(panel, Is.Not.Null, at + ": the panel is up.");
                    var root = (RectTransform)panel.parent;
                    var box = EpisodeHud.ReadingBox(root.rect.width, root.rect.height, hud.FontScale);
                    Assert.That(panel.anchoredPosition.x, Is.EqualTo(box.X).Within(.5f), at + ": the frame's left edge, " + box + ".");
                    Assert.That(panel.anchoredPosition.y, Is.EqualTo(box.Y).Within(.5f), at + ": it stands on the status line's band.");
                    Assert.That(panel.rect.width, Is.EqualTo(box.Width).Within(.5f), at + ": its width.");
                    Assert.That(panel.rect.height, Is.EqualTo(box.Height).Within(.5f), at + ": its height.");
                    Assert.That(panel.rect.height, Is.GreaterThanOrEqualTo(root.rect.height * .6f), at + ": a tall page, not a strip.");

                    var veil = LiveVeil(root);
                    Assert.That(veil, Is.Not.Null, at + ": the veil is up.");
                    Assert.That(veil.GetSiblingIndex(), Is.EqualTo(0), at + ": under every other piece of the HUD, so nothing it covers takes a click.");
                    Assert.That(veil.rect.size.x, Is.EqualTo(root.rect.width).Within(.5f), at + ": over the whole house view.");
                    Assert.That(veil.rect.size.y, Is.EqualTo(root.rect.height).Within(.5f), at);
                    var dim = veil.GetComponent<Image>();
                    Assert.That(dim.raycastTarget, Is.True, at + ": it takes the pointer.");
                    Assert.That(dim.color.a, Is.EqualTo(ReadingFrame.VeilOpacity).Within(.01f), at + ": the house is dimmed.");
                    Assert.That(veil.GetComponentsInChildren<Selectable>(true), Is.Empty, at + ": the veil offers no control.");

                    // What stands down is wholly off; what stays up is wholly clear of the frame.
                    foreach (var name in new[] { CastRail.RootName, CastRail.StripName, "Exploration controls", EpisodeHud.HouseVibeCardName, "Interaction prompt" })
                        Assert.That(ActiveUnder(root, name), Is.Null, at + ": '" + name + "' stands down while the page is up.");
                    var rail = ActiveUnder(root, IconRail.RootName);
                    Assert.That(rail, Is.Not.Null, at + ": the rail stays up, to turn the pages.");
                    Assert.That(ScreenRect(rail).Overlaps(ScreenRect(panel)), Is.False, at + ": the frame stands beside the rail.");
                    var status = ActiveUnder(root, "Status");
                    if (status != null)
                        Assert.That(ScreenRect(status).Overlaps(ScreenRect(panel)), Is.False, at + ": the status line stays whole under the frame.");
                    Assert.That(hud.PanelClose, Is.Not.Null, at + ": Close is on the frame.");
                }
            }
            finally
            {
                director.SetTextSize(originalSize);
                director.SetCompactHud(originalCompact);
                director.ClosePanels();
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator MenuReadingFrame_AQuickPanelKeepsTheShapeItHad()
        {
            var hud = director.GetComponent<EpisodeHud>();
            director.ClosePanels();
            yield return null;
            // The house activities: a short choice over the house, in the diary's column.
            director.OpenHouseActivities();
            yield return Frames(2);
            Canvas.ForceUpdateCanvases();
            Assert.That(hud.CurrentActivityLayout, Is.EqualTo(EpisodeHud.ActivityLayout.Diary), "The house activities are open in their column.");
            Assert.That(hud.CurrentPanelKind, Is.EqualTo(PanelKind.Quick));
            Assert.That(hud.IsReadingFrame, Is.False);
            var panel = LiveReadingPanel(hud);
            Assert.That(panel, Is.Not.Null, "The panel is up.");
            var root = (RectTransform)panel.parent;
            Assert.That(LiveVeil(root), Is.Null, "A QUICK panel has no veil: the house stays in view.");

            // The diary column's own arithmetic, as it was before the frame.
            float s = hud.FontScale, width = root.rect.width, height = root.rect.height, floor = EpisodeHud.PanelFloor(s);
            Assert.That(panel.anchorMin, Is.EqualTo(new Vector2(1f, 0f)));
            Assert.That(panel.anchorMax, Is.EqualTo(new Vector2(1f, 0f)));
            Assert.That(panel.pivot, Is.EqualTo(new Vector2(1f, 0f)));
            Assert.That(panel.anchoredPosition.x, Is.EqualTo(-24f).Within(.5f), "At the right edge's margin.");
            Assert.That(panel.anchoredPosition.y, Is.EqualTo(floor).Within(.5f), "On the status line's band.");
            Assert.That(panel.rect.width, Is.EqualTo(Mathf.Min(420f * s, width * .345f, width - EpisodeHud.LeftColumnX - 24f)).Within(.5f), "The column's width.");
            Assert.That(panel.rect.height, Is.EqualTo(Mathf.Max(300f, height - floor - 80f)).Within(.5f), "The column's height.");
            Assert.That(ActiveUnder(root, CastRail.RootName), Is.Not.Null, "The cast strip stays up beside it.");
            director.ClosePanels();
            yield return null;
        }

        [UnityTest]
        public IEnumerator MenuReadingFrame_AClickOnTheVeilNeitherClosesThePageNorReachesTheHouse()
        {
            var hud = director.GetComponent<EpisodeHud>();
            // The house holds still, so the revision below measures the click alone.
            director.SuspendNpcAutonomyForDiagnostics();
            director.ClosePanels();
            yield return null;
            director.OpenJournal();
            yield return Frames(2);
            Canvas.ForceUpdateCanvases();
            var panel = LiveReadingPanel(hud);
            Assert.That(panel, Is.Not.Null, "The notebook is open.");
            var veil = LiveVeil((RectTransform)panel.parent);
            Assert.That(veil, Is.Not.Null, "The veil is up.");
            Assert.That(EventSystem.current, Is.Not.Null, "The HUD has its event system.");

            // A point of the screen where the veil is the topmost thing: the dimmed house.
            Vector2? onVeil = null;
            var hits = new List<RaycastResult>();
            for (int row = 1; row < 16 && onVeil == null; row++)
            for (int column = 1; column < 24 && onVeil == null; column++)
            {
                var point = new Vector2(Screen.width * column / 24f, Screen.height * row / 16f);
                hits.Clear();
                EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = point }, hits);
                if (hits.Count > 0 && hits[0].gameObject == veil.gameObject) onVeil = point;
            }
            Assert.That(onVeil.HasValue, Is.True, "Somewhere on the screen the dimmed house shows, under the veil.");
            // The pointer is over the HUD there, so the house's own click never sees it.
            hits.Clear();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = onVeil.Value }, hits);
            Assert.That(hits.Count, Is.GreaterThan(0), "The veil takes the pointer: the house is not reached.");

            string section = director.ActiveSection;
            long revision = director.Snapshot.revision;
            var press = new PointerEventData(EventSystem.current) { position = onVeil.Value, button = PointerEventData.InputButton.Left };
            ExecuteEvents.ExecuteHierarchy(veil.gameObject, press, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.ExecuteHierarchy(veil.gameObject, press, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.ExecuteHierarchy(veil.gameObject, press, ExecuteEvents.pointerClickHandler);
            yield return Frames(2);
            Assert.That(director.IsPanelOpen, Is.True, "A click on the dimmed house does not close the page.");
            Assert.That(hud.IsReadingFrame, Is.True, "The page still stands in its frame.");
            Assert.That(director.ActiveSection, Is.EqualTo(section), "on the same page,");
            Assert.That(director.Snapshot.revision, Is.EqualTo(revision), "and the click committed nothing.");
            director.ClosePanels();
            yield return null;
        }

        [UnityTest]
        public IEnumerator MenuReadingFrame_CloseEscapeAndThePadsBStillCloseThePage()
        {
            var hud = director.GetComponent<EpisodeHud>();
            // No invitation can arrive to take the key first: the house holds still.
            director.SuspendNpcAutonomyForDiagnostics();
            foreach (var way in new[] { "Close", "Escape", "B" })
            {
                director.ClosePanels();
                yield return null;
                director.OpenJournal();
                yield return Frames(2);
                Assert.That(hud.IsReadingFrame, Is.True, way + ": the notebook opens in the reading frame.");
                Assert.That(hud.WayBackAction, Is.Null, way + ": the notebook's first page has no way back, so the key closes.");
                if (way == "Close") hud.PanelClose.onClick.Invoke();
                else if (way == "Escape") yield return PressKey(Key.Escape);
                else yield return PressBack();
                yield return null;
                Assert.That(director.IsPanelOpen, Is.False, way + " closes the page.");
                Assert.That(hud.IsReadingFrame, Is.False, way + ": the frame goes with it.");
                Assert.That(ActiveUnderDirector(ReadingFrame.VeilName), Is.Null, way + ": and so does the veil.");
            }
        }

        [UnityTest]
        public IEnumerator MenuReadingFrame_ReducedMotionShowsTheFrameAtOnce_OtherwiseItFadesWithoutASlide()
        {
            var hud = director.GetComponent<EpisodeHud>();
            bool original = hud.ReducedMotion;
            try
            {
                hud.ReducedMotion = true;
                director.ClosePanels();
                yield return Frames(2);
                director.OpenJournal();
                // The render that opens it, before any frame has passed.
                var panel = LiveReadingPanel(hud);
                Assert.That(panel, Is.Not.Null, "The notebook is open.");
                Assert.That(hud.IsReadingFrame, Is.True);
                Assert.That(panel.GetComponent<HudReveal>(), Is.Null, "Reduced motion: no arrival to wait for.");
                var group = panel.GetComponent<CanvasGroup>();
                Assert.That(group == null || group.alpha >= 1f, Is.True, "The frame is whole at once.");
                var veil = LiveVeil((RectTransform)panel.parent);
                Assert.That(veil, Is.Not.Null);
                Assert.That(veil.GetComponent<HudReveal>(), Is.Null, "and so is the veil, which never fades.");

                hud.ReducedMotion = false;
                director.ClosePanels();
                yield return Frames(2);
                director.OpenJournal();
                panel = LiveReadingPanel(hud);
                Assert.That(panel, Is.Not.Null);
                Assert.That(panel.GetComponent<HudReveal>(), Is.Not.Null, "With motion, the frame fades in.");
                var root = (RectTransform)panel.parent;
                var box = EpisodeHud.ReadingBox(root.rect.width, root.rect.height, hud.FontScale);
                yield return null;
                panel = LiveReadingPanel(hud);
                Assert.That(panel, Is.Not.Null);
                Assert.That(panel.anchoredPosition.y, Is.EqualTo(box.Y).Within(.01f), "It fades where it stands: no slide.");
                Assert.That(panel.anchoredPosition.x, Is.EqualTo(box.X).Within(.01f));
                Assert.That(HudReveal.Duration, Is.LessThanOrEqualTo(.2f), "in a fifth of a second at most.");
            }
            finally
            {
                hud.ReducedMotion = original;
                director.ClosePanels();
            }
            yield return null;
        }

        /// <summary>The live docked panel: the parent of the HUD's own Close, never a fading copy of the last one.</summary>
        private static RectTransform LiveReadingPanel(EpisodeHud hud)
        {
            if (hud == null || hud.PanelClose == null) return null;
            var panel = hud.PanelClose.transform.parent as RectTransform;
            return panel != null && panel.name == ModalRoot ? panel : null;
        }

        /// <summary>The live veil: an active child of the HUD's canvas by its name, or null.</summary>
        private static RectTransform LiveVeil(RectTransform canvasRoot)
        {
            if (canvasRoot == null) return null;
            foreach (Transform child in canvasRoot)
                if (child.name == ReadingFrame.VeilName && child.gameObject.activeInHierarchy) return (RectTransform)child;
            return null;
        }

        private static RectTransform ActiveUnder(RectTransform root, string name) => root.GetComponentsInChildren<RectTransform>(false)
            .FirstOrDefault(rect => rect.name == name && rect.gameObject.activeInHierarchy);

        private RectTransform ActiveUnderDirector(string name) => director.GetComponentsInChildren<RectTransform>(false)
            .FirstOrDefault(rect => rect.name == name && rect.gameObject.activeInHierarchy);
    }
}
