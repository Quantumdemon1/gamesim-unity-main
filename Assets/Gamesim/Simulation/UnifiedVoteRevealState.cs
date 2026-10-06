using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// Inert schema26 storage for a future actual completed Vote reveal. Private truth is not a
    /// knowledge grant. Current supported authority modes require the root archive to stay empty.
    /// </summary>
    [Serializable]
    public sealed class UnifiedVoteRevealState
    {
        public int week;
        public List<UnifiedVoteBallotState> ballots = new List<UnifiedVoteBallotState>();

        public UnifiedVoteRevealState Clone()
        {
            var copy = (UnifiedVoteRevealState)MemberwiseClone();
            // Preserve malformed nulls for validation to refuse rather than normalizing them away.
            copy.ballots = ballots?.Select(ballot => ballot?.Clone()).ToList();
            return copy;
        }
    }

    /// <summary>One future archived actual ballot, not a public claim or a listener receipt.</summary>
    [Serializable]
    public sealed class UnifiedVoteBallotState
    {
        public string voterId, targetId;

        public UnifiedVoteBallotState Clone() => (UnifiedVoteBallotState)MemberwiseClone();
    }
}
