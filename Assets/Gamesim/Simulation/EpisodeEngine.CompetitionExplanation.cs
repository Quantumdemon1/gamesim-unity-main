using System;
using System.Globalization;
using System.Linq;

namespace Gamesim.Simulation
{
    public sealed partial class EpisodeEngine
    {
        private static decimal DisplayedScore(double value) => Math.Round((decimal)value, 2, MidpointRounding.AwayFromZero);
        private static string ScoreNumber(double value) => DisplayedScore(value).ToString("0.##", CultureInfo.InvariantCulture);
        private static string SignedScore(decimal value) => value.ToString("+0.##;-0.##;0", CultureInfo.InvariantCulture);

        private static ContestantStats EmptyCompetitionStats() => new ContestantStats
        { physical = 0, mental = 0, endurance = 0, social = 0, luck = 0 };

        // Neutral roll .5 has multiplier 1. These pure calls reuse the scoring weights and consume
        // no random draws. The five default stats must explicitly be zero for basis components.
        private static string WeightedCompetitionExplanation(EpisodeState state, ContestantState actor,
            string category, double performance, bool simulated, double roll, double committedScore)
        {
            var basis = EmptyCompetitionStats(); basis.physical = actor.stats.physical;
            decimal physical = DisplayedScore(WebRules.WeightedCompetitionScore(basis, category, false, 0, .5));
            basis = EmptyCompetitionStats(); basis.mental = actor.stats.mental;
            decimal mental = DisplayedScore(WebRules.WeightedCompetitionScore(basis, category, false, 0, .5));
            basis = EmptyCompetitionStats(); basis.endurance = actor.stats.endurance;
            decimal endurance = DisplayedScore(WebRules.WeightedCompetitionScore(basis, category, false, 0, .5));
            basis = EmptyCompetitionStats(); basis.social = actor.stats.social;
            decimal social = DisplayedScore(WebRules.WeightedCompetitionScore(basis, category, false, 0, .5));
            basis = EmptyCompetitionStats(); basis.luck = actor.stats.luck;
            decimal luck = DisplayedScore(WebRules.WeightedCompetitionScore(basis, category, false, 0, .5));
            double weighted = WebRules.WeightedCompetitionScore(actor.stats, category, false, 0, .5);
            decimal chance = DisplayedScore(weighted * (.75 + roll * .5) - weighted);
            decimal nominee = DisplayedScore(state.nominees.Contains(actor.id) ? actor.stats.competition * .5 : 0);
            decimal preparation = DisplayedScore(state.playerStudyBonus), phaseEvent = DisplayedScore(state.phaseEventCompBonus);
            decimal storyline = DisplayedScore(Storylines.CompetitionBonus(state)), manual = DisplayedScore(simulated ? 0 : performance * 2);
            decimal total = DisplayedScore(committedScore);
            decimal rounding = total - (physical + mental + endurance + social + luck + chance + nominee + preparation + phaseEvent + storyline + manual);
            return (simulated ? "Simulated: no performance bonus" : "Played: performance " + ScoreNumber(performance * 100) + "%")
                + ". Your weighted stats: physical " + physical.ToString(CultureInfo.InvariantCulture)
                + "; mental " + mental.ToString(CultureInfo.InvariantCulture) + "; endurance " + endurance.ToString(CultureInfo.InvariantCulture)
                + "; social " + social.ToString(CultureInfo.InvariantCulture) + "; luck " + luck.ToString(CultureInfo.InvariantCulture)
                + ". Chance " + SignedScore(chance) + " (sampled multiplier " + (.75 + roll * .5).ToString("0.######", CultureInfo.InvariantCulture) + ")"
                + "; nominee " + SignedScore(nominee) + "; preparation " + SignedScore(preparation) + "; event " + SignedScore(phaseEvent)
                + "; storyline " + SignedScore(storyline) + "; performance " + SignedScore(manual) + "; rounding " + SignedScore(rounding)
                + ". Sum = " + total.ToString("0.00", CultureInfo.InvariantCulture) + ". All displayed terms add to this committed score. "
                + "Total player bonus " + SignedScore(preparation + phaseEvent + storyline + manual)
                + ". Zero performance can still win; full marks do not guarantee first place.";
        }
    }
}