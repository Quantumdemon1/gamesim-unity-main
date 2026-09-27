using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Gamesim.Editor
{
    /// <summary>
    /// Fixed import settings for the five UI/art asset packs, so nobody sets 397 textures by hand and
    /// a re-import cannot drift them. <see cref="UiPackCatalogue"/> says what each file is; this says
    /// what that means to the importer.
    ///
    /// <para>Two homes. A UI sprite lives under <see cref="UiRoot"/>, because the UI is built in code
    /// and loads what it draws by path. A pack-5 world texture lives under <see cref="WorldRoot"/>,
    /// outside Resources: the house is dressed by editor scripts that put texture references into the
    /// saved scene, so it ships only what the house actually uses.</para>
    ///
    /// <para>The rules, by kind:</para>
    /// <list type="bullet">
    /// <item>UI sprites - Sprite, one per file, full-rect mesh, no mipmaps (a canvas draws them near
    /// their authored size), clamped, sRGB. A 9-slice sprite carries the border measured from its own
    /// pixels. Uncompressed up to 1024 on a side, where a UI edge would show block compression;
    /// high-quality compression above that, where a full-screen card would otherwise cost 8 MB.</item>
    /// <item>World colour and particles - Default textures with mipmaps, because the overhead camera
    /// sees them small and at an angle; clamped, sRGB, high-quality compression.</item>
    /// <item>Neon and detail masks - data, not colour, so linear rather than sRGB; a neon mask is
    /// clamped to its sign and a detail mask repeats across a surface.</item>
    /// </list>
    /// </summary>
    public sealed class UiPackImporter : AssetPostprocessor
    {
        public const string UiRoot = "Assets/Gamesim/Resources/Packs/";
        public const string WorldRoot = "Assets/Gamesim/Art/Packs/";
        /// <summary>The largest side imported without compression.</summary>
        public const int UncompressedLimit = 1024;

        public override uint GetVersion() => 1;

        public static bool IsPack(string path) => path != null
            && path.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
            && (path.StartsWith(UiRoot, StringComparison.Ordinal) || path.StartsWith(WorldRoot, StringComparison.Ordinal));

        private void OnPreprocessTexture()
        {
            if (!IsPack(assetPath)) return;
            Apply((TextureImporter)assetImporter, assetPath);
        }

        /// <summary>The kind a pack file imports as: the catalogue's word, or the same rule for a file it does not list yet.</summary>
        public static UiPackCatalogue.Kind KindOf(string path)
        {
            if (UiPackCatalogue.TryGet(path, out var entry)) return entry.Kind;
            string name = Path.GetFileName(path);
            string category = Path.GetFileName(Path.GetDirectoryName(path) ?? string.Empty);
            if (path.StartsWith(UiRoot, StringComparison.Ordinal))
                return name.EndsWith("_9slice.png", StringComparison.Ordinal) ? UiPackCatalogue.Kind.UiSliced : UiPackCatalogue.Kind.UiSprite;
            switch (category)
            {
                case "NeonMasks": return name.EndsWith("_mask.png", StringComparison.Ordinal) ? UiPackCatalogue.Kind.NeonMask : UiPackCatalogue.Kind.WorldColour;
                case "SurfaceDecals": return UiPackCatalogue.Kind.DetailMask;
                case "VFXTextures": return UiPackCatalogue.Kind.Particle;
                default: return UiPackCatalogue.Kind.WorldColour;
            }
        }

        public static void Apply(TextureImporter importer, string path)
        {
            var kind = KindOf(path);
            bool ui = kind == UiPackCatalogue.Kind.UiSliced || kind == UiPackCatalogue.Kind.UiSprite;
            bool data = kind == UiPackCatalogue.Kind.NeonMask || kind == UiPackCatalogue.Kind.DetailMask;

            importer.textureType = ui ? TextureImporterType.Sprite : TextureImporterType.Default;
            importer.textureShape = TextureImporterShape.Texture2D;
            importer.sRGBTexture = !data;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = !ui;
            importer.wrapMode = kind == UiPackCatalogue.Kind.DetailMask ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.isReadable = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 2048;
            importer.textureCompression = ui && LargestSide(path) <= UncompressedLimit
                ? TextureImporterCompression.Uncompressed
                : TextureImporterCompression.CompressedHQ;

            if (!ui) return;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.spriteBorder = kind == UiPackCatalogue.Kind.UiSliced && UiPackCatalogue.TryGet(path, out var entry)
                ? entry.Border
                : Vector4.zero;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteGenerateFallbackPhysicsShape = false;
            importer.SetTextureSettings(settings);
        }

        /// <summary>The catalogue's size, or the PNG header's for a file it does not list yet.</summary>
        private static int LargestSide(string path)
        {
            if (UiPackCatalogue.TryGet(path, out var entry)) return Mathf.Max(entry.Width, entry.Height);
            try
            {
                var header = new byte[24];
                using (var stream = File.OpenRead(path))
                    if (stream.Read(header, 0, 24) < 24) return int.MaxValue;
                int Read(int at) => (header[at] << 24) | (header[at + 1] << 16) | (header[at + 2] << 8) | header[at + 3];
                return Mathf.Max(Read(16), Read(20));
            }
            catch (IOException) { return int.MaxValue; }
        }
    }
}
