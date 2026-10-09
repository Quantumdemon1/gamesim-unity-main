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
            + "agencyRulesStartWeek=1,finaleRulesStartWeek=1,commitmentRulesStartWeek=1,unifiedCommitmentRulesVersion=1,unifiedHearingRulesVersion=1,"
            + "blocRulesStartWeek=1,socialBudgetRulesStartWeek=1,dealRulesStartWeek=1,eventRulesStartWeek=1,storyRulesStartWeek=1,haveNotRulesStartWeek=1,"
            + "strategyRulesStartWeek=1,allianceLeakRulesStartWeek=1,pactPlanRulesStartWeek=1,allWeekRulesStartWeek=0,npcSocial.rulesStartWeek=1,"
            + "story.rulesStartWeek=1,story.rulesVersion=9";

        /// <summary>
        /// Each cell's digest: "policy/roster/size/npcN hash". Recorded with the war rooms played and D3's counter reach
        /// at twenty from a view of ten (re-recorded when it moved from eighteen: the war-room players' eleven cells).
        /// </summary>
        internal static readonly string[] Recorded =
        {
            "passive/Regular/8/npc0 1b405c79c3b6a14d",
            "random/Regular/8/npc0 b1c3e93c4fbe4c9c",
            "social/Regular/8/npc0 14e170517bd0918f",
            "reader/Regular/8/npc0 9f946759c9610e2f",
            "schemer/Regular/8/npc0 29471da96aee7816",
            "loyalist/Regular/8/npc0 fc5481ee5a1c0313",
            "floater/Regular/8/npc0 d562cf534b294edd",
            "beast/Regular/8/npc0 202a96a3eedf734b",
            "novice/Regular/8/npc0 a87d44ce47dfa83d",
            "exploit/Regular/8/npc0 b548c522943a309b",
            "oracle-reader/Regular/8/npc0 7f2727621b183e13",
            "oracle-skilled/Regular/8/npc0 72416c46a95850b1",
            "passive/Regular/12/npc0 75bcb38b179324e4",
            "random/Regular/12/npc0 0f25c43618c05f25",
            "social/Regular/12/npc0 9fb35ff6a1d3b804",
            "reader/Regular/12/npc0 2954a0e8c6bf0933",
            "schemer/Regular/12/npc0 aa4b8ec864dc35b8",
            "loyalist/Regular/12/npc0 1ce9a2b47cb39a45",
            "floater/Regular/12/npc0 575cca0fda23b400",
            "beast/Regular/12/npc0 f98caecb6d7f7c86",
            "novice/Regular/12/npc0 386fbb9197dba1e8",
            "exploit/Regular/12/npc0 c795c55264016e4c",
            "oracle-reader/Regular/12/npc0 c2a3967e8315494b",
            "oracle-skilled/Regular/12/npc0 59f1fb5910add447",
            "passive/Regular/8/npc300 62ca6d58ab8961f5",
            "random/Regular/8/npc300 1a041ee403734cec",
            "social/Regular/8/npc300 2fb493792a79c79b",
            "reader/Regular/8/npc300 63d93c2e7b1c4e84",
            "schemer/Regular/8/npc300 0daf8af2dabc013a",
            "loyalist/Regular/8/npc300 aecd6226fba347cf",
            "floater/Regular/8/npc300 9a866cd689c176fd",
            "beast/Regular/8/npc300 58bc055eb37ec865",
            "novice/Regular/8/npc300 633c86c784a35502",
            "exploit/Regular/8/npc300 e55d2e2d564913fb",
            "oracle-reader/Regular/8/npc300 f9eaff49b22c6272",
            "oracle-skilled/Regular/8/npc300 b17766d0858889ee",
            "passive/Regular/12/npc300 6f3f4ee3bd6bdbf4",
            "random/Regular/12/npc300 5c37e238f5884923",
            "social/Regular/12/npc300 1cada3efd96c8f16",
            "reader/Regular/12/npc300 b00adbef2ac7b7ab",
            "schemer/Regular/12/npc300 9c12d5245ffda077",
            "loyalist/Regular/12/npc300 d6a6f6fcdceb1852",
            "floater/Regular/12/npc300 4f9b96381641b7b5",
            "beast/Regular/12/npc300 6273019122a02c53",
            "novice/Regular/12/npc300 12a072d59d422a74",
            "exploit/Regular/12/npc300 9fd2c90569f93205",
            "oracle-reader/Regular/12/npc300 788958d99c7e2ff2",
            "oracle-skilled/Regular/12/npc300 bc774438e786b0e0",
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
