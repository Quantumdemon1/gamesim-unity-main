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
    /// Schema 9: the week the social-action budget starts following the cast.
    ///
    /// <para>The allowance was a flat eighteen and is now half the active house, rounded up. Unlike
    /// every other field a migration has added here, this one is <b>not</b> empty on arrival and is
    /// not merely derived — it is a promise. A season saved mid-week was played under the old
    /// allowance, and applying the new one to that week would retroactively overspend it: someone
    /// who legally took six actions loads to find the limit was three, with no way to give them
    /// back. The saved week keeps what it was played under and the new rule starts with the next.
    /// </para>
    ///
    /// <para>This is the third use of that boundary — <c>blocRulesStartWeek</c> and the NPC social
    /// subsystem are the other two — and the tests below are the same shape as theirs.</para>
    /// </summary>
    public sealed class PersistenceV9MigrationTests
    {
        [Test]
        public void AFreshSeasonIsUnderThePortedRuleFromItsFirstConversation()
        {
            foreach (var state in new[] { ContentCatalog.Create(11u), SeasonBuilder.Create(new SeasonBuilder.Choice(), 11u) })
            {
                Assert.That(state.schemaVersion, Is.EqualTo(13));
                Assert.That(state.socialBudgetRulesStartWeek, Is.EqualTo(1),
                    "New play gets the ported budget immediately; only migrated history gets a grace week.");
                Assert.That(EpisodeEngine.SocialActionBudget(state),
                    Is.EqualTo((int)Math.Ceiling(state.Active.Count() / 2.0)));
            }
        }

        /// <summary>
        /// The whole point of the field: the week a save was in keeps its old allowance, and the
        /// week after it does not.
        /// </summary>
        [TestCase(1)] [TestCase(2)] [TestCase(7)] [TestCase(40)]
        public void TheSavedWeekKeepsItsOldAllowanceAndTheNextWeekDoesNot(int week)
        {
            var before = CaptureV8(ContentCatalog.Create(601));
            before["week"] = week;

            var after = EpisodeSaveMigrations.UpgradeV8ToV9(before);
            Assert.That((int)after["schemaVersion"], Is.EqualTo(9));
            Assert.That((int)after["socialBudgetRulesStartWeek"], Is.EqualTo(week + 1));

            var parsed = after.ToObject<EpisodeState>(Serializer());
            Assert.That(EpisodeEngine.SocialActionBudget(parsed), Is.EqualTo(EpisodeEngine.LegacySocialActionBudget),
                "The week the save was in must keep the allowance it was played under.");

            parsed.week = week + 1;
            Assert.That(EpisodeEngine.SocialActionBudget(parsed),
                Is.EqualTo((int)Math.Ceiling(parsed.Active.Count() / 2.0)),
                "The next week is under the ported rule.");
        }

        /// <summary>
        /// A migrated season cannot be over budget on arrival, whatever it had already spent. That
        /// is the failure the boundary exists to prevent.
        /// </summary>
        [TestCase(0)] [TestCase(3)] [TestCase(9)] [TestCase(18)]
        public void NoMigratedSeasonArrivesAlreadyOverItsBudget(int spent)
        {
            var before = CaptureV8(ContentCatalog.Create(601));
            before["socialActions"] = spent;

            // The whole chain, not one step: a schema 9 payload no longer describes a state the
            // simulation will accept, and what has to stay legal is what a load actually produces.
            var parsed = EpisodeSaveMigrations.PrepareCurrentPayload(before, out _).ToObject<EpisodeState>(Serializer());
            Assert.That(EpisodeValidation.TryValidate(parsed, out var error), Is.True, error);
            Assert.That(EpisodeEngine.SocialActionsSpent(parsed),
                Is.LessThanOrEqualTo(EpisodeEngine.SocialActionBudget(parsed)),
                spent + " actions were legal when this season was saved and must not become illegal on load.");
        }

        [Test]
        public void TheUpgradeAddsOnlyTheBoundaryAndChangesNothingElse()
        {
            var before = CaptureV8(ContentCatalog.Create(601));
            string original = before.ToString(Formatting.None);

            var after = EpisodeSaveMigrations.UpgradeV8ToV9(before);

            Assert.That(before.ToString(Formatting.None), Is.EqualTo(original), "The source payload must not be mutated.");
            CollectionAssert.AreEquivalent(
                before.Properties().Select(p => p.Name).Concat(new[] { "socialBudgetRulesStartWeek" }).ToArray(),
                after.Properties().Select(p => p.Name).ToArray());
            foreach (var property in before.Properties())
                if (property.Name != "schemaVersion")
                    Assert.That(JToken.DeepEquals(property.Value, after[property.Name]), Is.True, property.Name);
        }

        [Test]
        public void EveryHistoricalVersionMigratesAllTheWayToSchemaNine()
        {
            for (int version = 1; version <= 8; version++)
            {
                var old = Historical(version);
                // PrepareV9Payload, not PrepareCurrentPayload: this file pins the step that ends at
                // schema 9, and "current" moved on when schema 10 opened deals.
                var result = EpisodeSaveMigrations.PrepareV9Payload(old, out bool migrated);

                Assert.That(migrated, Is.True, "version " + version);
                Assert.That((int)result["schemaVersion"], Is.EqualTo(9), "version " + version);
                Assert.That(result.Property("socialBudgetRulesStartWeek"), Is.Not.Null, "version " + version);
            }
        }

        [Test]
        public void MigratingAgainIsANoOpAndTheResultLoadsAndValidates()
        {
            var old = CaptureV8(ContentCatalog.Create(601));
            var result = EpisodeSaveMigrations.PrepareV9Payload(old, out bool migrated);
            Assert.That(migrated, Is.True);

            var repeat = EpisodeSaveMigrations.PrepareV9Payload(result, out migrated);
            Assert.That(migrated, Is.False, "A schema 9 payload must not be migrated again.");
            Assert.That(ReferenceEquals(result, repeat), Is.False);
            Assert.That(JToken.DeepEquals(result, repeat), Is.True);

            var parsed = EpisodeSaveMigrations.PrepareCurrentPayload(old, out _).ToObject<EpisodeState>(Serializer());
            Assert.That(EpisodeValidation.TryValidate(parsed, out var error), Is.True, error);
        }

        [Test]
        public void AV8SaveOnDiskLoadsUnchangedAndOnlyRewritesOnAnExplicitSave()
        {
            using var files = new Files();
            files.Write(CaptureV8(ContentCatalog.Create(601)));
            var originalBytes = File.ReadAllBytes(files.Store.SavePath);

            Assert.That(files.Store.TryLoad(out var loaded, out string message), Is.True, message);
            Assert.That(message, Does.Contain("Schema 8").And.Contain("schema 13 in memory"));
            Assert.That(loaded.schemaVersion, Is.EqualTo(13), "A load runs the whole chain, not one step.");
            Assert.That(loaded.socialBudgetRulesStartWeek, Is.EqualTo(loaded.week + 1));
            Assert.That(File.ReadAllBytes(files.Store.SavePath), Is.EqualTo(originalBytes),
                "Loading must not rewrite the file.");

            files.Store.Save(loaded);
            Assert.That(File.ReadAllBytes(files.Store.BackupPath), Is.EqualTo(originalBytes));
            Assert.That(files.Store.TryLoad(out var again, out message), Is.True, message);
            Assert.That(again.socialBudgetRulesStartWeek, Is.EqualTo(loaded.socialBudgetRulesStartWeek));
        }

        [TestCase("week", "101")]
        [TestCase("revision", "1000001")]
        [TestCase("outOfPhaseSocialActions", "19")]
        [TestCase("evictionStage", "5")]
        [TestCase("contestants[0].stats.luck", "10.1")]
        [TestCase("blocRulesStartWeek", "0")]
        public void FrozenV8RejectsFormerlyInvalidValuesBeforeAddingAnyDefaults(string path, string value)
        {
            var old = CaptureV8(ContentCatalog.Create(601));
            old.SelectToken(path).Replace(JToken.Parse(value));

            string before = old.ToString(Formatting.None);
            Assert.Throws<InvalidDataException>(() => EpisodeSaveMigrations.UpgradeV8ToV9(old));
            Assert.That(old.ToString(Formatting.None), Is.EqualTo(before));
        }

        /// <summary>
        /// The frozen v8 shape does not follow the growing runtime DTO: a v8 payload carrying schema
        /// 9's field is not a v8 payload, and neither is one missing a v8 field.
        /// </summary>
        [Test]
        public void FrozenV8RejectsUnknownAndMissingFieldsOnEveryRow()
        {
            var valid = CaptureV8(ContentCatalog.Create(601));
            Assert.DoesNotThrow(() => EpisodeSaveMigrations.UpgradeV8ToV9(valid));

            var ahead = CaptureV8(ContentCatalog.Create(601));
            ahead.Add("socialBudgetRulesStartWeek", 1);
            Assert.Throws<InvalidDataException>(() => EpisodeSaveMigrations.UpgradeV8ToV9(ahead));

            var paths = valid.DescendantsAndSelf().OfType<JObject>().Select(row => row.Path).ToArray();
            Assert.That(paths.Length, Is.GreaterThan(25));
            foreach (string path in paths)
            {
                var copy = (JObject)valid.DeepClone();
                var row = path.Length == 0 ? copy : (JObject)copy.SelectToken(path);
                row.Properties().Last().Remove();
                Assert.Throws<InvalidDataException>(() => EpisodeSaveMigrations.UpgradeV8ToV9(copy), path);
            }
        }

        // ---------------------------------------------------------------- fixtures

        /// <summary>The current runtime state expressed in schema 8's shape.</summary>
        private static JObject CaptureV8(EpisodeState state)
        {
            var payload = PersistenceMigrationTests.StripSchema9(PersistenceMigrationTests.StripSchema10(PersistenceMigrationTests.StripSchema11(PersistenceMigrationTests.StripSchema12(Capture(state)))));
            payload["schemaVersion"] = 8;
            return payload;
        }

        private static JObject Historical(int version)
        {
            if (version == 8) return CaptureV8(ContentCatalog.Create(601));
            return (JObject)typeof(PersistenceV8MigrationTests)
                .GetMethod("Historical", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, new object[] { version });
        }

        private static JObject Capture(EpisodeState state) => JObject.FromObject(state, Serializer());

        private static JsonSerializer Serializer() => (JsonSerializer)typeof(EpisodeSaveStore).Assembly
            .GetType("Gamesim.Persistence.SaveJson", true)
            .GetMethod("Serializer", BindingFlags.Public | BindingFlags.Static).Invoke(null, null);

        private sealed class Files : IDisposable
        {
            public readonly string DirectoryPath =
                Path.Combine(Path.GetTempPath(), "GamesimV9MigrationTests-" + Guid.NewGuid().ToString("N"));
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
