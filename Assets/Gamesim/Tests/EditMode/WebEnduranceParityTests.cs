using System;
using System.IO;
using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace Gamesim.Tests.EditMode
{
    public sealed class WebEnduranceParityTests
    {
        [Test]
        public void EliminationTimesOrderingAndRandomDrawCountMatchOriginalRunner()
        {
            var fixture = JsonUtility.FromJson<Goldens>(File.ReadAllText(Path.Combine(Application.dataPath,
                "Gamesim/Tests/EditMode/Fixtures/WebEnduranceFixtures.json")));
            Assert.That(fixture.schemaVersion, Is.EqualTo(1));
            Assert.That(fixture.cases, Has.Length.EqualTo(14));
            foreach (var item in fixture.cases)
            {
                int draws = 0;
                var rng = new SeededRandom(item.seed);
                var actual = WebEnduranceCompetition.Run(item.participants, () => { draws++; return item.useConstant ? item.constant : rng.NextDouble(); });
                Assert.That(actual.winnerId, Is.EqualTo(item.expected.winnerId), item.name);
                Assert.That(draws, Is.EqualTo(item.expectedDraws), item.name);
                Assert.That(actual.scores.Select(c => c.contestantId), Is.EqualTo(item.expected.scores.Select(c => c.contestantId)), item.name);
                Assert.That(actual.eliminationOrder.Select(c => c.contestantId), Is.EqualTo(item.expected.eliminationOrder.Select(c => c.contestantId)), item.name);
                for (int i = 0; i < actual.scores.Count; i++)
                    Assert.That(actual.scores[i].score, Is.EqualTo(item.expected.scores[i].score).Within(1e-10), item.name);
                for (int i = 0; i < actual.eliminationOrder.Count; i++)
                    Assert.That(actual.eliminationOrder[i].time, Is.EqualTo(item.expected.eliminationOrder[i].time).Within(1e-10), item.name);
            }
        }

        [Test]
        public void NativeFinalPartOneUsesSourceDrawScheduleWithoutMutatingStoredStats()
        {
            var initial = ContentCatalog.Create(87);
            foreach (var guest in initial.contestants.Skip(3)) guest.status = ContestantStatus.Jury;
            initial.phase = EpisodePhase.FinalHoHPart1;
            var effective = initial.Active.Select(c => c.Clone()).ToArray();
            effective.Single(c => c.isPlayer).stats.endurance = Math.Min(10, effective.Single(c => c.isPlayer).stats.endurance + 2);
            var random = new SeededRandom(initial.randomState);
            var expected = WebEnduranceCompetition.Run(effective, random.NextDouble);
            var engine = new EpisodeEngine(initial);
            var command = EpisodeEngineTests.Command(initial, EpisodeCommandKind.Compete);
            command.performance = 1;
            var result = engine.Apply(command);
            Assert.That(result.accepted, Is.True, result.reason);
            Assert.That(result.state.finalPart1WinnerId, Is.EqualTo(expected.winnerId));
            Assert.That(result.state.randomState, Is.EqualTo(random.State));
            Assert.That(result.state.competitionScores.Select(c => c.contestantId), Is.EqualTo(expected.scores.Select(c => c.contestantId)));
            Assert.That(result.state.contestants.Select(c => c.stats.endurance), Is.EqualTo(initial.contestants.Select(c => c.stats.endurance)));
            Assert.That(result.state.contestants.Sum(c => c.hohWins), Is.EqualTo(initial.contestants.Sum(c => c.hohWins)), "Only Part3 awards the HoH title.");
            var restored = new EpisodeEngine(result.state);
            Assert.That(restored.Apply(command).duplicate, Is.True);
            Assert.That(restored.Snapshot.randomState, Is.EqualTo(random.State));
        }

        [Test]
        public void InvalidEnduranceInputsCannotConsumeAnUnboundedOrInvalidRandomSource()
        {
            var participant = ContentCatalog.Create(1).contestants[0];
            Assert.Throws<ArgumentException>(() => WebEnduranceCompetition.Run(Array.Empty<ContestantState>(), () => 0));
            Assert.Throws<ArgumentException>(() => WebEnduranceCompetition.Run(new[] { participant, participant }, () => 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => WebEnduranceCompetition.Run(new[] { participant }, () => 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => WebEnduranceCompetition.Run(new[] { participant }, () => double.NaN));
        }

        [Serializable] private sealed class Goldens { public int schemaVersion; public EnduranceCase[] cases; }
        [Serializable] private sealed class EnduranceCase
        {
            public string name;
            public ContestantState[] participants;
            public uint seed;
            public bool useConstant;
            public double constant;
            public int expectedDraws;
            public WebEnduranceResult expected;
        }
    }
}
