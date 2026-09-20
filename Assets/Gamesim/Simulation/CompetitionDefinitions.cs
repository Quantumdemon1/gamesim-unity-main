using System;
using System.Collections.Generic;
using System.Globalization;

namespace Gamesim.Simulation
{
    public enum CompetitionPattern { Standard, AlternatingWindows, PreviewPairs, PressureWaves }

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

        public static IReadOnlyList<CompetitionDefinition> All { get; } = Array.AsReadOnly(new[] {
            SignalSprint, SwitchbackSignals, HouseMemory, FirstImpressions, HoldYourGround, PressureCooker });

        public static CompetitionDefinition Standard(string category) => category == "Skill" ? SignalSprint
            : category == "Mental" ? HouseMemory : category == "Endurance" ? HoldYourGround : null;

        public static CompetitionDefinition For(EpisodeState state) => state.competitionRulesVersion < 3 ? null
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
    }
}
