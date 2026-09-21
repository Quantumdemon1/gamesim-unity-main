using System;
using System.Linq;
using Gamesim.Presentation;
using Gamesim.Simulation;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    public sealed class CompetitionRulesV3Tests
    {
        [Test]
        public void ReactionScheduleIsIndependentOfHitsAndEveryTargetHasACompleteWindow()
        {
            for (uint seed = 1; seed <= 32; seed++)
            {
                var hit = new MiniGameRun(CompetitionMiniGames.Kind.Reaction, seed, 3);
                var idle = new MiniGameRun(CompetitionMiniGames.Kind.Reaction, seed, 3);
                int seen = 0;
                while (!hit.Finished)
                {
                    hit.Tick(.01); idle.Tick(.01);
                    Assert.That(hit.Spawned, Is.EqualTo(idle.Spawned), "A faster hit must not create extra opportunities.");
                    if (hit.Spawned > seen)
                    {
                        seen = hit.Spawned;
                        Assert.That(hit.Remaining + .010001, Is.GreaterThanOrEqualTo(MiniGameRun.TargetLife));
                        Assert.That(hit.TargetX, Is.EqualTo(idle.TargetX));
                        Assert.That(hit.TargetY, Is.EqualTo(idle.TargetY));
                        Assert.That(hit.Tap(hit.TargetDirection), Is.True);
                    }
                }
                Assert.That(hit.Score, Is.EqualTo(10));
                Assert.That(idle.Score, Is.Zero);
                Assert.That(idle.ExpiredTargets, Is.EqualTo(idle.Spawned));
            }
        }

        [Test]
        public void ReactionScheduleMatchesAcrossFramePartitions()
        {
            var coarse = new MiniGameRun(CompetitionMiniGames.Kind.Reaction, 77, 3);
            var fine = new MiniGameRun(CompetitionMiniGames.Kind.Reaction, 77, 3);
            for (int second = 1; second <= 20; second++)
            {
                coarse.Tick(1);
                for (int frame = 0; frame < 144; frame++) fine.Tick(1d / 144);
                Assert.That(coarse.Spawned, Is.EqualTo(fine.Spawned));
                Assert.That(coarse.ExpiredTargets, Is.EqualTo(fine.ExpiredTargets));
                Assert.That(coarse.TargetX, Is.EqualTo(fine.TargetX));
                Assert.That(coarse.TargetY, Is.EqualTo(fine.TargetY));
            }
            Assert.That(coarse.Score, Is.EqualTo(fine.Score));
            Assert.That(coarse.Elapsed, Is.EqualTo(fine.Elapsed));
        }

        [Test]
        public void PointerAndDirectionalErrorsHaveTheSameScoreAndSchedule()
        {
            var pointer = new MiniGameRun(CompetitionMiniGames.Kind.Reaction, 19, 3);
            var direction = new MiniGameRun(CompetitionMiniGames.Kind.Reaction, 19, 3);
            pointer.MissPointer(); direction.Tap(MiniGameRun.Direction.Up);
            pointer.Tick(.5); direction.Tick(.5);
            pointer.MissPointer();
            direction.Tap((MiniGameRun.Direction)(((int)direction.TargetDirection + 1) % 4));
            while (!pointer.TargetLive) { pointer.Tick(.01); direction.Tick(.01); }
            pointer.Tap(pointer.TargetDirection); direction.Tap(direction.TargetDirection);
            pointer.Finish(); direction.Finish();
            Assert.That(pointer.Score, Is.EqualTo(direction.Score).And.EqualTo(3.33));
            Assert.That(pointer.PointerMisses, Is.EqualTo(1));
            Assert.That(direction.WrongDirections, Is.EqualTo(1));
            Assert.That(pointer.FalseStarts, Is.EqualTo(direction.FalseStarts));
        }

        [TestCase(EpisodePhase.HoH)]
        [TestCase(EpisodePhase.Veto)]
        public void PlayedAndSimulatedEntriesShareEarnedBonusesAndIdenticalRolls(EpisodePhase phase)
        {
            var state = Fixture(3, phase);
            var simulated = Apply(state, EpisodeCommandKind.SimulateCompetition, 0);
            var noEffort = Apply(state, EpisodeCommandKind.Compete, 0);
            var accessible = Apply(state, EpisodeCommandKind.Compete, .5);
            var perfect = Apply(state, EpisodeCommandKind.Compete, 1);
            Assert.That(JsonConvert.SerializeObject(noEffort.competitionScores), Is.EqualTo(JsonConvert.SerializeObject(simulated.competitionScores)));
            Assert.That(perfect.randomState, Is.EqualTo(simulated.randomState));
            foreach (var score in simulated.competitionScores)
            {
                double expected = score.contestantId == state.playerId ? 2 : 0;
                Assert.That(perfect.competitionScores.Single(s => s.contestantId == score.contestantId).score - score.score,
                    Is.EqualTo(expected).Within(.0000001));
                Assert.That(accessible.competitionScores.Single(s => s.contestantId == score.contestantId).score - score.score,
                    Is.EqualTo(expected / 2).Within(.0000001));
            }
            Assert.That(perfect.playerStudyBonus, Is.EqualTo(state.playerStudyBonus));
            var reloaded = JsonConvert.DeserializeObject<EpisodeState>(JsonConvert.SerializeObject(perfect));
            string explanation = reloaded.events.Last(e => e.kind == "competition-performance").text;
            Assert.That(explanation, Does.Contain("preparation +3").And.Contain("event +1").And.Contain("storyline -1"));
            Assert.That(explanation, Does.Contain("Total player bonus +5"));
            Assert.That(simulated.events.Last(e => e.kind == "competition-performance").text, Does.Contain("Simulated: no performance bonus"));
        }

        [TestCase(1)]
        [TestCase(2)]
        public void ExistingVersionsKeepTheirDistinctPlayedAndSimulationPolicies(int version)
        {
            var state = Fixture(version, EpisodePhase.HoH);
            var played = Apply(state, EpisodeCommandKind.Compete, 1);
            var simulated = Apply(state, EpisodeCommandKind.SimulateCompetition, 0);
            var random = new SeededRandom(state.randomState);
            foreach (var actor in EpisodeEngine.CompetitionPlayers(state))
            {
                double roll = random.NextDouble();
                double expectedPlay = WebRules.WeightedCompetitionScore(actor.stats, EpisodeEngine.CompetitionCategory(state), false,
                    actor.isPlayer ? 1 : 0, roll);
                double expectedSim = WebRules.WeightedCompetitionScore(actor.stats, EpisodeEngine.CompetitionCategory(state), false,
                    actor.isPlayer ? 4 : 0, roll);
                Assert.That(played.competitionScores.Single(score => score.contestantId == actor.id).score, Is.EqualTo(expectedPlay));
                Assert.That(simulated.competitionScores.Single(score => score.contestantId == actor.id).score, Is.EqualTo(expectedSim));
            }
            Assert.That(played.randomState, Is.EqualTo(random.State));
            Assert.That(simulated.randomState, Is.EqualTo(random.State));
        }

        [Test]
        public void FinaleExplainsTheActualCappedEnduranceBenefitWithoutMutatingStats()
        {
            var state = Fixture(3, EpisodePhase.FinalHoHPart1);
            foreach (var actor in state.contestants.Skip(3)) actor.status = ContestantStatus.Jury;
            state.Find(state.playerId).stats.endurance = 9;
            var expectedCast = state.Active.Select(actor => actor.Clone()).ToArray();
            expectedCast.Single(actor => actor.isPlayer).stats.endurance = 10;
            var random = new SeededRandom(state.randomState);
            var expected = WebEnduranceCompetition.Run(expectedCast, random.NextDouble);
            var result = Apply(state, EpisodeCommandKind.Compete, 1);
            Assert.That(JsonConvert.SerializeObject(result.competitionScores), Is.EqualTo(JsonConvert.SerializeObject(expected.scores)));
            Assert.That(result.randomState, Is.EqualTo(random.State));
            Assert.That(result.Find(state.playerId).stats.endurance, Is.EqualTo(9));
            Assert.That(result.events.Last(e => e.kind == "competition-performance").text,
                Does.Contain("Effective endurance 9 → 10 (+1 after the 0–10 cap)"));
        }

        private static EpisodeState Fixture(int version, EpisodePhase phase)
        {
            var state = ContentCatalog.Create(1708);
            state.competitionRulesVersion = version; state.phase = phase;
            state.playerStudyBonus = 3; state.phaseEventCompBonus = 1;
            state.activeModifiers.Add(new StoryModifierState { id = "focus", name = "Distracted", weeksLeft = 2, competitionBonus = -1 });
            if (phase == EpisodePhase.Veto)
            {
                state.hohId = state.Active.First(actor => !actor.isPlayer).id;
                state.nominees = state.Active.Where(actor => actor.id != state.hohId).Take(2).Select(actor => actor.id).ToList();
                state.vetoPlayers = state.Active.Select(actor => actor.id).ToList();
            }
            return state;
        }

        private static EpisodeState Apply(EpisodeState state, EpisodeCommandKind kind, double performance)
        {
            var result = new EpisodeEngine(state).Apply(new EpisodeCommand { id = Guid.NewGuid().ToString("N"), actorId = state.playerId,
                expectedPhase = state.phase, expectedRevision = state.revision, kind = kind, performance = performance });
            Assert.That(result.accepted, Is.True, result.reason);
            return result.state;
        }
    }
}
