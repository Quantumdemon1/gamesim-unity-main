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
                Assert.That(importer.animationType, Is.EqualTo(AuthoredAssetImporter.IsRigged(path)
                    ? ModelImporterAnimationType.Human : ModelImporterAnimationType.None), path);
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
                var bounds = RenderBounds(root);
                Assert.That(bounds.min.y, Is.EqualTo(0f).Within(0.01f), path + ": the lowest point is the floor.");
                // A prop is centred on its origin so a plan can place it by its middle. The shell is
                // not a prop: it is the house, laid over the colliders at the world origin.
                if (path.StartsWith(AuthoredAssetImporter.Root + "Shell/", System.StringComparison.Ordinal)) continue;
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
