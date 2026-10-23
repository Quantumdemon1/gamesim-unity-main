// UNWIRED complete selected reference layer; storage success is not whole-reader acceptance.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Gamesim.Persistence.Frozen28Data;
using UnifiedCommitments = Gamesim.Persistence.Frozen28Semantics.FrozenV28SafetyStorage;
using UnifiedVoteFamilyValidation = Gamesim.Persistence.Frozen28Semantics.FrozenV28VoteStorage;
using UnifiedVoteReferences = Gamesim.Persistence.Frozen28Semantics.FrozenV28VoteReferences;
using UnifiedVoteSettlement = Gamesim.Persistence.Frozen28Semantics.FrozenV28Occurrence;
using UnifiedVoteTogether = Gamesim.Persistence.Frozen28Semantics.FrozenV28ReferenceKind;
using CommitmentReferences = Gamesim.Persistence.Frozen28Semantics.FrozenV28CommitmentReferences;
using DealKind = Gamesim.Persistence.Frozen28Semantics.FrozenV28VoteStorage.DealKind;
using DealStatus = Gamesim.Persistence.Frozen28Semantics.FrozenV28ReferenceStatus;
using Negotiation = Gamesim.Persistence.Frozen28Semantics.FrozenV28Occurrence;

namespace Gamesim.Persistence.Frozen28Semantics
{
    internal static class FrozenV28CommitmentReferences
    {
        private const string RefusalMark = "Gamesim.Simulation.CommitmentStorageRefusal";

        // BEGIN CommitmentReferences.Promises
        internal static IReadOnlyList<PromiseState> Promises(EpisodeState state)
        {
            CheckRules(state);
            if (state.unifiedCommitmentRulesVersion == UnifiedVoteFamilyValidation.Version)
                return Array.AsReadOnly(MergedPromises(state, true).Concat(state.unifiedCommitments
                    .Where(row => row.kind == UnifiedCommitments.Safety && row.sourcePolicy == UnifiedCommitments.PromisePolicy)
                    .Select(UnifiedVoteReferences.ProjectPromise)).ToArray());
            var rows = state.promises.Select(row => FrozenV28ReferenceRows.CloneOrNull(row)).ToList();
            if (UnifiedCommitments.RulesOn(state))
                rows.AddRange(state.unifiedCommitments.Where(row => row.sourcePolicy == UnifiedCommitments.PromisePolicy)
                    .Select(ProjectPromise));
            return rows.AsReadOnly();
        }
        // END CommitmentReferences.Promises

        // BEGIN CommitmentReferences.Deals
        internal static IReadOnlyList<DealState> Deals(EpisodeState state)
        {
            CheckRules(state);
            if (state.unifiedCommitmentRulesVersion == UnifiedVoteFamilyValidation.Version)
                return Array.AsReadOnly(MergedDeals(state, true).Concat(state.unifiedCommitments
                    .Where(row => row.kind == UnifiedCommitments.Safety && row.sourcePolicy == UnifiedCommitments.DealPolicy)
                    .Select(UnifiedVoteReferences.ProjectDeal)).ToArray());
            var rows = state.deals.Select(row => FrozenV28ReferenceRows.CloneOrNull(row)).ToList();
            if (UnifiedCommitments.RulesOn(state))
                rows.AddRange(state.unifiedCommitments.Where(row => row.sourcePolicy == UnifiedCommitments.DealPolicy)
                    .Select(ProjectDeal));
            return rows.AsReadOnly();
        }
        // END CommitmentReferences.Deals

        // BEGIN CommitmentReferences.RawPromises
        internal static IReadOnlyList<PromiseState> RawPromises(EpisodeState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (state.unifiedCommitmentRulesVersion != UnifiedVoteFamilyValidation.Version) return state.promises;
            CheckRules(state);
            return MergedPromises(state, false).AsReadOnly();
        }
        // END CommitmentReferences.RawPromises

        // BEGIN CommitmentReferences.RawDeals
        internal static IReadOnlyList<DealState> RawDeals(EpisodeState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (state.unifiedCommitmentRulesVersion != UnifiedVoteFamilyValidation.Version) return state.deals;
            CheckRules(state);
            return MergedDeals(state, false).AsReadOnly();
        }
        // END CommitmentReferences.RawDeals

        // BEGIN CommitmentReferences.MergedPromises
        private static List<PromiseState> MergedPromises(EpisodeState s, bool detached) => Merge(s.promises, row => row.id,
            row => detached ? FrozenV28ReferenceRows.Clone(row) : row, s.unifiedCommitments.Where(row => row.kind == UnifiedVoteTogether.Vote
                && row.sourcePolicy == UnifiedCommitments.PromisePolicy), UnifiedVoteReferences.ProjectPromise);
        // END CommitmentReferences.MergedPromises

        // BEGIN CommitmentReferences.MergedDeals
        private static List<DealState> MergedDeals(EpisodeState s, bool detached) => Merge(s.deals, row => row.id,
            row => detached ? FrozenV28ReferenceRows.Clone(row) : row, s.unifiedCommitments.Where(row => row.kind == UnifiedVoteTogether.Vote
                && row.sourcePolicy == UnifiedCommitments.DealPolicy), UnifiedVoteReferences.ProjectDeal);
        // END CommitmentReferences.MergedDeals

        // BEGIN CommitmentReferences.Merge
        private static List<T> Merge<T>(List<T> raw, Func<T, string> id, Func<T, T> own, IEnumerable<UnifiedCommitmentState> canonical,
            Func<UnifiedCommitmentState, T> project)
        {
            var rows = canonical.ToList();
            var merged = new List<T>(raw.Count + rows.Count);
            int next = 0;
            for (int index = 0; index < raw.Count; index++)
            {
                var key = UnifiedVoteSettlement.Occurrence(id(raw[index]), false, index);
                while (next < rows.Count && UnifiedVoteSettlement.Occurrence(rows[next].id, true, next).CompareTo(key) < 0)
                    merged.Add(project(rows[next++]));
                merged.Add(own(raw[index]));
            }
            while (next < rows.Count) merged.Add(project(rows[next++]));
            return merged;
        }
        // END CommitmentReferences.Merge

        // BEGIN CommitmentReferences.PromiseCount
        internal static int PromiseCount(EpisodeState state)
        {
            CheckRules(state);
            if (state.unifiedCommitmentRulesVersion == UnifiedVoteFamilyValidation.Version)
                return state.promises.Count + state.unifiedCommitments.Count(row => row.sourcePolicy == UnifiedCommitments.PromisePolicy);
            return state.promises.Count + (UnifiedCommitments.RulesOn(state)
                ? state.unifiedCommitments.Count(row => row.sourcePolicy == UnifiedCommitments.PromisePolicy) : 0);
        }
        // END CommitmentReferences.PromiseCount

        // BEGIN CommitmentReferences.DealCount
        internal static int DealCount(EpisodeState state)
        {
            CheckRules(state);
            if (state.unifiedCommitmentRulesVersion == UnifiedVoteFamilyValidation.Version)
                return state.deals.Count + state.unifiedCommitments.Count(row => row.sourcePolicy == UnifiedCommitments.DealPolicy);
            return state.deals.Count + (UnifiedCommitments.RulesOn(state)
                ? state.unifiedCommitments.Count(row => row.sourcePolicy == UnifiedCommitments.DealPolicy) : 0);
        }
        // END CommitmentReferences.DealCount

        // BEGIN CommitmentReferences.FindPromise
        internal static PromiseState FindPromise(EpisodeState state, string id) =>
            id == null ? null : Promises(state).FirstOrDefault(row => row?.id == id);
        // END CommitmentReferences.FindPromise

        // BEGIN CommitmentReferences.FindDeal
        internal static DealState FindDeal(EpisodeState state, string id) =>
            id == null ? null : Deals(state).FirstOrDefault(row => row?.id == id);
        // END CommitmentReferences.FindDeal

        // BEGIN CommitmentReferences.ReceiptWeek
        internal static int ReceiptWeek(EpisodeState state, string id, int legacyWeek)
        {
            var row = FindCanonicalSafety(state, id);
            return row != null && row.settledWeek > 0 ? row.settledWeek : legacyWeek;
        }
        // END CommitmentReferences.ReceiptWeek

        // BEGIN CommitmentReferences.FindCanonicalSafety
        internal static UnifiedCommitmentState FindCanonicalSafety(EpisodeState state, string id)
        {
            var row = FindCanonical(state, id);
            return row != null && row.kind == UnifiedCommitments.Safety ? row : null;
        }
        // END CommitmentReferences.FindCanonicalSafety

        // BEGIN CommitmentReferences.FindCanonical
        internal static UnifiedCommitmentState FindCanonical(EpisodeState state, string id)
        {
            CheckRules(state);
            if (state.unifiedCommitmentRulesVersion == UnifiedVoteFamilyValidation.Version)
                return id == null ? null : FrozenV28ReferenceRows.CloneOrNull(state.unifiedCommitments.FirstOrDefault(row => row.id == id));
            return id == null || !UnifiedCommitments.RulesOn(state) ? null
                : FrozenV28ReferenceRows.CloneOrNull(state.unifiedCommitments.FirstOrDefault(row => row.id == id));
        }
        // END CommitmentReferences.FindCanonical

        // BEGIN CommitmentReferences.ProjectPromise
        internal static PromiseState ProjectPromise(UnifiedCommitmentState row)
        {
            var projected = FrozenV28ReferenceRows.NewPromise();
            projected.id = row.id;
            projected.fromId = row.makerId;
            projected.toId = row.beneficiaryId;
            projected.targetId = null;
            projected.kind = PromiseKind.Safety;
            projected.status = PromiseStatusFor(row.status);
            projected.week = row.createdWeek;
            projected.expiresWeek = row.expiresWeek;
            projected.impact = row.trustImpact;
            projected.brokenById = row.brokenById;
            projected.settledWeek = row.settledWeek;
            return projected;
        }
        // END CommitmentReferences.ProjectPromise

        // BEGIN CommitmentReferences.ProjectDeal
        internal static DealState ProjectDeal(UnifiedCommitmentState row)
        {
            var projected = FrozenV28ReferenceRows.NewDeal();
            projected.id = row.id;
            projected.proposerId = row.makerId;
            projected.recipientId = row.beneficiaryId;
            projected.targetId = null;
            projected.type = DealKind.SafetyAgreement;
            projected.status = row.status;
            projected.week = row.createdWeek;
            projected.expiresWeek = row.expiresWeek;
            projected.trustImpact = row.trustImpact;
            projected.brokenById = row.brokenById;
            projected.settledWeek = row.settledWeek;
            projected.linkedDealId = row.linkedCommitmentId;
            return projected;
        }
        // END CommitmentReferences.ProjectDeal

        // BEGIN CommitmentReferences.PromiseStatusFor
        internal static PromiseStatus PromiseStatusFor(string status)
        {
            switch (status)
            {
                case DealStatus.Active: return PromiseStatus.Active;
                case DealStatus.Broken: return PromiseStatus.Broken;
                case DealStatus.Expired: return PromiseStatus.Expired;
                default: throw StorageRefusal("Unsupported canonical safety-promise status.");
            }
        }
        // END CommitmentReferences.PromiseStatusFor

        // BEGIN CommitmentReferences.CheckRules
        private static void CheckRules(EpisodeState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            // Do not add new validity checks to legacy reader paths or rewrite their records.
            if (state.unifiedCommitmentRulesVersion == 0) return;
            if (state.unifiedCommitmentRulesVersion == UnifiedVoteFamilyValidation.Version)
            {
                // The storage only (vote family V5a): a reader may run in the middle of a command, before what
                // the complete core asks of a finished one holds; the command's candidate is held to that core.
                if (!UnifiedVoteFamilyValidation.TryValidateStorage(state, out string storageError))
                    throw StorageRefusal(storageError, nameof(state));
                return;
            }
            if (!UnifiedCommitments.ValidateRecords(state, out string error))
                throw StorageRefusal(error, nameof(state));
        }
        // END CommitmentReferences.CheckRules

        // BEGIN CommitmentReferences.StorageRefusal
        internal static ArgumentException StorageRefusal(string error, string paramName = null)
        {
            var refusal = paramName == null ? new ArgumentException(error) : new ArgumentException(error, paramName);
            refusal.Data[RefusalMark] = true;
            return refusal;
        }
        // END CommitmentReferences.StorageRefusal

        // BEGIN CommitmentReferences.IsStorageRefusal
        internal static bool IsStorageRefusal(Exception error) =>
            error != null && error.GetType() == typeof(ArgumentException) && error.Data.Contains(RefusalMark);
        // END CommitmentReferences.IsStorageRefusal
    }

    internal static class FrozenV28VoteReferences
    {
        // BEGIN UnifiedVoteReferences.ProjectPromise
        internal static PromiseState ProjectPromise(UnifiedCommitmentState row)
        {
            var projected = FrozenV28ReferenceRows.NewPromise();
            projected.id = row.id;
            projected.fromId = row.makerId;
            projected.toId = row.beneficiaryId;
            projected.targetId = row.targetId;
            projected.kind = row.kind == UnifiedCommitments.Safety ? PromiseKind.Safety : PromiseKind.Vote;
            projected.status = Status(row.status);
            projected.week = row.createdWeek;
            projected.expiresWeek = row.expiresWeek;
            projected.impact = row.trustImpact;
            projected.brokenById = row.brokenById;
            projected.settledWeek = row.settledWeek;
            return projected;
        }
        // END UnifiedVoteReferences.ProjectPromise

        // BEGIN UnifiedVoteReferences.ProjectDeal
        internal static DealState ProjectDeal(UnifiedCommitmentState row)
        {
            var projected = FrozenV28ReferenceRows.NewDeal();
            projected.id = row.id;
            projected.proposerId = row.makerId;
            projected.recipientId = row.beneficiaryId;
            projected.targetId = row.targetId;
            projected.type = row.kind == UnifiedCommitments.Safety ? DealKind.SafetyAgreement : row.subtype;
            projected.status = row.status;
            projected.week = row.createdWeek;
            projected.expiresWeek = row.expiresWeek;
            projected.trustImpact = row.trustImpact;
            projected.brokenById = row.brokenById;
            projected.settledWeek = row.settledWeek;
            projected.linkedDealId = row.linkedCommitmentId;
            return projected;
        }
        // END UnifiedVoteReferences.ProjectDeal

        // BEGIN UnifiedVoteReferences.Status
        private static PromiseStatus Status(string status)
        {
            switch (status)
            {
                case DealStatus.Active: return PromiseStatus.Active;
                case DealStatus.Fulfilled: return PromiseStatus.Fulfilled;
                case DealStatus.Broken: return PromiseStatus.Broken;
                case DealStatus.Expired: return PromiseStatus.Expired;
                default: throw CommitmentReferences.StorageRefusal("Only a validated promise status may be projected.");
            }
        }
        // END UnifiedVoteReferences.Status
    }

    internal static class FrozenV28Occurrence
    {
        public const string PricePrefix = "deal-price-";

        // BEGIN UnifiedVoteSettlement.Occurrence
        internal static (long sequence, int price, int canonical, int index) Occurrence(string id, bool canonical, int index)
        {
            long sequence = long.MaxValue;
            int dash = id?.LastIndexOf('-') ?? -1;
            if (dash >= 0 && long.TryParse(id.Substring(dash + 1), NumberStyles.None, CultureInfo.InvariantCulture, out long parsed))
                sequence = parsed;
            bool price = id != null && id.StartsWith(Negotiation.PricePrefix, StringComparison.Ordinal);
            return (sequence, price ? 1 : 0, canonical ? 1 : 0, index);
        }
        // END UnifiedVoteSettlement.Occurrence
    }

    internal static class FrozenV28ReferenceStatus
    {
        public const string Active = "active", Fulfilled = "fulfilled", Broken = "broken", Expired = "expired";
    }

    internal static class FrozenV28ReferenceKind
    {
        public const string Vote = "vote";
    }
}
