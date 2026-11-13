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

namespace Gamesim.Uma.Tests
{
    /// <summary>
    /// U7: a hair part on a material of its own, as an All-Star's hair will be delivered (contract rows H3-H5, H7): a
    /// runtime copy of a stock hair whose overlay is on a NEW cutout UMAMaterial (UseExistingTextures, one pass) carrying
    /// its own root-tip map (_ID_Map). Worn by a look built through the game's provider, the material UMA generates for it
    /// carries that map, and its base and root colours follow the look's Hair colour, and follow it again when the look
    /// is rebuilt with another colour. Stock data copied at run time only; every runtime entry leaves the UMA index again.
    /// </summary>
    public sealed class UmaHairIdMapPlayModeTests
    {
        private const string StockRecipe = "Hair_LeftPart_Recipe", StockOverlay = "CardHairMultiPass";
        private static readonly Color Red = new Color(.9f, .1f, .1f), Green = new Color(.1f, .7f, .2f);

        private GameObject cast;
        private readonly List<GameObject> actors = new List<GameObject>();
        private readonly List<UnityEngine.Object> made = new List<UnityEngine.Object>();
        private readonly List<(Type type, string name)> indexed = new List<(Type, string)>();
        private int indexCount;

        [SetUp]
        public void SetUp()
        {
            // UMA warns about a sway bone some hair lacks; that is UMA's.
            LogAssert.ignoreFailingMessages = true;
            cast = new GameObject("UMA hair map provider", typeof(GamesimUmaCast));
            indexCount = UMAAssetIndexer.Instance.SerializedItems.Count;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var actor in actors) if (actor != null) UnityEngine.Object.Destroy(actor);
            actors.Clear();
            var index = UMAAssetIndexer.Instance;
            foreach (var (type, name) in indexed) index.RemoveAsset(type, name, refresh: false);
            while (index.SerializedItems.Count > indexCount && index.SerializedItems.Last() == null) index.SerializedItems.RemoveAt(index.SerializedItems.Count - 1);
            indexed.Clear();
            foreach (var item in made) if (item != null) UnityEngine.Object.Destroy(item);
            made.Clear();
            UnityEngine.Object.Destroy(cast);
            LogAssert.ignoreFailingMessages = false;
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator AHairOnItsOwnCutoutMaterial_KeepsItsRootTipMap_AndFollowsTheLooksHairColour()
        {
            yield return null;
            var index = UMAAssetIndexer.Instance;
            string tag = Guid.NewGuid().ToString("N").Substring(0, 10);

            // Its own UMAMaterial: UseExistingTextures, the stock hair's channels, one pass, cutout, two-sided, its own _ID_Map.
            var stockOverlay = index.GetAsset<OverlayDataAsset>(StockOverlay);
            Assert.That(stockOverlay, Is.Not.Null, StockOverlay + " is in the Global Library");
            Assert.That(stockOverlay.material, Is.Not.Null, StockOverlay + " has a UMAMaterial");
            var umaMaterial = UnityEngine.Object.Instantiate(stockOverlay.material);
            umaMaterial.name = "GamesimHairProbe_UMAMaterial_" + tag;
            umaMaterial.materialType = UMAMaterial.MaterialType.UseExistingTextures;
            var secondPass = typeof(UMAMaterial).GetField("_secondPass", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(secondPass, Is.Not.Null, "UMAMaterial keeps its second pass in _secondPass");
            secondPass.SetValue(umaMaterial, null);
            var material = new Material(stockOverlay.material.material) { name = "GamesimHairProbe_Hair_" + tag };
            Assert.That(material.HasProperty("_ID_Map"), Is.True, "the stock hair shader takes an _ID_Map");
            foreach (var (property, value) in new[] { ("_Surface", 0f), ("_AlphaClip", 1f), ("_AlphaClip2", .5f), ("_ZWrite", 1f), ("_Cull", 0f) })
                if (material.HasProperty(property)) material.SetFloat(property, value);
            var idMap = new Texture2D(64, 64, TextureFormat.RGBA32, false) { name = "GamesimHairProbe_ID_Map_" + tag };
            idMap.SetPixels(Enumerable.Range(0, 64 * 64).Select(i => new Color(i % 64 / 63f, i / 64 / 63f, 0f, 1f)).ToArray());
            idMap.Apply();
            material.SetTexture("_ID_Map", idMap);
            umaMaterial.material = material;
            Assert.That(umaMaterial.secondPass, Is.Null, "one pass");
            made.Add(umaMaterial); made.Add(material); made.Add(idMap);

            // Its own overlay on that material, and its own recipe naming the overlay.
            var overlay = UnityEngine.Object.Instantiate(stockOverlay);
            overlay.name = "GamesimHairProbe_Overlay_" + tag;
            overlay._oldOverlayName = overlay.name;
            overlay.material = umaMaterial;
            made.Add(overlay);
            Assert.That(index.AddAssetItem(new AssetItem(typeof(OverlayDataAsset), overlay.name, "", overlay), noDirty: true), Is.True);
            indexed.Add((typeof(OverlayDataAsset), overlay.name));
            var stockRecipe = index.GetAsset<UMAWardrobeRecipe>(StockRecipe);
            Assert.That(stockRecipe, Is.Not.Null, StockRecipe + " is in the Global Library");
            Assert.That(stockRecipe.recipeString, Does.Contain("\"" + StockOverlay + "\""));
            var recipe = UnityEngine.Object.Instantiate(stockRecipe);
            recipe.name = "GamesimHairProbe_Recipe_" + tag;
            recipe.recipeString = stockRecipe.recipeString.Replace("\"" + StockOverlay + "\"", "\"" + overlay.name + "\"");
            recipe.hideFlags = HideFlags.DontSave;
            made.Add(recipe);
            Assert.That(recipe.wardrobeSlot, Is.EqualTo("Hair"));
            Assert.That(index.AddAssetItem(new AssetItem(typeof(UMAWardrobeRecipe), recipe.name, "", recipe), noDirty: true), Is.True);
            indexed.Add((typeof(UMAWardrobeRecipe), recipe.name));

            // The player's look wearing it, built by the game's provider in red, then rebuilt in green.
            var provider = (IModularCharacterBodyProvider)CharacterBodySource.Provider;
            Assert.That(provider, Is.Not.Null, "the UMA cast registered its provider");
            var appearance = provider.Catalog.Materialize(CharacterAppearance.Preset(ContentCatalog.PlayerId));
            var hair = appearance.outfits.First(outfit => outfit.id == appearance.activeOutfit).wardrobe.FirstOrDefault(item => item.slot == "Hair");
            Assert.That(hair, Is.Not.Null, "the player's look wears hair");
            hair.itemId = recipe.name;
            foreach (var colour in new[] { Red, Green })
            {
                AppearanceEditing.SetColor(appearance, "Hair", colour);
                var actor = new GameObject("Hair map actor " + actors.Count);
                actor.transform.position = new Vector3(actors.Count * 3f, 0f, 0f);
                actors.Add(actor);
                Assert.That(provider.TryCreate(new CharacterBodyRequest(ContentCatalog.PlayerId, ContentCatalog.PlayerId, appearance, CharacterBuildPurpose.Studio, actors.Count),
                    actor.transform, Color.white, out var body), Is.True, "the player has a body");
                var state = body.Root.GetComponent<CharacterBodyBuildState>();
                double deadline = Time.realtimeSinceStartupAsDouble + 30;
                while (!state.Ready && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
                Assert.That(state.Ready, Is.True, "the body finished building (CharacterBodyBuildState.Ready) within 30 s of real time");
                Assert.That(string.IsNullOrEmpty(state.Substitution), Is.True, "the probe hair was worn, not substituted: " + state.Substitution);

                var data = body.Root.GetComponent<DynamicCharacterAvatar>().umaData;
                var generated = data.generatedMaterials.materials.Where(m => m?.materialFragments != null
                    && m.materialFragments.Any(f => f?.overlayList != null && f.overlayList.Any(o => o != null && o.overlayName == overlay.name))).ToArray();
                Debug.Log("[hair map] " + colour + ": generated materials " + string.Join(", ", data.generatedMaterials.materials.Where(m => m?.material != null)
                    .Select(m => m.material.name + "(" + (m.umaMaterial != null ? m.umaMaterial.name : "?") + ")")));
                Assert.That(generated.Length, Is.EqualTo(1), "one generated material draws the probe hair");
                var drawn = generated[0].material;
                Assert.That(drawn, Is.Not.Null);
                var map = drawn.GetTexture("_ID_Map");
                Debug.Log("[hair map] " + colour + ": " + drawn.name + " _ID_Map " + (map != null ? map.name : "none") + ", _BaseColor " + drawn.GetColor("_BaseColor")
                    + ", _RootColor " + drawn.GetColor("_RootColor") + ", _Cull " + (drawn.HasProperty("_Cull") ? drawn.GetFloat("_Cull").ToString() : "-"));
                Assert.That(map, Is.SameAs(idMap), "the hair's own root-tip map reaches the generated material");
                Assert.That(body.Root.GetComponentsInChildren<Renderer>().SelectMany(renderer => renderer.sharedMaterials).Contains(drawn), Is.True, "the body draws that material");
                AssertColour(drawn.GetColor("_BaseColor"), colour, "base");
                AssertColour(drawn.GetColor("_RootColor"), colour * .82f, "root");
                UnityEngine.Object.Destroy(actor);
                yield return null;
            }
        }

        private static void AssertColour(Color actual, Color expected, string what)
        {
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(.02f), what + " red: " + actual);
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(.02f), what + " green: " + actual);
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(.02f), what + " blue: " + actual);
        }
    }
}
