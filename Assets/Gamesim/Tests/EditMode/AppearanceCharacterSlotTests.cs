using System.Collections.Generic;
using System.Linq;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Hair, brows and a beard belong to the person; clothes belong to the outfit.
    ///
    /// <para>An outfit is a wardrobe list, so every worn thing is stored inside one. That made
    /// hairstyle a property of the clothes: a houseguest changed hair by changing into competition
    /// kit, and the creator's Hair panel silently edited whichever set happened to be active without
    /// saying which. The reset path already assumed otherwise - it deliberately preserves these
    /// three slots when it resets clothing - so the write is what was wrong, not the reset.</para>
    /// </summary>
    public sealed class AppearanceCharacterSlotTests
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

        private static CharacterAppearance ThreeOutfits()
        {
            var appearance = new CharacterAppearance { bodyId = "body", activeOutfit = "Everyday" };
            foreach (var id in new[] { "Everyday", "Competition", "Formal" })
                appearance.outfits.Add(new CharacterOutfit { id = id });
            return appearance;
        }

        private static string WornIn(CharacterAppearance appearance, string outfit, string slot) =>
            appearance.outfits.First(set => set.id == outfit).wardrobe
                .FirstOrDefault(worn => worn.slot == slot)?.itemId;

        [Test]
        public void HairBrowsAndBeardAreWrittenToEveryOutfit()
        {
            var catalog = new StubCatalog();
            var bob = Item("hair-bob", "Hair");
            var brows = Item("brows-soft", "Eyebrows");
            var beard = Item("beard-short", "Beard");
            catalog.All.AddRange(new[] { bob, brows, beard });

            var appearance = ThreeOutfits();
            AppearanceEditing.Wear(appearance, bob, catalog);
            AppearanceEditing.Wear(appearance, brows, catalog);
            AppearanceEditing.Wear(appearance, beard, catalog);

            foreach (var outfit in new[] { "Everyday", "Competition", "Formal" })
            {
                Assert.That(WornIn(appearance, outfit, "Hair"), Is.EqualTo("hair-bob"),
                    "A houseguest must not change hairstyle by changing clothes: " + outfit);
                Assert.That(WornIn(appearance, outfit, "Eyebrows"), Is.EqualTo("brows-soft"), outfit);
                Assert.That(WornIn(appearance, outfit, "Beard"), Is.EqualTo("beard-short"), outfit);
            }
        }

        [Test]
        public void ClothesStayInTheOutfitBeingWorn()
        {
            var catalog = new StubCatalog();
            var shirt = Item("shirt-tee", "Chest");
            catalog.All.Add(shirt);

            var appearance = ThreeOutfits();
            appearance.activeOutfit = "Competition";
            AppearanceEditing.Wear(appearance, shirt, catalog);

            Assert.That(WornIn(appearance, "Competition", "Chest"), Is.EqualTo("shirt-tee"));
            Assert.That(WornIn(appearance, "Everyday", "Chest"), Is.Null,
                "Clothing is the outfit's, and putting on competition kit must not redress the rest.");
            Assert.That(WornIn(appearance, "Formal", "Chest"), Is.Null);
        }

        [Test]
        public void ChangingHairWhileAnotherOutfitIsActiveStillChangesItEverywhere()
        {
            var catalog = new StubCatalog();
            var bob = Item("hair-bob", "Hair");
            var crop = Item("hair-crop", "Hair");
            catalog.All.AddRange(new[] { bob, crop });

            var appearance = ThreeOutfits();
            AppearanceEditing.Wear(appearance, bob, catalog);
            appearance.activeOutfit = "Formal";
            AppearanceEditing.Wear(appearance, crop, catalog);

            foreach (var outfit in new[] { "Everyday", "Competition", "Formal" })
                Assert.That(WornIn(appearance, outfit, "Hair"), Is.EqualTo("hair-crop"),
                    "The second hairstyle replaces the first everywhere, not only where it was chosen: " + outfit);
            foreach (var set in appearance.outfits)
                Assert.That(set.wardrobe.Count(worn => worn.slot == "Hair"), Is.EqualTo(1),
                    "and it replaces rather than stacking.");
        }

        [Test]
        public void AnAppearanceWithNoOutfitsStillGetsItsHair()
        {
            var catalog = new StubCatalog();
            var bob = Item("hair-bob", "Hair");
            catalog.All.Add(bob);

            var appearance = new CharacterAppearance { bodyId = "body", activeOutfit = "Everyday" };
            AppearanceEditing.Wear(appearance, bob, catalog);

            Assert.That(appearance.outfits, Has.Count.EqualTo(1),
                "An empty wardrobe gets the active set made for it rather than swallowing the choice.");
            Assert.That(WornIn(appearance, "Everyday", "Hair"), Is.EqualTo("hair-bob"));
        }

        /// <summary>
        /// The trap the reset path had to stop falling into: resolving the active outfit APPENDS one
        /// when it is missing, so a snapshot read that way is a snapshot edited.
        /// </summary>
        [Test]
        public void ResolvingAnAbsentActiveOutfitAppendsOneToWhateverIsAsked()
        {
            var snapshot = new CharacterAppearance { bodyId = "body", activeOutfit = "Swimwear" };
            snapshot.outfits.Add(new CharacterOutfit { id = "Everyday" });

            AppearanceEditing.Outfit(snapshot);

            Assert.That(snapshot.outfits.Select(set => set.id), Does.Contain("Swimwear"),
                "Reading a baseline through this helper changes it, which is why the reset reads the "
                + "list directly instead.");
        }
    }
}
