using System;
using System.Collections.Generic;
using System.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace Gamesim.Editor
{
    /// <summary>
    /// Asks the baked NavMesh which pieces of furniture a houseguest can walk straight through.
    ///
    /// <para>The scene's <see cref="NavMeshSurface"/> bakes from <b>physics colliders</b>, so an
    /// object with a renderer and no collider is invisible to it — the floor is baked flat underneath
    /// and an agent paths through the object exactly as if it were not there. Nothing about that is
    /// visible in the scene view, in the hierarchy, or in the NPC movement code, which is why this
    /// exists: it walks the geometry and asks the navigation system rather than the author.</para>
    ///
    /// <para>The test is a <see cref="NavMesh.Raycast"/> across each object's footprint, twice —
    /// along X and along Z, from just outside one edge to just outside the other, at floor height. A
    /// raycast that reaches the far side without hitting anything means there is unbroken walkable
    /// surface through the middle of the object.</para>
    /// </summary>
    public static class HouseNavigationAudit
    {
        /// <summary>
        /// How tall something has to be before walking through it would be visible.
        ///
        /// <para>Rugs, floor tiles and painted strips are flat by design and are supposed to be walked
        /// on. A quarter of a metre is roughly the point at which a thing reads as an object rather
        /// than as part of the floor.</para>
        /// </summary>
        private const float SolidHeight = 0.25f;

        /// <summary>Ignored below this footprint: cables, trim and edging are not obstacles.</summary>
        private const float SolidFootprint = 0.05f;

        /// <summary>
        /// The bake's own agent radius, which is how far a hole spreads beyond the thing that made it.
        ///
        /// <para>Shots have to start and finish outside that spread or both ends snap into the hole —
        /// possibly onto the same side of it — and the shot between them proves nothing. This is the
        /// difference between a test that asks the NavMesh a question and one that agrees with
        /// whatever you already believed.</para>
        /// </summary>
        private const float BakeAgentRadius = 0.5f;

        /// <summary>A floor slab is flat and enormous; walking "through" one is the point of it.</summary>
        private const float FloorArea = 8f;
        private const float FloorHeight = 0.5f;

        /// <summary>What a piece of furniture looked like to the navigation system.</summary>
        private struct Finding
        {
            public string Path;
            public Vector3 Size;
            public bool HasCollider;
            public bool WalkableThrough;
        }

        [MenuItem("Gamesim/Audit house navigation")]
        public static void Audit()
        {
            var surface = UnityEngine.Object.FindObjectsByType<NavMeshSurface>(FindObjectsInactive.Include)
                .FirstOrDefault();
            if (surface == null)
            {
                EditorUtility.DisplayDialog("House navigation",
                    "This scene has no NavMeshSurface, so there is nothing to audit.", "Close");
                return;
            }
            if (surface.navMeshData == null)
            {
                EditorUtility.DisplayDialog("House navigation",
                    "The NavMeshSurface has no baked data. Bake it before auditing.", "Close");
                return;
            }

            // The surface only registers its data automatically in Play Mode. Registering a second
            // copy of an already-live NavMesh would double the surface under every query, so this
            // only adds one when nothing is there, and always removes exactly what it added.
            var instance = default(NavMeshDataInstance);
            bool alreadyLive = NavMesh.SamplePosition(surface.transform.position, out _, 50f, NavMesh.AllAreas);
            if (!alreadyLive)
            {
                instance = NavMesh.AddNavMeshData(surface.navMeshData,
                    surface.transform.position, surface.transform.rotation);
                if (!instance.valid)
                {
                    EditorUtility.DisplayDialog("House navigation",
                        "The baked NavMesh could not be registered for querying.", "Close");
                    return;
                }
            }

            try { Report(Collect(surface)); Reachability(); }
            finally { if (instance.valid) instance.Remove(); }
        }

        private static List<Finding> Collect(NavMeshSurface surface)
        {
            var findings = new List<Finding>();
            foreach (var renderer in surface.GetComponentsInChildren<Renderer>(true))
            {
                if (!renderer.gameObject.activeInHierarchy) continue;
                var bounds = renderer.bounds;
                if (bounds.size.y < SolidHeight) continue;
                if (bounds.size.x < SolidFootprint || bounds.size.z < SolidFootprint) continue;
                if (bounds.size.x * bounds.size.z > FloorArea && bounds.size.y < FloorHeight) continue;

                // Floors and walls are the bake's own input; a wall reading as "walkable through"
                // would mean the bake failed outright rather than that the wall is decorative, and
                // that is a different report. Only things standing ON the floor are of interest.
                var go = renderer.gameObject;
                findings.Add(new Finding
                {
                    Path = Path(go.transform, surface.transform),
                    Size = bounds.size,
                    HasCollider = go.GetComponent<Collider>() != null,
                    WalkableThrough = PassesThrough(bounds),
                });
            }
            return findings;
        }

        /// <summary>
        /// Whether unbroken walkable surface runs through the middle of these bounds.
        ///
        /// <para>Cast at ankle height rather than at the object's centre: the NavMesh sits on the
        /// floor, and a shot through the middle of a tall object would start above it.</para>
        /// </summary>
        private static bool PassesThrough(Bounds bounds)
        {
            float y = bounds.min.y + 0.05f;
            var centre = new Vector3(bounds.center.x, y, bounds.center.z);
            if (!NavMesh.SamplePosition(centre, out var hit, 0.6f, NavMesh.AllAreas)) return false;

            return Clear(hit.position, bounds.extents.x, Vector3.right)
                || Clear(hit.position, bounds.extents.z, Vector3.forward);
        }

        /// <summary>
        /// Whether an agent can cross this object along one axis.
        ///
        /// <para>Both ends start a clear margin beyond where a hole would reach, and both must
        /// <i>stay</i> on their own side after being snapped to the mesh — a sample that slid around
        /// the obstacle would otherwise draw a line that never crosses it and call that clear.</para>
        /// </summary>
        private static bool Clear(Vector3 centre, float extent, Vector3 axis)
        {
            float reach = extent + BakeAgentRadius + 0.35f;
            if (!NavMesh.SamplePosition(centre - axis * reach, out var start, 0.4f, NavMesh.AllAreas)) return false;
            if (!NavMesh.SamplePosition(centre + axis * reach, out var end, 0.4f, NavMesh.AllAreas)) return false;

            float from = Vector3.Dot(start.position - centre, axis);
            float to = Vector3.Dot(end.position - centre, axis);
            if (from > -extent || to < extent) return false;

            return !NavMesh.Raycast(start.position, end.position, out _, NavMesh.AllAreas);
        }

        private static void Report(List<Finding> findings)
        {
            var through = findings.Where(f => f.WalkableThrough)
                .OrderByDescending(f => f.Size.x * f.Size.z)
                .ToList();

            int uncollided = through.Count(f => !f.HasCollider);
            var text = new System.Text.StringBuilder();
            text.AppendLine("[Gamesim] House navigation audit");
            text.AppendLine(findings.Count + " standing objects checked, " + through.Count
                            + " an agent can walk straight through.");
            text.AppendLine("  " + uncollided + " have no collider at all, so the bake never saw them.");
            text.AppendLine("  " + (through.Count - uncollided) + " do have one, which means a stale bake instead.");
            text.AppendLine();
            foreach (var finding in through)
                text.AppendLine(string.Format("  {0,-46} {1:0.00} x {2:0.00} x {3:0.00}   {4}",
                    finding.Path, finding.Size.x, finding.Size.y, finding.Size.z,
                    finding.HasCollider ? "has a collider — check the bake is current" : "NO COLLIDER — invisible to the bake"));

            if (through.Count == 0) text.AppendLine("  Nothing. Every standing object turns an agent aside.");
            Debug.Log(text.ToString());
        }

        /// <summary>
        /// Whether every room can still be walked to from every other room.
        ///
        /// <para>The other half of this problem, and the one that would be worse. Carving furniture
        /// out of the navigation mesh is the fix for walking through a bed, but a wardrobe against a
        /// doorway carves the doorway shut, and a houseguest who cannot reach the kitchen is a
        /// harder failure to spot than one who walks through the sofa on the way there — they simply
        /// stand still and nothing says why.</para>
        ///
        /// <para>Play Mode is the authority. Carving does take effect in the editor — fitting the
        /// obstacles drops the walk-through count here without entering Play Mode — but only a
        /// running game has every agent bound and every room occupied, so a difference between the
        /// two readings is worth believing the running one.</para>
        /// </summary>
        private static void Reachability()
        {
            var markers = UnityEngine.Object.FindObjectsByType<Gamesim.House.HouseRoomMarker>(FindObjectsInactive.Exclude)
                .Where(marker => !string.IsNullOrEmpty(marker.RoomName))
                .OrderBy(marker => marker.RoomName, System.StringComparer.Ordinal)
                .ToList();
            if (markers.Count < 2) return;

            var grounded = new List<KeyValuePair<string, Vector3>>();
            var adrift = new List<string>();
            foreach (var marker in markers)
            {
                if (NavMesh.SamplePosition(marker.transform.position, out var hit, 2f, NavMesh.AllAreas))
                    grounded.Add(new KeyValuePair<string, Vector3>(marker.RoomName, hit.position));
                else adrift.Add(marker.RoomName);
            }

            var broken = new List<string>();
            var path = new NavMeshPath();
            for (int a = 0; a < grounded.Count; a++)
                for (int b = a + 1; b < grounded.Count; b++)
                    if (!NavMesh.CalculatePath(grounded[a].Value, grounded[b].Value, NavMesh.AllAreas, path)
                        || path.status != NavMeshPathStatus.PathComplete)
                        broken.Add(grounded[a].Key + " -> " + grounded[b].Key);

            var text = new System.Text.StringBuilder();
            text.AppendLine("[Gamesim] House reachability"
                            + (Application.isPlaying ? "  (play mode)" : "  (edit mode)"));
            text.AppendLine(grounded.Count + " rooms on the mesh, "
                            + (grounded.Count * (grounded.Count - 1) / 2 - broken.Count) + " of "
                            + (grounded.Count * (grounded.Count - 1) / 2) + " room pairs reachable.");
            foreach (var room in adrift) text.AppendLine("  NO FLOOR UNDER  " + room);
            foreach (var pair in broken) text.AppendLine("  UNREACHABLE     " + pair);
            if (adrift.Count == 0 && broken.Count == 0) text.AppendLine("  Every room reaches every other room.");
            if (adrift.Count > 0 || broken.Count > 0) Debug.LogWarning(text.ToString());
            else Debug.Log(text.ToString());
        }

        private static string Path(Transform node, Transform stopAt)
        {
            var parts = new List<string>();
            for (var t = node; t != null && t != stopAt; t = t.parent) parts.Add(t.name);
            parts.Reverse();
            return string.Join("/", parts);
        }
    }
}
