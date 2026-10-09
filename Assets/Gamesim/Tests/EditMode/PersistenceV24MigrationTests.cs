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
    /// Schema24 persistence foundation only. These synthetic contracts and isolated SaveStore
    /// slots do not stand in for a retained schema23 shipping save, native scene, or desktop run.
    /// No Unity APIs: the coordinating harness may execute them separately under managed Mono.
    /// </summary>
    public sealed class PersistenceV24MigrationTests
    {
        private static JsonSerializer Serializer() => (JsonSerializer)typeof(EpisodeSaveStore).Assembly
            .GetType("Gamesim.Persistence.SaveJson", true).GetMethod("Serializer", BindingFlags.Public | BindingFlags.Static).Invoke(null, null);
        private static JObject Payload(EpisodeState s) => JObject.FromObject(s, Serializer());
        private static void Off(EpisodeState s)
        {
            Assert.That(s.schemaVersion, Is.EqualTo(28));
            Assert.That(s.unifiedCommitmentRulesVersion, Is.Zero);
            Assert.That(s.unifiedCommitments, Is.Not.Null.And.Empty);
            Assert.That(s.unifiedHearingRulesVersion, Is.Zero);
            Assert.That(s.unifiedHearingEvidence, Is.Not.Null.And.Empty);
            Assert.That(s.unifiedHearingReceipts, Is.Not.Null.And.Empty);
            EpisodeSaveValidation.Validate(s);
        }
        private static JObject Historical23(EpisodeState s)
        {
            Off(s);
            var old = PersistenceMigrationTests.StripSchema25(Payload(s));
            old.Remove("unifiedCommitmentRulesVersion"); old.Remove("unifiedCommitments"); old["schemaVersion"] = 23;
            return old;
        }
        private static EpisodeState Read(JObject old)
        {
            var current = EpisodeSaveMigrations.PrepareCurrentPayload(old, out _).ToObject<EpisodeState>(Serializer());
            Off(current); return current;
        }
        private static void Equivalent(EpisodeState expected, EpisodeState actual) =>
            Assert.That(JToken.DeepEquals(Payload(expected), Payload(actual)), Is.True, "Every field, RNG stream and receipt must survive.");
        private static void OnlyDefaults(JObject old23, JObject current)
        {
            Assert.That((int)old23["schemaVersion"], Is.EqualTo(23));
            current = PersistenceMigrationTests.StripSchema26((JObject)current.DeepClone());
            // This fixture still pins the exact old23->24 step. A current load also has the
            // separately tested inactive hearing defaults; refuse nonempty values before projecting.
            if ((int)current["schemaVersion"] == 25)
            {
                Assert.That((int)current["unifiedHearingRulesVersion"], Is.Zero);
                Assert.That((JArray)current["unifiedHearingEvidence"], Is.Empty);
                Assert.That((JArray)current["unifiedHearingReceipts"], Is.Empty);
                current = PersistenceMigrationTests.StripSchema25((JObject)current.DeepClone());
                current["schemaVersion"] = 24;
            }
            Assert.That((int)current["schemaVersion"], Is.EqualTo(24));
            Assert.That((int)current["unifiedCommitmentRulesVersion"], Is.Zero);
            Assert.That((JArray)current["unifiedCommitments"], Is.Empty);
            Assert.That(current.Properties().Select(p => p.Name).Except(old23.Properties().Select(p => p.Name)),
                Is.EquivalentTo(new[] { "unifiedCommitmentRulesVersion", "unifiedCommitments" }));
            var projection = (JObject)current.DeepClone();
            projection.Remove("unifiedCommitmentRulesVersion"); projection.Remove("unifiedCommitments"); projection["schemaVersion"] = 23;
            Assert.That(JToken.DeepEquals(projection, old23), Is.True, "No old promise, deal, oath, history, ID or RNG is converted.");
        }
        private static EpisodeState Fresh()
        {
            var s = EconomyRulesTests.Fresh(); s.strategyRulesStartWeek = 1; s.agencyRulesStartWeek = 1;
            EpisodeEngine.EnableRead(s); EpisodeEngine.EnableCommitments(s);
            return s;
        }
        private static EpisodeState Apply(EpisodeState s, EpisodeCommandKind kind, string target = null, string text = null, string second = null)
        {
            var result = new EpisodeEngine(s).Apply(new EpisodeCommand { id = "v24-" + s.revision + "-" + kind,
                actorId = s.playerId, expectedRevision = s.revision, expectedPhase = s.phase,
                kind = kind, targetId = target, text = text, secondTargetId = second });
            Assert.That(result.accepted, Is.True, result.reason); Off(result.state); return result.state;
        }
        private static EpisodeState Pitch(string stage)
        {
            var s = Fresh(); s.phase = EpisodePhase.Nomination; s.hohId = s.playerId; s.nominees.Clear();
            NpcSocialActions.Court(s, s.Active.First(c => !c.isPlayer), s.Find(s.playerId));
            Assert.That(s.replyCards, Has.Count.EqualTo(1));
            string id = s.replyCards[0].id;
            if (stage != "pending") s = Apply(s, EpisodeCommandKind.ReplyToHouseguest, id, "feel-out");
            if (stage == "answered") s = Apply(s, EpisodeCommandKind.ReplyToHouseguest, id, "promise-safety");
            return s;
        }
        private static EpisodeState Speech(bool quiet)
        {
            var s = Fresh(); var npcs = s.Active.Where(c => !c.isPlayer).Select(c => c.id).ToArray();
            s.phase = EpisodePhase.Eviction; s.evictionStage = EvictionStage.Speeches;
            s.hohId = npcs[1]; s.vetoHolderId = s.hohId; s.vetoResolved = true;
            s.vetoPlayers = s.Active.Take(EpisodeEngine.VetoPlayerCount(s.Active.Count())).Select(c => c.id).ToList();
            s.nominees = new List<string> { s.playerId, npcs[0] };
            return Apply(s, EpisodeCommandKind.SubmitEvictionSpeech, text: quiet ? "" : "My exact public words.\nPlease hear me out.",
                second: quiet ? "quiet" : "strategic");
        }

        [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)] [TestCase(6)]
        [TestCase(7)] [TestCase(8)] [TestCase(9)] [TestCase(10)] [TestCase(11)] [TestCase(12)]
        [TestCase(13)] [TestCase(14)] [TestCase(15)] [TestCase(16)] [TestCase(17)] [TestCase(18)]
        [TestCase(19)] [TestCase(20)] [TestCase(21)] [TestCase(22)] [TestCase(23)]
        public void EveryHistoricalVersionAddsOnlyInactiveAuthorityWithoutChangingItsFrozen23Result(int version)
        {
            // Synthetic v1 plus already-frozen dispatches, not a claim of 23 captured player saves.
            var v1 = PersistenceMigrationTests.V1Fixture();
            var old = version == 1 ? v1 : (JObject)typeof(EpisodeSaveMigrations)
                .GetMethod("PrepareV" + version + "Payload", BindingFlags.Public | BindingFlags.Static)
                .Invoke(null, new object[] { v1, false });
            string original = old.ToString(Formatting.None);
            var expected23 = EpisodeSaveMigrations.PrepareV23Payload(old, out _);
            var current = EpisodeSaveMigrations.PrepareCurrentPayload(old, out bool migrated);
            Assert.That(migrated, Is.True); OnlyDefaults(expected23, current); Off(current.ToObject<EpisodeState>(Serializer()));
            Assert.That(old.ToString(Formatting.None), Is.EqualTo(original));
            var again = EpisodeSaveMigrations.PrepareCurrentPayload(current, out migrated);
            Assert.That(migrated, Is.False); Assert.That(JToken.DeepEquals(again, current), Is.True);
            Assert.That(ReferenceEquals(again, current), Is.False);
            ((JArray)again["unifiedCommitments"]).Add(new JObject());
            Assert.That((JArray)current["unifiedCommitments"], Is.Empty, "Even inactive arrays are independently owned.");
        }

        [TestCase(false)] [TestCase(true)]
        public void NewBuilderAndCatalogSeasonsStayOffAndRoundTripWithoutOptIn(bool builder)
        {
            var s = builder ? SeasonBuilder.Create(new SeasonBuilder.Choice(), 2401) : ContentCatalog.Create(2401);
            Off(s); var engine = new EpisodeEngine(s); Off(engine.Snapshot);
            using var files = new Files(); files.Store.Save(engine.Snapshot);
            byte[] original = File.ReadAllBytes(files.Store.SavePath);
            Assert.That(files.Store.TryLoad(out var loaded, out string message), Is.True, message);
            Off(loaded); Equivalent(s, loaded);
            Assert.That(File.ReadAllBytes(files.Store.SavePath), Is.EqualTo(original));
            Assert.That(File.Exists(files.Store.BackupPath), Is.False);
            Assert.That(message, Does.Not.Contain("migrated"));
        }

        [TestCase(false)] [TestCase(true)]
        public void EvenPristineOldOpeningNeverOptsIntoUnifiedCommitments(bool economy)
        {
            var s = EconomyRulesTests.Fresh(enable: economy);
            var old = Historical23(s); string before = old.ToString(Formatting.None);
            var current = EpisodeSaveMigrations.UpgradeV23ToV24(old);
            OnlyDefaults(old, current); Equivalent(s, Read(old));
            Assert.That(old.ToString(Formatting.None), Is.EqualTo(before));
        }

        [TestCase("pending")] [TestCase("assessed")] [TestCase("answered")]
        [TestCase("speech")] [TestCase("quiet")]
        public void OldPitchAndSpeechAuthoritySurvivesWithoutBecomingUnifiedRows(string moment)
        {
            var s = moment == "speech" || moment == "quiet" ? Speech(moment == "quiet") : Pitch(moment);
            var old = Historical23(s); string before = old.ToString(Formatting.None);
            var current = EpisodeSaveMigrations.UpgradeV23ToV24(old);
            OnlyDefaults(old, current); Equivalent(s, Read(old));
            Assert.That(old.ToString(Formatting.None), Is.EqualTo(before));
            using var files = new Files();
            File.WriteAllText(files.Store.SavePath, PersistenceMigrationTests.Envelope(old));
            byte[] bytes = File.ReadAllBytes(files.Store.SavePath);
            Assert.That(files.Store.TryLoad(out var loaded, out string message), Is.True, message);
            Assert.That(message, Does.Contain("Schema 23").And.Contain("schema 28 in memory"));
            Off(loaded); Equivalent(s, loaded);
            Assert.That(File.ReadAllBytes(files.Store.SavePath), Is.EqualTo(bytes));
            Assert.That(File.Exists(files.Store.BackupPath), Is.False);
            files.Store.Save(loaded);
            Assert.That(File.ReadAllBytes(files.Store.BackupPath), Is.EqualTo(bytes));
            Assert.That(files.Store.TryLoad(out var again, out message), Is.True, message);
            Off(again); Equivalent(loaded, again); Assert.That(message, Does.Not.Contain("migrated"));
        }

        [Test]
        public void HistoricalDealsPromisesAndOathsAreNotConvertedOrSettled()
        {
            var s = Fresh(); string npc = s.Active.First(c => !c.isPlayer).id;
            s.promises.Add(new PromiseState { id = "legacy-promise", fromId = s.playerId, toId = npc,
                kind = PromiseKind.Safety, status = PromiseStatus.Active, week = 1, expiresWeek = 2 });
            s.deals.Add(new DealState { id = "legacy-deal", proposerId = npc, recipientId = s.playerId,
                type = DealKind.SafetyAgreement, status = DealStatus.Active, trustImpact = DealTrust.High, week = 1, expiresWeek = 2 });
            s.loyaltyOaths.Add(new WebOathRecord { playerId = s.playerId, targetId = npc, week = 1, timestamp = s.nextSequence++ });
            s.shownOathMilestones.Add(npc);
            var old = Historical23(s); var current = EpisodeSaveMigrations.UpgradeV23ToV24(old);
            OnlyDefaults(old, current); Equivalent(s, Read(old));
            foreach (string field in new[] { "promises", "deals", "loyaltyOaths", "randomState", "nextSequence", "acceptedCommandIds" })
                Assert.That(JToken.DeepEquals(old[field], current[field]), Is.True, field);
        }

        [Test]
        public void SnapshotOwnsItsNewListAndEachScalarRecordWithoutRepairingMalformedInputs()
        {
            // A nonempty list is intentionally not a playable/savable state in this foundation.
            var s = Fresh(); s.unifiedCommitments.Add(new UnifiedCommitmentState { id = "detached", makerId = s.playerId });
            var copy = s.Clone();
            Assert.That(ReferenceEquals(copy.unifiedCommitments, s.unifiedCommitments), Is.False);
            Assert.That(ReferenceEquals(copy.unifiedCommitments[0], s.unifiedCommitments[0]), Is.False);
            copy.unifiedCommitments[0].id = "changed"; copy.unifiedCommitments.Add(null);
            Assert.That(s.unifiedCommitments, Has.Count.EqualTo(1)); Assert.That(s.unifiedCommitments[0].id, Is.EqualTo("detached"));
            Assert.Throws<InvalidDataException>(() => EpisodeSaveValidation.Validate(s));
            s.unifiedCommitments = null; Assert.That(s.Clone().unifiedCommitments, Is.Null);
            s.unifiedCommitments = new List<UnifiedCommitmentState> { null };
            Assert.That(s.Clone().unifiedCommitments.Single(), Is.Null);
            Assert.Throws<InvalidDataException>(() => EpisodeSaveValidation.Validate(s));
        }

        [TestCase("unifiedCommitmentRulesVersion")] [TestCase("unifiedCommitments")]
        public void Schema23CannotSmuggleEvenDisabledFutureFields(string field)
        {
            var old = Historical23(Fresh()); old[field] = field == "unifiedCommitments" ? (JToken)new JArray() : new JValue(0);
            string original = old.ToString(Formatting.None);
            Assert.Throws<InvalidDataException>(() => EpisodeSaveMigrations.UpgradeV23ToV24(old));
            Assert.Throws<InvalidDataException>(() => EpisodeSaveMigrations.PrepareCurrentPayload(old, out _));
            Assert.That(old.ToString(Formatting.None), Is.EqualTo(original));
        }

        [TestCase(0)] [TestCase(-1)] [TestCase(27)]
        public void UnsupportedVersionsAreNotGuessed(int version)
        {
            var o = Payload(Fresh()); o["schemaVersion"] = version;
            string original = o.ToString(Formatting.None);
            Assert.Throws<InvalidDataException>(() => EpisodeSaveMigrations.PrepareV26Payload(o, out _));
            // 27 was the former current and 28 is current now: the former27 dispatch refuses an unknown 28,
            // and the actual current dispatch an unknown 29.
            if (version == 27)
            {
                var former27Future = (JObject)o.DeepClone(); former27Future["schemaVersion"] = 28;
                Assert.Throws<InvalidDataException>(() => EpisodeSaveMigrations.PrepareV27Payload(former27Future, out _));
            }
            var currentFuture = (JObject)o.DeepClone(); if (version == 27) currentFuture["schemaVersion"] = 29;
            Assert.Throws<InvalidDataException>(() => EpisodeSaveMigrations.PrepareCurrentPayload(currentFuture, out _));
            Assert.That(o.ToString(Formatting.None), Is.EqualTo(original));
        }

        [TestCase("missing-version")] [TestCase("missing-list")] [TestCase("null-version")] [TestCase("null-list")]
        [TestCase("text-version")] [TestCase("fraction-version")] [TestCase("negative-version")] [TestCase("unsupported-one")]
        [TestCase("future-version")] [TestCase("object-list")] [TestCase("null-row")] [TestCase("nonempty-list")]
        [TestCase("extra-root")] [TestCase("row-extra-field")]
        public void StrictCurrentDiskValidationRejectsIncompleteOrEnabledFoundationWithoutTouchingBytes(string defect)
        {
            using var files = new Files(); var o = Payload(Fresh());
            switch (defect)
            {
                case "missing-version": o.Remove("unifiedCommitmentRulesVersion"); break;
                case "missing-list": o.Remove("unifiedCommitments"); break;
                case "null-version": o["unifiedCommitmentRulesVersion"] = JValue.CreateNull(); break;
                case "null-list": o["unifiedCommitments"] = JValue.CreateNull(); break;
                case "text-version": o["unifiedCommitmentRulesVersion"] = "0"; break;
                case "fraction-version": o["unifiedCommitmentRulesVersion"] = 0.0; break;
                case "negative-version": o["unifiedCommitmentRulesVersion"] = -1; break;
                case "unsupported-one": o["unifiedCommitmentRulesVersion"] = 1; break;
                case "future-version": o["unifiedCommitmentRulesVersion"] = 2; break;
                case "object-list": o["unifiedCommitments"] = new JObject(); break;
                case "null-row": ((JArray)o["unifiedCommitments"]).Add(JValue.CreateNull()); break;
                case "nonempty-list": ((JArray)o["unifiedCommitments"]).Add(JObject.FromObject(new UnifiedCommitmentState(), Serializer())); break;
                case "extra-root": o["futureCommitmentFlag"] = false; break;
                case "row-extra-field": var row = JObject.FromObject(new UnifiedCommitmentState(), Serializer()); row["future"] = 1;
                    ((JArray)o["unifiedCommitments"]).Add(row); break;
                default: Assert.Fail("Unknown defect"); break;
            }
            File.WriteAllText(files.Store.SavePath, PersistenceMigrationTests.Envelope(o));
            byte[] original = File.ReadAllBytes(files.Store.SavePath);
            Assert.That(files.Store.TryLoad(out var loaded, out string message), Is.False, defect);
            Assert.That(loaded, Is.Null); Assert.That(message, Does.Not.Contain("checksum"));
            Assert.That(File.ReadAllBytes(files.Store.SavePath), Is.EqualTo(original));
            Assert.That(File.Exists(files.Store.BackupPath), Is.False);
            Assert.That(Directory.GetFiles(files.Root, "*.pending-*"), Is.Empty);
        }

        [TestCase("version-one")] [TestCase("version-negative")] [TestCase("null-list")] [TestCase("nonempty-list")]
        public void RejectedCurrentCandidatePreservesBothGoodPrimaryAndBackup(string defect)
        {
            using var files = new Files(); var good = Fresh(); files.Store.Save(good); files.Store.Save(good);
            byte[] primary = File.ReadAllBytes(files.Store.SavePath), backup = File.ReadAllBytes(files.Store.BackupPath);
            var bad = good.Clone();
            switch (defect)
            {
                case "version-one": bad.unifiedCommitmentRulesVersion = 1; break;
                case "version-negative": bad.unifiedCommitmentRulesVersion = -1; break;
                case "null-list": bad.unifiedCommitments = null; break;
                case "nonempty-list": bad.unifiedCommitments.Add(new UnifiedCommitmentState()); break;
                default: Assert.Fail("Unknown defect"); break;
            }
            Assert.Throws<InvalidDataException>(() => files.Store.Save(bad));
            Assert.That(File.ReadAllBytes(files.Store.SavePath), Is.EqualTo(primary));
            Assert.That(File.ReadAllBytes(files.Store.BackupPath), Is.EqualTo(backup));
            Assert.That(files.Store.TryLoad(out var loaded, out string message), Is.True, message); Equivalent(good, loaded); Off(loaded);
        }

        [Test]
        public void ExplicitRecoveryRetainsOldEnvelopeAndDamagedPrimaryWhileReturningInactive24()
        {
            using var files = new Files(); var old = Historical23(Pitch("assessed"));
            File.WriteAllText(files.Store.BackupPath, PersistenceMigrationTests.Envelope(old));
            byte[] backup = File.ReadAllBytes(files.Store.BackupPath);
            File.WriteAllText(files.Store.SavePath, "damaged original primary"); byte[] damaged = File.ReadAllBytes(files.Store.SavePath);
            Assert.That(files.Store.TryLoad(out _, out string message), Is.False); Assert.That(message, Does.Contain("backup"));
            Assert.That(File.ReadAllBytes(files.Store.SavePath), Is.EqualTo(damaged));
            Assert.That(files.Store.TryRecoverBackup(out var loaded, out message), Is.True, message); Off(loaded);
            OnlyDefaults(old, Payload(loaded));
            Assert.That(File.ReadAllBytes(files.Store.SavePath), Is.EqualTo(backup));
            Assert.That(File.ReadAllBytes(files.Store.BackupPath), Is.EqualTo(backup));
            Assert.That(File.ReadAllBytes(Directory.GetFiles(files.Root, "*.before-recovery-*.json").Single()), Is.EqualTo(damaged));
            files.Store.Save(loaded); Assert.That(File.ReadAllBytes(files.Store.BackupPath), Is.EqualTo(backup));
        }

        [Test]
        public void FullSeasonMigrationRoundTripsKeepExactInactiveStateAndCommandOutcomes()
        {
            var direct = new EpisodeEngine(Fresh()); var roundTrip = new EpisodeEngine(Read(Historical23(direct.Snapshot)));
            bool finished = false;
            for (int step = 0; step < 500; step++)
            {
                var before = direct.Snapshot; Off(before); Equivalent(before, roundTrip.Snapshot);
                if (before.phase == EpisodePhase.Finished) { finished = true; break; }
                var command = EpisodeEngineTests.NextCommand(before);
                var a = direct.Apply(command); var b = roundTrip.Apply(command);
                Assert.That(a.accepted, Is.True, a.reason); Assert.That(b.accepted, Is.True, b.reason);
                Equivalent(a.state, b.state);
                roundTrip = new EpisodeEngine(Read(Historical23(b.state)));
            }
            Assert.That(finished, Is.True);
            // Both engines use current code: a separate source-pinned old23/new24 replay must prove cross-build parity.
        }

        private sealed class Files : IDisposable
        {
            public readonly string Root = Path.Combine(Path.GetTempPath(), "GamesimV24-" + Guid.NewGuid().ToString("N"));
            public readonly EpisodeSaveStore Store;
            public Files() { Directory.CreateDirectory(Root); Store = new EpisodeSaveStore(Path.Combine(Root, "episode.json")); }
            public void Dispose()
            {
                string path = Path.GetFullPath(Root);
                Assert.That(Path.GetDirectoryName(path), Is.EqualTo(Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar)));
                Assert.That(Path.GetFileName(path), Does.StartWith("GamesimV24-"));
                if (Directory.Exists(path)) Directory.Delete(path, true);
            }
        }
    }
}
