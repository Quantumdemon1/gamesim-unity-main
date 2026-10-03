using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// The final Head of Household's choice when it is a houseguest's (ACTIONS-DEALS-ALLIANCES-PLAN
    /// C9): which of the other two finalists they take to the final two. One function, so the
    /// engine's final eviction and anything that measures it read the same choice.
    /// </summary>
    public sealed partial class EpisodeEngine
    {
        /// <summary>
        /// The final Head of Household's choice, weighed as the eviction vote weighs a ballot: the two
        /// finalists other than the Head of Household as its nominees, the one with the lower score
        /// evicted (<see cref="WebVoteEvaluation.selectedNomineeId"/>). Without memories or the
        /// player's persona, as the source's fast-forward final selection supplies none; under the
        /// commitment rules (C1) a final two deal is an obligation in it (<see cref="FinalTwoTerms"/>).
        /// Pure: no roll, no write. A tie is broken by the evaluator's own keyed draw, not the season's.
        /// </summary>
        public static WebVoteEvaluation FinalChoice(EpisodeState s)
        {
            var finalContext = s.Clone();
            finalContext.nominees = s.Active.Where(c => c.id != s.hohId).Select(c => c.id).ToList();
            var finalOptions = WebEvictionVoting.FromNative(finalContext, s.hohId);
            // Match the source fast-forward final-selection caller, which supplies no memory/persona context.
            finalOptions.memories.Clear(); finalOptions.playerPersonaLabel = null;
            // Under the commitment rules (C1) a final two deal is a real obligation in the
            // choice, not the web's deal term alone (about 4.5 points after its weight).
            if (CommitmentRulesOn(s)) finalOptions.obligations.AddRange(FinalTwoTerms(s, s.hohId, finalContext.nominees));
            return WebEvictionVoting.Evaluate(finalOptions);
        }
    }
}
