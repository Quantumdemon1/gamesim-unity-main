using System;
using System.IO;
using System.Linq;
using Gamesim.Bootstrap;
using Gamesim.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Gamesim.Editor
{
    public static class U01ProjectSetup
    {
        private const string BuildDirectory = "Builds/Windows";
        private const string BuildPath = BuildDirectory + "/Gamesim.exe";

        [MenuItem("Gamesim/U01/Apply Foundation Setup")]
        public static void ApplyFoundationSetup()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                throw new InvalidOperationException("U01 setup was cancelled because the active scene has unsaved changes.");
            }

            var sceneSetup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                EnsureContentFolders();
                EditorSettings.serializationMode = SerializationMode.ForceText;
                VersionControlSettings.mode = "Visible Meta Files";
                if (!File.Exists(FoundationInfo.BootstrapScenePath))
                {
                    CreateBootstrapScene();
                }
                ConfigureBuildSettings();
            }
            finally
            {
                if (sceneSetup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(sceneSetup);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("U01 foundation setup completed.");
        }

        [MenuItem("Gamesim/U01/Build Windows Desktop")]
        public static void BuildWindowsDesktop()
        {
            BuildDesktop(BuildPath, "Logs/U01-build-report.json");
        }

        [MenuItem("Gamesim/U02/Build Windows Desktop")]
        public static void BuildHouseDesktop()
        {
            if (!EditorBuildSettings.scenes.Any(s => s.enabled && s.path == HousePrototypeSetup.ScenePath))
                throw new BuildPlayerWindow.BuildMethodException("The U02 house scene must be enabled before building.");
            BuildDesktop("Builds/U02-Windows/Gamesim.exe", "Logs/U02-build-report.json");
        }

        private static void BuildDesktop(string outputPath, string reportPath)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
            var enabledScenes = Array.FindAll(EditorBuildSettings.scenes, scene => scene.enabled);
            foreach (var scene in enabledScenes)
                if (!File.Exists(scene.path))
                    throw new BuildPlayerWindow.BuildMethodException("Enabled scene is missing: " + scene.path);

            if (enabledScenes.Length == 0)
            {
                throw new BuildPlayerWindow.BuildMethodException("No enabled scenes are available to build.");
            }

            var scenePaths = Array.ConvertAll(enabledScenes, scene => scene.path);
            var options = new BuildPlayerOptions
            {
                scenes = scenePaths,
                locationPathName = outputPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.CleanBuildCache | BuildOptions.StrictMode
            };

            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            Directory.CreateDirectory("Logs");
            File.WriteAllText(reportPath, JsonUtility.ToJson(new DesktopBuildSummary
            {
                result = summary.result.ToString(),
                outputPath = summary.outputPath,
                totalBytes = summary.totalSize,
                errors = summary.totalErrors,
                warnings = summary.totalWarnings,
                durationSeconds = summary.totalTime.TotalSeconds,
                scenes = scenePaths,
                messages = report.steps.SelectMany(s => s.messages)
                    .Where(m => m.type == LogType.Error || m.type == LogType.Warning || m.type == LogType.Exception)
                    .Select(m => m.content).Distinct().ToArray()
            }, true));
            if (summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            {
                throw new BuildPlayerWindow.BuildMethodException(
                    $"Windows build failed: {summary.result} ({summary.totalErrors} errors, {summary.totalWarnings} warnings).");
            }

            Debug.Log($"Windows build succeeded: {outputPath} ({summary.totalSize} bytes). Report: {reportPath}");
        }

        [MenuItem("Gamesim/Port/Build Windows Desktop")]
        public static void BuildPortDesktop()
            => BuildConfiguredPort("Builds/Port-Windows-V7/Gamesim.exe", "Logs/Port-v7-build-report.json");

        /// <summary>Review candidate output is separate from the historically pinned V7 build.</summary>
        public static void BuildReviewCandidate()
            => BuildConfiguredPort("Builds/Port-Windows-Review13/Gamesim.exe", "Logs/review13-build-report.json");

        private static void BuildConfiguredPort(string outputPath, string reportPath)
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new BuildPlayerWindow.BuildMethodException("Wait for an idle Editor before building the port.");
            if (!EditorBuildSettings.scenes.Any(s => s.enabled && s.path == EpisodeProjectSetup.ScenePath))
                throw new BuildPlayerWindow.BuildMethodException("The episode scene must be enabled before building the port.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new BuildPlayerWindow.BuildMethodException("Save modified scenes before building the port.");
            PlayerSettings.defaultScreenWidth = 1600;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            // Preserve the starter project's original input-action preload after Test Framework runs.
            var input = AssetDatabase.LoadMainAssetAtPath("Assets/InputSystem_Actions.inputactions");
            var preloaded = PlayerSettings.GetPreloadedAssets().ToList();
            if (input != null && !preloaded.Contains(input)) { preloaded.Add(input); PlayerSettings.SetPreloadedAssets(preloaded.ToArray()); }
            AssetDatabase.SaveAssets();
            // Keep accepted V6 intact while developing the next activity increment.
            BuildDesktop(outputPath, reportPath);
        }

        [Serializable]
        private sealed class DesktopBuildSummary
        {
            public string result;
            public string outputPath;
            public ulong totalBytes;
            public int errors;
            public int warnings;
            public double durationSeconds;
            public string[] scenes;
            public string[] messages;
        }

        private static void EnsureContentFolders()
        {
            var folders = new[]
            {
                "Assets/Gamesim/Art",
                "Assets/Gamesim/Audio",
                "Assets/Gamesim/Data",
                "Assets/Gamesim/Prefabs",
                "Assets/Gamesim/Scenes"
            };

            foreach (var folder in folders)
            {
                Directory.CreateDirectory(folder);
            }
        }

        private static void CreateBootstrapScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var bootstrapRoot = new GameObject("Gamesim Bootstrap");
            bootstrapRoot.AddComponent<GamesimBootstrap>();

            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetPositionAndRotation(new Vector3(0f, 5f, -10f), Quaternion.Euler(20f, 0f, 0f));
            cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();

            var lightObject = new GameObject("Directional Light");
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1f;

            EditorSceneManager.SaveScene(scene, FoundationInfo.BootstrapScenePath);
        }

        private static void ConfigureBuildSettings()
        {
            const string sampleScenePath = "Assets/Scenes/SampleScene.unity";
            var scenes = EditorBuildSettings.scenes.Where(scene => scene.path != FoundationInfo.BootstrapScenePath).ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(FoundationInfo.BootstrapScenePath, true));
            if (File.Exists(sampleScenePath) && !scenes.Any(scene => scene.path == sampleScenePath))
                scenes.Add(new EditorBuildSettingsScene(sampleScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
