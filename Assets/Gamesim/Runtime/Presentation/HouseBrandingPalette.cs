using UnityEngine;

namespace Gamesim.Presentation
{
    /// <summary>
    /// The pack-5 textures and the one material template the house's runtime branding is built
    /// from, in a single asset the runtime can load by name.
    ///
    /// <para>Why an asset rather than a path. The pack-5 world textures live outside Resources, so a
    /// build ships only the ones something references - and this asset, in Resources, is that
    /// reference: every texture named here ships, the other seventy-odd stay out. The material is
    /// here for the same reason. A transparent additive URP Unlit made from scratch at runtime
    /// works in the editor and can render wrong in a player, whose build strips shader variants no
    /// material asked for; a template asset asks for the variant, and runtime copies it.</para>
    ///
    /// <para>Built by <c>Gamesim.Editor.HouseBrandingAssets</c>, never by hand.</para>
    /// </summary>
    public sealed class HouseBrandingPalette : ScriptableObject
    {
        public const string ResourceName = "HouseBrandingPalette";

        /// <summary>URP Unlit, transparent, additive, both faces: a lit line on a dark surface.</summary>
        public Material glowTemplate;

        [Header("Diary Room")]
        public Texture2D diaryHalo;
        public Texture2D diaryNeon;

        private static HouseBrandingPalette current;

        /// <summary>The palette, or null on a clone that has not built it - callers then dress nothing.</summary>
        public static HouseBrandingPalette Current
        {
            get
            {
                if (current == null) current = Resources.Load<HouseBrandingPalette>(ResourceName);
                return current;
            }
        }

        /// <summary>
        /// A glowing copy of the template carrying <paramref name="texture"/>, tinted
        /// <paramref name="colour"/>; above 1 the tint reads as light through the bloom. Null when
        /// the template is missing. The caller owns the copy and destroys it.
        /// </summary>
        public Material Glow(string name, Texture texture, Color colour)
        {
            if (glowTemplate == null) return null;
            var material = new Material(glowTemplate) { name = name };
            material.SetTexture("_BaseMap", texture != null ? texture : Texture2D.whiteTexture);
            material.SetColor("_BaseColor", colour);
            return material;
        }
    }
}
