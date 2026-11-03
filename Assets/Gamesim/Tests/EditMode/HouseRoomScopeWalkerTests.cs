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

        /// <summary>The shipped house's eight floors, as a scope with no scene (the plan rule's fixture reads it too).</summary>
        internal static HouseRoomScope House() => HouseRoomScope.FromPlan(Rooms, Plans);

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

            // Outside every floor - past an outer wall, in a gap - the last room is kept while the feet
            // are within three tenths of a metre of it; farther out, the floor nearest them.
            Assert.That(house.WalkerRoom(new Vector3(-14.2f, 0f, -5f), living), Is.EqualTo(living));
            Assert.That(house.WalkerRoom(new Vector3(-14.6f, 0f, -5f), living), Is.EqualTo(living), "the nearest floor is still the living room");
            Assert.That(house.WalkerRoom(new Vector3(0f, 0f, 25f), bedroom), Is.EqualTo(Index("Yard")),
                "Far past the yard's fence, a walker last in the bedroom is nearest the yard, not still the bedroom's.");
            // A warp lands where it lands: a body put a fifth of a metre inside the games room, last
            // seen in the bedroom, is in the games room, not the bedroom across the house.
            Assert.That(house.WalkerRoom(new Vector3(9.5f, 0f, -19.8f), bedroom), Is.EqualTo(Index("Games")));
        }

        [Test]
        public void TheWalkerRoomChangesThreeTenthsOfAMetrePastACornerDoorway()
        {
            var house = House();
            // The yard's doorway and the nomination room's open onto x = 0, where two floors of the
            // north block meet, and the north block's own walls are open there too. Through their
            // middle a walker stands centimetres inside either floor, never 0.3 m inside one: the room
            // still changes, once, three tenths of a metre past the wall line, to the floor underfoot.
            AssertOneChange(house, "Nomination", new Vector2(.07f, -11f), new Vector2(.07f, -8.7f), "Kitchen", new Vector2(.07f, -10f));
            AssertOneChange(house, "Kitchen", new Vector2(.07f, -8.7f), new Vector2(.07f, -11f), "Nomination", new Vector2(.07f, -10f));
            AssertOneChange(house, "Nomination", new Vector2(-.05f, -11f), new Vector2(-.05f, -8.7f), "Living", new Vector2(-.05f, -10f));
            AssertOneChange(house, "Yard", new Vector2(.005f, 11f), new Vector2(.005f, 8.7f), "Private", new Vector2(.005f, 10f));
            AssertOneChange(house, "Private", new Vector2(.005f, 8.7f), new Vector2(.005f, 11f), "Yard", new Vector2(.005f, 10f));
            AssertOneChange(house, "Yard", new Vector2(-.1f, 11f), new Vector2(-.1f, 8.7f), "Bedroom", new Vector2(-.1f, 10f));
            // And across x = 0 within 0.3 m of the corner, through the gap in the wall beside the
            // nomination room's doorway: never 0.3 m inside the kitchen's box, still a change at 0.3 m.
            AssertOneChange(house, "Living", new Vector2(-1f, -9.8f), new Vector2(1f, -9.8f), "Kitchen", new Vector2(0f, -9.8f));
            AssertOneChange(house, "Kitchen", new Vector2(1f, -9.8f), new Vector2(-1f, -9.8f), "Living", new Vector2(0f, -9.8f));
            AssertOneChange(house, "Bedroom", new Vector2(-1f, 9.8f), new Vector2(1f, 9.8f), "Private", new Vector2(0f, 9.8f));
        }

        /// <summary>
        /// Walks from <paramref name="start"/> to <paramref name="end"/> in centimetre steps and asserts
        /// one change, from <paramref name="from"/> to <paramref name="to"/>, 30 or 31 cm past the wall
        /// line through <paramref name="wall"/>.
        /// </summary>
        private static void AssertOneChange(HouseRoomScope house, string from, Vector2 start, Vector2 end, string to, Vector2 wall)
        {
            string walk = from + " to " + to + " at " + start;
            int room = house.WalkerRoom(V(start), -1);
            Assert.That(room, Is.EqualTo(Index(from)), walk + ": the walk starts in " + from + ".");
            var across = (end - start).normalized;
            int steps = Mathf.RoundToInt((end - start).magnitude * 100f), changes = 0;
            float changedPast = float.NaN;
            for (int step = 1; step <= steps; step++)
            {
                var at = start + (end - start) * (step / (float)steps);
                int next = house.WalkerRoom(V(at), room);
                if (next == room) continue;
                changes++;
                changedPast = Vector2.Dot(at - wall, across);
                Assert.That(next, Is.EqualTo(Index(to)), walk + ": changed to " + HouseRoomScope.RoomId(next) + ".");
                room = next;
            }
            Assert.That(changes, Is.EqualTo(1), walk + ": one change, not " + changes + ".");
            Assert.That(changedPast, Is.InRange(.2999f, .3101f), walk + ": changed " + changedPast + " m past the wall line.");
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
