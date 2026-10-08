using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Gamesim.Persistence;
using Gamesim.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// Actual fresh director, public player channel and local durable boundary. No phase, role,
    /// source record, pending conversation, physical evidence or random stream is manufactured.
    /// Existing scene fixtures still start in their legacy mode; only these cases start a new run.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        [UnityTest]
        public IEnumerator UnifiedSafety_QuickAndCastFreshSeasonsSelectRulesBeforeFirstSaveAndReloadRetainsThem()
        {
            Assert.That(director.Snapshot.unifiedCommitmentRulesVersion, Is.Zero, "Scene fallback is not silently upgraded.");
            Assert.That(director.Snapshot.unifiedHearingRulesVersion, Is.Zero);
            var legacy = ContentCatalog.Create(2511);
            Assert.That(legacy.unifiedCommitmentRulesVersion, Is.Zero);
            Assert.That(legacy.unifiedHearingRulesVersion, Is.Zero);

            yield return UsStartFresh(null);
            var quick = director.Snapshot;
            UsAssertDisk(quick);
            director.LoadNow();
            UsEquivalent(quick, director.Snapshot);
            UsAssertMode(director.Snapshot);

            string quickPath = director.SavePath;
            var retainedQuick = File.ReadAllBytes(quickPath);
            yield return UsStartFresh(new SeasonBuilder.Choice { HouseSize = 8 });
            var cast = director.Snapshot;
            Assert.That(cast.contestants, Has.Count.EqualTo(8));
            Assert.That(cast.sessionId, Is.Not.EqualTo(quick.sessionId));
            Assert.That(director.SavePath, Is.Not.EqualTo(quickPath));
            CollectionAssert.AreEqual(retainedQuick, File.ReadAllBytes(quickPath));
            UsAssertDisk(cast);
            director.LoadNow();
            UsEquivalent(cast, director.Snapshot);
            UsAssertMode(director.Snapshot);
            Assert.That(legacy.unifiedCommitmentRulesVersion, Is.Zero);
            Assert.That(legacy.unifiedHearingRulesVersion, Is.Zero);
        }

        [UnityTest]
        public IEnumerator UnifiedSafety_PublicPromiseRoundTripRejectsDuplicateStaleWrongActorAndRepeatedDutyWithoutSaving()
        {
            yield return UsStartFresh(null);
            var before = director.Snapshot;
            var command = UsPromise(before);
            var result = director.Submit(command);
            Assert.That(result.accepted, Is.True, result.reason);
            UsAssertPromise(before, result.state, command);
            UsAssertDisk(result.state);
            director.LoadNow();
            UsEquivalent(result.state, director.Snapshot);
            UsAssertMode(director.Snapshot);
            var retained = UsImage();
            int writes = 0;
            NpcWrite("saveCandidateForDiagnostics", (Action<EpisodeState>)(_ => writes++));
            try
            {
                var duplicate = director.Submit(command);
                Assert.That(duplicate.accepted, Is.False);
                Assert.That(duplicate.duplicate, Is.True, "The saved receipt survives reload even though the old expected revision is stale.");
                var stale = UsPromise(before);
                Assert.That(director.Submit(stale).accepted, Is.False);
                var wrongActor = UsPromise(director.Snapshot);
                wrongActor.actorId = wrongActor.targetId;
                Assert.That(director.Submit(wrongActor).accepted, Is.False);
                var repeated = UsPromise(director.Snapshot);
                Assert.That(director.Submit(repeated).accepted, Is.False, "A new command ID cannot repeat the same active duty.");
                Assert.That(writes, Is.Zero, "Rejected commands never reach the disk transaction.");
                UsEquivalent(result.state, director.Snapshot);
                UsAssertImage(retained);
                UsAssertDisk(result.state);
            }
            finally { NpcWrite("saveCandidateForDiagnostics", null); }
        }

        [UnityTest]
        public IEnumerator UnifiedSafety_PublicPromiseBeforeWriteFailurePreservesBothCopiesAndRetryCommitsTheSameCandidate()
        {
            yield return UsStartFresh(null);
            var before = director.Snapshot;
            var command = UsPromise(before);
            var expected = new EpisodeEngine(before).Apply(command);
            Assert.That(expected.accepted, Is.True, expected.reason);
            var retained = UsImage();
            EpisodeState attempted = null;
            int writes = 0;
            NpcWrite("saveCandidateForDiagnostics", (Action<EpisodeState>)(candidate =>
            {
                writes++; attempted = candidate.Clone();
                throw new IOException("Injected enabled Safety pre-write failure.");
            }));
            try
            {
                Assert.That(director.Submit(command).accepted, Is.False);
                Assert.That(writes, Is.EqualTo(1));
                UsEquivalent(expected.state, attempted);
                UsAssertPromise(before, attempted, command);
                UsEquivalent(before, director.Snapshot);
                UsAssertImage(retained);
                UsAssertDisk(before);
                Assert.That(NpcRead<bool>("npcSaveSuspended"), Is.True);
                Assert.That(director.StatusMessage, Does.Contain("not committed"));
            }
            finally { NpcWrite("saveCandidateForDiagnostics", null); }

            director.SaveNow(); // Save the unchanged session, then retry the same still-uncommitted ID.
            UsEquivalent(before, director.Snapshot);
            var retry = director.Submit(command);
            Assert.That(retry.accepted, Is.True, retry.reason);
            UsEquivalent(expected.state, retry.state);
            UsAssertDisk(expected.state);
            Assert.That(retry.state.acceptedCommandIds.Count(id => id == command.id), Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator UnifiedSafety_PublicPromiseAcknowledgementFailureAdoptsExactDiskCandidateAndDoesNotCommitTwice()
        {
            yield return UsStartFresh(null);
            var before = director.Snapshot;
            var command = UsPromise(before);
            var expected = new EpisodeEngine(before).Apply(command);
            Assert.That(expected.accepted, Is.True, expected.reason);
            int writes = 0;
            NpcWrite("saveCandidateForDiagnostics", (Action<EpisodeState>)(candidate =>
            {
                writes++;
                new EpisodeSaveStore(director.SavePath).Save(candidate);
                throw new IOException("Injected enabled Safety acknowledgement failure after replacement.");
            }));
            try
            {
                var result = director.Submit(command);
                Assert.That(result.accepted, Is.True, result.reason);
                Assert.That(writes, Is.EqualTo(1));
                UsAssertPromise(before, result.state, command);
                UsEquivalent(expected.state, director.Snapshot);
                UsAssertDisk(expected.state);
                var retained = UsImage();
                var duplicate = director.Submit(command);
                Assert.That(duplicate.duplicate, Is.True);
                Assert.That(duplicate.accepted, Is.False);
                Assert.That(director.Submit(UsPromise(before)).accepted, Is.False);
                Assert.That(writes, Is.EqualTo(1));
                UsAssertImage(retained);
            }
            finally { NpcWrite("saveCandidateForDiagnostics", null); }
            director.LoadNow();
            UsEquivalent(expected.state, director.Snapshot);
            UsAssertDisk(expected.state);
        }

        [UnityTest]
        public IEnumerator UnifiedSafety_NewFreshSlotFailurePreservesTheEnabledRunAndBothGoodCopies()
        {
            yield return UsStartFresh(null);
            var before = director.Snapshot;
            string originalPath = director.SavePath;
            var retained = UsImage();
            string obstruction = Path.Combine(temporaryDirectory, "unified-safety-not-a-directory");
            File.WriteAllText(obstruction, "private test-owned obstruction");
            NpcWrite("saveRoot", obstruction);
            try
            {
                string failure = null;
                director.StartSeason(null, reason => failure = reason);
                Assert.That(failure, Is.Not.Null.And.Not.Empty);
                Assert.That(director.SavePath, Is.EqualTo(originalPath));
                UsEquivalent(before, director.Snapshot);
                UsAssertMode(director.Snapshot);
                var withoutObstruction = UsImage();
                Assert.That(withoutObstruction.Remove(Path.GetFileName(obstruction)), Is.True);
                UsAssertImages(retained, withoutObstruction);
                UsAssertDisk(before);
            }
            finally
            {
                NpcWrite("saveRoot", temporaryDirectory);
                Assert.That(Path.GetDirectoryName(Path.GetFullPath(obstruction)), Is.EqualTo(Path.GetFullPath(temporaryDirectory)));
                File.Delete(obstruction);
            }
        }

        [UnityTest]
        public IEnumerator UnifiedSafety_PublicAcceptedDealBreachAndActualSpreadAreDurableOnceAcrossFailureRetryAndReload()
        {
            var proof = UsFindPublicAgreement(false, requireSpread: true);
            yield return UsInstallPublicCheckpoint(proof.Before);
            var before = director.Snapshot;
            UsEquivalent(proof.Before, before);
            var retained = UsImage();
            EpisodeState attempted = null;
            int writes = 0;
            NpcWrite("saveCandidateForDiagnostics", (Action<EpisodeState>)(candidate =>
            {
                writes++; attempted = candidate.Clone();
                throw new IOException("Injected actual public nomination/hearing pre-write failure.");
            }));
            try
            {
                Assert.That(director.Submit(proof.Nominate).accepted, Is.False);
                Assert.That(writes, Is.EqualTo(1));
                UsEquivalent(proof.Expected, attempted);
                UsAssertHearing(attempted, proof.Id, proof.Other);
                UsEquivalent(before, director.Snapshot);
                UsAssertImage(retained);
                UsAssertDisk(before);
            }
            finally { NpcWrite("saveCandidateForDiagnostics", null); }

            director.SaveNow();
            UsEquivalent(before, director.Snapshot);
            var committed = director.Submit(proof.Nominate);
            Assert.That(committed.accepted, Is.True, committed.reason);
            UsEquivalent(proof.Expected, committed.state);
            UsAssertHearing(committed.state, proof.Id, proof.Other);
            UsAssertDisk(committed.state);
            director.LoadNow();
            UsEquivalent(committed.state, director.Snapshot);
            UsAssertHearing(director.Snapshot, proof.Id, proof.Other);
            var saved = UsImage();
            int duplicateWrites = 0;
            NpcWrite("saveCandidateForDiagnostics", (Action<EpisodeState>)(_ => duplicateWrites++));
            try
            {
                var duplicate = director.Submit(proof.Nominate);
                Assert.That(duplicate.accepted, Is.False);
                Assert.That(duplicate.duplicate, Is.True);
                var repeated = UsCommand(director.Snapshot, EpisodeCommandKind.Nominate);
                repeated.targetId = proof.Nominate.targetId;
                repeated.secondTargetId = proof.Nominate.secondTargetId;
                Assert.That(director.Submit(repeated).accepted, Is.False, "A second ID cannot replay an already completed nomination anchor.");
                Assert.That(duplicateWrites, Is.Zero);
                UsEquivalent(committed.state, director.Snapshot);
                UsAssertHearing(director.Snapshot, proof.Id, proof.Other);
                UsAssertImage(saved);
            }
            finally { NpcWrite("saveCandidateForDiagnostics", null); }
        }

        [UnityTest]
        public IEnumerator UnifiedSafety_PublicPromiseBreachKeepsItsZeroFactSourcePolicyAfterDurableNominationAndReload()
        {
            var proof = UsFindPublicAgreement(true, requireSpread: false);
            yield return UsInstallPublicCheckpoint(proof.Before);
            var result = director.Submit(proof.Nominate);
            Assert.That(result.accepted, Is.True, result.reason);
            UsEquivalent(proof.Expected, result.state);
            var row = result.state.unifiedCommitments.Single(r => r.id == proof.Id);
            Assert.That(row.status, Is.EqualTo(DealStatus.Broken));
            Assert.That(row.sourcePolicy, Is.EqualTo(UnifiedCommitments.PromisePolicy));
            Assert.That(row.brokenById, Is.EqualTo(result.state.playerId));
            Assert.That(row.settledWeek, Is.EqualTo(result.state.week));
            Assert.That(result.state.story.facts.Where(f => f.kind == FactKinds.BrokenWord && f.refId == proof.Id), Is.Empty);
            Assert.That(result.state.unifiedHearingEvidence.Where(e => e.incidentKey == row.settlementEffectKey), Is.Empty);
            Assert.That(result.state.unifiedHearingReceipts.Where(r => r.incidentKey == row.settlementEffectKey), Is.Empty);
            UsAssertDisk(result.state);
            director.LoadNow();
            UsEquivalent(result.state, director.Snapshot);
            UsAssertMode(director.Snapshot);
            UsAssertDisk(result.state);
        }

        [UnityTest]
        public IEnumerator UnifiedSafety_RealArrivedNpcCompletionSavesBeforeInstallationAndRetryDoesNotRepeatEffects()
        {
            yield return UsStartFresh(null);
            director.OpenSettings(); // Bind the real world while the visible modal prevents scheduling.
            NpcWrite("npcDiagnosticsSuspended", false);
            NpcInvoke("EnsureNpcSocialWorld");
            float deadline = Time.realtimeSinceStartup + 5f;
            while (!director.NpcAutonomyReady && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(director.NpcAutonomyReady, Is.True, director.NpcAutonomyDiagnostic);
            director.ClosePanels();
            NpcInvoke("SetNpcWorldPaused", false);
            deadline = Time.realtimeSinceStartup + 65f;
            while ((director.Snapshot.npcSocial.pending.Count == 0
                || !director.Snapshot.npcSocial.pending.All(p => director.IsNpcConversationPhysicallyReady(p.sequence)))
                && Time.realtimeSinceStartup < deadline) yield return null;
            var started = director.Snapshot;
            Assert.That(started.npcSocial.pending.Count, Is.GreaterThan(0), director.NpcAutonomyDiagnostic);
            Assert.That(started.npcSocial.pending.All(p => director.IsNpcConversationPhysicallyReady(p.sequence)), Is.True);
            UsAssertMode(started);
            UsAssertDisk(started); // The physical start, not merely its approach, is already durable.
            NpcWrite("npcApproachDiagnosticsSuppressed", true);
            NpcWrite("npcWorldFraction", 0d);
            long untilCompletion = started.npcSocial.pending.Min(p =>
                (long)Math.Ceiling(p.durationMs / 1000d) - (started.npcSocial.clockTick - p.startedTick));
            Assert.That(untilCompletion, Is.GreaterThanOrEqualTo(1));
            NpcWrite("npcFrameFraction", (double)(untilCompletion - 1));
            Assert.That((bool)NpcInvoke("FlushNpcWholeTicks"), Is.True);
            var before = director.Snapshot;
            var retained = UsImage();
            EpisodeState attempted = null;
            int writes = 0;
            NpcWrite("saveCandidateForDiagnostics", (Action<EpisodeState>)(candidate =>
            {
                writes++; attempted = candidate.Clone();
                throw new IOException("Injected physically arrived enabled-mode completion failure.");
            }));
            try
            {
                NpcWrite("npcFrameFraction", 1.2d);
                Assert.That((bool)NpcInvoke("FlushNpcWholeTicks"), Is.False);
                Assert.That(writes, Is.EqualTo(1));
                Assert.That(attempted, Is.Not.Null);
                UsAssertMode(attempted);
                Assert.That(attempted.npcSocial.pending.Count, Is.LessThan(before.npcSocial.pending.Count));
                Assert.That(attempted.npcSocial.randomState, Is.Not.EqualTo(before.npcSocial.randomState));
                Assert.That(attempted.randomState, Is.EqualTo(before.randomState));
                UsEquivalent(before, director.Snapshot);
                UsAssertImage(retained);
                UsAssertDisk(before);
                Assert.That(NpcRead<bool>("npcSaveSuspended"), Is.True);
                Assert.That(director.ObservedNpcConversation, Is.Empty);
            }
            finally { NpcWrite("saveCandidateForDiagnostics", null); }

            director.SaveNow();
            Assert.That(NpcRead<bool>("npcSaveSuspended"), Is.False);
            NpcInvoke("SetNpcWorldPaused", false);
            NpcWrite("npcFrameFraction", 0d);
            deadline = Time.realtimeSinceStartup + 5f;
            while (!director.Snapshot.npcSocial.pending.All(p => director.IsNpcConversationPhysicallyReady(p.sequence))
                && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(director.Snapshot.npcSocial.pending.All(p => director.IsNpcConversationPhysicallyReady(p.sequence)), Is.True);
            UsEquivalent(before, director.Snapshot);
            NpcWrite("npcFrameFraction", 1.2d); NpcWrite("npcWorldFraction", 0d);
            Assert.That((bool)NpcInvoke("FlushNpcWholeTicks"), Is.True);
            UsEquivalent(attempted, director.Snapshot);
            UsAssertDisk(attempted);
            Assert.That((bool)NpcInvoke("FlushNpcWholeTicks"), Is.True);
            UsEquivalent(attempted, director.Snapshot);
            director.SuspendNpcAutonomyForDiagnostics();
            director.LoadNow();
            UsEquivalent(attempted, director.Snapshot);
            UsAssertDisk(attempted);
        }

        private IEnumerator UsStartFresh(SeasonBuilder.Choice choice)
        {
            director.SuspendNpcAutonomyForDiagnostics();
            director.StartSeason(choice);
            UsAssertMode(director.Snapshot);
            UsAssertDisk(director.Snapshot); // Bind the first write before a frame, opening beat or extra save can run.
            yield return SettleCast();
            // Real public skip operations also make this fixture usable interactively. Batchmode
            // does not play the opening; never replace its persisted beats or manufacture effects.
            if (director.Opening != null && director.Opening.IsPlaying)
            {
                float deadline = Time.realtimeSinceStartup + 10f;
                while (director.Opening.IsPlaying && Time.realtimeSinceStartup < deadline)
                {
                    director.SkipOpening();
                    director.Opening.SkipIntroductions();
                    yield return null;
                }
                Assert.That(director.Opening.IsPlaying, Is.False);
            }
            director.ClosePanels();
            UsAssertMode(director.Snapshot);
            UsAssertDisk(director.Snapshot); // StartSeason's initial write already contains both selected flags.
            director.SaveNow();
            director.SaveNow();
            Assert.That(File.Exists(director.SavePath + ".backup"), Is.True);
            UsAssertDisk(director.Snapshot);
        }

        private sealed class UsAgreementProof
        {
            internal EpisodeState Before, Expected;
            internal EpisodeCommand Nominate;
            internal string Id, Other;
        }

        private static UsAgreementProof UsFindPublicAgreement(bool promise, bool requireSpread)
        {
            // Seed and legal target selection are bounded coverage controls, not edited outcomes.
            // Every accepted step is the actual public Apply path, including its revision, command
            // receipt, post-command owners and full validator. Director StartSeason opt-in has its
            // own tests above; this explicit fresh-only factory opt-in is diagnostic setup only.
            for (uint seed = 2505; seed < 2537; seed++)
            {
                var fresh = ContentCatalog.Create(seed);
                Assert.That(fresh.unifiedCommitmentRulesVersion, Is.Zero);
                Assert.That(fresh.unifiedHearingRulesVersion, Is.Zero);
                fresh.competitionRulesVersion = CompetitionRules.Current;
                fresh.haveNotRulesStartWeek = fresh.strategyRulesStartWeek = 1;
                EpisodeEngine.EnableStory(fresh); EpisodeEngine.EnableRead(fresh);
                EpisodeEngine.EnableLevers(fresh); EpisodeEngine.EnableWeek(fresh);
                EpisodeEngine.EnableEconomy(fresh); EpisodeEngine.EnableAgency(fresh);
                EpisodeEngine.EnableFinale(fresh); EpisodeEngine.EnableCommitments(fresh);
                Assert.That(fresh.unifiedCommitments, Is.Empty);
                Assert.That(fresh.promises.Any(p => p.kind == PromiseKind.Safety), Is.False);
                Assert.That(fresh.deals.Any(d => d.type == DealKind.SafetyAgreement), Is.False);
                fresh.unifiedCommitmentRulesVersion = fresh.unifiedHearingRulesVersion = 1;
                UsAssertMode(fresh);
                var atHoH = UsApply(fresh, UsCommand(fresh, EpisodeCommandKind.Advance));
                Assert.That(atHoH.phase, Is.EqualTo(EpisodePhase.HoH));
                var compete = UsCommand(atHoH, EpisodeCommandKind.Compete); compete.performance = 1;
                var resolved = UsApply(atHoH, compete);
                if (resolved.hohId != resolved.playerId) continue;
                var context = UsApply(resolved, UsCommand(resolved, EpisodeCommandKind.Advance));
                Assert.That(context.phase, Is.EqualTo(EpisodePhase.Nomination));
                Assert.That(context.nominees, Is.Empty);
                foreach (var other in EpisodeEngine.NominationCandidates(context)
                    .Where(c => UnifiedCommitments.Binding(context, context.playerId, c.id).Count == 0))
                {
                    var propose = UsCommand(context, promise ? EpisodeCommandKind.PromiseSafety : EpisodeCommandKind.ProposeDeal);
                    propose.targetId = other.id;
                    if (!promise) propose.text = DealKind.SafetyAgreement;
                    var before = UsApply(context, propose);
                    var added = before.unifiedCommitments.SingleOrDefault(r => !context.unifiedCommitments.Any(old => old.id == r.id)
                        && r.makerId == before.playerId && r.beneficiaryId == other.id
                        && r.origin == (promise ? UnifiedCommitments.PlayerPromise : UnifiedCommitments.PlayerDeal));
                    if (added == null) { Assert.That(promise, Is.False); continue; } // A lawful proposal may be declined.
                    var nominate = UsCommand(before, EpisodeCommandKind.Nominate);
                    nominate.targetId = other.id;
                    nominate.secondTargetId = EpisodeEngine.NominationCandidates(before).First(c => c.id != other.id).id;
                    var expected = UsApply(before, nominate);
                    var settled = expected.unifiedCommitments.Single(r => r.id == added.id);
                    Assert.That(settled.status, Is.EqualTo(DealStatus.Broken));
                    if (requireSpread && !expected.unifiedHearingReceipts.Any(r => r.incidentKey == settled.settlementEffectKey
                        && r.kind == UnifiedCommitmentHearings.Spread)) continue;
                    return new UsAgreementProof { Before = before, Expected = expected, Nominate = nominate, Id = added.id, Other = other.id };
                }
            }
            Assert.Fail("The bounded real-source seed/target search did not produce the required publicly accepted agreement and hearing branch.");
            return null;
        }

        private static EpisodeCommand UsCommand(EpisodeState s, EpisodeCommandKind kind) => new EpisodeCommand
        {
            id = "unified-safety-source-" + Guid.NewGuid().ToString("N"), actorId = s.playerId,
            expectedRevision = s.revision, expectedPhase = s.phase, kind = kind,
        };

        private static EpisodeState UsApply(EpisodeState before, EpisodeCommand command)
        {
            string original = JsonUtility.ToJson(before);
            var result = new EpisodeEngine(before).Apply(command);
            Assert.That(result.accepted, Is.True, result.reason);
            Assert.That(result.duplicate, Is.False);
            Assert.That(result.state.revision, Is.EqualTo(before.revision + 1));
            Assert.That(result.state.acceptedCommandIds.Last(), Is.EqualTo(command.id));
            Assert.That(JsonUtility.ToJson(before), Is.EqualTo(original));
            UsAssertMode(result.state);
            return result.state;
        }

        private IEnumerator UsInstallPublicCheckpoint(EpisodeState checkpoint)
        {
            // Existing reload diagnostics stop the outgoing scene's autonomous/intro writes.
            // The only installed data is the complete state returned by public Apply above.
            director.FreezeForReloadForDiagnostics();
            new EpisodeSaveStore(director.SavePath).Save(checkpoint);
            yield return ReloadEpisode();
            director.SuspendNpcAutonomyForDiagnostics();
            director.ClosePanels();
            UsEquivalent(checkpoint, director.Snapshot);
            UsAssertMode(director.Snapshot);
            UsAssertDisk(checkpoint);
        }

        private static void UsAssertHearing(EpisodeState s, string id, string wronged)
        {
            UsAssertMode(s);
            var row = s.unifiedCommitments.Single(r => r.id == id);
            Assert.That(row.sourcePolicy, Is.EqualTo(UnifiedCommitments.DealPolicy));
            Assert.That(row.origin, Is.EqualTo(UnifiedCommitments.PlayerDeal));
            Assert.That(row.status, Is.EqualTo(DealStatus.Broken));
            Assert.That(row.brokenById, Is.EqualTo(s.playerId));
            Assert.That(row.settledWeek, Is.EqualTo(s.week));
            var live = s.story.facts.Single(f => f.kind == FactKinds.BrokenWord && f.refId == row.id);
            var archive = s.unifiedHearingEvidence.Single(e => e.incidentKey == row.settlementEffectKey && e.fact.id == live.id);
            Assert.That(archive.fact.refId, Is.EqualTo(row.id));
            Assert.That(archive.fact.actorId, Is.EqualTo(s.playerId));
            Assert.That(archive.fact.subjectId, Is.EqualTo(wronged));
            Assert.That(archive.fact.week, Is.EqualTo(row.settledWeek));
            Assert.That(archive.fact.knowers, Does.Contain(s.playerId).And.Contain(wronged));
            var receipts = s.unifiedHearingReceipts.Where(r => r.incidentKey == row.settlementEffectKey).ToArray();
            var initial = receipts.Single(r => r.kind == UnifiedCommitmentHearings.Initial);
            Assert.That(initial.listenerId, Is.EqualTo(wronged));
            Assert.That(initial.factId, Is.EqualTo(live.id));
            var spread = receipts.Where(r => r.kind == UnifiedCommitmentHearings.Spread).ToArray();
            Assert.That(spread.Length, Is.GreaterThan(0), "The actual NomsSet anchor, not a fabricated knower or direct hearing call, spread this fact.");
            Assert.That(receipts.Select(r => r.listenerId).Distinct().Count(), Is.EqualTo(receipts.Length), "One actual incident has at most one effect per listener.");
            foreach (var heard in spread)
            {
                Assert.That(heard.factId, Is.EqualTo(live.id));
                Assert.That(live.knowers, Does.Contain(heard.listenerId));
                Assert.That(heard.listenerId, Is.Not.EqualTo(s.playerId).And.Not.EqualTo(wronged));
                Assert.That(heard.heardWeek, Is.EqualTo(row.settledWeek));
            }
        }

        private static EpisodeCommand UsPromise(EpisodeState s) => new EpisodeCommand
        {
            id = "unified-safety-public-" + Guid.NewGuid().ToString("N"),
            actorId = s.playerId, expectedRevision = s.revision, expectedPhase = s.phase,
            kind = EpisodeCommandKind.PromiseSafety, targetId = s.Active.First(c => !c.isPlayer).id,
        };

        private static void UsAssertMode(EpisodeState s)
        {
            Assert.That(s.unifiedCommitmentRulesVersion, Is.EqualTo(1));
            Assert.That(s.unifiedHearingRulesVersion, Is.EqualTo(1));
            Assert.That(s.commitmentRulesStartWeek, Is.EqualTo(1));
            Assert.That(EpisodeEngine.CommitmentRulesOn(s), Is.True);
            Assert.That(YourWord.On(s), Is.True, "Story/Bonds and commitment authority are active now, not scheduled later.");
            Assert.That(s.promises.Any(p => p.kind == PromiseKind.Safety), Is.False);
            Assert.That(s.deals.Any(d => d.type == DealKind.SafetyAgreement), Is.False);
            EpisodeSaveValidation.Validate(s);
        }

        private static void UsAssertPromise(EpisodeState before, EpisodeState next, EpisodeCommand command)
        {
            UsAssertMode(next);
            Assert.That(next.revision, Is.EqualTo(before.revision + 1));
            Assert.That(next.acceptedCommandIds.Count(id => id == command.id), Is.EqualTo(1));
            var prior = new HashSet<string>(before.unifiedCommitments.Select(r => r.id), StringComparer.Ordinal);
            var row = next.unifiedCommitments.Single(r => !prior.Contains(r.id)
                && r.origin == UnifiedCommitments.PlayerPromise && r.sourcePolicy == UnifiedCommitments.PromisePolicy
                && r.makerId == next.playerId && r.beneficiaryId == command.targetId);
            Assert.That(row.sourcePolicy, Is.EqualTo(UnifiedCommitments.PromisePolicy));
            Assert.That(row.origin, Is.EqualTo(UnifiedCommitments.PlayerPromise));
            Assert.That(row.makerId, Is.EqualTo(next.playerId));
            Assert.That(row.beneficiaryId, Is.EqualTo(command.targetId));
            Assert.That(row.status, Is.EqualTo(DealStatus.Active));
            Assert.That(row.id, Does.StartWith("promise-"));
        }

        private static void UsEquivalent(EpisodeState expected, EpisodeState actual)
        {
            Assert.That(actual, Is.Not.Null);
            Assert.That(actual.randomState, Is.EqualTo(expected.randomState));
            Assert.That(actual.npcSocial.randomState, Is.EqualTo(expected.npcSocial.randomState));
            Assert.That(JsonUtility.ToJson(actual), Is.EqualTo(JsonUtility.ToJson(expected)), "The complete durable graph, including source and hearing receipts, remains exact.");
        }

        private void UsAssertDisk(EpisodeState expected)
        {
            Assert.That(new EpisodeSaveStore(director.SavePath).TryLoad(out var disk, out string message), Is.True, message);
            UsEquivalent(expected, disk);
        }

        private SortedDictionary<string, byte[]> UsImage()
        {
            string root = Path.GetFullPath(temporaryDirectory);
            string parent = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            Assert.That(Path.GetDirectoryName(root), Is.EqualTo(parent));
            const string prefix = "GamesimEpisodeTests-";
            string leaf = Path.GetFileName(root);
            Assert.That(leaf, Does.StartWith(prefix));
            Assert.That(Guid.TryParseExact(leaf.Substring(prefix.Length), "N", out _), Is.True);
            Assert.That(File.GetAttributes(root) & FileAttributes.ReparsePoint, Is.EqualTo((FileAttributes)0));
            Assert.That(Directory.GetDirectories(root), Is.Empty);
            return new SortedDictionary<string, byte[]>(Directory.GetFiles(root)
                .ToDictionary(Path.GetFileName, File.ReadAllBytes), StringComparer.Ordinal);
        }

        private void UsAssertImage(SortedDictionary<string, byte[]> expected) => UsAssertImages(expected, UsImage());

        private static void UsAssertImages(SortedDictionary<string, byte[]> expected, SortedDictionary<string, byte[]> actual)
        {
            Assert.That(actual.Keys, Is.EqualTo(expected.Keys));
            foreach (var file in expected) CollectionAssert.AreEqual(file.Value, actual[file.Key], file.Key);
        }
    }
}
