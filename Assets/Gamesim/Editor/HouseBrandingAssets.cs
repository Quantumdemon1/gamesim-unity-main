using System;
using Gamesim.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Gamesim.Editor
{
    /// <summary>
    /// Builds the assets the house's runtime branding needs: one glow material template and the
    /// <see cref="HouseBrandingPalette"/> in Resources that names the pack-5 textures in use.
    ///
    /// <para>Run from <c>Gamesim/Branding/Build the house branding palette</c>, or in batchmode with
    /// <c>-executeMethod Gamesim.Editor.HouseBrandingAssets.Build</c>. It re-applies every setting
    /// on each run, so a hand edit to either asset does not survive the next build - which is the
    /// point: what the house looks like is this file, not an inspector.</para>
    /// </summary>
    public static class HouseBrandingAssets
    {
        public const string PalettePath = "Assets/Gamesim/Resources/" + HouseBrandingPalette.ResourceName + ".asset";
        public const string Folder = "Assets/Gamesim/Art/Branding";
        public const string GlowMaterialPath = Folder + "/bb_mat_branding_glow.mat";
        private const string Pack5 = UiPackImporter.WorldRoot + "Pack5_HouseBroadcast/";
        public const string DiaryHaloPath = Pack5 + "EnvironmentWallGraphics/diary_room_halo.png";
        public const string DiaryNeonPath = Pack5 + "NeonMasks/neon_diary_room_mask.png";

        [MenuItem("Gamesim/Branding/Build the house branding palette")]
        public static void Build()
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) throw new InvalidOperationException("URP Unlit is not in the project.");
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Gamesim/Art", "Branding");

            var glow = AssetDatabase.LoadAssetAtPath<Material>(GlowMaterialPath);
            if (glow == null)
            {
                glow = new Material(shader) { name = "bb_mat_branding_glow" };
                AssetDatabase.CreateAsset(glow, GlowMaterialPath);
            }
            glow.shader = shader;
            ConfigureAdditive(glow);
            EditorUtility.SetDirty(glow);

            var palette = AssetDatabase.LoadAssetAtPath<HouseBrandingPalette>(PalettePath);
            if (palette == null)
            {
                palette = ScriptableObject.CreateInstance<HouseBrandingPalette>();
                AssetDatabase.CreateAsset(palette, PalettePath);
            }
            palette.glowTemplate = glow;
            palette.diaryHalo = Texture(DiaryHaloPath);
            palette.diaryNeon = Texture(DiaryNeonPath);
            EditorUtility.SetDirty(palette);
            AssetDatabase.SaveAssets();
            Debug.Log("[Gamesim] House branding palette built: " + PalettePath);
        }

        /// <summary>
        /// Transparent and additive, both faces, no depth write: a lit line added to whatever it is
        /// drawn on, which on a dark surface is a sign and on a lit one fades toward nothing.
        /// </summary>
        public static void ConfigureAdditive(Material material)
        {
            material.SetFloat("_Surface", 1f);   // transparent
            material.SetFloat("_Blend", 2f);     // additive
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.One);
            material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_Cull", (float)CullMode.Off);
            material.SetFloat("_AlphaClip", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHATEST_ON");
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)RenderQueue.Transparent;
            material.SetColor("_BaseColor", Color.white);
            // What URP's own validation writes for a transparent surface drawn on both faces, set
            // here so the asset is born in that state. Left for URP, the first editor to validate
            // it rewrote the committed asset in the middle of a test run, and the review pipeline's
            // audit rightly failed on a product input that changed under it (2026-09-27).
            material.doubleSidedGI = true;
            material.SetShaderPassEnabled("DepthOnly", false);
            material.SetShaderPassEnabled("SHADOWCASTER", false);
        }

        private static Texture2D Texture(string path)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null) throw new InvalidOperationException("Missing pack texture " + path);
            return texture;
        }
    }
}
