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
    /// E4 against the real production SaveStore, strict serializer and migrations. Deliberately
    /// excluded from the pure SimulationTests project; these authored tests need the native Edit
    /// assembly. Every write/corruption/recovery is confined to this test's unique temporary slot.
    /// </summary>
    public sealed class HoHPitchPersistenceTests
    {
        private static JsonSerializer Serializer() => (JsonSerializer)typeof(EpisodeSaveStore).Assembly
            .GetType("Gamesim.Persistence.SaveJson", true).GetMethod("Serializer", BindingFlags.Public | BindingFlags.Static).Invoke(null, null);
        private static JObject Payload(EpisodeState s) => JObject.FromObject(s, Serializer());
        private static void Equal(EpisodeState expected, EpisodeState actual) =>
            Assert.That(JToken.DeepEquals(Payload(expected), Payload(actual)), Is.True, "Every persisted field must survive, not only the visible pitch.");

        private static EpisodeState Pending(int size = 8, bool noTarget = false)
        {
            var s = EconomyRulesTests.Fresh(size);
            s.strategyRulesStartWeek = 1; s.agencyRulesStartWeek = 1;
            EpisodeEngine.EnableCommitments(s); EpisodeEngine.EnableRead(s);
            s.phase = EpisodePhase.Nomination; s.hohId = s.playerId; s.nominees.Clear();
            string speaker = s.Active.First(c => !c.isPlayer).id;
            if (noTarget) s.alliances.Add(new AllianceState { id = "pitch-persistence-npcs", name = "The other guests",
                members = s.Active.Where(c => !c.isPlayer).Select(c => c.id).ToList() });
            NpcSocialActions.Court(s, s.Find(speaker), s.Find(s.playerId));
            Assert.That(s.replyCards, Has.Count.EqualTo(1));
            Assert.That(s.replyCards[0].kind, Is.EqualTo(ReplyCards.Pitch));
            Assert.That(s.replyCards[0].aboutId == null, Is.EqualTo(noTarget));
            EpisodeSaveValidation.Validate(s);
            return s;
        }

        private static EpisodeCommand Command(EpisodeState s, EpisodeCommandKind kind, string target = null,
            string key = null, string second = null) => new EpisodeCommand
        {
            id = "pitch-store-" + s.revision + "-" + kind, actorId = s.playerId, expectedRevision = s.revision,
            expectedPhase = s.phase, kind = kind, targetId = target, secondTargetId = second, text = key,
        };

        private static EpisodeState Apply(EpisodeState s, EpisodeCommandKind kind, string target = null,
            string key = null, string second = null)
        {
            var result = new EpisodeEngine(s).Apply(Command(s, kind, target, key, second));
            Assert.That(result.accepted, Is.True, result.reason);
            EpisodeSaveValidation.Validate(result.state);
            return result.state;
        }

        private static EpisodeState Answer(EpisodeState s, string key) =>
            Apply(s, EpisodeCommandKind.ReplyToHouseguest, s.replyCards[0].id, key);

        private static EpisodeState LoadUnchanged(EpisodeSaveStore store, EpisodeState expected)
        {
            byte[] before = File.ReadAllBytes(store.SavePath);
            // A new store owns no in-memory episode; this is a real disk deserialization/validation.
            var reader = new EpisodeSaveStore(store.SavePath);
            Assert.That(reader.TryLoad(out var loaded, out string message), Is.True, message);
            Equal(expected, loaded);
            Assert.That(File.ReadAllBytes(store.SavePath), Is.EqualTo(before), "Loading current data never rewrites its bytes.");
            Assert.That(loaded.schemaVersion, Is.EqualTo(26));
            return loaded;
        }

        private static void RejectRepeatedInspection(EpisodeSaveStore store, EpisodeState loaded)
        {
            byte[] bytes = File.ReadAllBytes(store.SavePath);
            var engine = new EpisodeEngine(loaded);
            var card = loaded.replyCards[0];
            var request = Command(loaded, EpisodeCommandKind.ReplyToHouseguest, card.id, HoHPitches.FeelOutKey);
            var result = engine.Apply(request);
            Assert.That(result.accepted, Is.False, "A fresh command ID/revision cannot buy another read of the loaded card.");
            Assert.That(result.duplicate, Is.False, "The per-card receipt, not command-ID deduplication, owns the rejection.");
            Equal(loaded, result.state); Equal(loaded, engine.Snapshot);
            Assert.That(File.ReadAllBytes(store.SavePath), Is.EqualTo(bytes));
        }

        [TestCase(8, false)] [TestCase(16, false)] [TestCase(8, true)] [TestCase(16, true)]
        public void PendingPitchRoundTripsItsExactSuggestionAndLegacyDtoShape(int size, bool noTarget)
        {
            using var files = new Files();
            var s = Pending(size, noTarget); files.Store.Save(s);
            var loaded = LoadUnchanged(files.Store, s);
            Assert.That(HoHPitches.Available(loaded), Is.True);
            Assert.That(HoHPitches.Assessed(loaded, loaded.replyCards.Single()), Is.False);
            var state = (JObject)JObject.Parse(File.ReadAllText(files.Store.SavePath))["state"];
            var card = (JObject)state["replyCards"][0];
            Assert.That(card.Properties().Select(p => p.Name), Is.EquivalentTo(new[] { "id", "week", "kind", "fromId", "aboutId" }));
            Assert.That(File.Exists(files.Store.BackupPath), Is.False);
            Assert.That(Directory.GetFiles(files.DirectoryPath, "*.pending-*"), Is.Empty);
        }

        [TestCase(false)] [TestCase(true)]
        public void AssessedPitchRoundTripsItsReceiptStandingAndOnceOnlyQuestionWithoutSpendingTheWeeklyRead(bool noTarget)
        {
            using var files = new Files(); var pending = Pending(noTarget: noTarget);
            files.Store.Save(pending); byte[] pendingBytes = File.ReadAllBytes(files.Store.SavePath);
            var command = Command(pending, EpisodeCommandKind.ReplyToHouseguest, pending.replyCards[0].id, HoHPitches.FeelOutKey);
            var assessed = new EpisodeEngine(pending).Apply(command);
            Assert.That(assessed.accepted, Is.True, assessed.reason);
            files.Store.Save(assessed.state);
            Assert.That(File.ReadAllBytes(files.Store.BackupPath), Is.EqualTo(pendingBytes));
            var loaded = LoadUnchanged(files.Store, assessed.state); var card = loaded.replyCards.Single();
            Assert.That(HoHPitches.Assessed(loaded, card), Is.True);
            Assert.That(HoHPitches.Assessment(loaded, card), Is.EqualTo(HoHPitches.Assessment(assessed.state, assessed.state.replyCards.Single())));
            Assert.That(loaded.randomState, Is.EqualTo(pending.randomState));
            Assert.That(loaded.windowActions, Is.EqualTo(pending.windowActions));
            Assert.That(EpisodeEngine.ReadThisWeek(loaded, card.fromId), Is.False);
            Assert.That(loaded.ledger.standings.Last().source, Is.EqualTo(ClaimSource.Told));
            var engine = new EpisodeEngine(loaded);
            Assert.That(engine.Apply(command).duplicate, Is.True, "The actual saved command receipt also survives.");
            Equal(loaded, engine.Snapshot);
            RejectRepeatedInspection(files.Store, loaded);
        }

        [TestCase(false)] [TestCase(true)]
        public void SafetyAnswerReloadsWithoutDuplicationAndTheRealNominationStillBreaksIt(bool alreadyPromised)
        {
            using var files = new Files(); var pending = Pending(); string speaker = pending.replyCards[0].fromId;
            if (alreadyPromised) pending.promises.Add(new PromiseState { id = "pitch-stored-prior-safety", fromId = pending.playerId,
                toId = speaker, kind = PromiseKind.Safety, status = PromiseStatus.Active, week = pending.week, expiresWeek = pending.week + 1 });
            files.Store.Save(pending);
            var before = LoadUnchanged(files.Store, pending);
            var answered = Answer(before, "promise-safety"); files.Store.Save(answered);
            var loaded = LoadUnchanged(files.Store, answered);
            Assert.That(loaded.replyCards, Is.Empty); Assert.That(loaded.promises, Has.Count.EqualTo(1));
            var promise = loaded.promises.Single();
            Assert.That((promise.fromId, promise.toId, promise.kind, promise.status),
                Is.EqualTo((loaded.playerId, speaker, PromiseKind.Safety, PromiseStatus.Active)));
            if (alreadyPromised) Assert.That(promise.id, Is.EqualTo("pitch-stored-prior-safety"));
            Assert.That(loaded.ledger.replies.Single().promised, Is.False, "The vote-only flag never changes meaning.");
            string second = loaded.Active.First(c => !c.isPlayer && c.id != speaker).id;
            var nominated = Apply(loaded, EpisodeCommandKind.Nominate, speaker, second: second);
            byte[] answeredBytes = File.ReadAllBytes(files.Store.SavePath);
            files.Store.Save(nominated); Assert.That(File.ReadAllBytes(files.Store.BackupPath), Is.EqualTo(answeredBytes));
            var broken = LoadUnchanged(files.Store, nominated).promises.Single();
            Assert.That((broken.id, broken.status, broken.brokenById, broken.settledWeek),
                Is.EqualTo((promise.id, PromiseStatus.Broken, loaded.playerId, loaded.week)));
        }

        [TestCase(8)] [TestCase(16)]
        public void ActualNominationsDiscardIgnoredCardsButKeepTheAssessmentHistoryOnDisk(int size)
        {
            using var files = new Files(); var pending = Pending(size);
            foreach (var guest in pending.Active.Where(c => !c.isPlayer).Skip(1).ToList())
                NpcSocialActions.Court(pending, guest, pending.Find(pending.playerId));
            Assert.That(pending.replyCards.Count, Is.EqualTo(size - 1));
            var assessed = Answer(pending, HoHPitches.FeelOutKey); files.Store.Save(assessed);
            var loaded = LoadUnchanged(files.Store, assessed);
            string[] nominees = loaded.Active.Where(c => !c.isPlayer).Take(2).Select(c => c.id).ToArray();
            var named = Apply(loaded, EpisodeCommandKind.Nominate, nominees[0], second: nominees[1]);
            files.Store.Save(named); var again = LoadUnchanged(files.Store, named);
            Assert.That(again.replyCards, Is.Empty); Assert.That(again.ledger.replies, Has.Count.EqualTo(1));
            Assert.That(again.ledger.replies.Single().replyKey, Is.EqualTo(HoHPitches.FeelOutKey));
            Assert.That(again.promises, Is.Empty, "Ignoring a pitch creates no answer or promise.");
            var result = new EpisodeEngine(again).Apply(Command(again, EpisodeCommandKind.ReplyToHouseguest, loaded.replyCards[0].id, "hear"));
            Assert.That(result.accepted, Is.False); Equal(again, result.state);
            LoadUnchanged(files.Store, again);
        }

        private static EpisodeState Invalid(EpisodeState s, string defect)
        {
            var bad = s.Clone();
            switch (defect)
            {
                case "card-phase": bad.phase = EpisodePhase.Social; break;
                case "card-speaker": bad.replyCards[0].fromId = "missing-speaker"; break;
                case "card-subject": bad.replyCards[0].aboutId = bad.replyCards[0].fromId; break;
                case "card-duplicate":
                    var duplicate = bad.replyCards[0].Clone(); duplicate.id += "-duplicate"; bad.replyCards.Add(duplicate); break;
                case "receipt-vote": bad.ledger.replies[0].promised = true; break;
                case "receipt-impact": bad.ledger.replies[0].toThem = 1; break;
                case "receipt-duplicate":
                    var receipt = bad.ledger.replies[0].Clone(); receipt.cardId += "-duplicate"; bad.ledger.replies.Add(receipt); break;
                default: throw new ArgumentException("Unknown test defect", nameof(defect));
            }
            return bad;
        }

        [TestCase("card-phase")] [TestCase("card-speaker")] [TestCase("card-subject")] [TestCase("card-duplicate")]
        [TestCase("receipt-vote")] [TestCase("receipt-impact")] [TestCase("receipt-duplicate")]
        public void MalformedCandidateCannotOverwriteEitherGoodSaveCopy(string defect)
        {
            using var files = new Files(); var pending = Pending(); files.Store.Save(pending);
            var assessed = Answer(pending, HoHPitches.FeelOutKey); files.Store.Save(assessed);
            byte[] primary = File.ReadAllBytes(files.Store.SavePath), backup = File.ReadAllBytes(files.Store.BackupPath);
            var invalid = Invalid(assessed, defect);
            Assert.Throws<InvalidDataException>(() => files.Store.Save(invalid));
            Assert.That(File.ReadAllBytes(files.Store.SavePath), Is.EqualTo(primary));
            Assert.That(File.ReadAllBytes(files.Store.BackupPath), Is.EqualTo(backup));
            LoadUnchanged(files.Store, assessed);
            Assert.That(Directory.GetFiles(files.DirectoryPath, "*.pending-*"), Is.Empty);
        }

        [TestCase("card-phase")] [TestCase("card-subject")] [TestCase("receipt-vote")] [TestCase("receipt-duplicate")]
        public void ChecksumValidButMalformedDiskDataIsRefusedWithoutImplicitRecoveryOrOverwrite(string defect)
        {
            using var files = new Files(); var pending = Pending(); files.Store.Save(pending);
            var assessed = Answer(pending, HoHPitches.FeelOutKey); files.Store.Save(assessed);
            byte[] backup = File.ReadAllBytes(files.Store.BackupPath);
            // Synthetic malformed data receives an independently computed checksum, so rejection
            // must reach schema/rule validation rather than simply detecting changed bytes.
            File.WriteAllText(files.Store.SavePath, PersistenceMigrationTests.Envelope(Payload(Invalid(assessed, defect))));
            byte[] badBytes = File.ReadAllBytes(files.Store.SavePath);
            Assert.That(files.Store.TryLoad(out var loaded, out string message), Is.False);
            Assert.That(loaded, Is.Null); Assert.That(message, Does.Not.Contain("checksum"));
            Assert.That(message, Does.Contain("Recover backup"));
            Assert.Throws<InvalidDataException>(() => files.Store.Save(assessed));
            Assert.That(File.ReadAllBytes(files.Store.SavePath), Is.EqualTo(badBytes));
            Assert.That(File.ReadAllBytes(files.Store.BackupPath), Is.EqualTo(backup));
            Assert.That(Directory.GetFiles(files.DirectoryPath, "*.before-recovery-*"), Is.Empty, "Only explicit recovery may replace the primary.");
        }

        [Test]
        public void ExplicitValidatedBackupRecoveryPreservesTheInspectionMarkerAndRetainsTheDamagedPrimary()
        {
            using var files = new Files(); var pending = Pending(); files.Store.Save(pending);
            var assessed = Answer(pending, HoHPitches.FeelOutKey); files.Store.Save(assessed);
            byte[] assessedBytes = File.ReadAllBytes(files.Store.SavePath);
            var answered = Answer(assessed, "hear"); files.Store.Save(answered);
            Assert.That(File.ReadAllBytes(files.Store.BackupPath), Is.EqualTo(assessedBytes));
            const string damaged = "Synthetic interrupted E4 save; only this isolated temporary primary is damaged.";
            File.WriteAllText(files.Store.SavePath, damaged); byte[] damagedBytes = File.ReadAllBytes(files.Store.SavePath);
            Assert.That(files.Store.TryLoad(out _, out string message), Is.False);
            Assert.That(message, Does.Contain("Recover backup"));
            Assert.Throws<InvalidDataException>(() => files.Store.Save(answered));
            Assert.That(File.ReadAllBytes(files.Store.SavePath), Is.EqualTo(damagedBytes));
            Assert.That(files.Store.TryRecoverBackup(out var recovered, out message), Is.True, message);
            Equal(assessed, recovered);
            Assert.That(File.ReadAllBytes(files.Store.SavePath), Is.EqualTo(assessedBytes));
            Assert.That(File.ReadAllBytes(files.Store.BackupPath), Is.EqualTo(assessedBytes));
            var retained = Directory.GetFiles(files.DirectoryPath, "*.before-recovery-*.json");
            Assert.That(retained, Has.Length.EqualTo(1));
            Assert.That(File.ReadAllBytes(retained[0]), Is.EqualTo(damagedBytes));
            var loaded = LoadUnchanged(files.Store, assessed);
            Assert.That(HoHPitches.Assessed(loaded, loaded.replyCards.Single()), Is.True);
            RejectRepeatedInspection(files.Store, loaded);
            files.Store.Save(Answer(loaded, "hear"));
            Assert.That(File.ReadAllBytes(retained[0]), Is.EqualTo(damagedBytes));
            Assert.That(File.ReadAllBytes(files.Store.BackupPath), Is.EqualTo(assessedBytes));
        }

        [Test]
        public void FrozenV22RejectsAForgedPitchKindWithoutWideningTheHistoricalReplyVocabulary()
        {
            string fixture = Path.Combine(UnityEngine.Application.dataPath, "Gamesim", "Tests", "EditMode", "Fixtures", "V22MonoOpeningSave.json");
            byte[] retainedOriginal = File.ReadAllBytes(fixture);
            var old = (JObject)JObject.Parse(File.ReadAllText(fixture))["state"];
            Assert.That((int)old["schemaVersion"], Is.EqualTo(22));
            Assert.That((int)old["phase"], Is.EqualTo((int)EpisodePhase.Social));
            string speaker = (string)((JArray)old["contestants"]).First(c => !(bool)c["isPlayer"])["id"];
            old["strategyRulesStartWeek"] = 1;
            old["replyCards"] = new JArray(JObject.FromObject(new ReplyCardState { id = "synthetic-v22-reply",
                week = (int)old["week"], kind = ReplyCards.Confrontation, fromId = speaker }, Serializer()));
            // First prove that the exact old shape/phase with a historical card kind is accepted.
            var control = EpisodeSaveMigrations.PrepareCurrentPayload(old, out bool migrated);
            Assert.That(migrated, Is.True);
            EpisodeSaveValidation.Validate(control.ToObject<EpisodeState>(Serializer()));
            using var files = new Files();
            File.WriteAllText(files.Store.SavePath, PersistenceMigrationTests.Envelope(old));
            Assert.That(files.Store.TryLoad(out var legacy, out string message), Is.True, message);
            Assert.That(legacy.economyRulesVersion, Is.Zero);
            old["replyCards"][0]["kind"] = ReplyCards.Pitch;
            Assert.Throws<InvalidDataException>(() => EpisodeSaveMigrations.PrepareCurrentPayload(old, out _));
            File.WriteAllText(files.Store.SavePath, PersistenceMigrationTests.Envelope(old));
            byte[] forged = File.ReadAllBytes(files.Store.SavePath);
            Assert.That(files.Store.TryLoad(out var rejected, out message), Is.False);
            Assert.That(rejected, Is.Null); Assert.That(message, Does.Not.Contain("checksum"));
            Assert.That(File.ReadAllBytes(files.Store.SavePath), Is.EqualTo(forged));
            Assert.That(File.ReadAllBytes(fixture), Is.EqualTo(retainedOriginal), "The archived historical fixture is never rewritten or resealed.");
        }

        private sealed class Files : IDisposable
        {
            public readonly string DirectoryPath = Path.Combine(Path.GetTempPath(), "GamesimHoHPitch-" + Guid.NewGuid().ToString("N"));
            public readonly EpisodeSaveStore Store;
            public Files()
            {
                Directory.CreateDirectory(DirectoryPath);
                Store = new EpisodeSaveStore(Path.Combine(DirectoryPath, "episode.json"));
            }
            public void Dispose()
            {
                string resolved = Path.GetFullPath(DirectoryPath);
                string parent = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                Assert.That(Path.GetDirectoryName(resolved), Is.EqualTo(parent).IgnoreCase);
                Assert.That(Path.GetFileName(resolved), Does.StartWith("GamesimHoHPitch-"));
                if (Directory.Exists(resolved)) Directory.Delete(resolved, true);
            }
        }
    }
}
