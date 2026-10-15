using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The war room (WAVE-D-NPC-PACTS-PLAN §3, D3): a pact of three or more meets once the block is set,
    /// each member who answers the player as an ally says who they want out, the nominee most of them name
    /// is the members' plan, and the player goes with it, counters it once, or lies low; the plan is the
    /// week's call, and the campaign's close settles any plan left open. Every rule is pinned off as well as
    /// on, since a season without the rules plays as it did. Unity-free, so the dotnet subset runs it
    /// (Tools/SimulationTests).
    /// </summary>
    public sealed class PactPlanTests
    {
        internal const string PactId = "alliance-war-room", PactName = "The War Room";

        // ------------------------------------------------------------ D3-S1: the gate, the numbers, the majority

        [Test]
        public void TheGateNeedsItsStartWeekTheCommitmentRulesAndTheLevers()
        {
            var s = ContentCatalog.Create(7);
            Assert.That(EpisodeEngine.PactPlanRulesOn(s), Is.False, "Nothing on.");
            EpisodeEngine.EnablePactPlans(s);
            Assert.That(s.pactPlanRulesStartWeek, Is.EqualTo(1));
            Assert.That(EpisodeEngine.PactPlanRulesOn(s), Is.False, "The start week alone is not enough:");
            EpisodeEngine.EnableCommitments(s);
            Assert.That(EpisodeEngine.PactPlanRulesOn(s), Is.False, "nor with the commitment rules alone:");
            EpisodeEngine.EnableLevers(s);
            Assert.That(EpisodeEngine.PactPlanRulesOn(s), Is.True, "the levers make the call a plan is.");
            var off = s.Clone(); off.commitmentRulesStartWeek = 0;
            Assert.That(EpisodeEngine.PactPlanRulesOn(off), Is.False, "Without the commitment rules nobody answers as an ally.");

            // Clamped as every enable is: never later than the week after the season's own.
            var later = ContentCatalog.Create(7);
            later.week = 4;
            EpisodeEngine.EnablePactPlans(later, 40);
            Assert.That(later.pactPlanRulesStartWeek, Is.EqualTo(5));
            EpisodeEngine.EnablePactPlans(later, -3);
            Assert.That(later.pactPlanRulesStartWeek, Is.EqualTo(1));
            Assert.Throws<ArgumentNullException>(() => EpisodeEngine.EnablePactPlans(null));
        }

        // The reach amended in place for BALANCE plan §4 Q1 (eight at fifty, cap twelve, reached only toss-ups:
        // mean come-round odds 0.17 in PactPlanSeasonDigests, 0.10 in the lab): twenty at a view of ten or more,
        // scaled down to nothing at zero, Loyal half as much again, cap thirty.
        [TestCase(0, "", 0)] [TestCase(-20, "", 0)] [TestCase(5, "", 10)] [TestCase(10, "", 20)] [TestCase(90, "", 20)]
        [TestCase(10, "Loyal", 30)] [TestCase(100, "Loyal", 30)] [TestCase(5, "Loyal", 15)] [TestCase(100, "Sneaky", 0)]
        public void ACountersReachIsTheObligationsSizing(double view, string trait, double reach)
        {
            var traits = trait.Length == 0 ? new List<string> { "Social" } : new List<string> { trait };
            Assert.That(PactPlans.CounterReach(view, traits), Is.EqualTo(reach).Within(1e-9));
        }

        [TestCase(0, 0, 0)] [TestCase(0, 5, 0)] [TestCase(8, 0, 1)] [TestCase(8, 2, 0.75)] [TestCase(8, 4, 0.5)]
        [TestCase(8, 8, 0)] [TestCase(8, 30, 0)] [TestCase(12, 3, 0.75)]
        public void TheOddsOfComingRoundAreTheReachLessTheMarginOverTheReach(double reach, double margin, double odds)
        {
            Assert.That(PactPlans.ComeRoundOdds(reach, margin), Is.EqualTo(odds).Within(1e-9));
        }

        [Test]
        public void TheMembersPlanIsTheNomineeMoreOfTheSaysName()
        {
            PlanSay Say(string member, string target) => new PlanSay { memberId = member, targetId = target };
            Assert.That(PactPlans.MembersPlan(new List<PlanSay>()), Is.Null, "Nobody said.");
            Assert.That(PactPlans.IsSplit(new List<PlanSay>()), Is.False, "No says is no split.");
            Assert.That(PactPlans.MembersPlan(new[] { Say("a", "x") }), Is.EqualTo("x"));
            Assert.That(PactPlans.MembersPlan(new[] { Say("a", "x"), Say("b", "y") }), Is.Null, "One each is a split.");
            Assert.That(PactPlans.IsSplit(new[] { Say("a", "x"), Say("b", "y") }), Is.True);
            Assert.That(PactPlans.MembersPlan(new[] { Say("a", "y"), Say("b", "x"), Say("c", "x") }), Is.EqualTo("x"), "More says wins.");
            Assert.That(PactPlans.MembersPlan(new[] { Say("a", "x"), Say("b", "x"), Say("c", "y"), Say("d", "y") }), Is.Null);
            Assert.That(PactPlans.IsSplit(new[] { Say("a", "x"), Say("b", "x"), Say("c", "y") }), Is.False);
        }

        [Test]
        public void TheCoinsAreKeyedByTheWeekThePactAndTheMember()
        {
            var s = ContentCatalog.Create(7);
            s.week = 4;
            Assert.That(PactPlans.CounterKey(s, "p", "m"), Is.EqualTo("w4:pact-counter:p:m"));
            Assert.That(PactPlans.PlanKey(s, "p", "m"), Is.EqualTo("w4:pact-plan:p:m"));
        }

        [Test]
        public void APactOfThreeInTheHouseIsAWarRoomAndAPairIsNot()
        {
            var s = Campaign();
            var npcs = NpcIds(s);
            var trio = Pact(s, PactId, PactName, s.playerId, npcs[3], npcs[4]);
            var pair = Pact(s, "alliance-pair", "The Pair", s.playerId, npcs[0]);
            Assert.That(PactPlans.IsWarRoomPact(s, trio), Is.True);
            Assert.That(PactPlans.IsWarRoomPact(s, pair), Is.False, "Two in the house is a pair, whatever it began as.");
            s.Find(npcs[4]).status = ContestantStatus.Evicted;
            Assert.That(PactPlans.IsWarRoomPact(s, trio), Is.False, "A trio down to two in the house is a pair again.");
            s.Find(npcs[4]).status = ContestantStatus.Active;
            trio.active = false;
            Assert.That(PactPlans.IsWarRoomPact(s, trio), Is.False, "An ended pact never meets.");
            Assert.That(PactPlans.IsWarRoomPact(s, new AllianceState { id = "x", name = "X", active = true, members = new List<string> { npcs[0], npcs[1], npcs[2] } }),
                Is.False, "Nor one the player is not in.");
        }

        [Test]
        public void TheSaysAreEachAllysOwnLeanAndANomineeSaysTheOther()
        {
            var s = WarRoom(out var pact, extra: 1);
            var npcs = NpcIds(s);
            var says = PactPlans.Says(s, pact);
            Assert.That(says.Select(x => x.memberId), Is.EqualTo(new[] { npcs[3], npcs[4], npcs[1] }), "In the pact's order, everybody warm on the player saying.");
            Assert.That(says[0].targetId, Is.EqualTo(WebEvictionVoting.EvaluateNative(s, npcs[3]).selectedNomineeId), "A voter's own lean, without the bloc,");
            Assert.That(says[1].targetId, Is.EqualTo(WebEvictionVoting.EvaluateNative(s, npcs[4]).selectedNomineeId));
            Assert.That(says[2].targetId, Is.EqualTo(npcs[2]), "and a nominee the other nominee.");
            string json = Json(s);
            PactPlans.Says(s, pact);
            Assert.That(Json(s), Is.EqualTo(json), "Reading the says changes nothing and draws nothing.");

            // A member gone cold keeps quiet, and so does one whose betrayal the player knows of.
            SetScore(s, npcs[4], s.playerId, Allegiance.QuietLine - 1);
            Assert.That(PactPlans.Says(s, pact).Select(x => x.memberId), Is.EqualTo(new[] { npcs[3], npcs[1] }), "Cold: quiet.");
            SetScore(s, npcs[4], s.playerId, 30);
            KnownBetrayal(s, npcs[3]);
            Assert.That(Allegiance.KnownBetrayed(s, npcs[3]), Is.True, "Precondition: a betrayal the player knows of.");
            Assert.That(PactPlans.Says(s, pact).Select(x => x.memberId), Is.EqualTo(new[] { npcs[4], npcs[1] }), "Known betrayer: quiet.");
        }

        [Test]
        public void ConvenesNeedsTheBlockAVoterNoPlanOrCallAndASay()
        {
            var s = WarRoom(out var pact);
            var npcs = NpcIds(s);
            Assert.That(PactPlans.CouldConvene(s, pact), Is.True);
            Assert.That(PactPlans.Convenes(s, pact), Is.True);

            var off = s.Clone(); off.pactPlanRulesStartWeek = 0;
            Assert.That(PactPlans.CouldConvene(off, pact), Is.False, "Never without the rules.");
            var social = s.Clone(); social.phase = EpisodePhase.Social;
            Assert.That(PactPlans.CouldConvene(social, pact), Is.False, "Never before the block is set.");
            var called = s.Clone();
            called.ledger.calls.Add(new BlocCallRow { week = s.week, allianceId = PactId, callerId = s.playerId, targetId = s.nominees[0] });
            Assert.That(PactPlans.CouldConvene(called, pact), Is.False, "Not where the pact has a call this week.");
            var planned = s.Clone();
            planned.ledger.plans.Add(new PactPlanRow { week = s.week, allianceId = PactId, throughId = npcs[3], stance = PactPlanStance.Low });
            Assert.That(PactPlans.CouldConvene(planned, pact), Is.False, "Not where the pact has a plan this week.");
            var cold = s.Clone();
            SetScore(cold, npcs[3], cold.playerId, -40); SetScore(cold, npcs[4], cold.playerId, -40);
            Assert.That(PactPlans.CouldConvene(cold, pact), Is.True, "Whether anybody says is theirs: the player can see it could convene,");
            Assert.That(PactPlans.Convenes(cold, pact), Is.False, "and with nobody saying it does not.");
            // Met as C6's then - nobody at it had a say - it meets no more this week, and the player can see it cannot.
            var metCold = Apply(new EpisodeEngine(cold), EpisodeCommandKind.AllianceMeet, npcs[3], text: PactId);
            Assert.That(metCold.accepted, Is.True, metCold.reason);
            var coldPact = metCold.state.alliances.Single(a => a.id == PactId);
            Assert.That(PactPlans.ThisWeek(metCold.state, PactId), Is.Null, "Precondition: nobody had a say, so the meeting was C6's,");
            Assert.That(EpisodeEngine.MetThisWeek(metCold.state, coldPact), Is.True, "and the pact has met this week.");
            Assert.That(PactPlans.CouldConvene(metCold.state, coldPact), Is.False, "Not where the pact has met this week.");
            var noVoter = Campaign();
            var hohAndNominees = Pact(noVoter, PactId, PactName, noVoter.playerId, NpcIds(noVoter)[0], NpcIds(noVoter)[1]);
            Warm(noVoter, hohAndNominees);
            Assert.That(PactPlans.CouldConvene(noVoter, hohAndNominees), Is.False, "Not with nobody of it but the player voting.");
        }

        [Test]
        public void ALieLowAndAnAgreeSettleOnTheMembersPlanOrOnASplitTheFoundersSay()
        {
            var s = WarRoom(out var pact, extra: 1);
            var npcs = NpcIds(s);
            string a = s.nominees[0], b = s.nominees[1];
            // Two say a, one says b: the plan is a.
            var row = Row(s, (npcs[3], a), (npcs[4], a), (npcs[1], b));
            var low = PactPlans.Settle(s, pact, row, PactPlans.LieLow, null);
            Assert.That((low.stance, low.targetId), Is.EqualTo((PactPlanStance.Low, a)));
            Assert.That(low.callerId, Is.EqualTo(npcs[3]), "An NPC calls it: the first who ended on it, the founder being the player.");
            var lapse = PactPlans.Settle(s, pact, row, PactPlans.Lapse, null);
            Assert.That((lapse.stance, lapse.targetId, lapse.callerId), Is.EqualTo((PactPlanStance.Lapsed, a, npcs[3])), "The lapse is a lie-low.");
            var agree = PactPlans.Settle(s, pact, row, PactPlans.Agree, a);
            Assert.That((agree.stance, agree.targetId, agree.callerId), Is.EqualTo((PactPlanStance.Agreed, a, s.playerId)), "Going with it: the player calls it.");
            Assert.That(agree.cameRound, Is.Empty);

            // A split: on lying low, the NPC founder's say; with no NPC founder, the first say in the pact's order.
            var split = Row(s, (npcs[3], b), (npcs[4], a));
            Assert.That(PactPlans.IsSplit(split.says), Is.True);
            Assert.That(PactPlans.Settle(s, pact, split, PactPlans.LieLow, null).targetId, Is.EqualTo(b), "The player founded it: the first say.");
            var npcFounded = s.Clone();
            var theirs = npcFounded.alliances.Single(x => x.id == PactId);
            theirs.members = new List<string> { npcs[4], npcs[3], npcFounded.playerId, npcs[1] };
            theirs.playerJoined = true;
            Assert.That(PactPlans.Settle(npcFounded, theirs, Row(npcFounded, (npcs[3], b), (npcs[4], a)), PactPlans.LieLow, null).targetId,
                Is.EqualTo(a), "An NPC founder's say decides a split.");
            // On a split the player may go with either nominee.
            Assert.That(PactPlans.Settle(s, pact, split, PactPlans.Agree, a).targetId, Is.EqualTo(a));
            Assert.That(PactPlans.Settle(s, pact, split, PactPlans.Agree, b).targetId, Is.EqualTo(b));
            Assert.That(PactPlans.AnswerKind(s, pact, split, a), Is.EqualTo(PactPlans.Agree), "Either nominee is agreeing on a split.");
            Assert.That(PactPlans.AnswerKind(s, pact, row, b), Is.EqualTo(PactPlans.Counter), "The other nominee is the counter.");
            Assert.That(PactPlans.AnswerKind(s, pact, row, null), Is.EqualTo(PactPlans.LieLow));
        }

        [Test]
        public void APlanIsVoidWhenNoSayStandsThePactHasEndedOrTheBlockMoved()
        {
            var s = WarRoom(out var pact);
            var npcs = NpcIds(s);
            var row = Row(s, (npcs[3], s.nominees[0]));
            var ended = s.Clone(); ended.alliances.Single(x => x.id == PactId).active = false;
            Assert.That(PactPlans.Settle(ended, ended.alliances.Single(x => x.id == PactId), row, PactPlans.Lapse, null).Void, Is.True, "An ended pact.");
            var gone = s.Clone(); gone.alliances.Single(x => x.id == PactId).members.Remove(npcs[3]);
            Assert.That(PactPlans.Settle(gone, gone.alliances.Single(x => x.id == PactId), row, PactPlans.Lapse, null).Void, Is.True,
                "The only one who said was cut out: their say goes with them.");
            var quiet = Row(s);
            Assert.That(PactPlans.Settle(s, pact, quiet, PactPlans.LieLow, null).Void, Is.True, "Nobody said.");
            var moved = s.Clone(); moved.nominees = new List<string> { s.nominees[1] };
            Assert.That(PactPlans.Settle(moved, moved.alliances.Single(x => x.id == PactId), row, PactPlans.Lapse, null).Void, Is.True, "The block no longer stands.");
            var settled = PactPlans.Settle(s, pact, row, PactPlans.Lapse, null);
            Assert.That(settled.Void, Is.False);
            Assert.That(PactPlans.Settle(s, null, row, PactPlans.Lapse, null).Void, Is.True, "A pact that is gone.");
        }

        [Test]
        public void ACounterCarriesOnTheTallyAndATieGoesToTheFoundersSide()
        {
            var s = WarRoom(out var pact, extra: 0);
            var npcs = NpcIds(s);
            string plan = s.nominees[0], counter = s.nominees[1];
            // Both voters said the plan and neither can come round (Sneaky): two against the player's one.
            foreach (var id in new[] { npcs[3], npcs[4] }) s.Find(id).traits = new List<string> { "Sneaky" };
            var row = Row(s, (npcs[3], plan), (npcs[4], plan));
            var held = PactPlans.Settle(s, pact, row, PactPlans.Counter, counter);
            Assert.That(held.cameRound, Is.Empty, "The Sneaky never come round.");
            Assert.That((held.stance, held.targetId, held.counterId), Is.EqualTo((PactPlanStance.Countered, plan, counter)), "Two to one: the plan holds,");
            Assert.That(held.callerId, Is.EqualTo(npcs[3]), "and an NPC calls it.");

            // A pact of four, the first nominee in it saying the other: the fourth and the nominee say the
            // plan, the fifth the counter - two for the plan, two for the counter with the player's: a tie.
            var trio = WarRoom(out var bigger, extra: 1);
            foreach (var id in new[] { npcs[3], npcs[4], npcs[1] }) trio.Find(id).traits = new List<string> { "Sneaky" };
            plan = trio.nominees[1]; counter = trio.nominees[0];
            var tie = Row(trio, (npcs[3], plan), (npcs[4], counter), (npcs[1], plan));
            Assert.That(PactPlans.MembersPlan(tie.says), Is.EqualTo(plan));
            var tied = PactPlans.Settle(trio, bigger, tie, PactPlans.Counter, counter);
            Assert.That(tied.cameRound, Is.Empty);
            Assert.That(tied.targetId, Is.EqualTo(counter), "A tie goes to the founder's side: the player founded it.");
            Assert.That(tied.callerId, Is.EqualTo(trio.playerId), "The counter carried: the player calls it.");
            var npcFounded = trio.Clone();
            var theirs = npcFounded.alliances.Single(x => x.id == PactId);
            theirs.members = new List<string> { npcs[3], npcFounded.playerId, npcs[4], npcs[1] };
            theirs.playerJoined = true;
            var npcTie = PactPlans.Settle(npcFounded, theirs, Row(npcFounded, (npcs[3], plan), (npcs[4], counter), (npcs[1], plan)), PactPlans.Counter, counter);
            Assert.That(npcTie.targetId, Is.EqualTo(plan), "An NPC founder's side is their own final say.");
            Assert.That(npcTie.callerId, Is.EqualTo(npcs[3]), "and the founder, bound to it, calls it.");
            var noFounder = npcFounded.Clone();
            var gone = noFounder.alliances.Single(x => x.id == PactId);
            noFounder.Find(npcs[3]).status = ContestantStatus.Evicted;
            noFounder.Find(npcs[0]).traits = new List<string> { "Sneaky" };
            gone.members = new List<string> { npcs[3], noFounder.playerId, npcs[4], npcs[1], npcs[0] };
            var orphan = PactPlans.Settle(noFounder, gone, Row(noFounder, (npcs[0], plan), (npcs[4], counter), (npcs[1], plan)), PactPlans.Counter, counter);
            Assert.That(orphan.targetId, Is.EqualTo(plan), "With no founder here the plan stands.");
        }

        [Test]
        public void AMemberComesRoundOnTheirKeyedCoinUnderTheirOdds()
        {
            int came = 0, stayed = 0;
            foreach (uint seed in Enumerable.Range(1, 40).Select(n => (uint)n))
            {
                var s = WarRoom(out var pact, seed: seed);
                var npcs = NpcIds(s);
                string member = npcs[(int)(seed % 2) + 3];
                // On odd seeds a Loyal member who thinks the world of the player: the counter's furthest reach,
                // thirty (twelve before the reach was amended for BALANCE plan §4 Q1, since when nearly every such
                // member comes round); on even seeds one barely warm to the player, a view of three: reach six.
                bool far = seed % 2 == 1;
                double view = far ? 100 : 3, reach = far ? 30 : 20 * 0.3;
                s.Find(member).traits = new List<string> { far ? "Loyal" : "Social" };
                SetScore(s, member, s.playerId, view);
                double odds = PactPlans.ComeRoundOdds(s, member);
                var known = Allegiance.AsThePlayerKnows(s);
                double margin = WebEvictionVoting.EvaluateNative(known, member).margin;
                Assert.That(odds, Is.EqualTo(PactPlans.ComeRoundOdds(PactPlans.CounterReach(view, s.Find(member).traits), margin)).Within(1e-12));
                Assert.That(odds, Is.EqualTo(Math.Max(0, Math.Min(1, (reach - margin) / reach))).Within(1e-12), "Seed " + seed + ": reach " + reach + ", margin " + margin + ".");
                bool coin = StoryRandom.Unit(s, PactPlans.CounterKey(s, PactId, member)) < odds;
                bool round = PactPlans.ComesRound(s, PactId, member);
                Assert.That(round, Is.EqualTo(odds > 0 && coin), "Seed " + seed + ": the coin under the odds.");
                if (round) came++; else stayed++;
                uint before = s.randomState;
                PactPlans.ComesRound(s, PactId, member);
                Assert.That(s.randomState, Is.EqualTo(before), "Never the season's stream.");
            }
            TestContext.Out.WriteLine("came round " + came + ", stayed " + stayed);
            Assert.That(came, Is.GreaterThan(0), "Some members came round,");
            Assert.That(stayed, Is.GreaterThan(0), "and some did not.");
        }

        [Test]
        public void NobodyOnTheBlockNobodySneakyAndNobodyTheyKnowHasLapsedComesRound()
        {
            var s = WarRoom(out var pact, extra: 1);
            var npcs = NpcIds(s);
            SetScore(s, npcs[3], s.playerId, 100);
            Assert.That(PactPlans.ComesRound(s, PactId, npcs[1]), Is.False, "A nominee never comes round.");
            s.Find(npcs[3]).traits = new List<string> { "Sneaky" };
            Assert.That(PactPlans.ComeRoundOdds(s, npcs[3]), Is.Zero, "A Sneaky member's reach is nothing.");
            Assert.That(PactPlans.ComesRound(s, PactId, npcs[3]), Is.False);
            s.Find(npcs[3]).traits = new List<string> { "Loyal" };
            KnownBetrayal(s, npcs[3]);
            Assert.That(Allegiance.Lapsed(Allegiance.AsThePlayerKnows(s), npcs[3]), Is.True, "Precondition: lapsed, as the player knows it.");
            Assert.That(PactPlans.ComesRound(s, PactId, npcs[3]), Is.False, "Somebody the player knows has turned never comes round.");
        }

        /// <summary>
        /// D3-H2: a member whose betrayal only a ballot the player cannot place would tell - truly lapsed -
        /// gets the same odds, the same gate, and comes round or goes along exactly as an identical member
        /// who never turned, so no outcome tells the player of a ballot they were never told.
        /// </summary>
        [Test]
        public void AHiddenBetrayerGetsTheSameOddsAndGateAsAnIdenticalNonBetrayer()
        {
            int compared = 0;
            foreach (uint seed in Enumerable.Range(1, 30).Select(n => (uint)n))
            {
                var honest = WarRoom(out var pact, seed: seed);
                var npcs = NpcIds(honest);
                string member = npcs[3];
                var hidden = honest.Clone();
                HiddenBetrayal(hidden, member);
                Assert.That(Allegiance.Lapsed(hidden, member), Is.True, "Precondition: truly lapsed.");
                Assert.That(Allegiance.KnownBetrayed(hidden, member), Is.False, "Precondition: by a ballot the player cannot place.");
                Assert.That(Allegiance.Lapsed(Allegiance.AsThePlayerKnows(hidden), member), Is.False, "As the player knows it, they hold.");
                Assert.That(PactPlans.ComeRoundOdds(hidden, member), Is.EqualTo(PactPlans.ComeRoundOdds(honest, member)), "Seed " + seed + ": the same odds,");
                Assert.That(PactPlans.ComesRound(hidden, PactId, member), Is.EqualTo(PactPlans.ComesRound(honest, PactId, member)), "the same outcome.");
                string plan = honest.nominees[0], counter = honest.nominees[1];
                var row = Row(honest, (member, plan), (npcs[4], plan));
                var a = PactPlans.Settle(honest, pact, row, PactPlans.Counter, counter);
                var b = PactPlans.Settle(hidden, hidden.alliances.Single(x => x.id == PactId), row, PactPlans.Counter, counter);
                Assert.That(b.cameRound, Is.EqualTo(a.cameRound), "Seed " + seed + ": who came round.");
                Assert.That(b.followed, Is.EqualTo(a.followed), "Seed " + seed + ": who is with the call.");
                Assert.That(b.targetId, Is.EqualTo(a.targetId));
                compared++;
            }
            Assert.That(compared, Is.EqualTo(30));
        }

        [Test]
        public void BoundMembersFollowWithNoDrawAndDissentersGoAlongOnTheirKeyedCoin()
        {
            var s = WarRoom(out var pact, extra: 1);
            var npcs = NpcIds(s);
            // The first nominee is in the pact and says the other: that is the plan.
            string plan = s.nominees[1], other = s.nominees[0];
            // npcs[3] said the plan, npcs[4] the other: going with the plan binds npcs[3]; npcs[4] decides.
            var row = Row(s, (npcs[3], plan), (npcs[4], other), (npcs[1], plan));
            var agreed = PactPlans.Settle(s, pact, row, PactPlans.Agree, plan);
            Assert.That(agreed.followed, Does.Contain(npcs[3]), "Bound.");
            Assert.That(agreed.offered, Is.EqualTo(new[] { npcs[3], npcs[4] }), "Only those who vote are offered the call: the nominee's say counts, but casts no ballot.");
            Assert.That(agreed.followed, Has.No.Member(s.playerId), "The player is never bound.");
            var known = Allegiance.AsThePlayerKnows(s);
            var snapshot = WebVotingBlocs.FromNative(known);
            double loyalty = WebVotingBlocs.Loyalty(snapshot, snapshot.alliances.Single(x => x.id == PactId), npcs[4], s.playerId, plan, WebVotingBlocs.ProxyTrust(snapshot));
            bool goes = WebVotingBlocs.Complies(loyalty, s.Find(npcs[4]).traits, () => StoryRandom.Unit(s, PactPlans.PlanKey(s, PactId, npcs[4])));
            Assert.That(agreed.followed.Contains(npcs[4]), Is.EqualTo(goes), "The dissenter goes along by the round's own rule on their keyed coin.");

            // A dissenter who has lapsed as the player knows it never goes along, whatever the coin.
            var cold = s.Clone();
            SetScore(cold, npcs[4], cold.playerId, Allegiance.QuietLine - 5);
            Assert.That(PactPlans.Settle(cold, cold.alliances.Single(x => x.id == PactId), row, PactPlans.Agree, plan).followed, Has.No.Member(npcs[4]));
            // Somebody brought in since the meeting decides as a dissenter and joins the plan's record.
            var brought = s.Clone();
            var grown = brought.alliances.Single(x => x.id == PactId);
            grown.members.Remove(npcs[4]);
            var late = Row(brought, (npcs[3], plan), (npcs[1], plan));
            late.present.Remove(npcs[4]);
            grown.members.Add(npcs[4]);
            var settled = PactPlans.Settle(brought, grown, late, PactPlans.Agree, plan);
            Assert.That(settled.joined, Is.EqualTo(new[] { npcs[4] }), "Brought in since: offered, and on the record.");
        }

        [Test]
        public void TheWordsHoldNoNumberAndNameEverySay()
        {
            var s = WarRoom(out var pact, extra: 1);
            var npcs = NpcIds(s);
            string first = s.nominees[0], a = s.nominees[1];
            var at = EpisodeEngine.AtTheMeeting(s, pact);
            var says = new List<PlanSay> { new PlanSay { memberId = npcs[3], targetId = first }, new PlanSay { memberId = npcs[1], targetId = a } };
            string sentence = PactPlans.SaysSentence(s, at, says);
            Assert.That(sentence, Is.EqualTo(s.Find(npcs[3]).name + " wants " + s.Find(first).name + " out; " + s.Find(npcs[1]).name + " wants "
                + s.Find(a).name + " out; " + s.Find(npcs[4]).name + " kept quiet."));
            says.Add(new PlanSay { memberId = npcs[4], targetId = first });
            Assert.That(PactPlans.SaysSentence(s, at, says), Does.StartWith(s.Find(npcs[3]).name + " and " + s.Find(npcs[4]).name + " want "));
            Assert.That(PactPlans.WarRoomLine(s, pact, at, says), Does.StartWith(PactName + " met where nobody listens: you, "));
            Assert.That(PactPlans.Heading(PactName), Is.EqualTo("THE WAR ROOM'S PLAN"));
            var row = Row(s, (npcs[3], a), (npcs[1], a));
            foreach (var line in PactPlans.CardFacts(s, pact, row))
                Assert.That(PactPlans.CardText(line), Does.Not.Match("[0-9]"), "No number on the card: " + PactPlans.CardText(line));
            var facts = PactPlans.CardFacts(s, pact, row);
            Assert.That(facts.Select(f => f.memberId), Is.EqualTo(new[] { npcs[3], npcs[4], npcs[1] }));
            Assert.That(facts[1].text, Does.EndWith(" kept quiet"));
            Assert.That(facts[2].whip, Is.Null, "A nominee casts no ballot to read.");
            foreach (var f in facts.Where(f => f.whip != null))
                Assert.That(new[] { VoteRead.Firm, VoteRead.Leaning, VoteRead.Torn, VoteRead.Unknown }, Has.Member(f.whip));
            foreach (string kind in new[] { PactPlans.Agree, PactPlans.Counter, PactPlans.LieLow, PactPlans.Lapse })
            {
                var settled = PactPlans.Settle(s, pact, row, kind, kind == PactPlans.Counter ? first : kind == PactPlans.Agree ? a : null);
                Assert.That(settled.Void, Is.False, kind);
                string line = PactPlans.SettledLine(s, pact, null, settled, kind == PactPlans.Counter ? first : a);
                Assert.That(line, Does.Not.Match("[0-9]"), kind + ": " + line);
            }
            Assert.That(PactPlans.SettledLine(s, pact, null, new PactPlans.Settlement { stance = PactPlanStance.Void }, null),
                Is.EqualTo(PactName + "'s plan came to nothing."));
        }

        // ------------------------------------------------------------ D3-S3: the engine

        [Test]
        public void WithoutTheRulesAPactOfThreeMeetsAndCallsAsBeforeAndTheAnswerIsRefused()
        {
            // Free time: C6's meeting of the pact, as every recorded season holds it.
            var free = FreeTime(false, out var pact);
            var npcs = NpcIds(free);
            var met = Apply(new EpisodeEngine(free), EpisodeCommandKind.AllianceMeet, npcs[3], text: PactId);
            Assert.That(met.accepted, Is.True, met.reason);
            Assert.That(met.state.ledger.plans, Is.Empty);
            Assert.That(EpisodeEngine.MeetingPact(free, npcs[3])?.id, Is.EqualTo(PactId), "Offered in free time.");

            // The campaign: C6's meeting with one ally's claim, and a call the levers always took.
            var s = WarRoom(out pact, rules: false);
            Assert.That(EpisodeEngine.PactPlanRulesOn(s), Is.False);
            var meeting = Apply(new EpisodeEngine(s), EpisodeCommandKind.AllianceMeet, npcs[3], text: PactId);
            Assert.That(meeting.accepted, Is.True, meeting.reason);
            Assert.That(meeting.state.ledger.plans, Is.Empty, "No plan,");
            Assert.That(meeting.state.ledger.claims.Count(k => k.source == ClaimSource.Ally), Is.EqualTo(1), "and C6's claim.");
            var call = Apply(new EpisodeEngine(s), EpisodeCommandKind.CallTheVote, npcs[3], s.nominees[0], PactId);
            Assert.That(call.accepted, Is.True, call.reason);

            // Kind 62 without the rules: refused before anything is spent, drawn or logged.
            string before = Json(meeting.state);
            var engine = new EpisodeEngine(meeting.state);
            var answer = Apply(engine, EpisodeCommandKind.AnswerPactPlan, npcs[3], s.nominees[0], PactId);
            Assert.That(answer.accepted, Is.False);
            Assert.That(answer.reason, Is.EqualTo(EpisodeEngine.WaveDKindRefusal));
            Assert.That(Json(engine.Snapshot), Is.EqualTo(before));
        }

        /// <summary>D3-M1: the engine refuses a pact of three or more named by the command before the block is set, before anything is spent.</summary>
        [Test]
        public void TheEngineRefusesANamedPactOfThreeBeforeTheBlockIsSet()
        {
            var s = FreeTime(true, out var pact);
            var npcs = NpcIds(s);
            Assert.That(EpisodeEngine.MeetingPact(s, npcs[3]), Is.Null, "The house never offers it,");
            string before = Json(s);
            var engine = new EpisodeEngine(s);
            var refused = Apply(engine, EpisodeCommandKind.AllianceMeet, npcs[3], text: PactId);
            Assert.That(refused.accepted, Is.False, "and the engine refuses it by its id.");
            Assert.That(refused.reason, Is.EqualTo(PactPlans.NotYetRefusal(PactName)));
            Assert.That(Json(engine.Snapshot), Is.EqualTo(before), "Nothing spent, drawn or logged.");
            Assert.That(Apply(engine, EpisodeCommandKind.AllianceMeet, npcs[3]).reason, Is.EqualTo(PactPlans.NotYetRefusal(PactName)),
                "Unnamed, the meeting falls to the same pact and the same refusal.");

            // A pair still meets in free time.
            var pair = s.Clone();
            pair.alliances.Single(a => a.id == PactId).members.Remove(npcs[4]);
            Assert.That(EpisodeEngine.MeetingPact(pair, npcs[3])?.id, Is.EqualTo(PactId));
            Assert.That(Apply(new EpisodeEngine(pair), EpisodeCommandKind.AllianceMeet, npcs[3], text: PactId).accepted, Is.True);
        }

        [Test]
        public void AWarRoomOpensAPlanWithTheSaysAndDrawsWhatAMeetingDraws()
        {
            var s = Decided(out var pact);
            var npcs = NpcIds(s);
            Assert.That(EpisodeEngine.MeetingPact(s, npcs[3])?.id, Is.EqualTo(PactId), "Offered once the block is set.");
            var on = Apply(new EpisodeEngine(s), EpisodeCommandKind.AllianceMeet, npcs[3], text: PactId);
            Assert.That(on.accepted, Is.True, on.reason);
            var off = s.Clone(); off.pactPlanRulesStartWeek = 0;
            var c6 = Apply(new EpisodeEngine(off), EpisodeCommandKind.AllianceMeet, npcs[3], text: PactId);
            Assert.That(c6.accepted, Is.True, c6.reason);

            var after = on.state;
            var row = after.ledger.plans.Single();
            Assert.That((row.week, row.allianceId, row.throughId, row.stance), Is.EqualTo((s.week, PactId, npcs[3], PactPlanStance.Open)));
            Assert.That(row.present, Is.EqualTo(new[] { s.playerId, npcs[3], npcs[4] }), "The player and everybody of it in the house.");
            Assert.That(row.says.Select(x => (x.memberId, x.targetId)), Is.EqualTo(new[] { (npcs[3], npcs[1]), (npcs[4], npcs[1]) }), "Each ally's say.");
            Assert.That(row.targetId + row.callerId + row.counterId, Is.Empty, "Open: nobody named yet.");
            var line = after.events.Last(e => e.kind == "conversation");
            Assert.That(line.text, Is.EqualTo(PactPlans.WarRoomLine(after, after.alliances.Single(a => a.id == PactId), new[] { npcs[3], npcs[4] }, row.says)));
            Assert.That(line.text, Does.EndWith(Name(after, npcs[3]) + " and " + Name(after, npcs[4]) + " want " + Name(after, npcs[1]) + " out."));
            Assert.That(line.audienceIds, Is.EquivalentTo(new[] { s.playerId, npcs[3], npcs[4] }));
            Assert.That(after.ledger.claims.Any(k => k.source == ClaimSource.Ally), Is.False, "No ally's claim at a war room (§6 Q8),");
            Assert.That(c6.state.ledger.claims.Any(k => k.source == ClaimSource.Ally), Is.True, "where C6's meeting makes one.");
            Assert.That(after.randomState, Is.EqualTo(c6.state.randomState), "A war room draws exactly what C6's meeting draws.");
            Assert.That(after.events.Count(e => e.kind == WaveDEventKinds.PactPlan), Is.Zero, "The plan's line waits for its answer.");
            var nominees = s.nominees.Select(id => Name(s, id)).ToList();
            Assert.That(after.memories.Skip(s.memories.Count).Where(m => nominees.Any(n => m.text.Contains(n))), Is.Empty, "No memory names a nominee.");
            Assert.That(EpisodeEngine.MetThisWeek(after, after.alliances.Single(a => a.id == PactId)), Is.True);
            Assert.That(EpisodeEngine.MeetingPact(after, npcs[4]), Is.Null, "Once a week.");
            Valid(after);
        }

        [Test]
        public void APactMeetsOnceAWeekEvenWithEveryCooldownTaken()
        {
            var s = Decided(out var pact);
            var npcs = NpcIds(s);
            for (int i = s.story.cooldowns.Count; i < 256; i++) s.story.cooldowns.Add(new StoryCooldownState { key = "filler:" + i, untilWeek = s.week + 5 });
            Valid(s);
            var engine = new EpisodeEngine(s);
            Assert.That(Apply(engine, EpisodeCommandKind.AllianceMeet, npcs[3], text: PactId).accepted, Is.True);
            var after = engine.Snapshot;
            Assert.That(EpisodeEngine.MetThisWeek(after, after.alliances.Single(a => a.id == PactId)), Is.False, "Precondition: the cooldown found no room.");
            Assert.That(EpisodeEngine.MeetingPact(after, npcs[4]), Is.Null, "The plan is the attempt: the house offers no second meeting,");
            string before = Json(after);
            var again = Apply(engine, EpisodeCommandKind.AllianceMeet, npcs[4], text: PactId);
            Assert.That(again.accepted, Is.False, "and the engine holds none.");
            Assert.That(again.reason, Is.EqualTo(PactName + " has already met this week."));
            Assert.That(Json(engine.Snapshot), Is.EqualTo(before));
        }

        [Test]
        public void APactOfThreeTakesNoCallOfItsOwnAndAPairStillDoes()
        {
            var s = Decided(out var pact);
            var npcs = NpcIds(s);
            Pact(s, "alliance-pair", "The Pair", s.playerId, npcs[3]);
            Valid(s);
            string before = Json(s);
            var engine = new EpisodeEngine(s);
            var refused = Apply(engine, EpisodeCommandKind.CallTheVote, npcs[3], s.nominees[0], PactId);
            Assert.That(refused.accepted, Is.False);
            Assert.That(refused.reason, Is.EqualTo(PactPlans.CallRefusal(PactName)));
            Assert.That(Json(engine.Snapshot), Is.EqualTo(before), "Refused before anything is spent or drawn.");
            Assert.That(Apply(engine, EpisodeCommandKind.CallTheVote, npcs[3], s.nominees[0], "alliance-pair").accepted, Is.True, "A pair calls as ever.");
        }

        [Test]
        public void GoingWithThePlanIsThePlayersCallAndBindsWhoSaidIt()
        {
            var open = Opened(out var pact);
            var npcs = NpcIds(open);
            string plan = npcs[1];
            var engine = new EpisodeEngine(open);
            var agreed = Apply(engine, EpisodeCommandKind.AnswerPactPlan, npcs[4], plan, PactId);
            Assert.That(agreed.accepted, Is.True, agreed.reason);
            var after = agreed.state;
            var row = after.ledger.plans.Single();
            Assert.That((row.stance, row.targetId, row.callerId), Is.EqualTo((PactPlanStance.Agreed, plan, after.playerId)));
            Assert.That(row.followed, Is.EqualTo(new[] { npcs[3], npcs[4] }), "Both said it: both bound, with no draw.");
            var call = after.ledger.calls.Single();
            Assert.That((call.week, call.allianceId, call.callerId, call.targetId), Is.EqualTo((open.week, PactId, after.playerId, plan)), "The week's call, in the player's name,");
            Assert.That(call.followed, Is.EqualTo(row.followed));
            Assert.That(call.defected, Is.Empty, "and nobody flagged.");
            foreach (string bound in row.followed)
                Assert.That(BlocPressure(after, bound, plan), Is.EqualTo(-40), "A bound member carries the call's directive: " + bound);
            Assert.That(after.randomState, Is.EqualTo(open.randomState), "Free, and nothing drawn.");
            Assert.That(after.nextSequence, Is.EqualTo(open.nextSequence + 1), "One line,");
            var line = after.events.Last();
            Assert.That(line.kind, Is.EqualTo(WaveDEventKinds.PactPlan));
            Assert.That(line.text, Is.EqualTo("You went with " + PactName + "'s plan: evict " + Name(after, plan) + ". "
                + Name(after, npcs[3]) + " and " + Name(after, npcs[4]) + " are with you."));
            Assert.That(line.audienceIds, Is.EquivalentTo(new[] { after.playerId, npcs[3], npcs[4] }), "to the pact.");
            Assert.That(after.socialActions + after.outOfPhaseSocialActions, Is.EqualTo(open.socialActions + open.outOfPhaseSocialActions), "No action spent.");
            Assert.That(PactPlans.OpenPlan(after, PactId), Is.Null);

            // Once: a second answer is refused before anything happens.
            string before = Json(after);
            var again = Apply(engine, EpisodeCommandKind.AnswerPactPlan, npcs[3], null, PactId);
            Assert.That(again.accepted, Is.False);
            Assert.That(again.reason, Is.EqualTo(PactPlans.SettledRefusal(PactName)));
            Assert.That(Json(engine.Snapshot), Is.EqualTo(before));
        }

        [Test]
        public void EveryAnswerLeavesTheSameStreamAndSaysOneLine()
        {
            var open = Opened(out var pact);
            var npcs = NpcIds(open);
            var answers = new[] { (npcs[1], PactPlanStance.Agreed), (npcs[2], PactPlanStance.Countered), ((string)null, PactPlanStance.Low) };
            foreach (var (nominee, stance) in answers)
            {
                var result = Apply(new EpisodeEngine(open), EpisodeCommandKind.AnswerPactPlan, npcs[3], nominee, PactId);
                Assert.That(result.accepted, Is.True, stance + ": " + result.reason);
                Assert.That(result.state.ledger.plans.Single().stance, Is.EqualTo(stance));
                Assert.That(result.state.randomState, Is.EqualTo(open.randomState), stance + ": the season's stream untouched.");
                Assert.That(result.state.nextSequence, Is.EqualTo(open.nextSequence + 1), stance + ": one line, one id.");
                Assert.That(result.state.events.Last().kind, Is.EqualTo(WaveDEventKinds.PactPlan));
                Valid(result.state);
            }
        }

        [Test]
        public void ACounterIsDecidedByTheKeyedCoinsAndTheTally()
        {
            var open = Opened(out var pact);
            var npcs = NpcIds(open);
            var row = open.ledger.plans.Single();
            var expected = PactPlans.Settle(open, pact, row, PactPlans.Counter, npcs[2]);
            var result = Apply(new EpisodeEngine(open), EpisodeCommandKind.AnswerPactPlan, npcs[3], npcs[2], PactId);
            Assert.That(result.accepted, Is.True, result.reason);
            var after = result.state.ledger.plans.Single();
            Assert.That(after.counterId, Is.EqualTo(npcs[2]));
            Assert.That(after.cameRound, Is.EqualTo(expected.cameRound));
            Assert.That(after.cameRound, Is.EqualTo(new[] { npcs[3], npcs[4] }.Where(id => PactPlans.ComesRound(open, PactId, id)).ToArray()),
                "Each who said the plan came round on their own coin.");
            Assert.That(after.targetId, Is.EqualTo(expected.targetId));
            int forCounter = 1 + after.cameRound.Count, forPlan = 2 - after.cameRound.Count;
            Assert.That(after.targetId, Is.EqualTo(forCounter >= forPlan ? npcs[2] : npcs[1]), "More says wins; a tie goes to the player, who founded it.");
            Assert.That(result.state.ledger.calls.Count, Is.EqualTo(after.callerId == result.state.playerId ? 1 : 0), "A call row only where the counter carried.");
            string text = result.state.events.Last().text;
            Assert.That(text, Does.StartWith("You pushed for " + Name(open, npcs[2]) + "."));
            Assert.That(text, Does.Contain(PactName + " goes with " + Name(open, after.targetId) + "."));
        }

        /// <summary>
        /// D2's beats between the meeting and the answer (§3.6), with the all-week rules on: the meeting's commit
        /// catches the house up at the campaign's first tick - as many beats as are due, each an act of the
        /// window fired at tick one - and draws what the same meeting draws without them; the answer, free,
        /// fires none, and reads the state after them: its counter is settled by that snapshot's coins and odds;
        /// and the same two commands replay to the same state.
        /// </summary>
        [Test]
        public void TheHousesBeatsFallBetweenTheMeetingAndTheAnswer()
        {
            var decided = Decided(out _);
            EpisodeEngine.EnableWeek(decided);
            EpisodeEngine.EnableAllWeek(decided);
            Valid(decided);
            var npcs = NpcIds(decided);
            var meeting = Command(decided, EpisodeCommandKind.AllianceMeet, npcs[3], null, PactId);
            var engine = new EpisodeEngine(decided);
            var opened = engine.Apply(meeting);
            Assert.That(opened.accepted, Is.True, opened.reason);
            var s = opened.state;
            var row = PactPlans.OpenPlan(s, PactId);
            Assert.That(row, Is.Not.Null, "The war room met.");
            var social = s.npcSocial;
            Assert.That(social.beatWindow, Is.EqualTo(Windows.AfterVeto));
            Assert.That(EpisodeEngine.WindowTick(s, Windows.AfterVeto), Is.EqualTo(1), "The meeting spent the campaign's first seat.");
            Assert.That(social.beatsFired, Is.EqualTo(EpisodeEngine.Due(social.beatPlan.Count, social.beatSeats, 1)));
            var beats = social.acts.Where(EpisodeEngine.IsBeat).ToList();
            Assert.That(beats, Is.Not.Empty, "The house acted after the meeting.");
            Assert.That(beats.All(a => a.window == Windows.AfterVeto && a.firedTick == 1), Is.True);

            var off = Decided(out _);
            EpisodeEngine.EnableWeek(off);
            var without = new EpisodeEngine(off).Apply(meeting);
            Assert.That(without.accepted, Is.True, without.reason);
            Assert.That(s.randomState, Is.EqualTo(without.state.randomState), "The beats drew nothing from the season's stream.");

            var pact = s.alliances.Single(a => a.id == PactId);
            var expected = PactPlans.Settle(s, pact, row, PactPlans.Counter, npcs[2]);
            var answer = Command(s, EpisodeCommandKind.AnswerPactPlan, npcs[3], npcs[2], PactId);
            var answered = engine.Apply(answer);
            Assert.That(answered.accepted, Is.True, answered.reason);
            var settled = answered.state.ledger.plans.Single();
            Assert.That(settled.cameRound, Is.EqualTo(expected.cameRound), "Each came round by the post-beat snapshot's coin and odds.");
            Assert.That(settled.targetId, Is.EqualTo(expected.targetId));
            Assert.That(answered.state.randomState, Is.EqualTo(s.randomState), "The answer draws nothing,");
            Assert.That(answered.state.npcSocial.beatsFired, Is.EqualTo(social.beatsFired), "and, free, fires no beat.");
            Assert.That(answered.state.npcSocial.acts.Count, Is.EqualTo(social.acts.Count));

            var replay = new EpisodeEngine(decided);
            Assert.That(Json(replay.Apply(meeting).state), Is.EqualTo(Json(s)));
            Assert.That(Json(replay.Apply(answer).state), Is.EqualTo(Json(answered.state)), "The same commands, the same season.");
        }

        [Test]
        public void LyingLowLeavesAPlanAnNpcLeadsThatTheBlocRoundReads()
        {
            var open = Opened(out var pact);
            var npcs = NpcIds(open);
            var engine = new EpisodeEngine(open);
            var low = Apply(engine, EpisodeCommandKind.AnswerPactPlan, npcs[3], null, PactId);
            Assert.That(low.accepted, Is.True, low.reason);
            var row = low.state.ledger.plans.Single();
            Assert.That((row.stance, row.targetId, row.callerId), Is.EqualTo((PactPlanStance.Low, npcs[1], npcs[3])), "The plan, an NPC calling it.");
            Assert.That(low.state.ledger.calls, Is.Empty, "No call row: every row is the player's to its readers.");
            Assert.That(low.state.events.Last().text, Is.EqualTo("You let " + PactName + "'s plan stand: evict " + Name(open, npcs[1]) + ", as "
                + Name(open, npcs[3]) + " and " + Name(open, npcs[4]) + " wanted."));
            var call = EpisodeEngine.PlanCallThisWeek(low.state, PactId);
            Assert.That((call.callerId, call.targetId), Is.EqualTo((npcs[3], npcs[1])));
            Assert.That(EpisodeEngine.CallThisWeek(low.state, PactId), Is.Null);
            foreach (string bound in row.followed)
                Assert.That(BlocPressure(low.state, bound, npcs[1]), Is.EqualTo(-40), "In the campaign: " + bound);
            var advanced = Apply(engine, EpisodeCommandKind.Advance);
            Assert.That(advanced.accepted, Is.True, advanced.reason);
            Assert.That(advanced.state.phase, Is.EqualTo(EpisodePhase.Eviction));
            foreach (string bound in row.followed)
                Assert.That(BlocPressure(advanced.state, bound, npcs[1]), Is.EqualTo(-40), "At the eviction: " + bound);
            Assert.That(advanced.state.events.Count(e => e.kind == WaveDEventKinds.PactPlan), Is.EqualTo(1), "A settled plan does not lapse again.");
        }

        [Test]
        public void AnOpenPlanLapsesLastAsTheCampaignClosesOrComesToNothing()
        {
            var open = Opened(out var pact);
            var npcs = NpcIds(open);
            var closed = Apply(new EpisodeEngine(open), EpisodeCommandKind.Advance);
            Assert.That(closed.accepted, Is.True, closed.reason);
            var row = closed.state.ledger.plans.Single();
            Assert.That((row.stance, row.targetId, row.callerId), Is.EqualTo((PactPlanStance.Lapsed, npcs[1], npcs[3])), "Settled as lying low would.");
            var said = closed.state.events.Where(e => e.sequence >= open.nextSequence).ToList();
            Assert.That(said.Last().kind, Is.EqualTo(WaveDEventKinds.PactPlan), "Last in the step: after the readings and the eviction eve.");
            Assert.That(said.Last().text, Does.StartWith("You let " + PactName + "'s plan stand: evict " + Name(open, npcs[1])));
            Assert.That(said.Any(e => e.kind == "campaign-close" && e.text == "Campaigning has closed. The house votes privately to evict."), Is.True,
                "The frozen campaign-close sentence is untouched.");
            Assert.That(closed.state.randomState, Is.EqualTo(new EpisodeEngine(PlanFree(open)).Apply(Command(PlanFree(open), EpisodeCommandKind.Advance)).state.randomState),
                "The lapse draws nothing from the season's stream.");

            // A pact that has ended by the close: the plan comes to nothing.
            var ended = open.Clone();
            ended.alliances.Single(a => a.id == PactId).active = false;
            var voided = Apply(new EpisodeEngine(ended), EpisodeCommandKind.Advance);
            Assert.That(voided.accepted, Is.True, voided.reason);
            var gone = voided.state.ledger.plans.Single();
            Assert.That((gone.stance, gone.targetId, gone.callerId), Is.EqualTo((PactPlanStance.Void, (string)null, (string)null)));
            Assert.That(voided.state.events.Last().text, Is.EqualTo(PactName + "'s plan came to nothing."));
            Assert.That(EpisodeEngine.PlanCallThisWeek(voided.state, PactId), Is.Null, "No call.");
        }

        [Test]
        public void ThePlayerWhoLeavesCannotAnswerAndThePlanLapsesWithoutThem()
        {
            var open = Opened(out var pact);
            var npcs = NpcIds(open);
            var engine = new EpisodeEngine(open);
            var left = Apply(engine, EpisodeCommandKind.LeaveAlliance, npcs[3], text: PactId);
            Assert.That(left.accepted, Is.True, left.reason);
            Assert.That(left.state.alliances.Single(a => a.id == PactId).members, Has.No.Member(left.state.playerId), "A pact of three goes on without the player.");
            string before = Json(left.state);
            var refused = Apply(engine, EpisodeCommandKind.AnswerPactPlan, npcs[3], npcs[1], PactId);
            Assert.That(refused.accepted, Is.False);
            Assert.That(refused.reason, Is.EqualTo(EpisodeEngine.NotYourPactRefusal));
            Assert.That(Json(engine.Snapshot), Is.EqualTo(before));
            var closed = Apply(engine, EpisodeCommandKind.Advance);
            Assert.That(closed.accepted, Is.True, closed.reason);
            var row = closed.state.ledger.plans.Single();
            Assert.That(row.stance, Is.EqualTo(PactPlanStance.Lapsed));
            Assert.That(row.callerId, Is.Not.EqualTo(closed.state.playerId), "Led by an NPC.");
            Assert.That(closed.state.ledger.calls, Is.Empty);
        }

        [Test]
        public void TheAnswerIsSaidToSomebodyWhoWasThereAboutSomebodyOnTheBlock()
        {
            var open = Opened(out var pact);
            var npcs = NpcIds(open);
            var engine = new EpisodeEngine(open);
            string before = Json(open);
            foreach (var (through, nominee, text, reason) in new[]
                     {
                         (npcs[0], npcs[1], PactId, PactPlans.ThroughRefusal),
                         (npcs[3], open.playerId, PactId, PactPlans.NomineeRefusal),
                         (npcs[3], npcs[4], PactId, PactPlans.NomineeRefusal),
                         (npcs[3], npcs[1], "alliance-nobody", EpisodeEngine.NotYourPactRefusal),
                     })
            {
                var refused = Apply(engine, EpisodeCommandKind.AnswerPactPlan, through, nominee, text);
                Assert.That(refused.accepted, Is.False, reason);
                Assert.That(refused.reason, Is.EqualTo(reason));
                Assert.That(Json(engine.Snapshot), Is.EqualTo(before), "Nothing spent, drawn or logged: " + reason);
            }
        }

        [Test]
        public void ADissenterIsNeverFlaggedAtTheReveal()
        {
            var open = Opened(out var pact, split: true);
            var npcs = NpcIds(open);
            // A split: the player goes with the fourth's say, the fifth decides whether to go along.
            string chosen = open.ledger.plans.Single().says.Single(x => x.memberId == npcs[3]).targetId;
            var agreed = Apply(new EpisodeEngine(open), EpisodeCommandKind.AnswerPactPlan, npcs[3], chosen, PactId);
            Assert.That(agreed.accepted, Is.True, agreed.reason);
            Assert.That(agreed.state.ledger.calls.Single().defected, Is.Empty, "A dissenter is written to the plan alone,");
            var revealed = Reveal(agreed.state);
            var record = revealed.relationships.Where(r => r.fromId == revealed.playerId).SelectMany(r => r.events)
                .Where(e => e.type == Allegiance.BetrayedType && e.description.Contains("ignored your call")).ToList();
            Assert.That(record, Is.Empty, "never to the call's defectors that C2's betrayal check reads.");
        }

        [Test]
        public void SavingAnOpenPlanAndAnsweringAfterTheLoadIsAnsweringWithout()
        {
            var open = Opened(out var pact);
            var npcs = NpcIds(open);
            var settings = new JsonSerializerSettings { ObjectCreationHandling = ObjectCreationHandling.Replace };
            var loaded = JsonConvert.DeserializeObject<EpisodeState>(Json(open), settings);
            Valid(loaded);
            foreach (string nominee in new[] { npcs[1], npcs[2], null })
            {
                var direct = Apply(new EpisodeEngine(open), EpisodeCommandKind.AnswerPactPlan, npcs[3], nominee, PactId);
                var reloaded = Apply(new EpisodeEngine(loaded), EpisodeCommandKind.AnswerPactPlan, npcs[3], nominee, PactId);
                Assert.That(Json(reloaded.state), Is.EqualTo(Json(direct.state)), "Answer " + (nominee ?? "low") + ": the same after a reload.");
            }
        }

        [Test]
        public void ValidationHoldsTheStancesAndTheCallLinks()
        {
            var open = Opened(out var pact);
            var npcs = NpcIds(open);
            var agreed = Apply(new EpisodeEngine(open), EpisodeCommandKind.AnswerPactPlan, npcs[3], npcs[1], PactId).state;
            var low = Apply(new EpisodeEngine(open), EpisodeCommandKind.AnswerPactPlan, npcs[3], null, PactId).state;
            Valid(agreed); Valid(low);
            var defects = new Dictionary<string, (EpisodeState from, Action<EpisodeState> change)>
            {
                ["backed-without-its-call"] = (agreed, x => x.ledger.calls.Clear()),
                ["backed-twice"] = (agreed, x => x.ledger.calls.Add(x.ledger.calls[0].Clone())),
                ["call-to-another-target"] = (agreed, x => x.ledger.calls[0].targetId = npcs[2]),
                ["call-with-another-following"] = (agreed, x => x.ledger.calls[0].followed.RemoveAt(0)),
                ["call-with-a-defector"] = (agreed, x => x.ledger.calls[0].defected.Add(npcs[4])),
                ["npc-led-with-a-call"] = (low, x => x.ledger.calls.Add(new BlocCallRow { week = x.week, allianceId = PactId, callerId = x.playerId, targetId = npcs[1] })),
                ["player-bound"] = (agreed, x => { x.ledger.plans[0].followed.Add(x.playerId); x.ledger.calls[0].followed.Add(x.playerId); }),
                ["player-says"] = (open, x => x.ledger.plans[0].says.Add(new PlanSay { memberId = x.playerId, targetId = npcs[1] })),
                ["open-with-a-target"] = (open, x => x.ledger.plans[0].targetId = npcs[1]),
                ["open-binding"] = (open, x => x.ledger.plans[0].followed.Add(npcs[3])),
                ["settled-without-a-caller"] = (low, x => x.ledger.plans[0].callerId = null),
                ["settled-without-a-target"] = (low, x => x.ledger.plans[0].targetId = ""),
            };
            foreach (var defect in defects)
            {
                var copy = defect.Value.from.Clone();
                defect.Value.change(copy);
                Assert.That(EpisodeValidation.TryValidate(copy, out string error), Is.False, defect.Key);
                Assert.That(error, Does.Contain("pact plan").IgnoreCase, defect.Key + ": " + error);
            }
            // A JsonUtility round trip writes an absent name as "", which an open plan holds as nothing.
            var blank = open.Clone();
            blank.ledger.plans[0].targetId = blank.ledger.plans[0].callerId = blank.ledger.plans[0].counterId = "";
            Valid(blank);
        }

        [Test]
        public void GameSenseCountsEveryVoterNotWithAPlanBackedCall()
        {
            var open = Opened(out var pact, split: true);
            var npcs = NpcIds(open);
            string chosen = open.ledger.plans.Single().says.Single(x => x.memberId == npcs[3]).targetId;
            var after = Apply(new EpisodeEngine(open), EpisodeCommandKind.AnswerPactPlan, npcs[3], chosen, PactId).state;
            var row = after.ledger.plans.Single();
            var note = GameSense.Evaluate(after).notes.Single(n => n.rowKind == "call");
            int against = new[] { npcs[3], npcs[4] }.Count(id => !row.followed.Contains(id));
            Assert.That(PactPlans.NotFollowing(after, row), Has.Count.EqualTo(against));
            Assert.That(note.points, Is.EqualTo(row.followed.Count * 2 - against), "Followed twice over, less every voter at it not with it.");
            // Forced, so the count is seen to move: one bound and one who is not.
            var forced = after.Clone();
            forced.ledger.plans[0].followed = new List<string> { npcs[3] };
            forced.ledger.calls[0].followed = new List<string> { npcs[3] };
            Assert.That(GameSense.Evaluate(forced).notes.Single(n => n.rowKind == "call").points, Is.EqualTo(2 - 1));
            Assert.That(GameSense.Evaluate(forced).notes.Single(n => n.rowKind == "call").text, Does.Contain("1 went with it, 1 did not"));
        }

        /// <summary>The player on the block as the members' plan: never offered going with it, and never told to evict themselves.</summary>
        [Test]
        public void ThePlayerOnTheBlockAsThePlanIsNeverToldToEvictThemselves()
        {
            var s = Campaign();
            var npcs = NpcIds(s);
            s.nominees = new List<string> { s.playerId, npcs[2] };
            var pact = Pact(s, PactId, PactName, s.playerId, npcs[3], npcs[4]);
            Warm(s, pact);
            var row = Row(s, (npcs[3], s.playerId), (npcs[4], s.playerId));
            s.ledger.plans.Add(row);
            Assert.That(PactPlans.AnswerRefusal(s, pact, npcs[3], s.playerId), Is.EqualTo(PactPlans.NomineeRefusal), "Never going with a plan to evict the player.");
            Assert.That(PactPlans.AnswerRefusal(s, pact, npcs[3], npcs[2]), Is.Null, "Pushing for the other is open,");
            Assert.That(PactPlans.AnswerKind(s, pact, row, npcs[2]), Is.EqualTo(PactPlans.Counter));
            var low = PactPlans.Settle(s, pact, row, PactPlans.LieLow, null);
            Assert.That(low.targetId, Is.EqualTo(s.playerId), "and lying low lets it stand.");
            Assert.That(PactPlans.SettledLine(s, pact, null, low, null),
                Is.EqualTo("You let " + PactName + "'s plan to evict you stand, as " + Name(s, npcs[3]) + " and " + Name(s, npcs[4]) + " wanted."));
            var settled = row.Clone();
            settled.stance = PactPlanStance.Low; settled.targetId = s.playerId; settled.callerId = low.callerId;
            Assert.That(AllianceRead.PlanText(s, settled), Is.EqualTo("Plan: " + FinalistRead.FirstName(Name(s, npcs[3])) + " and "
                + FinalistRead.FirstName(Name(s, npcs[4])) + " wanted you out. You lay low: it named you."));
        }

        // ------------------------------------------------------------ D3-S5: follow-through on the page

        [Test]
        public void ThePageSaysThePlanInPlaceOfItsCallAndFollowThroughByTheBallotsThePlayerCanPlace()
        {
            var open = Opened(out var pact);
            var npcs = NpcIds(open);
            string First(EpisodeState s, string id) => FinalistRead.FirstName(s.Find(id).name);
            var card = AllianceRead.Read(open).yours.Single(p => p.id == PactId);
            Assert.That(card.warRoom, Is.True, "A pact of three under the war rooms.");
            Assert.That(card.plans.Single().text, Is.EqualTo("Plan: " + First(open, npcs[3]) + " and " + First(open, npcs[4]) + " wanted "
                + First(open, npcs[1]) + " out. You have not answered it yet."));

            var agreed = Apply(new EpisodeEngine(open), EpisodeCommandKind.AnswerPactPlan, npcs[3], npcs[1], PactId).state;
            card = AllianceRead.Read(agreed).yours.Single(p => p.id == PactId);
            Assert.That(card.calls, Is.Empty, "The call the plan made is said by its plan,");
            Assert.That(card.plans.Single().text, Does.EndWith(" You went with it: evict " + First(agreed, npcs[1]) + "."), "as the player answered it,");
            Assert.That(card.members.Where(m => m.id == npcs[3] || m.id == npcs[4]).Select(m => m.followed), Is.EqualTo(new[] { 1, 1 }),
                "and its members' follow count still counts it.");

            // The vote read: the player votes with the plan, and the count proves the rest.
            var engine = new EpisodeEngine(agreed);
            for (int i = 0; i < 40 && !engine.Snapshot.evictionResolved; i++)
            {
                var next = EpisodeEngineTests.NextCommand(engine.Snapshot);
                if (next.kind == EpisodeCommandKind.CastVote) next.targetId = npcs[1];
                var result = engine.Apply(next);
                Assert.That(result.accepted, Is.True, result.reason);
            }
            var revealed = engine.Snapshot;
            Assert.That(revealed.evictionResolved, Is.True);
            var sheet = KnownBallots.Read(revealed, revealed.week);
            Assert.That(sheet.Revealed, Is.True);
            var with = new[] { npcs[3], npcs[4] }.Where(id => sheet.Knows(id) && sheet.TargetOf(id) == npcs[1]).Select(id => First(revealed, id)).ToList();
            var not = new[] { npcs[3], npcs[4] }.Where(id => sheet.Knows(id) && sheet.TargetOf(id) != npcs[1]).Select(id => First(revealed, id)).ToList();
            Assert.That(with.Count + not.Count, Is.GreaterThan(0), "Precondition: the player can place a ballot of the plan's.");
            string expected = with.Count > 0 && not.Count > 0 ? " " + AllianceRead.Join(with) + " voted with it; " + AllianceRead.Join(not) + " didn't."
                : with.Count > 0 ? " " + AllianceRead.Join(with) + " voted with it." : " " + AllianceRead.Join(not) + " didn't vote with it.";
            string text = AllianceRead.Read(revealed).yours.Single(p => p.id == PactId).plans.Single().text;
            Assert.That(text, Does.EndWith(expected), "Follow-through by the ballots the player can place.");
            Assert.That(text, Does.Not.Match("[0-9]"), "No number on the page.");
        }

        [Test]
        public void ThePageSaysEveryStanceInWords()
        {
            var open = Opened(out var pact);
            var npcs = NpcIds(open);
            string First(string id) => FinalistRead.FirstName(open.Find(id).name);
            string Text(EpisodeState s) => AllianceRead.Read(s).yours.Single(p => p.id == PactId).plans.Single().text;
            var low = Apply(new EpisodeEngine(open), EpisodeCommandKind.AnswerPactPlan, npcs[3], null, PactId).state;
            Assert.That(Text(low), Does.EndWith(" You lay low: evict " + First(npcs[1]) + "."));
            var lapsed = Apply(new EpisodeEngine(open), EpisodeCommandKind.Advance).state;
            Assert.That(Text(lapsed), Does.EndWith(" You let it stand: evict " + First(npcs[1]) + "."));
            var countered = Apply(new EpisodeEngine(open), EpisodeCommandKind.AnswerPactPlan, npcs[3], npcs[2], PactId).state;
            var row = countered.ledger.plans.Single();
            string came = row.cameRound.Count > 0 ? ", and " + AllianceRead.Join(row.cameRound.Select(First).ToList()) + " came round" : "";
            Assert.That(Text(countered), Does.EndWith(" You pushed for " + First(npcs[2]) + came + "."
                + (row.targetId == npcs[2] ? " It carried." : " The pact held to " + First(npcs[1]) + ".")));
            var ended = open.Clone(); ended.alliances.Single(a => a.id == PactId).active = false;
            var voided = Apply(new EpisodeEngine(ended), EpisodeCommandKind.Advance).state;
            Assert.That(Text(voided), Does.EndWith(" It came to nothing."));
            foreach (var s in new[] { open, low, lapsed, countered, voided }) Assert.That(Text(s), Does.Not.Match("[0-9]"));
            var off = open.Clone(); off.pactPlanRulesStartWeek = 0; off.ledger.plans.Clear();
            var none = AllianceRead.Read(off).yours.Single(p => p.id == PactId);
            Assert.That(none.plans, Is.Empty);
            Assert.That(none.warRoom, Is.False, "Without the war rooms the pact calls as a pair does.");
        }

        // ------------------------------------------------------------ fixtures

        private static List<string> NpcIds(EpisodeState s) => s.Active.Where(c => !c.isPlayer).Select(c => c.id).ToList();

        private static string Json(EpisodeState s) => JsonConvert.SerializeObject(s);

        private static string Name(EpisodeState s, string id) => s.Find(id).name;

        internal static EpisodeCommand Command(EpisodeState s, EpisodeCommandKind kind, string target = null, string second = null, string text = null) =>
            new EpisodeCommand
            {
                id = "plan-" + kind + "-" + s.revision + "-" + target + "-" + second, actorId = s.playerId, kind = kind,
                targetId = target, secondTargetId = second, text = text, expectedRevision = s.revision, expectedPhase = s.phase,
            };

        internal static CommandResult Apply(EpisodeEngine engine, EpisodeCommandKind kind, string target = null, string second = null, string text = null) =>
            engine.Apply(Command(engine.Snapshot, kind, target, second, text));

        /// <summary>The bloc's pressure in a voter's ballot as it stands, on a nominee: −40 where the call's directive reaches them.</summary>
        private static double BlocPressure(EpisodeState s, string voterId, string nomineeId) =>
            EpisodeEngine.ProjectBallot(s, voterId).nomineeEvaluations.Single(n => n.nomineeId == nomineeId).factors.Single(f => f.code == "blocPressure").value;

        /// <summary>Free time in the catalogue's six-house, the story, the commitment rules and the levers on, the player's pact of three warm.</summary>
        internal static EpisodeState FreeTime(bool rules, out AllianceState pact)
        {
            var s = ContentCatalog.Create(7);
            EpisodeEngine.EnableStory(s);
            EpisodeEngine.EnableCommitments(s);
            EpisodeEngine.EnableLevers(s);
            if (rules) EpisodeEngine.EnablePactPlans(s);
            var npcs = NpcIds(s);
            pact = Pact(s, PactId, PactName, s.playerId, npcs[3], npcs[4]);
            Warm(s, pact);
            Valid(s);
            return s;
        }

        /// <summary>
        /// The war room's campaign with both voters' minds made up: each wants the first nominee out, by
        /// how they see the two on the block - or, <paramref name="split"/>, the fifth wants the second out.
        /// </summary>
        internal static EpisodeState Decided(out AllianceState pact, bool split = false)
        {
            var s = WarRoom(out pact);
            var npcs = NpcIds(s);
            foreach (string voter in new[] { npcs[3], npcs[4] })
            {
                bool other = split && voter == npcs[4];
                SetScore(s, voter, other ? npcs[2] : npcs[1], -60);
                SetScore(s, voter, other ? npcs[1] : npcs[2], 40);
            }
            var says = PactPlans.Says(s, pact);
            Assert.That(says.Select(x => (x.memberId, x.targetId)),
                Is.EqualTo(new[] { (npcs[3], npcs[1]), (npcs[4], split ? npcs[2] : npcs[1]) }), "Precondition: the voters' minds as made up.");
            Valid(s);
            return s;
        }

        /// <summary>The war room held through the fourth houseguest: its plan open.</summary>
        internal static EpisodeState Opened(out AllianceState pact, bool split = false)
        {
            var s = Decided(out pact, split);
            var result = Apply(new EpisodeEngine(s), EpisodeCommandKind.AllianceMeet, NpcIds(s)[3], text: PactId);
            Assert.That(result.accepted, Is.True, result.reason);
            Assert.That(PactPlans.OpenPlan(result.state, PactId), Is.Not.Null, "Precondition: the plan is open.");
            pact = result.state.alliances.Single(a => a.id == PactId);
            return result.state;
        }

        /// <summary>The same state with no plan on the ledger: what the step would draw without one.</summary>
        private static EpisodeState PlanFree(EpisodeState s)
        {
            var copy = s.Clone();
            copy.ledger.plans.Clear();
            return copy;
        }

        /// <summary>The house plays to the reveal.</summary>
        private static EpisodeState Reveal(EpisodeState before)
        {
            var engine = new EpisodeEngine(before);
            for (int i = 0; i < 40 && !engine.Snapshot.evictionResolved; i++)
            {
                var result = engine.Apply(EpisodeEngineTests.NextCommand(engine.Snapshot));
                Assert.That(result.accepted, Is.True, result.reason);
            }
            Assert.That(engine.Snapshot.evictionResolved, Is.True, "The eviction resolved.");
            return engine.Snapshot;
        }

        /// <summary>
        /// The campaign of the catalogue's six-house: a houseguest at the head of the house, the next two
        /// on the block, the fourth holding the veto; the player, the fourth and the fifth vote. The story's
        /// staging rules, the commitment rules and the levers are on, and the war rooms when <paramref name="rules"/>.
        /// </summary>
        internal static EpisodeState Campaign(bool rules = true, uint seed = 7)
        {
            var s = ContentCatalog.Create(seed);
            s.strategyRulesStartWeek = 1;
            var npcs = NpcIds(s);
            s.phase = EpisodePhase.Campaign;
            s.hohId = npcs[0];
            s.nominees = new List<string> { npcs[1], npcs[2] };
            s.vetoHolderId = npcs[3];
            s.vetoPlayers = s.Active.Select(c => c.id).Take(EpisodeEngine.VetoPlayerCount(s.Active.Count())).ToList();
            if (!s.vetoPlayers.Contains(s.vetoHolderId)) s.vetoPlayers[s.vetoPlayers.Count - 1] = s.vetoHolderId;
            s.vetoResolved = true;
            EpisodeEngine.EnableStory(s);
            EpisodeEngine.EnableCommitments(s);
            EpisodeEngine.EnableLevers(s);
            if (rules) EpisodeEngine.EnablePactPlans(s);
            Valid(s);
            return s;
        }

        /// <summary>
        /// The campaign with the player's pact of the two voters (the fourth and fifth houseguests), and with
        /// <paramref name="extra"/> the first nominee too - everybody in it at 30 with everybody else in it.
        /// </summary>
        internal static EpisodeState WarRoom(out AllianceState pact, int extra = 0, bool rules = true, uint seed = 7)
        {
            var s = Campaign(rules, seed);
            var npcs = NpcIds(s);
            var members = new List<string> { s.playerId, npcs[3], npcs[4] };
            if (extra > 0) members.Add(npcs[1]);
            pact = Pact(s, PactId, PactName, members.ToArray());
            Warm(s, pact);
            Valid(s);
            return s;
        }

        internal static void Warm(EpisodeState s, AllianceState pact)
        {
            foreach (var id in pact.members)
                foreach (var to in pact.members.Where(t => t != id)) SetScore(s, id, to, 30);
        }

        /// <summary>An open plan of the war room's pact this week with these says, everybody in it present.</summary>
        internal static PactPlanRow Row(EpisodeState s, params (string member, string target)[] says)
        {
            var pact = s.alliances.Single(a => a.id == PactId);
            return new PactPlanRow
            {
                week = s.week, allianceId = PactId, throughId = pact.members.First(id => id != s.playerId), stance = PactPlanStance.Open,
                present = new List<string> { s.playerId }.Concat(EpisodeEngine.AtTheMeeting(s, pact)).ToList(),
                says = says.Select(x => new PlanSay { memberId = x.member, targetId = x.target }).ToList(),
            };
        }

        internal static void Valid(EpisodeState s) => Assert.That(EpisodeValidation.TryValidate(s, out var error), Is.True, error);

        internal static void SetScore(EpisodeState s, string from, string to, double score)
        {
            var edge = s.relationships.FirstOrDefault(r => r.fromId == from && r.toId == to);
            if (edge == null) s.relationships.Add(edge = new RelationshipState { fromId = from, toId = to });
            edge.score = score;
        }

        /// <summary>A pact, with the ledger row every pact a season makes has: it began this week, the player's.</summary>
        internal static AllianceState Pact(EpisodeState s, string id, string name, params string[] members)
        {
            var pact = new AllianceState { id = id, name = name, members = members.ToList(), active = true };
            s.alliances.Add(pact);
            s.ledger.alliances.Add(new AllianceRow { id = id, startedWeek = s.week, why = "player" });
            return pact;
        }

        /// <summary>A betrayal the player can know of: the member nominated them, on the player's record.</summary>
        private static void KnownBetrayal(EpisodeState s, string npcId) =>
            RelationshipLedger.RecordOneWay(s, s.playerId, npcId, Allegiance.BetrayedType, Allegiance.BetrayalImpact,
                Allegiance.Record(s.Find(npcId).name, "nominated you", s.alliances.Where(a => a.active && a.members.Contains(npcId) && a.members.Contains(s.playerId)).Select(a => a.name)));

        /// <summary>A betrayal only a ballot the player cannot place would tell: their vote to evict the player, this week.</summary>
        private static void HiddenBetrayal(EpisodeState s, string npcId) =>
            RelationshipLedger.RecordOneWay(s, s.playerId, npcId, Allegiance.BetrayedType, Allegiance.BetrayalImpact,
                Allegiance.Record(s.Find(npcId).name, Allegiance.VotedAgainst, s.alliances.Where(a => a.active && a.members.Contains(npcId) && a.members.Contains(s.playerId)).Select(a => a.name)));
    }
}
