using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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

        // ---------------------------------------------------------------- D2's rules: a completion through the ledger

        /// <summary>A season as the director starts one (<see cref="ShippedRules.ApplyFresh"/>), with D2's all-week rules on or taken off.</summary>
        private static EpisodeState Shipped(uint seed, bool allWeek)
        {
            var s = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, seed);
            ShippedRules.ApplyFresh(s);
            if (!allWeek) s.allWeekRulesStartWeek = 0;
            Assert.That(EpisodeEngine.AllWeekOn(s), Is.EqualTo(allWeek));
            return s;
        }

        /// <summary>
        /// Starts the first pair's conversation and ticks to the second before it completes. A <paramref name="topic"/> is
        /// written into the started conversation and both directions' memory of it, as a saved season could carry it.
        /// </summary>
        private static EpisodeEngine ToTheLastSecond(EpisodeState fresh, string topic = null)
        {
            var engine = Commit(new EpisodeEngine(fresh), Start(fresh));
            if (topic != null)
            {
                var s = engine.Snapshot; var started = s.npcSocial.pending.Single(); started.topic = topic;
                foreach (var memory in s.npcSocial.pairMemory.Where(m => (m.fromId == started.firstId && m.toId == started.secondId) || (m.fromId == started.secondId && m.toId == started.firstId)))
                    memory.lastTopic = topic;
                Assert.That(EpisodeValidation.TryValidate(s, out string error), Is.True, error);
                engine = new EpisodeEngine(s);
            }
            var row = engine.Snapshot.npcSocial.pending.Single();
            long due = row.startedTick + (long)Math.Ceiling(row.durationMs / 1000);
            while (engine.Snapshot.npcSocial.clockTick < due - 1) engine = Commit(engine, Tick(engine.Snapshot));
            Assert.That(engine.Snapshot.npcSocial.pending, Has.Count.EqualTo(1), "Still talking, a second before the end.");
            return engine;
        }

        /// <summary>The source completion the engine draws for a pending conversation, on <paramref name="rng"/>.</summary>
        private static WebNpcConversationEnd ExpectedEnd(EpisodeState s, NpcConversationState row, SeededRandom rng)
        {
            var memory = s.npcSocial.pairMemory.Single(m => m.fromId == row.firstId && m.toId == row.secondId);
            return WebNpcConversations.Complete(new WebNpcConversationCompletionInput
            {
                participants = new List<string> { row.firstId, row.secondId }, topic = row.topic, playerId = s.playerId,
                traits1 = new List<string>(s.Find(row.firstId).traits), traits2 = new List<string>(s.Find(row.secondId).traits),
                memory = new WebNpcConversationMemory { partnerId = memory.toId, count = memory.count, lastTopic = memory.lastTopic, lastTime = memory.lastStartTick * 1000d },
                allNpcIds = s.Active.Where(c => !c.isPlayer).Select(c => c.id).ToList()
            }, rng.NextDouble);
        }

        /// <summary>The ledger entries a step added on one direction of a pair, as "type impact week N fades".</summary>
        private static string[] Added(EpisodeState before, EpisodeState after, string from, string to)
        {
            int had = before.relationships.Single(r => r.fromId == from && r.toId == to).events.Count;
            return after.relationships.Single(r => r.fromId == from && r.toId == to).events.Skip(had)
                .Select(e => Entry(e.type, e.impactScore, e.week) + (e.decayable ? "" : " (permanent)")).ToArray();
        }

        private static string Entry(string type, double impact, int week) =>
            type + " " + impact.ToString(System.Globalization.CultureInfo.InvariantCulture) + " week " + week + " fades";

        private static string Arc(EpisodeState s, string id) => Json(s.relationshipArcs.FirstOrDefault(a => a.npcId == id));

        /// <summary>Everything but the NPC world, the relationship graph and the sequence counter, before and after.</summary>
        private static void UnchangedOutsideTheLedger(EpisodeState before, EpisodeState after)
        {
            var left = JObject.FromObject(before); var right = JObject.FromObject(after);
            foreach (string field in new[] { "revision", "npcSocial", "relationships", "nextSequence" }) { left.Remove(field); right.Remove(field); }
            Assert.That(JToken.DeepEquals(left, right), Is.True, "A completed conversation changed game authority outside the ledger.");
        }

        /// <summary>
        /// The balance review's finding 3, behind D2's rule: under the all-week rules a completed conversation moves the pair
        /// as the ledger moves one - both directions by the same amount, one fading record each way - and writes no arc, which
        /// is the player's own. The same season with the rules off still takes the engine's own path (both houseguests' arcs
        /// move, the reverse direction by its reciprocal roll, no record), and the NPC world's stream ends the completion
        /// where the rules-off season's does: the reciprocal's draw is drawn and set aside. The season's stream never moves.
        /// </summary>
        [Test]
        public void UnderTheAllWeekRulesACompletedConversationMovesThePairThroughTheLedgerAndWritesNoArc()
        {
            for (uint seed = 1; seed <= 40; seed++)
            {
                var on = ToTheLastSecond(Shipped(seed, true)); var before = on.Snapshot; var row = before.npcSocial.pending.Single();
                var rng = new SeededRandom(before.npcSocial.randomState); var end = ExpectedEnd(before, row, rng);
                int delta = end.delta + EpisodeEngine.ConversationBias(before, row.firstId, row.secondId);
                if (delta == 0 || end.gossipTarget != null) continue;
                double reciprocal = WebRules.ReciprocalDelta(delta, rng.NextDouble());
                var off = ToTheLastSecond(Shipped(seed, false)); var offBefore = off.Snapshot;
                Assert.That(Json(offBefore.npcSocial), Is.EqualTo(Json(before.npcSocial)), "The two seasons' NPC worlds are the same up to the completion.");

                on = Commit(on, Tick(before), out var result); var after = on.Snapshot;
                Assert.That(result.completedSequences, Is.EqualTo(new[] { row.sequence }));
                Assert.That(Json(after.relationshipArcs), Is.EqualTo(Json(before.relationshipArcs)), "No arc: arcs are the player's own.");
                foreach (var (from, to) in new[] { (row.firstId, row.secondId), (row.secondId, row.firstId) })
                {
                    Assert.That(after.Score(from, to), Is.EqualTo(WebRules.ClampScore(before.Score(from, to) + delta)), from + " of " + to + ": the ledger's move is symmetric.");
                    Assert.That(Added(before, after, from, to), Is.EqualTo(new[] { Entry(EpisodeEngine.NpcConversationEvent, delta, before.week) }), from + " of " + to);
                    Assert.That(after.relationships.Single(r => r.fromId == from && r.toId == to).lastInteractionWeek, Is.EqualTo(before.week));
                }
                Assert.That(after.nextSequence, Is.EqualTo(before.nextSequence + 2), "Two records, one each way.");
                Assert.That(EpisodeEngine.HouseTalkRecords(after, before.nextSequence), Is.EqualTo(2), "The ids the house's talk took, as the autonomy QA allows them.");
                Assert.That(after.randomState, Is.EqualTo(before.randomState), "The season's stream.");
                Assert.That(after.npcSocial.randomState, Is.EqualTo(rng.State), "The completion's draws and the reciprocal's, set aside.");
                UnchangedOutsideTheLedger(before, after);

                off = Commit(off, Tick(offBefore)); var offAfter = off.Snapshot;
                Assert.That(offAfter.npcSocial.randomState, Is.EqualTo(after.npcSocial.randomState), "The NPC world's stream is where the rules-off season's is.");
                Assert.That(Arc(offAfter, row.firstId), Is.Not.EqualTo(Arc(offBefore, row.firstId)), "Without the rules the engine's path writes the first's arc...");
                Assert.That(Arc(offAfter, row.secondId), Is.Not.EqualTo(Arc(offBefore, row.secondId)), "...and the second's.");
                Assert.That(offAfter.Score(row.firstId, row.secondId), Is.EqualTo(WebRules.ClampScore(offBefore.Score(row.firstId, row.secondId) + delta)));
                Assert.That(offAfter.Score(row.secondId, row.firstId), Is.EqualTo(WebRules.ClampScore(offBefore.Score(row.secondId, row.firstId) + reciprocal)));
                Assert.That(Added(offBefore, offAfter, row.firstId, row.secondId), Is.Empty);
                Assert.That(offAfter.nextSequence, Is.EqualTo(offBefore.nextSequence));
                Assert.That(EpisodeEngine.HouseTalkRecords(offAfter, offBefore.nextSequence), Is.Zero, "Without the rules the house's talk takes no id.");
                return;
            }
            Assert.Fail("No season in forty completed its first conversation with a move and no gossip.");
        }

        /// <summary>
        /// Gossip under D2's rules: each speaker and the houseguest they talked about move through the ledger by the gossip's
        /// amount, both ways, with a fading record each way; no arc moves; the NPC world's stream spends the completion's draws
        /// and one set aside for each change the engine's path would have rolled a reciprocal for.
        /// </summary>
        [Test]
        public void UnderTheAllWeekRulesGossipMovesEachSpeakerAndTheirSubjectThroughTheLedger()
        {
            for (uint seed = 1; seed <= 60; seed++)
            {
                var engine = ToTheLastSecond(Shipped(seed, true), "gossip"); var before = engine.Snapshot; var row = before.npcSocial.pending.Single();
                var rng = new SeededRandom(before.npcSocial.randomState); var end = ExpectedEnd(before, row, rng);
                if (end.gossipTarget == null) continue;
                int delta = end.delta + EpisodeEngine.ConversationBias(before, row.firstId, row.secondId);
                string subject = end.gossipTarget.id; int said = end.gossipTarget.delta;
                Assert.That(subject, Is.Not.EqualTo(before.playerId), "Gossip leaves the player out.");

                engine = Commit(engine, Tick(before)); var after = engine.Snapshot;
                Assert.That(Json(after.relationshipArcs), Is.EqualTo(Json(before.relationshipArcs)), "No arc: arcs are the player's own.");
                foreach (string speaker in new[] { row.firstId, row.secondId })
                    foreach (var (from, to) in new[] { (speaker, subject), (subject, speaker) })
                    {
                        Assert.That(after.Score(from, to), Is.EqualTo(WebRules.ClampScore(before.Score(from, to) + said)), from + " of " + to);
                        Assert.That(Added(before, after, from, to), Is.EqualTo(new[] { Entry(EpisodeEngine.NpcGossipEvent, said, before.week) }), from + " of " + to);
                    }
                var own = delta == 0 ? new string[0] : new[] { Entry(EpisodeEngine.NpcConversationEvent, delta, before.week) };
                Assert.That(Added(before, after, row.firstId, row.secondId), Is.EqualTo(own));
                Assert.That(Added(before, after, row.secondId, row.firstId), Is.EqualTo(own));
                for (int change = 0; change < (delta == 0 ? 2 : 3); change++) rng.NextDouble();
                Assert.That(after.npcSocial.randomState, Is.EqualTo(rng.State), "The completion's draws and a reciprocal's for each change, set aside.");
                Assert.That(after.randomState, Is.EqualTo(before.randomState), "The season's stream.");
                Assert.That(after.nextSequence, Is.EqualTo(before.nextSequence + (delta == 0 ? 0 : 2) + 4), "A record each way for each change.");
                Assert.That(EpisodeEngine.HouseTalkRecords(after, before.nextSequence), Is.EqualTo(after.nextSequence - before.nextSequence),
                    "The ids the house's talk took, as the autonomy QA allows them.");
                UnchangedOutsideTheLedger(before, after);
                return;
            }
            Assert.Fail("No season in sixty completed a gossip with a subject.");
        }

        /// <summary>
        /// The ledger path is for two houseguests only: a change with the player as one of the two still takes the engine's own
        /// path under D2's rules and moves the player's arc (as an act of the house's on the player does, NpcSocialActions.Act).
        /// A conversation never has the player in it today, so this reaches the completion's change by name
        /// (EpisodeEngine.CompletionChange; the editor's tests cannot see the simulation's internals).
        /// </summary>
        [Test]
        public void UnderTheAllWeekRulesAChangeWithThePlayerStillTakesTheEnginesPathAndMovesTheirArc()
        {
            var s = Shipped(61, true);
            var npcs = s.Active.Where(c => !c.isPlayer).Take(2).ToArray();
            var row = new NpcConversationState { sequence = 1, firstId = npcs[0].id, secondId = npcs[1].id, topic = "gossip", week = s.week, phase = s.phase };
            var change = typeof(EpisodeEngine).GetMethod("CompletionChange", BindingFlags.NonPublic | BindingFlags.Static, null,
                new[] { typeof(EpisodeState), typeof(NpcConversationState), typeof(string), typeof(string), typeof(double), typeof(string) }, null);
            Assert.That(change, Is.Not.Null, "EpisodeEngine.CompletionChange(EpisodeState, NpcConversationState, string, string, double, string)");

            var before = s.Clone(); var rng = new SeededRandom(s.npcSocial.randomState);
            double reciprocal = WebRules.ReciprocalDelta(-3, rng.NextDouble());
            change.Invoke(null, new object[] { s, row, npcs[0].id, s.playerId, -3d, EpisodeEngine.NpcGossipEvent });
            Assert.That(Arc(s, npcs[0].id), Is.Not.EqualTo(Arc(before, npcs[0].id)), "The player's arc with the houseguest moves.");
            Assert.That(s.relationshipArcs.Select(a => a.npcId).Where(id => Arc(s, id) != Arc(before, id)), Is.EqualTo(new[] { npcs[0].id }), "...and no other.");
            Assert.That(s.Score(npcs[0].id, s.playerId), Is.EqualTo(WebRules.ClampScore(before.Score(npcs[0].id, s.playerId) - 3)));
            Assert.That(s.Score(s.playerId, npcs[0].id), Is.EqualTo(WebRules.ClampScore(before.Score(s.playerId, npcs[0].id) + reciprocal)), "The reciprocal, rolled.");
            Assert.That(Added(before, s, npcs[0].id, s.playerId), Is.Empty, "The engine's path, no ledger record.");
            Assert.That(s.npcSocial.randomState, Is.EqualTo(rng.State));
            Assert.That(s.randomState, Is.EqualTo(before.randomState), "The season's stream.");

            var withPlayer = s.Clone();
            change.Invoke(null, new object[] { s, row, npcs[0].id, npcs[1].id, 4d, EpisodeEngine.NpcConversationEvent });
            Assert.That(Json(s.relationshipArcs), Is.EqualTo(Json(withPlayer.relationshipArcs)), "Between two houseguests, no arc.");
            Assert.That(Added(withPlayer, s, npcs[1].id, npcs[0].id), Is.EqualTo(new[] { Entry(EpisodeEngine.NpcConversationEvent, 4, s.week) }));
        }

        /// <summary>
        /// What the autonomy QA (PortVerification's autonomy workload) lets free roam take of the season's sequence ids under
        /// D2's rules: the house's talk records between two houseguests from a given id on, one id each - not the same types on
        /// an edge of the player's, not another of the house's types, not a record before that id.
        /// </summary>
        [Test]
        public void TheHousesTalkRecordsCountOnlyTalkBetweenTwoHouseguestsFromAnId()
        {
            var s = Shipped(62, true);
            var npcs = s.Active.Where(c => !c.isPlayer).Take(3).ToArray();
            RelationshipLedger.Record(s, npcs[0].id, npcs[1].id, EpisodeEngine.NpcConversationEvent, 2, "Earlier talk");
            int from = s.nextSequence;
            Assert.That(EpisodeEngine.HouseTalkRecords(s, from), Is.Zero, "Nothing from the id on yet.");
            RelationshipLedger.Record(s, npcs[0].id, npcs[1].id, EpisodeEngine.NpcConversationEvent, 2, "Talk");
            RelationshipLedger.Record(s, npcs[0].id, npcs[2].id, EpisodeEngine.NpcGossipEvent, -1, "Gossip");
            RelationshipLedger.Record(s, npcs[0].id, s.playerId, EpisodeEngine.NpcGossipEvent, -1, "On the player's edges");
            RelationshipLedger.Record(s, npcs[1].id, npcs[2].id, "rumor", -1, "Another type");
            Assert.That(s.nextSequence - from, Is.EqualTo(8), "Every record took an id.");
            Assert.That(EpisodeEngine.HouseTalkRecords(s, from), Is.EqualTo(4), "The talk and the gossip between houseguests, both ways.");
            Assert.That(EpisodeEngine.HouseTalkRecords(s, from - 2), Is.EqualTo(6), "From the earlier talk's ids, it too.");
            Assert.That(EpisodeEngine.HouseTalkRecords(null, from), Is.Zero);
        }
    }
}
