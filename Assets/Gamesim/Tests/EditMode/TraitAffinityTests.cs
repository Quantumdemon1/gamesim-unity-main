using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The trait table (NPC-AGENCY-PLAN.md): every trait the web game knows has a reception and an
    /// opinion of others; kindred temperaments start close and clashing ones far apart, before a
    /// word is said; and the table is directional, so a schemer's view of the loyal is not the loyal's
    /// view of the schemer.
    /// </summary>
    public sealed class TraitAffinityTests
    {
        [Test]
        public void EveryTraitInTheTableIsOneTheWebGameKnowsAndEveryKnownTraitHasAnOpinion()
        {
            foreach (var trait in TraitAffinity.Reception.Keys) Assert.That(WebTraits.Known(trait), Is.True, trait);
            foreach (var observer in TraitAffinity.Affinity.Keys)
            {
                Assert.That(WebTraits.Known(observer), Is.True, observer);
                foreach (var other in TraitAffinity.Affinity[observer].Keys) Assert.That(WebTraits.Known(other), Is.True, observer + " on " + other);
            }
            foreach (var trait in WebTraits.Boosts.Keys) Assert.That(TraitAffinity.Affinity.ContainsKey(trait), Is.True, trait + " has no opinions.");
        }

        [Test]
        public void KindredTemperamentsStartCloseAndClashingOnesFarApart()
        {
            int loyalOnLoyal = TraitAffinity.Compatibility(new[] { "Loyal", "Emotional" }, new[] { "Loyal", "Funny" });
            int loyalOnSchemer = TraitAffinity.Compatibility(new[] { "Loyal", "Emotional" }, new[] { "Deceptive", "Sneaky" });
            int analystOnAnalyst = TraitAffinity.Compatibility(new[] { "Analytical", "Strategic" }, new[] { "Analytical", "Strategic" });
            Assert.That(loyalOnLoyal, Is.GreaterThan(0), "Loyalty and humour are liked, and the loyal warm to the loyal.");
            Assert.That(loyalOnSchemer, Is.LessThan(-4), "The loyal cannot abide a schemer, and nobody much likes one.");
            Assert.That(analystOnAnalyst, Is.GreaterThan(0), "Like warms to like.");
            Assert.That(loyalOnLoyal, Is.GreaterThan(loyalOnSchemer));
        }

        [Test]
        public void TheTableIsDirectional()
        {
            int schemerOnLoyal = TraitAffinity.Compatibility(new[] { "Manipulative" }, new[] { "Loyal" });
            int loyalOnSchemer = TraitAffinity.Compatibility(new[] { "Loyal" }, new[] { "Manipulative" });
            Assert.That(schemerOnLoyal, Is.GreaterThan(loyalOnSchemer), "A manipulator sees an easy mark; the loyal see a snake.");
        }

        [Test]
        public void ReceptionCountsForEverybodyAndOpinionsOnlyForTheObserver()
        {
            Assert.That(TraitAffinity.Compatibility(new string[0], new[] { "Charming" }), Is.EqualTo(TraitAffinity.Reception["Charming"]),
                "With no traits of your own you still find charm charming.");
            Assert.That(TraitAffinity.Compatibility(new[] { "Introverted" }, new[] { "Competitive" }), Is.EqualTo(0), "Nothing to say either way.");
            Assert.That(TraitAffinity.Compatibility((string[])null, null), Is.EqualTo(0));
            Assert.That(TraitAffinity.Compatibility((ContestantState)null, null), Is.EqualTo(0));
            Assert.That(TraitAffinity.Compatibility(new[] { "Loyal" }, new[] { "loyal" }), Is.EqualTo(TraitAffinity.Reception["Loyal"] + 2), "Case does not matter.");
        }

        [Test]
        public void TheShippedCastSpreadsAcrossTheScale()
        {
            var s = ContentCatalog.Create(31);
            var npcs = s.contestants.Where(c => !c.isPlayer).ToList();
            var values = npcs.SelectMany(a => npcs.Where(b => b.id != a.id).Select(b => TraitAffinity.Compatibility(a, b))).ToList();
            Assert.That(values.Max(), Is.GreaterThan(0), "Somebody in the house takes to somebody.");
            Assert.That(values.Min(), Is.LessThan(0), "and somebody grates on somebody.");
            Assert.That(TraitAffinity.Describe(6), Is.EqualTo("kindred"));
            Assert.That(TraitAffinity.Describe(-6), Is.EqualTo("oil and water"));
            Assert.That(TraitAffinity.Describe(0), Is.EqualTo("neither here nor there"));
        }
    }
}
