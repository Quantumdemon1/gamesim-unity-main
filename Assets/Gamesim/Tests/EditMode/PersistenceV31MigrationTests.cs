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
    /// Schema 31 (the free "Just talk" feature's S0, FIELDS.md): UpgradeV30ToV31 adds only the talk fields at their defaults
    /// - the story's three after its reckonings, every contact's after its last week - behind the frozen schema-30 whole
    /// reader, and nothing else; the store loads every schema-30 writer fixture read-only, saves it at 31 and reloads it
    /// unmigrated; and the store refuses a 31 holding a talk value this build does not admit without changing a file.
    /// </summary>
    public sealed class PersistenceV31MigrationTests
    {
        private static readonly string[] Writers30 =
        {
            "s30-default-w2-social", "s30-mode2-allstars-w3-campaign", "s30-mode2-allstars-alumni5-w3-campaign",
            "s30-mode2-allweek-w3-campaign", "s30-mode2-allweek-story11-tuning1-w3-veto-meeting",
            "s30-mode2-dialogue1-tuning1-w1-campaign", "s30-mode2-dialogue2-tuning1-w1-campaign",
        };
        private static string FixtureRoot => Path.Combine(UnityEngine.Application.dataPath,
            "Gamesim", "Tests", "EditMode", "Fixtures", "OldSaves");

        [TestCaseSource(nameof(Writers30))]
        public void TheStepAddsOnlyTheTalkDefaultsInTheirDeclaredPlaces(string id)
        {
            var original = Fixture(id);
            string before = Text(original);
            var next = EpisodeSaveMigrations.UpgradeV30ToV31(original);
            Assert.That(Text(original), Is.EqualTo(before), "The original is never edited.");
            Assert.That((int)next["schemaVersion"], Is.EqualTo(31));
            Assert.That(next.Properties().Select(p => p.Name), Is.EqualTo(original.Properties().Select(p => p.Name)), "No root field is added.");
            var story = (JObject)next["story"];
            var names = story.Properties().Select(p => p.Name).ToList();
            Assert.That(names.Skip(names.Count - 3), Is.EqualTo(PersistenceMigrationTests.Schema31Story));
            Assert.That((int)story["talkRules"], Is.Zero, "Old saves load with free talk off.");
            Assert.That((int)story["talkLean"], Is.Zero);
            Assert.That((JArray)story["talkPairs"], Is.Empty);
            foreach (JObject contact in (JArray)story["contacts"])
            {
                var fields = contact.Properties().Select(p => p.Name).ToList();
                Assert.That(fields.Take(fields.Count - PersistenceMigrationTests.Schema31Contact.Length),
                    Is.EqualTo(new[] { "npcId", "rapport", "weekCount", "lastWeek" }));
                Assert.That(fields.Skip(4), Is.EqualTo(PersistenceMigrationTests.Schema31Contact));
            }
            Assert.That(Text(next), Is.EqualTo(Text(PersistenceMigrationTests.Lift31(original))), "Exactly the hand-written literals.");
            var inverse = (JObject)next.DeepClone(); PersistenceMigrationTests.StripSchema31(inverse);
            Assert.That(Text(inverse), Is.EqualTo(before), "Every old token, property order, rule, RNG and history survives exactly.");
            var originals = new HashSet<JToken>(original.DescendantsAndSelf(), new Identity());
            Assert.That(next.DescendantsAndSelf().Any(token => originals.Contains(token)), Is.False, "A detached result.");
            var state = next.ToObject<EpisodeState>(Serializer());
            Assert.DoesNotThrow(() => EpisodeSaveValidation.Validate(state));
            Assert.That(Text(Payload(state)), Is.EqualTo(Text(next)), "Today's serializer writes the step's exact tokens.");
        }

        [Test]
        public void TheWritersFixturesHoldContactsSoEveryContactLiteralIsExercised()
        {
            Assert.That(Writers30.Sum(id => ((JArray)Fixture(id)["story"]["contacts"]).Count), Is.GreaterThan(0));
        }

        [TestCase("null-input")] [TestCase("missing-header")] [TestCase("text-header")] [TestCase("old-header")]
        [TestCase("current-header")] [TestCase("preexisting-talk-rules")] [TestCase("preexisting-contact-field")]
        [TestCase("unknown-root")] [TestCase("story-rules-13")] [TestCase("dialogue-rules-3")] [TestCase("missing-tuning")]
        public void TheStepRefusesAnythingButAWholeLawfulThirtyBeforeAnyLiteral(string defect)
        {
            var bad = defect == "null-input" ? null : Fixture("s30-mode2-allweek-w3-campaign");
            switch (defect)
            {
                case "missing-header": bad.Remove("schemaVersion"); break;
                case "text-header": bad["schemaVersion"] = "30"; break;
                case "old-header": bad["schemaVersion"] = 29; break;
                case "current-header": bad["schemaVersion"] = 31; break;
                case "preexisting-talk-rules": ((JObject)bad["story"]).Add("talkRules", 0); break;
                case "preexisting-contact-field":
                    if (((JArray)bad["story"]["contacts"]).Count == 0) ((JObject)bad["story"]).Add("talkLean", 0);
                    else ((JObject)bad["story"]["contacts"][0]).Add("talks", 0);
                    break;
                case "unknown-root": bad.Add("talkHistory", new JArray()); break;
                case "story-rules-13": bad["story"]["rulesVersion"] = 13; break;
                case "dialogue-rules-3": bad["dialogueRulesVersion"] = 3; break;
                case "missing-tuning": bad.Remove("tuningRulesVersion"); break;
            }
            string before = bad == null ? null : Text(bad);
            for (int attempt = 0; attempt < 2; attempt++)
            {
                Assert.Throws<InvalidDataException>(() => EpisodeSaveMigrations.UpgradeV30ToV31(bad), defect);
                if (bad != null) Assert.That(Text(bad), Is.EqualTo(before), "No partial literal or repair on refusal.");
            }
        }

        [TestCaseSource(nameof(Writers30))]
        public void TheDispatchToThirtyDetachesADirect30AndRefusesTheLiveSchema(string id)
        {
            var original = Fixture(id);
            var again = EpisodeSaveMigrations.PrepareV30Payload(original, out bool migrated);
            Assert.That(migrated, Is.False);
            Assert.That(again, Is.Not.SameAs(original));
            Assert.That(Text(again), Is.EqualTo(Text(original)));
            var current = EpisodeSaveMigrations.UpgradeV30ToV31(original);
            Assert.Throws<InvalidDataException>(() => EpisodeSaveMigrations.PrepareV30Payload(current, out _), "Thirty-one is above 30's dispatch.");
            var older = Fixture("s29-mode2-allweek-w3-campaign");
            var stepped = EpisodeSaveMigrations.PrepareV30Payload(older, out migrated);
            Assert.That(migrated, Is.True);
            Assert.That(Text(stepped), Is.EqualTo(Text(EpisodeSaveMigrations.UpgradeV29ToV30(older))));
            var live = EpisodeSaveMigrations.PrepareCurrentPayload(current, out migrated);
            Assert.That(migrated, Is.False, "A direct 31 is the live schema.");
            Assert.That(Text(live), Is.EqualTo(Text(current)));
        }

        [TestCaseSource(nameof(Writers30))]
        public void ASchemaThirtySaveLoadsReadOnlySavesAtThirtyOneAndReloadsUnmigrated(string id)
        {
            byte[] source = FixtureBytes(id);
            using var slot = new Slot();
            slot.Write(slot.Store.SavePath, source);
            Assert.That(slot.Store.TryLoad(out var loaded, out string message), Is.True, message);
            Assert.That(message, Does.Contain("Schema 30").And.Contain("schema 31 in memory"));
            Assert.That(File.ReadAllBytes(slot.Store.SavePath), Is.EqualTo(source), "Loading is read-only.");
            Assert.That(Text(Payload(loaded)), Is.EqualTo(Text(EpisodeSaveMigrations.UpgradeV30ToV31(Fixture(id)))));
            Assert.That(loaded.story.talkRules, Is.EqualTo(TalkRules.Off), "An old save keeps free talk off.");
            slot.Store.Save(loaded);
            Assert.That(File.ReadAllBytes(slot.Store.BackupPath), Is.EqualTo(source), "Rotation keeps the 30 writer's envelope.");
            var saved = JObject.Parse(File.ReadAllText(slot.Store.SavePath));
            Assert.That((int)saved["state"]["schemaVersion"], Is.EqualTo(31));
            Assert.That(slot.Store.TryLoad(out var again, out message), Is.True, message);
            Assert.That(message ?? "", Does.Not.Contain("migrated"));
            Assert.That(Text(Payload(again)), Is.EqualTo(Text(Payload(loaded))));
            Assert.That(slot.Store.TryRecoverBackup(out var recovered, out message), Is.True, message);
            Assert.That(Text(Payload(recovered)), Is.EqualTo(Text(Payload(loaded))), "The 30 backup recovers to the same 31 season.");
        }

        [TestCase("talk-rules")] [TestCase("contact-tenths")] [TestCase("contact-romance")] [TestCase("future-header")] [TestCase("missing-pairs")]
        public void TheStoreRefusesAThirtyOneHoldingAnUnadmittedTalkValueWithoutChangingAFile(string defect)
        {
            var current = EpisodeSaveMigrations.UpgradeV30ToV31(Fixture("s30-mode2-allweek-w3-campaign"));
            var story = (JObject)current["story"];
            if (((JArray)story["contacts"]).Count == 0 && defect.StartsWith("contact-", StringComparison.Ordinal))
                Assert.Fail("The fixture needs a contact for " + defect + ".");
            switch (defect)
            {
                case "talk-rules": story["talkRules"] = TalkRules.Latest + 1; break;
                case "contact-tenths": story["talkRules"] = TalkRules.Talk; story["contacts"][0]["talkTenths"] = 10; break;
                case "contact-romance": story["contacts"][0]["romance"] = TalkRules.Couple; break;
                case "future-header": current["schemaVersion"] = 32; break;
                default: story.Remove("talkPairs"); break;
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
                else if (defect == "talk-rules") Assert.That(message, Does.Contain("Unsupported talk rules version."));
                else if (defect == "contact-tenths") Assert.That(message, Does.Contain("Invalid contact data."));
                else if (defect == "contact-romance") Assert.That(message, Does.Contain("A season without free talk has none of its records."));
                Assert.That(File.ReadAllBytes(slot.Store.SavePath), Is.EqualTo(bytes));
            }
        }

        [TestCase("talk-rules")] [TestCase("contact-field")] [TestCase("order")] [TestCase("unknown")] [TestCase("header")]
        public void Current31NativeInverseRequiresCompleteInertSourceBeforeCallerMutation(string defect)
        {
            var payload = EpisodeSaveMigrations.UpgradeV30ToV31(Fixture("s30-mode2-allweek-w3-campaign"));
            string original = Text(payload);
            var retained = (JObject)payload.DeepClone();
            Assert.That(PersistenceMigrationTests.StripSchema31(retained), Is.SameAs(retained));
            Assert.That((int)retained["schemaVersion"], Is.EqualTo(30));
            Assert.That(Text(EpisodeSaveMigrations.UpgradeV30ToV31(retained)), Is.EqualTo(original));
            var bad = (JObject)payload.DeepClone();
            var story = (JObject)bad["story"];
            switch (defect)
            {
                case "talk-rules": story["talkRules"] = 1; break;
                case "contact-field":
                    if (((JArray)story["contacts"]).Count == 0) story["talkLean"] = 1; else story["contacts"][0]["talks"] = 1;
                    break;
                case "order": { var p = story.Property("talkPairs"); p.Remove(); story.AddFirst(p); break; }
                case "unknown": story["talkUnknown"] = 0; break;
                default: bad["schemaVersion"] = 32; break;
            }
            string before = Text(bad);
            var refusal = Assert.Catch(() => PersistenceMigrationTests.StripSchema31(bad));
            Assert.That(refusal is AssertionException || refusal is InvalidDataException, Is.True,
                "A contract refusal, never an unrelated null/index/reflection crash, is required.");
            Assert.That(Text(bad), Is.EqualTo(before), "No partial downgrade or normalization on refusal.");
            Assert.That(Text(payload), Is.EqualTo(original));
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
            private const string Prefix = "GamesimSchema31Step-";
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
                string temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                Assert.That(Path.GetDirectoryName(Root), Is.EqualTo(temp));
                Assert.That(Path.GetFileName(Root), Does.StartWith(Prefix));
                if (Directory.Exists(Root)) Directory.Delete(Root, true);
            }
        }
    }
}
