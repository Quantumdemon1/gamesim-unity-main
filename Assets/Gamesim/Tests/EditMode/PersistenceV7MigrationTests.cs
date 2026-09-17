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
    /// Schema 7: the card copy on a houseguest — an archetype, an age and a job.
    ///
    /// <para>Three optional fields would not need a version in a format that ignored unknown
    /// properties. This one does not: a stored object must carry exactly the fields its type
    /// declares, which is what catches a truncated or hand-edited save. So adding them is a
    /// migration, and these are the guards that migration has to pass.</para>
    ///
    /// <para>The claim that matters most is the one about <b>not inventing history</b>. A season
    /// saved before this existed has no record of who anybody was outside the house, and the
    /// migration must leave the fields empty rather than guess them from the shipped cast — those
    /// ids are not reserved, and an imported or hand-built save can hold anyone.</para>
    /// </summary>
    public sealed class PersistenceV7MigrationTests
    {
        private static readonly string[] CardFields = { "occupation", "archetype", "age" };

        [Test]
        public void AFreshSeasonIsWrittenAtSchemaSeven()
        {
            Assert.That(ContentCatalog.Create(11u).schemaVersion, Is.EqualTo(7));
            Assert.That(SeasonBuilder.Create(new SeasonBuilder.Choice(), 11u).schemaVersion, Is.EqualTo(7));
        }

        [Test]
        public void TheUpgradeAddsTheCardFieldsAndChangesNothingElse()
        {
            var before = CaptureV6(ContentCatalog.Create(601));
            string original = before.ToString(Formatting.None);

            var after = EpisodeSaveMigrations.UpgradeV6ToV7(before);

            Assert.That((int)after["schemaVersion"], Is.EqualTo(7));
            Assert.That(before.ToString(Formatting.None), Is.EqualTo(original), "The source payload must not be mutated.");

            // Root shape is untouched: the fields live on each contestant, not on the state.
            CollectionAssert.AreEquivalent(
                before.Properties().Select(p => p.Name).ToArray(),
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
                foreach (var field in CardFields)
                    Assert.That(actor.Property(field), Is.Not.Null, "contestants[" + i + "]." + field);

                // Everything the v6 record held is byte-identical.
                foreach (var property in ((JObject)oldCast[i]).Properties())
                    Assert.That(JToken.DeepEquals(property.Value, actor[property.Name]), Is.True,
                        "contestants[" + i + "]." + property.Name);
            }
        }

        /// <summary>
        /// The migration leaves the card blank rather than filling it in from the shipped cast.
        /// Matching on id would be wrong the moment a save holds someone who is not in it.
        /// </summary>
        [Test]
        public void TheUpgradeInventsNoHistoryForHouseguestsItRecognises()
        {
            var after = EpisodeSaveMigrations.UpgradeV6ToV7(CaptureV6(ContentCatalog.Create(601)));

            foreach (JObject actor in (JArray)after["contestants"])
            {
                Assert.That(actor["occupation"].Type, Is.EqualTo(JTokenType.Null), (string)actor["name"]);
                Assert.That(actor["archetype"].Type, Is.EqualTo(JTokenType.Null), (string)actor["name"]);
                Assert.That((int)actor["age"], Is.Zero, (string)actor["name"]);
            }

            // Including Maya, who does have authored card copy in a freshly created season — the
            // migration must not reach for it.
            var maya = ((JArray)after["contestants"]).OfType<JObject>()
                .Single(actor => (string)actor["id"] == ContentCatalog.MayaId);
            Assert.That(maya["archetype"].Type, Is.EqualTo(JTokenType.Null));
        }

        [Test]
        public void EveryHistoricalVersionMigratesAllTheWayToSchemaSeven()
        {
            for (int version = 1; version <= 6; version++)
            {
                var old = Historical(version);
                var result = EpisodeSaveMigrations.PrepareCurrentPayload(old, out bool migrated);

                Assert.That(migrated, Is.True, "version " + version);
                Assert.That((int)result["schemaVersion"], Is.EqualTo(7), "version " + version);
                foreach (JObject actor in (JArray)result["contestants"])
                    foreach (var field in CardFields)
                        Assert.That(actor.Property(field), Is.Not.Null, "version " + version + ", " + field);
            }
        }

        [Test]
        public void MigratingAgainIsANoOpAndTheResultLoadsAndValidates()
        {
            var old = CaptureV6(ContentCatalog.Create(601));
            var result = EpisodeSaveMigrations.PrepareCurrentPayload(old, out bool migrated);
            Assert.That(migrated, Is.True);

            var repeat = EpisodeSaveMigrations.PrepareCurrentPayload(result, out migrated);
            Assert.That(migrated, Is.False, "A schema 7 payload must not be migrated again.");
            Assert.That(ReferenceEquals(result, repeat), Is.False, "The payload must be cloned, not handed back.");
            Assert.That(JToken.DeepEquals(result, repeat), Is.True);

            var parsed = result.ToObject<EpisodeState>(Serializer());
            Assert.That(EpisodeValidation.TryValidate(parsed, out var error), Is.True, error);
            foreach (var actor in parsed.contestants)
            {
                Assert.That(actor.occupation, Is.Null);
                Assert.That(actor.archetype, Is.Null);
                Assert.That(actor.age, Is.Zero);
            }
        }

        /// <summary>
        /// A migrated save is readable end to end, the original bytes are left alone until an
        /// explicit save, and the message says what happened.
        /// </summary>
        [Test]
        public void AV6SaveOnDiskLoadsUnchangedAndOnlyRewritesOnAnExplicitSave()
        {
            using var files = new Files();
            files.Write(CaptureV6(ContentCatalog.Create(601)));
            var originalBytes = File.ReadAllBytes(files.Store.SavePath);

            Assert.That(files.Store.TryLoad(out var loaded, out string message), Is.True, message);
            Assert.That(message, Does.Contain("Schema 6").And.Contain("schema 7 in memory"));
            Assert.That(loaded.schemaVersion, Is.EqualTo(7));
            Assert.That(loaded.contestants, Has.All.Matches<ContestantState>(c => c.archetype == null && c.age == 0));
            Assert.That(File.ReadAllBytes(files.Store.SavePath), Is.EqualTo(originalBytes),
                "Loading must not rewrite the file.");

            files.Store.Save(loaded);
            Assert.That(File.ReadAllBytes(files.Store.BackupPath), Is.EqualTo(originalBytes),
                "The pre-migration bytes are kept as the backup.");
            Assert.That(files.Store.TryLoad(out var again, out message), Is.True, message);
            Assert.That(again.schemaVersion, Is.EqualTo(7));
            Assert.That(JToken.DeepEquals(Capture(loaded), Capture(again)), Is.True);
        }

        /// <summary>
        /// A v6 payload that was never legal stays rejected. The migration validates before it
        /// adds anything, so a damaged save cannot be repaired into a valid one by defaults.
        /// </summary>
        [TestCase("week", "101")]
        [TestCase("revision", "1000001")]
        [TestCase("playerStudyBonus", "6")]
        [TestCase("contestants[0].stats.luck", "10.1")]
        [TestCase("npcSocial", "null")]
        [TestCase("npcSocial.rulesStartWeek", "0")]
        [TestCase("blocRulesStartWeek", "0")]
        public void FrozenV6RejectsFormerlyInvalidValuesBeforeAddingAnyDefaults(string path, string value)
        {
            var old = CaptureV6(ContentCatalog.Create(601));
            old.SelectToken(path).Replace(JToken.Parse(value));

            string before = old.ToString(Formatting.None);
            Assert.Throws<InvalidDataException>(() => EpisodeSaveMigrations.UpgradeV6ToV7(old));
            Assert.That(old.ToString(Formatting.None), Is.EqualTo(before));
        }

        /// <summary>
        /// The frozen v6 shape does not follow the growing runtime DTO: a v6 payload carrying
        /// schema 7's own fields is not a v6 payload, and neither is one missing a v6 field.
        /// </summary>
        [Test]
        public void FrozenV6RejectsUnknownAndMissingFieldsOnEveryRow()
        {
            var valid = CaptureV6(ContentCatalog.Create(601));
            Assert.DoesNotThrow(() => EpisodeSaveMigrations.UpgradeV6ToV7(valid));

            var withCardCopy = CaptureV6(ContentCatalog.Create(601));
            ((JObject)withCardCopy["contestants"][0]).Add("archetype", "The Diplomat");
            Assert.Throws<InvalidDataException>(() => EpisodeSaveMigrations.UpgradeV6ToV7(withCardCopy));

            var paths = valid.DescendantsAndSelf().OfType<JObject>().Select(row => row.Path).ToArray();
            Assert.That(paths.Length, Is.GreaterThan(25));
            foreach (string path in paths)
            {
                var copy = (JObject)valid.DeepClone();
                var row = path.Length == 0 ? copy : (JObject)copy.SelectToken(path);
                row.Properties().Last().Remove();
                Assert.Throws<InvalidDataException>(() => EpisodeSaveMigrations.UpgradeV6ToV7(copy), path);
            }
        }

        /// <summary>
        /// The house sizes schema 6 grew to accept still migrate. Schema 6 was cut when a house held
        /// exactly six and later widened to three-to-sixteen without a version bump, so its frozen
        /// contract has to accept both eras or every large season already on disk becomes unreadable.
        /// </summary>
        [TestCase(3)] [TestCase(6)] [TestCase(8)] [TestCase(12)]
        public void EveryHouseSizeSchemaSixAcceptedStillMigrates(int size)
        {
            var built = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = size }, 77u);
            var old = CaptureV6(built);

            var result = EpisodeSaveMigrations.UpgradeV6ToV7(old);
            Assert.That(((JArray)result["contestants"]).Count, Is.EqualTo(size));

            var parsed = result.ToObject<EpisodeState>(Serializer());
            Assert.That(EpisodeValidation.TryValidate(parsed, out var error), Is.True, size + ": " + error);
        }

        // ---------------------------------------------------------------- fixtures

        /// <summary>The current runtime state expressed in schema 6's shape.</summary>
        private static JObject CaptureV6(EpisodeState state)
        {
            var payload = PersistenceMigrationTests.StripCardCopy(Capture(state));
            payload["schemaVersion"] = 6;
            return payload;
        }

        private static JObject Historical(int version)
        {
            if (version == 6) return CaptureV6(ContentCatalog.Create(601));
            var old = (JObject)typeof(PersistenceV6MigrationTests)
                .GetMethod("Historical", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, new object[] { version });
            if (version == 5) old["blocRulesStartWeek"] = 1;
            return old;
        }

        private static JObject Capture(EpisodeState state) => JObject.FromObject(state, Serializer());

        private static JsonSerializer Serializer() => (JsonSerializer)typeof(EpisodeSaveStore).Assembly
            .GetType("Gamesim.Persistence.SaveJson", true)
            .GetMethod("Serializer", BindingFlags.Public | BindingFlags.Static).Invoke(null, null);

        private sealed class Files : IDisposable
        {
            public readonly string DirectoryPath =
                Path.Combine(Path.GetTempPath(), "GamesimV7MigrationTests-" + Guid.NewGuid().ToString("N"));
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
