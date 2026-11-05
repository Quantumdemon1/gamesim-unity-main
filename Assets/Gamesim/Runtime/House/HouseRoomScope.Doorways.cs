using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gamesim.House
{
    /// <summary>
    /// The house's doorways, from its walls (ROOM-SCOPE-PLAN §2.1, D17, RS5): along each edge two floors
    /// share, a look across the line at half a metre is open where no wall stands, and an open run of
    /// <see cref="DoorwayWidth"/> or more is a doorway between the two rooms. The room view hangs each
    /// neighbouring room's travel icon at the viewed room's doorway that leads there.
    ///
    /// <para>A corner doorway (the yard's, the nomination room's) opens onto the line where two floors
    /// meet, so it is found as two halves, one onto each floor: from the far side's room, each is a
    /// doorway to one of the two. The walls are read once, by whoever first asks; the plain rule takes
    /// its look across as a function, so a fixture gives it walls of its own.</para>
    ///
    /// <para>An opening is a doorway only if somebody can walk through it (D17: "the doorway that leads
    /// there"): on the baked NavMesh, a walk from <see cref="DoorwayWalkReach"/> inside one floor to as far
    /// inside the other, through its middle, of no more than <see cref="DoorwayWalkMetres"/>. The gap
    /// between the kitchen and the private room was not one while the kitchen run and the agents' erosion
    /// closed it (section 2.1): the private room's icon waited at the frame's edge rather than over a gap a
    /// press would walk round through two rooms. The run moved into the corner (the quick fixes, 2026-11-03,
    /// HouseKitchenDoorway), and it is walked through. The plain rule takes that walk as a second function
    /// too; a house with no NavMesh baked keeps every opening its walls show.</para>
    /// </summary>
    public sealed partial class HouseRoomScope
    {
        /// <summary>The narrowest open run along a shared edge that is a doorway.</summary>
        public const float DoorwayWidth = .9f;

        /// <summary>How far apart the looks across a shared edge are, and how high they cross it.</summary>
        public const float DoorwayStep = .1f, DoorwayLookHeight = .5f;

        /// <summary>How far inside either floor the walk through a doorway starts and ends, and the longest that walk may be: a walk round through other rooms is many times longer.</summary>
        public const float DoorwayWalkReach = .6f, DoorwayWalkMetres = 3f;

        /// <summary>One doorway: the two rooms it joins (in the layers' order, <see cref="First"/> before <see cref="Second"/>), its middle on the wall line, and its width.</summary>
        public readonly struct Doorway
        {
            public readonly int First, Second;
            public readonly Vector3 Middle;
            public readonly float Width;
            public Doorway(int first, int second, Vector3 middle, float width) { First = first; Second = second; Middle = middle; Width = width; }
            /// <summary>Whether it joins <paramref name="room"/> to <paramref name="other"/>, either way.</summary>
            public bool Joins(int room, int other) => (First == room && Second == other) || (First == other && Second == room);
            public override string ToString() => RoomId(First) + "-" + RoomId(Second) + " at " + Middle.x.ToString("0.00") + ", " + Middle.z.ToString("0.00");
        }

        private List<Doorway> doorways;

        /// <summary>
        /// The house's doorways, read from its walls the first time they are asked for and kept: a solid
        /// the sight queries see across a shared edge, other than a body or a floor, is a wall, and an
        /// opening nobody can walk through on the NavMesh is not a doorway. None for a scope built from
        /// rectangles, which has no walls to read.
        /// </summary>
        public IReadOnlyList<Doorway> Doorways
        {
            get
            {
                if (doorways != null) return doorways;
                doorways = new List<Doorway>(12);
                if (query != null) ReadDoorwaysFromTheHouse(doorways);
                return doorways;
            }
        }

        /// <summary>
        /// The one read of the house's own walls and NavMesh: the physics scene synced with every transform
        /// first (the project syncs no transform by itself between fixed steps, and what is read is kept for
        /// the scope's life), then the walls looked across and each opening walked through.
        /// </summary>
        private void ReadDoorwaysFromTheHouse(List<Doorway> into)
        {
            Physics.SyncTransforms();
            FindDoorways(WallAcross, into, DoorwayWalk.Baked() ? DoorwayWalk.Through : (Func<Vector3, Vector3, bool>)null);
        }

        /// <summary>
        /// The doorway that leads from room <paramref name="from"/> to room <paramref name="to"/> nearest
        /// <paramref name="near"/>, in plan; false where the two rooms share no doorway.
        /// </summary>
        public bool TryDoorway(int from, int to, Vector3 near, out Doorway doorway)
        {
            doorway = default;
            bool found = false;
            float best = float.PositiveInfinity;
            var all = Doorways;
            for (int i = 0; i < all.Count; i++)
            {
                if (!all[i].Joins(from, to)) continue;
                float dx = all[i].Middle.x - near.x, dz = all[i].Middle.z - near.z;
                float away = dx * dx + dz * dz;
                if (away >= best) continue;
                best = away;
                doorway = all[i];
                found = true;
            }
            return found;
        }

        /// <summary>
        /// The doorways along every edge two floors share: <paramref name="wallAcross"/> says whether a wall
        /// stands across a segment, from one side of the line to the other, at <see cref="DoorwayLookHeight"/>;
        /// <paramref name="walkable"/>, when given, whether somebody can walk from one point on the floor to
        /// another - from <see cref="DoorwayWalkReach"/> inside one floor to as far inside the other, through
        /// an opening's middle - and an opening it refuses is not a doorway.
        /// </summary>
        public void FindDoorways(Func<Vector3, Vector3, bool> wallAcross, List<Doorway> into, Func<Vector3, Vector3, bool> walkable = null)
        {
            into.Clear();
            if (wallAcross == null) return;
            for (int a = 0; a < RoomLayers.Count; a++)
                for (int b = a + 1; b < RoomLayers.Count; b++)
                {
                    if (!Has(a) || !Has(b)) continue;
                    var one = Box(a);
                    var two = Box(b);
                    // The shared edge: a line, the axis it runs along, and the way from one floor into the other.
                    float zLow = Mathf.Max(one.yMin, two.yMin), zHigh = Mathf.Min(one.yMax, two.yMax);
                    float xLow = Mathf.Max(one.xMin, two.xMin), xHigh = Mathf.Min(one.xMax, two.xMax);
                    Vector3 normal; float line, low, high; bool alongZ;
                    if (zHigh - zLow >= .5f && Mathf.Abs(one.xMax - two.xMin) < .01f) { normal = Vector3.right; line = one.xMax; low = zLow; high = zHigh; alongZ = true; }
                    else if (zHigh - zLow >= .5f && Mathf.Abs(one.xMin - two.xMax) < .01f) { normal = Vector3.left; line = one.xMin; low = zLow; high = zHigh; alongZ = true; }
                    else if (xHigh - xLow >= .5f && Mathf.Abs(one.yMax - two.yMin) < .01f) { normal = Vector3.forward; line = one.yMax; low = xLow; high = xHigh; alongZ = false; }
                    else if (xHigh - xLow >= .5f && Mathf.Abs(one.yMin - two.yMax) < .01f) { normal = Vector3.back; line = one.yMin; low = xLow; high = xHigh; alongZ = false; }
                    else continue;
                    float runStart = float.NaN, runEnd = float.NaN;
                    for (float s = low + DoorwayStep * .5f; s <= high + .001f; s += DoorwayStep)
                    {
                        bool last = s + DoorwayStep > high + .001f;
                        var onLine = alongZ ? new Vector3(line, DoorwayLookHeight, s) : new Vector3(s, DoorwayLookHeight, line);
                        bool open = !wallAcross(onLine - normal * .6f, onLine + normal * .6f);
                        if (open) { if (float.IsNaN(runStart)) runStart = s; runEnd = s; }
                        if ((open && !last) || float.IsNaN(runStart)) continue;
                        float width = runEnd - runStart + DoorwayStep;
                        if (width >= DoorwayWidth)
                        {
                            float middle = (runStart + runEnd) * .5f;
                            var at = alongZ ? new Vector3(line, 0f, middle) : new Vector3(middle, 0f, line);
                            if (walkable == null || walkable(at - normal * DoorwayWalkReach, at + normal * DoorwayWalkReach))
                                into.Add(new Doorway(a, b, at, width));
                        }
                        runStart = runEnd = float.NaN;
                    }
                }
        }

        private readonly RaycastHit[] doorwayHits = new RaycastHit[16];

        /// <summary>Whether a wall stands across the segment: a solid the sight queries see, not a body or a floor.</summary>
        private bool WallAcross(Vector3 from, Vector3 to)
        {
            var direction = to - from;
            float length = direction.magnitude;
            if (length <= 0f) return false;
            int hits = Physics.RaycastNonAlloc(from, direction / length, doorwayHits, length, HouseLayers.Sight, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hits; i++)
            {
                var hit = doorwayHits[i].collider;
                if (hit == null || IsBody(hit.transform) || hit.GetComponent<HouseWalkable>() != null) continue;
                return true;
            }
            return false;
        }

        /// <summary>
        /// The walk through an opening, on the baked NavMesh. Its own class, so the plain fixtures, which run
        /// under the editor's Mono without the engine's navigation module, never load it.
        /// </summary>
        private static class DoorwayWalk
        {
            /// <summary>How far from a point the NavMesh may lie and that point still be on it: past the agents' erosion at a doorway's mouth.</summary>
            private const float OnTheNavMesh = .75f;

            /// <summary>Whether the house has a NavMesh to walk on at all.</summary>
            public static bool Baked()
            {
                var mesh = UnityEngine.AI.NavMesh.CalculateTriangulation();
                return mesh.vertices != null && mesh.vertices.Length > 0;
            }

            /// <summary>Whether a walk from <paramref name="from"/> to <paramref name="to"/> on the NavMesh is no longer than a doorway's.</summary>
            public static bool Through(Vector3 from, Vector3 to)
            {
                if (!UnityEngine.AI.NavMesh.SamplePosition(from, out var start, OnTheNavMesh, UnityEngine.AI.NavMesh.AllAreas)) return false;
                if (!UnityEngine.AI.NavMesh.SamplePosition(to, out var end, OnTheNavMesh, UnityEngine.AI.NavMesh.AllAreas)) return false;
                var path = new UnityEngine.AI.NavMeshPath();
                if (!UnityEngine.AI.NavMesh.CalculatePath(start.position, end.position, UnityEngine.AI.NavMesh.AllAreas, path)
                    || path.status != UnityEngine.AI.NavMeshPathStatus.PathComplete) return false;
                var corners = path.corners;
                float length = 0f;
                for (int i = 1; i < corners.Length; i++) length += Vector3.Distance(corners[i - 1], corners[i]);
                return length <= DoorwayWalkMetres;
            }
        }
    }
}
