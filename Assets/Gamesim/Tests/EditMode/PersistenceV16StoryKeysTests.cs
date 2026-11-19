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
    /// The schema-16 step writes the story bundle from the live <see cref="StoryWorldState"/>, so a story field a later
    /// schema adds must not leak into it: the frozen schema-16 reader counts the bundle's keys, and schema 31's talk fields
    /// once had every schema 2 to 15 save refused ("Invalid historical schema 16 data."). The step keeps the 16 keys of its
    /// own schema, in order; the schema-31 step adds the talk fields.
    /// </summary>
    public sealed class PersistenceV16StoryKeysTests
    {
        private static readonly string[] Sixteen =
        {
            "rulesStartWeek", "rulesVersion", "productionStrictness", "romanceStorylines", "grudges", "facts", "bonds", "hooks",
            "contacts", "lore", "knownFacts", "conduct", "removals", "pendingRemovalId", "cooldowns", "reckonings",
        };

        private static JsonSerializer Serializer() => (JsonSerializer)typeof(EpisodeSaveStore).Assembly
            .GetType("Gamesim.Persistence.SaveJson", true).GetMethod("Serializer", BindingFlags.Public | BindingFlags.Static).Invoke(null, null);

        private static void CheckShape(JObject payload) => typeof(EpisodeSaveStore).Assembly
            .GetType("Gamesim.Persistence.SaveJson", true).GetMethod("CheckDtoShape", BindingFlags.Public | BindingFlags.Static)
            .Invoke(null, new object[] { payload, typeof(EpisodeState), "state" });

        private static JObject FixtureState(string file) => (JObject)JObject.Parse(File.ReadAllText(Path.Combine(
            UnityEngine.Application.dataPath, "Gamesim", "Tests", "EditMode", "Fixtures", "OldSaves", file)))["state"];

        [Test]
        public void TheSchema16StepWritesOnlyItsOwnSixteenStoryKeysInOrder()
        {
            Assert.That(EpisodeSaveMigrations.StoryKeysV16.ToArray(), Is.EqualTo(Sixteen));
            var v15 = FixtureState("s15-director-w2-campaign.save.json");
            Assert.That((int)v15["schemaVersion"], Is.EqualTo(15));
            var v16 = EpisodeSaveMigrations.UpgradeV15ToV16(v15);
            Assert.That(((JObject)v16["story"]).Properties().Select(p => p.Name).ToArray(), Is.EqualTo(Sixteen),
                "No later story field (talkRules, talkLean, talkPairs) reaches the schema-16 contract.");
        }

        [Test]
        public void ASchema15FixtureLoadsToTheLiveSchemaWithFreeTalkOff()
        {
            var migrated = EpisodeSaveMigrations.PrepareCurrentPayload(FixtureState("s15-director-w2-campaign.save.json"), out bool stepped);
            Assert.That(stepped, Is.True);
            Assert.That((int)migrated["schemaVersion"], Is.EqualTo(EpisodeState.LiveSchema));
            CheckShape(migrated);
            var state = migrated.ToObject<EpisodeState>(Serializer());
            Assert.That(EpisodeValidation.TryValidate(state, out string reason), Is.True, reason);
            Assert.That(state.story.talkRules, Is.EqualTo(TalkRules.Off));
            Assert.That(state.story.talkPairs, Is.Empty);
        }
    }
}
