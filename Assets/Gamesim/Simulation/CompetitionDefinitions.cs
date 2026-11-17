using System;
using System.Collections.Generic;
using System.Globalization;

namespace Gamesim.Simulation
{
    public enum CompetitionPattern { Standard, AlternatingWindows, PreviewPairs, PressureWaves, HouseguestNames, RingTower }

    /// <summary>Immutable authored mechanics. Published IDs and their rules must never be repurposed.</summary>
    public sealed class CompetitionDefinition
    {
        public string Id { get; }
        public string Title { get; }
        public string Category { get; }
        public CompetitionPattern Pattern { get; }
        public string Summary { get; }
        public double Duration { get; }
        public double PreviewSeconds => Pattern == CompetitionPattern.PreviewPairs ? 3 : 0;

        internal CompetitionDefinition(string id, string title, string category, CompetitionPattern pattern, double duration, string summary)
        { Id = id; Title = title; Category = category; Pattern = pattern; Duration = duration; Summary = summary; }

        public double ReactionWindow(int oneBasedTarget) => Pattern == CompetitionPattern.AlternatingWindows
            ? (oneBasedTarget % 2 == 1 ? .65 : 1.15) : .9;

        public double GripPressure(double elapsed) => Pattern == CompetitionPattern.PressureWaves && elapsed % 6 >= 4.5 ? 1.7 : 1;

        public double PressureChangeIn(double elapsed)
        {
            double phase = elapsed % 6;
            return phase < 4.5 ? 4.5 - phase : 6 - phase;
        }
    }

    public static class CompetitionDefinitions
    {
        public static readonly CompetitionDefinition SignalSprint = new CompetitionDefinition(
            "signal-sprint-v3", "Signal Sprint", "Skill", CompetitionPattern.Standard, 20,
            "A steady sequence of targets, each visible for 0.90 seconds. Timing does not change when you hit one.");
        public static readonly CompetitionDefinition SwitchbackSignals = new CompetitionDefinition(
            "switchback-signals-v3", "Switchback Signals", "Skill", CompetitionPattern.AlternatingWindows, 20,
            "Targets alternate between 0.65 and 1.15 second windows. The current target shows its full window; every target counts equally.");
        public static readonly CompetitionDefinition HouseMemory = new CompetitionDefinition(
            "house-memory-v3", "House Memory", "Mental", CompetitionPattern.Standard, 30,
            "Discover and match eight hidden pairs in 30 seconds. Remember the cards revealed by your earlier choices.");
        public static readonly CompetitionDefinition FirstImpressions = new CompetitionDefinition(
            "first-impressions-v3", "First Impressions", "Mental", CompetitionPattern.PreviewPairs, 24,
            "Study all cards during a 3 second preview, then match eight pairs in 24 seconds. The preview begins only after the arena is ready.");
        public static readonly CompetitionDefinition HoldYourGround = new CompetitionDefinition(
            "hold-your-ground-v3", "Hold Your Ground", "Endurance", CompetitionPattern.Standard, 30,
            "Steady pressure rises as the clock advances. Manage grip and hold for 19.5 seconds total to earn full marks.");
        public static readonly CompetitionDefinition PressureCooker = new CompetitionDefinition(
            "pressure-cooker-v3", "Pressure Cooker", "Endurance", CompetitionPattern.PressureWaves, 30,
            "Every 6 seconds, a 1.5 second pressure wave drains grip 70% faster while holding. The wave countdown lets you plan recovery. Full marks still require 19.5 seconds of effort.");

        // Rules 4: the reference's luck and social games. Titles are the reference's own: Roll the
        // Dice from its Crapshoot names, Word Scramble its social game's; the houseguest variant is
        // this port's, the same game on the season's own names.
        public static readonly CompetitionDefinition RollTheDice = new CompetitionDefinition(
            "roll-the-dice-v4", "Roll the Dice", "Luck", CompetitionPattern.Standard, 30,
            "Three dice, up to three rolls. Every re-roll replaces the roll you have, so keep a good one when you see it. Your kept total, 3 to 18, is your score.");
        public static readonly CompetitionDefinition WordScramble = new CompetitionDefinition(
            "word-scramble-v4", "Word Scramble", "Social", CompetitionPattern.Standard, 30,
            "Unscramble Big Brother words by choosing their letters in order. Longer words score more: 2 points for five or six letters, 2.5 for seven or eight, 3 for nine or more.");
        public static readonly CompetitionDefinition HouseguestScramble = new CompetitionDefinition(
            "houseguest-scramble-v4", "Houseguest Scramble", "Social", CompetitionPattern.HouseguestNames, 30,
            "Unscramble the first names of this season's houseguests by choosing their letters in order. A longer name scores more: 1.5 points for four letters, 2 for five or six, 2.5 for seven or eight.");

        // Rules 5 (competition ring tower, slice 4): a mental game played on the yard's own three neon
        // stacks rather than on a console. The tower of Hanoi, said plainly; four rings, so the fewest
        // moves is fifteen.
        public static readonly CompetitionDefinition RingTower = new CompetitionDefinition(
            "ring-tower-v5", "Ring Tower", "Mental", CompetitionPattern.RingTower, 90,
            "Move the tower of four rings from the first stack to the last, one ring at a time, never a larger ring on a smaller one. "
            + "Finishing earns most; fewer moves and time left add to it. Fifteen moves is the fewest it can be done in.");

        /// <summary>
        /// Every console game: the boards rules 3 and 4 deal, each played on a station's console. The ring
        /// tower is played on the yard's stacks instead, so it is not here (every console's tests walk this
        /// list); <see cref="Catalogue"/> holds every game a season can deal.
        /// </summary>
        public static IReadOnlyList<CompetitionDefinition> All { get; } = Array.AsReadOnly(new[] {
            SignalSprint, SwitchbackSignals, HouseMemory, FirstImpressions, HoldYourGround, PressureCooker,
            RollTheDice, WordScramble, HouseguestScramble });

        /// <summary>Every game any rules deal: the console games and rules 5's ring tower. Published ids, never reused.</summary>
        public static IReadOnlyList<CompetitionDefinition> Catalogue { get; } = Array.AsReadOnly(new[] {
            SignalSprint, SwitchbackSignals, HouseMemory, FirstImpressions, HoldYourGround, PressureCooker,
            RollTheDice, WordScramble, HouseguestScramble, RingTower });

        public static CompetitionDefinition Standard(string category) => category == "Skill" ? SignalSprint
            : category == "Mental" ? HouseMemory : category == "Endurance" ? HoldYourGround
            : category == "Luck" ? RollTheDice : category == "Social" ? WordScramble : null;

        public static CompetitionDefinition For(EpisodeState state) => state.competitionRulesVersion < 3 ? null
            : state.competitionRulesVersion >= CompetitionRules.RingTower ? Version5(state.seed, state.week, state.phase)
            : state.competitionRulesVersion >= CompetitionRules.Widened ? Version4(state.seed, state.week, state.phase)
            : Version3(state.seed, state.week, state.phase);

        /// <summary>
        /// A frozen selector, not an index into All. New catalog entries cannot reshuffle version 3.
        /// Within each weekly discipline, its HoH and Veto appearances alternate the two patterns.
        /// Final HoH uses its authored advanced rounds. No season random state is read or spent.
        /// </summary>
        public static CompetitionDefinition Version3(uint seasonSeed, int week, EpisodePhase phase)
        {
            if (phase == EpisodePhase.FinalHoHPart1) return PressureCooker;
            if (phase == EpisodePhase.FinalHoHPart2) return SwitchbackSignals;
            if (phase == EpisodePhase.FinalHoHPart3) return FirstImpressions;
            string category = EpisodeEngine.CompetitionCategory(phase, week, 3);
            uint offset = SeededRandom.HashSeed(seasonSeed.ToString(CultureInfo.InvariantCulture) + "/" + category + "/competition-v3");
            bool variant = ((offset + (phase == EpisodePhase.Veto ? 1u : 0u)) & 1u) != 0;
            if (category == "Skill") return variant ? SwitchbackSignals : SignalSprint;
            if (category == "Mental") return variant ? FirstImpressions : HouseMemory;
            return variant ? PressureCooker : HoldYourGround;
        }

        /// <summary>
        /// Rules 4's frozen selector. A kind's variant changes every time the kind comes round,
        /// rather than once a season: its HoH and veto appearances alternate as rules 3's did, and so
        /// does each appearance after them, starting from a variant the season's seed picks. Luck
        /// has one game. The Final HoH keeps its authored rounds. No season random state is read.
        /// </summary>
        public static CompetitionDefinition Version4(uint seasonSeed, int week, EpisodePhase phase)
        {
            if (phase == EpisodePhase.FinalHoHPart1) return PressureCooker;
            if (phase == EpisodePhase.FinalHoHPart2) return SwitchbackSignals;
            if (phase == EpisodePhase.FinalHoHPart3) return FirstImpressions;
            string category = CompetitionRules.Category(phase, week, seasonSeed);
            if (category == CompetitionRules.Luck) return RollTheDice;
            // How many times this kind has come round already this season, HoH before veto each week.
            int appearance = 0;
            for (int earlier = 1; earlier <= week; earlier++)
            {
                if (earlier == week && phase != EpisodePhase.Veto) break;
                if (CompetitionRules.Category(EpisodePhase.HoH, earlier, seasonSeed) == category) appearance++;
                if (earlier == week) break;
                if (CompetitionRules.Category(EpisodePhase.Veto, earlier, seasonSeed) == category) appearance++;
            }
            uint offset = SeededRandom.HashSeed(seasonSeed.ToString(CultureInfo.InvariantCulture) + "/" + category + "/competition-v4");
            bool variant = ((offset + (uint)appearance) & 1u) != 0;
            if (category == CompetitionRules.Skill) return variant ? SwitchbackSignals : SignalSprint;
            if (category == CompetitionRules.Mental) return variant ? FirstImpressions : HouseMemory;
            if (category == CompetitionRules.Social) return variant ? HouseguestScramble : WordScramble;
            return variant ? PressureCooker : HoldYourGround;
        }

        /// <summary>
        /// Rules 5's frozen selector: rules 4's, but for the mental kind. Every other mental competition of
        /// a season is the <see cref="RingTower"/>, starting with its first - so a new season's first mental
        /// competition, always in its first three weeks, is the tower - and the ones between alternate the
        /// two memory boards from a variant the season's seed picks. Rules 4's selector itself is untouched,
        /// so a season under rules 4 deals exactly what it always dealt. No season random state is read.
        /// </summary>
        public static CompetitionDefinition Version5(uint seasonSeed, int week, EpisodePhase phase)
        {
            var dealt = Version4(seasonSeed, week, phase);
            if (phase == EpisodePhase.FinalHoHPart1 || phase == EpisodePhase.FinalHoHPart2 || phase == EpisodePhase.FinalHoHPart3
                || dealt.Category != CompetitionRules.Mental) return dealt;
            int appearance = Appearance(seasonSeed, week, phase, CompetitionRules.Mental);
            if (appearance % 2 == 0) return RingTower;
            uint offset = SeededRandom.HashSeed(seasonSeed.ToString(CultureInfo.InvariantCulture) + "/" + CompetitionRules.Mental + "/competition-v5");
            bool variant = ((offset + (uint)(appearance / 2)) & 1u) != 0;
            return variant ? FirstImpressions : HouseMemory;
        }

        /// <summary>
        /// How many times <paramref name="category"/> has come round already this season before this
        /// competition, HoH before veto each week: rules 4's count, written out again for rules 5 so rules 4's
        /// own selector stays as it was published.
        /// </summary>
        private static int Appearance(uint seasonSeed, int week, EpisodePhase phase, string category)
        {
            int appearance = 0;
            for (int earlier = 1; earlier <= week; earlier++)
            {
                if (earlier == week && phase != EpisodePhase.Veto) break;
                if (CompetitionRules.Category(EpisodePhase.HoH, earlier, seasonSeed) == category) appearance++;
                if (earlier == week) break;
                if (CompetitionRules.Category(EpisodePhase.Veto, earlier, seasonSeed) == category) appearance++;
            }
            return appearance;
        }
    }
}
