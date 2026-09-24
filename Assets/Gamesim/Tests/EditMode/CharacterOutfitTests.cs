using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    public sealed class CharacterOutfitTests
    {
        [TestCase(EpisodePhase.Social, "Everyday")]
        [TestCase(EpisodePhase.HoH, "Competition")]
        [TestCase(EpisodePhase.Veto, "Competition")]
        [TestCase(EpisodePhase.FinalHoHPart3, "Competition")]
        [TestCase(EpisodePhase.Nomination, "Formal")]
        [TestCase(EpisodePhase.Eviction, "Formal")]
        [TestCase(EpisodePhase.JuryQuestioning, "Formal")]
        public void ActivitiesDressIndependentSnapshotsWithoutChangingTheSeason(EpisodePhase phase, string expected)
        {
            var state = ContentCatalog.Create(91);
            var person = state.Find(state.playerId);
            person.appearance = Look();
            person.appearance.outfits.Add(new CharacterOutfit { id = "Swimwear" });
            person.appearance.activeOutfit = "Swimwear";
            var key = person.appearance.ContentKey();
            var random = state.randomState;
            var shown = CharacterOutfits.ForPhase(person, phase);
            Assert.That(shown.appearance.activeOutfit, Is.EqualTo(expected));
            Assert.That(shown.id, Is.EqualTo(person.id));
            Assert.That(shown.stats.mental, Is.EqualTo(person.stats.mental));
            shown.appearance.outfits[0].wardrobe[0].itemId = "changed";
            Assert.That(person.appearance.ContentKey(), Is.EqualTo(key));
            Assert.That(state.randomState, Is.EqualTo(random));
            Assert.That(person.appearance.activeOutfit, Is.EqualTo("Swimwear"), "The editor selection is preserved in its saved recipe.");
        }

        [Test]
        public void MissingActivitySetUsesEverydayThenSavedSelectionAndRetainsLegacyProviderDefaults()
        {
            var appearance = Look();
            appearance.activeOutfit = "Formal";
            Assert.That(CharacterOutfits.Resolve(appearance, CharacterOutfits.Sleepwear).activeOutfit, Is.EqualTo("Everyday"));
            appearance.outfits.RemoveAt(0);
            Assert.That(CharacterOutfits.Resolve(appearance, CharacterOutfits.Sleepwear).activeOutfit, Is.EqualTo("Formal"));
            var legacy = CharacterAppearance.Preset("emma-brown");
            Assert.That(CharacterOutfits.Resolve(legacy, CharacterOutfits.Formal).ContentKey(), Is.EqualTo(legacy.ContentKey()));
            Assert.That(CharacterOutfits.Resolve(null, CharacterOutfits.Formal), Is.Null);
        }

        [Test]
        public void AnExplicitEmptyOutfitIsNotReplacedByAnUnrelatedSet()
        {
            var appearance = Look();
            appearance.outfits.Add(new CharacterOutfit { id = CharacterOutfits.Swimwear });
            var selected = CharacterOutfits.Resolve(appearance, CharacterOutfits.Swimwear);
            Assert.That(selected.activeOutfit, Is.EqualTo(CharacterOutfits.Swimwear));
            Assert.That(selected.outfits[3].wardrobe, Is.Empty);
        }

        /// <summary>
        /// With no set of their own for it, a houseguest swims in what they wear under their
        /// everyday clothes and sleeps in that and their shirt - and never in nothing.
        /// </summary>
        [Test]
        public void AnActivityWithoutASetOfItsOwnTakesTheOuterLayersOff()
        {
            var appearance = Dressed("HumanMaleDCS", "Hair", "Eyebrows", "BottomUnderlayer", "Chest", "Legs", "Feet");
            var key = appearance.ContentKey();

            var swim = CharacterOutfits.ForActivity(appearance, CharacterOutfits.Swimwear);
            Assert.That(swim.activeOutfit, Is.EqualTo(CharacterOutfits.Swimwear));
            Assert.That(Slots(swim), Is.EquivalentTo(new[] { "Hair", "Eyebrows", "BottomUnderlayer" }), "In the water: hair, brows, what is underneath.");
            var sleep = CharacterOutfits.ForActivity(appearance, CharacterOutfits.Sleepwear);
            Assert.That(Slots(sleep), Is.EquivalentTo(new[] { "Hair", "Eyebrows", "BottomUnderlayer", "Chest" }), "In bed, the shirt stays on.");
            Assert.That(swim.TryValidate(out var error), Is.True, error);
            Assert.That(appearance.ContentKey(), Is.EqualTo(key), "The saved look is not touched.");
            Assert.That(CharacterOutfits.ForActivity(appearance, CharacterOutfits.Formal).activeOutfit, Is.EqualTo("Everyday"),
                "Only the activities strip: a phase with no set wears the everyday one, as it always has.");

            // Nothing underneath, nothing taken off.
            var bare = Dressed("HumanMaleDCS", "Hair", "Chest", "Legs");
            Assert.That(CharacterOutfits.ForActivity(bare, CharacterOutfits.Swimwear).activeOutfit, Is.EqualTo("Everyday"));
            var noTop = Dressed("HumanFemaleDCS", "Hair", "BottomUnderlayer", "Chest", "Legs");
            Assert.That(CharacterOutfits.ForActivity(noTop, CharacterOutfits.Swimwear).activeOutfit, Is.EqualTo("Everyday"),
                "A body that needs a top underneath is not undressed without one.");

            // A set of their own wins.
            appearance.outfits.Add(new CharacterOutfit { id = CharacterOutfits.Swimwear,
                wardrobe = { new AppearanceWardrobe { slot = "Legs", itemId = "trunks" } } });
            Assert.That(Slots(CharacterOutfits.ForActivity(appearance, CharacterOutfits.Swimwear)), Is.EqualTo(new[] { "Legs" }));
        }

        private static CharacterAppearance Dressed(string body, params string[] slots)
        {
            var appearance = CharacterAppearance.Preset("player");
            appearance.bodyId = body;
            var outfit = new CharacterOutfit { id = "Everyday" };
            foreach (var slot in slots) outfit.wardrobe.Add(new AppearanceWardrobe { slot = slot, itemId = slot + "-item" });
            appearance.outfits.Add(outfit);
            return appearance;
        }

        private static string[] Slots(CharacterAppearance appearance)
            => System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Select(
                System.Linq.Enumerable.First(appearance.outfits, set => set.id == appearance.activeOutfit).wardrobe, item => item.slot));

        private static CharacterAppearance Look()
        {
            var appearance = CharacterAppearance.Preset("emma-brown");
            foreach (var id in new[] { "Everyday", "Competition", "Formal" })
            {
                var outfit = new CharacterOutfit { id = id };
                outfit.wardrobe.Add(new AppearanceWardrobe { slot = "Chest", itemId = id + "Shirt" });
                appearance.outfits.Add(outfit);
            }
            return appearance;
        }
    }
}
