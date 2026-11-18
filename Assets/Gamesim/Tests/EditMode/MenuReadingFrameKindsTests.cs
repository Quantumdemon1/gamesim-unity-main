using System;
using System.Linq;
using Gamesim.Episode;
using Gamesim.Presentation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Menu slice 3, the review's change 5, against the HUD itself: every layout the season's HUD can
    /// dock a panel in has a kind in the one table, so a layout added without one fails here before
    /// it can open; and the frame the HUD lays out is the table's arithmetic on the HUD's own floor,
    /// at every text size. EditMode only: it reads EpisodeHud, which the Unity-free suite cannot build.
    /// </summary>
    public sealed class MenuReadingFrameKindsTests
    {
        [Test]
        public void EveryActivityLayoutHasAKind_AndTheTableListsNoOther()
        {
            var names = Enum.GetNames(typeof(EpisodeHud.ActivityLayout));
            foreach (var name in names)
                Assert.That(ReadingFrame.TryKind(name, out _), Is.True,
                    "EpisodeHud.ActivityLayout." + name + " has no kind: list it in ReadingFrame.Kinds as Reading or Quick.");
            Assert.That(ReadingFrame.Kinds.Keys.Except(names), Is.Empty, "The table names no layout the HUD does not have.");
            foreach (EpisodeHud.ActivityLayout layout in Enum.GetValues(typeof(EpisodeHud.ActivityLayout)))
                Assert.That(EpisodeHud.KindOf(layout), Is.EqualTo(layout == EpisodeHud.ActivityLayout.Reading ? PanelKind.Reading : PanelKind.Quick),
                    layout + "'s kind.");
        }

        [Test]
        public void TheHudsFloor_IsTheStatusLinesBandTop_AtEveryTextSize()
        {
            Assert.That(EpisodeHud.PanelFloor(1f), Is.EqualTo(182f).Within(.01f),
                "The cast strip's 14 + 96, a 14 gap, the 50-unit status line and an 8 gap.");
            foreach (var size in TextSizes.All)
            {
                float scale = TextSizes.Scale(size);
                Assert.That(EpisodeHud.PanelFloor(scale), Is.EqualTo(14f + 96f * scale + 14f + 50f * scale + 8f).Within(.01f), TextSizes.Name(size));
            }
        }

        [Test]
        public void TheReadingBox_IsTheTablesArithmeticOnTheHudsFloor_AtEveryTextSize()
        {
            foreach (var canvas in new[] { (1600f, 900f), (1385.6f, 1039.2f), (2133.3f, 900f) })
            foreach (var size in TextSizes.All)
            {
                float scale = TextSizes.Scale(size);
                var box = EpisodeHud.ReadingBox(canvas.Item1, canvas.Item2, scale);
                var expected = ReadingFrame.Place(canvas.Item1, canvas.Item2, EpisodeHud.LeftColumnX, EpisodeHud.PanelFloor(scale), 80f, EpisodeHud.FullFrameEdge);
                string at = canvas + " at " + TextSizes.Name(size) + " text";
                Assert.That(box.X, Is.EqualTo(expected.X).Within(.01f), at);
                Assert.That(box.Y, Is.EqualTo(expected.Y).Within(.01f), at);
                Assert.That(box.Width, Is.EqualTo(expected.Width).Within(.01f), at);
                Assert.That(box.Height, Is.EqualTo(expected.Height).Within(.01f), at + ": under the top bar's 80 units of headroom.");
                Assert.That(box.Height, Is.GreaterThanOrEqualTo(canvas.Item2 * .6f), at + ": a tall page.");
            }
        }

        [Test]
        public void TheFrameArrives_InAFifthOfASecondAtMost()
        {
            Assert.That(HudReveal.Duration, Is.GreaterThan(0f).And.LessThanOrEqualTo(.2f));
        }
    }
}
