#if !UNITY_5_3_OR_NEWER
// The balance lab's goldens (BALANCE plan B6b): Tools/SimulationTests only. Unity's batch runner executes
// [Explicit] tests, so the file is compiled out of the editor's assemblies.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The headline's seasons, pinned (BALANCE plan B6b): every headline cell - the twelve players at eight and
    /// twelve - at NPC budget nought and at 300 ticks a week (the lead's decision 1), its twenty seasons' rows
    /// digested as the season digests digest a season (16 hex of SHA-256, <see cref="BalanceLabTests.Hash"/>).
    ///
    /// <para><b>The rule.</b> A commit that changes any <c>Current</c> constant, <see cref="ShippedRules.ApplyFresh"/>,
    /// or anything a fresh season plays, moves these: it re-records them (run each case, copy the lines it prints)
    /// and says why in its message, as it re-records <see cref="BalanceLabTests.BudgetNoughtRows"/>. <see cref="Rules"/>
    /// is the shipped rules' tuple when they were recorded, so a moved digest says first whether the rules moved.</para>
    ///
    /// <para>Explicit, in eight tests - budget nought a size each, budget 300 a size and four players each - so each
    /// fits one ten-minute command: <c>dotnet test Tools/SimulationTests --filter "FullyQualifiedName~BalanceLabGoldens.Budget300Size12First"</c>.
    /// Budget 300 costs: every NPC operation validates the whole season twice (B5b).</para>
    /// </summary>
    public sealed class BalanceLabGoldens
    {
        /// <summary>The seasons a cell's digest covers.</summary>
        internal const int Seeds = 20;

        /// <summary>
        /// <see cref="ShippedRules.Fields"/> of a fresh season when the digests below were recorded. dialogueRulesVersion=0
        /// came with the field (9d87b59e): a fresh season plays no dialogue, and no cell moved. alumniRulesVersion=0 and
        /// tuningRulesVersion=0 came with schema 30 (SCH30): a fresh season plays neither, and no cell moved.
        /// tuningRulesVersion=1 came with B9 (B9-BALANCE-PLAN decision 8): every fresh season plays the balance bands.
        /// </summary>
        internal const string Rules = "competitionRulesVersion=4,readRulesStartWeek=1,leverRulesStartWeek=1,weekRulesStartWeek=1,economyRulesVersion=1,"
            + "agencyRulesStartWeek=1,finaleRulesStartWeek=1,commitmentRulesStartWeek=1,unifiedCommitmentRulesVersion=2,unifiedHearingRulesVersion=1,"
            + "blocRulesStartWeek=1,socialBudgetRulesStartWeek=1,dealRulesStartWeek=1,eventRulesStartWeek=1,storyRulesStartWeek=1,haveNotRulesStartWeek=1,"
            + "strategyRulesStartWeek=1,allianceLeakRulesStartWeek=1,pactPlanRulesStartWeek=1,allWeekRulesStartWeek=1,dialogueRulesVersion=0,"
            + "alumniRulesVersion=0,tuningRulesVersion=1,"
            + "npcSocial.rulesStartWeek=1,story.rulesStartWeek=1,story.rulesVersion=9";

        /// <summary>
        /// Each cell's digest: "policy/roster/size/npcN hash". Recorded with every shipped rule on: the war rooms played,
        /// D3's counter reach at twenty from a view of ten, D2's all-week rules from week one, the unified vote rules
        /// (unifiedCommitmentRulesVersion 2, vote family V6, with the lab's player view and skilled oracle reading mode 2's
        /// offers), (budget 300) a completed NPC conversation moving its pair through the ledger with no arc (the balance
        /// review's finding 3), and (every budget) a veto's, a lie's and a scheme's moves between two houseguests doing the
        /// same (the arcs review's finding 8, EpisodeEngine.ChangeBetween). Every cell moved when D2 went into ApplyFresh and
        /// again with V6; the budget-300 cells moved again with the arcs fix; every cell moved again with the change paths.
        /// Every cell moved again with B9's tuning rules 1 (B9-BALANCE-PLAN decision 8), re-recorded at B9-3: the Game Sense
        /// weights re-read every season's verdict, and the studying and countering players play B9's preparation and tally.
        /// </summary>
        internal static readonly string[] Recorded =
        {
            "passive/Regular/8/npc0 b1811c8b0b95635c",
            "random/Regular/8/npc0 4a5bc2ad067c022b",
            "social/Regular/8/npc0 5e215abcbe270332",
            "reader/Regular/8/npc0 40ad827bb2229d65",
            "schemer/Regular/8/npc0 ef7fe4f603a0d62f",
            "loyalist/Regular/8/npc0 3531b4b19d087131",
            "floater/Regular/8/npc0 7d9447d181fc954d",
            "beast/Regular/8/npc0 668b1534b2ae285f",
            "novice/Regular/8/npc0 8518823a4ed0aabd",
            "exploit/Regular/8/npc0 fb3fe1e326ef1903",
            "oracle-reader/Regular/8/npc0 fbe3c4638077b0b0",
            "oracle-skilled/Regular/8/npc0 129b40399003928b",
            "passive/Regular/12/npc0 15cb0fcee29a7e4d",
            "random/Regular/12/npc0 c4468689e0161f31",
            "social/Regular/12/npc0 14c5a0939d897a83",
            "reader/Regular/12/npc0 b8a17f3177bf69a4",
            "schemer/Regular/12/npc0 2679ce84f39b4d12",
            "loyalist/Regular/12/npc0 159a44090caa84c0",
            "floater/Regular/12/npc0 b5f09357202def57",
            "beast/Regular/12/npc0 1baa66be158a9e65",
            "novice/Regular/12/npc0 8c23a082e737f7fe",
            "exploit/Regular/12/npc0 f10c3f6fdf3ccc39",
            "oracle-reader/Regular/12/npc0 a5c434f8825a2ace",
            "oracle-skilled/Regular/12/npc0 a3df71289db87b8e",
            "passive/Regular/8/npc300 a505b5e6564f9710",
            "random/Regular/8/npc300 69e091335a7b03d8",
            "social/Regular/8/npc300 e7e1f5621e68b5f5",
            "reader/Regular/8/npc300 620023669861fdae",
            "schemer/Regular/8/npc300 6f00f9450877cb5d",
            "loyalist/Regular/8/npc300 970250bb134c7075",
            "floater/Regular/8/npc300 df2c5106eed3a3df",
            "beast/Regular/8/npc300 a3910d828298a006",
            "novice/Regular/8/npc300 2ef29f4836d3c737",
            "exploit/Regular/8/npc300 fa06987417c36a7e",
            "oracle-reader/Regular/8/npc300 434f1900aedef470",
            "oracle-skilled/Regular/8/npc300 e79cdaec1db2a1f4",
            "passive/Regular/12/npc300 032cf812140aeae7",
            "random/Regular/12/npc300 9ab37701436d410b",
            "social/Regular/12/npc300 7c068a161a094c4d",
            "reader/Regular/12/npc300 04b255f5380e2ca2",
            "schemer/Regular/12/npc300 461b6dcd8157c558",
            "loyalist/Regular/12/npc300 2e2e2955d68ff9c7",
            "floater/Regular/12/npc300 c824a8db7e9c6c50",
            "beast/Regular/12/npc300 b0708bf0f4934257",
            "novice/Regular/12/npc300 41fbff1b1b13438e",
            "exploit/Regular/12/npc300 9438c82a02e30789",
            "oracle-reader/Regular/12/npc300 e538646c7735358a",
            "oracle-skilled/Regular/12/npc300 283de34d715acc48",
        };

        /// <summary>The shipped rules' tuple as a fresh season takes it now.</summary>
        internal static string RulesNow()
        {
            var fresh = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, 1u);
            ShippedRules.ApplyFresh(fresh);
            return string.Join(",", ShippedRules.Fields(fresh).Select(f => f.Key + "=" + f.Value.ToString(CultureInfo.InvariantCulture)));
        }

        private const string Why = "B6b's goldens: one budget, size and four of the twelve players, 20 seasons a cell (budget 0 about a minute, 300 up to 8 min). Run by name.";
        [Test, Explicit(Why)] public void Budget0Size8() => Golden(0, 8, 0, 1, 2);
        [Test, Explicit(Why)] public void Budget0Size12() => Golden(0, 12, 0, 1, 2);
        [Test, Explicit(Why)] public void Budget300Size8First() => Golden(300, 8, 0);
        [Test, Explicit(Why)] public void Budget300Size8Second() => Golden(300, 8, 1);
        [Test, Explicit(Why)] public void Budget300Size8Third() => Golden(300, 8, 2);
        [Test, Explicit(Why)] public void Budget300Size12First() => Golden(300, 12, 0);
        [Test, Explicit(Why)] public void Budget300Size12Second() => Golden(300, 12, 1);
        [Test, Explicit(Why)] public void Budget300Size12Third() => Golden(300, 12, 2);

        /// <summary>Plays thirds of the headline's players at a budget and size and checks every cell's digest against its recording.</summary>
        private static void Golden(int budget, int size, params int[] thirds)
        {
            string rules = RulesNow();
            TestContext.WriteLine("Rules: " + rules);
            var players = BalancePolicies.All.Where((_, i) => thirds.Contains(i * 3 / BalancePolicies.All.Length)).ToArray();
            var cells = BalanceLab.Grid(players, new[] { size }, npcTicks: budget);
            var runs = BalanceLab.Run(cells, Seeds);
            Assert.That(runs.Where(r => r.error != null).Select(r => r.cell.Key + " #" + r.index + ": " + r.error).Take(10), Is.Empty);
            var played = cells.Select(c => c.policy + "/" + c.House + " " + BalanceLabTests.Hash(BalanceLab.Jsonl(runs.Where(r => r.cell.Key == c.Key).OrderBy(r => r.index)))).ToList();
            foreach (string line in played) TestContext.WriteLine("            \"" + line + "\",");
            Assert.That(rules, Is.EqualTo(Rules), "The shipped rules moved since the goldens were recorded: re-record them and say why.");
            var moved = played.Where(line => !Recorded.Contains(line)).ToList();
            Assert.That(moved, Is.Empty, "Cells whose seasons moved (re-record with the reason, BALANCE plan B6b):\n" + string.Join("\n", moved));
        }
    }
}
#endif
