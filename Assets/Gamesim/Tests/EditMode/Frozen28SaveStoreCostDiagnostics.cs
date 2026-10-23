// RETIRED 2026-10-22, kept for the record, not run: it measured the interim schema-28 landing (it asserts schema 28
// after a load and the 56-row manifest). Every load of its pre-29 fixtures now migrates to 29; the cost is measured in
// Assets/Plans/OLD-SAVE-ACCEPTANCE.md section 6, step 3. Write a new diagnostic for 29 rather than re-pinning this one.
// The !UNITY_5_3_OR_NEWER clause keeps it out of every Unity compile, diagnostic runs included: the batch runner
// executes [Explicit] tests, and this one would fail on the 59-row manifest. Frozen28PhasePilotDiagnostics, which
// reads this class, is retired the same way.
#if UNITY_EDITOR && GAMESIM_DIAGNOSTICS && !UNITY_5_3_OR_NEWER
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Gamesim.Persistence;
using Gamesim.Simulation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Explicit native cost observations, never shipping latency acceptance. The define keeps this
    /// out of normal discovery: Unity batch runs cannot be kept out by Explicit alone.
    /// Run only through the owned diagnostic controller with this exact class filter.
    /// </summary>
    // Unity's NUnit 3.5 fork supports explicit None, not NonParallelizableAttribute.
    [Parallelizable(ParallelScope.None)]
    public sealed class Frozen28SaveStoreCostDiagnostics
    {
        private const int Warmups = 5;
        private const int Samples = 30;
        private const string SemanticReason = "The all-week rules' records are only ever between two houseguests.";
        private const string ChecksumReason = "Save checksum does not match; the file may be damaged.";
        private static readonly string[] Required =
        {
            "s28-mode1-allweek-w2-campaign", "s28-mode1-finished",
            "s28-mode2-allweek-w3-campaign", "s28-mode2-finished", "s27-mode1-w3-social"
        };
        private static readonly string[] FrozenFiles =
        {
            "FrozenEpisodeV28", "FrozenV28CommitmentReferences", "FrozenV28Data", "FrozenV28Input",
            "FrozenV28ReadOnly.Core", "FrozenV28ReadOnly.Leaves", "FrozenV28ReadOnly.LiteralRows",
            "FrozenV28ReadOnly.Privacy", "FrozenV28ReadOnly.Safety", "FrozenV28ReferenceRows",
            "FrozenV28SafetyStorage", "FrozenV28Shape", "FrozenV28SocialHistory", "FrozenV28VoteStorage",
            "FrozenV28ReadOnly.VoteHistory", "FrozenV28UnifiedVoteCompletedReveal",
            "FrozenV28UnifiedVoteFamilyValidation", "FrozenV28UnifiedVoteRevealArchive"
        };

        private static IEnumerable<TestCaseData> StoreCostCases()
        {
            foreach (string id in Required)
                foreach (Operation operation in new[]
                {
                    Operation.Load, Operation.FreshSave, Operation.RotateSave, Operation.Recover, Operation.ChecksumLoadRefusal
                }) yield return StoreCostCase(id, operation);
            yield return StoreCostCase("s28-mode2-allweek-w3-campaign", Operation.SemanticLoadRefusal);
            yield return StoreCostCase("s28-mode2-allweek-w3-campaign", Operation.SemanticSaveRefusal);
        }

        private static TestCaseData StoreCostCase(string fixtureId, Operation operation) =>
            new TestCaseData(fixtureId, operation.ToString()).SetName(
                nameof(ObserveActualStoreCostsByOperation) + "_" + fixtureId + "_" + operation);

        // Each complete series owns its unchanged 180-second native case budget. Repeat all
        // fixture controls inside every case; no checks move into uncounted setup/background work.
        [TestCaseSource(nameof(StoreCostCases)), Explicit("Actual native SaveStore observations: first observed, five warmups, thirty samples; no latency SLA.")]
        public void ObserveActualStoreCostsByOperation(string fixtureId, string operationName)
        {
            string run = Guid.NewGuid().ToString("N");
            string fixtureRoot = Path.Combine(Application.dataPath, "Gamesim", "Tests", "EditMode", "Fixtures", "OldSaves");
            string manifestPath = Path.Combine(fixtureRoot, "manifest.json");
            byte[] manifestBytes = File.ReadAllBytes(manifestPath);
            var manifest = Parse(manifestBytes);
            var rows = ((JArray)manifest["episodes"]).Cast<JObject>().ToArray();
            Assert.That(rows.Length, Is.EqualTo(56), "Re-review a changed historical fixture matrix before measuring it.");
            var largest = rows.OrderByDescending(row => (long)row["saveBytes"])
                .ThenBy(row => (string)row["id"], StringComparer.Ordinal).First();
            string[] ids = Required.Concat(new[] { (string)largest["id"] }).Distinct(StringComparer.Ordinal).ToArray();
            var fixtures = ids.Select(id => new Fixture(fixtureRoot,
                rows.Single(row => string.Equals((string)row["id"], id, StringComparison.Ordinal)))).ToArray();
            Assert.That(fixtures.Length, Is.InRange(5, 6));
            Assert.That(fixtures.Select(f => f.Hash).Distinct(StringComparer.Ordinal).Count(), Is.EqualTo(fixtures.Length),
                "Duplicate fixture bytes require an explicit matrix review, not duplicate cost claims.");
            Assert.That(fixtures.Select(f => f.Id), Is.EqualTo(Required),
                "Re-review the partitioned protocol if the largest fixture changes the five-fixture matrix.");
            Assert.That(Enum.TryParse(operationName, out Operation selectedOperation), Is.True);
            Assert.That(Enum.IsDefined(typeof(Operation), selectedOperation), Is.True);
            Assert.That(operationName, Is.EqualTo(selectedOperation.ToString()));
            bool semanticRefusal = selectedOperation == Operation.SemanticLoadRefusal || selectedOperation == Operation.SemanticSaveRefusal;
            Assert.That(!semanticRefusal || fixtureId == "s28-mode2-allweek-w3-campaign", Is.True);
            Metadata(run, Hash(manifestBytes), (string)largest["id"], fixtureId, operationName);
            try
            {
                foreach (var fixture in fixtures)
                {
                    // Deliberately untimed: prove this exact writer file is a valid control first.
                    // Consequently no subsequent first sample is described as process/serializer cold.
                    using var control = new OwnedSlot();
                    control.Prepare(fixture.Bytes, null);
                    var image = control.Image();
                    bool loaded = control.Store.TryLoad(out var state, out string message);
                    Assert.That(loaded, Is.True, "Retained writer control must load: " + fixture.Id);
                    Assert.That(state, Is.Not.Null);
                    Assert.That(state.schemaVersion, Is.EqualTo(28), "This diagnostic is the schema-28 B+ gate.");
                    control.AssertImage(image);
                    fixture.State = state;
                    fixture.StateHash = StateHash(state);
                    fixture.BadChecksum = CorruptChecksum(fixture.Bytes);
                    Emit(new JObject
                    {
                        ["kind"] = "fixture", ["run"] = run, ["id"] = fixture.Id, ["sha256"] = fixture.Hash,
                        ["bytes"] = fixture.Bytes.Length, ["schema"] = fixture.Schema,
                        ["loadedSchema"] = state.schemaVersion, ["mode"] = state.unifiedCommitmentRulesVersion,
                        ["week"] = state.week, ["phase"] = state.phase.ToString(), ["normalizedStateSha256"] = fixture.StateHash,
                        ["largestRetainedByManifestBytes"] = fixture.Id == (string)largest["id"],
                        ["preflightValidated"] = true
                    });
                }
                var selected = fixtures.Single(f => f.Id == fixtureId);
                var alternate = fixtures.First(f => f.Hash != selected.Hash);
                byte[] malformed = semanticRefusal ? PlayerEdgeDefect(selected.Bytes) : null;
                Measure(run, selected, alternate, selectedOperation, malformed);
            }
            finally
            {
                foreach (var fixture in fixtures) fixture.AssertUnchanged();
                Assert.That(Hash(File.ReadAllBytes(manifestPath)), Is.EqualTo(Hash(manifestBytes)), "Fixture manifest changed.");
            }
            Emit(new JObject
            {
                ["kind"] = "complete", ["run"] = run, ["fixtureCount"] = fixtures.Length,
                ["series"] = 1,
                ["timedCalls"] = 1 + Warmups + Samples,
                ["interpretation"] = "Correctness assertions and cost observations completed; no latency acceptance verdict.",
                ["baselineComparison"] = "None. Candidate observations only; incremental Frozen28 overhead is not isolated.",
                ["standalone1080pAcceptance"] = "Not measured by this EditMode diagnostic."
            });
        }

        private enum Operation { Load, FreshSave, RotateSave, Recover, ChecksumLoadRefusal, SemanticLoadRefusal, SemanticSaveRefusal }
        private sealed class Result
        {
            internal bool Success;
            internal EpisodeState State;
            internal string Message;
            internal Exception Error;
        }

        private static void Measure(string run, Fixture fixture, Fixture alternate, Operation operation, byte[] malformed)
        {
            var measured = new List<double>(Samples);
            using var slot = new OwnedSlot();
            for (int index = -Warmups - 1; index < Samples; index++)
            {
                string phase = index == -Warmups - 1 ? "first-observed-after-preflight" : index < 0 ? "warmup" : "measured";
                byte[] primary = fixture.Bytes, backup = alternate.Bytes;
                if (operation == Operation.FreshSave) { primary = null; backup = null; }
                if (operation == Operation.Recover) { primary = fixture.BadChecksum; backup = fixture.Bytes; }
                if (operation == Operation.ChecksumLoadRefusal) primary = fixture.BadChecksum;
                if (operation == Operation.SemanticLoadRefusal || operation == Operation.SemanticSaveRefusal) primary = malformed;
                slot.Prepare(primary, backup);
                var before = slot.Image();
                var result = new Result();
                var watch = new Stopwatch();
                // Only the complete public operation is timed. No assertion, hashing, slot reset,
                // fixture load, diagnostic output, or reload correctness check is inside this region.
                watch.Start();
                try
                {
                    switch (operation)
                    {
                        case Operation.FreshSave:
                        case Operation.RotateSave:
                        case Operation.SemanticSaveRefusal:
                            slot.Store.Save(fixture.State);
                            result.Success = true;
                            break;
                        case Operation.Recover:
                            result.Success = slot.Store.TryRecoverBackup(out result.State, out result.Message);
                            break;
                        default:
                            result.Success = slot.Store.TryLoad(out result.State, out result.Message);
                            break;
                    }
                }
                catch (Exception error) { result.Error = error; }
                finally { watch.Stop(); }
                double milliseconds = watch.ElapsedTicks * 1000.0 / Stopwatch.Frequency;
                Emit(new JObject
                {
                    ["kind"] = "sample", ["run"] = run, ["fixture"] = fixture.Id, ["operation"] = operation.ToString(),
                    ["phase"] = phase, ["index"] = index, ["elapsedTicks"] = watch.ElapsedTicks,
                    ["milliseconds"] = milliseconds, ["returnedSuccess"] = result.Success,
                    ["exceptionType"] = result.Error == null ? null : result.Error.GetType().FullName
                });
                Check(operation, fixture, slot, before, result, primary);
                Assert.That(StateHash(fixture.State), Is.EqualTo(fixture.StateHash), "Save operation mutated its input.");
                slot.AssertNoPending();
                if (index >= 0) measured.Add(milliseconds);
            }
            var sorted = measured.OrderBy(value => value).ToArray();
            Assert.That(sorted.Length, Is.EqualTo(Samples));
            Emit(new JObject
            {
                ["kind"] = "series", ["run"] = run, ["fixture"] = fixture.Id, ["operation"] = operation.ToString(),
                ["samples"] = Samples, ["warmups"] = Warmups, ["firstObservedCount"] = 1,
                ["p50Ms"] = (sorted[Samples / 2 - 1] + sorted[Samples / 2]) / 2.0,
                ["p95Ms"] = sorted[(int)Math.Ceiling(Samples * 0.95) - 1], ["maxMs"] = sorted[Samples - 1],
                ["p95Definition"] = "nearest-rank", ["rawMeasuredMs"] = new JArray(measured),
                ["correctness"] = "Passed", ["latencyVerdict"] = "Not specified; observation only"
            });
        }

        private static void Check(Operation operation, Fixture fixture, OwnedSlot slot,
            SortedDictionary<string, byte[]> before, Result result, byte[] primary)
        {
            if (operation == Operation.SemanticSaveRefusal)
            {
                Assert.That(result.Error is InvalidDataException, Is.True,
                    "Expected explicit overwrite refusal; actual exception type: " + (result.Error?.GetType().FullName ?? "none"));
                Assert.That(result.Error.Message, Does.Contain("current save is unreadable"));
                Assert.That(result.Success, Is.False);
                slot.AssertImage(before);
                Assert.That(slot.Store.TryLoad(out var refused, out string reason), Is.False);
                Assert.That(refused, Is.Null);
                Assert.That(reason, Does.Contain(SemanticReason));
                slot.AssertImage(before);
                return;
            }
            Assert.That(result.Error == null, Is.True,
                "Unexpected store exception type: " + (result.Error?.GetType().FullName ?? "none"));
            if (operation == Operation.ChecksumLoadRefusal || operation == Operation.SemanticLoadRefusal)
            {
                Assert.That(result.Success, Is.False);
                Assert.That(result.State, Is.Null, "No automatic recovery on refused load.");
                Assert.That(result.Message, Does.Contain(operation == Operation.ChecksumLoadRefusal ? ChecksumReason : SemanticReason));
                Assert.That(result.Message, Does.Contain("validated backup is available"));
                slot.AssertImage(before);
                return;
            }
            Assert.That(result.Success, Is.True, "The public operation must succeed on its valid writer control.");
            if (operation == Operation.Load)
            {
                Assert.That(StateHash(result.State), Is.EqualTo(fixture.StateHash));
                slot.AssertImage(before);
                return;
            }
            if (operation == Operation.Recover)
            {
                Assert.That(StateHash(result.State), Is.EqualTo(fixture.StateHash));
                Assert.That(File.ReadAllBytes(slot.Store.SavePath), Is.EqualTo(fixture.Bytes), "Recovery must install the exact backup bytes.");
                Assert.That(File.ReadAllBytes(slot.Store.BackupPath), Is.EqualTo(fixture.Bytes), "Recovery must preserve the exact backup bytes.");
                string[] retained = slot.ContentFiles().Where(file => Path.GetFileName(file).StartsWith("episode.json.before-recovery-", StringComparison.Ordinal)).ToArray();
                Assert.That(retained.Length, Is.EqualTo(1));
                Assert.That(File.ReadAllBytes(retained[0]), Is.EqualTo(primary), "The refused original must survive explicit recovery.");
                Assert.That(slot.ContentFiles().Length, Is.EqualTo(3));
            }
            else
            {
                Assert.That(File.Exists(slot.Store.SavePath), Is.True);
                Assert.That(File.Exists(slot.Store.BackupPath), Is.EqualTo(operation == Operation.RotateSave));
                if (operation == Operation.RotateSave)
                    Assert.That(File.ReadAllBytes(slot.Store.BackupPath), Is.EqualTo(fixture.Bytes), "Backup must be the original primary, not the former backup.");
                Assert.That(slot.ContentFiles().Length, Is.EqualTo(operation == Operation.RotateSave ? 2 : 1));
                var saved = Parse(File.ReadAllBytes(slot.Store.SavePath));
                Assert.That((int)saved["state"]["schemaVersion"], Is.EqualTo(28));
                Assert.That(Hash(Encoding.UTF8.GetBytes(Canonical(saved["state"]))), Is.EqualTo(fixture.StateHash));
            }
            // Untimed roundtrip check; the public loader applies its normal checksum/shape/semantic gates.
            var installedImage = slot.Image();
            Assert.That(slot.Store.TryLoad(out var reloaded, out _), Is.True);
            Assert.That(StateHash(reloaded), Is.EqualTo(fixture.StateHash));
            slot.AssertImage(installedImage);
        }

        private static void Metadata(string run, string manifestHash, string largestId, string leafFixture, string leafOperation)
        {
            var sourceHashes = new JObject();
            foreach (string name in FrozenFiles.Concat(new[] { "EpisodeSaveStore", "EpisodeSaveMigrations" }))
            {
                string relative = "Gamesim/Runtime/Persistence/" + name + ".cs";
                sourceHashes[relative] = Hash(File.ReadAllBytes(Path.Combine(Application.dataPath, relative)));
            }
            string diagnostic = "Gamesim/Tests/EditMode/Frozen28SaveStoreCostDiagnostics.cs";
            sourceHashes[diagnostic] = Hash(File.ReadAllBytes(Path.Combine(Application.dataPath, diagnostic)));
            string driveRoot = Path.GetPathRoot(Path.GetFullPath(Path.GetTempPath()));
            var drive = new DriveInfo(driveRoot);
            Emit(new JObject
            {
                ["kind"] = "environment", ["run"] = run, ["utc"] = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                ["protocolVersion"] = 2, ["leafFixture"] = leafFixture, ["leafOperation"] = leafOperation,
                ["unity"] = Application.unityVersion, ["runtime"] = Environment.Version.ToString(),
                ["os"] = SystemInfo.operatingSystem, ["processor"] = SystemInfo.processorType,
                ["processorCount"] = SystemInfo.processorCount, ["is64BitProcess"] = Environment.Is64BitProcess,
                ["tempDriveRoot"] = driveRoot, ["tempDriveType"] = drive.DriveType.ToString(),
                ["tempDriveFormat"] = drive.DriveFormat, ["tempDriveFreeBytes"] = drive.AvailableFreeSpace,
                ["stopwatchFrequency"] = Stopwatch.Frequency, ["highResolutionTimer"] = Stopwatch.IsHighResolution,
                ["runtimeAssemblyMvid"] = typeof(EpisodeSaveStore).Assembly.ManifestModule.ModuleVersionId.ToString(),
                ["simulationAssemblyMvid"] = typeof(EpisodeState).Assembly.ManifestModule.ModuleVersionId.ToString(),
                ["testAssemblyMvid"] = typeof(Frozen28SaveStoreCostDiagnostics).Assembly.ManifestModule.ModuleVersionId.ToString(),
                ["newtonsoftAssembly"] = typeof(JObject).Assembly.GetName().Version.ToString(),
                ["manifestSha256"] = manifestHash, ["largestRetainedFixtureId"] = largestId, ["sourceSha256"] = sourceHashes,
                ["sourceIdentity"] = "Join this output to the owned controller's complete snapshot and manifest receipts.",
                ["coldProcessMeasurement"] = false, ["coldFilesystemMeasurement"] = false,
                ["firstSampleMeaning"] = "First observed in this operation series, after fixture validation; serializer, reader and filesystem caches may already be warm.",
                ["allocationMeasurement"] = "Not collected; no zero-allocation claim.",
                ["hostIsolation"] = "Owned-controller process checks only; no claim of globally idle CPU/GPU.",
                ["latencyBudgetMs"] = JValue.CreateNull()
            });
        }

        private static void Emit(JObject value) => TestContext.Out.WriteLine("FROZEN28_COST " + value.ToString(Formatting.None));

        private static JObject Parse(byte[] bytes)
        {
            using var stream = new MemoryStream(bytes, false);
            using var text = new StreamReader(stream, Encoding.UTF8, true);
            using var reader = new JsonTextReader(text) { DateParseHandling = DateParseHandling.None };
            return JObject.Load(reader);
        }

        private static string Hash(byte[] bytes)
        {
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }

        private static string Canonical(JToken token)
        {
            JToken Ordered(JToken value)
            {
                if (value is JObject obj) return new JObject(obj.Properties().OrderBy(property => property.Name, StringComparer.Ordinal)
                    .Select(property => new JProperty(property.Name, Ordered(property.Value))));
                if (value is JArray array) return new JArray(array.Select(Ordered));
                return value.DeepClone();
            }
            return Ordered(token).ToString(Formatting.None);
        }

        private static string StateHash(EpisodeState state)
        {
            Assert.That(state, Is.Not.Null);
            var method = typeof(EpisodeSaveStore).Assembly.GetType("Gamesim.Persistence.SaveJson", true)
                .GetMethod("Serializer", BindingFlags.Public | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);
            var serializer = (JsonSerializer)method.Invoke(null, null);
            return Hash(Encoding.UTF8.GetBytes(Canonical(JObject.FromObject(state, serializer))));
        }

        private static byte[] CorruptChecksum(byte[] original)
        {
            var envelope = Parse(original);
            string good = (string)envelope["checksum"];
            Assert.That(good.Length, Is.EqualTo(64));
            envelope["checksum"] = (good[0] == '0' ? "1" : "0") + good.Substring(1);
            return Encoding.UTF8.GetBytes(envelope.ToString(Formatting.Indented));
        }

        private static byte[] PlayerEdgeDefect(byte[] original)
        {
            var envelope = Parse(original);
            var payload = (JObject)envelope["state"];
            Assert.That((int)payload["schemaVersion"], Is.EqualTo(28));
            Assert.That((int)payload["unifiedCommitmentRulesVersion"], Is.EqualTo(2));
            int week = (int)payload["week"];
            Assert.That((int)payload["allWeekRulesStartWeek"], Is.InRange(1, week));
            string player = (string)payload["playerId"];
            var edge = ((JArray)payload["relationships"]).Cast<JObject>().First(row => (string)row["fromId"] == player);
            int sequence = (int)payload["nextSequence"];
            ((JArray)edge["events"]).Add(new JObject
            {
                ["sequence"] = sequence, ["week"] = week, ["type"] = "npc-conversation",
                ["description"] = "Detached cost diagnostic refusal control", ["impactScore"] = 25.0, ["decayable"] = true
            });
            payload["nextSequence"] = sequence + 1;
            envelope["checksum"] = Hash(Encoding.UTF8.GetBytes(Canonical(payload)));
            return Encoding.UTF8.GetBytes(envelope.ToString(Formatting.Indented));
        }

        private sealed class Fixture
        {
            internal readonly string Id, Hash;
            internal readonly byte[] Bytes;
            internal readonly int Schema;
            private readonly string path;
            internal EpisodeState State;
            internal string StateHash;
            internal byte[] BadChecksum;
            internal Fixture(string root, JObject entry)
            {
                Id = (string)entry["id"];
                Assert.That(Regex.IsMatch(Id, "\\A[a-z0-9-]+\\z"), Is.True);
                string file = (string)entry["save"];
                Assert.That(file, Is.EqualTo(Id + ".save.json"));
                path = Path.Combine(root, file);
                Bytes = File.ReadAllBytes(path);
                Hash = Frozen28SaveStoreCostDiagnostics.Hash(Bytes);
                Schema = (int)entry["schema"];
                Assert.That(Bytes.LongLength, Is.EqualTo((long)entry["saveBytes"]), "Fixture byte length: " + Id);
                Assert.That(Hash, Is.EqualTo((string)entry["saveSha256"]), "Fixture digest: " + Id);
            }
            internal void AssertUnchanged()
            {
                Assert.That(Frozen28SaveStoreCostDiagnostics.Hash(Bytes), Is.EqualTo(Hash), "In-memory fixture bytes changed: " + Id);
                Assert.That(File.ReadAllBytes(path), Is.EqualTo(Bytes), "Original writer fixture changed: " + Id);
            }
        }

        private sealed class OwnedSlot : IDisposable
        {
            private const string Prefix = "GamesimFrozen28Cost-";
            private readonly string token = Guid.NewGuid().ToString("N");
            private readonly string tempRoot, root, ownerPath;
            internal readonly EpisodeSaveStore Store;
            internal OwnedSlot()
            {
                tempRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                root = Path.GetFullPath(Path.Combine(tempRoot, Prefix + token));
                ownerPath = Path.Combine(root, ".owner");
                GuardPath();
                if (Directory.Exists(root) || File.Exists(root)) throw new InvalidOperationException("Owned diagnostic slot already exists.");
                Directory.CreateDirectory(root);
                using (var marker = new FileStream(ownerPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    byte[] bytes = Encoding.UTF8.GetBytes(token);
                    marker.Write(bytes, 0, bytes.Length);
                }
                Store = new EpisodeSaveStore(Path.Combine(root, "episode.json"));
                Guard();
            }
            internal void Prepare(byte[] primary, byte[] backup)
            {
                Clear();
                if (primary != null) WriteNew(Store.SavePath, primary);
                if (backup != null) WriteNew(Store.BackupPath, backup);
            }
            private void WriteNew(string path, byte[] bytes)
            {
                Guard();
                Assert.That(path == Store.SavePath || path == Store.BackupPath, Is.True);
                using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                output.Write(bytes, 0, bytes.Length);
            }
            internal string[] ContentFiles()
            {
                Guard();
                return Directory.GetFiles(root).Where(file => file != ownerPath).ToArray();
            }
            internal SortedDictionary<string, byte[]> Image()
            {
                Guard();
                return new SortedDictionary<string, byte[]>(Directory.GetFiles(root)
                    .ToDictionary(Path.GetFileName, File.ReadAllBytes), StringComparer.Ordinal);
            }
            internal void AssertImage(SortedDictionary<string, byte[]> expected)
            {
                var actual = Image();
                Assert.That(actual.Keys, Is.EqualTo(expected.Keys), "Unexpected file creation, deletion or recovery.");
                foreach (var pair in expected) Assert.That(actual[pair.Key], Is.EqualTo(pair.Value), "Changed owned slot bytes: " + pair.Key);
            }
            internal void AssertNoPending() => Assert.That(ContentFiles().Any(file => Path.GetFileName(file).Contains(".pending-")), Is.False);
            private void Clear()
            {
                Guard();
                foreach (string file in ContentFiles()) { Guard(); File.Delete(file); }
            }
            private void GuardPath()
            {
                Assert.That(string.Equals(Path.GetDirectoryName(root), tempRoot, StringComparison.OrdinalIgnoreCase), Is.True);
                Assert.That(Path.GetFileName(root), Is.EqualTo(Prefix + token));
                for (string cursor = root; !string.IsNullOrEmpty(cursor); cursor = Path.GetDirectoryName(cursor))
                    if (Directory.Exists(cursor) || File.Exists(cursor))
                        Assert.That(File.GetAttributes(cursor) & FileAttributes.ReparsePoint, Is.EqualTo((FileAttributes)0),
                            "Refuse any reparse ancestor of the owned diagnostic slot.");
            }
            private void Guard()
            {
                GuardPath();
                Assert.That(Directory.Exists(root), Is.True);
                Assert.That(Directory.GetDirectories(root), Is.Empty);
                foreach (string file in Directory.GetFiles(root))
                {
                    string name = Path.GetFileName(file);
                    Assert.That(name == ".owner" || name == "episode.json" || name == "episode.json.backup" ||
                        Regex.IsMatch(name, "\\Aepisode\\.json\\.(pending-[a-f0-9]{32}|before-recovery-[a-f0-9]{32}\\.json)\\z"), Is.True,
                        "Refuse cleanup of an unexpected file in the owned slot.");
                    Assert.That(File.GetAttributes(file) & FileAttributes.ReparsePoint, Is.EqualTo((FileAttributes)0));
                }
                Assert.That(File.ReadAllText(ownerPath, Encoding.UTF8), Is.EqualTo(token), "Diagnostic slot ownership changed.");
            }
            public void Dispose()
            {
                Clear();
                Guard();
                File.Delete(ownerPath);
                Directory.Delete(root, false);
            }
        }
    }
}
#endif
