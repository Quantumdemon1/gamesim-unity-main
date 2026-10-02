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
    /// Schema 17: the season ledger and the read rules. A v16 save gains an empty ledger and the read
    /// switched on from the week after its own; nothing else in the save moves. The frozen step is
    /// checked on its own, and the chain is checked to reach the current schema.
    /// </summary>
    public sealed class PersistenceV17MigrationTests
    {
        private static JsonSerializer Serializer() => (JsonSerializer)typeof(EpisodeSaveStore).Assembly
            .GetType("Gamesim.Persistence.SaveJson", true).GetMethod("Serializer", BindingFlags.Public | BindingFlags.Static).Invoke(null, null);

        private static void CheckShape(JObject payload) => typeof(EpisodeSaveStore).Assembly
            .GetType("Gamesim.Persistence.SaveJson", true).GetMethod("CheckDtoShape", BindingFlags.Public | BindingFlags.Static)
            .Invoke(null, new object[] { payload, typeof(EpisodeState), "state" });

        /// <summary>A v16 save with some history in it: a few weeks played.</summary>
        private static JObject V16(uint seed = 811)
        {
            var engine = new EpisodeEngine(ContentCatalog.Create(seed));
            for (int i = 0; i < 40 && engine.Snapshot.week < 2; i++) Assert.That(engine.Apply(EpisodeEngineTests.NextCommand(engine.Snapshot)).accepted, Is.True);
            var old = PersistenceMigrationTests.StripSchema17(JObject.FromObject(engine.Snapshot, Serializer()));
            old["schemaVersion"] = 16;
            return old;
        }

        [Test]
        public void TheFrozenStepAddsAnEmptyLedgerAndTheReadBoundaryAndMovesNothingElse()
        {
            var old = V16(); string original = old.ToString();
            var migrated = EpisodeSaveMigrations.UpgradeV16ToV17(old);
            Assert.That((int)migrated["schemaVersion"], Is.EqualTo(17));
            int week = (int)old["week"];
            Assert.That((int)migrated["readRulesStartWeek"], Is.EqualTo(week + 1), "The week the save was in keeps its own rules.");
            foreach (var list in new[] { "opportunities", "competitions", "power", "ballots", "claims", "alliances", "standings" })
                Assert.That(((JArray)migrated["ledger"][list]).Count, Is.Zero, list);
            Assert.That((int)migrated["ledger"]["dropped"], Is.Zero);
            Assert.That(migrated["ledger"]["replies"], Is.Null, "Schema 17 wrote seven lists; the eighth and ninth are schema 18's.");

            var projection = PersistenceMigrationTests.StripSchema17((JObject)migrated.DeepClone());
            projection["schemaVersion"] = 16;
            Assert.That(JToken.DeepEquals(projection, old), Is.True, "Take the new fields back off and the save is exactly what it was.");
            Assert.That(old.ToString(), Is.EqualTo(original), "The original payload must not be touched.");
        }

        [Test]
        public void AV16SaveReachesTheCurrentSchemaWithTheReadOffForItsOwnWeek()
        {
            var old = V16();
            var current = EpisodeSaveMigrations.PrepareCurrentPayload(old, out var changed);
            Assert.That(changed, Is.True);
            Assert.That((int)current["schemaVersion"], Is.EqualTo(22));
            CheckShape(current);
            var state = current.ToObject<EpisodeState>(Serializer());
            Assert.That(EpisodeValidation.TryValidate(state, out var error), Is.True, error);
            Assert.That(EpisodeEngine.ReadRulesOn(state), Is.False);
            var repeated = EpisodeSaveMigrations.PrepareCurrentPayload(current, out changed);
            Assert.That(changed, Is.False);
            Assert.That(JToken.DeepEquals(repeated, current), Is.True);
        }

        [Test]
        public void TheWholeChainFromV15ReachesTheCurrentSchema()
        {
            var engine = new EpisodeEngine(ContentCatalog.Create(823));
            for (int i = 0; i < 40 && engine.Snapshot.week < 2; i++) Assert.That(engine.Apply(EpisodeEngineTests.NextCommand(engine.Snapshot)).accepted, Is.True);
            var old = PersistenceMigrationTests.StripSchema16(JObject.FromObject(engine.Snapshot, Serializer()));
            old["schemaVersion"] = 15;
            var migrated = EpisodeSaveMigrations.PrepareCurrentPayload(old, out var changed);
            Assert.That(changed, Is.True);
            Assert.That((int)migrated["schemaVersion"], Is.EqualTo(22));
            Assert.That(migrated["story"], Is.Not.Null);
            Assert.That(migrated["ledger"], Is.Not.Null);
            CheckShape(migrated);
            Assert.That(EpisodeValidation.TryValidate(migrated.ToObject<EpisodeState>(Serializer()), out var error), Is.True, error);
        }

        [TestCase("ledger")] [TestCase("readRulesStartWeek")]
        public void AV16SaveCannotSmuggleSchema17Fields(string field)
        {
            var old = V16();
            old[field] = field == "ledger" ? (JToken)new JObject() : 1;
            Assert.Throws<InvalidDataException>(() => EpisodeSaveMigrations.PrepareCurrentPayload(old, out _));
        }

        [Test]
        public void ALedgerRoundTripsThroughTheSaveShape()
        {
            var engine = new EpisodeEngine(ContentCatalog.Create(29));
            for (int i = 0; i < 400 && !(engine.Snapshot.phase == EpisodePhase.Campaign && engine.Snapshot.nominees.Count == 2); i++)
                Assert.That(engine.Apply(EpisodeEngineTests.NextCommand(engine.Snapshot)).accepted, Is.True);
            var state = engine.Snapshot;
            var voter = EpisodeEngine.Voters(state).First(v => !v.isPlayer);
            state.ledger.claims.Add(new ClaimRow { week = state.week, voterId = voter.id, targetId = state.nominees[0], source = ClaimSource.Told });
            state.ledger.standings.Add(new StandingRow { week = state.week, fromId = voter.id, toId = state.playerId, source = ClaimSource.Read, score = 12 });
            state.ledger.ballots.Add(new BallotRow { week = state.week, voterId = state.playerId, targetId = state.nominees[1], readBefore = state.nominees[1] });
            var payload = JObject.FromObject(state, Serializer());
            CheckShape(payload);
            var loaded = payload.ToObject<EpisodeState>(Serializer());
            Assert.That(EpisodeValidation.TryValidate(loaded, out var error), Is.True, error);
            Assert.That(loaded.ledger.claims.Single().targetId, Is.EqualTo(state.nominees[0]));
            Assert.That(loaded.ledger.standings.Single().score, Is.EqualTo(12));
            Assert.That(VoteRead.Read(loaded).voters.Single(r => r.voterId == voter.id).saysId, Is.EqualTo(state.nominees[0]));
            // The ledger's own invariants: a claim from nobody is not a claim.
            loaded.ledger.claims[0].voterId = "nobody";
            Assert.That(EpisodeValidation.TryValidate(loaded, out error), Is.False);
            Assert.That(error, Does.Contain("claim"));
        }
    }
}
