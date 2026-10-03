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
            public bool reopenedAndVerified;
            public List<string> placed = new List<string>();
        }

        /// <summary>Explicit batch entry for a named acceptance copy; never an import callback.</summary>
        public static void AuthorAcceptance()
        {
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string[] args = Environment.GetCommandLineArgs();
            int key = Array.IndexOf(args, "-gamesimHouseAuthoringRoot");
            if (!Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode
                || key < 0 || key + 1 >= args.Length
                || !string.Equals(project.TrimEnd('\\', '/'), Path.GetFullPath(args[key + 1]).TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase)
                || Path.GetFileName(project).IndexOf("Acceptance", StringComparison.OrdinalIgnoreCase) < 0)
                throw new InvalidOperationException("House authoring requires the explicitly named isolated acceptance copy.");

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
            var world = scene.GetRootGameObjects().Single(g => g.name == "House Architecture").transform;
            var pieces = world.Find(HouseSetPieces.RootName);
            if (pieces == null) throw new InvalidOperationException("The furnished house is missing its set-piece root.");
            Place(world, pieces, report.placed);

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

        /// <summary>Shared with the full set rebuild so an explicit rebuild retains these props.</summary>
        public static void Place(Transform world, Transform pieces, List<string> evidence = null)
        {
            foreach (string id in ModelIds)
                if (HouseCatalogue.Resolve(id, out var tier) == null || tier != HouseCatalogue.Tier.Authored)
                    throw new InvalidOperationException("Missing authored house furnishing: " + id);
            var existing = pieces.Find(GroupName);
            if (existing != null) UnityEngine.Object.DestroyImmediate(existing.gameObject);
            var group = new GameObject(GroupName).transform;
            group.SetParent(pieces, false);
            Put(world, group, "Games floor", "bb_set_pooltable", -.17f, .22f, 0, evidence);
            Put(world, group, "HoH floor", "bb_set_hohbench", -.12f, .06f, 0, evidence);
            Put(world, group, "Competition yard floor", "bb_set_lighttower", -.28f, .43f, 155, evidence);
            Put(world, group, "Competition yard floor", "bb_set_lighttower", .28f, .43f, 205, evidence);
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

        private static string[] ProtectedSceneState(Scene scene)
        {
            var state = new List<string>();
            foreach (var anchor in HouseInteractionAnchors.InScene(scene))
                state.Add(FormattableString.Invariant($"anchor:{HierarchyPath(anchor.transform)}:{anchor.VenueId}:{anchor.RoomId}:{anchor.Slot}:{anchor.Pose}:{anchor.Position:F5}:{anchor.Facing:F5}:{anchor.Approach:F5}:{anchor.SeatContact:F5}:{anchor.CameraPosition:F5}"));
            foreach (var renderer in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Renderer>(true)))
            {
                if (InAmenities(renderer.transform)) continue;
                state.Add(FormattableString.Invariant($"lighting:{HierarchyPath(renderer.transform)}:{renderer.lightmapIndex}:{renderer.lightmapScaleOffset:F5}:{renderer.lightProbeUsage}:{renderer.reflectionProbeUsage}:{GameObjectUtility.GetStaticEditorFlags(renderer.gameObject)}"));
            }
            state.Add("lighting-data:" + AssetIdentity(Lightmapping.lightingDataAsset));
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
