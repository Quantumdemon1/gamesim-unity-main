using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using Newtonsoft.Json;
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

        private static string Json(object value) => JsonConvert.SerializeObject(value);

        private static string SourceResponse(double chance, double roll) => roll * 100 < chance * .6
            ? LobbyResponse.Receptive : roll * 100 < chance ? LobbyResponse.Open
            : roll * 100 < chance + 20 ? LobbyResponse.Skeptical : LobbyResponse.Hostile;

        // This deliberately isolates the legacy leaf, not the later paid dialogue/NPC cadence.
        // The campaign is played; window rules expose its real debit, while all-week stays off.
        private static EpisodeState EmotionalCampaign(string response, out string voterId)
        {
            var s = OnTheBlock(31);
            EpisodeEngine.EnableWeek(s);
            Assert.That(s.unifiedCommitmentRulesVersion, Is.Zero, "An isolated legacy-leaf cohort, not mode2 qualification.");
            Assert.That(s.allWeekRulesStartWeek, Is.Zero);
            Assert.That(EpisodeEngine.AllWeekOn(s), Is.False);
            var voter = NpcVoters(s).First(); voterId = voter.id;
            voter.traits.Clear();
            SetScore(s, s.playerId, voter.id, response == LobbyResponse.Hostile ? -99 : 99);
            SetScore(s, voter.id, s.playerId, response == LobbyResponse.Hostile ? -99 : response == LobbyResponse.Skeptical ? 0 : 99);
            Assert.That(StrategyRules.CanLobby(s, voter.id, LobbyAsk.Vote, s.playerId, out var reason), Is.True, reason);
            Assert.That(EpisodeEngine.SocialActionsSpent(s), Is.LessThan(EpisodeEngine.SocialActionBudget(s)));
            double chance = StrategyRules.Chance(s, voter.id, LobbyAsk.Vote, s.playerId, LobbyApproach.Emotional);
            uint seed = 1;
            for (; seed <= 4096; seed++)
                if (SourceResponse(chance, new SeededRandom(seed).NextDouble()) == response) break;
            Assert.That(seed, Is.LessThanOrEqualTo(4096u), "Select the input roll, never retry a command until it passes.");
            s.randomState = seed;
            Assert.That(EpisodeValidation.TryValidate(s, out reason), Is.True, reason);
            return s;
        }

        [TestCase(LobbyResponse.Receptive, 4)]
        [TestCase(LobbyResponse.Open, 3)]
        [TestCase(LobbyResponse.Skeptical, 1)]
        [TestCase(LobbyResponse.Hostile, 4)]
        public void EmotionalVotePleaKeepsTheSourceLeafAndOneWindowDebit(string response, int draws)
        {
            var s = EmotionalCampaign(response, out string voterId);
            var voter = s.Find(voterId);
            var command = Command(s, EpisodeCommandKind.Lobby, voterId, s.playerId, LobbyAsk.Encode(LobbyAsk.Vote, LobbyApproach.Emotional));
            string original = Json(s), request = Json(command);
            var rng = new SeededRandom(s.randomState);
            double chance = StrategyRules.Chance(s, voterId, LobbyAsk.Vote, s.playerId, LobbyApproach.Emotional);
            Assert.That(SourceResponse(chance, rng.NextDouble()), Is.EqualTo(response));
            double influence = response == LobbyResponse.Receptive ? 40 + 20 * rng.NextDouble()
                : response == LobbyResponse.Open ? 15 : response == LobbyResponse.Skeptical ? -5 : -(30 + 20 * rng.NextDouble());
            double impact = response == LobbyResponse.Receptive ? 5 : response == LobbyResponse.Open ? 3
                : response == LobbyResponse.Skeptical ? 0 : -8;
            double adjusted = WebRules.RelationshipDelta(impact, s.Find(s.playerId).stats.social, true);
            double reciprocalEvent = impact == 0 ? 0 : WebRules.ReciprocalDelta(impact, rng.NextDouble());
            double reciprocalScore = impact == 0 ? 0 : WebRules.ReciprocalDelta(impact, rng.NextDouble());
            var counted = new SeededRandom(s.randomState);
            for (int i = 0; i < draws; i++) counted.NextDouble();
            Assert.That(rng.State, Is.EqualTo(counted.State), "One response draw, conditional size, and two distinct Change draws.");
            string note = "Lobbying: asked to vote to keep you (" + response + ")";
            var expectedArcs = impact == 0 ? s.relationshipArcs
                : WebRelationshipArcs.Update(s.relationshipArcs, voterId, voter.name, adjusted, note, s.week).arcs;

            var engine = new EpisodeEngine(s);
            var result = engine.Apply(command);
            Assert.That(result.accepted, Is.True, result.reason);
            var after = engine.Snapshot;
            var plea = after.lobbies.Last();
            Assert.That(after.lobbies.Count, Is.EqualTo(s.lobbies.Count + 1));
            Assert.That((plea.week, plea.phase, plea.deciderId, plea.ask, plea.subjectId, plea.approach, plea.response),
                Is.EqualTo((s.week, s.phase, voterId, LobbyAsk.Vote, s.playerId, LobbyApproach.Emotional, response)));
            Assert.That(plea.influence, Is.EqualTo(Math.Max(-StrategyRules.MostInfluence, Math.Min(StrategyRules.MostInfluence, influence))));
            Assert.That(after.randomState, Is.EqualTo(counted.State));
            Assert.That(Json(after.npcSocial), Is.EqualTo(Json(s.npcSocial)), "No all-week/NPC-stream coverage is claimed here.");
            Assert.That(after.Score(s.playerId, voterId), Is.EqualTo(WebRules.ClampScore(s.Score(s.playerId, voterId) + adjusted)));
            Assert.That(after.Score(voterId, s.playerId), Is.EqualTo(WebRules.ClampScore(s.Score(voterId, s.playerId) + reciprocalScore)));
            foreach (bool reverse in new[] { false, true })
            {
                string from = reverse ? voterId : s.playerId, to = reverse ? s.playerId : voterId;
                var beforeRow = s.relationships.Single(r => r.fromId == from && r.toId == to);
                var afterRow = after.relationships.Single(r => r.fromId == from && r.toId == to);
                Assert.That(afterRow.events.Count, Is.EqualTo(beforeRow.events.Count + (impact == 0 ? 0 : 1)));
                if (impact == 0) { Assert.That(Json(afterRow), Is.EqualTo(Json(beforeRow))); continue; }
                var receipt = afterRow.events.Last();
                Assert.That((receipt.sequence, receipt.week, receipt.type, receipt.description, receipt.decayable),
                    Is.EqualTo((s.nextSequence + (reverse ? 1 : 0), s.week, "lobby", note, RelationshipLedger.Decays("lobby"))));
                Assert.That(receipt.impactScore, Is.EqualTo(reverse ? reciprocalEvent : adjusted));
                Assert.That(afterRow.lastInteractionWeek, Is.EqualTo(s.week));
                Assert.That(afterRow.notes.Last(), Is.EqualTo(note));
            }
            Assert.That(Json(after.relationshipArcs), Is.EqualTo(Json(expectedArcs)));
            var memory = after.memories.Last(m => m.ownerId == voterId && m.subjectId == s.playerId);
            Assert.That((memory.week, memory.isPrivate, memory.text),
                Is.EqualTo((s.week, true, "Asked me to vote to keep you in week " + s.week + ". I was " + response + ".")));
            var lines = after.events.Where(e => e.sequence >= s.nextSequence).ToArray();
            Assert.That(lines.Select(e => e.kind), Is.EqualTo(new[] { "lobby", "lever" }));
            Assert.That(lines[0].text, Is.EqualTo("You asked " + voter.name + " to vote to keep you: “"
                + StrategyRules.Pitch(s, LobbyAsk.Vote, s.playerId, LobbyApproach.Emotional) + "” " + voter.name + ": “"
                + StrategyRules.Answer(s, LobbyAsk.Vote, s.playerId, LobbyApproach.Emotional, response) + "”"));
            Assert.That(lines[1].text, Does.StartWith(voter.name + ": ").And.Contain("your plea"));
            foreach (var line in lines) Assert.That(line.audienceIds, Is.EquivalentTo(new[] { s.playerId, voterId }));
            Assert.That(after.nextSequence - s.nextSequence, Is.EqualTo(impact == 0 ? 2 : 4));
            Assert.That(Json(after.deals), Is.EqualTo(Json(s.deals)));
            Assert.That(Json(after.promises), Is.EqualTo(Json(s.promises)));
            Assert.That(Json(after.unifiedCommitments), Is.EqualTo(Json(s.unifiedCommitments)));
            Assert.That(after.outOfPhaseSocialActions, Is.EqualTo(s.outOfPhaseSocialActions + 1));
            Assert.That(after.socialActions, Is.EqualTo(s.socialActions));
            Assert.That(EpisodeEngine.SocialActionsSpent(after), Is.EqualTo(EpisodeEngine.SocialActionsSpent(s) + 1));
            for (int window = 0; window < Windows.Count; window++)
                Assert.That(after.windowActions[window], Is.EqualTo(s.windowActions[window] + (window == Windows.AfterVeto ? 1 : 0)));
            Assert.That(Json(s), Is.EqualTo(original));
            Assert.That(Json(command), Is.EqualTo(request));
        }

        [Test]
        public void EmotionalVotePleaDuplicateCannotSpendOrResolveAgain()
        {
            var s = EmotionalCampaign(LobbyResponse.Open, out string voterId);
            var command = Command(s, EpisodeCommandKind.Lobby, voterId, s.playerId, LobbyAsk.Encode(LobbyAsk.Vote, LobbyApproach.Emotional));
            string original = Json(s), request = Json(command);
            var engine = new EpisodeEngine(s);
            var first = engine.Apply(command);
            Assert.That(first.accepted, Is.True, first.reason);
            string committed = Json(engine.Snapshot);
            var duplicate = engine.Apply(command); // Its revision is now stale; committed ID wins first.
            Assert.That(duplicate.duplicate, Is.True);
            Assert.That(duplicate.accepted, Is.False);
            Assert.That(Json(duplicate.state), Is.EqualTo(committed));
            Assert.That(Json(engine.Snapshot), Is.EqualTo(committed));
            Assert.That(Json(s), Is.EqualTo(original));
            Assert.That(Json(command), Is.EqualTo(request));
        }

        [TestCase("ap0")]
        [TestCase("already-asked")]
        [TestCase("stale")]
        public void EmotionalVotePleaRefusalPreservesTheWholeStateAndRequest(string refusal)
        {
            var s = EmotionalCampaign(LobbyResponse.Open, out string voterId);
            if (refusal == "ap0")
            {
                int spent = EpisodeEngine.SocialActionBudget(s);
                s.windowActions[Windows.AfterVeto] = spent; s.outOfPhaseSocialActions = spent;
            }
            var engine = new EpisodeEngine(s);
            if (refusal == "already-asked")
            {
                var first = engine.Apply(Command(s, EpisodeCommandKind.Lobby, voterId, s.playerId, LobbyAsk.Encode(LobbyAsk.Vote, LobbyApproach.Emotional)));
                Assert.That(first.accepted, Is.True, first.reason);
            }
            var current = engine.Snapshot;
            var command = Command(current, EpisodeCommandKind.Lobby, voterId, current.playerId, LobbyAsk.Encode(LobbyAsk.Vote, LobbyApproach.Emotional));
            if (refusal == "stale") command.expectedRevision--;
            string unchanged = Json(current), request = Json(command), original = Json(s);
            var result = engine.Apply(command);
            Assert.That(result.accepted || result.duplicate, Is.False);
            Assert.That(result.reason, Does.Contain(refusal == "ap0" ? "no conversations left"
                : refusal == "already-asked" ? "already heard you out" : "episode changed"));
            Assert.That(Json(result.state), Is.EqualTo(unchanged));
            Assert.That(Json(engine.Snapshot), Is.EqualTo(unchanged));
            Assert.That(Json(s), Is.EqualTo(original));
            Assert.That(Json(command), Is.EqualTo(request));
        }

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
