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
        /// <summary>Changed whenever what a season records changes, so parts of two builds never merge.</summary>
        internal const string Stamp = "balance-lab-parts/v1";

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
                + " | " + string.Join(",", tier.cells().Select(c => c.Key));
        }

        [Test, Explicit("Plays one part of a tier: BALANCE_TIER, seed indices BALANCE_FROM to BALANCE_FROM + BALANCE_COUNT - 1, saved under BALANCE_PARTS. Run by name.")]
        public void PartReport()
        {
            var tier = TierFromEnvironment();
            int from = Math.Max(0, int.TryParse(Environment.GetEnvironmentVariable("BALANCE_FROM"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int f) ? f : 0);
            int count = BalanceLabReports.FromEnvironment("BALANCE_COUNT", 10);
            var clock = BalanceLab.Clock();
            var runs = BalanceLab.RunPart(tier.cells(), from, count);
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
            var cells = tier.cells();
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

            internal static Record Of(BalanceLab.SeasonRun r) => new Record
            {
                cell = r.cell, index = r.index, seed = r.seed, error = r.error, autopsy = r.autopsy, pace = PaceCounts.Of(r.pace),
                commands = r.commands, own = r.own, fallbacks = r.fallbacks, refusals = r.refusals, freeActions = r.freeActions,
                refusalsByKind = new SortedDictionary<string, int>(r.refusalsByKind, StringComparer.Ordinal),
                decisionsByWeek = new SortedDictionary<int, int>(r.decisionsByWeek), ceremonyByWeek = new SortedDictionary<int, double>(r.ceremonyByWeek),
                finaleSeconds = r.finaleSeconds, counterMembers = r.counterMembers.ToList(),
            };

            internal BalanceLab.SeasonRun ToRun()
            {
                var r = new BalanceLab.SeasonRun
                {
                    cell = cell, index = index, seed = seed, error = error, autopsy = autopsy, pace = pace?.ToPace(),
                    commands = commands, own = own, fallbacks = fallbacks, refusals = refusals, freeActions = freeActions, finaleSeconds = finaleSeconds,
                };
                foreach (var p in refusalsByKind) r.refusalsByKind[p.Key] = p.Value;
                foreach (var p in decisionsByWeek) r.decisionsByWeek[p.Key] = p.Value;
                foreach (var p in ceremonyByWeek) r.ceremonyByWeek[p.Key] = p.Value;
                r.counterMembers.AddRange(counterMembers ?? new List<BalanceLab.CounterMember>());
                return r;
            }
        }

        /// <summary>The story pacing report's counts of a season (<see cref="StoryPacingTests.Pace"/>), without its watch's own notes.</summary>
        internal sealed class PaceCounts
        {
            public int weeks, weeksWithACard, asks, budgetedAsks, summons, showmances, npcRemovals, pileOns, arcsFinished, storiesFinished, playOffers;
            public bool removalWindow, pariah;

            internal static PaceCounts Of(StoryPacingTests.Pace p) => p == null ? null : new PaceCounts
            {
                weeks = p.weeks, weeksWithACard = p.weeksWithACard, asks = p.asks, budgetedAsks = p.budgetedAsks, summons = p.summons, showmances = p.showmances,
                npcRemovals = p.npcRemovals, pileOns = p.pileOns, arcsFinished = p.arcsFinished, storiesFinished = p.storiesFinished, playOffers = p.playOffers,
                removalWindow = p.removalWindow, pariah = p.pariah,
            };

            internal StoryPacingTests.Pace ToPace() => new StoryPacingTests.Pace
            {
                weeks = weeks, weeksWithACard = weeksWithACard, asks = asks, budgetedAsks = budgetedAsks, summons = summons, showmances = showmances,
                npcRemovals = npcRemovals, pileOns = pileOns, arcsFinished = arcsFinished, storiesFinished = storiesFinished, playOffers = playOffers,
                removalWindow = removalWindow, pariah = pariah,
            };
        }
    }
}
#endif
