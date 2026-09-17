using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Gamesim.Presentation
{
    /// <summary>
    /// Single source of truth for HUD colour and shape. The palette is sampled from the house set
    /// itself — the neon room outlines, the gold competition rings and the camera's backdrop — so
    /// the screen-space UI and the 3D broadcast read as one design rather than two.
    ///
    /// Panel corners come from procedurally generated 9-sliced sprites, cached per radius, so no
    /// sprite assets ship and any radius is available without an import step.
    /// </summary>
    public static class UiTheme
    {
        // Ground tones. Ink sits fractionally below the camera backdrop (#121A24) so panels read
        // as sitting on top of the set instead of dissolving into the bloom.
        public static readonly Color Ink = Hex("0D141Cfa");
        public static readonly Color Surface = Hex("17222Efa");
        public static readonly Color SurfaceRaised = Hex("1E2C3Bfa");
        public static readonly Color Outline = Hex("3A5068b0");

        // Accents, taken from the set's emissive materials.
        public static readonly Color Accent = Hex("99D9FF");   // pale neon blue, the fixture glow
        public static readonly Color AccentDeep = Hex("1A73FF"); // room-outline blue
        public static readonly Color Gold = Hex("FFC726");     // competition rings
        public static readonly Color Warning = Hex("FF8A5C");   // recovery / caution copy
        public static readonly Color Danger = Hex("FF6B6B");    // softened from the set's FF1A1A
        public static readonly Color Positive = Hex("5BE49B");  // the yard's green, for allied relationships
        // A saturated green for a filled ceremony header, where Positive is a tint meant for text on
        // a dark ground and washes out as a background. This is the web build's veto-meeting banner.
        public static readonly Color PositiveDeep = Hex("16A34A");
        // Reserved for one thing only: the competition result banner. Gold already means the veto
        // here, so a gold banner over an HoH win read as the wrong power at a glance.
        public static readonly Color Award = Hex("7C3AED");
        public static readonly Color Paper = Hex("F2F5FA");
        public static readonly Color Muted = Hex("94A7B8");

        /// <summary>
        /// Ink or Paper, whichever reads against <paramref name="background"/>.
        ///
        /// <para>Phase-coloured bands run from a deep blue to gold, and a single hard-coded
        /// foreground is wrong at one end or the other. This picks by WCAG relative luminance, the
        /// same measure <c>UiThemeContrastTests</c> holds the rest of the palette to, so a new band
        /// colour cannot quietly ship unreadable copy.</para>
        /// </summary>
        public static Color OnColor(Color background)
        {
            static float Channel(float c) => c <= 0.03928f ? c / 12.92f : Mathf.Pow((c + 0.055f) / 1.055f, 2.4f);
            float luminance = 0.2126f * Channel(background.r)
                + 0.7152f * Channel(background.g)
                + 0.0722f * Channel(background.b);
            // Contrast against Ink (near-black) beats contrast against Paper above this crossover.
            return luminance > 0.18f ? Ink : Paper;
        }

        public const int PanelRadius = 10;
        public const int ControlRadius = 7;
        public const int BorderThickness = 2;

        private static readonly Dictionary<int, Sprite> FillCache = new Dictionary<int, Sprite>();
        private static readonly Dictionary<int, Sprite> OutlineCache = new Dictionary<int, Sprite>();

        public static Color Hex(string value)
        {
            ColorUtility.TryParseHtmlString("#" + value, out var color);
            return color;
        }

        /// <summary>Applies the themed rounded background to an existing Image.</summary>
        public static void Style(Image image, Color color, int radius)
        {
            if (image == null) return;
            image.sprite = Fill(radius);
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 1f;
            image.color = color;
        }

        /// <summary>
        /// Adds a hairline border as a non-interactive child so the panel edge stays visible
        /// against a bloom-heavy backdrop. Kept off buttons to avoid doubling the hierarchy.
        /// </summary>
        public static void AddBorder(RectTransform panel, int radius, Color color)
        {
            if (panel == null) return;
            var border = new GameObject("Border", typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)border.transform;
            rect.SetParent(panel, false);
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
            var image = border.GetComponent<Image>();
            image.sprite = OutlineSprite(radius);
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 1f;
            image.color = color;
            image.raycastTarget = false;
        }

        private static Sprite circle;

        /// <summary>
        /// A plain filled disc, drawn once and shared.
        ///
        /// <para>The rounded-rect sprites above are 9-sliced, which makes them the wrong tool for a
        /// circle: a radius large enough to round a 56px box gives slice borders wider than the box
        /// itself, and the centre collapses. This is full-rect, so it scales to any size and stays
        /// round — used for portrait frames and status pips, where the broadcast look wants circles
        /// rather than rounded squares.</para>
        /// </summary>
        /// <summary>
        /// A generated HUD icon by name, or null when the set has not been generated.
        ///
        /// <para>Loaded from <c>Resources/GamesimIcons</c> and cached. Every caller must cope with
        /// null and fall back to the shape it drew before: the icons are produced by an editor pass
        /// (<c>Gamesim ▸ U07 ▸ Generate HUD icons</c>), and a clone that has not run it should get a
        /// plainer badge rather than an empty square.</para>
        /// </summary>
        public static Sprite Icon(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (icons.TryGetValue(name, out var cached)) return cached;
            var sprite = Resources.Load<Sprite>("GamesimIcons/" + name);
            icons[name] = sprite;
            return sprite;
        }

        private static readonly System.Collections.Generic.Dictionary<string, Sprite> icons
            = new System.Collections.Generic.Dictionary<string, Sprite>();

        public static Sprite Circle()
        {
            if (circle != null) return circle;

            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "UiTheme Circle",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave,
            };

            const float half = size * 0.5f;
            const float radius = half - 1f;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - half;
                    float dy = y + 0.5f - half;
                    // The trailing half-pixel is the anti-aliasing term, as in Build above.
                    float alpha = Mathf.Clamp01(0.5f - (Mathf.Sqrt(dx * dx + dy * dy) - radius));
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, false);

            circle = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 100f, 0,
                SpriteMeshType.FullRect);
            circle.name = texture.name;
            circle.hideFlags = HideFlags.HideAndDontSave;
            return circle;
        }

        private static Sprite Fill(int radius) => Cached(FillCache, radius, false);
        private static Sprite OutlineSprite(int radius) => Cached(OutlineCache, radius, true);

        private static Sprite Cached(Dictionary<int, Sprite> cache, int radius, bool ring)
        {
            radius = Mathf.Clamp(radius, 1, 64);
            if (cache.TryGetValue(radius, out var existing) && existing != null) return existing;
            var sprite = Build(radius, ring);
            cache[radius] = sprite;
            return sprite;
        }

        /// <summary>
        /// Builds a rounded-rect sprite from its signed distance field. The 9-slice border is set
        /// one pixel outside the corner radius, leaving a 2px stretchable centre.
        /// </summary>
        private static Sprite Build(int radius, bool ring)
        {
            int size = radius * 2 + 4;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = (ring ? "UiTheme Outline " : "UiTheme Fill ") + radius,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave,
            };

            float half = size * 0.5f;
            float inner = half - radius;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // Distance from the rounded-rect boundary: negative inside, positive outside.
                    float px = Mathf.Abs(x + 0.5f - half) - inner;
                    float py = Mathf.Abs(y + 0.5f - half) - inner;
                    float qx = Mathf.Max(px, 0f);
                    float qy = Mathf.Max(py, 0f);
                    float distance = Mathf.Sqrt(qx * qx + qy * qy) + Mathf.Min(Mathf.Max(px, py), 0f) - radius;

                    // Fill covers d < 0. The ring is inset so it sits on the panel, not outside it,
                    // and the trailing half-pixel is the anti-aliasing term.
                    const float halfBorder = BorderThickness * 0.5f;
                    float alpha = ring
                        ? halfBorder - Mathf.Abs(distance + halfBorder) + 0.5f
                        : 0.5f - distance;
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha) * 255f));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, false);

            float edge = radius + 1f;
            var sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(edge, edge, edge, edge));
            sprite.name = texture.name;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }
    }
}
