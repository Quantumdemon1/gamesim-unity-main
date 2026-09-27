using Gamesim.Presentation;
using UnityEngine;

namespace Gamesim.Uma
{
    /// <summary>
    /// Pulls a freshly generated UMA character toward the house's Kenney-derived look.
    ///
    /// The house is flat-shaded, low-specular and reads at a distance; UMA ships tuned for a much
    /// more photographic target — glossy skin, pore-level normal maps, a subsurface skin shader.
    /// Standing one next to the other is the mismatch, not the mesh density, so this flattens the
    /// generated materials rather than replacing the content.
    ///
    /// UMA builds its materials per character at runtime, so every value written here lands on an
    /// instance owned by one avatar. Nothing touches a project asset.
    /// </summary>
    public static class UmaStylizer
    {
        // Enough sheen to keep eyes and shoes from going dead flat, far below UMA's default.
        private const float Smoothness = 0.08f;
        private const float Occlusion = 0.25f;

        private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
        private static readonly int GlossinessId = Shader.PropertyToID("_Glossiness");
        private static readonly int MetallicId = Shader.PropertyToID("_Metallic");
        private static readonly int BumpMapId = Shader.PropertyToID("_BumpMap");
        private static readonly int BumpScaleId = Shader.PropertyToID("_BumpScale");
        private static readonly int OcclusionStrengthId = Shader.PropertyToID("_OcclusionStrength");
        private static readonly int SpecularId = Shader.PropertyToID("_SpecColor");

        private static readonly int SmoothnessRemapId = Shader.PropertyToID("_Smoothness_Remap");
        private static readonly int DetailGlossId = Shader.PropertyToID("_Detail_Gloss_Scale");
        private static readonly int SubsurfaceColourId = Shader.PropertyToID("_SubsurfaceColor");
        private static readonly int SubsurfaceBlendId = Shader.PropertyToID("_SSSBlend");

        /// <summary>The skin shader's own subsurface tint, tuned for a light complexion, and a deep one's.</summary>
        private static readonly Color LightSubsurface = new Color(0.93f, 0.375f, 0.313f), DeepSubsurface = new Color(0.5f, 0.27f, 0.2f);

        /// <summary>Flattens every material under <paramref name="root"/>. Safe to call repeatedly.</summary>
        public static void Apply(GameObject root) => Apply(root, null);

        /// <summary>
        /// Flattens every material under <paramref name="root"/>, and tunes the skin for its tone.
        ///
        /// <para>The skin shader takes none of the properties the flattening writes, so it kept UMA's
        /// photographic gloss: a specular sheen that reads as grey on a dark albedo - the ashy look a
        /// deep complexion had - and a subsurface glow tuned pink for light skin that tinted every
        /// face the same. Its own gloss range is brought down for everyone, and the subsurface
        /// deepens and softens with the tone.</para>
        /// </summary>
        public static void Apply(GameObject root, Color? skin)
        {
            if (root == null) return;
            float depth = skin.HasValue ? 1f - Mathf.Clamp01(.2126f * skin.Value.r + .7152f * skin.Value.g + .0722f * skin.Value.b) : .3f;
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                // Hair and accessories built in code carry finishes of their own - a chrome visor
                // is meant to shine.
                if (renderer.GetComponent<GrownPiece>() != null) continue;
                foreach (var material in renderer.sharedMaterials)
                {
                    if (material == null) continue;
                    Flatten(material);
                    if (material.HasProperty(SmoothnessRemapId)) TuneSkin(material, depth);
                }
            }
        }

        private static void TuneSkin(Material material, float depth)
        {
            material.SetVector(SmoothnessRemapId, new Vector4(0f, Mathf.Lerp(.34f, .26f, depth), 0f, 0f));
            if (material.HasProperty(DetailGlossId)) material.SetFloat(DetailGlossId, .3f);
            float deep = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.25f, .85f, depth));
            if (material.HasProperty(SubsurfaceColourId)) material.SetColor(SubsurfaceColourId, Color.Lerp(LightSubsurface, DeepSubsurface, deep));
            if (material.HasProperty(SubsurfaceBlendId)) material.SetFloat(SubsurfaceBlendId, Mathf.Lerp(.5f, .28f, deep));
        }

        private static void Flatten(Material material)
        {
            if (material.HasProperty(SmoothnessId)) material.SetFloat(SmoothnessId, Smoothness);
            if (material.HasProperty(GlossinessId)) material.SetFloat(GlossinessId, Smoothness);
            if (material.HasProperty(MetallicId)) material.SetFloat(MetallicId, 0f);
            if (material.HasProperty(SpecularId)) material.SetColor(SpecularId, new Color(0.08f, 0.08f, 0.08f, 1f));

            // Skin pores and fabric weave are the loudest part of the mismatch. Scaling the normal
            // map to nothing keeps the shader variant intact where clearing the texture would not.
            if (material.HasProperty(BumpScaleId)) material.SetFloat(BumpScaleId, 0f);
            else if (material.HasProperty(BumpMapId)) material.SetTexture(BumpMapId, null);

            if (material.HasProperty(OcclusionStrengthId)) material.SetFloat(OcclusionStrengthId, Occlusion);
        }
    }
}
