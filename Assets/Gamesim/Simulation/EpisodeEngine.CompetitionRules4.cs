using System;
using System.Globalization;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// Competition rules 4 in the engine: the throw, and the score explanation for the five kinds.
    /// The rules themselves are <see cref="CompetitionRules"/>.
    /// </summary>
    public sealed partial class EpisodeEngine
    {
        /// <summary>The private line that records a throw: the results card tags the player's row from it.</summary>
        public const string ThrowEventKind = "competition-throw";

        /// <summary>
        /// Throwing a weekly competition. The player still competes: their statistics and roll are
        /// scored as anyone's are, then every bonus is dropped and only part of the score counts. The
        /// result binds like any other - it is not a way out of the competition.
        /// </summary>
        private static void ThrowWeeklyCompetition(EpisodeState s, EpisodeCommand c)
        {
            Require(s.competitionRulesVersion >= CompetitionRules.Widened,
                "This season's rules throw at the floor: compete with no performance instead.");
            Require((s.phase == EpisodePhase.HoH || s.phase == EpisodePhase.Veto) && !s.competitionResolved,
                "A throw is available only for an unresolved weekly HoH or Veto.");
            Require(c.performance == 0, "A thrown competition does not accept performance input.");
            Require(CompetitionPlayers(s).Any(contestant => contestant.isPlayer), "Only a player in the field can throw it.");
            ResolveCompetition(s, 0, thrown: true);
        }

        private static string AwardName(EpisodePhase phase) =>
            phase == EpisodePhase.Veto ? "Power of Veto" : phase == EpisodePhase.HoH ? "Head of Household" : "Final Head of Household";

        /// <summary>
        /// A rules-4 score, term by term, adding up to the committed score as the rules-3 explanation
        /// does - with luck's squeezed statistic and second roll, social's weights, and a throw's cut.
        /// A throw leads with its story: where it finished, and - when it won anyway - how.
        /// </summary>
        /// <param name="raw">The player's score before a throw's cut; their committed score otherwise.</param>
        private static string WidenedCompetitionExplanation(EpisodeState state, ContestantState actor, string category,
            double performance, bool simulated, bool thrown, double roll, double luckRoll, double raw)
        {
            var stats = actor.stats;
            decimal physical = 0, mental = 0, endurance = 0, social = 0, luck = 0;
            double weighted;
            string luckNote = "";
            if (category == CompetitionRules.Luck || category == CompetitionRules.Social)
            {
                weighted = CompetitionRules.Base(stats, category);
                if (category == CompetitionRules.Luck)
                {
                    mental = DisplayedScore(stats.mental * .1);
                    luck = DisplayedScore(CompetitionRules.SqueezedLuck(stats.luck) * .9);
                    luckNote = " (" + ScoreNumber(stats.luck) + " squeezed to " + ScoreNumber(CompetitionRules.SqueezedLuck(stats.luck)) + ")";
                }
                else
                {
                    mental = DisplayedScore(stats.mental * .2);
                    social = DisplayedScore(stats.social * .6);
                    luck = DisplayedScore(stats.luck * .2);
                }
            }
            else
            {
                // The reference's weights, one statistic at a time at the neutral roll, as rules 3 does.
                decimal Part(Action<ContestantStats> only)
                {
                    var basis = EmptyCompetitionStats();
                    only(basis);
                    return DisplayedScore(WebRules.WeightedCompetitionScore(basis, category, false, 0, .5));
                }
                physical = Part(basis => basis.physical = stats.physical);
                mental = Part(basis => basis.mental = stats.mental);
                endurance = Part(basis => basis.endurance = stats.endurance);
                social = Part(basis => basis.social = stats.social);
                luck = Part(basis => basis.luck = stats.luck);
                weighted = WebRules.WeightedCompetitionScore(stats, category, false, 0, .5);
            }
            bool twice = CompetitionRules.RollsTwice(category);
            decimal chance = DisplayedScore(weighted * (.75 + roll * .5) - weighted);
            decimal second = DisplayedScore(twice ? luckRoll * 3 : 0);
            decimal nominee = DisplayedScore(CompetitionRules.Clutch(stats, state.nominees.Contains(actor.id)));
            decimal haveNot = DisplayedScore(-HaveNots.Penalty(state, actor.id));
            decimal preparation = thrown ? 0m : DisplayedScore(state.playerStudyBonus);
            decimal phaseEvent = thrown ? 0m : DisplayedScore(state.phaseEventCompBonus);
            decimal storyline = thrown ? 0m : DisplayedScore(Storylines.CompetitionBonus(state));
            decimal manual = DisplayedScore(simulated || thrown ? 0 : performance * CompetitionRules.PerformanceWeight(state.competitionRulesVersion));
            double committed = state.competitionScores.First(entry => entry.contestantId == actor.id).score;
            decimal cut = thrown ? DisplayedScore(committed - raw) : 0m;
            decimal total = DisplayedScore(committed);
            decimal rounding = total - (physical + mental + endurance + social + luck + chance + second + nominee + haveNot
                + preparation + phaseEvent + storyline + manual + cut);

            string lead = thrown ? ThrowStory(state, actor, committed) + " Thrown: every bonus given up"
                : simulated ? "Simulated: no performance bonus"
                : "Played: performance " + ScoreNumber(performance * 100) + "%";
            string text = lead + ". Your weighted stats: physical " + Number(physical) + "; mental " + Number(mental)
                + "; endurance " + Number(endurance) + "; social " + Number(social) + "; luck " + Number(luck) + luckNote
                + ". Chance " + SignedScore(chance) + " (sampled multiplier " + (.75 + roll * .5).ToString("0.######", CultureInfo.InvariantCulture) + ")"
                + (twice ? "; second roll " + SignedScore(second) + " (" + luckRoll.ToString("0.######", CultureInfo.InvariantCulture) + " × 3)" : "")
                + "; nominee " + SignedScore(nominee)
                + (haveNot != 0 ? "; have-not " + SignedScore(haveNot) : "");
            if (thrown)
                text += "; throw " + SignedScore(cut) + " (" + Percent(CompetitionRules.ThrowShare(state.competitionScores.Count)) + "% of the score counts)";
            else
                text += "; preparation " + SignedScore(preparation) + "; event " + SignedScore(phaseEvent)
                    + "; storyline " + SignedScore(storyline) + "; performance " + SignedScore(manual);
            text += "; rounding " + SignedScore(rounding) + ". Sum = " + total.ToString("0.00", CultureInfo.InvariantCulture)
                + ". All displayed terms add to this committed score.";
            if (!thrown)
                text += " Total player bonus " + SignedScore(preparation + phaseEvent + storyline + manual)
                    + ". Zero performance can still win; full marks do not guarantee first place.";
            return text;
        }

        /// <summary>
        /// Where a throw finished, in the words the player is owed: that it lost, or that it won
        /// anyway - they got lucky - and how a throw can: it lowers only their own score.
        /// </summary>
        public static string ThrowStory(EpisodeState state, ContestantState actor, double committed)
        {
            int field = state.competitionScores.Count;
            double share = CompetitionRules.ThrowShare(field);
            string rule = "A throw gives up every bonus" + (share < 1
                ? " and counts " + Percent(share) + "% of your score in a field of " + field : "") + ".";
            // The same order that crowned the winner: highest first, the earlier entry on a tie.
            var ordered = state.competitionScores.OrderByDescending(entry => entry.score).ToList();
            if (ordered[0].contestantId == actor.id)
            {
                var next = ordered.Count > 1 ? ordered[1] : null;
                // The runner-up's score and not their name: the explanation is the player's own
                // account, and the standings beside it already say who.
                return "You threw it and won anyway: you got lucky. " + rule
                    + " It lowers only your own score, and yours came to " + ScoreNumber(committed)
                    + (next != null ? " while the best of the rest scored " + ScoreNumber(next.score) : "")
                    + ".";
            }
            int place = ordered.FindIndex(entry => entry.contestantId == actor.id) + 1;
            return "You threw it. " + rule + " Yours came to " + ScoreNumber(committed) + ", " + Ordinal(place) + " of " + field + ".";
        }

        private static string Number(decimal value) => value.ToString(CultureInfo.InvariantCulture);

        private static string Percent(double share) => Math.Round(share * 100).ToString("0", CultureInfo.InvariantCulture);

        private static string Ordinal(int place)
        {
            int tens = place % 100;
            string suffix = tens >= 11 && tens <= 13 ? "th" : (place % 10) == 1 ? "st" : (place % 10) == 2 ? "nd" : (place % 10) == 3 ? "rd" : "th";
            return place.ToString(CultureInfo.InvariantCulture) + suffix;
        }
    }
}
