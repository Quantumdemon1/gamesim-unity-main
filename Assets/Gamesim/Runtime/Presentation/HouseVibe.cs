using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using UnityEngine;

namespace Gamesim.Presentation
{
    /// <summary>
    /// The four-bar mood readout mockup-04 draws beside the cast: how much fun, trust, drama and
    /// harmony this week has had in it.
    ///
    /// <para>Four sums over the week's events, and nothing more. It is a <em>readout</em>: the
    /// simulation never reads it back, so a wrong bar misinforms and cannot misbehave. That is the
    /// whole reason it can be derived in the presentation layer rather than carried as saved state,
    /// and it is why adding it needed no schema version.</para>
    ///
    /// <para>Only events the player is allowed to have seen are counted, by the same audience rule
    /// the recent-events card and the notebook use. A bar that quietly summed private NPC scheming
    /// would be handing the player information their character does not have, which is the one way
    /// a readout can cheat.</para>
    ///
    /// <para>Harmony is the only derived bar: it is what is left of the week's goodwill after its
    /// drama, floored at nothing. A house can be busy and friendly at once, but it cannot be
    /// harmonious while it is arguing, and the mockup draws exactly that relationship - Harmony low
    /// in the frame where Drama is long.</para>
    /// </summary>
    public static class HouseVibe
    {
        /// <summary>One week's mood, as four counts and the bar fractions that draw them.</summary>
        public readonly struct Reading
        {
            public readonly int Fun, Trust, Drama, Harmony;

            public Reading(int fun, int trust, int drama, int harmony)
            { Fun = fun; Trust = trust; Drama = drama; Harmony = harmony; }

            /// <summary>The largest of the four, which every bar is drawn against. Never zero.</summary>
            public int Peak => Mathf.Max(1, Mathf.Max(Mathf.Max(Fun, Trust), Mathf.Max(Drama, Harmony)));

            /// <summary>How full a bar is drawn, 0 to 1.</summary>
            public float Fraction(int count) => Mathf.Clamp01(count / (float)Peak);

            /// <summary>The four in the order the mockup stacks them.</summary>
            public IEnumerable<(string Word, int Count, string Icon, Color Tint)> Rows()
            {
                yield return ("Fun", Fun, "mood-happy", UiTheme.Positive);
                yield return ("Trust", Trust, "handshake", UiTheme.Accent);
                yield return ("Drama", Drama, "mood-tense", UiTheme.Danger);
                yield return ("Harmony", Harmony, "heart", UiTheme.Gold);
            }
        }

        // Disjoint on purpose: an event counts once, so the bars can be read against each other.
        private static readonly HashSet<string> FunKinds = new HashSet<string>
        { "conversation", "house-event", "house-event-outcome", "house-ambient", "house-meeting", "competition" };

        private static readonly HashSet<string> TrustKinds = new HashSet<string>
        { "alliance", "deal", "deal-outcome", "promise", "promise-outcome", "relationship-milestone" };

        private static readonly HashSet<string> DramaKinds = new HashSet<string>
        { "nomination", "eviction", "final-eviction", "backdoor", "lie", "rumour", "rumour-backfire",
          "scheme", "vent", "campaign-close", "veto" };

        /// <summary>This week's reading, or an empty one for a state with nothing in it yet.</summary>
        public static Reading Of(EpisodeState state)
        {
            if (state?.events == null) return new Reading(0, 0, 0, 0);

            int fun = 0, trust = 0, drama = 0;
            foreach (var entry in state.events)
            {
                if (entry == null || entry.week != state.week) continue;
                if (entry.audienceIds != null && entry.audienceIds.Count > 0
                    && !entry.audienceIds.Contains(state.playerId)) continue;
                if (FunKinds.Contains(entry.kind)) fun++;
                else if (TrustKinds.Contains(entry.kind)) trust++;
                else if (DramaKinds.Contains(entry.kind)) drama++;
            }
            return new Reading(fun, trust, drama, Mathf.Max(0, fun + trust - drama));
        }

        /// <summary>
        /// How tense the house is in one word, for the conflict line the mockup puts under the bars.
        /// Thresholds rather than a curve: the reader wants "is this a bad week", not a number.
        /// </summary>
        public static string Tension(Reading reading)
        {
            if (reading.Drama == 0) return "Settled";
            if (reading.Drama >= reading.Fun + reading.Trust) return "High tension";
            if (reading.Drama * 2 >= reading.Fun + reading.Trust) return "Simmering";
            return "Mostly calm";
        }
    }
}
