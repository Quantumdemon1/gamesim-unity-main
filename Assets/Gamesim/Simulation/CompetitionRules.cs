using System;
using System.Globalization;

namespace Gamesim.Simulation
{
    /// <summary>
    /// Competition rules 4: five kinds of competition, a minigame that counts for more, and a throw
    /// that really throws.
    ///
    /// <para><b>Five kinds.</b> The reference build's player plays five competition types - physical,
    /// mental, endurance, luck and social - each with a game of its own. This port had three. A season
    /// now deals the five in an order drawn from its own seed, so no two seasons open the same way:
    /// the Head of Household competition takes the next kind each week, and the veto the kind two
    /// further on, so a week's two competitions are never the same kind and each kind comes round
    /// once as each in every five weeks.</para>
    ///
    /// <para><b>Luck and social scores.</b> Luck is the reference's Crapshoot - a tenth mental, nine
    /// tenths luck, and a second roll worth up to three - with the luck statistic squeezed toward the
    /// middle, as the reference's own dice game squeezes it (5 + (luck - 5) x 0.2): a lucky houseguest
    /// is favoured, not crowned. The reference has no social weights, so these are this port's:
    /// three fifths social, a fifth mental, a fifth luck.</para>
    ///
    /// <para><b>The minigame counts for more.</b> Full marks were worth two points against a weighted
    /// score of about five; they are worth three. Simulated in a field of six against the cast's own
    /// statistics and averaged over the five kinds, a balanced player's chance of winning runs from
    /// 18% at no performance to 56% at half marks and 89% at full marks. Over the three kinds at the
    /// old weight it ran from 22% to 71%.</para>
    ///
    /// <para><b>A throw.</b> Throwing gives up every bonus and counts only part of what the player's
    /// statistics and roll would have scored: the share that leaves a throw winning about one time in
    /// ten at every size of field (<see cref="ThrowShare"/>). It never touches anyone else's score, so
    /// a throw can still win - when every other houseguest rolls lower still.</para>
    ///
    /// <para>None of it reads or spends the season's generator beyond the rolls each competition
    /// already draws (luck draws a second per competitor). Seasons under rules 3 and before are
    /// frozen: they keep their three categories, their weight and their throw.</para>
    /// </summary>
    public static class CompetitionRules
    {
        /// <summary>The rules this version introduced, and the rules a new season is created under.</summary>
        public const int Widened = 4;
        public const int Current = Widened;

        public const string Skill = "Skill", Mental = "Mental", Endurance = "Endurance", Luck = "Luck", Social = "Social";

        /// <summary>The five kinds, in the order the season's shuffle starts from.</summary>
        public static readonly string[] Categories = { Skill, Mental, Endurance, Luck, Social };

        /// <summary>What full marks at a minigame are worth, in points: three from rules 4, two before.</summary>
        public static double PerformanceWeight(int rulesVersion) => rulesVersion >= Widened ? 3 : 2;

        /// <summary>The luck a luck competition reads: the reference dice game's squeeze toward five.</summary>
        public static double SqueezedLuck(double luck) => 5 + (luck - 5) * .2;

        /// <summary>Whether a competition of this kind draws a second roll for every competitor.</summary>
        public static bool RollsTwice(string category) => category == Luck;

        /// <summary>
        /// The season's order of the five kinds: a shuffle seeded from the season's seed alone, so a
        /// reload deals the same season and nothing is drawn from the season's generator.
        /// </summary>
        public static string[] CategoryOrder(uint seed)
        {
            var order = (string[])Categories.Clone();
            var shuffle = new SeededRandom(SeededRandom.HashSeed(seed.ToString(CultureInfo.InvariantCulture) + "/categories-v4"));
            for (int i = order.Length - 1; i > 0; i--)
            {
                int j = (int)(shuffle.NextDouble() * (i + 1));
                if (j > i) j = i;
                (order[i], order[j]) = (order[j], order[i]);
            }
            return order;
        }

        /// <summary>A weekly competition's kind: the next in the season's order for the HoH, two on for the veto.</summary>
        public static string Category(EpisodePhase phase, int week, uint seed)
        {
            var order = CategoryOrder(seed);
            int slot = phase == EpisodePhase.Veto ? week + 1 : week - 1;
            return order[((slot % order.Length) + order.Length) % order.Length];
        }

        /// <summary>
        /// One competitor's score. Skill, mental and endurance are the reference's weighted runner,
        /// unchanged; luck is its Crapshoot with the squeezed luck; social is this port's.
        /// <paramref name="luckRoll"/> is read only for luck.
        /// </summary>
        public static double Score(ContestantStats stats, string category, bool nominated, double bonus, double roll, double luckRoll = 0)
        {
            if (stats == null) throw new ArgumentNullException(nameof(stats));
            switch (category)
            {
                case Luck:
                    Validate(roll, nameof(roll)); Validate(luckRoll, nameof(luckRoll));
                    return Base(stats, category) * (.75 + roll * .5) + luckRoll * 3 + Clutch(stats, nominated) + bonus;
                case Social:
                    Validate(roll, nameof(roll));
                    return Base(stats, category) * (.75 + roll * .5) + Clutch(stats, nominated) + bonus;
                default:
                    return WebRules.WeightedCompetitionScore(stats, category, nominated, bonus, roll, 0);
            }
        }

        /// <summary>The weighted statistics before the roll's multiplier, for luck and social.</summary>
        public static double Base(ContestantStats stats, string category)
        {
            switch (category)
            {
                case Luck: return stats.mental * .1 + SqueezedLuck(stats.luck) * .9;
                case Social: return stats.mental * .2 + stats.social * .6 + stats.luck * .2;
                default: throw new ArgumentException("Only luck and social have rules-4 weights: " + category, nameof(category));
            }
        }

        /// <summary>The nominee's clutch bonus, the reference's: half the competition statistic, on the block only.</summary>
        public static double Clutch(ContestantStats stats, bool nominated) => nominated ? stats.competition * .5 : 0;

        /// <summary>
        /// The share of a thrown score that counts, by the size of the field: the share that leaves a
        /// throw winning one time in ten, measured over 2,000 seasons a field size - the regular
        /// roster's own houseguests, the player as each of them in turn, every kind of competition.
        /// A big field needs no cut at all: without their bonuses a player already wins about one
        /// time in ten there. Two is never a weekly field; its share only keeps the table whole.
        /// </summary>
        public static double ThrowShare(int fieldSize)
        {
            switch (fieldSize)
            {
                case 0: case 1: case 2: return .75;
                case 3: return .82;
                case 4: return .88;
                case 5: return .93;
                case 6: return .94;
                case 7: return .96;
                case 8: return .98;
                case 9: return .99;
                default: return 1;
            }
        }

        private static void Validate(double value, string parameter)
        {
            if (double.IsNaN(value) || value < 0 || value >= 1)
                throw new ArgumentOutOfRangeException(parameter, "Random samples must be in [0, 1).");
        }
    }
}
