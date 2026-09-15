using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Gamesim.Episode;
using Gamesim.House;
using Gamesim.Persistence;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// Real EpisodeHouse/isolated-save regressions. Reflection controls only transient
    /// timing/request seams; physical binding, leases, disk validation and HUD are real.
    /// No global diagnostic suspension, forged arrival proof or live user slot is used.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        [UnityTest]
        public IEnumerator NpcRuntime_DeferredLegacyWeekDoesNotBindMoveOrChangeCarving()
        {
            var fixture = ContentCatalog.Create(6101);
            fixture.npcSocial = NpcSocialState.Create(fixture.seed, fixture.week + 1);
            new EpisodeSaveStore(director.SavePath).Save(fixture);
            yield return ReloadEpisode();

            var npcs = SceneComponents<HouseNpc>();
            Assert.That(npcs, Has.Length.EqualTo(5));
            var positions = npcs.Select(npc => npc.transform.position).ToArray();
            var rotations = npcs.Select(npc => npc.transform.rotation).ToArray();
            var obstacles = npcs.Select(npc => npc.GetComponent<NavMeshObstacle>()).ToArray();
            Assert.That(obstacles.All(obstacle => obstacle != null && obstacle.enabled && obstacle.carving), Is.True,
                "The authored legacy house must still use its original static carving obstacles.");
            var bytes = File.ReadAllBytes(director.SavePath);

            for (int frame = 0; frame < 4; frame++)
            {
                NpcInvoke("TickNpcSocialRuntime", .75f);
                yield return null;
                Assert.That(NpcRead<HouseMeetingCoordinator>("npcMeetings"), Is.Null);
                for (int index = 0; index < npcs.Length; index++)
                {
                    Assert.That(npcs[index].GetComponent<HouseNpcMotion>(), Is.Null,
                        "Preactivation must not create a motion owner even if it would remain idle.");
                    Assert.That(npcs[index].transform.position, Is.EqualTo(positions[index]));
                    Assert.That(npcs[index].transform.rotation, Is.EqualTo(rotations[index]));
                    Assert.That(obstacles[index].enabled && obstacles[index].carving, Is.True);
                }
            }
            AssertEquivalent(fixture, director.Snapshot);
            Assert.That(File.ReadAllBytes(director.SavePath), Is.EqualTo(bytes));
        }

        [UnityTest]
        public IEnumerator NpcRuntime_MaximumSessionIdUsesRealBoundedReunionLeaseAndFreezesUntilArrival()
        {
            var fixture = NpcPendingRuntimeFixture();
            fixture.sessionId = new string('s', 160); // The actual strict save limit, not an invalid stress input.
            new EpisodeSaveStore(director.SavePath).Save(fixture);
            yield return ReloadEpisode();
            yield return WaitForNpcRuntimeBinding();

            NpcInvoke("SetNpcWorldPaused", false);
            NpcInvoke("RestorePendingNpcMeetings");
            var leases = NpcRead<Dictionary<long, HouseMeetingLease>>("npcPendingWorld");
            Assert.That(leases.TryGetValue(1, out var lease), Is.True,
                "A legal long session ID must still reserve its saved venue using real NavMesh paths.");
            Assert.That(lease.Token.Length, Is.LessThanOrEqualTo(128));
            Assert.That(lease.Generation.Length, Is.LessThanOrEqualTo(128));
            Assert.That(lease.Token, Does.Not.Contain(fixture.sessionId));
            Assert.That(lease.VenueId, Is.EqualTo(fixture.npcSocial.pending[0].rendezvousId));
            Assert.That(lease.FirstId, Is.EqualTo(fixture.npcSocial.pending[0].firstId));
            Assert.That(lease.SecondId, Is.EqualTo(fixture.npcSocial.pending[0].secondId));
            var coordinator = NpcRead<HouseMeetingCoordinator>("npcMeetings");
            Assert.That(coordinator.TryGetLease(lease.Token, out var owned), Is.True);
            Assert.That(owned, Is.SameAs(lease));
            Assert.That(coordinator.ValidateArrivedPair(lease, out _), Is.False,
                "The saved pair starts in different rooms and must physically reunite before time resumes.");

            var before = director.Snapshot;
            var bytes = File.ReadAllBytes(director.SavePath);
            NpcWrite("npcFrameFraction", .95d);
            NpcWrite("npcWorldFraction", .06d);
            Assert.That((bool)NpcInvoke("FlushNpcWholeTicks"), Is.False);
            Assert.That(NpcRead<double>("npcFrameFraction"), Is.EqualTo(.95d));
            Assert.That(NpcRead<double>("npcWorldFraction"), Is.EqualTo(.06d));
            AssertEquivalent(before, director.Snapshot);
            Assert.That(File.ReadAllBytes(director.SavePath), Is.EqualTo(bytes));
            Assert.That(director.Snapshot.npcSocial.randomState, Is.EqualTo(fixture.npcSocial.randomState),
                "Reunion must not reroll the topic/duration, add start memory, or consume completion draws.");
            Assert.That(JsonUtility.ToJson(director.Snapshot.npcSocial), Is.EqualTo(JsonUtility.ToJson(fixture.npcSocial)));
        }

        [UnityTest]
        public IEnumerator NpcRuntime_QueuedRequestCannotCrossSameSessionIdenticalRevisionReload()
        {
            yield return WaitForNpcRuntimeBinding();
            NpcResetTransientFractions();
            var before = director.Snapshot;
            var store = new EpisodeSaveStore(director.SavePath);
            store.Save(before);
            var queued = NpcInvoke("NpcRequest", NpcOperationKind.Tick);
            var generation = (long)NpcObjectField(queued, "Generation");
            var operation = (NpcOperationRequest)NpcObjectField(queued, "Operation");
            Assert.That(NpcObjectField(queued, "Owner"), Is.SameAs(director));
            Assert.That(operation.freeRoamReady, Is.True);

            director.LoadNow();
            AssertEquivalent(before, director.Snapshot);
            yield return WaitForNpcRuntimeBinding();
            NpcResetTransientFractions();
            AssertEquivalent(before, director.Snapshot);
            Assert.That(NpcRead<long>("loadGeneration"), Is.GreaterThan(generation));
            Assert.That(operation.sessionId, Is.EqualTo(director.Snapshot.sessionId));
            Assert.That(operation.expectedRevision, Is.EqualTo(director.Snapshot.revision));
            Assert.That(operation.expectedClockTick, Is.EqualTo(director.Snapshot.npcSocial.clockTick));
            var bytes = File.ReadAllBytes(director.SavePath);

            Assert.That((bool)NpcInvoke("CommitNpcOperation", queued, null), Is.False);
            AssertEquivalent(before, director.Snapshot);
            Assert.That(File.ReadAllBytes(director.SavePath), Is.EqualTo(bytes));
            // A fresh request succeeds in the SAME physical world: rejection was the
            // stale generation, not a disabled controller, unavailable route or modal.
            var current = NpcInvoke("NpcRequest", NpcOperationKind.Tick);
            Assert.That((bool)NpcInvoke("CommitNpcOperation", current, null), Is.True);
            Assert.That(director.Snapshot.npcSocial.clockTick, Is.EqualTo(before.npcSocial.clockTick + 1));
            Assert.That(director.Snapshot.revision, Is.EqualTo(before.revision + 1));
            Assert.That(store.TryLoad(out var disk, out var reason), Is.True, reason);
            AssertEquivalent(director.Snapshot, disk);
        }

        [UnityTest]
        public IEnumerator NpcRuntime_ExplicitFractionalFlushCommitsPointNineFivePlusPointZeroSixExactlyOnce()
        {
            yield return WaitForNpcRuntimeBinding();
            var before = director.Snapshot;
            Assert.That(before.npcSocial.pending, Is.Empty);
            new EpisodeSaveStore(director.SavePath).Save(before);
            NpcWrite("npcFrameFraction", .95d);
            NpcWrite("npcWorldFraction", .06d);
            double freeBefore = NpcRead<double>("npcFreeSeconds");

            Assert.That((bool)NpcInvoke("FlushNpcWholeTicks"), Is.True);
            var after = director.Snapshot;
            Assert.That(after.npcSocial.clockTick, Is.EqualTo(before.npcSocial.clockTick + 1));
            Assert.That(after.revision, Is.EqualTo(before.revision + 1));
            Assert.That(after.randomState, Is.EqualTo(before.randomState));
            Assert.That(after.npcSocial.randomState, Is.EqualTo(before.npcSocial.randomState));
            Assert.That(after.acceptedCommandIds, Is.EqualTo(before.acceptedCommandIds));
            Assert.That(after.socialActions, Is.EqualTo(before.socialActions));
            Assert.That(NpcRead<double>("npcFrameFraction"), Is.EqualTo(.01d).Within(1e-12));
            Assert.That(NpcRead<double>("npcWorldFraction"), Is.Zero);
            Assert.That(NpcRead<double>("npcFreeSeconds"), Is.EqualTo(freeBefore + .06d).Within(1e-12));
            Assert.That(new EpisodeSaveStore(director.SavePath).TryLoad(out var disk, out var reason), Is.True, reason);
            AssertEquivalent(after, disk);
            var bytes = File.ReadAllBytes(director.SavePath);
            Assert.That((bool)NpcInvoke("FlushNpcWholeTicks"), Is.True);
            AssertEquivalent(after, director.Snapshot);
            Assert.That(File.ReadAllBytes(director.SavePath), Is.EqualTo(bytes));
        }

        [UnityTest]
        public IEnumerator NpcRuntime_LoadIsNonwritingEvenWithAWholeUncommittedSecond()
        {
            yield return WaitForNpcRuntimeBinding();
            var before = director.Snapshot;
            var store = new EpisodeSaveStore(director.SavePath);
            store.Save(before); store.Save(before);
            var primary = File.ReadAllBytes(store.SavePath);
            var backup = File.ReadAllBytes(store.BackupPath);
            NpcWrite("npcFrameFraction", 1.01d);
            NpcWrite("npcWorldFraction", .04d);

            director.LoadNow();

            AssertEquivalent(before, director.Snapshot);
            Assert.That(File.ReadAllBytes(store.SavePath), Is.EqualTo(primary));
            Assert.That(File.ReadAllBytes(store.BackupPath), Is.EqualTo(backup));
            Assert.That(NpcRead<double>("npcFrameFraction"), Is.Zero);
            Assert.That(NpcRead<double>("npcWorldFraction"), Is.Zero);
            Assert.That(Directory.GetFiles(temporaryDirectory, "*.pending-*"), Is.Empty);
        }

        [UnityTest]
        public IEnumerator NpcRuntime_RecoveryReadsOriginalBackupBeforeAnyTimingWrite()
        {
            yield return WaitForNpcRuntimeBinding();
            var original = director.Snapshot;
            var simulation = new EpisodeEngine(original);
            var changed = simulation.Apply(new EpisodeCommand
            {
                id = "npc-runtime-backup-distinct", actorId = original.playerId,
                expectedRevision = original.revision, expectedPhase = original.phase,
                kind = EpisodeCommandKind.Talk, targetId = ContentCatalog.MayaId
            });
            Assert.That(changed.accepted, Is.True, changed.reason);
            var store = new EpisodeSaveStore(director.SavePath);
            store.Save(original); store.Save(changed.state);
            director.LoadNow();
            yield return WaitForNpcRuntimeBinding();
            AssertEquivalent(changed.state, director.Snapshot);
            var originalBackup = File.ReadAllBytes(store.BackupPath);
            var priorPrimary = File.ReadAllBytes(store.SavePath);
            NpcWrite("npcFrameFraction", 1.01d);
            NpcWrite("npcWorldFraction", .04d);

            director.RecoverBackup();

            AssertEquivalent(original, director.Snapshot);
            Assert.That(File.ReadAllBytes(store.BackupPath), Is.EqualTo(originalBackup));
            Assert.That(File.ReadAllBytes(store.SavePath), Is.EqualTo(originalBackup));
            var retained = Directory.GetFiles(temporaryDirectory, Path.GetFileName(store.SavePath) + ".before-recovery-*.json");
            Assert.That(retained, Has.Length.EqualTo(1));
            Assert.That(File.ReadAllBytes(retained[0]), Is.EqualTo(priorPrimary),
                "Recovery must archive the actual old primary, not a just-flushed replacement.");
            Assert.That(NpcRead<double>("npcFrameFraction"), Is.Zero);
            Assert.That(NpcRead<double>("npcWorldFraction"), Is.Zero);
        }

        [UnityTest]
        public IEnumerator NpcRuntime_NewSeasonRetainsOldSlotWithoutFlushingItsPendingTime()
        {
            yield return WaitForNpcRuntimeBinding();
            var before = director.Snapshot;
            var store = new EpisodeSaveStore(director.SavePath);
            store.Save(before); store.Save(before);
            var primary = File.ReadAllBytes(store.SavePath);
            var backup = File.ReadAllBytes(store.BackupPath);
            NpcWrite("npcFrameFraction", 1.01d);
            NpcWrite("npcWorldFraction", .04d);

            director.NewSeason();

            Assert.That(director.SavePath, Is.Not.EqualTo(store.SavePath));
            Assert.That(Path.GetFullPath(director.SavePath), Does.StartWith(Path.GetFullPath(temporaryDirectory)));
            Assert.That(director.Snapshot.sessionId, Is.Not.EqualTo(before.sessionId));
            Assert.That(director.Snapshot.revision, Is.Zero);
            Assert.That(director.Snapshot.npcSocial.clockTick, Is.Zero);
            Assert.That(File.ReadAllBytes(store.SavePath), Is.EqualTo(primary));
            Assert.That(File.ReadAllBytes(store.BackupPath), Is.EqualTo(backup));
            Assert.That(store.TryLoad(out var oldDisk, out var oldReason), Is.True, oldReason);
            AssertEquivalent(before, oldDisk);
            Assert.That(new EpisodeSaveStore(director.SavePath).TryLoad(out var fresh, out var reason), Is.True, reason);
            AssertEquivalent(director.Snapshot, fresh);
        }

        [UnityTest]
        public IEnumerator NpcRuntime_ControllerDisposalClearsTalkingOnStillActiveNpcRoots()
        {
            yield return WaitForNpcRuntimeBinding();
            var coordinator = NpcRead<HouseMeetingCoordinator>("npcMeetings");
            var visuals = SceneComponents<HouseNpc>().Select(npc => npc.GetComponent<CharacterPresentation>()).ToArray();
            Assert.That(visuals.All(visual => visual != null && visual.gameObject.activeInHierarchy), Is.True);
            foreach (var visual in visuals) visual.SetTalking(true);
            Assert.That(visuals.All(NpcVisualTalking), Is.True);
            var generation = NpcRead<long>("loadGeneration");

            director.enabled = false;
            try
            {
                Assert.That(visuals.All(visual => visual.gameObject.activeInHierarchy), Is.True);
                Assert.That(visuals.All(visual => !NpcVisualTalking(visual)), Is.True,
                    "Disabling the director alone must release NPC speaking gestures, not wait for root destruction.");
                Assert.That(coordinator.IsDisposed, Is.True);
                Assert.That(NpcRead<HouseMeetingCoordinator>("npcMeetings"), Is.Null);
                Assert.That(NpcRead<Dictionary<long, HouseMeetingLease>>("npcPendingWorld"), Is.Empty);
                Assert.That(NpcRead<long>("loadGeneration"), Is.GreaterThan(generation));
                Assert.That(director.ObservedNpcConversation, Is.Empty);
            }
            finally { director.enabled = true; }
        }

        [UnityTest]
        public IEnumerator NpcRuntime_NotebookDoesNotTurnUnwitnessedAggregateArcsIntoPlayerKnowledge()
        {
            var fixture = ContentCatalog.Create(6109);
            var first = fixture.Find(ContentCatalog.MayaId);
            var second = fixture.Find("taylor-kim");
            // Valid controlled source-arc history has no participant/knowledge field.
            // Its NPC-only origin cannot be guessed into a player relationship by UI.
            fixture.relationshipArcs = WebRelationshipArcs.Update(fixture.relationshipArcs,
                first.id, first.name, 20, "NPC conversation", fixture.week).arcs;
            fixture.relationshipArcs = WebRelationshipArcs.Update(fixture.relationshipArcs,
                first.id, first.name, 20, "NPC conversation", fixture.week).arcs;
            string forbiddenNarrative = WebRelationshipArcs.Narrative(fixture.relationshipArcs.Single());
            Assert.That(forbiddenNarrative, Is.Not.Empty);
            const string secretAlliance = "Unwitnessed NPC pact regression sentinel";
            const string secretEvent = "Private NPC conversation regression sentinel";
            const string knownEvent = "Player-known note regression sentinel";
            fixture.alliances.Add(new AllianceState { id = "npc-private-fixture", name = secretAlliance,
                members = new List<string> { first.id, second.id } });
            fixture.events.Add(new EpisodeEvent { sequence = fixture.nextSequence++, week = fixture.week,
                phase = fixture.phase, kind = "social", text = secretEvent,
                audienceIds = new List<string> { first.id, second.id } });
            fixture.events.Add(new EpisodeEvent { sequence = fixture.nextSequence++, week = fixture.week,
                phase = fixture.phase, kind = "social", text = knownEvent,
                audienceIds = new List<string> { fixture.playerId } });
            Assert.That(EpisodeValidation.TryValidate(fixture, out var validation), Is.True, validation);
            new EpisodeSaveStore(director.SavePath).Save(fixture);
            director.LoadNow();
            director.OpenJournal();
            yield return null;
            yield return null;

            string visible = string.Join("\n", director.GetComponentsInChildren<Text>(true)
                .Where(text => text.gameObject.activeInHierarchy).Select(text => text.text));
            Assert.That(visible, Does.Contain("YOUR NOTEBOOK"));
            Assert.That(visible, Does.Contain(knownEvent), "The privacy check must inspect the real populated notebook.");
            Assert.That(visible, Does.Contain(first.name + " · " + first.status + " · Your trust "
                + fixture.Score(fixture.playerId, first.id).ToString("0")));
            Assert.That(visible, Does.Not.Contain(forbiddenNarrative));
            Assert.That(visible, Does.Not.Contain(secretAlliance));
            Assert.That(visible, Does.Not.Contain(secretEvent));
            Assert.That(director.ObservedNpcConversation, Is.Empty);
            AssertEquivalent(fixture, director.Snapshot);
        }

        private IEnumerator WaitForNpcRuntimeBinding()
        {
            Assert.That(NpcRead<bool>("npcDiagnosticsSuspended"), Is.False,
                "These tests exercise actual runtime binding, not the diagnostic-suspended world.");
            // Suppress only NEW transient approach planning during fixture setup. No
            // durable clock/state, accepted pair, arrival proof or RNG is synthesized.
            NpcWrite("npcApproachDiagnosticsSuppressed", true);
            NpcInvoke("EnsureNpcSocialWorld");
            float deadline = Time.realtimeSinceStartup + 5;
            while (!director.NpcAutonomyReady && Time.realtimeSinceStartup < deadline)
            {
                NpcWrite("npcApproachDiagnosticsSuppressed", true);
                yield return null;
            }
            Assert.That(director.NpcAutonomyReady, Is.True, director.NpcAutonomyDiagnostic);
            NpcInvoke("SetNpcWorldPaused", false);
            Assert.That(NpcRead<HouseMeetingCoordinator>("npcMeetings").IsReady, Is.True);
        }

        private static EpisodeState NpcPendingRuntimeFixture()
        {
            var state = ContentCatalog.Create(6102);
            state.revision = 12;
            state.npcSocial.clockTick = 10;
            state.npcSocial.nextScanTick = 12;
            state.npcSocial.nextConversationSequence = 2;
            state.npcSocial.randomState = 0; // Zero is a legal restored independent RNG state.
            string first = ContentCatalog.MayaId, second = "taylor-kim";
            state.npcSocial.pending.Add(new NpcConversationState
            {
                sequence = 1, firstId = first, secondId = second, topic = "bonding",
                rendezvousId = "living-east-chat", week = state.week, phase = state.phase,
                startedTick = 10, durationMs = 15000.125
            });
            state.npcSocial.pairMemory.Add(new NpcPairMemoryState
                { fromId = first, toId = second, count = 1, lastTopic = "bonding", lastStartTick = 10 });
            state.npcSocial.pairMemory.Add(new NpcPairMemoryState
                { fromId = second, toId = first, count = 1, lastTopic = "bonding", lastStartTick = 10 });
            Assert.That(EpisodeValidation.TryValidate(state, out var reason), Is.True, reason);
            return state;
        }

        private void NpcResetTransientFractions()
        { NpcWrite("npcFrameFraction", 0d); NpcWrite("npcWorldFraction", 0d); }

        private object NpcInvoke(string name, params object[] arguments)
        {
            var method = typeof(EpisodeDirector).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, "Missing runtime regression seam: " + name);
            try { return method.Invoke(director, arguments); }
            catch (TargetInvocationException error) when (error.InnerException != null) { throw error.InnerException; }
        }

        private T NpcRead<T>(string name) => (T)NpcDirectorField(name).GetValue(director);
        private void NpcWrite(string name, object value) => NpcDirectorField(name).SetValue(director, value);
        private static FieldInfo NpcDirectorField(string name)
        {
            var field = typeof(EpisodeDirector).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "Missing runtime regression seam: " + name);
            return field;
        }

        private static object NpcObjectField(object value, string name)
        {
            Assert.That(value, Is.Not.Null);
            var field = value.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "Missing queued-operation ownership field: " + name);
            return field.GetValue(value);
        }

        private static bool NpcVisualTalking(CharacterPresentation visual)
        {
            var field = typeof(CharacterPresentation).GetField("talking", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            return (bool)field.GetValue(visual);
        }
    }
}
