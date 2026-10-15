using Gamesim.Simulation;

namespace Gamesim.OldSaves
{
    /// <summary>A fresh season's rules at a build that has <c>ShippedRules</c>: exactly the director's.</summary>
    internal static class Rules
    {
        public static void Fresh(EpisodeState fresh) => ShippedRules.ApplyFresh(fresh);
    }
}
