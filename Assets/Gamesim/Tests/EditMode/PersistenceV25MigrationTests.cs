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
    /// <summary>
    /// Native SaveStore tests of the disabled schema25 foundation, not hearing activation.
    /// Synthetic exact24 projections are identified as such; the retained21 player bytes are
    /// copied unchanged. No golden is regenerated, and every disk target is a guarded private slot.
    /// </summary>
    public sealed class PersistenceV25MigrationTests
    {
        [TestCase("all-missing")] [TestCase("future-all-missing")] [TestCase("missing-version")] [TestCase("missing-evidence")] [TestCase("missing-receipts")]
        [TestCase("enabled-one")] [TestCase("nonempty-evidence")] [TestCase("nonempty-receipt")]
        public void SyntheticCurrentProjectionCannotEraseIncompleteOrEnabledHearingStorage(string defect)
        {
            var payload = PersistenceV25TestPayloads.Payload(PersistenceV25TestPayloads.Fresh());
            if (defect == "all-missing" || defect == "future-all-missing")
                foreach (string field in new[] { "unifiedHearingRulesVersion", "unifiedHearingEvidence", "unifiedHearingReceipts" }) payload.Remove(field);
            else PersistenceV25TestPayloads.CorruptHearing(payload, defect);
            if (defect == "future-all-missing") payload["schemaVersion"] = 26;
            string original = payload.ToString(Formatting.None);
            Assert.Throws<AssertionException>(() => PersistenceMigrationTests.StripSchema25(payload));
            Assert.That(payload.ToString(Formatting.None), Is.EqualTo(original), "A synthetic downgrade must not hide missing or enabled current fields.");
        }

        [TestCase(23)] [TestCase(24)]
        public void SyntheticProjectionKeepsExactLowerHistoricalTreesWithNoHearingAdditions(int version)
        {
            var v1 = PersistenceMigrationTests.V1Fixture();
            var old = version == 23 ? EpisodeSaveMigrations.PrepareV23Payload(v1, out _)
                : EpisodeSaveMigrations.PrepareV24Payload(v1, out _);
            string original = old.ToString(Formatting.None);
            Assert.That(ReferenceEquals(PersistenceMigrationTests.StripSchema25(old), old), Is.True);
            Assert.That(old.ToString(Formatting.None), Is.EqualTo(original));
        }

        [TestCase(23, "unifiedHearingRulesVersion")] [TestCase(23, "unifiedHearingEvidence")] [TestCase(23, "unifiedHearingReceipts")]
        [TestCase(24, "unifiedHearingRulesVersion")] [TestCase(24, "unifiedHearingEvidence")] [TestCase(24, "unifiedHearingReceipts")]
        public void SyntheticHistoricalProjectionRefusesAnyFutureHearingField(int version, string field)
        {
            var v1 = PersistenceMigrationTests.V1Fixture();
            var old = version == 23 ? EpisodeSaveMigrations.PrepareV23Payload(v1, out _)
                : EpisodeSaveMigrations.PrepareV24Payload(v1, out _);
            old[field] = field == "unifiedHearingRulesVersion" ? (JToken)new JValue(0) : new JArray();
            string original = old.ToString(Formatting.None);
            Assert.Throws<AssertionException>(() => PersistenceMigrationTests.StripSchema25(old));
            Assert.That(old.ToString(Formatting.None), Is.EqualTo(original));
        }

        [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)] [TestCase(6)]
        [TestCase(7)] [TestCase(8)] [TestCase(9)] [TestCase(10)] [TestCase(11)] [TestCase(12)]
        [TestCase(13)] [TestCase(14)] [TestCase(15)] [TestCase(16)] [TestCase(17)] [TestCase(18)]
        [TestCase(19)] [TestCase(20)] [TestCase(21)] [TestCase(22)] [TestCase(23)] [TestCase(24)]
        public void EveryFrozenVersionAddsOnlyDisabledHearingToItsExact24Result(int version)
        {
            var v1 = PersistenceMigrationTests.V1Fixture();
            var old = version == 1 ? v1 : (JObject)typeof(EpisodeSaveMigrations)
                .GetMethod("PrepareV" + version + "Payload", BindingFlags.Public | BindingFlags.Static)
                .Invoke(null, new object[] { v1, false });
            string original = old.ToString(Formatting.None);
            var expected24 = EpisodeSaveMigrations.PrepareV24Payload(old, out _);
            var current = EpisodeSaveMigrations.PrepareCurrentPayload(old, out bool migrated);
            Assert.That(migrated, Is.True);
            PersistenceV25TestPayloads.OnlyHearingDefaults(expected24, current);
            PersistenceV25TestPayloads.Off(current.ToObject<EpisodeState>(PersistenceV25TestPayloads.Serializer()));
            Assert.That(old.ToString(Formatting.None), Is.EqualTo(original));
            var again = EpisodeSaveMigrations.PrepareCurrentPayload(current, out migrated);
            Assert.That(migrated, Is.False);
            Assert.That(JToken.DeepEquals(again, current), Is.True);
            Assert.That(ReferenceEquals(again, current), Is.False);
            ((JArray)again["unifiedHearingEvidence"]).Add(new JObject());
            Assert.That((JArray)current["unifiedHearingEvidence"], Is.Empty);
        }

        [TestCase("legacy")] [TestCase("opening")] [TestCase("debit")]
        [TestCase("pending-pitch")] [TestCase("assessed-pitch")] [TestCase("answered-pitch")]
        [TestCase("emotional")] [TestCase("strategic")] [TestCase("deal")] [TestCase("pressure")] [TestCase("quiet")]
        [TestCase("history")]
        public void CopiedExact24LoadsReadOnlyAndExplicitSaveRetainsOriginalEnvelope(string checkpoint)
        {
            var expected = PersistenceV25TestPayloads.Checkpoint(checkpoint);
            var old = PersistenceV25TestPayloads.As24(expected);
            string treeBefore = old.ToString(Formatting.None);
            using var files = new PersistenceV25TestPayloads.Files();
            files.Write(old);
            byte[] original = File.ReadAllBytes(files.Store.SavePath);
            Assert.That(files.Store.TryLoad(out var loaded, out string message), Is.True, message);
            Assert.That(message, Does.Contain("Schema 24").And.Contain("schema 25 in memory"));
            PersistenceV25TestPayloads.Off(loaded);
            PersistenceV25TestPayloads.Equivalent(expected, loaded);
            PersistenceV25TestPayloads.OnlyHearingDefaults(old, PersistenceV25TestPayloads.Payload(loaded));
            Assert.That(old.ToString(Formatting.None), Is.EqualTo(treeBefore));
            Assert.That(File.ReadAllBytes(files.Store.SavePath), Is.EqualTo(original));
            Assert.That(File.Exists(files.Store.BackupPath), Is.False);
            files.AssertNoPending();

            files.Store.Save(loaded);
            byte[] current = File.ReadAllBytes(files.Store.SavePath);
            Assert.That(File.ReadAllBytes(files.Store.BackupPath), Is.EqualTo(original));
            Assert.That((int)JObject.Parse(File.ReadAllText(files.Store.SavePath))["state"]["schemaVersion"], Is.EqualTo(25));
            Assert.That(files.Store.TryLoad(out var reloaded, out message), Is.True, message);
            Assert.That(message, Does.Not.Contain("migrated"));
            PersistenceV25TestPayloads.Equivalent(expected, reloaded);
            Assert.That(File.ReadAllBytes(files.Store.SavePath), Is.EqualTo(current));
            Assert.That(File.ReadAllBytes(files.Store.BackupPath), Is.EqualTo(original));
            files.AssertNoPending();
        }

        [TestCase(false)] [TestCase(true)]
        public void ActualNewFactoriesStayDisabledAndRoundTripWithoutOptIn(bool builder)
        {
            var state = builder ? SeasonBuilder.Create(new SeasonBuilder.Choice(), 2501) : ContentCatalog.Create(2501);
            PersistenceV25TestPayloads.Off(state);
            using var files = new PersistenceV25TestPayloads.Files();
            files.Store.Save(state);
            byte[] original = File.ReadAllBytes(files.Store.SavePath);
            Assert.That(files.Store.TryLoad(out var loaded, out string message), Is.True, message);
            PersistenceV25TestPayloads.Off(loaded);
            PersistenceV25TestPayloads.Equivalent(state, loaded);
            Assert.That(File.ReadAllBytes(files.Store.SavePath), Is.EqualTo(original));
            Assert.That(File.Exists(files.Store.BackupPath), Is.False);
        }

        [TestCase("missing-version")] [TestCase("missing-evidence")] [TestCase("missing-receipts")]
        [TestCase("null-version")] [TestCase("null-evidence")] [TestCase("null-receipts")]
        [TestCase("text-version")] [TestCase("fraction-version")] [TestCase("negative-version")]
        [TestCase("enabled-one")] [TestCase("future-version")]
        [TestCase("object-evidence")] [TestCase("object-receipts")]
        [TestCase("null-evidence-row")] [TestCase("null-receipt-row")]
        [TestCase("nonempty-evidence")] [TestCase("nonempty-receipt")]
        [TestCase("extra-root")] [TestCase("extra-evidence-field")] [TestCase("unified-one")]
        public void ChecksummedCurrentCorruptionIsRefusedWithoutInstallingOrRewriting(string defect)
        {
            using var files = new PersistenceV25TestPayloads.Files();
            var payload = PersistenceV25TestPayloads.Payload(PersistenceV25TestPayloads.Fresh());
            PersistenceV25TestPayloads.CorruptHearing(payload, defect);
            files.Write(payload);
            byte[] original = File.ReadAllBytes(files.Store.SavePath);
            Assert.That(files.Store.TryLoad(out var loaded, out string message), Is.False, defect);
            Assert.That(loaded, Is.Null);
            Assert.That(message, Does.Not.Contain("checksum"), "The original checksum is valid; shape or semantics must refuse it.");
            Assert.That(File.ReadAllBytes(files.Store.SavePath), Is.EqualTo(original));
            Assert.That(File.Exists(files.Store.BackupPath), Is.False);
            files.AssertNoPending();
        }

        [TestCase("enabled")] [TestCase("negative")] [TestCase("null-evidence")] [TestCase("null-receipts")]
        [TestCase("evidence")] [TestCase("receipt")] [TestCase("unified")]
        public void InvalidCandidateCannotReplaceEitherGoodCopy(string defect)
        {
            using var files = new PersistenceV25TestPayloads.Files();
            var good = PersistenceV25TestPayloads.Fresh();
            files.Store.Save(good); files.Store.Save(good);
            byte[] primary = File.ReadAllBytes(files.Store.SavePath), backup = File.ReadAllBytes(files.Store.BackupPath);
            var bad = good.Clone();
            switch (defect)
            {
                case "enabled": bad.unifiedHearingRulesVersion = 1; break;
                case "negative": bad.unifiedHearingRulesVersion = -1; break;
                case "null-evidence": bad.unifiedHearingEvidence = null; break;
                case "null-receipts": bad.unifiedHearingReceipts = null; break;
                case "evidence": bad.unifiedHearingEvidence.Add(new UnifiedHearingEvidenceState()); break;
                case "receipt": bad.unifiedHearingReceipts.Add(new UnifiedHearingReceiptState()); break;
                case "unified": bad.unifiedCommitmentRulesVersion = 1; break;
                default: Assert.Fail("Unknown defect"); break;
            }
            string originalCandidate = JsonConvert.SerializeObject(bad);
            Assert.Throws<InvalidDataException>(() => files.Store.Save(bad));
            Assert.That(JsonConvert.SerializeObject(bad), Is.EqualTo(originalCandidate));
            Assert.That(File.ReadAllBytes(files.Store.SavePath), Is.EqualTo(primary));
            Assert.That(File.ReadAllBytes(files.Store.BackupPath), Is.EqualTo(backup));
            Assert.That(files.Store.TryLoad(out var loaded, out string message), Is.True, message);
            PersistenceV25TestPayloads.Equivalent(good, loaded);
            files.AssertNoPending();
        }

        [TestCase("unifiedHearingRulesVersion")] [TestCase("unifiedHearingEvidence")] [TestCase("unifiedHearingReceipts")]
        public void Exact24CannotSmuggleEvenDisabled25Fields(string field)
        {
            var old = PersistenceV25TestPayloads.As24(PersistenceV25TestPayloads.Fresh());
            old[field] = field == "unifiedHearingRulesVersion" ? (JToken)new JValue(0) : new JArray();
            string original = old.ToString(Formatting.None);
            Assert.Throws<InvalidDataException>(() => EpisodeSaveMigrations.UpgradeV24ToV25(old));
            Assert.Throws<InvalidDataException>(() => EpisodeSaveMigrations.PrepareCurrentPayload(old, out _));
            using var files = new PersistenceV25TestPayloads.Files(); files.Write(old);
            byte[] bytes = File.ReadAllBytes(files.Store.SavePath);
            Assert.That(files.Store.TryLoad(out var loaded, out _), Is.False);
            Assert.That(loaded, Is.Null);
            Assert.That(File.ReadAllBytes(files.Store.SavePath), Is.EqualTo(bytes));
            Assert.That(old.ToString(Formatting.None), Is.EqualTo(original));
        }

        [TestCase(false)] [TestCase(true)]
        public void OriginalChecksumIsVerifiedBeforeAnyMigrationOrSemanticValidation(bool malformedShape)
        {
            using var files = new PersistenceV25TestPayloads.Files();
            var envelope = JObject.Parse(PersistenceMigrationTests.Envelope(
                PersistenceV25TestPayloads.As24(PersistenceV25TestPayloads.Fresh())));
            // Deliberately do NOT recompute the checksum after tampering with the original24 tree.
            if (malformedShape) ((JObject)envelope["state"]).Remove("unifiedCommitments");
            else envelope["state"]["unifiedCommitmentRulesVersion"] = 1;
            File.WriteAllText(files.Store.SavePath, envelope.ToString(Formatting.None));
            byte[] original = File.ReadAllBytes(files.Store.SavePath);
            Assert.That(files.Store.TryLoad(out var loaded, out string message), Is.False);
            Assert.That(loaded, Is.Null); Assert.That(message, Does.Contain("checksum"));
            Assert.That(File.ReadAllBytes(files.Store.SavePath), Is.EqualTo(original));
            Assert.That(File.Exists(files.Store.BackupPath), Is.False);
            files.AssertNoPending();
        }

        [Test]
        public void DamagedPrimaryCannotBeSilentlyOverwrittenOrRotateTheGoodBackup()
        {
            using var files = new PersistenceV25TestPayloads.Files();
            var good = PersistenceV25TestPayloads.Fresh(); files.Store.Save(good); files.Store.Save(good);
            byte[] backup = File.ReadAllBytes(files.Store.BackupPath);
            File.WriteAllText(files.Store.SavePath, "damaged original25");
            byte[] damaged = File.ReadAllBytes(files.Store.SavePath);
            Assert.Throws<InvalidDataException>(() => files.Store.Save(good));
            Assert.That(File.ReadAllBytes(files.Store.SavePath), Is.EqualTo(damaged));
            Assert.That(File.ReadAllBytes(files.Store.BackupPath), Is.EqualTo(backup));
            Assert.That(files.Store.TryLoad(out var loaded, out string message), Is.False);
            Assert.That(loaded, Is.Null); Assert.That(message, Does.Contain("backup"));
            files.AssertNoPending();
        }

        [TestCase(24)] [TestCase(25)]
        public void ExplicitRecoveryArchivesDamageAndKeepsTheValidatedBackupBytes(int schema)
        {
            using var files = new PersistenceV25TestPayloads.Files();
            var expected = PersistenceV25TestPayloads.Checkpoint("assessed-pitch");
            var payload = schema == 24 ? PersistenceV25TestPayloads.As24(expected) : PersistenceV25TestPayloads.Payload(expected);
            File.WriteAllText(files.Store.BackupPath, PersistenceMigrationTests.Envelope(payload));
            byte[] backup = File.ReadAllBytes(files.Store.BackupPath);
            File.WriteAllText(files.Store.SavePath, "damaged primary before explicit recovery");
            byte[] damaged = File.ReadAllBytes(files.Store.SavePath);
            Assert.That(files.Store.TryLoad(out var missing, out string message), Is.False);
            Assert.That(missing, Is.Null);
            Assert.That(files.Store.TryRecoverBackup(out var loaded, out message), Is.True, message);
            PersistenceV25TestPayloads.Off(loaded);
            PersistenceV25TestPayloads.Equivalent(expected, loaded);
            Assert.That(File.ReadAllBytes(files.Store.SavePath), Is.EqualTo(backup));
            Assert.That(File.ReadAllBytes(files.Store.BackupPath), Is.EqualTo(backup));
            Assert.That(File.ReadAllBytes(Directory.GetFiles(files.Root, "*.before-recovery-*.json").Single()), Is.EqualTo(damaged));
            files.Store.Save(loaded);
            Assert.That(File.ReadAllBytes(files.Store.BackupPath), Is.EqualTo(backup));
            files.AssertNoPending();
        }

        [Test]
        public void InvalidHistoricalBackupCannotBeRecoveredOrArchiveThePrimary()
        {
            using var files = new PersistenceV25TestPayloads.Files();
            var bad = PersistenceV25TestPayloads.As24(PersistenceV25TestPayloads.Fresh());
            bad["unifiedCommitmentRulesVersion"] = 1;
            File.WriteAllText(files.Store.BackupPath, PersistenceMigrationTests.Envelope(bad));
            File.WriteAllText(files.Store.SavePath, "keep this damaged primary too");
            byte[] primary = File.ReadAllBytes(files.Store.SavePath), backup = File.ReadAllBytes(files.Store.BackupPath);
            Assert.That(files.Store.TryRecoverBackup(out var loaded, out _), Is.False);
            Assert.That(loaded, Is.Null);
            Assert.That(File.ReadAllBytes(files.Store.SavePath), Is.EqualTo(primary));
            Assert.That(File.ReadAllBytes(files.Store.BackupPath), Is.EqualTo(backup));
            Assert.That(Directory.GetFiles(files.Root, "*.before-recovery-*.json"), Is.Empty);
            files.AssertNoPending();
        }

        [Test]
        public void TwoExplicitSavesRotateOnlyTheImmediatelyPreviousValidatedEnvelope()
        {
            using var files = new PersistenceV25TestPayloads.Files();
            var expected = PersistenceV25TestPayloads.Checkpoint("debit"); files.Write(PersistenceV25TestPayloads.As24(expected));
            byte[] old = File.ReadAllBytes(files.Store.SavePath);
            Assert.That(files.Store.TryLoad(out var loaded, out string message), Is.True, message);
            files.Store.Save(loaded); byte[] first25 = File.ReadAllBytes(files.Store.SavePath);
            Assert.That(File.ReadAllBytes(files.Store.BackupPath), Is.EqualTo(old));
            var result = new EpisodeEngine(loaded).Apply(PersistenceV25TestPayloads.Next(loaded));
            Assert.That(result.accepted, Is.True, result.reason);
            var advanced = result.state;
            PersistenceV25TestPayloads.Off(advanced);
            files.Store.Save(advanced);
            Assert.That(File.ReadAllBytes(files.Store.BackupPath), Is.EqualTo(first25));
            Assert.That(files.Store.TryLoad(out var current, out message), Is.True, message);
            PersistenceV25TestPayloads.Equivalent(advanced, current);
            files.AssertNoPending();
        }

        [Test]
        public void Original21ShippingSaveLoadsThrough25WithoutRewritingOrResealingItsGolden()
        {
            const string expectedHash = "ec715f20e9e8be1b62d4edfc32cd876b683f13867e47461935abf5a16596f78c";
            string fixture = Path.Combine(UnityEngine.Application.dataPath, "Gamesim", "Tests", "EditMode", "Fixtures", "V21StandaloneProfileSave.json");
            byte[] original = File.ReadAllBytes(fixture);
            Assert.That(original.Length, Is.EqualTo(28122));
            using (var hash = SHA256.Create()) Assert.That(BitConverter.ToString(hash.ComputeHash(original)).Replace("-", "").ToLowerInvariant(), Is.EqualTo(expectedHash));
            var old = (JObject)JObject.Parse(File.ReadAllText(fixture))["state"];
            using var files = new PersistenceV25TestPayloads.Files();
            File.Copy(fixture, files.Store.SavePath, false);
            Assert.That(files.Store.TryLoad(out var loaded, out string message), Is.True, message);
            PersistenceV25TestPayloads.Off(loaded);
            Assert.That(message, Does.Contain("Schema 21").And.Contain("schema 25 in memory"));
            var projection = PersistenceMigrationTests.StripSchema22(PersistenceV25TestPayloads.Payload(loaded)); projection["schemaVersion"] = 21;
            Assert.That(JToken.DeepEquals(projection, old), Is.True);
            Assert.That(File.ReadAllBytes(files.Store.SavePath), Is.EqualTo(original));
            Assert.That(File.Exists(files.Store.BackupPath), Is.False);
            files.Store.Save(loaded);
            Assert.That(File.ReadAllBytes(files.Store.BackupPath), Is.EqualTo(original));
            Assert.That(File.ReadAllBytes(fixture), Is.EqualTo(original));
            files.AssertNoPending();
        }

        [Test]
        public void CloneOwnsEvidenceFactsKnowersAndReceiptsWithoutNormalizingInvalidStorage()
        {
            var original = PersistenceV25TestPayloads.Fresh();
            string npc = original.Active.First(c => !c.isPlayer).id;
            original.unifiedHearingEvidence.Add(new UnifiedHearingEvidenceState { incidentKey = "detached", fact = new HouseFactState {
                id = "detached-fact", kind = "broken-word", actorId = original.playerId, subjectId = npc,
                visibility = "private", week = 1, knowers = new List<string> { original.playerId, npc } } });
            original.unifiedHearingReceipts.Add(new UnifiedHearingReceiptState { incidentKey = "detached", listenerId = npc,
                factId = "detached-fact", kind = "initial", heardWeek = 1 });
            var copy = original.Clone();
            Assert.That(ReferenceEquals(copy.unifiedHearingEvidence, original.unifiedHearingEvidence), Is.False);
            Assert.That(ReferenceEquals(copy.unifiedHearingEvidence[0], original.unifiedHearingEvidence[0]), Is.False);
            Assert.That(ReferenceEquals(copy.unifiedHearingEvidence[0].fact, original.unifiedHearingEvidence[0].fact), Is.False);
            Assert.That(ReferenceEquals(copy.unifiedHearingEvidence[0].fact.knowers, original.unifiedHearingEvidence[0].fact.knowers), Is.False);
            Assert.That(ReferenceEquals(copy.unifiedHearingReceipts[0], original.unifiedHearingReceipts[0]), Is.False);
            copy.unifiedHearingEvidence[0].incidentKey = "changed"; copy.unifiedHearingEvidence[0].fact.knowers.Clear();
            copy.unifiedHearingReceipts[0].listenerId = original.playerId;
            Assert.That(original.unifiedHearingEvidence[0].incidentKey, Is.EqualTo("detached"));
            Assert.That(original.unifiedHearingEvidence[0].fact.knowers, Has.Count.EqualTo(2));
            Assert.That(original.unifiedHearingReceipts[0].listenerId, Is.EqualTo(npc));
            Assert.Throws<InvalidDataException>(() => EpisodeSaveValidation.Validate(original));
            original.unifiedHearingEvidence = null; original.unifiedHearingReceipts = null;
            var nullCopy = original.Clone(); Assert.That(nullCopy.unifiedHearingEvidence, Is.Null); Assert.That(nullCopy.unifiedHearingReceipts, Is.Null);
            original.unifiedHearingEvidence = new List<UnifiedHearingEvidenceState> { null };
            original.unifiedHearingReceipts = new List<UnifiedHearingReceiptState> { null };
            Assert.That(original.Clone().unifiedHearingEvidence.Single(), Is.Null);
            Assert.That(original.Clone().unifiedHearingReceipts.Single(), Is.Null);
        }

        [TestCase(false)] [TestCase(true)]
        public void ActualSeasonCommandsAndDiskRoundTripsKeepDisabled25StateAndEveryOutcome(bool nativeFinale)
        {
            var state = PersistenceV25TestPayloads.Fresh(); if (nativeFinale) state.finaleRulesStartWeek = 1;
            var direct = new EpisodeEngine(state); var restored = new EpisodeEngine(state);
            using var files = new PersistenceV25TestPayloads.Files();
            var phases = new HashSet<EpisodePhase>();
            for (int step = 0; step < 500; step++)
            {
                var before = direct.Snapshot; phases.Add(before.phase);
                PersistenceV25TestPayloads.Off(before);
                PersistenceV25TestPayloads.Equivalent(before, restored.Snapshot);
                if (before.phase == EpisodePhase.Finished) break;
                var command = PersistenceV25TestPayloads.Next(before);
                var a = direct.Apply(command); var b = restored.Apply(command);
                Assert.That(a.accepted, Is.True, a.reason); Assert.That(b.accepted, Is.True, b.reason);
                PersistenceV25TestPayloads.Equivalent(a.state, b.state);
                files.Write(PersistenceV25TestPayloads.As24(b.state));
                byte[] original24 = File.ReadAllBytes(files.Store.SavePath);
                Assert.That(files.Store.TryLoad(out var loaded, out string message), Is.True, message);
                Assert.That(File.ReadAllBytes(files.Store.SavePath), Is.EqualTo(original24));
                PersistenceV25TestPayloads.Equivalent(b.state, loaded);
                restored = new EpisodeEngine(loaded);
            }
            Assert.That(phases, Does.Contain(EpisodePhase.Finished));
            Assert.That(phases, Does.Contain(EpisodePhase.Eviction));
            Assert.That(phases, Does.Contain(EpisodePhase.Campaign));
            files.AssertNoPending();
            // Same current engine, different disk paths: this is not a pinned old/new binary replay.
        }
    }

    internal static class PersistenceV25TestPayloads
    {
        internal static JsonSerializer Serializer() => (JsonSerializer)typeof(EpisodeSaveStore).Assembly
            .GetType("Gamesim.Persistence.SaveJson", true).GetMethod("Serializer", BindingFlags.Public | BindingFlags.Static).Invoke(null, null);
        internal static JObject Payload(EpisodeState s) => JObject.FromObject(s, Serializer());
        internal static EpisodeState Fresh(bool economy = true)
        {
            var state = EconomyRulesTests.Fresh(enable: economy);
            state.strategyRulesStartWeek = 1; state.agencyRulesStartWeek = 1;
            EpisodeEngine.EnableRead(state); EpisodeEngine.EnableCommitments(state);
            Off(state); return state;
        }
        internal static void Off(EpisodeState state)
        {
            Assert.That(state.schemaVersion, Is.EqualTo(25));
            Assert.That(state.unifiedCommitmentRulesVersion, Is.Zero); Assert.That(state.unifiedCommitments, Is.Not.Null.And.Empty);
            Assert.That(state.unifiedHearingRulesVersion, Is.Zero);
            Assert.That(state.unifiedHearingEvidence, Is.Not.Null.And.Empty); Assert.That(state.unifiedHearingReceipts, Is.Not.Null.And.Empty);
            EpisodeSaveValidation.Validate(state);
        }
        internal static JObject As24(EpisodeState state)
        {
            Off(state);
            var old = PersistenceMigrationTests.StripSchema25(Payload(state)); old["schemaVersion"] = 24;
            return old;
        }
        internal static void Equivalent(EpisodeState expected, EpisodeState actual) =>
            Assert.That(JToken.DeepEquals(Payload(expected), Payload(actual)), Is.True, "All rules, history, RNG streams, counters, IDs and command receipts survive exactly.");
        internal static void OnlyHearingDefaults(JObject old, JObject current)
        {
            Assert.That((int)old["schemaVersion"], Is.EqualTo(24)); Assert.That((int)current["schemaVersion"], Is.EqualTo(25));
            Assert.That(current.Properties().Select(p => p.Name).Except(old.Properties().Select(p => p.Name)), Is.EquivalentTo(
                new[] { "unifiedHearingRulesVersion", "unifiedHearingEvidence", "unifiedHearingReceipts" }));
            Assert.That(current["unifiedHearingRulesVersion"].Type, Is.EqualTo(JTokenType.Integer));
            Assert.That((int)current["unifiedHearingRulesVersion"], Is.Zero);
            Assert.That(current["unifiedHearingEvidence"], Is.TypeOf<JArray>()); Assert.That((JArray)current["unifiedHearingEvidence"], Is.Empty);
            Assert.That(current["unifiedHearingReceipts"], Is.TypeOf<JArray>()); Assert.That((JArray)current["unifiedHearingReceipts"], Is.Empty);
            var projection = PersistenceMigrationTests.StripSchema25((JObject)current.DeepClone()); projection["schemaVersion"] = 24;
            Assert.That(JToken.DeepEquals(projection, old), Is.True, "Migration does not infer facts/audiences, apply hearing effects, convert agreements, draw RNG, renumber IDs or settle history.");
        }
        internal static EpisodeState Apply(EpisodeState state, EpisodeCommandKind kind, string target = null, string text = null, string second = null)
        {
            var result = new EpisodeEngine(state).Apply(new EpisodeCommand { id = "v25-" + state.revision + "-" + kind,
                actorId = state.playerId, expectedRevision = state.revision, expectedPhase = state.phase,
                kind = kind, targetId = target, text = text, secondTargetId = second });
            Assert.That(result.accepted, Is.True, result.reason); Off(result.state); return result.state;
        }
        internal static EpisodeCommand Next(EpisodeState state)
        {
            var command = EpisodeEngineTests.NextCommand(state);
            if (command.kind == EpisodeCommandKind.AnswerJury && EpisodeEngine.FinaleOn(state))
            {
                var exchange = state.juryExchanges[state.juryQuestionIndex];
                if (exchange.finalistId == state.playerId)
                    command.secondTargetId = FinaleQuestions.Offered(exchange.category, exchange.receiptKind)[0];
            }
            return command;
        }
        internal static EpisodeState Checkpoint(string checkpoint)
        {
            var state = Fresh(checkpoint != "legacy");
            if (checkpoint == "legacy" || checkpoint == "opening") return state;
            if (checkpoint == "debit")
            {
                state = Apply(state, EpisodeCommandKind.BuyActionPoint, text: WebSocialVocabulary.SpreadAll);
                state = Apply(state, EpisodeCommandKind.BuyActionPoint, text: WebSocialVocabulary.SpreadAll);
                for (int i = 0; i < 3; i++) state = Apply(state, EpisodeCommandKind.Talk, state.Active.First(c => !c.isPlayer).id);
                state = Apply(state, EpisodeCommandKind.Advance);
                Assert.That(state.moveInExtrasSpent, Is.EqualTo(1)); return state;
            }
            if (checkpoint.EndsWith("pitch", StringComparison.Ordinal))
            {
                state.phase = EpisodePhase.Nomination; state.hohId = state.playerId; state.nominees.Clear();
                NpcSocialActions.Court(state, state.Active.First(c => !c.isPlayer), state.Find(state.playerId));
                Assert.That(state.replyCards, Has.Count.EqualTo(1));
                string card = state.replyCards[0].id;
                if (checkpoint != "pending-pitch") state = Apply(state, EpisodeCommandKind.ReplyToHouseguest, card, "feel-out");
                if (checkpoint == "answered-pitch") state = Apply(state, EpisodeCommandKind.ReplyToHouseguest, card, "promise-safety");
                Off(state); return state;
            }
            if (checkpoint == "history")
            {
                string npc = state.Active.First(c => !c.isPlayer).id;
                state.promises.Add(new PromiseState { id = "old-promise", fromId = npc, toId = state.playerId,
                    kind = PromiseKind.Safety, status = PromiseStatus.Broken, week = 1, expiresWeek = 2, brokenById = npc, settledWeek = 1 });
                state.deals.Add(new DealState { id = "old-deal", type = DealKind.SafetyAgreement, proposerId = state.playerId,
                    recipientId = npc, status = DealStatus.Broken, trustImpact = DealTrust.High, week = 1, expiresWeek = 2,
                    brokenById = npc, settledWeek = 1, linkedDealId = "deal-price-old" });
                state.deals.Add(new DealState { id = "deal-price-old", type = DealKind.InformationSharing, proposerId = npc,
                    recipientId = state.playerId, status = DealStatus.Active, trustImpact = DealTrust.Low, week = 1,
                    expiresWeek = 2, linkedDealId = "old-deal" });
                state.alliances.Add(new AllianceState { id = "old-pact", name = "Existing pact", active = true,
                    playerJoined = true, members = new List<string> { npc, state.playerId } });
                state.story.facts.Add(new HouseFactState { id = "old-audible-word", kind = FactKinds.BrokenWord,
                    actorId = npc, subjectId = state.playerId, refId = "old-promise", week = 1,
                    visibility = FactVisibility.Public, knowers = state.contestants.Select(c => c.id).ToList() });
                Off(state); return state;
            }
            Assert.That(new[] { "emotional", "strategic", "deal", "pressure", "quiet" }, Does.Contain(checkpoint));
            var ids = state.Active.Where(c => !c.isPlayer).Select(c => c.id).ToArray();
            state.phase = EpisodePhase.Eviction; state.evictionStage = EvictionStage.Speeches;
            state.hohId = ids[1]; state.vetoHolderId = state.hohId; state.vetoResolved = true;
            state.vetoPlayers = state.Active.Take(EpisodeEngine.VetoPlayerCount(state.Active.Count())).Select(c => c.id).ToList();
            state.nominees = new List<string> { state.playerId, ids[0] };
            return Apply(state, EpisodeCommandKind.SubmitEvictionSpeech, text: checkpoint == "quiet" ? "" :
                new string('x', 1980) + "\n<Exact & public>", second: checkpoint);
        }
        internal static void CorruptHearing(JObject payload, string defect)
        {
            switch (defect)
            {
                case "missing-version": payload.Remove("unifiedHearingRulesVersion"); break;
                case "missing-evidence": payload.Remove("unifiedHearingEvidence"); break;
                case "missing-receipts": payload.Remove("unifiedHearingReceipts"); break;
                case "null-version": payload["unifiedHearingRulesVersion"] = JValue.CreateNull(); break;
                case "null-evidence": payload["unifiedHearingEvidence"] = JValue.CreateNull(); break;
                case "null-receipts": payload["unifiedHearingReceipts"] = JValue.CreateNull(); break;
                case "text-version": payload["unifiedHearingRulesVersion"] = "0"; break;
                case "fraction-version": payload["unifiedHearingRulesVersion"] = 0.0; break;
                case "negative-version": payload["unifiedHearingRulesVersion"] = -1; break;
                case "enabled-one": payload["unifiedHearingRulesVersion"] = 1; break;
                case "future-version": payload["unifiedHearingRulesVersion"] = 2; break;
                case "object-evidence": payload["unifiedHearingEvidence"] = new JObject(); break;
                case "object-receipts": payload["unifiedHearingReceipts"] = new JObject(); break;
                case "null-evidence-row": ((JArray)payload["unifiedHearingEvidence"]).Add(JValue.CreateNull()); break;
                case "null-receipt-row": ((JArray)payload["unifiedHearingReceipts"]).Add(JValue.CreateNull()); break;
                case "nonempty-evidence": ((JArray)payload["unifiedHearingEvidence"]).Add(JObject.FromObject(new UnifiedHearingEvidenceState(), Serializer())); break;
                case "nonempty-receipt": ((JArray)payload["unifiedHearingReceipts"]).Add(JObject.FromObject(new UnifiedHearingReceiptState(), Serializer())); break;
                case "extra-root": payload["futureHearingFlag"] = false; break;
                case "extra-evidence-field": var row = JObject.FromObject(new UnifiedHearingEvidenceState(), Serializer()); row["future"] = 1;
                    ((JArray)payload["unifiedHearingEvidence"]).Add(row); break;
                case "unified-one": payload["unifiedCommitmentRulesVersion"] = 1; break;
                default: Assert.Fail("Unknown defect"); break;
            }
        }
        internal sealed class Files : IDisposable
        {
            internal readonly string Root = Path.Combine(Path.GetTempPath(), "GamesimV25-" + Guid.NewGuid().ToString("N"));
            internal readonly EpisodeSaveStore Store;
            internal Files() { Directory.CreateDirectory(Root); Store = new EpisodeSaveStore(Path.Combine(Root, "episode.json")); }
            internal void Write(JObject payload) => File.WriteAllText(Store.SavePath, PersistenceMigrationTests.Envelope(payload));
            internal void AssertNoPending() => Assert.That(Directory.GetFiles(Root, "*.pending-*"), Is.Empty);
            public void Dispose()
            {
                string path = Path.GetFullPath(Root);
                Assert.That(Path.GetDirectoryName(path), Is.EqualTo(Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar)));
                Assert.That(Path.GetFileName(path), Does.StartWith("GamesimV25-"));
                if (Directory.Exists(path)) Directory.Delete(path, true);
            }
        }
    }
}
