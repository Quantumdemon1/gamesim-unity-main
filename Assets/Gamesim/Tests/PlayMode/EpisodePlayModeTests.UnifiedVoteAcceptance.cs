using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Gamesim.Episode;
using Gamesim.Simulation;
using Newtonsoft.Json;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// V8's automated genuine-season evidence, not human playtest acceptance. These are fresh shipped seasons and actual
    /// public commands, never pinned ballots or manufactured receipts. The command walk records every accepted boundary's
    /// native serialized hash, reaches every Vote ending, and exercises a genuine overlapping word/deal and a jury receipt
    /// through the director's store and screens. The existing mode-1 screen twins remain in UnifiedVotePages, unchanged.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        [UnityTest]
        public IEnumerator UnifiedVoteAcceptance_PublicSeasonsReachEveryVoteEndingAndRecordTheirActualCommandBoundaries()
        {
            HoldTheHouseForTheFixture();
            var statuses = new HashSet<string>(StringComparer.Ordinal);
            int commands = 0, reveals = 0;
            EpisodeState finished = null;
            foreach (var recipe in new[] { (seed: 2u, size: 8), (seed: 6u, size: 8), (seed: 6u, size: 12) })
            {
                var start = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = recipe.size }, recipe.seed);
                ShippedRules.ApplyFresh(start);
                UvAssertMode(start);
                var engine = new EpisodeEngine(start);
                int accepted = 0, regular = 0;
                for (; accepted < 4000 && engine.Snapshot.phase != EpisodePhase.Finished; accepted++)
                {
                    var before = engine.Snapshot;
                    var applied = UvaStep(engine, recipe.seed, out var command);
                    Assert.That(applied.accepted, Is.True, recipe.seed + "/" + before.week + "/" + command.kind + ": " + applied.reason);
                    var after = applied.state;
                    Assert.That(after.revision, Is.EqualTo(before.revision + 1));
                    Assert.That(after.acceptedCommandIds.Last(), Is.EqualTo(command.id));
                    Assert.That(after.unifiedCommitmentRulesVersion, Is.EqualTo(UnifiedVoteFamilyValidation.Version));
                    foreach (var row in after.unifiedCommitments.Where(r => r.kind == UnifiedVoteTogether.Vote)) statuses.Add(row.status);
                    bool reveal = !before.evictionResolved && after.evictionResolved && before.phase == EpisodePhase.Eviction;
                    if (reveal)
                    {
                        regular++;
                        Assert.That(after.unifiedVoteReveals.Count, Is.EqualTo(before.unifiedVoteReveals.Count + 1));
                        Assert.That(after.unifiedVoteReveals.Last().ballots.Select(b => b.voterId + ">" + b.targetId),
                            Is.EqualTo(after.votes.Select(v => v.voterId + ">" + v.targetId)));
                    }
                    // Actual commands and boundary hashes, not a regenerated fixture or a public knowledge claim.
                    TestContext.Out.WriteLine("V8 public-command " + JsonConvert.SerializeObject(new
                    {
                        recipe.seed, recipe.size, command.id, kind = command.kind.ToString(), beforeRevision = before.revision,
                        afterRevision = after.revision, beforeWeek = before.week, afterWeek = after.week,
                        beforePhase = before.phase.ToString(), afterPhase = after.phase.ToString(),
                        target = command.targetId, secondTarget = command.secondTargetId, choice = command.text,
                        packet = command,
                        beforeHash = UvaHash(before), afterHash = UvaHash(after), regularReveal = reveal,
                        frames = after.unifiedVoteReveals.Count,
                    }));
                }
                finished = engine.Snapshot;
                Assert.That(finished.phase, Is.EqualTo(EpisodePhase.Finished), "Bounded V8 season: " + recipe);
                UvAssertMode(finished);
                Assert.That(finished.unifiedVoteReveals.Count, Is.EqualTo(regular), "Only regular reveals publish archive frames.");
                Assert.That(finished.contestants.Count(c => c.status == ContestantStatus.Winner), Is.EqualTo(1));
                Assert.That(finished.winnerId, Is.Not.EqualTo(finished.runnerUpId));
                commands += accepted; reveals += regular;
            }
            Assert.That(statuses, Is.SupersetOf(new[] { DealStatus.Active, DealStatus.Fulfilled, DealStatus.Broken, DealStatus.Expired, DealStatus.Declined }),
                "The fixed genuine public seasons must supply every ending; no skipped or synthesized witness.");
            Assert.That(reveals, Is.GreaterThan(1));
            TestContext.Out.WriteLine("V8 public seasons complete: " + commands + " accepted commands, " + reveals
                + " regular reveals, statuses " + string.Join(",", statuses.OrderBy(s => s, StringComparer.Ordinal)) + ".");
            yield return UvInstallCheckpoint(finished);
            UvEquivalent(finished, director.Snapshot);
        }

        [UnityTest]
        public IEnumerator UnifiedVoteAcceptance_OneOwnerLineAndTwoRealCommitmentRowsSurviveReloadAndReadOnScreen()
        {
            HoldTheHouseForTheFixture();
            var proof = UvaOverlapFixture();
            yield return UvInstallCheckpoint(proof.After);
            yield return SettleCast();
            var state = director.Snapshot;
            var inventory = CommitmentsRead.Of(state).Where(c => c.id == proof.PromiseId || c.id == proof.DealId).ToArray();
            Assert.That(inventory.Length, Is.EqualTo(2), "Your word inventories both real rows, even though one owns their consequence.");
            var grouped = UnifiedVoteHistory.Fulfillments(state).Concat(UnifiedVoteHistory.Incidents(state))
                .Single(g => g.EvidenceIds.Contains(proof.PromiseId) && g.EvidenceIds.Contains(proof.DealId));
            Assert.That(grouped.OwnerId, Is.EqualTo(proof.PromiseId));
            var owner = state.events.Single(e => e.sequence == proof.OwnerEventSequence);
            Assert.That(owner.audienceIds.Contains(state.playerId), Is.True, "The player's own word is theirs to see.");
            Assert.That(KnownBallots.TellsAnUnknownBallot(state, owner), Is.False);
            Assert.That(state.events.Count(e => e.week == proof.Week && e.kind == "promise-outcome" && e.text == owner.text), Is.EqualTo(1));
            Assert.That(state.events.Any(e => e.week == proof.Week && e.kind == "deal-outcome" && e.text == proof.SuppressedDealLine), Is.False,
                "The non-owner deal never re-mints an incident line.");

            yield return UvpOpenYourWord("V8 real overlap");
            var words = VisibleLines();
            foreach (var row in inventory) Assert.That(words.Any(line => line.Contains(CommitmentsRead.Line(row))), Is.True, row.id);
            if (Application.isBatchMode) yield return CaptureFraming("unified-vote-v8-two-rows-one-incident", settle: false);
            director.ClosePanels();
            yield return null;
            director.ShowNotebookSection(EpisodeDirector.NotebookSection.Story);
            yield return Frames(2);
            Canvas.ForceUpdateCanvases();
            Assert.That(VisibleLines().Any(line => line.Contains(owner.text)), Is.True, "The story reads the actual owner line.");
            Assert.That(VisibleLines().Any(line => line.Contains(proof.SuppressedDealLine)), Is.False);
            if (Application.isBatchMode) yield return CaptureFraming("unified-vote-v8-owner-line", settle: false);
            UvEquivalent(state, director.Snapshot);
            TestContext.Out.WriteLine("V8 genuine overlap: seed " + proof.Seed + ", week " + proof.Week + ", rows "
                + proof.PromiseId + "," + proof.DealId + ", owner event sequence " + proof.OwnerEventSequence + ", state " + UvaHash(state) + ".");
        }

        [UnityTest]
        public IEnumerator UnifiedVoteAcceptance_ARealSavedJuryReceiptAnswersOnceAndReloadsWithTheSameSource()
        {
            HoldTheHouseForTheFixture();
            var fixture = UvpJuryFixture(2, out uint seed);
            yield return UvInstallCheckpoint(fixture);
            yield return SettleCast();
            var before = director.Snapshot;
            int questionIndex = before.juryQuestionIndex;
            var question = before.juryExchanges[before.juryQuestionIndex];
            var row = before.unifiedCommitments.Single(r => r.id == question.receiptId);
            Assert.That((row.kind, row.sourcePolicy, row.status, row.makerId, row.beneficiaryId), Is.EqualTo((UnifiedVoteTogether.Vote,
                UnifiedCommitments.PromisePolicy, DealStatus.Broken, before.playerId, question.questionerId)));
            string receipt = FinaleQuestions.ReceiptLine(before, question);
            yield return OpenFinalePanel();
            yield return Frames(2);
            Assert.That(LastActive(EpisodeHud.JuryReceiptLineName).GetComponent<TMP_Text>().text, Is.EqualTo(receipt));
            if (Application.isBatchMode) yield return CaptureFraming("unified-vote-v8-real-jury-receipt", settle: false);
            director.ClosePanels();
            var command = UvScopedCommand(before, EpisodeCommandKind.AnswerJury, question.questionerId,
                FinaleQuestions.Offered(question.category, question.receiptKind)[0], label: "v8-jury");
            var expected = new EpisodeEngine(before).Apply(command);
            Assert.That(expected.accepted, Is.True, expected.reason);
            var answer = director.Submit(command);
            Assert.That(answer.accepted, Is.True, answer.reason);
            UvEquivalent(expected.state, answer.state);
            UvAssertDisk(expected.state);
            director.LoadNow();
            UvEquivalent(expected.state, director.Snapshot);
            var answered = director.Snapshot.juryExchanges[questionIndex];
            Assert.That((answered.receiptKind, answered.receiptId, answered.answerChoice),
                Is.EqualTo((question.receiptKind, question.receiptId, command.secondTargetId)));
            Assert.That(FinaleQuestions.ReceiptLine(director.Snapshot, answered), Is.EqualTo(receipt));
            UvAssertDuplicateIsReadOnly(command, expected.state);
            TestContext.Out.WriteLine("V8 genuine jury receipt: seed " + seed + ", question index " + questionIndex
                + ", canonical source " + row.id + ", answer " + command.id + ", state " + UvaHash(expected.state) + ".");
        }

        // A bounded test-only public choice policy: decline actual Vote offers to retain a genuine Declined witness,
        // while preserving the one-in-three response cadence for other families. No production strategy is changed.
        private static EpisodeCommand UvaEager(EpisodeState s)
        {
            if (s.pendingDiary != null || s.Find(s.playerId)?.status != ContestantStatus.Active) return null;
            var offer = NpcDeals.Pending(s).FirstOrDefault();
            if (offer != null) return UvScopedCommand(s, EpisodeCommandKind.RespondToDeal, offer.id, text:
                KnownBallots.IsVoteDeal(offer.type) || s.revision % 3 == 0 ? "decline" : EpisodeEngine.AcceptDeal, label: "v8-walk");
            if (s.phase != EpisodePhase.Campaign || !UvPlayerVotes(s)
                || EpisodeEngine.SocialActionsSpent(s) >= EpisodeEngine.SocialActionBudget(s)) return null;
            var voters = EpisodeEngine.Voters(s).Where(c => !c.isPlayer).Select(c => c.id).ToList();
            if (voters.Count == 0) return null;
            string to = voters[s.week % voters.Count];
            if (!CommitmentReferences.Promises(s).Any(p => p.kind == PromiseKind.Vote && p.fromId == s.playerId && p.toId == to && p.status == PromiseStatus.Active))
                return UvScopedCommand(s, EpisodeCommandKind.PromiseVote, to, s.nominees[s.week % 2], label: "v8-walk");
            string partner = voters[(s.week + 1) % voters.Count];
            var types = new[] { DealKind.VoteEvict, DealKind.VoteSave, DealKind.VoteTogether };
            for (int k = 0; k < types.Length; k++)
            {
                string type = types[(s.week + k) % types.Length];
                string target = type == DealKind.VoteTogether ? null : s.nominees[(s.week + k) % 2];
                if (PlayerDeals.CanPropose(s, partner, type, target, out _))
                    return UvScopedCommand(s, EpisodeCommandKind.ProposeDeal, partner, target, type, "v8-walk");
            }
            return null;
        }

        // The established V6 reader sweep's public choices, copied without its fixture/hidden-ballot helpers.
        // Its deterministic choice coin never reads or changes the season's RNG.
        private static EpisodeCommand UvaBusy(EpisodeState s, uint seed, int attempt)
        {
            if (s.pendingDiary != null) return null;
            var me = s.Find(s.playerId);
            if (me == null || me.status != ContestantStatus.Active) return null;
            if (attempt == 0)
            {
                var offer = NpcDeals.Pending(s).FirstOrDefault();
                if (offer != null)
                    return UvScopedCommand(s, EpisodeCommandKind.RespondToDeal, offer.id, text:
                        UvaPick(s, seed, 1, 4) == 0 ? "decline" : EpisodeEngine.AcceptDeal, label: "v8-busy");
            }
            bool social = s.phase == EpisodePhase.Social, campaign = s.phase == EpisodePhase.Campaign;
            if (!social && !campaign) return null;
            if (EpisodeEngine.SocialActionsSpent(s) >= EpisodeEngine.SocialActionBudget(s)) return null;
            var npcs = s.Active.Where(c => !c.isPlayer).ToList();
            if (npcs.Count == 0) return null;
            var a = npcs[UvaPick(s, seed, 2 + attempt * 5, npcs.Count)];
            bool voter = EpisodeEngine.Voters(s).Any(v => v.id == s.playerId);
            switch (UvaPick(s, seed, 3 + attempt * 5, 11))
            {
                case 0:
                case 1:
                    if (!campaign || !voter) return null;
                    var nominee = s.nominees.Where(id => id != a.id).ToList();
                    return nominee.Count == 0 ? null : UvScopedCommand(s, EpisodeCommandKind.PromiseVote, a.id,
                        nominee[UvaPick(s, seed, 4, nominee.Count)], label: "v8-busy");
                case 2:
                case 3:
                case 4:
                {
                    var kinds = PlayerDeals.Available(s, a.id);
                    var vote = kinds.Where(KnownBallots.IsVoteDeal).ToList();
                    var pool = vote.Count > 0 && UvaPick(s, seed, 5, 3) > 0 ? vote : kinds;
                    if (pool.Count == 0) return null;
                    string kind = pool[UvaPick(s, seed, 6 + attempt, pool.Count)];
                    string about = kind == DealKind.TargetAgreement ? PlayerDeals.Subjects(s, a.id).FirstOrDefault()
                        : kind == DealKind.VoteSave || kind == DealKind.VoteEvict
                            ? s.nominees.FirstOrDefault(id => id != a.id && id != s.playerId && PlayerDeals.CanPropose(s, a.id, kind, id, out _)) : null;
                    return UvScopedCommand(s, EpisodeCommandKind.ProposeDeal, a.id, about, kind, "v8-busy");
                }
                case 5: return UvScopedCommand(s, EpisodeCommandKind.PromiseSafety, a.id, label: "v8-busy");
                case 6:
                    if (!campaign || !VoteRead.Available(s)) return null;
                    var unasked = EpisodeEngine.Voters(s).FirstOrDefault(v => !v.isPlayer && !EpisodeEngine.AskedThisWeek(s, v.id));
                    return unasked == null ? null : UvScopedCommand(s, EpisodeCommandKind.AskVote, unasked.id, label: "v8-busy");
                case 7: return UvScopedCommand(s, EpisodeCommandKind.PromiseFinalTwo, a.id, label: "v8-busy");
                case 8: return UvScopedCommand(s, EpisodeCommandKind.SmallTalk, a.id, label: "v8-busy");
                case 9: return UvScopedCommand(s, EpisodeCommandKind.DiscussGame, a.id, label: "v8-busy");
                default: return null;
            }
        }

        private static int UvaPick(EpisodeState s, uint seed, int salt, int n) =>
            n <= 0 ? 0 : (int)((((uint)s.revision * 2654435761u) ^ (seed * 40503u)
                ^ ((uint)salt * 2246822519u) ^ ((uint)s.week * 97u)) % 100003u) % n;

        private static CommandResult UvaStep(EpisodeEngine engine, uint seed, out EpisodeCommand command)
        {
            var before = engine.Snapshot;
            for (int attempt = 0; attempt < 3; attempt++)
            {
                var choice = attempt == 0 ? UvaEager(before) : UvaBusy(before, seed, attempt - 1);
                if (choice == null) continue;
                var tried = engine.Apply(choice);
                if (tried.accepted) { command = choice; return tried; }
                UvEquivalent(before, engine.Snapshot);
            }
            command = UvpNext(before);
            return engine.Apply(command);
        }

        private sealed class UvaOverlap
        {
            internal uint Seed;
            internal int Week, OwnerEventSequence;
            internal string PromiseId, DealId, SuppressedDealLine;
            internal EpisodeState After;
        }

        /// <summary>At most 40 seeds x 600 public commands, including the word and the actual random proposal answer.</summary>
        private static UvaOverlap UvaOverlapFixture()
        {
            for (uint seed = 1; seed <= 40; seed++)
            {
                var start = ContentCatalog.Create(seed);
                ShippedRules.ApplyFresh(start);
                var engine = new EpisodeEngine(start);
                UvaOverlap proof = null;
                var tried = new HashSet<int>();
                for (int step = 0; step < 600; step++)
                {
                    var s = engine.Snapshot;
                    if (s.phase == EpisodePhase.Finished || s.Find(s.playerId).status != ContestantStatus.Active) break;
                    EpisodeCommand command = null;
                    if (proof == null && s.phase == EpisodePhase.Campaign && s.pendingDiary == null && UvPlayerVotes(s)
                        && EpisodeEngine.SocialActionsSpent(s) + 2 <= EpisodeEngine.SocialActionBudget(s) && tried.Add(s.week))
                    {
                        string target = s.nominees[0];
                        string partner = EpisodeEngine.Voters(s).Where(c => !c.isPlayer).Select(c => c.id).OrderBy(id => id, StringComparer.Ordinal)
                            .FirstOrDefault(id => PlayerDeals.CanPropose(s, id, DealKind.VoteEvict, target, out _));
                        if (partner != null)
                        {
                            var word = UvScopedCommand(s, EpisodeCommandKind.PromiseVote, partner, target, label: "v8-overlap");
                            var said = engine.Apply(word);
                            Assert.That(said.accepted, Is.True, said.reason);
                            var promise = said.state.unifiedCommitments.Single(r => r.origin == UnifiedCommitments.PlayerPromise
                                && r.kind == UnifiedVoteTogether.Vote && r.makerId == s.playerId && r.beneficiaryId == partner && r.createdWeek == s.week);
                            proof = new UvaOverlap { Seed = seed, Week = s.week, PromiseId = promise.id };
                            continue; // This accepted public word consumes this step of the unchanged bound.
                        }
                    }
                    if (proof != null && proof.DealId == null && s.phase == EpisodePhase.Campaign && s.pendingDiary == null)
                    {
                        var promise = s.unifiedCommitments.Single(r => r.id == proof.PromiseId);
                        command = UvScopedCommand(s, EpisodeCommandKind.ProposeDeal, promise.beneficiaryId, promise.targetId,
                            DealKind.VoteEvict, "v8-overlap");
                    }
                    command = command ?? UvpNext(s);
                    var applied = engine.Apply(command);
                    Assert.That(applied.accepted, Is.True, applied.reason);
                    if (proof == null) continue;
                    var after = applied.state;
                    if (command.kind == EpisodeCommandKind.ProposeDeal)
                    {
                        var row = after.unifiedCommitments.FirstOrDefault(r => r.origin == UnifiedCommitments.PlayerDeal && r.kind == UnifiedVoteTogether.Vote
                            && r.subtype == DealKind.VoteEvict && r.createdWeek == proof.Week && r.makerId == s.playerId
                            && r.beneficiaryId == command.targetId && r.status == DealStatus.Active);
                        if (row == null) { proof = null; continue; } // Real no; never force the acceptance draw.
                        proof.DealId = row.id;
                    }
                    if (!after.evictionResolved || s.evictionResolved || proof.DealId == null) continue;
                    var group = UnifiedVoteHistory.Fulfillments(after).Concat(UnifiedVoteHistory.Incidents(after))
                        .FirstOrDefault(g => g.EvidenceIds.Contains(proof.PromiseId) && g.EvidenceIds.Contains(proof.DealId));
                    if (group == null) { proof = null; continue; }
                    Assert.That(group.OwnerId, Is.EqualTo(proof.PromiseId));
                    string ownerText = after.Find(after.playerId).name + (group.Kept ? " fulfilled" : " broke") + " a " + PromiseKind.Vote + " promise.";
                    var owner = after.events.Single(e => e.week == proof.Week && e.kind == "promise-outcome" && e.text == ownerText);
                    var deal = after.unifiedCommitments.Single(r => r.id == proof.DealId);
                    proof.SuppressedDealLine = after.Find(after.playerId).name + (group.Kept ? " honoured a " : " broke a ")
                        + DealKind.Title(deal.subtype).ToLowerInvariant() + " with " + after.Find(deal.beneficiaryId).name + ".";
                    proof.OwnerEventSequence = owner.sequence; proof.After = after;
                    UvAssertMode(after);
                    return proof;
                }
            }
            Assert.Fail("No genuine overlapping Vote word/deal owner in 40 seeds x 600 public commands.");
            return null;
        }

        private static string UvaHash(EpisodeState state)
        {
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(UvPayload(state).ToString(Formatting.None))))
                .Replace("-", "").ToLowerInvariant();
        }
    }
}
