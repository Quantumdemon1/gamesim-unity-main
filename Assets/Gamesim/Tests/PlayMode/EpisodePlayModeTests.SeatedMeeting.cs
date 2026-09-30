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

        /// <summary>
        /// Arrival does not wait on the chairs, so a table pair can be talking with a chair pose that
        /// never began. The director said "seated" for both anyway, and the one with no pose sat
        /// down in the air at the table's approach (playtest, week 2): only a pose that holds a body
        /// in a seat may seat it.
        /// </summary>
        [UnityTest]
        public IEnumerator NpcRuntime_ATablePairWhoseChairPoseCannotBeginStandsToTalk()
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
            Assert.That(leases.TryGetValue(1, out var lease) && lease.Seated, Is.True, "The saved table meeting must reserve its two chairs.");
            var bodies = SceneComponents<HouseNpc>();
            var firstBody = bodies.Single(npc => npc.Id == lease.FirstId);
            var first = firstBody.GetComponent<CharacterPresentation>();
            var second = bodies.Single(npc => npc.Id == lease.SecondId).GetComponent<CharacterPresentation>();
            // A chair pose that cannot begin: HouseSeatPresentation.Begin does nothing while disabled.
            var blocked = firstBody.GetComponent<HouseSeatPresentation>();
            if (blocked == null) blocked = firstBody.gameObject.AddComponent<HouseSeatPresentation>();
            blocked.enabled = false;

            float deadline = Time.realtimeSinceStartup + 40;
            while (!director.IsNpcConversationPhysicallyReady(1) && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(director.IsNpcConversationPhysicallyReady(1), Is.True, "The pair must reach the table. " + director.NpcAutonomyDiagnostic);
            float settled = Time.realtimeSinceStartup + 1f;
            while (!(second.IsSeated && first.IsTalking) && Time.realtimeSinceStartup < settled) yield return null;
            Assert.That(second.IsSeated && first.IsTalking, Is.True, "The pair is in its conversation, the one whose pose took in its chair.");
            Assert.That(blocked.Active, Is.False, "The blocked pose never began.");
            Assert.That(first.IsSeated, Is.False, "The one with no chair pose stands: nobody sits with no seat under them.");
        }

        /// <summary>
        /// A seated pair stays in its chairs when somebody passes close and when a panel opens and
        /// closes over it (PACK8-PASS-PLAN A2). The pair's seats were decided again every tick from
        /// the strict arrival proof; anybody within 0.7 m of a parked root fails it, and so does
        /// every unpause, which makes both bodies arrive again; and each failure stood the pair up
        /// and sat it down. A pair that has sat down now keeps its seats while its lease holds. When
        /// the lease goes, both get up at their chairs instead of being snapped to their approaches.
        /// </summary>
        [UnityTest]
        public IEnumerator NpcRuntime_ASeatedPairStaysSeatedWhenSomebodyPassesOrAPanelOpens()
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
            Assert.That(leases.TryGetValue(1, out var lease) && lease.Seated, Is.True, "The saved table meeting must reserve its two chairs.");
            var coordinator = NpcRead<HouseMeetingCoordinator>("npcMeetings");
            var bodies = SceneComponents<HouseNpc>();
            var firstBody = bodies.Single(npc => npc.Id == lease.FirstId);
            var secondBody = bodies.Single(npc => npc.Id == lease.SecondId);
            var first = firstBody.GetComponent<CharacterPresentation>();
            var second = secondBody.GetComponent<CharacterPresentation>();

            float deadline = Time.realtimeSinceStartup + 40;
            while (!director.IsNpcConversationPhysicallyReady(1) && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(director.IsNpcConversationPhysicallyReady(1), Is.True, "The pair must reach the table. " + director.NpcAutonomyDiagnostic);
            HouseSeatPresentation SeatOf(HouseNpc npc) => npc.GetComponent<HouseSeatPresentation>();
            bool Sitting(HouseNpc npc, CharacterPresentation visual) =>
                SeatOf(npc) != null && SeatOf(npc).Active && !SeatOf(npc).IsExiting && visual.IsSeated;
            float settled = Time.realtimeSinceStartup + 4f;
            while (!(Sitting(firstBody, first) && SeatOf(firstBody).Settled && Sitting(secondBody, second) && SeatOf(secondBody).Settled)
                   && Time.realtimeSinceStartup < settled) yield return null;
            Assert.That(Sitting(firstBody, first) && Sitting(secondBody, second), Is.True, "Both sit once the pair has arrived at the table.");

            var broken = new List<string>();
            void Check(string moment)
            {
                foreach (var (npc, visual) in new[] { (firstBody, first), (secondBody, second) })
                    if (!Sitting(npc, visual) && broken.Count < 8)
                        broken.Add(npc.Id + " " + moment + ": " + (SeatOf(npc) == null || !SeatOf(npc).Active ? "the seat ended"
                            : SeatOf(npc).IsExiting ? "getting up" : "not seated"));
            }
            IEnumerator Hold(float seconds, string moment)
            {
                float until = Time.realtimeSinceStartup + seconds;
                while (Time.realtimeSinceStartup < until) { yield return null; Check(moment); }
            }

            // Somebody passes close: a body half a metre behind the first's parked root, on the far
            // side from the table, which fails the pair's proof for as long as it stands there.
            var away = firstBody.transform.position - secondBody.transform.position;
            away.y = 0f;
            var passer = new GameObject("Passer-by", typeof(CapsuleCollider));
            var capsule = passer.GetComponent<CapsuleCollider>();
            capsule.radius = .35f; capsule.height = 1.8f; capsule.center = Vector3.up * .9f;
            passer.transform.position = firstBody.transform.position + away.normalized * .5f;
            Physics.SyncTransforms();
            bool proofFailed = false;
            float passing = Time.realtimeSinceStartup + .6f;
            while (Time.realtimeSinceStartup < passing)
            {
                yield return null;
                proofFailed |= !director.IsNpcConversationPhysicallyReady(1);
                Check("with somebody passing");
            }
            Object.Destroy(passer);
            Assert.That(proofFailed, Is.True, "Somebody half a metre from a parked root fails the pair's strict proof; that is the case asked about.");
            yield return Hold(.6f, "after they passed");
            Assert.That(director.IsNpcConversationPhysicallyReady(1), Is.True, "The proof holds again once they have gone.");

            // A panel over them pauses the house, and closing it makes both bodies arrive again.
            director.OpenJournal();
            yield return null;
            Check("under the notebook");
            // The house's first world tick after the close is put on the frame it resumes, while both
            // bodies are still arriving again (two motion frames), so the tick that decides the seats
            // sees the pair's proof fail. Left to the clock, a batchmode frame is so short that the
            // tick came after the bodies had arrived, and this half passed without the fix. A whole
            // tenth of a second is due: the paused house adds nothing to the fraction, and closing
            // the panel does not clear it, so the resuming frame ticks whatever its delta.
            NpcWrite("npcWorldFraction", .1d);
            director.ClosePanels();
            yield return null;
            bool resumedOnATick = NpcRead<double>("npcWorldFraction") < .1d;
            bool resumedUnproved = !director.IsNpcConversationPhysicallyReady(1);
            Check("as the notebook closed");
            Assert.That(resumedOnATick && resumedUnproved, Is.True,
                "The house resumed on a world tick while the pair was still arriving again; that is the case asked about. "
                + "Ticked: " + resumedOnATick + ", proof failing: " + resumedUnproved + ".");
            yield return Hold(.6f, "after the notebook closed");
            Assert.That(broken, Is.Empty, "The pair stood up and sat down again:\n" + string.Join("\n", broken));

            // The lease goes, as a finished conversation's does: both get up at their chairs, which
            // reduced motion would make a cut.
            first.SetReducedMotion(false); second.SetReducedMotion(false);
            Assert.That(coordinator.Release(lease), Is.True, "The table meeting's lease is released.");
            yield return null;
            Assert.That(SeatOf(firstBody).IsExiting && SeatOf(secondBody).IsExiting, Is.True,
                "Both get up at their chairs: the stand-up outlives the lease that held them.");
        }
    }
}
