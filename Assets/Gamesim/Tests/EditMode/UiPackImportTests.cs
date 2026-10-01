using System;
using System.IO;
using System.Linq;
using Gamesim.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The five UI/art asset packs, UI Refinement Kit 6, Room Finish Pack 6, Season Complete Pack 7,
    /// Campaign, Veto and Nomination Pack 8 and Ceremony and Finale Pack 9, as imported: every file the
    /// catalogue lists is on disk and every file on disk is listed; each imports as its kind says, and
    /// a 9-slice sprite carries its border - measured from its own pixels for packs 1-5, 7, 8 and 9,
    /// the kit manifest's own for Kit 6; the UI sprites load by path the way the code-built UI loads
    /// everything; and the import rules touch nothing outside the packs.
    ///
    /// <para>The expectations are written out here per kind rather than asked of
    /// <see cref="UiPackImporter"/>, so a wrong rule fails against what the kind is for instead of
    /// agreeing with itself.</para>
    /// </summary>
    public sealed class UiPackImportTests
    {
        private static readonly string[] Roots = { UiPackImporter.UiRoot, UiPackImporter.WorldRoot, UiPackImporter.GizmoRoot };
        private const string Kit6Root = UiPackImporter.UiRoot + "Kit6_Refinement/";
        private const string Pack6Root = UiPackImporter.WorldRoot + "Pack6_RoomFinish/";
        private const string Pack6Markers = UiPackImporter.GizmoRoot + "Pack6_RoomFinish/";
        private const string Pack9Root = UiPackImporter.UiRoot + "Pack9_CeremonyFinale/";

        /// <summary>
        /// Pack 9's frames: an edge over a fill of one size, named by a segment of the file name -
        /// card_edge_rest and panel_edge_focus carry their state after the word, action_tile_focus_edge
        /// before it - so the test is the segment, never the suffix (bb_ui_pack9.kind_for).
        /// </summary>
        private static bool Pack9Frame(string path)
        {
            var segments = Path.GetFileNameWithoutExtension(path).Split('_');
            return segments.Contains("edge") || segments.Contains("fill");
        }

        [Test]
        public void TheCatalogueAndTheFoldersHoldTheSameFiles()
        {
            var onDisk = Roots.Where(Directory.Exists)
                .SelectMany(root => Directory.GetFiles(root, "*", SearchOption.AllDirectories))
                .Where(file => !file.EndsWith(".meta", StringComparison.Ordinal))
                .Select(file => file.Replace('\\', '/'))
                .OrderBy(file => file, StringComparer.Ordinal).ToArray();
            var listed = UiPackCatalogue.All.Select(entry => entry.Path).OrderBy(path => path, StringComparer.Ordinal).ToArray();

            Assert.That(listed.Length, Is.EqualTo(397 + 71 + 174 + 66 + 102 + 62),
                "Five packs, 397 images; Kit 6's 71 sprites; Room Finish Pack 6's 174 textures (162 world, 12 editor markers); "
                + "Season Complete Pack 7's 66 sprites; Pack 8's 102, its seven Icons/ twins among them; "
                + "Ceremony and Finale Pack 9's 62, its twelve Icons/ among them; "
                + "the previews, READMEs, manifests and SVG sources live in ArtSource/ui-packs.");
            Assert.That(onDisk.Except(listed), Is.Empty, "On disk and not in UiPackCatalogue - re-run the ArtSource/ui-packs/tools script for that pack.");
            Assert.That(listed.Except(onDisk), Is.Empty, "In UiPackCatalogue and missing from disk.");
            Assert.That(onDisk.All(file => file.EndsWith(".png", StringComparison.Ordinal)), Is.True,
                "Only images belong under the pack roots; reference material stays out of the build.");
        }

        [Test]
        public void EveryPackTextureImportsAsItsKind()
        {
            foreach (var entry in UiPackCatalogue.All)
            {
                var importer = AssetImporter.GetAtPath(entry.Path) as TextureImporter;
                Assert.That(importer, Is.Not.Null, entry.Path + " has not been imported as a texture.");
                string at = entry.Path + " (" + entry.Kind + ")";
                bool ui = entry.Kind == UiPackCatalogue.Kind.UiSliced || entry.Kind == UiPackCatalogue.Kind.UiSprite;
                bool data = entry.Kind == UiPackCatalogue.Kind.NeonMask || entry.Kind == UiPackCatalogue.Kind.DetailMask;

                Assert.That(importer.textureType, Is.EqualTo(ui ? TextureImporterType.Sprite : TextureImporterType.Default), at);
                Assert.That(importer.textureShape, Is.EqualTo(TextureImporterShape.Texture2D), at);
                Assert.That(importer.sRGBTexture, Is.EqualTo(!data), at + ": a mask is data, everything else is colour.");
                Assert.That(importer.alphaIsTransparency, Is.True, at);
                Assert.That(importer.mipmapEnabled, Is.EqualTo(!ui),
                    at + ": a canvas draws a UI sprite near its authored size; the overhead camera sees a world texture small and at an angle.");
                bool tiles = entry.Kind == UiPackCatalogue.Kind.DetailMask || entry.Kind == UiPackCatalogue.Kind.WorldTile;
                Assert.That(importer.wrapMode, Is.EqualTo(tiles ? TextureWrapMode.Repeat : TextureWrapMode.Clamp),
                    at + ": only a detail mask and a world tile repeat across a surface.");
                Assert.That(importer.isReadable, Is.False, at);
                Assert.That(importer.maxTextureSize, Is.EqualTo(2048), at);
                bool small = Mathf.Max(entry.Width, entry.Height) <= 1024;
                Assert.That(importer.textureCompression,
                    Is.EqualTo(ui && small ? TextureImporterCompression.Uncompressed : TextureImporterCompression.CompressedHQ),
                    at + ": uncompressed where a UI edge would show blocks, compressed where a full-screen card would cost 8 MB.");
                if (!ui) continue;
                Assert.That(importer.spriteImportMode, Is.EqualTo(SpriteImportMode.Single), at);
                Assert.That(importer.spritePixelsPerUnit, Is.EqualTo(100f), at);
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                Assert.That(settings.spriteMeshType, Is.EqualTo(SpriteMeshType.FullRect), at + ": a sliced or tiled Image needs the full rect.");
            }
        }

        [Test]
        public void EveryNineSliceSpriteCarriesTheBorderMeasuredFromItsPixels()
        {
            int sliced = 0;
            foreach (var entry in UiPackCatalogue.All)
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(entry.Path);
                if (entry.Kind != UiPackCatalogue.Kind.UiSliced)
                {
                    if (importer.textureType == TextureImporterType.Sprite)
                        Assert.That(importer.spriteBorder, Is.EqualTo(Vector4.zero), entry.Path + " is drawn whole and has no border.");
                    continue;
                }
                sliced++;
                // Packs 1-5, 7 and 8 name their 9-slices; Kit 6 says which of its sprites are sliced in
                // its manifest; Pack 9 slices every edge and fill of a frame and nothing else.
                if (entry.Path.StartsWith(Pack9Root, StringComparison.Ordinal))
                    Assert.That(Pack9Frame(entry.Path), Is.True, entry.Path + ": only an edge or a fill of Pack 9 is sliced.");
                else if (!entry.Path.StartsWith(Kit6Root, StringComparison.Ordinal))
                    Assert.That(entry.Path.EndsWith("_9slice.png", StringComparison.Ordinal), Is.True, entry.Path);
                var b = entry.Border;
                Assert.That(b.x > 0 && b.y > 0 && b.z > 0 && b.w > 0, Is.True, entry.Path + ": every side of a 9-slice has a fixed corner.");
                Assert.That(b.x + b.z, Is.LessThan(entry.Width), entry.Path + ": something is left to stretch across.");
                Assert.That(b.y + b.w, Is.LessThan(entry.Height), entry.Path + ": something is left to stretch down.");
                Assert.That(importer.spriteBorder, Is.EqualTo(b), entry.Path + ": the imported border is the measured one.");
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(entry.Path);
                Assert.That(sprite, Is.Not.Null, entry.Path);
                Assert.That(sprite.border, Is.EqualTo(b), entry.Path + ": and the sprite a Sliced Image draws carries it.");
            }
            Assert.That(sliced, Is.EqualTo(186 + 15 + 43 + 75 + 27),
                "Every file named _9slice across the five packs, Kit 6's fifteen sliced sprites, Pack 7's forty-three, "
                + "Pack 8's seventy-five and Pack 9's twenty-seven edges and fills.");
        }

        /// <summary>
        /// Pack 9 names no _9slice: a frame is an edge over a fill of one size, and the README's borders
        /// (panel 24, card 18, pill about 30) are nominal - the catalogue carries the ones
        /// <c>bb_ui_packs.measure</c> read from the pixels (panels 17-20, cards 14-19, pills 26-27), which
        /// is what the importer applies and the sweep above holds the sprites to. So the pack's rule is
        /// written out here: every edge and fill is sliced, nothing else is, and the counts are the pack's.
        /// </summary>
        [Test]
        public void Pack9SlicesEveryEdgeAndFillAndDrawsTheRestWhole()
        {
            var pack9 = UiPackCatalogue.All.Where(entry => entry.Path.StartsWith(Pack9Root, StringComparison.Ordinal)).ToArray();
            Assert.That(pack9, Has.Length.EqualTo(62), "The Ceremony and Finale pack's 62 images.");
            foreach (var (category, count) in new[] { ("Shared", 11), ("LiveEviction", 10), ("NominationCeremony", 9), ("SeasonFinale", 20), ("Icons", 12) })
                Assert.That(pack9.Count(entry => entry.Path.StartsWith(Pack9Root + category + "/", StringComparison.Ordinal)), Is.EqualTo(count),
                    "Pack 9's " + category + "/ (Icons_PNG installs as Icons; Icons_SVG is reference only, under ArtSource).");
            foreach (var entry in pack9)
            {
                bool frame = Pack9Frame(entry.Path);
                Assert.That(entry.Kind, Is.EqualTo(frame ? UiPackCatalogue.Kind.UiSliced : UiPackCatalogue.Kind.UiSprite),
                    entry.Path + (frame ? " is an edge or a fill and slices." : " is drawn whole."));
                // Nothing in the pack glows out past its rect: an edge's body sits 2 px in, a fill's 1, and
                // the halos are sprites of their own - so a frame drawn to a rect fills it (EndScreenKit's
                // 8 and 38 px insets are Pack 7's and Pack 8's, not these).
                var inset = entry.BodyInset;
                Assert.That(Mathf.Max(inset.x, inset.y, inset.z, inset.w), Is.LessThanOrEqualTo(frame ? 2f : 0f), entry.Path + "'s body inset.");
                Assert.That(Mathf.Max(entry.Width, entry.Height), Is.LessThanOrEqualTo(256), entry.Path + ": every sprite of the pack is small and imports uncompressed.");
            }
            Assert.That(pack9.Count(entry => entry.Kind == UiPackCatalogue.Kind.UiSliced), Is.EqualTo(27),
                "The pack's 27 edges and fills: Shared 8, LiveEviction 4, NominationCeremony 4, SeasonFinale 11.");
        }

        /// <summary>
        /// The UI is built in code, so a sprite it draws is loaded by path. The world textures are
        /// not in Resources at all: a scene references them, and the build ships only what it uses.
        /// Pack 6's interaction markers are editor aids and live in an Editor folder, which no build
        /// includes whatever references them.
        /// </summary>
        [Test]
        public void UiSpritesLoadByPathAndWorldTexturesStayOutOfTheBuild()
        {
            const string resources = "Assets/Gamesim/Resources/";
            int markers = 0;
            foreach (var entry in UiPackCatalogue.All)
            {
                bool ui = entry.Kind == UiPackCatalogue.Kind.UiSliced || entry.Kind == UiPackCatalogue.Kind.UiSprite;
                bool marker = entry.Path.StartsWith(Pack6Markers, StringComparison.Ordinal);
                string home = ui ? UiPackImporter.UiRoot : marker ? UiPackImporter.GizmoRoot : UiPackImporter.WorldRoot;
                Assert.That(entry.Path.StartsWith(home, StringComparison.Ordinal), Is.True,
                    entry.Path + " is in the wrong home for a " + entry.Kind + ".");
                if (ui)
                {
                    string key = entry.Path.Substring(resources.Length, entry.Path.Length - resources.Length - ".png".Length);
                    Assert.That(Resources.Load<Sprite>(key), Is.Not.Null, "Resources.Load<Sprite>(\"" + key + "\")");
                }
                else
                {
                    Assert.That(entry.Path.StartsWith(resources, StringComparison.Ordinal), Is.False, entry.Path);
                    Assert.That(AssetDatabase.LoadAssetAtPath<Texture2D>(entry.Path), Is.Not.Null, entry.Path);
                }
                if (!marker) continue;
                markers++;
                Assert.That(entry.Path.Contains("/Editor/"), Is.True, entry.Path + ": an editor marker ships in no build.");
                Assert.That(Path.GetFileName(entry.Path).StartsWith("marker_", StringComparison.Ordinal), Is.True,
                    entry.Path + ": only the pack's markers belong under the gizmo root.");
            }
            Assert.That(markers, Is.EqualTo(12), "Pack 6's twelve interaction markers.");
            Assert.That(UiPackCatalogue.All.Count(entry => entry.Path.StartsWith(Pack6Root, StringComparison.Ordinal)), Is.EqualTo(162),
                "Pack 6's 162 world textures, outside Resources.");
        }

        [Test]
        public void TheImportRulesClaimNothingOutsideThePacks()
        {
            var elsewhere = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Gamesim" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => !Roots.Any(root => path.StartsWith(root, StringComparison.Ordinal)))
                .ToArray();
            Assert.That(elsewhere.Length, Is.GreaterThan(0), "Precondition: the project has textures of its own.");
            Assert.That(elsewhere.Where(UiPackImporter.IsPack), Is.Empty,
                "The pack rules must never reach the icons, floors, portraits or anything else the project already imports its own way.");
            Assert.That(UiPackImporter.IsPack("Assets/Gamesim/Resources/Packs/Pack1_Foundation/Panels/panel_resting_9slice.png"), Is.True);
            Assert.That(UiPackImporter.IsPack("Assets/Gamesim/Art/Packs/Pack5_HouseBroadcast/NeonMasks/neon_gamesim_mask.png"), Is.True);
            Assert.That(UiPackImporter.IsPack(Pack6Root + "WallTreatments/wall_warm_oak_slats.png"), Is.True);
            Assert.That(UiPackImporter.IsPack(Pack6Markers + "marker_seated_conversation.png"), Is.True);
            Assert.That(UiPackImporter.IsPack(Pack9Root + "Shared/panel_fill.png"), Is.True, "Pack 9 sits under the UI root, so the UI rule covers it.");
            // Pack 9 slices its edges and fills by the catalogue, not by a _9slice name.
            Assert.That(UiPackImporter.KindOf(Pack9Root + "Shared/panel_fill.png"), Is.EqualTo(UiPackCatalogue.Kind.UiSliced));
            Assert.That(UiPackImporter.KindOf(Pack9Root + "Shared/card_edge_rest.png"), Is.EqualTo(UiPackCatalogue.Kind.UiSliced));
            Assert.That(UiPackImporter.KindOf(Pack9Root + "Shared/portrait_ring.png"), Is.EqualTo(UiPackCatalogue.Kind.UiSprite));
            // The catalogue says which pack-6 files tile; the folder rule alone covers the two folders that all tile.
            Assert.That(UiPackImporter.KindOf(Pack6Root + "WallTreatments/wall_warm_oak_slats.png"), Is.EqualTo(UiPackCatalogue.Kind.WorldTile));
            Assert.That(UiPackImporter.KindOf(Pack6Root + "Bedding/duvet_navy_diamond.png"), Is.EqualTo(UiPackCatalogue.Kind.WorldTile));
            Assert.That(UiPackImporter.KindOf(Pack6Root + "Rugs/rug_diary_violet.png"), Is.EqualTo(UiPackCatalogue.Kind.WorldColour));
            Assert.That(UiPackImporter.KindOf(Pack6Root + "DiaryRoom/diary_eye_halo_mask.png"), Is.EqualTo(UiPackCatalogue.Kind.NeonMask));
        }
    }
}
