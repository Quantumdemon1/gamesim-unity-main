using System;
using Gamesim.Presentation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The one way out's words and measures (menu slice 2, change 1): "Back to &lt;place&gt;" with a
    /// place always named, one "Close", each shared place spelled once, and a control tall enough for
    /// its label at every text size. Unity-free, so the subset runs it on every push.
    /// </summary>
    public sealed class WayOutWordsTests
    {
        private static readonly float[] TextScales = { 1f, 1.2f, 1.4f };

        [Test]
        public void BackTo_NamesThePlace()
        {
            Assert.That(WayOut.BackTo(WayOut.Cast), Is.EqualTo("Back to the cast"));
            Assert.That(WayOut.BackTo(WayOut.MainMenu), Is.EqualTo("Back to the main menu"));
            Assert.That(WayOut.BackTo(WayOut.House), Is.EqualTo("Back to the house"));
            Assert.That(WayOut.BackTo("  the briefing "), Is.EqualTo("Back to the briefing"));
        }

        [Test]
        public void BackTo_RefusesAWayBackWithNoPlace()
        {
            Assert.Throws<ArgumentException>(() => WayOut.BackTo(null));
            Assert.Throws<ArgumentException>(() => WayOut.BackTo("   "));
        }

        [Test]
        public void BackToKeeping_SaysWhatIsKept_InSliceOnesWords()
        {
            Assert.That(WayOut.BackToKeeping(WayOut.Cast, "draft"), Is.EqualTo("Back to the cast (draft kept)"));
            Assert.That(WayOut.BackToKeeping(WayOut.Cast, "draft"), Is.EqualTo(CharacterCreatorBackCaption),
                "the creator's way back reads as slice 1 left it");
            Assert.Throws<ArgumentException>(() => WayOut.BackToKeeping(WayOut.Cast, ""));
        }

        // Slice 1's caption, spelled here rather than read from CharacterCreator, which the subset does not compile.
        private const string CharacterCreatorBackCaption = "Back to the cast (draft kept)";

        [Test]
        public void IsWayBack_ReadsOnlyBackToAPlace()
        {
            Assert.That(WayOut.IsWayBack("Back to the house"), Is.True);
            Assert.That(WayOut.IsWayBack("Back to the cast (draft kept)"), Is.True);
            foreach (var other in new[] { "Back", "Back to ", "Back to   ", "Close", "Done", "Return to the house", "back to the house", null })
                Assert.That(WayOut.IsWayBack(other), Is.False, "\"" + other + "\" is not a way back");
        }

        [Test]
        public void Close_IsOneWordWithNoKeyInIt()
        {
            Assert.That(WayOut.CloseCaption, Is.EqualTo("Close"));
            Assert.That(WayOut.BackObjectName, Is.Not.EqualTo(WayOut.CloseObjectName));
        }

        [Test]
        public void Measures_FitTheLabelAtEveryTextSize()
        {
            foreach (var scale in TextScales)
            {
                float font = WayOut.FontSize(scale);
                Assert.That(font, Is.GreaterThanOrEqualTo(WayOut.MinFontSize), "no caption under 14 at scale " + scale);
                Assert.That(WayOut.Height(scale), Is.GreaterThanOrEqualTo(font * 1.3f), "the label box is 1.3 times the font at scale " + scale);
                Assert.That(WayOut.Inset(scale), Is.GreaterThan(0f));
            }
            Assert.That(WayOut.FontSize(1.4f), Is.GreaterThan(WayOut.FontSize(1f)), "larger text draws a larger caption");
        }

        [Test]
        public void Width_GrowsWithTheCaptionTheTextSizeTheArrowAndTheHint()
        {
            string shortCaption = WayOut.BackTo(WayOut.House), longCaption = WayOut.BackToKeeping(WayOut.Cast, "draft");
            Assert.That(WayOut.Width(longCaption, 1f, true), Is.GreaterThan(WayOut.Width(shortCaption, 1f, true)));
            Assert.That(WayOut.Width(shortCaption, 1.4f, true), Is.GreaterThan(WayOut.Width(shortCaption, 1f, true)));
            Assert.That(WayOut.Width(shortCaption, 1f, true), Is.GreaterThan(WayOut.Width(shortCaption, 1f, false)));
            Assert.That(WayOut.Width(WayOut.CloseCaption, 1f, false, "Esc"), Is.GreaterThan(WayOut.Width(WayOut.CloseCaption, 1f, false)));
            // Room for every character at a generous half-em each, so the caption is never cut.
            foreach (var scale in TextScales)
                Assert.That(WayOut.Width(longCaption, scale, true), Is.GreaterThan(longCaption.Length * WayOut.FontSize(scale) * 0.5f));
        }
    }
}
