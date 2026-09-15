using System;
using System.Collections;
using System.IO;
using System.Linq;
using Gamesim.Persistence;
using Gamesim.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    public sealed partial class EpisodePlayModeTests
    {
        [UnityTest]
        public IEnumerator NpcRuntime_RealArrivedCompletionFailureKeepsPendingAndRetryUsesIdenticalRandomEffects()
        {
            // Let the actual scheduler/path planner create a durable physical start.
            float deadline = Time.realtimeSinceStartup + 65;
            while ((director.Snapshot.npcSocial.pending.Count == 0
                || !director.Snapshot.npcSocial.pending.All(pending => director.IsNpcConversationPhysicallyReady(pending.sequence)))
                && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(director.Snapshot.npcSocial.pending.Count, Is.GreaterThan(0), director.NpcAutonomyDiagnostic);
            Assert.That(director.Snapshot.npcSocial.pending.All(pending => director.IsNpcConversationPhysicallyReady(pending.sequence)), Is.True);
            NpcWrite("npcApproachDiagnosticsSuppressed", true);
            NpcWrite("npcWorldFraction", 0d);
            var started = director.Snapshot;
            long untilFirstCompletion = started.npcSocial.pending.Min(pending =>
                (long)Math.Ceiling(pending.durationMs / 1000d) - (started.npcSocial.clockTick - pending.startedTick));
            Assert.That(untilFirstCompletion, Is.GreaterThanOrEqualTo(1));
            // Only transient elapsed time is injected; every operation still requires
            // the real currently arrived leases. No authoritative pending state/proof is forged.
            NpcWrite("npcFrameFraction", (double)(untilFirstCompletion - 1));
            Assert.That((bool)NpcInvoke("FlushNpcWholeTicks"), Is.True);
            var before = director.Snapshot;
            var primary = File.ReadAllBytes(director.SavePath);
            var backup = File.ReadAllBytes(director.SavePath + ".backup");
            EpisodeState attempted = null;
            int writes = 0;
            NpcWrite("saveCandidateForDiagnostics", (Action<EpisodeState>)(candidate =>
            {
                writes++; attempted = candidate.Clone();
                throw new IOException("Injected actual arrived completion write failure.");
            }));
            try
            {
                NpcWrite("npcFrameFraction", 1.2d);
                Assert.That((bool)NpcInvoke("FlushNpcWholeTicks"), Is.False);
                Assert.That(writes, Is.EqualTo(1));
                Assert.That(attempted, Is.Not.Null);
                Assert.That(attempted.npcSocial.pending.Count, Is.LessThan(before.npcSocial.pending.Count));
                Assert.That(attempted.npcSocial.randomState, Is.Not.EqualTo(before.npcSocial.randomState));
                Assert.That(attempted.randomState, Is.EqualTo(before.randomState));
                AssertEquivalent(before, director.Snapshot);
                CollectionAssert.AreEqual(primary, File.ReadAllBytes(director.SavePath));
                CollectionAssert.AreEqual(backup, File.ReadAllBytes(director.SavePath + ".backup"));
                Assert.That(NpcRead<bool>("npcSaveSuspended"), Is.True);
                Assert.That(director.ObservedNpcConversation, Is.Empty);

                NpcWrite("saveCandidateForDiagnostics", null);
                director.SaveNow(); // Explicit unchanged-session durability retry releases suspension.
                Assert.That(NpcRead<bool>("npcSaveSuspended"), Is.False);
                NpcInvoke("SetNpcWorldPaused", false);
                // Arrival needs two fresh world frames after pause. Prevent logical
                // time while those frames pass by holding the pending elapsed fraction.
                NpcWrite("npcFrameFraction", 0d);
                deadline = Time.realtimeSinceStartup + 5;
                while (!director.Snapshot.npcSocial.pending.All(pending => director.IsNpcConversationPhysicallyReady(pending.sequence))
                    && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(director.Snapshot.npcSocial.pending.All(pending => director.IsNpcConversationPhysicallyReady(pending.sequence)), Is.True);
                AssertEquivalent(before, director.Snapshot);
                NpcWrite("npcFrameFraction", 1.2d); NpcWrite("npcWorldFraction", 0d);
                Assert.That((bool)NpcInvoke("FlushNpcWholeTicks"), Is.True);
                AssertEquivalent(attempted, director.Snapshot);
                Assert.That(new EpisodeSaveStore(director.SavePath).TryLoad(out var saved, out var reason), Is.True, reason);
                AssertEquivalent(attempted, saved);
                Assert.That((bool)NpcInvoke("FlushNpcWholeTicks"), Is.True);
                AssertEquivalent(attempted, director.Snapshot); // No duplicated completion, draw or relationship effect.
            }
            finally { NpcWrite("saveCandidateForDiagnostics", null); }
        }
    }
}
