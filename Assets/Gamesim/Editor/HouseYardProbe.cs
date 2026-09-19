using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace Gamesim.Editor
{
    /// <summary>
    /// Diagnostics for the one junction in this house that does not bake connected: the opening
    /// between the main house and the competition yard.
    /// </summary>
    public static class HouseYardProbe
    {
        /// <summary>
        /// Throws away unsaved scene edits and reloads from disk.
        ///
        /// <para>Needed because an editor left holding a bad experiment cannot be handed back to the
        /// asset on disk any other way from here — <c>OpenScene</c> refuses while the scene is dirty
        /// and every other route puts a modal dialog in front of it.</para>
        /// </summary>
        [MenuItem("Gamesim/Reload house scene, discarding changes")]
        public static void Reload()
        {
            var scene = EditorSceneManager.GetActiveScene();
            string path = scene.path;
            if (string.IsNullOrEmpty(path)) { Debug.LogWarning("[Gamesim] The active scene has never been saved."); return; }
            EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            Debug.Log("[Gamesim] Reloaded " + path + " from disk; unsaved changes discarded.");
        }

        /// <summary>
        /// Reports what stands in the yard opening and where the walkable floor actually stops.
        ///
        /// <para>The opening is the gap between "House / yard left" and "House / yard right" at
        /// z = 10. A fresh bake leaves the yard unreachable and the gap is 2.8 m wide, which at an
        /// agent radius of 0.5 should leave 1.8 m to walk through — so either something is standing
        /// in it, or the floor does not actually join there.</para>
        /// </summary>
        [MenuItem("Gamesim/Probe yard doorway")]
        public static void Probe()
        {
            var report = new StringBuilder();
            report.AppendLine("[Gamesim] Yard doorway probe");

            report.AppendLine("-- colliders standing near the opening --");
            var near = Object.FindObjectsByType<Collider>(FindObjectsInactive.Exclude)
                .Where(c => c.bounds.max.x > -5f && c.bounds.min.x < 5f
                            && c.bounds.max.z > 6f && c.bounds.min.z < 14f)
                .OrderBy(c => c.bounds.center.x);
            foreach (var c in near)
                report.AppendLine(string.Format("   {0,-30} layer {1}  x {2,7:0.00}..{3,-7:0.00} z {4,7:0.00}..{5,-7:0.00} y {6,6:0.00}..{7:0.00}",
                    c.gameObject.name, c.gameObject.layer,
                    c.bounds.min.x, c.bounds.max.x, c.bounds.min.z, c.bounds.max.z, c.bounds.min.y, c.bounds.max.y));

            report.AppendLine("-- is there walkable floor across the opening? --");
            for (float z = 8f; z <= 12f; z += 0.5f)
            {
                var row = new StringBuilder(string.Format("   z {0,5:0.0}  ", z));
                for (float x = -4f; x <= 4f; x += 0.5f)
                    row.Append(NavMesh.SamplePosition(new Vector3(x, 0f, z), out _, 0.3f, NavMesh.AllAreas) ? '#' : '.');
                report.AppendLine(row + "   (x -4 .. +4, # = on the mesh)");
            }

            report.AppendLine("-- can an agent actually cross it? --");
            var path = new NavMeshPath();
            bool crossed = NavMesh.SamplePosition(new Vector3(0f, 0f, 7f), out var south, 2f, NavMesh.AllAreas)
                           && NavMesh.SamplePosition(new Vector3(0f, 0f, 13f), out var north, 2f, NavMesh.AllAreas)
                           && NavMesh.CalculatePath(south.position, north.position, NavMesh.AllAreas, path)
                           && path.status == NavMeshPathStatus.PathComplete;
            report.AppendLine("   house -> yard: " + (crossed ? "yes" : "NO"));

            Debug.Log(report.ToString());
        }
    }
}
