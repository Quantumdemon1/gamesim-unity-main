using System.Collections.Generic;
using Gamesim.Persistence;

namespace Gamesim.Presentation
{
    /// <summary>One figure of the main menu's career strip: the number, and what it counts in words.</summary>
    public sealed class CareerStat
    {
        public readonly string Value;
        public readonly string Label;

        public CareerStat(string value, string label)
        {
            Value = value;
            Label = label;
        }
    }

    /// <summary>
    /// The main menu's career strip: what the finished seasons add up to, one figure to a cell with
    /// its label in words under it ("3 / seasons", "4th / median finish"), where the menu used to
    /// carry one small line of shorthand. Built from the ledger's summary; no screen.
    /// </summary>
    public static class CareerStrip
    {
        /// <summary>The strip's heading, over its figures.</summary>
        public const string Heading = "Your career";

        /// <summary>The strip's figures, in reading order, or an empty list before any season has finished.</summary>
        public static IReadOnlyList<CareerStat> Of(CareerSummary summary)
        {
            var stats = new List<CareerStat>();
            if (summary == null || summary.Seasons == 0) return stats;
            stats.Add(new CareerStat(summary.Seasons.ToString(), summary.Seasons == 1 ? "season" : "seasons"));
            stats.Add(new CareerStat(summary.Wins.ToString(), summary.Wins == 1 ? "win" : "wins"));
            stats.Add(new CareerStat(CareerSummary.PlaceWord(summary.MedianPlacement), "median finish"));
            stats.Add(new CareerStat(CareerSummary.PlaceWord(summary.BestPlacement), "best finish"));
            stats.Add(new CareerStat(summary.HohWins.ToString(), summary.HohWins == 1 ? "Head of Household win" : "Head of Household wins"));
            stats.Add(new CareerStat(summary.VetoWins.ToString(), summary.VetoWins == 1 ? "Power of Veto win" : "Power of Veto wins"));
            return stats;
        }
    }
}
