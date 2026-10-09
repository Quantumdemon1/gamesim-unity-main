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
    /// twelve, each on the same seeds - the full grid (overnight: every policy at four to twelve and the
    /// All-Stars eight and twelve), and the performance grid of B4, the passive player at each fixed
    /// performance level. Each writes its rows as JSONL and its tables as Markdown under the test's work
    /// directory, balance/. Seed counts come from BALANCE_SEEDS, BALANCE_FULL_SEEDS and
    /// BALANCE_PERFORMANCE_SEEDS (the plan's 800, 800 and 200 by default). Run by name; the batch runner
    /// never sees them (compiled out of Unity).
    /// </summary>
    public sealed class BalanceLabReports
    {
        internal static readonly int[] HeadlineSizes = { 8, 12 };
        internal static readonly int[] FullSizes = { 4, 6, 8, 10, 12 };
        internal static readonly int[] FullAllStarsSizes = { 8, 12 };

        internal static int FromEnvironment(string name, int fallback) =>
            int.TryParse(Environment.GetEnvironmentVariable(name), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) && value > 0 ? value : fallback;

        /// <summary>A tier: its cells, the seed count's variable and default, and its tables.</summary>
        internal sealed class TierSpec
        {
            public string name, title, per, seedsVariable;
            public int defaultSeeds;
            public Func<List<BalanceLab.Cell>> cells;
            public Func<IReadOnlyList<BalanceLab.SeasonRun>, string> tables;
            public int Seeds => FromEnvironment(seedsVariable, defaultSeeds);
        }

        /// <summary>Every tier by name, for the reports below and for a tier played in parts (<see cref="BalanceLabParts"/>).</summary>
        internal static readonly Dictionary<string, TierSpec> Tiers = new[]
        {
            new TierSpec { name = "headline", title = "Headline tier", per = "policy and house", seedsVariable = "BALANCE_SEEDS", defaultSeeds = 800,
                cells = () => BalanceLab.Grid(BalancePolicies.All, HeadlineSizes), tables = Headline },
            new TierSpec { name = "full", title = "Full tier", per = "policy and house", seedsVariable = "BALANCE_FULL_SEEDS", defaultSeeds = 800,
                cells = FullGrid, tables = Headline },
            new TierSpec { name = "performance", title = "Performance tier", per = "level and size", seedsVariable = "BALANCE_PERFORMANCE_SEEDS", defaultSeeds = 200,
                cells = () => PerformanceModel.Levels.SelectMany(level => BalanceLab.Grid(new[] { BalancePolicies.Passive }, HeadlineSizes, PerformanceModel.Fixed(level))).ToList(),
                tables = Performance },
            new TierSpec { name = "warrooms", title = "War-room tier", per = "policy and house", seedsVariable = "BALANCE_WARROOM_SEEDS", defaultSeeds = 100,
                cells = () => BalanceLab.Grid(WarRoomPolicies, HeadlineSizes), tables = runs => { var md = new StringBuilder(); WarRooms(md, runs); md.Append(BalanceLabDiagnostics.CounterWhatIf(runs)); return md.ToString(); } },
            new TierSpec { name = "npc", title = "NPC budget tier", per = "policy, house and budget", seedsVariable = "BALANCE_NPC_SEEDS", defaultSeeds = 200,
                cells = NpcGrid, tables = runs => NpcBudget(runs) + BalanceLabDiagnostics.NominationDecomposition(runs) + BalanceLabDiagnostics.PariahDecomposition(runs) },
            new TierSpec { name = "projection", title = "Projection tier", per = "house and budget", seedsVariable = "BALANCE_PROJECTION_SEEDS", defaultSeeds = 400,
                cells = ProjectionGrid, tables = Projections },
            new TierSpec { name = "competition", title = "Competition tier", per = "player, performance and size", seedsVariable = "BALANCE_COMPETITION_SEEDS", defaultSeeds = 200,
                cells = () => new[] { 0.5, 0.8 }.SelectMany(level => BalanceLab.Grid(new[] { BalancePolicies.Passive, BalancePolicies.Studier }, HeadlineSizes, PerformanceModel.Fixed(level))).ToList(),
                tables = BalanceLabDiagnostics.Preparation },
        }.ToDictionary(t => t.name, StringComparer.Ordinal);

        /// <summary>B7's grid: the novice in every house the full tier plays, at BALANCE_PROJECTION_BUDGETS (nought, 300 and 900).</summary>
        internal static List<BalanceLab.Cell> ProjectionGrid() =>
            ListFromEnvironment("BALANCE_PROJECTION_BUDGETS", new[] { "0", "300", "900" }).Select(b => int.Parse(b, NumberStyles.Integer, CultureInfo.InvariantCulture))
                .SelectMany(budget => BalanceLab.Grid(new[] { BalancePolicies.Novice }, FullSizes, npcTicks: budget)
                    .Concat(BalanceLab.Grid(new[] { BalancePolicies.Novice }, new[] { 8 }, roster: CastTemplates.Roster.AllStars, npcTicks: budget))).ToList();

        [Test, Explicit("B7's projections: the novice at 4-12 and the All-Stars eight at BALANCE_PROJECTION_BUDGETS on BALANCE_PROJECTION_SEEDS seasons (400). Run by name, or in parts.")]
        public void ProjectionReport() => Tier(Tiers["projection"]);

        [Test, Explicit("T0 Q3: the studier against passive at fixed performance .5 and .8, eight and twelve, BALANCE_COMPETITION_SEEDS seasons (200). Run by name, or in parts.")]
        public void CompetitionReport() => Tier(Tiers["competition"]);

        /// <summary>The NPC budget grid's players (BALANCE_NPC_POLICIES, by default the brief's five).</summary>
        internal static string[] NpcPolicies => ListFromEnvironment("BALANCE_NPC_POLICIES", new[] { BalancePolicies.Passive, BalancePolicies.Novice, BalancePolicies.Social, BalancePolicies.Reader, BalancePolicies.Beast });

        /// <summary>The NPC budget grid's ticks a week (BALANCE_NPC_BUDGETS, by default nought, 300, 900 and 1800); nought first, the pairs' base.</summary>
        internal static int[] NpcBudgets => ListFromEnvironment("BALANCE_NPC_BUDGETS", new[] { "0", "300", "900", "1800" })
            .Select(b => int.Parse(b, NumberStyles.Integer, CultureInfo.InvariantCulture)).Where(b => b >= 0).Distinct().OrderBy(b => b).ToArray();

        private static string[] ListFromEnvironment(string name, string[] fallback)
        {
            string value = Environment.GetEnvironmentVariable(name);
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Split(',').Select(x => x.Trim()).Where(x => x.Length > 0).ToArray();
        }

        /// <summary>B5b's grid: each player at eight and twelve at each budget, every budget on the same seasons.</summary>
        internal static List<BalanceLab.Cell> NpcGrid() =>
            NpcBudgets.SelectMany(budget => BalanceLab.Grid(NpcPolicies, HeadlineSizes, npcTicks: budget)).ToList();

        [Test, Explicit("B5b's NPC budget tier: BALANCE_NPC_POLICIES at eight and twelve at BALANCE_NPC_BUDGETS ticks a week on BALANCE_NPC_SEEDS seasons (200), every metric against budget nought on the same seasons. Run by name, or in parts.")]
        public void NpcBudgetReport() => Tier(Tiers["npc"]);

        /// <summary>The players who convene a war room and answer its plan (B6a).</summary>
        internal static readonly string[] WarRoomPolicies = { BalancePolicies.Random, BalancePolicies.Reader, BalancePolicies.Schemer, BalancePolicies.Loyalist, BalancePolicies.Floater };

        [Test, Explicit("The headline tier: every policy at eight and twelve on BALANCE_SEEDS seasons each (800 by default). Run by name.")]
        public void HeadlineReport() => Tier(Tiers["headline"]);

        [Test, Explicit("The full tier (overnight): every policy at 4, 6, 8, 10 and 12 and the All-Stars eight and twelve, on BALANCE_FULL_SEEDS seasons each (800 by default). Run by name.")]
        public void FullReport() => Tier(Tiers["full"]);

        internal static List<BalanceLab.Cell> FullGrid() =>
            BalanceLab.Grid(BalancePolicies.All, FullSizes).Concat(BalanceLab.Grid(BalancePolicies.All, FullAllStarsSizes, roster: CastTemplates.Roster.AllStars)).ToList();

        [Test, Explicit("B4's performance grid: the passive player at each fixed performance level, at eight and twelve, on BALANCE_PERFORMANCE_SEEDS seasons (200 by default). Run by name.")]
        public void PerformanceReport() => Tier(Tiers["performance"]);

        [Test, Explicit("B6a's war-room grid: the players who convene a war room at eight and twelve on BALANCE_WARROOM_SEEDS seasons (100 by default), with the counter's what-if (T0, Q1). Run by name.")]
        public void WarRoomReport() => Tier(Tiers["warrooms"]);

        /// <summary>The NPC world's cost (B5b, risk 1): seasons at a budget, their operations and the engine's milliseconds an operation.</summary>
        internal static string NpcCost(IReadOnlyList<BalanceLab.SeasonRun> runs, double wallMinutes)
        {
            var md = new StringBuilder();
            md.AppendLine("### The NPC world's cost");
            md.AppendLine();
            md.AppendLine("| budget (ticks a week) | seasons | operations / season | ticks / season | starts / season | rejected / season | engine ms / operation | engine s / season |");
            md.AppendLine("|---|---|---|---|---|---|---|---|");
            foreach (var g in runs.Where(r => r.error == null).GroupBy(r => r.cell.npcTicks).OrderBy(g => g.Key))
            {
                double ops = g.Sum(r => r.npcOps), ms = g.Sum(r => r.npcMilliseconds);
                md.AppendLine("| " + g.Key + " | " + g.Count() + " | " + BalanceLab.Num(g.Average(r => r.npcOps), "0") + " | " + BalanceLab.Num(g.Average(r => r.npcTicks), "0") + " | "
                    + BalanceLab.Num(g.Average(r => r.npcStarts), "0.0") + " | " + BalanceLab.Num(g.Average(r => r.npcRejected), "0.0") + " | "
                    + (ops == 0 ? "-" : BalanceLab.Num(ms / ops, "0.000")) + " | " + BalanceLab.Num(ms / 1000 / g.Count(), "0.00") + " |");
            }
            md.AppendLine();
            md.AppendLine("Engine milliseconds are PrepareNpcOperation and installing the candidate as a new engine, as the director does: each validates the whole season.");
            md.AppendLine();
            if (double.IsNaN(wallMinutes)) return md.ToString();
            md.AppendLine(runs.Count + " seasons in " + BalanceLab.Num(wallMinutes, "0.0") + " min on " + Environment.ProcessorCount + " threads ("
                + BalanceLab.Num(wallMinutes * 60 * Environment.ProcessorCount / Math.Max(1, runs.Count), "0.00") + " thread-seconds a season).");
            md.AppendLine();
            return md.ToString();
        }

        [Test, Explicit("B5b's cost probe (risk 1): BALANCE_PROBE_SEEDS seasons a house (5) at BALANCE_PROBE_TICKS a week (1800), the reader at eight and twelve; the engine's ms an operation. Run by name.")]
        public void NpcWorldCostProbe()
        {
            int ticks = FromEnvironment("BALANCE_PROBE_TICKS", 1800);
            var cells = HeadlineSizes.Select(size => new BalanceLab.Cell { policy = BalancePolicies.Reader, size = size, npcTicks = ticks }).ToList();
            var clock = BalanceLab.Clock();
            var runs = BalanceLab.Run(cells, FromEnvironment("BALANCE_PROBE_SEEDS", 5));
            string md = NpcCost(runs, clock.Elapsed.TotalMinutes);
            TestContext.WriteLine(md);
            Assert.That(runs.Where(r => r.error != null).Select(r => r.cell.Key + " #" + r.index + ": " + r.error).Take(10), Is.Empty);
            Assert.That(runs.All(r => r.npcTicks > 0), Is.True, "Every season ticked.");
        }

        /// <summary>Plays a tier's cells, writes its rows and tables under balance/, and fails on any season with an error.</summary>
        private static void Tier(TierSpec tier)
        {
            var clock = BalanceLab.Clock();
            var runs = BalanceLab.Run(tier.cells(), tier.Seeds);
            Write(tier, runs, tier.Seeds, clock.Elapsed.TotalMinutes);
        }

        /// <summary>A tier's rows and tables, written under balance/ and to the test's output; fails on any season with an error.</summary>
        internal static void Write(TierSpec tier, IReadOnlyList<BalanceLab.SeasonRun> runs, int seeds, double minutes)
        {
            string rows = BalanceLab.Write(tier.name, BalanceLab.Jsonl(runs));
            var md = new StringBuilder();
            md.AppendLine(tier.title + ": " + runs.Count + " seasons (" + seeds + " per " + tier.per + ") in " + BalanceLab.Num(minutes, "0.0") + " min on "
                + Environment.ProcessorCount + " threads; rows in " + rows + ".");
            md.AppendLine();
            md.Append(tier.tables(runs));
            File.WriteAllText(Path.Combine(TestContext.CurrentContext.WorkDirectory, "balance", tier.name + ".md"), md.ToString(), new UTF8Encoding(false));
            TestContext.WriteLine(md.ToString());
            Assert.That(runs.Where(r => r.error != null).Select(r => r.cell.Key + " #" + r.index + ": " + r.error).Take(10), Is.Empty);
        }

        // ---------------------------------------------------------------- the headline tables

        /// <summary>
        /// A house as the tables name it: its size, with the roster when it is not the regular cast, and the NPC world's
        /// ticks a week when it plays one ("8 npc300"), so no table merges two budgets (B5b).
        /// </summary>
        internal static string HouseOf(BalanceLab.Cell cell) =>
            (cell.roster == CastTemplates.Roster.Regular ? "" : "All-Stars ") + cell.size.ToString(CultureInfo.InvariantCulture)
            + (cell.npcTicks == 0 ? "" : " npc" + cell.npcTicks.ToString(CultureInfo.InvariantCulture));

        private static int HouseOrder(BalanceLab.Cell cell) => (int)cell.roster * 1000000 + cell.size * 10000 + cell.npcTicks;

        /// <summary>The houses in the runs, regular cast first, each by size.</summary>
        private static List<string> Houses(IEnumerable<BalanceLab.SeasonRun> runs) =>
            runs.GroupBy(r => HouseOf(r.cell)).OrderBy(g => HouseOrder(g.First().cell)).Select(g => g.Key).ToList();

        /// <summary>The runs without an error by policy and house (the house in "size": a size, or "All-Stars" and a size, with its NPC budget).</summary>
        internal static IEnumerable<IGrouping<(string policy, string size), BalanceLab.SeasonRun>> Cells(IEnumerable<BalanceLab.SeasonRun> runs) =>
            runs.Where(r => r.error == null).GroupBy(r => (r.cell.policy, size: HouseOf(r.cell)))
                .OrderBy(g => HouseOrder(g.First().cell)).ThenBy(g => Array.IndexOf(BalancePolicies.All, g.Key.policy));

        private static double Mean(IEnumerable<double> values) { var list = values.ToList(); return list.Count == 0 ? double.NaN : list.Average(); }

        internal static string Headline(IReadOnlyList<BalanceLab.SeasonRun> runs)
        {
            var md = new StringBuilder();
            WinRates(md, runs);
            PairTable(md, runs);
            Band(md, runs);
            Survival(md, runs);
            EarlyRisk(md, runs);
            NewcomerLoad(md, runs);
            Competitions(md, runs);
            Concentration(md, runs);
            Commitments(md, runs);
            WarRooms(md, runs);
            Economy(md, runs);
            Sense(md, runs);
            Evictions(md, runs);
            House(md, runs);
            Pacing(md, runs);
            Refusals(md, runs);
            // T0: the diagnostics that decide the tuning questions (BALANCE plan §4).
            md.Append(BalanceLabDiagnostics.GameSenseWhatIf(runs));
            md.Append(BalanceLabDiagnostics.NominationDecomposition(runs));
            md.Append(BalanceLabDiagnostics.PariahDecomposition(runs));
            if (runs.Any(r => r.counterMembers.Count > 0)) md.Append(BalanceLabDiagnostics.CounterWhatIf(runs));
            return md.ToString();
        }

        // ---------------------------------------------------------------- the projections (B7)

        /// <summary>
        /// B7: for each house and budget, the novice's S(k), C(k) and the session criteria by week for three, four and
        /// five testers; the lowest week each reaches 90%; and the minutes a week in the house costs, with the session
        /// length each lowest week implies, against E1's 30-45 minutes an episode.
        /// </summary>
        internal static string Projections(IReadOnlyList<BalanceLab.SeasonRun> runs)
        {
            var md = new StringBuilder();
            var houses = runs.Where(r => r.error == null).GroupBy(r => HouseOf(r.cell)).OrderBy(g => HouseOrder(g.First().cell)).ToList();
            md.AppendLine("### B7: the projections for a playtest session (the novice)");
            md.AppendLine();
            md.AppendLine("S(k): out by week k. C(k): saw a commitment of theirs settle (a deal or promise kept or broken whose ending they know, or a war-room plan they answered) while still in the house, by week k. "
                + "Any out (n): 1 - (1 - S)^n, E4 alone. All saw (n): C^n, E3 alone. Joint (n): every tester saw one and at least one is out, P(A)^n - P(A and not B)^n from each season's joint indicators.");
            md.AppendLine();
            var summary = new List<string>();
            foreach (var house in houses)
            {
                var list = house.ToList();
                var testers = list.Select(r => new Projection.Tester(r.autopsy.playerOutWeek, r.autopsy.firstSettledWeek)).ToList();
                int max = list.Max(r => r.autopsy.weeks);
                md.AppendLine("#### " + house.Key + " (" + list.Count + " seasons)");
                md.AppendLine();
                md.AppendLine("| k | S(k) | C(k) | any out (3) | all saw (3) | joint (3) | joint (4) | joint (5) | session minutes to week k |");
                md.AppendLine("|---|---|---|---|---|---|---|---|---|");
                var minutes = WeekMinutes(list);
                for (int k = 1; k <= max; k++)
                    md.AppendLine("| " + k + " | " + Pct(Projection.S(testers, k)) + " | " + Pct(Projection.C(testers, k)) + " | " + Pct(Projection.AnyOut(testers, k, 3)) + " | "
                        + Pct(Projection.AllSaw(testers, k, 3)) + " | " + Pct(Projection.Joint(testers, k, 3)) + " | " + Pct(Projection.Joint(testers, k, 4)) + " | "
                        + Pct(Projection.Joint(testers, k, 5)) + " | " + BalanceLab.Num(SessionMinutes(minutes, k), "0") + " |");
                md.AppendLine();
                string Lowest(Func<int, double> p) { int k = Projection.LowestWeek(p, 0.9, max); return k == 0 ? "never" : k + " (" + BalanceLab.Num(SessionMinutes(minutes, k), "0") + " min)"; }
                double perWeek = minutes.Count == 0 ? 0 : minutes.Values.Sum(m => m.total) / Math.Max(1, minutes.Values.Sum(m => m.n));
                summary.Add("| " + house.Key + " | " + list.Count + " | " + BalanceLab.Num(perWeek, "0.0") + " | "
                    + string.Join(" | ", new[] { 3, 4, 5 }.Select(n => Lowest(k => Projection.AnyOut(testers, k, n)))) + " | "
                    + string.Join(" | ", new[] { 3, 4, 5 }.Select(n => Lowest(k => Projection.AllSaw(testers, k, n)))) + " | "
                    + string.Join(" | ", new[] { 3, 4, 5 }.Select(n => Lowest(k => Projection.Joint(testers, k, n)))) + " |");
            }
            md.AppendLine("#### The lowest week each criterion reaches 90%, with the session it implies");
            md.AppendLine();
            md.AppendLine("Minutes a week in the house: ceremonies at the suspenseful pace + the competitions played (duration and preview) + the NPC budget's free roam (a tick a second) + decisions at "
                + Projection.DecisionSeconds + " s each (until B8 measures them). E1's band is 30-45 minutes an episode.");
            md.AppendLine();
            md.AppendLine("| house | seasons | minutes a week | E4 any out, 3 | 4 | 5 | E3 all saw, 3 | 4 | 5 | joint, 3 | 4 | 5 |");
            md.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|");
            foreach (string line in summary) md.AppendLine(line);
            md.AppendLine();
            md.AppendLine("#### Where a week's minutes go");
            md.AppendLine();
            md.AppendLine("| house | ceremony s | competition s | free roam s | decisions | decision s | minutes |");
            md.AppendLine("|---|---|---|---|---|---|---|");
            foreach (var house in houses)
            {
                var parts = house.SelectMany(r => WeeksInTheHouse(r).Select(w => WeekParts(r, w))).ToList();
                if (parts.Count == 0) continue;
                md.AppendLine("| " + house.Key + " | " + BalanceLab.Num(parts.Average(p => p.ceremony), "0") + " | " + BalanceLab.Num(parts.Average(p => p.competition), "0") + " | "
                    + BalanceLab.Num(parts.Average(p => p.freeRoam), "0") + " | " + BalanceLab.Num(parts.Average(p => p.decisions), "0.0") + " | "
                    + BalanceLab.Num(parts.Average(p => p.decisions * Projection.DecisionSeconds), "0") + " | "
                    + BalanceLab.Num(parts.Average(p => Projection.WeekMinutes(p.ceremony, p.competition, p.freeRoam, p.decisions)), "0.0") + " |");
            }
            md.AppendLine();
            return md.ToString();
        }

        private static string Pct(double rate) => BalanceLab.Pct(rate);

        /// <summary>The weeks a season's player spent in the house: up to the week they went out, or every week.</summary>
        private static IEnumerable<int> WeeksInTheHouse(BalanceLab.SeasonRun r) => Enumerable.Range(1, Math.Max(0, r.autopsy.playerOutWeek > 0 ? r.autopsy.playerOutWeek : r.autopsy.weeks));

        private static (double ceremony, double competition, double freeRoam, int decisions) WeekParts(BalanceLab.SeasonRun r, int week) =>
            (r.ceremonyByWeek.TryGetValue(week, out double c) ? c : 0,
             Projection.CompetitionSeconds(r.seed, week, r.autopsy.competitionsPlayedByWeek.TryGetValue(week, out var phases) ? phases : null),
             r.cell.npcTicks, r.decisionsByWeek.TryGetValue(week, out int d) ? d : 0);

        /// <summary>Each week's mean minutes over the seasons whose player was in the house that week: (sum, count) by week.</summary>
        private static Dictionary<int, (double total, int n)> WeekMinutes(IEnumerable<BalanceLab.SeasonRun> runs)
        {
            var byWeek = new Dictionary<int, (double total, int n)>();
            foreach (var r in runs)
                foreach (int w in WeeksInTheHouse(r))
                {
                    var p = WeekParts(r, w);
                    var (total, n) = byWeek.TryGetValue(w, out var x) ? x : (0, 0);
                    byWeek[w] = (total + Projection.WeekMinutes(p.ceremony, p.competition, p.freeRoam, p.decisions), n + 1);
                }
            return byWeek;
        }

        /// <summary>A session's minutes to the end of week k: each week's mean over the testers still in it.</summary>
        private static double SessionMinutes(Dictionary<int, (double total, int n)> byWeek, int k) =>
            Enumerable.Range(1, k).Sum(w => byWeek.TryGetValue(w, out var x) && x.n > 0 ? x.total / x.n : 0);

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
                var passive = runs.Where(r => r.error == null && r.cell.policy == BalancePolicies.Passive && HouseOf(r.cell) == cell.Key.size).ToDictionary(r => r.index);
                var paired = list.Where(r => passive.ContainsKey(r.index)).ToList();
                var mc = BalanceLab.McNemar(paired.Select(r => r.Won).ToList(), paired.Select(r => passive[r.index].Won).ToList());
                md.AppendLine("| " + cell.Key.policy + " | " + cell.Key.size + " | " + n + " | " + wins + " | " + BalanceLab.Pct((double)wins / n) + " | "
                    + BalanceLab.Interval(BalanceLab.Wilson(wins, n)) + " | " + BalanceLab.Pct((double)list.Count(r => r.FinalTwo) / n) + " | "
                    + BalanceLab.Num(list.Average(r => r.autopsy.placement), "0.00") + " | "
                    + (cell.Key.policy == BalancePolicies.Passive ? "-" : mc.b + "/" + mc.c) + " | " + (cell.Key.policy == BalancePolicies.Passive ? "-" : BalanceLab.Num(mc.p, "0.000")) + " |");
            }
            md.AppendLine();
        }

        /// <summary>The pairs the baseline reads beside each policy against passive: first, then second.</summary>
        internal static readonly (string first, string second)[] Pairs =
        {
            (BalancePolicies.Reader, BalancePolicies.OracleReader), (BalancePolicies.Reader, BalancePolicies.OracleSkilled),
            (BalancePolicies.Social, BalancePolicies.Random), (BalancePolicies.Reader, BalancePolicies.Random), (BalancePolicies.Loyalist, BalancePolicies.Random),
            (BalancePolicies.Reader, BalancePolicies.Social), (BalancePolicies.Novice, BalancePolicies.Passive), (BalancePolicies.Exploit, BalancePolicies.Random),
        };

        private static void PairTable(StringBuilder md, IReadOnlyList<BalanceLab.SeasonRun> runs)
        {
            md.AppendLine("### Further pairs");
            md.AppendLine();
            md.AppendLine("McNemar on the same seasons: b where only the first won, c where only the second did.");
            md.AppendLine();
            md.AppendLine("| first | second | size | first wins | second wins | b/c | p |");
            md.AppendLine("|---|---|---|---|---|---|---|");
            foreach (string size in Houses(runs))
                foreach (var (first, second) in Pairs)
                {
                    Dictionary<int, bool> Wins(string policy) =>
                        runs.Where(r => r.error == null && r.cell.policy == policy && HouseOf(r.cell) == size).ToDictionary(r => r.index, r => r.Won);
                    var a = Wins(first);
                    var b = Wins(second);
                    var both = a.Keys.Where(b.ContainsKey).OrderBy(i => i).ToList();
                    if (both.Count == 0) continue;
                    var mc = BalanceLab.McNemar(both.Select(i => a[i]).ToList(), both.Select(i => b[i]).ToList());
                    md.AppendLine("| " + first + " | " + second + " | " + size + " | " + both.Count(i => a[i]) + " | " + both.Count(i => b[i]) + " | "
                        + mc.b + "/" + mc.c + " | " + BalanceLab.Num(mc.p, "0.000") + " |");
                }
            md.AppendLine();
        }

        /// <summary>The gated policies with a plan of their own: not the baselines (passive, random), the novice or the exploit hunter, nor the beast, whose edge is its performance.</summary>
        internal static readonly string[] Skilled = { BalancePolicies.Social, BalancePolicies.Reader, BalancePolicies.Schemer, BalancePolicies.Loyalist, BalancePolicies.Floater };

        /// <summary>The lead's proposed B-1 band: a knowledge-gated skilled policy wins at least 1.5x the base rate.</summary>
        private static void Band(StringBuilder md, IReadOnlyList<BalanceLab.SeasonRun> runs)
        {
            md.AppendLine("### The proposed B-1 band: a knowledge-gated skilled policy wins at least 1.5x the base rate");
            md.AppendLine();
            md.AppendLine("The bar is 1.5 / size. Clears: the win rate is at or over the bar; surely: the Wilson interval's low end is too.");
            md.AppendLine();
            md.AppendLine("| policy | size | bar | win rate [Wilson] | clears | surely |");
            md.AppendLine("|---|---|---|---|---|---|");
            foreach (var cell in Cells(runs).Where(c => Skilled.Contains(c.Key.policy)))
            {
                double bar = 1.5 / cell.First().cell.size;
                int n = cell.Count(), wins = cell.Count(r => r.Won);
                var ci = BalanceLab.Wilson(wins, n);
                double rate = (double)wins / n;
                md.AppendLine("| " + cell.Key.policy + " | " + cell.Key.size + " | " + BalanceLab.Pct(bar) + " | " + BalanceLab.Pct(rate) + " " + BalanceLab.Interval(ci) + " | "
                    + (rate >= bar ? "yes" : "no") + " | " + (ci.low >= bar ? "yes" : "no") + " |");
            }
            md.AppendLine();
        }

        private static void Survival(StringBuilder md, IReadOnlyList<BalanceLab.SeasonRun> runs)
        {
            md.AppendLine("### Survival by week: the player still in the house after week k");
            md.AppendLine();
            foreach (string size in Houses(runs))
            {
                int weeks = runs.Where(r => HouseOf(r.cell) == size && r.error == null).Select(r => r.autopsy.weeks).DefaultIfEmpty(0).Max();
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
            foreach (string size in Houses(runs))
            {
                var passive = runs.Where(r => r.error == null && HouseOf(r.cell) == size && r.cell.policy == BalancePolicies.Passive).ToList();
                var npcRows = passive.SelectMany(r => r.autopsy.houseguests.Where(h => !h.isPlayer)).ToList();
                double rho = BalanceLab.Spearman(npcRows.Select(h => h.statSum).ToList(), npcRows.Select(h => (double)(h.hohWins + h.vetoWins)).ToList());
                double top = Mean(passive.Select(r =>
                {
                    var wins = r.autopsy.houseguests.Where(h => !h.isPlayer).Select(h => h.hohWins + h.vetoWins).ToList();
                    return wins.Sum() == 0 ? 0 : (double)wins.Max() / wins.Sum();
                }));
                var weekly = runs.Where(r => r.error == null && HouseOf(r.cell) == size).SelectMany(r => r.autopsy.competitions.Where(c => c.phase == "HoH" || c.phase == "Veto")).ToList();
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

        /// <summary>
        /// The war rooms (D3, B6a): the plans the player's pacts of three made, how they settled, and the counter -
        /// those it could reach (who said the plan, off the block), their mean odds as the answer read them, who came
        /// round and the counters that carried; and the plans whose target that week's eviction took.
        /// </summary>
        internal static void WarRooms(StringBuilder md, IReadOnlyList<BalanceLab.SeasonRun> runs)
        {
            md.AppendLine("### War rooms (D3): plans, answers and the counter");
            md.AppendLine();
            md.AppendLine("Per policy and house, over all its seasons: seasons with a plan; plans; how they settled (agreed / countered / low / lapsed / void); the counter's reach - members who said the plan, off the block - with their mean come-round odds and how many came round; counters that carried; plans whose target was evicted that week; plans the player called. "
                + "The last column is the reach at every plan the player answered, a counter or not: the members a counter would have faced, and their mean odds.");
            md.AppendLine();
            md.AppendLine("| policy | size | seasons with a plan | plans | agreed / countered / low / lapsed / void | reachable | mean odds | came round | counters carried | target evicted | player called | would-be reach (mean odds) |");
            md.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|");
            foreach (var cell in Cells(runs))
            {
                var w = cell.Select(r => r.autopsy.warRooms).ToList();
                int Stance(string stance) => w.Sum(x => x.stances.TryGetValue(stance, out int n) ? n : 0);
                var all = cell.SelectMany(r => r.counterMembers).ToList();
                var reach = all.Where(m => m.stance == PactPlanStance.Countered).ToList();
                int plans = w.Sum(x => x.plans), counters = w.Sum(x => x.counters);
                md.AppendLine("| " + cell.Key.policy + " | " + cell.Key.size + " | " + w.Count(x => x.plans > 0) + " of " + w.Count + " | " + plans + " | "
                    + Stance(PactPlanStance.Agreed) + " / " + Stance(PactPlanStance.Countered) + " / " + Stance(PactPlanStance.Low) + " / " + Stance(PactPlanStance.Lapsed) + " / " + Stance(PactPlanStance.Void)
                    + " | " + reach.Count + " | " + (reach.Count == 0 ? "-" : BalanceLab.Num(reach.Average(m => m.odds), "0.00")) + " | " + reach.Count(m => m.cameRound)
                    + " | " + w.Sum(x => x.countersCarried) + " of " + counters + " | " + w.Sum(x => x.targetEvicted) + " of " + plans + " | " + w.Sum(x => x.playerCalls)
                    + " | " + all.Count + (all.Count == 0 ? "" : " (" + BalanceLab.Num(all.Average(m => m.odds), "0.00") + ")") + " |");
            }
            md.AppendLine();
        }

        // ---------------------------------------------------------------- the NPC budget tables (B5b)

        /// <summary>A season's measure for the sensitivity table: binary (McNemar against budget nought) or a number (paired bootstrap of the difference).</summary>
        internal sealed class Metric
        {
            public string name;
            public Func<BalanceLab.SeasonRun, bool> flag;
            public Func<BalanceLab.SeasonRun, double> value;
            public string format = "0.00";
        }

        private static double InHouseWeeks(BalanceLab.SeasonRun r) => r.autopsy.playerOutWeek > 0 ? r.autopsy.playerOutWeek : r.autopsy.weeks;

        /// <summary>Every metric of the sensitivity table, the headline's in short.</summary>
        internal static readonly Metric[] NpcMetrics =
        {
            new Metric { name = "win", flag = r => r.Won },
            new Metric { name = "final two", flag = r => r.FinalTwo },
            new Metric { name = "evicted in week 1", flag = r => r.autopsy.playerOutWeek == 1 },
            new Metric { name = "out by week 3", flag = r => r.autopsy.playerOutWeek > 0 && r.autopsy.playerOutWeek <= 3 },
            new Metric { name = "mean placement", value = r => r.autopsy.placement },
            new Metric { name = "weeks in the house", value = InHouseWeeks, format = "0.0" },
            new Metric { name = "player nominations per week in the house", value = r => r.autopsy.playerTimesNominated / Math.Max(1.0, InHouseWeeks(r)), format = "0.000" },
            new Metric { name = "player HoH and veto wins", value = r => r.autopsy.playerHohWins + r.autopsy.playerVetoWins },
            new Metric { name = "Game Sense", value = r => r.autopsy.gameSense, format = "0.0" },
            new Metric { name = "player pacts", value = r => r.autopsy.commitments.playerPacts },
            new Metric { name = "player deals", value = r => r.autopsy.commitments.playerDeals },
            new Metric { name = "NPC-only pacts", value = r => r.autopsy.commitments.npcPacts },
            new Metric { name = "NPC-only deals", value = r => r.autopsy.commitments.npcDeals },
            new Metric { name = "NPC-only deals broken", value = r => r.autopsy.commitments.npcDealsBroken },
            new Metric { name = "window seats spent", value = r => r.autopsy.economy.seatsSpent, format = "0.0" },
            new Metric { name = "war-room plans", value = r => r.autopsy.warRooms.plans },
            new Metric { name = "NPC HoH nominated the top threat (share of NPC nominations)", value = r => r.autopsy.agency.npcNominations == 0 ? 0 : (double)r.autopsy.agency.topThreatNominated / r.autopsy.agency.npcNominations },
            new Metric { name = "story: a pariah season", flag = r => r.pace != null && r.pace.pariah },
            new Metric { name = "warmest pair (mutual)", value = r => r.autopsy.warmest?.mutual ?? 0, format = "0" },
            new Metric { name = "coldest pair (mutual)", value = r => r.autopsy.coldest?.mutual ?? 0, format = "0" },
            new Metric { name = "houseguest pairs within ten of the bound (share)", value = r => r.pairs == 0 ? 0 : (double)r.pairsNearBound / r.pairs, format = "0.000" },
            new Metric { name = "NPC conversations started", value = r => r.npcStarts, format = "0" },
        };

        /// <summary>
        /// B5b's tables: the world's cost; every metric against the budget, each budget's seasons paired with budget
        /// nought's (the same seeds, decision 2) - McNemar's b/c and p for a flag, the mean difference and its
        /// bootstrap 95% interval for a number; and the house's saturation (risk 8).
        /// </summary>
        internal static string NpcBudget(IReadOnlyList<BalanceLab.SeasonRun> runs)
        {
            var md = new StringBuilder();
            md.Append(NpcCost(runs, double.NaN));
            var ok = runs.Where(r => r.error == null).ToList();
            var budgets = ok.Select(r => r.cell.npcTicks).Distinct().OrderBy(b => b).ToList();
            var groups = ok.GroupBy(r => (r.cell.policy, size: HouseOf(new BalanceLab.Cell { size = r.cell.size, roster = r.cell.roster })))
                .OrderBy(g => (int)g.First().cell.roster * 100 + g.First().cell.size).ThenBy(g => Array.IndexOf(BalancePolicies.All, g.Key.policy)).ToList();
            md.AppendLine("### Every metric against the NPC budget (ticks a week), paired with budget 0 on the same seasons");
            md.AppendLine();
            md.AppendLine("A flag shows its rate and McNemar's b/c (b: only at this budget, c: only at 0) and p; a number its mean and the mean difference from budget 0 with a bootstrap 95% interval. Budget 0 plays no NPC world.");
            md.AppendLine();
            foreach (var metric in NpcMetrics)
            {
                md.AppendLine("#### " + metric.name);
                md.AppendLine();
                md.AppendLine("| policy | size | " + string.Join(" | ", budgets.Select(b => b == 0 ? "0" : b + " (vs 0)")) + " |");
                md.AppendLine("|---|---|" + string.Concat(budgets.Select(_ => "---|")));
                foreach (var g in groups)
                {
                    var byBudget = budgets.ToDictionary(b => b, b => g.Where(r => r.cell.npcTicks == b).ToDictionary(r => r.index));
                    var cells = new List<string>();
                    foreach (int b in budgets)
                    {
                        var at = byBudget[b];
                        if (at.Count == 0) { cells.Add("-"); continue; }
                        string shown = metric.flag != null ? BalanceLab.Pct((double)at.Values.Count(metric.flag) / at.Count) : BalanceLab.Num(at.Values.Average(metric.value), metric.format);
                        if (b != 0 && byBudget.TryGetValue(0, out var nought) && nought.Count > 0)
                        {
                            var paired = at.Keys.Where(nought.ContainsKey).OrderBy(i => i).ToList();
                            if (metric.flag != null)
                            {
                                var mc = BalanceLab.McNemar(paired.Select(i => metric.flag(at[i])).ToList(), paired.Select(i => metric.flag(nought[i])).ToList());
                                shown += " (" + mc.b + "/" + mc.c + ", p " + BalanceLab.Num(mc.p, "0.000") + ")";
                            }
                            else
                            {
                                var diffs = paired.Select(i => metric.value(at[i]) - metric.value(nought[i])).ToList();
                                var ci = BalanceLab.Bootstrap(diffs);
                                shown += " (" + Signed(diffs.Count == 0 ? 0 : diffs.Average(), metric.format) + " [" + Signed(ci.low, metric.format) + ", " + Signed(ci.high, metric.format) + "]"
                                    + (ci.low > 0 || ci.high < 0 ? " *" : "") + ")";
                            }
                        }
                        cells.Add(shown);
                    }
                    md.AppendLine("| " + g.Key.policy + " | " + g.Key.size + " | " + string.Join(" | ", cells) + " |");
                }
                md.AppendLine();
            }
            md.AppendLine("A star marks an interval that excludes nought.");
            md.AppendLine();
            md.AppendLine("### Saturation (risk 8): the final house by size and budget");
            md.AppendLine();
            md.AppendLine("| size | budget | seasons | warmest pair, mean / max | coldest pair, mean / min | houseguest pairs within ten of ±200 | at ±200 |");
            md.AppendLine("|---|---|---|---|---|---|---|");
            foreach (var g in ok.GroupBy(r => (size: HouseOf(new BalanceLab.Cell { size = r.cell.size, roster = r.cell.roster }), r.cell.npcTicks)).OrderBy(g => g.Key.size, StringComparer.Ordinal).ThenBy(g => g.Key.npcTicks))
            {
                double pairs = Math.Max(1, g.Sum(r => r.pairs));
                md.AppendLine("| " + g.Key.size + " | " + g.Key.npcTicks + " | " + g.Count() + " | " + BalanceLab.Num(g.Average(r => r.autopsy.warmest?.mutual ?? 0), "0") + " / " + BalanceLab.Num(g.Max(r => r.autopsy.warmest?.mutual ?? 0), "0")
                    + " | " + BalanceLab.Num(g.Average(r => r.autopsy.coldest?.mutual ?? 0), "0") + " / " + BalanceLab.Num(g.Min(r => r.autopsy.coldest?.mutual ?? 0), "0")
                    + " | " + BalanceLab.Pct(g.Sum(r => r.pairsNearBound) / pairs) + " | " + BalanceLab.Pct(g.Sum(r => r.pairsAtBound) / pairs) + " |");
            }
            md.AppendLine();
            return md.ToString();
        }

        private static string Signed(double value, string format) => (value > 0 ? "+" : "") + BalanceLab.Num(value, format);

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
            foreach (string size in Houses(runs))
            {
                double G(string policy) => Mean(runs.Where(r => r.error == null && HouseOf(r.cell) == size && r.cell.policy == policy).Select(r => (double)r.autopsy.gameSense));
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
            foreach (string size in Houses(runs))
            {
                var list = runs.Where(r => r.error == null && HouseOf(r.cell) == size).ToList();
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

        /// <summary>
        /// The house itself (its agendas, its warmest and coldest pair), and the story's pace counted as the
        /// story pacing report counts it (<see cref="StoryPacingTests.Pace"/>), so the two read side by side;
        /// all policies' seasons.
        /// </summary>
        private static void House(StringBuilder md, IReadOnlyList<BalanceLab.SeasonRun> runs)
        {
            var sizes = Houses(runs);
            List<BalanceLab.SeasonRun> Of(string house) => runs.Where(r => r.error == null && HouseOf(r.cell) == house).ToList();

            md.AppendLine("### The house: agendas and pairs (all policies' seasons)");
            md.AppendLine();
            md.AppendLine("| house | NPC agendas as each social week closed | warmest pair (mutual, mean / max) | coldest pair (mutual, mean / min) |");
            md.AppendLine("|---|---|---|---|");
            foreach (var house in sizes)
            {
                var list = Of(house).Select(r => r.autopsy).ToList();
                if (list.Count == 0) continue;
                var agendas = list.SelectMany(a => a.agency.agendas).GroupBy(p => p.Key).Select(g => (kind: g.Key, total: g.Sum(p => p.Value))).OrderByDescending(x => x.total).ToList();
                double all = Math.Max(1, agendas.Sum(x => x.total));
                md.AppendLine("| " + house + " | " + string.Join(", ", agendas.Select(x => x.kind + " " + BalanceLab.Pct(x.total / all))) + " | "
                    + BalanceLab.Num(list.Average(a => a.warmest.mutual), "0") + " / " + BalanceLab.Num(list.Max(a => a.warmest.mutual), "0") + " | "
                    + BalanceLab.Num(list.Average(a => a.coldest.mutual), "0") + " / " + BalanceLab.Num(list.Min(a => a.coldest.mutual), "0") + " |");
            }
            md.AppendLine();

            md.AppendLine("### The story's pace (all policies' seasons), counted as StoryPacingTests.PacingReport counts it");
            md.AppendLine();
            md.AppendLine("Asks are the beats put to the player, a storyline's beats closing at one anchor one ask; the first night, summons and plays apart; "
                + "budgeted asks leave out production's must-fires and urgent moments. A pariah season has some houseguest (not the reigning Head of Household) "
                + "with three in the house at forty against them after two Advances running. The last column is the autopsy's own count, every story house event "
                + "in the final state, which is not comparable with the plan's targets.");
            md.AppendLine();
            md.AppendLine("| house | budgeted asks / season (target 4-6) | + must-fires | summons | play offers | weeks with a card (target 30-60%) | stories finished (target about 3) | moments | seasons with a pariah (target at most 20%) | NPC showmances | seasons with an NPC removal | seasons with a pile-on (target at most 25%) | story house events / season (autopsy) |");
            md.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|---|");
            foreach (var house in sizes)
            {
                var list = Of(house).Where(r => r.pace != null).ToList();
                if (list.Count == 0) continue;
                var paces = list.Select(r => r.pace).ToList();
                double weeks = Math.Max(1, paces.Sum(p => p.weeks));
                md.AppendLine("| " + house + " | " + BalanceLab.Num(paces.Average(p => p.budgetedAsks), "0.0") + " (max " + paces.Max(p => p.budgetedAsks) + ") | "
                    + BalanceLab.Num(paces.Average(p => p.asks - p.budgetedAsks), "0.0") + " | " + BalanceLab.Num(paces.Average(p => p.summons), "0.0") + " | "
                    + BalanceLab.Num(paces.Average(p => p.playOffers), "0.0") + " | " + BalanceLab.Pct(paces.Sum(p => p.weeksWithACard) / weeks) + " | "
                    + BalanceLab.Num(paces.Average(p => p.storiesFinished), "0.0") + " | " + BalanceLab.Num(paces.Average(p => p.arcsFinished - p.storiesFinished), "0.0") + " | "
                    + BalanceLab.Pct((double)paces.Count(p => p.pariah) / paces.Count) + " | " + BalanceLab.Num(paces.Average(p => p.showmances), "0.00") + " | "
                    + BalanceLab.Pct((double)paces.Count(p => p.npcRemovals > 0) / paces.Count) + " | " + BalanceLab.Pct((double)paces.Count(p => p.pileOns > 0) / paces.Count) + " | "
                    + BalanceLab.Num(list.Average(r => r.autopsy.story.asks), "0.0") + " |");
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
