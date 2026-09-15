using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    public sealed partial class EpisodeEngine
    {
        // Migrated/imported snapshots retain their captured week's original evaluator and ordering.
        // Even an untouched old week-one save waits until week two. New native games start at one.
        private static bool UsesBlocVoting(EpisodeState state) => state.week >= state.blocRulesStartWeek;

        private static Dictionary<string, WebVoteEvaluation> PrepareBlocBallots(EpisodeState state, string[] missingIds)
        {
            if (!UsesBlocVoting(state) || missingIds.Length == 0) return null;
            Require(state.phase == EpisodePhase.Eviction && !state.evictionResolved, "Only unresolved regular eviction votes can be coordinated.");
            // The adapter clones the snapshot and retains the full cast/player/committed membership
            // for source coordination draws; it evaluates only these missing NPC voters.
            var plan = WebNativeEvictionRound.Evaluate(state, missingIds);
            Require(plan.evaluations.Count == missingIds.Length && plan.evaluations.All(item => missingIds.Contains(item.voterId)),
                "The private coordination plan must cover exactly the requested missing ballots.");
            // No private directives, diagnostic reasoning, or plan enter the persistent EpisodeState.
            // The caller stores only selected targets and privacy-filtered public explanations.
            return plan.evaluations.ToDictionary(item => item.voterId);
        }

        private static WebVoteEvaluation EvaluateRegularHohTieBreak(EpisodeState state)
        {
            if (!UsesBlocVoting(state)) return WebEvictionVoting.EvaluateNative(state, state.hohId);
            Require(state.phase == EpisodePhase.Eviction && !state.evictionResolved && state.hohId != state.playerId,
                "An NPC HoH tie-break must belong to the unresolved regular eviction.");
            // Requested only after the actual tally ties. HoH is excluded from bloc directives.
            // Existing private ballots have changed no score/memory/persona inputs; all vote
            // consequences remain deferred until the caller's existing public reveal boundary.
            return WebNativeEvictionRound.Evaluate(state, new[] { state.hohId }).evaluations.Single();
        }
    }
}
