// Frozen29 (SCH30): carried from Tools/SimulationTests/Frozen28/Frozen28LiteralFixtureTests.cs, then to schema 29:
// the schema-29 fixtures as their builds wrote them, and the schema-28 writer fixtures given schema 29's three
// literals by hand, exactly as UpgradeV28ToV29 adds them (no live code is called).
// Literal JSON boundary controls. No current simulation DTO, enum, event vocabulary or migration
// is used to manufacture the reader's input; accepted controls come from retained writer fixtures.
using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Gamesim.Tests.Frozen29Integration
{
    [TestFixture]
    internal sealed class Frozen29LiteralFixtureTests
    {
        private const string PlayerEdgeReason = "The all-week rules' records are only ever between two houseguests.";
        private const string ActivationReason = "A season without the all-week rules has none of their records.";

        [TestCase("s29-default-w2-social", 0)]
        [TestCase("s29-mode1-allweek-w3-veto-meeting", 1)]
        [TestCase("s29-mode2-allweek-w3-campaign", 2)]
        public void SchemaTwentyNineWriterPayloadsAreAcceptedWithoutChangingAnyToken(string id, int mode)
        {
            var payload = Fixture(id);
            Assert.That((int)payload["schemaVersion"], Is.EqualTo(29));
            Assert.That((int)payload["unifiedCommitmentRulesVersion"], Is.EqualTo(mode));
            Assert.That((int)payload["dialogueRulesVersion"], Is.EqualTo(0));
            Accept(payload);
        }

        [TestCase("s28-mode1-allweek-w2-campaign", 1)]
        [TestCase("s28-mode1-finished", 1)]
        [TestCase("s28-mode2-allweek-w3-campaign", 2)]
        [TestCase("s28-mode2-finished", 2)]
        public void RetainedWriterPayloadsAreAcceptedWithoutChangingAnyToken(string id, int mode)
        {
            var payload = Lifted(id);
            Assert.That((int)payload["schemaVersion"], Is.EqualTo(29));
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

        [TestCase("s28-default-w1-social")]
        [TestCase("s28-mode1-leaks-w2-eviction")]
        [TestCase("s28-mode1-war-rooms-w3-veto-meeting")]
        [TestCase("s28-mode1-waved-off-w2-hoh")]
        public void EverySchemaTwentyEightWriterCellIsAcceptedOnceGivenSchemaTwentyNinesLiterals(string id) =>
            Accept(Lifted(id));

        [TestCase("s29-default-w2-social")]
        [TestCase("s29-mode2-allweek-w3-campaign")]
        public void AnUnliftedSchemaTwentyEightPayloadAndAPayloadMissingAnyLiteralAreRefused(string id)
        {
            var original = Fixture("s28-mode2-allweek-w3-campaign");
            RefuseAny(original);
            var payload = Fixture(id);
            foreach (string field in new[] { "dialogueRulesVersion", "dialogue" })
            {
                var missing = (JObject)payload.DeepClone();
                missing.Remove(field);
                RefuseAny(missing);
            }
            var row = (JObject)payload.DeepClone();
            var first = ((JArray)row["unifiedCommitments"]).Cast<JObject>().FirstOrDefault();
            if (first != null)
            {
                first.Remove("conditionalVeto");
                RefuseAny(row);
            }
        }

        [TestCase("npc-conversation", true)] [TestCase("npc-conversation", false)]
        [TestCase("npc-gossip", true)] [TestCase("npc-gossip", false)]
        [TestCase("veto-save", true)] [TestCase("veto-save", false)]
        [TestCase("veto-replacement", true)] [TestCase("veto-replacement", false)]
        [TestCase("veto-forced", true)] [TestCase("veto-forced", false)]
        public void LiteralAllWeekRecordsAcceptAnNpcPairButRefuseEitherPlayerDirection(string type, bool fromPlayer)
        {
            foreach (var control in new[] { Lifted("s28-mode2-allweek-w3-campaign"), Fixture("s29-mode2-allweek-w3-campaign") })
            {
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
        }

        [TestCase("npc-conversation", 0)] [TestCase("npc-conversation", 2)]
        [TestCase("npc-gossip", 0)] [TestCase("npc-gossip", 2)]
        [TestCase("veto-save", 0)] [TestCase("veto-save", 2)]
        [TestCase("veto-replacement", 0)] [TestCase("veto-replacement", 2)]
        [TestCase("veto-forced", 0)] [TestCase("veto-forced", 2)]
        public void DisabledOrBeforeStartRecordsKeepActivationRefusalBeforeThePlayerEdgeGuard(string type, int start)
        {
            var control = Lifted("s28-mode1-waved-off-w2-hoh");
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
            foreach (var payload in new[] { Lifted("s28-mode2-allweek-w3-campaign"), Fixture("s29-mode2-allweek-w3-campaign") })
            {
                Accept(payload);
                string player = (string)payload["playerId"];
                var edge = Edges(payload).First(row => fromPlayer ? (string)row["fromId"] == player : (string)row["toId"] == player);
                AddRecord(payload, edge, "lie", (int)payload["week"]);
                Accept(payload);
            }
        }

        // A ledger entry's type is free text at 29 (ALL-STAR-HISTORIES-PLAN 5.3): one typed "alumni-history" is an
        // ordinary entry, on a player edge and an NPC edge alike.
        [TestCase(true)] [TestCase(false)]
        public void AnEntryTypedAlumniHistoryIsAnOrdinaryEntryAtSchemaTwentyNine(bool playerEdge)
        {
            foreach (var payload in new[] { Fixture("s29-default-w2-social"), Fixture("s29-mode2-allweek-w3-campaign") })
            {
                string player = (string)payload["playerId"];
                var edge = Edges(payload).First(row => playerEdge == ((string)row["fromId"] == player || (string)row["toId"] == player));
                int next = (int)payload["nextSequence"];
                ((JArray)edge["events"]).Add(new JObject
                {
                    ["sequence"] = next, ["week"] = 1, ["type"] = "alumni-history",
                    ["description"] = "Played together before.", ["impactScore"] = 6.0, ["decayable"] = false
                });
                payload["nextSequence"] = next + 1;
                Accept(payload);
            }
        }

        private static JObject Fixture(string id)
        {
            string path = Path.Combine(AppContext.BaseDirectory, "OldSaves", id + ".save.json");
            return (JObject)JObject.Parse(File.ReadAllText(path))["state"];
        }

        // Schema 29's three literals, by hand (EpisodeSaveMigrations.UpgradeV28ToV29's, 9d87b59e).
        private static JObject Lifted(string id)
        {
            var payload = Fixture(id);
            Assert.That((int)payload["schemaVersion"], Is.EqualTo(28));
            payload.Add("dialogueRulesVersion", 0);
            payload.Add("dialogue", new JObject
            {
                ["episodes"] = new JArray(), ["grants"] = new JArray(), ["vetoOccasions"] = new JArray()
            });
            foreach (JObject row in (JArray)payload["unifiedCommitments"])
                row.Add("conditionalVeto", JValue.CreateNull());
            payload["schemaVersion"] = 29;
            return payload;
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
            Assert.DoesNotThrow(() => Gamesim.Persistence.FrozenEpisodeV29.Validate(payload));
            before.AssertUnchanged(payload);
        }

        private static void Refuse(JObject payload, string reason)
        {
            var before = new TokenImage(payload);
            for (int attempt = 0; attempt < 2; attempt++)
            {
                var error = Assert.Throws<InvalidDataException>(() => Gamesim.Persistence.FrozenEpisodeV29.Validate(payload));
                Assert.That(error.Message, Is.EqualTo(reason));
                before.AssertUnchanged(payload);
            }
        }

        private static void RefuseAny(JObject payload)
        {
            var before = new TokenImage(payload);
            Assert.Throws<InvalidDataException>(() => Gamesim.Persistence.FrozenEpisodeV29.Validate(payload));
            before.AssertUnchanged(payload);
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
