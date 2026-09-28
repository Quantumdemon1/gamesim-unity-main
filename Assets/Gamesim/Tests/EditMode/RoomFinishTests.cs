using System;
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

        [Test]
        public void TheLivingRoomGetsItsFeatureWallRugClusterAndPrints()
        {
            var scene = EditorSceneManager.OpenPreviewScene(EpisodeScene);
            try
            {
                var all = scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<Transform>(true)).ToArray();
                var floor = all.First(t => t.name == "Living room floor").GetComponent<Renderer>().bounds;
                var root = Root(scene);
                var living = root.Find("Living room");
                Assert.That(living, Is.Not.Null);
                var renderers = living.GetComponentsInChildren<Renderer>(true);
                Bounds BoundsOf(Transform t) => t.GetComponentsInChildren<Renderer>(true).Select(r => r.bounds).Aggregate((a, b) => { a.Encapsulate(b); return a; });

                // The feature wall: oak on both segments of the north partition, a strip either side of the screen's place, the mark.
                var bands = renderers.Where(r => r.name.StartsWith("Band · Living / bedroom", StringComparison.Ordinal)).ToList();
                Assert.That(bands.Count, Is.EqualTo(2), "Oak slats on both segments of the north partition.");
                foreach (var band in bands)
                {
                    Assert.That(AssetDatabase.GetAssetPath(band.sharedMaterial.GetTexture("_BaseMap")), Does.EndWith("wall_warm_oak_slats.png"), band.name);
                    Assert.That(band.bounds.center.z, Is.GreaterThan(floor.max.z - 0.3f), band.name + " is on the north wall.");
                }
                var strips = renderers.Where(r => r.name == "Feature strip").ToList();
                Assert.That(strips.Count, Is.EqualTo(2), "A lit strip either side of the screen's place.");
                var television = all.First(t => t.name == "Television");
                Assert.That(strips.Min(s => s.bounds.center.x), Is.LessThan(television.position.x), "one to its left,");
                Assert.That(strips.Max(s => s.bounds.center.x), Is.GreaterThan(television.position.x), "one to its right,");
                Assert.That(strips.All(s => s.sharedMaterial.renderQueue >= (int)RenderQueue.Transparent), Is.True, "both lit.");
                Assert.That(renderers.Count(r => r.name == "House mark"), Is.EqualTo(1), "The house mark, once.");

                // The rug binds the sofa and the coffee table.
                var sofa = all.Single(t => t.name == "loungeDesignSofa" && floor.Contains(new Vector3(t.position.x, floor.center.y, t.position.z)));
                var table = all.Single(t => t.name == "Coffee table (model)");
                var sb = BoundsOf(sofa); var tb = BoundsOf(table);
                var rug = renderers.Single(r => r.name == "Living rug");
                var between = (new Vector2(sb.center.x, sb.center.z) + new Vector2(tb.center.x, tb.center.z)) * 0.5f;
                Assert.That(Vector2.Distance(new Vector2(rug.bounds.center.x, rug.bounds.center.z), between), Is.LessThan(0.1f), "The rug is centred between the sofa and the table.");
                Assert.That(rug.bounds.size.x, Is.EqualTo(4.4f).Within(0.05f));
                var oldRugs = all.Where(t => !t.IsChildOf(root) && t.name.IndexOf("rug", StringComparison.OrdinalIgnoreCase) >= 0
                        && floor.Contains(new Vector3(t.position.x, floor.center.y, t.position.z)))
                    .Where(t => { var b = BoundsOf(t); return b.Intersects(rug.bounds); }).ToList();
                Assert.That(oldRugs.SelectMany(t => t.GetComponentsInChildren<Renderer>(true)).All(r => !r.enabled), Is.True,
                    "The flat rugs under the new one are switched off, not stacked.");

                // The table's cluster stands on its top.
                foreach (var piece in new[] { "bb_set_bookstack", "bb_set_candle", "bb_set_tray" })
                {
                    var thing = living.GetComponentsInChildren<Transform>(true).Single(t => t.name == piece);
                    var b = BoundsOf(thing);
                    Assert.That(b.min.y, Is.EqualTo(tb.max.y).Within(0.03f), piece + " stands on the coffee table.");
                    Assert.That(b.center.x, Is.InRange(tb.min.x - 0.3f, tb.max.x + 0.3f), piece + " is on the table.");
                    Assert.That(b.center.z, Is.InRange(tb.min.z - 0.3f, tb.max.z + 0.3f), piece + " is on the table.");
                }

                // Two framed prints on the 1.1 m south wall, under its top.
                var prints = renderers.Where(r => r.name == "Framed print").ToList();
                Assert.That(prints.Count, Is.EqualTo(2), "Two prints.");
                Assert.That(renderers.Count(r => r.name == "Framed print frame"), Is.EqualTo(2), "each in its frame.");
                foreach (var print in prints)
                {
                    Assert.That(print.bounds.center.z, Is.LessThan(floor.min.z + 0.3f), "on the south wall,");
                    Assert.That(print.bounds.max.y, Is.LessThanOrEqualTo(floor.max.y + 1.1f + 0.01f), "under the wall's top.");
                }
                Assert.That(living.GetComponentsInChildren<Transform>(true).Count(t => t.name == "bb_set_ph_plant"), Is.EqualTo(1), "A plant in the corner.");
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

        /// <summary>The pass is idempotent: run on the saved scene it rebuilds the same root.</summary>
        [Test]
        public void RunningThePassAgainRebuildsTheSameRoot()
        {
            var scene = EditorSceneManager.OpenPreviewScene(EpisodeScene);
            try
            {
                var before = Root(scene).GetComponentsInChildren<Renderer>(true)
                    .Select(r => r.name + "@" + r.bounds.center.ToString("F2") + "|" + r.bounds.size.ToString("F2") + "|" + r.sharedMaterial.name)
                    .OrderBy(s => s, StringComparer.Ordinal).ToArray();
                HouseRoomFinish.Apply(scene);
                var after = Root(scene).GetComponentsInChildren<Renderer>(true)
                    .Select(r => r.name + "@" + r.bounds.center.ToString("F2") + "|" + r.bounds.size.ToString("F2") + "|" + r.sharedMaterial.name)
                    .OrderBy(s => s, StringComparer.Ordinal).ToArray();
                Assert.That(after, Is.EqualTo(before), "A second run places what the first did, where it did, in the same materials.");
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        private static Transform Root(UnityEngine.SceneManagement.Scene scene)
        {
            var world = scene.GetRootGameObjects().FirstOrDefault(go => go.name == "House Architecture");
            return world == null ? null : world.transform.Find(HouseRoomFinish.RootName);
        }
    }
}
