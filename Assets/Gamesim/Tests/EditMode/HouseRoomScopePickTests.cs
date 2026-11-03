using Gamesim.House;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The pick filter's rules (ROOM-SCOPE-PLAN §3.2 f, D13, RS2b) with no scene: a renderer is left out
    /// of what a camera draws only on a room layer its mask leaves out, whatever else the mask leaves
    /// out; under a still pointer the hover picks again when the rooms the camera draws change, and looks
    /// again every frame while a room is left out; and only a collider on the furniture layer is drawn as
    /// the prop it hangs under. Plain C#, so it runs under the
    /// editor's Mono without Unity. HouseScopePickTests reads the same off real colliders, and the
    /// PlayMode tests (EpisodePlayModeTests.RoomScopePicks) off the house's own furniture.
    /// </summary>
    public sealed class HouseRoomScopePickTests
    {
        private const int Everything = -1;

        private static int ViewOf(int room)
        {
            int kept = RoomLayers.NothingKept;
            return RoomLayers.ScopeMask(Everything, RoomSet.Of(room), ref kept);
        }

        [Test]
        public void ARendererIsLeftOutOnlyOnARoomLayerTheMaskLeavesOut()
        {
            for (int room = 0; room < RoomLayers.Count; room++)
            {
                int mask = ViewOf(room);
                for (int layer = 0; layer < 32; layer++)
                    Assert.That(RoomLayers.Draws(mask, layer), Is.EqualTo(!RoomLayers.IsRoomLayer(layer) || layer == RoomLayers.First + room),
                        "Viewing the " + RoomLayers.RoomIds[room] + ", a renderer on layer " + layer + ".");
            }
            for (int layer = 0; layer < 32; layer++)
                Assert.That(RoomLayers.Draws(Everything, layer), Is.True, "The whole house draws layer " + layer + ".");

            // A layer the mask leaves out that is no room's is not the scope's to hide: a capture's guard
            // bit, the tests' 30, the furniture layer, the default layer.
            int others = Everything & ~(1 << 31) & ~(1 << 30) & ~(1 << HouseLayers.Furniture) & ~1;
            foreach (int layer in new[] { 0, HouseLayers.Furniture, 30, 31 })
                Assert.That(RoomLayers.Draws(others, layer), Is.True, "Layer " + layer + ", out of the mask but no room's: drawn as far as a pick goes.");
            Assert.That(RoomLayers.Draws(others & ~(1 << RoomLayers.Games), RoomLayers.Games), Is.False, "The games room's bit out: its furniture is not drawn.");
        }

        [Test]
        public void TheHoverPicksAgainOnlyWhenTheRoomsTheCameraDrawsChange()
        {
            int kitchen = ViewOf(RoomLayers.IndexOf("Kitchen"));
            Assert.That(RoomLayers.SameRooms(Everything, Everything), Is.True, "Nothing changed: no new pick.");
            Assert.That(RoomLayers.SameRooms(Everything, kitchen), Is.False, "The scope took seven rooms out: their furniture is not under a still pointer any more.");
            Assert.That(RoomLayers.SameRooms(kitchen, Everything), Is.False, "and put back, it is again.");
            Assert.That(RoomLayers.SameRooms(kitchen, ViewOf(RoomLayers.IndexOf("Yard"))), Is.False, "Another room: the kitchen's bit out, the yard's in.");
            foreach (int bit in new[] { 0, HouseLayers.Furniture, 19, 28, 30, 31 })
                Assert.That(RoomLayers.SameRooms(kitchen, kitchen ^ (1 << bit)), Is.True, "Bit " + bit + " is no room's: the same rooms, no new pick.");
            for (int layer = RoomLayers.First; layer < RoomLayers.First + RoomLayers.Count; layer++)
                Assert.That(RoomLayers.SameRooms(kitchen, kitchen ^ (1 << layer)), Is.False, "The " + RoomLayers.RoomOf(layer) + "'s bit alone changed: a new pick.");
        }

        [Test]
        public void UnderARoomViewTheHoverLooksAgainUnderAStillPointer()
        {
            Assert.That(RoomLayers.DrawsEveryRoom(Everything), Is.True, "The whole house: the scope hides nothing, and a still pointer is over what it was.");
            Assert.That(RoomLayers.DrawsEveryRoom(RoomLayers.Mask), Is.True, "Bits 20 to 27 alone are every room.");
            foreach (int bit in new[] { 0, HouseLayers.Furniture, 19, 28, 30, 31 })
                Assert.That(RoomLayers.DrawsEveryRoom(Everything & ~(1 << bit)), Is.True, "Bit " + bit + " out is no room's: every room is still drawn (a capture's guard bit, the tests' 30).");
            for (int room = 0; room < RoomLayers.Count; room++)
                Assert.That(RoomLayers.DrawsEveryRoom(ViewOf(room)), Is.False,
                    "Viewing the " + RoomLayers.RoomIds[room] + ", seven rooms are out: what the camera draws can change under a still pointer, so the hover looks again.");
            for (int layer = RoomLayers.First; layer < RoomLayers.First + RoomLayers.Count; layer++)
                Assert.That(RoomLayers.DrawsEveryRoom(Everything & ~(1 << layer)), Is.False, "The " + RoomLayers.RoomOf(layer) + " alone left out is a room view.");
        }

        [Test]
        public void OnlyAColliderOnTheFurnitureLayerIsDrawnAsItsProp()
        {
            for (int layer = 0; layer < 32; layer++)
                Assert.That(HouseScopePick.IsFurnitureProxy(layer), Is.EqualTo(layer == HouseLayers.Furniture),
                    "A collider on layer " + layer + (layer == HouseLayers.Furniture ? " is a furniture proxy: drawn as its prop is."
                        : " keeps its own answer: floors, walls and fences on 0, the room layers' collider-free pieces, the tests' layers."));
        }
    }
}
