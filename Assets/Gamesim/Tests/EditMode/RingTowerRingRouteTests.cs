using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Gamesim.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The Ring Tower's ring route (competition ring tower, slice 4): the playable rings are made at runtime to the
    /// yard stack's own ring profile and drawn with the stack's own materials, so a built game needs nothing read back
    /// from an imported mesh. These pin the three things that route rests on: the runtime profile is the authoring
    /// script's, the meshes are made here and graduated from the stack's own ring, and the stack model's material
    /// slots are named as the tower reads them.
    /// </summary>
    public sealed class RingTowerRingRouteTests
    {
        private const string StackModel = "Assets/Gamesim/Art/Authored/SetPieces/bb_set_comp_stack.fbx";

        private static string AuthoringScript() =>
            File.ReadAllText(Path.Combine(Application.dataPath, "..", "ArtSource", "setpieces", "bb_set_comp_stack.py"));

        private static float Number(string script, string pattern)
        {
            var match = Regex.Match(script, pattern, RegexOptions.CultureInvariant);
            Assert.That(match.Success, Is.True, "The authoring script still says " + pattern);
            return float.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        }

        [Test]
        public void TheRuntimeProfileIsTheAuthoredStacks()
        {
            string script = AuthoringScript();
            Assert.That(Number(script, @"PLINTH\s*=\s*([0-9.]+)"), Is.EqualTo(CompetitionRingTower.PlinthHeight).Within(1e-5f));
            Assert.That(Number(script, @"PEG\s*=\s*([0-9.]+)"), Is.EqualTo(CompetitionRingTower.PegHeight).Within(1e-5f));
            Assert.That(Number(script, @"DISC_H\s*=\s*([0-9.]+)"), Is.EqualTo(CompetitionRingTower.RingHeight).Within(1e-5f));
            Assert.That(Number(script, @"GAP\s*=\s*([0-9.]+)"), Is.EqualTo(CompetitionRingTower.RingGap).Within(1e-5f));
            Assert.That(Number(script, @"B\.ring\(""disc_%d"" % i,\s*([0-9.]+),"), Is.EqualTo(CompetitionRingTower.RingOuter).Within(1e-5f));
            Assert.That(Number(script, @"B\.ring\(""disc_%d"" % i,\s*[0-9.]+,\s*([0-9.]+),"), Is.EqualTo(CompetitionRingTower.RingInner).Within(1e-5f));
            Assert.That(Number(script, @"segments=([0-9]+)\)\)"), Is.EqualTo(CompetitionRingTower.RingSides));
            Assert.That(Number(script, @"B\.cylinder\(""peg"",\s*([0-9.]+),"), Is.EqualTo(CompetitionRingTower.PegRadius).Within(1e-5f));
            Assert.That(Number(script, @"B\.box\(""plinth"",\s*\(([0-9.]+),"), Is.EqualTo(CompetitionRingTower.PlinthWidth).Within(1e-5f));
            Assert.That(Number(script, @"B\.box\(""reveal"",\s*\(([0-9.]+),"), Is.EqualTo(CompetitionRingTower.RevealWidth).Within(1e-5f));
            Assert.That(Number(script, @"z = PLINTH \+ ([0-9.]+)"), Is.EqualTo(CompetitionRingTower.FirstRingFoot - CompetitionRingTower.PlinthHeight).Within(1e-5f));
        }

        [Test]
        public void TheRingsAreMadeHereGraduatedFromTheStacksOwnRing()
        {
            int rings = RingTowerPuzzle.StandardRings;
            Assert.That(CompetitionRingTower.RingRadius(rings, rings), Is.EqualTo(CompetitionRingTower.RingOuter), "The largest is the stack's own ring.");
            for (int size = 1; size < rings; size++)
                Assert.That(CompetitionRingTower.RingRadius(size, rings), Is.LessThan(CompetitionRingTower.RingRadius(size + 1, rings) - .02f), "Graduated, each visibly smaller.");
            Assert.That(CompetitionRingTower.RingRadius(1, rings), Is.GreaterThan(CompetitionRingTower.RingInner + .05f), "The smallest is still a ring round the bore.");
            Assert.That(CompetitionRingTower.FirstRingFoot + rings * (CompetitionRingTower.RingHeight + CompetitionRingTower.RingGap),
                Is.LessThan(CompetitionRingTower.PlinthHeight + CompetitionRingTower.PegHeight), "The whole tower fits on a peg.");
            var mesh = CompetitionRingTower.Ring(CompetitionRingTower.RingOuter, CompetitionRingTower.RingInner, CompetitionRingTower.RingHeight, CompetitionRingTower.RingSides);
            try
            {
                Assert.That(mesh.isReadable, Is.True, "Made at runtime: nothing waits on an importer flag.");
                Assert.That(mesh.vertexCount, Is.EqualTo(4 * 2 * (CompetitionRingTower.RingSides + 1)));
                Assert.That(mesh.triangles.Length / 3, Is.EqualTo(4 * 2 * CompetitionRingTower.RingSides));
                Assert.That(mesh.bounds.size.x, Is.EqualTo(2 * CompetitionRingTower.RingOuter).Within(.002f));
                Assert.That(mesh.bounds.size.y, Is.EqualTo(CompetitionRingTower.RingHeight).Within(1e-4f));
                Assert.That(mesh.bounds.min.y, Is.EqualTo(0f).Within(1e-5f), "Its foot at its origin.");
                Assert.That(mesh.vertices.Min(v => new Vector2(v.x, v.z).magnitude), Is.EqualTo(CompetitionRingTower.RingInner).Within(1e-4f), "The bore is the stack's.");
            }
            finally { Object.DestroyImmediate(mesh); }
        }

        [Test]
        public void TheStackModelsSlotsAreNamedAsTheTowerReadsThem()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(StackModel);
            Assert.That(model, Is.Not.Null, StackModel);
            var names = model.GetComponentsInChildren<MeshRenderer>(true).SelectMany(r => r.sharedMaterials).Where(m => m != null)
                .Select(m => m.name.ToLowerInvariant()).ToList();
            Assert.That(names.Any(n => n.Contains("neon")), Is.True, "The neon slot: " + string.Join(", ", names));
            Assert.That(names.Any(n => n.Contains("ink")), Is.True, "The plinth's slot: " + string.Join(", ", names));
            Assert.That(names.Any(n => n.Contains("steel")), Is.True, "The peg's slot: " + string.Join(", ", names));
        }

        [Test]
        public void TheTowerIsBuiltOnThreeStacksAndConcealsTheirOwnMeshes()
        {
            var made = new List<Object>();
            try
            {
                var parent = new GameObject("Ring tower test stage"); made.Add(parent);
                var neon = new Material(Shader.Find("Hidden/InternalErrorShader")) { name = "Neon Red" }; made.Add(neon);
                var ink = new Material(neon) { name = "bb_mat_ink_stack" }; made.Add(ink);
                var steel = new Material(neon) { name = "bb_mat_frame_steel" }; made.Add(steel);
                var box = CompetitionRingTower.Box(CompetitionRingTower.RevealWidth, CompetitionRingTower.AuthoredHeight, CompetitionRingTower.RevealWidth); made.Add(box);
                var stacks = new List<Transform>();
                foreach (float x in new[] { 6f, -6f, 0f })
                {
                    var stack = new GameObject("bb_set_comp_stack", typeof(MeshFilter), typeof(MeshRenderer)); made.Add(stack);
                    stack.GetComponent<MeshFilter>().sharedMesh = box;
                    stack.GetComponent<MeshRenderer>().sharedMaterials = new[] { ink, steel, neon };
                    stack.transform.position = new Vector3(x, CompetitionRingTower.AuthoredHeight * .5f, 15.5f);
                    stacks.Add(stack.transform);
                }
                var concealed = new List<Renderer>();
                var tower = CompetitionRingTower.Build(parent.transform, stacks, RingTowerPuzzle.StandardRings, () => null, () => true,
                    renderer => { renderer.enabled = false; concealed.Add(renderer); });
                Assert.That(tower, Is.Not.Null);
                Assert.That(concealed, Has.Count.EqualTo(3), "Every stack's own joined mesh is switched off.");
                Assert.That(tower.Stacks.Select(s => s.position.x), Is.EqualTo(new[] { -6f, 0f, 6f }), "Left to right as the camera sees them.");
                Assert.That(tower.Rings, Has.Count.EqualTo(RingTowerPuzzle.StandardRings), "Separate rings, one object each.");
                for (int size = 1; size <= tower.Rings.Count; size++)
                {
                    var ring = tower.Rings[size - 1];
                    Assert.That(ring.GetComponent<Collider>(), Is.Null, "Collider-free.");
                    Assert.That(ring.GetComponent<MeshRenderer>().sharedMaterial.name, Is.EqualTo("Neon Red"), "The stack's own neon.");
                    Assert.That(ring.GetComponent<MeshFilter>().sharedMesh.bounds.size.x, Is.EqualTo(2 * CompetitionRingTower.RingRadius(size, tower.Rings.Count)).Within(.003f));
                    Assert.That(new Vector2(ring.position.x, ring.position.z), Is.EqualTo(new Vector2(-6f, 15.5f)), "Every ring starts on the first stack.");
                    if (size > 1) Assert.That(ring.position.y, Is.LessThan(tower.Rings[size - 2].position.y), "The larger lower.");
                }
                Assert.That(tower.Rings[tower.Rings.Count - 1].position.y, Is.EqualTo(CompetitionRingTower.FirstRingFoot).Within(1e-4f));
                Assert.That(parent.GetComponentsInChildren<Collider>(true), Is.Empty, "Nothing of the tower collides.");
                // In edit mode a component's OnDestroy may not run: the meshes the tower made go with the test's own.
                made.AddRange(tower.GetComponentsInChildren<MeshFilter>(true).Select(filter => (Object)filter.sharedMesh));
                Object.DestroyImmediate(tower.gameObject);
            }
            finally { foreach (var item in made) if (item != null) Object.DestroyImmediate(item); }
        }
    }
}
