using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Gamesim.Persistence;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// JSON-only replay of retained managed schema25 TEST witnesses. No current DTO, engine,
    /// factory, migration, ordinary save, Unity path getter or command execution defines history.
    /// Command packets prove retained coordinate/hash chains, not replayed command behavior.
    /// These 34 cases are distinct from the original 166 current-engine compatibility cases.
    /// </summary>
    public sealed class FrozenEpisodeV25CorpusTests
    {
        private const string ManifestSha = "96e1fd4e26d82d3ceac0945df99a56b5396f8492774e98e5b36851ca39f94b38";
        private const string BindingSha = "984401d1cd2081bd2d0c3152f6689969acf332aa02b3a165103427ee40046501";
        private const string IndexSha = "28c7ff14ceb535cad67433c37b72bb526453db943cb754348feabee33dfee8d0";
        private const int IndexByteLength = 142582;
        private const string Attributes = "# Preserve byte-exact historical JSON witnesses.\n*.json -text -eol\n.gitattributes -text -eol\n";
        private const string RootMeta = "@adjacent-root";
        private const string RelativeRoot = "Assets/Gamesim/Tests/EditMode/Fixtures/FrozenV25";
        private const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly Type Contract = typeof(EpisodeSaveStore).Assembly.GetType("Gamesim.Persistence.FrozenEpisodeV25", true);
        private static readonly Type Shape = Contract.Assembly.GetType("Gamesim.Persistence.FrozenV25Shape", true);
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
        private static readonly Regex ShaPattern = new Regex("^[0-9a-f]{64}\\z", RegexOptions.CultureInvariant);
        private static readonly Regex RawPath = new Regex("^(payloads|commands)/[0-9a-f]{64}\\.json\\z", RegexOptions.CultureInvariant);
        // Optional fresh managed-harness seam only. Ordinary discovery is bounded BCL ancestry.
        private static string PackageRootOverride = null;
        private static Package Cached;

        private sealed class Package
        {
            internal string Root;
            internal JObject Index, Manifest;
            internal Dictionary<string, byte[]> Files, Metadata;
            internal JObject Payload(string path) => Parse(Files[path]);
        }

        [Test]
        public void PortableInventoryPinsOriginalBytesAndNeverLoadsAbsoluteProvenancePaths()
        {
            var package = GetPackage();
            Assert.That(package.Files.Count, Is.EqualTo(775));
            Assert.That(Hash(package.Files["corpus.json"]), Is.EqualTo(ManifestSha));
            Assert.That(Hash(package.Files["binding.json"]), Is.EqualTo(BindingSha));
            Assert.That(package.Metadata.Count, Is.EqualTo(779));
            VerifyManifest(package.Manifest);
            AssertPhysicalUnchanged(package);
        }

        [TestCase(0, 75)] [TestCase(1, 118)] [TestCase(2, 173)]
        public void EveryRetainedAcceptedAliasUsesOnlyTheFixed25Contract(int mode, int expected)
        {
            var package = GetPackage();
            int count = 0;
            foreach (var alias in Array(package.Manifest, "payloadAliases").OfType<JObject>().Where(a => Text(a, "kind") == "accepted"))
            {
                var payload = package.Payload(Text(alias, "file"));
                if (Mode(payload) != mode) continue;
                CheckMode(payload, mode); Accept(payload); count++;
            }
            Assert.That(count, Is.EqualTo(expected));
            AssertPhysicalUnchanged(package);
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
        public void CapturedSemanticDefectsHaveAnAcceptedSameCaseBaselineAndFixedShape(string family, int expected)
        {
            var package = GetPackage();
            var cases = Array(package.Manifest, "caseInvocations");
            var aliases = Array(package.Manifest, "payloadAliases").OfType<JObject>().ToArray();
            int count = 0;
            foreach (var alias in aliases.Where(a => Text(a, "kind") == "semantic-refused" && Text((JObject)cases[Int(a, "caseOrdinal")], "method") == family))
            {
                string baselineSha = Text(alias, "lastAcceptedBaselineSha256");
                Assert.That(aliases.Any(a => Text(a, "kind") == "accepted" && Int(a, "caseOrdinal") == Int(alias, "caseOrdinal")
                    && Text(a, "sha256") == baselineSha && Int(a, "observationOrdinal") < Int(alias, "observationOrdinal")), Is.True);
                var baseline = package.Payload("payloads/" + baselineSha + ".json");
                Accept(baseline);
                var defect = package.Payload(Text(alias, "file")); string original = Json(defect);
                Assert.That(Int(defect, "schemaVersion"), Is.EqualTo(25));
                Assert.DoesNotThrow(() => Invoke(Shape, defect), "Captured semantic defects must satisfy the FIXED25 shape, not a growing DTO shape.");
                Assert.Throws<InvalidDataException>(() => Invoke(Contract, defect));
                Assert.That(Json(defect), Is.EqualTo(original));
                Assert.That(Text(alias, "reason"), Does.Not.Contain("Unsupported episode schema"));
                count++;
            }
            Assert.That(count, Is.EqualTo(expected));
            AssertPhysicalUnchanged(package);
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void TwelveCapturedOpeningsKeepTheirActualModesCastAndFreshCoordinates(int mode)
        {
            var package = GetPackage(); var aliases = Array(package.Manifest, "payloadAliases");
            var rows = Array(Object(package.Manifest, "coverage"), "openings").OfType<JObject>().Where(r => Int(r, "mode") == mode).ToArray();
            Assert.That(rows.Select(r => Int(r, "cast")), Is.EquivalentTo(new[] { 3, 6, 8, 12 }));
            foreach (var row in rows)
            {
                var alias = (JObject)aliases[Int(row, "payloadAliasOrdinal")];
                Assert.That(Text(alias, "sha256"), Is.EqualTo(Text(row, "sha256")));
                Assert.That(Int(alias, "caseOrdinal"), Is.EqualTo(Int(row, "caseOrdinal")));
                Assert.That(Text(alias, "kind"), Is.EqualTo("accepted"));
                var payload = package.Payload(Text(alias, "file")); CheckMode(payload, mode); Accept(payload);
                Assert.That(Int(payload, "revision"), Is.Zero); Assert.That(Int(payload, "week"), Is.EqualTo(1));
                Assert.That(Int(payload, "phase"), Is.Zero); Assert.That(Long(payload, "seed"), Is.EqualTo(2505));
                Assert.That(Array(payload, "contestants").Count, Is.EqualTo(Int(row, "cast")));
            }
            AssertPhysicalUnchanged(package);
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void WholeSeasonPacketsMatchAll67FrozenBoundariesButDoNotReplayCommands(int mode)
        {
            var package = GetPackage(); var aliases = Array(package.Manifest, "payloadAliases"); var commands = Array(package.Manifest, "commandRecords");
            var season = Array(Object(package.Manifest, "coverage"), "wholeSeasons").OfType<JObject>().Single(r => Int(r, "mode") == mode);
            var boundaries = Array(season, "boundaryAliasOrdinals").Select(t => t.Value<int>()).ToArray();
            var sequence = Array(season, "commandRecordOrdinals").Select(t => t.Value<int>()).ToArray();
            Assert.That(boundaries.Length, Is.EqualTo(67)); Assert.That(sequence.Length, Is.EqualTo(66));
            JObject previous = null; var phases = new HashSet<int>();
            for (int i = 0; i < boundaries.Length; i++)
            {
                var alias = (JObject)aliases[boundaries[i]]; var payload = package.Payload(Text(alias, "file"));
                Assert.That(Text(alias, "kind"), Is.EqualTo("accepted")); Assert.That(Int(alias, "caseOrdinal"), Is.EqualTo(Int(season, "caseOrdinal")));
                CheckMode(payload, mode); Accept(payload); Assert.That(Int(payload, "revision"), Is.EqualTo(i));
                phases.Add(Int(payload, "phase"));
                Assert.That(Long(payload, "seed"), Is.EqualTo(2505)); Assert.That(Text(payload, "sessionId"), Is.EqualTo(Text(season, "sessionId")));
                Assert.That(JToken.DeepEquals(Rules(payload), Object(season, "rules")), Is.True);
                if (i == 0) { Assert.That(Int(payload, "phase"), Is.Zero); Assert.That(Int(payload, "week"), Is.EqualTo(1)); }
                if (previous != null)
                {
                    var record = (JObject)commands[sequence[i - 1]]; var packet = package.Payload(Text(record, "file"));
                    Assert.That(Int(record, "caseOrdinal"), Is.EqualTo(Int(season, "caseOrdinal")));
                    Assert.That(Int((JObject)aliases[boundaries[i - 1]], "observationOrdinal"), Is.LessThan(Int(record, "observationOrdinal")));
                    Assert.That(Int(record, "observationOrdinal"), Is.LessThan(Int(alias, "observationOrdinal")));
                    MatchCoordinates(Object(packet, "before"), previous, Text((JObject)aliases[boundaries[i - 1]], "sha256"));
                    MatchCoordinates(Object(packet, "after"), payload, Text(alias, "sha256"));
                    var command = Object(packet, "command");
                    Assert.That(Int(command, "expectedRevision"), Is.EqualTo(i - 1)); Assert.That(Int(command, "expectedPhase"), Is.EqualTo(Int(previous, "phase")));
                    Assert.That(Text(command, "actorId"), Is.EqualTo(Text(previous, "playerId")));
                    Assert.That((string)Array(payload, "acceptedCommandIds").Last(), Is.EqualTo(Text(command, "id")));
                    Assert.That(JToken.DeepEquals(Rules(payload), Rules(previous)), Is.True);
                }
                previous = payload;
            }
            Assert.That(Int(previous, "phase"), Is.EqualTo(13), "Pinned former25 Finished ordinal, not a growing enum.");
            Assert.That(phases, Is.EquivalentTo(Enumerable.Range(0, 16)), "All sixteen literal former25 phases are retained.");
            Assert.That(Text((JObject)aliases[boundaries.Last()], "sha256"), Is.EqualTo(Text(season, "finishedSha256")));
            Assert.That(Text(previous, "winnerId"), Is.EqualTo(Text(season, "winnerId")));
            Assert.That(Text(previous, "runnerUpId"), Is.EqualTo(Text(season, "runnerUpId")));
            Assert.That(Text(previous, "winnerId"), Is.Not.EqualTo(Text(previous, "runnerUpId")));
            AssertPhysicalUnchanged(package);
        }

        [Test]
        public void AllAliasesCasesAndObserverOrderKeepTheirActualMultiplicityAndHonestLabels()
        {
            var package = GetPackage(); var m = package.Manifest;
            var cases = Array(m, "caseInvocations"); var aliases = Array(m, "payloadAliases"); var commands = Array(m, "commandRecords");
            Assert.That(cases.Count, Is.EqualTo(166)); Assert.That(aliases.Count, Is.EqualTo(436)); Assert.That(commands.Count, Is.EqualTo(706));
            var labels = new HashSet<string>(StringComparer.Ordinal); int semantic = 0;
            for (int i = 0; i < cases.Count; i++)
            {
                var row = (JObject)cases[i]; Assert.That(Int(row, "ordinal"), Is.EqualTo(i)); Assert.That(Bool(row, "passed"), Is.True);
                Assert.That(labels.Add(Text(row, "label")), Is.True);
                var owned = aliases.OfType<JObject>().Where(a => Int(a, "caseOrdinal") == i).ToArray();
                Assert.That(Array(row, "payloadAliasOrdinals").Select(t => t.Value<int>()), Is.EqualTo(owned.Select(a => Int(a, "ordinal"))));
                int actual = owned.Count(a => Text(a, "kind") == "semantic-refused"); Assert.That(Int(row, "capturedSemanticRefusalCount"), Is.EqualTo(actual)); semantic += actual;
                if (Bool(row, "genericOrNullRefusalIsMethodPassOnly")) Assert.That(actual, Is.Zero);
            }
            Assert.That(semantic, Is.EqualTo(70)); Assert.That(Bool((JObject)cases[25], "genericOrNullRefusalIsMethodPassOnly"), Is.True);
            var observations = Array(m, "observerOrder"); Assert.That(observations.Count, Is.EqualTo(1142));
            for (int i = 0; i < observations.Count; i++)
            {
                var row = (JObject)observations[i]; Assert.That(Int(row, "ordinal"), Is.EqualTo(i));
                string kind = Text(row, "kind"); var referenced = kind == "accepted-public-command" ? (JObject)commands[Int(row, "commandRecordOrdinal")] : (JObject)aliases[Int(row, "payloadAliasOrdinal")];
                Assert.That(Int(referenced, "observationOrdinal"), Is.EqualTo(i)); Assert.That(Int(referenced, "caseOrdinal"), Is.EqualTo(Int(row, "caseOrdinal")));
                if (kind != "accepted-public-command") Assert.That(Text(referenced, "kind"), Is.EqualTo(kind));
                else Assert.That(Bool(referenced, "completeTraceOrFrozenAcceptanceImplied"), Is.False);
            }
            Assert.That(aliases.OfType<JObject>().Any(a => Text(a, "classification") == "detached-lawful-post-pruning-test-projection"), Is.True);
            AssertPhysicalUnchanged(package);
        }

        [TestCase("traversal")] [TestCase("absolute")] [TestCase("backslash")]
        [TestCase("missing-record")] [TestCase("extra-record")] [TestCase("raw-corruption")]
        [TestCase("wrong-length")] [TestCase("wrong-hash")] [TestCase("duplicate-record")]
        [TestCase("unsorted-records")] [TestCase("unknown-field")] [TestCase("future-version")] [TestCase("missing-field")]
        public void DetachedTamperingUsesTheActualPackageGuardWithoutWritingFiles(string defect)
        {
            var package = GetPackage(); string originalIndex = Json(package.Index);
            var index = (JObject)package.Index.DeepClone(); var files = package.Files.ToDictionary(r => r.Key, r => (byte[])r.Value.Clone(), StringComparer.Ordinal);
            var entries = Array(index, "files"); var entry = (JObject)entries[0]; string path = Text(entry, "path");
            switch (defect)
            {
                case "traversal": entry["path"] = "../corpus.json"; break;
                case "absolute": entry["path"] = "C:/corpus.json"; break;
                case "backslash": entry["path"] = "payloads\\" + ManifestSha + ".json"; break;
                case "missing-record": files.Remove(path); break;
                case "extra-record": files.Add("unexpected.json", Utf8.GetBytes("{}")); break;
                case "raw-corruption": files[path][0] ^= 1; break;
                case "wrong-length": entry["bytes"] = Long(entry, "bytes") + 1; break;
                case "wrong-hash": entry["sha256"] = new string('0', 64); break;
                case "duplicate-record": entries.Add(entry.DeepClone()); break;
                case "unsorted-records": var first = entries[0].DeepClone(); entries[0] = entries[1].DeepClone(); entries[1] = first; break;
                case "unknown-field": index["futureAuthority"] = false; break;
                case "future-version": index["version"] = 2; break;
                case "missing-field": index.Remove("kind"); break;
                default: Assert.Fail("Unknown detached tamper."); break;
            }
            Assert.Throws<InvalidDataException>(() => ValidateData(index, files));
            // Additional detached metadata controls share these existing NUnit cases; no files
            // are changed and no missing GUID/importer is repaired into a passing package.
            if (defect == "missing-record" || defect == "extra-record" || defect == "raw-corruption" || defect == "duplicate-record")
            {
                var metadata = package.Metadata.ToDictionary(r => r.Key, r => (byte[])r.Value.Clone(), StringComparer.Ordinal);
                if (defect == "missing-record") metadata.Remove("corpus.json.meta");
                if (defect == "extra-record") metadata.Add(".gitattributes.meta", MetaBytes(".gitattributes", false));
                if (defect == "raw-corruption") metadata["corpus.json.meta"][0] ^= 1;
                if (defect == "duplicate-record") metadata["binding.json.meta"] = MetaBytes("corpus.json", false);
                Assert.Throws<InvalidDataException>(() => ValidateMetadata(package.Index, metadata));
                if (defect == "missing-record")
                {
                    metadata = package.Metadata.ToDictionary(r => r.Key, r => (byte[])r.Value.Clone(), StringComparer.Ordinal);
                    metadata.Remove(RootMeta);
                    Assert.Throws<InvalidDataException>(() => ValidateMetadata(package.Index, metadata));
                }
            }
            Assert.That(Json(package.Index), Is.EqualTo(originalIndex)); AssertPhysicalUnchanged(package);
        }

        private static Package GetPackage()
        {
            string root = FindRoot();
            if (Cached == null || !string.Equals(Cached.Root, root, StringComparison.OrdinalIgnoreCase)) Cached = Load(root);
            return Cached;
        }
        private static Package Load(string root)
        {
            Require(ShaPattern.IsMatch(IndexSha), "Portable index pin has not been supplied by root.");
            NoReparse(root); var paths = Inventory(root, out var metadata);
            Require(Utf8.GetString(ReadExact(Path.Combine(root, ".gitattributes"), Utf8.GetByteCount(Attributes))) == Attributes, "Byte preservation policy differs.");
            byte[] indexBytes = ReadExact(Path.Combine(root, "portable-index.json"), IndexByteLength); Require(Hash(indexBytes) == IndexSha, "Portable index byte pin differs.");
            var index = Parse(indexBytes); var files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            var rawPaths = new HashSet<string>(paths.Where(p => p != "portable-index.json" && p != ".gitattributes"), StringComparer.Ordinal);
            var lengths = new Dictionary<string, long>(StringComparer.Ordinal); long budget = 0;
            // The index's exact length AND SHA are already pinned. Preflight the entire raw
            // inventory before allocating even the first payload, then recheck each open file.
            foreach (var row in Array(index, "files").OfType<JObject>())
            {
                string relative = Text(row, "path"); long length = Long(row, "bytes");
                Require(rawPaths.Contains(relative) && !lengths.ContainsKey(relative)
                    && (relative == "corpus.json" || relative == "binding.json" || RawPath.IsMatch(relative)), "Indexed raw inventory differs.");
                string full = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)); NoReparse(full);
                Require(length > 0 && length <= 8L * 1024 * 1024 && new FileInfo(full).Length == length, "Package record differs from its pinned bounded length.");
                budget += length; Require(budget <= 256L * 1024 * 1024, "Package raw budget exceeded before allocation."); lengths.Add(relative, length);
            }
            Require(lengths.Count == 775 && rawPaths.Count == 775 && rawPaths.All(lengths.ContainsKey), "Incomplete or extra preflighted raw inventory.");
            foreach (var row in lengths) files.Add(row.Key, ReadExact(Path.Combine(root, row.Key.Replace('/', Path.DirectorySeparatorChar)), row.Value));
            var manifest = ValidateData(index, files);
            ValidateMetadata(index, metadata);
            return new Package { Root = root, Index = index, Manifest = manifest, Files = files, Metadata = metadata };
        }
        private static JObject ValidateData(JObject index, Dictionary<string, byte[]> files)
        {
            Fields(index, "schema", "version", "kind", "manifestSha256", "bindingSha256", "files");
            Require(Int(index, "schema") == 1 && Int(index, "version") == 1 && Text(index, "kind") == "gamesim-frozen25-managed-test-corpus"
                && Text(index, "manifestSha256") == ManifestSha && Text(index, "bindingSha256") == BindingSha, "Unsupported or unpinned portable index.");
            var entries = Array(index, "files"); Require(entries.Count == 775 && files.Count == 775, "Incomplete or extra package inventory.");
            string previous = null; var seen = new HashSet<string>(StringComparer.Ordinal); long bytes = 0;
            foreach (var token in entries)
            {
                Require(token is JObject, "Index row must be an object."); var row = (JObject)token; Fields(row, "path", "sha256", "bytes");
                string path = Text(row, "path"), sha = Text(row, "sha256");
                Require(path == "corpus.json" || path == "binding.json" || RawPath.IsMatch(path), "Unsafe or unknown relative package path.");
                Require(ShaPattern.IsMatch(sha) && seen.Add(path) && (previous == null || StringComparer.Ordinal.Compare(previous, path) < 0), "Duplicate, unsorted or malformed index identity.");
                Require(files.TryGetValue(path, out var raw) && Long(row, "bytes") == raw.LongLength && raw.LongLength <= 8L * 1024 * 1024 && Hash(raw) == sha, "Missing or corrupted indexed bytes.");
                bytes += raw.LongLength; Require(bytes <= 256L * 1024 * 1024, "Package raw budget exceeded."); previous = path;
            }
            Require(files.Keys.All(seen.Contains) && Hash(files["corpus.json"]) == ManifestSha && Hash(files["binding.json"]) == BindingSha, "Original manifest/binding pins differ.");
            var manifest = Parse(files["corpus.json"]); VerifyManifest(manifest);
            var expected = new Dictionary<string, JObject>(StringComparer.Ordinal);
            foreach (string family in new[] { "payloadFiles", "commandFiles" }) foreach (var row in Array(manifest, family).OfType<JObject>())
            {
                Fields(row, "sha256", "file", "bytes"); string path = Text(row, "file"), sha = Text(row, "sha256");
                Require(RawPath.IsMatch(path) && path == (family == "payloadFiles" ? "payloads/" : "commands/") + sha + ".json" && ShaPattern.IsMatch(sha), "Original raw identity differs.");
                Require(!expected.ContainsKey(path), "Original raw identities repeat."); expected.Add(path, row);
            }
            Require(expected.Count == 773 && files.Keys.Where(p => p != "corpus.json" && p != "binding.json").All(expected.ContainsKey), "Portable inventory does not equal the fixed original raw inventory.");
            foreach (var item in expected) Require(files[item.Key].LongLength == Long(item.Value, "bytes") && Hash(files[item.Key]) == Text(item.Value, "sha256"), "Original raw file pin differs.");
            var binding = Parse(files["binding.json"]);
            Require(Text(binding, "sourceSha") == "8016d1e1c98cb12bc49315e116efe1a6869fb0da" && Int(binding, "exitCode") == 0 && !Bool(binding, "timedOut")
                && Bool(binding, "accepted") && Bool(binding, "complete") && Bool(binding, "sourceUnchanged") && Bool(binding, "headUnchanged")
                && Int(binding, "cases") == 166 && Int(binding, "passed") == 166 && Int(binding, "failed") == 0, "Captured execution provenance differs.");
            return manifest; // Absolute provenance strings are never converted into loader paths.
        }
        private static void ValidateMetadata(JObject index, Dictionary<string, byte[]> metadata)
        {
            var owners = Array(index, "files").OfType<JObject>().Select(r => Text(r, "path")).Concat(new[] { "portable-index.json" }).ToArray();
            Require(owners.Length == 776 && metadata.Count == 779, "Exact JSON/index/folder/root metadata inventory required.");
            var expected = new Dictionary<string, Tuple<string, bool>>(StringComparer.Ordinal);
            foreach (string owner in owners)
            {
                string path = owner + ".meta";
                Require(!expected.ContainsKey(path), "Duplicate metadata owner.");
                expected.Add(path, Tuple.Create(owner, false));
            }
            expected.Add("payloads.meta", Tuple.Create("payloads/", true));
            expected.Add("commands.meta", Tuple.Create("commands/", true));
            expected.Add(RootMeta, Tuple.Create("", true));
            Require(metadata.Keys.All(expected.ContainsKey), "Unexpected or orphan metadata, including hidden-policy metadata.");
            var guids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in expected)
            {
                string guid = MetaGuid(item.Value.Item1);
                Require(guids.Add(guid) && metadata.TryGetValue(item.Key, out var raw)
                    && raw.SequenceEqual(MetaBytes(item.Value.Item1, item.Value.Item2)), "Missing, duplicate-GUID or noncanonical metadata bytes.");
            }
            Require(guids.Count == 779, "All portable metadata identities must be unique.");
        }
        private static string MetaGuid(string relative) => Hash(Utf8.GetBytes("gamesim-frozen-v25-portable-fixture-v1|FrozenV25/" + relative)).Substring(0, 32);
        private static byte[] MetaBytes(string relative, bool folder) => Utf8.GetBytes("fileFormatVersion: 2\nguid: " + MetaGuid(relative) + "\n"
            + (folder ? "folderAsset: yes\nDefaultImporter:\n" : "TextScriptImporter:\n")
            + "  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n");
        private static void VerifyManifest(JObject m)
        {
            Require(Int(m, "schema") == 1 && Bool(m, "complete") && Int(m, "literalPayloadSchema") == 25
                && Int(m, "cases") == 166 && Int(m, "passed") == 166 && Int(m, "failed") == 0
                && Int(m, "payloadAliasCount") == 436 && Int(m, "commandRecordCount") == 706
                && Int(m, "distinctPayloadCount") == 304 && Int(m, "distinctCommandPacketCount") == 469
                && Long(m, "cumulativeRawUtf8Bytes") == 19655938 && !Bool(m, "nativeEditorOrPlayerLaunched") && !Bool(m, "ordinarySaveWritten"), "Pinned corpus counters or limits differ.");
            Require(Array(m, "payloadFiles").Count == 304 && Array(m, "commandFiles").Count == 469
                && Array(m, "caseInvocations").Count == 166 && Array(m, "payloadAliases").Count == 436 && Array(m, "commandRecords").Count == 706
                && Array(Object(m, "coverage"), "openings").Count == 12 && Array(Object(m, "coverage"), "wholeSeasons").Count == 3, "Pinned corpus sections are incomplete.");
        }
        private static string FindRoot()
        {
            if (PackageRootOverride != null)
            {
                Require(Path.IsPathRooted(PackageRootOverride), "Private fresh-harness override must be absolute.");
                string chosen = Path.GetFullPath(PackageRootOverride); NoReparse(chosen); return chosen;
            }
            var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string start in new[] { Directory.GetCurrentDirectory(), Path.GetDirectoryName(typeof(FrozenEpisodeV25CorpusTests).Assembly.Location) })
            {
                string at = start;
                for (int depth = 0; depth < 12 && !string.IsNullOrEmpty(at); depth++)
                {
                    string candidate = Path.Combine(at, RelativeRoot.Replace('/', Path.DirectorySeparatorChar));
                    if (File.Exists(Path.Combine(candidate, "portable-index.json"))) candidates.Add(Path.GetFullPath(candidate));
                    string next = Path.GetDirectoryName(at); if (next == at) break; at = next;
                }
            }
            Require(candidates.Count == 1, "Exactly one bounded portable package is required; missing/ambiguous is failure, never a skip.");
            return candidates.Single();
        }
        private static List<string> Inventory(string root, out Dictionary<string, byte[]> metadata)
        {
            NoReparse(root); var files = new List<string>(); metadata = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            var pending = new Stack<string>(); pending.Push(root); int innerCount = 0, directoryCount = 0;
            while (pending.Count > 0)
            {
                string directory = pending.Pop(); NoReparse(directory);
                foreach (string child in Directory.GetDirectories(directory))
                {
                    NoReparse(child); string relative = Relative(root, child); Require(relative == "payloads" || relative == "commands", "Unexpected package directory."); pending.Push(child); directoryCount++;
                }
                foreach (string file in Directory.GetFiles(directory))
                {
                    NoReparse(file); string relative = Relative(root, file); innerCount++;
                    if (relative.EndsWith(".meta", StringComparison.Ordinal))
                    {
                        string owner = relative.Substring(0, relative.Length - 5);
                        Require(owner == "payloads" || owner == "commands" || owner == "portable-index.json" || owner == "corpus.json" || owner == "binding.json" || RawPath.IsMatch(owner), "Orphan or unexpected Unity metadata.");
                        Require(File.Exists(file.Substring(0, file.Length - 5)) || Directory.Exists(file.Substring(0, file.Length - 5)), "Orphan Unity metadata.");
                        bool folder = owner == "payloads" || owner == "commands";
                        long expectedLength = MetaBytes(folder ? owner + "/" : owner, folder).LongLength;
                        Require(expectedLength < 4096 && new FileInfo(file).Length == expectedLength, "Noncanonical metadata length before allocation.");
                        metadata.Add(relative, ReadExact(file, expectedLength)); continue;
                    }
                    Require(relative == ".gitattributes" || relative == "portable-index.json" || relative == "corpus.json" || relative == "binding.json" || RawPath.IsMatch(relative), "Unexpected package file."); files.Add(relative);
                }
            }
            string adjacent = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + ".meta"; NoReparse(adjacent);
            long rootLength = MetaBytes("", true).LongLength;
            Require(File.Exists(adjacent) && rootLength < 4096 && new FileInfo(adjacent).Length == rootLength, "Adjacent canonical root metadata is required, not merely an index marker.");
            metadata.Add(RootMeta, ReadExact(adjacent, rootLength));
            Require(innerCount == 1555 && directoryCount == 2 && metadata.Count == 779 && files.Count == 777
                && files.Distinct(StringComparer.Ordinal).Count() == 777, "Exact1555 inner files/two folders/adjacent root metadata required."); return files;
        }
        private static string Relative(string root, string path)
        {
            string prefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string full = Path.GetFullPath(path); Require(full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase), "Package path escapes its exact root.");
            return full.Substring(prefix.Length).Replace(Path.DirectorySeparatorChar, '/');
        }
        private static void NoReparse(string path)
        {
            string at = Path.GetFullPath(path);
            while (!string.IsNullOrEmpty(at))
            {
                if (File.Exists(at) || Directory.Exists(at)) Require((File.GetAttributes(at) & FileAttributes.ReparsePoint) == 0, "Package ancestry contains a reparse point.");
                string next = Path.GetDirectoryName(at); if (next == at) break; at = next;
            }
        }
        private static void AssertPhysicalUnchanged(Package package)
        {
            Inventory(package.Root, out var metadata); ValidateMetadata(package.Index, metadata);
            Assert.That(Hash(ReadExact(Path.Combine(package.Root, "portable-index.json"), IndexByteLength)), Is.EqualTo(IndexSha));
            Assert.That(Utf8.GetString(ReadExact(Path.Combine(package.Root, ".gitattributes"), Utf8.GetByteCount(Attributes))), Is.EqualTo(Attributes));
            foreach (var file in package.Files)
                Assert.That(ReadExact(Path.Combine(package.Root, file.Key.Replace('/', Path.DirectorySeparatorChar)), file.Value.LongLength).SequenceEqual(file.Value), Is.True, "Replay may not rewrite original bytes: " + file.Key);
            foreach (var file in package.Metadata) Assert.That(metadata[file.Key].SequenceEqual(file.Value), Is.True, "Replay may not rewrite metadata: " + file.Key);
        }
        private static byte[] ReadExact(string path, long expectedLength)
        {
            NoReparse(path);
            Require(expectedLength > 0 && expectedLength <= 8L * 1024 * 1024 && new FileInfo(path).Length == expectedLength, "Pinned read length differs before allocation.");
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                Require(stream.Length == expectedLength, "Pinned open-file length differs before allocation.");
                var bytes = new byte[checked((int)expectedLength)]; int offset = 0;
                while (offset < bytes.Length)
                {
                    int read = stream.Read(bytes, offset, bytes.Length - offset);
                    Require(read > 0, "Truncated immutable file."); offset += read;
                }
                Require(stream.ReadByte() == -1, "Immutable file grew beyond its pinned read budget."); return bytes;
            }
        }
        private static void Accept(JObject payload)
        {
            string original = Json(payload); Assert.That(Int(payload, "schemaVersion"), Is.EqualTo(25));
            Assert.DoesNotThrow(() => Invoke(Contract, payload)); Assert.That(Json(payload), Is.EqualTo(original));
        }
        private static void Invoke(Type type, JObject payload)
        {
            var method = type.GetMethod("Validate", Static); Require(method != null, "Fixed contract entry is missing.");
            try { method.Invoke(null, new object[] { payload }); } catch (TargetInvocationException error) { throw error.InnerException ?? error; }
        }
        private static void CheckMode(JObject state, int mode) => Require(Int(state, "schemaVersion") == 25
            && Int(state, "unifiedCommitmentRulesVersion") == (mode == 0 ? 0 : 1) && Int(state, "unifiedHearingRulesVersion") == (mode == 2 ? 1 : 0), "Recorded mode changed; fixture2 means u1/h1.");
        private static int Mode(JObject state)
        {
            int unified = Int(state, "unifiedCommitmentRulesVersion"), hearing = Int(state, "unifiedHearingRulesVersion");
            Require((unified == 0 && hearing == 0) || (unified == 1 && (hearing == 0 || hearing == 1)), "Unsupported accepted captured mode."); return unified == 0 ? 0 : hearing == 1 ? 2 : 1;
        }
        private static void MatchCoordinates(JObject coordinate, JObject payload, string sha)
        {
            foreach (string field in new[] { "seed", "revision", "week", "phase", "sessionId" }) Assert.That(JToken.DeepEquals(coordinate[field], payload[field]), Is.True, field);
            Assert.That(Text(coordinate, "payloadSha256"), Is.EqualTo(sha));
        }
        private static JObject Rules(JObject state)
        {
            var result = new JObject(); foreach (var field in state.Properties().Where(p => p.Name.EndsWith("RulesStartWeek", StringComparison.Ordinal)
                || p.Name.EndsWith("RulesVersion", StringComparison.Ordinal))) result[field.Name] = field.Value.DeepClone();
            result["story"] = new JObject { ["rulesStartWeek"] = state["story"]["rulesStartWeek"].DeepClone(), ["rulesVersion"] = state["story"]["rulesVersion"].DeepClone() };
            result["npcSocial"] = state["npcSocial"].Type == JTokenType.Null ? JValue.CreateNull() : new JObject { ["rulesStartWeek"] = state["npcSocial"]["rulesStartWeek"].DeepClone() }; return result;
        }
        private static JObject Parse(byte[] bytes)
        {
            try
            {
                using (var text = new StringReader(Utf8.GetString(bytes))) using (var reader = new JsonTextReader(text) { DateParseHandling = DateParseHandling.None, MaxDepth = 64 })
                {
                    var token = JToken.ReadFrom(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                    Require(token is JObject && !reader.Read(), "Only one complete JSON object is accepted."); return (JObject)token;
                }
            }
            catch (Exception error) when (error is JsonException || error is DecoderFallbackException) { throw new InvalidDataException("Invalid immutable JSON bytes.", error); }
        }
        private static string Json(JToken value) => value.ToString(Formatting.None);
        private static string Hash(byte[] bytes) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }
        private static void Fields(JObject value, params string[] names) => Require(value.Properties().Count() == names.Length && names.All(n => value.Property(n) != null), "Missing or extra index fields.");
        private static int Int(JObject value, string key) => checked((int)Long(value, key));
        private static long Long(JObject value, string key) { Require(value[key]?.Type == JTokenType.Integer, "Expected literal integer: " + key); return value[key].Value<long>(); }
        private static string Text(JObject value, string key) { Require(value[key]?.Type == JTokenType.String && !string.IsNullOrWhiteSpace((string)value[key]), "Expected actual text: " + key); return (string)value[key]; }
        private static bool Bool(JObject value, string key) { Require(value[key]?.Type == JTokenType.Boolean, "Expected actual boolean: " + key); return (bool)value[key]; }
        private static JObject Object(JObject value, string key) { Require(value[key] is JObject, "Expected object: " + key); return (JObject)value[key]; }
        private static JArray Array(JObject value, string key) { Require(value[key] is JArray, "Expected array: " + key); return (JArray)value[key]; }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
    }
}
