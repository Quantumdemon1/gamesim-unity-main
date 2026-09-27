using Gamesim.House;
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Gamesim.Editor
{
    /// <summary>Explicit authoring only. Never runs on import or saves an open scene automatically.</summary>
    public static class HouseInteractionAnchorAuthoring
    {
        /// <summary>Batch entry restricted to the isolated acceptance project, never the user's editor.</summary>
        public static void AuthorAcceptanceScenes()
        {
            string project=Path.GetFullPath(Path.Combine(Application.dataPath,"..")).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);
            string approved=Path.GetFullPath("D:/GamesimAcceptance").TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);
            if(!Application.isBatchMode || !string.Equals(project,approved,StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Scene authoring is restricted to batch mode in D:/GamesimAcceptance.");
            var report=new System.Text.StringBuilder();
            foreach(var path in new[]{"Assets/Gamesim/Scenes/EpisodeHouse.unity","Assets/Gamesim/Scenes/HousePrototype.unity"})
            {
                var scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Single);
                var issues=Author(scene);
                if(!EditorSceneManager.SaveScene(scene,path))throw new IOException("Could not save acceptance scene: "+path);
                report.AppendLine(path+": "+HouseInteractionAnchors.InScene(scene).Length+" anchors, "+issues.Length+" diagnostics.");
                foreach(var issue in issues)report.AppendLine("  "+issue);
            }
            Directory.CreateDirectory(Path.Combine(project,"Logs"));
            File.WriteAllText(Path.Combine(project,"Logs","interaction-anchor-authoring.txt"),report.ToString());
            Debug.Log(report.ToString());
        }

        public static string[] Author(Scene scene)
        {
            HouseInteractionAnchors.EnsureDefaults(scene);
            HouseFurniture.AuthorLoungerApproaches(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            var kitchenIssue=HouseFurniture.AuthorKitchenAnchor(scene);
            var issues=HouseInteractionAnchors.Validate(scene);
            if(kitchenIssue!=null)issues.Add(kitchenIssue);
            return issues.ToArray();
        }

        [MenuItem("Gamesim/House/Author interaction anchors in active scene")]
        private static void AuthorActiveScene()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            var issues=Author(SceneManager.GetActiveScene());
            if(issues.Length==0)Debug.Log("House interaction anchors authored. Review their seat, approach and camera handles before saving.");
            else Debug.LogWarning(string.Join("\n",issues));
        }
    }
}
