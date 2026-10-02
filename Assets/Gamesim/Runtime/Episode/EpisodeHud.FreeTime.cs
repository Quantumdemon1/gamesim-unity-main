using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Presentation;
using Gamesim.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gamesim.Episode
{
    /// <summary>
    /// Free time as three screens (playtest, 2026-09-28): the Free Time root - the actions left over
    /// the house as cards and the moves that name nobody as tiles, with where you are, the play in
    /// motion and the threads beside them in a column of their own - a houseguest's own screen behind
    /// a card, with the three ways to spend an action on them, and the overview's dashboard. The
    /// notes card is the notebook's: one houseguest, their face, and what you have on them.
    ///
    /// <para>The primitives here draw into whatever column is current: a screen opens two columns
    /// and the side cards push themselves as the column for their lines, so every existing row
    /// - a paragraph, a grid, an action - lands where the screen put it without learning a new
    /// parent. Every caption and grid name a test finds a control by is unchanged.</para>
    /// </summary>
    public sealed partial class EpisodeHud
    {
        /// <summary>The free-time screen's parts and the notes page's cards, by the names a test finds them by.</summary>
        public const string HouseCardsName = "House cards", HouseMovesName = "House moves", NotesCardPrefix = "Notes · ";
        /// <summary>The free-time screens' parts a test finds by name.</summary>
        public const string ScreenHeadName = "Screen head", ColumnsName = "Columns", SideColumnName = "Side column", MainColumnName = "Main column";
        public const string LocationCardName = "Current location", CurrentPlayCardName = "Current play", ThreadsCardName = "Threads card";
        public const string YourContextCardName = "Your context", AboutCardName = "About card", HouseguestStripName = "Houseguest strip";
        public const string PinnedNoteName = "Pinned note", RolesBannerName = "Roles banner";

        /// <summary>One houseguest on the free-time screen: who, the role on their photo this week, and what the card says under their name.</summary>
        public struct HouseCard
        {
            public string Id, Role, Note;
            public Color RoleColour;
            public HouseCard(string id, string role, Color colour, string note) { Id = id; Role = role; RoleColour = colour; Note = note; }
        }

        // ------------------------------------------------------------ the column a screen draws into

        private readonly Stack<(RectTransform column, float width)> columns = new Stack<(RectTransform, float)>();
        /// <summary>The width of the column the rows are being laid in, when it is not the panel's own; zero otherwise.</summary>
        private float contentWidthOverride;

        /// <summary>Makes <paramref name="column"/> the place the flow rows land until <see cref="PopContent"/>.</summary>
        private void PushContent(RectTransform column, float width)
        {
            columns.Push((content, contentWidthOverride));
            content = column;
            contentWidthOverride = width;
        }

        private void PopContent()
        {
            if (columns.Count == 0) return;
            var (column, width) = columns.Pop();
            content = column;
            contentWidthOverride = width;
        }

        private void ResetColumns() { columns.Clear(); contentWidthOverride = 0f; caseColumns.Clear(); }

        private RectTransform columnsRow, mainColumn, sideColumn;

        /// <summary>
        /// Opens two columns in the panel: the main one, where the rows go until <see cref="SideColumn"/>,
        /// and a side column <paramref name="sideWidth"/> wide for the cards beside them. At the larger
        /// text, or on a narrow stage, the side column goes under the main one instead of beside it.
        /// </summary>
        public void BeginColumns(float sideWidth)
        {
            if (content == null) return;
            float s = FontScale, width = ContentWidth(), gap = 18f * s;
            bool beside = width >= 760f && FontScale <= 1.05f;
            float side = beside ? Mathf.Min(sideWidth * s, width * .36f) : width;
            float main = beside ? width - side - gap : width;
            columnsRow = new GameObject(ColumnsName, typeof(RectTransform)).GetComponent<RectTransform>();
            columnsRow.SetParent(content, false);
            if (beside)
            {
                var row = columnsRow.gameObject.AddComponent<HorizontalLayoutGroup>();
                row.spacing = gap; row.childAlignment = TextAnchor.UpperLeft;
                row.childControlWidth = true; row.childControlHeight = true;
                row.childForceExpandWidth = false; row.childForceExpandHeight = false;
            }
            else
            {
                var stack = columnsRow.gameObject.AddComponent<VerticalLayoutGroup>();
                stack.spacing = 12f * s;
                stack.childControlWidth = true; stack.childControlHeight = true;
                stack.childForceExpandWidth = true; stack.childForceExpandHeight = false;
            }
            mainColumn = Column(MainColumnName, columnsRow, main, beside ? 1f : 0f);
            sideColumn = Column(SideColumnName, columnsRow, side, 0f);
            PushContent(mainColumn, main - 4f);
            sideColumnWidth = side - 4f;
        }

        private float sideColumnWidth;

        private RectTransform Column(string name, RectTransform parent, float width, float flexible)
        {
            var column = new GameObject(name, typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement)).GetComponent<RectTransform>();
            column.SetParent(parent, false);
            var layout = column.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 12f * FontScale;
            layout.padding = new RectOffset(2, 2, 0, 0);
            layout.childControlWidth = true; layout.childControlHeight = true;
            layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
            var element = column.GetComponent<LayoutElement>();
            element.preferredWidth = width; element.flexibleWidth = flexible;
            return column;
        }

        /// <summary>The rows go to the side column from here until <see cref="EndColumns"/>.</summary>
        public void SideColumn()
        {
            if (sideColumn == null) return;
            PopContent();
            PushContent(sideColumn, sideColumnWidth);
        }

        /// <summary>Back to the panel's own column, under both.</summary>
        public void EndColumns()
        {
            if (columnsRow == null) return;
            PopContent();
            columnsRow = mainColumn = sideColumn = null;
        }

        // ------------------------------------------------------------ the heads

        /// <summary>
        /// A screen's head: the screen's name in the accent, the one line that matters large under it
        /// - "1 ACTION LEFT" - and a line of what to do here, all centred.
        /// </summary>
        public void ScreenHead(string title, string headline, string line) => ScreenHead(title, headline, line, Accent, null);

        /// <summary>
        /// The same head in the crown's gold, under the crown: the final Head of Household's own
        /// screen, the choice of who joins them in the Final 2 (MOCKUP-PASS M8, mockup 59).
        /// </summary>
        public void CrownedScreenHead(string title, string headline, string line) =>
            ScreenHead(title, headline, line, UiTheme.Gold, PackArt.KitIconCrown);

        /// <summary>
        /// A screen's head with its title in <paramref name="tint"/> and, when there is one, the pack
        /// mark <paramref name="mark"/> centred over it in the same colour.
        /// </summary>
        private void ScreenHead(string title, string headline, string line, Color tint, string mark)
        {
            if (content == null) return;
            var head = new GameObject(ScreenHeadName, typeof(RectTransform), typeof(VerticalLayoutGroup)).GetComponent<RectTransform>();
            head.SetParent(content, false);
            var layout = head.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 2f; layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true; layout.childControlHeight = true;
            layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
            PushContent(head, ContentWidth());
            var sprite = string.IsNullOrEmpty(mark) ? null : UiTheme.Pack(mark);
            if (sprite != null)
            {
                // Its own row, as tall as the mark: the image is stretched across the row and
                // keeps its shape, so it stands centred over the title.
                var crown = new GameObject("Screen mark", typeof(RectTransform), typeof(Image), typeof(LayoutElement)).GetComponent<Image>();
                crown.rectTransform.SetParent(head, false);
                crown.sprite = sprite; crown.color = tint; crown.preserveAspect = true; crown.raycastTarget = false;
                var size = crown.GetComponent<LayoutElement>();
                size.minHeight = size.preferredHeight = 34f * FontScale;
            }
            if (!string.IsNullOrEmpty(title))
            {
                var name = FlowText(title, 34, tint);
                name.alignment = TextAlignmentOptions.Center;
                var bold = UiTheme.Font(UiTheme.Weight.Bold);
                if (bold != null) name.font = bold;
                name.characterSpacing = 4f;
            }
            if (!string.IsNullOrEmpty(headline))
            {
                var big = FlowText(headline, 26, UiTheme.Glow);
                big.alignment = TextAlignmentOptions.Center;
                var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
                if (semibold != null) big.font = semibold;
                big.characterSpacing = 3f;
                big.name = "Headline";
            }
            if (!string.IsNullOrEmpty(line))
            {
                var words = FlowText(line, 16, UiTheme.Muted);
                words.alignment = TextAlignmentOptions.Center;
            }
            PopContent();
        }

        /// <summary>A section's head: a glyph, its name in the heading blue, and a line of what it is for.</summary>
        public void SectionHead(string glyph, string title, string line)
        {
            if (content == null) return;
            float s = FontScale;
            var row = new GameObject("Section head · " + title, typeof(RectTransform), typeof(LayoutElement)).GetComponent<RectTransform>();
            row.SetParent(content, false);
            float height = (string.IsNullOrEmpty(line) ? 30f : 50f) * s;
            var element = row.GetComponent<LayoutElement>();
            element.minHeight = element.preferredHeight = height;
            float text = 4f;
            var mark = HudPrimitives.Glyph("Section mark", row, glyph, Accent, new Vector2(2f, -4f * s), 26f * s);
            if (mark != null) text = 40f * s;
            var name = FixedText(row, title, 18, UiTheme.Heading, new Vector2(text, -2f * s), new Vector2(ContentWidth() - text, 26f * s));
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            if (semibold != null) name.font = semibold;
            name.characterSpacing = 2f;
            if (!string.IsNullOrEmpty(line))
                FixedText(row, line, 13, UiTheme.Muted, new Vector2(text, -28f * s), new Vector2(ContentWidth() - text, 20f * s));
        }

        /// <summary>A small muted line of copy: the rule under a head, a note under a card.</summary>
        public void Footnote(string words, Color? colour = null)
        {
            var text = FlowText(words, 13, colour ?? UiTheme.Muted);
            text.name = "Footnote";
        }

        // ------------------------------------------------------------ the side cards

        private RectTransform sideCard;

        /// <summary>
        /// Opens a card in the current column with a heading and, when given, a chip beside it. The
        /// rows until <see cref="EndSideCard"/> are the card's.
        /// </summary>
        public void BeginSideCard(string name, string heading, string chip = null, Color? chipTint = null)
        {
            if (content == null) return;
            float s = FontScale;
            var card = HudPrimitives.KitCard(name, content, false, 12f);
            // The kit card's own edge and glow stay stretched over it: the layout group lays out
            // only the lines added after it.
            foreach (Transform decoration in card) decoration.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            var layout = card.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(Mathf.RoundToInt(16f * s), Mathf.RoundToInt(16f * s), Mathf.RoundToInt(12f * s), Mathf.RoundToInt(14f * s));
            layout.spacing = 6f * s;
            layout.childControlWidth = true; layout.childControlHeight = true;
            layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
            float inner = ContentWidth() - 32f * s;
            PushContent(card, inner);
            var row = new GameObject("Card heading", typeof(RectTransform), typeof(LayoutElement)).GetComponent<RectTransform>();
            row.SetParent(content, false);
            var element = row.GetComponent<LayoutElement>();
            element.minHeight = element.preferredHeight = 22f * s;
            var title = FixedText(row, heading, 13, UiTheme.Heading, Vector2.zero, new Vector2(inner, 22f * s));
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            if (semibold != null) title.font = semibold;
            title.characterSpacing = 3f;
            if (!string.IsNullOrEmpty(chip))
            {
                float width = Mathf.Clamp(chip.Length * 7f + 18f, 44f, 120f) * s;
                var pill = HudPrimitives.Chip("Card chip", row, chip, chipTint ?? UiTheme.Muted, width, 20f * s);
                pill.anchorMin = pill.anchorMax = new Vector2(1f, .5f); pill.pivot = new Vector2(1f, .5f);
                pill.anchoredPosition = Vector2.zero;
            }
            sideCard = card;
        }

        public void EndSideCard()
        {
            if (sideCard == null) return;
            PopContent();
            sideCard = null;
        }

        /// <summary>A line in a card, at a size and weight: the room's name large, a thread's label bold, a note muted.</summary>
        public TMP_Text CardLine(string words, int size, Color colour, UiTheme.Weight weight = UiTheme.Weight.Regular)
        {
            var text = FlowText(words, size, colour);
            var font = UiTheme.Font(weight);
            if (font != null && weight != UiTheme.Weight.Regular) text.font = font;
            return text;
        }

        /// <summary>A play's progress as a bar: "Know Them" and "0/3" over a track filling as it is met.</summary>
        public void ProgressBar(string label, int have, int need, Color tint)
        {
            if (content == null) return;
            float s = FontScale, width = ContentWidth();
            var row = new GameObject("Progress · " + label, typeof(RectTransform), typeof(LayoutElement)).GetComponent<RectTransform>();
            row.SetParent(content, false);
            var element = row.GetComponent<LayoutElement>();
            element.minHeight = element.preferredHeight = 34f * s;
            var words = FixedText(row, label, 14, Paper, Vector2.zero, new Vector2(width * .7f, 20f * s));
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            if (semibold != null) words.font = semibold;
            var count = FixedText(row, have + "/" + Mathf.Max(1, need), 14, tint, new Vector2(width * .7f, 0f), new Vector2(width * .3f, 20f * s));
            count.alignment = TextAlignmentOptions.Right;
            count.name = "Progress count";
            var track = Panel("Track", row, UiTheme.Outline, 3);
            Anchor(track, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -25f * s), new Vector2(width, 6f * s));
            track.GetComponent<Image>().raycastTarget = false;
            var fill = Panel("Fill", track, tint, 3);
            Anchor(fill, new Vector2(0f, 0f), new Vector2(0f, 0f), Vector2.zero, new Vector2(width * Mathf.Clamp01(have / (float)Mathf.Max(1, need)), 6f * s));
            fill.GetComponent<Image>().raycastTarget = false;
        }

        // ------------------------------------------------------------ the house as cards

        /// <summary>
        /// The house as cards, those in the room with you first: the photo with the week's role on
        /// its foot, the name, where you stand with them by your own reading, the line the screen has
        /// on them, and a way to walk over and talk to them. With <paramref name="pick"/>, the name is
        /// a control of its own that opens their screen (the interaction detail): the card is the
        /// decision, the button under it the shortcut.
        /// </summary>
        public void HouseCards(IReadOnlyList<HouseCard> cards, Action<string> talk, Action<string> pick = null, float cardWidth = 150f)
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
                string captured = entry.Id;
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
                TMP_Text name;
                if (pick != null)
                {
                    // The name is the card's own control: it opens their screen. Named by the name,
                    // which is how a test and a screen reader find it.
                    var seat = Panel(actor.name, card, new Color(0f, 0f, 0f, 0f), 6);
                    Anchor(seat, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(6f * s, -(photo + 8f * s)), new Vector2(width - 12f * s, 24f * s));
                    var button = Pressable(seat, () => pick(captured));
                    var colours = button.colors;
                    colours.normalColor = new Color(1f, 1f, 1f, 0f);
                    colours.highlightedColor = new Color(1f, 1f, 1f, .12f);
                    colours.selectedColor = colours.highlightedColor;
                    colours.pressedColor = new Color(1f, 1f, 1f, .2f);
                    button.colors = colours;
                    name = NewText(seat, actor.name, 15, Paper);
                    Stretch(name.rectTransform, 2f, 0f, 2f, 0f);
                    AutoSize(name, 11);
                }
                else name = FixedText(card, actor.name, 15, Paper, new Vector2(6f * s, -(photo + 10f * s)), new Vector2(width - 12f * s, 20f * s));
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
                var talkButton = FixedButton(card, CastTalkCaption(first), new Vector2(8f * s, -(photo + 70f * s)),
                    new Vector2(width - 16f * s, 36f * s), () => talk(captured));
                var words = talkButton.GetComponentInChildren<TMP_Text>();
                if (words != null) { words.fontSize = 15; words.fontSizeMax = 15; words.alignment = TextAlignmentOptions.Center; }
            }
        }

        /// <summary>
        /// The house in a strip of small cards, one pressed: the photo with the week's role on it, the
        /// name and where you stand, each a control that makes them the one the screen is about.
        /// </summary>
        public void HouseguestStrip(IReadOnlyList<string> ids, string selectedId, Action<string> pick) => HouseguestStrip(ids, selectedId, pick, 0f);

        /// <summary>
        /// The strip with its photos no taller than <paramref name="photoCap"/> at the resting size
        /// (zero for the card's own proportion): the briefing's glance, whose column is wide and
        /// whose frame is short.
        /// </summary>
        public void HouseguestStrip(IReadOnlyList<string> ids, string selectedId, Action<string> pick, float photoCap)
        {
            if (ids == null || ids.Count == 0 || content == null) return;
            var state = director != null ? director.Snapshot : null;
            if (state == null) return;
            float s = FontScale, gap = 10f * s;
            float width = Mathf.Clamp((ContentWidth() + gap) / ids.Count - gap, 96f * s, 150f * s);
            float photo = width * .78f;
            if (photoCap > 0f) photo = Mathf.Min(photo, photoCap * s);
            float height = photo + 52f * s;
            int columns = Mathf.Max(1, Mathf.Min(ids.Count, Mathf.FloorToInt((ContentWidth() + gap) / (width + gap))));
            int rows = Mathf.CeilToInt(ids.Count / (float)columns);
            var grid = new GameObject(HouseguestStripName, typeof(RectTransform), typeof(GridLayoutGroup), typeof(LayoutElement)).GetComponent<RectTransform>();
            grid.SetParent(content, false);
            var layout = grid.GetComponent<GridLayoutGroup>();
            layout.cellSize = new Vector2(width, height);
            layout.spacing = new Vector2(gap, gap);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = columns;
            layout.childAlignment = TextAnchor.UpperLeft;
            var size = grid.GetComponent<LayoutElement>();
            size.minHeight = size.preferredHeight = rows * height + (rows - 1) * gap;
            foreach (var id in ids)
            {
                var actor = state.Find(id);
                if (actor == null) continue;
                bool selected = id == selectedId;
                var card = Chrome(actor.name, grid, selected ? UiTheme.Emphasis.Active : UiTheme.Emphasis.Interactive);
                if (selected) UiTheme.AddBorder(card, 8, Accent);
                string captured = id;
                var button = Pressable(card, () => pick(captured));
                var colours = button.colors;
                colours.highlightedColor = new Color(1.12f, 1.12f, 1.12f);
                colours.selectedColor = colours.highlightedColor;
                button.colors = colours;
                var picture = HudPrimitives.RectPortrait(card, "Photo", Portrait(id), actor, new Vector2(width - 10f * s, photo), 6);
                picture.anchorMin = picture.anchorMax = new Vector2(.5f, 1f);
                picture.pivot = new Vector2(.5f, 1f);
                picture.anchoredPosition = new Vector2(0f, -5f * s);
                string role = RoleFor(state, id, out var roleColour);
                if (role != null)
                {
                    var pill = Panel("Role", picture, roleColour, 4);
                    float pillWidth = Mathf.Clamp(role.Length * 7.5f + 16f, 42f, width - 16f * s) * s;
                    Anchor(pill, new Vector2(.5f, 0f), new Vector2(.5f, 0f), new Vector2(0f, 4f * s), new Vector2(pillWidth, 16f * s));
                    pill.GetComponent<Image>().raycastTarget = false;
                    var word = FixedText(pill, role, 10, UiTheme.Ink, Vector2.zero, pill.sizeDelta);
                    word.alignment = TextAlignmentOptions.Center;
                }
                var name = FixedText(card, actor.name, 13, Paper, new Vector2(4f * s, -(photo + 8f * s)), new Vector2(width - 8f * s, 18f * s));
                name.alignment = TextAlignmentOptions.Center;
                AutoSize(name, 10);
                var kind = RelationshipWeb.KindOf(state, id);
                var standing = FixedText(card, RelationshipWeb.StandingWord(kind), 11,
                    kind == RelationshipWeb.Kind.Neutral ? UiTheme.Muted : RelationshipWeb.StandingColour(kind),
                    new Vector2(4f * s, -(photo + 28f * s)), new Vector2(width - 8f * s, 16f * s));
                standing.alignment = TextAlignmentOptions.Center;
            }
        }

        /// <summary>The week's role a houseguest carries on a photo, and its colour; null for nobody in particular.</summary>
        public static string RoleFor(EpisodeState state, string id, out Color colour)
        {
            colour = UiTheme.Muted;
            if (state == null || id == null) return null;
            if (id == state.hohId) { colour = UiTheme.Gold; return "HOH"; }
            if (state.nominees != null && state.nominees.Contains(id)) { colour = UiTheme.Danger; return "NOM"; }
            if (id == state.vetoHolderId) { colour = UiTheme.Accent; return "VETO"; }
            return null;
        }

        /// <summary>
        /// The week's roles on one band: who is Head of Household, who holds the veto, who is on the
        /// block, each with its mark, in the mockups' colours.
        /// </summary>
        public void RolesBanner(EpisodeState state)
        {
            if (state == null || content == null) return;
            float s = FontScale, width = ContentWidth();
            var band = HudPrimitives.KitCard(RolesBannerName, content, false, 10f);
            var element = band.gameObject.AddComponent<LayoutElement>();
            element.minHeight = element.preferredHeight = 46f * s;
            var parts = new List<(string glyph, string label, Color tint, string value)>();
            var hoh = state.Find(state.hohId);
            if (hoh != null) parts.Add(("crown", "HOH", UiTheme.Gold, hoh.name));
            var veto = state.Find(state.vetoHolderId);
            if (veto != null) parts.Add(("veto-token", "Veto", UiTheme.Accent, veto.name));
            var block = (state.nominees ?? new List<string>()).Select(id => state.Find(id)).Where(c => c != null).Select(c => c.name).ToList();
            if (block.Count > 0) parts.Add(("target", "Nominees", UiTheme.Danger, string.Join(" and ", block)));
            if (parts.Count == 0) { FixedText(band, "No roles decided yet this week.", 15, UiTheme.Muted, new Vector2(16f * s, -12f * s), new Vector2(width - 32f * s, 22f * s)); return; }
            float slot = width / parts.Count;
            for (int i = 0; i < parts.Count; i++)
            {
                float x = i * slot + 14f * s;
                var mark = HudPrimitives.Glyph("Role mark", band, parts[i].glyph, parts[i].tint, new Vector2(x, -11f * s), 24f * s);
                float text = mark != null ? x + 32f * s : x;
                var label = FixedText(band, parts[i].label, 15, parts[i].tint, new Vector2(text, -12f * s), new Vector2(80f * s, 22f * s));
                var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
                if (semibold != null) label.font = semibold;
                float labelWidth = label.GetPreferredValues(label.text).x + 8f * s;
                var value = FixedText(band, parts[i].value, 16, Paper, new Vector2(text + labelWidth, -12f * s), new Vector2(slot - (text - i * slot) - labelWidth - 10f * s, 22f * s));
                value.name = "Role holder";
                AutoSize(value, 11);
                if (i > 0)
                {
                    var rule = Panel("Rule", band, UiTheme.Outline, 1);
                    Anchor(rule, new Vector2(0f, .5f), new Vector2(0f, .5f), new Vector2(i * slot, 0f), new Vector2(1f, 26f * s));
                    rule.GetComponent<Image>().raycastTarget = false;
                }
            }
        }

        /// <summary>
        /// The standing legend - a dot and a word for allied, neutral and hostile, in the web's own
        /// colours - at the right-hand end of a section head's row, on the head's own line: the
        /// briefing has no row to spare for it. Laid from the left and then hung from the right,
        /// since its width is the words'.
        /// </summary>
        private void StandingLegendIn(RectTransform row)
        {
            if (row == null) return;
            float s = FontScale;
            var run = new GameObject("Standing legend", typeof(RectTransform)).GetComponent<RectTransform>();
            run.SetParent(row, false);
            float x = 0f;
            foreach (var kind in new[] { RelationshipWeb.Kind.Alliance, RelationshipWeb.Kind.Neutral, RelationshipWeb.Kind.Rivalry })
            {
                var colour = kind == RelationshipWeb.Kind.Neutral ? UiTheme.Muted : RelationshipWeb.StandingColour(kind);
                var dot = HudPrimitives.Disc("Legend dot", run, colour);
                Anchor(dot, new Vector2(0f, .5f), new Vector2(0f, .5f), new Vector2(x, 0f), new Vector2(10f * s, 10f * s));
                var word = FixedText(run, RelationshipWeb.StandingWord(kind), 12, UiTheme.Muted, new Vector2(x + 14f * s, -1f * s), new Vector2(80f * s, 20f * s));
                float wide = Mathf.Ceil(word.GetPreferredValues(word.text).x);
                // The box as wide as the word, so the run ends where the words do.
                word.rectTransform.sizeDelta = new Vector2(wide + 2f, 20f * s);
                x += 14f * s + wide + 18f * s;
            }
            Anchor(run, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(0f, -4f * s), new Vector2(Mathf.Max(1f, x - 18f * s), 20f * s));
        }

        // ------------------------------------------------------------ the moves as tiles

        /// <summary>
        /// One move as a tile: the control's caption, what it does in a line, the word in its corner
        /// (a risk or a category) and its colour, a second word beside it for a value, its glyph, what
        /// it costs in a line at its foot, and what it commits.
        /// </summary>
        public struct MoveTile
        {
            public string Caption, Description, Corner, Glyph, Foot, Value;
            public Color CornerTint, ValueTint;
            public Action Choose;
            /// <summary>
            /// A choice that stays chosen until something else is (MOCKUP-PASS M15): null for a move,
            /// which is done when pressed; false for one of a set not chosen, true for the chosen one.
            /// A row draws a radio ring at its right-hand end, filled when chosen, and the chosen row
            /// Pack 1's selected panel.
            /// </summary>
            public bool? Selected;
        }

        /// <summary>
        /// How tiles are laid: two rows to a column, as cards several to a row (the free-time screens),
        /// one to a row (a column of choices), or one to a thin row as tall as its words (a jury
        /// response, MOCKUP-PASS M11).
        /// </summary>
        public enum TileStyle { Rows, Cards, List, Compact }

        /// <summary>The moves that name nobody, as cards several to a row on a wide column, else two to a row.</summary>
        public void MoveTiles(IList<MoveTile> tiles, TileStyle style = TileStyle.Rows)
        {
            if (tiles == null || tiles.Count == 0 || content == null) return;
            ChoiceTiles(HouseMovesName, tiles, style);
        }

        /// <summary>A named grid of tiles: the free-time screens' interactions and the overview's recommended moves.</summary>
        public void Tiles(string gridName, IList<MoveTile> tiles, TileStyle style = TileStyle.Cards) => Tiles(gridName, tiles, style, 0f);

        /// <summary>
        /// The same grid with its cards <paramref name="cardHeight"/> tall at the resting size
        /// (zero for the cards' own 150): the briefing's recommended moves, whose frame is short.
        /// </summary>
        public void Tiles(string gridName, IList<MoveTile> tiles, TileStyle style, float cardHeight)
        {
            if (tiles == null || tiles.Count == 0 || content == null) return;
            ChoiceTiles(gridName, tiles, style, cardHeight);
        }

        /// <summary>
        /// Tiles two to a row (mockup-04): a glyph for the kind of thing it is, the caption the control
        /// has always had, what it means in a line under it, and a word in the corner - a risk or a
        /// category - in its colour, because a warning carried by colour alone is one some players
        /// never receive. The caption is the button's name, which is how a test and a screen reader
        /// find it. As cards, several to a row: the glyph and the caption on top, the chips under
        /// them, the line, and the cost at the foot.
        /// </summary>
        private void ChoiceTiles(string gridName, IList<MoveTile> tiles, TileStyle style = TileStyle.Rows, float cardHeight = 0f)
        {
            bool compact = style == TileStyle.Compact;
            float s = FontScale, spacing = (compact ? 6f : 10f) * s;
            bool cards = style == TileStyle.Cards && FontScale <= 1.05f && ContentWidth() >= 560f;
            int columns = cards ? Mathf.Clamp(Mathf.FloorToInt((ContentWidth() + spacing) / (176f * s + spacing)), 2, Mathf.Max(2, tiles.Count))
                : style == TileStyle.List || compact ? 1 : tiles.Count > 1 ? 2 : 1;
            float cellWidth = (ContentWidth() - spacing * (columns - 1)) / columns;
            // Tall enough for a caption and two lines under it at the player's text size; a card
            // stacks its chips and its foot as well. A compact row starts at one line and grows to
            // its tallest tile's words once they are measured, below.
            float cellHeight = (cards ? (cardHeight > 0f ? cardHeight : 150f) : compact ? CompactTileHeight : 78f) * FontScale;
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

            float tallest = cellHeight;
            foreach (var tile in tiles)
            {
                var rect = Chrome(tile.Caption, grid, UiTheme.Emphasis.Interactive);
                HudEmphasis.Promote(rect, UiTheme.Emphasis.Interactive);
                var button = Pressable(rect, tile.Choose);
                var colours = button.colors;
                colours.highlightedColor = new Color(1.15f, 1.15f, 1.15f);
                colours.selectedColor = colours.highlightedColor;
                button.colors = colours;
                if (cards) CardTile(rect, tile, cellWidth, cellHeight);
                else if (compact) tallest = Mathf.Max(tallest, CompactTile(rect, tile, cellWidth));
                else RowTile(rect, tile, cellWidth);
            }
            if (!compact) return;
            // Every row as tall as the tallest one's words, so a line that wraps to two is never cut.
            layout.cellSize = new Vector2(cellWidth, tallest);
            size.minHeight = size.preferredHeight = rows * tallest + (rows - 1) * spacing;
        }

        /// <summary>A compact row's height with its line on one line, at the resting text size: the mockup's 52.</summary>
        private const float CompactTileHeight = 52f;

        /// <summary>
        /// A tile as a compact row (MOCKUP-PASS M11): a small glyph, the caption at 14 in the
        /// semibold cut with the risk as a chip in its corner, the line at 13 on one or two lines
        /// under it, and the chevron at the end saying the row acts. Returns the height its words
        /// need, which the grid takes for every row.
        /// </summary>
        private float CompactTile(RectTransform rect, MoveTile tile, float cellWidth)
        {
            float s = FontScale, pad = 10f * s, top = 7f * s;
            var mark = HudPrimitives.Glyph("Choice mark", rect, tile.Glyph ?? "journal", Accent, new Vector2(pad, -(top + 1f * s)), 20f * s);
            float text = mark != null ? pad + 28f * s : pad + 4f * s;
            float chevron = 14f * s;
            HudPrimitives.Chevron(rect, UiTheme.Hairline, chevron).anchoredPosition = new Vector2(-pad, 0f);
            float right = pad + chevron + 8f * s;
            float chip = 0f;
            if (!string.IsNullOrEmpty(tile.Corner))
            {
                chip = Mathf.Clamp(tile.Corner.Length * 7f + 18f, 44f, 120f) * s;
                var corner = HudPrimitives.Chip("Risk chip", rect, tile.Corner, tile.CornerTint, chip, 18f * s);
                Anchor(corner, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-right, -top), corner.sizeDelta);
                chip += 6f * s;
            }
            // The caption's box at 1.3 times its size: under 1.21 TMP draws nothing.
            float captionHeight = 19f * s;
            var caption = NewText(rect, tile.Caption, 14, Paper);
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            if (semibold != null) caption.font = semibold;
            AutoSize(caption, 11);
            Anchor(caption.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(text, -top),
                new Vector2(Mathf.Max(40f * s, cellWidth - text - right - chip), captionHeight));
            float y = top + captionHeight + 1f * s;
            if (string.IsNullOrEmpty(tile.Description)) return y + 6f * s;
            var line = NewText(rect, tile.Description, 13, UiTheme.Muted);
            float width = Mathf.Max(40f * s, cellWidth - text - right);
            // One line or two: what the words need, up to two lines of the size. A line that would
            // want a third is taken down a size or two by the auto-size rather than cut.
            float need = line.GetPreferredValues(line.text, width, 0f).y;
            float two = line.GetPreferredValues("Ag\nAg", width, 0f).y;
            float height = Mathf.Ceil(Mathf.Min(need, two)) + 3f * s;
            AutoSize(line, 10);
            Anchor(line.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(text, -y), new Vector2(width, height));
            return y + height + 6f * s;
        }

        private void RowTile(RectTransform rect, MoveTile tile, float cellWidth)
        {
            float s = FontScale;
            // A choice that stays chosen says so three ways: the selected panel, the filled ring and
            // the corner's word, which is the one of them that is not a colour.
            bool radio = tile.Selected.HasValue;
            if (tile.Selected == true) SelectedEdge(rect);
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
            // The ring sits below the corner's word at the row's right-hand end; the line stops short of it.
            float ring = radio ? (RadioSide + 12f) * s : 0f;
            if (!string.IsNullOrEmpty(tile.Description))
            {
                var line = NewText(rect, tile.Description, 13, UiTheme.Muted);
                AutoSize(line, 10);
                Anchor(line.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(text, -32f * s),
                    new Vector2(cellWidth - text - 12f * s - ring, 40f * s));
            }
            if (radio) RadioRing(rect, tile.Selected.Value, RadioSide * s, new Vector2(1f, .5f), new Vector2(-12f * s, -6f * s));
        }

        /// <summary>A tile as a card: the glyph and the caption on top, the chips under them, the line, the cost at the foot.</summary>
        private void CardTile(RectTransform rect, MoveTile tile, float cellWidth, float cellHeight)
        {
            float s = FontScale, pad = 12f * s, inner = cellWidth - 2f * pad;
            var mark = HudPrimitives.Glyph("Choice mark", rect, tile.Glyph ?? "journal", Accent, new Vector2(pad, -pad), 26f * s);
            float text = mark != null ? pad + 34f * s : pad;
            var caption = NewText(rect, tile.Caption, 15, Paper);
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            if (semibold != null) caption.font = semibold;
            AutoSize(caption, 10);
            Anchor(caption.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(text, -(pad - 2f * s)), new Vector2(cellWidth - text - pad, 30f * s));
            float y = pad + 34f * s;
            float x = pad;
            if (!string.IsNullOrEmpty(tile.Corner))
            {
                float width = Mathf.Clamp(tile.Corner.Length * 7f + 18f, 44f, inner * .6f) * s;
                var chip = HudPrimitives.Chip("Risk chip", rect, tile.Corner, tile.CornerTint, width, 20f * s);
                Anchor(chip, new Vector2(0, 1), new Vector2(0, 1), new Vector2(x, -y), chip.sizeDelta);
                x += width + 6f * s;
            }
            if (!string.IsNullOrEmpty(tile.Value))
            {
                float width = Mathf.Clamp(tile.Value.Length * 7f + 18f, 44f, inner * .6f) * s;
                var chip = HudPrimitives.Chip("Value chip", rect, tile.Value, tile.ValueTint, width, 20f * s);
                Anchor(chip, new Vector2(0, 1), new Vector2(0, 1), new Vector2(x, -y), chip.sizeDelta);
            }
            if (!string.IsNullOrEmpty(tile.Corner) || !string.IsNullOrEmpty(tile.Value)) y += 26f * s;
            float foot = string.IsNullOrEmpty(tile.Foot) ? 0f : 22f * s;
            if (!string.IsNullOrEmpty(tile.Description))
            {
                var line = NewText(rect, tile.Description, 12, UiTheme.Muted);
                AutoSize(line, 9);
                Anchor(line.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(pad, -y), new Vector2(inner, Mathf.Max(20f * s, cellHeight - y - foot - 6f * s)));
            }
            if (!string.IsNullOrEmpty(tile.Foot))
            {
                var cost = FixedText(rect, tile.Foot, 12, Paper, new Vector2(pad, -(cellHeight - pad - 16f * s)), new Vector2(inner, 18f * s));
                var medium = UiTheme.Font(UiTheme.Weight.Medium);
                if (medium != null) cost.font = medium;
                cost.name = "Tile foot";
            }
        }

        // ------------------------------------------------------------ under the way on

        /// <summary>
        /// A note under the pinned action, in the warning colour: what moving on costs - the unused
        /// actions. The pinned row rises to leave it room, and the scroll's foot with it.
        /// </summary>
        public void PinnedNote(string words)
        {
            if (modal == null || pinnedAction == null || string.IsNullOrEmpty(words)) return;
            float s = FontScale, height = 22f * s;
            pinnedAction.offsetMin = new Vector2(pinnedAction.offsetMin.x, PinnedMargin + height);
            pinnedAction.offsetMax = new Vector2(pinnedAction.offsetMax.x, PinnedMargin + height + PinnedHeight * s);
            var note = FixedText(modal, words, 13, UiTheme.Warning, Vector2.zero, new Vector2(400f * s, height));
            note.name = PinnedNoteName;
            note.alignment = TextAlignmentOptions.Center;
            var rect = note.rectTransform;
            rect.anchorMin = new Vector2(.5f, 0f); rect.anchorMax = new Vector2(.5f, 0f); rect.pivot = new Vector2(.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, PinnedMargin - 4f * s);
            rect.sizeDelta = new Vector2(Mathf.Min(modal.sizeDelta.x - 40f, 600f * s), height);
            pinnedNoteHeight = height;
            ApplyPinnedInset();
        }

        private float pinnedNoteHeight;

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
