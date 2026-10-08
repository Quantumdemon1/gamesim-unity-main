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
            Assert.That((int)migrated["schemaVersion"], Is.EqualTo(28));
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
            Assert.That(state.competitionRulesVersion, Is.EqualTo(CompetitionRules.Current), "A new season plays the current competition rules.");
            state.contestants[1].appearance.dna.Add(new AppearanceValue { id = "height", value = float.NaN });
            Assert.That(EpisodeValidation.TryValidate(state, out _), Is.False);
            state.contestants[1].appearance.dna.Clear(); state.competitionRulesVersion = CompetitionRules.Current + 1;
            Assert.That(EpisodeValidation.TryValidate(state, out _), Is.False);
        }

        [TestCase("socialActions", 19, true)] [TestCase("socialActions", 24, true)] [TestCase("socialActions", 25, false)]
        [TestCase("outOfPhaseSocialActions", 19, true)] [TestCase("outOfPhaseSocialActions", 24, true)] [TestCase("outOfPhaseSocialActions", 25, false)]
        public void V12PreservesItsOwnPurchasedActionBounds(string field, int count, bool valid)
        {
            var old = V12(); old[field] = count; old["boughtActionPoints"] = 6;
            string original = old.ToString();
            if (!valid)
            {
                Assert.Throws<InvalidDataException>(() => EpisodeSaveMigrations.UpgradeV12ToV13(old));
                Assert.That(old.ToString(), Is.EqualTo(original), "Even a rejected historical payload is not rewritten.");
                return;
            }
            var current = EpisodeSaveMigrations.UpgradeV12ToV13(old);
            Assert.That((int)current["schemaVersion"], Is.EqualTo(13), "The frozen step still stops at thirteen.");
            Assert.That((int)current[field], Is.EqualTo(count));
            Assert.That((int)old[field], Is.EqualTo(count));
            // Keep every historical step explicit, then finish the actual schema-23 migration.
            var version22 = EpisodeSaveMigrations.UpgradeV21ToV22(EpisodeSaveMigrations.UpgradeV20ToV21(EpisodeSaveMigrations.UpgradeV19ToV20(EpisodeSaveMigrations.UpgradeV18ToV19(EpisodeSaveMigrations.UpgradeV17ToV18(EpisodeSaveMigrations.UpgradeV16ToV17(
                EpisodeSaveMigrations.UpgradeV15ToV16(EpisodeSaveMigrations.UpgradeV14ToV15(EpisodeSaveMigrations.UpgradeV13ToV14(current)))))))));
            Assert.That((int)version22["schemaVersion"], Is.EqualTo(22));
            string previous = version22.ToString();
            var completed = EpisodeSaveMigrations.UpgradeV22ToV23(version22);
            Assert.That((int)completed["schemaVersion"], Is.EqualTo(23));
            Assert.That((int)completed["economyRulesVersion"], Is.Zero);
            Assert.That((int)completed["moveInExtrasSpent"], Is.Zero);
            Assert.That((int)completed[field], Is.EqualTo(count), "The current economy never clips a valid historical counter.");
            Assert.That((int)completed["boughtActionPoints"], Is.EqualTo(6));
            var projection = PersistenceMigrationTests.StripSchema23((JObject)completed.DeepClone());
            projection["schemaVersion"] = 22;
            Assert.That(JToken.DeepEquals(projection, version22), Is.True, "Only the declared schema-23 defaults are added.");
            var originalProjection = PersistenceMigrationTests.StripSchema13((JObject)completed.DeepClone());
            originalProjection["schemaVersion"] = 12;
            Assert.That(JToken.DeepEquals(originalProjection, old), Is.True);
            Assert.That(version22.ToString(), Is.EqualTo(previous));
            Assert.That(old.ToString(), Is.EqualTo(original));
            string frozen23 = completed.ToString();
            var current24 = EpisodeSaveMigrations.UpgradeV23ToV24(completed);
            Assert.That((int)current24["schemaVersion"], Is.EqualTo(24), "The frozen step still stops at twenty-four.");
            Assert.That((int)current24["unifiedCommitmentRulesVersion"], Is.Zero);
            Assert.That((JArray)current24["unifiedCommitments"], Is.Empty);
            var projection23 = PersistenceMigrationTests.StripSchema24((JObject)current24.DeepClone());
            projection23["schemaVersion"] = 23;
            Assert.That(JToken.DeepEquals(projection23, completed), Is.True);
            Assert.That(completed.ToString(), Is.EqualTo(frozen23));
            string frozen24 = current24.ToString();
            var current25 = EpisodeSaveMigrations.UpgradeV24ToV25(current24);
            PersistenceV25TestPayloads.OnlyHearingDefaults(current24, current25);
            Assert.That(current24.ToString(), Is.EqualTo(frozen24));
            string frozen25 = current25.ToString();
            var current26 = EpisodeSaveMigrations.UpgradeV25ToV26(current25);
            Assert.That(current25.ToString(), Is.EqualTo(frozen25));
            string frozen26 = current26.ToString();
            var current27 = EpisodeSaveMigrations.UpgradeV26ToV27(current26);
            Assert.That(current26.ToString(), Is.EqualTo(frozen26));
            string frozen27 = current27.ToString();
            var current28 = EpisodeSaveMigrations.UpgradeV27ToV28(current27);
            Assert.That(current27.ToString(), Is.EqualTo(frozen27));
            var loaded = current28.ToObject<EpisodeState>(Serializer());
            Assert.That(field == "socialActions" ? loaded.socialActions : loaded.outOfPhaseSocialActions, Is.EqualTo(count));
            Assert.That(EpisodeEngine.EconomyRulesOn(loaded), Is.False, "Historical seasons keep the old action economy.");
            Assert.That(EpisodeValidation.TryValidate(loaded, out var error), Is.True, error);
        }
    }
}
