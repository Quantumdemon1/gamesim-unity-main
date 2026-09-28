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
    /// Schema 15 opens the strategy fields switched off: a season saved before them keeps playing as
    /// it was. Schema 14's own contract is frozen - the Have-Not boundary, three lists of houseguests
    /// and the prize record - and still refuses what it refused.
    /// </summary>
    public sealed class PersistenceV15MigrationTests
    {
        private static JsonSerializer Serializer() => (JsonSerializer)typeof(EpisodeSaveStore).Assembly
            .GetType("Gamesim.Persistence.SaveJson", true).GetMethod("Serializer", BindingFlags.Public | BindingFlags.Static).Invoke(null, null);

        /// <summary>A schema 14 payload: a new season of today, less schema 15's fields.</summary>
        private static JObject V14(uint seed = 702)
        {
            var season = SeasonBuilder.Create(new SeasonBuilder.Choice(), seed);
            var old = PersistenceMigrationTests.StripSchema15(JObject.FromObject(season, Serializer()));
            old["schemaVersion"] = 14;
            return old;
        }

        [Test]
        public void MigrationOpensTheStrategyFieldsSwitchedOffAndKeepsEverythingElse()
        {
            var old = V14(); string original = old.ToString();
            Assert.That((int)old["haveNotRulesStartWeek"], Is.EqualTo(1), "Schema 14 already held seasons with Have-Nots.");
            // The step itself: the frozen dispatch to fifteen.
            var migrated = EpisodeSaveMigrations.PrepareV15Payload(old, out var changed);
            Assert.That(changed, Is.True);
            Assert.That((int)migrated["schemaVersion"], Is.EqualTo(15));
            Assert.That((int)migrated["strategyRulesStartWeek"], Is.Zero, "A season under way plays on without the windows.");
            Assert.That(((JArray)migrated["lobbies"]).Count, Is.Zero);
            Assert.That(((JArray)migrated["replyCards"]).Count, Is.Zero);
            var projection = PersistenceMigrationTests.StripSchema15((JObject)migrated.DeepClone());
            projection["schemaVersion"] = 14;
            Assert.That(JToken.DeepEquals(projection, old), Is.True, "Every schema 14 field is carried unchanged, Have-Nots included.");
            Assert.That(old.ToString(), Is.EqualTo(original), "The original payload is not touched.");
            // Validated as the save it becomes: the live contract is schema 19's.
            var state = EpisodeSaveMigrations.UpgradeV18ToV19(EpisodeSaveMigrations.UpgradeV17ToV18(EpisodeSaveMigrations.UpgradeV16ToV17(
                EpisodeSaveMigrations.UpgradeV15ToV16(migrated)))).ToObject<EpisodeState>(Serializer());
            Assert.That(EpisodeValidation.TryValidate(state, out var error), Is.True, error);
            var repeated = EpisodeSaveMigrations.PrepareV15Payload(migrated, out changed);
            Assert.That(changed, Is.False);
            Assert.That(JToken.DeepEquals(repeated, migrated), Is.True);
        }

        [Test]
        public void AMigratedSeasonOpensNoWindowsAndDealsNoCards()
        {
            var state = EpisodeSaveMigrations.PrepareCurrentPayload(V14(), out _).ToObject<EpisodeState>(Serializer());
            state.phase = EpisodePhase.Nomination;
            state.hohId = state.Active.First(c => !c.isPlayer).id;
            Assert.That(StrategyRules.Deciders(state), Is.Empty);
            var result = new EpisodeEngine(state).Apply(new EpisodeCommand { id = "talk", actorId = state.playerId,
                expectedPhase = state.phase, expectedRevision = state.revision, kind = EpisodeCommandKind.SmallTalk, targetId = state.hohId });
            Assert.That(result.accepted, Is.False, "The Head of Household still has a ceremony to get through.");

            state.phase = EpisodePhase.Social; state.hohId = null;
            string npc = state.Active.First(c => !c.isPlayer).id;
            foreach (var edge in state.relationships.Where(edge => edge.fromId == npc)) edge.score = edge.toId == state.playerId ? -30 : 50;
            Assert.That(NpcSocialActions.Perform(state, npc, NpcActionKind.Confront), Is.True);
            Assert.That(state.replyCards, Is.Empty, "The confrontation happens; the card is a new season's.");
        }

        [TestCase("start")]
        [TestCase("negative")]
        [TestCase("twice")]
        [TestCase("stranger")]
        [TestCase("prize")]
        [TestCase("prize-week")]
        [TestCase("prize-shape")]
        [TestCase("prize-type")]
        [TestCase("off")]
        [TestCase("smuggled")]
        public void TheFrozenSchema14ContractRefusesWhatSchema14Did(string place)
        {
            var old = V14();
            string someone = (string)((JArray)old["contestants"])[1]["id"];
            switch (place)
            {
                case "start": old["haveNotRulesStartWeek"] = (int)old["week"] + 2; break;
                case "negative": old["haveNotRulesStartWeek"] = -1; break;
                case "twice": old["haveNots"] = new JArray(someone, someone); break;
                case "stranger": old["haveNotPasses"] = new JArray("nobody-at-all"); break;
                case "prize": old["vetoPrizes"] = new JArray(Prize(1, someone, "a-car")); break;
                case "prize-week": old["vetoPrizes"] = new JArray(Prize((int)old["week"] + 1, someone, "cash")); break;
                case "prize-shape":
                    var shaped = Prize(1, someone, "cash"); shaped["amount"] = 5000;
                    old["vetoPrizes"] = new JArray(shaped); break;
                case "prize-type":
                    // A number written as text: the serializer would read it, the frozen shape does not.
                    var typed = Prize(1, someone, "cash"); typed["week"] = "1";
                    old["vetoPrizes"] = new JArray(typed); break;
                case "off": old["haveNotRulesStartWeek"] = 0; old["punishedHaveNots"] = new JArray(someone); break;
                case "smuggled": old["lobbies"] = new JArray(); break;
            }
            Assert.Throws<InvalidDataException>(() => EpisodeSaveMigrations.PrepareCurrentPayload(old, out _), place);
        }

        [Test]
        public void TheFrozenSchema14ContractAcceptsWhatSchema14Held()
        {
            var old = V14();
            string someone = (string)((JArray)old["contestants"])[1]["id"];
            old["haveNots"] = new JArray(someone);
            old["punishedHaveNots"] = new JArray((string)((JArray)old["contestants"])[2]["id"]);
            old["vetoPrizes"] = new JArray(Prize(1, someone, "luxury-night"));
            var migrated = EpisodeSaveMigrations.PrepareCurrentPayload(old, out _);
            Assert.That(migrated["haveNots"].Select(id => (string)id), Is.EqualTo(new[] { someone }));
            Assert.That((string)migrated["vetoPrizes"][0]["prizeId"], Is.EqualTo("luxury-night"));
        }

        [Test]
        public void EveryOlderSchemaLoadsThroughFifteen()
        {
            var old = PersistenceMigrationTests.StripSchema13(JObject.FromObject(ContentCatalog.Create(703), Serializer()));
            old["schemaVersion"] = 12;
            var migrated = EpisodeSaveMigrations.PrepareCurrentPayload(old, out var changed);
            Assert.That(changed, Is.True);
            Assert.That((int)migrated["schemaVersion"], Is.EqualTo(19), "The whole chain, not one step.");
            Assert.That(EpisodeSaveMigrations.PrepareV15Payload(old, out _)["schemaVersion"].Value<int>(), Is.EqualTo(15),
                "The frozen dispatch still stops at fifteen.");
            Assert.That((int)migrated["haveNotRulesStartWeek"], Is.Zero);
            Assert.That((int)migrated["strategyRulesStartWeek"], Is.Zero);
            Assert.That(EpisodeSaveMigrations.PrepareV14Payload(old, out _)["schemaVersion"].Value<int>(), Is.EqualTo(14),
                "The frozen dispatch still stops at fourteen.");
        }

        private static JObject Prize(int week, string contestant, string prize) =>
            new JObject { ["week"] = week, ["contestantId"] = contestant, ["prizeId"] = prize };
    }
}
