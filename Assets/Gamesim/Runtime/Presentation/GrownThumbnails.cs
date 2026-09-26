using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gamesim.Presentation
{
    /// <summary>
    /// The creator's pictures of the hair and accessories built in code: a small bust drawn in
    /// code, wearing the style - a fade, an afro, locs - or the piece - glasses, hoops, a cap.
    /// UMA's own items come with thumbnails of themselves on a mannequin; these match them in
    /// kind, so a strip of styles reads as a strip of styles rather than a row of placeholders.
    /// Drawn once each, anti-aliased by signed distance, and shared.
    /// </summary>
    public static class GrownThumbnails
    {
        private const int Size = 128;
        private static readonly Dictionary<string, Sprite> made = new Dictionary<string, Sprite>();

        private static readonly Color Skin = new Color(.78f, .6f, .47f), SkinShade = new Color(.66f, .49f, .37f);
        private static readonly Color Hair = new Color(.17f, .12f, .09f), HairLight = new Color(.33f, .24f, .17f);
        private static readonly Color Top = new Color(.36f, .4f, .47f);

        /// <summary>The picture for a built item, or null for anything else.</summary>
        public static Sprite For(string id)
        {
            if (id == null) return null;
            if (made.TryGetValue(id, out var sprite) && sprite != null) return sprite;
            var hair = ProceduralHair.Find(id);
            var accessory = ProceduralAccessories.Find(id);
            if (hair == null && accessory == null) return null;
            var canvas = new Canvas();
            if (hair != null) DrawHair(canvas, hair);
            else DrawAccessory(canvas, accessory);
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { name = "Thumbnail " + id, wrapMode = TextureWrapMode.Clamp };
            texture.SetPixels(canvas.Pixels);
            texture.Apply(false, false);
            sprite = Sprite.Create(texture, new Rect(0, 0, Size, Size), new Vector2(.5f, .5f), 100f);
            sprite.name = "Thumbnail " + id;
            made[id] = sprite;
            return sprite;
        }

        // Coordinates are in pixels, y up, origin bottom left; the head sits a little above centre.
        private const float HeadX = 64f, HeadY = 70f, HeadW = 25f, HeadH = 31f;

        private static void Bust(Canvas c, bool neckOnly = false)
        {
            c.Paint(p => RoundedBox(p, new Vector2(HeadX, 6f), new Vector2(46f, 22f), 20f), Top);
            c.Paint(p => RoundedBox(p, new Vector2(HeadX, 34f), new Vector2(10f, 14f), 5f), SkinShade);
            if (neckOnly) return;
            c.Paint(p => Ellipse(p, new Vector2(HeadX - HeadW, HeadY - 4f), new Vector2(4.5f, 7f)), SkinShade);
            c.Paint(p => Ellipse(p, new Vector2(HeadX + HeadW, HeadY - 4f), new Vector2(4.5f, 7f)), SkinShade);
            c.Paint(p => Ellipse(p, new Vector2(HeadX, HeadY), new Vector2(HeadW, HeadH)), Skin);
        }

        private static void DrawHair(Canvas c, ProceduralHair.Style style)
        {
            var head = new Vector2(HeadX, HeadY);
            if (style.Kind == ProceduralHair.Kind.Afro)
            {
                Bust(c, neckOnly: true);
                c.Paint(p => Ellipse(p, head + new Vector2(0f, 9f), new Vector2(44f, 42f)), Hair);
                for (int i = 0; i < 40; i++)
                {
                    float a = i / 40f * Mathf.PI * 2f;
                    var at = head + new Vector2(0f, 9f) + new Vector2(Mathf.Cos(a) * 37f, Mathf.Sin(a) * 35f);
                    c.Paint(p => Circle(p, at, 7f), Hair);
                    c.Paint(p => Ring(p, at + new Vector2(-1f, 1f), 3.5f, .9f), HairLight);
                }
                c.Paint(p => Ellipse(p, head + new Vector2(0f, -3f), new Vector2(HeadW, HeadH - 3f)), Skin);
                return;
            }
            if (style.Kind == ProceduralHair.Kind.Strands)
            {
                // Strands hanging behind, drawn before the head so it sits in front of them.
                Bust(c, neckOnly: true);
                bool braids = style.Pattern == ProceduralHair.Pattern.Braid;
                int count = braids ? 16 : 11;
                for (int i = 0; i < count; i++)
                {
                    float t = i / (float)(count - 1);
                    float x = HeadX - 36f + t * 72f;
                    float top = HeadY + 8f, bottom = braids ? 8f : 26f;
                    float width = braids ? 2.4f : 4.2f;
                    var a = new Vector2(x * .6f + HeadX * .4f, top);
                    var b = new Vector2(x, bottom + Mathf.Abs(t - .5f) * 14f);
                    c.Paint(p => Segment(p, a, b, width), Hair);
                    c.Paint(p => Segment(p, a + new Vector2(-width * .35f, 0f), b + new Vector2(-width * .35f, 0f), width * .25f), HairLight);
                }
                c.Paint(p => Ellipse(p, new Vector2(HeadX - HeadW, HeadY - 4f), new Vector2(4.5f, 7f)), SkinShade);
                c.Paint(p => Ellipse(p, new Vector2(HeadX + HeadW, HeadY - 4f), new Vector2(4.5f, 7f)), SkinShade);
                c.Paint(p => Ellipse(p, head, new Vector2(HeadW, HeadH)), Skin);
                Cap(c, 3.5f);
                PatternLines(c, braids ? 6 : 5, vertical: true);
                return;
            }
            Bust(c);
            switch (style.Pattern)
            {
                case ProceduralHair.Pattern.Stubble:
                    Cap(c, 1.8f, new Color(Hair.r, Hair.g, Hair.b, .75f));
                    break;
                case ProceduralHair.Pattern.Crop:
                    // Full on top, faded at the sides.
                    Cap(c, 7f, rise: 4f, sides: .25f);
                    break;
                case ProceduralHair.Pattern.Waves:
                    Cap(c, 3.5f);
                    for (int i = 0; i < 4; i++)
                    {
                        float r = HeadH * .55f + i * 4f;
                        c.Paint(p => Arc(p, head + new Vector2(0f, 2f), r, 1f, 40f, 140f), HairLight);
                    }
                    break;
                case ProceduralHair.Pattern.Coils:
                    Cap(c, 6f);
                    for (int i = 0; i < 16; i++)
                    {
                        float a = Mathf.Lerp(12f, 168f, i / 15f) * Mathf.Deg2Rad;
                        var at = head + new Vector2(Mathf.Cos(a) * (HeadW + 3f), 4f + Mathf.Sin(a) * (HeadH + 3f));
                        c.Paint(p => Circle(p, at, 5f), Hair);
                        c.Paint(p => Ring(p, at, 2.6f, .8f), HairLight);
                    }
                    break;
                case ProceduralHair.Pattern.Rows:
                    Cap(c, 3f);
                    PatternLines(c, 6, vertical: true);
                    break;
            }
        }

        /// <summary>Hair over the top of the head: a band from the hairline up, <paramref name="thickness"/> proud of it.</summary>
        private static void Cap(Canvas c, float thickness, Color? colour = null, float rise = 0f, float sides = 1f)
        {
            var head = new Vector2(HeadX, HeadY);
            var outer = new Vector2(HeadW + thickness * sides, HeadH + thickness + rise);
            float hairline = HeadY + HeadH * .32f;
            c.Paint(p =>
            {
                float shape = Ellipse(p, head + new Vector2(0f, rise * .5f), outer);
                // Down the sides to the ears, above the brow at the front.
                float cut = Mathf.Abs(p.x - HeadX) < HeadW * .62f ? hairline - p.y : HeadY - 2f - p.y;
                return Mathf.Max(shape, cut);
            }, colour ?? Hair);
        }

        private static void PatternLines(Canvas c, int count, bool vertical)
        {
            for (int i = 0; i < count; i++)
            {
                float t = (i + .5f) / count;
                float x = HeadX - HeadW * .8f + t * HeadW * 1.6f;
                var a = new Vector2(x, HeadY + HeadH * .38f);
                var b = new Vector2(HeadX + (x - HeadX) * .55f, HeadY + HeadH + 2f);
                c.Paint(p => Segment(p, a, b, .9f), HairLight);
            }
        }

        private static void DrawAccessory(Canvas c, ProceduralAccessories.Item item)
        {
            Bust(c);
            // A plain short crop on everyone, so the piece is what stands out.
            if (item.Slot != "Headwear") Cap(c, 3f);
            var head = new Vector2(HeadX, HeadY);
            float eyeY = HeadY - 1f;
            var main = item.Main;
            var outline = new Color(main.r * .45f, main.g * .45f, main.b * .45f, 1f);
            switch (item.Shape)
            {
                case ProceduralAccessories.Shape.FramedGlasses:
                case ProceduralAccessories.Shape.RoundGlasses:
                case ProceduralAccessories.Shape.Sunglasses:
                {
                    bool round = item.Shape == ProceduralAccessories.Shape.RoundGlasses, sun = item.Shape == ProceduralAccessories.Shape.Sunglasses;
                    float w = round ? 8f : 10f, h = round ? 8f : sun ? 7.5f : 6.5f;
                    for (int s = -1; s <= 1; s += 2)
                    {
                        var at = new Vector2(HeadX + s * 11f, eyeY);
                        if (sun) c.Paint(p => RoundedBox(p, at, new Vector2(w, h), 4f), item.Second);
                        c.Paint(p => Mathf.Abs(round ? Circle(p, at, w) : RoundedBox(p, at, new Vector2(w, h), 3f)) - (round ? .9f : 1.7f), main);
                        c.Paint(p => Segment(p, at + new Vector2(s * w, 2f), new Vector2(HeadX + s * HeadW, eyeY + 3f), 1.3f), main);
                    }
                    c.Paint(p => Arc(p, new Vector2(HeadX, eyeY - 1f), 4f, 1.2f, 20f, 160f), main);
                    break;
                }
                case ProceduralAccessories.Shape.Visor:
                    c.Paint(p => RoundedBox(p, new Vector2(HeadX, eyeY), new Vector2(HeadW + 2f, 6.5f), 5f), main);
                    c.Paint(p => RoundedBox(p, new Vector2(HeadX - 6f, eyeY + 2.5f), new Vector2(HeadW * .6f, 1.2f), 1f), Color.white);
                    break;
                case ProceduralAccessories.Shape.Studs:
                    for (int s = -1; s <= 1; s += 2) { var at = new Vector2(HeadX + s * (HeadW + 1.5f), HeadY - 11f); c.Paint(p => Circle(p, at, 2.8f), main); }
                    break;
                case ProceduralAccessories.Shape.Hoops:
                    for (int s = -1; s <= 1; s += 2) { var at = new Vector2(HeadX + s * (HeadW + 2f), HeadY - 18f); c.Paint(p => Ring(p, at, 6.5f, 1.4f), main); }
                    break;
                case ProceduralAccessories.Shape.Drops:
                    for (int s = -1; s <= 1; s += 2)
                    {
                        var top = new Vector2(HeadX + s * (HeadW + 1.5f), HeadY - 11f);
                        c.Paint(p => Segment(p, top, top + new Vector2(0f, -10f), .7f), main);
                        c.Paint(p => Ellipse(p, top + new Vector2(0f, -14f), new Vector2(3f, 4.5f)), item.Second);
                    }
                    break;
                case ProceduralAccessories.Shape.Chain:
                    c.Paint(p => Arc(p, new Vector2(HeadX, 38f), 16f, 1.3f, 200f, 340f), main);
                    break;
                case ProceduralAccessories.Shape.Collar:
                    c.Paint(p => Arc(p, new Vector2(HeadX, 40f), 14f, 2f, 200f, 340f), main);
                    for (int i = 0; i < 9; i++)
                    {
                        float a = Mathf.Lerp(208f, 332f, i / 8f) * Mathf.Deg2Rad;
                        float size = Mathf.Lerp(2.4f, 4.2f, 1f - Mathf.Abs(i - 4f) / 4f);
                        var at = new Vector2(HeadX + Mathf.Cos(a) * 14f, 40f + Mathf.Sin(a) * 14f - size * .5f);
                        c.Paint(p => Circle(p, at, size), main);
                    }
                    break;
                case ProceduralAccessories.Shape.Beads:
                    for (int i = 0; i < 15; i++)
                    {
                        float a = Mathf.Lerp(200f, 340f, i / 14f) * Mathf.Deg2Rad;
                        var at = new Vector2(HeadX + Mathf.Cos(a) * 17f, 40f + Mathf.Sin(a) * 17f);
                        c.Paint(p => Circle(p, at, 2.2f), i % 3 == 0 ? item.Second : main);
                    }
                    break;
                case ProceduralAccessories.Shape.BowTie:
                {
                    var knot = new Vector2(HeadX, 30f);
                    c.Paint(p => Triangle(p, knot, knot + new Vector2(-15f, 7f), knot + new Vector2(-15f, -7f)), main);
                    c.Paint(p => Triangle(p, knot, knot + new Vector2(15f, 7f), knot + new Vector2(15f, -7f)), main);
                    c.Paint(p => RoundedBox(p, knot, new Vector2(3.2f, 3.6f), 1.2f), outline);
                    break;
                }
                case ProceduralAccessories.Shape.TruckerCap:
                    c.Paint(p => Mathf.Max(Ellipse(p, head + new Vector2(0f, 6f), new Vector2(HeadW + 3f, HeadH)), HeadY + 8f - p.y), main);
                    c.Paint(p => RoundedBox(p, new Vector2(HeadX + 6f, HeadY + 8f), new Vector2(HeadW + 10f, 2.6f), 2.6f), main);
                    c.Paint(p => Circle(p, head + new Vector2(0f, HeadH + 5.5f), 2f), outline);
                    break;
                case ProceduralAccessories.Shape.Beanie:
                    c.Paint(p => Mathf.Max(Ellipse(p, head + new Vector2(0f, 4f), new Vector2(HeadW + 3f, HeadH + 1f)), HeadY + 5f - p.y), main);
                    c.Paint(p => RoundedBox(p, new Vector2(HeadX, HeadY + 8f), new Vector2(HeadW + 3.5f, 4f), 3f), outline);
                    for (int i = 0; i < 9; i++)
                    {
                        float x = HeadX - HeadW + 2f + i * (HeadW * 2f - 4f) / 8f;
                        c.Paint(p => Segment(p, new Vector2(x, HeadY + 13f), new Vector2(HeadX + (x - HeadX) * .6f, HeadY + HeadH + 2f), .6f), outline);
                    }
                    break;
            }
        }

        // ---- Signed distances, in pixels: negative inside ------------------------------------------------

        private static float Circle(Vector2 p, Vector2 centre, float radius) => (p - centre).magnitude - radius;
        private static float Ring(Vector2 p, Vector2 centre, float radius, float width) => Mathf.Abs((p - centre).magnitude - radius) - width;

        private static float Ellipse(Vector2 p, Vector2 centre, Vector2 radii)
        {
            var d = p - centre;
            float k = new Vector2(d.x / radii.x, d.y / radii.y).magnitude;
            return (k - 1f) * Mathf.Min(radii.x, radii.y);
        }

        private static float RoundedBox(Vector2 p, Vector2 centre, Vector2 half, float radius)
        {
            var d = new Vector2(Mathf.Abs(p.x - centre.x), Mathf.Abs(p.y - centre.y)) - half + Vector2.one * radius;
            return new Vector2(Mathf.Max(d.x, 0f), Mathf.Max(d.y, 0f)).magnitude + Mathf.Min(Mathf.Max(d.x, d.y), 0f) - radius;
        }

        private static float Segment(Vector2 p, Vector2 a, Vector2 b, float width)
        {
            var ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-5f, ab.sqrMagnitude));
            return (p - (a + ab * t)).magnitude - width;
        }

        /// <summary>A band along a circle between two angles, counter-clockwise from the right.</summary>
        private static float Arc(Vector2 p, Vector2 centre, float radius, float width, float from, float to)
        {
            var d = p - centre;
            float angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            if (angle < 0f) angle += 360f;
            if (angle >= from && angle <= to) return Mathf.Abs(d.magnitude - radius) - width;
            var a = centre + new Vector2(Mathf.Cos(from * Mathf.Deg2Rad), Mathf.Sin(from * Mathf.Deg2Rad)) * radius;
            var b = centre + new Vector2(Mathf.Cos(to * Mathf.Deg2Rad), Mathf.Sin(to * Mathf.Deg2Rad)) * radius;
            return Mathf.Min((p - a).magnitude, (p - b).magnitude) - width;
        }

        private static float Triangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float Edge(Vector2 x, Vector2 y)
            {
                var e = y - x; var n = new Vector2(e.y, -e.x).normalized;
                return Vector2.Dot(p - x, n);
            }
            float d0 = Edge(a, b), d1 = Edge(b, c), d2 = Edge(c, a);
            float sign = Vector2.Dot(new Vector2((b - a).y, -(b - a).x), c - a) > 0f ? -1f : 1f;
            return Mathf.Max(d0 * sign, Mathf.Max(d1 * sign, d2 * sign));
        }

        private sealed class Canvas
        {
            public readonly Color[] Pixels = new Color[Size * Size];

            /// <summary>Lays a shape over what is drawn, its edge softened over a pixel.</summary>
            public void Paint(Func<Vector2, float> distance, Color colour)
            {
                for (int y = 0; y < Size; y++)
                    for (int x = 0; x < Size; x++)
                    {
                        float coverage = Mathf.Clamp01(.5f - distance(new Vector2(x + .5f, y + .5f)));
                        if (coverage <= 0f) continue;
                        float alpha = coverage * colour.a;
                        var under = Pixels[y * Size + x];
                        float outAlpha = alpha + under.a * (1f - alpha);
                        var rgb = outAlpha > 0f ? (new Color(colour.r, colour.g, colour.b) * alpha + new Color(under.r, under.g, under.b) * under.a * (1f - alpha)) / outAlpha : Color.clear;
                        Pixels[y * Size + x] = new Color(rgb.r, rgb.g, rgb.b, outAlpha);
                    }
            }
        }
    }
}
