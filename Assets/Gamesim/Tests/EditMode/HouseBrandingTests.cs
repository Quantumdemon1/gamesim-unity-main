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
            // Pack 6's diary surfaces: the two that tile import as tiles, the sign and the tally as colour.
            Assert.That(AssetDatabase.GetAssetPath(palette.diaryPaddedWall), Is.EqualTo(HouseBrandingAssets.DiaryPaddedWallPath));
            Assert.That(AssetDatabase.GetAssetPath(palette.diarySlats), Is.EqualTo(HouseBrandingAssets.DiarySlatsPath));
            Assert.That(AssetDatabase.GetAssetPath(palette.diaryOnCamera), Is.EqualTo(HouseBrandingAssets.DiaryOnCameraPath));
            Assert.That(AssetDatabase.GetAssetPath(palette.diaryTally), Is.EqualTo(HouseBrandingAssets.DiaryTallyPath));
            Assert.That(UiPackCatalogue.TryGet(HouseBrandingAssets.DiaryPaddedWallPath, out var padded) && padded.Kind == UiPackCatalogue.Kind.WorldTile, Is.True,
                "The padded wall repeats across the interview wall.");
            Assert.That(UiPackCatalogue.TryGet(HouseBrandingAssets.DiarySlatsPath, out var slats) && slats.Kind == UiPackCatalogue.Kind.WorldTile, Is.True);
            Assert.That(UiPackCatalogue.TryGet(HouseBrandingAssets.DiaryOnCameraPath, out var onCamera) && onCamera.Kind == UiPackCatalogue.Kind.WorldColour, Is.True);
            Assert.That(UiPackCatalogue.TryGet(HouseBrandingAssets.DiaryTallyPath, out var tally) && tally.Kind == UiPackCatalogue.Kind.WorldColour, Is.True);
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
            Assert.That(textures, Is.EqualTo(HouseBrandingAssets.TexturePaths.OrderBy(path => path).ToArray()),
                "Exactly the pack-5 diary halo and neon and the pack-6 diary surfaces ride into a build through the palette.");
        }

        /// <summary>
        /// The tufted interview wall stands where the box did and wears the pack's padding the way
        /// the box did: 4.6 by 2.9 m from the floor, no deeper than 0.10 m, its crowns no further
        /// forward than +0.05 (the branding's WallFace once it stands at the studio's -0.85), and
        /// its front's UVs within 0..1, so Pad() still lays 3.83 by 2.42 tiles across it rather
        /// than repeating a metre-per-unit projection 3.8 times a metre.
        /// </summary>
        [Test]
        public void TheDiaryWallKeepsItsEnvelope()
        {
            var palette = Resources.Load<HouseBrandingPalette>(HouseBrandingPalette.ResourceName);
            Assert.That(palette.diaryWallMesh, Is.Not.Null, "The palette carries the authored wall.");
            Assert.That(AssetDatabase.GetAssetPath(palette.diaryWallMesh), Is.EqualTo(HouseBrandingAssets.DiaryWallPath));
            var mesh = palette.diaryWallMesh;
            var bounds = mesh.bounds;
            Assert.That(bounds.size.x, Is.EqualTo(DiaryStudioBranding.WallWidth).Within(.01f), "as wide as the box was");
            Assert.That(bounds.size.y, Is.EqualTo(DiaryStudioBranding.WallHeight).Within(.01f), "as tall");
            Assert.That(bounds.min.y, Is.EqualTo(0f).Within(.001f), "standing on the floor");
            Assert.That(bounds.center.x, Is.EqualTo(0f).Within(.005f), "centred on its origin");
            Assert.That(bounds.size.z, Is.LessThanOrEqualTo(.10f + 1e-4f), "no deeper than the box");
            Assert.That(bounds.max.z, Is.LessThanOrEqualTo(.05f + 1e-4f), "its crowns at the branding's wall face, never in front of it");
            var vertices = mesh.vertices; var normals = mesh.normals; var uvs = mesh.uv;
            Assert.That(uvs.Length, Is.EqualTo(vertices.Length), "The wall carries UVs.");
            int front = 0;
            for (int i = 0; i < vertices.Length; i++)
            {
                if (normals[i].z < .3f) continue;
                front++;
                Assert.That(uvs[i].x >= -1e-4f && uvs[i].x <= 1f + 1e-4f && uvs[i].y >= -1e-4f && uvs[i].y <= 1f + 1e-4f, Is.True,
                    "A front vertex's UV " + uvs[i] + " lies outside 0..1.");
            }
            Assert.That(front, Is.GreaterThan(1000), "The front is the tufted face, facing the camera (+z).");
            Assert.That(mesh.triangles.Length / 3, Is.InRange(8000, 10500), "8 to 10 thousand triangles");
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
