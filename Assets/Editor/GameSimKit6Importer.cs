#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace GameSim.ArtTools.Refinement6
{
    /// <summary>
    /// Manual, opt-in import utility. It only changes textures explicitly listed in this kit's manifest.
    /// It never changes scenes, prefabs, controllers, captions, palettes, player preferences, or lighting.
    /// This helper is intended for the GameSim Unity project and only touches Kit 6 textures listed in its manifest.
    /// </summary>
    public static class GameSimKit6Importer
    {
        private const string Root = "Assets/Gamesim/UI/RefinementKit6/";
        private const string ManifestPath = Root + "Settings/asset_manifest.json";

        [Serializable] private sealed class Manifest { public Entry[] assets; }
        [Serializable] private sealed class Entry
        {
            public string id;
            public string path;
            public int width;
            public int height;
            public int[] border; // left, bottom, right, top (Unity Vector4 order)
            public float pixelsPerUnit = 100f;
        }

        [MenuItem("Tools/GameSim/Refinement Kit 6/Apply Sprite Import Settings")]
        public static void ApplyImportSettings()
        {
            if (!File.Exists(ManifestPath))
            {
                Debug.LogError("Kit 6 manifest not found. Copy the supplied Assets/Gamesim/UI/RefinementKit6 folder into the project. Expected " + ManifestPath);
                return;
            }

            Manifest data;
            try { data = JsonUtility.FromJson<Manifest>(File.ReadAllText(ManifestPath)); }
            catch (Exception e) { Debug.LogError("Invalid Kit 6 manifest: " + e.Message); return; }

            if (data == null || data.assets == null || data.assets.Length == 0)
            {
                Debug.LogError("Kit 6 manifest has no asset entries.");
                return;
            }

            if (!EditorUtility.DisplayDialog(
                "Configure GameSim Kit 6 sprites?",
                "Applies uncompressed Sprite/Single, Full Rect, no mipmaps, Clamp, Bilinear, 100 PPU, and each sprite's exact border to " +
                data.assets.Length + " kit textures only.\n\nUse version control first. Existing UI and lighting are not changed.",
                "Apply to kit textures",
                "Cancel"))
                return;

            int ok = 0, failures = 0;
            try
            {
                for (int i = 0; i < data.assets.Length; i++)
                {
                    Entry e = data.assets[i];
                    EditorUtility.DisplayProgressBar("Importing Kit 6 sprites", e.id, (float)i / data.assets.Length);

                    try
                    {
                        if (string.IsNullOrEmpty(e.path) ||
                            !e.path.StartsWith("Sprites/", StringComparison.Ordinal) ||
                            e.path.Contains("..") ||
                            !e.path.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                            throw new InvalidDataException("Rejected path outside kit sprite scope.");

                        if (e.border == null || e.border.Length != 4 || e.pixelsPerUnit <= 0)
                            throw new InvalidDataException("Invalid border or PPU.");

                        foreach (int b in e.border)
                            if (b < 0) throw new InvalidDataException("Negative border.");

                        if (e.border[0] + e.border[2] >= e.width ||
                            e.border[1] + e.border[3] >= e.height)
                            throw new InvalidDataException("Nine-slice borders leave no stretchable center.");

                        string path = Root + e.path;
                        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                        if (importer == null)
                            throw new FileNotFoundException("Texture importer not found", path);

                        importer.textureType = TextureImporterType.Sprite;
                        importer.spriteImportMode = SpriteImportMode.Single;
                        importer.spritePixelsPerUnit = e.pixelsPerUnit;
                        importer.spriteBorder = new Vector4(e.border[0], e.border[1], e.border[2], e.border[3]);
                        importer.mipmapEnabled = false;
                        importer.isReadable = false;
                        importer.alphaSource = TextureImporterAlphaSource.FromInput;
                        importer.alphaIsTransparency = true;
                        importer.sRGBTexture = true;
                        importer.filterMode = FilterMode.Bilinear;
                        importer.wrapMode = TextureWrapMode.Clamp;
                        importer.textureCompression = TextureImporterCompression.Uncompressed;
                        importer.npotScale = TextureImporterNPOTScale.None;
                        importer.maxTextureSize = 2048;

                        var settings = new TextureImporterSettings();
                        importer.ReadTextureSettings(settings);
                        settings.spriteMeshType = SpriteMeshType.FullRect;
                        importer.SetTextureSettings(settings);
                        importer.SaveAndReimport();
                        ok++;
                    }
                    catch (Exception ex)
                    {
                        failures++;
                        Debug.LogError("Kit 6 " + e.id + ": " + ex.Message);
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            Debug.Log("Kit 6 import complete: " + ok + " configured, " + failures + " failed. No scene or prefab was modified.");
        }
    }
}
#endif
