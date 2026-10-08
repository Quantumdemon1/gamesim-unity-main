using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>Detached reservation only. The actual command owner still owns consent, cost, ID spending and effects.</summary>
    public sealed class UnifiedVoteAdmissionPlan
    {
        public readonly IReadOnlyList<UnifiedCommitmentState> CanonicalAdditions;
        public readonly IReadOnlyList<DealState> RawAdditions;
        public readonly DealState RawReplacement;
        internal UnifiedVoteAdmissionPlan(IEnumerable<UnifiedCommitmentState> canonical, IEnumerable<DealState> raw, DealState replacement)
        {
            CanonicalAdditions = Array.AsReadOnly(canonical.Select(row => row.Clone()).ToArray());
            RawAdditions = Array.AsReadOnly(raw.Select(row => row.Clone()).ToArray());
            RawReplacement = replacement?.Clone();
        }
    }

    /// <summary>
    /// Prospective read-only source-draft admission. All APIs require a fully validated explicit2
    /// aggregate, not a relabeled legacy state. No helper creates a source event or claims a roll,
    /// NPC offer selection, Story choice or audible counter was actually made. The original owner
    /// must still prove those prerequisites; this class handles term/chronology/reservation only.
    /// </summary>
    public static class UnifiedVoteAdmission
    {
        public static bool TryPromise(EpisodeState s, PromiseState sourceDraft, string origin,
            out UnifiedCommitmentState row, out string error)
        {
            row = null; error = null;
            if (!Ready(s, out error)) return false;
            if (sourceDraft == null || sourceDraft.kind != PromiseKind.Vote || sourceDraft.status != PromiseStatus.Active
                || sourceDraft.week != s.week || sourceDraft.expiresWeek != s.week || sourceDraft.impact != DealTrust.Medium
                || sourceDraft.brokenById != null || sourceDraft.settledWeek != 0
                || !Active(s, sourceDraft.fromId) || !Active(s, sourceDraft.toId)
                || sourceDraft.targetId != null && !Active(s, sourceDraft.targetId))
                return Fail(out error, "Use the actual current, unsettled Vote promise source draft.");
            if (origin == UnifiedCommitments.PlayerPromise)
            {
                if (s.phase != EpisodePhase.Campaign || s.evictionResolved || sourceDraft.fromId != s.playerId
                    || !EpisodeEngine.Voters(s).Any(person => person.id == s.playerId)
                    || !s.nominees.Contains(sourceDraft.targetId))
                    return Fail(out error, "The player's Vote promise keeps its Campaign/voter/block source prerequisites.");
            }
            else if (origin == UnifiedCommitments.NpcPromise)
            {
                if (sourceDraft.fromId == s.playerId || sourceDraft.toId == s.playerId || s.evictionResolved
                    || !s.nominees.Contains(sourceDraft.fromId) || s.nominees.Contains(sourceDraft.toId)
                    || sourceDraft.targetId != s.nominees.FirstOrDefault(id => id != sourceDraft.fromId))
                    return Fail(out error, "The actual NPC positional promise retains its nominee maker and other nominee target.");
            }
            else if (origin != UnifiedCommitments.StoryPromise || sourceDraft.targetId != null)
                return Fail(out error, "Only the native generic Story promise has a null Vote target.");
            // Preserve the actual makers' broader source duplicate refusal, not only the aggregate
            // normalized binding-duty key. A reverse promise is a different unilateral obligation.
            if (UnifiedVoteReferences.PromisesUnchecked(s).Any(old => old.status == PromiseStatus.Active
                && old.kind == PromiseKind.Vote && old.fromId == sourceDraft.fromId && old.toId == sourceDraft.toId))
                return Fail(out error, "This maker already owes that recipient an active Vote promise.");
            var candidate = new UnifiedCommitmentState { id = sourceDraft.id, kind = UnifiedVoteTogether.Vote,
                sourcePolicy = UnifiedCommitments.PromisePolicy, origin = origin, makerId = sourceDraft.fromId,
                beneficiaryId = sourceDraft.toId, reciprocal = false, createdWeek = sourceDraft.week,
                expiresWeek = sourceDraft.expiresWeek, status = DealStatus.Active, trustImpact = sourceDraft.impact,
                targetId = sourceDraft.targetId, voteBindingWeek = s.week, voteFirstRevealWeek = Floor(s, origin) };
            if (!UnifiedVoteFamilyValidation.TryValidateDraftBundle(s, new[] { candidate }, Array.Empty<DealState>(), null, out error)) return false;
            row = candidate.Clone(); return true;
        }

        public static bool TryDeal(EpisodeState s, DealState sourceDraft, string origin,
            out UnifiedCommitmentState row, out string error)
        {
            row = null; error = null;
            if (!Ready(s, out error)) return false;
            if (sourceDraft == null || !UnifiedVoteFamilyValidation.IsVoteType(sourceDraft.type)
                || sourceDraft.week != s.week || sourceDraft.expiresWeek != s.week
                || sourceDraft.brokenById != null || sourceDraft.settledWeek != 0 || sourceDraft.linkedDealId != null
                || !Active(s, sourceDraft.proposerId) || !Active(s, sourceDraft.recipientId)
                || sourceDraft.targetId != null && !Active(s, sourceDraft.targetId))
                return Fail(out error, "Use the current, unsettled and unlinked Vote deal source draft.");
            bool proposed = origin == UnifiedCommitments.NpcOffer;
            if (sourceDraft.status != (proposed ? DealStatus.Proposed : DealStatus.Active))
                return Fail(out error, "Consent remains Proposed for a real NPC question, otherwise already source-accepted Active.");
            int count = DealCount(s);
            if (origin == UnifiedCommitments.PlayerDeal)
            {
                if (count >= PlayerDeals.PlayerDealCeiling || sourceDraft.proposerId != s.playerId
                    || sourceDraft.recipientId == s.playerId || s.week < s.dealRulesStartWeek || s.evictionResolved
                    || s.nominees.Count == 0 || sourceDraft.type != DealKind.VoteTogether && !s.nominees.Contains(sourceDraft.targetId)
                    || Between(s, sourceDraft.proposerId, sourceDraft.recipientId).Any(old => old.type == sourceDraft.type
                        && (!Price(old) || old.targetId == sourceDraft.targetId)))
                    return Fail(out error, "Player admission preserves its actual 40 shelf, block and existing arrangement refusal.");
            }
            else if (origin == UnifiedCommitments.NpcDeal || proposed)
            {
                if (s.npcSocial == null || s.week < s.dealRulesStartWeek || s.evictionResolved
                    || sourceDraft.type != DealKind.VoteSave || !s.nominees.Contains(sourceDraft.proposerId)
                    || s.nominees.Contains(sourceDraft.recipientId) || sourceDraft.targetId != sourceDraft.proposerId
                    || Between(s, sourceDraft.proposerId, sourceDraft.recipientId).Any(old => old.type == sourceDraft.type))
                    return Fail(out error, "The real NPC Vote offer/direct ladder admits only its nominee's VoteSave.");
                var round = UnifiedVoteReferences.DealsUnchecked(s).Where(old => old.week == s.week
                    && old.id.StartsWith(NpcDeals.OfferPrefix, StringComparison.Ordinal) && old.recipientId == s.playerId).ToArray();
                if (proposed && (round.Length >= NpcDeals.ProposalsPerWeek
                    || round.Any(old => old.type == sourceDraft.type || old.proposerId == sourceDraft.proposerId)))
                    return Fail(out error, "The actual pending round preserves three offers and type/houseguest variety.");
                // Propose's once-per-week ROUND-entry gate is still the creator's responsibility.
                // Individual reservations during that same planned round must not reject its
                // second/third different offer merely because the first was already reserved.
            }
            else if (origin == UnifiedVoteFamilyValidation.VoteLobby)
            {
                if (s.phase != EpisodePhase.Campaign || s.evictionResolved || !s.nominees.Contains(s.playerId)
                    || !EpisodeEngine.Voters(s).Any(person => person.id == sourceDraft.proposerId)
                    || sourceDraft.type != DealKind.VoteSave || sourceDraft.targetId != s.playerId
                    || Between(s, sourceDraft.proposerId, s.playerId).Any(old => old.type == DealKind.VoteSave))
                    return Fail(out error, "Vote lobby retains the actual NPC voter's word to keep the player, not Safety Lobby.");
            }
            else if (origin != UnifiedCommitments.StoryDeal)
                return Fail(out error, "Use a recognized immediate Vote deal origin; linked sources have an atomic API.");
            else if (Between(s, sourceDraft.proposerId, sourceDraft.recipientId).Any(old => old.type == sourceDraft.type && old.targetId == sourceDraft.targetId))
                return Fail(out error, "The generic Story deal preserves its actual existing-type refusal.");
            var candidate = FromDeal(sourceDraft, origin, proposed ? 0 : s.week, proposed ? 0 : Floor(s, origin));
            if (!UnifiedVoteFamilyValidation.TryValidateDraftBundle(s, new[] { candidate }, Array.Empty<DealState>(), null, out error)) return false;
            row = candidate.Clone(); return true;
        }

        public static bool TryAnswerOffer(EpisodeState s, string id, out UnifiedCommitmentState row, out string error)
        {
            row = null; error = null;
            if (!Ready(s, out error)) return false;
            var source = s.unifiedCommitments.FirstOrDefault(item => item.id == id);
            if (source == null || source.kind != UnifiedVoteTogether.Vote || source.origin != UnifiedCommitments.NpcOffer
                || source.status != DealStatus.Proposed || source.beneficiaryId != s.playerId
                || !Active(s, source.makerId) || !Active(s, s.playerId))
                return Fail(out error, "Answer the genuine still-pending offer between its actual active parties.");
            var candidate = source.Clone(); candidate.status = DealStatus.Active; candidate.expiresWeek = s.week;
            candidate.voteBindingWeek = s.week; candidate.voteFirstRevealWeek = Floor(s, source.origin);
            if (!UnifiedVoteFamilyValidation.TryValidateAnswer(s, candidate, out error)) return false;
            row = candidate.Clone(); return true;
        }

        /// <summary>
        /// Reservation for already regenerated actual counter terms. Audible OpenCounter and the
        /// original CounterPrice ordered ladder remain mandatory owner prerequisites, not proved
        /// by this plan. At least one canonical Vote member is required; unsupported bought/price
        /// families retain their raw true owners. Safety members use the unchanged Safety row core.
        /// </summary>
        public static bool TryCounterBundle(EpisodeState s, DealState bought, DealState price,
            out UnifiedVoteAdmissionPlan plan, out string error)
        {
            plan = null; error = null;
            if (!Ready(s, out error)) return false;
            if (bought == null || price == null || DealCount(s) + 2 > PlayerDeals.PlayerDealCeiling
                || !CurrentDealDraft(s, bought) || !CurrentDealDraft(s, price)
                || bought.proposerId != s.playerId || price.proposerId != s.playerId
                || bought.recipientId != price.recipientId || !Active(s, bought.recipientId)
                || bought.linkedDealId != price.id || price.linkedDealId != bought.id
                || bought.type == price.type || !Price(price) || Price(bought)
                || !UnifiedVoteFamilyValidation.IsVoteType(bought.type) && !UnifiedVoteFamilyValidation.IsVoteType(price.type))
                return Fail(out error, "Reserve the real current counter pair together under the player's 40 shelf.");
            foreach (var draft in new[] { bought, price })
                if (Between(s, draft.proposerId, draft.recipientId).Any(old => old.type == draft.type
                    && old.targetId == draft.targetId && old.expiresWeek == draft.expiresWeek))
                    return Fail(out error, "Neither the canonical nor the still-raw duty in the counter already stands.");
            var canonical = new List<UnifiedCommitmentState>(); var raw = new List<DealState>();
            AddDeal(bought, UnifiedCommitments.CounterDeal); AddDeal(price, UnifiedCommitments.CounterPrice);
            // A raw counter member is also a source draft; unlike own-veto, its ID prefix differs.
            // The aggregate's draft whitelist below handles that exact explicit source prefix.
            if (!UnifiedVoteFamilyValidation.TryValidateCounterDraftBundle(s, canonical, raw, out error)) return false;
            plan = new UnifiedVoteAdmissionPlan(canonical, raw, null); return true;
            void AddDeal(DealState draft, string origin)
            {
                if (draft.type == DealKind.SafetyAgreement || UnifiedVoteFamilyValidation.IsVoteType(draft.type))
                    canonical.Add(FromDeal(draft, origin, s.week, s.week));
                else raw.Add(draft.Clone());
            }
        }

        public static bool TryOwnVetoPrice(EpisodeState s, DealState bought, DealState price,
            out UnifiedVoteAdmissionPlan plan, out string error)
        {
            plan = null; error = null;
            if (!Ready(s, out error)) return false;
            if (bought == null || price == null || DealCount(s) + 2 > PlayerDeals.PlayerDealCeiling
                || !CurrentDealDraft(s, bought) || !CurrentDealDraft(s, price)
                || s.phase != EpisodePhase.VetoMeeting || s.vetoResolved || s.vetoHolderId != s.playerId
                || !StrategyRules.VetoCanBeUsed(s) || bought.type != DealKind.VetoUse || bought.proposerId != s.playerId
                || bought.recipientId == s.playerId || !s.nominees.Contains(bought.recipientId)
                || price.proposerId != bought.recipientId || price.recipientId != s.playerId
                || price.type != DealKind.VoteSave || price.targetId != s.playerId || price.expiresWeek != 0
                || bought.linkedDealId != price.id || price.linkedDealId != bought.id
                || !EpisodeEngine.LeverRulesOn(s) || Between(s, price.proposerId, s.playerId).Any(old => old.type == DealKind.VoteSave)
                || s.deals.Any(old => old.week == s.week && old.type == DealKind.VetoUse
                    && (old.status == DealStatus.Active || old.status == DealStatus.Accepted)
                    && (old.proposerId == s.playerId || old.recipientId == s.playerId)))
                return Fail(out error, "Use the actual undecided player's veto, nominee and unowed VoteSave price prerequisites.");
            var candidate = FromDeal(price, UnifiedVoteFamilyValidation.OwnVetoPrice, s.week, s.week);
            if (!UnifiedVoteFamilyValidation.TryValidateDraftBundle(s, new[] { candidate }, new[] { bought }, null, out error)) return false;
            plan = new UnifiedVoteAdmissionPlan(new[] { candidate }, new[] { bought }, null); return true;
        }

        public static bool TryAcceptedVetoAskPrice(EpisodeState s, string askId, DealState price,
            out UnifiedVoteAdmissionPlan plan, out string error)
        {
            plan = null; error = null;
            if (!Ready(s, out error)) return false;
            var ask = s.deals.FirstOrDefault(old => old.id == askId);
            if (ask == null || price == null || DealCount(s) >= UnifiedCommitments.FamilyCapacity
                || ask.type != DealKind.VetoUse || ask.status != DealStatus.Proposed || ask.recipientId != s.playerId
                || ask.linkedDealId != null || !Active(s, ask.proposerId) || !Active(s, s.playerId)
                || !ask.id.StartsWith("deal-veto-", StringComparison.Ordinal) || !CurrentDealDraft(s, price)
                || price.proposerId != ask.proposerId || price.recipientId != s.playerId || price.targetId != s.playerId
                || price.type != DealKind.VoteSave || price.expiresWeek != 0 || price.linkedDealId != ask.id
                || !EpisodeEngine.LeverRulesOn(s) || Between(s, ask.proposerId, s.playerId).Any(old => old.type == DealKind.VoteSave))
                return Fail(out error, "Answer the real retained veto ask and its eligible unowed VoteSave price; no invented veto-phase gate.");
            var answered = ask.Clone(); answered.status = DealStatus.Active; answered.expiresWeek = s.week;
            answered.linkedDealId = price.id;
            var candidate = FromDeal(price, UnifiedVoteFamilyValidation.VetoAskPrice, s.week, Floor(s, UnifiedVoteFamilyValidation.VetoAskPrice));
            if (!UnifiedVoteFamilyValidation.TryValidateDraftBundle(s, new[] { candidate }, Array.Empty<DealState>(), answered, out error)) return false;
            plan = new UnifiedVoteAdmissionPlan(new[] { candidate }, Array.Empty<DealState>(), answered); return true;
        }

        internal static UnifiedCommitmentState FromDeal(DealState draft, string origin, int bindingWeek, int firstWeek) =>
            new UnifiedCommitmentState { id = draft.id, kind = draft.type == DealKind.SafetyAgreement ? UnifiedCommitments.Safety : UnifiedVoteTogether.Vote,
                sourcePolicy = UnifiedCommitments.DealPolicy, origin = origin, makerId = draft.proposerId, beneficiaryId = draft.recipientId,
                reciprocal = true, createdWeek = draft.week, expiresWeek = draft.expiresWeek, status = draft.status,
                trustImpact = draft.trustImpact, linkedCommitmentId = draft.linkedDealId,
                subtype = draft.type == DealKind.SafetyAgreement ? null : draft.type, targetId = draft.targetId,
                voteBindingWeek = draft.type == DealKind.SafetyAgreement ? 0 : bindingWeek,
                voteFirstRevealWeek = draft.type == DealKind.SafetyAgreement ? 0 : firstWeek };
        private static int Floor(EpisodeState s, string origin)
        {
            bool lateOwner = origin == UnifiedCommitments.NpcOffer || origin == UnifiedVoteFamilyValidation.VetoAskPrice
                || origin == UnifiedCommitments.StoryPromise || origin == UnifiedCommitments.StoryDeal;
            return s.week + (lateOwner && s.unifiedVoteReveals.Any(frame => frame.week == s.week) ? 1 : 0);
        }
        private static bool Ready(EpisodeState s, out string error) =>
            EpisodeValidation.TryValidateProspectiveUnifiedVote(s, out error);
        private static bool Active(EpisodeState s, string id) => s.Find(id)?.status == ContestantStatus.Active;
        private static int DealCount(EpisodeState s) => s.deals.Count + s.unifiedCommitments.Count(row => row.sourcePolicy == UnifiedCommitments.DealPolicy);
        private static IEnumerable<DealState> Between(EpisodeState s, string a, string b) => UnifiedVoteReferences.DealsUnchecked(s)
            .Where(row => DealStatus.Binds(row.status) && (row.proposerId == a && row.recipientId == b || row.proposerId == b && row.recipientId == a));
        private static bool CurrentDealDraft(EpisodeState s, DealState row) => row.week == s.week && row.status == DealStatus.Active
            && row.brokenById == null && row.settledWeek == 0 && row.trustImpact == DealKind.DefaultTrust(row.type)
            && Active(s, row.proposerId) && Active(s, row.recipientId);
        private static bool Price(DealState row) => row.id != null && row.id.StartsWith(Negotiation.PricePrefix, StringComparison.Ordinal);
        private static bool Fail(out string error, string reason) { error = reason; return false; }
    }
}
