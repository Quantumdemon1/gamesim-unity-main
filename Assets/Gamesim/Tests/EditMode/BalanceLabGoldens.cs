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

        /// <summary><see cref="ShippedRules.Fields"/> of a fresh season when the digests below were recorded.</summary>
        internal const string Rules = "competitionRulesVersion=4,readRulesStartWeek=1,leverRulesStartWeek=1,weekRulesStartWeek=1,economyRulesVersion=1,"
            + "agencyRulesStartWeek=1,finaleRulesStartWeek=1,commitmentRulesStartWeek=1,unifiedCommitmentRulesVersion=2,unifiedHearingRulesVersion=1,"
            + "blocRulesStartWeek=1,socialBudgetRulesStartWeek=1,dealRulesStartWeek=1,eventRulesStartWeek=1,storyRulesStartWeek=1,haveNotRulesStartWeek=1,"
            + "strategyRulesStartWeek=1,allianceLeakRulesStartWeek=1,pactPlanRulesStartWeek=1,allWeekRulesStartWeek=1,npcSocial.rulesStartWeek=1,"
            + "story.rulesStartWeek=1,story.rulesVersion=9";

        /// <summary>
        /// Each cell's digest: "policy/roster/size/npcN hash". Recorded with every shipped rule on: the war rooms played,
        /// D3's counter reach at twenty from a view of ten, D2's all-week rules from week one, the unified vote rules
        /// (unifiedCommitmentRulesVersion 2, vote family V6, with the lab's player view and skilled oracle reading mode 2's
        /// offers), and (budget 300) a completed NPC conversation moving its pair through the ledger with no arc (the balance
        /// review's finding 3). Every cell moved when D2 went into ApplyFresh and again with V6; the budget-300 cells moved
        /// again with the arcs fix.
        /// </summary>
        internal static readonly string[] Recorded =
        {
            "passive/Regular/8/npc0 dd34b55c93c22593",
            "random/Regular/8/npc0 f5ba35d31aa5c421",
            "social/Regular/8/npc0 7fdd488992ee0d68",
            "reader/Regular/8/npc0 047d2ab28c2329b8",
            "schemer/Regular/8/npc0 f5a6dd25fd0216ec",
            "loyalist/Regular/8/npc0 5b4e0798244a2723",
            "floater/Regular/8/npc0 3b7b17a5a7b82d3c",
            "beast/Regular/8/npc0 621a7c0ed2d2eb83",
            "novice/Regular/8/npc0 8fff1c5489c84553",
            "exploit/Regular/8/npc0 70fa0803dad06598",
            "oracle-reader/Regular/8/npc0 98b56e71a16f6fbd",
            "oracle-skilled/Regular/8/npc0 f99467d29aced9af",
            "passive/Regular/12/npc0 76def56bbc757019",
            "random/Regular/12/npc0 a63c4f0794c468b7",
            "social/Regular/12/npc0 d4a6e78da312ce61",
            "reader/Regular/12/npc0 d8fcf425a7685d94",
            "schemer/Regular/12/npc0 6ae1b0cd57769ef6",
            "loyalist/Regular/12/npc0 980e8b709e89d0c1",
            "floater/Regular/12/npc0 9a8d117b0abde251",
            "beast/Regular/12/npc0 8a18d86721253bef",
            "novice/Regular/12/npc0 5e3eb55dc4d7fea9",
            "exploit/Regular/12/npc0 7a5426228dfeed8f",
            "oracle-reader/Regular/12/npc0 bf3f9183d2f42d95",
            "oracle-skilled/Regular/12/npc0 015de0c0bd3f1fe9",
            "passive/Regular/8/npc300 29cace4f15555984",
            "random/Regular/8/npc300 eefa3eaa5d7d9543",
            "social/Regular/8/npc300 55cf699723312b4d",
            "reader/Regular/8/npc300 b9a07710fffaa17b",
            "schemer/Regular/8/npc300 7f3c2d509447067b",
            "loyalist/Regular/8/npc300 a25a35e3bff8bfa6",
            "floater/Regular/8/npc300 51bfda505356b6c5",
            "beast/Regular/8/npc300 c2fa51eb43450c3a",
            "novice/Regular/8/npc300 2405773e87740604",
            "exploit/Regular/8/npc300 db7692366dbbb121",
            "oracle-reader/Regular/8/npc300 34dc320d60dfad3f",
            "oracle-skilled/Regular/8/npc300 0d03e8c1d2e23605",
            "passive/Regular/12/npc300 93b9031365c8f427",
            "random/Regular/12/npc300 e45cf6c997793b54",
            "social/Regular/12/npc300 8f26c61e6788c0ef",
            "reader/Regular/12/npc300 5c603a5061728f13",
            "schemer/Regular/12/npc300 e593a17f655c473d",
            "loyalist/Regular/12/npc300 03e3d5d23ef7d659",
            "floater/Regular/12/npc300 40223dab2e83b037",
            "beast/Regular/12/npc300 86eb69b71f936d83",
            "novice/Regular/12/npc300 7101fec82fa5b867",
            "exploit/Regular/12/npc300 3945a203b0b0ba86",
            "oracle-reader/Regular/12/npc300 ed0645b74af10560",
            "oracle-skilled/Regular/12/npc300 07b6c40d6ab3d158",
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
