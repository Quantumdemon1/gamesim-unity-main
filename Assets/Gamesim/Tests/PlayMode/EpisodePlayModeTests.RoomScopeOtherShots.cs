using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Gamesim.Episode;
using Gamesim.House;
using Gamesim.Persistence;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// The other shots, live (ROOM-SCOPE-PLAN RS6b, D6): with nothing held, the conversation's two-shot draws
    /// the pair's rooms, the diary's chair shot the private room, a house event's shot the rooms of the
    /// people it frames, the activity warp the activity's room from the cut and the blink's first frame, and
    /// a jury or evicted player's watch the watched room - each while its own move holds the camera, so a
    /// shot let go, a conversation under reduced motion and a camera the viewer took back are the view's
    /// own again (free play's or following's, the rig's rooms from RS5). The overview, the opening and
    /// the walk-out name the whole house, held scope or not, and a capture asking for a room draws it from
    /// its first frame. As in RS6a, the rooms are read where they are drawn: the main camera's mask (bits
    /// 20 to 27), the lamps inside its own render, and the bodies.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        /// <summary>
        /// The overview, the opening and the walk-out are the whole house by decision (D1, D6): each names
        /// the whole house in the calls that move the camera, and none has a switch, so even with the scope
        /// held on (as the probe and the captures hold it) the main camera draws every room bit, every lamp
        /// and every body. The staged opening's door reveals and its introductions are the whole house; its
        /// hand-over is free play's, the player's room from the call (the cut D6 asks for, drawn as a room
        /// once free play is live, RS5). The overview's map is the look sheet's "house overview" (its route
        /// opens the director's overview). An unstaged walk-out follows the one leaving and draws the whole
        /// house; let go, it hands the camera back to free play.
        /// </summary>
        [UnityTest]
        public IEnumerator RoomScope_TheOverviewTheOpeningAndTheWalkOutDrawTheWholeHouse()
        {
            var opening = director.Opening;
            string firstGuest = director.Snapshot.Active.First(person => !person.isPlayer).id;
            var owner = new object();

            // The opening, staged: its door reveals and introductions.
            director.PlayOpeningForVerification(stage: true, holdHeadless: true, armSeconds: 0f);
            yield return WaitFor(() => director.IsOpeningStaged, 40f, "The house is placed behind the front door.");
            var homes = LampScopeHomes(ScopeOf(director));
            director.HoldRoomScope(owner);
            try
            {
                yield return WaitFor(() => opening.CurrentGuestId != null, 40f, "The reveals begin.");
                yield return null;
                Rs6bAssertAWholeHouseSource(EpisodeDirector.RoomScopeSource.Opening, homes, true, "A door reveal, the scope held on");
                director.SkipOpening();
                yield return WaitFor(() => opening.IsMeeting, 10f, "Skipping the show stops at the introductions.");
                yield return WaitFor(() => MeetCardIsSettled(opening, firstGuest), 8f, "The first houseguest's card is up and the camera has landed on them.");
                Rs6bAssertAWholeHouseSource(EpisodeDirector.RoomScopeSource.Opening, homes, true, "An introduction, the scope held on");
                opening.SkipIntroductions();
                yield return WaitFor(() => !opening.IsPlaying, 5f, "Skipping the introductions ends the opening.");
                // The hand-over: free play's, the player's room from the call.
                Assert.That(director.CurrentRoomScopeSource, Is.EqualTo(EpisodeDirector.RoomScopeSource.FreePlay), "Handed over, the view is free play's,");
                Assert.That(cameraRig.FocusedSubject, Is.SameAs(player.transform), "following the player,");
                string room = director.WalkerRoomOf(player.transform);
                Assert.That(room, Is.Not.Null, "who stands on a floor.");
                Assert.That(cameraRig.Rooms, Is.EqualTo(RoomSet.Of(room)), "The hand-over names the player's room in the call (D6).");
                Assert.That(director.ViewedRooms, Is.EqualTo(RoomSet.Of(room)), "Held on, the view is the player's room.");
            }
            finally { director.Release(owner); }
            Rs5AssertFreePlaysView("Let go, the opening handed over");
            yield return WaitFor(() => director.NpcAutonomyReady, 5f, "The house binds its people again and carries on.");

            // The overview: the map, held or not.
            director.HoldRoomScope(owner);
            try
            {
                Assert.That(director.ShowOverview(), Is.True, "The overview opens.");
                Assert.That(cameraRig.Rooms.IsWholeHouse, Is.True, "The overview's shot names the whole house in the call.");
                yield return null;
                Rs6bAssertAWholeHouseSource(EpisodeDirector.RoomScopeSource.Overview, homes, true, "The overview, the scope held on");
                director.EndOverview();
                Assert.That(director.CurrentRoomScopeSource, Is.EqualTo(EpisodeDirector.RoomScopeSource.FreePlay), "Left, the view is free play's.");
            }
            finally { director.Release(owner); }
            Assert.That(director.ShowOverview(), Is.True, "The overview opens again, with nothing held.");
            yield return null;
            Rs6bAssertAWholeHouseSource(EpisodeDirector.RoomScopeSource.Overview, homes, true, "The overview, nothing held");
            director.EndOverview();

            // The walk-out, unstaged: the one leaving followed, the whole house drawn.
            director.WalkOutsInBatchRuns = true;
            yield return PlayUntilAHouseguestLeaves();
            string leaving = director.DepartingId;
            SceneComponents<VoteReveal>().Single().Cancel();
            yield return Frames(3);
            Assert.That(director.WalkingOutId, Is.EqualTo(leaving), "Once the cards are done, the evicted walks out.");
            var body = SceneComponents<HouseNpc>().Single(npc => npc.Id == leaving).transform;
            director.HoldRoomScope(owner);
            try
            {
                yield return WaitFor(() => cameraRig.FocusedSubject == body, 15f, "The walk-out follows the one leaving once they take their navigation back.");
                yield return null;
                Rs6bAssertAWholeHouseSource(EpisodeDirector.RoomScopeSource.WalkOut, homes, false, "The walk-out, the scope held on");
                Rs6aAssertDrawn(body, "The one leaving");
                director.SkipWalkOut();
                Assert.That(director.WalkingOutId, Is.Null, "A press lets them go,");
                Assert.That(director.CurrentRoomScopeSource, Is.EqualTo(EpisodeDirector.RoomScopeSource.FreePlay), "and hands the camera back to free play.");
            }
            finally { director.Release(owner); }
            yield return null;
            Rs5AssertFreePlaysView("Free play after the walk-out, nothing held");
        }

        /// <summary>
        /// A capture of another room draws that room on its first frame (R5): a set capture's move - 0.01 s,
        /// naming its room in the call, as <c>CaptureSet</c> takes it - over a view held on the living room
        /// draws the kitchen in a render by hand on the call's own frame (with the living room the 0.01 s
        /// move leaves), and landed - waited for on the real clock, as the rig eases on unscaled time and a
        /// batch frame is under a millisecond - and a frame on for the late pass, the frame <c>CaptureSet</c>
        /// photographs, the kitchen alone: its room bit, its lamps (none) inside the render, a houseguest in it
        /// drawn and one in the games room hidden. With nothing held a test's own shot is free play's, which
        /// from RS5 draws the rooms the shot names: the kitchen.
        /// </summary>
        [UnityTest]
        public IEnumerator RoomScope_ACaptureOfAnotherRoomDrawsThatRoomOnItsFirstFrame()
        {
            director.ClosePanels();
            director.SuspendNpcAutonomyForDiagnostics();
            Rs6bMotion(false);
            yield return null;
            yield return Rs4BodiesBuilt();
            var scope = ScopeOf(director);
            var homes = LampScopeHomes(scope);
            var view = cameraRig.ViewCamera;
            ScopeParkHouseguests(scope);
            var guests = SceneComponents<HouseNpc>().Where(npc => npc.isActiveAndEnabled).ToArray();
            Assert.That(guests.Length, Is.GreaterThanOrEqualTo(2), "Two houseguests to place.");
            HouseNpc inKitchen = guests[0], inGames = guests[1];
            ScopePlace(inKitchen.transform, ScopeOpenFloor(scope, RoomLayers.IndexOf("Kitchen")), 2f);
            ScopePlace(inGames.transform, ScopeOpenFloor(scope, RoomLayers.IndexOf("Games")), 2f);
            ScopePlace(player.transform, ScopeOpenFloor(scope, RoomLayers.IndexOf("Living")), 2f);
            yield return null;
            yield return null;
            Assert.That(director.WalkerRoomOf(inKitchen.transform), Is.EqualTo("Kitchen"));
            Assert.That(director.WalkerRoomOf(inGames.transform), Is.EqualTo("Games"));
            var kitchen = RoomSet.Of("Kitchen");
            var living = RoomSet.Of("Living");
            var capture = new HouseCameraRig.Shot
            {
                Focus = ScopeCentre(scope, "Kitchen") + Vector3.up, Distance = 9f, Pitch = 35f, Yaw = 180f, FieldOfView = 45f, Seconds = .01f, Rooms = kitchen,
            };
            var owner = new object();
            cameraRig.MoveTo(ScopeCentre(scope, "Living"), 12f);
            director.HoldRoomScope(owner);
            try
            {
                yield return null;
                Assert.That(director.ViewedRooms, Is.EqualTo(living), "Held on, free play's view watches the living room.");
                var authored = homes.Select(home => home.Light.enabled).ToArray();
                cameraRig.MoveTo(capture);
                Assert.That(cameraRig.IsTravelling, Is.True, "The capture's move takes its hundredth of a second,");
                Assert.That(cameraRig.Rooms, Is.EqualTo(living.With(kitchen)), "and names the kitchen in the call, with the living room it leaves.");
                var sight = Rs6aRenderNow(homes);
                Assert.That(sight.Viewed, Is.EqualTo(living.With(kitchen)), "A render on the call's own frame draws the kitchen already,");
                Assert.That(sight.RoomBits & kitchen.CullingBits, Is.EqualTo(kitchen.CullingBits), "its room bit in the main camera's mask.");
                Rs6aAssertLamps(sight, homes, authored, living.With(kitchen), "The capture's first frame");
                Rs6aAssertDrawn(inKitchen.transform, inKitchen.DisplayName + ", in the kitchen, on the capture's first frame,");
                // Landed, on the clock: the rig eases on unscaled time, which no frame count pins.
                yield return ScopeWaitUntilLanded();
                yield return null;
                Assert.That(cameraRig.IsTravelling, Is.False, "Landed, a frame on for the late pass:");
                Assert.That(director.ViewedRooms, Is.EqualTo(kitchen), "the kitchen alone,");
                Assert.That(view.cullingMask & RoomLayers.Mask, Is.EqualTo(kitchen.CullingBits), "its room bit alone in the main camera's mask.");
                sight = Rs6aRenderNow(homes);
                Assert.That(sight.Viewed, Is.EqualTo(kitchen), "The captured frame draws the kitchen alone,");
                Rs6aAssertLamps(sight, homes, authored, kitchen, "The captured frame");
                Rs6aAssertDrawn(inKitchen.transform, inKitchen.DisplayName + ", in the kitchen,");
                Rs6aAssertHidden(inGames.transform, inGames.DisplayName + ", in the games room,");
            }
            finally
            {
                director.Release(owner);
                cameraRig.ReleaseShot(0f);
            }
            // With nothing held, a test's own shot is free play's view: from RS5, the rooms it names.
            cameraRig.MoveTo(capture);
            Assert.That(director.CurrentRoomScopeSource, Is.EqualTo(EpisodeDirector.RoomScopeSource.FreePlay), "A test's own shot is free play's view,");
            yield return ScopeWaitUntilLanded();
            yield return null;
            Rs5AssertFreePlaysView("A capture's shot with nothing held");
            Assert.That(director.ViewedRooms, Is.EqualTo(RoomSet.Of("Kitchen")), "which draws the kitchen it names.");
            cameraRig.ReleaseShot(0f);
        }

        /// <summary>
        /// A house event's shot draws its people's rooms: the two it is about stand either side of the living
        /// room's doorway into the bedroom, so the shot, over the doorway, names both their rooms - the
        /// people's, not the floor under its point. Opened from free play its 0.8 s move draws free play's
        /// room on its way with theirs; landed, the two rooms alone - their bits, their lamps inside the render,
        /// both people drawn and a houseguest in a room far from them hidden - for as long as the shot holds;
        /// the panel closed, free play's view in the same frame.
        /// </summary>
        [UnityTest]
        public IEnumerator RoomScope_AHouseEventShotDrawsItsPeoplesRooms()
        {
            var fixture = ContentCatalog.Create(7);
            var ids = fixture.contestants.Where(c => c.id != fixture.playerId).Take(3).Select(c => c.id).ToArray();
            fixture.houseEvents.Clear();
            fixture.houseEvents.Add(new HouseEventState
            {
                id = "room-scope-event", kind = HouseEventKind.House, week = fixture.week, title = "A disagreement at dinner",
                narrative = "Two houseguests disagree about the shared kitchen.", involvedIds = ids.Take(2).ToList(),
                choices = new List<HouseEventChoice> { new HouseEventChoice { label = "Listen without taking sides", risk = HouseEventRisk.Low, description = "Hear both houseguests out." } },
            });
            Assert.That(EpisodeValidation.TryValidate(fixture, out var reason), Is.True, reason);
            new EpisodeSaveStore(director.SavePath).Save(fixture);
            yield return ReloadEpisode();
            director.ClosePanels();
            director.SuspendNpcAutonomyForDiagnostics();
            Rs6bMotion(false);
            yield return null;
            yield return Rs4BodiesBuilt();
            var scope = ScopeOf(director);
            var homes = LampScopeHomes(scope);
            var view = cameraRig.ViewCamera;
            ScopeParkHouseguests(scope);
            int livingIndex = RoomLayers.IndexOf("Living"), bedroomIndex = RoomLayers.IndexOf("Bedroom");
            var door = ScopeDoorways(scope).First(c => c.From == livingIndex && c.To == bedroomIndex);
            Transform Body(string id) => SceneComponents<HouseNpc>().Single(npc => npc.Id == id).transform;
            Transform one = Body(ids[0]), two = Body(ids[1]), third = Body(ids[2]);
            ScopePlace(one, door.Start, .5f);
            ScopePlace(two, door.End, .5f);
            WarpPlayer(director.StationPosition);
            // The third well away from the pair and the player, so no doorway band and no prompt draws them.
            int away = Rs4FarRoom(scope, one.position, two.position, player.transform.position);
            ScopePlace(third, ScopeOpenFloor(scope, away), 2f);
            yield return null;
            yield return null;
            // The room view changes rooms at most once per 0.6 s (RS5): the warp's change waited out.
            yield return RoomViewWaitOutTheLastChange();
            Assert.That(director.WalkerRoomOf(one), Is.EqualTo("Living"), one.name + " stands in the living room.");
            Assert.That(director.WalkerRoomOf(two), Is.EqualTo("Bedroom"), two.name + " stands in the bedroom, across the doorway.");
            Assert.That(director.WalkerRoomOf(third), Is.EqualTo(RoomLayers.RoomIds[away]), third.name + " stands in the " + RoomLayers.RoomIds[away] + ".");
            var freeView = RoomSet.Of(director.WalkerRoomOf(player.transform));
            Assert.That(director.ViewedRooms, Is.EqualTo(freeView), "Free play draws the player's room (RS5).");
            var pair = RoomSet.Of("Living", "Bedroom");

            Assert.That(director.TryOpenPhasePanel(), Is.True, "The episode screen opens on the event.");
            yield return null;
            Assert.That(director.IsFramingHouseEvent, Is.True, "The camera takes the event's people, who stand together.");
            Assert.That(director.HouseEventShotHoldsTheCamera, Is.True, "Its shot holds the camera,");
            Assert.That(director.CurrentRoomScopeSource, Is.EqualTo(EpisodeDirector.RoomScopeSource.HouseEvent), "and is the view.");
            Assert.That(cameraRig.IsTravelling, Is.True, "A frame into its 0.8 seconds the move is on its way,");
            Assert.That(director.ViewedRooms, Is.EqualTo(freeView.With(pair)),
                "drawing free play's room with the pair's as it goes: nothing the viewer saw goes before the camera has moved.");
            Assert.That(view.cullingMask & RoomLayers.Mask, Is.EqualTo(freeView.With(pair).CullingBits), "Their bits on the way.");

            yield return ScopeWaitUntilLanded();
            yield return null;
            Assert.That(director.HouseEventShotHoldsTheCamera, Is.True, "Landed, the event's shot holds.");
            Assert.That(cameraRig.Rooms, Is.EqualTo(pair), "Its shot names its people's rooms: the living room and the bedroom.");
            Rs6aAssertTheViewIs(pair, "The house event's shot");
            var authored = homes.Select(home => home.Light.enabled).ToArray();
            var sight = Rs6aRenderNow(homes);
            Assert.That(sight.Viewed, Is.EqualTo(pair));
            Rs6aAssertLamps(sight, homes, authored, pair, "The house event's shot");
            Rs6aAssertDrawn(one, one.name + ", in the event, in the living room,");
            Rs6aAssertDrawn(two, two.name + ", in the event, in the bedroom,");
            Rs6aAssertHidden(third, third.name + ", in the " + RoomLayers.RoomIds[away] + ",");
            var failures = new List<string>();
            Rs6aBodiesFollowTheView(scope, pair, "The house event's shot", failures);
            Assert.That(failures, Is.Empty, string.Join("; ", failures));

            director.ClosePanels();
            Assert.That(director.HouseEventShotHoldsTheCamera, Is.False, "The panel closed lets the shot go,");
            Assert.That(director.CurrentRoomScopeSource, Is.EqualTo(EpisodeDirector.RoomScopeSource.FreePlay), "and the view is free play's in the same frame,");
            Assert.That(director.ViewedRooms, Is.EqualTo(cameraRig.Rooms), "the rooms the rig went back to.");
            yield return null;
            Rs5AssertFreePlaysView("The house event closed");
        }

        /// <summary>
        /// An activity across the house warps the player behind the blink, and the cut and the blink set the
        /// scope at once (D6): with nothing held, the activity's room is the view from the call - the render on
        /// the blink's own frame draws its room bit and its lamps alone - and on the blink's first drawn frame
        /// the player is drawn at the pool and nobody outside it is. A follow of someone else takes the camera
        /// from the warp: following's view, their room (RS5). The activity over, free play's view. Under reduced
        /// motion the warp is a cut with no blink, and its room is the view from the call. An activity reached
        /// on foot - a walk of 20 m or less, no warp - is free play's view, the player's room (RS5). The NPC world stays live (the activity needs its meetings), so before each leg the
        /// houseguests who are free to walk are put in a room away from the pool and the player, and the
        /// messages say when the NPC world, not the room scope, took the camera.
        /// </summary>
        [UnityTest]
        public IEnumerator RoomScope_AnActivityWarpLandsScoped()
        {
            director.ClosePanels();
            Rs6bMotion(false);
            yield return null;
            yield return Rs4BodiesBuilt();
            var scope = ScopeOf(director);
            var homes = LampScopeHomes(scope);
            var view = cameraRig.ViewCamera;
            var pool = PlacesFor(HouseFurnitureActivity.Swim).Single();
            int poolRoom = scope.RoomUnder(pool.Approach);
            Assert.That(poolRoom, Is.GreaterThanOrEqualTo(0), "The pool stands over a floor.");
            var room = RoomSet.Of(poolRoom);
            var far = FarthestRoomFrom(pool.Approach);
            int parking = Rs4FarRoom(scope, pool.Approach, far);
            Assert.That(parking, Is.Not.EqualTo(poolRoom), "The houseguests wait in a room other than the pool's.");
            string Why() => " [source " + director.CurrentRoomScopeSource + ", conversation " + cameraRig.IsConversationFocused
                + ", status: " + director.StatusMessage + "]";
            void NotTheNpcWorld(string how)
            {
                if (cameraRig.IsConversationFocused)
                    Assert.Fail(how + ": the NPC world opened a conversation over the activity - the autonomy flake, not the room scope" + Why());
            }

            // On foot: a walk of under 20 m is no warp, and the view stays free play's.
            Rs6bParkHouseguestsIn(scope, parking);
            WarpPlayer(OnFootFrom(pool.Approach, 2f, 8f));
            cameraRig.FocusSubject(player.transform, false);
            yield return null;
            Assert.That(player.TryMeasureRoute(pool.Approach, out float onFoot), Is.True, "On foot: the pool can be walked to.");
            Assert.That(onFoot, Is.LessThanOrEqualTo(HousePlayerController.WarpRouteMetres), "On foot: the pool is a walk away, not a warp's.");
            director.StartActivityInHouse(pool, HouseFurnitureActivity.Swim);
            NotTheNpcWorld("On foot");
            Assert.That(director.PlayerActivity, Is.EqualTo(HouseFurnitureActivity.Swim), "On foot: " + director.StatusMessage);
            Assert.That(director.IsTravelDipShowing, Is.False, "On foot: a walk of " + onFoot.ToString("F1") + " m is no warp, and no blink.");
            Assert.That(director.ActivityWarpHoldsTheCamera, Is.False, "On foot: no warp holds the camera," + Why());
            Assert.That(director.CurrentRoomScopeSource, Is.EqualTo(EpisodeDirector.RoomScopeSource.FreePlay), "On foot: the view is free play's," + Why());
            Assert.That(director.ViewedRooms, Is.EqualTo(cameraRig.Rooms), "On foot: free play's view, the player's room (RS5) (" + director.ViewedRooms + ")." + Why());
            yield return null;
            NotTheNpcWorld("On foot, a frame on");
            Assert.That(director.ActivityWarpHoldsTheCamera, Is.False, "On foot, a frame on: no warp holds the camera," + Why());
            Rs5AssertFreePlaysView("On foot, a frame on");
            director.FinishPlayerHouseActivity();
            yield return WaitFor(() => director.PlayerActivity == null, 20f, "On foot: the swim ends.");

            foreach (bool reduced in new[] { false, true })
            {
                string how = reduced ? "Under reduced motion" : "With the blink";
                Rs6bMotion(reduced);
                Assert.That(cameraRig.ReducedMotion, Is.EqualTo(reduced), how + ": the rig's reduced motion is as asked.");
                Rs6bParkHouseguestsIn(scope, parking);
                WarpPlayer(far);
                cameraRig.FocusSubject(player.transform, false);
                yield return null;
                NotTheNpcWorld(how + ", before the warp");
                Assert.That(director.WalkerRoomOf(player.transform), Is.Not.EqualTo(RoomLayers.RoomIds[poolRoom]), how + ": the player starts in another room.");
                Assert.That(director.ViewedRooms, Is.EqualTo(RoomSet.Of(director.WalkerRoomOf(player.transform))), how + ": free play draws the player's room (RS5)." + Why());
                var authored = homes.Select(home => home.Light.enabled).ToArray();
                director.StartActivityInHouse(pool, HouseFurnitureActivity.Swim);
                NotTheNpcWorld(how + ", the warp's call");
                Assert.That(director.PlayerActivity, Is.EqualTo(HouseFurnitureActivity.Swim), how + ": " + director.StatusMessage);
                Assert.That(director.IsTravelDipShowing, Is.EqualTo(!reduced), how + ": a trip across the house is a warp" + (reduced ? ", a cut with no blink." : " behind the blink."));
                Assert.That(director.ActivityWarpHoldsTheCamera, Is.True, how + ": the warp holds the camera," + Why());
                Assert.That(director.CurrentRoomScopeSource, Is.EqualTo(EpisodeDirector.RoomScopeSource.Activity), how + ": and is the view," + Why());
                Assert.That(director.ViewedRooms, Is.EqualTo(room), how + ": the pool's room from the call, with nothing held." + Why());
                var sight = Rs6aRenderNow(homes);
                Assert.That(sight.Viewed, Is.EqualTo(room), how + ": the render on the call's own frame draws the pool's room,");
                Assert.That(sight.RoomBits, Is.EqualTo(room.CullingBits), how + ": its room bit alone in the main camera's mask,");
                Rs6aAssertLamps(sight, homes, authored, room, how + ", the warp's frame");
                yield return null;
                NotTheNpcWorld(how + ", the warp's first drawn frame");
                Assert.That(view.cullingMask & RoomLayers.Mask, Is.EqualTo(room.CullingBits), how + ": the late pass keeps the pool's room bit alone." + Why());
                Rs6aAssertDrawn(player.transform, how + ": the player, at the pool and the camera's subject,");
                var failures = new List<string>();
                Rs6aBodiesFollowTheView(scope, room, how + ", the warp's first drawn frame", failures);
                Assert.That(failures, Is.Empty, string.Join("; ", failures) + Why());

                if (!reduced)
                {
                    // A follow takes the camera from the warp: following's view, their room (RS5).
                    var guest = SceneComponents<HouseNpc>().First(npc => npc.isActiveAndEnabled);
                    cameraRig.FocusSubject(guest.transform);
                    try
                    {
                        Assert.That(director.ActivityWarpHoldsTheCamera, Is.False, "A follow takes the camera from the warp:");
                        Assert.That(director.CurrentRoomScopeSource, Is.EqualTo(EpisodeDirector.RoomScopeSource.Following), "the view is following's," + Why());
                        Assert.That(director.ViewedRooms, Is.EqualTo(cameraRig.Rooms), "their room (RS5).");
                        yield return null;
                        NotTheNpcWorld("A follow over the activity");
                        Rs5AssertFreePlaysView("A follow over the activity");
                    }
                    finally { cameraRig.ClearSubject(); }
                }
                director.FinishPlayerHouseActivity();
                yield return WaitFor(() => director.PlayerActivity == null, 20f, how + ": the swim ends.");
                NotTheNpcWorld(how + ", the activity over");
                Assert.That(director.ActivityWarpHoldsTheCamera, Is.False, how + ": the activity over, the warp's view is over," + Why());
                Assert.That(director.ViewedRooms, Is.EqualTo(cameraRig.Rooms), how + ": free play's view (RS5)." + Why());
                yield return null;
                Rs5AssertFreePlaysView(how + ", the activity over");
            }
            Rs6bMotion(false);
        }

        /// <summary>
        /// The conversation's two-shot draws the pair's rooms (D6): with the player a metre into the living
        /// room and the houseguest a metre into the bedroom, across the doorway, the two-shot names both rooms
        /// in the call. It has no clock, so until the camera has come in on the pair the view is free play's,
        /// which from RS5 draws the rig's rooms - the two-shot's, the pair's, which hold the player's own room,
        /// so no room goes before the camera has moved - and from the late pass it arrives, the pair's rooms: their bits, their lamps inside the render, both drawn, a houseguest in the
        /// kitchen hidden. The two's rooms are tracked as they stand: the houseguest carried from the bedroom into
        /// the private room draws the private room on the next frame. Ended, free play's view. A
        /// conversation the director opens with someone in the kitchen draws the kitchen once its camera is in,
        /// and its panel closed, free play's view in the same frame. Under reduced motion the two-shot takes no
        /// shot and keeps the viewer's: the view's own, free play's (RS5).
        /// </summary>
        [UnityTest]
        public IEnumerator RoomScope_AConversationDrawsThePairsRooms()
        {
            director.ClosePanels();
            director.SuspendNpcAutonomyForDiagnostics();
            Rs6bMotion(false);
            yield return null;
            yield return Rs4BodiesBuilt();
            var scope = ScopeOf(director);
            var homes = LampScopeHomes(scope);
            var view = cameraRig.ViewCamera;
            ScopeParkHouseguests(scope);
            var guests = SceneComponents<HouseNpc>().Where(npc => npc.isActiveAndEnabled).ToArray();
            Assert.That(guests.Length, Is.GreaterThanOrEqualTo(2), "Two houseguests to place.");
            HouseNpc partner = guests[0], third = guests[1];
            int livingIndex = RoomLayers.IndexOf("Living"), bedroomIndex = RoomLayers.IndexOf("Bedroom"), privateIndex = RoomLayers.IndexOf("Private");
            var door = ScopeDoorways(scope).First(c => c.From == livingIndex && c.To == bedroomIndex);
            ScopePlace(player.transform, door.Start, .5f);
            ScopePlace(partner.transform, door.End, .5f);
            ScopePlace(third.transform, ScopeOpenFloor(scope, RoomLayers.IndexOf("Kitchen")), 2f);
            yield return null;
            yield return null;
            // The room view changes rooms at most once per 0.6 s (RS5): the placing's change waited out.
            yield return RoomViewWaitOutTheLastChange();
            Assert.That(director.WalkerRoomOf(player.transform), Is.EqualTo("Living"), "The player stands in the living room.");
            Assert.That(director.WalkerRoomOf(partner.transform), Is.EqualTo("Bedroom"), partner.DisplayName + " stands in the bedroom, across the doorway.");
            Assert.That(director.WalkerRoomOf(third.transform), Is.EqualTo("Kitchen"), third.DisplayName + " stands in the kitchen.");
            Assert.That(director.ViewedRooms, Is.EqualTo(RoomSet.Of("Living")), "Free play draws the player's living room (RS5).");
            var pair = RoomSet.Of("Living", "Bedroom");

            var authored = homes.Select(home => home.Light.enabled).ToArray();
            cameraRig.SetConversationFocus(player.transform, partner.transform);
            try
            {
                // The call: the two-shot is taken and names the pair's rooms, the camera still at the dollhouse's distance.
                Assert.That(cameraRig.IsConversationFocused && cameraRig.HasShot, Is.True, "The two-shot is taken,");
                Assert.That(cameraRig.Rooms, Is.EqualTo(pair), "and names the pair's rooms in the call: the living room and the bedroom.");
                Assert.That(cameraRig.HasArrived(HouseCameraRig.ArrivalTolerance), Is.False, "The camera has not come in on the pair yet,");
                Assert.That(cameraRig.HoldsTheTwoShot, Is.False, "so the two-shot does not hold the view yet:");
                Assert.That(director.CurrentRoomScopeSource, Is.EqualTo(EpisodeDirector.RoomScopeSource.FreePlay), "the view is the viewer's own,");
                Assert.That(director.ViewedRooms, Is.EqualTo(pair),
                    "free play's (RS5), the rig's rooms: the pair's, which hold the player's living room, so no room goes before the camera has moved.");
                var first = Rs6aRenderNow(homes);
                Assert.That(first.Viewed, Is.EqualTo(pair), "A render on the call's frame draws the pair's rooms,");
                Assert.That(first.RoomBits, Is.EqualTo(pair.CullingBits), "their bits,");
                Rs6aAssertLamps(first, homes, authored, pair, "The call's frame");
                int coming = 0;
                float inBy = Time.realtimeSinceStartup + 3f;
                while (!cameraRig.HoldsTheTwoShot && Time.realtimeSinceStartup < inBy)
                {
                    yield return null;
                    if (cameraRig.HoldsTheTwoShot) break;
                    coming++;
                    Rs5AssertFreePlaysView("The camera coming in on the pair, frame " + coming);
                }
                Assert.That(cameraRig.HoldsTheTwoShot, Is.True, "The camera came in on the pair and the two-shot holds the camera,");
                Assert.That(director.CurrentRoomScopeSource, Is.EqualTo(EpisodeDirector.RoomScopeSource.Conversation), "and is the view.");
                Assert.That(cameraRig.Rooms, Is.EqualTo(pair), "Its rooms are the pair's,");
                Assert.That(director.ViewedRooms, Is.EqualTo(pair), "drawn with nothing held from the camera's arrival.");
                Assert.That(view.cullingMask & RoomLayers.Mask, Is.EqualTo(pair.CullingBits), "The late pass of the arrival drew the pair's room bits.");
                var sight = Rs6aRenderNow(homes);
                Assert.That(sight.Viewed, Is.EqualTo(pair));
                Assert.That(sight.RoomBits, Is.EqualTo(pair.CullingBits), "The main camera's room bits are the pair's rooms'.");
                Rs6aAssertLamps(sight, homes, authored, pair, "The two-shot");
                Rs6aAssertDrawn(player.transform, "The player, one of the two,");
                Rs6aAssertDrawn(partner.transform, partner.DisplayName + ", the other,");
                Rs6aAssertHidden(third.transform, third.DisplayName + ", in the kitchen,");

                // The two's rooms are tracked: the houseguest carried from the bedroom into the private room next
                // door, to its middle, well clear of the kitchen's doorway band for the kitchen's conversation below.
                ScopePlace(partner.transform, ScopeOpenFloor(scope, privateIndex), 2f);
                yield return null;
                Assert.That(director.WalkerRoomOf(partner.transform), Is.EqualTo("Private"), partner.DisplayName + " stands in the private room now.");
                Assert.That(cameraRig.HoldsTheTwoShot, Is.True, "The two-shot still holds, the camera following the pair in,");
                Assert.That(director.ViewedRooms.Contains("Private"), Is.True, "and the view draws the private room the houseguest went into (" + director.ViewedRooms + "),");
                Assert.That(view.cullingMask & RoomSet.Of("Private").CullingBits, Is.EqualTo(RoomSet.Of("Private").CullingBits), "its bit in the main camera's mask,");
                Assert.That(director.ViewedRooms.Contains("Living"), Is.True, "with the player's living room.");
                Rs6aAssertDrawn(partner.transform, partner.DisplayName + ", in the private room,");
            }
            finally { cameraRig.EndConversation(); }
            Assert.That(cameraRig.HoldsTheTwoShot, Is.False, "Ended, the two-shot lets go,");
            Assert.That(director.ViewedRooms, Is.EqualTo(RoomSet.Of("Living")), "and the view is free play's, the player's living room (RS5).");
            yield return null;
            Rs5AssertFreePlaysView("The two-shot ended");

            // A conversation the director opens: its two-shot names the kitchen, where the two stand.
            yield return OpenNearbyNpc(third);
            try
            {
                Assert.That(cameraRig.IsConversationFocused && cameraRig.HasShot, Is.True, "The conversation takes its two-shot.");
                Assert.That(director.WalkerRoomOf(player.transform), Is.EqualTo("Kitchen"), "The player walked over into the kitchen.");
                yield return Rs6bWaitUntilArrived(() => cameraRig.HoldsTheTwoShot, "The camera comes in on the two and the two-shot holds the camera.");
                Assert.That(director.ViewedRooms, Is.EqualTo(RoomSet.Of("Kitchen")), "The conversation draws the kitchen.");
                Assert.That(view.cullingMask & RoomLayers.Mask, Is.EqualTo(RoomSet.Of("Kitchen").CullingBits));
                Rs6aAssertHidden(partner.transform, partner.DisplayName + ", in the private room,");
            }
            finally { director.ClosePanels(); }
            Assert.That(cameraRig.IsConversationFocused, Is.False, "The panel closed ends the conversation,");
            Assert.That(director.ViewedRooms, Is.EqualTo(cameraRig.Rooms), "and the view is free play's in the same frame (RS5).");
            yield return null;
            Rs5AssertFreePlaysView("The conversation closed");

            // Under reduced motion the two-shot takes no shot: the view is the view's own.
            Rs6bMotion(true);
            Assert.That(cameraRig.ReducedMotion, Is.True, "The rig is under reduced motion.");
            cameraRig.SetConversationFocus(player.transform, partner.transform);
            try
            {
                Assert.That(cameraRig.IsConversationFocused, Is.True, "The conversation is focused,");
                Assert.That(cameraRig.HoldsTheTwoShot, Is.False, "with no two-shot of its own:");
                Assert.That(director.CurrentRoomScopeSource, Is.EqualTo(EpisodeDirector.RoomScopeSource.FreePlay),
                    "the view is the viewer's own, free play's (live from RS5),");
                Assert.That(director.ViewedRooms, Is.EqualTo(cameraRig.Rooms), "the rig's rooms.");
                yield return null;
                Assert.That(cameraRig.HoldsTheTwoShot, Is.False, "A frame on, still no two-shot of its own,");
                Rs5AssertFreePlaysView("A conversation under reduced motion");
            }
            finally
            {
                cameraRig.EndConversation();
                Rs6bMotion(false);
            }
        }

        /// <summary>
        /// A jury or evicted player's watch draws the watched room (D6): before any watch the view is free
        /// play's, which for a player out of the house is the room nearest the camera's focus (RS5). "Watching
        /// the kitchen", with the kitchen's marker stood a metre over a neighbouring floor, names the kitchen in
        /// the call - the room itself, not the floor under its marker - and free play draws it from the call
        /// while the room view cuts the camera over it in the same frame; from then the kitchen alone - its bit and its lamps (none) inside the render, a houseguest there
        /// drawn and those in the yard hidden - and the bedroom's watch the bedroom, the same way. The overview
        /// opened over the watch is the whole house, and left by its own way out gives the watch back: the
        /// bedroom again once the camera is back in. A follow takes the camera from the watch: following's
        /// view, the followed houseguest's room (RS5).
        /// </summary>
        [UnityTest]
        public IEnumerator RoomScope_AJuryWatchDrawsTheWatchedRoom()
        {
            yield return InstallDiaryFixture(state => state.phase == EpisodePhase.Social && state.Find(state.playerId).status != ContestantStatus.Active,
                "a player out of the house, in free time");
            director.ClosePanels();
            director.SuspendNpcAutonomyForDiagnostics();
            Rs6bMotion(false);
            yield return null;
            yield return Rs4BodiesBuilt();
            var scope = ScopeOf(director);
            var homes = LampScopeHomes(scope);
            var view = cameraRig.ViewCamera;
            ScopeParkHouseguests(scope);
            var guests = SceneComponents<HouseNpc>().Where(npc => npc.isActiveAndEnabled).ToArray();
            Assert.That(guests.Length, Is.GreaterThanOrEqualTo(2), "Two houseguests in the house.");
            HouseNpc inKitchen = guests[0], inYard = guests[1];
            ScopePlace(inKitchen.transform, ScopeOpenFloor(scope, RoomLayers.IndexOf("Kitchen")), 2f);
            yield return null;
            yield return null;
            Assert.That(director.WalkerRoomOf(inKitchen.transform), Is.EqualTo("Kitchen"));
            Assert.That(director.WalkerRoomOf(inYard.transform), Is.EqualTo("Yard"));
            Assert.That(director.WatchHoldsTheCamera, Is.False, "Before any watch, no watch holds the camera:");
            Assert.That(director.CurrentRoomScopeSource, Is.EqualTo(EpisodeDirector.RoomScopeSource.FreePlay), "the view is free play's (live from RS5),");
            Assert.That(cameraRig.ViewsThePlayer, Is.False, "which for a player out of the house watches a room, not the player's body:");
            Assert.That(director.ViewedRooms.Count, Is.EqualTo(1), "the room nearest the camera's focus (" + director.ViewedRooms + ").");
            Rs5AssertFreePlaysView("Before any watch");

            // The kitchen's marker a metre over a neighbouring floor: a watch that took the floor under its point
            // would draw that room, so only the room the call names keeps the kitchen the view.
            int kitchenIndex = RoomLayers.IndexOf("Kitchen");
            var marker = SceneComponents<HouseRoomMarker>().First(m => m.RoomName == "Kitchen");
            var markerAt = marker.transform.position;
            var box = scope.Box(kitchenIndex);
            var markerMoved = new[]
                {
                    new Vector3(box.xMin - 1f, markerAt.y, markerAt.z), new Vector3(box.xMax + 1f, markerAt.y, markerAt.z),
                    new Vector3(markerAt.x, markerAt.y, box.yMin - 1f), new Vector3(markerAt.x, markerAt.y, box.yMax + 1f),
                }
                .FirstOrDefault(at => scope.RoomUnder(at) >= 0 && scope.RoomUnder(at) != kitchenIndex);
            int underMoved = scope.RoomUnder(markerMoved);
            Assert.That(markerMoved != default(Vector3) && underMoved >= 0 && underMoved != kitchenIndex, Is.True,
                "A metre past one of the kitchen's sides is another room's floor (" + HouseRoomScope.RoomId(underMoved) + ").");
            var kitchen = RoomSet.Of("Kitchen");
            var authored = homes.Select(home => home.Light.enabled).ToArray();
            marker.transform.position = markerMoved;
            try { director.GoToRoom("Kitchen"); }
            finally { marker.transform.position = markerAt; }
            Assert.That(director.StatusMessage, Does.StartWith("Watching the "), "The player out of the house watches, not walks.");
            Assert.That(cameraRig.Rooms, Is.EqualTo(kitchen), "The watch names the kitchen in the call, not the " + HouseRoomScope.RoomId(underMoved) + " under its marker,");
            Assert.That(cameraRig.HasArrived(HouseCameraRig.ArrivalTolerance), Is.False, "the camera not yet over it,");
            Assert.That(director.WatchHoldsTheCamera, Is.False, "so the watch does not hold the view yet:");
            Assert.That(director.ViewedRooms, Is.EqualTo(kitchen), "free play (RS5) draws the room the call names; the room view cuts the camera there this frame.");
            yield return Rs6bWaitForTheWatch("The kitchen's watch");
            Assert.That(director.CurrentRoomScopeSource, Is.EqualTo(EpisodeDirector.RoomScopeSource.Watching), "The watch is the view,");
            Assert.That(director.ViewedRooms, Is.EqualTo(kitchen), "the kitchen alone, not the " + HouseRoomScope.RoomId(underMoved) + " under its marker.");
            Assert.That(view.cullingMask & RoomLayers.Mask, Is.EqualTo(kitchen.CullingBits), "The main camera's room bit is the kitchen's.");
            var sight = Rs6aRenderNow(homes);
            Assert.That(sight.Viewed, Is.EqualTo(kitchen));
            Rs6aAssertLamps(sight, homes, authored, kitchen, "The kitchen's watch");
            Rs6aAssertDrawn(inKitchen.transform, inKitchen.DisplayName + ", in the kitchen,");
            Rs6aAssertHidden(inYard.transform, inYard.DisplayName + ", in the yard,");

            var bedroom = RoomSet.Of("Bedroom");
            director.GoToRoom("Bedroom");
            Assert.That(cameraRig.Rooms, Is.EqualTo(bedroom), "The bedroom's watch names the bedroom,");
            Assert.That(director.WatchHoldsTheCamera, Is.False, "and holds the view once the camera is over it:");
            Assert.That(director.ViewedRooms, Is.EqualTo(bedroom), "until then free play's, the rooms the call names (RS5).");
            yield return Rs6bWaitForTheWatch("The bedroom's watch");
            Assert.That(director.ViewedRooms, Is.EqualTo(bedroom), "The bedroom's watch draws the bedroom.");
            Assert.That(view.cullingMask & RoomLayers.Mask, Is.EqualTo(bedroom.CullingBits));
            Rs6aAssertHidden(inKitchen.transform, inKitchen.DisplayName + ", in the kitchen the watch left,");

            // The overview over the watch: the whole house; left by its own way out, the watch again.
            Assert.That(director.ShowOverview(), Is.True, "The overview opens over the watch.");
            Assert.That(director.WatchHoldsTheCamera, Is.False, "The map takes the view from the watch:");
            Assert.That(director.CurrentRoomScopeSource, Is.EqualTo(EpisodeDirector.RoomScopeSource.Overview), "the overview's,");
            yield return null;
            Rs6bAssertTheWholeHouse("The overview over the watch");
            director.EndOverview();
            Assert.That(cameraRig.Rooms.Contains("Bedroom"), Is.True, "Left, the camera goes back to the bedroom's watch (" + cameraRig.Rooms + "),");
            Assert.That(director.WatchHoldsTheCamera, Is.False, "and the watch holds once the camera is back:");
            Assert.That(director.ViewedRooms, Is.EqualTo(cameraRig.Rooms), "until then free play's, the rig's rooms on the way back (RS5),");
            Assert.That(director.ViewedRooms.Contains("Bedroom"), Is.True, "the bedroom among them.");
            yield return Rs6bWaitForTheWatch("The bedroom's watch after the overview");
            Assert.That(director.ViewedRooms, Is.EqualTo(bedroom), "Back from the map, the bedroom's watch draws the bedroom again.");
            Assert.That(view.cullingMask & RoomLayers.Mask, Is.EqualTo(bedroom.CullingBits));

            cameraRig.FocusSubject(inKitchen.transform);
            try
            {
                Assert.That(director.WatchHoldsTheCamera, Is.False, "A follow takes the camera from the watch:");
                Assert.That(director.CurrentRoomScopeSource, Is.EqualTo(EpisodeDirector.RoomScopeSource.Following), "the view is following's,");
                Assert.That(director.ViewedRooms, Is.EqualTo(kitchen), "the followed houseguest's kitchen (RS5).");
                yield return null;
                Rs5AssertFreePlaysView("A follow over the watch");
            }
            finally { cameraRig.ClearSubject(); }
        }

        // ------------------------------------------------------------------------------------
        // Helpers, shared with the tests RS6b changes.
        // ------------------------------------------------------------------------------------

        /// <summary>Reduced motion on or off for the director, the rig and every body, as the settings' switch sets it.</summary>
        private void Rs6bMotion(bool reduced)
        {
            typeof(EpisodeDirector).GetField("reducedMotion", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(director, reduced);
            cameraRig.SetReducedMotion(reduced);
            foreach (var visual in SceneComponents<CharacterPresentation>()) visual.SetReducedMotion(reduced);
        }

        /// <summary>The main view the whole house, a frame after the change: every room bit drawn, nobody hidden, nothing out.</summary>
        private void Rs6bAssertTheWholeHouse(string what)
        {
            Assert.That(director.ViewedRooms.IsWholeHouse, Is.True, what + ": the main view is the whole house (" + director.CurrentRoomScopeSource + ").");
            Assert.That(cameraRig.ViewCamera.cullingMask & RoomLayers.Mask, Is.EqualTo(RoomLayers.Mask), what + ": every room bit in the main camera's mask.");
            Assert.That(director.HiddenBodyCount, Is.Zero, what + ": nobody hidden.");
            Assert.That(director.MainCameraRoomBitsTaken, Is.Zero, what + ": no room bit out.");
        }

        /// <summary>
        /// A whole-house source (D1, D6) holding the view: the source itself, the whole house drawn - with the
        /// rig's own rooms the whole house where its shot names it - and every lamp on inside a render.
        /// </summary>
        private void Rs6bAssertAWholeHouseSource(EpisodeDirector.RoomScopeSource source, List<HouseRoomScope.LampHome> homes, bool rigWhole, string what)
        {
            Assert.That(director.CurrentRoomScopeSource, Is.EqualTo(source), what + ": the view is the " + source + "'s.");
            Assert.That(EpisodeDirector.IsWholeHouseSource(source), Is.True, what + ": the " + source + " is the whole house by decision.");
            if (rigWhole) Assert.That(cameraRig.Rooms.IsWholeHouse, Is.True, what + ": its shot names the whole house (" + cameraRig.Rooms + ").");
            Rs6bAssertTheWholeHouse(what);
            var sight = Rs6aRenderNow(homes);
            Assert.That(sight.Viewed.IsWholeHouse, Is.True, what + ": a render draws the whole house,");
            Assert.That(sight.RoomBits, Is.EqualTo(RoomLayers.Mask), what + ": every room bit,");
            Assert.That(sight.LampsOff, Is.Zero, what + ": every lamp.");
        }

        /// <summary>
        /// The opening holding the view (D6), for a frame the opening's tests take: its source, its shot naming
        /// the whole house, and the main camera drawing every room and every body.
        /// </summary>
        private void Rs6bAssertTheOpeningDrawsTheWholeHouse(string what)
        {
            Assert.That(director.CurrentRoomScopeSource, Is.EqualTo(EpisodeDirector.RoomScopeSource.Opening), what + ": the opening has the view.");
            Assert.That(cameraRig.Rooms.IsWholeHouse, Is.True, what + ": the opening's shot names the whole house (" + cameraRig.Rooms + ").");
            Rs6bAssertTheWholeHouse(what);
        }

        /// <summary>
        /// The walk-out holding the view (D6): its source, and the main camera drawing every room and every body
        /// whatever the rig follows or frames.
        /// </summary>
        private void Rs6bAssertTheWalkOutDrawsTheWholeHouse(string what)
        {
            Assert.That(director.WalkingOutId, Is.Not.Null, what + ": somebody is walking out.");
            Assert.That(director.CurrentRoomScopeSource, Is.EqualTo(EpisodeDirector.RoomScopeSource.WalkOut), what + ": the walk-out has the view.");
            Rs6bAssertTheWholeHouse(what);
        }

        /// <summary>
        /// The diary's chair shot holding the view (D6): with nothing held, the private room alone, its bit
        /// alone in the main camera's mask, every piece of the studio drawn by it, and inside its render only
        /// the private room's lamps on.
        /// </summary>
        private void Rs6bAssertTheDiaryDrawsThePrivateRoom(string what)
        {
            Assert.That(director.DiaryShotHoldsTheCamera, Is.True, what + ": the chair shot holds the camera.");
            Assert.That(director.CurrentRoomScopeSource, Is.EqualTo(EpisodeDirector.RoomScopeSource.Diary), what + ": the diary is the view.");
            Assert.That(cameraRig.Rooms, Is.EqualTo(EpisodeDirector.DiaryShotRooms), what + ": the chair shot names the private room.");
            Rs6aAssertTheViewIs(EpisodeDirector.DiaryShotRooms, what);
            var mask = cameraRig.ViewCamera.cullingMask;
            var studio = SceneComponents<Transform>().FirstOrDefault(t => t.name == "Diary interview backdrop" && t.gameObject.activeInHierarchy);
            Assert.That(studio, Is.Not.Null, what + ": the studio stands behind the chair.");
            var hidden = studio.GetComponentsInChildren<Renderer>().Where(r => r.enabled && !RoomLayers.Draws(mask, r.gameObject.layer))
                .Select(r => r.name + " on " + r.gameObject.layer).ToArray();
            Assert.That(hidden, Is.Empty, what + ": every piece of the studio is drawn by the main camera.");
            // The player is no camera subject under the diary's shot: drawn because they sit in the private room,
            // which is what the interview is of.
            Assert.That(director.WalkerRoomOf(player.transform), Is.EqualTo("Private"), what + ": the player sits in the private room.");
            Rs6aAssertDrawn(player.transform, what + ": the player, in the chair,");
            var homes = LampScopeHomes(ScopeOf(director));
            var authored = homes.Select(home => home.Light.enabled).ToArray();
            var sight = Rs6aRenderNow(homes);
            Assert.That(sight.Viewed, Is.EqualTo(EpisodeDirector.DiaryShotRooms), what + ": the render draws the private room alone.");
            Rs6aAssertLamps(sight, homes, authored, EpisodeDirector.DiaryShotRooms, what);
        }

        /// <summary>
        /// The diary open with no chair shot yet (D6): the walk to the chair and the sitting down are the view's
        /// own - free play's, the rig's rooms from RS5 - so the diary is not the view.
        /// </summary>
        private void Rs6bAssertTheDiaryIsNotYetTheView(string what)
        {
            Assert.That(director.IsDiaryOpen, Is.True, what + ": the diary is open,");
            Assert.That(director.DiaryShotHoldsTheCamera, Is.False, what + ": with no chair shot of its own yet,");
            Assert.That(director.CurrentRoomScopeSource, Is.EqualTo(EpisodeDirector.RoomScopeSource.FreePlay), what + ": so the view is free play's,");
            Assert.That(director.ViewedRooms, Is.EqualTo(cameraRig.Rooms), what + ": the rig's rooms (RS5) (" + director.ViewedRooms + ").");
        }

        /// <summary>
        /// Free play's or following's view (live from RS5), a frame after the change: the rig's rooms, not the
        /// whole house, and the main camera's room bits theirs.
        /// </summary>
        private void Rs5AssertFreePlaysView(string what)
        {
            var source = director.CurrentRoomScopeSource;
            Assert.That(source == EpisodeDirector.RoomScopeSource.FreePlay || source == EpisodeDirector.RoomScopeSource.Following, Is.True,
                what + ": the view is free play's or following's (" + source + ").");
            Assert.That(director.ViewedRooms, Is.EqualTo(cameraRig.Rooms), what + ": the rig's rooms (" + cameraRig.Rooms + ").");
            Assert.That(director.ViewedRooms.IsWholeHouse, Is.False, what + ": not the whole house: free play and following are live from RS5.");
            Assert.That(cameraRig.ViewCamera.cullingMask & RoomLayers.Mask, Is.EqualTo(director.ViewedRooms.CullingBits), what + ": the main camera's room bits are the view's.");
        }

        /// <summary>Waits on the real clock (the rig eases on unscaled time, which no frame count pins) for a condition, then asserts it.</summary>
        private IEnumerator Rs6bWaitUntilArrived(System.Func<bool> arrived, string what)
        {
            float deadline = Time.realtimeSinceStartup + 6f;
            while (!arrived() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(arrived(), Is.True, what);
        }

        /// <summary>
        /// A watch made, the camera on its way (D6): every frame until the camera has come in over the watched
        /// room the view is free play's (RS5: the rooms the call names, the room view cutting the camera over
        /// them) and then the watch holds the camera. On the real clock.
        /// </summary>
        private IEnumerator Rs6bWaitForTheWatch(string what)
        {
            float deadline = Time.realtimeSinceStartup + 6f;
            int frame = 0;
            while (!director.WatchHoldsTheCamera && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                if (director.WatchHoldsTheCamera) break;
                Rs5AssertFreePlaysView(what + ", the camera on its way, frame " + ++frame);
            }
            Assert.That(director.WatchHoldsTheCamera, Is.True, what + ": the camera came in over the watched room and the watch holds the camera.");
        }

        /// <summary>
        /// Puts the houseguests free to walk - their agent moving their body - on a grid in one room, 1.4 m apart,
        /// each on the nearest floor within two metres; one seated or at an activity stays where it is. For a test
        /// the NPC world stays live in, so they may walk on from there.
        /// </summary>
        private static void Rs6bParkHouseguestsIn(HouseRoomScope scope, int room)
        {
            var box = scope.Box(room);
            int columns = Mathf.Max(1, Mathf.FloorToInt((box.width - 2f) / 1.4f));
            int i = 0;
            foreach (var npc in SceneComponents<HouseNpc>().Where(npc => npc.gameObject.activeInHierarchy))
            {
                var agent = npc.GetComponent<NavMeshAgent>();
                if (agent == null || !agent.isActiveAndEnabled || !agent.updatePosition) continue;
                var at = new Vector3(box.xMin + 1f + 1.4f * (i % columns), 0f, box.yMin + 1f + 1.4f * (i / columns));
                i++;
                if (!NavMesh.SamplePosition(at, out var hit, 2f, agent.areaMask) || !agent.Warp(hit.position)) continue;
                agent.ResetPath();
            }
            Physics.SyncTransforms();
        }
    }
}
