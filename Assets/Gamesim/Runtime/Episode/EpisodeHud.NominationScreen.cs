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
    /// The nomination's pieces on the strategy stage (PACK8-PASS-PLAN B1, mockup 72): one step's
    /// body at a time, sized to the height the stage's header and footer leave it, so the screen
    /// never scrolls. The owner's screenshots 66 to 69 were two story beats, the ceremony, a row of
    /// faces and the way on stacked in one scroll.
    ///
    /// <para>Every piece here measures what the column already holds and takes what is left: a
    /// story beat's text beside its tiles, the Head of Household's candidates as cards that shrink
    /// to fit and turn into rows when a card would get too small to read, and the ceremony's faces,
    /// which do the same. Every caption, card name and pill a test finds a control by is the one it
    /// always had; the Pack 8 frames are children drawn behind them.</para>
    /// </summary>
    public sealed partial class EpisodeHud
    {
        /// <summary>The footer's way back to the picker, from a story opened from the tracker or the backdoor view.</summary>
        public const string BackToNomineesCaption = "Back to your nominees";
        /// <summary>The picker's door to the backdoor view, which holds the "Aim this week at" rows.</summary>
        public const string PlanBackdoorCaption = "Plan a backdoor";
        /// <summary>The nomination's parts, by the names a test finds them by.</summary>
        public const string NominationStatusRowName = "Status row", NominationTrackerRowName = "Tracker row";
        public const string StoryStepName = "Story step", StepTitleName = "Step title", SelectedNomineesName = "Selected nominees";
        public const string StoryPeopleName = "Story people", BackdoorAimsName = "Backdoor aims", PlanBackdoorRowName = "Backdoor plan row";
        /// <summary>A picker card's selected frame, shown while the card is picked.</summary>
        private const string PickedArtName = "Picked art";

        // ------------------------------------------------------------ the room a step has

        /// <summary>
        /// The height one step's body has on the strategy stage: the panel, less the phase band and
        /// the header rows over the scroll, the footer's row under it and the column's own padding.
        /// Read after the header is built; a step is laid out to it so the column never scrolls.
        /// </summary>
        public float NominationStepRoom()
        {
            if (modal == null || content == null) return 0f;
            var layout = content.GetComponent<VerticalLayoutGroup>();
            float padding = layout != null ? layout.padding.top + layout.padding.bottom : 24f;
            float top = StrategyBandFoot + strategyHeaderUsed;
            float foot = PinnedMargin + pinnedNoteHeight + PinnedHeight * FontScale + 10f;
            return Mathf.Max(0f, modal.sizeDelta.y - top - foot - padding);
        }

        /// <summary>
        /// What is left of <see cref="NominationStepRoom"/> for the next row: every row the column
        /// already holds, at the column's width, and the gap after each. A unit short, so rounding
        /// in the layout's own pass never tips the column into a scroll.
        /// </summary>
        private float NominationRoomLeft()
        {
            if (content == null) return 0f;
            var layout = content.GetComponent<VerticalLayoutGroup>();
            float spacing = layout != null ? layout.spacing : 12f, width = ContentWidth(), used = 0f;
            foreach (Transform child in content)
            {
                if (!child.gameObject.activeSelf || !(child is RectTransform row)) continue;
                used += ColumnRowHeight(row, width) + spacing;
            }
            return NominationStepRoom() - used - 2f;
        }

        /// <summary>A row's height as the column's layout will give it: its element's height, or its words' at the column's width.</summary>
        private static float ColumnRowHeight(RectTransform row, float width)
        {
            var element = row.GetComponent<LayoutElement>();
            float height = element != null ? Mathf.Max(element.minHeight, element.preferredHeight) : 0f;
            var text = row.GetComponent<TMP_Text>();
            if (text != null) height = Mathf.Max(height, Mathf.Ceil(text.GetPreferredValues(text.text, width, 0f).y));
            return height;
        }

        /// <summary>The last row of the column with this name: the one just built, never an older copy.</summary>
        private RectTransform LastColumnRow(string name)
        {
            if (content == null) return null;
            for (int i = content.childCount - 1; i >= 0; i--)
                if (content.GetChild(i).name == name) return (RectTransform)content.GetChild(i);
            return null;
        }

        /// <summary>
        /// A grid of cards no narrower than <paramref name="minWidth"/> and no shorter than
        /// <paramref name="minHeight"/>: as few rows as the column's width allows, so each card is
        /// as tall as the height lets it be, up to <paramref name="maxHeight"/>. When no number of
        /// rows fits the height, the cards stand at their minimum, as many to a row as the column
        /// holds, and the grid runs taller than asked: a name is not given up for the height.
        /// </summary>
        private static void SolveCardGrid(int count, float column, float fit, float minWidth, float maxWidth, float minHeight, float maxHeight,
            float gap, out float width, out float height, out int columns)
        {
            count = Mathf.Max(1, count);
            // Half a unit of slack either way: a column that holds five cards exactly must not lose
            // one to the float that scaled its minimum.
            for (int rows = 1; rows <= count; rows++)
            {
                int across = Mathf.CeilToInt(count / (float)rows);
                float wide = (column - (across - 1) * gap) / across;
                float tall = (fit - (rows - 1) * gap) / rows;
                if (wide + .5f < minWidth) continue;
                // More rows only make each shorter.
                if (tall + .5f < minHeight) break;
                width = Mathf.Min(wide, maxWidth); height = Mathf.Min(tall, maxHeight); columns = across;
                return;
            }
            columns = Mathf.Clamp(Mathf.FloorToInt((column + gap + .5f) / (minWidth + gap)), 1, count);
            int lines = Mathf.CeilToInt(count / (float)columns);
            width = Mathf.Min(maxWidth, (column - (columns - 1) * gap) / columns);
            height = Mathf.Clamp((fit - (lines - 1) * gap) / lines, minHeight, maxHeight);
        }

        /// <summary>A label of an exact size, not the text scale's: a card scaled to fit carries its words at its own scale.</summary>
        private TMP_Text SizedText(RectTransform parent, string value, float size, Color colour, float floor)
        {
            var text = NewText(parent, value, 10, colour);
            text.fontSize = Mathf.Max(floor, Mathf.Round(size));
            AutoSize(text, floor);
            return text;
        }

        /// <summary>
        /// The nomination's way on in Pack 8's continue button, drawn behind the caption it always had;
        /// the control, its name and its words are unchanged. Without the pack it stays the panel's row.
        /// </summary>
        public void WearNominationPrimary(Button primary)
        {
            if (primary == null || UiTheme.Pack(PackArt.Pack8ContinueButton) == null) return;
            EndScreenKit.Frame((RectTransform)primary.transform, PackArt.Pack8ContinueButton, 14f * FontScale,
                new Color(Surface.r, Surface.g, Surface.b, .92f));
        }

        // ------------------------------------------------------------ titles and lines

        /// <summary>
        /// A step's head: its name large, in the step's colour, and a line of what is happening at
        /// the stage's reading size. The ceremony's and the outcome's name is 'Ceremony title', which
        /// a test reads; the picker's is 'Step title'.
        /// </summary>
        public void NominationTitle(string title, string line, Color colour, bool ceremony)
        {
            if (content == null) return;
            var heading = FlowText(title, ceremony ? 34 : 28, colour);
            heading.gameObject.name = ceremony ? CeremonyTitleName : StepTitleName;
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            if (semibold != null) heading.font = semibold;
            if (!string.IsNullOrEmpty(line)) FlowText(line, 17, Paper);
        }

        /// <summary>A line of the step's body at the stage's note size, named when a test reads it by name.</summary>
        public TMP_Text NominationNote(string words, Color colour, string name = null)
        {
            if (content == null || string.IsNullOrEmpty(words)) return null;
            var text = FlowText(words, 15, colour);
            if (!string.IsNullOrEmpty(name)) text.name = name;
            return text;
        }

        /// <summary>A story sub-step's head: the arc's eyebrow and the beat's name, without the narrative the player has read.</summary>
        public void NominationStepHead(string eyebrow, string title)
        {
            if (content == null) return;
            var mark = DecisionText(content, (eyebrow ?? "STORY").ToUpperInvariant(), 12, UiTheme.Joke);
            mark.characterSpacing = 8f;
            var heading = DecisionText(content, title, 22, Paper);
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            if (semibold != null) heading.font = semibold;
        }

        // ------------------------------------------------------------ the faces

        /// <summary>
        /// The ceremony's faces under a heading, fitted to what the step has left: the ceremony's own
        /// cards (<see cref="CeremonyFaces"/>) when they fit at a size a name reads at, and a grid of
        /// rows - the face beside the name and the pill - when the house is too big for that. Either
        /// way the grid is 'Ceremony faces', each card 'Face · {full name}' with its 'Photo', and a
        /// week's role on a 'Role' pill. Each card wears its Pack 8 frame for the role, and the
        /// player's name under their face says it is theirs.
        /// </summary>
        public void NominationFaces(string heading, IReadOnlyList<CeremonyFace> faces, float widest, Func<string, string> frameFor, string playerId)
        {
            if (content == null || faces == null || faces.Count == 0) return;
            if (!string.IsNullOrEmpty(heading)) Eyebrow(heading, UiTheme.Muted);
            float s = FontScale, fit = Mathf.Max(0f, NominationRoomLeft()), column = ContentWidth();
            FaceGrid(faces.Count, column, widest * s, fit, s, out float width, out int columns);
            int rows = Mathf.CeilToInt(faces.Count / (float)columns);
            float tall = rows * (width * 1.12f + 50f * s) + (rows - 1) * FaceGap * s;
            if (tall <= fit + .5f)
            {
                CeremonyFaces(null, faces, widest, fit);
                SkinFaces(LastColumnRow(CeremonyFacesName), faces, frameFor, playerId);
            }
            else FaceRows(faces, fit, frameFor, playerId);
        }

        /// <summary>The ceremony's own cards, framed for their roles, the player's name marked as theirs; the card keeps the full name it is found by.</summary>
        private void SkinFaces(RectTransform grid, IReadOnlyList<CeremonyFace> faces, Func<string, string> frameFor, string playerId)
        {
            if (grid == null) return;
            float s = FontScale;
            var state = director != null ? director.Snapshot : null;
            for (int i = 0; i < faces.Count && i < grid.childCount; i++)
            {
                var card = (RectTransform)grid.GetChild(i);
                string frame = frameFor != null ? frameFor(faces[i].Id) : null;
                if (!string.IsNullOrEmpty(frame)) EndScreenKit.Frame(card, frame, 10f * s, new Color(Surface.r, Surface.g, Surface.b, .92f));
                var actor = state != null && faces[i].Id == playerId ? state.Find(playerId) : null;
                if (actor == null) continue;
                foreach (var label in card.GetComponentsInChildren<TMP_Text>())
                    if (label.transform.parent == card && label.text == Localisation.Text(actor.name))
                        label.text = Localisation.Text(HudPrimitives.WithYou(actor.name, true));
            }
        }

        /// <summary>The faces as rows, for a house too big for the ceremony's cards in the step's height.</summary>
        private void FaceRows(IReadOnlyList<CeremonyFace> faces, float fit, Func<string, string> frameFor, string playerId)
        {
            float s = FontScale, gap = FaceGap * s, column = ContentWidth();
            SolveCardGrid(faces.Count, column, fit, 150f * s, 300f * s, 56f * s, 92f * s, gap, out float width, out float height, out int columns);
            int rows = Mathf.CeilToInt(faces.Count / (float)columns);
            var grid = new GameObject(CeremonyFacesName, typeof(RectTransform), typeof(GridLayoutGroup), typeof(LayoutElement)).GetComponent<RectTransform>();
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
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            foreach (var face in faces)
            {
                var actor = state != null ? state.Find(face.Id) : null;
                var card = Chrome("Face · " + (actor != null ? actor.name : face.Id), grid);
                string frame = frameFor != null ? frameFor(face.Id) : null;
                if (!string.IsNullOrEmpty(frame)) EndScreenKit.Frame(card, frame, 10f * s, new Color(Surface.r, Surface.g, Surface.b, .92f));
                float side = height - 12f * s, photoWidth = side * .86f;
                var photo = HudPrimitives.RectPortrait(card, "Photo", Portrait(face.Id), actor, new Vector2(photoWidth, side), 6);
                photo.anchorMin = photo.anchorMax = new Vector2(0f, 1f);
                photo.pivot = new Vector2(0f, 1f);
                photo.anchoredPosition = new Vector2(6f * s, -6f * s);
                float x = 6f * s + photoWidth + 10f * s, inner = Mathf.Max(24f, width - x - 6f * s);
                bool role = !string.IsNullOrEmpty(face.Role);
                float nameBox = 15f * 1.3f * s, pill = 17f * s;
                float top = Mathf.Max(4f * s, (height - nameBox - (role ? pill + 4f * s : 0f)) * .5f);
                string shown = actor != null ? HudPrimitives.WithYou(actor.name, actor.id == playerId) : face.Id;
                var name = FixedText(card, shown, 15, Paper, new Vector2(x, -top), new Vector2(inner, nameBox));
                if (semibold != null) name.font = semibold;
                name.textWrappingMode = TextWrappingModes.NoWrap;
                name.overflowMode = TextOverflowModes.Ellipsis;
                AutoSize(name, 10);
                if (!role) continue;
                var tag = Panel("Role", card, face.RoleColour, 4);
                float pillWidth = Mathf.Min(inner, Mathf.Max(46f, face.Role.Length * 7.5f + 16f) * s);
                Anchor(tag, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, -(top + nameBox + 4f * s)), new Vector2(pillWidth, pill));
                tag.GetComponent<Image>().raycastTarget = false;
                var word = FixedText(tag, face.Role, 11, UiTheme.Ink, Vector2.zero, tag.sizeDelta);
                word.alignment = TextAlignmentOptions.Center;
            }
        }

        // ------------------------------------------------------------ a story beat as the step

        /// <summary>
        /// A story beat as the nomination's one step (mockup 72): the arc's eyebrow, the beat's name
        /// and the moment in words down the left, and its options as tiles on the right, two to a
        /// row, so six of them are three rows and the screen still does not scroll. Every tile keeps
        /// its option's own label as its caption, the grid is 'Story choices' inside the stage's
        /// column, and each wears the Pack 8 face its caller chose for the kind of answer.
        /// </summary>
        public void NominationStoryStep(string eyebrow, string title, string narrative, IList<(StoryChoice Choice, string Skin)> choices)
        {
            if (content == null || choices == null) return;
            float s = FontScale, width = ContentWidth(), gutter = 18f * s, spacing = 10f * s;
            float room = Mathf.Max(96f * s, NominationRoomLeft());
            float textWidth = Mathf.Floor(width * .38f - gutter * .5f), tilesWidth = width - textWidth - gutter;
            int count = Mathf.Max(1, choices.Count), columns = count > 1 ? 2 : 1;
            int rows = Mathf.CeilToInt(count / (float)columns);
            float cellWidth = (tilesWidth - spacing * (columns - 1)) / columns;
            float cell = Mathf.Clamp((room - (rows - 1) * spacing) / rows, 64f * s, 96f * s);
            float tilesHeight = rows * cell + (rows - 1) * spacing;

            var row = new GameObject(StoryStepName, typeof(RectTransform), typeof(LayoutElement)).GetComponent<RectTransform>();
            row.SetParent(content, false);
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            float markBox = 12f * 1.3f * s, y = 0f;
            var mark = FixedText(row, (eyebrow ?? "STORY").ToUpperInvariant(), 12, UiTheme.Joke, Vector2.zero, new Vector2(textWidth, markBox));
            mark.characterSpacing = 8f;
            mark.textWrappingMode = TextWrappingModes.NoWrap;
            mark.overflowMode = TextOverflowModes.Ellipsis;
            y += markBox + 6f * s;
            var heading = NewText(row, title, 22, Paper);
            if (semibold != null) heading.font = semibold;
            float headingLine = 22f * 1.3f * s;
            float headingHeight = Mathf.Clamp(Mathf.Ceil(heading.GetPreferredValues(heading.text, textWidth, 0f).y) + 2f, headingLine, headingLine * 2f);
            heading.overflowMode = TextOverflowModes.Ellipsis;
            Anchor(heading.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -y), new Vector2(textWidth, headingHeight));
            y += headingHeight + 8f * s;
            var story = NewText(row, narrative, 16, UiTheme.Muted);
            story.fontStyle = FontStyles.Italic;
            float storyLine = 16f * 1.3f * s;
            float storyHeight = Mathf.Max(storyLine, Mathf.Ceil(story.GetPreferredValues(story.text, textWidth, 0f).y) + 2f);
            // As tall as the tiles or the words, whichever is taller, and never past the step's room;
            // words longer than that step down a size or two and end in an ellipsis.
            float height = Mathf.Max(tilesHeight, Mathf.Min(room, y + storyHeight));
            if (y + storyHeight > height)
            {
                storyHeight = Mathf.Max(storyLine, height - y);
                AutoSize(story, 12);
                story.overflowMode = TextOverflowModes.Ellipsis;
            }
            Anchor(story.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -y), new Vector2(textWidth, storyHeight));
            var element = row.GetComponent<LayoutElement>();
            element.minHeight = element.preferredHeight = height;

            var grid = new GameObject(StoryChoicesName, typeof(RectTransform), typeof(GridLayoutGroup)).GetComponent<RectTransform>();
            grid.SetParent(row, false);
            Anchor(grid, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(textWidth + gutter, 0f), new Vector2(tilesWidth, tilesHeight));
            var layout = grid.GetComponent<GridLayoutGroup>();
            layout.cellSize = new Vector2(cellWidth, cell);
            layout.spacing = new Vector2(spacing, spacing);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = columns;
            layout.childAlignment = TextAnchor.UpperLeft;
            foreach (var choice in choices) StoryTile(grid, choice.Choice, choice.Skin, cellWidth, cell);
        }

        /// <summary>
        /// One option's tile: the glyph for the kind of answer, the caption the control is known by,
        /// what it means, and a foot with the odds or anything else to know first and the risk as a
        /// coloured word - the word as well as the colour. A locked option is drawn and cannot be
        /// pressed, with its reason on it.
        /// </summary>
        private void StoryTile(RectTransform grid, StoryChoice choice, string skin, float cellWidth, float cellHeight)
        {
            float s = FontScale;
            var emphasis = choice.Locked ? UiTheme.Emphasis.Resting : UiTheme.Emphasis.Interactive;
            var rect = Chrome(choice.Caption, grid, emphasis);
            HudEmphasis.Promote(rect, emphasis);
            if (!choice.Locked && !string.IsNullOrEmpty(skin))
                EndScreenKit.Frame(rect, skin, 10f * s, new Color(Surface.r, Surface.g, Surface.b, .92f));
            var button = Pressable(rect, choice.Choose);
            button.interactable = !choice.Locked;
            var colours = button.colors;
            colours.highlightedColor = new Color(1.15f, 1.15f, 1.15f);
            colours.selectedColor = colours.highlightedColor;
            button.colors = colours;

            float badge = Mathf.Min(40f * s, cellHeight - 24f * s);
            var tile = Panel("Choice tile", rect, UiTheme.SurfaceRaised, 8);
            Anchor(tile, new Vector2(0, 1), new Vector2(0, 1), new Vector2(12f * s, -12f * s), new Vector2(badge, badge));
            tile.GetComponent<Image>().raycastTarget = false;
            HudPrimitives.Glyph("Choice mark", tile, choice.Locked ? "lock" : ChoiceGlyph(choice.Caption), Accent,
                new Vector2(badge * .2f, -badge * .2f), badge * .6f);

            float text = 12f * s + badge + 12f * s, captionBox = 16f * 1.3f * s, footBox = 12f * 1.3f * s;
            var caption = NewText(rect, choice.Caption, 16, choice.Locked ? UiTheme.Muted : Paper);
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            if (semibold != null) caption.font = semibold;
            AutoSize(caption, 11);
            Anchor(caption.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(text, -8f * s), new Vector2(cellWidth - text - 12f * s, captionBox));
            float descriptionTop = 8f * s + captionBox + 2f * s;
            float descriptionHeight = cellHeight - descriptionTop - footBox - 12f * s;
            if (!string.IsNullOrEmpty(choice.Description) && descriptionHeight >= 13f * 1.3f * s)
            {
                var line = NewText(rect, choice.Description, 13, UiTheme.Muted);
                AutoSize(line, 10);
                line.overflowMode = TextOverflowModes.Ellipsis;
                Anchor(line.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(text, -descriptionTop),
                    new Vector2(cellWidth - text - 12f * s, descriptionHeight));
            }
            // The foot: the odds and the note on the left, never in the caption, which is how a test
            // and a screen reader find the control; the risk word on the right.
            float riskWidth = 78f * s;
            var riskWord = FixedText(rect, choice.Risk, 12, RiskTint(choice.Risk), Vector2.zero, new Vector2(riskWidth, footBox));
            Anchor(riskWord.rectTransform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-10f * s, 6f * s), new Vector2(riskWidth, footBox));
            riskWord.alignment = TextAlignmentOptions.Right;
            string foot = string.Join("  ·  ", new[] { choice.Odds, choice.Note }.Where(x => !string.IsNullOrEmpty(x)));
            if (foot.Length == 0) return;
            var odds = NewText(rect, foot, 12, choice.Locked ? UiTheme.Muted : Accent);
            AutoSize(odds, 9);
            odds.textWrappingMode = TextWrappingModes.NoWrap;
            odds.overflowMode = TextOverflowModes.Ellipsis;
            Anchor(odds.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(text, 6f * s),
                new Vector2(Mathf.Max(24f, cellWidth - text - riskWidth - 20f * s), footBox));
        }

        /// <summary>
        /// A story option's "who?": the people it can name as a grid of faces and names, each named
        /// and captioned by the name, fitted to the step's room less <paramref name="reserveBelow"/>
        /// for the rows after it. It was a column of up to fifteen rows, each with the player's own
        /// trust reading, so each cell keeps that reading under the name.
        /// </summary>
        public void NominationPeople(IList<(string Id, string Caption, Action Press)> people, float reserveBelow)
        {
            if (content == null || people == null || people.Count == 0) return;
            float s = FontScale, gap = 10f * s, column = ContentWidth();
            float fit = Mathf.Max(0f, NominationRoomLeft() - reserveBelow);
            SolveCardGrid(people.Count, column, fit, 200f * s, 320f * s, 52f * s, 64f * s, gap, out float width, out float height, out int columns);
            int rows = Mathf.CeilToInt(people.Count / (float)columns);
            var grid = new GameObject(StoryPeopleName, typeof(RectTransform), typeof(GridLayoutGroup), typeof(LayoutElement)).GetComponent<RectTransform>();
            grid.SetParent(content, false);
            var layout = grid.GetComponent<GridLayoutGroup>();
            layout.cellSize = new Vector2(width, height);
            layout.spacing = new Vector2(gap, gap);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = columns;
            layout.childAlignment = TextAnchor.UpperLeft;
            var size = grid.GetComponent<LayoutElement>();
            size.minHeight = size.preferredHeight = rows * height + (rows - 1) * gap;
            var state = director != null ? director.Snapshot : null;
            foreach (var person in people)
            {
                var cell = Chrome(person.Caption, grid, UiTheme.Emphasis.Interactive);
                HudEmphasis.Promote(cell, UiTheme.Emphasis.Interactive);
                Pressable(cell, person.Press);
                var actor = state != null ? state.Find(person.Id) : null;
                float side = height - 10f * s, photoWidth = side * .86f;
                var photo = HudPrimitives.RectPortrait(cell, "Photo", Portrait(person.Id), actor, new Vector2(photoWidth, side), 6);
                photo.anchorMin = photo.anchorMax = new Vector2(0f, 1f);
                photo.pivot = new Vector2(0f, 1f);
                photo.anchoredPosition = new Vector2(5f * s, -5f * s);
                float x = 5f * s + photoWidth + 10f * s, box = 16f * 1.3f * s, line = 12f * 1.3f * s, inner = Mathf.Max(24f, width - x - 8f * s);
                // The player's own reading of them, as the rows this grid replaced carried it: the
                // standing word and the trust number, the player's record and never theirs back.
                bool reading = actor != null && actor.id != state.playerId;
                float top = Mathf.Max(4f * s, (height - box - (reading ? 2f * s + line : 0f)) * .5f);
                var name = FixedText(cell, person.Caption, 16, Paper, new Vector2(x, -top), new Vector2(inner, box));
                name.textWrappingMode = TextWrappingModes.NoWrap;
                name.overflowMode = TextOverflowModes.Ellipsis;
                AutoSize(name, 11);
                if (!reading) continue;
                var kind = RelationshipWeb.KindOf(state, person.Id);
                var words = FixedText(cell, RelationshipWeb.StandingWord(kind) + " · Trust " + state.Score(state.playerId, person.Id).ToString("0"), 12,
                    kind == RelationshipWeb.Kind.Neutral ? UiTheme.Muted : RelationshipWeb.StandingColour(kind),
                    new Vector2(x, -(top + box + 2f * s)), new Vector2(inner, line));
                words.name = "Reading";
                words.textWrappingMode = TextWrappingModes.NoWrap;
                words.overflowMode = TextOverflowModes.Ellipsis;
                AutoSize(words, 10);
            }
        }

        // ------------------------------------------------------------ the Head of Household's picker

        /// <summary>
        /// The player Head of Household's step (mockup 72, PACK8-PASS-PLAN B1): the candidates as
        /// cards in 'Nominee grid', named and captioned by their names, sized to the step's room -
        /// as cards while a card reads at nine tenths of the text size or more, as rows of a face
        /// and a name beside the player's own reading when the house is too big for that. A pick
        /// lights its card in place and nothing is rebuilt, so the control a test or the keyboard
        /// holds stays the one it held; the picks are handed to <paramref name="picked"/>, which
        /// keeps them across a render. The commit is pinned as the way on, and the comparison is
        /// the footer's secondary slot, swapping the grid for the two records in place.
        /// </summary>
        public void NominationPicker(Option[] options, string firstPicked, string secondPicked, Action<string, string> picked,
            Action<string, string> commit, string commitCaption, string aimLine, Action planBackdoor)
        {
            if (content == null || options == null) return;
            var state = director != null ? director.Snapshot : null;
            string first = options.Any(o => o.Id == firstPicked) ? firstPicked : null;
            string second = options.Any(o => o.Id == secondPicked) && secondPicked != first ? secondPicked : null;
            float s = FontScale;
            NominationTitle(NominationSteps.PickerTitle, null, UiTheme.Gold, false);
            string Label(string id) => id == null ? "—" : Array.Find(options, o => o.Id == id).Label ?? "—";
            string Selected() => first == null && second == null ? "Choose two houseguests below." : "Selected: " + Label(first) + " and " + Label(second);
            var selection = FlowText(Selected(), 17, Accent);
            selection.name = SelectedNomineesName;
            // Same framing the notebook uses, so a trust number is never mistaken for fact.
            FlowText("Trust readings are your own perspective; another housemate may feel differently.", 13, UiTheme.Muted);
            if (!string.IsNullOrEmpty(aimLine)) FlowText(aimLine, 15, UiTheme.Gold);

            var columnLayout = content.GetComponent<VerticalLayoutGroup>();
            float planHeight = 44f * s, spacing = columnLayout != null ? columnLayout.spacing : 12f;
            float fit = Mathf.Max(0f, NominationRoomLeft() - (planBackdoor != null ? planHeight + spacing : 0f));
            float width = ContentWidth(), gap = 10f * s;
            int count = Mathf.Max(1, options.Length);
            // As cards while one reads at nine tenths of the text size: the largest the column and the
            // room allow, tried at every number of rows. Past that, rows at the text size itself.
            float scale = 0f; int tallColumns = 1;
            for (int rows = 1; rows <= count; rows++)
            {
                int across = Mathf.CeilToInt(count / (float)rows);
                float byWidth = (width - (across - 1) * gap) / (across * NomineeCardWidth);
                float byHeight = (fit - (rows - 1) * gap) / (rows * NomineeCardHeight);
                float fits = Mathf.Min(s, Mathf.Min(byWidth, byHeight));
                if (fits > scale) { scale = fits; tallColumns = across; }
            }
            bool cards = scale >= .9f * s;
            float cellWidth, cellHeight; int columns;
            if (cards) { cellWidth = NomineeCardWidth * scale; cellHeight = NomineeCardHeight * scale; columns = tallColumns; }
            else SolveCardGrid(count, width, fit, 168f * s, 320f * s, PickerRowShortest * s, 104f * s, gap, out cellWidth, out cellHeight, out columns);
            int gridRows = Mathf.CeilToInt(options.Length / (float)columns);

            var grid = new GameObject(NomineeGridName, typeof(RectTransform), typeof(GridLayoutGroup), typeof(LayoutElement)).GetComponent<RectTransform>();
            grid.SetParent(content, false);
            var layout = grid.GetComponent<GridLayoutGroup>();
            layout.cellSize = new Vector2(cellWidth, cellHeight);
            layout.spacing = new Vector2(gap, gap);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = columns;
            layout.childAlignment = TextAnchor.UpperCenter;
            var size = grid.GetComponent<LayoutElement>();
            size.minHeight = size.preferredHeight = Mathf.Max(1, gridRows) * cellHeight + Mathf.Max(0, gridRows - 1) * gap;

            var comparison = DecisionColumn(CandidateContextName, content);
            comparison.gameObject.SetActive(false);
            bool expanded = false;
            Action refresh = () =>
            {
                if (comparison == null) return; // Ignore a detached control from an older HUD revision.
                foreach (Transform child in comparison)
                { child.gameObject.SetActive(false); Destroy(child.gameObject); }
                comparison.gameObject.SetActive(expanded);
                if (!expanded) return;
                DecisionText(comparison, "PUBLIC RECORD · YOUR PERSPECTIVE", 17, Accent);
                if (first == null && second == null)
                { DecisionText(comparison, "Choose up to two candidates to compare their records.", 17, Paper); return; }
                var records = DecisionColumns("Candidate records", comparison);
                foreach (string id in new[] { first, second }.Where(id => id != null))
                {
                    var candidate = state != null ? DecisionContext.ForCandidate(state, id) : null;
                    if (candidate == null) continue;
                    var card = DecisionColumn("Candidate context " + id, records, true);
                    var cardWidth = card.gameObject.AddComponent<LayoutElement>();
                    cardWidth.minWidth = cardWidth.preferredWidth = 0f; cardWidth.flexibleWidth = 1f;
                    DecisionIdentity(card, candidate.Character, 78f, 18);
                    DecisionText(card, candidate.Record, 17, Paper);
                    DecisionText(card, candidate.Relationship, 17, Paper);
                    DecisionText(card, candidate.Promises, 16, UiTheme.Muted);
                    if (!string.IsNullOrEmpty(candidate.Deals)) DecisionText(card, candidate.Deals, 16, UiTheme.Muted);
                }
            };

            var built = new Dictionary<string, RectTransform>();
            var names = new List<TMP_Text>();
            foreach (var option in options)
            {
                var captured = option;
                Action press = () =>
                {
                    if (first == captured.Id) first = null;
                    else if (second == captured.Id) second = null;
                    else if (first == null) first = captured.Id;
                    else second = captured.Id;
                    selection.text = Localisation.Text(Selected());
                    foreach (var pair in built) MarkNominee(pair.Value, pair.Key == first || pair.Key == second);
                    picked?.Invoke(first, second);
                    refresh();
                };
                built[option.Id] = cards ? PickerCard(grid, option, state, scale, press, names) : PickerRow(grid, option, state, cellWidth, cellHeight, press, names);
            }
            // One size of name across the grid: the largest every name fits its box at.
            OneSize(names);
            foreach (var pair in built) if (pair.Key == first || pair.Key == second) MarkNominee(pair.Value, true);

            RectTransform planRow = null;
            if (planBackdoor != null)
            {
                planRow = new GameObject(PlanBackdoorRowName, typeof(RectTransform), typeof(LayoutElement)).GetComponent<RectTransform>();
                planRow.SetParent(content, false);
                var element = planRow.GetComponent<LayoutElement>();
                element.minHeight = element.preferredHeight = planHeight;
                FixedButton(planRow, PlanBackdoorCaption, Vector2.zero, new Vector2(Mathf.Min(width, 300f * s), planHeight), planBackdoor);
            }

            WearNominationPrimary(PinnedAction(commitCaption, () => commit(first, second)));
            Button toggle = null;
            toggle = PinnedSecondary(ShowCandidateContextCaption, () =>
            {
                expanded = !expanded;
                toggle.name = expanded ? HideCandidateContextCaption : ShowCandidateContextCaption;
                var words = toggle.GetComponentInChildren<TMP_Text>();
                if (words != null) words.text = toggle.name;
                // The records take the grid's place, so the step still fits; the cards are kept,
                // hidden, and come back with the picks they had.
                grid.gameObject.SetActive(!expanded);
                if (planRow != null) planRow.gameObject.SetActive(!expanded);
                refresh();
                LayoutStrategyFooter();
                // The ring holds the cards just hidden; it is wired again from what is on screen.
                restoreSelection = true;
            });
        }

        /// <summary>A candidate's card shell: named and captioned by the name, framed in Pack 8's houseguest card, with its selected frame waiting behind.</summary>
        private RectTransform PickerShell(RectTransform grid, Option option, Action press)
        {
            float s = FontScale;
            var rect = Chrome(option.Label, grid, UiTheme.Emphasis.Interactive);
            HudEmphasis.Promote(rect, UiTheme.Emphasis.Interactive);
            var ground = new Color(Surface.r, Surface.g, Surface.b, .92f);
            EndScreenKit.Frame(rect, PackArt.Pack8HouseguestNeutral, 10f * s, ground);
            var chosen = EndScreenKit.Frame(rect, PackArt.Pack8HouseguestSelected, 10f * s, ground, UiTheme.Accent);
            chosen.name = PickedArtName;
            chosen.transform.SetSiblingIndex(1);
            chosen.gameObject.SetActive(false);
            var button = Pressable(rect, press);
            var colours = button.colors;
            colours.highlightedColor = new Color(1.15f, 1.15f, 1.15f);
            colours.selectedColor = colours.highlightedColor;
            button.colors = colours;
            return rect;
        }

        /// <summary>A pick's check and lit edge, as the band's cards have them, and Pack 8's selected frame.</summary>
        private static void MarkNominee(RectTransform card, bool picked)
        {
            if (card == null) return;
            MarkPicked(card, picked);
            var art = card.Find(PickedArtName);
            if (art != null) art.gameObject.SetActive(picked);
        }

        /// <summary>The week's role on a photo's foot: the crown, the veto, the block.</summary>
        private void PickerRole(RectTransform photo, EpisodeState state, string id, float scale)
        {
            var role = state != null ? RoleOf(state, id) : HudPrimitives.RoleMark.None;
            if (role == HudPrimitives.RoleMark.None) return;
            var pill = Panel("Role", photo, role == HudPrimitives.RoleMark.Nominee ? UiTheme.Danger : UiTheme.Gold, 4);
            Anchor(pill, new Vector2(.5f, 0f), new Vector2(.5f, 0f), new Vector2(0f, 4f * scale), new Vector2(46f * scale, 15f * scale));
            pill.GetComponent<Image>().raycastTarget = false;
            var word = SizedText(pill, role == HudPrimitives.RoleMark.HeadOfHousehold ? "HOH"
                : role == HudPrimitives.RoleMark.VetoHolder ? "VETO" : "NOM", 10f * scale, UiTheme.Ink, 8f);
            Anchor(word.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, pill.sizeDelta);
            word.alignment = TextAlignmentOptions.Center;
        }

        /// <summary>
        /// Where the player stands with a candidate, in their own words: the standing word, the trust
        /// track and the reading - the player's own record, never what the houseguest thinks back.
        /// </summary>
        private void PickerStanding(RectTransform card, EpisodeState state, string id, float x, float y, float width, float scale, bool centred)
        {
            var kind = RelationshipWeb.KindOf(state, id);
            var tint = kind == RelationshipWeb.Kind.Neutral ? UiTheme.Muted : RelationshipWeb.StandingColour(kind);
            var standing = SizedText(card, RelationshipWeb.StandingWord(kind), 12f * scale, tint, 9f);
            Anchor(standing.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(x, -y), new Vector2(width, 16f * scale));
            standing.alignment = centred ? TextAlignmentOptions.Center : TextAlignmentOptions.Left;
            y += 18f * scale;
            double trust = state.Score(state.playerId, id);
            var track = Panel("Trust track", card, new Color(UiTheme.Outline.r, UiTheme.Outline.g, UiTheme.Outline.b, .55f), 2);
            Anchor(track, new Vector2(0, 1), new Vector2(0, 1), new Vector2(x + (centred ? 4f * scale : 0f), -y), new Vector2(width - (centred ? 8f * scale : 0f), 4f * scale));
            track.GetComponent<Image>().raycastTarget = false;
            float fill = Mathf.Clamp01((float)(trust + 100.0) / 200f);
            if (fill > 0f)
            {
                var bar = Panel("Trust fill", card, trust > 5 ? UiTheme.Allied : trust < -5 ? UiTheme.Conflict : UiTheme.Muted, 2);
                Anchor(bar, new Vector2(0, 1), new Vector2(0, 1), track.anchoredPosition, new Vector2(track.sizeDelta.x * fill, 4f * scale));
                bar.GetComponent<Image>().raycastTarget = false;
            }
            y += 7f * scale;
            var reading = SizedText(card, "Trust " + trust.ToString("0"), 11f * scale, UiTheme.Muted, 9f);
            Anchor(reading.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(x, -y), new Vector2(width, 15f * scale));
            reading.alignment = centred ? TextAlignmentOptions.Center : TextAlignmentOptions.Left;
        }

        /// <summary>The name on a candidate's card or row, so a test can read it whole.</summary>
        public const string PickerNameName = "Name";

        /// <summary>
        /// Draws a set of names at one size: the largest at which every one of them fits its box,
        /// never above their type (as KeyCeremony's roster does). Each label auto-sizes to its own
        /// box, which drew "Avery Thomp…" at ten points beside "Taylor Kim" at fifteen on one grid
        /// (nomination-picker-16; UI-UX-PASS-PLAN T0). Each is fitted on its own first and every
        /// box then takes the smallest of those fits as its ceiling, so a name only ever shrinks,
        /// and the whole set shrinks together.
        /// </summary>
        private static void OneSize(List<TMP_Text> names)
        {
            float size = float.MaxValue;
            foreach (var name in names)
            {
                if (name == null) continue;
                name.ForceMeshUpdate(true);
                size = Mathf.Min(size, name.fontSize);
            }
            if (size == float.MaxValue) return;
            foreach (var name in names)
            {
                if (name == null) continue;
                name.fontSizeMax = size;
                name.fontSize = size;
            }
        }

        /// <summary>A candidate as a card (mockup-09), scaled as a whole to <paramref name="scale"/>: the photo, the name under it, and the player's own reading.</summary>
        private RectTransform PickerCard(RectTransform grid, Option option, EpisodeState state, float scale, Action press, List<TMP_Text> names)
        {
            var rect = PickerShell(grid, option, press);
            var actor = state != null ? state.Find(option.Id) : null;
            float width = NomineeCardWidth * scale;
            var photo = HudPrimitives.RectPortrait(rect, "Photo", Portrait(option.Id), actor, new Vector2(width - 12f * scale, 96f * scale), 7);
            photo.anchorMin = photo.anchorMax = new Vector2(.5f, 1f);
            photo.pivot = new Vector2(.5f, 1f);
            photo.anchoredPosition = new Vector2(0f, -6f * scale);
            PickerRole(photo, state, option.Id, scale);
            var name = SizedText(rect, option.Label, 14f * scale, Paper, 10f);
            name.name = PickerNameName;
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            if (semibold != null) name.font = semibold;
            name.alignment = TextAlignmentOptions.Top;
            name.overflowMode = TextOverflowModes.Ellipsis;
            Anchor(name.rectTransform, new Vector2(.5f, 1f), new Vector2(.5f, 1f), new Vector2(0f, -106f * scale), new Vector2(width - 10f * scale, 34f * scale));
            names?.Add(name);
            if (actor != null && state != null) PickerStanding(rect, state, option.Id, 8f * scale, 142f * scale, width - 16f * scale, scale, true);
            return rect;
        }

        /// <summary>A candidate as a row, for a house too big for cards: the photo on the left, the name and the player's own reading beside it, at the text size.</summary>
        private RectTransform PickerRow(RectTransform grid, Option option, EpisodeState state, float width, float height, Action press, List<TMP_Text> names)
        {
            var rect = PickerShell(grid, option, press);
            var actor = state != null ? state.Find(option.Id) : null;
            float s = FontScale, side = height - 12f * s, photoWidth = side * .86f;
            var photo = HudPrimitives.RectPortrait(rect, "Photo", Portrait(option.Id), actor, new Vector2(photoWidth, side), 6);
            photo.anchorMin = photo.anchorMax = new Vector2(0f, 1f);
            photo.pivot = new Vector2(0f, 1f);
            photo.anchoredPosition = new Vector2(6f * s, -6f * s);
            PickerRole(photo, state, option.Id, s);
            // Clear of the pick's check in the corner.
            float x = 6f * s + photoWidth + 10f * s, inner = Mathf.Max(24f, width - x - 34f * s);
            // The name, then the reading: its word (18), its track (7) and its number (15) when the
            // row has the height, else the word and the number on one line. The name takes two
            // lines where the row holds them rather than ending in an ellipsis, and the lines it
            // takes decide the block, so a one-line name leaves no blank line over its reading.
            bool full = height >= PickerRowFull * s;
            float line = 15f * 1.3f * s, readingBlock = (full ? 40f : 16f) * s;
            int lines = height >= 2f * line + 2f * s + readingBlock + 8f * s ? 2 : 1;
            var name = FixedText(rect, option.Label, 15, Paper, new Vector2(x, 0f), new Vector2(inner, lines * line));
            name.name = PickerNameName;
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            if (semibold != null) name.font = semibold;
            name.textWrappingMode = lines > 1 ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            name.overflowMode = TextOverflowModes.Ellipsis;
            name.alignment = TextAlignmentOptions.TopLeft;
            AutoSize(name, 10);
            names?.Add(name);
            name.ForceMeshUpdate(true);
            float nameBox = Mathf.Clamp(name.textInfo.lineCount, 1, lines) * line, block = nameBox + 2f * s + readingBlock;
            float y = Mathf.Max(4f * s, (height - block) * .5f);
            Anchor(name.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(x, -y), new Vector2(inner, nameBox));
            if (actor == null || state == null) return rect;
            y += nameBox + 2f * s;
            if (full) { PickerStanding(rect, state, option.Id, x, y, inner, s, false); return rect; }
            var kind = RelationshipWeb.KindOf(state, option.Id);
            var standing = SizedText(rect, RelationshipWeb.StandingWord(kind), 12f * s,
                kind == RelationshipWeb.Kind.Neutral ? UiTheme.Muted : RelationshipWeb.StandingColour(kind), 9f);
            Anchor(standing.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(x, -y), new Vector2(inner * .5f, 16f * s));
            var reading = SizedText(rect, "Trust " + state.Score(state.playerId, option.Id).ToString("0"), 11f * s, UiTheme.Muted, 9f);
            Anchor(reading.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(x + inner * .5f, -y), new Vector2(inner * .5f, 16f * s));
            reading.alignment = TextAlignmentOptions.Right;
            return rect;
        }

        /// <summary>The height, at the resting text size, from which a candidate's row has room for the trust track as well as the reading.</summary>
        private const float PickerRowFull = 76f;
        /// <summary>The shortest a candidate's row goes: a name over the standing word and the reading, on one line.</summary>
        private const float PickerRowShortest = 62f;

        // ------------------------------------------------------------ the backdoor view

        /// <summary>
        /// The picker's backdoor view: what a plan is, and one "Aim this week at {name}" per
        /// candidate as a grid, each with the category tag it always carried. The way back is the
        /// footer's secondary slot.
        /// </summary>
        public void NominationBackdoor(string line, IList<(string Id, string Caption, Action Press, string Tag)> aims)
        {
            if (content == null || aims == null) return;
            NominationTitle("A backdoor plan", line, UiTheme.Gold, false);
            if (aims.Count == 0) return;
            float s = FontScale, gap = 10f * s, column = ContentWidth();
            SolveCardGrid(aims.Count, column, Mathf.Max(0f, NominationRoomLeft()), 240f * s, 420f * s, 56f * s, 72f * s, gap,
                out float width, out float height, out int columns);
            int rows = Mathf.CeilToInt(aims.Count / (float)columns);
            var grid = new GameObject(BackdoorAimsName, typeof(RectTransform), typeof(GridLayoutGroup), typeof(LayoutElement)).GetComponent<RectTransform>();
            grid.SetParent(content, false);
            var layout = grid.GetComponent<GridLayoutGroup>();
            layout.cellSize = new Vector2(width, height);
            layout.spacing = new Vector2(gap, gap);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = columns;
            layout.childAlignment = TextAnchor.UpperLeft;
            var size = grid.GetComponent<LayoutElement>();
            size.minHeight = size.preferredHeight = rows * height + (rows - 1) * gap;
            foreach (var aim in aims)
            {
                var cell = Chrome(aim.Caption, grid, UiTheme.Emphasis.Interactive);
                HudEmphasis.Promote(cell, UiTheme.Emphasis.Interactive);
                var button = Pressable(cell, aim.Press);
                float box = 16f * 1.3f * s;
                var words = FixedText(cell, aim.Caption, 16, Paper, new Vector2(12f * s, -8f * s), new Vector2(Mathf.Max(24f, width - 24f * s), box));
                words.textWrappingMode = TextWrappingModes.NoWrap;
                words.overflowMode = TextOverflowModes.Ellipsis;
                AutoSize(words, 11);
                Tag(button, aim.Tag, TagSeat.CardFoot);
            }
        }
    }
}
