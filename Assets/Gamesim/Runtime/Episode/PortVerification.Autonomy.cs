using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Gamesim.House;
using Gamesim.Simulation;
using UnityEngine;

namespace Gamesim.Episode
{
    /// <summary>Opt-in real-world workload. Expected candidates are never installed into the game.</summary>
    public sealed partial class PortVerification
    {
        private bool verifyAutonomy;
        private AutonomyReport autonomyReport;

        private IEnumerator ExerciseAutonomy(bool graphical)
        {
            autonomyReport = new AutonomyReport { status = "Running" };
            seasonReport.autonomy = autonomyReport;
            var baseline = seasonDirector.Snapshot;
            RequireSeason(baseline.npcSocial.rulesStartWeek == 1 && NpcSocialState.IsEligible(baseline),
                "Autonomy QA requires a legal, freshly created eligible season.");
            double started = Time.realtimeSinceStartupAsDouble;
            yield return CloseSeasonPanel();
            var observed = seasonDirector.Snapshot;
            double deadline = Time.realtimeSinceStartupAsDouble + 65;
            while (seasonDirector.Snapshot.npcSocial.pending.Count == 0 && Time.realtimeSinceStartupAsDouble < deadline)
            {
                CheckSeasonDeadline();
                yield return null;
                ObserveAutonomyStep(ref observed);
                RequireSeason(string.IsNullOrEmpty(seasonDirector.NpcAutonomyDiagnostic),
                    "The real house coordinator failed: " + seasonDirector.NpcAutonomyDiagnostic);
            }
            RequireSeason(seasonDirector.Snapshot.npcSocial.pending.Count > 0,
                "Real NPC routes must reach a venue and durably start a conversation within 65 seconds.");
            var first = seasonDirector.Snapshot.npcSocial.pending.First();
            RequireSeason(seasonDirector.IsNpcConversationPhysicallyReady(first.sequence),
                "A newly committed conversation must have fresh physical arrival evidence.");
            autonomyReport.actualStartProofChecks++;

            // Use an actual nearby player route; no player/NPC warp or synthetic topic.
            var actor = seasonDirector.gameObject.scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<HouseNpc>(true)).Single(npc => npc.Id == first.firstId);
            if (FindSeasonNpcApproach(actor, out var approach)) seasonPlayer.TryMoveTo(approach);
            deadline = Time.realtimeSinceStartupAsDouble + 55;
            while (string.IsNullOrEmpty(seasonDirector.ObservedNpcConversation) && Time.realtimeSinceStartupAsDouble < deadline)
            {
                CheckSeasonDeadline(); yield return null; ObserveAutonomyStep(ref observed);
                if (seasonDirector.Snapshot.npcSocial.pending.Count == 0) continue;
                var current = seasonDirector.Snapshot.npcSocial.pending.First();
                actor = seasonDirector.gameObject.scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<HouseNpc>(true)).Single(npc => npc.Id == current.firstId);
                if (seasonPlayer.HasArrived && FindSeasonNpcApproach(actor, out approach)) seasonPlayer.TryMoveTo(approach);
            }
            RequireSeason(!string.IsNullOrEmpty(seasonDirector.ObservedNpcConversation),
                "A physically nearby player must see the generic witnessed conversation caption.");
            autonomyReport.witnessedCaption = true;
            if (graphical)
            {
                string capture = Path.Combine(outputDirectory, "season-" + seasonReport.artifactId + "-npc-witnessed-conversation.png");
                ScreenCapture.CaptureScreenshot(capture);
                double captureDeadline = Time.realtimeSinceStartupAsDouble + 5;
                do { yield return null; ObserveAutonomyStep(ref observed); }
                while ((!File.Exists(capture) || new FileInfo(capture).Length == 0) && Time.realtimeSinceStartupAsDouble < captureDeadline);
                RequireSeason(File.Exists(capture) && new FileInfo(capture).Length > 0, "Witnessed conversation capture was not written.");
                seasonReport.screenshots.Add(capture);
            }
            ObserveAutonomyStep(ref observed);

            // Pause the world before capturing the exact saved pending receipt.
            // Retain enough duration to observe reunion before its completion frame.
            deadline = Time.realtimeSinceStartupAsDouble + 65;
            while ((seasonDirector.Snapshot.npcSocial.pending.Count == 0
                || seasonDirector.Snapshot.npcSocial.pending.Any(item =>
                    Math.Ceiling(item.durationMs / 1000d) - (seasonDirector.Snapshot.npcSocial.clockTick - item.startedTick) < 3))
                && Time.realtimeSinceStartupAsDouble < deadline)
            { CheckSeasonDeadline(); yield return null; ObserveAutonomyStep(ref observed); }
            RequireSeason(seasonDirector.Snapshot.npcSocial.pending.Count > 0
                && seasonDirector.Snapshot.npcSocial.pending.All(item =>
                    Math.Ceiling(item.durationMs / 1000d) - (seasonDirector.Snapshot.npcSocial.clockTick - item.startedTick) >= 3),
                "A pending-reload checkpoint needs at least three whole seconds before modal flush.");
            seasonDirector.OpenSettings(); ObserveAutonomyStep(ref observed);
            RequireSeason(seasonDirector.Snapshot.npcSocial.pending.Count > 0,
                "The pending-reload checkpoint must retain an actually started conversation.");
            seasonDirector.SaveNow();
            var paused = seasonDirector.Snapshot;
            string pausedJson = JsonUtility.ToJson(paused);
            var motions = seasonDirector.gameObject.scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<HouseNpcMotion>(true)).ToArray();
            var positions = motions.Select(motion => motion.transform.position).ToArray();
            double pauseUntil = Time.realtimeSinceStartupAsDouble + 1.25;
            while (Time.realtimeSinceStartupAsDouble < pauseUntil) yield return null;
            RequireSeason(JsonUtility.ToJson(seasonDirector.Snapshot) == pausedJson,
                "Settings must pause clock, memory, pending duration and both RNG streams.");
            RequireSeason(motions.Select((motion, index) => Vector3.Distance(motion.transform.position, positions[index]) < .03f).All(value => value),
                "Opening a modal must stop actual NPC movement.");
            RequireSeason(string.IsNullOrEmpty(seasonDirector.ObservedNpcConversation), "Paused conversations must not leave a stale caption.");
            autonomyReport.modalSnapshotAndMotionPaused = true;
            yield return ClickSeasonButton("Reload current slot");
            RequireSeason(JsonUtility.ToJson(seasonDirector.Snapshot) == pausedJson,
                "Reload must preserve the exact durable NPC receipt without rerolling memory, topic or duration.");
            autonomyReport.pendingReloadPreserved = true;
            observed = seasonDirector.Snapshot;
            long[] pendingSequences = paused.npcSocial.pending.Select(item => item.sequence).ToArray();
            var reunited = new HashSet<long>();
            deadline = Time.realtimeSinceStartupAsDouble + 80;
            while (pendingSequences.Any(sequence => seasonDirector.Snapshot.npcSocial.pending.Any(item => item.sequence == sequence))
                && Time.realtimeSinceStartupAsDouble < deadline)
            {
                CheckSeasonDeadline();
                var beforeFrame = seasonDirector.Snapshot;
                var readyBefore = new HashSet<long>(beforeFrame.npcSocial.pending
                    .Where(item => seasonDirector.IsNpcConversationPhysicallyReady(item.sequence)).Select(item => item.sequence));
                foreach (long sequence in pendingSequences.Where(sequence => readyBefore.Contains(sequence))) reunited.Add(sequence);
                yield return null;
                var afterFrame = seasonDirector.Snapshot;
                foreach (long sequence in pendingSequences.Where(seasonDirector.IsNpcConversationPhysicallyReady)) reunited.Add(sequence);
                if (afterFrame.npcSocial.clockTick != beforeFrame.npcSocial.clockTick)
                    RequireSeason(beforeFrame.npcSocial.pending.All(item => readyBefore.Contains(item.sequence)
                        || seasonDirector.IsNpcConversationPhysicallyReady(item.sequence)),
                        "Loaded clock advanced without actual physical reunion evidence for every pending pair.");
                else if (beforeFrame.npcSocial.pending.Any(item => !readyBefore.Contains(item.sequence)))
                    autonomyReport.unreadyReunionFrames++;
                ObserveAutonomyStep(ref observed);
            }
            RequireSeason(!pendingSequences.Any(sequence => seasonDirector.Snapshot.npcSocial.pending.Any(item => item.sequence == sequence))
                && autonomyReport.completedConversations > 0,
                "Saved pairs must physically reunite and complete, not be rerolled or silently cancelled.");
            autonomyReport.reunionPhysicalProofChecks = reunited.Count;
            RequireSeason(reunited.Count == pendingSequences.Length && autonomyReport.unreadyReunionFrames > 0,
                "Each loaded pair must first wait for and then demonstrate real physical reunion.");
            var after = seasonDirector.Snapshot;
            RequireSeason(after.randomState == baseline.randomState && after.socialActions == baseline.socialActions
                && after.nextSequence == baseline.nextSequence && after.acceptedCommandIds.SequenceEqual(baseline.acceptedCommandIds)
                && after.events.Select(JsonUtility.ToJson).SequenceEqual(baseline.events.Select(JsonUtility.ToJson))
                && after.memories.Select(JsonUtility.ToJson).SequenceEqual(baseline.memories.Select(JsonUtility.ToJson)),
                "Autonomous conversations must not charge player actions/RNG/receipts or invent public/private player events.");
            autonomyReport.playerStreamAndLogsPreserved = true;
            autonomyReport.elapsedSeconds = Time.realtimeSinceStartupAsDouble - started;
            autonomyReport.status = "Passed";
            yield return SaveReloadSeason("after-autonomous-reunion");
        }

        private void ObserveAutonomyStep(ref EpisodeState observed)
        {
            var after = seasonDirector.Snapshot;
            CheckObservedAutonomyCaption(after);
            if (after.revision == observed.revision) return;
            var expected = observed.Clone();
            // The runtime commits whole Tick(s), then accepted arrived Start(s).
            // These are detached expectations, never supplied as world evidence to the runtime.
            while (expected.npcSocial.clockTick < after.npcSocial.clockTick)
            {
                var request = AutonomyExpectation(expected, NpcOperationKind.Tick);
                foreach (var pending in expected.npcSocial.pending)
                    request.worldReady.Add(new NpcWorldReadyEvidence { sequence = pending.sequence,
                        observedClockTick = expected.npcSocial.clockTick, firstId = pending.firstId,
                        secondId = pending.secondId, rendezvousId = pending.rendezvousId });
                var result = new EpisodeEngine(expected).PrepareNpcOperation(request);
                RequireSeason(result.accepted, "Detached source tick expectation rejected: " + result.reason);
                autonomyReport.completedConversations += result.completedSequences.Count;
                expected = result.candidate;
            }
            foreach (var pending in after.npcSocial.pending.Where(item => item.sequence >= expected.npcSocial.nextConversationSequence).ToArray())
            {
                RequireSeason(seasonDirector.IsNpcConversationPhysicallyReady(pending.sequence),
                    "Every observed accepted start needs actual world evidence, not a timer alone.");
                var request = AutonomyExpectation(expected, NpcOperationKind.Start);
                request.sequence = pending.sequence; request.firstId = pending.firstId; request.secondId = pending.secondId;
                request.rendezvousId = pending.rendezvousId;
                request.worldReady.Add(new NpcWorldReadyEvidence { sequence = pending.sequence,
                    observedClockTick = expected.npcSocial.clockTick, firstId = pending.firstId,
                    secondId = pending.secondId, rendezvousId = pending.rendezvousId });
                var result = new EpisodeEngine(expected).PrepareNpcOperation(request);
                RequireSeason(result.accepted, "Detached source start expectation rejected: " + result.reason);
                expected = result.candidate; autonomyReport.actualStartProofChecks++;
            }
            RequireSeason(JsonUtility.ToJson(after) == JsonUtility.ToJson(expected),
                "Actual world commits must match detached source candidates exactly, including gossip, arcs, memory and RNG. No cancellation is allowed in this reunion workload.");
            autonomyReport.sourceTransitionChecks++;
            observed = after;
        }

        private void CheckObservedAutonomyCaption(EpisodeState state)
        {
            string caption = seasonDirector.ObservedNpcConversation;
            if (string.IsNullOrEmpty(caption)) return;
            RequireSeason(!seasonDirector.IsPanelOpen && HouseRoomQuery.TryCreate(seasonDirector.gameObject.scene, out _, out _),
                "An observed caption requires an unpaused valid local house.");
            HouseRoomQuery.TryCreate(seasonDirector.gameObject.scene, out var rooms, out _);
            var actors = seasonDirector.gameObject.scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<HouseNpc>(true)).ToDictionary(npc => npc.Id);
            bool valid = state.npcSocial.pending.Any(pending =>
            {
                if (!seasonDirector.IsNpcConversationPhysicallyReady(pending.sequence)) return false;
                var first = actors[pending.firstId]; var second = actors[pending.secondId];
                var offset = seasonPlayer.transform.position - (first.transform.position + second.transform.position) * .5f;
                if (offset.x * offset.x + offset.z * offset.z >= 36
                    || !rooms.TryLocate(seasonPlayer.transform.position, seasonPlayer.Agent.radius, out var playerRoom)
                    || !rooms.TryLocate(first.transform.position, first.GetComponent<UnityEngine.CapsuleCollider>().radius, out var firstRoom)
                    || playerRoom != firstRoom || !rooms.HasClearSight(seasonPlayer.transform, first.transform)
                    || !rooms.HasClearSight(seasonPlayer.transform, second.transform)) return false;
                return caption == HouseConversationCaption.Describe(state.Find(pending.firstId).name, state.Find(pending.secondId).name, pending.topic);
            });
            RequireSeason(valid, "Caption must exactly match a generic current topic with independently checked distance, room and both sight lines.");
            autonomyReport.captionPhysicalProofChecks++;
        }

        private static NpcOperationRequest AutonomyExpectation(EpisodeState state, NpcOperationKind kind) => new NpcOperationRequest
        {
            kind = kind, sessionId = state.sessionId, expectedRevision = state.revision, expectedPhase = state.phase,
            expectedClockTick = state.npcSocial.clockTick,
            targetClockTick = state.npcSocial.clockTick + (kind == NpcOperationKind.Tick ? 1 : 0), freeRoamReady = true
        };
        private void FinishAutonomyVerification()
        {
            if (!verifyAutonomy) return;
            if (autonomyReport == null || autonomyReport.status != "Passed" || autonomyReport.completedConversations < 1
                || autonomyReport.sourceTransitionChecks < 5 || autonomyReport.actualStartProofChecks < 1
                || !autonomyReport.pendingReloadPreserved || !autonomyReport.modalSnapshotAndMotionPaused
                || autonomyReport.reunionPhysicalProofChecks < 1 || autonomyReport.unreadyReunionFrames < 1
                || autonomyReport.captionPhysicalProofChecks < 1
                || !autonomyReport.witnessedCaption || !autonomyReport.playerStreamAndLogsPreserved)
            {
                if (autonomyReport != null) autonomyReport.status = "Failed";
                RecordSeasonError("Required standalone autonomy gates were not completed.");
            }
        }
        [Serializable] private sealed class AutonomyReport
        {
            public string status;
            public int actualStartProofChecks, sourceTransitionChecks, completedConversations;
            public int reunionPhysicalProofChecks, unreadyReunionFrames;
            public int captionPhysicalProofChecks;
            public bool witnessedCaption, modalSnapshotAndMotionPaused, pendingReloadPreserved, playerStreamAndLogsPreserved;
            public double elapsedSeconds;
        }
    }
}
