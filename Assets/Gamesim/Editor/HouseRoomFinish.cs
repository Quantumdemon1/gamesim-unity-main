using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Gamesim.House;
using Gamesim.Presentation;
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
        /// <summary>The base-colour lift a dark rug design gets, so it reads on the darkened floors from above.</summary>
        public const float DarkRugLift = 1.6f;
        /// <summary>Self-illumination for the pass's probe-lit surfaces (see <see cref="Finish"/>): rugs, bands, decals, labels.</summary>
        public const float RugGlow = 0.30f, BandGlow = 0.22f, DecalGlow = 0.30f, LabelGlow = 0.22f;
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
                    case "Nomination room": count += NominationRoom(house, group); break;
                    case "Game room": count += GameRoom(house, group); break;
                    case "Bedroom": count += Bedroom(house, group); break;
                    case "Competition yard": count += CompetitionYard(house, group); break;
                }
            }
            // Pass 8: the perimeter neon comes down, room by room, to a supporting brightness.
            foreach (var (floor, factor) in NeonBalance) count += Dim(house, floor, factor);
            return count;
        }

        /// <summary>Per room, how far the perimeter neon comes down (plan §7); the yard keeps its own.</summary>
        public static readonly (string floor, float factor)[] NeonBalance =
        {
            ("Private room floor", 0.5f), ("HoH floor", 0.5f), ("Games floor", 0.6f), ("Bedroom floor", 0.6f),
            ("Kitchen floor", 0.7f), ("Living room floor", 0.8f), ("Nomination floor", 0.8f),
        };

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
            if (Rug(group, "Diary rug", room, Pack6 + "DiaryRoom/diary_radial_carpet.png", 3.6f, new Vector2(at.x, at.z), 0f, true) != null) count++;

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
                // A folded authored towel on the end panel. The room-finish layer keeps the same
                // support point, but the prop now has the thickness and folds of the set's model.
                if (PieceAt(group, "Dish towel", "bb_set_towel", new Vector3(left + 6.15f, counter, mid), 0f) != null) count++;
            }

            // The long table's rug, centred under it.
            var table = house.Pieces(room, "bb_set_diningtable").FirstOrDefault();
            if (table != null)
            {
                var tb = Measure(table.gameObject);
                if (Rug(group, "Dining rug", room, Pack6 + "Rugs/rug_living_navy_cream.png", 5.0f, new Vector2(tb.center.x, tb.center.z), 0f, true) != null) count++;
            }

            // The room's sign, on the east wall where the overview reads it.
            var east = WallFace(house, room, "East wall");
            if (east.HasValue && Sign(group, "Kitchen sign", east.Value, Pack5 + "RoomSignage/kitchen_sign.png",
                new Color(0.90f, 0.95f, 1.00f) * 1.2f, 1.4f, room.Bounds.center.z - east.Value.Centre.z, 1.05f) != null) count++;
            return count;
        }

        /// <summary>
        /// The living room (plan §5.3, rebuilt for MOCKUP-PASS-PLAN M21): the eviction's set. The
        /// room's screen stands on its stage against the south wall now, behind the two red chairs,
        /// with the U of couches facing it, so the south wall is the feature wall: warm oak slats, a
        /// warm gold strip either side of the stage, the house mark beside it and a plant at each of
        /// its ends. The screen gets the gold linework frame the nomination room's wears, its own
        /// idle board - the house's GAMESIM, not the nomination room's NOMINATIONS - and the dark
        /// marble on its stage. The north partition keeps its oak (the storyboard's gold slats run
        /// behind the couches too) and takes the two framed prints. The navy rug binds the U, the low
        /// table and the red chairs; the table gets its cluster and a small plant. The seats do not
        /// move: they are anchors, laid on the pieces at runtime.
        /// </summary>
        private static int LivingRoom(House house, Transform group)
        {
            var room = house.Room("Living room floor");
            if (room == null) { Debug.LogWarning("[Gamesim] room finish · no living room floor"); return 0; }
            int count = 0;
            const string oak = Pack6 + "WallTreatments/wall_warm_oak_slats.png";
            var warm = new Color(1.00f, 0.85f, 0.45f);

            // The partition behind the U keeps its slats and hangs the prints, one on each segment,
            // clear of the bedroom door between them.
            var left = Band(house, room, group, "Living / bedroom left", oak, 1.0f);
            var right = Band(house, room, group, "Living / bedroom right", oak, 1.0f);
            count += (left.HasValue ? 1 : 0) + (right.HasValue ? 1 : 0);
            if (left.HasValue && Poster(group, "Framed print", left.Value, Pack6 + "WallArt/wall_art_01.png", Pack6 + "PhotoFrames/frame_portrait_single.png",
                0.6f, room.Bounds.center.x - 2.5f - left.Value.Centre.x, 0.95f) != null) count++;
            if (right.HasValue && Poster(group, "Framed print", right.Value, Pack6 + "WallArt/wall_art_05.png", Pack6 + "PhotoFrames/frame_portrait_single.png",
                0.6f, room.Bounds.center.x + 2.7f - right.Value.Centre.x, 0.95f) != null) count++;

            // The screen and its wall. Where the pass found no screen of the room's own, the strips
            // flank the room's middle-west, where the plan puts it.
            var screen = house.PiecesMatching(room, name => name == CeremonySets.LivingScreenName).FirstOrDefault();
            float screenX = screen != null ? screen.position.x : room.Bounds.center.x - 1f;
            var south = Band(house, room, group, "South wing link left", oak, 1.0f);
            if (south.HasValue)
            {
                count++;
                var f = south.Value;
                if (Strip(group, "Feature strip", f, screenX - 3.6f - f.Centre.x, 0.75f, 1.35f, warm) != null) count++;
                if (Strip(group, "Feature strip", f, screenX + 3.6f - f.Centre.x, 0.75f, 1.35f, warm) != null) count++;
                var mark = new Vector3(screenX + 3.0f, f.Bottom + 0.62f, f.Centre.z) + f.Normal * SignLift;
                if (Decal(group, "House mark", Pack5 + "EnvironmentWallGraphics/gamesim_house_geometry.png", mark, 0.6f, f.Normal) != null) count++;
            }
            if (screen != null)
            {
                var face = ScreenSurface.Measure(screen, CeremonySets.LivingRoom, room.Bounds.center);
                if (face != null)
                {
                    var lines = Glow("bb_mat_p6_glow_gold_linework_mask", Pack6 + "NominationRoom/gold_linework_mask.png", warm * 1.4f);
                    if (lines != null)
                    {
                        Quad(group, "Ceremony frame", lines, face.Centre - face.Normal * 0.16f,
                            new Vector2(face.Width + 0.5f, face.Height + 0.4f), face.Normal, Vector3.up);
                        count++;
                    }
                    // The house's idle board (16:9), inside the board's face.
                    var idle = Finish("bb_mat_p6_decal_display_idle_gamesim", Pack5 + "DigitalDisplays/display_idle_gamesim.png", Vector2.one, Color.white, 0.2f, 0f, true);
                    if (idle != null)
                    {
                        foreach (var old in screen.GetComponentsInChildren<Transform>(true).Where(t => t.name == ScreenSurface.IdleDisplayName).ToList())
                            UnityEngine.Object.DestroyImmediate(old.gameObject);
                        float w = Mathf.Min(face.Width * 0.94f, face.Height * 0.94f * 1920f / 1080f);
                        Quad(screen, ScreenSurface.IdleDisplayName, idle, face.Centre + face.Normal * 0.006f, new Vector2(w, w * 1080f / 1920f), face.Normal, Vector3.up);
                        count++;
                    }
                }
                count += Skin(house, room, CeremonySets.LivingScreenName, "bb_mat_ink_stage",
                    Finish("bb_mat_p6_nomination_stage_marble", Pack6 + "NominationRoom/dark_marble_detail.png", Vector2.one, Color.white, 0.5f, 0f, false));
                var sb = Measure(screen.gameObject);
                float plantZ = (sb.center.z - room.Bounds.center.z) / room.Bounds.size.z;
                foreach (float end in new[] { sb.min.x - 0.35f, sb.max.x + 0.35f })
                    if (Piece(house, room, group, "bb_set_ph_plant", (end - room.Bounds.center.x) / room.Bounds.size.x, plantZ, 20f, 0f) != null) count++;
            }

            // The gallery on one rug: the U, the low table and the red chairs.
            var table = house.Pieces(room, "bb_set_lowtable").FirstOrDefault();
            var couches = house.PiecesMatching(room, name => name == "bb_set_lounge4" || name == "bb_set_lounge3").ToList();
            if (table != null)
            {
                var tb = Measure(table.gameObject);
                float top = tb.max.y - room.FloorTop;
                float x = (tb.center.x - room.Bounds.center.x) / room.Bounds.size.x, z = (tb.center.z - room.Bounds.center.z) / room.Bounds.size.z;
                float dx = 0.20f / room.Bounds.size.x, dz = 0.16f / room.Bounds.size.z;
                if (Piece(house, room, group, "bb_set_bookstack", x - dx, z + dz * 0.5f, 15f, 0f, top) != null) count++;
                if (Piece(house, room, group, "bb_set_candle", x + dx * 0.7f, z - dz, 0f, 0f, top) != null) count++;
                if (Piece(house, room, group, "bb_set_ph_plantsmall", x + dx * 0.4f, z + dz * 0.9f, 0f, 0f, top) != null) count++;
            }
            if (couches.Count > 0)
            {
                var gallery = couches.Select(c => Measure(c.gameObject)).Aggregate((a, b) => { a.Encapsulate(b); return a; });
                // 7 m across, ending at the stage's front: under the whole U - the arms' feet too -
                // the table and the red chairs, and not under the stage.
                const float across = 7f;
                float stageFront = screen != null ? Measure(screen.gameObject).max.z : gallery.min.z - 1.2f;
                var centre = new Vector2(gallery.center.x, stageFront + across * 0.5f);
                count += HideRugsUnder(house, room, centre, across);
                if (Rug(group, "Living rug", room, Pack6 + "Rugs/rug_living_navy_cream.png", across, centre, 0f, true) != null) count++;
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
                var throwOver = Finish("bb_mat_p6_hoh_throw", Pack6 + "Bedding/throw_blanket_hoh.png", Vector2.one, Color.white, 0.12f, 0f, true, DecalGlow);
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
                var envelope = Finish("bb_mat_p6_decal_hoh_envelope", Pack6 + "HOHRewards/hoh_envelope.png", Vector2.one, Color.white, 0.2f, 0f, true, DecalGlow);
                if (envelope != null) { Quad(group, "HoH envelope", envelope, new Vector3(cb.min.x + 0.22f, top + 0.004f, cb.center.z - 0.05f), new Vector2(0.24f, 0.15f), Vector3.up, Quaternion.Euler(0f, 12f, 0f) * Vector3.forward); count++; }
                var letter = Finish("bb_mat_p6_decal_hoh_letter_sheet", Pack6 + "HOHRewards/hoh_letter_sheet.png", Vector2.one, Color.white, 0.2f, 0f, true, DecalGlow);
                if (letter != null) { Quad(group, "HoH letter", letter, new Vector3(cb.min.x + 0.40f, top + 0.006f, cb.center.z + 0.08f), new Vector2(0.21f, 0.16f), Vector3.up, Quaternion.Euler(0f, -6f, 0f) * Vector3.forward); count++; }
                if (Carton(group, "Gift box", Pack6 + "HOHRewards/gift_wrap_gold.png", new Vector3(0.22f, 0.16f, 0.16f), new Vector3(cb.max.x - 0.22f, top, cb.center.z), 15f) != null) count++;
                if (Carton(group, "Snack bag", Pack6 + "HOHRewards/snack_bag_gold.png", new Vector3(0.14f, 0.20f, 0.05f), new Vector3(cb.max.x - 0.48f, top, cb.center.z - 0.08f), -10f) != null) count++;
                if (Carton(group, "Snack bag", Pack6 + "HOHRewards/snack_bag_blue.png", new Vector3(0.14f, 0.20f, 0.05f), new Vector3(cb.max.x - 0.64f, top, cb.center.z + 0.04f), 25f) != null) count++;
                if (PieceAt(group, "HoH drink", "bb_set_bottle", new Vector3(cb.max.x - 0.36f, top, cb.center.z + 0.12f), 0f) != null) count++;
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

        /// <summary>
        /// The nomination room (plan §5.5): the display looks installed. The presentation wall
        /// behind the board, the gold linework glowing round the board's edge, the stage in dark
        /// marble with the ornament on its front, the idle display on the face itself (under the
        /// prop, so a card covers it and the living room's clone carries it), the ceremonial ring
        /// on the floor where the house sits, and the emblem on each divider.
        /// </summary>
        private static int NominationRoom(House house, Transform group)
        {
            var room = house.Room("Nomination floor");
            if (room == null) { Debug.LogWarning("[Gamesim] room finish · no nomination floor"); return 0; }
            int count = 0;
            Band(house, room, group, "South wing south wall", Pack6 + "WallTreatments/nomination_presentation_wall.png", 1.0f);
            count++;

            var screen = house.Pieces(room, "bb_set_ceremonyscreen").FirstOrDefault();
            if (screen != null)
            {
                var face = ScreenSurface.Measure(screen, "Nomination", room.Bounds.center);
                if (face != null)
                {
                    var lines = Glow("bb_mat_p6_glow_gold_linework_mask", Pack6 + "NominationRoom/gold_linework_mask.png", new Color(1.00f, 0.85f, 0.45f) * 1.4f);
                    if (lines != null)
                    {
                        Quad(group, "Ceremony frame", lines, face.Centre - face.Normal * 0.16f,
                            new Vector2(face.Width + 0.5f, face.Height + 0.4f), face.Normal, Vector3.up);
                        count++;
                    }
                    var idle = Finish("bb_mat_p6_decal_nomination_idle_display", Pack6 + "NominationRoom/nomination_idle_display.png", Vector2.one, Color.white, 0.2f, 0f, true);
                    if (idle != null)
                    {
                        foreach (var old in screen.GetComponentsInChildren<Transform>(true).Where(t => t.name == ScreenSurface.IdleDisplayName).ToList())
                            UnityEngine.Object.DestroyImmediate(old.gameObject);
                        float w = face.Width * 0.92f;
                        Quad(screen, ScreenSurface.IdleDisplayName, idle, face.Centre + face.Normal * 0.006f, new Vector2(w, w * 420f / 1200f), face.Normal, Vector3.up);
                        count++;
                    }
                    if (Decal(group, "Stage ornament", Pack6 + "NominationRoom/screen_frame_ornament.png",
                        new Vector3(face.Centre.x, room.FloorTop + 0.55f, face.Centre.z) + face.Normal * 0.03f, 1.4f, face.Normal) != null) count++;
                }
                count += Skin(house, room, "bb_set_ceremonyscreen", "bb_mat_ink_stage",
                    Finish("bb_mat_p6_nomination_stage_marble", Pack6 + "NominationRoom/dark_marble_detail.png", Vector2.one, Color.white, 0.5f, 0f, false));
            }

            var table = house.Pieces(room, "tableRound").FirstOrDefault();
            var ringAt = table != null ? new Vector2(Measure(table.gameObject).center.x, Measure(table.gameObject).center.z)
                : new Vector2(room.Bounds.center.x, room.Bounds.center.z);
            count += HideRugsUnder(house, room, ringAt, 4.6f);
            if (Rug(group, "Ceremony ring", room, Pack6 + "NominationRoom/ceremonial_floor_ring.png", 4.6f, ringAt, 0f, true) != null) count++;

            foreach (var wall in new[] { "South wing divider 1 south", "South wing divider 2 south" })
            {
                var f = WallFace(house, room, wall);
                if (!f.HasValue) continue;
                var v = f.Value;
                if (Decal(group, "Wall emblem", Pack6 + "NominationRoom/subtle_wall_emblem.png",
                    new Vector3(v.Centre.x, v.Bottom + 0.62f, v.Centre.z) + v.Normal * SignLift, 0.8f, v.Normal) != null) count++;
            }
            return count;
        }

        /// <summary>
        /// The game room (plan §5.6): three zones, playful, and less neon. Charcoal geometric on
        /// the west divider, one lit marquee over the bar, the trophy plaque beside the bookcase,
        /// game boxes stacked on the coffee table, two posters on the south wall, a mini fridge by
        /// the bar, the pack-5 pattern beside the television, and the playful circle rug under the
        /// lounge. The neon comes down with the pass's balance.
        /// </summary>
        private static int GameRoom(House house, Transform group)
        {
            var room = house.Room("Games floor");
            if (room == null) { Debug.LogWarning("[Gamesim] room finish · no games floor"); return 0; }
            int count = 0;
            Band(house, room, group, "South wing divider 2 south", Pack6 + "WallTreatments/wall_charcoal_geometric.png", 1.0f);
            Band(house, room, group, "South wing divider 2 north", Pack6 + "WallTreatments/wall_charcoal_geometric.png", 1.0f);
            count += 2;

            var east = WallFace(house, room, "South wing east wall");
            var bar = house.Pieces(room, "kitchenBar").FirstOrDefault();
            if (east.HasValue)
            {
                float along = (bar != null ? Measure(bar.gameObject).center.z : room.Bounds.center.z) - east.Value.Centre.z;
                if (Sign(group, "Arcade marquee", east.Value, Pack6 + "GameRoom/arcade_marquee_game_on.png", new Color(1.00f, 0.62f, 0.92f) * 1.3f, 1.6f, along, 0.82f) != null) count++;
                if (bar != null)
                {
                    var bb = Measure(bar.gameObject);
                    if (Carton(group, "Mini fridge", Pack6 + "GameRoom/mini_fridge_graphic.png", new Vector3(0.50f, 0.70f, 0.50f),
                        new Vector3(bb.center.x, room.FloorTop, bb.max.z + 0.45f), 0f) != null) count++;
                }
            }

            var westNorth = WallFace(house, room, "South wing divider 2 north");
            var bookcase = house.Pieces(room, "bookcaseOpen").FirstOrDefault();
            if (westNorth.HasValue && bookcase != null)
            {
                var bb = Measure(bookcase.gameObject);
                var f = westNorth.Value;
                // Beside the bookcase, not over it: the bookcase reaches the wall's own height.
                float z = bb.max.z + 0.55f < room.Bounds.max.z - 0.3f ? bb.max.z + 0.55f : bb.min.z - 0.55f;
                if (Decal(group, "Trophy plaque", Pack6 + "GameRoom/trophy_plaque.png", new Vector3(f.Centre.x, f.Bottom + 0.78f, z) + f.Normal * SignLift, 0.6f, f.Normal) != null) count++;
            }

            var table = house.Pieces(room, "tableCoffee").FirstOrDefault();
            var sofa = house.Pieces(room, "loungeSofaCorner").FirstOrDefault();
            if (table != null)
            {
                var tb = Measure(table.gameObject);
                float top = tb.max.y;
                var boxes = new[] { "boardgame_strategy", "boardgame_alliance", "cardgame_social" };
                for (int i = 0; i < boxes.Length; i++)
                    if (Carton(group, "Game box", Pack6 + "GameRoom/" + boxes[i] + ".png", new Vector3(0.30f, 0.05f, 0.20f),
                        new Vector3(tb.center.x - 0.12f + i * 0.01f, top + i * 0.05f, tb.center.z + 0.04f - i * 0.015f), -8f + i * 9f) != null) count++;
                var centre = new Vector2(tb.center.x, tb.center.z);
                if (sofa != null) { var sb = Measure(sofa.gameObject); centre = (centre + new Vector2(sb.center.x, sb.center.z)) * 0.5f; }
                count += HideRugsUnder(house, room, centre, 3.2f);
                if (Rug(group, "Game rug", room, Pack6 + "Rugs/rug_game_playful_circle.png", 3.2f, centre, 0f, true) != null) count++;
            }

            var south = WallFace(house, room, "South wing south wall");
            if (south.HasValue)
            {
                var s = south.Value;
                if (Poster(group, "Game poster", s, Pack6 + "GameRoom/game_room_poster_1.png", Pack6 + "PhotoFrames/frame_portrait_single.png", 0.55f, room.Bounds.center.x - 1.5f - s.Centre.x, 0.6f) != null) count++;
                if (Poster(group, "Game poster", s, Pack6 + "GameRoom/game_room_poster_2.png", Pack6 + "PhotoFrames/frame_portrait_single.png", 0.55f, room.Bounds.center.x + 2.0f - s.Centre.x, 0.6f) != null) count++;
            }

            var north = WallFace(house, room, "South wing link right");
            var console = house.Pieces(room, "cabinetTelevision").FirstOrDefault();
            if (north.HasValue && console != null)
            {
                var cb = Measure(console.gameObject);
                var n = north.Value;
                if (Decal(group, "Game room pattern", Pack5 + "EnvironmentWallGraphics/game_room_pattern.png",
                    new Vector3(cb.max.x + 1.2f, n.Bottom + 0.6f, n.Centre.z) + n.Normal * SignLift, 1.0f, n.Normal) != null) count++;
            }
            return count;
        }

        /// <summary>
        /// The shared bedroom (plan §5.7): several people clearly live here. Each single bed its own
        /// duvet, the bunks theirs, the pillows their pattern; the geometric blue behind the singles
        /// and the rose behind the bunks; one big shag rug joining the beds; a storage box at every
        /// single's foot and a shoe box at the bunks'; the laundry basket and the suitcase by the
        /// coat rack; books, a bottle, a headphone case and toiletries on the tables; a corkboard
        /// with polaroids on the east partition, two prints on the south one, a magazine on a bed.
        /// </summary>
        private static int Bedroom(House house, Transform group)
        {
            var room = house.Room("Bedroom floor");
            if (room == null) { Debug.LogWarning("[Gamesim] room finish · no bedroom floor"); return 0; }
            int count = 0;
            Band(house, room, group, "House / yard left", Pack6 + "WallTreatments/bedroom_geometric_blue.png", 1.2f);
            Band(house, room, group, "West wall", Pack6 + "WallTreatments/bedroom_geometric_rose.png", 1.2f);
            count += 2;

            Material Duvet(string file) => Finish("bb_mat_p6_" + file, Pack6 + "Bedding/" + file + ".png", new Vector2(1.2f, 1.2f), Color.white, 0.15f, 0f, false);
            count += SkinEach(house, room, "bedSingle", "bb_mat_velvet_teal", new[] { Duvet("duvet_navy_diamond"), Duvet("duvet_blue_stripe"), Duvet("duvet_cream_grid") });
            count += SkinEach(house, room, "bedBunk", "bb_mat_velvet_teal", new[] { Duvet("duvet_green_plain"), Duvet("duvet_rose_plain") });
            count += SkinEach(house, room, "bedBunk", "bb_mat_cushion_coral", new[] { Duvet("duvet_rose_plain"), Duvet("duvet_green_plain") });
            count += Skin(house, room, "pillowBlue", "bb_mat_linen_white",
                Finish("bb_mat_p6_pillow_blue_geo", Pack6 + "Bedding/pillow_blue_geo.png", Vector2.one, Color.white, 0.2f, 0f, false));

            // The textile field: one rug the beds share, off the room's centre toward its open half.
            var rugAt = new Vector2(room.Bounds.center.x - 0.6f, room.Bounds.center.z - 1.0f);
            count += HideRugsUnder(house, room, rugAt, 5.2f);
            if (Rug(group, "Bedroom rug", room, Pack6 + "Rugs/rug_bedroom_neutral_shag.png", 5.2f, rugAt, 0f) != null) count++;

            // Storage at every bed's foot: the foot is the end away from the wall the bed stands against.
            foreach (var bed in house.Pieces(room, "bedSingle"))
            {
                var bb = Measure(bed.gameObject);
                bool headNorth = room.Bounds.max.z - bb.max.z < bb.min.z - room.Bounds.min.z;
                float z = headNorth ? bb.min.z - 0.30f : bb.max.z + 0.30f;
                if (Carton(group, "Storage box", Pack6 + "BedroomClutter/storage_box_label.png", new Vector3(0.42f, 0.28f, 0.30f), new Vector3(bb.center.x, room.FloorTop, z), 0f) != null) count++;
            }
            foreach (var bunk in house.Pieces(room, "bedBunk"))
            {
                var bb = Measure(bunk.gameObject);
                bool headWest = bb.min.x - room.Bounds.min.x < room.Bounds.max.x - bb.max.x;
                float x = headWest ? bb.max.x + 0.28f : bb.min.x - 0.28f;
                if (Carton(group, "Shoe box", Pack6 + "BedroomClutter/shoe_box.png", new Vector3(0.32f, 0.12f, 0.20f), new Vector3(x, room.FloorTop, bb.center.z), 90f) != null) count++;
            }

            var rack = house.Pieces(room, "coatRackStanding").FirstOrDefault();
            if (rack != null)
            {
                var rb = Measure(rack.gameObject);
                if (Tin(group, "Laundry basket", Pack6 + "BedroomClutter/laundry_basket_label.png", 0.22f, 0.55f, new Vector3(rb.center.x + 0.75f, room.FloorTop, rb.center.z - 0.15f)) != null) count++;
                if (Carton(group, "Suitcase", Pack6 + "BedroomClutter/luggage_tag.png", new Vector3(0.55f, 0.42f, 0.22f), new Vector3(rb.center.x + 1.45f, room.FloorTop, rb.center.z - 0.25f), 20f) != null) count++;
            }

            var tables = house.Pieces(room, "sideTableDrawers").OrderBy(t => t.position.x).ToList();
            for (int i = 0; i < tables.Count; i++)
            {
                var tb = Measure(tables[i].gameObject);
                float top = tb.max.y;
                if (i == 0)
                {
                    if (PieceAt(group, "Book", "bb_set_bookstack", new Vector3(tb.center.x + 0.11f, top, tb.center.z - 0.06f), 15f) != null) count++;
                    if (PieceAt(group, "Water bottle", "bb_set_bottle", new Vector3(tb.center.x - 0.14f, top, tb.center.z + 0.10f), 0f) != null) count++;
                }
                else
                {
                    if (PieceAt(group, "Book", "bb_set_bookstack", new Vector3(tb.center.x - 0.11f, top, tb.center.z + 0.08f), -12f) != null) count++;
                    if (Carton(group, "Headphone case", Pack6 + "BedroomClutter/headphone_case.png", new Vector3(0.16f, 0.06f, 0.16f), new Vector3(tb.center.x + 0.16f, top, tb.center.z - 0.08f), 30f) != null) count++;
                }
            }
            var drawers = house.Pieces(room, "cabinetBedDrawer").OrderBy(t => t.position.x).ToList();
            for (int i = 0; i < drawers.Count; i++)
            {
                var db = Measure(drawers[i].gameObject);
                string label = i % 2 == 0 ? "cosmetic_label" : "toiletry_label";
                if (Tin(group, "Toiletry", Pack6 + "BedroomClutter/" + label + ".png", 0.03f, 0.10f, new Vector3(db.center.x + (i % 2 == 0 ? 0.12f : -0.12f), db.max.y, db.center.z)) != null) count++;
            }

            var east = WallFace(house, room, "Bedroom / private room north");
            if (east.HasValue)
            {
                var e = east.Value;
                var at = new Vector3(e.Centre.x, e.Bottom + 0.78f, e.Centre.z) + e.Normal * SignLift;
                if (Decal(group, "Corkboard", Pack6 + "PhotoFrames/corkboard_background.png", at, 1.0f, e.Normal) != null) count++;
                for (int i = 0; i < 3; i++)
                    Decal(group, "Polaroid", Pack6 + "PhotoFrames/polaroid_template.png",
                        at + e.Along * (-0.28f + i * 0.28f) + Vector3.up * (i == 1 ? -0.06f : 0.05f) + e.Normal * 0.003f, 0.18f, e.Normal);
                count += 3;
            }
            var south = WallFace(house, room, "Living / bedroom left");
            if (south.HasValue)
            {
                var s = south.Value;
                if (Poster(group, "Framed print", s, Pack6 + "WallArt/wall_art_03.png", Pack6 + "PhotoFrames/frame_portrait_single.png", 0.5f, -1.4f, 0.85f) != null) count++;
                if (Poster(group, "Framed print", s, Pack6 + "WallArt/wall_art_07.png", Pack6 + "PhotoFrames/frame_portrait_single.png", 0.5f, 1.4f, 0.85f) != null) count++;
            }
            var singles = house.Pieces(room, "bedSingle").OrderBy(t => t.position.x).ToList();
            if (singles.Count > 1)
            {
                var mb = Measure(singles[1].gameObject);
                if (PieceAt(group, "Magazine", "bb_set_magazines", new Vector3(mb.center.x + 0.2f, mb.max.y, mb.center.z - 0.2f), 25f) != null) count++;
            }
            return count;
        }

        /// <summary>
        /// The competition yard (plan §5.8): the course's display on the backdrop's face under its
        /// signs, a start and a finish line on every lane, and flight cases stacked by the west
        /// fence between the planters. The centre stays open; the light towers wait for a piece of
        /// their own (§9).
        /// </summary>
        private static int CompetitionYard(House house, Transform group)
        {
            var room = house.Room("Competition yard floor");
            if (room == null) { Debug.LogWarning("[Gamesim] room finish · no competition yard floor"); return 0; }
            int count = 0;
            var backdrop = house.Pieces(room, "bb_set_comp_backdrop").FirstOrDefault();
            if (backdrop != null)
            {
                var bb = Measure(backdrop.gameObject);
                if (Decal(group, "Yard display", Pack5 + "DigitalDisplays/display_idle_gamesim.png",
                    new Vector3(bb.center.x, room.FloorTop + 0.95f, bb.min.z - 0.03f), 2.4f, Vector3.back) != null) count++;
            }
            foreach (var lane in house.Pieces(room, "bb_set_comp_lane"))
            {
                var lb = Measure(lane.gameObject);
                var start = Finish("bb_mat_p6_decal_start_marker", Pack5 + "CompetitionKit/start_marker.png", Vector2.one, Color.white, 0.2f, 0f, true);
                var finish = Finish("bb_mat_p6_decal_finish_marker", Pack5 + "CompetitionKit/finish_marker.png", Vector2.one, Color.white, 0.2f, 0f, true);
                if (start != null) { Quad(group, "Start line", start, new Vector3(lb.center.x, room.FloorTop + FloorLift + 0.004f, lb.min.z + 1.0f), new Vector2(1.2f, 0.6f), Vector3.up, Vector3.forward); count++; }
                if (finish != null) { Quad(group, "Finish line", finish, new Vector3(lb.center.x, room.FloorTop + FloorLift + 0.004f, lb.max.z - 1.2f), new Vector2(1.2f, 0.6f), Vector3.up, Vector3.forward); count++; }
            }
            for (int i = 0; i < 3; i++)
                if (Carton(group, "Flight case", Pack5 + "CompetitionKit/hazard_stripe_decal.png", new Vector3(0.9f, 0.5f, 0.5f),
                    new Vector3(room.Bounds.min.x + 1.0f + (i == 2 ? 1.1f : 0f), room.FloorTop + (i == 2 ? 0f : 0f), room.Bounds.center.z - 2.4f + i * 0.6f), i * 6f) != null) count++;
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
                Color.white, smoothness, 0f, false, BandGlow);
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

        /// <summary>
        /// A lit strip on a band: a plain glow quad 0.06 m wide and <paramref name="height"/> tall,
        /// standing on the band's foot. Cool blue-white unless a colour is given; a warm strip is a
        /// material of its own, because Glow rewrites a material by name.
        /// </summary>
        private static GameObject Strip(Transform group, string name, Face face, float along, float height, float tint, Color? colour = null)
        {
            var material = colour.HasValue
                ? Glow("bb_mat_p6_glow_strip_warm", null, colour.Value * tint)
                : Glow("bb_mat_p6_glow_strip", null, new Color(0.62f, 0.70f, 1.00f) * tint);
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
            var material = Finish("bb_mat_p6_decal_" + Stem(texture), texture, Vector2.one, Color.white, 0.3f, 0f, true, DecalGlow);
            if (material == null) return null;
            return Quad(group, name, material, centre, new Vector2(width, width * image.height / image.width), normal, Vector3.up);
        }

        /// <summary>A box wearing a label on every face - a cereal box, a snack bag, a game box, a storage box - standing on <paramref name="at"/>.</summary>
        private static GameObject Carton(Transform group, string name, string texture, Vector3 size, Vector3 at, float yaw)
        {
            var material = Finish("bb_mat_p6_label_" + Stem(texture), texture, Vector2.one, Color.white, 0.2f, 0f, false, LabelGlow);
            if (material == null) return null;
            return Shape(group, name, material, PrimitiveType.Cube, size, at + Vector3.up * size.y * 0.5f, yaw);
        }

        /// <summary>A tin or a jar: a cylinder with the label wrapped round it, standing on <paramref name="at"/>.</summary>
        private static GameObject Tin(Transform group, string name, string texture, float radius, float height, Vector3 at)
        {
            var material = Finish("bb_mat_p6_label_" + Stem(texture), texture, Vector2.one, Color.white, 0.25f, 0f, false, LabelGlow);
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

        /// <summary>
        /// A rug: an alpha-clipped floor quad, its top toward <paramref name="yaw"/>. The pack's
        /// darker designs - the diary's carpet, the ceremonial ring, the game room's circle, the
        /// navy rugs - read as black patches from above under the set's darkened floors, so they
        /// take <see cref="DarkRugLift"/> on their base colour; the cream and neutral ones do not.
        /// </summary>
        private static GameObject Rug(Transform group, string name, Room room, string texture, float metres, Vector2 centre, float yaw, bool dark = false)
        {
            var material = Finish("bb_mat_p6_rug_" + Stem(texture), texture, Vector2.one, dark ? Color.white * DarkRugLift : Color.white, 0.12f, 0f, true, dark ? RugGlow * 1.3f : RugGlow);
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

        /// <summary>
        /// Places one authored catalogue prop with its aggregate bottom-centre on an exact world
        /// support point. Room-finish dressing is visual only, so even an authored collision proxy
        /// is removed and every child remains non-static just like the graphics around it.
        /// </summary>
        private static GameObject PieceAt(Transform group, string name, string model, Vector3 bottomCentre, float yaw)
        {
            var instance = HouseSetPieces.Model(model, group, yaw, 0f);
            if (instance == null) return null;
            var bounds = Measure(instance);
            instance.transform.position += bottomCentre - new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            instance.name = name;
            foreach (var collider in instance.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(collider);
            foreach (var node in instance.GetComponentsInChildren<Transform>(true)) GameObjectUtility.SetStaticEditorFlags(node.gameObject, 0);
            foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true)) renderer.shadowCastingMode = ShadowCastingMode.Off;
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

        /// <summary>
        /// Re-materials one slot of every placed piece named <paramref name="pieceName"/> in the
        /// room, each piece the next of <paramref name="materials"/> in turn (by its place, west to
        /// east then south to north), so five beds get four duvets and not one.
        /// </summary>
        private static int SkinEach(House house, Room room, string pieceName, string slotPrefix, Material[] materials)
        {
            if (materials == null || materials.Length == 0 || materials.Any(m => m == null)) return 0;
            int count = 0, i = 0;
            foreach (var piece in house.Pieces(room, pieceName).OrderBy(t => Mathf.Round(t.position.x * 4f)).ThenBy(t => t.position.z))
            {
                var material = materials[i++ % materials.Length];
                foreach (var renderer in piece.GetComponentsInChildren<Renderer>(true))
                {
                    var slots = renderer.sharedMaterials;
                    bool changed = false;
                    for (int s = 0; s < slots.Length; s++)
                        if (slots[s] != null && slots[s] != material && slots[s].name.StartsWith(slotPrefix, StringComparison.Ordinal))
                        { slots[s] = material; changed = true; count++; }
                    if (changed) renderer.sharedMaterials = slots;
                }
            }
            return count;
        }

        /// <summary>
        /// Pass 8: the transplanted neon trim inside a room's bounds wears a copy of its material
        /// with the emission scaled by <paramref name="factor"/>, so the perimeter drops to a
        /// supporting brightness room by room without touching the prototype's materials or the
        /// bake. A trim piece already dimmed is left as it is.
        /// </summary>
        private static int Dim(House house, string floorName, float factor)
        {
            var room = house.Room(floorName);
            if (room == null) return 0;
            int count = 0;
            foreach (var renderer in house.TrimRenderers(room))
            {
                var slots = renderer.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < slots.Length; i++)
                {
                    var neon = slots[i];
                    if (neon == null || !neon.name.StartsWith("Neon ", StringComparison.Ordinal)) continue;
                    var dim = Dimmed(neon, factor);
                    if (dim == null) continue;
                    slots[i] = dim; changed = true; count++;
                }
                if (changed) renderer.sharedMaterials = slots;
            }
            return count;
        }

        /// <summary>The neon material at a fraction of its emission, as an asset the scene can keep.</summary>
        private static Material Dimmed(Material neon, float factor)
        {
            string name = "bb_mat_p6_dim_" + neon.name.Replace(' ', '_').ToLowerInvariant() + "_" + Mathf.RoundToInt(factor * 100f);
            var material = MaterialAsset(name, neon.shader);
            material.CopyPropertiesFromMaterial(neon);
            if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", neon.GetColor("_EmissionColor") * factor);
            EditorUtility.SetDirty(material);
            return material;
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
        ///
        /// <para><paramref name="glow"/>: the set is lightmapped and the pass's quads are not, so
        /// away from a lamp's direct light a rug or a band gets only the night's ambient and reads
        /// black from above. A little self-illumination from the surface's own texture (the
        /// guide's "emissive presentation", not a bake change) keeps it reading as what it is.</para>
        /// </summary>
        public static Material Finish(string name, string texturePath, Vector2 tiling, Color tint, float smoothness, float metallic, bool cutout, float glow = 0f)
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
            if (glow > 0f)
            {
                material.EnableKeyword("_EMISSION");
                material.SetTexture("_EmissionMap", texture);
                material.SetTextureScale("_EmissionMap", tiling);
                material.SetColor("_EmissionColor", Color.white * glow);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            else
            {
                material.DisableKeyword("_EMISSION");
                material.SetTexture("_EmissionMap", null);
                material.SetColor("_EmissionColor", Color.black);
            }
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
            /// The perimeter neon standing in the room: the trim transplanted from the prototype
            /// ("Broadcast Dressing") and the south wing's own (HouseExpansion's "Trim" strips), which
            /// is to say every renderer under the house wearing one of the prototype's neon materials,
            /// outside the pass's own root.
            /// </summary>
            public IEnumerable<Renderer> TrimRenderers(Room room)
            {
                foreach (var renderer in world.GetComponentsInChildren<Renderer>(true))
                {
                    if (renderer.transform.IsChildOf(root)) continue;
                    var material = renderer.sharedMaterial;
                    if (material == null || !material.name.StartsWith("Neon ", StringComparison.Ordinal)) continue;
                    if (room.Contains(renderer.bounds.center, 0.15f)) yield return renderer;
                }
            }

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
