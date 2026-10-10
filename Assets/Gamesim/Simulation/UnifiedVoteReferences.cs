using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// Detached source-shaped views for the explicit prospective aggregate only. Unchecked
    /// methods are internal leaves: their caller must have validated identities and row shape.
    /// They never call the whole episode validator, write raw mirrors or grant ballot knowledge.
    /// </summary>
    internal static class UnifiedVoteReferences
    {
        internal static IReadOnlyList<PromiseState> PromisesUnchecked(EpisodeState s) =>
            Array.AsReadOnly(s.promises.Select(row => row.Clone()).Concat(s.unifiedCommitments
                .Where(row => row.sourcePolicy == UnifiedCommitments.PromisePolicy)
                .Select(ProjectPromise)).ToArray());

        internal static IReadOnlyList<DealState> DealsUnchecked(EpisodeState s) =>
            Array.AsReadOnly(s.deals.Select(row => row.Clone()).Concat(s.unifiedCommitments
                .Where(row => row.sourcePolicy == UnifiedCommitments.DealPolicy)
                .Select(ProjectDeal)).ToArray());

        internal static PromiseState ProjectPromise(UnifiedCommitmentState row) => new PromiseState
        {
            id = row.id, fromId = row.makerId, toId = row.beneficiaryId, targetId = row.targetId,
            kind = row.kind == UnifiedCommitments.Safety ? PromiseKind.Safety : PromiseKind.Vote,
            status = Status(row.status), week = row.createdWeek, expiresWeek = row.expiresWeek,
            impact = row.trustImpact, brokenById = row.brokenById, settledWeek = row.settledWeek
        };

        internal static DealState ProjectDeal(UnifiedCommitmentState row) => new DealState
        {
            id = row.id, proposerId = row.makerId, recipientId = row.beneficiaryId,
            targetId = row.targetId, type = row.kind == UnifiedCommitments.Safety
                ? DealKind.SafetyAgreement : row.subtype,
            status = row.status, week = row.createdWeek, expiresWeek = row.expiresWeek,
            trustImpact = row.trustImpact, brokenById = row.brokenById,
            settledWeek = row.settledWeek, linkedDealId = row.linkedCommitmentId
        };

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
    }
}
