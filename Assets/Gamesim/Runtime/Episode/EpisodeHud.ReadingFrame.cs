using System;
using Gamesim.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace Gamesim.Episode
{
    /// <summary>
    /// The tall reading frame (menu slice 3, the review's change 5). A READING panel - every page of
    /// the notebook, the houseguests and a houseguest's profile among them - opens in one frame about
    /// seven tenths of the canvas tall, beside the left rail, over a veil that dims the house. The
    /// kind of every docked layout is listed once, in <see cref="ReadingFrame.Kinds"/>; a QUICK
    /// panel keeps the shape its layout gives it.
    ///
    /// <para>How a page uses it: the director calls <see cref="UseReadingFrame"/> after the render's
    /// Begin (the notebook's render does it for every page); the page's head is
    /// <see cref="ReadingHeader"/>; a fixed row under the head (a tab strip) is built into
    /// <see cref="ReadingBand"/>; the body is the panel's ordinary rows, which scroll inside the
    /// frame; a page with a main action pins it in the footer with <see cref="ReadingFooter"/>. The
    /// way back (<see cref="WayBack"/>) and Close stand at the head's corners as on every panel.</para>
    ///
    /// <para>The veil is the canvas's first child, so the chrome that stays up while a page is open
    /// (the rail, the top bar, the status line) is drawn over it whole and keeps its clicks, and the
    /// chrome that stands down (the cast strip, the right column, the controls box, the prompt) is
    /// switched off, as it was for the notebook before the frame. The veil takes the pointer: a click
    /// on the dimmed house reaches nothing and closes nothing. Whether the house's clock runs is the
    /// opener's business and unchanged by the frame.</para>
    /// </summary>
    public sealed partial class EpisodeHud
    {
        /// <summary>The frame's head, the notebook's: a test finds it by this name.</summary>
        public const string ReadingHeaderName = NotebookHeaderName;
        /// <summary>The band under the head a page builds a fixed row into (a tab strip): a test finds it by the name the page gives it.</summary>
        public const string ReadingBandName = "Reading band";

        private RectTransform readingVeil;

        /// <summary>A docked layout's kind (<see cref="ReadingFrame.Kinds"/>); a layout the table does not list throws.</summary>
        public static PanelKind KindOf(ActivityLayout layout) => ReadingFrame.KindOf(layout.ToString());

        /// <summary>The kind of the panel up now: READING only while a page stands in the tall frame.</summary>
        public PanelKind CurrentPanelKind => KindOf(activityLayout);

        /// <summary>Whether a page stands in the tall reading frame this render.</summary>
        public bool IsReadingFrame => modal != null && activityLayout == ActivityLayout.Reading;

        /// <summary>
        /// The floor every docked panel stands on: the top of the status line's band, at a text
        /// scale. The same sum the HUD's own floor is (<see cref="ModalLift"/>).
        /// </summary>
        public static float PanelFloor(float fontScale) =>
            (CastRail.Bottom + CastRail.Height * fontScale + 14f) + StatusHeight * fontScale + 8f;

        /// <summary>The reading frame on a canvas of this size at this text scale: the arithmetic the frame is laid out with.</summary>
        public static ReadingFrame.Box ReadingBox(float canvasWidth, float canvasHeight, float fontScale) =>
            ReadingFrame.Place(canvasWidth, canvasHeight, LeftColumnX, PanelFloor(fontScale), ActivityHeadroom, FullFrameEdge);

        /// <summary>Puts the open panel in the tall reading frame: the director's call for a READING page.</summary>
        public void UseReadingFrame() => SetActivityLayout(ActivityLayout.Reading);

        /// <summary>
        /// The frame itself: placed by <see cref="ReadingBox"/>, its ground near opaque (a page to
        /// read, Refinement Kit 6's 0.94 to 0.98 on the ground Image), the veil behind it, the chrome
        /// it does not keep stood down, and the arrival a fade with no slide.
        /// </summary>
        private void ReadingLayout(float canvasWidth, float canvasHeight)
        {
            var box = ReadingBox(canvasWidth, canvasHeight, FontScale);
            modal.anchorMin = modal.anchorMax = Vector2.zero;
            modal.pivot = Vector2.zero;
            modal.anchoredPosition = new Vector2(box.X, box.Y);
            modal.sizeDelta = new Vector2(box.Width, box.Height);
            var ground = modal.GetComponent<Image>();
            if (ground != null) { var c = ground.color; ground.color = new Color(c.r, c.g, c.b, Mathf.Max(c.a, .97f)); }
            HideStageChrome();
            var hint = modal.Find(PanelHintName);
            if (hint != null) hint.gameObject.SetActive(false);
            if (modalScroll != null)
            {
                Stretch((RectTransform)modalScroll.transform, 20f, 76f, 20f, 16f);
                modalScroll.verticalNormalizedPosition = 1f;
            }

            // The arrival, only on the render that opens the panel (Begin's reveal is there then):
            // a fade, never a rise, and nothing at all with reduced motion (HudReveal plays nothing).
            var reveal = modal.GetComponent<HudReveal>();
            bool arriving = reveal != null;
            if (arriving)
            {
                DestroyImmediate(reveal);
                HudReveal.Play(modal, ReducedMotion, 0f);
            }
            Veil();
        }

        /// <summary>
        /// The veil: the theme's background over the whole canvas, under every other piece of it,
        /// taking the pointer and offering no control - a click on it does nothing. It is up at once,
        /// never faded in: a veil still fading would let a click through to the house.
        /// </summary>
        private void Veil()
        {
            if (canvas == null) return;
            if (readingVeil != null && readingVeil.gameObject.activeSelf && readingVeil.parent == canvas.transform)
            {
                readingVeil.SetAsFirstSibling();
                return;
            }
            var veil = new GameObject(ReadingFrame.VeilName, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            veil.SetParent(canvas.transform, false);
            veil.SetAsFirstSibling();
            veil.anchorMin = Vector2.zero; veil.anchorMax = Vector2.one;
            veil.offsetMin = Vector2.zero; veil.offsetMax = Vector2.zero;
            var image = veil.GetComponent<Image>();
            var dark = UiTheme.Background;
            image.color = new Color(dark.r, dark.g, dark.b, ReadingFrame.VeilOpacity);
            image.raycastTarget = true;
            readingVeil = veil;
        }

        /// <summary>
        /// A READING page's head: its small label (the eyebrow), its title and the line under it, in
        /// the notebook's page style, with Close at the top right and the page's way back, when it
        /// has one, at the top left. Call it once, after <see cref="UseReadingFrame"/>.
        /// </summary>
        public void ReadingHeader(string label, string title, string subtitle) =>
            ScreenHeader(ReadingHeaderName, label, title, null, subtitle);

        /// <summary>
        /// A fixed band under the head, <paramref name="height"/> canvas units tall (scale it by
        /// <see cref="FontScale"/>), that does not scroll: the scroll starts under it. Build a tab
        /// strip into it. Call it after <see cref="ReadingHeader"/>; null with no panel up.
        /// </summary>
        public RectTransform ReadingBand(string name, float height)
        {
            if (modal == null || modalScroll == null || height <= 0f) return null;
            var scroll = (RectTransform)modalScroll.transform;
            float top = -scroll.offsetMax.y;
            var band = new GameObject(string.IsNullOrEmpty(name) ? ReadingBandName : name, typeof(RectTransform)).GetComponent<RectTransform>();
            band.SetParent(modal, false);
            band.anchorMin = new Vector2(0f, 1f); band.anchorMax = new Vector2(1f, 1f); band.pivot = new Vector2(.5f, 1f);
            band.offsetMin = new Vector2(20f, -(top + height));
            band.offsetMax = new Vector2(-20f, -top);
            scroll.offsetMax = new Vector2(scroll.offsetMax.x, -(top + height + 8f * FontScale));
            return band;
        }

        /// <summary>
        /// A READING page's main action, pinned in the frame's footer under the scroll, named and
        /// captioned by its caption. Only a page that has a main action has a footer.
        /// </summary>
        public Button ReadingFooter(string caption, Action action) => PinnedAction(caption, action);
    }
}
