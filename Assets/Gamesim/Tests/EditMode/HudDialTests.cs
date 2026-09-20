using Gamesim.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The conversation dial's geometry, which is the part of it that can be wrong without anyone
    /// noticing until a screenshot: seats that collide, a block too small to hold its own petals, or
    /// a ring that runs the wrong way round and puts "Chat" somewhere other than the top.
    /// </summary>
    public sealed class HudDialTests
    {
        private GameObject host;

        [SetUp]
        public void CreateHost() => host = new GameObject("Dial host", typeof(RectTransform));

        [TearDown]
        public void DestroyHost() { if (host != null) Object.DestroyImmediate(host); }

        [Test]
        public void FirstSeatIsAtTheTopAndTheRingRunsClockwise()
        {
            var dial = HudPrimitives.Radial("Dial", host.transform, null, 7, 1f);

            var top = dial.Seat(0);
            Assert.That(top.x, Is.EqualTo(0f).Within(0.01f), "The first petal sits directly above the hub.");
            Assert.That(top.y, Is.EqualTo(HudPrimitives.DialRadius).Within(0.01f));

            // Clockwise on screen is to the right and then down: x grows, y falls.
            var second = dial.Seat(1);
            Assert.That(second.x, Is.GreaterThan(0f), "The second petal is to the right of the first.");
            Assert.That(second.y, Is.LessThan(top.y), "The second petal is below the first.");

            var last = dial.Seat(6);
            Assert.That(last.x, Is.LessThan(0f), "The last petal closes the ring on the left.");
        }

        [Test]
        public void SevenSeatedPetalsDoNotCollideAtEitherTextSize()
        {
            foreach (float scale in new[] { 1f, 1.2f })
            {
                var dial = HudPrimitives.Radial("Dial", host.transform, null, 7, scale);
                for (int a = 0; a < 7; a++)
                for (int b = a + 1; b < 7; b++)
                {
                    var first = Box(dial.Seat(a), dial.Petal);
                    var second = Box(dial.Seat(b), dial.Petal);
                    Assert.That(first.Overlaps(second), Is.False,
                        "At scale " + scale + ", seat " + a + " " + first + " overlaps seat " + b + " " + second + ".");
                }
            }
        }

        [Test]
        public void TheBlockHoldsEveryPetalItSeats()
        {
            var dial = HudPrimitives.Radial("Dial", host.transform, null, 7, 1.2f);
            var block = new Rect(-dial.Root.sizeDelta.x * .5f, -dial.Root.sizeDelta.y * .5f,
                dial.Root.sizeDelta.x, dial.Root.sizeDelta.y);

            for (int i = 0; i < 7; i++)
            {
                var petal = Box(dial.Seat(i), dial.Petal);
                Assert.That(block.xMin, Is.LessThanOrEqualTo(petal.xMin + 0.01f), "Seat " + i + " runs past the block's left edge.");
                Assert.That(block.xMax, Is.GreaterThanOrEqualTo(petal.xMax - 0.01f), "Seat " + i + " runs past the block's right edge.");
                Assert.That(block.yMin, Is.LessThanOrEqualTo(petal.yMin + 0.01f), "Seat " + i + " runs past the block's bottom edge.");
                Assert.That(block.yMax, Is.GreaterThanOrEqualTo(petal.yMax - 0.01f), "Seat " + i + " runs past the block's top edge.");
            }
        }

        /// <summary>
        /// The panel the dial is drawn in is 790 wide at every aspect ratio, and its scroll viewport
        /// masks anything wider. A dial that does not fit at the larger text size loses its side
        /// petals to that mask rather than reporting anything.
        /// </summary>
        [Test]
        public void TheBlockFitsTheEpisodePanelAtTheLargerTextSize()
        {
            const float PanelWidth = 790f;
            // Panel inset 20 either side, viewport inset 18 for the scrollbar, content padding 8.
            const float Usable = PanelWidth - 20f - 20f - 18f - 8f - 8f;
            var dial = HudPrimitives.Radial("Dial", host.transform, null, 7, 1.2f);
            Assert.That(dial.Root.sizeDelta.x, Is.LessThanOrEqualTo(Usable),
                "The dial is " + dial.Root.sizeDelta.x + " wide where the panel offers " + Usable + ".");
        }

        [Test]
        public void PlacingMoreThanTheSeatsWrapsRatherThanThrowing()
        {
            var dial = HudPrimitives.Radial("Dial", host.transform, null, 7, 1f);
            for (int i = 0; i < 7; i++) dial.Place(NewPetal());
            Assert.That(dial.Taken, Is.EqualTo(7));
            Assert.That(dial.Seat(7), Is.EqualTo(dial.Seat(0)));
        }

        private RectTransform NewPetal()
        {
            var rect = new GameObject("Petal", typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(host.transform, false);
            return rect;
        }

        private static Rect Box(Vector2 centre, Vector2 size) =>
            new Rect(centre.x - size.x * .5f, centre.y - size.y * .5f, size.x, size.y);
    }
}
