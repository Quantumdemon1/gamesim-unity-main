using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>A private, detached decision value, not a saved settlement or an effect plan.</summary>
    public sealed class UnifiedVoteObligationVerdict
    {
        public readonly string Status, ActorId;
        internal UnifiedVoteObligationVerdict(string status, string actorId)
        { Status = status; ActorId = actorId; }
    }

    /// <summary>
    /// Inactive Vote-promise and targeted-deal predicates. These helpers neither install a row
    /// nor prove its producer, term, links, admission or historical Active status. Public Vote
    /// authority remains unsupported; truth evidence grants no private-ballot knowledge.
    /// </summary>
    public static class UnifiedVoteObligations
    {
        public const int MaximumIdentityLength = UnifiedVoteTogether.MaximumIdentityLength;
        public const int MaximumBallots = UnifiedVoteTogether.MaximumBallots;

        /// <summary>
        /// Local row/block/whole-ballot shape first, then a possible native C0 verdict. Valid
        /// inactive rows, absent makers/parties and a targeted deal off this block decide nothing.
        /// ActorId is the source decision actor; a fulfilled writer must NOT persist it as a
        /// breaker. Two actual targeted breakers produce Broken with no sole ActorId.
        /// </summary>
        public static bool TryVerdict(UnifiedCommitmentState row,
            IReadOnlyList<UnifiedVoteBallotState> ballots, IReadOnlyList<string> nominees,
            out UnifiedVoteObligationVerdict verdict, out string error)
        {
            verdict = null; error = null;
            if (row == null || row.kind != UnifiedVoteTogether.Vote || !Token(row.id)
                || !Token(row.makerId) || !Token(row.beneficiaryId)
                || string.Equals(row.makerId, row.beneficiaryId, StringComparison.Ordinal))
                return Fail(out error, "A Vote obligation requires valid distinct parties and a row identity.");
            bool promise = row.sourcePolicy == UnifiedCommitments.PromisePolicy;
            if (promise)
            {
                if (row.reciprocal || row.subtype != null
                    || (row.targetId == null ? row.origin != UnifiedCommitments.StoryPromise : !Token(row.targetId))
                    || (row.status != DealStatus.Active && row.status != DealStatus.Fulfilled
                        && row.status != DealStatus.Broken && row.status != DealStatus.Expired))
                    return Fail(out error, "A unilateral Vote promise requires its native target, Story-null distinction and promise status.");
            }
            else if (row.sourcePolicy != UnifiedCommitments.DealPolicy || !row.reciprocal
                || (row.subtype != DealKind.VoteSave && row.subtype != DealKind.VoteEvict)
                || !Token(row.targetId) || !DealStatus.IsKnown(row.status))
                return Fail(out error, "A targeted Vote deal requires a reciprocal Save or Evict duty, named target and known status.");
            if (nominees == null || nominees.Count != 2 || !Token(nominees[0]) || !Token(nominees[1])
                || string.Equals(nominees[0], nominees[1], StringComparison.Ordinal))
                return Fail(out error, "A Vote obligation requires the two distinct final nominees.");
            if (ballots == null || ballots.Count < 0 || ballots.Count > MaximumBallots)
                return Fail(out error, "An actual Vote snapshot must be nonnull and within the house bound.");

            var voters = new HashSet<string>(StringComparer.Ordinal);
            string makerTarget = null, beneficiaryTarget = null;
            for (int index = 0; index < ballots.Count; index++)
            {
                var ballot = ballots[index];
                if (ballot == null || !Token(ballot.voterId) || !Token(ballot.targetId)
                    || IsNominee(nominees, ballot.voterId) || !IsNominee(nominees, ballot.targetId)
                    || !voters.Add(ballot.voterId))
                    return Fail(out error, "Actual Vote ballots must be unique eligible local identities targeting the final block.");
                if (string.Equals(ballot.voterId, row.makerId, StringComparison.Ordinal)) makerTarget = ballot.targetId;
                else if (string.Equals(ballot.voterId, row.beneficiaryId, StringComparison.Ordinal)) beneficiaryTarget = ballot.targetId;
            }
            // Even inactive rows and off-block targets must not bypass malformed evidence.
            if (row.status != DealStatus.Active) return true;
            if (promise)
            {
                if (makerTarget != null)
                    verdict = new UnifiedVoteObligationVerdict(
                        string.Equals(row.targetId, makerTarget, StringComparison.Ordinal) ? DealStatus.Fulfilled : DealStatus.Broken,
                        row.makerId);
                return true;
            }
            if (!IsNominee(nominees, row.targetId)) return true;
            string kept = null, broke = null;
            // Source proposer/recipient order, NOT private-box insertion order.
            foreach (var party in new[] { row.makerId, row.beneficiaryId })
            {
                string target = party == row.makerId ? makerTarget : beneficiaryTarget;
                if (target == null) continue;
                bool evictedTarget = string.Equals(target, row.targetId, StringComparison.Ordinal);
                bool breaks = row.subtype == DealKind.VoteEvict ? !evictedTarget : evictedTarget;
                if (breaks)
                {
                    if (broke != null)
                    { verdict = new UnifiedVoteObligationVerdict(DealStatus.Broken, null); return true; }
                    broke = party;
                }
                else if (kept == null) kept = party;
            }
            if (broke != null) verdict = new UnifiedVoteObligationVerdict(DealStatus.Broken, broke);
            else if (kept != null) verdict = new UnifiedVoteObligationVerdict(DealStatus.Fulfilled, kept);
            return true;
        }

        /// <summary>
        /// Proves the COMPLETE separate fresh-source archive before selecting its real regular
        /// frame and durable block. Does not install evidence or prove this row was admitted or
        /// Active at that historical instant; pass a retained predecision projection, not a
        /// terminal row reset to Active. Final selection and pending/jury ballots are refused.
        /// </summary>
        public static bool TryFromArchive(EpisodeState state,
            IReadOnlyList<UnifiedVoteRevealState> completeArchive, UnifiedCommitmentState row,
            int revealWeek, out UnifiedVoteObligationVerdict verdict, out string error)
        {
            verdict = null; error = null;
            if (!UnifiedVoteRevealArchive.TryValidateComplete(state, completeArchive, out error)) return false;
            var frame = completeArchive.FirstOrDefault(candidate => candidate.week == revealWeek);
            if (frame == null) return Fail(out error, "A Vote verdict requires an actual archived completed regular reveal.");
            var power = state.ledger.power.FirstOrDefault(candidate => candidate.week == revealWeek);
            var ids = new HashSet<string>(state.contestants.Select(person => person.id), StringComparer.Ordinal);
            if (row == null || !ids.Contains(row.makerId ?? "") || !ids.Contains(row.beneficiaryId ?? "")
                || (row.targetId != null && !ids.Contains(row.targetId)))
                return Fail(out error, "Vote parties and any named target must resolve to the stored cast.");
            return TryVerdict(row, frame.ballots, power.nominees, out verdict, out error);
        }

        private static bool IsNominee(IReadOnlyList<string> nominees, string id) =>
            string.Equals(nominees[0], id, StringComparison.Ordinal) || string.Equals(nominees[1], id, StringComparison.Ordinal);
        private static bool Token(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > MaximumIdentityLength) return false;
            for (int index = 0; index < value.Length; index++) if (char.IsControl(value[index])) return false;
            return true;
        }
        private static bool Fail(out string error, string reason) { error = reason; return false; }
    }
}
