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
    public sealed class PersistenceV4MigrationTests
    {
        [Test]
        public void FrozenV3RecursivelyMatchesEveryPreStudyPublicFieldIncludingNestedTypes()
        {
            var frozen = typeof(EpisodeSaveMigrations).Assembly.GetType("Gamesim.Persistence.FrozenEpisodeV3+State", true);
            CompareFields(typeof(EpisodeState), frozen, "state", new HashSet<string>());
        }

        [TestCase(0)]
        [TestCase(12)]
        [TestCase(13)]
        [TestCase(14)]
        [TestCase(15)]
        public void FrozenV3UpgradeAddsOnlyDefaultZeroWithoutChangingAnyHistoricalFields(int phase)
        {
            var original = RichV3Fixture(); original["phase"] = phase;
            string before = original.ToString(Formatting.None);
            var current = EpisodeSaveMigrations.UpgradeV3ToV4(original);
            Assert.That((int)current["schemaVersion"], Is.EqualTo(4));
            Assert.That((int)current["playerStudyBonus"], Is.Zero);
            Assert.That(current.Properties().Select(field => field.Name).Except(original.Properties().Select(field => field.Name)), Is.EqualTo(new[] { "playerStudyBonus" }));
            foreach (var field in original.Properties()) if (field.Name != "schemaVersion")
                Assert.That(JToken.DeepEquals(field.Value, current[field.Name]), Is.True, field.Name);
            current["playerPersona"]["scores"][0]["score"] = 99;
            current["jurySentiment"]["jurors"][0]["events"][0]["reason"] = "external";
            current["loyaltyOaths"][0]["timestamp"] = 999;
            Assert.That(original.ToString(Formatting.None), Is.EqualTo(before));
        }

        [Test]
        public void FrozenV3ContractRejectsMissingUnknownAndWrongNestedFieldTypesAtEveryRecord()
        {
            var rich = RichV3Fixture();
            var paths = rich.DescendantsAndSelf().OfType<JObject>().Select(record => record.Path).ToArray();
            Assert.That(paths.Length, Is.GreaterThan(25), "Include persona, juror events, oath and older nested shapes.");
            foreach (string path in paths) foreach (string damage in new[] { "missing", "unknown" })
            {
                var original = RichV3Fixture();
                var record = path.Length == 0 ? original : (JObject)original.SelectToken(path);
                if (damage == "unknown") record["futureField"] = 1;
                else record.Properties().Last().Remove();
                string before = original.ToString(Formatting.None);
                Assert.Throws<InvalidDataException>(() => EpisodeSaveMigrations.UpgradeV3ToV4(original), path + ":" + damage);
                Assert.That(original.ToString(Formatting.None), Is.EqualTo(before));
            }
        }

        [TestCase("phase", "16")]
        [TestCase("phase", "1.5")]
        [TestCase("events[0].phase", "16")]
        [TestCase("contestants[0].status", "5")]
        [TestCase("promises[0].kind", "5")]
        [TestCase("promises[0].status", "4")]
        [TestCase("playerPersona.scores[0].score", "'2'")]
        [TestCase("playerPersona.history[0].week", "1.5")]
        [TestCase("jurySentiment.jurors[0].events[0].delta", "'5'")]
        [TestCase("loyaltyOaths[0].timestamp", "2.5")]
        [TestCase("pendingDiary.isNominee", "'false'")]
        [TestCase("contestants", "null")]
        public void FrozenV3RejectsCoercibleScalarTypesAndNewEnumOrdinals(string path, string value)
        {
            var original = RichV3Fixture(); original.SelectToken(path).Replace(JToken.Parse(value));
            Assert.Throws<InvalidDataException>(() => EpisodeSaveMigrations.UpgradeV3ToV4(original));
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void CurrentDispatchTraversesFrozenContractsAndPreservesZeroGeneratorState(int version)
        {
            var source = version == 1 ? PersistenceMigrationTests.V1Fixture() : version == 2 ? PersistenceV3MigrationTests.V2Fixture() : V3Fixture();
            source["randomState"] = 0;
            string before = source.ToString(Formatting.None);
            var oldV3 = version < 3 ? EpisodeSaveMigrations.PrepareV3Payload(source, out _) : source;
            var migrated = EpisodeSaveMigrations.PrepareCurrentPayload(source, out bool changed);
            Assert.That(changed, Is.True);
            Assert.That((int)migrated["schemaVersion"], Is.EqualTo(11));
            Assert.That((int)migrated["playerStudyBonus"], Is.Zero);
            Assert.That((uint)migrated["randomState"], Is.Zero);
            // Compared without schema 7's card copy, which the last step adds to every contestant.
            // The claim is that nothing historical changed, not that nothing was added — what the
            // step adds is pinned separately in PersistenceV7MigrationTests.
            var historical = PersistenceMigrationTests.StripCardCopy(
                PersistenceMigrationTests.StripSchema8(PersistenceMigrationTests.StripSchema9(PersistenceMigrationTests.StripSchema10(PersistenceMigrationTests.StripSchema11((JObject)migrated.DeepClone())))));
            foreach (var field in oldV3.Properties()) if (field.Name != "schemaVersion")
                Assert.That(JToken.DeepEquals(field.Value, historical[field.Name]), Is.True, field.Name);
            Assert.That(source.ToString(Formatting.None), Is.EqualTo(before));
            var second = EpisodeSaveMigrations.PrepareCurrentPayload(migrated, out changed);
            Assert.That(changed, Is.False);
            Assert.That(JToken.DeepEquals(second, migrated), Is.True);
            Assert.That(ReferenceEquals(second, migrated), Is.False);
        }

        [Test]
        public void V3LoadAndRecoveryStayReadOnlyUntilExplicitSchema6Save()
        {
            using var files = new StoreFixture();
            string source = PersistenceMigrationTests.Envelope(V3Fixture());
            File.WriteAllText(files.Store.SavePath, source);
            byte[] before = File.ReadAllBytes(files.Store.SavePath);
            Assert.That(files.Store.TryLoad(out var loaded, out string message), Is.True, message);
            Assert.That(message, Does.Contain("Schema 3").And.Contain("schema 11 in memory"));
            Assert.That(loaded.schemaVersion, Is.EqualTo(11));
            Assert.That(loaded.playerStudyBonus, Is.Zero);
            Assert.That(loaded.playerPersona.scores.Count, Is.EqualTo(5));
            Assert.That(File.ReadAllBytes(files.Store.SavePath), Is.EqualTo(before));
            Assert.That(Directory.GetFiles(files.DirectoryPath).Length, Is.EqualTo(1));
            files.Store.Save(loaded);
            Assert.That(File.ReadAllBytes(files.Store.BackupPath), Is.EqualTo(before));
            Assert.That((int)JObject.Parse(File.ReadAllText(files.Store.SavePath))["state"]["schemaVersion"], Is.EqualTo(11));
            Assert.That(files.Store.TryRecoverBackup(out loaded, out message), Is.True, message);
            Assert.That(loaded.playerStudyBonus, Is.Zero);
            Assert.That(File.ReadAllBytes(files.Store.SavePath), Is.EqualTo(before));
            Assert.That(File.ReadAllBytes(files.Store.BackupPath), Is.EqualTo(before));
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void ChecksumValidHistoricalScoresKeepTheirOriginalCeilingDuringMigration(int version)
        {
            using var files = new StoreFixture();
            var payload = version == 1 ? PersistenceMigrationTests.V1Fixture() : version == 2 ? PersistenceV3MigrationTests.V2Fixture() : V3Fixture();
            payload["competitionScores"] = new JArray(new JObject
            {
                ["contestantId"] = (string)payload["contestants"][0]["id"], ["score"] = 1000
            });
            File.WriteAllText(files.Store.SavePath, PersistenceMigrationTests.Envelope(payload));
            byte[] validBefore = File.ReadAllBytes(files.Store.SavePath);
            Assert.That(files.Store.TryLoad(out var loaded, out string message), Is.True, message);
            Assert.That(loaded.competitionScores.Single().score, Is.EqualTo(1000));
            Assert.That(File.ReadAllBytes(files.Store.SavePath), Is.EqualTo(validBefore));

            payload["competitionScores"][0]["score"] = 1001;
            // Recompute a valid envelope checksum: rejection must be historical validation, not tamper detection.
            File.WriteAllText(files.Store.SavePath, PersistenceMigrationTests.Envelope(payload));
            byte[] invalidBefore = File.ReadAllBytes(files.Store.SavePath);
            Assert.That(files.Store.TryLoad(out loaded, out message), Is.False, "Historical schema " + version);
            Assert.That(message, Does.Contain("historical 0..1000"));
            Assert.That(loaded, Is.Null);
            Assert.That(File.ReadAllBytes(files.Store.SavePath), Is.EqualTo(invalidBefore));
            Assert.Throws<InvalidDataException>(() => EpisodeSaveMigrations.PrepareCurrentPayload(payload, out _));
        }

        [TestCase("checksum")]
        [TestCase("smuggled-study")]
        [TestCase("truncated-v4")]
        [TestCase("future-schema")]
        [TestCase("fractional-study")]
        [TestCase("negative-study")]
        [TestCase("overcap-study")]
        [TestCase("text-study")]
        public void MalformedVersionsAndStudyValuesRejectWithoutRewritingFiles(string damage)
        {
            using var files = new StoreFixture();
            var payload = V3Fixture();
            if (damage == "smuggled-study") payload["playerStudyBonus"] = 5;
            else if (damage == "truncated-v4") payload["schemaVersion"] = 4;
            else if (damage == "future-schema") payload["schemaVersion"] = 12;  // Eleven is current.
            else if (damage != "checksum")
            {
                payload = EpisodeSaveMigrations.UpgradeV3ToV4(payload);
                payload["playerStudyBonus"] = damage == "fractional-study" ? new JValue(1.5) : damage == "negative-study" ? new JValue(-1) :
                    damage == "overcap-study" ? new JValue(6) : new JValue("1");
            }
            var envelope = JObject.Parse(PersistenceMigrationTests.Envelope(payload));
            if (damage == "checksum") envelope["state"]["randomState"] = 999;
            File.WriteAllText(files.Store.SavePath, envelope.ToString(Formatting.Indented));
            byte[] before = File.ReadAllBytes(files.Store.SavePath);
            Assert.That(files.Store.TryLoad(out var loaded, out _), Is.False, damage);
            Assert.That(loaded, Is.Null);
            Assert.That(File.ReadAllBytes(files.Store.SavePath), Is.EqualTo(before));
        }

        /// <summary>Whether this live field post-dates the frozen shape, and where it sits.</summary>
        private static bool AddedSinceV3(string path, string field)
        {
            switch (path)
            {
                case "state": return new[] { "playerStudyBonus", "blocRulesStartWeek", "npcSocial",
                    "evictionStage", "evictionSpeeches", "backdoorTargetId",
                    "outOfPhaseSocialActions", "openingBeatsSeen",
                    "socialBudgetRulesStartWeek",
                    "deals", "dealRulesStartWeek",
                    "boughtActionPoints", "houseEvents", "eventRulesStartWeek" }.Contains(field);
                case "state.contestants[]": return new[] { "occupation", "archetype", "age", "hometown", "bio" }.Contains(field);
                default: return false;
            }
        }

        private static void CompareFields(Type live, Type frozen, string path, HashSet<string> visited)
        {
            if (live.IsEnum) { Assert.That(frozen, Is.EqualTo(typeof(int)), path); return; }
            if (live.IsGenericType && live.GetGenericTypeDefinition() == typeof(List<>))
            {
                Assert.That(frozen.GetGenericTypeDefinition(), Is.EqualTo(typeof(List<>)), path);
                CompareFields(live.GetGenericArguments()[0], frozen.GetGenericArguments()[0], path + "[]", visited); return;
            }
            if (live.IsPrimitive || live == typeof(string)) { Assert.That(frozen, Is.EqualTo(live), path); return; }
            if (!visited.Add(live.FullName + ":" + frozen.FullName)) return;
            // Fields added after this frozen version, keyed by where they live. The list used to
            // be a root-level check, which quietly stopped covering anything the moment a field was
            // added to a nested row instead — schema 7 added three to the contestant.
            var a = live.GetFields(BindingFlags.Public | BindingFlags.Instance)
                .Where(field => !AddedSinceV3(path, field.Name)).ToArray();
            var b = frozen.GetFields(BindingFlags.Public | BindingFlags.Instance);
            Assert.That(a.Select(field => field.Name), Is.EquivalentTo(b.Select(field => field.Name)), path);
            foreach (var field in a) CompareFields(field.FieldType, b.Single(other => other.Name == field.Name).FieldType, path + "." + field.Name, visited);
        }

        // Independent literal schema3 additions. Neither runtime defaults nor production migration builds this fixture.
        private static JObject V3Fixture()
        {
            var source = PersistenceV3MigrationTests.V2Fixture(); source["schemaVersion"] = 3;
            source["playerPersona"] = JObject.Parse(@"{'current':'Neutral','scores':[
                {'persona':'Neutral','score':0},{'persona':'Remorseful','score':0},{'persona':'Ruthless','score':0},
                {'persona':'Calculated','score':0},{'persona':'Social Butterfly','score':0}],'history':[]}");
            source["jurySentiment"] = JObject.Parse("{'jurors':[],'overallSentiment':0}");
            source["pendingDiary"] = null;
            source["lastDiaryRoomWeek"] = 0; source["phaseEventSocialBonus"] = 0; source["phaseEventCompBonus"] = 0;
            foreach (string field in new[] { "resolvedDiaryIds", "loyaltyOaths", "oathOpportunities", "shownOathMilestones" }) source[field] = new JArray();
            return source;
        }

        private static JObject RichV3Fixture()
        {
            var s = V3Fixture();
            // Shape-only records exercise frozen contracts; semantic legality is tested separately through Store.
            ((JArray)s["playerPersona"]["history"]).Add(JObject.Parse("{'persona':'Remorseful','week':1}"));
            ((JArray)s["jurySentiment"]["jurors"]).Add(JObject.Parse("{'jurorId':'fixture-1','jurorName':'Juror','sentiment':10,'events':[{'week':1,'delta':5,'reason':'literal'}]}"));
            s["pendingDiary"] = JObject.Parse("{'id':'diary-post_eviction-1','trigger':'post_eviction','evictedId':'fixture-1','week':1,'isNominee':false}");
            ((JArray)s["loyaltyOaths"]).Add(JObject.Parse("{'playerId':'fixture-0','targetId':'fixture-1','week':1,'timestamp':1}"));
            s["promises"] = JArray.Parse("[{'id':'p','fromId':'fixture-0','toId':'fixture-1','targetId':null,'kind':0,'status':0,'week':1,'expiresWeek':2,'impact':null}]");
            s["alliances"] = JArray.Parse("[{'id':'a','name':'Alliance','members':['fixture-0','fixture-1'],'active':true}]");
            s["memories"] = JArray.Parse("[{'ownerId':'fixture-0','subjectId':'fixture-1','text':'Memory','week':1,'isPrivate':true}]");
            s["votes"] = JArray.Parse("[{'voterId':'fixture-0','targetId':'fixture-1','reason':'Reason'}]");
            s["competitionScores"] = JArray.Parse("[{'contestantId':'fixture-0','score':5}]");
            s["juryExchanges"] = JArray.Parse("[{'questionerId':'fixture-1','finalistId':'fixture-0','tone':'respectful','question':'Q','optionA':'A','optionB':'B','correctChoice':'A','answerChoice':'A','answer':'A','opponentAnswer':null,'completed':true}]");
            s["finalSpeeches"] = JArray.Parse("[{'speakerId':'fixture-0','text':'Speech','isPlayerAuthored':true}]");
            return s;
        }

        private sealed class StoreFixture : IDisposable
        {
            public readonly string DirectoryPath = Path.Combine(Path.GetTempPath(), "GamesimV4MigrationTests-" + Guid.NewGuid().ToString("N"));
            public readonly EpisodeSaveStore Store;
            public StoreFixture() { Directory.CreateDirectory(DirectoryPath); Store = new EpisodeSaveStore(Path.Combine(DirectoryPath, "episode.json")); }
            public void Dispose()
            {
                string path = Path.GetFullPath(DirectoryPath);
                Assert.That(Path.GetDirectoryName(path), Is.EqualTo(Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar)));
                Assert.That(Path.GetFileName(path), Does.StartWith("GamesimV4MigrationTests-"));
                if (Directory.Exists(path)) Directory.Delete(path, true);
            }
        }
    }
}
