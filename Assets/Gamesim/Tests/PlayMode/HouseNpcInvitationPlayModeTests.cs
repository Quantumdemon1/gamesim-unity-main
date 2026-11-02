using System.Collections;
using System.Linq;
using Gamesim.House;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
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

        // ---------------------------------------------------------------- the walk-over holds (OFFERS-COME-TO-YOU-PLAN slice 3)

        /// <summary>
        /// A visit that answers from arrival walks on a budget of its own route: the route at walking pace, half again and
        /// 3 s to spare, between 6 s and 30 s. The houseguest across the house gets more than the one beside the player,
        /// and the visit as it always was keeps its lifetime from dispatch, whatever the route. All in one frame: nobody
        /// walks, each visit let go as it is read.
        /// </summary>
        [UnityTest]
        public IEnumerator NpcInvitation_AVisitAnsweredFromArrivalWalksOnItsRoutesBudget()
        {
            var query = CreateNpcRoomQuery(); var cast = CreateMeetingCast(3);
            var ids = cast.Select(npc => npc.Id).ToArray(); var coordinator = CreateMeetingCoordinator(query);
            try
            {
                Assert.That(coordinator.Reconcile("invitation-budget:1", cast, ids, ids, out var reason), Is.True, reason);
                yield return WaitForMeetingBinding(coordinator, cast);
                WarpMeetingTestPlayer(query, new Vector3(-5.5f, 0, -6));
                float far = NpcInvitationWindow(coordinator, cast[2], true, out float farRoute);
                float near = NpcInvitationWindow(coordinator, cast[1], true, out float nearRoute);
                float sent = NpcInvitationWindow(coordinator, cast[2], false, out _);

                float Budget(float route) => Mathf.Clamp(route / HouseNpcMotion.DefaultSpeed * HouseMeetingCoordinator.NpcInvitationTravelFactor
                    + HouseMeetingCoordinator.NpcInvitationTravelSlack, HouseMeetingCoordinator.NpcInvitationTravelMin, HouseMeetingCoordinator.NpcInvitationTravelMax);
                Assert.That(farRoute, Is.GreaterThan(nearRoute + 2f), "The Kitchen's houseguest has the longer walk: " + farRoute + " m against " + nearRoute + " m.");
                Assert.That(far, Is.EqualTo(Budget(farRoute)).Within(.01f), "The walk across the house is budgeted by its own route (" + farRoute + " m).");
                Assert.That(near, Is.EqualTo(Budget(nearRoute)).Within(.01f), "and so is the step beside the player (" + nearRoute + " m).");
                Assert.That(far, Is.GreaterThan(near), "A longer route, a longer budget.");
                Assert.That(far, Is.GreaterThan(farRoute / HouseNpcMotion.DefaultSpeed), "The walk fits its budget.");
                Assert.That(sent, Is.EqualTo(HouseMeetingCoordinator.NpcInvitationLifetime).Within(.001f),
                    "A visit that does not answer from arrival lasts its lifetime from dispatch, as it always did.");
            }
            finally { coordinator.Dispose(); }
        }

        /// <summary>
        /// A visit that answers from arrival: on its way, its travel budget stands; the moment the houseguest arrives, its
        /// lifetime starts, once; and past the travel budget the arrived visit stands. The visit as it always was keeps the
        /// lifetime it was dispatched with when its houseguest arrives.
        /// </summary>
        [UnityTest]
        public IEnumerator NpcInvitation_AVisitAnsweredFromArrivalRunsItsLifetimeFromArrival()
        {
            var query = CreateNpcRoomQuery(); var cast = CreateMeetingCast(3);
            var ids = cast.Select(npc => npc.Id).ToArray(); var coordinator = CreateMeetingCoordinator(query);
            try
            {
                Assert.That(coordinator.Reconcile("invitation-arrival:1", cast, ids, ids, out var reason), Is.True, reason);
                yield return WaitForMeetingBinding(coordinator, cast);
                WarpMeetingTestPlayer(query, new Vector3(-5.5f, 0, -6));
                var motion = cast[2].GetComponent<HouseNpcMotion>();
                Assert.That(coordinator.TryReserveNpcInvitation(ids[2], player, true, out var lease, out reason, out _), Is.True, reason);
                float travel = coordinator.NpcInvitationExpiresAt(lease);
                float arrivedAt = float.NaN;
                float deadline = Time.realtimeSinceStartup + 15f;
                while (Time.realtimeSinceStartup < deadline)
                {
                    coordinator.Tick();
                    if (motion.HasArrivedAt(motion.LeaseId)) { arrivedAt = Time.unscaledTime; break; }
                    Assert.That(lease.Released, Is.False, "The visit lapsed on its way: " + motion.ArrivalFailure + ", " + motion.LastRouteFailure);
                    Assert.That(coordinator.NpcInvitationExpiresAt(lease), Is.EqualTo(travel), "On the way, the travel budget stands.");
                    yield return null;
                }
                Assert.That(arrivedAt, Is.Not.NaN, "The houseguest walks the route and arrives: " + motion.ArrivalFailure + ", " + motion.LastRouteFailure);
                Assert.That(coordinator.NpcInvitationExpiresAt(lease),
                    Is.EqualTo(arrivedAt + HouseMeetingCoordinator.NpcInvitationLifetime).Within(.001f), "The time to answer starts at arrival.");
                Assert.That(coordinator.NpcInvitationExpiresAt(lease), Is.GreaterThan(travel));

                deadline = Time.realtimeSinceStartup + 35f;
                while (Time.unscaledTime < travel + .25f && Time.realtimeSinceStartup < deadline) { coordinator.Tick(); yield return null; }
                Assert.That(Time.unscaledTime, Is.GreaterThan(travel), "Waited past the travel budget.");
                Assert.That(coordinator.NpcInvitationValid(lease), Is.True, "Past its travel budget the arrived visit stands: the window runs from arrival.");
                Assert.That(lease.Released, Is.False);
                Assert.That(coordinator.NpcInvitationExpiresAt(lease),
                    Is.EqualTo(arrivedAt + HouseMeetingCoordinator.NpcInvitationLifetime).Within(.001f), "Once: standing there restarts nothing.");
                coordinator.ReleaseNpcInvitation(lease);

                // The visit as it always was: its lifetime from dispatch, arrival or not.
                var nearMotion = cast[1].GetComponent<HouseNpcMotion>();
                float sentAt = Time.unscaledTime;
                Assert.That(coordinator.TryReserveNpcInvitation(ids[1], player, out var legacy, out reason), Is.True, reason);
                float fixedEnd = coordinator.NpcInvitationExpiresAt(legacy);
                Assert.That(fixedEnd - sentAt, Is.EqualTo(HouseMeetingCoordinator.NpcInvitationLifetime).Within(.001f));
                deadline = Time.realtimeSinceStartup + 15f;
                bool arrived = false;
                while (!arrived && Time.realtimeSinceStartup < deadline)
                {
                    coordinator.Tick();
                    arrived = nearMotion.HasArrivedAt(nearMotion.LeaseId);
                    if (!arrived) yield return null;
                }
                Assert.That(arrived, Is.True, "The second houseguest arrives too: " + nearMotion.ArrivalFailure);
                coordinator.Tick();
                Assert.That(coordinator.NpcInvitationValid(legacy), Is.True);
                Assert.That(coordinator.NpcInvitationExpiresAt(legacy), Is.EqualTo(fixedEnd), "Without it, arriving restarts nothing.");
                coordinator.ReleaseNpcInvitation(legacy);
            }
            finally { coordinator.Dispose(); }
        }

        /// <summary>
        /// A visit answered from arrival still never follows the player: arrived, and the player walks away, it ends at the
        /// coordinator's next tick and the houseguest's motion is let go.
        /// </summary>
        [UnityTest]
        public IEnumerator NpcInvitation_AnArrivedVisitStillEndsWhenThePlayerWalksAway()
        {
            var query = CreateNpcRoomQuery(); var cast = CreateMeetingCast(2);
            var ids = cast.Select(npc => npc.Id).ToArray(); var coordinator = CreateMeetingCoordinator(query);
            try
            {
                Assert.That(coordinator.Reconcile("invitation-arrival-drift:1", cast, ids, ids, out var reason), Is.True, reason);
                yield return WaitForMeetingBinding(coordinator, cast);
                WarpMeetingTestPlayer(query, new Vector3(-5.5f, 0, -6));
                Assert.That(coordinator.TryReserveNpcInvitation(ids[1], player, true, out var lease, out reason, out _), Is.True, reason);
                float deadline = Time.realtimeSinceStartup + 15f;
                while (!coordinator.NpcInvitationCanSpeak(lease, out _) && Time.realtimeSinceStartup < deadline)
                { coordinator.Tick(); yield return null; }
                Assert.That(coordinator.NpcInvitationCanSpeak(lease, out _), Is.True, "The houseguest arrives beside the player.");
                Assert.That(coordinator.NpcInvitationExpiresAt(lease) - Time.unscaledTime,
                    Is.GreaterThan(HouseMeetingCoordinator.NpcInvitationTravelMin), "Arrived: the time to answer is the lifetime, from arrival.");
                WarpMeetingTestPlayer(query, new Vector3(-10.5f, 0, -8.5f));
                coordinator.Tick();
                Assert.That(lease.Released, Is.True, "Walking away ends it: the lease never follows the player.");
                Assert.That(coordinator.NpcInvitationValid(lease), Is.False);
                Assert.That(coordinator.NpcInvitationExpiresAt(lease), Is.NaN, "No visit is in flight.");
                Assert.That(cast[1].GetComponent<HouseNpcMotion>().LeaseId, Is.Null, "and the houseguest's motion is let go.");
            }
            finally { coordinator.Dispose(); }
        }

        /// <summary>
        /// Reserves a visit from this houseguest and lets it go in the same frame: how long it was given, in unscaled
        /// seconds, and the route to its point, measured as the reservation measures it - the houseguest's own agent,
        /// from where they stand.
        /// </summary>
        private float NpcInvitationWindow(HouseMeetingCoordinator coordinator, HouseNpc npc, bool answerFromArrival, out float route)
        {
            float now = Time.unscaledTime;
            Assert.That(coordinator.TryReserveNpcInvitation(npc.Id, player, answerFromArrival, out var lease, out var reason, out _), Is.True, reason);
            var agent = npc.GetComponent<HouseNpcMotion>().Agent;
            var path = new NavMeshPath();
            Assert.That(agent.CalculatePath(lease.Destination, path) && path.status == NavMeshPathStatus.PathComplete, Is.True,
                npc.DisplayName + " has a complete route to the visit's point.");
            var corners = path.corners;
            route = 0f;
            for (int i = 0; i < corners.Length; i++) route += Vector3.Distance(i == 0 ? agent.transform.position : corners[i - 1], corners[i]);
            float window = coordinator.NpcInvitationExpiresAt(lease) - now;
            coordinator.ReleaseNpcInvitation(lease);
            Assert.That(lease.Released, Is.True);
            return window;
        }
    }
}
