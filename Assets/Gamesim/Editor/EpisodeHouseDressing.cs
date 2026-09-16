using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Gamesim.Editor
{
    /// <summary>
    /// Brings the prototype set's dressing onto the episode scene: the emissive neon trim, the
    /// planting, and the Kenney furniture.
    ///
    /// <para>The two houses are the same house. They have the same footprint to the centimetre —
    /// 28.30 x 30.22 — the same five rooms and the same room names, because the episode scene was
    /// built from the prototype's plan. What did not come across was the art. The shipping scene
    /// references <b>zero</b> emissive materials; the prototype references 144 Neon Gold, 9 Neon
    /// Blue, 8 Neon Yellow and 4 Neon Red, and carries the Kenney furniture besides. So the set
    /// everyone has been judging is not the set that was built — the good one has been sitting in a
    /// scene nobody plays.</para>
    ///
    /// <para>This transplants rather than regenerates. The neon was placed by hand and there is no
    /// script that produced it, so recreating it procedurally would mean inventing a second, worse
    /// version of art that already exists. Copies are taken by world transform, which is sound here
    /// precisely because the footprints agree; the pass logs both roots so a future divergence
    /// shows up as a number rather than as a set that is quietly one metre out.</para>
    ///
    /// <para>Colliders are stripped from every copy. The trim is decoration lying on the floor and
    /// across doorways, and the NavMesh is baked from collision — dressing that could block a
    /// walkable surface would turn an art pass into a navigation bug.</para>
    /// </summary>
    public static class EpisodeHouseDressing
    {
        public const string DressingRoot = "Broadcast Dressing";
        private const string EpisodeScene = "Assets/Gamesim/Scenes/EpisodeHouse.unity";
        private const string PrototypeScene = "Assets/Gamesim/Scenes/HousePrototype.unity";
        private const string WorldRoot = "House Architecture";

        private static readonly string[] EmissiveMaterials =
        {
            "Neon Blue", "Neon Gold", "Neon Red", "Neon Yellow", "Lamp Glow", "TV Screen",
        };

        // Planting reads as set dressing rather than as furniture, and the furnishing plan does not
        // cover it because there is no primitive standing in for a pot.
        private static readonly string[] DressingMaterials = { "Plant Leaf", "Pot" };

        [MenuItem("Gamesim/U07/Dress the episode house like the prototype")]
        public static void DressEpisodeHouse()
        {
            var episode = EditorSceneManager.OpenScene(EpisodeScene, OpenSceneMode.Single);
            var prototype = EditorSceneManager.OpenScene(PrototypeScene, OpenSceneMode.Additive);

            try
            {
                var episodeWorld = FindWorld(episode);
                var prototypeWorld = FindWorld(prototype);

                Debug.Log(string.Format(
                    "[Gamesim] dressing · episode root at {0} · prototype root at {1}",
                    episodeWorld.position.ToString("F3"), prototypeWorld.position.ToString("F3")));

                int copied = Transplant(prototypeWorld, episodeWorld);
                Reshell(episodeWorld);
                int furnished = HouseFurnishing.Apply(episodeWorld);

                Debug.Log(string.Format(
                    "[Gamesim] dressing · {0} dressing objects copied, {1} furniture models placed",
                    copied, furnished));

                EditorSceneManager.MarkSceneDirty(episode);
            }
            finally
            {
                EditorSceneManager.CloseScene(prototype, true);
            }

            EditorSceneManager.SaveScene(EditorSceneManager.GetSceneManagerSetup()
                .Select(s => SceneManager.GetSceneByPath(s.path)).First(s => s.path == EpisodeScene));
            AssetDatabase.SaveAssets();
            Debug.Log("[Gamesim] dressing · episode scene saved.");
        }

        /// <summary>
        /// Points the living room floor at its own dark material.
        ///
        /// <para>It shared Warm Oak with the bed frames and tables, so it was the one floor the
        /// palette pass could not darken without taking the furniture down with it.</para>
        /// </summary>
        private static void Reshell(Transform world)
        {
            var floor = world.Find("Living room floor");
            if (floor == null) { Debug.LogWarning("[Gamesim] dressing · no living room floor to reshell."); return; }

            var material = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/Gamesim/Art/Prototype/Living Room Floor.mat");
            if (material == null)
            {
                Debug.LogWarning("[Gamesim] dressing · Living Room Floor material missing; "
                    + "run the palette pass first. Floor left on Warm Oak.");
                return;
            }

            var renderer = floor.GetComponent<Renderer>();
            if (renderer == null) return;
            renderer.sharedMaterial = material;
            Debug.Log("[Gamesim] dressing · living room floor moved off Warm Oak.");
        }

        private static Transform FindWorld(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
                if (root.name == WorldRoot) return root.transform;
            throw new InvalidOperationException(
                "Expected a '" + WorldRoot + "' root in " + scene.path + ".");
        }

        /// <summary>
        /// Copies every object whose renderer uses one of the dressing materials, preserving world
        /// position. Returns how many were copied.
        /// </summary>
        private static int Transplant(Transform source, Transform destination)
        {
            var wanted = new HashSet<string>(EmissiveMaterials.Concat(DressingMaterials), StringComparer.Ordinal);

            var existing = destination.Find(DressingRoot);
            if (existing != null) UnityEngine.Object.DestroyImmediate(existing.gameObject);

            var root = new GameObject(DressingRoot).transform;
            root.SetParent(destination, false);
            root.localPosition = Vector3.zero;
            root.localRotation = Quaternion.identity;
            root.localScale = Vector3.one;

            // Deepest-first would copy a strip and then its parent, duplicating it. Take the highest
            // node whose whole subtree is dressing, and copy that once.
            var taken = new List<Transform>();
            foreach (var candidate in source.GetComponentsInChildren<Transform>(true))
            {
                if (candidate == source) continue;
                if (!IsDressing(candidate, wanted)) continue;
                if (taken.Any(already => candidate.IsChildOf(already))) continue;
                taken.Add(candidate);
            }

            foreach (var node in taken)
            {
                var copy = UnityEngine.Object.Instantiate(node.gameObject, root);
                copy.name = node.name;
                copy.transform.position = node.position;
                copy.transform.rotation = node.rotation;
                copy.transform.localScale = node.lossyScale;

                foreach (var collider in copy.GetComponentsInChildren<Collider>(true))
                    UnityEngine.Object.DestroyImmediate(collider);
            }
            return taken.Count;
        }

        /// <summary>
        /// True when this node and everything under it renders only with dressing materials, so
        /// copying it cannot drag a wall or a floor across with it.
        /// </summary>
        private static bool IsDressing(Transform node, HashSet<string> wanted)
        {
            var renderers = node.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return false;

            foreach (var renderer in renderers)
            foreach (var material in renderer.sharedMaterials)
            {
                if (material == null) return false;
                if (!wanted.Contains(material.name)) return false;
            }
            return true;
        }
    }
}
