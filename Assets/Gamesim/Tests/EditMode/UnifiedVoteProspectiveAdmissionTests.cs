using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Candidate-only tests. Public seasons below are played under recorded0/1 through the public
    /// engine as constructed walks (<see cref="PinnedVoteSeason"/>): the only constructed facts are
    /// NPC ballots pinned on a detached snapshot and installed through public validation - to keep
    /// the player in the house when they are nominated, and to fix the box of the producer's first
    /// reveal. Every command, offer, verdict and record is the source's. Explicit2 rows/archive are
    /// detached prospective controls, not a public2 season/save, conversion/importer, effect
    /// application, source-round arithmetic or private knowledge.
    /// </summary>
    public sealed class UnifiedVoteProspectiveAdmissionTests
    {
        [TestCase(0)] [TestCase(1)]
        public void CompleteEmptyProspectiveOpeningLeavesPublicAndSaveAuthorityClosed(int sourceMode)
        {
            var source = Fresh(sourceMode, 8, 1); string sourceBefore = Fingerprint(source);
            var s = source.Clone(); s.unifiedCommitmentRulesVersion = UnifiedVoteFamilyValidation.Version;
            string before = Fingerprint(s);
            Assert.That(UnifiedVoteFamilyValidation.TryValidate(s, s.unifiedVoteReveals, out var error), Is.True, error);
            Assert.That(ProspectiveVoteFacade.TryValidateProspectiveUnifiedVote(s, out error), Is.True, error);
            Assert.That(EpisodeValidation.TryValidate(s, out _), Is.False);
            Assert.Throws<ArgumentException>(() => new EpisodeEngine(s));
            Assert.That(UnifiedCommitments.RulesOn(s), Is.False);
            Assert.That(UnifiedCommitmentHearings.RulesOn(s), Is.False);
            Assert.That(UnifiedCommitments.ValidateRecords(s, out _), Is.False);
            Assert.That(CommitmentReferences.PromiseCount(s), Is.Zero);
            Assert.That(CommitmentReferences.DealCount(s), Is.Zero);
            Assert.That(ProspectiveVoteFacade.TryValidateProspectiveVoteStorage(s, out error), Is.True, error);
            Assert.That(Fingerprint(s), Is.EqualTo(before)); Assert.That(Fingerprint(source), Is.EqualTo(sourceBefore));
        }

        [TestCase(0)] [TestCase(1)] [TestCase(3)] [TestCase(-1)]
        public void AggregateNeverTreatsAnyOtherAuthorityVersionAsVote(int version)
        {
            var s = Prospect(Game().Opening); s.unifiedCommitmentRulesVersion = version;
            Refused(s);
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
        [TestCase(6)] [TestCase(7)] [TestCase(8)] [TestCase(9)] [TestCase(10)] [TestCase(11)]
        public void AllOriginRowShapesKeepTheirDistinctTermsAndDirections(int originIndex)
        {
            var s = Prospect(Game().Opening); var row = ShapeRow(s, originIndex);
            string before = Fingerprint(s), rowBefore = Fingerprint(row);
            Assert.That(ProspectiveVoteFacade.TryContext(s, out var context, out var error), Is.True, error);
            Assert.That(ProspectiveVoteFacade.TryValidateVoteRow(s, context, s.unifiedVoteReveals, row, out error), Is.True, error);
            Assert.That(Fingerprint(s), Is.EqualTo(before)); Assert.That(Fingerprint(row), Is.EqualTo(rowBefore));
            // Row-only shape excludes linked-owner and historical command authenticity proof.
            if (row.linkedCommitmentId != null) { s.unifiedCommitments.Add(row); Refused(s); }
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
        [TestCase(6)] [TestCase(7)] [TestCase(8)] [TestCase(9)] [TestCase(10)] [TestCase(11)]
        [TestCase(12)] [TestCase(13)] [TestCase(14)] [TestCase(15)] [TestCase(16)] [TestCase(17)]
        [TestCase(18)] [TestCase(19)] [TestCase(20)] [TestCase(21)] [TestCase(22)] [TestCase(23)]
        public void DetachedMalformedVoteRowsRefuseWithoutNormalization(int defect)
        {
            var s = Prospect(Game().Opening); var row = ShapeRow(s, 4); s.unifiedCommitments.Add(row);
            Accepted(s);
            switch (defect)
            {
                case 0: row.kind = UnifiedCommitments.Safety; break;
                case 1: row.sourcePolicy = "unknown"; break;
                case 2: row.subtype = DealKind.VetoUse; break;
                case 3: row.targetId = s.playerId; break;
                case 4: row.reciprocal = false; break;
                case 5: row.origin = UnifiedCommitments.NpcDeal; break;
                case 6: row.makerId = row.beneficiaryId; break;
                case 7: row.beneficiaryId = "absent"; break;
                case 8: row.id = "deal-player-00"; break;
                case 9: row.id = "deal-player-" + s.nextSequence; break;
                case 10: row.id = "deal-player-01"; break;
                case 11: row.createdWeek = 0; break;
                case 12: row.createdWeek = s.week + 1; break;
                case 13: row.expiresWeek = 0; break;
                case 14: row.expiresWeek++; break;
                case 15: row.voteBindingWeek = 0; break;
                case 16: row.voteFirstRevealWeek++; break;
                case 17: row.status = DealStatus.Accepted; break;
                case 18: row.status = DealStatus.Proposed; break;
                case 19: row.settledWeek = 1; break;
                case 20: row.brokenById = row.makerId; break;
                case 21: row.settlementEffectKey = "vote:made-up"; break;
                case 22: row.linkedCommitmentId = row.id; break;
                default: row.trustImpact = DealTrust.Critical; break;
            }
            Refused(s);
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
        [TestCase(6)] [TestCase(7)] [TestCase(8)] [TestCase(9)] [TestCase(10)] [TestCase(11)]
        public void ClosedWorldAndCompleteArchiveAreCheckedBeforeUnsafeReferences(int defect)
        {
            var s = Prospect(Game().First);
            Accepted(s);
            switch (defect)
            {
                case 0: s.unifiedCommitments = null; break;
                case 1: s.unifiedCommitments.Add(null); break;
                case 2: s.promises = null; break;
                case 3: s.deals.Add(null); break;
                case 4: s.contestants.Add(s.contestants[0].Clone()); break;
                case 5: s.unifiedVoteReveals = null; break;
                case 6: s.unifiedVoteReveals.Clear(); break;
                case 7: s.unifiedVoteReveals.Add(s.unifiedVoteReveals[0].Clone()); break;
                case 8: s.unifiedVoteReveals[0].ballots.Add(s.unifiedVoteReveals[0].ballots[0].Clone()); break;
                case 9: s.ledger.power.Add(s.ledger.power[0].Clone()); break;
                case 10: s.unifiedVoteReveals[0].ballots[0].targetId = "unknown"; break;
                default: s.unifiedVoteReveals[0].ballots.Reverse(); break;
            }
            Refused(s);
        }

        [TestCase(false)] [TestCase(true)]
        public void DetachedCanonicalAndRawMirrorsNeverShareAuthority(bool promise)
        {
            var s = Prospect(Game().Opening); var row = ShapeRow(s, promise ? 0 : 4);
            s.unifiedCommitments.Add(row); Accepted(s);
            if (promise) s.promises.Add(ProspectiveVoteFacade.ProjectPromise(row));
            else s.deals.Add(ProspectiveVoteFacade.ProjectDeal(row));
            Refused(s);
        }

        [TestCase(false)] [TestCase(true)]
        public void NumericSuffixSharingAcrossUnrelatedTrueOwnersIsNotAnIdentityCollision(bool reverseOrder)
        {
            var s = Prospect(Game().Opening); var promise = ShapeRow(s, 0); var deal = ShapeRow(s, 4);
            promise.id = "promise-1"; deal.id = "deal-player-1"; s.nextSequence = Math.Max(s.nextSequence, 2);
            s.unifiedCommitments.Add(promise); s.unifiedCommitments.Add(deal);
            if (reverseOrder) s.unifiedCommitments.Reverse();
            Accepted(s);
            Assert.That(CommitmentReferences.Promises(s).Single().id, Is.EqualTo(promise.id));
            Assert.That(CommitmentReferences.Deals(s).Single().id, Is.EqualTo(deal.id));
        }

        [TestCase(false, 199)] [TestCase(false, 200)] [TestCase(false, 201)]
        [TestCase(true, 199)] [TestCase(true, 200)] [TestCase(true, 201)]
        public void HistoricalRawRowsOccupyTheirSeparateTwoHundredSourceShelves(bool promise, int count)
        {
            var s = Prospect(Game().Opening); s.nextSequence = 1000;
            for (int i = 1; i <= count; i++)
                if (promise) s.promises.Add(new PromiseState { id = "old-p-" + i, fromId = s.playerId,
                    toId = s.contestants[1].id, kind = PromiseKind.Information, status = PromiseStatus.Expired, week = 1, expiresWeek = 1 });
                else s.deals.Add(new DealState { id = "old-d-" + i, proposerId = s.playerId,
                    recipientId = s.contestants[1].id, type = DealKind.InformationSharing, status = DealStatus.Expired,
                    week = 1, expiresWeek = 0, trustImpact = DealTrust.Low });
            // These are detached storage-capacity controls, not invented 200 public commands.
            Check(s, count <= 200);
            if (count == 199)
            {
                s.unifiedCommitments.Add(ShapeRow(s, promise ? 0 : 4)); Accepted(s);
                var second = ShapeRow(s, promise ? 2 : 8); second.id = promise ? "promise-2" : "deal-story-2";
                s.unifiedCommitments.Add(second); Refused(s);
            }
        }

        [TestCase(false)] [TestCase(true)]
        public void SeparatePromiseAndDealShelvesDoNotBecomeOneCombinedLimit(bool reverse)
        {
            var s = Prospect(Game().Opening); s.nextSequence = 1000;
            for (int i = 1; i <= 200; i++)
            {
                s.promises.Add(new PromiseState { id = "p-" + i, kind = PromiseKind.Information,
                    fromId = s.playerId, toId = s.contestants[1].id, week = 1, expiresWeek = 1, status = PromiseStatus.Expired });
                s.deals.Add(new DealState { id = "d-" + i, type = DealKind.InformationSharing,
                    proposerId = s.playerId, recipientId = s.contestants[1].id, week = 1, expiresWeek = 0, status = DealStatus.Expired });
            }
            if (reverse) { s.promises.Reverse(); s.deals.Reverse(); }
            Accepted(s);
            Assert.That(CommitmentReferences.PromiseCount(s), Is.EqualTo(200));
            Assert.That(CommitmentReferences.DealCount(s), Is.EqualTo(200));
        }

        [TestCase(false)] [TestCase(true)]
        public void BindingDutyDuplicatesDoNotErasePolicyDirectionOrHistoricalProvenance(bool promise)
        {
            var s = Prospect(Game().Opening); var a = ShapeRow(s, promise ? 0 : 4);
            var b = a.Clone(); b.id = promise ? "promise-2" : "deal-player-2"; s.nextSequence = 100;
            s.unifiedCommitments.Add(a); s.unifiedCommitments.Add(b); Refused(s);
            b.status = DealStatus.Expired; b.expiresWeek = 1;
            // At week1 no compatible expiry exists, so changing status alone cannot hide it.
            Refused(s);
            s.unifiedCommitments.Remove(b);
            var other = ShapeRow(s, promise ? 2 : 2); other.id = "promise-3";
            other.makerId = a.beneficiaryId; other.beneficiaryId = a.makerId;
            s.unifiedCommitments.Add(other); Accepted(s);
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
        public void NativeTerminalVerdictsUseActualFirstRevealButNotSafetyIncidents(int duty)
        {
            var s = Prospect(PlayerReveal()); var row = TerminalRow(s, duty); s.unifiedCommitments.Add(row);
            Accepted(s);
            var before = Fingerprint(s);
            var safeBefore = UnifiedCommitmentHistory.Records(s).Select(item => item.id).ToArray();
            var decision = UnifiedVoteHistory.FindDecision(s, row.id);
            Assert.That(decision, Is.Not.Null); Assert.That(decision.SettledWeek, Is.EqualTo(row.settledWeek));
            Assert.That(decision.Status, Is.EqualTo(row.status));
            Assert.That(decision.Record.id, Is.EqualTo(row.id));
            Assert.That(UnifiedVoteHistory.Records(s).Single().id, Is.EqualTo(row.id));
            Assert.That(UnifiedCommitmentHistory.Records(s).Select(item => item.id), Is.EqualTo(safeBefore));
            Assert.That(UnifiedCommitmentHistory.Breaches(s).All(incident => !incident.EvidenceIds.Contains(row.id)), Is.True);
            Assert.That(UnifiedCommitmentHistory.Fulfillments(s).All(incident => !incident.EvidenceIds.Contains(row.id)), Is.True);
            Assert.That(ProspectiveVoteFacade.CanonicalLeaf(s, new HouseFactState { refId = row.id }), Is.False);
            Assert.That(CommitmentReferences.ReceiptWeek(s, row.id, row.createdWeek), Is.EqualTo(row.settledWeek));
            Assert.That(Fingerprint(s), Is.EqualTo(before));
            decision.Record.status = DealStatus.Expired; decision.Reveal.ballots.Clear();
            Assert.That(Fingerprint(s), Is.EqualTo(before));
            Assert.That(UnifiedVoteHistory.FindDecision(s, row.id).Status, Is.EqualTo(row.status));
        }

        [TestCase(4, false, 0)] [TestCase(4, false, 1)] [TestCase(4, false, 2)]
        [TestCase(4, true, 0)] [TestCase(4, true, 1)] [TestCase(4, true, 2)]
        [TestCase(5, false, 0)] [TestCase(5, false, 1)] [TestCase(5, false, 2)]
        [TestCase(5, true, 0)] [TestCase(5, true, 1)] [TestCase(5, true, 2)]
        public void DetachedTargetedDealsRequireLeversAtTheirActualDecidingFrame(int duty, bool terminal, int boundary)
        {
            var s = Prospect(PlayerReveal()); var row = TerminalRow(s, duty);
            var frame = s.unifiedVoteReveals.Last();
            if (!terminal) { row.status = DealStatus.Active; row.settledWeek = 0; row.brokenById = null; row.settlementEffectKey = null; }
            s.leverRulesStartWeek = boundary == 0 ? 0 : boundary == 1 ? frame.week : frame.week + 1;
            s.unifiedCommitments.Add(row);
            // Actual frame/ballots; explicitly detached recorded-Levers and row controls.
            // This is NOT a public historical-rule edit, source deal creation or save.
            Check(s, terminal == (boundary == 1));
            string before = Fingerprint(s);
            Assert.That(ProspectiveVoteFacade.FirstDecision(s, s.unifiedVoteReveals, row,
                out var decision, out var status, out _, out var error), Is.True, error);
            Assert.That(decision != null, Is.EqualTo(boundary == 1));
            if (decision != null) { Assert.That(decision.week, Is.EqualTo(frame.week)); Assert.That(status, Is.Not.Null); }
            Assert.That(Fingerprint(s), Is.EqualTo(before));
        }

        [TestCase(false)] [TestCase(true)]
        public void PreC0PromiseAndTogetherVerdictsAreNotErasedToInventLaterCanonicalStamps(bool together)
        {
            var point = PlayerReveal(); var s = Prospect(AfterWeekTurn(point)); var row = TerminalRow(s, together ? 2 : 1);
            s.unifiedCommitments.Add(row); Accepted(s);
            s.commitmentRulesStartWeek = s.week;
            Assert.That(row.settledWeek, Is.LessThan(s.commitmentRulesStartWeek));
            string before = Fingerprint(s);
            Assert.That(ProspectiveVoteFacade.FirstDecision(s, s.unifiedVoteReveals, row,
                out var frame, out var status, out _, out var error), Is.True, error);
            Assert.That(frame, Is.Not.Null); Assert.That(frame.week, Is.EqualTo(row.settledWeek));
            Assert.That(status, Is.EqualTo(row.status));
            Assert.That(ProspectiveVoteFacade.TryContext(s, out var context, out error), Is.True, error);
            Assert.That(ProspectiveVoteFacade.TryValidateVoteRow(s, context, s.unifiedVoteReveals, row, out error), Is.False);
            Assert.That(error, Does.Contain("terminal metadata"));
            Refused(s); Assert.That(Fingerprint(s), Is.EqualTo(before));
            // Original SettlePromise2218 and VoteTogether329 still decide status before C0;
            // canonical terminal metadata cannot represent that earlier un-stamped verdict.
        }

        [TestCase(false)] [TestCase(true)]
        public void CreatedBeforeC0AbsentVoterKeepsACompatibleUnsettledSourceRow(bool together)
        {
            var point = PlayerReveal(); var s = Prospect(AfterWeekTurn(point)); var frame = s.unifiedVoteReveals.Last();
            var power = s.ledger.power.Single(item => item.week == frame.week);
            s.commitmentRulesStartWeek = s.week;
            var row = new UnifiedCommitmentState { id = FreeId(s, together ? "deal-story-" : "promise-npc-"),
                kind = UnifiedVoteTogether.Vote, sourcePolicy = together ? UnifiedCommitments.DealPolicy : UnifiedCommitments.PromisePolicy,
                origin = together ? UnifiedCommitments.StoryDeal : UnifiedCommitments.NpcPromise,
                makerId = power.nominees[0], beneficiaryId = together ? power.nominees[1] : frame.ballots.First(ballot => ballot.voterId != s.playerId).voterId,
                reciprocal = together, createdWeek = frame.week, expiresWeek = frame.week,
                voteBindingWeek = frame.week, voteFirstRevealWeek = frame.week, status = DealStatus.Expired,
                trustImpact = DealTrust.Medium, subtype = together ? DealKind.VoteTogether : null,
                targetId = together ? null : power.nominees[1] };
            Assert.That(row.createdWeek, Is.LessThan(s.commitmentRulesStartWeek));
            string before = Fingerprint(s), rowBefore = Fingerprint(row);
            Assert.That(ProspectiveVoteFacade.TryContext(s, out var context, out var error), Is.True, error);
            Assert.That(ProspectiveVoteFacade.TryValidateVoteRow(s, context, s.unifiedVoteReveals, row, out error), Is.True, error);
            Assert.That(ProspectiveVoteFacade.FirstDecision(s, s.unifiedVoteReveals, row,
                out var decision, out _, out _, out error), Is.True, error);
            Assert.That(decision, Is.Null); Assert.That(row.settledWeek, Is.Zero);
            Assert.That(Fingerprint(s), Is.EqualTo(before)); Assert.That(Fingerprint(row), Is.EqualTo(rowBefore));
            // Row-only chronology compatibility, NOT whole-state historical activation. Other
            // source owners can be unrepresentable under this detached changed C0 boundary.
        }

        [Test]
        public void DetachedOpenVetoAskPriceUsesTheFirstLaterLeversEligibleActualNomineeFrame()
        {
            var witness = RepeatedPlayerNomineeFrames(); var s = Prospect(witness.Point);
            var first = s.unifiedVoteReveals.Single(frame => frame.week == witness.FirstWeek);
            var later = s.unifiedVoteReveals.Last();
            var bought = new DealState { id = FreeId(s, "deal-veto-"), type = DealKind.VetoUse,
                proposerId = witness.Maker, recipientId = s.playerId, week = first.week, expiresWeek = first.week,
                status = DealStatus.Fulfilled, settledWeek = first.week, trustImpact = DealKind.DefaultTrust(DealKind.VetoUse) };
            var price = new UnifiedCommitmentState { id = FreeId(s, Negotiation.PricePrefix), kind = UnifiedVoteTogether.Vote,
                sourcePolicy = UnifiedCommitments.DealPolicy, origin = UnifiedVoteFamilyValidation.VetoAskPrice,
                makerId = witness.Maker, beneficiaryId = s.playerId, reciprocal = true, createdWeek = first.week,
                expiresWeek = 0, voteBindingWeek = first.week, voteFirstRevealWeek = first.week,
                subtype = DealKind.VoteSave, targetId = s.playerId, status = DealStatus.Active, trustImpact = DealTrust.Medium,
                linkedCommitmentId = bought.id };
            bought.linkedDealId = price.id; s.deals.Add(bought); s.unifiedCommitments.Add(price);
            Assert.That(ProspectiveVoteFacade.FirstDecision(s, s.unifiedVoteReveals, price,
                out var originalFirst, out _, out _, out var error), Is.True, error);
            Assert.That(originalFirst.week, Is.EqualTo(first.week));
            s.leverRulesStartWeek = later.week;
            Assert.That(ProspectiveVoteFacade.FirstDecision(s, s.unifiedVoteReveals, price,
                out var selected, out var status, out var actor, out error), Is.True, error);
            Assert.That(selected, Is.Not.Null); Assert.That(selected.week, Is.EqualTo(later.week));
            price.status = status; price.settledWeek = selected.week;
            if (status == DealStatus.Broken) { price.brokenById = actor; price.settlementEffectKey = UnifiedVoteHistory.Key(price, selected.week); }
            Accepted(s);
            Assert.That(UnifiedVoteHistory.FindDecision(s, price.id).SettledWeek, Is.EqualTo(later.week));
            Assert.That(CommitmentReferences.ReceiptWeek(s, price.id, price.createdWeek), Is.EqualTo(later.week));
            // Both completed nominee frames/private ballots are ACTUAL public progression.
            // The changed historical Levers boundary and linked ask/price are detached local
            // compatibility controls, NOT actual ask commands, migration or historical saves.
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
        [TestCase(6)] [TestCase(7)] [TestCase(8)] [TestCase(9)] [TestCase(10)] [TestCase(11)]
        public void TerminalEvidenceCannotBeChangedToACompatibleLookingLaterOrHiddenOutcome(int defect)
        {
            var s = Prospect(PlayerReveal()); var row = TerminalRow(s, 1); s.unifiedCommitments.Add(row); Accepted(s);
            switch (defect)
            {
                case 0: row.status = DealStatus.Active; row.settledWeek = 0; row.brokenById = null; row.settlementEffectKey = null; break;
                case 1: row.status = DealStatus.Expired; row.settledWeek = 0; row.brokenById = null; row.settlementEffectKey = null; break;
                case 2: row.settledWeek = 0; break;
                case 3: row.settledWeek++; break;
                case 4: row.brokenById = row.beneficiaryId; break;
                case 5: row.brokenById = null; break;
                case 6: row.settlementEffectKey += " "; break;
                case 7: row.settlementEffectKey = row.settlementEffectKey.Replace("vote:", "safety:"); break;
                case 8: row.voteFirstRevealWeek++; break;
                case 9: row.expiresWeek = 0; break;
                case 10: row.targetId = s.unifiedVoteReveals.Single(frame => frame.week == row.settledWeek)
                    .ballots.First(ballot => ballot.voterId == row.makerId).targetId; break;
                default: row.status = DealStatus.Fulfilled; row.brokenById = null; row.settlementEffectKey = null; break;
            }
            Refused(s);
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
        public void NoDecisionAbsencesAndOffBlockTargetsAreNotInventedSettlements(int duty)
        {
            var s = Prospect(Game().First); var frame = s.unifiedVoteReveals[0]; var power = s.ledger.power.Single(item => item.week == frame.week);
            var row = ShapeRow(s, duty == 0 ? 2 : 8); row.createdWeek = row.voteBindingWeek = row.voteFirstRevealWeek = frame.week;
            row.expiresWeek = frame.week; row.makerId = power.nominees[0]; row.beneficiaryId = power.nominees[1];
            if (duty == 0) { row.targetId = null; row.subtype = null; row.reciprocal = false; row.sourcePolicy = UnifiedCommitments.PromisePolicy; }
            else if (duty == 1) { row.subtype = DealKind.VoteTogether; row.targetId = null; }
            else { row.subtype = duty == 2 ? DealKind.VoteSave : DealKind.VoteEvict; row.targetId = frame.ballots[0].voterId; }
            s.unifiedCommitments.Add(row); Accepted(s);
            Assert.That(UnifiedVoteHistory.FindDecision(s, row.id), Is.Null);
            Assert.That(CommitmentReferences.FindCanonical(s, row.id).settledWeek, Is.Zero);
        }

        [TestCase(false)] [TestCase(true)]
        public void OnlyActualSelectedPrivateBallotsDetermineTheNativeStoryNullPromise(bool reversed)
        {
            var s = Prospect(PlayerReveal()); var row = TerminalRow(s, 1);
            row.origin = UnifiedCommitments.StoryPromise; row.targetId = null;
            row.settlementEffectKey = UnifiedVoteHistory.Key(row, row.settledWeek);
            s.unifiedCommitments.Add(row);
            if (reversed) { s.unifiedCommitments.Reverse(); s.contestants.Reverse(); }
            Accepted(s);
            Assert.That(UnifiedVoteHistory.FindDecision(s, row.id).Status, Is.EqualTo(DealStatus.Broken));
            // Native null comparison intentionally differs from TS missing-target pending.
            Assert.That(s.story.facts.Any(fact => fact.refId == row.id), Is.False);
            Assert.That(s.unifiedHearingEvidence.Any(item => item.fact.refId == row.id), Is.False);
        }

        [TestCase(false)] [TestCase(true)]
        public void PromiseOriginCannotManufactureAStoryTargetOrVoteAgainstItsMaker(bool story)
        {
            var s = Prospect(PlayerReveal()); var row = TerminalRow(s, 1);
            if (story) { row.origin = UnifiedCommitments.StoryPromise; row.targetId = null; row.settlementEffectKey = UnifiedVoteHistory.Key(row, row.settledWeek); }
            s.unifiedCommitments.Add(row); Accepted(s);
            row.targetId = story ? s.ledger.power.Single(power => power.week == row.settledWeek).nominees[0] : row.makerId;
            row.settlementEffectKey = UnifiedVoteHistory.Key(row, row.settledWeek);
            Refused(s);
        }

        [TestCase(false, false)] [TestCase(false, true)] [TestCase(true, false)] [TestCase(true, true)]
        public void DetachedUnansweredProposalRespectsActualResultsVersusPublishedDeparture(bool afterEnd, bool declined)
        {
            var point = PlayerReveal();
            if (afterEnd)
            {
                var engine = new EpisodeEngine(point.State);
                for (int step = 0; step < 16 && engine.Snapshot.phase != EpisodePhase.Social; step++)
                {
                    var result = engine.Apply(Next(engine.Snapshot));
                    Assert.That(result.accepted && !result.duplicate, Is.True, result.reason); SourceValid(result.state);
                }
                Assert.That(engine.Snapshot.phase, Is.EqualTo(EpisodePhase.Social));
                Assert.That(engine.Snapshot.week, Is.EqualTo(point.State.week));
                point = PointOf(engine.Snapshot, point.Frames);
            }
            var s = Prospect(point); var power = s.ledger.power.Single(item => item.week == s.week);
            var offer = ShapeRow(s, 3); offer.makerId = offer.targetId = power.evicteeId;
            offer.status = declined ? DealStatus.Declined : DealStatus.Proposed; s.unifiedCommitments.Add(offer);
            // The frame, durable departure and public phase are real; this unanswered row is
            // explicitly a detached lifecycle boundary control, not an actually-created offer.
            Check(s, declined || !afterEnd);
        }

        [TestCase(false, false)] [TestCase(false, true)] [TestCase(true, false)] [TestCase(true, true)]
        public void DetachedSameWeekPriceVoidPreservesInformationRevealOrderButNotEarlierVetoBreak(bool information, bool terminal)
        {
            var s = Prospect(PlayerReveal()); var frame = s.unifiedVoteReveals.Last();
            string partner = frame.ballots.First(ballot => ballot.voterId != s.playerId).voterId;
            long sequence = s.nextSequence; s.nextSequence += 2;
            var bought = new DealState { id = Negotiation.CounterDealPrefix + sequence,
                type = information ? DealKind.InformationSharing : DealKind.VetoUse,
                proposerId = s.playerId, recipientId = partner, week = frame.week,
                expiresWeek = information ? 0 : frame.week, status = DealStatus.Broken, brokenById = partner,
                settledWeek = frame.week, trustImpact = DealKind.DefaultTrust(information ? DealKind.InformationSharing : DealKind.VetoUse),
                linkedDealId = Negotiation.PricePrefix + sequence };
            var price = new UnifiedCommitmentState { id = bought.linkedDealId, kind = UnifiedVoteTogether.Vote,
                sourcePolicy = UnifiedCommitments.DealPolicy, origin = UnifiedCommitments.CounterPrice,
                makerId = s.playerId, beneficiaryId = partner, reciprocal = true, subtype = DealKind.VoteTogether,
                createdWeek = frame.week, expiresWeek = frame.week, voteBindingWeek = frame.week, voteFirstRevealWeek = frame.week,
                status = DealStatus.Active, trustImpact = DealTrust.Medium, linkedCommitmentId = bought.id };
            Assert.That(UnifiedVoteTogether.TryVerdict(price, frame.ballots, out var verdict, out var error), Is.True, error);
            Assert.That(verdict, Is.Not.Null);
            if (terminal)
            {
                price.status = verdict; price.settledWeek = frame.week;
                if (verdict == DealStatus.Broken) price.settlementEffectKey = UnifiedVoteHistory.Key(price, frame.week);
            }
            else price.status = DealStatus.Expired;
            s.deals.Add(bought); s.unifiedCommitments.Add(price);
            // Real source box, explicitly detached linked-row/time controls. Information lies
            // are judged AFTER Vote settlement in this same reveal; veto breach precedes it.
            // No claimed source counter, lie, veto or VoidThePrice command was manufactured.
            Check(s, information == terminal);
        }

        [TestCase(false)] [TestCase(true)]
        public void ExactReferencesRemainDetachedAndPreserveAllSourceScalarsAndOrder(bool promise)
        {
            var s = Prospect(Game().Opening); var row = ShapeRow(s, promise ? 0 : 4); s.unifiedCommitments.Add(row); Accepted(s);
            string before = Fingerprint(s);
            if (promise)
            {
                var view = CommitmentReferences.FindPromise(s, row.id);
                Assert.That(view.kind, Is.EqualTo(PromiseKind.Vote)); Assert.That(view.targetId, Is.EqualTo(row.targetId));
                Assert.That(view.week, Is.EqualTo(row.createdWeek)); view.targetId = "changed"; view.status = PromiseStatus.Expired;
            }
            else
            {
                var view = CommitmentReferences.FindDeal(s, row.id);
                Assert.That(view.type, Is.EqualTo(row.subtype)); Assert.That(view.targetId, Is.EqualTo(row.targetId));
                Assert.That(view.week, Is.EqualTo(row.createdWeek)); view.targetId = "changed"; view.status = DealStatus.Expired;
            }
            var canonical = CommitmentReferences.FindCanonical(s, row.id); canonical.voteBindingWeek = 100;
            Assert.That(Fingerprint(s), Is.EqualTo(before));
        }

        [TestCase(false)] [TestCase(true)]
        public void GenuinePublicProducerDraftsAreDetachedBeforeProspectiveReservation(bool promise)
        {
            var witness = Producer(promise); var s = CompleteProspect(witness.Before); string before = Fingerprint(s);
            if (promise)
            {
                string rawBefore = Fingerprint(witness.Promise);
                Assert.That(UnifiedVoteAdmission.TryPromise(s, witness.Promise, UnifiedCommitments.PlayerPromise, out var row, out var error), Is.True, error);
                Assert.That(row.voteBindingWeek, Is.EqualTo(s.week)); Assert.That(row.voteFirstRevealWeek, Is.EqualTo(s.week));
                Assert.That(row.id, Is.EqualTo(witness.Promise.id)); row.targetId = "changed";
                Assert.That(Fingerprint(witness.Promise), Is.EqualTo(rawBefore));
            }
            else
            {
                string rawBefore = Fingerprint(witness.Deal);
                Assert.That(UnifiedVoteAdmission.TryDeal(s, witness.Deal, UnifiedCommitments.PlayerDeal, out var row, out var error), Is.True, error);
                Assert.That(row.voteBindingWeek, Is.EqualTo(s.week)); Assert.That(row.createdWeek, Is.EqualTo(witness.Deal.week));
                row.status = DealStatus.Expired; Assert.That(Fingerprint(witness.Deal), Is.EqualTo(rawBefore));
            }
            Assert.That(Fingerprint(s), Is.EqualTo(before));
        }

        [TestCase(false)] [TestCase(true)]
        public void GenuineLateNpcAnswerKeepsCreationButDerivesActualAnswerTermAndRevealFloor(bool afterReveal)
        {
            var witness = LateOffer(afterReveal); var s = CompleteProspect(witness.Before);
            var pending = ActualPendingOffer(s, witness); Accepted(s);
            Assert.That(ProspectiveVoteFacade.TryValidateProspectiveUnifiedVote(s, out var baseError), Is.True, baseError);
            var actualPendingOpportunity = witness.Before.State.ledger.opportunities.Single(row => row.id == pending.id);
            Assert.That(actualPendingOpportunity.week, Is.EqualTo(witness.Offer.week));
            Assert.That(actualPendingOpportunity.source, Is.EqualTo(DealKind.VoteSave + ":" + witness.Offer.targetId));
            Assert.That(actualPendingOpportunity.note, Is.EqualTo("offered by " + witness.Offer.proposerId + ", " + DealStatus.Proposed));
            Assert.That(actualPendingOpportunity.response, Is.EqualTo(OpportunityResponse.Ignored));
            string before = Fingerprint(s), rawBefore = Fingerprint(witness.Offer);
            Assert.That(UnifiedVoteAdmission.TryAnswerOffer(s, pending.id, out var answered, out var error), Is.True, error);
            Assert.That(answered.id, Is.EqualTo(witness.Offer.id));
            Assert.That(answered.createdWeek, Is.EqualTo(witness.Offer.week));
            Assert.That(answered.expiresWeek, Is.EqualTo(witness.ActuallyAnswered.expiresWeek));
            Assert.That(answered.voteBindingWeek, Is.EqualTo(witness.Before.State.week));
            Assert.That(answered.voteFirstRevealWeek, Is.EqualTo(s.week + (afterReveal ? 1 : 0)));
            Assert.That(answered.targetId, Is.EqualTo(witness.ActuallyAnswered.targetId));
            Assert.That(Fingerprint(s), Is.EqualTo(before));
            s.unifiedCommitments[s.unifiedCommitments.FindIndex(row => row.id == pending.id)] = answered.Clone();
            var opportunity = s.ledger.opportunities.Single(row => row.id == pending.id);
            var actualAnsweredOpportunity = witness.ActuallyAnsweredOpportunity;
            Assert.That(actualAnsweredOpportunity.week, Is.EqualTo(witness.Offer.week));
            Assert.That(actualAnsweredOpportunity.note, Is.EqualTo(actualPendingOpportunity.note));
            Assert.That(actualAnsweredOpportunity.source, Is.EqualTo(actualPendingOpportunity.source));
            Assert.That(actualAnsweredOpportunity.response, Is.EqualTo(OpportunityResponse.Taken));
            // Actual RespondToDeal2061 changes ONLY response until the next reconciliation.
            opportunity.response = actualAnsweredOpportunity.response;
            Assert.That(Fingerprint(opportunity), Is.EqualTo(Fingerprint(actualAnsweredOpportunity)));
            Accepted(s);
            Assert.That(ProspectiveVoteFacade.TryValidateProspectiveUnifiedVote(s, out error), Is.True, error);
            Assert.That(EpisodeValidation.TryValidate(s, out _), Is.False);
            Assert.That(UnifiedVoteHistory.FindDecision(s, pending.id), Is.Null, "A real answer is not an already-made archived ballot verdict.");
            Assert.That(CommitmentReferences.FindDeal(s, pending.id).week, Is.EqualTo(witness.Offer.week));
            Assert.That(CommitmentReferences.ReceiptWeek(s, pending.id, witness.Offer.week), Is.EqualTo(witness.Offer.week));
            Assert.That(s.story.facts.Any(fact => fact.refId == pending.id), Is.False);
            Assert.That(s.unifiedHearingEvidence.Any(item => item.fact.refId == pending.id), Is.False);
            Assert.That(Fingerprint(witness.Offer), Is.EqualTo(rawBefore));
            answered.status = DealStatus.Declined;
            Assert.That(Fingerprint(s), Is.Not.EqualTo(before)); // Only explicit test installation changed this detached control.
            Assert.That(s.unifiedCommitments.First(row => row.id == pending.id).status, Is.EqualTo(DealStatus.Active));
        }

        [TestCase(false)] [TestCase(true)]
        public void LateAnswerTypedOpportunityCannotRewriteItsCreationToTheAnswerWeek(bool changeOwner)
        {
            var witness = LateOffer(false); var s = CompleteProspect(witness.Before);
            var pending = ActualPendingOffer(s, witness);
            Assert.That(UnifiedVoteAdmission.TryAnswerOffer(s, pending.id, out var answered, out var error), Is.True, error);
            s.unifiedCommitments[s.unifiedCommitments.FindIndex(row => row.id == pending.id)] = answered.Clone();
            var opportunity = s.ledger.opportunities.Single(row => row.id == pending.id);
            opportunity.response = witness.ActuallyAnsweredOpportunity.response;
            Assert.That(ProspectiveVoteFacade.TryValidateProspectiveUnifiedVote(s, out error), Is.True, error);
            Assert.That(pending.createdWeek, Is.LessThan(s.week));
            if (changeOwner) s.unifiedCommitments.Single(row => row.id == pending.id).createdWeek = s.week;
            else opportunity.week = s.week;
            // Compatible scalar/family shape cannot erase the REAL older source creation.
            Accepted(s); string before = Fingerprint(s);
            Assert.That(ProspectiveVoteFacade.TryValidateProspectiveUnifiedVote(s, out error), Is.False);
            Assert.That(error, Does.Contain("creation week"));
            Assert.That(EpisodeValidation.TryValidate(s, out _), Is.False);
            Assert.That(Fingerprint(s), Is.EqualTo(before));
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
        [TestCase(4)] [TestCase(5)] [TestCase(6)] [TestCase(7)]
        [TestCase(8)] [TestCase(9)] [TestCase(10)] [TestCase(11)]
        public void AdmissionRejectsMalformedUnrelatedContainersBeforeAnyWholeStateClone(int defect)
        {
            var witness = Producer(true); var s = CompleteProspect(witness.Before);
            Assert.That(ProspectiveVoteFacade.TryValidateProspectiveUnifiedVote(s, out var validError), Is.True, validError);
            Assert.That(UnifiedVoteAdmission.TryPromise(s, witness.Promise, UnifiedCommitments.PlayerPromise,
                out var draft, out var draftError), Is.True, draftError);
            switch (defect)
            {
                case 0: s.ledger.claims = null; break;
                case 1: s.ledger.opportunities = null; break;
                case 2: s.story.grudges = null; break;
                case 3: s.story.knownFacts = null; break;
                case 4: s.story.lore = null; break;
                case 5: s.story.conduct = null; break;
                case 6: s.storylines = null; break;
                case 7: s.activeModifiers = null; break;
                case 8: s.houseEvents = null; break;
                case 9: s.houseEvents.Add(null); break;
                case 10: s.storylines.Add(null); break;
                default: s.activeModifiers.Add(null); break;
            }
            string before = Fingerprint(s), sourceBefore = Fingerprint(witness.Promise), draftBefore = Fingerprint(draft);
            // Family validity alone is intentionally not validity of every unrelated Clone container.
            Assert.That(UnifiedVoteFamilyValidation.TryValidate(s, s.unifiedVoteReveals, out var familyError), Is.True, familyError);
            Assert.That(UnifiedVoteAdmission.TryPromise(s, witness.Promise, UnifiedCommitments.PlayerPromise,
                out var promise, out var error), Is.False);
            Assert.That(promise, Is.Null); Assert.That(error, Is.Not.Null.And.Not.Empty);
            Assert.That(UnifiedVoteAdmission.TryDeal(s, null, UnifiedCommitments.PlayerDeal, out var deal, out error), Is.False);
            Assert.That(deal, Is.Null);
            Assert.That(UnifiedVoteAdmission.TryAnswerOffer(s, draft.id, out var answer, out error), Is.False);
            Assert.That(answer, Is.Null);
            Assert.That(UnifiedVoteAdmission.TryCounterBundle(s, null, null, out var counter, out error), Is.False);
            Assert.That(counter, Is.Null);
            Assert.That(UnifiedVoteAdmission.TryOwnVetoPrice(s, null, null, out var own, out error), Is.False);
            Assert.That(own, Is.Null);
            Assert.That(UnifiedVoteAdmission.TryAcceptedVetoAskPrice(s, draft.id, null, out var ask, out error), Is.False);
            Assert.That(ask, Is.Null);
            Assert.That(ProspectiveVoteFacade.TryValidateDraftBundle(s, new[] { draft }, Array.Empty<DealState>(), null, out error), Is.False);
            Assert.That(ProspectiveVoteFacade.TryValidateAnswer(s, draft, out error), Is.False);
            Assert.That(Fingerprint(s), Is.EqualTo(before));
            Assert.That(Fingerprint(witness.Promise), Is.EqualTo(sourceBefore)); Assert.That(Fingerprint(draft), Is.EqualTo(draftBefore));
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
        public void GenuineAnswerCannotReplaceImmutableSourceProvenanceOrItsActualAnswerWeek(int defect)
        {
            var witness = LateOffer(false); var s = CompleteProspect(witness.Before);
            var pending = ActualPendingOffer(s, witness);
            Assert.That(UnifiedVoteAdmission.TryAnswerOffer(s, pending.id, out var answered, out var error), Is.True, error);
            Assert.That(ProspectiveVoteFacade.TryValidateAnswer(s, answered, out error), Is.True, error);
            switch (defect)
            {
                case 0:
                    // Coherent alternative NPC + own target would otherwise be a locally valid
                    // offer shape. It is still NOT the actual source row the player answered.
                    answered.makerId = s.contestants.First(person => person.status == ContestantStatus.Active
                        && !person.isPlayer && person.id != pending.makerId).id;
                    answered.targetId = answered.makerId; break;
                case 1: answered.createdWeek = s.week; break;
                case 2: answered.trustImpact = DealTrust.High; break;
                case 3: answered.id = "deal-ask-unknown"; break;
                case 4: answered.voteBindingWeek = pending.createdWeek; answered.expiresWeek = pending.createdWeek; break;
                default: answered.voteFirstRevealWeek++; break;
            }
            string before = Fingerprint(s), changed = Fingerprint(answered), source = Fingerprint(witness.Offer);
            Assert.That(ProspectiveVoteFacade.TryValidateAnswer(s, answered, out error), Is.False);
            Assert.That(error, Is.Not.Null.And.Not.Empty);
            Assert.That(Fingerprint(s), Is.EqualTo(before)); Assert.That(Fingerprint(answered), Is.EqualTo(changed));
            Assert.That(Fingerprint(witness.Offer), Is.EqualTo(source));
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
        public void ImmediateSourceDraftCannotForgeFutureFloorOrConsumeTheInstalledSequenceRule(int defect)
        {
            var witness = Producer(true); var s = CompleteProspect(witness.Before); var raw = witness.Promise.Clone();
            switch (defect)
            {
                case 0: raw.id = "promise-" + (s.nextSequence - 1); break;
                case 1: raw.week++; break;
                case 2: raw.expiresWeek++; break;
                case 3: raw.status = PromiseStatus.Fulfilled; break;
                case 4: raw.brokenById = raw.fromId; break;
                default: raw.toId = "unknown"; break;
            }
            string before = Fingerprint(s), rawBefore = Fingerprint(raw);
            Assert.That(UnifiedVoteAdmission.TryPromise(s, raw, UnifiedCommitments.PlayerPromise, out var row, out var error), Is.False);
            Assert.That(row, Is.Null); Assert.That(error, Is.Not.Null.And.Not.Empty);
            Assert.That(Fingerprint(s), Is.EqualTo(before)); Assert.That(Fingerprint(raw), Is.EqualTo(rawBefore));
        }

        [TestCase(false)] [TestCase(true)]
        public void CounterDraftReservesBothTrueOwnersAndSharesOnlyItsActualNumericSequence(bool safetyPrice)
        {
            var s = CompleteProspect(Producer(true).Before);
            Assert.That(ProspectiveVoteFacade.TryValidateProspectiveUnifiedVote(s, out var baseError), Is.True, baseError);
            string priceType = safetyPrice ? DealKind.SafetyAgreement : DealKind.FinalTwo;
            int priceExpiry = safetyPrice ? s.week : 0;
            string npc = s.contestants.Where(person => !person.isPlayer && person.status == ContestantStatus.Active)
                .OrderBy(person => person.id, StringComparer.Ordinal)
                .First(person => !ProspectiveVoteFacade.DealsUnchecked(s).Any(row => DealStatus.Binds(row.status)
                    && row.type == priceType && row.targetId == null && row.expiresWeek == priceExpiry
                    && ((row.proposerId == s.playerId && row.recipientId == person.id)
                        || (row.proposerId == person.id && row.recipientId == s.playerId)))).id;
            var bought = new DealState { id = Negotiation.CounterDealPrefix + s.nextSequence, type = DealKind.VoteTogether,
                proposerId = s.playerId, recipientId = npc, status = DealStatus.Active, week = s.week, expiresWeek = s.week,
                trustImpact = DealTrust.Medium, linkedDealId = Negotiation.PricePrefix + s.nextSequence };
            var price = new DealState { id = Negotiation.PricePrefix + s.nextSequence, type = safetyPrice ? DealKind.SafetyAgreement : DealKind.FinalTwo,
                proposerId = s.playerId, recipientId = npc, status = DealStatus.Active, week = s.week,
                expiresWeek = safetyPrice ? s.week : 0, trustImpact = safetyPrice ? DealTrust.High : DealTrust.Critical, linkedDealId = bought.id };
            string before = Fingerprint(s), a = Fingerprint(bought), b = Fingerprint(price);
            // Detached local source-draft reservation, NOT proof an audible counter was offered.
            Assert.That(UnifiedVoteAdmission.TryCounterBundle(s, bought, price, out var plan, out var error), Is.True, error);
            Assert.That(plan.CanonicalAdditions.Count + plan.RawAdditions.Count, Is.EqualTo(2));
            Assert.That(plan.CanonicalAdditions.Count, Is.EqualTo(safetyPrice ? 2 : 1));
            Assert.That(plan.RawReplacement, Is.Null);
            plan.CanonicalAdditions[0].status = DealStatus.Expired;
            Assert.That(Fingerprint(s), Is.EqualTo(before)); Assert.That(Fingerprint(bought), Is.EqualTo(a)); Assert.That(Fingerprint(price), Is.EqualTo(b));
        }

        [TestCase(false)] [TestCase(true)]
        public void RawAtomicCounterIdentityRefusesBeforePrefixSelection(bool empty)
        {
            var s = Prospect(Game().Opening); string npc = s.contestants.First(person => !person.isPlayer).id;
            var bought = new DealState { id = empty ? string.Empty : null, type = DealKind.InformationSharing,
                proposerId = s.playerId, recipientId = npc, status = DealStatus.Active, week = s.week,
                expiresWeek = 0, trustImpact = DealKind.DefaultTrust(DealKind.InformationSharing),
                linkedDealId = Negotiation.PricePrefix + s.nextSequence };
            var price = new DealState { id = bought.linkedDealId, type = DealKind.VoteTogether,
                proposerId = s.playerId, recipientId = npc, status = DealStatus.Active, week = s.week,
                expiresWeek = s.week, trustImpact = DealTrust.Medium, linkedDealId = bought.id };
            string before = Fingerprint(s), a = Fingerprint(bought), b = Fingerprint(price);
            Assert.That(UnifiedVoteAdmission.TryCounterBundle(s, bought, price, out var plan, out var error), Is.False);
            Assert.That(plan, Is.Null); Assert.That(error, Is.Not.Null.And.Not.Empty);
            Assert.That(Fingerprint(s), Is.EqualTo(before)); Assert.That(Fingerprint(bought), Is.EqualTo(a)); Assert.That(Fingerprint(price), Is.EqualTo(b));
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
        [TestCase(6)] [TestCase(7)]
        public void DetachedMixedCounterStoredLinksRefuseConflatedOwnersAndTerms(int defect)
        {
            var s = Prospect(Game().Opening); s.nextSequence = 100;
            var bought = ShapeRow(s, 9); var price = ShapeRow(s, 10);
            bought.id = "deal-counter-90"; bought.subtype = DealKind.VoteEvict; bought.targetId = s.contestants[2].id;
            price.id = "deal-price-90"; price.subtype = DealKind.VoteTogether; price.targetId = null;
            bought.linkedCommitmentId = price.id; price.linkedCommitmentId = bought.id;
            s.unifiedCommitments.Add(bought); s.unifiedCommitments.Add(price); Accepted(s);
            switch (defect)
            {
                case 0: price.linkedCommitmentId = null; break;
                case 1: price.id = "deal-price-91"; bought.linkedCommitmentId = price.id; break;
                case 2: price.origin = UnifiedVoteFamilyValidation.OwnVetoPrice; break;
                case 3: bought.beneficiaryId = s.contestants[3].id; break;
                case 4: price.subtype = DealKind.VoteEvict; price.targetId = bought.targetId; break;
                case 5: bought.linkedCommitmentId = bought.id; break;
                case 6: price.createdWeek = 0; break;
                default: price.linkedCommitmentId = "unknown"; break;
            }
            Refused(s);
        }

        private sealed class Point
        {
            internal EpisodeState State; internal List<UnifiedVoteRevealState> Frames;
            // Only actual public before/after command observations populate this map. It is
            // not inferred from a terminal row or installed into the source season.
            internal Dictionary<string, ProspectiveVoteOwner> Owners;
        }
        private sealed class Played
        {
            internal Point Opening, Campaign, First;
        }
        private static Played Cached;
        private static Point PlayerRevealPoint;

        /// <summary>
        /// The first reveal after the genuine promise producer's Campaign, constructed: the real NPC
        /// batch, every NPC ballot pinned against the first nominee, the player's real ballot against
        /// the second (the walk's own choice), the real reveal. The box no longer rests on how the NPCs
        /// happen to vote; every verdict on it is still the source's.
        /// </summary>
        private static Point PlayerReveal()
        {
            if (PlayerRevealPoint != null) return PlayerRevealPoint;
            var witness = Producer(true);
            var walk = new PinnedVoteSeason(0, witness.Before.State);
            walk.Frames.AddRange(witness.Before.Frames.Select(frame => frame.Clone()));
            foreach (var pair in witness.Before.Owners) walk.Owners.Add(pair.Key, pair.Value.Clone());
            while (walk.Accepted < 512 && !PinnedVoteSeason.OpenVote(walk.State)) walk.Step(Next(walk.State));
            var s = walk.State;
            Assert.That(PinnedVoteSeason.OpenVote(s) && s.week == witness.Before.State.week && PinnedVoteSeason.PlayerVotes(s), Is.True,
                "The actual source Campaign player voter must supply the named-promise ballot.");
            Assert.That(PinnedVoteSeason.NpcVoters(s).Count(), Is.GreaterThanOrEqualTo(2), "The pinned box has no tie to break.");
            walk.RunNpcBatch();
            s = walk.State;
            walk.PinCast(PinnedVoteSeason.NpcVoters(s).ToDictionary(id => id, id => s.nominees[0], StringComparer.Ordinal));
            walk.CastVote(s.nominees[1]);
            var revealed = walk.Reveal();
            var power = revealed.ledger.power.Single(row => row.week == revealed.week);
            Assert.That(power.tally, Is.EqualTo(new List<int> { PinnedVoteSeason.NpcVoters(s).Count(), 1 }), "The designed count.");
            Assert.That(revealed.votes.Any(vote => vote.voterId == revealed.playerId), Is.True);
            PlayerRevealPoint = PointOf(revealed, walk.Frames); return PlayerRevealPoint;
        }
        private static Point AfterWeekTurn(Point point)
        {
            string sourceBefore = Fingerprint(point.State); var engine = new EpisodeEngine(point.State);
            for (int step = 0; step < 512 && engine.Snapshot.phase != EpisodePhase.Finished; step++)
            {
                var result = engine.Apply(Next(engine.Snapshot));
                Assert.That(result.accepted && !result.duplicate, Is.True, result.reason); SourceValid(result.state);
                if (result.state.week > point.State.week)
                {
                    Assert.That(Fingerprint(point.State), Is.EqualTo(sourceBefore));
                    return PointOf(result.state, point.Frames);
                }
            }
            Assert.Fail("The real post-reveal week turn was absent within512 accepted public steps; no date projection."); return null;
        }
        private sealed class RepeatedNomineeWitness { internal Point Point; internal int FirstWeek; internal string Maker; }
        private static RepeatedNomineeWitness RepeatedNominee;
        private static string RepeatedNomineeFailure;

        /// <summary>
        /// Two player-nominee frames sharing an ordinary NPC voter, constructed: seeds 1..32, the first
        /// success cached. The player competes at no effort (their own lawful input), and whenever they
        /// stand on the block at the vote every NPC ballot is pinned against the other nominee, so the
        /// player stays; everything else, including who nominates the player and when, is the source's.
        /// </summary>
        private static RepeatedNomineeWitness RepeatedPlayerNomineeFrames()
        {
            if (RepeatedNominee != null) return RepeatedNominee;
            if (RepeatedNomineeFailure != null) { Assert.Fail(RepeatedNomineeFailure); return null; }
            var diagnostics = new SearchDiagnostics();
            for (uint seed = 1; seed <= 32; seed++)
            {
                var walk = new PinnedVoteSeason(seed, Fresh(1, 8, seed));
                UnifiedVoteRevealState first = null;
                while (walk.Accepted < 512 && walk.State.phase != EpisodePhase.Finished && walk.State.Find(walk.State.playerId).status == ContestantStatus.Active)
                {
                    var before = walk.State;
                    if (!PinnedVoteSeason.OpenVote(before) || !before.nominees.Contains(before.playerId)) { Steer(walk, 0); continue; }
                    walk.PlayPinnedVote(before.nominees.First(id => id != before.playerId));
                    var after = walk.State; var frame = walk.Frames.Last();
                    var power = after.ledger.power.Single(row => row.week == frame.week);
                    Assert.That(power.evicteeId, Is.Not.EqualTo(after.playerId), "The pinned box keeps the player.");
                    if (first == null) { first = frame.Clone(); continue; }
                    var firstPower = after.ledger.power.Single(row => row.week == first.week);
                    string maker = first.ballots.Where(ballot => ballot.voterId != after.playerId && ballot.voterId != firstPower.hohId)
                        .Select(ballot => ballot.voterId).FirstOrDefault(id => frame.ballots.Any(ballot => ballot.voterId == id && id != power.hohId));
                    if (maker == null) { diagnostics.Command(seed, after, "no ordinary NPC voter shares both nominee frames"); continue; }
                    var point = PointOf(after, walk.Frames, walk.Owners); var candidate = Prospect(point);
                    if (!UnifiedVoteFamilyValidation.TryValidate(candidate, candidate.unifiedVoteReveals, out var familyError))
                    { diagnostics.Family(seed, after, familyError); continue; }
                    // This consumer is a detached family chronology control, not a complete
                    // saved-state claim. Record the complete source projection's real rejection
                    // as a diagnostic; never manufacture provenance to erase that rejection.
                    if (walk.Supported) CompleteProspectiveSourcePoint(point, diagnostics, seed);
                    TestContext.Out.WriteLine("Repeated nominee witness: seed " + seed + ", weeks " + first.week + " and " + frame.week
                        + ", maker " + maker + ", " + walk.Pins.Count + " pinned ballots.");
                    RepeatedNominee = new RepeatedNomineeWitness { Point = point, FirstWeek = first.week, Maker = maker };
                    return RepeatedNominee;
                }
                if (!walk.Supported) diagnostics.Unsupported(seed, walk.State, walk.FirstUnsupported);
            }
            RepeatedNomineeFailure = "Two constructed player nominee frames sharing an ordinary NPC voter were absent within32 seeds x512 accepted public commands; later targeted-survival evidence remains missing, not skipped or fabricated."
                + diagnostics;
            Assert.Fail(RepeatedNomineeFailure); return null;
        }
        private static Played Game()
        {
            if (Cached != null) return Cached;
            var walk = new PinnedVoteSeason(1, Fresh(1, 8, 1));
            var played = new Played { Opening = PointOf(walk.State, walk.Frames) };
            while (walk.Accepted < 512 && played.First == null)
            {
                var before = walk.State;
                if (before.phase == EpisodePhase.Campaign && played.Campaign == null) played.Campaign = PointOf(before, walk.Frames);
                var after = walk.Step(Next(before));
                if (!before.evictionResolved && after.evictionResolved) played.First = PointOf(after, walk.Frames);
            }
            Assert.That(played.First, Is.Not.Null, "Actual public reveal must occur within the fixed512 accepted-step bound.");
            Assert.That(played.Campaign, Is.Not.Null);
            Cached = played; return played;
        }
        private static EpisodeState Fresh(int mode, int size, uint seed) => PinnedVoteSeason.Fresh(seed, mode, size);
        private static EpisodeCommand Next(EpisodeState s)
        {
            var command = EpisodeEngineTests.NextCommand(s);
            if (command.kind == EpisodeCommandKind.CastVote && s.phase == EpisodePhase.Eviction) command.targetId = s.nominees[1];
            if (command.kind == EpisodeCommandKind.AnswerJury && EpisodeEngine.FinaleOn(s))
            {
                var exchange = s.juryExchanges[s.juryQuestionIndex];
                if (exchange.finalistId == s.playerId) command.secondTargetId = FinaleQuestions.Offered(exchange.category, exchange.receiptKind)[0];
            }
            return command;
        }

        /// <summary>
        /// One step of a constructed walk: at a vote the player stands on the block for, every NPC
        /// ballot is pinned against the other nominee; otherwise the walk's next command, competing
        /// with the given effort.
        /// </summary>
        private static void Steer(PinnedVoteSeason walk, double performance = 0.5)
        {
            var s = walk.State;
            if (PinnedVoteSeason.OpenVote(s) && s.nominees.Contains(s.playerId))
            { walk.PlayPinnedVote(s.nominees.First(id => id != s.playerId)); return; }
            var command = Next(s);
            if (command.kind == EpisodeCommandKind.Compete) command.performance = performance;
            walk.Step(command);
        }
        private sealed class SearchDiagnostics
        {
            internal string FirstFamily, FirstCore, FirstCommand, FirstUnsupported;
            private static string At(uint seed, EpisodeState s, string reason) =>
                "seed=" + seed + ", revision=" + s.revision + ", week=" + s.week + ", phase=" + s.phase + ": " + reason;
            internal void Family(uint seed, EpisodeState s, string reason)
            { if (FirstFamily == null) FirstFamily = At(seed, s, reason); }
            internal void Core(uint seed, EpisodeState s, string reason)
            { if (FirstCore == null) FirstCore = At(seed, s, reason); }
            internal void Command(uint seed, EpisodeState s, string reason)
            { if (FirstCommand == null) FirstCommand = At(seed, s, reason); }
            internal void Unsupported(uint seed, EpisodeState s, string reason)
            { if (FirstUnsupported == null) FirstUnsupported = At(seed, s, reason); }
            public override string ToString() => " First actual Family rejection: " + (FirstFamily ?? "none")
                + "; first actual whole-core rejection: " + (FirstCore ?? "none")
                + "; first actual command refusal or miss: " + (FirstCommand ?? "none")
                + "; first unsupported observed owner: " + (FirstUnsupported ?? "none") + ".";
        }
        private static Point PointOf(EpisodeState s, List<UnifiedVoteRevealState> frames,
            Dictionary<string, ProspectiveVoteOwner> owners = null) =>
            new Point { State = s.Clone(), Frames = frames.Select(frame => frame.Clone()).ToList(),
                Owners = owners?.ToDictionary(pair => pair.Key, pair => pair.Value.Clone(), StringComparer.Ordinal) };
        private static Point PointOf(PinnedVoteSeason walk) => PointOf(walk.State, walk.Frames, walk.Owners);
        private static EpisodeState Prospect(Point point)
        {
            string before = Fingerprint(point.State); var s = point.State.Clone();
            // Explicit DETACHED family controls: no owner calls, no save, no converter. Dropping
            // unrelated raw Vote rows here is not integration/backfill and does not prove typed
            // ledger/Story references. CompleteProspect separately retains every actual owner
            // and is mandatory wherever these tests claim a complete prospective source base.
            s.unifiedCommitmentRulesVersion = UnifiedVoteFamilyValidation.Version;
            s.promises.RemoveAll(row => row.kind == PromiseKind.Vote);
            s.deals.RemoveAll(row => KnownBallots.IsVoteDeal(row.type));
            s.unifiedVoteReveals = point.Frames.Select(frame => frame.Clone()).ToList();
            Assert.That(Fingerprint(point.State), Is.EqualTo(before));
            return s;
        }
        private static EpisodeState CompleteProspect(Point point)
        {
            Assert.That(point.Owners, Is.Not.Null, "Complete source projection requires actual before/after owner observations.");
            // ALL actual source owners retain the same IDs/links/terms/terminal stamps. Only the
            // detached representation moves; ledger/Story/jury references and source remain exact.
            return PinnedVoteSeason.Project(point.State, point.Owners, point.Frames);
        }
        private static UnifiedCommitmentState ActualPendingOffer(EpisodeState s, LateWitness witness)
        {
            var actual = s.unifiedCommitments.Single(row => row.id == witness.Offer.id);
            var expected = UnifiedVoteAdmission.FromDeal(witness.Offer, UnifiedCommitments.NpcOffer, 0, 0);
            Assert.That(Fingerprint(actual), Is.EqualTo(Fingerprint(expected)),
                "The existing projected pending owner must equal the actual Proposed source; do not add a duplicate.");
            return actual;
        }
        private static UnifiedCommitmentState ShapeRow(EpisodeState s, int index)
        {
            string[] origins = { UnifiedCommitments.PlayerPromise, UnifiedCommitments.NpcPromise, UnifiedCommitments.StoryPromise,
                UnifiedCommitments.NpcOffer, UnifiedCommitments.PlayerDeal, UnifiedCommitments.NpcDeal, UnifiedVoteFamilyValidation.VoteLobby,
                UnifiedVoteFamilyValidation.VetoAskPrice, UnifiedCommitments.StoryDeal, UnifiedCommitments.CounterDeal,
                UnifiedCommitments.CounterPrice, UnifiedVoteFamilyValidation.OwnVetoPrice };
            string origin = origins[index]; bool promise = index < 3, offered = index == 3;
            string maker = index == 1 || index == 2 || index == 3 || index == 5 || index == 6 || index == 7 || index == 11
                ? s.contestants[1].id : s.playerId;
            string beneficiary = index == 3 || index == 6 || index == 7 || index == 11 ? s.playerId : s.contestants[2].id;
            bool together = index == 4 || index == 8 || index == 9 || index == 10;
            var row = new UnifiedCommitmentState { kind = UnifiedVoteTogether.Vote, sourcePolicy = promise ? UnifiedCommitments.PromisePolicy : UnifiedCommitments.DealPolicy,
                origin = origin, id = FreeId(s, ProspectiveVoteFacade.Prefix(origin)), makerId = maker, beneficiaryId = beneficiary,
                reciprocal = !promise, createdWeek = s.week, expiresWeek = index == 7 || index == 11 ? 0 : s.week,
                status = offered ? DealStatus.Proposed : DealStatus.Active, trustImpact = DealTrust.Medium,
                subtype = promise ? null : together ? DealKind.VoteTogether : DealKind.VoteSave,
                targetId = index == 2 || together ? null : index == 3 || index == 5 ? maker
                    : index == 6 || index == 7 || index == 11 ? s.playerId : s.contestants[3].id,
                voteBindingWeek = offered ? 0 : s.week, voteFirstRevealWeek = offered ? 0 : s.week };
            if (index == 7 || index == 9 || index == 10 || index == 11) row.linkedCommitmentId = "linked-owner";
            return row;
        }
        private static UnifiedCommitmentState TerminalRow(EpisodeState s, int duty)
        {
            var frame = s.unifiedVoteReveals.Last(); var power = s.ledger.power.Single(row => row.week == frame.week);
            string maker = duty < 2 ? s.playerId : frame.ballots.First(ballot => ballot.voterId != power.hohId).voterId;
            string beneficiary = frame.ballots.First(ballot => ballot.voterId != maker && ballot.voterId != power.hohId).voterId;
            var row = new UnifiedCommitmentState { id = FreeId(s, duty < 2 ? "promise-" : "deal-story-"),
                kind = UnifiedVoteTogether.Vote, sourcePolicy = duty < 2 ? UnifiedCommitments.PromisePolicy : UnifiedCommitments.DealPolicy,
                origin = duty < 2 ? UnifiedCommitments.PlayerPromise : UnifiedCommitments.StoryDeal,
                makerId = maker, beneficiaryId = beneficiary, reciprocal = duty >= 2, createdWeek = frame.week,
                expiresWeek = frame.week, voteBindingWeek = frame.week, voteFirstRevealWeek = frame.week,
                trustImpact = DealTrust.Medium, status = DealStatus.Active,
                subtype = duty < 2 ? null : duty < 4 ? DealKind.VoteTogether : duty == 4 ? DealKind.VoteSave : DealKind.VoteEvict,
                targetId = duty < 2 ? (duty == 0 ? frame.ballots.First(ballot => ballot.voterId == maker).targetId
                    : power.nominees.First(id => id != frame.ballots.First(ballot => ballot.voterId == maker).targetId))
                    : duty < 4 ? null : power.nominees[0] };
            string status, actor;
            if (row.subtype == DealKind.VoteTogether)
            {
                Assert.That(UnifiedVoteTogether.TryVerdict(row, frame.ballots, out status, out var reason), Is.True, reason); actor = null;
            }
            else
            {
                Assert.That(UnifiedVoteObligations.TryVerdict(row, frame.ballots, power.nominees, out var verdict, out var reason), Is.True, reason);
                Assert.That(verdict, Is.Not.Null); status = verdict.Status; actor = verdict.ActorId;
            }
            Assert.That(status, Is.Not.Null); row.status = status; row.settledWeek = frame.week;
            if (status == DealStatus.Broken) { row.brokenById = actor; row.settlementEffectKey = UnifiedVoteHistory.Key(row, frame.week); }
            return row;
        }
        private static string FreeId(EpisodeState s, string prefix)
        {
            var ids = new HashSet<string>(s.promises.Select(row => row.id).Concat(s.deals.Select(row => row.id)).Concat(s.unifiedCommitments.Select(row => row.id)));
            for (long n = 1; n < s.nextSequence; n++) if (!ids.Contains(prefix + n)) return prefix + n;
            // Capacity fixtures explicitly set a detached nextSequence, not a source command claim.
            Assert.Fail("No consumed detached identity slot exists in the actual retained source point."); return null;
        }
        private sealed class ProducerWitness { internal Point Before; internal PromiseState Promise; internal DealState Deal; }
        private static readonly Dictionary<bool, ProducerWitness> Producers = new Dictionary<bool, ProducerWitness>();
        private static readonly Dictionary<bool, string> ProducerFailures = new Dictionary<bool, string>();

        /// <summary>
        /// The player's genuine promise or Together proposal, on a constructed walk: seeds 1..32, the
        /// first success cached. The walk keeps the player in the house by pinned ballots when they are
        /// nominated (<see cref="Steer"/>); at the first Campaign the producer can act in (a voter, for
        /// the promise) whose complete projection is valid, the player's real command goes to each
        /// active NPC in ordinal order until the source writes the row.
        /// </summary>
        private static ProducerWitness Producer(bool promise)
        {
            if (Producers.TryGetValue(promise, out var cached)) return cached;
            if (ProducerFailures.TryGetValue(promise, out var priorFailure)) { Assert.Fail(priorFailure); return null; }
            var diagnostics = new SearchDiagnostics();
            for (uint seed = 1; seed <= 32; seed++)
            {
                var walk = new PinnedVoteSeason(seed, Fresh(1, 8, seed));
                while (walk.Accepted < 512 && walk.State.phase != EpisodePhase.Finished && walk.State.Find(walk.State.playerId).status == ContestantStatus.Active)
                {
                    var s = walk.State;
                    if (s.phase == EpisodePhase.Campaign && s.pendingDiary == null && (!promise || PinnedVoteSeason.PlayerVotes(s))
                        && walk.Supported && CompleteProspectiveSourcePoint(PointOf(walk), diagnostics, seed))
                    {
                        foreach (var npc in s.contestants.Where(person => !person.isPlayer && person.status == ContestantStatus.Active).OrderBy(person => person.id, StringComparer.Ordinal))
                        {
                            if (walk.Accepted >= 512) break;
                            s = walk.State; var beforePoint = PointOf(walk);
                            if (!walk.Supported || !CompleteProspectiveSourcePoint(beforePoint, diagnostics, seed)) break;
                            if (!promise && !PlayerDeals.CanPropose(s, npc.id, DealKind.VoteTogether, null, out _)) continue;
                            string target = promise ? s.nominees.FirstOrDefault(id => id != npc.id) : null;
                            if (promise && (target == null || s.promises.Any(row => row.kind == PromiseKind.Vote && row.fromId == s.playerId && row.toId == npc.id && row.status == PromiseStatus.Active))) continue;
                            var command = EpisodeEngineTests.Command(s, promise ? EpisodeCommandKind.PromiseVote : EpisodeCommandKind.ProposeDeal);
                            command.id = "prospective-source-" + seed + "-" + s.revision + "-" + npc.id;
                            command.targetId = npc.id; command.secondTargetId = target;
                            // ProposeDeal names its kind in text, as the actual public command does.
                            if (!promise) command.text = DealKind.VoteTogether;
                            var result = walk.Apply(command);
                            if (!result.accepted) { diagnostics.Command(seed, s, command.kind + ": " + result.reason); continue; }
                            var producedPromise = result.state.promises.FirstOrDefault(row => row.id == "promise-" + s.nextSequence && !s.promises.Any(old => old.id == row.id));
                            var producedDeal = result.state.deals.FirstOrDefault(row => row.id == "deal-player-" + s.nextSequence && !s.deals.Any(old => old.id == row.id));
                            if (promise ? producedPromise != null : producedDeal != null)
                            {
                                var witness = new ProducerWitness { Before = beforePoint,
                                    Promise = producedPromise?.Clone(), Deal = producedDeal?.Clone() };
                                TestContext.Out.WriteLine("Producer(" + promise + ") witness: seed " + seed + ", week " + s.week + ", partner " + npc.id
                                    + ", " + walk.Pins.Count + " pinned ballots.");
                                Producers.Add(promise, witness); return witness;
                            }
                        }
                    }
                    if (walk.Accepted >= 512) break;
                    Steer(walk);
                }
                if (!walk.Supported) diagnostics.Unsupported(seed, walk.State, walk.FirstUnsupported);
            }
            string failure = "Required ACTUAL public producer is missing within32 seeds x512 accepted public commands; no fabricated or skipped witness."
                + diagnostics;
            ProducerFailures.Add(promise, failure); Assert.Fail(failure); return null;
        }
        private sealed class LateWitness { internal Point Before; internal DealState Offer, ActuallyAnswered; internal OpportunityRow ActuallyAnsweredOpportunity; }
        private static readonly Dictionary<bool, LateWitness> LateWitnesses = new Dictionary<bool, LateWitness>();
        private static readonly Dictionary<bool, string> LateFailures = new Dictionary<bool, string>();

        /// <summary>
        /// A genuine NPC VoteSave offer to the player answered late, on a constructed walk: seeds 1..32,
        /// the first success cached. The offer and its round are the source's own (an NPC's offer is
        /// never constructed); the walk keeps the player in the house by pinned ballots when they are
        /// nominated, and answers yes at the first boundary - after the reveal, or in a later week
        /// before the vote - where the offer still stands and the complete projection is valid.
        /// </summary>
        private static LateWitness LateOffer(bool afterReveal)
        {
            if (LateWitnesses.TryGetValue(afterReveal, out var retained)) return retained;
            if (LateFailures.TryGetValue(afterReveal, out var priorFailure)) { Assert.Fail(priorFailure); return null; }
            var diagnostics = new SearchDiagnostics();
            for (uint seed = 1; seed <= 32; seed++)
            {
                var walk = new PinnedVoteSeason(seed, Fresh(1, 8, seed));
                while (walk.Accepted < 512 && walk.State.phase != EpisodePhase.Finished && walk.State.Find(walk.State.playerId).status == ContestantStatus.Active)
                {
                    var s = walk.State;
                    bool boundary = afterReveal ? s.phase == EpisodePhase.Eviction && s.evictionResolved
                        : s.week > 1 && !s.evictionResolved && s.phase != EpisodePhase.Campaign && s.phase != EpisodePhase.Eviction;
                    var offer = boundary ? s.deals.FirstOrDefault(row => row.type == DealKind.VoteSave && row.status == DealStatus.Proposed
                        && row.id.StartsWith(NpcDeals.OfferPrefix, StringComparison.Ordinal) && row.recipientId == s.playerId
                        && s.Find(s.playerId).status == ContestantStatus.Active && s.Find(row.proposerId).status == ContestantStatus.Active
                        && (afterReveal || row.week < s.week)) : null;
                    if (offer != null && walk.Supported && CompleteProspectiveSourcePoint(PointOf(walk), diagnostics, seed))
                    {
                        var before = PointOf(walk);
                        var command = EpisodeEngineTests.Command(s, EpisodeCommandKind.RespondToDeal);
                        command.id = "actual-late-answer-" + seed + "-" + s.revision;
                        command.targetId = offer.id; command.text = EpisodeEngine.AcceptDeal;
                        string raw = Fingerprint(offer); var after = walk.Step(command);
                        Assert.That(Fingerprint(offer), Is.EqualTo(raw));
                        var answered = after.deals.Single(row => row.id == offer.id);
                        Assert.That(answered.status, Is.EqualTo(DealStatus.Active)); Assert.That(answered.week, Is.EqualTo(offer.week));
                        Assert.That(answered.expiresWeek, Is.EqualTo(s.week));
                        var found = new LateWitness { Before = before, Offer = offer.Clone(), ActuallyAnswered = answered.Clone(),
                            ActuallyAnsweredOpportunity = after.ledger.opportunities.Single(row => row.id == offer.id).Clone() };
                        TestContext.Out.WriteLine("LateOffer(" + afterReveal + ") witness: seed " + seed + ", week " + s.week + ", " + s.phase
                            + ", offer " + offer.id + " of week " + offer.week + ", " + walk.Pins.Count + " pinned ballots.");
                        LateWitnesses.Add(afterReveal, found); return found;
                    }
                    Steer(walk);
                }
                if (!walk.Supported) diagnostics.Unsupported(seed, walk.State, walk.FirstUnsupported);
            }
            string failure = "Required actual late NPC answer was absent within32 seeds x512 accepted public commands; no skip or inferred answer."
                + diagnostics;
            LateFailures.Add(afterReveal, failure); Assert.Fail(failure); return null;
        }
        private static bool CompleteProspectiveSourcePoint(Point point, SearchDiagnostics diagnostics, uint seed)
        {
            string before = Fingerprint(point.State);
            var candidate = CompleteProspect(point); string candidateBefore = Fingerprint(candidate);
            bool family = UnifiedVoteFamilyValidation.TryValidate(candidate, candidate.unifiedVoteReveals, out var familyError);
            if (!family) diagnostics.Family(seed, point.State, familyError);
            bool valid = ProspectiveVoteFacade.TryValidateProspectiveUnifiedVote(candidate, out var coreError);
            if (!valid) diagnostics.Core(seed, point.State, coreError);
            Assert.That(Fingerprint(candidate), Is.EqualTo(candidateBefore));
            Assert.That(Fingerprint(point.State), Is.EqualTo(before));
            return valid;
        }
        private static void SourceValid(EpisodeState s) => Assert.That(EpisodeValidation.TryValidate(s, out var error), Is.True, error);
        private static void Accepted(EpisodeState s) => Check(s, true);
        private static void Refused(EpisodeState s) => Check(s, false);
        private static void Check(EpisodeState s, bool expected)
        {
            string before = Fingerprint(s);
            Assert.That(UnifiedVoteFamilyValidation.TryValidate(s, s.unifiedVoteReveals, out var error), Is.EqualTo(expected), error);
            if (!expected) Assert.That(error, Is.Not.Null.And.Not.Empty);
            Assert.That(Fingerprint(s), Is.EqualTo(before));
        }
        private static string Fingerprint(object value)
        {
            var text = new StringBuilder(); Append(text, value); return text.ToString();
        }
        private static void Append(StringBuilder text, object value)
        {
            if (value == null) { text.Append("null;"); return; }
            var type = value.GetType(); text.Append(type.FullName).Append(':');
            if (type == typeof(string)) { string str = (string)value; text.Append(str.Length).Append(':').Append(str).Append(';'); return; }
            if (type.IsPrimitive || type.IsEnum || type == typeof(decimal))
            { text.Append(Convert.ToString(value, CultureInfo.InvariantCulture)).Append(';'); return; }
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(KeyValuePair<,>))
            { Append(text, type.GetProperty("Key").GetValue(value)); Append(text, type.GetProperty("Value").GetValue(value)); return; }
            if (value is IEnumerable sequence)
            { text.Append('['); foreach (var item in sequence) Append(text, item); text.Append(']'); return; }
            foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public).OrderBy(field => field.Name, StringComparer.Ordinal))
            { text.Append(field.Name).Append('='); Append(text, field.GetValue(value)); }
        }
    }
}
