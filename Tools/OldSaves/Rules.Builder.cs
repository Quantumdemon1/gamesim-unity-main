using Gamesim.Simulation;

namespace Gamesim.OldSaves
{
    /// <summary>
    /// A fresh season's rules at schemas 9 to 12's builds (b926b97e, eb0c9dd8, 586145ec, 8fcd5bf8): their
    /// EpisodeDirector.StartSeason set none of its own, so the season builder's defaults stand.
    /// </summary>
    internal static class Rules
    {
        public static void Fresh(EpisodeState fresh) { }
    }
}
