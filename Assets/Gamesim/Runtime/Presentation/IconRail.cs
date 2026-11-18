using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gamesim.Presentation
{
    /// <summary>
    /// The labelled navigation down the LEFT edge (mockup-01, -03, -12): an icon and a word per
    /// row, each jumping straight to a section of the notebook or to the overview.
    ///
    /// <para>It was a strip of six unlabelled 46-unit marks drawn from discs and bars, because the
    /// shipped font could not set a glyph and nothing else could draw one. The generated icon set
    /// exists now, and the mockups' rail was never icon-only: every row carries its word, which is
    /// also what a player who does not yet know what a network mark means needs. The word IS the
    /// caption - the same string a test and a screen reader identify the row by - so labelling the
    /// rail cost no new identity; the caption simply became visible.</para>
    ///
    /// <para>The item you are looking at is FILLED and carries a bar on its leading edge,
    /// <see cref="ActiveMarkName"/>. The caption does NOT change with the state - in this HUD a
    /// control's caption is its identity, and appending to one has broken seven tests at once
    /// before. A fill alone is a colour, and a colour must never be the only carrier.</para>
    ///
    /// <para>The rows join the HUD's keyboard ring like any other chrome: in it while nothing is
    /// open, and out of it while a panel is, so they never stand between a panel and its own
    /// controls.</para>
    /// </summary>
    public static class IconRail
    {
        public const string RootName = "Icon rail";

        /// <summary>
        /// The rail's width, which does not grow with the larger-text preference: only the rows'
        /// height does. Everything left-anchored past the gutter measures from this, through
        /// <c>EpisodeHud.LeftColumnX</c>, and a width that moved with the text size would move
        /// every one of them with it.
        /// </summary>
        public const float Width = 200f;

        /// <summary>How far below the top edge the rail starts: under the 52-unit top bar and a gap.</summary>
        public const float Top = 80f;

        /// <summary>A row's height at the standard text size, the gap between rows, and the inset
        /// the rows keep from the rail's ends.</summary>
        public const float RowHeight = 40f;
        public const float Gap = 4f;
        public const float Pad = 8f;

        /// <summary>The words' size, in the medium weight: the mockups' 15.</summary>
        private const float LabelSize = 15f;

        /// <summary>What a mark stands for, and the drawn shape a clone without the icons gets.</summary>
        public enum Mark { Network, Rooms, Votes, Story, Overview, People, Notes, Deals }

        /// <summary>
        /// The marker the open item carries, as a child of the row.
        ///
        /// <para>Named rather than implied so a test can ask which item is lit without reading a
        /// colour, and drawn as a bar on the row's leading edge so a player is not asked to read
        /// one either.</para>
        /// </summary>
        public const string ActiveMarkName = "Open";

        /// <summary>One entry: its mark, the section it jumps to, and its caption.</summary>
        public readonly struct Entry
        {
            public readonly Mark Mark;
            public readonly string Section;
            public readonly string Caption;

            public Entry(Mark mark, string section, string caption)
            {
                Mark = mark; Section = section; Caption = caption;
            }
        }

        /// <summary>The rail's height for <paramref name="count"/> rows at a text scale.</summary>
        public static float Height(int count, float scale) =>
            count <= 0 ? 0f : (2f * Pad + count * RowHeight + (count - 1) * Gap) * scale;

        /// <summary>The rail's height for <paramref name="count"/> rows with a heading among them (<see cref="HeadingBand"/>).</summary>
        public static float Height(int count, float scale, bool heading) =>
            Height(count, scale) + (heading && count > 0 ? HeadingBand * scale : 0f);

        /// <summary>
        /// The band a group's heading takes in the rail, at the standard size: the words at the
        /// section heading's size (<see cref="HeadingSize"/>) in a box 1.3 times as tall and more,
        /// and the divider under them.
        /// </summary>
        public const float HeadingBand = 30f;
        /// <summary>The heading's type size: the section heading's, the smallest a label may be.</summary>
        public const float HeadingSize = 14f;
        /// <summary>A rail heading's object name and its divider's.</summary>
        public const string HeadingName = "Rail label", HeadingRuleName = "Rail label rule";

        /// <summary>
        /// A group's heading in the rail (menu slice 3, the review's rail line): the section heading
        /// style - small letterspaced capitals in the heading blue, with a divider under them - in a
        /// band <see cref="HeadingBand"/> tall at <paramref name="top"/>, down from the parent's top,
        /// <paramref name="left"/> in and <paramref name="width"/> wide. The words are kept as given:
        /// the capitals are the style's, so the text a test reads is the one passed in.
        /// </summary>
        public static TMP_Text Heading(RectTransform parent, string words, float left, float top, float width, float scale)
        {
            float box = Mathf.Ceil(HeadingSize * 1.3f + 4f) * scale;
            var label = HudPrimitives.Label(HeadingName, parent, Mathf.Round(HeadingSize * scale), UiTheme.Heading, TextAlignmentOptions.MidlineLeft);
            label.text = Localisation.Text(words);
            label.fontStyle |= FontStyles.UpperCase;
            label.characterSpacing = 4f;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.raycastTarget = false;
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            if (semibold != null) label.font = semibold;
            var rect = label.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(left, -(top + 2f * scale));
            rect.sizeDelta = new Vector2(width, box);
            var rule = HudPrimitives.Fill(HeadingRuleName, parent, new Color(UiTheme.Heading.r, UiTheme.Heading.g, UiTheme.Heading.b, .35f), 0);
            rule.anchorMin = rule.anchorMax = new Vector2(0f, 1f);
            rule.pivot = new Vector2(0f, 1f);
            rule.anchoredPosition = new Vector2(left, -(top + 2f * scale + box + 2f * scale));
            rule.sizeDelta = new Vector2(width, 1f);
            rule.GetComponent<Image>().raycastTarget = false;
            return label;
        }

        /// <summary>
        /// The rail's rows, with an optional heading in the band before row <paramref name="headingBefore"/>
        /// (the Notebook group's, under the overview): the rows from there down stand a heading's band lower.
        /// </summary>
        public static RectTransform Build(
            Transform parent, IList<Entry> entries, float scale, Action<string> jump,
            string active = null, int headingBefore = -1, string heading = null)
        {
            var root = new GameObject(RootName, typeof(RectTransform)).GetComponent<RectTransform>();
            root.SetParent(parent, false);
            // The left gutter, under the brand. It reads before everything else in the frame,
            // which is where navigation belongs and where every one of the mockups puts it.
            root.anchorMin = new Vector2(0f, 1f);
            root.anchorMax = new Vector2(0f, 1f);
            root.pivot = new Vector2(0f, 1f);
            root.anchoredPosition = new Vector2(14f, -Top);

            int count = entries?.Count ?? 0;
            bool headed = !string.IsNullOrEmpty(heading) && headingBefore >= 0 && headingBefore < count;
            root.sizeDelta = new Vector2(Width, Height(count, scale, headed));
            if (headed)
                Heading(root, heading, Pad + 8f, (Pad + headingBefore * (RowHeight + Gap)) * scale, Width - 2f * Pad - 8f, scale);
            for (int i = 0; i < count; i++)
            {
                var entry = entries[i];
                bool on = !string.IsNullOrEmpty(active) && entry.Section == active;
                string section = entry.Section;
                var mark = entry.Mark;
                Row(root, entry.Caption, UiTheme.Icon(IconOf(entry.Mark)),
                    (box, tint) => Draw(mark, box, scale * .8f, tint),
                    on, (Pad + i * (RowHeight + Gap) + (headed && i >= headingBefore ? HeadingBand : 0f)) * scale, scale,
                    jump == null ? (Action)null : () => jump(section));
            }
            return root;
        }

        /// <summary>The generated icon a mark is drawn with, when the icon set is there.</summary>
        private static string IconOf(Mark mark)
        {
            switch (mark)
            {
                case Mark.Overview: return "house";
                case Mark.People: return "people";
                case Mark.Network: return "heart";
                case Mark.Votes: return "gavel";
                case Mark.Story: return "calendar";
                case Mark.Notes: return "journal";
                case Mark.Deals: return "handshake";
                // No floor plan in the generated set, and the pack's grid is a stroke icon beside
                // filled ones; the drawn four-rooms mark is the closer match.
                default: return null;
            }
        }

        /// <summary>
        /// One row of the rail's kind: a glyph and a word on a ground that only shows when the
        /// row is hovered, pressed or open. Public so the rest of the left gutter - the travel
        /// buttons and the notebook, save and settings row - is drawn as the same list rather
        /// than as a second style of button beside it.
        /// </summary>
        /// <param name="fallback">Draws a mark into the glyph's box when <paramref name="icon"/> is null.</param>
        /// <param name="top">The row's top edge, down from the parent's top.</param>
        public static Button Row(RectTransform parent, string caption, Sprite icon,
            Action<RectTransform, Color> fallback, bool on, float top, float scale, Action onClick,
            bool primary = false)
        {
            var row = HudPrimitives.Fill(caption, parent, Color.white, 8);
            row.anchorMin = new Vector2(0f, 1f); row.anchorMax = new Vector2(1f, 1f);
            row.pivot = new Vector2(.5f, 1f);
            row.offsetMin = new Vector2(Pad, -top - RowHeight * scale);
            row.offsetMax = new Vector2(-Pad, -top);
            var ground = row.GetComponent<Image>();
            ground.raycastTarget = true;

            var tint = on ? UiTheme.Paper : UiTheme.Accent;
            float side = 20f * scale;
            var box = new GameObject("Row mark", typeof(RectTransform)).GetComponent<RectTransform>();
            box.SetParent(row, false);
            box.anchorMin = box.anchorMax = new Vector2(0f, .5f);
            box.pivot = new Vector2(0f, .5f);
            box.anchoredPosition = new Vector2(12f, 0f);
            box.sizeDelta = new Vector2(side, side);
            if (icon != null)
            {
                var image = box.gameObject.AddComponent<Image>();
                image.sprite = icon; image.color = tint; image.preserveAspect = true; image.raycastTarget = false;
            }
            else fallback?.Invoke(box, tint);

            var label = HudPrimitives.Label("Row label", row, Mathf.Round(LabelSize * scale),
                on ? UiTheme.Paper : new Color(UiTheme.Paper.r, UiTheme.Paper.g, UiTheme.Paper.b, .88f),
                TextAlignmentOptions.MidlineLeft);
            var medium = UiTheme.Font(on || primary ? UiTheme.Weight.SemiBold : UiTheme.Weight.Medium);
            if (medium != null) label.font = medium;
            label.text = Localisation.Text(caption);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.enableAutoSizing = true;
            label.fontSizeMax = label.fontSize;
            label.fontSizeMin = Mathf.Min(12f, label.fontSize);
            var words = label.rectTransform;
            words.anchorMin = Vector2.zero; words.anchorMax = Vector2.one;
            words.offsetMin = new Vector2(12f + side + 10f, 2f);
            words.offsetMax = new Vector2(-8f, -2f);

            if (on)
            {
                // Inside the row, on its leading edge: outside it would hang off the canvas margin.
                var bar = HudPrimitives.Fill(ActiveMarkName, row, UiTheme.Accent, 2);
                bar.anchorMin = bar.anchorMax = new Vector2(0f, .5f);
                bar.pivot = new Vector2(0f, .5f);
                bar.anchoredPosition = Vector2.zero;
                bar.sizeDelta = new Vector2(3f, RowHeight * scale - 12f);
                bar.GetComponent<Image>().raycastTarget = false;
            }

            var button = row.gameObject.AddComponent<Button>();
            button.targetGraphic = ground;
            // The ground is white and every state's colour is the colour drawn, so a row at rest is
            // bare glass and the one you are on is a lifted card - the mockups' rail, which never
            // boxes the rows it is not on.
            var resting = on ? new Color(UiTheme.SurfaceRaised.r, UiTheme.SurfaceRaised.g, UiTheme.SurfaceRaised.b, .95f)
                : primary ? new Color(UiTheme.AccentDeep.r, UiTheme.AccentDeep.g, UiTheme.AccentDeep.b, .30f)
                : new Color(UiTheme.SurfaceRaised.r, UiTheme.SurfaceRaised.g, UiTheme.SurfaceRaised.b, 0f);
            var colours = button.colors;
            colours.normalColor = resting;
            colours.highlightedColor = new Color(.20f, .33f, .48f, .75f);
            colours.selectedColor = colours.highlightedColor;
            colours.pressedColor = new Color(UiTheme.AccentDeep.r, UiTheme.AccentDeep.g, UiTheme.AccentDeep.b, .45f);
            colours.disabledColor = new Color(resting.r, resting.g, resting.b, resting.a * .5f);
            colours.colorMultiplier = 1f;
            colours.fadeDuration = .08f;
            button.colors = colours;
            ground.color = Color.white;
            // Land on the resting colour now rather than fading to it: a white ground mid-fade is a
            // lit row, and a row built during a HUD rebuild was drawn lit for its first frames.
            ground.canvasRenderer.SetColor(resting);
            // Navigation is left to the HUD's keyboard ring, which takes every control outside an
            // open panel out of it and wires the rest - a row that opted itself out here was out
            // of the ring on the frames before the ring was rewired, and a keyboard player who
            // pressed Settings then was pressing nothing.
            if (onClick != null) button.onClick.AddListener(() => onClick());
            return button;
        }

        /// <summary>Draws a mark inside its box from discs and bars.</summary>
        private static void Draw(Mark mark, RectTransform cell, float scale, Color ink)
        {
            // The spine of the book is drawn in the ground rather than the ink.
            var ground = UiTheme.Ink;
            switch (mark)
            {
                case Mark.Network:
                    Bar(cell, ink, new Vector2(0f, 3f), new Vector2(18f, 2f), 52f, scale);
                    Bar(cell, ink, new Vector2(0f, 3f), new Vector2(18f, 2f), -52f, scale);
                    Dot(cell, ink, new Vector2(0f, 9f), 8f, scale);
                    Dot(cell, ink, new Vector2(-9f, -6f), 8f, scale);
                    Dot(cell, ink, new Vector2(9f, -6f), 8f, scale);
                    break;

                case Mark.Rooms:
                    // A floor plan: four rooms.
                    Square(cell, ink, new Vector2(-6f, 6f), 9f, scale);
                    Square(cell, ink, new Vector2(6f, 6f), 9f, scale);
                    Square(cell, ink, new Vector2(-6f, -6f), 9f, scale);
                    Square(cell, ink, new Vector2(6f, -6f), 9f, scale);
                    break;

                case Mark.Votes:
                    Bar(cell, ink, new Vector2(-8f, -2f), new Vector2(6f, 12f), 0f, scale);
                    Bar(cell, ink, new Vector2(0f, 1f), new Vector2(6f, 18f), 0f, scale);
                    Bar(cell, ink, new Vector2(8f, -4f), new Vector2(6f, 8f), 0f, scale);
                    break;

                case Mark.Story:
                    Bar(cell, ink, new Vector2(-6f, 0f), new Vector2(10f, 15f), 0f, scale);
                    Bar(cell, ink, new Vector2(6f, 0f), new Vector2(10f, 15f), 0f, scale);
                    Bar(cell, ground, new Vector2(0f, 0f), new Vector2(2f, 17f), 0f, scale);
                    break;

                case Mark.Overview:
                    Bar(cell, ink, new Vector2(0f, 9f), new Vector2(22f, 2f), 0f, scale);
                    Bar(cell, ink, new Vector2(0f, -9f), new Vector2(22f, 2f), 0f, scale);
                    Bar(cell, ink, new Vector2(-10f, 0f), new Vector2(2f, 16f), 0f, scale);
                    Bar(cell, ink, new Vector2(10f, 0f), new Vector2(2f, 16f), 0f, scale);
                    Square(cell, ink, new Vector2(0f, 0f), 6f, scale);
                    break;

                case Mark.People:
                    Dot(cell, ink, new Vector2(-7f, 5f), 10f, scale);
                    Dot(cell, ink, new Vector2(7f, 5f), 10f, scale);
                    Bar(cell, ink, new Vector2(-7f, -7f), new Vector2(16f, 9f), 0f, scale);
                    Bar(cell, ink, new Vector2(7f, -7f), new Vector2(16f, 9f), 0f, scale);
                    break;
            }
        }

        private static void Dot(RectTransform cell, Color colour, Vector2 offset, float size, float scale)
        {
            var dot = HudPrimitives.Disc("Mark", cell, colour);
            Centre(dot, offset, new Vector2(size, size), scale);
        }

        private static void Square(RectTransform cell, Color colour, Vector2 offset, float size, float scale)
        {
            var box = HudPrimitives.Fill("Mark", cell, colour, 2);
            Centre(box, offset, new Vector2(size, size), scale);
        }

        private static void Bar(RectTransform cell, Color colour, Vector2 offset, Vector2 size, float angle, float scale)
        {
            var bar = HudPrimitives.Fill("Mark", cell, colour, 2);
            Centre(bar, offset, size, scale);
            if (Mathf.Abs(angle) > 0.01f) bar.localRotation = Quaternion.Euler(0f, 0f, angle);
        }

        private static void Centre(RectTransform rect, Vector2 offset, Vector2 size, float scale)
        {
            rect.anchorMin = new Vector2(.5f, .5f);
            rect.anchorMax = new Vector2(.5f, .5f);
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = offset * scale;
            rect.sizeDelta = size * scale;
            // Nothing drawn in a mark takes a click meant for the row it is in.
            var graphic = rect.GetComponent<Graphic>();
            if (graphic != null) graphic.raycastTarget = false;
        }
    }
}
