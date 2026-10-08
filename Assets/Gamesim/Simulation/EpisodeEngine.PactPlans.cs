using System;

namespace Gamesim.Simulation
{
    /// <summary>
    /// The war room (WAVE-D-NPC-PACTS-PLAN §3): the engine's half. The rules' numbers, words and
    /// decisions are <see cref="PactPlans"/>'; here is where they act.
    /// </summary>
    public sealed partial class EpisodeEngine
    {
        /// <summary>Switches the war rooms on from a week, no later than the week after the season's own (schema 28's boundary).</summary>
        public static void EnablePactPlans(EpisodeState s, int fromWeek = 1)
        {
            if (s == null) throw new ArgumentNullException(nameof(s));
            s.pactPlanRulesStartWeek = Math.Max(1, Math.Min(fromWeek, s.week + 1));
        }

        /// <summary>
        /// Whether the season plays the war rooms this week (X8): their start week reached, and the
        /// commitment rules and the levers on - a war room's plan is the week's call, which only the levers
        /// make, and who has a say is who answers the player as an ally, which only the commitment rules say.
        /// </summary>
        public static bool PactPlanRulesOn(EpisodeState s) =>
            s != null && s.pactPlanRulesStartWeek >= 1 && s.week >= s.pactPlanRulesStartWeek
            && CommitmentRulesOn(s) && LeverRulesOn(s);
    }
}
