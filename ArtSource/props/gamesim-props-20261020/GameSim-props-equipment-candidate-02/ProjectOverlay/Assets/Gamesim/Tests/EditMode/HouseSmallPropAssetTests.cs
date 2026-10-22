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
    // These cases inspect actual native imports. They do not create assets, change readability,
    // rebuild a scene or require the saved scene to have adopted the new props yet.
    public sealed class HouseSmallPropAssetTests
    {
        private const string Root = "Assets/Gamesim/Art/Authored/HouseSmallProps/";
        public sealed class ExpectedAsset
        {
            public readonly string Id, FbxHash, LabelMaterial;
            public readonly Vector3 Size;
            public readonly int Triangles;
            public readonly string[] Materials;
            public readonly bool NormalMap;
            public ExpectedAsset(string id, Vector3 size, int triangles, string hash, bool normal, string labelMaterial, params string[] materials)
            { Id = id; Size = size; Triangles = triangles; FbxHash = hash; NormalMap = normal; LabelMaterial = labelMaterial; Materials = materials; }
            public override string ToString() => Id;
        }

        // Independent asset receipt: fixed native bytes and authored dimensions, not production helper data.
        public static IEnumerable<ExpectedAsset> Cases()
        {
            yield return new ExpectedAsset("bb_set_coffee_tin", new Vector3(0.12f, 0.16f, 0.12f), 1344, "07e59d9b0437c2f45923ce14f4fdca02546650bd49d28a094ac4c23a410f24da", false, "bb_mat_bb_set_coffee_tin", "bb_mat_bb_set_coffee_tin", "bb_mat_bb_set_coffee_tin_trim");
            yield return new ExpectedAsset("bb_set_food_jar", new Vector3(0.11f, 0.14f, 0.11f), 1056, "7ab202b3524f3c2ca709c3f11ca768e8ddec1b9c09c7f45b92a108ad32d0ead4", false, "bb_mat_bb_set_food_jar", "bb_mat_bb_set_food_jar", "bb_mat_bb_set_food_jar_trim");
            yield return new ExpectedAsset("bb_set_cereal_carton", new Vector3(0.2f, 0.3f, 0.07f), 432, "a66ad574bf3838feebc39755f1f1d0d5e3e551eedbba3b9d8a7b2cc1115dfcd2", false, "bb_mat_bb_set_cereal_carton", "bb_mat_bb_set_cereal_carton", "bb_mat_bb_set_cereal_carton_trim", "bb_mat_bb_set_cereal_carton_base");
            yield return new ExpectedAsset("bb_set_snack_blue", new Vector3(0.16f, 0.22f, 0.05f), 1424, "01d2c555304029bb7bd21c96e731204fc56d707e1a0a40d7c56766f532f425d2", false, "bb_mat_bb_set_snack_blue", "bb_mat_bb_set_snack_blue", "bb_mat_bb_set_snack_blue_trim");
            yield return new ExpectedAsset("bb_set_snack_gold", new Vector3(0.16f, 0.22f, 0.05f), 1424, "714ae079a56e9f70986eb9441920cea626d9adf4739a25fcf836fa8da45e7bc9", false, "bb_mat_bb_set_snack_gold", "bb_mat_bb_set_snack_gold", "bb_mat_bb_set_snack_gold_trim");
            yield return new ExpectedAsset("bb_set_hoh_snack_blue", new Vector3(0.14f, 0.2f, 0.05f), 1424, "7d590ff2fe4257c69a383067175c735392605855a724f4b7b4aed67fb5ed229c", false, "bb_mat_bb_set_hoh_snack_blue", "bb_mat_bb_set_hoh_snack_blue", "bb_mat_bb_set_hoh_snack_blue_trim");
            yield return new ExpectedAsset("bb_set_hoh_snack_gold", new Vector3(0.14f, 0.2f, 0.05f), 1424, "0f7f610dd87fab2fffc4b5746b8eb5e03129cb4afa1cec6be3d3fe280f5fdef2", false, "bb_mat_bb_set_hoh_snack_gold", "bb_mat_bb_set_hoh_snack_gold", "bb_mat_bb_set_hoh_snack_gold_trim");
            yield return new ExpectedAsset("bb_set_game_strategy", new Vector3(0.3f, 0.05f, 0.2f), 216, "ee320717271adb6fdb83c48a6c5cfe5cd29e90876e3975d9bbbcd1146104ff4e", false, "bb_mat_bb_set_game_strategy", "bb_mat_bb_set_game_strategy_base", "bb_mat_bb_set_game_strategy", "bb_mat_bb_set_game_strategy_trim");
            yield return new ExpectedAsset("bb_set_game_alliance", new Vector3(0.3f, 0.05f, 0.2f), 216, "4f4cc77879545d705d9bbbbc0af97a41e8d3e7bf444e57797f6eda2816080a29", false, "bb_mat_bb_set_game_alliance", "bb_mat_bb_set_game_alliance_base", "bb_mat_bb_set_game_alliance", "bb_mat_bb_set_game_alliance_trim");
            yield return new ExpectedAsset("bb_set_game_social", new Vector3(0.3f, 0.05f, 0.2f), 216, "8b2e24005c44ce8ea181c6785a1ce5fb9faff79774278b5f6383ddf12b2369ea", false, "bb_mat_bb_set_game_social", "bb_mat_bb_set_game_social_base", "bb_mat_bb_set_game_social", "bb_mat_bb_set_game_social_trim");
            yield return new ExpectedAsset("bb_set_toiletry", new Vector3(0.06f, 0.1f, 0.06f), 864, "0ed6c5ad0769201c311654bf3879fe86ccf0040e614b318cffcc6b53af3b26d8", false, "bb_mat_bb_set_toiletry", "bb_mat_bb_set_toiletry", "bb_mat_bb_set_toiletry_trim");
            yield return new ExpectedAsset("bb_set_cosmetic", new Vector3(0.06f, 0.1f, 0.06f), 768, "290a006e5361c3800115b86105078c0455e7ecfbf96c0b8a1dd068674eeef265", false, "bb_mat_bb_set_cosmetic", "bb_mat_bb_set_cosmetic", "bb_mat_bb_set_cosmetic_trim");
            yield return new ExpectedAsset("bb_set_mini_fridge", new Vector3(0.5f, 0.7f, 0.5f), 3880, "191111bf96e239e22054bf30ced58d49c7b262b5f6597ce66401df0ecf5617c1", true, "bb_mat_mini_fridge_atlas", "bb_mat_mini_fridge_atlas");
            yield return new ExpectedAsset("bb_set_headphone_case", new Vector3(0.16f, 0.06f, 0.16f), 2308, "4c5618775da308879c0902ce2d7e3715db4db378a0803a97f4f430dd85623e61", true, "bb_mat_headphone_case_atlas", "bb_mat_headphone_case_atlas");
        }

        [TestCaseSource(nameof(Cases))]
        public void NativeImportKeepsMetresBottomCentreTopologyAndSurfaceData(ExpectedAsset expected)
        {
            string path = Root + expected.Id + ".fbx";
            Assert.That(File.Exists(path), Is.True, "Admit this reviewed native file before running import tests: " + path);
            using (var sha = SHA256.Create())
            using (var stream = File.OpenRead(path))
                Assert.That(BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant(), Is.EqualTo(expected.FbxHash));
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(root, Is.Not.Null, path);
            Assert.That(root.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(root.GetComponentsInChildren<Animator>(true), Is.Empty);
            Assert.That(root.transform.localPosition.sqrMagnitude, Is.LessThan(1e-10f));
            Assert.That(Vector3.Distance(root.transform.localScale, Vector3.one), Is.LessThan(1e-5f));
            var bounds = root.GetComponentsInChildren<Renderer>(true).Select(r => r.bounds)
                .Aggregate((a, b) => { a.Encapsulate(b); return a; });
            Assert.That(Vector3.Distance(bounds.size, expected.Size), Is.LessThan(0.0001f), "Unscaled Unity XYZ envelope.");
            Assert.That(new Vector3(bounds.center.x, bounds.min.y, bounds.center.z).magnitude,
                Is.LessThan(0.0001f), "The root is the support point, centred in XZ.");
            int triangles = 0;
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = filter.sharedMesh;
                Assert.That(mesh, Is.Not.Null);
                Assert.That(mesh.isReadable, Is.False, "No CPU copy added for a static prop.");
                using (var array = MeshUtility.AcquireReadOnlyMeshData(mesh))
                {
                    var data = array[0];
                    Assert.That(data.HasVertexAttribute(VertexAttribute.Normal), Is.True);
                    Assert.That(data.HasVertexAttribute(VertexAttribute.TexCoord0), Is.True);
                    Assert.That(data.HasVertexAttribute(VertexAttribute.Tangent), Is.True);
                    using (var positions = new NativeArray<Vector3>(data.vertexCount, Allocator.Temp))
                    using (var normals = new NativeArray<Vector3>(data.vertexCount, Allocator.Temp))
                    using (var uvs = new NativeArray<Vector2>(data.vertexCount, Allocator.Temp))
                    using (var tangents = new NativeArray<Vector4>(data.vertexCount, Allocator.Temp))
                    {
                        data.GetVertices(positions); data.GetNormals(normals); data.GetUVs(0, uvs); data.GetTangents(tangents);
                        for (int v = 0; v < data.vertexCount; v++)
                        {
                            Assert.That(Finite(positions[v].x) && Finite(positions[v].y) && Finite(positions[v].z), Is.True);
                            Assert.That(Finite(normals[v].x) && Finite(normals[v].y) && Finite(normals[v].z), Is.True);
                            Assert.That(normals[v].sqrMagnitude, Is.EqualTo(1f).Within(0.002f));
                            Assert.That(uvs[v].x, Is.InRange(-0.00001f, 1.00001f));
                            Assert.That(uvs[v].y, Is.InRange(-0.00001f, 1.00001f));
                            var t = tangents[v];
                            Assert.That(Finite(t.x) && Finite(t.y) && Finite(t.z) && Finite(t.w), Is.True);
                            Assert.That(t.x * t.x + t.y * t.y + t.z * t.z, Is.EqualTo(1f).Within(0.002f));
                            Assert.That(Mathf.Abs(t.w), Is.EqualTo(1f).Within(0.00001f));
                        }
                        for (int submesh = 0; submesh < data.subMeshCount; submesh++)
                        {
                            var sub = data.GetSubMesh(submesh);
                            Assert.That(sub.topology, Is.EqualTo(MeshTopology.Triangles));
                            Assert.That(sub.indexCount % 3, Is.Zero);
                            triangles += sub.indexCount / 3;
                            using (var indices = new NativeArray<int>(sub.indexCount, Allocator.Temp))
                            {
                                data.GetIndices(indices, submesh, true);
                                for (int i = 0; i < indices.Length; i++) Assert.That(indices[i], Is.InRange(0, data.vertexCount - 1));
                                for (int i = 0; i < indices.Length; i += 3)
                                {
                                    var a = positions[indices[i]]; var b = positions[indices[i + 1]]; var c = positions[indices[i + 2]];
                                    Assert.That(Vector3.Cross(b - a, c - a).sqrMagnitude, Is.GreaterThan(1e-24f), "No collapsed triangle.");
                                }
                            }
                        }
                    }
                }
            }
            Assert.That(triangles, Is.EqualTo(expected.Triangles), "Import neither drops nor adds topology.");
        }

        [TestCaseSource(nameof(Cases))]
        public void NativeSlotsUseTheExplicitExternalUrpMaterialsAndCorrectMapChannels(ExpectedAsset expected)
        {
            string path = Root + expected.Id + ".fbx";
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(root, Is.Not.Null, path);
            var materials = root.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).Distinct().ToArray();
            Assert.That(materials.All(m => m != null), Is.True);
            Assert.That(materials.Select(m => m.name), Is.EquivalentTo(expected.Materials));
            var remaps = AssetImporter.GetAtPath(path).GetExternalObjectMap();
            foreach (var material in materials)
            {
                Assert.That(AssetDatabase.GetAssetPath(material), Is.EqualTo(Root + "Materials/" + material.name + ".mat"));
                var id = new AssetImporter.SourceAssetIdentifier(typeof(Material), material.name);
                Assert.That(remaps.TryGetValue(id, out var external) && external == material, Is.True);
                Assert.That(material.shader.name, Is.EqualTo("Universal Render Pipeline/Lit"));
                Assert.That(material.GetFloat("_Surface"), Is.Zero);
                Assert.That(material.GetFloat("_Cull"), Is.EqualTo((float)CullMode.Back));
                bool mapped = material.name == expected.LabelMaterial;
                Assert.That(material.IsKeywordEnabled("_METALLICSPECGLOSSMAP"), Is.EqualTo(mapped));
                Assert.That(material.IsKeywordEnabled("_NORMALMAP"), Is.EqualTo(mapped && expected.NormalMap));
                if (!mapped)
                {
                    Assert.That(material.GetTexture("_BaseMap"), Is.Null, "Solid trim/base never receives label UV art.");
                    Assert.That(material.GetTexture("_MetallicGlossMap"), Is.Null);
                    continue;
                }
                CheckMap(material, "_BaseMap", expected.Id, "albedo", true, false);
                CheckMap(material, "_MetallicGlossMap", expected.Id, "metallic", false, false);
                Assert.That(material.GetFloat("_SmoothnessTextureChannel"), Is.Zero, "Metallic alpha, never albedo alpha.");
                Assert.That(material.GetFloat("_Smoothness"), Is.EqualTo(1f));
                Assert.That(material.GetFloat("_Metallic"), Is.EqualTo(1f));
                if (expected.NormalMap)
                {
                    CheckMap(material, "_BumpMap", expected.Id, "normal", false, true);
                    Assert.That(material.GetFloat("_BumpScale"), Is.EqualTo(0.3f));
                }
                else Assert.That(material.GetTexture("_BumpMap"), Is.Null);
            }
        }

        [Test, Explicit("Run after the scene owner has admitted the props and saved the selected replacements.")]
        public void SavedEpisodeUsesExactlyTheSixteenSelectedAuthoredProps()
        {
            var scene = EditorSceneManager.OpenPreviewScene(HouseRoomFinish.EpisodeScene);
            try
            {
                var world = scene.GetRootGameObjects().Single(go => go.name == "House Architecture");
                var finish = world.transform.Find(HouseRoomFinish.RootName);
                Assert.That(finish, Is.Not.Null);
                var selected = finish.Cast<Transform>().SelectMany(group => group.Cast<Transform>())
                    .Where(t => (PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject) ?? "").StartsWith(Root, StringComparison.Ordinal)).ToArray();
                Assert.That(selected, Has.Length.EqualTo(16));
                foreach (var expected in Cases())
                {
                    string asset = Root + expected.Id + ".fbx";
                    int count = expected.Id == "bb_set_mini_fridge" || expected.Id == "bb_set_cosmetic" ? 2 : 1;
                    Assert.That(selected.Count(t => PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject) == asset), Is.EqualTo(count), expected.Id);
                }
                foreach (var instance in selected)
                {
                    Assert.That(instance.GetComponentsInChildren<Collider>(true), Is.Empty);
                    foreach (var node in instance.GetComponentsInChildren<Transform>(true))
                        Assert.That(GameObjectUtility.GetStaticEditorFlags(node.gameObject), Is.EqualTo((StaticEditorFlags)0));
                    foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
                        Assert.That(renderer.shadowCastingMode, Is.EqualTo(ShadowCastingMode.Off));
                }
                // RoomFinishTests independently check original support surfaces, stack heights,
                // room bounds and separation from the retained authored books and drinks.
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        private static void CheckMap(Material material, string property, string id, string kind, bool srgb, bool normal)
        {
            string path = Root + "Textures/" + id + "_" + kind + ".png";
            Assert.That(AssetDatabase.GetAssetPath(material.GetTexture(property)), Is.EqualTo(path));
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            Assert.That(importer, Is.Not.Null);
            Assert.That(importer.textureType, Is.EqualTo(normal ? TextureImporterType.NormalMap : TextureImporterType.Default));
            Assert.That(importer.sRGBTexture, Is.EqualTo(srgb));
            Assert.That(importer.mipmapEnabled, Is.True);
            Assert.That(importer.alphaIsTransparency, Is.False);
            Assert.That(importer.isReadable, Is.False);
        }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
