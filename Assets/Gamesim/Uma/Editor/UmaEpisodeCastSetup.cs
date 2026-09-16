using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Gamesim.Uma.Editor
{
    /// <summary>
    /// Switches the episode between UMA bodies and the authored prefabs, in one place, both ways.
    ///
    /// The provider seam is deliberately opt-in: houseguests use UMA only if the scene carries
    /// <see cref="GamesimUmaCast"/>. That makes the switch a scene edit, which is exactly the kind of
    /// change that is easy to make by hand, forget, and then not be able to undo cleanly. This does
    /// it reversibly and leaves no trace when turned off.
    ///
    /// Both entry points are public so a headless run can drive them with <c>-executeMethod</c>,
    /// which is how the UMA-bodied season was verified.
    /// </summary>
    public static class UmaEpisodeCastSetup
    {
        private const string ScenePath = "Assets/Gamesim/Scenes/EpisodeHouse.unity";
        private const string CastObjectName = "Gamesim UMA Cast";

        [MenuItem("Gamesim/UMA/Use UMA bodies in the episode", priority = 110)]
        public static void UseUmaBodies() => Apply(true);

        [MenuItem("Gamesim/UMA/Use the authored prefabs in the episode", priority = 111)]
        public static void UseAuthoredPrefabs() => Apply(false);

        private static void Apply(bool useUma)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[Gamesim.Uma] Leave play mode before changing the episode cast.");
                return;
            }

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                Debug.LogError("[Gamesim.Uma] Could not open " + ScenePath);
                return;
            }

            var existing = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<GamesimUmaCast>(true))
                .FirstOrDefault();

            if (useUma && existing == null)
            {
                var host = new GameObject(CastObjectName, typeof(GamesimUmaCast));
                UnityEditor.SceneManagement.EditorSceneManager.MoveGameObjectToScene(host, scene);
                Debug.Log("[Gamesim.Uma] Episode now builds houseguests from UMA.");
            }
            else if (!useUma && existing != null)
            {
                Object.DestroyImmediate(existing.gameObject);
                Debug.Log("[Gamesim.Uma] Episode reverted to the authored prefabs.");
            }
            else
            {
                Debug.Log("[Gamesim.Uma] Episode already uses "
                    + (useUma ? "UMA bodies" : "the authored prefabs") + "; nothing changed.");
                return;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }
    }
}
