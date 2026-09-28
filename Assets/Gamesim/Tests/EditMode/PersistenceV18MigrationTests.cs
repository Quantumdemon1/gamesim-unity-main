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
    /// Schema 18: the levers. A v17 save gains two empty ledger lists (replies, calls) and the levers
    /// switched on from the week after its own; nothing else in the save moves.
    /// </summary>
    public sealed class PersistenceV18MigrationTests
    {
        private static JsonSerializer Serializer() => (JsonSerializer)typeof(EpisodeSaveStore).Assembly
            .GetType("Gamesim.Persistence.SaveJson", true).GetMethod("Serializer", BindingFlags.Public | BindingFlags.Static).Invoke(null, null);

        private static void CheckShape(JObject payload) => typeof(EpisodeSaveStore).Assembly
            .GetType("Gamesim.Persistence.SaveJson", true).GetMethod("CheckDtoShape", BindingFlags.Public | BindingFlags.Static)
            .Invoke(null, new object[] { payload, typeof(EpisodeState), "state" });

        /// <summary>A v17 save with some history in it: a few weeks played, and a ledger with a row or two.</summary>
        private static JObject V17(uint seed = 907)
        {
            var engine = new EpisodeEngine(ContentCatalog.Create(seed));
            for (int i = 0; i < 40 && engine.Snapshot.week < 2; i++) Assert.That(engine.Apply(EpisodeEngineTests.NextCommand(engine.Snapshot)).accepted, Is.True);
            var state = engine.Snapshot;
            var npc = state.Active.First(c => !c.isPlayer);
            state.ledger.standings.Add(new StandingRow { week = 1, fromId = npc.id, toId = state.playerId, source = ClaimSource.Read, score = 9 });
            var old = PersistenceMigrationTests.StripSchema18(JObject.FromObject(state, Serializer()));
            old["schemaVersion"] = 17;
            return old;
        }

        [Test]
        public void MigrationAddsTheTwoLedgerListsAndTheLeverBoundaryAndMovesNothingElse()
        {
            var old = V17(); string original = old.ToString();
            var migrated = EpisodeSaveMigrations.UpgradeV17ToV18(old);
            Assert.That((int)migrated["schemaVersion"], Is.EqualTo(18));
            int week = (int)old["week"];
            Assert.That((int)migrated["leverRulesStartWeek"], Is.EqualTo(week + 1), "The week the save was in keeps its own rules.");
            Assert.That(((JArray)migrated["ledger"]["replies"]).Count, Is.Zero);
            Assert.That(((JArray)migrated["ledger"]["calls"]).Count, Is.Zero);
            Assert.That(((JArray)migrated["ledger"]["standings"]).Count, Is.GreaterThanOrEqualTo(1), "What the ledger held is still there.");

            var projection = PersistenceMigrationTests.StripSchema18((JObject)migrated.DeepClone());
            projection["schemaVersion"] = 17;
            Assert.That(JToken.DeepEquals(projection, old), Is.True, "Take the new fields back off and the save is exactly what it was.");
            Assert.That(old.ToString(), Is.EqualTo(original), "The original payload must not be touched.");

            var current = EpisodeSaveMigrations.PrepareCurrentPayload(old, out var changed);
            Assert.That(changed, Is.True);
            Assert.That((int)current["schemaVersion"], Is.EqualTo(19));
            CheckShape(current);
            var state = current.ToObject<EpisodeState>(Serializer());
            Assert.That(EpisodeValidation.TryValidate(state, out var error), Is.True, error);
            Assert.That(EpisodeEngine.LeverRulesOn(state), Is.False);
            Assert.That(EpisodeEngine.Obligations(state, state.Active.First(c => !c.isPlayer).id), Is.Empty, "No levers, no obligations.");

            var repeated = EpisodeSaveMigrations.PrepareCurrentPayload(current, out changed);
            Assert.That(changed, Is.False);
            Assert.That(JToken.DeepEquals(repeated, current), Is.True);
        }

        [Test]
        public void TheWholeChainFromV15ReachesEighteen()
        {
            var engine = new EpisodeEngine(ContentCatalog.Create(911));
            for (int i = 0; i < 40 && engine.Snapshot.week < 2; i++) Assert.That(engine.Apply(EpisodeEngineTests.NextCommand(engine.Snapshot)).accepted, Is.True);
            var old = PersistenceMigrationTests.StripSchema16(JObject.FromObject(engine.Snapshot, Serializer()));
            old["schemaVersion"] = 15;
            var migrated = EpisodeSaveMigrations.PrepareCurrentPayload(old, out var changed);
            Assert.That(changed, Is.True);
            Assert.That((int)migrated["schemaVersion"], Is.EqualTo(19));
            Assert.That(migrated["story"], Is.Not.Null);
            Assert.That(migrated["ledger"]["replies"], Is.Not.Null);
            Assert.That((int)migrated["leverRulesStartWeek"], Is.EqualTo((int)old["week"] + 1));
            CheckShape(migrated);
            Assert.That(EpisodeValidation.TryValidate(migrated.ToObject<EpisodeState>(Serializer()), out var error), Is.True, error);
        }

        [TestCase("leverRulesStartWeek")] [TestCase("replies")] [TestCase("calls")]
        public void AV17SaveCannotSmuggleSchema18Fields(string field)
        {
            var old = V17();
            if (field == "leverRulesStartWeek") old[field] = 1;
            else ((JObject)old["ledger"])[field] = new JArray();
            Assert.Throws<InvalidDataException>(() => EpisodeSaveMigrations.PrepareCurrentPayload(old, out _));
        }

        [Test]
        public void AReplyRowAndACallRowRoundTripThroughTheSaveShape()
        {
            var engine = new EpisodeEngine(ContentCatalog.Create(31));
            for (int i = 0; i < 400 && !(engine.Snapshot.phase == EpisodePhase.Campaign && engine.Snapshot.nominees.Count == 2); i++)
                Assert.That(engine.Apply(EpisodeEngineTests.NextCommand(engine.Snapshot)).accepted, Is.True);
            var state = engine.Snapshot;
            var npc = EpisodeEngine.Voters(state).First(v => !v.isPlayer);
            state.ledger.replies.Add(new ReplyRow { week = state.week, cardId = "card-1", kind = "plea", fromId = npc.id, replyKey = "promise", promised = true, toThem = 8 });
            state.ledger.calls.Add(new BlocCallRow { week = state.week, allianceId = "alliance-1", callerId = state.playerId, targetId = state.nominees[0], followed = { npc.id } });
            var payload = JObject.FromObject(state, Serializer());
            CheckShape(payload);
            var loaded = payload.ToObject<EpisodeState>(Serializer());
            Assert.That(EpisodeValidation.TryValidate(loaded, out var error), Is.True, error);
            Assert.That(loaded.ledger.replies.Single().replyKey, Is.EqualTo("promise"));
            Assert.That(loaded.ledger.calls.Single().followed, Is.EqualTo(new[] { npc.id }));
            // The ledger's own invariants: a reply from nobody is not a reply, and a call on nobody is not a call.
            loaded.ledger.replies[0].fromId = "nobody";
            Assert.That(EpisodeValidation.TryValidate(loaded, out error), Is.False);
            Assert.That(error, Does.Contain("reply"));
            loaded.ledger.replies[0].fromId = npc.id;
            loaded.ledger.calls[0].targetId = "nobody";
            Assert.That(EpisodeValidation.TryValidate(loaded, out error), Is.False);
            Assert.That(error, Does.Contain("call"));
        }
    }
}
