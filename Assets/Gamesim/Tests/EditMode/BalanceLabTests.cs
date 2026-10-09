#if !UNITY_5_3_OR_NEWER
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The balance lab's own tests (BALANCE plan B2-B4): the smoke tier, which plays one season per policy
    /// and house of the full grid (4 to 12 and the All-Stars 8 and 12) and proves every player stays legal; determinism; the statistics; the knowledge gate's
    /// structure; the seeds and the performance model. The headline tier is in
    /// <see cref="BalanceLabReports"/>, explicit. The Unity-free run only: Unity's batch runner would play
    /// these seasons under Mono several times slower, and the lab's files are compiled out of Unity.
    /// </summary>
    public sealed class BalanceLabTests
    {
        // ---------------------------------------------------------------- the smoke tier

        [Test]
        public void Smoke_EveryPolicyPlaysALegalSeasonAtEverySize()
        {
            var clock = BalanceLab.Clock();
            // The full tier's grid, one season a cell: every house any tier plays is legal for every player; and in
            // each house one season with the NPC world at the budget that stands for a human (B5b, decision 1).
            var grid = BalanceLabReports.FullGrid();
            grid.AddRange(grid.Where(c => c.policy == BalancePolicies.Reader).Select(c => new BalanceLab.Cell { policy = c.policy, size = c.size, roster = c.roster, npcTicks = HumanBudget }).ToList());
            var runs = BalanceLab.Run(grid, 1);
            double seconds = clock.Elapsed.TotalSeconds;
            TestContext.WriteLine("Smoke tier: " + runs.Length + " seasons in " + BalanceLab.Num(seconds, "0.0") + " s on " + Environment.ProcessorCount + " threads.");
            TestContext.WriteLine("| policy | house | outcome | weeks | commands | own | walker | free | refused | top refusal |");
            TestContext.WriteLine("|---|---|---|---|---|---|---|---|---|---|");
            foreach (var r in runs)
                TestContext.WriteLine("| " + r.cell.policy + " | " + BalanceLabReports.HouseOf(r.cell) + " | " + (r.error ?? r.autopsy.outcome + " (" + r.autopsy.placement + ")") + " | " + r.autopsy?.weeks
                    + " | " + r.commands + " | " + r.own + " | " + r.fallbacks + " | " + r.freeActions + " | " + r.refusals + " | "
                    + r.refusalsByKind.OrderByDescending(p => p.Value).Select(p => p.Key + " x" + p.Value).FirstOrDefault() + " |");
            TestContext.WriteLine("Rows: " + BalanceLab.Write("smoke", BalanceLab.Jsonl(runs)));

            foreach (var r in runs)
            {
                string at = r.cell.Key + " seed " + r.seed;
                Assert.That(r.error, Is.Null, at);
                Assert.That(r.autopsy.finished, Is.True, at);
                Assert.That(r.autopsy.houseSize, Is.EqualTo(r.cell.size), at);
                // The NPC world's clock is the ticks the world spent, none at budget nought; at a budget, at least the first
                // social week's half and never more than the budget a week.
                Assert.That(r.autopsy.npcTicks, Is.EqualTo(r.npcTicks), at + ": the season's NPC clock is the world's ticks.");
                if (r.cell.npcTicks == 0) Assert.That(r.npcOps, Is.Zero, at + ": budget nought plays no NPC world.");
                else Assert.That(r.npcTicks, Is.InRange(r.cell.npcTicks / 2, r.cell.npcTicks * r.autopsy.weeks), at + ": the world spent its budget.");
                Assert.That(r.pace.weeks, Is.EqualTo(r.autopsy.weeks), at + ": the story's pace was watched to the end.");
                if (r.cell.policy == BalancePolicies.Passive) Assert.That(r.own, Is.Zero, at + ": the passive player leaves every step to the walker.");
                else Assert.That(r.own, Is.GreaterThan(0), at + ": the player acted.");
                // Legal play: the house refuses a player now and then (a deal it will not take, a person
                // somebody else is with), never most of the time. The exploit hunter's refusals are its
                // measurement, so it answers only to the walker and the validator.
                if (r.cell.policy != BalancePolicies.Exploit)
                    Assert.That(r.refusals, Is.LessThanOrEqualTo(Math.Max(6, r.own / 4)), at + ": refusals " + string.Join("; ", r.refusalsByKind.Select(p => p.Key + " x" + p.Value)));
                // A veto holder is never evicted that week: on the block they save themselves, and off it they
                // cannot be named. (The final four's lock binds only a holder off the block; the final
                // eviction has no veto.)
                Assert.That(r.autopsy.weeksPlayed.Where(w => w.playerEvicted && w.playerVetoHolder && !w.finalEviction).Select(w => w.week), Is.Empty,
                    at + ": the player was evicted in a week they held the veto.");
                // A pact of three calls only in its war room (B6a): no gated player asks it to call outside one.
                if (!BalancePolicies.IsOracle(r.cell.policy))
                    Assert.That(r.refusalsByKind.Keys.Where(k => k.Contains("settles its call when it meets")), Is.Empty, at + ": a call refused a pact of three.");
            }
        }

        /// <summary>
        /// B6a: every engaged player (the loyalist, the schemer, the reader, the floater and the random player) in a pact
        /// of three, once the block is set, convenes its war room with a seat and answers the plan for free, as each
        /// answers: the loyalist goes with it, the floater lies low, the others as their card reads. The fixture is a
        /// season at eight with the player and the first two houseguests in a warm pact made in week one (as
        /// PactPlanSeasonDigests' trio player), walked to the first campaign where it could meet and somebody at it
        /// has a say, the player off the block; each player plays that campaign on through the lab's own step.
        /// </summary>
        [Test]
        public void EveryEngagedPlayerConvenesItsWarRoomAndAnswersThePlan()
        {
            EpisodeState campaign = null;
            for (uint seed = 5100; seed < 5140 && campaign == null; seed++) campaign = TheFirstWarRoomCampaign(seed, card => true);
            Assert.That(campaign, Is.Not.Null, "A campaign where the trio could meet as a war room.");
            foreach (string name in new[] { BalancePolicies.Loyalist, BalancePolicies.Schemer, BalancePolicies.Reader, BalancePolicies.Floater, BalancePolicies.Random })
                ConveneAndAnswer(campaign, name);
        }

        /// <summary>
        /// B6a: how each engaged player answers a plan, read off its card (the fixture's campaign, cards made by hand):
        /// the loyalist goes with it; the reader pushes against a plan somebody who said it is torn or leaning on; the
        /// schemer pushes against a plan that names an ally; the floater lies low; on a split each goes with a nominee
        /// of its own choosing, never itself; with nobody saying, everybody lies low.
        /// </summary>
        [Test]
        public void EachEngagedPlayerAnswersAPlanAsItsCardReads()
        {
            EpisodeState campaign = null;
            for (uint seed = 5100; seed < 5140 && campaign == null; seed++) campaign = TheFirstWarRoomCampaign(seed, card => true);
            Assert.That(campaign, Is.Not.Null);
            var view = new PlayerView(campaign, campaign.seed);
            var members = campaign.alliances.Single(a => a.id == TrioId).members.Where(id => id != campaign.playerId).ToList();
            string m1 = members[0], m2 = members[1];
            // n0: a nominee outside the pact, the plan's target; n1 the other.
            string n0 = campaign.nominees.FirstOrDefault(id => !members.Contains(id)), n1 = campaign.nominees.FirstOrDefault(id => id != n0);
            Assert.That(n0, Is.Not.Null, "Precondition: somebody on the block is outside the pact.");
            PlayerView.PlanCard Card(string plan, bool split, params (string member, string said, string whip)[] lines) => new PlayerView.PlanCard
            {
                pactId = TrioId, membersPlanId = plan, split = split,
                lines = lines.Select(l => new PlayerView.PlanLine { memberId = l.member, saidId = l.said, whip = l.whip }).ToList(),
            };
            const string firm = Gamesim.Simulation.VoteRead.Firm, leaning = Gamesim.Simulation.VoteRead.Leaning, torn = Gamesim.Simulation.VoteRead.Torn;
            var sure = Card(n0, false, (m1, n0, firm), (m2, n0, firm));
            var lean = Card(n0, false, (m1, n0, firm), (m2, n0, leaning));
            var torned = Card(n0, false, (m1, n0, torn), (m2, n0, firm));
            var split = Card(null, true, (m1, n0, firm), (m2, n1, firm));
            var quiet = Card(null, false, (m1, null, null), (m2, null, null));
            var ally = Card(m1, false, (m2, m1, firm));
            string Answer(string name, PlayerView.PlanCard card) => ((GatedPolicy)BalancePolicies.Create(name)).AnswerPlan(view, card);

            Assert.That(Answer(BalancePolicies.Loyalist, sure), Is.EqualTo(n0));
            Assert.That(Answer(BalancePolicies.Loyalist, lean), Is.EqualTo(n0), "The loyalist goes with the plan however its sayers lean.");
            Assert.That(Answer(BalancePolicies.Reader, sure), Is.EqualTo(n0), "The reader goes with a plan everybody who said it is firm on...");
            Assert.That(Answer(BalancePolicies.Reader, lean), Is.EqualTo(n1), "...and pushes against one somebody is leaning on...");
            Assert.That(Answer(BalancePolicies.Reader, torned), Is.EqualTo(n1), "...or torn on.");
            Assert.That(Answer(BalancePolicies.Schemer, sure), Is.EqualTo(n0));
            Assert.That(Answer(BalancePolicies.Schemer, ally), Is.Not.EqualTo(m1).And.Not.Null, "The schemer pushes against a plan that names an ally.");
            foreach (var card in new[] { sure, lean, split, quiet }) Assert.That(Answer(BalancePolicies.Floater, card), Is.Null, "The floater lies low.");
            foreach (string name in new[] { BalancePolicies.Loyalist, BalancePolicies.Reader, BalancePolicies.Schemer })
            {
                Assert.That(new[] { n0, n1 }, Does.Contain(Answer(name, split)), name + " goes with a nominee on a split.");
                Assert.That(Answer(name, quiet), Is.Null, name + " lies low with nobody saying.");
            }
            Assert.That(new[] { n0, n1, null }, Does.Contain(Answer(BalancePolicies.Random, sure)));
        }

        /// <summary>One player plays the fixture's campaign on through the lab's own step until its war room's plan settles; returns the stance.</summary>
        private static string ConveneAndAnswer(EpisodeState campaign, string name)
        {
            {
                var engine = new EpisodeEngine(campaign.Clone());
                var agent = BalancePolicies.Create(name);
                var run = new BalanceLab.SeasonRun { cell = new BalanceLab.Cell { policy = name, size = 8 }, seed = campaign.seed };
                var mine = new List<EpisodeCommandKind>();
                string expected = null;
                for (int step = 0; step < 40 && engine.Snapshot.phase == EpisodePhase.Campaign; step++)
                {
                    var s = engine.Snapshot;
                    var row = PactPlans.ThisWeek(s, TrioId);
                    if (row != null && row.stance != PactPlanStance.Open) break;
                    if (row != null && expected == null)
                    {
                        // What the card shows: the members' plan, and the player's read of who said it.
                        var view = new PlayerView(s, run.seed);
                        var card = view.OpenPlanCard(TrioId);
                        Assert.That(card, Is.Not.Null, name + ": the plan waits on the player.");
                        bool unsure = card.lines.Any(l => l.saidId == card.membersPlanId
                            && (l.whip == Gamesim.Simulation.VoteRead.Torn || l.whip == Gamesim.Simulation.VoteRead.Leaning));
                        bool ally = card.membersPlanId != null && view.AlliedWith(card.membersPlanId);
                        expected = name == BalancePolicies.Loyalist ? PactPlanStance.Agreed : name == BalancePolicies.Floater ? PactPlanStance.Low
                            : name == BalancePolicies.Schemer ? (ally ? PactPlanStance.Countered : PactPlanStance.Agreed)
                            : name == BalancePolicies.Reader ? (unsure && card.membersPlanId != null ? PactPlanStance.Countered : PactPlanStance.Agreed) : null;
                    }
                    var result = BalanceLab.Step(engine, agent, s, run, out var used);
                    Assert.That(result.accepted, Is.True, name + ": " + used.kind + ": " + result.reason);
                    if (used.id.StartsWith("lab-", StringComparison.Ordinal)) mine.Add(used.kind);
                }
                var settled = PactPlans.ThisWeek(engine.Snapshot, TrioId);
                TestContext.Out.WriteLine(name + ": " + string.Join(", ", mine) + " -> " + settled?.stance + " (expected " + (expected ?? "any") + ")");
                Assert.That(mine, Does.Contain(EpisodeCommandKind.AllianceMeet), name + ": convened the war room.");
                Assert.That(mine, Does.Contain(EpisodeCommandKind.AnswerPactPlan), name + ": answered the plan.");
                Assert.That(mine.IndexOf(EpisodeCommandKind.AllianceMeet), Is.LessThan(mine.IndexOf(EpisodeCommandKind.AnswerPactPlan)), name + ": met, then answered.");
                Assert.That(mine, Does.Not.Contain(EpisodeCommandKind.CallTheVote), name + ": no call outside the war room.");
                Assert.That(settled, Is.Not.Null, name);
                if (expected != null) Assert.That(settled.stance, Is.EqualTo(expected), name + ": answered as it answers.");
                else Assert.That(new[] { PactPlanStance.Agreed, PactPlanStance.Countered, PactPlanStance.Low }, Does.Contain(settled.stance), name);
                Assert.That(run.refusalsByKind.Keys.Where(k => k.StartsWith("AllianceMeet", StringComparison.Ordinal) || k.StartsWith("AnswerPactPlan", StringComparison.Ordinal)),
                    Is.Empty, name + ": the house took the meeting and the answer.");
                return settled.stance;
            }
        }

        private const string TrioId = "alliance-lab-war-room-trio";

        /// <summary>
        /// Whether a war room held now (on a copy) leaves a members' plan - not a split, so going with it and pushing
        /// against it differ - whose card is as <paramref name="wanted"/> asks.
        /// </summary>
        private static bool HasAMajority(EpisodeState s, AllianceState pact, Func<PlayerView.PlanCard, bool> wanted)
        {
            string through = pact.members.FirstOrDefault(id => id != s.playerId && Allegiance.InHouse(s, id));
            if (through == null) return false;
            var meet = EpisodeEngineTests.Command(s, EpisodeCommandKind.AllianceMeet);
            meet.id = "lab-fixture-meet"; meet.targetId = through; meet.text = pact.id;
            var held = new EpisodeEngine(s.Clone()).Apply(meet);
            var card = held.accepted ? new PlayerView(held.state, s.seed).OpenPlanCard(pact.id) : null;
            return card != null && card.membersPlanId != null && wanted(card);
        }

        /// <summary>A season at eight with the player and the first two houseguests in a warm pact from week one, walked to the first campaign where it could meet as a war room with a say, the player off the block; or null.</summary>
        private static EpisodeState TheFirstWarRoomCampaign(uint seed, Func<PlayerView.PlanCard, bool> wanted)
        {
            var s0 = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, seed);
            ShippedRules.ApplyFresh(s0);
            var npcs = s0.Active.Where(c => !c.isPlayer).Take(2).Select(c => c.id).ToList();
            var trio = new AllianceState { id = TrioId, name = "The Lab Trio", active = true, members = new List<string> { s0.playerId }.Concat(npcs).ToList() };
            s0.alliances.Add(trio);
            s0.ledger.alliances.Add(new AllianceRow { id = trio.id, startedWeek = s0.week, why = "player" });
            EpisodeEngine.AllianceFormedUnderRead(s0, trio);
            foreach (string from in trio.members)
                foreach (string to in trio.members.Where(id => id != from))
                {
                    var edge = s0.relationships.FirstOrDefault(r => r.fromId == from && r.toId == to);
                    if (edge == null) s0.relationships.Add(edge = new RelationshipState { fromId = from, toId = to });
                    edge.score = 30;
                }
            var engine = new EpisodeEngine(s0);
            for (int i = 0; i < 2000; i++)
            {
                var s = engine.Snapshot;
                if (s.phase == EpisodePhase.Finished || s.Find(s.playerId).status != ContestantStatus.Active) return null;
                var pact = s.alliances.FirstOrDefault(a => a.id == TrioId);
                if (pact == null || !pact.active) return null;
                if (s.phase == EpisodePhase.Campaign && PactPlans.BlockSet(s) && !s.nominees.Contains(s.playerId) && PactPlans.Convenes(s, pact)
                    && EpisodeEngine.SocialActionsSpent(s) < EpisodeEngine.SocialActionBudget(s) && HasAMajority(s, pact, wanted))
                    return s.Clone();
                var result = engine.Apply(BalanceLab.Walker(s));
                if (!result.accepted) return null;
            }
            return null;
        }

        /// <summary>
        /// Whoever plays - the walker for the passive player, each gated policy, each oracle - a player who holds
        /// the veto at the meeting while on the block comes off it. The meeting - the player in the block's second
        /// chair, where the engine tests' walker saved the other nominee - is found in the passive player's seasons
        /// at eight and played on from there by every player through the lab's own step.
        /// </summary>
        [Test]
        public void APlayerWhoHoldsTheVetoOnTheBlockSavesThemselvesWhoeverPlays()
        {
            var finder = new BalanceLab.Cell { policy = BalancePolicies.Passive, size = 8 };
            EpisodeState meeting = null;
            uint seed = 0;
            for (int index = 0; index < 80 && meeting == null; index++)
            {
                seed = BalanceLab.Seed(finder, index);
                meeting = TheVetoMeetingWithThePlayerOnTheBlock(finder, index);
            }
            Assert.That(meeting, Is.Not.Null, "No season put a veto-holding player in the block's second chair.");
            Assert.That(meeting.nominees[0], Is.Not.EqualTo(meeting.playerId));
            foreach (string name in BalancePolicies.All)
            {
                var engine = new EpisodeEngine(meeting.Clone());
                var agent = BalancePolicies.Create(name);
                var run = new BalanceLab.SeasonRun { cell = new BalanceLab.Cell { policy = name, size = 8 }, seed = seed };
                for (int step = 0; step < 20 && !engine.Snapshot.vetoResolved; step++)
                {
                    var s = engine.Snapshot;
                    var result = BalanceLab.Step(engine, agent, s, run, out var used);
                    Assert.That(result.accepted, Is.True, name + ": " + used.kind + ": " + result.reason);
                }
                var after = engine.Snapshot;
                Assert.That(after.vetoResolved, Is.True, name + ": the meeting was decided.");
                Assert.That(after.nominees, Does.Not.Contain(after.playerId), name + ": the veto holder left themselves on the block.");
            }
        }

        /// <summary>The passive player's season to the first veto meeting at which they hold the veto on the block, or null.</summary>
        private static EpisodeState TheVetoMeetingWithThePlayerOnTheBlock(BalanceLab.Cell cell, int index)
        {
            var run = new BalanceLab.SeasonRun { cell = cell, index = index, seed = BalanceLab.Seed(cell, index) };
            var fresh = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = cell.size }, run.seed);
            ShippedRules.ApplyFresh(fresh);
            var engine = new EpisodeEngine(fresh);
            var agent = BalancePolicies.Create(cell.policy);
            for (int i = 0; i < BalanceLab.CommandCap; i++)
            {
                var s = engine.Snapshot;
                if (s.phase == EpisodePhase.Finished || s.Find(s.playerId).status != ContestantStatus.Active) return null;
                // The second chair: the engine tests' walker saves the first nominee, which there is somebody else.
                if (s.phase == EpisodePhase.VetoMeeting && !s.vetoResolved && s.vetoHolderId == s.playerId && s.nominees.IndexOf(s.playerId) > 0) return s.Clone();
                Assert.That(BalanceLab.Step(engine, agent, s, run, out _).accepted, Is.True);
            }
            return null;
        }

        // ---------------------------------------------------------------- the projections (B7)

        /// <summary>B7: the projection's arithmetic on four hand-made testers.</summary>
        [Test]
        public void TheProjectionArithmeticIsTheBriefs()
        {
            // (out week, first commitment settled): out in week 1 having seen none; never out, saw one in week 2;
            // out in week 3, saw one in week 1; out in week 2, "saw" one in week 3 - after they left, so never.
            var t = new List<Projection.Tester> { new Projection.Tester(1, 0), new Projection.Tester(0, 2), new Projection.Tester(3, 1), new Projection.Tester(2, 3) };
            Assert.That(Projection.S(t, 1), Is.EqualTo(0.25)); Assert.That(Projection.S(t, 2), Is.EqualTo(0.5)); Assert.That(Projection.S(t, 3), Is.EqualTo(0.75));
            Assert.That(Projection.C(t, 1), Is.EqualTo(0.25)); Assert.That(Projection.C(t, 2), Is.EqualTo(0.5)); Assert.That(Projection.C(t, 3), Is.EqualTo(0.5), "A settle after the tester left is not seen.");
            Assert.That(Projection.AnyOut(t, 1, 3), Is.EqualTo(1 - Math.Pow(0.75, 3)).Within(1e-12));
            Assert.That(Projection.AllSaw(t, 2, 3), Is.EqualTo(0.125).Within(1e-12));
            // Joint at week 3: P(A) = 1/2 (the second and third); P(A and not B) = 1/4 (the second: the third went out in week 3).
            Assert.That(Projection.Joint(t, 3, 3), Is.EqualTo(0.125 - 0.015625).Within(1e-12));
            Assert.That(Projection.Joint(t, 1, 3), Is.EqualTo(0).Within(1e-12), "Week 1: the only one who saw is the third, still in, so nobody who saw is out.");
            Assert.That(Projection.LowestWeek(k => Projection.AnyOut(t, k, 3), 0.9, 5), Is.EqualTo(3), ".578, .875, .984");
            Assert.That(Projection.LowestWeek(k => Projection.AllSaw(t, k, 3), 0.9, 5), Is.Zero, "Never: half never see one.");
            Assert.That(Projection.WeekMinutes(120, 60, 300, 4), Is.EqualTo(10).Within(1e-12));
            Assert.That(Projection.CompetitionSeconds(1u, 9, new[] { "FinalHoHPart3" }), Is.EqualTo(CompetitionDefinitions.FirstImpressions.Duration + 3));
            Assert.That(Projection.CompetitionSeconds(1u, 9, new[] { "FinalHoHPart1", "FinalHoHPart2" }),
                Is.EqualTo(CompetitionDefinitions.PressureCooker.Duration + CompetitionDefinitions.SwitchbackSignals.Duration));
        }

        // ---------------------------------------------------------------- the NPC world (B5b)

        /// <summary>The NPC ticks a week that stand for a human (the lead's decision 1).</summary>
        internal const int HumanBudget = 300;

        /// <summary>One season at eight with the world at the human budget, every operation watched.</summary>
        private static BalanceLab.SeasonRun WatchedSeason(Action<EpisodeState, EpisodeState, NpcOperationKind> watch)
        {
            var cell = new BalanceLab.Cell { policy = BalancePolicies.Reader, size = 8, npcTicks = HumanBudget };
            var run = new BalanceLab.SeasonRun { cell = cell, index = 3, seed = BalanceLab.Seed(cell, 3) };
            var fresh = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, run.seed);
            ShippedRules.ApplyFresh(fresh);
            var world = new BalanceLabNpcWorld(new EpisodeEngine(fresh), HumanBudget, run) { Watch = watch };
            var agent = BalancePolicies.Create(cell.policy);
            for (int i = 0; i < BalanceLab.CommandCap && world.Engine.Snapshot.phase != EpisodePhase.Finished; i++)
            {
                world.SpendSlice();
                var result = BalanceLab.Step(world.Current, world.SpendRest, agent, world.Engine.Snapshot, run, out var used);
                Assert.That(result.accepted, Is.True, used.kind + ": " + result.reason);
                if (used.kind == EpisodeCommandKind.Advance) Assert.That(EpisodeValidation.TryValidate(result.state, out string invalid), Is.True, "after an Advance: " + invalid);
            }
            Assert.That(world.Engine.Snapshot.phase, Is.EqualTo(EpisodePhase.Finished));
            return run;
        }

        /// <summary>
        /// B5b: the world moves only the houseguests' world. Every operation leaves the season's own stream and the
        /// player's relationship rows (to and from them) as they were, and leaves a valid season; and the season played
        /// through the world validates after every Advance.
        ///
        /// <para>The player's relationship arcs: the brief expected them untouched too, and they are not. The engine's
        /// own completion of an NPC conversation (EpisodeNpcSocial's TickNpcSocial) moves the pair through ChangeWithRoll,
        /// which writes an arc for each houseguest of the pair - as the director's world does in a played season. The
        /// driver adds nothing of its own (it only calls PrepareNpcOperation); this test pins where it happens - only on a
        /// Tick that completed a conversation, only the arcs of that conversation's pair or of the houseguest the pair
        /// gossiped about - and the finding is the lead's, with the engine's NPC world.</para>
        /// </summary>
        [Test]
        public void EveryNpcOperationLeavesTheSeasonsStreamAndThePlayersRowsAloneAndTheSeasonValid()
        {
            int ops = 0, moved = 0, arcsMoved = 0, phaseCount = 0;
            string lastPhase = null;
            var ticksByPhase = new SortedDictionary<string, int>(StringComparer.Ordinal);
            string Rows(EpisodeState s) => Newtonsoft.Json.JsonConvert.SerializeObject(s.relationships.Where(r => r.fromId == s.playerId || r.toId == s.playerId)
                .OrderBy(r => r.fromId, StringComparer.Ordinal).ThenBy(r => r.toId, StringComparer.Ordinal));
            string Arc(EpisodeState s, string id) => Newtonsoft.Json.JsonConvert.SerializeObject(s.relationshipArcs.FirstOrDefault(a => a.npcId == id));
            var run = WatchedSeason((before, after, kind) =>
            {
                ops++;
                if (kind == NpcOperationKind.Tick)
                {
                    // One entry a phase as it is played: week one holds two free-time phases, the move-in night and the
                    // social week after the first eviction (the week turns at the Head of Household).
                    string at = before.week + " " + before.phase;
                    if (at != lastPhase) { lastPhase = at; phaseCount++; }
                    string phase = phaseCount.ToString("D2", CultureInfo.InvariantCulture) + ": week " + at;
                    ticksByPhase[phase] = (ticksByPhase.TryGetValue(phase, out int n) ? n : 0) + 1;
                }
                Assert.That(after.randomState, Is.EqualTo(before.randomState), kind + ": the season's stream.");
                Assert.That(Rows(after), Is.EqualTo(Rows(before)), kind + ": the player's relationship rows.");
                Assert.That(EpisodeValidation.TryValidate(after, out string invalid), Is.True, kind + ": " + invalid);
                if (after.npcSocial.randomState != before.npcSocial.randomState) moved++;
                var changedArcs = after.contestants.Select(c => c.id).Concat(before.contestants.Select(c => c.id)).Distinct().Where(id => Arc(after, id) != Arc(before, id)).ToList();
                if (changedArcs.Count == 0) return;
                arcsMoved++;
                var completed = before.npcSocial.pending.Where(p => after.npcSocial.pending.All(q => q.sequence != p.sequence)).ToList();
                Assert.That(kind, Is.EqualTo(NpcOperationKind.Tick), "Only a completion moves an arc.");
                Assert.That(completed, Is.Not.Empty, "Only a Tick that completed a conversation moves an arc.");
                var pairs = completed.SelectMany(p => new[] { p.firstId, p.secondId }).ToList();
                // The gossip target's arc moves by the gossip: anybody else's would be a new finding.
                var others = changedArcs.Where(id => !pairs.Contains(id)).ToList();
                foreach (string id in others)
                    Assert.That(pairs.Any(p => after.Score(p, id) != before.Score(p, id)), Is.True, id + "'s arc moved with nothing said about them.");
            });
            TestContext.WriteLine("operations " + ops + ", conversations started " + run.npcStarts + ", NPC draws on " + moved + " operations, arcs moved on " + arcsMoved
                + "; ticks by phase " + string.Join(", ", ticksByPhase.Select(p => p.Key + " " + p.Value)));
            // Decision 3: half the week's ticks in the social week and half in the campaign, every one spent - in slices
            // before the decisions and the rest before the Advance that closes the phase.
            Assert.That(ticksByPhase.Values.Distinct(), Is.EqualTo(new[] { HumanBudget / 2 }), "Each phase the world ran spent its half of the week.");
            Assert.That(ticksByPhase.Keys.Count(k => k.EndsWith("Social", StringComparison.Ordinal)), Is.GreaterThanOrEqualTo(2));
            Assert.That(ticksByPhase.Keys.Count(k => k.EndsWith("Campaign", StringComparison.Ordinal)), Is.GreaterThanOrEqualTo(2));
            Assert.That(ops, Is.EqualTo(run.npcOps));
            Assert.That(run.npcStarts, Is.GreaterThan(5), "Conversations started.");
            Assert.That(moved, Is.GreaterThan(5), "The world drew on its own stream.");
            Assert.That(run.npcRejected, Is.Zero, "The engine took every operation the world put to it.");
        }

        /// <summary>B5b: the driver goes through the engine's door only - no relationship change, no command - read from its source with the comments taken out.</summary>
        [Test]
        public void TheNpcWorldDriverNeitherChangesNorApplies()
        {
            string source = System.IO.File.ReadAllText(System.IO.Path.Combine(SourceRoot(), "Tests", "EditMode", "BalanceLabNpcWorld.cs"));
            string code = string.Join("\n", source.Split('\n').Select(line => { int at = line.IndexOf("//", StringComparison.Ordinal); return at < 0 ? line : line.Substring(0, at); }));
            foreach (string forbidden in new[] { "Change(", "ChangeWithRoll(", "Apply(", "RelationshipLedger", ".score", "Move(" })
                Assert.That(code, Does.Not.Contain(forbidden), "The driver calls " + forbidden);
            Assert.That(code, Does.Contain("PrepareNpcOperation("), "...and does go through the engine's door.");
            Assert.That(code, Does.Contain("NpcPairing.Plan("), "...and pairs as the house pairs.");
        }

        /// <summary>B5b: every budget plays the seasons budget nought plays, and at nought the seeds are those the lab always drew.</summary>
        [Test]
        public void SeedsPairAcrossBudgets()
        {
            foreach (int size in new[] { 6, 8, 12 })
            {
                var nought = new BalanceLab.Cell { policy = BalancePolicies.Novice, size = size };
                for (int i = 0; i < 100; i++)
                {
                    uint seed = BalanceLab.Seed(nought, i);
                    Assert.That(seed, Is.EqualTo(SeededRandom.HashSeed("balance-lab/v1/Regular/" + size + "/npc0/" + i)), "Budget nought draws the seeds it always drew.");
                    foreach (int budget in new[] { 300, 900, 1800 })
                        Assert.That(BalanceLab.Seed(new BalanceLab.Cell { policy = BalancePolicies.Reader, size = size, npcTicks = budget }, i), Is.EqualTo(seed), size + "/" + budget + "/" + i);
                }
            }
        }

        /// <summary>B5b: the tables keep each budget's seasons apart, a row each, and the sensitivity table pairs them.</summary>
        [Test]
        public void TheTablesKeepBudgetsApart()
        {
            var cells = new[] { 0, HumanBudget }.SelectMany(b => BalanceLab.Grid(new[] { BalancePolicies.Passive }, new[] { 6 }, npcTicks: b)).ToList();
            var runs = BalanceLab.Run(cells, 2);
            Assert.That(runs.Select(r => r.seed).Take(2), Is.EqualTo(runs.Select(r => r.seed).Skip(2)), "The budgets play the same seasons.");
            var groups = BalanceLabReports.Cells(runs).Select(g => g.Key.size).ToList();
            Assert.That(groups, Is.EqualTo(new[] { "6", "6 npc" + HumanBudget }), "One row a budget.");
            Assert.That(BalanceLabReports.Cells(runs).All(g => g.Count() == 2), Is.True);
            string md = BalanceLabReports.NpcBudget(runs);
            Assert.That(md, Does.Contain("| passive | 6 | "), "The sensitivity table has the cell.");
            Assert.That(md, Does.Contain(HumanBudget + " (vs 0)"));
        }

        /// <summary>B5b, risk 4: a policy's coins see the season's revision less the world's operations, so the world never reshuffles them.</summary>
        [Test]
        public void ThePlayersCoinsDoNotMoveWithTheNpcWorldsOperations()
        {
            var s = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, 77u);
            ShippedRules.ApplyFresh(s);
            var moved = s.Clone();
            moved.revision += 41;
            var plain = new PlayerView(s, 9u);
            var world = new PlayerView(moved, 9u, 41);
            var other = new PlayerView(moved, 9u);
            var coins = Enumerable.Range(0, 20).Select(plain.Coin).ToList();
            Assert.That(Enumerable.Range(0, 20).Select(world.Coin), Is.EqualTo(coins), "Forty-one operations later, the same coins.");
            Assert.That(Enumerable.Range(0, 20).Select(other.Coin), Is.Not.EqualTo(coins), "Precondition: the revision is in the coin.");
        }

        /// <summary>Assets/Gamesim, found upward from the test binary (Tools/SimulationTests).</summary>
        private static string SourceRoot()
        {
            string at = System.IO.Path.GetDirectoryName(typeof(BalanceLabTests).Assembly.Location);
            for (int depth = 0; depth < 12 && !string.IsNullOrEmpty(at); depth++)
            {
                string candidate = System.IO.Path.Combine(at, "Assets", "Gamesim");
                if (System.IO.File.Exists(System.IO.Path.Combine(candidate, "Simulation", "SeasonBuilder.cs"))) return candidate;
                at = System.IO.Path.GetDirectoryName(at);
            }
            Assert.Fail("No Assets/Gamesim above the test binary.");
            return null;
        }

        // ---------------------------------------------------------------- budget nought (B5b)

        /// <summary>
        /// The rows of budget nought's seasons as the lab played them before the NPC world's driver landed (B5b, test 1):
        /// the smoke grid's one season a house and the headline grid's twenty, hashed as the digests hash a season
        /// (16 hex of SHA-256). Recorded with the driver's plumbing (paired seeds, the coins' revision less the world's
        /// operations, the npcWorld and pairs fields) and none of its behaviour.
        /// </summary>
        // f3b663c383491ce1 before and after the driver (2989e94f, 6a29f08d); re-recorded when D3's counter reach was
        // re-amended to twenty at ten (the war rooms the policies play now settle differently). Re-recorded at vote family
        // V6 (fa8d4d420fad8953 at its base, 27b21b28, where D2's all-week rules had already moved it): fresh seasons play the
        // unified vote rules, and the lab's player view and skilled oracle read mode 2's offers.
        internal const string BudgetNoughtRows = "c7d250380346ae38";

        [Test, Explicit("B5b: budget nought plays, byte for byte, the seasons it played before the NPC world's driver (about 5 min). Run by name.")]
        public void BudgetNoughtPlaysTheSeasonsItPlayedBeforeTheDriver()
        {
            var smoke = BalanceLab.Run(BalanceLabReports.FullGrid(), 1);
            var headline = BalanceLab.Run(BalanceLab.Grid(BalancePolicies.All, BalanceLabReports.HeadlineSizes), 20);
            string rows = BalanceLab.Jsonl(smoke) + BalanceLab.Jsonl(headline);
            TestContext.WriteLine("Rows: " + BalanceLab.Write("budget0", rows) + "; hash " + Hash(rows));
            Assert.That(smoke.Concat(headline).All(r => r.cell.npcTicks == 0 && r.npcOps == 0), Is.True);
            Assert.That(Hash(rows), Is.EqualTo(BudgetNoughtRows));
        }

        /// <summary>The 16-hex SHA-256 the season digests use (CommitmentRulesSeasonDigests).</summary>
        internal static string Hash(string text)
        {
            using (var sha = System.Security.Cryptography.SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(text))).Replace("-", "").Substring(0, 16).ToLowerInvariant();
        }

        /// <summary>B6b: a tier played in parts - each part's seasons saved whole and read back - writes the rows one run writes.</summary>
        [Test]
        public void ATierPlayedInPartsWritesTheRowsOneRunWrites()
        {
            var cells = BalanceLab.Grid(new[] { BalancePolicies.Reader, BalancePolicies.Loyalist }, new[] { 6 });
            var one = BalanceLab.Run(cells, 3);
            string whole = BalanceLab.Jsonl(one);
            var parts = BalanceLab.RunPart(cells, 0, 2).Concat(BalanceLab.RunPart(cells, 2, 1)).Select(BalanceLabParts.RoundTrip)
                .OrderBy(r => cells.FindIndex(c => c.Key == r.cell.Key)).ThenBy(r => r.index).ToList();
            Assert.That(BalanceLab.Jsonl(parts), Is.EqualTo(whole));
            Assert.That(parts.Select(r => r.index), Is.EqualTo(new[] { 0, 1, 2, 0, 1, 2 }));
            // What the rows leave out survives too: the autopsy whole, and the diagnostics' own records.
            string Rest(BalanceLab.SeasonRun r) => Newtonsoft.Json.JsonConvert.SerializeObject(new { r.autopsy, r.gameSenseRows, r.nominations, r.pariah, r.preparation, r.counterMembers });
            Assert.That(parts.Select(Rest), Is.EqualTo(one.Select(Rest)));
            Assert.That(one.Sum(r => r.gameSenseRows.Count), Is.GreaterThan(0), "Game Sense's rows were kept.");
        }

        [Test]
        public void TwoRunsWriteTheSameRowsWhateverTheParallelism()
        {
            var cells = BalanceLab.Grid(new[] { BalancePolicies.Random, BalancePolicies.Schemer, BalancePolicies.Exploit, BalancePolicies.OracleSkilled }, new[] { 6 });
            string serial = BalanceLab.Jsonl(BalanceLab.Run(cells, 2, parallelism: 1));
            string parallel = BalanceLab.Jsonl(BalanceLab.Run(cells, 2));
            Assert.That(parallel, Is.EqualTo(serial));
            Assert.That(serial.Split('\n').Count(line => line.Length > 0), Is.EqualTo(8));
        }

        // ---------------------------------------------------------------- seeds and performance

        [Test]
        public void EveryPolicyPlaysTheSameSeasonsAndEachHouseItsOwn()
        {
            var a = new BalanceLab.Cell { policy = BalancePolicies.Passive, size = 8 };
            var b = new BalanceLab.Cell { policy = BalancePolicies.Schemer, size = 8, performance = PerformanceModel.Fixed(1) };
            var c = new BalanceLab.Cell { policy = BalancePolicies.Passive, size = 12 };
            var seeds = Enumerable.Range(0, 200).Select(i => BalanceLab.Seed(a, i)).ToList();
            Assert.That(Enumerable.Range(0, 200).Select(i => BalanceLab.Seed(b, i)), Is.EqualTo(seeds), "Policy and performance are not in the seed.");
            Assert.That(seeds.Distinct().Count(), Is.EqualTo(200));
            Assert.That(Enumerable.Range(0, 200).Select(i => BalanceLab.Seed(c, i)).Intersect(seeds), Is.Empty, "Each house size its own seasons.");
        }

        [Test]
        public void PerformanceIsKeyedToTheCompetitionAndStaysInRange()
        {
            foreach (double level in PerformanceModel.Levels)
                Assert.That(PerformanceModel.Draw(PerformanceModel.Fixed(level), BalancePolicies.Novice, 7u, 3, EpisodePhase.Veto), Is.EqualTo(level));
            double Mean(string policy) => Enumerable.Range(0, 4000)
                .Select(i => PerformanceModel.Draw(PerformanceModel.ByPolicy, policy, (uint)i, 1 + i % 9, i % 2 == 0 ? EpisodePhase.HoH : EpisodePhase.Veto)).Average();
            Assert.That(Mean(BalancePolicies.Beast), Is.EqualTo(0.79).Within(0.02));
            Assert.That(Mean(BalancePolicies.Novice), Is.EqualTo(0.37).Within(0.02));
            Assert.That(Mean(BalancePolicies.Reader), Is.EqualTo(0.5).Within(0.02));
            for (int i = 0; i < 500; i++)
            {
                double draw = PerformanceModel.Draw(PerformanceModel.ByPolicy, BalancePolicies.Random, (uint)i, i % 7 + 1, EpisodePhase.FinalHoHPart1);
                Assert.That(draw, Is.InRange(0.0, 1.0));
                Assert.That(PerformanceModel.Draw(PerformanceModel.ByPolicy, BalancePolicies.Random, (uint)i, i % 7 + 1, EpisodePhase.FinalHoHPart1), Is.EqualTo(draw), "The same competition, the same draw.");
            }
            // The same competition's luck for every average player: their draws are one draw.
            Assert.That(PerformanceModel.Draw(PerformanceModel.ByPolicy, BalancePolicies.Reader, 11u, 2, EpisodePhase.HoH),
                Is.EqualTo(PerformanceModel.Draw(PerformanceModel.ByPolicy, BalancePolicies.Social, 11u, 2, EpisodePhase.HoH)));
        }

        // ---------------------------------------------------------------- statistics

        [Test]
        public void TheWilsonIntervalMatchesItsFormula()
        {
            var ci = BalanceLab.Wilson(10, 80);
            Assert.That(ci.low, Is.EqualTo(0.0694).Within(0.0005));
            Assert.That(ci.high, Is.EqualTo(0.2153).Within(0.0005));
            var none = BalanceLab.Wilson(0, 10);
            Assert.That(none.low, Is.EqualTo(0).Within(1e-12));
            Assert.That(none.high, Is.EqualTo(0.2775).Within(0.0005));
            var all = BalanceLab.Wilson(800, 800);
            Assert.That(all.high, Is.EqualTo(1).Within(1e-12));
        }

        [Test]
        public void McNemarIsExactForFewDiscordantPairsAndChiSquareForMany()
        {
            bool[] Repeat(params (bool value, int times)[] parts) => parts.SelectMany(p => Enumerable.Repeat(p.value, p.times)).ToArray();
            // b = 10, c = 2: exact two-sided p = 2 * (1 + 12 + 66) / 4096.
            var first = Repeat((true, 10), (false, 2), (true, 5), (false, 20));
            var second = Repeat((false, 10), (true, 2), (true, 5), (false, 20));
            var exact = BalanceLab.McNemar(first, second);
            Assert.That((exact.b, exact.c), Is.EqualTo((10, 2)));
            Assert.That(exact.p, Is.EqualTo(158.0 / 4096).Within(1e-9));
            // b = 30, c = 15: chi-square (|30 - 15| - 1)^2 / 45 = 4.3556, p = 0.0369.
            var many = BalanceLab.McNemar(Repeat((true, 30), (false, 15), (false, 40)), Repeat((false, 30), (true, 15), (false, 40)));
            Assert.That(many.p, Is.EqualTo(0.0369).Within(0.0005));
            Assert.That(BalanceLab.McNemar(new[] { true, false }, new[] { true, false }).p, Is.EqualTo(1));
        }

        [Test]
        public void SpearmanRanksAndTheBootstrapIsSeeded()
        {
            Assert.That(BalanceLab.Spearman(new double[] { 1, 2, 3, 4 }, new double[] { 10, 20, 30, 40 }), Is.EqualTo(1).Within(1e-12));
            Assert.That(BalanceLab.Spearman(new double[] { 1, 2, 3, 4 }, new double[] { 4, 3, 2, 1 }), Is.EqualTo(-1).Within(1e-12));
            // Ties take their average rank: x ranks 1, 2.5, 2.5, 4 against y's 1..4.
            Assert.That(BalanceLab.Spearman(new double[] { 1, 2, 2, 3 }, new double[] { 1, 2, 3, 4 }), Is.EqualTo(0.9487).Within(0.0001));
            var values = Enumerable.Range(0, 200).Select(i => (double)(i % 10)).ToList();
            var ci = BalanceLab.Bootstrap(values);
            Assert.That(BalanceLab.Bootstrap(values), Is.EqualTo(ci), "Seeded: the same interval twice.");
            Assert.That(values.Average(), Is.InRange(ci.low, ci.high));
        }

        // ---------------------------------------------------------------- the knowledge gate

        /// <summary>
        /// The row types the player is handed by a reader because they are what the player was told: a
        /// houseguest's word about their vote, as heard or overheard (<see cref="VoteRead.VoterRead"/>'s claims).
        /// </summary>
        private static readonly Type[] PlayerKnown = { typeof(ClaimRow) };

        /// <summary>
        /// The types a policy must never be handed: the engine, and the season with every state type reachable
        /// from it - its public fields' and properties' types, through collections and generic arguments, enums,
        /// primitives and strings aside - less <see cref="PlayerKnown"/>. Built, not listed, so a state type
        /// added later is hidden without anybody remembering to add it.
        /// </summary>
        private static readonly HashSet<Type> Hidden = HiddenTypes();

        private static HashSet<Type> HiddenTypes()
        {
            var hidden = StateClosure();
            hidden.ExceptWith(PlayerKnown);
            return hidden;
        }

        /// <summary>The engine, and every Gamesim type reachable from the season's public members.</summary>
        private static HashSet<Type> StateClosure()
        {
            var seen = new HashSet<Type>();
            var queue = new Queue<Type>();
            void Visit(Type type)
            {
                if (type == null || type == typeof(void)) return;
                if (type.IsByRef || type.IsArray || type.IsPointer) { Visit(type.GetElementType()); return; }
                if (type.IsGenericType) foreach (var argument in type.GetGenericArguments()) Visit(argument);
                if (type.IsEnum || type.IsPrimitive || type == typeof(string) || type.Namespace == null || !type.Namespace.StartsWith("Gamesim", StringComparison.Ordinal)) return;
                if (seen.Add(type)) queue.Enqueue(type);
            }
            Visit(typeof(EpisodeState));
            while (queue.Count > 0)
            {
                var type = queue.Dequeue();
                foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public)) Visit(field.FieldType);
                foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public)) Visit(property.PropertyType);
            }
            seen.Add(typeof(EpisodeEngine));
            seen.Add(typeof(CommandResult));
            return seen;
        }

        /// <summary>The closure finds what was once listed by hand: the season's own state objects.</summary>
        [Test]
        public void TheHiddenTypesAreTheSeasonsWholeState()
        {
            var listed = new[]
            {
                typeof(EpisodeState), typeof(EpisodeEngine), typeof(CommandResult), typeof(ContestantState), typeof(ContestantStats), typeof(RelationshipState),
                typeof(AllianceState), typeof(DealState), typeof(PromiseState), typeof(MemoryState), typeof(SeasonLedger), typeof(CompetitionRow),
                typeof(StoryWorldState), typeof(NpcSocialState), typeof(HouseEventState), typeof(ReplyCardState), typeof(StorylineState),
            };
            Assert.That(listed.Where(type => !Hidden.Contains(type)).Select(type => type.Name), Is.Empty);
            Assert.That(Hidden.Count, Is.GreaterThan(listed.Length), "The closure reaches past the hand-made list.");
            var closure = StateClosure();
            Assert.That(PlayerKnown.Where(type => !closure.Contains(type)).Select(type => type.Name), Is.Empty,
                "An allowed type is a piece of the state the player was told, or it needs no allowance.");
        }

        /// <summary>
        /// Gating is structural: every type reachable from what <see cref="PlayerView"/> hands out - its public
        /// members' types, and theirs, through fields, properties, collections and tuples - is a record of its own
        /// or a reader's, never the season or a piece of it (<see cref="Hidden"/>: the whole state, less what the
        /// player was told). And the policies take a view, never a state.
        /// </summary>
        [Test]
        public void ThePlayerViewHandsOutNothingOfTheSeasonItself()
        {
            var seen = new HashSet<Type>();
            var queue = new Queue<Type>();
            void Visit(Type type)
            {
                if (type == null || type == typeof(void)) return;
                if (type.IsByRef || type.IsArray || type.IsPointer) { Visit(type.GetElementType()); return; }
                if (type.IsGenericType) foreach (var argument in type.GetGenericArguments()) Visit(argument);
                if (!seen.Add(type)) return;
                queue.Enqueue(type);
            }
            foreach (var member in typeof(PlayerView).GetMembers(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.DeclaredOnly))
            {
                if (member is PropertyInfo property) Visit(property.PropertyType);
                if (member is FieldInfo field) Visit(field.FieldType);
                if (member is MethodInfo method) { Visit(method.ReturnType); foreach (var p in method.GetParameters()) Visit(p.ParameterType); }
            }
            while (queue.Count > 0)
            {
                var type = queue.Dequeue();
                if (type.Namespace == null || !type.Namespace.StartsWith("Gamesim", StringComparison.Ordinal)) continue;
                foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public)) Visit(field.FieldType);
                foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public)) Visit(property.PropertyType);
            }
            var leaks = seen.Where(type => Hidden.Contains(type)).Select(type => type.Name).ToList();
            Assert.That(seen.Count, Is.GreaterThan(10), "The view's records were walked.");
            Assert.That(leaks, Is.Empty, "PlayerView hands out a piece of the season.");
            Assert.That(typeof(PlayerView).GetFields(BindingFlags.Instance | BindingFlags.Public), Is.Empty, "No public field.");

            var policies = typeof(GatedPolicy).Assembly.GetTypes().Where(t => typeof(IBalancePolicy).IsAssignableFrom(t) && !t.IsInterface).ToList();
            Assert.That(policies.Count(t => !t.IsAbstract), Is.EqualTo(BalancePolicies.Gated.Length), "Every gated player was found.");
            foreach (var policy in policies)
                foreach (var member in policy.GetMembers(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    var types = new List<Type>();
                    if (member is FieldInfo field) types.Add(field.FieldType);
                    if (member is PropertyInfo property) types.Add(property.PropertyType);
                    if (member is MethodInfo method) { types.Add(method.ReturnType); types.AddRange(method.GetParameters().Select(p => p.ParameterType)); }
                    Assert.That(types.Where(t => Hidden.Contains(t)), Is.Empty, policy.Name + "." + member.Name + " takes or keeps a piece of the season.");
                }
            foreach (string name in BalancePolicies.All)
                Assert.That(BalancePolicies.Create(name) is IOraclePolicy, Is.EqualTo(BalancePolicies.IsOracle(name)), name + ": only the labelled oracles read the season.");
        }

        /// <summary>
        /// <see cref="KnownOdds"/>, which the view hands every policy, reads only what the player knows: across
        /// the seasons of a smoke run, a houseguest's hidden view of the player (where the player holds no read),
        /// the views between houseguests the player learned nothing of, and every statistic can all be changed
        /// without moving one estimate the player is shown.
        /// </summary>
        [Test]
        public void KnownOddsReadOnlyWhatThePlayerKnows()
        {
            int compared = 0;
            foreach (string policy in new[] { BalancePolicies.Reader, BalancePolicies.Schemer })
            {
                var fresh = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, 9100u);
                ShippedRules.ApplyFresh(fresh);
                var engine = new EpisodeEngine(fresh);
                var agent = (IBalancePolicy)BalancePolicies.Create(policy);
                for (int i = 0; i < 4000 && engine.Snapshot.phase != EpisodePhase.Finished; i++)
                {
                    var s = engine.Snapshot;
                    if (i % 7 == 0 && s.Find(s.playerId).status == ContestantStatus.Active) { Compare(s, i); compared++; }
                    var command = agent.Next(new PlayerView(s, 9100u));
                    var result = command == null ? null : engine.Apply(command);
                    if (result == null || !result.accepted) Assert.That(engine.Apply(BalanceLab.Walker(s)).accepted, Is.True);
                }
            }
            Assert.That(compared, Is.GreaterThan(20), "States across the season were compared.");
        }

        private static void Compare(EpisodeState s, int salt)
        {
            var hidden = s.Clone();
            var random = new SeededRandom((uint)(salt * 7919 + 13));
            string me = hidden.playerId;
            foreach (var r in hidden.relationships)
            {
                bool aboutMe = r.toId == me, mine = r.fromId == me;
                if (mine) continue;
                // An ally the player was in touch with this week can be told to have gone quiet (C3,
                // Allegiance.CommitmentKnown): the notes say so in words, so that much of their view is known.
                // Under the all-week rules (D2) the house is in touch with the player in every window.
                if (aboutMe ? KnownOdds.HasRead(s, r.fromId) || Allegiance.CommitmentKnown(s, r.fromId) : KnownOdds.Band(s, r.fromId, r.toId) != null) continue;
                r.score = Math.Round(random.NextDouble() * 200 - 100);
            }
            foreach (var c in hidden.contestants.Where(c => !c.isPlayer))
                c.stats = new ContestantStats { physical = random.NextDouble() * 10, mental = random.NextDouble() * 10, endurance = random.NextDouble() * 10,
                    social = random.NextDouble() * 10, luck = random.NextDouble() * 10, competition = random.NextDouble() * 10, strategic = random.NextDouble() * 10, loyalty = random.NextDouble() * 10 };
            string Odds(EpisodeState state)
            {
                var lines = new List<string>();
                var npcs = state.Active.Where(c => !c.isPlayer).Select(c => c.id).ToList();
                foreach (var npc in npcs)
                {
                    lines.Add(npc + " alliance " + Show(KnownOdds.Alliance(state, npc)) + " presumed " + KnownOdds.PresumedView(state, npc));
                    foreach (var type in new[] { DealKind.InformationSharing, DealKind.FinalTwo, DealKind.SafetyAgreement, DealKind.VoteTogether, DealKind.Partnership })
                        lines.Add(npc + " " + type + " " + Show(KnownOdds.Deal(state, npc, type, null)));
                    foreach (var about in npcs.Where(x => x != npc))
                        foreach (var type in new[] { DealKind.TargetAgreement, DealKind.VoteEvict, DealKind.VoteSave })
                            lines.Add(npc + " " + type + " " + about + " " + Show(KnownOdds.Deal(state, npc, type, about)));
                    foreach (var ask in LobbyAsk.All)
                        foreach (var approach in LobbyApproach.All)
                            lines.Add(npc + " plea " + ask + "/" + approach + " " + Show(KnownOdds.Plea(state, npc, ask, npcs.FirstOrDefault(x => x != npc), approach)));
                }
                return string.Join("\n", lines);
            }
            Assert.That(Odds(hidden), Is.EqualTo(Odds(s)), "week " + s.week + " " + s.phase + ": an estimate moved with something the player cannot know.");
        }

        private static string Show(KnownOdds.Estimate e) =>
            e.chance.ToString("R", CultureInfo.InvariantCulture) + " " + e.word + " " + e.unknowns + " " + e.read + e.claim + e.history + e.aboutKnown + e.grudge;
    }
}
#endif
