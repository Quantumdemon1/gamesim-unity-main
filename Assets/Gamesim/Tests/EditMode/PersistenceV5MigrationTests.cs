using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Gamesim.Persistence;
using Gamesim.Simulation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    public sealed class PersistenceV5MigrationTests
    {
        [Test]
        public void FreshGamesStartInWeekOneAndThePrimitiveIsDetachedInEverySnapshot()
        {
            var input = ContentCatalog.Create(501);
            Assert.That(input.schemaVersion, Is.EqualTo(10));
            Assert.That(input.blocRulesStartWeek, Is.EqualTo(1));
            var engine = new EpisodeEngine(input);
            var copy = engine.Snapshot; copy.blocRulesStartWeek = 2; input.blocRulesStartWeek = 2;
            Assert.That(engine.Snapshot.blocRulesStartWeek, Is.EqualTo(1));
            using var files = new StoreFixture();
            files.Store.Save(engine.Snapshot);
            Assert.That(files.Store.TryLoad(out var loaded, out string message), Is.True, message);
            Assert.That(loaded.blocRulesStartWeek, Is.EqualTo(1));
        }

        [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)]
        public void EveryHistoricalVersionDefersToItsCapturedNextWeekWithoutChangingOlderData(int version)
        {
            var original = Historical(version); original["week"] = 7; original["randomState"] = 0;
            string before = original.ToString(Formatting.None);
            var v4 = version < 4 ? EpisodeSaveMigrations.PrepareV4Payload(original, out _) : original;
            var migrated = EpisodeSaveMigrations.PrepareCurrentPayload(original, out bool changed);
            Assert.That(changed, Is.True);
            Assert.That((int)migrated["schemaVersion"], Is.EqualTo(10));
            Assert.That((int)migrated["blocRulesStartWeek"], Is.EqualTo(8));
            Assert.That((uint)migrated["randomState"], Is.Zero);
            // Without schema 7's card copy: see the note on the v4 twin.
            AssertOldFieldsEqual(v4, PersistenceMigrationTests.StripCardCopy(
                PersistenceMigrationTests.StripSchema8(PersistenceMigrationTests.StripSchema9(PersistenceMigrationTests.StripSchema10((JObject)migrated.DeepClone())))));
            Assert.That(original.ToString(Formatting.None), Is.EqualTo(before));
            var second = EpisodeSaveMigrations.PrepareCurrentPayload(migrated, out changed);
            Assert.That(changed, Is.False);
            Assert.That(JToken.DeepEquals(migrated, second), Is.True);
            Assert.That(ReferenceEquals(migrated, second), Is.False);
            using var files = new StoreFixture();
            File.WriteAllText(files.Store.SavePath, PersistenceMigrationTests.Envelope(original));
            byte[] bytes = File.ReadAllBytes(files.Store.SavePath);
            Assert.That(files.Store.TryLoad(out var loaded, out string message), Is.True, message);
            Assert.That(loaded.blocRulesStartWeek, Is.EqualTo(8));
            Assert.That(message, Does.StartWith("Local episode loaded and validated.").And.Contain("Voting-bloc rules begin in week 8; the current week is unchanged."));
            Assert.That(File.ReadAllBytes(files.Store.SavePath), Is.EqualTo(bytes));
        }

        [Test]
        public void AllSixteenPhaseHistoriesIncludingPartialAndRevealedBallotsArePreserved()
        {
            var engine = new EpisodeEngine(ContentCatalog.Create(502));
            var phases = new HashSet<EpisodePhase>();
            bool partial = false, revealed = false, terminal = false;
            for (int guard = 0; guard < 280; guard++)
            {
                var state = engine.Snapshot; phases.Add(state.phase);
                partial |= state.phase == EpisodePhase.Eviction && state.votes.Count > 0 && !state.evictionResolved;
                revealed |= state.phase == EpisodePhase.Eviction && state.evictionResolved;
                terminal |= state.phase == EpisodePhase.Finished;
                var old = CaptureV4(state); string before = old.ToString(Formatting.None);
                var migrated = EpisodeSaveMigrations.UpgradeV4ToV5(old);
                Assert.That((int)migrated["blocRulesStartWeek"], Is.EqualTo(state.week + 1), state.phase.ToString());
                AssertOldFieldsEqual(old, migrated);
                Assert.That(old.ToString(Formatting.None), Is.EqualTo(before));
                if (terminal) break;
                var result = engine.Apply(EpisodeEngineTests.NextCommand(state));
                Assert.That(result.accepted, Is.True, result.reason);
            }
            Assert.That(phases.Count, Is.EqualTo(16));
            Assert.That(partial, Is.True, "Include an already committed pending player ballot.");
            Assert.That(revealed && terminal, Is.True);
        }

        [Test]
        public void MigrationMarkerSurvivesTheCompleteOldWeekReloadAndDuplicateReceipt()
        {
            using var files = new StoreFixture();
            var start = CaptureV4(ContentCatalog.Create(503));
            File.WriteAllText(files.Store.SavePath, PersistenceMigrationTests.Envelope(start));
            byte[] original = File.ReadAllBytes(files.Store.SavePath);
            Assert.That(files.Store.TryLoad(out var loaded, out string message), Is.True, message);
            var engine = new EpisodeEngine(loaded);
            Assert.That(loaded.blocRulesStartWeek, Is.EqualTo(2));
            EpisodeCommand committed = null;
            for (int guard = 0; guard < 80 && engine.Snapshot.week == 1; guard++)
            {
                var result = engine.Apply(committed = EpisodeEngineTests.NextCommand(engine.Snapshot));
                Assert.That(result.accepted, Is.True, result.reason);
                Assert.That(result.state.blocRulesStartWeek, Is.EqualTo(2));
            }
            Assert.That(engine.Snapshot.week, Is.EqualTo(2));
            files.Store.Save(engine.Snapshot);
            Assert.That(File.ReadAllBytes(files.Store.BackupPath), Is.EqualTo(original));
            Assert.That(files.Store.TryLoad(out loaded, out message), Is.True, message);
            engine = new EpisodeEngine(loaded);
            string before = JsonConvert.SerializeObject(engine.Snapshot);
            Assert.That(engine.Apply(committed).duplicate, Is.True);
            Assert.That(JsonConvert.SerializeObject(engine.Snapshot), Is.EqualTo(before));
            Assert.That(engine.Snapshot.blocRulesStartWeek, Is.EqualTo(2));
            Assert.That(files.Store.TryRecoverBackup(out loaded, out message), Is.True, message);
            Assert.That(loaded.blocRulesStartWeek, Is.EqualTo(2));
            Assert.That(loaded.week, Is.EqualTo(1));
            Assert.That(File.ReadAllBytes(files.Store.SavePath), Is.EqualTo(original));
        }

        [TestCase("0")] [TestCase("-1")] [TestCase("3")] [TestCase("102")]
        [TestCase("2147483648")] [TestCase("1.5")] [TestCase("'1'")]
        [TestCase("null")] [TestCase("true")]
        public void ChecksumValidMalformedCurrentMarkersRejectWithoutMutation(string value)
        {
            using var files = new StoreFixture();
            var current = CaptureCurrent(ContentCatalog.Create(504));
            File.WriteAllText(files.Store.SavePath, PersistenceMigrationTests.Envelope(current));
            Assert.That(files.Store.TryLoad(out _, out string baselineMessage), Is.True, baselineMessage);
            current["blocRulesStartWeek"] = JToken.Parse(value);
            File.WriteAllText(files.Store.SavePath, PersistenceMigrationTests.Envelope(current));
            var before = File.ReadAllBytes(files.Store.SavePath);
            Assert.That(files.Store.TryLoad(out var loaded, out _), Is.False);
            Assert.That(loaded, Is.Null);
            Assert.That(File.ReadAllBytes(files.Store.SavePath), Is.EqualTo(before));
        }

        [TestCase(1)] [TestCase(100)]
        public void HistoricalWeekBoundariesUseCheckedNextWeekIncludingTheTerminalUpperBoundary(int week)
        {
            var old = Historical(4); old["week"] = week;
            var migrated = EpisodeSaveMigrations.UpgradeV4ToV5(old);
            Assert.That((int)migrated["blocRulesStartWeek"], Is.EqualTo(week + 1));
            var current = EpisodeSaveMigrations.PrepareCurrentPayload(migrated, out _);
            var candidate = current.ToObject<EpisodeState>(JsonSerializer.Create(new JsonSerializerSettings { ObjectCreationHandling = ObjectCreationHandling.Replace }));
            Assert.That(EpisodeValidation.TryValidate(candidate, out string message), Is.True, message);
        }

        [TestCase("week", "0")] [TestCase("week", "101")] [TestCase("week", "2147483647")]
        [TestCase("revision", "1000001")] [TestCase("nextSequence", "1000001")]
        [TestCase("socialActions", "19")] [TestCase("playerStudyBonus", "6")]
        [TestCase("phaseEventCompBonus", "1001")] [TestCase("phaseEventSocialBonus", "1001")]
        [TestCase("contestants[0].stats.luck", "10.01")] [TestCase("contestants[0].hohWins", "101")]
        [TestCase("relationships[0].score", "100.01")]
        public void FrozenHistoricalScalarRangesRejectBeforeTheRuntimeValidationView(string path, string value)
        {
            var old = Historical(4); old.SelectToken(path).Replace(JToken.Parse(value));
            string before = old.ToString(Formatting.None);
            var error = Assert.Throws<InvalidDataException>(() => EpisodeSaveMigrations.UpgradeV4ToV5(old));
            Assert.That(error.Message, Does.Contain("Historical v4"));
            Assert.That(old.ToString(Formatting.None), Is.EqualTo(before));
        }

        [TestCase(1030, true)] [TestCase(1031, false)]
        public void FrozenV4ScoresKeepTheAcceptedStudyEraCeiling(int score, bool valid)
        {
            var old = Historical(4);
            old["competitionScores"] = new JArray(new JObject { ["contestantId"] = (string)old["contestants"][0]["id"], ["score"] = score });
            if (valid) Assert.That((double)EpisodeSaveMigrations.UpgradeV4ToV5(old)["competitionScores"][0]["score"], Is.EqualTo(score));
            else Assert.Throws<InvalidDataException>(() => EpisodeSaveMigrations.UpgradeV4ToV5(old));
        }

        [Test]
        public void FrozenV4RecursivelyMatchesAllPreBlocFieldsAndRejectsEveryNestedUnknownOrMissingField()
        {
            Type frozen = typeof(EpisodeSaveMigrations).Assembly.GetType("Gamesim.Persistence.FrozenEpisodeV4+State", true);
            CompareFields(typeof(EpisodeState), frozen, "state", new HashSet<string>());
            var factory = typeof(PersistenceV4MigrationTests).GetMethod("RichV3Fixture", BindingFlags.NonPublic | BindingFlags.Static);
            var rich = (JObject)factory.Invoke(null, null); rich["schemaVersion"] = 4; rich["playerStudyBonus"] = 0;
            var paths = rich.DescendantsAndSelf().OfType<JObject>().Select(item => item.Path).ToArray();
            Assert.That(paths.Length, Is.GreaterThan(25));
            foreach (string path in paths) foreach (bool missing in new[] { true, false })
            {
                var old = (JObject)rich.DeepClone();
                var record = path.Length == 0 ? old : (JObject)old.SelectToken(path);
                if (missing) record.Properties().Last().Remove(); else record["futureField"] = 1;
                Assert.Throws<InvalidDataException>(() => EpisodeSaveMigrations.UpgradeV4ToV5(old), path);
            }
        }

        [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)]
        public void HistoricalPayloadsCannotSmuggleTheNewMarkerEvenWithAValidChecksum(int version)
        {
            using var files = new StoreFixture(); var old = Historical(version); old["blocRulesStartWeek"] = 1;
            File.WriteAllText(files.Store.SavePath, PersistenceMigrationTests.Envelope(old));
            Assert.That(files.Store.TryLoad(out var loaded, out _), Is.False);
            Assert.That(loaded, Is.Null);
        }

        [Test]
        public void OriginalChecksumIsCheckedBeforeTheMigrationAndMalformedVersionsNeverDefault()
        {
            using var files = new StoreFixture();
            var envelope = JObject.Parse(PersistenceMigrationTests.Envelope(Historical(4)));
            envelope["state"]["week"] = 2;
            File.WriteAllText(files.Store.SavePath, envelope.ToString(Formatting.None));
            Assert.That(files.Store.TryLoad(out _, out string message), Is.False);
            Assert.That(message, Does.Contain("checksum").IgnoreCase);
            foreach (string value in new[] { "null", "4.5", "'4'", "10", "2147483648" })   // 8: still unsupported.
            {
                var old = Historical(4); old["schemaVersion"] = JToken.Parse(value);
                File.WriteAllText(files.Store.SavePath, PersistenceMigrationTests.Envelope(old));
                Assert.That(files.Store.TryLoad(out _, out _), Is.False, value);
            }
        }

        [TestCase(1)] [TestCase(7)] [TestCase(100)]
        public void RestrictedWebImportsDeferToCapturedNextWeekAndPreserveArchivedSource(int week)
        {
            using var files = new StoreFixture();
            var source = (JObject)typeof(PersistenceTests).GetMethod("WebFixture", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
            source["state"]["week"] = week; string json = source.ToString(Formatting.Indented);
            Assert.That(WebSaveImporter.TryImport(json, files.DirectoryPath, out var imported, out string message), Is.True, message);
            Assert.That(imported.blocRulesStartWeek, Is.EqualTo(week + 1));
            Assert.That(message, Does.Contain("Voting-bloc rules begin in week " + (week + 1) + "; the current week is unchanged."));
            Assert.That(imported.week, Is.EqualTo(week));
            Assert.That(imported.votes, Is.Empty); Assert.That(imported.acceptedCommandIds, Is.Empty);
            Assert.That(File.ReadAllText(Directory.GetFiles(files.DirectoryPath, "web-original-*.json").Single()), Is.EqualTo(json));
            files.Store.Save(imported);
            Assert.That(files.Store.TryLoad(out var loaded, out message), Is.True, message);
            Assert.That(loaded.blocRulesStartWeek, Is.EqualTo(week + 1));
            Assert.That(message, Does.StartWith("Local episode loaded and validated.").And.Contain("Voting-bloc rules begin in week " + (week + 1)));
            Assert.That(loaded.randomState, Is.EqualTo(imported.randomState));
        }

        [TestCase("101")] [TestCase("2147483647")] [TestCase("1.5")] [TestCase("'1'")]
        public void InvalidImportedWeeksCannotOverflowOrDefaultTheRuleBoundary(string week)
        {
            using var files = new StoreFixture();
            var source = (JObject)typeof(PersistenceTests).GetMethod("WebFixture", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
            source["state"]["week"] = JToken.Parse(week);
            Assert.That(WebSaveImporter.TryImport(source.ToString(), files.DirectoryPath, out var imported, out _), Is.False);
            Assert.That(imported, Is.Null);
        }

        private static JObject Historical(int version)
        {
            if (version == 1) return PersistenceMigrationTests.V1Fixture();
            var source = PersistenceV3MigrationTests.V2Fixture();
            if (version == 2) return source;
            source["schemaVersion"] = 3;
            source["playerPersona"] = JObject.Parse(@"{'current':'Neutral','scores':[
                {'persona':'Neutral','score':0},{'persona':'Remorseful','score':0},{'persona':'Ruthless','score':0},
                {'persona':'Calculated','score':0},{'persona':'Social Butterfly','score':0}],'history':[]}");
            source["jurySentiment"] = JObject.Parse("{'jurors':[],'overallSentiment':0}"); source["pendingDiary"] = null;
            source["lastDiaryRoomWeek"] = 0; source["phaseEventSocialBonus"] = 0; source["phaseEventCompBonus"] = 0;
            foreach (string field in new[] { "resolvedDiaryIds", "loyaltyOaths", "oathOpportunities", "shownOathMilestones" }) source[field] = new JArray();
            if (version == 4) { source["schemaVersion"] = 4; source["playerStudyBonus"] = 0; }
            return source;
        }
        private static JObject CaptureV4(EpisodeState state)
        {
            var source = PersistenceMigrationTests.StripCardCopy(PersistenceMigrationTests.StripSchema8(PersistenceMigrationTests.StripSchema9(PersistenceMigrationTests.StripSchema10(CaptureCurrent(state)))));
            source.Remove("npcSocial"); source.Remove("blocRulesStartWeek"); source["schemaVersion"] = 4; return source;
        }
        private static readonly PublicFieldContractResolver SharedCaptureResolver = new PublicFieldContractResolver();
        private static JObject CaptureCurrent(EpisodeState state) => JObject.FromObject(state,
            JsonSerializer.Create(new JsonSerializerSettings { ContractResolver = SharedCaptureResolver }));
        private sealed class PublicFieldContractResolver : DefaultContractResolver
        {
            protected override List<MemberInfo> GetSerializableMembers(Type objectType) => objectType.GetFields(BindingFlags.Instance | BindingFlags.Public).Cast<MemberInfo>().ToList();
        }
        private static void AssertOldFieldsEqual(JObject old, JObject migrated)
        {
            Assert.That(migrated.Properties().Select(item => item.Name).Except(old.Properties().Select(item => item.Name)), Is.EqualTo((int)migrated["schemaVersion"] == 5 ? new[] { "blocRulesStartWeek" } : new[] { "blocRulesStartWeek", "npcSocial" }));
            foreach (var property in old.Properties()) if (property.Name != "schemaVersion")
                Assert.That(JToken.DeepEquals(property.Value, migrated[property.Name]), Is.True, "Historical field changed: " + property.Name);
        }
        /// <summary>Whether this live field post-dates the frozen shape, and where it sits.</summary>
        private static bool AddedSinceV4(string path, string field)
        {
            switch (path)
            {
                case "state": return new[] { "blocRulesStartWeek", "npcSocial",
                    "evictionStage", "evictionSpeeches", "backdoorTargetId",
                    "outOfPhaseSocialActions", "openingBeatsSeen",
                    "socialBudgetRulesStartWeek",
                    "deals", "dealRulesStartWeek" }.Contains(field);
                case "state.contestants[]": return new[] { "occupation", "archetype", "age", "hometown", "bio" }.Contains(field);
                default: return false;
            }
        }

        private static void CompareFields(Type live, Type frozen, string path, HashSet<string> visited)
        {
            if (live.IsEnum) { Assert.That(frozen, Is.EqualTo(typeof(int)), path); return; }
            if (live.IsGenericType && live.GetGenericTypeDefinition() == typeof(List<>))
            { Assert.That(frozen.GetGenericTypeDefinition(), Is.EqualTo(typeof(List<>)), path); CompareFields(live.GetGenericArguments()[0], frozen.GetGenericArguments()[0], path + "[]", visited); return; }
            if (live.IsPrimitive || live == typeof(string)) { Assert.That(frozen, Is.EqualTo(live), path); return; }
            if (!visited.Add(live.FullName + ":" + frozen.FullName)) return;
            // See the note on the v4 twin: fields added after this frozen version, by location.
            var a = live.GetFields(BindingFlags.Public | BindingFlags.Instance)
                .Where(field => !AddedSinceV4(path, field.Name)).ToArray();
            var b = frozen.GetFields(BindingFlags.Public | BindingFlags.Instance);
            Assert.That(a.Select(field => field.Name), Is.EquivalentTo(b.Select(field => field.Name)), path);
            foreach (var field in a) CompareFields(field.FieldType, b.Single(other => other.Name == field.Name).FieldType, path + "." + field.Name, visited);
        }
        private sealed class StoreFixture : IDisposable
        {
            public readonly string DirectoryPath = Path.Combine(Path.GetTempPath(), "GamesimV5MigrationTests-" + Guid.NewGuid().ToString("N"));
            public readonly EpisodeSaveStore Store;
            public StoreFixture() { Directory.CreateDirectory(DirectoryPath); Store = new EpisodeSaveStore(Path.Combine(DirectoryPath, "episode.json")); }
            public void Dispose()
            {
                string path = Path.GetFullPath(DirectoryPath);
                Assert.That(Path.GetDirectoryName(path), Is.EqualTo(Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar)));
                Assert.That(Path.GetFileName(path), Does.StartWith("GamesimV5MigrationTests-"));
                if (Directory.Exists(path)) Directory.Delete(path, true);
            }
        }
    }
}
