using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Gamesim.Editor
{
    /// <summary>
    /// Stops the U02 capsule-and-sphere placeholders from rendering in the saved episode scene.
    ///
    /// <para><b>EpisodeHouse</b> replaces them at runtime: <c>CharacterPresentation.Build</c> asks
    /// UMA for each houseguest's body and switches the primitive renderers off. But it only does that
    /// in play mode, so anyone opening the scene in the editor saw six capsules and reasonably
    /// concluded the placeholders were still in the game. Disabling the renderers in the saved scene
    /// makes the editor agree with the build.</para>
    ///
    /// <para><b>HousePrototype</b> is left alone deliberately: it is the U02 greybox the NPC motion
    /// suite loads, not a scene anyone plays, and greybox capsules are the right thing in a greybox.</para>
    ///
    /// <para><b>Colliders are kept.</b> The NavMesh is baked from collision and the interaction
    /// prompt raycasts against these capsules; deleting the placeholder outright would take
    /// navigation and conversation with it. Only the renderers go.</para>
    /// </summary>
    public static class CharacterPlaceholders
    {
        private const string VisualName = "Gamesim Character Visual";

        [MenuItem("Gamesim/U07/Hide the placeholder bodies")]
        public static void ApplyToEpisode() => Apply("Assets/Gamesim/Scenes/EpisodeHouse.unity");

        public static void ApplyFromCommandLine() => Apply(Argument("-gamesimScene") ?? "Assets/Gamesim/Scenes/EpisodeHouse.unity");

        public static void Apply(string scenePath)
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            int hidden = 0;
            foreach (var root in scene.GetRootGameObjects())
            foreach (var node in root.GetComponentsInChildren<Transform>(true).ToArray())
            {
                var body = node.Find("Body");
                var head = node.Find("Head");
                if (body == null && head == null) continue;

                // A body built under the character has its own "Body" and "Head"; only the
                // placeholder rig's are this pass's to hide.
                if (InsideBuiltBody(node)) continue;

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
            Debug.Log(string.Format("[Gamesim] placeholders · {0}: {1} renderers hidden",
                System.IO.Path.GetFileNameWithoutExtension(scenePath), hidden));
        }

        /// <summary>True when this node is part of a built body rather than the placeholder rig.</summary>
        private static bool InsideBuiltBody(Transform node)
        {
            for (var parent = node; parent != null; parent = parent.parent)
                if (parent.name == VisualName) return true;
            return false;
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
