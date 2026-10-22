using System.Collections.Generic;
using System.Linq;
using Gamesim.Episode;
using NUnit.Framework;
using UnityEngine;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The bag's column on the veto draw's row (PACK8-PASS-PLAN B2), worked out without a HUD as
    /// FaceGridTests works out the faces. The row is sized from the height the step has left under
    /// the rows above it, and the bag's frame - which stands 8 units proud of the bag above and below
    /// - was left out of that height: a row whose cards the height had set, in a short step with a
    /// story beat open over the draw, ran 8 units past it, and the screen scrolled.
    /// </summary>
    public sealed class VetoDrawRowTests
    {
        private static readonly float[] Scales = { 1f, 1.2f };

        /// <summary>A card on the draw: a photo 1.12 of its width over a 50-unit foot, as EpisodeHud sizes it.</summary>
        private static float CardTall(float width, float scale) => width * 1.12f + 50f * scale;

        /// <summary>The bag's column under the bag at <paramref name="scale"/>: a gap, the chips, a gap and the line that says how many.</summary>
        private static float UnderTheBag(float scale) => (10f + 30f + 10f + 24f) * scale;

        /// <summary>
        /// The review's case: a step that leaves the cards 180 units at the resting text size, the
        /// cards by right and the pool solved to fill it. The row is 180 tall, not 188, and the bag's
        /// frame runs from the cards' top to their foot.
        /// </summary>
        [Test]
        public void ARowTheHeightSetIsThatHeightWithTheBagsFrameInside()
        {
            EpisodeHud.VetoDrawBagColumn(180f, 180f, 180f, 1f, out float bag, out float top, out float tall);
            Assert.That(tall, Is.EqualTo(180f).Within(.01f), "The row is the height its cards were solved for,");
            Assert.That(top - 8f, Is.GreaterThanOrEqualTo(-.01f), "the bag's frame starts at the cards' top or under it,");
            Assert.That(top + bag + UnderTheBag(1f) + 8f, Is.LessThanOrEqualTo(180f + .01f), "and ends at their foot or over it.");
        }

        /// <summary>
        /// Every height a step can leave the cards, at both text sizes, with the cards by right as tall
        /// as that height lets them stand or any shorter, and a pool of nobody, of one band beside
        /// them, or filling the height: the row always holds the bag's frame, the bag stands between
        /// its least and its most, and only a bag already at its least stands the row taller than its
        /// cards.
        /// </summary>
        [Test]
        public void TheBagsFrameStaysInsideTheHeightTheCardsWereSolvedFor()
        {
            var misses = new List<string>();
            foreach (float scale in Scales)
            {
                float floor = CardTall(EpisodeHud.FaceCardFloor * scale, scale), widest = CardTall(150f * scale, scale);
                float under = UnderTheBag(scale), proud = 8f * scale, least = 60f * scale, most = 124f * scale;
                for (float height = floor; height <= 700f; height += 1f)
                    for (float playing = floor; playing <= Mathf.Min(height, widest) + .001f; playing += 2f)
                        foreach (float eligible in new[] { 0f, playing, height })
                        {
                            EpisodeHud.VetoDrawBagColumn(playing, eligible, height, scale, out float bag, out float top, out float tall);
                            string at = playing + " by right and " + eligible + " in the pool, in " + height + " at " + scale;
                            float frameFoot = top + bag + under + proud;
                            if (bag < least - .001f || bag > most + .001f)
                                misses.Add(at + ": a bag of " + bag);
                            else if (frameFoot > tall + .01f)
                                misses.Add(at + ": the frame ends at " + frameFoot + " in a row " + tall + " tall");
                            else if (bag > least + .001f && tall > Mathf.Max(playing, eligible) + .01f)
                                misses.Add(at + ": a bag of " + bag + " stands the row " + tall + " tall");
                        }
            }
            Assert.That(misses, Is.Empty, misses.Count + " rows miss, among them: " + string.Join(" | ", misses.Take(8)));
        }

        /// <summary>The 1200x900 largest-text geometry inferred from retained failure3331 needs both groups to reflow.</summary>
        [Test]
        public void TheLargestDrawReflowsBothGroupsWithinItsActualBudget()
        {
            const float scale = 1.4f, canvasWidth = 1385.640646f, viewport = 761.430359f, beforeBoard = 158.8f;
            float room = canvasWidth - EpisodeHud.LeftColumnX - EpisodeHud.StrategyEdge - 74f - (2f * 44f + 190f) * scale;
            float height = viewport - beforeBoard - 12f - 2f - 27f * scale;
            float score = EpisodeHud.VetoDrawSplit(3, 13, room, height, scale, false,
                out float playing, out float eligible, out int playingColumns, out int columns);
            Assert.That(score, Is.GreaterThanOrEqualTo(EpisodeHud.FaceCardFloor * scale),
                "A real split exists without changing a font, card floor, viewport or safety reserve.");
            Assert.That(playingColumns, Is.EqualTo(1), "The three playing cards must reflow at this width and height.");
            Assert.That(columns, Is.EqualTo(5), "The thirteen eligible faces use three rows.");
            AssertDrawSplitFits(3, 13, room, height, scale, playing, eligible, playingColumns, columns);
        }

        /// <summary>The two-unit preference steps must not miss an exact-floor-only feasible split.</summary>
        [Test]
        public void AFeasibleExactFloorIsTriedAfterTheTwoUnitSteps()
        {
            const float scale = 1f, room = 284f, height = 150f;
            float score = EpisodeHud.VetoDrawSplit(3, 1, room, height, scale, false,
                out float playing, out float eligible, out int playingColumns, out int columns);
            Assert.That(score, Is.EqualTo(EpisodeHud.FaceCardFloor).Within(.001f),
                "Only four exact-floor cards plus the two playing-side gaps fit this width.");
            AssertDrawSplitFits(3, 1, room, height, scale, playing, eligible, playingColumns, columns);
        }

        /// <summary>Thirteen eligible faces cannot fit this height even at the unchanged readable floor.</summary>
        [Test]
        public void AFullDrawThatCannotFitReportsNoSplit()
        {
            float score = EpisodeHud.VetoDrawSplit(3, 13, 900f, 500f, 1.4f, false,
                out _, out _, out _, out _);
            Assert.That(score, Is.LessThan(0f), "An overflowing floor fallback must not be reported as a feasible split.");
        }

        /// <summary>Cards that fit alone do not license a bag frame taller than the row budget.</summary>
        [Test]
        public void ABagFrameThatCannotFitReportsNoSplit()
        {
            float score = EpisodeHud.VetoDrawSplit(3, 1, 900f, 190f, 1.4f, false,
                out _, out _, out _, out _);
            Assert.That(score, Is.LessThan(0f), "Even the minimum bag's frame needs more than 190 at this scale.");
        }

        private static void AssertDrawSplitFits(int byRight, int others, float room, float height, float scale,
            float playing, float eligible, int playingColumns, int columns)
        {
            float gap = EpisodeHud.FaceGap * scale;
            int leftRows = Mathf.CeilToInt(byRight / (float)playingColumns);
            int rows = Mathf.CeilToInt(others / (float)columns);
            float playingTall = leftRows * CardTall(playing, scale) + (leftRows - 1) * gap;
            float eligibleTall = rows * CardTall(eligible, scale) + (rows - 1) * gap;
            Assert.That(playing, Is.GreaterThanOrEqualTo(EpisodeHud.FaceCardFloor * scale));
            Assert.That(eligible, Is.GreaterThanOrEqualTo(EpisodeHud.FaceCardFloor * scale));
            Assert.That(playingColumns, Is.InRange(1, byRight));
            Assert.That(columns, Is.InRange(1, others));
            Assert.That(playingColumns * playing + (playingColumns - 1) * gap + columns * eligible + (columns - 1) * gap,
                Is.LessThanOrEqualTo(room + .01f), "Both card groups and all horizontal gaps fit.");
            Assert.That(playingTall, Is.LessThanOrEqualTo(height + .01f));
            Assert.That(eligibleTall, Is.LessThanOrEqualTo(height + .01f), "All eligible rows and vertical gaps fit.");
            EpisodeHud.VetoDrawBagColumn(playingTall, eligibleTall, height, scale, out _, out float columnTop, out float rowTall);
            Assert.That(columnTop - 8f * scale, Is.GreaterThanOrEqualTo(-.01f), "The bag's frame cannot rise into its heading.");
            Assert.That(rowTall, Is.LessThanOrEqualTo(height + .01f), "The bag's full frame also fits.");
        }

        /// <summary>Illustrative full-copy widths constrain the narrow Largest frame; native TMP metrics remain the runtime authority.</summary>
        [Test]
        public void CompleteHeadingsConstrainLargestFourThreePacking()
        {
            const float scale = 1.4f, top = 37.8f, height = 550.830359f, middle = 201f;
            const float room = 1071.640646f - 2f * 44f * scale - middle;
            var headings = new EpisodeHud.VetoDrawHeadingSpace(304f, 201f, 226f, top, middle, 4.2f, 4.2f, 4.2f, 4.2f);
            float score = EpisodeHud.VetoDrawConstrainedSplit(3, 13, room, height, scale, false, headings,
                out float playing, out float eligible, out int playingColumns, out int columns, out bool shared);
            Assert.That(score, Is.GreaterThanOrEqualTo(EpisodeHud.FaceCardFloor * scale));
            Assert.That(shared, Is.True, "The complete automatic heading needs the shared allocation.");
            AssertCompleteHeadingLayout(3, 13, room, height, scale, headings, playing, eligible, playingColumns, columns, shared);
        }

        /// <summary>The fixed bag width cannot fit this two-row case; measured full-copy middle width201 can.</summary>
        [Test]
        public void CompleteHeadingsConstrainLargestSixteenNinePacking()
        {
            const float scale = 1.4f, top = 37.8f, height = 411.6f, middle = 201f;
            const float room = 1286f - 2f * 44f * scale - middle;
            var headings = new EpisodeHud.VetoDrawHeadingSpace(304f, 201f, 226f, top, middle, 4.2f, 4.2f, 4.2f, 4.2f);
            float score = EpisodeHud.VetoDrawConstrainedSplit(3, 13, room, height, scale, false, headings,
                out float playing, out float eligible, out int playingColumns, out int columns, out bool shared);
            Assert.That(score, Is.GreaterThanOrEqualTo(EpisodeHud.FaceCardFloor * scale));
            Assert.That(Mathf.CeilToInt(3f / playingColumns), Is.EqualTo(2));
            Assert.That(Mathf.CeilToInt(13f / columns), Is.EqualTo(2));
            AssertCompleteHeadingLayout(3, 13, room, height, scale, headings, playing, eligible, playingColumns, columns, shared);
            var tooWide = new EpisodeHud.VetoDrawHeadingSpace(304f, 220f, 226f, top, middle);
            Assert.That(EpisodeHud.VetoDrawConstrainedSplit(3, 13, room, height, scale, false, tooWide,
                out _, out _, out _, out _, out _), Is.LessThan(0f), "Unknown wider actual copy cannot be declared fitted.");
            var tooTall = new EpisodeHud.VetoDrawHeadingSpace(304f, 201f, 226f, top, middle, 500f, 4.2f, 4.2f, 4.2f);
            Assert.That(EpisodeHud.VetoDrawConstrainedSplit(3, 13, room, height, scale, false, tooTall,
                out _, out _, out _, out _, out _), Is.LessThan(0f), "Actual frame overhang cannot be hidden behind its host's smaller bounds.");
        }

        private static void AssertCompleteHeadingLayout(int byRight, int others, float room, float height, float scale,
            EpisodeHud.VetoDrawHeadingSpace headings, float playing, float eligible, int playingColumns, int columns, bool shared)
        {
            AssertDrawSplitFits(byRight, others, room, height, scale, playing, eligible, playingColumns, columns);
            float gap = EpisodeHud.FaceGap * scale;
            float playingWidth = playingColumns * playing + (playingColumns - 1) * gap;
            float eligibleWidth = columns * eligible + (columns - 1) * gap;
            Assert.That(headings.PlayingWidth, Is.LessThanOrEqualTo(playingWidth + (shared ? 44f * scale + headings.MiddleWidth : 0f) + .01f));
            Assert.That(headings.DrawWidth, Is.LessThanOrEqualTo(headings.MiddleWidth + .01f));
            Assert.That(headings.EligibleWidth, Is.LessThanOrEqualTo(eligibleWidth + .01f));
            int leftRows = Mathf.CeilToInt(byRight / (float)playingColumns), rows = Mathf.CeilToInt(others / (float)columns);
            float playingTall = leftRows * CardTall(playing, scale) + (leftRows - 1) * gap;
            float eligibleTall = rows * CardTall(eligible, scale) + (rows - 1) * gap;
            EpisodeHud.VetoDrawHeadingBagColumn(playingTall, eligibleTall, height, scale, shared, headings.Top,
                headings.FrameTop, headings.FrameBottom, out float bag, out float bagTop, out float rowTall);
            Assert.That(bag, Is.InRange(60f * scale, 124f * scale), "The frame reservation cannot change the permitted bag sizes.");
            Assert.That(rowTall, Is.LessThanOrEqualTo(height + .01f));
            Assert.That(bagTop - 8f * scale - headings.FrameTop, Is.GreaterThanOrEqualTo((shared ? headings.Top : 0f) - .01f),
                "Every heading and rule clears the actual frame quad, not merely its host.");
            Assert.That(bagTop + bag + UnderTheBag(scale) + 8f * scale + headings.FrameBottom, Is.LessThanOrEqualTo(rowTall + .01f));
            Assert.That(headings.FrameLeft, Is.LessThanOrEqualTo(44f * scale));
            Assert.That(headings.FrameRight, Is.LessThanOrEqualTo(44f * scale));
        }
    }
}
