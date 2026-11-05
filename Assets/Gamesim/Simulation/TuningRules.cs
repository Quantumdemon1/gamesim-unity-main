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
        /// B9, the balance bands (the owner, 2026-11-03: "Set the B9 balance bands"). Four levers:
        /// <list type="bullet">
        /// <item>preparation is spent when it is used, and a study fills it to one (<see cref="StudyLimit"/>, <see cref="AfterUse"/>);</item>
        /// <item>in a house that moved in eight or fewer, a player who has studied and sits a Head of Household out learns
        /// from watching it (<see cref="AfterWatchedHoH"/>), and the final week's study fills it to two;</item>
        /// <item>Game Sense weighs strategy more: 0.2 / 0.6 / 0.2 (<see cref="SenseWeights"/>);</item>
        /// <item>a war room's counter counts no nominee's say, and a tie carries it only if a member came round
        /// (<see cref="NomineesHaveNoSay"/>, <see cref="TieNeedsAMemberRound"/>).</item>
        /// </list>
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

        /// <summary>Tuning 1: a study fills preparation to one.</summary>
        public const int StudyCap = 1;

        /// <summary>Tuning 1, in a house of <see cref="SmallHouse"/> or fewer: the final week's study fills it to two, and so can watching a Head of Household.</summary>
        public const int SmallHouseCap = 2;

        /// <summary>Tuning 1's small house: a cast that moved in eight or fewer.</summary>
        public const int SmallHouse = 8;

        /// <summary>Tuning 1's Game Sense weights: strategy is the game, the other two a fifth each.</summary>
        public const double CompetitionWeight = 0.2, StrategyWeight = 0.6, SocialWeight = 0.2;

        /// <summary>Whether a saved value is one this build defines (validation's range).</summary>
        public static bool IsDefined(int version) => version >= Off && version <= Latest;

        /// <summary>Whether a season plays a behaviour introduced at <paramref name="version"/>: a bound, never a gate on one number.</summary>
        public static bool At(EpisodeState s, int version) => s != null && s.tuningRulesVersion >= version;

        /// <summary>Tuning 1's small house: the cast that moved in, eight or fewer.</summary>
        public static bool InSmallHouse(EpisodeState s) => s?.contestants == null || s.contestants.Count <= SmallHouse;

        /// <summary>The final week: three remain, and the final Head of Household is the only competition left.</summary>
        public static bool FinalWeek(EpisodeState s) => s?.contestants != null && s.Active.Count() == 3;

        /// <summary>
        /// What a study fills preparation to: five at tuning 0; at tuning 1, one, or two in the final week of a small house.
        /// Under tuning 1 a study at or past it is refused before it spends anything.
        /// </summary>
        public static int StudyLimit(EpisodeState s) =>
            !At(s, Bands) ? UnspentLimit : InSmallHouse(s) && FinalWeek(s) ? SmallHouseCap : StudyCap;

        /// <summary>
        /// Tuning 1's refusal of a study at the limit, by what the preparation waits for: the next weekly Head of Household
        /// or veto, or in the final week the final Head of Household, whose three parts all count it.
        /// </summary>
        public static string FullRefusal(EpisodeState s) => FinalWeek(s)
            ? "Your preparation is full. All three parts of the final Head of Household count it."
            : "Your preparation is full. It is used in the next Head of Household or veto competition you enter.";

        /// <summary>The most preparation a season may hold (validation): five at tuning 0; at tuning 1, two in a small house, else one.</summary>
        public static int PreparationBound(EpisodeState s) =>
            !At(s, Bands) ? UnspentLimit : InSmallHouse(s) ? SmallHouseCap : StudyCap;

        /// <summary>What the copy counts preparation out of: five at tuning 0; at tuning 1 the study's limit, or the preparation itself when watching took it past.</summary>
        public static int PreparationOutOf(EpisodeState s) =>
            !At(s, Bands) ? UnspentLimit : Math.Max(StudyLimit(s), s.playerStudyBonus);

        /// <summary>What is left of preparation after a weekly Head of Household or veto the player entered: all of it at tuning 0, none at tuning 1.</summary>
        public static int AfterUse(EpisodeState s, int preparation) => At(s, Bands) ? 0 : preparation;

        /// <summary>
        /// What is left after a weekly Head of Household the player sat out: at tuning 1 in a small house, a player who has
        /// studied watched it and learned, one more up to <see cref="SmallHouseCap"/>; otherwise unchanged. A player who
        /// never studies never moves. The engine asks only while the player is in the house: a juror watches nothing.
        /// </summary>
        public static int AfterWatchedHoH(EpisodeState s, int preparation) =>
            At(s, Bands) && InSmallHouse(s) && preparation > 0 ? Math.Min(SmallHouseCap, preparation + 1) : preparation;

        /// <summary>Game Sense's weights: 0.3 / 0.4 / 0.3 at tuning 0 (<see cref="GameSense"/>'s constants), 0.2 / 0.6 / 0.2 at tuning 1.</summary>
        public static (double competitions, double strategy, double social) SenseWeights(EpisodeState s) => At(s, Bands)
            ? (CompetitionWeight, StrategyWeight, SocialWeight)
            : (GameSense.CompetitionWeight, GameSense.StrategyWeight, GameSense.SocialWeight);

        /// <summary>Tuning 1 (K5): a nominee cannot vote, so a nominee's standing say counts on neither side of a counter's tally.</summary>
        public static bool NomineesHaveNoSay(EpisodeState s) => At(s, Bands);

        /// <summary>Tuning 1 (K1): a tie in a pact the player founded carries the counter only if a member came round.</summary>
        public static bool TieNeedsAMemberRound(EpisodeState s) => At(s, Bands);
    }
}
