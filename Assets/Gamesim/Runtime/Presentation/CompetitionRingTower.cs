using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gamesim.Presentation
{
    /// <summary>
    /// The yard's three neon stacks as the Ring Tower's equipment (competition ring tower, slice 4).
    ///
    /// <para><b>The rings are made at runtime</b>, to the stack's own ring profile (bb_set_comp_stack.py: a
    /// 0.30 m annulus round a 0.055 m bore, 0.10 m tall, 28 sides, 15 mm daylight between rings) and drawn
    /// with the stacks' own neon materials, graduated from the stack's own size down to half of it. That is
    /// the route certain to work in a built game: nothing is read back from an imported mesh (the stack's is
    /// one joined mesh, imported with no CPU copy) and nothing waits for an editor pass. Each stack's joined
    /// mesh - its plinth, its peg and its four equal rings in one - is switched off for the attempt and its
    /// plinth, lit reveal and peg are drawn again from the same profile and the same materials, so its own
    /// rings never show under the playable ones. The yard's one owner (<see cref="CompetitionYardDressing"/>)
    /// records what is switched off and switches it back on whatever ends the competition.</para>
    ///
    /// <para>Collider-free, like every runtime piece of the stage: the stacks keep their fitted layer-8
    /// Collision proxies, which nobody is routed into, and nothing here carves the NavMesh. The rings follow
    /// the player's attempt (<see cref="MiniGameRun.Tower"/>) each frame: a lifted ring rises over its peg, a
    /// put one comes down on its stack. Under reduced motion they stand where they belong at once.</para>
    /// </summary>
    public sealed class CompetitionRingTower : MonoBehaviour
    {
        // The stack's authored profile, in metres (ArtSource/setpieces/bb_set_comp_stack.py). An EditMode case
        // reads the authoring script and holds these to it.
        public const float PlinthHeight = .30f, PlinthWidth = .95f, RevealWidth = .99f, RevealHeight = .02f;
        public const float PegRadius = .045f, PegHeight = 1.05f;
        public const float RingOuter = .30f, RingInner = .055f, RingHeight = .10f, RingGap = .015f;
        public const int RingSides = 28;
        /// <summary>The authored stack's whole height, plinth and peg: what a placed stack's bounds are read against.</summary>
        public const float AuthoredHeight = PlinthHeight + PegHeight;
        /// <summary>The first ring's foot over the floor: 2 cm over the plinth, as authored.</summary>
        public const float FirstRingFoot = PlinthHeight + .02f;
        /// <summary>The smallest ring's outer radius against the stack's own: graduated down to half.</summary>
        public const float SmallestRingShare = .5f;
        /// <summary>How far over the peg's top a lifted ring hangs.</summary>
        public const float LiftClearance = .14f;
        /// <summary>How fast a ring travels to where it belongs, in metres a second, when motion is on.</summary>
        public const float RingSpeed = 3.2f;

        /// <summary>The three neon colours of the course's lanes, red, blue, green: the board draws its rings in them.</summary>
        private static readonly Color[] Palette = { new Color(1f, .27f, .35f), new Color(.27f, .66f, 1f), new Color(.30f, 1f, .58f) };

        /// <summary>A ring's colour on the board, by size: the largest red, then blue, then green, round again.</summary>
        public static Color RingColour(int size, int rings) => Palette[((rings - size) % Palette.Length + Palette.Length) % Palette.Length];

        /// <summary>A ring's outer radius on the authored stack, by size (1 the smallest): the stack's own for the largest.</summary>
        public static float RingRadius(int size, int rings) => rings <= 1 ? RingOuter
            : RingOuter * (SmallestRingShare + (1f - SmallestRingShare) * (size - 1) / (rings - 1));

        private readonly List<Transform> stacks = new List<Transform>();
        private readonly List<Vector3> pegFeet = new List<Vector3>();
        private readonly List<Transform> rings = new List<Transform>();
        private readonly List<Mesh> meshes = new List<Mesh>();
        private float scale = 1f, spread = 1f;
        private Func<RingTowerPuzzle> puzzle;
        private Func<bool> reducedMotion;

        /// <summary>The stacks, left to right as the camera sees them (west to east).</summary>
        public IReadOnlyList<Transform> Stacks => stacks;

        /// <summary>The playable rings, by size: [0] the smallest.</summary>
        public IReadOnlyList<Transform> Rings => rings;

        /// <summary>Where a stack's peg meets its plinth, in the world.</summary>
        public Vector3 PegFoot(int stack) => pegFeet[stack];

        /// <summary>The middle of the three stacks at the height of their rings: what the attempt's shot looks at.</summary>
        public Vector3 Centre => pegFeet.Count == 0 ? transform.position
            : (pegFeet[0] + pegFeet[pegFeet.Count - 1]) * .5f + Vector3.up * (FirstRingFoot * scale);

        /// <summary>
        /// Builds the tower on <paramref name="standing"/> (the three stacks, any order) under <paramref name="parent"/>, the
        /// arena's own root. Each stack's renderers are handed to <paramref name="conceal"/>, which switches them off and
        /// remembers them for the yard's restore. Null when there are not three stacks with a drawn mesh.
        /// </summary>
        public static CompetitionRingTower Build(Transform parent, IReadOnlyList<Transform> standing, int ringCount,
            Func<MiniGameRun> attempt, Func<bool> reducedMotion, Action<Renderer> conceal)
            => BuildFollowing(parent, standing, ringCount, () => attempt?.Invoke()?.Tower, reducedMotion, conceal);

        /// <summary>
        /// The same tower, its rings following <paramref name="puzzle"/> - a tower show's scripted solve
        /// (<see cref="CompetitionTowerShowRings"/>) rather than the player's attempt. Null as <see cref="Build"/> is.
        /// </summary>
        public static CompetitionRingTower BuildFollowing(Transform parent, IReadOnlyList<Transform> standing, int ringCount,
            Func<RingTowerPuzzle> puzzle, Func<bool> reducedMotion, Action<Renderer> conceal)
        {
            if (parent == null || standing == null || standing.Count != RingTowerPuzzle.Stacks) return null;
            var ordered = new List<Transform>(standing);
            ordered.Sort((a, b) => a.position.x.CompareTo(b.position.x));
            var found = new List<(Bounds box, Material ink, Material steel, Material neon, List<Renderer> drawn)>();
            foreach (var stack in ordered)
            {
                if (stack == null) return null;
                var drawn = new List<Renderer>();
                foreach (var renderer in stack.GetComponentsInChildren<MeshRenderer>())
                    if (renderer.enabled) drawn.Add(renderer);
                if (drawn.Count == 0) return null;
                var box = drawn[0].bounds;
                Material ink = null, steel = null, neon = null;
                foreach (var renderer in drawn)
                {
                    box.Encapsulate(renderer.bounds);
                    foreach (var material in renderer.sharedMaterials)
                    {
                        if (material == null) continue;
                        string name = material.name.ToLowerInvariant();
                        if (name.Contains("neon")) { if (neon == null) neon = material; }
                        else if (name.Contains("steel")) { if (steel == null) steel = material; }
                        else if (name.Contains("ink")) { if (ink == null) ink = material; }
                    }
                }
                // Any slot the names did not place: the first material standing in for it.
                var first = drawn[0].sharedMaterial;
                found.Add((box, ink ?? first, steel ?? ink ?? first, neon ?? first, drawn));
            }

            var root = new GameObject("Ring tower");
            root.transform.SetParent(parent, false);
            var tower = root.AddComponent<CompetitionRingTower>();
            tower.puzzle = puzzle; tower.reducedMotion = reducedMotion;
            // The placed stacks' own size against the authored one: a scaled import still gets rings to its own scale.
            var reference = found[0].box;
            tower.scale = reference.size.y > .01f ? reference.size.y / AuthoredHeight : 1f;
            float across = Mathf.Max(reference.size.x, reference.size.z);
            tower.spread = across > .01f ? across / RevealWidth : tower.scale;

            // The neon materials in the lanes' order where their names say it (red, blue, green), else the stacks' own order.
            var neons = new List<Material>();
            foreach (string colour in new[] { "red", "blue", "green" })
                foreach (var entry in found)
                    if (entry.neon != null && entry.neon.name.ToLowerInvariant().Contains(colour) && !neons.Contains(entry.neon)) neons.Add(entry.neon);
            foreach (var entry in found) if (entry.neon != null && !neons.Contains(entry.neon)) neons.Add(entry.neon);

            for (int i = 0; i < found.Count; i++)
            {
                var entry = found[i];
                foreach (var renderer in entry.drawn) conceal?.Invoke(renderer);
                var foot = new Vector3(entry.box.center.x, entry.box.min.y, entry.box.center.z);
                tower.stacks.Add(ordered[i]);
                tower.pegFeet.Add(foot);
                var base_ = new GameObject("Ring tower stack " + (i + 1)).transform;
                base_.SetParent(root.transform, false);
                base_.position = foot;
                tower.Part(base_, "Plinth", Box(PlinthWidth * tower.spread, PlinthHeight * tower.scale, PlinthWidth * tower.spread),
                    new Vector3(0f, PlinthHeight * .5f * tower.scale, 0f), entry.ink);
                tower.Part(base_, "Reveal", Box(RevealWidth * tower.spread, RevealHeight * tower.scale, RevealWidth * tower.spread),
                    new Vector3(0f, (PlinthHeight - .03f) * tower.scale, 0f), entry.neon);
                tower.Part(base_, "Peg", Cylinder(PegRadius * tower.spread, PegHeight * tower.scale, 16),
                    new Vector3(0f, PlinthHeight * tower.scale, 0f), entry.steel);
            }
            for (int size = 1; size <= ringCount; size++)
            {
                var material = neons.Count > 0 ? neons[(ringCount - size) % neons.Count] : found[0].neon;
                var ring = tower.Part(root.transform, "Ring tower ring " + size,
                    Ring(RingRadius(size, ringCount) * tower.spread, RingInner * tower.spread, RingHeight * tower.scale, RingSides), Vector3.zero, material);
                tower.rings.Add(ring);
            }
            tower.Place(snap: true);
            return tower;
        }

        private Transform Part(Transform parent, string name, Mesh mesh, Vector3 local, Material material)
        {
            var part = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            part.transform.SetParent(parent, false);
            part.transform.localPosition = local;
            part.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = part.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            meshes.Add(mesh);
            return part.transform;
        }

        private void LateUpdate() => Place(snap: reducedMotion != null && reducedMotion());

        private void OnDestroy()
        {
            foreach (var mesh in meshes)
                if (mesh != null) { if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh); }
            meshes.Clear();
        }

        /// <summary>Where a ring belongs now: on its stack at its level, or hanging over its peg when lifted.</summary>
        public Vector3 Home(int size)
        {
            var tower = puzzle?.Invoke();
            int stack = 0, level = rings.Count - size;
            bool lifted = false;
            if (tower != null)
                for (int s = 0; s < RingTowerPuzzle.Stacks; s++)
                {
                    var on = tower.Stack(s);
                    int at = -1;
                    for (int l = 0; l < on.Count; l++) if (on[l] == size) at = l;
                    if (at < 0) continue;
                    stack = s; level = at; lifted = tower.Lifted == s && at == on.Count - 1;
                    break;
                }
            var foot = pegFeet[Mathf.Clamp(stack, 0, pegFeet.Count - 1)];
            float y = lifted ? (PlinthHeight + PegHeight + LiftClearance) * scale
                : (FirstRingFoot + level * (RingHeight + RingGap)) * scale;
            return foot + Vector3.up * y;
        }

        /// <summary>Puts every ring where it belongs: at once when <paramref name="snap"/>, otherwise a step towards it.</summary>
        public void Place(bool snap)
        {
            float step = RingSpeed * Mathf.Max(0f, Time.unscaledDeltaTime) * Mathf.Max(.2f, scale);
            // The stacks stand metres apart: a ring snaps only on a jump wider than the whole tower, and crosses faster the wider it is.
            float span = pegFeet.Count > 1 ? (pegFeet[pegFeet.Count - 1] - pegFeet[0]).magnitude : 0f, snapBeyond = span + 2f * Mathf.Max(.2f, scale);
            for (int size = 1; size <= rings.Count; size++)
            {
                var ring = rings[size - 1];
                if (ring == null) continue;
                var home = Home(size);
                if (snap || (ring.position - home).sqrMagnitude > snapBeyond * snapBeyond) { ring.position = home; continue; }
                var at = ring.position;
                // Up first, then across, then down: a ring is lifted off its peg, never dragged through the next.
                float top = (PlinthHeight + PegHeight + LiftClearance) * scale + pegFeet[0].y;
                bool across = Mathf.Abs(at.x - home.x) > .001f || Mathf.Abs(at.z - home.z) > .001f;
                if (across && at.y < top - .001f) ring.position = Vector3.MoveTowards(at, new Vector3(at.x, top, at.z), step);
                else if (across) ring.position = Vector3.MoveTowards(at, new Vector3(home.x, top, home.z), step * Mathf.Max(1f, span / 4f));
                else ring.position = Vector3.MoveTowards(at, home, step);
            }
        }

        // ---------------------------------------------------------------- meshes, built here so a build needs nothing read back

        /// <summary>An annulus extruded to a height, closed top and bottom, as bb_build.ring makes it: foot at y 0, smooth sides.</summary>
        public static Mesh Ring(float outer, float inner, float height, int sides)
        {
            sides = Mathf.Max(3, sides);
            var vertices = new List<Vector3>(); var normals = new List<Vector3>(); var triangles = new List<int>();
            // Sides: outer facing out, inner facing in, each its own ring of vertices so the sides shade smooth.
            for (int wall = 0; wall < 2; wall++)
            {
                float radius = wall == 0 ? outer : inner, facing = wall == 0 ? 1f : -1f;
                int start = vertices.Count;
                for (int i = 0; i <= sides; i++)
                {
                    float a = 2f * Mathf.PI * i / sides;
                    var out_ = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    vertices.Add(out_ * radius); normals.Add(out_ * facing);
                    vertices.Add(out_ * radius + Vector3.up * height); normals.Add(out_ * facing);
                }
                for (int i = 0; i < sides; i++)
                {
                    int b = start + i * 2;
                    if (wall == 0) { triangles.AddRange(new[] { b, b + 1, b + 3, b, b + 3, b + 2 }); }
                    else { triangles.AddRange(new[] { b, b + 3, b + 1, b, b + 2, b + 3 }); }
                }
            }
            // Top and bottom: flat, between the two radii.
            for (int cap = 0; cap < 2; cap++)
            {
                float y = cap == 0 ? height : 0f; var up = cap == 0 ? Vector3.up : Vector3.down;
                int start = vertices.Count;
                for (int i = 0; i <= sides; i++)
                {
                    float a = 2f * Mathf.PI * i / sides;
                    var out_ = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    vertices.Add(out_ * outer + Vector3.up * y); normals.Add(up);
                    vertices.Add(out_ * inner + Vector3.up * y); normals.Add(up);
                }
                for (int i = 0; i < sides; i++)
                {
                    int b = start + i * 2;
                    if (cap == 0) { triangles.AddRange(new[] { b, b + 1, b + 3, b, b + 3, b + 2 }); }
                    else { triangles.AddRange(new[] { b, b + 3, b + 1, b, b + 2, b + 3 }); }
                }
            }
            return Finish("Ring tower ring", vertices, normals, triangles);
        }

        /// <summary>A closed cylinder standing on y 0.</summary>
        public static Mesh Cylinder(float radius, float height, int sides)
        {
            sides = Mathf.Max(3, sides);
            var vertices = new List<Vector3>(); var normals = new List<Vector3>(); var triangles = new List<int>();
            for (int i = 0; i <= sides; i++)
            {
                float a = 2f * Mathf.PI * i / sides;
                var out_ = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                vertices.Add(out_ * radius); normals.Add(out_);
                vertices.Add(out_ * radius + Vector3.up * height); normals.Add(out_);
            }
            for (int i = 0; i < sides; i++) { int b = i * 2; triangles.AddRange(new[] { b, b + 1, b + 3, b, b + 3, b + 2 }); }
            int centre = vertices.Count;
            vertices.Add(Vector3.up * height); normals.Add(Vector3.up);
            for (int i = 0; i <= sides; i++)
            {
                float a = 2f * Mathf.PI * i / sides;
                vertices.Add(new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius + Vector3.up * height); normals.Add(Vector3.up);
            }
            for (int i = 0; i < sides; i++) triangles.AddRange(new[] { centre, centre + 2 + i, centre + 1 + i });
            return Finish("Ring tower peg", vertices, normals, triangles);
        }

        /// <summary>A box centred on its origin.</summary>
        public static Mesh Box(float width, float height, float depth)
        {
            var vertices = new List<Vector3>(); var normals = new List<Vector3>(); var triangles = new List<int>();
            var half = new Vector3(width, height, depth) * .5f;
            foreach (var normal in new[] { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back })
            {
                var u = Mathf.Abs(normal.y) > .5f ? Vector3.right : Vector3.up;
                var v = Vector3.Cross(normal, u);
                int b = vertices.Count;
                foreach (var corner in new[] { -u - v, -u + v, u + v, u - v })
                {
                    vertices.Add(Vector3.Scale(normal + corner, half)); normals.Add(normal);
                }
                // Clockwise seen from the face's own side, as Unity draws a front face.
                triangles.AddRange(new[] { b, b + 2, b + 1, b, b + 3, b + 2 });
            }
            return Finish("Ring tower box", vertices, normals, triangles);
        }

        private static Mesh Finish(string name, List<Vector3> vertices, List<Vector3> normals, List<int> triangles)
        {
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
