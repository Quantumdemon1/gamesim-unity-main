using System;
using System.Linq;
using Gamesim.Simulation;

namespace Gamesim.Presentation
{
    /// <summary>Read-only decision facts. Never reads reciprocal trust, NPC plans or private pairs.</summary>
    public static class DecisionContext
    {
        public sealed class Candidate
        {
            public ContestantState Character { get; }
            public string Record { get; }
            public string Relationship { get; }
            public string Promises { get; }
            internal Candidate(ContestantState character, string record, string relationship, string promises)
            { Character = character.Clone(); Record = record; Relationship = relationship; Promises = promises; }
        }

        public static Candidate ForCandidate(EpisodeState state, string id)
        {
            var actor = state?.Find(id);
            if (actor == null) return null;
            string relationship = id == state.playerId ? "Your houseguest"
                : "Your trust: " + state.Score(state.playerId, id).ToString("+0;-0;0")
                    + (state.Allied(state.playerId, id) ? " · Shared alliance" : "");
            var promises = state.promises.Where(p =>
                (p.fromId == state.playerId && p.toId == id) || (p.fromId == id && p.toId == state.playerId)).ToArray();
            string known = promises.Length == 0 ? "No promises recorded between you."
                : string.Join("\n", promises.Reverse().Take(2).Select(p =>
                    (p.fromId == state.playerId ? "You promised: " : "Promised to you: ") + PromiseName(p.kind)
                    + " · " + p.status.ToString().ToLowerInvariant()))
                    + (promises.Length > 2 ? "\n+" + (promises.Length - 2) + " other promises between you" : "");
            return new Candidate(actor, "HoH " + actor.hohWins + " · Veto " + actor.vetoWins
                + " · Nominated " + actor.timesNominated, relationship, known);
        }

        public static Candidate[] Participants(EpisodeState state, HouseEventState item) =>
            item?.involvedIds == null ? Array.Empty<Candidate>() : item.involvedIds.Distinct(StringComparer.Ordinal)
                .Select(id => ForCandidate(state, id)).Where(candidate => candidate != null).ToArray();

        public static string KnownEventSummary(EpisodeState state)
        {
            var reading = HouseVibe.Of(state);
            return reading.Activity + " activity · " + reading.Commitments + " commitments · "
                + reading.GameStakes + " game stakes this week";
        }

        private static string PromiseName(PromiseKind kind)
        {
            switch (kind)
            {
                case PromiseKind.Safety: return "safety";
                case PromiseKind.Vote: return "a vote";
                case PromiseKind.FinalTwo: return "final two";
                case PromiseKind.AllianceLoyalty: return "alliance loyalty";
                case PromiseKind.Information: return "information";
                default: return "a commitment";
            }
        }
    }
}
