using Gamesim.Presentation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>The room chips' glyphs: every room the house has gets a mark, and an unknown one gets the house.</summary>
    public sealed class HouseMapTests
    {
        [Test]
        public void RoomIcon_MatchesTheHousesRoomsAndFallsBackToTheHouse()
        {
            Assert.That(HouseMap.RoomIcon("HOH Suite"), Is.EqualTo("crown"));
            Assert.That(HouseMap.RoomIcon("Bedroom 2"), Is.EqualTo("bed"));
            Assert.That(HouseMap.RoomIcon("Kitchen"), Is.EqualTo("fork"));
            Assert.That(HouseMap.RoomIcon("Dining area"), Is.EqualTo("fork"));
            Assert.That(HouseMap.RoomIcon("Gym"), Is.EqualTo("dumbbell"));
            Assert.That(HouseMap.RoomIcon("Diary room"), Is.EqualTo("chat"));
            Assert.That(HouseMap.RoomIcon("Nomination room"), Is.EqualTo("target"));
            Assert.That(HouseMap.RoomIcon("Living room"), Is.EqualTo("people"));
            Assert.That(HouseMap.RoomIcon("Backyard & Pool"), Is.EqualTo("star"));
            Assert.That(HouseMap.RoomIcon("Observatory"), Is.EqualTo("house"), "A room the set does not know still gets a mark.");
            Assert.That(HouseMap.RoomIcon(null), Is.EqualTo("house"));
        }
    }
}
