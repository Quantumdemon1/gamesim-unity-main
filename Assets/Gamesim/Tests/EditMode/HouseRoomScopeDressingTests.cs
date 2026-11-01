using Gamesim.House;
using NUnit.Framework;
using UnityEngine;
using Placement = Gamesim.House.HouseRoomScope.Placement;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The dressing's rule (ROOM-SCOPE-PLAN §3.2 b, RS2a) on the house's eight floor boxes, with no
    /// scene: a piece the classifier puts in a room goes to that room's layer, wherever it was copied
    /// from; a piece that leaves its room goes back to the shell's layer 0; nothing on a layer of its
    /// own, of another kind or a body's is ever the dressing's to move; and an emitter is placed by its
    /// point in plan. Plain C#, so it runs under the editor's Mono without Unity. The PlayMode tests
    /// (EpisodePlayModeTests.RoomScopeDressing) read the same off the house itself.
    /// </summary>
    public sealed class HouseRoomScopeDressingTests
    {
        private static int Index(string room) => RoomLayers.IndexOf(room);

        private static readonly Placement[] Away = { Placement.Straddles, Placement.Outside, Placement.Shell, Placement.Collider };

        [Test]
        public void ARoomsPieceGoesToItsRoomsLayer()
        {
            for (int room = 0; room < RoomLayers.Count; room++)
                Assert.That(HouseRoomScope.DressingLayer(0, Placement.Room, room), Is.EqualTo(RoomLayers.First + room),
                    "From the shell's layer 0 to the " + RoomLayers.RoomIds[room] + "'s.");
            Assert.That(HouseRoomScope.DressingLayer(RoomLayers.Nomination, Placement.Room, Index("Living")), Is.EqualTo(RoomLayers.Living),
                "A copy of the nomination room's screen, standing in the living room, is the living room's: by where it stands.");
            Assert.That(HouseRoomScope.DressingLayer(RoomLayers.Kitchen, Placement.Room, Index("Kitchen")), Is.EqualTo(RoomLayers.Kitchen),
                "Placed again where it stands, a piece keeps its room's layer.");
        }

        [Test]
        public void APieceThatLeavesItsRoomGoesBackToTheShell()
        {
            foreach (var placement in Away)
                for (int layer = RoomLayers.First; layer < RoomLayers.First + RoomLayers.Count; layer++)
                    Assert.That(HouseRoomScope.DressingLayer(layer, placement, -1), Is.EqualTo(0),
                        "On the " + RoomLayers.RoomOf(layer) + "'s layer, now " + placement + ": back on the shell's layer 0.");
            Assert.That(HouseRoomScope.DressingLayer(RoomLayers.Games, Placement.Room, -1), Is.EqualTo(0), "A room with no index is no room.");
            Assert.That(HouseRoomScope.DressingLayer(RoomLayers.Games, Placement.Room, RoomLayers.Count), Is.EqualTo(0), "nor is one past the eight.");
        }

        [Test]
        public void TheShellAndEveryOtherLayerStayWhereTheyAre()
        {
            foreach (var placement in Away)
                Assert.That(HouseRoomScope.DressingLayer(0, placement, -1), Is.EqualTo(0), placement + " on layer 0 stays the shell's.");
            // A layer of its own is never the dressing's: Ignore Raycast, the furniture's 8, the tests' 30 and 31.
            foreach (int layer in new[] { 2, 5, HouseLayers.Furniture, 30, 31 })
                Assert.That(HouseRoomScope.DressingLayer(layer, Placement.Layer, -1), Is.EqualTo(layer), "Layer " + layer + " stays.");
            Assert.That(HouseRoomScope.DressingLayer(0, Placement.Kind, -1), Is.EqualTo(0), "A line or a sprite is not the dressing's.");
            Assert.That(HouseRoomScope.DressingLayer(RoomLayers.Yard, Placement.Kind, -1), Is.EqualTo(RoomLayers.Yard), "wherever it stands.");
            // A body's renderer objects are RS4's: hidden on its room's layer, drawn on 0, never moved by the dressing.
            Assert.That(HouseRoomScope.DressingLayer(RoomLayers.Bedroom, Placement.Body, -1), Is.EqualTo(RoomLayers.Bedroom), "A hidden body's piece stays hidden.");
            Assert.That(HouseRoomScope.DressingLayer(0, Placement.Body, Index("Kitchen")), Is.EqualTo(0), "A drawn body's piece stays drawn.");
        }

        [Test]
        public void AnEmitterIsPlacedByItsPointInPlan()
        {
            var house = HouseRoomScopeWalkerTests.House();
            Assert.That(house.RoomForPlan(-7f, -7f, -5f, -5f, out var placement), Is.EqualTo(Index("Living")), "A point in the middle of the living room.");
            Assert.That(placement, Is.EqualTo(Placement.Room));
            Assert.That(house.RoomForPlan(.1f, .1f, -5f, -5f, out placement), Is.EqualTo(Index("Kitchen")),
                "A tenth of a metre into the kitchen: both grown boxes hold it, the kitchen's own floor holds the point.");
            Assert.That(placement, Is.EqualTo(Placement.Room));
            Assert.That(house.RoomForPlan(0f, 0f, -5f, -5f, out placement), Is.EqualTo(-1), "On the wall line: shared, drawn always.");
            Assert.That(placement, Is.EqualTo(Placement.Straddles));
            Assert.That(house.RoomForPlan(-14.2f, -14.2f, -5f, -5f, out placement), Is.EqualTo(Index("Living")), "Within the growth past an outer wall: still the room's.");
            Assert.That(house.RoomForPlan(-14.5f, -14.5f, -5f, -5f, out placement), Is.EqualTo(-1), "Past the growth: over no floor.");
            Assert.That(placement, Is.EqualTo(Placement.Outside));

            // What the classifier reads of a renderer (HouseRoomScope.PlanExtent, which Classify calls):
            // an emitter's point, whatever its live particles' bounds; any other renderer's bounds,
            // wherever its pivot.
            var acrossTheWall = new Bounds(new Vector3(0f, 1f, -5f), new Vector3(4f, 1f, 1f));
            var inTheLivingRoom = new Vector3(-7f, .3f, -5f);
            HouseRoomScope.PlanExtent(true, inTheLivingRoom, acrossTheWall, out float x0, out float x1, out float z0, out float z1);
            Assert.That(new[] { x0, x1, z0, z1 }, Is.EqualTo(new[] { -7f, -7f, -5f, -5f }), "An emitter is read by its point in plan.");
            Assert.That(house.RoomForPlan(x0, x1, z0, z1, out placement), Is.EqualTo(Index("Living")),
                "An emitter in the living room whose particles drift across the kitchen's wall is the living room's.");
            Assert.That(placement, Is.EqualTo(Placement.Room));
            HouseRoomScope.PlanExtent(false, inTheLivingRoom, acrossTheWall, out x0, out x1, out z0, out z1);
            Assert.That(new[] { x0, x1, z0, z1 }, Is.EqualTo(new[] { -2f, 2f, -5.5f, -4.5f }), "A mesh is read by its bounds.");
            Assert.That(house.RoomForPlan(x0, x1, z0, z1, out placement), Is.EqualTo(-1), "A mesh with those bounds crosses the wall, wherever its pivot,");
            Assert.That(placement, Is.EqualTo(Placement.Straddles), "and is drawn always.");
        }
    }
}
