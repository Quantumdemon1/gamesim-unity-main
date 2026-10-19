// Literal JSON boundary controls. No current simulation DTO, enum, event vocabulary or migration
// is used to manufacture the reader's input; accepted controls come from retained writer fixtures.
using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Gamesim.Tests.Frozen28Integration
{
    [TestFixture]
    internal sealed class Frozen28LiteralFixtureTests
    {
        private const string PlayerEdgeReason = "The all-week rules' records are only ever between two houseguests.";
        private const string ActivationReason = "A season without the all-week rules has none of their records.";

        [TestCase("s28-mode1-allweek-w2-campaign", 1)]
        [TestCase("s28-mode1-finished", 1)]
        [TestCase("s28-mode2-allweek-w3-campaign", 2)]
        [TestCase("s28-mode2-finished", 2)]
        public void RetainedWriterPayloadsAreAcceptedWithoutChangingAnyToken(string id, int mode)
        {
            var payload = Fixture(id);
            Assert.That((int)payload["schemaVersion"], Is.EqualTo(28));
            Assert.That((int)payload["unifiedCommitmentRulesVersion"], Is.EqualTo(mode));
            if (mode == 2)
            {
                Assert.That(((JArray)payload["unifiedVoteReveals"]).Count, Is.GreaterThan(0));
                if (id.Contains("campaign"))
                    Assert.That(((JArray)payload["unifiedCommitments"]).Any(row =>
                        (string)row["kind"] == "vote" && (string)row["status"] == "active"), Is.True);
                else Assert.That((int)payload["phase"], Is.EqualTo(13), "The writer's finished checkpoint, not a synthetic reveal.");
            }
            Accept(payload);
        }

        [TestCase("npc-conversation", true)] [TestCase("npc-conversation", false)]
        [TestCase("npc-gossip", true)] [TestCase("npc-gossip", false)]
        [TestCase("veto-save", true)] [TestCase("veto-save", false)]
        [TestCase("veto-replacement", true)] [TestCase("veto-replacement", false)]
        [TestCase("veto-forced", true)] [TestCase("veto-forced", false)]
        public void LiteralAllWeekRecordsAcceptAnNpcPairButRefuseEitherPlayerDirection(string type, bool fromPlayer)
        {
            var control = Fixture("s28-mode2-allweek-w3-campaign");
            Accept(control);
            string player = (string)control["playerId"];
            var npc = Edges(control).First(edge => (string)edge["fromId"] != player && (string)edge["toId"] != player);
            AddRecord(control, npc, type, (int)control["week"]);
            Accept(control);
            var bad = (JObject)control.DeepClone();
            var playerEdge = Edges(bad).First(edge => fromPlayer
                ? (string)edge["fromId"] == player && (string)edge["toId"] != player
                : (string)edge["toId"] == player && (string)edge["fromId"] != player);
            AddRecord(bad, playerEdge, type, (int)bad["week"]);
            Refuse(bad, PlayerEdgeReason);
        }

        [TestCase("npc-conversation", 0)] [TestCase("npc-conversation", 2)]
        [TestCase("npc-gossip", 0)] [TestCase("npc-gossip", 2)]
        [TestCase("veto-save", 0)] [TestCase("veto-save", 2)]
        [TestCase("veto-replacement", 0)] [TestCase("veto-replacement", 2)]
        [TestCase("veto-forced", 0)] [TestCase("veto-forced", 2)]
        public void DisabledOrBeforeStartRecordsKeepActivationRefusalBeforeThePlayerEdgeGuard(string type, int start)
        {
            var control = Fixture("s28-mode1-waved-off-w2-hoh");
            Accept(control);
            control["allWeekRulesStartWeek"] = start;
            Accept(control);
            var bad = (JObject)control.DeepClone();
            string player = (string)bad["playerId"];
            var edge = Edges(bad).First(row => (string)row["fromId"] == player);
            AddRecord(bad, edge, type, 1);
            Refuse(bad, ActivationReason);
        }

        [TestCase(true)] [TestCase(false)]
        public void TheLiteralLieTypeIsNotReclassifiedAsAnNpcOnlyRecord(bool fromPlayer)
        {
            var payload = Fixture("s28-mode2-allweek-w3-campaign");
            Accept(payload);
            string player = (string)payload["playerId"];
            var edge = Edges(payload).First(row => fromPlayer ? (string)row["fromId"] == player : (string)row["toId"] == player);
            AddRecord(payload, edge, "lie", (int)payload["week"]);
            Accept(payload);
        }

        private static JObject Fixture(string id)
        {
            string path = Path.Combine(AppContext.BaseDirectory, "OldSaves", id + ".save.json");
            return (JObject)JObject.Parse(File.ReadAllText(path))["state"];
        }

        private static System.Collections.Generic.IEnumerable<JObject> Edges(JObject payload) =>
            ((JArray)payload["relationships"]).Cast<JObject>();

        private static void AddRecord(JObject payload, JObject edge, string type, int week)
        {
            int next = (int)payload["nextSequence"];
            ((JArray)edge["events"]).Add(new JObject
            {
                ["sequence"] = next, ["week"] = week, ["type"] = type,
                ["description"] = "Literal historical boundary control", ["impactScore"] = 25.0, ["decayable"] = true
            });
            payload["nextSequence"] = next + 1;
        }

        private static void Accept(JObject payload)
        {
            var before = new TokenImage(payload);
            Assert.DoesNotThrow(() => Gamesim.Persistence.FrozenEpisodeV28.Validate(payload));
            before.AssertUnchanged(payload);
        }

        private static void Refuse(JObject payload, string reason)
        {
            var before = new TokenImage(payload);
            for (int attempt = 0; attempt < 2; attempt++)
            {
                var error = Assert.Throws<InvalidDataException>(() => Gamesim.Persistence.FrozenEpisodeV28.Validate(payload));
                Assert.That(error.Message, Is.EqualTo(reason));
                before.AssertUnchanged(payload);
            }
        }

        private sealed class TokenImage
        {
            private readonly string text;
            private readonly JToken[] tokens, parents, previous, next;
            private readonly object[][] annotations;
            internal TokenImage(JObject root)
            {
                root.AddAnnotation(new object());
                root.Properties().First().Value.AddAnnotation(new object());
                text = root.ToString(Formatting.None);
                tokens = root.DescendantsAndSelf().ToArray();
                parents = tokens.Select(t => (JToken)t.Parent).ToArray();
                previous = tokens.Select(t => t.Previous).ToArray();
                next = tokens.Select(t => t.Next).ToArray();
                annotations = tokens.Select(t => t.Annotations<object>().ToArray()).ToArray();
            }
            internal void AssertUnchanged(JObject root)
            {
                Assert.That(root.ToString(Formatting.None), Is.EqualTo(text), "Original JSON and property order.");
                var now = root.DescendantsAndSelf().ToArray();
                Assert.That(now.Length, Is.EqualTo(tokens.Length));
                for (int i = 0; i < now.Length; i++)
                {
                    Assert.That(now[i], Is.SameAs(tokens[i]), "Original token " + i);
                    Assert.That(now[i].Parent, Is.SameAs(parents[i]));
                    Assert.That(now[i].Previous, Is.SameAs(previous[i]));
                    Assert.That(now[i].Next, Is.SameAs(next[i]));
                    var held = now[i].Annotations<object>().ToArray();
                    Assert.That(held.Length, Is.EqualTo(annotations[i].Length));
                    for (int a = 0; a < held.Length; a++) Assert.That(held[a], Is.SameAs(annotations[i][a]));
                }
            }
        }
    }
}
