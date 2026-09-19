using System.IO;
using System.Linq;
using Gamesim.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The baked floor textures and the floors they are laid on (MASTER-PLAN §4.3 "Textures" and
    /// Tier 2). Every texture the floor plan names is baked as an albedo and a normal pair, at a
    /// power of two no larger than 2048; a normal imports as a normal map and never as sRGB; and in
    /// the shipping scene every room floor wears its authored material, tiled to the floor.
    /// </summary>
    public sealed class AuthoredTextureTests
    {
        private const string EpisodeScene = "Assets/Gamesim/Scenes/EpisodeHouse.unity";

        [Test]
        public void EveryFloorTextureIsBakedAsAPowerOfTwoPairWithTheRightImportSettings()
        {
            foreach (var name in HouseFloorDressing.Plan.Select(f => f.Texture).Distinct())
            {
                foreach (var kind in new[] { "albedo", "normal" })
                {
                    string path = HouseFloorDressing.Textures + name + "_" + kind + ".png";
                    Assert.That(File.Exists(path), Is.True, path + " is baked by ArtSource/textures/bb_tex_floors.py");
                    var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                    Assert.That(importer, Is.Not.Null, path);
                    bool normal = kind == "normal";
                    Assert.That(importer.textureType, Is.EqualTo(normal ? TextureImporterType.NormalMap : TextureImporterType.Default), path);
                    Assert.That(importer.sRGBTexture, Is.EqualTo(!normal), path + ": colour is sRGB, a normal is data");
                    Assert.That(importer.wrapMode, Is.EqualTo(TextureWrapMode.Repeat), path + ": a floor tile repeats");
                    Assert.That(importer.mipmapEnabled, Is.True, path);
                    var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                    Assert.That(texture, Is.Not.Null, path);
                    Assert.That(Mathf.IsPowerOfTwo(texture.width) && Mathf.IsPowerOfTwo(texture.height), Is.True, path + ": power of two");
                    Assert.That(texture.width, Is.LessThanOrEqualTo(2048), path + ": 1024 for props, 2048 for hero pieces");
                    Assert.That(texture.width, Is.EqualTo(texture.height), path + ": a square tile");
                }
            }
        }

        [Test]
        public void TheShellWallWearsThePlasterAtTheFloorsTile()
        {
            foreach (var kind in new[] { "albedo", "normal" })
                Assert.That(File.Exists(HouseFloorDressing.Textures + HouseFloorDressing.WallTexture + "_" + kind + ".png"), Is.True,
                    "the plaster is baked by ArtSource/textures/bb_tex_walls.py");
            var wall = AssetDatabase.LoadAssetAtPath<Material>(HouseFloorDressing.WallMaterial);
            Assert.That(wall, Is.Not.Null, "the shell's extracted wall material");
            Assert.That(wall.GetTexture("_BaseMap"), Is.Not.Null, "plaster albedo");
            Assert.That(wall.GetTexture("_BaseMap").name, Is.EqualTo(HouseFloorDressing.WallTexture + "_albedo"));
            Assert.That(wall.GetTexture("_BumpMap"), Is.Not.Null, "plaster normal");
            Assert.That(wall.IsKeywordEnabled("_NORMALMAP"), Is.True);
            Assert.That(wall.GetTextureScale("_BaseMap").x, Is.EqualTo(1f / HouseFloorDressing.WallTile).Within(0.001f),
                "world-scale UVs: one metre is one UV unit, so a two-metre tile is a scale of a half");
            Assert.That(wall.GetColor("_BaseColor").maxColorComponent, Is.LessThan(0.35f), "the slab stays dark; the plaster is texture, not brightness");
        }

        [Test]
        public void EveryAuthoredMeshCarriesUvsForTiling()
        {
            // The box projection in bb_build is what lets a texture tile at world scale on every
            // piece; a mesh without UVs would show a texture as one stretched pixel.
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { AuthoredAssetImporter.Root.TrimEnd('/') }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase)) continue;
                foreach (var mesh in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>())
                    Assert.That(mesh.uv, Is.Not.Empty, path + ": " + mesh.name + " has no UVs");
            }
        }

        [Test]
        public void EveryRoomFloorWearsItsAuthoredMaterialTiledToTheFloor()
        {
            var scene = EditorSceneManager.OpenPreviewScene(EpisodeScene);
            try
            {
                var all = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true)).ToArray();
                foreach (var floor in HouseFloorDressing.Plan)
                {
                    var renderer = all.Where(t => t.name == floor.Name).Select(t => t.GetComponent<Renderer>()).FirstOrDefault(r => r != null);
                    Assert.That(renderer, Is.Not.Null, floor.Name);
                    var material = renderer.sharedMaterial;
                    Assert.That(material, Is.Not.Null, floor.Name);
                    Assert.That(AssetDatabase.GetAssetPath(material), Is.EqualTo(HouseFloorDressing.MaterialPath(floor.Name)),
                        floor.Name + " wears its own authored material asset");
                    Assert.That(material.GetTexture("_BaseMap"), Is.Not.Null, floor.Name + ": albedo");
                    Assert.That(material.GetTexture("_BaseMap").name, Is.EqualTo(floor.Texture + "_albedo"));
                    Assert.That(material.GetTexture("_BumpMap"), Is.Not.Null, floor.Name + ": normal");
                    Assert.That(material.IsKeywordEnabled("_NORMALMAP"), Is.True, floor.Name + ": the normal map is on");
                    var size = renderer.transform.lossyScale;
                    var tiling = material.GetTextureScale("_BaseMap");
                    Assert.That(tiling.x, Is.EqualTo(size.x / floor.Tile).Within(0.01f), floor.Name + ": one tile is " + floor.Tile + " m across");
                    Assert.That(tiling.y, Is.EqualTo(size.z / floor.Tile).Within(0.01f), floor.Name + ": one tile is " + floor.Tile + " m deep");
                    Assert.That(material.GetColor("_BaseColor").maxColorComponent, Is.LessThan(0.75f),
                        floor.Name + " is tinted toward the darkened set, not shown at full albedo");
                }
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }
    }
}
