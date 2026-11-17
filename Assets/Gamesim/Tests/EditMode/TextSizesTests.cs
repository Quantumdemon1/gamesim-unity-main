using System.Linq;
using Gamesim.Presentation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The three text sizes (PLAN A, A8; the lead's decision A-3): their scales, their words, the
    /// Settings row that chooses them, and the preference a player's choice is kept under,
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
        public void TheTextSizeRow_ReadsEachSizeByItsOwnWord()
        {
            // Menu slice 4: one row, "Text size", whose value is the size in use; the verifier and the
            // accessibility suites choose a size by it.
            Assert.That(SettingRowWords.TextSizeRow, Is.EqualTo("Text size"));
            Assert.That(TextSizes.All.Select(SettingRowWords.TextSizeValue), Is.EqualTo(new[] { "Standard", "Larger", "Largest" }));
            Assert.That(SettingRowWords.TextSizeValues.Distinct().Count(), Is.EqualTo(3), "Three sizes, three words.");
        }

        [Test]
        public void TheTextSizeRow_StepsThroughTheSizesInOrder_AndRound()
        {
            foreach (var size in TextSizes.All)
            {
                int at = SettingRowWords.IndexOf(size);
                Assert.That(TextSizes.All[at], Is.EqualTo(size), "The row's place is the size's.");
                Assert.That(TextSizes.All[SettingsWords.ChoiceStepped(at, TextSizes.All.Length, 1)], Is.Not.EqualTo(size), "Right moves to another size.");
            }
            Assert.That(TextSizes.All[SettingsWords.ChoiceStepped(SettingRowWords.IndexOf(TextSize.Largest), 3, 1)], Is.EqualTo(TextSize.Standard), "Past the largest, round to the standard.");
            Assert.That(TextSizes.All[SettingsWords.ChoiceStepped(SettingRowWords.IndexOf(TextSize.Standard), 3, -1)], Is.EqualTo(TextSize.Largest), "Left of the standard, the largest.");
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
