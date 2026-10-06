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
    /// Exact inert25-to26 migration and real isolated SaveStore tests. Retained JSON is managed
    /// test-corpus evidence, not historical shipping/user saves. Disk cases require their actual
    /// execution; authoring/compilation and in-memory cases cannot establish native acceptance.
    /// </summary>
    public sealed class PersistenceV26MigrationTests
    {
        private const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly Type JsonApi = typeof(EpisodeSaveStore).Assembly.GetType("Gamesim.Persistence.SaveJson", true);
        private static readonly Type Frozen = JsonApi.Assembly.GetType("Gamesim.Persistence.FrozenEpisodeV25", true);
        private static readonly Type Shape25 = JsonApi.Assembly.GetType("Gamesim.Persistence.FrozenV25Shape", true);
        private const string ManifestHash = "96e1fd4e26d82d3ceac0945df99a56b5396f8492774e98e5b36851ca39f94b38";
        private const string BindingHash = "984401d1cd2081bd2d0c3152f6689969acf332aa02b3a165103427ee40046501";
        private const string IndexHash = "28c7ff14ceb535cad67433c37b72bb526453db943cb754348feabee33dfee8d0";

        [TestCase(0, 75)] [TestCase(1, 118)] [TestCase(2, 173)]
        public void AllRetainedAcceptedAliasesGainOnlyTheExactNeutralAdditions(int mode, int count)
        {
            var package = Package(); int found = 0;
            foreach (JObject alias in Aliases(package).Where(a => (string)a["kind"] == "accepted"))
            {
                var old = Read(package, (string)alias["file"]); if (Mode(old) != mode) continue;
                Invoke(Frozen, "Validate", old); string before = Text(old);
                var next = EpisodeSaveMigrations.UpgradeV25ToV26(old); OnlyNeutral(old, next); Current(next);
                var dispatched = EpisodeSaveMigrations.PrepareCurrentPayload(old, out bool migrated);
                Assert.That(migrated, Is.True); Assert.That(JToken.DeepEquals(dispatched, next), Is.True);
                Assert.That(Text(old), Is.EqualTo(before)); found++;
            }
            Assert.That(found, Is.EqualTo(count)); PackageUnchanged(package);
        }

        [TestCase("ActualAudibleSourceLineageAndOncePerListenerHistoryCannotBeForged", 16)]
        [TestCase("ActualCourtAssessmentAndPitchPromiseKeepTheirReservedFormer25Receipts", 3)]
        [TestCase("ActualPlayerFinalistLocksOnlyResolvableHistoryAndJurorsOwnSourceReceipts", 4)]
        [TestCase("ActualPublicBlockSpeechAndItsTypedBroadcastCannotBeRelabelled", 2)]
        [TestCase("ActualPublicMixedSafetyVoteCounterRetainsBothTrueOwners", 4)]
        [TestCase("ActualSafetyIdentityAttributionStatusAndSourcePolicyStayFrozen", 17)]
        [TestCase("EarlierActualSettlementAndItsKnowledgeBoundaryDoNotBecomeTodaysIncident", 6)]
        [TestCase("Former25MeansItsActualPublicPrerequisitesAndRecordedSupportedModes", 11)]
        [TestCase("PermanentBoundedAuthorityCannotBeWidenedByTheFrozenContract", 3)]
        [TestCase("TypedHistoricalProjectionsStillResolveTheirTrueStoredSource", 4)]
        public void SeventyCapturedSemanticRefusalsCannotBeMigratedIntoAcceptance(string family, int count)
        {
            var package = Package(); var manifest = Manifest(package); int found = 0;
            foreach (JObject alias in Aliases(package).Where(a => (string)a["kind"] == "semantic-refused"
                && (string)manifest["caseInvocations"][(int)a["caseOrdinal"]]["method"] == family))
            {
                string baselineHash = (string)alias["lastAcceptedBaselineSha256"];
                Assert.That(Aliases(package).Any(a => (string)a["kind"] == "accepted" && (int)a["caseOrdinal"] == (int)alias["caseOrdinal"]
                    && (string)a["sha256"] == baselineHash && (int)a["observationOrdinal"] < (int)alias["observationOrdinal"]), Is.True);
                var baseline = Read(package, "payloads/" + baselineHash + ".json"); Invoke(Frozen, "Validate", baseline);
                Current(EpisodeSaveMigrations.UpgradeV25ToV26(baseline));
                var old = Read(package, (string)alias["file"]); string before = Text(old);
                Assert.That((int)old["schemaVersion"], Is.EqualTo(25)); Assert.DoesNotThrow(() => Invoke(Shape25, "Validate", old));
                var failure = Assert.Throws<InvalidDataException>(() => EpisodeSaveMigrations.UpgradeV25ToV26(old));
                Assert.That(failure.Message, Does.Not.Contain("Unsupported episode schema"));
                Assert.Throws<InvalidDataException>(() => EpisodeSaveMigrations.PrepareCurrentPayload(old, out _));
                Assert.That(Text(old), Is.EqualTo(before)); found++;
            }
            Assert.That(found, Is.EqualTo(count)); PackageUnchanged(package);
        }

        [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)] [TestCase(6)]
        [TestCase(7)] [TestCase(8)] [TestCase(9)] [TestCase(10)] [TestCase(11)] [TestCase(12)]
        [TestCase(13)] [TestCase(14)] [TestCase(15)] [TestCase(16)] [TestCase(17)] [TestCase(18)]
        [TestCase(19)] [TestCase(20)] [TestCase(21)] [TestCase(22)] [TestCase(23)] [TestCase(24)] [TestCase(25)]
        public void EveryHistoricalDispatcherKeepsItsFormer25ResultBeforeAddingInertStorage(int version)
        {
            // Synthetic v1 ancestry is explicitly distinct from the retained managed25 corpus.
            var v1 = PersistenceMigrationTests.V1Fixture();
            var old = version == 1 ? v1 : (JObject)typeof(EpisodeSaveMigrations).GetMethod("PrepareV" + version + "Payload", Static)
                .Invoke(null, new object[] { v1, false });
            string before = Text(old); var former25 = EpisodeSaveMigrations.PrepareV25Payload(old, out _);
            Invoke(Frozen, "Validate", former25);
            var next = EpisodeSaveMigrations.PrepareCurrentPayload(old, out bool migrated);
            Assert.That(migrated, Is.True); OnlyNeutral(former25, next); Current(next); Assert.That(Text(old), Is.EqualTo(before));
            Assert.That((int)former25["schemaVersion"], Is.EqualTo(25));
            Assert.That((int)next["unifiedCommitmentRulesVersion"], Is.Zero); Assert.That((int)next["unifiedHearingRulesVersion"], Is.Zero);
        }

        [Test]
        public void Current26DispatchIsDetachedAndNeverRepairsAnInvalidArchive()
        {
            var package = Package(); var current = EpisodeSaveMigrations.UpgradeV25ToV26(Recorded(package, 2)); Current(current);
            string before = Text(current); var again = EpisodeSaveMigrations.PrepareCurrentPayload(current, out bool migrated);
            Assert.That(migrated, Is.False); Assert.That(again, Is.Not.SameAs(current)); Assert.That(Text(again), Is.EqualTo(before));
            again["contestants"][0]["name"] = "Detached caller"; Assert.That(Text(current), Is.EqualTo(before));
            current["unifiedVoteReveals"] = JValue.CreateNull(); before = Text(current);
            again = EpisodeSaveMigrations.PrepareCurrentPayload(current, out migrated); Assert.That(migrated, Is.False);
            Assert.That(again["unifiedVoteReveals"].Type, Is.EqualTo(JTokenType.Null));
            var state = again.ToObject<EpisodeState>(Serializer()); Assert.Throws<InvalidDataException>(() => EpisodeSaveValidation.Validate(state));
            Assert.That(Text(current), Is.EqualTo(before)); PackageUnchanged(package);
        }

        [TestCase("null-input")] [TestCase("missing-header")] [TestCase("null-header")] [TestCase("fraction-header")]
        [TestCase("text-header")] [TestCase("old-header")] [TestCase("current-header")] [TestCase("future-header")]
        [TestCase("future-root-null")] [TestCase("future-root-empty")] [TestCase("future-target-null")]
        [TestCase("future-subtype-null")] [TestCase("unknown-nested")]
        public void UpgradeRefusesWrongOrIncompleteFormerShapeWithoutMutatingIt(string defect)
        {
            var package = Package(); var old = Recorded(package, 2);
            if (defect == "null-input") old = null;
            else if (defect == "missing-header") old.Remove("schemaVersion");
            else if (defect == "null-header") old["schemaVersion"] = JValue.CreateNull();
            else if (defect == "fraction-header") old["schemaVersion"] = 25.0;
            else if (defect == "text-header") old["schemaVersion"] = "25";
            else if (defect == "old-header") old["schemaVersion"] = 24;
            else if (defect == "current-header") old["schemaVersion"] = 26;
            else if (defect == "future-header") old["schemaVersion"] = 27;
            else if (defect == "future-root-null") old["unifiedVoteReveals"] = JValue.CreateNull();
            else if (defect == "future-root-empty") old["unifiedVoteReveals"] = new JArray();
            else if (defect == "future-target-null") old["unifiedCommitments"][0]["targetId"] = JValue.CreateNull();
            else if (defect == "future-subtype-null") old["unifiedCommitments"][0]["subtype"] = JValue.CreateNull();
            else old["story"]["futurePrivateTruth"] = new JArray();
            string before = old == null ? "null" : Text(old);
            Assert.Throws<InvalidDataException>(() => EpisodeSaveMigrations.UpgradeV25ToV26(old));
            Assert.That(old == null ? "null" : Text(old), Is.EqualTo(before)); PackageUnchanged(package);
        }

        [TestCase("missing-archive")] [TestCase("null-archive")] [TestCase("object-archive")] [TestCase("null-frame")]
        [TestCase("complete-frame")] [TestCase("missing-target")] [TestCase("missing-subtype")]
        [TestCase("boolean-target")] [TestCase("known-target")] [TestCase("empty-subtype")]
        [TestCase("vote-subtype")] [TestCase("proposed-mode-two")] [TestCase("future-header")] [TestCase("unrelated-core")]
        public void ChecksummedCurrent26ShapeOrInertSemanticsCannotInstallOrRewriteCorruption(string defect)
        {
            var package = Package(); var old = Recorded(package, 2); var current = EpisodeSaveMigrations.UpgradeV25ToV26(old); Current(current);
            if (defect == "missing-archive") current.Remove("unifiedVoteReveals");
            else if (defect == "null-archive") current["unifiedVoteReveals"] = JValue.CreateNull();
            else if (defect == "object-archive") current["unifiedVoteReveals"] = new JObject();
            else if (defect == "null-frame") ((JArray)current["unifiedVoteReveals"]).Add(JValue.CreateNull());
            else if (defect == "complete-frame") ((JArray)current["unifiedVoteReveals"]).Add(new JObject { ["week"] = 1, ["ballots"] = new JArray() });
            else if (defect == "missing-target") ((JObject)current["unifiedCommitments"][0]).Remove("targetId");
            else if (defect == "missing-subtype") ((JObject)current["unifiedCommitments"][0]).Remove("subtype");
            else if (defect == "boolean-target") current["unifiedCommitments"][0]["targetId"] = true;
            else if (defect == "known-target") current["unifiedCommitments"][0]["targetId"] = current["unifiedCommitments"][0]["beneficiaryId"].DeepClone();
            else if (defect == "empty-subtype") current["unifiedCommitments"][0]["subtype"] = "";
            else if (defect == "vote-subtype") current["unifiedCommitments"][0]["subtype"] = "vote_save";
            else if (defect == "proposed-mode-two") current["unifiedCommitmentRulesVersion"] = 2;
            else if (defect == "future-header") current["schemaVersion"] = 27;
            else current["revision"] = -1;
            string before = Text(current); ProjectionRefused(current); Assert.That(Text(current), Is.EqualTo(before));
            using var files = new Files(); files.Write(files.Store.BackupPath, old); files.Write(files.Store.SavePath, current);
            var image = files.Image();
            for (int attempt = 0; attempt < 2; attempt++)
            {
                Assert.That(files.Store.TryLoad(out var loaded, out string message), Is.False, defect); Assert.That(loaded, Is.Null);
                Assert.That(message, Does.Not.Contain("checksum")); files.Unchanged(image);
            }
            Assert.That(Text(current), Is.EqualTo(before)); PackageUnchanged(package);
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void Retained25LoadsReadOnlyAndOnlyAnExplicitSaveRotatesItsOriginalEnvelope(int mode)
        {
            var package = Package(); var old = Recorded(package, mode); var expected = EpisodeSaveMigrations.UpgradeV25ToV26(old);
            using var files = new Files(); files.Write(files.Store.SavePath, old); var image = files.Image();
            Assert.That(files.Store.TryLoad(out var loaded, out string message), Is.True, message);
            Assert.That(message, Does.Contain("Schema 25").And.Contain("schema 26 in memory"));
            Assert.That(JToken.DeepEquals(Payload(loaded), expected), Is.True); files.Unchanged(image);
            files.Store.Save(loaded); Assert.That(File.ReadAllBytes(files.Store.BackupPath).SequenceEqual(image["episode.json"]), Is.True);
            var saved = files.Image(); Assert.That((int)JObject.Parse(File.ReadAllText(files.Store.SavePath))["state"]["schemaVersion"], Is.EqualTo(26));
            Assert.That(files.Store.TryLoad(out var reloaded, out message), Is.True, message); Assert.That(JToken.DeepEquals(Payload(reloaded), expected), Is.True);
            files.Unchanged(saved); var progressed = PlayOne(reloaded); files.Store.Save(progressed);
            Assert.That(File.ReadAllBytes(files.Store.BackupPath).SequenceEqual(saved["episode.json"]), Is.True); PackageUnchanged(package);
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void OriginalEnvelopeChecksumIsCheckedBeforeAnyFormerShapeUpgrade(int mode)
        {
            var package = Package(); var old = Recorded(package, mode); using var files = new Files(); files.Write(files.Store.BackupPath, old);
            old["unifiedVoteReveals"] = new JArray(); var envelope = JObject.Parse(PersistenceMigrationTests.Envelope(old)); envelope["checksum"] = new string('0', 64);
            File.WriteAllText(files.Store.SavePath, envelope.ToString(Formatting.Indented)); var image = files.Image();
            Assert.That(files.Store.TryLoad(out var loaded, out string message), Is.False); Assert.That(loaded, Is.Null);
            Assert.That(message, Does.Contain("checksum").And.Contain("backup")); files.Unchanged(image); PackageUnchanged(package);
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void ValidChecksumDoesNotLaunderFutureFieldsIntoAFormer25Save(int mode)
        {
            var package = Package(); var old = Recorded(package, mode); using var files = new Files(); files.Write(files.Store.BackupPath, old);
            old["unifiedVoteReveals"] = new JArray(); files.Write(files.Store.SavePath, old); var image = files.Image();
            Assert.That(files.Store.TryLoad(out var loaded, out string message), Is.False); Assert.That(loaded, Is.Null);
            Assert.That(message, Does.Not.Contain("checksum")); files.Unchanged(image); PackageUnchanged(package);
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void ExplicitRecoveryRetainsOldBackupAndDamagedPrimaryThenCanPlayAndSave(int mode)
        {
            var package = Package(); var old = Recorded(package, mode); using var files = new Files(); files.Write(files.Store.BackupPath, old);
            File.WriteAllText(files.Store.SavePath, "a deliberately interrupted save"); var image = files.Image();
            Assert.That(files.Store.TryLoad(out var absent, out _), Is.False); Assert.That(absent, Is.Null); files.Unchanged(image);
            Assert.That(files.Store.TryRecoverBackup(out var loaded, out string message), Is.True, message);
            OnlyNeutral(old, Payload(loaded)); Current(Payload(loaded));
            Assert.That(File.ReadAllBytes(files.Store.BackupPath).SequenceEqual(image["episode.json.backup"]), Is.True);
            Assert.That(File.ReadAllBytes(files.Store.SavePath).SequenceEqual(image["episode.json.backup"]), Is.True);
            var retained = Directory.GetFiles(files.Root, "episode.json.before-recovery-*.json"); Assert.That(retained, Has.Length.EqualTo(1));
            Assert.That(File.ReadAllBytes(retained[0]).SequenceEqual(image["episode.json"]), Is.True);
            var progressed = PlayOne(loaded); files.Store.Save(progressed);
            Assert.That(files.Store.TryLoad(out var resumed, out message), Is.True, message);
            Assert.That(JToken.DeepEquals(Payload(resumed), Payload(progressed)), Is.True);
            Assert.That(File.ReadAllBytes(retained[0]).SequenceEqual(image["episode.json"]), Is.True); files.NoPending(); PackageUnchanged(package);
        }

        private static void OnlyNeutral(JObject old, JObject next)
        {
            Current(next); // Full current shape/core/storage must pass BEFORE any neutral projection.
            string before = Text(old); Assert.That((int)old["schemaVersion"], Is.EqualTo(25)); Assert.That((int)next["schemaVersion"], Is.EqualTo(26));
            Assert.That(next["unifiedVoteReveals"], Is.TypeOf<JArray>()); Assert.That((JArray)next["unifiedVoteReveals"], Is.Empty);
            var inverse = (JObject)next.DeepClone(); inverse.Remove("unifiedVoteReveals"); inverse["schemaVersion"] = 25;
            foreach (JObject row in (JArray)inverse["unifiedCommitments"])
            {
                Assert.That((string)row["kind"], Is.EqualTo(UnifiedCommitments.Safety)); Assert.That(row.Properties().Count(), Is.EqualTo(17));
                foreach (string field in new[] { "targetId", "subtype" }) { Assert.That(row.Property(field), Is.Not.Null); Assert.That(row[field].Type, Is.EqualTo(JTokenType.Null)); row.Remove(field); }
            }
            Assert.That(JToken.DeepEquals(inverse, old), Is.True, "Every RNG, ID, raw Vote price, term, audience, receipt and array order is unchanged.");
            var guarded = (JObject)next.DeepClone(); PersistenceMigrationTests.StripSchema26(guarded);
            Assert.That(JToken.DeepEquals(guarded, old), Is.True, "The actual test-only bridge proves the same exact inverse after full current validation.");
            Assert.That(Text(old), Is.EqualTo(before)); Assert.That(next, Is.Not.SameAs(old));
        }
        private static void Current(JObject payload)
        {
            string before = Text(payload); Invoke(JsonApi, "CheckDtoShape", payload, typeof(EpisodeState), "state");
            var state = payload.ToObject<EpisodeState>(Serializer()); string stateBefore = Text(Payload(state));
            Assert.That(EpisodeValidation.TryValidate(state, out string error), Is.True, error); Assert.DoesNotThrow(() => EpisodeSaveValidation.Validate(state));
            Assert.That(Text(Payload(state)), Is.EqualTo(stateBefore)); Assert.That(Text(payload), Is.EqualTo(before));
        }
        private static EpisodeState PlayOne(EpisodeState s)
        {
            var next = (EpisodeCommand)typeof(FrozenEpisodeV25ContractTests).GetMethod("Next", Static).Invoke(null, new object[] { s, true });
            var engine = new EpisodeEngine(s); string before = Text(Payload(s)); var result = engine.Apply(next);
            Assert.That(result.accepted, Is.True, result.reason); Assert.That(result.duplicate, Is.False);
            Assert.That(result.state.revision, Is.EqualTo(s.revision + 1)); Assert.That(Text(Payload(s)), Is.EqualTo(before));
            Current(Payload(result.state)); return result.state;
        }
        private static int Mode(JObject s) => (int)s["unifiedCommitmentRulesVersion"] == 0 ? 0 : (int)s["unifiedHearingRulesVersion"] == 1 ? 2 : 1;
        private static object Package()
        {
            var package = Invoke(typeof(FrozenEpisodeV25CorpusTests), "GetPackage");
            var files = (Dictionary<string, byte[]>)package.GetType().GetField("Files", Instance).GetValue(package);
            Assert.That(files.Count, Is.EqualTo(775)); Assert.That(Hash(files["corpus.json"]), Is.EqualTo(ManifestHash));
            Assert.That(Hash(files["binding.json"]), Is.EqualTo(BindingHash));
            string root = (string)package.GetType().GetField("Root", Instance).GetValue(package);
            string index = Path.Combine(root, "portable-index.json"); Files.NoReparse(index);
            Assert.That(new FileInfo(index).Length, Is.EqualTo(142582)); Assert.That(Hash(File.ReadAllBytes(index)), Is.EqualTo(IndexHash));
            return package;
        }
        private static JObject Manifest(object package) => (JObject)package.GetType().GetField("Manifest", Instance).GetValue(package);
        private static IEnumerable<JObject> Aliases(object package) => ((JArray)Manifest(package)["payloadAliases"]).OfType<JObject>();
        private static JObject Read(object package, string relative)
        {
            // Reuse the unchanged strict portable loader; never resolve its absolute provenance.
            var files = (Dictionary<string, byte[]>)package.GetType().GetField("Files", Instance).GetValue(package);
            var copy = (byte[])files[relative].Clone(); using var reader = new JsonTextReader(new StringReader(new System.Text.UTF8Encoding(false, true).GetString(copy)))
                { DateParseHandling = DateParseHandling.None, MaxDepth = 64 };
            var result = JToken.ReadFrom(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
            Assert.That(result, Is.TypeOf<JObject>()); Assert.That(reader.Read(), Is.False); return (JObject)result;
        }
        private static JObject Recorded(object package, int mode)
        {
            foreach (var alias in Aliases(package).Where(a => (string)a["kind"] == "accepted"))
            {
                var payload = Read(package, (string)alias["file"]);
                if (Mode(payload) != mode || (int)payload["revision"] == 0 || (int)payload["phase"] == 13) continue;
                if (mode != 0 && !((JArray)payload["unifiedCommitments"]).OfType<JObject>().Any(r => (string)r["sourcePolicy"] == UnifiedCommitments.DealPolicy
                    && (string)r["status"] == DealStatus.Broken)) continue;
                if (mode == 2 && ((JArray)payload["unifiedHearingEvidence"]).Count == 0) continue;
                Invoke(Frozen, "Validate", payload); return payload;
            }
            Assert.Fail("The pinned corpus must contain the specified real nonterminal recorded history."); return null;
        }
        private static void PackageUnchanged(object package) => Invoke(typeof(FrozenEpisodeV25CorpusTests), "AssertPhysicalUnchanged", package);
        private static void ProjectionRefused(JObject payload)
        {
            Exception refusal = null;
            try { PersistenceMigrationTests.StripSchema26(payload); }
            catch (Exception error) { refusal = error is TargetInvocationException invocation ? invocation.InnerException ?? invocation : error; }
            Assert.That(refusal, Is.Not.Null, "Do not strip neutral-looking fields from an invalid current state.");
            Assert.That(refusal is InvalidDataException || refusal is AssertionException, Is.True, "Only actual strict shape/core or literal guard refusal is accepted, not an incidental exception.");
        }
        private static object Invoke(Type type, string name, params object[] arguments)
        {
            try { return type.GetMethod(name, Static).Invoke(null, arguments); }
            catch (TargetInvocationException error) { throw error.InnerException ?? error; }
        }
        private static JsonSerializer Serializer() => (JsonSerializer)Invoke(JsonApi, "Serializer");
        private static JObject Payload(EpisodeState s) => JObject.FromObject(s, Serializer());
        private static string Text(JToken t) => t.ToString(Formatting.None);
        private static string Hash(byte[] bytes) { using var digest = SHA256.Create(); return BitConverter.ToString(digest.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }
        private sealed class Files : IDisposable
        {
            private readonly string id = Guid.NewGuid().ToString("N");
            internal readonly string Root;
            internal readonly EpisodeSaveStore Store;
            internal Files()
            {
                Root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "GamesimV26-" + id)); NoReparse(Root);
                Directory.CreateDirectory(Root); Store = new EpisodeSaveStore(Path.Combine(Root, "episode.json"));
            }
            internal void Write(string path, JObject payload) => File.WriteAllText(path, PersistenceMigrationTests.Envelope(payload));
            internal Dictionary<string, byte[]> Image()
            {
                NoReparse(Root); Assert.That(Directory.GetDirectories(Root), Is.Empty); var result = new Dictionary<string, byte[]>(StringComparer.Ordinal);
                foreach (var path in Directory.GetFiles(Root)) { NoReparse(path); Assert.That(new FileInfo(path).Length, Is.LessThanOrEqualTo(8L * 1024 * 1024)); result.Add(Path.GetFileName(path), File.ReadAllBytes(path)); }
                NoPending(); return result;
            }
            internal void Unchanged(Dictionary<string, byte[]> before)
            {
                var after = Image(); Assert.That(after.Keys, Is.EquivalentTo(before.Keys));
                foreach (var file in before) Assert.That(after[file.Key].SequenceEqual(file.Value), Is.True, file.Key);
            }
            internal void NoPending() => Assert.That(Directory.GetFiles(Root, "*.pending-*"), Is.Empty);
            public void Dispose()
            {
                string temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                Assert.That(string.Equals(Path.GetDirectoryName(Root), temp, StringComparison.OrdinalIgnoreCase), Is.True);
                Assert.That(Path.GetFileName(Root), Is.EqualTo("GamesimV26-" + id)); NoReparse(Root);
                if (!Directory.Exists(Root)) return;
                Assert.That(Directory.GetDirectories(Root), Is.Empty); foreach (string path in Directory.GetFiles(Root)) NoReparse(path);
                Directory.Delete(Root, true);
            }
            internal static void NoReparse(string path)
            {
                string at = Path.GetFullPath(path);
                while (!string.IsNullOrEmpty(at))
                {
                    if (File.Exists(at) || Directory.Exists(at)) Assert.That((File.GetAttributes(at) & FileAttributes.ReparsePoint) == 0, Is.True);
                    string parent = Path.GetDirectoryName(at); if (parent == at) break; at = parent;
                }
            }
        }
    }
}
