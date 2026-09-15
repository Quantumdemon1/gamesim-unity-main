using System;
using System.Collections;
using System.IO;
using Gamesim.House;
using Gamesim.Persistence;
using Gamesim.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// Uses the existing isolated EpisodeHouse fixture and runtime reflection helpers.
    /// Only transient elapsed-time/Editor-planning seams are controlled: no saved clock,
    /// pending conversation, arrival proof, engine authority or RNG is fabricated.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        [UnityTest]
        public IEnumerator NpcScan_FreshClockDoesNotPlanBeforeThirdDurableSecond()
        {
            yield return BindNpcScanWorld();
            Assert.That(director.Snapshot.npcSocial.clockTick, Is.Zero);
            Assert.That(director.Snapshot.npcSocial.nextScanTick, Is.EqualTo(3));
            var before = director.Snapshot;
            AssertNoNpcScanApproaches();

            CommitNpcScanSecond();
            Assert.That(director.Snapshot.npcSocial.clockTick, Is.EqualTo(1));
            AssertNoNpcScanApproaches();
            CommitNpcScanSecond();
            Assert.That(director.Snapshot.npcSocial.clockTick, Is.EqualTo(2));
            AssertNoNpcScanApproaches();
            CommitNpcScanSecond();

            Assert.That(director.Snapshot.npcSocial.clockTick, Is.EqualTo(3));
            Assert.That(director.Snapshot.npcSocial.nextScanTick, Is.EqualTo(6));
            AssertNpcScanCreatedRealApproaches();
            Assert.That(director.Snapshot.randomState, Is.EqualTo(before.randomState));
            Assert.That(director.Snapshot.npcSocial.randomState, Is.EqualTo(before.npcSocial.randomState),
                "World route planning must not choose a topic or consume a mechanical draw.");
            Assert.That(director.Snapshot.npcSocial.pending, Is.Empty,
                "Travel must physically finish before a conversation can become durable.");
            long leaseCounter = NpcRead<long>("npcLeaseCounter");
            NpcInvoke("TickNpcSocialRuntime", .1f);
            Assert.That(director.Snapshot.npcSocial.clockTick, Is.EqualTo(3));
            Assert.That(NpcRead<long>("npcLeaseCounter"), Is.EqualTo(leaseCounter),
                "A world observation frame is not a second opportunity scan at the same saved deadline.");
        }

        [UnityTest]
        public IEnumerator NpcScan_FailedThirdSecondDoesNotReserveRoutesBeforeExplicitRetry()
        {
            yield return BindNpcScanWorld();
            CommitNpcScanSecond(); CommitNpcScanSecond();
            var before = director.Snapshot;
            string beforeJson = JsonUtility.ToJson(before);
            byte[] primary = File.ReadAllBytes(director.SavePath);
            byte[] backup = File.ReadAllBytes(director.SavePath + ".backup");
            int writes = 0;
            NpcWrite("saveCandidateForDiagnostics", new Action<EpisodeState>(candidate =>
            {
                writes++;
                Assert.That(candidate.npcSocial.clockTick, Is.EqualTo(3));
                AssertNoNpcScanApproaches();
                throw new IOException("Injected cadence-boundary save failure.");
            }));
            try
            {
                NpcWrite("npcFrameFraction", 1d); NpcWrite("npcWorldFraction", 0d);
                director.SaveNow();
                Assert.That(writes, Is.EqualTo(1));
                Assert.That(JsonUtility.ToJson(director.Snapshot), Is.EqualTo(beforeJson));
                Assert.That(File.ReadAllBytes(director.SavePath), Is.EqualTo(primary));
                Assert.That(File.ReadAllBytes(director.SavePath + ".backup"), Is.EqualTo(backup));
                Assert.That(NpcRead<bool>("npcSaveSuspended"), Is.True);
                AssertNoNpcScanApproaches();
                NpcInvoke("TickNpcSocialRuntime", .5f);
                Assert.That(writes, Is.EqualTo(1), "Failure must suspend automatic retries and their routes.");
                AssertNoNpcScanApproaches();
            }
            finally { NpcWrite("saveCandidateForDiagnostics", null); }

            director.SaveNow(); // Explicitly validate/save the unchanged session and permit runtime resumption.
            Assert.That(NpcRead<bool>("npcSaveSuspended"), Is.False);
            NpcInvoke("TickNpcSocialRuntime", .1f); // Resumes real world ownership before retrying the retained whole second.
            Assert.That(director.Snapshot.npcSocial.clockTick, Is.EqualTo(3));
            Assert.That(director.Snapshot.npcSocial.nextScanTick, Is.EqualTo(6));
            AssertNpcScanCreatedRealApproaches();
            Assert.That(director.Snapshot.npcSocial.randomState, Is.EqualTo(before.npcSocial.randomState));
        }

        [UnityTest]
        public IEnumerator NpcScan_LoadedFirstAndThirdSecondsKeepTheirRemainingSavedCadence()
        {
            yield return BindNpcScanWorld();
            CommitNpcScanSecond();
            var savedAtOne = director.Snapshot;
            CommitNpcScanSecond(); CommitNpcScanSecond();
            var savedAtThree = director.Snapshot;
            AssertNpcScanCreatedRealApproaches();

            // These checkpoints were produced by real trusted runtime ticks, not edited DTO fields.
            foreach (var checkpoint in new[] { savedAtOne, savedAtThree })
            {
                new EpisodeSaveStore(director.SavePath).Save(checkpoint);
                director.LoadNow();
                yield return BindNpcScanWorld();
                Assert.That(JsonUtility.ToJson(director.Snapshot), Is.EqualTo(JsonUtility.ToJson(checkpoint)));
                AssertNoNpcScanApproaches();
                long remaining = checkpoint.npcSocial.nextScanTick - checkpoint.npcSocial.clockTick;
                Assert.That(remaining, Is.EqualTo(checkpoint.npcSocial.clockTick == 1 ? 2 : 3));
                for (long index = 1; index < remaining; index++)
                {
                    CommitNpcScanSecond();
                    AssertNoNpcScanApproaches();
                }
                CommitNpcScanSecond();
                Assert.That(director.Snapshot.npcSocial.clockTick, Is.EqualTo(checkpoint.npcSocial.nextScanTick));
                AssertNpcScanCreatedRealApproaches();
            }
        }

        [UnityTest]
        public IEnumerator NpcScan_ModalObservationAndExplicitFlushDoNotAdvanceClockOrPlanRoutes()
        {
            yield return BindNpcScanWorld();
            CommitNpcScanSecond(); CommitNpcScanSecond();
            NpcWrite("npcFrameFraction", .95d); NpcWrite("npcWorldFraction", 0d);
            director.OpenSettings();
            string paused = JsonUtility.ToJson(director.Snapshot);
            for (int frame = 0; frame < 4; frame++) NpcInvoke("TickNpcSocialRuntime", .75f);
            NpcInvoke("FlushNpcWholeTicks");
            director.SaveNow(); // Saving an unchanged paused snapshot is allowed; advancing it is not.
            Assert.That(JsonUtility.ToJson(director.Snapshot), Is.EqualTo(paused));
            Assert.That(NpcRead<double>("npcFrameFraction"), Is.EqualTo(.95d));
            Assert.That(NpcRead<double>("npcWorldFraction"), Is.Zero);
            AssertNoNpcScanApproaches();

            director.ClosePanels(); NpcInvoke("SetNpcWorldPaused", false);
            NpcWrite("npcWorldFraction", .06d);
            Assert.That((bool)NpcInvoke("FlushNpcWholeTicks"), Is.True);
            Assert.That(director.Snapshot.npcSocial.clockTick, Is.EqualTo(3));
            AssertNpcScanCreatedRealApproaches();
        }

        private IEnumerator BindNpcScanWorld()
        {
            Assert.That(NpcRead<bool>("npcDiagnosticsSuspended"), Is.False,
                "Cadence tests must retain the actual world controller.");
            NpcWrite("npcApproachDiagnosticsSuppressed", true);
            NpcResetTransientFractions();
            director.OpenSettings(); // Bind actors without allowing either elapsed social time or a route scan.
            string before = JsonUtility.ToJson(director.Snapshot);
            NpcInvoke("EnsureNpcSocialWorld");
            float deadline = Time.realtimeSinceStartup + 5f;
            while (!director.NpcAutonomyReady && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(director.NpcAutonomyReady, Is.True, director.NpcAutonomyDiagnostic);
            Assert.That(JsonUtility.ToJson(director.Snapshot), Is.EqualTo(before));
            AssertNoNpcScanApproaches();
            director.ClosePanels(); NpcInvoke("SetNpcWorldPaused", false);
            NpcResetTransientFractions();
            NpcWrite("npcApproachDiagnosticsSuppressed", false);
        }

        private void CommitNpcScanSecond()
        {
            var before = director.Snapshot;
            NpcWrite("npcFrameFraction", 1d); NpcWrite("npcWorldFraction", 0d);
            Assert.That((bool)NpcInvoke("FlushNpcWholeTicks"), Is.True, director.StatusMessage);
            Assert.That(director.Snapshot.npcSocial.clockTick, Is.EqualTo(before.npcSocial.clockTick + 1));
            Assert.That(director.Snapshot.revision, Is.EqualTo(before.revision + 1));
            Assert.That(new EpisodeSaveStore(director.SavePath).TryLoad(out var disk, out var reason), Is.True, reason);
            Assert.That(JsonUtility.ToJson(disk), Is.EqualTo(JsonUtility.ToJson(director.Snapshot)));
        }

        private void AssertNoNpcScanApproaches()
        {
            Assert.That(NpcRead<IList>("npcApproaches"), Is.Empty);
            var coordinator = NpcRead<HouseMeetingCoordinator>("npcMeetings");
            if (coordinator != null) Assert.That(coordinator.LeaseCount, Is.Zero);
        }

        private void AssertNpcScanCreatedRealApproaches()
        {
            var approaches = NpcRead<IList>("npcApproaches");
            var coordinator = NpcRead<HouseMeetingCoordinator>("npcMeetings");
            Assert.That(approaches.Count, Is.GreaterThan(0).And.LessThanOrEqualTo(2),
                "The actual bound house should reserve at least one pair when a saved scan becomes due.");
            Assert.That(coordinator.LeaseCount, Is.EqualTo(approaches.Count));
            foreach (var approach in approaches)
            {
                var lease = (HouseMeetingLease)NpcObjectField(approach, "lease");
                Assert.That(coordinator.TryGetLease(lease.Token, out var owned), Is.True);
                Assert.That(owned, Is.SameAs(lease));
                Assert.That(coordinator.TryGetMotion(lease.FirstId, out var first), Is.True);
                Assert.That(coordinator.TryGetMotion(lease.SecondId, out var second), Is.True);
                Assert.That(first.LeaseId, Is.EqualTo(lease.Token));
                Assert.That(second.LeaseId, Is.EqualTo(lease.Token));
            }
        }
    }
}
