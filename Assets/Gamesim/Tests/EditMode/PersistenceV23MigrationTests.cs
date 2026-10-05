using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using Gamesim.Persistence;
using Gamesim.Simulation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>Schema 23 adds only a disabled economy version and zero opening debit to historical saves.</summary>
    public sealed class PersistenceV23MigrationTests
    {
        private static JsonSerializer Serializer() => (JsonSerializer)typeof(EpisodeSaveStore).Assembly
            .GetType("Gamesim.Persistence.SaveJson", true).GetMethod("Serializer", BindingFlags.Public | BindingFlags.Static).Invoke(null, null);
        private static JObject AsV22(EpisodeState s)
        {
            var o = PersistenceMigrationTests.StripSchema23(JObject.FromObject(s, Serializer()));
            o["schemaVersion"] = 22;
            return o;
        }
        private static EpisodeState Read(JObject payload)
        {
            var state = EpisodeSaveMigrations.PrepareCurrentPayload(payload, out _).ToObject<EpisodeState>(Serializer());
            EpisodeSaveValidation.Validate(state);
            return state;
        }
        private static string FixturePath => Path.Combine(UnityEngine.Application.dataPath,
            "Gamesim", "Tests", "EditMode", "Fixtures", "V22MonoOpeningSave.json");

        [Test]
        public void RetainedV22PayloadGainsOnlyTwoDisabledFieldsAndKeepsEveryHistoricalValue()
        {
            var old = (JObject)JObject.Parse(File.ReadAllText(FixturePath))["state"];
            string original = old.ToString();
            var next = EpisodeSaveMigrations.PrepareV23Payload(old, out bool migrated);
            Assert.That(migrated, Is.True);
            Assert.That((int)next["schemaVersion"], Is.EqualTo(23));
            Assert.That((int)next["economyRulesVersion"], Is.Zero);
            Assert.That((int)next["moveInExtrasSpent"], Is.Zero);
            var projected = PersistenceMigrationTests.StripSchema23((JObject)next.DeepClone());
            projected["schemaVersion"] = 22;
            Assert.That(JToken.DeepEquals(projected, old), Is.True, "RNG, receipts, budgets, history and every existing rule stay unchanged.");
            Assert.That(old.ToString(), Is.EqualTo(original));
            Assert.That(EpisodeEngine.IsFirstNight(Read(old)), Is.True);
            Assert.That(EpisodeEngine.EconomyRulesOn(Read(old)), Is.False, "Even a pristine historical opening stays legacy.");
            var repeated = EpisodeSaveMigrations.PrepareV23Payload(next, out migrated);
            Assert.That(migrated, Is.False);
            Assert.That(JToken.DeepEquals(next, repeated), Is.True);
            Assert.That(ReferenceEquals(next, repeated), Is.False);
            var frozen = EpisodeSaveMigrations.PrepareV22Payload(old, out migrated);
            Assert.That(migrated, Is.False);
            Assert.That(JToken.DeepEquals(frozen, old), Is.True);
            var current = EpisodeSaveMigrations.PrepareCurrentPayload(old, out migrated);
            Assert.That(migrated, Is.True);
            Assert.That((int)current["schemaVersion"], Is.EqualTo(25));
            Assert.That((int)current["unifiedCommitmentRulesVersion"], Is.Zero);
            Assert.That((JArray)current["unifiedCommitments"], Is.Empty);
            var historical23 = PersistenceMigrationTests.StripSchema24((JObject)current.DeepClone());
            historical23["schemaVersion"] = 23;
            Assert.That(JToken.DeepEquals(historical23, next), Is.True);
            Assert.That(old.ToString(), Is.EqualTo(original));
        }

        [Test]
        public void EveryPhaseOfASyntheticLegacySeasonKeepsItsExactBudgetAndState()
        {
            var s = EconomyRulesTests.Fresh(enable: false);
            s.boughtActionPoints = 2; s.windowActions[Windows.AfterEviction] = 2;
            var engine = new EpisodeEngine(s);
            var phases = new HashSet<EpisodePhase>();
            for (int i = 0; i < 500; i++)
            {
                var before = engine.Snapshot;
                var migrated = Read(AsV22(before));
                Assert.That(JToken.DeepEquals(JObject.FromObject(before, Serializer()), JObject.FromObject(migrated, Serializer())), Is.True);
                Assert.That(EpisodeEngine.SocialActionBudget(migrated), Is.EqualTo(EpisodeEngine.SocialActionBudget(before)));
                Assert.That(EpisodeEngine.SocialActionsSpent(migrated), Is.EqualTo(EpisodeEngine.SocialActionsSpent(before)));
                phases.Add(before.phase);
                if (before.phase == EpisodePhase.Finished) break;
                var result = engine.Apply(EpisodeEngineTests.NextCommand(before));
                Assert.That(result.accepted, Is.True, result.reason);
            }
            Assert.That(phases, Does.Contain(EpisodePhase.Finished));
            Assert.That(phases, Does.Contain(EpisodePhase.Eviction));
            Assert.That(phases, Does.Contain(EpisodePhase.Campaign));
        }

        [Test]
        public void Frozen22RetainsFinalThreeLinkedDealsSettlementsAndJoinedAlliances()
        {
            var s = EconomyRulesTests.Fresh(enable: false);
            EpisodeEngine.EnableCommitments(s);
            string npc = s.Active.First(c => !c.isPlayer).id;
            s.deals.Add(new DealState { id = "final-three", type = DealKind.FinalThree, proposerId = s.playerId,
                recipientId = npc, status = DealStatus.Active, trustImpact = DealTrust.High, week = 1, expiresWeek = 0 });
            s.deals.Add(new DealState { id = "pact", type = DealKind.SafetyAgreement, proposerId = s.playerId,
                recipientId = npc, status = DealStatus.Broken, trustImpact = DealTrust.High, week = 1,
                expiresWeek = 2, brokenById = npc, settledWeek = 1, linkedDealId = "deal-price-pact" });
            s.deals.Add(new DealState { id = "deal-price-pact", type = DealKind.InformationSharing, proposerId = npc,
                recipientId = s.playerId, status = DealStatus.Active, trustImpact = DealTrust.Low, week = 1,
                expiresWeek = 2, linkedDealId = "pact" });
            s.promises.Add(new PromiseState { id = "promise", fromId = npc, toId = s.playerId, kind = PromiseKind.Safety,
                status = PromiseStatus.Broken, week = 1, expiresWeek = 2, brokenById = npc, settledWeek = 1 });
            s.alliances.Add(new AllianceState { id = "joined", name = "Joined pact", active = true, playerJoined = true,
                members = new List<string> { npc, s.playerId } });
            EpisodeSaveValidation.Validate(s);
            var migrated = Read(AsV22(s));
            Assert.That(JToken.DeepEquals(JObject.FromObject(s, Serializer()), JObject.FromObject(migrated, Serializer())), Is.True);
        }

        [TestCase("economyRulesVersion")] [TestCase("moveInExtrasSpent")]
        public void HistoricalSavesCannotSmuggleNewFields(string field)
        {
            var old = AsV22(EconomyRulesTests.Fresh(enable: false)); old[field] = 0;
            Assert.Throws<InvalidDataException>(() => EpisodeSaveMigrations.PrepareCurrentPayload(old, out _));
        }

        [TestCase("missing-boundary")] [TestCase("future-boundary")] [TestCase("string-boundary")]
        [TestCase("missing-breaker")] [TestCase("missing-settlement")] [TestCase("future-settlement")]
        [TestCase("missing-link")] [TestCase("invalid-link")] [TestCase("future-deal-kind")]
        [TestCase("missing-joined")] [TestCase("invalid-joined")] [TestCase("overspent-window")]
        public void Frozen22RefusesMissingMalformedOrFutureFields(string defect)
        {
            var s = EconomyRulesTests.Fresh(enable: false);
            string npc = s.Active.First(c => !c.isPlayer).id;
            s.deals.Add(new DealState { id = "test-deal", type = DealKind.SafetyAgreement, proposerId = s.playerId,
                recipientId = npc, week = 1, status = DealStatus.Active, trustImpact = DealTrust.High });
            s.alliances.Add(new AllianceState { id = "test-alliance", name = "Test", active = true, members = new List<string> { npc, s.playerId } });
            var old = AsV22(s);
            var deal = (JObject)old["deals"][0]; var alliance = (JObject)old["alliances"][0];
            switch (defect)
            {
                case "missing-boundary": old.Remove("commitmentRulesStartWeek"); break;
                case "future-boundary": old["commitmentRulesStartWeek"] = 3; break;
                case "string-boundary": old["commitmentRulesStartWeek"] = "1"; break;
                case "missing-breaker": deal.Remove("brokenById"); break;
                case "missing-settlement": deal.Remove("settledWeek"); break;
                case "future-settlement": deal["settledWeek"] = 2; break;
                case "missing-link": deal.Remove("linkedDealId"); break;
                case "invalid-link": deal["linkedDealId"] = 42; break;
                case "future-deal-kind": deal["type"] = "future-kind"; break;
                case "missing-joined": alliance.Remove("playerJoined"); break;
                case "invalid-joined": alliance["playerJoined"] = "false"; break;
                case "overspent-window": old["windowActions"][0] = 25; break;
                default: Assert.Fail("Unexercised defect"); break;
            }
            Assert.Throws<InvalidDataException>(() => EpisodeSaveMigrations.PrepareCurrentPayload(old, out _));
        }

        [TestCase(0)] [TestCase(26)] [TestCase(-1)]
        public void UnknownSchemasAreNeverGuessed(int schema)
        {
            var old = AsV22(EconomyRulesTests.Fresh(enable: false)); old["schemaVersion"] = schema;
            Assert.Throws<InvalidDataException>(() => EpisodeSaveMigrations.PrepareCurrentPayload(old, out _));
        }

        [TestCase("economyRulesVersion")] [TestCase("moveInExtrasSpent")]
        public void CurrentSaveMustActuallyContainTheNewFields(string field)
        {
            using var files = new Files();
            var payload = JObject.FromObject(EconomyRulesTests.Fresh(), Serializer()); payload.Remove(field);
            File.WriteAllText(files.Store.SavePath, PersistenceMigrationTests.Envelope(payload));
            var original = File.ReadAllBytes(files.Store.SavePath);
            Assert.That(files.Store.TryLoad(out _, out _), Is.False);
            Assert.That(File.ReadAllBytes(files.Store.SavePath), Is.EqualTo(original));
        }

        [Test]
        public void NewEconomyCanSavePartwayThroughOpeningAndRetainItsDebitAfterClosure()
        {
            using var files = new Files();
            var s = EconomyRulesTests.Fresh(); s.boughtActionPoints = 2; s.windowActions[Windows.AfterEviction] = 3;
            files.Store.Save(s);
            var bytes = File.ReadAllBytes(files.Store.SavePath);
            Assert.That(files.Store.TryLoad(out var loaded, out string message), Is.True, message);
            Assert.That(File.ReadAllBytes(files.Store.SavePath), Is.EqualTo(bytes));
            Assert.That(loaded.economyRulesVersion, Is.EqualTo(1));
            var engine = new EpisodeEngine(loaded);
            var result = engine.Apply(new EpisodeCommand { id = "close-opening", actorId = loaded.playerId,
                expectedRevision = loaded.revision, expectedPhase = loaded.phase, kind = EpisodeCommandKind.Advance });
            Assert.That(result.accepted, Is.True, result.reason);
            Assert.That(result.state.moveInExtrasSpent, Is.EqualTo(1));
            files.Store.Save(result.state);
            Assert.That(File.ReadAllBytes(files.Store.BackupPath), Is.EqualTo(bytes));
            Assert.That(files.Store.TryLoad(out var after, out message), Is.True, message);
            Assert.That(after.moveInExtrasSpent, Is.EqualTo(1));
            Assert.That(after.boughtActionPoints, Is.EqualTo(2));
            Assert.That(after.economyRulesVersion, Is.EqualTo(1));
        }

        [Test]
        public void UntouchedV22MonoSaveChecksOriginalChecksumAndKeepsExactBackupOnExplicitSave()
        {
            const string hash = "8a58c522d7b1fa1b3043709ccd395e4246e6613d17798d90d1351659c7a48dfe";
            var original = File.ReadAllBytes(FixturePath);
            Assert.That(original.Length, Is.EqualTo(52534));
            Assert.That(Hash(original), Is.EqualTo(hash), "Never reseal or normalize the historical Mono save bytes; this was not an editor/player run.");
            using var files = new Files();
            // Preserve the fixture's exact bytes, not its read-only archive attribute. Only this
            // fresh isolated test slot is writable; the retained original stays protected.
            File.WriteAllBytes(files.Store.SavePath, original);
            Assert.That(files.Store.TryLoad(out var loaded, out string message), Is.True, message);
            Assert.That(message, Does.Contain("Schema 22").And.Contain("schema 25 in memory"));
            Assert.That(loaded.economyRulesVersion, Is.Zero);
            Assert.That(loaded.moveInExtrasSpent, Is.Zero);
            Assert.That(loaded.unifiedCommitmentRulesVersion, Is.Zero);
            Assert.That(loaded.unifiedCommitments, Is.Empty);
            Assert.That(File.ReadAllBytes(files.Store.SavePath), Is.EqualTo(original));
            Assert.That(File.Exists(files.Store.BackupPath), Is.False);
            files.Store.Save(loaded);
            Assert.That(File.ReadAllBytes(files.Store.BackupPath), Is.EqualTo(original));
            var saved = File.ReadAllBytes(files.Store.SavePath);
            Assert.That(files.Store.TryLoad(out var again, out message), Is.True, message);
            Assert.That(message, Does.Not.Contain("migrated"));
            Assert.That(JToken.DeepEquals(JObject.FromObject(again, Serializer()), JObject.FromObject(loaded, Serializer())), Is.True);
            Assert.That(File.ReadAllBytes(files.Store.SavePath), Is.EqualTo(saved));
            Assert.That(File.ReadAllBytes(FixturePath), Is.EqualTo(original));
        }

        private static string Hash(byte[] bytes)
        {
            using var hash = SHA256.Create();
            return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }
        private sealed class Files : IDisposable
        {
            private readonly string root = Path.Combine(Path.GetTempPath(), "GamesimV23Migration-" + Guid.NewGuid().ToString("N"));
            public readonly EpisodeSaveStore Store;
            public Files() { Directory.CreateDirectory(root); Store = new EpisodeSaveStore(Path.Combine(root, "episode.json")); }
            public void Dispose() { try { Directory.Delete(root, true); } catch (IOException) { } }
        }
    }
}
