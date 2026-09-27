using System;

namespace Gamesim.Simulation
{
    /// <summary>
    /// The story system's randomness: a keyed hash stream that never touches
    /// <see cref="EpisodeState.randomState"/>.
    ///
    /// <para>Every roll on the season's main stream shifts every competition, veto and vote after
    /// it (<c>removing-a-command-re-rolls-the-season</c>). So no story draw comes off that stream.
    /// Each draw hashes a key that says exactly which draw it is - the week, the anchor, the cycle,
    /// the beat, the option, the purpose - together with the season's seed, the pattern
    /// <see cref="WebLoyaltyOaths"/> already uses for its eviction witnesses. Adding or removing a
    /// template reshuffles nothing, and a reload cannot re-roll a backfire the player has seen.</para>
    /// </summary>
    public static class StoryRandom
    {
        private const string Prefix = "gamesim:story:v1:";

        /// <summary>A draw in [0, 1) for one key in one season. The same key always draws the same number.</summary>
        public static double Unit(EpisodeState state, string key)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (string.IsNullOrEmpty(key)) throw new ArgumentException("A story draw needs a key.", nameof(key));
            return new SeededRandom(SeededRandom.HashSeed(Prefix + state.seed + ":" + key)).NextDouble();
        }

        /// <summary>
        /// A stream of draws under one key, for a caller that needs several in a row: the engine's
        /// relationship reducer draws once for a reciprocal score and once more for a typed event.
        /// </summary>
        public static Func<double> Stream(EpisodeState state, string key)
        {
            int n = 0;
            return () => Unit(state, key + "#" + n++);
        }

        /// <summary>An index in [0, count) from a key; zero when there is nothing to choose between.</summary>
        public static int Index(EpisodeState state, string key, int count)
        {
            if (count <= 1) return 0;
            int value = (int)(Unit(state, key) * count);
            return value >= count ? count - 1 : value;
        }

        /// <summary>Whether a keyed chance lands.</summary>
        public static bool Chance(EpisodeState state, string key, double chance) =>
            chance > 0 && Unit(state, key) < chance;
    }
}
