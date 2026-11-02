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
    /// </summary>
    public sealed partial class HouseRoomScope
    {
        /// <summary>The narrowest open run along a shared edge that is a doorway.</summary>
        public const float DoorwayWidth = .9f;

        /// <summary>How far apart the looks across a shared edge are, and how high they cross it.</summary>
        public const float DoorwayStep = .1f, DoorwayLookHeight = .5f;

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
        /// the sight queries see across a shared edge, other than a body or a floor, is a wall. None for a
        /// scope built from rectangles, which has no walls to read.
        /// </summary>
        public IReadOnlyList<Doorway> Doorways
        {
            get
            {
                if (doorways != null) return doorways;
                doorways = new List<Doorway>(12);
                if (query != null) FindDoorways(WallAcross, doorways);
                return doorways;
            }
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
        /// stands across a segment, from one side of the line to the other, at <see cref="DoorwayLookHeight"/>.
        /// </summary>
        public void FindDoorways(Func<Vector3, Vector3, bool> wallAcross, List<Doorway> into)
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
                            into.Add(new Doorway(a, b, alongZ ? new Vector3(line, 0f, middle) : new Vector3(middle, 0f, line), width));
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
    }
}
