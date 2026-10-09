using System;
using System.Linq;

namespace Gamesim.Simulation
{
    public static partial class EpisodeValidation
    {
        /// <summary>
        /// Typed saved references after the complete common core and aggregate archive proof.
        /// This does not publish an outcome, grant ballot knowledge, select an effect owner,
        /// or infer that an historical command was played from a lawful detached row.
        /// </summary>
        private static bool TryValidateUnifiedVoteReferences(EpisodeState s, out string error)
        {
            error = null;
            var canonical = s.unifiedCommitments.Where(row => row.kind == UnifiedVoteTogether.Vote)
                .ToDictionary(row => row.id, StringComparer.Ordinal);
            foreach (var opportunity in s.ledger.opportunities)
            {
                if (!canonical.TryGetValue(opportunity.id, out var row)) continue;
                if (opportunity.kind != OpportunityKinds.Deal || row.sourcePolicy != UnifiedCommitments.DealPolicy
                    || (row.makerId != s.playerId && row.beneficiaryId != s.playerId)
                    || opportunity.week != row.createdWeek || !ValidSavedVoteOpportunity(s, row, opportunity))
                    return Fail(out error, "A Vote opportunity must retain its actual player-party Deal source and projection.");
            }
            // A private ballot truth is not a Safety nomination fact or hearing lineage.
            // Other Story fact families keep the original complete Story validator's rules.
            if (s.story.facts.Any(fact => fact.kind == FactKinds.BrokenWord
                    && fact.refId != null && canonical.ContainsKey(fact.refId))
                || s.unifiedHearingEvidence.Any(evidence => canonical.ContainsKey(evidence.fact.refId ?? "")))
                return Fail(out error, "Vote truth cannot be installed as a Safety BrokenWord fact or hearing.");

            foreach (var question in s.juryExchanges)
            {
                if (question.receiptId == null || !canonical.TryGetValue(question.receiptId, out var row)) continue;
                if (!ValidSavedVoteJuryReceipt(s, row, question))
                    return Fail(out error, "A Vote jury receipt must resolve its real source, category, parties and known decision.");
            }
            return true;
        }

        private static bool ValidSavedVoteOpportunity(EpisodeState s, UnifiedCommitmentState row, OpportunityRow opportunity)
        {
            if (opportunity.anchor != null || opportunity.currency != null || opportunity.steps.Count != 0
                || opportunity.payoff != 0) return false;
            bool taken = opportunity.response == OpportunityResponse.Taken;
            bool neutral = opportunity.outcome == OpportunityOutcome.NotApplicable;
            bool answered = row.voteBindingWeek > 0 && (row.status == DealStatus.Active || row.status == DealStatus.Broken
                || row.status == DealStatus.Fulfilled || row.status == DealStatus.Expired);
            // Actual immediate command-created opportunities precede reconciliation. Generic
            // NPC/Story/Lobby creation does not fabricate an immediate Taken opportunity.
            if (opportunity.source == null && opportunity.note == null)
                return taken && neutral && answered && (row.origin == UnifiedCommitments.PlayerDeal
                    || row.origin == UnifiedCommitments.NpcOffer || row.origin == UnifiedCommitments.CounterDeal
                    || row.origin == UnifiedCommitments.CounterPrice || row.origin == UnifiedVoteFamilyValidation.VetoAskPrice
                    || row.origin == UnifiedVoteFamilyValidation.OwnVetoPrice);
            string source = row.subtype + (row.targetId != null ? ":" + row.targetId : "");
            if (opportunity.source != source || opportunity.note == null) return false;
            string prefix = (row.makerId == s.playerId ? "put to " + row.beneficiaryId
                : "offered by " + row.makerId) + ", ";
            if (!opportunity.note.StartsWith(prefix, StringComparison.Ordinal)) return false;
            string observed = opportunity.note.Substring(prefix.Length);
            // A real yes changes Taken only until the next reconciliation. It does not replace
            // the retained creation week, previous Proposed note or answering-week markers.
            if (observed == DealStatus.Proposed)
                return row.origin == UnifiedCommitments.NpcOffer && neutral
                    && ((opportunity.response == OpportunityResponse.Ignored && row.voteBindingWeek == 0
                            && (row.status == DealStatus.Proposed || row.status == DealStatus.Declined || row.status == DealStatus.Expired))
                        || (taken && answered));
            if (observed == DealStatus.Active) return taken && neutral && answered;
            if (observed == DealStatus.Declined)
                return row.status == DealStatus.Declined && row.voteBindingWeek == 0
                    && opportunity.response == OpportunityResponse.Declined && neutral;
            if (observed == DealStatus.Expired)
                return row.status == DealStatus.Expired && neutral && (opportunity.response == OpportunityResponse.Expired
                    || (taken && answered && EpisodeEngine.CommitmentRulesOn(s)));
            if (observed == DealStatus.Fulfilled)
                return row.status == DealStatus.Fulfilled && taken && opportunity.outcome == OpportunityOutcome.Won;
            if (observed == DealStatus.Broken)
                return row.status == DealStatus.Broken && taken && opportunity.outcome == OpportunityOutcome.Lost;
            return false;
        }

        private static bool ValidSavedVoteJuryReceipt(EpisodeState s, UnifiedCommitmentState row, JuryExchangeState question)
        {
            var decision = UnifiedVoteHistory.FindDecision(s, row.id);
            if (decision == null || decision.SettledWeek != row.settledWeek || decision.Status != row.status) return false;
            // The lead's decision D7 (vote family V5e): a receipt is its Rule2 group's owner, as the questions choose one
            // (FinaleQuestions.Receipts) - never a row that owned no consequence of its reveal. And the owner of the receipt's
            // own direction (the pre-V6 save-contract review): an Accountability receipt is the owner of the player's breach
            // of the juror, never of the juror's of the player, which a deal both of them broke can own alone; a Personal
            // one the owner of a kept group between the two.
            string player = s.playerId, juror = question.questionerId;
            var groups = row.status == DealStatus.Broken ? UnifiedVoteHistory.Incidents(s) : UnifiedVoteHistory.Fulfillments(s);
            bool accountability = question.category == FinaleQuestions.Accountability;
            if (!groups.Any(group => group.OwnerId == row.id && (group.ActorId == player && group.WrongedId == juror
                    || !accountability && group.ActorId == juror && group.WrongedId == player)))
                return false;
            if (question.receiptKind == FinaleQuestions.PromiseReceipt)
            {
                if (row.sourcePolicy != UnifiedCommitments.PromisePolicy || row.makerId != player || row.beneficiaryId != juror)
                    return false;
                return question.category == FinaleQuestions.Accountability
                    ? row.status == DealStatus.Broken && decision.ActorId == player
                    : question.category == FinaleQuestions.Personal && row.status == DealStatus.Fulfilled;
            }
            if (question.receiptKind != FinaleQuestions.DealReceipt || row.sourcePolicy != UnifiedCommitments.DealPolicy
                || !((row.makerId == player && row.beneficiaryId == juror) || (row.makerId == juror && row.beneficiaryId == player)))
                return false;
            var deal = CommitmentReferences.FindDeal(s, row.id);
            if (deal == null) return false;
            // Agreement-local truth proves no grouped reward/incident owner. Keep the source's
            // independent player-knowledge and actor reader; never read the archive as knowledge.
            return question.category == FinaleQuestions.Accountability
                ? row.status == DealStatus.Broken && FinalistRead.DealBreaker(s, deal) == player
                : question.category == FinaleQuestions.Personal && row.status == DealStatus.Fulfilled
                    && KnownBallots.DealOutcomeKnown(s, deal);
        }
    }
}
