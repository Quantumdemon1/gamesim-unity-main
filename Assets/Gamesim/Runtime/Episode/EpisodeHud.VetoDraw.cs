using System.Collections.Generic;
using Gamesim.Presentation;
using Gamesim.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gamesim.Episode
{
    /// <summary>
    /// The veto's player selection on one screen (PACK8-PASS-PLAN B2, the owner's mockup 76). It was
    /// a column seven hundred units tall - the house's status, a week eyebrow, a 34-point title, a
    /// two-line paragraph, the three playing by right, a 150-unit bag, then the rest of the house -
    /// and scrolled in a stage of about 465 (the owner's screenshots 71 and 73).
    ///
    /// <para>Now it is one row across the frame's whole width: AUTOMATICALLY PLAYING, the Head of
    /// Household and both nominees with their HOH and NOM pills; the bag, with a chip for each seat
    /// the draw fills; and ELIGIBLE FOR DRAW, the rest of the house with no pill. The cards are sized
    /// from how many there are and the height the step has left, so a house of sixteen fits as a
    /// house of seven does. Every name a test finds a part by is the one it had: 'Ceremony title',
    /// 'Face · {name}', 'Role', 'Chip bag' and 'Chip'.</para>
    ///
    /// <para>Nothing on the row is a control. The way on is the panel's own "Continue episode",
    /// which is the draw, dressed as the mockup's 'Reveal the draw' with a headline beside its
    /// caption, never in it (PACK8-PASS-PLAN decision 4).</para>
    /// </summary>
    public sealed partial class EpisodeHud
    {
        /// <summary>The draw's parts, by the names a test finds them by.</summary>
        public const string VetoDrawBoardName = "Veto draw", VetoDrawLineName = "Draw line", DrawPlayingName = "Playing by right",
            DrawColumnName = "Draw column", DrawEligibleName = "Eligible for draw", WayOnHeadlineName = "Way on headline";

        /// <summary>How wide the bag's column stands, and the room a chevron takes between two columns, at the resting text size.</summary>
        private const float DrawColumnWidth = 190f, DrawSeparator = 44f;
        /// <summary>The widest a card on the draw runs, at the resting text size: the mockup's.</summary>
        private const float DrawCardWidest = 150f;
        /// <summary>A column's heading, its rule, and the gap under them before the cards.</summary>
        private const float DrawHeadingBox = 20f, DrawCardsTop = 34f;

        /// <summary>
        /// Gives the strategy stage's rows the frame's whole width rather than the reading column's,
        /// for a screen laid out for the frame (PACK8-PASS-PLAN wave B): the draw's row is the width
        /// of Pack 8's shell, as the mockup draws it. Called before anything is built, so every row
        /// measures the column it is laid in. The footer follows the column, so it runs as wide.
        /// </summary>
        public void StrategyWholeWidth()
        {
            if (modal == null || activityLayout != ActivityLayout.Strategy) return;
            UncapContent();
        }

        /// <summary>
        /// The height of the strategy stage's scroll once its footer is pinned under it: the frame,
        /// less the phase band and any header rows over it, less the footer's row. Worked out rather
        /// than read, because the footer is pinned after the rows it stands under are built.
        /// </summary>
        private float StrategyBodyHeight()
        {
            if (modal == null) return 0f;
            float top = StrategyBandFoot + strategyHeaderUsed;
            float foot = PinnedMargin + pinnedNoteHeight + PinnedHeight * FontScale + 10f;
            return modal.rect.height - top - foot;
        }

        /// <summary>
        /// The draw's head: the ceremony's name small, under the name a test has always found it by,
        /// over the one line that says who plays. The week and the phase are the header's already.
        /// </summary>
        public void VetoDrawHead(string title, string line)
        {
            if (content == null) return;
            var heading = FlowText(title, 22, UiTheme.Gold);
            heading.gameObject.name = CeremonyTitleName;
            heading.alignment = TextAlignmentOptions.Center;
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            if (semibold != null) heading.font = semibold;
            if (string.IsNullOrEmpty(line)) return;
            var words = FlowText(line, 19, Paper);
            words.gameObject.name = VetoDrawLineName;
            words.alignment = TextAlignmentOptions.Center;
        }

        /// <summary>
        /// The draw as one row: <paramref name="playing"/> on the left with their pills, the bag with
        /// <paramref name="toDraw"/> chips in the middle, and <paramref name="eligible"/> on the right
        /// with none. With nobody eligible - everyone plays - the bag stands empty at the row's end
        /// and there is no third column. The row takes the height the step has left under what the
        /// column already holds, so the screen does not scroll; a card is never narrower than
        /// <see cref="FaceCardFloor"/>, and past that the row gives up the height rather than a name.
        /// </summary>
        public RectTransform VetoDrawBoard(IReadOnlyList<CeremonyFace> playing, IReadOnlyList<CeremonyFace> eligible, int toDraw,
            string playingHeading, string drawHeading, string chipsLine, string eligibleHeading)
        {
            if (content == null || playing == null || playing.Count == 0) return null;
            float s = FontScale, gap = FaceGap * s, sep = DrawSeparator * s, drawWidth = DrawColumnWidth * s;
            float width = ContentWidth(), top = DrawCardsTop * s;
            int others = eligible != null ? eligible.Count : 0;

            // What the step has left under the rows above it, measured as the column lays them out.
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            var column = content.GetComponent<VerticalLayoutGroup>();
            float spacing = column != null ? column.spacing : 12f;
            float budget = StrategyBodyHeight() - content.rect.height - spacing - 2f;
            float floorCard = FaceCardFloor * s * 1.12f + 50f * s;
            float cardsHeight = Mathf.Max(floorCard, budget - top);

            float playingWidth, eligibleWidth = 0f, playingCard, eligibleCard = 0f;
            int playingColumns, eligibleColumns = 0;
            if (others > 0)
            {
                // The three by right in one row. The pool beside them in the same band when its cards
                // still read comfortably there - the mockup's one row - and otherwise in as many rows
                // as the height holds. Either way the pair of widths whose smaller card is largest,
                // the cards by right never smaller than the pool's: they are the ones the draw is about.
                float room = width - 2f * sep - drawWidth;
                float best = SplitDraw(playing.Count, others, room, cardsHeight, true, out playingCard, out eligibleCard, out eligibleColumns);
                if (best < DrawBandComfort * s)
                    best = SplitDraw(playing.Count, others, room, cardsHeight, false, out playingCard, out eligibleCard, out eligibleColumns);
                if (best < 0f)
                {
                    // Narrower than any split: the floor on both sides, and the row runs long.
                    playingCard = eligibleCard = FaceCardFloor * s;
                    eligibleColumns = Mathf.Max(1, Mathf.FloorToInt((width * .4f + gap) / (eligibleCard + gap)));
                }
                playingColumns = playing.Count;
                eligibleWidth = eligibleColumns * eligibleCard + (eligibleColumns - 1) * gap;
            }
            else
            {
                FaceGrid(playing.Count, width - sep - drawWidth, DrawCardWidest * s, cardsHeight, s, out playingCard, out playingColumns);
            }
            playingWidth = playingColumns * playingCard + (playingColumns - 1) * gap;
            int playingRows = Mathf.CeilToInt(playing.Count / (float)playingColumns);
            int eligibleRows = others > 0 ? Mathf.CeilToInt(others / (float)eligibleColumns) : 0;
            float playingTall = playingRows * CardHeight(playingCard) + (playingRows - 1) * gap;
            float eligibleTall = eligibleRows > 0 ? eligibleRows * CardHeight(eligibleCard) + (eligibleRows - 1) * gap : 0f;

            // The bag's column: the bag, a row of chips under it, and how many there are.
            float chip = 30f * s, wordsBox = 24f * s;
            float bag = Mathf.Clamp(Mathf.Min(playingTall, cardsHeight) - chip - wordsBox - 20f * s, 60f * s, 124f * s);
            float bagTall = bag + 10f * s + chip + 10f * s + wordsBox;

            float total = playingWidth + sep + drawWidth + (others > 0 ? sep + eligibleWidth : 0f);
            float x = Mathf.Max(0f, (width - total) * .5f);
            // The bag's frame stands 8 units proud of what it holds, above and below.
            float height = top + Mathf.Max(playingTall, Mathf.Max(eligibleTall, bagTall + 8f * s));

            var row = new GameObject(VetoDrawBoardName, typeof(RectTransform), typeof(LayoutElement)).GetComponent<RectTransform>();
            row.SetParent(content, false);
            var size = row.GetComponent<LayoutElement>();
            size.minHeight = size.preferredHeight = height;
            var state = director != null ? director.Snapshot : null;

            // By right.
            var left = EndScreenKit.Box(DrawPlayingName, row, x, 0f, playingWidth, height);
            DrawHeading(left, playingHeading, playingWidth);
            for (int i = 0; i < playing.Count; i++)
            {
                var face = playing[i];
                string frame = face.Role == "HOH" ? PackArt.Pack8VetoAutoHoh
                    : string.IsNullOrEmpty(face.Role) ? PackArt.Pack8VetoEligible : PackArt.Pack8VetoAutoNominee;
                DrawCard(left, state, face, (i % playingColumns) * (playingCard + gap), top + (i / playingColumns) * (CardHeight(playingCard) + gap),
                    playingCard, frame);
            }
            x += playingWidth;
            float middle = top + Mathf.Min(playingTall, CardHeight(playingCard)) * .5f;
            if (others > 0) DrawChevron(row, x + sep * .5f, middle);
            x += sep;

            // The bag, framed as the row's middle.
            var draw = EndScreenKit.Box(DrawColumnName, row, x, 0f, drawWidth, height);
            DrawHeading(draw, drawHeading, drawWidth);
            float bagTop = top + Mathf.Max(0f, (playingTall - bagTall) * .5f);
            var area = EndScreenKit.Box("Draw area", draw, 0f, bagTop - 8f * s, drawWidth, bagTall + 16f * s);
            EndScreenKit.Frame(area, PackArt.Pack8DrawArea, 12f * s, new Color(Surface.r, Surface.g, Surface.b, .6f),
                new Color(UiTheme.Gold.r, UiTheme.Gold.g, UiTheme.Gold.b, .45f));
            ChipBagIn(draw, toDraw, drawWidth, bagTop, bag, chip);
            var words = FixedText(draw, chipsLine, 17, Paper, new Vector2(0f, -(bagTop + bag + 10f * s + chip + 10f * s)), new Vector2(drawWidth, wordsBox));
            words.name = "Chips line";
            words.alignment = TextAlignmentOptions.Center;
            AutoSize(words, 12);
            x += drawWidth;

            // The pool.
            if (others > 0)
            {
                DrawChevron(row, x + sep * .5f, middle);
                x += sep;
                var right = EndScreenKit.Box(DrawEligibleName, row, x, 0f, eligibleWidth, height);
                DrawHeading(right, eligibleHeading, eligibleWidth);
                for (int i = 0; i < others; i++)
                    DrawCard(right, state, eligible[i], (i % eligibleColumns) * (eligibleCard + gap),
                        top + (i / eligibleColumns) * (CardHeight(eligibleCard) + gap), eligibleCard, PackArt.Pack8VetoEligible);
            }
            return row;
        }

        /// <summary>
        /// Splits <paramref name="room"/> - the row less the bag's column and its chevrons - between
        /// <paramref name="byRight"/> cards in one row and a grid of <paramref name="others"/>, trying
        /// every width for the cards by right from the widest the height allows down to the floor, and
        /// keeping the one whose smaller card is largest. With <paramref name="oneBand"/> the pool is
        /// held to the height of the row by right, so the draw reads as one band. Returns that smaller
        /// card's width, or -1 when no split fits.
        /// </summary>
        private float SplitDraw(int byRight, int others, float room, float cardsHeight, bool oneBand,
            out float card, out float fitted, out int across)
        {
            float s = FontScale, gap = FaceGap * s, floor = FaceCardFloor * s;
            float widest = Mathf.Min(DrawCardWidest * s, (cardsHeight - 50f * s) / 1.12f);
            float best = -1f;
            card = fitted = floor;
            across = 1;
            for (float width = widest; width >= floor - .01f; width -= 2f * s)
            {
                float pool = room - (byRight * width + (byRight - 1) * gap);
                if (pool < floor) continue;
                FaceGrid(others, pool, width, oneBand ? CardHeight(width) : cardsHeight, s, out float cards, out int columns);
                // At the floor FaceGrid gives up the height, which a band cannot.
                if (oneBand && Mathf.CeilToInt(others / (float)columns) * CardHeight(cards) > CardHeight(width) + .5f) continue;
                float score = Mathf.Min(width, cards);
                if (score > best + .01f) { best = score; card = width; fitted = cards; across = columns; }
            }
            return best;
        }

        /// <summary>The narrowest the pool's cards may run and still share one band with the cards by right, at the resting text size.</summary>
        private const float DrawBandComfort = 96f;

        /// <summary>A card on the draw: a photo 1.12 of its width and a 50-unit foot for the name, as a ceremony's face card is.</summary>
        private float CardHeight(float width) => width * 1.12f + 50f * FontScale;

        /// <summary>A column's heading in the accent, letterspaced, over a rule the column's width.</summary>
        private void DrawHeading(RectTransform column, string words, float width)
        {
            if (string.IsNullOrEmpty(words)) return;
            float s = FontScale;
            var label = FixedText(column, words, 15, Accent, Vector2.zero, new Vector2(width, DrawHeadingBox * s));
            label.name = "Column heading";
            label.alignment = TextAlignmentOptions.Center;
            label.characterSpacing = 4f;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            if (semibold != null) label.font = semibold;
            AutoSize(label, 11);
            var rule = HudPrimitives.Fill("Column rule", column, new Color(Accent.r, Accent.g, Accent.b, .45f), 1);
            EndScreenKit.Place(rule, 0f, (DrawHeadingBox + 4f) * s, width, Mathf.Max(1f, 2f * s));
        }

        /// <summary>The mockup's chevron between two columns: Pack 8's arrow, or the drawn one without the pack.</summary>
        private void DrawChevron(RectTransform row, float centreX, float centreY)
        {
            float s = FontScale, side = 28f * s;
            var holder = EndScreenKit.Box("Chevron", row, centreX - side * .5f, centreY - side * .5f, side, side);
            var sprite = UiTheme.Pack(PackArt.Pack8ArrowRight);
            if (sprite != null)
            {
                var image = holder.gameObject.AddComponent<Image>();
                image.sprite = sprite;
                image.preserveAspect = true;
                image.raycastTarget = false;
                return;
            }
            HudPrimitives.Chevron(holder, Accent, side);
        }

        /// <summary>
        /// The bag and its chips, under the name the draw has always been found by: a chip for each
        /// seat the draw fills, and none at all when everyone plays.
        /// </summary>
        private void ChipBagIn(RectTransform column, int toDraw, float width, float y, float bag, float chip)
        {
            float s = FontScale;
            var holder = EndScreenKit.Box(ChipBagName, column, 0f, y, width, bag + 10f * s + chip);
            var art = UiTheme.Pack(PackArt.Pack8VetoBag);
            RectTransform sack;
            if (art != null)
            {
                var image = new GameObject("Bag", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                sack = image.rectTransform;
                sack.SetParent(holder, false);
                image.sprite = art;
                image.preserveAspect = true;
                image.raycastTarget = false;
            }
            else
            {
                // The drawn sack the screen always had, when the pack is not there.
                sack = Panel("Bag", holder, new Color(.55f, .33f, .12f, 1f), 26);
                sack.GetComponent<Image>().raycastTarget = false;
                UiTheme.AddBorder(sack, 26, new Color(.85f, .6f, .25f, 1f));
            }
            EndScreenKit.Place(sack, (width - bag) * .5f, 0f, bag, bag);
            var chipArt = UiTheme.Pack(PackArt.Pack8DrawChip);
            int chips = Mathf.Clamp(toDraw, 0, 6);
            float step = chip + 6f * s, start = width * .5f - (chips - 1) * step * .5f - chip * .5f;
            for (int i = 0; i < chips; i++)
            {
                RectTransform piece;
                if (chipArt != null)
                {
                    var image = new GameObject("Chip", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                    piece = image.rectTransform;
                    piece.SetParent(holder, false);
                    image.sprite = chipArt;
                    image.preserveAspect = true;
                    image.raycastTarget = false;
                }
                else
                {
                    piece = Panel("Chip", holder, UiTheme.Accent, 9);
                    piece.GetComponent<Image>().raycastTarget = false;
                }
                EndScreenKit.Place(piece, start + i * step, bag + 10f * s, chip, chip);
            }
        }

        /// <summary>
        /// One face on the draw, on Pack 8's card for what they are doing there: the photo, the role
        /// pill on its foot when they have one, the name under it. Named as every ceremony's face card
        /// is, 'Face · {name}', with the pill 'Role'. Not a control.
        /// </summary>
        private void DrawCard(RectTransform parent, EpisodeState state, CeremonyFace face, float x, float y, float width, string frame)
        {
            float s = FontScale, photo = width * 1.12f;
            var actor = state != null ? state.Find(face.Id) : null;
            string name = actor != null ? actor.name : face.Id;
            var card = EndScreenKit.Box("Face · " + name, parent, x, y, width, photo + 50f * s);
            EndScreenKit.Frame(card, frame, 10f * s, new Color(Surface.r, Surface.g, Surface.b, .92f),
                string.IsNullOrEmpty(face.Role) ? (Color?)null : new Color(face.RoleColour.r, face.RoleColour.g, face.RoleColour.b, .6f));
            var picture = HudPrimitives.RectPortrait(card, "Photo", Portrait(face.Id), actor, new Vector2(width - 12f * s, photo - 6f * s), 7);
            picture.anchorMin = picture.anchorMax = new Vector2(.5f, 1f);
            picture.pivot = new Vector2(.5f, 1f);
            picture.anchoredPosition = new Vector2(0f, -6f * s);
            if (!string.IsNullOrEmpty(face.Role))
            {
                var pill = Panel("Role", picture, face.RoleColour, 4);
                float pillWidth = Mathf.Clamp(face.Role.Length * 7.5f + 16f, 46f, width / s - 20f) * s;
                Anchor(pill, new Vector2(.5f, 0f), new Vector2(.5f, 0f), new Vector2(0f, 5f * s), new Vector2(pillWidth, 17f * s));
                pill.GetComponent<Image>().raycastTarget = false;
                var word = FixedText(pill, face.Role, 11, UiTheme.Ink, Vector2.zero, pill.sizeDelta);
                word.alignment = TextAlignmentOptions.Center;
            }
            var label = NewText(card, name, 15, Paper);
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            if (semibold != null) label.font = semibold;
            label.alignment = TextAlignmentOptions.Top;
            AutoSize(label, 10);
            Anchor(label.rectTransform, new Vector2(.5f, 1f), new Vector2(.5f, 1f), new Vector2(0f, -(photo + 6f * s)), new Vector2(width - 10f * s, 40f * s));
        }

        /// <summary>
        /// Dresses the pinned way on as the step it takes (PACK8-PASS-PLAN decision 4): Pack 8's face
        /// for it, and <paramref name="headline"/> - the mockup's label - as a label of its own over
        /// the caption. The caption keeps its words and stays the button's first label, so every
        /// lookup by caption, the footer's measure and a screen reader still find "Continue episode".
        /// Only the render's pinned action is dressed; any other button is left as it is.
        /// </summary>
        public void DressWayOn(Button button, string headline, string face)
        {
            if (button == null || string.IsNullOrEmpty(headline) || pinnedAction == null || button.transform != pinnedAction) return;
            float s = FontScale, left = 16f, right = 16f + 18f * s + 10f;
            var rect = pinnedAction;
            // The pack's face over the glass; without the pack the button keeps the glass it has.
            if (UiTheme.Pack(face) != null) EndScreenKit.Frame(rect, face, 12f * s, Surface);
            var caption = button.GetComponentInChildren<TMP_Text>();
            // The headline over the caption, each in a box 1.3 times its type.
            float headlineBox = 18f * 1.3f * s, captionBox = 14f * 1.3f * s;
            var words = NewText(rect, headline, 18, Paper);
            words.name = WayOnHeadlineName;
            var bold = UiTheme.Font(UiTheme.Weight.Bold);
            if (bold != null) words.font = bold;
            words.characterSpacing = 3f;
            words.alignment = TextAlignmentOptions.Left;
            words.textWrappingMode = TextWrappingModes.NoWrap;
            words.overflowMode = TextOverflowModes.Ellipsis;
            AutoSize(words, 13);
            words.rectTransform.anchorMin = new Vector2(0f, 1f); words.rectTransform.anchorMax = new Vector2(1f, 1f);
            words.rectTransform.pivot = new Vector2(.5f, 1f);
            words.rectTransform.offsetMin = new Vector2(left, -(6f * s + headlineBox));
            words.rectTransform.offsetMax = new Vector2(-right, -6f * s);
            if (caption == null) return;
            caption.fontSize = Mathf.RoundToInt(14 * s);
            caption.alignment = TextAlignmentOptions.Left;
            AutoSize(caption, 11);
            var line = caption.rectTransform;
            line.anchorMin = new Vector2(0f, 0f); line.anchorMax = new Vector2(1f, 0f);
            line.pivot = new Vector2(.5f, 0f);
            line.offsetMin = new Vector2(left, 7f * s);
            line.offsetMax = new Vector2(-right, 7f * s + captionBox);
        }
    }
}
