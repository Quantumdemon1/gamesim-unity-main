using System.Collections;
using System.Linq;
using Gamesim.House;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Gamesim.Tests.PlayMode
{
    public sealed partial class HouseNpcMotionPlayModeTests
    {
        [UnityTest]
        public IEnumerator Meeting_PairLeaseIsExclusiveAndDisposedSecondOwnerCannotStealMotion()
        {
            var query = CreateNpcRoomQuery();
            var cast = CreateMeetingCast(4);
            var ids = cast.Select(npc => npc.Id).ToArray();
            var coordinator = CreateMeetingCoordinator(query);
            HouseMeetingCoordinator other = null;
            try
            {
                Assert.That(coordinator.Reconcile("test-session:1", cast, ids, ids, out var reason), Is.True, reason);
                yield return WaitForMeetingBinding(coordinator, cast);
                other = CreateMeetingCoordinator(query);
                Assert.That(other.Reconcile("foreign-session:1", cast, ids, ids, out reason), Is.False);
                Assert.That(reason, Does.Contain("different coordinator"));
                other.Dispose();
                Assert.That(coordinator.IsReady, Is.True, "A failed second owner must not unbind the first owner on Dispose.");

                Assert.That(coordinator.TryReserveAtVenue("pair-a", ids[0], ids[1], "living-east-chat", out var first, out reason), Is.True, reason);
                Assert.That(coordinator.TryReserveAtVenue("pair-a", ids[0], ids[1], "living-east-chat", out var repeated, out reason), Is.True, reason);
                Assert.That(repeated, Is.SameAs(first));
                Assert.That(coordinator.TryReservePair("pair-a", ids[1], ids[0], out _, out _), Is.False, "An opaque token cannot silently change its ordered actors.");
                Assert.That(coordinator.TryReservePair("another-token", ids[0], ids[2], out _, out _), Is.False);
                Assert.That(coordinator.TryReserveAtVenue("pair-b", ids[2], ids[3], "living-east-chat", out _, out _), Is.False);
                Assert.That(coordinator.TryReserveAtVenue("pair-b", ids[2], ids[3], "private-room-chat", out _, out _), Is.False);
                Assert.That(coordinator.TryReserveAtVenue("pair-b", ids[2], ids[3], "kitchen-west-chat", out var second, out reason), Is.True, reason);
                Assert.That(coordinator.LeaseCount, Is.EqualTo(2));
                foreach (var lease in new[] { first, second })
                {
                    Assert.That(lease.RoomId, Is.Not.EqualTo("Private"));
                    foreach (var marker in MotionSceneComponents<HouseRoomMarker>())
                    {
                        Assert.That(HorizontalTestDistance(lease.FirstSlot, marker.transform.position), Is.GreaterThanOrEqualTo(1.25f));
                        Assert.That(HorizontalTestDistance(lease.SecondSlot, marker.transform.position), Is.GreaterThanOrEqualTo(1.25f));
                    }
                    Assert.That(coordinator.TryGetMotion(lease.FirstId, out var motion), Is.True);
                    Assert.That(motion.ReservedDestination, Is.EqualTo(lease.FirstSlot));
                }
                Assert.That(coordinator.Release(first), Is.True);
                Assert.That(coordinator.Release(first), Is.False);
                Assert.That(coordinator.TryGetLease("pair-b", out repeated), Is.True);
                Assert.That(repeated, Is.SameAs(second));
                // A saved bedroom pair that does not include Riley must be able
                // to reunite while the unrelated fourth actor is at Riley's
                // authored initial position. There has been no movement frame.
                Assert.That(cast[3].transform.position.x, Is.EqualTo(-7).Within(.25f));
                Assert.That(cast[3].transform.position.z, Is.EqualTo(2).Within(.25f));
                Assert.That(coordinator.TryReserveAtVenue("bedroom-reunion", ids[0], ids[1], "bedroom-south-chat", out var bedroom, out reason), Is.True, reason);
                Assert.That(bedroom.SecondSlot.z, Is.EqualTo(3.5f).Within(.25f));
                Assert.That(HorizontalTestDistance(bedroom.SecondSlot, cast[3].transform.position), Is.GreaterThan(.9f));
                foreach (var marker in MotionSceneComponents<HouseRoomMarker>())
                {
                    Assert.That(HorizontalTestDistance(bedroom.FirstSlot, marker.transform.position), Is.GreaterThanOrEqualTo(1.25f));
                    Assert.That(HorizontalTestDistance(bedroom.SecondSlot, marker.transform.position), Is.GreaterThanOrEqualTo(1.25f));
                }
            }
            finally { other?.Dispose(); coordinator.Dispose(); }
            foreach (var npc in cast)
            {
                Assert.That(npc.GetComponent<NavMeshObstacle>().enabled, Is.True);
                Assert.That(npc.GetComponent<HouseNpcMotion>().Agent.enabled, Is.False);
            }
        }

        [UnityTest]
        public IEnumerator Meeting_ActualPairArrivalSurvivesRepeatedProjectionAndHasFreshPrivacyProof()
        {
            var query = CreateNpcRoomQuery();
            var cast = CreateMeetingCast(2);
            var ids = cast.Select(npc => npc.Id).ToArray();
            var coordinator = CreateMeetingCoordinator(query);
            GameObject wall = null;
            try
            {
                Assert.That(coordinator.Reconcile("test-session:1", cast, ids, ids, out var reason), Is.True, reason);
                yield return WaitForMeetingBinding(coordinator, cast);
                var startA = cast[0].transform.position;
                var startB = cast[1].transform.position;
                Assert.That(coordinator.TryReserveAtVenue("arrive", ids[0], ids[1], "living-east-chat", out var lease, out reason), Is.True, reason);
                Assert.That(coordinator.ValidateArrivedPair(lease, out _), Is.False);
                coordinator.SetPaused(true);
                yield return null; yield return null;
                var heldA = cast[0].transform.position;
                var heldB = cast[1].transform.position;
                for (int frame = 0; frame < 5; frame++) { coordinator.SetPaused(true); coordinator.Tick(); yield return null; }
                Assert.That(Vector3.Distance(heldA, cast[0].transform.position), Is.LessThan(.03f));
                Assert.That(Vector3.Distance(heldB, cast[1].transform.position), Is.LessThan(.03f));
                Assert.That(coordinator.CanWitness(player, lease), Is.False);
                Assert.That(lease.Status, Is.EqualTo(HouseMeetingStatus.Paused));
                coordinator.SetPaused(false);
                float deadline = Time.realtimeSinceStartup + 16;
                while (!coordinator.ValidateArrivedPair(lease, out _) && Time.realtimeSinceStartup < deadline)
                {
                    Assert.That(coordinator.Reconcile("test-session:1", cast, ids, ids, out reason), Is.True, reason);
                    coordinator.SetPaused(false); coordinator.Tick();
                    yield return null;
                }
                Assert.That(coordinator.ValidateArrivedPair(lease, out reason), Is.True, reason);
                coordinator.Tick();
                Assert.That(lease.Status, Is.EqualTo(HouseMeetingStatus.Arrived));
                Assert.That(Vector3.Distance(startA, cast[0].transform.position), Is.GreaterThan(.3f));
                Assert.That(Vector3.Distance(startB, cast[1].transform.position), Is.GreaterThan(.3f));
                Assert.That(HorizontalTestDistance(cast[0].transform.position, lease.FirstSlot), Is.LessThanOrEqualTo(.25f));
                Assert.That(HorizontalTestDistance(cast[1].transform.position, lease.SecondSlot), Is.LessThanOrEqualTo(.25f));

                WarpMeetingTestPlayer(query, new Vector3(-5.5f,0,-6));
                Assert.That(coordinator.CanWitness(player, lease), Is.True, "A nearby visible pair permits a generic cue only.");
                wall = MotionQueryBlocker("Meeting witness wall", (player.transform.position + cast[0].transform.position) * .5f + Vector3.up,
                    new Vector3(.22f,2f,.22f));
                Assert.That(coordinator.CanWitness(player, lease), Is.False, "Sight is rechecked synchronously, not from a cached cue.");
                wall.SetActive(false);
                Assert.That(coordinator.CanWitness(player, lease), Is.True);
                wall.transform.position = (cast[0].transform.position + cast[1].transform.position) * .5f + Vector3.up;
                wall.SetActive(true);
                Assert.That(coordinator.ValidateArrivedPair(lease, out _), Is.False, "Start/completion must reject a newly occluded pair even if Status was Arrived.");
                wall.SetActive(false);
                Assert.That(coordinator.ValidateArrivedPair(lease, out reason), Is.True, reason);

                WarpMeetingTestPlayer(query, new Vector3(-5.5f,0,1.5f));
                Assert.That(query.TryLocate(player.transform.position, player.Agent.radius, out var room), Is.True);
                Assert.That(room, Is.EqualTo("Bedroom"));
                Assert.That(coordinator.CanWitness(player, lease), Is.False);
                // Clear of the planter centred at (-12,-8) and its baked erosion.
                WarpMeetingTestPlayer(query, new Vector3(-10.5f,0,-8.5f));
                Assert.That(query.TryLocate(player.transform.position, player.Agent.radius, out room), Is.True);
                Assert.That(room, Is.EqualTo("Living"));
                Assert.That(HorizontalTestDistance(player.transform.position,
                    (cast[0].transform.position + cast[1].transform.position) * .5f), Is.GreaterThan(6));
                Assert.That(coordinator.CanWitness(player, lease), Is.False, "Same-room observations beyond six metres are excluded.");
                coordinator.SetPaused(true);
                Assert.That(coordinator.ValidateArrivedPair(lease, out _), Is.False);
            }
            finally { if (wall != null) Object.Destroy(wall); coordinator.Dispose(); }
        }

        [UnityTest]
        public IEnumerator Meeting_StaleGenerationCannotReleaseReunionOrReuseAnImportedIdentity()
        {
            var query = CreateNpcRoomQuery();
            var cast = CreateMeetingCast(2);
            var ids = cast.Select(npc => npc.Id).ToArray();
            var coordinator = CreateMeetingCoordinator(query);
            try
            {
                Assert.That(coordinator.Reconcile("session:1", cast, ids, ids, out var reason), Is.True, reason);
                yield return WaitForMeetingBinding(coordinator, cast);
                Assert.That(coordinator.TryReservePair("reused-token", ids[0], ids[1], out var old, out reason), Is.True, reason);
                string savedVenue = old.VenueId;
                var originalAgent = cast[0].GetComponent<HouseNpcMotion>().Agent;
                cast[0].Configure("imported-first", "Imported First");
                ids[0] = "imported-first";
                Assert.That(coordinator.Reconcile("session:2", cast, ids, ids, out reason), Is.True, reason);
                Assert.That(coordinator.LeaseCount, Is.Zero);
                Assert.That(old.Status, Is.EqualTo(HouseMeetingStatus.Released));
                Assert.That(coordinator.ValidateArrivedPair(old, out _), Is.False);
                yield return WaitForMeetingBinding(coordinator, cast);
                Assert.That(cast[0].GetComponent<HouseNpcMotion>().Agent, Is.SameAs(originalAgent));
                Assert.That(cast[0].GetComponents<NavMeshAgent>(), Has.Length.EqualTo(1));
                Assert.That(coordinator.TryGetMotion("maya", out _), Is.False);
                Assert.That(coordinator.TryReserveAtVenue("reused-token", ids[0], ids[1], savedVenue, out var reunion, out reason), Is.True, reason);
                Assert.That(reunion.Generation, Is.EqualTo("session:2"));
                Assert.That(reunion.VenueId, Is.EqualTo(savedVenue));
                Assert.That(coordinator.Release(old), Is.False, "An old object cannot release the new lease even with the same opaque token.");
                Assert.That(coordinator.TryGetLease("reused-token", out var current), Is.True);
                Assert.That(current, Is.SameAs(reunion));
                Assert.That(coordinator.Reconcile("session:2", cast, ids, new[] { ids[0] }, out reason), Is.True, reason);
                Assert.That(reunion.Status, Is.EqualTo(HouseMeetingStatus.Invalid));
                Assert.That(coordinator.LeaseCount, Is.Zero);
                Assert.That(cast[1].GetComponent<NavMeshObstacle>().enabled, Is.True);
                Assert.That(cast[1].GetComponent<HouseNpcMotion>().Agent.enabled, Is.False);
            }
            finally { coordinator.Dispose(); }
        }

        [UnityTest]
        public IEnumerator Meeting_BlockedSecondSlotDoesNotReserveFirstAndPartialBindingFailureRestoresOwnedActors()
        {
            var query = CreateNpcRoomQuery();
            var cast = CreateMeetingCast(2);
            var ids = cast.Select(npc => npc.Id).ToArray();
            var coordinator = CreateMeetingCoordinator(query);
            GameObject blocker = null;
            try
            {
                var unknownBody = cast[1].gameObject.AddComponent<Rigidbody>();
                unknownBody.isKinematic = true; unknownBody.useGravity = false;
                Assert.That(coordinator.Reconcile("partial:1", cast, ids, ids, out _), Is.False);
                Assert.That(coordinator.IsReady, Is.False, "A missing motion after a partial binding failure is not a null dereference or a ready cast.");
                coordinator.Dispose();
                Assert.That(cast[0].GetComponent<NavMeshObstacle>().enabled, Is.True);
                Assert.That(cast[0].GetComponent<HouseNpcMotion>().Agent.enabled, Is.False);
                Assert.That(unknownBody != null, Is.True, "Unknown owners are preserved.");
                Object.Destroy(unknownBody);
                yield return null;
                coordinator = CreateMeetingCoordinator(query);
                Assert.That(coordinator.Reconcile("recovered:1", cast, ids, ids, out var reason), Is.True, reason);
                yield return WaitForMeetingBinding(coordinator, cast);
                blocker = MotionQueryBlocker("Occupied second meeting slot", new Vector3(-4.8f,1,-4), new Vector3(.6f,2f,.6f));
                Assert.That(coordinator.TryReserveAtVenue("blocked", ids[0], ids[1], "living-east-chat", out _, out _), Is.False);
                Assert.That(coordinator.LeaseCount, Is.Zero);
                foreach (var npc in cast) Assert.That(npc.GetComponent<HouseNpcMotion>().LeaseId, Is.Null);
                blocker.SetActive(false);
                Assert.That(coordinator.TryReserveAtVenue("now-clear", ids[0], ids[1], "living-east-chat", out _, out reason), Is.True, reason);
            }
            finally { if (blocker != null) Object.Destroy(blocker); coordinator.Dispose(); }
        }

        [UnityTest]
        public IEnumerator NpcMotion_UnboundOwnerCannotBeRevivedByReenablingItsExposedAgent()
        {
            var query = CreateNpcRoomQuery();
            var npc = MotionMaya();
            Assert.That(HouseNpcMotion.TryCreate(npc, query, NpcMotionFilter(), 0, out var motion, out var reason), Is.True, reason);
            yield return WaitForNpcBinding(motion, npc.GetComponent<NavMeshObstacle>());
            motion.Unbind();
            // External tampering occurs before carving updates. Do not ever accept
            // this agent as a valid route owner, even if Unity binds it immediately.
            motion.Agent.enabled = true;
            Assert.That(motion.IsBound, Is.False);
            Assert.That(motion.TryReserveAndPath("tampered", new Vector3(3,0,-3)), Is.False);
            Assert.That(motion.HasArrivedAt("tampered"), Is.False);
            motion.Unbind();
            Assert.That(motion.Agent.enabled, Is.False);
            Assert.That(npc.GetComponent<NavMeshObstacle>().enabled, Is.True);
        }

        private HouseNpc[] CreateMeetingCast(int count)
        {
            var first = MotionMaya();
            Assert.That(first.GetComponent<HouseNpcMotion>(), Is.Null, "Clone the static U02 fixture before binding, never clone an owned agent.");
            var cast = new HouseNpc[count]; cast[0] = first;
            var positions = new[] { new Vector3(-7,0,-7), new Vector3(3,0,-7), new Vector3(-7,0,2) };
            for (int i = 1; i < count; i++)
            {
                // Fixture-only actor placement, not a production arrival shortcut.
                var clone = Object.Instantiate(first.gameObject, positions[i-1], first.transform.rotation);
                clone.name = "Meeting test NPC " + i;
                SceneManager.MoveGameObjectToScene(clone, SceneManager.GetSceneByName(MotionScene));
                cast[i] = clone.GetComponent<HouseNpc>();
                cast[i].Configure("meeting-test-" + i, "Meeting Test " + i);
            }
            Physics.SyncTransforms();
            return cast;
        }

        private HouseMeetingCoordinator CreateMeetingCoordinator(HouseRoomQuery query)
        {
            Assert.That(HouseMeetingCoordinator.TryCreate(query, NpcMotionFilter(), out var coordinator, out var reason), Is.True, reason);
            return coordinator;
        }

        private IEnumerator WaitForMeetingBinding(HouseMeetingCoordinator coordinator, HouseNpc[] cast)
        {
            float deadline = Time.realtimeSinceStartup + 3;
            while (!coordinator.IsReady && Time.realtimeSinceStartup < deadline)
            {
                foreach (var npc in cast)
                {
                    var motion = npc.GetComponent<HouseNpcMotion>();
                    Assert.That(motion, Is.Not.Null);
                    Assert.That(motion.Agent.enabled && npc.GetComponent<NavMeshObstacle>().enabled, Is.False);
                    Assert.That(motion.State, Is.Not.EqualTo(HouseNpcMotionState.Failed), motion.FailureReason);
                }
                yield return null;
            }
            Assert.That(coordinator.IsReady, Is.True, coordinator.LastFailure);
        }

        private void WarpMeetingTestPlayer(HouseRoomQuery query, Vector3 point)
        {
            Assert.That(query.TrySampleFloor(point, player.Agent.radius, NpcMotionFilter(), .25f, out var sample, out _), Is.True, query.LastFailure);
            Assert.That(player.Agent.Warp(sample), Is.True, "Only the test observer is positioned directly; meeting NPCs must walk.");
            Physics.SyncTransforms();
        }

        private static float HorizontalTestDistance(Vector3 a, Vector3 b) => new Vector2(a.x-b.x,a.z-b.z).magnitude;
    }
}
