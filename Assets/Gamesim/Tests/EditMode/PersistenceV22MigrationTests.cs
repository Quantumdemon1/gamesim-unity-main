using System;
using System.Collections.Generic;
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
    /// Schema 22: the commitment rules (ACTIONS-DEALS-ALLIANCES-PLAN R0, C0). A v21 save gains them
    /// switched off - a rules week of 0, so a season saved before them plays without them to its end -
    /// and on every deal and every promise the two fields only a settlement under the rules fills,
    /// empty. The migration never guesses who broke something: a reader under the rules reads a deal
    /// settled before the record by FinalistRead.DealBreaker's rule, and a promise by its maker.
    /// Nothing else in the save moves.
    /// </summary>
    public sealed class PersistenceV22MigrationTests
    {
        private static JsonSerializer Serializer() => (JsonSerializer)typeof(EpisodeSaveStore).Assembly
            .GetType("Gamesim.Persistence.SaveJson", true).GetMethod("Serializer", BindingFlags.Public | BindingFlags.Static).Invoke(null, null);

        private static void CheckShape(JObject payload) => typeof(EpisodeSaveStore).Assembly
            .GetType("Gamesim.Persistence.SaveJson", true).GetMethod("CheckDtoShape", BindingFlags.Public | BindingFlags.Static)
            .Invoke(null, new object[] { payload, typeof(EpisodeState), "state" });

        private static JObject AsV21(EpisodeState state)
        {
            var old = PersistenceMigrationTests.StripSchema22(JObject.FromObject(state, Serializer()));
            old["schemaVersion"] = 21;
            return old;
        }

        /// <summary>
        /// A v21 save in which a Head of Household broke a safety pact and a safety promise to the
        /// player at the nominations, played on through that week's eviction so the record shows who
        /// put whom up: a deal and a promise settled before the record of who broke them existed.
        /// </summary>
        private static JObject V21WithABreach(out string hoh)
        {
            var s = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, 29);
            string head = s.contestants.First(c => !c.isPlayer).id;
            hoh = head;
            s.phase = EpisodePhase.Nomination;
            s.hohId = head;
            s.competitionResolved = true;
            foreach (var edge in s.relationships.Where(r => r.fromId == head))
                edge.score = edge.toId == s.playerId ? -90 : 60;
            s.deals.Add(new DealState
            {
                id = "deal-pact", type = DealKind.SafetyAgreement, proposerId = s.playerId, recipientId = head,
                status = DealStatus.Active, week = 1, expiresWeek = 2, trustImpact = DealKind.DefaultTrust(DealKind.SafetyAgreement),
            });
            s.promises.Add(new PromiseState { id = "promise-pact", fromId = head, toId = s.playerId, kind = PromiseKind.Safety, status = PromiseStatus.Active, week = 1, expiresWeek = 2 });
            var engine = new EpisodeEngine(s);
            for (int i = 0; i < 60 && !engine.Snapshot.evictionResolved; i++)
            {
                var result = engine.Apply(EpisodeEngineTests.NextCommand(engine.Snapshot));
                Assert.That(result.accepted, Is.True, result.reason);
            }
            var played = engine.Snapshot;
            Assert.That(played.evictionResolved, Is.True, "The week reached its eviction.");
            Assert.That(played.deals.Single(d => d.id == "deal-pact").status, Is.EqualTo(DealStatus.Broken));
            Assert.That(played.promises.Single(p => p.id == "promise-pact").status, Is.EqualTo(PromiseStatus.Broken));
            return AsV21(played);
        }

        [Test]
        public void MigrationAddsTheRulesSwitchedOffAndEmptyRecordsAndMovesNothingElse()
        {
            var old = V21WithABreach(out _); string original = old.ToString();
            var migrated = EpisodeSaveMigrations.PrepareCurrentPayload(old, out var changed);
            Assert.That(changed, Is.True);
            Assert.That((int)migrated["schemaVersion"], Is.EqualTo(22));
            Assert.That((int)migrated["commitmentRulesStartWeek"], Is.Zero, "A season saved before the rules plays without them to its end.");
            foreach (var name in new[] { "deals", "promises" })
                foreach (JObject row in (JArray)migrated[name])
                {
                    Assert.That(row["brokenById"].Type, Is.EqualTo(JTokenType.Null), name + " " + row["id"]);
                    Assert.That((int)row["settledWeek"], Is.Zero, name + " " + row["id"]);
                }

            var projection = PersistenceMigrationTests.StripSchema22((JObject)migrated.DeepClone());
            projection["schemaVersion"] = 21;
            Assert.That(JToken.DeepEquals(projection, old), Is.True, "Take the new fields back off and the save is exactly what it was.");
            Assert.That(old.ToString(), Is.EqualTo(original), "The original payload must not be touched.");

            CheckShape(migrated);
            var state = migrated.ToObject<EpisodeState>(Serializer());
            Assert.That(EpisodeValidation.TryValidate(state, out var error), Is.True, error);
            Assert.That(EpisodeEngine.CommitmentRulesOn(state), Is.False);

            var repeated = EpisodeSaveMigrations.PrepareCurrentPayload(migrated, out changed);
            Assert.That(changed, Is.False);
            Assert.That(JToken.DeepEquals(repeated, migrated), Is.True);
        }

        [Test]
        public void TheMigrationNeverGuessesWhoBrokeItAndAReaderUnderTheRulesUsesDealBreakersRule()
        {
            var old = V21WithABreach(out string hoh);
            var state = EpisodeSaveMigrations.PrepareCurrentPayload(old, out _).ToObject<EpisodeState>(Serializer());
            var deal = state.deals.Single(d => d.id == "deal-pact");
            var promise = state.promises.Single(p => p.id == "promise-pact");
            Assert.That(deal.brokenById, Is.Null, "Who broke it was never recorded, so the migration writes nothing.");
            Assert.That(promise.brokenById, Is.Null);

            // The same season under the rules, as an import's or a later rule's would be.
            state.commitmentRulesStartWeek = state.week;
            Assert.That(EpisodeValidation.TryValidate(state, out var error), Is.True, error);
            Assert.That(EpisodeEngine.CommitmentRulesOn(state), Is.True);
            Assert.That(FinalistRead.DealBreaker(state, deal), Is.EqualTo(hoh), "The record shows the Head of Household put the player up.");
            Assert.That(Breaches.DealBreaker(state, deal), Is.EqualTo(FinalistRead.DealBreaker(state, deal)), "The fallback is DealBreaker's answer.");
            Assert.That(Breaches.PromiseBreaker(promise), Is.EqualTo(hoh), "A promise is broken by its maker.");
            Assert.That(Breaches.CountsAgainst(state, deal, hoh), Is.True, "The one who broke it is held to it,");
            Assert.That(Breaches.CountsAgainst(state, deal, state.playerId), Is.False, "and the player, wronged, is not.");
            Assert.That(NpcDeals.BrokenDeals(state, state.playerId), Is.Zero);
            foreach (var broken in state.deals.Where(d => d.status == DealStatus.Broken))
                Assert.That(Breaches.DealBreaker(state, broken), Is.EqualTo(FinalistRead.DealBreaker(state, broken)), broken.id);
        }

        [Test]
        public void TheWholeChainFromV15ReachesTwentyTwo()
        {
            var engine = new EpisodeEngine(ContentCatalog.Create(1022));
            for (int i = 0; i < 40 && engine.Snapshot.week < 2; i++) Assert.That(engine.Apply(EpisodeEngineTests.NextCommand(engine.Snapshot)).accepted, Is.True);
            var old = PersistenceMigrationTests.StripSchema16(JObject.FromObject(engine.Snapshot, Serializer()));
            old["schemaVersion"] = 15;
            var migrated = EpisodeSaveMigrations.PrepareCurrentPayload(old, out var changed);
            Assert.That(changed, Is.True);
            Assert.That((int)migrated["schemaVersion"], Is.EqualTo(22));
            Assert.That((int)migrated["commitmentRulesStartWeek"], Is.Zero);
            Assert.That((int)migrated["finaleRulesStartWeek"], Is.Zero);
            Assert.That(EpisodeSaveMigrations.PrepareV21Payload(old, out _)["schemaVersion"].Value<int>(), Is.EqualTo(21), "The frozen dispatch still stops at twenty-one.");
            CheckShape(migrated);
            Assert.That(EpisodeValidation.TryValidate(migrated.ToObject<EpisodeState>(Serializer()), out var error), Is.True, error);
        }

        [Test]
        public void AV21SaveCannotSmuggleTheSchema22Fields()
        {
            var old = V21WithABreach(out _);
            old["commitmentRulesStartWeek"] = 0;
            Assert.Throws<InvalidDataException>(() => EpisodeSaveMigrations.PrepareCurrentPayload(old, out _));
            old = V21WithABreach(out string hoh);
            ((JObject)((JArray)old["deals"]).First(d => (string)d["id"] == "deal-pact"))["brokenById"] = hoh;
            Assert.Throws<InvalidDataException>(() => EpisodeSaveMigrations.PrepareCurrentPayload(old, out _));
            old = V21WithABreach(out _);
            ((JObject)((JArray)old["promises"]).First(p => (string)p["id"] == "promise-pact"))["settledWeek"] = 1;
            Assert.Throws<InvalidDataException>(() => EpisodeSaveMigrations.PrepareCurrentPayload(old, out _));
        }

        [Test]
        public void AV21SavesFinaleFieldsAreHeldToTheShapeSchema21Wrote()
        {
            var old = V21WithABreach(out _);
            old["finaleRulesStartWeek"] = (int)old["week"] + 2;
            Assert.Throws<InvalidDataException>(() => EpisodeSaveMigrations.PrepareCurrentPayload(old, out _));
            old = V21WithABreach(out _);
            old["finalArgument"] = new JObject { ["theme"] = FinalArgument.Themes[0] };
            Assert.Throws<InvalidDataException>(() => EpisodeSaveMigrations.PrepareCurrentPayload(old, out _), "An argument without its moments.");
            old = V21WithABreach(out _);
            old["finalArgument"] = new JObject { ["theme"] = FinalArgument.Themes[0], ["momentRefs"] = new JArray(1) };
            Assert.Throws<InvalidDataException>(() => EpisodeSaveMigrations.PrepareCurrentPayload(old, out _), "A moment is a reference, never a number.");

            // What schema 21 could hold passes its frozen contract and is carried unchanged; its
            // meaning is the live finale validator's to judge once loaded.
            old = V21WithABreach(out _);
            var argument = new JObject { ["theme"] = FinalArgument.Themes[0], ["momentRefs"] = new JArray("win:1:HoH") };
            old["finalArgument"] = argument;
            var migrated = EpisodeSaveMigrations.PrepareCurrentPayload(old, out _);
            Assert.That(JToken.DeepEquals(migrated["finalArgument"], argument), Is.True);
        }

        [Test]
        public void AV21SaveOnDiskLoadsUnchangedAndOnlyRewritesOnAnExplicitSave()
        {
            using var files = new Files();
            files.Write(V21WithABreach(out _));
            var originalBytes = File.ReadAllBytes(files.Store.SavePath);

            Assert.That(files.Store.TryLoad(out var loaded, out string message), Is.True, message);
            Assert.That(message, Does.Contain("Schema 21").And.Contain("schema 22 in memory"));
            Assert.That(loaded.schemaVersion, Is.EqualTo(22));
            Assert.That(loaded.commitmentRulesStartWeek, Is.Zero);
            Assert.That(loaded.deals.Single(d => d.id == "deal-pact").brokenById, Is.Null);
            Assert.That(File.ReadAllBytes(files.Store.SavePath), Is.EqualTo(originalBytes), "Loading must not rewrite the file.");

            files.Store.Save(loaded);
            Assert.That(File.ReadAllBytes(files.Store.BackupPath), Is.EqualTo(originalBytes), "The pre-migration bytes are kept as the backup.");
            Assert.That(files.Store.TryLoad(out var again, out message), Is.True, message);
            Assert.That(message, Does.Not.Contain("migrated"));
            Assert.That(again.commitmentRulesStartWeek, Is.Zero);
        }

        private sealed class Files : IDisposable
        {
            public readonly string DirectoryPath =
                Path.Combine(Path.GetTempPath(), "GamesimV22MigrationTests-" + Guid.NewGuid().ToString("N"));
            public readonly EpisodeSaveStore Store;

            public Files()
            {
                Directory.CreateDirectory(DirectoryPath);
                Store = new EpisodeSaveStore(Path.Combine(DirectoryPath, "episode.json"));
            }

            public void Write(JObject payload) =>
                File.WriteAllText(Store.SavePath, PersistenceMigrationTests.Envelope(payload));

            public void Dispose()
            {
                try { Directory.Delete(DirectoryPath, true); } catch (IOException) { }
            }
        }
    }
}
