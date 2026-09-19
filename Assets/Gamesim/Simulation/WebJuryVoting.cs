using System;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// How a juror decides who deserves to win.
    ///
    /// <para>Until this existed a juror voted on <c>relationship + a roll</c> and nothing else — the
    /// finalist who had been nicer to them won, and how either of them actually played the game was
    /// worth nothing. That is the bitter jury with no other kind available, and it makes a whole
    /// category of play pointless: winning competitions, surviving the block and outmanoeuvring
    /// people all stopped mattering the moment the finale began.</para>
    ///
    /// <para>Ported from <c>ai/fallback-generator.ts</c>'s <c>calculateJuryScore</c> and
    /// <c>calculateGameplayRespect</c>, weights and all. The plan singles this file out as worth
    /// having regardless of the rest of Tier 6, and the reason is in the source's own note: it is
    /// reproducible, which is what makes NPC behaviour testable without a network.</para>
    ///
    /// <para><b>Nothing here is new machinery.</b> Every term reads something the simulation already
    /// records — competition wins, times nominated, the strategic stat, alliances, deals — which is
    /// why a richer jury costs no schema version and cannot disagree with the save.</para>
    /// </summary>
    public static class WebJuryVoting
    {
        /// <summary>The source's four weights. They sum to one; the balance is the point.</summary>
        public const double RelationshipWeight = 0.3;
        public const double RespectWeight = 0.4;
        public const double LoyaltyWeight = 0.15;
        public const double ObligationWeight = 0.15;

        /// <summary>What each kind of win is worth to a juror's respect. The source's numbers.</summary>
        public const double PerHohWin = 8, PerVetoWin = 6, PerNomination = 5;

        /// <summary>What reaching the end is worth before anything else is counted.</summary>
        public const double MadeItToTheEnd = 20;

        /// <summary>Respect is capped, so a competition beast cannot run away with it alone.</summary>
        public const double RespectCeiling = 100;

        /// <summary>
        /// How much a juror respects how a finalist played, regardless of liking them.
        ///
        /// <para>Head of Household wins count for more than veto wins, surviving the block counts
        /// for something on its own — a finalist nominated four times and still standing played a
        /// different game from one never nominated at all — and reaching the end at all is worth
        /// twenty before anything else is added.</para>
        /// </summary>
        public static double GameplayRespect(ContestantState finalist)
        {
            if (finalist == null) return 0;
            double respect = MadeItToTheEnd
                + finalist.hohWins * PerHohWin
                + finalist.vetoWins * PerVetoWin
                + finalist.timesNominated * PerNomination
                + finalist.stats.strategic;
            return Math.Min(RespectCeiling, respect);
        }

        /// <summary>
        /// What an alliance with this finalist is worth to this juror.
        ///
        /// <para>The source keeps a stability figure per alliance; this port does not, so an active
        /// shared alliance is worth its full weight and a dissolved one is worth nothing. That is
        /// the honest reading of what is recorded rather than a stability number invented to fill
        /// the shape.</para>
        /// </summary>
        public static double AllianceLoyalty(EpisodeState state, string jurorId, string finalistId)
        {
            if (state == null) return 0;
            if (state.Allied(jurorId, finalistId)) return 100;
            // An alliance that existed and ended is not the same as never having had one. Somebody
            // who was with you and left is remembered differently from a stranger.
            bool broken = state.alliances.Any(a => !a.active
                                                   && a.members.Contains(jurorId)
                                                   && a.members.Contains(finalistId));
            return broken ? 25 : 0;
        }

        /// <summary>
        /// What was agreed between them, and whether it was kept.
        ///
        /// <para>Returns the source's −50 to 50, which <see cref="Score"/> then normalises. Deals
        /// and promises both count: a finalist who kept their word to this juror is owed something,
        /// and one who broke it is owed the opposite. This reads the systems built earlier in this
        /// port, which is why it could not have been written before them.</para>
        /// </summary>
        public static double Obligations(EpisodeState state, string jurorId, string finalistId)
        {
            if (state == null) return 0;
            double score = 0;

            foreach (var deal in state.deals.Where(d => Between(d.proposerId, d.recipientId, jurorId, finalistId)))
            {
                if (deal.status == DealStatus.Fulfilled) score += 20 * DealTrust.Weight(deal.trustImpact);
                else if (deal.status == DealStatus.Broken) score -= 25 * DealTrust.Weight(deal.trustImpact);
                else if (deal.status == DealStatus.Active) score += 10;
            }

            foreach (var promise in state.promises.Where(p => Between(p.fromId, p.toId, jurorId, finalistId)))
            {
                if (promise.status == PromiseStatus.Fulfilled) score += 15;
                else if (promise.status == PromiseStatus.Broken) score -= 20;
            }

            return Math.Max(-50, Math.Min(50, score));
        }

        private static bool Between(string a, string b, string first, string second) =>
            (a == first && b == second) || (a == second && b == first);

        /// <summary>
        /// What this juror thinks this finalist is worth, all in.
        ///
        /// <para>The source's four terms in its proportions: thirty per cent how much they like
        /// them, forty per cent how much they respect the game, and fifteen each for an alliance and
        /// for what was agreed. Respect outweighing affection is the whole design — it is what makes
        /// a jury capable of crowning somebody it does not much like.</para>
        /// </summary>
        public static double Score(EpisodeState state, string jurorId, string finalistId)
        {
            var finalist = state?.Find(finalistId);
            if (finalist == null) return 0;
            return state.Score(jurorId, finalistId) * RelationshipWeight
                   + GameplayRespect(finalist) * RespectWeight
                   + AllianceLoyalty(state, jurorId, finalistId) * LoyaltyWeight
                   // Normalised from the source's -50..50 into 0..100 before weighting, exactly as
                   // it does — otherwise the term would drag every score down by a fixed amount.
                   + (Obligations(state, jurorId, finalistId) + 50) * ObligationWeight;
        }

        /// <summary>
        /// How much of a final impression a juror brings with them, either way.
        ///
        /// <para>The jitter this port already applied. It is kept, and kept the same size, because a
        /// finale with no uncertainty in it is a finale whose result was known several weeks ago.
        /// </para>
        /// </summary>
        public const double FinalImpression = 10;

        /// <summary>Why a juror voted as they did, in a sentence the reveal can show.</summary>
        public static string Reason(EpisodeState state, string jurorId, ContestantState chosen, ContestantState other)
        {
            double likedChosen = state.Score(jurorId, chosen.id);
            double likedOther = state.Score(jurorId, other.id);
            double respectChosen = GameplayRespect(chosen);
            double respectOther = GameplayRespect(other);

            // The interesting case, and the one the old rule could never produce: a juror who prefers
            // the other finalist as a person and votes against them anyway.
            if (likedOther > likedChosen && respectChosen > respectOther)
                return "They played the better game, whatever I think of them.";
            if (respectOther > respectChosen && likedChosen > likedOther)
                return "I know what they did in here, and I am voting with my gut anyway.";
            if (state.Allied(jurorId, chosen.id))
                return "We were in this together and I am not walking away from that now.";
            if (Obligations(state, jurorId, chosen.id) > 10)
                return "They kept their word to me when it cost them something.";
            if (respectChosen > respectOther)
                return "They won when they had to. That is the game.";
            return "Personal trust and this juror's final impression.";
        }
    }
}
