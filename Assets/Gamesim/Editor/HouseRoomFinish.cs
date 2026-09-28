using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Gamesim.Editor
{
    /// <summary>
    /// The room finish pass (<c>Assets/Plans/ROOM-FINISH-PLAN.md</c>): Room Finish Pack 6's flat
    /// finish graphics laid over the rooms that exist, as a root of their own under the house.
    ///
    /// <para>What it does. Each room gets a group under <see cref="RootName"/>: wall treatments as
    /// bands on the inner faces of the cutaway walls, rugs as floor quads, signs and glyphs as glow
    /// quads, clutter pieces from the catalogue, and re-skins of the pieces already placed. It reads
    /// the floors, the wall colliders and the placed set pieces; it never rebuilds <c>Set Pieces</c>,
    /// which is what keeps the lightmaps (a rebuilt root loses them) and the NavMesh (baked from
    /// collision; nothing here carries any).</para>
    ///
    /// <para>What it does not do. Nothing under the root casts a shadow, contributes to GI or has a
    /// collider: it is probe-lit dressing on a lightmapped set, so the bake stays the bake and the
    /// anchors stay where they are. A piece it hides (a lounge chair in what is now a confessional)
    /// is disabled in place, never destroyed, so the layout record survives; a piece it re-skins
    /// keeps its lightmap. Those two edits live on the pieces rather than under the root, so a
    /// re-run cannot undo them - <c>git checkout</c> of the scene can.</para>
    ///
    /// <para>Idempotent: the root is destroyed and rebuilt, materials are re-applied by name under
    /// <see cref="MaterialFolder"/>, and a second run saves the same scene. Runs from the menu or in
    /// batchmode with <c>-executeMethod Gamesim.Editor.HouseRoomFinish.Apply</c>.</para>
    /// </summary>
    public static class HouseRoomFinish
    {
        public const string RootName = "Room Finish";
        public const string EpisodeScene = "Assets/Gamesim/Scenes/EpisodeHouse.unity";
        private const string WorldRoot = "House Architecture";
        /// <summary>Where the pass keeps the materials it makes: outside the pack root, which holds catalogued PNGs and nothing else.</summary>
        public const string MaterialFolder = "Assets/Gamesim/Art/RoomFinish/Materials";
        public const string Pack6 = UiPackImporter.WorldRoot + "Pack6_RoomFinish/";
        public const string Pack5 = UiPackImporter.WorldRoot + "Pack5_HouseBroadcast/";
        /// <summary>A floor graphic's height above the floor: clear of z-fighting, under a shoe.</summary>
        public const float FloorLift = 0.012f;
        /// <summary>A wall graphic's stand-off from the wall's face.</summary>
        public const float WallLift = 0.01f;
        /// <summary>A sign's stand-off from the band it hangs on.</summary>
        public const float SignLift = 0.006f;
        /// <summary>How far a band stops short of a wall segment's end, so the shell's jambs stay visible.</summary>
        public const float BandInset = 0.15f;
        /// <summary>The room groups, in the pack README's order of priority.</summary>
        public static readonly string[] RoomGroups =
        {
            "Diary room", "Kitchen", "Living room", "HoH suite", "Nomination room", "Game room", "Bedroom", "Competition yard",
        };

        // The diary's signage: lit, but signage - well under the neon's brightness.
        private static readonly Color SignTint = new Color(0.80f, 0.74f, 1.00f) * 1.15f;
        private static readonly Color EyeTint = new Color(0.62f, 0.44f, 1.00f) * 1.3f;

        [MenuItem("Gamesim/U07/Finish the rooms (pack 6)")]
        public static void Apply()
        {
            var scene = EditorSceneManager.OpenScene(EpisodeScene, OpenSceneMode.Single);
            int count = Apply(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[Gamesim] room finish · " + count + " pieces dressed, hidden or re-skinned under '" + RootName + "'");
        }

        /// <summary>Rebuilds the root in <paramref name="scene"/> and returns how many edits it made.</summary>
        public static int Apply(Scene scene)
        {
            var world = scene.GetRootGameObjects().FirstOrDefault(go => go.name == WorldRoot);
            if (world == null) throw new InvalidOperationException("No '" + WorldRoot + "' root in " + scene.path);

            var existing = world.transform.Find(RootName);
            if (existing != null) UnityEngine.Object.DestroyImmediate(existing.gameObject);
            var root = new GameObject(RootName).transform;
            root.SetParent(world.transform, false);

            var house = new House(world.transform, root);
            int count = 0;
            foreach (var name in RoomGroups)
            {
                var group = new GameObject(name).transform;
                group.SetParent(root, false);
                switch (name)
                {
                    case "Diary room": count += DiaryRoom(house, group); break;
                    case "Kitchen": count += Kitchen(house, group); break;
                    case "Living room": count += LivingRoom(house, group); break;
                    case "HoH suite": count += HohSuite(house, group); break;
                }
            }
            return count;
        }

        // ---------------------------------------------------------------- the rooms

        /// <summary>
        /// The diary room (plan §5.1): the lounge leaves, the walls become padded charcoal and dark
        /// slats, the radial carpet centres on the chair, the confessional sign hangs on the far
        /// wall and the eye on the east one, two plants flank the studio, and the chair wears navy
        /// velvet on a marble dais.
        /// </summary>
        private static int DiaryRoom(House house, Transform group)
        {
            var room = house.Room("Private room floor");
            if (room == null) { Debug.LogWarning("[Gamesim] room finish · no private room floor"); return 0; }
            int count = 0;

            // A second seat contradicts the room: the game fantasy is isolation. The prototype's two
            // confessional chairs and its table (HouseFurnishing) go with the set-piece lounge chair.
            count += Hide(house, room, "loungeDesignChair");
            count += Hide(house, room, "Confessional chair A");
            count += Hide(house, room, "Confessional chair B");
            count += Hide(house, room, "Private table");
            count += Hide(house, room, "bookcaseOpen");
            count += HideRugs(house, room);

            const string padded = Pack6 + "WallTreatments/wall_charcoal_upholstered.png";
            const string slats = Pack6 + "DiaryRoom/diary_dark_wood_slats.png";
            var north = Band(house, room, group, "House / yard right", padded, 1.2f);
            var east = Band(house, room, group, "East wall", padded, 1.2f);
            var westSouth = Band(house, room, group, "Bedroom / private room south", slats, 1.0f);
            Band(house, room, group, "Bedroom / private room north", slats, 1.0f);
            Band(house, room, group, "Kitchen / private room left", slats, 1.0f);
            Band(house, room, group, "Kitchen / private room right", slats, 1.0f);
            count += 6;

            var chair = house.Pieces(room, "bb_set_diarychair").FirstOrDefault();
            var at = chair != null ? chair.position : new Vector3(room.Bounds.center.x, room.FloorTop, room.Bounds.center.z - 1f);
            if (Rug(group, "Diary rug", room, Pack6 + "DiaryRoom/diary_radial_carpet.png", 3.6f, new Vector2(at.x, at.z), 0f) != null) count++;

            if (north.HasValue && Sign(group, "Confessional sign", north.Value, Pack6 + "DiaryRoom/confessional_wall_sign.png",
                SignTint, 1.6f, at.x - north.Value.Centre.x, 0.95f) != null) count++;
            if (east.HasValue && Sign(group, "Diary eye", east.Value, Pack6 + "DiaryRoom/diary_eye_halo_mask.png",
                EyeTint, 1.2f, at.z - east.Value.Centre.z, 0.78f) != null) count++;
            if (westSouth.HasValue && Sign(group, "Private sign", westSouth.Value, Pack6 + "DiaryRoom/private_sign.png",
                SignTint, 1.0f, 0f, 0.9f) != null) count++;

            // Two anchors either side of where the studio's wall stands (4.6 m wide, 0.85 m behind the chair).
            float flank = 2.6f / room.Bounds.size.x, behind = (at.z + 0.6f - room.Bounds.center.z) / room.Bounds.size.z;
            float centreX = (at.x - room.Bounds.center.x) / room.Bounds.size.x;
            if (Piece(house, room, group, "bb_set_ph_plant", centreX - flank, behind, 0f, 0f) != null) count++;
            if (Piece(house, room, group, "bb_set_ph_plant", centreX + flank, behind, 40f, 0f) != null) count++;

            count += Skin(house, room, "bb_set_diarychair", "bb_mat_velvet_teal",
                Finish("bb_mat_p6_diary_chair_velvet", Pack6 + "Upholstery/velvet_navy.png", new Vector2(2f, 2f), Color.white, 0.35f, 0f, false));
            count += Skin(house, room, "bb_set_diarychair", "bb_mat_coping_stone",
                Finish("bb_mat_p6_diary_dais_marble", Pack6 + "NominationRoom/dark_marble_detail.png", Vector2.one, Color.white, 0.5f, 0f, false));
            return count;
        }

        /// <summary>
        /// The kitchen (plan §5.2): the counter run's stone top becomes marble, a marble backsplash
        /// runs along the wall behind it from the counter to the wall's top, the fridge, the
        /// dishwasher and the oven get their control panels, the counter gets its clusters - coffee
        /// beside the machine, the pantry beside the toaster, a towel at the end - the long table
        /// gets its rug, and the room its sign. The footprint does not change.
        /// </summary>
        private static int Kitchen(House house, Transform group)
        {
            var room = house.Room("Kitchen floor");
            if (room == null) { Debug.LogWarning("[Gamesim] room finish · no kitchen floor"); return 0; }
            int count = 0;
            const string marble = Pack6 + "Kitchen/backsplash_marble_soft.png";

            // The stone the run was authored with is a dark slab; the marble is the guide's counter.
            count += Skin(house, room, "bb_set_kitchenrun", "bb_mat_counter_stone",
                Finish("bb_mat_p6_kitchen_counter", marble, new Vector2(1.2f, 1.2f), new Color(0.95f, 0.93f, 0.90f), 0.45f, 0f, false));

            // The backsplash: the wall behind the run, from the counter's height to the wall's top,
            // on both segments of the north partition so the line runs the room's width.
            Band(house, room, group, "Kitchen / private room left", marble, 0.6f, 1.5f, 0.92f, 0.35f);
            Band(house, room, group, "Kitchen / private room right", marble, 0.6f, 1.5f, 0.92f, 0.35f);
            count += 2;

            var run = house.Pieces(room, "bb_set_kitchenrun").FirstOrDefault();
            if (run != null)
            {
                // The run's sections, left to right as its script lays them (bb_set_kitchenrun.py):
                // fridge 0.8, doors 1.2, sink 0.9, drawers 0.9, stove 0.9, doors 1.2, end 0.5.
                var rb = Measure(run.gameObject);
                float left = rb.min.x, front = rb.min.z, counter = room.FloorTop + 0.92f, mid = rb.center.z;
                var into = Vector3.back;   // the run faces -z, into the room
                const string panels = Pack6 + "Kitchen/";
                // Rhythm: fridge / dishwasher / sink / drawers / cooktop and oven / doors.
                if (Decal(group, "Fridge panel", panels + "appliance_panel_fridge.png", new Vector3(left + 0.40f, room.FloorTop + 0.30f, front - 0.012f), 0.60f, into) != null) count++;
                if (Decal(group, "Dishwasher panel", panels + "appliance_panel_coffee.png", new Vector3(left + 1.40f, room.FloorTop + 0.78f, front - 0.012f), 0.60f, into) != null) count++;
                if (Decal(group, "Oven panel", panels + "appliance_panel_oven.png", new Vector3(left + 4.25f, room.FloorTop + 0.72f, front - 0.012f), 0.60f, into) != null) count++;
                // Coffee, beside the machine over the drawers.
                if (Tin(group, "Coffee tin", panels + "coffee_label.png", 0.06f, 0.16f, new Vector3(left + 3.00f, counter, mid + 0.05f)) != null) count++;
                if (Tin(group, "Jar", panels + "jar_label.png", 0.055f, 0.14f, new Vector3(left + 3.70f, counter, mid + 0.08f)) != null) count++;
                // The pantry, beside the toaster.
                if (Carton(group, "Cereal", panels + "cereal_label.png", new Vector3(0.20f, 0.30f, 0.07f), new Vector3(left + 4.85f, counter, mid + 0.10f), 12f) != null) count++;
                if (Carton(group, "Snack bag", panels + "snack_label_red.png", new Vector3(0.16f, 0.22f, 0.05f), new Vector3(left + 5.08f, counter, mid + 0.06f), -8f) != null) count++;
                if (Carton(group, "Snack bag", panels + "snack_label_blue.png", new Vector3(0.16f, 0.22f, 0.05f), new Vector3(left + 5.50f, counter, mid + 0.02f), 20f) != null) count++;
                // A folded towel on the end panel.
                var towel = Finish("bb_mat_p6_kitchen_towel", panels + "dish_towel_neutral.png", Vector2.one, Color.white, 0.10f, 0f, true);
                if (towel != null) { Quad(group, "Dish towel", towel, new Vector3(left + 6.15f, counter + 0.004f, mid), new Vector2(0.30f, 0.36f), Vector3.up, Vector3.back); count++; }
            }

            // The long table's rug, centred under it.
            var table = house.Pieces(room, "bb_set_diningtable").FirstOrDefault();
            if (table != null)
            {
                var tb = Measure(table.gameObject);
                if (Rug(group, "Dining rug", room, Pack6 + "Rugs/rug_living_navy_cream.png", 5.0f, new Vector2(tb.center.x, tb.center.z), 0f) != null) count++;
            }

            // The room's sign, on the east wall where the overview reads it.
            var east = WallFace(house, room, "East wall");
            if (east.HasValue && Sign(group, "Kitchen sign", east.Value, Pack5 + "RoomSignage/kitchen_sign.png",
                new Color(0.90f, 0.95f, 1.00f) * 1.2f, 1.4f, room.Bounds.center.z - east.Value.Centre.z, 1.05f) != null) count++;
            return count;
        }

        /// <summary>
        /// The living room (plan §5.3): the north partition - the wall the eviction's screen stands
        /// against - becomes the feature wall in warm oak slats with a lit strip either side of the
        /// screen's place and the house mark beside it; the teal rug binds the sofa and the coffee
        /// table; the table gets its cluster; two framed prints hang on the south wall; a plant takes
        /// the empty corner. The seats do not move: they are anchors.
        /// </summary>
        private static int LivingRoom(House house, Transform group)
        {
            var room = house.Room("Living room floor");
            if (room == null) { Debug.LogWarning("[Gamesim] room finish · no living room floor"); return 0; }
            int count = 0;
            const string oak = Pack6 + "WallTreatments/wall_warm_oak_slats.png";
            var left = Band(house, room, group, "Living / bedroom left", oak, 1.0f);
            var right = Band(house, room, group, "Living / bedroom right", oak, 1.0f);
            count += 2;

            // The screen's place is the prototype television's: its box stands at the partition, and
            // the cut scene's clone stands where it stood. The strips flank it symmetrically, one on
            // each segment, across the doorway between them.
            var television = house.PiecesMatching(room, name => name == "Television").FirstOrDefault();
            float screenX = television != null ? television.position.x : room.Bounds.center.x + 2f;
            if (left.HasValue && Strip(group, "Feature strip", left.Value, screenX - 3.6f - left.Value.Centre.x, 0.75f, 1.35f) != null) count++;
            if (right.HasValue && Strip(group, "Feature strip", right.Value, screenX + 3.6f - right.Value.Centre.x, 0.75f, 1.35f) != null) count++;
            if (right.HasValue)
            {
                var f = right.Value;
                var centre = new Vector3(screenX + 2.6f, f.Bottom + 0.72f, f.Centre.z) + f.Normal * SignLift;
                if (Decal(group, "House mark", Pack5 + "EnvironmentWallGraphics/gamesim_house_geometry.png", centre, 0.7f, f.Normal) != null) count++;
            }

            // The conversation group: the sofa and the coffee table on one rug.
            var sofa = house.Pieces(room, "loungeDesignSofa").FirstOrDefault();
            var table = house.PiecesMatching(room, name => name == "Coffee table (model)" || name == "tableCoffee").FirstOrDefault();
            if (sofa != null)
            {
                var sb = Measure(sofa.gameObject);
                var centre = new Vector2(sb.center.x, sb.center.z);
                if (table != null)
                {
                    var tb = Measure(table.gameObject);
                    centre = (centre + new Vector2(tb.center.x, tb.center.z)) * 0.5f;
                    // Its cluster, on the table's top.
                    float top = tb.max.y - room.FloorTop;
                    float x = (tb.center.x - room.Bounds.center.x) / room.Bounds.size.x, z = (tb.center.z - room.Bounds.center.z) / room.Bounds.size.z;
                    float dx = 0.22f / room.Bounds.size.x, dz = 0.16f / room.Bounds.size.z;
                    if (Piece(house, room, group, "bb_set_bookstack", x - dx, z + dz * 0.5f, 15f, 0f, top) != null) count++;
                    if (Piece(house, room, group, "bb_set_candle", x + dx * 0.6f, z - dz, 0f, 0f, top) != null) count++;
                    if (Piece(house, room, group, "bb_set_tray", x + dx * 0.9f, z + dz, 100f, 0f, top) != null) count++;
                }
                // The neutral abstract rather than the teal: under the room's own light the teal read
                // as a black patch from above, and the rug is there to be seen from above.
                count += HideRugsUnder(house, room, centre, 4.4f);
                if (Rug(group, "Living rug", room, Pack6 + "Rugs/rug_living_neutral_abstract.png", 4.4f, centre, 0f) != null) count++;
            }

            // Two framed prints on the south wall, at a wall 1.1 m tall's own height.
            var south = WallFace(house, room, "South wing link left");
            if (south.HasValue)
            {
                var s = south.Value;
                if (Poster(group, "Framed print", s, Pack6 + "WallArt/wall_art_01.png", Pack6 + "PhotoFrames/frame_portrait_single.png", 0.6f, room.Bounds.center.x - 4.5f - s.Centre.x, 0.6f) != null) count++;
                if (Poster(group, "Framed print", s, Pack6 + "WallArt/wall_art_05.png", Pack6 + "PhotoFrames/frame_portrait_single.png", 0.6f, room.Bounds.center.x + 2.5f - s.Centre.x, 0.6f) != null) count++;
            }

            if (Piece(house, room, group, "bb_set_ph_plant", 0.44f, -0.44f, 30f, 0f) != null) count++;
            return count;
        }

        /// <summary>
        /// The HoH suite (plan §5.4): status. The bed wears cream linen and a channelled cream
        /// headboard with a throw across its foot; the wall behind it is the luxury geometric, the
        /// west wall brass inlay; the cream-and-gold rug lies under the lounge; the reward station
        /// stands on the television console against the south wall - the plaque and four gold
        /// photo frames on the wall, the envelope, the letter, the gift, the snacks and the drink
        /// on the console, the mini fridge beside it; the crown beside the bed and the HoH neon by
        /// the door. Gold, because the suite is a power state.
        /// </summary>
        private static int HohSuite(House house, Transform group)
        {
            var room = house.Room("HoH floor");
            if (room == null) { Debug.LogWarning("[Gamesim] room finish · no HoH floor"); return 0; }
            int count = 0;

            // The bed: the velvet slot is the duvet and the headboard together (bb_set_hohbed.py
            // joins them on one material), so both go cream; the linen slot - mattress, pillows,
            // turnback - takes the cream weave.
            count += Skin(house, room, "bb_set_hohbed", "bb_mat_velvet_teal",
                Finish("bb_mat_p6_hoh_duvet", Pack6 + "Bedding/duvet_hoh_cream.png", new Vector2(1.2f, 1.2f), Color.white, 0.15f, 0f, false));
            count += Skin(house, room, "bb_set_hohbed", "bb_mat_linen_white",
                Finish("bb_mat_p6_hoh_linen", Pack6 + "Upholstery/fabric_cream_weave.png", new Vector2(2f, 2f), Color.white, 0.12f, 0f, false));
            var bed = house.Pieces(room, "bb_set_hohbed").FirstOrDefault();
            if (bed != null)
            {
                var bb = Measure(bed.gameObject);
                var throwOver = Finish("bb_mat_p6_hoh_throw", Pack6 + "Bedding/throw_blanket_hoh.png", Vector2.one, Color.white, 0.12f, 0f, true);
                // The foot of the bed is its end away from the wall it stands against.
                bool headNorth = Mathf.Abs(bb.max.z - room.Bounds.max.z) < Mathf.Abs(bb.min.z - room.Bounds.min.z);
                float footZ = headNorth ? bb.min.z + 0.45f : bb.max.z - 0.45f;
                // On the duvet, whose top is 0.57 m up (platform 0.25, mattress 0.22, duvet 0.10).
                if (throwOver != null)
                { Quad(group, "Bed throw", throwOver, new Vector3(bb.center.x, room.FloorTop + 0.575f, footZ), new Vector2(bb.size.x * 0.9f, 0.7f), Vector3.up, Vector3.forward); count++; }
            }

            // The walls: the geometric behind the bed, brass inlay on the west.
            var north = Band(house, room, group, "South wing link left", Pack6 + "WallTreatments/hoh_luxury_geometric.png", 1.0f);
            Band(house, room, group, "South wing west wall", Pack6 + "WallTreatments/wall_brass_inlay.png", 1.0f);
            count += 2;
            if (north.HasValue && bed != null)
            {
                var bb = Measure(bed.gameObject);
                var f = north.Value;
                float x = bb.min.x - 0.9f > room.Bounds.min.x + 0.6f ? bb.min.x - 0.9f : bb.max.x + 0.9f;
                var centre = new Vector3(x, f.Bottom + 0.72f, f.Centre.z) + f.Normal * SignLift;
                if (Decal(group, "Crown", Pack5 + "EnvironmentWallGraphics/hoh_crown_wall.png", centre, 0.5f, f.Normal) != null) count++;
            }

            // The lounge's rug, under the sofa, the chair and the table together.
            var lounge = new[] { "loungeDesignSofa", "loungeChairRelax", "tableCoffee" }
                .Select(name => house.Pieces(room, name).FirstOrDefault()).Where(t => t != null).ToList();
            if (lounge.Count > 0)
            {
                var centre = Vector2.zero;
                foreach (var piece in lounge) { var b = Measure(piece.gameObject); centre += new Vector2(b.center.x, b.center.z); }
                centre /= lounge.Count;
                count += HideRugsUnder(house, room, centre, 3.6f);
                if (Rug(group, "HoH rug", room, Pack6 + "Rugs/rug_hoh_cream_gold.png", 3.6f, centre, 0f) != null) count++;
            }

            // The reward station: on the console against the south wall, and on the wall above it.
            var console = house.Pieces(room, "cabinetTelevision").FirstOrDefault();
            var south = WallFace(house, room, "South wing south wall");
            if (console != null && south.HasValue)
            {
                var cb = Measure(console.gameObject);
                var s = south.Value;
                const float consoleTop = 0.45f;   // bb_set_tvconsole.py: H 0.45, the screen standing on it
                float top = room.FloorTop + consoleTop;
                float along = cb.center.x - s.Centre.x;
                // The plaque, then the four photo frames under it, all above the console's top edge.
                if (Decal(group, "Reward plaque", Pack6 + "HOHRewards/reward_plaque.png", new Vector3(cb.center.x, s.Bottom + 0.88f, s.Centre.z) + s.Normal * SignLift, 0.6f, s.Normal) != null) count++;
                for (int i = 0; i < 4; i++)
                {
                    float x = cb.center.x + (i - 1.5f) * 0.30f;
                    var at = new Vector3(x, s.Bottom + 0.52f, s.Centre.z) + s.Normal * SignLift;
                    if (Decal(group, "HoH photo", Pack6 + "HOHRewards/photo_placeholder_" + (i + 1) + ".png", at, 0.2f, s.Normal) != null) count++;
                    Decal(group, "HoH photo frame", Pack6 + "PhotoFrames/frame_hoh_gold.png", at + s.Normal * 0.002f, 0.26f, s.Normal);
                }
                // On the console: the letter and its envelope flat, the gift, the snacks, the drink.
                var envelope = Finish("bb_mat_p6_decal_hoh_envelope", Pack6 + "HOHRewards/hoh_envelope.png", Vector2.one, Color.white, 0.2f, 0f, true);
                if (envelope != null) { Quad(group, "HoH envelope", envelope, new Vector3(cb.min.x + 0.22f, top + 0.004f, cb.center.z - 0.05f), new Vector2(0.24f, 0.15f), Vector3.up, Quaternion.Euler(0f, 12f, 0f) * Vector3.forward); count++; }
                var letter = Finish("bb_mat_p6_decal_hoh_letter_sheet", Pack6 + "HOHRewards/hoh_letter_sheet.png", Vector2.one, Color.white, 0.2f, 0f, true);
                if (letter != null) { Quad(group, "HoH letter", letter, new Vector3(cb.min.x + 0.40f, top + 0.006f, cb.center.z + 0.08f), new Vector2(0.21f, 0.16f), Vector3.up, Quaternion.Euler(0f, -6f, 0f) * Vector3.forward); count++; }
                if (Carton(group, "Gift box", Pack6 + "HOHRewards/gift_wrap_gold.png", new Vector3(0.22f, 0.16f, 0.16f), new Vector3(cb.max.x - 0.22f, top, cb.center.z), 15f) != null) count++;
                if (Carton(group, "Snack bag", Pack6 + "HOHRewards/snack_bag_gold.png", new Vector3(0.14f, 0.20f, 0.05f), new Vector3(cb.max.x - 0.48f, top, cb.center.z - 0.08f), -10f) != null) count++;
                if (Carton(group, "Snack bag", Pack6 + "HOHRewards/snack_bag_blue.png", new Vector3(0.14f, 0.20f, 0.05f), new Vector3(cb.max.x - 0.64f, top, cb.center.z + 0.04f), 25f) != null) count++;
                if (Tin(group, "HoH drink", Pack6 + "HOHRewards/drink_label_hoh.png", 0.035f, 0.22f, new Vector3(cb.max.x - 0.36f, top, cb.center.z + 0.12f)) != null) count++;
                // The mini fridge, on the floor beside the console, toward the room's middle.
                float fridgeX = cb.max.x + 0.40f < room.Bounds.max.x - 0.5f ? cb.max.x + 0.40f : cb.min.x - 0.40f;
                if (Carton(group, "Mini fridge", Pack6 + "GameRoom/mini_fridge_graphic.png", new Vector3(0.50f, 0.70f, 0.50f), new Vector3(fridgeX, room.FloorTop, cb.center.z), 0f) != null) count++;
            }

            // The HoH neon by the door, on the divider's south segment.
            var divider = WallFace(house, room, "South wing divider 1 south");
            if (divider.HasValue && Sign(group, "HoH neon", divider.Value, Pack5 + "NeonMasks/neon_hoh_suite_mask.png",
                new Color(1.00f, 0.82f, 0.40f) * 1.5f, 1.0f, 0f, 0.72f) != null) count++;
            return count;
        }

        // ---------------------------------------------------------------- the vocabulary

        /// <summary>A wall band's face, for hanging signs on: where it is, which way it looks, how far it runs.</summary>
        public readonly struct Face
        {
            public readonly Vector3 Centre, Normal, Along;
            public readonly float Length, Height, Bottom;
            public Face(Vector3 centre, Vector3 normal, Vector3 along, float length, float height, float bottom)
            { Centre = centre; Normal = normal; Along = along; Length = length; Height = height; Bottom = bottom; }
        }

        /// <summary>
        /// A treatment on the inner face of one wall segment, clipped to the room's floor and inset
        /// from the segment's ends, never taller than the wall it is on. A segment ends at a doorway
        /// or a corner, so covering segments rather than edges keeps every gap clear by construction.
        /// <paramref name="height"/> and <paramref name="bottom"/> are metres above the floor; zero
        /// height means the wall's own.
        /// </summary>
        private static Face? Band(House house, Room room, Transform group, string wallName, string texture, float metresPerTile,
            float height = 0f, float bottom = 0f, float smoothness = 0.25f)
        {
            var face = WallFace(house, room, wallName, height, bottom);
            if (!face.HasValue) return null;
            var f = face.Value;
            var material = Finish(MaterialName(texture, f.Length, f.Height), texture, new Vector2(f.Length / metresPerTile, f.Height / metresPerTile),
                Color.white, smoothness, 0f, false);
            if (material == null) return null;
            Quad(group, "Band · " + wallName, material, f.Centre, new Vector2(f.Length, f.Height), f.Normal, Vector3.up);
            return face;
        }

        /// <summary>
        /// The inner face of one wall segment as the room sees it - where a band would go, or a
        /// sign on the bare shell: its centre a stand-off from the collider's face, its run clipped
        /// to the floor and inset from the segment's ends, its height the wall's or the one asked for.
        /// </summary>
        private static Face? WallFace(House house, Room room, string wallName, float height = 0f, float bottom = 0f)
        {
            var wall = house.Wall(wallName);
            if (wall == null) { Debug.LogWarning("[Gamesim] room finish · no wall named '" + wallName + "'"); return null; }
            var wb = wall.bounds;
            var fb = room.Bounds;
            bool alongX = wb.size.x >= wb.size.z;
            Vector3 normal;
            float faceAt;
            if (alongX)
            {
                float sign = Mathf.Sign(fb.center.z - wb.center.z);
                normal = new Vector3(0f, 0f, sign);
                faceAt = wb.center.z + sign * wb.extents.z;
            }
            else
            {
                float sign = Mathf.Sign(fb.center.x - wb.center.x);
                normal = new Vector3(sign, 0f, 0f);
                faceAt = wb.center.x + sign * wb.extents.x;
            }
            float min = (alongX ? Mathf.Max(wb.min.x, fb.min.x) : Mathf.Max(wb.min.z, fb.min.z)) + BandInset;
            float max = (alongX ? Mathf.Min(wb.max.x, fb.max.x) : Mathf.Min(wb.max.z, fb.max.z)) - BandInset;
            float length = max - min;
            if (length < 0.4f) { Debug.LogWarning("[Gamesim] room finish · '" + wallName + "' has no run inside " + room.Name); return null; }
            float floor = room.FloorTop;
            float low = floor + Mathf.Max(0f, bottom);
            float top = Mathf.Min(height > 0f ? floor + height : room.WallHeight, wb.max.y) - 0.02f;
            float h = top - low;
            if (h < 0.05f) { Debug.LogWarning("[Gamesim] room finish · '" + wallName + "' has no height left above " + bottom + " m"); return null; }
            var centre = alongX
                ? new Vector3((min + max) * 0.5f, low + h * 0.5f, faceAt + normal.z * WallLift)
                : new Vector3(faceAt + normal.x * WallLift, low + h * 0.5f, (min + max) * 0.5f);
            return new Face(centre, normal, alongX ? Vector3.right : Vector3.forward, length, h, low);
        }

        /// <summary>A lit strip on a band: a plain glow quad <paramref name="width"/> wide and <paramref name="height"/> tall, standing on the band's foot.</summary>
        private static GameObject Strip(Transform group, string name, Face face, float along, float height, float tint)
        {
            var material = Glow("bb_mat_p6_glow_strip", null, new Color(0.62f, 0.70f, 1.00f) * tint);
            if (material == null) return null;
            float h = Mathf.Min(height, face.Height - 0.1f);
            var centre = new Vector3(face.Centre.x, face.Bottom + 0.05f + h * 0.5f, face.Centre.z) + face.Along * along + face.Normal * SignLift;
            return Quad(group, name, material, centre, new Vector2(0.06f, h), face.Normal, Vector3.up);
        }

        /// <summary>A framed print: the art quad on a band and its frame a hair in front, both lit and cut to their edges.</summary>
        private static GameObject Poster(Transform group, string name, Face face, string art, string frame, float width, float along, float centreHeight)
        {
            var image = AssetDatabase.LoadAssetAtPath<Texture2D>(art);
            if (image == null) { Debug.LogWarning("[Gamesim] room finish · missing texture " + art); return null; }
            float h = width * image.height / image.width;
            float y = face.Bottom + Mathf.Min(centreHeight, face.Height - h * 0.5f - 0.02f);
            var centre = new Vector3(face.Centre.x, y, face.Centre.z) + face.Along * along + face.Normal * SignLift;
            var print = Decal(group, name, art, centre, width, face.Normal);
            if (print == null) return null;
            Decal(group, name + " frame", frame, centre + face.Normal * 0.002f, width, face.Normal);
            return print;
        }

        /// <summary>Disables every rug in the room whose footprint the new rug's would cover, so rugs never stack.</summary>
        private static int HideRugsUnder(House house, Room room, Vector2 centre, float metres)
        {
            int count = 0;
            float half = metres * 0.5f;
            foreach (var piece in house.PiecesMatching(room, name => name.IndexOf("rug", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                var b = Measure(piece.gameObject);
                bool overlaps = b.max.x > centre.x - half && b.min.x < centre.x + half && b.max.z > centre.y - half && b.min.z < centre.y + half;
                if (!overlaps) continue;
                foreach (var renderer in piece.GetComponentsInChildren<Renderer>(true))
                    if (renderer.enabled) { renderer.enabled = false; count++; }
            }
            return count;
        }

        /// <summary>A flat graphic on a piece's face: a lit quad, clipped to its own edge, <paramref name="width"/> across at its image's aspect.</summary>
        private static GameObject Decal(Transform group, string name, string texture, Vector3 centre, float width, Vector3 normal)
        {
            var image = AssetDatabase.LoadAssetAtPath<Texture2D>(texture);
            if (image == null) { Debug.LogWarning("[Gamesim] room finish · missing texture " + texture); return null; }
            var material = Finish("bb_mat_p6_decal_" + Stem(texture), texture, Vector2.one, Color.white, 0.3f, 0f, true);
            if (material == null) return null;
            return Quad(group, name, material, centre, new Vector2(width, width * image.height / image.width), normal, Vector3.up);
        }

        /// <summary>A box wearing a label on every face - a cereal box, a snack bag, a game box, a storage box - standing on <paramref name="at"/>.</summary>
        private static GameObject Carton(Transform group, string name, string texture, Vector3 size, Vector3 at, float yaw)
        {
            var material = Finish("bb_mat_p6_label_" + Stem(texture), texture, Vector2.one, Color.white, 0.2f, 0f, false);
            if (material == null) return null;
            return Shape(group, name, material, PrimitiveType.Cube, size, at + Vector3.up * size.y * 0.5f, yaw);
        }

        /// <summary>A tin or a jar: a cylinder with the label wrapped round it, standing on <paramref name="at"/>.</summary>
        private static GameObject Tin(Transform group, string name, string texture, float radius, float height, Vector3 at)
        {
            var material = Finish("bb_mat_p6_label_" + Stem(texture), texture, Vector2.one, Color.white, 0.25f, 0f, false);
            if (material == null) return null;
            return Shape(group, name, material, PrimitiveType.Cylinder, new Vector3(radius * 2f, height * 0.5f, radius * 2f), at + Vector3.up * height * 0.5f, 0f);
        }

        /// <summary>A primitive with no collider and no static flag; small things cast their shadows, as the set pieces do.</summary>
        private static GameObject Shape(Transform parent, string name, Material material, PrimitiveType type, Vector3 size, Vector3 centre, float yaw)
        {
            var shape = GameObject.CreatePrimitive(type);
            shape.name = name;
            UnityEngine.Object.DestroyImmediate(shape.GetComponent<Collider>());
            shape.transform.SetParent(parent, false);
            shape.transform.SetPositionAndRotation(centre, Quaternion.Euler(0f, yaw, 0f));
            shape.transform.localScale = size;
            shape.GetComponent<Renderer>().sharedMaterial = material;
            GameObjectUtility.SetStaticEditorFlags(shape, 0);
            return shape;
        }

        /// <summary>A rug: an alpha-clipped floor quad, its top toward <paramref name="yaw"/>.</summary>
        private static GameObject Rug(Transform group, string name, Room room, string texture, float metres, Vector2 centre, float yaw)
        {
            var material = Finish("bb_mat_p6_rug_" + Stem(texture), texture, Vector2.one, Color.white, 0.12f, 0f, true);
            if (material == null) return null;
            var up = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            return Quad(group, name, material, new Vector3(centre.x, room.FloorTop + FloorLift, centre.y), new Vector2(metres, metres), Vector3.up, up);
        }

        /// <summary>
        /// A lit sign on a band: a glow quad (the branding template's additive unlit) carrying the
        /// texture at <paramref name="width"/>, <paramref name="along"/> metres from the band's
        /// centre, its centre <paramref name="centreHeight"/> above the floor.
        /// </summary>
        private static GameObject Sign(Transform group, string name, Face face, string texture, Color tint, float width, float along, float centreHeight)
        {
            var image = AssetDatabase.LoadAssetAtPath<Texture2D>(texture);
            if (image == null) { Debug.LogWarning("[Gamesim] room finish · missing texture " + texture); return null; }
            var material = Glow("bb_mat_p6_glow_" + Stem(texture), texture, tint);
            if (material == null) return null;
            float h = width * image.height / image.width;
            // Never above the band's top: a sign over the wall line hangs in open sky.
            float y = face.Bottom + Mathf.Min(centreHeight, face.Height - h * 0.5f - 0.02f);
            var centre = new Vector3(face.Centre.x, y, face.Centre.z) + face.Along * along + face.Normal * SignLift;
            return Quad(group, name, material, centre, new Vector2(width, h), face.Normal, Vector3.up);
        }

        /// <summary>A catalogue piece at a floor fraction, the way a set-piece row is placed; no collider, no static flags.</summary>
        private static GameObject Piece(House house, Room room, Transform group, string model, float x, float z, float yaw, float height, float lift = 0f)
        {
            var instance = HouseSetPieces.Model(model, group, yaw, height);
            if (instance == null) return null;
            var b = room.Bounds;
            var scaled = Measure(instance);
            var target = new Vector3(b.center.x + x * b.size.x, b.max.y + lift, b.center.z + z * b.size.z);
            instance.transform.position += target - new Vector3(scaled.center.x, scaled.min.y, scaled.center.z);
            foreach (var collider in instance.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(collider);
            foreach (var node in instance.GetComponentsInChildren<Transform>(true)) GameObjectUtility.SetStaticEditorFlags(node.gameObject, 0);
            return instance;
        }

        /// <summary>Disables the renderers of every placed piece named <paramref name="pieceName"/> in the room. Returns how many it switched off.</summary>
        private static int Hide(House house, Room room, string pieceName)
        {
            int count = 0;
            foreach (var piece in house.Pieces(room, pieceName))
                foreach (var renderer in piece.GetComponentsInChildren<Renderer>(true))
                    if (renderer.enabled) { renderer.enabled = false; count++; }
            return count;
        }

        /// <summary>Disables every rug already lying in the room - the flat colour pieces the pass's own rug replaces.</summary>
        private static int HideRugs(House house, Room room)
        {
            int count = 0;
            foreach (var piece in house.PiecesMatching(room, name => name.IndexOf("rug", StringComparison.OrdinalIgnoreCase) >= 0))
                foreach (var renderer in piece.GetComponentsInChildren<Renderer>(true))
                    if (renderer.enabled) { renderer.enabled = false; count++; }
            return count;
        }

        /// <summary>Re-materials one slot (by material name prefix) of every placed piece named <paramref name="pieceName"/> in the room.</summary>
        private static int Skin(House house, Room room, string pieceName, string slotPrefix, Material material)
        {
            if (material == null) return 0;
            int count = 0;
            foreach (var piece in house.Pieces(room, pieceName))
                foreach (var renderer in piece.GetComponentsInChildren<Renderer>(true))
                {
                    var slots = renderer.sharedMaterials;
                    bool changed = false;
                    for (int i = 0; i < slots.Length; i++)
                        if (slots[i] != null && slots[i] != material && slots[i].name.StartsWith(slotPrefix, StringComparison.Ordinal))
                        { slots[i] = material; changed = true; count++; }
                    if (changed) renderer.sharedMaterials = slots;
                }
            return count;
        }

        /// <summary>
        /// A quad facing <paramref name="normal"/> with its image's top toward <paramref name="up"/>:
        /// no collider, no shadow, no static flags. Unity's quad shows its image to a viewer on its
        /// local -z, so it looks along -normal.
        /// </summary>
        private static GameObject Quad(Transform parent, string name, Material material, Vector3 centre, Vector2 size, Vector3 normal, Vector3 up)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = name;
            UnityEngine.Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.transform.SetParent(parent, false);
            quad.transform.SetPositionAndRotation(centre, Quaternion.LookRotation(-normal, up));
            quad.transform.localScale = new Vector3(size.x, size.y, 1f);
            var renderer = quad.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = true;
            renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
            GameObjectUtility.SetStaticEditorFlags(quad, 0);
            return quad;
        }

        // ---------------------------------------------------------------- materials

        /// <summary>
        /// A URP/Lit material asset under <see cref="MaterialFolder"/>, created when absent and
        /// re-applied when present, so what a surface wears is this file and not an inspector.
        /// </summary>
        public static Material Finish(string name, string texturePath, Vector2 tiling, Color tint, float smoothness, float metallic, bool cutout)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if (texture == null) { Debug.LogWarning("[Gamesim] room finish · missing texture " + texturePath); return null; }
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) { Debug.LogWarning("[Gamesim] room finish · URP/Lit is not in the project"); return null; }
            var material = MaterialAsset(name, shader);
            material.SetTexture("_BaseMap", texture);
            material.SetTextureScale("_BaseMap", tiling);
            material.SetTextureOffset("_BaseMap", Vector2.zero);
            material.SetColor("_BaseColor", tint);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_AlphaClip", cutout ? 1f : 0f);
            material.SetFloat("_Cutoff", 0.5f);
            material.SetFloat("_AlphaToMask", cutout ? 1f : 0f);
            if (cutout)
            {
                material.EnableKeyword("_ALPHATEST_ON");
                material.SetOverrideTag("RenderType", "TransparentCutout");
                material.renderQueue = (int)RenderQueue.AlphaTest;
            }
            else
            {
                material.DisableKeyword("_ALPHATEST_ON");
                material.SetOverrideTag("RenderType", "Opaque");
                material.renderQueue = -1;
            }
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>
        /// A glow material asset: the branding template's transparent additive unlit (the variant a
        /// player build keeps) carrying <paramref name="texturePath"/> (null for a plain strip) at
        /// <paramref name="tint"/>; above 1 the tint reads as light through the bloom.
        /// </summary>
        public static Material Glow(string name, string texturePath, Color tint)
        {
            var template = AssetDatabase.LoadAssetAtPath<Material>(HouseBrandingAssets.GlowMaterialPath);
            if (template == null) { Debug.LogWarning("[Gamesim] room finish · build the branding palette first (no glow template)"); return null; }
            Texture2D texture = null;
            if (texturePath != null)
            {
                texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
                if (texture == null) { Debug.LogWarning("[Gamesim] room finish · missing texture " + texturePath); return null; }
            }
            var material = MaterialAsset(name, template.shader);
            material.CopyPropertiesFromMaterial(template);
            HouseBrandingAssets.ConfigureAdditive(material);
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", tint);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material MaterialAsset(string name, Shader shader)
        {
            EnsureMaterialFolder();
            string path = MaterialFolder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader != shader) material.shader = shader;
            return material;
        }

        private static void EnsureMaterialFolder()
        {
            if (AssetDatabase.IsValidFolder(MaterialFolder)) return;
            string parent = Path.GetDirectoryName(MaterialFolder).Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(parent))
                AssetDatabase.CreateFolder(Path.GetDirectoryName(parent).Replace('\\', '/'), Path.GetFileName(parent));
            AssetDatabase.CreateFolder(parent, Path.GetFileName(MaterialFolder));
        }

        private static string MaterialName(string texture, float length, float height) =>
            "bb_mat_p6_" + Stem(texture) + "_" + Mathf.RoundToInt(length * 10f) + "x" + Mathf.RoundToInt(height * 10f);

        private static string Stem(string texturePath) => Path.GetFileNameWithoutExtension(texturePath);

        private static Bounds Measure(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        // ---------------------------------------------------------------- the house as read

        /// <summary>A room: its floor's world bounds and the height of the walls around it.</summary>
        private sealed class Room
        {
            public readonly string Name;
            public readonly Bounds Bounds;
            public readonly float WallHeight;
            public float FloorTop => Bounds.max.y;
            public Room(string name, Bounds bounds, float wallHeight) { Name = name; Bounds = bounds; WallHeight = wallHeight; }

            /// <summary>Whether a point lies over the floor, allowing <paramref name="margin"/> beyond its edge.</summary>
            public bool Contains(Vector3 point, float margin) =>
                point.x >= Bounds.min.x - margin && point.x <= Bounds.max.x + margin
                && point.z >= Bounds.min.z - margin && point.z <= Bounds.max.z + margin;
        }

        /// <summary>The house as the pass reads it: floors and walls by name, the pieces already placed.</summary>
        private sealed class House
        {
            private readonly Transform world, root;
            private readonly Dictionary<string, Renderer> floors = new Dictionary<string, Renderer>();
            private readonly Dictionary<string, BoxCollider> walls = new Dictionary<string, BoxCollider>();
            private readonly List<BoxCollider> wallList = new List<BoxCollider>();

            public House(Transform world, Transform root)
            {
                this.world = world;
                this.root = root;
                foreach (var node in world.GetComponentsInChildren<Transform>(true))
                {
                    if (node.IsChildOf(root)) continue;
                    var renderer = node.GetComponent<Renderer>();
                    if (renderer != null && node.name.EndsWith(" floor", StringComparison.Ordinal) && !floors.ContainsKey(node.name))
                        floors[node.name] = renderer;
                    var box = node.GetComponent<BoxCollider>();
                    if (box != null && IsWall(box))
                    {
                        wallList.Add(box);
                        if (!walls.ContainsKey(node.name)) walls[node.name] = box;
                    }
                }
            }

            /// <summary>The Shell() rule: tall, thin and long, and not the television.</summary>
            private static bool IsWall(BoxCollider box)
            {
                var size = Vector3.Scale(box.size, box.transform.lossyScale);
                return size.y >= 1f && Mathf.Min(size.x, size.z) <= 0.3f && Mathf.Max(size.x, size.z) >= 1f && box.name != "Television";
            }

            public Room Room(string floorName)
            {
                if (!floors.TryGetValue(floorName, out var floor)) return null;
                var bounds = floor.bounds;
                float height = 0f;
                foreach (var wall in wallList)
                {
                    var wb = wall.bounds;
                    bool touches = wb.max.x >= bounds.min.x - 0.3f && wb.min.x <= bounds.max.x + 0.3f
                        && wb.max.z >= bounds.min.z - 0.3f && wb.min.z <= bounds.max.z + 0.3f;
                    if (touches) height = Mathf.Max(height, wb.max.y);
                }
                return new Room(floorName, bounds, height > 0f ? height : bounds.max.y + 1.1f);
            }

            public BoxCollider Wall(string name) => walls.TryGetValue(name, out var wall) ? wall : null;

            /// <summary>
            /// Placed pieces named <paramref name="name"/> standing in the room, outside the pass's
            /// own root. A set-piece row names its instance exactly; the furnishing pass names its
            /// model "<target> (model)" beside the primitive it replaced, so a name matches its own
            /// parenthesised variants too.
            /// </summary>
            public IEnumerable<Transform> Pieces(Room room, string name) =>
                PiecesMatching(room, candidate => candidate == name || candidate.StartsWith(name + " (", StringComparison.Ordinal));

            public IEnumerable<Transform> PiecesMatching(Room room, Func<string, bool> name)
            {
                foreach (var node in world.GetComponentsInChildren<Transform>(true))
                {
                    if (node.IsChildOf(root) || !name(node.name)) continue;
                    var renderers = node.GetComponentsInChildren<Renderer>(true);
                    if (renderers.Length == 0) continue;
                    var bounds = renderers[0].bounds;
                    foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                    if (room.Contains(bounds.center, 0.2f)) yield return node;
                }
            }
        }
    }
}
