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
        /// story.rulesVersion=10 came with story rules 10's enrolment (STORY-RULES-10-PLAN §8): every fresh season plays Blame.
        /// story.rulesVersion=11 came with story rules 11's enrolment (ROLE-MOMENTS-PLAN R7): every fresh season plays Roles.
        /// </summary>
        internal const string Rules = "competitionRulesVersion=4,readRulesStartWeek=1,leverRulesStartWeek=1,weekRulesStartWeek=1,economyRulesVersion=1,"
            + "agencyRulesStartWeek=1,finaleRulesStartWeek=1,commitmentRulesStartWeek=1,unifiedCommitmentRulesVersion=2,unifiedHearingRulesVersion=1,"
            + "blocRulesStartWeek=1,socialBudgetRulesStartWeek=1,dealRulesStartWeek=1,eventRulesStartWeek=1,storyRulesStartWeek=1,haveNotRulesStartWeek=1,"
            + "strategyRulesStartWeek=1,allianceLeakRulesStartWeek=1,pactPlanRulesStartWeek=1,allWeekRulesStartWeek=1,dialogueRulesVersion=0,"
            + "alumniRulesVersion=0,tuningRulesVersion=1,"
            + "npcSocial.rulesStartWeek=1,story.rulesStartWeek=1,story.rulesVersion=11";

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
        /// 43 of the 48 moved again in B9's round 4 and were re-recorded at 46459d3e: the keyed jury tiebreak and the final
        /// Head of Household's half-a-tie read move every season whose jury ties or whose final choice a tie decides, and
        /// tuning 1 without the small house's boost moves the beast's. All 48 moved again with story rules 10's enrolment
        /// (STORY-RULES-10-PLAN §8) and were re-recorded at it: every fresh season starts on Blame, under which a houseguest
        /// who uses the veto is resented less by the replacement (nothing for a self-save, 35 for a save of somebody else),
        /// and the seasons go on from those grudges. All 48 moved again with story rules 11's enrolment (ROLE-MOMENTS-PLAN R7)
        /// and were re-recorded at it: every fresh season starts on Roles, under which the role moments play - the offer and
        /// pitch boards' costs, keep-the-block, the pawn talk and quiet tears - and the seasons go on from what they changed.
        /// </summary>
        internal static readonly string[] Recorded =
        {
            "passive/Regular/8/npc0 793c75032876d011",
            "random/Regular/8/npc0 c95a65956048eaf4",
            "social/Regular/8/npc0 521dd0c48f780b77",
            "reader/Regular/8/npc0 d5523fbc06d34ad1",
            "schemer/Regular/8/npc0 846eec0c91a69671",
            "loyalist/Regular/8/npc0 18058f456abc0e87",
            "floater/Regular/8/npc0 217cb344885a7cfd",
            "beast/Regular/8/npc0 2c267bcf21557934",
            "novice/Regular/8/npc0 151c7ca67d8cc6ab",
            "exploit/Regular/8/npc0 4b4fb65bdbbbac0d",
            "oracle-reader/Regular/8/npc0 f55ad73bce2a0317",
            "oracle-skilled/Regular/8/npc0 c45a31d5b8b014d9",
            "passive/Regular/12/npc0 c2657b1559c01f73",
            "random/Regular/12/npc0 a5bf441940905c68",
            "social/Regular/12/npc0 5bd50de396b9c5a4",
            "reader/Regular/12/npc0 b40129f82d7e4e64",
            "schemer/Regular/12/npc0 615c595ac70b06b3",
            "loyalist/Regular/12/npc0 1a0bd847f9ec563d",
            "floater/Regular/12/npc0 8631a4e0665513a7",
            "beast/Regular/12/npc0 88d9d5a0a55d38cd",
            "novice/Regular/12/npc0 fe438f71319bbffa",
            "exploit/Regular/12/npc0 bf864c335b708bd0",
            "oracle-reader/Regular/12/npc0 0611f0922e27e33b",
            "oracle-skilled/Regular/12/npc0 f13c58ad9c0294ca",
            "passive/Regular/8/npc300 e19cca6ff3f8a36a",
            "random/Regular/8/npc300 2f32a8f76503bb3b",
            "social/Regular/8/npc300 72bd344cdeb49ed8",
            "reader/Regular/8/npc300 a9db58d8b542eb4c",
            "schemer/Regular/8/npc300 7ffca01fcff340c1",
            "loyalist/Regular/8/npc300 21ec46dfb3b6770d",
            "floater/Regular/8/npc300 df45700c5e2d6ee4",
            "beast/Regular/8/npc300 0f8ece1d383b7e9c",
            "novice/Regular/8/npc300 1c364d1cee552c8e",
            "exploit/Regular/8/npc300 e1eabf3bcf6a2335",
            "oracle-reader/Regular/8/npc300 0268ec9466772d7c",
            "oracle-skilled/Regular/8/npc300 fc0dbb54de0aa2be",
            "passive/Regular/12/npc300 6c7700c16a1735be",
            "random/Regular/12/npc300 6a833161cca2d80a",
            "social/Regular/12/npc300 abc10a3c12cad1fd",
            "reader/Regular/12/npc300 5c245a18b2aeae65",
            "schemer/Regular/12/npc300 ea1dc458dece550f",
            "loyalist/Regular/12/npc300 04d7434313779e51",
            "floater/Regular/12/npc300 8db1345b8937851c",
            "beast/Regular/12/npc300 6483b9700ee15954",
            "novice/Regular/12/npc300 3b0a9cd2d1f8b548",
            "exploit/Regular/12/npc300 c3fd3a5a565c4dfa",
            "oracle-reader/Regular/12/npc300 96f800b516274b20",
            "oracle-skilled/Regular/12/npc300 9dc8e8e9dcaf6a23",
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
