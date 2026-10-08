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
    /// E5 against the production SaveStore and strict current/historical serializers. These are
    /// authored native Edit tests, not pure replay or standalone acceptance. Only unique temporary
    /// slots are written; the retained historical fixture is read without rewriting or resealing it.
    /// </summary>
    public sealed class BlockSpeechPersistenceTests
    {
        private static JsonSerializer Serializer() => (JsonSerializer)typeof(EpisodeSaveStore).Assembly
            .GetType("Gamesim.Persistence.SaveJson", true).GetMethod("Serializer", BindingFlags.Public | BindingFlags.Static).Invoke(null, null);
        private static JObject Payload(EpisodeState s) => JObject.FromObject(s, Serializer());
        private static void Equal(EpisodeState expected, EpisodeState actual) =>
            Assert.That(JToken.DeepEquals(Payload(expected), Payload(actual)), Is.True, "All persisted fields must survive, not only the visible words.");

        private static EpisodeState Night(bool playerOnBlock = true)
        {
            var s = EconomyRulesTests.Fresh();
            s.strategyRulesStartWeek = 1; s.agencyRulesStartWeek = 1;
            EpisodeEngine.EnableRead(s); EpisodeEngine.EnableCommitments(s);
            var npcs = s.Active.Where(c => !c.isPlayer).Select(c => c.id).ToArray();
            s.phase = EpisodePhase.Eviction; s.evictionStage = EvictionStage.Speeches;
            s.hohId = npcs[1]; s.vetoHolderId = s.hohId; s.vetoResolved = true;
            s.vetoPlayers = s.Active.Take(EpisodeEngine.VetoPlayerCount(s.Active.Count())).Select(c => c.id).ToList();
            s.nominees = new List<string> { playerOnBlock ? s.playerId : npcs[2], npcs[0] };
            EpisodeSaveValidation.Validate(s);
            return s;
        }

        private static EpisodeCommand Command(EpisodeState s, EpisodeCommandKind kind, string text = null,
            string approach = null, string target = null) => new EpisodeCommand
        {
            id = "speech-store-" + s.revision + "-" + kind, actorId = s.playerId,
            expectedRevision = s.revision, expectedPhase = s.phase, kind = kind,
            text = text, secondTargetId = approach, targetId = target,
        };

        private static EpisodeState Apply(EpisodeState s, EpisodeCommandKind kind, string text = null,
            string approach = null, string target = null)
        {
            var result = new EpisodeEngine(s).Apply(Command(s, kind, text, approach, target));
            Assert.That(result.accepted, Is.True, result.reason);
            EpisodeSaveValidation.Validate(result.state);
            return result.state;
        }

        private static EpisodeState Delivered(EpisodeState s = null, bool quiet = false) =>
            Apply(s ?? Night(), EpisodeCommandKind.SubmitEvictionSpeech,
                quiet ? "" : "Please hear my case. I want another week with this house.",
                quiet ? BlockSpeeches.Quiet : LobbyApproach.Emotional);

        private static EpisodeState LoadUnchanged(EpisodeSaveStore store, EpisodeState expected)
        {
            byte[] original = File.ReadAllBytes(store.SavePath);
            var freshReader = new EpisodeSaveStore(store.SavePath);
            Assert.That(freshReader.TryLoad(out var loaded, out string message), Is.True, message);
            Equal(expected, loaded);
            Assert.That(loaded.schemaVersion, Is.EqualTo(26));
            Assert.That(File.ReadAllBytes(store.SavePath), Is.EqualTo(original), "Loading never rewrites current save bytes.");
            return loaded;
        }

        private static EpisodeEvent PlayerReceipt(EpisodeState s) =>
            BlockSpeeches.Receipt(s, s.evictionSpeeches.Single(x => x.speakerId == s.playerId));

        private static void RejectSecondDelivery(EpisodeSaveStore store, EpisodeState loaded)
        {
            byte[] original = File.ReadAllBytes(store.SavePath);
            var engine = new EpisodeEngine(loaded);
            var second = engine.Apply(Command(loaded, EpisodeCommandKind.SubmitEvictionSpeech,
                "A second, different speech is not authorized.", LobbyApproach.Pressure));
            Assert.That(second.accepted, Is.False); Assert.That(second.duplicate, Is.False);
            Equal(loaded, second.state); Equal(loaded, engine.Snapshot);
            Assert.That(File.ReadAllBytes(store.SavePath), Is.EqualTo(original));
        }

        [TestCase(LobbyApproach.Emotional)] [TestCase(LobbyApproach.Strategic)]
        [TestCase(LobbyApproach.Deal)] [TestCase(LobbyApproach.Pressure)]
        public void FullSpeechApproachAndOrderedPublicAudienceRoundTripWithoutChangingTheDto(string approach)
        {
            using var files = new Files(); var pending = Night(); files.Store.Save(pending);
            byte[] pendingBytes = File.ReadAllBytes(files.Store.SavePath);
            string words = "  Please hear me out.\r\nI have my own words, not a keyword instruction.\n" + new string('x', 1750) + "  ";
            var command = Command(pending, EpisodeCommandKind.SubmitEvictionSpeech, words, approach);
            var result = new EpisodeEngine(pending).Apply(command);
            Assert.That(result.accepted, Is.True, result.reason);
            files.Store.Save(result.state);
            Assert.That(File.ReadAllBytes(files.Store.BackupPath), Is.EqualTo(pendingBytes));
            var loaded = LoadUnchanged(files.Store, result.state);
            var speech = loaded.evictionSpeeches.Single(); var receipt = PlayerReceipt(loaded);
            Assert.That(speech.text, Is.EqualTo(words.Trim()));
            Assert.That(receipt.text, Is.EqualTo(speech.text));
            Assert.That(receipt.kind, Is.EqualTo(BlockSpeeches.EventPrefix + approach));
            Assert.That(receipt.audienceIds, Is.EqualTo(BlockSpeeches.Audience(pending, pending.playerId)));
            Assert.That(receipt.audienceIds[0], Is.EqualTo(pending.playerId));
            Assert.That(loaded.nextSequence, Is.EqualTo(pending.nextSequence + 1), "One receipt replaces one legacy log; it does not add an extra event.");
            Assert.That(loaded.randomState, Is.EqualTo(pending.randomState));
            var saved = (JObject)JObject.Parse(File.ReadAllText(files.Store.SavePath))["state"];
            Assert.That(((JObject)saved["evictionSpeeches"][0]).Properties().Select(p => p.Name),
                Is.EquivalentTo(new[] { "speakerId", "text", "isPlayerAuthored", "week" }));
            Assert.That(((JObject)saved["events"].Last()).Properties().Select(p => p.Name),
                Is.EquivalentTo(new[] { "sequence", "week", "phase", "kind", "text", "audienceIds" }));
            var engine = new EpisodeEngine(loaded);
            Assert.That(engine.Apply(command).duplicate, Is.True); Equal(loaded, engine.Snapshot);
            RejectSecondDelivery(files.Store, loaded);
        }

        [Test]
        public void QuietDeliveryReloadsAsAnExplicitOnceOnlyZeroInfluenceChoice()
        {
            using var files = new Files(); var pending = Night(); var quiet = Delivered(pending, true);
            files.Store.Save(quiet); var loaded = LoadUnchanged(files.Store, quiet);
            var speech = loaded.evictionSpeeches.Single(); var receipt = PlayerReceipt(loaded);
            Assert.That(speech.text, Is.Empty);
            Assert.That(receipt.kind, Is.EqualTo(BlockSpeeches.EventPrefix + BlockSpeeches.Quiet));
            Assert.That(receipt.text, Is.EqualTo("No speech was given."));
            foreach (var voter in EpisodeEngine.Voters(loaded))
            {
                var evaluation = EpisodeEngine.ProjectBallot(loaded, voter.id);
                Assert.That(evaluation.nomineeEvaluations.SelectMany(n => n.factors)
                    .Where(f => f.code == BlockSpeeches.FactorCode).All(f => f.value == 0), Is.True);
            }
            RejectSecondDelivery(files.Store, loaded);
        }

        private static EpisodeState Invalid(EpisodeState source, string defect)
        {
            var s = source.Clone(); var receipt = PlayerReceipt(s);
            switch (defect)
            {
                case "approach": receipt.kind = BlockSpeeches.EventPrefix + "invented"; break;
                case "speaker":
                    string first = receipt.audienceIds[0]; receipt.audienceIds[0] = receipt.audienceIds[1]; receipt.audienceIds[1] = first; break;
                case "text": receipt.text += " Changed after delivery."; break;
                case "audience-duplicate": receipt.audienceIds.Add(receipt.audienceIds[1]); break;
                case "audience-missing": receipt.audienceIds.RemoveAt(receipt.audienceIds.Count - 1); break;
                case "audience-order":
                    string other = receipt.audienceIds[1]; receipt.audienceIds[1] = receipt.audienceIds[2]; receipt.audienceIds[2] = other; break;
                case "duplicate":
                    var extra = receipt.Clone(); extra.sequence = s.nextSequence++; s.events.Add(extra); break;
                case "receipt-phase": receipt.phase = EpisodePhase.Campaign; break;
                case "campaign-stage": s.phase = EpisodePhase.Campaign; s.evictionStage = EvictionStage.Interaction; break;
                case "before-speeches": s.evictionStage = EvictionStage.Interaction; break;
                case "week-rules-off": s.weekRulesStartWeek = 0; break;
                case "economy-off": s.economyRulesVersion = 0; break;
                case "quiet-forgery": receipt.kind = BlockSpeeches.EventPrefix + BlockSpeeches.Quiet; receipt.text = "No speech was given."; break;
                default: throw new ArgumentException("Unknown isolated corruption", nameof(defect));
            }
            return s;
        }

        [TestCase("approach")] [TestCase("speaker")] [TestCase("text")] [TestCase("audience-duplicate")]
        [TestCase("audience-missing")] [TestCase("audience-order")] [TestCase("duplicate")] [TestCase("receipt-phase")]
        [TestCase("campaign-stage")] [TestCase("week-rules-off")] [TestCase("economy-off")] [TestCase("quiet-forgery")]
        [TestCase("before-speeches")]
        public void ForgedReceiptCandidateCannotOverwriteEitherValidatedSaveCopy(string defect)
        {
            using var files = new Files(); var pending = Night(); files.Store.Save(pending);
            var delivered = Delivered(pending); files.Store.Save(delivered);
            byte[] primary = File.ReadAllBytes(files.Store.SavePath), backup = File.ReadAllBytes(files.Store.BackupPath);
            Assert.Throws<InvalidDataException>(() => files.Store.Save(Invalid(delivered, defect)));
            Assert.That(File.ReadAllBytes(files.Store.SavePath), Is.EqualTo(primary));
            Assert.That(File.ReadAllBytes(files.Store.BackupPath), Is.EqualTo(backup));
            LoadUnchanged(files.Store, delivered);
            Assert.That(Directory.GetFiles(files.DirectoryPath, "*.pending-*"), Is.Empty);
        }

        [TestCase("text")] [TestCase("audience-duplicate")] [TestCase("duplicate")]
        [TestCase("campaign-stage")] [TestCase("week-rules-off")] [TestCase("quiet-forgery")]
        [TestCase("before-speeches")]
        public void ChecksumValidForgedDiskReceiptIsRefusedWithoutImplicitRecovery(string defect)
        {
            using var files = new Files(); var pending = Night(); files.Store.Save(pending);
            var delivered = Delivered(pending); files.Store.Save(delivered);
            byte[] backup = File.ReadAllBytes(files.Store.BackupPath);
            File.WriteAllText(files.Store.SavePath, PersistenceMigrationTests.Envelope(Payload(Invalid(delivered, defect))));
            byte[] malformed = File.ReadAllBytes(files.Store.SavePath);
            Assert.That(files.Store.TryLoad(out var rejected, out string message), Is.False);
            Assert.That(rejected, Is.Null); Assert.That(message, Does.Not.Contain("checksum"));
            Assert.That(message, Does.Contain("Recover backup"));
            Assert.Throws<InvalidDataException>(() => files.Store.Save(delivered));
            Assert.That(File.ReadAllBytes(files.Store.SavePath), Is.EqualTo(malformed));
            Assert.That(File.ReadAllBytes(files.Store.BackupPath), Is.EqualTo(backup));
            Assert.That(Directory.GetFiles(files.DirectoryPath, "*.before-recovery-*"), Is.Empty);
        }

        [Test]
        public void ExplicitRecoveryRestoresExactSpeechReceiptAndArchivesTheDamagedPrimary()
        {
            using var files = new Files(); var delivered = Delivered(); files.Store.Save(delivered);
            byte[] original = File.ReadAllBytes(files.Store.SavePath);
            var voting = Apply(delivered, EpisodeCommandKind.Advance); files.Store.Save(voting);
            Assert.That(File.ReadAllBytes(files.Store.BackupPath), Is.EqualTo(original));
            File.WriteAllText(files.Store.SavePath, "Synthetic interrupted E5 save in this unique test slot only.");
            byte[] damaged = File.ReadAllBytes(files.Store.SavePath);
            Assert.That(files.Store.TryLoad(out _, out string message), Is.False);
            Assert.That(message, Does.Contain("Recover backup"));
            Assert.Throws<InvalidDataException>(() => files.Store.Save(voting));
            Assert.That(files.Store.TryRecoverBackup(out var recovered, out message), Is.True, message);
            Equal(delivered, recovered);
            Assert.That(File.ReadAllBytes(files.Store.SavePath), Is.EqualTo(original));
            Assert.That(File.ReadAllBytes(files.Store.BackupPath), Is.EqualTo(original));
            string[] retained = Directory.GetFiles(files.DirectoryPath, "*.before-recovery-*.json");
            Assert.That(retained, Has.Length.EqualTo(1));
            Assert.That(File.ReadAllBytes(retained[0]), Is.EqualTo(damaged));
            var loaded = LoadUnchanged(files.Store, delivered); RejectSecondDelivery(files.Store, loaded);
            files.Store.Save(Apply(loaded, EpisodeCommandKind.Advance));
            Assert.That(File.ReadAllBytes(retained[0]), Is.EqualTo(damaged));
            Assert.That(File.ReadAllBytes(files.Store.BackupPath), Is.EqualTo(original));
        }

        [TestCase(false)] [TestCase(true)]
        public void RealResultsSocialAndNextWeekTransitionsKeepHistoricWordsLoadableButNoLongerAuthoritative(bool quiet)
        {
            using var files = new Files(); var spoken = Delivered(quiet: quiet);
            var receipt = JObject.FromObject(PlayerReceipt(spoken), Serializer());
            int receiptSequence = PlayerReceipt(spoken).sequence, week = spoken.week;
            var voting = Apply(spoken, EpisodeCommandKind.Advance);
            var results = Apply(voting, EpisodeCommandKind.Advance);
            Assert.That(results.evictionStage, Is.EqualTo(EvictionStage.Results));
            Assert.That(results.evictionResolved, Is.True);
            files.Store.Save(results); var loaded = LoadUnchanged(files.Store, results);
            var social = Apply(loaded, EpisodeCommandKind.Advance);
            Assert.That(social.phase, Is.EqualTo(EpisodePhase.Social)); Assert.That(social.week, Is.EqualTo(week));
            files.Store.Save(social); social = LoadUnchanged(files.Store, social);
            if (social.pendingDiary != null) social = Apply(social, EpisodeCommandKind.SkipDiary, target: social.pendingDiary.id);
            var next = Apply(social, EpisodeCommandKind.Advance);
            Assert.That(next.week, Is.EqualTo(week + 1)); Assert.That(next.evictionSpeeches, Is.Empty);
            files.Store.Save(next); next = LoadUnchanged(files.Store, next);
            var historic = next.events.Single(e => e.sequence == receiptSequence);
            Assert.That(JToken.DeepEquals(receipt, JObject.FromObject(historic, Serializer())), Is.True);
            Assert.That(BlockSpeeches.ProtectedReceipt(next, historic), Is.False);
            Assert.That(next.events.Where(e => BlockSpeeches.IsReceiptKind(e.kind)).All(e => e.week < next.week), Is.True);
        }

        [Test]
        public void FullHistoryRoundTripPinsBothCurrentReceiptsWhileOrdinaryEntriesContinueFifo()
        {
            using var files = new Files(); var spoken = Delivered(); var held = PlayerReceipt(spoken).Clone();
            spoken.events.RemoveAll(e => !BlockSpeeches.IsReceiptKind(e.kind));
            while (spoken.events.Count < 256)
                spoken.events.Add(new EpisodeEvent { sequence = spoken.nextSequence++, week = spoken.week,
                    phase = spoken.phase, kind = "fixture-history", text = "An ordinary bounded history entry." });
            int firstOrdinary = spoken.events[1].sequence;
            files.Store.Save(spoken); var loaded = LoadUnchanged(files.Store, spoken);
            var voting = Apply(loaded, EpisodeCommandKind.Advance); files.Store.Save(voting);
            voting = LoadUnchanged(files.Store, voting);
            Assert.That(voting.events, Has.Count.EqualTo(256));
            Assert.That(voting.events.Count(e => BlockSpeeches.IsReceiptKind(e.kind)), Is.EqualTo(2));
            Assert.That(JToken.DeepEquals(JObject.FromObject(held, Serializer()), JObject.FromObject(PlayerReceipt(voting), Serializer())), Is.True);
            Assert.That(voting.events.Any(e => e.sequence == firstOrdinary), Is.False);
            Assert.That(voting.events.Select(e => e.sequence), Is.Ordered);
        }

        [Test]
        public void ReceiptLessSchema23PendingVoteNeverInfersAnApproachFromOldSpeechText()
        {
            using var files = new Files(); var old = Apply(Delivered(), EpisodeCommandKind.Advance);
            foreach (var e in old.events.Where(e => BlockSpeeches.IsReceiptKind(e.kind)))
            {
                string speaker = e.audienceIds[0];
                e.kind = "eviction-speech";
                e.text = speaker == old.playerId ? "You addressed the house from the block." : old.Find(speaker).name + ": " + e.text;
                e.audienceIds.Clear();
            }
            var historical23 = PersistenceMigrationTests.StripSchema24(Payload(old));
            historical23["schemaVersion"] = 23;
            File.WriteAllText(files.Store.SavePath, PersistenceMigrationTests.Envelope(historical23));
            var loaded = LoadUnchanged(files.Store, old); var before = loaded.Clone();
            Assert.That(loaded.unifiedCommitmentRulesVersion, Is.Zero);
            Assert.That(loaded.unifiedCommitments, Is.Empty);
            foreach (var voter in EpisodeEngine.Voters(loaded))
            {
                var options = WebEvictionVoting.FromNative(loaded, voter.id);
                Assert.That(options.speechAppeals, Is.Empty);
                var withPlaceholder = WebEvictionVoting.Evaluate(options);
                options.nativeSpeechRulesOn = false;
                var without = WebEvictionVoting.Evaluate(options);
                Assert.That(withPlaceholder.selectedNomineeId, Is.EqualTo(without.selectedNomineeId));
                Assert.That(withPlaceholder.margin, Is.EqualTo(without.margin));
                Assert.That(withPlaceholder.nomineeEvaluations.SelectMany(n => n.factors)
                    .Where(f => f.code == BlockSpeeches.FactorCode).All(f => f.value == 0), Is.True);
            }
            Equal(before, loaded);
            var results = Apply(loaded, EpisodeCommandKind.Advance); files.Store.Save(results);
            results = LoadUnchanged(files.Store, results);
            Assert.That(results.evictionResolved, Is.True);
            Assert.That(results.events.Any(e => BlockSpeeches.IsReceiptKind(e.kind)), Is.False, "A load or vote never backfills authority from prose.");
        }

        [Test]
        public void SavedNpcBallotsRemainExactAcrossPrivateProjectionAndTheLaterRealReveal()
        {
            using var files = new Files(); var voting = Apply(Night(false), EpisodeCommandKind.Advance);
            var awaitingPlayer = Apply(voting, EpisodeCommandKind.Advance);
            Assert.That(awaitingPlayer.evictionResolved, Is.False);
            Assert.That(awaitingPlayer.votes.Count, Is.GreaterThan(0));
            files.Store.Save(awaitingPlayer); var loaded = LoadUnchanged(files.Store, awaitingPlayer);
            var before = loaded.Clone(); byte[] bytes = File.ReadAllBytes(files.Store.SavePath);
            var ballots = loaded.votes.Select(v => v.Clone()).ToArray();
            foreach (var ballot in ballots)
            {
                var evaluation = EpisodeEngine.ProjectBallot(loaded, ballot.voterId);
                foreach (var factor in evaluation.nomineeEvaluations.SelectMany(n => n.factors).Where(f => f.code == BlockSpeeches.FactorCode))
                    Assert.That(VoteRead.FactorKnown(loaded, ballot.voterId, loaded.nominees[0], factor), Is.False);
            }
            Equal(before, loaded); Assert.That(File.ReadAllBytes(files.Store.SavePath), Is.EqualTo(bytes));
            var cast = Apply(loaded, EpisodeCommandKind.CastVote, target: loaded.nominees[0]);
            var results = Apply(cast, EpisodeCommandKind.Advance);
            Assert.That(results.evictionResolved, Is.True);
            files.Store.Save(results); results = LoadUnchanged(files.Store, results);
            foreach (var ballot in ballots)
            {
                var retained = results.votes.Single(v => v.voterId == ballot.voterId);
                Assert.That((retained.targetId, retained.reason), Is.EqualTo((ballot.targetId, ballot.reason)));
            }
        }

        [Test]
        public void SyntheticV22ReservedReceiptCannotGainAuthorityThroughUnchangedHistoricalMigration()
        {
            string fixture = Path.Combine(UnityEngine.Application.dataPath, "Gamesim", "Tests", "EditMode", "Fixtures", "V22MonoOpeningSave.json");
            byte[] retainedOriginal = File.ReadAllBytes(fixture);
            var old = (JObject)JObject.Parse(File.ReadAllText(fixture))["state"];
            Assert.That((int)old["schemaVersion"], Is.EqualTo(22));
            string player = (string)old["playerId"];
            var audience = new JArray(new[] { player }.Concat(((JArray)old["contestants"]).Select(c => (string)c["id"]).Where(id => id != player)));
            var line = new JObject { ["sequence"] = (int)old["nextSequence"], ["week"] = (int)old["week"],
                ["phase"] = (int)EpisodePhase.Eviction, ["kind"] = "eviction-speech",
                ["text"] = "A synthetic historical public line, never a real archived edit.", ["audienceIds"] = audience };
            old["nextSequence"] = (int)old["nextSequence"] + 1; ((JArray)old["events"]).Add(line);
            using var files = new Files();
            File.WriteAllText(files.Store.SavePath, PersistenceMigrationTests.Envelope(old));
            Assert.That(files.Store.TryLoad(out var control, out string message), Is.True, message);
            Assert.That(control.economyRulesVersion, Is.Zero);
            line["kind"] = BlockSpeeches.EventPrefix + LobbyApproach.Emotional;
            // Historical event kinds were open text. The frozen contract must remain unchanged;
            // The frozen v22-to-v23 chain still accepts its open prose shape. Frozen23 now
            // refuses that reserved authority before the inactive schema24 defaults are added.
            var migrated = EpisodeSaveMigrations.PrepareV23Payload(old, out bool changed);
            Assert.That(changed, Is.True); Assert.That((int)migrated["economyRulesVersion"], Is.Zero);
            Assert.That((int)migrated["schemaVersion"], Is.EqualTo(23));
            Assert.Throws<InvalidDataException>(() => EpisodeSaveMigrations.UpgradeV23ToV24(migrated));
            Assert.Throws<InvalidDataException>(() => EpisodeSaveMigrations.PrepareCurrentPayload(old, out _));
            File.WriteAllText(files.Store.SavePath, PersistenceMigrationTests.Envelope(old));
            byte[] forged = File.ReadAllBytes(files.Store.SavePath);
            Assert.That(files.Store.TryLoad(out var rejected, out message), Is.False);
            Assert.That(rejected, Is.Null); Assert.That(message, Does.Not.Contain("checksum"));
            Assert.That(File.ReadAllBytes(files.Store.SavePath), Is.EqualTo(forged));
            Assert.That(File.ReadAllBytes(fixture), Is.EqualTo(retainedOriginal), "No archived fixture bytes are rewritten or resealed.");
        }

        private sealed class Files : IDisposable
        {
            public readonly string DirectoryPath = Path.Combine(Path.GetTempPath(), "GamesimBlockSpeech-" + Guid.NewGuid().ToString("N"));
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
                Assert.That(Path.GetFileName(resolved), Does.StartWith("GamesimBlockSpeech-"));
                if (Directory.Exists(resolved)) Directory.Delete(resolved, true);
            }
        }
    }
}
