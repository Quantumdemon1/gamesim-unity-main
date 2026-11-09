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
        /// </summary>
        internal const string Rules = "competitionRulesVersion=4,readRulesStartWeek=1,leverRulesStartWeek=1,weekRulesStartWeek=1,economyRulesVersion=1,"
            + "agencyRulesStartWeek=1,finaleRulesStartWeek=1,commitmentRulesStartWeek=1,unifiedCommitmentRulesVersion=2,unifiedHearingRulesVersion=1,"
            + "blocRulesStartWeek=1,socialBudgetRulesStartWeek=1,dealRulesStartWeek=1,eventRulesStartWeek=1,storyRulesStartWeek=1,haveNotRulesStartWeek=1,"
            + "strategyRulesStartWeek=1,allianceLeakRulesStartWeek=1,pactPlanRulesStartWeek=1,allWeekRulesStartWeek=1,dialogueRulesVersion=0,"
            + "alumniRulesVersion=0,tuningRulesVersion=1,"
            + "npcSocial.rulesStartWeek=1,story.rulesStartWeek=1,story.rulesVersion=10";

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
        /// and the seasons go on from those grudges.
        /// </summary>
        internal static readonly string[] Recorded =
        {
            "passive/Regular/8/npc0 63806f6ffbbd4512",
            "random/Regular/8/npc0 4cf2290b35b49c85",
            "social/Regular/8/npc0 133fd776ac35dbea",
            "reader/Regular/8/npc0 1eebbacf2db9c404",
            "schemer/Regular/8/npc0 e233c60f3b639ddd",
            "loyalist/Regular/8/npc0 1f011b99b02ccec3",
            "floater/Regular/8/npc0 36a6e75c4cd5eeb7",
            "beast/Regular/8/npc0 5423c8352b68d170",
            "novice/Regular/8/npc0 99a4b23a57ad2754",
            "exploit/Regular/8/npc0 271955ab730dbb1e",
            "oracle-reader/Regular/8/npc0 50ff9776b8c3d98f",
            "oracle-skilled/Regular/8/npc0 e68a9e83d372e8eb",
            "passive/Regular/12/npc0 91c79c688a5f1408",
            "random/Regular/12/npc0 65101ed8eaec30cc",
            "social/Regular/12/npc0 71ae1d30dcfdff43",
            "reader/Regular/12/npc0 337038a57bac4e4a",
            "schemer/Regular/12/npc0 95eb1de50fa9b083",
            "loyalist/Regular/12/npc0 8c5e5b920305068a",
            "floater/Regular/12/npc0 321778c4b92e8054",
            "beast/Regular/12/npc0 bf1282ab6222bd93",
            "novice/Regular/12/npc0 44e103295e9b8f42",
            "exploit/Regular/12/npc0 07242f8c395ed629",
            "oracle-reader/Regular/12/npc0 4184b16b85bc3b09",
            "oracle-skilled/Regular/12/npc0 eade59c5219c083e",
            "passive/Regular/8/npc300 0a81e851b2123d6e",
            "random/Regular/8/npc300 5d008a97c3601d20",
            "social/Regular/8/npc300 00890dd70fa85fb5",
            "reader/Regular/8/npc300 f6265a9c2294c47b",
            "schemer/Regular/8/npc300 cec3b0b889c3b55d",
            "loyalist/Regular/8/npc300 45495e041d230d24",
            "floater/Regular/8/npc300 0c33d94165da4159",
            "beast/Regular/8/npc300 a857ee516c1fbb21",
            "novice/Regular/8/npc300 658421ff009579f2",
            "exploit/Regular/8/npc300 53eb34d04dffe81e",
            "oracle-reader/Regular/8/npc300 a9ed1d65bc352cc4",
            "oracle-skilled/Regular/8/npc300 dc0332f3c5b2e633",
            "passive/Regular/12/npc300 f7809dcdca1a5b7c",
            "random/Regular/12/npc300 281d59b1c32809b5",
            "social/Regular/12/npc300 4eb1f872995849f7",
            "reader/Regular/12/npc300 4a768a056e12faa1",
            "schemer/Regular/12/npc300 b9b8d4df7c18f8ee",
            "loyalist/Regular/12/npc300 ec4c0a0778255f9a",
            "floater/Regular/12/npc300 7e09e58481039ad4",
            "beast/Regular/12/npc300 02165c22f97014db",
            "novice/Regular/12/npc300 b1e7f4d791ffcfe9",
            "exploit/Regular/12/npc300 a966f92afeb034e4",
            "oracle-reader/Regular/12/npc300 27e1ecbb6ca0991c",
            "oracle-skilled/Regular/12/npc300 570a1791fac49223",
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
