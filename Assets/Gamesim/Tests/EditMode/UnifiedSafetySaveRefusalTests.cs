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
    /// Native SaveStore classification and preservation. Good legacy copies progress through public
    /// commands with canonical/hearing rules off. Complete active source-owner snapshots can round
    /// trip, while disabled-record corruption and explicit hearing/settlement corruption remain refused.
    /// Internal source owners are not evidence of a publicly played enabled command or HoH win.
    /// Every filesystem target is a guarded private slot. No retained fixture is changed.
    /// </summary>
    public sealed class UnifiedSafetySaveRefusalTests
    {
        [TestCase("empty-one")]
        [TestCase("promise")]
        [TestCase("deal")]
        [TestCase("hearing-missing-initial")]
        [TestCase("broken-promise-attribution")]
        [TestCase("disabled-promise")]
        [TestCase("disabled-deal")]
        [TestCase("disabled-evidence")]
        [TestCase("disabled-receipt")]
        public void CandidateSaveAcceptsCompleteSourcesAndPreservesCopiesWhenRefused(string sample)
        {
            using var files = new Files();
            var good = SeedTwoCopies(files);
            string unchangedGood = Json(good);
            var candidate = Prospective(good, sample);
            string unchangedCandidate = Json(candidate);
            var retained = files.Image();

            if (SupportedSourceSnapshot(sample))
            {
                byte[] previous = File.ReadAllBytes(files.Store.SavePath);
                files.Store.Save(candidate);
                Assert.That(File.ReadAllBytes(files.Store.BackupPath), Is.EqualTo(previous));
                Assert.That(files.Store.TryLoad(out var loaded, out string message), Is.True, message);
                EquivalentSupported(candidate, loaded);
                Assert.That(Json(candidate), Is.EqualTo(unchangedCandidate));
                Assert.That(Json(good), Is.EqualTo(unchangedGood));
                Assert.That(Directory.GetFiles(files.Root, "*.pending-*"), Is.Empty);
                return;
            }

            for (int attempt = 0; attempt < 2; attempt++)
            {
                Assert.Throws<InvalidDataException>(() => files.Store.Save(candidate), sample);
                files.AssertImage(retained);
                Assert.That(Json(candidate), Is.EqualTo(unchangedCandidate), "Refusal cannot normalize or repair the candidate.");
                Assert.That(files.Store.TryLoad(out var loaded, out string message), Is.True, message);
                Equivalent(good, loaded);
                files.AssertImage(retained);
            }

            Assert.That(Json(good), Is.EqualTo(unchangedGood), "The source owners operated only on a detached prospective copy.");
            ContinueDisabled(files, good);
            Assert.That(Json(candidate), Is.EqualTo(unchangedCandidate));
        }

        [TestCase("empty-one")]
        [TestCase("promise")]
        [TestCase("deal")]
        [TestCase("hearing-missing-initial")]
        [TestCase("broken-promise-attribution")]
        [TestCase("disabled-promise")]
        [TestCase("disabled-deal")]
        [TestCase("disabled-evidence")]
        [TestCase("disabled-receipt")]
        public void ChecksummedPrimaryHonorsSupportedRulesWithoutRepairingInvalidSources(string sample)
        {
            using var files = new Files();
            var good = SeedTwoCopies(files);
            var candidate = Prospective(good, sample);
            string unchangedCandidate = Json(candidate);
            // This is a newly constructed current-schema test envelope, not a resealed golden.
            files.Write(files.Store.SavePath, candidate);
            var retained = files.Image();

            if (SupportedSourceSnapshot(sample))
            {
                for (int attempt = 0; attempt < 2; attempt++)
                {
                    Assert.That(files.Store.TryLoad(out var loaded, out string message), Is.True, message);
                    EquivalentSupported(candidate, loaded);
                    files.AssertImage(retained);
                }
                Assert.That(Json(candidate), Is.EqualTo(unchangedCandidate));
                return;
            }

            for (int attempt = 0; attempt < 2; attempt++)
            {
                Assert.That(files.Store.TryLoad(out var loaded, out string message), Is.False, sample);
                Assert.That(loaded, Is.Null, "A refused candidate must not be returned for publication.");
                Assert.That(message, Does.Not.Contain("checksum"), "The independent checksum is valid; this is current production refusal.");
                Assert.That(message, Does.Contain("Recover backup"), "The good backup is offered, never installed automatically.");
                Assert.Throws<InvalidDataException>(() => files.Store.Save(good), "A good candidate cannot silently overwrite an unreadable primary.");
                files.AssertImage(retained);
            }

            Assert.That(Json(candidate), Is.EqualTo(unchangedCandidate));
            byte[] refusedPrimary = File.ReadAllBytes(files.Store.SavePath);
            byte[] backup = File.ReadAllBytes(files.Store.BackupPath);
            Assert.That(files.Store.TryRecoverBackup(out var recovered, out string recovery), Is.True, recovery);
            AssertOff(recovered);
            Assert.That(File.ReadAllBytes(files.Store.SavePath), Is.EqualTo(backup));
            Assert.That(File.ReadAllBytes(files.Store.BackupPath), Is.EqualTo(backup));
            string archive = Directory.GetFiles(files.Root, "*.before-recovery-*.json").Single();
            Assert.That(File.ReadAllBytes(archive), Is.EqualTo(refusedPrimary));
            ContinueDisabled(files, recovered);
            Assert.That(File.ReadAllBytes(archive), Is.EqualTo(refusedPrimary));
            Assert.That(Json(candidate), Is.EqualTo(unchangedCandidate));
        }

        [TestCase("disabled-promise")]
        [TestCase("disabled-deal")]
        [TestCase("hearing-missing-initial")]
        public void InvalidSourceBackupCannotBeRecoveredOrArchiveTheGoodPrimary(string sample)
        {
            using var files = new Files();
            var good = SeedTwoCopies(files);
            var candidate = Prospective(good, sample);
            string unchangedCandidate = Json(candidate);
            files.Write(files.Store.BackupPath, candidate);
            var retained = files.Image();

            for (int attempt = 0; attempt < 2; attempt++)
            {
                Assert.That(files.Store.TryRecoverBackup(out var recovered, out string message), Is.False, sample);
                Assert.That(recovered, Is.Null);
                Assert.That(message, Does.Not.Contain("checksum"));
                files.AssertImage(retained);
                Assert.That(files.Store.TryLoad(out var loaded, out message), Is.True, message);
                Equivalent(good, loaded);
                files.AssertImage(retained);
            }

            Assert.That(Json(candidate), Is.EqualTo(unchangedCandidate));
            Assert.That(Directory.GetFiles(files.Root, "*.before-recovery-*.json"), Is.Empty);
            // The corrupt backup does not prevent a legal save based on the still-good primary.
            ContinueDisabled(files, good);
            Assert.That(Json(candidate), Is.EqualTo(unchangedCandidate));
        }

        [TestCase("disabled-promise")]
        [TestCase("disabled-deal")]
        [TestCase("hearing-missing-initial")]
        public void InvalidSourceFirstSaveCannotCreateAnySlotFile(string sample)
        {
            using var files = new Files();
            var good = Progress(Fresh());
            var candidate = Prospective(good, sample);
            string unchangedCandidate = Json(candidate);
            var retained = files.Image();
            Assert.That(retained, Is.Empty);

            for (int attempt = 0; attempt < 2; attempt++)
            {
                Assert.Throws<InvalidDataException>(() => files.Store.Save(candidate));
                files.AssertImage(retained);
                Assert.That(Json(candidate), Is.EqualTo(unchangedCandidate));
            }

            files.Store.Save(good);
            Assert.That(files.Store.TryLoad(out var loaded, out string message), Is.True, message);
            Equivalent(good, loaded);
            Assert.That(File.Exists(files.Store.BackupPath), Is.False);
            ContinueDisabled(files, loaded);
        }

        [Test]
        public void DisabledCommandProgressAndRoundTripRetainTheirRecordedLegacyRules()
        {
            using var files = new Files();
            var good = SeedTwoCopies(files);
            var retained = files.Image();
            Assert.That(files.Store.TryLoad(out var loaded, out string message), Is.True, message);
            Equivalent(good, loaded);
            files.AssertImage(retained);
            ContinueDisabled(files, loaded);
        }

        private static EpisodeState Fresh()
        {
            var s = ContentCatalog.Create(2505);
            Assert.That(s.contestants, Has.Count.EqualTo(6), "Use the real authored factory, not injected capacity-test actors.");
            s.competitionRulesVersion = CompetitionRules.Current;
            s.haveNotRulesStartWeek = 1;
            s.strategyRulesStartWeek = 1;
            EpisodeEngine.EnableStory(s);
            EpisodeEngine.EnableRead(s);
            EpisodeEngine.EnableLevers(s);
            EpisodeEngine.EnableWeek(s);
            EpisodeEngine.EnableEconomy(s);
            EpisodeEngine.EnableAgency(s);
            EpisodeEngine.EnableFinale(s);
            EpisodeEngine.EnableCommitments(s);
            AssertOff(s);
            return s;
        }

        private static EpisodeState Progress(EpisodeState s)
        {
            string unchanged = Json(s);
            var command = new EpisodeCommand {
                id = "safety-save-control-" + s.revision,
                actorId = s.playerId, expectedRevision = s.revision, expectedPhase = s.phase,
                kind = EpisodeCommandKind.SmallTalk,
                targetId = s.Active.First(p => !p.isPlayer).id,
            };
            var result = new EpisodeEngine(s).Apply(command);
            Assert.That(result.accepted, Is.True, result.reason);
            Assert.That(result.duplicate, Is.False);
            Assert.That(result.state.revision, Is.EqualTo(s.revision + 1));
            Assert.That(result.state.acceptedCommandIds.Last(), Is.EqualTo(command.id));
            Assert.That(result.state.events.Count, Is.GreaterThan(s.events.Count));
            Assert.That(result.state.nextSequence, Is.GreaterThan(s.nextSequence));
            Assert.That(Json(s), Is.EqualTo(unchanged), "A public command progresses its detached engine copy.");
            AssertOff(result.state);
            return result.state;
        }

        private static EpisodeState SeedTwoCopies(Files files)
        {
            var initial = Fresh();
            files.Store.Save(initial);
            byte[] first = File.ReadAllBytes(files.Store.SavePath);
            var progressed = Progress(initial);
            files.Store.Save(progressed);
            Assert.That(File.ReadAllBytes(files.Store.BackupPath), Is.EqualTo(first));
            Assert.That(File.ReadAllBytes(files.Store.SavePath), Is.Not.EqualTo(first), "The controls are two genuinely different saves.");
            Assert.That(files.Store.TryLoad(out var loaded, out string message), Is.True, message);
            Equivalent(progressed, loaded);
            return progressed;
        }

        private static void ContinueDisabled(Files files, EpisodeState s)
        {
            var next = Progress(s);
            byte[] previous = File.ReadAllBytes(files.Store.SavePath);
            files.Store.Save(next);
            Assert.That(File.ReadAllBytes(files.Store.BackupPath), Is.EqualTo(previous));
            Assert.That(files.Store.TryLoad(out var loaded, out string message), Is.True, message);
            Equivalent(next, loaded);
            Assert.That(Directory.GetFiles(files.Root, "*.pending-*"), Is.Empty);
        }

        private static EpisodeState Prospective(EpisodeState good, string sample)
        {
            var s = good.Clone();
            s.unifiedCommitmentRulesVersion = UnifiedCommitments.ProspectiveVersion;
            var player = s.Find(s.playerId);
            var other = s.Active.First(p => !p.isPlayer);
            bool promise = sample == "promise" || sample == "disabled-promise" || sample == "broken-promise-attribution";
            bool deal = sample == "deal" || sample == "disabled-deal" || sample == "hearing-missing-initial"
                || sample == "disabled-evidence" || sample == "disabled-receipt";
            if (promise)
                Invoke("MakePromise", s, other.id, PromiseKind.Safety, null, null);
            else if (deal)
                Invoke("StoryDeal", s, player, other, null, DealKind.SafetyAgreement);
            else Assert.That(sample, Is.EqualTo("empty-one"));

            Assert.That(UnifiedCommitments.ValidateRecords(s, out string rowError), Is.True, rowError);
            Assert.That(s.promises.Any(p => p.kind == PromiseKind.Safety), Is.False, "No saved promise mirror was manufactured.");
            Assert.That(s.deals.Any(d => d.type == DealKind.SafetyAgreement), Is.False, "No saved deal mirror was manufactured.");
            if (promise || deal)
            {
                var row = s.unifiedCommitments.Single();
                Assert.That(row.sourcePolicy, Is.EqualTo(promise ? UnifiedCommitments.PromisePolicy : UnifiedCommitments.DealPolicy));
                Assert.That(row.origin, Is.EqualTo(promise ? UnifiedCommitments.PlayerPromise : UnifiedCommitments.StoryDeal));
                Assert.That(row.id, Does.StartWith(promise ? "promise-" : "deal-story-"));
            }

            bool hearing = sample == "hearing-missing-initial" || sample == "broken-promise-attribution"
                || sample == "disabled-evidence" || sample == "disabled-receipt";
            if (hearing)
            {
                s.unifiedHearingRulesVersion = UnifiedCommitmentHearings.ProspectiveVersion;
                // A detached source-owner fixture, not a claim of a publicly played HoH win or
                // enabled command transaction. Current nominations can be stored before a PowerRow:
                // Nominate itself owns the nomination, source effects, real fact emission and initial
                // archive/receipt installation. The complete valid base is proved before corruption.
                s.phase = EpisodePhase.Nomination;
                s.hohId = player.id;
                string second = s.Active.First(p => !p.isPlayer && p.id != other.id).id;
                Invoke("Nominate", s, other.id, second);
                var row = s.unifiedCommitments.Single();
                Assert.That(row.status, Is.EqualTo(DealStatus.Broken));
                Assert.That(row.brokenById, Is.EqualTo(player.id));
                Assert.That(row.settledWeek, Is.EqualTo(s.week));
                Assert.That(other.nominationWeeks, Does.Contain(s.week));
                Assert.That(UnifiedCommitmentHearings.ValidateStorage(s, out string hearingError), Is.True, hearingError);
                var audible = s.story.facts.Where(f => f.kind == FactKinds.BrokenWord && f.refId == row.id).ToArray();
                if (promise)
                {
                    Assert.That(audible, Is.Empty, "The actual selected PromisePolicy still emits zero facts.");
                    Assert.That(s.unifiedHearingEvidence, Is.Empty);
                    Assert.That(s.unifiedHearingReceipts, Is.Empty);
                }
                else
                {
                    Assert.That(audible, Has.Length.EqualTo(1), "The selected DealPolicy emits its one actual audible leaf.");
                    var archived = s.unifiedHearingEvidence.Single();
                    Assert.That(archived.fact.id, Is.EqualTo(audible[0].id));
                    Assert.That(archived.fact.refId, Is.EqualTo(row.id));
                    Assert.That(archived.fact.actorId, Is.EqualTo(player.id));
                    Assert.That(archived.fact.subjectId, Is.EqualTo(other.id));
                    Assert.That(archived.fact.knowers, Does.Contain(player.id).And.Contain(other.id));
                    var receipt = s.unifiedHearingReceipts.Single();
                    Assert.That(receipt.kind, Is.EqualTo(UnifiedCommitmentHearings.Initial));
                    Assert.That(receipt.listenerId, Is.EqualTo(other.id));
                    Assert.That(receipt.factId, Is.EqualTo(audible[0].id));
                    Assert.That(receipt.incidentKey, Is.EqualTo(row.settlementEffectKey));
                }
            }

            // These actual source-produced snapshots are complete-data-valid under today's active
            // C0/story authority, including a current nomination before its later PowerRow. Refusal
            // below must follow an explicit defect, never an invented earned-HoH or ceremony gate.
            string validBase = Json(s);
            Assert.That(EpisodeValidation.TryValidate(s, out string baseReason), Is.True, baseReason);
            Assert.DoesNotThrow(() => new EpisodeEngine(s));
            Assert.DoesNotThrow(() => EpisodeSaveValidation.Validate(s));
            Assert.That(Json(s), Is.EqualTo(validBase));

            if (sample == "hearing-missing-initial")
            {
                var receipt = s.unifiedHearingReceipts.Single();
                Assert.That(receipt.kind, Is.EqualTo(UnifiedCommitmentHearings.Initial));
                Assert.That(s.unifiedHearingReceipts.Remove(receipt), Is.True);
                Assert.That(s.unifiedHearingEvidence, Has.Count.EqualTo(1), "The genuine archive remains; only its required Initial is removed.");
                Assert.That(UnifiedCommitmentHearings.ValidateStorage(s, out string storageReason), Is.True, storageReason,
                    "Intermediate hearing storage does not establish complete emitted-source lineage.");
            }
            else if (sample == "broken-promise-attribution")
            {
                var row = s.unifiedCommitments.Single();
                Assert.That(row.sourcePolicy, Is.EqualTo(UnifiedCommitments.PromisePolicy));
                Assert.That(row.brokenById, Is.EqualTo(row.makerId));
                row.brokenById = row.beneficiaryId;
            }
            else if (sample == "disabled-promise" || sample == "disabled-deal")
                s.unifiedCommitmentRulesVersion = 0;
            else if (sample == "disabled-evidence" || sample == "disabled-receipt")
            {
                s.unifiedCommitmentRulesVersion = s.unifiedHearingRulesVersion = 0;
                s.unifiedCommitments.Clear();
                if (sample == "disabled-evidence") s.unifiedHearingReceipts.Clear();
                else s.unifiedHearingEvidence.Clear();
            }

            if (SupportedSourceSnapshot(sample))
            {
                Assert.That(EpisodeValidation.TryValidate(s, out string reason), Is.True, reason);
                Assert.DoesNotThrow(() => new EpisodeEngine(s));
            }
            else
            {
                Assert.That(EpisodeValidation.TryValidate(s, out _), Is.False,
                    "Explicitly corrupted settlement attribution, Initial lineage or disabled retained records remain invalid.");
                Assert.Throws<ArgumentException>(() => new EpisodeEngine(s));
            }
            return s;
        }

        private static bool SupportedSourceSnapshot(string sample) => sample == "empty-one" || sample == "promise" || sample == "deal";

        private static void AssertOff(EpisodeState s)
        {
            Assert.That(s.schemaVersion, Is.EqualTo(27));
            Assert.That(s.unifiedCommitmentRulesVersion, Is.Zero);
            Assert.That(s.unifiedCommitments, Is.Not.Null.And.Empty);
            Assert.That(s.unifiedHearingRulesVersion, Is.Zero);
            Assert.That(s.unifiedHearingEvidence, Is.Not.Null.And.Empty);
            Assert.That(s.unifiedHearingReceipts, Is.Not.Null.And.Empty);
            EpisodeSaveValidation.Validate(s);
        }

        private static JsonSerializer Serializer() => (JsonSerializer)typeof(EpisodeSaveStore).Assembly
            .GetType("Gamesim.Persistence.SaveJson", true)
            .GetMethod("Serializer", BindingFlags.Public | BindingFlags.Static).Invoke(null, null);
        private static JObject Payload(EpisodeState s) => JObject.FromObject(s, Serializer());
        private static string Json(EpisodeState s) => JsonConvert.SerializeObject(s);
        private static void Equivalent(EpisodeState expected, EpisodeState actual)
        {
            AssertOff(actual);
            Assert.That(JToken.DeepEquals(Payload(expected), Payload(actual)), Is.True,
                "All source rules, identities, history, knowledge, RNG and command receipts remain exact.");
        }

        private static void EquivalentSupported(EpisodeState expected, EpisodeState actual)
        {
            EpisodeSaveValidation.Validate(actual);
            Assert.That(actual.unifiedCommitmentRulesVersion, Is.EqualTo(1));
            Assert.That(actual.unifiedHearingRulesVersion, Is.Zero, "These source snapshots do not claim an actual public hearing transaction.");
            Assert.That(JToken.DeepEquals(Payload(expected), Payload(actual)), Is.True,
                "An enabled load preserves exact rules, source provenance, RNG, history and receipts without conversion.");
        }

        private static void Invoke(string name, params object[] arguments)
        {
            var method = typeof(EpisodeEngine).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, name);
            try { method.Invoke(null, arguments); }
            catch (TargetInvocationException error) { throw error.InnerException ?? error; }
        }

        private sealed class Files : IDisposable
        {
            private const string Prefix = "GamesimUnifiedSafetySaveRefusal-";
            private readonly string leaf = Prefix + Guid.NewGuid().ToString("N");
            internal readonly string Root;
            internal readonly EpisodeSaveStore Store;

            internal Files()
            {
                Root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), leaf));
                Guard();
                Directory.CreateDirectory(Root);
                Store = new EpisodeSaveStore(Path.Combine(Root, "episode.json"));
            }

            internal void Write(string target, EpisodeState s)
            {
                Guard();
                Assert.That(target == Store.SavePath || target == Store.BackupPath, Is.True);
                File.WriteAllText(target, PersistenceMigrationTests.Envelope(Payload(s)));
            }

            internal SortedDictionary<string, byte[]> Image()
            {
                Guard();
                Assert.That(Directory.GetDirectories(Root), Is.Empty);
                return new SortedDictionary<string, byte[]>(Directory.GetFiles(Root)
                    .ToDictionary(Path.GetFileName, File.ReadAllBytes), StringComparer.Ordinal);
            }

            internal void AssertImage(SortedDictionary<string, byte[]> expected)
            {
                var actual = Image();
                Assert.That(actual.Keys, Is.EqualTo(expected.Keys), "Refusal creates, deletes or renames no slot file.");
                foreach (var file in expected)
                    Assert.That(actual[file.Key], Is.EqualTo(file.Value), "Every retained slot byte remains unchanged: " + file.Key);
            }

            private void Guard()
            {
                string resolved = Path.GetFullPath(Root);
                string parent = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                Assert.That(Path.GetDirectoryName(resolved), Is.EqualTo(parent));
                Assert.That(Path.GetFileName(resolved), Is.EqualTo(leaf));
                Assert.That(leaf, Does.StartWith(Prefix));
                Assert.That(Guid.TryParseExact(leaf.Substring(Prefix.Length), "N", out _), Is.True);
                if (Directory.Exists(resolved))
                    Assert.That(File.GetAttributes(resolved) & FileAttributes.ReparsePoint, Is.EqualTo((FileAttributes)0),
                        "The private slot root must never be redirected before reading, writing or cleanup.");
            }

            public void Dispose()
            {
                Guard();
                if (Directory.Exists(Root)) Directory.Delete(Root, true);
            }
        }
    }
}
