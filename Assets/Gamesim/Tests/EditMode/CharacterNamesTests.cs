using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    public sealed class CharacterNamesTests
    {
        [TestCase("Dr. Will Kirby", "Will")]
        [TestCase("Janelle Pierzina", "Janelle")]
        [TestCase("Jun Song", "Jun")]
        [TestCase("Will", "Will")]
        [TestCase("Mary-Jane de Silva", "Mary-Jane")]
        [TestCase("Sha'Carri Richardson", "Sha'Carri")]
        [TestCase("J. R. Smith", "Smith")]
        [TestCase("Dr.", "Dr.")]
        [TestCase("  Dr.   Will Kirby  ", "Will")]
        [TestCase("Dr.\tWill\nKirby", "Will")]
        [TestCase(null, "")]
        [TestCase("", "")]
        [TestCase("   \t\n", "")]
        public void ShortLabelPreservesTheGivenNameAndExistingInitialConvention(string fullName, string expected)
        {
            Assert.That(CharacterNames.FirstName(fullName), Is.EqualTo(expected));
        }

        /// <summary>
        /// FinalistRead.FirstName's words go into saved log text (Negotiation.PriceWords), so it keeps the
        /// spaces-only split those seasons were saved with: a tab, a no-break space or an ideographic space
        /// does not split a name there. It agrees with the display formatter on space-separated names.
        /// </summary>
        [TestCase("Dr. Will Kirby", "Will")]
        [TestCase("Janelle Pierzina", "Janelle")]
        [TestCase("Mary-Jane de Silva", "Mary-Jane")]
        [TestCase("J. R. Smith", "Smith")]
        [TestCase("Dr.", "Dr.")]
        [TestCase("  Dr.   Will Kirby  ", "Will")]
        [TestCase("Ana Lima Souza", "Ana")]
        [TestCase("Dr.\tWill\nKirby", "Dr.\tWill\nKirby")]
        [TestCase("Ana\tLima", "Ana\tLima")]
        [TestCase("Ana Lima", "Ana Lima")]
        [TestCase("山田　太郎", "山田　太郎")]
        [TestCase(null, "")]
        [TestCase("", "")]
        public void SavedTextKeepsTheSpacesOnlySplitItWasSavedWith(string fullName, string expected)
        {
            Assert.That(FinalistRead.FirstName(fullName), Is.EqualTo(expected));
        }

        /// <summary>The saved line itself: a vote-save price names a tab-separated houseguest whole, as before.</summary>
        [Test]
        public void APriceLineNamesAHouseguestAsTheSavedSeasonsDid()
        {
            var s = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, 3301u);
            var other = s.contestants.First(c => c.id != s.playerId);
            other.name = "Ana\tLima Souza";
            var price = new Negotiation.Price { kind = DealKind.VoteSave, payerId = s.playerId, aboutId = other.id };
            Assert.That(Negotiation.PriceWords(s, price), Is.EqualTo("your vote to keep Ana\tLima this week"));
        }

        /// <summary>
        /// The names the engine writes are the season's (alumni rules 6, <see cref="AlumniRules.TitledNames"/>; the owner's
        /// "the 'Dr.' names", 2026-11-03). From 6 the betrayal's offer, its cut-ties line, the house's name for a pact and a
        /// story's pact call Dr. Will Kirby Will. Below 6 - every season saved before it, all of them below it for ever -
        /// they say "Dr.", as those seasons were played, so their replays write the words they wrote. Every place the engine
        /// names a pact is held to it (the quick fixes' review): the player's pact from an invitation agreed
        /// (EpisodeEngine.AllyThroughInvitation, EpisodeEngine.Strategy.cs), the player's pact a proposal or a story forms
        /// (EpisodeEngine.FormPact, EpisodeEngine.StoryEffects.cs), the weekly pass's pact of two houseguests
        /// (NpcAlliances.Form, through <see cref="NpcAlliances.TryPropose"/>), a story's pact of the houseguests'
        /// (<see cref="NpcAlliances.FormFromStory"/>) and the house's name for a pact (<see cref="PactNames.HouseName"/>).
        /// </summary>
        [TestCase(AlumniRules.TitledNames, "Will")]
        [TestCase(AlumniRules.GrowingPacts, "Dr.")]
        [TestCase(AlumniRules.Histories, "Dr.")]
        public void TheNamesTheEngineWritesCallATitledAlumnusByTheSeasonsRule(int value, string first)
        {
            Assert.That(AlumniRules.TitledNames, Is.EqualTo(6), "The next free value after S6's 5.");
            Assert.That(AlumniRules.Latest, Is.EqualTo(AlumniRules.TitledNames), "Fresh All-Star seasons start at 6.");
            var s = AlumniHistoriesTests.AllStars(8, 1u, value: value);
            var will = s.contestants.Single(c => c.sourceTemplateId == "dr-will-kirby");
            Assert.That(will.name, Is.EqualTo("Dr. Will Kirby"), "The alumni catalogue's one titled name.");
            Assert.That(will.isPlayer, Is.False);
            var other = s.contestants.First(c => !c.isPlayer && c.id != will.id && c.status == ContestantStatus.Active);
            string theirs = FinalistRead.FirstName(other.name);
            var start = s.Clone();

            Assert.That(CharacterNames.SeasonFirstName(s, will.name), Is.EqualTo(first));
            Assert.That(CharacterNames.SeasonFirstName(s, other.name), Is.EqualTo(theirs), "An untitled name reads the same at every value.");
            Assert.That(Allegiance.Offer(s, will.name), Is.EqualTo("You can cut ties with " + first + " this week at no cost, or stay allied."));
            Assert.That(Allegiance.CutTiesLine(s, will.name, "The Pact", false), Is.EqualTo("You cut ties with Dr. Will Kirby after they turned on The Pact. "
                + first + " is out of it, the rest of you keep it, and nobody holds it against you."));
            Assert.That(PactNames.HouseName(s, new[] { will.id }), Is.EqualTo("The " + first + " Pact"));
            Assert.That(PactNames.HouseName(s, new[] { other.id, will.id }), Is.EqualTo("The " + theirs + " and " + first + " Pact"));
            var pact = NpcAlliances.FormFromStory(s, new List<string> { will.id, other.id });
            Assert.That(pact, Is.Not.Null);
            Assert.That(pact.name, Is.EqualTo("The " + first + " and " + theirs + " Pact"), "A story's pact is named by the same rule.");

            var invited = Unallied(start, will.id);
            Assert.That(StrategyRules.Apply(invited), Is.True, "The strategy rules play from week one.");
            Engine("AllyThroughInvitation", invited, will.id);
            Assert.That(PactOf(invited, invited.playerId, will.id).name, Is.EqualTo("The " + first + " Pact"), "An invitation agreed names the player's pact by it,");

            var proposed = Unallied(start, will.id);
            Engine("FormPact", proposed, proposed.Find(will.id), (Func<double>)(() => 0.5));
            Assert.That(PactOf(proposed, proposed.playerId, will.id).name, Is.EqualTo("The " + first + " Pact"), "a proposal's or a story's pact of the player's,");

            var paired = Unallied(start, will.id);
            foreach (var a in paired.contestants)
            {
                a.traits = new List<string>();
                foreach (var b in paired.contestants.Where(c => c.id != a.id)) AllWeekCadenceTests.Score(paired, a.id, b.id, 0);
            }
            AllWeekCadenceTests.Score(paired, will.id, other.id, 90);
            AllWeekCadenceTests.Score(paired, other.id, will.id, 90);
            Assert.That(NpcAlliances.TryPropose(paired, will.id, new HashSet<string>(StringComparer.Ordinal)), Is.True, "The two warm to each other pair off.");
            Assert.That(PactOf(paired, will.id, other.id).name, Is.EqualTo("The " + first + " and " + theirs + " Pact"), "and the weekly pass's pact of two.");
        }

        /// <summary>A copy of the season with no pact of <paramref name="id"/>'s, so a new one is theirs to form.</summary>
        private static EpisodeState Unallied(EpisodeState s, string id)
        {
            var copy = s.Clone();
            copy.alliances.RemoveAll(a => a.members.Contains(id));
            return copy;
        }

        /// <summary>The one active pact of exactly these two.</summary>
        private static AllianceState PactOf(EpisodeState s, string a, string b) =>
            s.alliances.Single(x => x.active && x.members.Count == 2 && x.members.Contains(a) && x.members.Contains(b));

        /// <summary>One of the engine's private steps that names a pact, as a command reaches it.</summary>
        private static void Engine(string step, params object[] args) =>
            typeof(EpisodeEngine).GetMethod(step, BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, args);

        /// <summary>A season off the alumni rules - a Regular house, or any season saved before schema 30 - writes the name cut at its first space, as it always did.</summary>
        [Test]
        public void ASeasonOffTheAlumniRulesWritesTheNameAsItAlwaysDid()
        {
            var s = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, 3301u);
            ShippedRules.ApplyFresh(s);
            Assert.That(s.alumniRulesVersion, Is.EqualTo(AlumniRules.Off));
            Assert.That(CharacterNames.SeasonFirstName(s, "Dr. Will Kirby"), Is.EqualTo("Dr."));
            Assert.That(CharacterNames.SeasonFirstName(s, "Janelle Pierzina"), Is.EqualTo("Janelle"));
            Assert.That(CharacterNames.SeasonFirstName(null, "Dr. Will Kirby"), Is.EqualTo("Dr."), "No season, no rule: the old cut.");
            Assert.That(CharacterNames.SeasonFirstName(s, null), Is.EqualTo(""));
            Assert.That(CharacterNames.SeasonFirstName(s, ""), Is.EqualTo(""));
        }

        [TestCase("Dr. Will Kirby", "W")]
        [TestCase("Janelle Pierzina", "J")]
        [TestCase(null, "?")]
        [TestCase("   ", "?")]
        public void PortraitFallbackUsesGivenNameInitial(string fullName, string expected)
        {
            Assert.That(CharacterNames.Initial(fullName), Is.EqualTo(expected));
        }
    }
}
