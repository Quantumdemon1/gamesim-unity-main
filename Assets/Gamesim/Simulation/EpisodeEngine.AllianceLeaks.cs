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

        // ---------------------------------------------------------------- one pair, one pact

        /// <summary>
        /// The pact a story's alliance spread is about under the leak rules: the one pact of the two it
        /// names (<see cref="Knowledge.PactOfPair"/>), asked for its listener when it names one. Null for
        /// any other effect, or where no pact holds the two.
        /// </summary>
        private static AllianceState SpreadPact(EpisodeState s, StoryEffectState e)
        {
            if (e == null || e.kind != StoryEffects.Spread || e.type != FactKinds.Alliance) return null;
            if (s.Find(e.fromId) == null || s.Find(e.toId) == null) return null;
            return Knowledge.PactOfPair(s, e.fromId, e.toId, e.thirdId);
        }

        /// <summary>
        /// A story's alliance spread under the leak rules (WAVE-D-NPC-PACTS-PLAN §2.3, the C8 defect): it
        /// grants and widens exactly the one pact the pair names, where it once reached every pact holding
        /// both. A named listener learns of it and it is out as a whisper; with nobody named it goes as
        /// wide as the story says. The reach rules' skip stands: a pact with no fact is known to everyone
        /// already, so one more person hearing of it makes no secret of it.
        /// </summary>
        private static void SpreadOnePact(EpisodeState s, StoryEffectState e)
        {
            var alliance = SpreadPact(s, e);
            if (alliance == null) return;
            if (Knowledge.Of(s, FactKinds.Alliance, alliance.id) == null && e.thirdId != null && StoryAt(s, StoryRules.Reach)) return;
            if (Knowledge.Of(s, FactKinds.Alliance, alliance.id) == null) Knowledge.AllianceFormed(s, alliance);
            var fact = Knowledge.Of(s, FactKinds.Alliance, alliance.id);
            if (fact == null) return;
            if (e.thirdId != null)
            {
                Knowledge.AddKnower(s, fact, e.thirdId);
                Knowledge.MakeKnown(s, fact, FactVisibility.Whispered);
            }
            else Knowledge.MakeKnown(s, fact, FactVisibility.IsKnown(e.text) ? e.text : FactVisibility.Public);
        }

        /// <summary>
        /// Before each of a beat's or a play's effects is applied, under the leak rules: the pact an
        /// alliance spread among them is about, kept so its receipt names the pact it granted - asked
        /// after the grant, a listener who now knows of it would be pointed at another.
        /// </summary>
        private static void RememberSpreadPact(EpisodeState s, StoryEffectState e, Dictionary<StoryEffectState, AllianceState> spread)
        {
            if (spread == null) return;
            var alliance = SpreadPact(s, e);
            if (alliance != null) spread[e] = alliance;
        }
    }
}
