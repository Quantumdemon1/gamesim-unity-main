using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// Leaks and double-dealing (WAVE-D-NPC-PACTS-PLAN §2): the engine's half. The rules' numbers and
    /// words are <see cref="AllianceLeaks"/>'; here is where they act.
    /// </summary>
    public sealed partial class EpisodeEngine
    {
        /// <summary>Switches the leak rules on from a week, no later than the week after the season's own (schema 28's boundary).</summary>
        public static void EnableAllianceLeaks(EpisodeState s, int fromWeek = 1)
        {
            if (s == null) throw new ArgumentNullException(nameof(s));
            s.allianceLeakRulesStartWeek = Math.Max(1, Math.Min(fromWeek, s.week + 1));
        }
    }
}
