using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Gamesim.Persistence;
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
    /// <summary>Requires the authored knit/vest assets; a missing garment fails acceptance rather than skipping it.</summary>
    public sealed class UmaHouseGarmentLifecyclePlayModeTests
    {
        private static readonly string[] Outfits = { "Everyday", "Competition", "Formal", "Sleepwear", "Swimwear" };
        private static readonly string[] Ids = { "gamesim.knit.crew.a", "gamesim.knit.crew.b", "gamesim.vest.competition.a", "gamesim.vest.competition.b" };
        private static readonly string[] Slots = { "Gamesim_KnitCrew_A_Slot", "Gamesim_KnitCrew_B_Slot", "Gamesim_CompetitionVest_A_Slot", "Gamesim_CompetitionVest_B_Slot" };
        private GameObject cast, actor, other;
        private CharacterStudioPreview preview;
        private UmaAppearanceCatalog catalog;
        private string root;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            cast = new GameObject("House garment UMA provider", typeof(GamesimUmaCast));
            actor = new GameObject("House garment actor");
            other = new GameObject("Independent house garment actor");
            root = Path.Combine(Path.GetTempPath(), "GamesimUmaGarmentLifecycle-" + Guid.NewGuid().ToString("N"));
            yield return null;
            catalog = new UmaAppearanceCatalog();
            Assert.That(catalog.Diagnostics, Is.Empty);
            foreach (string id in Ids) Assert.That(AppearanceEditing.Find(catalog, id), Is.Not.Null, id + " must be generated and indexed before this acceptance run.");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            CharacterPortraits.Release();
            if (preview != null) Object.Destroy(preview.gameObject);
            if (actor != null) Object.Destroy(actor);
            if (other != null) Object.Destroy(other);
            if (cast != null) Object.Destroy(cast);
            yield return null;
            string resolved = Path.GetFullPath(root);
            Assert.That(resolved.StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase), Is.True);
            Assert.That(Path.GetFileName(resolved), Does.StartWith("GamesimUmaGarmentLifecycle-"));
            if (Directory.Exists(resolved)) Directory.Delete(resolved, true);
        }

        [UnityTest]
        public IEnumerator FourGarmentsHaveStableCatalogMetadataAndDistinctProjectOwnedMeshGeometry()
        {
            var index = UMAAssetIndexer.Instance;
            var geometry = new List<Vector3[]>();
            for (int i = 0; i < Ids.Length; i++)
            {
                var item = AppearanceEditing.Find(catalog, Ids[i]);
                string body = i % 2 == 0 ? UmaCastLibrary.FemaleRace : UmaCastLibrary.MaleRace;
                Assert.That(item.Slot, Is.EqualTo("Chest"));
                Assert.That(item.CompatibleBodies, Is.EquivalentTo(new[] { body }));
                Assert.That(item.Label, Is.EqualTo(i < 2 ? "Crew-neck knit" : "Competition vest"));
                Assert.That(item.StyleGroup, Is.EqualTo(i < 2 ? "gamesim-knit-crew" : "gamesim-competition-vest"));
                Assert.That(item.Tags, Does.Contain(i < 2 ? "casual" : "competition"));
                Assert.That(item.Thumbnail, Is.Not.Null, item.Id + " needs a discoverable creator thumbnail.");
                Assert.That(item.ColorChannels, Is.Empty, "The garment's private dye must not introduce shared skin or hair channels.");
                var recipe = index.GetAsset<UMAWardrobeRecipe>(catalog.ResolveRecipeName(item.Id));
                Assert.That(recipe, Is.Not.Null);
                Assert.That(AppearanceEditing.Find(catalog, recipe.name), Is.SameAs(item), "Recipe-name aliases still resolve to the stable save ID.");
                Assert.That(recipe.PackedLoad().slotsV3.Where(part => part != null).Select(part => part.id), Does.Contain(Slots[i]));
                var slot = index.GetAsset<SlotDataAsset>(Slots[i]);
                Assert.That(slot?.meshData, Is.Not.Null, Slots[i] + " is a real skinned garment slot.");
                Assert.That(slot.meshData.vertices, Is.Not.Empty);
                Assert.That(slot.meshData.vertices.All(vertex => float.IsFinite(vertex.x) && float.IsFinite(vertex.y) && float.IsFinite(vertex.z)), Is.True);
                geometry.Add(slot.meshData.vertices);
#if UNITY_EDITOR
                Assert.That(UnityEditor.AssetDatabase.GetAssetPath(slot), Does.StartWith("Assets/Gamesim/Uma/Content/HouseGarments/"), "The garment geometry is owned by the project.");
#endif
            }
            Assert.That(geometry[0].SequenceEqual(geometry[2]), Is.False, "The knit and vest on Body A must have different authored geometry.");
            Assert.That(geometry[1].SequenceEqual(geometry[3]), Is.False, "The knit and vest on Body B must have different authored geometry.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator ChangingBodyUsesThePairedFitInEveryOutfitWithoutChangingDyesOrSource()
        {
            var original = FiveOutfits(UmaCastLibrary.FemaleRace);
            string before = original.ContentKey();
            var changed = catalog.ChangeBody(original, UmaCastLibrary.MaleRace);
            Assert.That(original.ContentKey(), Is.EqualTo(before));
            Assert.That(changed.bodyId, Is.EqualTo(UmaCastLibrary.MaleRace));
            foreach (string outfit in Outfits)
            {
                var previous = original.outfits.Single(set => set.id == outfit);
                var next = changed.outfits.Single(set => set.id == outfit);
                Assert.That(Chest(changed, outfit), Is.EqualTo(outfit == "Competition" ? Ids[3] : Ids[1]), outfit);
                Assert.That(AppearanceEditing.TryFabric(previous, "Chest", out var oldDye), Is.True);
                Assert.That(AppearanceEditing.TryFabric(next, "Chest", out var newDye), Is.True);
                AssertColour(newDye, oldDye, outfit + " dye survives fit substitution");
                Assert.That(next.colors, Is.Not.SameAs(previous.colors));
            }
            var back = catalog.ChangeBody(changed, UmaCastLibrary.FemaleRace);
            foreach (string outfit in Outfits)
                Assert.That(Chest(back, outfit), Is.EqualTo(Chest(original, outfit)), "Returning to Body A restores the paired style in " + outfit);
            foreach (var value in original.dna)
                Assert.That(back.dna.Single(entry => entry.id == value.id).value, Is.EqualTo(value.value), "Body substitution retains the person's explicit DNA: " + value.id);
            yield return null;
        }

        [UnityTest]
        public IEnumerator PreviewProfileSeasonReloadAndFiveGameplayOutfitsRenderTheExactSavedGarments()
        {
            preview = CharacterStudioPreview.Create("Authored garment acceptance studio");
            foreach (string body in new[] { UmaCastLibrary.FemaleRace, UmaCastLibrary.MaleRace })
            {
                var appearance = FiveOutfits(body);
                string key = appearance.ContentKey();
                preview.Show(appearance);
                yield return Until(() => !preview.IsBuilding, "Studio " + body);
                Assert.That(preview.CanRetry, Is.False, preview.Status);
                Assert.That(preview.CompletedKey, Is.EqualTo(key));
                AssertWorn(preview.GetComponentInChildren<DynamicCharacterAvatar>(), Chest(appearance, "Everyday"), Dye(0));

                var draft = CharacterDraft.FromAppearance(CastTemplates.Find("emma-brown"));
                draft.Appearance = appearance;
                var profile = CharacterProfile.FromDraft(Guid.NewGuid().ToString("N"), draft);
                var library = new CharacterProfileStore(Path.Combine(root, "Profiles"));
                Assert.That(library.Save(profile, out var error), Is.True, error);
                Assert.That(library.TryLoad(profile.id, out var restoredProfile, out error), Is.True, error);
                var season = SeasonBuilder.Create(new SeasonBuilder.Choice { Authored = restoredProfile.ToDraft(), CustomHouseguests = new List<CharacterProfile> { restoredProfile.Clone() } }, 71u);
                CharacterAppearanceSnapshots.Materialize(season);
                var save = new EpisodeSaveStore(Path.Combine(root, "Season-" + (body == UmaCastLibrary.FemaleRace ? "A" : "B") + ".json"));
                save.Save(season);
                Assert.That(save.TryLoad(out var restored, out error), Is.True, error);
                var person = restored.Find("custom-1");
                Assert.That(person.appearance.ContentKey(), Is.EqualTo(key));
                string stateBefore = JsonUtility.ToJson(restored);
                string profileBefore = restoredProfile.contestant.appearance.ContentKey();
                for (int i = 0; i < Outfits.Length; i++)
                {
                    var projected = CharacterOutfits.ForContext(person, Outfits[i]);
                    var presentation = i == 0 ? CharacterPresentation.Attach(actor, projected, Color.magenta)
                        : CharacterPresentation.Dress(actor, projected, Color.green);
                    yield return Until(() => !presentation.IsBodyAssembling && !presentation.IsChangingOutfit
                        && actor.GetComponentInChildren<DynamicCharacterAvatar>()?.GetComponent<CharacterBodyBuildState>()?.Ready == true, body + " " + Outfits[i]);
                    Assert.That(presentation.AppearanceKey, Is.EqualTo(projected.appearance.ContentKey()));
                    var avatar = actor.GetComponentInChildren<DynamicCharacterAvatar>();
                    Assert.That(avatar.GetComponent<CharacterBodyBuildState>().Substitution, Is.Null.Or.Empty, body + " " + Outfits[i] + " must render directly.");
                    AssertWorn(avatar, Chest(appearance, Outfits[i]), Dye(i));
                }
                Assert.That(JsonUtility.ToJson(restored), Is.EqualTo(stateBefore), "Redressing cannot consume simulation randomness or rewrite saved appearances.");
                Assert.That(restoredProfile.contestant.appearance.ContentKey(), Is.EqualTo(profileBefore));
                Assert.That(appearance.ContentKey(), Is.EqualTo(key));
            }
        }

        [UnityTest]
        public IEnumerator PrivateGarmentDyeDoesNotRepaintAnotherActorOtherSlotsOrTheAuthoredRecipe()
        {
            foreach (string body in new[] { UmaCastLibrary.FemaleRace, UmaCastLibrary.MaleRace })
            {
                foreach (string id in Ids.Where(id => AppearanceEditing.Find(catalog, id).Fits(body)))
                {
                    var first = FiveOutfits(body); first.activeOutfit = "Everyday";
                    AppearanceEditing.Wear(first, AppearanceEditing.Find(catalog, id), catalog);
                    AppearanceEditing.SetFabric(first, "Chest", Color.red);
                    var second = first.Clone();
                    AppearanceEditing.SetFabric(second, "Chest", Color.blue);
                    var provider = new UmaBodyProvider(catalog);
                    var packed = UMAAssetIndexer.Instance.GetAsset<UMAWardrobeRecipe>(catalog.ResolveRecipeName(id));
                    string assetBefore = JsonUtility.ToJson(packed);
                    Assert.That(provider.TryCreate(new CharacterBodyRequest("first", "player", first), actor.transform, Color.magenta, out var firstBody), Is.True);
                    yield return Until(() => firstBody.Root.GetComponent<CharacterBodyBuildState>().Ready, id + " red actor");
                    var firstAvatar = firstBody.Root.GetComponent<DynamicCharacterAvatar>();
                    var untouched = OtherOverlayColors(firstAvatar, id);
                    Assert.That(untouched, Is.Not.Empty, "Other visible body/clothing slots must be present to check dye isolation.");
                    Assert.That(provider.TryCreate(new CharacterBodyRequest("second", "player", second), other.transform, Color.green, out var secondBody), Is.True);
                    yield return Until(() => secondBody.Root.GetComponent<CharacterBodyBuildState>().Ready, id + " blue actor");
                    AssertWorn(firstAvatar, id, Color.red);
                    AssertWorn(secondBody.Root.GetComponent<DynamicCharacterAvatar>(), id, Color.blue);
                    provider.SetWardrobeColor(firstBody, Color.yellow);
                    yield return null;
                    AssertWorn(firstAvatar, id, Color.red);
                    AssertWorn(secondBody.Root.GetComponent<DynamicCharacterAvatar>(), id, Color.blue);
                    var stillUntouched = OtherOverlayColors(firstAvatar, id);
                    Assert.That(stillUntouched.Keys, Is.EquivalentTo(untouched.Keys));
                    foreach (var color in untouched) AssertColour(stillUntouched[color.Key], color.Value, id + " retained " + color.Key);
                    Assert.That(JsonUtility.ToJson(packed), Is.EqualTo(assetBefore), "Dye application cannot edit the shared wardrobe recipe.");
                    Object.Destroy(firstBody.Root); Object.Destroy(secondBody.Root);
                    yield return null;
                }
            }
        }

        [UnityTest]
        public IEnumerator MissingContentShowsATemporarySubstituteAndRestoresTheOriginalIdWhenAvailable()
        {
            var appearance = FiveOutfits(UmaCastLibrary.FemaleRace);
            string key = appearance.ContentKey();
            var assets = Resources.LoadAll<UmaWardrobeCatalog>("Gamesim/CharacterCatalog").Select(asset => Object.Instantiate(asset)).ToArray();
            try
            {
                // Simulate a saved, known content pack whose recipe is not installed. No index or authored asset is edited.
                var entry = assets.SelectMany(asset => asset.entries).Single(value => value.id == Ids[0]);
                entry.recipeName = "Gamesim_Uninstalled_KnitCrew_A_Recipe";
                var unavailable = new UmaAppearanceCatalog(assets);
                Assert.That(AppearanceEditing.Find(unavailable, Ids[0]), Is.Null);
                var missingProvider = new UmaBodyProvider(unavailable);
                Assert.That(missingProvider.TryCreate(new CharacterBodyRequest("missing", "player", appearance), actor.transform, Color.white, out var temporary), Is.True);
                yield return Until(() => temporary.Root.GetComponent<CharacterBodyBuildState>().Ready, "Temporary compatible garment");
                Assert.That(temporary.Root.GetComponent<CharacterBodyBuildState>().Substitution, Does.Contain("Original choices are retained"));
                Assert.That(appearance.ContentKey(), Is.EqualTo(key));
                Object.Destroy(temporary.Root);
                yield return null;
                var restoredProvider = new UmaBodyProvider(catalog);
                Assert.That(restoredProvider.TryCreate(new CharacterBodyRequest("restored", "player", appearance), actor.transform, Color.white, out var restoredBody), Is.True);
                yield return Until(() => restoredBody.Root.GetComponent<CharacterBodyBuildState>().Ready, "Restored authored knit");
                Assert.That(restoredBody.Root.GetComponent<CharacterBodyBuildState>().Substitution, Is.Null.Or.Empty);
                AssertWorn(restoredBody.Root.GetComponent<DynamicCharacterAvatar>(), Ids[0], Dye(0));
                Assert.That(appearance.ContentKey(), Is.EqualTo(key));
            }
            finally { foreach (var asset in assets) Object.Destroy(asset); }
        }

        private CharacterAppearance FiveOutfits(string body)
        {
            var appearance = catalog.ChangeBody(catalog.Materialize(CharacterAppearance.Preset("emma-brown")), body);
            var baseline = appearance.outfits.Single(set => set.id == appearance.activeOutfit).Clone();
            appearance.outfits.Clear();
            for (int i = 0; i < Outfits.Length; i++)
            {
                var set = baseline.Clone(); set.id = Outfits[i];
                appearance.outfits.Add(set); appearance.activeOutfit = set.id;
                string id = i == 1 ? (body == UmaCastLibrary.FemaleRace ? Ids[2] : Ids[3]) : (body == UmaCastLibrary.FemaleRace ? Ids[0] : Ids[1]);
                AppearanceEditing.Wear(appearance, AppearanceEditing.Find(catalog, id), catalog);
                AppearanceEditing.SetFabric(appearance, "Chest", Dye(i));
            }
            appearance.activeOutfit = "Everyday";
            Assert.That(appearance.TryValidate(out var error), Is.True, error);
            return appearance;
        }

        private void AssertWorn(DynamicCharacterAvatar avatar, string id, Color dye)
        {
            Assert.That(avatar, Is.Not.Null);
            var recipe = UMAAssetIndexer.Instance.GetAsset<UMAWardrobeRecipe>(catalog.ResolveRecipeName(id));
            Assert.That(avatar.GetWardrobeItem("Chest")?.name, Is.EqualTo(recipe.name), id + " is the exact saved garment, not a substitute.");
            var names = recipe.PackedLoad().slotsV3.Where(part => part != null && !string.IsNullOrEmpty(part.id)).Select(part => part.id).ToArray();
            var slots = avatar.umaData.umaRecipe.slotDataList.Where(slot => slot != null && names.Contains(slot.slotName)).ToArray();
            Assert.That(slots, Is.Not.Empty, id + " has drawable assembled geometry.");
            var overlays = slots.SelectMany(slot => slot.GetOverlayList()).Where(overlay => overlay?.colorData != null && !overlay.colorData.IsASharedColor).ToArray();
            Assert.That(overlays, Is.Not.Empty, id + " has a private fabric dye target.");
            foreach (var overlay in overlays) AssertColour(overlay.colorData.channelMask[0], dye, id + " " + overlay.overlayName);
        }
        private Dictionary<string, Color> OtherOverlayColors(DynamicCharacterAvatar avatar, string garment)
        {
            var recipe = UMAAssetIndexer.Instance.GetAsset<UMAWardrobeRecipe>(catalog.ResolveRecipeName(garment));
            var garmentSlots = recipe.PackedLoad().slotsV3.Where(part => part != null).Select(part => part.id).ToArray();
            var colors = new Dictionary<string, Color>();
            foreach (var slot in avatar.umaData.umaRecipe.slotDataList.Where(slot => slot != null && !garmentSlots.Contains(slot.slotName)))
            {
                var overlays = slot.GetOverlayList();
                for (int i = 0; i < overlays.Count; i++)
                {
                    var overlay = overlays[i];
                    if (overlay?.colorData?.channelMask == null || overlay.colorData.channelMask.Length == 0) continue;
                    colors[slot.slotName + "/" + i + "/" + overlay.overlayName] = overlay.colorData.channelMask[0];
                }
            }
            return colors;
        }
        private static string Chest(CharacterAppearance appearance, string outfit) => appearance.outfits.Single(set => set.id == outfit).wardrobe.Single(worn => worn.slot == "Chest").itemId;
        private static Color Dye(int index) => new Color(.15f + index * .12f, .65f - index * .08f, .2f + index * .1f);
        private static void AssertColour(Color actual, Color expected, string context)
        {
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(.02f), context + " red");
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(.02f), context + " green");
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(.02f), context + " blue");
        }
        private static IEnumerator Until(Func<bool> done, string phase)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + 30;
            while (!done() && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.That(done(), Is.True, phase + " did not become drawable within thirty seconds.");
        }
    }
}
