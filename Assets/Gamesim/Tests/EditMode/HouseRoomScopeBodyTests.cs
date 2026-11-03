using Gamesim.House;
using NUnit.Framework;
using UnityEngine;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The bodies' rule and the cameras' masks (ROOM-SCOPE-PLAN §3.2 c and e, RS4) on the house's eight
    /// floor boxes, with no scene: a body is drawn in a viewed room and within 0.6 m of one, by the
    /// true distance off a corner; the whole house, no room and a body the floors cannot place draw
    /// everyone; a hidden body's renderer objects go to its walker room's layer and come back to 0, and
    /// a piece on any other layer is never the scope's; the main camera loses only the room bits outside
    /// the view and gets back only what the scope took; the live feed draws the shell and its own room.
    /// Plain C#, so it runs under the editor's Mono without Unity. The PlayMode tests
    /// (EpisodePlayModeTests.RoomScopeBodies) read the same off the house's own bodies and cameras.
    /// </summary>
    public sealed class HouseRoomScopeBodyTests
    {
        private static int Index(string room) => RoomLayers.IndexOf(room);
        private static Vector3 At(float x, float z) => new Vector3(x, 0f, z);

        [Test]
        public void ABodyInAViewedRoomIsDrawnAndOneElsewhereIsNot()
        {
            var house = HouseRoomScopeWalkerTests.House();
            for (int viewed = 0; viewed < RoomLayers.Count; viewed++)
                for (int room = 0; room < RoomLayers.Count; room++)
                {
                    var box = house.Box(room);
                    var middle = At(box.center.x, box.center.y);
                    Assert.That(house.DrawsWalker(RoomSet.Of(viewed), room, middle), Is.EqualTo(room == viewed),
                        "Viewing the " + RoomLayers.RoomIds[viewed] + ", a body in the middle of the " + RoomLayers.RoomIds[room]);
                }
            var two = RoomSet.Of("Kitchen", "Yard");
            Assert.That(house.DrawsWalker(two, Index("Yard"), At(0f, 15f)), Is.True, "Two rooms viewed, a timed move's union: each is drawn.");
            Assert.That(house.DrawsWalker(two, Index("Kitchen"), At(7f, -5f)), Is.True);
            Assert.That(house.DrawsWalker(two, Index("Games"), At(9.5f, -15f)), Is.False);
        }

        [Test]
        public void ABodyIsDrawnInItsDoorwayBandAndNotAStepPastIt()
        {
            var house = HouseRoomScopeWalkerTests.House();
            var living = RoomSet.Of("Living");
            int bedroom = Index("Bedroom"), kitchen = Index("Kitchen");
            // The Living-Bedroom wall is z = 0: a body in the bedroom, walking south towards the living room.
            Assert.That(house.DrawsWalker(living, bedroom, At(-5f, .59f)), Is.True, "0.59 m past the living room's floor: in its doorway band.");
            Assert.That(house.DrawsWalker(living, bedroom, At(-5f, .6f)), Is.True, "0.6 m, the band's edge, is in it.");
            Assert.That(house.DrawsWalker(living, bedroom, At(-5f, .61f)), Is.False, "0.61 m: past it, hidden.");
            Assert.That(house.DrawsWalker(living, bedroom, At(-5f, 3f)), Is.False, "Three metres into the bedroom: hidden.");
            // The kitchen's side, x = 0.
            Assert.That(house.DrawsWalker(living, kitchen, At(.5f, -5f)), Is.True, "Half a metre into the kitchen: in the living room's band.");
            Assert.That(house.DrawsWalker(living, kitchen, At(.7f, -5f)), Is.False);
            // Off the living room's north-east corner (0, 0), into the private room: the true distance, not each axis's.
            Assert.That(house.DrawsWalker(living, Index("Private"), At(.4f, .4f)), Is.True, "0.57 m off the corner: in the band.");
            Assert.That(house.DrawsWalker(living, Index("Private"), At(.45f, .45f)), Is.False, "0.64 m off the corner, though each axis is under 0.6: past it.");
        }

        [Test]
        public void TheWholeHouseNoRoomAndAnUnplacedBodyDrawEveryone()
        {
            var house = HouseRoomScopeWalkerTests.House();
            for (int room = 0; room < RoomLayers.Count; room++)
            {
                var box = house.Box(room);
                var middle = At(box.center.x, box.center.y);
                Assert.That(house.DrawsWalker(RoomSet.WholeHouse, room, middle), Is.True, "The whole house draws the " + RoomLayers.RoomIds[room] + ".");
                Assert.That(house.DrawsWalker(RoomSet.None, room, middle), Is.True, "No room at all is no scope: drawn.");
            }
            Assert.That(house.DrawsWalker(RoomSet.Of("Kitchen"), -1, At(-50f, 50f)), Is.True,
                "A body the floors cannot place is drawn: a missed case draws too much, never too little.");
        }

        [Test]
        public void AHiddenBodysPiecesGoToItsWalkerRoomsLayerAndComeBackToZero()
        {
            int games = Index("Games");
            Assert.That(HouseRoomScope.BodyLayer(0, false, games), Is.EqualTo(RoomLayers.Games), "Hidden: the games room's layer.");
            Assert.That(HouseRoomScope.BodyLayer(RoomLayers.Games, false, Index("HoH")), Is.EqualTo(RoomLayers.HoH),
                "Walking on, hidden, into the HoH suite: its layer.");
            Assert.That(HouseRoomScope.BodyLayer(RoomLayers.Games, true, games), Is.EqualTo(0), "Drawn again: back on 0.");
            Assert.That(HouseRoomScope.BodyLayer(0, true, games), Is.EqualTo(0), "Drawn, it stays on 0.");
            Assert.That(HouseRoomScope.BodyLayer(0, false, -1), Is.EqualTo(0), "A body with no room is never hidden on a layer.");
            foreach (int other in new[] { 8, 2, 5, 30, 31 })
            {
                Assert.That(HouseRoomScope.BodyLayer(other, false, games), Is.EqualTo(other), "Layer " + other + " is not the scope's: it stays, hidden or not.");
                Assert.That(HouseRoomScope.BodyLayer(other, true, games), Is.EqualTo(other));
            }
        }

        [Test]
        public void TheMainCameraLosesOnlyTheRoomsOutsideTheView()
        {
            const int Everything = -1;
            int kept = RoomLayers.NothingKept;
            int mask = RoomLayers.ScopeMask(Everything, RoomSet.Of("Kitchen"), ref kept);
            Assert.That(mask & RoomLayers.Mask, Is.EqualTo(1 << RoomLayers.Kitchen), "Viewing the kitchen: its room bit alone of the eight.");
            Assert.That(mask & ~RoomLayers.Mask, Is.EqualTo(Everything & ~RoomLayers.Mask), "Every bit outside 20 to 27 as it was: 0 to 19, 28 to 31.");
            Assert.That(kept, Is.EqualTo(RoomLayers.Mask), "It keeps the room bits the camera had when it began: all eight.");
            Assert.That(RoomLayers.TakenOut(kept, RoomSet.Of("Kitchen")), Is.EqualTo(RoomLayers.Mask & ~(1 << RoomLayers.Kitchen)), "What it has out is the other seven.");
            Assert.That(mask, Is.EqualTo((Everything & ~RoomLayers.Mask) | RoomSet.Of("Kitchen").CullingBits),
                "On a camera that draws every layer, the plan's (mask & ~RoomBits) | ScopeBits.");

            // A capture's guard bit, set and cleared by its owner, is never the scope's.
            int guarded = RoomLayers.ScopeMask(mask & ~(1 << 31), RoomSet.Of("Kitchen", "Yard"), ref kept);
            Assert.That(guarded & (1 << 31), Is.Zero, "The guard bit stays as its owner left it.");
            Assert.That(guarded & RoomLayers.Mask, Is.EqualTo(RoomSet.Of("Kitchen", "Yard").CullingBits), "The yard's bit is the view's too.");
            Assert.That(kept, Is.EqualTo(RoomLayers.Mask), "What it keeps is what the camera had when it began.");

            // A stale mask put back over the scope's - a capture's save and restore across a move - is the view's again at the next call.
            int stale = RoomLayers.ScopeMask((guarded & ~RoomLayers.Mask) | (1 << RoomLayers.Living), RoomSet.Of("Kitchen", "Yard"), ref kept);
            Assert.That(stale & RoomLayers.Mask, Is.EqualTo(RoomSet.Of("Kitchen", "Yard").CullingBits), "The living room's stale bit goes, the kitchen's and the yard's come back.");
            Assert.That(stale & ~RoomLayers.Mask, Is.EqualTo(guarded & ~RoomLayers.Mask), "and nothing outside 20 to 27 moves.");

            // Back to the whole house: what it kept goes back, nothing else moves, and it keeps nothing.
            int whole = RoomLayers.ScopeMask(stale, RoomSet.WholeHouse, ref kept);
            Assert.That(kept, Is.EqualTo(RoomLayers.NothingKept), "Nothing kept for the whole house.");
            Assert.That(RoomLayers.TakenOut(kept, RoomSet.Of("Kitchen")), Is.Zero, "and nothing out.");
            Assert.That(whole & RoomLayers.Mask, Is.EqualTo(RoomLayers.Mask), "Every room bit back on.");
            Assert.That(whole & ~RoomLayers.Mask, Is.EqualTo(guarded & ~RoomLayers.Mask), "and nothing else moved, the guard bit included.");
            Assert.That(RoomLayers.ScopeMask(whole & ~(1 << 30), RoomSet.WholeHouse, ref kept), Is.EqualTo(whole & ~(1 << 30)),
                "The whole house again, with nothing kept: the mask as it is.");
        }

        [Test]
        public void ARoomBitSomethingElseClearedStaysClear()
        {
            const int Everything = -1;
            int probe = 1 << RoomLayers.Living;
            int kept = RoomLayers.NothingKept;
            // Something else - a test, the probe's P2 - left the living room's bit out by hand before the scope began.
            int mask = RoomLayers.ScopeMask(Everything & ~probe, RoomSet.WholeHouse, ref kept);
            Assert.That(mask, Is.EqualTo(Everything & ~probe), "The whole house with nothing kept writes nothing.");
            Assert.That(kept, Is.EqualTo(RoomLayers.NothingKept));
            mask = RoomLayers.ScopeMask(mask, RoomSet.Of("Kitchen"), ref kept);
            Assert.That(kept, Is.EqualTo(RoomLayers.Mask & ~probe), "The scope keeps the room bits the camera had: not the living room's.");
            mask = RoomLayers.ScopeMask(mask, RoomSet.Of("Living"), ref kept);
            Assert.That(mask & probe, Is.Zero, "Viewing the living room does not put its bit back: the camera did not have it.");
            Assert.That(mask & RoomLayers.Mask, Is.Zero, "and every other room is out for the living room's view.");
            Assert.That(RoomLayers.TakenOut(kept, RoomSet.Of("Living")), Is.EqualTo(RoomLayers.Mask & ~probe), "Out: the seven it kept.");
            // A viewed room's bit cleared under the scope is the view's again at the next call.
            mask = RoomLayers.ScopeMask(mask, RoomSet.Of("Kitchen"), ref kept);
            mask = RoomLayers.ScopeMask(mask & ~(1 << RoomLayers.Kitchen), RoomSet.Of("Kitchen"), ref kept);
            Assert.That(mask & RoomLayers.Mask, Is.EqualTo(1 << RoomLayers.Kitchen), "The kitchen's bit, cleared under the scope, is the view's again.");
            mask = RoomLayers.ScopeMask(mask, RoomSet.WholeHouse, ref kept);
            Assert.That(mask & probe, Is.Zero, "Nor does the whole house after it put the living room's bit back.");
            Assert.That(mask & RoomLayers.Mask, Is.EqualTo(RoomLayers.Mask & ~probe), "Everything else is back.");
            Assert.That(RoomLayers.ScopeMask(mask, RoomSet.None, ref kept), Is.EqualTo(mask), "No room at all is no scope: nothing moves.");
        }

        [Test]
        public void TheLiveFeedDrawsTheShellAndItsOwnRoom()
        {
            const int Everything = -1;
            int feed = RoomLayers.OwnRoomMask(Everything, RoomSet.Of("Bedroom"));
            Assert.That(feed & RoomLayers.Mask, Is.EqualTo(1 << RoomLayers.Bedroom), "Its own room's layer alone of the eight.");
            Assert.That(feed & ~RoomLayers.Mask, Is.EqualTo(Everything & ~RoomLayers.Mask), "And the shell: every other layer as it was.");
            Assert.That(RoomLayers.OwnRoomMask(feed, RoomSet.Of("Private", "Bedroom")) & RoomLayers.Mask,
                Is.EqualTo(RoomSet.Of("Private", "Bedroom").CullingBits), "A conversation across a doorway: both rooms.");
            Assert.That(RoomLayers.OwnRoomMask(feed, RoomSet.None) & RoomLayers.Mask, Is.EqualTo(RoomLayers.Mask), "A feed that names no room draws them all.");
            Assert.That(RoomLayers.OwnRoomMask(feed, RoomSet.WholeHouse) & RoomLayers.Mask, Is.EqualTo(RoomLayers.Mask));
        }
    }
}
