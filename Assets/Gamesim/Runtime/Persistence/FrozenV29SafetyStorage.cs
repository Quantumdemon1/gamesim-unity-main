// Frozen29 (SCH30): the schema-29 whole reader, carried from FrozenV28SafetyStorage.cs and then by exactly the live
// validators' changes from 515246bf to b208d46d (ALL-STAR-HISTORIES-PLAN 8.3). Provenance comments below
// describe the schema-28 image this copy started from.
// UNWIRED supporting component. Historical schema29 storage policy, not whole-save acceptance.
// Authority: final-native-graph-01/project, HEAD515246bf / status61.
// Data types come only from the separately sealed 77-type/583-field literal snapshot.
using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Persistence.Frozen29Data;

namespace Gamesim.Persistence.Frozen29Semantics
{
    internal static class FrozenV29SafetyStorage
    {
        // Historical UnifiedCommitments.cs:80-84; no live project constant or factory reads.
        public const int ProspectiveVersion = 1, FamilyCapacity = 200;
        public const string Safety = "safety", PromisePolicy = "promise", DealPolicy = "deal";
        public const string PlayerPromise = "player-promise", NpcPromise = "npc-promise", StoryPromise = "story-promise", HoHPitch = "hoh-pitch";
        public const string PlayerDeal = "player-deal", NpcDeal = "npc-deal", NpcOffer = "npc-offer", Lobby = "lobby", StoryDeal = "story-deal";
        public const string CounterDeal = "counter-deal", CounterPrice = "counter-price";
        // Historical EpisodeValidation.cs:18 and UnifiedVoteFamilyValidation.cs:18.
        private const int MinimumCast = 3, MaximumCast = 16, VoteVersion = 2;

        // BEGIN UnifiedCommitments.RulesOn
        internal static bool RulesOn(EpisodeState state) => state != null && state.unifiedCommitmentRulesVersion == ProspectiveVersion;
        // END UnifiedCommitments.RulesOn

        // BEGIN UnifiedCommitments.SafetyAuthorityOn
        internal static bool SafetyAuthorityOn(EpisodeState state) => state != null
            && (state.unifiedCommitmentRulesVersion == ProspectiveVersion
                || state.unifiedCommitmentRulesVersion == VoteVersion);
        // END UnifiedCommitments.SafetyAuthorityOn

        // BEGIN UnifiedCommitments.IsPromiseOrigin
        internal static bool IsPromiseOrigin(string origin) => origin == PlayerPromise || origin == NpcPromise || origin == StoryPromise || origin == HoHPitch;
        // END UnifiedCommitments.IsPromiseOrigin

        // BEGIN UnifiedCommitments.IsDealOrigin
        internal static bool IsDealOrigin(string origin) => origin == PlayerDeal || origin == NpcDeal || origin == NpcOffer
            || origin == Lobby || origin == StoryDeal || origin == CounterDeal || origin == CounterPrice;
        // END UnifiedCommitments.IsDealOrigin

        // BEGIN UnifiedCommitments.ValidateRow
        internal static bool ValidateRow(EpisodeState state, UnifiedCommitmentState row, out string error)
        {
            error = null;
            if (!SafetyAuthorityOn(state)) return Refuse(out error, "Unified commitments are not enabled.");
            return ValidateSafetyRowCore(state, row, out error);
        }
        // END UnifiedCommitments.ValidateRow

        // BEGIN UnifiedCommitments.ValidateSafetyRowCore
        internal static bool ValidateSafetyRowCore(EpisodeState state, UnifiedCommitmentState row, out string error)
        {
            error = null;
            if (!IdentityContextValid(state) || row == null || !Token(row.id) || row.kind != Safety
                || row.targetId != null || row.subtype != null
                || row.voteBindingWeek != 0 || row.voteFirstRevealWeek != 0
                || !Token(row.makerId) || !Token(row.beneficiaryId) || row.makerId == row.beneficiaryId
                || Find(state, row.makerId) == null || Find(state, row.beneficiaryId) == null)
                return Refuse(out error, "Invalid canonical safety identity.");
            bool promise = row.sourcePolicy == PromisePolicy;
            if ((!promise && row.sourcePolicy != DealPolicy) || (promise ? !IsPromiseOrigin(row.origin) : !IsDealOrigin(row.origin))
                || row.reciprocal == promise || !DealStatus.IsKnown(row.status) || !DealTrust.IsKnown(row.trustImpact))
                return Refuse(out error, "Invalid canonical safety source policy.");
            // A proposed NPC safety offer can survive the week turn until the next NPC deal
            // pass. Its answer preserves the proposal week/ID, but acceptance resets the term
            // to the answering week (EpisodeEngine.RespondToDeal). Decline never resets it.
            bool answeredNpcOffer = row.origin == NpcOffer && row.status != DealStatus.Proposed && row.status != DealStatus.Declined;
            bool validTerm = answeredNpcOffer
                ? row.expiresWeek >= row.createdWeek && row.expiresWeek <= state.week
                : row.expiresWeek == row.createdWeek + (promise || row.origin == Lobby ? 1 : 0);
            if (row.createdWeek < 1 || row.createdWeek > state.week || !validTerm)
                return Refuse(out error, "Invalid canonical safety term.");
            if (promise && (row.status == DealStatus.Proposed || row.status == DealStatus.Accepted || row.status == DealStatus.Declined || row.status == DealStatus.Fulfilled
                || row.trustImpact != DealTrust.Medium || row.linkedCommitmentId != null))
                return Refuse(out error, "A safety promise is unilateral, not a pending bargain or a spared-deal receipt.");
            bool playerMakes = row.origin == PlayerPromise || row.origin == HoHPitch || row.origin == PlayerDeal || row.origin == Lobby
                || row.origin == CounterDeal || row.origin == CounterPrice;
            if ((playerMakes && (row.makerId != state.playerId || row.beneficiaryId == state.playerId))
                || (row.origin == NpcOffer && (row.makerId == state.playerId || row.beneficiaryId != state.playerId))
                || ((row.origin == NpcPromise || row.origin == NpcDeal) && (row.makerId == state.playerId || row.beneficiaryId == state.playerId)))
                return Refuse(out error, "The safety origin does not match its parties.");
            bool counter = row.origin == CounterDeal || row.origin == CounterPrice;
            if ((row.linkedCommitmentId != null && (!Token(row.linkedCommitmentId) || row.linkedCommitmentId == row.id))
                || counter != (row.linkedCommitmentId != null))
                return Refuse(out error, "Invalid safety consideration reference.");
            bool settled = row.status == DealStatus.Broken || row.status == DealStatus.Fulfilled;
            if (settled ? row.settledWeek < row.createdWeek || row.settledWeek > state.week : row.settledWeek != 0)
                return Refuse(out error, "Invalid canonical safety settlement week.");
            if (row.status == DealStatus.Broken)
            {
                if ((promise ? row.brokenById != row.makerId : row.brokenById != row.makerId && row.brokenById != row.beneficiaryId)
                    || !EffectToken(row.settlementEffectKey)) return Refuse(out error, "Invalid safety breach attribution.");
            }
            else if (row.brokenById != null || row.settlementEffectKey != null)
                return Refuse(out error, "Only a breach may carry a betrayal effect identity.");
            return true;
        }
        // END UnifiedCommitments.ValidateSafetyRowCore

        // BEGIN UnifiedCommitments.ValidateRecords
        internal static bool ValidateRecords(EpisodeState state, out string error)
        {
            error = null;
            if (state == null || state.unifiedCommitments == null) return Refuse(out error, "Missing canonical commitment list.");
            if (state.unifiedCommitmentRulesVersion == 0)
                return state.unifiedCommitments.Count == 0 || Refuse(out error, "Disabled commitment authority must remain empty.");
            if (!RulesOn(state) || !IdentityContextValid(state) || state.promises == null || state.deals == null
                || state.promises.Any(row => row == null) || state.deals.Any(row => row == null)
                || state.unifiedCommitments.Count > FamilyCapacity * 2) return Refuse(out error, "Invalid prospective commitment storage.");
            // There may be only one mutable safety authority. A future explicit converter must move
            // safety history out of these stores before opt-in; other families remain legacy-owned.
            if (state.promises.Any(row => row.kind == PromiseKind.Safety) || state.deals.Any(row => row.type == DealKind.SafetyAgreement))
                return Refuse(out error, "Legacy safety rows must not mirror the prospective canonical authority.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var promise in state.promises) if (promise?.id != null) ids.Add(promise.id);
            foreach (var deal in state.deals) if (deal?.id != null) ids.Add(deal.id);
            int promises = state.promises.Count, deals = state.deals.Count;
            foreach (var row in state.unifiedCommitments)
            {
                if (!ValidateRow(state, row, out error)) return false;
                if (!ids.Add(row.id)) return Refuse(out error, "Canonical identity collides with another commitment.");
                if (row.sourcePolicy == PromisePolicy) promises++; else deals++;
            }
            return (promises <= FamilyCapacity && deals <= FamilyCapacity) || Refuse(out error, "Commitment family capacity exceeded.");
        }
        // END UnifiedCommitments.ValidateRecords

        // BEGIN UnifiedCommitments.ValidateSafetyAuthority
        internal static bool ValidateSafetyAuthority(EpisodeState state, out string error)
        {
            if (RulesOn(state)) return ValidateRecords(state, out error);
            error = null;
            if (state == null || state.unifiedCommitmentRulesVersion != VoteVersion
                || state.unifiedCommitments == null || state.promises == null || state.deals == null
                || state.unifiedCommitments.Any(row => row == null) || state.promises.Any(row => row == null)
                || state.deals.Any(row => row == null) || !IdentityContextValid(state)
                || state.unifiedCommitments.Count > FamilyCapacity * 2)
                return Refuse(out error, "Invalid prospective commitment storage.");
            if (state.promises.Any(row => row.kind == PromiseKind.Safety) || state.deals.Any(row => row.type == DealKind.SafetyAgreement))
                return Refuse(out error, "Legacy safety rows must not mirror the prospective canonical authority.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (string id in state.promises.Select(row => row.id).Concat(state.deals.Select(row => row.id))
                         .Concat(state.unifiedCommitments.Select(row => row.id)))
                if (!Token(id) || !ids.Add(id)) return Refuse(out error, "Canonical identity collides with another commitment.");
            foreach (var row in state.unifiedCommitments.Where(row => row.kind == Safety))
                if (!ValidateSafetyRowCore(state, row, out error)) return false;
            int promises = state.promises.Count + state.unifiedCommitments.Count(row => row.sourcePolicy == PromisePolicy);
            int deals = state.deals.Count + state.unifiedCommitments.Count(row => row.sourcePolicy != PromisePolicy);
            return (promises <= FamilyCapacity && deals <= FamilyCapacity) || Refuse(out error, "Commitment family capacity exceeded.");
        }
        // END UnifiedCommitments.ValidateSafetyAuthority

        // BEGIN UnifiedCommitments.IdentityContextValid
        private static bool IdentityContextValid(EpisodeState state) => state != null && state.week >= 1 && state.week <= 100
            && state.contestants != null && state.contestants.Count >= MinimumCast && state.contestants.Count <= MaximumCast
            && state.contestants.All(person => person != null && Token(person.id) && person.nominationWeeks != null)
            && state.contestants.Select(person => person.id).Distinct(StringComparer.Ordinal).Count() == state.contestants.Count
            && Token(state.playerId) && state.contestants.Any(person => person.id == state.playerId);
        // END UnifiedCommitments.IdentityContextValid

        // BEGIN UnifiedCommitments.Token
        private static bool Token(string value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 160 && !value.Any(char.IsControl);
        // END UnifiedCommitments.Token

        // BEGIN UnifiedCommitments.EffectToken
        private static bool EffectToken(string value) => value != null && value.StartsWith("safety:", StringComparison.Ordinal) && value.Length <= 640 && !value.Any(char.IsControl);
        // END UnifiedCommitments.EffectToken

        // BEGIN UnifiedCommitments.Refuse
        private static bool Refuse(out string error, string reason) { error = reason; return false; }
        // END UnifiedCommitments.Refuse

        // BEGIN EpisodeState.Find
        private static ContestantState Find(EpisodeState state, string id) => state.contestants.FirstOrDefault(c => c.id == id);
        // END EpisodeState.Find

        private static class DealKind
        {
            public const string SafetyAgreement = "safety_agreement";
        }

        private static class DealStatus
        {
            public const string Proposed = "proposed";
            public const string Accepted = "accepted";
            public const string Active = "active";
            public const string Fulfilled = "fulfilled";
            public const string Broken = "broken";
            public const string Declined = "declined";
            public const string Expired = "expired";
            // Historical DealState.cs:197-198. Private container prevents external cell mutation.
            public static readonly string[] All =
                { Proposed, Accepted, Active, Fulfilled, Broken, Declined, Expired };

            // BEGIN DealStatus.IsKnown
            public static bool IsKnown(string status) => status != null && Array.IndexOf(All, status) >= 0;
            // END DealStatus.IsKnown
        }

        private static class DealTrust
        {
            public const string Low = "low", Medium = "medium", High = "high", Critical = "critical";
            // Historical DealState.cs:209. No live mutable table is consulted.
            public static readonly string[] All = { Low, Medium, High, Critical };

            // BEGIN DealTrust.IsKnown
            public static bool IsKnown(string trust) => trust != null && Array.IndexOf(All, trust) >= 0;
            // END DealTrust.IsKnown
        }
    }
}
