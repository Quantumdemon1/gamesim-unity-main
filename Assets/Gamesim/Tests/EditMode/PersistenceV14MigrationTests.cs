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
    /// Schema 14 opens the Have-Not fields switched off: a season saved before them keeps playing as
    /// it was. Schema 13's own contract is frozen - rules 1 to 4, a template and an appearance on
    /// every houseguest - and still refuses what it refused.
    /// </summary>
    public sealed class PersistenceV14MigrationTests
    {
        private static JsonSerializer Serializer() => (JsonSerializer)typeof(EpisodeSaveStore).Assembly
            .GetType("Gamesim.Persistence.SaveJson", true).GetMethod("Serializer", BindingFlags.Public | BindingFlags.Static).Invoke(null, null);

        /// <summary>A schema 13 payload: a new season of today, less schema 14's fields.</summary>
        private static JObject V13(uint seed = 602)
        {
            var season = SeasonBuilder.Create(new SeasonBuilder.Choice(), seed);
            var old = PersistenceMigrationTests.StripSchema14(JObject.FromObject(season, Serializer()));
            old["schemaVersion"] = 13;
            return old;
        }

        [Test]
        public void MigrationOpensTheHaveNotFieldsSwitchedOffAndKeepsEverythingElse()
        {
            var old = V13(); string original = old.ToString();
            Assert.That((int)old["competitionRulesVersion"], Is.EqualTo(4), "Schema 13 already held rules-4 seasons.");
            // The step itself: the frozen dispatch to fourteen.
            var migrated = EpisodeSaveMigrations.PrepareV14Payload(old, out var changed);
            Assert.That(changed, Is.True);
            Assert.That((int)migrated["schemaVersion"], Is.EqualTo(14));
            Assert.That((int)migrated["haveNotRulesStartWeek"], Is.Zero, "A season under way plays on without Have-Nots.");
            foreach (var field in new[] { "haveNots", "haveNotPasses", "punishedHaveNots", "vetoPrizes" })
                Assert.That(((JArray)migrated[field]).Count, Is.Zero, field);
            var projection = PersistenceMigrationTests.StripSchema14((JObject)migrated.DeepClone());
            projection["schemaVersion"] = 13;
            Assert.That(JToken.DeepEquals(projection, old), Is.True, "Every schema 13 field is carried unchanged.");
            Assert.That(old.ToString(), Is.EqualTo(original), "The original payload is not touched.");
            // Validated as the save it becomes: the live contract is schema 17's.
            var state = EpisodeSaveMigrations.UpgradeV16ToV17(EpisodeSaveMigrations.UpgradeV15ToV16(EpisodeSaveMigrations.UpgradeV14ToV15(migrated)))
                .ToObject<EpisodeState>(Serializer());
            Assert.That(EpisodeValidation.TryValidate(state, out var error), Is.True, error);
            var repeated = EpisodeSaveMigrations.PrepareV14Payload(migrated, out changed);
            Assert.That(changed, Is.False);
            Assert.That(JToken.DeepEquals(repeated, migrated), Is.True);
        }

        [Test]
        public void AMigratedSeasonNamesNoHaveNots()
        {
            var state = EpisodeSaveMigrations.PrepareCurrentPayload(V13(), out _).ToObject<EpisodeState>(Serializer());
            state.phase = EpisodePhase.HoH;
            var result = new EpisodeEngine(state).Apply(new EpisodeCommand { id = "hoh", actorId = state.playerId,
                expectedPhase = state.phase, expectedRevision = state.revision, kind = EpisodeCommandKind.Compete, performance = .5 });
            Assert.That(result.accepted, Is.True, result.reason);
            Assert.That(result.state.haveNots, Is.Empty, "Have-Nots are a new season's, not a change to one under way.");
        }

        [TestCase("rules")]
        [TestCase("template")]
        [TestCase("appearance")]
        [TestCase("smuggled")]
        public void TheFrozenSchema13ContractRefusesWhatSchema13Did(string place)
        {
            var old = V13();
            var person = (JObject)((JArray)old["contestants"])[1];
            switch (place)
            {
                case "rules": old["competitionRulesVersion"] = 5; break;
                case "template": person["sourceTemplateId"] = new string('x', 101); break;
                case "appearance":
                    if (person["appearance"].Type == JTokenType.Null) person["appearance"] = JObject.FromObject(CharacterAppearance.Preset("x"), Serializer());
                    ((JArray)person["appearance"]["dna"]).Add(JObject.FromObject(new { id = "height", value = 7.5 }));
                    break;
                case "smuggled": old["haveNots"] = new JArray(); break;
            }
            Assert.Throws<InvalidDataException>(() => EpisodeSaveMigrations.PrepareCurrentPayload(old, out _), place);
        }

        [Test]
        public void EveryOlderSchemaLoadsIntoTheCurrentOne()
        {
            var old = PersistenceMigrationTests.StripSchema13(JObject.FromObject(ContentCatalog.Create(603), Serializer()));
            old["schemaVersion"] = 12;
            var migrated = EpisodeSaveMigrations.PrepareCurrentPayload(old, out var changed);
            Assert.That(changed, Is.True);
            Assert.That((int)migrated["schemaVersion"], Is.EqualTo(17), "The whole chain, not one step.");
            Assert.That((int)migrated["competitionRulesVersion"], Is.EqualTo(1));
            Assert.That((int)migrated["haveNotRulesStartWeek"], Is.Zero);
            Assert.That(EpisodeSaveMigrations.PrepareV13Payload(old, out _)["schemaVersion"].Value<int>(), Is.EqualTo(13),
                "The frozen dispatch still stops at thirteen.");
            Assert.That(EpisodeSaveMigrations.PrepareV14Payload(old, out _)["schemaVersion"].Value<int>(), Is.EqualTo(14),
                "And the next one at fourteen.");
        }
    }
}
