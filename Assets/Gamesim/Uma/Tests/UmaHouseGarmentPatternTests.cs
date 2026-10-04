#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Gamesim.Uma.Tests
{
    /// <summary>Independent geometry fixtures: no asset authoring, installed clothes or generated mask expectations.</summary>
    public sealed class UmaHouseGarmentPatternTests
    {
        private const BindingFlags Fields = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        // Scratch float32 measurements: pole cross squares <=8e-21 m^4, regular faces >5e-10 m^4.
        private const float SyntheticCrossSquaredThreshold = 1e-18f;
        private static Type Author => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("Gamesim.Uma.Editor.HouseGarmentAuthoring"))
            .FirstOrDefault(t => t != null) ?? throw new InvalidOperationException("Editor authoring assembly is missing.");
        private static Type Nested(string name) => Author.GetNestedType(name, BindingFlags.NonPublic);
        private static object New(string name) => Activator.CreateInstance(Nested(name), true);
        private static void Set(object owner, string name, object value) => owner.GetType().GetField(name, Fields).SetValue(owner, value);
        private static T Get<T>(object owner, string name) => (T)owner.GetType().GetField(name, Fields).GetValue(owner);
        private static object Call(object owner, string name, params object[] args)
        {
            try { return owner.GetType().GetMethod(name, Fields).Invoke(owner, args); }
            catch (TargetInvocationException e) { throw e.InnerException ?? e; }
        }
        private static object Pattern(object surface, bool sleeves, object evidence = null)
        {
            object style = New("Style"); Set(style, "sleeves", sleeves); Set(style, "clearance", sleeves ? .012f : .008f);
            Set(style, "evidence", evidence);
            try { return Author.GetMethod("Panels", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new[] { surface, style }); }
            catch (TargetInvocationException e) { throw e.InnerException ?? e; }
        }

        [Test]
        public void GarmentPattern_CrewCoversRoundedShoulderCapsAndLeavesTheNeckOpen()
        {
            AssertOutwardComponentEvidence();
            AssertOutwardSleeveExit();
            foreach (bool loweredArms in new[] { false, true })
            {
                object surface = Body(loweredArms), pattern = Pattern(surface, true);
                List<Vector3> points = Get<List<Vector3>>(pattern, "points");
                // Panel points are front/back pairs. A straight neckline fails this independent silhouette check.
                Vector3 middle = points[(20 * 25 + 12) * 2];
                Vector3 left = points[(20 * 25 + 8) * 2], right = points[(20 * 25 + 16) * 2];
                Assert.That(middle.y, Is.LessThan(Mathf.Min(left.y, right.y) - .025f));
                Assert.That(Mathf.Abs(left.x) + Mathf.Abs(right.x), Is.InRange(.13f, .18f));
                Assert.That(middle.z, Is.GreaterThan(.06f));
                object coverage = Activator.CreateInstance(Nested("CoverageIndex"), Fields, null, new[] { pattern }, null);
                // Known caps from the independent ellipsoid fixture sit above the joint, not at its Y.
                foreach (float side in new[] { -1f, 1f })
                {
                    Vector3 crown = new Vector3(side * .2f, 1.565f, 0f);
                    Assert.That((bool)Call(coverage, "Covers", crown, Vector3.up, .04f), Is.True,
                        "The knit must place fabric above the actual shoulder crown.");
                }
                Assert.That((bool)Call(coverage, "Covers", new Vector3(0f, 1.64f, .066f), Vector3.forward, .04f), Is.False,
                    "The exposed neck must not find cloth through the opening.");
                // Known independent cylindrical arm surfaces, including inner/back quadrants.
                // A sleeve that merely covers its front or attaches at the shoulder is insufficient.
                foreach (float side in new[] { -1f, 1f })
                {
                    Vector3 shoulder = new Vector3(side * .2f, 1.5f, 0f);
                    Vector3 hand = new Vector3(side * .64f, loweredArms ? 1.16f : 1.5f, 0f);
                    Vector3 axis = (hand - shoulder).normalized, up = Vector3.ProjectOnPlane(Vector3.up, axis).normalized;
                    Vector3 forward = Vector3.Cross(axis, up);
                    foreach (float along in new[] { .35f, .6f, .82f })
                        for (int angle = 0; angle < 16; angle++)
                        {
                            float radians = angle * Mathf.PI / 8f;
                            Vector3 outward = up * Mathf.Cos(radians) + forward * Mathf.Sin(radians);
                            Vector3 skin = Vector3.Lerp(shoulder, hand, along) + outward * Mathf.Lerp(.06f, .035f, along);
                            Assert.That((bool)Call(coverage, "Covers", skin, outward, .04f), Is.True,
                                "Supported inner/outer arm skin must have fabric outside it, with only the real cuff open.");
                        }
                }
                AssertFaces(pattern);
                AssertApertureSeams(pattern, true);
                // Prove the orientation check rejects an actual reversed sleeve facet,
                // without changing shape, triangle membership, UVs or skin correspondence.
                List<int> indices = Get<List<int>>(pattern, "indices");
                int[] originalIndices = indices.ToArray();
                Vector3[] originalPoints = points.ToArray();
                Vector2[] originalUvs = Get<List<Vector2>>(pattern, "uvs").ToArray();
                try
                {
                    foreach (int facet in new[] { 0, indices.Count / 3 - 1 })
                    {
                        int offset = facet * 3, swap = indices[offset + 1];
                        indices[offset + 1] = indices[offset + 2]; indices[offset + 2] = swap;
                        Assert.Throws<AssertionException>(() => AssertConsistentWinding(pattern));
                        Call(pattern, "OrientFacesConsistently");
                        Assert.That(indices, Is.EqualTo(originalIndices), "Neither the first nor last facet can choose a component's outward orientation.");
                    }
                    // A coherent reversal has no shared-edge conflict; only independently
                    // retained anatomical evidence can distinguish its inward global sign.
                    for (int offset = 0; offset < indices.Count; offset += 3)
                    {
                        int swap = indices[offset + 1]; indices[offset + 1] = indices[offset + 2]; indices[offset + 2] = swap;
                    }
                    AssertConsistentWinding(pattern);
                    Call(pattern, "OrientFacesConsistently");
                    Assert.That(indices, Is.EqualTo(originalIndices));
                    Assert.That(points, Is.EqualTo(originalPoints));
                    Assert.That(Get<List<Vector2>>(pattern, "uvs"), Is.EqualTo(originalUvs));
                }
                finally { indices.Clear(); indices.AddRange(originalIndices); }
                AssertConsistentWinding(pattern);
            }
        }

        [Test]
        public void GarmentPattern_VestKeepsTheCapsExposedWithCurvedArmholesAndNarrowerStraps()
        {
            AssertCurvedBodyPanelRefinement();
            object surface = Body(true), knit = Pattern(surface, true), vest = Pattern(surface, false);
            List<Vector3> k = Get<List<Vector3>>(knit, "points"), v = Get<List<Vector3>>(vest, "points");
            // The demonstrated native back fold is the row 17-to-18, column 1-to-2
            // diagonal. Its coarse edge must be split in the actual generated vest,
            // together with the mirrored cell, not only in the synthetic helper.
            List<int> vestIndices = Get<List<int>>(vest, "indices");
            foreach (var chord in new[] { (853, 905), (895, 947) })
                Assert.That(Enumerable.Range(0, vestIndices.Count / 3).Any(face =>
                    Enumerable.Range(0, 3).Any(edge =>
                    {
                        int a = vestIndices[face * 3 + edge], b = vestIndices[face * 3 + (edge + 1) % 3];
                        return (a == chord.Item1 && b == chord.Item2) || (b == chord.Item1 && a == chord.Item2);
                    })), Is.False, "The demonstrated inward coarse chord and its counterpart must use fresh support nodes.");
            int topOuter = (20 * 25 + 24) * 2, lowerOuter = (15 * 25 + 24) * 2;
            Assert.That(v[topOuter].x, Is.LessThan(k[topOuter].x - .02f));
            Assert.That(v[topOuter].x, Is.LessThan(v[lowerOuter].x - .012f));
            Assert.That(v[(20 * 25 + 12) * 2].y, Is.LessThan(k[(20 * 25 + 12) * 2].y - .02f));
            var loops = Get<List<List<int>>>(vest, "armBoundaries");
            Assert.That(loops, Has.Count.EqualTo(2));
            foreach (List<int> loop in loops)
            {
                Assert.That(loop.Count, Is.GreaterThan(16), "The lower flank arc must be included, rather than one chest-crossing chord.");
                float lowerY = loop.Min(i => v[i].y);
                var bottom = loop.Where(i => v[i].y < lowerY + .025f).Select(i => v[i]).ToArray();
                Assert.That(bottom.Length, Is.GreaterThanOrEqualTo(5));
                Assert.That(bottom.Max(p => Mathf.Abs(p.x)) - bottom.Min(p => Mathf.Abs(p.x)), Is.GreaterThan(.012f),
                    "The armhole's bottom follows the curved flank surface.");
            }
            object coverage = Activator.CreateInstance(Nested("CoverageIndex"), Fields, null, new[] { vest }, null);
            Assert.That((bool)Call(coverage, "Covers", new Vector3(.2f, 1.565f, 0f), Vector3.up, .035f), Is.False);
            AssertFaces(vest);
            AssertApertureSeams(vest, false);
            AssertLowerLayerClearance(false);
            AssertLowerLayerClearance(true);
            AssertUpperLayerClearance(false);
            AssertUpperLayerClearance(true);
        }

        [Test]
        public void GarmentPattern_BroadNeckRetainsSupportedStrapsAndRejectsAnUnsupportedShift()
        {
            object evidence = New("FitReport");
            object regular = Pattern(Body(true), false), broad = Pattern(Body(true, .10f), false, evidence);
            List<Vector3> r = Get<List<Vector3>>(regular, "points"), b = Get<List<Vector3>>(broad, "points");
            int topLeft = (20 * 25) * 2, topRight = (20 * 25 + 24) * 2;
            Assert.That((r[topLeft] - b[topLeft]).magnitude, Is.LessThan(.00001f));
            Assert.That((r[topRight] - b[topRight]).magnitude, Is.LessThan(.00001f),
                "Supported broad-neck anatomy must preserve the default vest cap exposure.");
            Assert.That(Get<float>(evidence, "paddedNeckRadius"), Is.GreaterThan(.65f * .17f),
                "This fixture exercises anatomy the old style ratio refused.");
            Assert.That(Get<float>(evidence, "leftStrapWidth"), Is.GreaterThan(.04f));
            Assert.That(Get<float>(evidence, "rightStrapWidth"), Is.GreaterThan(.04f));
            Assert.That(Get<bool>(evidence, "upperBoundaryAdapted"), Is.False);
            AssertFaces(broad); AssertApertureSeams(broad, false);
            object coverage = Activator.CreateInstance(Nested("CoverageIndex"), Fields, null, new[] { broad }, null);
            Assert.That((bool)Call(coverage, "Covers", new Vector3(0f, 1.65f, .10f), Vector3.forward, .035f), Is.False,
                "The broad-neck aperture must still leave the actual neck exposed.");
            object narrowEvidence = New("FitReport");
            object narrow = Pattern(Body(true, .05f, 0f, .085f), false, narrowEvidence);
            Assert.That(Get<bool>(narrowEvidence, "upperBoundaryAdapted"), Is.True,
                "This supported narrow-shoulder fixture requires measured widening/padding adaptation.");
            Assert.That(Get<float>(narrowEvidence, "paddedNeckRadius"), Is.GreaterThanOrEqualTo(.056f));
            Assert.That(Get<float>(narrowEvidence, "leftOuterX"), Is.GreaterThanOrEqualTo(-.08076f));
            Assert.That(Get<float>(narrowEvidence, "rightOuterX"), Is.LessThanOrEqualTo(.08076f));
            Assert.That(Get<float>(narrowEvidence, "leftAvailableSpan"), Is.GreaterThanOrEqualTo(.01999f));
            Assert.That(Get<float>(narrowEvidence, "rightAvailableSpan"), Is.GreaterThanOrEqualTo(.01999f));
            AssertFaces(narrow); AssertApertureSeams(narrow, false);
            var error = Assert.Throws<InvalidOperationException>(() => Pattern(Body(true, .10f, .095f), false));
            Assert.That(error.Message, Does.Contain("cannot fit both bindings and neck clearance"));
            Assert.That(error.Message, Does.Contain("Measured neck radius="));
        }

        [Test]
        public void GarmentPattern_CollarBindingStaysOnItsOwnFabricWithoutAnUprightWall()
        {
            object pattern = New("Pattern");
            List<Vector3> points = Get<List<Vector3>>(pattern, "points");
            List<Vector2> uvs = Get<List<Vector2>>(pattern, "uvs");
            var loop = new List<int>();
            const int count = 24;
            // An independent annular shoulder surface slopes down away from the neck.
            for (int ring = 0; ring < 2; ring++) for (int i = 0; i < count; i++)
            {
                float angle = i * Mathf.PI * 2f / count, radius = ring == 0 ? .08f : .12f;
                points.Add(new Vector3(Mathf.Cos(angle) * radius, ring == 0 ? 1.6f : 1.57f, Mathf.Sin(angle) * radius));
                uvs.Add(Vector2.zero);
                if (ring == 0) loop.Add(i);
            }
            for (int i = 0; i < count; i++) Call(pattern, "Quad", i, (i + 1) % count, count + (i + 1) % count, count + i, Vector3.up);
            int before = points.Count;
            Author.GetMethod("Binding", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null,
                new object[] { pattern, loop, .009f, new Rect(0f, 0f, 1f, 1f) });
            for (int i = before; i < points.Count; i++)
            {
                Assert.That(points[i].y, Is.LessThan(1.601f), "A raised18mm collar strip fails this check.");
                Assert.That(new Vector2(points[i].x, points[i].z).magnitude, Is.InRange(.079f, .095f));
            }
            AssertFaces(pattern);
        }

        [Test]
        public void GarmentPattern_MasksRetainBoundaryCrossingAndOffCentreCutoutTriangles()
        {
            object pattern = FlatClothWithOpening();
            object coverage = Activator.CreateInstance(Nested("CoverageIndex"), Fields, null, new[] { pattern }, null);
            Assert.That((bool)Call(coverage, "Covers", new Vector3(.018f, .029f, 0f), Vector3.forward, .035f), Is.False);
            Assert.That((bool)Call(coverage, "Covers", new Vector3(.008f, .012f, 0f), Vector3.forward, .035f), Is.True,
                "A skin ray meets the cloth's back face, so the intersection must be two-sided.");
            Assert.That((bool)Call(coverage, "CoversTriangle", new Vector3(.002f, .002f, 0f), new Vector3(.055f, .002f, 0f),
                new Vector3(.002f, .055f, 0f), Vector3.forward, Vector3.forward, Vector3.forward, .035f), Is.False,
                "All vertices and centroid miss this off-centre opening; added bounded samples must retain the body triangle.");
            Assert.That((bool)Call(coverage, "CoversTriangle", new Vector3(.006f, .004f, 0f), new Vector3(.012f, .004f, 0f),
                new Vector3(.006f, .01f, 0f), Vector3.forward, Vector3.forward, Vector3.forward, .035f), Is.True);
        }

        [Test]
        public void GarmentPattern_MaskQueriesStayLocalAndUnsupportedLargeTrianglesStayVisible()
        {
            object pattern = FlatClothWithOpening();
            List<Vector3> points = Get<List<Vector3>>(pattern, "points");
            List<Vector2> uvs = Get<List<Vector2>>(pattern, "uvs");
            for (int i = 0; i < 300; i++)
            {
                int start = points.Count; float x = 1f + i * .08f;
                points.Add(new Vector3(x, 0f, .012f)); points.Add(new Vector3(x + .02f, 0f, .012f));
                points.Add(new Vector3(x + .02f, .02f, .012f)); points.Add(new Vector3(x, .02f, .012f));
                for (int j = 0; j < 4; j++) uvs.Add(Vector2.zero);
                Call(pattern, "Quad", start, start + 1, start + 2, start + 3, Vector3.forward);
            }
            object coverage = Activator.CreateInstance(Nested("CoverageIndex"), Fields, null, new[] { pattern }, null);
            Assert.That((bool)Call(coverage, "Covers", new Vector3(.006f, .006f, 0f), Vector3.forward, .035f), Is.True);
            Assert.That(Get<int>(coverage, "maximumCandidates"), Is.LessThan(20));
            Assert.That(Get<int>(coverage, "triangleTests"), Is.LessThan(20));
            Assert.That((bool)Call(coverage, "CoversTriangle", Vector3.zero, Vector3.right * .2f, Vector3.up * .2f,
                Vector3.forward, Vector3.forward, Vector3.forward, .035f), Is.False);
            Assert.That(Get<int>(coverage, "largeTrianglesRetained"), Is.EqualTo(1));
            Assert.Throws<InvalidOperationException>(() => Call(coverage, "Covers", Vector3.zero, Vector3.forward, .2f));
        }

        private static void AssertOutwardComponentEvidence()
        {
            object pattern = New("Pattern");
            var points = Get<List<Vector3>>(pattern, "points");
            var uvs = Get<List<Vector2>>(pattern, "uvs");
            // One tiny folded domain facet must not anchor a thousand-times-larger
            // supported panel. Both candidates remain connected along a real edge.
            points.AddRange(new[] { Vector3.zero, new Vector3(.002f, 0f, 0f),
                new Vector3(.002f, .002f, 0f), new Vector3(-2f, .002f, 0f) });
            uvs.AddRange(Enumerable.Repeat(Vector2.zero, 4));
            Call(pattern, "Face", 0, 1, 2, Vector3.back);
            Call(pattern, "Face", 0, 2, 3, Vector3.forward);
            var indices = Get<List<int>>(pattern, "indices");
            Call(pattern, "OrientFacesConsistently");
            Assert.That(Vector3.Cross(points[indices[4]] - points[indices[3]], points[indices[5]] - points[indices[3]]).z,
                Is.GreaterThan(.001f), "The large supported panel must retain its authored outward side.");
            AssertConsistentWinding(pattern);
            IList evidence = Get<IList>(pattern, "windingEvidence");
            Assert.That(evidence, Has.Count.EqualTo(1));
            Assert.That(Get<double>(evidence[0], "confidence"), Is.GreaterThan(.99d));
            // Equal opposed evidence is not permission to guess. It must reject
            // before mutating a single face, even though topology is orientable.
            object ambiguous = New("Pattern");
            Get<List<Vector3>>(ambiguous, "points").AddRange(new[] { Vector3.zero, Vector3.right, Vector3.one - Vector3.forward, Vector3.up });
            Get<List<Vector2>>(ambiguous, "uvs").AddRange(Enumerable.Repeat(Vector2.zero, 4));
            Call(ambiguous, "Face", 0, 1, 2, Vector3.forward);
            Call(ambiguous, "Face", 0, 2, 3, Vector3.back);
            List<int> ambiguousIndices = Get<List<int>>(ambiguous, "indices");
            int[] before = ambiguousIndices.ToArray();
            var failure = Assert.Throws<InvalidOperationException>(() => Call(ambiguous, "OrientFacesConsistently"));
            Assert.That(failure.Message, Does.Contain("ambiguous authored outward evidence"));
            Assert.That(failure.Message, Does.Contain("seed=").And.Contain("signedOutwardArea=").And.Contain("confidence="));
            Assert.That(ambiguousIndices, Is.EqualTo(before));
        }

        private static void AssertOutwardSleeveExit()
        {
            object surface = Body(true);
            // Independent numeric replay of ring 2/sample 9 in the lowered proxy: the ray
            // starts in its arm cylinder but enters a separate overlapping shoulder cap.
            Vector3 center = new Vector3(-.26271933f, 1.45251513f, .000029386f);
            Vector3 radial = new Vector3(-.57126915f, .8207292f, -.007433012f);
            object[] nearest = { center, radial, null, null, float.PositiveInfinity, float.NegativeInfinity };
            Assert.That((bool)Call(surface, "Ray", nearest), Is.True);
            Assert.That(Vector3.Dot(Get<Vector3>(nearest[2], "normal"), radial), Is.LessThan(0f),
                "The ordinary ray deliberately retains its nearest entering cap hit.");
            object[] outward = { center, radial, null, null, float.PositiveInfinity, .15f };
            Assert.That((bool)Call(surface, "Ray", outward), Is.True);
            object hit = outward[2];
            Assert.That(Vector3.Dot(Get<Vector3>(hit, "normal"), radial), Is.GreaterThan(.99f));
            Assert.That(Vector3.Distance(center, Get<Vector3>(hit, "position")), Is.InRange(.0557f, .0560f));
            Assert.That(Get<bool>(Get<object>(hit, "triangle"), "arm"), Is.True);
            Vector3 bary = Get<Vector3>(hit, "barycentric");
            Assert.That(bary.x + bary.y + bary.z, Is.EqualTo(1f).Within(.00001f));
            Assert.That(Mathf.Min(bary.x, Mathf.Min(bary.y, bary.z)), Is.GreaterThanOrEqualTo(0f));

            // An inward-only sheet has no supported exit. Do not invent a nearest-point fallback.
            object inwardOnly = New("Surface"), triangle = New("Triangle");
            Set(inwardOnly, "vertices", new[] { Vector3.zero, Vector3.right, Vector3.up });
            Set(inwardOnly, "normals", new[] { Vector3.back, Vector3.back, Vector3.back });
            Set(triangle, "a", 0); Set(triangle, "b", 1); Set(triangle, "c", 2);
            ((IList)Get<object>(inwardOnly, "triangles")).Add(triangle);
            object[] rejected = { new Vector3(.2f, .2f, -.02f), Vector3.forward, null, null, float.PositiveInfinity, .15f };
            Assert.That((bool)Call(inwardOnly, "Ray", rejected), Is.False);
            rejected[5] = float.NegativeInfinity;
            Assert.That((bool)Call(inwardOnly, "Ray", rejected), Is.True, "Other surface callers keep their original unrestricted ray semantics.");
        }

        private static void AssertLowerLayerClearance(bool sleeves)
        {
            object surface = Body(true), bare = Pattern(surface, sleeves);
            object envelope = Activator.CreateInstance(Nested("LowerLayerEnvelope"), Fields, null,
                new object[] { new Vector3(0f, 1f, 0f), .98f, 1.3f }, null);
            // Independent elliptical waistband with a narrow raised seam between cloth rows.
            // Sampling only at row heights or tapering inside the layer misses this peak.
            float[] heights = { 1.005f, 1.053f, 1.057f, 1.061f, 1.12f };
            float[] widths = { .18f, .18f, .205f, .18f, .18f };
            float[] depths = { .11f, .11f, .125f, .11f, .11f };
            Vector3[,] pants = new Vector3[heights.Length, 33];
            for (int j = 0; j < heights.Length; j++) for (int i = 0; i <= 32; i++)
            {
                float angle = i * Mathf.PI / 16f;
                pants[j, i] = new Vector3(Mathf.Cos(angle) * widths[j], heights[j], Mathf.Sin(angle) * depths[j]);
                if (j == 0 || i == 0) continue;
                Call(envelope, "Add", pants[j - 1, i - 1], pants[j - 1, i], pants[j, i]);
                Call(envelope, "Add", pants[j - 1, i - 1], pants[j, i], pants[j, i - 1]);
            }
            Set(surface, "lowerLayer", envelope);
            object layered = Pattern(surface, sleeves);
            List<Vector3> original = Get<List<Vector3>>(bare, "points"), expanded = Get<List<Vector3>>(layered, "points");
            IDictionary bodyHits = (IDictionary)Get<object>(layered, "bodyHits"), originalHits = (IDictionary)Get<object>(bare, "bodyHits");
            Assert.That(expanded.Count, Is.EqualTo(original.Count));
            for (int i = 0; i < expanded.Count; i++)
            {
                if (original[i].y > 1.22f) Assert.That(expanded[i], Is.EqualTo(original[i]), "The lower layer cannot alter the upper torso or sleeves.");
                if (!bodyHits.Contains(i)) continue;
                Assert.That(Get<Vector3>(bodyHits[i], "position"), Is.EqualTo(Get<Vector3>(originalHits[i], "position")),
                    "Moving fabric outside trousers must retain its naked-body weight correspondence.");
            }
            object bareCoverage = Activator.CreateInstance(Nested("CoverageIndex"), Fields, null, new[] { bare }, null);
            object coverage = Activator.CreateInstance(Nested("CoverageIndex"), Fields, null, new[] { layered }, null);
            int originallyExposed = 0;
            // Peak and edge-midpoint samples exercise face interiors and both shared flank seams.
            for (int ring = 1; ring < heights.Length; ring++) for (int i = 0; i < 32; i++)
                foreach (Vector3 point in new[] { pants[ring, i], (pants[ring, i] + pants[ring, i + 1]) * .5f })
                {
                    Vector3 radial = new Vector3(point.x, 0f, point.z).normalized;
                    if (!(bool)Call(bareCoverage, "Covers", point, radial, .06f)) originallyExposed++;
                    Assert.That((bool)Call(coverage, "Covers", point, radial, .06f), Is.True,
                        "Fabric faces, including seams and the between-row peak, must enclose the actual lower layer.");
                }
            Assert.That(originallyExposed, Is.GreaterThan(0), "This independent wardrobe shell must expose the nude-only fitting defect.");
            Assert.That(Get<int>(envelope, "expandedVertices"), Is.GreaterThan(0));
            Assert.That(Get<float>(envelope, "maximumExpansion"), Is.InRange(.001f, .12f));
            AssertFaces(layered);
            AssertApertureSeams(layered, sleeves);
        }

        private static void AssertCurvedBodyPanelRefinement()
        {
            // Independent cylinder: every offset corner lies outside the skin, but
            // the long internal chord cuts through its convex interior. Interpolating
            // more cloth points or their corner weights leaves the old defect intact.
            object surface = New("Surface"), pattern = New("Pattern");
            var vertices = new List<Vector3>(); var normals = new List<Vector3>();
            var triangles = (IList)Get<object>(surface, "triangles"); int collapsed = 0;
            const float radius = .06f, clearance = .008f;
            for (int j = 0; j <= 4; j++) for (int i = 0; i <= 64; i++)
            {
                float angle = -1f + i * .05f;
                Vector3 normal = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
                vertices.Add(normal * radius + Vector3.up * (j * .01f)); normals.Add(normal);
                if (i == 0 || j == 0) continue;
                int a = (j - 1) * 65 + i - 1, b = a + 1, d = j * 65 + i - 1, c = d + 1;
                AddTriangle(triangles, vertices, a, b, c, false, ref collapsed);
                AddTriangle(triangles, vertices, a, c, d, false, ref collapsed);
            }
            Set(surface, "vertices", vertices.ToArray()); Set(surface, "normals", normals.ToArray());
            var points = Get<List<Vector3>>(pattern, "points"); var uvs = Get<List<Vector2>>(pattern, "uvs");
            var hits = (IDictionary)Get<object>(pattern, "bodyHits");
            float[] angles = { -.6f, .6f, .6f, -.6f, 1.6f, 1.6f };
            float[] heights = { 0f, 0f, .04f, .04f, 0f, .04f };
            Vector2[] chart = { Vector2.zero, Vector2.right, Vector2.one, Vector2.up,
                new Vector2(1.25f, 0f), new Vector2(1.25f, 1f) };
            for (int i = 0; i < angles.Length; i++)
            {
                Vector3 nude = new Vector3(Mathf.Sin(angles[i]) * radius, heights[i], Mathf.Cos(angles[i]) * radius);
                object hit = Call(surface, "Closest", nude, false);
                Vector3 point = Get<Vector3>(hit, "position") + Get<Vector3>(hit, "normal") * clearance;
                points.Add(point); uvs.Add(chart[i]); hits.Add(i, hit);
                Assert.That(Vector3.Distance(point, Get<Vector3>(hit, "position")), Is.InRange(.00799f, .00801f));
            }
            Call(pattern, "Quad", 0, 1, 2, 3, Vector3.forward);
            Call(pattern, "Quad", 1, 4, 5, 2, Vector3.forward); // Unselected adjacent panel shares the rim edge.
            Vector3[] original = points.ToArray(); Vector2[] originalUvs = uvs.ToArray();
            object[] originalHits = Enumerable.Range(0, 6).Select(i => hits[i]).ToArray();
            object coarse = Activator.CreateInstance(Nested("CoverageIndex"), Fields, null, new[] { pattern }, null);
            Assert.That((points[0].z + points[2].z) * .5f, Is.LessThan(radius - .003f));
            Assert.That((bool)Call(coarse, "Covers", new Vector3(0f, .02f, radius), Vector3.forward, .03f), Is.False,
                "This control must expose an interior skin intersection despite positive corner offsets.");
            Vector3 adjacentRadial = new Vector3(Mathf.Sin(1.1f), 0f, Mathf.Cos(1.1f));
            Vector3 adjacentSkin = adjacentRadial * radius + Vector3.up * .02f;
            Assert.That((bool)Call(coarse, "Covers", adjacentSkin, adjacentRadial, .03f), Is.False,
                "The next inward cell has its own curved interior defect, despite the same positive corner offsets.");
            object refinement;
            try
            {
                refinement = Author.GetMethod("RefineBodyPanels", BindingFlags.NonPublic | BindingFlags.Static)
                    .Invoke(null, new object[] { surface, pattern, new HashSet<int> { 0, 1 }, clearance, .02f });
            }
            catch (TargetInvocationException e) { throw e.InnerException ?? e; }
            Call(pattern, "OrientFacesConsistently");
            AssertConsistentWinding(pattern);
            Assert.That(Get<int>(pattern, "refinedBodyVertices"), Is.InRange(5, 16));
            Assert.That(Get<int>(pattern, "refinementBodyTriangleTests"), Is.InRange(1, 2000000));
            Assert.That(points.Take(6), Is.EqualTo(original));
            Assert.That(uvs.Take(6), Is.EqualTo(originalUvs));
            foreach (int i in Enumerable.Range(0, 6)) Assert.That(hits[i], Is.EqualTo(originalHits[i]));
            var midpoints = Get<Dictionary<(int, int), int>>(refinement, "midpoints");
            foreach (var edge in midpoints)
            {
                int midpoint = edge.Value; object hit = hits[midpoint];
                Assert.That(uvs[midpoint], Is.EqualTo((uvs[edge.Key.Item1] + uvs[edge.Key.Item2]) * .5f));
                Vector3 bodyPoint = Get<Vector3>(hit, "position"), normal = Get<Vector3>(hit, "normal");
                Assert.That(points[midpoint], Is.EqualTo(bodyPoint + normal * clearance));
                Vector3 bary = Get<Vector3>(hit, "barycentric");
                Assert.That(bary.x + bary.y + bary.z, Is.InRange(.99999f, 1.00001f));
                Assert.That(Mathf.Min(bary.x, Mathf.Min(bary.y, bary.z)), Is.GreaterThanOrEqualTo(-.00001f));
                object support = Get<object>(hit, "triangle");
                Vector3 reconstructed = vertices[Get<int>(support, "a")] * bary.x + vertices[Get<int>(support, "b")] * bary.y
                    + vertices[Get<int>(support, "c")] * bary.z;
                Assert.That(Vector3.Distance(reconstructed, bodyPoint), Is.LessThan(.00001f),
                    "The new point must retain its actual sampled source triangle and barycentric weights, not blended corner Hits.");
                Assert.That(Vector3.Distance(points[midpoint], bodyPoint), Is.LessThan(.12f));
            }
            int diagonal = midpoints[(0, 2)];
            Assert.That(points[diagonal].z, Is.GreaterThan(radius + .007f),
                "The longitudinal midpoint must resample convex nude support, rather than preserve the old chord.");
            object refined = Activator.CreateInstance(Nested("CoverageIndex"), Fields, null, new[] { pattern }, null);
            foreach (float angle in new[] { -.45f, 0f, .45f }) foreach (float y in new[] { .01f, .02f, .03f })
            {
                Vector3 radial = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
                Assert.That((bool)Call(refined, "Covers", radial * radius + Vector3.up * y, radial, .03f), Is.True,
                    "Refined face interiors must enclose the independently curved body, using the original 8mm offset.");
            }
            AssertRefinedPanelPerimeter(pattern, refinement);
            Assert.That(Get<List<int>>(pattern, "indices").Count, Is.GreaterThan(12));
            Assert.That((bool)Call(refined, "Covers", adjacentSkin, adjacentRadial, .03f), Is.False,
                "Refining only the outer cell must not falsely certify its still-coarse inward neighbour.");
            // Selecting separated faces makes the intervening unselected triangle
            // inherit two split edges. The same open perimeter/manifold proof covers
            // this distinct transition, while the first selection also retains an
            // untouched triangle and a neighbour with just one split edge.
            object twoEdge = New("Pattern");
            Get<List<Vector3>>(twoEdge, "points").AddRange(original);
            Get<List<Vector2>>(twoEdge, "uvs").AddRange(originalUvs);
            var otherHits = (IDictionary)Get<object>(twoEdge, "bodyHits");
            for (int i = 0; i < originalHits.Length; i++) otherHits.Add(i, originalHits[i]);
            Call(twoEdge, "Quad", 0, 1, 2, 3, Vector3.forward); Call(twoEdge, "Quad", 1, 4, 5, 2, Vector3.forward);
            object second;
            try
            {
                second = Author.GetMethod("RefineBodyPanels", BindingFlags.NonPublic | BindingFlags.Static)
                    .Invoke(null, new object[] { surface, twoEdge, new HashSet<int> { 0, 2 }, clearance, .02f });
            }
            catch (TargetInvocationException e) { throw e.InnerException ?? e; }
            Call(twoEdge, "OrientFacesConsistently"); AssertConsistentWinding(twoEdge);
            var secondEdges = Get<Dictionary<(int, int), int>>(second, "midpoints");
            Assert.That(secondEdges.ContainsKey((1, 2)) && secondEdges.ContainsKey((1, 5)), Is.True);
            AssertRefinedPanelPerimeter(twoEdge, second);
            Assert.That(Get<List<Vector3>>(twoEdge, "points").Take(6), Is.EqualTo(original));
            Assert.That(Get<List<Vector2>>(twoEdge, "uvs").Take(6), Is.EqualTo(originalUvs));

            // Select the demonstrated inward neighbour in the same one-level pass.
            // Resampling its longitudinal midpoint must repair the second chord,
            // while the original perimeter, points, UVs and support Hits stay intact.
            object adjacent = New("Pattern");
            Get<List<Vector3>>(adjacent, "points").AddRange(original);
            Get<List<Vector2>>(adjacent, "uvs").AddRange(originalUvs);
            var adjacentHits = (IDictionary)Get<object>(adjacent, "bodyHits");
            for (int i = 0; i < originalHits.Length; i++) adjacentHits.Add(i, originalHits[i]);
            Call(adjacent, "Quad", 0, 1, 2, 3, Vector3.forward);
            Call(adjacent, "Quad", 1, 4, 5, 2, Vector3.forward);
            object adjacentRefinement;
            try
            {
                adjacentRefinement = Author.GetMethod("RefineBodyPanels", BindingFlags.NonPublic | BindingFlags.Static)
                    .Invoke(null, new object[] { surface, adjacent, new HashSet<int> { 0, 1, 2, 3 }, clearance, .02f });
            }
            catch (TargetInvocationException e) { throw e.InnerException ?? e; }
            Call(adjacent, "OrientFacesConsistently"); AssertConsistentWinding(adjacent);
            var adjacentEdges = Get<Dictionary<(int, int), int>>(adjacentRefinement, "midpoints");
            Assert.That(adjacentEdges.ContainsKey((1, 5)), Is.True);
            Assert.That(Vector3.Dot(Get<List<Vector3>>(adjacent, "points")[adjacentEdges[(1, 5)]], adjacentRadial),
                Is.GreaterThan(Vector3.Dot((original[1] + original[5]) * .5f, adjacentRadial) + .001f),
                "The inward cell must resample naked support, not interpolate its old penetrating chord.");
            object adjacentCoverage = Activator.CreateInstance(Nested("CoverageIndex"), Fields, null, new[] { adjacent }, null);
            foreach (float angle in new[] { .75f, 1.1f, 1.45f }) foreach (float y in new[] { .01f, .02f, .03f })
            {
                Vector3 radial = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
                Assert.That((bool)Call(adjacentCoverage, "Covers", radial * radius + Vector3.up * y, radial, .03f), Is.True,
                    "The additionally selected cell must enclose its own curved face interior at the original 8mm offset.");
            }
            AssertRefinedPanelPerimeter(adjacent, adjacentRefinement);
            Assert.That(Get<List<Vector3>>(adjacent, "points").Take(6), Is.EqualTo(original));
            Assert.That(Get<List<Vector2>>(adjacent, "uvs").Take(6), Is.EqualTo(originalUvs));
            for (int i = 0; i < originalHits.Length; i++) Assert.That(adjacentHits[i], Is.EqualTo(originalHits[i]));
        }

        private static void AssertRefinedPanelPerimeter(object pattern, object refinement)
        {
            var boundary = (List<int>)Call(refinement, "ExpandLoop", new List<int> { 0, 1, 4, 5, 2, 3 });
            var boundaryEdges = new HashSet<(int, int)>();
            for (int i = 0; i < boundary.Count; i++) boundaryEdges.Add((Mathf.Min(boundary[i], boundary[(i + 1) % boundary.Count]),
                Mathf.Max(boundary[i], boundary[(i + 1) % boundary.Count])));
            var uses = new Dictionary<(int, int), int>(); var indices = Get<List<int>>(pattern, "indices");
            for (int t = 0; t < indices.Count; t += 3) for (int e = 0; e < 3; e++)
            {
                int a = indices[t + e], b = indices[t + (e + 1) % 3]; var edge = (Mathf.Min(a, b), Mathf.Max(a, b));
                uses[edge] = uses.TryGetValue(edge, out int count) ? count + 1 : 1;
            }
            Assert.That(uses.Where(edge => edge.Value == 1).Select(edge => edge.Key), Is.EquivalentTo(boundaryEdges),
                "Every open edge must be the propagated original perimeter, with no T-junction or newly opened seam.");
            Assert.That(boundary.Count, Is.GreaterThan(6));
            Assert.That(uses.ContainsKey((1, 2)), Is.False, "The adjacent unselected panel must share the subdivided edge.");
        }

        private static void AssertUpperLayerClearance(bool sleeves)
        {
            object surface = Body(true), bare = Pattern(surface, sleeves);
            object envelope = Activator.CreateInstance(Nested("LowerLayerEnvelope"), Fields, null,
                new object[] { new Vector3(0f, 1f, 0f), .98f, 1.66f, "upper" }, null);
            // Independently placed breast cups and a narrow raised center clasp. Their
            // peaks lie between cloth rows, above the original pants-only sampling band.
            float[] heights = { 1.32f, 1.381f, 1.387f, 1.393f, 1.43f };
            float[] xs = { -.14f, -.10f, -.05f, -.012f, 0f, .012f, .05f, .10f, .14f };
            var layer = new Vector3[heights.Length, xs.Length];
            for (int j = 0; j < heights.Length; j++) for (int i = 0; i < xs.Length; i++)
            {
                float cup = Mathf.Abs(xs[i]) >= .05f ? .155f : .14f;
                float depth = j == 2 && Mathf.Abs(xs[i]) <= .012f ? .18f : cup;
                layer[j, i] = new Vector3(xs[i], heights[j], depth);
                if (j == 0 || i == 0) continue;
                Call(envelope, "Add", layer[j - 1, i - 1], layer[j - 1, i], layer[j, i]);
                Call(envelope, "Add", layer[j - 1, i - 1], layer[j, i], layer[j, i - 1]);
            }
            // Upper straps exercise the yoke's naked-body correspondence separately
            // from the lower shell; their source heights are below the neck aperture.
            foreach (float side in new[] { -1f, 1f })
            {
                Vector3 a = new Vector3(side * .13f, 1.53f, -.09f), b = new Vector3(side * .17f, 1.53f, -.09f);
                Vector3 c = new Vector3(side * .17f, 1.57f, .09f), d = new Vector3(side * .13f, 1.57f, .09f);
                Call(envelope, "Add", a, b, c); Call(envelope, "Add", a, c, d);
            }
            Set(surface, "upperLayer", envelope);
            object layered = Pattern(surface, sleeves);
            var original = Get<List<Vector3>>(bare, "points"); var points = Get<List<Vector3>>(layered, "points");
            var originalHits = (IDictionary)Get<object>(bare, "bodyHits"); var hits = (IDictionary)Get<object>(layered, "bodyHits");
            Assert.That(points.Count, Is.EqualTo(original.Count));
            Assert.That(Get<List<Vector2>>(layered, "uvs"), Is.EqualTo(Get<List<Vector2>>(bare, "uvs")));
            Assert.That(Get<List<int>>(layered, "neckBoundary"), Is.EqualTo(Get<List<int>>(bare, "neckBoundary")));
            Assert.That(Get<List<List<int>>>(layered, "armBoundaries"), Is.EqualTo(Get<List<List<int>>>(bare, "armBoundaries")),
                "Upper fit retains the same ordered openings and shared seam indices.");
            Assert.That(NakedSupportedFaces(layered), Is.EqualTo(NakedSupportedFaces(bare)),
                "Fitting upper layers preserves every body-supported fabric face; trim is generated on its fitted incident cloth.");
            foreach (DictionaryEntry entry in originalHits)
            {
                object hit = hits[entry.Key];
                Assert.That(Get<Vector3>(hit, "position"), Is.EqualTo(Get<Vector3>(entry.Value, "position")),
                    "Measured underlayers move cloth, never its naked-body support or arm-ring skin correspondence.");
                Assert.That(Get<Vector3>(hit, "barycentric"), Is.EqualTo(Get<Vector3>(entry.Value, "barycentric")));
                Assert.That(Vector3.Distance(points[(int)entry.Key], Get<Vector3>(hit, "position")), Is.LessThan(.12f),
                    "The production 120mm point-to-naked-support guard remains applicable.");
                if (original[(int)entry.Key].y < 1.25f) Assert.That(points[(int)entry.Key], Is.EqualTo(original[(int)entry.Key]),
                    "An upper shell cannot change the lower band.");
            }
            Assert.That(Enumerable.Range(0, original.Count).Any(i => original[i].y > 1.5f && points[i] != original[i]), Is.True,
                "The strap shell must exercise the yoke displacement, not only a scalar radius calculation.");
            var originalCoverage = Activator.CreateInstance(Nested("CoverageIndex"), Fields, null, new[] { bare }, null);
            var coverage = Activator.CreateInstance(Nested("CoverageIndex"), Fields, null, new[] { layered }, null);
            int originallyExposed = 0;
            for (int j = 1; j < heights.Length; j++) for (int i = 0; i < xs.Length; i++)
                foreach (var point in i + 1 < xs.Length ? new[] { layer[j, i], (layer[j, i] + layer[j, i + 1]) * .5f }
                    : new[] { layer[j, i] })
                {
                    Vector3 radial = new Vector3(point.x, 0f, point.z).normalized;
                    if (!(bool)Call(originalCoverage, "Covers", point, radial, .06f)) originallyExposed++;
                    Assert.That((bool)Call(coverage, "Covers", point, radial, .06f), Is.True,
                        "Actual cup/clasp vertices and face-interior points must have supported fabric outside them; "
                        + "sleeves=" + sleeves + ", row=" + j + ", column=" + i + ", point=" + point.ToString("R") + ".");
                }
            Assert.That(originallyExposed, Is.GreaterThan(0));
            Assert.That((bool)Call(coverage, "Covers", new Vector3(0f, 1.64f, .066f), Vector3.forward, .04f), Is.False);
            if (!sleeves) Assert.That((bool)Call(coverage, "Covers", new Vector3(.2f, 1.565f, 0f), Vector3.up, .035f), Is.False,
                "Fitting an upper layer must preserve the vest's exposed shoulder cap.");
            Assert.That(Get<int>(envelope, "expandedVertices"), Is.GreaterThan(0));
            AssertFaces(layered); AssertApertureSeams(layered, sleeves);
            // No available TopUnderlayer is a supported, exactly unchanged fit.
            Set(surface, "upperLayer", Activator.CreateInstance(Nested("LowerLayerEnvelope"), Fields, null,
                new object[] { new Vector3(0f, 1f, 0f), .98f, 1.66f, "upper" }, null));
            Assert.That(Get<List<Vector3>>(Pattern(surface, sleeves), "points"), Is.EqualTo(original));
        }

        private static string[] NakedSupportedFaces(object pattern)
        {
            var hits = (IDictionary)Get<object>(pattern, "bodyHits");
            var indices = Get<List<int>>(pattern, "indices");
            var faces = new List<string>();
            for (int t = 0; t < indices.Count; t += 3)
            {
                int[] face = { indices[t], indices[t + 1], indices[t + 2] };
                if (!face.All(vertex => hits.Contains(vertex))) continue;
                Array.Sort(face); faces.Add(string.Join(",", face));
            }
            return faces.OrderBy(face => face, StringComparer.Ordinal).ToArray();
        }

        private static object FlatClothWithOpening()
        {
            object pattern = New("Pattern");
            List<Vector3> points = Get<List<Vector3>>(pattern, "points");
            List<Vector2> uvs = Get<List<Vector2>>(pattern, "uvs");
            // A small off-centre rectangular aperture in otherwise flat cloth; no production boundary equation is reused.
            float[,] rectangles = { { 0f, .014f, 0f, .06f }, { .023f, .06f, 0f, .06f }, { .014f, .023f, 0f, .025f }, { .014f, .023f, .034f, .06f } };
            for (int r = 0; r < 4; r++)
            {
                int start = points.Count;
                points.Add(new Vector3(rectangles[r, 0], rectangles[r, 2], .012f));
                points.Add(new Vector3(rectangles[r, 1], rectangles[r, 2], .012f));
                points.Add(new Vector3(rectangles[r, 1], rectangles[r, 3], .012f));
                points.Add(new Vector3(rectangles[r, 0], rectangles[r, 3], .012f));
                for (int j = 0; j < 4; j++) uvs.Add(Vector2.zero);
                Call(pattern, "Quad", start, start + 1, start + 2, start + 3, Vector3.forward);
            }
            return pattern;
        }

        private static object Body(bool loweredArms, float neckRadius = .065f, float neckX = 0f, float shoulderHalf = .2f)
        {
            object surface = New("Surface");
            var vertices = new List<Vector3>(); var normals = new List<Vector3>();
            // The torso narrows with its shoulder landmarks; its caps must narrow with it.
            // Fixed full-size caps overlapped the narrow fixture's neck and each other,
            // folding its otherwise valid collar strip into tiny opposing faces.
            Vector3 capRadii = new Vector3(.085f, .065f, .085f) * (shoulderHalf / .2f);
            int collapsedPoleFaces = 0;
            var triangles = (IList)surface.GetType().GetField("triangles", Fields).GetValue(surface);
            Action<Vector3, Vector3, bool> ellipsoid = (center, radii, arm) =>
            {
                const int longitude = 32, latitude = 24;
                int first = vertices.Count;
                for (int j = 0; j <= latitude; j++) for (int i = 0; i <= longitude; i++)
                {
                    float phi = j * Mathf.PI / latitude, theta = i * Mathf.PI * 2f / longitude;
                    Vector3 unit = new Vector3(Mathf.Sin(phi) * Mathf.Cos(theta), Mathf.Cos(phi), Mathf.Sin(phi) * Mathf.Sin(theta));
                    vertices.Add(center + Vector3.Scale(unit, radii));
                    normals.Add(new Vector3(unit.x / radii.x, unit.y / radii.y, unit.z / radii.z).normalized);
                    if (i > 0 && j > 0)
                    {
                        int a = first + (j - 1) * (longitude + 1) + i - 1, b = a + 1, d = first + j * (longitude + 1) + i - 1, c = d + 1;
                        AddTriangle(triangles, vertices, a, b, c, arm, ref collapsedPoleFaces);
                        AddTriangle(triangles, vertices, a, c, d, arm, ref collapsedPoleFaces);
                    }
                }
            };
            ellipsoid(new Vector3(0f, 1.3f, 0f), new Vector3(shoulderHalf * 1.1f, .34f, .13f), false);
            // A true local neck section above the torso, without depending on an optional UpperChest mapping.
            const int segments = 32;
            int neckStart = vertices.Count;
            for (int j = 0; j < 2; j++) for (int i = 0; i <= segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                vertices.Add(new Vector3(neckX + Mathf.Cos(angle) * neckRadius, j == 0 ? 1.55f : 1.78f, Mathf.Sin(angle) * neckRadius));
                normals.Add(new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)));
                if (j == 1 && i > 0)
                {
                    int a = neckStart + i - 1, b = a + 1, d = neckStart + segments + 1 + i - 1, c = d + 1;
                    AddTriangle(triangles, vertices, a, b, c, false, ref collapsedPoleFaces);
                    AddTriangle(triangles, vertices, a, c, d, false, ref collapsedPoleFaces);
                }
            }
            var landmarks = Get<Dictionary<HumanBodyBones, Vector3>>(surface, "landmarks");
            landmarks.Add(HumanBodyBones.Hips, new Vector3(0f, 1f, 0f)); landmarks.Add(HumanBodyBones.Neck, new Vector3(neckX, 1.65f, 0f));
            foreach (bool left in new[] { false, true })
            {
                float sign = left ? 1f : -1f;
                Vector3 shoulder = new Vector3(sign * shoulderHalf, 1.5f, 0f), elbow = new Vector3(sign * (shoulderHalf + .21f), loweredArms ? 1.35f : 1.5f, 0f), hand = new Vector3(sign * (shoulderHalf + .44f), loweredArms ? 1.16f : 1.5f, 0f);
                landmarks.Add(left ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm, shoulder);
                landmarks.Add(left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm, elbow);
                landmarks.Add(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand, hand);
                ellipsoid(shoulder, capRadii, true);
                Vector3 axis = (hand - shoulder).normalized, up = Vector3.ProjectOnPlane(Vector3.up, axis).normalized, forward = Vector3.Cross(axis, up);
                int first = vertices.Count;
                for (int j = 0; j <= 20; j++) for (int i = 0; i <= 24; i++)
                {
                    float t = j / 20f, angle = i * Mathf.PI * 2f / 24f;
                    Vector3 radial = up * Mathf.Cos(angle) + forward * Mathf.Sin(angle);
                    vertices.Add(Vector3.Lerp(shoulder, hand, t) + radial * Mathf.Lerp(.06f, .035f, t)); normals.Add(radial);
                    if (i > 0 && j > 0)
                    {
                        int a = first + (j - 1) * 25 + i - 1, b = a + 1, d = first + j * 25 + i - 1, c = d + 1;
                        AddTriangle(triangles, vertices, a, b, c, true, ref collapsedPoleFaces);
                        AddTriangle(triangles, vertices, a, c, d, true, ref collapsedPoleFaces);
                    }
                }
            }
            Set(surface, "vertices", vertices.ToArray()); Set(surface, "normals", normals.ToArray());
            Assert.That(collapsedPoleFaces, Is.EqualTo(192), "Only the three ellipsoids' two collapsed pole faces per longitude are omitted.");
            float minimumCrossSquared = float.PositiveInfinity;
            foreach (object triangle in triangles)
            {
                Vector3 a = vertices[Get<int>(triangle, "a")], b = vertices[Get<int>(triangle, "b")], c = vertices[Get<int>(triangle, "c")];
                float area = Vector3.Cross(b - a, c - a).sqrMagnitude;
                Assert.That(float.IsFinite(area), Is.True);
                minimumCrossSquared = Mathf.Min(minimumCrossSquared, area);
            }
            Assert.That(minimumCrossSquared, Is.GreaterThan(SyntheticCrossSquaredThreshold));
            // Exercise the installed UMA closest-point helper before testing the garment.
            // A degenerate pole previously erased the nearer shoulder with a NaN comparison.
            foreach (float side in new[] { -1f, 1f })
            {
                object hit = Call(surface, "Closest", new Vector3(side * shoulderHalf, 1.5f + capRadii.y + .007f, 0f), null);
                Vector3 point = Get<Vector3>(hit, "position"), normal = Get<Vector3>(hit, "normal");
                Assert.That(float.IsFinite(point.x) && float.IsFinite(point.y) && float.IsFinite(point.z), Is.True);
                Assert.That(float.IsFinite(normal.x) && float.IsFinite(normal.y) && float.IsFinite(normal.z), Is.True);
                Assert.That(Get<float>(hit, "distance"), Is.InRange(0f, .02f * .02f), "The known local cap must remain the nearest supported surface.");
            }
            return surface;
        }

        private static void AddTriangle(IList triangles, List<Vector3> vertices, int a, int b, int c, bool arm, ref int collapsedPoleFaces)
        {
            float area = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]).sqrMagnitude;
            if (!float.IsFinite(area)) throw new InvalidOperationException("Synthetic body contains a nonfinite triangle.");
            if (area <= SyntheticCrossSquaredThreshold) { collapsedPoleFaces++; return; }
            object t = New("Triangle"); Set(t, "a", a); Set(t, "b", b); Set(t, "c", c); Set(t, "arm", arm); triangles.Add(t);
        }
        private static void AssertFaces(object pattern)
        {
            List<Vector3> points = Get<List<Vector3>>(pattern, "points"); List<int> indices = Get<List<int>>(pattern, "indices");
            Assert.That(indices.Count, Is.GreaterThan(100));
            Assert.That(points.All(p => float.IsFinite(p.x) && float.IsFinite(p.y) && float.IsFinite(p.z)), Is.True);
            for (int t = 0; t < indices.Count; t += 3)
                Assert.That(Vector3.Cross(points[indices[t + 1]] - points[indices[t]], points[indices[t + 2]] - points[indices[t]]).sqrMagnitude,
                    Is.GreaterThan(.000000000001f), "No collapsed collar/cap faces.");
            AssertConsistentWinding(pattern);
        }
        private static void AssertConsistentWinding(object pattern)
        {
            var edges = new Dictionary<(int, int), List<(int from, int to)>>();
            List<int> indices = Get<List<int>>(pattern, "indices");
            for (int t = 0; t < indices.Count; t += 3)
                for (int e = 0; e < 3; e++)
                {
                    int a = indices[t + e], b = indices[t + (e + 1) % 3];
                    var key = (Mathf.Min(a, b), Mathf.Max(a, b));
                    if (!edges.TryGetValue(key, out var uses)) edges.Add(key, uses = new List<(int, int)>());
                    uses.Add((a, b));
                }
            foreach (var edge in edges)
            {
                Assert.That(edge.Value.Count, Is.InRange(1, 2), "Every fabric edge is a boundary or shared by two faces.");
                if (edge.Value.Count == 2)
                    Assert.That(edge.Value[0].from == edge.Value[1].to && edge.Value[0].to == edge.Value[1].from, Is.True,
                        "Connected fabric faces must traverse their shared edge in opposite directions: " + edge.Key);
            }
        }
        private static void AssertApertureSeams(object pattern, bool sleeves)
        {
            List<int> indices = Get<List<int>>(pattern, "indices");
            var loops = new List<List<int>> { Get<List<int>>(pattern, "neckBoundary") };
            loops.AddRange(Get<List<List<int>>>(pattern, "armBoundaries"));
            for (int l = 0; l < loops.Count; l++)
                for (int i = 0; i < loops[l].Count; i++)
                {
                    int a = loops[l][i], b = loops[l][(i + 1) % loops[l].Count], uses = 0;
                    for (int t = 0; t < indices.Count; t += 3)
                        if ((indices[t] == a || indices[t + 1] == a || indices[t + 2] == a)
                            && (indices[t] == b || indices[t + 1] == b || indices[t + 2] == b)) uses++;
                    Assert.That(uses, Is.EqualTo(l > 0 && sleeves ? 2 : 1), "Every aperture edge must attach to its actual yoke/flank/panel seam.");
                }
        }
    }
}
#endif
