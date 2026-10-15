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
        /// <summary>
        /// The draw's parts, by the names a test finds them by. The way on's headline is named as the
        /// veto meeting's is, "Way on headline", under an identifier of the draw's own: the screens
        /// were built side by side, and two constants of one name in this partial class do not compile.
        /// </summary>
        public const string VetoDrawBoardName = "Veto draw", VetoDrawLineName = "Draw line", DrawPlayingName = "Playing by right",
            DrawColumnName = "Draw column", DrawEligibleName = "Eligible for draw", DrawWayOnHeadlineName = "Way on headline";

        /// <summary>How wide the bag's column stands, and the room a chevron takes between two columns, at the resting text size.</summary>
        private const float DrawColumnWidth = 190f, DrawSeparator = 44f;
        /// <summary>The widest a card on the draw runs, at the resting text size: the mockup's.</summary>
        private const float DrawCardWidest = 150f;
        /// <summary>The heading's 20 units, its rule ending at 26, then one clear unit before the cards.</summary>
        private const float DrawHeadingBox = 20f, DrawCardsTop = 27f;
        /// <summary>
        /// The bag's column at the resting text size: the least and the most the bag stands, the chips
        /// under it, the line under them, and how far the column's frame stands proud of it all.
        /// </summary>
        private const float DrawBagLeast = 60f, DrawBagMost = 124f, DrawChipSide = 30f, DrawChipsLineBox = 24f, DrawAreaProud = 8f;

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
        private float VetoDrawBodyHeight()
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
            float width = ContentWidth(), top;
            int others = eligible != null ? eligible.Count : 0;
            if (string.IsNullOrEmpty(playingHeading) || string.IsNullOrEmpty(drawHeading) || (others > 0 && string.IsNullOrEmpty(eligibleHeading)))
                throw new System.InvalidOperationException("Veto draw required heading is unavailable.");

            // What the step has left under the rows above it, measured as the column lays them out.
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            var column = content.GetComponent<VerticalLayoutGroup>();
            float spacing = column != null ? column.spacing : 12f;
            float budget = VetoDrawBodyHeight() - content.rect.height - spacing - 2f;
            var row = new GameObject(VetoDrawBoardName, typeof(RectTransform), typeof(LayoutElement)).GetComponent<RectTransform>();
            row.SetParent(content, false);
            var draw = EndScreenKit.Box(DrawColumnName, row, 0f, 0f, drawWidth, 0f);
            var area = EndScreenKit.Box("Draw area", draw, 0f, 0f, drawWidth, 0f);
            var frameArt = EndScreenKit.Frame(area, PackArt.Pack8DrawArea, 12f * s, new Color(Surface.r, Surface.g, Surface.b, .6f),
                new Color(UiTheme.Gold.r, UiTheme.Gold.g, UiTheme.Gold.b, .45f));
            Vector2 frameBefore = -frameArt.rectTransform.offsetMin, frameAfter = frameArt.rectTransform.offsetMax;
            if (!(frameBefore.x >= 0f) || !(frameBefore.y >= 0f) || !(frameAfter.x >= 0f) || !(frameAfter.y >= 0f)
                || float.IsInfinity(frameBefore.x) || float.IsInfinity(frameBefore.y) || float.IsInfinity(frameAfter.x) || float.IsInfinity(frameAfter.y))
                throw new System.InvalidOperationException("Veto draw frame measurement is unavailable.");
            var playingLabel = VetoDrawColumnHeading(row, playingHeading);
            var drawLabel = VetoDrawColumnHeading(row, drawHeading);
            var eligibleLabel = others > 0 ? VetoDrawColumnHeading(row, eligibleHeading) : null;
            var chipsLabel = FixedText(row, chipsLine, 17, Paper, Vector2.zero, new Vector2(drawWidth, DrawChipsLineBox * s));
            chipsLabel.name = "Chips line";
            chipsLabel.alignment = TextAlignmentOptions.Center;
            Vector2 playingMeasure = VetoDrawHeadingMeasure(playingLabel), drawMeasure = VetoDrawHeadingMeasure(drawLabel);
            Vector2 eligibleMeasure = VetoDrawHeadingMeasure(eligibleLabel), chipsMeasure = VetoDrawHeadingMeasure(chipsLabel);
            float headingBox = Mathf.Max(DrawHeadingBox * s, playingMeasure.y, drawMeasure.y, eligibleMeasure.y);
            top = Mathf.Max(DrawCardsTop * s, headingBox + 7f * s);
            float cardsHeight = Mathf.Max(0f, budget - top);
            int chipCount = Mathf.Clamp(toDraw, 0, 6);
            float chipWidth = chipCount * DrawChipSide * s + Mathf.Max(0, chipCount - 1) * 6f * s;
            float middleMinimum = Mathf.Max(drawMeasure.x, chipsMeasure.x, chipWidth, (DrawBagMost + 2f * DrawAreaProud) * s);
            drawWidth = Mathf.Max(drawWidth, middleMinimum);
            bool sharedHeading = false;

            float playingWidth, eligibleWidth = 0f, playingCard, eligibleCard = 0f;
            int playingColumns, eligibleColumns = 0;
            if (others > 0)
            {
                // Prefer the three by right in one row with a comfortable pool beside them. If
                // that cannot fit, reflow both groups within the actual height, keeping every name
                // at its readable floor and the playing cards no smaller than the pool's.
                var headings = new VetoDrawHeadingSpace(playingMeasure.x, drawMeasure.x, eligibleMeasure.x, top, drawWidth,
                    frameAfter.y, frameBefore.y, frameBefore.x, frameAfter.x);
                float best = VetoDrawConstrainedSplit(playing.Count, others, width - 2f * sep - drawWidth, cardsHeight, s, true, headings,
                    out playingCard, out eligibleCard, out playingColumns, out eligibleColumns, out sharedHeading);
                if (best < DrawBandComfort * s)
                {
                    drawWidth = middleMinimum;
                    headings = new VetoDrawHeadingSpace(playingMeasure.x, drawMeasure.x, eligibleMeasure.x, top, drawWidth,
                        frameAfter.y, frameBefore.y, frameBefore.x, frameAfter.x);
                    best = VetoDrawConstrainedSplit(playing.Count, others, width - 2f * sep - drawWidth, cardsHeight, s, false, headings,
                        out playingCard, out eligibleCard, out playingColumns, out eligibleColumns, out sharedHeading);
                }
                if (best < 0f)
                {
                    // Narrower than any split: the floor on both sides, and the row runs long.
                    playingCard = eligibleCard = FaceCardFloor * s;
                    playingColumns = playing.Count;
                    eligibleColumns = Mathf.Max(1, Mathf.FloorToInt((width * .4f + gap) / (eligibleCard + gap)));
                }
                eligibleWidth = eligibleColumns * eligibleCard + (eligibleColumns - 1) * gap;
            }
            else
            {
                FaceGrid(playing.Count, width - sep - drawWidth, DrawCardWidest * s, cardsHeight, s, out playingCard, out playingColumns);
            }
            playingWidth = playingColumns * playingCard + (playingColumns - 1) * gap;
            int playingRows = Mathf.CeilToInt(playing.Count / (float)playingColumns);
            int eligibleRows = others > 0 ? Mathf.CeilToInt(others / (float)eligibleColumns) : 0;
            float playingTall = playingRows * DrawCardHeight(playingCard) + (playingRows - 1) * gap;
            float eligibleTall = eligibleRows > 0 ? eligibleRows * DrawCardHeight(eligibleCard) + (eligibleRows - 1) * gap : 0f;

            // The bag's column: the bag, a row of chips under it, and how many there are, with its
            // frame inside the height the cards were solved for.
            float chip = DrawChipSide * s, wordsBox = DrawChipsLineBox * s, proud = DrawAreaProud * s;
            VetoDrawHeadingBagColumn(playingTall, eligibleTall, cardsHeight, s, sharedHeading, top, frameAfter.y, frameBefore.y,
                out float bag, out float bagDrop, out float cardsTall);
            float bagTall = bag + DrawUnderBag(s);

            float total = playingWidth + sep + drawWidth + (others > 0 ? sep + eligibleWidth : 0f);
            float x = Mathf.Max(0f, (width - total) * .5f);
            float height = top + cardsTall;

            var size = row.GetComponent<LayoutElement>();
            size.minHeight = size.preferredHeight = height;
            var state = director != null ? director.Snapshot : null;

            // By right.
            var left = EndScreenKit.Box(DrawPlayingName, row, x, 0f, playingWidth, height);
            VetoDrawPlaceHeading(playingLabel, row, x, 0f, sharedHeading ? playingWidth + sep + drawWidth : playingWidth, headingBox);
            for (int i = 0; i < playing.Count; i++)
            {
                var face = playing[i];
                string frame = face.Role == "HOH" ? PackArt.Pack8VetoAutoHoh
                    : string.IsNullOrEmpty(face.Role) ? PackArt.Pack8VetoEligible : PackArt.Pack8VetoAutoNominee;
                VetoDrawCard(left, state, face, (i % playingColumns) * (playingCard + gap),
                    top + (i / playingColumns) * (DrawCardHeight(playingCard) + gap), playingCard, frame);
            }
            x += playingWidth;
            float middle = top + Mathf.Min(playingTall, DrawCardHeight(playingCard)) * .5f;
            if (others > 0) VetoDrawChevron(row, x + sep * .5f, middle);
            x += sep;

            // The bag, framed as the row's middle.
            EndScreenKit.Place(draw, x, 0f, drawWidth, height);
            VetoDrawPlaceHeading(drawLabel, row, x, sharedHeading ? top : 0f, drawWidth, headingBox);
            float bagTop = top + bagDrop;
            EndScreenKit.Place(area, 0f, bagTop - proud, drawWidth, bagTall + 2f * proud);
            ChipBagIn(draw, toDraw, drawWidth, bagTop, bag, chip);
            chipsLabel.rectTransform.SetParent(draw, false);
            EndScreenKit.Place(chipsLabel.rectTransform, 0f, bagTop + bag + 10f * s + chip + 10f * s, drawWidth, wordsBox);
            AutoSize(chipsLabel, 12);
            x += drawWidth;

            // The pool.
            if (others > 0)
            {
                VetoDrawChevron(row, x + sep * .5f, middle);
                x += sep;
                var right = EndScreenKit.Box(DrawEligibleName, row, x, 0f, eligibleWidth, height);
                VetoDrawPlaceHeading(eligibleLabel, row, x, 0f, eligibleWidth, headingBox);
                for (int i = 0; i < others; i++)
                    VetoDrawCard(right, state, eligible[i], (i % eligibleColumns) * (eligibleCard + gap),
                        top + (i / eligibleColumns) * (DrawCardHeight(eligibleCard) + gap), eligibleCard, PackArt.Pack8VetoEligible);
            }
            return row;
        }

        /// <summary>The bag's column under the bag: a gap, the row of chips, a gap, and the line that says how many.</summary>
        private static float DrawUnderBag(float scale) => (10f + DrawChipSide + 10f + DrawChipsLineBox) * scale;

        /// <summary>
        /// The bag's column on the draw's row, worked out without a HUD: how big the bag stands
        /// (<paramref name="bag"/>), how far under the cards' top its column starts
        /// (<paramref name="columnTop"/>), and how tall the row must be under its headings
        /// (<paramref name="rowTall"/>) to hold the cards by right (<paramref name="playingTall"/>),
        /// the pool (<paramref name="eligibleTall"/>) and the bag's frame, which stands 8 units proud
        /// of the column above and below.
        ///
        /// <para>The frame is sized inside the height the cards were solved for
        /// (<paramref name="cardsHeight"/>). Left out of it, a row whose cards that height set ran 8
        /// units past it, and the step scrolled - with a story beat open over the draw, say. Only a
        /// bag already at its least can make the row taller than its cards, and then the row says
        /// so: its height always holds the frame.</para>
        /// </summary>
        public static void VetoDrawBagColumn(float playingTall, float eligibleTall, float cardsHeight, float scale,
            out float bag, out float columnTop, out float rowTall)
        {
            float proud = DrawAreaProud * scale, under = DrawUnderBag(scale);
            bag = Mathf.Clamp(Mathf.Min(playingTall, cardsHeight) - under - 2f * proud, DrawBagLeast * scale, DrawBagMost * scale);
            float column = bag + under;
            columnTop = Mathf.Max(proud, (playingTall - column) * .5f);
            rowTall = Mathf.Max(playingTall, Mathf.Max(eligibleTall, columnTop + column + proud));
        }

        /// <summary>
        /// Splits <paramref name="room"/> - the row less the bag's column and its chevrons - between
        /// <paramref name="byRight"/> cards and a grid of <paramref name="others"/>, trying widths and,
        /// outside the preferred single band, column counts for the cards by right. Keeps the split
        /// whose smaller card is largest. With <paramref name="oneBand"/> the playing cards stay in
        /// one row and the pool is held to its height. Returns that smaller
        /// card's width, or -1 when no split fits.
        /// </summary>
        /// <summary>
        /// The draw's split, without a HUD. The preferred single band keeps the playing cards
        /// together; otherwise their columns may reflow as well as the pool's. Only a full fit,
        /// including every row gap and the bag's frame, competes for the widest smaller card.
        /// Returns -1 when the readable floor cannot fit the actual remaining height.
        /// </summary>
        public static float VetoDrawSplit(int byRight, int others, float room, float cardsHeight, float scale, bool oneBand,
            out float card, out float fitted, out int playingColumns, out int across) =>
            VetoDrawConstrainedSplit(byRight, others, room, cardsHeight, scale, oneBand,
                new VetoDrawHeadingSpace(0f, 0f, 0f, DrawCardsTop * scale, DrawColumnWidth * scale),
                out card, out fitted, out playingColumns, out across, out _);

        /// <summary>Measured unshrunk heading space, middle allocation and actual retained frame-image overhang.</summary>
        public struct VetoDrawHeadingSpace
        {
            public readonly float PlayingWidth, DrawWidth, EligibleWidth, Top, MiddleWidth, FrameTop, FrameBottom, FrameLeft, FrameRight;
            public VetoDrawHeadingSpace(float playingWidth, float drawWidth, float eligibleWidth, float top, float middleWidth,
                float frameTop = 0f, float frameBottom = 0f, float frameLeft = 0f, float frameRight = 0f)
            {
                PlayingWidth = playingWidth; DrawWidth = drawWidth; EligibleWidth = eligibleWidth; Top = top; MiddleWidth = middleWidth;
                FrameTop = frameTop; FrameBottom = frameBottom; FrameLeft = frameLeft; FrameRight = frameRight;
            }
        }

        /// <summary>The same finite card search with complete heading and frame separation constraints.</summary>
        public static float VetoDrawConstrainedSplit(int byRight, int others, float room, float cardsHeight, float scale, bool oneBand,
            VetoDrawHeadingSpace headings, out float card, out float fitted, out int playingColumns, out int across, out bool sharedHeading)
        {
            float s = scale, gap = FaceGap * s, floor = FaceCardFloor * s;
            float best = -1f;
            card = fitted = floor;
            playingColumns = byRight;
            across = 1;
            sharedHeading = false;
            if (byRight < 1 || others < 1 || !(s > 0f) || !(room > 0f) || !(cardsHeight > 0f)
                || float.IsInfinity(s) || float.IsInfinity(room) || float.IsInfinity(cardsHeight)) return best;
            if (!(headings.PlayingWidth >= 0f) || !(headings.DrawWidth >= 0f) || !(headings.EligibleWidth >= 0f)
                || !(headings.Top >= 0f) || !(headings.MiddleWidth > 0f)
                || float.IsInfinity(headings.PlayingWidth) || float.IsInfinity(headings.DrawWidth) || float.IsInfinity(headings.EligibleWidth)
                || float.IsInfinity(headings.Top) || float.IsInfinity(headings.MiddleWidth)
                || headings.DrawWidth > headings.MiddleWidth + .01f) return best;
            if (!(headings.FrameTop >= 0f) || !(headings.FrameBottom >= 0f) || !(headings.FrameLeft >= 0f) || !(headings.FrameRight >= 0f)
                || float.IsInfinity(headings.FrameTop) || float.IsInfinity(headings.FrameBottom)
                || float.IsInfinity(headings.FrameLeft) || float.IsInfinity(headings.FrameRight)
                || headings.FrameLeft > DrawSeparator * s || headings.FrameRight > DrawSeparator * s) return best;
            for (int leftColumns = byRight; leftColumns >= (oneBand ? byRight : 1); leftColumns--)
            {
                int leftRows = Mathf.CeilToInt(byRight / (float)leftColumns);
                float byHeight = ((cardsHeight - (leftRows - 1) * gap) / leftRows - 50f * s) / 1.12f;
                float widest = Mathf.Min(DrawCardWidest * s, byHeight);
                if (widest < floor) continue;
                // Keep the two-unit preference search and always include the exact floor.
                int steps = Mathf.CeilToInt((widest - floor) / (2f * s));
                for (int step = 0; step <= steps; step++)
                {
                    float width = Mathf.Max(floor, widest - step * 2f * s);
                    float playingTall = leftRows * (width * 1.12f + 50f * s) + (leftRows - 1) * gap;
                    float pool = room - (leftColumns * width + (leftColumns - 1) * gap);
                    if (pool < floor) continue;
                    FaceGrid(others, pool, width, oneBand ? playingTall : cardsHeight, s, out float cards, out int columns);
                    int rows = Mathf.CeilToInt(others / (float)columns);
                    float poolWidth = columns * cards + (columns - 1) * gap;
                    float poolTall = rows * (cards * 1.12f + 50f * s) + (rows - 1) * gap;
                    // FaceGrid keeps names readable when its floor is too tall. That fallback
                    // must not outrank a feasible split, even outside the preferred single band.
                    if (poolWidth > pool + .01f || playingTall > cardsHeight + .01f || poolTall > cardsHeight + .01f) continue;
                    if (oneBand && poolTall > playingTall + .5f) continue;
                    float playingWidth = leftColumns * width + (leftColumns - 1) * gap;
                    if (headings.EligibleWidth > poolWidth + .01f) continue;
                    bool shared = headings.PlayingWidth > playingWidth + .01f;
                    if (shared && (oneBand || headings.PlayingWidth > playingWidth + DrawSeparator * s + headings.MiddleWidth + .01f)) continue;
                    VetoDrawHeadingBagColumn(playingTall, poolTall, cardsHeight, s, shared, headings.Top, headings.FrameTop, headings.FrameBottom,
                        out _, out _, out float rowTall);
                    if (rowTall > cardsHeight + .01f) continue;
                    float score = Mathf.Min(width, cards);
                    if (score > best + .01f)
                    {
                        best = score; card = width; fitted = cards; playingColumns = leftColumns; across = columns; sharedHeading = shared;
                    }
                }
            }
            return best;
        }


        /// <summary>Reserve the moved draw heading above, never overlapping, its complete bag frame.</summary>
        public static void VetoDrawHeadingBagColumn(float playingTall, float eligibleTall, float cardsHeight, float scale,
            bool sharedHeading, float headingTop, float frameTop, float frameBottom, out float bag, out float columnTop, out float rowTall)
        {
            VetoDrawBagColumn(playingTall, eligibleTall, cardsHeight, scale, out bag, out columnTop, out rowTall);
            float proud = DrawAreaProud * scale;
            columnTop = Mathf.Max(columnTop, (sharedHeading ? headingTop : 0f) + proud + frameTop);
            rowTall = Mathf.Max(rowTall, columnTop + bag + DrawUnderBag(scale) + proud + frameBottom);
        }

        /// <summary>The narrowest the pool's cards may run and still share one band with the cards by right, at the resting text size.</summary>
        private const float DrawBandComfort = 96f;

        /// <summary>A card on the draw: a photo 1.12 of its width and a 50-unit foot for the name, as a ceremony's face card is.</summary>
        private float DrawCardHeight(float width) => width * 1.12f + 50f * FontScale;

        /// <summary>A column's heading in the accent, letterspaced, over a rule the column's width.</summary>
        private TMP_Text VetoDrawColumnHeading(RectTransform parent, string words)
        {
            if (string.IsNullOrEmpty(words)) return null;
            var label = FixedText(parent, words, 15, Accent, Vector2.zero, Vector2.zero);
            label.name = "Column heading";
            label.alignment = TextAlignmentOptions.Center;
            label.characterSpacing = 4f;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
            label.enableAutoSizing = false;
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            if (semibold != null) label.font = semibold;
            return label;
        }

        private Vector2 VetoDrawHeadingMeasure(TMP_Text label)
        {
            if (label == null) return Vector2.zero;
            if (string.IsNullOrEmpty(label.text)) throw new System.InvalidOperationException("Veto draw measured copy is unavailable.");
            Vector2 required = label.GetPreferredValues(label.text);
            if (!(required.x >= 0f) || !(required.y > 0f) || float.IsInfinity(required.x) || float.IsInfinity(required.y))
                throw new System.InvalidOperationException("Veto draw full-copy measurement is unavailable.");
            return new Vector2(Mathf.Ceil(required.x) + 2f * FontScale, Mathf.Ceil(required.y));
        }

        private void VetoDrawPlaceHeading(TMP_Text label, RectTransform row, float x, float y, float width, float box)
        {
            if (label == null) return;
            EndScreenKit.Place(label.rectTransform, x, y, width, box);
            var rule = HudPrimitives.Fill("Column rule", row, new Color(Accent.r, Accent.g, Accent.b, .45f), 1);
            EndScreenKit.Place(rule, x, y + box + 4f * FontScale, width, Mathf.Max(1f, 2f * FontScale));
        }

        /// <summary>The mockup's chevron between two columns: Pack 8's arrow, or the drawn one without the pack.</summary>
        private void VetoDrawChevron(RectTransform row, float centreX, float centreY)
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
        private void VetoDrawCard(RectTransform parent, EpisodeState state, CeremonyFace face, float x, float y, float width, string frame)
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
            words.name = DrawWayOnHeadlineName;
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
