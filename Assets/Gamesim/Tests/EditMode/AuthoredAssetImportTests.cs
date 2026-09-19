using System.IO;
using System.Linq;
using Gamesim.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The contract between the Blender side and the Unity side of the authored-asset pipeline
    /// (MASTER-PLAN §4.3 and §4.6), checked on the assets that actually ship.
    ///
    /// <para>Every model under <c>Art/Authored</c> imports with the one set of settings the
    /// importer fixes; the three yard set pieces stand on the floor at the size their scripts
    /// declare, in metres, with no import scale; a <c>_col</c> child became a collider and
    /// stopped rendering; and the water is translucent. A convention that is not tested is a
    /// convention that drifts.</para>
    /// </summary>
    public sealed class AuthoredAssetImportTests
    {
        private const string SetPieces = AuthoredAssetImporter.Root + "SetPieces/";

        private static string[] AuthoredModels() => AssetDatabase.FindAssets("t:Model", new[] { AuthoredAssetImporter.Root.TrimEnd('/') })
            .Select(AssetDatabase.GUIDToAssetPath).Where(path => path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path, System.StringComparer.Ordinal).ToArray();

        [Test]
        public void EveryAuthoredModelImportsUnderTheFixedSettings()
        {
            var models = AuthoredModels();
            Assert.That(models, Is.Not.Empty, "Part 4 has begun; there should be authored models.");
            foreach (var path in models)
            {
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                Assert.That(importer, Is.Not.Null, path);
                Assert.That(importer.globalScale, Is.EqualTo(1f), path + ": no import scale; the export is in metres.");
                Assert.That(importer.useFileScale, Is.True, path);
                Assert.That(importer.bakeAxisConversion, Is.True, path + ": the axis conversion is baked once, here.");
                Assert.That(importer.addCollider, Is.False, path + ": colliders come only from _col meshes.");
                Assert.That(importer.isReadable, Is.False, path);
                Assert.That(importer.animationType, Is.EqualTo(AuthoredAssetImporter.IsGenericAnimation(path) ? ModelImporterAnimationType.Generic
                    : AuthoredAssetImporter.IsRigged(path) ? ModelImporterAnimationType.Human : ModelImporterAnimationType.None), path);
                Assert.That(importer.importAnimation, Is.EqualTo(AuthoredAssetImporter.IsAnimation(path)), path);
                Assert.That(importer.materialLocation, Is.EqualTo(ModelImporterMaterialLocation.External),
                    path + ": materials are extracted beside the model so a re-export keeps them.");
            }
        }

        [Test]
        public void EveryAuthoredModelStandsOnTheFloorWithABbName()
        {
            foreach (var path in AuthoredModels())
            {
                var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.That(root, Is.Not.Null, path);
                Assert.That(root.name, Does.StartWith("bb_"), path + ": the file and the root share the bb_ name.");
                // A clip file is an armature and its takes: nothing to render, nothing to stand.
                if (AuthoredAssetImporter.IsAnimation(path)) continue;
                var bounds = RenderBounds(root);
                // The memory wall hangs on a wall; its origin is the floor beneath it, and nothing of
                // it touches the floor. Everything else stands.
                if (root.name != "bb_set_memorywall")
                    Assert.That(bounds.min.y, Is.EqualTo(0f).Within(0.01f), path + ": the lowest point is the floor.");
                // A prop is centred on its origin so a plan can place it by its middle. The shell is
                // not a prop: it is the house, laid over the colliders at the world origin.
                if (path.StartsWith(AuthoredAssetImporter.Root + "Shell/", System.StringComparison.Ordinal)) continue;
                // The HoH door stands open: its leaf swings a metre into the suite, so its middle is
                // not its jambs. The plan places it by its bounds and says so.
                if (root.name == "bb_set_hohdoor") continue;
                Assert.That(bounds.center.x, Is.EqualTo(0f).Within(0.05f), path + ": centred on its origin in x.");
                Assert.That(bounds.center.z, Is.EqualTo(0f).Within(0.05f), path + ": centred on its origin in z.");
            }
        }

        [Test]
        public void ThePoolIsSevenByFourPointFourMetresAndOwnsItsCollider()
        {
            var pool = AssetDatabase.LoadAssetAtPath<GameObject>(SetPieces + "bb_set_pool.fbx");
            Assert.That(pool, Is.Not.Null);
            var bounds = RenderBounds(pool);
            Assert.That(bounds.size.x, Is.EqualTo(7.0f).Within(0.05f), "deck width");
            Assert.That(bounds.size.z, Is.EqualTo(4.4f).Within(0.05f), "deck depth");
            Assert.That(bounds.size.y, Is.EqualTo(0.39f).Within(0.03f), "deck plus coping height");

            var collider = pool.GetComponentsInChildren<MeshCollider>(true).Single();
            Assert.That(collider.name, Is.EqualTo("bb_set_pool_col"));
            Assert.That(collider.convex, Is.True);
            Assert.That(collider.GetComponent<Renderer>(), Is.Null, "A collision mesh does not render.");
            Assert.That(pool.GetComponentsInChildren<Collider>(true), Has.Length.EqualTo(1),
                "Only the _col mesh carries collision; the deck and the water do not.");

            var water = pool.GetComponentsInChildren<Renderer>(true).Single(r => r.name == "bb_set_pool_water");
            Assert.That(water.sharedMaterial.name, Is.EqualTo("bb_mat_water"));
            Assert.That(water.bounds.max.y, Is.LessThan(bounds.size.y), "The water sits below the coping.");
        }

        [Test]
        public void TheHotTubAndLoungerAreTheSizeTheirScriptsDeclare()
        {
            var tub = RenderBounds(AssetDatabase.LoadAssetAtPath<GameObject>(SetPieces + "bb_set_hottub.fbx"));
            Assert.That(tub.size.x, Is.EqualTo(2.42f).Within(0.05f), "hot tub across, rim included");
            Assert.That(tub.size.y, Is.EqualTo(0.90f).Within(0.03f), "hot tub height, rim included");

            var lounger = AssetDatabase.LoadAssetAtPath<GameObject>(SetPieces + "bb_set_lounger.fbx");
            var bounds = RenderBounds(lounger);
            Assert.That(bounds.size.x, Is.EqualTo(0.65f).Within(0.05f), "lounger width");
            Assert.That(bounds.size.z, Is.EqualTo(1.9f).Within(0.1f), "lounger length");
            Assert.That(bounds.size.y, Is.InRange(0.6f, 1.0f), "lounger with its backrest raised");
            Assert.That(lounger.GetComponentsInChildren<Collider>(true), Is.Empty, "Furniture is collider-free by design.");
        }

        [Test]
        public void TheDiningTableSeatsSixteenAndTheChairFitsItsPitch()
        {
            var table = RenderBounds(AssetDatabase.LoadAssetAtPath<GameObject>(SetPieces + "bb_set_diningtable.fbx"));
            Assert.That(table.size.x, Is.EqualTo(4.8f).Within(0.05f), "table length: seven chairs a side at a 0.65 m pitch");
            Assert.That(table.size.z, Is.EqualTo(1.1f).Within(0.05f), "table width");
            Assert.That(table.size.y, Is.EqualTo(0.76f).Within(0.03f), "table height");

            var chair = RenderBounds(AssetDatabase.LoadAssetAtPath<GameObject>(SetPieces + "bb_set_diningchair.fbx"));
            Assert.That(chair.size.x, Is.LessThan(0.65f), "a chair must fit the pitch it is placed at");
            Assert.That(chair.size.y, Is.InRange(0.85f, 1.0f), "chair height with its back");
            Assert.That(chair.size.z, Is.InRange(0.45f, 0.6f), "chair depth, back raked");
        }

        [Test]
        public void TheDiaryChairStandsUnderThePrivateRoomWall()
        {
            var chair = RenderBounds(AssetDatabase.LoadAssetAtPath<GameObject>(SetPieces + "bb_set_diarychair.fbx"));
            Assert.That(chair.size.y, Is.InRange(1.2f, 1.5f), "tall enough to read as the chair, under the 1.5 m cutaway wall");
            Assert.That(chair.size.x, Is.InRange(0.9f, 1.15f), "chair width with the plinth");
        }

        [Test]
        public void TheHohBedStaysUnderTheSouthWingWall()
        {
            var bed = RenderBounds(AssetDatabase.LoadAssetAtPath<GameObject>(SetPieces + "bb_set_hohbed.fbx"));
            Assert.That(bed.size.y, Is.LessThan(1.1f), "the south wing's cutaway wall is 1.1 m; nothing in the room may top it");
            Assert.That(bed.size.x, Is.EqualTo(1.9f).Within(0.05f), "bed across");
            Assert.That(bed.size.z, Is.EqualTo(2.1f).Within(0.05f), "bed head to foot");
        }

        [Test]
        public void TheCompetitionRingsAndPodiumMatchThePrimitivesTheyReplace()
        {
            var rings = AssetDatabase.LoadAssetAtPath<GameObject>(SetPieces + "bb_set_compring.fbx");
            var circle = RenderBounds(rings);
            Assert.That(circle.size.x, Is.EqualTo(8.92f).Within(0.05f), "the outer ring's 4.4 m radius plus half the band");
            Assert.That(circle.size.z, Is.EqualTo(8.92f).Within(0.05f));
            Assert.That(circle.size.y, Is.EqualTo(0.05f).Within(0.01f), "the primitives' 5 cm lip");
            Assert.That(rings.GetComponentsInChildren<Collider>(true), Is.Empty, "a floor graphic; the segments never had collision either");

            var podium = RenderBounds(AssetDatabase.LoadAssetAtPath<GameObject>(SetPieces + "bb_set_podium.fbx"));
            Assert.That(podium.size.x, Is.EqualTo(2.33f).Within(0.05f), "the block's 2.2 m plus the plinth's overhang");
            Assert.That(podium.size.z, Is.EqualTo(1.46f).Within(0.05f), "the block's 1.3 m plus the plinth's overhang");
            Assert.That(podium.size.y, Is.InRange(1.05f, 1.15f), "counter and buzzer over a one-metre block");

            string gold = SetPieces + "Materials/bb_mat_neon_gold.mat";
            Assert.That(File.Exists(gold), Is.True, gold);
            var neon = AssetDatabase.LoadAssetAtPath<Material>(gold);
            Assert.That(neon.IsKeywordEnabled("_EMISSION"), Is.True, "a neon material glows on import");
            Assert.That(neon.GetColor("_EmissionColor").maxColorComponent, Is.GreaterThan(1.5f), "at the house's neon strength");
        }

        [Test]
        public void TheMemoryWallHangsSixteenFramesUnderTheCutawayWall()
        {
            var wall = AssetDatabase.LoadAssetAtPath<GameObject>(SetPieces + "bb_set_memorywall.fbx");
            var bounds = RenderBounds(wall);
            Assert.That(bounds.size.z, Is.EqualTo(4.72f).Within(0.05f), "eight frames of 0.53 at a 0.053 gap, plus the backing's margin");
            Assert.That(bounds.max.y, Is.LessThan(1.5f), "under the 1.5 m cutaway wall it hangs on");
            Assert.That(bounds.min.y, Is.GreaterThan(0.1f), "hung, not stood");
            Assert.That(bounds.size.x, Is.LessThan(0.12f), "a wall hanging is thin");
            var frames = wall.GetComponentsInChildren<Transform>(true)
                .Where(t => t.name.StartsWith("bb_set_memorywall_f", System.StringComparison.Ordinal)).ToArray();
            Assert.That(frames, Has.Length.EqualTo(16), "one frame per seat of the largest house");
            Assert.That(wall.GetComponentsInChildren<Collider>(true), Is.Empty, "decoration on a surface the player walks past");
        }

        [Test]
        public void TheHohDoorBasketAndHaveNotCotFitTheirRooms()
        {
            var door = RenderBounds(AssetDatabase.LoadAssetAtPath<GameObject>(SetPieces + "bb_set_hohdoor.fbx"));
            Assert.That(door.size.y, Is.InRange(1.05f, 1.12f), "the south wing's cutaway wall is 1.1 m; the door is its height, not taller");
            Assert.That(door.size.z, Is.EqualTo(3.16f).Within(0.05f), "the divider's 2.8 m gap plus a capped jamb each end");
            Assert.That(door.min.x, Is.LessThan(-0.85f), "the leaf stands open into the suite");
            Assert.That(door.max.x, Is.LessThan(0.2f), "and nothing reaches into the nomination room");

            var basket = RenderBounds(AssetDatabase.LoadAssetAtPath<GameObject>(SetPieces + "bb_set_hohbasket.fbx"));
            Assert.That(basket.size.x, Is.EqualTo(0.52f).Within(0.05f), "basket across, rim included");
            Assert.That(basket.size.y, Is.InRange(0.5f, 0.6f), "handle and bottle necks over a 0.24 m drum");

            var cot = RenderBounds(AssetDatabase.LoadAssetAtPath<GameObject>(SetPieces + "bb_set_havenot_cot.fbx"));
            Assert.That(cot.size.x, Is.EqualTo(0.9f).Within(0.02f), "cot width");
            Assert.That(cot.size.z, Is.EqualTo(1.9f).Within(0.02f), "cot length");
            Assert.That(cot.size.y, Is.InRange(0.45f, 0.55f), "the head rail is the tallest thing on it");
        }

        [Test]
        public void TheKitchenRunFitsTheNorthWallUnderTheCutaway()
        {
            var run = RenderBounds(AssetDatabase.LoadAssetAtPath<GameObject>(SetPieces + "bb_set_kitchenrun.fbx"));
            Assert.That(run.size.x, Is.EqualTo(6.4f).Within(0.05f), "fridge to end panel");
            Assert.That(run.size.y, Is.EqualTo(1.45f).Within(0.02f), "the fridge is the tallest thing on it, under the 1.5 m wall");
            Assert.That(run.size.z, Is.InRange(0.62f, 0.75f), "a counter's depth plus its handles");
        }

        [Test]
        public void TheStoolAndTheBinStandInForTheKitWhereverARoomHasOne()
        {
            var stool = RenderBounds(AssetDatabase.LoadAssetAtPath<GameObject>(SetPieces + "bb_set_barstool.fbx"));
            Assert.That(stool.size.y, Is.EqualTo(0.78f).Within(0.02f), "seat height, the height the plan's stool rows ask for");
            Assert.That(stool.size.x, Is.EqualTo(0.38f).Within(0.02f), "seat across");
            var bin = RenderBounds(AssetDatabase.LoadAssetAtPath<GameObject>(SetPieces + "bb_set_bin.fbx"));
            Assert.That(bin.size.y, Is.InRange(0.54f, 0.58f), "a pedal bin's height");
            Assert.That(bin.size.x, Is.EqualTo(0.31f).Within(0.02f), "drum with its lid");

            var replaced = HouseCatalogue.Replaced.ToDictionary(pair => pair.Key, pair => pair.Value);
            Assert.That(replaced["stoolBar"], Is.EqualTo("bb_set_barstool"));
            Assert.That(replaced["trashcan"], Is.EqualTo("bb_set_bin"));
            Assert.That(HouseCatalogue.Resolve("stoolBar", out var tier), Is.Not.Null);
            Assert.That(tier, Is.EqualTo(HouseCatalogue.Tier.Authored), "a kit id with an authored stand-in resolves to it");
            Assert.That(HouseCatalogue.Resolve("trashcan", out tier).name, Is.EqualTo("bb_set_bin"));
        }

        [Test]
        public void TheLivingRoomSetIsAuthoredAtTheHeightsItsRowsAskFor()
        {
            // (piece, x across, y tall, z deep), within 5 cm: the rows scale by height, so a piece
            // authored at its row's height lands at its own size.
            var expected = new[]
            {
                ("bb_set_sofa", 2.2f, 0.78f, 0.85f),
                ("bb_set_armchair", 0.9f, 0.88f, 0.85f),
                ("bb_set_floorlamp", 0.37f, 1.5f, 0.37f),
                ("bb_set_tablelamp", 0.26f, 0.45f, 0.26f),
                ("bb_set_bookcase", 0.8f, 1.45f, 0.32f),
                ("bb_set_sidetable", 0.53f, 0.62f, 0.43f),
                ("bb_set_coffeetable", 1.2f, 0.42f, 0.6f),
                ("bb_set_speaker", 0.24f, 0.95f, 0.24f),
                ("bb_set_rug", 1f, 0.01f, 1f),
                ("bb_set_rug_round", 1f, 0.01f, 1f),
                ("bb_set_bed", 0.95f, 0.85f, 2.0f),
                ("bb_set_bunk", 1.0f, 1.45f, 2.0f),
                ("bb_set_tvconsole", 1.42f, 1.10f, 0.47f),
                ("bb_set_desk", 1.3f, 0.74f, 0.65f),
                ("bb_set_roundtable", 1.5f, 0.78f, 1.5f),
                ("bb_set_bathtub", 0.75f, 0.76f, 1.7f),
                ("bb_set_shower", 0.9f, 1.05f, 0.9f),
                ("bb_set_basin", 0.55f, 0.97f, 0.45f),
                ("bb_set_toilet", 0.4f, 0.72f, 0.62f),
                ("bb_set_bar", 1.86f, 1.05f, 0.7f),
                ("bb_set_microwave", 0.5f, 0.32f, 0.42f),
                ("bb_set_coffeemachine", 0.26f, 0.34f, 0.33f),
                ("bb_set_toaster", 0.3f, 0.225f, 0.16f),
                ("bb_set_doublebed", 1.6f, 0.85f, 2.0f),
                ("bb_set_cabinet", 0.6f, 0.92f, 0.68f),
                ("bb_set_fridge", 0.8f, 1.45f, 0.75f),
                ("bb_set_tv", 1.1f, 0.65f, 0.18f),
                ("bb_set_pillow", 0.5f, 0.11f, 0.34f),
                ("bb_set_coatrack", 0.4f, 1.45f, 0.4f),
                // Tier 4 clutter, native size, lifted onto surfaces by their rows.
                ("bb_set_mug", 0.13f, 0.10f, 0.09f),
                ("bb_set_bottle", 0.072f, 0.28f, 0.07f),
                ("bb_set_bookstack", 0.245f, 0.135f, 0.185f),
                ("bb_set_towel", 0.32f, 0.06f, 0.22f),
                ("bb_set_cushion", 0.40f, 0.12f, 0.40f),
                ("bb_set_laptop", 0.33f, 0.235f, 0.31f),
                ("bb_set_tray", 0.40f, 0.132f, 0.30f),
                ("bb_set_remote", 0.05f, 0.024f, 0.18f),
                ("bb_set_candle", 0.08f, 0.13f, 0.08f),
                ("bb_set_photoframe", 0.16f, 0.20f, 0.132f),
                ("bb_set_magazines", 0.272f, 0.015f, 0.322f),
                ("bb_set_fruitbowl", 0.28f, 0.13f, 0.28f),
                ("bb_set_cable", 0.626f, 0.039f, 0.16f),
            };
            foreach (var (name, x, y, z) in expected)
            {
                var piece = AssetDatabase.LoadAssetAtPath<GameObject>(SetPieces + name + ".fbx");
                Assert.That(piece, Is.Not.Null, name);
                var bounds = RenderBounds(piece);
                Assert.That(bounds.size.x, Is.EqualTo(x).Within(0.05f), name + " across");
                Assert.That(bounds.size.y, Is.EqualTo(y).Within(0.05f), name + " tall");
                Assert.That(bounds.size.z, Is.EqualTo(z).Within(0.05f), name + " deep");
                Assert.That(piece.GetComponentsInChildren<Collider>(true), Is.Empty, name + ": furniture is collider-free by design");
            }
            foreach (var pair in HouseCatalogue.Replaced)
                Assert.That(HouseCatalogue.Resolve(pair.Key, out var tier) != null && tier == HouseCatalogue.Tier.Authored, Is.True,
                    pair.Key + " resolves to its authored stand-in " + pair.Value);
            // Tier 3's finish line: no plan row, in either pass, resolves to the kit any more.
            var kit = HouseSetPieces.PlanModels.Concat(HouseFurnishing.PlanModels).Distinct()
                .Where(id => { HouseCatalogue.Resolve(id, out var t); return t == HouseCatalogue.Tier.Kenney; }).ToArray();
            Assert.That(kit, Is.Empty, "still resolving to the Kenney kit: " + string.Join(", ", kit));

            // The plants fan their leaves, so their footprint is a range rather than a number.
            var plant = RenderBounds(AssetDatabase.LoadAssetAtPath<GameObject>(SetPieces + "bb_set_plant.fbx"));
            Assert.That(plant.size.y, Is.InRange(0.8f, 1.0f), "a potted plant, near the rows' 0.85-1.05");
            Assert.That(plant.size.x, Is.InRange(0.5f, 0.9f), "leaves fanned out past the pot");
            var small = RenderBounds(AssetDatabase.LoadAssetAtPath<GameObject>(SetPieces + "bb_set_plantsmall.fbx"));
            Assert.That(small.size.y, Is.InRange(0.35f, 0.55f), "a small plant, near the rows' 0.5");
        }

        [Test]
        public void TheShellCoversTheWholeHouseAndCarriesNoCollision()
        {
            var shell = AssetDatabase.LoadAssetAtPath<GameObject>(AuthoredAssetImporter.Root + "Shell/bb_shell_house.fbx");
            Assert.That(shell, Is.Not.Null, "the Tier 2 shell");
            var bounds = RenderBounds(shell);
            Assert.That(bounds.min.x, Is.EqualTo(-14.2f).Within(0.3f), "west wall line");
            Assert.That(bounds.max.x, Is.EqualTo(14.3f).Within(0.3f), "east wall line");
            Assert.That(bounds.min.z, Is.EqualTo(-20.1f).Within(0.3f), "south wing wall line");
            Assert.That(bounds.max.z, Is.EqualTo(20.1f).Within(0.3f), "north fence line");
            Assert.That(bounds.max.y, Is.InRange(1.5f, 1.6f), "the 1.5 m cutaway plus a jamb; nothing taller");
            Assert.That(shell.GetComponentsInChildren<Collider>(true), Is.Empty, "the primitives own collision; the shell is visual");
        }

        [Test]
        public void TheWaterMaterialIsTranslucentAndExtractedBesideTheModel()
        {
            string path = SetPieces + "Materials/bb_mat_water.mat";
            Assert.That(File.Exists(path), Is.True, "Materials are extracted next to the model: " + path);
            var water = AssetDatabase.LoadAssetAtPath<Material>(path);
            Assert.That(water.shader.name, Does.Contain("Universal Render Pipeline"), water.shader.name);
            Assert.That(water.GetFloat("_Surface"), Is.EqualTo(1f), "Surface type must be Transparent.");
            Assert.That(water.color.a, Is.LessThan(0.7f).And.GreaterThan(0.3f), "Water is see-through, not invisible.");
            Assert.That(water.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT"), Is.True);
            Assert.That(water.renderQueue, Is.GreaterThanOrEqualTo((int)UnityEngine.Rendering.RenderQueue.Transparent));
        }

        [Test]
        public void EverySourceScriptHasItsExport()
        {
            // ArtSource is the source of truth; an export with no script cannot be regenerated, and a
            // script with no export is a set piece the house does not have yet.
            string sources = Path.Combine(Directory.GetCurrentDirectory(), "ArtSource", "setpieces");
            Assert.That(Directory.Exists(sources), Is.True, "ArtSource/setpieces sits beside Assets in the repository, and the acceptance mirror must carry it.");
            var scripted = Directory.GetFiles(sources, "bb_set_*.py").Select(Path.GetFileNameWithoutExtension).OrderBy(n => n).ToArray();
            var exported = AuthoredModels().Where(p => p.StartsWith(SetPieces)).Select(Path.GetFileNameWithoutExtension).OrderBy(n => n).ToArray();
            Assert.That(exported, Is.EqualTo(scripted));
        }

        private static Bounds RenderBounds(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            Assert.That(renderers, Is.Not.Empty, root.name + " renders nothing.");
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }
    }
}
