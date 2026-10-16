using System;
using Gamesim.Simulation;

namespace Gamesim.OldSaves
{
    /// <summary>A build before <c>ShippedRules</c>: recording only. Generating a director season there needs that build's own setup, written out.</summary>
    internal static class Rules
    {
        public static void Fresh(EpisodeState fresh) => throw new NotSupportedException("This build has no ShippedRules; pass a rules source written from its director's StartSeason.");
    }
}
