using System.Collections;
using System.Linq;
using Gamesim.House;
using Gamesim.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// Faces (MASTER-PLAN §3.B): a body's mood and stress reach its eyes as blend-shape weights,
    /// built at runtime on the shipped mesh, and the director pushes every houseguest's two fields
    /// to their body on each render.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        [UnityTest]
        public IEnumerator Faces_MoodAndStressReachTheEyesAsBlendShapes()
        {
            // A shipped Quaternius body, instantiated for the test: under GAMESIM_UMA the cast is
            // UMA, whose bodies have no Face material and no expression yet (Route A in the plan).
            var prefab = Resources.Load<GameObject>("GamesimCharacters/maya-hassan");
            Assert.That(prefab, Is.Not.Null);
            var body = Object.Instantiate(prefab);
            body.name = "Faces test body";
            try
            {
                var face = FaceExpression.Attach(body);
                Assert.That(face, Is.Not.Null);
                Assert.That(face.HasFace, Is.True, "The Quaternius body's Face material gives it eyes to move: " + face.BindReport);
                var renderer = body.GetComponentInChildren<SkinnedMeshRenderer>(true);
                Assert.That(renderer.sharedMesh.blendShapeCount, Is.EqualTo(FaceExpression.Shapes.Length), "Five shapes on the mesh.");
                foreach (var shape in FaceExpression.Shapes)
                {
                    var deltas = new Vector3[renderer.sharedMesh.vertexCount];
                    renderer.sharedMesh.GetBlendShapeFrameVertices(renderer.sharedMesh.GetBlendShapeIndex(shape), 0, deltas, null, null);
                    Assert.That(deltas.Count(d => d.sqrMagnitude > 1e-10f), Is.GreaterThan(8), shape + " moves the eyes' vertices.");
                }

                face.ReducedMotion = true;
                face.SetMood("Happy", "Normal");
                Assert.That(face.Weight(FaceExpression.Happy), Is.EqualTo(100f).Within(0.01f), "Happy is a full smile of the eyes.");
                Assert.That(face.Weight(FaceExpression.Angry), Is.EqualTo(0f).Within(0.01f));
                face.SetMood("Angry", "Overwhelmed");
                Assert.That(face.Weight(FaceExpression.Angry), Is.EqualTo(100f).Within(0.01f), "Angry: the inner corners down.");
                Assert.That(face.Weight(FaceExpression.Wide), Is.EqualTo(100f).Within(0.01f), "Overwhelmed: the eyes wide.");
                Assert.That(face.Weight(FaceExpression.Happy), Is.EqualTo(0f).Within(0.01f));

                face.ReducedMotion = false;
                face.SetMood("Neutral", "Normal");
                yield return null;
                Assert.That(face.Weight(FaceExpression.Angry), Is.GreaterThan(1f).And.LessThan(100f), "Off reduced motion the change eases.");
                float deadline = Time.realtimeSinceStartup + 3f;
                while (face.Weight(FaceExpression.Angry) > 0.5f && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(face.Weight(FaceExpression.Angry), Is.LessThan(0.5f), "and lands.");
            }
            finally
            {
                Object.Destroy(body);
            }
        }

        [UnityTest]
        public IEnumerator Faces_TheDirectorPushesEveryHouseguestsMoodToTheirBody()
        {
            yield return SettleCast();
            var state = director.Snapshot;
            var someone = state.contestants.First(c => !c.isPlayer && c.status == Simulation.ContestantStatus.Active);
            var npc = SceneComponents<HouseNpc>().First(n => n.Id == someone.id);
            var face = npc.GetComponentInChildren<FaceExpression>(true);
            Assert.That(face, Is.Not.Null, "Every body carries a face component, whether or not its mesh has eyes to move.");
            Assert.That(face.Mood, Is.EqualTo(someone.mood), "The body wears the simulation's mood.");
            Assert.That(face.Stress, Is.EqualTo(someone.stressLevel), "and its stress.");
        }
    }
}
