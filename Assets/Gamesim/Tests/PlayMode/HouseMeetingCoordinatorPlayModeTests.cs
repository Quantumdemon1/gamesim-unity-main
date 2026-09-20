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
        public IEnumerator Meeting_RemovingFirstIdentityComponentRetiresItsPairWithoutStrandingMotion()
        {
            yield return AssertMissingMeetingIdentityCleanup(0,false);
        }

        [UnityTest]
        public IEnumerator Meeting_RemovingSecondIdentityComponentStillAllowsDirectDisposal()
        {
            yield return AssertMissingMeetingIdentityCleanup(1,true);
        }

        private IEnumerator AssertMissingMeetingIdentityCleanup(int removedIndex,bool disposeDirectly)
        {
            var query=CreateNpcRoomQuery();var cast=CreateMeetingCast(2);
            var ids=cast.Select(npc=>npc.Id).ToArray();var coordinator=CreateMeetingCoordinator(query);
            try
            {
                Assert.That(coordinator.Reconcile("missing-identity:1",cast,ids,ids,out var reason),Is.True,reason);
                yield return WaitForMeetingBinding(coordinator,cast);
                Assert.That(coordinator.TryReserveAtVenue("live-pair",ids[0],ids[1],"living-east-chat",out var lease,out reason),Is.True,reason);
                var motions=cast.Select(npc=>npc.GetComponent<HouseNpcMotion>()).ToArray();
                var roots=cast.Select(npc=>npc.gameObject).ToArray();
                // Remove identity alone: the navigation owners and the partner's live route remain.
                // Destroying the whole root would hide the stale component dereference in Retire.
                Object.Destroy(cast[removedIndex]);yield return null;
                Assert.That(cast[removedIndex]==null,Is.True);
                Assert.That(roots.All(root=>root!=null && root.activeInHierarchy),Is.True);
                var partner=motions[1-removedIndex];
                Assert.That(partner.LeaseId,Is.EqualTo(lease.Token));
                if(disposeDirectly)Assert.DoesNotThrow(()=>coordinator.Dispose());
                else Assert.DoesNotThrow(()=>coordinator.Tick());
                Assert.That(lease.Status,Is.EqualTo(disposeDirectly ? HouseMeetingStatus.Released : HouseMeetingStatus.Invalid));
                Assert.That(coordinator.LeaseCount,Is.Zero);
                Assert.That(motions.All(motion=>motion.LeaseId==null),Is.True,"Both reservations must be released.");
                if(!disposeDirectly)
                {
                    Assert.That(partner.IsBound,Is.True,"The surviving actor remains available after invalidation.");
                    Assert.That(partner.Agent.hasPath,Is.False);
                    Assert.That(partner.Agent.isStopped,Is.True);
                }
                Assert.DoesNotThrow(()=>coordinator.Dispose());
                Assert.That(coordinator.IsDisposed,Is.True);
                foreach(var motion in motions)
                {
                    Assert.That(motion.Agent.enabled,Is.False);
                    Assert.That(motion.GetComponent<NavMeshObstacle>().enabled,Is.True);
                }
            }
            finally{coordinator.Dispose();}
        }

        [UnityTest]
        public IEnumerator Meeting_CompetitionStagingRespectsMeetingOwnershipAndReleasesItsRoutes()
        {
            var query=CreateNpcRoomQuery();var cast=CreateMeetingCast(2);
            var ids=cast.Select(npc=>npc.Id).ToArray();var coordinator=CreateMeetingCoordinator(query);
            try
            {
                Assert.That(coordinator.Reconcile("stage-test:1",cast,ids,ids,out var reason),Is.True,reason);
                yield return WaitForMeetingBinding(coordinator,cast);
                Assert.That(HouseInteractionAnchors.TryFind(query.Scene,"yard-south-chat",0,out var a),Is.True);
                Assert.That(HouseInteractionAnchors.TryFind(query.Scene,"yard-south-chat",1,out var b),Is.True);
                var anchors=new[]{a,b};
                Assert.That(coordinator.TryReserveAtVenue("existing-talk",ids[0],ids[1],"living-east-chat",out var meeting,out reason),Is.True,reason);
                Assert.That(coordinator.BeginCompetitionStage(ids,anchors,out _),Is.False,"Stage dressing cannot take an active meeting's actors.");
                Assert.That(cast[0].GetComponent<HouseNpcMotion>().LeaseId,Is.EqualTo(meeting.Token));
                coordinator.Release(meeting);yield return null;
                var before=cast.Select(npc=>npc.transform.position).ToArray();
                Assert.That(coordinator.BeginCompetitionStage(ids,anchors,out reason),Is.True,reason);
                Assert.That(coordinator.CompetitionStageCount,Is.EqualTo(2));
                for(int i=0;i<cast.Length;i++)
                {
                    var motion=cast[i].GetComponent<HouseNpcMotion>();
                    Assert.That(motion.LeaseId,Does.StartWith("competition:"));
                    Assert.That(Vector3.Distance(cast[i].transform.position,before[i]),Is.LessThan(.01f),"Starting a stage schedules routes without teleporting.");
                }
                coordinator.EndCompetitionStage();
                Assert.That(coordinator.HasCompetitionStage,Is.False);
                Assert.That(cast.All(npc=>npc.GetComponent<HouseNpcMotion>().LeaseId==null),Is.True);
                yield return null;
                Assert.That(coordinator.BeginCompetitionStage(ids,anchors,out reason),Is.True,reason);
                a.transform.position+=Vector3.forward*.2f;
                Assert.That(coordinator.ValidateCompetitionStage(out _),Is.False);
                Assert.That(coordinator.HasCompetitionStage,Is.False);
                Assert.That(cast.All(npc=>npc.GetComponent<HouseNpcMotion>().LeaseId==null),Is.True,"Moved stage geometry releases every reservation.");
            }
            finally{coordinator.Dispose();}
        }

        [UnityTest]
        public IEnumerator Meeting_MovingAPropInvalidatesItsLeaseAndNewReservationsUseItsAnchors()
        {
            var query=CreateNpcRoomQuery();
            var cast=CreateMeetingCast(2);
            var ids=cast.Select(npc=>npc.Id).ToArray();
            var coordinator=CreateMeetingCoordinator(query);
            var prop=new GameObject("Movable test conversation bench");
            SceneManager.MoveGameObjectToScene(prop,query.Scene);
            try
            {
                Assert.That(HouseInteractionAnchors.TryFind(query.Scene,"living-east-chat",0,out var a),Is.True);
                Assert.That(HouseInteractionAnchors.TryFind(query.Scene,"living-east-chat",1,out var b),Is.True);
                a.transform.SetParent(prop.transform,true);b.transform.SetParent(prop.transform,true);
                Assert.That(coordinator.Reconcile("anchor-test:1",cast,ids,ids,out var reason),Is.True,reason);
                yield return WaitForMeetingBinding(coordinator,cast);
                Assert.That(coordinator.TryReserveAtVenue("old-place",ids[0],ids[1],"living-east-chat",out var old,out reason),Is.True,reason);
                var before=old.FirstSlot;
                prop.transform.position+=Vector3.forward*.4f;
                coordinator.Tick();
                Assert.That(old.Status,Is.EqualTo(HouseMeetingStatus.Invalid));
                Assert.That(coordinator.LeaseCount,Is.Zero,"Moving furniture must release both navigation owners.");
                yield return null;
                Assert.That(coordinator.TryReserveAtVenue("new-place",ids[0],ids[1],"living-east-chat",out var moved,out reason),Is.True,reason);
                Assert.That(moved.VenueId,Is.EqualTo(old.VenueId),"Moving furniture keeps saved rendezvous identity.");
                Assert.That(Vector3.Distance(moved.FirstSlot,a.Position),Is.LessThan(.26f));
                Assert.That(moved.FirstSlot.z-before.z,Is.EqualTo(.4f).Within(.1f));
            }
            finally {coordinator.Dispose();Object.Destroy(prop);}
        }

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

        [UnityTest]
        public IEnumerator Meeting_ASeatedVenueBringsBothToTheTableAndFacesThemAcrossIt()
        {
            var query = CreateNpcRoomQuery();
            var cast = CreateMeetingCast(2);
            var ids = cast.Select(npc => npc.Id).ToArray();
            // The preserved prototype has no dining chairs. Author the two furniture slots in
            // this fixture explicitly; production must never invent seats in an unfurnished room.
            var north=new GameObject("Test north dining chair");
            var south=new GameObject("Test south dining chair");
            SceneManager.MoveGameObjectToScene(north,query.Scene);SceneManager.MoveGameObjectToScene(south,query.Scene);
            var firstSeat=HouseInteractionAnchor.Create(north.transform,"kitchen-table-chat","Kitchen",0,
                new Vector3(7.2f,0,-7.22f),180,true,Vector3.back*.55f);
            var secondSeat=HouseInteractionAnchor.Create(south.transform,"kitchen-table-chat","Kitchen",1,
                new Vector3(7.2f,0,-8.78f),0,true,Vector3.back*.55f);
            var coordinator = CreateMeetingCoordinator(query);
            try
            {
                Assert.That(coordinator.Reconcile("test-session:1", cast, ids, ids, out var reason), Is.True, reason);
                yield return WaitForMeetingBinding(coordinator, cast);
                Assert.That(coordinator.TryReserveAtVenue("table", ids[0], ids[1], "kitchen-table-chat", out var lease, out reason), Is.True, reason);
                Assert.That(lease.Seated, Is.True, "The long table's venue is a seated one.");
                Assert.That(lease.FirstFacing, Is.EqualTo(180f).Within(.01f), "The north chair faces south, across the table.");
                Assert.That(lease.SecondFacing, Is.EqualTo(0f).Within(.01f), "The south chair faces north.");
                // Navigation reaches each clear approach. Only presentation then enters its chair.
                Assert.That(HorizontalTestDistance(lease.FirstSlot, firstSeat.Approach), Is.LessThan(.25f));
                Assert.That(HorizontalTestDistance(lease.SecondSlot, secondSeat.Approach), Is.LessThan(.25f));
                Assert.That(HorizontalTestDistance(lease.FirstSlot,firstSeat.Position),Is.GreaterThan(.3f));
                Assert.That(HorizontalTestDistance(lease.SecondSlot,secondSeat.Position),Is.GreaterThan(.3f));
                float deadline = Time.realtimeSinceStartup + 25;
                while (!coordinator.ValidateArrivedPair(lease, out _) && Time.realtimeSinceStartup < deadline)
                { coordinator.Tick(); yield return null; }
                Assert.That(coordinator.ValidateArrivedPair(lease, out reason), Is.True, reason);
                coordinator.Tick();
                Assert.That(lease.Status, Is.EqualTo(HouseMeetingStatus.Arrived));
                Assert.That(HorizontalTestDistance(cast[0].transform.position, lease.FirstSlot), Is.LessThanOrEqualTo(.25f));
                Assert.That(HorizontalTestDistance(cast[1].transform.position, lease.SecondSlot), Is.LessThanOrEqualTo(.25f));

                // A standing venue turns the pair toward each other instead.
                Assert.That(coordinator.Release(lease), Is.True);
                Assert.That(coordinator.TryReserveAtVenue("stand", ids[0], ids[1], "living-east-chat", out var standing, out reason), Is.True, reason);
                Assert.That(standing.Seated, Is.False);
                Assert.That(Mathf.DeltaAngle(standing.FirstFacing, 90f), Is.EqualTo(0f).Within(1f), "The west slot faces east, toward the east slot.");
                Assert.That(Mathf.DeltaAngle(standing.SecondFacing, -90f), Is.EqualTo(0f).Within(1f), "The east slot faces west.");
            }
            finally { coordinator.Dispose();Object.Destroy(north);Object.Destroy(south); }
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
