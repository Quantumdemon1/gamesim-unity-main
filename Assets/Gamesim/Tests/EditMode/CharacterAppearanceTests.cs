using System;
using System.Globalization;
using System.IO;
using System.Linq;
using Gamesim.Persistence;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    public sealed class CharacterAppearanceTests
    {
        private static CharacterDraft Draft()
        {
            var draft = CharacterDraft.Blank(); draft.Name = "Alex";
            draft.Appearance.dna.Add(new AppearanceValue { id = "height", value = .6f });
            draft.Appearance.colors.Add(new AppearanceColor { id = "Skin", r = .7f, g = .4f, b = .3f });
            draft.Appearance.outfits.Add(new CharacterOutfit { wardrobe = { new AppearanceWardrobe { slot = "Chest", itemId = "Cotton shirt" } } });
            return draft;
        }

        [Test]
        public void AppearanceIsIndependentOfPlayerIdentityPronounsAndStats()
        {
            var template = CastTemplates.In(CastTemplates.Roster.Regular).First();
            var state = SeasonBuilder.Create(new SeasonBuilder.Choice { PlayerTemplateId = template.Id }, 4321);
            var player = state.Find(state.playerId);
            Assert.That(player.id, Is.EqualTo(ContentCatalog.PlayerId));
            Assert.That(player.sourceTemplateId, Is.EqualTo(template.Id));
            Assert.That(player.appearance.presetId, Is.EqualTo(template.Id));
            var key = player.appearance.ContentKey();
            player.pronouns = "they/them"; player.stats.physical = 1;
            Assert.That(player.appearance.ContentKey(), Is.EqualTo(key));
        }

        [Test]
        public void EverySnapshotOwnsItsAppearanceAndOutfits()
        {
            var draft = Draft();
            var copy = draft.Copy();
            var person = draft.ToContestant();
            var clone = person.Clone();
            draft.Appearance.dna[0].value = .2f;
            copy.Appearance.colors[0].r = .1f;
            person.appearance.outfits[0].wardrobe[0].itemId = "Different shirt";
            Assert.That(copy.Appearance.dna[0].value, Is.EqualTo(.6f));
            Assert.That(person.appearance.colors[0].r, Is.EqualTo(.7f));
            Assert.That(clone.appearance.outfits[0].wardrobe[0].itemId, Is.EqualTo("Cotton shirt"));
        }

        [Test]
        public void ContentIdentityIgnoresListOrderAndCultureButTracksAnOutfitColor()
        {
            var a = Draft().Appearance;
            a.dna.Add(new AppearanceValue { id = "chin", value = .31f });
            a.outfits[0].colors.Add(new AppearanceColor { id = "Cloth", r = .25f });
            string key = a.ContentKey();
            a.dna.Reverse();
            var previous = CultureInfo.CurrentCulture;
            try { CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR"); Assert.That(a.ContentKey(), Is.EqualTo(key)); }
            finally { CultureInfo.CurrentCulture = previous; }
            a.outfits[0].colors[0].r = .3f;
            Assert.That(a.ContentKey(), Is.Not.EqualTo(key));
        }

        [Test]
        public void ContentIdentityPreservesCollectionBoundaries()
        {
            var a = new CharacterAppearance { activeOutfit = "A" };
            a.outfits.Add(new CharacterOutfit { id = "A" });
            a.outfits.Add(new CharacterOutfit { id = "middle" });
            a.outfits.Add(new CharacterOutfit { id = "z" });
            var b = new CharacterAppearance { activeOutfit = "A" };
            b.outfits.Add(new CharacterOutfit { id = "A", wardrobe =
            {
                new AppearanceWardrobe { slot = "colors", itemId = "outfit" },
                new AppearanceWardrobe { slot = "middle", itemId = "colors" },
                new AppearanceWardrobe { slot = "outfit", itemId = "z" }
            } });
            Assert.That(a.TryValidate(out _), Is.True); Assert.That(b.TryValidate(out _), Is.True);
            Assert.That(a.ContentKey(), Is.Not.EqualTo(b.ContentKey()));
        }

        [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)] [TestCase(-.01f)] [TestCase(1.01f)]
        public void InvalidDnaCannotEnterASeason(float value)
        {
            var draft = Draft(); draft.Appearance.dna[0].value = value;
            Assert.That(draft.TryValidate(out _), Is.False);
            Assert.Throws<ArgumentException>(() => draft.ToContestant());
        }

        [Test]
        public void DuplicateSlotsUnknownActiveOutfitsAndAssetPathsAreRejected()
        {
            var recipe = Draft().Appearance;
            recipe.outfits[0].wardrobe.Add(new AppearanceWardrobe { slot = "Chest", itemId = "Other" });
            Assert.That(recipe.TryValidate(out _), Is.False);
            recipe.outfits[0].wardrobe.RemoveAt(1); recipe.activeOutfit = "Missing";
            Assert.That(recipe.TryValidate(out _), Is.False);
            recipe.activeOutfit = "Everyday"; recipe.bodyId = "../../asset";
            Assert.That(recipe.TryValidate(out _), Is.False);
        }

        [Test]
        public void CosmeticEditingPreservesTheExactCardStatsAndTraits()
        {
            var template = CastTemplates.In(CastTemplates.Roster.Regular).First();
            var original = CastTemplates.ToContestant(template, true);
            var draft = CharacterDraft.FromAppearance(template);
            Assert.That(draft.PreserveStats, Is.True);
            Assert.That(draft.Remaining, Is.Zero);
            Assert.That(draft.Raise("physical"), Is.False);
            Assert.That(draft.RemoveTrait(draft.Traits[0]), Is.False);
            foreach (string stat in WebTraits.StatNames)
                Assert.That(WebTraits.Get(draft.ToContestant().stats, stat), Is.EqualTo(WebTraits.Get(original.stats, stat)));
        }

        [Test]
        public void RefundingOneStatCannotSpendAnotherStatsOrTraitsPoints()
        {
            var draft = Draft(); draft.AddTrait("Social"); draft.Raise("mental");
            Assert.That(draft.Lower("social"), Is.False);
            Assert.That(draft.Lower("physical"), Is.False);
            var copy = draft.Copy();
            Assert.That(copy.Lower("mental"), Is.True);
            Assert.That(copy.Lower("mental"), Is.False);
            Assert.That(draft.Spent, Is.EqualTo(1));
            Assert.That(draft.Raise("not-a-stat"), Is.False);
        }

        [Test]
        public void ProfileAndSeasonRoundTripRetainRecipeAndDoNotShareMutableData()
        {
            string root = Path.Combine(Path.GetTempPath(), "GamesimAppearanceTests-" + Guid.NewGuid().ToString("N"));
            try
            {
                var draft = Draft();
                var profile = CharacterProfile.FromDraft(Guid.NewGuid().ToString("N"), draft);
                var library = new CharacterProfileStore(Path.Combine(root, "Houseguests"));
                Assert.That(library.Save(profile, out var error), Is.True, error);
                Assert.That(library.TryLoad(profile.id, out var loaded, out error), Is.True, error);
                var state = SeasonBuilder.Create(new SeasonBuilder.Choice { Authored = loaded.ToDraft() }, 7);
                var season = new EpisodeSaveStore(Path.Combine(root, "season.json"));
                season.Save(state);
                Assert.That(season.TryLoad(out var restored, out error), Is.True, error);
                Assert.That(restored.Find(restored.playerId).appearance.ContentKey(), Is.EqualTo(draft.Appearance.ContentKey()));
                loaded.contestant.appearance.dna[0].value = .9f;
                Assert.That(state.Find(state.playerId).appearance.dna[0].value, Is.EqualTo(.6f));
                Assert.That(library.Delete(profile.id, out error), Is.True, error);
                Assert.That(season.TryLoad(out _, out error), Is.True, error);
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
        }

        [Test]
        public void DamagedProfilePreservesPrimaryAndRecoversLastValidBackup()
        {
            string root = Path.Combine(Path.GetTempPath(), "GamesimProfileTests-" + Guid.NewGuid().ToString("N"));
            try
            {
                var library = new CharacterProfileStore(root);
                var profile = CharacterProfile.FromDraft(Guid.NewGuid().ToString("N"), Draft());
                Assert.That(library.Save(profile, out var error), Is.True, error);
                profile.name = profile.contestant.name = "Updated";
                Assert.That(library.Save(profile, out error), Is.True, error);
                string path = Path.Combine(root, profile.id + ".json");
                File.WriteAllText(path, "damaged");
                Assert.That(library.TryLoad(profile.id, out var recovered, out error), Is.True, error);
                Assert.That(recovered.name, Is.EqualTo("Alex"));
                Assert.That(error, Does.Contain("Recovered"));
                Assert.That(library.Save(profile, out error), Is.False);
                Assert.That(File.ReadAllText(path), Is.EqualTo("damaged"));
                Assert.That(library.Delete("../outside", out error), Is.False);
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
        }

        [Test]
        public void RepeatedLibraryProfilesBecomeDistinctIndependentCustomCastSlots()
        {
            var profile = CharacterProfile.FromDraft(Guid.NewGuid().ToString("N"), Draft());
            var choice = new SeasonBuilder.Choice { HouseSize = 12 };
            choice.CustomHouseguests.Add(profile); choice.CustomHouseguests.Add(profile);
            var state = SeasonBuilder.Create(choice, 18);
            Assert.That(state.contestants.Count, Is.EqualTo(12));
            Assert.That(state.contestants.Select(x => x.id).Distinct().Count(), Is.EqualTo(12));
            Assert.That(state.contestants.Count(x => x.isPlayer), Is.EqualTo(1));
            Assert.That(EpisodeValidation.TryValidate(state, out var error), Is.True, error);
            state.Find("custom-1").appearance.dna[0].value = .1f;
            Assert.That(state.Find("custom-2").appearance.dna[0].value, Is.EqualTo(.6f));
            Assert.That(profile.contestant.appearance.dna[0].value, Is.EqualTo(.6f));
            Assert.That(state.randomState, Is.EqualTo(18));
            var copy = choice.Copy(); copy.CustomHouseguests[0].contestant.name = "Another";
            Assert.That(choice.CustomHouseguests[0].contestant.name, Is.EqualTo("Alex"));
        }

        [Test]
        public void ExplicitGameplayRebuildKeepsTheLookButResetsAllocation()
        {
            var draft = CharacterDraft.FromAppearance(CastTemplates.In(CastTemplates.Roster.Regular).First());
            var rebuilt = draft.RebuildGameplay();
            Assert.That(rebuilt.PreserveStats, Is.False);
            Assert.That(rebuilt.Remaining, Is.EqualTo(5));
            Assert.That(rebuilt.Appearance.ContentKey(), Is.EqualTo(draft.Appearance.ContentKey()));
            Assert.That(rebuilt.SourceTemplateId, Is.EqualTo(draft.SourceTemplateId));
            rebuilt.Appearance.presetId = "player";
            Assert.That(draft.Appearance.presetId, Is.Not.EqualTo("player"));
        }

        [TestCase(true)] [TestCase(false)]
        public void AuthoredProfileOwnsThePlayerTemplateReservation(bool fromTemplate)
        {
            var templates = CastTemplates.In(CastTemplates.Roster.Regular).Take(2).ToArray();
            var draft = fromTemplate ? CharacterDraft.FromAppearance(templates[1]) : Draft();
            var state = SeasonBuilder.Create(new SeasonBuilder.Choice
            { PlayerTemplateId = templates[0].Id, Authored = draft, HouseSize = 12 }, 31);
            Assert.That(state.Find(templates[0].Id), Is.Not.Null, "A previously highlighted card is no longer reserved.");
            if (fromTemplate) Assert.That(state.Find(templates[1].Id), Is.Null, "The actual player template is reserved.");
            Assert.That(state.Find(state.playerId).name, Is.EqualTo(draft.Name));
        }
    }
}
