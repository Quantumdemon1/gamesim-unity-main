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
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Gamesim.Editor
{
    /// <summary>Adds the remaining furnishings without replacing the furnished house.</summary>
    public static class HouseAmenitiesAuthoring
    {
        private const string ScenePath = "Assets/Gamesim/Scenes/EpisodeHouse.unity";
        private const string GroupName = "House amenities";
        public static readonly string[] ModelIds =
            { "bb_set_pooltable", "bb_set_hohbench", "bb_set_lighttower", "bb_set_studiocam" };

        [Serializable] private sealed class Report
        {
            public string scene;
            public int roomPairsBefore, roomPairsAfter, approachesBefore, approachesAfter;
            public string navigationGuid;
            public string[] protectedSceneBefore, protectedSceneAfter;
            public string[] reachableApproachesBefore;
            public bool reopenedAndVerified;
            public List<string> placed = new List<string>();
        }

        [Serializable] private sealed class SpareLoungerReport
        {
            public string scene, navigationGuid, movedObject;
            public Vector3 before, after;
            public int roomPairsBefore, roomPairsAfter;
            public string[] protectedScene, protectedTransforms, reachableApproaches, reachableRoomPairs;
            public bool reopenedAndVerified, lightingRebaked;
        }

        /// <summary>Repairs only the spare lounger in an explicitly isolated acceptance scene.</summary>
        public static void AuthorSpareLoungerAcceptance()
        {
            string project = RequireAcceptanceProject();
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var all = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true)).ToArray();
            var homes = HouseInteractionAnchors.InScene(scene)
                .Where(anchor => anchor.VenueId == HouseFurniture.LoungerAnchor && (anchor.Slot == 0 || anchor.Slot == 1)).ToArray();
            if (homes.Length != 2 || homes.Select(anchor => anchor.Slot).Distinct().Count() != 2)
                throw new InvalidOperationException("Expected the two existing saved Home lounger seats.");
            var loungers = all.Where(item => item.name == "bb_set_lounger" && item.gameObject.activeInHierarchy).ToArray();
            if (loungers.Length != 3) throw new InvalidOperationException("Expected exactly three existing authored loungers.");
            var spare = loungers.Single(item => !homes.Any(anchor => anchor.transform.IsChildOf(item)));
            var surface = all.Select(item => item.GetComponent<NavMeshSurface>()).Single(item => item != null);
            var original = surface.navMeshData;
            if (original == null) throw new InvalidOperationException("The house has no existing navigation asset.");
            var renderers = spare.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) throw new InvalidOperationException("The spare lounger has no imported renderers.");
            bool rebakeLighting = renderers.Any(renderer => renderer.lightmapIndex >= 0 && renderer.lightmapIndex < LightmapSettings.lightmaps.Length);
            if (rebakeLighting && !Environment.GetCommandLineArgs().Contains("-gamesimRebakeExistingHouseLighting"))
                throw new InvalidOperationException("A lightmapped spare lounger requires a separately reviewed lighting bake.");
            if (rebakeLighting)
            {
                const string ownedLighting = "Assets/Gamesim/Scenes/EpisodeHouse/";
                if (AssetDatabase.GetAssetPath(Lightmapping.lightingDataAsset) != ownedLighting + "LightingData.asset"
                    || Lightmapping.lightingSettings == null
                    || AssetDatabase.GetAssetPath(Lightmapping.lightingSettings) != HouseCinematicLighting.LightingSettingsPath
                    || LightmapSettings.lightmaps.Any(map => map.lightmapColor == null
                        || !AssetDatabase.GetAssetPath(map.lightmapColor).StartsWith(ownedLighting, StringComparison.Ordinal)))
                    throw new InvalidOperationException("The lighting bake must own only this episode's existing lighting outputs and settings.");
            }
            var floor = all.Single(item => item.name == "Competition yard floor").GetComponent<Renderer>();
            if (floor == null) throw new InvalidOperationException("The yard has no authored floor.");
            var report = new SpareLoungerReport {
                scene = ScenePath, navigationGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(original)),
                movedObject = HierarchyPath(spare), before = spare.position,
                roomPairsBefore = HouseNavigationAudit.ReachablePairs(surface, original),
                protectedScene = ProtectedSceneState(scene, rebakeLighting), protectedTransforms = UnchangedTransforms(scene, spare),
                reachableApproaches = ReachableApproaches(scene).OrderBy(value => value, StringComparer.Ordinal).ToArray()
            };
            report.reachableRoomPairs = ReachableRoomPairs(scene);
            var room = floor.bounds;
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            var target = new Vector3(room.center.x + room.size.x * HouseSetPieces.SpareLoungerFloorX,
                room.max.y, room.center.z - room.size.z * .40f);
            // This changes only one imported instance, not the retained set, its Home anchors,
            // any collision dimensions, lighting configuration, or the two baseline scenes.
            spare.position += target - new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            report.after = spare.position;
            Physics.SyncTransforms();
            VerifyCollisionClearance(scene, spare, 1);
            surface.BuildNavMesh();
            var candidate = surface.navMeshData;
            if (candidate == null) throw new InvalidOperationException("The spare-lounger navigation bake produced no data.");
            report.roomPairsAfter = HouseNavigationAudit.ReachablePairs(surface, candidate);
            if (report.roomPairsAfter < report.roomPairsBefore || report.reachableApproaches.Except(ReachableApproaches(scene)).Any()
                || report.reachableRoomPairs.Except(ReachableRoomPairs(scene)).Any())
                throw new InvalidOperationException("Moving the spare lounger lost previously reachable rooms or saved approaches.");
            EditorUtility.CopySerialized(candidate, original);
            surface.navMeshData = original;
            EditorUtility.SetDirty(original);
            EditorUtility.SetDirty(surface);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save the repaired acceptance scene.");
            AssetDatabase.SaveAssetIfDirty(original);
            if (rebakeLighting)
            {
                // Keep the existing lights, probes, settings and materials. Re-running Apply would
                // redress the whole lighting rig; this bake updates only its generated output.
                if (!Lightmapping.Bake() || Lightmapping.lightingDataAsset == null
                    || LightmapSettings.lightmaps.Length == 0 || LightmapSettings.lightmaps.Any(map => map.lightmapColor == null))
                    throw new InvalidOperationException("The existing house lighting did not finish a valid bake.");
                report.lightingRebaked = true;
                if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save the freshly lit acceptance scene.");
                AssetDatabase.SaveAssets();
            }
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            all = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true)).ToArray();
            spare = all.Single(item => HierarchyPath(item) == report.movedObject);
            surface = all.Select(item => item.GetComponent<NavMeshSurface>()).Single(item => item != null);
            if (AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(surface.navMeshData)) != report.navigationGuid
                || Vector3.Distance(spare.position, report.after) > .001f
                || !report.protectedScene.SequenceEqual(ProtectedSceneState(scene, rebakeLighting))
                || !report.protectedTransforms.SequenceEqual(UnchangedTransforms(scene, spare)))
                throw new InvalidOperationException("The saved repair changed protected house content or its navigation identity.");
            report.roomPairsAfter = HouseNavigationAudit.ReachablePairs(surface, surface.navMeshData);
            if (report.roomPairsAfter < report.roomPairsBefore || report.reachableApproaches.Except(ReachableApproaches(scene)).Any()
                || report.reachableRoomPairs.Except(ReachableRoomPairs(scene)).Any())
                throw new InvalidOperationException("The reopened spare-lounger scene lost accepted navigation.");
            report.reopenedAndVerified = true;
            string directory = Path.Combine(project, "Logs");
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "spare-lounger-authoring.json"), JsonUtility.ToJson(report, true));
            Debug.Log("[Gamesim] Spare lounger repaired: " + report.before.ToString("F3") + " -> " + report.after.ToString("F3")
                + "; retained " + report.roomPairsAfter + " reachable room pairs and all saved approaches.");
        }

        private static string[] UnchangedTransforms(Scene scene, Transform moved)
        {
            return scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Where(item => item != moved)
                .Select(item => FormattableString.Invariant($"{HierarchyPath(item)}:{item.localPosition:F5}:{item.localRotation:F5}:{item.localScale:F5}:{item.gameObject.activeSelf}"))
                .OrderBy(value => value, StringComparer.Ordinal).ToArray();
        }

        private static string[] ReachableRoomPairs(Scene scene)
        {
            var points = new List<KeyValuePair<string, Vector3>>();
            foreach (var room in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<HouseRoomMarker>())
                .Where(room => room.isActiveAndEnabled && !string.IsNullOrEmpty(room.RoomName))
                .OrderBy(room => HierarchyPath(room.transform), StringComparer.Ordinal))
                if (NavMesh.SamplePosition(room.transform.position, out var hit, 2f, NavMesh.AllAreas))
                    points.Add(new KeyValuePair<string, Vector3>(HierarchyPath(room.transform), hit.position));
            var pairs = new List<string>();
            var path = new NavMeshPath();
            for (int a = 0; a < points.Count; a++)
                for (int b = a + 1; b < points.Count; b++)
                    if (NavMesh.CalculatePath(points[a].Value, points[b].Value, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete)
                        pairs.Add(points[a].Key + " -> " + points[b].Key);
            return pairs.ToArray();
        }

        /// <summary>Explicit batch entry for a named acceptance copy; never an import callback.</summary>
        public static void AuthorAcceptance()
        {
            string project = RequireAcceptanceProject();

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var surface = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<NavMeshSurface>(true)).Single();
            var original = surface.navMeshData;
            if (original == null) throw new InvalidOperationException("The house has no accepted navigation data.");
            var report = new Report {
                scene = ScenePath,
                roomPairsBefore = HouseNavigationAudit.ReachablePairs(surface, original),
                navigationGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(original)),
                protectedSceneBefore = ProtectedSceneState(scene)
            };
            var previouslyReachable = ReachableApproaches(scene);
            report.approachesBefore = previouslyReachable.Count;
            report.reachableApproachesBefore = previouslyReachable.OrderBy(value => value, StringComparer.Ordinal).ToArray();
            var world = scene.GetRootGameObjects().Single(g => g.name == "House Architecture").transform;
            var pieces = world.Find(HouseSetPieces.RootName);
            if (pieces == null) throw new InvalidOperationException("The furnished house is missing its set-piece root.");
            Place(world, pieces, report.placed);
            VerifyCollisionClearance(scene, pieces.Find(GroupName));

            surface.BuildNavMesh();
            var candidate = surface.navMeshData;
            if (candidate == null) throw new InvalidOperationException("The furnishing navigation bake produced no data.");
            report.roomPairsAfter = HouseNavigationAudit.ReachablePairs(surface, candidate);
            var after = ReachableApproaches(scene);
            report.approachesAfter = after.Count;
            var lost = previouslyReachable.Except(after).ToArray();
            if (report.roomPairsAfter < report.roomPairsBefore || lost.Length != 0)
            {
                surface.navMeshData = original;
                throw new InvalidOperationException("Furnishings blocked existing navigation: room pairs "
                    + report.roomPairsBefore + " -> " + report.roomPairsAfter + "; approaches lost: " + string.Join(", ", lost));
            }
            // Keep the existing GUID used by the scene, while saving the newly measured geometry.
            EditorUtility.CopySerialized(candidate, original);
            surface.navMeshData = original;
            EditorUtility.SetDirty(original);
            EditorUtility.SetDirty(surface);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save the furnished acceptance scene.");
            AssetDatabase.SaveAssetIfDirty(original);
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            surface = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<NavMeshSurface>(true)).Single();
            if (AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(surface.navMeshData)) != report.navigationGuid)
                throw new InvalidOperationException("The saved furnishing scene changed its navigation asset GUID.");
            report.protectedSceneAfter = ProtectedSceneState(scene);
            if (!report.protectedSceneBefore.SequenceEqual(report.protectedSceneAfter))
                throw new InvalidOperationException("The saved furnishing scene changed existing interaction anchors or baked lighting.");
            int reopenedPairs = HouseNavigationAudit.ReachablePairs(surface, surface.navMeshData);
            var reopenedApproaches = ReachableApproaches(scene);
            if (reopenedPairs < report.roomPairsBefore || previouslyReachable.Except(reopenedApproaches).Any())
                throw new InvalidOperationException("The reopened furnishing scene lost accepted navigation.");
            report.roomPairsAfter = reopenedPairs;
            report.approachesAfter = reopenedApproaches.Count;
            report.reopenedAndVerified = true;
            string directory = Path.Combine(project, "Logs");
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "house-amenities-authoring.json"), JsonUtility.ToJson(report, true));
            Debug.Log("[Gamesim] House amenities: " + report.placed.Count + " models; navigation "
                + report.roomPairsAfter + " room pairs; " + report.approachesAfter + " reachable approaches.");
        }

        /// <summary>Rechecks the written scene and navigation asset in a separate Unity process.</summary>
        public static void VerifySavedAcceptance()
        {
            string project = RequireAcceptanceProject();
            string directory = Path.Combine(project, "Logs");
            var report = JsonUtility.FromJson<Report>(File.ReadAllText(Path.Combine(directory, "house-amenities-authoring.json")));
            if (report == null || !report.reopenedAndVerified || report.reachableApproachesBefore == null)
                throw new InvalidOperationException("A completed house authoring report is required.");
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var surface = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<NavMeshSurface>(true)).Single();
            var pieces = scene.GetRootGameObjects().Single(g => g.name == "House Architecture").transform.Find(HouseSetPieces.RootName);
            var group = pieces == null ? null : pieces.Find(GroupName);
            if (group == null || group.childCount != 5 || surface.navMeshData == null
                || AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(surface.navMeshData)) != report.navigationGuid)
                throw new InvalidOperationException("The fresh process did not load the authored house and its original navigation asset.");
            VerifyCollisionClearance(scene, group);
            report.protectedSceneAfter = ProtectedSceneState(scene);
            report.roomPairsAfter = HouseNavigationAudit.ReachablePairs(surface, surface.navMeshData);
            var approaches = ReachableApproaches(scene);
            report.approachesAfter = approaches.Count;
            if (!report.protectedSceneBefore.SequenceEqual(report.protectedSceneAfter)
                || report.roomPairsAfter < report.roomPairsBefore || report.reachableApproachesBefore.Except(approaches).Any())
                throw new InvalidOperationException("Fresh-process house verification changed existing anchors, lighting or navigation.");
            File.WriteAllText(Path.Combine(directory, "house-amenities-verification.json"), JsonUtility.ToJson(report, true));
            Debug.Log("[Gamesim] Fresh-process house verification passed.");
        }

        private static string RequireAcceptanceProject()
        {
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string[] arguments = Environment.GetCommandLineArgs();
            int key = Array.IndexOf(arguments, "-gamesimHouseAuthoringRoot");
            if (!Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode
                || key < 0 || key + 1 >= arguments.Length
                || !string.Equals(project.TrimEnd('\\', '/'), Path.GetFullPath(arguments[key + 1]).TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase)
                || Path.GetFileName(project).IndexOf("Acceptance", StringComparison.OrdinalIgnoreCase) < 0)
                throw new InvalidOperationException("House authoring requires the explicitly named isolated acceptance copy.");
            return project;
        }

        /// <summary>Shared with the full set rebuild so an explicit rebuild retains these props.</summary>
        public static void Place(Transform world, Transform pieces, List<string> evidence = null)
        {
            foreach (string id in ModelIds)
                if (HouseCatalogue.Resolve(id, out var tier) == null || tier != HouseCatalogue.Tier.Authored)
                    throw new InvalidOperationException("Missing authored house furnishing: " + id);
            var existing = pieces.Find(GroupName);
            int siblingIndex = existing == null ? pieces.childCount : existing.GetSiblingIndex();
            if (existing != null) UnityEngine.Object.DestroyImmediate(existing.gameObject);
            var group = new GameObject(GroupName).transform;
            group.SetParent(pieces, false);
            group.SetSiblingIndex(siblingIndex);
            Put(world, group, "Games floor", "bb_set_pooltable", -.17f, .22f, 0, evidence);
            Put(world, group, "HoH floor", "bb_set_hohbench", -.12f, .06f, 0, evidence);
            Put(world, group, "Competition yard floor", "bb_set_lighttower", -.25f, .36f, 155, evidence);
            Put(world, group, "Competition yard floor", "bb_set_lighttower", .27f, .33f, 205, evidence);
            Put(world, group, "Competition yard floor", "bb_set_studiocam", -.43f, -.25f, 250, evidence);
        }

        private static void Put(Transform world, Transform group, string floorName, string id, float x, float z, float yaw, List<string> evidence)
        {
            var floor = world.GetComponentsInChildren<Transform>(true).Single(t => t.name == floorName).GetComponent<Renderer>();
            if (floor == null) throw new InvalidOperationException("No furnished room floor: " + floorName);
            var prop = HouseSetPieces.Model(id, group, yaw, 0);
            if (prop == null) throw new InvalidOperationException("Could not instantiate " + id);
            var renderers = prop.GetComponentsInChildren<Renderer>();
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            var room = floor.bounds;
            var target = new Vector3(room.center.x + room.size.x * x, room.max.y, room.center.z + room.size.z * z);
            prop.transform.position += target - new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            foreach (var renderer in renderers)
            {
                // Existing baked room probes provide indirect light; the new pieces receive live
                // direct light and shadows without invalidating the house's accepted lightmaps.
                GameObjectUtility.SetStaticEditorFlags(renderer.gameObject, 0);
                renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
            }
            var verdict = HouseFurnitureCollision.Judge(prop.transform, out var local);
            if (verdict != HouseFurnitureCollision.Verdict.Fit)
                throw new InvalidOperationException(id + " cannot safely receive furniture collision: " + verdict);
            HouseFurnitureCollision.Attach(prop.transform, local);
            var imported = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) imported.Encapsulate(renderer.bounds);
            evidence?.Add(id + " @ " + prop.transform.position.ToString("F3") + ", yaw " + yaw
                + ", bounds center " + imported.center.ToString("F3") + ", size " + imported.size.ToString("F3"));
        }

        private static string[] ProtectedSceneState(Scene scene, bool excludeBakeOutputs = false)
        {
            var state = new List<string>();
            foreach (var anchor in HouseInteractionAnchors.InScene(scene))
                state.Add(FormattableString.Invariant($"anchor:{HierarchyPath(anchor.transform)}:{anchor.VenueId}:{anchor.RoomId}:{anchor.Slot}:{anchor.Pose}:{anchor.Position:F5}:{anchor.Facing:F5}:{anchor.Approach:F5}:{anchor.SeatContact:F5}:{anchor.CameraPosition:F5}"));
            foreach (var renderer in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Renderer>(true)))
            {
                if (InAmenities(renderer.transform)) continue;
                state.Add(excludeBakeOutputs
                    ? FormattableString.Invariant($"lighting-input:{HierarchyPath(renderer.transform)}:{renderer.lightProbeUsage}:{renderer.reflectionProbeUsage}:{GameObjectUtility.GetStaticEditorFlags(renderer.gameObject)}")
                    : FormattableString.Invariant($"lighting:{HierarchyPath(renderer.transform)}:{renderer.lightmapIndex}:{renderer.lightmapScaleOffset:F5}:{renderer.lightProbeUsage}:{renderer.reflectionProbeUsage}:{GameObjectUtility.GetStaticEditorFlags(renderer.gameObject)}"));
            }
            state.Add("lighting-data:" + AssetIdentity(Lightmapping.lightingDataAsset));
            state.Add("lighting-settings:" + AssetIdentity(Lightmapping.lightingSettings));
            if (excludeBakeOutputs) return state.OrderBy(value => value, StringComparer.Ordinal).ToArray();
            state.Add("light-probes:" + AssetIdentity(LightmapSettings.lightProbes));
            for (int i = 0; i < LightmapSettings.lightmaps.Length; i++)
            {
                var map = LightmapSettings.lightmaps[i];
                state.Add("lightmap:" + i + ":" + AssetIdentity(map.lightmapColor) + ":"
                    + AssetIdentity(map.lightmapDir) + ":" + AssetIdentity(map.shadowMask));
            }
            return state.OrderBy(value => value, StringComparer.Ordinal).ToArray();
        }

        private static bool InAmenities(Transform item)
        {
            for (var parent = item; parent != null; parent = parent.parent)
                if (parent.name == GroupName && parent.parent != null && parent.parent.name == HouseSetPieces.RootName) return true;
            return false;
        }

        private static string HierarchyPath(Transform item) => item.parent == null
            ? item.name + "[" + item.GetSiblingIndex() + "]"
            : HierarchyPath(item.parent) + "/" + item.name + "[" + item.GetSiblingIndex() + "]";

        private static string AssetIdentity(UnityEngine.Object asset) => asset == null ? "none"
            : AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string guid, out long id) ? guid + ":" + id : "unsaved:" + asset.name;

        private static void VerifyCollisionClearance(Scene scene, Transform group, int expected = 5)
        {
            var furnishings = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Collider>(true))
                .Where(c => c.enabled && c.gameObject.activeInHierarchy && c.gameObject.layer == HouseLayers.Furniture).ToArray();
            var added = group.GetComponentsInChildren<Collider>(true).Where(c => c.enabled && c.gameObject.activeInHierarchy).ToArray();
            if (added.Length != expected) throw new InvalidOperationException("Each new furnishing must have exactly one solid proxy.");
            foreach (var current in added)
                foreach (var other in furnishings)
                {
                    if (current == other) continue;
                    if (Physics.ComputePenetration(current, current.transform.position, current.transform.rotation,
                        other, other.transform.position, other.transform.rotation, out _, out float depth) && depth > .001f)
                        throw new InvalidOperationException("New furnishing overlaps existing furniture: "
                            + HierarchyPath(current.transform) + " / " + HierarchyPath(other.transform) + ", depth " + depth);
                }
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
    }
}
