using System;
using System.IO;
using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace Gamesim.Tests.EditMode
{
    /// <summary>Expected values are captured by executing original web TypeScript, not a second C# implementation.</summary>
    public sealed class WebParityTests
    {
        private Goldens fixtures;

        [OneTimeSetUp]
        public void LoadOriginalTypeScriptFixtures()
        {
            var path = Path.Combine(Application.dataPath, "Gamesim/Tests/EditMode/Fixtures/WebParityFixtures.json");
            fixtures = JsonUtility.FromJson<Goldens>(File.ReadAllText(path));
            Assert.That(fixtures.schemaVersion, Is.EqualTo(1));
            Assert.That(fixtures.sourceFixtureSha256, Has.Length.EqualTo(64));
        }

        [Test]
        public void Hashes_MatchOriginalUtf16Fnv1a()
        {
            foreach (var item in fixtures.hashes)
                Assert.That(SeededRandom.HashSeed(item.input), Is.EqualTo(item.expected), item.input);
        }

        [Test]
        public void RandomSequences_MatchOriginalMulberry32IncludingZeroAndMaxSeeds()
        {
            foreach (var item in fixtures.random)
            {
                var random = new SeededRandom(item.seed);
                Assert.That(item.expectedUInt32, Has.Length.EqualTo(12));
                for (var index = 0; index < item.expectedUInt32.Length; index++)
                    Assert.That(random.NextDouble() * 4294967296d, Is.EqualTo((double)item.expectedUInt32[index]), $"seed {item.seed}, sample {index}");
            }
        }

        [Test]
        public void SavedRandomState_ResumesAtTheNextOriginalSample()
        {
            foreach (var item in fixtures.random)
            {
                var random = new SeededRandom(item.seed);
                for (var index = 0; index < 3; index++) random.NextDouble();
                var resumed = new SeededRandom(random.State);
                for (var index = 3; index < item.expectedUInt32.Length; index++)
                    Assert.That(resumed.NextDouble() * 4294967296d, Is.EqualTo((double)item.expectedUInt32[index]), $"resumed seed {item.seed}");
            }
        }

        [Test]
        public void Shuffle_MatchesOriginalOrderAndPreservesItsInput()
        {
            foreach (var item in fixtures.shuffles)
            {
                var before = item.input.ToArray();
                Assert.That(new SeededRandom(item.seed).Shuffle(item.input), Is.EqualTo(item.expected));
                Assert.That(item.input, Is.EqualTo(before));
            }
        }

        [Test]
        public void SocialArithmetic_MatchesOriginalDirectBonusIndependentReciprocityAndClamp()
        {
            foreach (var item in fixtures.social)
            {
                var actual = WebRules.RelationshipDelta(item.input.change, item.input.social, item.input.isPlayer);
                Assert.That(actual, Is.EqualTo(item.expected.actualChange), "direct social delta");
                Assert.That(WebRules.ClampScore(item.input.direct + actual), Is.EqualTo(item.expected.directScore));
                Assert.That(WebRules.ClampScore(item.input.reciprocal + WebRules.ReciprocalDelta(item.input.change, item.input.roll)),
                    Is.EqualTo(item.expected.reciprocalScore).Within(1e-12));
                Assert.That(WebRules.ReciprocalDelta(item.input.change, 0.25),
                    Is.EqualTo(item.expected.reciprocalEventImpact).Within(1e-12));
            }
        }

        [Test]
        public void PromiseImpacts_MatchOriginalAllTypesStatusesAndLoyaltyExtremes()
        {
            Assert.That(fixtures.promiseImpacts, Has.Length.EqualTo(63));
            foreach (var item in fixtures.promiseImpacts)
                Assert.That(WebRules.PromiseImpact(item.type, item.status, item.loyalty), Is.EqualTo(item.expected),
                    $"{item.type}/{item.status}/loyalty {item.loyalty}");
        }

        [Test]
        public void WeightedScores_MatchOriginalRunnerAllFiveCategories()
        {
            Assert.That(fixtures.weightedCompetitionScores, Has.Length.EqualTo(30));
            foreach (var item in fixtures.weightedCompetitionScores)
                Assert.That(WebRules.WeightedCompetitionScore(item.stats, item.category, item.nominated,
                    item.bonus, item.roll, item.luckRoll), Is.EqualTo(item.expected).Within(1e-12), item.category);
        }

        [Test]
        public void SeededWeightedPlacements_MatchOriginalRunnerDrawOrder()
        {
            foreach (var item in fixtures.weightedCompetitions)
            {
                var random = new SeededRandom(item.seed);
                random.NextDouble(); // Original competition name choice precedes participant scores.
                var actual = item.input.Select(person => new ScoreResult
                {
                    houseguestId = person.id,
                    score = WebRules.WeightedCompetitionScore(person.stats, item.category,
                        item.nominees.Contains(person.id), person.isPlayer ? item.playerCompBonus : 0,
                        random.NextDouble(), item.category == "Crapshoot" ? random.NextDouble() : 0)
                }).OrderByDescending(score => score.score).ToArray();
                Assert.That(actual.Select(score => score.houseguestId),
                    Is.EqualTo(item.expected.results.Select(score => score.houseguestId)), item.category);
                for (var index = 0; index < actual.Length; index++)
                    Assert.That(actual[index].score, Is.EqualTo(item.expected.results[index].score).Within(1e-12));
            }
        }

        [Test]
        public void SocialStatChecksAndFailurePenalties_MatchOriginalThresholds()
        {
            foreach (var item in fixtures.statChecks)
                Assert.That(WebRules.SuccessChance(item.stat, item.required), Is.EqualTo(item.expected));
            foreach (var item in fixtures.failedInteraction)
                Assert.That(WebRules.FailedInteractionPenalty(item.change), Is.EqualTo(item.expected));
        }

        [Serializable] private sealed class Goldens
        {
            public int schemaVersion;
            public string sourceFixtureSha256;
            public HashCase[] hashes;
            public RandomCase[] random;
            public ShuffleCase[] shuffles;
            public SocialCase[] social;
            public PromiseImpactCase[] promiseImpacts;
            public WeightedScoreCase[] weightedCompetitionScores;
            public WeightedRunCase[] weightedCompetitions;
            public StatCase[] statChecks;
            public FailureCase[] failedInteraction;
        }
        [Serializable] private sealed class HashCase { public string input; public uint expected; }
        [Serializable] private sealed class RandomCase { public uint seed; public uint[] expectedUInt32; }
        [Serializable] private sealed class ShuffleCase { public uint seed; public string[] input, expected; }
        [Serializable] private sealed class SocialCase { public SocialInput input; public SocialExpected expected; }
        [Serializable] private sealed class SocialInput
        {
            public double change, social, direct, reciprocal, roll;
            public bool isPlayer;
        }
        [Serializable] private sealed class SocialExpected { public double actualChange, directScore, reciprocalScore, reciprocalEventImpact; }
        [Serializable] private sealed class PromiseImpactCase { public string type, status; public double loyalty, expected; }
        [Serializable] private sealed class WeightedScoreCase
        {
            public ContestantStats stats;
            public string category;
            public bool nominated;
            public double bonus, roll, luckRoll, expected;
        }
        [Serializable] private sealed class WeightedRunCase
        {
            public uint seed;
            public string category;
            public Actor[] input;
            public string[] nominees;
            public double playerCompBonus;
            public CompetitionExpected expected;
        }
        [Serializable] private sealed class Actor { public string id; public ContestantStats stats; public bool isPlayer; }
        [Serializable] private sealed class CompetitionExpected { public ScoreResult[] results; }
        [Serializable] private sealed class ScoreResult { public string houseguestId; public double score; }
        [Serializable] private sealed class StatCase { public double stat, required, expected; }
        [Serializable] private sealed class FailureCase { public double change, expected; }
    }
}
