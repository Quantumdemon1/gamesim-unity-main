using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Gamesim.Presentation
{
    /// <summary>
    /// The vertical strip of marks down the LEFT edge, each jumping straight to a section of the
    /// notebook. This is the gutter the cast strip used to own; the mockups spend it on navigation
    /// and put the faces along the bottom, and so do we.
    ///
    /// <para>Deliberately not a second copy of the navigation panel. A rail that repeated Notebook,
    /// Save and Settings in smaller form would add a way to do things the player can already do and
    /// nothing else, which is a usability loss dressed as a feature. What the reference build's rail
    /// actually does is switch between views, so this does that: the relationship graph, the room
    /// map, the vote breakdown and the story are four separate questions that all currently live in
    /// one long scroll, and the rail is how you get to the one you want.</para>
    ///
    /// <para>The item you are looking at is FILLED, which is the one thing the mockups' rail does
    /// that a row of identical buttons cannot: it answers "which of these am I on" without being
    /// clicked. The caption does NOT change with the state - in this HUD a control's caption is its
    /// identity, and appending to one has broken seven tests at once before. The state rides on a
    /// child marker instead, <see cref="ActiveMarkName"/>, which is also the tab on the cell's
    /// leading edge: a fill alone is a colour, and a colour must never be the only carrier.</para>
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
        public enum Mark { Network, Rooms, Votes, Story, Overview, People }

        /// <summary>
        /// The marker the open item carries, as a child of the cell.
        ///
        /// <para>Named rather than implied so a test can ask which item is lit without reading a
        /// colour, and drawn as a tab on the cell's leading edge so a player is not asked to read
        /// one either.</para>
        /// </summary>
        public const string ActiveMarkName = "Open";

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
            Transform parent, IList<Entry> entries, float scale, Action<string> jump,
            string active = null)
        {
            var root = new GameObject(RootName, typeof(RectTransform)).GetComponent<RectTransform>();
            root.SetParent(parent, false);
            // The left gutter, under the brand card. It reads before everything else in the frame,
            // which is where navigation belongs and where every one of the mockups puts it.
            root.anchorMin = new Vector2(0f, 1f);
            root.anchorMax = new Vector2(0f, 1f);
            root.pivot = new Vector2(0f, 1f);
            root.anchoredPosition = new Vector2(14f, -104f);

            int count = entries?.Count ?? 0;
            root.sizeDelta = new Vector2(Width * scale, (count * ButtonSize + (count - 1) * Gap + 12f) * scale);
            if (count == 0) return root;

            for (int i = 0; i < count; i++)
            {
                var entry = entries[i];
                bool on = !string.IsNullOrEmpty(active) && entry.Section == active;
                var cell = HudPrimitives.Fill(entry.Caption, root,
                    on ? UiTheme.Accent : UiTheme.Ink, UiTheme.ControlRadius);
                cell.anchorMin = new Vector2(.5f, 1f); cell.anchorMax = new Vector2(.5f, 1f);
                cell.pivot = new Vector2(.5f, 1f);
                cell.anchoredPosition = new Vector2(0f, -(6f + i * (ButtonSize + Gap)) * scale);
                cell.sizeDelta = new Vector2(ButtonSize * scale, ButtonSize * scale);
                cell.GetComponent<Image>().raycastTarget = true;
                UiTheme.AddBorder(cell, UiTheme.ControlRadius, on
                    ? UiTheme.Edge(UiTheme.Emphasis.Active) : UiTheme.Outline);

                // On a filled cell the mark is drawn in the ground colour rather than the accent,
                // because an accent mark on an accent fill is an empty button.
                Draw(entry.Mark, cell, scale, on ? UiTheme.Ink : UiTheme.Accent);
                if (on)
                {
                    // Inside the cell, on its leading edge, in the paper tone: outside it would
                    // hang off the canvas margin, and in the accent it would be invisible against
                    // the accent fill it is meant to mark.
                    var tab = HudPrimitives.Fill(ActiveMarkName, cell, UiTheme.Paper, 2);
                    tab.anchorMin = new Vector2(0f, .5f); tab.anchorMax = new Vector2(0f, .5f);
                    tab.pivot = new Vector2(0f, .5f);
                    tab.anchoredPosition = new Vector2(4f * scale, 0f);
                    tab.sizeDelta = new Vector2(4f * scale, ButtonSize * .6f * scale);
                    tab.GetComponent<Image>().raycastTarget = false;
                }

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
        private static void Draw(Mark mark, RectTransform cell, float scale, Color ink)
        {
            // The spine of the book is the one mark drawn in the ground rather than the ink, so it
            // has to follow the fill too or it disappears on the active cell.
            var ground = ink == UiTheme.Accent ? UiTheme.Ink : UiTheme.Accent;
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
                    Bar(cell, ground, new Vector2(0f, 0f), new Vector2(2f, 17f), 0f, scale);
                    break;

                case Mark.Overview:
                    // A viewfinder: a frame with the house at its centre.
                    Bar(cell, ink, new Vector2(0f, 9f), new Vector2(22f, 2f), 0f, scale);
                    Bar(cell, ink, new Vector2(0f, -9f), new Vector2(22f, 2f), 0f, scale);
                    Bar(cell, ink, new Vector2(-10f, 0f), new Vector2(2f, 16f), 0f, scale);
                    Bar(cell, ink, new Vector2(10f, 0f), new Vector2(2f, 16f), 0f, scale);
                    Square(cell, ink, new Vector2(0f, 0f), 6f, scale);
                    break;

                case Mark.People:
                    // Two heads and two shoulders: the house, as a list of the people in it.
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
        }
    }
}
