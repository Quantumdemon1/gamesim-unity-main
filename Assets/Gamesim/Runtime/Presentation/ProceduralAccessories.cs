using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gamesim.Presentation
{
    /// <summary>
    /// What a houseguest wears besides clothes - glasses, a visor, earrings, a necklace, a bow tie,
    /// a cap - built in code and fitted to the body wearing it (see <see cref="HeadScan"/>).
    ///
    /// <para>UMA's library has no accessories at all, and the cast's photos are full of them: the
    /// glasses, the chrome visor, the hoops, the trucker cap. Each is made from a handful of
    /// primitives placed against the scanned head - lenses in front of the eyes, temples resting on
    /// the ears, hoops from the lobes, a chain draped round whatever the neck is wearing - so it
    /// fits every face and every proportion slider on both bodies.</para>
    ///
    /// <para>Presentation only: an accessory is a wardrobe item like a shirt, in one of four slots,
    /// saved by its id.</para>
    /// </summary>
    public static class ProceduralAccessories
    {
        public const string Prefix = "gs-acc-";
        public const string RootName = "Gamesim accessory";
        public static readonly string[] Slots = { "Eyewear", "Headwear", "Earrings", "Neckwear" };

        public enum Shape { FramedGlasses, RoundGlasses, Sunglasses, Visor, Studs, Hoops, Drops, Chain, Collar, Beads, BowTie, TruckerCap, Beanie }

        public sealed class Item
        {
            public readonly string Id, Label, Slot;
            public readonly Shape Shape;
            public readonly Color Main, Second;

            public Item(string id, string label, string slot, Shape shape, Color main, Color? second = null)
            { Id = id; Label = label; Slot = slot; Shape = shape; Main = main; Second = second ?? main; }
        }

        private static readonly Color Gold = new Color(1f, .77f, .34f), Silver = new Color(.9f, .9f, .92f), Chrome = new Color(.86f, .88f, .92f);
        private static readonly Color Frame = new Color(.045f, .045f, .05f), Lens = new Color(.035f, .04f, .05f);

        public static readonly Item[] Items =
        {
            new Item(Prefix + "glasses", "Black frames", "Eyewear", Shape.FramedGlasses, Frame),
            new Item(Prefix + "glasses-round", "Round wire frames", "Eyewear", Shape.RoundGlasses, Gold),
            new Item(Prefix + "sunglasses", "Sunglasses", "Eyewear", Shape.Sunglasses, Frame, Lens),
            new Item(Prefix + "visor", "Chrome visor", "Eyewear", Shape.Visor, Chrome),
            new Item(Prefix + "cap", "Trucker cap", "Headwear", Shape.TruckerCap, new Color(.06f, .06f, .07f)),
            new Item(Prefix + "beanie", "Beanie", "Headwear", Shape.Beanie, new Color(.24f, .25f, .27f)),
            new Item(Prefix + "studs", "Gold studs", "Earrings", Shape.Studs, Gold),
            new Item(Prefix + "hoops", "Gold hoops", "Earrings", Shape.Hoops, Gold),
            new Item(Prefix + "hoops-silver", "Silver hoops", "Earrings", Shape.Hoops, Silver),
            new Item(Prefix + "drops", "Drop earrings", "Earrings", Shape.Drops, Silver, new Color(.82f, .86f, .95f)),
            new Item(Prefix + "chain", "Gold chain", "Neckwear", Shape.Chain, Gold),
            new Item(Prefix + "collar", "Statement collar", "Neckwear", Shape.Collar, Gold),
            new Item(Prefix + "beads", "Beaded necklace", "Neckwear", Shape.Beads, new Color(.95f, .55f, .45f), new Color(.3f, .72f, .7f)),
            new Item(Prefix + "bowtie", "Red bow tie", "Neckwear", Shape.BowTie, new Color(.66f, .05f, .08f)),
        };

        public static bool IsProcedural(string id) => id != null && id.StartsWith(Prefix, StringComparison.Ordinal);

        public static Item Find(string id)
        {
            foreach (var item in Items) if (item.Id == id) return item;
            return null;
        }

        /// <summary>Takes every accessory off a body.</summary>
        public static void Remove(GameObject body) => GrownPiece.RemoveAll(body, RootName);

        /// <summary>
        /// Puts <paramref name="ids"/> on a built humanoid body, replacing what it wore. Returns how
        /// many were fitted. Dressed after any hair is grown (see <see cref="ProceduralHair.Grow"/>),
        /// a cap or a beanie goes over that hair.
        /// </summary>
        public static int Dress(GameObject body, IEnumerable<string> ids)
        {
            Remove(body);
            if (body == null || ids == null) return 0;
            HeadScan scan = null;
            int fitted = 0;
            foreach (string id in ids)
            {
                var item = Find(id);
                if (item == null) continue;
                scan = scan ?? HeadScan.Read(body);
                if (scan == null) return fitted;
                // Neckwear hangs from the chest, so it is built in the body's axes rather than the
                // head's: a head turned when the scan was taken would otherwise turn it too.
                bool neck = item.Slot == "Neckwear" && scan.ChestBone != null;
                var frame = neck ? scan.NeckInBodyAxes() : scan;
                var parts = new MeshParts();
                var materials = Build(frame, item, parts);
                if (materials != null && GrownPiece.Attach(frame, neck ? scan.ChestBone : scan.Head, RootName, item.Id, parts, materials) != null) fitted++;
            }
            return fitted;
        }

        private static Material[] Build(HeadScan scan, Item item, MeshParts parts)
        {
            switch (item.Shape)
            {
                case Shape.FramedGlasses:
                case Shape.RoundGlasses:
                case Shape.Sunglasses:
                    Glasses(scan, item, parts);
                    return new[] { Solid(item.Main, item.Shape == Shape.RoundGlasses ? .8f : .6f, item.Shape == Shape.RoundGlasses ? 1f : 0f), Solid(item.Second, .95f, .3f) };
                case Shape.Visor:
                    Visor(scan, parts);
                    return new[] { Solid(item.Main, .93f, 1f) };
                case Shape.Studs:
                case Shape.Hoops:
                case Shape.Drops:
                    Earrings(scan, item, parts);
                    return new[] { Solid(item.Main, .8f, 1f), Solid(item.Second, .9f, .2f) };
                case Shape.Chain:
                case Shape.Collar:
                case Shape.Beads:
                    if (!scan.HasTorso) return null;
                    Necklace(scan, item, parts);
                    return item.Shape == Shape.Beads
                        ? new[] { Solid(item.Main, .55f, 0f), Solid(item.Second, .55f, 0f) }
                        : new[] { Solid(item.Main, .8f, 1f) };
                case Shape.BowTie:
                    if (!scan.HasTorso) return null;
                    BowTie(scan, parts);
                    return new[] { Solid(item.Main, .42f, 0f) };
                case Shape.TruckerCap:
                    Cap(scan, parts);
                    return new[] { Solid(item.Main, .12f, 0f) };
                case Shape.Beanie:
                    Beanie(scan, parts);
                    return new[] { HairTextures.Material(ProceduralHair.Pattern.Knit, item.Main, smoothness: .08f) };
            }
            return null;
        }

        // ---- Eyewear ------------------------------------------------------------------------

        /// <summary>How far forward the face comes round an eye: brow, lids and cheek.</summary>
        private static float FrontAt(HeadScan scan, Vector3 eye)
        {
            float front = eye.z + .012f;
            foreach (var p in scan.Points)
                if (Mathf.Abs(p.x - eye.x) < .016f && Mathf.Abs(p.y - eye.y) < .014f) front = Mathf.Max(front, p.z);
            return front;
        }

        private static void Glasses(HeadScan scan, Item item, MeshParts parts)
        {
            var left = scan.LeftEye; var right = scan.RightEye;
            float ipd = Mathf.Max(.05f, right.x - left.x);
            float front = Mathf.Max(FrontAt(scan, left), FrontAt(scan, right)) + .009f;
            bool round = item.Shape == Shape.RoundGlasses, sun = item.Shape == Shape.Sunglasses;
            float halfWidth = ipd * (round ? .35f : sun ? .43f : .41f);
            float halfHeight = ipd * (round ? .35f : sun ? .31f : .27f);
            float exponent = round ? 2f : sun ? 3f : 4.5f;
            float rim = round ? .0009f : .0021f;
            float wrap = sun ? .012f : .005f;
            float y = scan.EyeY - .002f;
            const int around = 40;
            var hinges = new Vector3[2];
            var inner = new Vector3[2];
            for (int s = 0; s < 2; s++)
            {
                int side = s == 0 ? -1 : 1;
                var eye = s == 0 ? left : right;
                var centre = new Vector3(eye.x + side * ipd * .02f, y, front);
                var outline = new Vector3[around];
                for (int i = 0; i < around; i++)
                {
                    float a = i / (float)around * Mathf.PI * 2f;
                    float c = Mathf.Cos(a), sn = Mathf.Sin(a);
                    float x = centre.x + halfWidth * Mathf.Sign(c) * Mathf.Pow(Mathf.Abs(c), 2f / exponent);
                    float yy = centre.y + halfHeight * Mathf.Sign(sn) * Mathf.Pow(Mathf.Abs(sn), 2f / exponent);
                    float outer = Mathf.Clamp01(((x - centre.x) * side / halfWidth + 1f) * .5f);
                    outline[i] = new Vector3(x, yy, front - wrap * outer * outer);
                }
                parts.Tube(0, outline, rim, 6, closed: true);
                if (sun)
                {
                    int middle = parts.Vertex(centre + Vector3.forward * .001f, Vector3.forward, new Vector2(.5f, .5f));
                    for (int i = 0; i < around; i++)
                    {
                        int a = parts.Vertex(outline[i], Vector3.forward, Vector2.zero);
                        int b = parts.Vertex(outline[(i + 1) % around], Vector3.forward, Vector2.zero);
                        parts.Triangle(1, middle, a, b);
                    }
                }
                hinges[s] = new Vector3(centre.x + side * halfWidth, centre.y + halfHeight * .45f, front - wrap);
                inner[s] = new Vector3(centre.x - side * halfWidth * .97f, centre.y + halfHeight * .3f, front);
            }
            // The bridge, arched a little over the nose.
            var bridge = new Vector3[9];
            var top = new Vector3((inner[0].x + inner[1].x) * .5f, inner[0].y + halfHeight * .22f, front + .002f);
            for (int i = 0; i < bridge.Length; i++)
            {
                float t = i / (float)(bridge.Length - 1);
                bridge[i] = (1 - t) * (1 - t) * inner[0] + 2 * (1 - t) * t * top + t * t * inner[1];
            }
            parts.Tube(0, bridge, rim * .9f, 6);
            // The temples: back from the hinge, clear of the skin, over the top of the ear and down behind it.
            for (int s = 0; s < 2; s++)
            {
                int side = s == 0 ? -1 : 1;
                var ear = s == 0 ? scan.LeftEarTop : scan.RightEarTop;
                var hinge = hinges[s];
                // Only as far out as the face itself: pushed clear of the skin, not out to the skull's width.
                var keys = new[]
                {
                    hinge,
                    hinge + new Vector3(side * .002f, 0f, -.008f),
                    new Vector3(hinge.x + side * .002f, Mathf.Lerp(hinge.y, ear.y + .004f, .5f), Mathf.Lerp(hinge.z, ear.z, .5f)),
                    new Vector3(ear.x + side * .003f, ear.y + .004f, ear.z),
                    new Vector3(ear.x + side * .001f, ear.y - .016f, ear.z - .016f),
                };
                var path = Spline(keys, 6);
                for (int i = 1; i < path.Count; i++) path[i] = OutsideSkin(scan, path[i], rim + .002f);
                parts.Tube(0, path, rim * .85f, 6);
            }
        }

        /// <summary>A band of chrome round the face at eye height, from temple to temple.</summary>
        private static void Visor(HeadScan scan, MeshParts parts)
        {
            const int columns = 29, rows = 5;
            var axis = scan.Centre;
            var radius = new float[columns];
            for (int c = 0; c < columns; c++)
            {
                float degrees = Mathf.Lerp(-82f, 82f, c / (float)(columns - 1));
                radius[c] = scan.SkinReach(axis, scan.EyeY + .002f, degrees, .022f, 5f) + .014f;
            }
            for (int pass = 0; pass < 3; pass++)
            {
                var soft = (float[])radius.Clone();
                for (int c = 1; c < columns - 1; c++) soft[c] = Mathf.Max(radius[c], (radius[c - 1] + radius[c] * 2f + radius[c + 1]) * .25f);
                radius = soft;
            }
            var grid = new Vector3[rows, columns];
            for (int r = 0; r < rows; r++)
            {
                float t = r / (float)(rows - 1);
                float y = Mathf.Lerp(scan.EyeY - .02f, scan.EyeY + .024f, t);
                float bulge = .004f * (1f - (2f * t - 1f) * (2f * t - 1f));
                for (int c = 0; c < columns; c++)
                {
                    float a = Mathf.Lerp(-82f, 82f, c / (float)(columns - 1)) * Mathf.Deg2Rad;
                    float rr = radius[c] + bulge;
                    grid[r, c] = new Vector3(axis.x + Mathf.Sin(a) * rr, y, axis.z + Mathf.Cos(a) * rr);
                }
            }
            parts.Sheet(0, grid, new Vector3(axis.x, scan.EyeY, axis.z));
            for (int r = 0; r < rows; r += rows - 1)
            {
                var edge = new Vector3[columns];
                for (int c = 0; c < columns; c++) edge[c] = grid[r, c];
                parts.Tube(0, edge, .0016f, 6);
            }
        }

        // ---- Earrings -----------------------------------------------------------------------

        private static void Earrings(HeadScan scan, Item item, MeshParts parts)
        {
            for (int s = 0; s < 2; s++)
            {
                int side = s == 0 ? -1 : 1;
                var lobe = s == 0 ? scan.LeftLobe : scan.RightLobe;
                switch (item.Shape)
                {
                    case Shape.Studs:
                        parts.Ellipsoid(0, lobe + new Vector3(side * .0018f, .002f, 0f), Vector3.one * .0026f, 6, 10);
                        break;
                    case Shape.Hoops:
                    {
                        const float hoop = .012f;
                        var centre = lobe + new Vector3(side * .0028f, -hoop + .003f, 0f);
                        var ring = new Vector3[28];
                        for (int i = 0; i < ring.Length; i++)
                        {
                            float a = i / (float)ring.Length * Mathf.PI * 2f;
                            ring[i] = centre + new Vector3(0f, Mathf.Cos(a) * hoop, Mathf.Sin(a) * hoop);
                        }
                        parts.Tube(0, ring, .0012f, 6, closed: true, up: Vector3.right);
                        break;
                    }
                    case Shape.Drops:
                    {
                        var top = lobe + new Vector3(side * .0022f, .001f, 0f);
                        var bottom = top + new Vector3(0f, -.024f, 0f);
                        parts.Ellipsoid(0, top, Vector3.one * .0018f, 5, 8);
                        parts.Tube(0, new[] { top, Vector3.Lerp(top, bottom, .5f), bottom }, .0005f, 5);
                        parts.Ellipsoid(1, bottom + new Vector3(0f, -.005f, 0f), new Vector3(.0034f, .0058f, .0034f), 7, 10);
                        break;
                    }
                }
            }
        }

        // ---- Neckwear -----------------------------------------------------------------------
        // Built in the body's axes (see HeadScan.NeckInBodyAxes): only the neck is read here.

        /// <summary>
        /// A closed loop round the neck, high at the back and draped lower at the front by
        /// <paramref name="drop"/>, lying on whatever the collar is wearing.
        /// </summary>
        private static Vector3[] Drape(HeadScan scan, float drop, float clearance, int count = 72)
        {
            var neck = scan.Neck;
            var axis = new Vector3(neck.x, 0f, neck.z);
            var radius = new float[count];
            var heights = new float[count];
            for (int i = 0; i < count; i++)
            {
                float degrees = i / (float)count * 360f;
                float front = (1f + Mathf.Cos(degrees * Mathf.Deg2Rad)) * .5f;
                heights[i] = neck.y + .012f - drop * front * front;
                radius[i] = Mathf.Max(.055f, scan.CollarReach(axis, heights[i], degrees, .01f, 8f)) + clearance;
            }
            for (int pass = 0; pass < 4; pass++)
            {
                var soft = (float[])radius.Clone();
                for (int i = 0; i < count; i++)
                    soft[i] = Mathf.Max(radius[i], (radius[(i - 1 + count) % count] + radius[i] * 2f + radius[(i + 1) % count]) * .25f);
                radius = soft;
            }
            var loop = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                float a = i / (float)count * Mathf.PI * 2f;
                loop[i] = new Vector3(axis.x + Mathf.Sin(a) * radius[i], heights[i], axis.z + Mathf.Cos(a) * radius[i]);
            }
            return loop;
        }

        private static void Necklace(HeadScan scan, Item item, MeshParts parts)
        {
            switch (item.Shape)
            {
                case Shape.Chain:
                    parts.Tube(0, Drape(scan, .055f, .0025f), .0012f, 6, closed: true);
                    break;
                case Shape.Collar:
                {
                    var loop = Drape(scan, .03f, .006f, 60);
                    parts.Tube(0, loop, .0022f, 6, closed: true);
                    // Graduated beads across the front, largest at the middle.
                    for (int i = 0; i < loop.Length; i++)
                    {
                        float degrees = i / (float)loop.Length * 360f;
                        float fromFront = Mathf.Abs(Mathf.DeltaAngle(degrees, 0f));
                        if (fromFront > 75f) continue;
                        float size = Mathf.Lerp(.0085f, .004f, fromFront / 75f);
                        parts.Ellipsoid(0, loop[i] + Vector3.down * size * .6f, new Vector3(size * .8f, size, size * .6f), 6, 10);
                    }
                    break;
                }
                case Shape.Beads:
                {
                    var loop = Drape(scan, .065f, .004f, 96);
                    for (int i = 0; i < loop.Length; i++)
                        parts.Ellipsoid(i % 3 == 0 ? 1 : 0, loop[i], Vector3.one * .0034f, 5, 8);
                    break;
                }
            }
        }

        /// <summary>A bow at the front of the collar: a knot and two wings flaring from it.</summary>
        private static void BowTie(HeadScan scan, MeshParts parts)
        {
            var neck = scan.Neck;
            float y = neck.y + .016f;
            var axis = new Vector3(neck.x, 0f, neck.z);
            float reach = Mathf.Max(.05f, scan.CollarReach(axis, y, 0f, .012f, 10f));
            var knot = new Vector3(neck.x, y, neck.z + reach + .007f);
            Box(parts, knot, new Vector3(.013f, .012f, .009f));
            for (int s = -1; s <= 1; s += 2)
            {
                float innerX = knot.x + s * .006f, outerX = knot.x + s * .05f, back = knot.z - .004f;
                parts.Hexahedron(0, new[]
                {
                    new Vector3(innerX, y - .0055f, knot.z - .004f), new Vector3(innerX, y + .0055f, knot.z - .004f),
                    new Vector3(innerX, y + .0055f, knot.z + .003f), new Vector3(innerX, y - .0055f, knot.z + .003f),
                    new Vector3(outerX, y - .019f, back - .004f), new Vector3(outerX, y + .019f, back - .004f),
                    new Vector3(outerX, y + .019f, back + .002f), new Vector3(outerX, y - .019f, back + .002f),
                });
            }
        }

        private static void Box(MeshParts parts, Vector3 centre, Vector3 size)
        {
            var h = size * .5f;
            parts.Hexahedron(0, new[]
            {
                centre + new Vector3(-h.x, -h.y, -h.z), centre + new Vector3(-h.x, h.y, -h.z), centre + new Vector3(-h.x, h.y, h.z), centre + new Vector3(-h.x, -h.y, h.z),
                centre + new Vector3(h.x, -h.y, -h.z), centre + new Vector3(h.x, h.y, -h.z), centre + new Vector3(h.x, h.y, h.z), centre + new Vector3(h.x, -h.y, h.z),
            });
        }

        // ---- Headwear -----------------------------------------------------------------------

        /// <summary>
        /// A dome over the head and everything on it, from a band that runs <paramref name="front"/>,
        /// <paramref name="side"/> and <paramref name="back"/> (shares of eyes-to-crown above the
        /// eyes) up to the crown, never closer than <paramref name="room"/> to the head or its hair -
        /// grown hair included, which is fitted first (see <see cref="HeadScan.EnvelopeRadius"/>).
        /// </summary>
        private static Vector3[,] Dome(HeadScan scan, float front, float side, float back, float room, int rows, int columns, out Vector2[,] uvs)
        {
            float h = scan.Height;
            var centre = scan.Centre;
            // Over the highest thing on the head too: the envelope straight up is read between its
            // directions, and an afro's peak behind the crown can stand a little proud of it.
            float topY = Mathf.Max(centre.y + scan.EnvelopeRadius(Vector3.up), scan.CarriedTopY) + room;
            var grid = new Vector3[rows, columns];
            uvs = new Vector2[rows, columns];
            for (int c = 0; c < columns; c++)
            {
                float degrees = Mathf.Lerp(-180f, 180f, c / (float)(columns - 1));
                float a = Mathf.Abs(degrees);
                float rise = a <= 90f ? Mathf.Lerp(front, side, Mathf.SmoothStep(0f, 1f, a / 90f)) : Mathf.Lerp(side, back, Mathf.SmoothStep(0f, 1f, (a - 90f) / 90f));
                float baseY = scan.EyeY + rise * h;
                var bearing = new Vector3(Mathf.Sin(degrees * Mathf.Deg2Rad), 0f, Mathf.Cos(degrees * Mathf.Deg2Rad));
                float baseRadius = Mathf.Max(scan.SkinReach(centre, baseY, degrees, .01f, 7f), HorizontalEnvelope(scan, baseY, bearing)) + room;
                for (int r = 0; r < rows; r++)
                {
                    float t = r / (float)(rows - 1);
                    float y = Mathf.Lerp(baseY, topY, Mathf.Sin(t * Mathf.PI * .5f));
                    // Clamped: at the crown the cosine comes out a hair below zero, and its power is NaN.
                    float horizontal = baseRadius * Mathf.Pow(Mathf.Max(0f, Mathf.Cos(t * Mathf.PI * .5f)), .75f);
                    var p = new Vector3(centre.x + bearing.x * horizontal, y, centre.z + bearing.z * horizontal);
                    // Over the hair as well as the head: never inside what the head carries.
                    var dir = p - centre;
                    float need = scan.EnvelopeRadius(dir) + room;
                    if (dir.magnitude < need) p = centre + dir.normalized * need;
                    grid[r, c] = p;
                    uvs[r, c] = new Vector2(degrees / 360f * 2f * Mathf.PI * baseRadius, t * (topY - baseY) * 1.6f) * ProceduralHair.Density(ProceduralHair.Pattern.Knit);
                }
            }
            // Smoothed round each ring, so the dome reads as cloth rather than the lumps under it.
            for (int pass = 0; pass < 3; pass++)
                for (int r = 0; r < rows; r++)
                {
                    var ring = new Vector3[columns];
                    for (int c = 0; c < columns; c++) ring[c] = grid[r, c];
                    for (int c = 0; c < columns; c++)
                    {
                        int prev = c == 0 ? columns - 2 : c - 1, next = c == columns - 1 ? 1 : c + 1;
                        var soft = (ring[prev] + ring[c] * 2f + ring[next]) * .25f;
                        var dir = soft - centre;
                        float need = scan.EnvelopeRadius(dir) + room;
                        grid[r, c] = dir.magnitude < need ? centre + dir.normalized * need : soft;
                    }
                }
            // Close the crown on one point.
            var crown = Vector3.zero;
            for (int c = 0; c < columns; c++) crown += grid[rows - 1, c];
            crown /= columns;
            for (int c = 0; c < columns; c++) grid[rows - 1, c] = crown;
            return grid;
        }

        private static float HorizontalEnvelope(HeadScan scan, float y, Vector3 bearing)
        {
            // Walk out along the bearing at this height until past the envelope.
            float radius = .02f;
            for (int step = 0; step < 40; step++, radius += .004f)
            {
                var p = new Vector3(bearing.x * radius, y - scan.Centre.y, bearing.z * radius);
                if (p.magnitude > scan.EnvelopeRadius(p)) return radius;
            }
            return radius;
        }

        private static void Cap(HeadScan scan, MeshParts parts)
        {
            const int rows = 10, columns = 37;
            var grid = Dome(scan, .52f, .3f, .04f, .006f, rows, columns, out _);
            parts.Sheet(0, grid, scan.Centre);
            // The brim, out over the brow and dipping at its edges.
            const int brimColumns = 27, brimRows = 4;
            var brim = new Vector3[brimRows, brimColumns];
            float h = scan.Height;
            for (int c = 0; c < brimColumns; c++)
            {
                float degrees = Mathf.Lerp(-68f, 68f, c / (float)(brimColumns - 1));
                // The dome's own base at this bearing, interpolated from its columns.
                float column = (degrees + 180f) / 360f * (columns - 1);
                int c0 = Mathf.FloorToInt(column), c1 = Mathf.Min(c0 + 1, columns - 1);
                var start = Vector3.Lerp(grid[0, c0], grid[0, c1], column - c0);
                float along = Mathf.Pow(Mathf.Max(0f, Mathf.Cos(degrees / 68f * Mathf.PI * .5f)), .55f) * .072f;
                var outward = new Vector3(Mathf.Sin(degrees * Mathf.Deg2Rad) * .55f, 0f, Mathf.Cos(degrees * Mathf.Deg2Rad)).normalized;
                for (int r = 0; r < brimRows; r++)
                {
                    float t = r / (float)(brimRows - 1);
                    float dip = -.014f * t * (along / .072f) - .008f * (degrees / 68f) * (degrees / 68f) * t;
                    brim[r, c] = start + outward * along * t + Vector3.up * dip;
                }
            }
            parts.Sheet(0, brim, scan.Centre - Vector3.up * 2f);
            var rim = new Vector3[brimColumns];
            for (int c = 0; c < brimColumns; c++) rim[c] = brim[brimRows - 1, c];
            parts.Tube(0, rim, .0022f, 6);
            parts.Ellipsoid(0, grid[rows - 1, 0] + Vector3.up * .001f, new Vector3(.006f, .003f, .006f), 5, 10);
        }

        private static void Beanie(HeadScan scan, MeshParts parts)
        {
            const int rows = 11, columns = 41;
            var grid = Dome(scan, .38f, .06f, -.14f, .01f, rows, columns, out var uvs);
            // A turned-up cuff round the bottom.
            var centre = scan.Centre;
            for (int r = 0; r < 3; r++)
                for (int c = 0; c < columns; c++)
                {
                    var dir = new Vector3(grid[r, c].x - centre.x, 0f, grid[r, c].z - centre.z).normalized;
                    grid[r, c] += dir * .005f;
                }
            parts.Sheet(0, grid, centre, uvs);
        }

        // ---- Shared -------------------------------------------------------------------------

        /// <summary>A point pushed out from the head's middle until it clears the skin by <paramref name="room"/>.</summary>
        private static Vector3 OutsideSkin(HeadScan scan, Vector3 point, float room)
        {
            var axis = scan.Centre;
            var flat = new Vector2(point.x - axis.x, point.z - axis.z);
            float degrees = Mathf.Atan2(flat.x, flat.y) * Mathf.Rad2Deg;
            float reach = scan.SkinReach(axis, point.y, degrees, .006f, 4f) + room;
            if (flat.magnitude >= reach || flat.sqrMagnitude < 1e-8f) return point;
            flat = flat.normalized * reach;
            return new Vector3(axis.x + flat.x, point.y, axis.z + flat.y);
        }

        /// <summary>A Catmull-Rom curve through the keys, <paramref name="steps"/> points a span.</summary>
        private static List<Vector3> Spline(IList<Vector3> keys, int steps)
        {
            var path = new List<Vector3>();
            for (int i = 0; i < keys.Count - 1; i++)
            {
                var p0 = keys[Mathf.Max(0, i - 1)]; var p1 = keys[i]; var p2 = keys[i + 1]; var p3 = keys[Mathf.Min(keys.Count - 1, i + 2)];
                for (int s = 0; s < steps; s++)
                {
                    float t = s / (float)steps, t2 = t * t, t3 = t2 * t;
                    path.Add(.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3));
                }
            }
            path.Add(keys[keys.Count - 1]);
            return path;
        }

        private static readonly Dictionary<string, Material> solids = new Dictionary<string, Material>();
        private static Shader lit;

        /// <summary>A plain lit material, made once for each colour and finish and shared.</summary>
        internal static Material Solid(Color colour, float smoothness, float metallic)
        {
            string key = ColorUtility.ToHtmlStringRGB(colour) + "#" + smoothness.ToString("0.00") + "#" + metallic.ToString("0.00");
            if (solids.TryGetValue(key, out var cached) && cached != null) return cached;
            if (lit == null) lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var material = new Material(lit) { name = "Gamesim accessory " + key };
            colour.a = 1f;
            material.SetColor("_BaseColor", colour);
            material.SetColor("_Color", colour);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Glossiness", smoothness);
            material.SetFloat("_Metallic", metallic);
            solids[key] = material;
            return material;
        }
    }
}
