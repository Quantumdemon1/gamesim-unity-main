using System;
using System.Collections.Generic;

namespace Gamesim.Simulation
{
    /// <summary>
    /// A local, read-only VoteTogether verdict from an already-normalized actual ballot snapshot.
    /// The reference DealSystem's evaluateVoteDeal compares both parties' latest ballot targets;
    /// its public owner processes only active rows and owns the subsequent lazy expiry.
    /// </summary>
    /// <remarks>
    /// This is not full origin/date/cast/save validation, a settlement writer, an expiry owner,
    /// or evidence that anybody knows a private ballot. It creates no effects, keys or archive
    /// entries and does not enable the currently unsupported unified Vote authority mode.
    /// </remarks>
    public static class UnifiedVoteTogether
    {
        public const string Vote = "vote";
        public const int MaximumIdentityLength = 160;
        public const int MaximumBallots = EpisodeValidation.MaximumCast;

        /// <summary>
        /// Returns false for malformed local inputs. A valid inactive row or a missing actual
        /// party ballot succeeds with a null verdict. Otherwise equal targets fulfill the bloc
        /// and different targets break it, without choosing a sole breaker or changing the row.
        /// Repeated voter inputs must be normalized to their latest actual ballot by the caller;
        /// duplicates are refused here, never silently resolved by list order.
        /// </summary>
        public static bool TryVerdict(UnifiedCommitmentState row,
            IReadOnlyList<UnifiedVoteBallotState> ballots, out string verdict, out string error)
        {
            verdict = null;
            error = null;
            if (row == null) return Fail(out error, "A VoteTogether row is required.");
            if (row.kind != Vote || row.sourcePolicy != UnifiedCommitments.DealPolicy
                || row.subtype != DealKind.VoteTogether || row.targetId != null)
                return Fail(out error, "A reciprocal vote/deal/vote_together row has no named target.");
            if (!Token(row.id) || !Token(row.makerId) || !Token(row.beneficiaryId)
                || string.Equals(row.makerId, row.beneficiaryId, StringComparison.Ordinal))
                return Fail(out error, "A VoteTogether row requires valid distinct party identities and an id.");
            if (!row.reciprocal)
                return Fail(out error, "VoteTogether is a reciprocal agreement.");
            if (!DealStatus.IsKnown(row.status))
                return Fail(out error, "A VoteTogether row requires a known status.");
            if (ballots == null || ballots.Count > MaximumBallots)
                return Fail(out error, "An actual ballot snapshot must be present and within the house bound.");

            var voters = new HashSet<string>(StringComparer.Ordinal);
            string makerTarget = null, beneficiaryTarget = null;
            for (var index = 0; index < ballots.Count; index++)
            {
                var ballot = ballots[index];
                if (ballot == null || !Token(ballot.voterId) || !Token(ballot.targetId))
                    return Fail(out error, "Every actual ballot requires valid voter and target identities.");
                if (!voters.Add(ballot.voterId))
                    return Fail(out error, "Actual ballots must have unique voter identities.");
                if (string.Equals(ballot.voterId, row.makerId, StringComparison.Ordinal))
                    makerTarget = ballot.targetId;
                else if (string.Equals(ballot.voterId, row.beneficiaryId, StringComparison.Ordinal))
                    beneficiaryTarget = ballot.targetId;
            }

            // Validate the complete snapshot even when the caller supplied an inactive row.
            // Source lifecycle and full completed-reveal eligibility remain separate owners.
            if (row.status != DealStatus.Active || makerTarget == null || beneficiaryTarget == null)
                return true;
            verdict = string.Equals(makerTarget, beneficiaryTarget, StringComparison.Ordinal)
                ? DealStatus.Fulfilled : DealStatus.Broken;
            return true;
        }

        private static bool Token(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > MaximumIdentityLength) return false;
            for (var index = 0; index < value.Length; index++)
                if (char.IsControl(value[index])) return false;
            return true;
        }

        private static bool Fail(out string error, string message)
        {
            error = message;
            return false;
        }
    }
}
