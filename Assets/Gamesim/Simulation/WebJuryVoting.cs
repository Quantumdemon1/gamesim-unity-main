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
    /// why a richer jury costs no schema version and cannot disagree with the save. The one term
    /// that reads a choice of the player's own is the final argument (schema 21,
    /// <see cref="FinalArgument.Term"/>), and it reads it from the save.</para>
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
        /// What an alliance with this finalist is worth to this juror: 100 for an alliance still
        /// standing, 25 for one that ended, 0 for none.
        ///
        /// <para>These tiers are this port's own. The reference function it follows,
        /// <c>calculateAllianceLoyalty</c>, scores by membership and alliance size and never reads
        /// stability - and as the reference ships it always returns 0 (it asks the alliance system
        /// for a method that does not exist), inside a jury score the live reference game never
        /// calls: its real jury votes on relationship and a random ten either way. Kept deliberately
        /// on 2026-09-22 rather than zeroed to match a dead function.</para>
        ///
        /// <para>An alliance ends when a member is evicted - at the next weekly settle, or at the
        /// final eviction itself - so every juror's former ally scores the same 25. Until the final
        /// eviction ended alliances too, its evictee was the one juror whose former ally still read
        /// as current and scored 100.</para>
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
                   + (Obligations(state, jurorId, finalistId) + 50) * ObligationWeight
                   // Native: a bitter juror is not noise. A grudge, a kept or betrayed showmance, a
                   // nemesis. Zero on an empty story state, so every jury fixture holds.
                   + StoryConsumers.JuryStory(state, jurorId, finalistId)
                   // Schema 21 (ENDGAME-PLAN F4b): the player's final argument, for a juror whose
                   // theme it argues, capped under the final impression. Zero on every old save.
                   + FinalArgument.Term(state, jurorId, finalistId);
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
        /// <summary>
        /// One of three phrasings, by the juror's seat in the cast list, so four jurors giving the
        /// same reason do not give it in the same words. Deterministic and roll-free: a reason is
        /// presentation, and the vote it explains is already decided.
        /// </summary>
        private static string Say(EpisodeState state, string jurorId, string first, string second, string third)
        {
            int seat = 0;
            for (int index = 0; index < state.contestants.Count; index++)
                if (state.contestants[index] != null && state.contestants[index].id == jurorId) { seat = index; break; }
            switch (seat % 3)
            {
                case 1: return second;
                case 2: return third;
                default: return first;
            }
        }

        public static string Reason(EpisodeState state, string jurorId, ContestantState chosen, ContestantState other)
        {
            double likedChosen = state.Score(jurorId, chosen.id);
            double likedOther = state.Score(jurorId, other.id);
            double respectChosen = GameplayRespect(chosen);
            double respectOther = GameplayRespect(other);

            // The interesting case, and the one the old rule could never produce: a juror who prefers
            // the other finalist as a person and votes against them anyway.
            if (likedOther > likedChosen && respectChosen > respectOther)
                return Say(state, jurorId, "They played the better game, whatever I think of them.", "I don't like them. They played the better game, and this vote is about the game.", "The better game wins, even when I would rather it didn't.");
            if (respectOther > respectChosen && likedChosen > likedOther)
                return Say(state, jurorId, "I know what they did in here, and I am voting with my gut anyway.", "My gut says this, and I have stopped arguing with it.", "I could list what they did. I am going with my gut instead.");
            // What a story left between them, when it is what tipped it: a grudge the juror carried
            // into the jury house, or a bond that held.
            double storyChosen = StoryConsumers.JuryStory(state, jurorId, chosen.id);
            double storyOther = StoryConsumers.JuryStory(state, jurorId, other.id);
            if (storyOther <= -8 && storyChosen > storyOther)
                return Say(state, jurorId, "Some things you do not get over in a jury house.", "I remember how it went between me and the other one. So do they.", "I had weeks to think about what happened. It did not get better.");
            if (storyChosen >= 8)
                return Say(state, jurorId, "What we had in there was real. I am not pretending otherwise.", "They never turned on me. I am not turning on them.", "Some people you keep, whatever the game says.");
            // The final argument, when it spoke to what this juror values (ENDGAME-PLAN F4b).
            if (FinalArgument.Term(state, jurorId, chosen.id) >= 2 * FinalArgument.PerMoment)
            {
                // Words about a speech only where one was given: a player may lock the argument and
                // then let their game speak for itself.
                bool spoke = state.finalSpeeches.Any(x => x.speakerId == chosen.id && !string.IsNullOrWhiteSpace(x.text));
                return spoke
                    ? Say(state, jurorId, "Their final argument was about what I value in this game.", "They made the case I came here to hear.", "What they said at the end spoke to me.")
                    : Say(state, jurorId, "Their season was the kind of game I value.", "The game they played is the one I respect.", "Their record made the case on its own.");
            }
            if (state.Allied(jurorId, chosen.id))
                return Say(state, jurorId, "We were in this together and I am not walking away from that now.", "We had an alliance, and I am keeping my end of it tonight.", "I do not abandon people I made plans with. This is that.");
            if (Obligations(state, jurorId, chosen.id) > 10)
                return Say(state, jurorId, "They kept their word to me when it cost them something.", "Every deal they made me, they kept. That decides it.", "They paid for keeping a promise to me. I am paying it back.");
            if (respectChosen > respectOther)
                return Say(state, jurorId, "They won when they had to. That is the game.", "When it mattered, they won. That is what I respect.", "They took the competitions that counted. That is a winner.");
            return Say(state, jurorId, "Personal trust and this juror's final impression.", "Trust, mostly, and how they left me feeling at the end.", "It comes down to who I believe, and I believe them.");
        }
    }
}
