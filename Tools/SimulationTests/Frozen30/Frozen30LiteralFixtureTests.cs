// Frozen30 (talk S0, schema 31's step 1): carried from Tools/SimulationTests/Frozen29/Frozen29LiteralFixtureTests.cs,
// then to schema 30: every schema-30 fixture as its build wrote it (each value schema 30 holds beyond 29's - the
// alumni rules and their edition, the tuning rules, dialogue rules 2, competition rules 5, story rules 10 and 11),
// and the schema-29 writer fixtures given schema 30's three literals by hand, exactly as UpgradeV29ToV30 adds them
// (no live code is called). Then the values only a mutation reaches: the house talk's records (story rules 12) and
// the holder's offer's (15).
// Literal JSON boundary controls. No current simulation DTO, enum, event vocabulary or migration
// is used to manufacture the reader's input; accepted controls come from retained writer fixtures.
using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Gamesim.Tests.Frozen30Integration
{
    [TestFixture]
    internal sealed class Frozen30LiteralFixtureTests
    {
        private const string PlayerEdgeReason = "The all-week rules' records are only ever between two houseguests.";
        private const string ActivationReason = "A season without the all-week rules has none of their records.";
        private const string PooledReason = "Schema 30's pooled rules are not enabled in this build.";
        private const string MannersReason = "A season below the house talk's manners has none of its records.";
        private const string HolderReason = "A season without the holder's offer has none of its records.";

        // Every schema-30 writer fixture: each would be refused by a reader that did not carry its value's change.
        [TestCase("s30-default-w2-social", 0)]
        [TestCase("s30-mode2-allstars-w3-campaign", 2)]
        [TestCase("s30-mode2-allstars-alumni1-w3-campaign", 2)]
        [TestCase("s30-mode2-allstars-alumni5-w3-campaign", 2)]
        [TestCase("s30-mode2-allweek-w3-campaign", 2)]
        [TestCase("s30-mode2-allweek-story10-tuning1-w2-hoh", 2)]
        [TestCase("s30-mode2-allweek-story11-competition5-tuning1-w1-veto-meeting", 2)]
        [TestCase("s30-mode2-allweek-story11-tuning1-w3-veto-meeting", 2)]
        [TestCase("s30-mode2-allweek-tuning1-studier-w2-hoh", 2)]
        [TestCase("s30-mode2-allweek-tuning1-w3-campaign", 2)]
        [TestCase("s30-mode2-dialogue1-tuning1-w1-campaign", 2)]
        [TestCase("s30-mode2-dialogue2-tuning1-w1-campaign", 2)]
        public void SchemaThirtyWriterPayloadsAreAcceptedWithoutChangingAnyToken(string id, int mode)
        {
            var payload = Fixture(id);
            Assert.That((int)payload["schemaVersion"], Is.EqualTo(30));
            Assert.That((int)payload["unifiedCommitmentRulesVersion"], Is.EqualTo(mode));
            Accept(payload);
        }

        // Each value the fixtures above hold beyond schema 29's, read back from the fixture so the matrix covers it.
        [TestCase("s30-mode2-allstars-alumni5-w3-campaign", "alumniRulesVersion", 5)]
        [TestCase("s30-mode2-allweek-tuning1-w3-campaign", "tuningRulesVersion", 1)]
        [TestCase("s30-mode2-dialogue2-tuning1-w1-campaign", "dialogueRulesVersion", 2)]
        [TestCase("s30-mode2-allweek-story11-competition5-tuning1-w1-veto-meeting", "competitionRulesVersion", 5)]
        public void TheFixturesHoldSchemaThirtysValues(string id, string field, int value) =>
            Assert.That((int)Fixture(id)[field], Is.EqualTo(value));

        [TestCase("s30-mode2-allstars-alumni5-w3-campaign")]
        public void TheAlumniFixtureHoldsItsHistoriesReceipts(string id)
        {
            var payload = Fixture(id);
            Assert.That(Edges(payload).Any(edge => edge["events"].Any(e => (string)e["type"] == "alumni-history")), Is.True);
            Accept(payload);
        }

        [TestCase("s29-default-w2-social", 0)]
        [TestCase("s29-mode1-allweek-w3-veto-meeting", 1)]
        [TestCase("s29-mode2-allweek-w3-campaign", 2)]
        [TestCase("s29-mode2-dialogue1-w2-campaign", 2)]
        public void EverySchemaTwentyNineWriterCellIsAcceptedOnceGivenSchemaThirtysLiterals(string id, int mode)
        {
            var payload = Lifted(id);
            Assert.That((int)payload["unifiedCommitmentRulesVersion"], Is.EqualTo(mode));
            Accept(payload);
        }

        [TestCase("s30-default-w2-social")]
        [TestCase("s30-mode2-allweek-w3-campaign")]
        public void AnUnliftedSchemaTwentyNinePayloadAndAPayloadMissingAnyLiteralAreRefused(string id)
        {
            RefuseAny(Fixture("s29-mode2-allweek-w3-campaign"));
            var payload = Fixture(id);
            foreach (string field in new[] { "alumniRulesVersion", "alumniCatalogEdition", "tuningRulesVersion" })
            {
                var missing = (JObject)payload.DeepClone();
                missing.Remove(field);
                RefuseAny(missing);
            }
        }

        // The pooled values this build does not define, and an edition the alumni rules 0 cannot hold.
        [TestCase("alumniRulesVersion", 7)]
        [TestCase("alumniRulesVersion", -1)]
        [TestCase("tuningRulesVersion", 2)]
        [TestCase("tuningRulesVersion", -1)]
        public void UndefinedPooledValuesAreRefused(string field, int value)
        {
            var payload = Fixture("s30-mode2-allweek-w3-campaign");
            payload[field] = value;
            Refuse(payload, PooledReason);
        }

        [Test]
        public void AnEditionWithTheAlumniRulesOffIsRefused()
        {
            var payload = Fixture("s30-mode2-allstars-w3-campaign");
            payload["alumniCatalogEdition"] = "allstar-histories-1";
            Refuse(payload, PooledReason);
        }

        [Test]
        public void TheAlumniRulesNeedAnEditionThisBuildKnows()
        {
            var payload = Fixture("s30-mode2-allstars-alumni1-w3-campaign");
            payload["alumniCatalogEdition"] = "allstar-histories-2";
            Refuse(payload, "The alumni rules need the edition their season was built from.");
        }

        [Test]
        public void AnAlumniReceiptInOtherWordsIsRefused()
        {
            var payload = Fixture("s30-mode2-allstars-alumni5-w3-campaign");
            var receipt = Edges(payload).SelectMany(edge => edge["events"]).First(e => (string)e["type"] == "alumni-history");
            receipt["description"] = (string)receipt["description"] + " ";
            Refuse(payload, "Invalid alumni history receipt.");
        }

        // The house talk's records (story rules 12): refused below Manners, accepted at it.
        [Test]
        public void HouseTalkRecordsNeedTheMannersRules()
        {
            var payload = Fixture("s30-mode2-allweek-story11-tuning1-w3-veto-meeting");
            string npc = (string)((JArray)payload["contestants"]).First(c => !(bool)c["isPlayer"])["id"];
            ((JArray)payload["story"]["cooldowns"]).Add(new JObject { ["key"] = "tell:" + npc + ":hedged", ["untilWeek"] = 100 });
            Refuse(payload, MannersReason);
            payload["story"]["rulesVersion"] = 12;
            Accept(payload);
            ((JArray)payload["story"]["cooldowns"]).Add(new JObject { ["key"] = "tell:" + npc + ":definite", ["untilWeek"] = 100 });
            Refuse(payload, "Invalid house talk records.");
        }

        // Story rules 13 and 14 are claimed and not defined; 15 (the holder's offer) is, by its one exception.
        [TestCase(13, false)]
        [TestCase(14, false)]
        [TestCase(15, true)]
        [TestCase(16, false)]
        public void TheStoryLadderAt64d52f59(int version, bool defined)
        {
            var payload = Fixture("s30-mode2-allweek-story11-tuning1-w3-veto-meeting");
            payload["story"]["rulesVersion"] = version;
            if (defined) Accept(payload);
            else RefuseAny(payload);
        }

        // A holder's offer (its id deal-save-) in a season below story rules 15 is refused by its own rule; the prefix meant
        // nothing to the schema-29 image. The fixture's nominee ask, renamed, is the row.
        [Test]
        public void AHoldersOfferNeedsItsRules()
        {
            var payload = Fixture("s30-mode2-allweek-story11-tuning1-w3-veto-meeting");
            Accept(payload);
            var ask = ((JArray)payload["deals"]).Cast<JObject>().Single(d => ((string)d["id"]).StartsWith("deal-veto-", StringComparison.Ordinal));
            ask["id"] = "deal-save-" + ((string)ask["id"]).Substring("deal-veto-".Length);
            Refuse(payload, HolderReason);
        }

        [TestCase(6)]
        public void ACompetitionRuleBeyondTheRingTowerIsRefused(int version)
        {
            var payload = Fixture("s30-mode2-allweek-story11-competition5-tuning1-w1-veto-meeting");
            payload["competitionRulesVersion"] = version;
            RefuseAny(payload);
        }

        [Test]
        public void DialogueRulesThreeAreRefusedAtThirty()
        {
            var payload = Fixture("s30-mode2-dialogue2-tuning1-w1-campaign");
            payload["dialogueRulesVersion"] = 3;
            Refuse(payload, "Dialogue rules are not enabled in this build.");
        }

        [TestCase("npc-conversation", true)] [TestCase("npc-conversation", false)]
        [TestCase("npc-gossip", true)] [TestCase("npc-gossip", false)]
        [TestCase("veto-save", true)] [TestCase("veto-save", false)]
        [TestCase("veto-replacement", true)] [TestCase("veto-replacement", false)]
        [TestCase("veto-forced", true)] [TestCase("veto-forced", false)]
        public void LiteralAllWeekRecordsAcceptAnNpcPairButRefuseEitherPlayerDirection(string type, bool fromPlayer)
        {
            foreach (var control in new[] { Lifted("s29-mode2-allweek-w3-campaign"), Fixture("s30-mode2-allweek-w3-campaign") })
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
            var control = LiftedFrom28("s28-mode1-waved-off-w2-hoh");
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
            foreach (var payload in new[] { Lifted("s29-mode2-allweek-w3-campaign"), Fixture("s30-mode2-allweek-w3-campaign") })
            {
                Accept(payload);
                string player = (string)payload["playerId"];
                var edge = Edges(payload).First(row => fromPlayer ? (string)row["fromId"] == player : (string)row["toId"] == player);
                AddRecord(payload, edge, "lie", (int)payload["week"]);
                Accept(payload);
            }
        }

        // With the alumni rules off, a ledger entry typed "alumni-history" is an ordinary entry, as it was at 29.
        [TestCase(true)] [TestCase(false)]
        public void AnEntryTypedAlumniHistoryIsAnOrdinaryEntryWithTheAlumniRulesOff(bool playerEdge)
        {
            foreach (var payload in new[] { Fixture("s30-default-w2-social"), Fixture("s30-mode2-allweek-w3-campaign") })
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

        // Schema 30's three literals, by hand (EpisodeSaveMigrations.UpgradeV29ToV30's, 936b73a4).
        private static JObject Lifted(string id)
        {
            var payload = Fixture(id);
            Assert.That((int)payload["schemaVersion"], Is.EqualTo(29));
            payload.Add("alumniRulesVersion", 0);
            payload.Add("alumniCatalogEdition", JValue.CreateNull());
            payload.Add("tuningRulesVersion", 0);
            payload["schemaVersion"] = 30;
            return payload;
        }

        // Schema 29's three literals (UpgradeV28ToV29's, 9d87b59e) and then schema 30's, by hand: Frozen29's control season
        // with the all-week rules waved off, which no schema-29 or schema-30 writer fixture holds.
        private static JObject LiftedFrom28(string id)
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
            payload.Add("alumniRulesVersion", 0);
            payload.Add("alumniCatalogEdition", JValue.CreateNull());
            payload.Add("tuningRulesVersion", 0);
            payload["schemaVersion"] = 30;
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
            Assert.DoesNotThrow(() => Gamesim.Persistence.FrozenEpisodeV30.Validate(payload));
            before.AssertUnchanged(payload);
        }

        private static void Refuse(JObject payload, string reason)
        {
            var before = new TokenImage(payload);
            for (int attempt = 0; attempt < 2; attempt++)
            {
                var error = Assert.Throws<InvalidDataException>(() => Gamesim.Persistence.FrozenEpisodeV30.Validate(payload));
                Assert.That(error.Message, Is.EqualTo(reason));
                before.AssertUnchanged(payload);
            }
        }

        private static void RefuseAny(JObject payload)
        {
            var before = new TokenImage(payload);
            Assert.Throws<InvalidDataException>(() => Gamesim.Persistence.FrozenEpisodeV30.Validate(payload));
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
