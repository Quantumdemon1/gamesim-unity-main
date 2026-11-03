using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Gamesim.House;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace Gamesim.Editor
{
    /// <summary>
    /// Opens the doorway between the kitchen and the private room (ROOM-SCOPE-PLAN §2.1; the owner's quick fixes,
    /// 2026-11-03, "the doorway if you're OK with changing the scene").
    ///
    /// <para>The kitchen run stood along the north wall from x 1.0 to 7.4, in front of the west 1.8 m of the 2.8 m gap
    /// at x 5.6 to 8.4, and the agents' half-metre erosion closed the last metre, so no polygon of the baked NavMesh
    /// crossed z = 0 there and houseguests walked round through the living room and the bedroom. The least that opens
    /// it: the run moves west into the corner, its fridge end <see cref="RunLeft"/> from the kitchen's west wall
    /// (0.17: clear of the wall's 0.125 and its cap's 0.035), and is drawn 8% shorter, <see cref="RunLength"/> of its
    /// authored 6.4 (a scale of <see cref="Stretch"/> along its length; every section and the counter's look kept). It
    /// ends at 6.07, so the gap is clear for 2.33 m and walkable for 1.33 after the erosion on both sides. The things
    /// on the run - the kit's three appliances, a mug and a bottle, the room finish's panels and clusters - keep their
    /// places along it, their own size unchanged. The run's activity anchors stay in metres in their own frame, as
    /// <see cref="HouseInteractionAnchor.Create"/> keeps every anchor under a scaled prop.</para>
    ///
    /// <para>An explicit batch entry, never an import callback, as <see cref="HouseAmenitiesAuthoring"/>'s are: it
    /// changes the scene only in the project named by <c>-gamesimHouseAuthoringRoot</c>, refuses a house that is not
    /// in the state it was written for, bakes the NavMesh into the existing asset (its GUID kept), refuses the bake
    /// if any room pair or saved approach the house reached is lost or the doorway is under a metre wide, rebakes the
    /// house's lightmaps (the run and its appliances are lightmapped: <c>-gamesimRebakeExistingHouseLighting</c> is
    /// required, as for the spare lounger), saves, reopens and checks again, and writes its report to
    /// <c>Logs/kitchen-doorway-authoring.json</c>. A house already moved is checked and left as it is.</para>
    ///
    /// <code>Unity.exe -batchmode -quit -projectPath &lt;copy&gt; -gamesimHouseAuthoringRoot &lt;copy&gt;
    ///   -gamesimRebakeExistingHouseLighting -executeMethod Gamesim.Editor.HouseKitchenDoorway.AuthorAcceptance
    ///   -logFile &lt;copy&gt;/Logs/kitchen-doorway.log</code>
    /// </summary>
    public static class HouseKitchenDoorway
    {
        private const string ScenePath = "Assets/Gamesim/Scenes/EpisodeHouse.unity";
        private const string RunName = "bb_set_kitchenrun";
        private const string LightingFlag = "-gamesimRebakeExistingHouseLighting";

        /// <summary>The run's authored length, from its fridge to its end panel (bb_set_kitchenrun.py), and where it stood.</summary>
        public const float AuthoredLength = 6.4f, OldLeft = 1.0f;
        /// <summary>Its fridge end's place from the kitchen's west wall line, and its drawn length.</summary>
        public const float RunLeft = .17f, RunLength = 5.9f;
        /// <summary>The scale along its length the drawn length takes: 0.921875.</summary>
        public const float Stretch = RunLength / AuthoredLength;
        /// <summary>The doorway between the kitchen and the private room, on z = 0 (HousePrototypeSetup's door at 7, 1.4 a side).</summary>
        public const float DoorLow = 5.6f, DoorHigh = 8.4f;
        /// <summary>The least walkable width the bake must leave across the doorway, after the agents' erosion.</summary>
        public const float LeastWalkableWidth = 1f;

        /// <summary>The things that stand on the run or on its face, by name: the plan's five counter rows and the room finish's nine (two snack bags).</summary>
        private static readonly string[] OnTheRun =
        {
            "kitchenMicrowave", "kitchenCoffeeMachine", "toaster", "bb_set_mug", "bb_set_bottle",
            "Fridge panel", "Dishwasher panel", "Oven panel", "Coffee tin", "Jar", "Cereal", "Snack bag", "Dish towel",
        };
        private const int OnTheRunCount = 14;

        [Serializable] private sealed class Report
        {
            public string scene, navigationGuid;
            public bool alreadyApplied, lightingRebaked, reopenedAndVerified;
            public float runLeftBefore, runRightBefore, runLeftAfter, runRightAfter, stretch;
            public List<string> moved = new List<string>();
            public int roomPairsBefore, roomPairsAfter, approachesBefore, approachesAfter;
            public float doorwayWidthBefore, doorwayWidthAfter, pathBefore, pathAfter;
            public string[] lostApproaches = new string[0];
        }

        /// <summary>Applies the move to the named copy's house, rebakes, saves and verifies: see the class summary.</summary>
        public static void AuthorAcceptance()
        {
            string project = RequireAuthoringProject();
            var report = new Report { scene = ScenePath, stretch = Stretch };
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var all = All(scene);
            var surface = all.Select(t => t.GetComponent<NavMeshSurface>()).Single(s => s != null);
            var original = surface.navMeshData;
            if (original == null) throw new InvalidOperationException("The house has no existing navigation asset.");
            report.navigationGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(original));
            var run = all.Single(t => t.name == RunName && t.gameObject.activeInHierarchy);
            var floor = all.Single(t => t.name == "Kitchen floor").GetComponent<Renderer>().bounds;
            var before = Bounds(run);
            report.runLeftBefore = before.min.x; report.runRightBefore = before.max.x;
            report.roomPairsBefore = HouseNavigationAudit.ReachablePairs(surface, original);
            var approaches = ReachableApproaches(scene);
            report.approachesBefore = approaches.Count;
            report.doorwayWidthBefore = DoorwayWidth(floor.max.y);
            report.pathBefore = PathThrough(floor.max.y);

            bool applied = Mathf.Abs(run.localScale.x - Stretch) < .0005f && Mathf.Abs(before.min.x - RunLeft) < .01f;
            if (applied)
            {
                // A copy already moved: verify it and change nothing.
                report.alreadyApplied = true;
                Verify(report, floor.max.y, approaches);
                Write(project, report);
                Debug.Log("[Gamesim] Kitchen doorway already open: " + Describe(report));
                return;
            }
            if (Mathf.Abs(run.localScale.x - 1f) > .0005f || Mathf.Abs(before.min.x - OldLeft) > .02f
                || Mathf.Abs(before.size.x - AuthoredLength) > .05f || Mathf.Abs(run.eulerAngles.y) > .5f && Mathf.Abs(run.eulerAngles.y - 360f) > .5f)
                throw new InvalidOperationException("The kitchen run is not where this was written for (x 1.0 to 7.4, unscaled, at yaw 0): "
                    + before.min.x.ToString("F3") + " to " + before.max.x.ToString("F3") + ", scale " + run.localScale.ToString("F4") + ".");
            if (report.doorwayWidthBefore >= LeastWalkableWidth)
                throw new InvalidOperationException("The doorway is already walkable (" + report.doorwayWidthBefore.ToString("F2") + " m): nothing to do.");

            var riders = Riders(all, run, before);
            var lightmapped = new[] { run }.Concat(riders).SelectMany(t => t.GetComponentsInChildren<Renderer>(true))
                .Any(r => r.lightmapIndex >= 0 && r.lightmapIndex < LightmapSettings.lightmaps.Length);
            if (lightmapped) RequireOwnedLighting();
            var others = Transforms(scene, run, riders);

            // The run: shortened along its length about its own centre, then moved so its fridge end is at RunLeft.
            // Its yaw is 0 and its parent unscaled, so its local x is the house's x.
            run.localScale = new Vector3(Stretch, 1f, 1f);
            foreach (var anchor in run.GetComponentsInChildren<HouseInteractionAnchor>(true))
                if (anchor.transform.parent == run) anchor.transform.localScale = new Vector3(1f / Stretch, 1f, 1f);
            Physics.SyncTransforms();
            var scaled = Bounds(run);
            run.position += new Vector3(RunLeft - scaled.min.x, 0f, 0f);
            // What stands on it keeps its place along it, at its own size: its middle mapped as the run's length is.
            foreach (var rider in riders)
            {
                var b = Bounds(rider);
                float along = b.center.x - before.min.x;
                rider.position += new Vector3(RunLeft + along * Stretch - b.center.x, 0f, 0f);
                report.moved.Add(HierarchyPath(rider) + " " + b.center.x.ToString("F3") + " -> " + Bounds(rider).center.x.ToString("F3"));
            }
            Physics.SyncTransforms();
            var after = Bounds(run);
            report.runLeftAfter = after.min.x; report.runRightAfter = after.max.x;
            if (Mathf.Abs(after.size.x - RunLength) > .05f || Mathf.Abs(after.min.x - RunLeft) > .005f)
                throw new InvalidOperationException("The run did not land where it was sent: " + after.min.x.ToString("F3") + " to " + after.max.x.ToString("F3") + ".");
            VerifyClearance(scene, run);
            if (!others.SequenceEqual(Transforms(scene, run, riders)))
                throw new InvalidOperationException("Moving the run moved something that is not on it.");

            surface.BuildNavMesh();
            var candidate = surface.navMeshData;
            if (candidate == null) throw new InvalidOperationException("The kitchen doorway's navigation bake produced no data.");
            Verify(report, floor.max.y, approaches);
            EditorUtility.CopySerialized(candidate, original);
            surface.navMeshData = original;
            EditorUtility.SetDirty(original);
            EditorUtility.SetDirty(surface);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save the house with its doorway open.");
            AssetDatabase.SaveAssetIfDirty(original);
            if (lightmapped)
            {
                // Keep the existing lights, probes, settings and materials; this bake updates only its generated output,
                // so the floor and the wall lose the run's old shadow and take its new one.
                if (!Lightmapping.Bake() || Lightmapping.lightingDataAsset == null
                    || LightmapSettings.lightmaps.Length == 0 || LightmapSettings.lightmaps.Any(map => map.lightmapColor == null))
                    throw new InvalidOperationException("The existing house lighting did not finish a valid bake.");
                report.lightingRebaked = true;
                if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save the freshly lit house.");
                AssetDatabase.SaveAssets();
            }

            // Reopened, as the next process will see it.
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            all = All(scene);
            surface = all.Select(t => t.GetComponent<NavMeshSurface>()).Single(s => s != null);
            run = all.Single(t => t.name == RunName && t.gameObject.activeInHierarchy);
            if (AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(surface.navMeshData)) != report.navigationGuid
                || Mathf.Abs(Bounds(run).min.x - RunLeft) > .005f || Mathf.Abs(run.localScale.x - Stretch) > .0005f)
                throw new InvalidOperationException("The saved house lost the run's place or its navigation asset's identity.");
            Verify(report, floor.max.y, approaches);
            report.reopenedAndVerified = true;
            Write(project, report);
            Debug.Log("[Gamesim] Kitchen doorway opened: " + Describe(report));
        }

        /// <summary>The bake as it stands, against the house before: no room pair or approach lost, the doorway a metre wide and walked through.</summary>
        private static void Verify(Report report, float floorY, HashSet<string> approachesBefore)
        {
            var surface = UnityEngine.Object.FindObjectsByType<NavMeshSurface>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single();
            report.roomPairsAfter = HouseNavigationAudit.ReachablePairs(surface, surface.navMeshData);
            var approaches = ReachableApproaches(surface.gameObject.scene);
            report.approachesAfter = approaches.Count;
            report.lostApproaches = approachesBefore.Except(approaches).OrderBy(x => x, StringComparer.Ordinal).ToArray();
            report.doorwayWidthAfter = DoorwayWidth(floorY);
            report.pathAfter = PathThrough(floorY);
            if (report.roomPairsAfter < report.roomPairsBefore || report.lostApproaches.Length > 0)
                throw new InvalidOperationException("The bake lost navigation the house had: room pairs " + report.roomPairsBefore + " -> "
                    + report.roomPairsAfter + "; approaches lost: " + string.Join(", ", report.lostApproaches));
            if (report.doorwayWidthAfter < LeastWalkableWidth)
                throw new InvalidOperationException("The doorway is walkable for only " + report.doorwayWidthAfter.ToString("F2") + " m after the bake.");
            if (report.pathAfter <= 0f || report.pathAfter > DetourFloor())
                throw new InvalidOperationException("No walk from the kitchen to the private room goes through their doorway: " + report.pathAfter.ToString("F2") + " m.");
        }

        /// <summary>
        /// The longest run of NavMesh along the doorway's line, z = 0 from <see cref="DoorLow"/> to <see cref="DoorHigh"/>, in
        /// 2.5 cm steps: a point is walkable when the mesh is within 5 cm of it.
        /// </summary>
        public static float DoorwayWidth(float floorY)
        {
            float best = 0f, run = 0f;
            const float step = .025f;
            for (float x = DoorLow + step * .5f; x < DoorHigh; x += step)
            {
                bool on = NavMesh.SamplePosition(new Vector3(x, floorY, 0f), out var hit, .3f, NavMesh.AllAreas)
                          && Mathf.Abs(hit.position.x - x) < .05f && Mathf.Abs(hit.position.z) < .05f;
                run = on ? run + step : 0f;
                best = Mathf.Max(best, run);
            }
            return best;
        }

        /// <summary>
        /// The walk from 1.6 m inside the kitchen to 1.6 m inside the private room, either side of the doorway's open middle
        /// (x 7.25): its length, or 0 when there is none. Through the doorway it is about 3.2 m; round through the living
        /// room and the bedroom it must cross x = 0 twice, so it is at least <see cref="DetourFloor"/>.
        /// </summary>
        public static float PathThrough(float floorY)
        {
            if (!NavMesh.SamplePosition(new Vector3(7.25f, floorY, -1.6f), out var from, .5f, NavMesh.AllAreas)
                || !NavMesh.SamplePosition(new Vector3(7.25f, floorY, 1.6f), out var to, .5f, NavMesh.AllAreas)) return 0f;
            var path = new NavMeshPath();
            if (!NavMesh.CalculatePath(from.position, to.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete) return 0f;
            float length = 0f;
            for (int i = 1; i < path.corners.Length; i++) length += Vector3.Distance(path.corners[i - 1], path.corners[i]);
            return length;
        }

        /// <summary>The shortest a walk round through the living room and the bedroom could be: twice the way from x 7.25 to the x = 0 wall line.</summary>
        public static float DetourFloor() => 2f * 7.25f;

        /// <summary>
        /// The fourteen things on the run, each by the root that places it, found over the run's footprint. Anything else drawn
        /// over the footprint between the plinth and the cutaway's top - a prop a later pass stood on the counter - is
        /// refused by name, so nothing is left floating where the run was.
        /// </summary>
        private static List<Transform> Riders(Transform[] all, Transform run, Bounds footprint)
        {
            var names = new HashSet<string>(OnTheRun, StringComparer.Ordinal);
            var found = all.Where(t => names.Contains(t.name) && t.gameObject.activeInHierarchy && !t.IsChildOf(run)
                    && t.parent != null && (t.parent.name == HouseSetPieces.RootName || t.parent.name == "Kitchen" && t.parent.parent != null && t.parent.parent.name == HouseRoomFinish.RootName))
                .Where(t =>
                {
                    var b = Bounds(t);
                    return b.size != Vector3.zero && b.center.x > footprint.min.x - .06f && b.center.x < footprint.max.x + .06f
                           && b.center.z > footprint.min.z - .06f && b.center.z < footprint.max.z + .06f;
                })
                .ToList();
            if (found.Count != OnTheRunCount)
                throw new InvalidOperationException("Expected the " + OnTheRunCount + " things on the kitchen run, found " + found.Count + ": "
                    + string.Join(", ", found.Select(HierarchyPath)));
            float floorTop = all.Single(t => t.name == "Kitchen floor").GetComponent<Renderer>().bounds.max.y;
            var strays = all.SelectMany(t => t.GetComponents<Renderer>())
                .Where(r => r.enabled && r.gameObject.activeInHierarchy && !r.transform.IsChildOf(run) && !found.Any(r.transform.IsChildOf))
                .Where(r =>
                {
                    var b = r.bounds;
                    return b.size.x < 2f && b.min.y > floorTop + .2f && b.max.y < floorTop + 1.6f
                           && b.center.x > footprint.min.x - .06f && b.center.x < footprint.max.x + .06f
                           && b.center.z > footprint.min.z - .06f && b.center.z < footprint.max.z + .06f;
                })
                .Select(r => HierarchyPath(r.transform)).ToList();
            if (strays.Count > 0)
                throw new InvalidOperationException("Something else stands on the kitchen run, which this would leave behind: " + string.Join(", ", strays));
            return found;
        }

        /// <summary>Every transform in the house other than the run, its children and what stands on it, by path and pose.</summary>
        private static string[] Transforms(Scene scene, Transform run, List<Transform> riders) =>
            All(scene).Where(t => !t.IsChildOf(run) && !riders.Any(t.IsChildOf))
                .Select(t => FormattableString.Invariant($"{HierarchyPath(t)}:{t.position:F4}:{t.rotation:F4}:{t.lossyScale:F4}:{t.gameObject.activeSelf}"))
                .OrderBy(x => x, StringComparer.Ordinal).ToArray();

        /// <summary>The run's solid proxy clear of every other furniture proxy (layer 8), as each new furnishing must be.</summary>
        private static void VerifyClearance(Scene scene, Transform run)
        {
            var mine = run.GetComponentsInChildren<Collider>(true).Where(c => c.enabled && c.gameObject.activeInHierarchy && c.gameObject.layer == HouseLayers.Furniture).ToArray();
            if (mine.Length == 0) throw new InvalidOperationException("The kitchen run has no solid proxy on the furniture layer.");
            var furniture = All(scene).SelectMany(t => t.GetComponents<Collider>())
                .Where(c => c.enabled && c.gameObject.activeInHierarchy && c.gameObject.layer == HouseLayers.Furniture && !c.transform.IsChildOf(run)).ToArray();
            foreach (var current in mine)
                foreach (var other in furniture)
                    if (Physics.ComputePenetration(current, current.transform.position, current.transform.rotation,
                            other, other.transform.position, other.transform.rotation, out _, out float depth) && depth > .001f)
                        throw new InvalidOperationException("The moved run overlaps furniture: " + HierarchyPath(other.transform) + ", depth " + depth.ToString("F3"));
        }

        /// <summary>The lighting bake may write only this episode's own outputs and settings (the spare lounger's rule).</summary>
        private static void RequireOwnedLighting()
        {
            if (!Environment.GetCommandLineArgs().Contains(LightingFlag))
                throw new InvalidOperationException("The kitchen run and its appliances are lightmapped: moving them requires the house's lighting bake ("
                    + LightingFlag + ").");
            const string owned = "Assets/Gamesim/Scenes/EpisodeHouse/";
            if (AssetDatabase.GetAssetPath(Lightmapping.lightingDataAsset) != owned + "LightingData.asset"
                || Lightmapping.lightingSettings == null
                || AssetDatabase.GetAssetPath(Lightmapping.lightingSettings) != HouseCinematicLighting.LightingSettingsPath
                || LightmapSettings.lightmaps.Any(map => map.lightmapColor == null
                    || !AssetDatabase.GetAssetPath(map.lightmapColor).StartsWith(owned, StringComparison.Ordinal)))
                throw new InvalidOperationException("The lighting bake must own only this episode's existing lighting outputs and settings.");
        }

        private static HashSet<string> ReachableApproaches(Scene scene)
        {
            var result = new HashSet<string>();
            var rooms = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<HouseRoomMarker>(true)).ToArray();
            foreach (var anchor in HouseInteractionAnchors.InScene(scene))
            {
                var room = rooms.FirstOrDefault(r => r.RoomName == anchor.RoomId);
                if (room == null || !NavMesh.SamplePosition(room.transform.position, out var start, .35f, NavMesh.AllAreas)
                    || !NavMesh.SamplePosition(anchor.Approach, out var end, .25f, NavMesh.AllAreas)) continue;
                var path = new NavMeshPath();
                if (NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete)
                    result.Add(anchor.VenueId + ":" + anchor.Slot);
            }
            return result;
        }

        private static string RequireAuthoringProject()
        {
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string[] arguments = Environment.GetCommandLineArgs();
            int key = Array.IndexOf(arguments, "-gamesimHouseAuthoringRoot");
            if (!Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode || key < 0 || key + 1 >= arguments.Length
                || !string.Equals(project.TrimEnd('\\', '/'), Path.GetFullPath(arguments[key + 1]).TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Opening the kitchen doorway requires batch mode and -gamesimHouseAuthoringRoot naming this project.");
            return project;
        }

        private static void Write(string project, Report report)
        {
            string directory = Path.Combine(project, "Logs");
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "kitchen-doorway-authoring.json"), JsonUtility.ToJson(report, true));
        }

        private static string Describe(Report r) =>
            FormattableString.Invariant($"run {r.runLeftBefore:F3}..{r.runRightBefore:F3} -> {r.runLeftAfter:F3}..{r.runRightAfter:F3} (x{r.stretch:F6}), {r.moved.Count} on it moved; ")
            + FormattableString.Invariant($"doorway {r.doorwayWidthBefore:F2} -> {r.doorwayWidthAfter:F2} m walkable, kitchen to private room {r.pathBefore:F2} -> {r.pathAfter:F2} m; ")
            + FormattableString.Invariant($"room pairs {r.roomPairsBefore} -> {r.roomPairsAfter}, approaches {r.approachesBefore} -> {r.approachesAfter}; lighting rebaked {r.lightingRebaked}.");

        private static Transform[] All(Scene scene) => scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true)).ToArray();

        private static Bounds Bounds(Transform item)
        {
            var renderers = item.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return new Bounds(item.position, Vector3.zero);
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        private static string HierarchyPath(Transform item) => item.parent == null
            ? item.name + "[" + item.GetSiblingIndex() + "]"
            : HierarchyPath(item.parent) + "/" + item.name + "[" + item.GetSiblingIndex() + "]";
    }
}
