using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace Gamesim.House
{
    /// <summary>
    /// Read-only room/floor/visibility queries for the house.
    /// Markers are destinations, never room volumes. No simulation or save dependency.
    ///
    /// <para>The floor set is a closed list on purpose: an NPC standing somewhere this class cannot
    /// name is a position the rest of the system cannot reason about, so an unrecognised floor is a
    /// hard failure rather than a shrug.</para>
    ///
    /// <para>The first five are required and the south wing is optional, because two houses use this
    /// class. <c>EpisodeHouse</c> has eight rooms; <c>HousePrototype</c> — which the NPC motion suite
    /// loads, and which is the U02 reference scene — still has five. Demanding all eight would have
    /// meant the query worked in the shipping scene and failed in the one the tests run against,
    /// which is the wrong way round for something this load-bearing.</para>
    /// </summary>
    public sealed class HouseRoomQuery
    {
        private sealed class Floor
        {
            public string roomId;
            public BoxCollider collider;
        }

        private static readonly string[] FloorNames =
        {
            "Living room floor", "Kitchen floor", "Bedroom floor", "Private room floor", "Competition yard floor",
            "HoH floor", "Nomination floor", "Games floor",
        };
        private static readonly string[] RoomIds =
        {
            "Living", "Kitchen", "Bedroom", "Private", "Yard",
            "HoH", "Nomination", "Games",
        };
        private readonly Scene scene;
        /// <summary>Floors present in this scene, in table order. Optional rooms may be absent.</summary>
        private readonly List<Floor> floors = new List<Floor>(FloorNames.Length);
        /// <summary>How many of <see cref="FloorNames"/> every house must have.</summary>
        private const int RequiredFloors = 5;
        private readonly List<GameObject> rootBuffer = new List<GameObject>(64);
        private readonly List<HouseWalkable> walkableBuffer = new List<HouseWalkable>(8);
        private readonly RaycastHit[] sightHits = new RaycastHit[32];
        private readonly Collider[] overlapHits = new Collider[64];

        private HouseRoomQuery(Scene ownedScene) { scene = ownedScene; }
        public Scene Scene => scene;
        public string LastFailure { get; private set; }

        public static bool TryCreate(Scene ownedScene, out HouseRoomQuery query, out string reason)
        {
            query = null;
            reason = null;
            if (!ownedScene.IsValid() || !ownedScene.isLoaded)
            { reason = "The house scene is not loaded."; return false; }
            var candidate = new HouseRoomQuery(ownedScene);
            ownedScene.GetRootGameObjects(candidate.rootBuffer);
            var seen = new bool[FloorNames.Length];
            foreach (var root in candidate.rootBuffer)
            {
                candidate.walkableBuffer.Clear();
                root.GetComponentsInChildren(true, candidate.walkableBuffer);
                foreach (var marker in candidate.walkableBuffer)
                {
                    int index = Array.IndexOf(FloorNames, marker.gameObject.name);
                    var collider = marker.GetComponent<BoxCollider>();
                    if (index < 0 || collider == null || seen[index])
                    {
                        reason = "Expected uniquely named BoxCollider house floors; found an unknown or duplicate floor: "
                            + marker.gameObject.name + ".";
                        return false;
                    }
                    seen[index] = true;
                    candidate.floors.Add(new Floor { roomId = RoomIds[index], collider = collider });
                }
            }

            // Table order, so callers that enumerate floors see rooms in a stable sequence rather
            // than in whatever order the scene roots happened to be walked.
            candidate.floors.Sort((a, b) =>
                Array.IndexOf(RoomIds, a.roomId).CompareTo(Array.IndexOf(RoomIds, b.roomId)));

            for (int i = 0; i < RequiredFloors; i++)
            {
                if (seen[i]) continue;
                reason = "The house is missing its " + FloorNames[i] + ".";
                return false;
            }
            if (!candidate.TryValidateScene(out reason)) return false;
            query = candidate;
            return true;
        }

        public bool TryValidateScene(out string reason)
        {
            reason = null;
            if (!scene.IsValid() || !scene.isLoaded)
                return Fail(out reason, "The bound house scene is no longer loaded.");
            foreach (var floor in floors)
            {
                var collider = floor?.collider;
                if (collider == null || collider.gameObject.scene != scene || !collider.enabled
                    || collider.isTrigger || !collider.gameObject.activeInHierarchy
                    || !Finite(collider.size) || collider.size.x <= 0 || collider.size.y <= 0 || collider.size.z <= 0
                    || !Finite(collider.transform.lossyScale) || collider.transform.lossyScale.x <= 0
                    || collider.transform.lossyScale.y <= 0 || collider.transform.lossyScale.z <= 0
                    || Vector3.Dot(collider.transform.up, Vector3.up) < .999f)
                    return Fail(out reason, "A bound floor is missing, disabled, tilted, mirrored, or otherwise unsupported.");
            }
            // NavMesh's global query API cannot identify its owning data instance.
            // Refuse overlapping second houses instead of sampling their geometry by accident.
            for (int index = 0; index < SceneManager.sceneCount; index++)
            {
                var other = SceneManager.GetSceneAt(index);
                if (other == scene || !other.isLoaded) continue;
                rootBuffer.Clear();
                other.GetRootGameObjects(rootBuffer);
                foreach (var root in rootBuffer)
                {
                    walkableBuffer.Clear();
                    root.GetComponentsInChildren(true, walkableBuffer);
                    foreach (var marker in walkableBuffer)
                    {
                        var otherFloor = marker.GetComponent<Collider>();
                        if (otherFloor == null || !otherFloor.enabled || !otherFloor.gameObject.activeInHierarchy) continue;
                        foreach (var floor in floors)
                            if (floor.collider.bounds.Intersects(otherFloor.bounds))
                                return Fail(out reason, "Another loaded house overlaps the bound floor geometry.");
                    }
                }
            }
            LastFailure = null;
            return true;
        }

        public bool TryLocate(Vector3 feet, float bodyRadius, out string roomId)
        {
            roomId = null;
            Physics.SyncTransforms(); // Queries must not accept stale moved floor/body geometry.
            if (!TryFindFloor(feet, bodyRadius, out var floor)) return false;
            roomId = floor.roomId;
            return true;
        }

        public bool TrySampleFloor(Vector3 requested, float bodyRadius, NavMeshQueryFilter filter,
            float maximumAdjustment, out Vector3 sampled, out string roomId)
        {
            sampled = default;
            roomId = null;
            if (filter.agentTypeID < 0 || NavMesh.GetSettingsByID(filter.agentTypeID).agentTypeID == -1
                || filter.areaMask == 0 || !Finite(maximumAdjustment)
                || maximumAdjustment <= 0 || maximumAdjustment > .25f)
                return Fail("Floor sampling permits at most a .25 m adjustment.");
            if (!TryLocate(requested, bodyRadius, out var requestedRoom)) return false;
            if (!NavMesh.SamplePosition(requested, out var hit, maximumAdjustment, filter)
                || !Finite(hit.position) || Vector3.Distance(requested, hit.position) > maximumAdjustment
                || Mathf.Abs(requested.y - hit.position.y) > .20f
                || !TryLocate(hit.position, bodyRadius, out var sampledRoom) || sampledRoom != requestedRoom)
                return Fail("No compatible NavMesh point on the same bound floor within the allowed adjustment.");
            sampled = hit.position;
            roomId = sampledRoom;
            LastFailure = null;
            return true;
        }

        public bool HasClearSight(Transform from, Transform to, float torsoHeight = 1.15f)
        {
            Physics.SyncTransforms();
            if (!TryValidateScene(out _) || !LocalActor(from) || !LocalActor(to) || from == to
                || !Finite(torsoHeight) || torsoHeight <= 0 || torsoHeight > 2.5f)
                return Fail("Sight requires two distinct active local actors and a valid torso height.");
            var origin = from.position + Vector3.up * torsoHeight;
            var offset = to.position + Vector3.up * torsoHeight - origin;
            float distance = offset.magnitude;
            if (!Finite(origin) || !Finite(offset) || distance < .01f)
                return Fail("Sight endpoints are invalid or coincident.");
            int count = scene.GetPhysicsScene().Raycast(origin, offset / distance, sightHits, distance,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (count == sightHits.Length) return Fail("The sight query overflowed; visibility is unknown.");
            for (int i = 0; i < count; i++)
            {
                var hit = sightHits[i].collider;
                if (hit == null) return Fail("A sight blocker disappeared during validation.");
                if (hit.gameObject.scene != scene) return Fail("A foreign-scene collider obstructs this house sight query.");
                if (hit.transform.IsChildOf(from) || hit.transform.IsChildOf(to)) continue;
                return Fail("A collider obstructs the conversation sight line.");
            }
            LastFailure = null;
            return true;
        }

        public bool HasCapsuleClearance(Vector3 feet, float radius, float height, Transform self,
            Transform allowedPartner = null)
        {
            Physics.SyncTransforms();
            if (!LocalActor(self) || (allowedPartner != null && (!LocalActor(allowedPartner) || allowedPartner == self))
                || !Finite(height) || height < radius * 2 || height > 3
                || !TryFindFloor(feet, radius, out var floor))
                return Fail("Capsule clearance needs a valid local actor, floor and body dimensions.");
            int count = scene.GetPhysicsScene().OverlapCapsule(feet + Vector3.up * radius,
                feet + Vector3.up * (height - radius), radius, overlapHits,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (count == overlapHits.Length) return Fail("The clearance query overflowed; occupancy is unknown.");
            for (int i = 0; i < count; i++)
            {
                var hit = overlapHits[i];
                if (hit == null) return Fail("A clearance collider disappeared during validation.");
                if (hit.gameObject.scene != scene) return Fail("A foreign-scene collider overlaps the body clearance.");
                if (hit == floor.collider || hit.transform.IsChildOf(self)
                    || allowedPartner != null && hit.transform.IsChildOf(allowedPartner)) continue;
                return Fail("The body clearance overlaps another actor or obstacle.");
            }
            LastFailure = null;
            return true;
        }

        private bool TryFindFloor(Vector3 feet, float bodyRadius, out Floor found)
        {
            found = null;
            if (!Finite(feet) || !Finite(bodyRadius) || bodyRadius <= 0 || bodyRadius > 1
                || !TryValidateScene(out _)) return Fail("The requested feet or body radius is invalid for the bound house.");
            var ray = new Ray(feet + Vector3.up * .30f, Vector3.down);
            foreach (var floor in floors)
            {
                var collider = floor.collider;
                // Query this known collider directly: an NPC's body is not its floor.
                if (!collider.Raycast(ray, out var hit, .65f)) continue;
                float vertical = feet.y - hit.point.y;
                if (vertical < -.05f || vertical > .20f || Vector3.Dot(hit.normal, Vector3.up) < .999f) continue;
                var point = collider.transform.InverseTransformPoint(hit.point) - collider.center;
                var scale = collider.transform.lossyScale;
                float insetX = (bodyRadius + .10f) / scale.x, insetZ = (bodyRadius + .10f) / scale.z;
                if (Mathf.Abs(point.x) >= collider.size.x * .5f - insetX
                    || Mathf.Abs(point.z) >= collider.size.z * .5f - insetZ) continue;
                if (found != null) { found = null; return Fail("The feet belong to more than one floor volume."); }
                found = floor;
            }
            if (found == null) return Fail("The feet are not inside one bound floor's safe interior.");
            LastFailure = null;
            return true;
        }

        private bool LocalActive(Transform target) => target != null && target.gameObject.scene == scene && target.gameObject.activeInHierarchy;
        private bool LocalActor(Transform target) => LocalActive(target)
            && (target.GetComponent<HouseNpc>() != null || target.GetComponent<HousePlayerController>() != null);
        private bool Fail(string reason) { LastFailure = reason; return false; }
        private bool Fail(out string reason, string message) { reason = message; return Fail(message); }
        internal static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        internal static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
    }
}
