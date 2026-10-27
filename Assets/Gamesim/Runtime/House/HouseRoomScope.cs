using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Gamesim.House
{
    /// <summary>
    /// The house's rooms as the view scope sees them (ROOM-SCOPE-PLAN §3.2 a and b): the eight floor
    /// boxes in plan, the walker rule that says which room a body is in, the classifier that says which
    /// room a piece of the house belongs to, and the lamps' home rooms.
    ///
    /// <para>It computes and reports only. Nothing here writes a layer, a culling mask or a light:
    /// the classification is a table for RS2a to apply, and the walker rooms are what the camera rig
    /// reports and, from RSW, what who-is-where reads.</para>
    ///
    /// <para>The boxes come from the floors through one <see cref="HouseRoomQuery"/>, which the scope
    /// keeps (<see cref="Query"/>): a reader that wants the query's own questions - the safe interior,
    /// the floor sample - asks this one instead of building another. The pure parts (the boxes, the
    /// walker rule, the plan rule) need no scene, so a fixture can build a scope from rectangles.</para>
    /// </summary>
    public sealed class HouseRoomScope
    {
        /// <summary>
        /// How far inside a new floor's box the feet must be before a walker is in that room. Until
        /// then the last room is kept, so a doorway shuffle never flips it: 0.14 s past the wall line
        /// at a walk (2.2 m/s), 0.075 s at a run.
        /// </summary>
        public const float DoorwayDepth = .3f;

        /// <summary>How far past its floor's box a piece may reach and still be that room's.</summary>
        public const float BoxGrowth = .25f;

        /// <summary>
        /// Where a renderer stands for the scope: in a room, or drawn always and why. Only
        /// <see cref="Room"/> ever moves to a room layer (RS2a).
        /// </summary>
        public enum Placement : byte
        {
            /// <summary>Inside one floor box grown by <see cref="BoxGrowth"/>: that room's.</summary>
            Room,
            /// <summary>On the shell list: the neon room outlines and the authored shell.</summary>
            Shell,
            /// <summary>Its bounds cross a wall line, or reach past the growth into a neighbour.</summary>
            Straddles,
            /// <summary>Over no floor at all, or over one floor and out past its growth (beyond an outer wall).</summary>
            Outside,
            /// <summary>Its GameObject carries a collider: its layer is physics, so it stays.</summary>
            Collider,
            /// <summary>Already on a layer of its own (not layer 0).</summary>
            Layer,
            /// <summary>Not a mesh, skinned mesh or particle renderer.</summary>
            Kind,
            /// <summary>Part of a body; bodies are drawn by where they walk (RS4), never by where they stood.</summary>
            Body,
        }

        /// <summary>One renderer's row in the classification table.</summary>
        public readonly struct Classified
        {
            public readonly Renderer Renderer;
            /// <summary>The room's index (<see cref="RoomLayers.RoomIds"/>), or -1 for anything drawn always.</summary>
            public readonly int Room;
            public readonly Placement Placement;

            public Classified(Renderer renderer, int room, Placement placement)
            {
                Renderer = renderer;
                Room = room;
                Placement = placement;
            }
        }

        /// <summary>A lamp and the room it lights: where it stands.</summary>
        public readonly struct LampHome
        {
            public readonly Light Light;
            public readonly int Room;

            public LampHome(Light light, int room)
            {
                Light = light;
                Room = room;
            }
        }

        /// <summary>
        /// The shell list: what stays drawn whatever room is viewed, beside everything with a collider
        /// (the floors, walls and fences carry theirs). The neon outlines of the north block and the
        /// yard live under this root (the prototype's broadcast dressing).
        /// </summary>
        public const string NeonOutlinesRoot = "Neon Room Outlines";
        /// <summary>The south wing's own neon outline: HouseExpansion's strips, named this under <see cref="SouthWingRoot"/>.</summary>
        public const string SouthWingOutlineName = "Trim";
        public const string SouthWingRoot = "South Wing";
        /// <summary>The authored shell (HouseSetPieces.Shell): every wall's caps, skirting and jambs, and the fences, as one mesh.</summary>
        public const string ShellMeshName = "bb_shell_house";

        private struct Walker
        {
            public Transform Body;
            public int Room;
        }

        private readonly HouseRoomQuery query;
        // Plan boxes by room index: x min/max and z min/max. A room the house lacks (the prototype has
        // no south wing) is absent.
        private readonly float[] minX = new float[RoomLayers.Count], maxX = new float[RoomLayers.Count];
        private readonly float[] minZ = new float[RoomLayers.Count], maxZ = new float[RoomLayers.Count];
        private readonly bool[] present = new bool[RoomLayers.Count];
        private readonly List<Walker> walkers = new List<Walker>(32);
        private readonly List<Renderer> rendererBuffer = new List<Renderer>(1024);
        private readonly List<Light> lightBuffer = new List<Light>(64);
        private readonly List<GameObject> rootBuffer = new List<GameObject>(64);

        private HouseRoomScope(HouseRoomQuery query) { this.query = query; }

        /// <summary>The kept room query whose floors the boxes came from; null for a scope built from rectangles.</summary>
        public HouseRoomQuery Query => query;

        /// <summary>
        /// The scope over a loaded house: its floors' boxes, read once through a new room query. False,
        /// with the query's reason, for a scene the room query refuses.
        /// </summary>
        public static bool TryCreate(Scene scene, out HouseRoomScope scope, out string reason)
        {
            scope = null;
            if (!HouseRoomQuery.TryCreate(scene, out var query, out reason)) return false;
            var candidate = new HouseRoomScope(query);
            for (int i = 0; i < query.FloorCount; i++)
            {
                int room = RoomLayers.IndexOf(query.FloorRoom(i));
                var floor = query.FloorCollider(i);
                if (room < 0 || floor == null) { reason = "A floor names no room the scope knows: " + query.FloorRoom(i) + "."; return false; }
                // The box's four corners in plan: a floor turned about its up axis still reads true.
                var centre = floor.center;
                var half = floor.size * .5f;
                float x0 = float.PositiveInfinity, x1 = float.NegativeInfinity, z0 = float.PositiveInfinity, z1 = float.NegativeInfinity;
                for (int corner = 0; corner < 4; corner++)
                {
                    var local = centre + new Vector3((corner & 1) == 0 ? -half.x : half.x, 0f, (corner & 2) == 0 ? -half.z : half.z);
                    var world = floor.transform.TransformPoint(local);
                    x0 = Mathf.Min(x0, world.x); x1 = Mathf.Max(x1, world.x);
                    z0 = Mathf.Min(z0, world.z); z1 = Mathf.Max(z1, world.z);
                }
                candidate.SetBox(room, x0, x1, z0, z1);
            }
            scope = candidate;
            return true;
        }

        /// <summary>A scope from plan rectangles (x is world x, y is world z), for fixtures with no house.</summary>
        public static HouseRoomScope FromPlan(string[] rooms, Rect[] plans)
        {
            var scope = new HouseRoomScope(null);
            for (int i = 0; i < rooms.Length && i < plans.Length; i++)
            {
                int room = RoomLayers.IndexOf(rooms[i]);
                if (room >= 0) scope.SetBox(room, plans[i].xMin, plans[i].xMax, plans[i].yMin, plans[i].yMax);
            }
            return scope;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private void SetBox(int room, float x0, float x1, float z0, float z1)
        {
            minX[room] = x0; maxX[room] = x1; minZ[room] = z0; maxZ[room] = z1;
            present[room] = true;
        }

        /// <summary>Whether the house has room <paramref name="index"/> (the prototype has five of the eight).</summary>
        public bool Has(int index) => index >= 0 && index < RoomLayers.Count && present[index];

        /// <summary>Room <paramref name="index"/>'s floor box in plan (x is world x, y is world z).</summary>
        public Rect Box(int index) => Has(index) ? Rect.MinMaxRect(minX[index], minZ[index], maxX[index], maxZ[index]) : default;

        /// <summary>The rooms this house has.</summary>
        public RoomSet Rooms
        {
            get
            {
                int bits = 0;
                for (int i = 0; i < RoomLayers.Count; i++) if (present[i]) bits |= 1 << i;
                return RoomSet.FromBits(bits);
            }
        }

        /// <summary>The room id at <paramref name="index"/>, or null for -1.</summary>
        public static string RoomId(int index) => index >= 0 && index < RoomLayers.Count ? RoomLayers.RoomIds[index] : null;

        // ------------------------------------------------------------------------------------
        // The walker rule (§3.2 a).
        // ------------------------------------------------------------------------------------

        /// <summary>
        /// How far inside room <paramref name="index"/>'s box the point stands: positive inside, the
        /// distance to the nearest edge; negative outside, by the farthest edge it is past.
        /// </summary>
        private float Depth(int index, float x, float z)
            => Mathf.Min(Mathf.Min(x - minX[index], maxX[index] - x), Mathf.Min(z - minZ[index], maxZ[index] - z));

        /// <summary>
        /// The room a walker is in: <paramref name="last"/> until the feet stand
        /// <see cref="DoorwayDepth"/> inside another floor's box, then that one. The last room is kept
        /// only while the feet are within <see cref="DoorwayDepth"/> of its own box: farther out, the
        /// floor under them. That is the corner doorways' case - the yard's and the nomination room's
        /// open onto the line where two floors meet, so a walker through their middle stands a few
        /// centimetres inside either floor and never 0.3 m inside one - and a warp's. A walker with no
        /// room yet (-1) takes the floor it stands on, or the nearest - a load or the opening's
        /// hand-over in a doorway. -1 only for a house with no floors.
        /// </summary>
        public int WalkerRoom(Vector3 feet, int last)
        {
            float x = feet.x, z = feet.z;
            bool known = last >= 0 && last < RoomLayers.Count && present[last];
            if (!Finite(x) || !Finite(z)) return known ? last : -1;
            int entered = -1;
            float deepest = DoorwayDepth;
            for (int i = 0; i < RoomLayers.Count; i++)
            {
                if (!present[i] || i == last) continue;
                float depth = Depth(i, x, z);
                if (depth < deepest) continue;
                deepest = depth;
                entered = i;
            }
            if (entered >= 0) return entered;
            return known && Depth(last, x, z) > -DoorwayDepth ? last : RoomUnder(feet);
        }

        /// <summary>
        /// The floor room under a point: the floor box holding it (the deepest, on a shared edge the
        /// first in the room order), else the nearest box. A free camera move watches this room, and a
        /// lamp lights this one. -1 only for a house with no floors.
        /// </summary>
        public int RoomUnder(Vector3 point)
        {
            float x = point.x, z = point.z;
            if (!Finite(x) || !Finite(z)) return -1;
            int best = -1;
            float bestDepth = float.NegativeInfinity;
            for (int i = 0; i < RoomLayers.Count; i++)
            {
                if (!present[i]) continue;
                float depth = Depth(i, x, z);
                if (depth < 0f)
                {
                    // Outside: rank by the true distance to the box, not to its nearest edge line.
                    float dx = Mathf.Max(minX[i] - x, Mathf.Max(0f, x - maxX[i]));
                    float dz = Mathf.Max(minZ[i] - z, Mathf.Max(0f, z - maxZ[i]));
                    depth = -Mathf.Sqrt(dx * dx + dz * dz);
                }
                if (depth <= bestDepth) continue;
                bestDepth = depth;
                best = i;
            }
            return best;
        }

        /// <summary>
        /// Advances <paramref name="body"/>'s walker room to where it stands now and returns it. One
        /// kept row per body, so the last room survives between calls: the director tracks every body
        /// every frame and the rig its subject; both read the same row. A position read twice gives
        /// the same room, so extra calls are harmless. No allocation once a body has its row.
        /// </summary>
        public int Track(Transform body)
        {
            if (body == null) return -1;
            var feet = body.position;
            for (int i = 0; i < walkers.Count; i++)
            {
                if (!ReferenceEquals(walkers[i].Body, body)) continue;
                var walker = walkers[i];
                walker.Room = WalkerRoom(feet, walker.Room);
                walkers[i] = walker;
                return walker.Room;
            }
            int room = WalkerRoom(feet, -1);
            walkers.Add(new Walker { Body = body, Room = room });
            return room;
        }

        /// <summary>The room <paramref name="body"/> was last tracked in, without moving it on; -1 for a body never tracked.</summary>
        public int LastRoom(Transform body)
        {
            if (body == null) return -1;
            for (int i = 0; i < walkers.Count; i++)
                if (ReferenceEquals(walkers[i].Body, body)) return walkers[i].Room;
            return -1;
        }

        /// <summary>Lets the rows of destroyed bodies go: the director does, once a second.</summary>
        public void ForgetMissing()
        {
            for (int i = walkers.Count - 1; i >= 0; i--)
                if (walkers[i].Body == null) walkers.RemoveAt(i);
        }

        /// <summary>How many bodies have a row.</summary>
        public int TrackedCount => walkers.Count;

        // ------------------------------------------------------------------------------------
        // The classifier (§3.2 b): a table, applied by nothing in RS1.
        // ------------------------------------------------------------------------------------

        /// <summary>
        /// The plan rule: the room whose floor box, grown by <see cref="BoxGrowth"/>, holds the whole
        /// plan rectangle. Where two grown boxes hold it (a piece within the growth of a shared wall),
        /// the room whose own box holds its centre; a piece centred on the wall line is shared, and
        /// stays drawn always. -1, with why, for a piece that crosses a wall or stands over no floor.
        /// </summary>
        public int RoomForPlan(float x0, float x1, float z0, float z1, out Placement placement)
        {
            int holder = -1, holders = 0, touched = 0;
            for (int i = 0; i < RoomLayers.Count; i++)
            {
                if (!present[i]) continue;
                if (x0 < maxX[i] && minX[i] < x1 && z0 < maxZ[i] && minZ[i] < z1) touched++;
                if (x0 < minX[i] - BoxGrowth || x1 > maxX[i] + BoxGrowth || z0 < minZ[i] - BoxGrowth || z1 > maxZ[i] + BoxGrowth) continue;
                holders++;
                holder = i;
            }
            if (holders == 1) { placement = Placement.Room; return holder; }
            if (holders == 0) { placement = touched > 1 ? Placement.Straddles : Placement.Outside; return -1; }
            const float Inside = .001f;
            float cx = (x0 + x1) * .5f, cz = (z0 + z1) * .5f;
            int centred = -1, count = 0;
            for (int i = 0; i < RoomLayers.Count; i++)
            {
                if (!present[i]) continue;
                if (x0 < minX[i] - BoxGrowth || x1 > maxX[i] + BoxGrowth || z0 < minZ[i] - BoxGrowth || z1 > maxZ[i] + BoxGrowth) continue;
                if (cx <= minX[i] + Inside || cx >= maxX[i] - Inside || cz <= minZ[i] + Inside || cz >= maxZ[i] - Inside) continue;
                count++;
                centred = i;
            }
            if (count == 1) { placement = Placement.Room; return centred; }
            placement = Placement.Straddles;
            return -1;
        }

        /// <summary>
        /// Where one renderer stands (§3.2 b): a room when it is a mesh, skinned mesh or particle
        /// renderer, on layer 0 with no collider on its GameObject, not part of a body, not on the
        /// shell list, and its world bounds lie in plan inside one floor box grown by
        /// <see cref="BoxGrowth"/>. Anything else is drawn always. Reads only; writes nothing.
        /// </summary>
        public Placement Classify(Renderer renderer, out int room)
        {
            room = -1;
            if (renderer == null || !(renderer is MeshRenderer || renderer is SkinnedMeshRenderer || renderer is ParticleSystemRenderer))
                return Placement.Kind;
            var piece = renderer.gameObject;
            if (piece.layer != 0) return Placement.Layer;
            if (piece.GetComponent<Collider>() != null) return Placement.Collider;
            if (IsBody(renderer.transform)) return Placement.Body;
            if (InShellList(renderer.transform)) return Placement.Shell;
            var bounds = renderer.bounds;
            room = RoomForPlan(bounds.min.x, bounds.max.x, bounds.min.z, bounds.max.z, out var placement);
            return placement;
        }

        /// <summary>
        /// The classification of every renderer in the scope's house, active or not, in hierarchy
        /// order: the table RS2a applies. Reads only.
        /// </summary>
        public void Classify(List<Classified> into)
        {
            into.Clear();
            if (query == null) return;
            rootBuffer.Clear();
            query.Scene.GetRootGameObjects(rootBuffer);
            foreach (var root in rootBuffer)
            {
                rendererBuffer.Clear();
                root.GetComponentsInChildren(true, rendererBuffer);
                foreach (var renderer in rendererBuffer)
                {
                    var placement = Classify(renderer, out int room);
                    into.Add(new Classified(renderer, room, placement));
                }
            }
        }

        /// <summary>Whether a piece is on the shell list: the neon room outlines and the authored shell.</summary>
        public static bool InShellList(Transform piece)
        {
            if (piece == null) return false;
            if (piece.name == ShellMeshName) return true;
            if (piece.name == SouthWingOutlineName && piece.parent != null && piece.parent.name == SouthWingRoot) return true;
            for (var above = piece.parent; above != null; above = above.parent)
                if (above.name == NeonOutlinesRoot) return true;
            return false;
        }

        /// <summary>Whether a piece belongs to a body: a houseguest's or the player's.</summary>
        public static bool IsBody(Transform piece)
            => piece != null && (piece.GetComponentInParent<HouseNpc>(true) != null || piece.GetComponentInParent<HousePlayerController>(true) != null);

        // ------------------------------------------------------------------------------------
        // Lamps (§3.2 b, d).
        // ------------------------------------------------------------------------------------

        /// <summary>A lamp: a point light that casts shadows. The fills, the screens' lights and the Moon are not.</summary>
        public static bool IsLamp(Light light) => light != null && light.type == LightType.Point && light.shadows != LightShadows.None;

        /// <summary>Every lamp in the scope's house, active or not, with the room it stands in.</summary>
        public void LampHomes(List<LampHome> into)
        {
            into.Clear();
            if (query == null) return;
            rootBuffer.Clear();
            query.Scene.GetRootGameObjects(rootBuffer);
            foreach (var root in rootBuffer)
            {
                lightBuffer.Clear();
                root.GetComponentsInChildren(true, lightBuffer);
                foreach (var light in lightBuffer)
                    if (IsLamp(light)) into.Add(new LampHome(light, RoomUnder(light.transform.position)));
            }
        }
    }
}
