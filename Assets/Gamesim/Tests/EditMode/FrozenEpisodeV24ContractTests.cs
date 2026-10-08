using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Gamesim.Persistence;
using Gamesim.Simulation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Exact former24 validation, including semantics its frozen ancestry deliberately did not
    /// own. These are synthetic24 payloads, not a claim of captured shipping24 save artifacts.
    /// Each accepted/rejected public migration checks the caller's complete original tree.
    /// </summary>
    public sealed class FrozenEpisodeV24ContractTests
    {
        private const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly Type Contract = typeof(EpisodeSaveStore).Assembly.GetType("Gamesim.Persistence.FrozenEpisodeV24", true);
        private static object Invoke(Type type, string name, JObject payload)
        {
            try { return type.GetMethod(name, Static).Invoke(null, new object[] { payload }); }
            catch (TargetInvocationException error) { throw error.InnerException ?? error; }
        }
        private static void Validate(JObject payload) => Invoke(Contract, "Validate", payload);
        private static void Accept(JObject payload)
        {
            string original = payload.ToString(Formatting.None);
            Assert.DoesNotThrow(() => Validate(payload));
            var migrated = EpisodeSaveMigrations.UpgradeV24ToV25(payload);
            PersistenceV25TestPayloads.OnlyHearingDefaults(payload, migrated);
            Assert.That(payload.ToString(Formatting.None), Is.EqualTo(original));
            ((JArray)migrated["contestants"])[0]["name"] = "Mutated detached result";
            Assert.That(payload.ToString(Formatting.None), Is.EqualTo(original), "Nested former data is independently owned.");
        }
        private static void Reject(JObject payload)
        {
            string original = payload.ToString(Formatting.None);
            Assert.Throws<InvalidDataException>(() => Validate(payload));
            Assert.Throws<InvalidDataException>(() => EpisodeSaveMigrations.UpgradeV24ToV25(payload));
            Assert.Throws<InvalidDataException>(() => EpisodeSaveMigrations.PrepareCurrentPayload(payload, out _));
            Assert.That(payload.ToString(Formatting.None), Is.EqualTo(original), "A bad historical payload is preserved, not repaired.");
        }

        [TestCase(3, false)] [TestCase(3, true)] [TestCase(8, false)] [TestCase(8, true)]
        [TestCase(16, false)] [TestCase(16, true)]
        public void ExactFormerOpeningsKeepTheirRecordedCastAndEconomy(int size, bool economy)
        {
            var state = EconomyRulesTests.Fresh(size, economy);
            Accept(PersistenceV25TestPayloads.As24(state));
        }

        [TestCase("debit")] [TestCase("pending-pitch")] [TestCase("assessed-pitch")] [TestCase("answered-pitch")]
        [TestCase("emotional")] [TestCase("strategic")] [TestCase("deal")] [TestCase("pressure")]
        [TestCase("quiet")] [TestCase("history")]
        public void ActualEconomyPitchSpeechAndLegacyIncidentRecordsRemainExact(string checkpoint) =>
            Accept(PersistenceV25TestPayloads.As24(PersistenceV25TestPayloads.Checkpoint(checkpoint)));

        [Test]
        public void Former24IsValidatedWithoutDelegatingItsHeaderToTheCurrent25Validator()
        {
            var old = PersistenceV25TestPayloads.As24(PersistenceV25TestPayloads.Fresh());
            Accept(old);
            var detachedCarrier = old.ToObject<EpisodeState>(PersistenceV25TestPayloads.Serializer());
            Assert.That(EpisodeValidation.TryValidate(detachedCarrier, out string reason), Is.False);
            Assert.That(reason, Does.Contain("schema"));
            Assert.Throws<InvalidDataException>(() => EpisodeSaveValidation.Validate(detachedCarrier));
            // Frozen24's own semantics/header accept it; current25 must not pretend it is current.
        }

        [TestCase("missing-version")] [TestCase("missing-list")] [TestCase("null-version")] [TestCase("null-list")]
        [TestCase("text-version")] [TestCase("fraction-version")] [TestCase("negative-version")] [TestCase("enabled-one")]
        [TestCase("future-version")] [TestCase("object-list")] [TestCase("null-row")] [TestCase("nonempty-list")]
        [TestCase("unknown-root")] [TestCase("fraction-schema")] [TestCase("wrong-schema")] [TestCase("unsupported-schema")]
        public void ExactDisabled24FoundationCannotBeDefaultedWidenedOrErased(string defect)
        {
            var old = PersistenceV25TestPayloads.As24(PersistenceV25TestPayloads.Fresh());
            switch (defect)
            {
                case "missing-version": old.Remove("unifiedCommitmentRulesVersion"); break;
                case "missing-list": old.Remove("unifiedCommitments"); break;
                case "null-version": old["unifiedCommitmentRulesVersion"] = JValue.CreateNull(); break;
                case "null-list": old["unifiedCommitments"] = JValue.CreateNull(); break;
                case "text-version": old["unifiedCommitmentRulesVersion"] = "0"; break;
                case "fraction-version": old["unifiedCommitmentRulesVersion"] = 0.0; break;
                case "negative-version": old["unifiedCommitmentRulesVersion"] = -1; break;
                case "enabled-one": old["unifiedCommitmentRulesVersion"] = 1; break;
                case "future-version": old["unifiedCommitmentRulesVersion"] = 2; break;
                case "object-list": old["unifiedCommitments"] = new JObject(); break;
                case "null-row": ((JArray)old["unifiedCommitments"]).Add(JValue.CreateNull()); break;
                case "nonempty-list": ((JArray)old["unifiedCommitments"]).Add(JObject.FromObject(new UnifiedCommitmentState(), PersistenceV25TestPayloads.Serializer())); break;
                case "unknown-root": old["futureAuthority"] = false; break;
                case "fraction-schema": old["schemaVersion"] = 24.0; break;
                case "wrong-schema": old["schemaVersion"] = 25; break;
                case "unsupported-schema": old["schemaVersion"] = 27; break;
                default: Assert.Fail("Unknown defect"); break;
            }
            if (defect == "wrong-schema")
            {
                string original = old.ToString(Formatting.None);
                Assert.Throws<InvalidDataException>(() => Validate(old), "A current header is not a former24 header.");
                Assert.Throws<InvalidDataException>(() => EpisodeSaveMigrations.UpgradeV24ToV25(old));
                // Preserve the original former25 clone-dispatch witness explicitly. Current26
                // instead validates fixed25 before migration and refuses this missing-hearing tree.
                Assert.Throws<InvalidDataException>(() => EpisodeSaveMigrations.PrepareCurrentPayload(old, out _));
                var current = EpisodeSaveMigrations.PrepareV25Payload(old, out bool migrated);
                Assert.That(migrated, Is.False);
                Assert.That(ReferenceEquals(current, old), Is.False);
                Assert.That(JToken.DeepEquals(current, old), Is.True);
                using var files = new PersistenceV25TestPayloads.Files();
                files.Write(current);
                byte[] bytes = File.ReadAllBytes(files.Store.SavePath);
                Assert.That(files.Store.TryLoad(out var installed, out string message), Is.False);
                Assert.That(installed, Is.Null);
                Assert.That(message, Does.Contain("missing or unknown").And.Not.Contain("checksum"));
                Assert.That(File.ReadAllBytes(files.Store.SavePath), Is.EqualTo(bytes));
                Assert.That(File.Exists(files.Store.BackupPath), Is.False);
                files.AssertNoPending();
                ((JArray)current["contestants"])[0]["name"] = "Mutated detached current dispatch";
                Assert.That(old.ToString(Formatting.None), Is.EqualTo(original));
                return;
            }
            Reject(old);
        }

        [TestCase("promise")] [TestCase("deal")]
        public void AncestryShapeAloneCannotAcceptAnActiveRowWithABreakerAndNoSettlement(string family)
        {
            var state = PersistenceV25TestPayloads.Fresh();
            string npc = state.Active.First(c => !c.isPlayer).id;
            var old = PersistenceV25TestPayloads.As24(state);
            if (family == "promise") ((JArray)old["promises"]).Add(JObject.FromObject(new PromiseState {
                id = "contradictory-active", fromId = state.playerId, toId = npc, kind = PromiseKind.Safety,
                status = PromiseStatus.Active, week = 1, expiresWeek = 2, brokenById = state.playerId, settledWeek = 0 }, PersistenceV25TestPayloads.Serializer()));
            else ((JArray)old["deals"]).Add(JObject.FromObject(new DealState { id = "contradictory-active",
                proposerId = state.playerId, recipientId = npc, type = DealKind.SafetyAgreement, status = DealStatus.Active,
                trustImpact = DealTrust.High, week = 1, expiresWeek = 2, brokenById = state.playerId, settledWeek = 0 }, PersistenceV25TestPayloads.Serializer()));
            var ancestry = (JObject)old.DeepClone(); ancestry.Remove("unifiedCommitmentRulesVersion");
            ancestry.Remove("unifiedCommitments"); ancestry["schemaVersion"] = 23;
            var previous = typeof(EpisodeSaveStore).Assembly.GetType("Gamesim.Persistence.FrozenEpisodeV23", true);
            Assert.DoesNotThrow(() => Invoke(previous, "Validate", ancestry), "Frozen23's shape ancestry intentionally leaves complete settlement semantics to its later load boundary.");
            Reject(old); // The actual24 public step MUST reject before returning an upgraded tree.
        }

        [TestCase("wrong-promise-actor")] [TestCase("fulfilled-deal-with-breaker")]
        [TestCase("active-with-settlement")] [TestCase("missing-price-link")]
        [TestCase("nonreciprocal-link")] [TestCase("rules-off-records")]
        public void CompleteFormerSettlementAndPriceOwnershipPredicatesRemainFrozen(string defect)
        {
            var old = PersistenceV25TestPayloads.As24(PersistenceV25TestPayloads.Checkpoint("history"));
            var promise = (JObject)((JArray)old["promises"])[0];
            var deal = (JObject)((JArray)old["deals"])[0];
            switch (defect)
            {
                case "wrong-promise-actor": promise["brokenById"] = promise["toId"].DeepClone(); break;
                case "fulfilled-deal-with-breaker": deal["status"] = "fulfilled"; break;
                case "active-with-settlement": promise["status"] = 0; promise["brokenById"] = JValue.CreateNull(); break;
                case "missing-price-link": ((JObject)((JArray)old["deals"])[1])["linkedDealId"] = JValue.CreateNull(); break;
                case "nonreciprocal-link": ((JObject)((JArray)old["deals"])[1])["linkedDealId"] = "other"; break;
                case "rules-off-records": old["commitmentRulesStartWeek"] = 0; break;
                default: Assert.Fail("Unknown defect"); break;
            }
            Reject(old);
        }

        [TestCase("phase-enum")] [TestCase("contestant-enum")] [TestCase("promise-kind-enum")] [TestCase("promise-status-enum")]
        [TestCase("clock-ahead")] [TestCase("clock-cadence")] [TestCase("unsupported-topic")]
        [TestCase("appearance-version")] [TestCase("appearance-dna")]
        [TestCase("unsupported-story")] [TestCase("future-grudge-cause")] [TestCase("unknown-standing-source")]
        public void FrozenEnumsNpcClockAppearanceAndVocabularyRejectFutureOrCorruptData(string defect)
        {
            var state = PersistenceV25TestPayloads.Checkpoint("history");
            state.Find(state.playerId).appearance = CharacterAppearance.Preset("player");
            state.Find(state.playerId).appearance.dna.Add(new AppearanceValue { id = "height", value = .5f });
            if (defect == "unsupported-topic")
            {
                state = PersistenceV25TestPayloads.Apply(state, EpisodeCommandKind.Talk, state.Active.First(c => !c.isPlayer).id);
                string first = state.Active.First(c => !c.isPlayer).id, second = state.Active.Last(c => !c.isPlayer).id;
                state.npcSocial.nextConversationSequence = 2;
                state.npcSocial.pairMemory.Add(new NpcPairMemoryState { fromId = first, toId = second, lastTopic = "bonding", count = 1 });
                state.npcSocial.pairMemory.Add(new NpcPairMemoryState { fromId = second, toId = first, lastTopic = "bonding", count = 1 });
            }
            var old = PersistenceV25TestPayloads.As24(state);
            Accept(old); // Every negative starts from a separately accepted complete former24 contract.
            var player = ((JArray)old["contestants"]).OfType<JObject>().Single(c => (string)c["id"] == state.playerId);
            switch (defect)
            {
                case "phase-enum": old["phase"] = 16; break;
                case "contestant-enum": old["contestants"][0]["status"] = 6; break;
                case "promise-kind-enum": old["promises"][0]["kind"] = 5; break;
                case "promise-status-enum": old["promises"][0]["status"] = 4; break;
                case "clock-ahead": old["npcSocial"]["clockTick"] = (int)old["revision"] + 1; break;
                case "clock-cadence": old["npcSocial"]["nextScanTick"] = 2; break;
                case "unsupported-topic": foreach (var row in ((JArray)old["npcSocial"]["pairMemory"]).OfType<JObject>()) row["lastTopic"] = "future"; break;
                case "appearance-version": player["appearance"]["version"] = 2; break;
                case "appearance-dna": player["appearance"]["dna"][0]["value"] = 1.01; break;
                case "unsupported-story": old["story"]["rulesVersion"] = 10; break;
                case "future-grudge-cause": ((JArray)old["story"]["grudges"]).Add(JObject.FromObject(new GrudgeState {
                    holderId = state.playerId, targetId = state.Active.First(c => !c.isPlayer).id,
                    cause = "future", originWeek = 1, count = 1, severity = 1 }, PersistenceV25TestPayloads.Serializer())); break;
                case "unknown-standing-source": ((JArray)old["ledger"]["standings"]).Add(JObject.FromObject(new StandingRow {
                    week = 1, fromId = state.playerId, toId = state.Active.First(c => !c.isPlayer).id,
                    source = "future", score = 1 }, PersistenceV25TestPayloads.Serializer())); break;
                default: Assert.Fail("Unknown defect"); break;
            }
            Reject(old);
        }

        [TestCase(false)] [TestCase(true)]
        public void PinnedStoryKindBranchKeepsItsActual24BeatVersusLegacySemantics(bool story)
        {
            var state = PersistenceV25TestPayloads.Fresh();
            var item = new HouseEventState { id = "frozen-branch", kind = story ? "story" : "house",
                title = "Branch", narrative = "Saved historical prose", week = 1 };
            if (story) { item.contentId = "test:beat"; item.surface = "scene"; }
            state.houseEvents.Add(item);
            var old = PersistenceV25TestPayloads.As24(state); Accept(old);
            var row = ((JArray)old["houseEvents"]).OfType<JObject>().Single(e => (string)e["id"] == item.id);
            if (story) row["contentId"] = JValue.CreateNull();
            else row["surface"] = "scene";
            Reject(old);
        }

        [Test]
        public void OrdinaryHistoryAndLegacyUndatedSettlementsArePreservedWithoutInventedIncidents()
        {
            var state = PersistenceV25TestPayloads.Fresh(); string npc = state.Active.First(c => !c.isPlayer).id;
            state.promises.Add(new PromiseState { id = "legacy-undated", fromId = npc, toId = state.playerId,
                kind = PromiseKind.Safety, status = PromiseStatus.Broken, week = 1, expiresWeek = 2 });
            state.ledger.replies.Add(new ReplyRow { week = 1, cardId = "ordinary", kind = "historical-other",
                fromId = npc, listenerId = "", replyKey = "unreserved", promised = true, toThem = 3 });
            state.events.Add(new EpisodeEvent { sequence = state.nextSequence++, week = 1, phase = state.phase,
                kind = "airing-backed", text = "Exact ordinary public history", audienceIds = new List<string> { npc, npc } });
            var old = PersistenceV25TestPayloads.As24(state); Accept(old);
            var upgraded = EpisodeSaveMigrations.UpgradeV24ToV25(old);
            Assert.That(JToken.DeepEquals(upgraded["promises"], old["promises"]), Is.True);
            Assert.That((int)upgraded["promises"][0]["settledWeek"], Is.Zero);
            Assert.That(upgraded["promises"][0]["brokenById"].Type, Is.EqualTo(JTokenType.Null));
        }

        [TestCase(false)] [TestCase(true)]
        public void RealSeasonBothFinaleContractsRemainLegalThroughEveryFrozen24Checkpoint(bool nativeFinale)
        {
            var state = PersistenceV25TestPayloads.Fresh(); if (nativeFinale) state.finaleRulesStartWeek = 1;
            var engine = new EpisodeEngine(state); bool finished = false;
            for (int step = 0; step < 500; step++)
            {
                var snapshot = engine.Snapshot; Accept(PersistenceV25TestPayloads.As24(snapshot));
                if (snapshot.phase == EpisodePhase.Finished) { finished = true; break; }
                var result = engine.Apply(PersistenceV25TestPayloads.Next(snapshot));
                Assert.That(result.accepted, Is.True, result.reason);
            }
            Assert.That(finished, Is.True);
        }

        [Test]
        public void NullHistoricalInputIsRefusedWithoutCreatingAReplacement()
        {
            Assert.Throws<InvalidDataException>(() => Validate(null));
            Assert.Throws<InvalidDataException>(() => EpisodeSaveMigrations.UpgradeV24ToV25(null));
        }
    }
}
