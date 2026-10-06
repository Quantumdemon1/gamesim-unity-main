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
    /// Schema 16: the story bundle. A v15 save gains it switched on from the week after its own,
    /// with lore cast from its saved cards, and every house event, storyline and modifier gains its
    /// story fields at the legacy values - nothing else in the save moves.
    /// </summary>
    public sealed class PersistenceV16MigrationTests
    {
        private static JsonSerializer Serializer() => (JsonSerializer)typeof(EpisodeSaveStore).Assembly
            .GetType("Gamesim.Persistence.SaveJson", true).GetMethod("Serializer", BindingFlags.Public | BindingFlags.Static).Invoke(null, null);

        private static void CheckShape(JObject payload) => typeof(EpisodeSaveStore).Assembly
            .GetType("Gamesim.Persistence.SaveJson", true).GetMethod("CheckDtoShape", BindingFlags.Public | BindingFlags.Static)
            .Invoke(null, new object[] { payload, typeof(EpisodeState), "state" });

        /// <summary>A v15 save with some history in it: a few weeks played with house events and storylines.</summary>
        private static JObject V15(uint seed = 701)
        {
            var engine = new EpisodeEngine(ContentCatalog.Create(seed));
            for (int i = 0; i < 40 && engine.Snapshot.week < 2; i++) Assert.That(engine.Apply(EpisodeEngineTests.NextCommand(engine.Snapshot)).accepted, Is.True);
            var old = PersistenceMigrationTests.StripSchema16(JObject.FromObject(engine.Snapshot, Serializer()));
            old["schemaVersion"] = 15;
            return old;
        }

        [Test]
        public void MigrationAddsTheBundleFromNextWeekAndMovesNothingElse()
        {
            var old = V15(); string original = old.ToString();
            var migrated = EpisodeSaveMigrations.PrepareCurrentPayload(old, out var changed);
            Assert.That(changed, Is.True);
            Assert.That((int)migrated["schemaVersion"], Is.EqualTo(26));
            int week = (int)old["week"];
            Assert.That((int)migrated["story"]["rulesStartWeek"], Is.EqualTo(week + 1));
            Assert.That((int)migrated["story"]["rulesVersion"], Is.EqualTo(StoryRules.Current));
            Assert.That(((JArray)migrated["story"]["lore"]).Count, Is.EqualTo(((JArray)old["contestants"]).Count(c => !(bool)c["isPlayer"])));
            foreach (var list in new[] { "grudges", "facts", "bonds", "hooks", "contacts", "knownFacts", "conduct", "removals", "cooldowns", "reckonings" })
                Assert.That(((JArray)migrated["story"][list]).Count, Is.Zero, list);

            foreach (JObject item in (JArray)migrated["houseEvents"])
            {
                Assert.That(item["contentId"].Type, Is.EqualTo(JTokenType.Null));
                Assert.That(((JArray)item["cast"]).Count, Is.Zero);
                foreach (JObject choice in (JArray)item["choices"])
                {
                    Assert.That((double)choice["checkBase"], Is.EqualTo(-1));
                    Assert.That(choice["optionId"].Type, Is.EqualTo(JTokenType.Null));
                    Assert.That((bool)choice["locked"], Is.False);
                }
            }
            foreach (JObject modifier in (JArray)migrated["activeModifiers"]) Assert.That((string)modifier["ownerId"], Is.EqualTo(""));

            // Take the new fields back off and the save is exactly what it was.
            var projection = PersistenceMigrationTests.StripSchema16((JObject)migrated.DeepClone());
            projection["schemaVersion"] = 15;
            Assert.That(JToken.DeepEquals(projection, old), Is.True);
            Assert.That(old.ToString(), Is.EqualTo(original), "The original payload must not be touched.");

            CheckShape(migrated);
            var state = migrated.ToObject<EpisodeState>(Serializer());
            Assert.That(EpisodeValidation.TryValidate(state, out var error), Is.True, error);
            Assert.That(EpisodeEngine.StoryOn(state), Is.False, "The week the save was in keeps its own rules.");

            var repeated = EpisodeSaveMigrations.PrepareCurrentPayload(migrated, out changed);
            Assert.That(changed, Is.False);
            Assert.That(JToken.DeepEquals(repeated, migrated), Is.True);
        }

        [Test]
        public void AMigratedSeasonPlaysOnIntoTheStorySystemAndFinishes()
        {
            var migrated = EpisodeSaveMigrations.PrepareCurrentPayload(V15(733), out _);
            var state = migrated.ToObject<EpisodeState>(Serializer());
            var run = StorySeasonTests.Play(state, 733);
            Assert.That(run.final.story.rulesStartWeek, Is.EqualTo(state.week + 1));
        }

        [Test]
        public void TheMigrationCastsLoreExactlyAsANewSeasonWould()
        {
            var old = V15(745);
            var migrated = EpisodeSaveMigrations.PrepareCurrentPayload(old, out _).ToObject<EpisodeState>(Serializer());
            var fresh = PersistenceMigrationTests.StripSchema16(JObject.FromObject(ContentCatalog.Create(745), Serializer()))
                .ToObject<EpisodeState>(Serializer());
            fresh.story = new StoryWorldState();
            Lore.Cast(fresh);
            Assert.That(migrated.story.lore.Select(l => l.contestantId + ":" + l.sheetKey + ":" + string.Join(",", l.factIds)),
                Is.EqualTo(fresh.story.lore.Select(l => l.contestantId + ":" + l.sheetKey + ":" + string.Join(",", l.factIds))));
        }

        [TestCase("root")] [TestCase("event")] [TestCase("choice")] [TestCase("storyline")] [TestCase("modifier")]
        public void AV15SaveCannotSmuggleSchema16Fields(string place)
        {
            var old = V15();
            if (place == "root") old["story"] = new JObject();
            else if (place == "event" || place == "choice")
            {
                var events = (JArray)old["houseEvents"];
                if (events.Count == 0)
                    events.Add(JObject.FromObject(new
                    {
                        id = "house-event-1", kind = "house", title = "t", narrative = "n", outcome = (string)null,
                        involvedIds = new string[0], week = 1, resolved = true, chosenIndex = -1,
                        choices = new[] { new { label = "a", description = "b", risk = "low", impacts = new object[0], trustChange = 0.0 } },
                    }));
                var item = (JObject)events[0];
                if (place == "event") item["contentId"] = "smuggled";
                else ((JObject)((JArray)item["choices"])[0])["optionId"] = "smuggled";
            }
            else if (place == "storyline")
                ((JArray)old["storylines"]).Add(JObject.FromObject(new { id = "s", templateId = "t", category = "c", title = "x", eventId = (string)null,
                    status = "completed", week = 1, endedWeek = 1, beatId = "smuggled" }));
            else ((JArray)old["activeModifiers"]).Add(JObject.FromObject(new { id = "m", name = "n", description = "d", weeksLeft = 1,
                competitionBonus = 0.0, socialBonus = 0.0, ownerId = "" }));
            Assert.Throws<InvalidDataException>(() => EpisodeSaveMigrations.UpgradeV15ToV16(old));
        }

        [Test]
        public void AStorySeasonRoundTripsThroughTheSaveShape()
        {
            var engine = new EpisodeEngine(StorySeasonTests.StorySeason(19u));
            for (int i = 0; i < 400 && engine.Snapshot.week < 4 && engine.Snapshot.phase != EpisodePhase.Finished; i++)
                Assert.That(engine.Apply(StorySeasonTests.StoryNext(engine.Snapshot, 19)).accepted, Is.True);
            var state = engine.Snapshot;
            Assert.That(state.houseEvents.Any(e => e.IsStory) || state.storylines.Any(x => x.beatId != null), Is.True,
                "The capture should carry story state, or this proves nothing.");
            var payload = JObject.FromObject(state, Serializer());
            CheckShape(payload);
            var loaded = payload.ToObject<EpisodeState>(Serializer());
            Assert.That(EpisodeValidation.TryValidate(loaded, out var error), Is.True, error);
            Assert.That(JToken.DeepEquals(JObject.FromObject(loaded, Serializer()), payload), Is.True);
        }
    }
}
