using System;
using System.Collections.Generic;
using Gamesim.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gamesim.Episode
{
    /// <summary>
    /// The notebook's page furniture from Refinement Kit 6: a filter row under the page's head, a
    /// footer with the page's one secondary action, and a designed empty state. A notebook page is
    /// "a readable information workspace with stable heading, filters, content area and footer" -
    /// the kit's words - rather than the same scrolling window every page shared.
    /// </summary>
    public sealed partial class EpisodeHud
    {
        /// <summary>The notebook's eyebrow, over every page's title.</summary>
        public const string NotebookEyebrowCopy = "YOUR NOTEBOOK · WHAT YOUR CHARACTER KNOWS";

        /// <summary>
        /// A row of filter pills along the top of a page, with an optional summary at its right end.
        /// The pills are the page's filter buttons - the active one in the action blue, the rest
        /// quiet glass - each captioned by the words on it. Choosing one is view state: it commits
        /// nothing and saves nothing.
        /// </summary>
        public RectTransform FilterRow(string name, IList<(string Caption, bool Active, Action Choose)> tabs, string summary = null)
        {
            if (content == null) return null;
            float s = FontScale, height = 36f * s, width = ContentWidth();
            var row = new GameObject(name, typeof(RectTransform), typeof(LayoutElement)).GetComponent<RectTransform>();
            row.SetParent(content, false);
            // The pills wrap onto a second line rather than run past a narrow column's edge.
            float x = 0f, y = 2f * s;
            foreach (var tab in tabs)
            {
                var pill = FilterPill(row, tab.Caption, tab.Active, tab.Choose, height);
                var rect = (RectTransform)pill.transform;
                if (x > 0f && x + rect.sizeDelta.x > width) { x = 0f; y += height + 8f * s; }
                Anchor(rect, new Vector2(0, 1), new Vector2(0, 1), new Vector2(x, -y), rect.sizeDelta);
                x += rect.sizeDelta.x + 10f * s;
            }
            var size = row.GetComponent<LayoutElement>();
            size.minHeight = size.preferredHeight = y + height + 4f * s;
            if (!string.IsNullOrEmpty(summary))
            {
                float room = Mathf.Max(80f, width - x - 12f * s);
                var line = FixedText(row, summary, 15, UiTheme.Muted, new Vector2(x + 12f * s, -(y + 7f * s)), new Vector2(room, 22f * s));
                line.alignment = TextAlignmentOptions.Right;
                AutoSize(line, 11);
            }
            return row;
        }

        /// <summary>One filter pill: Kit 6's capsule, sized to its words.</summary>
        private Button FilterPill(RectTransform parent, string caption, bool active, Action choose, float height)
        {
            float s = FontScale;
            var rect = Panel(caption, parent, active ? UiTheme.ActionBlue : UiTheme.SurfaceRaised, Mathf.RoundToInt(height * .5f) - 1);
            UiTheme.PackSliced(rect.GetComponent<Image>(), PackArt.KitPillFill, height * .5f, active ? UiTheme.ActionBlue : UiTheme.SurfaceRaised);
            if (!active)
            {
                var edge = new GameObject("Border", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                edge.rectTransform.SetParent(rect, false);
                edge.rectTransform.anchorMin = Vector2.zero; edge.rectTransform.anchorMax = Vector2.one;
                edge.rectTransform.offsetMin = Vector2.zero; edge.rectTransform.offsetMax = Vector2.zero;
                edge.raycastTarget = false;
                if (!UiTheme.PackSliced(edge, PackArt.KitPillEdge, height * .5f, UiTheme.Edge(UiTheme.Emphasis.Interactive)))
                    edge.color = new Color(0f, 0f, 0f, 0f);
            }
            var label = NewText(rect, caption, 15, active ? Color.white : Paper);
            var weight = UiTheme.Font(active ? UiTheme.Weight.SemiBold : UiTheme.Weight.Medium);
            if (weight != null) label.font = weight;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            float width = Mathf.Ceil(label.GetPreferredValues(label.text).x) + 40f * s;
            rect.sizeDelta = new Vector2(Mathf.Max(96f * s, width), height);
            Stretch(label.rectTransform, 12f, 2f, 12f, 2f);
            return Pressable(rect, choose);
        }

        /// <summary>
        /// A page's foot: a quiet rule, a line of what the page is (with the kit's info mark), and the
        /// page's one secondary action as a quiet button sized to its words. The action keeps its
        /// caption and its callback; only where it stands changes - it used to head every page.
        /// </summary>
        public Button NotebookFooter(string note, string caption, Action action)
        {
            if (content == null) return null;
            float s = FontScale;
            var foot = new GameObject("Notebook footer", typeof(RectTransform), typeof(LayoutElement)).GetComponent<RectTransform>();
            foot.SetParent(content, false);
            float width = ContentWidth();
            float y = 10f * s;
            var rule = HudPrimitives.Fill("Footer rule", foot, UiTheme.Edge(UiTheme.Emphasis.Interactive), 1);
            Anchor(rule, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0f, -y), new Vector2(width, 1f));
            y += 14f * s;
            if (!string.IsNullOrEmpty(note))
            {
                float x = 0f;
                var info = UiTheme.Pack(PackArt.KitIconInfo);
                if (info != null)
                {
                    var mark = new GameObject("Footer mark", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                    mark.rectTransform.SetParent(foot, false);
                    Anchor(mark.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0f, -y - 1f * s), new Vector2(20f * s, 20f * s));
                    mark.sprite = info; mark.color = UiTheme.Muted; mark.preserveAspect = true; mark.raycastTarget = false;
                    x = 30f * s;
                }
                var line = NewText(foot, note, 15, UiTheme.Muted);
                float lineHeight = Mathf.Ceil(line.GetPreferredValues(line.text, width - x, 0f).y) + 4f;
                Anchor(line.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(x, -y), new Vector2(width - x, lineHeight));
                y += lineHeight + 12f * s;
            }
            Button button = null;
            if (!string.IsNullOrEmpty(caption) && action != null)
            {
                var rect = KitButton(foot, caption, PackArt.KitIconPeople, action, out button);
                Anchor(rect, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0f, -y), rect.sizeDelta);
                y += rect.sizeDelta.y;
            }
            var size = foot.GetComponent<LayoutElement>();
            size.minHeight = size.preferredHeight = y + 6f * s;
            return button;
        }

        /// <summary>
        /// Kit 6's quiet secondary button: the button fill, a resting edge, a glyph and the caption,
        /// sized to its words. The caller places it; the caption is the control's name and key.
        /// </summary>
        private RectTransform KitButton(RectTransform parent, string caption, string icon, Action action, out Button button)
        {
            float s = FontScale, h = 44f * s;
            var rect = Panel(caption, parent, UiTheme.SurfaceRaised, UiTheme.ControlRadius);
            UiTheme.PackSliced(rect.GetComponent<Image>(), PackArt.KitButtonFill, 10f, UiTheme.SurfaceRaised);
            var edge = new GameObject("Border", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            edge.rectTransform.SetParent(rect, false);
            edge.rectTransform.anchorMin = Vector2.zero; edge.rectTransform.anchorMax = Vector2.one;
            edge.rectTransform.offsetMin = Vector2.zero; edge.rectTransform.offsetMax = Vector2.zero;
            edge.raycastTarget = false;
            if (!UiTheme.PackSliced(edge, PackArt.KitButtonEdge, 10f, UiTheme.Edge(UiTheme.Emphasis.Interactive)))
                edge.color = new Color(0f, 0f, 0f, 0f);
            float x = 16f * s;
            var glyph = string.IsNullOrEmpty(icon) ? null : UiTheme.Pack(icon);
            if (glyph != null)
            {
                var mark = new GameObject("Button mark", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                mark.rectTransform.SetParent(rect, false);
                Anchor(mark.rectTransform, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(x, 0f), new Vector2(20f * s, 20f * s));
                mark.sprite = glyph; mark.color = Paper; mark.preserveAspect = true; mark.raycastTarget = false;
                x += 30f * s;
            }
            var label = NewText(rect, caption, 16, Paper);
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            if (semibold != null) label.font = semibold;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            float words = Mathf.Ceil(label.GetPreferredValues(label.text).x);
            rect.sizeDelta = new Vector2(x + words + 22f * s, h);
            Stretch(label.rectTransform, x, 2f, 10f, 2f);
            HudEmphasis.Promote(rect, UiTheme.Emphasis.Interactive);
            button = Pressable(rect, action);
            return rect;
        }

        /// <summary>A kit pill in place: the pill fill and its edge, the words centred, sized to them.</summary>
        private RectTransform KitPill(RectTransform parent, string name, string words, Color fill, Color ink, int size, float height)
        {
            float s = FontScale;
            var rect = Panel(name, parent, fill, Mathf.RoundToInt(height * .5f) - 1);
            var image = rect.GetComponent<Image>();
            image.raycastTarget = false;
            UiTheme.PackSliced(image, PackArt.KitPillFill, height * .5f, fill);
            var label = NewText(rect, words, size, ink);
            var medium = UiTheme.Font(UiTheme.Weight.Medium);
            if (medium != null) label.font = medium;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            float width = Mathf.Ceil(label.GetPreferredValues(label.text).x) + 28f * s;
            rect.sizeDelta = new Vector2(width, height);
            Stretch(label.rectTransform, 10f * s, 1f, 10f * s, 1f);
            return rect;
        }

        /// <summary>
        /// One wrapped line of copy placed at <paramref name="y"/> from the top of its parent, as tall
        /// as its words need at the width given. Returns the y under it.
        /// </summary>
        private float PlacedCopy(RectTransform parent, string words, int size, UiTheme.Weight weight, Color ink, float x, float y, float width)
        {
            var text = NewText(parent, words, size, ink);
            var face = UiTheme.Font(weight);
            if (face != null) text.font = face;
            float height = Mathf.Ceil(text.GetPreferredValues(text.text, width, 0f).y) + 4f;
            Anchor(text.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(x, -y), new Vector2(width, height));
            return y + height;
        }

        /// <summary>A Kit 6 glyph, tinted, at a point in its parent.</summary>
        private Image KitGlyph(RectTransform parent, string icon, Color tint, Vector2 anchor, Vector2 at, float side)
        {
            var sprite = UiTheme.Pack(icon);
            if (sprite == null) return null;
            var mark = new GameObject("Glyph", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            mark.rectTransform.SetParent(parent, false);
            Anchor(mark.rectTransform, anchor, anchor, at, new Vector2(side, side));
            mark.sprite = sprite; mark.color = tint; mark.preserveAspect = true; mark.raycastTarget = false;
            return mark;
        }

        /// <summary>
        /// A designed empty state: one of Kit 6's text-free illustrations, a title and a line under it,
        /// centred. The words are the caller's, and must be true of the state they describe - an empty
        /// list is not by itself evidence that nothing happened.
        /// </summary>
        public RectTransform EmptyState(string name, string illustration, string title, string body)
        {
            if (content == null) return null;
            float s = FontScale, width = ContentWidth();
            var root = new GameObject(name, typeof(RectTransform), typeof(LayoutElement)).GetComponent<RectTransform>();
            root.SetParent(content, false);
            float y = 18f * s;
            var art = UiTheme.Pack(illustration);
            if (art != null)
            {
                var image = new GameObject("Empty illustration", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                image.rectTransform.SetParent(root, false);
                float h = 150f * s;
                Anchor(image.rectTransform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0f, -y), new Vector2(h * 640f / 440f, h));
                image.sprite = art; image.color = new Color(UiTheme.Muted.r, UiTheme.Muted.g, UiTheme.Muted.b, .75f);
                image.preserveAspect = true; image.raycastTarget = false;
                y += h + 16f * s;
            }
            float measure = Mathf.Min(width, 640f * s);
            var heading = NewText(root, title, 22, Paper);
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            if (semibold != null) heading.font = semibold;
            heading.alignment = TextAlignmentOptions.Center;
            float headingHeight = Mathf.Ceil(heading.GetPreferredValues(heading.text, measure, 0f).y) + 4f;
            Anchor(heading.rectTransform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0f, -y), new Vector2(measure, headingHeight));
            y += headingHeight + 8f * s;
            if (!string.IsNullOrEmpty(body))
            {
                var line = NewText(root, body, 16, UiTheme.Muted);
                line.alignment = TextAlignmentOptions.Top;
                float lineHeight = Mathf.Ceil(line.GetPreferredValues(line.text, measure, 0f).y) + 4f;
                Anchor(line.rectTransform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0f, -y), new Vector2(measure, lineHeight));
                y += lineHeight;
            }
            var size = root.GetComponent<LayoutElement>();
            size.minHeight = size.preferredHeight = y + 18f * s;
            return root;
        }
    }
}
