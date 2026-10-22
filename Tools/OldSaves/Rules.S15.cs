using Gamesim.Simulation;

namespace Gamesim.OldSaves
{
    /// <summary>A fresh season's rules at schema 15's build (0b474234, the game-flow line), from its EpisodeDirector.StartSeason.</summary>
    internal static class Rules
    {
        public static void Fresh(EpisodeState fresh)
        {
            fresh.competitionRulesVersion = CompetitionRules.Current;
            fresh.haveNotRulesStartWeek = 1;
            fresh.strategyRulesStartWeek = 1;
        }
    }
}
