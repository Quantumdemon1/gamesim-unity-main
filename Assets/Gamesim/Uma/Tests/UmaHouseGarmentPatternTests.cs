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
        private static object Pattern(object surface, bool sleeves)
        {
            object style = New("Style"); Set(style, "sleeves", sleeves); Set(style, "clearance", sleeves ? .012f : .008f);
            try { return Author.GetMethod("Panels", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new[] { surface, style }); }
            catch (TargetInvocationException e) { throw e.InnerException ?? e; }
        }

        [Test]
        public void GarmentPattern_CrewCoversRoundedShoulderCapsAndLeavesTheNeckOpen()
        {
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
                AssertFaces(pattern);
                AssertApertureSeams(pattern, true);
            }
        }

        [Test]
        public void GarmentPattern_VestKeepsTheCapsExposedWithCurvedArmholesAndNarrowerStraps()
        {
            object surface = Body(true), knit = Pattern(surface, true), vest = Pattern(surface, false);
            List<Vector3> k = Get<List<Vector3>>(knit, "points"), v = Get<List<Vector3>>(vest, "points");
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

        private static object Body(bool loweredArms)
        {
            object surface = New("Surface");
            var vertices = new List<Vector3>(); var normals = new List<Vector3>();
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
                        AddTriangle(triangles, a, b, c, arm); AddTriangle(triangles, a, c, d, arm);
                    }
                }
            };
            ellipsoid(new Vector3(0f, 1.3f, 0f), new Vector3(.22f, .34f, .13f), false);
            // A true local neck section above the torso, without depending on an optional UpperChest mapping.
            const int segments = 32;
            int neckStart = vertices.Count;
            for (int j = 0; j < 2; j++) for (int i = 0; i <= segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                vertices.Add(new Vector3(Mathf.Cos(angle) * .065f, j == 0 ? 1.55f : 1.78f, Mathf.Sin(angle) * .065f));
                normals.Add(new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)));
                if (j == 1 && i > 0)
                {
                    int a = neckStart + i - 1, b = a + 1, d = neckStart + segments + 1 + i - 1, c = d + 1;
                    AddTriangle(triangles, a, b, c, false); AddTriangle(triangles, a, c, d, false);
                }
            }
            var landmarks = Get<Dictionary<HumanBodyBones, Vector3>>(surface, "landmarks");
            landmarks.Add(HumanBodyBones.Hips, new Vector3(0f, 1f, 0f)); landmarks.Add(HumanBodyBones.Neck, new Vector3(0f, 1.65f, 0f));
            foreach (bool left in new[] { false, true })
            {
                float sign = left ? 1f : -1f;
                Vector3 shoulder = new Vector3(sign * .2f, 1.5f, 0f), elbow = new Vector3(sign * .41f, loweredArms ? 1.35f : 1.5f, 0f), hand = new Vector3(sign * .64f, loweredArms ? 1.16f : 1.5f, 0f);
                landmarks.Add(left ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm, shoulder);
                landmarks.Add(left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm, elbow);
                landmarks.Add(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand, hand);
                ellipsoid(shoulder, new Vector3(.085f, .065f, .085f), true);
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
                        AddTriangle(triangles, a, b, c, true); AddTriangle(triangles, a, c, d, true);
                    }
                }
            }
            Set(surface, "vertices", vertices.ToArray()); Set(surface, "normals", normals.ToArray());
            return surface;
        }

        private static void AddTriangle(IList triangles, int a, int b, int c, bool arm)
        {
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
