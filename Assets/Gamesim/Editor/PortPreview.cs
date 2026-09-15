using System;
using System.IO;
using System.Linq;
using Gamesim.Episode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Gamesim.Editor
{
    /// <summary>Editor-only preview with a disposable save location and recoverable scene setup.</summary>
    [InitializeOnLoad]
    public static class PortPreview
    {
        private const string Prefix = "Gamesim.PortPreview.";
        private const string RootKey = Prefix + "Root";
        private const string SetupKey = Prefix + "SceneSetup";
        private const string ScenePath = EpisodeProjectSetup.ScenePath;
        public static bool IsIsolatedPreview => !string.IsNullOrEmpty(SessionState.GetString(RootKey, ""));
        public static string PreviewDirectory => SessionState.GetString(RootKey, "");

        [Serializable]
        private sealed class SavedSetup { public SavedScene[] scenes; }

        [Serializable]
        private sealed class SavedScene
        {
            public string path;
            public bool isLoaded, isActive;
        }

        static PortPreview()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            if (!IsIsolatedPreview) return;
            // SessionState survives script/domain reload; the runtime static override does not.
            if (EditorApplication.isPlayingOrWillChangePlaymode) ApplySaveIsolation();
            EditorApplication.delayCall += RecoverAfterReload;
        }

        [InitializeOnEnterPlayMode]
        private static void BeforeEnteringPlayMode(EnterPlayModeOptions options) => ApplySaveIsolation();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void BeforeSceneLoad() => ApplySaveIsolation();

        [MenuItem("Gamesim/Port/Start Isolated Preview")]
        public static void StartIsolatedPreview()
        {
            if (IsIsolatedPreview)
                throw new InvalidOperationException("An isolated preview already exists. Stop it before starting another.");
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("Wait for an idle editor outside Play Mode before starting an isolated preview.");
            if (!string.IsNullOrEmpty(EpisodeDirector.SaveRootOverride))
                throw new InvalidOperationException("Another test or tool already owns the episode save override.");
            EnsureScenesClean();
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
                throw new FileNotFoundException("Create EpisodeHouse before starting its preview.", ScenePath);
            var setup = EditorSceneManager.GetSceneManagerSetup();
            if (setup.Any(scene => string.IsNullOrEmpty(scene.path)))
                throw new InvalidOperationException("Save any untitled scene before previewing so its exact setup can be restored.");
            var stored = new SavedSetup
            {
                scenes = setup.Select(scene => new SavedScene
                { path = scene.path, isLoaded = scene.isLoaded, isActive = scene.isActive }).ToArray()
            };
            var directory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "Port-preview-" + Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(directory);
            SessionState.SetString(SetupKey, JsonUtility.ToJson(stored));
            SessionState.SetString(RootKey, directory);
            try
            {
                ApplySaveIsolation();
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                EditorApplication.isPlaying = true;
                Debug.Log("Starting isolated Gamesim preview. Saves and captures stay in " + directory);
            }
            catch
            {
                RestoreSceneSetup();
                throw;
            }
        }

        [MenuItem("Gamesim/Port/Stop Isolated Preview")]
        public static void StopIsolatedPreview()
        {
            if (!IsIsolatedPreview) { Debug.Log("There is no isolated Gamesim preview to stop."); return; }
            if (EditorApplication.isPlayingOrWillChangePlaymode) EditorApplication.isPlaying = false;
            else RestoreSceneSetup();
        }

        [MenuItem("Gamesim/Port/Capture Preview Screen")]
        public static void CapturePreviewScreen()
        {
            RequireReadyPreview();
            var path = Path.Combine(PreviewDirectory, "preview-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff") + ".png");
            ScreenCapture.CaptureScreenshot(path);
            Debug.Log("Preview capture requested; Unity writes it after the rendered frame: " + path);
        }

        [MenuItem("Gamesim/Port/Show Preview Settings")]
        public static void ShowSettings() => RequireReadyPreview().OpenSettings();

        [MenuItem("Gamesim/Port/Show Preview Notebook")]
        public static void ShowNotebook() => RequireReadyPreview().OpenJournal();

        private static EpisodeDirector RequireReadyPreview()
        {
            if (!IsIsolatedPreview || !EditorApplication.isPlaying || EditorApplication.isCompiling)
                throw new InvalidOperationException("Start an isolated preview and wait for its episode to be ready first.");
            ApplySaveIsolation();
            var scene = SceneManager.GetSceneByPath(ScenePath);
            var director = scene.IsValid() && scene.isLoaded
                ? scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<EpisodeDirector>(true)).SingleOrDefault()
                : null;
            if (director == null || !director.IsReady)
                throw new InvalidOperationException("The isolated episode has not finished initialization.");
            return director;
        }

        private static void ApplySaveIsolation()
        {
            if (IsIsolatedPreview) EpisodeDirector.SaveRootOverride = PreviewDirectory;
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (!IsIsolatedPreview) return;
            if (state == PlayModeStateChange.ExitingEditMode || state == PlayModeStateChange.EnteredPlayMode)
                ApplySaveIsolation();
            else if (state == PlayModeStateChange.EnteredEditMode) RestoreSceneSetup();
        }

        private static void RecoverAfterReload()
        {
            if (!IsIsolatedPreview) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode) ApplySaveIsolation();
            else if (!EditorApplication.isCompiling && !EditorApplication.isUpdating) RestoreSceneSetup();
        }

        private static void EnsureScenesClean()
        {
            for (int index = 0; index < SceneManager.sceneCount; index++)
            {
                var scene = SceneManager.GetSceneAt(index);
                if (scene.isDirty)
                    throw new InvalidOperationException("Save modified scenes before changing preview setup: " + scene.name);
            }
        }

        private static void RestoreSceneSetup()
        {
            EpisodeDirector.SaveRootOverride = null;
            if (!IsIsolatedPreview) return;
            try
            {
                EnsureScenesClean();
                var stored = JsonUtility.FromJson<SavedSetup>(SessionState.GetString(SetupKey, ""));
                if (stored?.scenes == null || stored.scenes.Length == 0)
                    throw new InvalidOperationException("The preview's original scene setup is missing; automatic restoration was not attempted.");
                var setup = stored.scenes.Select(scene => new SceneSetup
                { path = scene.path, isLoaded = scene.isLoaded, isActive = scene.isActive }).ToArray();
                EditorSceneManager.RestoreSceneManagerSetup(setup);
                var directory = PreviewDirectory;
                SessionState.EraseString(RootKey);
                SessionState.EraseString(SetupKey);
                Debug.Log("Isolated preview stopped and previous scene setup restored. Preview saves/captures were retained at " + directory);
            }
            catch (Exception exception)
            {
                // Keep recovery metadata for a manual Stop retry; never discard a dirty scene.
                Debug.LogError("The save override was cleared, but preview scene restoration needs attention. Save any scene changes and choose Stop Isolated Preview again. " + exception.Message);
            }
        }
    }
}
