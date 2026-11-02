using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Episode;
using Gamesim.House;
using Gamesim.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// The ceremonies and the competitions, live (ROOM-SCOPE-PLAN RS6a, D6, D7): with nothing held, each
    /// of their shots names its rooms in the call that moves the camera, and while those shots hold the
    /// camera the main view is those rooms - the ceremony's room, with what the view drew as its framing's
    /// move left until it lands (from free play, its room view from RS5); the stage's room for every staged shot,
    /// the veto meeting's close shot among them; the Yard for the competition's. The rooms are read where
    /// the view is drawn: the main camera's mask (bits 20 to 27), the lamps inside its own render (a render
    /// by hand, read in its begin callback after the director's hook), and the bodies, each drawn or left
    /// out by the mask. A competition under reduced motion takes no shot, and a framing the viewer took
    /// back, a framing let go and a shot let go are the view's own again: free play's or following's, live
    /// from RS5. And R19's guard: no body, drawn or hidden, pulls the boom of a low shot in.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        /// <summary>What one render of the main camera saw, from inside its begin callback, after the director's hook.</summary>
        private sealed class Rs6aSight
        {
            public int Renders;
            public RoomSet Viewed;
            public int RoomBits;
            public int LampsOff;
            public bool[] Lamps;
        }

        /// <summary>
        /// A ceremony framed: the call that moves the camera names the ceremony's room, so on its way the
        /// rooms the view drew as it left are drawn with the ceremony's room, and from its landing the
        /// ceremony's room alone, the player left in the bedroom with it. From free play - the room view of
        /// the bedroom from RS5 - the way there is both rooms - the mask's room bits, the lamps inside the main
        /// camera's render and the bodies in both: nothing the viewer saw goes before the camera has moved.
        /// With the nomination's marker stood a metre north of its room, over the north block's floor, the
        /// landed view is still the nomination room: the rooms named in the call, not the floor under the
        /// camera's point. Let go, the move back - the framing taken from free play on the player - ends on the
        /// player's room, tracked (RS5), so once it lands free play shows the bedroom again, its bit, its lamps,
        /// its bodies, and when the player goes into the living room next door the view cuts with them, as it
        /// did before the ceremony. The same with the scope held on. Under reduced motion the framing is a cut,
        /// its room alone from the call, and its let-go a cut back to the bedroom.
        /// </summary>
        [UnityTest]
        public IEnumerator RoomScope_ACeremonyMoveDrawsBothRoomsUntilItLands()
        {
            director.ClosePanels();
            director.SuspendNpcAutonomyForDiagnostics();
            AskForTheStages(false);
            yield return null;
            yield return Rs4BodiesBuilt();
            var scope = ScopeOf(director);
            var homes = LampScopeHomes(scope);
            var authored = homes.Select(home => home.Light.enabled).ToArray();
            var view = cameraRig.ViewCamera;
            Assert.That(view.cullingMask & RoomLayers.Mask, Is.EqualTo(director.ViewedRooms.CullingBits), "Before the ceremony the main camera's room bits are the view's.");
            Assert.That(cameraRig.ReducedMotion, Is.False, "The framing moves on its clock: the rig is not under reduced motion.");

            // The player and one houseguest in the bedroom, out of each other's reach (no Talk names them),
            // one in the nomination room, and the rest at the yard's far side.
            int bedroomIndex = RoomLayers.IndexOf("Bedroom"), nominationIndex = RoomLayers.IndexOf("Nomination");
            ScopeParkHouseguests(scope);
            var guests = SceneComponents<HouseNpc>().Where(npc => npc.isActiveAndEnabled).ToArray();
            Assert.That(guests.Length, Is.GreaterThanOrEqualTo(3), "Three houseguests to stand in three rooms.");
            HouseNpc inBedroom = guests[0], inNomination = guests[1], inYard = guests[2];
            ScopePlace(player.transform, ScopeOpenFloor(scope, bedroomIndex), 2f);
            // The houseguest deep in the bedroom's north half: well clear of the living room's doorway band.
            var bedroomBox = scope.Box(bedroomIndex);
            Vector3? deep = null;
            foreach (var corner in new[] { new Vector3(bedroomBox.xMin + 2f, 0f, bedroomBox.yMax - 2f), new Vector3(bedroomBox.xMax - 2f, 0f, bedroomBox.yMax - 2f) })
                if (deep == null && NavMesh.SamplePosition(corner, out var hit, 1.5f, player.Agent.areaMask)
                    && Vector3.Distance(hit.position, player.transform.position) > 4f
                    && hit.position.z > bedroomBox.yMin + 2f && hit.position.z < bedroomBox.yMax - 1f)
                    deep = hit.position;
            Assert.That(deep.HasValue, Is.True, "A floor in the bedroom's north half, out of Talk's reach of the player.");
            ScopePlace(inBedroom.transform, deep.Value, .3f);
            ScopePlace(inNomination.transform, ScopeOpenFloor(scope, nominationIndex), 2f);
            var bedroom = RoomSet.Of("Bedroom");
            var nomination = RoomSet.Of(EpisodeDirector.CeremonyRoom(CeremonySting.NominationKind));
            Assert.That(nomination, Is.EqualTo(RoomSet.Of("Nomination")), "The nomination is framed in the nomination room.");
            var both = bedroom.With(nomination);

            // Free play on the player in the bedroom (F): live from RS5, the room view of the bedroom.
            cameraRig.RecenterOnPlayer();
            yield return null;
            yield return null;
            Assert.That(cameraRig.ViewsThePlayer, Is.True, "The view is free play's on the player,");
            Assert.That(cameraRig.Rooms, Is.EqualTo(bedroom), "the player's bedroom.");
            Assert.That(director.CurrentRoomScopeSource, Is.EqualTo(EpisodeDirector.RoomScopeSource.FreePlay));
            Assert.That(director.ViewedRooms, Is.EqualTo(bedroom), "Free play draws the bedroom (RS5).");
            Rs6aAssertDrawn(inBedroom.transform, inBedroom.DisplayName + ", in the bedroom, in free play,");
            Rs6aAssertHidden(inYard.transform, inYard.DisplayName + ", in the yard, in free play,");
            Assert.That(director.WalkerRoomOf(inYard.transform), Is.EqualTo("Yard"), inYard.DisplayName + " stands in the yard.");
            Assert.That(director.WalkerRoomOf(inBedroom.transform), Is.EqualTo("Bedroom"), inBedroom.DisplayName + " stands in the bedroom.");
            Assert.That(director.WalkerRoomOf(inNomination.transform), Is.EqualTo("Nomination"), inNomination.DisplayName + " stands in the nomination room.");

            // The nomination's marker a metre north of its room, over the north block's floor: a move that named
            // no rooms would watch the floor under its point there, so only the rooms the call names keep the
            // nomination room the view.
            var marker = SceneComponents<HouseRoomMarker>().First(m => m.RoomName == "Nomination");
            var markerAt = marker.transform.position;
            var markerMoved = new Vector3(markerAt.x, markerAt.y, scope.Box(nominationIndex).yMax + 1f);
            int underMoved = scope.RoomUnder(markerMoved);
            Assert.That(underMoved >= 0 && underMoved != nominationIndex, Is.True,
                "A metre north of the nomination room is another room's floor: " + HouseRoomScope.RoomId(underMoved) + ".");
            Rs6aSight sight;
            marker.transform.position = markerMoved;
            try
            {
                director.FrameCeremony(CeremonySting.NominationKind);
                Assert.That(director.IsFramingCeremony, Is.True, "The nomination is framed.");
                Assert.That(director.CeremonyFramingHoldsTheCamera, Is.True, "Its move holds the camera.");
                Assert.That(director.CurrentRoomScopeSource, Is.EqualTo(EpisodeDirector.RoomScopeSource.Ceremony), "The ceremony's framing is the view.");
                Assert.That(cameraRig.IsTravelling, Is.True, "on its clock");
                // On its way, the rooms the view drew as it left - free play's bedroom - with the nomination room's.
                Assert.That(cameraRig.Rooms, Is.EqualTo(both),
                    "The move leaves free play's bedroom, and draws it with the nomination room until it lands: " + cameraRig.Rooms + ".");
                Assert.That(director.ViewedRooms, Is.EqualTo(both), "Nothing the viewer saw goes before the camera has moved.");
                // A render straight after the move, as a capture's by hand would be.
                sight = Rs6aRenderNow(homes);
                Assert.That(sight.Viewed, Is.EqualTo(both), "The render on the move's own frame draws both rooms,");
                Assert.That(sight.RoomBits, Is.EqualTo(both.CullingBits), "their room bits,");
                Rs6aAssertLamps(sight, homes, authored, both, "on the way from free play");
                yield return null;
                Assert.That(cameraRig.IsTravelling, Is.True, "A frame into its 1.2 seconds, the move is still on its way.");
                Assert.That(view.cullingMask & RoomLayers.Mask, Is.EqualTo(both.CullingBits), "The late pass keeps both rooms' bits on the way.");
                Rs6aAssertDrawn(player.transform, "The player, in the bedroom the move left,");
                Rs6aAssertDrawn(inBedroom.transform, inBedroom.DisplayName + ", in the bedroom, on the way,");
                Rs6aAssertDrawn(inNomination.transform, inNomination.DisplayName + ", in the nomination room, on the way,");
                Rs6aAssertHidden(inYard.transform, inYard.DisplayName + ", in the yard, on the way,");

                yield return ScopeWaitUntilLanded();
                yield return null;
                Assert.That(director.IsFramingCeremony, Is.True, "Landed, the framing holds.");
                Assert.That(cameraRig.Rooms, Is.EqualTo(nomination), "Landed, the nomination room alone: the room the call names, not the "
                    + HouseRoomScope.RoomId(underMoved) + " floor under the camera's point.");
                Assert.That(director.ViewedRooms, Is.EqualTo(nomination));
                Assert.That(view.cullingMask & RoomLayers.Mask, Is.EqualTo(nomination.CullingBits), "The main camera's room bits are the nomination room's.");
                Rs6aAssertDrawn(inNomination.transform, inNomination.DisplayName + ", in the nomination room,");
                Rs6aAssertHidden(inBedroom.transform, inBedroom.DisplayName + ", left in the bedroom,");
                Rs6aAssertHidden(player.transform, "The player, left in the bedroom and no subject of the ceremony's shot,");
                Rs6aAssertHidden(inYard.transform, inYard.DisplayName + ", in the yard,");
                sight = Rs6aRenderNow(homes);
                Assert.That(sight.Viewed, Is.EqualTo(nomination));
                Rs6aAssertLamps(sight, homes, authored, nomination, "landed");
            }
            finally { marker.transform.position = markerAt; }

            yield return Rs6aFramingLetGo();
            Assert.That(director.CurrentRoomScopeSource, Is.EqualTo(EpisodeDirector.RoomScopeSource.FreePlay), "Let go, free play again,");
            // The move back ends on the player's room, tracked (RS5): drawn with the nomination room until it lands.
            Assert.That(cameraRig.ViewsThePlayer, Is.True, "the move back's view the player's own, not a watch of the room they stood in,");
            yield return ScopeWaitUntilLanded();
            yield return null;
            Assert.That(director.ViewedRooms, Is.EqualTo(bedroom), "landed back, the player's bedroom,");
            Assert.That(view.cullingMask & RoomLayers.Mask, Is.EqualTo(bedroom.CullingBits), "its bit alone of the eight,");
            foreach (var body in new[] { player.transform, inBedroom.transform })
                Rs6aAssertDrawn(body, body.name + ", let go, in the bedroom,");
            foreach (var body in new[] { inNomination.transform, inYard.transform })
                Rs6aAssertHidden(body, body.name + ", let go, out of the bedroom,");
            sight = Rs6aRenderNow(homes);
            Assert.That(sight.Viewed, Is.EqualTo(bedroom));
            Rs6aAssertLamps(sight, homes, authored, bedroom, "let go");

            // The player goes into the living room next door: the view cuts with them, as before the ceremony; and back.
            int livingIndex = RoomLayers.IndexOf("Living");
            var inTheBedroom = player.transform.position;
            yield return RoomViewWaitOutTheLastChange();
            int cutsBefore = cameraRig.RoomViewCuts;
            ScopePlace(player.transform, ScopeOpenFloor(scope, livingIndex), 2f);
            yield return null;
            yield return null;
            Assert.That(director.WalkerRoomOf(player.transform), Is.EqualTo("Living"), "The player stands in the living room.");
            Assert.That(cameraRig.ViewRoom, Is.EqualTo(livingIndex), "The view cut to the living room with the player,");
            Assert.That(cameraRig.RoomViewCuts, Is.GreaterThan(cutsBefore), "by a cut,");
            Assert.That(director.ViewedRooms, Is.EqualTo(RoomSet.Of("Living")), "drawing the living room,");
            Assert.That(director.LookingAwayFromThePlayer, Is.False, "and the view is on the player: looking.");
            yield return RoomViewWaitOutTheLastChange();
            ScopePlace(player.transform, inTheBedroom, .5f);
            yield return null;
            yield return null;
            Assert.That(director.ViewedRooms, Is.EqualTo(bedroom), "Back in the bedroom, the view is the bedroom again.");

            // The same with the scope held on over free play on the player, in the bedroom. The way there draws both
            // rooms, and the landing the nomination room alone.
            var owner = new object();
            director.HoldRoomScope(owner);
            try
            {
                yield return null;
                Assert.That(director.ViewedRooms, Is.EqualTo(bedroom), "Held on, free play's view is the player's bedroom.");
                director.FrameCeremony(CeremonySting.NominationKind);
                Assert.That(director.CurrentRoomScopeSource, Is.EqualTo(EpisodeDirector.RoomScopeSource.Ceremony), "The ceremony's framing is the view.");
                Assert.That(cameraRig.IsTravelling, Is.True, "on its clock");
                Assert.That(cameraRig.Rooms, Is.EqualTo(both), "The move names the nomination room, in the call: with the bedroom the view drew, until it lands.");
                Assert.That(director.ViewedRooms, Is.EqualTo(both), "The main view is both rooms on the way.");
                // A render straight after the move, as a capture's by hand would be: drawn for that render at its begin.
                sight = Rs6aRenderNow(homes);
                Assert.That(sight.Viewed, Is.EqualTo(both), "The render on the move's own frame draws both rooms.");
                Assert.That(sight.RoomBits, Is.EqualTo(both.CullingBits), "The main camera's room bits are the two rooms'.");
                Rs6aAssertLamps(sight, homes, authored, both, "on the way");
                yield return null;
                Assert.That(cameraRig.IsTravelling, Is.True, "A frame into its 1.2 seconds, the move is still on its way.");
                Assert.That(view.cullingMask & RoomLayers.Mask, Is.EqualTo(both.CullingBits), "The late pass keeps the two rooms' bits on the way.");
                Rs6aAssertDrawn(player.transform, "The player, in the bedroom the move left,");
                Rs6aAssertDrawn(inBedroom.transform, inBedroom.DisplayName + ", in the bedroom,");
                Rs6aAssertDrawn(inNomination.transform, inNomination.DisplayName + ", in the nomination room,");
                Rs6aAssertHidden(inYard.transform, inYard.DisplayName + ", in the yard,");
                var asAuthored = new List<string>();
                LampScopeAsAuthored(homes.Select(home => home.Light).ToArray(), authored, "between frames on the way", asAuthored);
                Assert.That(asAuthored, Is.Empty, string.Join("; ", asAuthored));
                yield return ScopeWaitUntilLanded();
                yield return null;
                Assert.That(director.ViewedRooms, Is.EqualTo(nomination), "Landed, the nomination room alone.");
                Assert.That(view.cullingMask & RoomLayers.Mask, Is.EqualTo(nomination.CullingBits));
                Rs6aAssertHidden(inBedroom.transform, inBedroom.DisplayName + ", left in the bedroom,");
            }
            finally { director.Release(owner); }
            Assert.That(director.ViewedRooms, Is.EqualTo(nomination), "Released, the framing still holds the view: the nomination room.");
            yield return Rs6aFramingLetGo();
            yield return ScopeWaitUntilLanded();
            yield return null;
            Assert.That(director.ViewedRooms, Is.EqualTo(bedroom), "Let go with nothing held, free play's bedroom again,");
            Assert.That(view.cullingMask & RoomLayers.Mask, Is.EqualTo(bedroom.CullingBits), "its bit alone,");
            Rs6aAssertHidden(inYard.transform, inYard.DisplayName + ", in the yard,");

            // Under reduced motion the framing cuts: the eviction's room alone, from the call.
            AskForTheStages(true);
            Assert.That(cameraRig.ReducedMotion, Is.True);
            var living = RoomSet.Of(EpisodeDirector.CeremonyRoom(CeremonySting.EvictionKind));
            director.FrameCeremony(CeremonySting.EvictionKind);
            Assert.That(director.IsFramingCeremony, Is.True, "The eviction is framed under reduced motion too.");
            Assert.That(cameraRig.IsTravelling, Is.False, "as a cut,");
            Assert.That(director.ViewedRooms, Is.EqualTo(living), "its room alone from the call that cuts there.");
            yield return null;
            Assert.That(view.cullingMask & RoomLayers.Mask, Is.EqualTo(living.CullingBits), "The main camera's room bits are the living room's.");
            Rs6aAssertHidden(inBedroom.transform, inBedroom.DisplayName + ", in the bedroom, under the cut to the living room,");
            yield return Rs6aFramingLetGo();
            Assert.That(director.ViewedRooms, Is.EqualTo(bedroom), "The cut's framing lets go too, by a cut back to free play's bedroom.");
            Assert.That(view.cullingMask & RoomLayers.Mask, Is.EqualTo(bedroom.CullingBits));
            Rs6aAssertHidden(inYard.transform, inYard.DisplayName + ", in the yard,");
        }

        /// <summary>
        /// A ceremony's framing is a free move with the controls left on, held for its minimum and for as
        /// long as its cards play. A camera the viewer takes back during the hold - F, free play's own
        /// recentre on the player - and a follow begun under it are not the framing's view: with nothing
        /// held the view is theirs (live from RS5), the player's room after F and the followed houseguest's
        /// room under the follow, each room's bit alone drawn and the bodies outside it hidden. A framing the
        /// viewer took has no move back (RS5): the camera stays theirs - the stopped follow's room watched, or
        /// after a floor click and a walk into the next room, that room with the player still followed. A shot
        /// that took the camera straight from the framing keeps it as the framing lets go, and its release goes
        /// back where the framing would have: free play on the player, their room.
        /// </summary>
        [UnityTest]
        public IEnumerator RoomScope_AFramingTheViewerTakesOverIsTheirView()
        {
            director.ClosePanels();
            director.SuspendNpcAutonomyForDiagnostics();
            AskForTheStages(false);
            yield return null;
            yield return Rs4BodiesBuilt();
            var scope = ScopeOf(director);
            var view = cameraRig.ViewCamera;
            var nomination = RoomSet.Of(EpisodeDirector.CeremonyRoom(CeremonySting.NominationKind));
            // The player in the bedroom and a houseguest in the kitchen: neither in the nomination room the framing draws.
            ScopeParkHouseguests(scope);
            ScopePlace(player.transform, ScopeOpenFloor(scope, RoomLayers.IndexOf("Bedroom")), 2f);
            var guest = SceneComponents<HouseNpc>().First(npc => npc.isActiveAndEnabled);
            ScopePlace(guest.transform, ScopeOpenFloor(scope, RoomLayers.IndexOf("Kitchen")), 2f);
            cameraRig.ControlsEnabled = true;
            yield return null;
            yield return null;
            Assert.That(director.WalkerRoomOf(player.transform), Is.EqualTo("Bedroom"), "The player stands in the bedroom.");
            Assert.That(director.WalkerRoomOf(guest.transform), Is.EqualTo("Kitchen"), guest.DisplayName + " stands in the kitchen.");

            // F during the hold: free play's again.
            yield return Rs6aFramedAndLanded(nomination, "Before F");
            Assert.That(cameraRig.ControlsEnabled, Is.True, "No panel is open: the viewer has the camera under the framing.");
            yield return PressKey(Key.F);
            Assert.That(director.IsFramingCeremony, Is.True, "F is pressed while the framing holds its minimum,");
            Assert.That(director.CeremonyFramingHoldsTheCamera, Is.False, "and takes the camera back from it:");
            Assert.That(director.CurrentRoomScopeSource, Is.EqualTo(EpisodeDirector.RoomScopeSource.FreePlay), "the view is free play's,");
            Assert.That(director.ViewedRooms, Is.EqualTo(RoomSet.Of("Bedroom")), "the player's bedroom (RS5), not the framing's room (" + cameraRig.Rooms + "),");
            yield return null;
            Assert.That(view.cullingMask & RoomLayers.Mask, Is.EqualTo(RoomSet.Of("Bedroom").CullingBits), "the bedroom's bit drawn,");
            Rs6aAssertDrawn(player.transform, "The player, recentred on,");
            Rs6aAssertHidden(guest.transform, guest.DisplayName + ", in the kitchen,");
            yield return Rs6aFramingLetGo();
            yield return ScopeWaitUntilLanded();

            // A follow begun under the framing: following's view, not the framing's.
            yield return Rs6aFramedAndLanded(nomination, "Before the follow");
            cameraRig.FocusSubject(guest.transform);
            try
            {
                Assert.That(director.CeremonyFramingHoldsTheCamera, Is.False, "A follow takes the camera from the framing:");
                Assert.That(director.CurrentRoomScopeSource, Is.EqualTo(EpisodeDirector.RoomScopeSource.Following), "the view is following's,");
                Assert.That(director.ViewedRooms, Is.EqualTo(RoomSet.Of("Kitchen")), "the followed houseguest's kitchen (RS5), not the framing's room (" + cameraRig.Rooms + "),");
                yield return null;
                Assert.That(view.cullingMask & RoomLayers.Mask, Is.EqualTo(RoomSet.Of("Kitchen").CullingBits), "the kitchen's bit drawn,");
                Rs6aAssertDrawn(guest.transform, guest.DisplayName + ", followed,");
                Rs6aAssertHidden(player.transform, "The player, in the bedroom, no subject while a houseguest is followed,");
            }
            finally { cameraRig.ClearSubject(); }
            yield return Rs6aFramingLetGo();
            yield return ScopeWaitUntilLanded();
            yield return null;
            // The viewer took the camera: the framing has no move back (RS5), and the stopped follow's kitchen stays watched.
            Assert.That(director.ViewedRooms, Is.EqualTo(RoomSet.Of("Kitchen")),
                "Let go after the viewer took the camera, nothing moves it back: the view is the kitchen the stopped follow showed (" + cameraRig.Rooms + ").");

            // A floor click under the framing - free play following the player - and a walk into the living room
            // during the hold: let go, the view is the living room the player walked into, the player still followed
            // and looked at, not the framing's move back to the room they stood in.
            yield return Rs6aFramedAndLanded(nomination, "Before the floor click");
            cameraRig.FocusSubject(player.transform, false);
            Assert.That(director.CeremonyFramingHoldsTheCamera, Is.False, "A floor click's follow takes the camera from the framing,");
            Assert.That(director.IsFramingCeremony, Is.True, "while the framing still holds its minimum.");
            int livingIndex = RoomLayers.IndexOf("Living");
            ScopePlace(player.transform, ScopeOpenFloor(scope, livingIndex), 2f);
            yield return Rs6aFramingLetGo();
            yield return ScopeWaitUntilLanded();
            yield return RoomViewWaitOutTheLastChange();
            yield return null;
            Assert.That(director.WalkerRoomOf(player.transform), Is.EqualTo("Living"), "The player stands in the living room.");
            Assert.That(cameraRig.FocusedSubject, Is.SameAs(player.transform), "The player is still followed: nothing moved the camera back,");
            Assert.That(cameraRig.ViewRoom, Is.EqualTo(livingIndex), "and the view is the living room they walked into,");
            Assert.That(director.ViewedRooms, Is.EqualTo(RoomSet.Of("Living")), "drawn alone,");
            Assert.That(director.LookingAwayFromThePlayer, Is.False, "and looking at the player.");
            cameraRig.ClearSubject();

            // A shot that takes the camera straight from the framing - a panel's, the overview's - keeps it when the
            // framing lets go, and let go itself, goes back where the framing would have: free play on the player.
            cameraRig.RecenterOnPlayer();
            yield return null;
            yield return RoomViewWaitOutTheLastChange();
            yield return Rs6aFramedAndLanded(nomination, "Before a shot over the framing");
            cameraRig.MoveTo(new HouseCameraRig.Shot
            {
                Focus = ScopeCentre(scope, "Kitchen") + Vector3.up, Distance = 9f, Pitch = 35f, Yaw = 180f, Seconds = .01f, Rooms = RoomSet.Of("Kitchen"),
            });
            try
            {
                Assert.That(director.CeremonyFramingHoldsTheCamera, Is.False, "A shot takes the camera from the framing,");
                yield return Rs6aFramingLetGo();
                Assert.That(cameraRig.HasShot, Is.True, "and keeps it when the framing lets go: nothing drops it,");
                Assert.That(director.ViewedRooms, Is.EqualTo(RoomSet.Of("Kitchen")), "its kitchen drawn.");
            }
            finally { cameraRig.ReleaseShot(0f); }
            yield return null;
            yield return null;
            Assert.That(cameraRig.ViewsThePlayer, Is.True, "Let go, the shot goes back where the framing would have: free play on the player,");
            Assert.That(director.ViewedRooms, Is.EqualTo(RoomSet.Of("Living")), "their living room, not the nomination room it took the camera in.");
        }

        /// <summary>
        /// The veto meeting staged in the living room: from the summons' wide to the meeting's close shot -
        /// the push-in on the red chairs while the holder's decision is pending (CeremonyStageVeto) - every
        /// shot names the living room, so with nothing held the main view is the living room on every frame
        /// the stage holds the camera, its mask's room bits the living room's; inside the close shot's render
        /// only the living room's lamps are on, and every body in the room or its doorway is drawn. Its shot
        /// let go under it at the decision, the stage is not the view: free play's (live from RS5), the rooms
        /// the rig went back to, in that same frame. The card down, the stage lets the house go and the view
        /// is free play's again.
        /// </summary>
        [UnityTest]
        public IEnumerator RoomScope_TheVetoMeetingDrawsItsRoom()
        {
            yield return InstallStagedSeason(71, state => AtVetoMeeting(state, playerHolds: false));
            yield return Rs4BodiesBuilt();
            var scope = ScopeOf(director);
            var homes = LampScopeHomes(scope);
            var authored = homes.Select(home => home.Light.enabled).ToArray();
            var view = cameraRig.ViewCamera;
            Assert.That(view.cullingMask & RoomLayers.Mask, Is.EqualTo(director.ViewedRooms.CullingBits), "Before the meeting the main camera's room bits are the view's.");
            var living = RoomSet.Of(EpisodeDirector.CeremonyStageRoom(CeremonySting.VetoKind));
            Assert.That(living, Is.EqualTo(RoomSet.Of("Living")), "The veto meeting is staged in the living room.");
            Assert.That(director.ViewedRooms.IsWholeHouse, Is.False, "Before it, free play's room view (RS5), not the whole house.");
            var takeover = SceneComponents<CeremonyTakeover>().Single();

            yield return PlayUntilTheVetoMeeting();
            Assert.That(director.IsCeremonyStaged, Is.True, "The veto meeting is staged.");
            Assert.That(director.CeremonyStageKind, Is.EqualTo(CeremonySting.VetoKind));
            Assert.That(director.CurrentRoomScopeSource, Is.EqualTo(EpisodeDirector.RoomScopeSource.Ceremony), "The stage's shots hold the camera.");
            Assert.That(cameraRig.Rooms.Contains("Living"), Is.True, "The summons' wide names the living room: " + cameraRig.Rooms + ".");
            Assert.That(director.ViewedRooms, Is.EqualTo(cameraRig.Rooms), "Live with nothing held: the view is the stage's.");
            // Taken from free play (no framing held the view), the wide draws on its way what the view drew - free
            // play's room view from RS5, not the whole house - with the living room.
            if (cameraRig.IsTravelling && !director.IsFramingCeremony)
                Assert.That(cameraRig.Rooms.IsWholeHouse, Is.False,
                    "On its way the summons' wide draws what the view drew as the stage took it, free play's room view: " + cameraRig.Rooms + ".");
            yield return ScopeWaitUntilLanded();
            yield return null;
            Assert.That(director.ViewedRooms, Is.EqualTo(living), "The wide landed: the living room alone.");
            Assert.That(view.cullingMask & RoomLayers.Mask, Is.EqualTo(living.CullingBits), "The main camera's room bits are the living room's.");
            var sight = Rs6aRenderNow(homes);
            Rs6aAssertLamps(sight, homes, authored, living, "the summons' wide");

            director.SkipCeremonySummons();
            yield return WaitFor(() => takeover.PlayingMeeting, 3f, "the meeting plays once the summons is skipped");
            float openedAt = Time.realtimeSinceStartup;
            float decisionBy = CeremonyPacing.FadeIn + CeremonyPacing.VetoIntro(takeover.MeetingPace) + CeremonyPacing.VetoQuestion(takeover.MeetingPace);
            var failures = new List<string>();
            void Fail(string what) { if (failures.Count < 20) failures.Add(what); }
            var lastFocus = cameraRig.DesiredFocus;
            float lastDistance = cameraRig.DesiredDistance, travelSince = -1f;
            bool pushedIn = false;
            int frames = 0, shots = 0;
            while (takeover.PlayingMeeting && takeover.Page != CeremonyTakeover.MeetingPage.Decision
                && Time.realtimeSinceStartup < openedAt + decisionBy + 3f)
            {
                yield return null;
                if (!director.IsCeremonyStaged) break;
                frames++;
                var focus = cameraRig.DesiredFocus;
                float distance = cameraRig.DesiredDistance;
                bool moved = (focus - lastFocus).sqrMagnitude > 1e-6f || Mathf.Abs(distance - lastDistance) > 1e-4f;
                if (moved) shots++;
                lastFocus = focus; lastDistance = distance;
                // The push-in: the pair shot nearer, on its own clock over the last of the question (0.4 s at
                // the least) - the meeting's one timed move; every other shot is a 0.01 s cut.
                if (!cameraRig.IsTravelling) travelSince = -1f;
                else if (moved || travelSince < 0f) travelSince = Time.realtimeSinceStartup;
                bool pushIn = travelSince >= 0f && Time.realtimeSinceStartup - travelSince >= .15f;
                string at = "frame " + frames + " (" + (Time.realtimeSinceStartup - openedAt).ToString("0.00") + " s into the meeting)";
                if (director.CurrentRoomScopeSource != EpisodeDirector.RoomScopeSource.Ceremony) { Fail(at + ": the view's source is " + director.CurrentRoomScopeSource); continue; }
                if (cameraRig.Rooms != living) Fail(at + ": the stage's shot names " + cameraRig.Rooms);
                if (director.ViewedRooms != living) Fail(at + ": the view is " + director.ViewedRooms);
                if ((view.cullingMask & RoomLayers.Mask) != living.CullingBits)
                    Fail(at + ": the main camera's room bits are " + (view.cullingMask & RoomLayers.Mask).ToString("X"));
                if (!pushIn || pushedIn) continue;
                pushedIn = true;
                sight = Rs6aRenderNow(homes);
                if (sight.Viewed != living) Fail(at + ": the close shot's render draws " + sight.Viewed);
                if (sight.RoomBits != living.CullingBits) Fail(at + ": the close shot's render has room bits " + sight.RoomBits.ToString("X"));
                var expected = Rs6aLampsOn(homes, authored, living);
                if (!sight.Lamps.SequenceEqual(expected)) Fail(at + ": inside the close shot's render the lamps on are not the living room's own");
                Rs6aBodiesFollowTheView(scope, living, "the close shot", failures);
            }
            Assert.That(failures, Is.Empty, string.Join("; ", failures));
            Assert.That(pushedIn, Is.True, "The close shot pushed in on the red chairs while the decision was pending ("
                + shots + " shots over " + frames + " frames).");

            // The stage's shot let go under it - as a panel closed over the stage releases any shot - and the
            // stage is not the view until its next shot: the whole house, read and drawn in this same frame.
            Assert.That(director.IsCeremonyStaged, Is.True, "The meeting is still staged at the decision.");
            Assert.That(director.CurrentRoomScopeSource, Is.EqualTo(EpisodeDirector.RoomScopeSource.Ceremony), "Its shot holds the camera.");
            cameraRig.ReleaseShot(0f);
            Assert.That(cameraRig.HasShot, Is.False, "The stage's shot is let go,");
            Assert.That(director.CurrentRoomScopeSource, Is.Not.EqualTo(EpisodeDirector.RoomScopeSource.Ceremony), "so the stage is not the view,");
            var back = director.ViewedRooms;
            Assert.That(back, Is.EqualTo(cameraRig.Rooms), "which is free play's (live from RS5): the rooms the rig went back to (" + cameraRig.Rooms + "),");
            sight = Rs6aRenderNow(homes);
            Assert.That(sight.Viewed, Is.EqualTo(back), "drawn so in a render by hand:");
            Assert.That(sight.RoomBits, Is.EqualTo(back.CullingBits), "its room bits,");
            Rs6aAssertLamps(sight, homes, authored, back, "the stage's shot let go");

            takeover.Cancel();
            yield return WaitFor(() => !director.IsCeremonyStaged, 3f, "the stage ends with the card");
            yield return WaitFor(() => !director.IsFramingCeremony, EpisodeDirector.CeremonyHoldSeconds + 3f, "nothing frames the room after it");
            yield return Frames(2);
            Assert.That(director.CurrentRoomScopeSource, Is.Not.EqualTo(EpisodeDirector.RoomScopeSource.Ceremony), "The house let go, the stage is not the view.");
            var after = director.ViewedRooms;
            Assert.That(after, Is.EqualTo(cameraRig.Rooms), "Free play's view again (RS5): the rig's rooms,");
            Assert.That(view.cullingMask & RoomLayers.Mask, Is.EqualTo(after.CullingBits), "their bits drawn,");
            sight = Rs6aRenderNow(homes);
            Rs6aAssertLamps(sight, homes, authored, after, "the house let go");
        }

        /// <summary>
        /// A practice competition: the briefing's shot names the yard and is a panel over free play, which
        /// from RS5 draws the rooms the shot holding the camera names: the yard; the attempt's own shot names
        /// the yard (D7), and on its way draws the yard the briefing showed; landed, with nothing held the
        /// main view is the yard alone - its room
        /// bit only in the mask, every lamp off inside the main camera's render (the yard has none), the
        /// player at the station drawn and a houseguest in the games room hidden. Its shot let go under it,
        /// the attempt is not the view: the whole house. Cancelled, the whole house. Under reduced motion
        /// the attempt takes no shot, so it is not the competition's view, a yard shot held over it or not:
        /// the whole house throughout.
        /// </summary>
        [UnityTest]
        public IEnumerator RoomScope_TheCompetitionDrawsTheYard()
        {
            AskForTheStages(false);
            yield return null;
            yield return Rs4BodiesBuilt();
            var scope = ScopeOf(director);
            var homes = LampScopeHomes(scope);
            var authored = homes.Select(home => home.Light.enabled).ToArray();
            var view = cameraRig.ViewCamera;
            var yard = EpisodeDirector.CompetitionRooms;
            Assert.That(yard, Is.EqualTo(RoomSet.Of("Yard")), "The competition's shots name the yard.");
            Assert.That(view.cullingMask & RoomLayers.Mask, Is.EqualTo(director.ViewedRooms.CullingBits), "Before the competition the main camera's room bits are the view's.");

            WarpPlayer(director.StationPosition);
            Assert.That(director.TryOpenPhasePanel(), Is.True);
            ButtonWithCaption("Begin the next competition").onClick.Invoke();
            yield return null;
            // No ceremony's framing over the briefing: its source would come first.
            yield return WaitFor(() => !director.IsFramingCeremony, EpisodeDirector.CeremonyHoldSeconds + 3f, "no ceremony is framed");
            WarpPlayer(director.StationPosition);
            Assert.That(director.TryOpenPhasePanel(), Is.True);
            yield return null;
            Assert.That(director.IsFramingBriefing, Is.True, "The briefing frames the arena.");
            yield return ScopeWaitUntilLanded();
            Assert.That(cameraRig.Rooms, Is.EqualTo(yard), "Its shot names the yard,");
            Assert.That(director.CurrentRoomScopeSource, Is.EqualTo(EpisodeDirector.RoomScopeSource.FreePlay),
                "and the briefing is a panel over free play,");
            Assert.That(director.ViewedRooms, Is.EqualTo(yard), "which from RS5 draws the rooms the shot holding the camera names: the yard.");

            var far = SceneComponents<HouseNpc>().First(npc => npc.isActiveAndEnabled);
            int games = RoomLayers.IndexOf("Games");
            var before = director.Snapshot;
            ButtonWithCaption("Practice this competition").onClick.Invoke();
            Assert.That(director.IsFramingCompetition, Is.True, "The attempt takes its own shot.");
            Assert.That(director.CurrentRoomScopeSource, Is.EqualTo(EpisodeDirector.RoomScopeSource.Competition), "The competition's shot is the view.");
            Assert.That(cameraRig.IsTravelling, Is.True, "Its shot moves on its clock from the briefing's.");
            // On its way it draws what the view drew as it left - the briefing's yard - with the yard.
            Assert.That(cameraRig.Rooms, Is.EqualTo(yard),
                "The move leaves the briefing's yard, and draws it until it lands: " + cameraRig.Rooms + ".");
            Assert.That(director.ViewedRooms, Is.EqualTo(yard), "Nothing the viewer saw goes before the camera has moved.");
            var sight = Rs6aRenderNow(homes);
            Assert.That(sight.Viewed, Is.EqualTo(yard), "The render on the move's own frame draws the yard,");
            Rs6aAssertLamps(sight, homes, authored, yard, "on the attempt's way");
            yield return ScopeWaitUntilLanded();
            // A houseguest in the games room, well clear of the yard and its doorway band, put there once the
            // shot has landed (the field walks to its stations meanwhile), and two frames for its walker room.
            ScopePlace(far.transform, ScopeOpenFloor(scope, games), 2f);
            yield return null;
            yield return null;
            Assert.That(director.IsFramingCompetition, Is.True, "Landed, the attempt's shot holds the camera.");
            Assert.That(cameraRig.Rooms, Is.EqualTo(yard), "It names the yard, in the call.");
            Assert.That(director.ViewedRooms, Is.EqualTo(yard), "Live with nothing held: the main view is the yard alone (D7).");
            Assert.That(view.cullingMask & RoomLayers.Mask, Is.EqualTo(yard.CullingBits), "The late pass keeps the yard's bit alone.");
            sight = Rs6aRenderNow(homes);
            Assert.That(sight.Viewed, Is.EqualTo(yard));
            Assert.That(sight.RoomBits, Is.EqualTo(yard.CullingBits), "The main camera's room bits are the yard's.");
            Rs6aAssertLamps(sight, homes, authored, yard, "the competition");
            Assert.That(sight.Lamps.Any(on => on), Is.False, "The yard has no lamp: every lamp is off for the main camera.");
            Rs6aAssertDrawn(player.transform, "The player, at the station in the yard,");
            Assert.That(director.WalkerRoomOf(far.transform), Is.EqualTo("Games"), far.DisplayName + " is in the games room.");
            Rs6aAssertHidden(far.transform, far.DisplayName + ", in the games room,");
            var failures = new List<string>();
            Rs6aBodiesFollowTheView(scope, yard, "the competition", failures);
            Assert.That(failures, Is.Empty, string.Join("; ", failures));
            LampScopeAsAuthored(homes.Select(home => home.Light).ToArray(), authored, "between frames", failures);
            Assert.That(failures, Is.Empty, string.Join("; ", failures));

            // The attempt's shot let go under it - input lets a shot go, and so does a panel closed over it - and the
            // competition is not the view: the whole house, read and drawn in this same frame.
            cameraRig.ReleaseShot(0f);
            Assert.That(NpcRead<bool>("challengeActive"), Is.True, "The attempt is still on,");
            Assert.That(director.IsFramingCompetition, Is.False, "but its shot no longer holds the camera,");
            Assert.That(director.CurrentRoomScopeSource, Is.Not.EqualTo(EpisodeDirector.RoomScopeSource.Competition), "so it is not the view,");
            var back = director.ViewedRooms;
            Assert.That(back, Is.EqualTo(cameraRig.Rooms), "which is free play's (live from RS5): the rooms the rig went back to (" + cameraRig.Rooms + "),");
            sight = Rs6aRenderNow(homes);
            Assert.That(sight.Viewed, Is.EqualTo(back), "drawn so in a render by hand:");
            Assert.That(sight.RoomBits, Is.EqualTo(back.CullingBits), "its room bits,");
            Rs6aAssertLamps(sight, homes, authored, back, "the attempt's shot let go");

            yield return PressKey(Key.Escape);
            yield return WaitFor(() => !NpcRead<bool>("challengeActive"), 3f, "Escape cancels the practice");
            yield return null;
            Assert.That(director.IsFramingCompetition, Is.False, "Cancelled, the competition's shot is let go:");
            Assert.That(director.ViewedRooms, Is.EqualTo(cameraRig.Rooms), "free play's view (RS5), the rig's rooms,");
            Assert.That(view.cullingMask & RoomLayers.Mask, Is.EqualTo(director.ViewedRooms.CullingBits), "their bits drawn.");
            Assert.That(director.Snapshot.revision, Is.EqualTo(before.revision), "A practice commits nothing.");

            // Under reduced motion the attempt keeps the viewer's shot: not the competition's view.
            director.ClosePanels();
            yield return null;
            AskForTheStages(true);
            Assert.That(cameraRig.ReducedMotion, Is.True, "Reduced motion is on for the rig before the attempt.");
            WarpPlayer(director.StationPosition);
            Assert.That(director.TryOpenPhasePanel(), Is.True);
            yield return null;
            ButtonWithCaption("Practice this competition").onClick.Invoke();
            Assert.That(NpcRead<bool>("challengeActive"), Is.True, "The attempt is on under reduced motion.");
            Assert.That(director.IsFramingCompetition, Is.False, "It takes no shot,");
            Assert.That(director.CurrentRoomScopeSource, Is.Not.EqualTo(EpisodeDirector.RoomScopeSource.Competition), "so it is not the competition's view,");
            Assert.That(director.ViewedRooms, Is.EqualTo(cameraRig.Rooms), "and the view is the viewer's own (free play's, RS5): the rig's rooms.");
            // A shot naming the yard held over the attempt is not the attempt's own: the competition is the view only
            // once its own shot is taken.
            cameraRig.MoveTo(new HouseCameraRig.Shot
            {
                Focus = director.StationPosition + Vector3.up, Distance = 12f, Pitch = 30f, Yaw = 0f, FieldOfView = 45f, Seconds = .01f, Rooms = yard,
            });
            try
            {
                Assert.That(cameraRig.HasShot, Is.True, "A test's shot naming the yard holds the camera,");
                Assert.That(director.IsFramingCompetition, Is.False, "but the attempt took no shot of its own,");
                Assert.That(director.CurrentRoomScopeSource, Is.EqualTo(EpisodeDirector.RoomScopeSource.FreePlay), "so the competition is not the view: free play's is,");
                Assert.That(director.ViewedRooms, Is.EqualTo(yard), "which draws the rooms the test's shot names (RS5): the yard.");
                yield return null;
                Assert.That(view.cullingMask & RoomLayers.Mask, Is.EqualTo(yard.CullingBits), "The yard's bit,");
                sight = Rs6aRenderNow(homes);
                Rs6aAssertLamps(sight, homes, authored, yard, "a test's yard shot over the attempt");
            }
            finally { cameraRig.ReleaseShot(0f); }
            yield return PressKey(Key.Escape);
            yield return WaitFor(() => !NpcRead<bool>("challengeActive"), 3f, "Escape cancels the practice under reduced motion");
            Assert.That(director.Snapshot.revision, Is.EqualTo(before.revision), "Practice under reduced motion commits nothing either.");
        }

        /// <summary>
        /// R19's guard (ROOM-SCOPE-PLAN §6.1), for the first low shots RS6a makes live: a houseguest the
        /// view hides, standing between a low shot's eye and its focus, leaves the boom whole, as a drawn
        /// one does - the rig's pull-in passes over every body (HouseCameraRig.IsTracked) - while a box in
        /// the same place pulls it in, so the line tested is the boom's.
        /// </summary>
        [UnityTest]
        public IEnumerator RoomScope_AHiddenBodyNeverPullsTheBoomIn()
        {
            director.ClosePanels();
            director.SuspendNpcAutonomyForDiagnostics();
            yield return null;
            yield return Rs4BodiesBuilt();
            var scope = ScopeOf(director);
            ScopeParkHouseguests(scope);
            // The player in the yard, out of Talk's reach of the body under the boom.
            ScopePlace(player.transform, ScopeOpenFloor(scope, RoomLayers.IndexOf("Yard")), 2f);
            // A houseguest standing, its root on a layer the boom's cast sees (a seated one's is parked on the furniture layer).
            var guest = SceneComponents<HouseNpc>().First(npc => npc.isActiveAndEnabled && (HouseLayers.Sight & (1 << npc.gameObject.layer)) != 0);
            var parked = guest.transform.position;
            var owner = new object();
            GameObject box = null;
            var tried = new List<string>();
            bool found = false;
            director.HoldRoomScope(owner);
            try
            {
                foreach (var room in new[] { "Living", "Kitchen", "Bedroom", "Private", "HoH", "Nomination", "Games" })
                {
                    foreach (float yaw in new[] { 0f, 90f, 180f, 270f })
                    {
                        // A low shot over the room's middle, naming a room well away from it: whoever stands
                        // under its boom is outside the view and its doorway bands.
                        var centre = ScopeCentre(scope, room);
                        var focus = new Vector3(centre.x, 1f, centre.z);
                        int elsewhere = Rs4FarRoom(scope, focus);
                        var shot = new HouseCameraRig.Shot
                        {
                            Focus = focus, Distance = 4.8f, Pitch = 10f, Yaw = yaw, FieldOfView = 40f, Seconds = .01f, Rooms = RoomSet.Of(elsewhere),
                        };
                        string where = room + " at yaw " + yaw;
                        cameraRig.MoveTo(shot);
                        yield return Rs6aBoomSettled();
                        float clear = cameraRig.AppliedDistance;
                        if (clear < shot.Distance - .05f) { tried.Add(where + ": the boom is " + clear.ToString("0.00") + " with nobody there"); continue; }
                        var back = Quaternion.Euler(shot.Pitch, shot.Yaw, 0f) * Vector3.back;
                        var under = focus + back * 3.4f;
                        if (!NavMesh.SamplePosition(new Vector3(under.x, 0f, under.z), out var floor, .2f, player.Agent.areaMask))
                        { tried.Add(where + ": no floor under the boom"); continue; }
                        ScopePlace(guest.transform, floor.position, .3f);
                        yield return null;
                        yield return null;
                        if (!Rs6aOnTheBoom(focus, back, shot.Distance, guest.transform))
                        { tried.Add(where + ": the body does not stand in the boom's way"); ScopePlace(guest.transform, parked, 2f); continue; }
                        found = true;
                        Assert.That(director.ViewedRooms, Is.EqualTo(RoomSet.Of(elsewhere)), where + ": the held view is the room the shot names.");
                        Rs6aAssertHidden(guest.transform, where + ": " + guest.DisplayName + ", under the boom,");
                        yield return Rs6aBoomSettled();
                        Assert.That(cameraRig.AppliedDistance, Is.EqualTo(clear).Within(.02f),
                            where + ": a hidden body between the eye and the focus leaves the boom whole.");
                        // Drawn, the same: the whole house held over the view.
                        director.HoldWholeHouse(owner);
                        yield return Rs6aBoomSettled();
                        Assert.That(director.IsBodyDrawn(guest.transform), Is.True, where + ": with the whole house held the body is drawn,");
                        Assert.That(cameraRig.AppliedDistance, Is.EqualTo(clear).Within(.02f), where + ": and the boom is whole still.");
                        director.Release(owner);
                        director.HoldRoomScope(owner);
                        // A box where the body stood pulls the boom in: the line is the boom's.
                        ScopePlace(guest.transform, parked, 2f);
                        box = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        box.name = "R19 boom box";
                        SceneManager.MoveGameObjectToScene(box, director.gameObject.scene);
                        box.transform.position = floor.position + Vector3.up * .95f;
                        box.transform.localScale = new Vector3(.5f, 1.9f, .5f);
                        Physics.SyncTransforms();
                        yield return Rs6aBoomSettled();
                        Assert.That(cameraRig.AppliedDistance, Is.LessThan(clear - .5f),
                            where + ": a box where the body stood pulls the boom in (" + cameraRig.AppliedDistance.ToString("0.00") + " of " + clear.ToString("0.00") + ").");
                        break;
                    }
                    if (found) break;
                }
                Assert.That(found, Is.True, "A low shot whose boom is whole with a body under it: " + string.Join("; ", tried));
            }
            finally
            {
                director.Release(owner);
                cameraRig.ReleaseShot(0f);
                if (box != null) Object.Destroy(box);
            }
        }

        // ------------------------------------------------------------------------------------
        // Helpers, shared with the ceremony and competition tests RS6a changes.
        // ------------------------------------------------------------------------------------

        /// <summary>The nomination framed and landed, its move still holding the camera: its room alone the view, the player in the bedroom hidden.</summary>
        private IEnumerator Rs6aFramedAndLanded(RoomSet room, string what)
        {
            director.FrameCeremony(CeremonySting.NominationKind);
            yield return ScopeWaitUntilLanded();
            yield return null;
            Assert.That(director.CeremonyFramingHoldsTheCamera, Is.True, what + ": the framing's move holds the camera, landed,");
            Assert.That(director.CurrentRoomScopeSource, Is.EqualTo(EpisodeDirector.RoomScopeSource.Ceremony), what + ": the framing is the view,");
            Assert.That(director.ViewedRooms, Is.EqualTo(room), what + ": its room alone,");
            Rs6aAssertHidden(player.transform, what + ": the player, in the bedroom,");
        }

        /// <summary>The framing let go, with no card playing: its minimum held, then two frames for the late pass.</summary>
        private IEnumerator Rs6aFramingLetGo()
        {
            float deadline = Time.realtimeSinceStartup + EpisodeDirector.CeremonyHoldSeconds + 3f;
            while (director.IsFramingCeremony && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(director.IsFramingCeremony, Is.False, "With no card playing, the framing holds its minimum and lets go.");
            yield return null;
            yield return null;
        }

        /// <summary>The main camera rendered once by hand, now, and read inside its begin callback, after the director's hook.</summary>
        private Rs6aSight Rs6aRenderNow(List<HouseRoomScope.LampHome> homes)
        {
            var view = cameraRig.ViewCamera;
            var sight = new Rs6aSight();
            void Look(ScriptableRenderContext context, Camera camera)
            {
                if (camera != view || sight.Renders > 0) return;
                sight.Renders++;
                sight.Viewed = director.ViewedRooms;
                sight.RoomBits = camera.cullingMask & RoomLayers.Mask;
                sight.LampsOff = director.LampsOffForTheMainCamera;
                sight.Lamps = homes.Select(home => home.Light.enabled).ToArray();
            }
            RenderPipelineManager.beginCameraRendering += Look;
            try { LampScopeRenderByHand(view); }
            finally { RenderPipelineManager.beginCameraRendering -= Look; }
            Assert.That(sight.Renders, Is.EqualTo(1), "The main camera's render by hand reached its begin callback.");
            return sight;
        }

        /// <summary>Which lamps the main camera renders with for a view: those homed in its rooms that are on as authored.</summary>
        private static bool[] Rs6aLampsOn(List<HouseRoomScope.LampHome> homes, bool[] authored, RoomSet viewed)
            => homes.Select((home, i) => authored[i] && (viewed.IsWholeHouse || viewed.Contains(home.Room))).ToArray();

        private static void Rs6aAssertLamps(Rs6aSight sight, List<HouseRoomScope.LampHome> homes, bool[] authored, RoomSet viewed, string what)
        {
            var expected = Rs6aLampsOn(homes, authored, viewed);
            Assert.That(sight.Lamps, Is.EqualTo(expected), what + ": inside the main camera's render the lamps homed in " + viewed + " are on, and only they.");
            Assert.That(sight.LampsOff, Is.EqualTo(authored.Where((on, i) => on && !expected[i]).Count()), what + ": the hook switched off every other lamp that was on.");
        }

        /// <summary>The body drawn by the scope and by the main camera: none of the renderers it has drawing left out by the mask.</summary>
        private void Rs6aAssertDrawn(Transform body, string what)
        {
            Rs4Read(body, out _, out int outOfMask, out string layers);
            Assert.That(director.IsBodyDrawn(body), Is.True, what + " is drawn by the scope.");
            Assert.That(outOfMask, Is.Zero, what + " is drawn by the main camera: " + layers + ".");
        }

        /// <summary>The body hidden by the scope and by the main camera: every renderer it has drawing left out by the mask.</summary>
        private void Rs6aAssertHidden(Transform body, string what)
        {
            Assert.That(director.IsBodyDrawn(body), Is.False, what + " is hidden by the scope.");
            Assert.That(Rs4HiddenNow(body, out string layers), Is.True, what + " is left out by the main camera: " + layers + ".");
        }

        /// <summary>
        /// Every body against the view: one standing in a viewed room or its doorway band is drawn, none of
        /// its renderers left out by the mask; one the scope hides (outside both, no subject, no prompt's)
        /// has every renderer it has drawing left out. The drawing is the last late pass's, a frame behind
        /// the feet read here, so a body walking within a frame's stride of where the rule's answer turns
        /// (a band's edge) is not judged by where it stands now; a body standing still always is.
        /// </summary>
        private void Rs6aBodiesFollowTheView(HouseRoomScope scope, RoomSet viewed, string what, List<string> failures)
        {
            var bodies = SceneComponents<HouseNpc>().Where(npc => npc.isActiveAndEnabled).Select(npc => npc.transform)
                .Concat(new[] { player.transform });
            foreach (var body in bodies)
            {
                int walker = RoomLayers.IndexOf(director.WalkerRoomOf(body));
                Rs4Read(body, out int drawing, out int outOfMask, out string layers);
                if (scope.DrawsWalker(viewed, walker, body.position))
                {
                    if ((!director.IsBodyDrawn(body) || outOfMask > 0) && !Rs6aWalkingAcrossTheRule(scope, viewed, walker, body))
                        failures.Add(what + ": " + body.name + " stands in the " + viewed + " or its doorway and is not drawn (" + layers + ")");
                }
                else if (!director.IsBodyDrawn(body) && outOfMask != drawing)
                    failures.Add(what + ": " + body.name + " is hidden by the scope and drawn by the camera (" + layers + ")");
            }
        }

        /// <summary>
        /// Whether a body is walking, and the drawing rule's answer turns within a frame's stride of its feet
        /// (0.15 m, and the stride at its speed over twice this frame's time): the late pass judged it where
        /// it stood a frame ago, on the other side of the line, as frames stretch on a loaded lane.
        /// </summary>
        private static bool Rs6aWalkingAcrossTheRule(HouseRoomScope scope, RoomSet viewed, int walker, Transform body)
        {
            var agent = body.GetComponent<NavMeshAgent>();
            float speed = agent != null && agent.isActiveAndEnabled ? agent.velocity.magnitude : 0f;
            if (speed < .05f) return false;
            float reach = .15f + speed * Mathf.Max(Time.unscaledDeltaTime, Time.deltaTime) * 2f;
            var feet = body.position;
            bool here = scope.DrawsWalker(viewed, walker, feet);
            foreach (var step in new[] { Vector3.right, Vector3.left, Vector3.forward, Vector3.back })
                if (scope.DrawsWalker(viewed, walker, feet + step * reach) != here) return true;
            return false;
        }

        /// <summary>
        /// The view the stage's shots hold (RS6a): a ceremony is staged, its shots name its room, and with
        /// nothing held the main camera draws that room alone among the rooms, its mask's room bits the
        /// room's. For a frame a test takes of a staged ceremony, inside the capture.
        /// </summary>
        private void Rs6aAssertTheStageDrawsItsRoom(string what)
        {
            Assert.That(director.IsCeremonyStaged, Is.True, what + ": a ceremony is staged.");
            var room = RoomSet.Of(EpisodeDirector.CeremonyStageRoom(director.CeremonyStageKind));
            Assert.That(director.CurrentRoomScopeSource, Is.EqualTo(EpisodeDirector.RoomScopeSource.Ceremony), what + ": the stage's shots hold the camera.");
            Assert.That(cameraRig.Rooms, Is.EqualTo(room), what + ": the shot names the " + room + ".");
            Rs6aAssertTheViewIs(room, what);
        }

        /// <summary>With nothing held, the main view is these rooms, and the main camera's room bits are theirs.</summary>
        private void Rs6aAssertTheViewIs(RoomSet rooms, string what)
        {
            Assert.That(director.ViewedRooms, Is.EqualTo(rooms), what + ": the main camera draws the " + rooms + " alone, with nothing held.");
            Assert.That(cameraRig.ViewCamera.cullingMask & RoomLayers.Mask, Is.EqualTo(rooms.CullingBits),
                what + ": the main camera's room bits are the " + rooms + "'s.");
        }

        /// <summary>
        /// The gathered house all drawn (R10: at a ceremony everyone is in the room): the stage's room is
        /// the view, nobody is hidden, and no body has a renderer the mask leaves out.
        /// </summary>
        private void Rs6aAssertTheGatheredHouseIsDrawn(string what)
        {
            Rs6aAssertTheStageDrawsItsRoom(what);
            Assert.That(director.HiddenBodyCount, Is.Zero, what + ": nobody is hidden, the house gathered in the room.");
            foreach (var body in SceneComponents<HouseNpc>().Where(npc => npc.isActiveAndEnabled).Select(npc => npc.transform).Concat(new[] { player.transform }))
            {
                Rs4Read(body, out _, out int outOfMask, out string layers);
                Assert.That(outOfMask, Is.Zero, what + ": " + body.name + " is drawn by the main camera (" + layers + ").");
            }
        }

        /// <summary>
        /// The view as a ceremony's card plays (RS6a): while its framing or its stage holds the camera, it
        /// draws the ceremony's room - with what the view drew as the framing's move left, while it travels
        /// - and otherwise the view's own: free play's or following's rooms (RS5), another live shot's, or the
        /// whole house. True when the ceremony's shot held the view.
        /// </summary>
        private bool Rs6aAssertACardsViewDrawsItsRoom(string kind)
        {
            var room = RoomSet.Of(EpisodeDirector.CeremonyRoom(kind));
            Assert.That(room.IsEmpty, Is.False, kind + " is framed in a room of the house.");
            var source = director.CurrentRoomScopeSource;
            var viewed = director.ViewedRooms;
            Debug.Log("[Gamesim] RS6a: the " + kind + " card's frame draws " + viewed + " (" + source + ").");
            if (source != EpisodeDirector.RoomScopeSource.Ceremony)
            {
                // Free play's or following's view (live from RS5) draws the rig's rooms: the player's or a watched
                // room, the followed houseguest's, a shot's. Another live shot holding the view over the card (RS6b:
                // a house event's, a two-shot) draws its own rooms: that source's own hold is what makes it the view,
                // and the frame draws the rooms its shot names, never none. Any other view is the whole house.
                if (source == EpisodeDirector.RoomScopeSource.FreePlay || source == EpisodeDirector.RoomScopeSource.Following)
                {
                    Assert.That(viewed.IsEmpty, Is.False, kind + ": the " + source + " view over the card draws rooms.");
                    Assert.That(viewed, Is.EqualTo(cameraRig.Rooms), kind + ": the " + source + " view over the card draws the rig's rooms (" + cameraRig.Rooms + ").");
                }
                else if (EpisodeDirector.IsRoomScopeSourceLive(source))
                {
                    bool holds = source == EpisodeDirector.RoomScopeSource.HouseEvent ? director.HouseEventShotHoldsTheCamera
                        : source == EpisodeDirector.RoomScopeSource.Conversation ? cameraRig.HoldsTheTwoShot
                        : source == EpisodeDirector.RoomScopeSource.Diary ? director.DiaryShotHoldsTheCamera
                        : source == EpisodeDirector.RoomScopeSource.Activity ? director.ActivityWarpHoldsTheCamera
                        : source == EpisodeDirector.RoomScopeSource.Watching ? director.WatchHoldsTheCamera
                        : source == EpisodeDirector.RoomScopeSource.Competition && director.IsFramingCompetition;
                    Assert.That(holds, Is.True, kind + ": the " + source + " view over the card is its own shot's, held.");
                    Assert.That(viewed.IsEmpty, Is.False, kind + ": the " + source + "'s shot over the card draws rooms.");
                    Assert.That(viewed, Is.EqualTo(cameraRig.Rooms), kind + ": the " + source + "'s shot over the card draws the rooms it names (" + cameraRig.Rooms + ").");
                }
                else Assert.That(viewed.IsWholeHouse, Is.True, kind + ": the " + source + " view is the whole house.");
                return false;
            }
            Assert.That(viewed.With(room), Is.EqualTo(viewed), kind + ": the ceremony's view draws the " + room + " (" + viewed + ").");
            if (!cameraRig.IsTravelling) Assert.That(viewed, Is.EqualTo(room), kind + ": landed, the " + room + " alone.");
            return true;
        }

        /// <summary>
        /// The view a competition's shot holds (RS6a, D7): the yard alone, its room bit alone in the mask,
        /// every body standing in the yard drawn, the player among them, and with <paramref name="everyone"/>
        /// nobody hidden at all - the whole field at its stations.
        /// </summary>
        private void Rs6aAssertTheCompetitionDrawsTheYard(string what, bool everyone)
        {
            Assert.That(director.IsFramingCompetition, Is.True, what + ": the competition's shot holds the camera.");
            Assert.That(director.CurrentRoomScopeSource, Is.EqualTo(EpisodeDirector.RoomScopeSource.Competition), what + ": the competition is the view.");
            Rs6aAssertTheViewIs(EpisodeDirector.CompetitionRooms, what);
            var failures = new List<string>();
            Rs6aBodiesFollowTheView(director.RoomScope, EpisodeDirector.CompetitionRooms, what, failures);
            Assert.That(failures, Is.Empty, string.Join("; ", failures));
            Rs6aAssertDrawn(player.transform, what + ": the player");
            if (everyone) Assert.That(director.HiddenBodyCount, Is.Zero, what + ": nobody is hidden, the field at its stations.");
        }

        /// <summary>The boom at rest: a timed shot landed and the boom at its length, two frames on.</summary>
        private IEnumerator Rs6aBoomSettled()
        {
            float deadline = Time.realtimeSinceStartup + 3f;
            yield return null;
            while ((cameraRig.IsTravelling || Mathf.Abs(cameraRig.Distance - cameraRig.DesiredDistance) > .01f) && Time.realtimeSinceStartup < deadline)
                yield return null;
            yield return null;
            yield return null;
        }

        /// <summary>Whether a body's collider stands in the boom's way: the rig's own cast (a 0.18 sphere from 0.4 out) meets it.</summary>
        private static bool Rs6aOnTheBoom(Vector3 focus, Vector3 back, float distance, Transform body)
        {
            Physics.SyncTransforms();
            var hits = Physics.SphereCastAll(focus + back * .4f, .18f, back, distance - .4f, HouseLayers.Sight, QueryTriggerInteraction.Ignore);
            return hits.Any(hit => hit.collider != null && hit.collider.transform.IsChildOf(body));
        }
    }
}
