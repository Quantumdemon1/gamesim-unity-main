using System;
using System.IO;
using System.Linq;
using Gamesim.Core;
using Gamesim.House;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace Gamesim.Editor
{
    public static class HousePrototypeSetup
    {
        public const string ScenePath = "Assets/Gamesim/Scenes/HousePrototype.unity";
        private const string ArtPath = "Assets/Gamesim/Art/Prototype";
        private const string NavigationPath = "Assets/Gamesim/Data/HousePrototypeNavMesh.asset";

        [MenuItem("Gamesim/U02/Create or Register House Prototype")]
        public static void CreateOrRegister()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before setting up the house.");
            // Refuse to replace unsaved work; creating the scene is additive and registration is repeatable.
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save modified scenes before creating the house prototype.");

            Directory.CreateDirectory(ArtPath);
            Directory.CreateDirectory("Assets/Gamesim/Scenes");
            Directory.CreateDirectory("Assets/Gamesim/Data");
            AssetDatabase.Refresh();
            if (!File.Exists(ScenePath)) CreateScene();
            RegisterBuildScene();
            AssetDatabase.SaveAssets();
            Debug.Log("U02 house prototype registered. Open Assets/Gamesim/Scenes/HousePrototype.unity and press Play.");
        }

        private static void CreateScene()
        {
            var previousActive = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                var world = new GameObject("House Architecture");
                var wall = Material("Walls", new Color(0.105f, 0.115f, 0.125f));
                var wood = Material("Warm Oak", new Color(0.48f, 0.31f, 0.19f));
                // Shell tones above are the darkened broadcast values. They are only used
                // when a material asset does not exist yet, but leaving the old bright ones
                // here meant a regenerated set would silently come back as the pastel house.
                var livingFloor = Material("Living Room Floor", new Color(0.105f, 0.080f, 0.060f));
                var tile = Material("Kitchen Stone", new Color(0.080f, 0.105f, 0.115f));
                var bedroom = Material("Bedroom Floor", new Color(0.085f, 0.085f, 0.130f));
                var privateFloor = Material("Private Room Floor", new Color(0.070f, 0.105f, 0.095f));
                var grass = Material("Yard", new Color(0.055f, 0.110f, 0.080f));
                var dark = Material("Ink", new Color(0.065f, 0.11f, 0.15f));
                var mint = Material("Mint", new Color(0.26f, 0.76f, 0.65f));
                var coral = Material("Coral", new Color(0.88f, 0.42f, 0.32f));
                var cream = Material("Linen", new Color(0.88f, 0.82f, 0.66f));
                var brass = Material("Brass", new Color(0.77f, 0.60f, 0.28f));

                Floor(world.transform, "Living room floor", new Vector3(-7, -0.15f, -5), new Vector3(14, 0.3f, 10), livingFloor);
                Floor(world.transform, "Kitchen floor", new Vector3(7, -0.15f, -5), new Vector3(14, 0.3f, 10), tile);
                Floor(world.transform, "Bedroom floor", new Vector3(-7, -0.15f, 5), new Vector3(14, 0.3f, 10), bedroom);
                Floor(world.transform, "Private room floor", new Vector3(7, -0.15f, 5), new Vector3(14, 0.3f, 10), privateFloor);
                Floor(world.transform, "Competition yard floor", new Vector3(0, -0.15f, 15), new Vector3(28, 0.3f, 10), grass);

                Box(world.transform, "South cutaway wall", new Vector3(0, 0.55f, -10), new Vector3(28.3f, 1.1f, 0.25f), wall);
                Box(world.transform, "West wall", new Vector3(-14, 0.75f, 0), new Vector3(0.25f, 1.5f, 20), wall);
                Box(world.transform, "East wall", new Vector3(14, 0.75f, 0), new Vector3(0.25f, 1.5f, 20), wall);
                HorizontalDoor(world.transform, "Living / bedroom", -14, 0, 0, -7, wall);
                HorizontalDoor(world.transform, "Kitchen / private room", 0, 14, 0, 7, wall);
                HorizontalDoor(world.transform, "House / yard", -14, 14, 10, 0, wall);
                VerticalDoor(world.transform, "Living / kitchen", -10, 0, -5, wall);
                VerticalDoor(world.transform, "Bedroom / private room", 0, 10, 5, wall);
                Box(world.transform, "North garden fence", new Vector3(0, 0.6f, 20), new Vector3(28.2f, 1.2f, 0.18f), dark);
                Box(world.transform, "West garden fence", new Vector3(-14, 0.6f, 15), new Vector3(0.18f, 1.2f, 10), dark);
                Box(world.transform, "East garden fence", new Vector3(14, 0.6f, 15), new Vector3(0.18f, 1.2f, 10), dark);

                // Simple furniture establishes scale and creates real pathfinding obstacles.
                Rug(world.transform, "Living rug", new Vector3(-9, 0.01f, -4), new Vector3(6, 0.02f, 5), cream);
                Box(world.transform, "Sofa seat", new Vector3(-11, 0.4f, -4), new Vector3(1.4f, 0.8f, 4), mint);
                Box(world.transform, "Sofa back", new Vector3(-11.7f, 0.8f, -4), new Vector3(0.25f, 0.8f, 4), mint);
                Box(world.transform, "Coffee table", new Vector3(-8.5f, 0.3f, -4), new Vector3(1.5f, 0.6f, 2), wood);
                Box(world.transform, "Television console", new Vector3(-5, 0.5f, -1.5f), new Vector3(4, 1, 0.8f), dark);
                Box(world.transform, "Television", new Vector3(-5, 1.4f, -1.5f), new Vector3(3, 1.2f, 0.15f), dark);
                Box(world.transform, "Kitchen island", new Vector3(7, 0.55f, -5), new Vector3(3.5f, 1.1f, 1.7f), cream);
                Box(world.transform, "Countertop", new Vector3(7, 1.15f, -5), new Vector3(3.7f, 0.15f, 1.9f), dark);
                Box(world.transform, "Kitchen counters", new Vector3(12.5f, 0.5f, -5), new Vector3(1.2f, 1, 7), wood);
                Box(world.transform, "Refrigerator", new Vector3(12.5f, 1, -8.5f), new Vector3(1.2f, 2, 1.2f), cream);
                foreach (float x in new[] { -11f, -5f })
                {
                    Box(world.transform, "Bed frame", new Vector3(x, 0.25f, 6.5f), new Vector3(2.3f, 0.5f, 3.8f), wood);
                    Box(world.transform, "Duvet", new Vector3(x, 0.6f, 6.5f), new Vector3(2.2f, 0.3f, 3.5f), cream);
                    Box(world.transform, "Pillow", new Vector3(x, 0.8f, 7.7f), new Vector3(1.6f, 0.2f, 0.65f), mint);
                }
                Box(world.transform, "Wardrobe", new Vector3(-12, 1, 1.5f), new Vector3(2.4f, 2, 1), dark);
                Rug(world.transform, "Private room rug", new Vector3(8, 0.01f, 6), new Vector3(6, 0.02f, 5), cream);
                Box(world.transform, "Confessional chair A", new Vector3(6, 0.4f, 7), new Vector3(1.2f, 0.8f, 1.2f), coral);
                Box(world.transform, "Confessional chair B", new Vector3(10, 0.4f, 7), new Vector3(1.2f, 0.8f, 1.2f), mint);
                Box(world.transform, "Private table", new Vector3(8, 0.35f, 7), new Vector3(1.1f, 0.7f, 1.1f), wood);
                for (int i = 0; i < 3; i++)
                {
                    Box(world.transform, "Competition podium " + (i + 1), new Vector3(-6 + i * 6, 0.5f, 17), new Vector3(2.2f, 1, 1.3f), i == 1 ? brass : mint);
                }
                foreach (var position in new[] { new Vector3(-12, 0, -8), new Vector3(12, 0, 8), new Vector3(-12, 0, 18), new Vector3(12, 0, 18) })
                {
                    Box(world.transform, "Planter", position + Vector3.up * 0.3f, new Vector3(1, 0.6f, 1), cream);
                    Primitive(world.transform, "Foliage", PrimitiveType.Sphere, position + Vector3.up * 1.15f, Vector3.one * 1.5f, grass, false);
                }

                Label(world.transform, "LIVING", new Vector3(-10, 0.03f, -8.7f));
                Label(world.transform, "KITCHEN", new Vector3(6, 0.03f, -8.7f));
                Label(world.transform, "BEDROOM", new Vector3(-7, 0.03f, 3));
                Label(world.transform, "PRIVATE ROOM", new Vector3(8, 0.03f, 2));
                Label(world.transform, "COMPETITION YARD", new Vector3(0, 0.03f, 13));

                // Authored furniture is purely visual and collider-free, so it cannot alter the bake.
                HouseFurnishing.Apply(world.transform);

                var surface = world.AddComponent<NavMeshSurface>();
                surface.collectObjects = CollectObjects.Children;
                surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
                surface.BuildNavMesh();
                if (surface.navMeshData == null) throw new InvalidOperationException("House NavMesh bake produced no data.");
                if (AssetDatabase.LoadAssetAtPath<NavMeshData>(NavigationPath) != null)
                    throw new InvalidOperationException("A NavMesh asset already exists without its scene. Restore the scene or choose a new asset path.");
                AssetDatabase.CreateAsset(surface.navMeshData, NavigationPath);

                var playerObject = Character("Player", new Vector3(-5, 0, -7), mint, cream);
                Primitive(playerObject.transform, "Selected player marker", PrimitiveType.Cylinder,
                    new Vector3(0, 0.03f, 0), new Vector3(1.1f, 0.02f, 1.1f), mint, false);
                var agent = playerObject.AddComponent<NavMeshAgent>();
                agent.enabled = false; // HousePlayerController.Start waits for NavMeshSurface registration.
                agent.radius = 0.35f;
                agent.height = 1.8f;
                agent.speed = 4;
                agent.angularSpeed = 540;
                agent.acceleration = 20;
                agent.stoppingDistance = 0.15f;
                var player = playerObject.AddComponent<HousePlayerController>();
                var cameraRigObject = new GameObject("House Camera Rig");
                var cameraObject = new GameObject("Main Camera");
                cameraObject.transform.SetParent(cameraRigObject.transform);
                cameraObject.tag = "MainCamera";
                var camera = cameraObject.AddComponent<Camera>();
                camera.fieldOfView = 55;
                camera.nearClipPlane = 0.15f;
                camera.farClipPlane = 150;
                camera.backgroundColor = new Color(0.07f, 0.10f, 0.14f);
                camera.clearFlags = CameraClearFlags.SolidColor;
                cameraObject.AddComponent<AudioListener>();
                var rig = cameraRigObject.AddComponent<HouseCameraRig>();
                var cameraSettings = new SerializedObject(rig);
                cameraSettings.FindProperty("distance").floatValue = 38;
                cameraSettings.FindProperty("maximumDistance").floatValue = 48;
                cameraSettings.ApplyModifiedPropertiesWithoutUndo();
                rig.Configure(playerObject.transform);
                rig.ConfigureBounds(new Vector3(0, 1.2f, 4), new Vector2(16, 17));
                player.Configure(camera);

                var npcObject = Character("Maya", new Vector3(-4, 0, -3), coral, cream);
                npcObject.transform.rotation = Quaternion.Euler(0, 180, 0);
                var npc = npcObject.AddComponent<HouseNpc>();
                npc.Configure("maya", "Maya");
                var obstacle = npcObject.AddComponent<NavMeshObstacle>();
                obstacle.shape = NavMeshObstacleShape.Capsule;
                obstacle.center = Vector3.up * 0.9f;
                obstacle.height = 1.8f;
                obstacle.radius = 0.4f;
                obstacle.carving = true;
                var namePlate = new GameObject("Maya name").AddComponent<TextMesh>();
                namePlate.transform.SetParent(npcObject.transform, false);
                namePlate.transform.localPosition = new Vector3(0, 2.4f, 0);
                namePlate.transform.rotation = camera.transform.rotation;
                namePlate.text = "MAYA";
                namePlate.anchor = TextAnchor.MiddleCenter;
                namePlate.fontSize = 64;
                namePlate.characterSize = 0.04f;
                namePlate.color = Color.white;
                var interaction = new GameObject("House Interaction").AddComponent<HouseInteraction>();
                interaction.Configure(player, npc, rig);
                var eventSystem = new GameObject("EventSystem");
                eventSystem.AddComponent<EventSystem>();
                eventSystem.AddComponent<InputSystemUIInputModule>();

                var light = new GameObject("Sun").AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.3f;
                light.color = new Color(1, 0.94f, 0.83f);
                light.transform.rotation = Quaternion.Euler(50, -35, 0);
                light.shadows = LightShadows.Soft;
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                // Dark, cool ambient is what makes the neon outlines and accent lights read.
                RenderSettings.ambientLight = new Color(0.14f, 0.16f, 0.22f);
                RenderSettings.sun = light;

                Room("Living", new Vector3(-5, 0, -7));
                Room("Kitchen", new Vector3(4, 0, -7));
                Room("Bedroom", new Vector3(-7, 0, 2));
                Room("Private", new Vector3(7, 0, 3));
                Room("Yard", new Vector3(0, 0, 14));
                if (!EditorSceneManager.SaveScene(scene, ScenePath))
                    throw new IOException("Could not save the house scene.");
            }
            finally
            {
                if (previousActive.IsValid() && previousActive.isLoaded) SceneManager.SetActiveScene(previousActive);
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static void RegisterBuildScene()
        {
            var scenes = EditorBuildSettings.scenes
                .Where(s => s.path != FoundationInfo.BootstrapScenePath && s.path != ScenePath).ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            scenes.Insert(0, new EditorBuildSettingsScene(FoundationInfo.BootstrapScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static Material Material(string name, Color color)
        {
            string path = ArtPath + "/" + name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("URP/Lit shader is unavailable.");
            var value = new Material(shader) { name = name, color = color };
            value.SetFloat("_Smoothness", 0.18f);
            AssetDatabase.CreateAsset(value, path);
            return value;
        }

        private static GameObject Primitive(Transform parent, string name, PrimitiveType type, Vector3 position, Vector3 scale, Material material, bool collider = true)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material;
            if (!collider) UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        private static GameObject Box(Transform parent, string name, Vector3 pos, Vector3 size, Material material)
            => Primitive(parent, name, PrimitiveType.Cube, pos, size, material);

        private static void Floor(Transform parent, string name, Vector3 pos, Vector3 size, Material material)
            => Box(parent, name, pos, size, material).AddComponent<HouseWalkable>();

        private static void Rug(Transform parent, string name, Vector3 pos, Vector3 size, Material material)
            => Primitive(parent, name, PrimitiveType.Cube, pos, size, material, false);

        private static void HorizontalDoor(Transform parent, string name, float min, float max, float z, float door, Material material)
        {
            const float halfDoor = 1.4f;
            Box(parent, name + " left", new Vector3((min + door - halfDoor) / 2, 0.75f, z), new Vector3(door - halfDoor - min, 1.5f, 0.25f), material);
            Box(parent, name + " right", new Vector3((door + halfDoor + max) / 2, 0.75f, z), new Vector3(max - door - halfDoor, 1.5f, 0.25f), material);
        }

        private static void VerticalDoor(Transform parent, string name, float min, float max, float door, Material material)
        {
            const float halfDoor = 1.4f;
            Box(parent, name + " south", new Vector3(0, 0.75f, (min + door - halfDoor) / 2), new Vector3(0.25f, 1.5f, door - halfDoor - min), material);
            Box(parent, name + " north", new Vector3(0, 0.75f, (door + halfDoor + max) / 2), new Vector3(0.25f, 1.5f, max - door - halfDoor), material);
        }

        private static GameObject Character(string name, Vector3 position, Material shirt, Material skin)
        {
            var root = new GameObject(name);
            root.transform.position = position;
            var body = Primitive(root.transform, "Body", PrimitiveType.Capsule, new Vector3(0, 0.8f, 0), new Vector3(0.65f, 0.65f, 0.65f), shirt);
            Primitive(root.transform, "Head", PrimitiveType.Sphere, new Vector3(0, 1.65f, 0), Vector3.one * 0.5f, skin, false);
            var capsule = root.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(0, 0.95f, 0);
            capsule.height = 1.9f;
            capsule.radius = 0.35f;
            UnityEngine.Object.DestroyImmediate(body.GetComponent<Collider>());
            return root;
        }

        private static void Label(Transform parent, string text, Vector3 position)
        {
            var go = new GameObject(text + " label");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.rotation = Quaternion.Euler(90, 0, 0);
            var label = go.AddComponent<TextMesh>();
            label.text = text;
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.fontSize = 64;
            label.characterSize = 0.06f;
            label.color = new Color(0.95f, 0.95f, 0.87f);
        }

        private static void Room(string name, Vector3 position)
        {
            var go = new GameObject("Room - " + name);
            go.transform.position = position;
            go.AddComponent<HouseRoomMarker>().Configure(name);
        }
    }
}
