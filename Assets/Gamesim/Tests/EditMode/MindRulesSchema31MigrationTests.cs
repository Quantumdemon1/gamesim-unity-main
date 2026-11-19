using System;
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
    /// Schema 31's last field, the housemates' minds' ladder (<see cref="MindRules"/>, FIELDS.md), through the save store's
    /// own steps: the step from 30 writes <c>story.mindRules</c> as a literal 0 after the talk fields, on every schema-30
    /// writer fixture and at the end of an older chain; the store keeps a defined level a 31 save holds; and it refuses a
    /// 31 whose mind rules are undefined, missing or not a number without changing a file. Editor only: the store and the
    /// migrations are not in the Unity-free subset.
    /// </summary>
    public sealed class MindRulesSchema31MigrationTests
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
        public void ASchemaThirtySaveLoadsWithTheMindsOff(string id)
        {
            var migrated = EpisodeSaveMigrations.PrepareCurrentPayload(Fixture(id), out bool stepped);
            Assert.That(stepped, Is.True);
            Assert.That((int)migrated["schemaVersion"], Is.EqualTo(31));
            var story = (JObject)migrated["story"];
            var last = story.Properties().Last();
            Assert.That(last.Name, Is.EqualTo("mindRules"), "Written after the talk fields, last on the story.");
            Assert.That(last.Value.Type, Is.EqualTo(JTokenType.Integer));
            Assert.That((long)last.Value, Is.Zero, "A literal 0: an old season keeps the minds off.");
            var state = Load(migrated);
            Assert.That(state.story.mindRules, Is.EqualTo(MindRules.Off));
        }

        [Test]
        public void ASchemaFifteenSaveReachesTheLiveSchemaWithTheMindsOff()
        {
            var migrated = EpisodeSaveMigrations.PrepareCurrentPayload(Fixture("s15-director-w2-campaign"), out bool stepped);
            Assert.That(stepped, Is.True);
            Assert.That((int)migrated["schemaVersion"], Is.EqualTo(EpisodeState.LiveSchema));
            Assert.That(((JObject)migrated["story"]).Properties().Last().Name, Is.EqualTo("mindRules"));
            Assert.That(Load(migrated).story.mindRules, Is.EqualTo(MindRules.Off));
        }

        [Test]
        public void TheStoreKeepsADefinedMindLevelASchemaThirtyOneSaveHolds()
        {
            var current = EpisodeSaveMigrations.UpgradeV30ToV31(Fixture("s30-mode2-allweek-w3-campaign"));
            current["story"]["mindRules"] = MindRules.Latest;
            using var slot = new Slot();
            slot.Write(System.Text.Encoding.UTF8.GetBytes(PersistenceMigrationTests.Envelope(current)));
            Assert.That(slot.Store.TryLoad(out var loaded, out string message), Is.True, message);
            Assert.That(loaded.story.mindRules, Is.EqualTo(MindRules.Latest));
        }

        [TestCase("mind-rules-above")] [TestCase("mind-rules-below")] [TestCase("mind-rules-missing")] [TestCase("mind-rules-text")]
        public void TheStoreRefusesAThirtyOneWithUnadmittedMindRulesWithoutChangingAFile(string defect)
        {
            var current = EpisodeSaveMigrations.UpgradeV30ToV31(Fixture("s30-mode2-allweek-w3-campaign"));
            var story = (JObject)current["story"];
            Assert.That(story.Property("mindRules"), Is.Not.Null, "The step wrote the field this case changes.");
            switch (defect)
            {
                case "mind-rules-above": story["mindRules"] = MindRules.Latest + 1; break;
                case "mind-rules-below": story["mindRules"] = -1; break;
                case "mind-rules-missing": story.Remove("mindRules"); break;
                default: story["mindRules"] = "1"; break;
            }
            using var slot = new Slot();
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(PersistenceMigrationTests.Envelope(current));
            slot.Write(bytes);
            for (int attempt = 0; attempt < 2; attempt++)
            {
                Assert.That(slot.Store.TryLoad(out var refused, out string message), Is.False, defect);
                Assert.That(refused, Is.Null);
                Assert.That(message, Does.Not.Contain("checksum"));
                if (defect == "mind-rules-above" || defect == "mind-rules-below")
                    Assert.That(message, Does.Contain("Unsupported mind rules version."));
                Assert.That(File.ReadAllBytes(slot.Store.SavePath), Is.EqualTo(bytes));
            }
        }

        private static JObject Fixture(string id) => (JObject)JObject.Parse(File.ReadAllText(Path.Combine(FixtureRoot, id + ".save.json")))["state"];

        /// <summary>The store's own shape check, then the live type and the store's validation.</summary>
        private static EpisodeState Load(JObject payload)
        {
            var saveJson = typeof(EpisodeSaveStore).Assembly.GetType("Gamesim.Persistence.SaveJson", true);
            saveJson.GetMethod("CheckDtoShape", BindingFlags.Public | BindingFlags.Static).Invoke(null, new object[] { payload, typeof(EpisodeState), "state" });
            var serializer = (JsonSerializer)saveJson.GetMethod("Serializer", BindingFlags.Public | BindingFlags.Static).Invoke(null, null);
            var state = payload.ToObject<EpisodeState>(serializer);
            Assert.DoesNotThrow(() => EpisodeSaveValidation.Validate(state));
            return state;
        }

        private sealed class Slot : IDisposable
        {
            private const string Prefix = "GamesimSchema31Minds-";
            private readonly string leaf = Prefix + Guid.NewGuid().ToString("N");
            internal readonly string Root;
            internal readonly EpisodeSaveStore Store;
            internal Slot()
            {
                Root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), leaf));
                Directory.CreateDirectory(Root);
                Store = new EpisodeSaveStore(Path.Combine(Root, "episode.json"));
            }
            internal void Write(byte[] bytes) => File.WriteAllBytes(Store.SavePath, bytes);
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
