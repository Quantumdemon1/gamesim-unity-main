using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gamesim.Presentation
{
    /// <summary>
    /// Hair grown from the head it sits on: the textured styles UMA's library has none of - a buzz
    /// cut, a fade, waves, short coils, an afro, cornrows, locs and box braids.
    ///
    /// <para>Each style starts from the scalp of the built body itself (see <see cref="HeadScan"/>):
    /// the head's own skin above a hairline, lifted off it by the style's thickness, thinned down
    /// the sides for a fade, rounded out for an afro, or seeded with strands that fall from it for
    /// locs and braids. Grown from the head rather than modelled beside one, it fits every face and
    /// every proportion slider on both bodies, and it follows the head because it hangs from it.
    /// The surfaces are drawn once, in code, and shared (<see cref="HairTextures"/>).</para>
    ///
    /// <para>Presentation only: a style is an item in the wardrobe's Hair slot like any other, saved
    /// by its id, and nothing here draws on the season's randomness.</para>
    /// </summary>
    public static class ProceduralHair
    {
        public const string Prefix = "gs-hair-";
        public const string RootName = "Gamesim hair";

        public enum Kind { Shell, Afro, Strands }
        public enum Pattern { Stubble, Crop, Waves, Coils, Rows, Parted, Locs, Braid, Knit }

        public sealed class Style
        {
            public readonly string Id, Label;
            public readonly Kind Kind;
            public readonly Pattern Pattern;
            /// <summary>Metres off the scalp at the crown; for strands, the base they grow from.</summary>
            public readonly float Thickness;
            /// <summary>How far the sides and back thin toward the skin: a fade.</summary>
            public readonly float Taper;
            /// <summary>Metres of lumpiness: coil clumps, a silhouette that is not a helmet.</summary>
            public readonly float Clump;
            public readonly float StrandLength, StrandRadius;
            public readonly int StrandCount;
            /// <summary>How high the hairline sits over the brow, as a share of eyes-to-crown.</summary>
            public readonly float Hairline;

            public Style(string id, string label, Kind kind, Pattern pattern, float thickness, float taper = 0f, float clump = 0f,
                float strandLength = 0f, float strandRadius = 0f, int strandCount = 0, float hairline = .5f)
            {
                Id = id; Label = label; Kind = kind; Pattern = pattern; Thickness = thickness; Taper = taper; Clump = clump;
                StrandLength = strandLength; StrandRadius = strandRadius; StrandCount = strandCount; Hairline = hairline;
            }
        }

        public static readonly Style[] Styles =
        {
            new Style(Prefix + "buzz", "Buzz cut", Kind.Shell, Pattern.Stubble, .003f, hairline: .5f),
            new Style(Prefix + "fade", "Taper fade", Kind.Shell, Pattern.Crop, .013f, taper: .9f, clump: .0015f, hairline: .48f),
            new Style(Prefix + "waves", "Waves", Kind.Shell, Pattern.Waves, .007f, taper: .45f, hairline: .48f),
            new Style(Prefix + "coils", "Short coils", Kind.Shell, Pattern.Coils, .026f, taper: .4f, clump: .008f, hairline: .46f),
            new Style(Prefix + "afro", "Afro", Kind.Afro, Pattern.Coils, .075f, clump: .012f, hairline: .44f),
            new Style(Prefix + "cornrows", "Cornrows", Kind.Shell, Pattern.Rows, .006f, hairline: .48f),
            new Style(Prefix + "locs", "Locs", Kind.Strands, Pattern.Locs, .008f, strandLength: .24f, strandRadius: .009f, strandCount: 84, hairline: .48f),
            new Style(Prefix + "braids", "Box braids", Kind.Strands, Pattern.Braid, .006f, strandLength: .4f, strandRadius: .0064f, strandCount: 150, hairline: .48f),
        };

        public static bool IsProcedural(string id) => id != null && id.StartsWith(Prefix, StringComparison.Ordinal);

        public static Style Find(string id)
        {
            foreach (var style in Styles) if (style.Id == id) return style;
            return null;
        }

        /// <summary>Takes grown hair off a body.</summary>
        public static void Remove(GameObject body) => GrownPiece.RemoveAll(body, RootName);

        /// <summary>
        /// Grows a style on a built humanoid body, replacing any it had. Null when the body has no
        /// humanoid head, or no readable skin to grow from.
        /// </summary>
        public static GameObject Grow(GameObject body, string styleId, Color colour, Color? skin = null)
        {
            Remove(body);
            var style = Find(styleId);
            if (body == null || style == null) return null;
            var scan = HeadScan.Read(body);
            if (scan == null) return null;
            var parts = new MeshParts();
            if (!Shell(scan, style, parts)) return null;
            if (style.Kind == Kind.Strands) Strands(scan, style, parts, StrandSubmesh);
            // The shell in three bands - full, thinned, close - the thinner showing more scalp through
            // it, as a buzz cut or the sides of a fade do; then the strands.
            var basePattern = style.Kind == Kind.Strands ? Pattern.Parted : style.Pattern;
            var materials = new[]
            {
                HairTextures.Material(basePattern, colour, skin, Bare(style, 0)),
                HairTextures.Material(Pattern.Stubble, colour, skin, Bare(style, 1)),
                HairTextures.Material(Pattern.Stubble, colour, skin, Bare(style, 2)),
                HairTextures.Material(style.Pattern, colour),
            };
            var piece = GrownPiece.Attach(scan, scan.Head, RootName, style.Id, parts, materials);
            return piece == null ? null : piece.gameObject;
        }

        private const int StrandSubmesh = 3;

        /// <summary>How much scalp shows through a band of the shell: none on full hair, a little through a buzz cut, most low on a fade.</summary>
        private static float Bare(Style style, int band)
        {
            if (style.Pattern == Pattern.Stubble) return band == 0 ? .38f : band == 1 ? .5f : .62f;
            return band == 0 ? 0f : band == 1 ? .38f : .6f;
        }

        /// <summary>The height of the hairline at a bearing round the head: over the brow, the temples, the ears, the nape.</summary>
        internal static float HairlineAt(HeadScan scan, Style style, float degrees)
        {
            float a = Mathf.Abs(degrees), h = scan.Height;
            float[] at = { 0f, 45f, 80f, 115f, 180f };
            float[] rise = { style.Hairline, style.Hairline * .8f, .2f, -.18f, -.62f };
            for (int i = 1; i < at.Length; i++)
                if (a <= at[i])
                    return scan.EyeY + h * Mathf.Lerp(rise[i - 1], rise[i], Mathf.SmoothStep(0f, 1f, (a - at[i - 1]) / (at[i] - at[i - 1])));
            return scan.EyeY + h * rise[rise.Length - 1];
        }

        private static bool OnScalp(HeadScan scan, Style style, Vector3 p)
        {
            if (p.y <= HairlineAt(scan, style, scan.Around(p))) return false;
            // The ears stand out past the skull; hair stops round them.
            bool ear = Mathf.Abs(p.x - scan.Centre.x) > scan.SkullHalfWidth + .003f && p.y < scan.EyeY + .32f * scan.Height;
            return !ear;
        }

        /// <summary>
        /// The scalp lifted into hair: a close crop, a fade thinning down the sides, coils, or an afro
        /// rounded out from the head. The open edge at the hairline is turned down to the skin, so
        /// the hair has a lip rather than a paper edge.
        /// </summary>
        private static bool Shell(HeadScan scan, Style style, MeshParts parts)
        {
            scan.Region(p => OnScalp(scan, style, p), out var points, out var normals, out var triangles);
            if (triangles.Length < 90) return false;
            if (style.Kind == Kind.Afro || style.Clump > .004f) Subdivide(ref points, ref triangles, ref normals);

            float h = scan.Height;
            bool afro = style.Kind == Kind.Afro;
            // The afro's outline: the head's own extents grown out, highest and fullest behind the crown.
            var centre = scan.Centre + new Vector3(0f, h * .15f, -scan.Extent.z * .12f);
            float reachUp = scan.Extent.y + style.Thickness * .95f;
            float reachSide = scan.Extent.x + style.Thickness * .95f;
            float reachBack = scan.Extent.z + style.Thickness * .9f;
            float reachFront = scan.Extent.z + style.Thickness * .35f;
            var outer = new Vector3[points.Length];
            var faded = new float[points.Length];
            for (int i = 0; i < points.Length; i++)
            {
                var p = points[i];
                float degrees = scan.Around(p);
                float hairline = HairlineAt(scan, style, degrees);
                // Up from the hairline, 0 to 1 over the first quarter of the head.
                float grown = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((p.y - hairline) / (h * .25f)));
                float lift = style.Thickness;
                // A fade: full on top, thinning to almost nothing low on the sides and the back.
                float side = Mathf.Clamp01(Mathf.Abs(degrees) / 90f);
                float low = Mathf.Clamp01(1f - (p.y - scan.EyeY) / (h * .85f));
                faded[i] = side * low * style.Taper;
                lift *= Mathf.Lerp(1f, Mathf.Lerp(1f, .1f, side * low), style.Taper);
                var q = p + normals[i] * (lift * Mathf.Lerp(.25f, 1f, Mathf.Sqrt(grown)));
                if (afro)
                {
                    var from = p - centre;
                    var dir = from.normalized;
                    float rz = dir.z > 0f ? reachFront : reachBack;
                    float ry = dir.y > 0f ? reachUp : scan.Extent.y * .95f;
                    float envelope = 1f / Mathf.Sqrt(dir.x * dir.x / (reachSide * reachSide) + dir.y * dir.y / (ry * ry) + dir.z * dir.z / (rz * rz));
                    float reach = Mathf.Max(from.magnitude + style.Thickness * .2f, envelope);
                    q = Vector3.Lerp(q, centre + dir * reach, grown);
                }
                if (style.Clump > 0f)
                    q += normals[i] * ((Lumps(p, 42f) - .5f) * 2f * style.Clump * grown);
                outer[i] = q;
            }

            // The lip: every open edge joined back down to the skin just under it.
            var edgeUse = new Dictionary<long, int>();
            for (int t = 0; t < triangles.Length; t += 3)
                for (int e = 0; e < 3; e++)
                {
                    long key = EdgeKey(triangles[t + e], triangles[t + (e + 1) % 3]);
                    edgeUse[key] = edgeUse.TryGetValue(key, out int n) ? n + 1 : 1;
                }
            var vertices = new List<Vector3>(outer);
            var fadedAll = new List<float>(faded);
            var faces = new List<int>(triangles);
            var lip = new Dictionary<int, int>();
            int Lip(int a)
            {
                if (lip.TryGetValue(a, out int found)) return found;
                found = vertices.Count;
                vertices.Add(points[a] - normals[a] * .0015f);
                fadedAll.Add(faded[a]);
                lip.Add(a, found);
                return found;
            }
            for (int t = 0; t < triangles.Length; t += 3)
                for (int e = 0; e < 3; e++)
                {
                    int a = triangles[t + e], b = triangles[t + (e + 1) % 3];
                    if (edgeUse[EdgeKey(a, b)] != 1) continue;
                    int la = Lip(a), lb = Lip(b);
                    faces.Add(b); faces.Add(a); faces.Add(la);
                    faces.Add(b); faces.Add(la); faces.Add(lb);
                }

            // Normals from the lifted surface, then every triangle given its own corners so each can
            // take the texture projected along whichever axis it faces most.
            var smooth = HeadScan.SmoothNormals(vertices.ToArray(), faces.ToArray());
            for (int i = 0; i < outer.Length; i++)
                if (Vector3.Dot(smooth[i], vertices[i] - scan.Centre) < 0f) smooth[i] = -smooth[i];
            float density = Density(style.Kind == Kind.Strands ? Pattern.Parted : style.Pattern);
            bool rows = style.Pattern == Pattern.Rows;
            for (int t = 0; t + 2 < faces.Count; t += 3)
            {
                var a = vertices[faces[t]]; var b = vertices[faces[t + 1]]; var c = vertices[faces[t + 2]];
                var face = Vector3.Cross(b - a, c - a);
                float ax = Mathf.Abs(face.x), ay = Mathf.Abs(face.y), az = Mathf.Abs(face.z);
                int axis = ay >= ax && ay >= az ? 1 : ax >= az ? 0 : 2;
                // The lip faces out and down from the hairline, whatever its neighbours' normals say.
                bool lipFace = faces[t] >= outer.Length || faces[t + 1] >= outer.Length || faces[t + 2] >= outer.Length;
                var lipNormal = Vector3.zero;
                if (lipFace)
                {
                    lipNormal = face.normalized;
                    var middle = (a + b + c) / 3f - scan.Centre;
                    middle.y = 0f;
                    if (Vector3.Dot(lipNormal, middle) < 0f) lipNormal = -lipNormal;
                }
                for (int k = 0; k < 3; k++)
                {
                    int index = faces[t + k];
                    var p = vertices[index];
                    var normal = lipFace ? lipNormal : smooth[index];
                    Vector2 uv = axis == 1 ? new Vector2(p.x, p.z) : axis == 0 ? (rows ? new Vector2(p.y, p.z) : new Vector2(p.z, p.y)) : new Vector2(p.x, p.y);
                    parts.Vertex(p, normal, uv * density);
                }
                // The band: how faded the triangle is, where the scalp shows through.
                float fade = 0f;
                for (int k = 0; k < 3; k++) fade += fadedAll[faces[t + k]];
                fade /= 3f;
                int band = style.Pattern == Pattern.Stubble ? 0 : fade > .42f ? 2 : fade > .2f ? 1 : 0;
                int start = parts.Vertices.Count - 3;
                parts.Triangle(band, start, start + 1, start + 2);
            }
            return true;
        }

        private static long EdgeKey(int a, int b) => a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;

        /// <summary>Texture repeats a metre: sized so a coil, a row or a wave comes out the size it is on a real head.</summary>
        internal static float Density(Pattern pattern)
        {
            switch (pattern)
            {
                case Pattern.Stubble: return 10f;
                case Pattern.Crop: return 8f;
                case Pattern.Waves: return 6f;
                case Pattern.Coils: return 8f;
                case Pattern.Rows: return 5f;
                case Pattern.Parted: return 6f;
                case Pattern.Knit: return 9f;
                default: return 8f;
            }
        }

        /// <summary>A small, repeatable noise field: the same head gets the same lumps every build.</summary>
        private static float Lumps(Vector3 p, float scale)
            => Mathf.PerlinNoise(p.x * scale + 17.3f, p.y * scale + 3.1f) * .5f + Mathf.PerlinNoise(p.z * scale + 7.7f, p.y * scale + 41.9f) * .5f;

        /// <summary>Splits every triangle in four, sharing the new midpoints: enough resolution for coils to show.</summary>
        private static void Subdivide(ref Vector3[] points, ref int[] triangles, ref Vector3[] normals)
        {
            var p = new List<Vector3>(points);
            var n = new List<Vector3>(normals);
            var mids = new Dictionary<long, int>();
            int Mid(int a, int b)
            {
                long key = EdgeKey(a, b);
                if (mids.TryGetValue(key, out int m)) return m;
                m = p.Count; p.Add((p[a] + p[b]) * .5f); n.Add((n[a] + n[b]).normalized); mids.Add(key, m);
                return m;
            }
            var t = new List<int>(triangles.Length * 4);
            for (int i = 0; i < triangles.Length; i += 3)
            {
                int a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
                int ab = Mid(a, b), bc = Mid(b, c), ca = Mid(c, a);
                t.AddRange(new[] { a, ab, ca, ab, b, bc, ca, bc, c, ab, bc, ca });
            }
            points = p.ToArray(); normals = n.ToArray(); triangles = t.ToArray();
        }

        /// <summary>
        /// Locs or braids falling from the base: out from their roots, then down and back, kept
        /// clear of the face, the head and the shoulders they hang past.
        /// </summary>
        private static void Strands(HeadScan scan, Style style, MeshParts parts, int submesh)
        {
            scan.Region(p => OnScalp(scan, style, p), out var roots, out var normals, out _);
            if (roots.Length == 0) return;
            float h = scan.Height;
            const int segments = 14;
            var random = new System.Random(20260925);
            var path = new Vector3[segments + 1];
            int placed = 0;
            for (int attempt = 0; attempt < style.StrandCount * 8 && placed < style.StrandCount; attempt++)
            {
                int index = random.Next(roots.Length);
                var root = roots[index];
                var normal = normals[index];
                float degrees = scan.Around(root);
                // Nothing rooted over the face falls across it: the front is swept back from the crown.
                if (Mathf.Abs(degrees) < 50f && root.y < scan.CrownY - h * .2f) continue;
                placed++;
                var outward = new Vector3(root.x - scan.Centre.x, 0f, root.z - scan.Centre.z).normalized;
                var point = root + normal * (style.Thickness + style.StrandRadius * .8f);
                // Along the scalp, back and outward, barely off it: the head keeps them on its surface.
                var along = Vector3.ProjectOnPlane(outward * .55f + Vector3.back * .8f + Vector3.down * .25f, normal);
                var direction = (along.normalized * .9f + normal * .1f).normalized;
                float step = style.StrandLength / segments * (.85f + .3f * (float)random.NextDouble());
                path[0] = point;
                for (int s = 1; s <= segments; s++)
                {
                    direction = (direction + Vector3.down * .42f).normalized;
                    point += direction * step;
                    point = ClearOf(scan, point, style.StrandRadius + .004f);
                    path[s] = point;
                }
                parts.Tube(submesh, path, style.StrandRadius, 6, tipScale: .75f, uvAlong: 40f);
            }
        }

        /// <summary>A point pushed out of the head, the neck and the shoulders, with <paramref name="room"/> to spare.</summary>
        private static Vector3 ClearOf(HeadScan scan, Vector3 point, float room)
        {
            var radii = scan.Extent + Vector3.one * room;
            var off = point - scan.Centre;
            var scaled = new Vector3(off.x / radii.x, off.y / radii.y, off.z / radii.z);
            if (scaled.sqrMagnitude < 1f) point = scan.Centre + Vector3.Scale(scaled.normalized, radii);
            if (!scan.HasTorso) return point;
            // The neck, as an upright cylinder from the head bone down to the chest.
            var neck = scan.Neck;
            float neckRadius = .065f + room;
            if (point.y < scan.ChinY && point.y > scan.Chest.y)
            {
                var flat = new Vector2(point.x - neck.x, point.z - neck.z);
                if (flat.magnitude < neckRadius)
                {
                    flat = flat.sqrMagnitude > 1e-8f ? flat.normalized * neckRadius : new Vector2(0f, -neckRadius);
                    point = new Vector3(neck.x + flat.x, point.y, neck.z + flat.y);
                }
            }
            // The shoulders and upper back, as an ellipsoid round the chest.
            var chest = scan.Chest;
            float halfWidth = Mathf.Abs(scan.RightShoulder.x - scan.LeftShoulder.x) * .5f + .06f + room;
            var torso = new Vector3(halfWidth, Mathf.Max(.1f, neck.y - chest.y) + .04f + room, .14f + room);
            var fromChest = point - chest;
            var inTorso = new Vector3(fromChest.x / torso.x, fromChest.y / torso.y, fromChest.z / torso.z);
            if (inTorso.sqrMagnitude < 1f) point = chest + Vector3.Scale(inTorso.normalized, torso);
            return point;
        }
    }

    /// <summary>
    /// The hair's surface, drawn in code: a detail texture the chosen colour multiplies and a normal
    /// map that gives it relief. One pair a pattern, made once and shared by every head; one
    /// material a pattern and colour, which the palettes keep to a small number.
    /// </summary>
    internal static class HairTextures
    {
        private const int Size = 256;
        private static readonly Dictionary<ProceduralHair.Pattern, (Texture2D albedo, Texture2D normal)> made
            = new Dictionary<ProceduralHair.Pattern, (Texture2D, Texture2D)>();
        private static readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
        private static Shader lit;

        /// <summary>
        /// A material for a pattern in a colour. Given the skin and how <paramref name="bare"/> the
        /// hair is, the texture itself is painted - scalp in the partings of braids and rows, and
        /// showing through a buzz cut or a fade - since a colour laid over a grey texture can only
        /// darken it, never show skin.
        /// </summary>
        public static Material Material(ProceduralHair.Pattern pattern, Color colour, Color? skin = null, float bare = 0f, float smoothness = -1f)
        {
            bool parted = pattern == ProceduralHair.Pattern.Rows || pattern == ProceduralHair.Pattern.Parted;
            bool painted = skin.HasValue && (bare > 0f || parted);
            string key = pattern + "#" + ColorUtility.ToHtmlStringRGB(colour) + "#" + smoothness.ToString("0.00")
                         + (painted ? "#" + ColorUtility.ToHtmlStringRGB(skin.Value) + "#" + bare.ToString("0.00") : "");
            if (materials.TryGetValue(key, out var cached) && cached != null) return cached;
            if (lit == null) lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var (albedo, normal) = Textures(pattern);
            if (painted) albedo = Painted(pattern, colour, skin.Value, bare);
            var material = new Material(lit) { name = "Gamesim " + pattern + " " + ColorUtility.ToHtmlStringRGB(colour) };
            colour.a = 1f;
            if (painted) colour = Color.white;
            float gloss = smoothness >= 0f ? smoothness : pattern == ProceduralHair.Pattern.Waves ? .34f : .22f;
            material.SetTexture("_BaseMap", albedo);
            material.SetTexture("_MainTex", albedo);
            material.SetColor("_BaseColor", colour);
            material.SetColor("_Color", colour);
            material.SetTexture("_BumpMap", normal);
            material.SetFloat("_BumpScale", 1f);
            material.EnableKeyword("_NORMALMAP");
            material.SetFloat("_Smoothness", gloss);
            material.SetFloat("_Glossiness", gloss);
            material.SetFloat("_Metallic", 0f);
            materials[key] = material;
            return material;
        }

        private static readonly Dictionary<string, Texture2D> painted = new Dictionary<string, Texture2D>();

        /// <summary>The pattern painted in hair over skin: hair where the relief is high, scalp in the gaps and where the cut is close.</summary>
        private static Texture2D Painted(ProceduralHair.Pattern pattern, Color hair, Color skin, float bare)
        {
            string key = pattern + "#" + ColorUtility.ToHtmlStringRGB(hair) + "#" + ColorUtility.ToHtmlStringRGB(skin) + "#" + bare.ToString("0.00");
            if (painted.TryGetValue(key, out var found) && found != null) return found;
            bool parted = pattern == ProceduralHair.Pattern.Rows || pattern == ProceduralHair.Pattern.Parted;
            var colours = new Color32[Size * Size];
            // The scalp a shade deeper than the face, as it is under hair.
            var scalp = Color.Lerp(skin, skin * .78f, .5f);
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    float h = Height(pattern, x / (float)Size, y / (float)Size);
                    float cover = parted ? Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.02f, .28f, h)) : 1f;
                    cover *= Mathf.Clamp01(1f - bare + (h - .5f) * .9f);
                    var strand = hair * Mathf.Lerp(.55f, 1f, h);
                    var c = Color.Lerp(scalp, strand, cover);
                    colours[y * Size + x] = new Color32((byte)(Mathf.Clamp01(c.r) * 255f), (byte)(Mathf.Clamp01(c.g) * 255f), (byte)(Mathf.Clamp01(c.b) * 255f), 255);
                }
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, true, false) { name = "Gamesim hair " + key, wrapMode = TextureWrapMode.Repeat };
            texture.SetPixels32(colours);
            texture.Apply(true, true);
            painted[key] = texture;
            return texture;
        }

        private static (Texture2D, Texture2D) Textures(ProceduralHair.Pattern pattern)
        {
            if (made.TryGetValue(pattern, out var pair) && pair.albedo != null) return pair;
            var height = new float[Size * Size];
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                    height[y * Size + x] = Height(pattern, x / (float)Size, y / (float)Size);
            var albedo = new Texture2D(Size, Size, TextureFormat.RGBA32, true, false) { name = "Gamesim hair " + pattern, wrapMode = TextureWrapMode.Repeat };
            var normal = new Texture2D(Size, Size, TextureFormat.RGBA32, true, true) { name = "Gamesim hair normal " + pattern, wrapMode = TextureWrapMode.Repeat };
            var colours = new Color32[Size * Size];
            var normals = new Color32[Size * Size];
            float strength = pattern == ProceduralHair.Pattern.Stubble ? 1.5f : 3.5f;
            float floor = pattern == ProceduralHair.Pattern.Knit ? .62f : .42f;
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    float value = height[y * Size + x];
                    // Deep between the clumps, bright on them: the chosen colour multiplies this.
                    byte shade = (byte)Mathf.RoundToInt(255f * Mathf.Lerp(floor, 1f, value));
                    colours[y * Size + x] = new Color32(shade, shade, shade, 255);
                    float dx = height[y * Size + (x + 1) % Size] - height[y * Size + (x + Size - 1) % Size];
                    float dy = height[((y + 1) % Size) * Size + x] - height[((y + Size - 1) % Size) * Size + x];
                    var n = new Vector3(-dx * strength, -dy * strength, 1f).normalized;
                    normals[y * Size + x] = new Color32((byte)((n.x * .5f + .5f) * 255f), (byte)((n.y * .5f + .5f) * 255f), (byte)((n.z * .5f + .5f) * 255f), 255);
                }
            albedo.SetPixels32(colours); albedo.Apply(true, true);
            normal.SetPixels32(normals); normal.Apply(true, true);
            made[pattern] = (albedo, normal);
            return (albedo, normal);
        }

        /// <summary>The relief of a pattern at a point of its tile, 0 in the gaps to 1 on top.</summary>
        private static float Height(ProceduralHair.Pattern pattern, float u, float v)
        {
            const float Tau = Mathf.PI * 2f;
            switch (pattern)
            {
                case ProceduralHair.Pattern.Stubble:
                    return .5f + .5f * Mathf.Lerp(Tile(u, v, 64f), Tile(u, v, 128f), .5f);
                case ProceduralHair.Pattern.Crop:
                    return .45f + .55f * Mathf.Pow(Mathf.Lerp(Tile(u, v * 2f, 48f), Tile(u, v, 96f), .4f), 1.3f);
                case ProceduralHair.Pattern.Waves:
                {
                    // Brushed waves: ripples running across, a fine grain along them.
                    float ripple = .5f + .5f * Mathf.Sin((v * 8f + .15f * Mathf.Sin(u * Tau * 2f)) * Tau);
                    return Mathf.Lerp(ripple, Tile(u, v * 4f, 64f), .3f);
                }
                case ProceduralHair.Pattern.Coils:
                    return Mathf.Lerp(Cells(u, v, 14, 3), Tile(u, v, 96f), .2f);
                case ProceduralHair.Pattern.Rows:
                {
                    // Braided rows: a chevron of plaits along each row, a parting of scalp between.
                    float across = u * 9f;
                    float within = across - Mathf.Floor(across);
                    float part = Edge(0f, .14f, within) * Edge(1f, .86f, within);
                    float plait = .5f + .5f * Mathf.Sin((v * 24f + Mathf.Abs(within - .5f) * 3f) * Tau);
                    return part * Mathf.Lerp(.5f, 1f, plait);
                }
                case ProceduralHair.Pattern.Parted:
                {
                    // Sections of hair with partings between, bricked: the base braids and locs grow from.
                    float y = v * 8f;
                    float x = u * 8f + .5f * (Mathf.FloorToInt(y) % 2);
                    float fx = x - Mathf.Floor(x), fy = y - Mathf.Floor(y);
                    float part = Edge(0f, .12f, fx) * Edge(1f, .88f, fx) * Edge(0f, .12f, fy) * Edge(1f, .88f, fy);
                    return part * Mathf.Lerp(.65f, 1f, Tile(u, v * 4f, 64f));
                }
                case ProceduralHair.Pattern.Locs:
                    return .4f + .6f * Mathf.Lerp(Tile(u, v, 8f), Tile(u * 2f, v * 2f, 32f), .45f);
                case ProceduralHair.Pattern.Braid:
                {
                    float plait = .5f + .5f * Mathf.Sin((v * 3f + Mathf.Abs(u - .5f) * 1.5f) * Tau);
                    return Mathf.Lerp(.4f, 1f, plait) * Mathf.Lerp(.85f, 1f, Tile(u, v, 64f));
                }
                case ProceduralHair.Pattern.Knit:
                {
                    // A rib knit: raised ribs running up, a stitch texture along them.
                    float rib = .5f + .5f * Mathf.Sin(u * 16f * Tau);
                    float stitch = .5f + .5f * Mathf.Sin((v * 24f + (Mathf.FloorToInt(u * 32f) % 2) * .5f) * Tau);
                    return Mathf.Lerp(rib, stitch * rib, .35f);
                }
                default: return 1f;
            }
        }

        /// <summary>
        /// 0 below <paramref name="from"/>, 1 past <paramref name="to"/>, eased between: a soft edge.
        /// Not <c>Mathf.SmoothStep</c>, which eases between two VALUES - asked for an edge it
        /// returned at most the edge's width, and every parting came out almost all scalp.
        /// </summary>
        private static float Edge(float from, float to, float x) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(from, to, x));

        /// <summary>Tiling value noise at a whole-number frequency: the grain everything else is drawn over.</summary>
        private static float Tile(float u, float v, float frequency)
        {
            float x = u * frequency, y = v * frequency;
            int period = Mathf.Max(1, Mathf.RoundToInt(frequency));
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3f - 2f * fx); fy = fy * fy * (3f - 2f * fy);
            float a = Hash(x0, y0, period), b = Hash(x0 + 1, y0, period), c = Hash(x0, y0 + 1, period), d = Hash(x0 + 1, y0 + 1, period);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        private static float Hash(int x, int y, int period)
        {
            x = ((x % period) + period) % period; y = ((y % period) + period) % period;
            uint h = (uint)(x * 374761393 + y * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            return (h ^ (h >> 16)) / (float)uint.MaxValue;
        }

        /// <summary>Tiling cells, bright at their middles: clumps of coils, each curling round a darker core.</summary>
        private static float Cells(float u, float v, int count, int seed)
        {
            float best = 9f, second = 9f;
            float x = u * count, y = v * count;
            int cx = Mathf.FloorToInt(x), cy = Mathf.FloorToInt(y);
            for (int j = -1; j <= 1; j++)
                for (int i = -1; i <= 1; i++)
                {
                    int gx = cx + i, gy = cy + j;
                    float px = gx + Hash(gx * 3 + seed, gy * 5, count), py = gy + Hash(gx * 7, gy * 11 + seed, count);
                    float d = (x - px) * (x - px) + (y - py) * (y - py);
                    if (d < best) { second = best; best = d; } else if (d < second) second = d;
                }
            float edge = Mathf.Sqrt(second) - Mathf.Sqrt(best);
            float swirl = .5f + .5f * Mathf.Sin(Mathf.Sqrt(best) * 18f);
            return Mathf.Clamp01(edge * 2.2f) * Mathf.Lerp(.72f, 1f, swirl);
        }
    }
}
