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

        /// <summary>Flattens every material under <paramref name="root"/>. Safe to call repeatedly.</summary>
        public static void Apply(GameObject root)
        {
            if (root == null) return;
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                foreach (var material in renderer.sharedMaterials)
                {
                    if (material != null) Flatten(material);
                }
            }
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
