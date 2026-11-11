using System.Collections.Generic;
using System.Linq;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// A place on the body holds one thing: a code-built piece (earrings, a cap, a necklace) or the
    /// UMA item on the same place (WardrobeSlots.Twins), never both. Wearing either in the creator
    /// takes the other off, through the one table of places and nothing per item.
    /// </summary>
    public sealed class AppearanceAccessoryPlaceTests
    {
        private sealed class StubCatalog : ICharacterAppearanceCatalog
        {
            public readonly List<AppearanceItem> All = new List<AppearanceItem>();
            public IReadOnlyList<AppearanceBodyOption> Bodies { get; } = new[] { new AppearanceBodyOption("body", "Body") };
            public IReadOnlyList<AppearanceControl> Controls { get; } = new AppearanceControl[0];
            public IReadOnlyList<AppearanceItem> Items => All;
            /// <summary>Library looks by presetId: the item ids each one wears, as the UMA catalog's library does.</summary>
            public readonly Dictionary<string, string[]> Looks = new Dictionary<string, string[]>();

            /// <summary>An appearance with no body yet is dressed in its look, as the UMA catalog does; any other is returned as it is.</summary>
            public CharacterAppearance Materialize(CharacterAppearance appearance)
            {
                if (appearance == null || !string.IsNullOrEmpty(appearance.bodyId)) return appearance;
                var dressed = appearance.Clone();
                dressed.bodyId = "body";
                var outfit = AppearanceEditing.Outfit(dressed);
                if (Looks.TryGetValue(dressed.presetId ?? "", out var ids) || Looks.TryGetValue(dressed.fallbackId ?? "", out ids))
                    foreach (string id in ids)
                        outfit.wardrobe.Add(new AppearanceWardrobe { slot = AppearanceEditing.Find(this, id)?.Slot ?? "Missing-" + outfit.wardrobe.Count, itemId = id });
                return dressed;
            }
            public CharacterAppearance ChangeBody(CharacterAppearance appearance, string bodyId) => appearance;
        }

        private static AppearanceItem Item(string id, string slot) =>
            new AppearanceItem { Id = id, Label = id, Slot = slot };

        private static CharacterAppearance Dressed(params AppearanceItem[] worn)
        {
            var appearance = new CharacterAppearance { bodyId = "body", activeOutfit = "Everyday" };
            var outfit = new CharacterOutfit { id = "Everyday" };
            foreach (var item in worn) outfit.wardrobe.Add(new AppearanceWardrobe { slot = item.Slot, itemId = item.Id });
            appearance.outfits.Add(outfit);
            return appearance;
        }

        private static string WornIn(CharacterAppearance appearance, string slot) =>
            appearance.outfits[0].wardrobe.FirstOrDefault(worn => worn.slot == slot)?.itemId;

        [Test]
        public void WearingAPieceTakesItsTwinOffInBothDirections()
        {
            var pairs = new[]
            {
                new[] { Item("gs-acc-hoops", "Earrings"), Item("gamesim.example.earrings.a", "Ears") },
                new[] { Item("gs-acc-cap", "Headwear"), Item("gamesim.example.cap.a", "Helmet") },
                new[] { Item("gs-acc-chain", "Neckwear"), Item("gamesim.example.necklace.a", "Shoulders") },
            };
            foreach (var pair in pairs)
            {
                var codeBuilt = pair[0];
                var uma = pair[1];
                var catalog = new StubCatalog();
                catalog.All.AddRange(pair);

                var appearance = Dressed(codeBuilt);
                AppearanceEditing.Wear(appearance, uma, catalog);
                Assert.That(WornIn(appearance, uma.Slot), Is.EqualTo(uma.Id), uma.Slot + " is worn.");
                Assert.That(WornIn(appearance, codeBuilt.Slot), Is.Null, "Wearing the " + uma.Slot + " item takes the " + codeBuilt.Slot + " piece off.");

                AppearanceEditing.Wear(appearance, codeBuilt, catalog);
                Assert.That(WornIn(appearance, codeBuilt.Slot), Is.EqualTo(codeBuilt.Id), codeBuilt.Slot + " is worn.");
                Assert.That(WornIn(appearance, uma.Slot), Is.Null, "Wearing the " + codeBuilt.Slot + " piece takes the " + uma.Slot + " item off.");
                Assert.That(appearance.outfits[0].wardrobe.Count, Is.EqualTo(1));
                Assert.That(appearance.TryValidate(out var error), Is.True, error);
            }
        }

        [Test]
        public void WearingOnePlaceLeavesTheOtherPlacesAlone()
        {
            var glasses = Item("gs-acc-glasses", "Eyewear");
            var cap = Item("gs-acc-cap", "Headwear");
            var chain = Item("gs-acc-chain", "Neckwear");
            var watch = Item("gamesim.example.watch.a", "Hands");
            var bracelet = Item("gamesim.example.bracelet.a", "Arms");
            var shirt = Item("shirt", "Chest");
            var hair = Item("hair", "Hair");
            var earrings = Item("gamesim.example.earrings.a", "Ears");
            var catalog = new StubCatalog();
            catalog.All.AddRange(new[] { glasses, cap, chain, watch, bracelet, shirt, hair, earrings });

            var appearance = Dressed(glasses, cap, chain, watch, bracelet, shirt, hair);
            AppearanceEditing.Wear(appearance, earrings, catalog);
            foreach (var kept in new[] { glasses, cap, chain, watch, bracelet, shirt, hair })
                Assert.That(WornIn(appearance, kept.Slot), Is.EqualTo(kept.Id), "Earrings leave " + kept.Slot + " alone.");
            Assert.That(WornIn(appearance, "Ears"), Is.EqualTo(earrings.Id));

            var otherWatch = Item("gamesim.example.watch.b", "Hands");
            catalog.All.Add(otherWatch);
            AppearanceEditing.Wear(appearance, otherWatch, catalog);
            Assert.That(WornIn(appearance, "Hands"), Is.EqualTo(otherWatch.Id), "A slot with no twin replaces only its own item.");
            foreach (var kept in new[] { glasses, cap, chain, bracelet, shirt, hair, earrings })
                Assert.That(WornIn(appearance, kept.Slot), Is.EqualTo(kept.Id), "A watch leaves " + kept.Slot + " alone.");
            Assert.That(appearance.outfits[0].wardrobe.Count, Is.EqualTo(8));
        }

        private static AppearanceItem Hidden(string id, string slot)
        {
            var item = Item(id, slot);
            item.PickerHidden = true;
            return item;
        }

        private static string Ids(IEnumerable<AppearanceItem> items) => string.Join(",", items.Select(item => item.Id));

        /// <summary>
        /// With no picker-hidden item a one-slot row offers what the creator offered before: every
        /// item of the slot that fits the body, in catalog order - kept or not. A two-slot place puts
        /// the code-built slot's items first, whatever the catalog's order.
        /// </summary>
        [Test]
        public void ChoicesOfAOneSlotRowAreTodaysItemsInTodaysOrder()
        {
            var catalog = new StubCatalog();
            var other = Item("tee-other-body", "Chest");
            other.CompatibleBodies = new[] { "other" };
            var fitted = Item("tee-fitted", "Chest");
            fitted.CompatibleBodies = new[] { "body", "other" };
            catalog.All.AddRange(new[]
            {
                Item("gamesim.example.earrings.a", "Ears"), Item("tee-b", "Chest"), other, Item("gs-acc-hoops", "Earrings"),
                Item("hair-a", "Hair"), fitted, Item("tee-a", "Chest"), Item("gs-acc-studs", "Earrings"), Item("gamesim.example.watch.a", "Hands"),
            });
            foreach (string slot in new[] { "Chest", "Hair", "Earrings", "Ears", "Hands", "Eyewear", "Legs" })
            {
                var today = catalog.Items.Where(item => item.Slot == slot && item.Fits("body")).ToList();
                Assert.That(AppearanceEditing.Choices(catalog, new[] { slot }, "body"), Is.EqualTo(today), slot);
                Assert.That(AppearanceEditing.Choices(catalog, new[] { slot }, "body", new[] { "tee-b", "gs-acc-hoops" }), Is.EqualTo(today),
                    slot + ": a keep list changes nothing while no item is hidden.");
            }
            Assert.That(Ids(AppearanceEditing.Choices(catalog, new[] { "Chest" }, "body")), Is.EqualTo("tee-b,tee-fitted,tee-a"));
            Assert.That(Ids(AppearanceEditing.Choices(catalog, WardrobeSlots.Place("Ears"), "body")),
                Is.EqualTo("gs-acc-hoops,gs-acc-studs,gamesim.example.earrings.a"), "The code-built pieces first, then the UMA twin's.");
            Assert.That(Ids(AppearanceEditing.Choices(catalog, WardrobeSlots.Place("Earrings"), "body")),
                Is.EqualTo(Ids(AppearanceEditing.Choices(catalog, WardrobeSlots.Place("Ears"), "body"))), "Either slot names the same place.");
        }

        /// <summary>
        /// A picker-hidden item is offered only to a draft whose own library look names it (by id or
        /// alias), or while it is worn; never to anyone else, never with no keep list (Randomize), and
        /// never on a body it does not fit.
        /// </summary>
        [Test]
        public void AHiddenItemIsOfferedOnlyToItsOwnLookOrWhileWorn()
        {
            var hoops = Item("gs-acc-hoops", "Earrings");
            var drops = Hidden("gamesim.example.earrings.drop", "Ears");
            drops.Aliases = new[] { "Example_Drop_Recipe" };
            var offered = Item("gamesim.example.earrings.stud", "Ears");
            var catalog = new StubCatalog();
            catalog.All.AddRange(new[] { drops, hoops, offered });
            catalog.Looks["example-star"] = new[] { "Example_Drop_Recipe" };
            catalog.Looks["player"] = new[] { "gs-acc-hoops" };
            var place = WardrobeSlots.Place("Earrings");

            List<string> Keep(CharacterAppearance draft)
            {
                var keep = AppearanceEditing.LookItemIds(catalog, draft);
                keep.AddRange(draft.outfits.SelectMany(outfit => outfit.wardrobe).Where(worn => place.Contains(worn.slot)).Select(worn => worn.itemId));
                return keep;
            }

            var custom = CharacterAppearance.Preset("player");
            custom.bodyId = "body";
            Assert.That(Ids(AppearanceEditing.Choices(catalog, place, "body", Keep(custom))), Is.EqualTo("gs-acc-hoops,gamesim.example.earrings.stud"),
                "A draft from a look that names no hidden item is never offered one.");

            var star = CharacterAppearance.Preset("example-star");
            star.bodyId = "body";
            star.outfits.Add(new CharacterOutfit { id = star.activeOutfit });
            Assert.That(AppearanceEditing.LookItemIds(catalog, star), Is.EqualTo(new[] { "Example_Drop_Recipe" }));
            Assert.That(Ids(AppearanceEditing.Choices(catalog, place, "body", Keep(star))),
                Is.EqualTo("gs-acc-hoops,gamesim.example.earrings.drop,gamesim.example.earrings.stud"),
                "The look that names it (by an alias) offers it, in catalog order after the code-built pieces, so the draft may step off it and back.");

            Assert.That(Ids(AppearanceEditing.Choices(catalog, place, "body")), Is.EqualTo("gs-acc-hoops,gamesim.example.earrings.stud"),
                "With no keep list - Randomize - it is never a choice.");

            var moved = CharacterAppearance.Preset("player");
            moved.bodyId = "body";
            moved.outfits.Add(new CharacterOutfit { id = moved.activeOutfit });
            moved.outfits[0].wardrobe.Add(new AppearanceWardrobe { slot = "Ears", itemId = drops.Id });
            Assert.That(AppearanceEditing.Choices(catalog, place, "body", Keep(moved)).Contains(drops), Is.True,
                "A draft that moved to another look keeps it among its choices while it is worn.");
            moved.outfits[0].wardrobe.Clear();
            Assert.That(AppearanceEditing.Choices(catalog, place, "body", Keep(moved)).Contains(drops), Is.False,
                "Once taken off, it is gone from that draft's choices.");

            drops.CompatibleBodies = new[] { "other" };
            Assert.That(AppearanceEditing.Choices(catalog, place, "body", Keep(star)).Contains(drops), Is.False,
                "Kept or not, an item is offered only on a body it fits.");
            Assert.That(AppearanceEditing.LookItemIds(null, star), Is.Empty);
        }

        /// <summary>The code-built pieces of today's catalog, a few of each place, and clothes beside them.</summary>
        private static StubCatalog CodeBuiltCatalog()
        {
            var catalog = new StubCatalog();
            catalog.All.AddRange(new[]
            {
                Item("gs-acc-glasses", "Eyewear"), Item("gs-acc-round", "Eyewear"), Item("gs-acc-shades", "Eyewear"),
                Item("gs-acc-cap", "Headwear"), Item("gs-acc-beanie", "Headwear"),
                Item("gs-acc-studs", "Earrings"), Item("gs-acc-hoops", "Earrings"), Item("gs-acc-silver", "Earrings"),
                Item("gs-acc-chain", "Neckwear"), Item("gs-acc-collar", "Neckwear"),
                Item("tee", "Chest"), Item("jeans", "Legs"), Item("hair", "Hair"),
            });
            return catalog;
        }

        private static CharacterAppearance Starting(StubCatalog catalog) =>
            Dressed(catalog.All.Single(item => item.Id == "tee"), catalog.All.Single(item => item.Id == "hair"), catalog.All.Single(item => item.Id == "gs-acc-cap"));

        private static string Outfit(CharacterAppearance appearance) =>
            string.Join(",", appearance.outfits.SelectMany(outfit => outfit.wardrobe.Select(worn => outfit.id + ":" + worn.slot + "=" + worn.itemId)));

        /// <summary>
        /// Randomize's accessory loop as the creator ran it before the Accessories rows became
        /// places (CharacterCreator.Studio.cs, RandomizeAppearance), word for word but for its names.
        /// </summary>
        private static void TodaysLoop(CharacterAppearance appearance, ICharacterAppearanceCatalog catalog, System.Random cosmeticRandom, double accessoryOdds)
        {
            foreach (string slot in new[] { "Eyewear", "Headwear", "Earrings", "Neckwear" })
            {
                var choices = catalog.Items.Where(item => item.Slot == slot && item.Fits(appearance.bodyId)).ToList();
                if (choices.Count > 0 && cosmeticRandom.NextDouble() < accessoryOdds)
                    AppearanceEditing.Wear(appearance, choices[cosmeticRandom.Next(choices.Count)], catalog);
                else AppearanceEditing.Outfit(appearance).wardrobe.RemoveAll(item => item.slot == slot);
            }
        }

        /// <summary>
        /// For a catalog with no UMA accessory, RollAccessories wears what the old loop wore and leaves
        /// the random sequence where it left it: the same houseguests roll as before, seed for seed.
        /// </summary>
        [Test]
        public void RollAccessoriesDrawsAsTodaysLoopDid()
        {
            var catalog = CodeBuiltCatalog();
            foreach (double odds in new[] { .3, .8 })
                for (int seed = 1; seed <= 20; seed++)
                {
                    var before = Starting(catalog);
                    var after = Starting(catalog);
                    var old = new System.Random(seed);
                    var now = new System.Random(seed);
                    for (int roll = 0; roll < 3; roll++)
                    {
                        TodaysLoop(before, catalog, old, odds);
                        AppearanceEditing.RollAccessories(after, catalog, now, odds);
                        Assert.That(Outfit(after), Is.EqualTo(Outfit(before)), "Seed " + seed + ", odds " + odds + ", roll " + roll);
                    }
                    Assert.That(now.Next(), Is.EqualTo(old.Next()), "Seed " + seed + ", odds " + odds + ": the next draw is the same.");
                }
        }

        /// <summary>
        /// Hidden UMA pieces in every accessory slot change nothing about a roll: no row offers them,
        /// so the same seed wears the same pieces and leaves the same next draw.
        /// </summary>
        [Test]
        public void RollingAccessoriesDrawsTheSameWithHiddenUmaItemsPresent()
        {
            var plain = CodeBuiltCatalog();
            var withHidden = CodeBuiltCatalog();
            foreach (string slot in WardrobeSlots.UmaAccessories)
                withHidden.All.Add(Hidden("gamesim.example." + slot.ToLowerInvariant() + ".hidden", slot));
            for (int seed = 1; seed <= 20; seed++)
            {
                var a = Starting(plain);
                var b = Starting(withHidden);
                var first = new System.Random(seed);
                var second = new System.Random(seed);
                for (int roll = 0; roll < 3; roll++)
                {
                    AppearanceEditing.RollAccessories(a, plain, first, .5);
                    AppearanceEditing.RollAccessories(b, withHidden, second, .5);
                    Assert.That(Outfit(b), Is.EqualTo(Outfit(a)), "Seed " + seed + ", roll " + roll);
                    Assert.That(b.outfits.SelectMany(outfit => outfit.wardrobe).Any(worn => worn.itemId.EndsWith(".hidden")), Is.False, "No hidden piece is ever rolled.");
                }
                Assert.That(second.Next(), Is.EqualTo(first.Next()), "Seed " + seed + ": the next draw is the same.");
            }

            var wearing = Starting(withHidden);
            wearing.outfits[0].wardrobe.Add(new AppearanceWardrobe { slot = "Hands", itemId = "gamesim.example.hands.hidden" });
            AppearanceEditing.RollAccessories(wearing, withHidden, new System.Random(7), .5);
            Assert.That(WornIn(wearing, "Hands"), Is.Null, "A row with no offered piece rolls bare: it takes a worn hidden piece off, as a bare roll takes off a code-built one.");
        }

        [Test]
        public void TakingOffAPlaceClearsBothOfItsSlots()
        {
            var hoops = Item("gs-acc-hoops", "Earrings");
            var drops = Item("gamesim.example.earrings.a", "Ears");
            var watch = Item("gamesim.example.watch.a", "Hands");
            var bracelet = Item("gamesim.example.bracelet.a", "Arms");
            var shirt = Item("shirt", "Chest");
            // Both sides of a place at once, as a save from before the twin rule could hold them.
            var appearance = Dressed(hoops, drops, watch, bracelet, shirt);
            var outfit = appearance.outfits[0];

            AppearanceEditing.TakeOff(outfit, WardrobeSlots.Place("Earrings"));
            Assert.That(WornIn(appearance, "Earrings"), Is.Null);
            Assert.That(WornIn(appearance, "Ears"), Is.Null, "Remove clears the place's UMA side too.");
            Assert.That(outfit.wardrobe.Select(worn => worn.slot), Is.EqualTo(new[] { "Hands", "Arms", "Chest" }));

            AppearanceEditing.TakeOff(outfit, WardrobeSlots.Place("Hands"));
            Assert.That(outfit.wardrobe.Select(worn => worn.slot), Is.EqualTo(new[] { "Arms", "Chest" }), "A place of one slot clears that slot alone.");
            AppearanceEditing.TakeOff(null, WardrobeSlots.Place("Arms"));
        }

        [Test]
        public void ClothingResetLeavesEveryAccessorySlotToItsPage()
        {
            foreach (string slot in WardrobeSlots.Accessories)
                Assert.That(AppearanceEditing.IsClothingResetSlot(slot), Is.False, slot + " is the Accessories page's to reset.");
            foreach (string slot in AppearanceEditing.CharacterSlots)
                Assert.That(AppearanceEditing.IsClothingResetSlot(slot), Is.False, slot + " has a page of its own.");
            foreach (string slot in new[] { "Chest", "Legs", "Feet", "TopUnderlayer", "BottomUnderlayer", "Missing-3" })
                Assert.That(AppearanceEditing.IsClothingResetSlot(slot), Is.True, slot + " is the Clothing page's, as before.");
        }
    }
}
