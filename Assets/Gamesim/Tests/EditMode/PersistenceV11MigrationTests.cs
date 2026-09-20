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
    /// Schema 11: bought action points, and things happening to the house.
    ///
    /// <para><b>One version carrying two systems, deliberately.</b> The social vocabulary needs a
    /// count of purchased actions and the event layer needs somewhere to keep events, and the save
    /// format checks stored objects field for field — so landing them separately would cost two
    /// migrations, two frozen contracts and two sweeps of every fixture, for the same work either
    /// way.</para>
    ///
    /// <para>Both arrive empty, because there is nothing in a schema 10 save to infer either from.
    /// <c>eventRulesStartWeek</c> is the fifth use of the rule-version boundary and exists for the
    /// same reason as the other four. <c>boughtActionPoints</c> has no boundary: it is a count of
    /// something nobody has done yet, and zero is not a rule but the truth about every save written
    /// before the control existed.</para>
    /// </summary>
    public sealed class PersistenceV11MigrationTests
    {
        [Test]
        public void AFreshSeasonHasBoughtNothingAndMayHaveEventsFromItsFirstWeek()
        {
            foreach (var state in new[] { ContentCatalog.Create(11u), SeasonBuilder.Create(new SeasonBuilder.Choice(), 11u) })
            {
                Assert.That(state.schemaVersion, Is.EqualTo(13));
                Assert.That(state.boughtActionPoints, Is.Zero);
                Assert.That(state.houseEvents, Is.Empty);
                Assert.That(state.eventRulesStartWeek, Is.EqualTo(1),
                    "New play gets the event layer immediately; only migrated history waits a week.");
            }
        }

        [TestCase(1)] [TestCase(2)] [TestCase(7)] [TestCase(40)]
        public void TheSavedWeekGetsNoEventsAndTheNextWeekMay(int week)
        {
            var before = CaptureV10(ContentCatalog.Create(601));
            before["week"] = week;

            var after = EpisodeSaveMigrations.UpgradeV10ToV11(before);
            Assert.That((int)after["schemaVersion"], Is.EqualTo(11));
            Assert.That((int)after["eventRulesStartWeek"], Is.EqualTo(week + 1));
            Assert.That((int)after["boughtActionPoints"], Is.Zero);
            Assert.That(after["houseEvents"], Is.Empty);

            var parsed = after.ToObject<EpisodeState>(Serializer());
            Assert.That(parsed.week, Is.LessThan(parsed.eventRulesStartWeek),
                "The week the save was in keeps the rules it was played under.");
            parsed.week = week + 1;
            Assert.That(parsed.week, Is.GreaterThanOrEqualTo(parsed.eventRulesStartWeek));
        }

        [TestCase(1)] [TestCase(6)] [TestCase(40)] [TestCase(100)]
        public void NoMigratedSeasonArrivesInvalid(int week)
        {
            var before = CaptureV10(ContentCatalog.Create(601));
            before["week"] = week;

            // The whole chain, not one step: what has to stay legal is what a load actually
            // produces. Validating a single step's output is what makes a test like this break on
            // every later version, which is exactly what happened to V9's and V10's.
            var parsed = EpisodeSaveMigrations.PrepareCurrentPayload(before, out _).ToObject<EpisodeState>(Serializer());
            Assert.That(EpisodeValidation.TryValidate(parsed, out var error), Is.True, error);
            Assert.That(parsed.eventRulesStartWeek, Is.EqualTo(Math.Min(101, week + 1)),
                "A season in its hundredth week may not name a start week validation would reject.");
        }

        [Test]
        public void TheUpgradeAddsOnlyTheThreeNewFieldsAndChangesNothingElse()
        {
            var before = CaptureV10(ContentCatalog.Create(601));
            string original = before.ToString(Formatting.None);

            var after = EpisodeSaveMigrations.UpgradeV10ToV11(before);

            Assert.That(before.ToString(Formatting.None), Is.EqualTo(original), "The source payload must not be mutated.");
            CollectionAssert.AreEquivalent(
                before.Properties().Select(p => p.Name)
                    .Concat(new[] { "boughtActionPoints", "houseEvents", "eventRulesStartWeek" }).ToArray(),
                after.Properties().Select(p => p.Name).ToArray());
            foreach (var property in before.Properties())
                if (property.Name != "schemaVersion")
                    Assert.That(JToken.DeepEquals(property.Value, after[property.Name]), Is.True, property.Name);
        }

        [Test]
        public void EveryHistoricalVersionMigratesAllTheWayToSchemaEleven()
        {
            for (int version = 1; version <= 10; version++)
            {
                var old = Historical(version);
                // PrepareV11Payload, not PrepareCurrentPayload: this file pins the step that ends
                // at schema 11, and "current" moved on when schema 12 opened storylines.
                var result = EpisodeSaveMigrations.PrepareV11Payload(old, out bool migrated);

                Assert.That(migrated, Is.True, "version " + version);
                Assert.That((int)result["schemaVersion"], Is.EqualTo(11), "version " + version);
                foreach (string field in new[] { "boughtActionPoints", "houseEvents", "eventRulesStartWeek" })
                    Assert.That(result.Property(field), Is.Not.Null, "version " + version + ", " + field);
            }
        }

        [Test]
        public void MigratingAgainIsANoOpAndTheResultLoadsAndValidates()
        {
            var old = CaptureV10(ContentCatalog.Create(601));
            var result = EpisodeSaveMigrations.PrepareV11Payload(old, out bool migrated);
            Assert.That(migrated, Is.True);

            var repeat = EpisodeSaveMigrations.PrepareV11Payload(result, out migrated);
            Assert.That(migrated, Is.False, "A schema 11 payload must not be migrated again.");
            Assert.That(ReferenceEquals(result, repeat), Is.False, "The payload must be cloned, not handed back.");
            Assert.That(JToken.DeepEquals(result, repeat), Is.True);

            var parsed = EpisodeSaveMigrations.PrepareCurrentPayload(old, out _).ToObject<EpisodeState>(Serializer());
            Assert.That(EpisodeValidation.TryValidate(parsed, out var error), Is.True, error);
            Assert.That(parsed.houseEvents, Is.Empty);
            Assert.That(parsed.boughtActionPoints, Is.Zero);
        }

        [Test]
        public void AV10SaveOnDiskLoadsUnchangedAndOnlyRewritesOnAnExplicitSave()
        {
            using var files = new Files();
            files.Write(CaptureV10(ContentCatalog.Create(601)));
            var originalBytes = File.ReadAllBytes(files.Store.SavePath);

            Assert.That(files.Store.TryLoad(out var loaded, out string message), Is.True, message);
            Assert.That(message, Does.Contain("Schema 10").And.Contain("schema 13 in memory"));
            Assert.That(loaded.schemaVersion, Is.EqualTo(13), "A load runs the whole chain, not one step.");
            Assert.That(loaded.houseEvents, Is.Empty);
            Assert.That(loaded.eventRulesStartWeek, Is.EqualTo(loaded.week + 1));
            Assert.That(File.ReadAllBytes(files.Store.SavePath), Is.EqualTo(originalBytes),
                "Loading must not rewrite the file.");

            files.Store.Save(loaded);
            Assert.That(File.ReadAllBytes(files.Store.BackupPath), Is.EqualTo(originalBytes));
            Assert.That(files.Store.TryLoad(out var again, out message), Is.True, message);
            Assert.That(again.eventRulesStartWeek, Is.EqualTo(loaded.eventRulesStartWeek));
        }

        /// <summary>
        /// A season carrying events survives the round trip with its choices intact — which is the
        /// whole reason the choices are stored rather than regenerated.
        /// </summary>
        [Test]
        public void AHouseFullOfEventsSurvivesBeingSavedAndLoaded()
        {
            using var files = new Files();
            var state = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 10 }, 5u);
            state.week = 3;
            state.boughtActionPoints = 2;
            state.houseEvents.Add(Event(state, "event-1", resolved: false));
            state.houseEvents.Add(Event(state, "event-2", resolved: true));

            files.Store.Save(state);
            Assert.That(files.Store.TryLoad(out var loaded, out string message), Is.True, message);
            Assert.That(loaded.boughtActionPoints, Is.EqualTo(2));
            CollectionAssert.AreEqual(state.houseEvents.Select(Describe).ToList(),
                loaded.houseEvents.Select(Describe).ToList());
        }

        // ---------------------------------------------------------------- what validation refuses

        [Test]
        public void AnUnresolvedEventCannotClaimAChoiceAndAResolvedOneMustNameOneThatExists()
        {
            var state = SeasonBuilder.Create(new SeasonBuilder.Choice(), 5u);
            var open = Event(state, "e", resolved: false);
            open.chosenIndex = 0;
            state.houseEvents.Add(open);
            Assert.That(EpisodeValidation.TryValidate(state, out _), Is.False,
                "An event nobody has answered cannot say what was chosen.");

            state.houseEvents.Clear();
            var done = Event(state, "e", resolved: true);
            done.chosenIndex = 9;
            state.houseEvents.Add(done);
            Assert.That(EpisodeValidation.TryValidate(state, out _), Is.False,
                "A resolved event cannot point at a choice that is not there.");
        }

        [TestCase("kind")] [TestCase("risk")] [TestCase("week")] [TestCase("bought")]
        [TestCase("duplicate")] [TestCase("impact")] [TestCase("stranger")]
        public void ASeasonWithBadEventDataDoesNotValidate(string damage)
        {
            var state = SeasonBuilder.Create(new SeasonBuilder.Choice(), 5u);
            var item = Event(state, "e", resolved: false);
            state.houseEvents.Add(item);
            switch (damage)
            {
                case "kind": item.kind = "not-a-kind"; break;
                case "risk": item.choices[0].risk = "catastrophic"; break;
                case "week": item.week = state.week + 1; break;
                case "bought": state.boughtActionPoints = -1; break;
                case "duplicate": state.houseEvents.Add(Event(state, "e", resolved: false)); break;
                case "impact": item.choices[0].impacts[0].amount = double.NaN; break;
                case "stranger": item.choices[0].impacts[0].targetId = "nobody-by-that-name"; break;
            }
            Assert.That(EpisodeValidation.TryValidate(state, out _), Is.False, damage);
        }

        [TestCase("week", "101")]
        [TestCase("revision", "1000001")]
        [TestCase("dealRulesStartWeek", "0")]
        [TestCase("socialBudgetRulesStartWeek", "0")]
        [TestCase("contestants[0].stats.luck", "10.1")]
        public void FrozenV10RejectsFormerlyInvalidValuesBeforeAddingAnyDefaults(string path, string value)
        {
            var old = CaptureV10(ContentCatalog.Create(601));
            old.SelectToken(path).Replace(JToken.Parse(value));

            string before = old.ToString(Formatting.None);
            Assert.Throws<InvalidDataException>(() => EpisodeSaveMigrations.UpgradeV10ToV11(old));
            Assert.That(old.ToString(Formatting.None), Is.EqualTo(before));
        }

        [Test]
        public void FrozenV10RejectsUnknownAndMissingFieldsOnEveryRow()
        {
            var valid = CaptureV10(ContentCatalog.Create(601));
            Assert.DoesNotThrow(() => EpisodeSaveMigrations.UpgradeV10ToV11(valid));

            foreach (string field in new[] { "boughtActionPoints", "houseEvents", "eventRulesStartWeek" })
            {
                var ahead = CaptureV10(ContentCatalog.Create(601));
                ahead.Add(field, field == "houseEvents" ? (JToken)new JArray() : 1);
                Assert.Throws<InvalidDataException>(() => EpisodeSaveMigrations.UpgradeV10ToV11(ahead), field);
            }

            var paths = valid.DescendantsAndSelf().OfType<JObject>().Select(row => row.Path).ToArray();
            Assert.That(paths.Length, Is.GreaterThan(25));
            foreach (string path in paths)
            {
                var copy = (JObject)valid.DeepClone();
                var row = path.Length == 0 ? copy : (JObject)copy.SelectToken(path);
                row.Properties().Last().Remove();
                Assert.Throws<InvalidDataException>(() => EpisodeSaveMigrations.UpgradeV10ToV11(copy), path);
            }
        }

        /// <summary>A v10 save carrying deals still migrates, and carries them across.</summary>
        [Test]
        public void ThePreviousVersionsDealsSurviveTheUpgrade()
        {
            var state = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 10 }, 5u);
            state.week = 3;
            foreach (var edge in state.relationships) edge.score = 60;
            NpcDeals.Settle(state);
            Assert.That(state.deals, Is.Not.Empty, "The fixture needs deals to carry.");

            var payload = CaptureV10(state);
            // The whole chain, not one step: a schema 11 payload no longer describes a state the
            // simulation accepts. Validating a step's output is what makes these break on every
            // later version, which has now happened three times.
            var parsed = EpisodeSaveMigrations.PrepareCurrentPayload(payload, out _)
                .ToObject<EpisodeState>(Serializer());

            Assert.That(parsed.deals, Has.Count.EqualTo(state.deals.Count));
            Assert.That(EpisodeValidation.TryValidate(parsed, out string error), Is.True, error);
        }

        // ---------------------------------------------------------------- fixtures

        private static string Describe(HouseEventState e) => string.Join("|",
            e.id, e.kind, e.title, e.narrative, string.Join(",", e.involvedIds),
            e.week.ToString(), e.resolved.ToString(), e.chosenIndex.ToString(), e.outcome,
            string.Join(";", e.choices.Select(c => c.label + ":" + c.risk + ":" + c.trustChange
                + ":" + string.Join("/", c.impacts.Select(i => i.targetId + "=" + i.amount)))));

        private static HouseEventState Event(EpisodeState state, string id, bool resolved)
        {
            var other = state.contestants.First(c => !c.isPlayer).id;
            return new HouseEventState
            {
                id = id, kind = HouseEventKind.House, title = "A disagreement",
                narrative = "Two housemates fell out over the washing up.",
                involvedIds = new List<string> { state.playerId, other },
                week = state.week, resolved = resolved, chosenIndex = resolved ? 0 : -1,
                outcome = resolved ? "You stayed out of it." : null,
                choices = new List<HouseEventChoice>
                {
                    new HouseEventChoice
                    {
                        label = "Stay out of it", description = "Let them sort it out.",
                        risk = HouseEventRisk.Low, trustChange = 0,
                        impacts = new List<HouseEventImpact>
                        {
                            new HouseEventImpact { targetId = other, amount = -2 },
                        },
                    },
                    new HouseEventChoice
                    {
                        label = "Take a side", description = "Back one of them, publicly.",
                        risk = HouseEventRisk.High, trustChange = -5,
                        impacts = new List<HouseEventImpact>
                        {
                            new HouseEventImpact { targetId = other, amount = 12 },
                        },
                    },
                },
            };
        }

        /// <summary>The current runtime state expressed in schema 10's shape.</summary>
        private static JObject CaptureV10(EpisodeState state)
        {
            var payload = PersistenceMigrationTests.StripSchema11(PersistenceMigrationTests.StripSchema12(Capture(state)));
            payload["schemaVersion"] = 10;
            return payload;
        }

        private static JObject Historical(int version)
        {
            if (version == 10) return CaptureV10(ContentCatalog.Create(601));
            return (JObject)typeof(PersistenceV10MigrationTests)
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
                Path.Combine(Path.GetTempPath(), "GamesimV11MigrationTests-" + Guid.NewGuid().ToString("N"));
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
