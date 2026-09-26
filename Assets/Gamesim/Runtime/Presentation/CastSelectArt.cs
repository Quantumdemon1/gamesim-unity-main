using System.Collections.Generic;
using UnityEngine;

namespace Gamesim.Presentation
{
    /// <summary>
    /// The cast screen's own dress, drawn once in code and shared: the gold ring each glamour photo
    /// sits in, the soft glow behind a picked one, the capsule plates for names and nicknames in
    /// each category's two colours, and the photos themselves.
    ///
    /// <para>The colours are the web game's: its portrait ring runs amber to yellow to orange, its
    /// name plate is slate with an amber edge, and each category wears the gradient its
    /// <c>archetypeInfo</c> gives it.</para>
    /// </summary>
    internal static class CastSelectArt
    {
        private static readonly Color Amber = UiTheme.Hex("FBBF24"), Yellow = UiTheme.Hex("EAB308"), Orange = UiTheme.Hex("F97316");
        private static readonly Color AmberPale = UiTheme.Hex("FDE68A");

        /// <summary>The gold of the ring, for anything that should match it.</summary>
        public static Color Gold => Amber;

        /// <summary>A category's two colours, left to right.</summary>
        public static (Color from, Color to) CategoryColours(string category)
        {
            switch ((category ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "strategist": return (UiTheme.Hex("3B82F6"), UiTheme.Hex("9333EA"));
                case "competitor": return (UiTheme.Hex("EF4444"), UiTheme.Hex("F97316"));
                case "socialite": return (UiTheme.Hex("F472B6"), UiTheme.Hex("F43F5E"));
                case "wildcard": return (UiTheme.Hex("A855F7"), UiTheme.Hex("D946EF"));
                case "underdog": return (UiTheme.Hex("2DD4BF"), UiTheme.Hex("06B6D4"));
                default: return (UiTheme.Accent, UiTheme.Glow);
            }
        }

        private static readonly Dictionary<string, Texture2D> photos = new Dictionary<string, Texture2D>();

        /// <summary>A houseguest's glamour photo - square, on the face - or null when they have none.</summary>
        public static Texture2D Glamour(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (photos.TryGetValue(id, out var photo)) return photo;
            photo = Resources.Load<Texture2D>("Portraits/Glamour/" + id);
            photos[id] = photo;
            return photo;
        }

        private static Sprite ring, glow, plate;
        private static readonly Dictionary<string, Sprite> pills = new Dictionary<string, Sprite>();

        /// <summary>The portrait ring: gold running amber to yellow to orange, a pale inner edge.</summary>
        public static Sprite Ring()
        {
            if (ring != null) return ring;
            const int size = 256;
            var pixels = new Color32[size * size];
            float outer = size * .5f, inner = outer - 13f, edge = inner + 2.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = x + .5f - outer, dy = y + .5f - outer;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float coverage = Mathf.Clamp01(outer - r) * Mathf.Clamp01(r - inner);
                    if (coverage <= 0f) continue;
                    // Diagonal, top left to bottom right, as the web's gradient runs.
                    float t = Mathf.Clamp01(((x / (float)size) + (1f - y / (float)size)) * .5f);
                    var gold = t < .5f ? Color.Lerp(Amber, Yellow, t * 2f) : Color.Lerp(Yellow, Orange, (t - .5f) * 2f);
                    if (r < edge) gold = Color.Lerp(gold, AmberPale, .5f);
                    gold.a = coverage;
                    pixels[y * size + x] = gold;
                }
            ring = Make("Cast ring", size, size, pixels, Vector4.zero);
            return ring;
        }

        /// <summary>A soft round glow, white, to be tinted.</summary>
        public static Sprite Glow()
        {
            if (glow != null) return glow;
            const int size = 128;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + .5f) / size * 2f - 1f, dy = (y + .5f) / size * 2f - 1f;
                    float r = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy));
                    float a = (1f - r) * (1f - r);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            glow = Make("Cast glow", size, size, pixels, Vector4.zero);
            return glow;
        }

        /// <summary>The name plate: a slate capsule, lighter at its ends, with an amber edge.</summary>
        public static Sprite NamePlate()
        {
            if (plate != null) return plate;
            var slate = UiTheme.Hex("1E293B");
            var deep = UiTheme.Hex("0F172A");
            plate = Capsule("Cast name plate", t => Color.Lerp(slate, deep, 1f - Mathf.Abs(t * 2f - 1f)),
                new Color(UiTheme.Hex("F59E0B").r, UiTheme.Hex("F59E0B").g, UiTheme.Hex("F59E0B").b, .45f));
            return plate;
        }

        /// <summary>A capsule in a category's gradient.</summary>
        public static Sprite CategoryPill(string category)
        {
            string key = (category ?? string.Empty).ToLowerInvariant();
            if (pills.TryGetValue(key, out var sprite) && sprite != null) return sprite;
            var (from, to) = CategoryColours(category);
            sprite = Capsule("Cast pill " + key, t => Color.Lerp(from, to, t), new Color(1f, 1f, 1f, .2f));
            pills[key] = sprite;
            return sprite;
        }

        /// <summary>The colour words read in on a category's pill.</summary>
        public static Color OnCategory(string category)
        {
            var (from, to) = CategoryColours(category);
            return UiTheme.OnColor(Color.Lerp(from, to, .5f));
        }

        /// <summary>
        /// A capsule 128 by 40, nine-sliced so it stretches to any width with its round ends kept:
        /// the fill a function of how far across it is, a one-pixel edge in <paramref name="edge"/>.
        /// </summary>
        private static Sprite Capsule(string name, System.Func<float, Color> fill, Color edge)
        {
            const int w = 128, h = 40;
            float radius = h * .5f;
            var pixels = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float px = x + .5f, py = y + .5f;
                    float cx = Mathf.Clamp(px, radius, w - radius);
                    float d = Mathf.Sqrt((px - cx) * (px - cx) + (py - radius) * (py - radius));
                    float coverage = Mathf.Clamp01(radius - d);
                    if (coverage <= 0f) continue;
                    var c = fill(px / w);
                    float rim = Mathf.Clamp01(1.5f - Mathf.Abs(radius - 1f - d));
                    c = Color.Lerp(c, new Color(edge.r, edge.g, edge.b, 1f), rim * edge.a);
                    c.a = coverage;
                    pixels[y * w + x] = c;
                }
            return Make(name, w, h, pixels, new Vector4(radius - 1f, 0f, radius - 1f, 0f));
        }

        private static Sprite Make(string name, int w, int h, Color32[] pixels, Vector4 border)
        {
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            var sprite = Sprite.Create(texture, new Rect(0, 0, w, h), new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect, border);
            sprite.name = name;
            return sprite;
        }
    }
}
