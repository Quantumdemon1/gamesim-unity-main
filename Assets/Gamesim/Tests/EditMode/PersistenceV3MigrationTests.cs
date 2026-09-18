using System;
using System.IO;
using System.Linq;
using System.Text;
using Gamesim.Persistence;
using Gamesim.Simulation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    public sealed class PersistenceV3MigrationTests
    {
        [TestCase(0)]
        [TestCase(12)]
        [TestCase(13)]
        [TestCase(14)]
        [TestCase(15)]
        public void FrozenV2Upgrade_PreservesAllOriginalHistoryAndPhase(int phase)
        {
            var original = RichShapeFixture(); original["phase"] = phase;
            var before = original.ToString(Formatting.None);
            var defaults = new JObject { ["newLedger"] = new JArray() };
            var result = EpisodeSaveMigrations.UpgradeV2ToV3(original, defaults);
            Assert.That((int)result["schemaVersion"], Is.EqualTo(3));
            Assert.That((int)result["phase"], Is.EqualTo(phase));
            foreach (var property in original.Properties())
                if (property.Name != "schemaVersion") Assert.That(JToken.DeepEquals(property.Value, result[property.Name]), Is.True, property.Name);
            result["relationships"][0]["events"][0]["description"] = "detached mutation";
            result["juryExchanges"][0]["answer"] = "detached answer";
            ((JArray)result["newLedger"]).Add(1);
            Assert.That(original.ToString(Formatting.None), Is.EqualTo(before));
            Assert.That((JArray)defaults["newLedger"], Is.Empty);
        }

        [TestCase("")]
        [TestCase("contestants[0]")]
        [TestCase("contestants[0].stats")]
        [TestCase("relationships[0]")]
        [TestCase("relationships[0].events[0]")]
        [TestCase("promises[0]")]
        [TestCase("alliances[0]")]
        [TestCase("memories[0]")]
        [TestCase("votes[0]")]
        [TestCase("competitionScores[0]")]
        [TestCase("events[0]")]
        [TestCase("juryExchanges[0]")]
        [TestCase("finalSpeeches[0]")]
        [TestCase("relationshipArcs[0]")]
        [TestCase("relationshipArcs[0].weeklyHistory[0]")]
        public void FrozenV2Contract_RejectsMissingAndUnknownFieldsAtEveryNestedRecord(string path)
        {
            foreach (bool unknown in new[] { false, true })
            {
                var original = RichShapeFixture();
                var target = path == "" ? original : (JObject)original.SelectToken(path);
                if (unknown) target["futureSchemaField"] = "must not be silently accepted";
                else target.Properties().Last().Remove();
                var before = original.ToString(Formatting.None);
                Assert.Throws<InvalidDataException>(() => EpisodeSaveMigrations.UpgradeV2ToV3(original, new JObject()), path);
                Assert.That(original.ToString(Formatting.None), Is.EqualTo(before));
            }
        }

        [TestCase("phase", "16")]
        [TestCase("phase", "-1")]
        [TestCase("phase", "1.5")]
        [TestCase("phase", "'Social'")]
        [TestCase("events[0].phase", "16")]
        [TestCase("contestants[0].status", "5")]
        [TestCase("promises[0].kind", "5")]
        [TestCase("promises[0].status", "4")]
        [TestCase("juryExchanges[0].completed", "'true'")]
        [TestCase("relationships[0].lastInteractionWeek", "1.25")]
        [TestCase("relationshipArcs[0].weeklyHistory[0].delta", "'15'")]
        [TestCase("contestants", "null")]
        public void FrozenV2Contract_RejectsUnsupportedTypesAndEnumOrdinals(string path, string value)
        {
            var original = RichShapeFixture(); original.SelectToken(path).Replace(JToken.Parse(value));
            var before = original.ToString(Formatting.None);
            Assert.Throws<InvalidDataException>(() => EpisodeSaveMigrations.UpgradeV2ToV3(original, new JObject()));
            Assert.That(original.ToString(Formatting.None), Is.EqualTo(before));
        }

        [TestCase("schemaVersion")]
        [TestCase("randomState")]
        [TestCase("juryExchanges")]
        [TestCase("finalSpeeches")]
        [TestCase("relationships")]
        public void FrozenV2Upgrade_RefusesDefaultOverwrite(string field)
        {
            var original = V2Fixture(); var before = original.ToString(Formatting.None);
            Assert.Throws<InvalidDataException>(() => EpisodeSaveMigrations.UpgradeV2ToV3(original, new JObject { [field] = null }));
            Assert.That(original.ToString(Formatting.None), Is.EqualTo(before));
        }

        [TestCase(1)]
        [TestCase(3)]
        [TestCase(99)]
        public void FrozenV2Upgrade_RejectsWrongVersions(int version)
        {
            var original = V2Fixture(); original["schemaVersion"] = version;
            Assert.Throws<InvalidDataException>(() => EpisodeSaveMigrations.UpgradeV2ToV3(original, new JObject()));
        }

        [TestCase(1)]
        [TestCase(2)]
        public void CurrentDispatch_UsesOnlyReviewedDefaultsAndDoesNotInventHistoricalOathsOrDiary(int version)
        {
            var source = version == 1 ? PersistenceMigrationTests.V1Fixture() : V2Fixture();
            var before = source.ToString(Formatting.None);
            var current = EpisodeSaveMigrations.PrepareCurrentPayload(source, out var migrated);
            Assert.That(migrated, Is.True);
            Assert.That((int)current["schemaVersion"], Is.EqualTo(10));
            var oldV2 = version == 1 ? EpisodeSaveMigrations.PrepareV2Payload(source, out _) : source;
            Assert.That(current.Properties().Select(p => p.Name).Except(oldV2.Properties().Select(p => p.Name)), Is.EquivalentTo(new[]
            {
                "playerPersona", "jurySentiment", "pendingDiary", "lastDiaryRoomWeek", "phaseEventSocialBonus",
                "phaseEventCompBonus", "resolvedDiaryIds", "loyaltyOaths", "oathOpportunities", "shownOathMilestones", "playerStudyBonus", "blocRulesStartWeek", "npcSocial",
                // Schema 8.
                "evictionStage", "evictionSpeeches", "backdoorTargetId", "outOfPhaseSocialActions", "openingBeatsSeen",
                // Schema 9.
                "socialBudgetRulesStartWeek",
                // Schema 10.
                "deals", "dealRulesStartWeek"
            }));
            Assert.That((string)current["playerPersona"]["current"], Is.EqualTo("Neutral"));
            Assert.That(current["playerPersona"]["scores"].Select(item => (string)item["persona"]),
                Is.EqualTo(new[] { "Neutral", "Remorseful", "Ruthless", "Calculated", "Social Butterfly" }));
            Assert.That(current["playerPersona"]["scores"].All(item => (int)item["score"] == 0), Is.True);
            Assert.That((JArray)current["playerPersona"]["history"], Is.Empty);
            Assert.That((JArray)current["jurySentiment"]["jurors"], Is.Empty);
            Assert.That((double)current["jurySentiment"]["overallSentiment"], Is.Zero);
            Assert.That(current["pendingDiary"].Type, Is.EqualTo(JTokenType.Null));
            foreach (var field in new[] { "loyaltyOaths", "oathOpportunities", "shownOathMilestones", "resolvedDiaryIds" })
                Assert.That((JArray)current[field], Is.Empty, field);
            foreach (var field in new[] { "lastDiaryRoomWeek", "phaseEventSocialBonus", "phaseEventCompBonus" })
                Assert.That((int)current[field], Is.Zero, field);
            Assert.That((uint)current["randomState"], Is.Zero);
            Assert.That(source.ToString(Formatting.None), Is.EqualTo(before));
            var readAgain = EpisodeSaveMigrations.PrepareCurrentPayload(current, out migrated);
            Assert.That(migrated, Is.False);
            Assert.That(JToken.DeepEquals(readAgain, current), Is.True);
            Assert.That(ReferenceEquals(readAgain, current), Is.False);
        }

        [Test]
        public void Store_V2LoadIsReadOnlyAndExplicitSaveRetainsOriginalBytesAsBackup()
        {
            using var fixture = new IsolatedStore();
            var source = V2Fixture();
            File.WriteAllText(fixture.Store.SavePath, PersistenceMigrationTests.Envelope(source), new UTF8Encoding(false));
            var originalBytes = File.ReadAllBytes(fixture.Store.SavePath);
            Assert.That(fixture.Store.TryLoad(out var loaded, out var message), Is.True, message);
            Assert.That(loaded.schemaVersion, Is.EqualTo(10));
            Assert.That(message, Does.Contain("Schema 2").And.Contain("schema 10 in memory"));
            Assert.That(loaded.randomState, Is.Zero);
            Assert.That(loaded.relationships[0].events[0].description, Is.EqualTo("Pinned directed history"));
            Assert.That(loaded.relationshipArcs[0].weeklyHistory[0].reason, Is.EqualTo("Pinned arc history"));
            Assert.That(loaded.loyaltyOaths, Is.Empty);
            Assert.That(File.ReadAllBytes(fixture.Store.SavePath), Is.EqualTo(originalBytes));
            Assert.That(Directory.GetFiles(fixture.DirectoryPath), Has.Length.EqualTo(1));
            fixture.Store.Save(loaded);
            Assert.That(File.ReadAllBytes(fixture.Store.BackupPath), Is.EqualTo(originalBytes));
            Assert.That((int)JObject.Parse(File.ReadAllText(fixture.Store.SavePath))["state"]["schemaVersion"], Is.EqualTo(10));
            Assert.That(fixture.Store.TryLoad(out _, out message), Is.True, message);
            Assert.That(message, Does.Not.Contain("migrated"));
        }

        [Test]
        public void Store_V2RecoveryRetainsBothRawV2CopiesAndArchivesBadPrimary()
        {
            using var fixture = new IsolatedStore();
            File.WriteAllText(fixture.Store.BackupPath, PersistenceMigrationTests.Envelope(V2Fixture()), new UTF8Encoding(false));
            File.WriteAllText(fixture.Store.SavePath, "broken primary");
            var original = File.ReadAllBytes(fixture.Store.BackupPath);
            Assert.That(fixture.Store.TryRecoverBackup(out var state, out var message), Is.True, message);
            Assert.That(state.schemaVersion, Is.EqualTo(10));
            Assert.That(File.ReadAllBytes(fixture.Store.SavePath), Is.EqualTo(original));
            Assert.That(File.ReadAllBytes(fixture.Store.BackupPath), Is.EqualTo(original));
            Assert.That(File.ReadAllText(Directory.GetFiles(fixture.DirectoryPath, "*.before-recovery-*.json").Single()), Is.EqualTo("broken primary"));
            Assert.That(message, Does.Contain("Schema 2").And.Contain("schema 10 in memory"));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Store_OriginalV2ChecksumIsCheckedBeforeMigration(bool forgedMigratedChecksum)
        {
            using var fixture = new IsolatedStore();
            var source = V2Fixture(); var envelope = JObject.Parse(PersistenceMigrationTests.Envelope(source));
            if (forgedMigratedChecksum)
            {
                var migrated = EpisodeSaveMigrations.PrepareCurrentPayload(source, out _);
                envelope["checksum"] = JObject.Parse(PersistenceMigrationTests.Envelope(migrated))["checksum"].DeepClone();
            }
            else envelope["state"]["relationships"][0]["score"] = 99;
            var bytes = Encoding.UTF8.GetBytes(envelope.ToString(Formatting.Indented));
            File.WriteAllBytes(fixture.Store.SavePath, bytes);
            Assert.That(fixture.Store.TryLoad(out var loaded, out var message), Is.False);
            Assert.That(loaded, Is.Null);
            Assert.That(message, Does.Contain("checksum"));
            Assert.That(File.ReadAllBytes(fixture.Store.SavePath), Is.EqualTo(bytes));
        }

        [TestCase("v3-field-disguised-as-v2")]
        [TestCase("truncated-v3")]
        [TestCase("future-version")]
        public void Store_ValidChecksumCannotSmuggleNewFieldsAcrossVersions(string problem)
        {
            using var fixture = new IsolatedStore(); var source = V2Fixture();
            if (problem == "v3-field-disguised-as-v2") source["loyaltyOaths"] = new JArray();
            // Eight, not seven: seven is the current schema now, and "a version from the future"
            // has to actually be one.
            else source["schemaVersion"] = problem == "truncated-v3" ? 3 : 10;   // 10: still unsupported.
            File.WriteAllText(fixture.Store.SavePath, PersistenceMigrationTests.Envelope(source));
            var before = File.ReadAllBytes(fixture.Store.SavePath);
            Assert.That(fixture.Store.TryLoad(out var loaded, out _), Is.False);
            Assert.That(loaded, Is.Null);
            Assert.That(File.ReadAllBytes(fixture.Store.SavePath), Is.EqualTo(before));
        }

        [Test]
        public void Store_NonemptyV3RoundTripPreservesOrderedPersonaPendingDiaryAndOathWithoutAppendingDefaults()
        {
            using var fixture = new IsolatedStore();
            var original = V3State();
            fixture.Store.Save(original);
            Assert.That(fixture.Store.TryLoad(out var loaded, out var message), Is.True, message);
            Assert.That(loaded.playerPersona.scores.Count, Is.EqualTo(5), "Saved scores replace constructor defaults; they must not append.");
            Assert.That(loaded.playerPersona.scores.Select(x => x.persona), Is.EqualTo(original.playerPersona.scores.Select(x => x.persona)));
            Assert.That(loaded.playerPersona.current, Is.EqualTo("Remorseful"));
            Assert.That(loaded.playerPersona.history.Count, Is.EqualTo(2));
            Assert.That(loaded.pendingDiary.id, Is.EqualTo("diary-post_eviction-3"));
            Assert.That(loaded.pendingDiary.evictedId, Is.EqualTo("riley-johnson"));
            Assert.That(loaded.resolvedDiaryIds, Is.EqualTo(original.resolvedDiaryIds));
            Assert.That(loaded.lastDiaryRoomWeek, Is.EqualTo(2));
            Assert.That(loaded.phaseEventSocialBonus, Is.EqualTo(2));
            Assert.That(loaded.phaseEventCompBonus, Is.EqualTo(1));
            Assert.That(loaded.jurySentiment.jurors[0].events.Count, Is.EqualTo(2));
            Assert.That(loaded.jurySentiment.overallSentiment, Is.EqualTo(original.jurySentiment.overallSentiment));
            Assert.That(loaded.loyaltyOaths.Single().timestamp, Is.EqualTo(2L));
            Assert.That(loaded.oathOpportunities.Single(), Is.EqualTo("taylor-kim"));
            Assert.That(loaded.shownOathMilestones, Is.EqualTo(original.shownOathMilestones));
            fixture.Store.Save(loaded);
            Assert.That(fixture.Store.TryLoad(out var twice, out message), Is.True, message);
            Assert.That(twice.playerPersona.scores.Count, Is.EqualTo(5));
            Assert.That(twice.playerPersona.history.Count, Is.EqualTo(2));
            loaded.playerPersona.history[0].persona = "Ruthless";
            loaded.loyaltyOaths[0].timestamp = 999;
            loaded.pendingDiary.id = "changed in memory";
            Assert.That(twice.playerPersona.history[0].persona, Is.EqualTo("Remorseful"));
            Assert.That(twice.loyaltyOaths[0].timestamp, Is.EqualTo(2L));
            Assert.That(twice.pendingDiary.id, Is.EqualTo("diary-post_eviction-3"));
        }

        [TestCase("playerPersona.scores[0].score", "999")]
        [TestCase("playerPersona.current", "'Ruthless'")]
        [TestCase("playerPersona.history[0].week", "100")]
        [TestCase("jurySentiment.jurors[0].jurorId", "'unknown-juror'")]
        [TestCase("jurySentiment.overallSentiment", "99")]
        [TestCase("pendingDiary", "'not a record'")]
        [TestCase("pendingDiary.week", "4")]
        [TestCase("loyaltyOaths[0].timestamp", "2.5")]
        [TestCase("loyaltyOaths[0].timestamp", "'2'")]
        [TestCase("loyaltyOaths[0].timestamp", "9223372036854775808")]
        [TestCase("shownOathMilestones", "[]")]
        [TestCase("oathOpportunities", "['taylor-kim','taylor-kim']")]
        public void Store_MalformedV3DataWithValidChecksumRejectsWithoutRewriting(string path, string value)
        {
            using var fixture = new IsolatedStore();
            fixture.Store.Save(V3State());
            var payload = (JObject)JObject.Parse(File.ReadAllText(fixture.Store.SavePath))["state"];
            payload.SelectToken(path).Replace(JToken.Parse(value));
            File.WriteAllText(fixture.Store.SavePath, PersistenceMigrationTests.Envelope(payload));
            var bytes = File.ReadAllBytes(fixture.Store.SavePath);
            Assert.That(fixture.Store.TryLoad(out var state, out _), Is.False);
            Assert.That(state, Is.Null);
            Assert.That(File.ReadAllBytes(fixture.Store.SavePath), Is.EqualTo(bytes));
        }

        private static EpisodeState V3State()
        {
            var state = ContentCatalog.Create(17); state.week = 3; state.nextSequence = 10;
            state.Find("riley-johnson").status = ContestantStatus.Jury;
            state.playerPersona.current = "Remorseful";
            state.playerPersona.scores.Single(x => x.persona == "Remorseful").score = 2;
            state.playerPersona.history.Add(new WebPersonaHistory { persona = "Remorseful", week = 1 });
            state.playerPersona.history.Add(new WebPersonaHistory { persona = "Remorseful", week = 2 });
            state.lastDiaryRoomWeek = 2; state.phaseEventSocialBonus = 2; state.phaseEventCompBonus = 1;
            state.resolvedDiaryIds.AddRange(new[] { "diary-post_eviction-1", "diary-post_eviction-2" });
            state.pendingDiary = new DiaryPromptState
            { id = "diary-post_eviction-3", trigger = "post_eviction", evictedId = "riley-johnson", week = 3, isNominee = false };
            state.jurySentiment = WebJurySentiment.AddJuror(state.jurySentiment, "riley-johnson", "Riley Johnson", 20);
            state.jurySentiment = WebJurySentiment.ShiftAllJurorSentiment(state.jurySentiment, 5, "Fixture reflection", 3);
            state.loyaltyOaths.Add(new WebOathRecord { playerId = state.playerId, targetId = ContentCatalog.MayaId, week = 1, timestamp = 2 });
            state.shownOathMilestones.AddRange(new[] { ContentCatalog.MayaId, "taylor-kim" });
            state.oathOpportunities.Add("taylor-kim");
            return state;
        }

        // Frozen literal native v2 fixture, built without the current simulation
        // constructors or either production migration step. The common v1 part
        // itself is a pinned literal; all v2 nested additions are explicit here.
        internal static JObject V2Fixture()
        {
            var state = PersistenceMigrationTests.V1Fixture(); state["schemaVersion"] = 2;
            state["juryExchanges"] = new JArray(); state["juryQuestionIndex"] = 0;
            state["finalSpeeches"] = new JArray(); state["relationshipArcs"] = new JArray();
            foreach (JObject actor in (JArray)state["contestants"])
            { actor["mood"] = "Neutral"; actor["stressLevel"] = "Normal"; }
            foreach (JObject edge in (JArray)state["relationships"])
            { edge["notes"] = new JArray(); edge["events"] = new JArray(); edge["lastInteractionWeek"] = 0; }
            var first = state["relationships"][0];
            first["lastInteractionWeek"] = 1;
            ((JArray)first["notes"]).Add("Pinned note");
            ((JArray)first["events"]).Add(JObject.Parse("{'sequence':1,'week':1,'type':'general','description':'Pinned directed history','impactScore':12.5,'decayable':true}"));
            ((JArray)state["relationshipArcs"]).Add(JObject.Parse(@"{
                'npcId':'fixture-1','npcName':'Fixture 1','arcType':'friendship','intensity':15,'escalationLevel':0,
                'weeklyHistory':[{'week':1,'delta':15,'reason':'Pinned arc history'}] }"));
            return state;
        }

        private static JObject RichShapeFixture()
        {
            // All nested shapes populated for strict schema tests. Gameplay
            // legality is deliberately separate and checked by EpisodeValidation.
            var state = V2Fixture();
            ((JArray)state["votes"]).Add(JObject.Parse("{'voterId':'fixture-2','targetId':'fixture-3','reason':'Test ballot'}"));
            ((JArray)state["competitionScores"]).Add(JObject.Parse("{'contestantId':'fixture-0','score':51.25}"));
            ((JArray)state["juryExchanges"]).Add(JObject.Parse(@"{
                'questionerId':'fixture-2','finalistId':'fixture-0','tone':'loyal','question':'Frozen question',
                'optionA':'A text','optionB':'B text','correctChoice':'A','answerChoice':'A','answer':'A text',
                'opponentAnswer':'Opponent text','completed':true }"));
            ((JArray)state["finalSpeeches"]).Add(JObject.Parse("{'speakerId':'fixture-0','text':'Frozen speech','isPlayerAuthored':true}"));
            return state;
        }

        private sealed class IsolatedStore : IDisposable
        {
            public readonly string DirectoryPath;
            public readonly EpisodeSaveStore Store;
            public IsolatedStore()
            {
                DirectoryPath = Path.Combine(Path.GetTempPath(), "GamesimV3MigrationTests-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(DirectoryPath); Store = new EpisodeSaveStore(Path.Combine(DirectoryPath, "episode.json"));
            }
            public void Dispose()
            {
                var resolved = Path.GetFullPath(DirectoryPath);
                var root = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                Assert.That(resolved.StartsWith(root, StringComparison.OrdinalIgnoreCase), Is.True);
                Assert.That(Path.GetFileName(resolved), Does.StartWith("GamesimV3MigrationTests-"));
                if (Directory.Exists(resolved)) Directory.Delete(resolved, true);
            }
        }
    }
}
