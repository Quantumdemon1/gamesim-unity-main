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
    /// Schema 10: deals, and the week houseguests may start striking them.
    ///
    /// <para>The list itself arrives empty and could not arrive any other way — there is nothing in a
    /// schema 9 save to infer a bargain from, because the field never existed. What the boundary
    /// buys is not the list but the behaviour: a season restored mid-week should not suddenly have
    /// the house agreeing pacts it was not being played under. This is the fourth use of that
    /// boundary, after <c>blocRulesStartWeek</c>, <c>npcSocial.rulesStartWeek</c> and
    /// <c>socialBudgetRulesStartWeek</c>, and these tests are the same shape as theirs.</para>
    /// </summary>
    public sealed class PersistenceV10MigrationTests
    {
        [Test]
        public void AFreshSeasonMayDealFromItsFirstWeek()
        {
            foreach (var state in new[] { ContentCatalog.Create(11u), SeasonBuilder.Create(new SeasonBuilder.Choice(), 11u) })
            {
                Assert.That(state.schemaVersion, Is.EqualTo(11));
                Assert.That(state.deals, Is.Empty, "A season starts with nothing agreed.");
                Assert.That(state.dealRulesStartWeek, Is.EqualTo(1),
                    "New play deals immediately; only migrated history gets a grace week.");
            }
        }

        [TestCase(1)] [TestCase(2)] [TestCase(7)] [TestCase(40)]
        public void TheSavedWeekStrikesNothingAndTheNextWeekMay(int week)
        {
            var before = CaptureV9(ContentCatalog.Create(601));
            before["week"] = week;

            var after = EpisodeSaveMigrations.UpgradeV9ToV10(before);
            Assert.That((int)after["schemaVersion"], Is.EqualTo(10));
            Assert.That((int)after["dealRulesStartWeek"], Is.EqualTo(week + 1));

            var parsed = after.ToObject<EpisodeState>(Serializer());
            NpcDeals.Settle(parsed);
            Assert.That(parsed.deals, Is.Empty, "The week the save was in keeps the rules it was played under.");

            parsed.week = week + 1;
            foreach (var edge in parsed.relationships) edge.score = 60;
            NpcDeals.Settle(parsed);
            Assert.That(parsed.deals, Is.Not.Empty, "The next week is under the ported rule.");
        }

        /// <summary>A migrated season is valid the moment it lands, before anything touches it.</summary>
        [TestCase(1)] [TestCase(6)] [TestCase(40)] [TestCase(100)]
        public void NoMigratedSeasonArrivesInvalid(int week)
        {
            var before = CaptureV9(ContentCatalog.Create(601));
            before["week"] = week;

            // The whole chain, not one step. A schema 10 payload no longer describes a state the
            // simulation accepts, and what has to stay legal is what a load actually produces —
            // which is also what stops this test breaking again on the next version.
            var parsed = EpisodeSaveMigrations.PrepareCurrentPayload(before, out _).ToObject<EpisodeState>(Serializer());
            Assert.That(EpisodeValidation.TryValidate(parsed, out var error), Is.True, error);
            Assert.That(parsed.dealRulesStartWeek, Is.EqualTo(Math.Min(101, week + 1)),
                "A season in its hundredth week may not name a start week validation would reject.");
        }

        [Test]
        public void TheUpgradeAddsOnlyDealsAndTheirBoundaryAndChangesNothingElse()
        {
            var before = CaptureV9(ContentCatalog.Create(601));
            string original = before.ToString(Formatting.None);

            var after = EpisodeSaveMigrations.UpgradeV9ToV10(before);

            Assert.That(before.ToString(Formatting.None), Is.EqualTo(original), "The source payload must not be mutated.");
            CollectionAssert.AreEquivalent(
                before.Properties().Select(p => p.Name).Concat(new[] { "deals", "dealRulesStartWeek" }).ToArray(),
                after.Properties().Select(p => p.Name).ToArray());
            foreach (var property in before.Properties())
                if (property.Name != "schemaVersion")
                    Assert.That(JToken.DeepEquals(property.Value, after[property.Name]), Is.True, property.Name);
        }

        [Test]
        public void EveryHistoricalVersionMigratesAllTheWayToSchemaTen()
        {
            for (int version = 1; version <= 9; version++)
            {
                var old = Historical(version);
                // PrepareV10Payload, not PrepareCurrentPayload: this file pins the step that ends
                // at schema 10, and "current" moved on when schema 11 opened the event layer.
                var result = EpisodeSaveMigrations.PrepareV10Payload(old, out bool migrated);

                Assert.That(migrated, Is.True, "version " + version);
                Assert.That((int)result["schemaVersion"], Is.EqualTo(10), "version " + version);
                Assert.That(result.Property("deals"), Is.Not.Null, "version " + version);
                Assert.That(result.Property("dealRulesStartWeek"), Is.Not.Null, "version " + version);
            }
        }

        [Test]
        public void MigratingAgainIsANoOpAndTheResultLoadsAndValidates()
        {
            var old = CaptureV9(ContentCatalog.Create(601));
            var result = EpisodeSaveMigrations.PrepareV10Payload(old, out bool migrated);
            Assert.That(migrated, Is.True);

            var repeat = EpisodeSaveMigrations.PrepareV10Payload(result, out migrated);
            Assert.That(migrated, Is.False, "A schema 10 payload must not be migrated again.");
            Assert.That(ReferenceEquals(result, repeat), Is.False, "The payload must be cloned, not handed back.");
            Assert.That(JToken.DeepEquals(result, repeat), Is.True);

            var parsed = EpisodeSaveMigrations.PrepareCurrentPayload(old, out _).ToObject<EpisodeState>(Serializer());
            Assert.That(EpisodeValidation.TryValidate(parsed, out var error), Is.True, error);
            Assert.That(parsed.deals, Is.Empty);
        }

        [Test]
        public void AV9SaveOnDiskLoadsUnchangedAndOnlyRewritesOnAnExplicitSave()
        {
            using var files = new Files();
            files.Write(CaptureV9(ContentCatalog.Create(601)));
            var originalBytes = File.ReadAllBytes(files.Store.SavePath);

            Assert.That(files.Store.TryLoad(out var loaded, out string message), Is.True, message);
            Assert.That(message, Does.Contain("Schema 9").And.Contain("schema 11 in memory"));
            Assert.That(loaded.schemaVersion, Is.EqualTo(11), "A load runs the whole chain, not one step.");
            Assert.That(loaded.deals, Is.Empty);
            Assert.That(loaded.dealRulesStartWeek, Is.EqualTo(loaded.week + 1));
            Assert.That(File.ReadAllBytes(files.Store.SavePath), Is.EqualTo(originalBytes),
                "Loading must not rewrite the file.");

            files.Store.Save(loaded);
            Assert.That(File.ReadAllBytes(files.Store.BackupPath), Is.EqualTo(originalBytes));
            Assert.That(files.Store.TryLoad(out var again, out message), Is.True, message);
            Assert.That(again.dealRulesStartWeek, Is.EqualTo(loaded.dealRulesStartWeek));
        }

        /// <summary>A season that has agreed things survives the round trip with them intact.</summary>
        [Test]
        public void AHouseFullOfBargainsSurvivesBeingSavedAndLoaded()
        {
            using var files = new Files();
            var state = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 10 }, 5u);
            state.week = 3;
            foreach (var edge in state.relationships) edge.score = 60;
            NpcDeals.Settle(state);
            Assert.That(state.deals, Is.Not.Empty, "The fixture needs something to round-trip.");

            files.Store.Save(state);
            Assert.That(files.Store.TryLoad(out var loaded, out string message), Is.True, message);
            CollectionAssert.AreEqual(
                state.deals.Select(Describe).ToList(), loaded.deals.Select(Describe).ToList());
        }

        [TestCase("week", "101")]
        [TestCase("revision", "1000001")]
        [TestCase("socialBudgetRulesStartWeek", "0")]
        [TestCase("outOfPhaseSocialActions", "19")]
        [TestCase("contestants[0].stats.luck", "10.1")]
        [TestCase("blocRulesStartWeek", "0")]
        public void FrozenV9RejectsFormerlyInvalidValuesBeforeAddingAnyDefaults(string path, string value)
        {
            var old = CaptureV9(ContentCatalog.Create(601));
            old.SelectToken(path).Replace(JToken.Parse(value));

            string before = old.ToString(Formatting.None);
            Assert.Throws<InvalidDataException>(() => EpisodeSaveMigrations.UpgradeV9ToV10(old));
            Assert.That(old.ToString(Formatting.None), Is.EqualTo(before));
        }

        /// <summary>
        /// The frozen v9 shape does not follow the growing runtime DTO: a v9 payload carrying schema
        /// 10's fields is not a v9 payload, and neither is one missing a v9 field.
        /// </summary>
        [Test]
        public void FrozenV9RejectsUnknownAndMissingFieldsOnEveryRow()
        {
            var valid = CaptureV9(ContentCatalog.Create(601));
            Assert.DoesNotThrow(() => EpisodeSaveMigrations.UpgradeV9ToV10(valid));

            foreach (string field in new[] { "deals", "dealRulesStartWeek" })
            {
                var ahead = CaptureV9(ContentCatalog.Create(601));
                ahead.Add(field, field == "deals" ? (JToken)new JArray() : 1);
                Assert.Throws<InvalidDataException>(() => EpisodeSaveMigrations.UpgradeV9ToV10(ahead), field);
            }

            var paths = valid.DescendantsAndSelf().OfType<JObject>().Select(row => row.Path).ToArray();
            Assert.That(paths.Length, Is.GreaterThan(25));
            foreach (string path in paths)
            {
                var copy = (JObject)valid.DeepClone();
                var row = path.Length == 0 ? copy : (JObject)copy.SelectToken(path);
                row.Properties().Last().Remove();
                Assert.Throws<InvalidDataException>(() => EpisodeSaveMigrations.UpgradeV9ToV10(copy), path);
            }
        }

        /// <summary>
        /// The stored shape is checked field for field, so a deal row that has grown or lost a field
        /// is refused rather than quietly loaded half-populated.
        /// </summary>
        [Test]
        public void ADealRowIsCheckedFieldForFieldLikeEveryOtherRow()
        {
            using var files = new Files();
            var state = ContentCatalog.Create(601);
            state.deals.Add(new DealState
            {
                id = "deal-1", type = DealKind.FinalTwo, proposerId = state.contestants[1].id,
                recipientId = state.contestants[2].id, status = DealStatus.Active,
                week = state.week, trustImpact = DealTrust.Critical,
            });

            var payload = Capture(state);
            Assert.DoesNotThrow(() => files.Write(payload));
            Assert.That(files.Store.TryLoad(out var loaded, out string message), Is.True, message);
            Assert.That(loaded.deals, Has.Count.EqualTo(1));

            foreach (var damage in new Action<JObject>[]
                     {
                         row => row.Add("leverage", 3),
                         row => row.Property("trustImpact").Remove(),
                         row => row["week"] = "3",
                     })
            {
                var broken = (JObject)payload.DeepClone();
                damage((JObject)broken["deals"][0]);
                using var scratch = new Files();
                scratch.Write(broken);
                Assert.That(scratch.Store.TryLoad(out var rejected, out _), Is.False);
                Assert.That(rejected, Is.Null);
            }
        }

        // ---------------------------------------------------------------- fixtures

        private static string Describe(DealState d) =>
            string.Join("|", d.id, d.type, d.proposerId, d.recipientId, d.targetId, d.status,
                d.week.ToString(), d.expiresWeek.ToString(), d.trustImpact);

        /// <summary>The current runtime state expressed in schema 9's shape.</summary>
        private static JObject CaptureV9(EpisodeState state)
        {
            var payload = PersistenceMigrationTests.StripSchema10(PersistenceMigrationTests.StripSchema11(Capture(state)));
            payload["schemaVersion"] = 9;
            return payload;
        }

        private static JObject Historical(int version)
        {
            if (version == 9) return CaptureV9(ContentCatalog.Create(601));
            return (JObject)typeof(PersistenceV9MigrationTests)
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
                Path.Combine(Path.GetTempPath(), "GamesimV10MigrationTests-" + Guid.NewGuid().ToString("N"));
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
