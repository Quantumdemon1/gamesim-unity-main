using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Gamesim.Editor;
using NUnit.Framework;
using Unity.Collections;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Gamesim.Tests.EditMode
{
    // Native import proof, not a Blender result, render approval or performance measurement.
    public sealed partial class FlightCaseAssetTests
    {
        internal const string ModelPath = "Assets/Gamesim/Art/Authored/SetPieces/bb_set_flight_case.fbx";
        // ArtSource/setpieces/bb_set_flight_case.py through bb_export.export_collection (-Z forward, Y up).
        internal const string ModelSha256 = "92c78d9891fe35619ba2145b19e991082ae073a0393f0abdd0396c2dec780ce9";
        internal const string MaterialPath = "Assets/Gamesim/Art/Authored/SetPieces/Materials/bb_mat_flightcase_atlas.mat";
        private const string TextureRoot = "Assets/Gamesim/Art/Authored/Textures/FlightCase/";
        private const string EpisodeScene = HouseRoomFinish.EpisodeScene;
        private const double PositionEpsilon = 1e-6, UvEpsilon = 1e-7, NormalEpsilon = 1e-5;
        // ArtSource/setpieces/bb_set_flight_case.py's SHARP_DEGREES: a seam flatter than this is smooth.
        private const float CreaseDegrees = 5f;
        private static readonly string[] MapKinds = { "albedo", "normal", "metallic", "occlusion" };
        private static readonly string[] MapHashes = {
            "176db42160c7ee0ec30075925796cfd11ae4bb75bae69ba225b27adb22c16ac4",
            "f5712f381e678b7af757dabe439bb24537c9369e11e329fb2403b52456272f01",
            "912ed2679d26056ea792817c146cb7135901fe39dc6ab54e01fc46dfd2bfd432",
            "85e229fd6fdd63b4f8c0c7d3a972a155063f73f9de46e2c6426b9cb996d9f01d" };

        [TestCase(0)]
        [TestCase(1)]
        public void NativeCornersKeepThePredeclaredAxisWindingNormalsAndAtlas(int level)
        {
            var root = Model();
            var mesh = Level(root, level).GetComponent<MeshFilter>().sharedMesh;
            var expected = FbxReference.Read(level);
            var actual = NativeTriangles(mesh);
            Assert.That(expected.Length, Is.EqualTo(level == 0 ? 11792 : 2648));
            Assert.That(actual.Length, Is.EqualTo(expected.Length), "No decimation, dropped faces or extra topology.");
            MatchOrientedMultiset(expected, actual);
            if (level == 0) VerifyComparatorControls(expected);
            // Import can reorder/split/weld vertices and generate UV2; neither changes the UV0 corners.
            Assert.That(mesh.isReadable, Is.False, "Do not toggle import readability for this proof.");
        }

        [TestCase(0)]
        [TestCase(1)]
        public void TheExportGivesEachCornerOneNormalPerSurface(int level)
        {
            // Unity's import keeps one normal for one vertex with one UV whose normals nearly agree,
            // so two normals there survive only as the same surface (within the guard) or a real
            // crease. Shaded flat, the export split each quad into triangles with normals of their
            // own, up to 1.2 degrees apart across a non-planar bevel quad's diagonal, and the native
            // corners case refused the import by 0.0132. The script smooths seams under 5 degrees;
            // the shallowest real crease is 12.6.
            int pairs = 0;
            foreach (var corner in FbxReference.Read(level).SelectMany(t => t).GroupBy(c => (c.Position, c.Uv)))
            {
                var normals = corner.Select(c => c.Normal).Distinct().ToArray();
                for (int a = 0; a < normals.Length; a++)
                    for (int b = a + 1; b < normals.Length; b++, pairs++)
                    {
                        double apart = Math.Max(Math.Abs((double)normals[a].x - normals[b].x),
                            Math.Max(Math.Abs((double)normals[a].y - normals[b].y), Math.Abs((double)normals[a].z - normals[b].z)));
                        float degrees = Vector3.Angle(normals[a], normals[b]);
                        if (apart > NormalEpsilon && degrees < CreaseDegrees)
                            Assert.Fail("Corner " + corner.Key.Position.ToString("F4") + " uv " + corner.Key.Uv.ToString("F4") + " carries normals "
                                + degrees.ToString("G3") + " degrees (" + apart.ToString("G3") + ") apart: neither one surface nor a crease.");
                    }
            }
            Assert.That(pairs, Is.GreaterThan(0), "The bevels' creases meet at shared corners; a reader that saw none read nothing.");
        }

        [TestCase(0)]
        [TestCase(1)]
        public void IndependentAsymmetricLandmarksKeepFrontRearFeetAndLid(int level)
        {
            var triangles = NativeTriangles(Level(Model(), level).GetComponent<MeshFilter>().sharedMesh);
            var label = Graphic(triangles, 4);
            var rear = Graphic(triangles, 7);
            // The house's set-piece convention: Blender's -y front faces Unity -z (bb_set_kitchenrun,
            // bb_set_comp_backdrop, the signs), so the label is on the -z face and the rear on +z.
            Near(Centre(label), new Vector3(0f, .421f, -.2265f), 1e-6f, "Broadcast label is on the front (-z), not the rear.");
            Near(Centre(rear), new Vector3(.277f, .186f, .234f), 1e-6f,
                "The asymmetric rear LIVE mark independently detects X reflection and front sign.");
            AssertClockwise(label, Vector3.back, Vector3.up);
            AssertClockwise(rear, Vector3.forward, Vector3.up);
            var corners = triangles.SelectMany(t => t).ToArray();
            Near(Bounds(corners.Select(c => c.Position)).min, new Vector3(-.45f, 0f, -.25f), 1e-6f, "Bottom-centred metres.");
            Near(Bounds(corners.Select(c => c.Position)).max, new Vector3(.45f, .5f, .25f), 1e-6f, "Metre envelope.");
            // Entire native footprint, not only equal .5m height/depth; feet occupy the bottom .024m.
            var rubber = corners.Where(c => Tile(c.Uv) == 3 && c.Position.y < .024001f).ToArray();
            Assert.That(rubber, Is.Not.Empty);
            Near(Centre(rubber), new Vector3(0f, .012f, 0f), 1e-6f, "Four original bottom feet.");
            var lid = corners.Where(c => Tile(c.Uv) == 0 && c.Position.y > .345f && c.Position.y < .484f
                && Mathf.Abs(c.Position.x) < .426f && Mathf.Abs(c.Position.z) < .226f
                && (Mathf.Abs(c.Position.x) > .38f || Mathf.Abs(c.Position.z) > .21f)).ToArray();
            Assert.That(lid, Is.Not.Empty);
            // Full corner-multiset case already proves every lid vertex; this independent height check
            // prevents a symmetric envelope alone from being reported as an axis proof.
            // Wide original lid perimeter excludes the separately authored narrower top inset.
            Assert.That(lid.Min(c => c.Position.y), Is.EqualTo(.346f).Within(1e-6f));
            Assert.That(lid.Max(c => c.Position.y), Is.EqualTo(.483f).Within(1e-6f));
            Assert.That(Centre(lid).y, Is.EqualTo(.4145f).Within(1e-6f), "Original lid centre is height, not depth.");
        }

        [Test]
        public void TheModelHasOneNativeLodGroupWithTwoExclusiveUnscaledLevels()
        {
            var root = Model();
            var importer = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
            Assert.That(importer.bakeAxisConversion && importer.useFileScale && importer.useFileUnits, Is.True);
            Assert.That(importer.globalScale, Is.EqualTo(1f));
            Assert.That(importer.isReadable || importer.addCollider || importer.importAnimation, Is.False);
            Assert.That(importer.importNormals, Is.EqualTo(ModelImporterNormals.Import));
            Assert.That(root.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(root.GetComponentsInChildren<Animation>(true), Is.Empty);
            Assert.That(root.GetComponentsInChildren<Animator>(true), Is.Empty);
            var groups = root.GetComponentsInChildren<LODGroup>(true);
            Assert.That(groups.Length, Is.EqualTo(1), "Suffixes alone are not a native LOD result.");
            var levels = groups[0].GetLODs();
            Assert.That(levels.Length, Is.EqualTo(2));
            var seen = new HashSet<Renderer>();
            for (int i = 0; i < 2; i++)
            {
                Assert.That(levels[i].renderers.Length, Is.EqualTo(1));
                Assert.That(levels[i].renderers[0], Is.SameAs(Level(root, i)));
                Assert.That(seen.Add(levels[i].renderers[0]), Is.True);
                Assert.That(Finite(levels[i].screenRelativeTransitionHeight), Is.True);
                Assert.That(levels[i].screenRelativeTransitionHeight, Is.InRange(0f, 1f));
                Assert.That(Finite(levels[i].fadeTransitionWidth), Is.True);
            }
            Assert.That(levels[0].screenRelativeTransitionHeight, Is.GreaterThan(levels[1].screenRelativeTransitionHeight));
            Assert.That(root.GetComponentsInChildren<Renderer>(true), Is.EquivalentTo(seen), "No independently rendered duplicate LOD.");
            foreach (var item in root.GetComponentsInChildren<Transform>(true))
            {
                Near(item.localPosition, Vector3.zero, 1e-6f, item.name + " origin");
                Near(item.localScale, Vector3.one, 1e-6f, item.name + " scale");
                Assert.That(Quaternion.Angle(item.localRotation, Quaternion.identity), Is.LessThanOrEqualTo(1e-4f), item.name + " baked axes");
            }
            TestContext.WriteLine("Observed LOD metadata, not transition-quality acceptance: " +
                levels[0].screenRelativeTransitionHeight + "/" + levels[1].screenRelativeTransitionHeight +
                " fade=" + groups[0].fadeMode + " bias=" + QualitySettings.lodBias + " max=" + QualitySettings.maximumLODLevel);
        }

        [Test]
        public void BothLevelsUseThePersistentOpaqueUrpAtlasAndFourPinnedMaps()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            Assert.That(material, Is.Not.Null);
            Assert.That(material.shader.name, Is.EqualTo("Universal Render Pipeline/Lit"));
            Assert.That(material.name, Is.EqualTo("bb_mat_flightcase_atlas"));
            Assert.That(material.GetFloat("_Surface"), Is.Zero);
            Assert.That(material.GetFloat("_ZWrite"), Is.EqualTo(1f));
            Assert.That(material.GetFloat("_Cull"), Is.EqualTo(2f));
            Assert.That(material.GetColor("_BaseColor"), Is.EqualTo(Color.white));
            Assert.That(material.GetFloat("_BumpScale"), Is.EqualTo(1f));
            Assert.That(material.GetFloat("_Metallic"), Is.EqualTo(1f));
            Assert.That(material.GetFloat("_Smoothness"), Is.EqualTo(1f));
            Assert.That(material.GetFloat("_SmoothnessTextureChannel"), Is.Zero);
            Assert.That(material.GetFloat("_OcclusionStrength"), Is.EqualTo(1f));
            Assert.That(material.shaderKeywords.OrderBy(x => x, StringComparer.Ordinal), Is.EqualTo(
                new[] { "_METALLICSPECGLOSSMAP", "_NORMALMAP", "_OCCLUSIONMAP" }));
            var properties = new[] { "_BaseMap", "_BumpMap", "_MetallicGlossMap", "_OcclusionMap" };
            for (int i = 0; i < 4; i++)
            {
                var path = TextureRoot + "bb_flightcase_" + MapKinds[i] + ".png";
                var bytes = ReadPinned(path, MapHashes[i], 2 * 1024 * 1024);
                Assert.That(bytes.Length, Is.GreaterThan(0));
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                Assert.That(texture, Is.Not.Null);
                Assert.That(texture.width, Is.EqualTo(1024)); Assert.That(texture.height, Is.EqualTo(1024));
                Assert.That(material.GetTexture(properties[i]), Is.SameAs(texture));
                Assert.That(material.GetTextureScale(properties[i]), Is.EqualTo(Vector2.one));
                Assert.That(material.GetTextureOffset(properties[i]), Is.EqualTo(Vector2.zero));
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                Assert.That(importer.sRGBTexture, Is.EqualTo(i == 0));
                Assert.That(importer.textureType, Is.EqualTo(i == 1 ? TextureImporterType.NormalMap : TextureImporterType.Default));
                Assert.That(importer.mipmapEnabled && !importer.isReadable, Is.True);
                Assert.That(importer.wrapMode, Is.EqualTo(TextureWrapMode.Repeat));
                if (i == 1) Assert.That(importer.flipGreenChannel, Is.False);
            }
            foreach (var renderer in Model().GetComponentsInChildren<Renderer>(true))
            {
                Assert.That(renderer.sharedMaterials.Length, Is.EqualTo(1));
                Assert.That(renderer.sharedMaterials[0], Is.SameAs(material));
                Assert.That(AssetDatabase.GetAssetPath(renderer.sharedMaterial), Is.EqualTo(MaterialPath));
            }
        }

        private static GameObject Model()
        {
            ReadPinned(ModelPath, ModelSha256, 2 * 1024 * 1024);
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            Assert.That(root, Is.Not.Null, "Actual native import required.");
            return root;
        }
        private static Renderer Level(GameObject root, int i) => root.GetComponentsInChildren<Renderer>(true)
            .Single(r => r.name == "bb_set_flight_case_LOD" + i);
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static void Near(Vector3 actual, Vector3 expected, float epsilon, string why)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(epsilon), why + " x");
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(epsilon), why + " y");
            Assert.That(actual.z, Is.EqualTo(expected.z).Within(epsilon), why + " z");
        }
        private static Bounds Bounds(IEnumerable<Vector3> values)
        {
            var points = values.ToArray(); Assert.That(points, Is.Not.Empty);
            var result = new Bounds(points[0], Vector3.zero);
            foreach (var point in points) result.Encapsulate(point);
            return result;
        }
        private static int Tile(Vector2 uv) => Mathf.FloorToInt(uv.y * 4) * 4 + Mathf.FloorToInt(uv.x * 4);
        private static Corner[] Graphic(Corner[][] triangles, int tile)
        {
            var faces = triangles.Where(t => t.All(c => Tile(c.Uv) == tile)).ToArray();
            Assert.That(faces.Length, Is.EqualTo(2), "Original planar graphic has two triangles.");
            return faces.SelectMany(t => t).ToArray();
        }
        private static Vector3 Centre(Corner[] values) => Bounds(values.Select(c => c.Position)).center;
        private static void AssertClockwise(Corner[] faces, Vector3 outward, Vector3 up)
        {
            var right = Vector3.Cross(up, -outward);
            for (int i = 0; i < faces.Length; i += 3)
            {
                var a = faces[i + 1].Position - faces[i].Position;
                var b = faces[i + 2].Position - faces[i].Position;
                var signedArea = Vector3.Dot(a, right) * Vector3.Dot(b, up) - Vector3.Dot(a, up) * Vector3.Dot(b, right);
                Assert.That(signedArea, Is.LessThan(0f), "Clockwise from the labelled outward face; not mirrored double-sided compensation.");
                foreach (var corner in faces.Skip(i).Take(3)) Near(corner.Normal, outward, 1e-5f, "Original planar outward normal.");
            }
        }

        private readonly struct Corner
        {
            public readonly Vector3 Position, Normal;
            public readonly Vector2 Uv;
            public Corner(Vector3 position, Vector3 normal, Vector2 uv) { Position = position; Normal = normal; Uv = uv; }
        }
        private static Corner[][] NativeTriangles(Mesh mesh)
        {
            Assert.That(mesh, Is.Not.Null);
            Assert.That(mesh.subMeshCount, Is.EqualTo(1));
            using (var array = MeshUtility.AcquireReadOnlyMeshData(mesh))
            {
                var data = array[0];
                Assert.That(data.vertexCount, Is.InRange(1, 60000));
                Assert.That(data.HasVertexAttribute(VertexAttribute.Normal) && data.HasVertexAttribute(VertexAttribute.TexCoord0), Is.True);
                var sub = data.GetSubMesh(0);
                Assert.That(sub.topology, Is.EqualTo(MeshTopology.Triangles));
                Assert.That(sub.indexCount, Is.InRange(3, 45000));
                Assert.That(sub.indexCount % 3, Is.Zero);
                using (var positions = new NativeArray<Vector3>(data.vertexCount, Allocator.Temp))
                using (var normals = new NativeArray<Vector3>(data.vertexCount, Allocator.Temp))
                using (var uvs = new NativeArray<Vector2>(data.vertexCount, Allocator.Temp))
                using (var indices = new NativeArray<int>(sub.indexCount, Allocator.Temp))
                {
                    data.GetVertices(positions); data.GetNormals(normals); data.GetUVs(0, uvs);
                    data.GetIndices(indices, 0, true);
                    var result = new Corner[sub.indexCount / 3][];
                    for (int face = 0; face < result.Length; face++)
                    {
                        result[face] = new Corner[3];
                        for (int c = 0; c < 3; c++)
                        {
                            int v = indices[face * 3 + c]; Assert.That(v, Is.InRange(0, data.vertexCount - 1));
                            Assert.That(Finite(positions[v].x) && Finite(positions[v].y) && Finite(positions[v].z) &&
                                Finite(normals[v].x) && Finite(normals[v].y) && Finite(normals[v].z) &&
                                Finite(uvs[v].x) && Finite(uvs[v].y), Is.True);
                            result[face][c] = new Corner(positions[v], normals[v], uvs[v]);
                        }
                    }
                    return result;
                }
            }
        }
        private static (long, long, long) Bucket(Vector3 p) => ((long)Math.Floor(p.x / PositionEpsilon),
            (long)Math.Floor(p.y / PositionEpsilon), (long)Math.Floor(p.z / PositionEpsilon));
        private static bool Same(Corner a, Corner b) =>
            Math.Abs((double)a.Position.x-b.Position.x) <= PositionEpsilon && Math.Abs((double)a.Position.y-b.Position.y) <= PositionEpsilon &&
            Math.Abs((double)a.Position.z-b.Position.z) <= PositionEpsilon &&
            Math.Abs((double)a.Normal.x-b.Normal.x) <= NormalEpsilon && Math.Abs((double)a.Normal.y-b.Normal.y) <= NormalEpsilon &&
            Math.Abs((double)a.Normal.z-b.Normal.z) <= NormalEpsilon &&
            Math.Abs((double)a.Uv.x-b.Uv.x) <= UvEpsilon && Math.Abs((double)a.Uv.y-b.Uv.y) <= UvEpsilon;
        private static void MatchOrientedMultiset(Corner[][] expected, Corner[][] actual)
        {
            var buckets = new Dictionary<(long, long, long), HashSet<int>>();
            for (int i = 0; i < expected.Length; i++)
                foreach (var corner in expected[i])
                {
                    var key = Bucket(corner.Position);
                    if (!buckets.TryGetValue(key, out var values)) buckets.Add(key, values = new HashSet<int>());
                    values.Add(i);
                }
            var consumed = new HashSet<int>();
            for (int n = 0; n < actual.Length; n++)
            {
                var p = Bucket(actual[n][0].Position);
                var candidates = new SortedSet<int>();
                for (int x = -1; x <= 1; x++) for (int y = -1; y <= 1; y++) for (int z = -1; z <= 1; z++)
                    if (buckets.TryGetValue((p.Item1+x, p.Item2+y, p.Item3+z), out var rows)) candidates.UnionWith(rows);
                int selected = -1;
                foreach (int i in candidates)
                {
                    if (consumed.Contains(i)) continue;
                    for (int shift = 0; shift < 3; shift++)
                        if (Enumerable.Range(0, 3).All(c => Same(expected[i][(c+shift)%3], actual[n][c]))) { selected = i; break; }
                    if (selected >= 0) break;
                }
                if (selected < 0)
                    Assert.Fail("Native face " + n + ": predeclared file H=(x,y,-z), Blender M=(x,z,y), one winding reversal; no fitted basis/normalization. "
                        + Closest(expected, actual[n], candidates, consumed));
                consumed.Add(selected);
            }
            Assert.That(consumed.Count, Is.EqualTo(expected.Length), "Each original oriented face occurs exactly once.");
        }

        /// <summary>A refusal's evidence: the nearest original face, by how far each attribute misses its guard.</summary>
        private static string Closest(Corner[][] expected, Corner[] face, IEnumerable<int> candidates, HashSet<int> consumed)
        {
            double Max(Func<int, double> component) => Enumerable.Range(0, 3).Max(component);
            double best = double.MaxValue; string said = "no original face within a position bucket";
            foreach (int i in candidates)
                for (int shift = 0; shift < 3; shift++)
                {
                    double dp = 0, dn = 0, du = 0;
                    for (int c = 0; c < 3; c++)
                    {
                        var e = expected[i][(c + shift) % 3]; var a = face[c];
                        dp = Math.Max(dp, Max(k => Math.Abs((double)e.Position[k] - a.Position[k])));
                        dn = Math.Max(dn, Max(k => Math.Abs((double)e.Normal[k] - a.Normal[k])));
                        du = Math.Max(du, Math.Max(Math.Abs((double)e.Uv.x - a.Uv.x), Math.Abs((double)e.Uv.y - a.Uv.y)));
                    }
                    double score = Math.Max(dp / PositionEpsilon, Math.Max(dn / NormalEpsilon, du / UvEpsilon));
                    if (score >= best) continue;
                    best = score;
                    said = "Nearest original face " + i + (consumed.Contains(i) ? " (already matched)" : "") + ", shift " + shift
                        + ": position " + dp.ToString("G3") + ", normal " + dn.ToString("G3") + ", uv " + du.ToString("G3") + ".";
                }
            return said;
        }

        private static void VerifyComparatorControls(Corner[][] source)
        {
            // Detached controls test this comparator, NOT an alternative successful native import.
            MatchOrientedMultiset(source, source.Reverse().Select(t => new[] { t[1], t[2], t[0] }).ToArray());
            int face = Array.FindIndex(source, t => Vector3.Cross(t[1].Position - t[0].Position,
                t[2].Position - t[0].Position).sqrMagnitude > 1e-10f);
            Assert.That(face, Is.GreaterThanOrEqualTo(0));
            for (int field = 0; field < 4; field++)
            {
                var changed = source.Select(t => t.ToArray()).ToArray(); var old = changed[face][0];
                if (field == 0) changed[face][0] = new Corner(old.Position + Vector3.right * .001f, old.Normal, old.Uv);
                if (field == 1) changed[face][0] = new Corner(old.Position, old.Normal, old.Uv + Vector2.right * .001f);
                if (field == 2) changed[face][0] = new Corner(old.Position, old.Normal + Vector3.right * .001f, old.Uv);
                if (field == 3) (changed[face][1], changed[face][2]) = (changed[face][2], changed[face][1]);
                Assert.Throws<AssertionException>(() => MatchOrientedMultiset(source, changed),
                    "Detached position/UV/normal/winding control " + field + " must refuse without raising its numerical guard.");
            }
        }

        private static byte[] ReadPinned(string relative, string sha256, int cap)
        {
            var project = Path.GetDirectoryName(Application.dataPath);
            var path = Path.GetFullPath(Path.Combine(project, relative));
            Assert.That(ReparseInput(path, project), Is.Null, "No reparse input inside the project.");
            byte[] bytes;
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                Assert.That(stream.Length, Is.InRange(1L, (long)cap));
                int length = checked((int)stream.Length); bytes = new byte[length]; int offset = 0;
                while (offset < length)
                {
                    int read = stream.Read(bytes, offset, length - offset);
                    Assert.That(read, Is.GreaterThan(0)); offset += read;
                }
                Assert.That(stream.ReadByte(), Is.EqualTo(-1)); Assert.That(stream.Length, Is.EqualTo(length));
            }
            using (var digest = SHA256.Create())
                Assert.That(BitConverter.ToString(digest.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(), Is.EqualTo(sha256));
            return bytes;
        }

        /// <summary>
        /// The first link (a reparse point: junction, symbolic link, redirected folder) on the way from
        /// <paramref name="path"/> up to <paramref name="project"/>, the file itself included and the
        /// project folder not, or null. Only the pinned path inside the project is the test's to refuse:
        /// where the checkout itself is reached from is the machine's, and the sha256 pins the bytes.
        /// A path outside the project is walked to its drive root.
        /// </summary>
        internal static string ReparseInput(string path, string project)
        {
            var root = Path.GetFullPath(project).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            for (var item = new FileInfo(Path.GetFullPath(path)) as FileSystemInfo;
                item != null && !string.Equals(item.FullName.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), root, StringComparison.OrdinalIgnoreCase);
                item = item is FileInfo file ? file.Directory : ((DirectoryInfo)item).Parent)
                if ((item.Attributes & FileAttributes.ReparsePoint) != 0) return item.FullName;
            return null;
        }
    }
}
