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
            written.Add(Write("houseguest", Houseguest));
            written.Add(Write("eye", Eye));
            written.Add(Write("house", House));
            foreach (var glyph in Glyphs) written.Add(Write(glyph.Key, glyph.Value));

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
            var names = new System.Collections.Generic.List<string> { "target", "crown", "veto-token", "trophy", "gavel", "evicted", "key",
                "houseguest", "eye", "house" };
            names.AddRange(Glyphs.Keys);

            const int cell = 160, pad = 16;
            int columns = Mathf.Min(7, names.Count), rows = (names.Count + columns - 1) / columns, width = columns * cell, height = rows * cell;

            var sheet = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var ground = new Color32(18, 26, 36, 255);
            var pixels = new Color32[width * height];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = ground;

            for (int i = 0; i < names.Count; i++)
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
                    int dx = (i % columns) * cell + pad + x, dy = (rows - 1 - i / columns) * cell + pad + y;
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

        /// <summary>
        /// A houseguest: head and shoulders.
        ///
        /// <para>Drawn rather than photographed on purpose. A cast card needs a face before any body
        /// exists to render one, and the honest answer to "who is this" at that point is a
        /// silhouette — a generated portrait would be inventing a person's appearance, and initials
        /// alone make a grid of twenty-four cards read as a spreadsheet.</para>
        /// </summary>
        private static void Houseguest(Canvas c)
        {
            c.Disc(.50f, .70f, .21f);
            // Shoulders: a wide arc, flattened where it meets the bottom edge so the shape reads at
            // 56 px instead of tapering into a point.
            var shoulders = new List<Vector2>();
            const int steps = 28;
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                float angle = Mathf.PI * t;
                shoulders.Add(new Vector2(.50f - Mathf.Cos(angle) * .38f, .08f + Mathf.Sin(angle) * .34f));
            }
            shoulders.Add(new Vector2(.88f, .06f));
            shoulders.Add(new Vector2(.12f, .06f));
            c.Polygon(shoulders.ToArray());
        }

        /// <summary>
        /// A watching eye: a stroked lens with an iris and a pupil.
        ///
        /// <para>A generic surveillance motif, not a reproduction of any broadcaster's mark. The
        /// lens is stroked rather than filled because the canvas only adds coverage — a filled lens
        /// with a filled iris on top is one solid blob, so the outline has to be drawn as a path.
        /// </para>
        /// </summary>
        private static void Eye(Canvas c)
        {
            const int steps = 40;
            const float stroke = .055f;
            Vector2 Lens(float t, int side)
            {
                float x = Mathf.Lerp(.06f, .94f, t);
                float y = .50f + side * Mathf.Sin(Mathf.PI * t) * .30f;
                return new Vector2(x, y);
            }
            foreach (int side in new[] { 1, -1 })
                for (int i = 0; i < steps; i++)
                {
                    var a = Lens(i / (float)steps, side);
                    var b = Lens((i + 1) / (float)steps, side);
                    c.Bar(a.x, a.y, b.x, b.y, stroke);
                }
            c.Ring(.50f, .50f, .17f, .05f);
            c.Disc(.50f, .50f, .08f);
        }

        /// <summary>The house itself: a roof over a body, with a door.</summary>
        private static void House(Canvas c)
        {
            c.Polygon(new[]
            {
                new Vector2(.50f, .92f), new Vector2(.94f, .54f),
                new Vector2(.80f, .54f), new Vector2(.50f, .78f),
                new Vector2(.20f, .54f), new Vector2(.06f, .54f),
            });
            // The walls are drawn around the doorway rather than over it. Coverage only adds, so a
            // door painted on top of a filled body is invisible — the gap has to be left.
            c.Rect(.18f, .10f, .26f, .46f);   // left wall
            c.Rect(.56f, .10f, .26f, .46f);   // right wall
            c.Rect(.44f, .36f, .12f, .20f);   // lintel over the doorway
        }


        // ------------------------------------------------------------------ the mockups' glyphs (V2)

        /// <summary>A heart: two lobes and a point.</summary>
        private static void Heart(Canvas c)
        {
            c.Disc(.34f, .62f, .22f);
            c.Disc(.66f, .62f, .22f);
            c.Polygon(new[] { new Vector2(.13f, .56f), new Vector2(.87f, .56f), new Vector2(.50f, .10f) });
        }

        /// <summary>Two houseguests, for the cast and the relationships cards.</summary>
        private static void People(Canvas c)
        {
            c.Disc(.34f, .70f, .15f);
            c.Disc(.68f, .66f, .13f);
            Shoulders(c, .34f, .14f, .30f, .26f);
            Shoulders(c, .68f, .12f, .25f, .22f);
        }

        private static void Shoulders(Canvas c, float cx, float baseY, float halfWidth, float height)
        {
            var points = new List<Vector2>();
            const int steps = 20;
            for (int i = 0; i <= steps; i++)
            {
                float angle = Mathf.PI * i / steps;
                points.Add(new Vector2(cx - Mathf.Cos(angle) * halfWidth, baseY + Mathf.Sin(angle) * height));
            }
            points.Add(new Vector2(cx + halfWidth, baseY - .02f));
            points.Add(new Vector2(cx - halfWidth, baseY - .02f));
            c.Polygon(points.ToArray());
        }

        /// <summary>A task: a framed square with a tick.</summary>
        private static void Task(Canvas c)
        {
            Frame(c, .12f, .12f, .76f, .76f, .07f);
            c.Bar(.28f, .48f, .44f, .30f, .09f);
            c.Bar(.44f, .30f, .74f, .68f, .09f);
        }

        private static void Frame(Canvas c, float x, float y, float w, float h, float stroke)
        {
            c.Bar(x, y, x + w, y, stroke);
            c.Bar(x + w, y, x + w, y + h, stroke);
            c.Bar(x + w, y + h, x, y + h, stroke);
            c.Bar(x, y + h, x, y, stroke);
        }

        /// <summary>An open journal: two pages and the gap of the spine.</summary>
        private static void Journal(Canvas c)
        {
            c.Polygon(new[] { new Vector2(.10f, .20f), new Vector2(.46f, .26f), new Vector2(.46f, .84f), new Vector2(.10f, .78f) });
            c.Polygon(new[] { new Vector2(.54f, .26f), new Vector2(.90f, .20f), new Vector2(.90f, .78f), new Vector2(.54f, .84f) });
            c.Bar(.10f, .14f, .46f, .20f, .05f);
            c.Bar(.54f, .20f, .90f, .14f, .05f);
        }

        /// <summary>A gear: a ring and eight teeth.</summary>
        private static void Settings(Canvas c)
        {
            c.Ring(.50f, .50f, .30f, .13f);
            for (int i = 0; i < 8; i++)
            {
                float angle = Mathf.PI * 2f * i / 8f;
                float dx = Mathf.Cos(angle), dy = Mathf.Sin(angle);
                c.Bar(.50f + dx * .26f, .50f + dy * .26f, .50f + dx * .44f, .50f + dy * .44f, .13f);
            }
        }

        /// <summary>A camera: a framed body, a viewfinder bump, a lens ring.</summary>
        private static void CameraGlyph(Canvas c)
        {
            Frame(c, .10f, .24f, .80f, .50f, .07f);
            c.Rect(.34f, .72f, .26f, .12f);
            c.Ring(.50f, .49f, .16f, .07f);
            c.Disc(.50f, .49f, .05f);
        }

        /// <summary>A calendar: a frame, a solid header, two pegs, six days.</summary>
        private static void Calendar(Canvas c)
        {
            Frame(c, .12f, .10f, .76f, .70f, .06f);
            c.Rect(.12f, .66f, .76f, .14f);
            c.Rect(.30f, .78f, .08f, .14f);
            c.Rect(.62f, .78f, .08f, .14f);
            for (int row = 0; row < 2; row++)
                for (int col = 0; col < 3; col++)
                    c.Disc(.30f + col * .20f, .50f - row * .18f, .05f);
        }

        /// <summary>A five-point star.</summary>
        private static void Star(Canvas c)
        {
            var points = new List<Vector2>();
            for (int i = 0; i < 10; i++)
            {
                float angle = Mathf.PI / 2f + Mathf.PI * i / 5f;
                float r = i % 2 == 0 ? .46f : .20f;
                points.Add(new Vector2(.50f + Mathf.Cos(angle) * r, .52f + Mathf.Sin(angle) * r));
            }
            c.Polygon(points.ToArray());
        }

        /// <summary>An ear, for eavesdropping: an outer curl and an inner one.</summary>
        private static void Ear(Canvas c)
        {
            c.Arc(.50f, .58f, .30f, 20f, 300f, .08f);
            c.Arc(.48f, .52f, .15f, 40f, 250f, .07f);
            c.Bar(.50f, .28f, .40f, .10f, .09f);
        }

        /// <summary>A speech bubble with a tail.</summary>
        private static void Chat(Canvas c)
        {
            c.Disc(.50f, .58f, .36f);
            c.Polygon(new[] { new Vector2(.30f, .34f), new Vector2(.22f, .08f), new Vector2(.52f, .26f) });
        }

        /// <summary>Two bubbles, for gossip.</summary>
        private static void Gossip(Canvas c)
        {
            c.Disc(.34f, .64f, .25f);
            c.Polygon(new[] { new Vector2(.18f, .48f), new Vector2(.10f, .26f), new Vector2(.36f, .42f) });
            c.Disc(.70f, .40f, .21f);
            c.Polygon(new[] { new Vector2(.84f, .26f), new Vector2(.92f, .06f), new Vector2(.68f, .22f) });
        }

        /// <summary>A bulb, for strategy: a globe, a neck, a base.</summary>
        private static void Bulb(Canvas c)
        {
            c.Disc(.50f, .62f, .27f);
            c.Polygon(new[] { new Vector2(.36f, .46f), new Vector2(.64f, .46f), new Vector2(.60f, .30f), new Vector2(.40f, .30f) });
            c.Rect(.39f, .20f, .22f, .07f);
            c.Rect(.41f, .11f, .18f, .06f);
        }

        /// <summary>Two links, for deals and alliances.</summary>
        private static void Handshake(Canvas c)
        {
            c.Ring(.36f, .50f, .24f, .10f);
            c.Ring(.64f, .50f, .24f, .10f);
        }

        /// <summary>A doorway and an arrow out of it.</summary>
        private static void Exit(Canvas c)
        {
            Frame(c, .14f, .10f, .46f, .80f, .07f);
            c.Bar(.44f, .50f, .88f, .50f, .09f);
            c.Bar(.72f, .66f, .88f, .50f, .09f);
            c.Bar(.72f, .34f, .88f, .50f, .09f);
        }

        /// <summary>A bed seen from the side.</summary>
        private static void Bed(Canvas c)
        {
            c.Rect(.08f, .30f, .07f, .44f);
            c.Rect(.08f, .30f, .84f, .18f);
            c.Rect(.18f, .50f, .22f, .12f);
            c.Rect(.08f, .14f, .07f, .16f);
            c.Rect(.85f, .14f, .07f, .16f);
        }

        /// <summary>A dumbbell.</summary>
        private static void Dumbbell(Canvas c)
        {
            c.Bar(.22f, .50f, .78f, .50f, .10f);
            c.Rect(.10f, .30f, .12f, .40f);
            c.Rect(.24f, .36f, .08f, .28f);
            c.Rect(.68f, .36f, .08f, .28f);
            c.Rect(.78f, .30f, .12f, .40f);
        }

        /// <summary>A fork, for the kitchen.</summary>
        private static void Fork(Canvas c)
        {
            c.Rect(.46f, .08f, .08f, .50f);
            c.Rect(.34f, .56f, .32f, .08f);
            c.Rect(.34f, .62f, .07f, .30f);
            c.Rect(.465f, .62f, .07f, .30f);
            c.Rect(.59f, .62f, .07f, .30f);
        }

        // ------------------------------------------------------------------ the mood faces (V2)

        /// <summary>The face every mood shares: a stroked disc, so the mood is in the eyes and mouth.</summary>
        private static void FaceRing(Canvas c) => c.Ring(.50f, .50f, .44f, .07f);
        private static void Eyes(Canvas c, float r = .06f) { c.Disc(.36f, .60f, r); c.Disc(.64f, .60f, r); }
        private static void Smile(Canvas c, float r = .20f) => c.Arc(.50f, .48f, r, 205f, 335f, .07f);
        private static void Frown(Canvas c) => c.Arc(.50f, .22f, .20f, 25f, 155f, .07f);
        private static void Flat(Canvas c) => c.Bar(.34f, .32f, .66f, .32f, .07f);

        private static void MoodHappy(Canvas c) { FaceRing(c); Eyes(c); Smile(c, .22f); }
        private static void MoodConfident(Canvas c) { FaceRing(c); Eyes(c); c.Arc(.54f, .46f, .18f, 215f, 335f, .07f); c.Bar(.28f, .72f, .42f, .74f, .06f); }
        private static void MoodPlayful(Canvas c) { FaceRing(c); c.Disc(.36f, .60f, .06f); c.Bar(.58f, .61f, .70f, .61f, .06f); Smile(c); c.Disc(.62f, .30f, .05f); }
        private static void MoodCharming(Canvas c) { FaceRing(c); Eyes(c); Smile(c); c.Arc(.36f, .60f, .12f, 30f, 150f, .05f); c.Arc(.64f, .60f, .12f, 30f, 150f, .05f); }
        private static void MoodTense(Canvas c) { FaceRing(c); Eyes(c); Flat(c); c.Bar(.26f, .76f, .42f, .70f, .06f); c.Bar(.58f, .70f, .74f, .76f, .06f); }
        private static void MoodShocked(Canvas c) { FaceRing(c); c.Ring(.36f, .60f, .09f, .05f); c.Ring(.64f, .60f, .09f, .05f); c.Ring(.50f, .30f, .10f, .06f); }
        private static void MoodConcerned(Canvas c) { FaceRing(c); Eyes(c); Frown(c); c.Bar(.26f, .70f, .42f, .76f, .06f); c.Bar(.58f, .76f, .74f, .70f, .06f); }
        private static void MoodAmused(Canvas c) { FaceRing(c); Eyes(c); Smile(c, .24f); c.Bar(.56f, .76f, .72f, .80f, .06f); }
        private static void MoodSuspicious(Canvas c) { FaceRing(c); c.Bar(.28f, .60f, .44f, .60f, .07f); c.Bar(.56f, .60f, .72f, .60f, .07f); c.Bar(.40f, .30f, .66f, .34f, .07f); }
        private static void MoodAnxious(Canvas c) { FaceRing(c); Eyes(c, .08f); c.Bar(.32f, .30f, .41f, .36f, .06f); c.Bar(.41f, .36f, .50f, .30f, .06f); c.Bar(.50f, .30f, .59f, .36f, .06f); c.Bar(.59f, .36f, .68f, .30f, .06f); }
        private static void MoodRelieved(Canvas c) { FaceRing(c); c.Arc(.36f, .58f, .09f, 20f, 160f, .06f); c.Arc(.64f, .58f, .09f, 20f, 160f, .06f); Smile(c); }
        private static void MoodObservant(Canvas c) { FaceRing(c); c.Ring(.36f, .60f, .09f, .045f); c.Ring(.64f, .60f, .09f, .045f); c.Disc(.36f, .60f, .035f); c.Disc(.64f, .60f, .035f); Flat(c); }
        private static void MoodNeutral(Canvas c) { FaceRing(c); Eyes(c); Flat(c); }
        private static void MoodAngry(Canvas c) { FaceRing(c); Eyes(c); Frown(c); c.Bar(.26f, .80f, .44f, .70f, .07f); c.Bar(.56f, .70f, .74f, .80f, .07f); }
        private static void MoodSad(Canvas c) { FaceRing(c); Eyes(c); Frown(c); c.Bar(.28f, .70f, .42f, .76f, .05f); c.Bar(.58f, .76f, .72f, .70f, .05f); }

        /// <summary>The mockups' glyphs and mood faces, by the name UiTheme.Icon loads them under.</summary>
        private static readonly SortedDictionary<string, System.Action<Canvas>> Glyphs = new SortedDictionary<string, System.Action<Canvas>>
        {
            { "heart", Heart }, { "people", People }, { "task", Task }, { "journal", Journal }, { "settings", Settings },
            { "camera", CameraGlyph }, { "calendar", Calendar }, { "star", Star }, { "ear", Ear }, { "chat", Chat },
            { "gossip", Gossip }, { "bulb", Bulb }, { "handshake", Handshake }, { "exit", Exit }, { "bed", Bed },
            { "dumbbell", Dumbbell }, { "fork", Fork },
            { "mood-happy", MoodHappy }, { "mood-confident", MoodConfident }, { "mood-playful", MoodPlayful },
            { "mood-charming", MoodCharming }, { "mood-tense", MoodTense }, { "mood-shocked", MoodShocked },
            { "mood-concerned", MoodConcerned }, { "mood-amused", MoodAmused }, { "mood-suspicious", MoodSuspicious },
            { "mood-anxious", MoodAnxious }, { "mood-relieved", MoodRelieved }, { "mood-observant", MoodObservant },
            { "mood-neutral", MoodNeutral }, { "mood-angry", MoodAngry }, { "mood-sad", MoodSad },
        };

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
            /// <summary>A stroked arc, as a run of short bars; angles in degrees, counter-clockwise from +x.</summary>
            public void Arc(float cx, float cy, float r, float fromDegrees, float toDegrees, float thickness)
            {
                int steps = Mathf.Max(6, Mathf.CeilToInt(Mathf.Abs(toDegrees - fromDegrees) / 8f));
                for (int i = 0; i < steps; i++)
                {
                    float a = Mathf.Deg2Rad * Mathf.Lerp(fromDegrees, toDegrees, i / (float)steps);
                    float b = Mathf.Deg2Rad * Mathf.Lerp(fromDegrees, toDegrees, (i + 1) / (float)steps);
                    Bar(cx + Mathf.Cos(a) * r, cy + Mathf.Sin(a) * r, cx + Mathf.Cos(b) * r, cy + Mathf.Sin(b) * r, thickness);
                }
            }

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
