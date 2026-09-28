using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// The levers (STRATEGY-LOOP-PLAN.md §3): what the player can do to move a vote, and the rule
    /// boundary they run under. A vote deal with a voter enters their ballot as an obligation, sized
    /// so a deal and a standing flip a torn voter and never a firm one; the reveal judges every vote
    /// deal; bought time is the week's, not the season's; a lie sounds like one to the person told;
    /// and every lever says what it moved, in the read's own terms.
    /// </summary>
    public sealed partial class EpisodeEngine
    {
        /// <summary>An obligation's size before their view of the player scales it, and the view at which it is whole.</summary>
        public const double ObligationBase = 8, ObligationFullView = 50;
        /// <summary>A Loyal voter's word is worth half as much again. A Sneaky voter's is worth nothing.</summary>
        public const double LoyalObligation = 1.5;

        /// <summary>
        /// Switches the levers on for a season from <paramref name="fromWeek"/>. The director calls
        /// this for every season it starts; the save migration and the importer do the same from the
        /// week after a save's, so the week it was in plays under the rules it was played under.
        /// </summary>
        public static void EnableLevers(EpisodeState s, int fromWeek = 1)
        {
            if (s == null) throw new ArgumentNullException(nameof(s));
            s.leverRulesStartWeek = Math.Max(1, Math.Min(fromWeek, s.week + 1));
        }

        /// <summary>Whether the levers run this week.</summary>
        public static bool LeverRulesOn(EpisodeState s) => s != null && s.leverRulesStartWeek >= 1 && s.week >= s.leverRulesStartWeek;

        /// <summary>
        /// What a voter owes the player on this vote: every active vote deal between them that names
        /// somebody on the block, as a term on that nominee. Base 8, scaled by their view of the player
        /// (nothing at a view of zero or less, whole at fifty), Loyal x1.5, Sneaky x0: a friend keeps
        /// their word, a stranger does not, and a Sneaky voter's word was never worth anything. Sized
        /// so a deal and a standing flip a torn voter and never a firm one; relationship and alliance
        /// still dominate, which is the point.
        /// </summary>
        public static List<WebVoteObligation> Obligations(EpisodeState s, string voterId)
        {
            var terms = new List<WebVoteObligation>();
            if (!LeverRulesOn(s) || string.IsNullOrEmpty(voterId) || voterId == s.playerId) return terms;
            var voter = s.Find(voterId);
            if (voter == null) return terms;
            double scale = Math.Max(0, Math.Min(1, s.Score(voterId, s.playerId) / ObligationFullView));
            double word = voter.traits.Contains("Sneaky") ? 0 : voter.traits.Contains("Loyal") ? LoyalObligation : 1;
            foreach (var deal in s.deals.Where(d => d.status == DealStatus.Active
                         && (d.type == DealKind.VoteSave || d.type == DealKind.VoteEvict)
                         && d.targetId != null && s.nominees.Contains(d.targetId)
                         && ((d.proposerId == s.playerId && d.recipientId == voterId) || (d.proposerId == voterId && d.recipientId == s.playerId))))
            {
                var term = terms.FirstOrDefault(t => t.nomineeId == deal.targetId);
                if (term == null) terms.Add(term = new WebVoteObligation { nomineeId = deal.targetId });
                term.value += ObligationBase * scale * word * (deal.type == DealKind.VoteSave ? 1 : -1);
                term.evidenceIds.Add(deal.id);
            }
            return terms;
        }

        /// <summary>The nominee the player wants out under a vote deal: the one it names for a vote to evict, the other one for a vote to keep.</summary>
        private static string WantsOut(EpisodeState s, string type, string aboutId) =>
            type == DealKind.VoteEvict ? aboutId : s.nominees.FirstOrDefault(id => id != aboutId);

        /// <summary>A voter's read as a lever finds it, or null where there is no vote to read or they do not vote.</summary>
        private static VoteRead.VoterRead LeverRead(EpisodeState s, string voterId) =>
            VoteRead.Available(s) && Voters(s).Any(v => v.id == voterId && !v.isPlayer) ? VoteRead.ReadVoter(s, voterId, ProjectBallot(s, voterId)) : null;

        /// <summary>
        /// A lever says what it moved, in the read's terms: "Riley: torn → leaning evict Jo (+9,
        /// your deal)". The number is how far the voter's known lean moved toward evicting
        /// <paramref name="wantsOutId"/>. Where nothing of the voter is known yet the read cannot
        /// move, so the line says what the lever is worth to them instead.
        /// </summary>
        private static void LeverLine(EpisodeState s, string voterId, VoteRead.VoterRead before, string wantsOutId, string why)
        {
            if (before == null || wantsOutId == null || !VoteRead.Available(s)) return;
            var after = VoteRead.ReadVoter(s, voterId, ProjectBallot(s, voterId));
            string line;
            if (after.confidence == VoteRead.Unknown)
            {
                double worth = Obligations(s, voterId).Where(t => t.nomineeId == wantsOutId).Sum(t => -t.value)
                    + Obligations(s, voterId).Where(t => t.nomineeId != wantsOutId).Sum(t => t.value);
                line = Name(s, voterId) + ": no read yet; " + why + " is worth " + Signed(worth) + " toward evicting " + Name(s, wantsOutId) + ".";
            }
            else
            {
                double moved = Toward(after, wantsOutId) - Toward(before, wantsOutId);
                line = Name(s, voterId) + ": " + VoteRead.Describe(s, before) + " → " + VoteRead.Describe(s, after) + " (" + Signed(moved) + ", " + why + ")";
            }
            Log(s, "lever", line, s.playerId, voterId);
        }

        private static string Signed(double value) => (value >= 0 ? "+" : "") + value.ToString("0");

        private static double Toward(VoteRead.VoterRead read, string wantsOutId) =>
            read.leaningId == null ? 0 : read.leaningId == wantsOutId ? read.knownMargin : -read.knownMargin;
    }
}
