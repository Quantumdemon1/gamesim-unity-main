using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Gamesim.House;
using Gamesim.Persistence;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// A saved conversation at the kitchen table reunites its pair in the chairs: both walk there,
    /// both sit once the pair has physically arrived, and each turns into its chair. This is the
    /// seated pose the plan called the most visible defect in the house - the controller had the
    /// SitDown clip and the presentation drove the parameter, and nothing in the house ever set it.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        [UnityTest]
        public IEnumerator NpcRuntime_ASavedTableMeetingReunitesSeatedAndFacingTheChairs()
        {
            var fixture = NpcPendingRuntimeFixture();
            fixture.npcSocial.pending[0].rendezvousId = "kitchen-table-chat";
            Assert.That(EpisodeValidation.TryValidate(fixture, out var why), Is.True, why);
            new EpisodeSaveStore(director.SavePath).Save(fixture);
            yield return ReloadEpisode();
            yield return WaitForNpcRuntimeBinding();

            NpcInvoke("SetNpcWorldPaused", false);
            NpcInvoke("RestorePendingNpcMeetings");
            var leases = NpcRead<Dictionary<long, HouseMeetingLease>>("npcPendingWorld");
            Assert.That(leases.TryGetValue(1, out var lease), Is.True, "The saved table meeting must reserve its two chairs.");
            Assert.That(lease.Seated, Is.True);
            var coordinator = NpcRead<HouseMeetingCoordinator>("npcMeetings");
            var bodies = SceneComponents<HouseNpc>();
            var first = bodies.Single(npc => npc.Id == lease.FirstId).GetComponent<CharacterPresentation>();
            var second = bodies.Single(npc => npc.Id == lease.SecondId).GetComponent<CharacterPresentation>();
            Assert.That(first.IsSeated || second.IsSeated, Is.False, "Nobody sits before arriving.");

            // The director ticks its own world every frame; ticking it again here would run the
            // twenty-second reunion clock at double speed and cancel the meeting mid-walk.
            float deadline = Time.realtimeSinceStartup + 40;
            while (!director.IsNpcConversationPhysicallyReady(1) && Time.realtimeSinceStartup < deadline)
            {
                Assert.That(NpcRead<bool>("npcWorldPaused"), Is.False, director.NpcAutonomyDiagnostic ?? director.StatusMessage);
                Assert.That(leases.ContainsKey(1), Is.True, "The table meeting must stay pending until the pair arrives.");
                yield return null;
            }
            Assert.That(leases.TryGetValue(1, out lease), Is.True, "The table meeting is still the director's.");
            Assert.That(coordinator.ValidateArrivedPair(lease, out var reason), Is.True, reason);
            // One world tick (a tenth of a second) carries the arrival into the presentation.
            float settled = Time.realtimeSinceStartup + 1f;
            while (!(first.IsSeated && second.IsSeated) && Time.realtimeSinceStartup < settled) yield return null;
            Assert.That(first.IsSeated && second.IsSeated, Is.True, "Both sit once the pair has arrived at the table.");
            Assert.That(first.IsTalking && second.IsTalking, Is.True, "both are in the conversation");
            Assert.That(first.IsSpeaking != second.IsSpeaking, Is.True, "and exactly one of them has the floor");
            Assert.That(first.FacingYaw, Is.EqualTo(lease.FirstFacing).Within(.01f));
            Assert.That(second.FacingYaw, Is.EqualTo(lease.SecondFacing).Within(.01f));
            Assert.That(Mathf.DeltaAngle(lease.FirstFacing, lease.SecondFacing), Is.EqualTo(180f).Within(.5f).Or.EqualTo(-180f).Within(.5f),
                "Across the table, the two chairs face each other.");

            deadline = Time.realtimeSinceStartup + 4;
            var trace = new System.Text.StringBuilder();
            var agent = first.GetComponent<UnityEngine.AI.NavMeshAgent>();
            // Both turn at the same rate from wherever their walks left them; the one that has the
            // further to turn is the one to wait for.
            while ((Mathf.Abs(Mathf.DeltaAngle(first.transform.eulerAngles.y, lease.FirstFacing)) > 3f
                    || Mathf.Abs(Mathf.DeltaAngle(second.transform.eulerAngles.y, lease.SecondFacing)) > 3f)
                && Time.realtimeSinceStartup < deadline)
            {
                if (trace.Length < 6000 && (Time.frameCount & 3) == 0)
                    trace.AppendFormat("t={0:0.00} yaw={1:0.0} facing={2} seated={3} talking={4} ready={5} pending={6} stopped={7} updRot={8} vel={9:0.00}",
                        Time.realtimeSinceStartup, first.transform.eulerAngles.y, first.FacingYaw, first.IsSeated, first.IsTalking,
                        director.IsNpcConversationPhysicallyReady(1), leases.ContainsKey(1),
                        agent != null && agent.isStopped, agent != null && agent.updateRotation, agent != null ? agent.velocity.magnitude : -1f)
                        .AppendLine();
                yield return null;
            }
            Assert.That(Mathf.DeltaAngle(first.transform.eulerAngles.y, lease.FirstFacing), Is.EqualTo(0f).Within(3f),
                "A seated body turns into its chair. " + trace);
            Assert.That(Mathf.DeltaAngle(second.transform.eulerAngles.y, lease.SecondFacing), Is.EqualTo(0f).Within(3f),
                "The other body turns into its chair too. " + trace);
        }
    }
}
