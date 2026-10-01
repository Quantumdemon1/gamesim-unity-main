using Gamesim.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The gate that stops one click both dismissing a ceremony card and walking the player under it.
    ///
    /// <para>Small enough to look obviously correct and it was not. The stamp is compared by
    /// subtracting it from <see cref="Time.frameCount"/>, so seeding it with
    /// <see cref="int.MinValue"/> - the natural choice for "never" - overflows on the first frame to
    /// a large negative number, which reads as <em>a card is on screen right now</em>. Nothing on
    /// screen, no ceremony running, and every click in the game silently swallowed for the first two
    /// billion frames.</para>
    /// </summary>
    public sealed class CeremonyOverlayGateTests
    {
        // Order matters here and cannot be left to chance. The gate is a frame stamp on a static,
        // the only way to un-show a card is the one play's start takes, and a whole fixture can run
        // inside one editor frame - so a showing test running first would leave the stamp on the
        // current frame and the at-rest test would read its own neighbour instead of the seed it is
        // checking.
        [Test, Order(1)]
        public void WithNoCeremonyOnScreenTheWorldTakesItsOwnClicks()
        {
            Assert.That(CeremonyOverlays.OnScreen, Is.False,
                "Nothing has been shown, so nothing may be holding the click. A gate that answers "
                + "'yes' here stops the player moving at all, and does it without a symptom to search for.");
        }

        [Test, Order(2)]
        public void AShowingCardHoldsTheClickForTheFrameItIsShownOn()
        {
            CeremonyOverlays.Showing();
            Assert.That(CeremonyOverlays.OnScreen, Is.True,
                "The click that dismisses a near-opaque card must not also reach the house behind it.");
        }

        /// <summary>
        /// The one way to un-show a card is the one play's start takes: the editor can begin a
        /// session without reloading the domain, and the stamp the last session left would
        /// otherwise be read against this session's frame count.
        /// </summary>
        [Test, Order(3)]
        public void PlaysStartForgetsTheLastSessionsCard()
        {
            CeremonyOverlays.Showing();
            CeremonyOverlays.Forget();
            Assert.That(CeremonyOverlays.OnScreen, Is.False, "A card the last session showed is not on this session's screen.");
        }

        [Test]
        public void TheStampHoldsThisFrameAndTheLastAndNeverAFrameStillToCome()
        {
            Assert.That(CeremonyOverlays.Holds(10, 10), Is.True, "Shown this frame.");
            Assert.That(CeremonyOverlays.Holds(10, 9), Is.True, "Shown last frame: script order between the card and the player is not defined.");
            Assert.That(CeremonyOverlays.Holds(10, 8), Is.False, "Two frames on, the card is gone.");
            Assert.That(CeremonyOverlays.Holds(0, -1000), Is.False, "Nothing shown yet.");
            Assert.That(CeremonyOverlays.Holds(0, int.MinValue), Is.False, "The seed that overflowed once.");
            // A session started without a domain reload, an hour after the last one showed a card.
            Assert.That(CeremonyOverlays.Holds(5, 214961), Is.False, "A stamp from a frame that has not happened is not a card on screen.");
            Assert.That(CeremonyOverlays.Holds(11, 12), Is.False, "Not even one frame ahead.");
        }
    }
}
