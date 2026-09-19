using System.Linq;
using Gamesim.Editor;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// An authored body on the shipped rig: the importer takes it as a readable Generic rig, the
    /// prefab builder names it for its template, it faces the way the six bodies face, and the
    /// template wears it.
    /// </summary>
    public sealed class AuthoredCharacterTests
    {
        private const string Dan = "dan-gheesling";
        private const string DanModel = AuthoredCharacterPrefabs.Folder + "bb_char_dan_gheesling.fbx";
        private const string Prefabs = "Assets/Gamesim/Resources/GamesimCharacters/";

        [Test]
        public void ImporterTakesABodyUnderCharactersGenericAsAReadableGenericRig()
        {
            var body = AuthoredAssetImporter.Root + "Characters/Generic/bb_char_x.fbx";
            Assert.That(AuthoredAssetImporter.IsGenericCharacter(body), Is.True);
            Assert.That(AuthoredAssetImporter.IsGeneric(body), Is.True);
            Assert.That(AuthoredAssetImporter.IsRigged(body), Is.True);
            Assert.That(AuthoredAssetImporter.IsAnimation(body), Is.False, "A body is not a clip file.");

            var humanoid = AuthoredAssetImporter.Root + "Characters/bb_char_x.fbx";
            Assert.That(AuthoredAssetImporter.IsGenericCharacter(humanoid), Is.False, "Characters/ itself stays the Humanoid route.");
            Assert.That(AuthoredAssetImporter.IsGeneric(humanoid), Is.False);
            var clips = AuthoredAssetImporter.Root + "Animation/Generic/bb_anim_casual.fbx";
            Assert.That(AuthoredAssetImporter.IsGenericCharacter(clips), Is.False);
            Assert.That(AuthoredAssetImporter.IsGeneric(clips), Is.True, "The authored clips are still Generic.");
        }

        [Test]
        public void TheFileNameIsTheTemplateId()
        {
            Assert.That(AuthoredCharacterPrefabs.AppearanceIdOf("bb_char_dan_gheesling"), Is.EqualTo(Dan));
            Assert.That(AuthoredCharacterPrefabs.AppearanceIdOf("bb_char_x"), Is.EqualTo("x"));
            Assert.That(AuthoredCharacterPrefabs.AppearanceIdOf("bb_set_mug"), Is.Null, "A set piece is not a body.");
            Assert.That(AuthoredCharacterPrefabs.AppearanceIdOf("bb_char_"), Is.Null);
        }

        [Test]
        public void DanGheeslingsBodyImportsOnTheSharedRig()
        {
            var importer = AssetImporter.GetAtPath(DanModel) as ModelImporter;
            Assert.That(importer, Is.Not.Null, DanModel + " ships; ArtSource/characters/bb_char_dan_gheesling.py exports it.");
            Assert.That(importer.animationType, Is.EqualTo(ModelImporterAnimationType.Generic),
                "Every clip the game plays is a Generic clip on the shipped skeleton.");
            Assert.That(importer.isReadable, Is.True, "The face is built from the mesh at runtime.");
            Assert.That(importer.avatarSetup, Is.EqualTo(ModelImporterAvatarSetup.CreateFromThisModel), "The prefab carries the avatar.");

            var root = AssetDatabase.LoadAssetAtPath<GameObject>(DanModel);
            var rig = root.transform.Find("CharacterArmature");
            Assert.That(rig, Is.Not.Null, "The clips bind by the shipped rig's path.");
            var bones = rig.GetComponentsInChildren<Transform>(true).Select(t => t.name).ToArray();
            foreach (var name in new[] { "Hips", "Abdomen", "Torso", "Neck", "Head", "UpperArm.L", "LowerArm.R", "Fist.L", "UpperLeg.R", "LowerLeg.L", "Foot.R" })
                Assert.That(bones, Does.Contain(name), name);
        }

        [Test]
        public void DanGheeslingHasThePrefabTheRuntimeLoads()
        {
            var prefab = Resources.Load<GameObject>("GamesimCharacters/" + Dan);
            Assert.That(prefab, Is.Not.Null, "Gamesim/U07/Build the authored character prefabs writes it.");

            var animator = prefab.GetComponent<Animator>();
            Assert.That(animator, Is.Not.Null);
            Assert.That(animator.runtimeAnimatorController, Is.Not.Null.And.Property("name").EqualTo("GamesimCharacter"));
            Assert.That(animator.avatar, Is.Not.Null);
            Assert.That(animator.avatar.isHuman, Is.False, "A Generic avatar, like the six bodies.");
            Assert.That(animator.applyRootMotion, Is.False);
            Assert.That(animator.cullingMode, Is.EqualTo(AnimatorCullingMode.CullUpdateTransforms));

            var skinned = prefab.GetComponentInChildren<SkinnedMeshRenderer>(true);
            Assert.That(skinned, Is.Not.Null);
            Assert.That(skinned.sharedMesh.isReadable, Is.True);
            var materials = skinned.sharedMaterials.Where(m => m != null).Select(m => m.name).ToArray();
            Assert.That(materials.Any(m => m.StartsWith("Face")), Is.True, "FaceExpression finds the eyes by this name.");
            Assert.That(materials.Any(m => m.Contains("Shirt")), Is.False,
                "The palette recolour finds a shirt by name; Dan's is black on the sheet and must keep it.");
            Assert.That(materials, Does.Contain("bb_mat_dan_hair").And.Contain("bb_mat_dan_sneaker").And.Contain("bb_mat_dan_mouth"));
        }

        /// <summary>
        /// The eyes, in the prefab root's space, sit in front of the head bone for Casey; they must
        /// for Dan too, or he walks backwards and the portrait shows the back of his head. Which is
        /// exactly what the first export did: the base imports into Blender facing -Y and this
        /// pipeline bakes that to -Z, where the shipped bodies face +Z.
        /// </summary>
        [Test]
        public void TheBodyFacesTheWayTheShippedBodiesFace()
        {
            float casey = EyesForward("casey-wilson");
            float dan = EyesForward(Dan);
            Assert.That(casey, Is.GreaterThan(0.2f), "Casey's eyes are in front of the head bone: that is 'forward'.");
            Assert.That(dan, Is.GreaterThan(0.2f), "Dan faces the way Casey faces.");
        }

        /// <summary>The Face submesh's mean vertex, in root space, relative to the Head bone along the root's forward axis.</summary>
        private static float EyesForward(string id)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + id + ".prefab");
            Assert.That(prefab, Is.Not.Null, id);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            try
            {
                var skinned = instance.GetComponentInChildren<SkinnedMeshRenderer>(true);
                var mesh = skinned.sharedMesh;
                int face = -1;
                for (int i = 0; i < skinned.sharedMaterials.Length; i++)
                    if (skinned.sharedMaterials[i] != null && skinned.sharedMaterials[i].name.StartsWith("Face")) face = i;
                Assert.That(face, Is.GreaterThanOrEqualTo(0), id + " has a Face material.");
                var vertices = mesh.vertices;
                var triangles = mesh.GetTriangles(face);
                var sum = Vector3.zero;
                foreach (var t in triangles) sum += vertices[t];
                var eyes = instance.transform.InverseTransformPoint(skinned.transform.TransformPoint(sum / triangles.Length));
                var head = instance.GetComponentsInChildren<Transform>(true).First(t => t.name == "Head");
                var headLocal = instance.transform.InverseTransformPoint(head.position);
                return eyes.z - headLocal.z;
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void TheAllStarsTemplateWearsItsOwnBody()
        {
            var template = CastTemplates.Find(Dan);
            Assert.That(template, Is.Not.Null, "The All-Stars roster carries Dan.");
            var contestant = CastTemplates.ToContestant(template, false);
            Assert.That(CharacterPresentation.AppearanceId(contestant, ContentCatalog.CanonicalId(contestant.id)), Is.EqualTo(Dan));
        }
    }
}
