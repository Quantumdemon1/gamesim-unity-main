#if GAMESIM_ALLSTAR_CANDIDATE
using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    public sealed class AlumniStrategyTests
    {
        private static string Person(EpisodeState state, string name) => state.contestants.Single(c => c.name == name).id;
        private static EpisodeState ProtectedWill()
        {
            var state = AlumniHistoryTests.Fresh();
            string will = Person(state, "Dr. Will Kirby"), dan = Person(state, "Dan Gheesling");
            state.phase = EpisodePhase.HoH;
            state.alliances.Add(new AllianceState { id = "test-protection", name = "Protection", active = true,
                members = new System.Collections.Generic.List<string> { will, dan } });
            RelationshipLedger.RecordOneWay(state, will, dan, "conversation", 36, "Built real protection this season.");
            return state;
        }

        [Test]
        public void ProtectedWillCanConcealThreatWithoutChangingStoredAbilityOrDrawingRandomness()
        {
            var state = ProtectedWill(); string will = Person(state, "Dr. Will Kirby");
            uint rng = state.randomState; double ability = state.Find(will).stats.competition;
            Assert.That(AlumniStrategy.ShouldThrow(state, will, false, "Mental"), Is.True);
            Assert.That(AlumniStrategy.CompetitionEffort(state, will, false, "Mental"), Is.EqualTo(.5));
            Assert.That(state.randomState, Is.EqualTo(rng)); Assert.That(state.Find(will).stats.competition, Is.EqualTo(ability));
        }

        [TestCase("nominee")]
        [TestCase("finale")]
        [TestCase("unprotected")]
        public void SurvivalOverridesWillThrowing(string risk)
        {
            var state = ProtectedWill(); string will = Person(state, "Dr. Will Kirby");
            if (risk == "nominee") state.nominees.Add(will);
            if (risk == "unprotected") state.alliances.Clear();
            Assert.That(AlumniStrategy.ShouldThrow(state, will, risk == "finale", "Mental"), Is.False);
        }

        [Test]
        public void CurrentKnownDirectBetrayalClearsPositiveHistoryWithoutAnExtraPenalty()
        {
            var state = AlumniHistoryTests.Fresh(player: "derrick-levasseur");
            string cody = Person(state, "Cody Calafiore");
            var promise = EpisodeEngineTests.Command(state, EpisodeCommandKind.PromiseSafety); promise.targetId = cody;
            var promised = new EpisodeEngine(state).Apply(promise);
            Assert.That(promised.accepted, Is.True, promised.reason);
            var ceremony = promised.state;
            ceremony.phase = EpisodePhase.Nomination; ceremony.hohId = ceremony.playerId;
            var nomination = EpisodeEngineTests.Command(ceremony, EpisodeCommandKind.Nominate);
            nomination.targetId = cody; nomination.secondTargetId = Person(ceremony, "Dan Gheesling");
            var result = new EpisodeEngine(ceremony).Apply(nomination);
            Assert.That(result.accepted, Is.True, result.reason);
            var edge = result.state.relationships.Single(r => r.fromId == cody && r.toId == result.state.playerId);
            Assert.That(edge.priorTrustOffset, Is.Zero, "A real publicly broken safety promise invalidates the inherited protection.");
            Assert.That(result.state.story.grudges.Count, Is.GreaterThan(0));
        }

        [Test]
        public void HistoricalPairsAreRiskWithoutProofOfTheirCurrentSecretDeal()
        {
            var state = AlumniHistoryTests.Fresh();
            string will = Person(state, "Dr. Will Kirby"), derrick = Person(state, "Derrick Levasseur");
            double risk = AlumniStrategy.KnownPairRisk(state, will, derrick);
            Assert.That(risk, Is.EqualTo(8));
            state.deals.Clear();
            Assert.That(AlumniStrategy.KnownPairRisk(state, will, derrick), Is.EqualTo(risk));
            var disabled = AlumniHistoryTests.Fresh(false);
            Assert.That(AlumniStrategy.KnownPairRisk(disabled, will, derrick), Is.Zero);
        }

        [Test]
        public void ActualThrowChangesOnlyWillsCommittedScoreAndPreservesCompetitorRolls()
        {
            var protectedState = ProtectedWill(); protectedState.allWeekRulesStartWeek = 0;
            string will = Person(protectedState, "Dr. Will Kirby"), dan = Person(protectedState, "Dan Gheesling");
            var control = protectedState.Clone();
            control.relationships.Single(r => r.fromId == will && r.toId == dan).events.Clear();
            var throwCommand = EpisodeEngineTests.Command(protectedState, EpisodeCommandKind.Compete);
            var normalCommand = EpisodeEngineTests.Command(control, EpisodeCommandKind.Compete);
            var throwing = new EpisodeEngine(protectedState).Apply(throwCommand);
            var normal = new EpisodeEngine(control).Apply(normalCommand);
            Assert.That(throwing.accepted, Is.True, throwing.reason); Assert.That(normal.accepted, Is.True, normal.reason);
            foreach (var score in normal.state.competitionScores)
                Assert.That(throwing.state.competitionScores.Single(row => row.contestantId == score.contestantId).score,
                    Is.EqualTo(score.score * (score.contestantId == will ? .5 : 1)));
            Assert.That(throwing.state.randomState, Is.EqualTo(normal.state.randomState));
            Assert.That(throwing.state.events.Any(e => e.kind == "alumni-competition-effort"
                && e.audienceIds.Contains(throwing.state.playerId)), Is.False);
        }

        [Test]
        public void KeepingATrustedBiggerThreatIsUsefulEarlyAndLosesShieldValueAtTheEnd()
        {
            var state = AlumniHistoryTests.Fresh();
            string dan = Person(state, "Dan Gheesling"), janelle = Person(state, "Janelle Pierzina");
            state.Find(janelle).hohWins = 4;
            Assert.That(AlumniStrategy.ShieldValue(state, dan, janelle), Is.Zero, "Fame alone does not grant trusted protection.");
            RelationshipLedger.RecordOneWay(state, dan, janelle, "conversation", 36, "Built protection through real conversations.");
            Assert.That(AlumniStrategy.ShieldValue(state, dan, janelle), Is.GreaterThan(0));
            foreach (var person in state.Active.Where(c => c.id != dan && c.id != janelle).Skip(4).ToArray())
                person.status = ContestantStatus.Evicted;
            Assert.That(state.Active.Count(), Is.EqualTo(6));
            Assert.That(AlumniStrategy.ShieldValue(state, dan, janelle), Is.Zero);
        }
    }
}
#endif
