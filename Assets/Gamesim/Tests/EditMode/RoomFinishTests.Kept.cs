using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Gamesim.Editor;
using Gamesim.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The pass keeps what an earlier run made (HouseRoomFinish.Kept): a run on the saved scene
    /// destroys and makes nothing, so every object keeps its file ID, and the screens' idle boards
    /// keep theirs and their static flags, which is what keeps them in the committed bake. Before
    /// this the pass destroyed and rebuilt its root, renumbering every file ID under it on every
    /// run (a 19,470-line scene diff for sixteen swapped props), and re-made both boards with no
    /// static flags, orphaning their entries in LightingData.asset (review of 2026-10-25).
    /// </summary>
    public sealed partial class RoomFinishTests
    {
        /// <summary>The idle boards' static flags in the committed scene: lightmapped with the set, as the lighting pass marks the set pieces.</summary>
        private const StaticEditorFlags LightmapStatic =
            StaticEditorFlags.ContributeGI | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ReflectionProbeStatic;

        [Test]
        public void ARunOnTheSavedSceneKeepsEveryObjectInPlaceAndTheIdleBoardsWithTheirFlags()
        {
            var materials = OwnedMaterialState.Capture();
            var scene = default(UnityEngine.SceneManagement.Scene);
            try
            {
                scene = EditorSceneManager.OpenPreviewScene(EpisodeScene);
                var root = Root(scene);
                Assert.That(root, Is.Not.Null, "Run Gamesim/U07/Finish the rooms (pack 6).");
                var before = root.GetComponentsInChildren<Transform>(true)
                    .Select(node => (Node: node, Parent: node.parent, Index: node.GetSiblingIndex(), Name: node.name)).ToArray();
                Assert.That(before.Length, Is.GreaterThan(100), "Precondition: the saved root holds the pass's pieces.");
                var boards = IdleBoards(scene).Select(board => (Board: board, Flags: GameObjectUtility.GetStaticEditorFlags(board))).ToArray();
                Assert.That(boards, Has.Length.EqualTo(2), "Precondition: the living room's screen and the nomination room's each carry a board.");

                HouseRoomFinish.Apply(scene);

                Assert.That(Root(scene), Is.SameAs(root), "The root is kept, not rebuilt.");
                var lost = before.Where(entry => entry.Node == null).Select(entry => entry.Name).ToList();
                Assert.That(lost, Is.Empty, "A run on the scene the pass saved destroys none of its objects (each would come back under a new file ID).");
                foreach (var entry in before)
                {
                    Assert.That(entry.Node.parent, Is.SameAs(entry.Parent), entry.Name + " stays under its parent.");
                    Assert.That(entry.Node.GetSiblingIndex(), Is.EqualTo(entry.Index), entry.Name + " keeps its place among its siblings.");
                }
                var after = root.GetComponentsInChildren<Transform>(true);
                Assert.That(after.Length, Is.EqualTo(before.Length), "and makes none.");
                for (int i = 0; i < after.Length; i++)
                    Assert.That(after[i], Is.SameAs(before[i].Node), "The same objects in the same order, at " + before[i].Name + ".");

                var kept = IdleBoards(scene);
                Assert.That(kept, Has.Length.EqualTo(2));
                foreach (var (board, flags) in boards)
                {
                    Assert.That(board != null, Is.True, "The idle board is kept: its renderer's file ID is the one the committed bake lights.");
                    Assert.That(kept.Contains(board), Is.True);
                    Assert.That(GameObjectUtility.GetStaticEditorFlags(board), Is.EqualTo(flags), board.name + " keeps its static flags.");
                }
            }
            finally
            {
                try { materials.RestoreAndAssertUnchanged(); }
                finally { if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene); }
            }
        }

        /// <summary>
        /// A kept piece goes back to how a new one is made (HouseRoomFinish.IsShape and AsMadeObject).
        /// A primitive someone added a component to is not one the pass made, so a run replaces it with
        /// a new primitive of exactly a Transform, a MeshFilter and a MeshRenderer. A primitive someone
        /// switched off, moved to another layer and tagged is still the pass's own, so a run keeps the
        /// very object and puts it back active, on the default layer and untagged, as CreatePrimitive
        /// makes one; a kept prefab instance edited the same way goes back to its prefab's layer and
        /// tag. On a scene the pass saved every piece is already as made, so without these edits a run
        /// would pass whether or not either rule held. A first run brings whatever the committed scene
        /// holds to what the pass makes, so the case does not depend on which pass saved the scene.
        /// </summary>
        [Test]
        public void ARunPutsAHandEditedPieceBackAsANewOneIsMade()
        {
            var materials = OwnedMaterialState.Capture();
            var scene = default(UnityEngine.SceneManagement.Scene);
            try
            {
                scene = EditorSceneManager.OpenPreviewScene(EpisodeScene);
                Assert.That(Root(scene), Is.Not.Null, "Run Gamesim/U07/Finish the rooms (pack 6).");
                HouseRoomFinish.Apply(scene);
                var root = Root(scene);
                // Pieces each the only child of its name under a chain of the pass's plain groups, so one
                // call of the run under test claims it and the run's sweep reaches whatever it leaves.
                bool OnlyOfItsName(Transform node) => node.parent.Cast<Transform>().Count(sibling => sibling.name == node.name) == 1;
                bool UnderGroups(Transform node)
                {
                    for (var up = node.parent; up != root; up = up.parent)
                        if (up == null || up.GetComponents<Component>().Length != 1 || PrefabUtility.IsPartOfAnyPrefab(up.gameObject)) return false;
                    return true;
                }
                // An explicit null test: in the editor a missing component reads as a fake null.
                string MeshOf(Transform node)
                {
                    var filter = node.GetComponent<MeshFilter>();
                    return filter != null && filter.sharedMesh != null ? filter.sharedMesh.name : null;
                }
                var nodes = root.GetComponentsInChildren<Transform>(true).Where(node => node != root && OnlyOfItsName(node) && UnderGroups(node)).ToArray();
                var primitives = nodes.Where(node => node.GetComponents<Component>().Length == 3 && node.childCount == 0
                    && node.GetComponent<MeshRenderer>() != null && new[] { "Quad", "Cube", "Cylinder" }.Contains(MeshOf(node))
                    && !PrefabUtility.IsPartOfAnyPrefab(node.gameObject)).ToArray();
                Assert.That(primitives.Length, Is.GreaterThanOrEqualTo(2), "Precondition: the pass's primitives under the root.");
                var instance = nodes.FirstOrDefault(node => PrefabUtility.IsOutermostPrefabInstanceRoot(node.gameObject));
                Assert.That(instance != null, Is.True, "Precondition: a prefab instance the pass placed under the root.");
                var source = PrefabUtility.GetCorrespondingObjectFromSource(instance.gameObject);
                Assert.That(source != null, Is.True, "Precondition: " + instance.name + " has its prefab.");
                var extended = primitives[0];
                var edited = primitives[1];
                foreach (var (piece, madeLayer, madeTag) in new[] { (extended, 0, "Untagged"), (edited, 0, "Untagged"), (instance, source.layer, source.tag) })
                    Assert.That(piece.gameObject.activeSelf && piece.gameObject.layer == madeLayer && piece.CompareTag(madeTag), Is.True,
                        "Precondition: " + piece.name + " is as the first run made it.");

                var extendedParent = extended.parent;
                string extendedName = extended.name, extendedMesh = MeshOf(extended);
                extended.gameObject.AddComponent<AudioSource>();
                Assert.That(extended.GetComponents<Component>(), Has.Length.EqualTo(4), "Precondition: " + extendedName + " carries the added AudioSource.");
                int layer = LayerMask.NameToLayer("Ignore Raycast");
                Assert.That(layer, Is.GreaterThan(0), "Precondition: Unity's built-in 'Ignore Raycast' layer.");
                Assert.That(UnityEditorInternal.InternalEditorUtility.tags, Does.Contain("EditorOnly"), "Precondition: Unity's built-in 'EditorOnly' tag.");
                foreach (var piece in new[] { edited, instance })
                {
                    piece.gameObject.layer = layer;
                    piece.gameObject.tag = "EditorOnly";
                    piece.gameObject.SetActive(false);
                }
                Assert.That(source.layer != layer && !source.CompareTag("EditorOnly"), Is.True,
                    "Precondition: the edits differ from what " + instance.name + "'s prefab is made with.");
                var before = new HashSet<Transform>(root.GetComponentsInChildren<Transform>(true));

                HouseRoomFinish.Apply(scene);

                Assert.That(extended == null, Is.True, extendedName + " carries a component the pass never adds: the run does not keep it.");
                var made = extendedParent.Cast<Transform>().Where(child => child.name == extendedName).ToArray();
                Assert.That(made, Has.Length.EqualTo(1), "The run makes one " + extendedName + " in its place.");
                Assert.That(before.Contains(made[0]), Is.False, extendedName + " is a new object, not one the scene held before the run.");
                Assert.That(made[0].GetComponents<Component>().Select(component => component.GetType()).ToArray(),
                    Is.EquivalentTo(new[] { typeof(Transform), typeof(MeshFilter), typeof(MeshRenderer) }),
                    extendedName + " is a primitive as the pass makes one: a Transform, a MeshFilter and a MeshRenderer.");
                Assert.That(made[0].GetComponent<MeshFilter>().sharedMesh.name, Is.EqualTo(extendedMesh), extendedName + " keeps its shape.");

                foreach (var (piece, madeLayer, madeTag) in new[] { (edited, 0, "Untagged"), (instance, source.layer, source.tag) })
                {
                    Assert.That(piece != null, Is.True, "The run keeps the pass's own " + (piece == instance ? "prefab instance" : "primitive") + ".");
                    Assert.That(piece.parent.Cast<Transform>().Single(child => child.name == piece.name), Is.SameAs(piece),
                        piece.name + " is the very object, the only one of its name under its group.");
                    Assert.That(piece.gameObject.activeSelf, Is.True, piece.name + " is active again, as a new one is.");
                    Assert.That(piece.gameObject.layer, Is.EqualTo(madeLayer), piece.name + " is back on the layer a new one is made on.");
                    Assert.That(piece.CompareTag(madeTag), Is.True, piece.name + " is tagged '" + madeTag + "' again, as a new one is, not '" + piece.tag + "'.");
                }
            }
            finally
            {
                try { materials.RestoreAndAssertUnchanged(); }
                finally { if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene); }
            }
        }

        /// <summary>
        /// Read from the files, not a loaded scene: both idle boards in the saved scene are lightmap
        /// static and are the renderers the committed bake lights - each board renderer's file ID is
        /// in the scene's LightingData.asset, as an 8-byte little-endian integer. A board the pass
        /// re-made carries a new file ID the bake does not know, and no static flags.
        /// </summary>
        [Test]
        public void TheSavedIdleBoardsAreTheRenderersTheCommittedBakeLightsAndStayLightmapStatic()
        {
            string text = File.ReadAllText(EpisodeScene);
            var lighting = Regex.Match(text, @"m_LightingDataAsset: \{fileID: -?\d+, guid: ([0-9a-f]{32})");
            Assert.That(lighting.Success, Is.True, "The scene names its lighting data.");
            string lightingPath = AssetDatabase.GUIDToAssetPath(lighting.Groups[1].Value);
            Assert.That(File.Exists(lightingPath), Is.True, "The committed bake: " + lightingPath);
            byte[] bake = File.ReadAllBytes(lightingPath);

            var documents = Regex.Split(text, @"^--- !u!", RegexOptions.Multiline)
                .Select(doc => (Header: Regex.Match(doc, @"^(\d+) &(-?\d+)"), Body: doc))
                .Where(doc => doc.Header.Success)
                .ToDictionary(doc => doc.Header.Groups[2].Value, doc => (Class: doc.Header.Groups[1].Value, doc.Body));
            var boards = documents.Where(doc => doc.Value.Class == "1"
                && Regex.IsMatch(doc.Value.Body, "^  m_Name: " + Regex.Escape(ScreenSurface.IdleDisplayName) + "$", RegexOptions.Multiline)).ToList();
            Assert.That(boards, Has.Count.EqualTo(2), "The living room's board and the nomination room's.");
            foreach (var board in boards)
            {
                var flags = Regex.Match(board.Value.Body, @"^  m_StaticEditorFlags: (\d+)$", RegexOptions.Multiline);
                Assert.That(flags.Success, Is.True);
                Assert.That((StaticEditorFlags)int.Parse(flags.Groups[1].Value), Is.EqualTo(LightmapStatic),
                    "Board &" + board.Key + " is lightmap-static with the set, not probe-lit only.");
                var renderers = Regex.Matches(board.Value.Body, @"component: \{fileID: (-?\d+)\}").Cast<Match>()
                    .Select(m => m.Groups[1].Value).Where(id => documents.TryGetValue(id, out var doc) && doc.Class == "23").ToList();
                Assert.That(renderers, Has.Count.EqualTo(1), "Board &" + board.Key + " has its mesh renderer.");
                byte[] id = BitConverter.GetBytes(long.Parse(renderers[0]));
                if (!BitConverter.IsLittleEndian) Array.Reverse(id);
                Assert.That(HasBytes(bake, id), Is.True,
                    "Board &" + board.Key + "'s renderer &" + renderers[0] + " is in " + lightingPath + ": the committed bake lights this very board.");
            }
        }

        private static GameObject[] IdleBoards(UnityEngine.SceneManagement.Scene scene) =>
            scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Renderer>(true))
                .Where(renderer => renderer.name == ScreenSurface.IdleDisplayName).Select(renderer => renderer.gameObject).ToArray();

        private static bool HasBytes(byte[] haystack, byte[] needle)
        {
            for (int i = 0; i + needle.Length <= haystack.Length; i++)
            {
                int j = 0;
                while (j < needle.Length && haystack[i + j] == needle[j]) j++;
                if (j == needle.Length) return true;
            }
            return false;
        }
    }
}
