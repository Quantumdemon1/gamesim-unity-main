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
    /// Schema 12: storylines, and what their choices leave behind.
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
    public sealed class PersistenceV12MigrationTests
    {
        [Test]
        public void AFreshSeasonHasBoughtNothingAndMayHaveEventsFromItsFirstWeek()
        {
            foreach (var state in new[] { ContentCatalog.Create(11u), SeasonBuilder.Create(new SeasonBuilder.Choice(), 11u) })
            {
                Assert.That(state.schemaVersion, Is.EqualTo(13));
                Assert.That(state.storylines, Is.Empty);
                Assert.That(state.activeModifiers, Is.Empty);
                Assert.That(state.storyRulesStartWeek, Is.EqualTo(1),
                    "New play gets the event layer immediately; only migrated history waits a week.");
            }
        }

        [TestCase(1)] [TestCase(2)] [TestCase(7)] [TestCase(40)]
        public void TheSavedWeekTellsNoStoriesAndTheNextWeekMay(int week)
        {
            var before = CaptureV11(ContentCatalog.Create(601));
            before["week"] = week;

            var after = EpisodeSaveMigrations.UpgradeV11ToV12(before);
            Assert.That((int)after["schemaVersion"], Is.EqualTo(12));
            Assert.That((int)after["storyRulesStartWeek"], Is.EqualTo(week + 1));
            Assert.That((int)after["storylines"].Count(), Is.Zero);
            Assert.That(after["activeModifiers"], Is.Empty);

            var parsed = after.ToObject<EpisodeState>(Serializer());
            Assert.That(parsed.week, Is.LessThan(parsed.storyRulesStartWeek),
                "The week the save was in keeps the rules it was played under.");
            parsed.week = week + 1;
            Assert.That(parsed.week, Is.GreaterThanOrEqualTo(parsed.storyRulesStartWeek));
        }

        [TestCase(1)] [TestCase(6)] [TestCase(40)] [TestCase(100)]
        public void NoMigratedSeasonArrivesInvalid(int week)
        {
            var before = CaptureV11(ContentCatalog.Create(601));
            before["week"] = week;

            // The whole chain, not one step: what has to stay legal is what a load actually
            // produces. Validating a single step's output is what makes a test like this break on
            // every later version, which is exactly what happened to V9's and V10's.
            var parsed = EpisodeSaveMigrations.PrepareCurrentPayload(before, out _).ToObject<EpisodeState>(Serializer());
            Assert.That(EpisodeValidation.TryValidate(parsed, out var error), Is.True, error);
            Assert.That(parsed.storyRulesStartWeek, Is.EqualTo(Math.Min(101, week + 1)),
                "A season in its hundredth week may not name a start week validation would reject.");
        }

        [Test]
        public void TheUpgradeAddsOnlyTheThreeNewFieldsAndChangesNothingElse()
        {
            var before = CaptureV11(ContentCatalog.Create(601));
            string original = before.ToString(Formatting.None);

            var after = EpisodeSaveMigrations.UpgradeV11ToV12(before);

            Assert.That(before.ToString(Formatting.None), Is.EqualTo(original), "The source payload must not be mutated.");
            CollectionAssert.AreEquivalent(
                before.Properties().Select(p => p.Name)
                    .Concat(new[] { "storylines", "activeModifiers", "storyRulesStartWeek" }).ToArray(),
                after.Properties().Select(p => p.Name).ToArray());
            foreach (var property in before.Properties())
                if (property.Name != "schemaVersion")
                    Assert.That(JToken.DeepEquals(property.Value, after[property.Name]), Is.True, property.Name);
        }

        [Test]
        public void EveryHistoricalVersionMigratesAllTheWayToSchemaTwelve()
        {
            for (int version = 1; version <= 11; version++)
            {
                var old = Historical(version);
                // Preserve this historical dispatch even when the current schema advances.
                var result = EpisodeSaveMigrations.PrepareV12Payload(old, out bool migrated);

                Assert.That(migrated, Is.True, "version " + version);
                Assert.That((int)result["schemaVersion"], Is.EqualTo(12), "version " + version);
                foreach (string field in new[] { "storylines", "activeModifiers", "storyRulesStartWeek" })
                    Assert.That(result.Property(field), Is.Not.Null, "version " + version + ", " + field);
            }
        }

        [Test]
        public void MigratingAgainIsANoOpAndTheResultLoadsAndValidates()
        {
            var old = CaptureV11(ContentCatalog.Create(601));
            var result = EpisodeSaveMigrations.PrepareCurrentPayload(old, out bool migrated);
            Assert.That(migrated, Is.True);

            var repeat = EpisodeSaveMigrations.PrepareCurrentPayload(result, out migrated);
            Assert.That(migrated, Is.False, "A current payload must not be migrated again.");
            Assert.That(ReferenceEquals(result, repeat), Is.False, "The payload must be cloned, not handed back.");
            Assert.That(JToken.DeepEquals(result, repeat), Is.True);

            var parsed = EpisodeSaveMigrations.PrepareCurrentPayload(old, out _).ToObject<EpisodeState>(Serializer());
            Assert.That(EpisodeValidation.TryValidate(parsed, out var error), Is.True, error);
            Assert.That(parsed.storylines, Is.Empty);
            Assert.That(parsed.activeModifiers, Is.Empty);
        }

        [Test]
        public void AV11SaveOnDiskLoadsUnchangedAndOnlyRewritesOnAnExplicitSave()
        {
            using var files = new Files();
            files.Write(CaptureV11(ContentCatalog.Create(601)));
            var originalBytes = File.ReadAllBytes(files.Store.SavePath);

            Assert.That(files.Store.TryLoad(out var loaded, out string message), Is.True, message);
            Assert.That(message, Does.Contain("Schema 11").And.Contain("schema 13 in memory"));
            Assert.That(loaded.schemaVersion, Is.EqualTo(13), "A load runs the whole chain, not one step.");
            Assert.That(loaded.storylines, Is.Empty);
            Assert.That(loaded.storyRulesStartWeek, Is.EqualTo(loaded.week + 1));
            Assert.That(File.ReadAllBytes(files.Store.SavePath), Is.EqualTo(originalBytes),
                "Loading must not rewrite the file.");

            files.Store.Save(loaded);
            Assert.That(File.ReadAllBytes(files.Store.BackupPath), Is.EqualTo(originalBytes));
            Assert.That(files.Store.TryLoad(out var again, out message), Is.True, message);
            Assert.That(again.storyRulesStartWeek, Is.EqualTo(loaded.storyRulesStartWeek));
        }

        /// <summary>
        /// A season carrying storylines survives the round trip with its modifiers intact.
        /// </summary>
        [Test]
        public void AHouseFullOfStoriesSurvivesBeingSavedAndLoaded()
        {
            using var files = new Files();
            var state = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 10 }, 5u);
            state.week = 3;
            Assert.That(Storylines.Begin(state, 0.1, 1, out var story, out var chapter), Is.True);
            state.storylines.Add(story);
            state.houseEvents.Add(chapter);
            state.activeModifiers.Add(new StoryModifierState
            {
                id = "power_move", name = "Power Move", description = "You showed the house.",
                weeksLeft = 2, competitionBonus = 2, socialBonus = -5,
            });

            files.Store.Save(state);
            Assert.That(files.Store.TryLoad(out var loaded, out string message), Is.True, message);
            Assert.That(loaded.storylines.Single().templateId, Is.EqualTo(story.templateId));
            Assert.That(loaded.storylines.Single().eventId, Is.EqualTo(chapter.id));
            var modifier = loaded.activeModifiers.Single();
            Assert.That(modifier.weeksLeft, Is.EqualTo(2));
            Assert.That(modifier.competitionBonus, Is.EqualTo(2));
            Assert.That(modifier.socialBonus, Is.EqualTo(-5));
        }

        /// <summary>
        /// A running story has not ended and a finished one ended on a week the season reached.
        /// Anything else is a save that says a story closed and cannot say when.
        /// </summary>
        [TestCase("running-but-ended")]
        [TestCase("ended-before-it-began")]
        [TestCase("ended-in-the-future")]
        [TestCase("unknown-status")]
        [TestCase("duplicate")]
        public void ASeasonWithBadStorylineDataDoesNotValidate(string damage)
        {
            var state = SeasonBuilder.Create(new SeasonBuilder.Choice(), 5u);
            state.week = 5;
            var story = new StorylineState
            {
                id = "s", templateId = "fallback_power_play", category = Storylines.PowerPlay,
                title = "The Power Struggle", status = StorylineStatus.Completed, week = 2, endedWeek = 3,
            };
            state.storylines.Add(story);
            Assert.That(EpisodeValidation.TryValidate(state, out _), Is.True, "The fixture must start valid.");

            switch (damage)
            {
                case "running-but-ended": story.status = StorylineStatus.Active; break;
                case "ended-before-it-began": story.endedWeek = 1; break;
                case "ended-in-the-future": story.endedWeek = state.week + 1; break;
                case "unknown-status": story.status = "halfway"; break;
                case "duplicate": state.storylines.Add(story.Clone()); break;
            }
            Assert.That(EpisodeValidation.TryValidate(state, out _), Is.False, damage);
        }

        [TestCase("weeks")] [TestCase("competition")] [TestCase("social")] [TestCase("name")]
        public void ASeasonWithBadModifierDataDoesNotValidate(string damage)
        {
            var state = SeasonBuilder.Create(new SeasonBuilder.Choice(), 5u);
            var modifier = new StoryModifierState
            {
                id = "m", name = "Something", weeksLeft = 2, competitionBonus = 2,
            };
            state.activeModifiers.Add(modifier);
            Assert.That(EpisodeValidation.TryValidate(state, out _), Is.True, "The fixture must start valid.");

            switch (damage)
            {
                // Zero weeks is not a modifier that is nearly over; it is one that should have been
                // removed, and a save carrying it has lost track of its own clock.
                case "weeks": modifier.weeksLeft = 0; break;
                case "competition": modifier.competitionBonus = double.NaN; break;
                case "social": modifier.socialBonus = 1000; break;
                case "name": modifier.name = ""; break;
            }
            Assert.That(EpisodeValidation.TryValidate(state, out _), Is.False, damage);
        }

        /// <summary>A v11 save carrying house events still migrates, and carries them across.</summary>
        [Test]
        public void ThePreviousVersionsEventsSurviveTheUpgrade()
        {
            var state = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 10 }, 5u);
            state.week = 3;
            var drawn = HouseEvents.Draw(state, 0.5, 1);
            Assert.That(drawn, Is.Not.Null, "The fixture needs an event to carry.");
            state.houseEvents.Add(drawn);

            // The whole chain even though one step reaches current today, because this exact shape
            // has broken on three successive versions: what has to stay legal is what a load
            // produces, and a test that pins a step is a test that breaks on the next bump.
            var parsed = EpisodeSaveMigrations.PrepareCurrentPayload(CaptureV11(state), out _)
                .ToObject<EpisodeState>(Serializer());

            Assert.That(parsed.houseEvents, Has.Count.EqualTo(1));
            Assert.That(parsed.houseEvents[0].narrative, Is.EqualTo(drawn.narrative));
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
        private static JObject CaptureV11(EpisodeState state)
        {
            var payload = PersistenceMigrationTests.StripSchema12((Capture(state)));
            payload["schemaVersion"] = 11;
            return payload;
        }

        private static JObject Historical(int version)
        {
            if (version == 11) return CaptureV11(ContentCatalog.Create(601));
            return (JObject)typeof(PersistenceV11MigrationTests)
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
                Path.Combine(Path.GetTempPath(), "GamesimV12MigrationTests-" + Guid.NewGuid().ToString("N"));
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
