using System.Linq;
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
