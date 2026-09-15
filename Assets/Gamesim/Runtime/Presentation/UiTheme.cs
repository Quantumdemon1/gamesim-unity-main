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
        public static readonly Color Paper = Hex("F2F5FA");
        public static readonly Color Muted = Hex("94A7B8");

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
