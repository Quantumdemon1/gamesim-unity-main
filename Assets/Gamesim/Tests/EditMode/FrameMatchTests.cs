using Gamesim.Presentation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The wide frame's rule on its own (PLAN A, A12b): a canvas keeps its own match on 16:9 and on
    /// every narrower frame the game is held to, and matches height on a frame wider than 16:9 by
    /// more than a hair. Unity-free; the frames test holds the HUD to it at 21:9.
    /// </summary>
    public sealed class FrameMatchTests
    {
        [TestCase(1600, 900)]
        [TestCase(1920, 1080)]
        [TestCase(2560, 1440)]
        [TestCase(1280, 720)]
        [TestCase(1366, 768)]
        [TestCase(1280, 800)]
        [TestCase(1200, 900)]
        [TestCase(640, 480)]
        [TestCase(1080, 1920)]
        public void A16By9OrNarrowerFrame_KeepsTheCanvassOwnMatch(int width, int height)
        {
            Assert.That(FrameMatch.IsWide(width, height), Is.False);
            Assert.That(FrameMatch.For(width, height, .5f), Is.EqualTo(.5f));
            Assert.That(FrameMatch.For(width, height, 1f), Is.EqualTo(1f), "A canvas set to match height keeps it too.");
            Assert.That(FrameMatch.For(width, height, 0f), Is.EqualTo(0f));
        }

        [TestCase(2560, 1080)]
        [TestCase(3440, 1440)]
        [TestCase(5120, 1440)]
        [TestCase(1920, 800)]
        public void AFrameWiderThan16By9_MatchesHeight(int width, int height)
        {
            Assert.That(FrameMatch.IsWide(width, height), Is.True);
            Assert.That(FrameMatch.For(width, height, .5f), Is.EqualTo(FrameMatch.WideMatch));
            Assert.That(FrameMatch.WideMatch, Is.EqualTo(1f), "Wide is the height alone: the reference's 900 at any width.");
        }

        [Test]
        public void TheLineIsOnePartInAHundredPast16By9()
        {
            // 1600 tall: 16:9 is 2844.4 wide; one in a hundred past it is 2872.9.
            Assert.That(FrameMatch.IsWide(2872, 1600), Is.False);
            Assert.That(FrameMatch.IsWide(2873, 1600), Is.True);
        }

        [TestCase(0, 900)]
        [TestCase(1600, 0)]
        [TestCase(-1, -1)]
        public void AFrameWithNoSize_KeepsTheCanvassOwnMatch(int width, int height) =>
            Assert.That(FrameMatch.For(width, height, .5f), Is.EqualTo(.5f));
    }
}
