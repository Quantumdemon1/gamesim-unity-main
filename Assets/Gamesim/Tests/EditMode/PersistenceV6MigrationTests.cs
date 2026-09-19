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
    public sealed class PersistenceV6MigrationTests
    {
        [TestCase(0u)] [TestCase(1u)] [TestCase(uint.MaxValue)]
        public void FreshFactoryUsesFixedIndependentSeedWithoutDrawingTheMainStream(uint seed)
        {
            var state = ContentCatalog.Create(seed);
            // A fresh season is written at whatever the current schema is, not at this file's step.
            Assert.That(state.schemaVersion, Is.EqualTo(11));
            Assert.That(state.npcSocial.rulesStartWeek, Is.EqualTo(1));
            Assert.That(state.npcSocial.randomState, Is.EqualTo(SeededRandom.HashSeed("gamesim:npc-social:v1:" + seed.ToString("x8", System.Globalization.CultureInfo.InvariantCulture))));
            Assert.That(state.randomState, Is.EqualTo(seed == 0 ? 0x6D2B79F5u : seed));
            Assert.That(state.npcSocial.clockTick, Is.Zero); Assert.That(state.npcSocial.nextScanTick, Is.EqualTo(3));
            Assert.That(state.npcSocial.nextConversationSequence, Is.EqualTo(1));
            Assert.That(NpcSocialState.IsEligible(state), Is.True);
        }

        [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
        public void EveryHistoricalVersionPreservesTheCompleteV5StateAndStartsOnlyNextWeek(int version)
        {
            var old = Historical(version); old["week"] = 7; old["randomState"] = 0;
            if (version == 5) old["blocRulesStartWeek"] = 2; // Never recompute this established rule boundary.
            string before = old.ToString(Formatting.None);
            var v5 = EpisodeSaveMigrations.PrepareV5Payload(old, out _);
            // PrepareV6Payload, not PrepareCurrentPayload: this test is about the v5-to-v6 step, and
            // "current" moved on to schema 7 when the contestant card copy was added.
            var result = EpisodeSaveMigrations.PrepareV6Payload(old, out bool migrated);
            Assert.That(migrated, Is.True); Assert.That((int)result["schemaVersion"], Is.EqualTo(6));
            Assert.That((int)result["npcSocial"]["rulesStartWeek"], Is.EqualTo(8));
            AssertPreserved(v5, result);
            Assert.That(old.ToString(Formatting.None), Is.EqualTo(before));
            var repeat = EpisodeSaveMigrations.PrepareV6Payload(result, out migrated);
            Assert.That(migrated, Is.False); Assert.That(ReferenceEquals(result, repeat), Is.False);
            Assert.That(JToken.DeepEquals(result, repeat), Is.True);
            using var files = new Files(); files.Write(old);
            var originalBytes = File.ReadAllBytes(files.Store.SavePath);
            Assert.That(files.Store.TryLoad(out var loaded, out string message), Is.True, message);
            Assert.That(message, Does.StartWith("Local episode loaded and validated.").And.Contain("NPC conversations begin in week 8; the current week is unchanged."));
            Assert.That(loaded.npcSocial.rulesStartWeek, Is.EqualTo(8)); Assert.That(loaded.randomState, Is.Zero);
            Assert.That(loaded.blocRulesStartWeek, Is.EqualTo((int)v5["blocRulesStartWeek"]));
            Assert.That(loaded.npcSocial.pending, Is.Empty); Assert.That(loaded.npcSocial.pairMemory, Is.Empty);
            Assert.That(NpcSocialState.IsEligible(loaded), Is.False);
            Assert.That(File.ReadAllBytes(files.Store.SavePath), Is.EqualTo(originalBytes));
            files.Store.Save(loaded);
            Assert.That(File.ReadAllBytes(files.Store.BackupPath), Is.EqualTo(originalBytes));
            Assert.That(files.Store.TryLoad(out var again, out message), Is.True, message);
            Assert.That(JToken.DeepEquals(Capture(loaded), Capture(again)), Is.True);
        }

        [Test]
        public void EveryV5PhaseAndPendingOrRevealedBallotSurvivesWithoutReplay()
        {
            var engine = new EpisodeEngine(ContentCatalog.Create(502));
            var phases = new HashSet<EpisodePhase>(); bool partial = false, revealed = false;
            for (int guard = 0; guard < 280; guard++)
            {
                var state = engine.Snapshot; phases.Add(state.phase);
                partial |= state.phase == EpisodePhase.Eviction && state.votes.Count > 0 && !state.evictionResolved;
                revealed |= state.phase == EpisodePhase.Eviction && state.evictionResolved;
                var old = CaptureV5(state); var migrated = EpisodeSaveMigrations.UpgradeV5ToV6(old);
                AssertPreserved(old, migrated);
                using (var files = new Files())
                {
                    files.Write(old);
                    Assert.That(files.Store.TryLoad(out var loaded, out string message), Is.True, message);
                    Assert.That(NpcSocialState.IsEligible(loaded), Is.False);
                    Assert.That(loaded.npcSocial.rulesStartWeek, Is.EqualTo(state.week + 1));
                }
                if (state.phase == EpisodePhase.Finished) break;
                var result = engine.Apply(EpisodeEngineTests.NextCommand(state));
                Assert.That(result.accepted, Is.True, result.reason);
            }
            Assert.That(phases.Count, Is.EqualTo(16)); Assert.That(partial && revealed, Is.True);
        }

        [TestCase("blocRulesStartWeek", "0")] [TestCase("blocRulesStartWeek", "-1")]
        [TestCase("blocRulesStartWeek", "3")] [TestCase("blocRulesStartWeek", "102")]
        [TestCase("blocRulesStartWeek", "2147483648")] [TestCase("blocRulesStartWeek", "1.5")]
        [TestCase("blocRulesStartWeek", "null")] [TestCase("blocRulesStartWeek", "true")]
        [TestCase("week", "101")] [TestCase("phase", "16")]
        [TestCase("revision", "1000001")] [TestCase("playerStudyBonus", "6")]
        [TestCase("contestants[0].stats.luck", "10.1")]
        public void FrozenV5RejectsFormerlyInvalidValuesBeforeAddingAnyDefaults(string path, string value)
        {
            var old = CaptureV5(ContentCatalog.Create(601)); old.SelectToken(path).Replace(JToken.Parse(value));
            AssertRejectedOld(old);
        }

        [TestCase(1, 1001, false)] [TestCase(2, 1001, false)] [TestCase(3, 1001, false)]
        [TestCase(4, 1030, true)] [TestCase(5, 1030, true)] [TestCase(4, 1031, false)] [TestCase(5, 1031, false)]
        public void ScoreCeilingsStayTiedToTheOriginalVersion(int version, int score, bool valid)
        {
            var old = Historical(version);
            old["competitionScores"] = new JArray(new JObject { ["contestantId"] = old["contestants"][0]["id"].DeepClone(), ["score"] = score });
            if (valid) Assert.That((double)EpisodeSaveMigrations.PrepareCurrentPayload(old, out _)["competitionScores"][0]["score"], Is.EqualTo(score));
            else AssertRejectedOld(old);
        }

        [Test]
        public void FrozenV5RejectsUnknownAndMissingFieldsRecursivelyWithoutFollowingTheGrowingDto()
        {
            var fixtureFactory = typeof(PersistenceV4MigrationTests).GetMethod("RichV3Fixture", BindingFlags.NonPublic | BindingFlags.Static);
            var old = (JObject)fixtureFactory.Invoke(null, null);
            old["schemaVersion"] = 5; old["playerStudyBonus"] = 0; old["blocRulesStartWeek"] = 1;
            // This deliberately rich fixture characterizes SHAPE, not a playable phase.
            // Semantic acceptance is covered independently by the full sixteen-phase test.
            Assert.DoesNotThrow(() => CheckFrozenShape(old));
            var paths = old.DescendantsAndSelf().OfType<JObject>().Select(row => row.Path).ToArray();
            Assert.That(paths.Length, Is.GreaterThan(25));
            foreach (string path in paths) foreach (bool missing in new[] { true, false })
            {
                var copy = (JObject)old.DeepClone(); var row = path.Length == 0 ? copy : (JObject)copy.SelectToken(path);
                if (missing) row.Properties().Last().Remove(); else row["futureNpcField"] = 0;
                Assert.Throws<InvalidDataException>(() => CheckFrozenShape(copy), path);
            }
            old["npcSocial"] = new JObject(); AssertRejectedOld(old);
        }

        [Test]
        public void PendingVenueFractionalDurationAndEveryNestedRowRoundTripDetached()
        {
            var original = PendingState(); var engine = new EpisodeEngine(original);
            var snapshot = engine.Snapshot;
            snapshot.npcSocial.pending[0].topic = "gossip"; snapshot.npcSocial.pairMemory[0].count = 99;
            snapshot.npcSocial.cooldowns[0].untilTick = 99; snapshot.npcSocial.pending.Clear();
            Assert.That(JToken.DeepEquals(Capture(original), Capture(engine.Snapshot)), Is.True);
            using var files = new Files(); files.Store.Save(original);
            Assert.That(files.Store.TryLoad(out var first, out string message), Is.True, message);
            Assert.That(files.Store.TryLoad(out var second, out message), Is.True, message);
            Assert.That(first.npcSocial.pending[0].durationMs, Is.EqualTo(15000.125));
            Assert.That(first.npcSocial.pending[0].rendezvousId, Is.EqualTo("living-east-chat"));
            Assert.That(first.npcSocial.randomState, Is.Zero, "Zero is valid for an activated saved generator.");
            Assert.That(first.npcSocial.pending[0].durationMs - (first.npcSocial.clockTick - first.npcSocial.pending[0].startedTick) * 1000L, Is.EqualTo(15000.125));
            first.npcSocial.pending[0].rendezvousId = "bedroom-south-chat";
            first.npcSocial.pairMemory[0].lastTopic = "gossip"; first.npcSocial.cooldowns.Clear();
            Assert.That(JToken.DeepEquals(Capture(original), Capture(second)), Is.True);
        }

        [TestCase("npcSocial", "null")] [TestCase("npcSocial.rulesStartWeek", "0")]
        [TestCase("npcSocial.rulesStartWeek", "3")] [TestCase("npcSocial.rulesStartWeek", "2147483648")]
        [TestCase("npcSocial.clockTick", "-1")] [TestCase("npcSocial.clockTick", "13")]
        [TestCase("npcSocial.clockTick", "9223372036854775808")]
        [TestCase("npcSocial.nextScanTick", "10")] [TestCase("npcSocial.nextScanTick", "14")]
        [TestCase("npcSocial.nextConversationSequence", "1")]
        [TestCase("npcSocial.pending[0].durationMs", "0")] [TestCase("npcSocial.pending[0].durationMs", "40000.1")]
        [TestCase("npcSocial.pending[0].startedTick", "-1")]
        [TestCase("npcSocial.pending[0].topic", "'invented-topic'")]
        [TestCase("npcSocial.pending[0].rendezvousId", "'unregistered-room'")]
        [TestCase("npcSocial.pending[0].firstId", "'player'")]
        [TestCase("npcSocial.pending[0].sequence", "2")]
        [TestCase("npcSocial.pending[0].phase", "1")]
        [TestCase("npcSocial.pairMemory[1].count", "2")]
        [TestCase("npcSocial.cooldowns[0].untilTick", "26")]
        public void MalformedCurrentSubsystemCannotBeRepairedByDefaults(string path, string value)
        {
            var token = Capture(PendingState()); token.SelectToken(path).Replace(JToken.Parse(value));
            using var files = new Files(); files.Write(token); var bytes = File.ReadAllBytes(files.Store.SavePath);
            Assert.That(files.Store.TryLoad(out var state, out _), Is.False); Assert.That(state, Is.Null);
            Assert.That(File.ReadAllBytes(files.Store.SavePath), Is.EqualTo(bytes));
        }

        [Test]
        public void IssuedReceiptsAllowConversationTwoToFinishBeforeOneWithoutRequiringACompletionWatermark()
        {
            var state = PendingState(); state.npcSocial.nextConversationSequence = 3;
            // Sequence2 was already finalized. Sequence1 remains pending and must stay loadable.
            Assert.That(EpisodeValidation.TryValidate(state, out string message), Is.True, message);
            state.npcSocial.pending[0].sequence = 3;
            Assert.That(EpisodeValidation.TryValidate(state, out _), Is.False);
        }

        [Test]
        public void TwoDisjointPairsCannotReserveTheSameTwoSeatVenue()
        {
            var state = PendingState(); var social = state.npcSocial; social.nextConversationSequence = 3;
            var first = state.contestants[3].id; var second = state.contestants[4].id;
            social.pending.Add(new NpcConversationState { sequence = 2, firstId = first, secondId = second,
                topic = "casual", rendezvousId = "kitchen-west-chat", week = 1, phase = EpisodePhase.Social,
                startedTick = 10, durationMs = 9000.25 });
            social.pairMemory.Add(new NpcPairMemoryState { fromId = first, toId = second, lastTopic = "casual", count = 1, lastStartTick = 10 });
            social.pairMemory.Add(new NpcPairMemoryState { fromId = second, toId = first, lastTopic = "casual", count = 1, lastStartTick = 10 });
            Assert.That(EpisodeValidation.TryValidate(state, out string reason), Is.True, reason);
            social.pending[1].rendezvousId = social.pending[0].rendezvousId;
            Assert.That(EpisodeValidation.TryValidate(state, out reason), Is.False); Assert.That(reason, Does.Contain("two-seat"));
        }

        [TestCase(1)] [TestCase(100)]
        public void RestrictedImportStartsNextWeekAndPreservesBothBoundariesAcrossReload(int week)
        {
            var source = (JObject)typeof(PersistenceTests).GetMethod("WebFixture", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
            source["state"]["week"] = week; using var files = new Files(); string json = source.ToString();
            Assert.That(WebSaveImporter.TryImport(json, files.DirectoryPath, out var state, out string message), Is.True, message);
            Assert.That(state.npcSocial.rulesStartWeek, Is.EqualTo(week + 1)); Assert.That(state.blocRulesStartWeek, Is.EqualTo(week + 1));
            Assert.That(state.npcSocial.randomState, Is.EqualTo(NpcSocialState.InitialRandomState(state.seed)));
            Assert.That(state.npcSocial.pending, Is.Empty); Assert.That(state.votes, Is.Empty);
            Assert.That(message, Does.Contain("NPC conversations begin in week " + (week + 1)));
            Assert.That(File.ReadAllText(Directory.GetFiles(files.DirectoryPath, "web-original-*.json").Single()), Is.EqualTo(json));
            files.Store.Save(state); Assert.That(files.Store.TryLoad(out var loaded, out message), Is.True, message);
            Assert.That(JToken.DeepEquals(Capture(state), Capture(loaded)), Is.True);
        }

        private static EpisodeState PendingState()
        {
            var state = ContentCatalog.Create(603); state.revision = 12;
            var first = state.contestants[1].id; var second = state.contestants[2].id;
            var social = state.npcSocial; social.clockTick = 10; social.nextScanTick = 12; social.nextConversationSequence = 2; social.randomState = 0;
            social.pending.Add(new NpcConversationState { sequence = 1, firstId = first, secondId = second,
                topic = "bonding", rendezvousId = "living-east-chat", week = 1, phase = EpisodePhase.Social, startedTick = 10, durationMs = 15000.125 });
            social.pairMemory.Add(new NpcPairMemoryState { fromId = first, toId = second, lastTopic = "bonding", count = 1, lastStartTick = 10 });
            social.pairMemory.Add(new NpcPairMemoryState { fromId = second, toId = first, lastTopic = "bonding", count = 1, lastStartTick = 10 });
            social.cooldowns.Add(new NpcCooldownState { npcId = first, untilTick = 10 });
            Assert.That(EpisodeValidation.TryValidate(state, out string reason), Is.True, reason); return state;
        }

        private static JObject Historical(int version)
        {
            if (version < 5) return (JObject)typeof(PersistenceV5MigrationTests).GetMethod("Historical", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { version });
            return CaptureV5(ContentCatalog.Create(601));
        }
        private static JObject CaptureV5(EpisodeState state)
        {
            var result = PersistenceMigrationTests.StripCardCopy(PersistenceMigrationTests.StripSchema8(PersistenceMigrationTests.StripSchema9(PersistenceMigrationTests.StripSchema10(PersistenceMigrationTests.StripSchema11(Capture(state))))));
            result.Remove("npcSocial"); result["schemaVersion"] = 5; return result;
        }
        private static JObject Capture(EpisodeState state) => JObject.FromObject(state, Serializer());
        private static JsonSerializer Serializer() => (JsonSerializer)typeof(EpisodeSaveStore).Assembly.GetType("Gamesim.Persistence.SaveJson", true)
            .GetMethod("Serializer", BindingFlags.Public | BindingFlags.Static).Invoke(null, null);
        private static void CheckFrozenShape(JObject payload)
        {
            var assembly = typeof(EpisodeSaveStore).Assembly;
            try
            {
                assembly.GetType("Gamesim.Persistence.SaveJson", true).GetMethod("CheckDtoShape", BindingFlags.Public | BindingFlags.Static)
                    .Invoke(null, new object[] { payload, assembly.GetType("Gamesim.Persistence.FrozenEpisodeV5+State", true), "state(v5)" });
            }
            catch (TargetInvocationException error) when (error.InnerException != null) { throw error.InnerException; }
        }
        private static void AssertPreserved(JObject old, JObject current)
        {
            Assert.That(current.Properties().Select(row => row.Name).Except(old.Properties().Select(row => row.Name)), Is.EqualTo(new[] { "npcSocial" }));
            foreach (var property in old.Properties()) if (property.Name != "schemaVersion")
                Assert.That(JToken.DeepEquals(property.Value, current[property.Name]), Is.True, property.Name);
        }
        private static void AssertRejectedOld(JObject old)
        {
            string before = old.ToString(Formatting.None);
            Assert.Throws<InvalidDataException>(() => EpisodeSaveMigrations.PrepareCurrentPayload(old, out _));
            Assert.That(old.ToString(Formatting.None), Is.EqualTo(before));
            using var files = new Files(); files.Write(old);
            Assert.That(files.Store.TryLoad(out _, out _), Is.False);
            Assert.That(File.ReadAllText(files.Store.SavePath), Is.EqualTo(PersistenceMigrationTests.Envelope(old)));
        }
        private sealed class Files : IDisposable
        {
            public readonly string DirectoryPath = Path.Combine(Path.GetTempPath(), "GamesimV6MigrationTests-" + Guid.NewGuid().ToString("N"));
            public readonly EpisodeSaveStore Store;
            public Files() { Directory.CreateDirectory(DirectoryPath); Store = new EpisodeSaveStore(Path.Combine(DirectoryPath, "episode.json")); }
            public void Write(JObject payload) => File.WriteAllText(Store.SavePath, PersistenceMigrationTests.Envelope(payload));
            public void Dispose()
            {
                string path = Path.GetFullPath(DirectoryPath);
                Assert.That(Path.GetDirectoryName(path), Is.EqualTo(Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar)));
                Assert.That(Path.GetFileName(path), Does.StartWith("GamesimV6MigrationTests-"));
                if (Directory.Exists(path)) Directory.Delete(path, true);
            }
        }
    }
}
