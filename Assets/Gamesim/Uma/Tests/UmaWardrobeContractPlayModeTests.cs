#if GAMESIM_UMA
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
    /// What a UMA wardrobe part must meet to be worn in the house, checked on every entry of every
    /// wardrobe catalog the game loads. The tests walk the catalogs' entries, not only the creator's
    /// items (an entry whose recipe is missing from UMA's index yields no item and no diagnostic), so
    /// a part delivered later is held to the same rules with no change here.
    ///
    /// <para>Two tiers. Every entry: its recipe, slot, ids, label, finish mark and place. The game's
    /// own content - an entry whose id starts <c>gamesim.</c> or that no picker offers - is also held
    /// to the rules a delivered part's files must meet: a named thumbnail is valid, each slot carries
    /// its own name and has vertices, and the part is worn after a save and a reload and keeps a
    /// marked finish through a rebuild. Stock UMA entries (<c>uma-</c> ids) are not held to those.</para>
    ///
    /// <para>A recipe's slot entry flagged <c>isPlaceholderSlot</c> (a painted face piece's form) is not the part's
    /// own slot: UMA builds no slot under its id and hands its overlays to the built slots that carry one of its
    /// tags (DynamicCharacterAvatar.PostProcessSlots). For such an entry the contract is that each of its overlays
    /// is on a built slot carrying one of the entry's tags after a save and a reload and through a rebuild, an
    /// overlay sharing a colour still shares that colour, and an overlay with a colour of its own keeps it. Every
    /// other entry is held to the slot rules above.</para>
    /// </summary>
    [TestFixture]
    public sealed class UmaWardrobeContractPlayModeTests
    {
        private const string CatalogFolder = "Gamesim/CharacterCatalog";
        private static readonly string[] Outfits = { "Everyday", "Competition", "Formal", "Sleepwear", "Swimwear" };
        private static readonly string[] Pages = { "Body", "Face", "Hair", "Clothing", "Accessories", "Colors" };
        /// <summary>The creator pages a wardrobe row is drawn on, by the slots of their rows.</summary>
        private static readonly Dictionary<string, string[]> PageSlots = new Dictionary<string, string[]>
        {
            ["Face"] = new[] { "Face" },
            ["Hair"] = new[] { "Hair", "Eyebrows", "Beard" },
            ["Clothing"] = new[] { "Chest", "Legs", "Feet", "TopUnderlayer", "BottomUnderlayer" },
            ["Accessories"] = WardrobeSlots.Accessories,
        };
        /// <summary>The shared colours an accessory may declare: the person's own, never a dye channel.</summary>
        private static readonly string[] PersonColours = { "Skin", "Hair", "Eyes", "Brows" };
        /// <summary>Ruling 16: the finish mark is for metal jewellery and watches, worn in these places.</summary>
        private static readonly string[] MarkedFinishSlots = { "Ears", "Shoulders", "Arms", "Hands" };

        private static readonly int Metallic = Shader.PropertyToID("_Metallic"), Smoothness = Shader.PropertyToID("_Smoothness"),
            BumpScale = Shader.PropertyToID("_BumpScale");

        private readonly List<Object> owned = new List<Object>();
        /// <summary>Each authored slot's mesh label as the fixture found it, before any avatar of its own was built.</summary>
        private Dictionary<string, string> meshLabels;

        [OneTimeSetUp]
        public void ReadMeshLabels()
        {
            // Stock UMA relabels a slot's source meshData in memory on every build (meshData.SlotName =
            // slotName), so a label read after a build proves nothing. Read once, before this fixture
            // builds an avatar. An earlier fixture in a long session may still have built the part and
            // masked a wrong label: the asset on disk is the checker's to judge (contract item 18).
            meshLabels = new Dictionary<string, string>(StringComparer.Ordinal);
            var index = UMAAssetIndexer.Instance;
            if (index == null) return;
            foreach (var entry in Entries().Where(Authored))
            {
                var recipe = index.GetAsset<UMAWardrobeRecipe>(entry.recipeName);
                if (recipe == null) continue;
                foreach (var slot in SlotAssets(recipe))
                    if (slot != null && slot.meshData != null) meshLabels[slot.slotName] = slot.meshData.SlotName;
            }
        }

        [TearDown]
        public void DestroyOwned()
        {
            foreach (var value in owned) if (value != null) Object.Destroy(value);
            owned.Clear();
        }

        // ----------------------------------------------------------------------------------- reading

        /// <summary>Every entry of every catalog the game loads, in load order.</summary>
        private static List<UmaWardrobeEntry> Entries() => Resources.LoadAll<UmaWardrobeCatalog>(CatalogFolder)
            .Where(asset => asset != null).OrderBy(asset => asset.name, StringComparer.Ordinal)
            .SelectMany(asset => asset.entries).Where(entry => entry != null).ToList();

        /// <summary>
        /// Content authored for this game: an id in the game's own namespace, or a part no picker
        /// offers (every All-Star part). Held to the rules on a part's files as well.
        /// </summary>
        private static bool Authored(UmaWardrobeEntry entry) =>
            entry.pickerHidden || (entry.id != null && entry.id.StartsWith("gamesim.", StringComparison.Ordinal));

        /// <summary>The ids of the recipe's own slots: every slot entry but a placeholder, which builds no slot of its id.</summary>
        private static string[] SlotIds(UMAWardrobeRecipe recipe) => (recipe.PackedLoad().slotsV3 ?? Array.Empty<UMAPackedRecipeBase.PackedSlotDataV3>())
            .Where(part => part != null && !string.IsNullOrEmpty(part.id) && !part.isPlaceholderSlot).Select(part => part.id).Distinct().ToArray();

        /// <summary>An overlay of a placeholder entry, with the colour the recipe gives it.</summary>
        private sealed class PlaceholderOverlay
        {
            public string Id;
            /// <summary>Whether the recipe names a colour for it; when false its colour is not judged.</summary>
            public bool KnownColour;
            public bool Shared;
            public string Colour;
            public Color Multiply, Additive;
        }

        /// <summary>A slot entry flagged isPlaceholderSlot: the tags its overlays find built slots by, and the overlays.</summary>
        private sealed class Placeholder
        {
            public string Id;
            public string[] Tags;
            public List<PlaceholderOverlay> Overlays;
        }

        private static List<Placeholder> Placeholders(UMAWardrobeRecipe recipe)
        {
            var packed = recipe.PackedLoad();
            var colours = packed.fColors ?? Array.Empty<UMAPackedRecipeBase.PackedOverlayColorDataV3>();
            PlaceholderOverlay Read(UMAPackedRecipeBase.PackedOverlayDataV3 overlay)
            {
                var read = new PlaceholderOverlay { Id = overlay.id };
                var colour = overlay.colorIdx >= 0 && overlay.colorIdx < colours.Length ? colours[overlay.colorIdx] : null;
                if (colour == null) return read;
                read.Shared = overlay.colorIdx < packed.sharedColorCount;
                read.Colour = colour.name;
                if (read.Shared) { read.KnownColour = true; return read; }
                if (colour.colors == null || colour.colors.Length < 8) return read;
                read.KnownColour = true;
                read.Multiply = new Color(colour.colors[0] / 255f, colour.colors[1] / 255f, colour.colors[2] / 255f, colour.colors[3] / 255f);
                read.Additive = new Color(colour.colors[4] / 255f, colour.colors[5] / 255f, colour.colors[6] / 255f, colour.colors[7] / 255f);
                return read;
            }
            return (packed.slotsV3 ?? Array.Empty<UMAPackedRecipeBase.PackedSlotDataV3>())
                .Where(part => part != null && !string.IsNullOrEmpty(part.id) && part.isPlaceholderSlot)
                .Select(part => new Placeholder
                {
                    Id = part.id,
                    Tags = part.Tags ?? Array.Empty<string>(),
                    Overlays = (part.overlays ?? Array.Empty<UMAPackedRecipeBase.PackedOverlayDataV3>())
                        .Where(overlay => overlay != null && !string.IsNullOrEmpty(overlay.id)).Select(Read).ToList(),
                }).ToList();
        }

        private static bool Near(Color a, Color b) =>
            Mathf.Abs(a.r - b.r) <= .003f && Mathf.Abs(a.g - b.g) <= .003f && Mathf.Abs(a.b - b.b) <= .003f && Mathf.Abs(a.a - b.a) <= .003f;

        /// <summary>
        /// Where a built body breaks the placeholder contract: an overlay of a placeholder entry that is on no built
        /// slot carrying one of the entry's tags (UMA's own test, SlotData.HasTag), an overlay that shared a colour and
        /// no longer shares it, and an overlay with a colour of its own that lost it. Empty when the body keeps it.
        /// </summary>
        private static List<string> PlaceholderMisses(DynamicCharacterAvatar avatar, string id, IEnumerable<Placeholder> placeholders, string when)
        {
            var misses = new List<string>();
            var slots = avatar.umaData.umaRecipe.slotDataList.Where(slot => slot != null && !slot.isPlaceholderSlot).ToList();
            foreach (var placeholder in placeholders)
            {
                var tagged = slots.Where(slot => slot.HasTag(placeholder.Tags)).ToList();
                foreach (var overlay in placeholder.Overlays)
                {
                    var on = tagged.SelectMany(slot => slot.GetOverlayList()).Where(value => value != null && value.asset != null && value.overlayName == overlay.Id).ToList();
                    if (on.Count == 0)
                    {
                        var elsewhere = slots.Where(slot => slot.GetOverlayList().Any(value => value != null && value.asset != null && value.overlayName == overlay.Id)).Select(slot => slot.slotName);
                        misses.Add(id + ": the overlay " + overlay.Id + " of the placeholder entry " + placeholder.Id + " is on no built slot tagged "
                            + string.Join("/", placeholder.Tags) + " " + when + "; it is on [" + string.Join(", ", elsewhere) + "].");
                        continue;
                    }
                    if (!overlay.KnownColour) continue;
                    foreach (var worn in on)
                    {
                        var colour = worn.colorData;
                        if (overlay.Shared)
                        {
                            if (colour == null || !colour.IsASharedColor || colour.name != overlay.Colour)
                                misses.Add(id + ": the overlay " + overlay.Id + " no longer shares the colour " + overlay.Colour + " " + when + "; it has "
                                    + (colour == null ? "none" : "'" + colour.name + "'") + ".");
                        }
                        else if (colour == null || colour.IsASharedColor || colour.channelMask == null || colour.channelMask.Length == 0
                            || colour.channelAdditiveMask == null || colour.channelAdditiveMask.Length == 0
                            || !Near(colour.channelMask[0], overlay.Multiply) || !Near(colour.channelAdditiveMask[0], overlay.Additive))
                            misses.Add(id + ": the overlay " + overlay.Id + " does not keep its own colour (multiply " + overlay.Multiply + ", additive "
                                + overlay.Additive + ") " + when + "; it has " + (colour == null ? "none" : "'" + colour.name + "' multiply "
                                + (colour.channelMask != null && colour.channelMask.Length > 0 ? colour.channelMask[0].ToString() : "none")) + ".");
                    }
                }
            }
            return misses;
        }

        private static IEnumerable<SlotDataAsset> SlotAssets(UMAWardrobeRecipe recipe) =>
            SlotIds(recipe).Select(id => UMAAssetIndexer.Instance.GetAsset<SlotDataAsset>(id));

        /// <summary>
        /// Each slot of a recipe with the UMAMaterial UMA draws it with: the slot's own, else its first
        /// overlay's (a slot asset names no material of its own; the overlays carry it).
        /// </summary>
        private static List<(string Slot, UMAMaterial Material)> SlotMaterials(UMAWardrobeRecipe recipe)
        {
            var found = new List<(string Slot, UMAMaterial Material)>();
            foreach (var slot in recipe.GetCachedRecipe().slotDataList)
            {
                if (slot == null) continue;
                var material = slot.material;
                if (material == null) material = slot.GetOverlay(0)?.asset?.GetMaterial();
                if (material != null) found.Add((slot.slotName, material));
            }
            return found;
        }

        private static string[] GameBodies(UmaAppearanceCatalog catalog, UMAWardrobeRecipe recipe) =>
            catalog.Bodies.Select(body => body.Id).Where(recipe.compatibleRaces.Contains).ToArray();

        private sealed class BuiltBody { public CharacterBody Body; public DynamicCharacterAvatar Avatar; }

        private static IEnumerator Build(IModularCharacterBodyProvider provider, CharacterAppearance appearance, Transform parent, BuiltBody into)
        {
            Assert.That(provider.TryCreate(new CharacterBodyRequest("custom-contract", "player", appearance, CharacterBuildPurpose.Studio, 1),
                parent, Color.white, out var body), Is.True);
            var state = body.Root.GetComponent<CharacterBodyBuildState>();
            double deadline = Time.realtimeSinceStartupAsDouble + 30;
            while (!state.Ready && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.That(state.Ready, Is.True, "The body finished building.");
            Assert.That(string.IsNullOrEmpty(state.Substitution), Is.True, "Worn as saved, with no substitute: " + state.Substitution);
            // The code-built pieces are fitted once the body is up.
            yield return null; yield return null;
            into.Body = body;
            into.Avatar = body.Root.GetComponent<DynamicCharacterAvatar>();
            Assert.That(into.Avatar, Is.Not.Null);
        }

        // ----------------------------------------------------------------------- every entry: data

        [Test]
        public void EveryEntrysRecipeIsInTheIndexInASlotTheGameAndItsRaceTake()
        {
            var index = UMAAssetIndexer.Instance;
            var catalog = new UmaAppearanceCatalog();
            var entries = Entries();
            Assert.That(entries, Is.Not.Empty, "The game loads its wardrobe catalogs from Resources/" + CatalogFolder + ".");
            foreach (var entry in entries.Where(entry => entry.available))
            {
                var recipe = index.GetAsset<UMAWardrobeRecipe>(entry.recipeName);
                Assert.That(recipe, Is.Not.Null, entry.id + ": its recipe '" + entry.recipeName + "' is not in UMA's index.");
                Assert.That(WardrobeSlots.Uma, Does.Contain(recipe.wardrobeSlot), entry.id + " is in a slot the game does not dress.");
                var bodies = GameBodies(catalog, recipe);
                Assert.That(bodies, Is.Not.Empty, entry.id + " fits neither of the game's bodies.");
                foreach (string body in bodies)
                    Assert.That(index.GetRace(body).wardrobeSlots, Does.Contain(recipe.wardrobeSlot),
                        entry.id + ": " + body + " has no " + recipe.wardrobeSlot + " slot.");
            }
        }

        [Test]
        public void EveryEntryHasItsIdAndAliasesInPlace()
        {
            var catalog = new UmaAppearanceCatalog();
            Assert.That(catalog.Diagnostics, Is.Empty, "Every entry loads: valid and unique ids, aliases and recipe names.");
            foreach (var entry in Entries())
            {
                Assert.That(entry.id.StartsWith("gamesim.", StringComparison.Ordinal) || entry.id.StartsWith("uma-", StringComparison.Ordinal),
                    Is.True, entry.id + " is in neither the game's namespace nor the installed UMA one.");
                var aliases = entry.aliases ?? new List<string>();
                Assert.That(aliases, Does.Contain(entry.recipeName), entry.id + " keeps its recipe name as an alias.");
                var item = catalog.Items.FirstOrDefault(value => value.Id == entry.id);
                if (item == null) continue;
                foreach (string key in aliases.Concat(new[] { entry.recipeName }))
                    Assert.That(AppearanceEditing.Find(catalog, key), Is.SameAs(item), entry.id + ": '" + key + "' finds it.");
            }
        }

        /// <summary>
        /// A page draws all its rows at once, and tests and screen readers find a control by its
        /// caption, so a label names one item per page and body. Two items of one family (styleGroup)
        /// may share a label - the house's stock pairs do, and a delivered part is a family of its own,
        /// so any clash with it is caught.
        /// </summary>
        [Test]
        public void EveryLabelIsUniqueOnItsCreatorPage()
        {
            var catalog = new UmaAppearanceCatalog();
            var codeBuilt = new HashSet<string>(ProceduralAccessories.Items.Select(item => item.Label), StringComparer.Ordinal);
            foreach (var item in catalog.Items.Where(item => !UmaAppearanceCatalog.IsGrown(item.Id)))
            {
                Assert.That(Outfits.Concat(Pages), Does.Not.Contain(item.Label), item.Id + "'s label is a creator caption.");
                Assert.That(codeBuilt, Does.Not.Contain(item.Label), item.Id + "'s label is a code-built piece's.");
            }
            foreach (var body in catalog.Bodies)
                foreach (var page in PageSlots)
                {
                    var shown = catalog.Items.Where(item => page.Value.Contains(item.Slot) && item.Fits(body.Id)).ToList();
                    foreach (var item in shown)
                    {
                        var clash = shown.FirstOrDefault(other => other != item && other.Label == item.Label
                            && (string.IsNullOrEmpty(item.StyleGroup) || other.StyleGroup != item.StyleGroup));
                        Assert.That(clash, Is.Null, item.Id + " and " + clash?.Id + " are both '" + item.Label + "' on the "
                            + page.Key + " page for " + body.Label + ".");
                    }
                }
        }

        /// <summary>
        /// A marked finish works only on a material the part has to itself: UMA draws Atlas slots that
        /// share a UMAMaterial (by objectName) with one material. UseExistingMaterial draws the project
        /// asset itself, which the flattening pass would change for every character: such a part of the
        /// game's own content carries the mark, and a marked material is always of that type.
        /// </summary>
        [UnityTest]
        public IEnumerator EveryAuthoredFinishPartHasAMaterialOfItsOwn()
        {
            owned.Add(new GameObject("Contract provider", typeof(GamesimUmaCast)));
            yield return null;
            var index = UMAAssetIndexer.Instance;
            var catalog = new UmaAppearanceCatalog();
            var marked = new List<UMAMaterial>();
            foreach (var entry in Entries().Where(entry => entry.available))
            {
                var recipe = index.GetAsset<UMAWardrobeRecipe>(entry.recipeName);
                if (recipe == null) continue;
                foreach (var (slot, material) in SlotMaterials(recipe))
                {
                    bool keeps = UmaStylizer.KeepsAuthoredFinish(material.material);
                    if (Authored(entry) && material.materialType == UMAMaterial.MaterialType.UseExistingMaterial)
                        Assert.That(keeps, Is.True, entry.id + ": " + slot + " draws its project material itself (UseExistingMaterial), which "
                            + "only metal jewellery and watches may, and they must carry " + UmaStylizer.AuthoredPbrTag + " = "
                            + UmaStylizer.AuthoredPbrVersion + ". Cloth, hair, shoes and caps use an Atlas or NoAtlas material.");
                    if (!keeps) continue;
                    marked.Add(material);
                    // Ruling C8: a marked finish sits only on a UseExistingMaterial material, as contract item 12 says.
                    Assert.That(material.materialType, Is.EqualTo(UMAMaterial.MaterialType.UseExistingMaterial),
                        entry.id + ": a marked finish is drawn from the part's own project material (UseExistingMaterial), never an atlas or a NoAtlas copy.");
                    Assert.That(MarkedFinishSlots, Does.Contain(recipe.wardrobeSlot),
                        entry.id + ": the finish mark is for metal jewellery and watches.");
                    var item = catalog.Items.FirstOrDefault(value => value.Id == entry.id);
                    if (item != null) Assert.That(item.ColorChannels, Is.Empty, entry.id + ": a marked part takes no colour.");
                }
            }
            if (marked.Count == 0) yield break;
            foreach (var other in index.GetAllAssets<UMAMaterial>().Where(value => value != null && !UmaStylizer.KeepsAuthoredFinish(value.material)))
                foreach (var material in marked)
                    Assert.That(other.objectName, Is.Not.EqualTo(material.objectName),
                        other.name + " is unmarked and shares " + material.objectName + " with a marked material.");
        }

        [Test]
        public void EveryAccessoryItemDeclaresNoDyeChannel()
        {
            var catalog = new UmaAppearanceCatalog();
            foreach (var item in catalog.Items.Where(item => WardrobeSlots.UmaAccessories.Contains(item.Slot)))
                Assert.That(item.ColorChannels.Except(PersonColours), Is.Empty,
                    item.Id + " is an accessory: a shared colour of its own would draw a swatch row and save as an outfit colour.");
        }

        [Test]
        public void NoHelmetRecipeSuppressesHair()
        {
            var index = UMAAssetIndexer.Instance;
            foreach (var entry in Entries().Where(entry => entry.available))
            {
                var recipe = index.GetAsset<UMAWardrobeRecipe>(entry.recipeName);
                if (recipe == null) continue;
                var suppressed = recipe.suppressWardrobeSlots ?? new List<string>();
                if (recipe.wardrobeSlot == "Helmet")
                    Assert.That(suppressed, Does.Not.Contain("Hair"), entry.id + ": UMA would hide all the hair, and the creator would drop the hair choice.");
                Assert.That(suppressed.Intersect(WardrobeSlots.CodeBuiltAccessories), Is.Empty,
                    entry.id + ": the game pairs the code-built places itself.");
            }
        }

        [Test]
        public void AnUnavailableOrAbsentRecipeIsNeverAnItem()
        {
            var index = UMAAssetIndexer.Instance;
            var catalog = new UmaAppearanceCatalog();
            foreach (var entry in Entries())
            {
                if (entry.available && index.GetAsset<UMAWardrobeRecipe>(entry.recipeName) != null) continue;
                Assert.That(catalog.Items.Any(item => item.Id == entry.id), Is.False, entry.id + " is offered.");
                Assert.That(AppearanceEditing.Find(catalog, entry.id), Is.Null, entry.id);
            }
        }

        // ------------------------------------------------------------------------------- the looks

        [Test]
        public void EveryLibraryLookMaterializesEachIdIntoItsRecipesSlot() => EachIdMaterializesIntoItsRecipesSlot(new UmaAppearanceCatalog());

        private static void EachIdMaterializesIntoItsRecipesSlot(UmaAppearanceCatalog catalog)
        {
            var index = UMAAssetIndexer.Instance;
            foreach (string id in UmaCastLibrary.AppearanceIds)
                foreach (var worn in catalog.Materialize(CharacterAppearance.Preset(id)).outfits.SelectMany(outfit => outfit.wardrobe))
                {
                    Assert.That(worn.slot, Does.Not.StartWith("Missing-"), id + " wears '" + worn.itemId + "', which does not resolve.");
                    if (UmaAppearanceCatalog.IsGrown(worn.itemId)) continue;
                    var recipe = index.GetAsset<UMAWardrobeRecipe>(catalog.ResolveRecipeName(worn.itemId));
                    Assert.That(recipe?.wardrobeSlot, Is.EqualTo(worn.slot), id + ": " + worn.itemId);
                }
        }

        [Test]
        public void NoLibraryLookWearsAUmaItemAndItsCodeBuiltTwin() => NoLookWearsAUmaItemAndItsTwin(new UmaAppearanceCatalog());

        private static void NoLookWearsAUmaItemAndItsTwin(UmaAppearanceCatalog catalog)
        {
            foreach (string id in UmaCastLibrary.AppearanceIds)
                foreach (var outfit in catalog.Materialize(CharacterAppearance.Preset(id)).outfits)
                {
                    var slots = outfit.wardrobe.Select(worn => worn.slot).ToArray();
                    foreach (string slot in slots)
                        Assert.That(WardrobeSlots.Twins(slot).Intersect(slots), Is.Empty, id + " wears two pieces on the place of " + slot + ".");
                }
        }

        [Test]
        public void ALibraryLookNamingACatalogIdMaterializesIntoTheRecipesSlot()
        {
            var catalog = new UmaAppearanceCatalog();
            // An item whose saved id is not its recipe's name: the id the index cannot find by itself.
            var item = catalog.Items.First(value => !UmaAppearanceCatalog.IsGrown(value.Id) && value.Slot == "Chest"
                && value.Id != catalog.ResolveRecipeName(value.Id) && value.CompatibleBodies.Length > 0);
            string race = item.CompatibleBodies[0];
            var underwear = catalog.Items.Where(value => !UmaAppearanceCatalog.IsGrown(value.Id) && value.Fits(race)
                    && (value.Slot == "TopUnderlayer" || value.Slot == "BottomUnderlayer"))
                .GroupBy(value => value.Slot).Select(group => group.First().Id);
            var looks = (Dictionary<string, UmaCastLook>)typeof(UmaCastLibrary)
                .GetField("Looks", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            string key = "contract-look-" + Guid.NewGuid().ToString("N");
            looks.Add(key, new UmaCastLook { Race = race, Wardrobe = new[] { item.Id }.Concat(underwear).ToArray() });
            try
            {
                var outfit = catalog.Materialize(CharacterAppearance.Preset(key)).outfits.Single();
                var worn = outfit.wardrobe.Single(value => value.itemId == item.Id);
                Assert.That(worn.slot, Is.EqualTo(item.Slot), "A look naming " + item.Id + " wears it in its recipe's slot.");
            }
            finally { looks.Remove(key); }
        }

        // ----------------------------------------------------------------- runtime-only content

#if UNITY_EDITOR
        /// <summary>
        /// Runtime-only copies of installed recipes registered in UMA's index, with a catalog of their
        /// own, as newly authored content would arrive. No AssetDatabase write and no lasting index edit.
        /// </summary>
        private sealed class RuntimeContent : IDisposable
        {
            public readonly UMAAssetIndexer Index = UMAAssetIndexer.Instance;
            public readonly UmaWardrobeCatalog Extension = ScriptableObject.CreateInstance<UmaWardrobeCatalog>();
            private readonly List<UMAWardrobeRecipe> added = new List<UMAWardrobeRecipe>();
            private readonly List<OverlayDataAsset> addedOverlays = new List<OverlayDataAsset>();
            private readonly int originalCount;

            public RuntimeContent()
            {
                Extension.name = "Contract fixture content";
                originalCount = Index.SerializedItems.Count;
            }

            public UMAWardrobeRecipe Copy(string sourceRecipe, string kind, string slot = null)
            {
                var recipe = Object.Instantiate(Index.GetAsset<UMAWardrobeRecipe>(sourceRecipe));
                recipe.name = "GamesimContract-" + kind + "-" + Guid.NewGuid().ToString("N");
                recipe.hideFlags = HideFlags.DontSave;
                if (slot != null) recipe.wardrobeSlot = slot;
                added.Add(recipe);
                Assert.That(Index.AddAssetItem(new AssetItem(typeof(UMAWardrobeRecipe), recipe.name, "", recipe), noDirty: true), Is.True);
                return recipe;
            }

            /// <summary>A runtime copy of an installed overlay under a name of its own, registered in UMA's index.</summary>
            public OverlayDataAsset Overlay(string sourceOverlay, string kind)
            {
                var stock = Index.GetAsset<OverlayDataAsset>(sourceOverlay);
                Assert.That(stock, Is.Not.Null, sourceOverlay + " is in UMA's index.");
                var overlay = Object.Instantiate(stock);
                overlay.name = overlay._oldOverlayName = "GamesimContract-" + kind + "-" + Guid.NewGuid().ToString("N");
                overlay.hideFlags = HideFlags.DontSave;
                addedOverlays.Add(overlay);
                Assert.That(Index.AddAssetItem(new AssetItem(typeof(OverlayDataAsset), overlay.name, "", overlay), noDirty: true), Is.True);
                return overlay;
            }

            /// <summary>
            /// A painted face piece in the form the game wears one: a copy of UMA's stock face recipe, whose one slot
            /// entry is a placeholder on the head slot, tagged <paramref name="tag"/> and carrying two overlays made from
            /// the stock one: a skin layer sharing the colour Skin and a features layer on a colour of its own (white
            /// multiply, zero additive). Its catalog entry is picker-hidden, so it is held to the authored part rules.
            /// </summary>
            public UmaWardrobeEntry FacePiece(string id, string tag)
            {
                var skin = Overlay(FaceSourceOverlay, "FaceSkin");
                var features = Overlay(FaceSourceOverlay, "FaceFeatures");
                var recipe = Copy(FaceSource, "Face");
                var packed = recipe.PackedLoad();
                var part = packed.slotsV3.Single(value => value != null && !string.IsNullOrEmpty(value.id));
                Assert.That(part.isPlaceholderSlot && part.id == HeadSlot && part.overlays != null && part.overlays.Length == 1
                    && part.overlays[0].colorIdx == 0 && packed.sharedColorCount == 1 && packed.fColors[0].name == "Skin", Is.True,
                    "UMA's stock " + FaceSource + " is still one placeholder entry on " + HeadSlot + " with one overlay on the shared colour Skin.");
                part.Tags = new[] { tag };
                part.overlays[0].id = skin.overlayName;
                var own = JsonUtility.FromJson<UMAPackedRecipeBase.PackedOverlayDataV3>(JsonUtility.ToJson(part.overlays[0]));
                own.id = features.overlayName;
                own.colorIdx = packed.fColors.Length;
                part.overlays = new[] { part.overlays[0], own };
                int channels = Math.Max(1, packed.fColors[0].colors.Length / 8);
                var white = Enumerable.Range(0, channels).SelectMany(_ => new short[] { 255, 255, 255, 255, 0, 0, 0, 0 }).ToArray();
                packed.fColors = packed.fColors.Concat(new[]
                {
                    new UMAPackedRecipeBase.PackedOverlayColorDataV3 { name = OverlayColorData.UNSHARED, colors = white, displayColor = -1 },
                }).ToArray();
                recipe.PackedSave(packed);
                var entry = new UmaWardrobeEntry { id = id, recipeName = recipe.name, label = "Contract " + id, pickerHidden = true };
                Extension.entries.Add(entry);
                return entry;
            }

            public UmaAppearanceCatalog Catalog() =>
                new UmaAppearanceCatalog(Resources.LoadAll<UmaWardrobeCatalog>(CatalogFolder).Concat(new[] { Extension }));

            public void Dispose()
            {
                foreach (var recipe in added) Index.RemoveAsset(typeof(UMAWardrobeRecipe), recipe.name, refresh: false);
                foreach (var overlay in addedOverlays) Index.RemoveAsset(typeof(OverlayDataAsset), overlay.name, refresh: false);
                while (Index.SerializedItems.Count > originalCount && Index.SerializedItems.Last() == null)
                    Index.SerializedItems.RemoveAt(Index.SerializedItems.Count - 1);
                foreach (var recipe in added) Object.Destroy(recipe);
                foreach (var overlay in addedOverlays) Object.Destroy(overlay);
                Object.Destroy(Extension);
            }
        }

        /// <summary>UMA's stock face recipe in the placeholder form (one entry on the head slot, tagged Face) and its overlay.</summary>
        private const string FaceSource = "Freckled_Light_U_Wardrobe", FaceSourceOverlay = "Freckled_Light_U_Overlay", HeadSlot = "UMA30_Body_UDIM1001_slot";

        /// <summary>Builds a body wearing <paramref name="entry"/>'s item after a save and a reload, on the first body it fits.</summary>
        private IEnumerator WearOne(UmaAppearanceCatalog catalog, UmaWardrobeEntry entry, Transform parent, BuiltBody into)
        {
            var item = catalog.Items.FirstOrDefault(value => value.Id == entry.id);
            Assert.That(item, Is.Not.Null, entry.id + " is an item.");
            var appearance = catalog.ChangeBody(catalog.Materialize(CharacterAppearance.Preset("player")), item.CompatibleBodies[0]);
            AppearanceEditing.Wear(appearance, item, catalog);
            var loaded = JsonUtility.FromJson<CharacterAppearance>(JsonUtility.ToJson(appearance));
            yield return Build(new UmaBodyProvider(catalog), loaded, parent, into);
            Assert.That(into.Avatar.GetWardrobeItem(item.Slot)?.name, Is.EqualTo(entry.recipeName), entry.id + " is worn.");
        }

        /// <summary>
        /// The placeholder contract on a face piece made from UMA's stock face recipe: its files pass (no slot of its own
        /// is asked of it), and on a body, after a save and a reload and through a rebuild, both its overlays are on a
        /// built slot tagged Face, the skin layer sharing Skin and the features layer keeping its own white.
        /// </summary>
        [UnityTest]
        public IEnumerator APlaceholderFacePieceIsHeldToThePlaceholderContract()
        {
            owned.Add(new GameObject("Contract provider", typeof(GamesimUmaCast)));
            yield return null;
            using (var content = new RuntimeContent())
            {
                var entry = content.FacePiece("contract-face", "Face");
                var catalog = content.Catalog();
                Assert.That(catalog.Diagnostics, Is.Empty);
                Assert.That(Authored(entry), Is.True, "The piece is held to the authored part rules.");
                var recipe = content.Index.GetAsset<UMAWardrobeRecipe>(entry.recipeName);
                Assert.That(SlotIds(recipe), Is.Empty, "A placeholder entry is not the part's own slot.");
                var placeholder = Placeholders(recipe).Single();
                Assert.That(placeholder.Id, Is.EqualTo(HeadSlot));
                Assert.That(placeholder.Tags, Is.EqualTo(new[] { "Face" }));
                Assert.That(placeholder.Overlays.Select(overlay => overlay.KnownColour + " " + overlay.Shared + " " + overlay.Colour),
                    Is.EqualTo(new[] { "True True Skin", "True False " + OverlayColorData.UNSHARED }), "A skin layer sharing Skin and a features layer on its own colour.");
                Assert.That(placeholder.Overlays[1].Multiply, Is.EqualTo(Color.white));
                Assert.That(placeholder.Overlays[1].Additive, Is.EqualTo(new Color(0f, 0f, 0f, 0f)));
                AssertPartFiles(content.Index, entry);
                yield return WearEachPart(catalog, new[] { entry });
            }
            yield return null;
        }

        /// <summary>
        /// The control: a face piece whose placeholder is tagged with a tag no built slot carries is still worn, but
        /// UMA hands its overlays to no slot, and the contract says so for each overlay.
        /// </summary>
        [UnityTest]
        public IEnumerator APlaceholderWhoseOverlaysReachNoTaggedSlotFailsThePlaceholderContract()
        {
            var cast = new GameObject("Contract provider", typeof(GamesimUmaCast));
            var actor = new GameObject("Contract actor");
            owned.Add(actor); owned.Add(cast);
            yield return null;
            using (var content = new RuntimeContent())
            {
                var entry = content.FacePiece("contract-face-untagged", "GamesimContractNoSuchTag");
                var catalog = content.Catalog();
                Assert.That(catalog.Diagnostics, Is.Empty);
                var built = new BuiltBody();
                yield return WearOne(catalog, entry, actor.transform, built);
                var placeholders = Placeholders(content.Index.GetAsset<UMAWardrobeRecipe>(entry.recipeName));
                var misses = PlaceholderMisses(built.Avatar, entry.id, placeholders, "on its first build");
                var overlays = placeholders.Single().Overlays;
                Assert.That(overlays.Count, Is.EqualTo(2));
                foreach (var overlay in overlays)
                    Assert.That(misses.Any(miss => miss.Contains(overlay.Id) && miss.Contains("is on no built slot tagged GamesimContractNoSuchTag")), Is.True,
                        "The contract reports that " + overlay.Id + " reached no tagged slot; it reported: " + string.Join(" | ", misses));
                Assert.That(misses.Count, Is.EqualTo(overlays.Count), string.Join(" | ", misses));
            }
            yield return null;
        }

        /// <summary>
        /// The control for the colours: on a body wearing the face piece the contract holds; once the built skin layer
        /// stops sharing Skin and the built features layer loses its own white, the contract reports each.
        /// </summary>
        [UnityTest]
        public IEnumerator APlaceholderOverlayThatLosesItsColourFailsThePlaceholderContract()
        {
            var cast = new GameObject("Contract provider", typeof(GamesimUmaCast));
            var actor = new GameObject("Contract actor");
            owned.Add(actor); owned.Add(cast);
            yield return null;
            using (var content = new RuntimeContent())
            {
                var entry = content.FacePiece("contract-face-colours", "Face");
                var catalog = content.Catalog();
                Assert.That(catalog.Diagnostics, Is.Empty);
                var built = new BuiltBody();
                yield return WearOne(catalog, entry, actor.transform, built);
                var placeholders = Placeholders(content.Index.GetAsset<UMAWardrobeRecipe>(entry.recipeName));
                var before = PlaceholderMisses(built.Avatar, entry.id, placeholders, "on its first build");
                Assert.That(before, Is.Empty, "Control: the body keeps the contract. " + string.Join(" | ", before));

                var skin = placeholders.Single().Overlays[0];
                var features = placeholders.Single().Overlays[1];
                var worn = built.Avatar.umaData.umaRecipe.slotDataList.Where(slot => slot != null).SelectMany(slot => slot.GetOverlayList())
                    .Where(overlay => overlay != null && overlay.asset != null).ToList();
                foreach (var overlay in worn.Where(value => value.overlayName == skin.Id))
                {
                    var unshared = overlay.colorData.Clone();
                    unshared.name = OverlayColorData.UNSHARED;
                    overlay.colorData = unshared;
                }
                foreach (var overlay in worn.Where(value => value.overlayName == features.Id))
                {
                    var red = overlay.colorData.Clone();
                    red.channelMask[0] = Color.red;
                    overlay.colorData = red;
                }
                var after = PlaceholderMisses(built.Avatar, entry.id, placeholders, "once repainted");
                Assert.That(after.Any(miss => miss.Contains(skin.Id) && miss.Contains("no longer shares the colour Skin")), Is.True, string.Join(" | ", after));
                Assert.That(after.Any(miss => miss.Contains(features.Id) && miss.Contains("does not keep its own colour")), Is.True, string.Join(" | ", after));
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator AHiddenEntryIsAnItemThatNoPickerOffersAndNoSubstitutionPicks()
        {
            yield return null;
            using (var content = new RuntimeContent())
            {
                var installed = new UmaAppearanceCatalog();
                string female = UmaCastLibrary.FemaleRace, male = UmaCastLibrary.MaleRace;
                // A family with an offered piece for each body: the hidden copy joins it, cheaper than any
                // of them, so without the switch it would be every substitution's first pick.
                var maleItem = installed.Items.First(value => !UmaAppearanceCatalog.IsGrown(value.Id) && value.Slot == "Chest"
                    && !string.IsNullOrEmpty(value.StyleGroup) && value.Fits(male) && !value.Fits(female) && !value.PickerHidden
                    && installed.Items.Any(other => other.StyleGroup == value.StyleGroup && other.Fits(female) && !other.Fits(male) && !other.PickerHidden));
                var source = installed.Items.First(value => value.StyleGroup == maleItem.StyleGroup && value.Fits(female) && !value.Fits(male) && !value.PickerHidden);
                var recipe = content.Copy(installed.ResolveRecipeName(source.Id), "Hidden");
                content.Extension.entries.Add(new UmaWardrobeEntry
                {
                    id = "contract-hidden", recipeName = recipe.name, label = "Contract hidden", styleGroup = source.StyleGroup,
                    tags = source.Tags.ToList(), fallbackPriority = 0, pickerHidden = true,
                });
                var catalog = content.Catalog();
                Assert.That(catalog.Diagnostics, Is.Empty);
                var hidden = AppearanceEditing.Find(catalog, "contract-hidden");
                Assert.That(hidden, Is.Not.Null, "A hidden entry is an item: a look and a save can wear it.");
                Assert.That(hidden.PickerHidden, Is.True);
                Assert.That(AppearanceEditing.Find(catalog, recipe.name), Is.SameAs(hidden));

                Assert.That(AppearanceEditing.Choices(catalog, new[] { "Chest" }, female), Has.No.Member(hidden), "No picker offers it.");
                Assert.That(AppearanceEditing.Choices(catalog, new[] { "Chest" }, female, new[] { hidden.Id }), Has.Member(hidden),
                    "Its own look's row does.");
                foreach (string from in new[] { source.Id, maleItem.Id })
                {
                    var pick = catalog.FindEquivalent(from, "Chest", female);
                    Assert.That(pick, Is.Not.Null, from);
                    Assert.That(pick, Is.Not.SameAs(hidden), "No substitution for " + from + " picks a hidden part.");
                }

                var wearing = catalog.ChangeBody(catalog.Materialize(CharacterAppearance.Preset("player")), female);
                AppearanceEditing.Wear(wearing, hidden, catalog);
                Assert.That(catalog.ChangeBody(wearing, female).outfits.SelectMany(outfit => outfit.wardrobe).Select(worn => worn.itemId),
                    Does.Contain(hidden.Id), "It stays worn on a body it fits.");
                foreach (var worn in catalog.ChangeBody(wearing, male).outfits.SelectMany(outfit => outfit.wardrobe).Where(worn => worn.slot == "Chest"))
                {
                    var item = AppearanceEditing.Find(catalog, worn.itemId);
                    Assert.That(item.PickerHidden, Is.False, "Changing body never substitutes a hidden part.");
                    Assert.That(item.Fits(male), Is.True);
                }
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator AnUnavailableOrAbsentRuntimeRecipeIsNeverAnItem()
        {
            yield return null;
            using (var content = new RuntimeContent())
            {
                var installed = new UmaAppearanceCatalog();
                var source = installed.Items.First(value => !UmaAppearanceCatalog.IsGrown(value.Id) && value.Slot == "Chest");
                var recipe = content.Copy(installed.ResolveRecipeName(source.Id), "Retired");
                string absent = "GamesimContract-Absent-" + Guid.NewGuid().ToString("N");
                content.Extension.entries.Add(new UmaWardrobeEntry { id = "contract-retired", recipeName = recipe.name, label = "Contract retired", available = false });
                content.Extension.entries.Add(new UmaWardrobeEntry { id = "contract-absent", recipeName = absent, label = "Contract absent" });
                var catalog = content.Catalog();
                Assert.That(catalog.Diagnostics, Is.Empty);
                Assert.That(content.Index.GetAsset<UMAWardrobeRecipe>(recipe.name), Is.Not.Null, "The retired part is installed.");
                foreach (string id in new[] { "contract-retired", "contract-absent" })
                {
                    Assert.That(AppearanceEditing.Find(catalog, id), Is.Null, id + " is never an item.");
                    Assert.That(catalog.Items.Any(item => item.Id == id), Is.False, id);
                }
                Assert.That(catalog.ResolveRecipeName("contract-retired"), Is.EqualTo(recipe.name), "A save naming it still finds its recipe.");
            }
            yield return null;
        }

        // The two look walkers again with pieces tagged for one library look in the catalog (UmaAllStarPiecesPlayModeTests).
        private UmaAllStarPiecesPlayModeTests.Pieces pieces;

        [TearDown]
        public void RemoveTaggedPieces() { pieces?.Dispose(); pieces = null; }

        [UnityTest]
        public IEnumerator EveryLibraryLookMaterializesEachIdIntoItsRecipesSlotWithATaggedPiece()
        {
            yield return null;
            pieces = UmaAllStarPiecesPlayModeTests.TaggedForStar(new UmaAppearanceCatalog(), duplicate: false);
            EachIdMaterializesIntoItsRecipesSlot(UmaAllStarPiecesPlayModeTests.TaggedCatalog(pieces));
            var removed = pieces;
            RemoveTaggedPieces();
            UmaAllStarPiecesPlayModeTests.AssertNothingLeft(removed);
        }

        [UnityTest]
        public IEnumerator NoLibraryLookWearsAUmaItemAndItsCodeBuiltTwinWithATaggedPiece()
        {
            yield return null;
            pieces = UmaAllStarPiecesPlayModeTests.TaggedForStar(new UmaAppearanceCatalog(), duplicate: false);
            NoLookWearsAUmaItemAndItsTwin(UmaAllStarPiecesPlayModeTests.TaggedCatalog(pieces));
            var removed = pieces;
            RemoveTaggedPieces();
            UmaAllStarPiecesPlayModeTests.AssertNothingLeft(removed);
        }

        [UnityTest]
        public IEnumerator AnEarsItemIsWornAndTheCodeBuiltEarringsAreNot()
        {
            var cast = new GameObject("Contract provider", typeof(GamesimUmaCast));
            var actor = new GameObject("Contract actor");
            owned.Add(actor); owned.Add(cast);
            yield return null;
            using (var content = new RuntimeContent())
            {
                var installed = new UmaAppearanceCatalog();
                string female = UmaCastLibrary.FemaleRace;
                var appearance = installed.ChangeBody(installed.Materialize(CharacterAppearance.Preset("player")), female);
                var outfit = AppearanceEditing.Outfit(appearance);
                // Any top the look does not wear, re-slotted as an Ears part: geometry the body build can dress.
                var source = installed.Items.First(value => !UmaAppearanceCatalog.IsGrown(value.Id) && value.Slot == "Chest"
                    && value.Fits(female) && !value.PickerHidden && outfit.wardrobe.All(worn => worn.itemId != value.Id));
                var recipe = content.Copy(installed.ResolveRecipeName(source.Id), "Ears", "Ears");
                content.Extension.entries.Add(new UmaWardrobeEntry { id = "contract-ears", recipeName = recipe.name, label = "Contract ears", styleGroup = "contract-ears" });
                var catalog = content.Catalog();
                Assert.That(catalog.Diagnostics, Is.Empty);
                var ears = AppearanceEditing.Find(catalog, "contract-ears");
                Assert.That(ears?.Slot, Is.EqualTo("Ears"));

                // A save that names both pieces of one place, as a library look or an older save may. Written
                // directly: wearing either through the creator takes the other off.
                string hoops = ProceduralAccessories.Prefix + "hoops", glasses = ProceduralAccessories.Prefix + "glasses";
                outfit.wardrobe.RemoveAll(worn => WardrobeSlots.IsAccessory(worn.slot));
                outfit.wardrobe.Add(new AppearanceWardrobe { slot = "Earrings", itemId = hoops });
                outfit.wardrobe.Add(new AppearanceWardrobe { slot = "Eyewear", itemId = glasses });
                outfit.wardrobe.Add(new AppearanceWardrobe { slot = "Ears", itemId = ears.Id });
                Assert.That(appearance.TryValidate(out var error), Is.True, error);
                var loaded = JsonUtility.FromJson<CharacterAppearance>(JsonUtility.ToJson(appearance));
                string saved = JsonUtility.ToJson(loaded);

                var built = new BuiltBody();
                yield return Build(new UmaBodyProvider(catalog), loaded, actor.transform, built);
                Assert.That(built.Avatar.GetWardrobeItem("Ears")?.name, Is.EqualTo(recipe.name), "The UMA part is worn in Ears.");
                var pieces = built.Body.Root.GetComponentsInChildren<GrownPiece>().Select(piece => piece.ItemId).ToArray();
                Assert.That(pieces, Does.Contain(glasses), "The code-built pieces were fitted; a place with no UMA twin keeps its own.");
                Assert.That(pieces, Does.Not.Contain(hoops), "The code-built earrings are not worn under the UMA part on the same place.");
                Assert.That(JsonUtility.ToJson(loaded), Is.EqualTo(saved), "Dressing never rewrites the saved choice.");
            }
            yield return null;
        }
#endif

        // --------------------------------------------------------- the game's own content: files

        /// <summary>The second tier must hold something, or its checks would pass on nothing.</summary>
        [Test]
        public void TheGamesOwnContentIsHeldToThePartRules()
        {
            Assert.That(Entries().Where(entry => entry.available && Authored(entry)), Is.Not.Empty,
                "No entry is the game's own content (an id starting gamesim., or picker-hidden).");
        }

        [Test]
        public void EveryAuthoredPartsSlotsCarryTheirOwnNameAndHaveVertices()
        {
            var index = UMAAssetIndexer.Instance;
            foreach (var entry in Entries().Where(entry => entry.available && Authored(entry))) AssertPartFiles(index, entry);
        }

        /// <summary>
        /// One entry's files: each slot of its own is in UMA's index under its name, with vertices; each placeholder
        /// entry names a tag to find built slots by and carries overlays that are all in UMA's index.
        /// </summary>
        private void AssertPartFiles(UMAAssetIndexer index, UmaWardrobeEntry entry)
        {
            var recipe = index.GetAsset<UMAWardrobeRecipe>(entry.recipeName);
            Assert.That(recipe, Is.Not.Null, entry.id);
            var slots = SlotIds(recipe);
            var placeholders = Placeholders(recipe);
            Assert.That(slots.Length + placeholders.Count, Is.GreaterThan(0), entry.id + " builds no slot.");
            foreach (string id in slots)
            {
                var slot = index.GetAsset<SlotDataAsset>(id);
                Assert.That(slot, Is.Not.Null, entry.id + ": slot " + id + " is not in UMA's index.");
                Assert.That(slot.meshData, Is.Not.Null, entry.id + ": " + id + " has no mesh.");
                Assert.That(slot.meshData.vertexCount, Is.GreaterThan(0), entry.id + ": " + id + " has no vertices.");
                string label = meshLabels != null && meshLabels.TryGetValue(slot.slotName, out var before) ? before : slot.meshData.SlotName;
                Assert.That(label, Is.EqualTo(slot.slotName), entry.id + ": the mesh of " + id + " carries another slot's name.");
            }
            foreach (var placeholder in placeholders)
            {
                Assert.That(placeholder.Tags.Where(tag => !string.IsNullOrEmpty(tag)), Is.Not.Empty,
                    entry.id + ": the placeholder entry " + placeholder.Id + " names no tag for its overlays to find a built slot by.");
                Assert.That(placeholder.Overlays, Is.Not.Empty, entry.id + ": the placeholder entry " + placeholder.Id + " carries no overlay.");
                foreach (var overlay in placeholder.Overlays)
                    Assert.That(index.GetAsset<OverlayDataAsset>(overlay.Id), Is.Not.Null,
                        entry.id + ": the overlay " + overlay.Id + " of the placeholder entry " + placeholder.Id + " is not in UMA's index.");
            }
        }

        /// <summary>
        /// A thumbnail is optional (a part with none shows the kit icon). One that is named must be
        /// right: a Sprite, present, and one for each of the game's bodies the part fits.
        /// </summary>
        [Test]
        public void EveryNamedThumbnailIsASpriteForEachBodyThePartFits()
        {
            var index = UMAAssetIndexer.Instance;
            var catalog = new UmaAppearanceCatalog();
            foreach (var entry in Entries().Where(entry => entry.available && Authored(entry)))
            {
                var recipe = index.GetAsset<UMAWardrobeRecipe>(entry.recipeName);
                Assert.That(recipe, Is.Not.Null, entry.id);
                var thumbs = recipe.wardrobeRecipeThumbs ?? new List<WardrobeRecipeThumb>();
#if UNITY_EDITOR
                // A reference to a file that is gone, or to a PNG not imported as a Sprite (it has no Sprite
                // to point at), reads as null here; the inspector's test tells it from no reference at all.
                var serialized = new UnityEditor.SerializedObject(recipe);
                var rows = serialized.FindProperty("wardrobeRecipeThumbs");
                for (int i = 0; rows != null && i < rows.arraySize; i++)
                {
                    var thumb = rows.GetArrayElementAtIndex(i).FindPropertyRelative("thumb");
                    Assert.That(thumb == null || thumb.objectReferenceValue != null || thumb.objectReferenceEntityIdValue.Equals(default(EntityId)), Is.True,
                        entry.id + " names a thumbnail that is missing or is not a Sprite.");
                }
                serialized.Dispose();
#endif
                // A row with no picture names none: the part shows the kit icon in its row.
                var named = thumbs.Where(thumb => thumb != null && thumb.thumb != null).ToList();
                if (named.Count == 0) continue;
#if UNITY_EDITOR
                foreach (var thumb in named)
                {
                    string path = UnityEditor.AssetDatabase.GetAssetPath(thumb.thumb);
                    var importer = UnityEditor.AssetImporter.GetAtPath(path) as UnityEditor.TextureImporter;
                    Assert.That(importer?.textureType, Is.EqualTo(UnityEditor.TextureImporterType.Sprite), entry.id + ": " + path + " is not imported as a Sprite.");
                }
#endif
                foreach (string body in GameBodies(catalog, recipe))
                    Assert.That(named.Any(thumb => thumb.race == body), Is.True, entry.id + " names a thumbnail, but none for " + body + ".");
            }
        }

        // ---------------------------------------------------- the game's own content: on a body

        /// <summary>The material values a marked finish is authored with, read before any body is built.</summary>
        private sealed class Finish
        {
            public Material Source; public readonly Dictionary<int, float> Values = new Dictionary<int, float>();
            public Finish(Material source)
            {
                Source = source;
                foreach (int id in new[] { Metallic, Smoothness, BumpScale }) if (source.HasProperty(id)) Values[id] = source.GetFloat(id);
            }
        }

        /// <summary>
        /// Each marked material of the part is drawn with the values it was authored with. The drawn
        /// material is found by identity (UseExistingMaterial draws the asset itself), else by name (should
        /// UMA draw a copy: an Instantiate keeps the name and adds a suffix), and only failing both among
        /// the marked materials of its shader. The round builder wears at most one part with a
        /// marked finish on an avatar, so no other part's material can stand in for this one's.
        /// </summary>
        private static void AssertFinishes(DynamicCharacterAvatar avatar, string id, IEnumerable<Finish> finishes, string when)
        {
            var drawn = avatar.GetComponentsInChildren<Renderer>(true).SelectMany(renderer => renderer.sharedMaterials)
                .Where(value => value != null && UmaStylizer.KeepsAuthoredFinish(value)).ToList();
            foreach (var finish in finishes)
            {
                var own = drawn.Where(value => value == finish.Source).ToList();
                if (own.Count == 0) own = drawn.Where(value => value.name.StartsWith(finish.Source.name, StringComparison.Ordinal)).ToList();
                if (own.Count == 0) own = drawn.Where(value => value.shader == finish.Source.shader).ToList();
                Assert.That(own, Is.Not.Empty, id + ": its marked material " + finish.Source.name + " is drawn " + when + ".");
                bool Keeps(Material material) => finish.Values.All(value => Mathf.Abs(material.GetFloat(value.Key) - value.Value) <= .001f);
                Assert.That(own.Any(Keeps), Is.True, id + " keeps the authored finish of " + finish.Source.name + " " + when + ": drawn "
                    + string.Join("; ", own.Select(material => material.name + " " + string.Join(",", finish.Values.Keys.Select(key => material.GetFloat(key).ToString("0.###"))))));
            }
        }

        /// <summary>
        /// The game's own parts on a real avatar: saved and read back as a season save is, built with
        /// no substitute, each slot of the part built with vertices, and a marked finish kept on the
        /// first build and after a rebuild (a colour change rebuilds and runs the flattening again).
        /// Parts are worn several to an avatar where their places differ, one avatar per round. A placeholder
        /// entry's overlays are on a built slot carrying one of its tags, with their colours, on the first
        /// build and after the rebuild.
        /// </summary>
        [UnityTest]
        public IEnumerator EveryAuthoredPartIsWornAfterASaveAndReloadAndKeepsItsFinishThroughARebuild()
        {
            var cast = new GameObject("Contract provider", typeof(GamesimUmaCast));
            owned.Add(cast);
            yield return null;
            yield return WearEachPart(new UmaAppearanceCatalog(), Entries().Where(entry => entry.available && Authored(entry)));
        }

        /// <summary>The walk of <see cref="EveryAuthoredPartIsWornAfterASaveAndReloadAndKeepsItsFinishThroughARebuild"/> over <paramref name="entries"/>.</summary>
        private IEnumerator WearEachPart(UmaAppearanceCatalog catalog, IEnumerable<UmaWardrobeEntry> entries)
        {
            var index = UMAAssetIndexer.Instance;
            var provider = new UmaBodyProvider(catalog);
            var parts = new List<(AppearanceItem Item, string Body, string[] Slots, List<Finish> Finishes, List<Placeholder> Placeholders)>();
            foreach (var entry in entries)
            {
                var recipe = index.GetAsset<UMAWardrobeRecipe>(entry.recipeName);
                var item = catalog.Items.FirstOrDefault(value => value.Id == entry.id);
                Assert.That(item, Is.Not.Null, entry.id + " is an item.");
                var finishes = SlotMaterials(recipe).Where(slot => UmaStylizer.KeepsAuthoredFinish(slot.Material.material))
                    .Select(slot => new Finish(slot.Material.material)).ToList();
                parts.Add((item, item.CompatibleBodies[0], SlotIds(recipe), finishes, Placeholders(recipe)));
            }
            Assert.That(parts, Is.Not.Empty);
            int round = 0;
            while (parts.Count > 0)
            {
                // One avatar per body per round, wearing at most one part per place, and at most one part
                // with a marked finish: two marked metal parts usually share a shader, and each part's
                // finish is read off what the avatar draws.
                foreach (var group in parts.GroupBy(part => part.Body).ToList())
                {
                    var taken = new List<(AppearanceItem Item, string Body, string[] Slots, List<Finish> Finishes, List<Placeholder> Placeholders)>();
                    foreach (var part in group)
                        if (taken.All(other => other.Item.Slot != part.Item.Slot && !WardrobeSlots.Twins(other.Item.Slot).Contains(part.Item.Slot)
                            && (other.Finishes.Count == 0 || part.Finishes.Count == 0)))
                            taken.Add(part);
                    var appearance = catalog.ChangeBody(catalog.Materialize(CharacterAppearance.Preset("player")), group.Key);
                    foreach (var part in taken) AppearanceEditing.Wear(appearance, part.Item, catalog);
                    var loaded = JsonUtility.FromJson<CharacterAppearance>(JsonUtility.ToJson(appearance));
                    Assert.That(loaded.ContentKey(), Is.EqualTo(appearance.ContentKey()), "The save reads back unchanged.");
                    Assert.That(catalog.Materialize(loaded).ContentKey(), Is.EqualTo(loaded.ContentKey()), "Reading a save never rewrites it.");

                    var actor = new GameObject("Contract actor " + round);
                    actor.transform.position = new Vector3(round * 3f, 0f, 0f);
                    owned.Add(actor);
                    var built = new BuiltBody();
                    yield return Build(provider, loaded, actor.transform, built);
                    var slotData = built.Avatar.umaData.umaRecipe.slotDataList.Where(slot => slot != null).ToList();
                    foreach (var part in taken)
                    {
                        Assert.That(built.Avatar.GetWardrobeItem(part.Item.Slot)?.name, Is.EqualTo(catalog.ResolveRecipeName(part.Item.Id)),
                            part.Item.Id + " is worn after a save and a reload.");
                        foreach (string id in part.Slots)
                        {
                            var slot = slotData.FirstOrDefault(value => value.slotName == id);
                            Assert.That(slot, Is.Not.Null, part.Item.Id + ": " + id + " is built.");
                            Assert.That(slot.asset.meshData.vertexCount, Is.GreaterThan(0), part.Item.Id + ": " + id + " has vertices.");
                        }
                        var misses = PlaceholderMisses(built.Avatar, part.Item.Id, part.Placeholders, "on its first build");
                        Assert.That(misses, Is.Empty, string.Join(" ", misses));
                        AssertFinishes(built.Avatar, part.Item.Id, part.Finishes, "on its first build");
                    }
                    Assert.That(built.Avatar.GetRenderers().Sum(renderer => renderer != null && renderer.sharedMesh != null ? renderer.sharedMesh.vertexCount : 0),
                        Is.GreaterThan(0), "The body has a mesh.");

                    int updates = 0;
                    void Updated(UMAData data) => updates++;
                    built.Avatar.OnCharacterUpdated += Updated;
                    built.Avatar.UpdateColors(true);
                    double deadline = Time.realtimeSinceStartupAsDouble + 20;
                    while (updates == 0 && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
                    built.Avatar.OnCharacterUpdated -= Updated;
                    Assert.That(updates, Is.GreaterThan(0), "The body rebuilt.");
                    yield return null;
                    foreach (var part in taken)
                    {
                        var misses = PlaceholderMisses(built.Avatar, part.Item.Id, part.Placeholders, "after a rebuild");
                        Assert.That(misses, Is.Empty, string.Join(" ", misses));
                        AssertFinishes(built.Avatar, part.Item.Id, part.Finishes, "after a rebuild");
                    }

                    Object.Destroy(actor);
                    foreach (var part in taken) parts.Remove(part);
                    round++;
                }
            }
            yield return null;
        }

        // ------------------------------------------------------------------- the finish mark

        /// <summary>A shiny lit material, as a metal part is authored: metal, polished, with its normal map at full.</summary>
        private Material Shiny(string name, string tag = null, string value = null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            Assert.That(shader, Is.Not.Null, "The house renders with URP's Lit shader.");
            var material = new Material(shader) { name = name, hideFlags = HideFlags.DontSave };
            owned.Add(material);
            material.SetFloat(Metallic, 1f);
            material.SetFloat(Smoothness, .9f);
            material.SetFloat(BumpScale, 1f);
            if (tag != null) material.SetOverrideTag(tag, value);
            return material;
        }

        [Test]
        public void AMaterialMarkedAuthoredKeepsItsFinishAndTheOthersAreFlattened()
        {
            var marked = Shiny("Marked", UmaStylizer.AuthoredPbrTag, UmaStylizer.AuthoredPbrVersion);
            var plain = Shiny("Plain");
            var wrongValue = Shiny("Wrong value", UmaStylizer.AuthoredPbrTag, "2");
            // A name with other letters, not the mark in another case: Unity matches a tag's name without
            // regard to case, so "GamesimAuthoredPbr" reads as the mark itself and keeps its finish.
            var wrongName = Shiny("Wrong name", "GamesimAuthoredFinish", UmaStylizer.AuthoredPbrVersion);
            var root = new GameObject("Authored finish part");
            owned.Add(root);
            root.AddComponent<MeshRenderer>().sharedMaterials = new[] { marked, plain, wrongValue, wrongName };

            Assert.That(UmaStylizer.AuthoredPbrTag, Is.EqualTo("GamesimAuthoredPBR"), "The name promised to the part authors.");
            Assert.That(UmaStylizer.AuthoredPbrVersion, Is.EqualTo("1"));
            Assert.That(UmaStylizer.KeepsAuthoredFinish(null), Is.False);
            Assert.That(UmaStylizer.KeepsAuthoredFinish(marked), Is.True);
            foreach (var other in new[] { plain, wrongValue, wrongName })
                Assert.That(UmaStylizer.KeepsAuthoredFinish(other), Is.False, other.name);

            // Twice, as a rebuild runs the pass again over everything on the body.
            UmaStylizer.Apply(root, new Color(.55f, .38f, .28f));
            UmaStylizer.Apply(root, new Color(.55f, .38f, .28f));

            Assert.That(marked.GetFloat(Metallic), Is.EqualTo(1f), "A marked part keeps its metal.");
            Assert.That(marked.GetFloat(Smoothness), Is.EqualTo(.9f).Within(.0001f), "And its polish.");
            Assert.That(marked.GetFloat(BumpScale), Is.EqualTo(1f), "And its normal map.");
            foreach (var other in new[] { plain, wrongValue, wrongName })
            {
                Assert.That(other.GetFloat(Metallic), Is.EqualTo(0f), other.name + " is flattened like any other material.");
                Assert.That(other.GetFloat(Smoothness), Is.LessThanOrEqualTo(.1f), other.name);
                Assert.That(other.GetFloat(BumpScale), Is.EqualTo(0f), other.name);
            }
        }

        /// <summary>
        /// UMA draws a part that is not UseExistingMaterial with a copy of its UMAMaterial's material:
        /// Instantiate, then the shader and CopyPropertiesFromMaterial (UMAGeneratorPro). The mark has
        /// to survive that copy for a NoAtlas part to keep its finish.
        /// </summary>
        [Test]
        public void AnOverrideTagSurvivesTheCopyUmaMakesOfAMaterial()
        {
            var source = Shiny("Authored source", UmaStylizer.AuthoredPbrTag, UmaStylizer.AuthoredPbrVersion);
            var copy = Object.Instantiate(source);
            owned.Add(copy);
            copy.shader = source.shader;
            copy.CopyPropertiesFromMaterial(source);
            Assert.That(copy.GetTag(UmaStylizer.AuthoredPbrTag, false, string.Empty), Is.EqualTo(UmaStylizer.AuthoredPbrVersion));
            Assert.That(UmaStylizer.KeepsAuthoredFinish(copy), Is.True);
        }
    }
}
#endif
