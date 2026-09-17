using System;
using System.Linq;
using Gamesim.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Gamesim.Editor
{
    /// <summary>
    /// Lists every character-shaped object in a scene and says which of them is a placeholder.
    ///
    /// <para>"A placeholder is still showing" is a question about a specific object in a specific
    /// scene, and the two scenes here answer it differently: the episode house has six characters
    /// all driven by <see cref="CharacterPresentation"/>, which switches the primitive renderers off
    /// at runtime, while the prototype house has primitives and no presentation component at all.
    /// Guessing which one a screenshot came from is how the wrong thing gets deleted.</para>
    /// </summary>
    public static class CharacterAudit
    {
        public static void AuditFromCommandLine()
        {
            string scenePath = Argument("-gamesimScene") ?? "Assets/Gamesim/Scenes/EpisodeHouse.unity";
            Audit(scenePath);
        }

        [MenuItem("Gamesim/U07/Audit characters in the open scene")]
        private static void AuditOpenScene() => Report(EditorSceneManager.GetActiveScene());

        public static void Audit(string scenePath)
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            Report(scene);
        }

        private static void Report(UnityEngine.SceneManagement.Scene scene)
        {
            Debug.Log("[Gamesim] character audit · " + scene.path);

            foreach (var root in scene.GetRootGameObjects())
            foreach (var node in root.GetComponentsInChildren<Transform>(true))
            {
                var body = node.Find("Body");
                var head = node.Find("Head");
                if (body == null && head == null) continue;

                bool presented = node.GetComponent<CharacterPresentation>() != null;
                var bodyRenderer = body != null ? body.GetComponent<Renderer>() : null;
                var headRenderer = head != null ? head.GetComponent<Renderer>() : null;

                Debug.Log(string.Format(
                    "[Gamesim] character · {0} at {1} · presentation: {2} · body mesh: {3} · body material: {4} · "
                    + "renderers enabled: {5}/{6}",
                    Path(node), node.position.ToString("F1"),
                    presented ? "yes" : "NO — primitives are permanent",
                    bodyRenderer is MeshRenderer && body.GetComponent<MeshFilter>() != null
                        ? body.GetComponent<MeshFilter>().sharedMesh?.name ?? "none" : "none",
                    bodyRenderer != null && bodyRenderer.sharedMaterial != null ? bodyRenderer.sharedMaterial.name : "none",
                    bodyRenderer != null && bodyRenderer.enabled ? "body" : "-",
                    headRenderer != null && headRenderer.enabled ? "head" : "-"));
            }
        }

        private static string Path(Transform node)
        {
            string name = node.name;
            for (var parent = node.parent; parent != null; parent = parent.parent) name = parent.name + "/" + name;
            return name;
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
