using System.Collections.Generic;
using System.Linq;
using Gamesim.Episode;
using NUnit.Framework;
using UnityEngine;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The ceremony faces' grid (PACK8-PASS-PLAN A3): the card width and the number to a row that
    /// <see cref="EpisodeHud.CeremonyFaces"/> lays a house out at, fitted to a height or not. Worked
    /// out without a HUD, because the defect this guards is in float division rather than in the
    /// layout: a card width the column set divides the column back into its count only in real
    /// arithmetic, and ten faces at the larger text floored to nine a row - two rows, more than
    /// twice the height asked.
    /// </summary>
    public sealed class FaceGridTests
    {
        /// <summary>The reading column at both text sizes on the 16:9 frame and on the 4:3 batch canvas, and a narrower and a wider one.</summary>
        private static readonly float[] Columns = { 880f, 1040f, 1071.64f, 1248f, 1286f };
        private static readonly float[] Scales = { 1f, 1.2f };

        /// <summary>The grid's height as CeremonyFaces sizes it: rows of a photo 1.12 of the card's width over a 50-unit foot, a gap between them.</summary>
        private static float GridHeight(int count, float width, int columns, float scale)
        {
            int rows = Mathf.CeilToInt(count / (float)columns);
            return rows * (width * 1.12f + 50f * scale) + (rows - 1) * (EpisodeHud.FaceGap * scale);
        }

        private static float GridWidth(float width, int columns, float scale) =>
            columns * width + (columns - 1) * (EpisodeHud.FaceGap * scale);

        /// <summary>
        /// The case the review found: the 16:9 frame at the larger text, where the column is 1248
        /// and a step 183 high takes ten faces in one row at 109.68. Dividing the column again by
        /// that width gave 9.999999 in float, and nine to a row.
        /// </summary>
        [Test]
        public void TenFacesAtTheLargerTextStandInTheOneRowTheirWidthWasSolvedFor()
        {
            EpisodeHud.FaceGrid(10, 1248f, 150f * 1.2f, 183f, 1.2f, out float width, out int columns);
            Assert.That(columns, Is.EqualTo(10), "One row of ten, at " + width + " a card.");
            Assert.That(GridHeight(10, width, columns, 1.2f), Is.LessThanOrEqualTo(183f + .01f), "in the height asked,");
            Assert.That(GridWidth(width, columns, 1.2f), Is.LessThanOrEqualTo(1248f + .01f), "and the column's width.");
        }

        /// <summary>
        /// Every house from one to sixteen, at both text sizes, in every column a screen gives it and
        /// every height from 100 to 700: the grid fits the height unless the cards stopped at the
        /// floor, never runs wider than the column, and never stands more to a row than there are faces.
        /// </summary>
        [Test]
        public void EveryFittedGridStaysInItsHeightAndItsColumn()
        {
            var misses = new List<string>();
            foreach (float scale in Scales)
                foreach (float column in Columns)
                    for (int count = 1; count <= 16; count++)
                        for (int fit = 100; fit <= 700; fit++)
                        {
                            EpisodeHud.FaceGrid(count, column, 150f * scale, fit, scale, out float width, out int columns);
                            float height = GridHeight(count, width, columns, scale);
                            bool atTheFloor = width <= EpisodeHud.FaceCardFloor * scale + .001f;
                            if (columns < 1 || columns > count || (!atTheFloor && height > fit + .01f)
                                || (columns > 1 && GridWidth(width, columns, scale) > column + .01f))
                                misses.Add(count + " faces at " + scale + " in " + column + " by " + fit + ": " + columns
                                    + " to a row at " + width + ", " + height + " high");
                        }
            Assert.That(misses, Is.Empty, misses.Count + " grids miss their height or column, among them: "
                + string.Join(" | ", misses.Take(8)));
        }

        /// <summary>Without a height to fit, a card is the width asked, as many to a row as the column holds and no more than there are faces.</summary>
        [Test]
        public void WithoutAHeightEachCardIsTheWidthAsked()
        {
            EpisodeHud.FaceGrid(7, 1040f, 150f, 0f, 1f, out float width, out int columns);
            Assert.That(width, Is.EqualTo(150f));
            Assert.That(columns, Is.EqualTo(6), "(1040 + 14) / (150 + 14) is six and a bit.");
            EpisodeHud.FaceGrid(3, 1040f, 150f, 0f, 1f, out width, out columns);
            Assert.That(columns, Is.EqualTo(3), "Never more to a row than there are faces.");
        }

        /// <summary>
        /// Sixteen faces at the larger text in a step 100 high fit at no width a name still reads
        /// at: the card stops at the floor and the grid runs taller than asked, as FaceGrid says,
        /// but never wider than the column.
        /// </summary>
        [Test]
        public void AtTheFloorTheNamesOutrankTheHeight()
        {
            EpisodeHud.FaceGrid(16, 1248f, 150f * 1.2f, 100f, 1.2f, out float width, out int columns);
            Assert.That(width, Is.EqualTo(EpisodeHud.FaceCardFloor * 1.2f).Within(.001f), "The card stops at the floor,");
            Assert.That(GridHeight(16, width, columns, 1.2f), Is.GreaterThan(100f), "the grid runs taller than asked,");
            Assert.That(GridWidth(width, columns, 1.2f), Is.LessThanOrEqualTo(1248f + .01f), "and never wider than the column.");
        }
    }
}
