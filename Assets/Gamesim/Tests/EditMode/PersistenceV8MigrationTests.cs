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
    /// Schema 8: eviction night's stages and speeches, the backdoor plan, the out-of-phase action
    /// count, the opening beats already played, and the last two fields of a houseguest's card.
    ///
    /// <para>The claim worth testing hardest is the one the migration does <b>not</b> default.
    /// Every other new field arrives empty, because a season saved before them holds no record of
    /// what they would have been. The eviction stage is different: a v7 save already records
    /// whether the eviction resolved and which ballots were cast, so the stage is a reading of
    /// facts the save holds rather than an invention. Defaulting it would take a season with a
    /// completed vote and put it back before the speeches — a rewind, not a conservative default.
    /// </para>
    /// </summary>
    public sealed class PersistenceV8MigrationTests
    {
        private static readonly string[] AddedRootFields =
            { "evictionStage", "evictionSpeeches", "backdoorTargetId", "outOfPhaseSocialActions", "openingBeatsSeen" };
        private static readonly string[] AddedCardFields = { "hometown", "bio" };

        [Test]
        public void AFreshSeasonIsWrittenAtTheCurrentSchema()
        {
            Assert.That(ContentCatalog.Create(11u).schemaVersion, Is.EqualTo(12));
            Assert.That(SeasonBuilder.Create(new SeasonBuilder.Choice(), 11u).schemaVersion, Is.EqualTo(12));
        }

        [Test]
        public void TheUpgradeAddsTheNewFieldsAndChangesNothingElse()
        {
            var before = CaptureV7(ContentCatalog.Create(601));
            string original = before.ToString(Formatting.None);

            var after = EpisodeSaveMigrations.UpgradeV7ToV8(before);

            Assert.That((int)after["schemaVersion"], Is.EqualTo(8));
            Assert.That(before.ToString(Formatting.None), Is.EqualTo(original), "The source payload must not be mutated.");

            CollectionAssert.AreEquivalent(
                before.Properties().Select(p => p.Name).Concat(AddedRootFields).ToArray(),
                after.Properties().Select(p => p.Name).ToArray());

            foreach (var property in before.Properties())
            {
                if (property.Name == "schemaVersion" || property.Name == "contestants") continue;
                Assert.That(JToken.DeepEquals(property.Value, after[property.Name]), Is.True, property.Name);
            }

            var oldCast = (JArray)before["contestants"];
            var newCast = (JArray)after["contestants"];
            Assert.That(newCast.Count, Is.EqualTo(oldCast.Count));
            for (int i = 0; i < newCast.Count; i++)
            {
                var actor = (JObject)newCast[i];
                foreach (var field in AddedCardFields)
                    Assert.That(actor.Property(field), Is.Not.Null, "contestants[" + i + "]." + field);
                foreach (var property in ((JObject)oldCast[i]).Properties())
                    Assert.That(JToken.DeepEquals(property.Value, actor[property.Name]), Is.True,
                        "contestants[" + i + "]." + property.Name);
            }
        }

        [Test]
        public void TheUpgradeInventsNoCardCopyItWasNotGiven()
        {
            var after = EpisodeSaveMigrations.UpgradeV7ToV8(CaptureV7(ContentCatalog.Create(601)));
            foreach (JObject actor in (JArray)after["contestants"])
            {
                Assert.That(actor["hometown"].Type, Is.EqualTo(JTokenType.Null), (string)actor["name"]);
                Assert.That(actor["bio"].Type, Is.EqualTo(JTokenType.Null), (string)actor["name"]);
            }
            Assert.That(after["backdoorTargetId"].Type, Is.EqualTo(JTokenType.Null));
            Assert.That((int)after["outOfPhaseSocialActions"], Is.Zero);
            Assert.That(((JArray)after["evictionSpeeches"]).Count, Is.Zero);
            Assert.That(((JArray)after["openingBeatsSeen"]).Count, Is.Zero);
        }

        /// <summary>
        /// The stage is read from the save rather than defaulted. A season mid-eviction must come
        /// back where it was, not before the speeches it already gave.
        /// </summary>
        [Test]
        public void TheEvictionStageIsDerivedFromWhatTheSaveAlreadyRecords()
        {
            var engine = new EpisodeEngine(ContentCatalog.Create(4242));
            bool sawPartial = false, sawResolved = false, sawOutsideEviction = false;

            for (int guard = 0; guard < 320 && engine.Snapshot.phase != EpisodePhase.Finished; guard++)
            {
                var state = engine.Snapshot;
                var upgraded = EpisodeSaveMigrations.UpgradeV7ToV8(CaptureV7(state));
                var stage = (EvictionStage)(int)upgraded["evictionStage"];

                if (state.phase != EpisodePhase.Eviction)
                {
                    Assert.That(stage, Is.EqualTo(EvictionStage.Interaction),
                        "Outside eviction night the stage is the one a fresh night opens on: " + state.phase);
                    sawOutsideEviction = true;
                }
                else if (state.evictionResolved)
                {
                    Assert.That(stage, Is.EqualTo(EvictionStage.Results));
                    sawResolved = true;
                }
                else if (state.votes.Count > 0)
                {
                    Assert.That(stage, Is.EqualTo(EvictionStage.Voting),
                        "A save holding cast ballots must not rewind to the interaction stage.");
                    sawPartial = true;
                }

                var result = engine.Apply(EpisodeEngineTests.NextCommand(state));
                Assert.That(result.accepted, Is.True, result.reason);
            }

            Assert.That(sawOutsideEviction && sawPartial && sawResolved, Is.True,
                "The season should have passed through all three readings.");
        }

        [Test]
        public void EveryHistoricalVersionMigratesAllTheWayToSchemaEight()
        {
            for (int version = 1; version <= 7; version++)
            {
                var old = Historical(version);
                // PrepareV8Payload, not PrepareCurrentPayload: this file pins the step that ends at
                // schema 8, and "current" moved on when schema 9 added the budget rule boundary.
                var result = EpisodeSaveMigrations.PrepareV8Payload(old, out bool migrated);

                Assert.That(migrated, Is.True, "version " + version);
                Assert.That((int)result["schemaVersion"], Is.EqualTo(8), "version " + version);
                foreach (var field in AddedRootFields)
                    Assert.That(result.Property(field), Is.Not.Null, "version " + version + ", " + field);
                foreach (JObject actor in (JArray)result["contestants"])
                    foreach (var field in AddedCardFields)
                        Assert.That(actor.Property(field), Is.Not.Null, "version " + version + ", " + field);
            }
        }

        [Test]
        public void MigratingAgainIsANoOpAndTheResultLoadsAndValidates()
        {
            var old = CaptureV7(ContentCatalog.Create(601));
            var result = EpisodeSaveMigrations.PrepareV8Payload(old, out bool migrated);
            Assert.That(migrated, Is.True);

            var repeat = EpisodeSaveMigrations.PrepareV8Payload(result, out migrated);
            Assert.That(migrated, Is.False, "A schema 8 payload must not be migrated again.");
            Assert.That(ReferenceEquals(result, repeat), Is.False, "The payload must be cloned, not handed back.");
            Assert.That(JToken.DeepEquals(result, repeat), Is.True);

            var parsed = EpisodeSaveMigrations.PrepareCurrentPayload(old, out _).ToObject<EpisodeState>(Serializer());
            Assert.That(EpisodeValidation.TryValidate(parsed, out var error), Is.True, error);
            Assert.That(parsed.evictionStage, Is.EqualTo(EvictionStage.Interaction));
            Assert.That(parsed.evictionSpeeches, Is.Empty);
            Assert.That(parsed.backdoorTargetId, Is.Null);
            Assert.That(parsed.openingBeatsSeen, Is.Empty);
            Assert.That(parsed.contestants, Has.All.Matches<ContestantState>(c => c.hometown == null && c.bio == null));
        }

        [Test]
        public void AV7SaveOnDiskLoadsUnchangedAndOnlyRewritesOnAnExplicitSave()
        {
            using var files = new Files();
            files.Write(CaptureV7(ContentCatalog.Create(601)));
            var originalBytes = File.ReadAllBytes(files.Store.SavePath);

            Assert.That(files.Store.TryLoad(out var loaded, out string message), Is.True, message);
            Assert.That(message, Does.Contain("Schema 7").And.Contain("schema 12 in memory"));
            Assert.That(loaded.schemaVersion, Is.EqualTo(12), "A load runs the whole chain, not one step.");
            Assert.That(File.ReadAllBytes(files.Store.SavePath), Is.EqualTo(originalBytes),
                "Loading must not rewrite the file.");

            files.Store.Save(loaded);
            Assert.That(File.ReadAllBytes(files.Store.BackupPath), Is.EqualTo(originalBytes),
                "The pre-migration bytes are kept as the backup.");
            Assert.That(files.Store.TryLoad(out var again, out message), Is.True, message);
            Assert.That(again.schemaVersion, Is.EqualTo(12));
            Assert.That(JToken.DeepEquals(Capture(loaded), Capture(again)), Is.True);
        }

        [TestCase("week", "101")]
        [TestCase("revision", "1000001")]
        [TestCase("playerStudyBonus", "6")]
        [TestCase("contestants[0].stats.luck", "10.1")]
        [TestCase("contestants[0].age", "121")]
        [TestCase("npcSocial", "null")]
        [TestCase("blocRulesStartWeek", "0")]
        public void FrozenV7RejectsFormerlyInvalidValuesBeforeAddingAnyDefaults(string path, string value)
        {
            var old = CaptureV7(ContentCatalog.Create(601));
            old.SelectToken(path).Replace(JToken.Parse(value));

            string before = old.ToString(Formatting.None);
            Assert.Throws<InvalidDataException>(() => EpisodeSaveMigrations.UpgradeV7ToV8(old));
            Assert.That(old.ToString(Formatting.None), Is.EqualTo(before));
        }

        /// <summary>
        /// The frozen v7 shape does not follow the growing runtime DTO: a v7 payload carrying
        /// schema 8's own fields is not a v7 payload, and neither is one missing a v7 field.
        /// </summary>
        [Test]
        public void FrozenV7RejectsUnknownAndMissingFieldsOnEveryRow()
        {
            var valid = CaptureV7(ContentCatalog.Create(601));
            Assert.DoesNotThrow(() => EpisodeSaveMigrations.UpgradeV7ToV8(valid));

            foreach (var field in AddedRootFields)
            {
                var ahead = CaptureV7(ContentCatalog.Create(601));
                ahead.Add(field, field == "evictionStage" || field == "outOfPhaseSocialActions"
                    ? (JToken)0 : new JArray());
                Assert.Throws<InvalidDataException>(() => EpisodeSaveMigrations.UpgradeV7ToV8(ahead), field);
            }

            var paths = valid.DescendantsAndSelf().OfType<JObject>().Select(row => row.Path).ToArray();
            Assert.That(paths.Length, Is.GreaterThan(25));
            foreach (string path in paths)
            {
                var copy = (JObject)valid.DeepClone();
                var row = path.Length == 0 ? copy : (JObject)copy.SelectToken(path);
                row.Properties().Last().Remove();
                Assert.Throws<InvalidDataException>(() => EpisodeSaveMigrations.UpgradeV7ToV8(copy), path);
            }
        }

        /// <summary>
        /// Schema 7 accepted three to sixteen houseguests, so its frozen contract has to as well or
        /// every large season already on disk becomes unreadable at the version bump.
        /// </summary>
        [TestCase(3)] [TestCase(6)] [TestCase(8)] [TestCase(12)]
        public void EveryHouseSizeSchemaSevenAcceptedStillMigrates(int size)
        {
            var built = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = size }, 77u);
            var old = CaptureV7(built);

            var result = EpisodeSaveMigrations.UpgradeV7ToV8(old);
            Assert.That(((JArray)result["contestants"]).Count, Is.EqualTo(size));

            var parsed = EpisodeSaveMigrations.PrepareCurrentPayload(old, out _).ToObject<EpisodeState>(Serializer());
            Assert.That(EpisodeValidation.TryValidate(parsed, out var error), Is.True, size + ": " + error);
        }

        // ---------------------------------------------------------------- fixtures

        /// <summary>The current runtime state expressed in schema 7's shape.</summary>
        private static JObject CaptureV7(EpisodeState state)
        {
            var payload = PersistenceMigrationTests.StripSchema8(PersistenceMigrationTests.StripSchema9(PersistenceMigrationTests.StripSchema10(PersistenceMigrationTests.StripSchema11(PersistenceMigrationTests.StripSchema12(Capture(state))))));
            payload["schemaVersion"] = 7;
            return payload;
        }

        private static JObject Historical(int version)
        {
            if (version == 7) return CaptureV7(ContentCatalog.Create(601));
            var old = (JObject)typeof(PersistenceV7MigrationTests)
                .GetMethod("Historical", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, new object[] { version });
            return old;
        }

        private static JObject Capture(EpisodeState state) => JObject.FromObject(state, Serializer());

        private static JsonSerializer Serializer() => (JsonSerializer)typeof(EpisodeSaveStore).Assembly
            .GetType("Gamesim.Persistence.SaveJson", true)
            .GetMethod("Serializer", BindingFlags.Public | BindingFlags.Static).Invoke(null, null);

        private sealed class Files : IDisposable
        {
            public readonly string DirectoryPath =
                Path.Combine(Path.GetTempPath(), "GamesimV8MigrationTests-" + Guid.NewGuid().ToString("N"));
            public readonly EpisodeSaveStore Store;

            public Files()
            {
                Directory.CreateDirectory(DirectoryPath);
                Store = new EpisodeSaveStore(Path.Combine(DirectoryPath, "episode.json"));
            }

            public void Write(JObject payload) =>
                File.WriteAllText(Store.SavePath, PersistenceMigrationTests.Envelope(payload));

            public void Dispose()
            {
                try { Directory.Delete(DirectoryPath, true); } catch (IOException) { }
            }
        }
    }
}
