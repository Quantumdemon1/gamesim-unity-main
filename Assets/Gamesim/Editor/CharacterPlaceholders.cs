using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Gamesim.Editor
{
    /// <summary>
    /// Stops the U02 capsule-and-sphere placeholders from rendering, and gives the prototype scene
    /// the authored models it never got.
    ///
    /// <para>The two scenes were showing placeholders for different reasons, and only one of them
    /// was a runtime concern.</para>
    ///
    /// <para><b>EpisodeHouse</b> already replaces them: <c>CharacterPresentation.Build</c> loads an
    /// authored prefab and switches the primitive renderers off. But it only does that in play mode,
    /// so anyone opening the scene in the editor sees six capsules and reasonably concludes the
    /// placeholders are still in the game. Disabling the renderers in the saved scene makes the
    /// editor agree with the build.</para>
    ///
    /// <para><b>HousePrototype</b> has no <c>CharacterPresentation</c> at all — its Player and Maya
    /// are permanent primitives, which is why the green Mint capsule is the one that stands out.
    /// Hiding its renderers alone would leave invisible characters, so the authored prefabs are
    /// instantiated into the scene instead, under the same <c>Gamesim Character Visual</c> child the
    /// runtime uses, so anything that looks for that name finds it in either scene.</para>
    ///
    /// <para><b>Colliders are kept in both.</b> The NavMesh is baked from collision and the
    /// interaction prompt raycasts against these capsules; deleting the placeholder outright would
    /// take navigation and conversation with it. Only the renderers go.</para>
    /// </summary>
    public static class CharacterPlaceholders
    {
        private const string VisualName = "Gamesim Character Visual";
        private const string ModelRoot = "GamesimCharacters/";

        /// <summary>Which authored model each prototype character should wear.</summary>
        private static readonly Dictionary<string, string> PrototypeCast = new Dictionary<string, string>
        {
            { "Player", "player" },
            { "Maya", "maya-hassan" },
        };

        [MenuItem("Gamesim/U07/Hide the placeholder bodies")]
        public static void ApplyToBothScenes()
        {
            Apply("Assets/Gamesim/Scenes/EpisodeHouse.unity", false);
            Apply("Assets/Gamesim/Scenes/HousePrototype.unity", true);
        }

        public static void ApplyFromCommandLine()
        {
            string scenePath = Argument("-gamesimScene") ?? "Assets/Gamesim/Scenes/EpisodeHouse.unity";
            bool authored = string.Equals(Argument("-gamesimAuthored"), "true", StringComparison.OrdinalIgnoreCase);
            Apply(scenePath, authored);
        }

        public static void Apply(string scenePath, bool addAuthoredModels)
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            int hidden = 0, dressed = 0, missing = 0;
            foreach (var root in scene.GetRootGameObjects())
            foreach (var node in root.GetComponentsInChildren<Transform>(true).ToArray())
            {
                var body = node.Find("Body");
                var head = node.Find("Head");
                if (body == null && head == null) continue;

                // Authored models have their own "Body" — a skinned mesh on the armature — so a pass
                // that hid every Body it found would blank the very characters it just added. The
                // first run survived only because the transform list is snapshotted before dressing,
                // which is luck rather than a rule; this makes it a rule, and makes re-running safe.
                if (InsideAuthoredModel(node)) continue;

                if (addAuthoredModels && Dress(node, ref missing)) dressed++;

                foreach (var placeholder in new[] { body, head })
                {
                    var renderer = placeholder != null ? placeholder.GetComponent<Renderer>() : null;
                    if (renderer == null || !renderer.enabled) continue;
                    renderer.enabled = false;
                    hidden++;
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log(string.Format(
                "[Gamesim] placeholders · {0}: {1} renderers hidden, {2} characters dressed, {3} models missing",
                System.IO.Path.GetFileNameWithoutExtension(scenePath), hidden, dressed, missing));
        }

        /// <summary>True when this node is part of an authored model rather than the placeholder rig.</summary>
        private static bool InsideAuthoredModel(Transform node)
        {
            for (var parent = node; parent != null; parent = parent.parent)
                if (parent.name == VisualName) return true;
            return false;
        }

        /// <summary>
        /// Puts the authored model on a character that has none, matching the runtime's hierarchy.
        /// Returns false when the character already has one, so re-running does not stack models.
        /// </summary>
        private static bool Dress(Transform node, ref int missing)
        {
            if (node.Find(VisualName) != null) return false;
            if (!PrototypeCast.TryGetValue(node.name, out var appearance)) return false;

            var prefab = Resources.Load<GameObject>(ModelRoot + appearance);
            if (prefab == null)
            {
                Debug.LogWarning("[Gamesim] placeholders · no authored model for " + node.name + " (" + appearance + ")");
                missing++;
                return false;
            }

            var visual = new GameObject(VisualName).transform;
            visual.SetParent(node, false);
            visual.localPosition = Vector3.zero;
            visual.localRotation = Quaternion.identity;
            visual.localScale = Vector3.one;

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, visual);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;

            // Authored models are decoration here: the capsule collider on the parent is what
            // navigation and the interaction raycast already use, and a second collider would
            // change both.
            foreach (var collider in instance.GetComponentsInChildren<Collider>(true))
                UnityEngine.Object.DestroyImmediate(collider);

            Debug.Log("[Gamesim] placeholders · dressed " + node.name + " as " + appearance);
            return true;
        }

        private static string Argument(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
            return null;
        }
    }
}
