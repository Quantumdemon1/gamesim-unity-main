using System.Collections.Generic;
using System.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace Gamesim.Editor
{
    /// <summary>
    /// Makes the set dressing turn a houseguest aside.
    ///
    /// <para>Every piece of furniture in this house is built collider-free on purpose.
    /// <c>HouseSetPieces.Shape</c> destroys the collider on each primitive it creates and strips them
    /// from every imported model, and the reason is in
    /// <see cref="Gamesim.House.HousePlayerController"/>: clicking to move takes the <b>first</b>
    /// raycast hit and walks there only if it carries <see cref="Gamesim.House.HouseWalkable"/>.
    /// Give a sofa a collider and every click whose ray grazes that sofa first silently does nothing
    /// — the floor behind the furniture develops dead spots. Sightline checks in
    /// <c>HouseInteraction</c> would change too.</para>
    ///
    /// <para>The cost of that decision was invisible: the <see cref="NavMeshSurface"/> bakes from
    /// physics colliders, so furniture with no collider is baked straight over. The floor under every
    /// bed, sofa, counter and bookcase is walkable, and agents path through them — which is what a
    /// houseguest crossing a double bed on the way to the kitchen actually is.</para>
    ///
    /// <para><b>A <see cref="NavMeshObstacle"/> is not a <see cref="Collider"/>.</b> It takes no part
    /// in any raycast, so clicking and sightlines are untouched, and it carves the navigation mesh at
    /// runtime instead. Carving only while stationary means the furniture cuts its hole once on load
    /// and then costs nothing. No re-bake, and every one of them is removable in a single pass.</para>
    /// </summary>
    public static class HouseNavigationObstacles
    {
        private const string Fit = "Gamesim/Fit navigation obstacles to set dressing";
        private const string Clear = "Gamesim/Remove fitted navigation obstacles";

        /// <summary>
        /// Below this, walking through something would not read as a mistake.
        ///
        /// <para>Deliberately the same numbers the audit uses to decide what counts as an object, so
        /// the thing that reports the problem and the thing that fixes it cannot disagree about what
        /// the problem is.</para>
        /// </summary>
        private const float SolidHeight = 0.25f;
        private const float SolidFootprint = 0.05f;

        /// <summary>
        /// Floor that must stay clear around anything navigation depends on.
        ///
        /// <para>Learned the hard way. Fitting obstacles to everything solid took seventeen PlayMode
        /// tests down at once: furniture standing on a room's marker carved away the very point the
        /// player and every NPC route to, and furniture standing where a houseguest spawns stopped
        /// them binding to the mesh at all — "NPC carving did not clear to a safe floor binding".</para>
        ///
        /// <para>A metre and a half from the surface of the box, which covers the destination sample
        /// radius, the final approach, and a houseguest's own carving obstacle needing somewhere to
        /// sit. The cost is that a sofa parked on a room marker stays walkable; a walkable sofa is a
        /// much smaller problem than a room nobody can enter.</para>
        /// </summary>
        private const float ProtectedRadius = 1.5f;

        [MenuItem(Fit)]
        public static void FitObstacles()
        {
            if (!TryFindSurface(out var surface)) return;

            var protect = ProtectedPoints();
            int fitted = 0, skippedCollider = 0, already = 0, skippedProtected = 0;
            foreach (var renderer in surface.GetComponentsInChildren<Renderer>(true))
            {
                var go = renderer.gameObject;
                if (!Qualifies(renderer)) continue;

                // Never carve the floor out from under a route's destination or a spawn point.
                if (protect.Any(point => renderer.bounds.SqrDistance(point) < ProtectedRadius * ProtectedRadius))
                { skippedProtected++; continue; }

                // Anything with a collider is already the bake's business. Carving on top of a baked
                // hole is harmless but pointless, and it would double the obstacle count for no gain.
                if (go.GetComponent<Collider>() != null) { skippedCollider++; continue; }
                if (go.GetComponent<NavMeshObstacle>() != null) { already++; continue; }

                var obstacle = Undo.AddComponent<NavMeshObstacle>(go);
                obstacle.shape = NavMeshObstacleShape.Box;
                // Local bounds, so the obstacle inherits the object's own rotation and scale rather
                // than an axis-aligned box that is too big in every direction a chair is turned.
                var local = renderer.localBounds;
                obstacle.center = local.center;
                obstacle.size = local.size;
                obstacle.carving = true;
                // Set dressing never moves. Carving only when stationary means each piece cuts its
                // hole once and then stops costing anything at all.
                obstacle.carveOnlyStationary = true;
                fitted++;
            }

            EditorSceneManager.MarkSceneDirty(surface.gameObject.scene);
            Debug.Log("[Gamesim] Navigation obstacles · fitted " + fitted
                      + ", already had one " + already
                      + ", left to the bake because they have a collider " + skippedCollider
                      + ", left clear because they sit on a route destination or spawn point " + skippedProtected
                      + ".\nSave the scene to keep them, and run the audit in Play Mode to confirm the carving landed.");
        }

        /// <summary>Takes them all off again, so the change is one menu item to undo.</summary>
        [MenuItem(Clear)]
        public static void RemoveObstacles()
        {
            if (!TryFindSurface(out var surface)) return;

            int removed = 0;
            foreach (var obstacle in surface.GetComponentsInChildren<NavMeshObstacle>(true).ToList())
            {
                // Never touch a houseguest's own obstacle: HouseNpcMotion owns it, validates that
                // there is exactly one, and fails its binding if anything else has been added or
                // taken away.
                if (obstacle.GetComponentInParent<Gamesim.House.HouseNpc>() != null) continue;
                Undo.DestroyObjectImmediate(obstacle);
                removed++;
            }

            EditorSceneManager.MarkSceneDirty(surface.gameObject.scene);
            Debug.Log("[Gamesim] Navigation obstacles · removed " + removed + ".");
        }

        /// <summary>
        /// Rebakes the walkable floor and writes it to disk.
        ///
        /// <para>Needed because the baked asset can fall behind the scene without anything saying so.
        /// <c>HouseExpansion.Rebake</c> builds the mesh and marks the asset dirty but never calls
        /// <see cref="AssetDatabase.SaveAssets"/>, so a bake that is not followed by some other save
        /// is simply lost when the editor closes — and the symptom is not a missing NavMesh, it is a
        /// NavMesh that describes a house which no longer exists.</para>
        ///
        /// <para>The data is copied into the existing asset rather than replacing the file, because
        /// the surface holds a GUID reference to it. Replacing it leaves the surface pointing at
        /// nothing, which presents as a cast that cannot move at all.</para>
        ///
        /// <para>Carving obstacles are not affected: the surface is set to ignore them while baking,
        /// so they keep carving at runtime exactly as before.</para>
        /// </summary>
        [MenuItem("Gamesim/Rebake house navigation")]
        public static void Rebake()
        {
            if (!TryFindSurface(out var surface)) return;

            var existing = surface.navMeshData;
            // What the house can currently do, so the new bake can be compared against it. Measured
            // against the committed asset by name rather than against whatever is registered: a bake
            // leaves registration in a state that depends on what the caller did first, and reading
            // it loosely once made this guard refuse a mesh that reached every room.
            int before = HouseNavigationAudit.ReachablePairs(surface, existing);

            surface.BuildNavMesh();
            if (surface.navMeshData == null)
            {
                Debug.LogError("[Gamesim] The NavMesh bake produced no data; nothing was written.");
                return;
            }

            // A bake that disconnects rooms is worse than a stale one, so a bake that loses ground is
            // refused rather than written. The guard stays because it is the right guard, not
            // because the house currently fails it.
            //
            // This comment used to say that a fresh bake of the current geometry does NOT join the
            // yard to the rest of the house. That was true when it was written, on 2026-09-18, and
            // it stopped being true ninety-eight minutes later: 145244e pulled two dividers out of
            // the doorways they were standing in, and recorded 28 of 28 room pairs from a bake
            // rather than from the committed asset. The comment was never updated, and a stale
            // warning that says "do not bake" is expensive - it is why the furniture that ought to
            // be carved has stayed walkable. Re-checked 2026-09-21: the committed mesh reaches 28 of
            // 28, and a trial bake of today geometry reaches the same 28. Bake when the geometry
            // warrants it; this guard is here to catch the day it stops being safe, not to forbid it.
            int after = HouseNavigationAudit.ReachablePairs(surface, surface.navMeshData);
            if (after < before)
            {
                surface.navMeshData = existing;
                Debug.LogWarning("[Gamesim] Rebake REFUSED: the new bake reaches " + after
                                 + " room pairs where the current one reaches " + before
                                 + ". The old NavMesh has been kept. Something in the scene no longer"
                                 + " bakes into a connected house — find it before baking again.");
                return;
            }

            string path = existing == null ? null : AssetDatabase.GetAssetPath(existing);
            if (existing != null && !string.IsNullOrEmpty(path))
            {
                EditorUtility.CopySerialized(surface.navMeshData, existing);
                surface.navMeshData = existing;
                EditorUtility.SetDirty(existing);
            }
            EditorUtility.SetDirty(surface);
            EditorSceneManager.MarkSceneDirty(surface.gameObject.scene);
            // The step HouseExpansion leaves out, and the reason a bake can silently not survive.
            AssetDatabase.SaveAssets();

            Debug.Log("[Gamesim] House navigation rebaked and saved"
                      + (string.IsNullOrEmpty(path) ? " (new asset)" : " to " + path)
                      + ". Run the audit to confirm every room reaches every other room.");
        }

        /// <summary>
        /// Everywhere navigation needs bare floor: the room markers every route ends at, and the
        /// spot each houseguest and the player stands on when the scene loads.
        /// </summary>
        private static List<Vector3> ProtectedPoints()
        {
            var points = new List<Vector3>();
            points.AddRange(Object.FindObjectsByType<Gamesim.House.HouseRoomMarker>(FindObjectsInactive.Include)
                .Select(marker => marker.transform.position));
            points.AddRange(Object.FindObjectsByType<Gamesim.House.HouseNpc>(FindObjectsInactive.Include)
                .Select(npc => npc.transform.position));
            points.AddRange(Object.FindObjectsByType<Gamesim.House.HousePlayerController>(FindObjectsInactive.Include)
                .Select(player => player.transform.position));
            return points;
        }

        private static bool Qualifies(Renderer renderer)
        {
            if (!renderer.gameObject.activeInHierarchy) return false;
            var size = renderer.bounds.size;
            if (size.y < SolidHeight) return false;
            if (size.x < SolidFootprint || size.z < SolidFootprint) return false;
            // A floor slab is flat and enormous, and walking across one is the entire point of it.
            if (size.x * size.z > 8f && size.y < 0.5f) return false;
            return true;
        }

        private static bool TryFindSurface(out NavMeshSurface surface)
        {
            surface = Object.FindObjectsByType<NavMeshSurface>(FindObjectsInactive.Include)
                .FirstOrDefault();
            if (surface != null) return true;
            EditorUtility.DisplayDialog("House navigation",
                "This scene has no NavMeshSurface, so there is nothing to fit obstacles to.", "Close");
            return false;
        }
    }
}
