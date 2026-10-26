#if !UNITY_5_3_OR_NEWER
// The All-Star histories' balance gate (ALL-STAR-HISTORIES-PLAN §9): Tools/SimulationTests only. Unity's batch runner
// executes [Explicit] tests, so the file is compiled out of the editor's assemblies.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The histories' opening state against the same seasons without it (ALL-STAR-HISTORIES-PLAN §9, slice H1b): All-Stars
    /// houses of 8 and 12, alumni rules 1 (the shipped <see cref="ShippedRules.ApplyFresh"/>) against the paired control
    /// held at 0 (the lab's <c>alumniOff</c>, the same seed), BALANCE_ALUMNI_SEEDS seasons a cell (100).
    ///
    /// <para><b>The gate</b>, fixed before the run: the headline rates in the alumni cells - Q3, the player's season wins
    /// (the passive and the studier newcomer); Q4, the passive newcomer's nomination rate (the share of an NPC Head of
    /// Household's nominations that put them up); and the player's weekly competition win rate (passive and studier) - each
    /// a paired difference with its 95% interval. If an interval excludes zero, the opening state moves balance beyond seed
    /// noise, and by the plan's default H1b lands with fresh seasons not enrolled until B9 unless the owner decides
    /// otherwise. The social newcomer and the player on Cody's card (the receipts about the player reach their deal odds,
    /// §7.2) are reported beside it, and read by no gate.</para>
    ///
    /// <para><b>Reported beside the gate</b> (§9): "both together", the shipped season against a second control that
    /// starts with neither the histories nor agency's first impressions (the lab's <c>impressionsOff</c> with
    /// <c>alumniOff</c>), so the two opening states are never stacked unmeasured; and pacing (commands and weeks a season,
    /// the player's decisions a week, ceremony seconds a regular week). Neither is gated.</para>
    ///
    /// <para>The NPC world is not driven (B5): NPC-to-NPC outcomes are pinned with fixtures, never from these walks.</para>
    /// </summary>
    public sealed class BalanceLabAlumniReport
    {
        internal static readonly int[] Sizes = { 8, 12 };
        internal static readonly string[] GatePolicies = { BalancePolicies.Passive, BalancePolicies.Studier };
        internal static readonly string[] ReportPolicies = { BalancePolicies.Social };
        internal const string Card = "cody-calafiore";

        /// <summary>The three arms of a cell: shipped, the histories alone left out, and both opening states left out.</summary>
        private static readonly (bool alumniOff, bool impressionsOff)[] Arms = { (false, false), (true, false), (true, true) };

        /// <summary>Every cell, each with its two controls: the newcomer at each policy and size, and the passive player on Cody's card.</summary>
        internal static List<BalanceLab.Cell> Cells()
        {
            var cells = new List<BalanceLab.Cell>();
            foreach (int size in Sizes)
            {
                foreach (string policy in GatePolicies.Concat(ReportPolicies))
                    foreach (var arm in Arms)
                        cells.Add(new BalanceLab.Cell { policy = policy, size = size, roster = CastTemplates.Roster.AllStars, alumniOff = arm.alumniOff, impressionsOff = arm.impressionsOff });
                foreach (var arm in Arms)
                    cells.Add(new BalanceLab.Cell { policy = BalancePolicies.Passive, size = size, roster = CastTemplates.Roster.AllStars, playerTemplate = Card, alumniOff = arm.alumniOff, impressionsOff = arm.impressionsOff });
            }
            return cells;
        }

        [Test, Explicit("H1b's balance gate: All-Stars 8 and 12, the histories on against off on the same BALANCE_ALUMNI_SEEDS seasons (100), with both opening states against neither and pacing beside it. Run by name.")]
        public void AlumniHistoriesGate()
        {
            // The "neither" control is the shipped season less exactly its two opening states: no first impression, no
            // history, no ledger row, and every rule as shipped.
            var probe = BalanceLab.Fresh(new BalanceLab.Cell { policy = BalancePolicies.Passive, size = 8, roster = CastTemplates.Roster.AllStars, alumniOff = true, impressionsOff = true }, 1u);
            Assert.That(probe.relationships.All(r => r.score == 0 && r.events.Count == 0), Is.True, "The neither control starts with no standing and no entry.");
            Assert.That(EpisodeEngine.AgencyOn(probe) && probe.alumniRulesVersion == AlumniRules.Off, Is.True, "Agency is on; only its first impressions are left out.");

            int seeds = BalanceLabReports.FromEnvironment("BALANCE_ALUMNI_SEEDS", 100);
            var clock = BalanceLab.Clock();
            var cells = Cells();
            var runs = BalanceLab.Run(cells, seeds);
            Assert.That(runs.Where(r => r.error != null).Select(r => r.cell.Key + " #" + r.index + ": " + r.error).Take(10), Is.Empty);
            var md = new StringBuilder();
            md.AppendLine("All-Star histories gate: " + runs.Length + " seasons (" + seeds + " a cell, paired by seed) in "
                + BalanceLab.Num(clock.Elapsed.TotalMinutes, "0.0") + " min on " + Environment.ProcessorCount + " threads.");
            md.AppendLine();
            md.AppendLine("Against: \"histories alone\" is the shipped season against the same season without the histories (first impressions in both arms; the gate). "
                + "\"both together\" is the shipped season against the same season with neither the histories nor agency's first impressions (reported, not gated).");
            md.AppendLine();
            md.AppendLine("| player | size | against | metric | gate | on | control | paired difference [95%] | n pairs | beyond noise |");
            md.AppendLine("|---|---|---|---|---|---|---|---|---|---|");
            var moved = new List<string>();
            var ungated = new List<string>();
            foreach (var on in cells.Where(c => !c.alumniOff))
            {
                string who = on.playerTemplate == null ? on.policy : on.policy + " on " + on.playerTemplate;
                bool gatedPlayer = on.playerTemplate == null && GatePolicies.Contains(on.policy);
                foreach (var (against, neither) in new[] { ("histories alone", false), ("both together", true) })
                {
                    var control = cells.Single(c => c.alumniOff && c.impressionsOff == neither && c.policy == on.policy && c.size == on.size && c.playerTemplate == on.playerTemplate);
                    var a = runs.Where(r => r.cell.Key == on.Key).OrderBy(r => r.index).ToList();
                    var b = runs.Where(r => r.cell.Key == control.Key).OrderBy(r => r.index).ToList();
                    Assert.That(a.Select(r => r.seed), Is.EqualTo(b.Select(r => r.seed)), "Paired by seed.");
                    bool gated = gatedPlayer && !neither;
                    void Row(string metric, bool gate, string format, Func<BalanceLab.SeasonRun, double?> value)
                    {
                        var pairs = a.Zip(b, (x, y) => (x: value(x), y: value(y))).Where(p => p.x.HasValue && p.y.HasValue).Select(p => (x: p.x.Value, y: p.y.Value)).ToList();
                        if (pairs.Count < 2) { md.AppendLine("| " + who + " | " + on.size + " | " + against + " | " + metric + " | - | n/a | n/a | n/a | " + pairs.Count + " | - |"); return; }
                        var d = pairs.Select(p => p.x - p.y).ToList();
                        double mean = d.Average(), sd = Math.Sqrt(d.Sum(v => (v - mean) * (v - mean)) / (d.Count - 1)), half = 1.959963984540054 * sd / Math.Sqrt(d.Count);
                        bool beyond = mean - half > 0 || mean + half < 0;
                        string signed = "+" + format + ";-" + format + ";" + format;
                        string where = who + " " + on.size + " " + metric + " (" + against + ", " + BalanceLab.Num(mean, signed) + ")";
                        if (beyond && gate) moved.Add(where);
                        else if (beyond) ungated.Add(where);
                        md.AppendLine("| " + who + " | " + on.size + " | " + against + " | " + metric + " | " + (gate ? "yes" : "-") + " | " + BalanceLab.Num(pairs.Average(p => p.x), format)
                            + " | " + BalanceLab.Num(pairs.Average(p => p.y), format) + " | " + BalanceLab.Num(mean, signed) + " [" + BalanceLab.Num(mean - half, signed)
                            + ", " + BalanceLab.Num(mean + half, signed) + "] | " + d.Count + " | " + (beyond ? "**yes**" : "no") + " |");
                    }
                    const string Rate = "0.000", Pace = "0.0";
                    Row("Q3 season wins", gated, Rate, r => r.Won ? 1 : 0);
                    Row("Q4 nomination rate (NPC HoH)", gated && on.policy == BalancePolicies.Passive, Rate,
                        r => r.nominations.Count == 0 ? (double?)null : r.nominations.Count(n => n.playerNominated) / (double)r.nominations.Count);
                    Row("weekly competition win rate", gated, Rate, r =>
                    {
                        var played = r.autopsy.competitions.Where(c => c.playerInField && (c.phase == "HoH" || c.phase == "Veto")).ToList();
                        return played.Count == 0 ? (double?)null : played.Count(c => c.winnerIsPlayer) / (double)played.Count;
                    });
                    Row("final two", false, Rate, r => r.FinalTwo ? 1 : 0);
                    Row("times nominated", false, Rate, r => r.autopsy.playerTimesNominated);
                    Row("weeks in the house", false, Rate, r => r.autopsy.playerOutWeek == 0 ? r.autopsy.weeks : r.autopsy.playerOutWeek);
                    // Pacing (§9: the histories add no interaction; reported anyway).
                    Row("pacing: commands a season", false, Pace, r => r.commands);
                    Row("pacing: weeks a season", false, Pace, r => r.autopsy.weeks);
                    Row("pacing: decisions a week in the house", false, Pace, r =>
                    {
                        int weeks = r.autopsy.playerOutWeek > 0 ? r.autopsy.playerOutWeek : r.autopsy.weeks;
                        return (double)r.decisionsByWeek.Where(p => p.Key <= weeks).Sum(p => p.Value) / Math.Max(1, weeks);
                    });
                    Row("pacing: ceremony s a regular week", false, Pace, r => r.ceremonyByWeek.Count == 0 ? (double?)null : r.ceremonyByWeek.Values.Average());
                }
            }
            md.AppendLine();
            md.AppendLine(moved.Count == 0
                ? "Gate: no headline rate moves beyond seed noise; the opening state enrols fresh seasons (§9)."
                : "Gate: beyond seed noise - " + string.Join("; ", moved) + ". By §9's default, fresh seasons are not enrolled until B9 unless the owner decides otherwise.");
            md.AppendLine();
            md.AppendLine(ungated.Count == 0 ? "Ungated rows beyond seed noise: none." : "Ungated rows beyond seed noise (reported, not gated): " + string.Join("; ", ungated) + ".");
            Directory.CreateDirectory(Path.Combine(TestContext.CurrentContext.WorkDirectory, "balance"));
            File.WriteAllText(Path.Combine(TestContext.CurrentContext.WorkDirectory, "balance", "alumni-histories.md"), md.ToString(), new UTF8Encoding(false));
            TestContext.WriteLine(md.ToString());
        }
    }
}
#endif
