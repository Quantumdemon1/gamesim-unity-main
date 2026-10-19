// UNWIRED historical storage component; success is not whole-save or whole-reader acceptance.
// No LiveSchema factory initializer: schema authority is the fixed EpisodeState.cs:247 literal.
using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Persistence.Frozen28Data;
using UnifiedCommitments = Gamesim.Persistence.Frozen28Semantics.FrozenV28SafetyStorage;

namespace Gamesim.Persistence.Frozen28Semantics
{
    internal static class FrozenV28VoteStorage
    {
        internal const int HistoricalSchema = 28;
        public const int Version = 2;
        public const string VoteLobby = "vote-lobby", VetoAskPrice = "veto-ask-price", OwnVetoPrice = "own-veto-price";

        // BEGIN UnifiedVoteFamilyValidation.TryValidateStorage
        internal static bool TryValidateStorage(EpisodeState s, out string error)
        {
            if (s == null || s.unifiedCommitmentRulesVersion != Version)
                return Fail(out error, "Expected the explicit unified vote storage.");
            if (!UnifiedCommitments.ValidateSafetyAuthority(s, out error)) return false;
            if (s.promises.Any(row => row.kind == PromiseKind.Vote) || s.deals.Any(row => IsVoteType(row.type)))
                return Fail(out error, "The unified Safety and Vote authority cannot have writable raw mirrors.");
            HashSet<string> deals = null;
            foreach (var row in s.unifiedCommitments)
            {
                if (row.kind == UnifiedCommitments.Safety) continue;
                bool promise = row.sourcePolicy == UnifiedCommitments.PromisePolicy;
                if (row.kind != UnifiedVoteTogether.Vote || !promise && row.sourcePolicy != UnifiedCommitments.DealPolicy
                    || (promise ? row.subtype != null || !IsPromiseOrigin(row.origin) : !IsVoteType(row.subtype) || !IsDealOrigin(row.origin))
                    || !DealStatus.IsKnown(row.status) || row.status == DealStatus.Accepted
                    || promise && (row.status == DealStatus.Proposed || row.status == DealStatus.Declined))
                    return Fail(out error, "A stored Vote row needs its family, policy, origin, subtype and an installed status.");
                // The weight every reader of a broken or kept row prices it by (DealResolution.Impact, a promise's
                // native impact): a promise's is medium and a deal's its subtype's default, as the core holds them.
                if (row.trustImpact != (promise ? DealTrust.Medium : DealKind.DefaultTrust(row.subtype)))
                    return Fail(out error, "A stored Vote row keeps its source's trust weight.");
                if (!Token(row.makerId) || !Token(row.beneficiaryId) || row.makerId == row.beneficiaryId
                    || Find(s, row.makerId) == null || Find(s, row.beneficiaryId) == null
                    || row.targetId != null && (!Token(row.targetId) || Find(s, row.targetId) == null))
                    return Fail(out error, "A stored Vote row's parties and target must be houseguests of this season.");
                // Whether it names a target at all, as the core holds it (the pre-V6 save-contract review): a story's word on the
                // vote and a bloc name none, every other word and deal one - the target every reader compares a ballot with.
                if ((row.targetId == null) != (promise ? row.origin == UnifiedCommitments.StoryPromise : row.subtype == DealKind.VoteTogether))
                    return Fail(out error, "A stored Vote row names a target exactly where its source does.");
                if (row.linkedCommitmentId == null) continue;
                deals ??= new HashSet<string>(s.deals.Select(item => item.id)
                    .Concat(s.unifiedCommitments.Where(item => item.sourcePolicy == UnifiedCommitments.DealPolicy).Select(item => item.id)),
                    StringComparer.Ordinal);
                if (promise || row.linkedCommitmentId == row.id || !deals.Contains(row.linkedCommitmentId))
                    return Fail(out error, "A stored Vote deal's link must resolve to another deal of this season.");
            }
            return true;
        }
        // END UnifiedVoteFamilyValidation.TryValidateStorage

        // BEGIN UnifiedVoteFamilyValidation.IsVoteType
        internal static bool IsVoteType(string type) => type == DealKind.VoteTogether || type == DealKind.VoteSave || type == DealKind.VoteEvict;
        // END UnifiedVoteFamilyValidation.IsVoteType

        // BEGIN UnifiedVoteFamilyValidation.IsPromiseOrigin
        private static bool IsPromiseOrigin(string origin) => origin == UnifiedCommitments.PlayerPromise
            || origin == UnifiedCommitments.NpcPromise || origin == UnifiedCommitments.StoryPromise;
        // END UnifiedVoteFamilyValidation.IsPromiseOrigin

        // BEGIN UnifiedVoteFamilyValidation.IsDealOrigin
        private static bool IsDealOrigin(string origin) => origin == UnifiedCommitments.PlayerDeal || origin == UnifiedCommitments.NpcDeal
            || origin == UnifiedCommitments.NpcOffer || origin == VoteLobby || origin == UnifiedCommitments.StoryDeal
            || origin == UnifiedCommitments.CounterDeal || origin == UnifiedCommitments.CounterPrice || origin == VetoAskPrice || origin == OwnVetoPrice;
        // END UnifiedVoteFamilyValidation.IsDealOrigin

        // BEGIN UnifiedVoteFamilyValidation.Token
        private static bool Token(string value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 160 && !value.Any(char.IsControl);
        // END UnifiedVoteFamilyValidation.Token

        // BEGIN UnifiedVoteFamilyValidation.Fail
        private static bool Fail(out string error, string reason) { error = reason; return false; }
        // END UnifiedVoteFamilyValidation.Fail

        // BEGIN EpisodeState.Find
        private static ContestantState Find(EpisodeState state, string id) => state.contestants.FirstOrDefault(c => c.id == id);
        // END EpisodeState.Find

        private static class UnifiedVoteTogether
        {
            public const string Vote = "vote";
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
            public static readonly string[] All =
                { Proposed, Accepted, Active, Fulfilled, Broken, Declined, Expired };

            // BEGIN DealStatus.IsKnown
            public static bool IsKnown(string status) => status != null && Array.IndexOf(All, status) >= 0;
            // END DealStatus.IsKnown
        }

        private static class DealTrust
        {
            public const string Low = "low", Medium = "medium", High = "high", Critical = "critical";
        }

        internal static class DealKind
        {
            public const string TargetAgreement = "target_agreement";
            public const string SafetyAgreement = "safety_agreement";
            public const string VoteTogether = "vote_together";
            public const string VoteSave = "vote_save";
            public const string VoteEvict = "vote_evict";
            public const string VetoUse = "veto_use";
            public const string InformationSharing = "information_sharing";
            public const string FinalTwo = "final_two";
            public const string Partnership = "partnership";
            public const string AllianceInvite = "alliance_invite";
            public const string FinalThree = "final_three";
            // The full source initial table is kept even though DefaultTrust does not enumerate it.
            public static readonly string[] All =
            {
                TargetAgreement, SafetyAgreement, VoteTogether, VoteSave, VoteEvict,
                VetoUse, InformationSharing, FinalTwo, Partnership, AllianceInvite,
                FinalThree,
            };

            // BEGIN DealKind.DefaultTrust
            public static string DefaultTrust(string type)
            {
                switch (type)
                {
                    case VetoUse:
                    case FinalTwo: return DealTrust.Critical;
                    case TargetAgreement:
                    case SafetyAgreement:
                    case AllianceInvite:
                    // The safety pact's weight: it is broken by the same act, a nomination.
                    case FinalThree: return DealTrust.High;
                    case InformationSharing: return DealTrust.Low;
                    default: return DealTrust.Medium;
                }
            }
            // END DealKind.DefaultTrust
        }
    }
}
