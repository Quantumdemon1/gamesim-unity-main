using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Presentation
{
    /// <summary>
    /// Where each course runner's leg goes (competition show edges): from their post out to a turn
    /// mark and back, along a straight line the yard's walking map gives them, since the stations'
    /// equipment stands on no walking map. A leg is only offered where that line keeps a runner's
    /// width clear of every other station's equipment and body and of every other runner's leg, so
    /// nobody runs through a console and no two legs cross - in a field of one row or of two.
    ///
    /// <para>Straight back toward the house first, as far as is clear, as it always was. A post with a
    /// station in front of it - the back row of a field over fourteen - leans away from the yard's
    /// middle instead, and its neighbours lean with it, so the back row fans out in parallel past the
    /// front row. The posts are settled most hemmed in first, then from the middle out.</para>
    ///
    /// <para>Pure presentation over the yard's floor plan, on the ground plane (x across the yard, z
    /// from the house to the back wall): no Unity type, no season state, no random draw. The arena
    /// still checks every mark it is offered against the walking map, the scenery and the other
    /// reservations, and tells this plan which it took.</para>
    /// </summary>
    public sealed class CompetitionCourseLegs
    {
        /// <summary>How far out a turn mark is tried, metres, longest first (the course's 4.5 m down to 2 m, as before).</summary>
        public static readonly float[] Distances = { 4.5f, 4f, 3.5f, 3f, 2.5f, 2f };

        /// <summary>
        /// The angles a leg is tried at, degrees from straight back toward the house: positive leans
        /// away from the yard's middle, negative toward it, 90 runs along the row (the old sideways
        /// leg, for a post whose way back is blocked by scenery).
        /// </summary>
        public static readonly float[] Angles = { 0f, 20f, 30f, 40f, 45f, 50f, 55f, 60f, 70f, -20f, -30f, -45f, -60f, 90f, -90f };

        /// <summary>The gap kept between a runner's side and another station's equipment or body, metres.</summary>
        public const float EquipmentGap = .05f;

        /// <summary>The gap kept between two runners' sides, anywhere on their legs, metres.</summary>
        public const float LegGap = .1f;

        /// <summary>A rectangle on the ground: a station's equipment and body, or a body standing.</summary>
        public readonly struct Box
        {
            public readonly float MinX, MaxX, MinZ, MaxZ;
            public Box(float minX, float maxX, float minZ, float maxZ) { MinX = minX; MaxX = maxX; MinZ = minZ; MaxZ = maxZ; }

            /// <summary>
            /// A station facing +z, the arena's one facing: the body of <paramref name="radius"/> at the
            /// post, the equipment <paramref name="reach"/> ahead of it and <paramref name="halfWidth"/>
            /// either side of it.
            /// </summary>
            public static Box Station(float x, float z, float radius, float reach, float halfWidth)
            {
                float side = Math.Max(halfWidth, radius);
                return new Box(x - side, x + side, z - radius, z + Math.Max(reach, radius));
            }

            /// <summary>A body standing at a place.</summary>
            public static Box Body(float x, float z, float radius) => new Box(x - radius, x + radius, z - radius, z + radius);

            public override string ToString() => "[" + MinX.ToString("0.00") + ".." + MaxX.ToString("0.00") + " x " + MinZ.ToString("0.00") + ".." + MaxZ.ToString("0.00") + "]";
        }

        /// <summary>A point on the ground.</summary>
        public readonly struct Spot
        {
            public readonly float X, Z;
            public Spot(float x, float z) { X = x; Z = z; }
            public override string ToString() => "(" + X.ToString("0.00") + ", " + Z.ToString("0.00") + ")";
        }

        private readonly float minX, maxX, minZ, maxZ, centreX, runnerRadius;
        private readonly List<string> posts = new List<string>();
        private readonly Dictionary<string, Spot> postAt = new Dictionary<string, Spot>();
        private readonly Dictionary<string, Box> stationOf = new Dictionary<string, Box>();
        private readonly List<Box> others = new List<Box>();
        private readonly Dictionary<string, Spot> marks = new Dictionary<string, Spot>();
        private List<string> order;

        /// <summary>
        /// A plan for the yard's floor from <paramref name="deckMinX"/> to <paramref name="deckMaxX"/> and
        /// <paramref name="deckMinZ"/> to <paramref name="deckMaxZ"/>, keeping every mark
        /// <paramref name="edge"/> inside it, for runners as wide as <paramref name="runnerRadius"/>,
        /// leaning away from <paramref name="centreX"/>.
        /// </summary>
        public CompetitionCourseLegs(float deckMinX, float deckMaxX, float deckMinZ, float deckMaxZ, float edge, float runnerRadius, float centreX)
        {
            if (edge < 0f) edge = 0f;
            minX = deckMinX + edge; maxX = deckMaxX - edge; minZ = deckMinZ + edge; maxZ = deckMaxZ - edge;
            this.runnerRadius = runnerRadius > 0f ? runnerRadius : 0f;
            this.centreX = centreX;
        }

        /// <summary>A runner's post and the station standing at it (its equipment and body).</summary>
        public void AddPost(string id, float x, float z, Box station)
        {
            if (id == null || postAt.ContainsKey(id)) return;
            posts.Add(id); postAt[id] = new Spot(x, z); stationOf[id] = station; order = null;
        }

        /// <summary>Something no leg may run through that is not a runner's own station: a non-running station, an audience member.</summary>
        public void AddObstacle(Box box) { others.Add(box); order = null; }

        /// <summary>The marks taken so far, by runner.</summary>
        public IReadOnlyDictionary<string, Spot> Marks => marks;

        /// <summary>The post of <paramref name="id"/>.</summary>
        public Spot PostOf(string id) => postAt[id];

        /// <summary>
        /// The order the posts are settled in: those with no clear way straight back first (a station
        /// in front of them), then the rest; each group from the yard's middle out, the back of the
        /// yard first at a tie, then by id.
        /// </summary>
        public IReadOnlyList<string> Order
        {
            get
            {
                if (order != null) return order;
                order = posts.OrderBy(id => StraightBackBlocked(id) ? 0 : 1)
                    .ThenBy(id => Math.Abs(postAt[id].X - centreX)).ThenByDescending(id => postAt[id].Z)
                    .ThenBy(id => postAt[id].X).ThenBy(id => id, StringComparer.Ordinal).ToList();
                return order;
            }
        }

        private bool StraightBackBlocked(string id)
        {
            var post = postAt[id];
            foreach (float distance in Distances)
            {
                var end = new Spot(post.X, post.Z - distance);
                if (InDeck(end) && ClearOfStations(id, post, end)) return false;
            }
            return true;
        }

        /// <summary>The direction of a leg at <paramref name="degrees"/> from straight back, leaning away from the middle for a positive angle.</summary>
        public Spot Direction(string id, float degrees)
        {
            float away = postAt[id].X >= centreX ? 1f : -1f;
            double radians = degrees * Math.PI / 180.0;
            return new Spot((float)(away * Math.Sin(radians)), (float)(-Math.Cos(radians)));
        }

        /// <summary>
        /// Every turn mark for <paramref name="id"/> that keeps their leg clear, best first: by angle
        /// (<see cref="Angles"/>), then by length (<see cref="Distances"/>). Read lazily, so each is
        /// checked against the legs taken before it is offered.
        /// </summary>
        public IEnumerable<Spot> Candidates(string id)
        {
            if (id == null || !postAt.TryGetValue(id, out var post) || marks.ContainsKey(id)) yield break;
            foreach (float angle in Angles)
            {
                var direction = Direction(id, angle);
                foreach (float distance in Distances)
                {
                    var end = new Spot(post.X + direction.X * distance, post.Z + direction.Z * distance);
                    if (Clears(id, end)) yield return end;
                }
            }
        }

        /// <summary>Whether a leg from <paramref name="id"/>'s post to <paramref name="mark"/> stays on the floor and clear of every station, body and leg taken.</summary>
        public bool Clears(string id, Spot mark)
        {
            if (id == null || !postAt.TryGetValue(id, out var post) || !InDeck(mark)) return false;
            if (!ClearOfStations(id, post, mark)) return false;
            float apart = 2f * runnerRadius + LegGap;
            foreach (var pair in marks)
            {
                if (pair.Key == id) continue;
                if (SegmentDistance(post, mark, postAt[pair.Key], pair.Value) < apart) return false;
            }
            return true;
        }

        /// <summary>The arena took this mark for <paramref name="id"/>: later legs keep clear of it.</summary>
        public void Accept(string id, Spot mark)
        {
            if (id == null || !postAt.ContainsKey(id)) return;
            marks[id] = mark;
        }

        private bool InDeck(Spot spot) => spot.X >= minX && spot.X <= maxX && spot.Z >= minZ && spot.Z <= maxZ;

        private bool ClearOfStations(string id, Spot from, Spot to)
        {
            float clear = runnerRadius + EquipmentGap;
            foreach (var pair in stationOf)
                if (pair.Key != id && SegmentBoxDistance(from, to, pair.Value) < clear) return false;
            foreach (var box in others)
                if (SegmentBoxDistance(from, to, box) < clear) return false;
            return true;
        }

        // ------------------------------------------------------------ plane geometry

        /// <summary>The least distance between two segments on the ground (0 where they cross).</summary>
        public static float SegmentDistance(Spot a, Spot b, Spot c, Spot d)
        {
            if (SegmentsCross(a, b, c, d)) return 0f;
            return Math.Min(Math.Min(PointSegment(a, c, d), PointSegment(b, c, d)), Math.Min(PointSegment(c, a, b), PointSegment(d, a, b)));
        }

        /// <summary>The least distance between a segment and a rectangle on the ground (0 where it touches or enters it).</summary>
        public static float SegmentBoxDistance(Spot a, Spot b, Box box)
        {
            if (Inside(a, box) || Inside(b, box)) return 0f;
            var c00 = new Spot(box.MinX, box.MinZ); var c10 = new Spot(box.MaxX, box.MinZ);
            var c11 = new Spot(box.MaxX, box.MaxZ); var c01 = new Spot(box.MinX, box.MaxZ);
            float best = float.MaxValue;
            foreach (var edge in new[] { (c00, c10), (c10, c11), (c11, c01), (c01, c00) })
            {
                float distance = SegmentDistance(a, b, edge.Item1, edge.Item2);
                if (distance < best) best = distance;
            }
            return best;
        }

        private static bool Inside(Spot p, Box box) => p.X >= box.MinX && p.X <= box.MaxX && p.Z >= box.MinZ && p.Z <= box.MaxZ;

        private static float PointSegment(Spot p, Spot a, Spot b)
        {
            float dx = b.X - a.X, dz = b.Z - a.Z;
            float length = dx * dx + dz * dz;
            float t = length > 1e-9f ? ((p.X - a.X) * dx + (p.Z - a.Z) * dz) / length : 0f;
            if (t < 0f) t = 0f; else if (t > 1f) t = 1f;
            float x = a.X + dx * t - p.X, z = a.Z + dz * t - p.Z;
            return (float)Math.Sqrt(x * x + z * z);
        }

        private static float Cross(Spot o, Spot a, Spot b) => (a.X - o.X) * (b.Z - o.Z) - (a.Z - o.Z) * (b.X - o.X);

        // Crossing or touching - an end on the other segment, or a leg through a box's corner - counts:
        // the distance is nothing. Two segments on one line are left to the distances, which measure them.
        private static bool SegmentsCross(Spot a, Spot b, Spot c, Spot d)
        {
            float d1 = Cross(c, d, a), d2 = Cross(c, d, b), d3 = Cross(a, b, c), d4 = Cross(a, b, d);
            if (d1 == 0f && d2 == 0f && d3 == 0f && d4 == 0f) return false;
            return (d1 >= 0f && d2 <= 0f || d1 <= 0f && d2 >= 0f) && (d3 >= 0f && d4 <= 0f || d3 <= 0f && d4 >= 0f);
        }
    }
}
