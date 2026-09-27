using System.Collections;
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
    /// <summary>
    /// The colours a player picks reach what is drawn. Every houseguest's hair used to come out
    /// white: UMA 3's card-hair shader reads its colour from its own base, root and tip properties,
    /// which a plain shared colour never set. And the house's clothes have no shared colour at all,
    /// so a fabric colour had nothing to reach.
    /// </summary>
    public sealed class UmaColourPlayModeTests
    {
        private static readonly Color Red = new Color(.9f, .1f, .1f), Green = new Color(.1f, .7f, .2f);

        [UnityTest]
        public IEnumerator HairAndFabricColours_ReachTheMaterialsThatDrawThem()
        {
            var cast = new GameObject("UMA colour provider", typeof(GamesimUmaCast));
            var actor = new GameObject("Colour actor");
            try
            {
                yield return null;
                var provider = (IModularCharacterBodyProvider)CharacterBodySource.Provider;
                var appearance = provider.Catalog.Materialize(CharacterAppearance.Preset("player"));
                AppearanceEditing.SetColor(appearance, "Hair", Red);
                AppearanceEditing.SetFabric(appearance, "Chest", Green);
                Assert.That(provider.TryCreate(new CharacterBodyRequest("player", "player", appearance,
                    CharacterBuildPurpose.Studio, 1), actor.transform, Color.magenta, out var body), Is.True);
                var state = body.Root.GetComponent<CharacterBodyBuildState>();
                for (int frame = 0; frame < 900 && !state.Ready; frame++) yield return null;
                Assert.That(state.Ready, Is.True);

                // The card hair's own colours are the pick, not the material's pale default.
                var hair = body.Root.GetComponentsInChildren<Renderer>().SelectMany(renderer => renderer.sharedMaterials)
                    .Where(material => material != null && material.shader.name.Contains("HairShader")).ToArray();
                Assert.That(hair, Is.Not.Empty, "The player's hair is drawn by the card-hair shader.");
                foreach (var material in hair)
                {
                    AssertColour(material.GetColor("_BaseColor"), Red, material.name + " base");
                    AssertColour(material.GetColor("_RootColor"), Red * .82f, material.name + " root");
                    // And its highlight is a shade of the hair, not the material's orange-red.
                    AssertColour(material.GetColor("_SpecularTint"), UmaBodyProvider.HairHighlight(Red), material.name + " highlight");
                }

                // The shirt's overlays carry the fabric tint; the trousers keep their own white.
                var avatar = body.Root.GetComponent<DynamicCharacterAvatar>();
                var worn = appearance.outfits.First(outfit => outfit.id == appearance.activeOutfit).wardrobe;
                var catalog = (UmaAppearanceCatalog)provider.Catalog;
                var chestParts = PartsOf(catalog, worn.First(item => item.slot == "Chest").itemId);
                var legParts = PartsOf(catalog, worn.First(item => item.slot == "Legs").itemId);
                var slots = avatar.umaData.umaRecipe.slotDataList.Where(slot => slot != null).ToArray();
                var shirt = slots.Where(slot => chestParts.Contains(slot.slotName)).SelectMany(slot => slot.GetOverlayList()).ToArray();
                Assert.That(shirt, Is.Not.Empty, "The shirt is on the body.");
                foreach (var overlay in shirt.Where(overlay => !overlay.colorData.IsASharedColor))
                    AssertColour(overlay.colorData.channelMask[0], Green, overlay.overlayName);
                foreach (var overlay in slots.Where(slot => legParts.Contains(slot.slotName)).SelectMany(slot => slot.GetOverlayList()))
                    AssertColour(overlay.colorData.channelMask[0], Color.white, overlay.overlayName + " (untinted)");
            }
            finally { Object.Destroy(actor); Object.Destroy(cast); }
            yield return null;
        }

        private static string[] PartsOf(UmaAppearanceCatalog catalog, string itemId) =>
            UMAAssetIndexer.Instance.GetAsset<UMAWardrobeRecipe>(catalog.ResolveRecipeName(itemId)).PackedLoad()
                .slotsV3.Where(part => part != null && !string.IsNullOrEmpty(part.id)).Select(part => part.id).ToArray();

        private static void AssertColour(Color actual, Color expected, string what)
        {
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(.02f), what + " red: " + actual);
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(.02f), what + " green: " + actual);
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(.02f), what + " blue: " + actual);
        }
    }
}
