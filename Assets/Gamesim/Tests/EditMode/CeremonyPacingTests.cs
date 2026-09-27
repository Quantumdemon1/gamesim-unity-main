using Gamesim.Presentation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The two tempos the key ceremony and the live eviction can play at.
    ///
    /// <para>The reveals spend a result that is already decided, so their length is all there is to
    /// them: suspense on a key is the time between one name and the next. Three promises hold the
    /// table together - the quick setting is quicker at every beat, not just overall; a big house
    /// gets a shorter beat per name, so sixteen keys are still a scene and not a wait; and speeding
    /// a reveal up actually speeds it up.</para>
    /// </summary>
    public sealed class CeremonyPacingTests
    {
        /// <summary>Every house size the setup allows, and a few either side of the thresholds.</summary>
        private static readonly int[] Houses = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16 };

        [Test]
        public void QuickIsShorterThanSuspensefulAtEveryStep()
        {
            const CeremonyPace quick = CeremonyPace.Quick, suspense = CeremonyPace.Suspenseful;
            Assert.That(CeremonyPacing.KeyIntro(quick), Is.LessThan(CeremonyPacing.KeyIntro(suspense)), "The Head of Household's line.");
            Assert.That(CeremonyPacing.LastKeyBeat(quick), Is.LessThan(CeremonyPacing.LastKeyBeat(suspense)), "The beat before the last key.");
            Assert.That(CeremonyPacing.BlockHold(quick), Is.LessThan(CeremonyPacing.BlockHold(suspense)), "The block.");
            Assert.That(CeremonyPacing.VoteIntro(quick), Is.LessThan(CeremonyPacing.VoteIntro(suspense)), "The eviction's title and block.");
            Assert.That(CeremonyPacing.LastVoteBeat(quick), Is.LessThan(CeremonyPacing.LastVoteBeat(suspense)), "The beat before the house's last vote.");
            Assert.That(CeremonyPacing.TieBeat(quick), Is.LessThan(CeremonyPacing.TieBeat(suspense)), "A tie announced.");
            Assert.That(CeremonyPacing.ResultHold(quick), Is.LessThan(CeremonyPacing.ResultHold(suspense)), "The result.");
            foreach (int count in Houses)
            {
                Assert.That(CeremonyPacing.PerKey(quick, count), Is.LessThan(CeremonyPacing.PerKey(suspense, count)),
                    "Each of " + count + " keys.");
                Assert.That(CeremonyPacing.PerVote(quick, count), Is.LessThan(CeremonyPacing.PerVote(suspense, count)),
                    "Each of " + count + " votes.");
            }
        }

        [Test]
        public void EachKeyAndEachVoteIsShorterInABigHouse()
        {
            foreach (var pace in new[] { CeremonyPace.Suspenseful, CeremonyPace.Quick })
                for (int count = 1; count < 16; count++)
                {
                    Assert.That(CeremonyPacing.PerKey(pace, count + 1), Is.LessThanOrEqualTo(CeremonyPacing.PerKey(pace, count)),
                        pace + ": one more key never makes each key longer (" + count + " to " + (count + 1) + ").");
                    Assert.That(CeremonyPacing.PerVote(pace, count + 1), Is.LessThanOrEqualTo(CeremonyPacing.PerVote(pace, count)),
                        pace + ": one more vote never makes each vote longer (" + count + " to " + (count + 1) + ").");
                }

            // And it does shorten: in a full house of sixteen, each of the thirteen keys and each of the
            // thirteen votes holds for less than one of a small house's three.
            Assert.That(CeremonyPacing.PerKey(CeremonyPace.Suspenseful, 13), Is.LessThan(CeremonyPacing.PerKey(CeremonyPace.Suspenseful, 3)));
            Assert.That(CeremonyPacing.PerVote(CeremonyPace.Suspenseful, 13), Is.LessThan(CeremonyPacing.PerVote(CeremonyPace.Suspenseful, 3)));
        }

        [Test]
        public void SpeedingARevealUpMakesItFaster()
        {
            Assert.That(CeremonyPacing.SpeedUp, Is.GreaterThan(1f),
                "The card's clock runs at this multiple while sped up; at one or below, Space would do nothing or slow it down.");
        }
    }
}
