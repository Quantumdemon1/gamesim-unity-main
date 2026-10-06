using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// Safety storage writer, callable only from simulation command owners. It does not select
    /// public mode or activate prerequisites. This does not spend, roll, mint IDs, publish effects,
    /// settle commitments, or bypass the command's save-before-publication transaction.
    /// New records and changed lists are detached; every refusal leaves the entire input unchanged.
    /// </summary>
    internal static class UnifiedCommitmentStore
    {
        internal static bool TryAddPromise(EpisodeState state, PromiseState draft, string origin, out string error)
        {
            if (!Ready(state, out error)) return false;
            if (draft == null || draft.kind != PromiseKind.Safety || draft.status != PromiseStatus.Active
                || draft.targetId != null || draft.impact != DealTrust.Medium || draft.brokenById != null || draft.settledWeek != 0
                || !SourceId(draft.id, origin))
                return Refuse(out error, "Expected a new unencumbered safety promise; no source fields may be discarded.");
            var row = new UnifiedCommitmentState
            {
                id = draft.id, kind = UnifiedCommitments.Safety, sourcePolicy = UnifiedCommitments.PromisePolicy,
                origin = origin, makerId = draft.fromId, beneficiaryId = draft.toId, createdWeek = draft.week,
                expiresWeek = draft.expiresWeek, status = DealStatus.Active, trustImpact = draft.impact,
            };
            if (!UnifiedCommitments.CanOffer(state, row, false, out error)) return false;
            var staged = Stage(state); staged.unifiedCommitments.Add(row.Clone());
            return Commit(state, staged, out error);
        }

        internal static bool TryAddDeal(EpisodeState state, DealState draft, string origin, out string error)
        {
            if (!CanAddDeal(state, draft, origin, out error)) return false;
            var row = FromDeal(draft, origin);
            var staged = Stage(state); staged.unifiedCommitments.Add(row.Clone());
            return Commit(state, staged, out error);
        }

        /// <summary>Source-shaped preflight before the command spends or rolls; never installs a draft.</summary>
        internal static bool CanAddDeal(EpisodeState state, DealState draft, string origin, out string error)
        {
            if (!Ready(state, out error) || !SafetyDraft(state, draft, origin, false, out error)) return false;
            return UnifiedCommitments.CanOffer(state, FromDeal(draft, origin), origin == UnifiedCommitments.PlayerDeal, out error);
        }

        /// <summary>
        /// Voids only a canonical safety consideration, through its actual storage owner. The
        /// command owns the public receipt; this writes no breach, effect, RNG or history of its own.
        /// A ballot or a breach after the price's term cannot disclose or void it.
        /// </summary>
        internal static bool TryExpireLinkedPrice(EpisodeState state, string boughtId, string breakerId, out string error)
        {
            if (!Ready(state, out error)) return false;
            var bought = CommitmentReferences.FindDeal(state, boughtId);
            var price = bought?.linkedDealId == null ? null : state.unifiedCommitments.FirstOrDefault(row => row.id == bought.linkedDealId);
            if (bought == null || Negotiation.IsPrice(bought) || bought.status != DealStatus.Broken
                || Breaches.DealBreaker(state, bought) != breakerId || KnownBallots.SettledByABallot(bought)
                || price == null || price.origin != UnifiedCommitments.CounterPrice || price.sourcePolicy != UnifiedCommitments.DealPolicy
                || price.linkedCommitmentId != bought.id || !DealStatus.Binds(price.status) || price.beneficiaryId != breakerId
                || bought.week != price.createdWeek || bought.linkedDealId != price.id
                || (price.expiresWeek != 0 && state.week > price.expiresWeek))
                return Refuse(out error, "Only an active safety price for a public breach by its payee may lapse.");
            var staged = Stage(state);
            staged.unifiedCommitments.First(row => row.id == price.id).status = DealStatus.Expired;
            return Commit(state, staged, out error);
        }

        /// <summary>
        /// The existing accepted-counter pair, with at least one safety member. Both are reserved
        /// together under the player's forty-row limit. The command must still re-derive the live
        /// counter and own consent/eligibility; these are storage/term checks, not a UI capability.
        /// </summary>
        internal static bool TryAddLinkedDeals(EpisodeState state, DealState bought, string boughtOrigin,
            DealState price, string priceOrigin, out string error)
        {
            if (!Ready(state, out error)) return false;
            if (boughtOrigin != UnifiedCommitments.CounterDeal || priceOrigin != UnifiedCommitments.CounterPrice
                || bought == null || price == null || bought.type == price.type
                || (bought.type != DealKind.SafetyAgreement && price.type != DealKind.SafetyAgreement)
                || !Token(bought.id) || !Token(price.id) || bought.id == price.id
                || !bought.id.StartsWith(Negotiation.CounterDealPrefix, StringComparison.Ordinal)
                || !price.id.StartsWith(Negotiation.PricePrefix, StringComparison.Ordinal)
                || bought.id.Substring(Negotiation.CounterDealPrefix.Length) != price.id.Substring(Negotiation.PricePrefix.Length)
                || Negotiation.IsPrice(bought) || !Negotiation.IsPrice(price)
                || bought.linkedDealId != price.id || price.linkedDealId != bought.id
                || bought.proposerId != state.playerId || price.proposerId != state.playerId
                || bought.recipientId != price.recipientId || !ActiveOther(state, bought.recipientId)
                || bought.week != state.week || price.week != state.week
                || !EpisodeEngine.CommitmentRulesOn(state))
                return Refuse(out error, "Expected one source-shaped accepted counter and its reciprocal price link.");
            int used = CommitmentReferences.DealCount(state);
            if (used + 2 > PlayerDeals.PlayerDealCeiling || used + 2 > UnifiedCommitments.FamilyCapacity)
                return Refuse(out error, "There is no room for both counter records.");
            if (!SourceCounterDraft(state, bought, false, out error) || !SourceCounterDraft(state, price, true, out error)) return false;
            var staged = Stage(state);
            if (!StageDeal(staged, bought, boughtOrigin, out error) || !StageDeal(staged, price, priceOrigin, out error)) return false;
            return Commit(state, staged, out error);
        }

        internal static bool TryRespond(EpisodeState state, string id, bool accept, out string error)
        {
            if (!Ready(state, out error)) return false;
            var pending = state.unifiedCommitments.FirstOrDefault(row => row.id == id);
            if (pending == null || pending.sourcePolicy != UnifiedCommitments.DealPolicy || pending.origin != UnifiedCommitments.NpcOffer
                || pending.status != DealStatus.Proposed || pending.beneficiaryId != state.playerId
                || !Active(state, state.playerId) || !ActiveOther(state, pending.makerId))
                return Refuse(out error, "That safety offer is no longer on the table.");
            var answered = pending.Clone(); answered.status = accept ? DealStatus.Active : DealStatus.Declined;
            if (accept) answered.expiresWeek = state.week;
            var staged = Stage(state);
            if (accept)
            {
                // Check the newly binding term against other rows, not the pending record itself.
                // The offer's original creation week survives, so CanOffer's new-creation gate is
                // intentionally not used for an answer.
                if (staged.unifiedCommitments.Any(row => row.id != id && DealStatus.Binds(row.status)
                    && SameDealTerm(row, answered))) return Refuse(out error, "The same safety duty and term already stand.");
            }
            int at = staged.unifiedCommitments.FindIndex(row => row.id == id);
            staged.unifiedCommitments[at] = answered;
            return Commit(state, staged, out error);
        }

        private static bool SafetyDraft(EpisodeState state, DealState draft, string origin, bool linked, out string error)
        {
            error = null;
            bool pending = origin == UnifiedCommitments.NpcOffer;
            if (draft == null || draft.type != DealKind.SafetyAgreement || draft.targetId != null
                || draft.status != (pending ? DealStatus.Proposed : DealStatus.Active)
                || draft.brokenById != null || draft.settledWeek != 0 || draft.trustImpact != DealKind.DefaultTrust(draft.type)
                || !SourceId(draft.id, origin)
                || (!linked && draft.linkedDealId != null)
                || (linked != (origin == UnifiedCommitments.CounterDeal || origin == UnifiedCommitments.CounterPrice)))
                return Refuse(out error, "Expected a new safety deal with its complete source policy and consent state.");
            return UnifiedCommitments.ValidateRow(state, FromDeal(draft, origin), out error);
        }

        private static bool SourceCounterDraft(EpisodeState state, DealState draft, bool price, out string error)
        {
            error = null;
            if (!DealKind.IsKnown(draft.type) || draft.status != DealStatus.Active || draft.brokenById != null || draft.settledWeek != 0
                || draft.trustImpact != DealKind.DefaultTrust(draft.type)
                || (DealKind.CommitmentRulesOnly(draft.type) && !EpisodeEngine.CommitmentRulesOn(state)))
                return Refuse(out error, "Unsupported counter source fields.");
            if (price && draft.type != DealKind.SafetyAgreement && draft.type != DealKind.FinalTwo
                && draft.type != DealKind.VoteSave && draft.type != DealKind.VoteTogether)
                return Refuse(out error, "That is not a source counter-price family.");
            var expected = PlayerDeals.Draft(state, draft.recipientId, draft.type, draft.targetId, draft.id);
            if (draft.expiresWeek != expected.expiresWeek || draft.targetId != expected.targetId
                || (DealKind.NamesATarget(draft.type) && (draft.targetId == null || !Active(state, draft.targetId)))
                || (price && draft.type == DealKind.VoteSave && draft.targetId != draft.recipientId))
                return Refuse(out error, "Counter target or term does not match the source draft.");
            return true;
        }

        private static bool StageDeal(EpisodeState staged, DealState draft, string origin, out string error)
        {
            if (draft.type == DealKind.SafetyAgreement)
            {
                if (!SafetyDraft(staged, draft, origin, true, out error)) return false;
                var row = FromDeal(draft, origin);
                if (!UnifiedCommitments.CanOffer(staged, row, false, out error)) return false;
                staged.unifiedCommitments.Add(row.Clone()); return true;
            }
            if (AllIds(staged).Contains(draft.id)) return Refuse(out error, "Counter identity collides with another commitment.");
            // The other family stays legacy-owned, but is checked against all projected deals.
            if (CommitmentReferences.Deals(staged).Any(row => DealStatus.Binds(row.status) && row.type == draft.type
                && row.targetId == draft.targetId && row.expiresWeek == draft.expiresWeek
                && ((row.proposerId == draft.proposerId && row.recipientId == draft.recipientId)
                    || (row.proposerId == draft.recipientId && row.recipientId == draft.proposerId))))
                return Refuse(out error, "The counter's other duty already stands.");
            staged.deals.Add(draft.Clone()); error = null; return true;
        }

        private static UnifiedCommitmentState FromDeal(DealState draft, string origin) => new UnifiedCommitmentState
        {
            id = draft.id, kind = UnifiedCommitments.Safety, sourcePolicy = UnifiedCommitments.DealPolicy, origin = origin,
            makerId = draft.proposerId, beneficiaryId = draft.recipientId, reciprocal = true, createdWeek = draft.week,
            expiresWeek = draft.expiresWeek, status = draft.status, trustImpact = draft.trustImpact,
            linkedCommitmentId = draft.linkedDealId, brokenById = draft.brokenById, settledWeek = draft.settledWeek,
        };

        private static bool Ready(EpisodeState state, out string error)
        {
            if (!UnifiedCommitments.RulesOn(state)) return Refuse(out error, "Unified commitments are not enabled.");
            if (!UnifiedCommitments.ValidateRecords(state, out error)) return false;
            var ids = AllIds(state);
            if (ids.Any(id => !Token(id)) || ids.Distinct(StringComparer.Ordinal).Count() != ids.Count)
                return Refuse(out error, "Every source commitment needs a globally unique bounded identity.");
            return true;
        }

        private static List<string> AllIds(EpisodeState state) => state.promises.Select(row => row.id)
            .Concat(state.deals.Select(row => row.id)).Concat(state.unifiedCommitments.Select(row => row.id)).ToList();

        // Only the three commitment lists are staged. All other source fields remain read-only;
        // unlike EpisodeState.Clone, this also does not traverse unrelated gameplay subsystems.
        private static EpisodeState Stage(EpisodeState state) => new EpisodeState
        {
            week = state.week, playerId = state.playerId, contestants = state.contestants,
            unifiedCommitmentRulesVersion = state.unifiedCommitmentRulesVersion,
            promises = state.promises.Select(row => row.Clone()).ToList(), deals = state.deals.Select(row => row.Clone()).ToList(),
            unifiedCommitments = state.unifiedCommitments.Select(row => row.Clone()).ToList(),
        };

        private static bool Commit(EpisodeState state, EpisodeState staged, out string error)
        {
            if (!Ready(staged, out error)) return false;
            var deals = staged.deals.Concat(staged.unifiedCommitments.Where(row => row.sourcePolicy == UnifiedCommitments.DealPolicy)
                .Select(row => new DealState { id = row.id, proposerId = row.makerId, recipientId = row.beneficiaryId,
                    week = row.createdWeek, linkedDealId = row.linkedCommitmentId })).ToList();
            foreach (var deal in deals)
            {
                bool price = Negotiation.IsPrice(deal);
                if (deal.linkedDealId == null) { if (price) return Refuse(out error, "A price must resolve its bought commitment."); continue; }
                var linked = deals.FirstOrDefault(row => row.id == deal.linkedDealId);
                if (linked == null || linked.id == deal.id || linked.linkedDealId != deal.id || linked.week != deal.week
                    || price == Negotiation.IsPrice(linked)
                    || !((linked.proposerId == deal.proposerId && linked.recipientId == deal.recipientId)
                        || (linked.proposerId == deal.recipientId && linked.recipientId == deal.proposerId)))
                    return Refuse(out error, "Counter links must be reciprocal across their true storage owners.");
            }
            // This facade may append a mixed bundle's non-safety member, never rewrite a legacy
            // record. Preserve existing handles a command may legitimately still hold. Build
            // every new list/draft before publishing either authoritative list.
            if (staged.deals.Count < state.deals.Count || state.deals.Where((row, index) => !SameLegacyDeal(row, staged.deals[index])).Any())
                return Refuse(out error, "The safety writer cannot update another commitment family's rows.");
            List<DealState> appendedDeals = null;
            if (staged.deals.Count != state.deals.Count)
            {
                appendedDeals = new List<DealState>(state.deals);
                appendedDeals.AddRange(staged.deals.Skip(state.deals.Count).Select(row => row.Clone()));
            }
            state.unifiedCommitments = staged.unifiedCommitments;
            if (appendedDeals != null) state.deals = appendedDeals;
            return true;
        }

        private static bool SameLegacyDeal(DealState left, DealState right) => left.id == right.id && left.type == right.type
            && left.proposerId == right.proposerId && left.recipientId == right.recipientId && left.targetId == right.targetId
            && left.status == right.status && left.week == right.week && left.expiresWeek == right.expiresWeek
            && left.trustImpact == right.trustImpact && left.brokenById == right.brokenById
            && left.settledWeek == right.settledWeek && left.linkedDealId == right.linkedDealId;

        private static bool SameDealTerm(UnifiedCommitmentState left, UnifiedCommitmentState right) =>
            left.sourcePolicy == UnifiedCommitments.DealPolicy && left.expiresWeek == right.expiresWeek
            && ((left.makerId == right.makerId && left.beneficiaryId == right.beneficiaryId)
                || (left.makerId == right.beneficiaryId && left.beneficiaryId == right.makerId));
        private static bool SourceId(string id, string origin)
        {
            if (!Token(id)) return false;
            string prefix;
            switch (origin)
            {
                case UnifiedCommitments.PlayerPromise: case UnifiedCommitments.HoHPitch: case UnifiedCommitments.StoryPromise: prefix = "promise-"; break;
                case UnifiedCommitments.NpcPromise: prefix = "promise-npc-"; break;
                case UnifiedCommitments.PlayerDeal: prefix = "deal-player-"; break;
                case UnifiedCommitments.NpcDeal: prefix = "deal-npc-"; break;
                case UnifiedCommitments.NpcOffer: prefix = NpcDeals.OfferPrefix; break;
                case UnifiedCommitments.Lobby: prefix = "deal-lobby-"; break;
                case UnifiedCommitments.StoryDeal: prefix = "deal-story-"; break;
                case UnifiedCommitments.CounterDeal: prefix = Negotiation.CounterDealPrefix; break;
                case UnifiedCommitments.CounterPrice: prefix = Negotiation.PricePrefix; break;
                default: return false;
            }
            return id.StartsWith(prefix, StringComparison.Ordinal) && id.Length > prefix.Length
                && id.Substring(prefix.Length).All(ch => ch >= '0' && ch <= '9');
        }
        private static bool Active(EpisodeState state, string id) => id != null && state.Find(id)?.status == ContestantStatus.Active;
        private static bool ActiveOther(EpisodeState state, string id) => id != state.playerId && Active(state, id);
        private static bool Token(string value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 160 && !value.Any(char.IsControl);
        private static bool Refuse(out string error, string reason) { error = reason; return false; }
    }
}
