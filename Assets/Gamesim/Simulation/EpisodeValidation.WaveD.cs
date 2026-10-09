using System;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// Schema 28 (WAVE-D-NPC-PACTS-PLAN §0.3): Wave D's three start weeks, and the lines and the
    /// receipt each design alone may write. D2's cadence is checked with the NPC world
    /// (EpisodeNpcSocialValidation) and D3's plans with the ledger (EpisodeLedgerValidation).
    /// While a design's start week is 0 it has left nothing behind; once set, nothing it wrote
    /// predates it.
    /// </summary>
    public static partial class EpisodeValidation
    {
        private static bool TryValidateWaveD(EpisodeState s, out string error)
        {
            error = null;
            if (!WaveDStartWeek(s, s.allianceLeakRulesStartWeek) || !WaveDStartWeek(s, s.pactPlanRulesStartWeek)
                || !WaveDStartWeek(s, s.allWeekRulesStartWeek))
                return Fail(out error, "Wave D activation weeks must be within the saved season boundary.");
            if (!WaveDLinesKept(s, s.allWeekRulesStartWeek, WaveDEventKinds.Sighting, WaveDEventKinds.Overheard))
                return Fail(out error, "A season without the all-week rules has none of their lines.");
            if (!WaveDLinesKept(s, s.pactPlanRulesStartWeek, WaveDEventKinds.PactPlan))
                return Fail(out error, "A season without the war rooms has none of their lines.");
            if (!WaveDLinesKept(s, s.allianceLeakRulesStartWeek, WaveDEventKinds.DoubleDealing))
                return Fail(out error, "A season without the leak rules has none of their lines.");
            // D4's receipt, held either way between any two houseguests.
            if (s.relationships.Any(r => r.events.Any(e => e.type == StoryReceipts.DoubleDealt
                    && (s.allianceLeakRulesStartWeek == 0 || e.week < s.allianceLeakRulesStartWeek))))
                return Fail(out error, "A season without the leak rules has none of their receipts.");
            return true;
        }

        /// <summary>0 for never, or a week the season has reached or will reach next.</summary>
        private static bool WaveDStartWeek(EpisodeState s, int week) => week >= 0 && week <= 101 && week <= s.week + 1;

        /// <summary>No line of these kinds while the start week is 0, and none dated before it.</summary>
        private static bool WaveDLinesKept(EpisodeState s, int startWeek, params string[] kinds) =>
            !s.events.Any(e => Array.IndexOf(kinds, e.kind) >= 0 && (startWeek == 0 || e.week < startWeek));
    }
}
