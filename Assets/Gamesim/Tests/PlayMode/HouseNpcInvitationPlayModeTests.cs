using System.Collections;
using System.Linq;
using Gamesim.House;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Gamesim.Tests.PlayMode
{
    public sealed partial class HouseNpcMotionPlayModeTests
    {
        [UnityTest]
        public IEnumerator NpcInvitation_RouteIsOwnedAndSpeechWaitsForVisiblePhysicalArrival()
        {
            // The existing fixture's third actor starts in the Kitchen, beyond speaking
            // distance from the Living-room observer. Every invitation step must then walk.
            var query = CreateNpcRoomQuery(); var cast = CreateMeetingCast(3);
            var ids = cast.Select(npc => npc.Id).ToArray(); var coordinator = CreateMeetingCoordinator(query);
            try
            {
                Assert.That(coordinator.Reconcile("invitation:1", cast, ids, ids, out var reason), Is.True, reason);
                yield return WaitForMeetingBinding(coordinator, cast);
                WarpMeetingTestPlayer(query, new Vector3(-5.5f, 0, -6));
                Vector3 playerBefore = player.transform.position, npcBefore = cast[2].transform.position;
                Assert.That(Vector3.Distance(npcBefore, playerBefore), Is.GreaterThan(5f), "The visitor starts outside speaking distance.");
                Assert.That(coordinator.TryReserveNpcInvitation(ids[2], player, out var lease, out reason), Is.True, reason);
                Assert.That(Vector3.Distance(npcBefore, lease.Destination), Is.GreaterThan(2f), "The complete route requires real movement.");
                Assert.That(coordinator.NpcInvitationCanSpeak(lease, out _), Is.False, "A new route is not an arrival.");
                Assert.That(coordinator.Reconcile("invitation:1", cast, ids, ids, out reason), Is.True, reason);
                float deadline = Time.realtimeSinceStartup + 15f;
                while (!coordinator.NpcInvitationCanSpeak(lease, out _) && Time.realtimeSinceStartup < deadline)
                { coordinator.Tick(); yield return null; }
                Assert.That(coordinator.NpcInvitationCanSpeak(lease, out var anchor), Is.True,
                    "The houseguest must walk a complete route and stop close enough to be heard.");
                Assert.That(player.transform.position, Is.EqualTo(playerBefore), "The invitation never drags the player.");
                Assert.That(Vector3.Distance(cast[2].transform.position, npcBefore), Is.GreaterThan(2f));
                Assert.That(anchor.y, Is.GreaterThan(cast[2].transform.position.y + 1f));
                coordinator.ReleaseNpcInvitation(lease);
                Assert.That(lease.Released, Is.True);
                Assert.That(cast[2].GetComponent<HouseNpcMotion>().LeaseId, Is.Null);
                Assert.That(coordinator.NpcInvitationCanSpeak(lease, out _), Is.False);
            }
            finally { coordinator.Dispose(); }
        }

        [UnityTest]
        public IEnumerator NpcInvitation_WalkingAwayReleasesTheVisitAndNeverFollowsThePlayer()
        {
            var query = CreateNpcRoomQuery(); var cast = CreateMeetingCast(2);
            var ids = cast.Select(npc => npc.Id).ToArray(); var coordinator = CreateMeetingCoordinator(query);
            try
            {
                Assert.That(coordinator.Reconcile("invitation-drift:1", cast, ids, ids, out var reason), Is.True, reason);
                yield return WaitForMeetingBinding(coordinator, cast);
                WarpMeetingTestPlayer(query, new Vector3(-5.5f, 0, -6));
                Assert.That(coordinator.TryReserveNpcInvitation(ids[1], player, out var lease, out reason), Is.True, reason);
                WarpMeetingTestPlayer(query, new Vector3(-10.5f, 0, -8.5f));
                coordinator.Tick();
                Assert.That(lease.Released, Is.True);
                Assert.That(coordinator.NpcInvitationValid(lease), Is.False);
                Assert.That(cast[1].GetComponent<HouseNpcMotion>().LeaseId, Is.Null);
            }
            finally { coordinator.Dispose(); }
        }

        [UnityTest]
        public IEnumerator NpcInvitation_OccludedSpeakerCannotDisplayWordsThroughTheWall()
        {
            var query = CreateNpcRoomQuery(); var cast = CreateMeetingCast(2);
            var ids = cast.Select(npc => npc.Id).ToArray(); var coordinator = CreateMeetingCoordinator(query);
            GameObject blocker = null;
            try
            {
                Assert.That(coordinator.Reconcile("invitation-sight:1", cast, ids, ids, out var reason), Is.True, reason);
                yield return WaitForMeetingBinding(coordinator, cast);
                WarpMeetingTestPlayer(query, new Vector3(-5.5f, 0, -6));
                Assert.That(coordinator.TryReserveNpcInvitation(ids[1], player, out var lease, out reason), Is.True, reason);
                float deadline = Time.realtimeSinceStartup + 15f;
                while (!coordinator.NpcInvitationCanSpeak(lease, out _) && Time.realtimeSinceStartup < deadline)
                { coordinator.Tick(); yield return null; }
                Assert.That(coordinator.NpcInvitationCanSpeak(lease, out _), Is.True);
                var midpoint = (player.transform.position + cast[1].transform.position) * .5f;
                blocker = MotionQueryBlocker("Invitation sight obstruction", midpoint + Vector3.up,
                    new Vector3(.24f, 2f, .24f));
                Physics.SyncTransforms();
                Assert.That(coordinator.NpcInvitationCanSpeak(lease, out _), Is.False,
                    "A physically arrived NPC still needs current visibility.");
            }
            finally { if (blocker != null) Object.Destroy(blocker); coordinator.Dispose(); }
        }

        [UnityTest]
        public IEnumerator NpcInvitation_AnotherGenerationAndDisposalReleaseOnlyOwnedMotion()
        {
            var query = CreateNpcRoomQuery(); var cast = CreateMeetingCast(2);
            var ids = cast.Select(npc => npc.Id).ToArray(); var coordinator = CreateMeetingCoordinator(query);
            try
            {
                Assert.That(coordinator.Reconcile("invitation-load:1", cast, ids, ids, out var reason), Is.True, reason);
                yield return WaitForMeetingBinding(coordinator, cast);
                WarpMeetingTestPlayer(query, new Vector3(-5.5f, 0, -6));
                Assert.That(coordinator.TryReserveNpcInvitation(ids[1], player, out var lease, out reason), Is.True, reason);
                Assert.That(coordinator.Reconcile("invitation-load:2", cast, ids, ids, out reason), Is.True, reason);
                Assert.That(lease.Released, Is.True);
                Assert.That(coordinator.NpcInvitationValid(lease), Is.False);
                yield return WaitForMeetingBinding(coordinator, cast);
                Assert.That(coordinator.TryReserveNpcInvitation(ids[1], player, out var next, out reason), Is.True, reason);
                var motion = cast[1].GetComponent<HouseNpcMotion>();
                Assert.That(motion.Release(motion.LeaseId), Is.True);
                Assert.That(motion.TryReserveAndPath("foreign-invitation-test", next.Destination), Is.True);
                var foreignState = motion.State;
                coordinator.ReleaseNpcInvitation(next);
                Assert.That(motion.LeaseId, Is.EqualTo("foreign-invitation-test"));
                Assert.That(motion.State, Is.EqualTo(foreignState), "An obsolete invitation cannot pause another motion reservation.");
                Assert.That(motion.Release("foreign-invitation-test"), Is.True);
                Assert.That(coordinator.TryReserveNpcInvitation(ids[1], player, out var final, out reason), Is.True, reason);
                coordinator.Dispose();
                Assert.That(next.Released, Is.True);
                Assert.That(final.Released, Is.True);
                Assert.That(coordinator.IsDisposed, Is.True);
            }
            finally { coordinator.Dispose(); }
        }
    }
}
