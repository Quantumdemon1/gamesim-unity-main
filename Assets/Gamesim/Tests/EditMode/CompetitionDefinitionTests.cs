using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Presentation;
using Gamesim.Simulation;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    public sealed class CompetitionDefinitionTests
    {
        [Test]
        public void PublishedVersion3SelectionsAreFixedAndAlternateWithinEachDiscipline()
        {
            string[] expected = { "switchback-signals-v3", "house-memory-v3", "first-impressions-v3",
                "hold-your-ground-v3", "pressure-cooker-v3", "signal-sprint-v3" };
            var actual = new List<string>();
            for (int week = 1; week <= 3; week++)
            {
                actual.Add(CompetitionDefinitions.Version3(17, week, EpisodePhase.HoH).Id);
                actual.Add(CompetitionDefinitions.Version3(17, week, EpisodePhase.Veto).Id);
            }
            CollectionAssert.AreEqual(expected, actual, "Adding definitions cannot change this published selector.");
            Assert.That(CompetitionDefinitions.All.Select(definition => definition.Id).Distinct().Count(), Is.EqualTo(6));
            foreach (uint seed in new uint[] { 1, 17, 87, 440, uint.MaxValue })
            {
                var previous = new Dictionary<string, string>();
                for (int week = 1; week <= 12; week++)
                foreach (var phase in new[] { EpisodePhase.HoH, EpisodePhase.Veto })
                {
                    var definition = CompetitionDefinitions.Version3(seed, week, phase);
                    Assert.That(definition.Category, Is.EqualTo(EpisodeEngine.CompetitionCategory(phase, week, 3)));
                    if (previous.TryGetValue(definition.Category, out var prior)) Assert.That(definition.Id, Is.Not.EqualTo(prior));
                    previous[definition.Category] = definition.Id;
                }
            }
            Assert.That(CompetitionDefinitions.Version3(17, 7, EpisodePhase.FinalHoHPart1), Is.SameAs(CompetitionDefinitions.PressureCooker));
            Assert.That(CompetitionDefinitions.Version3(17, 7, EpisodePhase.FinalHoHPart2), Is.SameAs(CompetitionDefinitions.SwitchbackSignals));
            Assert.That(CompetitionDefinitions.Version3(17, 7, EpisodePhase.FinalHoHPart3), Is.SameAs(CompetitionDefinitions.FirstImpressions));
        }

        [Test]
        public void SelectingAndReloadingDefinitionsNeverSpendsSeasonRandomness()
        {
            var state = ContentCatalog.Create(17); state.competitionRulesVersion = 3; state.phase = EpisodePhase.HoH;
            string before = JsonConvert.SerializeObject(state);
            var definition = CompetitionDefinitions.For(state);
            var reloaded = JsonConvert.DeserializeObject<EpisodeState>(before);
            Assert.That(CompetitionDefinitions.For(reloaded).Id, Is.EqualTo(definition.Id));
            Assert.That(JsonConvert.SerializeObject(state), Is.EqualTo(before));
            var result = new EpisodeEngine(state).Apply(new EpisodeCommand { id = "definition-test", actorId = state.playerId,
                expectedPhase = state.phase, expectedRevision = state.revision, kind = EpisodeCommandKind.Compete, performance = .5 });
            Assert.That(result.accepted, Is.True, result.reason);
            string text = result.state.events.Single(entry => entry.kind == "competition-definition").text;
            Assert.That(text, Does.StartWith(definition.Id).And.Contain(definition.Title).And.Contain(definition.Summary));
            var restored = JsonConvert.DeserializeObject<EpisodeState>(JsonConvert.SerializeObject(result.state));
            Assert.That(restored.events.Single(entry => entry.kind == "competition-definition").text, Is.EqualTo(text));
            state.competitionRulesVersion = 2;
            Assert.That(CompetitionDefinitions.For(state), Is.Null);
            Assert.Throws<ArgumentException>(() => new MiniGameRun(CompetitionMiniGames.Kind.Memory, 7, 2, CompetitionDefinitions.FirstImpressions));
        }

        [Test]
        public void AlternatingReactionWindowsRemainCompleteAndOfferTheSameTargetsRegardlessOfHits()
        {
            for (uint seed = 1; seed <= 16; seed++)
            {
                var run = new MiniGameRun(CompetitionMiniGames.Kind.Reaction, seed, 3, CompetitionDefinitions.SwitchbackSignals);
                var idle = new MiniGameRun(CompetitionMiniGames.Kind.Reaction, seed, 3, CompetitionDefinitions.SwitchbackSignals);
                int seen = 0;
                while (!run.Finished)
                {
                    run.Tick(.005); idle.Tick(.005);
                    Assert.That(run.Spawned, Is.EqualTo(idle.Spawned));
                    if (run.Spawned == seen) continue;
                    seen = run.Spawned;
                    Assert.That(run.TargetWindowSeconds, Is.EqualTo(seen % 2 == 1 ? .65 : 1.15));
                    Assert.That(run.Remaining + .005001, Is.GreaterThanOrEqualTo(run.TargetWindowSeconds));
                    Assert.That(run.Tap(run.TargetDirection), Is.True);
                }
                Assert.That(run.Score, Is.EqualTo(10));
                Assert.That(idle.ExpiredTargets, Is.EqualTo(idle.Spawned));
            }
        }

        [Test]
        public void PressureWavesChangeEffortCostButRecoveryAndFullMarksRemainAvailable()
        {
            var steady = new MiniGameRun(CompetitionMiniGames.Kind.Endurance, 4, 3, CompetitionDefinitions.HoldYourGround);
            var pressure = new MiniGameRun(CompetitionMiniGames.Kind.Endurance, 4, 3, CompetitionDefinitions.PressureCooker);
            steady.SetHolding(true); pressure.SetHolding(true); steady.Tick(4.5); pressure.Tick(4.5);
            Assert.That(pressure.Meter, Is.EqualTo(steady.Meter).Within(.000001));
            steady.Tick(.5); pressure.Tick(.5);
            Assert.That(pressure.Meter, Is.LessThan(steady.Meter - 3));
            double prior = pressure.Meter; pressure.SetHolding(false); pressure.Tick(.5);
            Assert.That(pressure.Meter - prior, Is.EqualTo(14).Within(.000001), "Waves affect effort, not recovery.");
            var balanced = new MiniGameRun(CompetitionMiniGames.Kind.Endurance, 4, 3, CompetitionDefinitions.PressureCooker);
            for (int frame = 0; frame < 4000 && !balanced.Finished; frame++)
            {
                if (balanced.Meter < 25) balanced.SetHolding(false);
                else if (balanced.Meter > 80) balanced.SetHolding(true);
                balanced.Tick(.01);
            }
            Assert.That(balanced.Finished, Is.True); Assert.That(balanced.Score, Is.EqualTo(10));
            Assert.That(balanced.Elapsed, Is.EqualTo(30));
        }

        [Test]
        public void PreviewMemoryUsesTheShorterClockAndCannotBeAppliedToAnotherGame()
        {
            var run = new MiniGameRun(CompetitionMiniGames.Kind.Memory, 7, 3, CompetitionDefinitions.FirstImpressions);
            Assert.That(run.TimeLimit, Is.EqualTo(24)); Assert.That(run.Pairs, Is.EqualTo(8));
            run.Tick(24); Assert.That(run.Finished, Is.True); Assert.That(run.Score, Is.Zero);
            Assert.Throws<ArgumentException>(() => new MiniGameRun(CompetitionMiniGames.Kind.Endurance, 7, 3, CompetitionDefinitions.FirstImpressions));
        }
    }
}
