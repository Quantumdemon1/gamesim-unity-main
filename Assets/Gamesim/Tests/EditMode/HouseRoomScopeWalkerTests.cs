using Gamesim.House;
using NUnit.Framework;
using UnityEngine;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The walker rule (ROOM-SCOPE-PLAN §3.2 a) on the house's eight floor boxes, with no scene: a room
    /// changes once the feet are three tenths of a metre inside a new floor, the last room is kept
    /// until then, and a walker with no room yet takes the floor it stands on, or the nearest. Plain
    /// C#, so it runs under the editor's Mono without Unity.
    /// </summary>
    public sealed class HouseRoomScopeWalkerTests
    {
        // The shipped house's floors in plan (EpisodeHouse.unity): x, z, width, depth.
        private static readonly string[] Rooms = { "Living", "Kitchen", "Bedroom", "Private", "Yard", "HoH", "Nomination", "Games" };
        private static readonly Rect[] Plans =
        {
            Rect.MinMaxRect(-14f, -10f, 0f, 0f), Rect.MinMaxRect(0f, -10f, 14f, 0f),
            Rect.MinMaxRect(-14f, 0f, 0f, 10f), Rect.MinMaxRect(0f, 0f, 14f, 10f),
            Rect.MinMaxRect(-14f, 10f, 14f, 20f), Rect.MinMaxRect(-14f, -20f, -4.62f, -10f),
            Rect.MinMaxRect(-4.62f, -20f, 4.77f, -10f), Rect.MinMaxRect(4.77f, -20f, 14.15f, -10f),
        };

        private static HouseRoomScope House() => HouseRoomScope.FromPlan(Rooms, Plans);

        private static int Index(string room) => RoomLayers.IndexOf(room);

        [Test]
        public void TheWalkerRoomChangesThreeTenthsOfAMetreInsideTheNewFloor()
        {
            var house = House();
            int changes = 0;
            int checkedEdges = 0;
            // Every pair of floors that share an edge, crossed at the middle of what they share, both
            // ways, in centimetre steps from a metre inside one to a metre inside the other.
            for (int a = 0; a < RoomLayers.Count; a++)
                for (int b = 0; b < RoomLayers.Count; b++)
                {
                    if (a == b || !SharedEdge(Plans[a], Plans[b], out var middle, out var across)) continue;
                    checkedEdges++;
                    int room = house.WalkerRoom(V(middle - across), -1);
                    Assert.That(room, Is.EqualTo(a), Rooms[a] + " is where a walker a metre inside it stands.");
                    int changedAtStep = -1, changedTo = -1;
                    for (int step = -100; step <= 100; step++)
                    {
                        var at = middle + across * (step / 100f);
                        int next = house.WalkerRoom(V(at), room);
                        if (next == room) continue;
                        Assert.That(changedAtStep, Is.EqualTo(-1), Rooms[a] + " to " + Rooms[b] + " changed room twice in one crossing.");
                        changedAtStep = step;
                        changedTo = next;
                        room = next;
                        changes++;
                    }
                    Assert.That(changedTo, Is.EqualTo(b), Rooms[a] + " to " + Rooms[b] + ": the walker ends in the room it walked into.");
                    // Thirty centimetres in, to the centimetre (a float wall line at -4.62 lands the
                    // thirtieth step a hair short): not on the wall line, not a stride later.
                    Assert.That(changedAtStep, Is.InRange(30, 31), Rooms[a] + " to " + Rooms[b] + " changed " + changedAtStep + " cm past the wall line.");
                }
            Assert.That(checkedEdges, Is.EqualTo(24), "Twelve shared edges, each crossed both ways.");
            Assert.That(changes, Is.EqualTo(checkedEdges), "One change per crossing.");

            // A shuffle in a doorway keeps the room: up to 29 cm into the bedroom and back, a dozen
            // times, and the walker is still in the living room; at 30 cm it is in the bedroom, and
            // the same shuffle the other way keeps it there.
            int living = Index("Living"), bedroom = Index("Bedroom");
            int shuffled = house.WalkerRoom(new Vector3(-7f, 0f, -1f), -1);
            Assert.That(shuffled, Is.EqualTo(living));
            for (int i = 0; i < 12; i++)
            {
                shuffled = house.WalkerRoom(new Vector3(-7f, 0f, .29f), shuffled);
                shuffled = house.WalkerRoom(new Vector3(-7f, 0f, -.29f), shuffled);
                shuffled = house.WalkerRoom(new Vector3(-7f, 0f, 0f), shuffled);
                Assert.That(shuffled, Is.EqualTo(living), "A doorway shuffle never leaves the room.");
            }
            shuffled = house.WalkerRoom(new Vector3(-7f, 0f, .3f), shuffled);
            Assert.That(shuffled, Is.EqualTo(bedroom), "Thirty centimetres in is in.");
            for (int i = 0; i < 12; i++)
            {
                shuffled = house.WalkerRoom(new Vector3(-7f, 0f, -.29f), shuffled);
                shuffled = house.WalkerRoom(new Vector3(-7f, 0f, .01f), shuffled);
                Assert.That(shuffled, Is.EqualTo(bedroom), "And the shuffle back keeps the new room.");
            }

            // Outside every floor - past an outer wall, in a gap - the last room is kept.
            Assert.That(house.WalkerRoom(new Vector3(-14.6f, 0f, -5f), living), Is.EqualTo(living));
            Assert.That(house.WalkerRoom(new Vector3(0f, 0f, 25f), bedroom), Is.EqualTo(bedroom),
                "Far past the yard's fence, a walker last in the bedroom is still the bedroom's until it stands in another floor.");
        }

        [Test]
        public void AnUnlocatedWalkerTakesTheNearestFloor()
        {
            var house = House();
            // On a floor, however near its edge: that floor, not a doorway's nothing.
            Assert.That(house.WalkerRoom(new Vector3(.1f, 0f, -5f), -1), Is.EqualTo(Index("Kitchen")),
                "Ten centimetres into the kitchen is the kitchen for a walker with no room yet.");
            Assert.That(house.WalkerRoom(new Vector3(-.05f, 0f, 9.95f), -1), Is.EqualTo(Index("Bedroom")));
            // Exactly on a shared edge: the first of the two, in the room order.
            Assert.That(house.WalkerRoom(new Vector3(0f, 0f, -5f), -1), Is.EqualTo(Index("Living")));
            // Off every floor: the nearest box.
            Assert.That(house.WalkerRoom(new Vector3(-15f, 0f, -5f), -1), Is.EqualTo(Index("Living")), "a metre west of the living room");
            Assert.That(house.WalkerRoom(new Vector3(0f, 0f, 21f), -1), Is.EqualTo(Index("Yard")), "a metre past the yard's fence");
            Assert.That(house.WalkerRoom(new Vector3(14.6f, 0f, -15f), -1), Is.EqualTo(Index("Games")), "east of the games room");
            Assert.That(house.WalkerRoom(new Vector3(15.5f, 0f, -9f), -1), Is.EqualTo(Index("Kitchen")),
                "beyond a corner, by the distance to the box (1.5 m to the kitchen, 1.68 m to the games room), not to its nearest edge's line");
            Assert.That(house.RoomUnder(new Vector3(5f, 0f, -21f)), Is.EqualTo(Index("Games")), "the free camera's point takes the same floor");

            // A last room this house lacks counts as none: the prototype has no south wing.
            var prototype = HouseRoomScope.FromPlan(new[] { "Living", "Kitchen", "Bedroom", "Private", "Yard" },
                new[] { Plans[0], Plans[1], Plans[2], Plans[3], Plans[4] });
            Assert.That(prototype.Has(Index("HoH")), Is.False);
            Assert.That(prototype.WalkerRoom(new Vector3(-5f, 0f, -12f), Index("HoH")), Is.EqualTo(Index("Living")),
                "a walker last in a room this house lacks takes the nearest floor it has");

            // A position that is not one: a known walker keeps its room, an unknown one has none.
            var nowhere = new Vector3(float.NaN, 0f, 0f);
            Assert.That(house.WalkerRoom(nowhere, Index("Kitchen")), Is.EqualTo(Index("Kitchen")));
            Assert.That(house.WalkerRoom(nowhere, -1), Is.EqualTo(-1));
            Assert.That(HouseRoomScope.FromPlan(new string[0], new Rect[0]).WalkerRoom(Vector3.zero, -1), Is.EqualTo(-1),
                "and a house with no floors has no rooms to give");
        }

        /// <summary>The middle of the edge two floors share, and the step a metre across it from the first into the second.</summary>
        private static bool SharedEdge(Rect from, Rect to, out Vector2 middle, out Vector2 across)
        {
            middle = default;
            across = default;
            const float Touch = .01f, Overlap = .5f;
            float zLow = Mathf.Max(from.yMin, to.yMin), zHigh = Mathf.Min(from.yMax, to.yMax);
            float xLow = Mathf.Max(from.xMin, to.xMin), xHigh = Mathf.Min(from.xMax, to.xMax);
            if (zHigh - zLow >= Overlap && Mathf.Abs(from.xMax - to.xMin) < Touch)
            { middle = new Vector2(from.xMax, (zLow + zHigh) * .5f); across = Vector2.right; return true; }
            if (zHigh - zLow >= Overlap && Mathf.Abs(from.xMin - to.xMax) < Touch)
            { middle = new Vector2(from.xMin, (zLow + zHigh) * .5f); across = Vector2.left; return true; }
            if (xHigh - xLow >= Overlap && Mathf.Abs(from.yMax - to.yMin) < Touch)
            { middle = new Vector2((xLow + xHigh) * .5f, from.yMax); across = Vector2.up; return true; }
            if (xHigh - xLow >= Overlap && Mathf.Abs(from.yMin - to.yMax) < Touch)
            { middle = new Vector2((xLow + xHigh) * .5f, from.yMin); across = Vector2.down; return true; }
            return false;
        }

        private static Vector3 V(Vector2 plan) => new Vector3(plan.x, 0f, plan.y);
    }
}
