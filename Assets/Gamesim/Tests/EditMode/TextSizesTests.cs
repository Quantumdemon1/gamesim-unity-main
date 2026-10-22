using System.Linq;
using Gamesim.Presentation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The three text sizes (PLAN A, A8; the lead's decision A-3): their scales, their words, the two
    /// controls the settings offer at each, and the preference a player's choice is kept under,
    /// including the larger text an earlier build kept as a boolean. Unity-free.
    /// </summary>
    public sealed class TextSizesTests
    {
        [Test]
        public void TheSizes_Are1_1Point2And1Point4()
        {
            Assert.That(TextSizes.All, Is.EqualTo(new[] { TextSize.Standard, TextSize.Larger, TextSize.Largest }));
            Assert.That(TextSizes.All.Select(TextSizes.Scale), Is.EqualTo(new[] { 1f, 1.2f, 1.4f }));
            Assert.That(TextSizes.All.Select(TextSizes.Name), Is.EqualTo(new[] { "standard", "larger", "largest" }));
        }

        [Test]
        public void TheTwoOldCaptions_KeepTheirWords_AndTheThirdIsNew()
        {
            // Contracts: the verifier, the look sheet and the accessibility suites press these by name.
            Assert.That(TextSizes.Caption(TextSize.Standard), Is.EqualTo("Use standard text"));
            Assert.That(TextSizes.Caption(TextSize.Larger), Is.EqualTo("Use larger text"));
            Assert.That(TextSizes.Caption(TextSize.Largest), Is.EqualTo("Use largest text"));
            Assert.That(TextSizes.All.Select(TextSizes.Caption).Distinct().Count(), Is.EqualTo(3), "Three controls, three captions.");
        }

        [Test]
        public void TheSettings_OfferTheTwoSizesNotInUse_SmallestFirst()
        {
            Assert.That(TextSizes.Others(TextSize.Standard), Is.EqualTo(new[] { TextSize.Larger, TextSize.Largest }));
            Assert.That(TextSizes.Others(TextSize.Larger), Is.EqualTo(new[] { TextSize.Standard, TextSize.Largest }));
            Assert.That(TextSizes.Others(TextSize.Largest), Is.EqualTo(new[] { TextSize.Standard, TextSize.Larger }));
            foreach (var size in TextSizes.All)
                Assert.That(TextSizes.Others(size), Has.No.Member(size), "The size in use offers no control of its own.");
        }

        [TestCase(null, null, TextSize.Standard)]
        [TestCase(null, 0, TextSize.Standard)]
        [TestCase(null, 1, TextSize.Larger)]
        [TestCase(0, 1, TextSize.Standard)]
        [TestCase(1, 0, TextSize.Larger)]
        [TestCase(2, 1, TextSize.Largest)]
        [TestCase(2, null, TextSize.Largest)]
        [TestCase(3, 1, TextSize.Larger)]
        [TestCase(-1, null, TextSize.Standard)]
        public void AKeptChoice_ReadsAsItsSize_AndTheOldBooleanCarriesOver(int? size, int? legacy, TextSize expected) =>
            Assert.That(TextSizes.FromRecord(size, legacy), Is.EqualTo(expected));

        [Test]
        public void TheOldBoolean_IsStillWritten_ForABuildFromBeforeTheThirdSize()
        {
            Assert.That(TextSizes.LegacyRecord(TextSize.Standard), Is.EqualTo(0));
            Assert.That(TextSizes.LegacyRecord(TextSize.Larger), Is.EqualTo(1));
            Assert.That(TextSizes.LegacyRecord(TextSize.Largest), Is.EqualTo(1), "The nearest an earlier build knows.");
            foreach (var size in TextSizes.All)
                Assert.That(TextSizes.FromRecord((int)size, TextSizes.LegacyRecord(size)), Is.EqualTo(size), "Written and read back.");
            Assert.That(TextSizes.SizeKey, Is.EqualTo("Gamesim.TextSize"));
            Assert.That(TextSizes.LegacyKey, Is.EqualTo("Gamesim.LargeText"));
        }
    }
}
