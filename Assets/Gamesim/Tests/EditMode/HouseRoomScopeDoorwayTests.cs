using System.Collections.Generic;
using System.Linq;
using Gamesim.House;
using NUnit.Framework;
using UnityEngine;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The house's doorways from its walls (ROOM-SCOPE-PLAN §2.1, D17, RS5), on the eight floor boxes with
    /// walls of the fixture's own: every shared edge is read across at a tenth of a metre, an open run of
    /// 0.9 m or more is a doorway, a corner doorway is a half onto each floor, and the room view's icon for a
    /// room goes to the viewed room's doorway nearest the camera. Plain C#, so it runs under the editor's
    /// Mono without Unity.
    /// </summary>
    public sealed class HouseRoomScopeDoorwayTests
    {
        /// <summary>An opening in a wall line: the line x = Line (AlongZ) or z = Line, open from Low to High along it.</summary>
        private struct Gap
        {
            public bool AlongZ;
            public float Line, Low, High;
            public string First, Second;
        }

        // The plan's ten doorways (section 2.1) as openings in the shared edges: the two corner doorways, the
        // yard's (x -1.37 to 1.38 at z 10) and the nomination room's (x -1.29 to 1.43 at z -10), open onto x = 0,
        // so each reads as a half onto either floor.
        private static readonly Gap[] Gaps =
        {
            new Gap { AlongZ = true, Line = 0f, Low = -8f, High = -6.6f, First = "Living", Second = "Kitchen" },
            new Gap { AlongZ = true, Line = 0f, Low = -4f, High = -2.6f, First = "Living", Second = "Kitchen" },
            new Gap { AlongZ = false, Line = 0f, Low = -8f, High = -5.2f, First = "Living", Second = "Bedroom" },
            new Gap { AlongZ = false, Line = 0f, Low = 3f, High = 5.8f, First = "Kitchen", Second = "Private" },
            new Gap { AlongZ = true, Line = 0f, Low = 2f, High = 3.4f, First = "Bedroom", Second = "Private" },
            new Gap { AlongZ = true, Line = 0f, Low = 6f, High = 7.4f, First = "Bedroom", Second = "Private" },
            new Gap { AlongZ = false, Line = 10f, Low = -1.37f, High = 1.38f, First = "Bedroom", Second = "Yard" },
            new Gap { AlongZ = false, Line = -10f, Low = -1.29f, High = 1.43f, First = "Living", Second = "Nomination" },
            new Gap { AlongZ = true, Line = -4.62f, Low = -16f, High = -14.6f, First = "HoH", Second = "Nomination" },
            new Gap { AlongZ = true, Line = 4.77f, Low = -16f, High = -14.6f, First = "Nomination", Second = "Games" },
        };

        /// <summary>A wall across the look, unless it crosses its line in a gap.</summary>
        private static bool WallAcross(Vector3 from, Vector3 to)
        {
            var middle = (from + to) * .5f;
            bool acrossX = Mathf.Abs(to.x - from.x) > Mathf.Abs(to.z - from.z);
            foreach (var gap in Gaps)
            {
                if (gap.AlongZ != acrossX) continue;
                if (Mathf.Abs((acrossX ? middle.x : middle.z) - gap.Line) > .01f) continue;
                float along = acrossX ? middle.z : middle.x;
                if (along >= gap.Low && along <= gap.High) return false;
            }
            return true;
        }

        private static string Pair(HouseRoomScope.Doorway door) => RoomLayers.RoomIds[door.First] + "-" + RoomLayers.RoomIds[door.Second];

        [Test]
        public void EveryDoorwayIsFoundFromTheWallsAndACornerDoorwayIsAHalfOntoEachFloor()
        {
            var house = HouseRoomScopeWalkerTests.House();
            var found = new List<HouseRoomScope.Doorway>();
            house.FindDoorways(WallAcross, found);
            string report = string.Join(", ", found.Select(door => door + " (" + door.Width.ToString("0.00") + " m)"));
            var pairs = found.Select(Pair).OrderBy(pair => pair).ToArray();
            Assert.That(pairs, Is.EqualTo(new[]
            {
                "Bedroom-Private", "Bedroom-Private", "Bedroom-Yard", "HoH-Nomination", "Kitchen-Nomination", "Kitchen-Private",
                "Living-Bedroom", "Living-Kitchen", "Living-Kitchen", "Living-Nomination", "Nomination-Games", "Private-Yard",
            }), "The ten doorways, the two corner ones as halves: " + report);
            foreach (var door in found)
            {
                Assert.That(door.First, Is.LessThan(door.Second), door + ": the rooms in the layers' order.");
                Assert.That(door.Width, Is.GreaterThanOrEqualTo(HouseRoomScope.DoorwayWidth), door + " is a doorway's width.");
                Assert.That(door.Middle.y, Is.EqualTo(0f), "on the floor");
                // The middle lies on the two floors' shared edge.
                var one = house.Box(door.First);
                var two = house.Box(door.Second);
                bool onEdge = Mathf.Abs(door.Middle.x - one.xMax) < .01f && Mathf.Abs(door.Middle.x - two.xMin) < .01f
                    || Mathf.Abs(door.Middle.x - one.xMin) < .01f && Mathf.Abs(door.Middle.x - two.xMax) < .01f
                    || Mathf.Abs(door.Middle.z - one.yMax) < .01f && Mathf.Abs(door.Middle.z - two.yMin) < .01f
                    || Mathf.Abs(door.Middle.z - one.yMin) < .01f && Mathf.Abs(door.Middle.z - two.yMax) < .01f;
                Assert.That(onEdge, Is.True, door + " stands on the line its floors share.");
            }
            // The living room's doorway into the bedroom, to the tenth of a metre: its middle and its width.
            var living = found.Single(door => Pair(door) == "Living-Bedroom");
            Assert.That(living.Middle.x, Is.EqualTo(-6.6f).Within(.06f), report);
            Assert.That(living.Width, Is.EqualTo(2.8f).Within(.11f), report);
            // The nomination room's corner doorway: a half onto the living room, a half onto the kitchen.
            var west = found.Single(door => Pair(door) == "Living-Nomination");
            var east = found.Single(door => Pair(door) == "Kitchen-Nomination");
            Assert.That(west.Middle.x, Is.LessThan(0f), "The living room's half is west of x = 0.");
            Assert.That(east.Middle.x, Is.GreaterThan(0f), "The kitchen's half is east of it.");
        }

        [Test]
        public void TheIconsDoorwayIsTheViewedRoomsNearestToTheCameraAndNoneWithoutOne()
        {
            var house = HouseRoomScopeWalkerTests.House();
            var found = new List<HouseRoomScope.Doorway>();
            house.FindDoorways(WallAcross, found);
            int living = RoomLayers.IndexOf("Living"), kitchen = RoomLayers.IndexOf("Kitchen"), privateRoom = RoomLayers.IndexOf("Private");
            int yard = RoomLayers.IndexOf("Yard"), bedroom = RoomLayers.IndexOf("Bedroom"), games = RoomLayers.IndexOf("Games");
            // The scope's own lookup reads the walls it was built from; a plan scope has none, so the test's own list
            // stands in through the same rule: the nearest of the doorways that join the two rooms, either way.
            HouseRoomScope.Doorway Nearest(int from, int to, Vector3 near)
                => found.Where(door => door.Joins(from, to)).OrderBy(door => (door.Middle - new Vector3(near.x, 0f, near.z)).sqrMagnitude).First();
            var north = Nearest(living, kitchen, new Vector3(-1f, 0f, -9f));
            var south = Nearest(living, kitchen, new Vector3(-1f, 0f, -1f));
            Assert.That(north.Middle.z, Is.LessThan(-6f), "From the living room's south end, the kitchen's southern doorway.");
            Assert.That(south.Middle.z, Is.GreaterThan(-4.5f), "From its north end, the northern one.");
            Assert.That(found.Any(door => door.Joins(kitchen, living)), Is.True, "A doorway joins its rooms either way round.");
            Assert.That(found.Any(door => door.Joins(living, privateRoom)), Is.False, "The living room and the private room share a corner, not a doorway.");
            Assert.That(found.Any(door => door.Joins(living, yard)), Is.False, "The yard is no doorway from the living room: its icon goes to the frame's edge.");
            Assert.That(found.Any(door => door.Joins(bedroom, games)), Is.False);
            Assert.That(found.Count(door => door.Joins(yard, bedroom) || door.Joins(yard, privateRoom)), Is.EqualTo(2), "From the yard, a half doorway into each of the two rooms behind it.");
            // A scope built from rectangles has no walls to read: no doorways, and no lookup finds one.
            Assert.That(house.Doorways, Is.Empty, "A plan scope reads no walls.");
            Assert.That(house.TryDoorway(living, kitchen, Vector3.zero, out _), Is.False);
        }

        [Test]
        public void WallsEverywhereHaveNoDoorwayAndNoWallsOneAlongEachSharedEdge()
        {
            var house = HouseRoomScopeWalkerTests.House();
            var found = new List<HouseRoomScope.Doorway>();
            house.FindDoorways((from, to) => true, found);
            Assert.That(found, Is.Empty, "Walls all along every shared edge: no doorway.");
            house.FindDoorways((from, to) => false, found);
            // Living-Kitchen, Living-Bedroom, Kitchen-Private, Bedroom-Private, Bedroom-Yard, Private-Yard, Living-Nomination,
            // Kitchen-Nomination, Living-HoH, HoH-Nomination, Nomination-Games, Kitchen-Games: twelve shared edges, each open whole.
            Assert.That(found.Select(Pair).OrderBy(pair => pair).ToArray(), Is.EqualTo(new[]
            {
                "Bedroom-Private", "Bedroom-Yard", "HoH-Nomination", "Kitchen-Games", "Kitchen-Nomination", "Kitchen-Private",
                "Living-Bedroom", "Living-HoH", "Living-Kitchen", "Living-Nomination", "Nomination-Games", "Private-Yard",
            }), string.Join(", ", found));
            var whole = found.Single(door => Pair(door) == "Living-Kitchen");
            Assert.That(whole.Width, Is.EqualTo(10f).Within(.11f), "Open all along: the whole shared edge.");
            house.FindDoorways(null, found);
            Assert.That(found, Is.Empty, "No look across, no doorways.");
        }
    }
}
