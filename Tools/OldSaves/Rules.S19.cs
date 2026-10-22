using Gamesim.Simulation;

namespace Gamesim.OldSaves
{
    /// <summary>A fresh season's rules at schema 19's build (b465d93c), transcribed line for line from its EpisodeDirector.StartSeason.</summary>
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
        }
    }
}
