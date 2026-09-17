using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Gamesim.Editor
{
    /// <summary>
    /// Draws the HUD's icon set and writes it out as sprites.
    ///
    /// <para>The shipped font is LiberationSans SDF, which has no dingbats — a crown or trophy
    /// character renders as tofu. Every mark in this project has therefore been assembled at runtime
    /// from discs and bars, which works but caps how close the set can get to the reference build's
    /// glyphs. This generates real artwork instead: rasterised here, saved as PNG, imported as
    /// sprites, and tinted at use so one white icon serves every colour it appears in.</para>
    ///
    /// <para>Everything is drawn procedurally rather than hand-painted, so the set is reproducible:
    /// re-running the pass regenerates byte-identical files, and a shape can be corrected in code
    /// rather than in an image editor nobody has.</para>
    ///
    /// <para>Supersampled four times and box-filtered down, because these are viewed at 20–60 px and
    /// aliased diagonals at that size read as damage rather than as style.</para>
    /// </summary>
    public static class IconForge
    {
        public const string Folder = "Assets/Gamesim/Resources/GamesimIcons";
        private const int Size = 128;
        private const int Super = 4;

        [MenuItem("Gamesim/U07/Generate HUD icons")]
        public static void Generate()
        {
            Directory.CreateDirectory(Folder);

            var written = new List<string>();
            written.Add(Write("target", Target));
            written.Add(Write("crown", Crown));
            written.Add(Write("veto-token", VetoToken));
            written.Add(Write("trophy", Trophy));
            written.Add(Write("gavel", Gavel));
            written.Add(Write("evicted", Evicted));
            written.Add(Write("key", Key));

            AssetDatabase.Refresh();
            foreach (var path in written) Configure(path);
            AssetDatabase.SaveAssets();
            Debug.Log("[Gamesim] icons · generated " + written.Count + " sprites in " + Folder);
        }

        /// <summary>
        /// Lays every icon out on a dark ground and writes one sheet.
        ///
        /// <para>The icons are white on transparent, so opening one shows an empty square in any
        /// viewer that paints transparency white. This exists so the artwork can actually be looked
        /// at — it is a review aid and is written outside Assets so it never ships.</para>
        /// </summary>
        [MenuItem("Gamesim/U07/Preview HUD icons")]
        public static void Preview()
        {
            var names = new[] { "target", "crown", "veto-token", "trophy", "gavel", "evicted", "key" };
            const int cell = 160, pad = 16;
            int columns = names.Length, width = columns * cell, height = cell;

            var sheet = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var ground = new Color32(18, 26, 36, 255);
            var pixels = new Color32[width * height];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = ground;

            for (int i = 0; i < names.Length; i++)
            {
                // Decoded from the file rather than from the imported asset: an imported sprite is
                // not CPU-readable unless its importer says so, and turning that on for shipping
                // art to satisfy a preview would be the wrong trade.
                string file = Path.GetFullPath(Folder + "/" + names[i] + ".png");
                if (!File.Exists(file)) continue;
                var sprite = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!sprite.LoadImage(File.ReadAllBytes(file))) { UnityEngine.Object.DestroyImmediate(sprite); continue; }
                var source = sprite.GetPixels32();
                int inner = cell - pad * 2;
                for (int y = 0; y < inner; y++)
                for (int x = 0; x < inner; x++)
                {
                    int sx = Mathf.Clamp(x * sprite.width / inner, 0, sprite.width - 1);
                    int sy = Mathf.Clamp(y * sprite.height / inner, 0, sprite.height - 1);
                    var texel = source[sy * sprite.width + sx];
                    float alpha = texel.a / 255f;
                    int dx = i * cell + pad + x, dy = pad + y;
                    var under = pixels[dy * width + dx];
                    pixels[dy * width + dx] = new Color32(
                        (byte)Mathf.RoundToInt(under.r * (1 - alpha) + texel.r * alpha),
                        (byte)Mathf.RoundToInt(under.g * (1 - alpha) + texel.g * alpha),
                        (byte)Mathf.RoundToInt(under.b * (1 - alpha) + texel.b * alpha), 255);
                }
                UnityEngine.Object.DestroyImmediate(sprite);
            }
            sheet.SetPixels32(pixels);
            sheet.Apply();
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "icon-preview.png"));
            File.WriteAllBytes(path, sheet.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(sheet);
            Debug.Log("[Gamesim] icons · preview sheet -> " + path);
        }

        // ------------------------------------------------------------------ shapes

        /// <summary>Nominee: concentric rings, the mark the block carries in the reference build.</summary>
        private static void Target(Canvas c)
        {
            c.Ring(.5f, .5f, .46f, .10f);
            c.Ring(.5f, .5f, .28f, .09f);
            c.Disc(.5f, .5f, .11f);
        }

        /// <summary>Head of Household: a five-point crown on a band.</summary>
        private static void Crown(Canvas c)
        {
            c.Polygon(new[]
            {
                new Vector2(.10f, .34f), new Vector2(.90f, .34f), new Vector2(.90f, .62f),
                new Vector2(.74f, .44f), new Vector2(.62f, .70f), new Vector2(.50f, .42f),
                new Vector2(.38f, .70f), new Vector2(.26f, .44f), new Vector2(.10f, .62f),
            });
            c.Rect(.10f, .22f, .80f, .10f);
        }

        /// <summary>Veto holder: a struck token, the shape the reference build uses for the power.</summary>
        private static void VetoToken(Canvas c)
        {
            c.Ring(.5f, .5f, .44f, .11f);
            c.Bar(.30f, .30f, .70f, .70f, .11f);
        }

        /// <summary>Competition win: a cup with handles, a stem and a base.</summary>
        private static void Trophy(Canvas c)
        {
            // A deep tapered bowl. The first attempt was a shallow trapezoid and read as a table:
            // a cup needs most of its height in the bowl and a visible narrowing toward the stem.
            c.Polygon(new[]
            {
                new Vector2(.28f, .90f), new Vector2(.72f, .90f), new Vector2(.70f, .70f),
                new Vector2(.62f, .54f), new Vector2(.38f, .54f), new Vector2(.30f, .70f),
            });
            c.Ring(.20f, .78f, .14f, .06f);
            c.Ring(.80f, .78f, .14f, .06f);
            c.Rect(.45f, .34f, .10f, .22f);
            c.Rect(.36f, .26f, .28f, .08f);
            c.Rect(.28f, .16f, .44f, .10f);
        }

        /// <summary>Veto meeting: a gavel, head and handle.</summary>
        private static void Gavel(Canvas c)
        {
            // Head across, handle away from it. The first attempt put both on nearly the same
            // diagonal, so the two merged into one blob with no readable silhouette.
            c.Bar(.20f, .74f, .54f, .74f, .26f);
            c.Bar(.50f, .70f, .84f, .38f, .10f);
            c.Rect(.14f, .14f, .58f, .10f);
        }

        /// <summary>Eviction: a figure with a cross beside it.</summary>
        private static void Evicted(Canvas c)
        {
            c.Disc(.36f, .74f, .17f);
            c.Polygon(new[]
            {
                new Vector2(.10f, .16f), new Vector2(.62f, .16f),
                new Vector2(.56f, .46f), new Vector2(.16f, .46f),
            });
            c.Bar(.66f, .70f, .92f, .44f, .10f);
            c.Bar(.92f, .70f, .66f, .44f, .10f);
        }

        /// <summary>Nomination keys: the bow and bit of a key.</summary>
        private static void Key(Canvas c)
        {
            c.Ring(.30f, .50f, .24f, .10f);
            c.Rect(.50f, .45f, .42f, .10f);
            c.Rect(.74f, .30f, .08f, .16f);
            c.Rect(.88f, .30f, .08f, .16f);
        }

        // ------------------------------------------------------------------ raster

        /// <summary>A supersampled coverage buffer in unit space, resolved to white-on-transparent.</summary>
        private sealed class Canvas
        {
            private readonly float[] coverage = new float[Size * Super * Size * Super];
            private int Side => Size * Super;

            private void Plot(int x, int y) { if (x >= 0 && y >= 0 && x < Side && y < Side) coverage[y * Side + x] = 1f; }

            public void Disc(float cx, float cy, float r)
            {
                int s = Side; float px = cx * s, py = cy * s, pr = r * s;
                for (int y = Mathf.FloorToInt(py - pr); y <= Mathf.CeilToInt(py + pr); y++)
                for (int x = Mathf.FloorToInt(px - pr); x <= Mathf.CeilToInt(px + pr); x++)
                {
                    float dx = x + .5f - px, dy = y + .5f - py;
                    if (dx * dx + dy * dy <= pr * pr) Plot(x, y);
                }
            }

            public void Ring(float cx, float cy, float r, float thickness)
            {
                int s = Side; float px = cx * s, py = cy * s, outer = r * s, inner = Mathf.Max(0f, (r - thickness) * s);
                for (int y = Mathf.FloorToInt(py - outer); y <= Mathf.CeilToInt(py + outer); y++)
                for (int x = Mathf.FloorToInt(px - outer); x <= Mathf.CeilToInt(px + outer); x++)
                {
                    float dx = x + .5f - px, dy = y + .5f - py, d2 = dx * dx + dy * dy;
                    if (d2 <= outer * outer && d2 >= inner * inner) Plot(x, y);
                }
            }

            public void Rect(float x0, float y0, float w, float h)
            {
                int s = Side;
                for (int y = Mathf.FloorToInt(y0 * s); y < Mathf.CeilToInt((y0 + h) * s); y++)
                for (int x = Mathf.FloorToInt(x0 * s); x < Mathf.CeilToInt((x0 + w) * s); x++)
                    Plot(x, y);
            }

            /// <summary>A thick line between two points, with rounded ends.</summary>
            public void Bar(float x0, float y0, float x1, float y1, float thickness)
            {
                int s = Side;
                var a = new Vector2(x0 * s, y0 * s);
                var b = new Vector2(x1 * s, y1 * s);
                float half = thickness * s * .5f;
                var min = Vector2.Min(a, b) - Vector2.one * half;
                var max = Vector2.Max(a, b) + Vector2.one * half;
                var ab = b - a;
                float lengthSquared = Mathf.Max(ab.sqrMagnitude, 1e-4f);
                for (int y = Mathf.FloorToInt(min.y); y <= Mathf.CeilToInt(max.y); y++)
                for (int x = Mathf.FloorToInt(min.x); x <= Mathf.CeilToInt(max.x); x++)
                {
                    var p = new Vector2(x + .5f, y + .5f);
                    float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / lengthSquared);
                    if ((p - (a + ab * t)).sqrMagnitude <= half * half) Plot(x, y);
                }
            }

            /// <summary>Even-odd polygon fill in unit space.</summary>
            public void Polygon(Vector2[] points)
            {
                int s = Side;
                float minY = float.MaxValue, maxY = float.MinValue;
                foreach (var point in points) { minY = Mathf.Min(minY, point.y * s); maxY = Mathf.Max(maxY, point.y * s); }

                var crossings = new List<float>();
                for (int y = Mathf.FloorToInt(minY); y <= Mathf.CeilToInt(maxY); y++)
                {
                    float scan = y + .5f;
                    crossings.Clear();
                    for (int i = 0; i < points.Length; i++)
                    {
                        var a = points[i] * s;
                        var b = points[(i + 1) % points.Length] * s;
                        if (a.y == b.y) continue;
                        if (scan < Mathf.Min(a.y, b.y) || scan >= Mathf.Max(a.y, b.y)) continue;
                        crossings.Add(a.x + (scan - a.y) / (b.y - a.y) * (b.x - a.x));
                    }
                    crossings.Sort();
                    for (int i = 0; i + 1 < crossings.Count; i += 2)
                        for (int x = Mathf.FloorToInt(crossings[i]); x <= Mathf.CeilToInt(crossings[i + 1]); x++)
                            Plot(x, y);
                }
            }

            /// <summary>Box-filters the supersampled buffer down to the final white-on-alpha texture.</summary>
            public Texture2D Resolve()
            {
                var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
                var pixels = new Color32[Size * Size];
                float samples = Super * Super;
                for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    float sum = 0f;
                    for (int sy = 0; sy < Super; sy++)
                    for (int sx = 0; sx < Super; sx++)
                        sum += coverage[(y * Super + sy) * Side + x * Super + sx];
                    byte alpha = (byte)Mathf.RoundToInt(Mathf.Clamp01(sum / samples) * 255f);
                    // White, so a single icon can be tinted to whatever the surface needs.
                    pixels[y * Size + x] = new Color32(255, 255, 255, alpha);
                }
                texture.SetPixels32(pixels);
                texture.Apply();
                return texture;
            }
        }

        private static string Write(string name, Action<Canvas> draw)
        {
            var canvas = new Canvas();
            draw(canvas);
            var texture = canvas.Resolve();
            string path = Folder + "/" + name + ".png";
            File.WriteAllBytes(Path.GetFullPath(path), texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            return path;
        }

        /// <summary>
        /// Imports each icon as an uncompressed sprite. Uncompressed on purpose: these are tiny,
        /// they carry long clean diagonals, and block compression puts visible mud on exactly that.
        /// </summary>
        private static void Configure(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }
    }
}
