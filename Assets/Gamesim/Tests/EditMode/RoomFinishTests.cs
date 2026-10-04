using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Gamesim.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The room finish pass (ROOM-FINISH-PLAN.md) as saved in the episode scene: Pack 6's finish
    /// graphics laid over the rooms as a root of their own, with nothing that could reach the bake,
    /// the NavMesh or the anchors - no collider, no shadow, no static flag - and every surface
    /// inside its room, under its wall's height, wearing a material the pass made.
    /// </summary>
    public sealed class RoomFinishTests
    {
        private const string EpisodeScene = HouseRoomFinish.EpisodeScene;
        /// <summary>Where the diary chair stands and has stood: the pass dresses around it and never moves it.</summary>
        private static readonly Vector3 DiaryChair = new Vector3(7f, 0f, 4f);

        [Test]
        public void TheRootDressesEveryRoomGroupWithoutAColliderAShadowOrAStaticFlag()
        {
            var scene = EditorSceneManager.OpenPreviewScene(EpisodeScene);
            try
            {
                var root = Root(scene);
                Assert.That(root, Is.Not.Null, "Run Gamesim/U07/Finish the rooms (pack 6).");
                foreach (var group in HouseRoomFinish.RoomGroups)
                    Assert.That(root.Find(group), Is.Not.Null, "A group per room: " + group);
                Assert.That(root.GetComponentsInChildren<Collider>(true), Is.Empty, "Dressing carries no collision: the NavMesh was baked without it and stays so.");
                foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                {
                    Assert.That(GameObjectUtility.GetStaticEditorFlags(renderer.gameObject), Is.EqualTo((StaticEditorFlags)0),
                        renderer.name + ": probe-lit dressing on a lightmapped set; nothing here contributes to a bake it is not in.");
                    var filter = renderer.GetComponent<MeshFilter>();
                    bool quad = filter != null && filter.sharedMesh != null && filter.sharedMesh.name == "Quad";
                    if (quad)
                        Assert.That(renderer.shadowCastingMode, Is.EqualTo(ShadowCastingMode.Off), renderer.name + ": a flat graphic casts no shadow.");
                    foreach (var material in renderer.sharedMaterials)
                    {
                        Assert.That(material, Is.Not.Null, renderer.name + " has an empty material slot.");
                        string path = AssetDatabase.GetAssetPath(material);
                        bool owned = path.StartsWith(HouseRoomFinish.MaterialFolder + "/", StringComparison.Ordinal);
                        bool authored = path.StartsWith(AuthoredAssetImporter.Root, StringComparison.Ordinal);
                        Assert.That(owned || authored, Is.True, renderer.name + " wears " + path + ": the pass's own materials, or an authored piece's.");
                    }
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [Test]
        public void TheDiaryRoomIsAConfessionalAroundTheChairThatDidNotMove()
        {
            var scene = EditorSceneManager.OpenPreviewScene(EpisodeScene);
            try
            {
                var all = scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<Transform>(true)).ToArray();
                var floor = all.First(t => t.name == "Private room floor").GetComponent<Renderer>().bounds;
                var chair = all.Single(t => t.name == "bb_set_diarychair");
                Assert.That(Vector3.Distance(chair.position, DiaryChair), Is.LessThan(0.02f), "The chair, and so its seat anchor, stays where the diary walks to.");

                var root = Root(scene);
                var diary = root.Find("Diary room");
                Assert.That(diary, Is.Not.Null);
                var renderers = diary.GetComponentsInChildren<Renderer>(true);
                Assert.That(renderers.Length, Is.GreaterThanOrEqualTo(11), "Six wall bands, a rug, three signs and two plants.");
                float wallTop = floor.max.y + 1.5f;
                foreach (var renderer in renderers)
                {
                    var b = renderer.bounds;
                    Assert.That(b.min.x, Is.GreaterThanOrEqualTo(floor.min.x - 0.3f), renderer.name + " runs out of the room to the west.");
                    Assert.That(b.max.x, Is.LessThanOrEqualTo(floor.max.x + 0.3f), renderer.name + " runs out of the room to the east.");
                    Assert.That(b.min.z, Is.GreaterThanOrEqualTo(floor.min.z - 0.3f), renderer.name + " runs out of the room to the south.");
                    Assert.That(b.max.z, Is.LessThanOrEqualTo(floor.max.z + 0.3f), renderer.name + " runs out of the room to the north.");
                    Assert.That(b.max.y, Is.LessThanOrEqualTo(wallTop + 0.01f), renderer.name + " hangs above the 1.5 m wall, in open sky.");
                }

                var bands = renderers.Where(r => r.name.StartsWith("Band · ", StringComparison.Ordinal)).ToList();
                Assert.That(bands.Count, Is.EqualTo(6), "The four walls in their seven segments, less the doorway the room is entered by.");
                foreach (var band in bands)
                {
                    var texture = band.sharedMaterial.GetTexture("_BaseMap");
                    Assert.That(texture, Is.Not.Null, band.name);
                    Assert.That(AssetDatabase.GetAssetPath(texture).StartsWith(HouseRoomFinish.Pack6, StringComparison.Ordinal), Is.True, band.name + " is dressed from pack 6.");
                    Assert.That(band.sharedMaterial.GetTextureScale("_BaseMap").x, Is.GreaterThan(1f), band.name + " tiles along its run rather than stretching one image.");
                    Assert.That(band.bounds.size.y, Is.GreaterThan(1.3f), band.name + " covers the wall's height.");
                }

                var rug = renderers.Single(r => r.name == "Diary rug");
                Assert.That(Vector2.Distance(new Vector2(rug.bounds.center.x, rug.bounds.center.z), new Vector2(DiaryChair.x, DiaryChair.z)), Is.LessThan(0.05f),
                    "The radial carpet centres on the chair: the halo from above.");
                Assert.That(rug.bounds.size.x, Is.EqualTo(3.6f).Within(0.05f));
                Assert.That(rug.sharedMaterial.IsKeywordEnabled("_ALPHATEST_ON"), Is.True, "cut to its own edge");
                Assert.That(rug.bounds.min.y, Is.GreaterThan(floor.max.y), "and lying on the floor, not in it.");

                foreach (var name in new[] { "Confessional sign", "Diary eye", "Private sign" })
                {
                    var sign = renderers.Single(r => r.name == name);
                    Assert.That(sign.sharedMaterial.shader.name, Is.EqualTo("Universal Render Pipeline/Unlit"), name + " is lit signage: the branding glow.");
                    Assert.That(sign.sharedMaterial.renderQueue, Is.GreaterThanOrEqualTo((int)RenderQueue.Transparent), name + " is light added to the wall.");
                }
                Assert.That(renderers.Count(r => r.transform.parent != null && r.transform.parent.name == "bb_set_ph_plant"
                    || r.name == "bb_set_ph_plant" || (r.transform.parent != null && r.transform.parent.parent != null && r.transform.parent.parent.name == "bb_set_ph_plant")),
                    Is.GreaterThan(0), "Two plants flank the studio.");
                Assert.That(diary.GetComponentsInChildren<Transform>(true).Count(t => t.name == "bb_set_ph_plant"), Is.EqualTo(2));

                // The lounge left: hidden in place, so the layout record survives and nothing else moved.
                bool InRoom(Transform t) { var p = t.position; return p.x > floor.min.x && p.x < floor.max.x && p.z > floor.min.z && p.z < floor.max.z; }
                foreach (var hidden in new[] { "loungeDesignChair", "Confessional chair A", "Confessional chair B", "Private table", "bookcaseOpen" })
                {
                    // A set-piece row's instance is named exactly; the furnishing pass's model is "<target> (model)".
                    var pieces = all.Where(t => (t.name == hidden || t.name.StartsWith(hidden + " (", StringComparison.Ordinal)) && !t.IsChildOf(root) && InRoom(t)).ToList();
                    Assert.That(pieces, Is.Not.Empty, hidden + " is still in the scene, disabled and not destroyed.");
                    Assert.That(pieces.SelectMany(p => p.GetComponentsInChildren<Renderer>(true)).All(r => !r.enabled), Is.True, hidden + " no longer draws.");
                }
                var oldRugs = all.Where(t => !t.IsChildOf(root) && InRoom(t) && t.name.IndexOf("rug", StringComparison.OrdinalIgnoreCase) >= 0)
                    .SelectMany(t => t.GetComponentsInChildren<Renderer>(true)).ToList();
                Assert.That(oldRugs.All(r => !r.enabled), Is.True, "The flat rugs the carpet replaces are switched off, not stacked under it.");

                // The chair wears the pass's velvet and marble in the slots the piece authored for them.
                var slots = chair.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).Where(m => m != null).Select(m => m.name).ToList();
                Assert.That(slots, Does.Contain("bb_mat_p6_diary_chair_velvet"), "navy velvet");
                Assert.That(slots, Does.Contain("bb_mat_p6_diary_dais_marble"), "a marble dais");
                Assert.That(slots, Does.Not.Contain("bb_mat_velvet_teal"), "and no teal left on it.");
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [Test]
        public void TheKitchenGetsItsMarbleCounterBacksplashClustersRugAndSign()
        {
            var scene = EditorSceneManager.OpenPreviewScene(EpisodeScene);
            try
            {
                var all = scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<Transform>(true)).ToArray();
                var floor = all.First(t => t.name == "Kitchen floor").GetComponent<Renderer>().bounds;
                var root = Root(scene);
                var kitchen = root.Find("Kitchen");
                Assert.That(kitchen, Is.Not.Null);
                var renderers = kitchen.GetComponentsInChildren<Renderer>(true);

                var run = all.Single(t => t.name == "bb_set_kitchenrun");
                var slots = run.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).Where(m => m != null).Select(m => m.name).Distinct().ToList();
                Assert.That(slots, Does.Contain("bb_mat_p6_kitchen_counter"), "The counter top is marble.");
                Assert.That(slots, Does.Not.Contain("bb_mat_counter_stone"), "and none of the authored slab is left.");

                var bands = renderers.Where(r => r.name.StartsWith("Band · ", StringComparison.Ordinal)).ToList();
                Assert.That(bands.Count, Is.EqualTo(2), "The backsplash runs on both segments of the north partition.");
                foreach (var band in bands)
                {
                    Assert.That(band.bounds.min.y, Is.GreaterThanOrEqualTo(floor.max.y + 0.9f), band.name + " starts at the counter's height, not the floor.");
                    Assert.That(band.bounds.max.y, Is.LessThanOrEqualTo(floor.max.y + 1.5f + 0.01f), band.name + " stops at the wall's top.");
                    Assert.That(band.bounds.center.z, Is.GreaterThan(floor.max.z - 0.3f), band.name + " is on the north wall.");
                }

                var runBounds = run.GetComponentsInChildren<Renderer>(true).Select(r => r.bounds).Aggregate((a, b) => { a.Encapsulate(b); return a; });
                var counterTop = floor.max.y + 0.92f;
                var clusters = renderers.Where(r => r.name == "Coffee tin" || r.name == "Jar" || r.name == "Cereal" || r.name == "Snack bag" || r.name == "Dish towel").ToList();
                Assert.That(clusters.Count, Is.EqualTo(6), "Coffee, the pantry and a towel: six small things on the counter.");
                foreach (var thing in clusters)
                {
                    Assert.That(thing.bounds.min.y, Is.EqualTo(counterTop).Within(0.02f), thing.name + " stands on the counter.");
                    Assert.That(thing.bounds.center.x, Is.InRange(runBounds.min.x, runBounds.max.x), thing.name + " is on the run.");
                    Assert.That(thing.GetComponent<Collider>(), Is.Null, thing.name + " has no collider.");
                }
                foreach (var panel in new[] { "Fridge panel", "Dishwasher panel", "Oven panel" })
                {
                    var decal = renderers.Single(r => r.name == panel);
                    Assert.That(decal.bounds.center.z, Is.LessThan(runBounds.min.z + 0.02f), panel + " is on the run's front face.");
                    Assert.That(decal.bounds.max.y, Is.LessThan(counterTop), panel + " sits below the counter.");
                }

                var table = all.Single(t => t.name == "bb_set_diningtable");
                var tableBounds = table.GetComponentsInChildren<Renderer>(true).Select(r => r.bounds).Aggregate((a, b) => { a.Encapsulate(b); return a; });
                var rug = renderers.Single(r => r.name == "Dining rug");
                Assert.That(Vector2.Distance(new Vector2(rug.bounds.center.x, rug.bounds.center.z), new Vector2(tableBounds.center.x, tableBounds.center.z)), Is.LessThan(0.05f),
                    "The rug is centred under the long table.");
                var sign = renderers.Single(r => r.name == "Kitchen sign");
                Assert.That(sign.bounds.center.x, Is.GreaterThan(floor.max.x - 0.3f), "The sign hangs on the east wall.");
                Assert.That(sign.sharedMaterial.renderQueue, Is.GreaterThanOrEqualTo((int)RenderQueue.Transparent), "lit.");
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        /// <summary>
        /// The living room as the eviction's set (MOCKUP-PASS-PLAN M21): the U of couches, the low
        /// table and the red chairs on one navy rug; the room's screen on the south wall behind the
        /// chairs, with the oak, the warm strips and the house mark round it; the prints on the north
        /// partition, whose oak stays behind the U; the prototype's sofa, table and television gone.
        /// </summary>
        [Test]
        public void TheLivingRoomIsTheEvictionsSetRoundItsScreenOnTheSouthWall()
        {
            var scene = EditorSceneManager.OpenPreviewScene(EpisodeScene);
            try
            {
                var all = scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<Transform>(true)).ToArray();
                var floor = all.First(t => t.name == "Living room floor").GetComponent<Renderer>().bounds;
                bool InRoom(Transform t) => floor.Contains(new Vector3(t.position.x, floor.center.y, t.position.z));
                var root = Root(scene);
                var living = root.Find("Living room");
                Assert.That(living, Is.Not.Null);
                var renderers = living.GetComponentsInChildren<Renderer>(true);
                Bounds BoundsOf(Transform t) => t.GetComponentsInChildren<Renderer>(true).Select(r => r.bounds).Aggregate((a, b) => { a.Encapsulate(b); return a; });

                // The set: four couches, two red chairs, the low table and the room's own screen, placed.
                var couches = all.Where(t => (t.name == "bb_set_lounge4" || t.name == "bb_set_lounge3") && InRoom(t) && t.gameObject.activeInHierarchy).ToList();
                Assert.That(couches.Count(t => t.name == "bb_set_lounge4"), Is.EqualTo(2), "The U's base: two four-seat couches.");
                Assert.That(couches.Count(t => t.name == "bb_set_lounge3"), Is.EqualTo(2), "Its arms: two three-seat couches.");
                var chairs = all.Where(t => t.name == "bb_set_wingback" && InRoom(t) && t.gameObject.activeInHierarchy).ToList();
                Assert.That(chairs.Count, Is.EqualTo(2), "The two red chairs.");
                var table = all.Single(t => t.name == "bb_set_lowtable" && InRoom(t));
                var screen = all.Single(t => t.name == Gamesim.House.CeremonySets.LivingScreenName);
                Assert.That(InRoom(screen), Is.True, "The room's own screen stands in the room.");
                Assert.That(screen.position.z, Is.LessThan(floor.min.z + 1.6f), "on its stage by the south wall,");
                Assert.That(chairs.All(c => c.position.z > screen.position.z && c.position.z < table.position.z), Is.True,
                    "the red chairs between it and the table.");
                foreach (var gone in new[] { "Television", "Coffee table (model)", "Sofa seat (model)" })
                    Assert.That(all.Where(t => t.name == gone && InRoom(t)).All(t => !t.gameObject.activeInHierarchy), Is.True, gone + " is struck.");
                Assert.That(all.Any(t => t.name == "loungeDesignSofa" && InRoom(t)), Is.False, "The U takes the kitchen-door sofa's place.");

                // The feature wall: oak on the south wall, a warm strip either side of the screen, the mark; the oak behind the U stays.
                var south = renderers.Where(r => r.name == "Band · South wing link left").ToList();
                Assert.That(south.Count, Is.EqualTo(1), "Oak on the south wall behind the screen.");
                Assert.That(AssetDatabase.GetAssetPath(south[0].sharedMaterial.GetTexture("_BaseMap")), Does.EndWith("wall_warm_oak_slats.png"));
                Assert.That(renderers.Count(r => r.name.StartsWith("Band · Living / bedroom", StringComparison.Ordinal)), Is.EqualTo(2),
                    "The north partition keeps its oak behind the U.");
                var strips = renderers.Where(r => r.name == "Feature strip").ToList();
                Assert.That(strips.Count, Is.EqualTo(2), "A lit strip either side of the screen.");
                Assert.That(strips.Min(s => s.bounds.center.x), Is.LessThan(screen.position.x), "one to its left,");
                Assert.That(strips.Max(s => s.bounds.center.x), Is.GreaterThan(screen.position.x), "one to its right,");
                Assert.That(strips.All(s => s.bounds.center.z < floor.min.z + 0.3f), Is.True, "on the south wall,");
                Assert.That(strips.All(s => s.sharedMaterial.renderQueue >= (int)RenderQueue.Transparent), Is.True, "both lit,");
                Assert.That(strips.All(s => s.sharedMaterial.name.EndsWith("_warm", StringComparison.Ordinal)), Is.True, "and warm, not the cool strips elsewhere.");
                Assert.That(renderers.Count(r => r.name == "House mark"), Is.EqualTo(1), "The house mark, once.");
                Assert.That(renderers.Count(r => r.name == "Ceremony frame"), Is.EqualTo(1), "The screen in its gold frame.");
                var idle = screen.GetComponentsInChildren<Renderer>(true).Where(r => r.name == Gamesim.Presentation.ScreenSurface.IdleDisplayName).ToList();
                Assert.That(idle.Count, Is.EqualTo(1), "The screen idles on a board of its own,");
                Assert.That(AssetDatabase.GetAssetPath(idle[0].sharedMaterial.GetTexture("_BaseMap")), Does.EndWith("display_idle_gamesim.png"),
                    "the house's GAMESIM, not the nomination room's NOMINATIONS.");

                // One navy rug under the couches' feet, the table and the chairs; the old rugs off.
                var rug = renderers.Single(r => r.name == "Living rug");
                Assert.That(AssetDatabase.GetAssetPath(rug.sharedMaterial.GetTexture("_BaseMap")), Does.EndWith("rug_living_navy_cream.png"));
                Assert.That(rug.bounds.size.x, Is.EqualTo(7f).Within(0.05f));
                // Under each couch's inner half - the seats' feet - toward the table.
                Assert.That(couches.All(c =>
                {
                    var inward = table.position - c.position; inward.y = 0f;
                    var feet = c.position + inward.normalized * 0.4f;
                    return rug.bounds.Contains(new Vector3(feet.x, rug.bounds.center.y, feet.z));
                }), Is.True, "under every couch's seats,");
                foreach (var gone in new[] { "Books A", "Books B", "Vase", "Lamp - Living", "Frame - Living South" })
                    Assert.That(all.Where(t => t.name == gone && InRoom(t)).All(t => !t.gameObject.activeInHierarchy), Is.True, "The old layout's " + gone + " is struck.");
                Assert.That(all.Where(t => t.name.StartsWith("Television", StringComparison.Ordinal) && t.name.EndsWith("practical", StringComparison.Ordinal) && InRoom(t))
                    .All(t => !t.gameObject.activeInHierarchy), Is.True, "The television's practicals are off with it.");
                Assert.That(rug.bounds.Contains(new Vector3(table.position.x, rug.bounds.center.y, table.position.z)), Is.True, "under the table,");
                Assert.That(chairs.All(c => rug.bounds.Contains(new Vector3(c.position.x, rug.bounds.center.y, c.position.z))), Is.True, "and the red chairs.");
                var oldRugs = all.Where(t => !t.IsChildOf(root) && t.name.IndexOf("rug", StringComparison.OrdinalIgnoreCase) >= 0 && InRoom(t))
                    .Where(t => { var b = BoundsOf(t); return b.Intersects(rug.bounds); }).ToList();
                Assert.That(oldRugs.SelectMany(t => t.GetComponentsInChildren<Renderer>(true)).All(r => !r.enabled || !r.gameObject.activeInHierarchy), Is.True,
                    "The flat rugs under the new one are switched off, not stacked.");

                // The table's cluster stands on its top.
                var tb = BoundsOf(table);
                foreach (var piece in new[] { "bb_set_bookstack", "bb_set_candle", "bb_set_ph_plantsmall" })
                {
                    var thing = living.GetComponentsInChildren<Transform>(true).Single(t => t.name == piece);
                    var b = BoundsOf(thing);
                    Assert.That(b.min.y, Is.EqualTo(tb.max.y).Within(0.03f), piece + " stands on the low table.");
                    Assert.That(b.center.x, Is.InRange(tb.min.x - 0.3f, tb.max.x + 0.3f), piece + " is on the table.");
                    Assert.That(b.center.z, Is.InRange(tb.min.z - 0.3f, tb.max.z + 0.3f), piece + " is on the table.");
                }

                // Two framed prints on the north partition, clear of the bedroom door.
                var prints = renderers.Where(r => r.name == "Framed print").ToList();
                Assert.That(prints.Count, Is.EqualTo(2), "Two prints.");
                Assert.That(renderers.Count(r => r.name == "Framed print frame"), Is.EqualTo(2), "each in its frame.");
                Assert.That(prints.All(p => p.bounds.center.z > floor.max.z - 0.3f), Is.True, "on the north partition.");
                Assert.That(living.GetComponentsInChildren<Transform>(true).Count(t => t.name == "bb_set_ph_plant"), Is.EqualTo(3),
                    "A plant at each end of the stage, and one in the corner.");
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [Test]
        public void TheHohSuiteGetsItsBedWallsRugAndRewardStation()
        {
            var scene = EditorSceneManager.OpenPreviewScene(EpisodeScene);
            try
            {
                var all = scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<Transform>(true)).ToArray();
                var floor = all.First(t => t.name == "HoH floor").GetComponent<Renderer>().bounds;
                var root = Root(scene);
                var suite = root.Find("HoH suite");
                Assert.That(suite, Is.Not.Null);
                var renderers = suite.GetComponentsInChildren<Renderer>(true);
                Bounds BoundsOf(Transform t) => t.GetComponentsInChildren<Renderer>(true).Select(r => r.bounds).Aggregate((a, b) => { a.Encapsulate(b); return a; });

                var bed = all.Single(t => t.name == "bb_set_hohbed");
                var slots = bed.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).Where(m => m != null).Select(m => m.name).Distinct().ToList();
                Assert.That(slots, Does.Contain("bb_mat_p6_hoh_duvet").And.Contain("bb_mat_p6_hoh_linen"), "A cream duvet and headboard, cream weave on the linen.");
                Assert.That(slots, Does.Not.Contain("bb_mat_linen_white").And.Not.Contain("bb_mat_velvet_teal"), "and none of the plain bed left.");
                var throwOver = renderers.Single(r => r.name == "Bed throw");
                var bb = BoundsOf(bed);
                Assert.That(throwOver.bounds.center.x, Is.EqualTo(bb.center.x).Within(0.05f), "The throw lies across the bed,");
                Assert.That(throwOver.bounds.center.z, Is.InRange(bb.min.z, bb.max.z), "at its foot.");

                var bands = renderers.Where(r => r.name.StartsWith("Band · ", StringComparison.Ordinal)).ToList();
                Assert.That(bands.Select(b => b.name), Is.EquivalentTo(new[] { "Band · South wing link left", "Band · South wing west wall" }));
                foreach (var band in bands)
                    Assert.That(band.bounds.max.y, Is.LessThanOrEqualTo(floor.max.y + 1.1f + 0.01f), band.name + " stops at the south wing's 1.1 m wall.");
                Assert.That(renderers.Count(r => r.name == "Crown"), Is.EqualTo(1), "The crown beside the bed.");

                var rug = renderers.Single(r => r.name == "HoH rug");
                Assert.That(rug.bounds.size.x, Is.EqualTo(3.6f).Within(0.05f));
                Assert.That(floor.Contains(new Vector3(rug.bounds.center.x, floor.center.y, rug.bounds.center.z)), Is.True, "The rug is in the suite.");

                var console = all.Single(t => t.name == "cabinetTelevision" && floor.Contains(new Vector3(t.position.x, floor.center.y, t.position.z)));
                var cb = BoundsOf(console);
                Assert.That(renderers.Count(r => r.name == "Reward plaque"), Is.EqualTo(1));
                Assert.That(renderers.Count(r => r.name == "HoH photo"), Is.EqualTo(4), "Four photo slots,");
                Assert.That(renderers.Count(r => r.name == "HoH photo frame"), Is.EqualTo(4), "each in a gold frame.");
                foreach (var onTheWall in renderers.Where(r => r.name == "Reward plaque" || r.name == "HoH photo"))
                {
                    Assert.That(onTheWall.bounds.center.z, Is.LessThan(floor.min.z + 0.3f), onTheWall.name + " is on the south wall.");
                    Assert.That(onTheWall.bounds.max.y, Is.LessThanOrEqualTo(floor.max.y + 1.1f + 0.01f), onTheWall.name + " is under the wall's top.");
                }
                var onTheConsole = renderers.Where(r => r.name == "HoH envelope" || r.name == "HoH letter" || r.name == "Gift box" || r.name == "Snack bag" || r.name == "HoH drink").ToList();
                Assert.That(onTheConsole.Count, Is.EqualTo(6), "The letter, its envelope, the gift, two snacks and the drink.");
                foreach (var thing in onTheConsole)
                {
                    Assert.That(thing.bounds.min.y, Is.EqualTo(floor.max.y + 0.45f).Within(0.02f), thing.name + " stands on the console.");
                    Assert.That(thing.bounds.center.x, Is.InRange(cb.min.x, cb.max.x), thing.name + " is on the console.");
                }
                Assert.That(renderers.Count(r => r.name == "Mini fridge"), Is.EqualTo(1));
                var neon = renderers.Single(r => r.name == "HoH neon");
                Assert.That(neon.sharedMaterial.renderQueue, Is.GreaterThanOrEqualTo((int)RenderQueue.Transparent), "The HoH neon is lit.");
                Assert.That(neon.bounds.center.x, Is.GreaterThan(floor.max.x - 0.3f), "by the door on the east divider.");
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [Test]
        public void TheNominationRoomFramesItsScreenRingsItsFloorAndCarriesTheIdleDisplay()
        {
            var scene = EditorSceneManager.OpenPreviewScene(EpisodeScene);
            try
            {
                var all = scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<Transform>(true)).ToArray();
                var floor = all.First(t => t.name == "Nomination floor").GetComponent<Renderer>().bounds;
                var root = Root(scene);
                var nomination = root.Find("Nomination room");
                Assert.That(nomination, Is.Not.Null);
                var renderers = nomination.GetComponentsInChildren<Renderer>(true);
                Bounds BoundsOf(Transform t) => t.GetComponentsInChildren<Renderer>(true).Select(r => r.bounds).Aggregate((a, b) => { a.Encapsulate(b); return a; });

                var screen = all.Single(t => t.name == "bb_set_ceremonyscreen");
                var face = Gamesim.Presentation.ScreenSurface.Measure(screen, "Nomination", floor.center);
                Assert.That(face, Is.Not.Null);
                var frame = renderers.Single(r => r.name == "Ceremony frame");
                Assert.That(frame.bounds.size.x, Is.GreaterThan(face.Width + 0.3f), "The gold linework is wider than the board, so it shows round its edge,");
                Assert.That(Vector3.Dot(frame.bounds.center - face.Centre, face.Normal), Is.LessThan(0f), "and stands behind the face.");
                Assert.That(frame.sharedMaterial.renderQueue, Is.GreaterThanOrEqualTo((int)RenderQueue.Transparent), "lit.");
                var idle = screen.GetComponentsInChildren<Renderer>(true).Single(r => r.name == Gamesim.Presentation.ScreenSurface.IdleDisplayName);
                Assert.That(idle.enabled, Is.True, "The idle display shows when no card does.");
                Assert.That(Vector3.Dot(idle.bounds.center - face.Centre, face.Normal), Is.InRange(0.001f, 0.012f), "It sits on the face, inside a card's stand-off.");
                Assert.That(idle.bounds.size.x, Is.LessThan(face.Width), "and inside the board's width.");
                Assert.That(renderers.Count(r => r.name == "Stage ornament"), Is.EqualTo(1));
                var stageSlots = screen.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).Where(m => m != null).Select(m => m.name).ToList();
                Assert.That(stageSlots, Does.Contain("bb_mat_p6_nomination_stage_marble"), "The stage is dark marble.");

                var table = all.Single(t => t.name == "tableRound" && floor.Contains(new Vector3(t.position.x, floor.center.y, t.position.z)));
                var tb = BoundsOf(table);
                var ring = renderers.Single(r => r.name == "Ceremony ring");
                Assert.That(Vector2.Distance(new Vector2(ring.bounds.center.x, ring.bounds.center.z), new Vector2(tb.center.x, tb.center.z)), Is.LessThan(0.1f), "The ring is centred on the table.");
                Assert.That(ring.bounds.size.x, Is.EqualTo(4.6f).Within(0.05f));
                Assert.That(renderers.Count(r => r.name == "Wall emblem"), Is.EqualTo(2), "An emblem on each divider.");
                Assert.That(renderers.Count(r => r.name == "Band · South wing south wall"), Is.EqualTo(1), "The presentation wall behind the board.");
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [Test]
        public void TheGameRoomGetsOneMarqueeItsBoxesRugPostersAndFridge()
        {
            var scene = EditorSceneManager.OpenPreviewScene(EpisodeScene);
            try
            {
                var all = scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<Transform>(true)).ToArray();
                var floor = all.First(t => t.name == "Games floor").GetComponent<Renderer>().bounds;
                var games = Root(scene).Find("Game room");
                Assert.That(games, Is.Not.Null);
                var renderers = games.GetComponentsInChildren<Renderer>(true);
                Assert.That(renderers.Count(r => r.name == "Arcade marquee"), Is.EqualTo(1), "One neon focal piece, not six.");
                Assert.That(renderers.Count(r => r.sharedMaterial != null && r.sharedMaterial.renderQueue >= (int)RenderQueue.Transparent), Is.EqualTo(1), "and nothing else in the room glows.");
                Assert.That(renderers.Count(r => r.name == "Game box"), Is.EqualTo(3), "Three game boxes on the coffee table,");
                var boxes = renderers.Where(r => r.name == "Game box").OrderBy(r => r.bounds.min.y).ToList();
                Assert.That(boxes[1].bounds.min.y, Is.EqualTo(boxes[0].bounds.max.y).Within(0.01f), "stacked.");
                Assert.That(renderers.Count(r => r.name == "Game poster"), Is.EqualTo(2), "Two posters,");
                Assert.That(renderers.Count(r => r.name == "Game poster frame"), Is.EqualTo(2), "framed.");
                Assert.That(renderers.Count(r => r.name == "Trophy plaque"), Is.EqualTo(1));
                Assert.That(renderers.Count(r => r.name == "Mini fridge"), Is.EqualTo(1));
                Assert.That(renderers.Count(r => r.name == "Game room pattern"), Is.EqualTo(1));
                var rug = renderers.Single(r => r.name == "Game rug");
                Assert.That(floor.Contains(new Vector3(rug.bounds.center.x, floor.center.y, rug.bounds.center.z)), Is.True);
                Assert.That(renderers.Count(r => r.name.StartsWith("Band · South wing divider 2", StringComparison.Ordinal)), Is.EqualTo(2), "Charcoal geometric on the west divider.");
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [Test]
        public void TheBedroomGivesEveryBedItsOwnDuvetAndAStorageBoxAtItsFoot()
        {
            var scene = EditorSceneManager.OpenPreviewScene(EpisodeScene);
            try
            {
                var all = scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<Transform>(true)).ToArray();
                var floor = all.First(t => t.name == "Bedroom floor").GetComponent<Renderer>().bounds;
                var bedroom = Root(scene).Find("Bedroom");
                Assert.That(bedroom, Is.Not.Null);
                var renderers = bedroom.GetComponentsInChildren<Renderer>(true);
                bool InRoom(Transform t) => floor.Contains(new Vector3(t.position.x, floor.center.y, t.position.z));
                var singles = all.Where(t => t.name == "bedSingle" && InRoom(t)).ToList();
                Assert.That(singles.Count, Is.EqualTo(3), "Three singles.");
                var duvets = singles.Select(b => b.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials)
                    .First(m => m != null && m.name.StartsWith("bb_mat_p6_duvet_", StringComparison.Ordinal)).name).ToList();
                Assert.That(duvets.Distinct().Count(), Is.EqualTo(3), "each in its own duvet: " + string.Join(", ", duvets));
                var bunks = all.Where(t => t.name == "bedBunk" && InRoom(t)).ToList();
                Assert.That(bunks.Count, Is.EqualTo(2));
                foreach (var bunk in bunks)
                {
                    var names = bunk.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).Where(m => m != null).Select(m => m.name).ToList();
                    Assert.That(names.Count(n => n.StartsWith("bb_mat_p6_duvet_", StringComparison.Ordinal)), Is.EqualTo(2), "a bunk's two beds in two duvets");
                    Assert.That(names, Does.Not.Contain("bb_mat_velvet_teal").And.Not.Contain("bb_mat_cushion_coral"));
                }
                Assert.That(renderers.Count(r => r.name == "Storage box"), Is.EqualTo(3), "A storage box at every single's foot,");
                Assert.That(renderers.Count(r => r.name == "Shoe box"), Is.EqualTo(2), "a shoe box at each bunk's.");
                foreach (var box in renderers.Where(r => r.name == "Storage box"))
                    Assert.That(singles.Any(b => Mathf.Abs(b.position.x - box.bounds.center.x) < 0.3f), Is.True, "each box under its bed's line.");
                var rug = renderers.Single(r => r.name == "Bedroom rug");
                Assert.That(rug.bounds.size.x, Is.EqualTo(5.2f).Within(0.05f), "One big rug joins the beds.");
                Assert.That(renderers.Count(r => r.name == "Laundry basket"), Is.EqualTo(1));
                Assert.That(renderers.Count(r => r.name == "Suitcase"), Is.EqualTo(1));
                Assert.That(renderers.Count(r => r.name == "Book"), Is.EqualTo(2));
                Assert.That(renderers.Count(r => r.name == "Toiletry"), Is.EqualTo(3), "toiletries on the three drawers");
                Assert.That(renderers.Count(r => r.name == "Corkboard"), Is.EqualTo(1));
                Assert.That(renderers.Count(r => r.name == "Polaroid"), Is.EqualTo(3));
                Assert.That(renderers.Count(r => r.name == "Framed print"), Is.EqualTo(2));
                Assert.That(renderers.Count(r => r.name == "Magazine"), Is.EqualTo(1));
                Assert.That(renderers.Select(r => r.name).Where(n => n.StartsWith("Band · ", StringComparison.Ordinal)),
                    Is.EquivalentTo(new[] { "Band · House / yard left", "Band · West wall" }), "Blue behind the singles, rose behind the bunks.");
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [Test]
        public void TheYardGetsItsDisplayItsLinesAndItsCasesAndKeepsItsCentreOpen()
        {
            var scene = EditorSceneManager.OpenPreviewScene(EpisodeScene);
            try
            {
                var all = scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<Transform>(true)).ToArray();
                var floor = all.First(t => t.name == "Competition yard floor").GetComponent<Renderer>().bounds;
                var yard = Root(scene).Find("Competition yard");
                Assert.That(yard, Is.Not.Null);
                var renderers = yard.GetComponentsInChildren<Renderer>(true);
                var display = renderers.Single(r => r.name == "Yard display");
                Assert.That(display.bounds.center.z, Is.GreaterThan(floor.max.z - 1.5f), "The display is on the backdrop at the head of the yard.");
                Assert.That(renderers.Count(r => r.name == "Start line"), Is.EqualTo(3), "A start line per lane,");
                Assert.That(renderers.Count(r => r.name == "Finish line"), Is.EqualTo(3), "a finish line per lane.");
                foreach (var line in renderers.Where(r => r.name == "Start line" || r.name == "Finish line"))
                    Assert.That(line.bounds.max.y, Is.LessThan(floor.max.y + 0.05f), line.name + " lies on the floor.");
                Assert.That(renderers.Count(r => r.name == "Flight case"), Is.EqualTo(3), "Three cases by the west fence,");
                foreach (var thing in renderers.Where(r => r.name == "Flight case"))
                    Assert.That(thing.bounds.center.x, Is.LessThan(floor.min.x + 3f), "at the edge, never in the middle.");
                // The centre stays open: nothing the yard's finish placed stands within four metres of its middle.
                var middle = new Vector2(floor.center.x, floor.center.z);
                foreach (var renderer in renderers.Where(r => r.name != "Start line" && r.name != "Finish line"))
                    Assert.That(Vector2.Distance(new Vector2(renderer.bounds.center.x, renderer.bounds.center.z), middle), Is.GreaterThan(4f), renderer.name + " crowds the yard's centre.");
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        /// <summary>Pass 8: the perimeter neon inside each room wears a dimmed copy of its material; the yard keeps its own.</summary>
        [Test]
        public void ThePerimeterNeonComesDownRoomByRoomAndTheYardKeepsItsOwn()
        {
            var scene = EditorSceneManager.OpenPreviewScene(EpisodeScene);
            try
            {
                var world = scene.GetRootGameObjects().First(go => go.name == "House Architecture").transform;
                var root = Root(scene);
                // The perimeter neon: the prototype's transplanted trim and the south wing's own strips.
                var trim = world.GetComponentsInChildren<Renderer>(true)
                    .Where(r => !r.transform.IsChildOf(root) && r.sharedMaterial != null
                        && (r.sharedMaterial.name.StartsWith("Neon ", StringComparison.Ordinal) || r.sharedMaterial.name.StartsWith("bb_mat_p6_dim_neon", StringComparison.Ordinal)))
                    .ToList();
                Assert.That(trim.Count, Is.GreaterThan(50), "Precondition: the house has its neon trim.");
                foreach (var (floorName, factor) in HouseRoomFinish.NeonBalance)
                {
                    var floor = world.GetComponentsInChildren<Transform>(true).First(t => t.name == floorName).GetComponent<Renderer>().bounds;
                    var inside = trim.Where(r => r.bounds.center.x > floor.min.x - 0.15f && r.bounds.center.x < floor.max.x + 0.15f
                        && r.bounds.center.z > floor.min.z - 0.15f && r.bounds.center.z < floor.max.z + 0.15f).ToList();
                    Assert.That(inside, Is.Not.Empty, floorName + " has trim to dim.");
                    foreach (var piece in inside)
                    {
                        Assert.That(piece.sharedMaterial.name, Does.StartWith("bb_mat_p6_dim_neon").And.EndWith("_" + Mathf.RoundToInt(factor * 100f)),
                            floorName + ": " + piece.name + " wears " + piece.sharedMaterial.name);
                        Assert.That(AssetDatabase.GetAssetPath(piece.sharedMaterial), Does.StartWith(HouseRoomFinish.MaterialFolder + "/"));
                    }
                }
                var yard = world.GetComponentsInChildren<Transform>(true).First(t => t.name == "Competition yard floor").GetComponent<Renderer>().bounds;
                var yardTrim = trim.Where(r => r.bounds.center.z > yard.min.z + 0.5f).ToList();
                Assert.That(yardTrim.All(r => r.sharedMaterial.name.StartsWith("Neon ", StringComparison.Ordinal)), Is.True, "The yard's neon is untouched: it is the competition's own.");
                var dim = AssetDatabase.LoadAssetAtPath<Material>(HouseRoomFinish.MaterialFolder + "/bb_mat_p6_dim_neon_gold_50.mat");
                var full = AssetDatabase.LoadAssetAtPath<Material>("Assets/Gamesim/Art/Prototype/Emissive/Neon Gold.mat");
                Assert.That(dim, Is.Not.Null); Assert.That(full, Is.Not.Null);
                Assert.That(dim.GetColor("_EmissionColor").maxColorComponent, Is.EqualTo(full.GetColor("_EmissionColor").maxColorComponent * 0.5f).Within(0.01f),
                    "Half the emission, the same colour.");
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        /// <summary>The pass is idempotent: run on the saved scene it rebuilds the same root.</summary>
        [Test]
        public void RunningThePassAgainRebuildsTheSameRoot()
        {
            // Apply(scene) edits persistent shared materials as well as this preview's geometry.
            // Closing the preview does not undo those assets: a dirty material is saved on exit.
            var materials = OwnedMaterialState.Capture();
            var scene = default(UnityEngine.SceneManagement.Scene);
            try
            {
                scene = EditorSceneManager.OpenPreviewScene(EpisodeScene);
                var before = Root(scene).GetComponentsInChildren<Renderer>(true)
                    .Select(r => r.name + "@" + r.bounds.center.ToString("F2") + "|" + r.bounds.size.ToString("F2") + "|" + r.sharedMaterial.name)
                    .OrderBy(s => s, StringComparer.Ordinal).ToArray();
                HouseRoomFinish.Apply(scene);
                var after = Root(scene).GetComponentsInChildren<Renderer>(true)
                    .Select(r => r.name + "@" + r.bounds.center.ToString("F2") + "|" + r.bounds.size.ToString("F2") + "|" + r.sharedMaterial.name)
                    .OrderBy(s => s, StringComparer.Ordinal).ToArray();
                Assert.That(after, Is.EqualTo(before), "A second run places what the first did, where it did, in the same materials.");
                Assert.That(materials.AnySerializedStateChanged(), Is.True,
                    "Precondition: the real authoring pass changed an existing material, so this case exercises asset restoration.");
            }
            finally
            {
                try { materials.RestoreAndAssertUnchanged(); }
                finally { if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene); }
            }
        }

        private sealed class OwnedMaterialState
        {
            private readonly List<MaterialState> originals = new List<MaterialState>();
            private readonly HashSet<string> paths = new HashSet<string>(StringComparer.Ordinal);

            public static OwnedMaterialState Capture()
            {
                Assert.That(AssetDatabase.IsValidFolder(HouseRoomFinish.MaterialFolder), Is.True,
                    "This saved-scene test needs the existing owned material folder.");
                var state = new OwnedMaterialState();
                try
                {
                    foreach (string path in MaterialPaths())
                    {
                        state.originals.Add(new MaterialState(path));
                        state.paths.Add(path);
                    }
                    Assert.That(state.originals, Is.Not.Empty);
                    return state;
                }
                catch
                {
                    state.DestroyCopies();
                    throw;
                }
            }

            public bool AnySerializedStateChanged() => originals.Any(s => EditorJsonUtility.ToJson(s.Material) != s.Serialized);

            public void RestoreAndAssertUnchanged()
            {
                var failures = new List<string>();
                try
                {
                    // A future pass may add a material. Remove only .mat assets in this exact
                    // owned folder that did not exist before this synchronous test began.
                    try
                    {
                        foreach (string path in MaterialPaths().Where(p => !paths.Contains(p)))
                        {
                            try
                            {
                                if (!AssetDatabase.DeleteAsset(path)) failures.Add("Could not remove test-created material " + path);
                                else failures.Add("The saved-scene re-run unexpectedly created " + path);
                            }
                            catch (Exception error) { failures.Add(path + ": " + error); }
                        }
                    }
                    catch (Exception error) { failures.Add("Could not enumerate test-created materials: " + error); }
                    foreach (var state in originals)
                    {
                        try { state.Restore(failures); }
                        catch (Exception error) { failures.Add(state.Path + ": " + error); }
                    }
                }
                finally { DestroyCopies(); }
                Assert.That(failures, Is.Empty, "The re-run must leave the original material assets and their dirty state unchanged.");
            }

            private void DestroyCopies()
            {
                foreach (var state in originals)
                    if (state.Copy != null) UnityEngine.Object.DestroyImmediate(state.Copy);
            }

            private static string[] MaterialPaths() => AssetDatabase.FindAssets("t:Material", new[] { HouseRoomFinish.MaterialFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => p.StartsWith(HouseRoomFinish.MaterialFolder + "/", StringComparison.Ordinal)
                    && p.EndsWith(".mat", StringComparison.Ordinal))
                .Distinct(StringComparer.Ordinal).OrderBy(p => p, StringComparer.Ordinal).ToArray();

            private sealed class MaterialState
            {
                public readonly string Path, Serialized;
                public readonly Material Material, Copy;
                private readonly HideFlags hideFlags;
                private readonly bool wasDirty;
                private readonly string[] keywords;
                private readonly Color baseColor, emissionColor;
                private readonly float smoothness;
                private readonly byte[] bytes, metaBytes;
                private readonly string absolutePath, guid;

                public MaterialState(string path)
                {
                    Path = path;
                    absolutePath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Application.dataPath), path);
                    Material = AssetDatabase.LoadAssetAtPath<Material>(path);
                    Assert.That(Material, Is.Not.Null, path);
                    guid = AssetDatabase.AssetPathToGUID(path);
                    bytes = File.ReadAllBytes(absolutePath);
                    metaBytes = File.ReadAllBytes(absolutePath + ".meta");
                    Serialized = EditorJsonUtility.ToJson(Material);
                    hideFlags = Material.hideFlags;
                    wasDirty = EditorUtility.IsDirty(Material);
                    keywords = Material.shaderKeywords.ToArray();
                    baseColor = Material.HasProperty("_BaseColor") ? Material.GetColor("_BaseColor") : default;
                    emissionColor = Material.HasProperty("_EmissionColor") ? Material.GetColor("_EmissionColor") : default;
                    smoothness = Material.HasProperty("_Smoothness") ? Material.GetFloat("_Smoothness") : 0f;
                    Copy = new Material(Material);
                    try
                    {
                        EditorUtility.CopySerialized(Material, Copy);
                        Copy.hideFlags = HideFlags.HideAndDontSave;
                    }
                    catch
                    {
                        UnityEngine.Object.DestroyImmediate(Copy);
                        throw;
                    }
                }

                public void Restore(List<string> failures)
                {
                    // Preserve the object referenced by the AssetDatabase and scene renderers.
                    // Restoring file bytes alone would leave the edited, dirty object to flush.
                    EditorUtility.CopySerialized(Copy, Material);
                    Material.hideFlags = hideFlags;
                    if (wasDirty) EditorUtility.SetDirty(Material);
                    else EditorUtility.ClearDirty(Material);
                    RestoreBytes(absolutePath, bytes, failures);
                    RestoreBytes(absolutePath + ".meta", metaBytes, failures);
                    if (EditorJsonUtility.ToJson(Material) != Serialized) failures.Add(Path + ": serialized material state changed");
                    if (!Material.shaderKeywords.SequenceEqual(keywords)) failures.Add(Path + ": shader keywords changed");
                    if (Material.HasProperty("_BaseColor") && Material.GetColor("_BaseColor") != baseColor) failures.Add(Path + ": base colour changed");
                    if (Material.HasProperty("_EmissionColor") && Material.GetColor("_EmissionColor") != emissionColor) failures.Add(Path + ": emission colour changed");
                    if (Material.HasProperty("_Smoothness") && Material.GetFloat("_Smoothness") != smoothness) failures.Add(Path + ": smoothness changed");
                    if (EditorUtility.IsDirty(Material) != wasDirty) failures.Add(Path + ": original dirty flag changed");
                    if (AssetDatabase.AssetPathToGUID(Path) != guid) failures.Add(Path + ": asset GUID changed");
                }

                private static void RestoreBytes(string path, byte[] original, List<string> failures)
                {
                    if (File.Exists(path) && File.ReadAllBytes(path).SequenceEqual(original)) return;
                    failures.Add(path + ": authoring changed original file bytes");
                    File.WriteAllBytes(path, original);
                    if (!File.ReadAllBytes(path).SequenceEqual(original)) failures.Add(path + ": original file bytes were not restored");
                }
            }
        }

        private static Transform Root(UnityEngine.SceneManagement.Scene scene)
        {
            var world = scene.GetRootGameObjects().FirstOrDefault(go => go.name == "House Architecture");
            return world == null ? null : world.transform.Find(HouseRoomFinish.RootName);
        }
    }
}
