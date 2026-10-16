using Gamesim.Simulation;

namespace Gamesim.OldSaves
{
    /// <summary>
    /// A fresh season's rules at the story-arcs line's schema 14 (52cb23df), from its EpisodeDirector.StartSeason:
    /// competition rules 3 and the story system from week one. That line's "14" is not the shipped schema 14 (the
    /// game-flow line's Have-Nots); the two met at 76654f3b as 14, 15 and 16.
    /// </summary>
    internal static class Rules
    {
        public static void Fresh(EpisodeState fresh)
        {
            fresh.competitionRulesVersion = 3;
            EpisodeEngine.EnableStory(fresh);
        }
    }
}
