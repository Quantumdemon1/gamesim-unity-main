using System.Collections.Generic;
using Gamesim.Episode;
using Gamesim.House;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The lamps' rule (ROOM-SCOPE-PLAN §3.2 d, D8 i, RS3) with no scene: a lamp is off for the main
    /// camera only while a scoped view - neither the whole house nor no room - leaves its home room
    /// out; and with the house's lamps at the homes RS1 pins, each room view keeps §3.5's shadow maps
    /// and per-map sizes in the 2048 atlas. Plain C#, so it runs under the editor's Mono without
    /// Unity. The PlayMode tests (EpisodePlayModeTests.RoomScopeLamps) read the same from the main
    /// camera's own render.
    /// </summary>
    public sealed class HouseRoomScopeLampTests
    {
        /// <summary>
        /// The lamps per home room: RS1's homes (Living 3, Bedroom 3, HoH 3, Nomination 2, Private 2,
        /// Games 1, Kitchen 0, Yard 0), here in <see cref="RoomLayers"/> order (Living, Kitchen, Bedroom,
        /// Private, Yard, HoH, Nomination, Games): 3, 0, 3, 2, 0, 3, 2, 1.
        /// </summary>
        private static readonly int[] LampsAtHome = { 3, 0, 3, 2, 0, 3, 2, 1 };

        [Test]
        public void ALampIsOffOnlyWhileAScopedViewLeavesItsRoomOut()
        {
            for (int home = 0; home < RoomLayers.Count; home++)
            {
                string at = " (a lamp in the " + RoomLayers.RoomIds[home] + ")";
                Assert.That(HouseRoomScope.LampOutsideScope(home, RoomSet.WholeHouse), Is.False, "The whole house keeps every lamp" + at);
                Assert.That(HouseRoomScope.LampOutsideScope(home, RoomSet.None), Is.False, "No room at all is no scope: lit" + at);
                for (int viewed = 0; viewed < RoomLayers.Count; viewed++)
                    Assert.That(HouseRoomScope.LampOutsideScope(home, RoomSet.Of(viewed)), Is.EqualTo(home != viewed),
                        "Viewing the " + RoomLayers.RoomIds[viewed] + at);
                var withNext = RoomSet.Of(home).With(RoomSet.Of((home + 1) % RoomLayers.Count));
                Assert.That(HouseRoomScope.LampOutsideScope(home, withNext), Is.False, "Two rooms, a timed move's union, keep both rooms' lamps" + at);
                var allButHome = RoomSet.FromBits(RoomSet.WholeHouse.Bits & ~(1 << home));
                Assert.That(HouseRoomScope.LampOutsideScope(home, allButHome), Is.True, "Every room but its own: off" + at);
            }
            Assert.That(HouseRoomScope.LampOutsideScope(-1, RoomSet.Of("Kitchen")), Is.False, "A lamp with no home room stays on.");
            Assert.That(HouseRoomScope.LampOutsideScope(RoomLayers.Count, RoomSet.Of("Kitchen")), Is.False, "Nor does one past the eight.");
        }

        [TestCase("Living", 19, 256)]
        [TestCase("Kitchen", 1, 256)]
        [TestCase("Bedroom", 19, 256)]
        [TestCase("Private", 13, 512)]
        [TestCase("Yard", 1, 256)]
        [TestCase("HoH", 19, 256)]
        [TestCase("Nomination", 13, 512)]
        [TestCase("Games", 7, 512)]
        public void EachRoomViewKeepsOnlyItsOwnLampsMaps(string room, int maps, int mapSize)
        {
            var atlas = AtlasFor(RoomSet.Of(room), out int lamps);
            Assert.That(lamps, Is.EqualTo(LampsAtHome[RoomLayers.IndexOf(room)]), "Only the " + room + "'s own lamps stay on.");
            Assert.That(atlas.slicesRequested, Is.EqualTo(maps), "§3.5: six faces a lamp and the follow spotlight's one.");
            Assert.That(atlas.slicesKept, Is.EqualTo(maps), "Nothing is dropped.");
            Assert.That(atlas.largestSlice, Is.EqualTo(mapSize), "§3.5's size a map, against the whole house's 128.");
            Assert.That(atlas.tooMany, Is.False);
        }

        [Test]
        public void TheWholeHouseKeepsEveryLampAtAnEighth()
        {
            int total = 0;
            foreach (int count in LampsAtHome) total += count;
            Assert.That(total, Is.EqualTo(14), "The house's fourteen lamps.");
            var atlas = AtlasFor(RoomSet.WholeHouse, out int lamps);
            Assert.That(lamps, Is.EqualTo(14), "The whole house: every lamp stays on.");
            Assert.That(atlas.slicesRequested, Is.EqualTo(85), "84 lamp faces and the follow spotlight's one.");
            Assert.That(atlas.scale, Is.EqualTo(8), "Every map reduced by 8,");
            Assert.That(atlas.largestSlice, Is.EqualTo(128), "to 128 px: today's house.");
        }

        /// <summary>
        /// The lamps a view keeps on, soft at the High tier (six 1024 px faces), at the plan's homes,
        /// and the follow spotlight (hard, Low tier, 256 px, one face), laid in a 2048 atlas as URP 17.6 does.
        /// </summary>
        private static VerificationShadowAtlas AtlasFor(RoomSet viewed, out int lamps)
        {
            var lights = new List<VerificationShadowLight>();
            lamps = 0;
            for (int home = 0; home < RoomLayers.Count; home++)
                for (int n = 0; n < LampsAtHome[home]; n++)
                {
                    if (HouseRoomScope.LampOutsideScope(home, viewed)) continue;
                    lamps++;
                    lights.Add(new VerificationShadowLight(RoomLayers.RoomIds[home] + " lamp " + n, home * 2 + n, 1, 0, 6, false, true, 1024));
                }
            lights.Add(new VerificationShadowLight("follow spot", 0, 4.2, 0, 6.7, true, false, 256));
            return VerificationViews.LayoutShadowAtlas(lights, 0, 12, -10, 2048);
        }
    }
}
