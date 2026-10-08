using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Gamesim.Persistence;
using Gamesim.Simulation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Actual retained26 JSON to inert27 migration and real test-owned SaveStore controls.
    /// The original managed corpus is not a shipping/user save. Memory, authored disk cases,
    /// their actual native execution and ordinary Unity/Director acceptance remain separate.
    /// </summary>
    public sealed class PersistenceV27MigrationTests
    {
        private const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly Type JsonApi = typeof(EpisodeSaveStore).Assembly.GetType("Gamesim.Persistence.SaveJson", true);
        private static readonly Type Frozen26 = JsonApi.Assembly.GetType("Gamesim.Persistence.FrozenEpisodeV26", true);
        private const string ManifestHash = "9fe276f0a3cf761ad18b2b4c08444097589b2673283b63788eabe5724150e993";
        private const string BindingHash = "7efd225a127350503ff3028bf878e3d38fd469907ff47504d1137f2e31e9a757";

        [TestCase(0, 73)] [TestCase(1, 80)] [TestCase(2, 83)]
        public void EveryGenuineAccepted26AliasGetsExactlyTwoZerosAndNoOtherHistory(int mode, int count)
        {
            PinCorpus(); int found = 0;
            foreach (var alias in FrozenV26TestCorpus.Aliases.Where(a => AcceptedAlias(a) && (int)a["mode"] == mode))
            {
                var old = FrozenV26TestCorpus.Payload(alias); Fixed(old); string before = Text(old);
                var next = EpisodeSaveMigrations.UpgradeV26ToV27(old); Exact(old, next);
                var dispatch = EpisodeSaveMigrations.PrepareCurrentPayload(old, out bool migrated);
                Assert.That(migrated, Is.True); Assert.That(JToken.DeepEquals(dispatch, next), Is.True);
                Assert.That(Text(old), Is.EqualTo(before)); found++;
            }
            Assert.That(found, Is.EqualTo(count)); FrozenV26TestCorpus.AssertPhysicalUnchanged();
        }

        [TestCase("unknown-authority")] [TestCase("unknown-hearing")] [TestCase("commitments-off")] [TestCase("story-off")]
        [TestCase("bad-week")] [TestCase("bad-stat")] [TestCase("future-id")] [TestCase("future-settlement")]
        [TestCase("unknown-kind")] [TestCase("missing-initial")] [TestCase("missing-evidence")] [TestCase("original-party-absent")]
        [TestCase("null-vote-archive")] [TestCase("nonempty-vote-archive")] [TestCase("nonnull-target")] [TestCase("nonnull-subtype")]
        public void SourceLinkedSemanticAndInertOverlayRefusalsDoNotBecomeAccepted(string label)
        {
            var alias = Alias("semantic-refused", label); var baseline = Baseline(alias); Fixed(baseline);
            Exact(baseline, EpisodeSaveMigrations.UpgradeV26ToV27(baseline));
            var old = FrozenV26TestCorpus.Payload(alias); Assert.That(old["schemaVersion"]?.Type, Is.EqualTo(JTokenType.Integer));
            Assert.That((int)old["schemaVersion"], Is.EqualTo(26));
            // bad-week is a documented compound prerequisite defect; archive/extension
            // cases are fixed26 overlay refusals, not invented source semantic isolates.
            RefusedUpgrade(old); FrozenV26TestCorpus.AssertPhysicalUnchanged();
        }

        [TestCase("missing-header")] [TestCase("null-header")] [TestCase("text-header")] [TestCase("fraction-header")]
        [TestCase("old-header")] [TestCase("future-header")] [TestCase("unknown-root")] [TestCase("missing-archive")]
        [TestCase("object-archive")] [TestCase("missing-target")] [TestCase("missing-subtype")] [TestCase("unknown-row")]
        public void CapturedLiteral26ShapeRefusalsPrecedeAllNeutralAdditions(string label)
        {
            var alias = Alias("shape-refused", label); Fixed(Baseline(alias));
            RefusedUpgrade(FrozenV26TestCorpus.Payload(alias)); FrozenV26TestCorpus.AssertPhysicalUnchanged();
        }

        [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)] [TestCase(6)]
        [TestCase(7)] [TestCase(8)] [TestCase(9)] [TestCase(10)] [TestCase(11)] [TestCase(12)]
        [TestCase(13)] [TestCase(14)] [TestCase(15)] [TestCase(16)] [TestCase(17)] [TestCase(18)]
        [TestCase(19)] [TestCase(20)] [TestCase(21)] [TestCase(22)] [TestCase(23)] [TestCase(24)] [TestCase(25)] [TestCase(26)]
        public void EveryPriorDispatcherKeepsItsExactFormer26ResultBeforeTheSingleStep(int version)
        {
            // Synthetic v1 ancestry is distinct from genuine actual26 captured command history.
            var v1 = PersistenceMigrationTests.V1Fixture();
            var old = version == 1 ? v1 : (JObject)Invoke(typeof(EpisodeSaveMigrations), "PrepareV" + version + "Payload", v1, false);
            string before = Text(old); var former = EpisodeSaveMigrations.PrepareV26Payload(old, out _); Fixed(former);
            var next = EpisodeSaveMigrations.PrepareCurrentPayload(old, out bool migrated);
            Assert.That(migrated, Is.True); Exact(former, next); Assert.That(Text(old), Is.EqualTo(before));
            Assert.That((int)former["schemaVersion"], Is.EqualTo(26)); Assert.That((int)next["schemaVersion"], Is.EqualTo(27));
            Assert.That((int)next["unifiedCommitmentRulesVersion"], Is.Zero); Assert.That((int)next["unifiedHearingRulesVersion"], Is.Zero);
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void Current27DispatcherDetachesButCannotNormalizeCorruptNullStorage(int mode)
        {
            var old = Recorded(mode); var now = EpisodeSaveMigrations.UpgradeV26ToV27(old); Exact(old, now);
            string before = Text(now); var again = EpisodeSaveMigrations.PrepareCurrentPayload(now, out bool migrated);
            Assert.That(migrated, Is.False); Assert.That(again, Is.Not.SameAs(now)); Assert.That(Text(again), Is.EqualTo(before));
            again["contestants"][0]["name"] = "A detached current caller"; Assert.That(Text(now), Is.EqualTo(before));
            now["unifiedVoteReveals"] = JValue.CreateNull(); before = Text(now);
            again = EpisodeSaveMigrations.PrepareCurrentPayload(now, out migrated); Assert.That(migrated, Is.False);
            Assert.That(again["unifiedVoteReveals"].Type, Is.EqualTo(JTokenType.Null));
            Assert.Throws<InvalidDataException>(() => Current(again)); Assert.That(Text(now), Is.EqualTo(before));
            FrozenV26TestCorpus.AssertPhysicalUnchanged();
        }

        [TestCase("null-input")] [TestCase("missing-header")] [TestCase("null-header")] [TestCase("text-header")]
        [TestCase("fraction-header")] [TestCase("boolean-header")] [TestCase("old-header")] [TestCase("current-header")]
        [TestCase("future-header")] [TestCase("future-binding-zero")] [TestCase("future-first-zero")]
        [TestCase("future-binding-null")] [TestCase("future-first-null")] [TestCase("unknown-root")]
        [TestCase("unknown-nested")] [TestCase("missing-archive")] [TestCase("missing-target")]
        public void UpgradeCannotDefaultAnIncompleteOrFutureFormer26Tree(string defect)
        {
            var old = Recorded(2);
            if (defect == "null-input") old = null;
            else if (defect == "missing-header") old.Remove("schemaVersion");
            else if (defect == "null-header") old["schemaVersion"] = JValue.CreateNull();
            else if (defect == "text-header") old["schemaVersion"] = "26";
            else if (defect == "fraction-header") old["schemaVersion"] = 26.0;
            else if (defect == "boolean-header") old["schemaVersion"] = true;
            else if (defect == "old-header") old["schemaVersion"] = 25;
            else if (defect == "current-header") old["schemaVersion"] = 27;
            else if (defect == "future-header") old["schemaVersion"] = 28;
            else if (defect.StartsWith("future-binding", StringComparison.Ordinal)) old["unifiedCommitments"][0]["voteBindingWeek"] = defect.EndsWith("null", StringComparison.Ordinal) ? JValue.CreateNull() : new JValue(0);
            else if (defect.StartsWith("future-first", StringComparison.Ordinal)) old["unifiedCommitments"][0]["voteFirstRevealWeek"] = defect.EndsWith("null", StringComparison.Ordinal) ? JValue.CreateNull() : new JValue(0);
            else if (defect == "unknown-root") old["unexpectedAuthority"] = false;
            else if (defect == "unknown-nested") old["story"]["futureAudience"] = new JArray();
            else if (defect == "missing-archive") old.Remove("unifiedVoteReveals");
            else ((JObject)old["unifiedCommitments"][0]).Remove("targetId");
            RefusedUpgrade(old); FrozenV26TestCorpus.AssertPhysicalUnchanged();
        }

        [TestCase("voteBindingWeek", "missing")] [TestCase("voteBindingWeek", "null")] [TestCase("voteBindingWeek", "text")]
        [TestCase("voteBindingWeek", "fraction")] [TestCase("voteBindingWeek", "boolean")] [TestCase("voteBindingWeek", "nonzero")]
        [TestCase("voteFirstRevealWeek", "missing")] [TestCase("voteFirstRevealWeek", "null")] [TestCase("voteFirstRevealWeek", "text")]
        [TestCase("voteFirstRevealWeek", "fraction")] [TestCase("voteFirstRevealWeek", "boolean")] [TestCase("voteFirstRevealWeek", "nonzero")]
        [TestCase("root", "future-header")] [TestCase("root", "unknown-root")] [TestCase("root", "unknown-nested")]
        [TestCase("root", "null-list")] [TestCase("root", "null-archive")] [TestCase("root", "nonempty-archive")]
        [TestCase("root", "object-archive")] [TestCase("root", "unrelated-core")]
        public void CurrentShapeAndZeroMarkerGuardsRejectWithoutARepairedCandidate(string field, string defect)
        {
            var now = EpisodeSaveMigrations.UpgradeV26ToV27(Recorded(2)); Current(now);
            if (field != "root")
            {
                var row = (JObject)now["unifiedCommitments"][0];
                if (defect == "missing") row.Remove(field);
                else row[field] = defect == "null" ? JValue.CreateNull() : defect == "text" ? new JValue("0")
                    : defect == "fraction" ? new JValue(0.0) : defect == "boolean" ? new JValue(false) : new JValue(1);
            }
            else if (defect == "future-header") now["schemaVersion"] = 28;
            else if (defect == "unknown-root") now["unrecordedAuthority"] = JValue.CreateNull();
            else if (defect == "unknown-nested") now["story"]["unrecordedWitnesses"] = new JArray();
            else if (defect == "null-list") now["unifiedCommitments"] = JValue.CreateNull();
            else if (defect == "null-archive") now["unifiedVoteReveals"] = JValue.CreateNull();
            else if (defect == "nonempty-archive") ((JArray)now["unifiedVoteReveals"]).Add(JValue.CreateNull());
            else if (defect == "object-archive") now["unifiedVoteReveals"] = new JObject();
            else now["revision"] = -1;
            string before = Text(now); Assert.Throws<InvalidDataException>(() => Current(now));
            Exception refusal = null;
            try { PersistenceMigrationTests.StripSchema27(now); }
            catch (Exception error) { refusal = error is TargetInvocationException invocation ? invocation.InnerException ?? invocation : error; }
            Assert.That(refusal is AssertionException || refusal is InvalidDataException, Is.True);
            Assert.That(Text(now), Is.EqualTo(before)); FrozenV26TestCorpus.AssertPhysicalUnchanged();
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void SharedFieldObserverMatchesTheActualNativeSerializerNotTheLegacyTrace(int mode)
        {
            var state = (EpisodeState)Invoke(typeof(FrozenEpisodeV25ContractTests), "Witness", mode, "promise");
            var rawTrace = JObject.FromObject(state); string traceBefore = Text(rawTrace), stateBefore = Text(Payload(state));
            Assert.That(rawTrace["Active"], Is.TypeOf<JArray>());
            var observer = LegacyDigestSchema27Observer.CheckFieldObserver(state, rawTrace);
            var saved = Payload(state); Current(saved);
            Assert.That(JToken.DeepEquals(observer, saved), Is.True, "The shared observer is checked against the REAL public-fields serializer.");
            Assert.That(observer.Property("Active"), Is.Null);
            foreach (JObject row in (JArray)observer["houseEvents"]) Assert.That(row.Property("IsStory"), Is.Null);
            // Structural reconstruction is checked by the real native migration owner,
            // not presented as a migration invocation by the pure observer branch.
            var former = (JObject)saved.DeepClone(); PersistenceMigrationTests.StripSchema27(former); Fixed(former);
            Assert.That(JToken.DeepEquals(EpisodeSaveMigrations.UpgradeV26ToV27(former), observer), Is.True);
            Assert.That(Text(rawTrace), Is.EqualTo(traceBefore)); Assert.That(Text(Payload(state)), Is.EqualTo(stateBefore));
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void Original26PrimaryAndBackupLoadReadOnlyBeforeAnExplicit27Save(int mode)
        {
            PinCorpus(); using var slot = new Slot(); ImportPair(slot, mode); var image = slot.Image();
            var original = EnvelopeState(image["episode.json"]); Fixed(original);
            var expected = EpisodeSaveMigrations.UpgradeV26ToV27(original); Exact(original, expected);
            Assert.That(slot.Store.TryLoad(out var loaded, out string message), Is.True, message);
            Assert.That(message, Does.Contain("Schema 26").And.Contain("schema 27 in memory"));
            Assert.That(JToken.DeepEquals(Payload(loaded), expected), Is.True); slot.Unchanged(image);
            slot.Store.Save(loaded); Assert.That(slot.Read(slot.Store.BackupPath).SequenceEqual(image["episode.json"]), Is.True);
            var saved = slot.Image(); Assert.That((int)EnvelopeState(saved["episode.json"])["schemaVersion"], Is.EqualTo(27));
            Assert.That(slot.Store.TryLoad(out var resumed, out message), Is.True, message); slot.Unchanged(saved);
            Assert.That(JToken.DeepEquals(Payload(resumed), expected), Is.True);
            slot.Store.Save(PlayOne(resumed)); Assert.That(slot.Read(slot.Store.BackupPath).SequenceEqual(saved["episode.json"]), Is.True);
            FrozenV26TestCorpus.AssertPhysicalUnchanged();
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void OriginalBadChecksumWinsBeforeHistoricalShapeOrChronologyMigration(int mode)
        {
            using var slot = new Slot(); ImportPair(slot, mode);
            slot.Replace(slot.Store.SavePath, Envelope(mode, "checksumCorruptSha256")); var image = slot.Image();
            for (int i = 0; i < 2; i++)
            {
                Assert.That(slot.Store.TryLoad(out var absent, out string message), Is.False); Assert.That(absent, Is.Null);
                Assert.That(message, Does.Contain("checksum").And.Contain("backup")); slot.Unchanged(image);
            }
            FrozenV26TestCorpus.AssertPhysicalUnchanged();
        }

        [TestCase(1, "voteBindingWeek")] [TestCase(2, "voteBindingWeek")]
        [TestCase(1, "voteFirstRevealWeek")] [TestCase(2, "voteFirstRevealWeek")]
        public void AChecksummed26FutureZeroCannotBeMistakenForAnInert27Addition(int mode, string field)
        {
            using var slot = new Slot(); ImportPair(slot, mode); var old = Recorded(mode); Fixed(old);
            old["unifiedCommitments"][0][field] = 0; RefusedUpgrade(old); string before = Text(old);
            slot.Replace(slot.Store.SavePath, Encoding.UTF8.GetBytes(PersistenceMigrationTests.Envelope(old))); var image = slot.Image();
            Assert.That(slot.Store.TryLoad(out var absent, out string message), Is.False); Assert.That(absent, Is.Null);
            Assert.That(message, Does.Not.Contain("checksum")); slot.Unchanged(image); Assert.That(Text(old), Is.EqualTo(before));
            FrozenV26TestCorpus.AssertPhysicalUnchanged();
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void OriginalChecksumValidSemanticDamageCannotPublishA27Candidate(int mode)
        {
            using var slot = new Slot(); ImportPair(slot, mode);
            slot.Replace(slot.Store.SavePath, Envelope(mode, "semanticCorruptSha256")); var image = slot.Image();
            Assert.That(slot.Store.TryLoad(out var absent, out string message), Is.False); Assert.That(absent, Is.Null);
            Assert.That(message, Does.Not.Contain("checksum")); slot.Unchanged(image); FrozenV26TestCorpus.AssertPhysicalUnchanged();
        }

        [TestCase(1, "voteBindingWeek", -1)] [TestCase(1, "voteBindingWeek", 1)]
        [TestCase(2, "voteBindingWeek", -1)] [TestCase(2, "voteBindingWeek", 1)]
        [TestCase(1, "voteFirstRevealWeek", -1)] [TestCase(1, "voteFirstRevealWeek", 1)]
        [TestCase(2, "voteFirstRevealWeek", -1)] [TestCase(2, "voteFirstRevealWeek", 1)]
        public void SaveRefusalKeepsActual26PrimaryBackupBytesAndValidContinuation(int mode, string field, int value)
        {
            using var slot = new Slot(); ImportPair(slot, mode); var old = Recorded(mode); Fixed(old);
            var valid = EpisodeSaveMigrations.UpgradeV26ToV27(old).ToObject<EpisodeState>(Serializer()); Current(Payload(valid));
            var candidate = valid.Clone(); typeof(UnifiedCommitmentState).GetField(field).SetValue(candidate.unifiedCommitments[0], value);
            string candidateBefore = Text(Payload(candidate)); var image = slot.Image();
            for (int i = 0; i < 2; i++)
            { Assert.Throws<InvalidDataException>(() => slot.Store.Save(candidate)); slot.Unchanged(image); Assert.That(Text(Payload(candidate)), Is.EqualTo(candidateBefore)); }
            Assert.That(slot.Store.TryLoad(out var loaded, out string message), Is.True, message); slot.Unchanged(image);
            var next = PlayOne(loaded); slot.Store.Save(next);
            Assert.That(slot.Read(slot.Store.BackupPath).SequenceEqual(image["episode.json"]), Is.True);
            Assert.That(slot.Store.TryLoad(out var resumed, out message), Is.True, message);
            Assert.That(JToken.DeepEquals(Payload(resumed), Payload(next)), Is.True); FrozenV26TestCorpus.AssertPhysicalUnchanged();
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void OnlyExplicitRecoveryInstallsOriginalBackupAndRetainsTheCorruptPrimary(int mode)
        {
            using var slot = new Slot(); ImportPair(slot, mode);
            slot.Replace(slot.Store.SavePath, Envelope(mode, "checksumCorruptSha256")); var image = slot.Image();
            Assert.That(slot.Store.TryLoad(out var absent, out _), Is.False); Assert.That(absent, Is.Null); slot.Unchanged(image);
            Assert.That(slot.Store.TryRecoverBackup(out var loaded, out string message), Is.True, message);
            Assert.That(slot.Read(slot.Store.SavePath).SequenceEqual(image["episode.json.backup"]), Is.True);
            Assert.That(slot.Read(slot.Store.BackupPath).SequenceEqual(image["episode.json.backup"]), Is.True);
            var expected = EpisodeSaveMigrations.UpgradeV26ToV27(EnvelopeState(image["episode.json.backup"]));
            Assert.That(JToken.DeepEquals(Payload(loaded), expected), Is.True); Current(Payload(loaded));
            string retained = Directory.GetFiles(slot.Root, "episode.json.before-recovery-*.json").Single();
            Assert.That(slot.Read(retained).SequenceEqual(image["episode.json"]), Is.True);
            slot.Store.Save(PlayOne(loaded)); Assert.That(slot.Read(retained).SequenceEqual(image["episode.json"]), Is.True);
            Assert.That(slot.Store.TryLoad(out var resumed, out message), Is.True, message); Current(Payload(resumed));
            FrozenV26TestCorpus.AssertPhysicalUnchanged();
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void AnInvalidBackupNeverReplacesOrArchivesTheExistingPrimary(int mode)
        {
            using var slot = new Slot(); ImportPair(slot, mode);
            slot.Replace(slot.Store.BackupPath, Envelope(mode, "checksumCorruptSha256")); var image = slot.Image();
            Assert.That(slot.Store.TryRecoverBackup(out var absent, out string message), Is.False); Assert.That(absent, Is.Null);
            Assert.That(message, Does.Contain("checksum")); slot.Unchanged(image); FrozenV26TestCorpus.AssertPhysicalUnchanged();
        }

        private static void PinCorpus()
        {
            Assert.That(Hash(FrozenV26TestCorpus.ReadRaw("corpus.json")), Is.EqualTo(ManifestHash));
            Assert.That(Hash(FrozenV26TestCorpus.ReadRaw("binding.json")), Is.EqualTo(BindingHash));
            var m = FrozenV26TestCorpus.Manifest; Assert.That((int)m["literalPayloadSchema"], Is.EqualTo(26));
            Assert.That((bool)m["complete"], Is.True); Assert.That((JArray)m["files"], Has.Count.EqualTo(471));
            Assert.That((JArray)m["aliases"], Has.Count.EqualTo(264));
        }
        private static bool AcceptedAlias(JObject alias) => (string)alias["kind"] == "opening" || (string)alias["kind"] == "checkpoint" || (string)alias["kind"] == "season-boundary";
        private static JObject Alias(string kind, string label)
        { PinCorpus(); return FrozenV26TestCorpus.Aliases.Single(a => (string)a["kind"] == kind && (string)a["label"] == label); }
        private static JObject Baseline(JObject alias)
        {
            string hash = (string)alias["acceptedBaselineSha256"];
            var actual = FrozenV26TestCorpus.Aliases.First(a => AcceptedAlias(a) && (string)a["payloadSha256"] == hash);
            return FrozenV26TestCorpus.Payload(actual);
        }
        private static JObject Recorded(int mode)
        {
            PinCorpus(); var alias = FrozenV26TestCorpus.Aliases.Single(a => (string)a["kind"] == "checkpoint"
                && (int)a["mode"] == mode && (string)a["label"] == "promise");
            var payload = FrozenV26TestCorpus.Payload(alias); Fixed(payload);
            if (mode != 0) Assert.That((JArray)payload["unifiedCommitments"], Is.Not.Empty); return payload;
        }
        private static byte[] Envelope(int mode, string field)
        {
            PinCorpus(); var disk = ((JArray)FrozenV26TestCorpus.Manifest["diskExercises"]).OfType<JObject>().Single(d => (int)d["mode"] == mode);
            return FrozenV26TestCorpus.ReadRaw("envelopes/" + (string)disk[field] + ".json");
        }
        private static void ImportPair(Slot slot, int mode)
        {
            var primary = Envelope(mode, "primarySha256"); var backup = Envelope(mode, "backupSha256");
            Assert.That(primary.SequenceEqual(backup), Is.False, "These are the two distinct actual source SaveStore writes, not copies of one synthesized envelope.");
            Fixed(EnvelopeState(primary)); Fixed(EnvelopeState(backup)); slot.Replace(slot.Store.SavePath, primary); slot.Replace(slot.Store.BackupPath, backup);
        }
        private static JObject EnvelopeState(byte[] bytes) => (JObject)Parse(bytes)["state"].DeepClone();
        private static JObject Parse(byte[] bytes)
        {
            Assert.That(bytes, Is.Not.Null); Assert.That(bytes.Length, Is.LessThanOrEqualTo(8 * 1024 * 1024));
            using var text = new StringReader(new UTF8Encoding(false, true).GetString(bytes));
            using var reader = new JsonTextReader(text) { DateParseHandling = DateParseHandling.None, MaxDepth = 64 };
            var token = JToken.ReadFrom(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
            Assert.That(token, Is.TypeOf<JObject>()); Assert.That(reader.Read(), Is.False); return (JObject)token;
        }
        private static void Exact(JObject old, JObject next)
        {
            string oldBefore = Text(old), nextBefore = Text(next); Fixed(old); Current(next);
            Assert.That((int)old["schemaVersion"], Is.EqualTo(26)); Assert.That((int)next["schemaVersion"], Is.EqualTo(27));
            var reverse = (JObject)next.DeepClone(); reverse["schemaVersion"] = 26;
            foreach (JObject row in (JArray)reverse["unifiedCommitments"])
            {
                foreach (string field in new[] { "voteBindingWeek", "voteFirstRevealWeek" })
                { Assert.That(row.Property(field), Is.Not.Null); Assert.That(row[field].Type, Is.EqualTo(JTokenType.Integer)); Assert.That((int)row[field], Is.Zero); row.Remove(field); }
            }
            Assert.That(JToken.DeepEquals(reverse, old), Is.True, "Every original ID/RNG/rule/term/incident/audience/receipt/price and list order survives.");
            var guarded = (JObject)next.DeepClone(); PersistenceMigrationTests.StripSchema27(guarded);
            Assert.That(JToken.DeepEquals(guarded, old), Is.True); Assert.That(next, Is.Not.SameAs(old));
            Assert.That(Text(old), Is.EqualTo(oldBefore)); Assert.That(Text(next), Is.EqualTo(nextBefore));
        }
        private static void RefusedUpgrade(JObject old)
        {
            string before = old == null ? "null" : Text(old);
            Assert.Throws<InvalidDataException>(() => EpisodeSaveMigrations.UpgradeV26ToV27(old));
            Assert.Throws<InvalidDataException>(() => Fixed(old)); Assert.That(old == null ? "null" : Text(old), Is.EqualTo(before));
        }
        private static void Fixed(JObject payload) => Invoke(Frozen26, "Validate", payload);
        private static void Current(JObject payload)
        {
            string before = Text(payload); Invoke(JsonApi, "CheckDtoShape", payload, typeof(EpisodeState), "state");
            var state = payload.ToObject<EpisodeState>(Serializer()); EpisodeSaveValidation.Validate(state);
            Assert.That(state.schemaVersion, Is.EqualTo(27)); Assert.That(Text(payload), Is.EqualTo(before));
        }
        private static EpisodeState PlayOne(EpisodeState s)
        {
            var next = (EpisodeCommand)Invoke(typeof(FrozenEpisodeV25ContractTests), "Next", s, true);
            var engine = new EpisodeEngine(s); string before = Text(Payload(s)); var result = engine.Apply(next);
            Assert.That(result.accepted, Is.True, result.reason); Assert.That(result.duplicate, Is.False);
            Assert.That(result.state.revision, Is.EqualTo(s.revision + 1)); Assert.That(Text(Payload(s)), Is.EqualTo(before));
            Current(Payload(result.state)); return result.state;
        }
        private static object Invoke(Type type, string name, params object[] arguments)
        { try { return type.GetMethod(name, Static).Invoke(null, arguments); } catch (TargetInvocationException error) { throw error.InnerException ?? error; } }
        private static JsonSerializer Serializer() => (JsonSerializer)Invoke(JsonApi, "Serializer");
        private static JObject Payload(EpisodeState s) => JObject.FromObject(s, Serializer());
        private static string Text(JToken token) => token.ToString(Formatting.None);
        private static string Hash(byte[] bytes) { using var hash = SHA256.Create(); return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }

        private sealed class Slot : IDisposable
        {
            private readonly string id = Guid.NewGuid().ToString("N");
            internal readonly string Root;
            internal readonly EpisodeSaveStore Store;
            internal Slot()
            {
                Root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "GamesimV27-" + id)); NoReparse(Root);
                Assert.That(Directory.Exists(Root) || File.Exists(Root), Is.False); Directory.CreateDirectory(Root); NoReparse(Root);
                Store = new EpisodeSaveStore(Path.Combine(Root, "episode.json"));
            }
            internal void Replace(string path, byte[] bytes)
            {
                Assert.That(path == Store.SavePath || path == Store.BackupPath, Is.True); NoReparse(path);
                Assert.That(bytes.Length, Is.LessThanOrEqualTo(8 * 1024 * 1024)); File.WriteAllBytes(path, bytes);
                Assert.That(Read(path).SequenceEqual(bytes), Is.True);
            }
            internal byte[] Read(string path)
            {
                Assert.That(Path.GetDirectoryName(Path.GetFullPath(path)), Is.EqualTo(Root)); NoReparse(path);
                using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                long length = file.Length; Assert.That(length, Is.InRange(0L, 8L * 1024 * 1024)); var bytes = new byte[(int)length];
                int offset = 0; while (offset < bytes.Length) { int got = file.Read(bytes, offset, bytes.Length - offset); Assert.That(got, Is.GreaterThan(0)); offset += got; }
                Assert.That(file.ReadByte(), Is.EqualTo(-1)); Assert.That(file.Length, Is.EqualTo(length)); return bytes;
            }
            internal Dictionary<string, byte[]> Image()
            {
                NoReparse(Root); Assert.That(Directory.GetDirectories(Root), Is.Empty); var result = new Dictionary<string, byte[]>(StringComparer.Ordinal);
                var files = Directory.GetFiles(Root); Assert.That(files.Length, Is.LessThanOrEqualTo(16));
                foreach (string path in files) result.Add(Path.GetFileName(path), Read(path));
                Assert.That(files.Where(p => Path.GetFileName(p).Contains(".pending-")), Is.Empty); return result;
            }
            internal void Unchanged(Dictionary<string, byte[]> before)
            { var after = Image(); Assert.That(after.Keys, Is.EquivalentTo(before.Keys)); foreach (var file in before) Assert.That(after[file.Key].SequenceEqual(file.Value), Is.True, file.Key); }
            public void Dispose()
            {
                string temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                Assert.That(string.Equals(Path.GetDirectoryName(Root), temp, StringComparison.OrdinalIgnoreCase), Is.True);
                Assert.That(Path.GetFileName(Root), Is.EqualTo("GamesimV27-" + id)); NoReparse(Root);
                if (!Directory.Exists(Root)) return;
                Assert.That(Directory.GetDirectories(Root), Is.Empty); foreach (string path in Directory.GetFiles(Root)) NoReparse(path);
                Directory.Delete(Root, true);
            }
            private static void NoReparse(string path)
            {
                for (string at = Path.GetFullPath(path); !string.IsNullOrEmpty(at); at = Path.GetDirectoryName(at))
                { if (File.Exists(at) || Directory.Exists(at)) Assert.That((File.GetAttributes(at) & FileAttributes.ReparsePoint) == 0, Is.True); }
            }
        }
    }
}
