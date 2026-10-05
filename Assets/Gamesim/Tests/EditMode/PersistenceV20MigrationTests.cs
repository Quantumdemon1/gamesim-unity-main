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
    /// Schema 20: NPC agency. A v19 save gains the agency boundary switched on from the week after
    /// its own, so the week it was in keeps the house it was played with, and no first impressions
    /// are seeded into a season under way; nothing else in the save moves.
    /// </summary>
    public sealed class PersistenceV20MigrationTests
    {
        private static JsonSerializer Serializer() => (JsonSerializer)typeof(EpisodeSaveStore).Assembly
            .GetType("Gamesim.Persistence.SaveJson", true).GetMethod("Serializer", BindingFlags.Public | BindingFlags.Static).Invoke(null, null);

        private static void CheckShape(JObject payload) => typeof(EpisodeSaveStore).Assembly
            .GetType("Gamesim.Persistence.SaveJson", true).GetMethod("CheckDtoShape", BindingFlags.Public | BindingFlags.Static)
            .Invoke(null, new object[] { payload, typeof(EpisodeState), "state" });

        /// <summary>A v19 save with some history in it: a few weeks played.</summary>
        private static JObject V19(uint seed = 1013)
        {
            var engine = new EpisodeEngine(ContentCatalog.Create(seed));
            for (int i = 0; i < 40 && engine.Snapshot.week < 2; i++) Assert.That(engine.Apply(EpisodeEngineTests.NextCommand(engine.Snapshot)).accepted, Is.True);
            var old = PersistenceMigrationTests.StripSchema20(JObject.FromObject(engine.Snapshot, Serializer()));
            old["schemaVersion"] = 19;
            return old;
        }

        [Test]
        public void MigrationAddsTheAgencyBoundaryAndMovesNothingElse()
        {
            var old = V19(); string original = old.ToString();
            var migrated = EpisodeSaveMigrations.PrepareCurrentPayload(old, out var changed);
            Assert.That(changed, Is.True);
            Assert.That((int)migrated["schemaVersion"], Is.EqualTo(23));
            int week = (int)old["week"];
            Assert.That((int)migrated["agencyRulesStartWeek"], Is.EqualTo(week + 1), "The week the save was in keeps the house it was played with.");
            Assert.That(JToken.DeepEquals(migrated["relationships"], old["relationships"]), Is.True, "No first impressions into a season under way.");

            var projection = PersistenceMigrationTests.StripSchema20((JObject)migrated.DeepClone());
            projection["schemaVersion"] = 19;
            Assert.That(JToken.DeepEquals(projection, old), Is.True, "Take the new field back off and the save is exactly what it was.");
            Assert.That(old.ToString(), Is.EqualTo(original), "The original payload must not be touched.");

            CheckShape(migrated);
            var state = migrated.ToObject<EpisodeState>(Serializer());
            Assert.That(EpisodeValidation.TryValidate(state, out var error), Is.True, error);
            Assert.That(EpisodeEngine.AgencyOn(state), Is.False, "This week plays as it was.");
            Assert.That(NpcAgendas.Of(state, state.contestants.First(c => !c.isPlayer).id), Is.Null);

            var repeated = EpisodeSaveMigrations.PrepareCurrentPayload(migrated, out changed);
            Assert.That(changed, Is.False);
            Assert.That(JToken.DeepEquals(repeated, migrated), Is.True);
        }

        [Test]
        public void TheWholeChainFromV15ReachesTheCurrentSchemaThroughTwenty()
        {
            var engine = new EpisodeEngine(ContentCatalog.Create(1019));
            for (int i = 0; i < 40 && engine.Snapshot.week < 2; i++) Assert.That(engine.Apply(EpisodeEngineTests.NextCommand(engine.Snapshot)).accepted, Is.True);
            var old = PersistenceMigrationTests.StripSchema16(JObject.FromObject(engine.Snapshot, Serializer()));
            old["schemaVersion"] = 15;
            var migrated = EpisodeSaveMigrations.PrepareCurrentPayload(old, out var changed);
            Assert.That(changed, Is.True);
            Assert.That((int)migrated["schemaVersion"], Is.EqualTo(23));
            Assert.That((int)migrated["agencyRulesStartWeek"], Is.EqualTo((int)old["week"] + 1));
            Assert.That((int)migrated["weekRulesStartWeek"], Is.EqualTo((int)old["week"] + 1));
            CheckShape(migrated);
            Assert.That(EpisodeValidation.TryValidate(migrated.ToObject<EpisodeState>(Serializer()), out var error), Is.True, error);
        }

        [Test]
        public void AV19SaveCannotSmuggleTheSchema20Field()
        {
            var old = V19();
            old["agencyRulesStartWeek"] = 1;
            Assert.Throws<InvalidDataException>(() => EpisodeSaveMigrations.PrepareCurrentPayload(old, out _));
        }

        [Test]
        public void AV19SaveWithABrokenWeekShapeIsRefused()
        {
            var old = V19();
            old["windowActions"] = new JArray(0, 0, 0);
            Assert.Throws<InvalidDataException>(() => EpisodeSaveMigrations.PrepareCurrentPayload(old, out _));
            old = V19();
            old["weekRulesStartWeek"] = (int)old["week"] + 2;
            Assert.Throws<InvalidDataException>(() => EpisodeSaveMigrations.PrepareCurrentPayload(old, out _));
        }
    }
}
