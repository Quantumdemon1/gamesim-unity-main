#if !UNITY_5_3_OR_NEWER
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Gamesim.Simulation;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The diagnostics that decide each tuning question (BALANCE plan T0, §4): they read only the lab's rows and
    /// final states and change nothing. Each returns Markdown for its tier's report.
    /// </summary>
    internal static class BalanceLabDiagnostics
    {
        // ---------------------------------------------------------------- Q1: D3's counter

        /// <summary>
        /// The counter's reach as <see cref="PactPlans.CounterReach"/> computes it, under candidate constants: base at a
        /// full view, the view that is full, the Loyal multiplier and the cap.
        /// </summary>
        internal static double Reach(BalanceLab.CounterMember m, double reachBase, double fullView, double loyal, double cap)
        {
            double scale = Math.Max(0, Math.Min(1, m.view / fullView));
            double word = m.traits != null && m.traits.Contains("Sneaky") ? 0 : m.traits != null && m.traits.Contains("Loyal") ? loyal : 1;
            return Math.Min(cap, reachBase * scale * word);
        }

        /// <summary>A member's come-round odds under candidate constants (<see cref="PactPlans.ComeRoundOdds(double, double)"/>, the shape unchanged).</summary>
        internal static double Odds(BalanceLab.CounterMember m, double reachBase, double fullView, double loyal, double cap) =>
            PactPlans.ComeRoundOdds(Reach(m, reachBase, fullView, loyal, cap), m.margin);

        /// <summary>The candidates the what-if replays: the base reach at a full view, with the cap at half as much again (the Loyal reach), the view that is full at 50 or 25.</summary>
        internal static readonly (double reachBase, double fullView)[] Candidates =
        {
            (8, 50), (12, 50), (16, 50), (20, 50), (24, 50), (30, 50), (36, 50), (40, 50), (50, 50), (60, 50),
            (8, 25), (12, 25), (16, 25), (20, 25), (24, 25), (30, 25), (40, 25),
        };

        /// <summary>
        /// Q1 (D3's counter rarely moves anyone): every member a counter could reach at every plan the players answered,
        /// as the lab logged them - their view of the player, their vote margin and traits, as the player knew the season -
        /// and the mean come-round odds and expected comers-round under candidate constants. The odds under today's
        /// constants are recomputed from the record and checked against the odds the lab read.
        /// </summary>
        internal static string CounterWhatIf(IReadOnlyList<BalanceLab.SeasonRun> runs)
        {
            var md = new StringBuilder();
            var all = runs.Where(r => r.error == null).SelectMany(r => r.counterMembers).ToList();
            var countered = all.Where(m => m.stance == PactPlanStance.Countered).ToList();
            md.AppendLine("### Q1 what-if: the counter's reach under candidate constants (T0)");
            md.AppendLine();
            if (all.Count == 0) { md.AppendLine("No member a counter could reach in these seasons."); md.AppendLine(); return md.ToString(); }
            double drift = all.Max(m => Math.Abs(Odds(m, PactPlans.ReachBase, PactPlans.ReachFullView, PactPlans.ReachLoyal, PactPlans.ReachCap) - m.odds));
            md.AppendLine("Members a counter could reach (said the plan, off the block) at every plan the players answered: " + all.Count + ", of them at the "
                + countered.Count + " counters " + countered.Count + "; known to have turned " + all.Count(m => m.lapsed) + ". Today's constants (base "
                + Num(PactPlans.ReachBase) + ", full view " + Num(PactPlans.ReachFullView) + ", Loyal x" + Num(PactPlans.ReachLoyal) + ", cap " + Num(PactPlans.ReachCap)
                + ") recomputed from the records differ from the odds the lab read by at most " + drift.ToString("0.######", CultureInfo.InvariantCulture) + ".");
            md.AppendLine();
            double Quantile(IEnumerable<double> values, double q)
            {
                var sorted = values.OrderBy(x => x).ToList();
                return sorted.Count == 0 ? double.NaN : sorted[(int)Math.Min(sorted.Count - 1, Math.Floor(q * sorted.Count))];
            }
            md.AppendLine("| | p10 | p25 | median | p75 | p90 |");
            md.AppendLine("|---|---|---|---|---|---|");
            foreach (var (name, values) in new[] { ("view of the player", all.Select(m => m.view)), ("vote margin", all.Select(m => m.margin)) })
                md.AppendLine("| " + name + " | " + string.Join(" | ", new[] { 0.1, 0.25, 0.5, 0.75, 0.9 }.Select(q => Num(Quantile(values, q)))) + " |");
            md.AppendLine();
            md.AppendLine("Traits: Loyal " + all.Count(m => m.traits.Contains("Loyal")) + ", Sneaky " + all.Count(m => m.traits.Contains("Sneaky")) + " of " + all.Count + ".");
            md.AppendLine();
            md.AppendLine("| base | full view | cap | mean odds (every answer) | mean odds (counters) | reachable at all | expected to come round (not turned) |");
            md.AppendLine("|---|---|---|---|---|---|---|");
            foreach (var (reachBase, fullView) in Candidates)
            {
                double cap = reachBase * PactPlans.ReachLoyal;
                double Mean(List<BalanceLab.CounterMember> list) => list.Count == 0 ? double.NaN : list.Average(m => Odds(m, reachBase, fullView, PactPlans.ReachLoyal, cap));
                md.AppendLine("| " + Num(reachBase) + " | " + Num(fullView) + " | " + Num(cap) + " | " + Num(Mean(all), "0.00") + " | " + Num(Mean(countered), "0.00") + " | "
                    + BalanceLab.Pct((double)all.Count(m => Odds(m, reachBase, fullView, PactPlans.ReachLoyal, cap) > 0) / all.Count) + " | "
                    + Num(all.Where(m => !m.lapsed).Sum(m => Odds(m, reachBase, fullView, PactPlans.ReachLoyal, cap)) / all.Count, "0.00") + " |");
            }
            md.AppendLine();
            return md.ToString();
        }

        private static string Num(double value, string format = "0.#") => BalanceLab.Num(value, format);
    }
}
#endif
