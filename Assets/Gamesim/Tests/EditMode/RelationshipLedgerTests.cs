using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// What the house remembers, and for how long.
    ///
    /// <para>The reference build's note on this system is that betrayals never decay, and that the
    /// asymmetry is the main reason its house develops long memories. These pin that asymmetry
    /// directly rather than through a season, because the season cannot yet produce a permanent act
    /// — naming one means passing an event type where none is passed today, and that branch
    /// consumes an extra generator roll, so it lands with the NPC writes rather than on its own.
    /// </para>
    /// </summary>
    public sealed class RelationshipLedgerTests
    {
        [TestCase("nominated")]
        [TestCase("saved-with-veto")]
        [TestCase("voted-against")]
        [TestCase("promise-broken")]
        [TestCase("alliance-betrayed")]
        public void ActsThatChangedSomeonesGameNeverFade(string type)
        {
            Assert.That(RelationshipLedger.Decays(type), Is.False, type + " should be permanent.");
        }

        [TestCase("conversation")]
        [TestCase("information")]
        [TestCase("lie")]
        [TestCase("vent")]
        [TestCase("scheme")]
        [TestCase("eavesdrop")]
        [TestCase(null)]
        public void OrdinarySocialTrafficFades(string type)
        {
            Assert.That(RelationshipLedger.Decays(type), Is.True, (type ?? "untyped") + " should fade.");
        }

        /// <summary>
        /// A conversation holds its weight for two weeks, then fades to nothing over four. A
        /// betrayal is worth as much in week forty as the day it happened.
        /// </summary>
        [Test]
        public void ADecayingActFadesAndAPermanentOneDoesNot()
        {
            var fading = Entry("conversation", 10, week: 1, decays: true);
            var permanent = Entry("alliance-betrayed", -50, week: 1, decays: false);

            Assert.That(RelationshipLedger.Weight(fading, 1), Is.EqualTo(1));
            Assert.That(RelationshipLedger.Weight(fading, 3), Is.EqualTo(1), "Two weeks is still recent.");
            Assert.That(RelationshipLedger.Weight(fading, 5), Is.EqualTo(0.5).Within(0.001));
            Assert.That(RelationshipLedger.Weight(fading, 7), Is.EqualTo(0));
            Assert.That(RelationshipLedger.Weight(fading, 40), Is.EqualTo(0), "A fading act cannot go negative.");

            foreach (int week in new[] { 1, 3, 7, 40 })
                Assert.That(RelationshipLedger.Weight(permanent, week), Is.EqualTo(1),
                    "Week " + week + ": a betrayal is worth what it was worth.");
        }

        /// <summary>
        /// The asymmetry in one assertion. Two houseguests do the same amount of damage on the same
        /// week; six weeks later only one of them is still being held to it.
        /// </summary>
        [Test]
        public void SixWeeksLaterOnlyTheBetrayalIsStillFelt()
        {
            var state = ContentCatalog.Create(4242);
            var cast = state.Active.Where(c => !c.isPlayer).Take(2).ToList();
            string forgiven = cast[0].id, unforgiven = cast[1].id;

            Edge(state, state.playerId, forgiven).events.Add(Entry("vent", -30, week: 1, decays: true));
            Edge(state, state.playerId, unforgiven).events.Add(Entry("alliance-betrayed", -30, week: 1, decays: false));

            state.week = 7;
            Assert.That(RelationshipLedger.WeightedImpact(state, state.playerId, forgiven), Is.EqualTo(0),
                "An argument six weeks ago should be spent.");
            Assert.That(RelationshipLedger.WeightedImpact(state, state.playerId, unforgiven), Is.EqualTo(-30),
                "A betrayal six weeks ago should cost exactly what it cost.");

            Assert.That(RelationshipLedger.HoldsAGrudge(state, state.playerId, unforgiven), Is.True);
            Assert.That(RelationshipLedger.HoldsAGrudge(state, state.playerId, forgiven), Is.False);
        }

        /// <summary>
        /// The last two weeks, unweighted — the window the source feeds into its decision factors at
        /// the highest multiplier it uses, so a fresh betrayal reorders targets immediately.
        /// </summary>
        [Test]
        public void RecentImpactSeesOnlyTheLastTwoWeeks()
        {
            var state = ContentCatalog.Create(4242);
            string other = state.Active.First(c => !c.isPlayer).id;
            var edge = Edge(state, state.playerId, other);

            edge.events.Add(Entry("conversation", 10, week: 1, decays: true));
            edge.events.Add(Entry("conversation", 5, week: 4, decays: true));
            edge.events.Add(Entry("vent", -3, week: 5, decays: true));

            state.week = 5;
            Assert.That(RelationshipLedger.RecentImpact(state, state.playerId, other), Is.EqualTo(2),
                "Weeks four and five count; week one does not.");
        }

        [Test]
        public void TheHeaviestThingOnTheRecordIsFoundByWhatItStillCounts()
        {
            var state = ContentCatalog.Create(4242);
            string other = state.Active.First(c => !c.isPlayer).id;
            var edge = Edge(state, state.playerId, other);

            edge.events.Add(Entry("conversation", 40, week: 1, decays: true));      // faded to nothing
            edge.events.Add(Entry("promise-broken", -20, week: 1, decays: false));  // still worth -20

            state.week = 9;
            var heaviest = RelationshipLedger.MostSignificant(state, state.playerId, other);
            Assert.That(heaviest, Is.Not.Null);
            Assert.That(heaviest.type, Is.EqualTo("promise-broken"),
                "A spent kindness must not outrank a standing grudge.");
        }

        /// <summary>
        /// An ordinary season now leaves a ledger, and every entry in it carries the policy its type
        /// demands.
        ///
        /// <para>It did not used to. Relationship events are only written when a change carries an
        /// event type, and for a long time the only calls passing one were risky social actions the
        /// player might never choose — so a 512-capped, validated, persisted structure sat empty
        /// through a whole season. Houseguests forming their own alliances and giving their own word
        /// is what filled it, which is why that work came before anything that reads it.</para>
        /// </summary>
        [Test]
        public void AnOrdinarySeasonNowLeavesALedgerAndEveryEntryCarriesItsPolicy()
        {
            var engine = new EpisodeEngine(ContentCatalog.Create(4242));
            for (int guard = 0; guard < 240 && engine.Snapshot.phase != EpisodePhase.Finished; guard++)
            {
                var step = engine.Apply(EpisodeEngineTests.NextCommand(engine.Snapshot));
                Assert.That(step.accepted, Is.True, step.reason);
            }

            var written = engine.Snapshot.relationships.SelectMany(r => r.events).ToList();
            Assert.That(written, Is.Not.Empty,
                "Houseguests acting on their own account should leave a record of it.");
            foreach (var entry in written)
                Assert.That(entry.decayable, Is.EqualTo(RelationshipLedger.Decays(entry.type)),
                    "A '" + entry.type + "' entry carries the wrong decay policy.");

            Assert.That(written.Any(e => !e.decayable), Is.True,
                "And some of what a season produces should be permanent.");
        }

        /// <summary>
        /// The flag is computed from the act rather than hardcoded, on the one path that writes it.
        /// </summary>
        [Test]
        public void AWrittenEventCarriesThePolicyItsTypeDemands()
        {
            var engine = new EpisodeEngine(ContentCatalog.Create(4242));
            var state = engine.Snapshot;
            var others = state.Active.Where(c => !c.isPlayer).Select(c => c.id).ToList();

            var result = engine.Apply(new EpisodeCommand
            {
                id = "ledger-policy", kind = EpisodeCommandKind.VentAbout, actorId = state.playerId,
                targetId = others[0], secondTargetId = others[1],
                expectedPhase = state.phase, expectedRevision = state.revision,
            });
            Assert.That(result.accepted, Is.True, result.reason);

            var written = result.state.relationships.SelectMany(r => r.events).ToList();
            Assert.That(written, Is.Not.Empty, "Venting carries an event type, so it writes a ledger entry.");
            foreach (var entry in written)
            {
                Assert.That(entry.type, Is.EqualTo("vent"));
                Assert.That(entry.decayable, Is.EqualTo(RelationshipLedger.Decays(entry.type)),
                    "A '" + entry.type + "' entry carries the wrong decay policy.");
            }
        }

        // ---------------------------------------------------------------- fixtures

        private static RelationshipEventState Entry(string type, double impact, int week, bool decays) =>
            new RelationshipEventState
            {
                sequence = week, week = week, type = type,
                description = type + " in week " + week, impactScore = impact, decayable = decays,
            };

        private static RelationshipState Edge(EpisodeState state, string from, string to) =>
            state.relationships.Single(r => r.fromId == from && r.toId == to);
    }
}
