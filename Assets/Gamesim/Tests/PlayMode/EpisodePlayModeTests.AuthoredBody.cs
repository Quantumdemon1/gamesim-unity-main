using System.Collections;
using System.Linq;
using Gamesim.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// An authored body on the shipped rig (ArtSource/characters/bb_char_dan_gheesling.py) is a
    /// houseguest like the six: it gets eyes that move, it carries the controller every clip is
    /// wired into, and its shirt keeps the sheet's colour because nothing on it is named for one.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        [UnityTest]
        public IEnumerator AuthoredBody_DanGheeslingGetsAFaceAndTheSharedController()
        {
            var prefab = Resources.Load<GameObject>("GamesimCharacters/dan-gheesling");
            Assert.That(prefab, Is.Not.Null, "Gamesim/U07/Build the authored character prefabs writes it.");
            var body = Object.Instantiate(prefab);
            body.name = "Authored body test";
            try
            {
                var face = FaceExpression.Attach(body);
                Assert.That(face, Is.Not.Null);
                Assert.That(face.HasFace, Is.True, "The Face material gives him eyes to move: " + face.BindReport);
                var renderer = body.GetComponentInChildren<SkinnedMeshRenderer>(true);
                Assert.That(renderer.sharedMesh.blendShapeCount, Is.EqualTo(FaceExpression.Shapes.Length), "Five shapes on the mesh.");
                face.ReducedMotion = true;
                face.SetMood("Happy", "Normal");
                Assert.That(face.Weight(FaceExpression.Happy), Is.EqualTo(100f).Within(0.01f));

                var animator = body.GetComponent<Animator>();
                Assert.That(animator, Is.Not.Null);
                Assert.That(animator.runtimeAnimatorController, Is.Not.Null.And.Property("name").EqualTo("GamesimCharacter"));
                var bones = body.GetComponentsInChildren<Transform>(true).Select(t => t.name).ToArray();
                foreach (var name in new[] { "CharacterArmature", "Hips", "Torso", "Head", "UpperArm.L", "LowerArm.R", "Foot.L" })
                    Assert.That(bones, Does.Contain(name), name + ": the clips bind by the shipped rig's paths.");

                var names = renderer.sharedMaterials.Where(m => m != null).Select(m => m.name).ToArray();
                Assert.That(names.Any(n => n.Contains("Shirt")), Is.False,
                    "The palette recolour finds a shirt by name; Dan's stays the sheet's black.");
                Assert.That(names, Does.Contain("bb_mat_dan_mouth"), "The grin is its own material, clear of the eye clusters.");
                yield return null;
            }
            finally
            {
                Object.Destroy(body);
            }
        }
    }
}
