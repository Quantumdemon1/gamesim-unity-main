using System;
using System.Globalization;
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
    /// Consumes the admitted original pre-v2 native writer bytes and observations.
    /// Expected data is never recaptured from the current reader/catalog/engine.
    /// This is retained writer compatibility and deterministic replay, not independent effect parity.
    /// </summary>
    public sealed class DialogueCampaignV1RetainedReaderTests
    {
        private const string DirectoryPrefix = "GamesimDialogueV1Retained-";
        private const string DigestAlgorithm = "SHA256/UTF8/recursively-Ordinal-sorted-object-keys/array-order-preserved/compact-json/DateParseNone/FloatParseDouble";
        private static readonly string[] Ids = { "case", "finish-personal", "answered", "neutral", "lapsed" };
        private sealed class Pin
        {
            internal readonly string name, sha;
            internal readonly int bytes;
            internal Pin(string name, int bytes, string sha) { this.name = name; this.bytes = bytes; this.sha = sha; }
        }
        // Actual raw-byte pins from the independently admitted original extraction, not future placeholders.
        private static readonly Pin[] Pins = {
            new Pin("case.primary.json", 76530, "a11bf8418b8446c41ae184bf2c6119407df717759e737821f955e151d73e198c"),
            new Pin("case.backup.json", 68513, "3c11591a815d1f789e4de3fcb4cb32c83ba71fd14b14dde94a2b6540ede7dbfe"),
            new Pin("finish-personal.primary.json", 80651, "7e261576515cb04ed785cfc45d4cff1628386f43c98bd6faeb3395c9fd021dd8"),
            new Pin("finish-personal.backup.json", 76530, "b6a78e9bad63e62ded34413c6f399225a6206512343edcdd37feff9faeedfe94"),
            new Pin("answered.primary.json", 83916, "63d612ed7abc1aa0b0779ca7bc1a5843ad797abf019948b539cdb6a9dc76e6d3"),
            new Pin("answered.backup.json", 80651, "9e64570e0e8152a52d8f26f6d89b3d8231d9463cc85398e3113a4ed5aead9529"),
            new Pin("neutral.primary.json", 81718, "297e09b007c1b1d39f4ea5f37d78b4e88b33c7a8d9040daf1e5e8c18387dfd22"),
            new Pin("neutral.backup.json", 80651, "d91711efaee8e5f5583e9b7ba37369cbe3d410e15171aae44298d2c0d11857e8"),
            new Pin("lapsed.primary.json", 82167, "1b855667fd154f9c531ecfc6bbfc7ee1a64a3465e24accd3550a2f17a008b2c6"),
            new Pin("lapsed.backup.json", 80651, "7ca988389100d8f74e881170413f181399922579853c263832cc3434d4dc593b"),
            new Pin("v1-baseline.json", 1859618, "fcc85c0f9f46fda9d98791b31eb34deac24677d10f9c69d95c96091cc78eadf1")
        };
        private static string CorpusRoot => Path.Combine(UnityEngine.Application.dataPath,
            "Gamesim", "Tests", "EditMode", "Fixtures", "DialogueV1");
        private JObject baseline;
        private string baselineImage, directory, primaryCopy, backupCopy;
        private byte[] primaryBytes, backupBytes;
        private EpisodeSaveStore observed;

        [SetUp]
        public void ReadOnlyOriginalCorpusAndOwnSeparateTemporaryObservationSlots()
        {
            baseline = null; baselineImage = null; directory = null; primaryCopy = null; backupCopy = null;
            primaryBytes = null; backupBytes = null; observed = null;
            AssertPlainAncestors(CorpusRoot);
            foreach (var pin in Pins) ReadPinned(pin);
            baseline = ParseObject(ReadPinned(Pins.Single(pin => pin.name == "v1-baseline.json")));
            Assert.That((int)baseline["schema"], Is.EqualTo(1));
            Assert.That((string)baseline["kind"], Is.EqualTo("actual-v1-writer-baseline"));
            Assert.That((int)baseline["seed"], Is.EqualTo(1));
            Assert.That((int)baseline["houseSize"], Is.EqualTo(8));
            Assert.That((int)baseline["dialogueFixtureOptIn"], Is.EqualTo(1));
            Assert.That((bool)baseline["shippingFreshWasOff"], Is.True);
            Assert.That((string)baseline["templateId"], Is.EqualTo("dialogue-campaign-v1"));
            Assert.That((int)baseline["contentVersion"], Is.EqualTo(1));
            Assert.That((string)baseline["stateDigestAlgorithm"], Is.EqualTo(DigestAlgorithm));
            Assert.That((bool)baseline["independentEffectParity"], Is.False);
            Assert.That((bool)baseline["fullSuiteAcceptance"], Is.False);
            Assert.That(((JArray)baseline["fixtures"]).Select(item => (string)item["id"]), Is.EqualTo(Ids));
            baselineImage = baseline.ToString(Formatting.None);
            directory = Path.Combine(Path.GetTempPath(), DirectoryPrefix + Guid.NewGuid().ToString("N"));
            Assert.That(Directory.Exists(directory), Is.False);
            Directory.CreateDirectory(directory);
            AssertPlainAncestors(directory);
            primaryCopy = Path.Combine(directory, "original-primary.json");
            backupCopy = Path.Combine(directory, "original-backup.json");
            observed = new EpisodeSaveStore(Path.Combine(directory, "current-observation.json"));
        }

        [TearDown]
        public void KeepCorpusAndExpectedObservationsUnchangedAndRemoveOnlyKnownOwnedFiles()
        {
            try
            {
                foreach (var pin in Pins) ReadPinned(pin);
                if (baseline != null && baselineImage != null)
                    Assert.That(baseline.ToString(Formatting.None), Is.EqualTo(baselineImage), "Never mutate retained expected observations.");
                AssertProtectedCopies();
            }
            finally
            {
                if (directory != null && Directory.Exists(directory))
                {
                    string full = Path.GetFullPath(directory);
                    string temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                        + Path.DirectorySeparatorChar;
                    Assert.That(full.StartsWith(temp, StringComparison.OrdinalIgnoreCase), Is.True);
                    Assert.That(Path.GetFileName(full), Does.StartWith(DirectoryPrefix));
                    AssertPlainAncestors(full);
                    Assert.That(Directory.GetDirectories(full), Is.Empty);
                    string[] allowed = { Path.Combine(full, "original-primary.json"), Path.Combine(full, "original-backup.json"),
                        Path.Combine(full, "current-observation.json"), Path.Combine(full, "current-observation.json.backup") };
                    string[] files = Directory.GetFiles(full);
                    Assert.That(files.All(file => allowed.Contains(file, StringComparer.OrdinalIgnoreCase)), Is.True,
                        "Unexpected files are retained; never recursively clean a diagnostic or user save root.");
                    Assert.That(files.All(file => (File.GetAttributes(file) & FileAttributes.ReparsePoint) == 0), Is.True);
                    foreach (string file in files) File.Delete(file);
                    Directory.Delete(full, false);
                }
            }
        }

        [Test]
        public void TheElevenOriginalWriterFilesAreTheFixedAdmittedBytesAndAllFiveRolesRemainPresent()
        {
            Assert.That(Directory.GetDirectories(CorpusRoot), Is.Empty);
            Assert.That(Directory.GetFiles(CorpusRoot, "*.json").Select(Path.GetFileName),
                Is.EquivalentTo(Pins.Select(pin => pin.name)), "Missing or extra JSON cannot disappear from discovery.");
            Assert.That(Pins.Select(pin => pin.name).Distinct(StringComparer.Ordinal).Count(), Is.EqualTo(11));
            foreach (string id in Ids)
            {
                var fixture = Fixture(id);
                Assert.That((string)fixture["primaryFile"], Is.EqualTo(id + ".primary.json"));
                Assert.That((string)fixture["backupFile"], Is.EqualTo(id + ".backup.json"));
                Assert.That((bool)fixture["originalPrimaryAndBackupUnchanged"], Is.True);
                foreach (string role in new[] { "primary", "backup" })
                {
                    var envelope = ParseObject(ReadPinned(Pins.Single(pin => pin.name == id + "." + role + ".json")));
                    Assert.That(JToken.DeepEquals(envelope["state"], fixture[role]["state"]), Is.True,
                        id + "/" + role + ": original file and original baseline must crossbind.");
                }
                var missing = MissingTransition(fixture);
                Assert.That(JToken.DeepEquals(missing["after"], fixture["primary"]), Is.True,
                    id + ": exactly one original transition must reach the original primary observation.");
            }
            Assert.That(((JArray)Fixture("case")["backup"]["state"]["dialogue"]["episodes"]), Is.Empty);
            Assert.That(((JArray)Fixture("case")["backup"]["presentations"]), Is.Empty);

            // Captured observations crossed JSON text; raw string-null JValues have not.
            string absent = null;
            var constructed = new JObject {
                ["endingId"] = absent, ["outcome"] = absent, ["emptyText"] = "",
                ["falseFlag"] = false, ["zero"] = 0, ["ordered"] = new JArray("second", "first", "second")
            };
            string constructedBefore = constructed.ToString(Formatting.None);
            var expectedWire = ParseObject(Encoding.UTF8.GetBytes(
                "{\"endingId\":null,\"outcome\":null,\"emptyText\":\"\",\"falseFlag\":false,\"zero\":0,\"ordered\":[\"second\",\"first\",\"second\"]}"));
            Assert.That(constructed["endingId"].Type, Is.EqualTo(JTokenType.String));
            Assert.That(JToken.DeepEquals(constructed, expectedWire), Is.False);
            var actualWire = ObservationWire(constructed);
            Assert.That(JToken.DeepEquals(actualWire, expectedWire), Is.True, "Keep every field, null, scalar and ordered array at the original producer boundary.");
            Assert.That(actualWire["endingId"].Type, Is.EqualTo(JTokenType.Null));
            Assert.That(actualWire["outcome"].Type, Is.EqualTo(JTokenType.Null));
            var missingNull = (JObject)actualWire.DeepClone(); missingNull.Remove("endingId");
            Assert.That(JToken.DeepEquals(missingNull, expectedWire), Is.False, "Missing is not a retained explicit null.");
            var wrongNull = (JObject)actualWire.DeepClone(); wrongNull["endingId"] = "";
            Assert.That(JToken.DeepEquals(wrongNull, expectedWire), Is.False, "An empty string is not a retained null.");
            var wrongOrder = (JObject)actualWire.DeepClone(); wrongOrder["ordered"] = new JArray("first", "second", "second");
            Assert.That(JToken.DeepEquals(wrongOrder, expectedWire), Is.False, "Ordered arrays and duplicate entries remain exact.");
            Assert.That(constructed.ToString(Formatting.None), Is.EqualTo(constructedBefore));
            Assert.That(constructed["endingId"].Type, Is.EqualTo(JTokenType.String), "Boundary measurement must not mutate its raw input.");
        }

        [TestCase("case", "primary")]
        [TestCase("case", "backup")]
        [TestCase("finish-personal", "primary")]
        [TestCase("finish-personal", "backup")]
        [TestCase("answered", "primary")]
        [TestCase("answered", "backup")]
        [TestCase("neutral", "primary")]
        [TestCase("neutral", "backup")]
        [TestCase("lapsed", "primary")]
        [TestCase("lapsed", "backup")]
        public void OriginalV1WriterFileLoadsReadsAndReplaysWithoutReinterpretingItsSavedContract(string id, string role)
        {
            var fixture = Fixture(id);
            primaryBytes = ReadPinned(Pins.Single(pin => pin.name == id + ".primary.json"));
            backupBytes = ReadPinned(Pins.Single(pin => pin.name == id + ".backup.json"));
            WriteNew(primaryCopy, primaryBytes); WriteNew(backupCopy, backupBytes);
            string selected = role == "primary" ? primaryCopy : backupCopy;
            var reader = new EpisodeSaveStore(selected);
            Assert.That(File.Exists(reader.BackupPath), Is.False, "Each role is a strict primary read, never an automatic recovery.");
            Assert.That(reader.TryLoad(out var loaded, out string message), Is.True, message);
            Valid(loaded);
            Assert.That(loaded.schemaVersion, Is.EqualTo(29));
            Assert.That(loaded.dialogueRulesVersion, Is.EqualTo(1));
            if (id == "case" && role == "backup")
            {
                Assert.That(loaded.phase, Is.EqualTo(EpisodePhase.Campaign));
                Assert.That(loaded.dialogue.episodes, Is.Empty, "The actual case backup predates paid entry.");
                Assert.That(DialogueRules.Active(loaded), Is.Null);
                Assert.That((JArray)fixture[role]["presentations"], Is.Empty);
            }
            else Assert.That(loaded.dialogue.episodes.Count, Is.EqualTo(1));
            foreach (var episode in loaded.dialogue.episodes)
            {
                Assert.That(episode.contentVersion, Is.EqualTo(1));
                Assert.That(DialogueRules.Cycle(loaded, episode).templateId, Is.EqualTo("dialogue-campaign-v1"));
            }
            AssertObservation((JObject)fixture[role], ObserveCurrent(loaded), id + "/" + role + " load");
            AssertProtectedCopies();
            var engine = new EpisodeEngine(loaded);
            if (role == "backup")
            {
                var missing = MissingTransition(fixture);
                ApplyOriginal(engine, missing, id + "/backup missing transition");
                AssertObservation((JObject)fixture["primary"], ObserveCurrent(engine.Snapshot), id + "/backup reached primary");
            }
            foreach (JObject record in (JArray)fixture["resumeAfterLoad"])
                ApplyOriginal(engine, record, id + "/" + role + " recorded resume");
            var duplicate = (JObject)fixture["idempotencyAfterResumeOrTerminalLoad"];
            var request = OriginalCommand((JObject)duplicate["command"]);
            string committed = Json(engine.Snapshot), requestBefore = Json(request);
            var refused = engine.Apply(request);
            Assert.That(refused.accepted, Is.EqualTo((bool)duplicate["accepted"]));
            Assert.That(refused.duplicate, Is.EqualTo((bool)duplicate["duplicate"]));
            Assert.That(refused.reason, Is.EqualTo((string)duplicate["reason"]));
            Assert.That(Json(refused.state), Is.EqualTo(committed));
            Assert.That(Json(engine.Snapshot), Is.EqualTo(committed));
            Assert.That(Json(request), Is.EqualTo(requestBefore));
            Valid(refused.state);
            AssertObservation((JObject)duplicate["after"], ObserveCurrent(refused.state), id + "/" + role + " duplicate refusal");
            AssertProtectedCopies();
        }

        private JObject Fixture(string id) => ((JArray)baseline["fixtures"]).Cast<JObject>()
            .Single(item => (string)item["id"] == id);

        private static JObject MissingTransition(JObject fixture)
        {
            var backup = fixture["backup"]; var primary = fixture["primary"];
            var matches = ((JArray)fixture["acceptedBranchAfterCampaign"]).Cast<JObject>().Where(record =>
                (int)record["beforeRevision"] == (int)backup["revision"]
                && (string)record["beforePhase"] == (string)backup["phase"]).ToArray();
            Assert.That(matches.Length, Is.EqualTo(1), "A backup is exactly one recorded transition behind its own primary.");
            var missing = matches[0];
            Assert.That((int)missing["command"]["expectedRevision"], Is.EqualTo((int)backup["revision"]));
            Assert.That(((EpisodePhase)(int)missing["command"]["expectedPhase"]).ToString(), Is.EqualTo((string)backup["phase"]));
            Assert.That((int)primary["revision"], Is.EqualTo((int)backup["revision"] + 1));
            Assert.That(JToken.DeepEquals(missing["after"], primary), Is.True, "Never replay the earlier already-accepted prefix.");
            return missing;
        }

        private void ApplyOriginal(EpisodeEngine engine, JObject record, string context)
        {
            var source = engine.Snapshot; Valid(source);
            Assert.That(source.revision, Is.EqualTo((int)record["beforeRevision"]), context);
            Assert.That(source.phase.ToString(), Is.EqualTo((string)record["beforePhase"]), context);
            var request = OriginalCommand((JObject)record["command"]);
            Assert.That(request.expectedRevision, Is.EqualTo(source.revision), context);
            Assert.That(request.expectedPhase, Is.EqualTo(source.phase), context);
            string before = Json(source), commandBefore = Json(request);
            var result = engine.Apply(request);
            Assert.That(result.accepted, Is.EqualTo((bool)record["accepted"]), context + ": " + result.reason);
            Assert.That(result.duplicate, Is.EqualTo((bool)record["duplicate"]), context);
            Assert.That(result.reason, Is.EqualTo((string)record["reason"]), context);
            Assert.That(result.accepted && !result.duplicate, Is.True, context);
            Assert.That(result.state.revision, Is.EqualTo(source.revision + 1), context);
            Assert.That(Json(source), Is.EqualTo(before), context);
            Assert.That(Json(request), Is.EqualTo(commandBefore), context);
            Assert.That(Json(engine.Snapshot), Is.EqualTo(Json(result.state)), context);
            Valid(result.state);
            AssertObservation((JObject)record["after"], ObserveCurrent(result.state), context);
            AssertProtectedCopies();
        }

        private static EpisodeCommand OriginalCommand(JObject data)
        {
            string[] fields = typeof(EpisodeCommand).GetFields(BindingFlags.Instance | BindingFlags.Public).Select(field => field.Name).ToArray();
            Assert.That(data.Properties().Select(property => property.Name), Is.EquivalentTo(fields));
            // Deserializing the exact retained request does not mint/replace its command ID or choices.
            using (var input = new StringReader(data.ToString(Formatting.None)))
            using (var reader = new JsonTextReader(input) { DateParseHandling = DateParseHandling.None, MaxDepth = 64 })
                return JsonSerializer.Create(new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.None,
                    MissingMemberHandling = MissingMemberHandling.Error, ObjectCreationHandling = ObjectCreationHandling.Replace,
                    DateParseHandling = DateParseHandling.None, MaxDepth = 64 }).Deserialize<EpisodeCommand>(reader);
        }

        private JObject ObserveCurrent(EpisodeState state)
        {
            Valid(state); string original = Json(state);
            byte[] oldPrimary = File.Exists(observed.SavePath) ? File.ReadAllBytes(observed.SavePath) : null;
            observed.Save(state); // Actual current writer is measured; retained expected data is never rewritten.
            byte[] primary = File.ReadAllBytes(observed.SavePath);
            byte[] backup = File.Exists(observed.BackupPath) ? File.ReadAllBytes(observed.BackupPath) : null;
            if (oldPrimary == null) Assert.That(backup, Is.Null);
            else CollectionAssert.AreEqual(oldPrimary, backup);
            Assert.That(observed.TryLoad(out var reloaded, out string message), Is.True, message);
            Assert.That(Json(reloaded), Is.EqualTo(original));
            Assert.That(Json(state), Is.EqualTo(original));
            CollectionAssert.AreEqual(primary, File.ReadAllBytes(observed.SavePath));
            if (backup != null) CollectionAssert.AreEqual(backup, File.ReadAllBytes(observed.BackupPath));
            var envelope = ParseObject(primary);
            Assert.That((string)envelope["format"], Is.EqualTo("gamesim-unity-save"));
            Assert.That((int)envelope["version"], Is.EqualTo(1));
            var wire = (JObject)envelope["state"];
            var presentations = new JArray();
            foreach (var episode in state.dialogue.episodes)
            {
                var cycle = state.storylines.Single(item => item.id == episode.id);
                var events = new JArray();
                foreach (var item in state.houseEvents.Where(row => row.cycleId == episode.id))
                    events.Add(new JObject {
                        ["id"] = item.id, ["contentId"] = item.contentId, ["resolved"] = item.resolved,
                        ["title"] = StoryText.Title(item), ["narrative"] = StoryText.Narrative(state, item),
                        ["eyebrow"] = StoryText.Eyebrow(item), ["outcome"] = item.outcome,
                        ["choices"] = new JArray(item.choices.Select(choice => new JObject {
                            ["optionId"] = choice.optionId, ["label"] = choice.label,
                            ["description"] = StoryText.Description(state, item, choice),
                            ["locked"] = choice.locked, ["costsAction"] = choice.costsAction, ["pickPerson"] = choice.pickPerson
                        }))
                    });
                presentations.Add(new JObject {
                    ["episodeId"] = episode.id, ["templateId"] = cycle.templateId, ["contentVersion"] = episode.contentVersion,
                    ["node"] = cycle.beatId, ["status"] = cycle.status, ["endingId"] = cycle.endingId,
                    ["responseKeys"] = new JArray(episode.responses.Select(response => response.responseKey)), ["events"] = events
                });
            }
            return ObservationWire(new JObject {
                ["revision"] = state.revision, ["phase"] = state.phase.ToString(), ["week"] = state.week,
                ["mainRng"] = state.randomState, ["npcRng"] = state.npcSocial.randomState,
                ["remainingAp"] = EpisodeEngine.SocialActionBudget(state) - EpisodeEngine.SocialActionsSpent(state),
                ["windowActions"] = new JArray(state.windowActions), ["socialActions"] = state.socialActions,
                ["outOfPhaseSocialActions"] = state.outOfPhaseSocialActions, ["boughtActionPoints"] = state.boughtActionPoints,
                ["moveInExtrasSpent"] = state.moveInExtrasSpent, ["stateSha256"] = Sha(Encoding.UTF8.GetBytes(Canonical(wire))),
                ["state"] = wire.DeepClone(), ["presentations"] = presentations
            });
        }

        // The original writer serialized its entire baseline before the consumer parsed it.
        // Measure the whole current observation at that same boundary; never transform expected data.
        private static JObject ObservationWire(JObject actual) =>
            ParseObject(Encoding.UTF8.GetBytes(actual.ToString(Formatting.None)));

        private static void AssertObservation(JObject expected, JObject actual, string context)
        {
            Assert.That((string)actual["stateSha256"], Is.EqualTo((string)expected["stateSha256"]), context + ": complete saved-state digest");
            Assert.That(JToken.DeepEquals(actual, expected), Is.True, context + ": complete state, presentation, costs and both RNG streams");
        }
        private void AssertProtectedCopies()
        {
            if (primaryBytes != null) CollectionAssert.AreEqual(primaryBytes, File.ReadAllBytes(primaryCopy));
            if (backupBytes != null) CollectionAssert.AreEqual(backupBytes, File.ReadAllBytes(backupCopy));
        }
        private static byte[] ReadPinned(Pin pin)
        {
            string path = Path.Combine(CorpusRoot, pin.name);
            Assert.That((File.GetAttributes(path) & FileAttributes.ReparsePoint) == 0, Is.True);
            Assert.That(new FileInfo(path).Length, Is.EqualTo(pin.bytes), pin.name);
            byte[] bytes = File.ReadAllBytes(path);
            Assert.That(bytes.Length, Is.EqualTo(pin.bytes), pin.name);
            Assert.That(Sha(bytes), Is.EqualTo(pin.sha), pin.name + ": retained original bytes must never be normalized or resealed.");
            return bytes;
        }
        private static void WriteNew(string path, byte[] bytes)
        {
            using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
            CollectionAssert.AreEqual(bytes, File.ReadAllBytes(path));
        }
        private static JObject ParseObject(byte[] bytes)
        {
            Assert.That(bytes.Length, Is.InRange(1, 8 * 1024 * 1024));
            using (var input = new StringReader(new UTF8Encoding(false, true).GetString(bytes)))
            using (var reader = new JsonTextReader(input) { DateParseHandling = DateParseHandling.None, FloatParseHandling = FloatParseHandling.Double, MaxDepth = 64 })
            {
                var token = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                Assert.That(reader.Read(), Is.False); return token;
            }
        }
        private static string Canonical(JToken token) => Ordered(token).ToString(Formatting.None);
        private static JToken Ordered(JToken token)
        {
            if (token is JObject obj) return new JObject(obj.Properties().OrderBy(property => property.Name, StringComparer.Ordinal)
                .Select(property => new JProperty(property.Name, Ordered(property.Value))));
            if (token is JArray array) return new JArray(array.Select(Ordered));
            return token.DeepClone();
        }
        private static string Sha(byte[] bytes)
        {
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }
        private static string Json(object value) => JsonConvert.SerializeObject(value, Formatting.None);
        private static void Valid(EpisodeState state) => Assert.That(EpisodeValidation.TryValidate(state, out string error), Is.True, error);
        private static void AssertPlainAncestors(string path)
        {
            for (string at = Path.GetFullPath(path); !string.IsNullOrEmpty(at); at = Path.GetDirectoryName(at))
                if (Directory.Exists(at) || File.Exists(at))
                    Assert.That((File.GetAttributes(at) & FileAttributes.ReparsePoint) == 0, Is.True, "Fixture/temp ancestry must not redirect.");
        }
    }
}
