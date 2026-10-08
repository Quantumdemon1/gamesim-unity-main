#if !UNITY_5_3_OR_NEWER
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
    /// The balance lab's explicit tiers (BALANCE plan B.4): the headline grid - every policy at eight and
    /// twelve, each on the same seeds - and the performance grid of B4, the passive player at each fixed
    /// performance level. Each writes its rows as JSONL and its tables as Markdown under the test's work
    /// directory, balance/. Seed counts come from BALANCE_SEEDS and BALANCE_PERFORMANCE_SEEDS (the plan's
    /// 800 and 200 by default). Run by name; the batch runner never sees them (compiled out of Unity).
    /// </summary>
    public sealed class BalanceLabReports
    {
        internal static readonly int[] HeadlineSizes = { 8, 12 };

        private static int FromEnvironment(string name, int fallback) =>
            int.TryParse(Environment.GetEnvironmentVariable(name), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) && value > 0 ? value : fallback;

        [Test, Explicit("The headline tier: every policy at eight and twelve on BALANCE_SEEDS seasons each (800 by default). Run by name.")]
        public void HeadlineReport()
        {
            int seeds = FromEnvironment("BALANCE_SEEDS", 800);
            var clock = BalanceLab.Clock();
            var runs = BalanceLab.Run(BalanceLab.Grid(BalancePolicies.All, HeadlineSizes), seeds);
            double minutes = clock.Elapsed.TotalMinutes;
            string rows = BalanceLab.Write("headline", BalanceLab.Jsonl(runs));
            var md = new StringBuilder();
            md.AppendLine("Headline tier: " + runs.Length + " seasons (" + seeds + " per policy and size) in " + BalanceLab.Num(minutes, "0.0") + " min on "
                + Environment.ProcessorCount + " threads; rows in " + rows + ".");
            md.AppendLine();
            md.Append(Headline(runs));
            File.WriteAllText(Path.Combine(TestContext.CurrentContext.WorkDirectory, "balance", "headline.md"), md.ToString(), new UTF8Encoding(false));
            TestContext.WriteLine(md.ToString());
            var errors = runs.Where(r => r.error != null).ToList();
            Assert.That(errors.Select(r => r.cell.Key + " #" + r.index + ": " + r.error).Take(10), Is.Empty);
        }

        [Test, Explicit("B4's performance grid: the passive player at each fixed performance level, at eight and twelve, on BALANCE_PERFORMANCE_SEEDS seasons (200 by default). Run by name.")]
        public void PerformanceReport()
        {
            int seeds = FromEnvironment("BALANCE_PERFORMANCE_SEEDS", 200);
            var cells = PerformanceModel.Levels.SelectMany(level => BalanceLab.Grid(new[] { BalancePolicies.Passive }, HeadlineSizes, PerformanceModel.Fixed(level))).ToList();
            var clock = BalanceLab.Clock();
            var runs = BalanceLab.Run(cells, seeds);
            double minutes = clock.Elapsed.TotalMinutes;
            string rows = BalanceLab.Write("performance", BalanceLab.Jsonl(runs));
            var md = new StringBuilder();
            md.AppendLine("Performance tier: " + runs.Length + " seasons (" + seeds + " per level and size) in " + BalanceLab.Num(minutes, "0.0") + " min; rows in " + rows + ".");
            md.AppendLine();
            md.Append(Performance(runs));
            File.WriteAllText(Path.Combine(TestContext.CurrentContext.WorkDirectory, "balance", "performance.md"), md.ToString(), new UTF8Encoding(false));
            TestContext.WriteLine(md.ToString());
            Assert.That(runs.Where(r => r.error != null).Select(r => r.cell.Key + " #" + r.index + ": " + r.error).Take(10), Is.Empty);
        }

        // ---------------------------------------------------------------- the headline tables

        private static IEnumerable<IGrouping<(string policy, int size), BalanceLab.SeasonRun>> Cells(IEnumerable<BalanceLab.SeasonRun> runs) =>
            runs.Where(r => r.error == null).GroupBy(r => (r.cell.policy, r.cell.size))
                .OrderBy(g => g.Key.size).ThenBy(g => Array.IndexOf(BalancePolicies.All, g.Key.policy));

        private static double Mean(IEnumerable<double> values) { var list = values.ToList(); return list.Count == 0 ? double.NaN : list.Average(); }

        internal static string Headline(IReadOnlyList<BalanceLab.SeasonRun> runs)
        {
            var md = new StringBuilder();
            WinRates(md, runs);
            Survival(md, runs);
            EarlyRisk(md, runs);
            NewcomerLoad(md, runs);
            Competitions(md, runs);
            Concentration(md, runs);
            Commitments(md, runs);
            Economy(md, runs);
            Sense(md, runs);
            Evictions(md, runs);
            House(md, runs);
            Pacing(md, runs);
            Refusals(md, runs);
            return md.ToString();
        }

        private static void WinRates(StringBuilder md, IReadOnlyList<BalanceLab.SeasonRun> runs)
        {
            md.AppendLine("### Win rate by policy and size");
            md.AppendLine();
            md.AppendLine("Base rate is 1/size. McNemar pairs each policy with the passive player on the same seasons (b: only this policy won, c: only passive won).");
            md.AppendLine();
            md.AppendLine("| policy | size | n | wins | win rate | Wilson 95% | final two | mean placement | vs passive b/c | p |");
            md.AppendLine("|---|---|---|---|---|---|---|---|---|---|");
            foreach (var cell in Cells(runs))
            {
                var list = cell.OrderBy(r => r.index).ToList();
                int n = list.Count, wins = list.Count(r => r.Won);
                var passive = runs.Where(r => r.error == null && r.cell.policy == BalancePolicies.Passive && r.cell.size == cell.Key.size).ToDictionary(r => r.index);
                var paired = list.Where(r => passive.ContainsKey(r.index)).ToList();
                var mc = BalanceLab.McNemar(paired.Select(r => r.Won).ToList(), paired.Select(r => passive[r.index].Won).ToList());
                md.AppendLine("| " + cell.Key.policy + " | " + cell.Key.size + " | " + n + " | " + wins + " | " + BalanceLab.Pct((double)wins / n) + " | "
                    + BalanceLab.Interval(BalanceLab.Wilson(wins, n)) + " | " + BalanceLab.Pct((double)list.Count(r => r.FinalTwo) / n) + " | "
                    + BalanceLab.Num(list.Average(r => r.autopsy.placement), "0.00") + " | "
                    + (cell.Key.policy == BalancePolicies.Passive ? "-" : mc.b + "/" + mc.c) + " | " + (cell.Key.policy == BalancePolicies.Passive ? "-" : BalanceLab.Num(mc.p, "0.000")) + " |");
            }
            md.AppendLine();
        }

        private static void Survival(StringBuilder md, IReadOnlyList<BalanceLab.SeasonRun> runs)
        {
            md.AppendLine("### Survival by week: the player still in the house after week k");
            md.AppendLine();
            foreach (int size in runs.Select(r => r.cell.size).Distinct().OrderBy(x => x))
            {
                int weeks = runs.Where(r => r.cell.size == size && r.error == null).Select(r => r.autopsy.weeks).DefaultIfEmpty(0).Max();
                md.AppendLine("| policy (" + size + ") | " + string.Join(" | ", Enumerable.Range(1, weeks).Select(w => "wk " + w)) + " |");
                md.AppendLine("|---|" + string.Concat(Enumerable.Repeat("---|", weeks)));
                foreach (var cell in Cells(runs).Where(c => c.Key.size == size))
                {
                    var list = cell.ToList();
                    md.AppendLine("| " + cell.Key.policy + " | " + string.Join(" | ", Enumerable.Range(1, weeks)
                        .Select(w => BalanceLab.Pct((double)list.Count(r => r.autopsy.playerOutWeek == 0 || r.autopsy.playerOutWeek > w) / list.Count))) + " |");
                }
                md.AppendLine();
            }
        }

        private static void EarlyRisk(StringBuilder md, IReadOnlyList<BalanceLab.SeasonRun> runs)
        {
            md.AppendLine("### Early risk (F7): nominated and evicted in week one, out by week k, and the chance at least one of three testers is out");
            md.AppendLine();
            md.AppendLine("| policy | size | nominated wk 1 | evicted wk 1 [Wilson] | out by wk 2 | out by wk 3 | P(≥1 of 3 out) wk 1 / 2 / 3 |");
            md.AppendLine("|---|---|---|---|---|---|---|");
            foreach (var cell in Cells(runs))
            {
                var list = cell.ToList();
                int n = list.Count;
                double Out(int k) => (double)list.Count(r => r.autopsy.playerOutWeek > 0 && r.autopsy.playerOutWeek <= k) / n;
                int evicted1 = list.Count(r => r.autopsy.playerOutWeek == 1);
                double AnyOfThree(double p) => 1 - Math.Pow(1 - p, 3);
                md.AppendLine("| " + cell.Key.policy + " | " + cell.Key.size + " | " + BalanceLab.Pct((double)list.Count(r => r.autopsy.weeksPlayed.Any(w => w.week == 1 && w.playerNominated)) / n)
                    + " | " + BalanceLab.Pct((double)evicted1 / n) + " " + BalanceLab.Interval(BalanceLab.Wilson(evicted1, n)) + " | " + BalanceLab.Pct(Out(2)) + " | " + BalanceLab.Pct(Out(3))
                    + " | " + BalanceLab.Pct(AnyOfThree(Out(1))) + " / " + BalanceLab.Pct(AnyOfThree(Out(2))) + " / " + BalanceLab.Pct(AnyOfThree(Out(3))) + " |");
            }
            md.AppendLine();
        }

        /// <summary>F4: how often the player is nominated per week in the house, against the houseguests in the same seasons.</summary>
        private static void NewcomerLoad(StringBuilder md, IReadOnlyList<BalanceLab.SeasonRun> runs)
        {
            md.AppendLine("### Nominations per week in the house (F4): the player against the houseguests of the same seasons");
            md.AppendLine();
            md.AppendLine("| policy | size | player | houseguests | ratio |");
            md.AppendLine("|---|---|---|---|---|");
            foreach (var cell in Cells(runs))
            {
                double Rate(SeasonAutopsy.Report a, SeasonAutopsy.Houseguest h)
                {
                    int weeks = h.outWeek > 0 ? h.outWeek : a.weeks;
                    return weeks <= 0 ? 0 : (double)h.timesNominated / weeks;
                }
                double player = Mean(cell.Select(r => Rate(r.autopsy, r.autopsy.houseguests.Single(h => h.isPlayer))));
                double npcs = Mean(cell.SelectMany(r => r.autopsy.houseguests.Where(h => !h.isPlayer).Select(h => Rate(r.autopsy, h))));
                md.AppendLine("| " + cell.Key.policy + " | " + cell.Key.size + " | " + BalanceLab.Num(player, "0.000") + " | " + BalanceLab.Num(npcs, "0.000") + " | " + BalanceLab.Num(player / npcs, "0.00") + " |");
            }
            md.AppendLine();
        }

        private static void Competitions(StringBuilder md, IReadOnlyList<BalanceLab.SeasonRun> runs)
        {
            md.AppendLine("### The player's competitions by policy (weekly HoH and veto they played)");
            md.AppendLine();
            md.AppendLine("| policy | size | played / season | won | win rate [Wilson] | mean performance | HoH wins / season | veto wins / season |");
            md.AppendLine("|---|---|---|---|---|---|---|---|");
            foreach (var cell in Cells(runs))
            {
                var list = cell.ToList();
                var comps = list.SelectMany(r => r.autopsy.competitions.Where(c => c.playerInField && (c.phase == "HoH" || c.phase == "Veto"))).ToList();
                int won = comps.Count(c => c.winnerIsPlayer);
                md.AppendLine("| " + cell.Key.policy + " | " + cell.Key.size + " | " + BalanceLab.Num((double)comps.Count / list.Count, "0.00") + " | " + won + " | "
                    + BalanceLab.Pct(comps.Count == 0 ? 0 : (double)won / comps.Count) + " " + BalanceLab.Interval(BalanceLab.Wilson(won, comps.Count)) + " | "
                    + BalanceLab.Num(Mean(comps.Select(c => c.playerPerformance)), "0.00") + " | " + BalanceLab.Num(list.Average(r => r.autopsy.playerHohWins), "0.00") + " | "
                    + BalanceLab.Num(list.Average(r => r.autopsy.playerVetoWins), "0.00") + " |");
            }
            md.AppendLine();
        }

        /// <summary>How much the cast's statistics decide competitions: the passive seasons, where the player plays at the average draw.</summary>
        private static void Concentration(StringBuilder md, IReadOnlyList<BalanceLab.SeasonRun> runs)
        {
            md.AppendLine("### Competition concentration (all policies' seasons; NPC rows from the passive player's)");
            md.AppendLine();
            md.AppendLine("| size | Spearman NPC wins vs stat sum | top NPC's share of NPC wins | weekly comps won by the strongest on paper | mean winner rank on paper | field size |");
            md.AppendLine("|---|---|---|---|---|---|");
            foreach (int size in runs.Select(r => r.cell.size).Distinct().OrderBy(x => x))
            {
                var passive = runs.Where(r => r.error == null && r.cell.size == size && r.cell.policy == BalancePolicies.Passive).ToList();
                var npcRows = passive.SelectMany(r => r.autopsy.houseguests.Where(h => !h.isPlayer)).ToList();
                double rho = BalanceLab.Spearman(npcRows.Select(h => h.statSum).ToList(), npcRows.Select(h => (double)(h.hohWins + h.vetoWins)).ToList());
                double top = Mean(passive.Select(r =>
                {
                    var wins = r.autopsy.houseguests.Where(h => !h.isPlayer).Select(h => h.hohWins + h.vetoWins).ToList();
                    return wins.Sum() == 0 ? 0 : (double)wins.Max() / wins.Sum();
                }));
                var weekly = runs.Where(r => r.error == null && r.cell.size == size).SelectMany(r => r.autopsy.competitions.Where(c => c.phase == "HoH" || c.phase == "Veto")).ToList();
                md.AppendLine("| " + size + " | " + BalanceLab.Num(rho, "0.000") + " | " + BalanceLab.Pct(top) + " | " + BalanceLab.Pct((double)weekly.Count(c => c.winnerStatRank == 1) / weekly.Count)
                    + " | " + BalanceLab.Num(weekly.Average(c => c.winnerStatRank), "0.00") + " | " + BalanceLab.Num(weekly.Average(c => c.field), "0.0") + " |");
            }
            md.AppendLine();
        }

        private static void Commitments(StringBuilder md, IReadOnlyList<BalanceLab.SeasonRun> runs)
        {
            md.AppendLine("### Pacts and deals per season");
            md.AppendLine();
            md.AppendLine("| policy | size | player pacts | NPC-only pacts | player deals (kept/broken) | NPC-only deals (kept/broken) | player promises (kept/broken) | oaths | weeks at the deal ceiling |");
            md.AppendLine("|---|---|---|---|---|---|---|---|---|");
            foreach (var cell in Cells(runs))
            {
                var c = cell.Select(r => r.autopsy.commitments).ToList();
                md.AppendLine("| " + cell.Key.policy + " | " + cell.Key.size + " | " + BalanceLab.Num(c.Average(x => x.playerPacts)) + " | " + BalanceLab.Num(c.Average(x => x.npcPacts)) + " | "
                    + BalanceLab.Num(c.Average(x => x.playerDeals)) + " (" + BalanceLab.Num(c.Average(x => x.playerDealsKept)) + "/" + BalanceLab.Num(c.Average(x => x.playerDealsBroken)) + ") | "
                    + BalanceLab.Num(c.Average(x => x.npcDeals)) + " (" + BalanceLab.Num(c.Average(x => x.npcDealsKept)) + "/" + BalanceLab.Num(c.Average(x => x.npcDealsBroken)) + ") | "
                    + BalanceLab.Num(c.Average(x => x.playerPromises)) + " (" + BalanceLab.Num(c.Average(x => x.playerPromisesKept)) + "/" + BalanceLab.Num(c.Average(x => x.playerPromisesBroken)) + ") | "
                    + BalanceLab.Num(c.Average(x => x.oathsSworn)) + " | " + BalanceLab.Num(c.Average(x => x.ceilingWeeks)) + " |");
            }
            md.AppendLine();
        }

        private static void Economy(StringBuilder md, IReadOnlyList<BalanceLab.SeasonRun> runs)
        {
            md.AppendLine("### Economy per season: window seats, bought time, Have-Nots, free actions");
            md.AppendLine();
            md.AppendLine("| policy | size | seats offered | spent | wasted | purchases | goodwill paid | Have-Not weeks | free actions |");
            md.AppendLine("|---|---|---|---|---|---|---|---|---|");
            foreach (var cell in Cells(runs))
            {
                var e = cell.Select(r => r.autopsy.economy).ToList();
                md.AppendLine("| " + cell.Key.policy + " | " + cell.Key.size + " | " + BalanceLab.Num(e.Average(x => x.seatsOffered), "0.0") + " | " + BalanceLab.Num(e.Average(x => x.seatsSpent), "0.0")
                    + " | " + BalanceLab.Num(e.Average(x => x.seatsWasted), "0.0") + " | " + BalanceLab.Num(e.Average(x => x.purchases)) + " | " + BalanceLab.Num(e.Average(x => x.goodwillPaid), "0.0")
                    + " | " + BalanceLab.Num(e.Average(x => x.haveNotWeeks)) + " | " + BalanceLab.Num(cell.Average(r => r.freeActions), "0.0") + " |");
            }
            md.AppendLine();
        }

        private static void Sense(StringBuilder md, IReadOnlyList<BalanceLab.SeasonRun> runs)
        {
            md.AppendLine("### Game Sense by policy (bootstrap 95% for the mean)");
            md.AppendLine();
            md.AppendLine("| policy | size | Game Sense | competitions | strategy | social |");
            md.AppendLine("|---|---|---|---|---|---|");
            foreach (var cell in Cells(runs))
            {
                var scores = cell.Select(r => (double)r.autopsy.gameSense).ToList();
                var ci = BalanceLab.Bootstrap(scores);
                md.AppendLine("| " + cell.Key.policy + " | " + cell.Key.size + " | " + BalanceLab.Num(scores.Average(), "0.0") + " [" + BalanceLab.Num(ci.low, "0.0") + ", " + BalanceLab.Num(ci.high, "0.0") + "] | "
                    + BalanceLab.Num(cell.Average(r => r.autopsy.gameSenseCompetitions), "0.0") + " | " + BalanceLab.Num(cell.Average(r => r.autopsy.gameSenseStrategy), "0.0") + " | "
                    + BalanceLab.Num(cell.Average(r => r.autopsy.gameSenseSocial), "0.0") + " |");
            }
            foreach (int size in runs.Select(r => r.cell.size).Distinct().OrderBy(x => x))
            {
                double G(string policy) => Mean(runs.Where(r => r.error == null && r.cell.size == size && r.cell.policy == policy).Select(r => (double)r.autopsy.gameSense));
                md.AppendLine();
                md.AppendLine("Size " + size + ": gated reader minus random " + BalanceLab.Num(G(BalancePolicies.Reader) - G(BalancePolicies.Random), "0.0")
                    + "; oracle reader minus random " + BalanceLab.Num(G(BalancePolicies.OracleReader) - G(BalancePolicies.Random), "0.0") + " (target 20).");
            }
            md.AppendLine();
        }

        private static void Evictions(StringBuilder md, IReadOnlyList<BalanceLab.SeasonRun> runs)
        {
            md.AppendLine("### Who goes out, NPC Heads of Household, and the jury (all policies' seasons)");
            md.AppendLine();
            md.AppendLine("| size | evictee's threat rank (HoH's view) | evictees in a pact | evictee's competition wins | NPC HoH nominated the top threat | jury margin | bitter jurors |");
            md.AppendLine("|---|---|---|---|---|---|---|");
            foreach (int size in runs.Select(r => r.cell.size).Distinct().OrderBy(x => x))
            {
                var list = runs.Where(r => r.error == null && r.cell.size == size).ToList();
                var weeks = list.SelectMany(r => r.autopsy.weeksPlayed.Where(w => w.evicteeId != null && !w.finalEviction)).ToList();
                var ranked = weeks.Where(w => w.evicteeThreatRank > 0).ToList();
                int noms = list.Sum(r => r.autopsy.agency.npcNominations), top = list.Sum(r => r.autopsy.agency.topThreatNominated);
                int withEvictor = list.Sum(r => r.autopsy.jury.jurorsWithEvictorFinalist), bitter = list.Sum(r => r.autopsy.jury.bitterJurors);
                md.AppendLine("| " + size + " | " + BalanceLab.Num(Mean(ranked.Select(w => (double)w.evicteeThreatRank)), "0.00") + " | " + BalanceLab.Pct((double)weeks.Count(w => w.evicteeInPact) / weeks.Count)
                    + " | " + BalanceLab.Num(Mean(weeks.Where(w => w.evicteeCompetitionWins >= 0).Select(w => (double)w.evicteeCompetitionWins))) + " | "
                    + (noms == 0 ? "n/a" : BalanceLab.Pct((double)top / noms)) + " | " + BalanceLab.Num(Mean(list.Select(r => (double)r.autopsy.jury.margin))) + " | "
                    + (withEvictor == 0 ? "n/a" : BalanceLab.Pct((double)bitter / withEvictor) + " of jurors whose evictor reached the final two") + " |");
            }
            md.AppendLine();
        }

        /// <summary>The house itself: its agendas, its warmest and coldest pair, and the story's pace (all policies' seasons).</summary>
        private static void House(StringBuilder md, IReadOnlyList<BalanceLab.SeasonRun> runs)
        {
            md.AppendLine("### The house: agendas, pairs and the story's pace (all policies' seasons)");
            md.AppendLine();
            md.AppendLine("| size | NPC agendas as each social week closed | warmest pair (mutual, mean / max) | coldest pair (mutual, mean / min) | story asks / season | weeks with a card | storylines finished | NPC removals | showmances | pile-ons | pariah weeks |");
            md.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|");
            foreach (int size in runs.Select(r => r.cell.size).Distinct().OrderBy(x => x))
            {
                var list = runs.Where(r => r.error == null && r.cell.size == size).Select(r => r.autopsy).ToList();
                var agendas = list.SelectMany(a => a.agency.agendas).GroupBy(p => p.Key).Select(g => (kind: g.Key, total: g.Sum(p => p.Value))).OrderByDescending(x => x.total).ToList();
                double all = Math.Max(1, agendas.Sum(x => x.total));
                md.AppendLine("| " + size + " | " + string.Join(", ", agendas.Select(x => x.kind + " " + BalanceLab.Pct(x.total / all))) + " | "
                    + BalanceLab.Num(list.Average(a => a.warmest.mutual), "0") + " / " + BalanceLab.Num(list.Max(a => a.warmest.mutual), "0") + " | "
                    + BalanceLab.Num(list.Average(a => a.coldest.mutual), "0") + " / " + BalanceLab.Num(list.Min(a => a.coldest.mutual), "0") + " | "
                    + BalanceLab.Num(list.Average(a => a.story.asks), "0.0") + " | " + BalanceLab.Num(list.Average(a => a.story.weeksWithACard), "0.0") + " | "
                    + BalanceLab.Num(list.Average(a => a.story.completed), "0.0") + " | " + BalanceLab.Num(list.Average(a => a.story.npcRemovals), "0.00") + " | "
                    + BalanceLab.Num(list.Average(a => a.story.showmances), "0.00") + " | " + BalanceLab.Num(list.Average(a => a.story.pileOns), "0.00") + " | "
                    + BalanceLab.Num(list.Average(a => a.story.pariahWeeks), "0.00") + " |");
            }
            md.AppendLine();
        }

        private static void Pacing(StringBuilder md, IReadOnlyList<BalanceLab.SeasonRun> runs)
        {
            md.AppendLine("### Pacing: decisions and ceremony seconds a week");
            md.AppendLine();
            md.AppendLine("Decisions are the player's accepted commands other than Advance. Ceremony seconds are the key ceremony, veto meeting and live eviction at the suspenseful pace, fade to fade, unskipped (CeremonyPacing); the finale's jury reveal apart.");
            md.AppendLine();
            md.AppendLine("| policy | size | decisions / week (weeks in the house) | ceremony s / regular week | finale reveal s |");
            md.AppendLine("|---|---|---|---|---|");
            foreach (var cell in Cells(runs))
            {
                double decisions = Mean(cell.Select(r =>
                {
                    int weeks = r.autopsy.playerOutWeek > 0 ? r.autopsy.playerOutWeek : r.autopsy.weeks;
                    return (double)r.decisionsByWeek.Where(p => p.Key <= weeks).Sum(p => p.Value) / Math.Max(1, weeks);
                }));
                md.AppendLine("| " + cell.Key.policy + " | " + cell.Key.size + " | " + BalanceLab.Num(decisions, "0.0") + " | "
                    + BalanceLab.Num(Mean(cell.SelectMany(r => r.ceremonyByWeek.Values)), "0.0") + " | " + BalanceLab.Num(Mean(cell.Select(r => r.finaleSeconds)), "0.0") + " |");
            }
            md.AppendLine();
        }

        private static void Refusals(StringBuilder md, IReadOnlyList<BalanceLab.SeasonRun> runs)
        {
            md.AppendLine("### Refusals and the walker");
            md.AppendLine();
            md.AppendLine("| policy | size | own commands / season | walker steps / season | refusals / season | most common refusal |");
            md.AppendLine("|---|---|---|---|---|---|");
            foreach (var cell in Cells(runs))
            {
                var reasons = cell.SelectMany(r => r.refusalsByKind).GroupBy(p => p.Key).Select(g => (g.Key, total: g.Sum(p => p.Value)))
                    .OrderByDescending(x => x.total).ThenBy(x => x.Key, StringComparer.Ordinal).FirstOrDefault();
                md.AppendLine("| " + cell.Key.policy + " | " + cell.Key.size + " | " + BalanceLab.Num(cell.Average(r => r.own), "0.0") + " | " + BalanceLab.Num(cell.Average(r => r.fallbacks), "0.0")
                    + " | " + BalanceLab.Num(cell.Average(r => r.refusals), "0.00") + " | " + (reasons.Key == null ? "-" : reasons.Key + " (" + reasons.total + ")") + " |");
            }
            int errors = runs.Count(r => r.error != null);
            md.AppendLine();
            md.AppendLine("Seasons with an error (walker refused, invalid state, unfinished): " + errors + " of " + runs.Count + ".");
            md.AppendLine();
        }

        // ---------------------------------------------------------------- the performance tables

        internal static string Performance(IReadOnlyList<BalanceLab.SeasonRun> runs)
        {
            var md = new StringBuilder();
            md.AppendLine("### The passive player's weekly competitions by fixed performance (F3)");
            md.AppendLine();
            md.AppendLine("| size | performance | played | won | win rate [Wilson] | field 3-5 | field 6-8 | field 9-11 | season win rate [Wilson] |");
            md.AppendLine("|---|---|---|---|---|---|---|---|---|");
            foreach (var group in runs.Where(r => r.error == null).GroupBy(r => (r.cell.size, r.cell.performance)).OrderBy(g => g.Key.size).ThenBy(g => g.Key.performance, StringComparer.Ordinal))
            {
                var comps = group.SelectMany(r => r.autopsy.competitions.Where(c => c.playerInField && (c.phase == "HoH" || c.phase == "Veto"))).ToList();
                int won = comps.Count(c => c.winnerIsPlayer), seasons = group.Count(), wins = group.Count(r => r.Won);
                string Field(int low, int high)
                {
                    var some = comps.Where(c => c.field >= low && c.field <= high).ToList();
                    return some.Count == 0 ? "-" : BalanceLab.Pct((double)some.Count(c => c.winnerIsPlayer) / some.Count) + " (" + some.Count + ")";
                }
                md.AppendLine("| " + group.Key.size + " | " + group.Key.performance.Substring(6) + " | " + comps.Count + " | " + won + " | "
                    + BalanceLab.Pct(comps.Count == 0 ? 0 : (double)won / comps.Count) + " " + BalanceLab.Interval(BalanceLab.Wilson(won, comps.Count)) + " | "
                    + Field(3, 5) + " | " + Field(6, 8) + " | " + Field(9, 11) + " | " + BalanceLab.Pct((double)wins / seasons) + " " + BalanceLab.Interval(BalanceLab.Wilson(wins, seasons)) + " |");
            }
            md.AppendLine();
            return md.ToString();
        }
    }
}
#endif
