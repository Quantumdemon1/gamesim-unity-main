using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Setup step two: building a houseguest who is not on any card.
    ///
    /// <para>The numbers are the source's — five to start, five spare, two traits, held between one
    /// and ten — and the two things worth watching are the ones where an obvious improvement would
    /// be wrong: the clamp is lossy on the way back, and a spent point is not the same thing as a
    /// trait's boost.</para>
    /// </summary>
    public sealed class CharacterDraftTests
    {
        // ---------------------------------------------------------------- the allowance

        [Test]
        public void EveryStatStartsAtFiveWithFiveSpare()
        {
            var draft = CharacterDraft.Blank();
            Assert.That(WebTraits.StatNames.Select(stat => WebTraits.Get(draft.Stats, stat)),
                Is.All.EqualTo(CharacterDraft.StartingStat));
            Assert.That(draft.Remaining, Is.EqualTo(CharacterDraft.SparePoints));
        }

        [Test]
        public void TheAllowanceRunsOut()
        {
            var draft = CharacterDraft.Blank();
            for (int i = 0; i < CharacterDraft.SparePoints; i++)
                Assert.That(draft.Raise("mental"), Is.True, "point " + i);

            Assert.That(draft.Remaining, Is.Zero);
            Assert.That(draft.Raise("social"), Is.False);
            Assert.That(WebTraits.Get(draft.Stats, "mental"), Is.EqualTo(10));
        }

        [Test]
        public void NothingGoesPastTen()
        {
            var draft = CharacterDraft.Blank();
            draft.AddTrait("Strategic");   // mental +2
            for (int i = 0; i < CharacterDraft.SparePoints; i++) draft.Raise("mental");

            Assert.That(WebTraits.Get(draft.Stats, "mental"), Is.EqualTo(WebTraits.Maximum));
            Assert.That(draft.CanRaise("mental"), Is.False);
        }

        /// <summary>
        /// A trait's boost is not the player's to take back. Without this, adding a trait and then
        /// lowering the stat it raised would launder a boost into a spare point.
        /// </summary>
        [Test]
        public void ATraitsBoostIsNotASparePoint()
        {
            var draft = CharacterDraft.Blank();
            draft.AddTrait("Social");   // social +2, luck +1

            Assert.That(draft.Remaining, Is.EqualTo(CharacterDraft.SparePoints),
                "A trait costs nothing from the allowance.");
            Assert.That(draft.Lower("social"), Is.False, "…and gives nothing back to it either.");
            Assert.That(WebTraits.Get(draft.Stats, "social"), Is.EqualTo(7));
        }

        [Test]
        public void APointPutInCanBeTakenBackOut()
        {
            var draft = CharacterDraft.Blank();
            draft.Raise("luck");
            Assert.That(draft.Remaining, Is.EqualTo(CharacterDraft.SparePoints - 1));

            Assert.That(draft.Lower("luck"), Is.True);
            Assert.That(draft.Remaining, Is.EqualTo(CharacterDraft.SparePoints));
            Assert.That(WebTraits.Get(draft.Stats, "luck"), Is.EqualTo(CharacterDraft.StartingStat));
        }

        // ---------------------------------------------------------------- traits

        [Test]
        public void TwoTraitsAtMost()
        {
            var draft = CharacterDraft.Blank();
            Assert.That(draft.AddTrait("Strategic"), Is.True);
            Assert.That(draft.AddTrait("Loyal"), Is.True);
            Assert.That(draft.AddTrait("Sneaky"), Is.False, "A third is refused, not swapped in.");
            Assert.That(draft.Crowded, Is.EqualTo("Strategic"));
        }

        [Test]
        public void AnUnknownTraitIsRefused()
        {
            var draft = CharacterDraft.Blank();
            Assert.That(draft.AddTrait("Photogenic"), Is.False);
            Assert.That(draft.Traits, Is.Empty);
        }

        [Test]
        public void TheSameTraitCannotBeHeldTwiceInAnyCasing()
        {
            var draft = CharacterDraft.Blank();
            Assert.That(draft.AddTrait("sneaky"), Is.True);
            Assert.That(draft.AddTrait("SNEAKY"), Is.False);
            Assert.That(draft.Traits.Single(), Is.EqualTo("Sneaky"),
                "Stored as the table spells it, so the boosts are always found.");
        }

        [Test]
        public void RemovingATraitTakesItsBoostBack()
        {
            var draft = CharacterDraft.Blank();
            draft.AddTrait("Competitive");   // physical +2, endurance +1
            Assert.That(draft.RemoveTrait("Competitive"), Is.True);

            Assert.That(WebTraits.Get(draft.Stats, "physical"), Is.EqualTo(CharacterDraft.StartingStat));
            Assert.That(WebTraits.Get(draft.Stats, "endurance"), Is.EqualTo(CharacterDraft.StartingStat));
        }

        /// <summary>
        /// The one place a houseguest can lose a point to arithmetic, kept because the web form loses
        /// it too. A stat at nine that gains +2 lands on ten and gives back two, finishing at eight.
        /// Recomputing the block from the traits would repair this and make the two builds disagree.
        /// </summary>
        [Test]
        public void TheClampIsStillLossyOnTheWayBack()
        {
            var draft = CharacterDraft.Blank();
            for (int i = 0; i < 4; i++) draft.Raise("physical");   // five to nine
            Assert.That(WebTraits.Get(draft.Stats, "physical"), Is.EqualTo(9));

            draft.AddTrait("Competitive");
            Assert.That(WebTraits.Get(draft.Stats, "physical"), Is.EqualTo(10), "Clamped on the way up.");
            draft.RemoveTrait("Competitive");
            Assert.That(WebTraits.Get(draft.Stats, "physical"), Is.EqualTo(8), "And not restored on the way down.");
        }

        // ---------------------------------------------------------------- opening a card

        [Test]
        public void OpeningACardBringsItsCopyAndItsTraits()
        {
            var template = CastTemplates.In(CastTemplates.Roster.Regular).First();
            var draft = CharacterDraft.From(template);

            Assert.That(draft.Name, Is.EqualTo(template.Name));
            Assert.That(draft.Occupation, Is.EqualTo(template.Occupation));
            Assert.That(draft.Pronouns, Is.EqualTo(template.Pronouns));
            Assert.That(draft.Archetype, Is.EqualTo(template.Archetype));
            Assert.That(draft.Age, Is.EqualTo(template.Age));
            CollectionAssert.AreEqual(template.Traits, draft.Traits);
        }

        /// <summary>
        /// Customising a card is starting again from what it was, not keeping it and adjusting.
        ///
        /// <para>The source lists what a chosen character brings along and stats are not on it; step
        /// two then puts everyone who reaches it on five and five spare. A card played as it is gets
        /// its block from <see cref="WebTraits.CreateStats"/>, which seeds each stat at six, five or
        /// four by whether a held trait names it — sharper and more lopsided than an even five. This
        /// records the gap rather than asserting it is desirable.</para>
        /// </summary>
        [Test]
        public void CustomisingACardDoesNotPreserveItsStats()
        {
            var template = CastTemplates.In(CastTemplates.Roster.Regular).First();
            var played = CastTemplates.ToContestant(template, true).stats;
            var customised = CharacterDraft.From(template).ToContestant().stats;

            Assert.That(WebTraits.StatNames.Any(stat =>
                    WebTraits.Get(played, stat) != WebTraits.Get(customised, stat)), Is.True,
                "If these ever agree, one of the two build paths has changed and the note on "
                + "CharacterDraft.From needs rewriting.");

            // Each trait is worth three points wherever it is applied — two on its primary stat and
            // one on its secondary — so the difference between the two paths is entirely in what
            // they start from: an even five each, against six, five or four by whether a held trait
            // names the stat.
            double expected = CharacterDraft.StartingStat * WebTraits.StatNames.Length
                              + 3 * template.Traits.Length;
            Assert.That(WebTraits.StatNames.Sum(stat => WebTraits.Get(customised, stat)),
                Is.EqualTo(expected), "Flat fives plus the traits, with the allowance still unspent.");

            // And the customised houseguest is the stronger of the two before a single spare point
            // is spent, because a flat five beats the four that CreateStats gives every stat no
            // trait names. Worth seeing rather than discovering: customising a card is not a
            // cosmetic choice.
            Assert.That(WebTraits.StatNames.Sum(stat => WebTraits.Get(customised, stat)),
                Is.GreaterThan(WebTraits.StatNames.Sum(stat => WebTraits.Get(played, stat))));
        }

        /// <summary>
        /// The whole card comes along, hometown and bio included.
        ///
        /// <para>This test used to assert the opposite — that both stayed blank, because "the cast
        /// table has never held either". That was true of this project's table and wrong about the
        /// source, which has carried a hometown and a bio for every houseguest all along. The table
        /// was the thing that needed fixing, not the creator.</para>
        /// </summary>
        [Test]
        public void OpeningACardBringsItsWholeCard()
        {
            foreach (var template in CastTemplates.In(CastTemplates.Roster.Regular))
            {
                var draft = CharacterDraft.From(template);
                Assert.That(draft.Hometown, Is.Not.Empty, template.Name + " has no hometown.");
                Assert.That(draft.Bio, Is.Not.Empty, template.Name + " has no bio.");
                Assert.That(draft.Hometown, Is.EqualTo(template.Hometown));
                Assert.That(draft.Bio, Is.EqualTo(template.Bio));
            }
        }

        /// <summary>Every houseguest in the pool carries the full card, so no season builds a blank one.</summary>
        [Test]
        public void EveryTemplateCarriesItsCardCopy()
        {
            foreach (var roster in (CastTemplates.Roster[])System.Enum.GetValues(typeof(CastTemplates.Roster)))
                foreach (var template in CastTemplates.In(roster))
                {
                    Assert.That(template.Hometown, Is.Not.Null.And.Not.Empty, template.Name + " hometown");
                    Assert.That(template.Bio, Is.Not.Null.And.Not.Empty, template.Name + " bio");
                    Assert.That(template.Bio.Length, Is.LessThanOrEqualTo(CharacterDraft.BioLimit));
                    Assert.That(template.Hometown.Length, Is.LessThanOrEqualTo(CharacterDraft.ShortLimit));
                    var built = CastTemplates.ToContestant(template, false);
                    Assert.That(built.hometown, Is.EqualTo(template.Hometown));
                    Assert.That(built.bio, Is.EqualTo(template.Bio));
                }
        }

        // ---------------------------------------------------------------- committing

        [Test]
        public void ANamelessHouseguestIsRefused()
        {
            var draft = CharacterDraft.Blank();
            Assert.That(draft.TryValidate(out var error), Is.False);
            Assert.That(error, Does.Contain("name"));
        }

        [TestCase(CharacterDraft.MinimumAge - 1)]
        [TestCase(CharacterDraft.MaximumAge + 1)]
        public void AnAgeOutsideTheFormsRangeIsRefused(int age)
        {
            var draft = Named();
            draft.Age = age;
            Assert.That(draft.TryValidate(out _), Is.False);
        }

        [Test]
        public void ANameLongerThanTheEngineAcceptsIsRefusedHereFirst()
        {
            var draft = Named();
            draft.Name = new string('a', CharacterDraft.NameLimit + 1);
            Assert.That(draft.TryValidate(out _), Is.False,
                "Caught while they are typing rather than at the save, where it would already have "
                + "cost them their slot decision.");
        }

        /// <summary>
        /// The bounds this form enforces are the engine's bounds. A draft that passes here and a
        /// season that fails at the save would mean the two had drifted apart.
        /// </summary>
        [Test]
        public void EveryDraftTheFormAcceptsBuildsASeasonTheEngineAccepts()
        {
            var draft = Named();
            draft.Occupation = new string('o', CharacterDraft.ShortLimit);
            draft.Hometown = new string('h', CharacterDraft.ShortLimit);
            draft.Bio = new string('b', CharacterDraft.BioLimit);
            draft.AddTrait("Strategic");
            draft.AddTrait("Loyal");
            for (int i = 0; i < CharacterDraft.SparePoints; i++) draft.Raise("luck");

            Assert.That(draft.TryValidate(out var error), Is.True, error);
            var state = SeasonBuilder.Create(new SeasonBuilder.Choice { Authored = draft, HouseSize = 8 }, 5u);
            Assert.That(EpisodeValidation.TryValidate(state, out var invalid), Is.True, invalid);
        }

        [Test]
        public void TheHouseguestYouBuiltIsTheOneYouPlay()
        {
            var draft = Named();
            draft.Name = "Robin Vale";
            draft.Hometown = "Dunedin";
            draft.Pronouns = "they/them";
            draft.AddTrait("Sneaky");

            var state = SeasonBuilder.Create(new SeasonBuilder.Choice { Authored = draft, HouseSize = 8 }, 5u);
            var you = state.Find(state.playerId);

            Assert.That(you.isPlayer, Is.True);
            Assert.That(you.name, Is.EqualTo("Robin Vale"));
            Assert.That(you.hometown, Is.EqualTo("Dunedin"));
            CollectionAssert.AreEqual(new[] { "Sneaky" }, you.traits);
        }

        /// <summary>
        /// A customised card arrives as both a draft and the id of the card it came from. The draft
        /// decides who the player is; the id is what stops the house also casting them as an NPC of
        /// the person they are playing.
        /// </summary>
        [Test]
        public void ACustomisedCardIsStillSpokenFor()
        {
            var template = CastTemplates.In(CastTemplates.Roster.Regular).First();
            var draft = CharacterDraft.From(template);
            draft.Name = "Somebody Else";

            var state = SeasonBuilder.Create(new SeasonBuilder.Choice
            {
                Authored = draft, PlayerTemplateId = template.Id, HouseSize = 8,
            }, 5u);

            Assert.That(state.Find(state.playerId).name, Is.EqualTo("Somebody Else"));
            Assert.That(state.contestants.Any(c => c.id == template.Id), Is.False,
                template.Name + " is being played, so nobody else in the house is them.");
        }

        [Test]
        public void ACopyIsItsOwnDraft()
        {
            var draft = Named();
            draft.AddTrait("Loyal");
            draft.Raise("mental");

            var copy = draft.Copy();
            copy.Name = "Different";
            copy.RemoveTrait("Loyal");
            copy.Raise("mental");

            Assert.That(draft.Name, Is.EqualTo("Ada Quinn"));
            CollectionAssert.AreEqual(new[] { "Loyal" }, draft.Traits);
            Assert.That(draft.Remaining, Is.EqualTo(CharacterDraft.SparePoints - 1));
        }

        private static CharacterDraft Named()
        {
            var draft = CharacterDraft.Blank();
            draft.Name = "Ada Quinn";
            return draft;
        }
    }
}
