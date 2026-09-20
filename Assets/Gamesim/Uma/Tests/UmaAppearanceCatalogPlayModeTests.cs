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

namespace Gamesim.Uma.Tests
{
    public sealed class UmaAppearanceCatalogPlayModeTests
    {
        [UnityTest]
        public IEnumerator MaterializedPresetsAreValidIndependentSnapshotsOfInstalledContent()
        {
            yield return null;
            var catalog = new UmaAppearanceCatalog();
            Assert.That(catalog.Bodies.Count, Is.EqualTo(2));
            Assert.That(catalog.Items.Count, Is.GreaterThan(40));
            foreach (string id in UmaCastLibrary.AppearanceIds)
            {
                var snapshot = catalog.Materialize(CharacterAppearance.Preset(id));
                Assert.That(snapshot.TryValidate(out var error), Is.True, id + ": " + error);
                Assert.That(snapshot.dna.Count, Is.GreaterThan(15), "Persist actual DNA, not just a seed.");
                Assert.That(snapshot.colors.Count, Is.GreaterThanOrEqualTo(4));
                Assert.That(snapshot.outfits.Single().wardrobe.Count, Is.GreaterThan(3));
                var again = catalog.Materialize(snapshot);
                Assert.That(again.ContentKey(), Is.EqualTo(snapshot.ContentKey()), "Resolving cannot mutate a saved recipe.");
                again.dna[0].value = .991f;
                Assert.That(snapshot.dna[0].value, Is.Not.EqualTo(.991f));
            }
        }

        [UnityTest]
        public IEnumerator BodySwitchChoosesOnlyCompatibleGarmentsWithoutChangingSource()
        {
            yield return null;
            var catalog = new UmaAppearanceCatalog();
            var original = catalog.Materialize(CharacterAppearance.Preset("player"));
            string before = original.ContentKey();
            var changed = catalog.ChangeBody(original, UmaCastLibrary.FemaleRace);
            Assert.That(original.ContentKey(), Is.EqualTo(before));
            foreach (var worn in changed.outfits.Single().wardrobe)
            {
                var recipe = UMAAssetIndexer.Instance.GetAsset<UMAWardrobeRecipe>(catalog.ResolveRecipeName(worn.itemId));
                Assert.That(recipe.compatibleRaces, Does.Contain(UmaCastLibrary.FemaleRace), worn.itemId);
            }
        }

        [UnityTest]
        public IEnumerator AdvertisedControlsMatchEachBasesActualDnaAndIdsAcceptOldSaves()
        {
            yield return null;
            var catalog = new UmaAppearanceCatalog();
            Assert.That(catalog.Diagnostics, Is.Empty);
            Assert.That(catalog.Items.Select(item => item.Id).Distinct().Count(), Is.EqualTo(catalog.Items.Count));
            foreach (var body in catalog.Bodies)
            {
                var supported = UMAAssetIndexer.Instance.GetRace(body.Id).GetDNANames();
                foreach (var control in catalog.Controls)
                    Assert.That(control.Fits(body.Id), Is.EqualTo(supported.Contains(control.Id)), body.Id + " / " + control.Id);
            }
            foreach (var item in catalog.Items)
            {
                Assert.That(item.Id, Does.StartWith("uma-"), "Authored save IDs must not depend on asset names.");
                string recipe = catalog.ResolveRecipeName(item.Id);
                Assert.That(UMAAssetIndexer.Instance.GetAsset<UMAWardrobeRecipe>(recipe), Is.Not.Null, item.Id);
                Assert.That(AppearanceEditing.Find(catalog, recipe), Is.SameAs(item), "Schema 13 raw recipe names remain aliases.");
            }
            var appearance = catalog.Materialize(CharacterAppearance.Preset("player"));
            foreach (var worn in appearance.outfits.Single().wardrobe) worn.itemId = catalog.ResolveRecipeName(worn.itemId);
            string legacyKey = appearance.ContentKey();
            var resolved = catalog.Materialize(appearance);
            Assert.That(resolved.ContentKey(), Is.EqualTo(legacyKey), "Reading an old saved recipe must not rewrite its identifiers.");
            var selected = AppearanceEditing.Find(catalog, resolved.outfits.Single().wardrobe.First().itemId);
            Assert.That(selected, Is.Not.Null);
            AppearanceEditing.Wear(resolved, selected, catalog);
            Assert.That(resolved.outfits.Single().wardrobe.Single(value => value.slot == selected.Slot).itemId, Is.EqualTo(selected.Id));
            Assert.That(appearance.ContentKey(), Is.EqualTo(legacyKey));
        }

        [UnityTest]
        public IEnumerator CuratedEquivalentAndHistoricAliasApplyToAllOutfitsWithoutMutatingSource()
        {
            yield return null;
            var content = ScriptableObject.CreateInstance<UmaWardrobeCatalog>();
            try
            {
                content.entries = new List<UmaWardrobeEntry>
                {
                    new UmaWardrobeEntry { id = "stable-shirt-a", recipeName = "male_tshirt_white_Recipe", aliases = new List<string> { "old-mens-shirt-name" }, styleGroup = "top.tshirt", label = "White T-shirt" },
                    new UmaWardrobeEntry { id = "stable-shirt-b", recipeName = "tshirt_turquoise_Recipe", styleGroup = "top.tshirt", label = "Turquoise T-shirt" },
                    new UmaWardrobeEntry { id = "first-but-not-equivalent", recipeName = "colors_top_Recipe", styleGroup = "top.other", label = "Other top", fallbackPriority = 0 },
                };
                var catalog = new UmaAppearanceCatalog(new[] { content });
                Assert.That(catalog.ResolveRecipeName("old-mens-shirt-name"), Is.EqualTo("male_tshirt_white_Recipe"));
                var original = new CharacterAppearance { provider = "uma", bodyId = UmaCastLibrary.MaleRace };
                foreach (string outfit in new[] { "Everyday", "Formal", "Competition" })
                    original.outfits.Add(new CharacterOutfit { id = outfit, wardrobe = new List<AppearanceWardrobe>
                        { new AppearanceWardrobe { slot = "Chest", itemId = "old-mens-shirt-name" } } });
                string key = original.ContentKey();
                var changed = catalog.ChangeBody(original, UmaCastLibrary.FemaleRace);
                Assert.That(original.ContentKey(), Is.EqualTo(key));
                foreach (var outfit in changed.outfits)
                    Assert.That(outfit.wardrobe.Single().itemId, Is.EqualTo("stable-shirt-b"), outfit.id);
                string notice = AppearanceEditing.DescribeSubstitutions(original, changed, catalog);
                foreach (var outfit in original.outfits) Assert.That(notice, Does.Contain(outfit.id));
                Assert.That(notice, Does.Contain("White T-shirt → Turquoise T-shirt"));
                Assert.That(notice, Does.Contain("Undo restores every outfit"));
            }
            finally { Object.Destroy(content); }
        }

        [UnityTest]
        public IEnumerator DuplicateCatalogAliasesAreReportedInsteadOfResolvingToAnotherGarment()
        {
            yield return null;
            var content = ScriptableObject.CreateInstance<UmaWardrobeCatalog>();
            try
            {
                content.entries = new List<UmaWardrobeEntry>
                {
                    new UmaWardrobeEntry { id = "first-shirt", recipeName = "male_tshirt_white_Recipe", aliases = new List<string> { "shared-old-name" } },
                    new UmaWardrobeEntry { id = "second-shirt", recipeName = "tshirt_turquoise_Recipe", aliases = new List<string> { "shared-old-name" } },
                };
                var catalog = new UmaAppearanceCatalog(new[] { content });
                Assert.That(catalog.Diagnostics, Has.Count.EqualTo(1));
                Assert.That(catalog.ResolveRecipeName("shared-old-name"), Is.EqualTo("male_tshirt_white_Recipe"));
                Assert.That(catalog.Items.Any(item => item.Id == "second-shirt"), Is.False);
            }
            finally { Object.Destroy(content); }
        }

#if UNITY_EDITOR
        [UnityTest]
        public IEnumerator AdditionalHairAndOutfitAreDiscoverableAndBuildFromCatalogEntriesOnly()
        {
            // Runtime-only asset copies model newly authored content. No AssetDatabase writes or permanent index edits.
            var cast = new GameObject("Content smoke provider", typeof(GamesimUmaCast));
            var actor = new GameObject("Content smoke actor");
            var extension = ScriptableObject.CreateInstance<UmaWardrobeCatalog>();
            extension.name = "Smoke content extension";
            var added = new List<UMAWardrobeRecipe>();
            UMAAssetIndexer index = null;
            int originalItemCount = 0;
            try
            {
                yield return null;
                index = UMAAssetIndexer.Instance;
                originalItemCount = index.SerializedItems.Count;
                var installed = new UmaAppearanceCatalog();
                foreach (string slot in new[] { "Hair", "Chest", "Legs", "Feet" })
                {
                    var source = installed.Items.First(item => item.Slot == slot && item.Fits(UmaCastLibrary.FemaleRace)
                        && (slot != "Chest" || item.StyleGroup == "top.tshirt"));
                    var recipe = Object.Instantiate(index.GetAsset<UMAWardrobeRecipe>(installed.ResolveRecipeName(source.Id)));
                    recipe.name = "GamesimSmoke-" + slot + "-" + System.Guid.NewGuid().ToString("N");
                    recipe.hideFlags = HideFlags.DontSave;
                    added.Add(recipe);
                    Assert.That(index.AddAssetItem(new AssetItem(typeof(UMAWardrobeRecipe), recipe.name, "", recipe), noDirty: true), Is.True);
                    extension.entries.Add(new UmaWardrobeEntry
                    {
                        id = "smoke-content-" + slot, recipeName = recipe.name, label = "Smoke " + slot,
                        styleGroup = source.StyleGroup, aliases = new List<string> { "retired-smoke-" + slot },
                    });
                }
                var catalogs = Resources.LoadAll<UmaWardrobeCatalog>("Gamesim/CharacterCatalog").Concat(new[] { extension });
                var catalog = new UmaAppearanceCatalog(catalogs);
                Assert.That(catalog.Diagnostics, Is.Empty);
                var appearance = catalog.Materialize(CharacterAppearance.Preset("emma-brown"));
                appearance = catalog.ChangeBody(appearance, UmaCastLibrary.FemaleRace);
                foreach (var entry in extension.entries)
                {
                    var choice = catalog.Items.Single(item => item.Id == entry.id);
                    Assert.That(choice.Label, Is.EqualTo(entry.label));
                    Assert.That(choice.Fits(appearance.bodyId), Is.True);
                    AppearanceEditing.Wear(appearance, choice, catalog); // The exact service used by creator wardrobe cards.
                }
                Assert.That(appearance.TryValidate(out var error), Is.True, error);
                var loaded = JsonUtility.FromJson<CharacterAppearance>(JsonUtility.ToJson(appearance));
                Assert.That(loaded.ContentKey(), Is.EqualTo(appearance.ContentKey()));
                var provider = new UmaBodyProvider(catalog);
                Assert.That(provider.TryCreate(new CharacterBodyRequest("custom-smoke", "player", loaded,
                    CharacterBuildPurpose.Studio, 1), actor.transform, Color.white, out var body), Is.True);
                var state = body.Root.GetComponent<CharacterBodyBuildState>();
                double deadline = Time.realtimeSinceStartupAsDouble + 20;
                while (!state.Ready && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
                Assert.That(state.Ready, Is.True);
                Assert.That(string.IsNullOrEmpty(state.Substitution), Is.True, "New content must render directly, without fallback.");
                var avatar = body.Root.GetComponent<DynamicCharacterAvatar>();
                foreach (var recipe in added)
                    Assert.That(avatar.GetWardrobeItem(recipe.wardrobeSlot)?.name, Is.EqualTo(recipe.name), recipe.wardrobeSlot);
            }
            finally
            {
                Object.Destroy(actor); Object.Destroy(cast);
                if (index != null)
                {
                    foreach (var recipe in added) index.RemoveAsset(typeof(UMAWardrobeRecipe), recipe.name, refresh: false);
                    while (index.SerializedItems.Count > originalItemCount && index.SerializedItems.Last() == null)
                        index.SerializedItems.RemoveAt(index.SerializedItems.Count - 1);
                }
                foreach (var recipe in added) Object.Destroy(recipe);
                Object.Destroy(extension);
            }
            yield return null;
        }
#endif

        [UnityTest]
        public IEnumerator ExplicitDnaSurvivesTheNewDnaBuildAndPaletteDoesNotRepaintClothing()
        {
            var cast = new GameObject("UMA test provider", typeof(GamesimUmaCast));
            var actor = new GameObject("Explicit recipe actor");
            try
            {
                yield return null;
                var provider = (IModularCharacterBodyProvider)CharacterBodySource.Provider;
                var appearance = provider.Catalog.Materialize(CharacterAppearance.Preset("player"));
                AppearanceEditing.SetValue(appearance, "headSize", .61f);
                AppearanceEditing.SetValue(appearance, "height", .23f);
                AppearanceEditing.SetColor(appearance, "Hair", new Color(.8f, .2f, .1f));
                Assert.That(provider.TryCreate(new CharacterBodyRequest("player", "player", appearance,
                    CharacterBuildPurpose.Studio, 4), actor.transform, Color.magenta, out var body), Is.True);
                var state = body.Root.GetComponent<CharacterBodyBuildState>();
                for (int frame = 0; frame < 900 && !state.Ready; frame++) yield return null;
                Assert.That(state.Ready, Is.True, "Ready must follow the post-build DNA pass.");
                Assert.That(state.Revision, Is.EqualTo(4));
                var avatar = body.Root.GetComponent<DynamicCharacterAvatar>();
                Assert.That(avatar.GetDNA()["headSize"].Value, Is.EqualTo(.61f).Within(.001f));
                Assert.That(avatar.GetDNA()["height"].Value, Is.EqualTo(.23f).Within(.001f));
                var hash = appearance.ContentKey();
                provider.SetWardrobeColor(body, Color.green);
                yield return null;
                Assert.That(appearance.ContentKey(), Is.EqualTo(hash));
            }
            finally { Object.Destroy(actor); Object.Destroy(cast); }
            yield return null;
        }
    }
}
