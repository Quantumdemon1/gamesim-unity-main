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

        /// <summary>Every model id the plan names, for the catalogue audit.</summary>
        public static IEnumerable<string> PlanModels => Plan.Select(prop => prop.Model).Concat(SteppedModels);
        /// <summary>Models placed by a step rather than a plan row: the competition set.</summary>
        private static readonly string[] SteppedModels =
        {
            "bb_set_podium", "bb_set_compring",
            "bb_set_comp_lane", "bb_set_comp_gate", "bb_set_comp_stack", "bb_set_comp_crate",
            "bb_set_comp_backdrop",
            "bb_set_sign_hoh", "bb_set_sign_pillars", "bb_set_sign_samehouse",
        };

        // Heights are in metres and chosen to read at a glance rather than to be exact: a stove
        // that is waist-high and a lamp that is head-high is the whole requirement. A *negative*
        // height means "lay it flat, this many metres across" — the rule rugs are sized by.
        private static readonly Prop[] Plan =
        {
            // ---------------------------------------------------------------- Kitchen
            // A working counter run along the north wall. Everything stays at counter height:
            // see the note on Lift for why there are no wall units.
            // Tier 3 begins here: the kitchen run replaces the kit's stove, sink, cabinet and bar
            // end with one authored counter from the fridge to the end panel along the north wall
            // (x 1.0 to 7.4, its back a couple of centimetres off the wall's face). The kit's small
            // appliances stay, lifted onto its counter over the doors, the drawers and the doors.
            new Prop("Kitchen floor", "bb_set_kitchenrun",     -0.20f,  0.455f,  0f, 0f),
            new Prop("Kitchen floor", "kitchenMicrowave",      -0.329f, 0.455f, 180f, 0.32f, 0.92f),
            new Prop("Kitchen floor", "kitchenCoffeeMachine",  -0.189f, 0.455f, 180f, 0.34f, 0.92f),
            new Prop("Kitchen floor", "toaster",               -0.05f,  0.455f, 180f, 0.22f, 0.92f),
            // Tier 4 clutter: on the counter, and on the long table (its top is 0.76 m).
            new Prop("Kitchen floor", "bb_set_mug",            -0.29f,  0.455f,  30f, 0f, 0.92f),
            new Prop("Kitchen floor", "bb_set_bottle",         -0.02f,  0.455f,   0f, 0f, 0.92f),
            new Prop("Kitchen floor", "bb_set_mug",            -0.04f, -0.285f, 200f, 0f, 0.76f),
            new Prop("Kitchen floor", "bb_set_mug",             0.11f, -0.315f, 340f, 0f, 0.76f),
            new Prop("Kitchen floor", "bb_set_bottle",          0.03f, -0.300f,   0f, 0f, 0.76f),
            new Prop("Kitchen floor", "bb_set_fruitbowl",       0.19f, -0.300f,   0f, 0f, 0.76f),
            new Prop("Kitchen floor", "stoolBar",              -0.10f, -0.06f,   0f, 0.78f),
            new Prop("Kitchen floor", "stoolBar",               0.02f, -0.06f,   0f, 0.78f),
            new Prop("Kitchen floor", "stoolBar",               0.14f, -0.06f,   0f, 0.78f),
            // A dining table the whole cast can sit at — the room this format eats and argues in.
            // The long table where the house argues: sixteen seats, seven a side and one at each
            // end, at authored size (a zero height). It replaces the round table for four.
            //
            // The chairs are the scanned ones (V4), scaled to 0.95 m so that a chair 0.67 m wide as
            // scanned fits the 0.65 m pitch the seven a side are placed at. The *table* is still the
            // authored plank: Poly Haven's dining table is 2.26 m long, half what sixteen seats
            // need, and no scaling stretches a table lengthways. Its Poly Haven twin is exported
            // and waiting for a room that wants a table for four.
            new Prop("Kitchen floor", "bb_set_diningtable",     0.060f, -0.300f,   0f, 0f),
            new Prop("Kitchen floor", "bb_set_ph_diningchair",  -0.079f, -0.222f, 180f, 0.95f),
            new Prop("Kitchen floor", "bb_set_ph_diningchair",  -0.033f, -0.222f, 180f, 0.95f),
            new Prop("Kitchen floor", "bb_set_ph_diningchair",  0.014f, -0.222f, 180f, 0.95f),
            new Prop("Kitchen floor", "bb_set_ph_diningchair",  0.060f, -0.222f, 180f, 0.95f),
            new Prop("Kitchen floor", "bb_set_ph_diningchair",  0.106f, -0.222f, 180f, 0.95f),
            new Prop("Kitchen floor", "bb_set_ph_diningchair",  0.153f, -0.222f, 180f, 0.95f),
            new Prop("Kitchen floor", "bb_set_ph_diningchair",  0.199f, -0.222f, 180f, 0.95f),
            new Prop("Kitchen floor", "bb_set_ph_diningchair",  -0.079f, -0.378f,   0f, 0.95f),
            new Prop("Kitchen floor", "bb_set_ph_diningchair",  -0.033f, -0.378f,   0f, 0.95f),
            new Prop("Kitchen floor", "bb_set_ph_diningchair",  0.014f, -0.378f,   0f, 0.95f),
            new Prop("Kitchen floor", "bb_set_ph_diningchair",  0.060f, -0.378f,   0f, 0.95f),
            new Prop("Kitchen floor", "bb_set_ph_diningchair",  0.106f, -0.378f,   0f, 0.95f),
            new Prop("Kitchen floor", "bb_set_ph_diningchair",  0.153f, -0.378f,   0f, 0.95f),
            new Prop("Kitchen floor", "bb_set_ph_diningchair",  0.199f, -0.378f,   0f, 0.95f),
            new Prop("Kitchen floor", "bb_set_ph_diningchair", -0.133f, -0.300f,  90f, 0.95f),
            new Prop("Kitchen floor", "bb_set_ph_diningchair",  0.253f, -0.300f, 270f, 0.95f),
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
            new Prop("Living room floor", "bb_set_cushion",     0.40f,  0.07f, 270f, 0f, 0.54f),
            new Prop("Living room floor", "bb_set_cushion",     0.40f, -0.03f, 300f, 0f, 0.54f),
            // Against the north wall's west segment, not the west wall: the memory wall hangs there
            // now, two rows of eight along the living room's half of it, and a bookcase stood in
            // front of its middle.
            new Prop("Living room floor", "bookcaseOpen",      -0.30f,  0.44f,   0f, 1.45f),
            new Prop("Living room floor", "sideTableDrawers",   0.30f,  0.40f,   0f, 0.62f),
            new Prop("Living room floor", "bb_set_photoframe",  0.30f,  0.40f, 200f, 0f, 0.62f),
            new Prop("Living room floor", "lampSquareTable",    0.30f,  0.40f,   0f, 0.45f, 0.62f),

            // The ceremony screen (mockup-10): the lit board the house turns to face, on its own
            // low stage against the nomination room's far side. Here rather than in the living
            // room because this is where the ceremony camera goes - the key ceremony frames the
            // nomination room, and a screen the beat never looks at is furniture. Free-standing
            // because this house's walls are 1.1 m cutaways and there is nothing to hang a 1.5 m
            // board on, which is the same reason the yard's backdrop stands on the deck. Yaw 180
            // turns its face into the room; authored, it looks at -z.
            new Prop("Nomination floor", "bb_set_ceremonyscreen", 0f, -0.39f, 180f, 0f),
            new Prop("Living room floor", "rugSquare",         -0.18f, -0.30f,   0f, -3.2f),
            new Prop("Living room floor", "trashcan",          -0.20f,  0.42f,   0f, 0.55f),
            new Prop("Living room floor", "plantSmall2",       -0.36f, -0.42f,   0f, 0.50f),

            // ---------------------------------------------------------------- Bedroom
            // The shared bedroom is the one room this format fills wall to wall with beds.
            new Prop("Bedroom floor", "bedBunk",               -0.42f,  0.30f,  90f, 1.45f),
            new Prop("Bedroom floor", "bedBunk",               -0.42f, -0.02f,  90f, 1.45f),
            new Prop("Bedroom floor", "bedSingle",             -0.16f,  0.42f, 180f, 0f),
            new Prop("Bedroom floor", "bedSingle",              0.06f,  0.42f, 180f, 0f),
            new Prop("Bedroom floor", "bedSingle",              0.28f,  0.42f, 180f, 0f),
            new Prop("Bedroom floor", "pillowBlue",            -0.16f,  0.45f, 180f, 0.10f, 0.52f),
            new Prop("Bedroom floor", "pillowBlue",             0.28f,  0.45f, 180f, 0.10f, 0.52f),
            new Prop("Bedroom floor", "cabinetBedDrawer",      -0.05f,  0.42f,   0f, 0.52f),
            new Prop("Bedroom floor", "cabinetBedDrawer",       0.17f,  0.42f,   0f, 0.52f),
            new Prop("Bedroom floor", "cabinetBedDrawer",      -0.42f, -0.28f,  90f, 0.52f),
            new Prop("Bedroom floor", "sideTableDrawers",      -0.34f,  0.02f,   0f, 0.62f),
            new Prop("Bedroom floor", "bb_set_photoframe",     -0.34f,  0.02f, 160f, 0f, 0.62f),
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
            new Prop("Private room floor", "loungeDesignChair", 0.18f, -0.30f, 200f, 0.82f),
            new Prop("Private room floor", "sideTableDrawers", -0.42f, -0.16f,  90f, 0.62f),
            new Prop("Private room floor", "lampSquareTable",  -0.42f, -0.16f,  90f, 0.45f, 0.62f),
            new Prop("Private room floor", "speaker",          -0.42f, -0.40f,  90f, 0.90f),
            new Prop("Private room floor", "pottedPlant",      -0.10f,  0.43f,   0f, 0.90f),

            // ---------------------------------------------------------------- HoH suite
            // Was empty. The bed, a sitting corner, and the ensuite that makes it a prize.
            // The reward room's bed, authored with its own pillows and a headboard kept under the
            // south wing's 1.1 m wall; at its own size (a zero height).
            new Prop("HoH floor", "bb_set_hohbed",             -0.12f,  0.22f, 180f, 0f),
            // The door with the key, dressing the divider's gap into the nomination room (the piece
            // is placed by its bounds, and the open leaf swings into the suite, so the fraction puts
            // the jambs on the divider at x -4.617 rather than the piece's middle there), and the
            // week's basket on the coffee table.
            new Prop("HoH floor", "bb_set_hohdoor",             0.4587f, 0.00f,  0f, 0f),
            new Prop("HoH floor", "bb_set_hohbasket",          -0.30f, -0.34f,  30f, 0f, 0.42f),
            new Prop("HoH floor", "bb_set_mug",                -0.245f, -0.34f, 120f, 0f, 0.42f),
            new Prop("HoH floor", "bb_set_towel",               0.38f,  0.42f, 270f, 0f, 0.60f),
            new Prop("HoH floor", "bb_set_magazines",          -0.33f, -0.30f,  20f, 0f, 0.42f),
            new Prop("HoH floor", "bb_set_candle",              0.34f,  0.33f,   0f, 0f, 0.60f),
            new Prop("HoH floor", "pillow",                    -0.05f,  0.35f, 180f, 0.11f, 0.58f),
            new Prop("HoH floor", "cabinetBedDrawer",          -0.33f,  0.40f,   0f, 0.52f),
            new Prop("HoH floor", "lampSquareTable",           -0.33f,  0.40f,   0f, 0.42f, 0.52f),
            new Prop("HoH floor", "cabinetBedDrawer",           0.09f,  0.40f,   0f, 0.52f),
            new Prop("HoH floor", "lampSquareTable",            0.09f,  0.40f,   0f, 0.42f, 0.52f),
            new Prop("HoH floor", "rugRectangle",              -0.12f, -0.06f,   0f, -3.4f),
            new Prop("HoH floor", "loungeDesignSofa",          -0.42f, -0.20f,  90f, 0.78f),
            new Prop("HoH floor", "loungeChairRelax",          -0.14f, -0.34f,   0f, 0.88f),
            new Prop("HoH floor", "tableCoffee",               -0.30f, -0.34f,   0f, 0.42f),
            new Prop("HoH floor", "cabinetTelevision",         -0.12f, -0.44f, 180f, 0f),
            new Prop("HoH floor", "lampSquareFloor",           -0.42f,  0.42f,   0f, 1.05f),
            new Prop("HoH floor", "pottedPlant",                0.42f, -0.42f,   0f, 0.90f),
            new Prop("HoH floor", "bookcaseOpen",              -0.42f,  0.06f,  90f, 1.05f),
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
            new Prop("Games floor", "cabinetTelevision",        0.00f,  0.42f, 180f, 0f),
            new Prop("Games floor", "speaker",                 -0.30f,  0.42f, 180f, 0.95f),
            new Prop("Games floor", "speaker",                  0.30f,  0.42f, 180f, 0.95f),
            new Prop("Games floor", "kitchenBar",               0.36f, -0.02f, 270f, 1.05f),
            new Prop("Games floor", "stoolBar",                 0.20f,  0.06f,  90f, 0.78f),
            new Prop("Games floor", "stoolBar",                 0.20f, -0.06f,  90f, 0.78f),
            new Prop("Games floor", "stoolBar",                 0.20f, -0.18f,  90f, 0.78f),
            new Prop("Games floor", "loungeChairRelax",        -0.40f,  0.16f,  70f, 0.88f),
            new Prop("Games floor", "bookcaseOpen",            -0.42f,  0.40f,  90f, 1.05f),
            new Prop("Games floor", "desk",                     0.40f, -0.36f, 270f, 0.74f),
            new Prop("Games floor", "bb_set_laptop",            0.40f, -0.33f, 270f, 0f, 0.74f),
            new Prop("Games floor", "bb_set_bookstack",         0.40f, -0.40f, 280f, 0f, 0.74f),
            new Prop("Games floor", "bb_set_tray",              0.36f, -0.02f, 270f, 0f, 1.05f),
            new Prop("Games floor", "bb_set_remote",            0.10f, -0.22f, 300f, 0f, 0.42f),
            new Prop("Games floor", "bb_set_cable",             0.46f, -0.46f,   0f, 0f),
            new Prop("Games floor", "loungeDesignChair",        0.26f, -0.36f,  90f, 0.82f),
            new Prop("Games floor", "lampRoundFloor",          -0.42f, -0.42f,   0f, 1.05f),
            // The have-not end: two steel cots along the south wall, cold and hard. Dressing only.
            new Prop("Games floor", "bb_set_havenot_cot",      -0.28f, -0.44f,  90f, 0f),
            new Prop("Games floor", "bb_set_havenot_cot",      -0.05f, -0.44f,  90f, 0f),
            new Prop("Games floor", "plantSmall3",              0.42f,  0.40f,   0f, 0.55f),
            new Prop("Games floor", "trashcan",                 0.42f,  0.22f,   0f, 0.55f),

            // ---------------------------------------------------------------- Private room
            // The diary chair stands just behind the diary marker at (7, 3), facing the camera, so
            // the player who walks to the marker stands in front of it rather than inside it.
            new Prop("Private room floor", "bb_set_diarychair",   0.00f, -0.10f, 180f, 0f),

            // ---------------------------------------------------------------- Competition yard
            // The east end is the pool end: the authored pool, hot tub and loungers sit where two
            // of the planters used to, at their own size (a zero height). The yard floor is 28 x
            // 10 m centred at z = 15, so 0.34 of its width is 9.5 m east of the circle.
            // The competition set - three gold rings on the yard's centre and the three podiums at
            // x -6, 0 and 6 on z 17 - is placed by its own steps below, and its podium blocks carry
            // the colliders the NavMesh was baked from, so the water goes round it: the pool turned
            // long-ways along the east wall, clear of podium 3 (it used to stand on its corner), the
            // loungers along the pool's south end facing it, and the hot tub in the north-west corner.
            new Prop("Competition yard floor", "bb_set_pool",     0.40f,  0.07f,  90f, 0f),
            new Prop("Competition yard floor", "bb_set_hottub",  -0.357f, 0.30f,   0f, 0f),
            new Prop("Competition yard floor", "bb_set_lounger",  0.336f, -0.40f,  0f, 0f),
            new Prop("Competition yard floor", "bb_set_lounger",  0.386f, -0.40f,  0f, 0f),
            new Prop("Competition yard floor", "bb_set_lounger",  0.436f, -0.40f,  0f, 0f),
            new Prop("Competition yard floor", "pottedPlant",  -0.42f, -0.34f,   0f, 0.90f),
            new Prop("Competition yard floor", "pottedPlant",  -0.46f,  0.34f,   0f, 1.05f),
            new Prop("Competition yard floor", "pottedPlant",   0.24f,  0.44f,   0f, 1.05f),
            new Prop("Competition yard floor", "pottedPlant",  -0.46f,  0.02f,   0f, 1.05f),
            new Prop("Competition yard floor", "plantSmall2",  -0.30f,  0.40f,   0f, 0.55f),
            new Prop("Competition yard floor", "plantSmall3",   0.30f,  0.40f,   0f, 0.55f),
            new Prop("Competition yard floor", "plantSmall1",  -0.16f,  0.44f,   0f, 0.50f),
            new Prop("Competition yard floor", "plantSmall1",   0.16f,  0.44f,   0f, 0.50f),
            new Prop("Competition yard floor", "plantSmall2",  -0.36f, -0.44f,   0f, 0.50f),
            new Prop("Competition yard floor", "plantSmall3",   0.46f, -0.44f,   0f, 0.50f),
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
            int rings = Rings(world.transform, root);
            int course = Course(world.transform, root);
            int shell = Shell(world.transform, root);

            // Placement and collision have to travel together. Set dressing was re-placed once
            // without the lighting pass that follows it and the house lost its lightmaps for three
            // days without anybody noticing; a separate "and now run the other menu item" step is
            // exactly how that happens. Fitting here means a re-placed house is a solid house.
            HouseFurnitureCollision.FitCollision();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log(string.Format(
                "[Gamesim] set pieces · {0} props placed, {1} skipped, {2} floor tiles, {3} entrance parts, "
                + "{4} plants swapped in, {5} podium parts, {6} ring segments dressed by the authored circle, "
                + "{7} primitive walls dressed by the authored shell, {8} competition course pieces",
                placed, missing, tiles, entrance, greenery, podiums, rings, shell, course));
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
            var source = HouseCatalogue.Resolve(model, out var tier);
            if (source == null) { Debug.LogWarning("[Gamesim] set pieces · missing model: " + model); return null; }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
            instance.transform.SetParent(parent, false);
            instance.transform.localScale = Vector3.one;
            instance.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            // A negative height means "lay it flat and make it this many metres across" — the rule
            // rugs need, because a rug's own height is a centimetre or two and scaling to that
            // shrinks the whole model to a coaster.
            // A zero height means "as authored": a piece built in metres by its own script is
            // already the size it should be, and scaling it to a guessed height would undo that.
            var raw = Measure(instance);
            float scale = height == 0f ? 1f
                : height < 0f
                ? Mathf.Abs(height) / Mathf.Max(Mathf.Max(raw.size.x, raw.size.z), 0.001f)
                : raw.size.y > 0.001f ? height / raw.size.y : 1f;
            instance.transform.localScale = Vector3.one * scale;

            // Kit models never carry collision. An authored piece keeps only the shape its export
            // promised, the _col child - which is what stops a click landing in the pool.
            foreach (var collider in instance.GetComponentsInChildren<Collider>(true))
                if (tier != HouseCatalogue.Tier.Authored || !collider.name.EndsWith(AuthoredAssetImporter.ColliderSuffix, StringComparison.Ordinal))
                    UnityEngine.Object.DestroyImmediate(collider);

            instance.name = model;
            return instance;
        }

        /// <summary>
        /// The authored shell (Part 4, Tier 2): caps, skirting and door jambs laid exactly over the
        /// wall colliders, from <c>ArtSource/shell/bb_shell.py</c>. The primitives stay where they
        /// are with their colliders - the NavMesh is baked from them - and only their renderers go,
        /// and only once the shell has resolved, so a missing export never leaves the house bare.
        /// Returns how many primitive walls the shell now stands in for.
        /// </summary>
        private static int Shell(Transform world, Transform root)
        {
            var source = HouseCatalogue.Resolve("bb_shell_house", out var tier);
            if (source == null || tier != HouseCatalogue.Tier.Authored)
            {
                Debug.LogWarning("[Gamesim] set pieces · no authored shell; the primitive walls stay visible.");
                return 0;
            }
            var previous = root.Find("bb_shell_house");
            if (previous != null) UnityEngine.Object.DestroyImmediate(previous.gameObject);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
            instance.transform.SetParent(root, false);
            instance.transform.SetPositionAndRotation(world.position, Quaternion.identity);
            instance.transform.localScale = Vector3.one;
            instance.name = "bb_shell_house";

            int dressed = 0;
            foreach (var box in world.GetComponentsInChildren<BoxCollider>(true))
            {
                if (box.transform.IsChildOf(root) || !box.enabled || !box.gameObject.activeInHierarchy) continue;
                var size = Vector3.Scale(box.size, box.transform.lossyScale);
                bool wallLike = size.y >= 1f && Mathf.Min(size.x, size.z) <= 0.3f && Mathf.Max(size.x, size.z) >= 1f
                    && box.name != "Television";
                if (!wallLike) continue;
                var renderer = box.GetComponent<Renderer>();
                if (renderer == null || !renderer.enabled) continue;
                renderer.enabled = false;
                dressed++;
            }
            return dressed;
        }

        /// <summary>
        /// The competition circle (Part 4, Tier 1): one authored mesh of three gold rings, from
        /// <c>ArtSource/setpieces/bb_set_compring.py</c>, in place of the 144 primitive segments.
        /// The segments stay in the scene with their renderers off - the yard was authored against
        /// them and they carry no colliders, so nothing else changes. Returns how many segments the
        /// authored circle now stands in for.
        /// </summary>
        private static int Rings(Transform world, Transform root)
        {
            var yard = Room(world, "Competition yard floor");
            if (yard == null) return 0;
            var segments = world.GetComponentsInChildren<Transform>(true)
                .Where(node => !node.IsChildOf(root) && node.parent != null
                    && node.parent.name.StartsWith("Ring ", StringComparison.Ordinal))
                .Select(node => node.GetComponent<Renderer>())
                .Where(renderer => renderer != null)
                .ToArray();
            var rings = Model("bb_set_compring", root, 0f, 0f);
            if (rings == null) return 0;
            // Where the primitives put the circle - their common centre - on the yard's floor.
            var centre = segments.Length > 0
                ? new Vector3(segments.Average(r => r.bounds.center.x), yard.bounds.max.y, segments.Average(r => r.bounds.center.z))
                : new Vector3(yard.bounds.center.x, yard.bounds.max.y, yard.bounds.center.z);
            rings.transform.position = centre;
            int dressed = 0;
            foreach (var segment in segments)
            {
                if (!segment.enabled) continue;
                segment.enabled = false;
                dressed++;
            }
            return dressed;
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
        /// <summary>The yard's own floor, which every piece of the course is measured from.</summary>
        private const string CompetitionFloor = "Competition yard floor";

        /// <summary>
        /// The competition course (VISUAL-TARGET.md V4/V5, mockup-05): three lit lanes across the
        /// yard, a gate at the head of each, and the stacking prop with its crate of spares in
        /// front of every podium.
        ///
        /// <para>The mockup's competition is a course, not a circle. Three lanes run away from the
        /// camera in red, blue and green; a neon frame stands over each; and in front of every
        /// houseguest is the thing they are actually doing - a peg with discs to stack. The last of
        /// those is the one that matters most: a competition the viewer watches without knowing the
        /// task is a crowd scene.</para>
        ///
        /// <para>The yard already had a competition set, the three gold rings on its floor. Both are
        /// floor graphics in the same place and they read as clutter together, so dressing the
        /// course turns the ring's renderer off rather than stacking the two. The ring itself is
        /// untouched, and deleting this step's group brings it back.</para>
        ///
        /// <para>Measured rather than guessed: the floor is read for its extent and the podiums for
        /// their spacing, so a yard that is resized moves the course with it. Nothing here carries a
        /// collider - the NavMesh was baked without these pieces, and furniture in the walk path is
        /// what cost seventeen PlayMode tests the last time it was tried.</para>
        /// </summary>
        private static int Course(Transform world, Transform root)
        {
            var floor = world.GetComponentsInChildren<Renderer>(true)
                .FirstOrDefault(renderer => !renderer.transform.IsChildOf(root)
                    && renderer.name == CompetitionFloor);
            if (floor == null) return 0;

            var blocks = world.GetComponentsInChildren<Transform>(true)
                .Where(node => !node.IsChildOf(root))
                .Where(node => Stem(node.name) == "Competition podium")
                .Select(node => node.GetComponent<Renderer>())
                .Where(renderer => renderer != null)
                .OrderBy(renderer => renderer.bounds.center.x)
                .ToArray();
            if (blocks.Length == 0) return 0;

            // Left to right, the mockup's own order.
            var colours = new[] { "Neon Red", "Neon Blue", "Neon Green" };

            var group = new GameObject("Competition course").transform;
            group.SetParent(root, false);

            var deck = floor.bounds;
            float top = deck.max.y;
            int placed = 0;

            // The wall first, so the gates read against it. It keeps its own cool tubes rather
            // than a lane's colour: it is one piece behind all three, and painting it red would
            // make the left lane's backdrop the whole yard's.
            placed += Stand("bb_set_comp_backdrop", group,
                new Vector3(deck.center.x, top, deck.max.z - 0.1f), null);

            // The lettering, hung on the wall's lit face. Signs are measured from the base of their
            // own type, so the heights below are baselines, not centres. They keep the backdrop's
            // cool tubes rather than a lane's colour, for the same reason the wall does.
            float face = deck.max.z - 0.28f;
            placed += Stand("bb_set_sign_pillars", group, new Vector3(deck.center.x - 7.5f, top + 1.20f, face), null);
            placed += Stand("bb_set_sign_hoh", group, new Vector3(deck.center.x, top + 1.85f, face), null);
            placed += Stand("bb_set_sign_samehouse", group, new Vector3(deck.center.x + 7.5f, top + 1.50f, face), null);

            for (int lane = 0; lane < blocks.Length; lane++)
            {
                var neon = AssetDatabase.LoadAssetAtPath<Material>(
                    ArtRoot + "Emissive/" + colours[Mathf.Min(lane, colours.Length - 1)] + ".mat");
                float x = blocks[lane].bounds.center.x;
                float podiumZ = blocks[lane].bounds.center.z;

                // The lane, centred on the deck so it stops half a metre short at both ends.
                placed += Stand("bb_set_comp_lane", group, new Vector3(x, top, deck.center.z), neon);
                // The gate at the head of the lane, short of the back edge so it is not in the wall.
                placed += Stand("bb_set_comp_gate", group, new Vector3(x, top, deck.max.z - 0.7f), neon);
                // The task, in front of the podium where the camera sees it before the houseguest.
                placed += Stand("bb_set_comp_stack", group, new Vector3(x, top, podiumZ - 1.5f), neon);
                // The spares, beside it and inside the lane's own edge.
                placed += Stand("bb_set_comp_crate", group, new Vector3(x + 1.7f, top, podiumZ - 1.1f), neon);
            }

            // One competition graphic on the floor at a time.
            foreach (var ring in root.GetComponentsInChildren<Renderer>(true))
                if (ring.name.StartsWith("bb_set_compring", StringComparison.Ordinal)) ring.enabled = false;

            return placed;
        }

        /// <summary>
        /// Places one course piece at a world position and gives its neon the lane's colour.
        ///
        /// <para>By the piece's own origin, not by its bounding box. Every authoring script states
        /// where it put the origin and they are not all the centre - a lane's is at its near end,
        /// so that a lane can be laid from a start line - and placing by a measured centre would
        /// silently push it half its own length up the yard.</para>
        ///
        /// <para>The pieces are authored white and named <c>bb_mat_neon_*</c> for the glow, so one
        /// mesh serves all three lanes instead of three near-identical exports. Only the neon slots
        /// are repainted; the dark parts stay dark.</para>
        /// </summary>
        private static int Stand(string model, Transform parent, Vector3 at, Material neon)
        {
            var piece = Model(model, parent, 0f, 0f);
            if (piece == null) return 0;

            piece.transform.position = at;

            if (neon != null)
                foreach (var renderer in piece.GetComponentsInChildren<Renderer>(true))
                {
                    var slots = renderer.sharedMaterials;
                    bool repainted = false;
                    for (int i = 0; i < slots.Length; i++)
                        if (slots[i] != null
                            && slots[i].name.StartsWith("bb_mat_neon", StringComparison.Ordinal))
                        { slots[i] = neon; repainted = true; }
                    if (repainted) renderer.sharedMaterials = slots;
                }
            return 1;
        }

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

            // Authored first (Part 4, Tier 1): one mesh per podium at the block's own footprint,
            // from ArtSource/setpieces/bb_set_podium.py. The primitive composition below is the
            // fallback, so a missing export never leaves the yard without its podiums.
            var authored = HouseCatalogue.Resolve("bb_set_podium", out var podiumTier);
            if (authored != null && podiumTier == HouseCatalogue.Tier.Authored)
            {
                int placedPodiums = 0;
                foreach (var block in blocks)
                {
                    var slot = block.bounds;
                    var piece = Model("bb_set_podium", group, 0f, 0f);
                    if (piece == null) continue;
                    piece.name = block.name + " (set)";
                    piece.transform.position = new Vector3(slot.center.x, slot.min.y, slot.center.z);
                    block.enabled = false;
                    placedPodiums++;
                }
                return placedPodiums;
            }

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
