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
        /// Preserves legacy list order and every legacy scalar. Canonical Safety rows follow in their own
        /// stored order. In the prospective mode 2 the list is mode 1's, element for element: the raw rows
        /// with the canonical Vote rows merged where mode 1's one list held them (<see cref="RawPromises"/>),
        /// then the Safety rows. This does not establish chronological interleaving across authoring stores;
        /// callers selecting a latest outcome must use an explicit event/settlement ordering contract.
        /// Every returned record is detached, even in a legacy game.
        /// </summary>
        public static IReadOnlyList<PromiseState> Promises(EpisodeState state)
        {
            CheckRules(state);
            if (state.unifiedCommitmentRulesVersion == UnifiedVoteFamilyValidation.Version)
                return Array.AsReadOnly(MergedPromises(state, true).Concat(state.unifiedCommitments
                    .Where(row => row.kind == UnifiedCommitments.Safety && row.sourcePolicy == UnifiedCommitments.PromisePolicy)
                    .Select(UnifiedVoteReferences.ProjectPromise)).ToArray());
            var rows = state.promises.Select(row => row?.Clone()).ToList();
            if (UnifiedCommitments.RulesOn(state))
                rows.AddRange(state.unifiedCommitments.Where(row => row.sourcePolicy == UnifiedCommitments.PromisePolicy)
                    .Select(ProjectPromise));
            return rows.AsReadOnly();
        }

        /// <summary>Full provenance view, not a strongest-protection or once-per-incident count. Ordered as <see cref="Promises"/> is.</summary>
        public static IReadOnlyList<DealState> Deals(EpisodeState state)
        {
            CheckRules(state);
            if (state.unifiedCommitmentRulesVersion == UnifiedVoteFamilyValidation.Version)
                return Array.AsReadOnly(MergedDeals(state, true).Concat(state.unifiedCommitments
                    .Where(row => row.kind == UnifiedCommitments.Safety && row.sourcePolicy == UnifiedCommitments.DealPolicy)
                    .Select(UnifiedVoteReferences.ProjectDeal)).ToArray());
            var rows = state.deals.Select(row => row?.Clone()).ToList();
            if (UnifiedCommitments.RulesOn(state))
                rows.AddRange(state.unifiedCommitments.Where(row => row.sourcePolicy == UnifiedCommitments.DealPolicy)
                    .Select(ProjectDeal));
            return rows.AsReadOnly();
        }

        /// <summary>
        /// The promises a mode-1 reader of the raw list reads (vote family V5a): in modes 0 and 1 the raw list itself,
        /// the very instance; in the prospective mode 2 the list mode 1's would have been - the raw rows, themselves,
        /// with each canonical Vote promise projected where mode 1 appended it (<see cref="UnifiedVoteSettlement.Occurrence"/>:
        /// by the sequence its id was minted at, raw rows keeping their own order). Safety rows are not here, as mode 1's
        /// raw list holds none. A reader's view, never a writer's: nothing written to it reaches the season.
        /// </summary>
        public static IReadOnlyList<PromiseState> RawPromises(EpisodeState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (state.unifiedCommitmentRulesVersion != UnifiedVoteFamilyValidation.Version) return state.promises;
            CheckRules(state);
            return MergedPromises(state, false).AsReadOnly();
        }

        /// <summary>The deals a mode-1 reader of the raw list reads, as <see cref="RawPromises"/> gives the promises.</summary>
        public static IReadOnlyList<DealState> RawDeals(EpisodeState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (state.unifiedCommitmentRulesVersion != UnifiedVoteFamilyValidation.Version) return state.deals;
            CheckRules(state);
            return MergedDeals(state, false).AsReadOnly();
        }

        private static List<PromiseState> MergedPromises(EpisodeState s, bool detached) => Merge(s.promises, row => row.id,
            row => detached ? row.Clone() : row, s.unifiedCommitments.Where(row => row.kind == UnifiedVoteTogether.Vote
                && row.sourcePolicy == UnifiedCommitments.PromisePolicy), UnifiedVoteReferences.ProjectPromise);

        private static List<DealState> MergedDeals(EpisodeState s, bool detached) => Merge(s.deals, row => row.id,
            row => detached ? row.Clone() : row, s.unifiedCommitments.Where(row => row.kind == UnifiedVoteTogether.Vote
                && row.sourcePolicy == UnifiedCommitments.DealPolicy), UnifiedVoteReferences.ProjectDeal);

        /// <summary>
        /// Mode 1's one list from its two halves: each list keeps its own order, and a canonical row goes in before
        /// the first raw row it occurred before - an earlier sequence, or the same one as the deal a price bought.
        /// </summary>
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

        /// <summary>
        /// Canonical Safety outcomes name their actual settlement week; legacy references retain their original meaning - and
        /// so does a canonical Vote row (vote family V5e), which mode 1 holds as a legacy row dated by the week it was made.
        /// </summary>
        public static int ReceiptWeek(EpisodeState state, string id, int legacyWeek)
        {
            var row = FindCanonicalSafety(state, id);
            return row != null && row.settledWeek > 0 ? row.settledWeek : legacyWeek;
        }

        /// <summary>
        /// The canonical Safety row of this id, or null: every canonical row in modes 0 and 1, and in mode 2 the rows mode 1
        /// keeps canonical - its Vote rows are mode 1's legacy rows (vote family V5e), for a reader that keeps mode 1's meaning.
        /// </summary>
        public static UnifiedCommitmentState FindCanonicalSafety(EpisodeState state, string id)
        {
            var row = FindCanonical(state, id);
            return row != null && row.kind == UnifiedCommitments.Safety ? row : null;
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
                // The storage only (vote family V5a): a reader may run in the middle of a command, before what
                // the complete core asks of a finished one holds; the command's candidate is held to that core.
                if (!UnifiedVoteFamilyValidation.TryValidateStorage(state, out string storageError))
                    throw new ArgumentException(storageError, nameof(state));
                return;
            }
            if (!UnifiedCommitments.ValidateRecords(state, out string error))
                throw new ArgumentException(error, nameof(state));
        }
    }
}
