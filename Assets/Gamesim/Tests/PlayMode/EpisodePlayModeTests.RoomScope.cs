using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Episode;
using Gamesim.House;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// The room scope's first slice (ROOM-SCOPE-PLAN RS1): the camera rig reports the rooms every move
    /// is about, and the director's scope reads them. Nothing these tests move is drawn differently - the
    /// sources of their views (free play, following, a test's own shots, the activity warp) are not live
    /// before RS6b and RS5, RS6a having turned on only the ceremonies' and the competitions', so the scope
    /// says the whole house unless a test holds it on - so they read what is reported, frame by frame, as
    /// the bodies cross the house's doorways.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        /// <summary>A straight walk through one doorway: a metre inside one floor to a metre inside the next.</summary>
        private struct ScopeCrossing
        {
            public int From, To;
            public Vector3 Start, End;
            /// <summary>Where the walk crosses the wall line, and the way across it, from the first floor to the second.</summary>
            public Vector3 Wall, Across;
            public float Width;
            /// <summary>The open run along the wall line.</summary>
            public float Low, High;
            public override string ToString() => RoomLayers.RoomIds[From] + " to " + RoomLayers.RoomIds[To] + " at " + Start.ToString("F2");
        }

        /// <summary>
        /// The player walks through every doorway of the house, and the viewed room changes once at
        /// each: when the feet are three tenths of a metre past the wall line, never before. The two
        /// corner doorways - the yard's and the nomination room's, which open onto the line where two
        /// floors meet - are walked through their middle as well as through each floor's half.
        /// </summary>
        [UnityTest]
        public IEnumerator RoomScope_TheViewedRoomFollowsThePlayerThroughEachDoorwayOnce()
        {
            director.ClosePanels();
            director.SuspendNpcAutonomyForDiagnostics();
            yield return null;
            var scope = ScopeOf(director);
            var crossings = ScopeDoorways(scope);
            var corners = ScopeCornerDoorways(scope, crossings);
            Assert.That(corners.Count, Is.EqualTo(4), "The yard's doorway and the nomination room's, each walked both ways through its middle.");
            crossings.AddRange(corners);
            ScopeParkHouseguests(scope);
            var owner = new object();
            Assert.That(director.ViewedRooms.IsWholeHouse, Is.True, "Free play is not live before RS5: the scope draws the whole house.");
            // The walker rule reads only the feet, so the player is carried through each doorway by
            // its transform rather than warped along the NavMesh: the bake closes the kitchen's door
            // to the private room behind the kitchen run, and its half-metre erosion keeps the
            // narrow doorways' middles off the mesh. The agent takes the player back at the end.
            var agent = player.Agent;
            bool agentPlaces = agent.updatePosition;
            agent.ResetPath();
            agent.updatePosition = false;
            director.HoldRoomScope(owner);
            var failures = new List<string>();
            try
            {
                foreach (var crossing in crossings)
                {
                    // Watched the way a floor click watches: the player is the camera's subject.
                    ScopePlace(player.transform, crossing.Start);
                    cameraRig.FocusSubject(player.transform, false);
                    yield return null;
                    yield return ScopeWalk(player.transform, crossing, failures);
                }
            }
            finally
            {
                director.Release(owner);
                agent.updatePosition = agentPlaces;
            }
            Assert.That(failures, Is.Empty, string.Join("; ", failures));
            Assert.That(director.ViewedRooms.IsWholeHouse, Is.True, "Released, the whole house again.");
        }

        /// <summary>
        /// A player shuffling in a doorway keeps the room they were in: fifteen centimetres either side
        /// of the wall line, again and again, and the view stays; half a metre in, it changes, once.
        /// </summary>
        [UnityTest]
        public IEnumerator RoomScope_ADoorwayShuffleKeepsTheRoom()
        {
            director.ClosePanels();
            director.SuspendNpcAutonomyForDiagnostics();
            yield return null;
            var scope = ScopeOf(director);
            ScopeParkHouseguests(scope);
            // The Living-to-Bedroom doorway, named: six doorways are 2.8 m wide to float rounding, and the
            // runner-up by width, Kitchen to Private, is the one the baked NavMesh closes.
            int living = RoomLayers.IndexOf("Living"), bedroom = RoomLayers.IndexOf("Bedroom");
            var door = ScopeDoorways(scope).First(c => c.From == living && c.To == bedroom);
            var across = (door.End - door.Start).normalized;
            var line = (door.Start + door.End) * .5f;
            // At the walkable surface's own height, so a ten-centimetre reach only ever slides along it.
            Assert.That(NavMesh.SamplePosition(line, out var surface, 1f, player.Agent.areaMask), Is.True, "The doorway " + door + " is walkable.");
            line.y = surface.position.y;
            var owner = new object();
            director.HoldRoomScope(owner);
            try
            {
                ScopePlace(player.transform, door.Start);
                cameraRig.FocusSubject(player.transform, false);
                yield return null;
                var from = RoomSet.Of(door.From);
                var to = RoomSet.Of(door.To);
                Assert.That(cameraRig.Rooms, Is.EqualTo(from), "A metre inside, the " + RoomLayers.RoomIds[door.From] + ".");
                for (int i = 0; i < 24; i++)
                {
                    float offset = (i % 2 == 0 ? .15f : -.15f) * (i % 3 == 0 ? .5f : 1f);
                    ScopePlace(player.transform, line + across * offset, .1f);
                    yield return null;
                    Assert.That(cameraRig.Rooms, Is.EqualTo(from), "Shuffling " + ScopeDepthText(scope, door, player.transform.position)
                        + " keeps the room.");
                    Assert.That(director.ViewedRooms, Is.EqualTo(from), "and the scope's view with it");
                }
                ScopePlace(player.transform, line + across * .5f, .1f);
                yield return null;
                Assert.That(cameraRig.Rooms, Is.EqualTo(to), "Half a metre into the " + RoomLayers.RoomIds[door.To] + " ("
                    + ScopeDepthText(scope, door, player.transform.position) + ") is in it.");
                for (int i = 0; i < 12; i++)
                {
                    ScopePlace(player.transform, line - across * .15f * (i % 2 == 0 ? 1f : .3f), .1f);
                    yield return null;
                    Assert.That(cameraRig.Rooms, Is.EqualTo(to), "and the shuffle back keeps the new room ("
                        + ScopeDepthText(scope, door, player.transform.position) + ").");
                }
                ScopePlace(player.transform, line - across * .5f, .1f);
                yield return null;
                Assert.That(cameraRig.Rooms, Is.EqualTo(from), "Half a metre back, the first room again.");
            }
            finally { director.Release(owner); }
        }

        /// <summary>
        /// Following a houseguest reports their room, wherever the player stands, and follows them
        /// through a doorway once; let go, the view keeps the room it was watching.
        /// </summary>
        [UnityTest]
        public IEnumerator RoomScope_TheViewedRoomFollowsTheFollowedHouseguest()
        {
            director.ClosePanels();
            // Nobody else moves them, and their own motion lets go of their bodies.
            director.SuspendNpcAutonomyForDiagnostics();
            yield return null;
            var scope = ScopeOf(director);
            ScopeParkHouseguests(scope);
            var crossings = ScopeDoorways(scope);
            var guest = SceneComponents<HouseNpc>().First(npc => npc.isActiveAndEnabled);
            var door = crossings.First();
            // The player somewhere else entirely: neither of the doorway's rooms.
            int elsewhere = Enumerable.Range(0, RoomLayers.Count).First(room => scope.Has(room) && room != door.From && room != door.To);
            ScopePlace(player.transform, ScopeOpenFloor(scope, elsewhere), 2f);
            ScopePlace(guest.transform, door.Start);
            yield return null;
            Assert.That(director.WalkerRoomOf(player.transform), Is.EqualTo(RoomLayers.RoomIds[elsewhere]), "The player is in the " + RoomLayers.RoomIds[elsewhere] + ".");

            var owner = new object();
            director.HoldRoomScope(owner);
            var failures = new List<string>();
            try
            {
                director.FollowHouseguest(guest.Id);
                Assert.That(cameraRig.FocusedSubject, Is.SameAs(guest.transform), "The camera follows " + guest.DisplayName + ".");
                Assert.That(cameraRig.Rooms, Is.EqualTo(RoomSet.Of(door.From)), "Their room, not the player's.");
                Assert.That(director.ViewedRooms, Is.EqualTo(RoomSet.Of(door.From)));
                yield return ScopeWalk(guest.transform, door, failures);
                Assert.That(failures, Is.Empty, string.Join("; ", failures));

                // Let go: the room they were followed into stays the view's, wherever they go next.
                director.FollowHouseguest(guest.Id);
                Assert.That(cameraRig.FocusedSubject, Is.Null, "The same houseguest again lets go.");
                Assert.That(cameraRig.Rooms, Is.EqualTo(RoomSet.Of(door.To)), "Watching the room they were in.");
                ScopePlace(guest.transform, door.Start);
                yield return null;
                Assert.That(cameraRig.Rooms, Is.EqualTo(RoomSet.Of(door.To)), "And no longer following them back.");
            }
            finally { director.Release(owner); }
        }

        /// <summary>
        /// Every kind of camera move reports, in the same call, the rooms it is about: a shot naming
        /// none is the whole house, a shot names its own or takes its focus's, a cut and a follow take
        /// their subject's, a free move the floor under its point or the nearest, and a timed move
        /// both ends until it lands. With none of their sources live (free play's and following's wait for
    /// RS5) the scope still draws the whole house; a
        /// hold switches it on, and a whole-house hold wins.
        /// </summary>
        [UnityTest]
        public IEnumerator RoomScope_EveryRigMoveReportsItsRooms()
        {
            director.ClosePanels();
            director.SuspendNpcAutonomyForDiagnostics();
            yield return null;
            var scope = ScopeOf(director);
            Assert.That(cameraRig.ReducedMotion, Is.False, "Timed moves take their time here: the rig is not under reduced motion.");
            var owner = new object();
            var capture = new object();

            // A free move: the floor under its point.
            cameraRig.MoveTo(ScopeCentre(scope, "Kitchen"), 12f);
            Assert.That(cameraRig.Rooms, Is.EqualTo(RoomSet.Of("Kitchen")), "A free move to the kitchen watches the kitchen.");
            Assert.That(director.ViewedRooms.IsWholeHouse, Is.True, "Free play is not live before RS5: the scope still draws the whole house.");
            director.HoldRoomScope(owner);
            try
            {
                Assert.That(director.ViewedRooms, Is.EqualTo(RoomSet.Of("Kitchen")), "Held on, the scope is the rig's.");
                director.HoldWholeHouse(capture);
                Assert.That(director.ViewedRooms.IsWholeHouse, Is.True, "A whole-house hold wins.");
                director.Release(capture);
                Assert.That(director.ViewedRooms, Is.EqualTo(RoomSet.Of("Kitchen")), "Released, the scope again.");

                // Off every floor: the nearest one.
                var games = scope.Box(RoomLayers.IndexOf("Games"));
                cameraRig.MoveTo(new Vector3(games.xMax + 1.5f, 0f, games.center.y), 12f);
                Assert.That(cameraRig.Rooms, Is.EqualTo(RoomSet.Of("Games")), "Past the games room's wall, its floor is the nearest.");

                // Shots: one naming nothing, one naming its rooms, one taking its focus's.
                cameraRig.MoveTo(new HouseCameraRig.Shot { Focus = ScopeCentre(scope, "Bedroom"), Distance = 9f, Pitch = 40f, Yaw = 30f });
                Assert.That(cameraRig.Rooms.IsWholeHouse, Is.True, "A shot that names no room is the whole house.");
                cameraRig.MoveTo(new HouseCameraRig.Shot
                {
                    Focus = ScopeCentre(scope, "Bedroom"), Distance = 9f, Pitch = 40f, Yaw = 30f, Rooms = RoomSet.Of("Kitchen", "Yard"),
                });
                Assert.That(cameraRig.Rooms, Is.EqualTo(RoomSet.Of("Kitchen", "Yard")), "A shot that names its rooms is those rooms.");
                cameraRig.MoveTo(new HouseCameraRig.Shot { Focus = ScopeCentre(scope, "Private"), Distance = 6f, Pitch = 30f, Yaw = 0f, RoomsFromFocus = true });
                Assert.That(cameraRig.Rooms, Is.EqualTo(RoomSet.Of("Private")), "A shot from its focus is the floor under it.");
                cameraRig.MoveTo(new HouseCameraRig.Shot
                {
                    Focus = ScopeCentre(scope, "Private"), Distance = 6f, Pitch = 30f, Yaw = 0f, RoomsFromFocus = true, Rooms = RoomSet.Of("Yard"),
                });
                Assert.That(cameraRig.Rooms, Is.EqualTo(RoomSet.Of("Private", "Yard")), "A shot from its focus that names a room as well is both.");
                cameraRig.ReleaseShot(0f);
                Assert.That(cameraRig.Rooms, Is.EqualTo(RoomSet.Of("Games")), "Let go, the view the first shot left.");

                // A cut: the target's room at once, on the call's own frame.
                ScopePlace(player.transform, ScopeOpenFloor(scope, RoomLayers.IndexOf("Bedroom")), 2f);
                cameraRig.CutTo(player.transform);
                Assert.That(cameraRig.Rooms, Is.EqualTo(RoomSet.Of("Bedroom")), "A cut to the player in the bedroom is the bedroom, at once.");
                // A follow: their room, as the scope walks them.
                var guest = SceneComponents<HouseNpc>().First(npc => npc.isActiveAndEnabled);
                cameraRig.FocusSubject(guest.transform);
                string theirs = director.WalkerRoomOf(guest.transform);
                Assert.That(theirs, Is.Not.Null);
                Assert.That(cameraRig.Rooms, Is.EqualTo(RoomSet.Of(theirs)), guest.DisplayName + " is followed in the " + theirs + ".");
                cameraRig.ClearSubject();
                Assert.That(cameraRig.Rooms, Is.EqualTo(RoomSet.Of(theirs)), "Let go, the view keeps watching that room.");

                // Free play is the player's room, tracked. A floor click follows the player and a pan
                // lets them go: still free play, so the view goes where they walk next.
                cameraRig.FocusSubject(player.transform, false);
                Assert.That(cameraRig.Rooms, Is.EqualTo(RoomSet.Of("Bedroom")), "A floor click: the player's room.");
                cameraRig.ClearSubject();
                ScopePlace(player.transform, ScopeOpenFloor(scope, RoomLayers.IndexOf("Living")), 2f);
                Assert.That(cameraRig.Rooms, Is.EqualTo(RoomSet.Of("Living")), "Let go by a pan, the view is still the player's room: the living room now.");
                // A shot taken in free play, let go: the player's room as it is then, not as it was.
                cameraRig.MoveTo(new HouseCameraRig.Shot { Focus = ScopeCentre(scope, "Yard"), Distance = 12f, Pitch = 45f, Yaw = 0f, Rooms = RoomSet.Of("Yard") });
                Assert.That(cameraRig.Rooms, Is.EqualTo(RoomSet.Of("Yard")));
                ScopePlace(player.transform, ScopeOpenFloor(scope, RoomLayers.IndexOf("Kitchen")), 2f);
                cameraRig.ReleaseShot(0f);
                Assert.That(cameraRig.Rooms, Is.EqualTo(RoomSet.Of("Kitchen")), "Let go, free play again: the kitchen the player has since moved to.");

                // The overview is the whole house by decision (D1), held scope or not, even with the
                // rig following one body over its shot. Let go with that body still followed, the
                // camera goes on following them, and the rooms are theirs.
                ScopePlace(guest.transform, ScopeOpenFloor(scope, RoomLayers.IndexOf("Games")), 2f);
                Assert.That(director.WalkerRoomOf(guest.transform), Is.EqualTo("Games"), guest.DisplayName + " is in the games room.");
                Assert.That(director.ShowOverview(), Is.True, "The overview opens.");
                cameraRig.FocusSubject(guest.transform, false);
                Assert.That(director.CurrentRoomScopeSource, Is.EqualTo(EpisodeDirector.RoomScopeSource.Overview));
                Assert.That(cameraRig.Rooms, Is.EqualTo(RoomSet.Of("Games")), "The rig reports the one it follows,");
                Assert.That(director.ViewedRooms.IsWholeHouse, Is.True, "but the overview draws the whole house, held scope or not.");
                director.EndOverview();
                Assert.That(cameraRig.FocusedSubject, Is.SameAs(guest.transform));
                Assert.That(cameraRig.Rooms, Is.EqualTo(RoomSet.Of("Games")), "The overview let go with " + guest.DisplayName
                    + " followed: their room, not the kitchen the view was on before it.");

                // A timed move: both ends until it lands, then the end's.
                cameraRig.MoveTo(ScopeCentre(scope, "Living"), 12f);
                yield return null;
                cameraRig.MoveTo(ScopeCentre(scope, "Yard"), 14f, 1.2f);
                Assert.That(cameraRig.IsTravelling, Is.True, "A timed move is in flight.");
                Assert.That(cameraRig.Rooms, Is.EqualTo(RoomSet.Of("Living", "Yard")), "In flight, the room it leaves and the room it goes to.");
                yield return ScopeWaitUntilLanded();
                Assert.That(cameraRig.Rooms, Is.EqualTo(RoomSet.Of("Yard")), "Landed, the yard alone.");
                // A timed shot the same way.
                cameraRig.MoveTo(new HouseCameraRig.Shot
                {
                    Focus = ScopeCentre(scope, "Nomination"), Distance = 11f, Pitch = 45f, Yaw = 0f, Seconds = 1.2f, Rooms = RoomSet.Of("Nomination"),
                });
                Assert.That(cameraRig.IsTravelling, Is.True);
                Assert.That(cameraRig.Rooms, Is.EqualTo(RoomSet.Of("Yard", "Nomination")), "A timed shot draws both ends too.");
                yield return ScopeWaitUntilLanded();
                Assert.That(cameraRig.Rooms, Is.EqualTo(RoomSet.Of("Nomination")));
                Assert.That(director.ViewedRooms, Is.EqualTo(RoomSet.Of("Nomination")), "and the held scope with it");
            }
            finally { director.Release(owner); director.Release(capture); }
            Assert.That(director.ViewedRooms.IsWholeHouse, Is.True, "Released, free play's view is the whole house again.");
            // And nothing is drawn differently with no live source the view's: the camera draws every room layer,
            // and what stands on one is the house's dressing, each piece in its room by the rule (RS2a,
            // which turned RS1's "nothing stands on one" over), with no body and no collider among it.
            Assert.That(cameraRig.ViewCamera.cullingMask & RoomLayers.Mask, Is.EqualTo(RoomLayers.Mask), "The camera draws every room layer.");
            var dressing = new List<string>();
            Assert.That(Rs2aCheckTheHouse(scope, dressing).Sum(), Is.GreaterThan(0), "The house's dressing stands on its rooms' layers.");
            Assert.That(dressing, Is.Empty, string.Join("; ", dressing.Take(20)));
            Assert.That(SceneComponents<Transform>().Where(t => RoomLayers.IsRoomLayer(t.gameObject.layer) && HouseRoomScope.IsBody(t)).Select(t => t.name).ToArray(),
                Is.Empty, "No body stands on a room layer.");
            Assert.That(SceneComponents<Collider>().Where(c => RoomLayers.IsRoomLayer(c.gameObject.layer)).Select(c => c.name).ToArray(), Is.Empty,
                "and no collider.");
        }

        /// <summary>
        /// An activity across the house warps the player behind the blink, and the view reports the
        /// activity's room on the blink's own frame, not a frame later.
        /// </summary>
        [UnityTest]
        public IEnumerator RoomScope_AnActivityWarpReportsItsRoomOnTheBlinksFrame()
        {
            director.ClosePanels();
            yield return null;
            var scope = ScopeOf(director);
            var pool = PlacesFor(HouseFurnitureActivity.Swim).Single();
            int poolRoom = scope.RoomUnder(pool.Approach);
            Assert.That(poolRoom, Is.GreaterThanOrEqualTo(0), "The pool stands over a floor.");
            WarpPlayer(FarthestRoomFrom(pool.Approach));
            cameraRig.FocusSubject(player.transform, false);
            yield return null;
            Assert.That(cameraRig.Rooms, Is.Not.EqualTo(RoomSet.Of(poolRoom)), "The player starts in another room.");
            var owner = new object();
            director.HoldRoomScope(owner);
            try
            {
                director.StartActivityInHouse(pool, HouseFurnitureActivity.Swim);
                Assert.That(director.PlayerActivity, Is.Not.Null, director.StatusMessage);
                Assert.That(director.IsTravelDipShowing, Is.True, "A trip across the house is a warp behind the blink.");
                Assert.That(cameraRig.Rooms, Is.EqualTo(RoomSet.Of(poolRoom)), "The blink's frame reports the pool's room.");
                Assert.That(director.ViewedRooms, Is.EqualTo(RoomSet.Of(poolRoom)), "and so does the held scope");
                // The blink's first drawn frame (it clears on the real clock, so a long frame may end it).
                yield return null;
                Assert.That(cameraRig.Rooms, Is.EqualTo(RoomSet.Of(poolRoom)), "The next frame, the pool's room still.");
            }
            finally { director.Release(owner); }
            Assert.That(director.ViewedRooms.IsWholeHouse, Is.True, "Released, the whole house: the activity warp's source is not live before RS6b.");
        }

        private static HouseRoomScope ScopeOf(EpisodeDirector episode)
        {
            var scope = episode.RoomScope;
            Assert.That(scope, Is.Not.Null, "The director built the house's room scope.");
            Assert.That(scope.Rooms.IsWholeHouse, Is.True, "The episode house has all eight rooms.");
            return scope;
        }

        /// <summary>
        /// The house's doorways, from its walls: along each edge two floors share, a ray at half a metre
        /// across the line is open where no wall stands; open runs of 0.9 m or more are doorways. Each
        /// is walked from a metre inside one floor to a metre inside the other, through its middle.
        /// </summary>
        private List<ScopeCrossing> ScopeDoorways(HouseRoomScope scope)
        {
            Physics.SyncTransforms();
            var found = new List<ScopeCrossing>();
            for (int a = 0; a < RoomLayers.Count; a++)
                for (int b = a + 1; b < RoomLayers.Count; b++)
                {
                    if (!scope.Has(a) || !scope.Has(b)) continue;
                    var one = scope.Box(a);
                    var two = scope.Box(b);
                    // The shared edge: a line, which axis it runs along, and from one floor into the other.
                    float zLow = Mathf.Max(one.yMin, two.yMin), zHigh = Mathf.Min(one.yMax, two.yMax);
                    float xLow = Mathf.Max(one.xMin, two.xMin), xHigh = Mathf.Min(one.xMax, two.xMax);
                    Vector3 normal; float line, low, high; bool alongZ;
                    if (zHigh - zLow >= .5f && Mathf.Abs(one.xMax - two.xMin) < .01f) { normal = Vector3.right; line = one.xMax; low = zLow; high = zHigh; alongZ = true; }
                    else if (zHigh - zLow >= .5f && Mathf.Abs(one.xMin - two.xMax) < .01f) { normal = Vector3.left; line = one.xMin; low = zLow; high = zHigh; alongZ = true; }
                    else if (xHigh - xLow >= .5f && Mathf.Abs(one.yMax - two.yMin) < .01f) { normal = Vector3.forward; line = one.yMax; low = xLow; high = xHigh; alongZ = false; }
                    else if (xHigh - xLow >= .5f && Mathf.Abs(one.yMin - two.yMax) < .01f) { normal = Vector3.back; line = one.yMin; low = xLow; high = xHigh; alongZ = false; }
                    else continue;
                    float runStart = float.NaN, runEnd = float.NaN;
                    for (float s = low + .025f; s <= high + .001f; s += .05f)
                    {
                        bool last = s + .05f > high + .001f;
                        var onLine = alongZ ? new Vector3(line, .5f, s) : new Vector3(s, .5f, line);
                        bool open = !ScopeWallAcross(onLine - normal * .6f, onLine + normal * .6f);
                        if (open) { if (float.IsNaN(runStart)) runStart = s; runEnd = s; }
                        if ((!open || last) && !float.IsNaN(runStart))
                        {
                            float width = runEnd - runStart + .05f;
                            if (width >= .9f)
                            {
                                float middle = (runStart + runEnd) * .5f;
                                var at = alongZ ? new Vector3(line, 0f, middle) : new Vector3(middle, 0f, line);
                                float runLow = runStart - .025f, runHigh = runEnd + .025f;
                                found.Add(new ScopeCrossing
                                {
                                    From = a, To = b, Start = at - normal, End = at + normal, Wall = at, Across = normal, Width = width, Low = runLow, High = runHigh,
                                });
                                found.Add(new ScopeCrossing
                                {
                                    From = b, To = a, Start = at + normal, End = at - normal, Wall = at, Across = -normal, Width = width, Low = runLow, High = runHigh,
                                });
                            }
                            runStart = runEnd = float.NaN;
                        }
                    }
                }
            // The plan's ten doorways, each found: the house to the yard and the north block to the
            // nomination room stand on two floors' edges each, so they are walked from both.
            var pairs = new HashSet<string>(found.Select(c => RoomLayers.RoomIds[Mathf.Min(c.From, c.To)] + "-" + RoomLayers.RoomIds[Mathf.Max(c.From, c.To)]));
            string Report() => string.Join(", ", found.Where(c => c.From < c.To).Select(c => c + " (" + c.Width.ToString("0.00") + " m)"));
            foreach (var pair in new[] { "Living-Kitchen", "Living-Bedroom", "Kitchen-Private", "Bedroom-Private", "HoH-Nomination", "Nomination-Games" })
                Assert.That(pairs.Contains(pair), Is.True, "No doorway " + pair + " in: " + Report());
            Assert.That(pairs.Contains("Bedroom-Yard") || pairs.Contains("Private-Yard"), Is.True, "No doorway to the yard in: " + Report());
            Assert.That(pairs.Contains("Living-Nomination") || pairs.Contains("Kitchen-Nomination"), Is.True, "No doorway to the nomination room in: " + Report());
            Assert.That(found.Count(c => c.From < c.To && RoomLayers.RoomIds[c.From] == "Living" && RoomLayers.RoomIds[c.To] == "Kitchen"), Is.EqualTo(2),
                "Two doorways between the living room and the kitchen: " + Report());
            Assert.That(found.Count(c => c.From < c.To && RoomLayers.RoomIds[c.From] == "Bedroom" && RoomLayers.RoomIds[c.To] == "Private"), Is.EqualTo(2),
                "Two between the bedroom and the private room: " + Report());
            return found;
        }

        /// <summary>
        /// The corner doorways whole: two halves <see cref="ScopeDoorways"/> found on one wall line,
        /// open into the same room, that meet where the far side's two floors do (the nomination
        /// room's doorway onto the living room and the kitchen, the yard's onto the bedroom and the
        /// private room). Each is walked through its middle, from the shared room to the floor under
        /// the far end and straight back, so the way back starts where the way there ended.
        /// </summary>
        private static List<ScopeCrossing> ScopeCornerDoorways(HouseRoomScope scope, List<ScopeCrossing> found)
        {
            var corners = new List<ScopeCrossing>();
            var halves = found.Where(c => c.From < c.To).ToList();
            for (int i = 0; i < halves.Count; i++)
                for (int j = i + 1; j < halves.Count; j++)
                {
                    var one = halves[i];
                    var two = halves[j];
                    // The runs are measured along the wall line's own axis: z for a wall across x, x for one across z.
                    var along = Mathf.Abs(one.Across.x) > .5f ? Vector3.forward : Vector3.right;
                    // On one wall line, and the same line through it.
                    if (Mathf.Abs(Mathf.Abs(Vector3.Dot(one.Across, two.Across)) - 1f) > .001f) continue;
                    if (Mathf.Abs(Vector3.Dot(two.Wall - one.Wall, one.Across)) > .01f) continue;
                    // One room in common, and the two runs meeting end to end.
                    int shared = one.From == two.From || one.From == two.To ? one.From : one.To == two.From || one.To == two.To ? one.To : -1;
                    if (shared < 0 || (one.From == two.From && one.To == two.To)) continue;
                    if (Mathf.Min(Mathf.Abs(one.High - two.Low), Mathf.Abs(two.High - one.Low)) > .11f) continue;
                    float low = Mathf.Min(one.Low, two.Low), high = Mathf.Max(one.High, two.High);
                    // The wall point at the merged run's middle, and the way from the shared room across.
                    var wall = one.Wall + along * ((low + high) * .5f - Vector3.Dot(one.Wall, along));
                    wall.y = 0f;
                    var across = shared == one.From ? one.Across : -one.Across;
                    var start = wall - across;
                    var end = wall + across;
                    int far = scope.RoomUnder(end);
                    corners.Add(new ScopeCrossing { From = shared, To = far, Start = start, End = end, Wall = wall, Across = across, Width = high - low, Low = low, High = high });
                    corners.Add(new ScopeCrossing { From = far, To = shared, Start = end, End = start, Wall = wall, Across = -across, Width = high - low, Low = low, High = high });
                }
            return corners;
        }

        /// <summary>Whether a wall stands across the segment: a solid the sight queries see, not a body.</summary>
        private static bool ScopeWallAcross(Vector3 from, Vector3 to)
        {
            var direction = to - from;
            foreach (var hit in Physics.RaycastAll(from, direction.normalized, direction.magnitude, HouseLayers.Sight, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.GetComponentInParent<HouseNpc>() != null || hit.collider.GetComponentInParent<HousePlayerController>() != null) continue;
                if (hit.collider.GetComponent<HouseWalkable>() != null) continue;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Walks a body through a doorway a few centimetres a frame and checks, every frame, the view's
        /// room: the first floor until the feet are three tenths of a metre past the wall line (in the
        /// middle of a doorway, three tenths inside the second floor), then the second, and only one
        /// change.
        /// </summary>
        private IEnumerator ScopeWalk(Transform body, ScopeCrossing crossing, List<string> failures)
        {
            var from = RoomSet.Of(crossing.From);
            var to = RoomSet.Of(crossing.To);
            int changes = 0;
            float lastDepth = ScopePast(crossing, body.position);
            var seen = cameraRig.Rooms;
            if (seen != from) { failures.Add(crossing + ": started in " + seen + ", not " + from); yield break; }
            const int Steps = 50;
            for (int step = 1; step <= Steps; step++)
            {
                ScopePlace(body, Vector3.Lerp(crossing.Start, crossing.End, step / (float)Steps), .3f);
                yield return null;
                var feet = body.position;
                float depth = ScopePast(crossing, feet);
                var now = cameraRig.Rooms;
                if (director.ViewedRooms != now) failures.Add(crossing + ": the held scope reads " + director.ViewedRooms + " against the rig's " + now);
                if (now != seen)
                {
                    changes++;
                    if (seen != from || now != to) failures.Add(crossing + ": went from " + seen + " to " + now);
                    if (depth < HouseRoomScope.DoorwayDepth - .001f)
                        failures.Add(crossing + ": changed only " + depth.ToString("0.000") + " m past the wall line into the " + RoomLayers.RoomIds[crossing.To]);
                    if (lastDepth >= HouseRoomScope.DoorwayDepth + .001f)
                        failures.Add(crossing + ": already " + lastDepth.ToString("0.000") + " m past the wall line a step before it changed");
                    seen = now;
                }
                lastDepth = depth;
            }
            if (changes != 1) failures.Add(crossing + ": " + changes + " changes, ending in " + seen);
            if (cameraRig.Rooms != to) failures.Add(crossing + ": ended in " + cameraRig.Rooms);
        }

        /// <summary>How far past the crossing's wall line the feet stand, in plan: negative still on the first side.</summary>
        private static float ScopePast(ScopeCrossing crossing, Vector3 feet)
        {
            var past = feet - crossing.Wall;
            return past.x * crossing.Across.x + past.z * crossing.Across.z;
        }

        /// <summary>How far inside a floor box the feet stand: positive inside, to its nearest edge.</summary>
        private static float ScopeDepth(Rect box, Vector3 feet)
            => Mathf.Min(Mathf.Min(feet.x - box.xMin, box.xMax - feet.x), Mathf.Min(feet.z - box.yMin, box.yMax - feet.z));

        private static string ScopeDepthText(HouseRoomScope scope, ScopeCrossing door, Vector3 feet)
            => ScopeDepth(scope.Box(door.To), feet).ToString("0.00") + " m into the " + RoomLayers.RoomIds[door.To];

        /// <summary>
        /// Puts a body at a point: the player (or a body whose agent places it) by a warp onto the
        /// nearest mesh within <paramref name="reach"/>; a houseguest let go by its motion, or a body
        /// whose agent no longer moves its transform, straight there.
        /// </summary>
        private static void ScopePlace(Transform body, Vector3 at, float reach = .5f)
        {
            var agent = body.GetComponent<NavMeshAgent>();
            if (agent != null && agent.enabled && agent.updatePosition)
            {
                Assert.That(NavMesh.SamplePosition(at, out var hit, reach, agent.areaMask), Is.True, "No floor within " + reach + " m of " + at.ToString("F2") + ".");
                Assert.That(agent.Warp(hit.position), Is.True);
                agent.ResetPath();
            }
            else body.position = at;
            Physics.SyncTransforms();
        }

        /// <summary>A walkable point well inside a room: its box's centre, or the nearest floor to it.</summary>
        private Vector3 ScopeOpenFloor(HouseRoomScope scope, int room)
        {
            var centre = scope.Box(room).center;
            var at = new Vector3(centre.x, 0f, centre.y);
            Assert.That(NavMesh.SamplePosition(at, out var hit, 3f, player.Agent.areaMask), Is.True, "No floor near the " + RoomLayers.RoomIds[room] + "'s middle.");
            return hit.position;
        }

        private static Vector3 ScopeCentre(HouseRoomScope scope, string room)
        {
            var centre = scope.Box(RoomLayers.IndexOf(room)).center;
            return new Vector3(centre.x, 0f, centre.y);
        }

        /// <summary>
        /// Moves every houseguest to the yard's far side, out of the doorways a walk goes through: a
        /// body standing in one carves the floor and turns the walk aside. Their motion has let them
        /// go (autonomy suspended), so they stay where they are put.
        /// </summary>
        private static void ScopeParkHouseguests(HouseRoomScope scope)
        {
            var yard = scope.Box(RoomLayers.IndexOf("Yard"));
            int i = 0;
            foreach (var npc in SceneComponents<HouseNpc>().Where(npc => npc.gameObject.activeInHierarchy))
                ScopePlace(npc.transform, new Vector3(yard.xMin + 2f + 1.6f * i++, 0f, yard.yMax - 1.5f), 2f);
        }

        /// <summary>Waits, on the real clock, for a timed move to land: the rig eases on unscaled time.</summary>
        private IEnumerator ScopeWaitUntilLanded()
        {
            float deadline = Time.realtimeSinceStartup + 6f;
            while (cameraRig.IsTravelling && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(cameraRig.IsTravelling, Is.False, "The timed move landed.");
        }
    }
}
