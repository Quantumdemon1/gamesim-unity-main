using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Gamesim.Editor;
using NUnit.Framework;
using Unity.Collections;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Where the room finish seats the sixteen authored house props (HouseSmallPropAssetTests' set),
    /// measured on their meshes and on every mesh drawn around them, in the saved scene and in what a
    /// run of the pass makes: each shows its front to its room, stands clear of everything else, and
    /// rests on what it stands on.
    ///
    /// <para>A model's front is its -z: the packaging's printed face and the middle of a wrapped label
    /// (texture u 0.5; the seam is on +z), the mini fridge's door, and the side a game box's lid art
    /// reads upright from. The props were authored with the front at Blender -Y and exported forward
    /// -Z (ArtSource/props, the packaging README), which the FBX bear out.</para>
    ///
    /// <para><b>Front</b>: the front within <see cref="FacingLimit"/> of the level direction from the
    /// prop to the middle of its room's floor. <b>Clear</b>: the slab <see cref="LabelReach"/> deep in
    /// front of the front face (<see cref="DoorReach"/> for a fridge door's swing), and the mesh's own
    /// box, each <see cref="Slack"/> in from every side, meet no triangle of another drawn mesh - a
    /// wall, the shell, furniture, another prop, the television's foot plate, the floor neon.
    /// <b>Resting</b>: under the middle of the base the highest surface within 5 cm below it is no more
    /// than <see cref="Slack"/> under the base, and under the middle and the four corners (80 % out) no
    /// surface stands more than <see cref="Slack"/> up into it. A flat quad lying on a surface (a rug,
    /// a start line, the letter on the HoH console) is a graphic, not a solid, and takes no part.
    /// Measuring adds no collider: the props stay collider-free.</para>
    ///
    /// <para>Each of these caught a defect of the 2026-10-25 review: both mini fridges and both HoH
    /// snack bags facing away (front), the game boxes' lids reading upside down (front), the cosmetic
    /// by the west wall showing its seam (front), the game-room fridge's door 0.2 m from the bar
    /// (clear), the HoH gold bag behind the screen (clear, resting), both HoH bags sunk 2 cm into the
    /// screen's foot plate (clear, resting), the kitchen blue bag 2 cm inside the toaster (clear,
    /// resting), and the HoH fridge standing over the floor neon (clear, resting). The gift box the
    /// finish stands among them on the HoH console is held to the same clear and resting checks: it
    /// went 6 cm up into the screen's frame (clear).</para>
    /// </summary>
    public sealed partial class RoomFinishTests
    {
        private const string HouseSmallProps = "Assets/Gamesim/Art/Authored/HouseSmallProps/";
        /// <summary>Degrees; every prop the pass seats is within 58 of its room's middle, and the faults were 70 to 180.</summary>
        private const float FacingLimit = 65f;
        /// <summary>Metres a side that count as touching, not overlap: the stacked game boxes touch exactly; the faults went 20 mm and more into their neighbours.</summary>
        private const float Slack = 0.003f;
        private const float LabelReach = 0.03f, DoorReach = 0.40f;
        private static readonly (string Group, string Floor)[] PropRooms =
        {
            ("Kitchen", "Kitchen floor"), ("HoH suite", "HoH floor"), ("Game room", "Games floor"), ("Bedroom", "Bedroom floor"),
        };

        [Test]
        public void TheSavedSceneShowsEveryAuthoredPropToItsRoomClearOfEverythingAndResting()
        {
            var scene = EditorSceneManager.OpenPreviewScene(EpisodeScene);
            try { AssertAuthoredPropsPlaced(scene); }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        /// <summary>The same, on what a run of the pass makes: a placement changed in the code fails here before any scene is saved.</summary>
        [Test]
        public void ARunOfThePassShowsEveryAuthoredPropToItsRoomClearOfEverythingAndResting()
        {
            var materials = OwnedMaterialState.Capture();
            var scene = default(UnityEngine.SceneManagement.Scene);
            try
            {
                scene = EditorSceneManager.OpenPreviewScene(EpisodeScene);
                HouseRoomFinish.Apply(scene);
                AssertAuthoredPropsPlaced(scene);
            }
            finally
            {
                try { materials.RestoreAndAssertUnchanged(); }
                finally { if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene); }
            }
        }

        private static void AssertAuthoredPropsPlaced(UnityEngine.SceneManagement.Scene scene)
        {
            var root = Root(scene);
            Assert.That(root, Is.Not.Null, "Run Gamesim/U07/Finish the rooms (pack 6).");
            var everything = scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<Transform>(true)).ToArray();
            var drawn = scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<MeshRenderer>(true))
                .Where(renderer => renderer.enabled && renderer.gameObject.activeInHierarchy && MeshOf(renderer) != null && !FlatGraphic(renderer)).ToList();
            var solids = new Solids();
            var failures = new List<string>();
            int props = 0;
            foreach (var (groupName, floorName) in PropRooms)
            {
                var group = root.Find(groupName);
                Assert.That(group, Is.Not.Null, groupName);
                var floor = everything.First(t => t.name == floorName && t.GetComponent<Renderer>() != null).GetComponent<Renderer>().bounds;
                foreach (Transform prop in group)
                {
                    string source = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(prop.gameObject) ?? "";
                    if (!source.StartsWith(HouseSmallProps, StringComparison.Ordinal)) continue;
                    props++;
                    string name = groupName + " / " + prop.name + "[" + prop.GetSiblingIndex() + "] (" + Path.GetFileNameWithoutExtension(source) + ")";
                    var own = prop.GetComponentsInChildren<MeshRenderer>(true).Where(renderer => MeshOf(renderer) != null).ToArray();
                    Assert.That(own, Is.Not.Empty, name);
                    var others = drawn.Where(renderer => !own.Contains(renderer)).ToList();
                    var bounds = BoundsOf(prop);

                    // Front.
                    Vector3 front = prop.rotation * Vector3.back, toMiddle = floor.center - bounds.center;
                    front.y = 0f; toMiddle.y = 0f;
                    float angle = Vector3.Angle(front, toMiddle);
                    if (angle > FacingLimit)
                        failures.Add(name + ": its front is " + angle.ToString("F1") + " degrees from the room's middle (limit " + FacingLimit + ").");

                    // Clear.
                    float reach = source.EndsWith("/bb_set_mini_fridge.fbx", StringComparison.Ordinal) ? DoorReach : LabelReach;
                    foreach (var renderer in own)
                    {
                        foreach (var (what, hit) in solids.Meeting(Box(renderer, Slack, reach), others))
                            failures.Add(name + ": the " + reach.ToString("F2") + " m in front of its front meets " + what + " (" + hit + " triangles).");
                        foreach (var (what, hit) in solids.Meeting(Box(renderer, Slack), others))
                            failures.Add(name + ": its mesh's box meets " + what + " (" + hit + " triangles).");
                    }

                    // Resting.
                    AddRestingFailures(name, own[0], bounds, others, solids, failures);
                }
            }
            Assert.That(props, Is.EqualTo(16), "The sixteen authored props, in the four rooms.");

            // The gift box on the HoH console is the room finish's own label-wrapped carton, not an
            // authored model, but it stands among the authored props there - under the screen's frame,
            // beside the foot plate, the two HoH snack bags and the drink - and the pass at 2822c021 put
            // it 6 cm up into the frame. Clear and resting, as a prop; gift wrap shows on every face, so
            // it has no front to turn.
            var gifts = root.Find("HoH suite").Cast<Transform>().Where(t => t.name == "Gift box").ToArray();
            Assert.That(gifts, Has.Length.EqualTo(1), "The one gift box on the HoH console.");
            var gift = gifts[0].GetComponent<MeshRenderer>();
            Assert.That(gift != null && MeshOf(gift) != null && drawn.Contains(gift), Is.True, "The gift box is a drawn solid.");
            var aroundTheGift = drawn.Where(renderer => renderer != gift).ToList();
            foreach (var (what, hit) in solids.Meeting(Box(gift, Slack), aroundTheGift))
                failures.Add("HoH suite / Gift box: its mesh's box meets " + what + " (" + hit + " triangles).");
            AddRestingFailures("HoH suite / Gift box", gift, gift.bounds, aroundTheGift, solids, failures);
            Assert.That(failures, Is.Empty, string.Join("\n", failures));
        }

        /// <summary>
        /// Resting: under the middle of the base the highest surface within 5 cm below it is no more
        /// than <see cref="Slack"/> under the base, and under the middle and the four corners (80 % out)
        /// no surface stands more than <see cref="Slack"/> up into it.
        /// </summary>
        private static void AddRestingFailures(string name, MeshRenderer body, Bounds bounds, List<MeshRenderer> others, Solids solids, List<string> failures)
        {
            float baseY = bounds.min.y, into = Mathf.Min(0.03f, bounds.size.y * 0.5f);
            var local = MeshOf(body).bounds;
            var corners = new[] { Vector2.zero, new Vector2(-.8f, -.8f), new Vector2(.8f, -.8f), new Vector2(.8f, .8f), new Vector2(-.8f, .8f) };
            for (int c = 0; c < corners.Length; c++)
            {
                var at = body.localToWorldMatrix.MultiplyPoint3x4(new Vector3(local.center.x + corners[c].x * local.extents.x, local.min.y,
                    local.center.z + corners[c].y * local.extents.z));
                var (surface, under) = solids.Highest(at.x, at.z, baseY - 0.05f, baseY + into, others);
                if (c == 0 && (!surface.HasValue || surface.Value < baseY - Slack))
                    failures.Add(name + ": nothing within " + (Slack * 1000f).ToString("F0") + " mm under the middle of its base at " + baseY.ToString("F3")
                        + (surface.HasValue ? " (the highest surface, " + under + ", is at " + surface.Value.ToString("F3") + ")" : "") + ".");
                if (surface.HasValue && surface.Value > baseY + Slack)
                {
                    failures.Add(name + ": " + under + " stands " + ((surface.Value - baseY) * 1000f).ToString("F0") + " mm up into its base.");
                    break;
                }
            }
        }

        private static Mesh MeshOf(Renderer renderer)
        {
            var filter = renderer.GetComponent<MeshFilter>();
            return filter != null ? filter.sharedMesh : null;
        }

        /// <summary>A quad lying flat - a rug, a line, a letter on a table: a graphic on a surface, not a solid.</summary>
        private static bool FlatGraphic(Renderer renderer) =>
            MeshOf(renderer).name == "Quad" && Mathf.Abs(renderer.transform.forward.y) > 0.99f;

        /// <summary>An oriented box: its centre, its three unit axes, its half extents.</summary>
        private readonly struct Obb
        {
            public readonly Vector3 Centre, X, Y, Z, Half;
            public Obb(Vector3 centre, Vector3 x, Vector3 y, Vector3 z, Vector3 half) { Centre = centre; X = x; Y = y; Z = z; Half = half; }

            public Bounds Aabb
            {
                get
                {
                    var extent = new Vector3(
                        Mathf.Abs(X.x) * Half.x + Mathf.Abs(Y.x) * Half.y + Mathf.Abs(Z.x) * Half.z,
                        Mathf.Abs(X.y) * Half.x + Mathf.Abs(Y.y) * Half.y + Mathf.Abs(Z.y) * Half.z,
                        Mathf.Abs(X.z) * Half.x + Mathf.Abs(Y.z) * Half.y + Mathf.Abs(Z.z) * Half.z);
                    return new Bounds(Centre, extent * 2f);
                }
            }

            public Vector3 Local(Vector3 world)
            {
                var d = world - Centre;
                return new Vector3(Vector3.Dot(d, X), Vector3.Dot(d, Y), Vector3.Dot(d, Z));
            }
        }

        /// <summary>
        /// The renderer's mesh box in the world, <paramref name="shrink"/> in from every side; with a
        /// <paramref name="reach"/>, the slab that deep in front of the box's -z face instead.
        /// </summary>
        private static Obb Box(MeshRenderer renderer, float shrink, float reach = 0f)
        {
            var matrix = renderer.localToWorldMatrix;
            var local = MeshOf(renderer).bounds;
            Vector3 x = matrix.GetColumn(0), y = matrix.GetColumn(1), z = matrix.GetColumn(2);
            var scale = new Vector3(x.magnitude, y.magnitude, z.magnitude);
            var half = new Vector3(Mathf.Max(0f, local.extents.x * scale.x - shrink), Mathf.Max(0f, local.extents.y * scale.y - shrink),
                Mathf.Max(0f, local.extents.z * scale.z - shrink));
            var centre = local.center;
            if (reach > 0f)
            {
                centre.z = local.min.z - reach * 0.5f / scale.z;
                half.z = reach * 0.5f;
            }
            return new Obb(matrix.MultiplyPoint3x4(centre), x / scale.x, y / scale.y, z / scale.z, half);
        }

        /// <summary>Drawn meshes' triangles in world space, read once per renderer (the editor reads an import that keeps no CPU copy).</summary>
        private sealed class Solids
        {
            private readonly Dictionary<MeshRenderer, Vector3[]> triangles = new Dictionary<MeshRenderer, Vector3[]>();

            private Vector3[] Of(MeshRenderer renderer)
            {
                if (triangles.TryGetValue(renderer, out var known)) return known;
                var world = new List<Vector3>();
                var matrix = renderer.localToWorldMatrix;
                using (var array = MeshUtility.AcquireReadOnlyMeshData(MeshOf(renderer)))
                {
                    var data = array[0];
                    using (var positions = new NativeArray<Vector3>(data.vertexCount, Allocator.Temp))
                    {
                        data.GetVertices(positions);
                        for (int sub = 0; sub < data.subMeshCount; sub++)
                        {
                            var descriptor = data.GetSubMesh(sub);
                            if (descriptor.topology != MeshTopology.Triangles) continue;
                            using (var indices = new NativeArray<int>(descriptor.indexCount, Allocator.Temp))
                            {
                                data.GetIndices(indices, sub, true);
                                for (int i = 0; i + 2 < indices.Length; i += 3)
                                {
                                    world.Add(matrix.MultiplyPoint3x4(positions[indices[i]]));
                                    world.Add(matrix.MultiplyPoint3x4(positions[indices[i + 1]]));
                                    world.Add(matrix.MultiplyPoint3x4(positions[indices[i + 2]]));
                                }
                            }
                        }
                    }
                }
                return triangles[renderer] = world.ToArray();
            }

            /// <summary>Every other renderer one of whose triangles meets the box, with how many do.</summary>
            public IEnumerable<(string What, int Count)> Meeting(Obb box, IEnumerable<MeshRenderer> others)
            {
                var near = box.Aabb;
                foreach (var other in others)
                {
                    if (!other.bounds.Intersects(near)) continue;
                    var tris = Of(other);
                    int count = 0;
                    for (int i = 0; i < tris.Length; i += 3)
                    {
                        Vector3 a = tris[i], b = tris[i + 1], c = tris[i + 2];
                        if (Mathf.Max(a.x, Mathf.Max(b.x, c.x)) < near.min.x || Mathf.Min(a.x, Mathf.Min(b.x, c.x)) > near.max.x
                            || Mathf.Max(a.y, Mathf.Max(b.y, c.y)) < near.min.y || Mathf.Min(a.y, Mathf.Min(b.y, c.y)) > near.max.y
                            || Mathf.Max(a.z, Mathf.Max(b.z, c.z)) < near.min.z || Mathf.Min(a.z, Mathf.Min(b.z, c.z)) > near.max.z) continue;
                        if (Meets(box, a, b, c)) count++;
                    }
                    if (count > 0) yield return (PathOf(other.transform), count);
                }
            }

            /// <summary>The highest surface over (<paramref name="x"/>, <paramref name="z"/>) between <paramref name="low"/> and <paramref name="high"/>, and whose it is.</summary>
            public (float? Height, string What) Highest(float x, float z, float low, float high, IEnumerable<MeshRenderer> others)
            {
                float? best = null;
                string what = null;
                foreach (var other in others)
                {
                    var b = other.bounds;
                    if (x < b.min.x || x > b.max.x || z < b.min.z || z > b.max.z || b.max.y < low || b.min.y > high) continue;
                    var tris = Of(other);
                    for (int i = 0; i < tris.Length; i += 3)
                    {
                        float? h = Under(tris[i], tris[i + 1], tris[i + 2], x, z);
                        if (h.HasValue && h.Value >= low && h.Value <= high && (!best.HasValue || h.Value > best.Value))
                        {
                            best = h;
                            what = PathOf(other.transform);
                        }
                    }
                }
                return (best, what);
            }
        }

        private static readonly Vector3[] BoxAxes = { Vector3.right, Vector3.up, Vector3.forward };

        /// <summary>The separating-axis test of an oriented box against a triangle: the box's three faces, the triangle's face and the nine edge crossings.</summary>
        private static bool Meets(Obb box, Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 p = box.Local(a), q = box.Local(b), r = box.Local(c);
            Vector3 e0 = q - p, e1 = r - q, e2 = p - r;
            foreach (var axis in BoxAxes)
                if (Apart(axis, p, q, r, box.Half)) return false;
            if (Apart(Vector3.Cross(e0, e1), p, q, r, box.Half)) return false;
            foreach (var axis in BoxAxes)
                if (Apart(Vector3.Cross(axis, e0), p, q, r, box.Half) || Apart(Vector3.Cross(axis, e1), p, q, r, box.Half)
                    || Apart(Vector3.Cross(axis, e2), p, q, r, box.Half)) return false;
            return true;
        }

        private static bool Apart(Vector3 axis, Vector3 p, Vector3 q, Vector3 r, Vector3 half)
        {
            if (axis.sqrMagnitude < 1e-14f) return false;
            float a = Vector3.Dot(p, axis), b = Vector3.Dot(q, axis), c = Vector3.Dot(r, axis);
            float reach = half.x * Mathf.Abs(axis.x) + half.y * Mathf.Abs(axis.y) + half.z * Mathf.Abs(axis.z);
            return Mathf.Min(a, Mathf.Min(b, c)) > reach || Mathf.Max(a, Mathf.Max(b, c)) < -reach;
        }

        /// <summary>The height at which the vertical line through (<paramref name="x"/>, <paramref name="z"/>) meets the triangle, or null.</summary>
        private static float? Under(Vector3 a, Vector3 b, Vector3 c, float x, float z)
        {
            float d = (b.z - c.z) * (a.x - c.x) + (c.x - b.x) * (a.z - c.z);
            if (Mathf.Abs(d) < 1e-12f) return null;
            float l1 = ((b.z - c.z) * (x - c.x) + (c.x - b.x) * (z - c.z)) / d;
            float l2 = ((c.z - a.z) * (x - c.x) + (a.x - c.x) * (z - c.z)) / d;
            float l3 = 1f - l1 - l2;
            if (l1 < -1e-6f || l2 < -1e-6f || l3 < -1e-6f) return null;
            return l1 * a.y + l2 * b.y + l3 * c.y;
        }

        private static string PathOf(Transform node)
        {
            var parts = new List<string>();
            for (var at = node; at != null; at = at.parent) parts.Add(at.name);
            parts.Reverse();
            return string.Join("/", parts);
        }
    }
}
