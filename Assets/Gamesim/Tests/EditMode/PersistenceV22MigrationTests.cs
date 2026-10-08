using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using Gamesim.Persistence;
using Gamesim.Simulation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Schema 22: the commitment rules (ACTIONS-DEALS-ALLIANCES-PLAN R0, C0, C7). A v21 save gains them
    /// switched off - a rules week of 0, so a season saved before them plays without them to its end -
    /// on every deal and every promise the two fields only a settlement under the rules fills, empty, and
    /// on every deal the link only a negotiation under the rules writes (C7), empty, and on every
    /// alliance the mark only an invitation under the rules writes (C5), false. The migration never
    /// guesses who broke something: a reader under the rules reads a deal settled before the record by
    /// FinalistRead.DealBreaker's rule, and a promise by its maker. Nothing else in the save moves.
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
            // The nominations as the week reaches them: the crowning clears the competition, so the
            // veto's is still to be played when the week goes on.
            s.phase = EpisodePhase.Nomination;
            s.hohId = head;
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
            var old = V21WithABreach(out _);
            // A pact of the player's, so the mark every alliance gains (C5) has one to be written on.
            string partner = (string)((JArray)old["contestants"]).First(c => !(bool)c["isPlayer"])["id"];
            ((JArray)old["alliances"]).Add(new JObject
            {
                ["id"] = "alliance-v21", ["name"] = "The V21 Pact", ["members"] = new JArray((string)old["playerId"], partner), ["active"] = true,
            });
            string original = old.ToString();
            var migrated = EpisodeSaveMigrations.PrepareCurrentPayload(old, out var changed);
            Assert.That(changed, Is.True);
            Assert.That((int)migrated["schemaVersion"], Is.EqualTo(27));
            Assert.That((int)migrated["commitmentRulesStartWeek"], Is.Zero, "A season saved before the rules plays without them to its end.");
            foreach (var name in new[] { "deals", "promises" })
                foreach (JObject row in (JArray)migrated[name])
                {
                    Assert.That(row["brokenById"].Type, Is.EqualTo(JTokenType.Null), name + " " + row["id"]);
                    Assert.That((int)row["settledWeek"], Is.Zero, name + " " + row["id"]);
                    // C7: every deal gains its link, empty; a promise has none.
                    if (name == "deals") Assert.That(row["linkedDealId"].Type, Is.EqualTo(JTokenType.Null), name + " " + row["id"]);
                    else Assert.That(row.Property("linkedDealId"), Is.Null, name + " " + row["id"]);
                }
            Assert.That(((JArray)migrated["deals"]).Count, Is.GreaterThan(0), "The fixture has deals for the link to be written on.");
            // C5: every alliance gains its mark, false - before the rules nobody joined a pact after the player.
            foreach (JObject row in (JArray)migrated["alliances"])
            {
                Assert.That(row["playerJoined"].Type, Is.EqualTo(JTokenType.Boolean), "alliances " + row["id"]);
                Assert.That((bool)row["playerJoined"], Is.False, "alliances " + row["id"]);
            }
            Assert.That(((JArray)migrated["alliances"]).Any(row => (string)row["id"] == "alliance-v21"), Is.True, "The fixture has a pact for the mark to be written on.");

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
            Assert.That(state.deals.All(d => d.linkedDealId == null), Is.True, "No deal of a v21 save was a price or bought with one.");
            Assert.That(state.deals.Any(Negotiation.IsPrice), Is.False);

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
        public void TheWholeChainFromV15ReachesCurrentAndFrozenDispatchStillStopsAtTwentyTwo()
        {
            var engine = new EpisodeEngine(ContentCatalog.Create(1022));
            for (int i = 0; i < 40 && engine.Snapshot.week < 2; i++) Assert.That(engine.Apply(EpisodeEngineTests.NextCommand(engine.Snapshot)).accepted, Is.True);
            var old = PersistenceMigrationTests.StripSchema16(JObject.FromObject(engine.Snapshot, Serializer()));
            old["schemaVersion"] = 15;
            var migrated = EpisodeSaveMigrations.PrepareCurrentPayload(old, out var changed);
            Assert.That(changed, Is.True);
            Assert.That((int)migrated["schemaVersion"], Is.EqualTo(27));
            Assert.That((int)migrated["commitmentRulesStartWeek"], Is.Zero);
            Assert.That((int)migrated["finaleRulesStartWeek"], Is.Zero);
            Assert.That(EpisodeSaveMigrations.PrepareV21Payload(old, out _)["schemaVersion"].Value<int>(), Is.EqualTo(21), "The frozen dispatch still stops at twenty-one.");
            var frozen22 = EpisodeSaveMigrations.PrepareV22Payload(old, out _);
            Assert.That((int)frozen22["schemaVersion"], Is.EqualTo(22));
            Assert.That(frozen22["economyRulesVersion"], Is.Null);
            Assert.That(frozen22["moveInExtrasSpent"], Is.Null);
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
            // C7's link, empty or naming a deal, is no more a v21 field than the record is.
            foreach (var link in new JToken[] { JValue.CreateNull(), new JValue("deal-pact") })
            {
                old = V21WithABreach(out _);
                ((JObject)((JArray)old["deals"]).First(d => (string)d["id"] == "deal-pact"))["linkedDealId"] = link;
                Assert.Throws<InvalidDataException>(() => EpisodeSaveMigrations.PrepareCurrentPayload(old, out _), "linkedDealId " + link);
            }
            // Nor is C5's mark on an alliance, either way.
            foreach (bool joined in new[] { false, true })
            {
                old = V21WithABreach(out _);
                string partner = (string)((JArray)old["contestants"]).First(c => !(bool)c["isPlayer"])["id"];
                ((JArray)old["alliances"]).Add(new JObject
                {
                    ["id"] = "alliance-v21", ["name"] = "The V21 Pact", ["members"] = new JArray((string)old["playerId"], partner),
                    ["active"] = true, ["playerJoined"] = joined,
                });
                Assert.Throws<InvalidDataException>(() => EpisodeSaveMigrations.PrepareCurrentPayload(old, out _), "playerJoined " + joined);
            }
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

            // Schema 21's own shape contract carries this argument unchanged at its frozen step.
            // That shape proof is not acceptance of a complete current save: this played state
            // has not reached the final eviction, so former24's full semantics must refuse it.
            old = V21WithABreach(out _);
            var argument = new JObject { ["theme"] = FinalArgument.Themes[0], ["momentRefs"] = new JArray("win:1:HoH") };
            old["finalArgument"] = argument;
            string original = old.ToString(Formatting.None);
            var migrated = EpisodeSaveMigrations.UpgradeV21ToV22(old);
            Assert.That((int)migrated["schemaVersion"], Is.EqualTo(22), "The frozen shape step stops at twenty-two.");
            Assert.That(JToken.DeepEquals(migrated["finalArgument"], argument), Is.True);
            Assert.That(old.ToString(Formatting.None), Is.EqualTo(original));
            var refusal = Assert.Throws<InvalidDataException>(() => EpisodeSaveMigrations.PrepareCurrentPayload(old, out _));
            Assert.That(refusal.Message, Does.Contain("Finale records cannot precede the final eviction."));
            Assert.That(old.ToString(Formatting.None), Is.EqualTo(original), "The whole-chain refusal leaves historical bytes unchanged.");
        }

        private static string V21FixturePath =>
#if UNITY_EDITOR
            Path.Combine(UnityEngine.Application.dataPath, "Gamesim", "Tests", "EditMode", "Fixtures", "V21CommitmentsSave.json");
#else
            Path.Combine(AppContext.BaseDirectory, "V21CommitmentsSave.json");
#endif

        /// <summary>
        /// A save the schema 21 build wrote itself (Fixtures/README.md), not a current capture with the
        /// new fields taken off: through the whole chain it gains the rules switched off and empty
        /// records and nothing else, and what that build settled reads, once the rules are on, by
        /// DealBreaker's rule for the deal and by its maker for the promise.
        /// </summary>
        [Test]
        public void ASaveTheV21BuildWroteLoadsThroughTheChain()
        {
            var written = JObject.Parse(File.ReadAllText(V21FixturePath));
            var payload = (JObject)written["state"];
            Assert.That((int)payload["schemaVersion"], Is.EqualTo(21), "The fixture is the schema 21 build's own save.");
            Assert.That(payload["commitmentRulesStartWeek"], Is.Null, "and it knows nothing of the commitment rules.");

            using var files = new Files();
            // Sealed again for the runtime this runs in, not a value of the state changed. The envelope's
            // checksum covers the canonical text of the state, and a double's round-trip text is the
            // runtime's own: the .NET that ran the v21 build writes 4.625035332515836, where Unity's Mono
            // can write a seventeenth digit - the editor read the file as written as damaged.
            files.Write(payload);
            var originalBytes = File.ReadAllBytes(files.Store.SavePath);
            Assert.That(files.Store.TryLoad(out var loaded, out string message), Is.True, message);
            Assert.That(message, Does.Contain("Schema 21").And.Contain("schema 27 in memory"));
            Assert.That(File.ReadAllBytes(files.Store.SavePath), Is.EqualTo(originalBytes), "Loading must not rewrite the file.");
            Assert.That(loaded.schemaVersion, Is.EqualTo(27));
            Assert.That(loaded.commitmentRulesStartWeek, Is.Zero, "It plays without the rules to its end.");
            Assert.That(loaded.deals.All(d => d.brokenById == null && d.settledWeek == 0), Is.True, "No deal's breaker is guessed.");
            Assert.That(loaded.promises.All(p => p.brokenById == null && p.settledWeek == 0), Is.True, "Nor any promise's.");
            Assert.That(loaded.deals.All(d => d.linkedDealId == null), Is.True, "Nor any deal's link (C7).");
            Assert.That(EpisodeValidation.TryValidate(loaded, out var error), Is.True, error);

            var migrated = EpisodeSaveMigrations.PrepareCurrentPayload(payload, out bool changed);
            Assert.That(changed, Is.True);
            var projection = PersistenceMigrationTests.StripSchema22((JObject)migrated.DeepClone());
            projection["schemaVersion"] = 21;
            Assert.That(JToken.DeepEquals(projection, payload), Is.True, "Take schema 22's fields back off and the save is the v21 build's.");

            var pact = loaded.deals.Single(d => d.id == "deal-pact");
            var promise = loaded.promises.Single(p => p.id == "promise-pact");
            Assert.That(pact.status, Is.EqualTo(DealStatus.Broken), "The Head of Household put their safety partner up,");
            Assert.That(promise.status, Is.EqualTo(PromiseStatus.Broken), "and broke their promise of safety.");
            Assert.That(loaded.deals.Single(d => d.id == "deal-bloc").status, Is.EqualTo(DealStatus.Fulfilled), "The bloc held.");
            loaded.commitmentRulesStartWeek = loaded.week;
            Assert.That(EpisodeValidation.TryValidate(loaded, out error), Is.True, error);
            Assert.That(Breaches.DealBreaker(loaded, pact), Is.EqualTo(loaded.hohId), "Read by the record the v21 build kept: who put whom up.");
            Assert.That(Breaches.DealBreaker(loaded, pact), Is.EqualTo(FinalistRead.DealBreaker(loaded, pact)));
            Assert.That(Breaches.PromiseBreaker(promise), Is.EqualTo(loaded.hohId), "A promise is broken by its maker.");
            Assert.That(Breaches.CountsAgainst(loaded, pact, loaded.playerId), Is.False, "The player it was broken against is not held to it.");
        }

        [Test]
        public void AV21SaveOnDiskLoadsUnchangedAndOnlyRewritesOnAnExplicitSave()
        {
            using var files = new Files();
            files.Write(V21WithABreach(out _));
            var originalBytes = File.ReadAllBytes(files.Store.SavePath);

            Assert.That(files.Store.TryLoad(out var loaded, out string message), Is.True, message);
            Assert.That(message, Does.Contain("Schema 21").And.Contain("schema 27 in memory"));
            Assert.That(loaded.schemaVersion, Is.EqualTo(27));
            Assert.That(loaded.commitmentRulesStartWeek, Is.Zero);
            Assert.That(loaded.deals.Single(d => d.id == "deal-pact").brokenById, Is.Null);
            Assert.That(File.ReadAllBytes(files.Store.SavePath), Is.EqualTo(originalBytes), "Loading must not rewrite the file.");

            files.Store.Save(loaded);
            Assert.That(File.ReadAllBytes(files.Store.BackupPath), Is.EqualTo(originalBytes), "The pre-migration bytes are kept as the backup.");
            Assert.That(files.Store.TryLoad(out var again, out message), Is.True, message);
            Assert.That(message, Does.Not.Contain("migrated"));
            Assert.That(again.commitmentRulesStartWeek, Is.Zero);
        }

#if UNITY_EDITOR
        /// <summary>
        /// The untouched bytes of an actual Unity standalone save, including its original checksum.
        /// This is native Editor/Mono persistence coverage, not a new standalone or scene-install run.
        /// </summary>
        [Test]
        public void UntouchedV21StandaloneSaveMigratesInUnityAndRetainsExactBytesUntilExplicitSave()
        {
            const string originalHash = "ec715f20e9e8be1b62d4edfc32cd876b683f13867e47461935abf5a16596f78c";
            string fixture = Path.Combine(UnityEngine.Application.dataPath, "Gamesim", "Tests", "EditMode", "Fixtures", "V21StandaloneProfileSave.json");
            var originalBytes = File.ReadAllBytes(fixture);
            Assert.That(originalBytes.Length, Is.EqualTo(28122));
            Assert.That(HashBytes(originalBytes), Is.EqualTo(originalHash),
                "The fixture keeps the shipping player's original envelope and CRLF bytes; never reseal it.");
            var originalPayload = (JObject)JObject.Parse(File.ReadAllText(fixture))["state"];
            Assert.That((int)originalPayload["schemaVersion"], Is.EqualTo(21));

            using var files = new Files();
            File.Copy(fixture, files.Store.SavePath, overwrite: false);
            Assert.That(HashBytes(File.ReadAllBytes(files.Store.SavePath)), Is.EqualTo(originalHash));
            Assert.That(files.Store.TryLoad(out var loaded, out string message), Is.True, message);
            Assert.That(message, Does.Contain("Schema 21").And.Contain("schema 27 in memory"));
            Assert.That(loaded.schemaVersion, Is.EqualTo(27));
            Assert.That(loaded.commitmentRulesStartWeek, Is.Zero);
            Assert.That(File.ReadAllBytes(files.Store.SavePath), Is.EqualTo(originalBytes), "Loading never rewrites the original save.");
            Assert.That(File.Exists(files.Store.BackupPath), Is.False, "Loading does not create or rotate a backup.");

            var expected = JObject.FromObject(loaded, Serializer());
            var projection = PersistenceMigrationTests.StripSchema22((JObject)expected.DeepClone());
            projection["schemaVersion"] = 21;
            Assert.That(JToken.DeepEquals(projection, originalPayload), Is.True, "Every field written by the old player survives unchanged.");
            files.Store.Save(loaded);
            Assert.That(File.ReadAllBytes(files.Store.BackupPath), Is.EqualTo(originalBytes), "The explicit save retains the entire original envelope as its backup.");
            Assert.That(HashBytes(File.ReadAllBytes(files.Store.BackupPath)), Is.EqualTo(originalHash));
            var savedBytes = File.ReadAllBytes(files.Store.SavePath);
            var savedPayload = (JObject)JObject.Parse(File.ReadAllText(files.Store.SavePath))["state"];
            Assert.That((int)savedPayload["schemaVersion"], Is.EqualTo(27));
            Assert.That(JToken.DeepEquals(savedPayload, expected), Is.True);
            Assert.That(files.Store.TryLoad(out var reloaded, out message), Is.True, message);
            Assert.That(message, Does.Not.Contain("migrated"));
            Assert.That(JToken.DeepEquals(JObject.FromObject(reloaded, Serializer()), expected), Is.True, "Reload installs the same validated migrated state.");
            Assert.That(File.ReadAllBytes(files.Store.SavePath), Is.EqualTo(savedBytes), "Reloading the new schema is read-only too.");
            Assert.That(File.ReadAllBytes(files.Store.BackupPath), Is.EqualTo(originalBytes));
            Assert.That(File.ReadAllBytes(fixture), Is.EqualTo(originalBytes), "Only the isolated copy is ever a save target.");
        }

        private static string HashBytes(byte[] bytes)
        {
            using var hash = SHA256.Create();
            return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }
#endif

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
