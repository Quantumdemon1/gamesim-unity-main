using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Gamesim.Editor
{
    /// <summary>
    /// Explicit material preparation for this fourteen-model batch. No import callback,
    /// catalogue scan, scene rebuild or change to the shared authored-asset importer.
    /// Run only after the native FBX files and external maps have been admitted.
    /// </summary>
    public static class HouseSmallPropMaterials
    {
        public const string Root = "Assets/Gamesim/Art/Authored/HouseSmallProps/";
        public const string ShaderName = "Universal Render Pipeline/Lit";
        public const string Materials = Root + "Materials/";
        public const string Textures = Root + "Textures/";

        private sealed class Slot
        {
            public readonly string Name;
            public readonly Color LinearColor;
            public readonly float Metallic, Roughness;
            public readonly bool Mapped, Normal;
            public Slot(string name, Color color, float metallic, float roughness, bool mapped, bool normal = false)
            { Name = name; LinearColor = color; Metallic = metallic; Roughness = roughness; Mapped = mapped; Normal = normal; }
        }
        private sealed class Piece
        {
            public readonly string Id;
            public readonly Slot[] Slots;
            public Piece(string id, params Slot[] slots) { Id = id; Slots = slots; }
        }

        // Exact source material names and linear PBR factors from the delivered GLBs.
        // The metallic maps already contain the final metallic and smoothness values.
        private static readonly Piece[] Pieces =
        {
            new Piece("bb_set_coffee_tin",
                new Slot("bb_mat_bb_set_coffee_tin", new Color(1f, 1f, 1f, 1f), 1f, 1f, true, false),
                new Slot("bb_mat_bb_set_coffee_tin_trim", new Color(0.479320168f, 0.274677306f, 0.0528606474f, 1f), 0.280000001f, 0.379999995f, false, false)),
            new Piece("bb_set_food_jar",
                new Slot("bb_mat_bb_set_food_jar", new Color(1f, 1f, 1f, 1f), 1f, 1f, true, false),
                new Slot("bb_mat_bb_set_food_jar_trim", new Color(0.064803265f, 0.278894275f, 0.366252601f, 1f), 0.280000001f, 0.379999995f, false, false)),
            new Piece("bb_set_cereal_carton",
                new Slot("bb_mat_bb_set_cereal_carton", new Color(1f, 1f, 1f, 1f), 1f, 1f, true, false),
                new Slot("bb_mat_bb_set_cereal_carton_trim", new Color(0.479320168f, 0.274677306f, 0.0528606474f, 1f), 0f, 0.379999995f, false, false),
                new Slot("bb_mat_bb_set_cereal_carton_base", new Color(0.0379999988f, 0.050999999f, 0.063000001f, 1f), 0f, 0.550000012f, false, false)),
            new Piece("bb_set_snack_blue",
                new Slot("bb_mat_bb_set_snack_blue", new Color(1f, 1f, 1f, 1f), 1f, 1f, true, false),
                new Slot("bb_mat_bb_set_snack_blue_trim", new Color(0.064803265f, 0.278894275f, 0.366252601f, 1f), 0f, 0.379999995f, false, false)),
            new Piece("bb_set_snack_gold",
                new Slot("bb_mat_bb_set_snack_gold", new Color(1f, 1f, 1f, 1f), 1f, 1f, true, false),
                new Slot("bb_mat_bb_set_snack_gold_trim", new Color(0.479320168f, 0.274677306f, 0.0528606474f, 1f), 0f, 0.379999995f, false, false)),
            new Piece("bb_set_hoh_snack_blue",
                new Slot("bb_mat_bb_set_hoh_snack_blue", new Color(1f, 1f, 1f, 1f), 1f, 1f, true, false),
                new Slot("bb_mat_bb_set_hoh_snack_blue_trim", new Color(0.064803265f, 0.278894275f, 0.366252601f, 1f), 0f, 0.379999995f, false, false)),
            new Piece("bb_set_hoh_snack_gold",
                new Slot("bb_mat_bb_set_hoh_snack_gold", new Color(1f, 1f, 1f, 1f), 1f, 1f, true, false),
                new Slot("bb_mat_bb_set_hoh_snack_gold_trim", new Color(0.479320168f, 0.274677306f, 0.0528606474f, 1f), 0f, 0.379999995f, false, false)),
            new Piece("bb_set_game_strategy",
                new Slot("bb_mat_bb_set_game_strategy_base", new Color(0.0379999988f, 0.050999999f, 0.063000001f, 1f), 0f, 0.550000012f, false, false),
                new Slot("bb_mat_bb_set_game_strategy", new Color(1f, 1f, 1f, 1f), 1f, 1f, true, false),
                new Slot("bb_mat_bb_set_game_strategy_trim", new Color(0.064803265f, 0.278894275f, 0.366252601f, 1f), 0f, 0.379999995f, false, false)),
            new Piece("bb_set_game_alliance",
                new Slot("bb_mat_bb_set_game_alliance_base", new Color(0.0379999988f, 0.050999999f, 0.063000001f, 1f), 0f, 0.550000012f, false, false),
                new Slot("bb_mat_bb_set_game_alliance", new Color(1f, 1f, 1f, 1f), 1f, 1f, true, false),
                new Slot("bb_mat_bb_set_game_alliance_trim", new Color(0.479320168f, 0.274677306f, 0.0528606474f, 1f), 0f, 0.379999995f, false, false)),
            new Piece("bb_set_game_social",
                new Slot("bb_mat_bb_set_game_social_base", new Color(0.0379999988f, 0.050999999f, 0.063000001f, 1f), 0f, 0.550000012f, false, false),
                new Slot("bb_mat_bb_set_game_social", new Color(1f, 1f, 1f, 1f), 1f, 1f, true, false),
                new Slot("bb_mat_bb_set_game_social_trim", new Color(0.064803265f, 0.278894275f, 0.366252601f, 1f), 0f, 0.379999995f, false, false)),
            new Piece("bb_set_toiletry",
                new Slot("bb_mat_bb_set_toiletry", new Color(1f, 1f, 1f, 1f), 1f, 1f, true, false),
                new Slot("bb_mat_bb_set_toiletry_trim", new Color(0.064803265f, 0.278894275f, 0.366252601f, 1f), 0f, 0.379999995f, false, false)),
            new Piece("bb_set_cosmetic",
                new Slot("bb_mat_bb_set_cosmetic", new Color(1f, 1f, 1f, 1f), 1f, 1f, true, false),
                new Slot("bb_mat_bb_set_cosmetic_trim", new Color(0.479320168f, 0.274677306f, 0.0528606474f, 1f), 0f, 0.379999995f, false, false)),
            new Piece("bb_set_mini_fridge",
                new Slot("bb_mat_mini_fridge_atlas", new Color(1f, 1f, 1f, 1f), 1f, 1f, true, true)),
            new Piece("bb_set_headphone_case",
                new Slot("bb_mat_headphone_case_atlas", new Color(1f, 1f, 1f, 1f), 1f, 1f, true, true)),
        };

        [MenuItem("Gamesim/U07/Prepare authored house small props (14 models)")]
        public static void Prepare()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("Prepare house props while the editor is idle.");
            var shader = Shader.Find(ShaderName);
            if (shader == null) throw new InvalidOperationException("URP/Lit is required.");
            // Check the complete batch before writing any material or importer settings.
            foreach (var piece in Pieces) Preflight(piece);
            EnsureFolder(Materials.TrimEnd('/'));
            foreach (var piece in Pieces)
            {
                PrepareTexture(piece.Id, "albedo", false, true);
                PrepareTexture(piece.Id, "metallic", false, false);
                if (piece.Slots.Any(slot => slot.Normal)) PrepareTexture(piece.Id, "normal", true, false);
                var importer = (ModelImporter)AssetImporter.GetAtPath(ModelPath(piece.Id));
                foreach (var slot in piece.Slots)
                {
                    var material = BuildMaterial(shader, piece.Id, slot);
                    importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), slot.Name), material);
                }
                // Keep the shared importer responsible for units, axes, rig and readability.
                importer.importNormals = ModelImporterNormals.Import;
                importer.importTangents = ModelImporterTangents.CalculateMikk;
                importer.SaveAndReimport();
                VerifyRemaps(piece);
            }
            Debug.Log("[Gamesim] Prepared 14 authored house props and their own materials. No scene was opened or saved.");
        }

        private static string ModelPath(string id) => Root + id + ".fbx";
        private static string TexturePath(string id, string kind) => Textures + id + "_" + kind + ".png";

        private static void Preflight(Piece piece)
        {
            string path = ModelPath(piece.Id);
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (importer == null || model == null) throw new InvalidOperationException("Missing imported model: " + path);
            var names = model.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials)
                .Select(m => m != null ? m.name : "<missing>").Distinct().OrderBy(n => n, StringComparer.Ordinal).ToArray();
            var expected = piece.Slots.Select(s => s.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray();
            if (!names.SequenceEqual(expected))
                throw new InvalidOperationException(path + " material slots differ from the reviewed batch: " + string.Join(", ", names));
            foreach (string kind in new[] { "albedo", "metallic" }.Concat(piece.Slots.Any(s => s.Normal) ? new[] { "normal" } : Array.Empty<string>()))
            {
                string texture = TexturePath(piece.Id, kind);
                if (!File.Exists(texture) || !(AssetImporter.GetAtPath(texture) is TextureImporter))
                    throw new InvalidOperationException("Missing imported external map: " + texture);
            }
            foreach (var slot in piece.Slots)
            {
                var identifier = new AssetImporter.SourceAssetIdentifier(typeof(Material), slot.Name);
                string target = Materials + slot.Name + ".mat";
                if (importer.GetExternalObjectMap().TryGetValue(identifier, out var mapped)
                    && mapped != null && AssetDatabase.GetAssetPath(mapped) != target)
                    throw new InvalidOperationException(path + " already has an artist remap outside this batch: " + slot.Name);
                var existing = AssetDatabase.LoadMainAssetAtPath(target);
                if (existing != null && !(existing is Material)) throw new InvalidOperationException("Material path is occupied: " + target);
            }
        }

        private static void PrepareTexture(string id, string kind, bool normal, bool color)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(TexturePath(id, kind));
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.textureShape = TextureImporterShape.Texture2D;
            importer.sRGBTexture = color;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Trilinear;
            importer.mipmapEnabled = true;
            importer.maxTextureSize = 1024;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.isReadable = false;
            importer.SaveAndReimport();
        }

        private static Material BuildMaterial(Shader shader, string id, Slot slot)
        {
            string path = Materials + slot.Name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = slot.Name };
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                Undo.RecordObject(material, "Prepare house prop material");
                material.shader = shader;
            }
            material.shaderKeywords = Array.Empty<string>();
            // Match this project's glTFast ShaderGraphMaterialGenerator: baseColorLinear.gamma.
            // Preserve that authoring convention for URP/Lit's ordinary _BaseColor property.
            material.SetColor("_BaseColor", slot.LinearColor.gamma);
            material.SetTexture("_BaseMap", slot.Mapped ? Map(id, "albedo") : null);
            material.SetTextureScale("_BaseMap", Vector2.one);
            material.SetTextureOffset("_BaseMap", Vector2.zero);
            material.SetTexture("_MetallicGlossMap", slot.Mapped ? Map(id, "metallic") : null);
            material.SetFloat("_Metallic", slot.Mapped ? 1f : slot.Metallic);
            material.SetFloat("_Smoothness", slot.Mapped ? 1f : 1f - slot.Roughness);
            material.SetFloat("_SmoothnessTextureChannel", 0f);
            material.SetFloat("_WorkflowMode", 1f);
            if (slot.Mapped) material.EnableKeyword("_METALLICSPECGLOSSMAP");
            material.SetTexture("_BumpMap", slot.Normal ? Map(id, "normal") : null);
            material.SetFloat("_BumpScale", slot.Normal ? 0.3f : 1f);
            if (slot.Normal) material.EnableKeyword("_NORMALMAP");
            material.SetTexture("_OcclusionMap", null);
            material.SetTexture("_EmissionMap", null);
            material.SetColor("_EmissionColor", Color.black);
            material.SetFloat("_Surface", 0f);
            material.SetFloat("_AlphaClip", 0f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_Cull", (float)CullMode.Back);
            material.SetFloat("_ZWrite", 1f);
            material.SetFloat("_SrcBlend", (float)BlendMode.One);
            material.SetFloat("_DstBlend", (float)BlendMode.Zero);
            material.SetOverrideTag("RenderType", "Opaque");
            material.renderQueue = (int)RenderQueue.Geometry;
            material.doubleSidedGI = false;
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            material.SetShaderPassEnabled("ShadowCaster", true);
            material.SetShaderPassEnabled("DepthOnly", true);
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssetIfDirty(material);
            return material;
        }

        private static Texture2D Map(string id, string kind)
        {
            string path = TexturePath(id, kind);
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null) throw new InvalidOperationException("Texture did not import: " + path);
            return texture;
        }

        private static void VerifyRemaps(Piece piece)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath(piece.Id));
            foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
            foreach (var material in renderer.sharedMaterials)
                if (material == null || AssetDatabase.GetAssetPath(material) != Materials + material.name + ".mat"
                    || material.shader == null || material.shader.name != ShaderName)
                    throw new InvalidOperationException("External material did not survive import: " + piece.Id);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
