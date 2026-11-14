#if !UNITY_5_3_OR_NEWER
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Gamesim.Simulation;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// A tier played in parts (BALANCE plan B6b): a long tier - the headline at hundreds of seeds, the NPC budgets,
    /// the projections - is played a range of seed indices at a time (<see cref="PartReport"/>), each part's seasons
    /// saved whole, and the parts merged into the tier's rows and tables (<see cref="MergeReport"/>) exactly as one
    /// run would have written them: every season's seed is its house and index, never the part it was played in.
    ///
    /// <para>BALANCE_TIER names the tier (<see cref="BalanceLabReports.Tiers"/>); BALANCE_FROM and BALANCE_COUNT the
    /// part's indices; BALANCE_PARTS the folder the parts go in (by default balance/parts/&lt;tier&gt; under the test's
    /// work directory). The merge refuses parts of another build of the lab (their stamp: the rules, the cells and
    /// <see cref="Stamp"/>), overlapping parts and a gap.</para>
    /// </summary>
    public sealed class BalanceLabParts
    {
        /// <summary>
        /// Changed whenever what a season records changes, so parts of two builds never merge. v4: a completed NPC
        /// conversation moves its pair through the ledger under D2's rules (the arcs fix), which the rules' tuple does not show.
        /// v5: so do a veto's, a lie's and a scheme's moves between two houseguests (EpisodeEngine.ChangeBetween), at every budget.
        /// v6: a season carries its pact watch and its own time (ALL-STAR-HISTORIES-PLAN 14.L.H, <see cref="BalanceLab.PactWatch"/>).
        /// v7: the watch's record gains its founders out, its final scores' hash and its own error (S4's review).
        /// v8: B9's tuning bands. v9: story rules 10's measure (STORY-RULES-10-PLAN §5): a cell's story rules, the pariah
        /// record's holders' rows, veto and player in the house, DP's own record, D1 and D2, and the pace's DP.
        /// v10: R7's A1 records (R7 build plan C1): each week's prompts, answers, role-week flags and minutes
        /// (<see cref="BalanceLab.WeekPrompts"/>), the season's storylines at its end and at their peak, and a cell's board
        /// answering (<see cref="BalanceLab.Cell.answersBoards"/>).
        /// </summary>
        internal const string Stamp = "balance-lab-parts/v10";

        private static BalanceLabReports.TierSpec TierFromEnvironment()
        {
            string name = Environment.GetEnvironmentVariable("BALANCE_TIER") ?? "headline";
            Assert.That(BalanceLabReports.Tiers.ContainsKey(name), Is.True, "No such tier: " + name);
            return BalanceLabReports.Tiers[name];
        }

        private static string Folder(BalanceLabReports.TierSpec tier)
        {
            string folder = Environment.GetEnvironmentVariable("BALANCE_PARTS");
            if (string.IsNullOrEmpty(folder)) folder = Path.Combine(TestContext.CurrentContext.WorkDirectory, "balance", "parts", tier.name);
            Directory.CreateDirectory(folder);
            return folder;
        }

        /// <summary>The stamp a part carries: the lab's, the shipped rules a fresh season takes, and the tier's cells.</summary>
        private static string StampOf(BalanceLabReports.TierSpec tier)
        {
            var fresh = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, 1u);
            ShippedRules.ApplyFresh(fresh);
            return Stamp + " | " + string.Join(",", ShippedRules.Fields(fresh).Select(f => f.Key + "=" + f.Value.ToString(CultureInfo.InvariantCulture)))
                + " | " + string.Join(",", CellsOf(tier).Select(c => c.Key));
        }

        /// <summary>
        /// A tier's cells: as the tier names them, or every one held at tuning 0 when BALANCE_TUNING is 0 (B9's control,
        /// B9-BALANCE-PLAN decision 8), so a tier replays the seasons it played before B9; and every one played at the
        /// story rules BALANCE_STORY names (<see cref="StoryRulesFromEnvironment"/>), so a milestone is measured before it
        /// is enrolled; and with BALANCE_BOARDS=1 every one's player answering the role boards (<see cref="BoardsFromEnvironment"/>,
        /// R7's A2). The held, opted-in and answering cells' names say so, and so does the stamp.
        /// </summary>
        internal static List<BalanceLab.Cell> CellsOf(BalanceLabReports.TierSpec tier)
        {
            var cells = tier.cells();
            if (Environment.GetEnvironmentVariable("BALANCE_TUNING") == "0") cells = cells.Select(c => c.HeldAtTuning0()).ToList();
            int story = StoryRulesFromEnvironment();
            if (story != 0) cells = cells.Select(c => c.AtStoryRules(story)).ToList();
            return BoardsFromEnvironment() ? cells.Select(c => c.AnsweringBoards()).ToList() : cells;
        }

        /// <summary>
        /// BALANCE_BOARDS: whether a tier's players answer the role boards (<see cref="BalanceLab.Cell.answersBoards"/>). Unset,
        /// empty or 0 leaves them unanswered, as every tier always has; 1 answers them; anything else stops the run here.
        /// </summary>
        internal static bool BoardsFromEnvironment() => Boards(Environment.GetEnvironmentVariable("BALANCE_BOARDS"));

        /// <summary><see cref="BoardsFromEnvironment"/>'s reading of a value.</summary>
        internal static bool Boards(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Trim() == "0") return false;
            if (value.Trim() == "1") return true;
            throw new ArgumentException("BALANCE_BOARDS is 0 or 1, not " + value);
        }

        /// <summary>
        /// BALANCE_STORY: the story rules a tier's cells play at (<see cref="BalanceLab.Cell.storyRules"/>). Unset, empty
        /// or 0 plays EnableStory's own, as every tier always has; anything else must be a version this build defines
        /// from Threads on other than Current (<see cref="BalanceLab.CheckedStoryRules"/>), or the run stops here.
        /// </summary>
        internal static int StoryRulesFromEnvironment()
        {
            string value = Environment.GetEnvironmentVariable("BALANCE_STORY");
            if (string.IsNullOrWhiteSpace(value)) return 0;
            if (!int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int version))
                throw new ArgumentException("BALANCE_STORY is not a whole number: " + value);
            return BalanceLab.CheckedStoryRules(version);
        }

        [Test, Explicit("Plays one part of a tier: BALANCE_TIER, seed indices BALANCE_FROM to BALANCE_FROM + BALANCE_COUNT - 1, saved under BALANCE_PARTS. Run by name.")]
        public void PartReport()
        {
            var tier = TierFromEnvironment();
            int from = Math.Max(0, int.TryParse(Environment.GetEnvironmentVariable("BALANCE_FROM"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int f) ? f : 0);
            int count = BalanceLabReports.FromEnvironment("BALANCE_COUNT", 10);
            var clock = BalanceLab.Clock();
            var runs = BalanceLab.RunPart(CellsOf(tier), from, count);
            double minutes = clock.Elapsed.TotalMinutes;
            var part = new Part { stamp = StampOf(tier), tier = tier.name, from = from, count = count, minutes = minutes, runs = runs.Select(Record.Of).ToList() };
            string path = Path.Combine(Folder(tier), "part-" + from.ToString("D5", CultureInfo.InvariantCulture) + "-" + (from + count).ToString("D5", CultureInfo.InvariantCulture) + ".json");
            File.WriteAllText(path, JsonConvert.SerializeObject(part, Formatting.None, Settings), new UTF8Encoding(false));
            TestContext.WriteLine(tier.name + " part " + from + ".." + (from + count - 1) + ": " + runs.Length + " seasons in " + BalanceLab.Num(minutes, "0.0") + " min ("
                + BalanceLab.Num(minutes * 60 / Math.Max(1, runs.Length) * Environment.ProcessorCount, "0.00") + " s a season on one thread); errors "
                + runs.Count(r => r.error != null) + "; saved " + path);
            Assert.That(runs.Where(r => r.error != null).Select(r => r.cell.Key + " #" + r.index + ": " + r.error).Take(10), Is.Empty);
        }

        [Test, Explicit("Merges a tier's parts (BALANCE_TIER, under BALANCE_PARTS) into its rows and tables. Run by name.")]
        public void MergeReport()
        {
            var tier = TierFromEnvironment();
            var parts = Directory.GetFiles(Folder(tier), "part-*.json").OrderBy(p => p, StringComparer.Ordinal)
                .Select(p => JsonConvert.DeserializeObject<Part>(File.ReadAllText(p), Settings)).ToList();
            Assert.That(parts, Is.Not.Empty, "No parts of " + tier.name + " to merge.");
            string stamp = StampOf(tier);
            Assert.That(parts.Where(p => p.stamp != stamp || p.tier != tier.name).Select(p => p.from + ".." + (p.from + p.count - 1)), Is.Empty, "Parts of another build of the lab.");
            int next = 0;
            foreach (var part in parts.OrderBy(p => p.from))
            {
                Assert.That(part.from, Is.EqualTo(next), "The parts cover the indices once each, from nought: a gap or an overlap at " + next + ".");
                next = part.from + part.count;
            }
            var cells = CellsOf(tier);
            var runs = parts.SelectMany(p => p.runs).Select(r => r.ToRun())
                .OrderBy(r => cells.FindIndex(c => c.Key == r.cell.Key)).ThenBy(r => r.index).ToList();
            Assert.That(runs.Count, Is.EqualTo(cells.Count * next), "Every cell's every season.");
            BalanceLabReports.Write(tier, runs, next, parts.Sum(p => p.minutes));
        }

        // ---------------------------------------------------------------- the saved season

        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings { Culture = CultureInfo.InvariantCulture, FloatParseHandling = FloatParseHandling.Double };

        /// <summary>A season saved as a part saves it and read back.</summary>
        internal static BalanceLab.SeasonRun RoundTrip(BalanceLab.SeasonRun run) =>
            JsonConvert.DeserializeObject<Record>(JsonConvert.SerializeObject(Record.Of(run), Formatting.None, Settings), Settings).ToRun();

        private sealed class Part
        {
            public string stamp, tier;
            public int from, count;
            public double minutes;
            public List<Record> runs = new List<Record>();
        }

        /// <summary>A season as the reports read it: <see cref="BalanceLab.SeasonRun"/>'s fields, the story's pace as its counts.</summary>
        internal sealed class Record
        {
            public BalanceLab.Cell cell;
            public int index;
            public uint seed;
            public string error;
            public SeasonAutopsy.Report autopsy;
            public PaceCounts pace;
            public int commands, own, fallbacks, refusals, freeActions;
            public SortedDictionary<string, int> refusalsByKind = new SortedDictionary<string, int>(StringComparer.Ordinal);
            public SortedDictionary<int, int> decisionsByWeek = new SortedDictionary<int, int>();
            public SortedDictionary<int, double> ceremonyByWeek = new SortedDictionary<int, double>();
            public double finaleSeconds;
            public List<BalanceLab.CounterMember> counterMembers = new List<BalanceLab.CounterMember>();
            public List<BalanceLab.CounterTally> counterTallies = new List<BalanceLab.CounterTally>();
            public int npcOps, npcTicks, npcStarts, npcScans, npcTries, npcHeld, npcRejected, pairs, pairsNearBound, pairsAtBound;
            public double npcMilliseconds;
            public SortedDictionary<string, double> gameSenseRows = new SortedDictionary<string, double>(StringComparer.Ordinal);
            public List<BalanceLab.NominationRecord> nominations = new List<BalanceLab.NominationRecord>();
            public BalanceLab.PariahRecord pariah;
            public BalanceLab.PariahRecord pariahInHouse;
            public bool pariahAtClosings, pariahBySize;
            public int preparation;
            public BalanceLab.PactRecord pactWatch;
            public double seasonMilliseconds;
            /// <summary>R7's A1 records (stamp v10): <see cref="BalanceLab.WeekPrompts"/> by week, and the season's storylines at its end and peak.</summary>
            public SortedDictionary<int, BalanceLab.WeekPrompts> promptsByWeek = new SortedDictionary<int, BalanceLab.WeekPrompts>();
            public int storylines, storylinesPeak;

            internal static Record Of(BalanceLab.SeasonRun r) => new Record
            {
                promptsByWeek = new SortedDictionary<int, BalanceLab.WeekPrompts>(r.promptsByWeek), storylines = r.storylines, storylinesPeak = r.storylinesPeak,
                cell = r.cell, index = r.index, seed = r.seed, error = r.error, autopsy = r.autopsy, pace = PaceCounts.Of(r.pace),
                commands = r.commands, own = r.own, fallbacks = r.fallbacks, refusals = r.refusals, freeActions = r.freeActions,
                refusalsByKind = new SortedDictionary<string, int>(r.refusalsByKind, StringComparer.Ordinal),
                decisionsByWeek = new SortedDictionary<int, int>(r.decisionsByWeek), ceremonyByWeek = new SortedDictionary<int, double>(r.ceremonyByWeek),
                finaleSeconds = r.finaleSeconds, counterMembers = r.counterMembers.ToList(), counterTallies = r.counterTallies.ToList(),
                npcOps = r.npcOps, npcTicks = r.npcTicks, npcStarts = r.npcStarts, npcScans = r.npcScans, npcTries = r.npcTries, npcHeld = r.npcHeld,
                npcRejected = r.npcRejected, npcMilliseconds = r.npcMilliseconds, pairs = r.pairs, pairsNearBound = r.pairsNearBound, pairsAtBound = r.pairsAtBound,
                gameSenseRows = new SortedDictionary<string, double>(r.gameSenseRows, StringComparer.Ordinal), nominations = r.nominations.ToList(), pariah = r.pariah,
                pariahInHouse = r.pariahInHouse, pariahAtClosings = r.pariahAtClosings, pariahBySize = r.pariahBySize,
                preparation = r.preparation, pactWatch = r.pactWatch, seasonMilliseconds = r.seasonMilliseconds,
            };

            internal BalanceLab.SeasonRun ToRun()
            {
                var r = new BalanceLab.SeasonRun
                {
                    cell = cell, index = index, seed = seed, error = error, autopsy = autopsy, pace = pace?.ToPace(),
                    commands = commands, own = own, fallbacks = fallbacks, refusals = refusals, freeActions = freeActions, finaleSeconds = finaleSeconds,
                    npcOps = npcOps, npcTicks = npcTicks, npcStarts = npcStarts, npcScans = npcScans, npcTries = npcTries, npcHeld = npcHeld,
                    npcRejected = npcRejected, npcMilliseconds = npcMilliseconds, pairs = pairs, pairsNearBound = pairsNearBound, pairsAtBound = pairsAtBound,
                    pariah = pariah, pariahInHouse = pariahInHouse, pariahAtClosings = pariahAtClosings, pariahBySize = pariahBySize,
                    preparation = preparation, pactWatch = pactWatch, seasonMilliseconds = seasonMilliseconds, storylines = storylines, storylinesPeak = storylinesPeak,
                };
                foreach (var p in promptsByWeek ?? new SortedDictionary<int, BalanceLab.WeekPrompts>()) r.promptsByWeek[p.Key] = p.Value;
                foreach (var p in gameSenseRows ?? new SortedDictionary<string, double>()) r.gameSenseRows[p.Key] = p.Value;
                r.nominations.AddRange(nominations ?? new List<BalanceLab.NominationRecord>());
                foreach (var p in refusalsByKind) r.refusalsByKind[p.Key] = p.Value;
                foreach (var p in decisionsByWeek) r.decisionsByWeek[p.Key] = p.Value;
                foreach (var p in ceremonyByWeek) r.ceremonyByWeek[p.Key] = p.Value;
                r.counterMembers.AddRange(counterMembers ?? new List<BalanceLab.CounterMember>());
                r.counterTallies.AddRange(counterTallies ?? new List<BalanceLab.CounterTally>());
                return r;
            }
        }

        /// <summary>The story pacing report's counts of a season (<see cref="StoryPacingTests.Pace"/>), without its watch's own notes.</summary>
        internal sealed class PaceCounts
        {
            public int weeks, weeksWithACard, asks, budgetedAsks, summons, showmances, npcRemovals, pileOns, arcsFinished, storiesFinished, playOffers;
            public bool removalWindow, pariah, pariahInHouse;

            internal static PaceCounts Of(StoryPacingTests.Pace p) => p == null ? null : new PaceCounts
            {
                weeks = p.weeks, weeksWithACard = p.weeksWithACard, asks = p.asks, budgetedAsks = p.budgetedAsks, summons = p.summons, showmances = p.showmances,
                npcRemovals = p.npcRemovals, pileOns = p.pileOns, arcsFinished = p.arcsFinished, storiesFinished = p.storiesFinished, playOffers = p.playOffers,
                removalWindow = p.removalWindow, pariah = p.pariah, pariahInHouse = p.pariahInHouse,
            };

            internal StoryPacingTests.Pace ToPace() => new StoryPacingTests.Pace
            {
                weeks = weeks, weeksWithACard = weeksWithACard, asks = asks, budgetedAsks = budgetedAsks, summons = summons, showmances = showmances,
                npcRemovals = npcRemovals, pileOns = pileOns, arcsFinished = arcsFinished, storiesFinished = storiesFinished, playOffers = playOffers,
                removalWindow = removalWindow, pariah = pariah, pariahInHouse = pariahInHouse,
            };
        }
    }
}
#endif
