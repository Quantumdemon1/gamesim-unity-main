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
    /// houses of 8 and 12, alumni rules 1 (the shipped <see cref="ShippedRules.ApplyFresh"/>, held at 1 since fresh seasons
    /// start at <see cref="AlumniRules.Latest"/>, §14.H: the lab's <c>alumniHeld</c>, so the gate goes on reproducing H1b's
    /// evidence, its house names ending "/alumni1") against the paired control held at 0 (the lab's <c>alumniOff</c>, the
    /// same seed), BALANCE_ALUMNI_SEEDS seasons a cell (100).
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

        /// <summary>The three arms of a cell: the histories held at alumni rules 1, the histories alone left out, and both opening states left out.</summary>
        private static readonly (bool alumniOff, bool impressionsOff, int held)[] Arms = { (false, false, AlumniRules.Histories), (true, false, 0), (true, true, 0) };

        /// <summary>Every cell, each with its two controls: the newcomer at each policy and size, and the passive player on Cody's card.</summary>
        internal static List<BalanceLab.Cell> Cells()
        {
            var cells = new List<BalanceLab.Cell>();
            foreach (int size in Sizes)
            {
                foreach (string policy in GatePolicies.Concat(ReportPolicies))
                    foreach (var arm in Arms)
                        cells.Add(new BalanceLab.Cell { policy = policy, size = size, roster = CastTemplates.Roster.AllStars, alumniOff = arm.alumniOff, impressionsOff = arm.impressionsOff, alumniHeld = arm.held });
                foreach (var arm in Arms)
                    cells.Add(new BalanceLab.Cell { policy = BalancePolicies.Passive, size = size, roster = CastTemplates.Roster.AllStars, playerTemplate = Card, alumniOff = arm.alumniOff, impressionsOff = arm.impressionsOff, alumniHeld = arm.held });
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
            md.AppendLine("Against: \"histories alone\" is the shipped season, held at alumni rules 1, against the same season without the histories (first impressions in both arms; the gate). "
                + "\"both together\" is the same against the same season with neither the histories nor agency's first impressions (reported, not gated).");
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

        // ================================================================ 14.H: loyalty, reputation and known blocs

        /// <summary>
        /// The report's arms (ALL-STAR-HISTORIES-PLAN 14.H, 14.L.H), the shipped value first: 5 (<see cref="AlumniRules.Latest"/>,
        /// the cell left unheld), 4, 3, 2 and 1 held as a harness holds one (the lab's <c>alumniHeld</c>, 14.G), and 0, the
        /// season without the histories (the lab's <c>alumniOff</c>, H1b's control: a held 0 would keep the opening's receipts).
        /// </summary>
        internal static readonly int[] ReportArms =
            { AlumniRules.GrowingPacts, AlumniRules.CounterBlocs, AlumniRules.Reputation, AlumniRules.Loyalty, AlumniRules.Histories, AlumniRules.Off };

        /// <summary>14.H's paired comparisons, the later arm first: each value against the one below, and growth and the counter-blocs against no histories.</summary>
        internal static readonly (int later, int earlier)[] Comparisons = { (2, 1), (3, 2), (4, 3), (5, 4), (4, 0), (5, 0) };

        /// <summary>The four players of a size (14.H): the newcomer as passive, studier and social, and the passive player on Cody's card.</summary>
        internal static readonly (string policy, string card)[] ReportPlayers =
            { (BalancePolicies.Passive, null), (BalancePolicies.Studier, null), (BalancePolicies.Social, null), (BalancePolicies.Passive, Card) };

        /// <summary>The story session's budgets stop condition 1 reads (NPC-AGENCY-PLAN constraint 1): pile-on seasons at most 25%; pariah-free seasons at least 80%, and the twelve-house's 45% not lower.</summary>
        internal const double PileOnBudget = 0.25, PariahFreeBudget = 0.80, PariahFreeBudgetTwelve = 0.45;

        /// <summary>The near-zero rule's line (14.L.H): fewer than one season in ten with a join in an 8-house cell at 5.</summary>
        internal const double NearZeroLine = 0.10;

        /// <summary>One cell of the report: an All-Stars house, a player, an arm; its seed is the shipped cell's, so arms pair.</summary>
        internal static BalanceLab.Cell ReportCell(string policy, int size, string card, int arm) => new BalanceLab.Cell
        {
            policy = policy, size = size, roster = CastTemplates.Roster.AllStars, playerTemplate = card,
            alumniOff = arm == AlumniRules.Off, alumniHeld = arm == AlumniRules.Off || arm == AlumniRules.Latest ? 0 : arm,
        };

        /// <summary>The arm a report cell plays.</summary>
        internal static int ArmOf(BalanceLab.Cell cell) => cell.alumniOff ? AlumniRules.Off : cell.alumniHeld == 0 ? AlumniRules.Latest : cell.alumniHeld;

        /// <summary>Every cell, size by size, player by player, arm by arm: 2 × 4 × 6 = 48.</summary>
        internal static List<BalanceLab.Cell> ReportCells() =>
            (from size in Sizes from player in ReportPlayers from arm in ReportArms select ReportCell(player.policy, size, player.card, arm)).ToList();

        [Test, Explicit("14.H's report: All-Stars 8 and 12, four players, alumni rules 5 (shipped) against held 4, 3, 2, 1 and no histories (0) on the same "
            + "BALANCE_ALUMNI_SEEDS seasons (100: 4,800 seasons, about 36 minutes on six cores), with 14.L.H's pact watch, written to BALANCE_ALUMNI_OUT. "
            + "Not a gate. Run by name, alone, never beside a Unity run.")]
        public void LoyaltyReputationBlocsReport()
        {
            int seeds = BalanceLabReports.FromEnvironment("BALANCE_ALUMNI_SEEDS", 100);
            int threads = BalanceLabReports.FromEnvironment("BALANCE_ALUMNI_THREADS", Environment.ProcessorCount);
            string folder = Environment.GetEnvironmentVariable("BALANCE_ALUMNI_OUT");
            if (string.IsNullOrEmpty(folder)) folder = Path.Combine(TestContext.CurrentContext.WorkDirectory, "balance");
            Directory.CreateDirectory(folder);
            var cells = ReportCells();
            // The shipped arm is the latest value, and every held arm enrols at its own value.
            Assert.That(ReportArms[0], Is.EqualTo(AlumniRules.Latest), "The report's first arm is the shipped value.");
            var started = DateTime.Now;
            var clock = BalanceLab.Clock();
            var runs = BalanceLab.Run(cells, seeds, threads);
            double minutes = clock.Elapsed.TotalMinutes;
            var meta = new AlumniReport.Meta
            {
                seeds = seeds, threads = threads, minutes = minutes, commit = SourceCommit(), started = started.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                command = "BALANCE_ALUMNI_SEEDS=" + seeds.ToString(CultureInfo.InvariantCulture) + " BALANCE_ALUMNI_THREADS=" + threads.ToString(CultureInfo.InvariantCulture)
                    + " BALANCE_ALUMNI_OUT=\"" + folder + "\" dotnet test Tools/SimulationTests/SimulationTests.csproj --nologo --filter \"FullyQualifiedName="
                    + typeof(BalanceLabAlumniReport).FullName + "." + nameof(LoyaltyReputationBlocsReport) + "\"",
                host = Environment.CommandLine.Length > 400 ? Environment.CommandLine.Substring(0, 400) + " ..." : Environment.CommandLine,
            };
            var built = AlumniReport.Build(cells, runs, meta);
            string stem = Path.Combine(folder, "alumni-14h-report");
            File.WriteAllText(stem + ".md", built.text, new UTF8Encoding(false));
            File.WriteAllText(stem + ".json", built.json, new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(folder, "alumni-14h-seasons.csv"), built.csv, new UTF8Encoding(false));
            TestContext.WriteLine("Written: " + stem + ".md, .json and " + Path.Combine(folder, "alumni-14h-seasons.csv"));
            TestContext.WriteLine(built.summary);
            // Not a gate: nothing about a crossing is asserted. A season that could not be played is the lab's failure.
            Assert.That(runs.Where(r => r.error != null).Select(r => r.cell.Key + " #" + r.index + ": " + r.error).Take(10), Is.Empty);
        }

        /// <summary>
        /// The commit the report ran from: BALANCE_COMMIT when set, else the source tree's HEAD, read from its .git (a worktree's
        /// pointer and the common directory followed, loose refs then packed-refs). Changes not yet committed are not shown.
        /// </summary>
        internal static string SourceCommit()
        {
            string set = Environment.GetEnvironmentVariable("BALANCE_COMMIT");
            if (!string.IsNullOrEmpty(set)) return set.Trim();
            try
            {
                string at = Path.GetDirectoryName(typeof(BalanceLabAlumniReport).Assembly.Location);
                for (int depth = 0; depth < 12 && !string.IsNullOrEmpty(at); depth++, at = Path.GetDirectoryName(at))
                {
                    string dotGit = Path.Combine(at, ".git");
                    string gitDir = Directory.Exists(dotGit) ? dotGit : File.Exists(dotGit) ? GitDirOf(at, File.ReadAllText(dotGit)) : null;
                    if (gitDir == null) continue;
                    string head = File.ReadAllText(Path.Combine(gitDir, "HEAD")).Trim();
                    if (!head.StartsWith("ref: ", StringComparison.Ordinal)) return head;
                    string reference = head.Substring(5).Trim();
                    string commonFile = Path.Combine(gitDir, "commondir");
                    string common = File.Exists(commonFile) ? Path.GetFullPath(Path.Combine(gitDir, File.ReadAllText(commonFile).Trim())) : gitDir;
                    foreach (string dir in new[] { gitDir, common })
                    {
                        string loose = Path.Combine(dir, reference.Replace('/', Path.DirectorySeparatorChar));
                        if (File.Exists(loose)) return File.ReadAllText(loose).Trim() + " (" + reference + ")";
                    }
                    string packed = Path.Combine(common, "packed-refs");
                    if (File.Exists(packed))
                        foreach (string line in File.ReadAllLines(packed))
                            if (line.EndsWith(" " + reference, StringComparison.Ordinal)) return line.Substring(0, line.IndexOf(' ')) + " (" + reference + ")";
                    return "unknown (" + reference + ")";
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            return "unknown";
        }

        private static string GitDirOf(string root, string text)
        {
            text = text.Trim();
            if (!text.StartsWith("gitdir:", StringComparison.Ordinal)) return null;
            string path = text.Substring(7).Trim();
            return Path.IsPathRooted(path) ? path : Path.GetFullPath(Path.Combine(root, path));
        }
    }

    /// <summary>
    /// 14.H's report from the lab's seasons (ALL-STAR-HISTORIES-PLAN 14.H, 14.L.H): every row a cell at every arm, the paired
    /// comparisons with their 95% intervals, the pact watch's rows and tables, the must-be-zero rows, the near-zero rule and
    /// both stop conditions, as plain text for the owner, JSON for a reader, and one CSV line a season. It decides nothing:
    /// a crossing is reported, never asserted, and a must-be-zero row that is not zero is a bug line at the top.
    /// </summary>
    internal static class AlumniReport
    {
        internal sealed class Meta
        {
            public int seeds, threads;
            public double minutes;
            public string commit, command, host, started;
        }

        /// <summary>One row of the report: a per-season value (null where the season has none), a rate or a mean.</summary>
        internal sealed class Metric
        {
            public string name, section, format = "0.000";
            public bool rate, compared = true;
            public Func<BalanceLab.SeasonRun, double?> of;
        }

        private const double Z = 1.959963984540054;

        private static double? Flag(bool value) => value ? 1 : 0;
        private static BalanceLab.PactRecord W(BalanceLab.SeasonRun r) => r.pactWatch;
        private static bool Ok(BalanceLab.SeasonRun r) => r.error == null && r.autopsy != null && r.pactWatch != null;

        /// <summary>When each known-pair member left the house (0 a finalist), from the autopsy.</summary>
        private static List<int> MemberOutWeeks(BalanceLab.SeasonRun r) =>
            r.autopsy.houseguests.Where(h => W(r).members.Contains(h.id)).Select(h => h.outWeek).ToList();

        private static string FirstEvictee(BalanceLab.SeasonRun r) =>
            r.autopsy.weeksPlayed.Where(w => w.evicteeId != null).OrderBy(w => w.week).FirstOrDefault()?.evicteeId;

        private static double? Q4(BalanceLab.SeasonRun r) =>
            r.nominations.Count == 0 ? (double?)null : r.nominations.Count(n => n.playerNominated) / (double)r.nominations.Count;

        private static double? WeeklyWins(BalanceLab.SeasonRun r)
        {
            var played = r.autopsy.competitions.Where(c => c.playerInField && (c.phase == "HoH" || c.phase == "Veto")).ToList();
            return played.Count == 0 ? (double?)null : played.Count(c => c.winnerIsPlayer) / (double)played.Count;
        }

        /// <summary>Every row of 14.H and 14.L.H, in the report's order.</summary>
        internal static List<Metric> Metrics()
        {
            var m = new List<Metric>();
            void AddRate(string section, string name, Func<BalanceLab.SeasonRun, double?> of) => m.Add(new Metric { section = section, name = name, rate = true, of = r => Ok(r) ? of(r) : null });
            void AddMean(string section, string name, string format, Func<BalanceLab.SeasonRun, double?> of, bool compared = true) =>
                m.Add(new Metric { section = section, name = name, format = format, compared = compared, of = r => Ok(r) ? of(r) : null });
            const string Head = "Headline rates (section 9)", Pairs = "The known pairs (14.H)", Pace = "Pacing (14.H)", Watch = "The pact watch (14.L.H)";
            AddRate(Head, "Q3 season wins", r => Flag(r.Won));
            AddMean(Head, "Q4 nomination rate (NPC HoH)", "0.000", Q4);
            AddMean(Head, "weekly competition win rate", "0.000", WeeklyWins);
            AddMean(Pairs, "known-pair member put up by an NPC HoH, per member-week", "0.000", r => W(r).memberWeeks == 0 ? (double?)null : W(r).memberPutUps / (double)W(r).memberWeeks);
            AddRate(Pairs, "first evictee a known-pair member", r => W(r).members.Count == 0 || FirstEvictee(r) == null ? (double?)null : Flag(W(r).members.Contains(FirstEvictee(r))));
            AddRate(Pairs, "a known-pair member out by week 2", r => W(r).members.Count == 0 ? (double?)null : Flag(MemberOutWeeks(r).Any(w => w >= 1 && w <= 2)));
            AddMean(Pairs, "week the first known-pair member leaves", "0.00", r => MemberOutWeeks(r).Where(w => w > 0).Select(w => (double?)w).DefaultIfEmpty(null).Min());
            AddRate(Pairs, "a known pair split in public", r => W(r).members.Count == 0 ? (double?)null : Flag(W(r).publicSplit));
            AddRate(Pairs, "the Hitmen's final two broken", r => W(r).hitmenFinalTwo == null || W(r).hitmenFinalTwo == "none" ? (double?)null : Flag(W(r).hitmenFinalTwo == DealStatus.Broken));
            AddRate(Pairs, "the Hitmen re-form a pact", r => W(r).hitmenFirst == null ? (double?)null : Flag(W(r).hitmenReformed));
            AddRate(Pairs, "an outsider-only pact by week 3", r => W(r).members.Count == 0 ? (double?)null : Flag(W(r).outsiderPactByWeek3));
            AddMean(Pairs, "bloc hunts a season", "0.00", r => W(r).blocHunts);
            AddMean(Pairs, "longest run of weeks one listener hears a bloc hunt", "0.00", r => W(r).longestListenerRun);
            AddMean(Pairs, "longest same-subject rumour run, weeks", "0.00", r => W(r).longestRumourRun);
            AddMean(Pairs, "goodwill share of past-game receipts at the end", "0.000", r => W(r).goodwill);
            AddRate(Pairs, "pile-on seasons", r => r.pace == null ? (double?)null : Flag(r.pace.pileOns > 0));
            AddRate(Pairs, "pariah-free seasons", r => r.pace == null ? (double?)null : Flag(!r.pace.pariah));
            AddRate(Pairs, "one houseguest put up three or more weeks running", r => Flag(W(r).putUpThreeRunning));
            AddMean(Pace, "commands a season", "0.0", r => r.commands);
            AddMean(Pace, "weeks a season", "0.0", r => r.autopsy.weeks);
            AddMean(Pace, "decisions a week in the house", "0.00", r =>
            {
                int weeks = r.autopsy.playerOutWeek > 0 ? r.autopsy.playerOutWeek : r.autopsy.weeks;
                return (double)r.decisionsByWeek.Where(p => p.Key <= weeks).Sum(p => p.Value) / Math.Max(1, weeks);
            });
            AddMean(Pace, "ceremony s a regular week", "0.0", r => r.ceremonyByWeek.Count == 0 ? (double?)null : r.ceremonyByWeek.Values.Average());
            AddMean(Pace, "seconds a season on its thread (timed)", "0.00", r => r.seasonMilliseconds / 1000.0, compared: false);
            AddMean(Watch, "births a season", "0.00", r => W(r).births);
            AddMean(Watch, "joins a season", "0.00", r => W(r).joins);
            AddRate(Watch, "seasons with a join", r => Flag(W(r).joins > 0));
            AddMean(Watch, "week of the first join", "0.00", r => W(r).firstJoinWeek == 0 ? (double?)null : W(r).firstJoinWeek);
            AddMean(Watch, "bloc-live weeks (bound above two)", "0.00", r => W(r).blocLiveWeeks.Count);
            AddMean(Watch, "joins per bloc-live week", "0.000", r => W(r).blocLiveWeeks.Count == 0 ? (double?)null : W(r).joins / (double)W(r).blocLiveWeeks.Count);
            AddRate(Watch, "largest outsider pact reaches 3", r => Flag(W(r).largestOutsiderPact >= 3));
            AddRate(Watch, "largest outsider pact reaches 4", r => Flag(W(r).largestOutsiderPact >= 4));
            AddMean(Watch, "pact-weeks held below the bound by being known", "0.00", r => W(r).heldBack.Values.Sum());
            AddMean(Watch, "grown pacts", "0.00", r => W(r).grownEnds.Count);
            AddMean(Watch, "joiners shed", "0.00", r => W(r).shed);
            AddMean(Watch, "joiners out otherwise (a story's leave)", "0.00", r => W(r).joinersOut);
            AddMean(Watch, "NPCs in NPC-only pacts, weekly mean share", "0.000", r => W(r).npcPactShares.Count == 0 ? (double?)null : W(r).npcPactShares.Average());
            AddMean(Watch, "NPCs in NPC-only pacts, weekly most", "0.000", r => W(r).npcPactShares.Count == 0 ? (double?)null : W(r).npcPactShares.Max());
            AddRate(Watch, "two grown pacts at once", r => Flag(W(r).twoGrownAtOnce));
            AddRate(Watch, "the player invited into a grown pact", r => Flag(W(r).playerInvitedIntoGrown));
            AddMean(Watch, "week the player first knew of a grown pact", "0.00", r => W(r).playerLearnedGrownWeek == 0 ? (double?)null : W(r).playerLearnedGrownWeek);
            AddMean(Watch, "evictions carried by an outsider bloc of three or more", "0.000", r => W(r).blocCarried);
            AddMean(Watch, "cold pairs at week 3", "0.00", r => W(r).coldPairsWeek3 < 0 ? (double?)null : W(r).coldPairsWeek3);
            AddMean(Watch, "most hunts on one target in a week", "0.00", r => W(r).mostHuntsOnOneTarget);
            AddMean(Watch, "hunts at a pact-mate of both", "0.000", r => W(r).huntsAtPactMate);
            AddMean(Watch, "target agreements at a pact-mate of both", "0.000", r => W(r).agreementsAtPactMate);
            AddMean(Watch, "Power Shift cast on a grown pact", "0.000", r => W(r).powerShiftOnGrown);
            AddMean(Watch, "pile-ons a season", "0.00", r => W(r).pileOns.Count);
            return m;
        }

        /// <summary>A cell's value of a row: seasons with one, its mean, and a 95% interval (Wilson for a rate, the normal one for a mean).</summary>
        internal static (int n, double mean, double low, double high) Summary(IEnumerable<BalanceLab.SeasonRun> runs, Metric metric)
        {
            var values = runs.Select(metric.of).Where(v => v.HasValue).Select(v => v.Value).ToList();
            if (values.Count == 0) return (0, double.NaN, double.NaN, double.NaN);
            double mean = values.Average();
            if (metric.rate)
            {
                var ci = BalanceLab.Wilson((int)Math.Round(values.Sum()), values.Count);
                return (values.Count, mean, ci.low, ci.high);
            }
            if (values.Count < 2) return (values.Count, mean, double.NaN, double.NaN);
            double half = Z * Math.Sqrt(values.Sum(v => (v - mean) * (v - mean)) / (values.Count - 1)) / Math.Sqrt(values.Count);
            return (values.Count, mean, mean - half, mean + half);
        }

        /// <summary>
        /// A paired difference, later arm less earlier, over the seasons both hold a value for, paired by index (the runs come
        /// ordered, and their seeds are checked equal): its mean and 95% interval, as the H1b gate computes them.
        /// </summary>
        internal static (int n, double mean, double low, double high) Paired(IReadOnlyList<BalanceLab.SeasonRun> later, IReadOnlyList<BalanceLab.SeasonRun> earlier, Func<BalanceLab.SeasonRun, double?> of)
        {
            if (later.Count != earlier.Count || later.Zip(earlier, (x, y) => x.seed != y.seed || x.index != y.index).Any(differ => differ))
                throw new ArgumentException("The arms pair by seed.");
            var d = later.Zip(earlier, (x, y) => (x: of(x), y: of(y))).Where(p => p.x.HasValue && p.y.HasValue).Select(p => p.x.Value - p.y.Value).ToList();
            if (d.Count == 0) return (0, double.NaN, double.NaN, double.NaN);
            double mean = d.Average();
            if (d.Count < 2) return (d.Count, mean, double.NaN, double.NaN);
            double half = Z * Math.Sqrt(d.Sum(v => (v - mean) * (v - mean)) / (d.Count - 1)) / Math.Sqrt(d.Count);
            return (d.Count, mean, mean - half, mean + half);
        }

        internal static bool Beyond((int n, double mean, double low, double high) d) => !double.IsNaN(d.low) && (d.low > 0 || d.high < 0);

        /// <summary>
        /// Stop condition 1 for one budget in one cell (14.H): it fires when the shipped arm crosses the budget beyond noise - its
        /// 95% interval wholly on the wrong side of the line - where arm 1 meets it (its rate on the right side). Growth is
        /// the cause when arm 4 meets it there.
        /// </summary>
        internal static (bool fires, bool growthTheCause) StopOne(double line, bool atMost, (int k, int n) arm1, (int k, int n) arm4, (int k, int n) arm5)
        {
            bool Meets((int k, int n) a) => a.n > 0 && (atMost ? (double)a.k / a.n <= line : (double)a.k / a.n >= line);
            bool Crosses((int k, int n) a)
            {
                if (a.n == 0) return false;
                var ci = BalanceLab.Wilson(a.k, a.n);
                return atMost ? ci.low > line : ci.high < line;
            }
            bool fires = Meets(arm1) && Crosses(arm5);
            return (fires, fires && Meets(arm4));
        }

        /// <summary>Stop condition 2 (14.H): a known-pair member is the first evictee in more than half a cell's seasons.</summary>
        internal static bool StopTwo(int k, int n) => n > 0 && k * 2 > n;

        /// <summary>The near-zero rule (14.L.H): an 8-house cell at 5 where fewer than one season in ten holds a join.</summary>
        internal static bool NearZero(int k, int n) => n > 0 && (double)k / n < BalanceLabAlumniReport.NearZeroLine;

        internal sealed class Built
        {
            public string text, json, csv, summary;
        }

        private static string Who((string policy, string card) p) => p.card == null ? p.policy + " newcomer" : p.policy + " on " + p.card;
        private static string Arm(int arm) => arm == AlumniRules.Latest ? arm + " (shipped)" : arm == AlumniRules.Off ? "0 (no histories)" : arm + " (held)";
        private static string N(double v, string format) => BalanceLab.Num(v, format);
        private static string Signed(double v, string format) => BalanceLab.Num(v, "+" + format + ";-" + format + ";" + format);
        private static string Rate(int k, int n) => n == 0 ? "n/a" : BalanceLab.Pct((double)k / n) + " " + BalanceLab.Interval(BalanceLab.Wilson(k, n)) + " (" + k + "/" + n + ")";

        private static string Value(Metric metric, (int n, double mean, double low, double high) s) =>
            s.n == 0 ? "n/a" : metric.rate ? BalanceLab.Pct(s.mean) : N(s.mean, metric.format);

        private static string Difference(Metric metric, (int n, double mean, double low, double high) d)
        {
            if (d.n == 0) return "n/a";
            string f = metric.rate ? "0.000" : metric.format;
            string text = Signed(d.mean, f) + (double.IsNaN(d.low) ? "" : " [" + Signed(d.low, f) + ", " + Signed(d.high, f) + "]");
            return Beyond(d) ? "**" + text + "**" : text;
        }

        /// <summary>The report, whole: the text for the owner, the JSON and the seasons' CSV.</summary>
        internal static Built Build(IReadOnlyList<BalanceLab.Cell> cells, IReadOnlyList<BalanceLab.SeasonRun> runs, Meta meta)
        {
            var metrics = Metrics();
            var byKey = runs.GroupBy(r => r.cell.Key).ToDictionary(g => g.Key, g => g.OrderBy(r => r.index).ToList(), StringComparer.Ordinal);
            List<BalanceLab.SeasonRun> Of(int size, (string policy, string card) p, int arm) =>
                byKey.TryGetValue(BalanceLabAlumniReport.ReportCell(p.policy, size, p.card, arm).Key, out var list) ? list : new List<BalanceLab.SeasonRun>();
            var sizes = cells.Select(c => c.size).Distinct().OrderBy(s => s).ToList();
            var players = BalanceLabAlumniReport.ReportPlayers.Where(p => cells.Any(c => c.policy == p.policy && c.playerTemplate == p.card)).ToList();
            var arms = BalanceLabAlumniReport.ReportArms.Where(a => cells.Any(c => BalanceLabAlumniReport.ArmOf(c) == a)).ToList();
            var bases = (from size in sizes from p in players select (size, p)).ToList();
            foreach (var (size, p) in bases)
                foreach (int arm in arms)
                {
                    var seeds = Of(size, p, arm).Select(r => r.seed).ToList();
                    if (!seeds.SequenceEqual(Of(size, p, arms[0]).Select(r => r.seed))) throw new InvalidOperationException("The arms of " + Who(p) + " " + size + " do not pair by seed.");
                }

            var json = new Dictionary<string, object>(StringComparer.Ordinal);
            var bugs = new List<string>();
            var top = new List<string>();
            var md = new StringBuilder();

            // ------------------------------------------------ must be zero
            var zeroRows = new List<object>();
            var zeroTable = new StringBuilder();
            zeroTable.AppendLine("| row | " + string.Join(" | ", arms.Select(Arm)) + " |");
            zeroTable.AppendLine("|---|" + string.Concat(arms.Select(_ => "---|")));
            var rulesMoved = runs.Where(r => r.pactWatch != null && r.pactWatch.rulesLow != r.pactWatch.rulesHigh).ToList();
            foreach (string row in BalanceLab.PactWatch.MustBeZero.Concat(new[] { "a season whose alumni rules value moved", "a season the lab could not play" }))
            {
                var counts = new List<string>();
                foreach (int arm in arms)
                {
                    var armRuns = runs.Where(r => BalanceLabAlumniReport.ArmOf(r.cell) == arm).ToList();
                    int total = row == "a season whose alumni rules value moved" ? armRuns.Count(r => rulesMoved.Contains(r))
                        : row == "a season the lab could not play" ? armRuns.Count(r => r.error != null)
                        : armRuns.Where(r => r.pactWatch != null).Sum(r => r.pactWatch.mustBeZero.TryGetValue(row, out int n) ? n : 0);
                    counts.Add(total == 0 ? "0" : "**" + total + "**");
                    zeroRows.Add(new { row, arm, count = total });
                    if (total == 0) continue;
                    var where = armRuns.Where(r => (row == "a season whose alumni rules value moved" && rulesMoved.Contains(r)) || (row == "a season the lab could not play" && r.error != null)
                        || (r.pactWatch != null && r.pactWatch.mustBeZero.TryGetValue(row, out int k) && k > 0)).Take(3).Select(r => r.cell.Key + " #" + r.index);
                    bugs.Add("BUG: must be zero, is " + total + " at arm " + Arm(arm) + ": " + row + " (first: " + string.Join("; ", where) + ")."
                        + (arm == AlumniRules.GrowingPacts ? " Fixed before S6 (14.L.H); never an owner question." : " A value below 5: not growth's, still a bug."));
                }
                zeroTable.AppendLine("| " + row + " | " + string.Join(" | ", counts) + " |");
            }
            json["mustBeZero"] = zeroRows;

            // ------------------------------------------------ the near-zero rule
            int latest = AlumniRules.Latest;
            var nearRows = new List<object>();
            var eightLow = new List<string>();
            var twelve = new List<string>();
            foreach (var (size, p) in bases)
            {
                var list = Of(size, p, latest).Where(Ok).ToList();
                int k = list.Count(r => r.pactWatch.joins > 0), n = list.Count;
                bool fires = size == 8 && NearZero(k, n);
                nearRows.Add(new { size, player = Who(p), seasonsWithAJoin = k, seasons = n, fires });
                if (fires) eightLow.Add(Who(p) + " " + Rate(k, n));
                if (size > 8) twelve.Add(Who(p) + " at " + size + " " + Rate(k, n));
            }
            json["nearZero"] = nearRows;
            if (eightLow.Count > 0)
                top.Add("Near-zero rule (14.L.H): fewer than one season in ten of an 8-house cell at alumni rules 5 holds a join - " + string.Join("; ", eightLow)
                    + ". In the twelve-house: " + (twelve.Count == 0 ? "not run" : string.Join("; ", twelve)) + ". The owner's call shows mostly in houses of 9 or more. "
                    + "Nothing is retuned before S6; the lever named for a later value, built only on the owner's word, is the recruit's bar toward its sponsor (score 25, desire above 25).");

            // ------------------------------------------------ the stop conditions
            var stop = new StringBuilder();
            var stopJson = new List<object>();
            bool anyOne = false, anyTwo = false;
            Metric pileOn = metrics.Single(x => x.name == "pile-on seasons"), pariahFree = metrics.Single(x => x.name == "pariah-free seasons");
            Metric firstOut = metrics.Single(x => x.name == "first evictee a known-pair member");
            (int k, int n) Count(Metric metric, List<BalanceLab.SeasonRun> list)
            {
                var values = list.Select(metric.of).Where(v => v.HasValue).ToList();
                return ((int)Math.Round(values.Sum(v => v.Value)), values.Count);
            }
            stop.AppendLine("**Stop condition 1** (14.H): the shipped arm (5) crosses a story-session budget beyond noise - its 95% interval wholly past the line - in a cell where arm 1 meets it. "
                + "If arm 4 meets it there, growth is the cause and the owner is offered value 4 (S3b out before S6). Budgets: pile-on seasons at most 25%; pariah-free seasons at least 80% "
                + "(the twelve-house: not below its 45%; NPC-AGENCY-PLAN constraint 1).");
            stop.AppendLine();
            stop.AppendLine("| player | size | budget | arm 1 | arm 4 | arm 5 | 5 - 1 paired | fires | growth the cause |");
            stop.AppendLine("|---|---|---|---|---|---|---|---|---|");
            bool canOne = arms.Contains(AlumniRules.Histories) && arms.Contains(AlumniRules.CounterBlocs) && arms.Contains(AlumniRules.GrowingPacts);
            foreach (var (size, p) in bases)
            {
                if (!canOne) break;
                foreach (var (metric, line, atMost, label) in new[] { (pileOn, BalanceLabAlumniReport.PileOnBudget, true, "pile-on <= 25%"), (pariahFree, PariahLine(size), false, size > 8 ? "pariah-free >= 45%" : "pariah-free >= 80%") })
                {
                    var a1 = Count(metric, Of(size, p, AlumniRules.Histories)); var a4 = Count(metric, Of(size, p, AlumniRules.CounterBlocs)); var a5 = Count(metric, Of(size, p, AlumniRules.GrowingPacts));
                    var (fires, growth) = StopOne(line, atMost, a1, a4, a5);
                    var d = Paired(Of(size, p, AlumniRules.GrowingPacts), Of(size, p, AlumniRules.Histories), metric.of);
                    anyOne |= fires;
                    stop.AppendLine("| " + Who(p) + " | " + size + " | " + label + " | " + Rate(a1.k, a1.n) + " | " + Rate(a4.k, a4.n) + " | " + Rate(a5.k, a5.n) + " | " + Difference(metric, d)
                        + " | " + (fires ? "**FIRES**" : "no") + " | " + (fires ? (growth ? "**yes: offer value 4**" : "no: arm 4 crosses too") : "-") + " |");
                    stopJson.Add(new { condition = 1, player = Who(p), size, budget = label, arm1 = new { a1.k, a1.n }, arm4 = new { a4.k, a4.n }, arm5 = new { a5.k, a5.n }, fires, growthTheCause = growth });
                }
            }
            stop.AppendLine();
            stop.AppendLine("**Stop condition 2** (14.H): a known-pair member is the first evictee in more than half the seasons of a cell, read at 4 and at 5 separately.");
            stop.AppendLine();
            stop.AppendLine("| player | size | arm 4 | fires at 4 | arm 5 | fires at 5 |");
            stop.AppendLine("|---|---|---|---|---|---|");
            foreach (var (size, p) in bases)
            {
                var a4 = Count(firstOut, Of(size, p, AlumniRules.CounterBlocs)); var a5 = Count(firstOut, Of(size, p, AlumniRules.GrowingPacts));
                bool f4 = StopTwo(a4.k, a4.n), f5 = StopTwo(a5.k, a5.n);
                anyTwo |= f4 || f5;
                stop.AppendLine("| " + Who(p) + " | " + size + " | " + Rate(a4.k, a4.n) + " | " + (f4 ? "**FIRES**" : "no") + " | " + Rate(a5.k, a5.n) + " | " + (f5 ? "**FIRES**" : "no") + " |");
                stopJson.Add(new { condition = 2, player = Who(p), size, arm4 = new { a4.k, a4.n }, firesAt4 = f4, arm5 = new { a5.k, a5.n }, firesAt5 = f5 });
            }
            json["stopConditions"] = stopJson;

            // ------------------------------------------------ the rows and their comparisons
            var rows = new StringBuilder();
            var cellJson = new List<object>();
            var comparisonJson = new List<object>();
            var crossed = new List<string>();
            int compared = 0;
            var comparisons = BalanceLabAlumniReport.Comparisons.Where(c => arms.Contains(c.later) && arms.Contains(c.earlier)).ToList();
            foreach (var section in metrics.Select(x => x.section).Distinct())
            {
                rows.AppendLine("### " + section);
                rows.AppendLine();
                foreach (var metric in metrics.Where(x => x.section == section))
                {
                    rows.AppendLine("**" + metric.name + "**" + (metric.rate ? " (share of seasons)" : "") + ": by arm, then the paired differences (later arm less earlier, 95% interval; bold is beyond noise).");
                    rows.AppendLine();
                    rows.AppendLine("| player | size | " + string.Join(" | ", arms.Select(Arm)) + " | n | " + string.Join(" | ", comparisons.Select(c => c.later + " - " + c.earlier)) + " |");
                    rows.AppendLine("|---|---|" + string.Concat(arms.Select(_ => "---|")) + "---|" + string.Concat(comparisons.Select(_ => "---|")));
                    foreach (var (size, p) in bases)
                    {
                        var values = arms.Select(arm => (arm, s: Summary(Of(size, p, arm), metric))).ToList();
                        foreach (var v in values)
                            cellJson.Add(new { metric = metric.name, section, player = Who(p), size, arm = v.arm, v.s.n, mean = Json(v.s.mean), low = Json(v.s.low), high = Json(v.s.high) });
                        var diffs = new List<string>();
                        foreach (var c in comparisons)
                        {
                            if (!metric.compared) { diffs.Add("-"); continue; }
                            var d = Paired(Of(size, p, c.later), Of(size, p, c.earlier), metric.of);
                            if (d.n > 0) compared++;
                            if (Beyond(d)) crossed.Add(metric.name + ", " + Who(p) + " " + size + ", " + c.later + " - " + c.earlier + ": " + Difference(metric, d).Replace("**", ""));
                            diffs.Add(Difference(metric, d));
                            comparisonJson.Add(new { metric = metric.name, player = Who(p), size, later = c.later, earlier = c.earlier, d.n, mean = Json(d.mean), low = Json(d.low), high = Json(d.high), beyond = Beyond(d) });
                        }
                        rows.AppendLine("| " + Who(p) + " | " + size + " | " + string.Join(" | ", values.Select(v => Value(metric, v.s))) + " | "
                            + values.Select(v => v.s.n).DefaultIfEmpty(0).Min() + " | " + string.Join(" | ", diffs) + " |");
                    }
                    rows.AppendLine();
                }
            }
            json["cells"] = cellJson;
            json["comparisons"] = comparisonJson;

            // ------------------------------------------------ the pact watch's tables
            var tables = new StringBuilder();
            tables.AppendLine("**The largest outsider pact a season** (14.L.H): the share of seasons whose largest outsider pact held none, 2, 3 or 4 in the house at once.");
            tables.AppendLine();
            tables.AppendLine("| player | size | arm | none | 2 | 3 | 4 |");
            tables.AppendLine("|---|---|---|---|---|---|---|");
            foreach (var (size, p) in bases)
                foreach (int arm in arms)
                {
                    var list = Of(size, p, arm).Where(Ok).ToList();
                    if (list.Count == 0) continue;
                    string Share(Func<int, bool> at) => BalanceLab.Pct((double)list.Count(r => at(r.pactWatch.largestOutsiderPact)) / list.Count);
                    tables.AppendLine("| " + Who(p) + " | " + size + " | " + Arm(arm) + " | " + Share(x => x < 2) + " | " + Share(x => x == 2) + " | " + Share(x => x == 3) + " | " + Share(x => x >= 4) + " |");
                }
            tables.AppendLine();
            tables.AppendLine("**Pact-weeks held below the bound only because somebody outside knew of the pact**, by how it first came out (14.L.H; below 5 the bound is the one the pact would have had), every player pooled.");
            tables.AppendLine();
            tables.AppendLine("| size | arm | seasons | " + string.Join(" | ", BalanceLab.PactWatch.Causes) + " |");
            tables.AppendLine("|---|---|---|" + string.Concat(BalanceLab.PactWatch.Causes.Select(_ => "---|")));
            foreach (int size in sizes)
                foreach (int arm in arms)
                {
                    var list = players.SelectMany(p => Of(size, p, arm)).Where(Ok).ToList();
                    tables.AppendLine("| " + size + " | " + Arm(arm) + " | " + list.Count + " | "
                        + string.Join(" | ", BalanceLab.PactWatch.Causes.Select(cause => list.Sum(r => r.pactWatch.heldBack.TryGetValue(cause, out int n) ? n : 0).ToString(CultureInfo.InvariantCulture))) + " |");
                }
            tables.AppendLine();
            tables.AppendLine("**How grown pacts ended** (the alliance row's why; \"standing\" at the finale), the weeks they lasted, their most in the house, and whether anyone outside knew of them then; every player pooled.");
            tables.AppendLine();
            tables.AppendLine("| size | arm | ending | pacts | mean weeks | known at the end | reached 4 |");
            tables.AppendLine("|---|---|---|---|---|---|---|");
            foreach (int size in sizes)
                foreach (int arm in arms)
                    foreach (var g in players.SelectMany(p => Of(size, p, arm)).Where(Ok).SelectMany(r => r.pactWatch.grownEnds).GroupBy(e => e.why).OrderBy(x => x.Key, StringComparer.Ordinal))
                        tables.AppendLine("| " + size + " | " + Arm(arm) + " | " + g.Key + " | " + g.Count() + " | " + N(g.Average(e => e.weeks), "0.0") + " | "
                            + BalanceLab.Pct((double)g.Count(e => e.known) / g.Count()) + " | " + BalanceLab.Pct((double)g.Count(e => e.largest >= 4) / g.Count()) + " |");
            tables.AppendLine();
            tables.AppendLine("**The Hitmen's opening final two at the season's end** (its status; \"none\" without one, as at 0).");
            tables.AppendLine();
            var statuses = runs.Where(Ok).Select(r => r.pactWatch.hitmenFinalTwo ?? "none").Distinct().OrderBy(x => x, StringComparer.Ordinal).ToList();
            tables.AppendLine("| player | size | arm | " + string.Join(" | ", statuses) + " |");
            tables.AppendLine("|---|---|---|" + string.Concat(statuses.Select(_ => "---|")));
            foreach (var (size, p) in bases)
                foreach (int arm in arms)
                {
                    var list = Of(size, p, arm).Where(Ok).ToList();
                    if (list.Count == 0) continue;
                    tables.AppendLine("| " + Who(p) + " | " + size + " | " + Arm(arm) + " | " + string.Join(" | ", statuses.Select(st => BalanceLab.Pct((double)list.Count(r => (r.pactWatch.hitmenFinalTwo ?? "none") == st) / list.Count))) + " |");
                }
            tables.AppendLine();
            tables.AppendLine("**Pile-on targets** (the story's the-house-turns), classed as 14.L.H classes them, with the pariah decomposition's columns (BalanceLabDiagnostics.PariahDecomposition); every player pooled.");
            tables.AppendLine();
            tables.AppendLine("| size | arm | target | pile-ons | mean week | in the house then | holders | target's HoH wins (house's most) | target won a HoH | target won two | causes of the holders' grudges |");
            tables.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|");
            foreach (int size in sizes)
                foreach (int arm in arms)
                    foreach (string kind in BalanceLab.PactWatch.PileOnKinds)
                    {
                        var piles = players.SelectMany(p => Of(size, p, arm)).Where(Ok).SelectMany(r => r.pactWatch.pileOns).Where(x => x.kind == kind).ToList();
                        if (piles.Count == 0) { tables.AppendLine("| " + size + " | " + Arm(arm) + " | " + kind + " | 0 | - | - | - | - | - | - | - |"); continue; }
                        var causes = piles.SelectMany(x => x.causes).GroupBy(c => c).Select(g => (cause: g.Key, n: g.Count())).OrderByDescending(x => x.n).ThenBy(x => x.cause, StringComparer.Ordinal).ToList();
                        double all = Math.Max(1, causes.Sum(x => x.n));
                        tables.AppendLine("| " + size + " | " + Arm(arm) + " | " + kind + " | " + piles.Count + " | " + N(piles.Average(x => x.week), "0.0") + " | " + N(piles.Average(x => x.houseCount), "0.0")
                            + " | " + N(piles.Average(x => x.holders), "0.0") + " | " + N(piles.Average(x => x.targetHohWins), "0.00") + " (" + N(piles.Average(x => x.npcHohWinsMax), "0.00") + ") | "
                            + BalanceLab.Pct((double)piles.Count(x => x.targetHohWins >= 1) / piles.Count) + " | " + BalanceLab.Pct((double)piles.Count(x => x.targetHohWins >= 2) / piles.Count) + " | "
                            + string.Join(", ", causes.Select(x => (x.cause.Length == 0 ? "?" : x.cause) + " " + BalanceLab.Pct(x.n / all))) + " |");
                    }
            tables.AppendLine();

            // ------------------------------------------------ the text, top first
            int seasons = runs.Count, errors = runs.Count(r => r.error != null);
            double threadSeconds = runs.Sum(r => r.seasonMilliseconds) / 1000.0;
            foreach (string line in top) md.AppendLine(line).AppendLine();
            foreach (string line in bugs) md.AppendLine(line).AppendLine();
            md.AppendLine("# All-Star loyalty, reputation and known blocs: the 14.H report");
            md.AppendLine();
            md.AppendLine("ALL-STAR-HISTORIES-PLAN 14.H and 14.L.H. Not a gate: about one comparison in twenty crosses at 95% by chance, nothing is retuned in place, and a retune is a later value.");
            md.AppendLine();
            md.AppendLine("- Run as: `" + meta.command + "`");
            md.AppendLine("- Commit: " + meta.commit + " (the source tree's HEAD; changes not yet committed are not shown). Started " + meta.started + ".");
            md.AppendLine("- Seasons: " + seasons + " (" + cells.Count + " cells, " + meta.seeds + " seeds each, paired by seed across the arms), " + errors + " the lab could not play.");
            md.AppendLine("- Elapsed: " + N(meta.minutes, "0.0") + " min on " + meta.threads + " threads (" + Environment.ProcessorCount + " processors seen): "
                + N(seasons / Math.Max(1e-9, meta.minutes), "0.0") + " seasons a minute; " + N(threadSeconds / Math.Max(1, seasons), "0.00") + " s a season on its thread.");
            md.AppendLine("- Arms: " + string.Join(", ", arms.Select(Arm)) + ". 0 is the season without the histories (H1b's control); 1 to 4 are the shipped season held at the value after it enrolled (14.G).");
            md.AppendLine("- Cells: All-Stars " + string.Join(" and ", sizes) + " x " + string.Join(", ", players.Select(Who)) + ". The NPC world is not driven (B5): NPC-to-NPC behaviour is proved by fixtures, never by these walks.");
            md.AppendLine("- Known pairs are edition 1's seated shared alliances, read the same way at every arm (0 included), live while both are in the house and neither has put the other up.");
            md.AppendLine();
            md.AppendLine("## For the owner: the stop conditions");
            md.AppendLine();
            md.AppendLine("- Stop condition 1 " + (canOne ? anyOne ? "**FIRES** in the cells marked below." : "does not fire in any cell." : "was not computed: arms 1, 4 and 5 were not all run."));
            md.AppendLine("- Stop condition 2 " + (anyTwo ? "**FIRES** in the cells marked below." : "does not fire in any cell."));
            md.AppendLine("- Must-be-zero rows: " + (bugs.Count == 0 ? "all zero." : bugs.Count + " bug line(s) at the top."));
            md.AppendLine("- Comparisons beyond noise: " + crossed.Count + " of " + compared + " (about " + N(compared * 0.05, "0") + " expected by chance); listed at the end.");
            md.AppendLine();
            md.Append(stop);
            md.AppendLine();
            md.AppendLine("## Must be zero (14.L.H, and the watch's own checks)");
            md.AppendLine();
            md.Append(zeroTable);
            md.AppendLine();
            md.AppendLine("## The rows");
            md.AppendLine();
            md.Append(rows);
            md.AppendLine("## The pact watch's tables (14.L.H)");
            md.AppendLine();
            md.Append(tables);
            md.AppendLine("## Comparisons beyond noise");
            md.AppendLine();
            if (crossed.Count == 0) md.AppendLine("None.");
            foreach (string line in crossed) md.AppendLine("- " + line);

            json["meta"] = new
            {
                meta.commit, meta.command, meta.started, meta.seeds, meta.threads, minutes = Math.Round(meta.minutes, 3), seasons, errors, host = meta.host,
                seasonsPerMinute = Math.Round(seasons / Math.Max(1e-9, meta.minutes), 3), secondsASeasonOnItsThread = Math.Round(threadSeconds / Math.Max(1, seasons), 4),
                arms, sizes, players = players.Select(Who).ToList(), comparisons = comparisons.Select(c => c.later + "-" + c.earlier).ToList(),
            };
            json["bugLines"] = bugs;
            json["topLines"] = top;
            json["crossed"] = crossed;

            // ------------------------------------------------ one line a season
            var csv = new StringBuilder();
            csv.AppendLine("cell,player,size,arm,index,seed,error," + string.Join(",", metrics.Select(x => Csv(x.name))));
            foreach (var r in runs)
                csv.AppendLine(Csv(r.cell.Key) + "," + Csv(Who((r.cell.policy, r.cell.playerTemplate))) + "," + r.cell.size + "," + BalanceLabAlumniReport.ArmOf(r.cell) + "," + r.index + "," + r.seed
                    + "," + Csv(r.error ?? "") + "," + string.Join(",", metrics.Select(x => x.of(r) is double v ? v.ToString("R", CultureInfo.InvariantCulture) : "")));

            string summary = string.Join("\n", top.Concat(bugs).Concat(new[]
            {
                "Run as: " + meta.command,
                "Commit: " + meta.commit + "; started " + meta.started + ".",
                seasons + " seasons in " + N(meta.minutes, "0.0") + " min on " + meta.threads + " threads; " + N(threadSeconds / Math.Max(1, seasons), "0.00") + " s a season on its thread; " + errors + " errors.",
                "Stop condition 1: " + (canOne ? anyOne ? "FIRES" : "does not fire" : "not computed") + ". Stop condition 2: " + (anyTwo ? "FIRES" : "does not fire") + ".",
                "Comparisons beyond noise: " + crossed.Count + " of " + compared + ".",
            }));
            return new Built
            {
                text = md.ToString(), csv = csv.ToString(), summary = summary,
                json = Newtonsoft.Json.JsonConvert.SerializeObject(json, Newtonsoft.Json.Formatting.Indented, new Newtonsoft.Json.JsonSerializerSettings { Culture = CultureInfo.InvariantCulture }),
            };
        }

        private static double PariahLine(int size) => size > 8 ? BalanceLabAlumniReport.PariahFreeBudgetTwelve : BalanceLabAlumniReport.PariahFreeBudget;

        private static double? Json(double value) => double.IsNaN(value) || double.IsInfinity(value) ? (double?)null : Math.Round(value, 6);

        private static string Csv(string text) => text.IndexOfAny(new[] { ',', '"', '\n' }) < 0 ? text : "\"" + text.Replace("\"", "\"\"") + "\"";
    }
}
#endif
