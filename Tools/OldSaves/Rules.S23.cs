using Gamesim.Simulation;

namespace Gamesim.OldSaves
{
    /// <summary>
    /// A fresh season's rules at schemas 23 to 25's builds (a17429a3, 296266ce, 29dad8fe), transcribed line for line
    /// from their EpisodeDirector.StartSeason: schema 22's, with the economy selected after the week rules.
    /// </summary>
    internal static class Rules
    {
        public static void Fresh(EpisodeState fresh)
        {
            fresh.competitionRulesVersion = CompetitionRules.Current;
            fresh.haveNotRulesStartWeek = 1;
            fresh.strategyRulesStartWeek = 1;
            EpisodeEngine.EnableStory(fresh);
            EpisodeEngine.EnableRead(fresh);
            EpisodeEngine.EnableLevers(fresh);
            EpisodeEngine.EnableWeek(fresh);
            EpisodeEngine.EnableEconomy(fresh);
            EpisodeEngine.EnableAgency(fresh);
            EpisodeEngine.EnableFinale(fresh);
            EpisodeEngine.EnableCommitments(fresh);
        }
    }
}
