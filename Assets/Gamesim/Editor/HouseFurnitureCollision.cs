using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Gamesim.House;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Gamesim.Editor
{
    /// <summary>
    /// Gives the furniture a body.
    ///
    /// <para>Every prop in this house was placed collider-free, and for a while that was the right
    /// call: with one raycast mask in the whole project, a sofa with a collider meant a click that
    /// grazed the sofa did nothing, and a conversation behind the sofa could not be witnessed. The
    /// price was that the NavMeshSurface - which bakes from physics colliders - saw no furniture at
    /// all, so houseguests walked through beds and the entire dining set. Twenty-two invisible boxes
    /// left over from the prototype were the only reason anything blocked.</para>
    ///
    /// <para><see cref="HouseLayers"/> retired that trade. Collision lives on
    /// <see cref="HouseLayers.Furniture"/>, which the bake reads, the mouse can hit, and no sightline
    /// can see. So the props can finally be solid.</para>
    ///
    /// <para>The collider goes on a child called <see cref="ChildName"/> rather than on the prop
    /// root, because a layer is not only a physics fact: cameras cull by it and lights list it. The
    /// art stays on the layer it was authored on and the proxy carries the physics, which also makes
    /// the pass reversible in one sweep - delete every <see cref="ChildName"/> and the house is
    /// exactly as it was.</para>
    /// </summary>
    public static class HouseFurnitureCollision
    {
        private const string Fit = "Gamesim/Fit furniture collision";
        private const string Clear = "Gamesim/Remove fitted furniture collision";

        /// <summary>The name of the proxy child. Nothing else in the scene may use it.</summary>
        public const string ChildName = "Collision";

        /// <summary>
        /// A proxy that was fitted and then switched off again, with the reason in its name.
        ///
        /// <para>Switched off rather than deleted, and named rather than silently inactive, because
        /// the next person to wonder why the bookcase by the yard door is walk-through will be
        /// looking at the hierarchy when they wonder it.</para>
        /// </summary>
        public const string OffPrefix = ChildName + " (off: ";

        /// <summary>Every proxy this pass owns, live or switched off.</summary>
        public static bool IsProxy(Transform t) => t.name.StartsWith(ChildName, StringComparison.Ordinal);

        /// <summary>
        /// A person walks around it rather than over it.
        ///
        /// <para>Footprint is the <em>smaller</em> horizontal side, so a bookcase 0.80 wide and 0.32
        /// deep is furniture while a 0.24 speaker pole is not. Height is generous enough to take the
        /// cot (0.52) and the coffee tables (0.42) and to leave the rugs (0.03), the cable run (0.04)
        /// and the competition circle (0.05) alone.</para>
        /// </summary>
        private const float SolidFootprint = 0.30f;
        private const float SolidHeight = 0.40f;

        /// <summary>
        /// How far off the floor a prop may start and still be furniture.
        ///
        /// <para>This is the whole of the tabletop rule. Mugs, bowls, laptops, candles and table
        /// lamps are all above it and none of them should stop anybody walking; they are also the
        /// props most likely to punch a hole in the mesh for no reason, because the bake blocks a
        /// voxel column from any height a body would meet.</para>
        /// </summary>
        private const float FloorTolerance = 0.05f;

        /// <summary>
        /// Named exclusions, because size cannot tell these apart from real furniture.
        ///
        /// <para><c>bb_shell_house</c> is permanent and the reason is measured: baking the render
        /// mesh of the house shell - which is what "collide everything" looks like - drops room
        /// reachability from 28 of 28 pairs to 21, every missing pair something-to-Yard. The shell's
        /// collision is the prototype's wall boxes and stays that way.</para>
        ///
        /// <para>Nothing else is here on suspicion. A prop that might seal a doorway is a question the
        /// reachability check answers in a second, and a name added to this list on a hunch is a
        /// piece of furniture that stays walk-through forever without anybody remembering why.</para>
        /// </summary>
        private static readonly HashSet<string> Never = new HashSet<string>(StringComparer.Ordinal)
        {
            "bb_shell_house",
        };

        [MenuItem(Fit)]
        public static void FitCollision()
        {
            if (!TryFindRoot(out var root)) return;

            var tally = new Tally();
            Walk(root, tally);

            EditorSceneManager.MarkSceneDirty(root.gameObject.scene);

            var sb = new StringBuilder();
            sb.AppendLine("[Gamesim] Furniture collision - fitted " + tally.Fitted + " new, refitted " + tally.Refitted
                          + ", moved " + tally.Relayered + " authored _col shapes onto layer " + HouseLayers.Furniture + ".");
            sb.AppendLine("Left alone: " + tally.TooSmall + " too small to walk around, " + tally.OffFloor
                          + " standing on a surface rather than the floor, " + tally.Protected
                          + " standing on a route destination, " + tally.Named + " excluded by name, "
                          + tally.Grouped + " procedural groups walked into.");
            foreach (var line in tally.Skipped) sb.AppendLine("   " + line);
            sb.Append("Save the scene to keep them, then rebake - the mesh does not change until you do.");
            Debug.Log(sb.ToString());
        }

        private sealed class Tally
        {
            public int Fitted, Refitted, Relayered, TooSmall, OffFloor, Named, Grouped, Protected;
            public readonly List<string> Skipped = new List<string>();
        }

        private static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

        /// <summary>
        /// Whether this prop is standing on somewhere navigation routes to.
        ///
        /// <para>Furniture on a room marker is the one way a collider can do what carving did: take
        /// the floor out from under the point every route into that room ends at. It is not
        /// hypothetical - the nomination room's marker sits in the middle of its own round table, so
        /// giving the table a body cut the whole room off the mesh.</para>
        ///
        /// <para>Containment only, not a radius. <c>HouseNavigationObstacles</c> keeps a metre and a
        /// half clear because a carving obstacle deletes a whole bounding box at runtime, after the
        /// director has already picked its seats. A baked collider has neither problem, and a radius
        /// that generous here would spare the Head of Household bed at 1.16 m and the diary chair at
        /// 0.45 m - the two props this whole stage exists to make solid.</para>
        /// </summary>
        private static bool StandsOnADestination(Transform prop, Bounds local)
        {
            var world = new Bounds(prop.TransformPoint(local.center), Abs(Vector3.Scale(local.size, prop.lossyScale)));
            foreach (var point in Destinations())
            {
                var flat = new Vector3(point.x, world.center.y, point.z);
                if (world.Contains(flat)) return true;
            }
            return false;
        }

        private static IEnumerable<Vector3> Destinations()
        {
            foreach (var marker in UnityEngine.Object.FindObjectsByType<HouseRoomMarker>(FindObjectsInactive.Include))
                yield return marker.transform.position;
            foreach (var npc in UnityEngine.Object.FindObjectsByType<HouseNpc>(FindObjectsInactive.Include))
                yield return npc.transform.position;
            foreach (var player in UnityEngine.Object.FindObjectsByType<HousePlayerController>(FindObjectsInactive.Include))
                yield return player.transform.position;
        }

        /// <summary>
        /// Walks the set, treating each prefab instance as one prop and descending through anything
        /// that is not one.
        ///
        /// <para>The descent matters. The prototype's procedural groups - the competition course, the
        /// planting, the entrance run - are dozens of primitives under a single transform, and a box
        /// around a whole group would wall off half the yard. But real props are parked inside some
        /// of those groups: the course's crates and stacks, the planting's tubs. Stopping at the top
        /// level would leave them walk-through for no reason other than where they were filed.</para>
        /// </summary>
        private static void Walk(Transform parent, Tally tally)
        {
            foreach (Transform prop in parent)
            {
                // This pass's own output. Walking into it would measure the proxy instead of the art.
                if (IsProxy(prop)) continue;

                if (Never.Contains(prop.name))
                { tally.Named++; tally.Skipped.Add(prop.name + " (excluded by name)"); continue; }

                if (PrefabUtility.GetPrefabInstanceHandle(prop.gameObject) == null && prop.childCount > 0)
                { tally.Grouped++; Walk(prop, tally); continue; }

                // An authored _col child is a real collision mesh from the export and beats anything
                // this pass could guess. It only needs telling which layer it is on.
                var authored = prop.GetComponentsInChildren<Collider>(true)
                    .Where(c => c.name.EndsWith(AuthoredAssetImporter.ColliderSuffix, StringComparison.Ordinal))
                    .ToList();
                if (authored.Count > 0)
                {
                    foreach (var collider in authored)
                        if (collider.gameObject.layer != HouseLayers.Furniture)
                        {
                            Undo.RecordObject(collider.gameObject, "Furniture collision");
                            collider.gameObject.layer = HouseLayers.Furniture;
                            tally.Relayered++;
                        }
                    continue;
                }

                switch (Judge(prop, out var local))
                {
                    case Verdict.AlreadySolid:
                        tally.Skipped.Add(prop.name + " (already has a collider of its own)"); continue;
                    case Verdict.OffFloor: tally.OffFloor++; continue;
                    case Verdict.TooSmall: tally.TooSmall++; continue;
                    case Verdict.OnADestination:
                        tally.Protected++; tally.Skipped.Add(prop.name + " (stands on a route destination)"); continue;
                    case Verdict.NothingToMeasure: continue;
                }

                if (Attach(prop, local)) tally.Refitted++; else tally.Fitted++;
            }
        }

        /// <summary>What the pass decided about one prop, and why.</summary>
        public enum Verdict
        {
            /// <summary>Solid enough, on the floor, and not standing anywhere navigation needs.</summary>
            Fit,
            /// <summary>Set dressing: too thin or too low to walk around.</summary>
            TooSmall,
            /// <summary>On a tabletop, a shelf or a rail rather than on the floor.</summary>
            OffFloor,
            /// <summary>On a room marker or a spawn point, so the floor under it has to stay.</summary>
            OnADestination,
            /// <summary>Carries collision of its own already, authored or otherwise.</summary>
            AlreadySolid,
            /// <summary>No renderers, so there is no shape to fit a box to.</summary>
            NothingToMeasure,
        }

        /// <summary>
        /// Whether this prop should be solid, and the box to use if it should.
        ///
        /// <para>Public and named so the rule can be tested one prop at a time rather than only by
        /// its effect on a whole house.</para>
        /// </summary>
        public static Verdict Judge(Transform prop, out Bounds local)
        {
            local = default;

            // Something already solid is already the bake's business. Boxing it a second time
            // would only widen its footprint by whatever the render mesh overhangs.
            if (prop.GetComponent<Collider>() != null) return Verdict.AlreadySolid;
            if (!TryMeasure(prop, out local, out float floor)) return Verdict.NothingToMeasure;
            if (floor > FloorTolerance) return Verdict.OffFloor;

            // The box is measured in the prop's own space so it turns with the prop, but the
            // thresholds are metres and have to be judged in metres. Judging the local numbers
            // instead put a waist-high box around eighteen floor tiles - a unit cube squashed to
            // 0.02 m still measures 1.0 before its scale is applied - and around six stanchion
            // poles 0.09 m thick. Scale first, then decide.
            var world = Abs(Vector3.Scale(local.size, prop.lossyScale));
            if (Mathf.Min(world.x, world.z) < SolidFootprint || world.y < SolidHeight) return Verdict.TooSmall;

            return StandsOnADestination(prop, local) ? Verdict.OnADestination : Verdict.Fit;
        }

        [MenuItem(Clear)]
        public static void ClearCollision()
        {
            if (!TryFindRoot(out var root)) return;

            int removed = 0;
            foreach (var child in root.GetComponentsInChildren<Transform>(true)
                         .Where(IsProxy).ToList())
            {
                Undo.DestroyObjectImmediate(child.gameObject);
                removed++;
            }

            EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
            Debug.Log("[Gamesim] Furniture collision - removed " + removed
                      + ". Authored _col shapes are untouched; rebake to put the floor back.");
        }

        /// <summary>
        /// Puts the proxy under the prop, or resizes the one already there. Returns whether it had
        /// to be created.
        /// </summary>
        public static bool Attach(Transform prop, Bounds local)
        {
            Transform existing = null;
            foreach (Transform child in prop) if (IsProxy(child)) { existing = child; break; }
            bool existed = existing != null;
            if (!existed)
            {
                var go = new GameObject(ChildName);
                Undo.RegisterCreatedObjectUndo(go, "Furniture collision");
                go.transform.SetParent(prop, false);
                existing = go.transform;
            }

            // A re-fit starts from a clean slate: a proxy switched off by an earlier resolve has to
            // earn that again against the geometry as it stands now.
            existing.name = ChildName;
            existing.gameObject.SetActive(true);
            existing.localPosition = Vector3.zero;
            existing.localRotation = Quaternion.identity;
            existing.localScale = Vector3.one;
            existing.gameObject.layer = HouseLayers.Furniture;
            // Nothing should light, bake or occlude from a shape that is not there to be seen.
            GameObjectUtility.SetStaticEditorFlags(existing.gameObject, 0);

            var box = existing.GetComponent<BoxCollider>();
            if (box == null) box = Undo.AddComponent<BoxCollider>(existing.gameObject);
            else Undo.RecordObject(box, "Furniture collision");
            box.center = local.center;
            box.size = local.size;
            return existed;
        }

        /// <summary>
        /// The prop's own renderers, expressed as one box in the prop's local space. Local, so a
        /// chair turned to face the table gets a box turned with it rather than an axis-aligned one
        /// that is too wide in every direction it is not.
        /// </summary>
        public static bool TryMeasure(Transform prop, out Bounds local, out float floor)
        {
            local = default;
            floor = 0f;
            var renderers = prop.GetComponentsInChildren<Renderer>(true)
                .Where(r => !(r is ParticleSystemRenderer)).ToList();
            if (renderers.Count == 0) return false;

            var toLocal = prop.worldToLocalMatrix;
            bool started = false;
            float worldFloor = float.MaxValue;
            foreach (var renderer in renderers)
            {
                var b = renderer.localBounds;
                var toProp = toLocal * renderer.transform.localToWorldMatrix;
                for (int corner = 0; corner < 8; corner++)
                {
                    var point = new Vector3(
                        (corner & 1) == 0 ? b.min.x : b.max.x,
                        (corner & 2) == 0 ? b.min.y : b.max.y,
                        (corner & 4) == 0 ? b.min.z : b.max.z);
                    var inProp = toProp.MultiplyPoint3x4(point);
                    if (!started) { local = new Bounds(inProp, Vector3.zero); started = true; }
                    else local.Encapsulate(inProp);
                }
                worldFloor = Mathf.Min(worldFloor, renderer.bounds.min.y);
            }

            floor = worldFloor;
            return started;
        }

        private static bool TryFindRoot(out Transform root)
        {
            var scene = EditorSceneManager.GetActiveScene();
            root = scene.GetRootGameObjects()
                .SelectMany(g => g.GetComponentsInChildren<Transform>(true))
                .FirstOrDefault(t => t.name == HouseSetPieces.RootName);
            if (root == null)
                Debug.LogWarning("[Gamesim] Furniture collision - no set-piece root in the open scene. Open EpisodeHouse first.");
            return root != null;
        }
    }
}
