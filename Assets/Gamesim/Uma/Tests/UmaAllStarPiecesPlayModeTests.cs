using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using UMA;
using UMA.CharacterSystem;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Gamesim.Uma.Tests
{
    /// <summary>
    /// A library look wears the pieces a catalog entry's <c>worn-by:</c> tag names it for, with no edit to
    /// the look (LookPieces), and LibraryLooks.txt is the library's own values. The pieces here are
    /// runtime clones of stock UMA recipes in a catalog of their own, removed again at the end.
    /// </summary>
    public sealed class UmaAllStarPiecesPlayModeTests
    {
        private const string CatalogFolder = "Gamesim/CharacterCatalog";
        internal const string Star = "janelle-pierzina", Other = "danielle-reyes";
        /// <summary>The pieces TaggedForStar dresses her in (a duplicate hair, when asked for, is not among them).</summary>
        internal static readonly string[] Worn = { "allstar-test-face", "allstar-test-hair-a", "allstar-test-ears" };

#if UNITY_EDITOR
        /// <summary>Runtime copies of installed recipes in UMA's index, with their own catalog: no AssetDatabase write, no lasting index edit.</summary>
        internal sealed class Pieces : IDisposable
        {
            public readonly UMAAssetIndexer Index = UMAAssetIndexer.Instance;
            public readonly UmaWardrobeCatalog Extension = ScriptableObject.CreateInstance<UmaWardrobeCatalog>();
            public readonly Dictionary<string, UMAWardrobeRecipe> Recipes = new Dictionary<string, UMAWardrobeRecipe>(StringComparer.Ordinal);
            private readonly int originalCount;

            public Pieces() { Extension.name = "All-Star pieces fixture"; originalCount = Index.SerializedItems.Count; }

            /// <summary>A hidden entry tagged for <paramref name="tags"/>, cloned from <paramref name="source"/> (re-slotted when <paramref name="slot"/> is given).</summary>
            public void Add(string id, AppearanceItem source, UmaAppearanceCatalog installed, string slot, params string[] tags)
            {
                var recipe = Object.Instantiate(Index.GetAsset<UMAWardrobeRecipe>(installed.ResolveRecipeName(source.Id)));
                recipe.name = "GamesimAllStar-" + id + "-" + Guid.NewGuid().ToString("N");
                recipe.hideFlags = HideFlags.DontSave;
                if (slot != null) recipe.wardrobeSlot = slot;
                Recipes.Add(id, recipe);
                Assert.That(Index.AddAssetItem(new AssetItem(typeof(UMAWardrobeRecipe), recipe.name, "", recipe), noDirty: true), Is.True);
                Extension.entries.Add(new UmaWardrobeEntry { id = id, recipeName = recipe.name, label = "All-Star test " + id, tags = tags.ToList(), pickerHidden = true });
            }

            public UmaAppearanceCatalog Catalog() =>
                new UmaAppearanceCatalog(Resources.LoadAll<UmaWardrobeCatalog>(CatalogFolder).Concat(new[] { Extension }));

            public void Dispose()
            {
                foreach (var recipe in Recipes.Values) Index.RemoveAsset(typeof(UMAWardrobeRecipe), recipe.name, refresh: false);
                while (Index.SerializedItems.Count > originalCount && Index.SerializedItems.Last() == null)
                    Index.SerializedItems.RemoveAt(Index.SerializedItems.Count - 1);
                foreach (var recipe in Recipes.Values) Object.Destroy(recipe);
                Object.Destroy(Extension);
            }
        }

        /// <summary>
        /// A hidden Face clone, hair clone and Ears clone tagged for <see cref="Star"/>, and a second hair for
        /// her when <paramref name="duplicate"/> (a higher id: it loses). The look walkers' tagged run uses it too.
        /// </summary>
        internal static Pieces TaggedForStar(UmaAppearanceCatalog installed, bool duplicate)
        {
            var pieces = new Pieces();
            string female = UmaCastLibrary.FemaleRace;
            UmaCastLibrary.TryGet(Star, out var look);
            bool Free(AppearanceItem item) => !UmaAppearanceCatalog.IsGrown(item.Id) && item.Fits(female) && !item.PickerHidden
                && !look.Wardrobe.Any(item.Matches);
            string tag = LookPieces.WornBy + Star;
            pieces.Add("allstar-test-face", installed.Items.First(item => item.Slot == "Face" && Free(item)), installed, null, tag);
            pieces.Add("allstar-test-hair-a", installed.Items.First(item => item.Slot == "Hair" && Free(item)), installed, null, tag);
            // Any top she does not wear, re-slotted as an Ears part: geometry the body build can dress.
            pieces.Add("allstar-test-ears", installed.Items.First(item => item.Slot == "Chest" && Free(item)), installed, "Ears", tag);
            if (duplicate) pieces.Add("allstar-test-hair-b", installed.Items.Last(item => item.Slot == "Hair" && Free(item)), installed, null, tag);
            return pieces;
        }

        /// <summary>The installed catalogs and the pieces' own: no diagnostic, and her look sees the pieces.</summary>
        internal static UmaAppearanceCatalog TaggedCatalog(Pieces pieces)
        {
            var catalog = pieces.Catalog();
            Assert.That(catalog.Diagnostics, Is.Empty);
            Assert.That(AppearanceEditing.LookItemIds(catalog, CharacterAppearance.Preset(Star)), Is.SupersetOf(Worn),
                "The tagged pieces are in her look.");
            return catalog;
        }

        /// <summary>The last line of a test with tagged pieces: once they are removed, the installed catalog holds no worn-by: entry.</summary>
        internal static void AssertNoTaggedEntryInstalled() =>
            Assert.That(new UmaAppearanceCatalog().Items.Where(item => item.Tags.Any(tag => tag.StartsWith(LookPieces.WornBy, StringComparison.Ordinal)))
                .Select(item => item.Id), Is.Empty, "A tagged entry outlived its test.");

        private Pieces pieces;

        [TearDown]
        public void RemoveTaggedPieces() { pieces?.Dispose(); pieces = null; }

        private sealed class Built { public CharacterBodyBuildState State; public DynamicCharacterAvatar Avatar; }

        private static IEnumerator Build(UmaBodyProvider provider, string id, CharacterAppearance appearance, Transform parent, Built into)
        {
            Assert.That(provider.TryCreate(new CharacterBodyRequest(id, id, appearance, CharacterBuildPurpose.Studio, 1),
                parent, Color.white, out var body), Is.True, id);
            into.State = body.Root.GetComponent<CharacterBodyBuildState>();
            double deadline = Time.realtimeSinceStartupAsDouble + 30;
            while (!into.State.Ready && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.That(into.State.Ready, Is.True, id + "'s body finished building.");
            into.Avatar = body.Root.GetComponent<DynamicCharacterAvatar>();
        }

        [UnityTest]
        public IEnumerator ALookWearsThePiecesTaggedForItAndNoOtherLookDoes()
        {
            var cast = new GameObject("All-Star pieces provider", typeof(GamesimUmaCast));
            var parent = new GameObject("All-Star pieces actors");
            var warnings = new List<string>();
            void Count(string message, string trace, LogType type)
            {
                if (type == LogType.Warning && message.StartsWith("[Gamesim.Uma]", StringComparison.Ordinal) && message.Contains(Star)) warnings.Add(message);
            }
            yield return null;
            var installed = new UmaAppearanceCatalog();
            // Her outfit as saved before any piece was tagged: a save keeps it.
            var saved = installed.Materialize(CharacterAppearance.Preset(Star));
            Application.logMessageReceived += Count;
            try
            {
                pieces = TaggedForStar(installed, duplicate: true);
                var catalog = TaggedCatalog(pieces);
                var mine = Worn;
                foreach (string id in mine.Concat(new[] { "allstar-test-hair-b" }))
                    Assert.That(AppearanceEditing.Find(catalog, id)?.PickerHidden, Is.True, id + " is a hidden item.");

                var outfit = catalog.Materialize(CharacterAppearance.Preset(Star)).outfits.Single().wardrobe;
                Assert.That(outfit.Where(worn => worn.slot.StartsWith("Missing-", StringComparison.Ordinal)), Is.Empty);
                foreach (string id in mine)
                    Assert.That(outfit.Single(worn => worn.itemId == id).slot, Is.EqualTo(pieces.Recipes[id].wardrobeSlot), id);
                Assert.That(outfit.Select(worn => worn.itemId), Has.No.Member("allstar-test-hair-b"), "The lowest id of two wins.");
                Assert.That(outfit.Count(worn => worn.slot == "Hair"), Is.EqualTo(1), "In place of her own hair.");
                Assert.That(AppearanceEditing.LookItemIds(catalog, CharacterAppearance.Preset(Star)), Is.SupersetOf(mine));
                // Her face and hair stay on in the water and in bed (the swim and sleep sets keep those slots).
                foreach (string context in new[] { CharacterOutfits.Swimwear, CharacterOutfits.Sleepwear })
                {
                    var changed = CharacterOutfits.ForActivity(catalog.Materialize(CharacterAppearance.Preset(Star)), context);
                    Assert.That(changed.activeOutfit, Is.EqualTo(context));
                    Assert.That(changed.outfits.Single(set => set.id == context).wardrobe.Select(worn => worn.itemId),
                        Is.SupersetOf(new[] { "allstar-test-face", "allstar-test-hair-a" }), context);
                }
                var draft = catalog.Materialize(new CharacterAppearance { presetId = "custom-draft", fallbackId = Star });
                Assert.That(draft.outfits.Single().wardrobe.Select(worn => worn.itemId), Is.SupersetOf(mine), "A draft from her look wears them.");
                var other = catalog.Materialize(CharacterAppearance.Preset(Other)).outfits.Single().wardrobe.Select(worn => worn.itemId).ToArray();
                Assert.That(other.Intersect(pieces.Recipes.Keys), Is.Empty, Other + " wears none of them.");

                var provider = new UmaBodyProvider(catalog);
                var star = new Built();
                yield return Build(provider, Star, null, parent.transform, star);
                var beside = new Built();
                yield return Build(provider, Other, null, parent.transform, beside);
                Assert.That(string.IsNullOrEmpty(star.State.Substitution), Is.True, star.State.Substitution);
                foreach (string id in mine)
                {
                    var recipe = pieces.Recipes[id];
                    Assert.That(star.Avatar.GetWardrobeItem(recipe.wardrobeSlot)?.name, Is.EqualTo(recipe.name), id + " is on her body.");
                    Assert.That(beside.Avatar.GetWardrobeItem(recipe.wardrobeSlot)?.name, Is.Not.EqualTo(recipe.name), id + " is not on " + Other + ".");
                }

                var kept = new Built();
                yield return Build(provider, Star, catalog.Materialize(saved), parent.transform, kept);
                Assert.That(catalog.Materialize(saved).outfits.Single().wardrobe.Select(worn => worn.itemId),
                    Is.EqualTo(saved.outfits.Single().wardrobe.Select(worn => worn.itemId)), "A saved outfit is never added to.");
                foreach (string id in mine)
                    Assert.That(kept.Avatar.GetWardrobeItem(pieces.Recipes[id].wardrobeSlot)?.name, Is.Not.EqualTo(pieces.Recipes[id].name),
                        "Her save keeps its own outfit: " + id);
                Assert.That(warnings, Has.Count.EqualTo(1), "One warning for her Hair slot: " + string.Join(" | ", warnings));
                Assert.That(warnings[0], Does.Contain("Hair"));
                RemoveTaggedPieces();
                AssertNoTaggedEntryInstalled();
            }
            finally
            {
                Application.logMessageReceived -= Count;
                Object.Destroy(parent); Object.Destroy(cast);
            }
            yield return null;
        }

        // ------------------------------------------------------------------------- LibraryLooks.txt

        /// <summary>Jun's values are frozen while parts are fitted to her head: a change to any of them fails here until it is reopened.</summary>
        private const string JunLine = "jun-song\tHuman Female 3.0\tarmWidth=0.62\tcheekPosition=0.44\teyeRotation=0.56\teyeSize=0.62"
            + "\tfeetSize=0.66\tforearmWidth=0.62\thandsSize=0.68\theadSize=0.72\theadWidth=0.58\theight=0.09\tlegsSize=0.62"
            + "\tlowerMuscle=0.58\tneckThickness=0.6\tnoseFlatten=0.56\tupperMuscle=0.58\tupperWeight=0.58";

        /// <summary>
        /// LibraryLooks.txt (read by the lane's checker without parsing C#) is, byte for byte, each library look's line from
        /// LookPieces.LibraryLine over the merge the game makes: the house proportions, then the look's own DNA.
        /// </summary>
        [Test]
        public void LibraryLooksTxtIsTheLibraryByteForByte()
        {
            var expected = new StringBuilder();
            foreach (string id in UmaCastLibrary.AppearanceIds.OrderBy(id => id, StringComparer.Ordinal))
            {
                Assert.That(UmaCastLibrary.TryGet(id, out var look), Is.True);
                var merged = new Dictionary<string, float>(UmaCastLibrary.HouseProportions);
                foreach (var entry in look.Dna) merged[entry.Key] = entry.Value;
                foreach (var entry in merged)
                    Assert.That(entry.Value, Is.InRange(0f, 1f), id + " " + entry.Key);
                expected.Append(LookPieces.LibraryLine(id, look.Race, merged)).Append('\n');
            }
            byte[] onDisk = File.ReadAllBytes(Path.Combine(Application.dataPath, "Gamesim/Uma/LibraryLooks.txt"));
            byte[] wanted = new UTF8Encoding(false).GetBytes(expected.ToString());
            Assert.That(onDisk, Is.EqualTo(wanted), "Assets/Gamesim/Uma/LibraryLooks.txt should read (UTF-8, no BOM, LF):\n" + expected);

            var ids = Encoding.UTF8.GetString(onDisk).Split('\n').Where(line => line.Length > 0).Select(line => line.Split('\t')[0]).ToArray();
            Assert.That(ids, Is.EquivalentTo(UmaCastLibrary.AppearanceIds), "Every line is a library look and every library look has its line.");
            Assert.That(Encoding.UTF8.GetString(onDisk).Split('\n').Single(line => line.StartsWith("jun-song\t", StringComparison.Ordinal)),
                Is.EqualTo(JunLine), "Jun's values are frozen.");
        }
#endif
    }
}
