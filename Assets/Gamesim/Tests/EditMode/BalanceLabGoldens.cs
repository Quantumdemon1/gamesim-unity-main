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
        /// competitionRulesVersion=5 came with the Ring Tower (competition rules 5, pull request 77): every fresh season deals
        /// the Ring Tower as every other mental competition. Recorded again on rules 5 on 2026-11-17: only this number moved;
        /// all 48 cells printed the digests below byte for byte (the rows keep a competition's category, not its game).
        /// story.talkRules=0 came with schema 31 (the free talk's S0, TalkRules): a fresh season plays no free talk, the
        /// field is written 0 and nothing reads it, so no cell moves; level 1's enrolment re-records the cells (ruling R11).
        /// story.mindRules=0 came with schema 31 too (MindRules, the housemates' minds): written 0 and read by nothing, so no
        /// cell moves; a level's enrolment re-records the cells.
        /// story.talkRules=1 came with level 1's enrolment (free talk slice 1, ruling R16): every fresh season offers the free
        /// "Just talk". Only the player's JustTalk command reads the level and no lab policy sends one, so the cells are the
        /// same seasons. Recorded again on talk rules 1 on 2026-11-19: only this number moved. All eight cases, run by name,
        /// passed (the digests below, byte for byte): seven on the slice's integration branch, and Budget300Size8Third, cut off
        /// there by a time limit, on the slice's merge with main.
        /// story.talkRules=2 came with level 2's enrolment (free talk slice 2, ruling R16): every fresh season plays chemistry and
        /// the friendship stages. No lab policy sends JustTalk, but paid talk builds rapport too, so a housemate can reach Friend
        /// in a lab season, and at level 2 a Friend answers a vote question more honestly and costs 8 more to nominate: a cell can
        /// move, and cells did: Budget0Size12, run by name at the slice's integration head (629c53de), printed six cells moved
        /// (social, schemer, floater, beast, oracle-reader, oracle-skilled at size 12, budget 0), recorded below; its other six
        /// printed their level-1 digests byte for byte. PROVISIONAL: the other seven cases (Budget0Size8 was cut off by a time
        /// limit; the six budget-300 cases were not run) still hold their level-1 digests until they are run by name.
        /// story.talkRules=3 came with level 3's enrolment (free talk slice 3, ruling R16): every fresh season plays romance with
        /// the player, the stances and the house's reaction. No lab policy sends JustTalk, so no lab player flirts; but at level 3
        /// Late Nights' opening beat is not offered and the Friends named are read before a nomination's closing beats (T9), so a
        /// cell can move. Budget0Size8 and Budget0Size12, run by name at the slice's integration head (cbea5601), printed eleven
        /// cells moved (size 8: random, social, schemer, loyalist, floater, beast, novice; size 12: random, social, novice,
        /// oracle-skilled), recorded below; their other thirteen printed the digests below byte for byte. (Budget0Size8 had not
        /// been run since level 1, so its moves are levels 2 and 3 together.) Budget300Size8First and Budget300Size8Second, run
        /// by name at 6e485ef6, printed five cells moved (social, schemer, loyalist, floater, beast at size 8, budget 300; levels
        /// 2 and 3 together), recorded below; their other three printed the digests below byte for byte. Budget300Size8Third and
        /// Budget300Size12First, run by name at 8d578f65, printed four cells moved (novice and oracle-skilled at size 8, random
        /// and social at size 12, budget 300), recorded below; their other four printed the digests below byte for byte.
        /// Budget300Size12Second and Budget300Size12Third, run by name at d687bbed, printed four cells moved (schemer, floater,
        /// beast and oracle-skilled at size 12, budget 300), recorded below; their other four printed the digests below byte
        /// for byte. All eight cases have now been run by name at level 3: nothing is provisional.
        /// competitionRulesVersion=6 came with the trivia's switch-on (competition rules 6): from week 4, after three evictions,
        /// a memory board a fresh season deals can be the trivia instead. A row names a competition's category, which the trivia
        /// shares with the board it replaces, and a season commits the same results under rules 5 and 6 (CompetitionRules6Tests),
        /// so only this number moves and no cell should.
        /// story.rulesVersion=12 came with story rules 12's enrolment (plan 32 A-ON): every fresh season plays the house talks
        /// back. All eight cases, run by name at the switch's head (00e83cd9), printed 29 of the 48 cells moved, recorded below
        /// (see <see cref="Recorded"/>); the other 19 printed their digests byte for byte.
        /// </summary>
        internal const string Rules = "competitionRulesVersion=6,readRulesStartWeek=1,leverRulesStartWeek=1,weekRulesStartWeek=1,economyRulesVersion=1,"
            + "agencyRulesStartWeek=1,finaleRulesStartWeek=1,commitmentRulesStartWeek=1,unifiedCommitmentRulesVersion=2,unifiedHearingRulesVersion=1,"
            + "blocRulesStartWeek=1,socialBudgetRulesStartWeek=1,dealRulesStartWeek=1,eventRulesStartWeek=1,storyRulesStartWeek=1,haveNotRulesStartWeek=1,"
            + "strategyRulesStartWeek=1,allianceLeakRulesStartWeek=1,pactPlanRulesStartWeek=1,allWeekRulesStartWeek=1,dialogueRulesVersion=0,"
            + "alumniRulesVersion=0,tuningRulesVersion=1,"
            + "npcSocial.rulesStartWeek=1,story.rulesStartWeek=1,story.rulesVersion=12,story.talkRules=3,story.mindRules=0";

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
        /// None of the 48 moved with competition rules 5, the Ring Tower (recorded again on 2026-11-17): a season commits the
        /// same results under rules 4 and 5, and a row names a competition's category, which the tower shares with the game
        /// it replaces. 29 of the 48 moved with story rules 12's enrolment (plan 32 A-ON) and were re-recorded at it: every
        /// fresh season starts on Manners, where a houseguest answers a vote question in a voice and a manner with a candour of
        /// its own and the reveal teaches tells, so the players who ask and read (random, social, reader, schemer, novice,
        /// exploit and the oracles) play on from different answers; the passive, loyal, floating and competing players, who
        /// never ask, kept every digest, as did three more cells (oracle-skilled 12 npc300, oracle-skilled 8 npc0 and schemer 8
        /// npc300). No lab player listens in, so overhearing moves none of them.
        /// </summary>
        internal static readonly string[] Recorded =
        {
            "passive/Regular/8/npc0 793c75032876d011",
            "random/Regular/8/npc0 36d770cd4a0727f0",
            "social/Regular/8/npc0 79e9c9e98a3de1a4",
            "reader/Regular/8/npc0 799d9ee467cab17c",
            "schemer/Regular/8/npc0 a16d203557b638a9",
            "loyalist/Regular/8/npc0 0b21feda66ef8b6c",
            "floater/Regular/8/npc0 7b2e7372bd87d9e3",
            "beast/Regular/8/npc0 e1c554852da5324a",
            "novice/Regular/8/npc0 e924e50665376a9b",
            "exploit/Regular/8/npc0 bf0d76c519098d18",
            "oracle-reader/Regular/8/npc0 d1642dbe615d5b92",
            "oracle-skilled/Regular/8/npc0 c45a31d5b8b014d9",
            "passive/Regular/12/npc0 c2657b1559c01f73",
            "random/Regular/12/npc0 3fed938bcd47ab65",
            "social/Regular/12/npc0 bc719ff654dd2ac2",
            "reader/Regular/12/npc0 fd1df52cbb287dbc",
            "schemer/Regular/12/npc0 b0d78b72fd398071",
            "loyalist/Regular/12/npc0 1a0bd847f9ec563d",
            "floater/Regular/12/npc0 ef5049109a043fb0",
            "beast/Regular/12/npc0 c6e7db942219b6fa",
            "novice/Regular/12/npc0 680fea9c019dcbdd",
            "exploit/Regular/12/npc0 34ca03b708ad5503",
            "oracle-reader/Regular/12/npc0 dad73b9125dab7d2",
            "oracle-skilled/Regular/12/npc0 903401aba25cc138",
            "passive/Regular/8/npc300 e19cca6ff3f8a36a",
            "random/Regular/8/npc300 473f7f14f2d78f09",
            "social/Regular/8/npc300 43df9e78b6a98c3b",
            "reader/Regular/8/npc300 fd4d90908e000dd9",
            "schemer/Regular/8/npc300 ed54dcc576ae8d67",
            "loyalist/Regular/8/npc300 7d380951159e24c4",
            "floater/Regular/8/npc300 51d80b025a2f5420",
            "beast/Regular/8/npc300 5e224d8686bf4e69",
            "novice/Regular/8/npc300 726ff7ab0aad8dac",
            "exploit/Regular/8/npc300 83900f949c0b5d2f",
            "oracle-reader/Regular/8/npc300 05b7a06bb4c68982",
            "oracle-skilled/Regular/8/npc300 eb131018d1c99164",
            "passive/Regular/12/npc300 6c7700c16a1735be",
            "random/Regular/12/npc300 7373950e527452b6",
            "social/Regular/12/npc300 64e517fac9765aa5",
            "reader/Regular/12/npc300 18a2c74aea96c4a7",
            "schemer/Regular/12/npc300 5936687bdfc19aab",
            "loyalist/Regular/12/npc300 04d7434313779e51",
            "floater/Regular/12/npc300 67075c36ac942981",
            "beast/Regular/12/npc300 bd8fb61a7680ac4a",
            "novice/Regular/12/npc300 1b92bca6d1a280a6",
            "exploit/Regular/12/npc300 73622d6b682b2c3f",
            "oracle-reader/Regular/12/npc300 4e9cefe074ced072",
            "oracle-skilled/Regular/12/npc300 ae886154bfd997e3",
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
