using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// The personality traits a houseguest can carry, and what each one does to their stats.
    ///
    /// <para>Ported from the web game's <c>TRAIT_STAT_BOOSTS</c> and <c>TRAIT_BOOST_VALUES</c>.
    /// Each trait raises two stats: <b>+2 on its primary and +1 on its secondary</b>.</para>
    ///
    /// <para>The native cast already carried trait names, but nothing in this project ever read
    /// them mechanically — they were flavour on a contestant record and six of the web's seventeen
    /// were in use. This is the table that makes them mean something, and the one player creation
    /// needs before it can exist.</para>
    ///
    /// <para><b>Nothing applies these to the existing cast.</b> The shipped six have authored stats
    /// and rewriting them from their traits would change every seeded competition result in the
    /// project's tests. This is data plus a pure function; the caller decides when it applies, which
    /// in the web game is character creation and nowhere else.</para>
    /// </summary>
    public static class WebTraits
    {
        public const int PrimaryBoost = 2;
        public const int SecondaryBoost = 1;

        /// <summary>Stats are held between these, matching the web form's clamps.</summary>
        public const double Minimum = 1, Maximum = 10;

        /// <summary>A trait's two stats: the one it raises by two, and the one it raises by one.</summary>
        public readonly struct Boost
        {
            public readonly string Primary, Secondary;
            public Boost(string primary, string secondary) { Primary = primary; Secondary = secondary; }
        }

        /// <summary>
        /// The seventeen traits, exactly as the web game weights them.
        ///
        /// <para>Worth noting that <c>competition</c> is the one stat no trait touches — it appears
        /// in neither column. That is the source's shape, not an omission here.</para>
        /// </summary>
        public static readonly IReadOnlyDictionary<string, Boost> Boosts = new Dictionary<string, Boost>(
            StringComparer.OrdinalIgnoreCase)
        {
            { "Competitive",     new Boost("physical",  "endurance") },
            { "Strategic",       new Boost("mental",    "strategic") },
            { "Loyal",           new Boost("loyalty",   "social")    },
            { "Emotional",       new Boost("social",    "loyalty")   },
            { "Funny",           new Boost("social",    "mental")    },
            { "Charming",        new Boost("social",    "strategic") },
            { "Manipulative",    new Boost("strategic", "mental")    },
            { "Analytical",      new Boost("mental",    "strategic") },
            { "Impulsive",       new Boost("physical",  "endurance") },
            { "Deceptive",       new Boost("strategic", "social")    },
            { "Social",          new Boost("social",    "luck")      },
            { "Introverted",     new Boost("mental",    "loyalty")   },
            { "Stubborn",        new Boost("endurance", "physical")  },
            { "Flexible",        new Boost("strategic", "mental")    },
            { "Intuitive",       new Boost("mental",    "luck")      },
            { "Sneaky",          new Boost("strategic", "mental")    },
            { "Confrontational", new Boost("physical",  "endurance") },
        };

        /// <summary>How many traits one houseguest may carry. The web form enforces two.</summary>
        public const int MaximumTraits = 2;

        public static bool Known(string trait) => trait != null && Boosts.ContainsKey(trait);

        /// <summary>
        /// A fresh stat block for a houseguest with these traits: the web's lower-middle base roll,
        /// then each trait's boosts.
        ///
        /// <para>The base is the deterministic centre of <c>creation.ts</c>'s ranges rather than a
        /// roll — primary 6, secondary 5, everything else 4, against its 5–8 / 4–7 / 3–6 — because a
        /// season has to replay identically and a random base would make the same seed produce a
        /// different house.</para>
        ///
        /// <para>This is the arithmetic <see cref="ContentCatalog"/> has always used for the shipped
        /// cast, lifted here so <see cref="CastTemplates"/> cannot quietly disagree with it. Five
        /// houseguests appear in both, and a test pins that they come out the same either way.</para>
        /// </summary>
        public static ContestantStats CreateStats(IEnumerable<string> traits)
        {
            var known = (traits ?? Enumerable.Empty<string>()).Where(Known).ToList();
            var primary = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var secondary = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var trait in known)
            {
                primary.Add(Boosts[trait].Primary);
                secondary.Add(Boosts[trait].Secondary);
            }

            var stats = new ContestantStats();
            foreach (var key in StatNames)
                Set(stats, key, primary.Contains(key) ? 6 : secondary.Contains(key) ? 5 : 4);
            foreach (var trait in known) Apply(stats, trait, true);
            return stats;
        }

        /// <summary>The eight stats, in the order the web form lists them.</summary>
        public static readonly string[] StatNames =
            { "physical", "mental", "endurance", "social", "luck", "competition", "strategic", "loyalty" };

        /// <summary>
        /// Adds or removes a trait's boosts on a stat block, in place.
        ///
        /// <para><b>This is not reversible at the clamps, and that is faithful rather than a bug
        /// here.</b> The web applies the same <c>min(10)</c> on the way up and <c>max(1)</c> on the
        /// way down, so a stat at 9 that gains +2 lands on 10 and gives back 2 when the trait is
        /// removed, finishing at 8. Copying the source's arithmetic keeps a character built in the
        /// web game and one built here identical; "fixing" it would make them disagree.</para>
        /// </summary>
        public static void Apply(ContestantStats stats, string trait, bool adding)
        {
            if (stats == null || !Known(trait)) return;
            var boost = Boosts[trait];
            int sign = adding ? 1 : -1;
            Set(stats, boost.Primary, Get(stats, boost.Primary) + sign * PrimaryBoost);
            Set(stats, boost.Secondary, Get(stats, boost.Secondary) + sign * SecondaryBoost);
        }

        public static double Get(ContestantStats stats, string name)
        {
            if (stats == null || name == null) return 0;
            switch (name.ToLowerInvariant())
            {
                case "physical":    return stats.physical;
                case "mental":      return stats.mental;
                case "endurance":   return stats.endurance;
                case "social":      return stats.social;
                case "luck":        return stats.luck;
                case "competition": return stats.competition;
                case "strategic":   return stats.strategic;
                case "loyalty":     return stats.loyalty;
                default:            return 0;
            }
        }

        public static void Set(ContestantStats stats, string name, double value)
        {
            if (stats == null || name == null) return;
            double held = value < Minimum ? Minimum : value > Maximum ? Maximum : value;
            switch (name.ToLowerInvariant())
            {
                case "physical":    stats.physical = held; break;
                case "mental":      stats.mental = held; break;
                case "endurance":   stats.endurance = held; break;
                case "social":      stats.social = held; break;
                case "luck":        stats.luck = held; break;
                case "competition": stats.competition = held; break;
                case "strategic":   stats.strategic = held; break;
                case "loyalty":     stats.loyalty = held; break;
            }
        }
    }
}
