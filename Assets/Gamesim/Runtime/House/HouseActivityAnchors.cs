using System;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace Gamesim.House
{
    /// <summary>
    /// The places the house's newer verbs happen: the beds, the pool, the hot tub, the hob and the
    /// living room floor. Built at runtime from the set pieces the scene already places, as the
    /// meeting venues' fallbacks are, and nothing is invented where a piece is missing: no bed,
    /// no "Lie down".
    ///
    /// <para>Every measurement here is the set piece's own, from its Blender script
    /// (<c>ArtSource/setpieces</c>): the mattress tops, the tub's seat ring and water, the pool's
    /// basin and water plane, the stove's place along the kitchen run. Each bed's head end is the
    /// piece's local -z, which is where every one of those scripts puts its headboard.</para>
    /// </summary>
    public static class HouseActivityAnchors
    {
        /// <summary>Metres a body stands clear of the thing it is walking up to: past the agent's radius, with room to turn.</summary>
        private const float Clearance = .6f;

        private static readonly (string prop, float mattress, float halfWidth)[] Beds =
        {
            ("bedSingle", .48f, .475f), ("bedBunk", .40f, .495f), ("bb_set_hohbed", .47f, .95f), ("bb_set_havenot_cot", .34f, .45f),
        };

        /// <summary>The pool's basin, water plane and deck, from bb_set_pool.py.</summary>
        public const float PoolWater = .29f, PoolBasinLength = 5.6f, PoolDeckHalfDepth = 2.2f;
        /// <summary>The hot tub's floor, seat and seat ring, from bb_set_hottub.py.</summary>
        public const float TubFloor = .20f, TubSeat = .54f, TubSeatRing = .84f, TubApproach = 1.75f;
        /// <summary>The stove's centre along the kitchen run, from its fridge end's opposite side (bb_set_kitchenrun.py).</summary>
        private const float StoveAlongRun = 1.05f, CounterFront = 1.175f;

        public static void EnsureDefaults(Scene scene, HouseInteractionAnchor[] existing, Transform[] all, HouseRoomMarker[] markers)
        {
            if (!scene.IsValid() || !scene.isLoaded) return;
            HouseRoomQuery.TryCreate(scene, out var rooms, out _);
            bool Has(string venue, int slot) => existing.Any(a => a.VenueId == venue && a.Slot == slot);

            int bed = 0;
            foreach (var (prop, mattress, halfWidth) in Beds)
                foreach (var piece in all.Where(t => t.name == prop && t.gameObject.activeInHierarchy)
                             .OrderBy(t => t.position.x).ThenBy(t => t.position.z))
                {
                    string venue = HouseFurniture.BedPrefix + bed++;
                    if (Has(venue, 0)) continue;
                    // Laid head to the headboard: the anchor's forward runs from the feet to it.
                    var forward = piece.rotation * Vector3.back;
                    var floor = piece.position;
                    var approach = Beside(floor, piece.rotation * Vector3.right, halfWidth + Clearance, rooms);
                    if (!approach.HasValue) continue;
                    var anchor = HouseInteractionAnchor.Create(piece, venue, RoomOf(approach.Value, rooms, markers), 0, floor,
                        Quaternion.LookRotation(forward).eulerAngles.y, false);
                    anchor.Configure(venue, anchor.RoomId, 0, false, anchor.transform.InverseTransformPoint(approach.Value));
                    anchor.SetPose(HouseAnchorPose.Lie);
                    anchor.SetSeatHeight(mattress);
                }

            var pool = all.FirstOrDefault(t => t.name == "bb_set_pool" && t.gameObject.activeInHierarchy);
            if (pool != null && !Has(HouseFurniture.PoolAnchor, 0))
            {
                // The deck's long side is the piece's local x; lengths are swum along it.
                var along = pool.rotation * Vector3.right;
                var approach = Beside(pool.position, pool.rotation * Vector3.forward, PoolDeckHalfDepth + .4f, rooms);
                if (approach.HasValue)
                {
                    var anchor = HouseInteractionAnchor.Create(pool, HouseFurniture.PoolAnchor, RoomOf(approach.Value, rooms, markers), 0,
                        pool.position, Quaternion.LookRotation(along).eulerAngles.y, false);
                    anchor.Configure(anchor.VenueId, anchor.RoomId, 0, false, anchor.transform.InverseTransformPoint(approach.Value));
                    anchor.SetPose(HouseAnchorPose.Float);
                    anchor.SetSeatHeight(PoolWater);
                }
            }

            var tub = all.FirstOrDefault(t => t.name == "bb_set_hottub" && t.gameObject.activeInHierarchy);
            if (tub != null)
            {
                // Two places on the seat ring, a quarter-turn apart, each facing the middle.
                var sides = new[] { tub.rotation * Vector3.right, tub.rotation * Vector3.back };
                for (int slot = 0; slot < sides.Length; slot++)
                {
                    if (Has(HouseFurniture.HotTubAnchor, slot)) continue;
                    var seat = tub.position + sides[slot] * TubSeatRing + Vector3.up * TubFloor;
                    var approach = OnFloor(tub.position + sides[slot] * TubApproach, rooms)
                                   ?? OnFloor(tub.position + sides[slot] * (TubApproach + .75f), rooms);
                    if (!approach.HasValue) continue;
                    var anchor = HouseInteractionAnchor.Create(tub, HouseFurniture.HotTubAnchor, RoomOf(approach.Value, rooms, markers), slot,
                        seat, Quaternion.LookRotation(-sides[slot]).eulerAngles.y, true);
                    anchor.Configure(anchor.VenueId, anchor.RoomId, slot, true, anchor.transform.InverseTransformPoint(approach.Value));
                    anchor.SetSeatHeight(TubSeat - TubFloor);
                }
            }

            var run = all.FirstOrDefault(t => t.name == "bb_set_kitchenrun" && t.gameObject.activeInHierarchy);
            if (run != null && !Has(HouseFurniture.StoveAnchor, 0))
            {
                // The counter's own anchor stands 1.175 m out from the run's middle, facing it; the
                // stove's stands as far out, in front of the hob.
                var at = run.position + run.rotation * new Vector3(StoveSide(run) * StoveAlongRun, 0f, -CounterFront);
                var standing = OnFloor(at, rooms);
                if (standing.HasValue)
                    HouseInteractionAnchor.Create(run, HouseFurniture.StoveAnchor, RoomOf(standing.Value, rooms, markers), 0,
                        standing.Value, run.eulerAngles.y, false);
            }

            var living = markers.FirstOrDefault(m => m.RoomName == "Living");
            if (living != null && !Has(HouseFurniture.DanceAnchor, 0))
            {
                // The middle of the living room, facing the way the house is watched from.
                var spot = OnFloor(living.transform.position, rooms);
                if (spot.HasValue)
                    HouseInteractionAnchor.Create(living.transform, HouseFurniture.DanceAnchor, "Living", 0, spot.Value, 180f, false);
            }
        }

        /// <summary>
        /// Which way along the kitchen run the stove is: the far side from the fridge. The fridge is
        /// the only steel in the piece, so the steel's side says which end is which, whichever way
        /// the model came out of the axis conversion.
        /// </summary>
        private static float StoveSide(Transform run)
        {
            foreach (var renderer in run.GetComponentsInChildren<MeshRenderer>())
            {
                var filter = renderer.GetComponent<MeshFilter>();
                var mesh = filter != null ? filter.sharedMesh : null;
                if (mesh == null) continue;
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length && i < mesh.subMeshCount; i++)
                {
                    if (materials[i] == null || !materials[i].name.StartsWith("steel_appliance", StringComparison.Ordinal)) continue;
                    var steel = renderer.transform.TransformPoint(mesh.GetSubMesh(i).bounds.center);
                    float side = Vector3.Dot(steel - run.position, run.rotation * Vector3.right);
                    if (Mathf.Abs(side) > .5f) return side < 0f ? 1f : -1f;
                }
            }
            return 1f;
        }

        /// <summary>A reachable floor point on either side of a piece, nearer side first.</summary>
        private static Vector3? Beside(Vector3 centre, Vector3 side, float distance, HouseRoomQuery rooms)
            => OnFloor(centre + side * distance, rooms) ?? OnFloor(centre - side * distance, rooms);

        private static Vector3? OnFloor(Vector3 point, HouseRoomQuery rooms)
        {
            if (!NavMesh.SamplePosition(point, out var hit, .3f, NavMesh.AllAreas)) return null;
            if (rooms != null && !rooms.TryLocate(hit.position, .3f, out _)) return null;
            return hit.position;
        }

        /// <summary>The room a point stands in, by the house's own floors; the nearest room marker if they cannot say.</summary>
        private static string RoomOf(Vector3 point, HouseRoomQuery rooms, HouseRoomMarker[] markers)
        {
            if (rooms != null && rooms.TryLocate(point, .3f, out var room)) return room;
            var nearest = markers.OrderBy(m => (m.transform.position - point).sqrMagnitude).FirstOrDefault();
            return nearest != null ? nearest.RoomName : "";
        }
    }
}
