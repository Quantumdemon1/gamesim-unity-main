using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Gamesim.Editor
{
    /// <summary>
    /// Dresses the rooms with the rest of the Kenney kit, and adds the two set pieces the format is
    /// known by: the checkered kitchen floor and the roped entrance.
    ///
    /// <para>The furnishing pass places fourteen models, each one standing in for a primitive that
    /// was already there. That left <b>thirty-nine of the fifty-two models in the kit unplaced</b> —
    /// a stove, a sink, a microwave, lamps, stools, plants, a coat rack — because nothing in the
    /// blocked-out house stood in for them. This places those directly, from the room's own floor
    /// bounds.</para>
    ///
    /// <para><b>Nothing here has a collider.</b> The NavMesh is baked from collision, and adding
    /// solid props to a walkable floor would either carve holes in the mesh or require a rebake this
    /// pass is not in a position to verify. The trade is deliberate and worth stating plainly: a
    /// houseguest can walk through the stove. Props are placed against walls and in corners, where
    /// the navigation the cast actually uses does not go, so the compromise is rarely visible —
    /// but it is a compromise, not an oversight.</para>
    ///
    /// <para>The bathroom fixtures in the kit — toilet, bath, shower, basin, mirror — are not placed.
    /// This house has five rooms and none of them is a bathroom, and putting a toilet in the corner
    /// of the bedroom would be inventing a room rather than dressing one.</para>
    /// </summary>
    public static class HouseSetPieces
    {
        public const string RootName = "Set Pieces";
        private const string EpisodeScene = "Assets/Gamesim/Scenes/EpisodeHouse.unity";
        private const string WorldRoot = "House Architecture";
        private const string Kit = "Assets/Gamesim/Art/External/KenneyFurniture/";
        private const string ArtRoot = "Assets/Gamesim/Art/Prototype/";

        /// <summary>
        /// A prop, positioned as a fraction of its room's floor so the layout survives the house
        /// being resized. x and z run -0.5 to 0.5 from the room's centre.
        /// </summary>
        private readonly struct Prop
        {
            public readonly string Room, Model;
            public readonly float X, Z, Yaw, Height;

            public Prop(string room, string model, float x, float z, float yaw, float height)
            {
                Room = room; Model = model; X = x; Z = z; Yaw = yaw; Height = height;
            }
        }

        // Heights are in metres and chosen to read at a glance rather than to be exact: a stove
        // that is waist-high and a lamp that is head-high is the whole requirement.
        private static readonly Prop[] Plan =
        {
            // Kitchen — a working counter run along the north wall.
            new Prop("Kitchen floor", "kitchenStove",         -0.30f,  0.40f, 180f, 0.95f),
            new Prop("Kitchen floor", "kitchenSink",          -0.08f,  0.40f, 180f, 0.95f),
            new Prop("Kitchen floor", "kitchenMicrowave",      0.12f,  0.40f, 180f, 0.32f),
            new Prop("Kitchen floor", "kitchenCoffeeMachine",  0.26f,  0.40f, 180f, 0.34f),
            new Prop("Kitchen floor", "toaster",               0.36f,  0.40f, 180f, 0.22f),
            new Prop("Kitchen floor", "stoolBar",             -0.10f, -0.06f,   0f, 0.78f),
            new Prop("Kitchen floor", "stoolBar",              0.02f, -0.06f,   0f, 0.78f),
            new Prop("Kitchen floor", "stoolBar",              0.14f, -0.06f,   0f, 0.78f),
            new Prop("Kitchen floor", "trashcan",              0.44f, -0.38f,   0f, 0.60f),
            new Prop("Kitchen floor", "plantSmall1",          -0.44f, -0.40f,   0f, 0.55f),

            // Living room — lamps and a second seat, away from the walked routes.
            new Prop("Living room floor", "lampSquareFloor",  -0.43f,  0.38f,   0f, 1.55f),
            new Prop("Living room floor", "lampRoundFloor",    0.41f, -0.38f,   0f, 1.50f),
            new Prop("Living room floor", "speaker",          -0.43f, -0.26f,  90f, 0.95f),
            new Prop("Living room floor", "pottedPlant",       0.43f,  0.38f,   0f, 0.85f),
            new Prop("Living room floor", "loungeChairRelax",  0.22f, -0.20f, 210f, 0.90f),
            new Prop("Living room floor", "trashcan",         -0.20f,  0.42f,   0f, 0.55f),

            // Bedroom — bedside tables with lamps on them, and somewhere to hang a coat.
            new Prop("Bedroom floor", "sideTableDrawers",     -0.34f,  0.02f,   0f, 0.62f),
            new Prop("Bedroom floor", "lampSquareTable",      -0.34f,  0.02f,   0f, 0.45f),
            new Prop("Bedroom floor", "sideTableDrawers",      0.16f,  0.02f,   0f, 0.62f),
            new Prop("Bedroom floor", "lampSquareTable",       0.16f,  0.02f,   0f, 0.45f),
            new Prop("Bedroom floor", "coatRackStanding",     -0.44f,  0.40f,   0f, 1.70f),
            new Prop("Bedroom floor", "plantSmall2",           0.43f, -0.40f,   0f, 0.50f),
            new Prop("Bedroom floor", "bookcaseOpen",          0.43f,  0.34f, 270f, 1.60f),

            // Private room — kept sparse. It is where conversations happen and clutter reads as noise.
            new Prop("Private room floor", "lampRoundFloor",  -0.42f,  0.40f,   0f, 1.50f),
            new Prop("Private room floor", "plantSmall3",      0.43f,  0.40f,   0f, 0.55f),
            new Prop("Private room floor", "plantSmall1",      0.43f, -0.40f,   0f, 0.55f),

            // Yard — planting at the corners, either side of the entrance.
            new Prop("Competition yard floor", "pottedPlant", -0.42f, -0.34f,   0f, 0.90f),
            new Prop("Competition yard floor", "pottedPlant",  0.42f, -0.34f,   0f, 0.90f),
            new Prop("Competition yard floor", "plantSmall2", -0.30f,  0.40f,   0f, 0.55f),
            new Prop("Competition yard floor", "plantSmall3",  0.30f,  0.40f,   0f, 0.55f),
        };

        [MenuItem("Gamesim/U07/Add the remaining set pieces")]
        public static void Apply()
        {
            var scene = EditorSceneManager.OpenScene(EpisodeScene, OpenSceneMode.Single);
            var world = scene.GetRootGameObjects().FirstOrDefault(root => root.name == WorldRoot);
            if (world == null) throw new InvalidOperationException("No '" + WorldRoot + "' root in the episode scene.");

            var existing = world.transform.Find(RootName);
            if (existing != null) UnityEngine.Object.DestroyImmediate(existing.gameObject);

            var root = new GameObject(RootName).transform;
            root.SetParent(world.transform, false);

            int placed = 0, missing = 0;
            foreach (var prop in Plan)
            {
                var floor = world.transform.Find(prop.Room)?.GetComponent<Renderer>();
                if (floor == null) { Debug.LogWarning("[Gamesim] set pieces · no room floor: " + prop.Room); missing++; continue; }

                var source = AssetDatabase.LoadAssetAtPath<GameObject>(Kit + prop.Model + ".glb");
                if (source == null) { Debug.LogWarning("[Gamesim] set pieces · missing model: " + prop.Model); missing++; continue; }

                var bounds = floor.bounds;
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
                instance.transform.SetParent(root, false);
                instance.transform.localScale = Vector3.one;
                instance.transform.rotation = Quaternion.Euler(0f, prop.Yaw, 0f);

                // Scale to the intended height, then sit it on the floor by its own measured base.
                var raw = Measure(instance);
                float scale = raw.size.y > 0.001f ? prop.Height / raw.size.y : 1f;
                instance.transform.localScale = Vector3.one * scale;

                var scaled = Measure(instance);
                var target = new Vector3(
                    bounds.center.x + prop.X * bounds.size.x,
                    bounds.max.y,
                    bounds.center.z + prop.Z * bounds.size.z);
                instance.transform.position += target - new Vector3(scaled.center.x, scaled.min.y, scaled.center.z);

                foreach (var collider in instance.GetComponentsInChildren<Collider>(true))
                    UnityEngine.Object.DestroyImmediate(collider);

                instance.name = prop.Model;
                placed++;
            }

            int tiles = CheckerKitchen(world.transform, root);
            int entrance = Entrance(world.transform, root);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log(string.Format(
                "[Gamesim] set pieces · {0} props placed, {1} skipped, {2} floor tiles, {3} entrance parts",
                placed, missing, tiles, entrance));
        }

        /// <summary>
        /// The checkered kitchen floor: light tiles laid over the dark one, every other square.
        /// Only half the squares are built — the floor underneath is the other colour.
        /// </summary>
        private static int CheckerKitchen(Transform world, Transform root)
        {
            var floor = world.Find("Kitchen floor")?.GetComponent<Renderer>();
            // Its own tone rather than Linen. Linen is upholstery at 0.88 luminance, and a
            // checkerboard that bright against the darkened shell stops reading as a floor and
            // starts reading as a light source — it took over the frame.
            var tileMaterial = Tone("Kitchen Tile", new Color(0.56f, 0.55f, 0.52f));
            if (floor == null || tileMaterial == null) return 0;

            var bounds = floor.bounds;
            const int columns = 7, rows = 5;
            float tileX = bounds.size.x / columns, tileZ = bounds.size.z / rows;

            var group = new GameObject("Kitchen checker").transform;
            group.SetParent(root, false);

            int count = 0;
            for (int column = 0; column < columns; column++)
            for (int row = 0; row < rows; row++)
            {
                if ((column + row) % 2 != 0) continue;
                var tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
                tile.name = "Tile";
                tile.transform.SetParent(group, false);
                tile.transform.position = new Vector3(
                    bounds.min.x + (column + 0.5f) * tileX,
                    bounds.max.y + 0.01f,
                    bounds.min.z + (row + 0.5f) * tileZ);
                tile.transform.localScale = new Vector3(tileX * 0.97f, 0.02f, tileZ * 0.97f);
                tile.GetComponent<Renderer>().sharedMaterial = tileMaterial;
                UnityEngine.Object.DestroyImmediate(tile.GetComponent<Collider>());
                count++;
            }
            return count;
        }

        /// <summary>
        /// The roped entrance at the yard's outer edge: a gold arch and a run of stanchions, which
        /// is how the house announces itself in every broadcast of this format.
        /// </summary>
        private static int Entrance(Transform world, Transform root)
        {
            var yard = world.Find("Competition yard floor")?.GetComponent<Renderer>();
            var gold = AssetDatabase.LoadAssetAtPath<Material>(ArtRoot + "Emissive/Neon Gold.mat");
            var brass = AssetDatabase.LoadAssetAtPath<Material>(ArtRoot + "Brass.mat");
            var red = AssetDatabase.LoadAssetAtPath<Material>(ArtRoot + "Emissive/Neon Red.mat");
            if (yard == null || gold == null || red == null) return 0;
            if (brass == null) brass = gold;

            var bounds = yard.bounds;
            float z = bounds.max.z - 0.9f;          // just inside the far edge
            float centre = bounds.center.x;
            const float openWidth = 4.6f, height = 3.1f, post = 0.22f;

            var group = new GameObject("Entrance").transform;
            group.SetParent(root, false);

            int parts = 0;
            // Two uprights and a lintel.
            parts += Box(group, "Arch post left", gold,
                new Vector3(post, height, post), new Vector3(centre - openWidth * 0.5f, bounds.max.y + height * 0.5f, z));
            parts += Box(group, "Arch post right", gold,
                new Vector3(post, height, post), new Vector3(centre + openWidth * 0.5f, bounds.max.y + height * 0.5f, z));
            parts += Box(group, "Arch lintel", gold,
                new Vector3(openWidth + post * 2f, post * 1.4f, post),
                new Vector3(centre, bounds.max.y + height, z));

            // Stanchions and rope, running out to either side of the opening.
            for (int side = -1; side <= 1; side += 2)
            {
                float from = centre + side * (openWidth * 0.5f + 1.4f);
                for (int i = 0; i < 3; i++)
                {
                    float x = from + side * i * 1.6f;
                    parts += Box(group, "Stanchion", brass,
                        new Vector3(0.12f, 1.0f, 0.12f), new Vector3(x, bounds.max.y + 0.5f, z));
                    if (i == 2) continue;
                    parts += Box(group, "Rope", red,
                        new Vector3(1.6f, 0.07f, 0.07f), new Vector3(x + side * 0.8f, bounds.max.y + 0.82f, z));
                }
            }
            return parts;
        }

        /// <summary>Loads a flat material by name, creating it at the given tone if absent.</summary>
        private static Material Tone(string name, Color colour)
        {
            string path = ArtRoot + name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) return null;
            var material = new Material(shader) { name = name };
            material.SetFloat("_Smoothness", 0.14f);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", colour);
            if (material.HasProperty("_Color")) material.SetColor("_Color", colour);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static int Box(Transform parent, string name, Material material, Vector3 size, Vector3 position)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.position = position;
            box.transform.localScale = size;
            box.GetComponent<Renderer>().sharedMaterial = material;
            UnityEngine.Object.DestroyImmediate(box.GetComponent<Collider>());
            return 1;
        }

        private static Bounds Measure(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }
    }
}
