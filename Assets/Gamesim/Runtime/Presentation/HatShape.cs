using UnityEngine;

namespace Gamesim.Presentation
{
    /// <summary>
    /// The inside of a hat, carried on the hat, and the hair under it held there. A cap or a beanie
    /// is fitted to the head - its skin and a little room for hair pressed flat - not to everything
    /// the hair carries: fitted over the hair it stood off big hair like a mushroom, and a head of
    /// long hair still came through the back of it. The hair the hat covers is pressed in under it
    /// instead, and below the band it may stand out across the head only as fast as it falls, so it
    /// comes out from under the rim close to the head and fans out as it goes down, as hair does
    /// under a hat - rather than flaring out from under the rim at full volume, over the hat's back:
    /// UMA's own hair, in the body's mesh, and hair grown on the head.
    ///
    /// <para>The hat's dome is kept in the head's own space, a line from its centre out through the
    /// band and up to the crown for every bearing: how far the hat reaches in any direction is read
    /// off it, and a point is under the hat when it is above the band at its bearing.</para>
    ///
    /// <para>Below the band hair is held in across the head - towards the line up through its
    /// middle - and never up or down. Held instead within a set distance of the head's middle,
    /// hair hanging straight down past the shoulders was out of reach however it hung, and was
    /// drawn up into the neck: long hair came out of a cap shortened by a hand's width, and box
    /// braids by twenty centimetres.</para>
    ///
    /// <para>Hair is held by its points, and drawn by its triangles. A hair card runs from under the
    /// hat to well below the band in one long triangle, so a card held inside at its top and fanned
    /// out at its foot crossed the hat just above the band, straight through the cloth. So a point
    /// below the band that shares a triangle with a point under the hat is tethered: held in far
    /// enough that the edge between them passes the band <see cref="Crossing"/> inside it. Every
    /// copy of a point shares its tether: a grown shell gives each of its triangles corners of its
    /// own, and a copy tethered in one triangle and free in the next tore the shell open all round
    /// just under the band.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HatShape : MonoBehaviour
    {
        /// <summary>How far inside the hat's surface the hair is held, for the cloth's thickness and the dome's facets.</summary>
        public const float Inset = .010f;
        /// <summary>
        /// How far hair below the band may stand out, for each metre it has fallen below it: out
        /// from under the rim close to the head, and no more than this fanning out as it goes down.
        /// </summary>
        public const float Flare = .7f;
        /// <summary>How far inside the rim, across the head, the edge from a covered point to a tethered one passes the band.</summary>
        public const float Crossing = .007f;

        // Head-local space to the hat's frame and back; the frame is the head scan's, in which the
        // dome was built, so up is the head's up and the dome's columns run round it by bearing.
        private Matrix4x4 headToFrame, frameToHead;
        private Vector3 centre;
        private int rows, columns;
        private float[] elevations, reaches;

        /// <summary>Puts the shape of <paramref name="dome"/> - rows from the band up to the crown, columns by bearing all the way round - on a fitted hat.</summary>
        internal static HatShape Attach(GrownPiece hat, HeadScan scan, Vector3[,] dome)
        {
            if (hat == null || scan == null || dome == null) return null;
            var shape = hat.gameObject.AddComponent<HatShape>();
            var head = hat.transform;
            shape.headToFrame = scan.FrameToWorld.inverse * head.localToWorldMatrix;
            shape.frameToHead = shape.headToFrame.inverse;
            shape.centre = scan.Centre;
            shape.rows = dome.GetLength(0);
            shape.columns = dome.GetLength(1);
            shape.elevations = new float[shape.rows * shape.columns];
            shape.reaches = new float[shape.rows * shape.columns];
            for (int c = 0; c < shape.columns; c++)
            {
                float highest = -90f;
                for (int r = 0; r < shape.rows; r++)
                {
                    var d = dome[r, c] - scan.Centre;
                    float reach = d.magnitude;
                    // The rows climb to the crown; kept climbing, so a smoothed ring a hair lower
                    // than the one under it cannot fold the line back on itself.
                    highest = Mathf.Max(highest, reach > 1e-5f ? Mathf.Asin(Mathf.Clamp(d.y / reach, -1f, 1f)) * Mathf.Rad2Deg : 90f);
                    shape.elevations[r * shape.columns + c] = highest;
                    shape.reaches[r * shape.columns + c] = reach;
                }
            }
            return shape;
        }

        /// <summary>
        /// Where <paramref name="world"/> is held by the hat: pulled in under it when the hat covers
        /// it and it stands outside; below the band, pulled in across the head to no further out than
        /// the rim plus <see cref="Flare"/> times how far it has fallen below it; left where it is
        /// anywhere else. A limit, not a nudge: held once or many times, a point ends in the same place.
        /// </summary>
        public Vector3 Hold(Vector3 world)
        {
            var v = ToFrame(world);
            var held = HoldInFrame(v, float.PositiveInfinity);
            return held == v ? world : FromFrame(held);
        }

        /// <summary>
        /// A point in the hat's frame, from the head's middle, held: below the band no further out
        /// across the head than <paramref name="limit"/> either.
        /// </summary>
        private Vector3 HoldInFrame(Vector3 v, float limit)
        {
            if (v.sqrMagnitude < 1e-10f) return v;
            var held = v;
            Read(v, out float elevation, out float band, out float reach, out float bandReach);
            if (elevation < band)
            {
                Rim(band, bandReach, out float rimY, out float rim);
                float allowed = Mathf.Max(Mathf.Min(rim - Inset + Flare * Mathf.Max(0f, rimY - v.y), limit), 0f);
                float across = Mathf.Sqrt(v.x * v.x + v.z * v.z);
                if (across <= allowed) return v;
                held = new Vector3(v.x * allowed / across, v.y, v.z * allowed / across);
                // Drawn in level just under the band, a point can come under the hat's edge: then it
                // is held as the hat holds what it covers, too.
                if (held.sqrMagnitude < 1e-10f) return held;
                Read(held, out elevation, out band, out reach, out _);
                if (elevation < band) return held;
            }
            float distance = held.magnitude;
            float inside = reach - Inset;
            return distance <= inside ? held : held / distance * Mathf.Max(inside, 0f);
        }

        /// <summary>
        /// How far inside the hat <paramref name="world"/> is, when the hat covers it: negative when it
        /// comes through. Positive infinity where the hat does not cover it, below the band.
        /// </summary>
        public float Clearance(Vector3 world)
        {
            var v = ToFrame(world);
            float distance = v.magnitude;
            if (distance < 1e-5f) return float.PositiveInfinity;
            Read(v, out float elevation, out float band, out float reach, out _);
            return elevation >= band ? reach - distance : float.PositiveInfinity;
        }

        /// <summary>Whether the hat covers <paramref name="world"/>: above its band, at its bearing.</summary>
        public bool Covers(Vector3 world) => CoversInFrame(ToFrame(world));

        private bool CoversInFrame(Vector3 v)
        {
            if (v.sqrMagnitude < 1e-10f) return false;
            Read(v, out float elevation, out float band, out _, out _);
            return elevation >= band;
        }

        /// <summary>
        /// For a point the hat does not cover: how far it has fallen below the rim, and how far out
        /// across the head it stands past the rim. False, and nothing, for a point the hat covers.
        /// </summary>
        public bool UnderRim(Vector3 world, out float below, out float past)
        {
            below = past = 0f;
            var v = ToFrame(world);
            if (v.sqrMagnitude < 1e-10f) return false;
            Read(v, out float elevation, out float band, out _, out float bandReach);
            if (elevation >= band) return false;
            Rim(band, bandReach, out float rimY, out float rim);
            below = Mathf.Max(0f, rimY - v.y);
            past = Mathf.Sqrt(v.x * v.x + v.z * v.z) - rim;
            return true;
        }

        private Vector3 ToFrame(Vector3 world) => (headToFrame * transform.worldToLocalMatrix).MultiplyPoint3x4(world) - centre;

        private Vector3 FromFrame(Vector3 v) => (transform.localToWorldMatrix * frameToHead).MultiplyPoint3x4(v + centre);

        /// <summary>The rim at a bearing: how high it is above the head's middle, and how far out across the head.</summary>
        private static void Rim(float band, float bandReach, out float height, out float across)
        {
            height = bandReach * Mathf.Sin(band * Mathf.Deg2Rad);
            across = bandReach * Mathf.Cos(band * Mathf.Deg2Rad);
        }

        /// <summary>
        /// The elevation of <paramref name="v"/> (from the centre, in the hat's frame), the band's
        /// elevation at its bearing, and how far the hat reaches along it and at the band.
        /// </summary>
        private void Read(Vector3 v, out float elevation, out float band, out float reach, out float bandReach)
        {
            float distance = v.magnitude;
            elevation = Mathf.Asin(Mathf.Clamp(v.y / distance, -1f, 1f)) * Mathf.Rad2Deg;
            float bearing = Mathf.Atan2(v.x, v.z) * Mathf.Rad2Deg;
            float column = (bearing + 180f) / 360f * (columns - 1);
            int c0 = Mathf.Clamp(Mathf.FloorToInt(column), 0, columns - 1), c1 = Mathf.Min(c0 + 1, columns - 1);
            float f = Mathf.Clamp01(column - c0);
            band = Mathf.Lerp(elevations[c0], elevations[c1], f);
            bandReach = Mathf.Lerp(reaches[c0], reaches[c1], f);
            reach = Mathf.Lerp(ReachAlong(c0, elevation), ReachAlong(c1, elevation), f);
        }

        /// <summary>How far one column of the dome reaches at an elevation, between the rows either side of it.</summary>
        private float ReachAlong(int column, float elevation)
        {
            if (elevation <= elevations[column]) return reaches[column];
            for (int r = 1; r < rows; r++)
            {
                float upper = elevations[r * columns + column];
                if (elevation > upper) continue;
                float lower = elevations[(r - 1) * columns + column];
                float t = upper > lower ? (elevation - lower) / (upper - lower) : 1f;
                return Mathf.Lerp(reaches[(r - 1) * columns + column], reaches[r * columns + column], t);
            }
            return reaches[(rows - 1) * columns + column];
        }

        /// <summary>
        /// Holds every hair on <paramref name="body"/> under the hat: the hair grown on its head, and
        /// the hair in the body's own mesh - UMA's hair cards, told by their materials. Returns how
        /// many points were moved. Read and written as the body stands now, which is how it is fitted.
        /// </summary>
        public int HoldHairUnder(GameObject body)
        {
            if (body == null) return 0;
            int moved = 0;
            foreach (var piece in body.GetComponentsInChildren<GrownPiece>())
            {
                if (piece.name != ProceduralHair.RootName && piece.name != ProceduralHair.StrandsName) continue;
                var mesh = piece.Mesh;
                if (mesh == null || !mesh.isReadable) continue;
                var skin = piece.GetComponent<SkinnedMeshRenderer>();
                moved += skin != null ? HoldSkinned(skin, mesh, null, mesh.triangles, true) : HoldRigid(piece.transform, mesh);
            }
            foreach (var skin in body.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (skin.GetComponent<GrownPiece>() != null) continue;
                var mesh = skin.sharedMesh;
                if (mesh == null || !mesh.isReadable) continue;
                var hair = HairVertices(skin, mesh, out var hairTriangles);
                if (hair != null) moved += HoldSkinned(skin, mesh, hair, hairTriangles, false);
            }
            return moved;
        }

        private int HoldRigid(Transform holder, Mesh mesh)
        {
            var vertices = mesh.vertices;
            var frame = new Vector3[vertices.Length];
            for (int i = 0; i < vertices.Length; i++) frame[i] = ToFrame(holder.TransformPoint(vertices[i]));
            var limits = Limits(frame, mesh.triangles, null);
            int moved = 0;
            for (int i = 0; i < vertices.Length; i++)
            {
                var held = HoldInFrame(frame[i], limits[i]);
                if (held == frame[i]) continue;
                vertices[i] = holder.InverseTransformPoint(FromFrame(held));
                moved++;
            }
            if (moved > 0) { mesh.SetVertices(vertices); mesh.RecalculateBounds(); }
            return moved;
        }

        /// <summary>
        /// How far out across the head each point below the band may stand for the edges it shares
        /// with points the hat covers - among <paramref name="only"/>, when given - to pass the band
        /// <see cref="Crossing"/> inside it; positive infinity for a point that shares none. The same
        /// for every copy of a point. Points are in the hat's frame, from the head's middle.
        /// </summary>
        private float[] Limits(Vector3[] frame, int[] triangles, bool[] only)
        {
            int count = frame.Length;
            var covered = new bool[count];
            var held = new Vector3[count];
            var limits = new float[count];
            for (int i = 0; i < count; i++)
            {
                limits[i] = float.PositiveInfinity;
                if (only != null && !only[i]) continue;
                covered[i] = CoversInFrame(frame[i]);
                if (covered[i]) held[i] = HoldInFrame(frame[i], float.PositiveInfinity);
            }
            bool any = false;
            for (int t = 0; t + 2 < triangles.Length; t += 3)
                for (int k = 0; k < 3; k++)
                {
                    int free = triangles[t + k];
                    if (covered[free] || (only != null && !only[free])) continue;
                    for (int j = 1; j < 3; j++)
                    {
                        int under = triangles[t + (k + j) % 3];
                        if (!covered[under]) continue;
                        limits[free] = Mathf.Min(limits[free], Tether(held[under], frame[free]));
                        any = true;
                    }
                }
            if (!any) return limits;
            // Copies of one point, one tether.
            var shared = new System.Collections.Generic.Dictionary<Vector3Int, float>();
            for (int i = 0; i < count; i++)
            {
                if (float.IsPositiveInfinity(limits[i])) continue;
                var key = Vector3Int.RoundToInt(frame[i] * 4000f);
                shared[key] = shared.TryGetValue(key, out float limit) ? Mathf.Min(limit, limits[i]) : limits[i];
            }
            for (int i = 0; i < count; i++)
                if (!covered[i] && (only == null || only[i]) && shared.TryGetValue(Vector3Int.RoundToInt(frame[i] * 4000f), out float limit))
                    limits[i] = Mathf.Min(limits[i], limit);
            return limits;
        }

        /// <summary>
        /// How far out across the head <paramref name="free"/>, below the band, may stand for the
        /// straight edge to it from <paramref name="under"/>, held under the hat, to pass the band
        /// <see cref="Crossing"/> inside the rim. Never less than the rim less <see cref="Inset"/>:
        /// held further in than that it would go into the head.
        /// </summary>
        private float Tether(Vector3 under, Vector3 free)
        {
            Read(free, out _, out float band, out _, out float bandReach);
            Rim(band, bandReach, out float rimY, out float rim);
            float rise = under.y - rimY;
            if (rise <= 0f) return float.PositiveInfinity;
            float fall = Mathf.Max(0f, rimY - free.y);
            float across = Mathf.Sqrt(under.x * under.x + under.z * under.z);
            // Where the edge passes the rim's height, a share of the way from the covered point.
            float share = rise / (rise + fall);
            return Mathf.Max(across + (rim - Crossing - across) / share, rim - Inset);
        }

        /// <summary>
        /// Holds a skinned mesh's points under the hat - only <paramref name="only"/>, when given. Each
        /// point is carried to where its bones hold it now, held there, and carried back through the
        /// same bones, so the mesh at rest is changed by just what keeps it under the hat: exact for
        /// the body as it stands, which is how it is fitted. With <paramref name="asTheHeadHoldsIt"/>
        /// a point is read as the head holds it instead, as though it went wherever the head goes -
        /// for grown strands, which hang onto the neck: read through the neck as well, what was held
        /// depended on how the head was turned against it when they were fitted.
        /// </summary>
        private int HoldSkinned(SkinnedMeshRenderer skin, Mesh mesh, bool[] only, int[] triangles, bool asTheHeadHoldsIt)
        {
            var bones = skin.bones;
            var bindposes = mesh.bindposes;
            var weights = mesh.boneWeights;
            var vertices = mesh.vertices;
            if (weights.Length != vertices.Length || bindposes.Length == 0) return 0;
            var skinning = new Matrix4x4[bones.Length];
            for (int b = 0; b < bones.Length && b < bindposes.Length; b++)
                skinning[b] = bones[b] != null ? bones[b].localToWorldMatrix * bindposes[b] : Matrix4x4.identity;
            int head = asTheHeadHoldsIt && transform.parent != null ? System.Array.IndexOf(bones, transform.parent) : -1;
            var blends = new Matrix4x4[vertices.Length];
            var frame = new Vector3[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                if (only != null && !only[i]) continue;
                blends[i] = head >= 0 && head < skinning.Length ? skinning[head] : Blend(skinning, weights[i]);
                frame[i] = ToFrame(blends[i].MultiplyPoint3x4(vertices[i]));
            }
            var limits = Limits(frame, triangles, only);
            int moved = 0;
            for (int i = 0; i < vertices.Length; i++)
            {
                if (only != null && !only[i]) continue;
                var held = HoldInFrame(frame[i], limits[i]);
                if (held == frame[i]) continue;
                vertices[i] = blends[i].inverse.MultiplyPoint3x4(FromFrame(held));
                moved++;
            }
            if (moved > 0) mesh.SetVertices(vertices);
            return moved;
        }

        private static Matrix4x4 Blend(Matrix4x4[] skinning, BoneWeight w)
        {
            var m = new Matrix4x4();
            float total = 0f;
            void Add(int bone, float weight)
            {
                if (weight <= 0f || bone < 0 || bone >= skinning.Length) return;
                var s = skinning[bone];
                for (int k = 0; k < 16; k++) m[k] += s[k] * weight;
                total += weight;
            }
            Add(w.boneIndex0, w.weight0); Add(w.boneIndex1, w.weight1); Add(w.boneIndex2, w.weight2); Add(w.boneIndex3, w.weight3);
            // Four of however many weights UMA gave the point, which need not sum to one: as the GPU
            // draws them, renormalised.
            if (total <= 0f) return Matrix4x4.identity;
            for (int k = 0; k < 16; k++) m[k] /= total;
            return m;
        }

        /// <summary>
        /// The points of a body mesh drawn with a hair material - its hair cards, brows and beard -
        /// and the triangles that draw them; null for none.
        /// </summary>
        private static bool[] HairVertices(SkinnedMeshRenderer skin, Mesh mesh, out int[] triangles)
        {
            var materials = skin.sharedMaterials;
            bool[] hair = null;
            var drawn = new System.Collections.Generic.List<int>();
            for (int s = 0; s < mesh.subMeshCount && s < materials.Length; s++)
            {
                var material = materials[s];
                if (material == null || !IsHair(material)) continue;
                hair = hair ?? new bool[mesh.vertexCount];
                var submesh = mesh.GetTriangles(s);
                foreach (int index in submesh) hair[index] = true;
                drawn.AddRange(submesh);
            }
            triangles = drawn.ToArray();
            return hair;
        }

        private static bool IsHair(Material material)
            => material.name.IndexOf("Hair", System.StringComparison.OrdinalIgnoreCase) >= 0
               || (material.shader != null && material.shader.name.IndexOf("Hair", System.StringComparison.OrdinalIgnoreCase) >= 0);
    }
}
