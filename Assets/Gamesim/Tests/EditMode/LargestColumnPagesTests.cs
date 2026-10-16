using Gamesim.Presentation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The largest text's pages for a screen that is a column of rows (the owner's contract of
    /// 2026-10-15): the column's units - its rows, and a grid's lines one by one - dealt whole and in
    /// reading order, each with the gap before it that it has on the page, none before the first unit
    /// of a page, and a heading held with the first line it heads. The nomination's steps page this
    /// way (EpisodeHud.NominationPages.cs); the drawing and the reach are the PlayMode suites'.
    /// </summary>
    public sealed class LargestColumnPagesTests
    {
        [Test]
        public void AColumnThatFits_IsOnePage_WithEveryGapCounted()
        {
            var pages = LargestColumnPages.Deal(new[] { 60f, 30f, 80f, 80f }, new[] { 0f, 12f, 12f, 14f }, null, 288f);
            Assert.That(pages, Is.EqualTo(new[] { 0, 0, 0, 0 }), "60 + 12 + 30 + 12 + 80 + 14 + 80 = 288 holds in 288.");
            Assert.That(LargestPages.Count(pages), Is.EqualTo(1));
            Assert.That(LargestColumnPages.Deal(new[] { 60f, 30f, 80f, 80f }, new[] { 0f, 12f, 12f, 14f }, null, 287f),
                Is.EqualTo(new[] { 0, 0, 0, 1 }), "and not in 287: the gap before a unit is its own, a grid's 14 as well as the column's 12.");
        }

        [Test]
        public void TheUnitThatStartsAPage_HasNoGapBeforeIt()
        {
            // 100 + 12 + 100 = 212; with the third, 212 + 14 + 100 = 326 > 300: it starts page two, where
            // it and the fourth take 100 + 14 + 100 = 214 - not 228, which would have counted its gap.
            var pages = LargestColumnPages.Deal(new[] { 100f, 100f, 100f, 100f, 72f }, new[] { 0f, 12f, 14f, 14f, 14f }, null, 300f);
            Assert.That(pages, Is.EqualTo(new[] { 0, 0, 1, 1, 1 }), "214 + 14 + 72 = 300 holds the fifth on page two.");
            Assert.That(LargestPages.Count(pages), Is.EqualTo(2));
        }

        [Test]
        public void AHeading_IsNeverTheLastThingOnAPage()
        {
            var heights = new[] { 200f, 40f, 80f, 80f };
            var gaps = new[] { 0f, 12f, 12f, 14f };
            Assert.That(LargestColumnPages.Deal(heights, gaps, null, 300f), Is.EqualTo(new[] { 0, 0, 1, 1 }),
                "Unheld, the heading (40) fits after the first row (252) and ends the page without its first line.");
            Assert.That(LargestColumnPages.Deal(heights, gaps, new[] { false, true, false, false }, 300f), Is.EqualTo(new[] { 0, 1, 1, 1 }),
                "Held with its first line, it starts the next page with it: 40 + 12 + 80 + 14 + 80 = 226.");
        }

        [Test]
        public void AHeldPairThatCannotShareAnyPage_StillTakesItsTurn_InOrder()
        {
            var pages = LargestColumnPages.Deal(new[] { 50f, 40f, 400f, 50f }, new[] { 0f, 12f, 12f, 12f }, new[] { false, true, false, false }, 300f);
            Assert.That(pages, Is.EqualTo(new[] { 0, 1, 2, 3 }),
                "The heading moves to a page of its own rather than loop; the unit taller than a page has one alone; nothing is reordered.");
        }

        [Test]
        public void HalfAUnitOfRounding_DoesNotTurnAPage()
        {
            Assert.That(LargestColumnPages.Deal(new[] { 300f, 88.4f }, new[] { 0f, 12f }, null, 400f), Is.EqualTo(new[] { 0, 0 }), "400.4 in 400 is rounding.");
            Assert.That(LargestColumnPages.Deal(new[] { 300f, 88.6f }, new[] { 0f, 12f }, null, 400f), Is.EqualTo(new[] { 0, 1 }), "400.6 in 400 is not.");
        }

        [Test]
        public void NoUnits_AreOnePage()
        {
            Assert.That(LargestColumnPages.Deal(new float[0], new float[0], null, 300f), Is.Empty);
            Assert.That(LargestColumnPages.Deal(null, null, null, 300f), Is.Empty);
            Assert.That(LargestPages.Count(LargestColumnPages.Deal(new float[0], null, null, 300f)), Is.EqualTo(1));
        }
    }
}
