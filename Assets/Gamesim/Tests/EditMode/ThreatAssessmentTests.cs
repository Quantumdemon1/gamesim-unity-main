using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Threat assessment and trust, against the reference build's own numbers.
    ///
    /// <para>The property worth defending hardest is the capping. Five components, each with its own
    /// ceiling, is what stops any one dimension deciding every nomination — and a cap is exactly the
    /// kind of thing a later "simplification" removes without anyone noticing until the house only
    /// ever targets whoever has won the most.</para>
    /// </summary>
    public sealed class ThreatAssessmentTests
    {
        // ---------------------------------------------------------------- caps

        /// <summary>
        /// No component can exceed its own ceiling, however extreme the houseguest.
        ///
        /// <para>Two of the source's numbers are pinned here as the quirks they are rather than the
        /// values they claim. Potential is capped at ten but its terms reach only seven, so that cap
        /// can never bind. And the five ceilings sum to 112, so the "0–100" the source labels the
        /// total with is aspirational. Both are kept faithfully; this test records them so nobody
        /// later "corrects" the port away from the build it copies.</para>
        /// </summary>
        [Test]
        public void NoComponentCanExceedItsCeiling()
        {
            var state = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 10 }, 7u);
            var target = state.Active.First(c => !c.isPlayer);

            target.hohWins = 40; target.vetoWins = 40;
            target.stats.competition = 10; target.stats.strategic = 10; target.stats.social = 10;
            foreach (var edge in state.relationships.Where(r => r.toId == target.id)) edge.score = 100;
            state.alliances.Add(new AllianceState
            {
                id = "everyone", name = "The Whole House",
                members = state.Active.Select(c => c.id).ToList(),
            });

            var worst = ThreatAssessment.Assess(state, state.playerId, target.id);
            Assert.That(worst.Competition, Is.EqualTo(40), "Competition caps at forty.");
            Assert.That(worst.Social, Is.EqualTo(30), "Social caps at thirty.");
            Assert.That(worst.Alliance, Is.EqualTo(20), "Alliance caps at twenty.");
            Assert.That(worst.Potential, Is.EqualTo(7),
                "The source caps potential at ten, but three plus two plus two is as high as it goes.");
            Assert.That(worst.Reputation, Is.InRange(0, 15), "Reputation is held between zero and fifteen.");
            Assert.That(worst.Total, Is.EqualTo(worst.Competition + worst.Social + worst.Alliance
                + worst.Potential + worst.Reputation), "The total is the sum, unclamped, as in the source.");
            Assert.That(worst.Total, Is.LessThanOrEqualTo(112), "Which is what the five ceilings actually add to.");
        }

        /// <summary>
        /// The capping in the form that matters: winning everything is not enough on its own. A
        /// competition beast with no social capital reads as less dangerous than the ceiling.
        /// </summary>
        [Test]
        public void WinningEverythingIsNotEnoughToMaxOutThreat()
        {
            var state = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, 7u);
            var beast = state.Active.First(c => !c.isPlayer);
            beast.hohWins = 10; beast.vetoWins = 10;
            foreach (var edge in state.relationships.Where(r => r.toId == beast.id)) edge.score = -100;

            var assessed = ThreatAssessment.Assess(state, state.playerId, beast.id);
            Assert.That(assessed.Competition, Is.EqualTo(40), "They have maxed the one thing they are good at.");
            Assert.That(assessed.Social, Is.EqualTo(0), "Nobody likes them.");
            Assert.That(assessed.Total, Is.LessThan(60),
                "Without social capital or a bloc, a comp beast must not read as an extreme threat.");
        }

        // ---------------------------------------------------------------- components

        [TestCase(0, 0, 0)]
        [TestCase(1, 0, 8)]
        [TestCase(0, 1, 6)]
        [TestCase(2, 3, 34)]
        [TestCase(5, 5, 40)]
        public void CompetitionThreatCountsHeadOfHouseholdHigherThanVeto(int hoh, int veto, double expected)
        {
            var state = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, 7u);
            var target = state.Active.First(c => !c.isPlayer);
            target.hohWins = hoh; target.vetoWins = veto;
            Assert.That(ThreatAssessment.Assess(state, state.playerId, target.id).Competition, Is.EqualTo(expected));
        }

        /// <summary>The source's mapping: −100 reads as nothing, neutral as 15, +100 as the full 30.</summary>
        [TestCase(-100, 0)]
        [TestCase(0, 15)]
        [TestCase(100, 30)]
        public void BeingLikedIsItselfAThreat(double everyoneFeels, double expected)
        {
            var state = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, 7u);
            var target = state.Active.First(c => !c.isPlayer);
            foreach (var edge in state.relationships.Where(r => r.toId == target.id)) edge.score = everyoneFeels;
            Assert.That(ThreatAssessment.Assess(state, state.playerId, target.id).Social,
                Is.EqualTo(expected).Within(0.001));
        }

        [Test]
        public void ABiggerBlocIsABiggerThreat()
        {
            var state = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 10 }, 7u);
            var target = state.Active.First(c => !c.isPlayer);
            Assert.That(ThreatAssessment.Assess(state, state.playerId, target.id).Alliance, Is.Zero,
                "Nobody is allied at the start of a season.");

            state.alliances.Add(new AllianceState
            {
                id = "pair", name = "The Pair",
                members = new List<string> { target.id, state.playerId },
            });
            Assert.That(ThreatAssessment.Assess(state, state.playerId, target.id).Alliance, Is.EqualTo(8),
                "Two members, four points a head.");
        }

        /// <summary>
        /// Reputation is the one component that depends on who is asking — the same person can be a
        /// threat to somebody who distrusts them and not to somebody who does not.
        /// </summary>
        [Test]
        public void ReputationDiffersByWhoIsAsking()
        {
            var state = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, 7u);
            var cast = state.Active.Where(c => !c.isPlayer).Take(2).ToList();
            string burned = cast[0].id, untouched = cast[1].id;
            string target = state.Active.First(c => c.id != burned && c.id != untouched && !c.isPlayer).id;

            // The target betrayed one of them and never touched the other.
            Edge(state, burned, target).events.Add(new RelationshipEventState
            {
                sequence = 1, week = 1, type = "alliance-betrayed",
                description = "They walked out of our alliance", impactScore = -50, decayable = false,
            });

            var burnedView = ThreatAssessment.Assess(state, burned, target);
            var untouchedView = ThreatAssessment.Assess(state, untouched, target);

            Assert.That(burnedView.Reputation, Is.GreaterThan(untouchedView.Reputation),
                "Somebody who was betrayed should read the betrayer as more dangerous.");
            Assert.That(burnedView.Competition, Is.EqualTo(untouchedView.Competition),
                "The other four components do not depend on the asker.");
        }

        // ---------------------------------------------------------------- trust

        [Test]
        public void TrustStartsNeutralAndIsSymmetricallyUnknown()
        {
            var state = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, 7u);
            foreach (var pair in state.Active.SelectMany(a => state.Active.Where(b => b.id != a.id).Select(b => (a, b))))
                Assert.That(ThreatAssessment.TrustScore(state, pair.b.id, pair.a.id),
                    Is.EqualTo(ThreatAssessment.NeutralTrust),
                    "Before anybody has done anything, trust is neutral.");
        }

        /// <summary>
        /// The arithmetic the scale was chosen for: the heaviest act on the source's table must
        /// clear the threshold the source reacts to, not stop exactly on it.
        /// </summary>
        [Test]
        public void OneAllianceBetrayalDropsTrustIntoTheSourcesDangerBand()
        {
            var state = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, 7u);
            var cast = state.Active.Where(c => !c.isPlayer).Take(2).ToList();
            string betrayed = cast[0].id, betrayer = cast[1].id;

            Edge(state, betrayed, betrayer).events.Add(new RelationshipEventState
            {
                sequence = 1, week = 1, type = "alliance-betrayed",
                description = "They walked out", impactScore = -50, decayable = false,
            });

            double trust = ThreatAssessment.TrustScore(state, betrayer, betrayed);
            Assert.That(trust, Is.LessThan(35),
                "A betrayal must cross the line the source treats as dangerous, not land on it.");
            Assert.That(trust, Is.GreaterThan(25), "One betrayal is not total distrust either.");
        }

        /// <summary>A grudge outlives a grievance, so trust recovers from one and not the other.</summary>
        [Test]
        public void TrustRecoversFromAGrievanceButNotFromABetrayal()
        {
            var state = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, 7u);
            var cast = state.Active.Where(c => !c.isPlayer).Take(2).ToList();
            string forgiven = cast[0].id, unforgiven = cast[1].id;

            Edge(state, state.playerId, forgiven).events.Add(new RelationshipEventState
            {
                sequence = 1, week = 1, type = "vent", description = "An argument", impactScore = -50, decayable = true,
            });
            Edge(state, state.playerId, unforgiven).events.Add(new RelationshipEventState
            {
                sequence = 2, week = 1, type = "alliance-betrayed", description = "A betrayal", impactScore = -50, decayable = false,
            });

            state.week = 8;
            Assert.That(ThreatAssessment.TrustScore(state, forgiven, state.playerId),
                Is.EqualTo(ThreatAssessment.NeutralTrust), "An old argument should be forgotten.");
            Assert.That(ThreatAssessment.TrustScore(state, unforgiven, state.playerId),
                Is.LessThan(35), "A betrayal should not be.");
        }

        // ---------------------------------------------------------------- ranking

        [Test]
        public void TheRankingIsStableAndExcludesTheEvaluator()
        {
            var state = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 10 }, 7u);
            var ranked = ThreatAssessment.RankedTargets(state, state.playerId);

            Assert.That(ranked, Does.Not.Contain(state.playerId), "Nobody ranks themselves.");
            Assert.That(ranked, Has.Count.EqualTo(state.Active.Count() - 1));
            CollectionAssert.AreEqual(ranked, ThreatAssessment.RankedTargets(state, state.playerId),
                "Equal threats must not reshuffle between calls.");
        }

        [Test]
        public void WinningMovesYouUpTheRanking()
        {
            var state = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 10 }, 7u);
            string climber = ThreatAssessment.RankedTargets(state, state.playerId).Last();

            state.Find(climber).hohWins = 3;
            Assert.That(ThreatAssessment.RankedTargets(state, state.playerId).First(), Is.EqualTo(climber),
                "Three Head of Household wins should make somebody the most dangerous person in the house.");
        }

        /// <summary>Threat reads state and never writes it — no schema version, and no replay impact.</summary>
        [Test]
        public void AssessingThreatChangesNothing()
        {
            var state = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, 7u);
            uint randomBefore = state.randomState;
            int sequenceBefore = state.nextSequence;
            var scoresBefore = state.relationships.Select(r => r.fromId + r.toId + r.score).ToList();

            foreach (var evaluator in state.Active)
                ThreatAssessment.RankedTargets(state, evaluator.id);

            Assert.That(state.randomState, Is.EqualTo(randomBefore), "Assessment must not draw from the generator.");
            Assert.That(state.nextSequence, Is.EqualTo(sequenceBefore));
            CollectionAssert.AreEqual(scoresBefore,
                state.relationships.Select(r => r.fromId + r.toId + r.score).ToList());
        }

        private static RelationshipState Edge(EpisodeState state, string from, string to) =>
            state.relationships.Single(r => r.fromId == from && r.toId == to);
    }
}
