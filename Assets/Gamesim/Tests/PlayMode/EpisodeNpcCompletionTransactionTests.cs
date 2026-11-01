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
        public IEnumerator NpcRuntime_RealArrivedCompletionInstallsOnceAndAFailedWriteRetriesTheSameState()
        {
            // Let the actual scheduler/path planner create a physical start.
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
            // The house's seconds reach disk in batches, so the wait can end with no save yet, or without its
            // backup: two explicit saves write both, and leave nothing owed, before the files are read.
            director.SaveNow(); director.SaveNow();
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
                Assert.That((bool)NpcInvoke("FlushNpcWholeTicks"), Is.True, director.StatusMessage);
                Assert.That(writes, Is.Zero, "The completion is installed and written with the house's next write.");
                var completed = director.Snapshot;
                Assert.That(completed.npcSocial.pending.Count, Is.LessThan(before.npcSocial.pending.Count));
                Assert.That(completed.npcSocial.randomState, Is.Not.EqualTo(before.npcSocial.randomState));
                Assert.That(completed.randomState, Is.EqualTo(before.randomState));
                CollectionAssert.AreEqual(primary, File.ReadAllBytes(director.SavePath));
                CollectionAssert.AreEqual(backup, File.ReadAllBytes(director.SavePath + ".backup"));

                director.SaveNow();
                Assert.That(writes, Is.EqualTo(1));
                Assert.That(attempted, Is.Not.Null);
                AssertEquivalent(completed, attempted);
                AssertEquivalent(completed, director.Snapshot);
                CollectionAssert.AreEqual(primary, File.ReadAllBytes(director.SavePath));
                CollectionAssert.AreEqual(backup, File.ReadAllBytes(director.SavePath + ".backup"));
                Assert.That(NpcRead<bool>("npcSaveSuspended"), Is.True);
                Assert.That(director.ObservedNpcConversation, Is.Empty);

                NpcWrite("saveCandidateForDiagnostics", null);
                director.SaveNow(); // The explicit retry writes exactly the session memory holds and releases suspension.
                Assert.That(NpcRead<bool>("npcSaveSuspended"), Is.False);
                Assert.That(new EpisodeSaveStore(director.SavePath).TryLoad(out var saved, out var reason), Is.True, reason);
                AssertEquivalent(attempted, saved);
                AssertEquivalent(attempted, director.Snapshot);
                NpcWrite("npcFrameFraction", .2d); NpcWrite("npcWorldFraction", 0d);
                Assert.That((bool)NpcInvoke("FlushNpcWholeTicks"), Is.True);
                AssertEquivalent(attempted, director.Snapshot); // No duplicated completion, draw or relationship effect.
            }
            finally { NpcWrite("saveCandidateForDiagnostics", null); }
        }
    }
}
