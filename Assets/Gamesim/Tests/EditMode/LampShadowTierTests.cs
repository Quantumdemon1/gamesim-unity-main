using Gamesim.Episode;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The lamps' shadow tiers (SB1, <see cref="VerificationViews.LampShadowTier"/>): a lamp asks for
    /// the smallest tier at which no frame of its room's lamps gets a lamp fewer pixels than the High
    /// tier gave. At today's pipeline (a 2048 atlas, tiers of 256, 512 and 1024, the follow spotlight
    /// at 256) that is Medium in every room, so a two-shot keeps 512 px a lamp, a room view keeps
    /// 256 or 512, the whole house keeps 128, and the follow spotlight's own map doubles. Plain C#:
    /// it runs in the Unity-free suite and under the editor's Mono.
    /// </summary>
    public sealed class LampShadowTierTests
    {
        private const int Atlas = 2048, Low = 256, Medium = 512, High = 1024, FollowSpot = 256;

        [TestCase(1, Atlas, VerificationViews.ShadowTierMedium)]
        [TestCase(2, Atlas, VerificationViews.ShadowTierMedium)]
        [TestCase(3, Atlas, VerificationViews.ShadowTierMedium)]
        [TestCase(1, 4096, VerificationViews.ShadowTierHigh)]
        [TestCase(1, 1024, VerificationViews.ShadowTierLow)]
        public void ALampAsksForTheSmallestTierNoFrameOfItsRoomMakesSofter(int lampsAtHome, int atlas, int tier)
        {
            Assert.That(VerificationViews.LampShadowTier(lampsAtHome, atlas, Low, Medium, High, FollowSpot), Is.EqualTo(tier));
            int asked = VerificationViews.TierResolution(tier, Low, Medium, High);
            for (int seen = 1; seen <= lampsAtHome; seen++)
                foreach (int spot in new[] { 0, FollowSpot })
                {
                    int now = VerificationViews.LampPixels(VerificationViews.LampFrame(seen, asked, atlas, spot), asked);
                    int before = VerificationViews.LampPixels(VerificationViews.LampFrame(seen, High, atlas, spot), High);
                    Assert.That(now, Is.GreaterThanOrEqualTo(before), seen + " of " + lampsAtHome + " lamps, spotlight " + spot + " px: no softer than High.");
                }
            if (tier == VerificationViews.ShadowTierLow) return;
            // One tier lower, some frame of the room's lamps would be softer than High made it.
            int lower = VerificationViews.TierResolution(tier - 1, Low, Medium, High);
            bool softer = false;
            for (int seen = 1; seen <= lampsAtHome; seen++)
                softer |= VerificationViews.LampPixels(VerificationViews.LampFrame(seen, lower, atlas, 0), lower)
                          < VerificationViews.LampPixels(VerificationViews.LampFrame(seen, High, atlas, 0), High);
            Assert.That(softer, Is.True, "The tier below would soften a frame, so it is the smallest that does not.");
        }

        /// <summary>
        /// Today's frames of a room's lamps and the follow spotlight, High against Medium: one or two
        /// lamps (a two-shot, a close zoom) keep 512 px at scale 1 instead of 2; three (the full view
        /// of the Living room, the Bedroom or HoH) keep 256 px at scale 2 instead of 4; the spotlight's
        /// map doubles in every one.
        /// </summary>
        [TestCase(1, 2, 1, 512, 128, 256)]
        [TestCase(2, 2, 1, 512, 128, 256)]
        [TestCase(3, 4, 2, 256, 64, 128)]
        public void ARoomsFrameKeepsItsLampPixelsAndSharpensTheSpotlight(int lamps, int scaleBefore, int scaleAfter, int lampPixels, int spotBefore, int spotAfter)
        {
            var before = VerificationViews.LampFrame(lamps, High, Atlas, FollowSpot);
            var after = VerificationViews.LampFrame(lamps, Medium, Atlas, FollowSpot);
            Assert.That(after.slicesRequested, Is.EqualTo(6 * lamps + 1), "Six faces a lamp and the spotlight's one.");
            Assert.That(after.slicesKept, Is.EqualTo(after.slicesRequested), "Nothing is dropped.");
            Assert.That(new[] { before.scale, before.largestSlice, before.smallestSlice }, Is.EqualTo(new[] { scaleBefore, lampPixels, spotBefore }), "High.");
            Assert.That(new[] { after.scale, after.largestSlice, after.smallestSlice }, Is.EqualTo(new[] { scaleAfter, lampPixels, spotAfter }), "Medium.");
        }

        [Test]
        public void TheWholeHouseKeepsItsLampsAt128AndHalvesItsScale()
        {
            // The fourteen lamps all on (the overview, the opening, the walk-out) and the follow spotlight.
            var before = VerificationViews.LampFrame(14, High, Atlas, FollowSpot);
            var after = VerificationViews.LampFrame(14, Medium, Atlas, FollowSpot);
            Assert.That(after.slicesKept, Is.EqualTo(85));
            Assert.That(new[] { before.scale, before.largestSlice, before.smallestSlice }, Is.EqualTo(new[] { 8, 128, 32 }), "High: reduced by 8.");
            Assert.That(new[] { after.scale, after.largestSlice, after.smallestSlice }, Is.EqualTo(new[] { 4, 128, 64 }), "Medium: reduced by 4, the lamps as before.");
        }
    }
}
