using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The levers, second half (STRATEGY-LOOP-PLAN.md §3): a plea for your vote from the block that
    /// the voter's ballot answers, an all-in deal that binds their vote, calling the vote in an
    /// alliance so the bloc's pressure lands where you point it, keeping a third person off the
    /// block, and a call-out that reaches whoever is there.
    /// </summary>
    public sealed class VotePleaAndCallTests
    {
        private static EpisodeEngine Reach(uint seed, Func<EpisodeState, bool> until, int limit = 600)
        {
            var engine = TryReach(seed, until, limit);
            Assert.That(engine, Is.Not.Null, "The season never reached the state the test needs.");
            return engine;
        }

        /// <summary>The season walked until the state holds, or null when it never does within the limit.</summary>
        private static EpisodeEngine TryReach(uint seed, Func<EpisodeState, bool> until, int limit = 600)
        {
            var engine = new EpisodeEngine(ContentCatalog.Create(seed));
            for (int i = 0; i < limit && !until(engine.Snapshot); i++)
                if (!engine.Apply(EpisodeEngineTests.NextCommand(engine.Snapshot)).accepted) return null;
            return until(engine.Snapshot) ? engine : null;
        }

        private static bool Campaign(EpisodeState x) => x.phase == EpisodePhase.Campaign && x.nominees.Count == 2 && x.hohId != x.playerId;

        /// <summary>The first seed from <paramref name="seed"/> whose season reaches a campaign the test wants, with the levers and the windows on and the week's conversations unspent.</summary>
        private static EpisodeState Levered(uint seed, Func<EpisodeState, bool> wanted)
        {
            for (uint candidate = seed; candidate < seed + 60; candidate++)
            {
                var engine = TryReach(candidate, x => Campaign(x) && wanted(x), 900);
                if (engine == null) continue;
                var s = engine.Snapshot;
                EpisodeEngine.EnableLevers(s);
                s.strategyRulesStartWeek = 1;
                s.socialActions = 0; s.outOfPhaseSocialActions = 0;
                return s;
            }
            Assert.Fail("No seed from " + seed + " reached the campaign the test wants.");
            return null;
        }

        /// <summary>A campaign with the player off the block.</summary>
        private static EpisodeState Levered(uint seed) => Levered(seed, x => !x.nominees.Contains(x.playerId));

        /// <summary>A campaign with the player on the block, as the season put them there.</summary>
        private static EpisodeState OnTheBlock(uint seed) => Levered(seed, x => x.nominees.Contains(x.playerId));

        private static EpisodeCommand Command(EpisodeState s, EpisodeCommandKind kind, string targetId, string secondTargetId = null, string text = null) =>
            new EpisodeCommand
            {
                id = "plea-" + kind + "-" + s.revision + "-" + Guid.NewGuid().ToString("N"), actorId = s.playerId, kind = kind,
                targetId = targetId, secondTargetId = secondTargetId, text = text, expectedRevision = s.revision, expectedPhase = s.phase,
            };

        private static ContestantState[] NpcVoters(EpisodeState s) => EpisodeEngine.Voters(s).Where(v => !v.isPlayer).ToArray();

        private static void SetScore(EpisodeState s, string from, string to, double score)
        {
            var row = s.relationships.FirstOrDefault(r => r.fromId == from && r.toId == to);
            if (row == null) s.relationships.Add(row = new RelationshipState { fromId = from, toId = to });
            row.score = score;
        }

        private static LobbyState Plea(EpisodeState s, string voterId, string response, double influence) => new LobbyState
        {
            week = s.week, phase = s.phase, deciderId = voterId, ask = LobbyAsk.Vote, subjectId = s.playerId,
            approach = LobbyApproach.Emotional, response = response, influence = influence,
        };

        [Test]
        public void APleaForYourVoteIsThereWhenYouAreOnTheBlock()
        {
            var s = OnTheBlock(31);
            var voter = NpcVoters(s).First();
            Assert.That(StrategyRules.CanBeAskedForTheirVote(s, voter.id), Is.True);
            Assert.That(StrategyRules.CanLobby(s, voter.id, LobbyAsk.Vote, s.playerId, out _), Is.True, "From the block, a voter can be asked for their vote.");
            Assert.That(StrategyRules.Pleas(s, voter.id), Does.Contain((LobbyAsk.Vote, s.playerId)));
            Assert.That(StrategyRules.CanLobby(s, voter.id, LobbyAsk.Vote, s.nominees.First(id => id != s.playerId), out _), Is.False, "and only for yourself.");
            Assert.That(StrategyRules.CanLobby(s, s.hohId, LobbyAsk.Vote, s.playerId, out _), Is.False, "The Head of Household does not vote.");
            Assert.That(StrategyRules.Deciders(s), Is.Empty, "A voter is not a window's decider: nothing else changes about the campaign.");
            var off = Levered(31);
            Assert.That(StrategyRules.CanLobby(off, NpcVoters(off).First().id, LobbyAsk.Vote, off.playerId, out _), Is.False, "Off the block there is nothing to plead for.");
            s.leverRulesStartWeek = 0;
            Assert.That(StrategyRules.CanLobby(s, voter.id, LobbyAsk.Vote, s.playerId, out _), Is.False, "Before the levers the plea does not exist.");
        }

        [Test]
        public void APleaHeardEntersTheBallotAsATermYouKnow()
        {
            var s = OnTheBlock(31);
            var voter = NpcVoters(s).First();
            Assert.That(WebEvictionVoting.EvaluateNative(s, voter.id).nomineeEvaluations.SelectMany(n => n.factors).Any(f => f.code == "plea"), Is.False);
            s.lobbies.Add(Plea(s, voter.id, LobbyResponse.Receptive, 50));
            var heard = WebEvictionVoting.EvaluateNative(s, voter.id);
            var factor = heard.nomineeEvaluations.Single(n => n.nomineeId == s.playerId).factors.Single(f => f.code == "plea");
            Assert.That(factor.value, Is.EqualTo(10).Within(1e-9), "A receptive hearing of fifty is worth ten toward keeping you.");
            Assert.That(factor.visibility, Is.EqualTo("playerKnown"));
            Assert.That(VoteRead.FactorKnown(s, voter.id, s.playerId, factor), Is.True, "Your own plea is a term you know.");
            Assert.That(heard.nomineeEvaluations.Single(n => n.nomineeId != s.playerId).factors.Any(f => f.code == "plea"), Is.False);
            s.lobbies.Clear(); s.lobbies.Add(Plea(s, voter.id, LobbyResponse.Hostile, -40));
            var hostile = WebEvictionVoting.EvaluateNative(s, voter.id).nomineeEvaluations.Single(n => n.nomineeId == s.playerId).factors.Single(f => f.code == "plea");
            Assert.That(hostile.value, Is.EqualTo(-8).Within(1e-9), "A hostile hearing counts against you: a plea can backfire.");
        }

        [Test]
        public void AnAllInDealForYourVoteBindsThemAndTheLeverSaysSo()
        {
            for (uint seed = 31; seed < 91; seed += 7)
            {
                var s = OnTheBlock(seed);
                var voter = NpcVoters(s).First();
                SetScore(s, voter.id, s.playerId, 60);
                var engine = new EpisodeEngine(s);
                var result = engine.Apply(Command(s, EpisodeCommandKind.Lobby, voter.id, s.playerId, LobbyAsk.Encode(LobbyAsk.Vote, LobbyApproach.Deal)));
                Assert.That(result.accepted, Is.True, result.reason);
                var after = engine.Snapshot;
                var plea = after.lobbies.Single(l => l.deciderId == voter.id && l.ask == LobbyAsk.Vote);
                Assert.That(after.events.Any(e => e.kind == "lever" && e.text.StartsWith(voter.name + ": ") && e.text.Contains("your plea")), Is.True,
                    "The plea says what it moved.");
                if (!StrategyRules.Landed(plea.response)) continue;   // the roll's business: try the next seed
                var deal = after.deals.Single(d => d.type == DealKind.VoteSave && d.proposerId == voter.id && d.recipientId == after.playerId);
                Assert.That(deal.targetId, Is.EqualTo(after.playerId), "An all-in deal that lands is their vote to keep you, judged at the reveal.");
                Assert.That(EpisodeEngine.Obligations(after, voter.id).Single().nomineeId, Is.EqualTo(after.playerId), "and their obligation in the ballot.");
                return;
            }
            Assert.Fail("No seed had an all-in plea land.");
        }

        [Test]
        public void CallingTheVoteDecidesWhoFollowsAndTheRoundHonoursIt()
        {
            var s = Levered(31, x => !x.nominees.Contains(x.playerId) && EpisodeEngine.Voters(x).Count(v => !v.isPlayer) >= 2);
            s.blocRulesStartWeek = 1;
            var voters = NpcVoters(s);
            var loyal = voters[0]; var loose = voters[1];
            s.alliances.RemoveAll(a => a.members.Contains(loyal.id) || a.members.Contains(loose.id));
            var pact = new AllianceState { id = "alliance-test", name = "The Test Pact", members = new List<string> { s.playerId, loyal.id, loose.id }, active = true };
            s.alliances.Add(pact);
            string target = s.nominees[0];
            // Loyal trusts the player (proxy 90 x .4 = 36, plus stability); loose does not (proxy 20 x .4 = 8).
            SetScore(s, loyal.id, s.playerId, 80); SetScore(s, loyal.id, target, 0);
            SetScore(s, loose.id, s.playerId, -60); SetScore(s, loose.id, target, 0);
            foreach (var id in pact.members) foreach (var other in pact.members.Where(o => o != id)) if (!(id == loose.id && other == s.playerId) && !(id == loyal.id && other == s.playerId)) SetScore(s, id, other, 20);
            var engine = new EpisodeEngine(s);
            var result = engine.Apply(Command(s, EpisodeCommandKind.CallTheVote, loyal.id, target, pact.id));
            Assert.That(result.accepted, Is.True, result.reason);
            var after = engine.Snapshot;
            var call = after.ledger.calls.Single();
            Assert.That(call.allianceId, Is.EqualTo(pact.id)); Assert.That(call.targetId, Is.EqualTo(target)); Assert.That(call.callerId, Is.EqualTo(after.playerId));
            Assert.That(call.followed, Is.EqualTo(new[] { loyal.id }), "Whoever trusts you follows.");
            Assert.That(call.defected, Is.EqualTo(new[] { loose.id }), "Whoever does not, does not, and you learn who.");
            var line = after.events.Last(e => e.kind == "lever");
            Assert.That(line.text, Does.Contain("You called it in The Test Pact: evict " + after.Find(target).name).And.Contain(loyal.name + " is with you").And.Contain(loose.name + " isn't"));
            Assert.That(EpisodeEngine.SocialActionsSpent(after), Is.EqualTo(EpisodeEngine.SocialActionsSpent(s) + 1), "A call costs a conversation.");

            var round = WebNativeEvictionRound.Evaluate(after, new[] { loyal.id, loose.id });
            var followed = round.evaluations.Single(e => e.voterId == loyal.id).nomineeEvaluations.Single(n => n.nomineeId == target).factors.Single(f => f.code == "blocPressure");
            Assert.That(followed.value, Is.EqualTo(-40), "The bloc's pressure lands where you pointed it, on whoever followed.");
            var defected = round.evaluations.Single(e => e.voterId == loose.id).nomineeEvaluations.SelectMany(n => n.factors).Where(f => f.code == "blocPressure");
            Assert.That(defected.Select(f => f.value), Has.All.EqualTo(0), "and nowhere on whoever did not.");
            Assert.That(round.coordination.results.Single(r => r.allianceId == pact.id).shotCallerId, Is.EqualTo(after.playerId), "The call is yours.");
            Assert.That(engine.Apply(Command(after, EpisodeCommandKind.CallTheVote, loyal.id, target, pact.id)).accepted, Is.False, "One call per alliance a week.");
        }

        [Test]
        public void KeepingSomebodyElseOffTheBlockIsAPleaUnderTheLevers()
        {
            var s = Reach(31, x => x.phase == EpisodePhase.Nomination && x.nominees.Count == 0 && x.hohId != null && x.hohId != x.playerId).Snapshot;
            s.strategyRulesStartWeek = 1;
            var friend = EpisodeEngine.NominationCandidates(s).First(c => !c.isPlayer);
            Assert.That(StrategyRules.CanLobby(s, s.hohId, LobbyAsk.Spare, friend.id, out _), Is.False, "Before the levers, only your own name could be kept off.");
            EpisodeEngine.EnableLevers(s);
            Assert.That(StrategyRules.CanLobby(s, s.hohId, LobbyAsk.Spare, friend.id, out var why), Is.True, why);
            Assert.That(StrategyRules.CanLobby(s, s.hohId, LobbyAsk.Spare, s.playerId, out _), Is.True, "and your own still can be.");
            Assert.That(StrategyRules.CanLobby(s, s.hohId, LobbyAsk.Spare, s.hohId, out _), Is.False, "Not the Head of Household themselves.");
            Assert.That(StrategyRules.Describe(s, LobbyAsk.Spare, friend.id), Is.EqualTo("to keep " + friend.name + " off the block"));
            Assert.That(StrategyRules.Pleas(s, s.hohId), Does.Contain((LobbyAsk.Spare, friend.id)));
        }

        [Test]
        public void ACallOutReachesWhoeverIsThereUnderTheLevers()
        {
            var list = new List<string> { "a", "b", "c", "d" };
            var rolls = new Queue<double>(new[] { 0.99, 0.0, 0.5 });
            EpisodeEngine.Shuffle(list, () => rolls.Dequeue());
            Assert.That(list, Is.EqualTo(new[] { "c", "b", "a", "d" }), "A seeded shuffle, the same for the same rolls.");
            Assert.That(rolls, Is.Empty, "One roll per position but the first.");
        }
    }
}
