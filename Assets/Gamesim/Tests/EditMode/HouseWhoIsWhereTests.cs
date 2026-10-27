using System.Collections.Generic;
using Gamesim.House;
using NUnit.Framework;
using UnityEngine;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Who is where by floor (ROOM-SCOPE-PLAN §3.2 k, RSW) on the house's eight floor boxes, with no
    /// scene: every point of every floor is its own room to a walker who walked there, where the
    /// nearest room marker named another room for 39.5% of the living room; Talk's doorway band
    /// reaches 0.6 m past a floor and needs both people in it; and a spot three tenths of a metre
    /// inside a floor is that room to a walker arriving from anywhere. Plain C#, so it runs under the
    /// editor's Mono without Unity.
    /// </summary>
    public sealed class HouseWhoIsWhereTests
    {
        // The saved house's room markers in plan (EpisodeHouse.unity), in the room layers' order: what
        // who-is-where read before the floors.
        private static readonly Vector2[] Markers =
        {
            new Vector2(-5f, -7.6f), new Vector2(3f, -6f), new Vector2(-5f, 2f), new Vector2(7f, 3f),
            new Vector2(0f, 14f), new Vector2(-9.308f, -15f), new Vector2(.075f, -15f), new Vector2(9.458f, -15f),
        };

        private static int Index(string room) => RoomLayers.IndexOf(room);

        [Test]
        public void EveryFloorPointIsItsOwnRoomToAWalkerWhoWalkedThere()
        {
            var house = HouseRoomScopeWalkerTests.House();
            var failures = new List<string>();
            var sampled = new int[RoomLayers.Count];
            var misnamed = new int[RoomLayers.Count];
            for (int room = 0; room < RoomLayers.Count; room++)
            {
                var box = house.Box(room);
                // In through the middle, from every other room and from none, then over the whole floor
                // in a half-metre grid a quarter-metre in from its edges, column by column, as a walker
                // crosses it: the floor's own room at every point, its edges included.
                for (int from = -1; from < RoomLayers.Count; from++)
                    if (house.WalkerRoom(V(box.center), from) != room)
                        failures.Add(RoomLayers.RoomIds[room] + "'s middle, walked into from " + (from < 0 ? "nowhere" : RoomLayers.RoomIds[from]));
                int walker = house.WalkerRoom(V(box.center), -1);
                foreach (var feet in Grid(box))
                {
                    walker = house.WalkerRoom(V(feet), walker);
                    sampled[room]++;
                    if (walker != room && failures.Count < 20)
                        failures.Add(feet.ToString("F2") + " on the " + RoomLayers.RoomIds[room] + "'s floor is " + HouseRoomScope.RoomId(walker));
                    if (NearestMarker(feet) != room) misnamed[room]++;
                }
            }
            Assert.That(failures, Is.Empty, string.Join("; ", failures));
            // The grid is the one the plan measured the markers on: the living room's 39.5% is in it.
            int living = Index("Living"), kitchen = Index("Kitchen");
            Assert.That(sampled[living], Is.EqualTo(560), "Every half metre of the living room's 14 by 10 m.");
            Assert.That(misnamed[living] / (float)sampled[living], Is.GreaterThan(.39f),
                "The nearest marker named another room for " + misnamed[living] + " of the living room's " + sampled[living] + " points.");
            Assert.That(misnamed[kitchen] / (float)sampled[kitchen], Is.GreaterThan(.35f),
                "and for " + misnamed[kitchen] + " of the kitchen's " + sampled[kitchen] + ".");
        }

        [Test]
        public void TheDoorwayBandReachesSixTenthsOfAMetrePastAFloor()
        {
            var house = HouseRoomScopeWalkerTests.House();
            int living = Index("Living");
            Assert.That(HouseWhoIsWhere.DoorwayBand, Is.EqualTo(.6f), "The band of §3.2 e.");
            // On the floor is in it; past its north edge, up to 0.6 m.
            Assert.That(HouseWhoIsWhere.InDoorwayBand(house, living, new Vector3(-7f, 0f, -5f)), Is.True);
            Assert.That(HouseWhoIsWhere.InDoorwayBand(house, living, new Vector3(-7f, 0f, .59f)), Is.True);
            Assert.That(HouseWhoIsWhere.InDoorwayBand(house, living, new Vector3(-7f, 0f, .61f)), Is.False);
            // Off a corner by the true distance, not the larger of the two: 0.64 m out at 0.45 m on each axis.
            Assert.That(HouseWhoIsWhere.OutsideBy(house.Box(living), new Vector3(.4f, 0f, .4f)), Is.EqualTo(.5657f).Within(.001f));
            Assert.That(HouseWhoIsWhere.InDoorwayBand(house, living, new Vector3(.4f, 0f, .4f)), Is.True);
            Assert.That(HouseWhoIsWhere.InDoorwayBand(house, living, new Vector3(.45f, 0f, .45f)), Is.False);
            Assert.That(HouseWhoIsWhere.OutsideBy(house.Box(living), new Vector3(-7f, 0f, -5f)), Is.EqualTo(0f), "On the floor is no distance out.");
            Assert.That(HouseWhoIsWhere.InDoorwayBand(house, -1, Vector3.zero), Is.False, "No room has no band.");
            Assert.That(HouseWhoIsWhere.InDoorwayBand(null, living, Vector3.zero), Is.False);
        }

        [Test]
        public void TalkAcrossTwoFloorsNeedsBothInOneDoorwaysBand()
        {
            var house = HouseRoomScopeWalkerTests.House();
            int living = Index("Living"), bedroom = Index("Bedroom"), hoh = Index("HoH"), nomination = Index("Nomination");
            bool Talk(Vector2 one, int oneRoom, Vector2 two, int twoRoom)
                => HouseWhoIsWhere.FloorsLetThemTalk(house, V(one), oneRoom, V(two), twoRoom);

            // One floor: the floors have nothing against it, however far apart (range and sight are Talk's own).
            Assert.That(Talk(new Vector2(-13f, -9f), living, new Vector2(-1f, -1f), living), Is.True);
            // Either side of the line the living room and the bedroom share, both within 0.6 m of it.
            Assert.That(Talk(new Vector2(-7f, -.4f), living, new Vector2(-7f, .4f), bedroom), Is.True);
            Assert.That(Talk(new Vector2(-7f, -.59f), living, new Vector2(-7f, .59f), bedroom), Is.True);
            // One of them a step back from it: the other room, whichever of the two steps back.
            Assert.That(Talk(new Vector2(-7f, -1f), living, new Vector2(-7f, .4f), bedroom), Is.False, "The one in the living room is a metre from the bedroom.");
            Assert.That(Talk(new Vector2(-7f, -.4f), living, new Vector2(-7f, .7f), bedroom), Is.False, "The one in the bedroom is 0.7 m from the living room.");
            // The reviewers' case: the player 1 m into the bedroom, the pair 1.5 m into the living room.
            Assert.That(Talk(new Vector2(-5f, 1f), bedroom, new Vector2(-5.5f, -1.5f), living), Is.False);
            // A walker kept in the living room 0.2 m into the bedroom is on the bedroom's floor, so in its
            // band; the one they talk to must be in the living room's.
            Assert.That(Talk(new Vector2(-7f, .2f), living, new Vector2(-7f, .5f), bedroom), Is.True);
            Assert.That(Talk(new Vector2(-7f, .2f), living, new Vector2(-7f, 1.5f), bedroom), Is.False);
            // The south wing: the doorway between the HoH suite and the nomination room, and two metres
            // apart across its divider - a wall under Talk's sight line, which the band alone refuses.
            Assert.That(Talk(new Vector2(-5f, -15f), hoh, new Vector2(-4.2f, -15f), nomination), Is.True);
            Assert.That(Talk(new Vector2(-5.6f, -18f), hoh, new Vector2(-3.6f, -18f), nomination), Is.False);
            // Where the floors cannot say, they do not refuse.
            Assert.That(Talk(new Vector2(-5.6f, -18f), -1, new Vector2(-3.6f, -18f), nomination), Is.True);
            Assert.That(Talk(new Vector2(-5.6f, -18f), hoh, new Vector2(-3.6f, -18f), -1), Is.True);
            Assert.That(HouseWhoIsWhere.FloorsLetThemTalk(null, V(new Vector2(-5.6f, -18f)), hoh, V(new Vector2(-3.6f, -18f)), nomination), Is.True);
        }

        [Test]
        public void ASpotThreeTenthsInsideAFloorIsThatRoomToAWalkerFromAnywhere()
        {
            var house = HouseRoomScopeWalkerTests.House();
            int living = Index("Living");
            Assert.That(HouseWhoIsWhere.SurelyIn(house, living, new Vector3(-7f, 0f, -.31f)), Is.True);
            Assert.That(HouseWhoIsWhere.SurelyIn(house, living, new Vector3(-7f, 0f, -.29f)), Is.False, "Under 0.3 m in, a walker may keep the bedroom.");
            Assert.That(HouseWhoIsWhere.SurelyIn(house, living, new Vector3(-7f, 0f, .2f)), Is.False, "Off the floor is never in it.");
            Assert.That(HouseWhoIsWhere.SurelyIn(house, -1, Vector3.zero), Is.False);
            // Over every floor, every 0.1 m: a spot it calls surely in a room is that room to a walker
            // last in any room, or in none - what a walk to talk to someone relies on.
            int checkedSpots = 0;
            for (int room = 0; room < RoomLayers.Count; room++)
            {
                var box = house.Box(room);
                for (float x = box.xMin + .05f; x < box.xMax; x += .1f)
                    for (float z = box.yMin + .05f; z < box.yMax; z += .1f)
                    {
                        var feet = new Vector3(x, 0f, z);
                        if (!HouseWhoIsWhere.SurelyIn(house, room, feet)) continue;
                        checkedSpots++;
                        for (int from = -1; from < RoomLayers.Count; from++)
                        {
                            int walker = house.WalkerRoom(feet, from);
                            if (walker != room)
                                Assert.Fail(feet.ToString("F2") + " is surely the " + RoomLayers.RoomIds[room] + ", but a walker from "
                                    + (from < 0 ? "nowhere" : RoomLayers.RoomIds[from]) + " is in " + HouseRoomScope.RoomId(walker) + ".");
                        }
                    }
            }
            Assert.That(checkedSpots, Is.GreaterThan(95000), "The whole house was looked at: about 101,000 spots.");
        }

        /// <summary>A floor's half-metre grid, a quarter-metre in from its edges, column by column and back, as a walker crosses it.</summary>
        private static IEnumerable<Vector2> Grid(Rect box)
        {
            int column = 0;
            for (float x = box.xMin + .25f; x < box.xMax; x += .5f, column++)
            {
                var row = new List<float>();
                for (float z = box.yMin + .25f; z < box.yMax; z += .5f) row.Add(z);
                if (column % 2 == 1) row.Reverse();
                foreach (float z in row) yield return new Vector2(x, z);
            }
        }

        /// <summary>The room whose marker is nearest: who-is-where's rule before the floors.</summary>
        private static int NearestMarker(Vector2 feet)
        {
            int best = -1;
            float nearest = float.MaxValue;
            for (int room = 0; room < Markers.Length; room++)
            {
                float distance = (Markers[room] - feet).sqrMagnitude;
                if (distance >= nearest) continue;
                nearest = distance;
                best = room;
            }
            return best;
        }

        private static Vector3 V(Vector2 plan) => new Vector3(plan.x, 0f, plan.y);
    }
}
