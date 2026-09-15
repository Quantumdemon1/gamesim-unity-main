using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Gamesim.Simulation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    public sealed class WebNpcConversationParityTests
    {
        private static string FixturePath =>
#if UNITY_EDITOR
            Path.Combine(UnityEngine.Application.dataPath, "Gamesim", "Tests", "EditMode", "Fixtures", "WebNpcConversationFixtures.json");
#else
            Path.Combine(AppContext.BaseDirectory, "WebNpcConversationFixtures.json");
#endif
        // A read-only fixture token graph, parsed once. Individual calls deserialize
        // detached inputs; no new contract resolver or large TestCase argument graph.
        private static readonly Lazy<JObject> Golden = new Lazy<JObject>(() => JObject.Parse(File.ReadAllText(FixturePath)));
        private static T Read<T>(JToken token) => token.ToObject<T>(JsonSerializer.Create(new JsonSerializerSettings
            { ObjectCreationHandling = ObjectCreationHandling.Replace, MissingMemberHandling = MissingMemberHandling.Error }));
        private static string Json(object value) => JsonConvert.SerializeObject(value);
        private static void Equivalent(JToken expected, JToken actual, string path)
        {
            bool Number(JToken token) => token.Type == JTokenType.Float || token.Type == JTokenType.Integer;
            if (Number(expected) && Number(actual))
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
        private sealed class Draws
        {
            private readonly double[] supplied;
            private int consumed;
            public Draws(IEnumerable<double> samples) { supplied = samples.ToArray(); }
            public double Next()
            {
                Assert.That(consumed, Is.LessThan(supplied.Length), "Unexpected extra mechanical draw.");
                return supplied[consumed++];
            }
            public void AssertDone(int expected)
            { Assert.That(consumed, Is.EqualTo(expected)); Assert.That(consumed, Is.EqualTo(supplied.Length)); }
        }

        [Test]
        public void OrderedTraitWeightsMatchAllSourceTraitCases()
        {
            Assert.That(Golden.Value["traits"].Count(), Is.EqualTo(20));
            foreach (var item in Golden.Value["traits"])
            {
                var input = Read<List<string>>(item["input"]); string before = Json(input);
                Equivalent(item["expected"], JArray.FromObject(WebNpcConversations.TraitWeights(input)), (string)item["name"]);
                Assert.That(Json(input), Is.EqualTo(before));
            }
        }

        [Test]
        public void TopicWeightsAndExactBoundaryChoicesMatchOriginalBodies()
        {
            Assert.That(Golden.Value["topics"].Count(), Is.EqualTo(1275));
            foreach (var item in Golden.Value["topics"])
            {
                var input = Read<WebNpcConversationTopicInput>(item["input"]); string before = Json(input);
                Equivalent(item["weights"], JArray.FromObject(WebNpcConversations.TopicWeights(input)), (string)item["name"]);
                var draws = new Draws(item["drawTrace"].Values<double>());
                Assert.That(WebNpcConversations.PickTopic(input, draws.Next), Is.EqualTo((string)item["output"]), (string)item["name"]);
                draws.AssertDone((int)item["randomDraws"]); Assert.That(Json(input), Is.EqualTo(before));
            }
        }

        [Test]
        public void DurationPriorityAndStrictThresholdsMatchOriginalBodies()
        {
            Assert.That(Golden.Value["durations"].Count(), Is.EqualTo(432));
            foreach (var item in Golden.Value["durations"])
            {
                var draws = new Draws(item["drawTrace"].Values<double>());
                double duration = WebNpcConversations.DurationMilliseconds((string)item["topic"], (bool)item["areAllied"], (double)item["score"], draws.Next);
                Assert.That(duration, Is.EqualTo((double)item["output"]).Within(1e-10), (string)item["name"]);
                draws.AssertDone((int)item["randomDraws"]);
            }
        }

        [Test]
        public void StartMemoryHasExactDecayBoundaryAndDetachedDirectedEntries()
        {
            Assert.That(Golden.Value["memories"].Count(), Is.EqualTo(40));
            foreach (var item in Golden.Value["memories"])
            {
                var input = item["input"];
                var forward = Read<WebNpcConversationMemory>(input["forward"]); var reverse = Read<WebNpcConversationMemory>(input["reverse"]);
                string before = Json(new { forward, reverse });
                var result = WebNpcConversations.RecordPairMemory((string)input["firstId"], (string)input["secondId"], (string)input["topic"],
                    (double)input["nowMilliseconds"], forward, reverse);
                Equivalent(item["output"], JObject.FromObject(result), (string)item["name"]);
                Assert.That((int)item["randomDraws"], Is.Zero);
                result.forward.count = 999; result.reverse.lastTopic = "detached change";
                Assert.That(Json(new { forward, reverse }), Is.EqualTo(before));
            }
        }

        [Test]
        public void CompletionHasExactJsRoundingModifiersAndGossipDrawOrdering()
        {
            Assert.That(Golden.Value["completions"].Count(), Is.EqualTo(1031));
            foreach (var item in Golden.Value["completions"])
            {
                var input = Read<WebNpcConversationCompletionInput>(item["input"]); string before = Json(input);
                var draws = new Draws(item["rolls"].Values<double>());
                var result = WebNpcConversations.Complete(input, draws.Next);
                Equivalent(item["output"], JObject.FromObject(result), (string)item["name"]);
                draws.AssertDone((int)item["randomDraws"]);
                result.participants.Clear();
                if (result.gossipTarget != null) result.gossipTarget.id = "detached change";
                Assert.That(Json(input), Is.EqualTo(before));
            }
        }

        [Test]
        public void SourceOrderingKeepsStaleMomentumBeforeStartResetAndCompletionDiminishing()
        {
            foreach (var item in Golden.Value["sequences"])
            {
                var topicInput = Read<WebNpcConversationTopicInput>(item["topicInput"]);
                var topicDraws = new Draws(new[] { (double)item["topicDraw"] });
                string topic = WebNpcConversations.PickTopic(topicInput, topicDraws.Next);
                Assert.That(topic, Is.EqualTo((string)item["topic"])); topicDraws.AssertDone(1);
                var memoryInput = item["memoryInput"];
                var memory = WebNpcConversations.RecordPairMemory((string)memoryInput["firstId"], (string)memoryInput["secondId"], topic,
                    (double)memoryInput["nowMilliseconds"], Read<WebNpcConversationMemory>(memoryInput["forward"]), Read<WebNpcConversationMemory>(memoryInput["reverse"]));
                Equivalent(item["memory"], JObject.FromObject(memory), (string)item["name"]);
                var completionInput = Read<WebNpcConversationCompletionInput>(item["completeInput"]); completionInput.memory = memory.forward;
                var completionDraws = new Draws(item["completionRolls"].Values<double>());
                Equivalent(item["completion"], JObject.FromObject(WebNpcConversations.Complete(completionInput, completionDraws.Next)), (string)item["name"]);
                completionDraws.AssertDone(item["completionRolls"].Count());
            }
            var previous = new WebNpcConversationMemory { partnerId = "second", count = 2, lastTime = 0, lastTopic = "bonding" };
            var started = WebNpcConversations.RecordPairMemory("first", "second", "bonding", 100, previous, null);
            Assert.That(started.forward.count, Is.EqualTo(3));
            var completion = new WebNpcConversationCompletionInput { participants = new List<string> { "first", "second" }, topic = "bonding", memory = started.forward };
            Assert.That(WebNpcConversations.Complete(completion, () => .99).delta, Is.EqualTo(1), "Third start already receives diminishing returns.");
        }

        [Test]
        public void GoldenExplicitlyContainsZeroPairDeltaWithGossipAndNoThirdPartyChanceConsumption()
        {
            var completions = Golden.Value["completions"].ToArray();
            Assert.That(completions.Any(item => (int)item["output"]["delta"] == 0 && item["output"]["gossipTarget"].Type == JTokenType.Object), Is.True);
            Assert.That(completions.Any(item => (string)item["input"]["topic"] == "gossip" && item["input"]["allNpcIds"].Type == JTokenType.Null
                && (int)item["randomDraws"] == 2), Is.True);
            Assert.That(completions.Any(item => item["input"]["allNpcIds"] is JArray array && array.Count == 3 && array.Values<string>().SequenceEqual(new[] { "first", "second", "player" })
                && (int)item["randomDraws"] == 2 && item["output"]["gossipTarget"].Type == JTokenType.Null), Is.True);
        }

        [TestCase(double.NaN)] [TestCase(double.PositiveInfinity)] [TestCase(-.01)] [TestCase(1)]
        public void InvalidInjectedDrawsRejectWithoutMutatingInputs(double sample)
        {
            var input = new WebNpcConversationTopicInput(); string before = Json(input);
            Assert.Throws<ArgumentOutOfRangeException>(() => WebNpcConversations.PickTopic(input, () => sample));
            Assert.That(Json(input), Is.EqualTo(before));
            Assert.Throws<ArgumentOutOfRangeException>(() => WebNpcConversations.DurationMilliseconds("casual", false, 0, () => sample));
            var completion = new WebNpcConversationCompletionInput { participants = new List<string> { "first", "second" }, topic = "gossip" };
            before = Json(completion);
            Assert.Throws<ArgumentOutOfRangeException>(() => WebNpcConversations.Complete(completion, () => sample));
            Assert.That(Json(completion), Is.EqualTo(before));
        }

        [Test]
        public void InvalidCanonicalInputsRejectBeforeAnyDrawAndTopicOrderIsDetached()
        {
            int count = 0; double Draw() { count++; return .5; }
            Assert.Throws<ArgumentException>(() => WebNpcConversations.DurationMilliseconds("unknown", false, 0, Draw));
            Assert.Throws<ArgumentOutOfRangeException>(() => WebNpcConversations.PickTopic(new WebNpcConversationTopicInput { score = double.NaN }, Draw));
            Assert.Throws<ArgumentException>(() => WebNpcConversations.Complete(new WebNpcConversationCompletionInput { participants = new List<string> { "same", "same" }, topic = "casual" }, Draw));
            Assert.That(count, Is.Zero);
            var order = WebNpcConversations.TopicOrder; order[0] = "tamper";
            Assert.That(WebNpcConversations.TopicOrder[0], Is.EqualTo("bonding"));
        }

        [Test]
        public void NoTransientDtoPretendsToBeUnitySerializedOrDropsJsonFields()
        {
            foreach (var type in new[] { typeof(WebNpcConversationMemory), typeof(WebNpcConversationGameContext), typeof(WebNpcConversationTopicInput),
                typeof(WebNpcConversationMemoryPair), typeof(WebNpcConversationCompletionInput), typeof(WebNpcConversationGossipTarget), typeof(WebNpcConversationEnd) })
                Assert.That(Attribute.IsDefined(type, typeof(SerializableAttribute)), Is.False, type.Name);
            var input = new WebNpcConversationCompletionInput { participants = new List<string> { "first", "second" }, topic = "gossip", allNpcIds = null };
            var token = JObject.FromObject(input);
            Assert.That(token["allNpcIds"].Type, Is.EqualTo(JTokenType.Null));
            Assert.That(Json(Read<WebNpcConversationCompletionInput>(token)), Is.EqualTo(Json(input)));
        }
    }
}
