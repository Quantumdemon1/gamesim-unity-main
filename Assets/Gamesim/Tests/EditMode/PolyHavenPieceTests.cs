using System.IO;
using System.Linq;
using Gamesim.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The Poly Haven pieces (VISUAL-TARGET.md V4): each ships its four maps with the right import
    /// settings, a URP material that wears them, a per-piece script naming its source, and a mesh
    /// inside the budget with the UVs the textures need.
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
    }
}
