using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Gamesim.Editor
{
    /// <summary>
    /// Lays the authored floor textures (Part 4, Tier 2) over the eight room floors of the episode
    /// house: planks, tile, and lawn, under <c>Art/Authored/Textures</c>.
    ///
    /// <para>Seven of the eight now wear a photographed surface - Poly Haven's scans, fetched by
    /// <c>ArtSource/textures/bb_tex_polyhaven.py</c>, four maps each (VISUAL-TARGET.md V4). The
    /// yard keeps the lawn baked by <c>ArtSource/textures/bb_tex_floors.py</c>, which is a field of
    /// noise and tiles at four metres without a seam that reads. Every row carries the metres one
    /// tile of its surface covers in life, straight off the asset's page, so a plank is a plank's
    /// width underfoot rather than whatever a guess made it.</para>
    ///
    /// <para>Each floor gets its own material asset, tiled to that size, and tinted down toward the
    /// darkened broadcast tone the flat floors had - a floor that renders its albedo at full
    /// strength under the set's lighting reads as a showroom, not the house. Where a surface ships
    /// the metallic and occlusion maps a scan comes with, the material wears those too, and its
    /// smoothness comes from the map rather than one number for the whole floor. The floor
    /// primitives themselves are untouched: same colliders, same NavMesh, only the material
    /// changes, and a missing texture leaves that floor as it was.</para>
    /// </summary>
    public static class HouseFloorDressing
    {
        private const string EpisodeScene = "Assets/Gamesim/Scenes/EpisodeHouse.unity";
        private const string WorldRoot = "House Architecture";
        public const string Textures = AuthoredAssetImporter.Root + "Textures/";
        public const string Materials = AuthoredAssetImporter.Root + "Materials/";

        /// <summary>A floor's texture set, the tile it repeats at, and the tone it is tinted to.</summary>
        public readonly struct Floor
        {
            public readonly string Name, Texture;
            public readonly float Tile;
            public readonly Color Tint;
            public Floor(string name, string texture, float tile, Color tint) { Name = name; Texture = texture; Tile = tile; Tint = tint; }
        }

        // The tile of each Poly Haven surface is its own size in life: Wood Floor is 1.70 m across,
        // Dark Wooden Planks 2.00, Interior Tiles 1.90. The lawn is the baked one, at four.
        public static readonly Floor[] Plan =
        {
            new Floor("Living room floor",      "bb_tex_ph_oak",     1.70f, new Color(0.50f, 0.46f, 0.42f)),
            new Floor("Kitchen floor",          "bb_tex_ph_tile",    1.90f, new Color(0.55f, 0.58f, 0.62f)),
            new Floor("Bedroom floor",          "bb_tex_ph_walnut",  2.00f, new Color(0.55f, 0.52f, 0.55f)),
            new Floor("Private room floor",     "bb_tex_ph_oak",     1.70f, new Color(0.46f, 0.50f, 0.46f)),
            new Floor("Competition yard floor", "bb_tex_lawn",       4.00f, new Color(0.55f, 0.62f, 0.50f)),
            new Floor("HoH floor",              "bb_tex_ph_walnut",  2.00f, new Color(0.58f, 0.52f, 0.50f)),
            new Floor("Nomination floor",       "bb_tex_ph_oak",     1.70f, new Color(0.46f, 0.44f, 0.46f)),
            new Floor("Games floor",            "bb_tex_ph_tile",    1.90f, new Color(0.50f, 0.54f, 0.60f)),
        };

        public static string MaterialPath(string floorName) =>
            Materials + "bb_mat_floor_" + floorName.ToLowerInvariant().Replace(" floor", "").Replace(' ', '_') + ".mat";

        [MenuItem("Gamesim/U07/Lay the authored floors")]
        public static void Apply()
        {
            var scene = EditorSceneManager.OpenScene(EpisodeScene, OpenSceneMode.Single);
            var world = scene.GetRootGameObjects().FirstOrDefault(root => root.name == WorldRoot);
            if (world == null) throw new InvalidOperationException("No '" + WorldRoot + "' root in the episode scene.");
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("URP/Lit is not available.");
            if (!AssetDatabase.IsValidFolder(Materials.TrimEnd('/')))
                AssetDatabase.CreateFolder(AuthoredAssetImporter.Root.TrimEnd('/'), "Materials");

            int laid = 0, missing = 0;
            foreach (var floor in Plan)
            {
                var renderer = world.GetComponentsInChildren<Transform>(true)
                    .Where(t => t.name == floor.Name).Select(t => t.GetComponent<Renderer>()).FirstOrDefault(r => r != null);
                if (renderer == null) { Debug.LogWarning("[Gamesim] floors · no floor named " + floor.Name); missing++; continue; }
                var albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(Textures + floor.Texture + "_albedo.png");
                var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(Textures + floor.Texture + "_normal.png");
                if (albedo == null || normal == null)
                {
                    Debug.LogWarning("[Gamesim] floors · " + floor.Texture + " is not baked; " + floor.Name + " keeps its flat tone.");
                    missing++;
                    continue;
                }

                string path = MaterialPath(floor.Name);
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    material = new Material(shader) { name = System.IO.Path.GetFileNameWithoutExtension(path) };
                    AssetDatabase.CreateAsset(material, path);
                }
                material.shader = shader;
                material.SetTexture("_BaseMap", albedo);
                material.SetTexture("_BumpMap", normal);
                material.EnableKeyword("_NORMALMAP");
                material.SetColor("_BaseColor", floor.Tint);
                material.SetFloat("_Smoothness", floor.Texture.Contains("tile") ? 0.45f : 0.25f);
                Surface(material, floor.Texture);
                var size = Vector3.Scale(renderer.transform.lossyScale, Vector3.one);
                var tiling = new Vector2(Mathf.Max(1f, size.x / floor.Tile), Mathf.Max(1f, size.z / floor.Tile));
                material.SetTextureScale("_BaseMap", tiling);
                EditorUtility.SetDirty(material);
                renderer.sharedMaterial = material;
                laid++;
            }

            bool walls = Walls();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log(string.Format("[Gamesim] floors · {0} floors laid with authored textures, {1} left as they were; wall plaster {2}",
                laid, missing, walls ? "laid" : "not baked"));
        }

        /// <summary>
        /// The maps a scan brings that a bake does not: occlusion, and metalness with the
        /// surface's own smoothness in its alpha. A surface without them keeps the one smoothness
        /// number set above, and a baked pair is left exactly as it was.
        /// </summary>
        private static void Surface(Material material, string texture)
        {
            var metallic = AssetDatabase.LoadAssetAtPath<Texture2D>(Textures + texture + "_metallic.png");
            var occlusion = AssetDatabase.LoadAssetAtPath<Texture2D>(Textures + texture + "_occlusion.png");
            material.SetTexture("_MetallicGlossMap", metallic);
            if (metallic != null)
            {
                material.EnableKeyword("_METALLICSPECGLOSSMAP");
                material.SetFloat("_SmoothnessTextureChannel", 0f);   // the metallic map's alpha
                material.SetFloat("_Smoothness", 1f);
                material.SetFloat("_Metallic", 1f);
            }
            else material.DisableKeyword("_METALLICSPECGLOSSMAP");
            material.SetTexture("_OcclusionMap", occlusion);
            if (occlusion != null)
            {
                material.EnableKeyword("_OCCLUSIONMAP");
                material.SetFloat("_OcclusionStrength", 1f);
            }
            else material.DisableKeyword("_OCCLUSIONMAP");
        }

        public const string WallMaterial = AuthoredAssetImporter.Root + "Shell/Materials/bb_mat_shell_wall.mat";
        public const string WallTexture = "bb_tex_ph_plaster";
        public const float WallTile = 2f;
        public static readonly Color WallTint = new Color(0.22f, 0.24f, 0.26f);

        /// <summary>
        /// The shell's wall slab gets the plaster: its meshes carry world-scale box UVs (one metre to
        /// one UV unit), so a tile that is two metres on the floor is two metres on the wall too -
        /// and White Stucco, the scan it now wears, is two metres in life, which is why the tile did
        /// not have to move. The tint keeps the slab at the darkened tone it has always had; the
        /// texture adds only the mottle and the relief that stop a 28 m wall reading as one flat card.
        /// </summary>
        private static bool Walls()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(WallMaterial);
            var albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(Textures + WallTexture + "_albedo.png");
            var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(Textures + WallTexture + "_normal.png");
            if (material == null || albedo == null || normal == null) return false;
            material.SetTexture("_BaseMap", albedo);
            material.SetTexture("_BumpMap", normal);
            material.EnableKeyword("_NORMALMAP");
            material.SetColor("_BaseColor", WallTint);
            Surface(material, WallTexture);
            material.SetTextureScale("_BaseMap", Vector2.one / WallTile);
            EditorUtility.SetDirty(material);
            return true;
        }
    }
}
