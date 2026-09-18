using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Houseguests pairing up without the player.
    ///
    /// <para>Before this, <c>alliances.Add</c> appeared once in the whole simulation, on the
    /// player's path — so the voting-bloc system had only ever coordinated blocs the player built.
    /// The tests that matter most here are the two properties that make the pass safe to run every
    /// week: it spends no randomness, and the same house always pairs up the same way.</para>
    /// </summary>
    public sealed class NpcAllianceTests
    {
        // ---------------------------------------------------------------- safety

        /// <summary>
        /// The pass reads and writes state but never draws. A roll spent here would shift every
        /// competition and vote after it, which is what makes this the property to defend.
        /// </summary>
        [Test]
        public void SettlingSpendsNoRandomness()
        {
            var state = Warm(SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, 7u), 40);
            uint before = state.randomState;

            NpcAlliances.Settle(state);

            Assert.That(state.randomState, Is.EqualTo(before),
                "Alliance formation must not touch the season's generator.");
        }

        [Test]
        public void TheSameHousePairsUpTheSameWayEveryTime()
        {
            var first = Warm(SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 10 }, 11u), 40);
            var second = Warm(SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 10 }, 11u), 40);

            NpcAlliances.Settle(first);
            NpcAlliances.Settle(second);

            CollectionAssert.AreEqual(Pacts(first), Pacts(second));
        }

        [Test]
        public void SettlingLeavesAValidSeason()
        {
            var state = Warm(SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 12 }, 3u), 60);
            NpcAlliances.Settle(state);
            Assert.That(EpisodeValidation.TryValidate(state, out var error), Is.True, error);
        }

        // ---------------------------------------------------------------- the formula

        /// <summary>
        /// The source's floor: below 25 nothing happens however attractive the other terms are.
        /// </summary>
        [Test]
        public void NobodyAlliesBelowTheRelationshipFloor()
        {
            var state = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, 7u);
            var pair = state.Active.Where(c => !c.isPlayer).Take(2).ToList();
            Set(state, pair[0].id, pair[1].id, NpcAlliances.MinimumRelationship - 1);

            Assert.That(NpcAlliances.WouldPropose(state, pair[0].id, pair[1].id), Is.False);

            Set(state, pair[0].id, pair[1].id, NpcAlliances.MinimumRelationship + 40);
            Assert.That(NpcAlliances.WouldPropose(state, pair[0].id, pair[1].id), Is.True,
                "Well above the floor, with warmth on both sides, they should want to work together.");
        }

        /// <summary>
        /// Enemy of my enemy, worth fifteen a head. Two houseguests who are lukewarm about each
        /// other but share a problem should want to work together more than two who share nothing.
        /// </summary>
        [Test]
        public void SharedThreatsDrawPeopleTogether()
        {
            var state = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, 7u);
            var cast = state.Active.Where(c => !c.isPlayer).ToList();
            string a = cast[0].id, b = cast[1].id, enemy = cast[2].id, bystander = cast[3].id;

            Set(state, a, b, 30);
            Set(state, a, bystander, 30);
            double without = NpcAlliances.Desire(state, a, b);

            // Now give A and B somebody they both dislike.
            Set(state, a, enemy, NpcAlliances.DislikeLine - 5);
            Set(state, b, enemy, NpcAlliances.DislikeLine - 5);
            double with = NpcAlliances.Desire(state, a, b);

            Assert.That(with - without, Is.EqualTo(15).Within(0.001),
                "One shared threat is worth fifteen points of desire.");
        }

        /// <summary>Diminishing returns: every pact already carried costs ten points of appetite.</summary>
        [Test]
        public void EveryAllianceAlreadyCarriedMakesTheNextOneLessAttractive()
        {
            var state = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 10 }, 7u);
            var cast = state.Active.Where(c => !c.isPlayer).ToList();
            string a = cast[0].id, b = cast[1].id;
            Set(state, a, b, 40);

            double alone = NpcAlliances.Desire(state, a, b);
            state.alliances.Add(new AllianceState
            {
                id = "existing", name = "An Earlier Pact",
                members = new List<string> { a, cast[2].id }, active = true,
            });

            Assert.That(NpcAlliances.Desire(state, a, b), Is.EqualTo(alone - 10).Within(0.001));
        }

        [Test]
        public void NobodyCarriesMoreThanThreeAlliances()
        {
            var state = Warm(SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 12 }, 5u), 80);
            for (int week = 0; week < 8; week++) NpcAlliances.Settle(state);

            foreach (var houseguest in state.Active)
                Assert.That(NpcAlliances.ActiveAlliancesFor(state, houseguest.id).Count,
                    Is.LessThanOrEqualTo(NpcAlliances.MaximumEach),
                    houseguest.name + " is in too many alliances.");
        }

        // ---------------------------------------------------------------- forming and falling apart

        [Test]
        public void AWarmHouseActuallyFormsAlliances()
        {
            var state = Warm(SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 10 }, 9u), 60);
            Assert.That(state.alliances, Is.Empty, "A fresh season has none.");

            NpcAlliances.Settle(state);

            Assert.That(state.alliances.Where(a => a.active), Is.Not.Empty,
                "Houseguests who all like each other should find partners.");
            Assert.That(state.alliances.All(a => a.members.Count == 2), Is.True);
        }

        /// <summary>One new pact each per week, so a warm house does not pair off in one evening.</summary>
        [Test]
        public void NobodyJoinsMoreThanOneNewAllianceInAWeek()
        {
            var state = Warm(SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 12 }, 9u), 80);
            NpcAlliances.Settle(state);

            var joined = state.alliances.Where(a => a.active).SelectMany(a => a.members).ToList();
            CollectionAssert.AreEqual(joined.Distinct().OrderBy(id => id).ToList(),
                joined.OrderBy(id => id).ToList(),
                "Nobody should appear in two alliances formed on the same week.");
        }

        [Test]
        public void APactDissolvesWhenItsMembersTurnOnEachOther()
        {
            var state = Warm(SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, 9u), 60);
            NpcAlliances.Settle(state);
            var pact = state.alliances.First(a => a.active);
            string one = pact.members[0], other = pact.members[1];

            Set(state, one, other, NpcAlliances.SourLine - 10);
            NpcAlliances.Settle(state);

            Assert.That(pact.active, Is.False, "An alliance between two people who now dislike each other is over.");
        }

        /// <summary>
        /// Souring is not betrayal. Two people drifting apart must not put a permanent grudge on the
        /// books that neither of them earned.
        /// </summary>
        [Test]
        public void DriftingApartLeavesNoGrudge()
        {
            var state = Warm(SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, 9u), 60);
            NpcAlliances.Settle(state);
            var pact = state.alliances.First(a => a.active);
            string one = pact.members[0], other = pact.members[1];

            Set(state, one, other, NpcAlliances.SourLine - 10);
            NpcAlliances.Settle(state);

            Assert.That(RelationshipLedger.HoldsAGrudge(state, one, other), Is.False,
                "Nobody did anything to anybody here.");
        }

        [Test]
        public void AnAllianceLosesItsClaimWhenItsMembersLeaveTheHouse()
        {
            var state = Warm(SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, 9u), 60);
            NpcAlliances.Settle(state);
            var pact = state.alliances.First(a => a.active);

            state.Find(pact.members[0]).status = ContestantStatus.Jury;
            NpcAlliances.Settle(state);

            Assert.That(pact.active, Is.False, "A pact of one is not a pact.");
        }

        // ---------------------------------------------------------------- what it feeds

        /// <summary>
        /// The whole reason this phase came first: an alliance the player never touched must be
        /// visible to the threat model, because that is what the bloc system reads.
        /// </summary>
        [Test]
        public void AnAllianceThePlayerNeverTouchedRaisesItsMembersThreat()
        {
            var state = Warm(SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 10 }, 9u), 60);
            NpcAlliances.Settle(state);
            var pact = state.alliances.First(a => a.active);
            string member = pact.members[0];

            Assert.That(pact.members, Does.Not.Contain(state.playerId),
                "This fixture is about a pact formed between houseguests.");
            Assert.That(ThreatAssessment.Assess(state, state.playerId, member).Alliance, Is.EqualTo(8),
                "Two members, four points a head — the player can now see a bloc they had no part in.");
        }

        /// <summary>Forming a pact writes a permanent ledger entry, which is what trust reads.</summary>
        [Test]
        public void FormingAPactIsRememberedPermanently()
        {
            var state = Warm(SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, 9u), 60);
            NpcAlliances.Settle(state);
            var pact = state.alliances.First(a => a.active);
            string one = pact.members[0], other = pact.members[1];

            var entry = state.relationships
                .Single(r => r.fromId == one && r.toId == other)
                .events.Single(e => e.type == "alliance-formed");
            Assert.That(entry.decayable, Is.False, "Forming an alliance is not something that fades.");

            state.week += 20;
            Assert.That(ThreatAssessment.TrustScore(state, other, one),
                Is.GreaterThan(ThreatAssessment.NeutralTrust),
                "Twenty weeks on, they should still trust the person they allied with.");
        }

        // ---------------------------------------------------------------- fixtures

        /// <summary>A house where everybody has warmed to everybody by the given amount.</summary>
        private static EpisodeState Warm(EpisodeState state, double score)
        {
            foreach (var edge in state.relationships) edge.score = score;
            return state;
        }

        private static void Set(EpisodeState state, string from, string to, double score)
        {
            foreach (var edge in state.relationships.Where(r =>
                         (r.fromId == from && r.toId == to) || (r.fromId == to && r.toId == from)))
                edge.score = score;
        }

        private static List<string> Pacts(EpisodeState state) => state.alliances
            .Where(a => a.active)
            .Select(a => string.Join("+", a.members.OrderBy(id => id)))
            .OrderBy(name => name)
            .ToList();
    }
}
