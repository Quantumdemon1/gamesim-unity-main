using Gamesim.House;
using NUnit.Framework;
using UnityEngine;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The room view's framing (ROOM-SCOPE-PLAN §3.2 g, D4, D5, D11, RS5) on the house's eight floor boxes,
    /// with no scene: the farthest zoom fixed per room and never by the yaw, the best headings, the room
    /// clamp narrowing with the zoom, the camera over its room, and the 0.6 s a walk's change of room
    /// waits. Plain C#, so it runs under the editor's Mono without Unity.
    /// </summary>
    public sealed class HouseRoomViewTests
    {
        private static readonly string[] North = { "Living", "Kitchen", "Bedroom", "Private" };
        private static readonly string[] South = { "HoH", "Nomination", "Games" };

        [Test]
        public void TheFarthestZoomIsFixedPerRoomAndTheNearestIsFourMetres()
        {
            foreach (var room in North)
                Assert.That(HouseRoomView.FarthestDistance(RoomLayers.IndexOf(room)), Is.EqualTo(11.25f), room + ": a 14 x 10 m room fits at 11.25 m (D4).");
            foreach (var room in South)
                Assert.That(HouseRoomView.FarthestDistance(RoomLayers.IndexOf(room)), Is.EqualTo(10.5f), room + ": a 9.4 x 10 m room fits at 10.5 m.");
            Assert.That(HouseRoomView.FarthestDistance(RoomLayers.IndexOf("Yard")), Is.EqualTo(18.25f), "The Yard fits at 18.25 m.");
            Assert.That(HouseRoomView.FarthestDistance(-1), Is.EqualTo(0f), "No room, no zoom.");
            Assert.That(HouseRoomView.FarthestDistance(RoomLayers.Count), Is.EqualTo(0f));
            Assert.That(HouseRoomView.NearestDistance, Is.EqualTo(4f), "Four metres, not ten (D11).");
            for (int room = 0; room < RoomLayers.Count; room++)
                Assert.That(HouseRoomView.FarthestDistance(room), Is.GreaterThan(HouseRoomView.NearestDistance), RoomLayers.RoomIds[room] + " has a zoom range.");
        }

        [Test]
        public void EachRoomLooksAlongItsShortSideAndTurnsTheLeastToIt()
        {
            var house = HouseRoomScopeWalkerTests.House();
            for (int room = 0; room < RoomLayers.Count; room++)
            {
                var box = house.Box(room);
                float best = HouseRoomView.BestHeading(room);
                // Yaw 0 looks along +z: the room's x side spans the frame, so it is the long side there.
                bool wide = box.width >= box.height;
                Assert.That(best, Is.EqualTo(wide ? 0f : 90f), RoomLayers.RoomIds[room] + " (" + box.width + " x " + box.height + ") looks along its short side.");
                // The nearest of the heading and its opposite, from every yaw around.
                for (int yaw = -180; yaw < 540; yaw += 15)
                {
                    float chosen = HouseRoomView.NearestBestHeading(room, yaw);
                    float opposite = Mathf.Repeat(best + 180f, 360f);
                    Assert.That(chosen, Is.EqualTo(best).Or.EqualTo(opposite), "one of the two headings");
                    float turned = Mathf.Abs(Mathf.DeltaAngle(yaw, chosen));
                    Assert.That(turned, Is.LessThanOrEqualTo(90f + .001f), RoomLayers.RoomIds[room] + " from yaw " + yaw + " turns " + turned + " degrees.");
                    Assert.That(turned, Is.LessThanOrEqualTo(Mathf.Abs(Mathf.DeltaAngle(yaw, chosen == best ? opposite : best)) + .001f), "and never the farther one");
                }
            }
            Assert.That(HouseRoomView.NearestBestHeading(RoomLayers.IndexOf("Living"), 45f), Is.EqualTo(0f), "The scene's yaw 45 turns the living room to 0.");
            Assert.That(HouseRoomView.NearestBestHeading(RoomLayers.IndexOf("Living"), 150f), Is.EqualTo(180f), "and 150 to 180.");
            Assert.That(HouseRoomView.NearestBestHeading(RoomLayers.IndexOf("Games"), 45f), Is.EqualTo(90f), "A south-wing room from 45 turns to 90.");
            Assert.That(HouseRoomView.NearestBestHeading(RoomLayers.IndexOf("Games"), -60f), Is.EqualTo(270f), "and from -60 to 270.");
        }

        [Test]
        public void TheRoomClampNarrowsWithTheZoomToTheCentreAtTheFarthest()
        {
            var house = HouseRoomScopeWalkerTests.House();
            for (int room = 0; room < RoomLayers.Count; room++)
            {
                var box = house.Box(room);
                float farthest = HouseRoomView.FarthestDistance(room), nearest = HouseRoomView.NearestDistance;
                string name = RoomLayers.RoomIds[room];
                // Far outside the room, every way.
                foreach (var outside in new[] { new Vector3(box.xMin - 30f, 1.2f, box.yMin - 30f), new Vector3(box.xMax + 30f, 2f, box.yMax + 30f),
                             new Vector3(box.center.x, 1.2f, box.yMax + 9f), new Vector3(box.xMin - 9f, 1.2f, box.center.y) })
                {
                    var atFarthest = HouseRoomView.Clamp(box, outside, farthest, nearest, farthest);
                    Assert.That(atFarthest.x, Is.EqualTo(box.center.x).Within(.0001f), name + ": at the farthest zoom the focus is the room's centre.");
                    Assert.That(atFarthest.z, Is.EqualTo(box.center.y).Within(.0001f));
                    Assert.That(atFarthest.y, Is.EqualTo(outside.y), "The height is the caller's.");
                    Assert.That(HouseRoomView.Clamp(box, outside, farthest + 6f, nearest, farthest), Is.EqualTo(atFarthest), "and beyond it.");
                    var atNearest = HouseRoomView.Clamp(box, outside, nearest, nearest, farthest);
                    Assert.That(atNearest.x, Is.InRange(box.xMin - .001f, box.xMax + .001f), name + ": at the nearest zoom the focus reaches the walls.");
                    Assert.That(atNearest.z, Is.InRange(box.yMin - .001f, box.yMax + .001f));
                    Assert.That(Mathf.Approximately(atNearest.x, box.xMin) || Mathf.Approximately(atNearest.x, box.xMax)
                        || Mathf.Approximately(atNearest.z, box.yMin) || Mathf.Approximately(atNearest.z, box.yMax)
                        || Mathf.Approximately(atNearest.x, box.center.x) || Mathf.Approximately(atNearest.z, box.center.y), Is.True,
                        name + ": a point outside comes to the room's edge (" + atNearest + ").");
                    // In between, the reach grows as the zoom comes in, and never leaves the room.
                    float last = -1f;
                    for (float d = farthest; d >= nearest - .001f; d -= .25f)
                    {
                        var at = HouseRoomView.Clamp(box, outside, d, nearest, farthest);
                        Assert.That(at.x, Is.InRange(box.xMin - .001f, box.xMax + .001f), name + " at " + d + " m stays in the room.");
                        Assert.That(at.z, Is.InRange(box.yMin - .001f, box.yMax + .001f));
                        float off = Vector2.Distance(new Vector2(at.x, at.z), box.center);
                        Assert.That(off, Is.GreaterThanOrEqualTo(last - .0001f), name + ": closer in, the focus may stray further, at " + d + " m.");
                        last = off;
                    }
                }
                // A focus inside the narrowed clamp is left where it is.
                var inside = new Vector3(box.center.x + .1f, 1.2f, box.center.y - .1f);
                Assert.That(HouseRoomView.Clamp(box, inside, nearest + 1f, nearest, farthest), Is.EqualTo(inside), name + ": a focus near the centre is not moved.");
            }
            Assert.That(HouseRoomView.Reach(11.25f, 4f, 11.25f), Is.EqualTo(0f));
            Assert.That(HouseRoomView.Reach(4f, 4f, 11.25f), Is.EqualTo(1f));
            Assert.That(HouseRoomView.Reach(7.625f, 4f, 11.25f), Is.EqualTo(.5f).Within(.0001f), "Halfway in, half the reach.");
            Assert.That(HouseRoomView.Reach(5f, 6f, 6f), Is.EqualTo(0f), "A room with no zoom range has none.");
        }

        [Test]
        public void TheCameraIsOverItsRoomOnlyInsideItsBoxAndItsFarthestZoom()
        {
            var house = HouseRoomScopeWalkerTests.House();
            int living = RoomLayers.IndexOf("Living");
            var box = house.Box(living);
            float farthest = HouseRoomView.FarthestDistance(living);
            Assert.That(HouseRoomView.Over(box, new Vector3(box.center.x, 1.2f, box.center.y), farthest, farthest), Is.True, "Centred at the farthest zoom.");
            Assert.That(HouseRoomView.Over(box, new Vector3(box.center.x, 1.2f, box.center.y), 24f, farthest), Is.False, "The dollhouse's 24 m is not over a room: a cut.");
            Assert.That(HouseRoomView.Over(box, new Vector3(box.center.x, 1.2f, box.center.y), farthest + .4f, farthest), Is.True, "Within the slack.");
            Assert.That(HouseRoomView.Over(box, new Vector3(box.xMax + .4f, 1.2f, box.center.y), 6f, farthest), Is.True, "At the wall, within the slack.");
            Assert.That(HouseRoomView.Over(box, new Vector3(box.xMax + 1f, 1.2f, box.center.y), 6f, farthest), Is.False, "A metre into the kitchen is not over the living room.");
            Assert.That(HouseRoomView.Over(box, new Vector3(.075f, 1.2f, 0f), 24f, farthest), Is.False, "The house centre at the default distance: a cut onto the room.");
        }

        [Test]
        public void AWalksChangeOfRoomWaitsOutTheLastByAnyClock()
        {
            Assert.That(HouseRoomView.RoomChangeSeconds, Is.EqualTo(.6f), "At most once per 0.6 s (section 3.2 a).");
            Assert.That(HouseRoomView.Waits(10.1f, 10f), Is.True, "A tenth of a second after the last change, a walk's waits.");
            Assert.That(HouseRoomView.Waits(10.59f, 10f), Is.True);
            Assert.That(HouseRoomView.Waits(10.6f, 10f), Is.False, "0.6 s on, it goes.");
            Assert.That(HouseRoomView.Waits(5f, float.NegativeInfinity), Is.False, "Before any change, none waits.");
            Assert.That(HouseRoomView.Waits(1f, 3600f), Is.False, "A clock that reads earlier than the last change (a new play session's) never holds a change.");
        }
    }
}
