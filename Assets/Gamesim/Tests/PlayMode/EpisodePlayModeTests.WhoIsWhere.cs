using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Gamesim.Episode;
using Gamesim.House;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// Who is where by floor (ROOM-SCOPE-PLAN §3.2 k, RSW; the owner's D14, "Follow the floor"): who
    /// is where reads the floor each body stands on, not the nearest room marker, and everything that
    /// reads it follows - the walk-in watch, the free-time board's HERE, the room icons' counts - and
    /// Talk needs the two on one floor or both in one doorway's band. Bodies are stood where a test
    /// wants them, the houseguests let go by their motion and the player's agent no longer placing
    /// it, so each case reads exactly the point it names.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        /// <summary>
        /// Every point of every floor names that floor's room in the notebook's Who is where: the whole
        /// house in a half-metre grid a quarter-metre in from every edge, each body walked in through
        /// the floor's middle and across it, as a walker crosses a room. The grid is the one the plan
        /// measured the markers on, where the nearest marker named another room for 39.5% of the
        /// living room.
        /// </summary>
        [UnityTest]
        public IEnumerator WhoIsWhere_EveryFloorPointNamesItsOwnRoom()
        {
            director.ClosePanels();
            director.SuspendNpcAutonomyForDiagnostics();
            yield return null;
            var scope = ScopeOf(director);
            var markers = RswMarkers();
            var bodies = SceneComponents<HouseNpc>().Where(npc => npc.gameObject.activeInHierarchy)
                .Select(npc => (id: npc.Id, body: npc.transform)).ToList();
            bodies.Add((director.Snapshot.playerId, player.transform));
            Assert.That(bodies.Count, Is.GreaterThan(1), "The player and the houseguests walk the floors.");
            var agent = player.Agent;
            bool agentPlaces = agent.updatePosition;
            agent.ResetPath();
            agent.updatePosition = false;
            var failures = new List<string>();
            int sampled = 0, livingSampled = 0, livingMisnamed = 0;
            try
            {
                for (int room = 0; room < RoomLayers.Count; room++)
                {
                    string id = RoomLayers.RoomIds[room];
                    var box = scope.Box(room);
                    var points = RswFloorGrid(box);
                    // In through the middle, everyone, read as the notebook reads them.
                    foreach (var (_, body) in bodies) body.position = new Vector3(box.center.x, 0f, box.center.y);
                    var entered = RswRoomsOf(director.WhoIsWhere());
                    foreach (var (who, _) in bodies)
                        if (!entered.TryGetValue(who, out var first) || first != id) failures.Add(who + " walked into the " + id + "'s middle and is in " + (first ?? "no room"));
                    // Then across it: each body its own run of the grid, a step at a time.
                    int lanes = bodies.Count, run = (points.Count + lanes - 1) / lanes;
                    for (int step = 0; step < run; step++)
                    {
                        for (int lane = 0; lane < lanes; lane++)
                            if (lane * run + step < points.Count) bodies[lane].body.position = points[lane * run + step];
                        var rooms = RswRoomsOf(director.WhoIsWhere());
                        for (int lane = 0; lane < lanes; lane++)
                        {
                            int at = lane * run + step;
                            if (at >= points.Count) continue;
                            sampled++;
                            string named = rooms.TryGetValue(bodies[lane].id, out var where) ? where : null;
                            if (named != id && failures.Count < 20)
                                failures.Add(points[at].ToString("F2") + " on the " + id + "'s floor is " + (named ?? "no room"));
                            if (id != "Living") continue;
                            livingSampled++;
                            if (RswNearestMarker(markers, points[at]) != id) livingMisnamed++;
                        }
                    }
                }
            }
            finally { agent.updatePosition = agentPlaces; }
            Assert.That(failures, Is.Empty, string.Join("; ", failures));
            Assert.That(sampled, Is.GreaterThan(4000), "The whole house, every half metre.");
            Assert.That(livingMisnamed / (float)livingSampled, Is.GreaterThan(.35f), "The grid holds the part of the living room the nearest marker"
                + " named another room: " + livingMisnamed + " of its " + livingSampled + " points.");
        }

        /// <summary>
        /// The reviewers' case (D14): the player a metre into the bedroom and two houseguests standing
        /// together 1.5 m into the living room, the wall between them. The nearest marker put all three
        /// in the bedroom, and the walk-in watch offered the pair as a bedroom walk-in, in a room the
        /// room view will not draw. By the floors the pair are in the living room, and nothing is
        /// offered until the player is in there with them.
        /// </summary>
        [UnityTest]
        public IEnumerator WalkIn_APairAcrossTheLivingBedroomWallIsNotOffered()
        {
            director.ClosePanels();
            director.SuspendNpcAutonomyForDiagnostics();
            yield return null;
            var scope = ScopeOf(director);
            ScopeParkHouseguests(scope);
            var pair = SceneComponents<HouseNpc>().Where(npc => npc.gameObject.activeInHierarchy)
                .OrderBy(npc => npc.Id, StringComparer.Ordinal).Take(2).ToArray();
            Assert.That(pair, Has.Length.EqualTo(2));
            var markers = RswMarkers();
            var agent = player.Agent;
            bool agentPlaces = agent.updatePosition;
            agent.ResetPath();
            agent.updatePosition = false;
            try
            {
                RswStand(player.transform, new Vector3(-5f, 0f, 1f));
                RswStand(pair[0].transform, new Vector3(-5.5f, 0f, -1.5f));
                RswStand(pair[1].transform, new Vector3(-4.5f, 0f, -1.5f));
                foreach (var body in new[] { player.transform, pair[0].transform, pair[1].transform })
                    Assert.That(RswNearestMarker(markers, body.position), Is.EqualTo("Bedroom"),
                        "The case as the reviewers found it: the bedroom's marker is the nearest to " + body.name + ".");
                Assert.That(RswSolidAcross(player.transform, pair[0].transform, 1.15f), Is.True, "The living room's wall stands between them.");

                Assert.That(director.WalkerRoomOf(player.transform), Is.EqualTo("Bedroom"));
                var rooms = RswRoomsOf(director.WhoIsWhere());
                Assert.That(rooms[director.Snapshot.playerId], Is.EqualTo("Bedroom"), "Who is where has the player in the bedroom");
                foreach (var npc in pair)
                    Assert.That(rooms[npc.Id], Is.EqualTo("Living"), "and " + npc.DisplayName + " in the living room, where they stand.");
                Assert.That(RswWalkIn(out var room, out var first, out var second), Is.False,
                    "No walk-in on a pair in another room: " + first + " and " + second + " in the " + room + ".");

                // Through the door and beside them, it is offered, in the living room.
                RswStand(player.transform, new Vector3(-5f, 0f, -2.5f));
                Assert.That(RswWalkIn(out room, out first, out second), Is.True, "Beside them on their floor, the two are walked in on.");
                Assert.That(room, Is.EqualTo("Living"), "In the room they are in.");
                Assert.That((first, second), Is.EqualTo((pair[0].Id, pair[1].Id)));
            }
            finally { agent.updatePosition = agentPlaces; }
        }

        /// <summary>
        /// The south wing's walls are 1.1 m, under the 1.15 m line Talk looks along, so two people two
        /// metres apart across one could talk. Not on two floors now - nor hugging the wall from
        /// either side, though each is within the other floor's band. In the doorway between the same
        /// two rooms, both in its band, they talk; a step back into the suite, they do not; and on one
        /// floor, as before.
        /// </summary>
        [UnityTest]
        public IEnumerator Talk_IsNotOfferedOverASouthWingWall()
        {
            director.ClosePanels();
            director.SuspendNpcAutonomyForDiagnostics();
            yield return null;
            var scope = ScopeOf(director);
            ScopeParkHouseguests(scope);
            var guest = SceneComponents<HouseNpc>().First(npc => npc.gameObject.activeInHierarchy);
            var agent = player.Agent;
            bool agentPlaces = agent.updatePosition;
            agent.ResetPath();
            agent.updatePosition = false;
            try
            {
                // Two metres apart across the HoH suite's wall with the nomination room, south of its doorway.
                RswStand(player.transform, new Vector3(-5.6f, 0f, -18f));
                RswStand(guest.transform, new Vector3(-3.6f, 0f, -18f));
                Assert.That(director.WalkerRoomOf(player.transform), Is.EqualTo("HoH"));
                Assert.That(director.WalkerRoomOf(guest.transform), Is.EqualTo("Nomination"));
                Assert.That(RswSolidAcross(player.transform, guest.transform, HouseWhoIsWhere.BelowEveryWall), Is.True, "A wall stands between them,");
                Assert.That(RswSolidAcross(player.transform, guest.transform, 1.15f), Is.False,
                    "under Talk's sight line: the rule before the floors offered this conversation.");
                Assert.That(RswCanTalk(guest), Is.False, "Across a wall, on two floors, nobody talks.");
                Assert.That(director.TryOpenNpc(guest.Id), Is.False, "No conversation opens.");

                // Hugging the wall from either side: each within 0.6 m of the other's floor, the wall between them.
                RswStand(player.transform, new Vector3(-5f, 0f, -18f));
                RswStand(guest.transform, new Vector3(-4.2f, 0f, -18f));
                Assert.That(director.WalkerRoomOf(player.transform), Is.EqualTo("HoH"));
                Assert.That(director.WalkerRoomOf(guest.transform), Is.EqualTo("Nomination"));
                Assert.That(RswCanTalk(guest), Is.False, "0.8 m apart, both in the band, the wall between them: no.");
                Assert.That(director.TryOpenNpc(guest.Id), Is.False);

                // The same two rooms' doorway: both in its band, nothing between.
                RswStand(player.transform, new Vector3(-5f, 0f, -15f));
                RswStand(guest.transform, new Vector3(-4.2f, 0f, -15f));
                Assert.That(director.WalkerRoomOf(player.transform), Is.EqualTo("HoH"));
                Assert.That(director.WalkerRoomOf(guest.transform), Is.EqualTo("Nomination"));
                Assert.That(RswSolidAcross(player.transform, guest.transform, HouseWhoIsWhere.BelowEveryWall), Is.False, "The doorway is open.");
                Assert.That(RswCanTalk(guest), Is.True, "In one doorway's band, the two talk across it.");

                // A step back into the suite: through the same doorway, but out of its band.
                RswStand(player.transform, new Vector3(-6.4f, 0f, -15f));
                Assert.That(RswCanTalk(guest), Is.False, "2.2 m apart through the doorway, the player 1.8 m from the nomination room: no.");

                // On one floor, as before.
                RswStand(player.transform, new Vector3(-3.4f, 0f, -15f));
                Assert.That(director.WalkerRoomOf(player.transform), Is.EqualTo("Nomination"));
                Assert.That(RswCanTalk(guest), Is.True, "In the same room, they talk.");
                Assert.That(director.TryOpenNpc(guest.Id), Is.True, "and the conversation opens.");
            }
            finally
            {
                director.ClosePanels();
                agent.updatePosition = agentPlaces;
            }
        }

        /// <summary>
        /// A click on someone too far to talk to walks the player over, and the walk ends on their
        /// floor. Standing just inside the nomination room at its doorway, with the player back in the
        /// HoH suite, the first spot towards the player is through the doorway in the suite - where
        /// Talk would not open, and the walk would go on until it gave up.
        /// </summary>
        [UnityTest]
        public IEnumerator Talk_AWalkOverEndsOnTheirFloor()
        {
            director.ClosePanels();
            director.SuspendNpcAutonomyForDiagnostics();
            yield return null;
            var scope = ScopeOf(director);
            ScopeParkHouseguests(scope);
            var guest = SceneComponents<HouseNpc>().First(npc => npc.gameObject.activeInHierarchy);
            // Let go by its motion, a houseguest carves the floor it stands on; walking, it does not.
            var carving = guest.GetComponent<NavMeshObstacle>();
            bool carved = carving != null && carving.enabled;
            if (carved) carving.enabled = false;
            var agent = player.Agent;
            bool agentPlaces = agent.updatePosition;
            agent.ResetPath();
            agent.updatePosition = false;
            try
            {
                RswStand(guest.transform, new Vector3(-4.2f, 0f, -15f));
                RswStand(player.transform, new Vector3(-7.5f, 0f, -15f));
                Assert.That(director.WalkerRoomOf(guest.transform), Is.EqualTo("Nomination"));
                Assert.That(director.WalkerRoomOf(player.transform), Is.EqualTo("HoH"));
                Assert.That(RswCanTalk(guest), Is.False, "Three metres off, in the next room: too far to talk.");
                Assert.That(RswApproach(guest, out var spot), Is.True, "There is somewhere to walk to.");
                Assert.That(HouseWhoIsWhere.SurelyIn(scope, RoomLayers.IndexOf("Nomination"), spot), Is.True,
                    "The walk ends on the nomination room's floor, not back through the doorway in the suite: " + spot.ToString("F2") + ".");
                RswStand(player.transform, spot);
                Assert.That(director.WalkerRoomOf(player.transform), Is.EqualTo("Nomination"));
                Assert.That(RswCanTalk(guest), Is.True, "Where the walk ends, they talk.");
            }
            finally
            {
                agent.updatePosition = agentPlaces;
                if (carved && carving != null) carving.enabled = true;
            }
        }

        /// <summary>
        /// The free-time board's HERE is the player's floor. With the player in the bedroom: a
        /// houseguest across the wall in the living room - whose nearest marker is the bedroom's - is
        /// not here; one in the bedroom's far corner by the yard - whose nearest marker is the yard's -
        /// is; and one in the middle of the bedroom is, by either rule.
        /// </summary>
        [UnityTest]
        public IEnumerator FreeTime_HereMeansThisFloor()
        {
            director.SuspendNpcAutonomyForDiagnostics();
            yield return OpenFreeTime();
            Assert.That(director.IsFreeTimeBoard, Is.True, "Free time is the board.");
            var state = director.Snapshot;
            var scope = ScopeOf(director);
            ScopeParkHouseguests(scope);
            var plain = new HashSet<string>(state.Active.Where(c => !c.isPlayer && EpisodeHud.RoleFor(state, c.id, out _) == null).Select(c => c.id));
            var guests = SceneComponents<HouseNpc>().Where(npc => npc.gameObject.activeInHierarchy && plain.Contains(npc.Id))
                .OrderBy(npc => npc.Id, StringComparer.Ordinal).ToArray();
            Assert.That(guests.Length, Is.GreaterThanOrEqualTo(3), "Three houseguests with no role this week, whose cards can say HERE.");
            HouseNpc across = guests[0], corner = guests[1], middle = guests[2];
            var markers = RswMarkers();
            var agent = player.Agent;
            bool agentPlaces = agent.updatePosition;
            agent.ResetPath();
            agent.updatePosition = false;
            try
            {
                RswStand(player.transform, new Vector3(-5f, 0f, 1f));
                RswStand(across.transform, new Vector3(-5f, 0f, -1.5f));
                RswStand(corner.transform, new Vector3(-3f, 0f, 9.4f));
                RswStand(middle.transform, new Vector3(-7f, 0f, 4f));
                Assert.That(RswNearestMarker(markers, player.transform.position), Is.EqualTo("Bedroom"));
                Assert.That(RswNearestMarker(markers, across.transform.position), Is.EqualTo("Bedroom"), "Across the wall, the bedroom's marker is the nearest,");
                Assert.That(RswNearestMarker(markers, corner.transform.position), Is.EqualTo("Yard"), "and by the yard, the yard's.");
                // The board drawn again with everyone where they now stand.
                RswRender();
                yield return null;
                Canvas.ForceUpdateCanvases();
                Assert.That(director.IsFreeTimeBoard, Is.True, "Still the board.");
                Assert.That(RswRoleOnCard(state, across), Is.Null, across.DisplayName + " is in the living room, across the wall: not here.");
                Assert.That(RswRoleOnCard(state, corner), Is.EqualTo("HERE"), corner.DisplayName + " is on the bedroom's floor: here.");
                Assert.That(RswRoleOnCard(state, middle), Is.EqualTo("HERE"), middle.DisplayName + " is in the middle of the bedroom: here.");
            }
            finally
            {
                agent.updatePosition = agentPlaces;
                director.ClosePanels();
            }
        }

        /// <summary>
        /// The room icons count by floor: a houseguest on the living room's floor whose nearest marker
        /// is the bedroom's counts in the living room, and one on the kitchen's floor whose nearest
        /// marker is the private room's counts in the kitchen - as the notebook says, and as the icons
        /// on screen show.
        /// </summary>
        [UnityTest]
        public IEnumerator Beacons_CountsAreByFloor()
        {
            director.ClosePanels();
            director.SuspendNpcAutonomyForDiagnostics();
            yield return null;
            var scope = ScopeOf(director);
            ScopeParkHouseguests(scope);
            var guests = SceneComponents<HouseNpc>().Where(npc => npc.gameObject.activeInHierarchy)
                .OrderBy(npc => npc.Id, StringComparer.Ordinal).ToArray();
            Assert.That(guests.Length, Is.GreaterThanOrEqualTo(2));
            var markers = RswMarkers();
            RswStand(guests[0].transform, new Vector3(-5f, 0f, -1.5f));
            RswStand(guests[1].transform, new Vector3(8f, 0f, -1.5f));
            Assert.That(RswNearestMarker(markers, guests[0].transform.position), Is.EqualTo("Bedroom"), "On the living room's floor, by the bedroom's marker;");
            Assert.That(RswNearestMarker(markers, guests[1].transform.position), Is.EqualTo("Private"), "on the kitchen's floor, by the private room's.");
            // The player out of the way, in the games room.
            WarpPlayer(ScopeOpenFloor(scope, RoomLayers.IndexOf("Games")));
            yield return PullBackOverTheHouse();
            // The icons count twice a second, on the real clock.
            float deadline = Time.realtimeSinceStartup + 3f;
            while (Time.realtimeSinceStartup < deadline && (director.HouseguestsIn("Living") != 1 || director.HouseguestsIn("Kitchen") != 1)) yield return null;
            yield return ForSeconds(.6f);

            Assert.That(director.HouseguestsIn("Living"), Is.EqualTo(1), guests[0].DisplayName + " counts in the living room, where they stand,");
            Assert.That(director.HouseguestsIn("Bedroom"), Is.EqualTo(0), "not in the bedroom, whose marker is nearer.");
            Assert.That(director.HouseguestsIn("Kitchen"), Is.EqualTo(1), guests[1].DisplayName + " counts in the kitchen,");
            Assert.That(director.HouseguestsIn("Private"), Is.EqualTo(0), "not in the private room.");
            Assert.That(director.HouseguestsIn("Yard"), Is.EqualTo(guests.Length - 2), "The rest wait in the yard.");
            foreach (var room in director.WhoIsWhere())
                Assert.That(director.HouseguestsIn(room.Name), Is.EqualTo(room.Occupants.Count(person => !person.IsPlayer)),
                    room.Name + ": the icons count what the notebook says.");
            var beacons = director.TravelBeacons;
            Assert.That(beacons, Is.Not.Null, "The icons were built.");
            int shown = 0;
            foreach (var room in new[] { "Living", "Bedroom", "Kitchen", "Private" })
            {
                int count = beacons.ShownCount(room);
                if (count < 0) continue;
                shown++;
                Assert.That(count, Is.EqualTo(director.HouseguestsIn(room)), room + "'s icon shows its count.");
            }
            Assert.That(shown, Is.GreaterThan(0), "Some of the four rooms' icons are on screen to read.");
        }

        /// <summary>
        /// The live feed's rooms are the floors its caption counts: the player's room is the floor they
        /// stand on, and a conversation's room the floor under its middle - not the room of the
        /// nearest marker. A metre and a half into the living room by its wall with the bedroom, the
        /// bedroom's marker is the nearer; by the kitchen's wall with the private room, the private
        /// room's.
        /// </summary>
        [UnityTest]
        public IEnumerator LiveFeed_ItsRoomIsTheFloorItCounts()
        {
            director.ClosePanels();
            director.SuspendNpcAutonomyForDiagnostics();
            yield return null;
            ScopeOf(director);
            var markers = RswMarkers();
            var feedRoom = typeof(EpisodeDirector).GetMethod("FloorMarker", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(feedRoom, Is.Not.Null, "The feed finds its room in one place.");
            var agent = player.Agent;
            bool agentPlaces = agent.updatePosition;
            agent.ResetPath();
            agent.updatePosition = false;
            try
            {
                RswStand(player.transform, new Vector3(-5f, 0f, -1.5f));
                Assert.That(RswNearestMarker(markers, player.transform.position), Is.EqualTo("Bedroom"));
                var mine = (HouseRoomMarker)feedRoom.Invoke(director, new object[] { markers, player.transform.position, player.transform });
                Assert.That(mine.RoomName, Is.EqualTo("Living"), "The player's room is the living room they stand in.");
                var middle = new Vector3(8f, 0f, -1.5f);
                Assert.That(RswNearestMarker(markers, middle), Is.EqualTo("Private"));
                var theirs = (HouseRoomMarker)feedRoom.Invoke(director, new object[] { markers, middle, null });
                Assert.That(theirs.RoomName, Is.EqualTo("Kitchen"), "A conversation in the kitchen is in the kitchen.");
            }
            finally { agent.updatePosition = agentPlaces; }
        }

        // ------------------------------------------------------------------ RSW's helpers

        /// <summary>A body stood at a point: the houseguests are let go by their motion, and the player's agent no longer places it.</summary>
        private static void RswStand(Transform body, Vector3 at)
        {
            body.position = at;
            Physics.SyncTransforms();
        }

        private static HouseRoomMarker[] RswMarkers() => SceneComponents<HouseRoomMarker>()
            .Where(marker => !string.IsNullOrEmpty(marker.RoomName))
            .OrderBy(marker => marker.RoomName, StringComparer.Ordinal).ToArray();

        /// <summary>The room whose marker is nearest a point: who-is-where's rule before the floors.</summary>
        private static string RswNearestMarker(HouseRoomMarker[] markers, Vector3 at)
        {
            string best = null;
            float nearest = float.MaxValue;
            foreach (var marker in markers)
            {
                float distance = (marker.transform.position - at).sqrMagnitude;
                if (distance >= nearest) continue;
                nearest = distance;
                best = marker.RoomName;
            }
            return best;
        }

        /// <summary>Each person's room in a reading of who is where.</summary>
        private static Dictionary<string, string> RswRoomsOf(IList<HouseMap.Room> rooms)
        {
            var at = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var room in rooms)
                if (room.Occupants != null)
                    foreach (var person in room.Occupants)
                        if (!string.IsNullOrEmpty(person.Id)) at[person.Id] = room.Name;
            return at;
        }

        /// <summary>A floor's half-metre grid, a quarter-metre in from its edges, column by column and back.</summary>
        private static List<Vector3> RswFloorGrid(Rect box)
        {
            var points = new List<Vector3>();
            int column = 0;
            for (float x = box.xMin + .25f; x < box.xMax; x += .5f, column++)
            {
                var row = new List<Vector3>();
                for (float z = box.yMin + .25f; z < box.yMax; z += .5f) row.Add(new Vector3(x, 0f, z));
                if (column % 2 == 1) row.Reverse();
                points.AddRange(row);
            }
            return points;
        }

        /// <summary>Whether a solid other than a body or a floor stands across the floor between two bodies, at a height over their feet.</summary>
        private static bool RswSolidAcross(Transform one, Transform two, float height)
        {
            Physics.SyncTransforms();
            var from = one.position + Vector3.up * height;
            var to = two.position + Vector3.up * height;
            foreach (var hit in Physics.RaycastAll(from, (to - from).normalized, (to - from).magnitude, HouseLayers.Sight, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.GetComponentInParent<HouseNpc>() != null || hit.collider.GetComponentInParent<HousePlayerController>() != null) continue;
                if (hit.collider.GetComponent<HouseWalkable>() != null) continue;
                return true;
            }
            return false;
        }

        /// <summary>Talk's own test, as the E key, a click and a walk ask it.</summary>
        private bool RswCanTalk(HouseNpc npc)
        {
            var method = typeof(EpisodeDirector).GetMethod("CanTalk", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(method, Is.Not.Null, "Whether the player can talk to someone is asked in one place.");
            return (bool)method.Invoke(director, new object[] { npc });
        }

        /// <summary>Where a walk over to someone ends, as a click on them chooses it.</summary>
        private bool RswApproach(HouseNpc npc, out Vector3 spot)
        {
            var method = typeof(EpisodeDirector).GetMethod("TryApproach", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(method, Is.Not.Null, "The walk over is aimed in one place.");
            var arguments = new object[] { npc, null };
            bool found = (bool)method.Invoke(director, arguments);
            spot = (Vector3)arguments[1];
            return found;
        }

        /// <summary>What the walk-in watch finds, before the engine is asked: the room and the pair.</summary>
        private bool RswWalkIn(out string room, out string first, out string second)
        {
            var method = typeof(EpisodeDirector).GetMethod("TryWalkInHere", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(method, Is.Not.Null, "The walk-in watch finds its pair in one place.");
            var arguments = new object[] { director.Snapshot, null, null, null };
            bool found = (bool)method.Invoke(director, arguments);
            room = (string)arguments[1];
            first = (string)arguments[2];
            second = (string)arguments[3];
            return found;
        }

        /// <summary>Draws the HUD again, as a commit or a page turn does.</summary>
        private void RswRender()
        {
            var render = typeof(EpisodeDirector).GetMethod("Render", BindingFlags.NonPublic | BindingFlags.Instance, null, Type.EmptyTypes, null);
            Assert.That(render, Is.Not.Null);
            render.Invoke(director, null);
        }

        /// <summary>The week's word on a houseguest's card on the free-time board - "HERE", a role, or null for none.</summary>
        private string RswRoleOnCard(EpisodeState state, HouseNpc npc)
        {
            var cards = ActiveRect(EpisodeHud.HouseCardsName);
            Assert.That(cards, Is.Not.Null, "The board's house cards are on screen.");
            string name = "Houseguest · " + state.Find(npc.Id).name;
            var card = cards.GetComponentsInChildren<RectTransform>().LastOrDefault(rect => rect.name == name && rect.gameObject.activeInHierarchy);
            Assert.That(card, Is.Not.Null, npc.DisplayName + " has a card on the board.");
            var role = card.Find("Photo/Role");
            return role != null ? role.GetComponentInChildren<TMP_Text>().text : null;
        }
    }
}
