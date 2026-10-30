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
    /// SCH30's step to the pooled schema 30 (ALL-STAR-HISTORIES-PLAN 8.4): UpgradeV29ToV30 holds its input to the frozen
    /// schema-29 whole reader before any clone or literal, then adds only the pooled off literals, in their declared
    /// order after the dialogue store; PrepareV29Payload is the frozen dispatch to 29; and the real store loads every
    /// schema-29 writer's file read-only, saves it at 30, reloads it unmigrated, and refuses a 30 that holds a pooled value
    /// or a future schema without changing a file. Retained fixtures are read-only; every edit is a detached control.
    /// </summary>
    public sealed class PersistenceV30MigrationTests
    {
        private static readonly string[] Pooled = { "alumniRulesVersion", "alumniCatalogEdition", "tuningRulesVersion" };
        private static readonly string[] Writers29 =
        {
            "s29-default-w2-social", "s29-mode1-allweek-w3-veto-meeting", "s29-mode2-allweek-w3-campaign",
            // Dialogue D1's writer (36882bb8), on dialogue rules 1 with a conversation open: it steps to 30 as the others do.
            "s29-mode2-dialogue1-w2-campaign",
        };
        private static string FixtureRoot => Path.Combine(UnityEngine.Application.dataPath,
            "Gamesim", "Tests", "EditMode", "Fixtures", "OldSaves");

        [TestCaseSource(nameof(Writers29))]
        public void TheStepAddsOnlyThePooledOffLiteralsInTheirDeclaredOrder(string id)
        {
            var original = Fixture(id);
            string before = Text(original);
            var next = EpisodeSaveMigrations.UpgradeV29ToV30(original);
            Assert.That(Text(original), Is.EqualTo(before), "The original is never edited.");
            Assert.That((int)next["schemaVersion"], Is.EqualTo(30));
            var names = next.Properties().Select(p => p.Name).ToArray();
            Assert.That(names.Skip(names.Length - 4), Is.EqualTo(new[] { "dialogue" }.Concat(Pooled)),
                "Appended after the dialogue store, in the declared order.");
            Assert.That(next["alumniRulesVersion"].Type, Is.EqualTo(JTokenType.Integer));
            Assert.That((int)next["alumniRulesVersion"], Is.Zero);
            Assert.That(next["alumniCatalogEdition"].Type, Is.EqualTo(JTokenType.Null));
            Assert.That(next["tuningRulesVersion"].Type, Is.EqualTo(JTokenType.Integer));
            Assert.That((int)next["tuningRulesVersion"], Is.Zero);
            var inverse = (JObject)next.DeepClone();
            foreach (string name in Pooled) inverse.Remove(name);
            inverse["schemaVersion"] = 29;
            Assert.That(Text(inverse), Is.EqualTo(before), "Every old token, property order, rule, RNG and history survives exactly.");
            var originals = new HashSet<JToken>(original.DescendantsAndSelf(), new Identity());
            Assert.That(next.DescendantsAndSelf().Any(token => originals.Contains(token)), Is.False, "A detached result.");
            // The live 30 shape and saved semantics take it as it is.
            var state = next.ToObject<EpisodeState>(Serializer());
            Assert.DoesNotThrow(() => EpisodeSaveValidation.Validate(state));
            Assert.That(Text(Payload(state)), Is.EqualTo(Text(next)), "Today's serializer writes the step's exact tokens.");
        }

        [TestCase("null-input")] [TestCase("missing-header")] [TestCase("text-header")] [TestCase("fraction-header")]
        [TestCase("old-header")] [TestCase("current-header")] [TestCase("future-header")]
        [TestCase("preexisting-alumni")] [TestCase("preexisting-edition")] [TestCase("preexisting-tuning")]
        [TestCase("unknown-root")] [TestCase("dialogue-rules-2")] [TestCase("story-rules-10")] [TestCase("missing-dialogue")]
        public void TheStepRefusesAnythingButAWholeLawfulTwentyNineBeforeAnyLiteral(string defect)
        {
            var bad = defect == "null-input" ? null : Fixture("s29-mode2-allweek-w3-campaign");
            switch (defect)
            {
                case "missing-header": bad.Remove("schemaVersion"); break;
                case "text-header": bad["schemaVersion"] = "29"; break;
                case "fraction-header": bad["schemaVersion"] = 29.0; break;
                case "old-header": bad["schemaVersion"] = 28; break;
                case "current-header": bad["schemaVersion"] = 30; break;
                case "future-header": bad["schemaVersion"] = 31; break;
                case "preexisting-alumni": bad.Add("alumniRulesVersion", 0); break;
                case "preexisting-edition": bad.Add("alumniCatalogEdition", JValue.CreateNull()); break;
                case "preexisting-tuning": bad.Add("tuningRulesVersion", 0); break;
                case "unknown-root": bad.Add("alumniHistory", new JArray()); break;
                case "dialogue-rules-2": bad["dialogueRulesVersion"] = 2; break;
                case "story-rules-10": bad["story"]["rulesVersion"] = 10; break;
                case "missing-dialogue": bad.Remove("dialogue"); break;
            }
            string before = bad == null ? null : Text(bad);
            for (int attempt = 0; attempt < 2; attempt++)
            {
                Assert.Throws<InvalidDataException>(() => EpisodeSaveMigrations.UpgradeV29ToV30(bad), defect);
                if (bad != null) Assert.That(Text(bad), Is.EqualTo(before), "No partial literal or repair on refusal.");
            }
        }

        [TestCaseSource(nameof(Writers29))]
        public void TheFrozenDispatchToTwentyNineDetachesADirect29AndRefusesTheLiveSchema(string id)
        {
            var original = Fixture(id);
            var again = EpisodeSaveMigrations.PrepareV29Payload(original, out bool migrated);
            Assert.That(migrated, Is.False);
            Assert.That(again, Is.Not.SameAs(original));
            Assert.That(Text(again), Is.EqualTo(Text(original)));
            var current = EpisodeSaveMigrations.UpgradeV29ToV30(original);
            Assert.Throws<InvalidDataException>(() => EpisodeSaveMigrations.PrepareV29Payload(current, out _), "Thirty is above 29's dispatch.");
            var older = Fixture("s28-mode2-allweek-w3-campaign");
            var stepped = EpisodeSaveMigrations.PrepareV29Payload(older, out migrated);
            Assert.That(migrated, Is.True);
            Assert.That(Text(stepped), Is.EqualTo(Text(EpisodeSaveMigrations.UpgradeV28ToV29(older))));
        }

        [TestCaseSource(nameof(Writers29))]
        public void ASchemaTwentyNineSaveLoadsReadOnlySavesAtThirtyAndReloadsUnmigrated(string id)
        {
            byte[] source = FixtureBytes(id);
            using var slot = new Slot();
            slot.Write(slot.Store.SavePath, source);
            Assert.That(slot.Store.TryLoad(out var loaded, out string message), Is.True, message);
            Assert.That(message, Does.Contain("Schema 29").And.Contain("schema 30 in memory"));
            Assert.That(File.ReadAllBytes(slot.Store.SavePath), Is.EqualTo(source), "Loading is read-only.");
            Assert.That(Text(Payload(loaded)), Is.EqualTo(Text(EpisodeSaveMigrations.UpgradeV29ToV30(Fixture(id)))));
            slot.Store.Save(loaded);
            Assert.That(File.ReadAllBytes(slot.Store.BackupPath), Is.EqualTo(source), "Rotation keeps the 29 writer's envelope.");
            var saved = JObject.Parse(File.ReadAllText(slot.Store.SavePath));
            Assert.That((int)saved["state"]["schemaVersion"], Is.EqualTo(30));
            Assert.That(slot.Store.TryLoad(out var again, out message), Is.True, message);
            Assert.That(message ?? "", Does.Not.Contain("migrated"));
            Assert.That(Text(Payload(again)), Is.EqualTo(Text(Payload(loaded))));
            Assert.That(slot.Store.TryRecoverBackup(out var recovered, out message), Is.True, message);
            Assert.That(Text(Payload(recovered)), Is.EqualTo(Text(Payload(loaded))), "The 29 backup recovers to the same 30 season.");
        }

        // From H1b the alumni rules define 1, with its edition, and from section 14 every value to AlumniRules.Latest:
        // "alumni-rule" holds the first undefined value, Latest + 1 (it held 1 at SCH30 and 2 at H1b), and "alumni-unedited"
        // holds 1 without the edition its season was built from.
        [TestCase("alumni-rule")] [TestCase("alumni-unedited")] [TestCase("alumni-edition")] [TestCase("tuning-rule")] [TestCase("future-header")]
        [TestCase("missing-pooled")]
        public void TheStoreRefusesAThirtyHoldingAPooledValueOrAFutureSchemaWithoutChangingAFile(string defect)
        {
            var current = EpisodeSaveMigrations.UpgradeV29ToV30(Fixture("s29-mode2-allweek-w3-campaign"));
            switch (defect)
            {
                case "alumni-rule": current["alumniRulesVersion"] = AlumniRules.Latest + 1; break;
                case "alumni-unedited": current["alumniRulesVersion"] = 1; break;
                case "alumni-edition": current["alumniCatalogEdition"] = "allstar-histories-1"; break;
                case "tuning-rule": current["tuningRulesVersion"] = 1; break;
                case "future-header": current["schemaVersion"] = 31; break;
                default: current.Remove("tuningRulesVersion"); break;
            }
            using var slot = new Slot();
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(PersistenceMigrationTests.Envelope(current));
            slot.Write(slot.Store.SavePath, bytes);
            for (int attempt = 0; attempt < 2; attempt++)
            {
                Assert.That(slot.Store.TryLoad(out var refused, out string message), Is.False, defect);
                Assert.That(refused, Is.Null);
                Assert.That(message, Does.Not.Contain("checksum"));
                if (defect == "future-header") Assert.That(message, Does.Contain("Unsupported simulation schema version."));
                else if (defect == "alumni-unedited") Assert.That(message, Does.Contain("The alumni rules need the edition their season was built from."));
                else if (defect != "missing-pooled") Assert.That(message, Does.Contain("Schema 30's pooled rules are not enabled in this build."));
                Assert.That(File.ReadAllBytes(slot.Store.SavePath), Is.EqualTo(bytes));
            }
        }

        private static JObject Fixture(string id) => (JObject)JObject.Parse(System.Text.Encoding.UTF8.GetString(FixtureBytes(id)))["state"];
        private static byte[] FixtureBytes(string id) => File.ReadAllBytes(Path.Combine(FixtureRoot, id + ".save.json"));
        private static string Text(JToken token) => token.ToString(Formatting.None);
        private static JsonSerializer Serializer() => (JsonSerializer)typeof(EpisodeSaveStore).Assembly
            .GetType("Gamesim.Persistence.SaveJson", true).GetMethod("Serializer", BindingFlags.Public | BindingFlags.Static).Invoke(null, null);
        private static JObject Payload(EpisodeState state) => JObject.FromObject(state, Serializer());

        private sealed class Identity : IEqualityComparer<JToken>
        {
            public bool Equals(JToken first, JToken second) => ReferenceEquals(first, second);
            public int GetHashCode(JToken token) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(token);
        }

        private sealed class Slot : IDisposable
        {
            private const string Prefix = "GamesimSchema30Step-";
            private readonly string leaf = Prefix + Guid.NewGuid().ToString("N");
            internal readonly string Root;
            internal readonly EpisodeSaveStore Store;
            internal Slot()
            {
                Root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), leaf));
                Directory.CreateDirectory(Root);
                Store = new EpisodeSaveStore(Path.Combine(Root, "episode.json"));
            }
            internal void Write(string path, byte[] bytes)
            {
                Assert.That(path == Store.SavePath || path == Store.BackupPath, Is.True);
                File.WriteAllBytes(path, bytes);
            }
            public void Dispose()
            {
                Assert.That(Path.GetFileName(Root), Does.StartWith(Prefix));
                if (Directory.Exists(Root)) Directory.Delete(Root, true);
            }
        }
    }
}
