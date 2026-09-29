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
    /// Schema 21: the finale rules (ENDGAME-PLAN §3). A v20 save gains them switched off - a rules
    /// week of 0, so a season saved before them keeps the catalogue's questions to its end - no
    /// final argument, and on every jury exchange the three fields only a history question fills,
    /// empty. Nothing else in the save moves.
    /// </summary>
    public sealed class PersistenceV21MigrationTests
    {
        private static JsonSerializer Serializer() => (JsonSerializer)typeof(EpisodeSaveStore).Assembly
            .GetType("Gamesim.Persistence.SaveJson", true).GetMethod("Serializer", BindingFlags.Public | BindingFlags.Static).Invoke(null, null);

        private static void CheckShape(JObject payload) => typeof(EpisodeSaveStore).Assembly
            .GetType("Gamesim.Persistence.SaveJson", true).GetMethod("CheckDtoShape", BindingFlags.Public | BindingFlags.Static)
            .Invoke(null, new object[] { payload, typeof(EpisodeState), "state" });

        private static JObject AsV20(EpisodeState state)
        {
            var old = PersistenceMigrationTests.StripSchema21(JObject.FromObject(state, Serializer()));
            old["schemaVersion"] = 20;
            return old;
        }

        /// <summary>A v20 save with some history in it: a few weeks played.</summary>
        private static JObject V20(uint seed = 1021)
        {
            var engine = new EpisodeEngine(ContentCatalog.Create(seed));
            for (int i = 0; i < 40 && engine.Snapshot.week < 2; i++) Assert.That(engine.Apply(EpisodeEngineTests.NextCommand(engine.Snapshot)).accepted, Is.True);
            return AsV20(engine.Snapshot);
        }

        /// <summary>A v20 save in the finale: the questioning open, the first answer given.</summary>
        private static JObject V20AtTheFinale()
        {
            var state = ContentCatalog.Create(337); state.week = 4; state.phase = EpisodePhase.FinalEviction;
            foreach (var actor in state.contestants.Skip(3)) actor.status = ContestantStatus.Jury;
            state.hohId = state.playerId; state.finalPart1WinnerId = state.playerId; state.finalPart2WinnerId = state.contestants[1].id;
            var engine = new EpisodeEngine(state);
            var evict = EpisodeEngineTests.Command(state, EpisodeCommandKind.FinalEvict); evict.targetId = state.contestants[2].id;
            Assert.That(engine.Apply(evict).accepted, Is.True);
            var answer = EpisodeEngineTests.Command(engine.Snapshot, EpisodeCommandKind.AnswerJury);
            answer.targetId = engine.Snapshot.juryExchanges[0].questionerId; answer.secondTargetId = "A";
            Assert.That(engine.Apply(answer).accepted, Is.True);
            Assert.That(engine.Snapshot.juryExchanges, Has.Count.EqualTo(1));
            return AsV20(engine.Snapshot);
        }

        [Test]
        public void MigrationAddsTheFinaleRulesSwitchedOffAndMovesNothingElse()
        {
            var old = V20(); string original = old.ToString();
            var migrated = EpisodeSaveMigrations.PrepareCurrentPayload(old, out var changed);
            Assert.That(changed, Is.True);
            Assert.That((int)migrated["schemaVersion"], Is.EqualTo(21));
            Assert.That((int)migrated["finaleRulesStartWeek"], Is.Zero, "A season saved before the finale rules keeps the catalogue to its end.");
            Assert.That(migrated["finalArgument"].Type, Is.EqualTo(JTokenType.Null));

            var projection = PersistenceMigrationTests.StripSchema21((JObject)migrated.DeepClone());
            projection["schemaVersion"] = 20;
            Assert.That(JToken.DeepEquals(projection, old), Is.True, "Take the new fields back off and the save is exactly what it was.");
            Assert.That(old.ToString(), Is.EqualTo(original), "The original payload must not be touched.");

            CheckShape(migrated);
            var state = migrated.ToObject<EpisodeState>(Serializer());
            Assert.That(EpisodeValidation.TryValidate(state, out var error), Is.True, error);
            Assert.That(EpisodeEngine.FinaleOn(state), Is.False);

            var repeated = EpisodeSaveMigrations.PrepareCurrentPayload(migrated, out changed);
            Assert.That(changed, Is.False);
            Assert.That(JToken.DeepEquals(repeated, migrated), Is.True);
        }

        [Test]
        public void AV20SaveInTheFinaleGivesEveryExchangeTheNewFieldsEmpty()
        {
            var old = V20AtTheFinale();
            var migrated = EpisodeSaveMigrations.PrepareCurrentPayload(old, out var changed);
            Assert.That(changed, Is.True);
            foreach (JObject exchange in (JArray)migrated["juryExchanges"])
                foreach (var field in new[] { "category", "receiptKind", "receiptId" })
                    Assert.That(exchange[field]?.Type, Is.EqualTo(JTokenType.Null), field);
            CheckShape(migrated);
            var state = migrated.ToObject<EpisodeState>(Serializer());
            Assert.That(EpisodeValidation.TryValidate(state, out var error), Is.True, error);
            Assert.That(state.juryExchanges[0].answerChoice, Is.EqualTo("A"), "The catalogue's answer, as it was given.");
        }

        [Test]
        public void TheWholeChainFromV15ReachesTwentyOne()
        {
            var engine = new EpisodeEngine(ContentCatalog.Create(1019));
            for (int i = 0; i < 40 && engine.Snapshot.week < 2; i++) Assert.That(engine.Apply(EpisodeEngineTests.NextCommand(engine.Snapshot)).accepted, Is.True);
            var old = PersistenceMigrationTests.StripSchema16(JObject.FromObject(engine.Snapshot, Serializer()));
            old["schemaVersion"] = 15;
            var migrated = EpisodeSaveMigrations.PrepareCurrentPayload(old, out var changed);
            Assert.That(changed, Is.True);
            Assert.That((int)migrated["schemaVersion"], Is.EqualTo(21));
            Assert.That((int)migrated["finaleRulesStartWeek"], Is.Zero);
            Assert.That((int)migrated["agencyRulesStartWeek"], Is.EqualTo((int)old["week"] + 1));
            CheckShape(migrated);
            Assert.That(EpisodeValidation.TryValidate(migrated.ToObject<EpisodeState>(Serializer()), out var error), Is.True, error);
        }

        [Test]
        public void AV20SaveCannotSmuggleTheSchema21Fields()
        {
            var old = V20();
            old["finaleRulesStartWeek"] = 1;
            Assert.Throws<InvalidDataException>(() => EpisodeSaveMigrations.PrepareCurrentPayload(old, out _));
            old = V20();
            old["finalArgument"] = JValue.CreateNull();
            Assert.Throws<InvalidDataException>(() => EpisodeSaveMigrations.PrepareCurrentPayload(old, out _));
            old = V20AtTheFinale();
            ((JObject)((JArray)old["juryExchanges"])[0])["category"] = "comparison";
            Assert.Throws<InvalidDataException>(() => EpisodeSaveMigrations.PrepareCurrentPayload(old, out _));
        }

        [Test]
        public void AV20SaveWithABrokenAgencyBoundaryIsRefused()
        {
            var old = V20();
            old["agencyRulesStartWeek"] = (int)old["week"] + 2;
            Assert.Throws<InvalidDataException>(() => EpisodeSaveMigrations.PrepareCurrentPayload(old, out _));
            old = V20();
            old["agencyRulesStartWeek"] = "1";
            Assert.Throws<InvalidDataException>(() => EpisodeSaveMigrations.PrepareCurrentPayload(old, out _));
        }
    }
}
