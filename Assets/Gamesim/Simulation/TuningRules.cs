using System;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// B9's tuning rules (Assets/Plans/B9-BALANCE-PLAN.md): the season's balance numbers, keyed to the saved
    /// <see cref="EpisodeState.tuningRulesVersion"/> (schema 30). Every season saved before B9, every migrated save and
    /// every season a test builds holds 0 and plays exactly as it always did; <see cref="ShippedRules.ApplyFresh"/> writes
    /// <see cref="Shipped"/>, so every new season the director starts plays B9's numbers.
    /// <para>Each number here reads <see cref="At"/> with the value that introduced it, and at tuning 0 returns what the
    /// season played before B9. A later retune is tuning 2, keyed <c>At(s, 2)</c>: a season keeps the numbers it began on.</para>
    /// </summary>
    public static class TuningRules
    {
        /// <summary>No tuning: every season before B9.</summary>
        public const int Off = 0;

        /// <summary>
        /// B9, the balance bands (the owner, 2026-11-03: "Set the B9 balance bands"). Three levers, and the jury's tiebreak
        /// (the owner's call 3 of 2026-11-07, <see cref="JuryTieWinner"/>), part of tuning 1 before it ever shipped:
        /// <list type="bullet">
        /// <item>preparation is spent when it is used, and a study fills it to one, in every house and every week
        /// (<see cref="StudyLimit"/>, <see cref="AfterUse"/>);</item>
        /// <item>Game Sense weighs strategy more: 0.2 / 0.6 / 0.2 (<see cref="SenseWeights"/>);</item>
        /// <item>a war room's counter counts no nominee's say, and a tie carries it only if a member came round
        /// (<see cref="NomineesHaveNoSay"/>, <see cref="TieNeedsAMemberRound"/>);</item>
        /// <item>a tied jury is settled by the season's keyed coin, not by cast order (<see cref="KeyedJuryTiebreak"/>).</item>
        /// </list>
        /// <para>B9 first built a fourth: in a house that moved in eight or fewer, a studied player who sat a Head of
        /// Household out learned from watching it, and the final week's study filled preparation to two. The owner's call 1
        /// of 2026-11-07 held it until the jury tie was fixed. Measured again with the tiebreak (round 4's lab, 800 seasons),
        /// the studier at eight met both bands without it (weekly wins 62.4%, season wins 32.9%) and went over the season
        /// band with it (38.5%, against 25 to 35%), so tuning 1 dropped it before it ever shipped. Preparation is one point
        /// everywhere.</para>
        /// </summary>
        public const int Bands = 1;

        /// <summary>The highest value this build defines: validation's bound only, never what a season starts on.</summary>
        public const int Latest = Bands;

        /// <summary>
        /// The tuning a new season starts on (<see cref="ShippedRules.ApplyFresh"/>): a pinned literal, moved by hand.
        /// Tuning 1 froze the day it was first written, so a later retune moves this to its own value and never changes
        /// the numbers of a season already begun.
        /// </summary>
        public const int Shipped = Bands;

        /// <summary>Tuning 0's preparation: a study fills it to five, and it is never spent.</summary>
        public const int UnspentLimit = 5;

        /// <summary>Tuning 1: a study fills preparation to one, in every house and every week.</summary>
        public const int StudyCap = 1;

        /// <summary>Tuning 1's Game Sense weights: strategy is the game, the other two a fifth each.</summary>
        public const double CompetitionWeight = 0.2, StrategyWeight = 0.6, SocialWeight = 0.2;

        /// <summary>Whether a saved value is one this build defines (validation's range).</summary>
        public static bool IsDefined(int version) => version >= Off && version <= Latest;

        /// <summary>Whether a season plays a behaviour introduced at <paramref name="version"/>: a bound, never a gate on one number.</summary>
        public static bool At(EpisodeState s, int version) => s != null && s.tuningRulesVersion >= version;

        /// <summary>The final week: three remain, and the final Head of Household is the only competition left.</summary>
        public static bool FinalWeek(EpisodeState s) => s?.contestants != null && s.Active.Count() == 3;

        /// <summary>
        /// What a study fills preparation to: five at tuning 0, one at tuning 1. Under tuning 1 a study at or past it is
        /// refused before it spends anything.
        /// </summary>
        public static int StudyLimit(EpisodeState s) => !At(s, Bands) ? UnspentLimit : StudyCap;

        /// <summary>
        /// Tuning 1's refusal of a study at the limit, by what the preparation waits for: the next weekly Head of Household
        /// or veto, or in the final week the final Head of Household, whose three parts all count it.
        /// </summary>
        public static string FullRefusal(EpisodeState s) => FinalWeek(s)
            ? "Your preparation is full. All three parts of the final Head of Household count it."
            : "Your preparation is full. It is used in the next Head of Household or veto competition you enter.";

        /// <summary>The most preparation a season may hold (validation): the study's limit, five at tuning 0 and one at tuning 1, since nothing else adds to it.</summary>
        public static int PreparationBound(EpisodeState s) => StudyLimit(s);

        /// <summary>What the copy counts preparation out of: the study's limit, five at tuning 0 and one at tuning 1.</summary>
        public static int PreparationOutOf(EpisodeState s) => StudyLimit(s);

        /// <summary>What is left of preparation after a weekly Head of Household or veto the player entered: all of it at tuning 0, none at tuning 1.</summary>
        public static int AfterUse(EpisodeState s, int preparation) => At(s, Bands) ? 0 : preparation;

        /// <summary>Game Sense's weights: 0.3 / 0.4 / 0.3 at tuning 0 (<see cref="GameSense"/>'s constants), 0.2 / 0.6 / 0.2 at tuning 1.</summary>
        public static (double competitions, double strategy, double social) SenseWeights(EpisodeState s) => At(s, Bands)
            ? (CompetitionWeight, StrategyWeight, SocialWeight)
            : (GameSense.CompetitionWeight, GameSense.StrategyWeight, GameSense.SocialWeight);

        /// <summary>Tuning 1 (K5): a nominee cannot vote, so a nominee's standing say counts on neither side of a counter's tally.</summary>
        public static bool NomineesHaveNoSay(EpisodeState s) => At(s, Bands);

        /// <summary>Tuning 1 (K1): a tie in a pact the player founded carries the counter only if a member came round.</summary>
        public static bool TieNeedsAMemberRound(EpisodeState s) => At(s, Bands);

        // ---------------------------------------------------------------- the jury's tiebreak

        /// <summary>
        /// The key the jury tiebreak hashes first. Fixed for good: a new key would move the winner of every tied tuning-1
        /// season, so a different tiebreak is a later tuning value with a key of its own.
        /// </summary>
        public const string JuryTiebreakKey = "gamesim/tuning-1/jury-tiebreak";

        /// <summary>
        /// Whether a tied jury is settled by the season's keyed coin (<see cref="JuryTieWinner"/>): tuning 1 and later. At
        /// tuning 0 a tie goes to the second finalist in cast order, the source reveal's strict comparison, as it always did.
        /// </summary>
        public static bool KeyedJuryTiebreak(EpisodeState s) => At(s, Bands);

        /// <summary>Validation's refusal of a finished tuning-1 season whose tied jury crowned the finalist the coin did not.</summary>
        public const string JuryTieRefusal = "A tied jury's winner must be the finalist the season's tiebreak draw gives the win to.";

        /// <summary>
        /// Tuning 1's jury tiebreak: which of two finalists a tied jury crowns. The owner's call 3 of 2026-11-07: fix the
        /// player's systematic loss of jury ties for new rules, with a fair, seeded, replay-stable tiebreak that loading a
        /// save cannot reroll. Before it a tie went to the second finalist in cast order, and the player, usually first in
        /// the cast, lost every tie.
        /// <para>A keyed coin: <see cref="JuryTiebreakHash"/> of the season's seed and the pair, whose low bit picks the
        /// winner of the pair in ordinal id order (0 the first, 1 the second). The same pair in either order, or in any
        /// cast order, gets the same winner.</para>
        /// <para>It draws nothing from the season's random stream and saves nothing. The seed is set when the season is
        /// built and saved with it, so once the two finalists are known no load, replay or earlier choice changes who a tie
        /// goes to. Validation re-derives it: a tuning-1 season whose tied jury crowned the other finalist is refused.</para>
        /// </summary>
        public static string JuryTieWinner(uint seed, string firstId, string secondId)
        {
            if (firstId == null) throw new ArgumentNullException(nameof(firstId));
            if (secondId == null) throw new ArgumentNullException(nameof(secondId));
            bool inOrder = string.CompareOrdinal(firstId, secondId) <= 0;
            string low = inOrder ? firstId : secondId, high = inOrder ? secondId : firstId;
            return (JuryTiebreakHash(seed, firstId, secondId) & 1UL) == 0 ? low : high;
        }

        /// <summary>
        /// The coin's 64 bits: FNV-1a 64 over <see cref="JuryTiebreakKey"/>, the seed's four bytes (least significant
        /// first) and the two ids in ordinal order, each string as its length in UTF-16 code units (four bytes) and then
        /// its code units (two bytes each, low byte first); then MurmurHash3's 64-bit finalizer. The finalizer is there
        /// because FNV-1a multiplies by an odd prime, so its own low bit is only the parity of its input bytes' low bits.
        /// Plain integer arithmetic, the same on every runtime: never string.GetHashCode.
        /// </summary>
        public static ulong JuryTiebreakHash(uint seed, string firstId, string secondId)
        {
            if (firstId == null) throw new ArgumentNullException(nameof(firstId));
            if (secondId == null) throw new ArgumentNullException(nameof(secondId));
            bool inOrder = string.CompareOrdinal(firstId, secondId) <= 0;
            ulong hash = FnvOffset;
            hash = MixText(hash, JuryTiebreakKey);
            hash = MixWord(hash, seed);
            hash = MixText(hash, inOrder ? firstId : secondId);
            hash = MixText(hash, inOrder ? secondId : firstId);
            unchecked
            {
                hash ^= hash >> 33;
                hash *= 0xff51afd7ed558ccdUL;
                hash ^= hash >> 33;
                hash *= 0xc4ceb9fe1a85ec53UL;
                hash ^= hash >> 33;
            }
            return hash;
        }

        private const ulong FnvOffset = 14695981039346656037UL, FnvPrime = 1099511628211UL;

        private static ulong MixByte(ulong hash, int value)
        {
            unchecked { return (hash ^ (ulong)(value & 0xFF)) * FnvPrime; }
        }

        private static ulong MixWord(ulong hash, uint value)
        {
            for (int shift = 0; shift < 32; shift += 8) hash = MixByte(hash, (int)((value >> shift) & 0xFFu));
            return hash;
        }

        private static ulong MixText(ulong hash, string text)
        {
            hash = MixWord(hash, (uint)text.Length);
            foreach (char unit in text)
            {
                hash = MixByte(hash, unit);
                hash = MixByte(hash, unit >> 8);
            }
            return hash;
        }
    }
}
