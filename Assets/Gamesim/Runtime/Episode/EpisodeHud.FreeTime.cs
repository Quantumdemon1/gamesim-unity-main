using System;
using System.Collections.Generic;
using Gamesim.Presentation;
using Gamesim.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gamesim.Episode
{
    /// <summary>
    /// Free time as a screen (playtest, 2026-09-28): the house as cards you press to walk over and
    /// talk, and the moves that name nobody as tiles that say in a line what each does. It was a
    /// column of paragraphs - a heading, a sentence on meetings, two rows, a sentence on buying
    /// time, two rows, a sentence on exploring, a sentence on listening in, a row - before the one
    /// way on; the words are still said, on the tile they belong to. The notes card is the
    /// notebook's: one houseguest, their face, and what you have on them.
    /// </summary>
    public sealed partial class EpisodeHud
    {
        /// <summary>The free-time screen's parts and the notes page's cards, by the names a test finds them by.</summary>
        public const string HouseCardsName = "House cards", HouseMovesName = "House moves", NotesCardPrefix = "Notes · ";

        /// <summary>One houseguest on the free-time screen: who, the role on their photo this week, and the latest thing you have on them.</summary>
        public struct HouseCard
        {
            public string Id, Role, Note;
            public Color RoleColour;
            public HouseCard(string id, string role, Color colour, string note) { Id = id; Role = role; RoleColour = colour; Note = note; }
        }

        /// <summary>
        /// The house as cards, those in the room with you first: the photo with the week's role on
        /// its foot, the name, where you stand with them by your own reading, the latest thing you
        /// have on them, and a way to walk over and talk to them. The campaign's grid of voters, for
        /// everybody, with your notes in place of the vote's read.
        /// </summary>
        public void HouseCards(IReadOnlyList<HouseCard> cards, Action<string> talk, float cardWidth = 150f)
        {
            if (cards == null || cards.Count == 0 || content == null) return;
            float s = FontScale, width = cardWidth * s, photo = width * .9f, height = photo + 118f * s, gap = 14f * s;
            int columns = Mathf.Max(1, Mathf.Min(cards.Count, Mathf.FloorToInt((ContentWidth() + gap) / (width + gap))));
            int rows = Mathf.CeilToInt(cards.Count / (float)columns);
            var grid = new GameObject(HouseCardsName, typeof(RectTransform), typeof(GridLayoutGroup), typeof(LayoutElement)).GetComponent<RectTransform>();
            grid.SetParent(content, false);
            var layout = grid.GetComponent<GridLayoutGroup>();
            layout.cellSize = new Vector2(width, height);
            layout.spacing = new Vector2(gap, gap);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = columns;
            layout.childAlignment = TextAnchor.UpperCenter;
            var size = grid.GetComponent<LayoutElement>();
            size.minHeight = size.preferredHeight = rows * height + (rows - 1) * gap;
            var state = director != null ? director.Snapshot : null;
            if (state == null) return;
            foreach (var entry in cards)
            {
                var actor = state.Find(entry.Id);
                if (actor == null) continue;
                var card = Chrome("Houseguest · " + actor.name, grid);
                var picture = HudPrimitives.RectPortrait(card, "Photo", Portrait(entry.Id), actor, new Vector2(width - 12f * s, photo), 7);
                picture.anchorMin = picture.anchorMax = new Vector2(.5f, 1f);
                picture.pivot = new Vector2(.5f, 1f);
                picture.anchoredPosition = new Vector2(0f, -6f * s);
                if (!string.IsNullOrEmpty(entry.Role))
                {
                    var pill = Panel("Role", picture, entry.RoleColour, 4);
                    float pillWidth = Mathf.Clamp(entry.Role.Length * 7.5f + 16f, 46f, width - 20f * s) * s;
                    Anchor(pill, new Vector2(.5f, 0f), new Vector2(.5f, 0f), new Vector2(0f, 5f * s), new Vector2(pillWidth, 17f * s));
                    pill.GetComponent<Image>().raycastTarget = false;
                    var word = FixedText(pill, entry.Role, 11, UiTheme.Ink, Vector2.zero, pill.sizeDelta);
                    word.alignment = TextAlignmentOptions.Center;
                }
                var name = FixedText(card, actor.name, 15, Paper, new Vector2(6f * s, -(photo + 10f * s)), new Vector2(width - 12f * s, 20f * s));
                name.alignment = TextAlignmentOptions.Center;
                var kind = RelationshipWeb.KindOf(state, entry.Id);
                var standing = FixedText(card, RelationshipWeb.StandingWord(kind), 12,
                    kind == RelationshipWeb.Kind.Neutral ? UiTheme.Muted : RelationshipWeb.StandingColour(kind),
                    new Vector2(6f * s, -(photo + 32f * s)), new Vector2(width - 12f * s, 16f * s));
                standing.alignment = TextAlignmentOptions.Center;
                var note = FixedText(card, entry.Note ?? "", 12, string.IsNullOrEmpty(entry.Note) ? UiTheme.Muted : Paper,
                    new Vector2(6f * s, -(photo + 48f * s)), new Vector2(width - 12f * s, 16f * s));
                note.alignment = TextAlignmentOptions.Center;
                note.name = "Note line";
                AutoSize(note, 9);
                string first = (actor.name ?? "").Split(' ')[0];
                string captured = entry.Id;
                var button = FixedButton(card, CastTalkCaption(first), new Vector2(8f * s, -(photo + 70f * s)),
                    new Vector2(width - 16f * s, 36f * s), () => talk(captured));
                var words = button.GetComponentInChildren<TMP_Text>();
                if (words != null) { words.fontSize = 15; words.fontSizeMax = 15; words.alignment = TextAlignmentOptions.Center; }
            }
        }

        /// <summary>One move as a tile: the control's caption, what it does in a line, the word in its corner and its colour, its glyph, and what it commits.</summary>
        public struct MoveTile
        {
            public string Caption, Description, Corner, Glyph;
            public Color CornerTint;
            public Action Choose;
        }

        /// <summary>The moves that name nobody, two to a row: the same tiles a house event's answers are drawn as.</summary>
        public void MoveTiles(IList<MoveTile> tiles)
        {
            if (tiles == null || tiles.Count == 0 || content == null) return;
            ChoiceTiles(HouseMovesName, tiles);
        }

        /// <summary>
        /// Tiles two to a row (mockup-04): a glyph for the kind of thing it is, the caption the control
        /// has always had, what it means in a line under it, and a word in the corner - a risk or a
        /// category - in its colour, because a warning carried by colour alone is one some players
        /// never receive. The caption is the button's name, which is how a test and a screen reader
        /// find it.
        /// </summary>
        private void ChoiceTiles(string gridName, IList<MoveTile> tiles)
        {
            int columns = tiles.Count > 1 ? 2 : 1;
            float spacing = 10f * FontScale;
            float cellWidth = (ContentWidth() - spacing * (columns - 1)) / columns;
            // Tall enough for a caption and two lines under it at the player's text size.
            float cellHeight = 78f * FontScale;
            int rows = Mathf.CeilToInt(tiles.Count / (float)columns);
            var grid = new GameObject(gridName, typeof(RectTransform), typeof(GridLayoutGroup), typeof(LayoutElement)).GetComponent<RectTransform>();
            grid.SetParent(content, false);
            var layout = grid.GetComponent<GridLayoutGroup>();
            layout.cellSize = new Vector2(cellWidth, cellHeight);
            layout.spacing = new Vector2(spacing, spacing);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = columns;
            var size = grid.GetComponent<LayoutElement>();
            size.minHeight = size.preferredHeight = rows * cellHeight + (rows - 1) * spacing;

            foreach (var tile in tiles)
            {
                var rect = Chrome(tile.Caption, grid, UiTheme.Emphasis.Interactive);
                HudEmphasis.Promote(rect, UiTheme.Emphasis.Interactive);
                var button = Pressable(rect, tile.Choose);
                var colours = button.colors;
                colours.highlightedColor = new Color(1.15f, 1.15f, 1.15f);
                colours.selectedColor = colours.highlightedColor;
                button.colors = colours;

                float s = FontScale;
                var mark = Panel("Choice tile", rect, UiTheme.SurfaceRaised, 8);
                Anchor(mark, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(12f * s, 0f), new Vector2(40f * s, 40f * s));
                mark.GetComponent<Image>().raycastTarget = false;
                HudPrimitives.Glyph("Choice mark", mark, tile.Glyph ?? "journal", Accent, new Vector2(8f * s, -8f * s), 24f * s);

                if (!string.IsNullOrEmpty(tile.Corner))
                {
                    var cornerWord = FixedText(rect, tile.Corner, 11, tile.CornerTint, Vector2.zero, new Vector2(78f * s, 16f * s));
                    Anchor(cornerWord.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-10f * s, -8f * s), new Vector2(78f * s, 16f * s));
                    cornerWord.alignment = TextAlignmentOptions.Right;
                }

                float text = 64f * s;
                var caption = NewText(rect, tile.Caption, 16, Paper);
                var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
                if (semibold != null) caption.font = semibold;
                AutoSize(caption, 11);
                Anchor(caption.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(text, -8f * s),
                    new Vector2(cellWidth - text - 92f * s, 22f * s));
                if (!string.IsNullOrEmpty(tile.Description))
                {
                    var line = NewText(rect, tile.Description, 13, UiTheme.Muted);
                    AutoSize(line, 10);
                    Anchor(line.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(text, -32f * s),
                        new Vector2(cellWidth - text - 12f * s, 40f * s));
                }
            }
        }

        /// <summary>
        /// One houseguest's notes (the notebook's notes page): their face, their name and status, where
        /// you stand with them by your own reading, then what you have on them, newest first, and how
        /// many more lines the page is not showing.
        /// </summary>
        public RectTransform NotesCard(ContestantState actor, string status, string standingWord, Color standingColour, IList<string> lines, int more)
        {
            if (content == null || actor == null) return null;
            float s = FontScale, width = ContentWidth(), pad = 20f * s, face = 44f * s;
            float left = pad + face + 14f * s, inner = width - left - pad;
            var card = HudPrimitives.KitCard(NotesCardPrefix + actor.name, content, false, 14f);
            float y = 16f * s;
            var rim = HudPrimitives.Portrait(card, CharacterPortraits.Get(actor), UiTheme.Outline, face, 2f * s, false, actor);
            rim.name = "Face";
            Anchor(rim, new Vector2(0, 1), new Vector2(0, 1), new Vector2(pad, -y), rim.sizeDelta);
            var name = FixedText(card, actor.name, 18, Paper, new Vector2(left, -y), new Vector2(inner * .6f, 24f * s));
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            if (semibold != null) name.font = semibold;
            name.textWrappingMode = TextWrappingModes.NoWrap;
            AutoSize(name, 12);
            var side = FixedText(card, standingWord + (string.IsNullOrEmpty(status) ? "" : "  ·  " + status), 13, standingColour,
                new Vector2(-pad, -(y + 4f * s)), new Vector2(inner * .4f, 18f * s));
            side.alignment = TextAlignmentOptions.Right;
            side.rectTransform.anchorMin = side.rectTransform.anchorMax = side.rectTransform.pivot = new Vector2(1, 1);
            y += 30f * s;
            if (lines != null)
                foreach (var line in lines)
                    y = PlacedCopy(card, line, 15, UiTheme.Weight.Regular, UiTheme.Muted, left, y, inner) + 4f * s;
            if (more > 0)
                y = PlacedCopy(card, "and " + more + (more == 1 ? " more line" : " more lines") + " in the season's record", 13, UiTheme.Weight.Regular, UiTheme.Muted, left, y, inner) + 2f * s;
            y = Mathf.Max(y, 16f * s + face + 8f * s);
            var size = card.gameObject.AddComponent<LayoutElement>();
            size.minHeight = size.preferredHeight = y + 12f * s;
            return card;
        }
    }
}
