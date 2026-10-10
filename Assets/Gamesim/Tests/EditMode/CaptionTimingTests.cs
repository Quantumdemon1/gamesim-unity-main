using Gamesim.Presentation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The caption reading time (PLAN A, A10): a caption the game takes down by itself holds for what
    /// it was authored to hold or for 1.2 seconds and 0.3 a word, whichever is longer, and half as long
    /// again under "Longer". Unity-free.
    /// </summary>
    public sealed class CaptionTimingTests
    {
        [TestCase(null, 0)]
        [TestCase("", 0)]
        [TestCase("   ", 0)]
        [TestCase("EVICTED", 1)]
        [TestCase("Riley Johnson wins the season.", 5)]
        [TestCase("  two\twords\n", 2)]
        [TestCase("VETO MEETING Taylor Kim used the veto.", 7)]
        public void Words_CountsEveryRunBetweenWhitespace(string text, int words) =>
            Assert.That(CaptionTiming.Words(text), Is.EqualTo(words));

        [Test]
        public void ReadingTime_IsOnePointTwoSecondsAndPointThreeAWord()
        {
            Assert.That(CaptionTiming.ReadingTime(""), Is.EqualTo(1.2).Within(1e-9));
            Assert.That(CaptionTiming.ReadingTime("one"), Is.EqualTo(1.5).Within(1e-9));
            Assert.That(CaptionTiming.ReadingTime("Maya Hassan and Riley Johnson are discussing the game."), Is.EqualTo(1.2 + 0.3 * 9).Within(1e-9));
        }

        [Test]
        public void Hold_IsTheLongerOfTheAuthoredHoldAndTheReadingTime()
        {
            // Two words read in 1.8 s: the strip's authored 2 s stands.
            Assert.That(CaptionTiming.Hold(2.0, "VETO USED", CaptionTime.Standard), Is.EqualTo(2.0).Within(1e-9));
            // Seven words read in 3.3 s: longer than the authored 2 s.
            Assert.That(CaptionTiming.Hold(2.0, "VETO MEETING Taylor Kim used the veto.", CaptionTime.Standard), Is.EqualTo(3.3).Within(1e-9));
            // A caption with no authored hold - the witnessed conversation's line - holds its reading time.
            Assert.That(CaptionTiming.Hold(0.0, "Maya and Riley are chatting.", CaptionTime.Standard), Is.EqualTo(2.7).Within(1e-9));
            Assert.That(CaptionTiming.Hold(-5.0, "", CaptionTime.Standard), Is.EqualTo(1.2).Within(1e-9), "An authored hold below nothing counts as nothing.");
        }

        [Test]
        public void Longer_IsHalfAsLongAgain_OnBothSidesOfTheMax()
        {
            Assert.That(CaptionTiming.Factor(CaptionTime.Standard), Is.EqualTo(1.0));
            Assert.That(CaptionTiming.Factor(CaptionTime.Longer), Is.EqualTo(1.5));
            Assert.That(CaptionTiming.Hold(2.0, "VETO USED", CaptionTime.Longer), Is.EqualTo(3.0).Within(1e-9));
            Assert.That(CaptionTiming.Hold(2.0, "VETO MEETING Taylor Kim used the veto.", CaptionTime.Longer), Is.EqualTo(4.95).Within(1e-9));
        }

        [Test]
        public void TheSetting_HasItsWordsItsControlsAndItsRecord()
        {
            Assert.That(CaptionTiming.Name(CaptionTime.Standard), Is.EqualTo("standard"));
            Assert.That(CaptionTiming.Name(CaptionTime.Longer), Is.EqualTo("longer"));
            Assert.That(CaptionTiming.Caption(CaptionTime.Standard), Is.EqualTo("Caption time: Standard  (change)"));
            Assert.That(CaptionTiming.Caption(CaptionTime.Longer), Is.EqualTo("Caption time: Longer  (change)"));
            Assert.That(CaptionTiming.FromRecord(1), Is.EqualTo(CaptionTime.Longer));
            Assert.That(CaptionTiming.FromRecord(0), Is.EqualTo(CaptionTime.Standard));
            Assert.That(CaptionTiming.FromRecord(7), Is.EqualTo(CaptionTime.Standard), "Anything but 1 is the standard time.");
            Assert.That(CaptionTiming.Key, Is.EqualTo("Gamesim.CaptionTime"));
        }
    }
}
