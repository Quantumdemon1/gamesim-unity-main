using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
    /// The private shelf (PrivatePieceShelf): slots, overlays and wardrobe recipes that source control never sees are
    /// put on UMA's index at run time, a library look wears the one a catalog tag names it for, and the look's
    /// garment dye is not laid on such a piece. Every asset here is built in code from a stock one and taken off the
    /// index again; the look is a fictional house guest no delivered piece names (UmaAllStarPiecesPlayModeTests.Star).
    /// </summary>
    public sealed class UmaPrivatePieceShelfPlayModeTests
    {
#if UNITY_EDITOR
        private const string CatalogFolder = "Gamesim/CharacterCatalog";
        private static string Star => UmaAllStarPiecesPlayModeTests.Star;
        private static string Other => UmaAllStarPiecesPlayModeTests.Other;

        private readonly List<Object> made = new List<Object>();
        private IReadOnlyList<Object> registered;
        private int length;

        [SetUp]
        public void Remember()
        {
            PrivatePieceShelf.Ensure(UMAAssetIndexer.Instance);
            length = UMAAssetIndexer.Instance.SerializedItems.Count;
        }

        [TearDown]
        public void Clear()
        {
            if (registered != null) PrivatePieceShelf.Withdraw(registered);
            registered = null;
            foreach (var asset in made) if (asset != null) Object.Destroy(asset);
            made.Clear();
        }

        private T Made<T>(T asset) where T : Object { asset.hideFlags = HideFlags.DontSave; made.Add(asset); return asset; }

        /// <summary>An item <see cref="Star"/> does not wear, in <paramref name="slot"/>, offered in the pickers and cut for her body.</summary>
        private static AppearanceItem FreeFor(UmaAppearanceCatalog installed, string slot)
        {
            Assert.That(UmaCastLibrary.TryGet(Star, out var look), Is.True);
            return installed.Items.First(item => item.Slot == slot && !UmaAppearanceCatalog.IsGrown(item.Id)
                && item.Fits(UmaCastLibrary.FemaleRace) && !item.PickerHidden && !look.Wardrobe.Any(item.Matches));
        }

        private static UmaWardrobeEntry TaggedEntry(string id, UMAWardrobeRecipe recipe) => new UmaWardrobeEntry
        {
            id = id, recipeName = recipe.name, label = "Shelf test " + id,
            tags = new List<string> { LookPieces.WornBy + Star }, pickerHidden = true,
        };

        private UmaAppearanceCatalog CatalogWith(params UmaWardrobeEntry[] entries)
        {
            var extension = Made(ScriptableObject.CreateInstance<UmaWardrobeCatalog>());
            extension.name = "Private shelf fixture";
            extension.entries.AddRange(entries);
            var catalog = new UmaAppearanceCatalog(Resources.LoadAll<UmaWardrobeCatalog>(CatalogFolder).Concat(new[] { extension }));
            Assert.That(catalog.Diagnostics, Is.Empty);
            return catalog;
        }

        /// <summary>After a withdrawal: none of <paramref name="names"/> is on the index, and the index is back to its length.</summary>
        private void AssertWithdrawn(UMAAssetIndexer index, params (Type type, string name)[] names)
        {
            PrivatePieceShelf.Withdraw(registered);
            registered = null;
            foreach (var (type, name) in names)
                Assert.That(index.GetAssetDictionary(type).ContainsKey(name), Is.False, name + " outlived its test in UMA's index.");
            Assert.That(index.SerializedItems.Count, Is.EqualTo(length), "The index is back to its length.");
        }

        private sealed class Built { public CharacterBodyBuildState State; public DynamicCharacterAvatar Avatar; }

        private static IEnumerator Build(UmaBodyProvider provider, string id, Transform parent, Built into)
        {
            Assert.That(provider.TryCreate(new CharacterBodyRequest(id, id, null, CharacterBuildPurpose.Studio, 1),
                parent, Color.white, out var body), Is.True, id);
            into.State = body.Root.GetComponent<CharacterBodyBuildState>();
            double deadline = Time.realtimeSinceStartupAsDouble + 30;
            while (!into.State.Ready && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.That(into.State.Ready, Is.True, id + "'s body finished building.");
            into.Avatar = body.Root.GetComponent<DynamicCharacterAvatar>();
        }

        /// <summary>
        /// The shelf this copy holds (none in the repository) is on the index, each asset under its own name; asking
        /// again, registering nothing, or registering what the index already holds changes nothing; and an asset named
        /// like one the index holds is left out with a warning, the installed one still answering to the name.
        /// </summary>
        [Test]
        public void AnEmptyOrRepeatedShelfChangesNothing()
        {
            var index = UMAAssetIndexer.Instance;
            Assert.That(index, Is.Not.Null);
            PrivatePieceShelf.Ensure(index);
            foreach (var slot in Resources.LoadAll<SlotDataAsset>(PrivatePieceShelf.Folder))
                Assert.That(index.GetAsset<SlotDataAsset>(AssetItem.GetEvilName(slot)), Is.SameAs(slot), "Shelf slot " + slot.name);
            foreach (var overlay in Resources.LoadAll<OverlayDataAsset>(PrivatePieceShelf.Folder))
                Assert.That(index.GetAsset<OverlayDataAsset>(AssetItem.GetEvilName(overlay)), Is.SameAs(overlay), "Shelf overlay " + overlay.name);
            foreach (var recipe in Resources.LoadAll<UMAWardrobeRecipe>(PrivatePieceShelf.Folder))
                Assert.That(index.GetAsset<UMAWardrobeRecipe>(AssetItem.GetEvilName(recipe)), Is.SameAs(recipe), "Shelf recipe " + recipe.name);

            int before = index.SerializedItems.Count, shelved = PrivatePieceShelf.Count;
            Assert.That(PrivatePieceShelf.Ensure(index), Is.Zero, "The shelf is read once a session.");
            Assert.That(PrivatePieceShelf.Register(index, Array.Empty<Object>()), Is.Empty, "An empty shelf adds nothing.");
            var stock = index.GetAllAssets<UMAWardrobeRecipe>().First(recipe => recipe != null);
            Assert.That(PrivatePieceShelf.Register(index, new Object[] { null, stock }), Is.Empty, "What the index holds is not added again.");

            var twin = Made(Object.Instantiate(stock));
            twin.name = stock.name;
            LogAssert.Expect(LogType.Warning, "[Gamesim.Uma] A private piece is named like an asset UMA already holds and is left out: " + stock.name);
            Assert.That(PrivatePieceShelf.Register(index, new Object[] { twin }), Is.Empty, "A name the index holds is never taken.");
            Assert.That(index.GetAsset<UMAWardrobeRecipe>(stock.name), Is.SameAs(stock), "The installed recipe still answers to its name.");
            Assert.That(index.SerializedItems.Count, Is.EqualTo(before), "Nothing was added to the index.");
            Assert.That(PrivatePieceShelf.Count, Is.EqualTo(shelved), "Nothing was added to the shelf's register.");
        }

        /// <summary>
        /// A slot, an overlay and a recipe that names them, all made in code, are registered through the shelf and
        /// resolve by name; a catalog entry tagged for <see cref="Star"/> dresses her library look in the recipe, her
        /// built body carries the new slot with the new overlay, and the other look wears none of it.
        /// </summary>
        [UnityTest]
        public IEnumerator ARegisteredRecipeResolvesAndItsLookWearsIt()
        {
            var cast = new GameObject("Private shelf provider", typeof(GamesimUmaCast));
            var parent = new GameObject("Private shelf actors");
            try
            {
                yield return null;
                var index = UMAAssetIndexer.Instance;
                var installed = new UmaAppearanceCatalog();
                var stock = index.GetAsset<UMAWardrobeRecipe>(installed.ResolveRecipeName(FreeFor(installed, "Hair").Id));
                var packed = stock.PackedLoad();
                var part = packed.slotsV3?.FirstOrDefault(value => value != null && !string.IsNullOrEmpty(value.id)
                    && value.overlays != null && value.overlays.Length > 0 && value.overlays[0] != null && !string.IsNullOrEmpty(value.overlays[0].id));
                Assert.That(part, Is.Not.Null, stock.name + " has a slot with an overlay to copy.");

                string stamp = Guid.NewGuid().ToString("N");
                var slot = Made(Object.Instantiate(index.GetAsset<SlotDataAsset>(part.id)));
                slot.PrepareForAssetPath("Assets/ShelfTest/GamesimShelfTestSlot-" + stamp + ".asset", "GamesimShelfTestSlot-" + stamp);
                var overlay = Made(Object.Instantiate(index.GetAsset<OverlayDataAsset>(part.overlays[0].id)));
                overlay.name = overlay._oldOverlayName = "GamesimShelfTestOverlay-" + stamp;
                var recipe = Made(Object.Instantiate(stock));
                recipe.name = "GamesimShelfTestRecipe-" + stamp;
                part.id = slot.slotName;
                part.overlays[0].id = overlay.overlayName;
                recipe.PackedSave(packed);
                Assert.That(slot.slotName, Is.EqualTo("GamesimShelfTestSlot-" + stamp));

                registered = PrivatePieceShelf.Register(index, new Object[] { slot, overlay, recipe });
                Assert.That(registered.Count, Is.EqualTo(3), "All three are new to the index.");
                Assert.That(index.GetAsset<SlotDataAsset>(slot.slotName), Is.SameAs(slot));
                Assert.That(index.GetAsset<OverlayDataAsset>(overlay.overlayName), Is.SameAs(overlay));
                Assert.That(index.GetAsset<UMAWardrobeRecipe>(recipe.name), Is.SameAs(recipe));

                var catalog = CatalogWith(TaggedEntry("shelf-test-hair", recipe));
                Assert.That(AppearanceEditing.Find(catalog, "shelf-test-hair")?.PickerHidden, Is.True, "A shelf piece is hidden from the pickers.");
                Assert.That(UmaCastLibrary.TryGet(Star, out var look), Is.True);
                Assert.That(catalog.LookWardrobe(Star, look.Race, look.Wardrobe), Has.Member("shelf-test-hair"));

                var provider = new UmaBodyProvider(catalog);
                var star = new Built();
                yield return Build(provider, Star, parent.transform, star);
                var beside = new Built();
                yield return Build(provider, Other, parent.transform, beside);
                Assert.That(string.IsNullOrEmpty(star.State.Substitution), Is.True, star.State.Substitution);
                Assert.That(star.Avatar.GetWardrobeItem("Hair")?.name, Is.EqualTo(recipe.name), "The shelf recipe is on her body.");
                Assert.That(beside.Avatar.GetWardrobeItem("Hair")?.name, Is.Not.EqualTo(recipe.name), Other + " does not wear it.");
                var worn = star.Avatar.umaData.umaRecipe.slotDataList.FirstOrDefault(value => value != null && ReferenceEquals(value.asset, slot));
                Assert.That(worn, Is.Not.Null, "Her body carries the shelf slot.");
                Assert.That(worn.GetOverlayList().Any(value => value != null && ReferenceEquals(value.asset, overlay)), Is.True,
                    "The shelf slot carries the shelf overlay.");

                AssertWithdrawn(index, (typeof(SlotDataAsset), slot.slotName), (typeof(OverlayDataAsset), overlay.overlayName),
                    (typeof(UMAWardrobeRecipe), recipe.name));
            }
            finally { Object.Destroy(parent); Object.Destroy(cast); }
            yield return null;
        }

        /// <summary>
        /// A top tagged for <see cref="Star"/> keeps its own colours: her look's Chest dye is neither saved with her
        /// materialised outfit nor laid on the top's overlays when her library look is built. The control: with no
        /// piece her outfit carries the dye, and the other look's own top is dyed on her body beside her.
        /// </summary>
        [UnityTest]
        public IEnumerator ALooksDyeIsNotLaidOnAPieceMadeForIt()
        {
            var cast = new GameObject("Private shelf dye provider", typeof(GamesimUmaCast));
            var parent = new GameObject("Private shelf dye actors");
            try
            {
                yield return null;
                var index = UMAAssetIndexer.Instance;
                var installed = new UmaAppearanceCatalog();
                Assert.That(UmaCastLibrary.TryGet(Star, out var look), Is.True);
                Assert.That(look.Fabric.TryGetValue("Chest", out var dye), Is.True, Star + "'s look dyes her top.");
                Assert.That(UmaCastLibrary.TryGet(Other, out var otherLook), Is.True);
                Assert.That(otherLook.Fabric.TryGetValue("Chest", out var otherDye), Is.True, Other + "'s look dyes her top.");
                Assert.That(AppearanceEditing.TryFabric(AppearanceEditing.Outfit(installed.Materialize(CharacterAppearance.Preset(Star))), "Chest", out var plain)
                    && Same(plain, dye), Is.True, "Control: with no piece, her materialised outfit carries her look's Chest dye.");

                // A copy of the top her look wears, so the piece's overlays are ones her dye reaches when it is her own.
                var own = look.Wardrobe.Select(line => index.GetAsset<UMAWardrobeRecipe>(installed.ResolveRecipeName(line)))
                    .FirstOrDefault(value => value != null && value.wardrobeSlot == "Chest");
                Assert.That(own, Is.Not.Null, Star + "'s look wears a top.");
                var recipe = Made(Object.Instantiate(own));
                recipe.name = "GamesimShelfTestTop-" + Guid.NewGuid().ToString("N");
                registered = PrivatePieceShelf.Register(index, new Object[] { recipe });
                Assert.That(registered.Count, Is.EqualTo(1));
                var catalog = CatalogWith(TaggedEntry("shelf-test-top", recipe));
                Assert.That(catalog.IsLookPiece(Star, "shelf-test-top") && catalog.IsLookPiece(Star, recipe.name), Is.True);
                Assert.That(catalog.IsLookPiece(Other, "shelf-test-top"), Is.False);

                var outfit = AppearanceEditing.Outfit(catalog.Materialize(CharacterAppearance.Preset(Star)));
                Assert.That(outfit.wardrobe.Any(value => value.itemId == "shelf-test-top" && value.slot == "Chest"), Is.True, "She wears the top.");
                Assert.That(AppearanceEditing.TryFabric(outfit, "Chest", out _), Is.False, "Her look's dye is not saved for a piece made for her.");

                var provider = new UmaBodyProvider(catalog);
                var star = new Built();
                yield return Build(provider, Star, parent.transform, star);
                var beside = new Built();
                yield return Build(provider, Other, parent.transform, beside);
                Assert.That(star.Avatar.GetWardrobeItem("Chest")?.name, Is.EqualTo(recipe.name), "The top is on her body.");

                var topSlots = new HashSet<string>(recipe.PackedLoad().slotsV3.Where(value => value != null && !string.IsNullOrEmpty(value.id)).Select(value => value.id));
                var mine = Garment(star.Avatar, topSlots);
                Assert.That(mine, Is.Not.Empty, "Her top's own overlays are found on her body.");
                Assert.That(mine.Where(value => Same(value, dye)), Is.Empty, "Her look's Chest dye is not laid on the top.");

                var theirs = beside.Avatar.GetWardrobeItem("Chest");
                Assert.That(theirs, Is.Not.Null);
                var theirSlots = new HashSet<string>(theirs.PackedLoad().slotsV3.Where(value => value != null && !string.IsNullOrEmpty(value.id)).Select(value => value.id));
                Assert.That(Garment(beside.Avatar, theirSlots).Any(value => Same(value, otherDye)), Is.True,
                    "Control: " + Other + "'s own top carries her look's dye.");

                AssertWithdrawn(index, (typeof(UMAWardrobeRecipe), recipe.name));
            }
            finally { Object.Destroy(parent); Object.Destroy(cast); }
            yield return null;
        }

        /// <summary>The first-channel colours of the private (unshared) overlay colours on the body's slots named in <paramref name="slots"/>.</summary>
        private static List<Color> Garment(DynamicCharacterAvatar avatar, HashSet<string> slots)
        {
            var found = new List<Color>();
            foreach (var slot in avatar.umaData.umaRecipe.slotDataList)
            {
                if (slot == null || !slots.Contains(slot.slotName)) continue;
                foreach (var overlay in slot.GetOverlayList())
                    if (overlay?.colorData != null && !overlay.colorData.IsASharedColor && overlay.colorData.channelMask != null
                        && overlay.colorData.channelMask.Length > 0)
                        found.Add(overlay.colorData.channelMask[0]);
            }
            return found;
        }

        private static bool Same(Color a, Color b) =>
            Mathf.Abs(a.r - b.r) < .002f && Mathf.Abs(a.g - b.g) < .002f && Mathf.Abs(a.b - b.b) < .002f;
#endif
    }
}
