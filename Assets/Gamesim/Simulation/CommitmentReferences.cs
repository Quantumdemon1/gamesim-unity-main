using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// Detached, source-shaped references for readers being moved to the single safety authority.
    /// These are NOT writable mirrors, settlement verdicts, or deduplicated mechanical scores.
    /// All evidence remains visible; a future incident reader must group betrayal effects separately.
    /// Version 1 still cannot be created, loaded or played by the production engine.
    /// </summary>
    public static class CommitmentReferences
    {
        /// <summary>
        /// Preserves legacy list order and every legacy scalar. Canonical rows follow in their own
        /// stored order. This does not establish chronological interleaving across authoring stores;
        /// callers selecting a latest outcome must use an explicit event/settlement ordering contract.
        /// Every returned record is detached, even in a legacy game.
        /// </summary>
        public static IReadOnlyList<PromiseState> Promises(EpisodeState state)
        {
            CheckRules(state);
            if (state.unifiedCommitmentRulesVersion == UnifiedVoteFamilyValidation.Version)
                return UnifiedVoteReferences.PromisesUnchecked(state);
            var rows = state.promises.Select(row => row?.Clone()).ToList();
            if (UnifiedCommitments.RulesOn(state))
                rows.AddRange(state.unifiedCommitments.Where(row => row.sourcePolicy == UnifiedCommitments.PromisePolicy)
                    .Select(ProjectPromise));
            return rows.AsReadOnly();
        }

        /// <summary>Full provenance view, not a strongest-protection or once-per-incident count.</summary>
        public static IReadOnlyList<DealState> Deals(EpisodeState state)
        {
            CheckRules(state);
            if (state.unifiedCommitmentRulesVersion == UnifiedVoteFamilyValidation.Version)
                return UnifiedVoteReferences.DealsUnchecked(state);
            var rows = state.deals.Select(row => row?.Clone()).ToList();
            if (UnifiedCommitments.RulesOn(state))
                rows.AddRange(state.unifiedCommitments.Where(row => row.sourcePolicy == UnifiedCommitments.DealPolicy)
                    .Select(ProjectDeal));
            return rows.AsReadOnly();
        }

        /// <summary>Historical rows count too; moving safety authority cannot free an authoring slot.</summary>
        public static int PromiseCount(EpisodeState state)
        {
            CheckRules(state);
            if (state.unifiedCommitmentRulesVersion == UnifiedVoteFamilyValidation.Version)
                return state.promises.Count + state.unifiedCommitments.Count(row => row.sourcePolicy == UnifiedCommitments.PromisePolicy);
            return state.promises.Count + (UnifiedCommitments.RulesOn(state)
                ? state.unifiedCommitments.Count(row => row.sourcePolicy == UnifiedCommitments.PromisePolicy) : 0);
        }

        /// <summary>Includes every legacy non-safety family, NPC row and settled canonical deal.</summary>
        public static int DealCount(EpisodeState state)
        {
            CheckRules(state);
            if (state.unifiedCommitmentRulesVersion == UnifiedVoteFamilyValidation.Version)
                return state.deals.Count + state.unifiedCommitments.Count(row => row.sourcePolicy == UnifiedCommitments.DealPolicy);
            return state.deals.Count + (UnifiedCommitments.RulesOn(state)
                ? state.unifiedCommitments.Count(row => row.sourcePolicy == UnifiedCommitments.DealPolicy) : 0);
        }

        public static PromiseState FindPromise(EpisodeState state, string id) =>
            id == null ? null : Promises(state).FirstOrDefault(row => row?.id == id);

        public static DealState FindDeal(EpisodeState state, string id) =>
            id == null ? null : Deals(state).FirstOrDefault(row => row?.id == id);

        /// <summary>Canonical outcomes name their actual settlement week; legacy references retain their original meaning.</summary>
        public static int ReceiptWeek(EpisodeState state, string id, int legacyWeek)
        {
            var row = FindCanonical(state, id);
            return row != null && row.settledWeek > 0 ? row.settledWeek : legacyWeek;
        }

        /// <summary>Includes the canonical effect identity, which a legacy-shaped DTO cannot carry.</summary>
        public static UnifiedCommitmentState FindCanonical(EpisodeState state, string id)
        {
            CheckRules(state);
            if (state.unifiedCommitmentRulesVersion == UnifiedVoteFamilyValidation.Version)
                return id == null ? null : state.unifiedCommitments.FirstOrDefault(row => row.id == id)?.Clone();
            return id == null || !UnifiedCommitments.RulesOn(state) ? null
                : state.unifiedCommitments.FirstOrDefault(row => row.id == id)?.Clone();
        }

        private static PromiseState ProjectPromise(UnifiedCommitmentState row) => new PromiseState
        {
            id = row.id, fromId = row.makerId, toId = row.beneficiaryId, targetId = null,
            kind = PromiseKind.Safety, status = PromiseStatusFor(row.status),
            week = row.createdWeek, expiresWeek = row.expiresWeek, impact = row.trustImpact,
            brokenById = row.brokenById, settledWeek = row.settledWeek,
        };

        private static DealState ProjectDeal(UnifiedCommitmentState row) => new DealState
        {
            id = row.id, proposerId = row.makerId, recipientId = row.beneficiaryId, targetId = null,
            type = DealKind.SafetyAgreement, status = row.status,
            week = row.createdWeek, expiresWeek = row.expiresWeek, trustImpact = row.trustImpact,
            brokenById = row.brokenById, settledWeek = row.settledWeek, linkedDealId = row.linkedCommitmentId,
        };

        private static PromiseStatus PromiseStatusFor(string status)
        {
            switch (status)
            {
                case DealStatus.Active: return PromiseStatus.Active;
                case DealStatus.Broken: return PromiseStatus.Broken;
                case DealStatus.Expired: return PromiseStatus.Expired;
                default: throw new ArgumentException("Unsupported canonical safety-promise status.");
            }
        }

        private static void CheckRules(EpisodeState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            // Do not add new validity checks to legacy reader paths or rewrite their records.
            if (state.unifiedCommitmentRulesVersion == 0) return;
            if (state.unifiedCommitmentRulesVersion == UnifiedVoteFamilyValidation.Version)
            {
                if (!UnifiedVoteFamilyValidation.TryValidate(state, state.unifiedVoteReveals, out string aggregateError))
                    throw new ArgumentException(aggregateError, nameof(state));
                return;
            }
            if (!UnifiedCommitments.ValidateRecords(state, out string error))
                throw new ArgumentException(error, nameof(state));
        }
    }
}
