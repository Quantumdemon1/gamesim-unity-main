using System;
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
