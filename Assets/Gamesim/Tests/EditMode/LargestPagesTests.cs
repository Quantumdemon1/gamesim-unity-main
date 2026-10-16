using Gamesim.Presentation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The largest text's pages (the owner's contract of 2026-10-15): which size may page, and how a
    /// screen's sections are dealt onto pages - whole, in reading order, each page taking the next
    /// while it fits. The pager's drawing and its reach (EpisodeHud.LargestPages.cs) are the PlayMode
    /// suites'; this is its arithmetic (LargestPages), which every screen family shares.
    /// </summary>
    public sealed class LargestPagesTests
    {
        [Test]
        public void OnlyTheLargestText_MayPage()
        {
            Assert.That(LargestPages.At(TextSizes.Scale(TextSize.Standard)), Is.False, "The standard text never pages.");
            Assert.That(LargestPages.At(TextSizes.Scale(TextSize.Larger)), Is.False, "The larger text never pages.");
            Assert.That(LargestPages.At(TextSizes.Scale(TextSize.Largest)), Is.True, "The largest text may.");
        }

        [Test]
        public void SectionsThatFit_AreOnePage()
        {
            var breaks = LargestPages.Breaks(new[] { 100f, 40f, 200f }, 10f, 360f);
            Assert.That(breaks, Is.EqualTo(new[] { 0, 0, 0 }), "100 + 10 + 40 + 10 + 200 = 360 holds in 360.");
            Assert.That(LargestPages.Count(breaks), Is.EqualTo(1));
        }

        [Test]
        public void TheSectionThatWouldNotFit_StartsTheNextPage_Whole()
        {
            var breaks = LargestPages.Breaks(new[] { 200f, 40f, 200f, 150f }, 14f, 460f);
            Assert.That(breaks, Is.EqualTo(new[] { 0, 0, 1, 1 }),
                "200 + 14 + 40 = 254, and 254 + 14 + 200 = 468 is past 460: the third starts the second page, whole, and the fourth joins it (200 + 14 + 150 = 364).");
            Assert.That(LargestPages.Count(breaks), Is.EqualTo(2));
        }

        [Test]
        public void HalfAUnitOfRounding_DoesNotTurnAPage()
        {
            Assert.That(LargestPages.Breaks(new[] { 300f, 100.4f }, 0f, 400f), Is.EqualTo(new[] { 0, 0 }), "400.4 in 400 is rounding.");
            Assert.That(LargestPages.Breaks(new[] { 300f, 100.6f }, 0f, 400f), Is.EqualTo(new[] { 0, 1 }), "400.6 in 400 is not.");
        }

        [Test]
        public void ASectionTallerThanAPage_HasAPageOfItsOwn_AndTheOrderHolds()
        {
            var breaks = LargestPages.Breaks(new[] { 50f, 500f, 50f, 50f }, 10f, 300f);
            Assert.That(breaks, Is.EqualTo(new[] { 0, 1, 2, 2 }), "Never split, never reordered: the tall one alone, the rest after it.");
            Assert.That(LargestPages.Count(breaks), Is.EqualTo(3));
        }

        [Test]
        public void NoSections_AreOnePage()
        {
            Assert.That(LargestPages.Breaks(new float[0], 10f, 300f), Is.Empty);
            Assert.That(LargestPages.Count(new int[0]), Is.EqualTo(1));
            Assert.That(LargestPages.Count(null), Is.EqualTo(1));
        }
    }
}
