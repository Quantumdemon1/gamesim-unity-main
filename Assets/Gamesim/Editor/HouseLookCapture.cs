using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Gamesim.Editor
{
    /// <summary>
    /// Renders a scene's set from the editor, with no play mode and no test harness.
    ///
    /// <para>Comparing two houses is the one question the PlayMode capture cannot answer: it loads
    /// the episode scene by name and drives a real episode, which is the right tool for judging the
    /// episode and the wrong one for judging a scene nobody plays. This opens any scene and
    /// photographs it, so "what does the prototype set actually look like" is a two-minute answer
    /// rather than an argument.</para>
    /// </summary>
    public static class HouseLookCapture
    {
        private const int Width = 1600, Height = 900;

        /// <summary>
        /// Batch entry point. Reads <c>-gamesimScene</c> and <c>-gamesimShot</c> from the command
        /// line so one method serves every scene rather than one method per scene.
        /// </summary>
        public static void CaptureFromCommandLine()
        {
            string scene = Argument("-gamesimScene") ?? "Assets/Gamesim/Scenes/HousePrototype.unity";
            string shot = Argument("-gamesimShot") ?? "house-look.png";
            Capture(scene, shot, Argument("-gamesimFocus"));
        }

        public static void Capture(string scenePath, string outputName, string focusName = null)
        {
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            // Frame the whole set from its own bounds, so a scene laid out on different coordinates
            // is still fully in shot rather than silently cropped.
            var renderers = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None)
                .Where(r => r.enabled && r.gameObject.activeInHierarchy).ToArray();
            if (renderers.Length == 0) throw new InvalidOperationException("Nothing to photograph in " + scenePath);

            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);

            // Framing one fixture instead of the whole set. A memory wall is 1.3 units tall inside a
            // 30-unit house, so the wide shot is the wrong tool for asking whether it was built
            // correctly — it is a handful of pixels there either way.
            if (!string.IsNullOrEmpty(focusName))
            {
                var focus = GameObject.Find(focusName);
                if (focus == null) throw new InvalidOperationException("No object named '" + focusName + "' to focus.");
                var focusRenderers = focus.GetComponentsInChildren<Renderer>(true);
                if (focusRenderers.Length == 0) throw new InvalidOperationException(focusName + " has nothing to render.");
                bounds = focusRenderers[0].bounds;
                foreach (var renderer in focusRenderers) bounds.Encapsulate(renderer.bounds);
                Debug.Log("[Gamesim] focus · " + focusName + " at " + bounds.center.ToString("F2")
                    + " size " + bounds.size.ToString("F2"));
            }

            var rig = new GameObject("Gamesim Look Camera");
            var camera = rig.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.02f, 0.02f, 0.03f, 1f);
            camera.fieldOfView = 40f;

            float reach = bounds.extents.magnitude;
            var direction = string.IsNullOrEmpty(focusName)
                ? new Vector3(0.62f, 0.72f, -0.62f).normalized
                : new Vector3(1f, 0.22f, -0.28f).normalized;
            rig.transform.position = bounds.center + direction * (reach * 2.35f);
            rig.transform.LookAt(bounds.center);
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = reach * 6f;

            var texture = new RenderTexture(Width, Height, 24);
            var readback = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                camera.targetTexture = texture;
                camera.Render();
                RenderTexture.active = texture;
                readback.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                readback.Apply();

                string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", outputName));
                File.WriteAllBytes(path, readback.EncodeToPNG());
                Debug.Log("[Gamesim] set capture -> " + path + "  (" + renderers.Length + " renderers, bounds " + bounds.size + ")");
            }
            finally
            {
                RenderTexture.active = previous;
                camera.targetTexture = null;
                UnityEngine.Object.DestroyImmediate(rig);
                UnityEngine.Object.DestroyImmediate(readback);
                texture.Release();
                UnityEngine.Object.DestroyImmediate(texture);
            }
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
