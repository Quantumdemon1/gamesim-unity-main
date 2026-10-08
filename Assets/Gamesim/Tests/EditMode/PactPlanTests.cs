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
        private const string PactId = "alliance-war-room", PactName = "The War Room";

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

        [TestCase(0, "", 0)] [TestCase(-20, "", 0)] [TestCase(25, "", 4)] [TestCase(50, "", 8)] [TestCase(90, "", 8)]
        [TestCase(50, "Loyal", 12)] [TestCase(100, "Loyal", 12)] [TestCase(25, "Loyal", 6)] [TestCase(100, "Sneaky", 0)]
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
                // A Loyal member who thinks the world of the player: the counter's furthest reach, twelve.
                s.Find(member).traits = new List<string> { "Loyal" };
                SetScore(s, member, s.playerId, 100);
                double odds = PactPlans.ComeRoundOdds(s, member);
                var known = Allegiance.AsThePlayerKnows(s);
                double margin = WebEvictionVoting.EvaluateNative(known, member).margin;
                Assert.That(odds, Is.EqualTo(PactPlans.ComeRoundOdds(PactPlans.CounterReach(100, s.Find(member).traits), margin)).Within(1e-12));
                Assert.That(odds, Is.EqualTo(Math.Max(0, Math.Min(1, (12 - margin) / 12))).Within(1e-12), "Seed " + seed + ": reach twelve, margin " + margin + ".");
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

        // ------------------------------------------------------------ fixtures

        private static List<string> NpcIds(EpisodeState s) => s.Active.Where(c => !c.isPlayer).Select(c => c.id).ToList();

        private static string Json(EpisodeState s) => JsonConvert.SerializeObject(s);

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
