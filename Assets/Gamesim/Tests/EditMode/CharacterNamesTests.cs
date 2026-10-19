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
            Assert.That(FinalistRead.FirstName(fullName), Is.EqualTo(expected), "Existing callers use the same display contract.");
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
