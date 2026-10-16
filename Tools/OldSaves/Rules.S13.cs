using Gamesim.Simulation;

namespace Gamesim.OldSaves
{
    /// <summary>A fresh season's rules at schema 13's build (74091da0), from its EpisodeDirector.StartSeason: competition rules 3.</summary>
    internal static class Rules
    {
        public static void Fresh(EpisodeState fresh) => fresh.competitionRulesVersion = 3;
    }
}
