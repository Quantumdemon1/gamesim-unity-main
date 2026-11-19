// Frozen30 (talk S0, schema 31's step 1): the schema-30 whole reader, a copy of FrozenV29ReferenceRows.cs by name
// (FrozenV29 to FrozenV30, Frozen29 to Frozen30), then by exactly the live validators' changes from b208d46d to
// 64d52f59, each marked DELTA 64d52f59. Frozen29 carried FrozenV28ReferenceRows.cs to schema 29; the provenance comments below
// describe the images it was carried from.
// UNWIRED source-backed allocation and scalar-copy adaptations. Never used to repair hydration.
using Gamesim.Persistence.Frozen30Data;

namespace Gamesim.Persistence.Frozen30Semantics
{
    internal static class FrozenV30ReferenceRows
    {
        // BEGIN Defaults.PromiseState
        internal static PromiseState NewPromise() => new PromiseState
        {
            id = null, fromId = null, toId = null, targetId = null,
            kind = PromiseKind.Safety, status = PromiseStatus.Active, week = 0, expiresWeek = 0,
            impact = "medium", brokenById = null, settledWeek = 0
        };
        // END Defaults.PromiseState

        // BEGIN Defaults.DealState
        internal static DealState NewDeal() => new DealState
        {
            id = null, type = "partnership", proposerId = null, recipientId = null, targetId = null,
            status = "proposed", week = 0, expiresWeek = 0, trustImpact = "medium",
            brokenById = null, settledWeek = 0, linkedDealId = null
        };
        // END Defaults.DealState

        // BEGIN PromiseState.Clone
        internal static PromiseState Clone(PromiseState row) => new PromiseState
        {
            id = row.id,
            fromId = row.fromId,
            toId = row.toId,
            targetId = row.targetId,
            kind = row.kind,
            status = row.status,
            week = row.week,
            expiresWeek = row.expiresWeek,
            impact = row.impact,
            brokenById = row.brokenById,
            settledWeek = row.settledWeek,
        };
        // END PromiseState.Clone

        // Source null-conditional Clone call: evaluate receiver once and copy only a nonnull row.
        internal static PromiseState CloneOrNull(PromiseState row) => row == null ? null : Clone(row);

        // BEGIN DealState.Clone
        internal static DealState Clone(DealState row) => new DealState
        {
            id = row.id,
            type = row.type,
            proposerId = row.proposerId,
            recipientId = row.recipientId,
            targetId = row.targetId,
            status = row.status,
            week = row.week,
            expiresWeek = row.expiresWeek,
            trustImpact = row.trustImpact,
            brokenById = row.brokenById,
            settledWeek = row.settledWeek,
            linkedDealId = row.linkedDealId,
        };
        // END DealState.Clone

        // Source null-conditional Clone call: evaluate receiver once and copy only a nonnull row.
        internal static DealState CloneOrNull(DealState row) => row == null ? null : Clone(row);

        // BEGIN UnifiedCommitmentState.Clone
        internal static UnifiedCommitmentState Clone(UnifiedCommitmentState row) => new UnifiedCommitmentState
        {
            id = row.id,
            kind = row.kind,
            sourcePolicy = row.sourcePolicy,
            origin = row.origin,
            makerId = row.makerId,
            beneficiaryId = row.beneficiaryId,
            reciprocal = row.reciprocal,
            createdWeek = row.createdWeek,
            expiresWeek = row.expiresWeek,
            status = row.status,
            settledWeek = row.settledWeek,
            brokenById = row.brokenById,
            trustImpact = row.trustImpact,
            linkedCommitmentId = row.linkedCommitmentId,
            settlementEffectKey = row.settlementEffectKey,
            targetId = row.targetId,
            subtype = row.subtype,
            voteBindingWeek = row.voteBindingWeek,
            voteFirstRevealWeek = row.voteFirstRevealWeek,
            // DELTA b208d46d (schema 29): the live Clone deep-copies the conditional terms.
            conditionalVeto = row.conditionalVeto == null ? null : new ConditionalVetoTermsState
            {
                statementId = row.conditionalVeto.statementId,
                consentEventId = row.conditionalVeto.consentEventId,
                consentPathIndex = row.conditionalVeto.consentPathIndex,
                consentRevision = row.conditionalVeto.consentRevision,
                resolution = row.conditionalVeto.resolution == null ? null : new ConditionalVetoResolutionState
                {
                    reason = row.conditionalVeto.resolution.reason,
                    departedId = row.conditionalVeto.resolution.departedId,
                    departedStatus = row.conditionalVeto.resolution.departedStatus,
                    revision = row.conditionalVeto.resolution.revision,
                    sequence = row.conditionalVeto.resolution.sequence,
                    vetoOccasionWeek = row.conditionalVeto.resolution.vetoOccasionWeek,
                },
            },
        };
        // END UnifiedCommitmentState.Clone

        // Source null-conditional Clone call: evaluate receiver once and copy only a nonnull row.
        internal static UnifiedCommitmentState CloneOrNull(UnifiedCommitmentState row) => row == null ? null : Clone(row);

    }
}
