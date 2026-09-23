using System.Linq;
using Gamesim.Editor;
using Gamesim.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The assets the house's runtime branding is built from: a palette the runtime loads by name,
    /// naming exactly the pack textures in use, and a glow template whose shader variant a player
    /// build keeps.
    /// </summary>
    public sealed class HouseBrandingTests
    {
        [Test]
        public void ThePaletteLoadsByNameAndNamesThePackArt()
        {
            var palette = Resources.Load<HouseBrandingPalette>(HouseBrandingPalette.ResourceName);
            Assert.That(palette, Is.Not.Null, "Build it with Gamesim/Branding/Build the house branding palette.");
            Assert.That(AssetDatabase.GetAssetPath(palette.diaryHalo), Is.EqualTo(HouseBrandingAssets.DiaryHaloPath));
            Assert.That(AssetDatabase.GetAssetPath(palette.diaryNeon), Is.EqualTo(HouseBrandingAssets.DiaryNeonPath));
            Assert.That(UiPackCatalogue.TryGet(HouseBrandingAssets.DiaryHaloPath, out var halo) && halo.Kind == UiPackCatalogue.Kind.WorldColour, Is.True);
            Assert.That(UiPackCatalogue.TryGet(HouseBrandingAssets.DiaryNeonPath, out var neon) && neon.Kind == UiPackCatalogue.Kind.NeonMask, Is.True);
            Assert.That(AssetDatabase.GetAssetPath(palette.glowTemplate), Is.EqualTo(HouseBrandingAssets.GlowMaterialPath));
        }

        /// <summary>
        /// The pack-5 textures live outside Resources so a build ships only what something uses; the
        /// palette, inside Resources, is that something. It must name exactly the art in use, or the
        /// rest of the pack rides into every build through it.
        /// </summary>
        [Test]
        public void ThePaletteShipsOnlyTheArtItNames()
        {
            var textures = AssetDatabase.GetDependencies(HouseBrandingAssets.PalettePath, true)
                .Where(path => AssetDatabase.GetMainAssetTypeAtPath(path) == typeof(Texture2D))
                .OrderBy(path => path).ToArray();
            Assert.That(textures, Is.EqualTo(new[] { HouseBrandingAssets.DiaryHaloPath, HouseBrandingAssets.DiaryNeonPath }.OrderBy(path => path).ToArray()));
        }

        [Test]
        public void TheGlowTemplateIsTransparentAdditiveUnlit()
        {
            var glow = AssetDatabase.LoadAssetAtPath<Material>(HouseBrandingAssets.GlowMaterialPath);
            Assert.That(glow, Is.Not.Null);
            Assert.That(glow.shader.name, Is.EqualTo("Universal Render Pipeline/Unlit"));
            Assert.That(glow.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT"), Is.True,
                "The variant a player build keeps is the one a material asset asks for.");
            Assert.That(glow.renderQueue, Is.EqualTo((int)RenderQueue.Transparent));
            // URP recomputes the blend factors from these two when it imports a material, so they are
            // the settings that decide what it draws; a hand-edited factor does not survive import.
            Assert.That(glow.GetFloat("_Surface"), Is.EqualTo(1f), "Transparent.");
            Assert.That(glow.GetFloat("_Blend"), Is.EqualTo(2f), "Additive.");
            Assert.That(glow.GetFloat("_SrcBlend"), Is.EqualTo((float)BlendMode.SrcAlpha));
            Assert.That(glow.GetFloat("_DstBlend"), Is.EqualTo((float)BlendMode.One), "Additive: light added to the wall, not paint over it.");
            Assert.That(glow.GetFloat("_ZWrite"), Is.EqualTo(0f));
            Assert.That(glow.GetFloat("_Cull"), Is.EqualTo((float)CullMode.Off));
        }
    }
}
