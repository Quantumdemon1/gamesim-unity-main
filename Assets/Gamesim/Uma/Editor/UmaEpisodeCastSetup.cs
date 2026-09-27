using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Gamesim.Uma.Editor
{
    /// <summary>
    /// Puts the UMA cast in the episode scene, where every houseguest's body comes from.
    ///
    /// The provider seam is opt-in: houseguests use UMA only if the scene carries
    /// <see cref="GamesimUmaCast"/>, which the committed episode does. This restores it to a scene
    /// that lost it. There is no switch back to another cast: the cast is UMA's alone (2026-09-27),
    /// and a scene without the component builds the primitive rig, which is only ever meant for a
    /// clone without the UMA package.
    ///
    /// The entry point is public so a headless run can drive it with <c>-executeMethod</c>.
    /// </summary>
    public static class UmaEpisodeCastSetup
    {
        private const string ScenePath = "Assets/Gamesim/Scenes/EpisodeHouse.unity";
        private const string CastObjectName = "Gamesim UMA Cast";

        [MenuItem("Gamesim/UMA/Use UMA bodies in the episode", priority = 110)]
        public static void UseUmaBodies()
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

            if (scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<GamesimUmaCast>(true)).Any())
            {
                Debug.Log("[Gamesim.Uma] Episode already builds houseguests from UMA; nothing changed.");
                return;
            }

            var host = new GameObject(CastObjectName, typeof(GamesimUmaCast));
            EditorSceneManager.MoveGameObjectToScene(host, scene);
            Debug.Log("[Gamesim.Uma] Episode now builds houseguests from UMA.");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }
    }
}
