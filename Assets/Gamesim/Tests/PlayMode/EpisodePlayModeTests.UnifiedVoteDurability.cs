using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Gamesim.Persistence;
using Gamesim.Simulation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// Vote family V6: the unified vote rules (mode 2) at the director's durable boundary. A season the director starts is
    /// mode 2 before its first save, and a reload keeps it; a vote promise the player makes is a canonical row, saved before
    /// it is installed, reloaded exactly, and its repeat refused without a save; and the reveal - the command that settles
    /// the week's Vote rows and publishes their archive frame - is saved before it is installed: a failed save leaves the
    /// season and both slot files as they were, and the retry commits exactly the prepared reveal, once. Every fixture is the
    /// public engine's own season from <see cref="ShippedRules.ApplyFresh"/>, walked by public commands and loaded as a player
    /// loads a save; nothing is manufactured.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        [UnityTest]
        public IEnumerator UnifiedVote_FreshSeasonsStartInModeTwoBeforeTheFirstSaveAndReloadKeepsIt()
        {
            Assert.That(director.Snapshot.unifiedCommitmentRulesVersion, Is.Zero, "The scene's fallback season is not upgraded.");
            director.SuspendNpcAutonomyForDiagnostics();
            director.StartSeason(null);
            // Before any frame can run: the snapshot and the first write already carry mode 2.
            UvAssertMode(director.Snapshot);
            UvAssertDisk(director.Snapshot);
            yield return SettleCast();
            director.ClosePanels();
            var quick = director.Snapshot;
            UvAssertMode(quick);
            director.SaveNow();
            director.LoadNow();
            UvEquivalent(quick, director.Snapshot);

            director.SuspendNpcAutonomyForDiagnostics();
            director.StartSeason(new SeasonBuilder.Choice { HouseSize = 8 });
            UvAssertMode(director.Snapshot);
            UvAssertDisk(director.Snapshot);
            yield return SettleCast();
            director.ClosePanels();
            var cast = director.Snapshot;
            director.SaveNow();
            director.LoadNow();
            UvEquivalent(cast, director.Snapshot);
            UvAssertMode(director.Snapshot);
        }

        [UnityTest]
        public IEnumerator UnifiedVote_APublicVotePromiseIsACanonicalRowSavedReloadedAndItsRepeatRefusedWithoutASave()
        {
            yield return UvInstall(s => s.phase == EpisodePhase.Campaign && s.pendingDiary == null && UvPlayerVotes(s)
                && EpisodeEngine.SocialActionsSpent(s) < EpisodeEngine.SocialActionBudget(s), "a campaign the player votes in");
            var before = director.Snapshot;
            var command = UvPromise(before);
            var expected = new EpisodeEngine(before).Apply(command);
            Assert.That(expected.accepted, Is.True, expected.reason);
            var result = director.Submit(command);
            Assert.That(result.accepted, Is.True, result.reason);
            UvEquivalent(expected.state, director.Snapshot);
            var row = director.Snapshot.unifiedCommitments.Single(r => !before.unifiedCommitments.Any(old => old.id == r.id));
            Assert.That((row.kind, row.sourcePolicy, row.origin, row.makerId, row.beneficiaryId, row.targetId),
                Is.EqualTo((UnifiedVoteTogether.Vote, UnifiedCommitments.PromisePolicy, UnifiedCommitments.PlayerPromise,
                    before.playerId, command.targetId, command.secondTargetId)), "The word is one canonical Vote row.");
            Assert.That(director.Snapshot.promises.Any(p => p.kind == PromiseKind.Vote), Is.False, "No raw mirror.");
            UvAssertDisk(expected.state);
            director.LoadNow();
            UvEquivalent(expected.state, director.Snapshot);

            int writes = 0;
            NpcWrite("saveCandidateForDiagnostics", (Action<EpisodeState>)(_ => writes++));
            try
            {
                var duplicate = director.Submit(command);
                Assert.That((duplicate.accepted, duplicate.duplicate), Is.EqualTo((false, true)), "The saved receipt survives the reload.");
                var repeat = UvPromise(director.Snapshot);
                Assert.That(director.Submit(repeat).accepted, Is.False, "A new id cannot give the same active word twice.");
                Assert.That(writes, Is.Zero, "Refused commands never reach the disk.");
                UvEquivalent(expected.state, director.Snapshot);
                UvAssertDisk(expected.state);
            }
            finally { NpcWrite("saveCandidateForDiagnostics", null); }
        }

        [UnityTest]
        public IEnumerator UnifiedVote_ARevealsFailedSavePreservesTheSeasonAndBothCopiesAndTheRetryCommitsTheSameReveal()
        {
            yield return UvInstall(s => s.phase == EpisodePhase.Eviction && !s.evictionResolved && s.pendingDiary == null
                && s.evictionStage == EvictionStage.Voting && UvPlayerVotes(s) && s.votes.Any(v => v.voterId == s.playerId)
                && s.week > 1, "an open vote past the first week, the player's ballot cast");
            var before = director.Snapshot;
            var reveal = UvCommand(before, EpisodeCommandKind.Advance);
            var expected = new EpisodeEngine(before).Apply(reveal);
            Assert.That(expected.accepted && expected.state.evictionResolved, Is.True, expected.reason);
            Assert.That(expected.state.unifiedVoteReveals.Count, Is.EqualTo(before.unifiedVoteReveals.Count + 1), "Fixture: the reveal publishes a frame.");
            director.SaveNow();
            director.SaveNow();
            var retained = UvImage();
            EpisodeState attempted = null;
            int writes = 0;
            NpcWrite("saveCandidateForDiagnostics", (Action<EpisodeState>)(candidate =>
            {
                writes++; attempted = candidate.Clone();
                throw new IOException("Injected mode-2 reveal pre-write failure.");
            }));
            try
            {
                Assert.That(director.Submit(reveal).accepted, Is.False);
                Assert.That(writes, Is.EqualTo(1));
                UvEquivalent(expected.state, attempted);
                UvEquivalent(before, director.Snapshot);
                UvAssertImage(retained);
                UvAssertDisk(before);
                Assert.That(NpcRead<bool>("npcSaveSuspended"), Is.True);
                Assert.That(director.StatusMessage, Does.Contain("not committed"));
            }
            finally { NpcWrite("saveCandidateForDiagnostics", null); }

            director.SaveNow();
            UvEquivalent(before, director.Snapshot);
            var retry = director.Submit(reveal);
            Assert.That(retry.accepted, Is.True, retry.reason);
            UvEquivalent(expected.state, director.Snapshot);
            UvAssertDisk(expected.state);
            Assert.That(director.Snapshot.acceptedCommandIds.Count(id => id == reveal.id), Is.EqualTo(1), "Committed once.");
            director.LoadNow();
            UvEquivalent(expected.state, director.Snapshot);
        }

        [UnityTest]
        public IEnumerator UnifiedVote_ATwoPartyDealIsSavedBeforeInstallAndRetryReloadAndDuplicateKeepItExact(
            [Values(DealKind.VoteTogether, DealKind.VoteSave, DealKind.VoteEvict)] string subtype)
        {
            var proof = UvDealFixture(subtype);
            yield return UvInstallCheckpoint(proof.BeforeDeal);
            var before = director.Snapshot;
            // Both copies are real, validated saves of the pre-command public season.
            director.SaveNow();
            var retained = UvImage();
            EpisodeState attempted = null;
            int writes = 0;
            NpcWrite("saveCandidateForDiagnostics", (Action<EpisodeState>)(candidate =>
            {
                writes++; attempted = candidate.Clone();
                // Saving must precede installation, even while the writer is running.
                UvEquivalent(before, director.Snapshot);
                throw new IOException("Injected two-party Vote deal pre-write failure.");
            }));
            try
            {
                var failed = director.Submit(proof.DealCommand);
                Assert.That(failed.accepted, Is.False);
                Assert.That(writes, Is.EqualTo(1));
                UvEquivalent(proof.AfterDeal, attempted);
                UvAssertDeal(attempted, proof, DealStatus.Active);
                UvEquivalent(before, director.Snapshot);
                UvAssertImage(retained);
                UvAssertDisk(before);
                Assert.That(NpcRead<bool>("npcSaveSuspended"), Is.True);
            }
            finally { NpcWrite("saveCandidateForDiagnostics", null); }

            director.SaveNow();
            UvEquivalent(before, director.Snapshot);
            var retry = director.Submit(proof.DealCommand);
            Assert.That(retry.accepted, Is.True, retry.reason);
            UvEquivalent(proof.AfterDeal, retry.state);
            UvAssertDeal(retry.state, proof, DealStatus.Active);
            UvAssertDisk(proof.AfterDeal);
            director.LoadNow();
            UvEquivalent(proof.AfterDeal, director.Snapshot);
            UvAssertDuplicateIsReadOnly(proof.DealCommand, proof.AfterDeal);

            // A new command ID cannot put the same live arrangement to this party again.
            var repeat = UvScopedCommand(director.Snapshot, EpisodeCommandKind.ProposeDeal,
                proof.DealCommand.targetId, proof.DealCommand.secondTargetId, subtype, "repeat");
            var saved = UvImage();
            int refusedWrites = 0;
            NpcWrite("saveCandidateForDiagnostics", (Action<EpisodeState>)(_ => refusedWrites++));
            try
            {
                Assert.That(director.Submit(repeat).accepted, Is.False);
                Assert.That(refusedWrites, Is.Zero);
                UvEquivalent(proof.AfterDeal, director.Snapshot);
                UvAssertImage(saved);
            }
            finally { NpcWrite("saveCandidateForDiagnostics", null); }
        }

        [UnityTest]
        public IEnumerator UnifiedVote_ATwoPartyDealsDecidingRevealIsDurableOnceAcrossFailureRetryAndReload(
            [Values(DealKind.VoteTogether, DealKind.VoteSave, DealKind.VoteEvict)] string subtype)
        {
            var proof = UvDealFixture(subtype);
            yield return UvInstallCheckpoint(proof.BeforeReveal);
            var before = director.Snapshot;
            UvAssertDeal(before, proof, DealStatus.Active);
            director.SaveNow();
            var retained = UvImage();
            EpisodeState attempted = null;
            int writes = 0;
            NpcWrite("saveCandidateForDiagnostics", (Action<EpisodeState>)(candidate =>
            {
                writes++; attempted = candidate.Clone();
                UvEquivalent(before, director.Snapshot);
                throw new IOException("Injected real two-party Vote settlement pre-write failure.");
            }));
            try
            {
                Assert.That(director.Submit(proof.RevealCommand).accepted, Is.False);
                Assert.That(writes, Is.EqualTo(1));
                UvEquivalent(proof.AfterReveal, attempted);
                UvAssertDecidedDeal(attempted, before, proof);
                UvEquivalent(before, director.Snapshot);
                UvAssertImage(retained);
                UvAssertDisk(before);
            }
            finally { NpcWrite("saveCandidateForDiagnostics", null); }

            director.SaveNow();
            UvEquivalent(before, director.Snapshot);
            var retry = director.Submit(proof.RevealCommand);
            Assert.That(retry.accepted, Is.True, retry.reason);
            UvEquivalent(proof.AfterReveal, retry.state);
            UvAssertDecidedDeal(retry.state, before, proof);
            UvAssertDisk(proof.AfterReveal);
            director.LoadNow();
            UvEquivalent(proof.AfterReveal, director.Snapshot);
            UvAssertDecidedDeal(director.Snapshot, before, proof);
            UvAssertDuplicateIsReadOnly(proof.RevealCommand, proof.AfterReveal);
        }

        [UnityTest]
        public IEnumerator UnifiedVote_ATwoPartyRevealAcknowledgementFailureAdoptsExactDiskCandidateWithoutRepeatingEffects()
        {
            var proof = UvDealFixture(DealKind.VoteTogether);
            yield return UvInstallCheckpoint(proof.BeforeReveal);
            var before = director.Snapshot;
            int writes = 0;
            NpcWrite("saveCandidateForDiagnostics", (Action<EpisodeState>)(candidate =>
            {
                writes++;
                UvEquivalent(before, director.Snapshot);
                new EpisodeSaveStore(director.SavePath).Save(candidate);
                throw new IOException("Injected two-party Vote acknowledgement failure after replacement.");
            }));
            try
            {
                var committed = director.Submit(proof.RevealCommand);
                Assert.That(committed.accepted, Is.True, committed.reason);
                Assert.That(writes, Is.EqualTo(1));
                UvEquivalent(proof.AfterReveal, committed.state);
                UvAssertDecidedDeal(committed.state, before, proof);
                UvAssertDisk(committed.state);
                var saved = UvImage();
                var duplicate = director.Submit(proof.RevealCommand);
                Assert.That((duplicate.accepted, duplicate.duplicate), Is.EqualTo((false, true)));
                Assert.That(writes, Is.EqualTo(1));
                UvEquivalent(proof.AfterReveal, director.Snapshot);
                UvAssertImage(saved);
            }
            finally { NpcWrite("saveCandidateForDiagnostics", null); }
            director.LoadNow();
            UvEquivalent(proof.AfterReveal, director.Snapshot);
            UvAssertDuplicateIsReadOnly(proof.RevealCommand, proof.AfterReveal);
        }

        // ------------------------------------------------------------ helpers

        private sealed class UvDealProof
        {
            internal uint Seed;
            internal int Steps;
            internal string Id, Subtype;
            internal EpisodeState BeforeDeal, AfterDeal, BeforeReveal, AfterReveal;
            internal EpisodeCommand DealCommand, RevealCommand;
        }

        private static readonly Dictionary<string, UvDealProof> UvDeals = new Dictionary<string, UvDealProof>(StringComparer.Ordinal);

        /// <summary>
        /// A real accepted public proposal and its first deciding reveal. At most 40 fresh seeds and 600 accepted commands per
        /// seed, including declined proposals. Only ordinary voters are approached; no ballots, roles, status or RNG are written.
        /// The fixed public walker supplies every other command. Cached proofs are never installed back into that walker.
        /// </summary>
        private static UvDealProof UvDealFixture(string subtype)
        {
            if (UvDeals.TryGetValue(subtype, out var cached)) return cached;
            string firstRefusal = null;
            for (uint seed = 1; seed <= 40; seed++)
            {
                var start = ContentCatalog.Create(seed);
                ShippedRules.ApplyFresh(start);
                UvAssertMode(start);
                var engine = new EpisodeEngine(start);
                var proposed = new HashSet<string>(StringComparer.Ordinal);
                UvDealProof proof = null;
                for (int step = 0; step < 600; step++)
                {
                    var before = engine.Snapshot;
                    if (before.phase == EpisodePhase.Finished || before.Find(before.playerId).status != ContestantStatus.Active) break;
                    EpisodeCommand command = null;
                    if (proof == null && before.phase == EpisodePhase.Campaign && before.pendingDiary == null && UvPlayerVotes(before)
                        && EpisodeEngine.SocialActionsSpent(before) < EpisodeEngine.SocialActionBudget(before))
                    {
                        foreach (string partner in EpisodeEngine.Voters(before).Where(c => !c.isPlayer).Select(c => c.id).OrderBy(id => id, StringComparer.Ordinal))
                        {
                            string key = before.week + ":" + partner;
                            string target = subtype == DealKind.VoteTogether ? null : before.nominees[0];
                            if (proposed.Contains(key) || !PlayerDeals.CanPropose(before, partner, subtype, target, out _)) continue;
                            proposed.Add(key);
                            command = UvScopedCommand(before, EpisodeCommandKind.ProposeDeal, partner, target, subtype, "fixture");
                            break;
                        }
                    }
                    command = command ?? UvpNext(before);
                    var applied = engine.Apply(command);
                    if (!applied.accepted)
                    {
                        UvEquivalent(before, engine.Snapshot);
                        firstRefusal = firstRefusal ?? (seed + "/" + before.week + "/" + command.kind + ": " + applied.reason);
                        // The plain walk is never allowed to silently skip a failed source step.
                        if (command.kind != EpisodeCommandKind.ProposeDeal) Assert.Fail(firstRefusal);
                        command = UvpNext(before);
                        applied = engine.Apply(command);
                        Assert.That(applied.accepted, Is.True, applied.reason);
                    }
                    var after = applied.state;
                    UvAssertMode(after);
                    if (proof == null && command.kind == EpisodeCommandKind.ProposeDeal)
                    {
                        var oldIds = new HashSet<string>(before.unifiedCommitments.Select(r => r.id), StringComparer.Ordinal);
                        var row = after.unifiedCommitments.SingleOrDefault(r => !oldIds.Contains(r.id) && r.kind == UnifiedVoteTogether.Vote
                            && r.sourcePolicy == UnifiedCommitments.DealPolicy && r.origin == UnifiedCommitments.PlayerDeal
                            && r.subtype == subtype && r.makerId == before.playerId && r.beneficiaryId == command.targetId && r.status == DealStatus.Active);
                        if (row != null) proof = new UvDealProof { Seed = seed, Id = row.id, Subtype = subtype,
                            BeforeDeal = before, AfterDeal = after, DealCommand = command };
                    }
                    if (proof == null) continue;
                    var settled = after.unifiedCommitments.Single(r => r.id == proof.Id);
                    if (settled.status != DealStatus.Fulfilled && settled.status != DealStatus.Broken) continue;
                    Assert.That(before.unifiedCommitments.Single(r => r.id == proof.Id).status, Is.EqualTo(DealStatus.Active));
                    Assert.That(after.evictionResolved && !before.evictionResolved, Is.True, "The source command really decided the vote.");
                    proof.BeforeReveal = before; proof.AfterReveal = after; proof.RevealCommand = command; proof.Steps = step + 1;
                    UvAssertDecidedDeal(after, before, proof);
                    TestContext.Out.WriteLine("Vote durability: " + subtype + ", seed " + seed + ", " + proof.Steps
                        + " accepted commands; " + proof.Id + " -> " + settled.status + " in week " + settled.settledWeek + ".");
                    return UvDeals[subtype] = proof;
                }
            }
            Assert.Fail("No bounded real two-party " + subtype + " deal/reveal (40 seeds x 600 commands). First refusal: " + firstRefusal);
            return null;
        }

        private static EpisodeCommand UvScopedCommand(EpisodeState s, EpisodeCommandKind kind, string target = null,
            string second = null, string text = null, string label = "evidence") => new EpisodeCommand
        {
            id = "unified-vote-" + label + "-" + s.seed + "-" + s.revision + "-" + kind + "-" + target + "-" + text,
            actorId = s.playerId, expectedRevision = s.revision, expectedPhase = s.phase, kind = kind,
            targetId = target, secondTargetId = second, text = text,
        };

        private IEnumerator UvInstallCheckpoint(EpisodeState checkpoint)
        {
            UvAssertMode(checkpoint);
            director.FreezeForReloadForDiagnostics();
            new EpisodeSaveStore(director.SavePath).Save(checkpoint);
            yield return ReloadEpisode();
            director.SuspendNpcAutonomyForDiagnostics();
            director.ClosePanels();
            UvEquivalent(checkpoint, director.Snapshot);
            UvAssertDisk(checkpoint);
        }

        private static void UvAssertDeal(EpisodeState s, UvDealProof proof, string status)
        {
            UvAssertMode(s);
            var row = s.unifiedCommitments.Single(r => r.id == proof.Id);
            Assert.That((row.kind, row.sourcePolicy, row.origin, row.subtype, row.makerId, row.beneficiaryId, row.targetId, row.status),
                Is.EqualTo((UnifiedVoteTogether.Vote, UnifiedCommitments.DealPolicy, UnifiedCommitments.PlayerDeal,
                    proof.Subtype, proof.BeforeDeal.playerId, proof.DealCommand.targetId, proof.DealCommand.secondTargetId, status)));
            Assert.That(row.reciprocal, Is.True, "Both real parties, one canonical deal.");
            Assert.That((row.createdWeek, row.voteBindingWeek, row.voteFirstRevealWeek),
                Is.EqualTo((proof.BeforeDeal.week, proof.BeforeDeal.week, proof.BeforeDeal.week)));
            Assert.That(s.deals.Any(d => d.id == proof.Id), Is.False, "No raw mirror.");
            Assert.That(s.acceptedCommandIds.Count(id => id == proof.DealCommand.id), Is.EqualTo(1));
        }

        private static void UvAssertDecidedDeal(EpisodeState after, EpisodeState before, UvDealProof proof)
        {
            var row = after.unifiedCommitments.Single(r => r.id == proof.Id);
            Assert.That(new[] { DealStatus.Fulfilled, DealStatus.Broken }, Does.Contain(row.status));
            UvAssertDeal(after, proof, row.status);
            Assert.That(row.settledWeek, Is.EqualTo(before.week));
            Assert.That(after.unifiedVoteReveals.Count, Is.EqualTo(before.unifiedVoteReveals.Count + 1));
            var frame = after.unifiedVoteReveals.Last();
            Assert.That(frame.week, Is.EqualTo(row.settledWeek));
            Assert.That(frame.ballots.Select(b => b.voterId + ">" + b.targetId),
                Is.EqualTo(after.votes.Select(b => b.voterId + ">" + b.targetId)), "Archive is the actual private box, in order.");
            Assert.That(frame.ballots.Count(b => b.voterId == row.makerId), Is.EqualTo(1));
            Assert.That(frame.ballots.Count(b => b.voterId == row.beneficiaryId), Is.EqualTo(1));
            var decision = UnifiedVoteHistory.FindDecision(after, proof.Id);
            Assert.That(decision, Is.Not.Null);
            Assert.That((decision.Status, decision.SettledWeek), Is.EqualTo((row.status, row.settledWeek)));
            Assert.That(row.settlementEffectKey, Is.EqualTo(row.status == DealStatus.Broken ? UnifiedVoteHistory.Key(row, row.settledWeek) : null));
            Assert.That(row.brokenById, Is.EqualTo(row.status == DealStatus.Broken ? decision.ActorId : null),
                "The real verdict's actor, including a collective null, is not manufactured into a sole breaker.");
            Assert.That(after.acceptedCommandIds.Count(id => id == proof.RevealCommand.id), Is.EqualTo(1));
        }

        private void UvAssertDuplicateIsReadOnly(EpisodeCommand command, EpisodeState expected)
        {
            var retained = UvImage();
            int writes = 0;
            NpcWrite("saveCandidateForDiagnostics", (Action<EpisodeState>)(_ => writes++));
            try
            {
                var duplicate = director.Submit(command);
                Assert.That((duplicate.accepted, duplicate.duplicate), Is.EqualTo((false, true)), "Reload retained the exact accepted command receipt.");
                Assert.That(writes, Is.Zero);
                UvEquivalent(expected, director.Snapshot);
                UvAssertImage(retained);
                UvAssertDisk(expected);
            }
            finally { NpcWrite("saveCandidateForDiagnostics", null); }
        }

        /// <summary>
        /// A fresh season as the director starts one (the quick start's factory and <see cref="ShippedRules.ApplyFresh"/>: mode 2),
        /// walked by public commands to the first state <paramref name="at"/> takes, saved to this test's slot and loaded as a
        /// player loads it.
        /// </summary>
        private IEnumerator UvInstall(Func<EpisodeState, bool> at, string what)
        {
            EpisodeState fixture = null;
            for (uint seed = 1; seed <= 40 && fixture == null; seed++)
            {
                var start = ContentCatalog.Create(seed);
                ShippedRules.ApplyFresh(start);
                UvAssertMode(start);
                var engine = new EpisodeEngine(start);
                for (int guard = 0; guard < 600; guard++)
                {
                    var current = engine.Snapshot;
                    if (at(current)) { fixture = current; break; }
                    if (current.phase == EpisodePhase.Finished || current.Find(current.playerId).status != ContestantStatus.Active) break;
                    var result = engine.Apply(NextCommand(current));
                    Assert.That(result.accepted, Is.True, result.reason);
                }
            }
            Assert.That(fixture, Is.Not.Null, "No bounded public fixture: " + what);
            UvAssertMode(fixture);
            director.FreezeForReloadForDiagnostics();
            new EpisodeSaveStore(director.SavePath).Save(fixture);
            yield return ReloadEpisode();
            director.SuspendNpcAutonomyForDiagnostics();
            director.ClosePanels();
            UvEquivalent(fixture, director.Snapshot);
            UvAssertDisk(fixture);
        }

        private static bool UvPlayerVotes(EpisodeState s) => EpisodeEngine.Voters(s).Any(c => c.isPlayer) && s.nominees.Count == 2;

        private static EpisodeCommand UvCommand(EpisodeState s, EpisodeCommandKind kind) => new EpisodeCommand
        {
            id = "unified-vote-" + kind + "-" + Guid.NewGuid().ToString("N"), actorId = s.playerId,
            expectedRevision = s.revision, expectedPhase = s.phase, kind = kind,
        };

        /// <summary>The player's word on the vote to the first voter who is not on the block: to evict the nominee who is not them.</summary>
        private static EpisodeCommand UvPromise(EpisodeState s)
        {
            var command = UvCommand(s, EpisodeCommandKind.PromiseVote);
            command.targetId = EpisodeEngine.Voters(s).First(c => !c.isPlayer).id;
            command.secondTargetId = s.nominees.First(id => id != command.targetId);
            return command;
        }

        private static void UvAssertMode(EpisodeState s)
        {
            Assert.That(s.unifiedCommitmentRulesVersion, Is.EqualTo(UnifiedVoteFamilyValidation.Version), "Mode 2, the unified vote rules (V6).");
            Assert.That(s.unifiedHearingRulesVersion, Is.EqualTo(1));
            Assert.That(s.dealRulesStartWeek, Is.EqualTo(1));
            Assert.That(YourWord.On(s), Is.True);
            Assert.That(s.promises.Any(p => p.kind == PromiseKind.Vote || p.kind == PromiseKind.Safety), Is.False, "No raw Vote or Safety word.");
            Assert.That(s.deals.Any(d => d.type == DealKind.SafetyAgreement || d.type == DealKind.VoteTogether
                || d.type == DealKind.VoteSave || d.type == DealKind.VoteEvict), Is.False, "No raw Vote or Safety deal.");
            EpisodeSaveValidation.Validate(s);
        }

        private static void UvEquivalent(EpisodeState expected, EpisodeState actual)
        {
            Assert.That(actual, Is.Not.Null);
            Assert.That(actual.randomState, Is.EqualTo(expected.randomState));
            Assert.That(actual.npcSocial.randomState, Is.EqualTo(expected.npcSocial.randomState));
            Assert.That(JToken.DeepEquals(UvPayload(actual), UvPayload(expected)), Is.True,
                "The complete durable graph - canonical rows, archive frames, receipts - remains exact.");
        }

        private static JObject UvPayload(EpisodeState state) => JObject.FromObject(state, (JsonSerializer)typeof(EpisodeSaveStore).Assembly
            .GetType("Gamesim.Persistence.SaveJson", true).GetMethod("Serializer", BindingFlags.Static | BindingFlags.Public).Invoke(null, null));

        private void UvAssertDisk(EpisodeState expected)
        {
            Assert.That(new EpisodeSaveStore(director.SavePath).TryLoad(out var disk, out string message), Is.True, message);
            UvEquivalent(expected, disk);
        }

        private SortedDictionary<string, byte[]> UvImage()
        {
            string root = Path.GetFullPath(temporaryDirectory);
            Assert.That(Path.GetFileName(root), Does.StartWith("GamesimEpisodeTests-"));
            Assert.That(Directory.GetDirectories(root), Is.Empty);
            return new SortedDictionary<string, byte[]>(Directory.GetFiles(root)
                .ToDictionary(Path.GetFileName, File.ReadAllBytes), StringComparer.Ordinal);
        }

        private void UvAssertImage(SortedDictionary<string, byte[]> expected)
        {
            var actual = UvImage();
            Assert.That(actual.Keys, Is.EqualTo(expected.Keys));
            foreach (var file in expected) CollectionAssert.AreEqual(file.Value, actual[file.Key], file.Key);
        }
    }
}
