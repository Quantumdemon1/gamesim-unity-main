using System;
using Gamesim.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gamesim.Episode
{
    /// <summary>
    /// The episode screen as a decision screen rather than a strip with a scroll in it.
    ///
    /// <para>The docked panel is 900x300, which leaves 174 units to read in. A resolved competition
    /// printed its standings there with "Continue to the next ceremony" under them, some 320 units
    /// down; free time put "Begin the next competition" under the meter, the house-wide actions and
    /// Listen in. The player scrolled to find the way on. Three things fix that without giving up
    /// the docked design: the panel grows with what it holds, up to the free area above the status
    /// band; the step that moves the week on is pinned under the scroll, where it is always seen;
    /// and a choice between people is laid out as peers, two to a row, rather than a stack.</para>
    /// </summary>
    public sealed partial class EpisodeHud
    {
        /// <summary>The hint over the Standard panel, and its two readings.</summary>
        public const string PanelHintName = "Panel control hint";
        public const string PanelHintCopy = "Tab / Up / Down select · Enter confirm";
        public const string PanelHintScrollCopy = PanelHintCopy + " · Scroll for more";
        /// <summary>Peer choices, two to a row.</summary>
        public const string ChoiceRowName = "Choice row";

        private const float PinnedHeight = 57f;
        private const float PinnedMargin = 16f;
        private const float FittedFloor = 200f;

        /// <summary>Whether the Standard panel takes its height from its content this render.</summary>
        private bool fitToContent;
        /// <summary>The render's one pinned action, or null.</summary>
        private RectTransform pinnedAction;
        /// <summary>The widest the column of a wide screen runs this render; zero when uncapped.</summary>
        private float contentCap;

        /// <summary>The width the panel's rows are built to this render, in canvas units, for a test.</summary>
        public float ColumnWidth => ContentWidth();

        /// <summary>
        /// Lets the Standard panel take its height from what it holds, from a short card up to the
        /// free area above the status band. Measured after the rows have grown to their wrapped
        /// labels, so nothing is cut. A dedicated layout sizes itself and turns this off.
        ///
        /// <para>Called before the content is built, because the width is decided here and rows
        /// measure the column as they are built. A panel that can grow to the top bar puts away
        /// what the activity layouts put away - the follow chip over the frame's top centre, and
        /// the compact HUD's objective card - and keeps to the band between the rail and the right
        /// column, which on a squarer screen is narrower than the docked 900.</para>
        /// </summary>
        public void FitPanelToContent()
        {
            fitToContent = true;
            if (modal == null || canvas == null) return;
            SetChromeVisible(FollowChipName, false);
            if (Compact) SetChromeVisible("Objective", false);
            var bounds = ((RectTransform)canvas.transform).rect;
            float canvasWidth = bounds.width > 0 ? bounds.width : 1600f;
            float half = Mathf.Min(canvasWidth * .5f - LeftColumnX - 12f,
                canvasWidth * .5f - RightColumnInset - RightColumnWidth - 12f);
            float width = Mathf.Min(ModalWidth, Mathf.Max(420f, 2f * half));
            modal.sizeDelta = new Vector2(width, modal.sizeDelta.y);
        }

        /// <summary>
        /// The step that moves the week on, pinned under the scroll: the same row an action is -
        /// named and captioned by its caption, so every lookup by caption or by name still finds it -
        /// but outside the scrolling column, so it is on screen however long the panel above it is.
        /// One a render; a second is drawn as an ordinary row rather than stacked on the first.
        /// </summary>
        public Button PinnedAction(string caption, Action action)
        {
            if (modal == null || modalScroll == null || pinnedAction != null) return Action(caption, action);
            float s = FontScale;
            var rect = Chrome(caption, modal, UiTheme.Emphasis.Interactive);
            HudEmphasis.Promote(rect, UiTheme.Emphasis.Interactive);
            rect.anchorMin = new Vector2(0f, 0f); rect.anchorMax = new Vector2(1f, 0f); rect.pivot = new Vector2(.5f, 0f);
            // Under the column it moves on from: on a capped stage, as wide as the column.
            float side = 20f + (contentCap > 0f ? Mathf.Max(0f, (modal.sizeDelta.x - 40f - 18f - 16f - contentCap) * .5f) : 0f);
            rect.offsetMin = new Vector2(side, PinnedMargin);
            rect.offsetMax = new Vector2(-side, PinnedMargin + PinnedHeight * s);
            float mark = 18f * s;
            var button = FinishButton(rect, caption, action, 16f, 16f + mark + 10f);
            HudPrimitives.Chevron(rect, UiTheme.Hairline, mark).anchoredPosition = new Vector2(-16f, 0f);
            pinnedAction = rect;
            ApplyPinnedInset();
            return button;
        }

        /// <summary>Keeps the scroll's foot clear of the pinned action, whatever layout re-stretched it.</summary>
        private void ApplyPinnedInset()
        {
            if (pinnedAction == null || modalScroll == null) return;
            var scroll = (RectTransform)modalScroll.transform;
            scroll.offsetMin = new Vector2(scroll.offsetMin.x, PinnedMargin + PinnedHeight * FontScale + 10f);
        }

        /// <summary>The pinned action, if the render has one and it can take the focus.</summary>
        private Selectable PinnedSelectable(Selectable[] eligible) =>
            pinnedAction == null ? null : Array.Find(eligible, item => item.transform == pinnedAction);

        /// <summary>
        /// Sizes a fitted panel to its content, then says whether it scrolls: the hint's "Scroll for
        /// more" is only true of a panel that does. Runs in the selection pass, after the rows have
        /// grown and before the opening control is revealed.
        /// </summary>
        private void FitStandardPanel()
        {
            if (modal == null || content == null || modalScroll == null || modalScroll.viewport == null) return;
            if (fitToContent && activityLayout == ActivityLayout.Standard)
            {
                var root = (RectTransform)canvas.transform;
                var bounds = root.rect;
                float canvasHeight = bounds.height > 0 ? bounds.height : 900f;
                // Up to the top bar, and short of any chrome still standing above the panel's
                // column - never lower than the docked panel always stood.
                float ceiling = canvasHeight - ActivityHeadroom;
                var column = CanvasRect(root, modal);
                foreach (Transform child in canvas.transform)
                {
                    if (child == modal || !child.gameObject.activeInHierarchy || !(child is RectTransform piece)) continue;
                    var area = CanvasRect(root, piece);
                    if (area.width <= 0f || area.height <= 0f || area.yMin < ModalLift + 1f) continue;
                    if (area.xMax <= column.xMin || area.xMin >= column.xMax) continue;
                    ceiling = Mathf.Min(ceiling, area.yMin - 8f);
                }
                float most = Mathf.Max(ModalHeight, ceiling - ModalLift);
                float chrome = modal.rect.height - modalScroll.viewport.rect.height;
                float height = Mathf.Clamp(content.rect.height + chrome, FittedFloor, most);
                if (Mathf.Abs(height - modal.sizeDelta.y) > .5f)
                {
                    modal.sizeDelta = new Vector2(modal.sizeDelta.x, height);
                    Canvas.ForceUpdateCanvases();
                }
            }
            var hint = modal.Find(PanelHintName);
            if (hint != null && hint.gameObject.activeSelf && hint.GetComponent<TMP_Text>() is TMP_Text words)
                words.text = Localisation.Text(content.rect.height > modalScroll.viewport.rect.height + .5f ? PanelHintScrollCopy : PanelHintCopy);
        }

        /// <summary>A rect in the canvas's own units, from its bottom-left corner.</summary>
        private static Rect CanvasRect(RectTransform root, RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            var low = root.InverseTransformPoint(corners[0]);
            var high = root.InverseTransformPoint(corners[2]);
            var bounds = root.rect;
            return Rect.MinMaxRect(low.x - bounds.xMin, low.y - bounds.yMin, high.x - bounds.xMin, high.y - bounds.yMin);
        }

        /// <summary>A run of peer choices laid two to a row; see <see cref="PairedAction"/>.</summary>
        public sealed class ChoicePairs
        {
            internal RectTransform Row;
        }

        /// <summary>Starts a run of peer choices.</summary>
        public ChoicePairs Pairs() => new ChoicePairs();

        /// <summary>
        /// One of a run of peer choices: the same row an action is, laid beside its peer. Two to a row
        /// on the resting text size; one at the larger, where half the column is too narrow for a
        /// name and the player's trust reading. A row grows to the taller of its two captions, and
        /// the last choice of an odd run takes the row's whole width.
        /// </summary>
        public Button PairedAction(ChoicePairs pairs, string caption, Action action) =>
            Paired(ActionIn(PairRow(pairs), caption, action));

        /// <summary>A peer choice about a houseguest, fronted by their face and the player's trust reading.</summary>
        public Button PairedActionFor(ChoicePairs pairs, string contestantId, string caption, Action action)
        {
            var button = Paired(ActionIn(PairRow(pairs), caption, Portrait(contestantId), action));
            Annotate(button, contestantId);
            return button;
        }

        /// <summary>Peers share their row equally, whatever their captions.</summary>
        private static Button Paired(Button button)
        {
            var element = button.GetComponent<LayoutElement>();
            if (element != null && button.transform.parent != null && button.transform.parent.name == ChoiceRowName)
            { element.preferredWidth = 0f; element.flexibleWidth = 1f; }
            return button;
        }

        private RectTransform PairRow(ChoicePairs pairs)
        {
            int columns = FontScale > 1.05f || ContentWidth() < 720f ? 1 : 2;
            if (pairs == null || columns == 1) return content;
            if (pairs.Row != null && pairs.Row.childCount < columns) return pairs.Row;
            var row = new GameObject(ChoiceRowName, typeof(RectTransform), typeof(HorizontalLayoutGroup)).GetComponent<RectTransform>();
            row.SetParent(content, false);
            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 10f * FontScale;
            layout.childControlWidth = true; layout.childControlHeight = true;
            layout.childForceExpandWidth = true; layout.childForceExpandHeight = true;
            pairs.Row = row;
            return row;
        }
    }
}
