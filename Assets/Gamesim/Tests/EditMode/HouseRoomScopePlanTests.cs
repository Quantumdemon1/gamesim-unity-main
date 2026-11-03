using Gamesim.House;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The classifier's plan rule (ROOM-SCOPE-PLAN §3.2 b) on the house's eight floor boxes, with no
    /// scene: a piece is a room's when its plan lies inside that floor grown by a quarter of a metre;
    /// where two grown floors hold it, the one whose own floor holds its centre; centred on a wall
    /// line, reaching past the growth into a neighbour, or over no floor, it is drawn always. Plain C#,
    /// so it runs under the editor's Mono without Unity.
    /// </summary>
    public sealed class HouseRoomScopePlanTests
    {
        [Test]
        public void APieceIsItsRoomsWithinAQuarterMetreOfItsFloor()
        {
            var house = HouseRoomScopeWalkerTests.House();
            int living = RoomLayers.IndexOf("Living"), kitchen = RoomLayers.IndexOf("Kitchen");
            // The living room's east wall is x = 0, the kitchen beyond it; a piece from x = -3, z -6 to -4.
            AssertPlan(house, -3f, .24f, HouseRoomScope.Placement.Room, living, "Reaching 0.24 m through the wall into the kitchen, it is still the living room's.");
            AssertPlan(house, -3f, .26f, HouseRoomScope.Placement.Straddles, -1, "At 0.26 m it reaches past the growth into the kitchen: drawn always.");
            AssertPlan(house, -.2f, .2f, HouseRoomScope.Placement.Straddles, -1, "Centred on the wall line, both floors' and neither's: drawn always.");
            AssertPlan(house, -.2f, .1f, HouseRoomScope.Placement.Room, living, "Both grown floors hold it; its centre is on the living room's.");
            AssertPlan(house, -.1f, .2f, HouseRoomScope.Placement.Room, kitchen, "Both grown floors hold it; its centre is on the kitchen's.");
            AssertPlan(house, 20f, 21f, HouseRoomScope.Placement.Outside, -1, "Over no floor at all.");
            Assert.That(HouseRoomScope.BoxGrowth, Is.EqualTo(.25f), "A quarter of a metre (§3.2 b).");
        }

        private static void AssertPlan(HouseRoomScope house, float x0, float x1, HouseRoomScope.Placement expected, int room, string why)
        {
            int got = house.RoomForPlan(x0, x1, -6f, -4f, out var placement);
            Assert.That(placement, Is.EqualTo(expected), why + " (x " + x0 + " to " + x1 + ")");
            Assert.That(got, Is.EqualTo(room), why + " (x " + x0 + " to " + x1 + ")");
        }
    }
}
