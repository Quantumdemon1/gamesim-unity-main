using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Bootstrap;
using Gamesim.House;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Gamesim.Tests.PlayMode
{
    // Uses preserved U02, not EpisodeHouse: future automatic episode scheduling
    // must not silently own NPCs before these low-level collision tests begin.
    public sealed partial class HouseNpcMotionPlayModeTests
    {
        private const string MotionScene = "HousePrototype";
        private HousePlayerController player;

        [UnitySetUp]
        public IEnumerator LoadPrimitivesHouse()
        {
            if (GamesimBootstrap.Instance != null)
            { Object.Destroy(GamesimBootstrap.Instance.gameObject); yield return null; }
            Assert.That(Application.CanStreamedLevelBeLoaded(MotionScene), Is.True);
            yield return SceneManager.LoadSceneAsync(MotionScene, LoadSceneMode.Single);
            yield return null;
            player = MotionSceneComponents<HousePlayerController>().Single();
            foreach (var interaction in MotionSceneComponents<HouseInteraction>())
            {
                // Disabling only this MonoBehaviour destroys its HUD in OnDisable,
                // but does not stop the pending Start coroutine. Deactivate its
                // separate controller object to cancel that coroutine legitimately,
                // keeping the actual player, NPC and camera wiring alive.
                Assert.That(interaction.GetComponentInChildren<HousePlayerController>(true), Is.Null);
                Assert.That(interaction.GetComponentInChildren<HouseNpc>(true), Is.Null);
                Assert.That(interaction.GetComponentInChildren<HouseCameraRig>(true), Is.Null);
                interaction.gameObject.SetActive(false);
            }
            player.SetInputEnabled(false); // Isolate native route proofs from physical or prior test input.
            yield return null;
            Assert.That(player.Agent.isOnNavMesh, Is.True);
            Assert.That(MotionSceneComponents<HouseCameraRig>().Single().isActiveAndEnabled, Is.True);
            Assert.That(MotionMaya().isActiveAndEnabled, Is.True);
        }

        [UnityTearDown]
        public IEnumerator UnloadPrimitivesHouse()
        {
            var scene = SceneManager.GetSceneByName(MotionScene);
            if (scene.IsValid() && scene.isLoaded)
            {
                var cleanup = SceneManager.CreateScene("NPC primitive test cleanup " + Guid.NewGuid().ToString("N"));
                SceneManager.SetActiveScene(cleanup);
                yield return SceneManager.UnloadSceneAsync(scene);
            }
            if (GamesimBootstrap.Instance != null) Object.Destroy(GamesimBootstrap.Instance.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator NpcRooms_UseActualFloorInteriorsAndRejectThresholdHeightAndWideSamples()
        {
            var query = CreateNpcRoomQuery();
            var filter = NpcMotionFilter();
            var points = new[]
            {
                new Vector3(-5,0,-7), new Vector3(3,0,-6), new Vector3(-5,0,2),
                new Vector3(7,0,3), new Vector3(0,0,14)
            };
            var rooms = new[] { "Living", "Kitchen", "Bedroom", "Private", "Yard" };
            for (int i = 0; i < points.Length; i++)
            {
                Assert.That(query.TryLocate(points[i], .35f, out var room), Is.True, query.LastFailure);
                Assert.That(room, Is.EqualTo(rooms[i]));
                Assert.That(query.TrySampleFloor(points[i], .35f, filter, .25f, out var sampled, out var sampleRoom), Is.True, query.LastFailure);
                Assert.That(sampleRoom, Is.EqualTo(room));
                Assert.That(Vector3.Distance(sampled, points[i]), Is.LessThanOrEqualTo(.25f));
            }
            Assert.That(query.TryLocate(new Vector3(0,0,-5), .35f, out _), Is.False, "A doorway is not the safe interior of either room.");
            Assert.That(query.TryLocate(new Vector3(-.10f,0,-5), .35f, out _), Is.False, "Radius plus inset must fit inside the real floor rectangle.");
            Assert.That(query.TryLocate(new Vector3(-5,2,-7), .35f, out _), Is.False);
            Assert.That(query.TryLocate(new Vector3(float.NaN,0,0), .35f, out _), Is.False);
            Assert.That(query.TrySampleFloor(points[0], .35f, filter, .26f, out _, out _), Is.False);
            Assert.That(query.TrySampleFloor(new Vector3(0,0,0), .35f, filter, .25f, out _, out _), Is.False);
            yield return null;
        }

        [UnityTest]
        public IEnumerator NpcRooms_LosAndClearanceRejectWallsThirdBodiesAndUnrecognizedIgnoreRoots()
        {
            var query = CreateNpcRoomQuery();
            var maya = MotionMaya();
            Assert.That(query.HasClearSight(player.transform, maya.transform), Is.True, query.LastFailure);
            Assert.That(query.HasCapsuleClearance(maya.transform.position, .35f, 1.9f, maya.transform), Is.True, query.LastFailure);
            var created = new List<GameObject>();
            try
            {
                var wall = MotionQueryBlocker("Test sight wall", (player.transform.position + maya.transform.position) * .5f + Vector3.up,
                    new Vector3(.3f,2f,.3f));
                created.Add(wall);
                Physics.SyncTransforms();
                Assert.That(query.HasClearSight(player.transform, maya.transform), Is.False);
                wall.SetActive(false);
                var occupant = MotionQueryBlocker("Test other occupant", maya.transform.position + Vector3.up,
                    new Vector3(.2f,1f,.2f));
                created.Add(occupant);
                Physics.SyncTransforms();
                Assert.That(query.HasCapsuleClearance(maya.transform.position, .35f, 1.9f, maya.transform), Is.False);
                Assert.That(query.HasCapsuleClearance(maya.transform.position, .35f, 1.9f, maya.transform, occupant.transform), Is.False,
                    "A caller cannot label arbitrary geometry as a partner and bypass occupancy.");
                occupant.SetActive(false);
                // A buffer at capacity is not proof that only the endpoints were hit.
                for (int index = 0; index < 33; index++)
                {
                    var position = Vector3.Lerp(player.transform.position, maya.transform.position, .2f + index * .018f) + Vector3.up * 1.15f;
                    created.Add(MotionQueryBlocker("Test dense ray " + index, position, Vector3.one * .025f));
                }
                Physics.SyncTransforms();
                Assert.That(query.HasClearSight(player.transform, maya.transform), Is.False);
                Assert.That(query.LastFailure, Does.Contain("overflowed"));
            }
            finally { foreach (var item in created) if (item != null) Object.Destroy(item); }
            yield return null;
        }

        [UnityTest]
        public IEnumerator NpcRooms_RejectAnOverlappingForeignHouseWithoutAdoptingItsFloor()
        {
            var query = CreateNpcRoomQuery();
            var foreign = SceneManager.CreateScene("Foreign house query test " + Guid.NewGuid().ToString("N"));
            try
            {
                var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                floor.name = "Living room floor";
                SceneManager.MoveGameObjectToScene(floor, foreign);
                floor.transform.position = new Vector3(-7,-.15f,-5);
                floor.transform.localScale = new Vector3(14,.3f,10);
                floor.AddComponent<HouseWalkable>();
                Physics.SyncTransforms();
                Assert.That(query.TryValidateScene(out var reason), Is.False);
                Assert.That(reason, Does.Contain("Another loaded house overlaps"));
                Assert.That(query.TryLocate(new Vector3(-5,0,-7), .35f, out _), Is.False);
                Assert.That(HouseRoomQuery.TryCreate(SceneManager.GetSceneByName(MotionScene), out _, out _), Is.False);
            }
            finally { if (foreign.IsValid() && foreign.isLoaded) SceneManager.UnloadSceneAsync(foreign); }
            yield return null; yield return null;
        }

        [UnityTest]
        public IEnumerator NpcMotion_BindingWaitsWithoutDualOwnersAndDisableRestoresOriginalObstacle()
        {
            var query = CreateNpcRoomQuery();
            var npc = MotionMaya();
            var originalPosition = npc.transform.position;
            var obstacle = npc.GetComponent<NavMeshObstacle>();
            var capsule = npc.GetComponent<CapsuleCollider>();
            var originalCenter = obstacle.center;
            float originalRadius = obstacle.radius, originalHeight = obstacle.height;
            bool originalCarving = obstacle.carving, originalEnabled = obstacle.enabled;
            Assert.That(HouseNpcMotion.TryCreate(npc, query, NpcMotionFilter(), 0, out var motion, out var reason), Is.True, reason);
            Assert.That(motion.State, Is.EqualTo(HouseNpcMotionState.Binding));
            Assert.That(motion.Agent.enabled, Is.False);
            Assert.That(obstacle.enabled, Is.False);
            yield return WaitForNpcBinding(motion, obstacle);
            Assert.That(Vector3.Distance(originalPosition, npc.transform.position), Is.LessThanOrEqualTo(.25f));
            Assert.That(capsule.enabled && !capsule.isTrigger, Is.True);
            Assert.That(capsule.radius, Is.EqualTo(.35f).Within(.001f));
            Assert.That(capsule.height, Is.EqualTo(1.9f).Within(.001f));
            motion.Unbind();
            Assert.That(motion.Agent.enabled, Is.False);
            Assert.That(obstacle.enabled, Is.EqualTo(originalEnabled));
            Assert.That(obstacle.carving, Is.EqualTo(originalCarving));
            Assert.That(obstacle.center, Is.EqualTo(originalCenter));
            Assert.That(obstacle.radius, Is.EqualTo(originalRadius));
            Assert.That(obstacle.height, Is.EqualTo(originalHeight));
            Assert.That(motion.BeginBinding(out reason), Is.True, reason);
            yield return WaitForNpcBinding(motion, obstacle);
            Assert.That(npc.GetComponents<NavMeshAgent>(), Has.Length.EqualTo(1));
            motion.enabled = false;
            Assert.That(motion.Agent.enabled, Is.False);
            Assert.That(obstacle.enabled, Is.EqualTo(originalEnabled));
        }

        [UnityTest]
        public IEnumerator NpcMotion_UnknownMoversAreRejectedBeforeChangingOriginalOwnership()
        {
            var npc = MotionMaya();
            var query = CreateNpcRoomQuery();
            var obstacle = npc.GetComponent<NavMeshObstacle>();
            var originalPosition = npc.transform.position;
            var body = npc.gameObject.AddComponent<Rigidbody>();
            body.isKinematic = true; body.useGravity = false;
            Assert.That(HouseNpcMotion.TryCreate(npc, query, NpcMotionFilter(), 0, out var motion, out _), Is.False);
            Assert.That(motion, Is.Null);
            Assert.That(obstacle.enabled, Is.True);
            Assert.That(npc.GetComponent<HouseNpcMotion>(), Is.Null);
            Object.Destroy(body);
            yield return null;
            // A disabled pre-existing agent also belongs to its creator, not this adapter.
            npc.gameObject.SetActive(false);
            var unknownAgent = npc.gameObject.AddComponent<NavMeshAgent>();
            unknownAgent.enabled = false;
            npc.gameObject.SetActive(true);
            Assert.That(HouseNpcMotion.TryCreate(npc, query, NpcMotionFilter(), 0, out motion, out var reason), Is.False);
            Assert.That(reason, Does.Contain("unknown ownership"));
            Assert.That(motion, Is.Null);
            Assert.That(unknownAgent.enabled, Is.False);
            Assert.That(obstacle.enabled, Is.True);
            Assert.That(npc.transform.position, Is.EqualTo(originalPosition));
            Object.Destroy(unknownAgent);
            yield return null;
        }

        [UnityTest]
        public IEnumerator NpcMotion_CompleteRoomRoutePausesAndRequiresPhysicalEndpointBeforeRelease()
        {
            var query = CreateNpcRoomQuery();
            var npc = MotionMaya();
            Assert.That(HouseNpcMotion.TryCreate(npc, query, NpcMotionFilter(), 0, out var motion, out var reason), Is.True, reason);
            yield return WaitForNpcBinding(motion, npc.GetComponent<NavMeshObstacle>());
            var start = npc.transform.position;
            var requested = new Vector3(3,0,-3);
            Assert.That(motion.TryReserveAndPath("test-cross-room", requested), Is.True, query.LastFailure);
            Assert.That(motion.TryReserveAndPath("test-cross-room", requested), Is.True, "The identical lease/path request is idempotent.");
            var reserved = motion.ReservedDestination;
            Assert.That(motion.TryReserveAndPath("another-owner", new Vector3(4,0,-3)), Is.False);
            Assert.That(motion.TryReserveAndPath("test-cross-room", new Vector3(float.NaN,0,0)), Is.False);
            Assert.That(motion.ReservedDestination, Is.EqualTo(reserved));
            Assert.That(motion.HasArrivedAt("test-cross-room"), Is.False);
            float movedDeadline = Time.realtimeSinceStartup + 4;
            while (Vector3.Distance(start, npc.transform.position) < .3f && Time.realtimeSinceStartup < movedDeadline) yield return null;
            Assert.That(Vector3.Distance(start, npc.transform.position), Is.GreaterThanOrEqualTo(.3f), "The agent must actually move along the baked route.");
            motion.SetPaused(true);
            yield return null; yield return null;
            var pausedPosition = npc.transform.position;
            for (int frame = 0; frame < 8; frame++) yield return null;
            Assert.That(Vector3.Distance(pausedPosition, npc.transform.position), Is.LessThan(.03f));
            Assert.That(motion.HasArrivedAt("test-cross-room"), Is.False);
            Assert.That(npc.GetComponent<NavMeshObstacle>().enabled, Is.False, "Pause does not recarve the actor's own feet.");
            motion.SetPaused(false);
            float arrivalDeadline = Time.realtimeSinceStartup + 14;
            while (!motion.HasArrivedAt("test-cross-room") && Time.realtimeSinceStartup < arrivalDeadline)
            {
                motion.SetPaused(false); // Deliberately repeated every coordinator tick.
                yield return null;
            }
            Assert.That(motion.HasArrivedAt("test-cross-room"), Is.True, motion.FailureReason ?? query.LastFailure);
            var offset = npc.transform.position - reserved;
            Assert.That(new Vector2(offset.x,offset.z).magnitude, Is.LessThanOrEqualTo(.25f));
            Assert.That(query.TryLocate(npc.transform.position,.35f,out var room), Is.True);
            Assert.That(room, Is.EqualTo("Kitchen"));
            Assert.That(motion.Release("another-owner"), Is.False);
            Assert.That(motion.Release("test-cross-room"), Is.True);
            Assert.That(motion.LeaseId, Is.Null);
            Assert.That(motion.Agent.isStopped, Is.True);
            Assert.That(motion.IsBound, Is.True);
            Assert.That(npc.GetComponent<NavMeshObstacle>().enabled, Is.False);
            var releasedPosition = npc.transform.position;
            for (int frame = 0; frame < 8; frame++) yield return null;
            Assert.That(motion.Agent.isStopped, Is.True);
            Assert.That(motion.Agent.hasPath, Is.False);
            Assert.That(Vector3.Distance(releasedPosition, npc.transform.position), Is.LessThan(.03f),
                "A released lease must leave a bound, stationary actor without a residual route.");
            motion.Unbind();
            Assert.That(npc.GetComponent<NavMeshObstacle>().enabled, Is.True);
        }

        [UnityTest]
        public IEnumerator NpcMotion_EmptyPathAtOriginAndStaleIdentityNeverCountAsArrival()
        {
            var query = CreateNpcRoomQuery();
            var npc = MotionMaya();
            string originalId = npc.Id, originalName = npc.DisplayName;
            Assert.That(HouseNpcMotion.TryCreate(npc, query, NpcMotionFilter(), 0, out var motion, out var reason), Is.True, reason);
            yield return WaitForNpcBinding(motion, npc.GetComponent<NavMeshObstacle>());
            Assert.That(motion.TryReserveAndPath("test-unfinished-route", new Vector3(4,0,13.5f)), Is.True);
            motion.Agent.ResetPath();
            for (int frame = 0; frame < 4; frame++) yield return null;
            Assert.That(motion.Agent.hasPath, Is.False);
            Assert.That(motion.HasArrivedAt("test-unfinished-route"), Is.False);
            npc.Configure("test-rebound-npc", originalName);
            yield return null;
            Assert.That(motion.State, Is.EqualTo(HouseNpcMotionState.Failed));
            Assert.That(motion.Agent.enabled, Is.False);
            Assert.That(npc.GetComponent<NavMeshObstacle>().enabled, Is.True);
            Assert.That(motion.LeaseId, Is.Null);
            npc.Configure(originalId, originalName);
        }

        [UnityTest]
        public IEnumerator NpcMotion_UnbakedAreaHasBoundedBindingFailureWithoutFallbackTeleport()
        {
            var query = CreateNpcRoomQuery();
            var npc = MotionMaya();
            var originalPosition = npc.transform.position;
            var filter = NpcMotionFilter();
            filter.areaMask = 1 << 30; // Existing house is baked as Walkable, not this unused area.
            Assert.That(HouseNpcMotion.TryCreate(npc, query, filter, 0, out var motion, out var reason), Is.True, reason);
            float deadline = Time.realtimeSinceStartup + 3;
            while (motion.State == HouseNpcMotionState.Binding && Time.realtimeSinceStartup < deadline)
            {
                Assert.That(motion.Agent.enabled && npc.GetComponent<NavMeshObstacle>().enabled, Is.False);
                yield return null;
            }
            Assert.That(motion.State, Is.EqualTo(HouseNpcMotionState.Failed));
            Assert.That(motion.FailureReason, Does.Contain("bounded wait"));
            Assert.That(npc.transform.position, Is.EqualTo(originalPosition));
            Assert.That(motion.Agent.enabled, Is.False);
            Assert.That(npc.GetComponent<NavMeshObstacle>().enabled, Is.True);
        }

        [UnityTest]
        public IEnumerator NpcMotion_ExplicitUnboundIdentityRebindReusesTheSameAgentAndResumesOwnership()
        {
            var query = CreateNpcRoomQuery();
            var npc = MotionMaya();
            Assert.That(HouseNpcMotion.TryCreate(npc, query, NpcMotionFilter(), 0, out var motion, out var reason), Is.True, reason);
            var obstacle = npc.GetComponent<NavMeshObstacle>();
            yield return WaitForNpcBinding(motion, obstacle);
            var ownedAgent = motion.Agent;
            Assert.That(motion.RebindIdentity(out _, 2), Is.False, "A caller must explicitly unbind before rebinding identity.");
            npc.Configure("imported-housemate", "Imported Housemate");
            yield return null;
            Assert.That(motion.State, Is.EqualTo(HouseNpcMotionState.Failed));
            Assert.That(motion.RebindIdentity(out _, 2), Is.False, "A failed owner is not an implicit rebind request.");
            motion.Unbind();
            Assert.That(motion.RebindIdentity(out reason, 2), Is.True, reason);
            Assert.That(motion.BoundNpcId, Is.EqualTo("imported-housemate"));
            Assert.That(motion.Agent, Is.SameAs(ownedAgent));
            yield return WaitForNpcBinding(motion, obstacle);
            Assert.That(motion.Agent.avoidancePriority, Is.EqualTo(62));
            Assert.That(npc.GetComponents<NavMeshAgent>(), Has.Length.EqualTo(1));
            Assert.That(npc.GetComponents<HouseNpcMotion>(), Has.Length.EqualTo(1));
            Assert.That(motion.TryReserveAndPath("rebound-owner-route", new Vector3(3,0,-3)), Is.True);
            var destination = motion.ReservedDestination;
            npc.Configure("imported-housemate", "Updated Display Name");
            motion.SetPaused(false); motion.SetPaused(false);
            yield return null;
            Assert.That(motion.IsBound, Is.True, "Matching-ID presentation refresh must not reset a route.");
            Assert.That(motion.LeaseId, Is.EqualTo("rebound-owner-route"));
            Assert.That(motion.ReservedDestination, Is.EqualTo(destination));
        }

        private HouseRoomQuery CreateNpcRoomQuery()
        {
            Assert.That(HouseRoomQuery.TryCreate(SceneManager.GetSceneByName(MotionScene), out var query, out var reason), Is.True, reason);
            return query;
        }

        private HouseNpc MotionMaya() => MotionSceneComponents<HouseNpc>().Single();
        private static T[] MotionSceneComponents<T>() where T : Component => SceneManager.GetSceneByName(MotionScene)
            .GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();
        private NavMeshQueryFilter NpcMotionFilter() => new NavMeshQueryFilter
        { agentTypeID = player.Agent.agentTypeID, areaMask = player.Agent.areaMask };

        private IEnumerator WaitForNpcBinding(HouseNpcMotion motion, NavMeshObstacle obstacle)
        {
            float deadline = Time.realtimeSinceStartup + 3;
            while (motion.State == HouseNpcMotionState.Binding && Time.realtimeSinceStartup < deadline)
            {
                Assert.That(motion.Agent.enabled && obstacle.enabled, Is.False, "Only one navigation owner may be enabled.");
                yield return null;
            }
            Assert.That(motion.IsBound, Is.True, motion.FailureReason);
            Assert.That(obstacle.enabled, Is.False);
        }

        private GameObject MotionQueryBlocker(string name, Vector3 position, Vector3 size)
        {
            var blocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            blocker.name = name;
            SceneManager.MoveGameObjectToScene(blocker, SceneManager.GetSceneByName(MotionScene));
            blocker.transform.position = position;
            blocker.transform.localScale = size;
            return blocker;
        }
    }
}
