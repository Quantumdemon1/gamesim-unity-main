using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Gamesim.House;
using Gamesim.Presentation;
using Gamesim.Simulation;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace Gamesim.Editor
{
    /// <summary>Creates the native episode as a new scene while retaining U02 and editor scene setup.</summary>
    public static class EpisodeProjectSetup
    {
        public const string ScenePath = "Assets/Gamesim/Scenes/EpisodeHouse.unity";
        public const string NavigationPath = "Assets/Gamesim/Data/EpisodeHouseNavMesh.asset";
        private const string SourcePath = HousePrototypeSetup.ScenePath;
        private const string SourceNavigationPath = "Assets/Gamesim/Data/HousePrototypeNavMesh.asset";
        private const string DirectorTypeName = "Gamesim.Episode.EpisodeDirector, Gamesim.Runtime";
        private static readonly Vector3 ValidationOffset = new Vector3(4096f, 0f, 4096f);

        [MenuItem("Gamesim/Port/Create or Register Episode House")]
        public static void CreateOrRegister()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before creating or registering the episode scene.");
            for (int index = 0; index < SceneManager.sceneCount; index++)
            {
                var loaded = SceneManager.GetSceneAt(index);
                if (loaded.isDirty)
                    throw new InvalidOperationException("Save modified scenes before creating or registering EpisodeHouse: " + loaded.name);
            }

            // Existing authored destination content is never opened, rebuilt, or overwritten.
            if (File.Exists(ScenePath) || AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            {
                RegisterBuildScene();
                Debug.Log("Existing EpisodeHouse preserved and registered. Its scene content was not changed.");
                return;
            }
            if (File.Exists(ScenePath + ".meta"))
                throw new InvalidOperationException("An existing EpisodeHouse .meta file has no scene asset. Restore that scene before creating a new copy.");
            if (File.Exists(NavigationPath) || File.Exists(NavigationPath + ".meta") || AssetDatabase.LoadMainAssetAtPath(NavigationPath) != null)
                throw new InvalidOperationException("EpisodeHouse is missing but its NavMesh path already exists. Restore the matching scene or review " + NavigationPath + "; existing data will not be overwritten.");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(SourcePath) == null)
                throw new FileNotFoundException("Create the U02 HousePrototype before creating EpisodeHouse.", SourcePath);
            if (AssetDatabase.LoadAssetAtPath<NavMeshData>(SourceNavigationPath) == null)
                throw new FileNotFoundException("The existing U02 baked NavMesh is required; setup does not rebake or replace it.", SourceNavigationPath);

            // Validate the promised runtime contract before creating any asset.
            var directorType = Type.GetType(DirectorTypeName, false);
            if (directorType == null || !typeof(MonoBehaviour).IsAssignableFrom(directorType))
                throw new InvalidOperationException("The compiled Gamesim.Episode.EpisodeDirector component is required.");
            var configure = directorType.GetMethod("Configure", BindingFlags.Public | BindingFlags.Instance,
                null, new[] { typeof(HousePlayerController), typeof(HouseCameraRig), typeof(HouseNpc[]) }, null);
            if (configure == null)
                throw new InvalidOperationException("EpisodeDirector.Configure(HousePlayerController, HouseCameraRig, HouseNpc[]) is required.");

            var previousActive = SceneManager.GetActiveScene();
            Scene copiedScene = default;
            NavMeshDataInstance validationData = default;
            bool copiedAsset = false;
            bool copiedNavigation = false;
            bool savedSuccessfully = false;
            try
            {
                // Give the copied surface a unique asset GUID. Sharing the original causes the
                // navigation package's Editor OnValidate to clear one surface when both scenes open.
                if (!AssetDatabase.CopyAsset(SourceNavigationPath, NavigationPath))
                    throw new IOException("Unity could not copy the existing baked NavMesh for EpisodeHouse.");
                copiedNavigation = true;
                AssetDatabase.ImportAsset(NavigationPath, ImportAssetOptions.ForceSynchronousImport);
                if (!AssetDatabase.CopyAsset(SourcePath, ScenePath))
                    throw new IOException("Unity could not copy HousePrototype to the new EpisodeHouse scene.");
                copiedAsset = true;
                AssetDatabase.ImportAsset(ScenePath, ImportAssetOptions.ForceSynchronousImport);
                copiedScene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
                SceneManager.SetActiveScene(copiedScene);
                var surface = copiedScene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<NavMeshSurface>(true)).Single();
                var episodeData = AssetDatabase.LoadAssetAtPath<NavMeshData>(NavigationPath);
                if (episodeData == null) throw new IOException("The copied episode NavMesh could not be imported.");
                surface.RemoveData();
                surface.navMeshData = episodeData;
                EditorUtility.SetDirty(surface);
                // Validate the baked floor away from the live house's carving obstacles, including
                // Maya's own obstacle. Only this temporary instance is removed in finally.
                validationData = NavMesh.AddNavMeshData(episodeData, surface.transform.position + ValidationOffset, surface.transform.rotation);
                if (!validationData.valid) throw new InvalidOperationException("The copied baked NavMesh could not be registered for validation.");
                ConfigureScene(copiedScene, directorType, configure, ValidationOffset);
                EditorSceneManager.MarkSceneDirty(copiedScene);
                if (!EditorSceneManager.SaveScene(copiedScene, ScenePath))
                    throw new IOException("Unity could not save the configured EpisodeHouse scene.");
                savedSuccessfully = true;
            }
            finally
            {
                if (validationData.valid) validationData.Remove();
                // Additive opening retains every previously loaded scene, including untitled scenes.
                if (previousActive.IsValid() && previousActive.isLoaded) SceneManager.SetActiveScene(previousActive);
                if (copiedScene.IsValid() && copiedScene.isLoaded) EditorSceneManager.CloseScene(copiedScene, true);
                bool incompleteSceneRemoved = !copiedAsset;
                if (copiedAsset && !savedSuccessfully)
                {
                    // Roll back only the new copy made by this invocation. The original never changed.
                    incompleteSceneRemoved = AssetDatabase.DeleteAsset(ScenePath);
                    if (!incompleteSceneRemoved)
                        Debug.LogWarning("EpisodeHouse setup failed and its new incomplete copy could not be removed. Review " + ScenePath + " before retrying.");
                }
                if (copiedNavigation && !savedSuccessfully)
                {
                    if (!incompleteSceneRemoved)
                        Debug.LogWarning("The newly copied NavMesh was retained with the incomplete episode scene to preserve its reference. Review " + NavigationPath + " before retrying.");
                    else if (!AssetDatabase.DeleteAsset(NavigationPath))
                        Debug.LogWarning("EpisodeHouse setup failed and its newly copied NavMesh could not be removed. Review " + NavigationPath + " before retrying.");
                }
            }

            RegisterBuildScene();
            Debug.Log("EpisodeHouse created from U02: six native cast presentations, episode director, local audio, and a separate copy of the existing baked NavMesh. Original scene and NavMesh preserved without rebaking.");
        }

        private static void ConfigureScene(Scene scene, Type directorType, MethodInfo configure, Vector3 validationOffset)
        {
            var roots = scene.GetRootGameObjects();
            var player = roots.SelectMany(root => root.GetComponentsInChildren<HousePlayerController>(true)).Single();
            var rig = roots.SelectMany(root => root.GetComponentsInChildren<HouseCameraRig>(true)).Single();
            var existingNpcs = roots.SelectMany(root => root.GetComponentsInChildren<HouseNpc>(true)).ToArray();
            if (existingNpcs.Length != 1)
                throw new InvalidOperationException("The U02 source must contain exactly its one authored Maya NPC.");
            var maya = existingNpcs[0];
            if (ContentCatalog.CanonicalId(maya.Id) != ContentCatalog.MayaId)
                throw new InvalidOperationException("The U02 NPC does not have the expected Maya identity.");
            if (rig.ViewCamera == null)
                throw new InvalidOperationException("The copied house camera is missing.");

            foreach (var oldInteraction in roots.SelectMany(root => root.GetComponentsInChildren<HouseInteraction>(true)))
            {
                oldInteraction.enabled = false;
                EditorUtility.SetDirty(oldInteraction);
            }

            var filter = new NavMeshQueryFilter
            {
                agentTypeID = player.Agent.agentTypeID,
                areaMask = player.Agent.areaMask
            };
            var cast = ContentCatalog.Create(1u);
            var npcs = new List<HouseNpc> { maya };
            var spawnPositions = new Dictionary<string, Vector3>
            {
                { "taylor-kim", new Vector3(0f, 0f, 15f) },
                { "jamie-roberts", new Vector3(4f, 0f, -7f) },
                { "casey-wilson", new Vector3(-8f, 0f, -7f) },
                { "riley-johnson", new Vector3(-7f, 0f, 2f) }
            };

            // Clone the untouched U02 Maya before adding any serialized presentation definition.
            foreach (var character in cast.contestants.Where(character => !character.isPlayer && character.id != ContentCatalog.MayaId))
            {
                var clone = UnityEngine.Object.Instantiate(maya.gameObject);
                clone.name = character.name;
                if (clone.transform.parent != null) clone.transform.SetParent(null, true);
                SceneManager.MoveGameObjectToScene(clone, scene);
                var npc = clone.GetComponent<HouseNpc>();
                npc.Configure(character.id, character.name);
                clone.transform.position = spawnPositions[character.id];
                clone.transform.rotation = Quaternion.Euler(0f, character.id == "taylor-kim" ? 180f : 140f, 0f);
                Physics.SyncTransforms();
                clone.transform.position = ValidatedPosition(scene, clone, clone.transform.position, filter, validationOffset);
                ConfigureNameplate(npc, rig.ViewCamera);
                npcs.Add(npc);
            }

            var mayaState = cast.Find(ContentCatalog.MayaId);
            maya.Configure(mayaState.id, mayaState.name);
            maya.gameObject.name = mayaState.name;
            Physics.SyncTransforms();
            maya.transform.position = ValidatedPosition(scene, maya.gameObject, maya.transform.position, filter, validationOffset);
            ConfigureNameplate(maya, rig.ViewCamera);
            foreach (var npc in npcs)
            {
                var definition = cast.Find(npc.Id);
                var presentation = CharacterPresentation.Attach(npc.gameObject, definition, Palette(definition.id));
                EditorUtility.SetDirty(npc);
                EditorUtility.SetDirty(presentation);
            }
            var playerPresentation = CharacterPresentation.Attach(player.gameObject, cast.Find(cast.playerId), Palette(cast.playerId));
            EditorUtility.SetDirty(playerPresentation);

            // The expanded cast occupies two of U02's old diagnostic destinations. Keep these
            // room routes on clear floor, not inside an NPC's carving obstacle.
            foreach (var marker in roots.SelectMany(root => root.GetComponentsInChildren<HouseRoomMarker>(true)))
            {
                if (marker.RoomName == "Bedroom") marker.transform.position = new Vector3(-5,0,2);
                if (marker.RoomName == "Kitchen") marker.transform.position = new Vector3(3,0,-6);
                EditorUtility.SetDirty(marker.transform);
            }

            var cameraSettings = new SerializedObject(rig);
            cameraSettings.FindProperty("distance").floatValue = 48f;
            cameraSettings.FindProperty("maximumDistance").floatValue = Mathf.Max(56f, cameraSettings.FindProperty("maximumDistance").floatValue);
            cameraSettings.ApplyModifiedPropertiesWithoutUndo();
            rig.Configure(player.transform);
            player.Configure(rig.ViewCamera);
            EditorUtility.SetDirty(rig);
            EditorUtility.SetDirty(player);

            var directorObject = new GameObject("Gamesim Episode");
            SceneManager.MoveGameObjectToScene(directorObject, scene);
            var audio = HouseAudio.Attach(directorObject);
            var director = directorObject.AddComponent(directorType);
            try
            {
                configure.Invoke(director, new object[] { player, rig, npcs.ToArray() });
            }
            catch (TargetInvocationException exception)
            {
                throw new InvalidOperationException("EpisodeDirector could not configure its local scene references.", exception.InnerException ?? exception);
            }
            EditorUtility.SetDirty(audio);
            EditorUtility.SetDirty(director);
        }

        private static Vector3 ValidatedPosition(Scene scene, GameObject character, Vector3 requested, NavMeshQueryFilter filter, Vector3 validationOffset)
        {
            bool found = NavMesh.SamplePosition(requested + validationOffset, out var hit, 0.75f, filter);
            var floorPosition = hit.position - validationOffset;
            if (!found ||
                Vector2.Distance(new Vector2(requested.x, requested.z), new Vector2(floorPosition.x, floorPosition.z)) > 0.65f ||
                Mathf.Abs(requested.y - floorPosition.y) > 0.25f)
                throw new InvalidOperationException("No floor-level baked NavMesh near the requested position for " + character.name + ": " + requested);

            // NavMesh queries span loaded scenes, so collider validation is explicitly scene-local.
            var overlaps = Physics.OverlapCapsule(floorPosition + Vector3.up * 0.4f,
                floorPosition + Vector3.up * 1.45f, 0.35f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            foreach (var collider in overlaps)
            {
                if (collider.gameObject.scene != scene || collider.transform.IsChildOf(character.transform)) continue;
                throw new InvalidOperationException("The spawn for " + character.name + " overlaps " + collider.name + ". The original house was preserved.");
            }
            return floorPosition;
        }

        private static void ConfigureNameplate(HouseNpc npc, Camera camera)
        {
            var label = npc.GetComponentInChildren<TextMesh>(true);
            if (label == null) return;
            label.gameObject.name = npc.DisplayName + " name";
            label.text = npc.DisplayName;
            label.transform.rotation = camera.transform.rotation;
            EditorUtility.SetDirty(label);
        }

        private static Color Palette(string id)
        {
            string hex;
            switch (id)
            {
                case "maya-hassan": hex = "#476A88"; break;
                case "taylor-kim": hex = "#CF6D52"; break;
                case "jamie-roberts": hex = "#7E9E87"; break;
                case "casey-wilson": hex = "#C79C53"; break;
                case "riley-johnson": hex = "#807B9C"; break;
                default: hex = "#42C2A6"; break;
            }
            ColorUtility.TryParseHtmlString(hex, out var color);
            return color;
        }

        private static void RegisterBuildScene()
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.Any(scene => scene.path == ScenePath)) return;
            scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
