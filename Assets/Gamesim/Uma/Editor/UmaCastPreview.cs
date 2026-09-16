using System.Collections.Generic;
using System.Text;
using Gamesim.Presentation;
using UMA;
using UMA.CharacterSystem;
using UnityEditor;
using UnityEngine;

namespace Gamesim.Uma.Editor
{
    /// <summary>
    /// Builds the whole cast from <see cref="UmaCastLibrary"/> in one row so the looks can be judged
    /// side by side, and reports what each assembled character actually ended up with.
    ///
    /// The preview goes through <see cref="UmaBodyProvider"/> rather than talking to UMA directly,
    /// so what is on screen is the same body a houseguest gets in a running episode.
    /// </summary>
    internal static class UmaCastPreview
    {
        private const string PreviewRoot = "__Gamesim UMA Cast Preview";
        private const float Spacing = 1.2f;

        // Stand-ins for the per-houseguest palette the episode supplies at runtime.
        private static readonly Color[] Palettes =
        {
            new Color(0.26f, 0.76f, 0.65f),
            new Color(0.95f, 0.58f, 0.36f),
            new Color(0.56f, 0.64f, 0.92f),
            new Color(0.93f, 0.78f, 0.36f),
            new Color(0.82f, 0.45f, 0.58f),
            new Color(0.45f, 0.70f, 0.42f),
        };

        [MenuItem("Gamesim/UMA/Stage Cast Preview", priority = 100)]
        private static void Stage()
        {
            if (!Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Gamesim UMA",
                    "UMA assembles characters over several frames, so the cast preview needs play mode.\n\n" +
                    "Enter play mode, then run this again.", "OK");
                return;
            }

            Clear();

            var root = new GameObject(PreviewRoot);
            root.transform.position = new Vector3(0f, 0f, 10f);
            root.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

            var provider = new UmaBodyProvider();
            var ids = new List<string>(UmaCastLibrary.AppearanceIds);
            float origin = -(ids.Count - 1) * Spacing * 0.5f;

            for (int i = 0; i < ids.Count; i++)
            {
                var stand = new GameObject(ids[i]);
                stand.transform.SetParent(root.transform, false);
                stand.transform.localPosition = new Vector3(origin + i * Spacing, 0f, 0f);

                if (!provider.TryCreate(ids[i], stand.transform, Palettes[i % Palettes.Length], out _))
                    Debug.LogWarning("[Gamesim.Uma] No body built for '" + ids[i] + "'.");
            }

            Selection.activeGameObject = root;
            Debug.Log("[Gamesim.Uma] Staged " + ids.Count + " cast previews. They finish assembling over the next few frames — " +
                "then run Gamesim > UMA > Report Cast Build.");
        }

        [MenuItem("Gamesim/UMA/Report Cast Build", priority = 101)]
        private static void Report()
        {
            var root = GameObject.Find(PreviewRoot);
            if (root == null)
            {
                Debug.LogWarning("[Gamesim.Uma] No cast preview in the scene. Run Gamesim > UMA > Stage Cast Preview first.");
                return;
            }

            var report = new StringBuilder("[Gamesim.Uma] Cast build report\n");
            foreach (var avatar in root.GetComponentsInChildren<DynamicCharacterAvatar>(true))
            {
                var stand = avatar.transform.parent != null ? avatar.transform.parent.name : avatar.name;
                report.Append("\n=== ").Append(stand).Append(" (").Append(avatar.activeRace.name).Append(") ===\n");

                var renderers = avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                if (renderers.Length == 0)
                {
                    report.Append("   NOT BUILT — no skinned mesh renderer yet\n");
                    continue;
                }

                foreach (var renderer in renderers)
                {
                    var mesh = renderer.sharedMesh;
                    report.Append("   mesh ").Append(mesh != null ? mesh.vertexCount : 0).Append(" verts, ")
                          .Append(renderer.bones.Length).Append(" bones, height ")
                          .Append(renderer.bounds.size.y.ToString("F2")).Append(" m\n");
                    foreach (var material in renderer.sharedMaterials)
                    {
                        if (material == null) continue;
                        report.Append("      ").Append(material.shader.name)
                              .Append("  smoothness=").Append(Read(material, "_Smoothness"))
                              .Append("  metallic=").Append(Read(material, "_Metallic"))
                              .Append("  bumpScale=").Append(Read(material, "_BumpScale"))
                              .Append('\n');
                    }
                }

                report.Append("   wardrobe: ");
                foreach (var pair in avatar.WardrobeRecipes) report.Append(pair.Key).Append('=').Append(pair.Value.name).Append("  ");
                report.Append('\n');

                report.Append("   shared colours: ");
                var colors = avatar.characterColors != null ? avatar.characterColors.Colors : null;
                if (colors == null || colors.Count == 0) report.Append("(none)");
                else foreach (var color in colors) report.Append(color.name).Append('=').Append(ColorUtility.ToHtmlStringRGB(color.color)).Append("  ");
                report.Append('\n');
            }

            Debug.Log(report.ToString());
        }

        [MenuItem("Gamesim/UMA/Clear Cast Preview", priority = 102)]
        private static void Clear()
        {
            var root = GameObject.Find(PreviewRoot);
            if (root == null) return;
            if (Application.isPlaying) Object.Destroy(root); else Object.DestroyImmediate(root);
        }

        private static string Read(Material material, string property) =>
            material.HasProperty(property) ? material.GetFloat(property).ToString("F2") : "-";
    }
}
