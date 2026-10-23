using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using Gamesim.Persistence;
using Gamesim.Simulation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Native actual migration/store boundary only. Historical input is retained writer JSON,
    /// never current DTOs relabelled as schema28. Inert29 controls are literal migration
    /// expectations, not newly played/writer-authenticated dialogue histories.
    /// </summary>
    public sealed class PersistenceV29MigrationTests
    {
        private const string EdgeReason = "The all-week rules' records are only ever between two houseguests.";
        private const string ActivationReason = "A season without the all-week rules has none of their records.";
        private static string FixtureRoot => Path.Combine(UnityEngine.Application.dataPath,
            "Gamesim", "Tests", "EditMode", "Fixtures", "OldSaves");
        private Dictionary<string, byte[]> corpus;

        [OneTimeSetUp]
        public void RetainTheReadOnlyCorpusImage()
        {
            corpus = Directory.GetFiles(FixtureRoot, "*.json")
                .ToDictionary(Path.GetFileName, File.ReadAllBytes, StringComparer.Ordinal);
            Assert.That(corpus, Is.Not.Empty);
        }

        [OneTimeTearDown]
        public void EveryOriginalCorpusFileStillHasItsExactBytes()
        {
            if (corpus == null) return;
            var paths = Directory.GetFiles(FixtureRoot, "*.json");
            Assert.That(paths.Select(Path.GetFileName), Is.EquivalentTo(corpus.Keys));
            foreach (string path in paths)
                Assert.That(File.ReadAllBytes(path), Is.EqualTo(corpus[Path.GetFileName(path)]), Path.GetFileName(path));
        }

        [TestCase("s28-default-w1-social", 0)]
        [TestCase("s28-mode1-allweek-w2-campaign", 1)]
        [TestCase("s28-mode1-finished", 1)]
        [TestCase("s28-mode2-allweek-w3-campaign", 2)]
        [TestCase("s28-mode2-finished", 2)]
        public void Original28BecomesOnlyTheReviewedInert29LiteralsAndLoadsReadOnly(string id, int mode)
        {
            var original = Fixture(id);
            var before = new TokenImage(original);
            Frozen(28, original);
            Assert.That((int)original["unifiedCommitmentRulesVersion"], Is.EqualTo(mode));
            if (mode != 0) Assert.That((JArray)original["unifiedCommitments"], Is.Not.Empty);
            if (mode == 2)
            {
                var rows = ((JArray)original["unifiedCommitments"]).Cast<JObject>().ToArray();
                Assert.That(rows.Any(row => (string)row["kind"] == "safety"), Is.True);
                Assert.That(rows.Any(row => (string)row["kind"] == "vote"), Is.True);
                Assert.That((JArray)original["unifiedVoteReveals"], Is.Not.Empty);
            }
            var expected = Inert29(original);
            var actual = EpisodeSaveMigrations.PrepareCurrentPayload(original, out bool migrated);
            Assert.That(migrated, Is.True, "A real 28->29 migration, not detached current28 dispatch.");
            ExactInertStep(original, actual);
            Assert.That(Text(actual), Is.EqualTo(Text(expected)));
            AssertNoSharedTokens(original, actual);
            var beforeFutureReader = new TokenImage(actual);
            Assert.That(Assert.Throws<InvalidDataException>(() => Frozen(28, actual)).Message, Is.Not.Empty,
                "A historical reader never accepts the new current wire contract.");
            beforeFutureReader.AssertUnchanged(actual);
            using var slot = new Slot();
            byte[] source = corpus[id + ".save.json"];
            slot.Write(source);
            Assert.That(slot.Store.TryLoad(out var loaded, out string message), Is.True, message);
            var loadedPayload = Payload(loaded);
            Assert.That(JToken.DeepEquals(loadedPayload, expected), Is.True,
                id + ": the actual serialized SaveJson wire payload must equal the complete retained migrated wire.");
            slot.Unchanged(source);
            ((JArray)actual["dialogue"]["episodes"]).Add(new JObject { ["caller-only"] = true });
            ((JObject)actual["npcSocial"])["beatWeek"] = -123;
            before.AssertUnchanged(original);
        }

        [TestCase("s27-default-w2-social")]
        [TestCase("s27-mode1-w3-social")]
        public void OlderChainMustReachValidFrozen28BeforeTheSameInertStep(string id)
        {
            var original = Fixture(id);
            var before = new TokenImage(original);
            Frozen(27, original);
            var fixed28 = EpisodeSaveMigrations.UpgradeV27ToV28(original);
            Frozen(28, fixed28);
            var intermediate = new TokenImage(fixed28);
            var actual = EpisodeSaveMigrations.PrepareCurrentPayload(original, out bool migrated);
            Assert.That(migrated, Is.True);
            ExactInertStep(fixed28, actual);
            intermediate.AssertUnchanged(fixed28);
            before.AssertUnchanged(original);
        }

        [TestCase("missing-historical-field")]
        [TestCase("unknown-historical-field")]
        [TestCase("preexisting-dialogue-rule")]
        [TestCase("preexisting-dialogue-storage")]
        [TestCase("preexisting-conditional-null")]
        [TestCase("player-edge")]
        public void Direct28MustPassItsOriginalFrozenReaderBeforeDefaults(string defect)
        {
            var valid = Fixture("s28-mode2-allweek-w3-campaign");
            Frozen(28, valid);
            var control = EpisodeSaveMigrations.PrepareCurrentPayload(valid, out bool controlMigrated);
            Assert.That(controlMigrated, Is.True);
            ExactInertStep(valid, control);
            var bad = (JObject)valid.DeepClone();
            switch (defect)
            {
                case "missing-historical-field": bad.Remove("allWeekRulesStartWeek"); break;
                case "unknown-historical-field": ((JObject)bad["npcSocial"]).Add("futureDialogueBudget", 0); break;
                case "preexisting-dialogue-rule": bad.Add("dialogueRulesVersion", 0); break;
                case "preexisting-dialogue-storage": bad.Add("dialogue", EmptyDialogue()); break;
                case "preexisting-conditional-null": ((JObject)bad["unifiedCommitments"][0]).Add("conditionalVeto", JValue.CreateNull()); break;
                default: AddNpcRecord(bad, true); break;
            }
            var before = new TokenImage(bad);
            var historical = Assert.Throws<InvalidDataException>(() => Frozen(28, bad));
            bool migrated = true;
            var refused = Assert.Throws<InvalidDataException>(() => EpisodeSaveMigrations.PrepareCurrentPayload(bad, out migrated));
            Assert.That(refused.Message, Is.EqualTo(historical.Message),
                "The original historical refusal is not replaced by repair/current defaults.");
            Assert.That(migrated, Is.False);
            if (defect == "player-edge") Assert.That(refused.Message, Is.EqualTo(EdgeReason));
            before.AssertUnchanged(bad);
        }

        [Test]
        public void A27ValidRecordCannotSkipTheIntermediate28ActivationGuard()
        {
            var original = Fixture("s27-mode1-w3-social");
            Frozen(27, original);
            AddNpcRecord(original, false);
            Frozen(27, original);
            var before = new TokenImage(original);
            var stepped = EpisodeSaveMigrations.UpgradeV27ToV28(original);
            Assert.That(Assert.Throws<InvalidDataException>(() => Frozen(28, stepped)).Message, Is.EqualTo(ActivationReason));
            bool migrated = true;
            var refused = Assert.Throws<InvalidDataException>(() => EpisodeSaveMigrations.PrepareCurrentPayload(original, out migrated));
            Assert.That(refused.Message, Is.EqualTo(ActivationReason));
            Assert.That(migrated, Is.False);
            before.AssertUnchanged(original);
        }

        [TestCase("s28-default-w1-social")]
        [TestCase("s28-mode1-allweek-w2-campaign")]
        [TestCase("s28-mode2-allweek-w3-campaign")]
        public void Direct29OffControlsLoadWithoutEnrollmentOrMigration(string id)
        {
            var historical = Fixture(id);
            Frozen(28, historical);
            var current = Inert29(historical);
            var before = new TokenImage(current);
            var detached = EpisodeSaveMigrations.PrepareCurrentPayload(current, out bool migrated);
            Assert.That(migrated, Is.False);
            Assert.That(Text(detached), Is.EqualTo(Text(current)));
            AssertNoSharedTokens(current, detached);
            using var slot = new Slot();
            byte[] bytes = Envelope(current);
            slot.Write(bytes);
            Assert.That(slot.Store.TryLoad(out var loaded, out string message), Is.True, message);
            Assert.That(JToken.DeepEquals(Payload(loaded), current), Is.True);
            Assert.That((int)Payload(loaded)["dialogueRulesVersion"], Is.Zero);
            slot.Unchanged(bytes);
            before.AssertUnchanged(current);
        }

        [TestCase("missing-rule")] [TestCase("null-rule")] [TestCase("text-rule")]
        [TestCase("unknown-rule")] [TestCase("negative-rule")]
        [TestCase("missing-dialogue")] [TestCase("null-dialogue")] [TestCase("extra-dialogue")]
        [TestCase("missing-episodes")] [TestCase("null-episodes")]
        [TestCase("missing-grants")] [TestCase("null-grants")]
        [TestCase("missing-vetoOccasions")] [TestCase("null-vetoOccasions")]
        [TestCase("missing-conditional")] [TestCase("non-null-conditional")]
        [TestCase("extra-canonical")] [TestCase("extra-root")]
        public void ActualCurrentStoreRefusesMalformed29WithoutRepairOrChangingFiles(string defect)
        {
            var current = Inert29(Fixture("s28-mode2-allweek-w3-campaign"));
            using var slot = new Slot();
            byte[] good = Envelope(current); slot.Write(good);
            Assert.That(slot.Store.TryLoad(out var control, out string message), Is.True, message);
            Assert.That(JToken.DeepEquals(Payload(control), current), Is.True);
            slot.Unchanged(good);
            var bad = (JObject)current.DeepClone();
            var dialogue = (JObject)bad["dialogue"];
            var canonical = (JObject)bad["unifiedCommitments"][0];
            switch (defect)
            {
                case "missing-rule": bad.Remove("dialogueRulesVersion"); break;
                case "null-rule": bad["dialogueRulesVersion"] = JValue.CreateNull(); break;
                case "text-rule": bad["dialogueRulesVersion"] = "0"; break;
                case "unknown-rule": bad["dialogueRulesVersion"] = 2; break;
                case "negative-rule": bad["dialogueRulesVersion"] = -1; break;
                case "missing-dialogue": bad.Remove("dialogue"); break;
                case "null-dialogue": bad["dialogue"] = JValue.CreateNull(); break;
                case "extra-dialogue": dialogue.Add("freeActionWallet", 0); break;
                case "missing-conditional": canonical.Remove("conditionalVeto"); break;
                case "non-null-conditional":
                    canonical["conditionalVeto"] = JObject.Parse("{'statementId':null,'consentEventId':null,'consentPathIndex':0,'consentRevision':0,'resolution':null}");
                    break;
                case "extra-canonical": canonical.Add("conditionalExpression", JValue.CreateNull()); break;
                case "extra-root": bad.Add("dialogueFutureVersion", 0); break;
                default:
                    string field = defect.Substring(defect.IndexOf('-') + 1);
                    if (defect.StartsWith("missing-", StringComparison.Ordinal)) dialogue.Remove(field);
                    else dialogue[field] = JValue.CreateNull();
                    break;
            }
            var before = new TokenImage(bad);
            byte[] bytes = Envelope(bad); slot.Write(bytes);
            for (int attempt = 0; attempt < 2; attempt++)
            {
                Assert.That(slot.Store.TryLoad(out var refused, out message), Is.False, defect);
                Assert.That(refused, Is.Null);
                Assert.That(message, Is.Not.Null.And.Not.Empty);
                Assert.That(message, Does.Not.Contain("checksum"),
                    "An independently checksummed current payload must reach the current shape/semantic boundary.");
                slot.Unchanged(bytes); before.AssertUnchanged(bad);
            }
        }

        [TestCase("s28-mode1-finished")]
        [TestCase("s28-mode2-finished")]
        public void ActualSaveJsonWireComparisonChecksEveryScalarAndNeverMasksAChangedValue(string id)
        {
            var expected = Inert29(Fixture(id));
            using var slot = new Slot();
            byte[] source = corpus[id + ".save.json"]; slot.Write(source);
            Assert.That(slot.Store.TryLoad(out var loaded, out string message), Is.True, message);
            var raw = RawPayload(loaded);
            var wire = Payload(loaded);
            Assert.That(raw["contestants"][0]["appearance"]["dna"].Any(), Is.True);
            Assert.That(((JValue)raw["contestants"][0]["appearance"]["dna"][1]["value"]).Value, Is.TypeOf<float>(),
                "The actual saved appearance DTO owns Single, not an invented all-double DTO.");
            Assert.That(JToken.DeepEquals(raw, expected), Is.False, "Retain the diagnosed in-memory boxing distinction.");
            Assert.That(Text(wire), Is.EqualTo(Text(raw)), "Crossing the real wire reader changes no serialized value or order.");
            Assert.That(JToken.DeepEquals(wire, expected), Is.True, "The full object is compared, not a DNA-only view.");

            var probe = (JObject)wire.DeepClone();
            var scalars = probe.DescendantsAndSelf().OfType<JValue>().ToArray();
            Assert.That(scalars.Length, Is.GreaterThan(100));
            foreach (var scalar in scalars)
            {
                object saved = scalar.Value;
                switch (scalar.Type)
                {
                    case JTokenType.Integer: scalar.Value = Convert.ToInt64(saved) + 1L; break;
                    case JTokenType.Float: scalar.Value = Convert.ToDouble(saved) == 0.0 ? 1.0 : -Convert.ToDouble(saved); break;
                    case JTokenType.String: scalar.Value = (string)saved + "-changed"; break;
                    case JTokenType.Boolean: scalar.Value = !(bool)saved; break;
                    case JTokenType.Null: scalar.Value = "changed-from-null"; break;
                    default: Assert.Fail("Unreviewed scalar kind in the actual wire witness: " + scalar.Type); break;
                }
                Assert.That(JToken.DeepEquals(probe, expected), Is.False,
                    id + ": the full strict comparison must notice a changed value at " + scalar.Path);
                scalar.Value = saved;
            }
            Assert.That(JToken.DeepEquals(probe, expected), Is.True, "Every exact scalar value was restored.");
            probe.Add("unknown-wire-field", 0);
            Assert.That(JToken.DeepEquals(probe, expected), Is.False, "An extra field is not ignored.");
            probe.Remove("unknown-wire-field");
            probe.Remove("randomState");
            Assert.That(JToken.DeepEquals(probe, expected), Is.False, "A missing non-DNA field is not ignored.");
            var reversed = (JObject)wire.DeepClone();
            var people = (JArray)reversed["contestants"];
            var first = people[0].DeepClone(); people[0] = people[1].DeepClone(); people[1] = first;
            Assert.That(JToken.DeepEquals(reversed, expected), Is.False, "Array order remains part of the whole-wire oracle.");
            slot.Unchanged(source);
            Assert.That(Text(Payload(loaded)), Is.EqualTo(Text(wire)), "Probing detached JSON never changes the loaded state.");
        }

        private static JObject Fixture(string id) =>
            (JObject)JObject.Parse(File.ReadAllText(Path.Combine(FixtureRoot, id + ".save.json")))["state"];
        private static JObject EmptyDialogue() => new JObject
        { ["episodes"] = new JArray(), ["grants"] = new JArray(), ["vetoOccasions"] = new JArray() };
        private static JObject Inert29(JObject original)
        {
            Assert.That((int)original["schemaVersion"], Is.EqualTo(28));
            var result = (JObject)original.DeepClone();
            result.Add("dialogueRulesVersion", 0);
            result.Add("dialogue", EmptyDialogue());
            foreach (JObject row in (JArray)result["unifiedCommitments"]) row.Add("conditionalVeto", JValue.CreateNull());
            result["schemaVersion"] = 29;
            return result;
        }
        private static void ExactInertStep(JObject original, JObject actual)
        {
            Assert.That((int)actual["schemaVersion"], Is.EqualTo(29));
            Assert.That(actual["dialogueRulesVersion"]?.Type, Is.EqualTo(JTokenType.Integer));
            Assert.That((int)actual["dialogueRulesVersion"], Is.Zero);
            Assert.That(Text(actual["dialogue"]), Is.EqualTo(Text(EmptyDialogue())));
            var reverse = (JObject)actual.DeepClone();
            reverse.Remove("dialogueRulesVersion"); reverse.Remove("dialogue");
            foreach (JObject row in (JArray)reverse["unifiedCommitments"])
            {
                Assert.That(row.Property("conditionalVeto"), Is.Not.Null);
                Assert.That(row["conditionalVeto"].Type, Is.EqualTo(JTokenType.Null));
                row.Remove("conditionalVeto");
            }
            reverse["schemaVersion"] = 28;
            Assert.That(Text(reverse), Is.EqualTo(Text(original)),
                "Every old token, property/array order, rule, RNG, history, identity and scalar survives exactly.");
        }
        private static void AddNpcRecord(JObject payload, bool playerEdge)
        {
            string player = (string)payload["playerId"];
            var edge = ((JArray)payload["relationships"]).Cast<JObject>().First(row => playerEdge
                ? (string)row["fromId"] == player
                : (string)row["fromId"] != player && (string)row["toId"] != player);
            int sequence = (int)payload["nextSequence"];
            ((JArray)edge["events"]).Add(new JObject
            {
                ["sequence"] = sequence, ["week"] = 1, ["type"] = "npc-conversation",
                ["description"] = "Detached historical admission control", ["impactScore"] = 25.0, ["decayable"] = true
            });
            payload["nextSequence"] = sequence + 1;
        }
        private static void Frozen(int version, JObject original)
        {
            var type = typeof(EpisodeSaveStore).Assembly.GetType("Gamesim.Persistence.FrozenEpisodeV" + version, true);
            var method = type.GetMethod("Validate", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);
            Invoke(method, null, new object[] { original });
        }
        private static object Invoke(MethodInfo method, object target, object[] arguments)
        {
            try { return method.Invoke(target, arguments); }
            catch (TargetInvocationException error) when (error.InnerException != null)
            { ExceptionDispatchInfo.Capture(error.InnerException).Throw(); throw; }
        }
        // The real writer first makes a public-field JObject, then writes JSON text; the
        // real loader reads that text with SaveJson.ParseObject. A bare FromObject retains
        // CLR Single boxes that cannot be compared to parsed Double boxes as a wire oracle.
        private static JObject RawPayload(EpisodeState state)
        {
            var api = typeof(EpisodeSaveStore).Assembly.GetType("Gamesim.Persistence.SaveJson", true);
            var serializer = (JsonSerializer)Invoke(api.GetMethod("Serializer", BindingFlags.Public | BindingFlags.Static), null, null);
            return JObject.FromObject(state, serializer);
        }
        private static JObject Payload(EpisodeState state)
        {
            var api = typeof(EpisodeSaveStore).Assembly.GetType("Gamesim.Persistence.SaveJson", true);
            return (JObject)Invoke(api.GetMethod("ParseObject", BindingFlags.Public | BindingFlags.Static),
                null, new object[] { Text(RawPayload(state)) });
        }
        private static byte[] Envelope(JObject payload) => Encoding.UTF8.GetBytes(PersistenceMigrationTests.Envelope(payload));
        private static string Text(JToken value) => value.ToString(Formatting.None);
        private static void AssertNoSharedTokens(JObject original, JObject candidate)
        {
            var held = new HashSet<JToken>(original.DescendantsAndSelf(), TokenReferenceComparer.Instance);
            Assert.That(candidate.DescendantsAndSelf().Any(held.Contains), Is.False);
        }
        private sealed class TokenReferenceComparer : IEqualityComparer<JToken>
        {
            internal static readonly TokenReferenceComparer Instance = new TokenReferenceComparer();
            public bool Equals(JToken left, JToken right) => ReferenceEquals(left, right);
            public int GetHashCode(JToken token) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(token);
        }
        private sealed class TokenImage
        {
            private readonly string text;
            private readonly JToken[] nodes, parents, previous, next;
            internal TokenImage(JObject root)
            {
                text = Text(root); nodes = root.DescendantsAndSelf().ToArray();
                parents = nodes.Select(t => (JToken)t.Parent).ToArray();
                previous = nodes.Select(t => t.Previous).ToArray(); next = nodes.Select(t => t.Next).ToArray();
            }
            internal void AssertUnchanged(JObject root)
            {
                Assert.That(Text(root), Is.EqualTo(text));
                var current = root.DescendantsAndSelf().ToArray();
                Assert.That(current.Length, Is.EqualTo(nodes.Length));
                for (int i = 0; i < current.Length; i++)
                {
                    Assert.That(current[i], Is.SameAs(nodes[i]));
                    Assert.That(current[i].Parent, Is.SameAs(parents[i]));
                    Assert.That(current[i].Previous, Is.SameAs(previous[i]));
                    Assert.That(current[i].Next, Is.SameAs(next[i]));
                }
            }
        }
        private sealed class Slot : IDisposable
        {
            private readonly string leaf = "GamesimR1Shape-" + Guid.NewGuid().ToString("N");
            private readonly string root;
            internal readonly EpisodeSaveStore Store;
            internal Slot()
            {
                root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), leaf));
                Guard(); Assert.That(Directory.Exists(root) || File.Exists(root), Is.False);
                Directory.CreateDirectory(root); Store = new EpisodeSaveStore(Path.Combine(root, "episode.json"));
            }
            internal void Write(byte[] bytes) { Guard(); File.WriteAllBytes(Store.SavePath, bytes); }
            internal void Unchanged(byte[] bytes)
            {
                Guard();
                Assert.That(Directory.GetFiles(root).Select(Path.GetFileName), Is.EquivalentTo(new[] { "episode.json" }),
                    "Loading/refusal creates no backup, pending file or preservation artifact.");
                Assert.That(File.ReadAllBytes(Store.SavePath), Is.EqualTo(bytes));
            }
            private void Guard()
            {
                string temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                Assert.That(Path.GetDirectoryName(root), Is.EqualTo(temp));
                Assert.That(Path.GetFileName(root), Is.EqualTo(leaf));
                for (string at = root; !string.IsNullOrEmpty(at); at = Path.GetDirectoryName(at))
                    if (Directory.Exists(at) || File.Exists(at))
                        Assert.That(File.GetAttributes(at) & FileAttributes.ReparsePoint, Is.EqualTo((FileAttributes)0));
                if (!Directory.Exists(root)) return;
                Assert.That(Directory.GetDirectories(root), Is.Empty);
                foreach (string file in Directory.GetFiles(root))
                    Assert.That(File.GetAttributes(file) & FileAttributes.ReparsePoint, Is.EqualTo((FileAttributes)0));
            }
            public void Dispose()
            {
                Guard();
                if (!Directory.Exists(root)) return;
                foreach (string file in Directory.GetFiles(root)) File.Delete(file);
                Directory.Delete(root);
            }
        }
    }
}
