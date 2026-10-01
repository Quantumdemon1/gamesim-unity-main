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
            /// <summary>
            /// The deals between the player and them, newest first, each with its term and where it
            /// stands (ACTIONS-DEALS-ALLIANCES-PLAN V1): the comparison listed promises and never
            /// deals, so a safety deal was forgotten at the very ceremony that breaks it. Null when
            /// there are none, so the comparison is no taller for a candidate with nothing to say.
            /// </summary>
            public string Deals { get; }
            internal Candidate(ContestantState character, string record, string relationship, string promises, string deals)
            { Character = character.Clone(); Record = record; Relationship = relationship; Promises = promises; Deals = deals; }
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
                + " · Nominated " + actor.timesNominated, relationship, known, Deals(state, id));
        }

        /// <summary>
        /// "Deal: Safety deal you proposed · this week · agreed", the newest two and a count of the
        /// rest: only deals the player is a party to, read as the Your word page reads them.
        /// </summary>
        private static string Deals(EpisodeState state, string id)
        {
            if (id == state.playerId) return null;
            var deals = CommitmentsRead.With(state, id).Where(c => c.kind == CommitmentsRead.Kinds.Deal).ToList();
            if (deals.Count == 0) return null;
            string Line(CommitmentsRead.Commitment deal)
            {
                var about = state.Find(deal.aboutId);
                return "Deal: " + deal.title + (about != null ? " (" + FinalistRead.FirstName(about.name) + ")" : "")
                    + " · " + deal.term + " · " + deal.status;
            }
            return string.Join("\n", Enumerable.Reverse(deals).Take(2).Select(Line))
                + (deals.Count > 2 ? "\n+" + (deals.Count - 2) + " other deals between you" : "");
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
