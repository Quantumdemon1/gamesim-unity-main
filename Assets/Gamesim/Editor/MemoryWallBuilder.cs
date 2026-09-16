using System;
using System.Linq;
using Gamesim.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Gamesim.Editor
{
    /// <summary>
    /// Builds the memory wall into the episode scene: a backing panel and a grid of lit frames
    /// mounted on the living room's west wall.
    ///
    /// <para>Geometry only. Which face goes in which frame, and which frames go dark, is decided at
    /// runtime by <see cref="MemoryWall"/> from committed state — a wall with the cast baked into it
    /// would be wrong the moment somebody was evicted, and wrong in a way that survives a reload.</para>
    ///
    /// <para>Placement is derived from the wall's own renderer bounds rather than from constants.
    /// The set has been rebuilt twice already; a wall mounted at hard-coded coordinates is a wall
    /// that ends up floating in the yard the next time the house moves.</para>
    /// </summary>
    public static class MemoryWallBuilder
    {
        public const string RootName = "Memory Wall";
        private const string EpisodeScene = "Assets/Gamesim/Scenes/EpisodeHouse.unity";
        private const string WorldRoot = "House Architecture";
        private const string MountWall = "West wall";
        private const string ArtRoot = "Assets/Gamesim/Art/Prototype/";

        private const int Columns = 3, Rows = 2;
        // Fractions of the wall rather than absolute sizes. The set is a cutaway with 1.5-unit
        // walls so the camera can see in, and a grid sized in metres hangs straight through the
        // top of it — which is how the first build of this came out.
        private const float UsableHeight = 0.78f;
        private const float GapFraction = 0.10f;
        private const float BorderFraction = 0.09f;

        [MenuItem("Gamesim/U07/Build the memory wall")]
        public static void Build()
        {
            var scene = EditorSceneManager.OpenScene(EpisodeScene, OpenSceneMode.Single);
            var world = scene.GetRootGameObjects().FirstOrDefault(root => root.name == WorldRoot);
            if (world == null) throw new InvalidOperationException("No '" + WorldRoot + "' root in the episode scene.");

            var wall = world.transform.Find(MountWall);
            if (wall == null) throw new InvalidOperationException("No '" + MountWall + "' to mount the memory wall on.");
            var wallRenderer = wall.GetComponent<Renderer>();
            if (wallRenderer == null) throw new InvalidOperationException(MountWall + " has no renderer to measure.");

            var existing = world.transform.Find(RootName);
            if (existing != null) UnityEngine.Object.DestroyImmediate(existing.gameObject);

            var bounds = wallRenderer.bounds;
            // The west wall runs the full depth of the house. The living room is its southern half,
            // so the wall hangs at the middle of that half rather than at the middle of the wall.
            float southHalfCentre = (bounds.min.z + bounds.center.z) * 0.5f;
            // Just proud of the inner face, so it cannot z-fight with the wall it hangs on.
            float faceX = bounds.max.x + 0.06f;

            var gold = Material("Emissive/Neon Gold");
            var ink = Material("Ink");
            if (gold == null || ink == null)
                throw new InvalidOperationException("Neon Gold and Ink materials are required to build the memory wall.");

            var root = new GameObject(RootName);
            root.transform.SetParent(world.transform, false);
            root.transform.position = new Vector3(faceX, 0f, southHalfCentre);

            float usable = bounds.size.y * UsableHeight;
            float gap = usable * GapFraction;
            float frameSize = (usable - (Rows - 1) * gap) / Rows;
            float border = frameSize * BorderFraction;
            float mountHeight = bounds.center.y;

            float pitch = frameSize + gap;
            float spanZ = Columns * frameSize + (Columns - 1) * gap;
            float spanY = Rows * frameSize + (Rows - 1) * gap;

            Box("Backing", root.transform, ink,
                new Vector3(0.04f, spanY + gap * 2f, spanZ + gap * 2f),
                new Vector3(-0.03f, mountHeight, 0f));

            int index = 0;
            for (int row = 0; row < Rows; row++)
            for (int column = 0; column < Columns; column++)
            {
                float z = (column - (Columns - 1) * 0.5f) * pitch;
                float y = mountHeight + ((Rows - 1) * 0.5f - row) * pitch;

                var frame = new GameObject(MemoryWall.FramePrefix + " " + index.ToString("00"));
                frame.transform.SetParent(root.transform, false);
                frame.transform.localPosition = new Vector3(0f, y, z);

                // Border behind, portrait in front: one box each rather than four edge strips,
                // because the portrait covers the middle and the difference is never visible.
                Box(MemoryWall.BorderChild, frame.transform, gold,
                    new Vector3(0.03f, frameSize + border * 2f, frameSize + border * 2f),
                    Vector3.zero);
                Box(MemoryWall.PortraitChild, frame.transform, ink,
                    new Vector3(0.03f, frameSize, frameSize),
                    new Vector3(0.02f, 0f, 0f));
                index++;
            }

            root.AddComponent<MemoryWall>();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log(string.Format(
                "[Gamesim] memory wall · {0} frames of {1:0.00} at x={2:0.00}, z={3:0.00}, spanning y {4:0.00}-{5:0.00} "
                + "on {6} (wall y {7:0.00}-{8:0.00})",
                index, frameSize, faceX, southHalfCentre,
                mountHeight - spanY * 0.5f, mountHeight + spanY * 0.5f,
                MountWall, bounds.min.y, bounds.max.y));
        }

        private static Material Material(string name) =>
            AssetDatabase.LoadAssetAtPath<Material>(ArtRoot + name + ".mat");

        /// <summary>
        /// A collider-free box. The wall is decoration on a surface the player walks past; giving it
        /// collision would carve the NavMesh along the living room's west edge.
        /// </summary>
        private static GameObject Box(string name, Transform parent, Material material, Vector3 size, Vector3 localPosition)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.localPosition = localPosition;
            box.transform.localScale = size;
            box.GetComponent<Renderer>().sharedMaterial = material;
            UnityEngine.Object.DestroyImmediate(box.GetComponent<Collider>());
            return box;
        }
    }
}
