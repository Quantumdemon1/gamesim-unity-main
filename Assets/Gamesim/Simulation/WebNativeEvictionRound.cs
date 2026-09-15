using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>Detached PRIVATE evidence, not a persisted ballot or public event.</summary>
    public sealed class WebNativeEvictionRoundPlan
    {
        public WebBlocRound coordination = new WebBlocRound();
        public List<WebVoteEvaluation> evaluations = new List<WebVoteEvaluation>();
    }

    /// <summary>
    /// Canonical native adapter for the original local eviction-vote-round caller.
    /// Does not invent deals, grudges, founder or stability; does not commit/reveal
    /// ballots or decide rules-version migration. The caller supplies missing voters.
    /// </summary>
    public static class WebNativeEvictionRound
    {
        public static WebNativeEvictionRoundPlan Evaluate(EpisodeState state, IEnumerable<string> voterIds)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (voterIds == null) throw new ArgumentNullException(nameof(voterIds));
            if (state.nominees == null) throw new ArgumentException("A nominee list is required.");
            if (state.nominees.Count != 2) return new WebNativeEvictionRoundPlan();
            var requested = voterIds.Take(65).ToArray();
            if (requested.Length > 64 || requested.Any(id => string.IsNullOrEmpty(id) || state.Find(id) == null))
                throw new ArgumentException("Requested voters must be known canonical contestant IDs.");
            var snapshot = state.Clone();
            var plan = new WebNativeEvictionRoundPlan
            {
                // Never filter players/already-voted people out of the bloc snapshot:
                // their eligibility and random draws affect other members' directives.
                coordination = WebVotingBlocs.ResolveRound(WebVotingBlocs.FromNative(snapshot))
            };
            var directives = plan.coordination.directives.ToDictionary(d => d.voterId, StringComparer.Ordinal);
            // serializeForVoting filters cast in its existing order, not voterIds order.
            foreach (var voter in snapshot.contestants.Where(c => requested.Contains(c.id) && !c.isPlayer))
            {
                var options = WebEvictionVoting.FromNative(snapshot, voter.id);
                var nominees = options.nominees.ToDictionary(n => n.id, StringComparer.Ordinal);
                // Source bloc selection above uses supplied nominees; per-voter source
                // evaluation instead receives nominees serialized in original cast order.
                options.nominees = snapshot.contestants.Where(c => nominees.ContainsKey(c.id)).Select(c => nominees[c.id]).ToList();
                options.blocDirective = directives.TryGetValue(voter.id, out var directive) ? directive.ForVote() : null;
                plan.evaluations.Add(WebEvictionVoting.Evaluate(options));
            }
            return plan;
        }
    }
}
