using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Gamesim.Simulation
{
    public static partial class EpisodeValidation
    {
        /// <summary>
        /// Saved-reference leaves after complete common validation and source-owned Safety checks.
        /// Read-only and internal: this neither activates rules nor normalizes, mirrors or repairs
        /// evidence. A saved projection may legitimately lag its source until reconciliation.
        /// </summary>
        private static bool TryValidateUnifiedSafetyReferences(EpisodeState s, out string error)
        {
            error = null;
            var canonical = s.unifiedCommitments.ToDictionary(row => row.id, StringComparer.Ordinal);
            var deals = CommitmentReferences.Deals(s).ToDictionary(row => row.id, StringComparer.Ordinal);
            foreach (var opportunity in s.ledger.opportunities)
            {
                bool own = canonical.TryGetValue(opportunity.id, out var record);
                if (own && opportunity.kind != OpportunityKinds.Deal)
                    return Fail(out error, "A Safety opportunity must retain its actual Deal family.");
                if (opportunity.kind != OpportunityKinds.Deal) continue;
                if (!deals.TryGetValue(opportunity.id, out var deal)
                    || (deal.proposerId != s.playerId && deal.recipientId != s.playerId)
                    || opportunity.week != deal.week)
                    return Fail(out error, "A Deal opportunity must resolve its own player-party source and creation week.");
                if (own && !ValidSavedSafetyOpportunity(s, record, deal, opportunity))
                    return Fail(out error, "The Safety opportunity is not an actual immediate or historical source projection.");
            }

            var incidents = UnifiedCommitmentHistory.Breaches(s);
            foreach (var fact in s.story.facts.Where(f => f.kind == FactKinds.BrokenWord && f.refId != null))
                // Generic story facts name their cycle (which may later be pruned), or nothing.
                // Actual commitment-shaped BrokenWord provenance names a permanent Deal owner;
                // erasing that owner must not turn it into an unrelated generic story fact.
                if (!deals.ContainsKey(fact.refId) && (fact.refId.StartsWith("deal-", StringComparison.Ordinal)
                    || fact.refId.StartsWith("promise-", StringComparison.Ordinal)
                    || s.promises.Any(p => p.id == fact.refId)))
                    return Fail(out error, "A commitment-shaped BrokenWord fact lost its true Deal-family owner.");
            var canonicalFacts = s.story.facts.Where(fact => canonical.ContainsKey(fact.refId ?? "")).ToArray();
            foreach (var fact in canonicalFacts)
                if (!ValidSavedSafetyFact(s, canonical[fact.refId], fact, incidents))
                    return Fail(out error, "A canonical fact must be its actual audible Deal-policy breach leaf.");
            if (canonicalFacts.GroupBy(fact => fact.refId, StringComparer.Ordinal).Any(group => group.Count() > 1))
                return Fail(out error, "A Safety source cannot have two live BrokenWord identities.");
            if (!UnifiedCommitmentHearings.ValidateStorage(s, out error)) return false;
            // A lawful live-fact pruning keeps the same permanent source leaf. Archived-only
            // facts need settlement-time knowledge eligibility too, not merely today's rules.
            foreach (var evidence in s.unifiedHearingEvidence)
                if (!canonical.TryGetValue(evidence.fact.refId, out var archivedOwner)
                    || !ValidSavedSafetyFact(s, archivedOwner, evidence.fact, incidents))
                    return Fail(out error, "An archived Safety fact must retain its actual audible settlement provenance.");
            if (UnifiedCommitmentHearings.RulesOn(s))
            {
                // Under the fresh hearing-1 writer contract, every selected player Deal breach
                // audible at its recorded settlement installed permanent archive/Initial lineage.
                // Live facts can roll off; these bounded archives and receipts cannot. Iterating
                // incidents also catches deletion of the fact, archive and Initial together.
                // Promise owners, weaker aliases, NPC breakers and pre-knowledge settlements
                // acquire no inferred emission or witness from this completed-state invariant.
                foreach (var incident in incidents)
                {
                    var owner = canonical[incident.EffectOwnerId];
                    if (incident.ActorId != s.playerId || owner.sourcePolicy != UnifiedCommitments.DealPolicy
                        || owner.status != DealStatus.Broken || !SavedSafetyWordWasOn(s, owner.settledWeek)) continue;
                    var evidence = s.unifiedHearingEvidence.FirstOrDefault(e => e.incidentKey == incident.EffectKey
                        && e.fact.refId == owner.id);
                    if (evidence == null || evidence.fact.actorId != incident.ActorId
                        || evidence.fact.subjectId != incident.WrongedId || evidence.fact.week != owner.settledWeek
                        || !s.unifiedHearingReceipts.Any(r => r.kind == UnifiedCommitmentHearings.Initial
                            && r.incidentKey == incident.EffectKey && r.factId == evidence.fact.id
                            && r.listenerId == incident.WrongedId && r.heardWeek == owner.settledWeek))
                        return Fail(out error, "The selected audible Deal incident lost its permanent emission lineage.");
                }
                // Knowledge.BrokenWord installs its selected Deal emission, archive and Initial
                // receipt atomically. Check that completed writer invariant here, never inside
                // ValidateStorage, which also validates the writer's legitimate intermediate state.
                // A weaker audible alias proves no Initial and a Public fact proves no hearings.
                foreach (var fact in canonicalFacts.Concat(s.unifiedHearingEvidence.Select(e => e.fact)))
                {
                    var incident = incidents.FirstOrDefault(i => i.EvidenceIds.Contains(fact.refId));
                    if (incident == null || incident.EffectOwnerId != fact.refId
                        || canonical[fact.refId].sourcePolicy != UnifiedCommitments.DealPolicy) continue;
                    var evidence = s.unifiedHearingEvidence.FirstOrDefault(e => e.fact.id == fact.id);
                    if (evidence == null || evidence.incidentKey != incident.EffectKey
                        || !s.unifiedHearingReceipts.Any(r => r.kind == UnifiedCommitmentHearings.Initial
                            && r.incidentKey == incident.EffectKey && r.factId == fact.id
                            && r.listenerId == incident.WrongedId
                            && r.heardWeek == canonical[fact.refId].settledWeek))
                        return Fail(out error, "The selected audible Deal emission lost its atomic archive or Initial receipt.");
                }
            }

            var brokenOwners = new HashSet<string>(incidents.Select(i => i.EffectOwnerId), StringComparer.Ordinal);
            var keptOwners = new HashSet<string>(UnifiedCommitmentHistory.Fulfillments(s)
                .Select(receipt => receipt.EffectOwnerId), StringComparer.Ordinal);
            foreach (var question in s.juryExchanges.Where(q => q.receiptKind != null))
            {
                if (!ValidSavedSafetyJuryReceipt(s, question, canonical, brokenOwners, keptOwners))
                    return Fail(out error, "The saved jury receipt does not resolve its actual category, parties and source evidence.");
                // Source-qualified wording is narrower than the category's catalog. Resolve the
                // actual source first; never use ReceiptLine's privacy-safe rendering as validation
                // or regenerate today's preferred/latest receipt to replace a saved question.
                var receipt = new FinaleQuestions.Receipt { category = question.category, kind = question.receiptKind,
                    id = question.receiptId, week = FinaleQuestions.ReceiptWeek(s, question) ?? 0 };
                if (!FinaleQuestions.QuestionsFor(s, question.category, receipt, question.questionerId).Contains(question.question))
                    return Fail(out error, "The saved jury question claims more than its actual receipt proves.");
            }
            // TryValidateV2 already uses FinalArgument.Resolves: locks must still name their own
            // family/party source, not still qualify as a selectable or strongest scoring moment.
            return true;
        }

        private static bool ValidSavedSafetyOpportunity(EpisodeState s, UnifiedCommitmentState record,
            DealState deal, OpportunityRow opportunity)
        {
            if (record.sourcePolicy != UnifiedCommitments.DealPolicy || opportunity.anchor != null
                || opportunity.currency != null || opportunity.steps.Count != 0 || opportunity.payoff != 0) return false;
            bool taken = opportunity.response == OpportunityResponse.Taken;
            bool neutral = opportunity.outcome == OpportunityOutcome.NotApplicable;
            bool answered = record.status == DealStatus.Active || record.status == DealStatus.Broken
                || record.status == DealStatus.Fulfilled || record.status == DealStatus.Expired;
            if (opportunity.source == null && opportunity.note == null)
                return taken && neutral && answered && (record.origin == UnifiedCommitments.PlayerDeal
                    || record.origin == UnifiedCommitments.NpcOffer || record.origin == UnifiedCommitments.CounterDeal
                    || record.origin == UnifiedCommitments.CounterPrice);
            if (opportunity.source != DealKind.SafetyAgreement || opportunity.note == null) return false;
            string prefix = (deal.proposerId == s.playerId ? "put to " + deal.recipientId : "offered by " + deal.proposerId) + ", ";
            if (!opportunity.note.StartsWith(prefix, StringComparison.Ordinal)) return false;
            string observed = opportunity.note.Substring(prefix.Length);
            // Responses are not eager snapshots. Accepting an NPC offer updates Taken only; it
            // leaves the preceding Proposed source/note until the next actual reconciliation.
            if (observed == DealStatus.Proposed)
                return record.origin == UnifiedCommitments.NpcOffer && neutral
                    && ((opportunity.response == OpportunityResponse.Ignored
                            && (record.status == DealStatus.Proposed || record.status == DealStatus.Declined
                                || record.status == DealStatus.Expired))
                        || (taken && answered));
            if (observed == DealStatus.Active)
                return answered && taken && neutral;
            if (observed == DealStatus.Declined)
                return record.status == DealStatus.Declined && opportunity.response == OpportunityResponse.Declined && neutral;
            if (observed == DealStatus.Expired)
                return record.status == DealStatus.Expired && neutral
                    && (opportunity.response == OpportunityResponse.Expired || (taken && EpisodeEngine.CommitmentRulesOn(s)));
            if (observed == DealStatus.Fulfilled)
                return record.status == DealStatus.Fulfilled && taken && opportunity.outcome == OpportunityOutcome.Won;
            if (observed == DealStatus.Broken)
                return record.status == DealStatus.Broken && taken && opportunity.outcome == OpportunityOutcome.Lost;
            return false;
        }

        private static bool ValidSavedSafetyFact(EpisodeState s, UnifiedCommitmentState record, HouseFactState fact,
            IReadOnlyList<UnifiedCommitmentIncident> incidents)
        {
            if (record.sourcePolicy != UnifiedCommitments.DealPolicy || record.status != DealStatus.Broken
                || !SavedSafetyWordWasOn(s, record.settledWeek) || fact.kind != FactKinds.BrokenWord || fact.actorId != s.playerId
                || fact.week != record.settledWeek || fact.visibility == FactVisibility.Private
                || !SavedSafetySequence(fact.id, "fact-", s.nextSequence)
                || !fact.knowers.Contains(fact.actorId) || !fact.knowers.Contains(fact.subjectId)) return false;
            return incidents.Any(i => i.ActorId == fact.actorId && i.WrongedId == fact.subjectId
                && i.EvidenceIds.Contains(fact.refId));
        }

        // Source knowledge eligibility belongs to the actual settlement week, not today's week.
        // Read recorded boundaries directly; never mutate the state or retrofit old witnesses.
        private static bool SavedSafetyWordWasOn(EpisodeState s, int week) =>
            s.commitmentRulesStartWeek >= 1 && s.commitmentRulesStartWeek <= week
            && s.story.rulesStartWeek >= 1 && s.story.rulesStartWeek <= week
            && s.story.rulesVersion >= StoryRules.Bonds;

        private static bool ValidSavedSafetyJuryReceipt(EpisodeState s, JuryExchangeState q,
            IReadOnlyDictionary<string, UnifiedCommitmentState> canonical, ISet<string> brokenOwners, ISet<string> keptOwners)
        {
            string player = s.playerId, juror = q.questionerId;
            switch (q.receiptKind)
            {
                case FinaleQuestions.PromiseReceipt:
                    var promise = CommitmentReferences.FindPromise(s, q.receiptId);
                    if (promise == null || promise.fromId != player || promise.toId != juror) return false;
                    if (q.category == FinaleQuestions.Accountability)
                        return promise.status == PromiseStatus.Broken
                            && (!canonical.ContainsKey(promise.id) || brokenOwners.Contains(promise.id));
                    return q.category == FinaleQuestions.Personal && promise.status == PromiseStatus.Fulfilled;
                case FinaleQuestions.DealReceipt:
                    var deal = CommitmentReferences.FindDeal(s, q.receiptId);
                    if (deal == null || !((deal.proposerId == player && deal.recipientId == juror)
                        || (deal.proposerId == juror && deal.recipientId == player))) return false;
                    if (q.category == FinaleQuestions.Accountability)
                        return deal.status == DealStatus.Broken && FinalistRead.DealBreaker(s, deal) == player
                            && (!canonical.ContainsKey(deal.id) || brokenOwners.Contains(deal.id));
                    return q.category == FinaleQuestions.Personal && deal.status == DealStatus.Fulfilled
                        && (!EpisodeEngine.CommitmentRulesOn(s) || KnownBallots.DealOutcomeKnown(s, deal))
                        && (!canonical.ContainsKey(deal.id) || keptOwners.Contains(deal.id));
                case FinaleQuestions.PowerReceipt:
                    if (!SavedSafetyWeek(q.receiptId, s.week, out int powerWeek)) return false;
                    var power = s.ledger.power.FirstOrDefault(p => p.week == powerWeek);
                    if (power == null) return false;
                    bool final = power.tally.Count == 0 && power.evicteeId != null && power.vetoHolderId == null;
                    if (q.category == FinaleQuestions.Ownership)
                        return (power.hohId == player && (FinalistRead.PutUp(power, juror) || (final && power.evicteeId == juror)))
                            || (power.vetoHolderId == player && power.vetoUsed && power.replacementId == juror);
                    if (q.category == FinaleQuestions.Strategy) return power.hohId == player;
                    return q.category == FinaleQuestions.Social && !final
                        && (power.nominees.Contains(player) || power.savedId == player)
                        && power.evicteeId != null && power.evicteeId != player;
                case FinaleQuestions.BallotReceipt:
                    if (!SavedSafetyWeek(q.receiptId, s.week, out int ballotWeek)) return false;
                    return s.ledger.ballots.Any(b => b.week == ballotWeek && b.voterId == player
                        && (q.category == FinaleQuestions.Ownership ? b.targetId == juror
                            : q.category == FinaleQuestions.Mistake && ((b.readBefore != null && !b.correct)
                                || s.ledger.power.Any(p => p.week == b.week && p.evicteeId != null && p.evicteeId != b.targetId))));
                case FinaleQuestions.ReplyReceipt:
                    return q.category == FinaleQuestions.JuryManagement && s.ledger.replies.Any(r => r.cardId == q.receiptId
                        && r.fromId == juror && r.kind == ReplyCards.Plea && r.replyKey == "refuse");
                case FinaleQuestions.CallReceipt:
                    if (q.category != FinaleQuestions.Strategy) return false;
                    return s.ledger.calls.Any(c => q.receiptId == c.allianceId + ":" + c.week.ToString(CultureInfo.InvariantCulture)
                        && c.callerId == player && c.callerId != juror && !c.followed.Contains(juror) && !c.defected.Contains(juror)
                        && !s.alliances.Any(a => a.id == c.allianceId && a.members.Contains(juror)));
                case FinaleQuestions.AllianceReceipt:
                    if (q.category != FinaleQuestions.Personal) return false;
                    int? left = JuryHouseRead.LeftWeek(s, juror);
                    return s.alliances.Any(a => a.id == q.receiptId && a.members.Contains(player) && a.members.Contains(juror)
                        && (a.active || s.ledger.alliances.Any(r => r.id == a.id && r.why != null
                            && r.why.EndsWith("/left-house", StringComparison.Ordinal) && left != null && r.endedWeek == left)));
                default: return false;
            }
        }

        private static bool SavedSafetySequence(string id, string prefix, int next) => id != null
            && id.StartsWith(prefix, StringComparison.Ordinal)
            && int.TryParse(id.Substring(prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out int sequence)
            && sequence >= 1 && sequence < next && id == prefix + sequence.ToString(CultureInfo.InvariantCulture);

        private static bool SavedSafetyWeek(string text, int current, out int week) =>
            int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out week)
            && week >= 1 && week <= current && text == week.ToString(CultureInfo.InvariantCulture);
    }
}
