using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The trait table, checked against the web game's <c>TRAIT_STAT_BOOSTS</c>.
    ///
    /// <para>These are parity tests rather than design tests: the value of the table is that a
    /// character built here and one built in the web game come out identical, so what is asserted
    /// is agreement with the source, including where the source is inconvenient.</para>
    /// </summary>
    public sealed class WebTraitsTests
    {
        [Test]
        public void AllSeventeenWebTraitsArePresent()
        {
            Assert.That(WebTraits.Boosts.Count, Is.EqualTo(17),
                "The web game defines seventeen traits; a missing one silently stops boosting.");
            foreach (var trait in new[]
            {
                "Competitive", "Strategic", "Loyal", "Emotional", "Funny", "Charming",
                "Manipulative", "Analytical", "Impulsive", "Deceptive", "Social", "Introverted",
                "Stubborn", "Flexible", "Intuitive", "Sneaky", "Confrontational",
            })
                Assert.That(WebTraits.Known(trait), Is.True, trait + " is missing from the table.");
        }

        [Test]
        public void EveryBoostNamesRealStats()
        {
            var stats = new ContestantStats();
            foreach (var entry in WebTraits.Boosts)
            {
                // Get returns 0 for a name it does not recognise, and every stat starts at 5, so a
                // zero here means the column named a stat that does not exist.
                Assert.That(WebTraits.Get(stats, entry.Value.Primary), Is.Not.Zero,
                    entry.Key + " has an unknown primary stat: " + entry.Value.Primary);
                Assert.That(WebTraits.Get(stats, entry.Value.Secondary), Is.Not.Zero,
                    entry.Key + " has an unknown secondary stat: " + entry.Value.Secondary);
            }
        }

        [Test]
        public void PrimaryRaisesByTwoAndSecondaryByOne()
        {
            var stats = new ContestantStats();
            WebTraits.Apply(stats, "Strategic", true);      // mental primary, strategic secondary
            Assert.That(stats.mental, Is.EqualTo(7));
            Assert.That(stats.strategic, Is.EqualTo(6));
            Assert.That(stats.physical, Is.EqualTo(5), "An unrelated stat moved.");
        }

        [Test]
        public void RemovingATraitGivesTheBoostBack()
        {
            var stats = new ContestantStats();
            WebTraits.Apply(stats, "Charming", true);
            WebTraits.Apply(stats, "Charming", false);
            Assert.That(stats.social, Is.EqualTo(5));
            Assert.That(stats.strategic, Is.EqualTo(5));
        }

        [Test]
        public void StatsAreHeldBetweenOneAndTen()
        {
            var high = new ContestantStats { social = 10, strategic = 10 };
            WebTraits.Apply(high, "Charming", true);
            Assert.That(high.social, Is.EqualTo(10));
            Assert.That(high.strategic, Is.EqualTo(10));

            var low = new ContestantStats { social = 1, strategic = 1 };
            WebTraits.Apply(low, "Charming", false);
            Assert.That(low.social, Is.EqualTo(1));
            Assert.That(low.strategic, Is.EqualTo(1));
        }

        /// <summary>
        /// The clamps are not symmetric, and this pins that deliberately.
        ///
        /// <para>A stat at 9 that gains +2 is held at 10, and removing the trait subtracts the full
        /// 2, finishing at 8. The web game does exactly this. The test exists so that nobody
        /// "fixes" it later and quietly makes characters built here differ from characters built
        /// there.</para>
        /// </summary>
        [Test]
        public void ClampingIsLossyOnTheWayBack_MatchingTheWebGame()
        {
            var stats = new ContestantStats { mental = 9 };
            WebTraits.Apply(stats, "Strategic", true);
            Assert.That(stats.mental, Is.EqualTo(10), "Clamped on the way up.");
            WebTraits.Apply(stats, "Strategic", false);
            Assert.That(stats.mental, Is.EqualTo(8),
                "The web game gives back the full boost regardless of the clamp, and so do we.");
        }

        [Test]
        public void NoTraitBoostsTheCompetitionStat()
        {
            // Not a rule anyone chose here — it is the shape of the source table, asserted so that a
            // future edit to it is a decision rather than a slip.
            Assert.That(WebTraits.Boosts.Values.Any(
                b => b.Primary == "competition" || b.Secondary == "competition"), Is.False);
        }

        [Test]
        public void UnknownTraitsAreIgnoredRatherThanThrowing()
        {
            var stats = new ContestantStats();
            Assert.DoesNotThrow(() => WebTraits.Apply(stats, "Nonexistent", true));
            Assert.That(stats.social, Is.EqualTo(5));
            Assert.That(WebTraits.Known(null), Is.False);
        }

        [Test]
        public void TheShippedCastsTraitsAreAllRecognised()
        {
            var state = ContentCatalog.Create(1701);
            foreach (var contestant in state.contestants)
            foreach (var trait in contestant.traits)
                Assert.That(WebTraits.Known(trait), Is.True,
                    contestant.name + " carries '" + trait + "', which the trait table does not define.");
        }
    }
}
