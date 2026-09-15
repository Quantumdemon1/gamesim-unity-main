using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    public sealed class EpisodeNpcSocialTests
    {
        private static string Json(object value) => JsonConvert.SerializeObject(value);
        private static NpcOperationRequest Request(EpisodeState s, NpcOperationKind kind) => new NpcOperationRequest
        {
            kind = kind, sessionId = s.sessionId, expectedRevision = s.revision, expectedPhase = s.phase,
            expectedClockTick = s.npcSocial.clockTick, targetClockTick = s.npcSocial.clockTick + (kind == NpcOperationKind.Tick ? 1 : 0), freeRoamReady = true
        };
        private static NpcWorldReadyEvidence Evidence(NpcConversationState row, long clock) => new NpcWorldReadyEvidence
        { sequence = row.sequence, firstId = row.firstId, secondId = row.secondId, rendezvousId = row.rendezvousId, observedClockTick = clock };
        private static NpcOperationRequest Start(EpisodeState s, int pair = 0)
        {
            var npcs = s.Active.Where(c => !c.isPlayer).ToArray();
            var request = Request(s, NpcOperationKind.Start); request.sequence = s.npcSocial.nextConversationSequence;
            request.firstId = npcs[pair * 2].id; request.secondId = npcs[pair * 2 + 1].id;
            request.rendezvousId = pair == 0 ? "living-east-chat" : "kitchen-west-chat";
            request.worldReady.Add(new NpcWorldReadyEvidence { sequence = request.sequence, firstId = request.firstId, secondId = request.secondId,
                rendezvousId = request.rendezvousId, observedClockTick = request.expectedClockTick });
            return request;
        }
        private static NpcOperationRequest Tick(EpisodeState s)
        {
            var request = Request(s, NpcOperationKind.Tick);
            request.worldReady = s.npcSocial.pending.Select(row => Evidence(row, s.npcSocial.clockTick)).ToList(); return request;
        }
        private static NpcOperationRequest Cancel(EpisodeState s, long sequence)
        { var request = Request(s, NpcOperationKind.Cancel); request.sequence = sequence; return request; }
        private static EpisodeEngine Commit(EpisodeEngine engine, NpcOperationRequest request, out NpcOperationResult result)
        {
            string before = Json(engine.Snapshot); result = engine.PrepareNpcOperation(request);
            Assert.That(result.accepted, Is.True, result.reason);
            Assert.That(Json(engine.Snapshot), Is.EqualTo(before), "Prepare must never install authority before the caller saves.");
            Assert.That(EpisodeValidation.TryValidate(result.candidate, out string error), Is.True, error);
            return new EpisodeEngine(result.candidate);
        }
        private static EpisodeEngine Commit(EpisodeEngine engine, NpcOperationRequest request) => Commit(engine, request, out _);
        private static void UnchangedOutsideNpc(EpisodeState before, EpisodeState after, bool allowRelationships = false)
        {
            var left = JObject.FromObject(before); var right = JObject.FromObject(after);
            foreach (string field in new[] { "revision", "npcSocial" }) { left.Remove(field); right.Remove(field); }
            if (allowRelationships) foreach (string field in new[] { "relationships", "relationshipArcs" }) { left.Remove(field); right.Remove(field); }
            Assert.That(JToken.DeepEquals(left, right), Is.True, "Trusted NPC operation changed unrelated game authority.");
        }

        [Test]
        public void AcceptedStartUsesTwoSeparateDrawsAndDetachedCandidateWithoutInstalling()
        {
            var engine = new EpisodeEngine(ContentCatalog.Create(44)); var before = engine.Snapshot; var request = Start(before);
            var rng = new SeededRandom(before.npcSocial.randomState); rng.NextDouble(); rng.NextDouble();
            var first = engine.PrepareNpcOperation(request); var second = engine.PrepareNpcOperation(request);
            Assert.That(first.accepted, Is.True, first.reason); Assert.That(Json(second.candidate), Is.EqualTo(Json(first.candidate)));
            Assert.That(first.candidate.npcSocial.randomState, Is.EqualTo(rng.State));
            Assert.That(first.candidate.npcSocial.pending, Has.Count.EqualTo(1));
            Assert.That(first.candidate.npcSocial.pairMemory, Has.Count.EqualTo(2));
            Assert.That(first.candidate.npcSocial.pairMemory.All(row => row.count == 1 && row.lastStartTick == 0), Is.True);
            Assert.That(first.candidate.npcSocial.nextConversationSequence, Is.EqualTo(2));
            Assert.That(first.candidate.revision, Is.EqualTo(before.revision + 1)); UnchangedOutsideNpc(before, first.candidate);
            first.candidate.npcSocial.pending.Clear(); first.candidate.relationships[0].score = 100;
            Assert.That(Json(engine.Snapshot), Is.EqualTo(Json(before))); Assert.That(second.candidate.npcSocial.pending, Has.Count.EqualTo(1));
        }

        [Test]
        public void TickAdvancesExactlyOneSecondAndScansAtThreeSecondBoundaries()
        {
            var engine = new EpisodeEngine(ContentCatalog.Create(45)); var before = engine.Snapshot; var originalRng = before.npcSocial.randomState;
            for (int index = 1; index <= 7; index++)
            {
                var request = Tick(engine.Snapshot); engine = Commit(engine, request, out var result);
                Assert.That(result.scanDue, Is.EqualTo(index % 3 == 0));
                Assert.That(engine.Snapshot.npcSocial.clockTick, Is.EqualTo(index));
                Assert.That(engine.Snapshot.npcSocial.nextScanTick, Is.EqualTo((index / 3 + 1) * 3));
                var retry = engine.PrepareNpcOperation(request); Assert.That(retry.duplicate, Is.True);
                Assert.That(Json(retry.candidate), Is.EqualTo(Json(engine.Snapshot)));
            }
            Assert.That(engine.Snapshot.npcSocial.randomState, Is.EqualTo(originalRng)); UnchangedOutsideNpc(before, engine.Snapshot);
        }

        [Test]
        public void MissingStaleOrMismatchedWorldProofPausesEveryPendingPairBeforeItIsDue()
        {
            var engine = new EpisodeEngine(ContentCatalog.Create(46)); engine = Commit(engine, Start(engine.Snapshot));
            var before = engine.Snapshot; string original = Json(before);
            foreach (int variant in Enumerable.Range(0, 6))
            {
                var request = Tick(before);
                if (variant == 0) request.worldReady.Clear();
                if (variant == 1) request.worldReady[0].sequence++;
                if (variant == 2) request.worldReady[0].observedClockTick++;
                if (variant == 3) request.worldReady[0].rendezvousId = "yard-south-chat";
                if (variant == 4) request.worldReady[0].secondId = before.playerId;
                if (variant == 5) request.worldReady.Add(request.worldReady[0]);
                var result = engine.PrepareNpcOperation(request); Assert.That(result.accepted, Is.False, variant.ToString());
                Assert.That(Json(result.candidate), Is.EqualTo(original)); Assert.That(Json(engine.Snapshot), Is.EqualTo(original));
            }
        }

        [Test]
        public void StaleSessionRevisionPhaseClockJumpAndUnavailableStartsRejectAtomically()
        {
            var engine = new EpisodeEngine(ContentCatalog.Create(47)); var before = engine.Snapshot; string original = Json(before);
            foreach (int variant in Enumerable.Range(0, 10))
            {
                var request = Start(before);
                if (variant == 0) request.sessionId += "wrong";
                if (variant == 1) request.expectedRevision++;
                if (variant == 2) request.expectedPhase = EpisodePhase.HoH;
                if (variant == 3) { request.expectedClockTick++; request.targetClockTick++; }
                if (variant == 4) request.targetClockTick++;
                if (variant == 5) request.firstId = before.playerId;
                if (variant == 6) request.rendezvousId = "untrusted-venue";
                if (variant == 7) request.sequence++;
                if (variant == 8) request.freeRoamReady = false;
                if (variant == 9) request.worldReady.Clear();
                var result = engine.PrepareNpcOperation(request); Assert.That(result.accepted || result.duplicate, Is.False, variant.ToString());
                Assert.That(Json(result.candidate), Is.EqualTo(original));
            }
            var jump = Tick(before); jump.targetClockTick += 20;
            Assert.That(engine.PrepareNpcOperation(jump).accepted, Is.False);
            var pending = Commit(engine, Start(before)); var overlap = Start(pending.Snapshot);
            Assert.That(pending.PrepareNpcOperation(overlap).accepted, Is.False);
            var second = Start(pending.Snapshot, 1); second.rendezvousId = "living-east-chat"; second.worldReady[0].rendezvousId = second.rendezvousId;
            Assert.That(pending.PrepareNpcOperation(second).accepted, Is.False, "Authored two-seat venues are exclusive.");
            Assert.That(Json(engine.Snapshot), Is.EqualTo(original));
        }

        [Test]
        public void CancellationKeepsAcceptedStartMemoryButConsumesNoCompletionDrawOrEffect()
        {
            var engine = new EpisodeEngine(ContentCatalog.Create(48)); var start = Start(engine.Snapshot); engine = Commit(engine, start);
            var before = engine.Snapshot; var request = Cancel(before, 1); request.freeRoamReady = false;
            engine = Commit(engine, request, out var result);
            Assert.That(result.cancelledSequences, Is.EqualTo(new[] { 1L })); Assert.That(engine.Snapshot.npcSocial.pending, Is.Empty);
            Assert.That(engine.Snapshot.npcSocial.randomState, Is.EqualTo(before.npcSocial.randomState));
            Assert.That(Json(engine.Snapshot.npcSocial.pairMemory), Is.EqualTo(Json(before.npcSocial.pairMemory)));
            Assert.That(engine.Snapshot.npcSocial.cooldowns, Is.Empty); UnchangedOutsideNpc(before, engine.Snapshot);
            Assert.That(engine.PrepareNpcOperation(request).duplicate, Is.True);
            Assert.That(engine.PrepareNpcOperation(start).duplicate, Is.True, "Already issued start cannot replay after cancellation.");
            Assert.That(engine.PrepareNpcOperation(Cancel(engine.Snapshot, 999)).accepted, Is.False);
        }

        [Test]
        public void CompletionCooldownBlocksUntilExactDeadlineAndDoesNotCatchUpOfflineTime()
        {
            var engine = new EpisodeEngine(ContentCatalog.Create(53)); engine = Commit(engine, Start(engine.Snapshot));
            for (int guard = 0; guard < 41 && engine.Snapshot.npcSocial.pending.Count > 0; guard++) engine = Commit(engine, Tick(engine.Snapshot));
            Assert.That(engine.Snapshot.npcSocial.pending, Is.Empty);
            long deadline = engine.Snapshot.npcSocial.cooldowns.First().untilTick;
            uint savedRng = engine.Snapshot.npcSocial.randomState;
            var blocked = engine.PrepareNpcOperation(Start(engine.Snapshot)); Assert.That(blocked.accepted, Is.False);
            // Reconstructing an engine is a load; no wall-clock or catch-up action occurs.
            string saved = Json(engine.Snapshot); engine = new EpisodeEngine(engine.Snapshot);
            Assert.That(Json(engine.Snapshot), Is.EqualTo(saved));
            var catchUp = Tick(engine.Snapshot); catchUp.targetClockTick += 86400;
            Assert.That(engine.PrepareNpcOperation(catchUp).accepted, Is.False);
            for (int guard = 0; guard < 15 && engine.Snapshot.npcSocial.clockTick < deadline - 1; guard++) engine = Commit(engine, Tick(engine.Snapshot));
            Assert.That(engine.Snapshot.npcSocial.clockTick, Is.EqualTo(deadline - 1));
            Assert.That(engine.PrepareNpcOperation(Start(engine.Snapshot)).accepted, Is.False);
            engine = Commit(engine, Tick(engine.Snapshot));
            Assert.That(engine.Snapshot.npcSocial.clockTick, Is.EqualTo(deadline));
            Assert.That(engine.Snapshot.npcSocial.randomState, Is.EqualTo(savedRng), "Empty eligible time consumes no draws.");
            engine = Commit(engine, Start(engine.Snapshot));
            Assert.That(engine.Snapshot.npcSocial.pairMemory.All(row => row.count == 2), Is.True);
        }

        private static EpisodeEngine TwoConversations(Func<double, double, bool> durationTest)
        {
            for (uint seed = 1; seed <= 200; seed++)
            {
                var engine = new EpisodeEngine(ContentCatalog.Create(seed)); engine = Commit(engine, Start(engine.Snapshot)); engine = Commit(engine, Start(engine.Snapshot, 1));
                var pending = engine.Snapshot.npcSocial.pending;
                if (durationTest(Math.Ceiling(pending[0].durationMs / 1000), Math.Ceiling(pending[1].durationMs / 1000))) return engine;
            }
            Assert.Fail("Bounded legal starts did not produce required duration ordering."); return null;
        }

        [Test]
        public void OlderPendingRemainsValidAfterLaterSequenceCompletes()
        {
            var engine = TwoConversations((first, second) => first > second);
            for (int guard = 0; guard < 41 && engine.Snapshot.npcSocial.pending.Any(row => row.sequence == 2); guard++) engine = Commit(engine, Tick(engine.Snapshot));
            Assert.That(engine.Snapshot.npcSocial.pending.Select(row => row.sequence), Is.EqualTo(new[] { 1L }));
            Assert.That(engine.PrepareNpcOperation(Cancel(engine.Snapshot, 2)).duplicate, Is.True);
            var before = engine.Snapshot; engine = Commit(engine, Cancel(before, 1));
            Assert.That(engine.Snapshot.npcSocial.pending, Is.Empty); Assert.That(engine.Snapshot.npcSocial.randomState, Is.EqualTo(before.npcSocial.randomState));
        }

        [Test]
        public void DueCompletionsUseSavedOrderAndIndependentReducerDrawsExactlyOnce()
        {
            var engine = TwoConversations((first, second) => first == second);
            long deadline = (long)Math.Ceiling(engine.Snapshot.npcSocial.pending[0].durationMs / 1000);
            while (engine.Snapshot.npcSocial.clockTick < deadline - 1) engine = Commit(engine, Tick(engine.Snapshot));
            var before = engine.Snapshot; var expected = before.Clone(); var rng = new SeededRandom(before.npcSocial.randomState);
            foreach (var row in before.npcSocial.pending)
            {
                var memory = before.npcSocial.pairMemory.Single(m => m.fromId == row.firstId && m.toId == row.secondId);
                var effect = WebNpcConversations.Complete(new WebNpcConversationCompletionInput
                {
                    participants = new List<string> { row.firstId, row.secondId }, topic = row.topic, playerId = before.playerId,
                    traits1 = new List<string>(before.Find(row.firstId).traits), traits2 = new List<string>(before.Find(row.secondId).traits),
                    memory = new WebNpcConversationMemory { partnerId = memory.toId, count = memory.count, lastTopic = memory.lastTopic, lastTime = memory.lastStartTick * 1000d },
                    allNpcIds = before.Active.Where(c => !c.isPlayer).Select(c => c.id).ToList()
                }, rng.NextDouble);
                if (effect.delta != 0) ReferenceNpcChange(expected, row.firstId, row.secondId, effect.delta, rng.NextDouble());
                if (effect.gossipTarget != null)
                {
                    ReferenceNpcChange(expected, row.firstId, effect.gossipTarget.id, effect.gossipTarget.delta, rng.NextDouble());
                    ReferenceNpcChange(expected, row.secondId, effect.gossipTarget.id, effect.gossipTarget.delta, rng.NextDouble());
                }
            }
            var request = Tick(before); request.worldReady.Reverse(); engine = Commit(engine, request, out var result);
            Assert.That(result.completedSequences, Is.EqualTo(new[] { 1L, 2L })); Assert.That(engine.Snapshot.npcSocial.pending, Is.Empty);
            Assert.That(Json(engine.Snapshot.relationships), Is.EqualTo(Json(expected.relationships)));
            Assert.That(Json(engine.Snapshot.relationshipArcs), Is.EqualTo(Json(expected.relationshipArcs)));
            Assert.That(engine.Snapshot.npcSocial.randomState, Is.EqualTo(rng.State));
            Assert.That(engine.Snapshot.npcSocial.cooldowns.All(row => row.untilTick == deadline + 15), Is.True);
            UnchangedOutsideNpc(before, engine.Snapshot, true);
            var duplicate = engine.PrepareNpcOperation(request); Assert.That(duplicate.duplicate, Is.True);
            Assert.That(Json(duplicate.candidate), Is.EqualTo(Json(engine.Snapshot)));
        }

        private static void ReferenceNpcChange(EpisodeState state, string first, string second, int delta, double reciprocalRoll)
        {
            var forward = state.relationships.Single(row => row.fromId == first && row.toId == second);
            var reverse = state.relationships.Single(row => row.fromId == second && row.toId == first);
            forward.score = WebRules.ClampScore(forward.score + delta);
            reverse.score = WebRules.ClampScore(reverse.score + WebRules.ReciprocalDelta(delta, reciprocalRoll));
            forward.lastInteractionWeek = reverse.lastInteractionWeek = state.week;
            string reason = "Relationship changed by " + delta.ToString(System.Globalization.CultureInfo.InvariantCulture);
            state.relationshipArcs = WebRelationshipArcs.Update(state.relationshipArcs, second, state.Find(second).name, delta, reason, state.week).arcs;
            state.relationshipArcs = WebRelationshipArcs.Update(state.relationshipArcs, first, state.Find(first).name, delta, reason, state.week).arcs;
        }

        [Test]
        public void PlayerPhaseTransitionCancelsWithoutExtraNpcDrawsOrHistoricMemoryRewrite()
        {
            var engine = new EpisodeEngine(ContentCatalog.Create(49)); engine = Commit(engine, Start(engine.Snapshot)); var before = engine.Snapshot;
            var command = EpisodeEngineTests.Command(before, EpisodeCommandKind.Advance);
            var result = engine.Apply(command); Assert.That(result.accepted, Is.True, result.reason);
            Assert.That(result.state.phase, Is.EqualTo(EpisodePhase.HoH)); Assert.That(result.state.npcSocial.pending, Is.Empty);
            Assert.That(result.state.npcSocial.randomState, Is.EqualTo(before.npcSocial.randomState));
            Assert.That(Json(result.state.npcSocial.pairMemory), Is.EqualTo(Json(before.npcSocial.pairMemory)));
            Assert.That(result.state.npcSocial.clockTick, Is.EqualTo(before.npcSocial.clockTick));
            Assert.That(engine.PrepareNpcOperation(Tick(engine.Snapshot)).accepted, Is.False);
            var deferred = ContentCatalog.Create(50); deferred.npcSocial.rulesStartWeek = 2;
            engine = new EpisodeEngine(deferred); Assert.That(engine.PrepareNpcOperation(Start(deferred)).accepted, Is.False);
            Assert.That(engine.PrepareNpcOperation(Tick(deferred)).accepted, Is.False);
        }

        [Test]
        public void PlayerChangeForwarderKeepsOriginalMainDrawAndDoesNotTouchNpcStream()
        {
            var engine = new EpisodeEngine(ContentCatalog.Create(51)); var before = engine.Snapshot; string target = before.Active.First(c => !c.isPlayer).id;
            var rng = new SeededRandom(before.randomState); double reciprocal = WebRules.ReciprocalDelta(4, rng.NextDouble());
            var command = EpisodeEngineTests.Command(before, EpisodeCommandKind.Talk); command.targetId = target;
            var result = engine.Apply(command); Assert.That(result.accepted, Is.True, result.reason);
            Assert.That(result.state.randomState, Is.EqualTo(rng.State)); Assert.That(result.state.npcSocial.randomState, Is.EqualTo(before.npcSocial.randomState));
            Assert.That(result.state.Score(target, before.playerId), Is.EqualTo(WebRules.ClampScore(before.Score(target, before.playerId) + reciprocal)));
            Assert.That(result.state.Score(before.playerId, target), Is.EqualTo(WebRules.ClampScore(before.Score(before.playerId, target) +
                WebRules.RelationshipDelta(4, before.Find(before.playerId).stats.social, true))));
        }

        [Test]
        public void NativeInputsUseCommittedTraitsActualPactDirectedScoreAndTruthfulEventPresence()
        {
            // Canonical persisted adapter fixture, not a claim that the existing player UI
            // can create an NPC-only pact or an eviction event without playing the season.
            var state = ContentCatalog.Create(52); var pair = state.Active.Where(c => !c.isPlayer).Take(2).ToArray();
            pair[0].traits = new List<string> { "Loyal", "Emotional" }; pair[1].traits = new List<string> { "Analytical" };
            state.relationships.Single(row => row.fromId == pair[0].id && row.toId == pair[1].id).score = 65;
            state.relationships.Single(row => row.fromId == pair[1].id && row.toId == pair[0].id).score = -80;
            state.alliances.Add(new AllianceState { id = "persisted-npc-pact", name = "Canonical NPC pact", members = pair.Select(c => c.id).ToList() });
            state.events.Add(new EpisodeEvent { sequence = state.nextSequence++, week = state.week, phase = state.phase, kind = "eviction", text = "Existing canonical eviction evidence." });
            var input = new WebNpcConversationTopicInput { traits1 = pair[0].traits, traits2 = pair[1].traits, score = 65, areAllied = true,
                gameContext = new WebNpcConversationGameContext { phase = "SocialInteraction", nominees = state.nominees } };
            var rng = new SeededRandom(state.npcSocial.randomState); string topic = WebNpcConversations.PickTopic(input, true, rng.NextDouble);
            double duration = WebNpcConversations.DurationMilliseconds(topic, true, 65, rng.NextDouble);
            var engine = new EpisodeEngine(state); engine = Commit(engine, Start(state));
            Assert.That(engine.Snapshot.npcSocial.pending[0].topic, Is.EqualTo(topic));
            Assert.That(engine.Snapshot.npcSocial.pending[0].durationMs, Is.EqualTo(duration));
            Assert.That(engine.Snapshot.npcSocial.randomState, Is.EqualTo(rng.State));
            // Original input overload and explicit presence overload must agree for all
            // semantic flags without adding fabricated actor IDs to native inputs.
            input.gameContext.recentEvictees = new List<string> { "actual-source-involved-id" };
            Assert.That(WebNpcConversations.TopicWeights(input), Is.EqualTo(WebNpcConversations.TopicWeights(input, true)));
        }
    }
}
