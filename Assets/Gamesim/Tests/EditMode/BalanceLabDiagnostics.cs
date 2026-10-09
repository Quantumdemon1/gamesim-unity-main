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
            foreach (var (reachBase, fullView) in new[] { (PactPlans.ReachBase, PactPlans.ReachFullView) }.Concat(Candidates))
            {
                double cap = reachBase * PactPlans.ReachLoyal;
                bool today = reachBase == PactPlans.ReachBase && fullView == PactPlans.ReachFullView;
                double Mean(List<BalanceLab.CounterMember> list) => list.Count == 0 ? double.NaN : list.Average(m => Odds(m, reachBase, fullView, PactPlans.ReachLoyal, cap));
                md.AppendLine("| " + Num(reachBase) + (today ? " (today)" : "") + " | " + Num(fullView) + " | " + Num(cap) + " | " + Num(Mean(all), "0.00") + " | " + Num(Mean(countered), "0.00") + " | "
                    + BalanceLab.Pct((double)all.Count(m => Odds(m, reachBase, fullView, PactPlans.ReachLoyal, cap) > 0) / all.Count) + " | "
                    + Num(all.Where(m => !m.lapsed).Sum(m => Odds(m, reachBase, fullView, PactPlans.ReachLoyal, cap)) / all.Count, "0.00") + " |");
            }
            md.AppendLine();
            return md.ToString();
        }

        // ---------------------------------------------------------------- Q2: Game Sense's gap

        /// <summary>A Game Sense formula: the faces' weights, whether a face is held to 0-100, and a scale on the strategy face's notes.</summary>
        internal sealed class SenseFormula
        {
            public string name;
            public double competitions = GameSense.CompetitionWeight, strategy = GameSense.StrategyWeight, social = GameSense.SocialWeight, strategyScale = 1;
            public bool clamp = true;

            /// <summary>A season's score under this formula, from its notes summed by face and row kind (as <see cref="GameSense.Evaluate"/> scores them).</summary>
            public int Score(IReadOnlyDictionary<string, double> rows)
            {
                double Face(string face, double scale)
                {
                    double raw = GameSense.Base + scale * rows.Where(p => p.Key.StartsWith(face + "/", StringComparison.Ordinal)).Sum(p => p.Value);
                    return (int)Math.Round(clamp ? Math.Max(0, Math.Min(100, raw)) : raw);
                }
                return (int)Math.Round(Face(GameSense.Competitions, 1) * competitions + Face(GameSense.Strategy, strategyScale) * strategy + Face(GameSense.Social, 1) * social);
            }
        }

        internal static readonly SenseFormula[] SenseFormulas =
        {
            new SenseFormula { name = "today (0.3 / 0.4 / 0.3, faces 0-100)" },
            new SenseFormula { name = "faces unbounded", clamp = false },
            new SenseFormula { name = "weights 0.2 / 0.6 / 0.2", competitions = 0.2, strategy = 0.6, social = 0.2 },
            new SenseFormula { name = "strategy notes x1.5", strategyScale = 1.5 },
            new SenseFormula { name = "weights 0.2 / 0.6 / 0.2, strategy notes x1.5", competitions = 0.2, strategy = 0.6, social = 0.2, strategyScale = 1.5 },
            new SenseFormula { name = "weights 0 / 0.7 / 0.3 (no competitions face)", competitions = 0, strategy = 0.7, social = 0.3 },
        };

        /// <summary>
        /// Q2 (Game Sense's gap, target re-set to at least 10 at both sizes with a bootstrap low end over 5): the gated
        /// reader against the random player on the same seasons - today's gap and the gap under candidate formulas,
        /// replayed from each season's notes (Game Sense reads only the final state, so no season is replayed) - and
        /// where today's gap comes from, by face and ledger row kind.
        /// </summary>
        internal static string GameSenseWhatIf(IReadOnlyList<BalanceLab.SeasonRun> runs)
        {
            var md = new StringBuilder();
            var ok = runs.Where(r => r.error == null && r.gameSenseRows.Count > 0).ToList();
            md.AppendLine("### Q2 what-if: Game Sense's gap, the gated reader against random on the same seasons (T0)");
            md.AppendLine();
            if (ok.Count == 0) { md.AppendLine("No seasons with Game Sense's notes."); md.AppendLine(); return md.ToString(); }
            int drift = ok.Max(r => Math.Abs(SenseFormulas[0].Score(r.gameSenseRows) - r.autopsy.gameSense));
            md.AppendLine("Today's formula replayed from the notes differs from the autopsy's score by at most " + drift + ". The interval is a bootstrap 95% of the paired difference.");
            md.AppendLine();
            var houses = ok.GroupBy(r => BalanceLabReports.HouseOf(r.cell)).OrderBy(g => g.First().cell.size).ToList();
            foreach (var (first, second) in new[] { (BalancePolicies.Reader, BalancePolicies.Random), (BalancePolicies.OracleReader, BalancePolicies.Random) })
            {
                md.AppendLine("| formula (" + first + " minus " + second + ") | " + string.Join(" | ", houses.Select(h => "size " + h.Key)) + " |");
                md.AppendLine("|---|" + string.Concat(houses.Select(_ => "---|")));
                foreach (var formula in SenseFormulas)
                {
                    var cells = new List<string>();
                    foreach (var house in houses)
                    {
                        var a = house.Where(r => r.cell.policy == first).ToDictionary(r => r.index);
                        var b = house.Where(r => r.cell.policy == second).ToDictionary(r => r.index);
                        var diffs = a.Keys.Where(b.ContainsKey).Select(i => (double)(formula.Score(a[i].gameSenseRows) - formula.Score(b[i].gameSenseRows))).ToList();
                        if (diffs.Count == 0) { cells.Add("-"); continue; }
                        var ci = BalanceLab.Bootstrap(diffs);
                        cells.Add(Num(diffs.Average(), "0.0") + " [" + Num(ci.low, "0.0") + ", " + Num(ci.high, "0.0") + "]" + (diffs.Average() >= 10 && ci.low > 5 ? " meets" : ""));
                    }
                    md.AppendLine("| " + formula.name + " | " + string.Join(" | ", cells) + " |");
                }
                md.AppendLine();
            }
            md.AppendLine("\"meets\": a gap of at least 10 with the interval's low end over 5 (the lead's decision 8).");
            md.AppendLine();
            md.AppendLine("Where today's reader-minus-random gap comes from: the mean points a season, by face and row kind (before each face is held to 0-100).");
            md.AppendLine();
            var kinds = ok.SelectMany(r => r.gameSenseRows.Keys).Distinct().OrderBy(k => k, StringComparer.Ordinal).ToList();
            md.AppendLine("| face/row kind | " + string.Join(" | ", houses.Select(h => "reader " + h.Key + " | random " + h.Key + " | gap " + h.Key)) + " |");
            md.AppendLine("|---|" + string.Concat(houses.Select(_ => "---|---|---|")));
            foreach (string kind in kinds)
            {
                var cells = new List<string>();
                foreach (var house in houses)
                {
                    double Mean(string policy) { var list = house.Where(r => r.cell.policy == policy).ToList(); return list.Count == 0 ? double.NaN : list.Average(r => r.gameSenseRows.TryGetValue(kind, out double v) ? v : 0); }
                    double reader = Mean(BalancePolicies.Reader), random = Mean(BalancePolicies.Random);
                    cells.Add(Num(reader, "0.0") + " | " + Num(random, "0.0") + " | " + Num(reader - random, "0.0"));
                }
                md.AppendLine("| " + kind + " | " + string.Join(" | ", cells) + " |");
            }
            md.AppendLine();
            return md.ToString();
        }

        // ---------------------------------------------------------------- Q3: preparation

        /// <summary>
        /// Q3 (competitions dominate): the studier - passive, studying the house until prepared - against the plain
        /// passive player at the same fixed performance, on the same seasons: weekly HoH and veto wins, season wins
        /// (McNemar), and the preparation reached.
        /// </summary>
        internal static string Preparation(IReadOnlyList<BalanceLab.SeasonRun> runs)
        {
            var md = new StringBuilder();
            md.AppendLine("### Q3: what preparation is worth - the studier against passive at a fixed performance (T0)");
            md.AppendLine();
            md.AppendLine("| size | performance | player | seasons | preparation at the end | weekly comps won [Wilson] | season wins [Wilson] | studier vs passive b/c, p |");
            md.AppendLine("|---|---|---|---|---|---|---|---|");
            foreach (var g in runs.Where(r => r.error == null).GroupBy(r => (r.cell.size, r.cell.performance)).OrderBy(g => g.Key.size).ThenBy(g => g.Key.performance, StringComparer.Ordinal))
            {
                var passive = g.Where(r => r.cell.policy == BalancePolicies.Passive).ToDictionary(r => r.index);
                foreach (var p in g.GroupBy(r => r.cell.policy).OrderBy(p => p.Key == BalancePolicies.Passive ? 0 : 1))
                {
                    var list = p.ToList();
                    var comps = list.SelectMany(r => r.autopsy.competitions.Where(c => c.playerInField && (c.phase == "HoH" || c.phase == "Veto"))).ToList();
                    int won = comps.Count(c => c.winnerIsPlayer), wins = list.Count(r => r.Won);
                    string pair = "-";
                    if (p.Key != BalancePolicies.Passive)
                    {
                        var both = list.Where(r => passive.ContainsKey(r.index)).OrderBy(r => r.index).ToList();
                        var mc = BalanceLab.McNemar(both.Select(r => r.Won).ToList(), both.Select(r => passive[r.index].Won).ToList());
                        pair = mc.b + "/" + mc.c + ", p " + Num(mc.p, "0.000");
                    }
                    md.AppendLine("| " + g.Key.size + " | " + g.Key.performance.Replace("fixed-", "") + " | " + p.Key + " | " + list.Count + " | " + Num(list.Average(r => r.preparation), "0.0")
                        + " | " + BalanceLab.Pct(comps.Count == 0 ? 0 : (double)won / comps.Count) + " " + BalanceLab.Interval(BalanceLab.Wilson(won, comps.Count))
                        + " | " + BalanceLab.Pct((double)wins / list.Count) + " " + BalanceLab.Interval(BalanceLab.Wilson(wins, list.Count)) + " | " + pair + " |");
                }
            }
            md.AppendLine();
            return md.ToString();
        }

        // ---------------------------------------------------------------- Q4: who an NPC Head of Household puts up

        /// <summary>
        /// Q4 (the passive newcomer is nominated more): at every NPC Head of Household's nominations while the player was
        /// in the house, the nomination weight (<see cref="EpisodeEngine.NominationWeight"/>, lower is put up first) split
        /// into its terms, for the player and for the mean other candidate; the term whose difference is most negative is
        /// what puts the player up.
        /// </summary>
        internal static string NominationDecomposition(IReadOnlyList<BalanceLab.SeasonRun> runs)
        {
            var md = new StringBuilder();
            md.AppendLine("### Q4: an NPC Head of Household's nominations, the weight in its terms - the player against the mean other candidate (T0)");
            md.AppendLine();
            md.AppendLine("Each term as it enters the weight (lower is put up first): the strategy windows' reluctance, the story's preference, minus the HoH's view, minus the overlapping protection, minus the threat. Player minus others; negative means the term pushes the player up.");
            md.AppendLine();
            md.AppendLine("| policy | house | NPC nominations | player put up | mean rank (1 first) of candidates | reluctance | story | view | protection | threat | total |");
            md.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|");
            foreach (var cell in BalanceLabReports.Cells(runs))
            {
                var records = cell.SelectMany(r => r.nominations).ToList();
                if (records.Count == 0) continue;
                string D(Func<BalanceLab.NominationTerms, double> term) => Num(records.Average(x => term(x.player)), "0.0") + " - " + Num(records.Average(x => term(x.others)), "0.0")
                    + " = " + Num(records.Average(x => term(x.player) - term(x.others)), "0.0");
                md.AppendLine("| " + cell.Key.policy + " | " + cell.Key.size + " | " + records.Count + " | " + BalanceLab.Pct((double)records.Count(x => x.playerNominated) / records.Count)
                    + " | " + Num(records.Average(x => x.playerRank), "0.0") + " of " + Num(records.Average(x => x.candidates), "0.0") + " | " + D(t => t.reluctance) + " | " + D(t => t.story)
                    + " | " + D(t => t.view) + " | " + D(t => t.protection) + " | " + D(t => t.threat) + " | " + D(t => t.total) + " |");
            }
            md.AppendLine();
            return md.ToString();
        }

        // ---------------------------------------------------------------- Q5: the pariah

        /// <summary>
        /// Q5 (story pariah rate): every season's first pariah run as the story pacing report counts one, by house - the
        /// holders' grudge causes, the target's competition record against the house's strongest, the week and the
        /// house's size then.
        /// </summary>
        internal static string PariahDecomposition(IReadOnlyList<BalanceLab.SeasonRun> runs)
        {
            var md = new StringBuilder();
            md.AppendLine("### Q5: the pariah runs decomposed (T0)");
            md.AppendLine();
            md.AppendLine("| house | seasons | with a pariah | mean week | in the house then | holders | target's HoH wins (house's most) | target won a HoH | target won two | causes of the holders' grudges |");
            md.AppendLine("|---|---|---|---|---|---|---|---|---|---|");
            foreach (var house in runs.Where(r => r.error == null).GroupBy(r => BalanceLabReports.HouseOf(r.cell)).OrderBy(g => (int)g.First().cell.roster * 1000000 + g.First().cell.size * 10000 + g.First().cell.npcTicks))
            {
                var list = house.ToList();
                var p = list.Where(r => r.pariah != null).Select(r => r.pariah).ToList();
                if (p.Count == 0) { md.AppendLine("| " + house.Key + " | " + list.Count + " | 0 | - | - | - | - | - | - | - |"); continue; }
                var causes = p.SelectMany(x => x.causes).GroupBy(c => c).Select(g => (cause: g.Key, n: g.Count())).OrderByDescending(x => x.n).ToList();
                double all = causes.Sum(x => x.n);
                md.AppendLine("| " + house.Key + " | " + list.Count + " | " + BalanceLab.Pct((double)p.Count / list.Count) + " | " + Num(p.Average(x => x.week), "0.0") + " | " + Num(p.Average(x => x.houseCount), "0.0")
                    + " | " + Num(p.Average(x => x.holders), "0.0") + " | " + Num(p.Average(x => x.targetHohWins), "0.00") + " (" + Num(p.Average(x => x.npcHohWinsMax), "0.00") + ") | "
                    + BalanceLab.Pct((double)p.Count(x => x.targetHohWins >= 1) / p.Count) + " | " + BalanceLab.Pct((double)p.Count(x => x.targetHohWins >= 2) / p.Count) + " | "
                    + string.Join(", ", causes.Select(x => (x.cause.Length == 0 ? "?" : x.cause) + " " + BalanceLab.Pct(x.n / all))) + " |");
            }
            md.AppendLine();
            return md.ToString();
        }

        private static string Num(double value, string format = "0.#") => BalanceLab.Num(value, format);
    }

    /// <summary>
    /// The human-acceptance projections (BALANCE plan B7): from lab seasons of the novice, the chance three (or four,
    /// five) testers in a session see what PLAYTEST_PROTOCOL's E3 and E4 need by week k, and how long that session is.
    /// Pure arithmetic over each season's out week and first commitment settled; the tests hold it.
    /// </summary>
    internal static class Projection
    {
        /// <summary>One tester's season, as the projection reads it: the week they went out (0 never) and the first week they saw a commitment of theirs settle (0 never).</summary>
        internal struct Tester
        {
            public int outWeek, firstSettledWeek;
            public Tester(int outWeek, int firstSettledWeek) { this.outWeek = outWeek; this.firstSettledWeek = firstSettledWeek; }
            /// <summary>Out of the house by week k (E4 has its loser).</summary>
            public bool Out(int k) => outWeek > 0 && outWeek <= k;
            /// <summary>Saw a commitment settle while still in the house, by week k (E3's promise or betrayal).</summary>
            public bool Saw(int k) => firstSettledWeek > 0 && firstSettledWeek <= k && (outWeek == 0 || firstSettledWeek <= outWeek);
        }

        /// <summary>S(k): the chance a tester is out by week k.</summary>
        internal static double S(IReadOnlyList<Tester> t, int k) => t.Count == 0 ? 0 : (double)t.Count(x => x.Out(k)) / t.Count;

        /// <summary>C(k): the chance a tester saw a commitment settle while in the house, by week k.</summary>
        internal static double C(IReadOnlyList<Tester> t, int k) => t.Count == 0 ? 0 : (double)t.Count(x => x.Saw(k)) / t.Count;

        /// <summary>E4 alone: at least one of n independent testers out by week k, 1 − (1 − S)ⁿ.</summary>
        internal static double AnyOut(IReadOnlyList<Tester> t, int k, int n) => 1 - Math.Pow(1 - S(t, k), n);

        /// <summary>E3 alone: every one of n testers saw a commitment settle by week k, Cⁿ.</summary>
        internal static double AllSaw(IReadOnlyList<Tester> t, int k, int n) => Math.Pow(C(t, k), n);

        /// <summary>
        /// Both: every tester saw a commitment settle and at least one is out, P = P(A)ⁿ − P(A ∧ ¬B)ⁿ for independent
        /// testers, from each season's joint indicators (A saw, B out).
        /// </summary>
        internal static double Joint(IReadOnlyList<Tester> t, int k, int n)
        {
            if (t.Count == 0) return 0;
            double a = (double)t.Count(x => x.Saw(k)) / t.Count, aNotB = (double)t.Count(x => x.Saw(k) && !x.Out(k)) / t.Count;
            return Math.Pow(a, n) - Math.Pow(aNotB, n);
        }

        /// <summary>The smallest week k (1..max) at which <paramref name="p"/> reaches the target, or 0 where none does.</summary>
        internal static int LowestWeek(Func<int, double> p, double target, int max)
        {
            for (int k = 1; k <= max; k++) if (p(k) >= target - 1e-12) return k;
            return 0;
        }

        /// <summary>The seconds a decision is taken to cost until B8 measures it.</summary>
        internal const double DecisionSeconds = 30;

        /// <summary>
        /// A tester's minutes in one week in the house: the ceremonies (the lab's, at the suspenseful pace), the
        /// competitions they played (each one's duration and preview), the NPC free roam (the budget's ticks, a second
        /// each) and their decisions at <see cref="DecisionSeconds"/> each.
        /// </summary>
        internal static double WeekMinutes(double ceremonySeconds, double competitionSeconds, double freeRoamTicks, int decisions) =>
            (ceremonySeconds + competitionSeconds + freeRoamTicks + decisions * DecisionSeconds) / 60;

        /// <summary>The competitions' seconds of a week: each played competition's duration and preview (<see cref="CompetitionDefinitions.Version4"/>).</summary>
        internal static double CompetitionSeconds(uint seed, int week, IEnumerable<string> phases) =>
            (phases ?? Enumerable.Empty<string>()).Select(p => (EpisodePhase)Enum.Parse(typeof(EpisodePhase), p)).Select(p => CompetitionDefinitions.Version4(seed, week, p))
                .Where(d => d != null).Sum(d => d.Duration + d.PreviewSeconds);
    }
}
#endif
