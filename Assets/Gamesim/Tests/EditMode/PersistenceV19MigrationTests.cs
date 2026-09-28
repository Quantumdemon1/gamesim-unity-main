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
    /// Schema 19: the week. A v18 save gains four window counters at zero and the week's windows
    /// switched on from the week after its own, so the week it was in keeps the pool it was played
    /// under; nothing else in the save moves.
    /// </summary>
    public sealed class PersistenceV19MigrationTests
    {
        private static JsonSerializer Serializer() => (JsonSerializer)typeof(EpisodeSaveStore).Assembly
            .GetType("Gamesim.Persistence.SaveJson", true).GetMethod("Serializer", BindingFlags.Public | BindingFlags.Static).Invoke(null, null);

        private static void CheckShape(JObject payload) => typeof(EpisodeSaveStore).Assembly
            .GetType("Gamesim.Persistence.SaveJson", true).GetMethod("CheckDtoShape", BindingFlags.Public | BindingFlags.Static)
            .Invoke(null, new object[] { payload, typeof(EpisodeState), "state" });

        /// <summary>A v18 save with some history in it: a few weeks played.</summary>
        private static JObject V18(uint seed = 1013)
        {
            var engine = new EpisodeEngine(ContentCatalog.Create(seed));
            for (int i = 0; i < 40 && engine.Snapshot.week < 2; i++) Assert.That(engine.Apply(EpisodeEngineTests.NextCommand(engine.Snapshot)).accepted, Is.True);
            var old = PersistenceMigrationTests.StripSchema19(JObject.FromObject(engine.Snapshot, Serializer()));
            old["schemaVersion"] = 18;
            return old;
        }

        [Test]
        public void MigrationAddsTheWindowCountersAndTheWeekBoundaryAndMovesNothingElse()
        {
            var old = V18(); string original = old.ToString();
            var migrated = EpisodeSaveMigrations.PrepareCurrentPayload(old, out var changed);
            Assert.That(changed, Is.True);
            Assert.That((int)migrated["schemaVersion"], Is.EqualTo(19));
            int week = (int)old["week"];
            Assert.That((int)migrated["weekRulesStartWeek"], Is.EqualTo(week + 1), "The week the save was in keeps its pool.");
            Assert.That(((JArray)migrated["windowActions"]).Select(t => (int)t), Is.EqualTo(new[] { 0, 0, 0, 0 }));

            var projection = PersistenceMigrationTests.StripSchema19((JObject)migrated.DeepClone());
            projection["schemaVersion"] = 18;
            Assert.That(JToken.DeepEquals(projection, old), Is.True, "Take the new fields back off and the save is exactly what it was.");
            Assert.That(old.ToString(), Is.EqualTo(original), "The original payload must not be touched.");

            CheckShape(migrated);
            var state = migrated.ToObject<EpisodeState>(Serializer());
            Assert.That(EpisodeValidation.TryValidate(state, out var error), Is.True, error);
            Assert.That(EpisodeEngine.WeekRulesOn(state), Is.False);
            Assert.That(EpisodeEngine.SocialActionBudget(state), Is.EqualTo(EpisodeEngine.EarnedSocialActionBudget(state) + System.Math.Max(0, state.boughtActionPoints)
                + Storylines.SocialActions(state) - HaveNots.ActionCost(state)).Or.EqualTo(1), "The pool it was played under.");

            var repeated = EpisodeSaveMigrations.PrepareCurrentPayload(migrated, out changed);
            Assert.That(changed, Is.False);
            Assert.That(JToken.DeepEquals(repeated, migrated), Is.True);
        }

        [Test]
        public void TheWholeChainFromV15ReachesNineteen()
        {
            var engine = new EpisodeEngine(ContentCatalog.Create(1019));
            for (int i = 0; i < 40 && engine.Snapshot.week < 2; i++) Assert.That(engine.Apply(EpisodeEngineTests.NextCommand(engine.Snapshot)).accepted, Is.True);
            var old = PersistenceMigrationTests.StripSchema16(JObject.FromObject(engine.Snapshot, Serializer()));
            old["schemaVersion"] = 15;
            var migrated = EpisodeSaveMigrations.PrepareCurrentPayload(old, out var changed);
            Assert.That(changed, Is.True);
            Assert.That((int)migrated["schemaVersion"], Is.EqualTo(19));
            Assert.That(migrated["windowActions"], Is.Not.Null);
            Assert.That((int)migrated["weekRulesStartWeek"], Is.EqualTo((int)old["week"] + 1));
            CheckShape(migrated);
            Assert.That(EpisodeValidation.TryValidate(migrated.ToObject<EpisodeState>(Serializer()), out var error), Is.True, error);
        }

        [TestCase("weekRulesStartWeek")] [TestCase("windowActions")]
        public void AV18SaveCannotSmuggleSchema19Fields(string field)
        {
            var old = V18();
            old[field] = field == "windowActions" ? (JToken)new JArray(0, 0, 0, 0) : 1;
            Assert.Throws<InvalidDataException>(() => EpisodeSaveMigrations.PrepareCurrentPayload(old, out _));
        }
    }
}
