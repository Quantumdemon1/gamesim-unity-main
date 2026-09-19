using System.IO;
using System.Linq;
using Gamesim.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Faces (MASTER-PLAN §3.B) need the bodies' vertices at runtime: every model a character
    /// prefab draws imports Read/Write, carries a Face material, and yields the five shapes.
    /// </summary>
    public sealed class CharacterFaceTests
    {
        private static string[] Prefabs => Directory.GetFiles("Assets/Gamesim/Resources/GamesimCharacters", "*.prefab")
            .Select(p => p.Replace('\\', '/')).ToArray();

        [Test]
        public void EveryBodyAPrefabDrawsIsReadableAndHasAFace()
        {
            Assert.That(Prefabs, Is.Not.Empty);
            foreach (var path in Prefabs)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var renderer = prefab.GetComponentInChildren<SkinnedMeshRenderer>(true);
                Assert.That(renderer, Is.Not.Null, path + " draws a skinned body.");
                var mesh = renderer.sharedMesh;
                var model = AssetDatabase.GetAssetPath(mesh);
                var importer = AssetImporter.GetAtPath(model) as ModelImporter;
                Assert.That(importer != null && importer.isReadable, Is.True, model + " must import Read/Write: the faces are built from its vertices at runtime.");
                Assert.That(mesh.isReadable, Is.True, model + "'s mesh must be readable.");
                int face = System.Array.FindIndex(renderer.sharedMaterials, m => m != null && m.name.StartsWith("Face"));
                Assert.That(face, Is.GreaterThanOrEqualTo(0), path + " carries a Face material.");
                var built = FaceExpression.WithShapes(mesh, face, renderer.transform);
                Assert.That(built, Is.Not.Null, path + ": the five shapes build.");
                Assert.That(built.blendShapeCount, Is.EqualTo(FaceExpression.Shapes.Length));
                var deltas = new Vector3[built.vertexCount];
                built.GetBlendShapeFrameVertices(0, 0, deltas, null, null);
                Assert.That(deltas.Count(d => d.sqrMagnitude > 1e-10f), Is.GreaterThan(8), path + ": the eyes move.");
            }
        }
    }
}
