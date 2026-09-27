using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Gamesim.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The Poly Haven pieces and surfaces (VISUAL-TARGET.md V4): each ships its four maps with the
    /// right import settings, a URP material that wears them, a per-piece script naming its source,
    /// and a mesh inside the budget with the UVs the textures need; each surface ships the same four
    /// maps beside the baked floors, and is laid at the size it is in life.
    /// </summary>
    public sealed class PolyHavenPieceTests
    {
        [Test]
        public void EveryPolyHavenPieceShipsItsMapsMaterialScriptAndBudget()
        {
            var pieces = PolyHavenMaterials.PieceNames();
            Assert.That(pieces, Is.Not.Empty, "At least the hero sofa has arrived.");
            foreach (var piece in pieces)
            {
                foreach (var kind in new[] { PolyHavenMaterials.Albedo, PolyHavenMaterials.Normal, PolyHavenMaterials.Metallic, PolyHavenMaterials.Occlusion })
                {
                    string path = PolyHavenMaterials.TexturePath(piece, kind);
                    Assert.That(File.Exists(path), Is.True, path);
                    var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                    Assert.That(importer, Is.Not.Null, path);
                    bool normal = kind == PolyHavenMaterials.Normal;
                    bool data = kind == PolyHavenMaterials.Metallic || kind == PolyHavenMaterials.Occlusion;
                    Assert.That(importer.textureType, Is.EqualTo(normal ? TextureImporterType.NormalMap : TextureImporterType.Default), path);
                    Assert.That(importer.sRGBTexture, Is.EqualTo(!normal && !data), path + ": colour is sRGB, a normal and a data map are not");
                    var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                    Assert.That(texture, Is.Not.Null, path);
                    Assert.That(texture.width, Is.LessThanOrEqualTo(2048), path);
                }

                var material = AssetDatabase.LoadAssetAtPath<Material>(PolyHavenMaterials.MaterialPath(piece));
                Assert.That(material, Is.Not.Null, piece + ": Gamesim/U07/Build the Poly Haven materials");
                Assert.That(material.shader.name, Is.EqualTo("Universal Render Pipeline/Lit"));
                Assert.That(material.GetTexture("_BaseMap"), Is.Not.Null.And.Property("name").EqualTo(PolyHavenMaterials.Prefix + piece + "_albedo"));
                Assert.That(material.GetTexture("_BumpMap"), Is.Not.Null.And.Property("name").EqualTo(PolyHavenMaterials.Prefix + piece + "_normal"));
                Assert.That(material.GetTexture("_MetallicGlossMap"), Is.Not.Null, piece + ": metallic with smoothness in alpha");
                Assert.That(material.GetTexture("_OcclusionMap"), Is.Not.Null, piece);
                Assert.That(material.IsKeywordEnabled("_NORMALMAP") && material.IsKeywordEnabled("_METALLICSPECGLOSSMAP") && material.IsKeywordEnabled("_OCCLUSIONMAP"), Is.True, piece);

                string fbx = PolyHavenMaterials.PiecePath(piece);
                Assert.That(File.Exists(fbx), Is.True, fbx);
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
                Assert.That(model, Is.Not.Null, fbx);
                var renderer = model.GetComponentInChildren<MeshRenderer>();
                Assert.That(renderer, Is.Not.Null, fbx);
                Assert.That(renderer.sharedMaterial, Is.SameAs(material), fbx + ": the importer matched the material by name");
                var mesh = model.GetComponentInChildren<MeshFilter>().sharedMesh;
                Assert.That(mesh.triangles.Length / 3, Is.LessThanOrEqualTo(4200), fbx + ": inside the hero budget");
                Assert.That(mesh.uv, Is.Not.Empty, fbx + ": the textures need the model's own UVs");
                Assert.That(mesh.bounds.min.y, Is.EqualTo(0f).Within(0.01f), fbx + ": stands on the floor");

                string script = Path.Combine(Directory.GetCurrentDirectory(), "ArtSource", "setpieces", "bb_set_" + piece + ".py");
                Assert.That(File.Exists(script), Is.True, script + " names the Poly Haven source the piece is built from");
                Assert.That(File.ReadAllText(script), Does.Contain("bb_polyhaven").And.Contain("\"" + piece + "\""));
            }
        }

        /// <summary>
        /// The surfaces: the floors and the walls wear scans now, and a scan is only right if it is
        /// laid at its own size. Every Poly Haven surface the dressing names ships its four maps
        /// beside the baked ones, imports the way its suffix says, and is tiled at the metres
        /// <c>ArtSource/textures/bb_tex_polyhaven.py</c> records from the asset's page.
        /// </summary>
        [Test]
        public void EveryPolyHavenSurfaceShipsItsMapsAndIsLaidAtItsOwnSize()
        {
            var sizes = SurfaceSizes();
            Assert.That(sizes, Is.Not.Empty, "ArtSource/textures/bb_tex_polyhaven.py names the surfaces it fetches");

            var used = HouseFloorDressing.Plan.Select(floor => floor.Texture)
                .Concat(new[] { HouseFloorDressing.WallTexture })
                .Distinct().Where(name => name.StartsWith(SurfacePrefix)).ToArray();
            Assert.That(used, Is.Not.Empty, "the dressing lays at least one scanned surface");

            foreach (var name in used)
            {
                foreach (var kind in new[] { "albedo", "normal", "metallic", "occlusion" })
                {
                    string path = HouseFloorDressing.Textures + name + "_" + kind + ".png";
                    Assert.That(File.Exists(path), Is.True, path + " is fetched by ArtSource/textures/bb_tex_polyhaven.py");
                    var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                    Assert.That(importer, Is.Not.Null, path);
                    bool normal = kind == "normal";
                    bool data = kind == "metallic" || kind == "occlusion";
                    Assert.That(importer.textureType, Is.EqualTo(normal ? TextureImporterType.NormalMap : TextureImporterType.Default), path);
                    Assert.That(importer.sRGBTexture, Is.EqualTo(!normal && !data), path + ": colour is sRGB, a normal and a data map are not");
                    Assert.That(importer.wrapMode, Is.EqualTo(TextureWrapMode.Repeat), path + ": a surface repeats over a floor");
                    var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                    Assert.That(texture, Is.Not.Null, path);
                    Assert.That(Mathf.IsPowerOfTwo(texture.width) && texture.width == texture.height, Is.True, path + ": a square power of two");
                    Assert.That(texture.width, Is.LessThanOrEqualTo(2048), path);
                }

                Assert.That(sizes.ContainsKey(name), Is.True, name + " is not in bb_tex_polyhaven.py's list of surfaces");
                foreach (var floor in HouseFloorDressing.Plan.Where(f => f.Texture == name))
                    Assert.That(floor.Tile, Is.EqualTo(sizes[name]).Within(0.01f),
                        floor.Name + " lays " + name + " at " + floor.Tile + " m; the scan is " + sizes[name] + " m across");
                if (name == HouseFloorDressing.WallTexture)
                    Assert.That(HouseFloorDressing.WallTile, Is.EqualTo(sizes[name]).Within(0.01f),
                        "the shell's walls tile the plaster at its own size");
            }
        }

        /// <summary>
        /// The swaps the catalogue makes for V4: the plants and the dining chairs are scans, and
        /// each resolves to a piece that is actually on disk rather than to the kit.
        /// </summary>
        [Test]
        public void TheCatalogueSendsThePlantsAndTheChairsToTheirPolyHavenPieces()
        {
            var expected = new[]
            {
                ("pottedPlant", "bb_set_ph_plant"),
                ("plantSmall1", "bb_set_ph_plantsmall"),
                ("plantSmall2", "bb_set_ph_plantsmall"),
                ("plantSmall3", "bb_set_ph_plantsmall"),
                ("chairModernCushion", "bb_set_ph_diningchair"),
            };
            var replaced = HouseCatalogue.Replaced.ToDictionary(pair => pair.Key, pair => pair.Value);
            foreach (var (id, piece) in expected)
            {
                Assert.That(replaced.ContainsKey(id), Is.True, id);
                Assert.That(replaced[id], Is.EqualTo(piece), id);
                var model = HouseCatalogue.Resolve(id, out var tier);
                Assert.That(model, Is.Not.Null, id);
                Assert.That(tier, Is.EqualTo(HouseCatalogue.Tier.Authored), id + " resolves to the authored scan, not the kit");
                Assert.That(model.name, Is.EqualTo(piece));
            }
            Assert.That(HouseSetPieces.PlanModels.Count(id => id == "bb_set_ph_diningchair"), Is.EqualTo(16),
                "sixteen seats at the long table");
        }

        private const string SurfacePrefix = "bb_tex_ph_";

        /// <summary>Each surface's name and the metres one tile of it covers, from its own script.</summary>
        private static System.Collections.Generic.Dictionary<string, float> SurfaceSizes()
        {
            string script = Path.Combine(Directory.GetCurrentDirectory(), "ArtSource", "textures", "bb_tex_polyhaven.py");
            Assert.That(File.Exists(script), Is.True, script + " is where a surface's Poly Haven id and size are recorded");
            var sizes = new System.Collections.Generic.Dictionary<string, float>();
            foreach (Match match in Regex.Matches(File.ReadAllText(script), "Surface\\(\\s*\"([a-z0-9_]+)\"\\s*,\\s*\"([a-z0-9_]+)\"\\s*,\\s*([0-9.]+)"))
                sizes["bb_tex_" + match.Groups[2].Value] = float.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture);
            return sizes;
        }
    }
}
