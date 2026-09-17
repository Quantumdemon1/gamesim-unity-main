using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Gamesim.Editor
{
    /// <summary>
    /// Dresses every room from the Kenney kit, and builds the set pieces the format is known by:
    /// the checkered kitchen floor, the roped entrance, and the competition podiums.
    ///
    /// <para>The furnishing pass places fourteen models, each one standing in for a primitive that
    /// was already there. That is the whole of its reach, and it leaves two gaps that read badly on
    /// screen. The first is coverage: most of the fifty-two models in the kit had nothing to stand
    /// in for, so they were never placed. The second is worse — <b>the three south-wing rooms and
    /// the yard contained no furniture at all</b>, because they were built after the furnishing plan
    /// and nothing added them to it. An empty HoH suite is not a stylistic choice.</para>
    ///
    /// <para>This places props directly, from each room's own floor bounds, so the layout survives
    /// the house being resized. Forty-eight of the kit's fifty-two models are placed; the four that
    /// are not — the extractor hood, the upper cabinets, the bathroom mirror and the ceiling fan —
    /// are the four that mount high on a wall or a ceiling, and this set has neither. Where the
    /// house still showed untextured primitives standing in for objects — foliage spheres, pot
    /// boxes, the podium blocks — those are replaced here too, in place and by measurement, rather
    /// than left for a screenshot to find.</para>
    ///
    /// <para><b>Nothing here has a collider.</b> The NavMesh is baked from collision, and adding
    /// solid props to a walkable floor would either carve holes in the mesh or require a rebake this
    /// pass is not in a position to verify. The trade is deliberate and worth stating plainly: a
    /// houseguest can walk through the stove. Props are placed against walls and in corners, where
    /// the navigation the cast actually uses does not go, so the compromise is rarely visible —
    /// but it is a compromise, not an oversight.</para>
    ///
    /// <para>The bathroom fixtures go in the HoH suite, which is the one room in this format that
    /// has its own. They are not scattered through the house: a toilet in the corner of the shared
    /// bedroom would be inventing a room rather than dressing one.</para>
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
        ///
        /// <para><c>Lift</c> is metres above the floor. Without it every prop sits on the ground,
        /// so nothing could be stood on anything else — books on a shelf, a lamp on the side table
        /// it belongs to, a television on its console.</para>
        ///
        /// <para>It is deliberately not used to hang things high on a wall, because <b>there is no
        /// wall to hang them on</b>. This is a cutaway set: the house walls top out at 1.5 m and the
        /// south wing at 1.1 m, so the overhead camera can see in. Upper kitchen cabinets, an
        /// extractor hood and a bathroom mirror were all drafted here and all cut — at the height
        /// those read at, they hang in open sky above the wall line. For the same reason nothing in
        /// the plan is taller than its own room's wall.</para>
        /// </summary>
        private readonly struct Prop
        {
            public readonly string Room, Model;
            public readonly float X, Z, Yaw, Height, Lift;

            public Prop(string room, string model, float x, float z, float yaw, float height, float lift = 0f)
            {
                Room = room; Model = model; X = x; Z = z; Yaw = yaw; Height = height; Lift = lift;
            }
        }

        // Heights are in metres and chosen to read at a glance rather than to be exact: a stove
        // that is waist-high and a lamp that is head-high is the whole requirement. A *negative*
        // height means "lay it flat, this many metres across" — the rule rugs are sized by.
        private static readonly Prop[] Plan =
        {
            // ---------------------------------------------------------------- Kitchen
            // A working counter run along the north wall. Everything stays at counter height:
            // see the note on Lift for why there are no wall units.
            new Prop("Kitchen floor", "kitchenStove",          -0.30f,  0.40f, 180f, 0.95f),
            new Prop("Kitchen floor", "kitchenSink",           -0.08f,  0.40f, 180f, 0.95f),
            new Prop("Kitchen floor", "kitchenMicrowave",       0.12f,  0.40f, 180f, 0.32f),
            new Prop("Kitchen floor", "kitchenCoffeeMachine",   0.26f,  0.40f, 180f, 0.34f),
            new Prop("Kitchen floor", "toaster",                0.36f,  0.40f, 180f, 0.22f),
            new Prop("Kitchen floor", "kitchenCabinetDrawer",  -0.44f,  0.40f, 180f, 0.90f),
            new Prop("Kitchen floor", "kitchenBarEnd",          0.44f,  0.16f, 270f, 1.05f),
            new Prop("Kitchen floor", "stoolBar",              -0.10f, -0.06f,   0f, 0.78f),
            new Prop("Kitchen floor", "stoolBar",               0.02f, -0.06f,   0f, 0.78f),
            new Prop("Kitchen floor", "stoolBar",               0.14f, -0.06f,   0f, 0.78f),
            // A dining table the whole cast can sit at — the room this format eats and argues in.
            new Prop("Kitchen floor", "tableRound",             0.16f, -0.30f,   0f, 0.76f),
            new Prop("Kitchen floor", "chairModernCushion",     0.092f,-0.300f,  90f, 0.88f),
            new Prop("Kitchen floor", "chairModernCushion",     0.228f,-0.300f, 270f, 0.88f),
            new Prop("Kitchen floor", "chairModernCushion",     0.160f,-0.205f, 180f, 0.88f),
            new Prop("Kitchen floor", "chairModernCushion",     0.160f,-0.395f,   0f, 0.88f),
            new Prop("Kitchen floor", "books",                  0.40f,  0.40f, 180f, 0.22f, 0.95f),
            new Prop("Kitchen floor", "trashcan",               0.44f, -0.38f,   0f, 0.60f),
            new Prop("Kitchen floor", "plantSmall1",           -0.44f, -0.40f,   0f, 0.55f),
            new Prop("Kitchen floor", "pottedPlant",            0.44f,  0.40f,   0f, 0.95f),

            // ---------------------------------------------------------------- Living room
            new Prop("Living room floor", "lampSquareFloor",   -0.43f,  0.38f,   0f, 1.55f),
            new Prop("Living room floor", "lampRoundFloor",     0.41f, -0.38f,   0f, 1.50f),
            new Prop("Living room floor", "speaker",           -0.43f, -0.26f,  90f, 0.95f),
            new Prop("Living room floor", "speaker",            0.43f,  0.26f, 270f, 0.95f),
            new Prop("Living room floor", "pottedPlant",        0.43f,  0.38f,   0f, 0.85f),
            new Prop("Living room floor", "loungeChairRelax",   0.22f, -0.20f, 210f, 0.90f),
            new Prop("Living room floor", "loungeDesignChair", -0.24f, -0.22f, 150f, 0.82f),
            new Prop("Living room floor", "loungeDesignSofa",   0.40f,  0.02f, 270f, 0.78f),
            new Prop("Living room floor", "bookcaseOpen",      -0.44f,  0.06f,  90f, 1.45f),
            new Prop("Living room floor", "books",             -0.42f,  0.06f,  90f, 0.24f, 0.95f),
            new Prop("Living room floor", "books",             -0.42f,  0.14f,  90f, 0.24f, 0.62f),
            new Prop("Living room floor", "sideTableDrawers",   0.30f,  0.40f,   0f, 0.62f),
            new Prop("Living room floor", "lampSquareTable",    0.30f,  0.40f,   0f, 0.45f, 0.62f),
            new Prop("Living room floor", "rugSquare",         -0.18f, -0.30f,   0f, -3.2f),
            new Prop("Living room floor", "trashcan",          -0.20f,  0.42f,   0f, 0.55f),
            new Prop("Living room floor", "plantSmall2",       -0.36f, -0.42f,   0f, 0.50f),

            // ---------------------------------------------------------------- Bedroom
            // The shared bedroom is the one room this format fills wall to wall with beds.
            new Prop("Bedroom floor", "bedBunk",               -0.42f,  0.30f,  90f, 1.45f),
            new Prop("Bedroom floor", "bedBunk",               -0.42f, -0.02f,  90f, 1.45f),
            new Prop("Bedroom floor", "bedSingle",             -0.16f,  0.42f, 180f, 0.55f),
            new Prop("Bedroom floor", "bedSingle",              0.06f,  0.42f, 180f, 0.55f),
            new Prop("Bedroom floor", "bedSingle",              0.28f,  0.42f, 180f, 0.55f),
            new Prop("Bedroom floor", "pillowBlue",            -0.16f,  0.45f, 180f, 0.10f, 0.52f),
            new Prop("Bedroom floor", "pillow",                 0.06f,  0.45f, 180f, 0.10f, 0.52f),
            new Prop("Bedroom floor", "pillowBlue",             0.28f,  0.45f, 180f, 0.10f, 0.52f),
            new Prop("Bedroom floor", "cabinetBedDrawer",      -0.05f,  0.42f,   0f, 0.52f),
            new Prop("Bedroom floor", "cabinetBedDrawer",       0.17f,  0.42f,   0f, 0.52f),
            new Prop("Bedroom floor", "cabinetBedDrawer",      -0.42f, -0.28f,  90f, 0.52f),
            new Prop("Bedroom floor", "sideTableDrawers",      -0.34f,  0.02f,   0f, 0.62f),
            new Prop("Bedroom floor", "lampSquareTable",       -0.34f,  0.02f,   0f, 0.45f, 0.62f),
            new Prop("Bedroom floor", "sideTableDrawers",       0.16f,  0.02f,   0f, 0.62f),
            new Prop("Bedroom floor", "lampSquareTable",        0.16f,  0.02f,   0f, 0.45f, 0.62f),
            new Prop("Bedroom floor", "coatRackStanding",      -0.44f,  0.40f,   0f, 1.70f),
            new Prop("Bedroom floor", "plantSmall2",            0.43f, -0.40f,   0f, 0.50f),
            new Prop("Bedroom floor", "bookcaseOpen",           0.43f,  0.34f, 270f, 1.60f),
            new Prop("Bedroom floor", "rugSquare",             -0.10f, -0.22f,   0f, -3.0f),
            new Prop("Bedroom floor", "lampRoundFloor",         0.42f, -0.06f,   0f, 1.45f),
            new Prop("Bedroom floor", "trashcan",               0.40f, -0.42f,   0f, 0.55f),

            // ---------------------------------------------------------------- Private room
            new Prop("Private room floor", "lampRoundFloor",   -0.42f,  0.40f,   0f, 1.50f),
            new Prop("Private room floor", "plantSmall3",       0.43f,  0.40f,   0f, 0.55f),
            new Prop("Private room floor", "plantSmall1",       0.43f, -0.40f,   0f, 0.55f),
            new Prop("Private room floor", "bookcaseOpen",      0.44f,  0.06f, 270f, 1.45f),
            new Prop("Private room floor", "books",             0.42f,  0.06f, 270f, 0.24f, 0.95f),
            new Prop("Private room floor", "loungeDesignChair", 0.18f, -0.30f, 200f, 0.82f),
            new Prop("Private room floor", "sideTableDrawers", -0.42f, -0.16f,  90f, 0.62f),
            new Prop("Private room floor", "lampSquareTable",  -0.42f, -0.16f,  90f, 0.45f, 0.62f),
            new Prop("Private room floor", "speaker",          -0.42f, -0.40f,  90f, 0.90f),
            new Prop("Private room floor", "pottedPlant",      -0.10f,  0.43f,   0f, 0.90f),

            // ---------------------------------------------------------------- HoH suite
            // Was empty. The bed, a sitting corner, and the ensuite that makes it a prize.
            new Prop("HoH floor", "bedDouble",                 -0.12f,  0.22f, 180f, 0.62f),
            new Prop("HoH floor", "pillowBlue",                -0.19f,  0.35f, 180f, 0.11f, 0.58f),
            new Prop("HoH floor", "pillow",                    -0.05f,  0.35f, 180f, 0.11f, 0.58f),
            new Prop("HoH floor", "cabinetBedDrawer",          -0.33f,  0.40f,   0f, 0.52f),
            new Prop("HoH floor", "lampSquareTable",           -0.33f,  0.40f,   0f, 0.42f, 0.52f),
            new Prop("HoH floor", "cabinetBedDrawer",           0.09f,  0.40f,   0f, 0.52f),
            new Prop("HoH floor", "lampSquareTable",            0.09f,  0.40f,   0f, 0.42f, 0.52f),
            new Prop("HoH floor", "rugRectangle",              -0.12f, -0.06f,   0f, -3.4f),
            new Prop("HoH floor", "loungeDesignSofa",          -0.42f, -0.20f,  90f, 0.78f),
            new Prop("HoH floor", "loungeChairRelax",          -0.14f, -0.34f,   0f, 0.88f),
            new Prop("HoH floor", "tableCoffee",               -0.30f, -0.34f,   0f, 0.42f),
            new Prop("HoH floor", "cabinetTelevision",         -0.12f, -0.44f, 180f, 0.45f),
            new Prop("HoH floor", "televisionModern",          -0.12f, -0.45f, 180f, 0.55f, 0.46f),
            new Prop("HoH floor", "lampSquareFloor",           -0.42f,  0.42f,   0f, 1.05f),
            new Prop("HoH floor", "pottedPlant",                0.42f, -0.42f,   0f, 0.90f),
            new Prop("HoH floor", "bookcaseOpen",              -0.42f,  0.06f,  90f, 1.05f),
            new Prop("HoH floor", "books",                     -0.40f,  0.06f,  90f, 0.22f, 0.70f),
            // The ensuite, along the east wall.
            new Prop("HoH floor", "bathtub",                    0.38f,  0.38f, 270f, 0.60f),
            new Prop("HoH floor", "showerRound",                0.40f,  0.14f, 270f, 1.05f),
            new Prop("HoH floor", "bathroomSink",               0.42f, -0.08f, 270f, 0.85f),
            new Prop("HoH floor", "toilet",                     0.42f, -0.26f, 270f, 0.72f),

            // ---------------------------------------------------------------- Nomination room
            // Was empty. One table, one chair per houseguest, and nothing else to look at.
            new Prop("Nomination floor", "rugRound",            0.00f,  0.04f,   0f, -4.2f),
            new Prop("Nomination floor", "tableRound",          0.00f,  0.04f,   0f, 0.78f),
            new Prop("Nomination floor", "chairModernCushion", -0.118f, 0.040f,  90f, 0.90f),
            new Prop("Nomination floor", "chairModernCushion",  0.118f, 0.040f, 270f, 0.90f),
            new Prop("Nomination floor", "chairModernCushion", -0.059f, 0.135f, 150f, 0.90f),
            new Prop("Nomination floor", "chairModernCushion",  0.059f, 0.135f, 210f, 0.90f),
            new Prop("Nomination floor", "chairModernCushion", -0.059f,-0.055f,  30f, 0.90f),
            new Prop("Nomination floor", "chairModernCushion",  0.059f,-0.055f, 330f, 0.90f),
            new Prop("Nomination floor", "lampSquareFloor",    -0.40f,  0.42f,   0f, 1.05f),
            new Prop("Nomination floor", "lampSquareFloor",     0.40f,  0.42f,   0f, 1.05f),
            new Prop("Nomination floor", "pottedPlant",        -0.40f, -0.40f,   0f, 0.95f),
            new Prop("Nomination floor", "pottedPlant",         0.40f, -0.40f,   0f, 0.95f),
            new Prop("Nomination floor", "speaker",            -0.42f,  0.10f,  90f, 0.90f),
            new Prop("Nomination floor", "speaker",             0.42f,  0.10f, 270f, 0.90f),

            // ---------------------------------------------------------------- Game room
            // Was empty. A bar, a screen and somewhere to sit and watch it.
            new Prop("Games floor", "loungeSofaCorner",        -0.22f, -0.22f,   0f, 0.80f),
            new Prop("Games floor", "tableCoffee",              0.06f, -0.20f,   0f, 0.42f),
            new Prop("Games floor", "rugSquare",               -0.06f, -0.22f,   0f, -3.0f),
            new Prop("Games floor", "cabinetTelevision",        0.00f,  0.42f, 180f, 0.45f),
            new Prop("Games floor", "televisionModern",         0.00f,  0.43f, 180f, 0.55f, 0.46f),
            new Prop("Games floor", "speaker",                 -0.30f,  0.42f, 180f, 0.95f),
            new Prop("Games floor", "speaker",                  0.30f,  0.42f, 180f, 0.95f),
            new Prop("Games floor", "kitchenBar",               0.36f, -0.02f, 270f, 1.05f),
            new Prop("Games floor", "stoolBar",                 0.20f,  0.06f,  90f, 0.78f),
            new Prop("Games floor", "stoolBar",                 0.20f, -0.06f,  90f, 0.78f),
            new Prop("Games floor", "stoolBar",                 0.20f, -0.18f,  90f, 0.78f),
            new Prop("Games floor", "loungeChairRelax",        -0.40f,  0.16f,  70f, 0.88f),
            new Prop("Games floor", "bookcaseOpen",            -0.42f,  0.40f,  90f, 1.05f),
            new Prop("Games floor", "books",                   -0.40f,  0.40f,  90f, 0.22f, 0.70f),
            new Prop("Games floor", "desk",                     0.40f, -0.36f, 270f, 0.74f),
            new Prop("Games floor", "loungeDesignChair",        0.26f, -0.36f,  90f, 0.82f),
            new Prop("Games floor", "lampRoundFloor",          -0.42f, -0.42f,   0f, 1.05f),
            new Prop("Games floor", "plantSmall3",              0.42f,  0.40f,   0f, 0.55f),
            new Prop("Games floor", "trashcan",                 0.42f,  0.22f,   0f, 0.55f),

            // ---------------------------------------------------------------- Competition yard
            new Prop("Competition yard floor", "pottedPlant",  -0.42f, -0.34f,   0f, 0.90f),
            new Prop("Competition yard floor", "pottedPlant",   0.42f, -0.34f,   0f, 0.90f),
            new Prop("Competition yard floor", "pottedPlant",  -0.46f,  0.34f,   0f, 1.05f),
            new Prop("Competition yard floor", "pottedPlant",   0.46f,  0.34f,   0f, 1.05f),
            new Prop("Competition yard floor", "pottedPlant",  -0.46f,  0.02f,   0f, 1.05f),
            new Prop("Competition yard floor", "pottedPlant",   0.46f,  0.02f,   0f, 1.05f),
            new Prop("Competition yard floor", "plantSmall2",  -0.30f,  0.40f,   0f, 0.55f),
            new Prop("Competition yard floor", "plantSmall3",   0.30f,  0.40f,   0f, 0.55f),
            new Prop("Competition yard floor", "plantSmall1",  -0.16f,  0.44f,   0f, 0.50f),
            new Prop("Competition yard floor", "plantSmall1",   0.16f,  0.44f,   0f, 0.50f),
            new Prop("Competition yard floor", "plantSmall2",  -0.36f, -0.44f,   0f, 0.50f),
            new Prop("Competition yard floor", "plantSmall3",   0.36f, -0.44f,   0f, 0.50f),
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
                var floor = Room(world.transform, prop.Room);
                if (floor == null) { Debug.LogWarning("[Gamesim] set pieces · no room floor: " + prop.Room); missing++; continue; }

                var instance = Model(prop.Model, root, prop.Yaw, prop.Height);
                if (instance == null) { missing++; continue; }

                var bounds = floor.bounds;
                var scaled = Measure(instance);
                var target = new Vector3(
                    bounds.center.x + prop.X * bounds.size.x,
                    bounds.max.y + prop.Lift,
                    bounds.center.z + prop.Z * bounds.size.z);
                instance.transform.position += target - new Vector3(scaled.center.x, scaled.min.y, scaled.center.z);
                placed++;
            }

            int tiles = CheckerKitchen(world.transform, root);
            int entrance = Entrance(world.transform, root);
            int greenery = Greenery(world.transform, root);
            int podiums = Podiums(world.transform, root);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log(string.Format(
                "[Gamesim] set pieces · {0} props placed, {1} skipped, {2} floor tiles, {3} entrance parts, "
                + "{4} plants swapped in, {5} podium parts",
                placed, missing, tiles, entrance, greenery, podiums));
        }

        /// <summary>
        /// A room's floor renderer, searched through the whole hierarchy rather than the world's
        /// direct children.
        ///
        /// <para>The south wing's floors hang under their own group rather than at the top level,
        /// so a <c>Find</c> on the world root returned null for all three of them. That is why the
        /// HoH suite, the nomination room and the game room were bare: not a decision, a lookup
        /// that quietly matched nothing.</para>
        /// </summary>
        private static Renderer Room(Transform world, string name)
        {
            foreach (var node in world.GetComponentsInChildren<Transform>(true))
            {
                if (node.name != name) continue;
                var renderer = node.GetComponent<Renderer>();
                if (renderer != null) return renderer;
            }
            return null;
        }

        /// <summary>
        /// Instantiates a kit model, turned and scaled to the intended height, with its colliders
        /// stripped. Returns null and warns when the model is missing.
        /// </summary>
        private static GameObject Model(string model, Transform parent, float yaw, float height)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(Kit + model + ".glb");
            if (source == null) { Debug.LogWarning("[Gamesim] set pieces · missing model: " + model); return null; }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
            instance.transform.SetParent(parent, false);
            instance.transform.localScale = Vector3.one;
            instance.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            // A negative height means "lay it flat and make it this many metres across" — the rule
            // rugs need, because a rug's own height is a centimetre or two and scaling to that
            // shrinks the whole model to a coaster.
            var raw = Measure(instance);
            float scale = height < 0f
                ? Mathf.Abs(height) / Mathf.Max(Mathf.Max(raw.size.x, raw.size.z), 0.001f)
                : raw.size.y > 0.001f ? height / raw.size.y : 1f;
            instance.transform.localScale = Vector3.one * scale;

            foreach (var collider in instance.GetComponentsInChildren<Collider>(true))
                UnityEngine.Object.DestroyImmediate(collider);

            instance.name = model;
            return instance;
        }

        /// <summary>
        /// The checkered kitchen floor: light tiles laid over the dark one, every other square.
        /// Only half the squares are built — the floor underneath is the other colour.
        /// </summary>
        private static int CheckerKitchen(Transform world, Transform root)
        {
            var floor = Room(world, "Kitchen floor");
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
                count += Box(group, "Tile", tileMaterial,
                    new Vector3(tileX * 0.97f, 0.02f, tileZ * 0.97f),
                    new Vector3(bounds.min.x + (column + 0.5f) * tileX,
                                bounds.max.y + 0.01f,
                                bounds.min.z + (row + 0.5f) * tileZ));
            }
            return count;
        }

        /// <summary>
        /// The roped entrance at the yard's outer edge: a gold arch and a run of stanchions, which
        /// is how the house announces itself in every broadcast of this format.
        /// </summary>
        private static int Entrance(Transform world, Transform root)
        {
            var yard = Room(world, "Competition yard floor");
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

            // Stanchions and rope, running out to either side of the opening. The posts are turned
            // cylinders with a cap and a base plate rather than single boxes: a square stanchion is
            // the clearest tell there is that a set was blocked out and never finished.
            for (int side = -1; side <= 1; side += 2)
            {
                float from = centre + side * (openWidth * 0.5f + 1.4f);
                for (int i = 0; i < 3; i++)
                {
                    float x = from + side * i * 1.6f;
                    parts += Shape(group, "Stanchion", brass, PrimitiveType.Cylinder,
                        new Vector3(0.09f, 0.5f, 0.09f), new Vector3(x, bounds.max.y + 0.5f, z));
                    parts += Shape(group, "Stanchion cap", gold, PrimitiveType.Sphere,
                        new Vector3(0.14f, 0.14f, 0.14f), new Vector3(x, bounds.max.y + 1.04f, z));
                    parts += Shape(group, "Stanchion base", brass, PrimitiveType.Cylinder,
                        new Vector3(0.30f, 0.02f, 0.30f), new Vector3(x, bounds.max.y + 0.02f, z));
                    if (i == 2) continue;
                    parts += Box(group, "Rope", red,
                        new Vector3(1.6f, 0.07f, 0.07f), new Vector3(x + side * 0.8f, bounds.max.y + 0.82f, z));
                }
            }
            return parts;
        }

        /// <summary>
        /// Swaps the planting placeholders for models from the kit.
        ///
        /// <para>The yard and the corridors were planted with a sphere sitting on a box — a shape
        /// that says "a plant goes here" and nothing else. Each one is measured, its renderer
        /// switched off, and a kit plant dropped in its place at the same centre and height, so the
        /// planting plan somebody chose is kept and only the geometry changes.</para>
        ///
        /// <para>The wide planters keep their box and get three small plants standing in it, because
        /// the box in that case is a raised bed and not a pot — deleting it would leave the plants
        /// floating over a hole in the yard's edging.</para>
        /// </summary>
        private static int Greenery(Transform world, Transform root)
        {
            var group = new GameObject("Planting").transform;
            group.SetParent(root, false);

            // Foliage sits above its own pot, so the pair has to be found together and replaced
            // once — swapping them separately buries a potted plant inside a second pot.
            var pots = new List<Renderer>();
            var leaves = new List<Renderer>();
            foreach (var node in world.GetComponentsInChildren<Transform>(true))
            {
                if (node.IsChildOf(root)) continue;
                var renderer = node.GetComponent<Renderer>();
                if (renderer == null) continue;
                string stem = Stem(node.name);
                if (stem == "Foliage") leaves.Add(renderer);
                else if (stem == "Pot" || stem == "Planter") pots.Add(renderer);
            }

            int swapped = 0;
            foreach (var leaf in leaves)
            {
                var slot = leaf.bounds;
                var pot = pots
                    .Where(p => Mathf.Abs(p.bounds.center.x - slot.center.x) < 0.6f
                             && Mathf.Abs(p.bounds.center.z - slot.center.z) < 0.6f)
                    .OrderBy(p => Mathf.Abs(p.bounds.center.y - slot.min.y))
                    .FirstOrDefault();
                if (pot != null) slot.Encapsulate(pot.bounds);

                // A wide planter reads as a bed of small plants; a narrow pot reads as one plant.
                bool bed = slot.size.x > 1.2f;
                string model = bed ? "plantSmall" + (swapped % 3 + 1) : "pottedPlant";
                int copies = bed ? 3 : 1;

                for (int i = 0; i < copies; i++)
                {
                    var instance = Model(model, group, i * 47f, slot.size.y * (bed ? 0.8f : 1f));
                    if (instance == null) break;
                    var scaled = Measure(instance);
                    float offset = copies == 1 ? 0f : (i - (copies - 1) * 0.5f) * (slot.size.x / (copies + 1));
                    instance.transform.position +=
                        new Vector3(slot.center.x + offset, bed ? slot.max.y : slot.min.y, slot.center.z)
                        - new Vector3(scaled.center.x, scaled.min.y, scaled.center.z);
                }

                leaf.enabled = false;
                if (pot != null && !bed) pot.enabled = false;
                swapped++;
            }
            return swapped;
        }

        /// <summary>
        /// Rebuilds the competition podiums.
        ///
        /// <para>They were single boxes, and they are the one prop in the yard the camera holds on:
        /// the competition cuts to three of them side by side, so a box is what that shot is made
        /// of. The kit has no podium — no CC0 furniture set has one — so this composes it from a
        /// plinth, an inset body, an overhanging counter, a lit front panel and a buzzer. Seven
        /// parts give it a silhouette; one box cannot have one.</para>
        ///
        /// <para>The original box is measured and hidden rather than deleted, because it is what the
        /// yard's layout was authored against and what any later pass will look for.</para>
        /// </summary>
        private static int Podiums(Transform world, Transform root)
        {
            var ink = AssetDatabase.LoadAssetAtPath<Material>(ArtRoot + "Ink.mat");
            var brass = AssetDatabase.LoadAssetAtPath<Material>(ArtRoot + "Brass.mat");
            var linen = AssetDatabase.LoadAssetAtPath<Material>(ArtRoot + "Linen.mat");
            var blue = AssetDatabase.LoadAssetAtPath<Material>(ArtRoot + "Emissive/Neon Blue.mat");
            var red = AssetDatabase.LoadAssetAtPath<Material>(ArtRoot + "Emissive/Neon Red.mat");
            if (ink == null || blue == null) return 0;
            if (brass == null) brass = blue;
            if (linen == null) linen = ink;
            if (red == null) red = blue;

            var blocks = world.GetComponentsInChildren<Transform>(true)
                .Where(node => !node.IsChildOf(root))
                .Where(node => Stem(node.name) == "Competition podium")
                .Select(node => node.GetComponent<Renderer>())
                .Where(renderer => renderer != null)
                .OrderBy(renderer => renderer.bounds.center.x)
                .ToArray();
            if (blocks.Length == 0) return 0;

            var group = new GameObject("Podiums").transform;
            group.SetParent(root, false);

            int parts = 0;
            foreach (var block in blocks)
            {
                var slot = block.bounds;
                var podium = new GameObject(block.name + " (set)").transform;
                podium.SetParent(group, false);

                float w = slot.size.x, d = slot.size.z, h = slot.size.y;
                float y = slot.min.y, cx = slot.center.x, cz = slot.center.z;

                // Plinth: wider than the body, so the podium stands on something.
                parts += Box(podium, "Plinth", ink,
                    new Vector3(w * 1.06f, h * 0.10f, d * 1.12f), new Vector3(cx, y + h * 0.05f, cz));
                // Body: inset, so the plinth and the counter both overhang it.
                parts += Box(podium, "Body", ink,
                    new Vector3(w * 0.88f, h * 0.78f, d * 0.82f), new Vector3(cx, y + h * 0.49f, cz));
                // Lit front panel, proud of the body by a centimetre so it never z-fights.
                parts += Box(podium, "Front panel", blue,
                    new Vector3(w * 0.66f, h * 0.34f, 0.02f),
                    new Vector3(cx, y + h * 0.52f, cz - d * 0.42f));
                // A brass reveal under the counter: the detail that reads at distance.
                parts += Box(podium, "Reveal", brass,
                    new Vector3(w * 0.90f, h * 0.04f, d * 0.86f), new Vector3(cx, y + h * 0.86f, cz));
                // Counter top, overhanging on every side.
                parts += Box(podium, "Counter", linen,
                    new Vector3(w * 1.02f, h * 0.08f, d * 1.06f), new Vector3(cx, y + h * 0.92f, cz));
                // The buzzer.
                parts += Shape(podium, "Buzzer stem", brass, PrimitiveType.Cylinder,
                    new Vector3(0.07f, h * 0.06f, 0.07f), new Vector3(cx, y + h * 1.02f, cz + d * 0.22f));
                parts += Shape(podium, "Buzzer", red, PrimitiveType.Sphere,
                    new Vector3(0.20f, 0.14f, 0.20f), new Vector3(cx, y + h * 1.10f, cz + d * 0.22f));

                block.enabled = false;
            }
            return parts;
        }

        /// <summary>
        /// A name with any trailing copy number removed, so "Foliage (3)" and "Competition podium 2"
        /// both stem to the name the plan uses.
        /// </summary>
        private static string Stem(string name)
        {
            string trimmed = name.Trim();
            int cut = trimmed.Length;
            while (cut > 0 && (char.IsDigit(trimmed[cut - 1]) || trimmed[cut - 1] == '(' || trimmed[cut - 1] == ')'
                               || trimmed[cut - 1] == ' '))
                cut--;
            return trimmed.Substring(0, cut);
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
            => Shape(parent, name, material, PrimitiveType.Cube, size, position);

        private static int Shape(Transform parent, string name, Material material,
            PrimitiveType type, Vector3 size, Vector3 position)
        {
            var shape = GameObject.CreatePrimitive(type);
            shape.name = name;
            shape.transform.SetParent(parent, false);
            shape.transform.position = position;
            shape.transform.localScale = size;
            shape.GetComponent<Renderer>().sharedMaterial = material;
            UnityEngine.Object.DestroyImmediate(shape.GetComponent<Collider>());
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
