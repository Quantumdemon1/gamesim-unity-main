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
            public CharacterAppearance Materialize(CharacterAppearance appearance) => appearance;
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
    }
}
