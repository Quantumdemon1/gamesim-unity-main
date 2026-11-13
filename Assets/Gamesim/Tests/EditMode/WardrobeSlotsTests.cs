using System;
using System.Linq;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The wardrobe's slot lists (<see cref="WardrobeSlots"/>): which UMA slots the catalog opens,
    /// which code-built piece and UMA item share a place on the body, the Accessories page's rows,
    /// and what the five UMA accessory slots do to the outfits and the saves that hold them. Plain
    /// data, so it is pinned here without UMA or the engine.
    /// </summary>
    public sealed class WardrobeSlotsTests
    {
        [Test]
        public void TheCatalogTakesTheNineGarmentSlotsAndTheFiveAccessorySlots()
        {
            Assert.That(WardrobeSlots.Garments, Is.EqualTo(new[] { "Hair", "Beard", "Eyebrows", "Face", "Chest", "Legs", "Feet", "BottomUnderlayer", "TopUnderlayer" }));
            Assert.That(WardrobeSlots.UmaAccessories, Is.EqualTo(new[] { "Ears", "Helmet", "Shoulders", "Arms", "Hands" }));
            Assert.That(WardrobeSlots.CodeBuiltAccessories, Is.EqualTo(new[] { "Eyewear", "Headwear", "Earrings", "Neckwear" }));
            Assert.That(WardrobeSlots.Uma, Is.EqualTo(WardrobeSlots.Garments.Concat(WardrobeSlots.UmaAccessories).ToArray()));
            Assert.That(WardrobeSlots.Accessories, Is.EqualTo(WardrobeSlots.CodeBuiltAccessories.Concat(WardrobeSlots.UmaAccessories).ToArray()));
            foreach (var list in new[] { WardrobeSlots.Uma, WardrobeSlots.Accessories })
                Assert.That(list.Distinct(StringComparer.Ordinal).Count(), Is.EqualTo(list.Length), "No slot is listed twice.");
            Assert.That(WardrobeSlots.Uma, Has.No.Member("Waist"), "UMA's Waist slot stays closed.");
            Assert.That(WardrobeSlots.Uma.Intersect(WardrobeSlots.CodeBuiltAccessories, StringComparer.Ordinal), Is.Empty,
                "A code-built slot is never a UMA slot: UMA recipes never land in the code-built pieces' slots.");
            foreach (string slot in WardrobeSlots.Accessories) Assert.That(WardrobeSlots.IsAccessory(slot), Is.True, slot);
            foreach (string slot in WardrobeSlots.Garments) Assert.That(WardrobeSlots.IsAccessory(slot), Is.False, slot);
            Assert.That(WardrobeSlots.IsAccessory(null), Is.False);
            Assert.That(WardrobeSlots.IsAccessory("ears"), Is.False, "Slot names compare ordinally.");
        }

        [Test]
        public void EachTwinIsACodeBuiltSlotAndAUmaAccessorySlotBothWays()
        {
            var pairs = new[] { new[] { "Earrings", "Ears" }, new[] { "Headwear", "Helmet" }, new[] { "Neckwear", "Shoulders" } };
            foreach (var pair in pairs)
            {
                Assert.That(WardrobeSlots.Twins(pair[0]), Is.EqualTo(new[] { pair[1] }), pair[0]);
                Assert.That(WardrobeSlots.Twins(pair[1]), Is.EqualTo(new[] { pair[0] }), pair[1]);
                Assert.That(WardrobeSlots.CodeBuiltAccessories, Does.Contain(pair[0]));
                Assert.That(WardrobeSlots.UmaAccessories, Does.Contain(pair[1]));
                Assert.That(WardrobeSlots.Place(pair[0]), Is.EqualTo(pair), "The code-built slot comes first in its place.");
                Assert.That(WardrobeSlots.Place(pair[1]), Is.EqualTo(pair), "Either slot names the same place.");
            }
            foreach (string alone in new[] { "Eyewear", "Arms", "Hands", "Chest", "Hair" })
            {
                Assert.That(WardrobeSlots.Twins(alone), Is.Empty, alone + " shares its place with nothing.");
                Assert.That(WardrobeSlots.Place(alone), Is.EqualTo(new[] { alone }));
            }
            Assert.That(WardrobeSlots.Twins(null), Is.Empty, "No slot (a piece the catalog does not know) has no twin.");
            Assert.That(WardrobeSlots.Twins("Missing-0"), Is.Empty);
            Assert.That(WardrobeSlots.Twins("ears"), Is.Empty, "Slot names compare ordinally.");
            Assert.That(WardrobeSlots.Place(null), Is.Empty);
        }

        [Test]
        public void TheAccessoryPageShowsTheFourCodeBuiltRowsThenArmsAndHands()
        {
            Assert.That(WardrobeSlots.AccessoryRows, Is.EqualTo(new[] { "Eyewear", "Headwear", "Earrings", "Neckwear", "Arms", "Hands" }));
            // Every accessory slot is shown by exactly one row.
            var shown = WardrobeSlots.AccessoryRows.SelectMany(WardrobeSlots.Place).ToArray();
            Assert.That(shown, Is.EquivalentTo(WardrobeSlots.Accessories));
        }

        /// <summary>
        /// A derived swim or sleep set keeps hair, brows, beard, the face's details and what is
        /// worn underneath (and the shirt for bed): every accessory comes off, the code-built pieces
        /// and the five UMA slots alike.
        /// </summary>
        [Test]
        public void EveryAccessoryComesOffForThePoolAndForBed()
        {
            var appearance = CharacterAppearance.Preset("player");
            appearance.bodyId = "HumanFemaleDCS";
            var outfit = new CharacterOutfit { id = CharacterOutfits.Everyday };
            foreach (string slot in new[] { "Hair", "Eyebrows", "Face", "TopUnderlayer", "BottomUnderlayer", "Chest", "Legs", "Feet" }.Concat(WardrobeSlots.Accessories))
                outfit.wardrobe.Add(new AppearanceWardrobe { slot = slot, itemId = slot + "-item" });
            appearance.outfits.Add(outfit);
            Assert.That(outfit.wardrobe.Count(worn => WardrobeSlots.IsAccessory(worn.slot)), Is.EqualTo(9));
            var key = appearance.ContentKey();

            var swim = CharacterOutfits.ForActivity(appearance, CharacterOutfits.Swimwear);
            Assert.That(swim.activeOutfit, Is.EqualTo(CharacterOutfits.Swimwear));
            Assert.That(Slots(swim), Is.EquivalentTo(new[] { "Hair", "Eyebrows", "Face", "TopUnderlayer", "BottomUnderlayer" }));
            var sleep = CharacterOutfits.ForActivity(appearance, CharacterOutfits.Sleepwear);
            Assert.That(sleep.activeOutfit, Is.EqualTo(CharacterOutfits.Sleepwear));
            Assert.That(Slots(sleep), Is.EquivalentTo(new[] { "Hair", "Eyebrows", "Face", "TopUnderlayer", "BottomUnderlayer", "Chest" }));
            Assert.That(swim.TryValidate(out var error), Is.True, error);
            Assert.That(appearance.ContentKey(), Is.EqualTo(key), "The saved look is not touched.");
        }

        /// <summary>
        /// A save that holds a UMA item in one of the new slots needs no new field: a slot is a plain
        /// string, the item a catalog id. Both twins in one outfit are a valid save (the body build
        /// shows the UMA item); one slot twice is refused, as for any slot.
        /// </summary>
        [Test]
        public void AnOutfitWearingEarsAndEarringsIsAValidSave()
        {
            var appearance = Old();
            var outfit = appearance.outfits[0];
            outfit.wardrobe.Add(new AppearanceWardrobe { slot = "Ears", itemId = "gamesim.example.earrings.a" });
            outfit.wardrobe.Add(new AppearanceWardrobe { slot = "Hands", itemId = "gamesim.example.watch.a" });
            Assert.That(outfit.wardrobe.Any(worn => worn.slot == "Earrings"), Is.True, "The code-built twin is still saved beside it.");
            Assert.That(appearance.TryValidate(out var error), Is.True, error);
            var copy = appearance.Clone();
            Assert.That(copy.ContentKey(), Is.EqualTo(appearance.ContentKey()), "A copy keeps every new slot.");
            Assert.That(copy.outfits[0].wardrobe.Single(worn => worn.slot == "Ears").itemId, Is.EqualTo("gamesim.example.earrings.a"));

            outfit.wardrobe.Add(new AppearanceWardrobe { slot = "Ears", itemId = "gamesim.example.earrings.b" });
            Assert.That(appearance.TryValidate(out error), Is.False, "Two items in one slot are refused, the new slots included.");
        }

        /// <summary>
        /// The five new slots touch no slot an older save can hold: an outfit saved before them
        /// validates, keeps its content key and wears and strips exactly as it did.
        /// </summary>
        [Test]
        public void AnOldOutfitWithoutTheNewSlotsIsUnchangedByTheirArrival()
        {
            var appearance = Old();
            var before = appearance.Clone();
            Assert.That(appearance.TryValidate(out var error), Is.True, error);
            var oldSlots = new[] { "Hair", "Beard", "Eyebrows", "Face", "Chest", "Legs", "Feet", "BottomUnderlayer", "TopUnderlayer", "Eyewear", "Headwear", "Earrings", "Neckwear" };
            Assert.That(WardrobeSlots.UmaAccessories.Intersect(oldSlots, StringComparer.Ordinal), Is.Empty, "Every new slot is a name no old save holds.");
            foreach (var worn in appearance.outfits[0].wardrobe)
                Assert.That(WardrobeSlots.IsAccessory(worn.slot), Is.EqualTo(Array.IndexOf(new[] { "Eyewear", "Headwear", "Earrings", "Neckwear" }, worn.slot) >= 0),
                    worn.slot + " stays on the page that owned it.");

            var everyday = CharacterOutfits.Resolve(appearance, CharacterOutfits.Everyday);
            Assert.That(everyday.ContentKey(), Is.EqualTo(before.ContentKey()));
            var swim = CharacterOutfits.ForActivity(appearance, CharacterOutfits.Swimwear);
            Assert.That(Slots(swim), Is.EquivalentTo(new[] { "Hair", "Eyebrows", "TopUnderlayer", "BottomUnderlayer" }));
            var sleep = CharacterOutfits.ForActivity(appearance, CharacterOutfits.Sleepwear);
            Assert.That(Slots(sleep), Is.EquivalentTo(new[] { "Hair", "Eyebrows", "TopUnderlayer", "BottomUnderlayer", "Chest" }));
            Assert.That(appearance.ContentKey(), Is.EqualTo(before.ContentKey()), "Reading the outfit changes nothing saved.");
        }

        /// <summary>A look saved before the UMA accessory slots: clothes, underwear, code-built glasses and earrings.</summary>
        private static CharacterAppearance Old()
        {
            var appearance = CharacterAppearance.Preset("player");
            appearance.bodyId = "HumanFemaleDCS";
            var outfit = new CharacterOutfit { id = CharacterOutfits.Everyday };
            foreach (var pair in new[]
            {
                new[] { "Hair", "FemaleHair2" }, new[] { "Eyebrows", "FemaleBrows" }, new[] { "Chest", "gamesim.knit.crew.a" },
                new[] { "Legs", "FemaleJeans" }, new[] { "Feet", "FemaleSneakers" }, new[] { "TopUnderlayer", "FemaleBra" },
                new[] { "BottomUnderlayer", "FemaleBriefs" }, new[] { "Eyewear", "gs-acc-glasses" }, new[] { "Earrings", "gs-acc-hoops" },
            })
                outfit.wardrobe.Add(new AppearanceWardrobe { slot = pair[0], itemId = pair[1] });
            appearance.outfits.Add(outfit);
            return appearance;
        }

        private static string[] Slots(CharacterAppearance appearance) =>
            appearance.outfits.First(set => set.id == appearance.activeOutfit).wardrobe.Select(item => item.slot).ToArray();
    }
}
