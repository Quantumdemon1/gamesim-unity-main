using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Gamesim.Uma.Editor
{
    public static partial class HouseGarmentAuthoring
    {
        private static Pattern Panels(Surface surface, Style style)
        {
            const int columns = 24, rows = 20, armStart = 15, neckLeft = 8, neckRight = 16;
            var pattern = new Pattern();
            Vector3 hips = surface.Bone(HumanBodyBones.Hips);
            Vector3 left = surface.Bone(HumanBodyBones.LeftUpperArm), right = surface.Bone(HumanBodyBones.RightUpperArm);
            float center = hips.x, half = Mathf.Abs(left.x - right.x) * .5f;
            pattern.shoulder = (left.y + right.y) * .5f;
            pattern.hem = hips.y + (pattern.shoulder - hips.y) * .03f;
            pattern.armpit = pattern.shoulder - (pattern.shoulder - pattern.hem) * .23f;
            Require(half > .08f && pattern.shoulder - pattern.hem > .15f, "Unexpected humanoid torso landmarks.");
            UpperEdge edge = UpperEdge.From(surface, style, half, pattern.shoulder - pattern.hem);
            int[,] front = new int[rows + 1, columns + 1], back = new int[rows + 1, columns + 1];
            for (int j = 0; j <= rows; j++)
            {
                float v = j / (float)rows;
                float yAtCenter = Mathf.Lerp(pattern.hem, pattern.shoulder, v);
                float width = TorsoHalfWidth(surface, yAtCenter, center, half);
                width = Mathf.Lerp(width, half, Mathf.InverseLerp(.7f, 1f, v));
                float upper = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.7f, 1f, v));
                for (int i = 0; i <= columns; i++)
                {
                    float u = i / (float)columns, across = u * 2f - 1f;
                    float neck = Mathf.Clamp01(1f - Mathf.Abs(across) * 3f);
                    // UVs and naked-body correspondences survive measured wardrobe-layer displacement.
                    float x = center + across * width;
                    float yFront = Mathf.Lerp(pattern.hem, pattern.shoulder + neck * .012f, v);
                    float yBack = Mathf.Lerp(pattern.hem, pattern.shoulder + neck * .028f, v);
                    Hit f, b;
                    if (j <= 14)
                    {
                        f = surface.FrontBack(x, yFront, true);
                        b = surface.FrontBack(x, yBack, false);
                    }
                    else
                    {
                        edge.At(across, out float upperX, out float frontY, out float backY);
                        x = Mathf.Lerp(x, upperX, upper);
                        yFront = Mathf.Lerp(yFront, frontY, upper);
                        yBack = Mathf.Lerp(yBack, backY, upper);
                        // Upper panels include clavicle and proximal-arm surface, with no nearest-torso fallback.
                        Require(surface.Ray(new Vector3(x, yFront, 3f), Vector3.back, out f),
                            "Unsupported upper front panel at row " + j + ", column " + i + ".");
                        Require(surface.Ray(new Vector3(x, yBack, -3f), Vector3.forward, out b),
                            "Unsupported upper back panel at row " + j + ", column " + i + ".");
                    }
                    Vector3 frontPoint = LowerPoint(surface, f.position + f.normal * style.clearance, style.clearance, (pattern.shoulder - pattern.hem) / rows);
                    Vector3 backPoint = LowerPoint(surface, b.position + b.normal * style.clearance, style.clearance, (pattern.shoulder - pattern.hem) / rows);
                    front[j, i] = pattern.Add(frontPoint, new Vector2(.02f + u * .44f, .04f + v * .62f), f);
                    back[j, i] = pattern.Add(backPoint, new Vector2(.50f + u * .44f, .04f + v * .62f), b);
                    if (j > 0 && i > 0)
                    {
                        pattern.Quad(front[j - 1, i - 1], front[j - 1, i], front[j, i], front[j, i - 1], Vector3.forward);
                        pattern.Quad(back[j - 1, i - 1], back[j - 1, i], back[j, i], back[j, i - 1], Vector3.back);
                    }
                }
            }
            int[,] flankEnd = new int[2, 7];
            for (int side = 0; side < 2; side++)
            {
                const int divisions = 6;
                int column = side == 0 ? 0 : columns;
                int[,] strip = new int[armStart + 1, divisions + 1];
                for (int j = 0; j <= armStart; j++) for (int k = 0; k <= divisions; k++)
                {
                    float across = k / (float)divisions;
                    Hit frontHit = pattern.bodyHits[front[j, column]], backHit = pattern.bodyHits[back[j, column]];
                    Vector3 desired = Vector3.Lerp(frontHit.position + frontHit.normal * style.clearance,
                        backHit.position + backHit.normal * style.clearance, across);
                    // Exact shared seam endpoints prevent duplicate projection from opening a crack.
                    if (k == 0) strip[j, k] = front[j, column];
                    else if (k == divisions) strip[j, k] = back[j, column];
                    else
                    {
                        Hit hit = surface.Closest(desired, false);
                        Vector3 point = LowerPoint(surface, hit.position + hit.normal * style.clearance, style.clearance, (pattern.shoulder - pattern.hem) / rows);
                        strip[j, k] = pattern.Add(point, new Vector2(.95f + across * .04f, .04f + j / (float)rows * .62f), hit);
                    }
                    if (j == armStart) flankEnd[side, k] = strip[j, k];
                    if (j > 0 && k > 0) pattern.Quad(strip[j - 1, k - 1], strip[j - 1, k], strip[j, k], strip[j, k - 1], side == 0 ? Vector3.left : Vector3.right);
                }
            }
            // A subdivided yoke follows the rounded shoulder crown, rather than cutting through it.
            const int shoulderDivisions = 6;
            int[,] yoke = new int[columns + 1, shoulderDivisions + 1];
            for (int i = 0; i <= columns; i++)
            {
                if (i > neckLeft && i < neckRight) continue;
                for (int k = 0; k <= shoulderDivisions; k++)
                {
                    if (k == 0) yoke[i, k] = front[rows, i];
                    else if (k == shoulderDivisions) yoke[i, k] = back[rows, i];
                    else
                    {
                        Hit frontHit = pattern.bodyHits[front[rows, i]], backHit = pattern.bodyHits[back[rows, i]];
                        Vector3 desired = Vector3.Lerp(frontHit.position + frontHit.normal * style.clearance,
                            backHit.position + backHit.normal * style.clearance, k / (float)shoulderDivisions);
                        Hit hit = surface.Closest(desired);
                        Require(hit.distance < .08f * .08f, "Unsupported local shoulder crown.");
                        Vector3 point = LowerPoint(surface, hit.position + hit.normal * style.clearance, style.clearance, (pattern.shoulder - pattern.hem) / rows);
                        yoke[i, k] = pattern.Add(point,
                            new Vector2(.95f + k / (float)shoulderDivisions * .04f, .68f + i / (float)columns * .27f), hit);
                    }
                    if (i > 0 && (i <= neckLeft || i > neckRight) && k > 0)
                        pattern.Quad(yoke[i - 1, k - 1], yoke[i, k - 1], yoke[i, k], yoke[i - 1, k], Vector3.up);
                }
            }
            // Include the round, subdivided side arcs in both collar and armhole boundaries.
            var neckLoop = new List<int>();
            for (int i = neckLeft; i <= neckRight; i++) neckLoop.Add(front[rows, i]);
            for (int k = 1; k <= shoulderDivisions; k++) neckLoop.Add(yoke[neckRight, k]);
            for (int i = neckRight - 1; i >= neckLeft; i--) neckLoop.Add(back[rows, i]);
            for (int k = shoulderDivisions - 1; k > 0; k--) neckLoop.Add(yoke[neckLeft, k]);
            neckLoop = DistinctLoop(pattern, neckLoop);
            pattern.neckBoundary = neckLoop;
            Binding(pattern, neckLoop, CollarBindingWidth, new Rect(.54f, .7f, .4f, .06f));
            for (int side = 0; side < 2; side++)
            {
                int column = side == 0 ? 0 : columns;
                var loop = new List<int>();
                for (int j = armStart; j <= rows; j++) loop.Add(front[j, column]);
                for (int k = 1; k <= shoulderDivisions; k++) loop.Add(yoke[column, k]);
                for (int j = rows - 1; j >= armStart; j--) loop.Add(back[j, column]);
                for (int k = 5; k > 0; k--) loop.Add(flankEnd[side, k]);
                loop = DistinctLoop(pattern, loop);
                pattern.armBoundaries.Add(loop);
                bool anatomicalLeft = Mathf.Abs(edge.OuterX(side) - left.x) < Mathf.Abs(edge.OuterX(side) - right.x);
                if (style.sleeves) Sleeve(pattern, surface, loop, anatomicalLeft, style.clearance);
                else Binding(pattern, loop, ArmholeBindingWidth, new Rect(.54f, .8f + side * .07f, .4f, .05f));
            }
            return pattern;
        }

        private static Vector3 LowerPoint(Surface surface, Vector3 point, float clearance, float rowHeight)
        {
            if (surface.lowerLayer != null) point = surface.lowerLayer.Enclose(point, clearance, rowHeight);
            // Only existing torso/flank/yoke cloth is fitted. No arm-ring expansion,
            // suppression, mask widening or faces across a collar/vest opening.
            return surface.upperLayer == null ? point : surface.upperLayer.Enclose(point, clearance, rowHeight);
        }

        /// <summary>Authoring-only bounded shell of actual compatible layer meshes; never changes the worn layer.</summary>
        private sealed class LowerLayerEnvelope
        {
            private const float SectionHeight = .025f;
            private const int MaximumTriangles = 50000, MaximumMemberships = 250000, MaximumTriangleTests = 12000000;
            private struct Facet { public Vector3 a, b, c; public float lo, hi; }
            private readonly List<Facet> facets = new List<Facet>();
            private readonly Dictionary<int, List<int>> sections = new Dictionary<int, List<int>>();
            private readonly HashSet<int> candidates = new HashSet<int>();
            private readonly Vector3[] crossings = new Vector3[6];
            private readonly Vector3 center;
            private readonly float floor, ceiling;
            private readonly string layer;
            private int memberships;
            public readonly List<string> recipes = new List<string>();
            public readonly HashSet<string> slotNames = new HashSet<string>(StringComparer.Ordinal), inputs = new HashSet<string>(StringComparer.Ordinal);
            public int queries, triangleTests, expandedVertices;
            public float maximumExpansion;
            public int TriangleCount => facets.Count;
            public float FloorY => floor;
            public float CeilingY => ceiling;
            public float TopY { get; private set; }
            public LowerLayerEnvelope(Vector3 center, float floor, float ceiling) : this(center, floor, ceiling, "lower") { }
            public LowerLayerEnvelope(Vector3 center, float floor, float ceiling, string layer)
            {
                Require(Finite(center) && float.IsFinite(floor) && float.IsFinite(ceiling) && ceiling > floor,
                    "Invalid " + layer + "-layer sampling band.");
                this.center = center; this.floor = floor; this.ceiling = ceiling; this.layer = layer; TopY = floor;
            }
            public void ResetMeasurements() { queries = triangleTests = expandedVertices = 0; maximumExpansion = 0f; }
            public void Add(Vector3 a, Vector3 b, Vector3 c)
            {
                Require(Finite(a) && Finite(b) && Finite(c), "The compatible " + layer + " layer has non-finite geometry.");
                float lo = Mathf.Min(a.y, Mathf.Min(b.y, c.y)), hi = Mathf.Max(a.y, Mathf.Max(b.y, c.y));
                if (hi < floor || lo > ceiling || Vector3.Cross(b - a, c - a).sqrMagnitude <= .000000000001f) return;
                Require(facets.Count < MaximumTriangles, "Compatible " + layer + "-layer geometry exceeds its triangle budget.");
                int index = facets.Count; facets.Add(new Facet { a = a, b = b, c = c, lo = lo, hi = hi });
                TopY = Mathf.Max(TopY, Mathf.Min(hi, ceiling));
                for (int section = Key(Mathf.Max(lo, floor)); section <= Key(Mathf.Min(hi, ceiling)); section++)
                {
                    Require(++memberships <= MaximumMemberships, "The compatible " + layer + "-layer section index exceeds its budget.");
                    if (!sections.TryGetValue(section, out List<int> values)) sections.Add(section, values = new List<int>());
                    values.Add(index);
                }
            }
            private static int Key(float y) => Mathf.FloorToInt(y / SectionHeight);
            public Vector3 Enclose(Vector3 point, float clearance, float rowHeight)
            {
                Require(Finite(point) && float.IsFinite(clearance) && clearance > 0f && float.IsFinite(rowHeight) && rowHeight > 0f,
                    "Invalid " + layer + " garment sample.");
                // Both ends of a cloth row must enclose any intervening waistband peak.
                float dilation = rowHeight, transition = rowHeight * 2f;
                if (facets.Count == 0 || TopY <= floor || point.y < floor || point.y > TopY + dilation + transition) return point;
                Vector3 radial = new Vector3(point.x - center.x, 0f, point.z - center.z);
                float radius = radial.magnitude;
                Require(radius > .0001f, "The " + layer + " garment point has no supported radial direction.");
                radial /= radius;
                float outer = 0f;
                float lo = Mathf.Clamp(point.y - dilation, floor, TopY), hi = Mathf.Clamp(point.y + dilation, floor, TopY);
                candidates.Clear();
                for (int section = Key(lo); section <= Key(hi); section++)
                {
                    if (sections.TryGetValue(section, out List<int> values)) candidates.UnionWith(values);
                }
                Require(++queries <= 20000, "The " + layer + "-layer queries exceed their bound.");
                foreach (int index in candidates)
                {
                    Facet facet = facets[index];
                    if (hi < facet.lo || lo > facet.hi) continue;
                    Require(++triangleTests <= MaximumTriangleTests, "The " + layer + "-layer section tests exceed their bound.");
                    outer = Mathf.Max(outer, RadialMaximum(facet, radial, lo, hi));
                }
                if (outer <= 0f) return point;
                float expansion = Mathf.Max(0f, outer + clearance - radius);
                // Keep full clearance throughout the measured shell plus a mesh row, then
                // transition above it. Fading inside measured pants would reproduce the intersection.
                expansion *= 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(TopY + dilation, TopY + dilation + transition, point.y));
                if (expansion > 0f) { expandedVertices++; maximumExpansion = Mathf.Max(maximumExpansion, expansion); }
                return point + radial * expansion;
            }

            // Intersect each actual triangle with the vertical radial plane, then clip its
            // segment to the row slab. Its endpoints give the exact maximum radial extent,
            // including a narrow waistband spike between the mesh rows or sample heights.
            private float RadialMaximum(Facet facet, Vector3 radial, float lo, float hi)
            {
                Vector3 tangent = new Vector3(-radial.z, 0f, radial.x);
                int count = 0;
                Crossings(facet.a, facet.b, tangent, ref count);
                Crossings(facet.b, facet.c, tangent, ref count);
                Crossings(facet.c, facet.a, tangent, ref count);
                float maximum = 0f;
                for (int i = 0; i < count; i++)
                {
                    Vector3 point = crossings[i];
                    if (point.y >= lo && point.y <= hi) maximum = Mathf.Max(maximum, Vector3.Dot(point - center, radial));
                    for (int j = i + 1; j < count; j++)
                    {
                        Vector3 other = crossings[j];
                        if (Mathf.Abs(other.y - point.y) <= .0000001f) continue;
                        float lowT = (lo - point.y) / (other.y - point.y), highT = (hi - point.y) / (other.y - point.y);
                        if (lowT >= 0f && lowT <= 1f)
                            maximum = Mathf.Max(maximum, Vector3.Dot(Vector3.Lerp(point, other, lowT) - center, radial));
                        if (highT >= 0f && highT <= 1f)
                            maximum = Mathf.Max(maximum, Vector3.Dot(Vector3.Lerp(point, other, highT) - center, radial));
                    }
                }
                return maximum;
            }
            private void Crossings(Vector3 a, Vector3 b, Vector3 tangent, ref int count)
            {
                float da = Vector3.Dot(a - center, tangent), db = Vector3.Dot(b - center, tangent);
                if (Mathf.Abs(da) <= .0000001f) crossings[count++] = a;
                if ((da < 0f && db > 0f) || (da > 0f && db < 0f))
                    crossings[count++] = Vector3.Lerp(a, b, da / (da - db));
            }
        }

        private static List<int> DistinctLoop(Pattern pattern, List<int> loop)
        {
            var result = new List<int>();
            foreach (int vertex in loop)
                if (result.Count == 0 || (pattern.points[result[result.Count - 1]] - pattern.points[vertex]).sqrMagnitude > .0000000001f)
                    result.Add(vertex);
            if (result.Count > 1 && (pattern.points[result[0]] - pattern.points[result[result.Count - 1]]).sqrMagnitude <= .0000000001f)
                result.RemoveAt(result.Count - 1);
            Require(result.Count >= 6, "Garment aperture collapsed to too few distinct points.");
            return result;
        }

        private const float CollarBindingWidth = .009f, ArmholeBindingWidth = .008f;
        // Both bindings plus a 3mm fabric interval must fit between the two exposed apertures.
        private const float MinimumStrapSpan = CollarBindingWidth + ArmholeBindingWidth + .003f;

        private sealed class UpperEdge
        {
            public Surface surface;
            public float center, neckRadius, leftOuter, rightOuter, neckZ, ceiling, leftY, rightY, frontDepth, backDepth;
            public static UpperEdge From(Surface surface, Style style, float half, float height)
            {
                Vector3 neck = surface.Bone(HumanBodyBones.Neck);
                Vector3 leftArm = surface.Bone(HumanBodyBones.LeftUpperArm), rightArm = surface.Bone(HumanBodyBones.RightUpperArm);
                float shoulderCenter = (leftArm.x + rightArm.x) * .5f;
                FitReport evidence = style.evidence;
                if (evidence != null)
                {
                    evidence.hipsLandmark = surface.Bone(HumanBodyBones.Hips);
                    evidence.neckLandmark = neck;
                    evidence.leftUpperArmLandmark = leftArm;
                    evidence.rightUpperArmLandmark = rightArm;
                    evidence.shoulderHalfWidth = half;
                    evidence.torsoHeight = height;
                    evidence.minimumUsableStrapWidth = MinimumStrapSpan;
                }
                bool hasLeft = surface.Ray(new Vector3(-3f, neck.y, neck.z), Vector3.right, out Hit left, false);
                bool hasRight = surface.Ray(new Vector3(3f, neck.y, neck.z), Vector3.left, out Hit right, false);
                Require(hasLeft && hasRight, "The torso has no supported neck surface section.");
                float radius = (right.position.x - left.position.x) * .5f;
                float neckCenter = (left.position.x + right.position.x) * .5f;
                float desiredRadius = radius + (style.sleeves ? .007f : .018f);
                float desiredHalf = half * (style.sleeves ? 1f : .85f), maximumHalf = half * (style.sleeves ? 1f : .95f);
                float leftLimit = shoulderCenter - maximumHalf, rightLimit = shoulderCenter + maximumHalf;
                float desiredLeft = shoulderCenter - desiredHalf, desiredRight = shoulderCenter + desiredHalf;
                float availableRadius = Mathf.Min(neckCenter - leftLimit, rightLimit - neckCenter) - MinimumStrapSpan;
                float paddedRadius = Mathf.Min(desiredRadius, availableRadius);
                var edge = new UpperEdge { surface = surface, center = neckCenter, neckZ = neck.z, ceiling = neck.y + .01f,
                    neckRadius = paddedRadius,
                    leftOuter = Mathf.Max(leftLimit, Mathf.Min(desiredLeft, neckCenter - paddedRadius - MinimumStrapSpan)),
                    rightOuter = Mathf.Min(rightLimit, Mathf.Max(desiredRight, neckCenter + paddedRadius + MinimumStrapSpan)),
                    frontDepth = Mathf.Clamp(height * (style.sleeves ? .10f : .18f), .025f, style.sleeves ? .05f : .085f),
                    backDepth = Mathf.Clamp(height * (style.sleeves ? .03f : .05f), .008f, .022f) };
                float leftSpan = neckCenter - paddedRadius - edge.leftOuter, rightSpan = edge.rightOuter - neckCenter - paddedRadius;
                if (evidence != null)
                {
                    evidence.measuredNeckRadius = radius; evidence.desiredNeckRadius = desiredRadius;
                    evidence.paddedNeckRadius = paddedRadius; evidence.neckSectionCenterX = neckCenter;
                    evidence.leftOuterX = edge.leftOuter; evidence.rightOuterX = edge.rightOuter;
                    evidence.desiredLeftOuterX = desiredLeft; evidence.desiredRightOuterX = desiredRight;
                    evidence.upperBoundaryAdapted = Mathf.Abs(desiredRadius - paddedRadius) > .000001f
                        || Mathf.Abs(desiredLeft - edge.leftOuter) > .000001f || Mathf.Abs(desiredRight - edge.rightOuter) > .000001f;
                    evidence.leftAvailableSpan = leftSpan; evidence.rightAvailableSpan = rightSpan;
                }
                string measurements = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    " Measured neck radius={0:R}, desired={1:R}, padded={2:R}, shoulder half={3:R}, torso height={4:R}, neck center={5:R}, outer X=({6:R},{7:R}), spans=({8:R},{9:R}), minimum={10:R}.",
                    radius, desiredRadius, paddedRadius, half, height, neckCenter, edge.leftOuter, edge.rightOuter, leftSpan, rightSpan, MinimumStrapSpan);
                Require(radius > .025f && radius < half * .6f, "Unsupported neck-to-shoulder surface section." + measurements);
                // Keep real clearance around the neck; narrower straps must adapt their outer boundary, not clip skin.
                Require(paddedRadius >= radius + .006f && leftSpan >= MinimumStrapSpan - .00001f && rightSpan >= MinimumStrapSpan - .00001f,
                    "The supported shoulder cannot fit both bindings and neck clearance." + measurements);
                Vector3 leftNeck = edge.CrownPoint(neckCenter - paddedRadius), rightNeck = edge.CrownPoint(neckCenter + paddedRadius);
                Vector3 leftOuter = edge.CrownPoint(edge.leftOuter), rightOuter = edge.CrownPoint(edge.rightOuter);
                float leftWidth = (leftNeck - leftOuter).magnitude, rightWidth = (rightNeck - rightOuter).magnitude;
                if (evidence != null) { evidence.leftStrapWidth = leftWidth; evidence.rightStrapWidth = rightWidth; }
                Require(leftWidth >= MinimumStrapSpan && rightWidth >= MinimumStrapSpan,
                    "The sampled shoulder crown has no usable fabric strip." + measurements);
                edge.leftY = leftNeck.y - .004f;
                edge.rightY = rightNeck.y - .004f;
                return edge;
            }
            public float OuterX(int side) => side == 0 ? leftOuter : rightOuter;
            private Vector3 CrownPoint(float x)
            {
                Require(surface.Ray(new Vector3(x, ceiling + .5f, neckZ), Vector3.down, out Hit hit, null, ceiling),
                    "Unsupported shoulder crown at x=" + x + ".");
                return hit.position;
            }
            public void At(float across, out float x, out float frontY, out float backY)
            {
                float a = Mathf.Abs(across), sign = across < 0f ? -1f : 1f;
                if (a <= 1f / 3f)
                {
                    float s = across * 3f;
                    x = center + s * neckRadius;
                    float sideY = Mathf.Lerp(leftY, rightY, (s + 1f) * .5f);
                    float round = Mathf.Sqrt(Mathf.Max(0f, 1f - s * s));
                    frontY = sideY - frontDepth * round;
                    backY = sideY - backDepth * round;
                }
                else
                {
                    float outer = across < 0f ? leftOuter : rightOuter;
                    x = Mathf.Lerp(center + sign * neckRadius, outer, (a - 1f / 3f) * 1.5f);
                    frontY = backY = CrownPoint(x).y - .004f;
                }
            }
        }

        private static void Binding(Pattern pattern, List<int> loop, float width, Rect uv)
        {
            // A trim stays on the incident fabric. It neither raises a vertical fence nor bridges a cutout.
            int triangleLimit = pattern.indices.Count;
            var boundary = new HashSet<int>(loop);
            int[] inner = new int[loop.Count + 1], outer = new int[loop.Count + 1];
            Vector3[] directions = new Vector3[loop.Count], normals = new Vector3[loop.Count];
            for (int i = 0; i < loop.Count; i++)
            {
                int vertex = loop[i]; Vector3 point = pattern.points[vertex], inward = Vector3.zero, normal = Vector3.zero;
                float shortest = float.PositiveInfinity;
                for (int t = 0; t < triangleLimit; t += 3)
                {
                    int a = pattern.indices[t], b = pattern.indices[t + 1], c = pattern.indices[t + 2];
                    if (a != vertex && b != vertex && c != vertex) continue;
                    normal += Vector3.Cross(pattern.points[b] - pattern.points[a], pattern.points[c] - pattern.points[a]);
                    foreach (int neighbor in new[] { a, b, c })
                    {
                        if (boundary.Contains(neighbor)) continue;
                        Vector3 delta = pattern.points[neighbor] - point;
                        inward += delta.normalized;
                        shortest = Mathf.Min(shortest, delta.magnitude);
                    }
                }
                Require(inward.sqrMagnitude > .000001f && normal.sqrMagnitude > .0000000001f,
                    "Trim edge has no incident fabric: " + vertex);
                normals[i] = normal.normalized;
                directions[i] = Vector3.ProjectOnPlane(inward, normals[i]).normalized * Mathf.Min(width, shortest * .45f);
            }
            for (int i = 0; i <= loop.Count; i++)
            {
                int j = i % loop.Count;
                Vector3 point = pattern.points[loop[j]], desired = point + directions[j], closest = point;
                float best = float.PositiveInfinity;
                // Projection only onto this edge's own incident cloth cannot choose the opposite side of an opening.
                for (int t = 0; t < triangleLimit; t += 3)
                {
                    int a = pattern.indices[t], b = pattern.indices[t + 1], c = pattern.indices[t + 2];
                    if (a != loop[j] && b != loop[j] && c != loop[j]) continue;
                    Vector3 candidate = UMA.ClothingConformerMeshUtility.ClosestPointOnTriangle(desired, pattern.points[a], pattern.points[b], pattern.points[c]);
                    float distance = (candidate - desired).sqrMagnitude;
                    if (distance < best) { best = distance; closest = candidate; }
                }
                Require((closest - point).sqrMagnitude > .00000001f, "Trim collapsed at its aperture edge.");
                float u = uv.x + uv.width * i / loop.Count;
                // A sub-millimetre lift prevents overlay z-fighting while leaving the aperture unchanged.
                inner[i] = pattern.Add(point + normals[j] * .0006f, new Vector2(u, uv.yMin));
                outer[i] = pattern.Add(closest + normals[j] * .0006f, new Vector2(u, uv.yMax));
            }
            for (int i = 0; i < loop.Count; i++) pattern.Quad(inner[i], inner[i + 1], outer[i + 1], outer[i], normals[i]);
        }

        private static bool Intersect(Vector3 origin, Vector3 direction, Vector3 a, Vector3 b, Vector3 c,
            out float distance, out Vector3 barycentric)
        {
            distance = 0f; barycentric = Vector3.zero;
            Vector3 e1 = b - a, e2 = c - a, h = Vector3.Cross(direction, e2);
            float determinant = Vector3.Dot(e1, h);
            if (Mathf.Abs(determinant) < .0000001f) return false;
            float inverse = 1f / determinant;
            Vector3 s = origin - a;
            float u = inverse * Vector3.Dot(s, h);
            if (u < -.00001f || u > 1.00001f) return false;
            Vector3 q = Vector3.Cross(s, e1);
            float v = inverse * Vector3.Dot(direction, q);
            if (v < -.00001f || u + v > 1.00001f) return false;
            distance = inverse * Vector3.Dot(e2, q);
            if (distance < 0f) return false;
            barycentric = new Vector3(1f - u - v, u, v);
            return true;
        }

        private sealed class CoverageIndex
        {
            private const float Cell = .04f;
            private const int MaximumCandidates = 512, MaximumTriangleTests = 12000000, MaximumQueries = 200000;
            private readonly Pattern pattern;
            private readonly Dictionary<Vector3Int, List<int>> cells = new Dictionary<Vector3Int, List<int>>();
            public int queries, triangleTests, maximumCandidates, largeTrianglesRetained;
            public CoverageIndex(Pattern pattern)
            {
                this.pattern = pattern;
                Require(pattern.indices.Count / 3 <= 12000, "Garment triangle count exceeds bounded mask authoring.");
                int memberships = 0;
                for (int t = 0; t < pattern.indices.Count; t += 3)
                {
                    Vector3 a = pattern.points[pattern.indices[t]], b = pattern.points[pattern.indices[t + 1]], c = pattern.points[pattern.indices[t + 2]];
                    Require(Finite(a) && Finite(b) && Finite(c), "Non-finite mask geometry.");
                    Vector3Int lo = Key(Vector3.Min(a, Vector3.Min(b, c))), hi = Key(Vector3.Max(a, Vector3.Max(b, c)));
                    long volume = (long)(hi.x - lo.x + 1) * (hi.y - lo.y + 1) * (hi.z - lo.z + 1);
                    Require(volume <= 2048, "A garment triangle exceeds the spatial mask cell bound.");
                    for (int x = lo.x; x <= hi.x; x++) for (int y = lo.y; y <= hi.y; y++) for (int z = lo.z; z <= hi.z; z++)
                    {
                        Require(++memberships <= 250000, "Garment mask index exceeds its cell budget.");
                        var key = new Vector3Int(x, y, z);
                        if (!cells.TryGetValue(key, out List<int> values)) cells.Add(key, values = new List<int>());
                        values.Add(t);
                    }
                }
            }
            private static Vector3Int Key(Vector3 p) => new Vector3Int(Mathf.FloorToInt(p.x / Cell), Mathf.FloorToInt(p.y / Cell), Mathf.FloorToInt(p.z / Cell));
            public bool Covers(Vector3 point, Vector3 normal, float reach)
            {
                Require(++queries <= MaximumQueries && Finite(point) && Finite(normal) && normal.sqrMagnitude > .5f,
                    "Invalid or excessive mask coverage queries.");
                Require(reach > 0f && reach <= .06f, "Mask clearance must remain local to the fabric.");
                normal.Normalize();
                Vector3 origin = point + normal * .0002f, end = origin + normal * reach;
                Vector3Int lo = Key(Vector3.Min(origin, end) - Vector3.one * .0001f), hi = Key(Vector3.Max(origin, end) + Vector3.one * .0001f);
                long volume = (long)(hi.x - lo.x + 1) * (hi.y - lo.y + 1) * (hi.z - lo.z + 1);
                Require(volume <= 64, "Mask ray exceeds its bounded cell traversal.");
                var candidates = new HashSet<int>();
                for (int x = lo.x; x <= hi.x; x++) for (int y = lo.y; y <= hi.y; y++) for (int z = lo.z; z <= hi.z; z++)
                    if (cells.TryGetValue(new Vector3Int(x, y, z), out List<int> values)) foreach (int t in values) candidates.Add(t);
                maximumCandidates = Mathf.Max(maximumCandidates, candidates.Count);
                Require(candidates.Count <= MaximumCandidates, "A mask ray exceeds the local triangle candidate bound.");
                foreach (int t in candidates)
                {
                    Require(++triangleTests <= MaximumTriangleTests, "Mask ray tests exceed their total budget.");
                    Vector3 a = pattern.points[pattern.indices[t]], b = pattern.points[pattern.indices[t + 1]], c = pattern.points[pattern.indices[t + 2]];
                    Vector3 outward = Vector3.Cross(b - a, c - a).normalized;
                    if (Vector3.Dot(outward, normal) < .15f) continue;
                    if (Intersect(origin, normal, a, b, c, out float distance, out Vector3 bary) && distance <= reach) return true;
                }
                return false;
            }
            public bool CoversTriangle(Vector3 a, Vector3 b, Vector3 c, Vector3 an, Vector3 bn, Vector3 cn, float reach)
            {
                float longest = Mathf.Max((a - b).magnitude, Mathf.Max((b - c).magnitude, (c - a).magnitude));
                int divisions = Mathf.CeilToInt(longest / .012f);
                // Large unsupported topology stays visible; a budget must never authorize hiding more skin.
                if (divisions > 8) { largeTrianglesRetained++; return false; }
                if (!(Covers(a, an, reach) && Covers(b, bn, reach) && Covers(c, cn, reach)
                    && Covers((a + b + c) / 3f, (an + bn + cn).normalized, reach)
                    && Covers((a + b) * .5f, (an + bn).normalized, reach)
                    && Covers((b + c) * .5f, (bn + cn).normalized, reach)
                    && Covers((c + a) * .5f, (cn + an).normalized, reach))) return false;
                if (divisions <= 2) return true;
                // Additional bounded samples catch cutouts inside a triangle, away from its corners/centroid.
                for (int i = 0; i <= divisions; i++) for (int j = 0; j <= divisions - i; j++)
                {
                    float u = i / (float)divisions, v = j / (float)divisions, w = 1f - u - v;
                    if (!Covers(a * w + b * u + c * v, (an * w + bn * u + cn * v).normalized, reach)) return false;
                }
                return true;
            }
        }
    }
}
