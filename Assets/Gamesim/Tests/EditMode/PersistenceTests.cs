using System;
using System.IO;
using System.Linq;
using Gamesim.Persistence;
using Gamesim.Simulation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    public sealed class PersistenceTests
    {
        private string directory;
        private EpisodeSaveStore store;

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "GamesimPersistenceTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            store = new EpisodeSaveStore(Path.Combine(directory, "episode.json"));
        }

        [TearDown]
        public void TearDown()
        {
            var resolved = Path.GetFullPath(directory);
            Assert.That(resolved.StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase), Is.True);
            Assert.That(Path.GetFileName(resolved), Does.StartWith("GamesimPersistenceTests-"));
            if (Directory.Exists(resolved)) Directory.Delete(resolved, true);
        }

        [Test]
        public void LocalRoundTrip_PreservesZeroRandomStateAndDetachedHistory()
        {
            var original = ContentCatalog.Create(0);
            original.randomState = 0;
            original.relationships[0].score = 37;
            store.Save(original);
            original.relationships[0].score = -90;
            Assert.That(store.TryLoad(out var loaded, out var message), Is.True, message);
            Assert.That(loaded.seed, Is.EqualTo(0));
            Assert.That(loaded.randomState, Is.EqualTo(0));
            Assert.That(loaded.relationships[0].score, Is.EqualTo(37));
            Assert.That(loaded.events[0].text, Is.EqualTo(original.events[0].text));
            Assert.That(loaded.memories.Count, Is.EqualTo(original.memories.Count));
            Assert.That(Directory.GetFiles(directory, "*.pending-*"), Is.Empty);
        }

        [Test]
        public void DamagedPrimary_BlocksOverwriteAndRequiresExplicitBackupRecovery()
        {
            var first = ContentCatalog.Create(17);
            store.Save(first);
            var second = first.Clone();
            second.revision = 1;
            second.relationships[0].score = 42;
            store.Save(second);
            var backupBytes = File.ReadAllBytes(store.BackupPath);
            const string damaged = "interrupted external write";
            File.WriteAllText(store.SavePath, damaged);
            Assert.That(store.TryLoad(out var failed, out var message), Is.False);
            Assert.That(failed, Is.Null);
            Assert.That(message, Does.Contain("Recover backup"));
            Assert.Throws<InvalidDataException>(() => store.Save(second));
            Assert.That(File.ReadAllText(store.SavePath), Is.EqualTo(damaged));
            Assert.That(File.ReadAllBytes(store.BackupPath), Is.EqualTo(backupBytes));
            Assert.That(store.TryRecoverBackup(out var restored, out message), Is.True, message);
            Assert.That(restored.revision, Is.EqualTo(first.revision));
            Assert.That(restored.relationships[0].score, Is.EqualTo(first.relationships[0].score));
            Assert.That(File.ReadAllBytes(store.BackupPath), Is.EqualTo(backupBytes));
            var archives = Directory.GetFiles(directory, "*.before-recovery-*.json");
            Assert.That(archives, Has.Length.EqualTo(1));
            Assert.That(File.ReadAllText(archives[0]), Is.EqualTo(damaged));
            Assert.That(store.TryLoad(out _, out message), Is.True, message);
        }

        [Test]
        public void InvalidCandidate_DoesNotChangeAnyGoodSave()
        {
            var valid = ContentCatalog.Create(31);
            store.Save(valid);
            var before = File.ReadAllBytes(store.SavePath);
            var invalid = valid.Clone();
            invalid.contestants[1].id = invalid.contestants[0].id;
            Assert.Throws<InvalidDataException>(() => store.Save(invalid));
            Assert.That(File.ReadAllBytes(store.SavePath), Is.EqualTo(before));
            Assert.That(File.Exists(store.BackupPath), Is.False);
        }

        [Test]
        public void ChangedPayload_FailsChecksumBeforeStateInstallation()
        {
            store.Save(ContentCatalog.Create(7));
            var document = JObject.Parse(File.ReadAllText(store.SavePath));
            document["state"]["week"] = 99;
            File.WriteAllText(store.SavePath, document.ToString());
            Assert.That(store.TryLoad(out var loaded, out var message), Is.False);
            Assert.That(loaded, Is.Null);
            Assert.That(message, Does.Contain("checksum"));
        }

        [Test]
        public void FutureEnvelope_IsPreservedAndRejected()
        {
            store.Save(ContentCatalog.Create(7));
            var document = JObject.Parse(File.ReadAllText(store.SavePath));
            document["version"] = 2;
            File.WriteAllText(store.SavePath, document.ToString());
            var before = File.ReadAllBytes(store.SavePath);
            Assert.That(store.TryLoad(out var loaded, out var message), Is.False);
            Assert.That(loaded, Is.Null);
            Assert.That(message, Does.Contain("unsupported"));
            Assert.That(File.ReadAllBytes(store.SavePath), Is.EqualTo(before));
        }

        [Test]
        public void MissingOrDamagedBackup_DoesNotReplaceThePrimary()
        {
            store.Save(ContentCatalog.Create(7));
            var before = File.ReadAllBytes(store.SavePath);
            Assert.That(store.TryRecoverBackup(out _, out _), Is.False);
            File.WriteAllText(store.BackupPath, "broken backup");
            Assert.That(store.TryRecoverBackup(out _, out _), Is.False);
            Assert.That(File.ReadAllBytes(store.SavePath), Is.EqualTo(before));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void SupportedWebEnvelope_PreservesIdentityDirectedScoresAndOriginal(int version)
        {
            var document = WebFixture();
            if (version > 0) document["version"] = version;
            var json = "\n" + (version == 0 ? document["state"] : document).ToString(Formatting.Indented) + "\n";
            var archive = Path.Combine(directory, "imports");
            Assert.That(WebSaveImporter.TryImport(json, archive, out var imported, out var message), Is.True, message);
            Assert.That(imported.playerId, Is.EqualTo("external-player"));
            Assert.That(imported.contestants[1].id, Is.EqualTo("external-maya"));
            Assert.That(imported.Score("external-player", "external-maya"), Is.EqualTo(25));
            Assert.That(imported.Score("external-maya", "external-player"), Is.EqualTo(-12));
            Assert.That(imported.contestants[0].stats.social, Is.EqualTo(6));
            Assert.That(imported.contestants[0].hohWins, Is.EqualTo(1));
            Assert.That(imported.phase, Is.EqualTo(EpisodePhase.Social));
            var originals = Directory.GetFiles(archive);
            Assert.That(originals, Has.Length.EqualTo(1));
            Assert.That(File.ReadAllText(originals[0]), Is.EqualTo(json));
            Assert.That(message, Does.Contain("defaults").And.Contain("Original preserved"));
            Assert.That(WebSaveImporter.TryImport(json, archive, out var repeated, out _), Is.True);
            Assert.That(repeated.randomState, Is.EqualTo(imported.randomState));
            Assert.That(Directory.GetFiles(archive), Has.Length.EqualTo(1));
        }

        [TestCase("receipt")]
        [TestCase("phase")]
        [TestCase("cast")]
        [TestCase("duplicate-id")]
        [TestCase("unknown-field")]
        [TestCase("storyline")]
        [TestCase("malformed-flag")]
        [TestCase("future")]
        public void UnsupportedWebGameplay_IsRejectedAndOriginalArchived(string problem)
        {
            var document = WebFixture();
            var source = (JObject)document["state"];
            switch (problem)
            {
                case "receipt": source["nominationResolution"] = new JObject(); break;
                case "phase": source["phase"] = "Eviction"; break;
                case "cast": ((JArray)source["houseguests"]).Add(new JObject { ["id"] = "seventh", ["name"] = "Seventh" }); break;
                case "duplicate-id": source["houseguests"][1]["id"] = "external-player"; break;
                case "unknown-field": source["unmodeledGameplay"] = 12; break;
                case "storyline": source["activeStorylines"] = new JArray(new JObject { ["id"] = "unfinished-story" }); break;
                case "malformed-flag": source["isFinalStage"] = ""; break;
                case "future": document["version"] = 100; break;
            }

            var json = document.ToString();
            var archive = Path.Combine(directory, "imports");
            Assert.That(WebSaveImporter.TryImport(json, archive, out var imported, out var message), Is.False);
            Assert.That(imported, Is.Null);
            Assert.That(message, Does.Contain("not installed").And.Contain("Original preserved"));
            Assert.That(File.ReadAllText(Directory.GetFiles(archive).Single()), Is.EqualTo(json));
            Assert.That(File.Exists(store.SavePath), Is.False);
        }

        [Test]
        public void DuplicateJsonProperties_AreRejectedWithoutGuessing()
        {
            const string json = "{\"week\":1,\"week\":2}";
            var archive = Path.Combine(directory, "imports");
            Assert.That(WebSaveImporter.TryImport(json, archive, out var imported, out _), Is.False);
            Assert.That(imported, Is.Null);
            Assert.That(File.ReadAllText(Directory.GetFiles(archive).Single()), Is.EqualTo(json));
        }

        [Test]
        public void ArchiveFailure_PreventsImportInstallation()
        {
            var blockedDirectory = Path.Combine(directory, "file-not-directory");
            File.WriteAllText(blockedDirectory, "preserve this file");
            Assert.That(WebSaveImporter.TryImport(WebFixture().ToString(), blockedDirectory, out var imported, out _), Is.False);
            Assert.That(imported, Is.Null);
            Assert.That(File.ReadAllText(blockedDirectory), Is.EqualTo("preserve this file"));
        }

        private static JObject WebFixture()
        {
            var ids = new[] { "external-player", "external-maya", "external-taylor", "external-jamie", "external-casey", "external-riley" };
            var cast = ContentCatalog.Create(13).contestants;
            var guests = new JArray(cast.Select((actor, index) => new JObject
            {
                ["id"] = ids[index], ["name"] = actor.name, ["isPlayer"] = index == 0, ["status"] = "Active",
                ["stats"] = JObject.FromObject(actor.stats), ["traits"] = new JArray(actor.traits),
                ["competitionsWon"] = new JObject { ["hoh"] = index == 0 ? 1 : 0, ["pov"] = 0, ["other"] = 0 },
                ["nominations"] = new JObject { ["times"] = 0, ["receivedOn"] = new JArray() }
            }));
            return new JObject
            {
                ["format"] = "gamesim-save", ["version"] = 1, ["savedAt"] = "2026-09-10T00:00:00.000Z",
                ["state"] = new JObject
                {
                    ["phase"] = "SocialInteraction", ["week"] = 1, ["houseguests"] = guests,
                    ["relationships"] = new JObject
                    {
                        [ids[0]] = new JObject { [ids[1]] = new JObject { ["score"] = 25 } },
                        [ids[1]] = new JObject { [ids[0]] = new JObject { ["score"] = -12 } }
                    }
                }
            };
        }
    }
}
