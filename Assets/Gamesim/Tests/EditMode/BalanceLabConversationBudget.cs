#if !UNITY_5_3_OR_NEWER
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Gamesim.Simulation;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// DIALOGUE-PLAN §5.5's measurement, P1(d): the most storylines and house events a season reaches without dialogue,
    /// which rules 2's conversation budget (DialogueRules.ConversationBudget at rules 2) must leave room above. The
    /// storyline peak plus the budget stays at or under <see cref="DialogueRules.Rules2StorylineCeiling"/>, and the
    /// house-event peak plus <see cref="DialogueRules.Rules2Steps"/> a budgeted conversation at or under
    /// <see cref="DialogueRules.MaximumEvents"/>, so an arm allows min(budget, 92 - storylines, floor((400 - events) / 5)).
    ///
    /// <para><b>A season</b> is played as the lab plays one: <see cref="BalanceLab.Step"/> with the lab's players, the NPC
    /// world (<see cref="BalanceLabNpcWorld"/>) at a budget of ticks a week, the state validated at every Advance and phase
    /// change and at the end, on dialogue rules 0. Both counts are read after every command and every NPC operation.
    /// Nothing prunes either list, so neither count may ever fall: a season in which one does is an error, and its final
    /// counts must be its peaks. The lab issues no walk-ins (WitnessProximity), which a played season has and which start
    /// storylines and add house events, so these are the lab's peaks, not a played season's upper bound.</para>
    ///
    /// <para><b>The arms</b> (BALANCE_P1D_ARMS, by default all four). A: sixteen houseguests, the verification stress
    /// house (<see cref="SeasonBuilder.CreateVerificationStressHouse"/>, both rosters, so an alumni season) under the
    /// shipped rules, its story held at <see cref="StoryRules.Blame"/> by name. B: A with the story at
    /// <see cref="StoryRules.Roles"/>, the shipped story rules since R7 enrolled them (by name too). C:
    /// twelve houseguests of the regular cast as the cast screen builds them (<see cref="SeasonBuilder.Create"/>, through
    /// <see cref="BalanceLab.Fresh"/>) under the shipped rules, held at Blame. D: C with Roles. A and B play the same seeds, and so do C
    /// and D, whose seeds are the lab's own at twelve; every player plays the same seeds.</para>
    ///
    /// <para><b>The knobs</b>: BALANCE_P1D_POLICIES (by default the NPC tier's five), BALANCE_P1D_FROM and
    /// BALANCE_P1D_COUNT (the seed indices, 0 and 12), BALANCE_P1D_TICKS (the NPC world's ticks a week: 300, the budget that
    /// stands for a human; 0 plays none), BALANCE_P1D_THREADS (seasons at once: the processor count) and BALANCE_P1D_OUT
    /// (this part's rows, one line a season; by default balance/p1d/peaks-&lt;arms&gt;-npc&lt;ticks&gt;-&lt;from&gt;-&lt;to&gt;.jsonl
    /// under the test's work directory). After a part, every part's rows in that folder are summed into its summary.txt,
    /// one line an arm (<see cref="SummarizePeaks"/> does only that). Run by name; compiled out of Unity, so neither the
    /// batch runner nor the floors count it.</para>
    /// </summary>
    public sealed class BalanceLabConversationBudget
    {
        /// <summary>Changed whenever what a row records changes: the summary sums only rows of this stamp.</summary>
        internal const string Stamp = "balance-lab-p1d/v1";

        /// <summary>One arm: its house and whether the story plays the role moments.</summary>
        internal sealed class Arm
        {
            public string name, about;
            public int size;
            public bool stress, roles;
        }

        internal static readonly Arm[] Arms =
        {
            new Arm { name = "A", size = 16, stress = true, about = "the verification stress house of 16, story rules Blame (10)" },
            new Arm { name = "B", size = 16, stress = true, roles = true, about = "the verification stress house of 16, story rules Roles" },
            new Arm { name = "C", size = 12, about = "a regular cast of 12, story rules Blame (10)" },
            new Arm { name = "D", size = 12, roles = true, about = "a regular cast of 12, story rules Roles" },
        };

        /// <summary>The players by default: the NPC budget tier's five (<see cref="BalanceLabReports.NpcPolicies"/>'s default).</summary>
        internal static readonly string[] DefaultPolicies =
            { BalancePolicies.Passive, BalancePolicies.Novice, BalancePolicies.Social, BalancePolicies.Reader, BalancePolicies.Beast };

        /// <summary>One season's line.</summary>
        internal sealed class Row
        {
            public string probe, arm, cast, policy;
            public int size, storyRules, tuningRules, alumniRules, dialogueRules, ticks, index;
            public uint seed;
            /// <summary>The last week played; the week the player left the house (0 if they never did).</summary>
            public int weeks, playerOutWeek;
            public int commands, npcOps, npcTicks, npcStarts;
            public int peakStorylines, peakHouseEvents, finalStorylines, finalHouseEvents;
            /// <summary>The season's wall-clock seconds on its thread.</summary>
            public double seconds;
            public string error;
        }

        // ---------------------------------------------------------------- the tests

        [Test, Explicit("DIALOGUE-PLAN §5.5's P1(d): BALANCE_P1D_ARMS x BALANCE_P1D_POLICIES x seeds BALANCE_P1D_FROM.. (BALANCE_P1D_COUNT), dialogue off, the NPC world at BALANCE_P1D_TICKS; rows to BALANCE_P1D_OUT, the folder's summary beside them. Run by name.")]
        public void MeasurePeaks()
        {
            var arms = ArmsFromEnvironment();
            var policies = ListFromEnvironment("BALANCE_P1D_POLICIES", DefaultPolicies);
            foreach (string policy in policies) Assert.That(BalancePolicies.Create(policy), Is.Not.Null, "No such policy: " + policy);
            int first = CountFromEnvironment("BALANCE_P1D_FROM", 0), count = CountFromEnvironment("BALANCE_P1D_COUNT", 12);
            int ticks = CountFromEnvironment("BALANCE_P1D_TICKS", BalanceLabTests.HumanBudget);
            int threads = BalanceLabReports.FromEnvironment("BALANCE_P1D_THREADS", Environment.ProcessorCount);
            string path = Environment.GetEnvironmentVariable("BALANCE_P1D_OUT");
            if (string.IsNullOrEmpty(path))
                path = Path.Combine(DefaultFolder(), "peaks-" + string.Concat(arms.Select(a => a.name)) + "-npc" + ticks.ToString(CultureInfo.InvariantCulture)
                    + "-" + first.ToString("D5", CultureInfo.InvariantCulture) + "-" + (first + count).ToString("D5", CultureInfo.InvariantCulture) + ".jsonl");
            path = Path.GetFullPath(path);

            var seasons = (from arm in arms from policy in policies from index in Enumerable.Range(first, count) select (arm, policy, index)).ToList();
            Assert.That(seasons, Is.Not.Empty, "Nothing to play: an arm, a player and a seed at least.");
            var rows = new Row[seasons.Count];
            var clock = BalanceLab.Clock();
            Parallel.For(0, rows.Length, new ParallelOptions { MaxDegreeOfParallelism = threads }, i => rows[i] = Play(seasons[i].arm, seasons[i].policy, seasons[i].index, ticks));
            double minutes = clock.Elapsed.TotalMinutes;

            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, string.Join("\n", rows.Select(Json)) + "\n", new UTF8Encoding(false));
            var md = new StringBuilder();
            md.AppendLine("P1(d) part: " + rows.Length + " seasons (arms " + string.Join(",", arms.Select(a => a.name)) + "; " + string.Join(",", policies) + "; seeds "
                + first + ".." + (first + count - 1) + "; " + ticks + " NPC ticks a week) in " + BalanceLab.Num(minutes, "0.0") + " min on " + threads + " thread(s); rows in " + path + ".");
            foreach (string line in Summary(rows)) md.AppendLine("  " + line);
            md.AppendLine();
            md.Append(WriteSummary(Path.GetDirectoryName(path)));
            TestContext.WriteLine(md.ToString());
            Assert.That(rows.Where(r => r.error != null).Select(r => r.arm + "/" + r.policy + " #" + r.index + ": " + r.error).Take(10), Is.Empty);
        }

        [Test, Explicit("P1(d)'s summary alone: every part's rows in BALANCE_P1D_OUT's folder (by default balance/p1d) summed into its summary.txt, one line an arm. Run by name.")]
        public void SummarizePeaks()
        {
            string path = Environment.GetEnvironmentVariable("BALANCE_P1D_OUT");
            string folder = string.IsNullOrEmpty(path) ? DefaultFolder() : Path.GetDirectoryName(Path.GetFullPath(path));
            Assert.That(Directory.Exists(folder) && Directory.GetFiles(folder, "*.jsonl").Length > 0, Is.True, "No rows in " + folder + ".");
            TestContext.WriteLine(WriteSummary(folder));
        }

        // ---------------------------------------------------------------- one season

        /// <summary>A season's seed: the stress house's own hash of its size and index, or the lab's seed at twelve.</summary>
        internal static uint Seed(Arm arm, int index) => arm.stress
            ? SeededRandom.HashSeed("balance-lab/v1/Stress/" + arm.size.ToString(CultureInfo.InvariantCulture) + "/npc0/" + index.ToString(CultureInfo.InvariantCulture))
            : BalanceLab.Seed(new BalanceLab.Cell { size = arm.size }, index);

        /// <summary>The arm's fresh season under the shipped rules, its story rules named by the arm: Roles or Blame.</summary>
        internal static EpisodeState Fresh(Arm arm, BalanceLab.Cell cell, uint seed)
        {
            EpisodeState fresh;
            if (arm.stress)
            {
                // As StressHouseTests' DirectorStressSeason builds it: the house the standalone verify seats at sixteen.
                fresh = SeasonBuilder.CreateVerificationStressHouse(arm.size, seed);
                ShippedRules.ApplyFresh(fresh);
            }
            else fresh = BalanceLab.Fresh(cell, seed);
            // Both arms by name (decision minor 8): Roles, the shipped story rules since R7's enrolment, and Blame, the rules
            // before it, which arms A and C measured as "shipped" when P1(d) ran.
            fresh.story.rulesVersion = arm.roles ? StoryRules.Roles : StoryRules.Blame;
            return fresh;
        }

        internal static Row Play(Arm arm, string policy, int index, int ticks)
        {
            var cell = new BalanceLab.Cell { policy = policy, size = arm.size, npcTicks = ticks };
            var run = new BalanceLab.SeasonRun { cell = cell, index = index, seed = Seed(arm, index) };
            var row = new Row { probe = Stamp, arm = arm.name, policy = policy, size = arm.size, ticks = ticks, index = index, seed = run.seed };
            var clock = Stopwatch.StartNew();
            try { PlayInto(arm, run, row); }
            catch (Exception error) { row.error = "exception: " + error.GetType().Name + ": " + error.Message; }
            row.seconds = Math.Round(clock.Elapsed.TotalSeconds, 3);
            row.commands = run.commands; row.npcOps = run.npcOps; row.npcTicks = run.npcTicks; row.npcStarts = run.npcStarts;
            return row;
        }

        /// <summary>The highest counts seen, and the first time either fell.</summary>
        private sealed class Peaks
        {
            public int storylines, houseEvents;
            public string error;

            public Peaks(EpisodeState fresh) { storylines = fresh.storylines.Count; houseEvents = fresh.houseEvents.Count; }

            public void See(EpisodeState s, string after)
            {
                int lines = s.storylines?.Count ?? 0, events = s.houseEvents?.Count ?? 0;
                if (error == null && (lines < storylines || events < houseEvents))
                    error = "week " + s.week + " " + s.phase + " after " + after + ": the storylines went from " + storylines + " to " + lines
                        + " and the house events from " + houseEvents + " to " + events + ", and both lists only grow";
                storylines = Math.Max(storylines, lines);
                houseEvents = Math.Max(houseEvents, events);
            }
        }

        private static bool Left(ContestantStatus status) =>
            status == ContestantStatus.Evicted || status == ContestantStatus.Jury || status == ContestantStatus.Expelled;

        /// <summary>BalanceLab.PlayInto's season without its reports: the same steps, world and validation, and the two counts read throughout.</summary>
        private static void PlayInto(Arm arm, BalanceLab.SeasonRun run, Row row)
        {
            var fresh = Fresh(arm, run.cell, run.seed);
            row.size = fresh.contestants.Count;
            row.cast = AlumniRules.On(fresh) ? "alumni" : "regular";
            row.alumniRules = fresh.alumniRulesVersion;
            row.storyRules = fresh.story.rulesVersion;
            row.tuningRules = fresh.tuningRulesVersion;
            row.dialogueRules = fresh.dialogueRulesVersion;
            if (fresh.dialogueRulesVersion != 0) { row.error = "the season starts on dialogue rules " + fresh.dialogueRulesVersion + ", not 0"; return; }
            if (EpisodeEngine.RolesOn(fresh) != arm.roles) { row.error = "the role moments are " + (arm.roles ? "off" : "on") + " in arm " + arm.name; return; }
            if (!EpisodeValidation.TryValidate(fresh, out string freshError)) { row.error = "the fresh state is invalid: " + freshError; return; }

            var peaks = new Peaks(fresh);
            var engine = new EpisodeEngine(fresh);
            var world = run.cell.npcTicks > 0 ? new BalanceLabNpcWorld(engine, run.cell.npcTicks, run) : null;
            if (world != null) world.Watch = (before, operated, kind) => peaks.See(operated, "the NPC world's " + kind);
            Func<EpisodeEngine> engineOf = world == null ? (Func<EpisodeEngine>)(() => engine) : world.Current;
            var agent = BalancePolicies.Create(run.cell.policy);
            var s = engine.Snapshot;
            while (s.phase != EpisodePhase.Finished && run.commands < BalanceLab.CommandCap && peaks.error == null)
            {
                // A slice of the phase's NPC time before the decision, as the lab spends it.
                if (world != null && world.SpendSlice()) s = world.Engine.Snapshot;
                var applied = BalanceLab.Step(engineOf, world == null ? (Func<bool>)null : world.SpendRest, agent, s, run, out var used, out var at);
                s = at;
                if (!applied.accepted)
                {
                    row.error = "week " + s.week + " " + s.phase + "/" + s.evictionStage + ": the walker's " + used.kind + " was refused: " + applied.reason;
                    break;
                }
                run.commands++;
                var after = applied.state;
                peaks.See(after, used.kind.ToString());
                row.weeks = Math.Max(row.weeks, after.week);
                if (row.playerOutWeek == 0 && Left(after.Find(after.playerId).status)) row.playerOutWeek = after.week;
                if ((SeasonAutopsy.IsPhaseChange(s, after) || used.kind == EpisodeCommandKind.Advance) && !EpisodeValidation.TryValidate(after, out string invalid))
                {
                    row.error = "week " + after.week + " " + after.phase + " after " + used.kind + ": invalid: " + invalid;
                    break;
                }
                s = after;
            }
            if (row.error == null) row.error = peaks.error;
            if (row.error == null && s.phase != EpisodePhase.Finished) row.error = "did not finish in " + run.commands + " commands (week " + s.week + " " + s.phase + ")";
            if (row.error == null && !EpisodeValidation.TryValidate(s, out string finalError)) row.error = "the final state is invalid: " + finalError;
            row.peakStorylines = peaks.storylines;
            row.peakHouseEvents = peaks.houseEvents;
            row.finalStorylines = s.storylines.Count;
            row.finalHouseEvents = s.houseEvents.Count;
            if (row.error == null && (row.finalStorylines != row.peakStorylines || row.finalHouseEvents != row.peakHouseEvents))
                row.error = "the final counts (" + row.finalStorylines + " storylines, " + row.finalHouseEvents + " house events) are not the peaks ("
                    + row.peakStorylines + ", " + row.peakHouseEvents + ")";
        }

        // ---------------------------------------------------------------- the budget and the summary

        /// <summary>The budget a pair of peaks allows (§5.5): min(rules 2's budget, 92 - storylines, floor((400 - events) / 5)).</summary>
        internal static int Budget(int storylines, int houseEvents) =>
            Math.Min(DialogueRules.ConversationBudget(DialogueRules.Rules2), Math.Min(DialogueRules.Rules2StorylineCeiling - storylines,
                (int)Math.Floor((DialogueRules.MaximumEvents - houseEvents) / (double)DialogueRules.StepsFor(DialogueRules.Rules2))));

        private static string BudgetText(int budget) => budget < 1 ? budget.ToString(CultureInfo.InvariantCulture) + ", so no budget fits" : budget.ToString(CultureInfo.InvariantCulture);

        /// <summary>The q-th quantile, linear between the order statistics (Excel's PERCENTILE.INC); NaN for no values.</summary>
        internal static double Percentile(IEnumerable<double> values, double q)
        {
            var sorted = values.OrderBy(v => v).ToList();
            if (sorted.Count == 0) return double.NaN;
            double at = (sorted.Count - 1) * q;
            int low = (int)Math.Floor(at), high = Math.Min(sorted.Count - 1, low + 1);
            return sorted[low] + (at - low) * (sorted[high] - sorted[low]);
        }

        /// <summary>One line an arm (and NPC budget): its house, its seasons, both peaks' maximum and p99, and the budget each allows.</summary>
        internal static List<string> Summary(IEnumerable<Row> rows)
        {
            var lines = new List<string>();
            foreach (var g in rows.GroupBy(r => (r.arm, r.ticks)).OrderBy(g => g.Key.arm, StringComparer.Ordinal).ThenBy(g => g.Key.ticks))
            {
                var ok = g.Where(r => r.error == null).ToList();
                string Values<T>(Func<Row, T> of) => string.Join("/", g.Select(of).Distinct().OrderBy(v => v).Select(v => Convert.ToString(v, CultureInfo.InvariantCulture)));
                string about = Arms.FirstOrDefault(a => a.name == g.Key.arm)?.about ?? "?";
                var line = new StringBuilder("arm " + g.Key.arm + " (" + about + "): " + Values(r => r.size) + " houseguests, " + Values(r => r.cast) + " cast, story rules "
                    + Values(r => r.storyRules) + ", tuning rules " + Values(r => r.tuningRules) + ", dialogue rules " + Values(r => r.dialogueRules) + ", "
                    + g.Key.ticks + " NPC ticks a week; " + ok.Count + " seasons (" + Values(r => r.policy) + "; " + g.Select(r => r.index).Distinct().Count() + " seeds, indices "
                    + g.Min(r => r.index) + ".." + g.Max(r => r.index) + ")");
                int errors = g.Count() - ok.Count;
                if (errors > 0) line.Append(", " + errors + " with errors, left out");
                if (ok.Count == 0) { lines.Add(line.Append("; no season finished without an error").ToString()); continue; }
                int maxLines = ok.Max(r => r.peakStorylines), maxEvents = ok.Max(r => r.peakHouseEvents);
                double p99Lines = Percentile(ok.Select(r => (double)r.peakStorylines), 0.99), p99Events = Percentile(ok.Select(r => (double)r.peakHouseEvents), 0.99);
                line.Append("; storylines max " + maxLines + ", p99 " + BalanceLab.Num(p99Lines, "0.0") + ", mean " + BalanceLab.Num(ok.Average(r => r.peakStorylines), "0.0"));
                line.Append("; house events max " + maxEvents + ", p99 " + BalanceLab.Num(p99Events, "0.0") + ", mean " + BalanceLab.Num(ok.Average(r => r.peakHouseEvents), "0.0"));
                line.Append("; weeks " + ok.Min(r => r.weeks) + ".." + ok.Max(r => r.weeks));
                line.Append("; budget min(" + DialogueRules.ConversationBudget(DialogueRules.Rules2) + ", " + DialogueRules.Rules2StorylineCeiling + " - " + maxLines + ", floor(("
                    + DialogueRules.MaximumEvents + " - " + maxEvents + ") / " + DialogueRules.StepsFor(DialogueRules.Rules2) + ")) = " + BudgetText(Budget(maxLines, maxEvents)));
                line.Append(" (at the p99s rounded up: " + BudgetText(Budget((int)Math.Ceiling(p99Lines), (int)Math.Ceiling(p99Events))) + ")");
                line.Append("; " + BalanceLab.Num(g.Average(r => r.seconds), "0.0") + " s a season (most " + BalanceLab.Num(g.Max(r => r.seconds), "0.0") + ")");
                lines.Add(line.ToString());
            }
            return lines;
        }

        /// <summary>
        /// Every part's rows in the folder, of this stamp (a season played twice counts once, as its latest file has it),
        /// summed per arm into summary.txt; returns the text.
        /// </summary>
        internal static string WriteSummary(string folder)
        {
            var files = Directory.GetFiles(folder, "*.jsonl").OrderBy(f => File.GetLastWriteTimeUtc(f)).ThenBy(f => f, StringComparer.Ordinal).ToList();
            var latest = new Dictionary<string, Row>(StringComparer.Ordinal);
            foreach (string file in files)
                foreach (string json in File.ReadAllLines(file).Where(l => l.Trim().Length > 0))
                {
                    var row = JsonConvert.DeserializeObject<Row>(json, Settings);
                    if (row?.probe != Stamp) continue;
                    latest[row.arm + "/" + row.ticks + "/" + row.policy + "/" + row.index] = row;
                }
            var text = new StringBuilder();
            text.AppendLine("P1(d) summary (" + Stamp + "): " + latest.Count + " seasons from " + files.Count + " rows file(s) in " + folder + ".");
            foreach (string line in Summary(latest.Values)) text.AppendLine(line);
            File.WriteAllText(Path.Combine(folder, "summary.txt"), text.ToString(), new UTF8Encoding(false));
            return text.ToString();
        }

        // ---------------------------------------------------------------- the knobs and the rows

        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings { Culture = CultureInfo.InvariantCulture, FloatParseHandling = FloatParseHandling.Double };

        private static string Json(Row row) => JsonConvert.SerializeObject(row, Formatting.None, Settings);

        private static string DefaultFolder() => Path.Combine(TestContext.CurrentContext.WorkDirectory, "balance", "p1d");

        private static List<Arm> ArmsFromEnvironment()
        {
            var names = ListFromEnvironment("BALANCE_P1D_ARMS", Arms.Select(a => a.name).ToArray());
            var arms = new List<Arm>();
            foreach (string name in names)
            {
                var arm = Arms.FirstOrDefault(a => string.Equals(a.name, name, StringComparison.OrdinalIgnoreCase));
                Assert.That(arm, Is.Not.Null, "No such arm: " + name + " (A, B, C or D).");
                if (!arms.Contains(arm)) arms.Add(arm);
            }
            return arms;
        }

        private static string[] ListFromEnvironment(string name, string[] fallback)
        {
            string value = Environment.GetEnvironmentVariable(name);
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Split(',').Select(x => x.Trim()).Where(x => x.Length > 0).ToArray();
        }

        /// <summary>A whole number of nought or more from the environment, or the fallback.</summary>
        private static int CountFromEnvironment(string name, int fallback) =>
            int.TryParse(Environment.GetEnvironmentVariable(name), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) && value >= 0 ? value : fallback;
    }
}
#endif
