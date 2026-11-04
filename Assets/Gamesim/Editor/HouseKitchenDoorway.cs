using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Gamesim.House;
using Unity.AI.Navigation;
using Unity.Collections;
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
    /// places along it, their own size unchanged, but for the pantry beside the toaster (the cereal and the two snack
    /// bags), which keeps its own gaps from the toaster (<see cref="HouseRoomFinish.PlaceThePantryAgain"/>): scaled with
    /// the rest, the bags met the toaster. The run's activity anchors stay in metres in their own frame, as
    /// <see cref="HouseInteractionAnchor.Create"/> keeps every anchor under a scaled prop.</para>
    ///
    /// <para>Nothing is saved while one of the counter's authored props meets another mesh (<see cref="CounterMeetings"/>,
    /// RoomFinishTests' clear and resting rules). <see cref="ClearTheCounter"/> is the second batch entry, for a house
    /// whose doorway this already opened with the pantry scaled: it places the pantry again and saves the scene alone,
    /// with no bake, because those three carry no collider, no static flag and no shadow.</para>
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
        private const string ReportName = "kitchen-doorway-authoring.json", CounterReportName = "kitchen-counter-clearance.json";

        /// <summary>The authored props' folder (HouseSmallPropAssetTests' set): the props RoomFinishTests holds clear and resting.</summary>
        private const string HouseSmallProps = "Assets/Gamesim/Art/Authored/HouseSmallProps/";
        /// <summary>RoomFinishTests' measures: 3 mm a side counts as touching, not overlap; the slab 0.03 m deep in front of a front.</summary>
        private const float Slack = .003f, LabelReach = .03f;
        /// <summary>The counter's authored props in the room finish's kitchen: the coffee tin, the jar, the cereal and the two snack bags.</summary>
        public const int CounterPropCount = 5;

        [Serializable] private sealed class CounterReport
        {
            public string scene, navigationGuid;
            public bool alreadyClear, visualOnly, nothingElseMoved, reopenedAndVerified;
            public float runLeft, runRight, stretch;
            public int counterProps;
            public List<string> moved = new List<string>();
            public string[] meetingBefore = new string[0], meetingAfter = new string[0];
        }

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
                Verify(report, surface, floor.max.y, approaches);
                RequireClearCounter(scene, "run ClearTheCounter to place the pantry beside the toaster again");
                Write(project, ReportName, report);
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
            var moving = new[] { run }.Concat(riders).ToArray();
            var others = Transforms(scene, moving);
            var from = riders.Select(rider => Bounds(rider).center.x).ToArray();

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
            }
            // But the pantry beside the toaster keeps its gaps from the toaster, as the room finish places it now.
            HouseRoomFinish.PlaceThePantryAgain(scene);
            Physics.SyncTransforms();
            for (int i = 0; i < riders.Count; i++)
                report.moved.Add(HierarchyPath(riders[i]) + " " + from[i].ToString("F3") + " -> " + Bounds(riders[i]).center.x.ToString("F3"));
            var after = Bounds(run);
            report.runLeftAfter = after.min.x; report.runRightAfter = after.max.x;
            if (Mathf.Abs(after.size.x - RunLength) > .05f || Mathf.Abs(after.min.x - RunLeft) > .005f)
                throw new InvalidOperationException("The run did not land where it was sent: " + after.min.x.ToString("F3") + " to " + after.max.x.ToString("F3") + ".");
            VerifyClearance(scene, run);
            if (!others.SequenceEqual(Transforms(scene, moving)))
                throw new InvalidOperationException("Moving the run moved something that is not on it.");
            RequireClearCounter(scene, "nothing is saved");

            surface.BuildNavMesh();
            var candidate = surface.navMeshData;
            if (candidate == null) throw new InvalidOperationException("The kitchen doorway's navigation bake produced no data.");
            Verify(report, surface, floor.max.y, approaches);
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
            Verify(report, surface, floor.max.y, approaches);
            RequireClearCounter(scene, "the saved house is not what was checked");
            report.reopenedAndVerified = true;
            Write(project, ReportName, report);
            Debug.Log("[Gamesim] Kitchen doorway opened: " + Describe(report));
        }

        /// <summary>
        /// The pantry beside the toaster placed again on a house whose doorway <see cref="AuthorAcceptance"/> opened with
        /// the pantry scaled along the run (d6f63a32's: the gold bag in the toaster's side, the blue bag's label against
        /// its end). An explicit batch entry, as AuthorAcceptance is, for the copy named by <c>-gamesimHouseAuthoringRoot</c>:
        /// it refuses a house whose run is not where the doorway put it, and refuses to move anything a bake sees - the
        /// cereal and the two snack bags must carry no collider (the NavMesh is baked from colliders), no static flag
        /// (nothing lightmaps them or bakes them into a reflection probe) and no shadow - so it saves the scene alone,
        /// with no NavMesh or lighting bake. It places the three with <see cref="HouseRoomFinish.PlaceThePantryAgain"/>,
        /// as a run of the room finish places them, checks that nothing else in the house moved and that no authored
        /// prop on the counter meets another mesh (<see cref="CounterMeetings"/>) before it saves, then reopens and
        /// checks again, and writes its report to <c>Logs/kitchen-counter-clearance.json</c>. A counter already clear is
        /// checked and left as it is.
        ///
        /// <code>Unity.exe -batchmode -quit -projectPath &lt;copy&gt; -gamesimHouseAuthoringRoot &lt;copy&gt;
        ///   -executeMethod Gamesim.Editor.HouseKitchenDoorway.ClearTheCounter -logFile &lt;copy&gt;/Logs/kitchen-counter.log</code>
        /// </summary>
        public static void ClearTheCounter()
        {
            string project = RequireAuthoringProject();
            var report = new CounterReport { scene = ScenePath };
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var all = All(scene);
            var surface = all.Select(t => t.GetComponent<NavMeshSurface>()).Single(s => s != null);
            if (surface.navMeshData == null) throw new InvalidOperationException("The house has no navigation asset.");
            report.navigationGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(surface.navMeshData));
            var run = all.Single(t => t.name == RunName && t.gameObject.activeInHierarchy);
            var bounds = Bounds(run);
            report.runLeft = bounds.min.x; report.runRight = bounds.max.x; report.stretch = run.localScale.x;
            if (Mathf.Abs(run.localScale.x - Stretch) > .0005f || Mathf.Abs(bounds.min.x - RunLeft) > .005f || Mathf.Abs(bounds.size.x - RunLength) > .05f)
                throw new InvalidOperationException("The kitchen run is not where the doorway put it (x 0.17 to 6.07 at a scale of 0.921875): "
                    + bounds.min.x.ToString("F3") + " to " + bounds.max.x.ToString("F3") + ", scale " + run.localScale.ToString("F4") + "; open the doorway first (AuthorAcceptance).");

            var pantry = HouseRoomFinish.ThePantry(scene);
            RequireVisualOnly(surface, pantry);
            report.visualOnly = true;
            var others = Transforms(scene, pantry);
            var from = pantry.Select(item => Bounds(item).center.x).ToArray();
            report.meetingBefore = CounterMeetings(scene, out _).ToArray();

            var placed = HouseRoomFinish.PlaceThePantryAgain(scene);
            if (!placed.SequenceEqual(pantry)) throw new InvalidOperationException("The room finish placed other objects than the pantry it found.");
            int changed = 0;
            for (int i = 0; i < pantry.Length; i++)
            {
                float to = Bounds(pantry[i]).center.x;
                if (Mathf.Abs(to - from[i]) > .0001f) changed++;
                report.moved.Add(HierarchyPath(pantry[i]) + " " + from[i].ToString("F3") + " -> " + to.ToString("F3"));
            }
            if (!others.SequenceEqual(Transforms(scene, pantry)))
                throw new InvalidOperationException("Placing the pantry again moved something that is not in it.");
            report.nothingElseMoved = true;
            report.meetingAfter = CounterMeetings(scene, out report.counterProps).ToArray();
            if (report.meetingAfter.Length > 0)
                throw new InvalidOperationException("The counter's props still meet, so the house is not saved:\n" + string.Join("\n", report.meetingAfter));
            if (changed == 0)
            {
                // A counter already clear: checked, and nothing saved.
                report.alreadyClear = true;
                Write(project, CounterReportName, report);
                Debug.Log("[Gamesim] Kitchen counter already clear: " + Describe(report));
                return;
            }
            var places = pantry.Select(item => FormattableString.Invariant($"{item.position:F5}")).ToArray();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save the house with its counter cleared.");

            // Reopened, as the next process will see it.
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            surface = All(scene).Select(t => t.GetComponent<NavMeshSurface>()).Single(s => s != null);
            var reopened = HouseRoomFinish.ThePantry(scene);
            if (!reopened.Select(item => FormattableString.Invariant($"{item.position:F5}")).SequenceEqual(places))
                throw new InvalidOperationException("The saved house lost the pantry's places.");
            if (!others.SequenceEqual(Transforms(scene, reopened)))
                throw new InvalidOperationException("The saved house differs from the one opened outside the pantry.");
            if (AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(surface.navMeshData)) != report.navigationGuid)
                throw new InvalidOperationException("The saved house lost its navigation asset's identity.");
            RequireClearCounter(scene, "the saved house is not what was checked");
            report.reopenedAndVerified = true;
            Write(project, CounterReportName, report);
            Debug.Log("[Gamesim] Kitchen counter cleared: " + Describe(report));
        }

        /// <summary>
        /// The pieces <see cref="ClearTheCounter"/> moves are dressing no bake sees: no collider (and the surface collects
        /// colliders), no static flag on any node, no shadow, no lightmap. Otherwise moving them would need the NavMesh
        /// and the lighting baked again, which that entry does not do.
        /// </summary>
        private static void RequireVisualOnly(NavMeshSurface surface, IEnumerable<Transform> pieces)
        {
            var faults = new List<string>();
            if (surface.useGeometry != NavMeshCollectGeometry.PhysicsColliders) faults.Add("the NavMesh is baked from " + surface.useGeometry + ", not the colliders");
            foreach (var piece in pieces)
            {
                string name = HierarchyPath(piece);
                if (piece.GetComponentsInChildren<Collider>(true).Length > 0) faults.Add(name + " has a collider");
                if (piece.GetComponentsInChildren<Transform>(true).Any(node => GameObjectUtility.GetStaticEditorFlags(node.gameObject) != 0)) faults.Add(name + " has a static flag");
                foreach (var renderer in piece.GetComponentsInChildren<Renderer>(true))
                {
                    if (renderer.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.Off) faults.Add(name + " casts a shadow");
                    if (renderer.lightmapIndex >= 0 && renderer.lightmapIndex < LightmapSettings.lightmaps.Length) faults.Add(name + " is lightmapped");
                }
            }
            if (faults.Count > 0)
                throw new InvalidOperationException("The pantry is not dressing no bake sees, so placing it again would need a bake: " + string.Join("; ", faults) + ".");
        }

        /// <summary>Throws, naming every meeting, unless each of the counter's authored props is clear and resting (<see cref="CounterMeetings"/>).</summary>
        private static void RequireClearCounter(Scene scene, string consequence)
        {
            var meetings = CounterMeetings(scene, out _);
            if (meetings.Count > 0)
                throw new InvalidOperationException("The counter's props meet (" + consequence + "):\n" + string.Join("\n", meetings));
        }

        /// <summary>The bake as it stands, against the house before: no room pair or approach lost, the doorway a metre wide and walked through.</summary>
        private static void Verify(Report report, NavMeshSurface surface, float floorY, HashSet<string> approachesBefore)
        {
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

        /// <summary>Every transform in the house outside the moving ones and their children, by path and pose.</summary>
        private static string[] Transforms(Scene scene, IReadOnlyCollection<Transform> moving) =>
            All(scene).Where(t => !moving.Any(t.IsChildOf))
                .Select(t => FormattableString.Invariant($"{HierarchyPath(t)}:{t.position:F4}:{t.rotation:F4}:{t.lossyScale:F4}:{t.gameObject.activeSelf}"))
                .OrderBy(x => x, StringComparer.Ordinal).ToArray();

        /// <summary>
        /// RoomFinishTests' clear and resting rules (TheSavedScene... and ARunOfThePassShowsEveryAuthoredPropToItsRoomClearOfEverythingAndResting),
        /// held here for the authored props on the kitchen's counter so that no batch run saves a counter whose props meet:
        /// for each, the slab <see cref="LabelReach"/> deep in front of its mesh box's front (-z) face, and the box itself,
        /// each <see cref="Slack"/> in from every side, meet no triangle of another drawn mesh (a flat quad lying on a
        /// surface is a graphic and takes no part); and it rests: under the middle of its base the highest surface within
        /// 5 cm below is no more than <see cref="Slack"/> under it, and under the middle and the four corners (80 % out)
        /// nothing stands more than <see cref="Slack"/> up into it. One line per fault, in the test's words; a counter
        /// that does not hold the <see cref="CounterPropCount"/> props is a fault too. <paramref name="props"/>: how many it holds.
        /// </summary>
        public static List<string> CounterMeetings(Scene scene, out int props)
        {
            var world = scene.GetRootGameObjects().FirstOrDefault(go => go.name == "House Architecture");
            var kitchen = world != null ? world.transform.Find(HouseRoomFinish.RootName + "/Kitchen") : null;
            if (kitchen == null) throw new InvalidOperationException("No room finish kitchen in the house.");
            var drawn = scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<MeshRenderer>(true))
                .Where(renderer => renderer.enabled && renderer.gameObject.activeInHierarchy && MeshOf(renderer) != null && !FlatGraphic(renderer)).ToList();
            var triangles = new Dictionary<MeshRenderer, Vector3[]>();
            var faults = new List<string>();
            props = 0;
            foreach (Transform prop in kitchen)
            {
                string source = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(prop.gameObject) ?? "";
                if (!source.StartsWith(HouseSmallProps, StringComparison.Ordinal)) continue;
                props++;
                string name = "Kitchen / " + prop.name + "[" + prop.GetSiblingIndex() + "] (" + Path.GetFileNameWithoutExtension(source) + ")";
                var own = prop.GetComponentsInChildren<MeshRenderer>(true).Where(renderer => MeshOf(renderer) != null).ToArray();
                if (own.Length == 0) { faults.Add(name + ": it draws no mesh."); continue; }
                var others = drawn.Where(renderer => !own.Contains(renderer)).ToList();
                foreach (var renderer in own)
                {
                    foreach (var (what, hit) in Meeting(Box(renderer, Slack, LabelReach), others, triangles))
                        faults.Add(name + ": the " + LabelReach.ToString("F2") + " m in front of its front meets " + what + " (" + hit + " triangles).");
                    foreach (var (what, hit) in Meeting(Box(renderer, Slack), others, triangles))
                        faults.Add(name + ": its mesh's box meets " + what + " (" + hit + " triangles).");
                }
                Resting(name, own[0], Bounds(prop), others, triangles, faults);
            }
            if (props != CounterPropCount)
                faults.Add("The kitchen's counter holds " + props + " authored props, not its " + CounterPropCount + " (the coffee tin, the jar, the cereal and the two snack bags).");
            return faults;
        }

        private static void Resting(string name, MeshRenderer body, Bounds bounds, List<MeshRenderer> others, Dictionary<MeshRenderer, Vector3[]> triangles, List<string> faults)
        {
            float baseY = bounds.min.y, into = Mathf.Min(.03f, bounds.size.y * .5f);
            var local = MeshOf(body).bounds;
            var corners = new[] { Vector2.zero, new Vector2(-.8f, -.8f), new Vector2(.8f, -.8f), new Vector2(.8f, .8f), new Vector2(-.8f, .8f) };
            for (int c = 0; c < corners.Length; c++)
            {
                var at = body.localToWorldMatrix.MultiplyPoint3x4(new Vector3(local.center.x + corners[c].x * local.extents.x, local.min.y,
                    local.center.z + corners[c].y * local.extents.z));
                var (surface, under) = Highest(at.x, at.z, baseY - .05f, baseY + into, others, triangles);
                if (c == 0 && (!surface.HasValue || surface.Value < baseY - Slack))
                    faults.Add(name + ": nothing within " + (Slack * 1000f).ToString("F0") + " mm under the middle of its base at " + baseY.ToString("F3")
                        + (surface.HasValue ? " (the highest surface, " + under + ", is at " + surface.Value.ToString("F3") + ")" : "") + ".");
                if (surface.HasValue && surface.Value > baseY + Slack)
                {
                    faults.Add(name + ": " + under + " stands " + ((surface.Value - baseY) * 1000f).ToString("F0") + " mm up into its base.");
                    break;
                }
            }
        }

        private static Mesh MeshOf(Renderer renderer)
        {
            var filter = renderer.GetComponent<MeshFilter>();
            return filter != null ? filter.sharedMesh : null;
        }

        /// <summary>A quad lying flat - a rug, a line, a letter on a table: a graphic on a surface, not a solid.</summary>
        private static bool FlatGraphic(Renderer renderer) =>
            MeshOf(renderer).name == "Quad" && Mathf.Abs(renderer.transform.forward.y) > .99f;

        /// <summary>An oriented box: its centre, its three unit axes, its half extents.</summary>
        private readonly struct Obb
        {
            public readonly Vector3 Centre, X, Y, Z, Half;
            public Obb(Vector3 centre, Vector3 x, Vector3 y, Vector3 z, Vector3 half) { Centre = centre; X = x; Y = y; Z = z; Half = half; }

            public Bounds Aabb
            {
                get
                {
                    var extent = new Vector3(
                        Mathf.Abs(X.x) * Half.x + Mathf.Abs(Y.x) * Half.y + Mathf.Abs(Z.x) * Half.z,
                        Mathf.Abs(X.y) * Half.x + Mathf.Abs(Y.y) * Half.y + Mathf.Abs(Z.y) * Half.z,
                        Mathf.Abs(X.z) * Half.x + Mathf.Abs(Y.z) * Half.y + Mathf.Abs(Z.z) * Half.z);
                    return new Bounds(Centre, extent * 2f);
                }
            }

            public Vector3 Local(Vector3 world)
            {
                var d = world - Centre;
                return new Vector3(Vector3.Dot(d, X), Vector3.Dot(d, Y), Vector3.Dot(d, Z));
            }
        }

        /// <summary>The renderer's mesh box in the world, <paramref name="shrink"/> in from every side; with a <paramref name="reach"/>, the slab that deep in front of the box's -z face instead.</summary>
        private static Obb Box(MeshRenderer renderer, float shrink, float reach = 0f)
        {
            var matrix = renderer.localToWorldMatrix;
            var local = MeshOf(renderer).bounds;
            Vector3 x = matrix.GetColumn(0), y = matrix.GetColumn(1), z = matrix.GetColumn(2);
            var scale = new Vector3(x.magnitude, y.magnitude, z.magnitude);
            var half = new Vector3(Mathf.Max(0f, local.extents.x * scale.x - shrink), Mathf.Max(0f, local.extents.y * scale.y - shrink),
                Mathf.Max(0f, local.extents.z * scale.z - shrink));
            var centre = local.center;
            if (reach > 0f)
            {
                centre.z = local.min.z - reach * .5f / scale.z;
                half.z = reach * .5f;
            }
            return new Obb(matrix.MultiplyPoint3x4(centre), x / scale.x, y / scale.y, z / scale.z, half);
        }

        /// <summary>A drawn mesh's triangles in world space, read once per renderer and call (the editor reads an import that keeps no CPU copy).</summary>
        private static Vector3[] Triangles(MeshRenderer renderer, Dictionary<MeshRenderer, Vector3[]> triangles)
        {
            if (triangles.TryGetValue(renderer, out var known)) return known;
            var world = new List<Vector3>();
            var matrix = renderer.localToWorldMatrix;
            using (var array = MeshUtility.AcquireReadOnlyMeshData(MeshOf(renderer)))
            {
                var data = array[0];
                using (var positions = new NativeArray<Vector3>(data.vertexCount, Allocator.Temp))
                {
                    data.GetVertices(positions);
                    for (int sub = 0; sub < data.subMeshCount; sub++)
                    {
                        var descriptor = data.GetSubMesh(sub);
                        if (descriptor.topology != MeshTopology.Triangles) continue;
                        using (var indices = new NativeArray<int>(descriptor.indexCount, Allocator.Temp))
                        {
                            data.GetIndices(indices, sub, true);
                            for (int i = 0; i + 2 < indices.Length; i += 3)
                            {
                                world.Add(matrix.MultiplyPoint3x4(positions[indices[i]]));
                                world.Add(matrix.MultiplyPoint3x4(positions[indices[i + 1]]));
                                world.Add(matrix.MultiplyPoint3x4(positions[indices[i + 2]]));
                            }
                        }
                    }
                }
            }
            return triangles[renderer] = world.ToArray();
        }

        /// <summary>Every other renderer one of whose triangles meets the box, with how many do.</summary>
        private static IEnumerable<(string What, int Count)> Meeting(Obb box, IEnumerable<MeshRenderer> others, Dictionary<MeshRenderer, Vector3[]> triangles)
        {
            var near = box.Aabb;
            foreach (var other in others)
            {
                if (!other.bounds.Intersects(near)) continue;
                var tris = Triangles(other, triangles);
                int count = 0;
                for (int i = 0; i < tris.Length; i += 3)
                {
                    Vector3 a = tris[i], b = tris[i + 1], c = tris[i + 2];
                    if (Mathf.Max(a.x, Mathf.Max(b.x, c.x)) < near.min.x || Mathf.Min(a.x, Mathf.Min(b.x, c.x)) > near.max.x
                        || Mathf.Max(a.y, Mathf.Max(b.y, c.y)) < near.min.y || Mathf.Min(a.y, Mathf.Min(b.y, c.y)) > near.max.y
                        || Mathf.Max(a.z, Mathf.Max(b.z, c.z)) < near.min.z || Mathf.Min(a.z, Mathf.Min(b.z, c.z)) > near.max.z) continue;
                    if (Meets(box, a, b, c)) count++;
                }
                if (count > 0) yield return (PathOf(other.transform), count);
            }
        }

        /// <summary>The highest surface over (<paramref name="x"/>, <paramref name="z"/>) between <paramref name="low"/> and <paramref name="high"/>, and whose it is.</summary>
        private static (float? Height, string What) Highest(float x, float z, float low, float high, IEnumerable<MeshRenderer> others, Dictionary<MeshRenderer, Vector3[]> triangles)
        {
            float? best = null;
            string what = null;
            foreach (var other in others)
            {
                var b = other.bounds;
                if (x < b.min.x || x > b.max.x || z < b.min.z || z > b.max.z || b.max.y < low || b.min.y > high) continue;
                var tris = Triangles(other, triangles);
                for (int i = 0; i < tris.Length; i += 3)
                {
                    float? h = Under(tris[i], tris[i + 1], tris[i + 2], x, z);
                    if (h.HasValue && h.Value >= low && h.Value <= high && (!best.HasValue || h.Value > best.Value))
                    {
                        best = h;
                        what = PathOf(other.transform);
                    }
                }
            }
            return (best, what);
        }

        private static readonly Vector3[] BoxAxes = { Vector3.right, Vector3.up, Vector3.forward };

        /// <summary>The separating-axis test of an oriented box against a triangle: the box's three faces, the triangle's face and the nine edge crossings.</summary>
        private static bool Meets(Obb box, Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 p = box.Local(a), q = box.Local(b), r = box.Local(c);
            Vector3 e0 = q - p, e1 = r - q, e2 = p - r;
            foreach (var axis in BoxAxes)
                if (Apart(axis, p, q, r, box.Half)) return false;
            if (Apart(Vector3.Cross(e0, e1), p, q, r, box.Half)) return false;
            foreach (var axis in BoxAxes)
                if (Apart(Vector3.Cross(axis, e0), p, q, r, box.Half) || Apart(Vector3.Cross(axis, e1), p, q, r, box.Half)
                    || Apart(Vector3.Cross(axis, e2), p, q, r, box.Half)) return false;
            return true;
        }

        private static bool Apart(Vector3 axis, Vector3 p, Vector3 q, Vector3 r, Vector3 half)
        {
            if (axis.sqrMagnitude < 1e-14f) return false;
            float a = Vector3.Dot(p, axis), b = Vector3.Dot(q, axis), c = Vector3.Dot(r, axis);
            float reach = half.x * Mathf.Abs(axis.x) + half.y * Mathf.Abs(axis.y) + half.z * Mathf.Abs(axis.z);
            return Mathf.Min(a, Mathf.Min(b, c)) > reach || Mathf.Max(a, Mathf.Max(b, c)) < -reach;
        }

        /// <summary>The height at which the vertical line through (<paramref name="x"/>, <paramref name="z"/>) meets the triangle, or null.</summary>
        private static float? Under(Vector3 a, Vector3 b, Vector3 c, float x, float z)
        {
            float d = (b.z - c.z) * (a.x - c.x) + (c.x - b.x) * (a.z - c.z);
            if (Mathf.Abs(d) < 1e-12f) return null;
            float l1 = ((b.z - c.z) * (x - c.x) + (c.x - b.x) * (z - c.z)) / d;
            float l2 = ((c.z - a.z) * (x - c.x) + (a.x - c.x) * (z - c.z)) / d;
            float l3 = 1f - l1 - l2;
            if (l1 < -1e-6f || l2 < -1e-6f || l3 < -1e-6f) return null;
            return l1 * a.y + l2 * b.y + l3 * c.y;
        }

        private static string PathOf(Transform node)
        {
            var parts = new List<string>();
            for (var at = node; at != null; at = at.parent) parts.Add(at.name);
            parts.Reverse();
            return string.Join("/", parts);
        }

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

        private static void Write(string project, string file, object report)
        {
            string directory = Path.Combine(project, "Logs");
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, file), JsonUtility.ToJson(report, true));
        }

        private static string Describe(CounterReport r) =>
            FormattableString.Invariant($"run {r.runLeft:F3}..{r.runRight:F3} (x{r.stretch:F6}); the pantry: ") + string.Join(", ", r.moved)
            + FormattableString.Invariant($"; the counter's {r.counterProps} authored props meeting {r.meetingBefore.Length} -> {r.meetingAfter.Length} times; nothing else moved {r.nothingElseMoved}; no bake (visual only {r.visualOnly}).");

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
