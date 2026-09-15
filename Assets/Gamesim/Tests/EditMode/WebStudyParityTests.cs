using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;
#if UNITY_5_3_OR_NEWER
using System.IO;
using Newtonsoft.Json;
using UnityEngine;
#endif

namespace Gamesim.Tests.EditMode
{
    public sealed class WebStudyParityTests
    {
#if UNITY_5_3_OR_NEWER
        [Test]
        public void StudyAndDistinctCompetitionConsumersMatchExecutedOriginalSource()
        {
            VerifyFixture(JsonConvert.DeserializeObject<Goldens>(File.ReadAllText(Path.Combine(Application.dataPath,
                "Gamesim/Tests/EditMode/Fixtures/WebStudyFixtures.json"))));
        }
#endif

        public static void VerifyFixture(Goldens fixture)
        {
            Assert.That(fixture.schemaVersion, Is.EqualTo(1));
            Assert.That(fixture.availability.Length, Is.EqualTo(22));
            Assert.That(fixture.plans.Length, Is.EqualTo(792));
            Assert.That(fixture.scalarScores.Length, Is.EqualTo(540));
            Assert.That(fixture.rounds.Length, Is.EqualTo(120));
            Assert.That(fixture.fastForward.Length, Is.EqualTo(150));
            Assert.That(fixture.merges.Length, Is.EqualTo(24));
            foreach (var item in fixture.availability)
                Assert.That(WebStudyHouse.IsAvailable(item.phase, item.activePlayer), Is.EqualTo(item.expected), item.phase);
            foreach (var item in fixture.plans)
            {
                int draws = 0;
                var actual = WebStudyHouse.PlanStudy(item.currentBonus, item.approachId, item.sourcePersonalityTraits,
                    () => { draws++; return Parse(item.rollText); });
                Assert.That(draws, Is.EqualTo(item.expectedDraws), item.name);
                foreach (var field in typeof(WebStudyPlan).GetFields())
                    Assert.That(field.GetValue(actual), Is.EqualTo(field.GetValue(item.expected)), item.name + ":" + item.approachId + ":" + item.rollText + ":" + field.Name);
                Assert.That(item.expectedDispatchTypes, Does.Not.Contain("UPDATE_RELATIONSHIPS"));
                Assert.That(item.expectedDispatchTypes, Does.Not.Contain("ADD_NPC_MEMORY"));
                // The fixture captures actual callback dispatches; this leaf does not implement a budget authority.
                Assert.That(item.expectedActionCounter, Is.EqualTo(item.phase == "SocialInteraction" ? 2 : 3));
            }
            foreach (var item in fixture.scalarScores)
            {
                double roll = Parse(item.rollText);
                Assert.That(WebMiniGameScoring.ScoreNpc(item.stats, item.type, roll), Is.EqualTo(item.expectedNpc), "npc:" + item.type + ":" + item.rollText);
                Assert.That(WebMiniGameScoring.SkipScore(item.stats, item.type, item.studyBonus, roll), Is.EqualTo(item.expectedSkip), "skip:" + item.type + ":" + item.rollText);
                Assert.That(WebMiniGameScoring.ThrowScore(roll), Is.EqualTo(item.expectedThrow), "throw:" + item.rollText);
            }
            foreach (var item in fixture.rounds)
            {
                var generator = new SeededRandom(item.seed);
                var samples = new List<uint>();
                Func<double> next = () => { double roll = generator.NextDouble(); samples.Add((uint)(roll * 4294967296)); return roll; };
                var player = item.participants.Single(guest => guest.isPlayer);
                double playerScore = item.mode == "skip" ? WebMiniGameScoring.SkipScore(player.stats, item.type, item.studyBonus, next()) :
                    item.mode == "throw" ? WebMiniGameScoring.ThrowScore(next()) : item.suppliedScore;
                var npcScores = WebMiniGameScoring.GenerateNpcScores(item.participants, item.type, next);
                var ranked = WebMiniGameScoring.MergePlayerWithNpcScores(player.id, player.name, playerScore, npcScores, item.effectiveFocusBonus);
                Assert.That(samples, Is.EqualTo(item.uintSamples), "Exact source RNG numerators: " + item.mode + ":" + item.seed);
                Assert.That(samples.Count, Is.EqualTo(item.expectedDraws));
                SameRankings(ranked, item.expected, item.mode + ":" + item.seed);
            }
            foreach (var item in fixture.fastForward)
            {
                double bonus = WebStudyHouse.FastForwardCompetitionBonus(item.phaseEventCompBonus, item.studyBonus);
                Assert.That(bonus, Is.EqualTo(item.expectedBonus));
                double participantBonus = item.isPlayer ? bonus : 0;
                Assert.That(participantBonus, Is.EqualTo(item.expectedAppliedBonus));
                Assert.That(WebRules.WeightedCompetitionScore(item.stats, item.category, item.nominated, participantBonus, item.rolls[0], item.rolls[1]),
                    Is.EqualTo(item.expectedScore), item.category + ":" + item.isPlayer + ":" + bonus);
                Assert.That(item.expectedDraws, Is.EqualTo(item.category == "Crapshoot" ? 2 : 1));
            }
            foreach (var item in fixture.merges)
                SameRankings(WebMiniGameScoring.MergePlayerWithNpcScores("player", "Player", item.playerScore, item.npcs, item.focusBonus), item.expected, "merge");
        }

        [Test]
        public void FailedMemorizeStillGrantsOneAndTraitFieldsAreNotUnified()
        {
            var failed = WebStudyHouse.PlanStudy(2, "memorize-layout", null, 0.99);
            Assert.That(failed.chance, Is.EqualTo(95));
            Assert.That(failed.success, Is.False);
            Assert.That(failed.bonusDelta, Is.EqualTo(1));
            Assert.That(failed.studyBonus, Is.EqualTo(3));
            var ordinaryGuest = new ContestantState { traits = new List<string> { "Strategic" } };
            Assert.That(ordinaryGuest.traits, Does.Contain("Strategic"));
            Assert.That(WebStudyHouse.SuccessChance("sneak-peek", null), Is.EqualTo(45), "The native missing personalityTraits field remains absent.");
            Assert.That(WebStudyHouse.SuccessChance("sneak-peek", new[] { "strategic" }), Is.EqualTo(45));
            Assert.That(WebStudyHouse.SuccessChance("sneak-peek", new[] { "Strategic", "Strategic" }), Is.EqualTo(60), "The source matching trait is a single boolean bonus.");
            Assert.That(WebStudyHouse.PlanStudy(2, "sneak-peek", null, 0.45).success, Is.True);
            Assert.That(WebStudyHouse.PlanStudy(2, "sneak-peek", null, 0.45000000000000007).success, Is.False);
        }

        [Test]
        public void ClampsProtectIntegerBoundariesWithoutResettingStoredBonus()
        {
            Assert.That(WebStudyHouse.ApplyBonus(5, 2), Is.EqualTo(5));
            Assert.That(WebStudyHouse.ApplyBonus(0, -1), Is.Zero);
            Assert.That(WebStudyHouse.ApplyBonus(int.MaxValue, int.MaxValue), Is.EqualTo(5));
            Assert.That(WebStudyHouse.ApplyBonus(int.MinValue, int.MinValue), Is.Zero);
            Assert.That(WebStudyHouse.ApplyBonus(int.MaxValue, int.MinValue), Is.Zero);
            int savedBonus = 5;
            var stats = new ContestantStats();
            for (int week = 1; week <= 4; week++)
            {
                Assert.That(WebMiniGameScoring.SkipScore(stats, "physical", savedBonus, 0.5), Is.EqualTo(6.5));
                Assert.That(WebStudyHouse.FastForwardCompetitionBonus(3, savedBonus), Is.EqualTo(8));
                Assert.That(savedBonus, Is.EqualTo(5), "These source consumers do not spend or reset study.");
            }
        }

        [Test]
        public void PlayedAndThrowMergeDoNotAcquireStudyOrWeightedBonuses()
        {
            var npcScores = new[] { new WebMiniGameScore { houseguestId = "npc", name = "Npc", score = 4, isPlayer = false } };
            var played = WebMiniGameScoring.MergePlayerWithNpcScores("p", "Player", 5.005, npcScores);
            Assert.That(played.Single(entry => entry.isPlayer).score, Is.EqualTo(5.005), "Played scores are not rounded by merge.");
            Assert.That(WebMiniGameScoring.ThrowScore(0.25), Is.EqualTo(0.75));
            var thrown = WebMiniGameScoring.MergePlayerWithNpcScores("p", "Player", WebMiniGameScoring.ThrowScore(0.25), npcScores);
            Assert.That(thrown.Single(entry => entry.isPlayer).score, Is.EqualTo(0.75));
            Assert.That(WebStudyHouse.FastForwardCompetitionBonus(3, 5), Is.EqualTo(8), "Raw fast-forward input is a separate route, not applied above.");
            Assert.That(WebMiniGameScoring.ApplyWeekFocusScore(5, 3), Is.EqualTo(5.9), "Only explicitly applicable focus is used here.");
            Assert.That(WebMiniGameScoring.ApplyWeekFocusScore(9.9, 3), Is.EqualTo(10));
            Assert.That(WebMiniGameScoring.ApplyWeekFocusScore(0.1, -3), Is.Zero);
        }

        [Test]
        public void StableTiesFavorPlayerThenInputOrderAndCopiesAreDetached()
        {
            var npcs = new[] { new WebMiniGameScore { houseguestId = "second", name = "Second", score = 5 },
                new WebMiniGameScore { houseguestId = "first", name = "First", score = 5 } };
            var ranked = WebMiniGameScoring.MergePlayerWithNpcScores("player", "Player", 5, npcs);
            Assert.That(ranked.Select(entry => entry.id), Is.EqualTo(new[] { "player", "second", "first" }));
            ranked[1].score = 99;
            Assert.That(npcs[0].score, Is.EqualTo(5));
            Assert.That(WebMiniGameScoring.MergePlayerWithNpcScores("player", "Player", 5, Array.Empty<WebMiniGameScore>()).Count, Is.EqualTo(1));
        }

        [Test]
        public void PlayerFilteringConsumesNoSamplesAndPreservesOriginalNpcOrder()
        {
            int draws = 0;
            Func<double> next = () => { draws++; return 0.5; };
            var guests = new[] {
                new WebMiniGameContestant { id = "a", name = "A", stats = new ContestantStats() },
                new WebMiniGameContestant { id = "p", isPlayer = true, stats = null },
                new WebMiniGameContestant { id = "b", name = "B", stats = new ContestantStats() } };
            var scored = WebMiniGameScoring.GenerateNpcScores(guests, "mental", next);
            Assert.That(draws, Is.EqualTo(2));
            Assert.That(scored.Select(entry => entry.houseguestId), Is.EqualTo(new[] { "a", "b" }));
            Assert.That(scored.All(entry => !entry.isPlayer), Is.True);
            Assert.That(guests[0].stats.mental, Is.EqualTo(5));
            draws = 0;
            Assert.That(WebMiniGameScoring.GenerateNpcScores(Array.Empty<WebMiniGameContestant>(), "mental", next), Is.Empty);
            Assert.That(draws, Is.Zero);
        }

        [Test]
        public void InvalidStudyInputCannotRequestRandomSamples()
        {
            int draws = 0;
            Assert.Throws<ArgumentException>(() => WebStudyHouse.PlanStudy(0, "forged", null, () => { draws++; return 0; }));
            Assert.That(draws, Is.Zero);
            Assert.Throws<ArgumentNullException>(() => WebStudyHouse.PlanStudy(0, "memorize-layout", null, (Func<double>)null));
            Assert.That(WebStudyHouse.IsAvailable("FinalHoH", true), Is.False);
            Assert.That(WebStudyHouse.IsAvailable("HoH", false), Is.False);
        }

        [Test]
        public void NonFiniteInputsAndOutOfRangeRandomSamplesAreRejected()
        {
            foreach (double value in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
            {
                Assert.Throws<ArgumentOutOfRangeException>(() => WebStudyHouse.FastForwardCompetitionBonus(value, 2));
                Assert.Throws<ArgumentOutOfRangeException>(() => WebMiniGameScoring.SkipScore(new ContestantStats(), "physical", value, 0));
                Assert.Throws<ArgumentOutOfRangeException>(() => WebMiniGameScoring.ApplyWeekFocusScore(value, 0));
                Assert.Throws<ArgumentOutOfRangeException>(() => WebMiniGameScoring.ApplyWeekFocusScore(0, value));
                var badStats = new ContestantStats { physical = value };
                Assert.Throws<ArgumentOutOfRangeException>(() => WebMiniGameScoring.ScoreNpc(badStats, "physical", 0));
                Assert.Throws<ArgumentOutOfRangeException>(() => WebMiniGameScoring.SkipScore(badStats, "physical", 0, 0));
                Assert.Throws<ArgumentOutOfRangeException>(() => WebMiniGameScoring.MergePlayerWithNpcScores("p", "Player", 0,
                    new[] { new WebMiniGameScore { score = value } }));
            }
            foreach (double roll in new[] { -0.001, 1, double.NaN, double.PositiveInfinity, double.NegativeInfinity })
            {
                Assert.Throws<ArgumentOutOfRangeException>(() => WebStudyHouse.PlanStudy(0, "memorize-layout", null, roll));
                Assert.Throws<ArgumentOutOfRangeException>(() => WebMiniGameScoring.ScoreNpc(new ContestantStats(), "physical", roll));
                Assert.Throws<ArgumentOutOfRangeException>(() => WebMiniGameScoring.SkipScore(new ContestantStats(), "physical", 0, roll));
                Assert.Throws<ArgumentOutOfRangeException>(() => WebMiniGameScoring.ThrowScore(roll));
            }
            Assert.Throws<ArgumentNullException>(() => WebMiniGameScoring.ScoreNpc(null, "physical", 0));
            Assert.Throws<ArgumentException>(() => WebMiniGameScoring.ScoreNpc(new ContestantStats(), "invented", 0));
        }

        private static double Parse(string value) => double.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);
        private static void SameRankings(IReadOnlyList<WebMiniGamePlacement> actual, IReadOnlyList<WebMiniGamePlacement> expected, string label)
        {
            Assert.That(actual.Count, Is.EqualTo(expected.Count), label);
            for (int index = 0; index < actual.Count; index++) foreach (var field in typeof(WebMiniGamePlacement).GetFields())
                Assert.That(field.GetValue(actual[index]), Is.EqualTo(field.GetValue(expected[index])), label + ":" + index + ":" + field.Name);
        }

        [Serializable] public sealed class Goldens
        {
            public int schemaVersion;
            public Availability[] availability;
            public Plan[] plans;
            public ScalarScore[] scalarScores;
            public Round[] rounds;
            public FastForward[] fastForward;
            public Merge[] merges;
        }
        [Serializable] public sealed class Availability { public string phase; public bool activePlayer, expected; }
        [Serializable] public sealed class Plan
        {
            public string name, approachId, rollText, phase;
            public int currentBonus, expectedDraws, expectedActionCounter;
            public string[] sourcePersonalityTraits, ordinaryTraits, expectedDispatchTypes;
            public WebStudyPlan expected;
        }
        [Serializable] public sealed class ScalarScore
        {
            public string type, rollText;
            public ContestantStats stats;
            public int studyBonus;
            public double expectedNpc, expectedSkip, expectedThrow;
        }
        [Serializable] public sealed class Round
        {
            public string mode, type;
            public int studyBonus, week, expectedDraws;
            public uint seed;
            public uint[] uintSamples;
            public double suppliedScore, effectiveFocusBonus;
            public WebMiniGameContestant[] participants;
            public WebMiniGamePlacement[] expected;
        }
        [Serializable] public sealed class FastForward
        {
            public double phaseEventCompBonus, expectedBonus, expectedAppliedBonus, expectedScore;
            public int studyBonus, expectedDraws;
            public string category;
            public bool isPlayer, nominated;
            public ContestantStats stats;
            public double[] rolls;
        }
        [Serializable] public sealed class Merge
        {
            public double playerScore, focusBonus;
            public WebMiniGameScore[] npcs;
            public WebMiniGamePlacement[] expected;
        }
    }
}
