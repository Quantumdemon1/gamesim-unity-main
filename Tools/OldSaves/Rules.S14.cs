using Gamesim.Simulation;

namespace Gamesim.OldSaves
{
    /// <summary>A fresh season's rules at the game-flow line's schema 14 (7fc031fb), from its EpisodeDirector.StartSeason.</summary>
    internal static class Rules
    {
        public static void Fresh(EpisodeState fresh)
        {
            fresh.competitionRulesVersion = CompetitionRules.Current;
            fresh.haveNotRulesStartWeek = 1;
        }
    }
}
