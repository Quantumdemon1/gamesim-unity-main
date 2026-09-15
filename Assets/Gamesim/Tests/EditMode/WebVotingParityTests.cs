using System;
using System.IO;
using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace Gamesim.Tests.EditMode
{
    public sealed class WebVotingParityTests
    {
        private Goldens fixtures;

        [OneTimeSetUp]
        public void LoadOriginalSourceFixtures()
        {
            fixtures = JsonUtility.FromJson<Goldens>(File.ReadAllText(Path.Combine(Application.dataPath,
                "Gamesim/Tests/EditMode/Fixtures/WebVotingFixtures.json")));
            Assert.That(fixtures.schemaVersion, Is.EqualTo(1));
            Assert.That(fixtures.cases, Has.Length.EqualTo(55));
        }

        [Test]
        public void AllTenFactorsAndBothExplanationsMatchOriginalTypeScript()
        {
            foreach (var item in fixtures.cases)
            {
                int randomCalls = 0;
                Func<double> random = item.useExplicitRandom ? (Func<double>)(() => { randomCalls++; return item.tieRoll; }) : null;
                var actual = WebEvictionVoting.Evaluate(item.options, random);
                Assert.That(actual.voterId, Is.EqualTo(item.expected.voterId), item.name);
                Assert.That(actual.selectedNomineeId, Is.EqualTo(item.expected.selectedNomineeId), item.name);
                Assert.That(actual.savedNomineeId, Is.EqualTo(item.expected.savedNomineeId), item.name);
                Assert.That(actual.confidence, Is.EqualTo(item.expected.confidence), item.name);
                Assert.That(actual.margin, Is.EqualTo(item.expected.margin).Within(1e-10), item.name);
                Assert.That(actual.publicReasonCodes, Is.EqualTo(item.expected.publicReasonCodes), item.name);
                Assert.That(actual.privateReasonCodes, Is.EqualTo(item.expected.privateReasonCodes), item.name);
                Assert.That(randomCalls, Is.EqualTo(item.expectedRandomCalls), item.name);
                Assert.That(actual.nomineeEvaluations, Has.Count.EqualTo(2));
                for (int i = 0; i < actual.nomineeEvaluations.Count; i++)
                {
                    var nominee = actual.nomineeEvaluations[i];
                    var expected = item.expected.nomineeEvaluations[i];
                    Assert.That(nominee.nomineeId, Is.EqualTo(expected.nomineeId), item.name);
                    Assert.That(nominee.score, Is.EqualTo(expected.score).Within(1e-10), item.name);
                    Assert.That(nominee.factors, Has.Count.EqualTo(10));
                    for (int j = 0; j < nominee.factors.Count; j++)
                    {
                        var factor = nominee.factors[j];
                        var expectedFactor = expected.factors[j];
                        Assert.That(factor.code, Is.EqualTo(expectedFactor.code), item.name);
                        Assert.That(factor.value, Is.EqualTo(expectedFactor.value).Within(1e-10), item.name + "/" + factor.code);
                        Assert.That(factor.visibility, Is.EqualTo(expectedFactor.visibility), item.name + "/" + factor.code);
                        Assert.That(factor.evidenceIds, Is.EqualTo(expectedFactor.evidenceIds), item.name + "/" + factor.code);
                    }
                }
                Assert.That(WebEvictionVoting.Explain(actual, item.options.nominees), Is.EqualTo(item.publicExplanation), item.name);
                Assert.That(WebEvictionVoting.Explain(actual, item.options.nominees, true), Is.EqualTo(item.privateExplanation), item.name);
            }
        }

        [Test]
        public void GoldenCoverageExercisesEveryFactorWithANonzeroContribution()
        {
            var covered = fixtures.cases.SelectMany(c => c.expected.nomineeEvaluations)
                .SelectMany(n => n.factors).Where(f => f.value != 0).Select(f => f.code).Distinct().OrderBy(x => x).ToArray();
            Assert.That(covered, Is.EqualTo(new[] { "relationship", "threat", "alliance", "deal", "strategicValue", "history", "personality", "memory", "persona", "blocPressure" }.OrderBy(x => x)));
        }

        [Test]
        public void PublicReasonsNeverExposePrivateAllianceDealMemoryOrPersonaFactors()
        {
            foreach (var item in fixtures.cases)
            {
                var actual = WebEvictionVoting.Evaluate(item.options);
                Assert.That(actual.publicReasonCodes.All(code => code == "threat" || code == "history"), Is.True, item.name);
                foreach (var code in actual.publicReasonCodes)
                    Assert.That(actual.nomineeEvaluations.All(n => n.factors.Single(f => f.code == code).visibility != "private"), Is.True, item.name);
            }
        }

        [Test]
        public void NativeAdapterDoesNotFabricateUnstoredAdvancedContext()
        {
            var state = NativeVoteState();
            var options = WebEvictionVoting.FromNative(state, state.contestants[3].id);
            Assert.That(options.state.deals, Is.Empty);
            Assert.That(options.state.relationshipArcs, Is.Empty);
            Assert.That(options.playerPersonaLabel, Is.EqualTo("Neutral"));
            Assert.That(options.blocDirective, Is.Null);
            options.state.relationships[0].score = 99;
            options.state.allActive[0].stats.social = 0;
            Assert.That(state.relationships[0].score, Is.Not.EqualTo(99));
            Assert.That(state.contestants[0].stats.social, Is.Not.EqualTo(0));
        }

        [Test]
        public void NativeMemoryAdapterUsesNewestTenOwnedRecordsOnly()
        {
            var state = NativeVoteState();
            string voter = state.contestants[3].id;
            for (int i = 0; i < 15; i++)
                state.memories.Add(new MemoryState { ownerId = voter, subjectId = state.playerId, text = "Own " + i, week = 1 });
            state.memories.Add(new MemoryState { ownerId = state.contestants[1].id, subjectId = state.playerId, text = "Somebody else's secret", week = 1 });
            var options = WebEvictionVoting.FromNative(state, voter);
            Assert.That(options.memories, Is.EqualTo(Enumerable.Range(5, 10).Reverse().Select(i => "Own " + i)));
        }

        [Test]
        public void NativeEvaluationIsDeterministicAndDoesNotConsumeTheEpisodeRandomStream()
        {
            var state = NativeVoteState();
            uint randomBefore = state.randomState;
            int revisionBefore = state.revision;
            string voter = state.contestants[3].id;
            var first = WebEvictionVoting.EvaluateNative(state, voter);
            var second = WebEvictionVoting.EvaluateNative(state, voter);
            Assert.That(second.selectedNomineeId, Is.EqualTo(first.selectedNomineeId));
            Assert.That(second.nomineeEvaluations.Select(n => n.score), Is.EqualTo(first.nomineeEvaluations.Select(n => n.score)));
            Assert.That(state.randomState, Is.EqualTo(randomBefore));
            Assert.That(state.revision, Is.EqualTo(revisionBefore));
            Assert.That(state.votes, Is.Empty);
        }

        private static EpisodeState NativeVoteState()
        {
            var state = ContentCatalog.Create(72);
            state.nominees.Add(state.contestants[1].id);
            state.nominees.Add(state.contestants[2].id);
            return state;
        }

        [Serializable] private sealed class Goldens { public int schemaVersion; public VoteCase[] cases; }
        [Serializable] private sealed class VoteCase
        {
            public string name, publicExplanation, privateExplanation;
            public WebVoteOptions options;
            public WebVoteEvaluation expected;
            public bool useExplicitRandom;
            public double tieRoll;
            public int expectedRandomCalls;
        }
    }
}
