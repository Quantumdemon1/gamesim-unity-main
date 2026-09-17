using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Gamesim.Presentation
{
    /// <summary>
    /// The vertical strip of marks down the right edge, each jumping straight to a section of the
    /// notebook.
    ///
    /// <para>Deliberately not a second copy of the navigation panel. A rail that repeated Notebook,
    /// Save and Settings in smaller form would add a way to do things the player can already do and
    /// nothing else, which is a usability loss dressed as a feature. What the reference build's rail
    /// actually does is switch between views, so this does that: the relationship graph, the room
    /// map, the vote breakdown and the story are four separate questions that all currently live in
    /// one long scroll, and the rail is how you get to the one you want.</para>
    ///
    /// <para>The marks are drawn from discs and rounded rectangles rather than set in a font. The
    /// shipped atlas is LiberationSans SDF, which has no dingbats — the same constraint that made
    /// the ceremony mark a pair of concentric circles. A glyph that is not in the atlas renders as
    /// tofu, and tofu in permanent chrome is worse than a shape that is merely abstract.</para>
    /// </summary>
    public static class IconRail
    {
        public const string RootName = "Icon rail";
        public const float Width = 52f;

        private const float ButtonSize = 46f;
        private const float Gap = 8f;

        /// <summary>What a mark stands for. Each is buildable from the two primitive shapes.</summary>
        public enum Mark { Network, Rooms, Votes, Story }

        /// <summary>One entry: its mark, the section it jumps to, and its accessible name.</summary>
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

        public static RectTransform Build(
            Transform parent, IList<Entry> entries, float scale, Action<string> jump)
        {
            var root = new GameObject(RootName, typeof(RectTransform)).GetComponent<RectTransform>();
            root.SetParent(parent, false);
            root.anchorMin = new Vector2(1f, 1f);
            root.anchorMax = new Vector2(1f, 1f);
            root.pivot = new Vector2(1f, 1f);
            root.anchoredPosition = new Vector2(-24f, -104f);

            int count = entries?.Count ?? 0;
            root.sizeDelta = new Vector2(Width * scale, (count * ButtonSize + (count - 1) * Gap + 12f) * scale);
            if (count == 0) return root;

            for (int i = 0; i < count; i++)
            {
                var entry = entries[i];
                var cell = HudPrimitives.Fill(entry.Caption, root, UiTheme.Ink, UiTheme.ControlRadius);
                cell.anchorMin = new Vector2(.5f, 1f); cell.anchorMax = new Vector2(.5f, 1f);
                cell.pivot = new Vector2(.5f, 1f);
                cell.anchoredPosition = new Vector2(0f, -(6f + i * (ButtonSize + Gap)) * scale);
                cell.sizeDelta = new Vector2(ButtonSize * scale, ButtonSize * scale);
                cell.GetComponent<Image>().raycastTarget = true;
                UiTheme.AddBorder(cell, UiTheme.ControlRadius, UiTheme.Outline);

                Draw(entry.Mark, cell, scale);

                var button = cell.gameObject.AddComponent<Button>();
                button.targetGraphic = cell.GetComponent<Image>();
                // The rail is a shortcut, not a focus target: keyboard players reach the same
                // sections by opening the notebook and scrolling, and putting four more stops in the
                // tab order ahead of the panel's own controls would cost them more than it saves.
                var navigation = button.navigation;
                navigation.mode = Navigation.Mode.None;
                button.navigation = navigation;

                string section = entry.Section;
                if (jump != null) button.onClick.AddListener(() => jump(section));
            }
            return root;
        }

        /// <summary>Draws a mark inside its cell from discs and bars.</summary>
        private static void Draw(Mark mark, RectTransform cell, float scale)
        {
            var ink = UiTheme.Accent;
            switch (mark)
            {
                case Mark.Network:
                    // Three nodes and the links between them.
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
                    // A tally, taller on one side, which is what a vote looks like.
                    Bar(cell, ink, new Vector2(-8f, -2f), new Vector2(6f, 12f), 0f, scale);
                    Bar(cell, ink, new Vector2(0f, 1f), new Vector2(6f, 18f), 0f, scale);
                    Bar(cell, ink, new Vector2(8f, -4f), new Vector2(6f, 8f), 0f, scale);
                    break;

                case Mark.Story:
                    // An open book: two leaves either side of a spine.
                    Bar(cell, ink, new Vector2(-6f, 0f), new Vector2(10f, 15f), 0f, scale);
                    Bar(cell, ink, new Vector2(6f, 0f), new Vector2(10f, 15f), 0f, scale);
                    Bar(cell, UiTheme.Ink, new Vector2(0f, 0f), new Vector2(2f, 17f), 0f, scale);
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
        }
    }
}
