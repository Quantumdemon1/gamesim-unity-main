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
        /// offers), (budget 300) a completed NPC conversation moving its pair through the ledger with no arc (the balance
        /// review's finding 3), and (every budget) a veto's, a lie's and a scheme's moves between two houseguests doing the
        /// same (the arcs review's finding 8, EpisodeEngine.ChangeBetween). Every cell moved when D2 went into ApplyFresh and
        /// again with V6; the budget-300 cells moved again with the arcs fix; every cell moved again with the change paths.
        /// </summary>
        internal static readonly string[] Recorded =
        {
            "passive/Regular/8/npc0 b37cafc96696903e",
            "random/Regular/8/npc0 8ef488be47080709",
            "social/Regular/8/npc0 53eeaa44532fd7f1",
            "reader/Regular/8/npc0 5e16fca5422d52ce",
            "schemer/Regular/8/npc0 b42c6d9fce90c8f4",
            "loyalist/Regular/8/npc0 09f3465308e715e3",
            "floater/Regular/8/npc0 f76090aa7b40bf0f",
            "beast/Regular/8/npc0 0b7fcf4f7e4608e6",
            "novice/Regular/8/npc0 72e7bc6226db9cda",
            "exploit/Regular/8/npc0 94e8fda2f2e66933",
            "oracle-reader/Regular/8/npc0 a74525454301730f",
            "oracle-skilled/Regular/8/npc0 9a8d509156ebc7e4",
            "passive/Regular/12/npc0 e54be2fdf3531462",
            "random/Regular/12/npc0 d3db8a9b38a483f7",
            "social/Regular/12/npc0 50ad3c5d9f699468",
            "reader/Regular/12/npc0 f496b93d7d7e5ab3",
            "schemer/Regular/12/npc0 1b117bfdbdc98439",
            "loyalist/Regular/12/npc0 8e42ebb9d4c66eca",
            "floater/Regular/12/npc0 3febfd9a8c1e25ac",
            "beast/Regular/12/npc0 cc59cb80d3606058",
            "novice/Regular/12/npc0 2761abf253718d5a",
            "exploit/Regular/12/npc0 70cfbad1e36accb2",
            "oracle-reader/Regular/12/npc0 8624c61c2935935c",
            "oracle-skilled/Regular/12/npc0 c9a6ce37f0fbc9be",
            "passive/Regular/8/npc300 d8a9aeccf2b04eb7",
            "random/Regular/8/npc300 5d455805a4718090",
            "social/Regular/8/npc300 e55ce43885024785",
            "reader/Regular/8/npc300 3c6993af9fcefde7",
            "schemer/Regular/8/npc300 ea348d2f24f38c0b",
            "loyalist/Regular/8/npc300 73383199d8adb540",
            "floater/Regular/8/npc300 8c4d59a9149c447c",
            "beast/Regular/8/npc300 ff777e305485034c",
            "novice/Regular/8/npc300 4a735a57214cb3b5",
            "exploit/Regular/8/npc300 7a532d85ea1b409c",
            "oracle-reader/Regular/8/npc300 aa7571858bd363e8",
            "oracle-skilled/Regular/8/npc300 524c71f83bea9aa3",
            "passive/Regular/12/npc300 c12665fbaf9a8290",
            "random/Regular/12/npc300 fc141897fcef4794",
            "social/Regular/12/npc300 e21cb70719ae521b",
            "reader/Regular/12/npc300 e210da97ce1b5702",
            "schemer/Regular/12/npc300 e1148e72d779d081",
            "loyalist/Regular/12/npc300 4ea89b6d4cb9e29a",
            "floater/Regular/12/npc300 ba774cc442461548",
            "beast/Regular/12/npc300 45044431cff86ba8",
            "novice/Regular/12/npc300 502f9c8008906531",
            "exploit/Regular/12/npc300 261544cd6853370d",
            "oracle-reader/Regular/12/npc300 569caa0f1a4890fc",
            "oracle-skilled/Regular/12/npc300 fa6b7f1ba5578ea7",
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
