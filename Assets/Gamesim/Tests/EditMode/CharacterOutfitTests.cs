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
