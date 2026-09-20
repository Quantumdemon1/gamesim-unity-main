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
    public sealed class PersistenceV13MigrationTests
    {
        private static JsonSerializer Serializer() => (JsonSerializer)typeof(EpisodeSaveStore).Assembly
            .GetType("Gamesim.Persistence.SaveJson", true).GetMethod("Serializer", BindingFlags.Public | BindingFlags.Static).Invoke(null, null);
        private static JObject V12()
        {
            var old = PersistenceMigrationTests.StripSchema13(JObject.FromObject(ContentCatalog.Create(601), Serializer()));
            old["schemaVersion"] = 12; return old;
        }

        [Test]
        public void MigrationPreservesEveryOldFieldAndDoesNotGuessAppearance()
        {
            var old = V12(); string original = old.ToString();
            var migrated = EpisodeSaveMigrations.PrepareCurrentPayload(old, out var changed);
            Assert.That(changed, Is.True);
            Assert.That((int)migrated["schemaVersion"], Is.EqualTo(13));
            Assert.That((int)migrated["competitionRulesVersion"], Is.EqualTo(1));
            foreach (var person in (JArray)migrated["contestants"])
            {
                Assert.That(person["appearance"].Type, Is.EqualTo(JTokenType.Null));
                Assert.That(person["sourceTemplateId"].Type, Is.EqualTo(JTokenType.Null));
            }
            var projection = PersistenceMigrationTests.StripSchema13((JObject)migrated.DeepClone());
            projection["schemaVersion"] = 12;
            Assert.That(JToken.DeepEquals(projection, old), Is.True);
            Assert.That(old.ToString(), Is.EqualTo(original));
            Assert.That(EpisodeValidation.TryValidate(migrated.ToObject<EpisodeState>(Serializer()), out var error), Is.True, error);
            var repeated = EpisodeSaveMigrations.PrepareCurrentPayload(migrated, out changed);
            Assert.That(changed, Is.False); Assert.That(JToken.DeepEquals(repeated, migrated), Is.True);
        }

        [TestCase("root")] [TestCase("contestant")] [TestCase("storyline")] [TestCase("modifier")]
        public void HistoricalShapeCannotSmuggleNewOrUnknownFields(string place)
        {
            var old = V12();
            if (place == "root") old["competitionRulesVersion"] = 2;
            else if (place == "contestant") old["contestants"][0]["appearance"] = new JObject();
            else if (place == "storyline") ((JArray)old["storylines"]).Add(JObject.FromObject(new { id = "s", unexpected = true }));
            else ((JArray)old["activeModifiers"]).Add(JObject.FromObject(new { id = "m", unexpected = true }));
            Assert.Throws<InvalidDataException>(() => EpisodeSaveMigrations.UpgradeV12ToV13(old));
        }

        [Test]
        public void CurrentSavesRejectInvalidRecipesAndUnsupportedRules()
        {
            var state = SeasonBuilder.Create(new SeasonBuilder.Choice(), 6);
            Assert.That(state.competitionRulesVersion, Is.EqualTo(3));
            state.contestants[1].appearance.dna.Add(new AppearanceValue { id = "height", value = float.NaN });
            Assert.That(EpisodeValidation.TryValidate(state, out _), Is.False);
            state.contestants[1].appearance.dna.Clear(); state.competitionRulesVersion = 4;
            Assert.That(EpisodeValidation.TryValidate(state, out _), Is.False);
        }

        [TestCase("socialActions", 19, true)] [TestCase("socialActions", 24, true)] [TestCase("socialActions", 25, false)]
        [TestCase("outOfPhaseSocialActions", 19, true)] [TestCase("outOfPhaseSocialActions", 24, true)] [TestCase("outOfPhaseSocialActions", 25, false)]
        public void V12PreservesItsOwnPurchasedActionBounds(string field, int count, bool valid)
        {
            var old = V12(); old[field] = count; old["boughtActionPoints"] = 6;
            if (!valid) { Assert.Throws<InvalidDataException>(() => EpisodeSaveMigrations.UpgradeV12ToV13(old)); return; }
            var current = EpisodeSaveMigrations.UpgradeV12ToV13(old);
            Assert.That((int)current[field], Is.EqualTo(count));
            Assert.That((int)old[field], Is.EqualTo(count));
            Assert.That(EpisodeValidation.TryValidate(current.ToObject<EpisodeState>(Serializer()), out var error), Is.True, error);
        }
    }
}
