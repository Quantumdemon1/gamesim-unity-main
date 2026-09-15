using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Gamesim.Simulation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    public sealed class WebVotingBlocParityTests
    {
        public static string FixturePath =>
#if UNITY_EDITOR
            Path.Combine(UnityEngine.Application.dataPath, "Gamesim", "Tests", "EditMode", "Fixtures", "WebBlocFixtures.json");
#else
            Path.Combine(AppContext.BaseDirectory, "WebBlocFixtures.json");
#endif
        public static IEnumerable<TestCaseData> ResolverCases() => Cases("cases", "Web bloc resolver: ");
        public static IEnumerable<TestCaseData> RoundCases() => Cases("rounds", "Web bloc round: ");
        public static IEnumerable<TestCaseData> VoteCases() => Cases("votes", "Web coordinated vote: ");

        [TestCaseSource(nameof(ResolverCases))]
        public void ResolverMatchesOriginalSourceAndExactDrawCount(JObject item)
        {
            var input = Convert<WebBlocSnapshot>(item["input"]);
            var trust = Convert<List<WebBlocScore>>(item["trustScores"]);
            var rolls = Convert<List<double>>(item["rolls"]);
            string before = JsonConvert.SerializeObject(input); int consumed = 0;
            var results = WebVotingBlocs.Resolve(input,
                (from, to) => trust.FirstOrDefault(r => r.fromId == from && r.toId == to)?.score ?? 50,
                () => { Assert.That(consumed, Is.LessThan(rolls.Count), "Unexpected extra RNG draw."); return rolls[consumed++]; });
            Equivalent(item["expected"]["results"], JArray.FromObject(results), "results");
            Equivalent(item["expected"]["directives"], JArray.FromObject(WebVotingBlocs.Flatten(results)), "directives");
            Assert.That(consumed, Is.EqualTo((int)item["expected"]["randomDraws"]));
            Assert.That(consumed, Is.EqualTo(rolls.Count));
            Assert.That(JsonConvert.SerializeObject(input), Is.EqualTo(before));
        }

        [TestCaseSource(nameof(RoundCases))]
        public void SeparateSeededRoundMatchesOriginalCaller(JObject item)
        {
            var input = Convert<WebBlocSnapshot>(item["input"]);
            string before = JsonConvert.SerializeObject(input);
            var result = WebVotingBlocs.ResolveRound(input);
            Equivalent(item["expected"], JObject.FromObject(result), "round");
            Assert.That(JsonConvert.SerializeObject(WebVotingBlocs.ResolveRound(input)), Is.EqualTo(JsonConvert.SerializeObject(result)));
            Assert.That(JsonConvert.SerializeObject(input), Is.EqualTo(before));
        }

        [TestCaseSource(nameof(VoteCases))]
        public void AttachedDirectiveMatchesFullSourceVoteAndPrivateExplanation(JObject item)
        {
            var input = Convert<WebBlocSnapshot>(item["input"]);
            var options = Convert<WebVoteOptions>(item["options"]);
            var round = WebVotingBlocs.ResolveRound(input);
            var directive = round.directives.FirstOrDefault(d => d.voterId == (string)item["voterId"]);
            Equivalent(item["directive"], directive == null ? JValue.CreateNull() : JObject.FromObject(directive), "directive");
            Equivalent(item["uncoordinated"], JObject.FromObject(WebEvictionVoting.Evaluate(options)), "uncoordinated");
            options.blocDirective = directive?.ForVote();
            string before = JsonConvert.SerializeObject(options);
            var evaluation = WebEvictionVoting.Evaluate(options);
            Equivalent(item["expected"], JObject.FromObject(evaluation), "evaluation");
            Assert.That(WebEvictionVoting.Explain(evaluation, options.nominees), Is.EqualTo((string)item["expectedPublic"]));
            Assert.That(WebEvictionVoting.Explain(evaluation, options.nominees, true), Is.EqualTo((string)item["expectedPrivate"]));
            Assert.That(evaluation.publicReasonCodes, Does.Not.Contain("blocPressure"));
            foreach (var factor in evaluation.nomineeEvaluations.SelectMany(n => n.factors).Where(f => f.code == "blocPressure"))
                Assert.That(factor.visibility, Is.EqualTo("private"));
            Assert.That(WebEvictionVoting.Explain(evaluation, options.nominees), Does.Not.Contain("My alliance needs"));
            Assert.That(JsonConvert.SerializeObject(options), Is.EqualTo(before));
        }

        [Test]
        public void GoldenSetContainsChangedVotesPrivateEvidenceAndBothNomineeOrders()
        {
            var votes = Read()["votes"].ToArray();
            Assert.That(votes.Count(v => (string)v["expected"]["selectedNomineeId"] != (string)v["uncoordinated"]["selectedNomineeId"]), Is.GreaterThan(0));
            Assert.That(votes.Any(v => (string)v["expectedPrivate"] != (string)v["expectedPublic"]), Is.True);
            Assert.That(votes.Any(v => !v["input"]["nomineeIds"].Values<string>().SequenceEqual(v["options"]["nominees"].Select(n => (string)n["id"]))), Is.True,
                "Full web round serializes its nominee pair in cast order, independently from bloc target's supplied order.");
        }

        [TestCaseSource(nameof(VoteCases))]
        public void PureNativeRoundPreservesSourceCallerNomineeOrdering(JObject item)
        {
            var state = Native(Convert<WebBlocSnapshot>(item["input"]));
            string before = JsonConvert.SerializeObject(state);
            var result = WebNativeEvictionRound.Evaluate(state, new[] { (string)item["voterId"] });
            Assert.That(result.evaluations.Count, Is.EqualTo(1));
            Equivalent(item["expected"], JObject.FromObject(result.evaluations[0]), "native round");
            Assert.That(JsonConvert.SerializeObject(state), Is.EqualTo(before));
        }

        [Test]
        public void MissingVoterRequestDoesNotFilterPlayerOrCommittedBlocDraws()
        {
            var state = Native();
            state.votes.Add(new VoteState { voterId = "caller", targetId = "nominee-b", reason = "Keep this old ballot" });
            string before = JsonConvert.SerializeObject(state);
            var full = WebNativeEvictionRound.Evaluate(state, new[] { "caller", "partner", "outsider" });
            var onlyMissing = WebNativeEvictionRound.Evaluate(state, new[] { "partner" });
            Assert.That(full.evaluations.Select(e => e.voterId), Does.Not.Contain("caller"), "Human ballots are never invented.");
            Assert.That(JsonConvert.SerializeObject(onlyMissing.coordination), Is.EqualTo(JsonConvert.SerializeObject(full.coordination)));
            Assert.That(JsonConvert.SerializeObject(onlyMissing.evaluations.Single()),
                Is.EqualTo(JsonConvert.SerializeObject(full.evaluations.Single(e => e.voterId == "partner"))));
            Assert.That(onlyMissing.coordination.directives.Select(d => d.voterId), Does.Contain("caller"));
            Assert.That(JsonConvert.SerializeObject(state), Is.EqualTo(before));
        }

        [Test]
        public void ExplicitHohTieBreakHasNoBlocPressureAndReturnsDetachedPrivatePlan()
        {
            var state = Native(); string before = JsonConvert.SerializeObject(state);
            var result = WebNativeEvictionRound.Evaluate(state, new[] { "hoh", "hoh" });
            Assert.That(result.evaluations.Count, Is.EqualTo(1));
            Assert.That(result.coordination.directives.Select(d => d.voterId), Does.Not.Contain("hoh"));
            Assert.That(result.evaluations[0].nomineeEvaluations.SelectMany(e => e.factors)
                .Where(f => f.code == "blocPressure").All(f => f.value == 0), Is.True);
            result.evaluations[0].nomineeEvaluations.Clear(); result.coordination.directives.Clear();
            Assert.That(JsonConvert.SerializeObject(state), Is.EqualTo(before));
        }

        [Test]
        public void NativeAdapterDoesNotInventAllianceMetadataOrConsumePersistedState()
        {
            var state = Native();
            state.votes.Add(new VoteState { voterId = "caller", targetId = "nominee-b", reason = "Historical private ballot" });
            state.events.Add(new EpisodeEvent { sequence = 3, week = 1, phase = EpisodePhase.Eviction,
                kind = "VoteCast", text = "Historical owned event", audienceIds = new List<string> { "caller" } });
            string before = JsonConvert.SerializeObject(state);
            var snapshot = WebVotingBlocs.FromNative(state);
            Assert.That(snapshot.alliances.Single().founderId, Is.Null);
            Assert.That(snapshot.alliances.Single().stability, Is.Null);
            Assert.That(snapshot.grudges, Is.Empty);
            string expected = JsonConvert.SerializeObject(WebVotingBlocs.ResolveRound(snapshot));
            for (int index = 0; index < 20; index++)
                Assert.That(JsonConvert.SerializeObject(WebVotingBlocs.ResolveRound(WebVotingBlocs.FromNative(state))), Is.EqualTo(expected));
            Assert.That(state.randomState, Is.EqualTo(0xabc123u));
            Assert.That(JsonConvert.SerializeObject(state), Is.EqualTo(before), "Planning must not rewrite old ballots, privacy, revision, events or RNG.");
            snapshot.actors[0].traits.Add("Added only to detached copy");
            snapshot.alliances[0].members.Clear(); snapshot.relationships[0].score = 123;
            Assert.That(JsonConvert.SerializeObject(state), Is.EqualTo(before));
        }

        [Test]
        public void OutputPlansAndFlattenedDirectivesAreDetached()
        {
            var snapshot = Basic(); var round = WebVotingBlocs.ResolveRound(snapshot);
            string expected = JsonConvert.SerializeObject(round);
            var first = round.results[0].directives[0];
            var flattened = round.directives.Single(d => d.voterId == first.voterId);
            string firstTarget = first.targetNomineeId;
            flattened.targetNomineeId = "modified copy";
            Assert.That(first.targetNomineeId, Is.EqualTo(firstTarget));
            round.results[0].defectors.Clear(); round.results[0].directives.Clear();
            Assert.That(JsonConvert.SerializeObject(WebVotingBlocs.ResolveRound(snapshot)), Is.EqualTo(expected));
        }

        [Test]
        public void InvalidNomineeCountsReturnNoPlanAndConsumeNoSamples()
        {
            foreach (int count in new[] { 0, 1, 3 })
            {
                var snapshot = Basic(); snapshot.nomineeIds = snapshot.actors.Take(count).Select(a => a.id).ToList();
                int samples = 0;
                Assert.That(WebVotingBlocs.Resolve(snapshot, (_, __) => 50, () => { samples++; return .5; }), Is.Empty);
                Assert.That(samples, Is.Zero);
            }
        }

        [TestCase(double.NaN)] [TestCase(double.PositiveInfinity)] [TestCase(double.NegativeInfinity)]
        [TestCase(-0.0001)] [TestCase(1)]
        public void InvalidRandomDrawRejectsWithoutChangingInput(double sample)
        {
            var snapshot = Basic(); string before = JsonConvert.SerializeObject(snapshot);
            Assert.Throws<ArgumentOutOfRangeException>(() => WebVotingBlocs.Resolve(snapshot, (_, __) => 50, () => sample));
            Assert.That(JsonConvert.SerializeObject(snapshot), Is.EqualTo(before));
        }

        [TestCase(double.NaN)] [TestCase(double.PositiveInfinity)] [TestCase(double.NegativeInfinity)]
        public void NonfiniteTrustScoreStabilityAndGrudgeAreRejected(double value)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => WebVotingBlocs.Resolve(Basic(), (_, __) => value, () => .99));
            var snapshot = Basic(); snapshot.relationships[0].score = value;
            Assert.Throws<ArgumentOutOfRangeException>(() => WebVotingBlocs.ResolveRound(snapshot));
            snapshot = Basic(); snapshot.alliances[0].stability = value;
            Assert.Throws<ArgumentOutOfRangeException>(() => WebVotingBlocs.ResolveRound(snapshot));
            snapshot = Basic(); snapshot.grudges.Add(new WebBlocGrudge { holderId = "partner", targetId = "nominee-a", severity = value });
            Assert.Throws<ArgumentOutOfRangeException>(() => WebVotingBlocs.ResolveRound(snapshot));
        }

        [Test]
        public void AmbiguousCanonicalInputIsRejected()
        {
            var snapshot = Basic(); snapshot.actors.Add(snapshot.actors[0]);
            Assert.Throws<ArgumentException>(() => WebVotingBlocs.ResolveRound(snapshot));
            snapshot = Basic(); snapshot.alliances[0].members.Add(snapshot.alliances[0].members[0]);
            Assert.Throws<ArgumentException>(() => WebVotingBlocs.ResolveRound(snapshot));
            snapshot = Basic(); snapshot.relationships.Add(snapshot.relationships[0]);
            Assert.Throws<ArgumentException>(() => WebVotingBlocs.ResolveRound(snapshot));
            snapshot = Basic(); snapshot.nomineeIds[0] = "missing actor";
            Assert.Throws<ArgumentException>(() => WebVotingBlocs.ResolveRound(snapshot));
        }

        [Test]
        public void RoundHashIsCultureIndependentAndOnlyItsSeedSortsIds()
        {
            var prior = CultureInfo.CurrentCulture;
            try
            {
                var snapshot = Basic(); uint expected = WebVotingBlocs.RoundSeed(snapshot);
                CultureInfo.CurrentCulture = new CultureInfo("tr-TR");
                snapshot.actors.Reverse(); snapshot.nomineeIds.Reverse();
                Assert.That(WebVotingBlocs.RoundSeed(snapshot), Is.EqualTo(expected));
                var result = WebVotingBlocs.ResolveRound(snapshot);
                Assert.That(result.results[0].targetNomineeId, Is.EqualTo(snapshot.nomineeIds[0]));
            }
            finally { CultureInfo.CurrentCulture = prior; }
        }

        [Test]
        public void TransientJsonGraphPreservesMissingZeroAndNonzeroStabilityWithoutUnitySerialization()
        {
            foreach (double? stability in new double?[] { null, 0, 62.5 })
            {
                var snapshot = Basic(); snapshot.alliances[0].stability = stability;
                var json = JObject.FromObject(snapshot);
                Assert.That(json["alliances"][0]["stability"], Is.Not.Null, "Null must be represented, not silently dropped.");
                Assert.That(Convert<WebBlocSnapshot>(json).alliances[0].stability, Is.EqualTo(stability));
                Equivalent(json, JObject.FromObject(Convert<WebBlocSnapshot>(json)), "transient JSON roundtrip");
            }
            foreach (var type in new[] { typeof(WebBlocActor), typeof(WebBlocAlliance), typeof(WebBlocScore),
                typeof(WebBlocGrudge), typeof(WebBlocSnapshot), typeof(WebBlocDirective), typeof(WebBlocResult),
                typeof(WebBlocRound), typeof(WebNativeEvictionRoundPlan) })
                Assert.That(Attribute.IsDefined(type, typeof(SerializableAttribute)), Is.False, type.Name + " is a transient JSON-only DTO.");
        }

        private static IEnumerable<TestCaseData> Cases(string field, string prefix) => Read()[field]
            .Select(item => new TestCaseData(item).SetName(prefix + (string)item["name"]));
        private static JObject Read() => JObject.Parse(File.ReadAllText(FixturePath));
        private static T Convert<T>(JToken token) => token.ToObject<T>(JsonSerializer.Create(new JsonSerializerSettings
            { ObjectCreationHandling = ObjectCreationHandling.Replace, MissingMemberHandling = MissingMemberHandling.Error }));
        private static WebBlocSnapshot Basic() => Convert<WebBlocSnapshot>(Read()["cases"][0]["input"]);
        private static EpisodeState Native(WebBlocSnapshot input = null)
        {
            input = input ?? Basic();
            return new EpisodeState
            {
                sessionId = "bloc-regression-only", seed = 17, randomState = 0xabc123u, revision = 9, week = input.week,
                nextSequence = 4, phase = EpisodePhase.Eviction, playerId = "caller", hohId = "hoh", nominees = new List<string>(input.nomineeIds),
                contestants = input.actors.Select(a => new ContestantState { id = a.id, name = a.name, status = ContestantStatus.Active,
                    isPlayer = a.id == "caller", traits = new List<string>(a.traits) }).ToList(),
                alliances = input.alliances.Select(a => new AllianceState { id = a.id, name = a.name, members = new List<string>(a.members) }).ToList(),
                relationships = input.relationships.Select(r => new RelationshipState { fromId = r.fromId, toId = r.toId, score = r.score }).ToList()
            };
        }
        private static bool Numeric(JToken value) => value.Type == JTokenType.Integer || value.Type == JTokenType.Float;
        internal static void Equivalent(JToken expected, JToken actual, string path)
        {
            if (Numeric(expected) && Numeric(actual))
            { Assert.That((double)actual, Is.EqualTo((double)expected).Within(1e-10), path); return; }
            Assert.That(actual.Type, Is.EqualTo(expected.Type), path);
            if (expected is JObject obj)
            {
                Assert.That(((JObject)actual).Properties().Select(p => p.Name), Is.EquivalentTo(obj.Properties().Select(p => p.Name)), path);
                foreach (var property in obj.Properties()) Equivalent(property.Value, actual[property.Name], path + "." + property.Name);
            }
            else if (expected is JArray array)
            {
                Assert.That(((JArray)actual).Count, Is.EqualTo(array.Count), path);
                for (int index = 0; index < array.Count; index++) Equivalent(array[index], actual[index], path + "[" + index + "]");
            }
            else Assert.That(JToken.DeepEquals(expected, actual), Is.True, path);
        }
    }
}
