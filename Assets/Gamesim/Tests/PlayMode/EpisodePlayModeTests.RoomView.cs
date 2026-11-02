using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Gamesim.Episode;
using Gamesim.House;
using Gamesim.Persistence;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// The room view (ROOM-SCOPE-PLAN §3.2 g to j, RS5): free play and following are live, so with nothing
    /// held the free camera stays over one room - the player's, the followed houseguest's, a watched one -
    /// with the room's own farthest zoom and the focus clamped inside it, narrowing with the zoom, and every
    /// change of room is a cut onto the new room's framing (D4, D5). The nearest zoom is four metres (D11).
    /// The travel icons find the way at every room-view distance (D17); a view away from the player's room
    /// is not looking (D18); "Show the whole house" brings back today's view and is kept (D12).
    ///
    /// <para>The owner (2026-11-01) judges the framing in play: these tests pin the rules - the rooms the view
    /// draws, the mask, the lamps and the bodies in each, the clamp, the zoom limits and the cuts - and take
    /// no captures. Waits that the room view's clocks set (the 0.6 s a walk's change of room waits) are on
    /// the real clock.</para>
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        // ------------------------------------------------------------------------------------
        // Helpers, shared with the tests RS5 changes.
        // ------------------------------------------------------------------------------------

        /// <summary>The owner of the whole-house hold a dollhouse test takes: today's view, drawing and free camera (as D12's setting).</summary>
        private static readonly object RoomViewDollhouse = new object();

        /// <summary>
        /// Holds the whole house for a test about the dollhouse (RS5): the room view stands down, so the
        /// free camera is today's, 4 to 34 m over the whole house, and everything is drawn. The fixture's
        /// scene goes with the test, and the hold with its director; a test that needs the room view after
        /// lets go with <see cref="RoomViewLetGoOfTheDollhouse"/>.
        /// </summary>
        private void RoomViewHoldTheDollhouse() => director.HoldWholeHouse(RoomViewDollhouse);

        private IEnumerator RoomViewLetGoOfTheDollhouse()
        {
            director.Release(RoomViewDollhouse);
            yield return null;
            yield return null;
        }

        /// <summary>Waits out, on the real clock, the 0.6 s a walk's change of room waits since the view's last change.</summary>
        private static IEnumerator RoomViewWaitOutTheLastChange()
        {
            yield return new WaitForSecondsRealtime(HouseRoomView.RoomChangeSeconds + .1f);
        }

        /// <summary>The player stood on a room's open floor and the view back on them (F), the last change waited out, the camera settled: free play's room view.</summary>
        private IEnumerator RoomViewPlayerIn(HouseRoomScope scope, int room)
        {
            ScopePlace(player.transform, ScopeOpenFloor(scope, room), 2f);
            cameraRig.RecenterOnPlayer();
            yield return null;
            yield return RoomViewWaitOutTheLastChange();
            yield return SettleCamera();
            Assert.That(cameraRig.IsRoomView, Is.True, "Free play over the " + RoomLayers.RoomIds[room] + " is the room view (" + director.CurrentRoomScopeSource + ").");
            Assert.That(cameraRig.ViewRoom, Is.EqualTo(room), "and it frames the room the player stands in.");
        }

        /// <summary>The rig's private fields, for the clamp's own cases.</summary>
        private void RoomViewSetDesired(Vector3 focus, float distance)
        {
            var type = typeof(HouseCameraRig);
            const BindingFlags any = BindingFlags.Instance | BindingFlags.NonPublic;
            type.GetField("desiredFocus", any).SetValue(cameraRig, focus);
            type.GetField("desiredDistance", any).SetValue(cameraRig, distance);
        }

        private void RoomViewSetYaw(float yaw)
            => typeof(HouseCameraRig).GetField("yaw", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(cameraRig, yaw);

        /// <summary>With nothing held, the view is one room: the rig's, the director's and the main camera's room bits.</summary>
        private void RoomViewAssertTheView(int room, string what)
        {
            Assert.That(cameraRig.ViewRoom, Is.EqualTo(room), what + ": the room view frames the " + RoomLayers.RoomIds[room] + ".");
            Rs6aAssertTheViewIs(RoomSet.Of(room), what);
        }

        // ------------------------------------------------------------------------------------
        // The free camera.
        // ------------------------------------------------------------------------------------

        /// <summary>
        /// Free play's view is the player's room: drawn alone among the rooms, its lamps alone on inside the
        /// main camera's render, a houseguest beyond its walls hidden. The free camera stays over it: at the
        /// room's farthest zoom the focus is its centre and a pan does not move it; closer in the focus may
        /// stray towards the walls, all the way at the nearest zoom, and never out of the room; the player
        /// followed to the wall is followed to the wall at the nearest zoom.
        /// </summary>
        [UnityTest]
        public IEnumerator RoomView_TheFreeCameraStaysOverItsRoom()
        {
            director.ClosePanels();
            director.SuspendNpcAutonomyForDiagnostics();
            yield return null;
            yield return Rs4BodiesBuilt();
            var scope = ScopeOf(director);
            var homes = LampScopeHomes(scope);
            var authored = homes.Select(home => home.Light.enabled).ToArray();
            int living = RoomLayers.IndexOf("Living"), kitchen = RoomLayers.IndexOf("Kitchen");
            ScopeParkHouseguests(scope);
            var guests = SceneComponents<HouseNpc>().Where(npc => npc.isActiveAndEnabled).ToArray();
            Assert.That(guests.Length, Is.GreaterThanOrEqualTo(2), "Two houseguests to place.");
            var near = guests[0];
            var far = guests[1];
            yield return RoomViewPlayerIn(scope, living);
            ScopePlace(near.transform, ScopeOpenFloor(scope, living) + new Vector3(1.5f, 0f, 0f), 2f);
            ScopePlace(far.transform, ScopeOpenFloor(scope, kitchen), 2f);
            yield return null;
            yield return null;

            Assert.That(director.CurrentRoomScopeSource, Is.EqualTo(EpisodeDirector.RoomScopeSource.FreePlay), "Free play.");
            Assert.That(EpisodeDirector.IsRoomScopeSourceLive(EpisodeDirector.RoomScopeSource.FreePlay), Is.True, "Free play is live from RS5.");
            RoomViewAssertTheView(living, "Free play in the living room");
            Rs6aAssertDrawn(player.transform, "The player");
            Rs6aAssertDrawn(near.transform, near.DisplayName + ", in the living room");
            Rs6aAssertHidden(far.transform, far.DisplayName + ", in the kitchen");
            Rs6aAssertLamps(Rs6aRenderNow(homes), homes, authored, RoomSet.Of(living), "Free play in the living room");

            var box = scope.Box(living);
            float farthest = HouseRoomView.FarthestDistance(living);
            Assert.That(cameraRig.FreeFarthestDistance, Is.EqualTo(farthest), "The living room's farthest zoom (D4).");
            Assert.That(cameraRig.DesiredDistance, Is.LessThanOrEqualTo(farthest + .001f), "The view comes on no farther out than the room's farthest.");

            // The clamp, at three distances, from a focus far outside the house.
            cameraRig.ClearSubject();
            foreach (float distance in new[] { farthest, (farthest + HouseRoomView.NearestDistance) * .5f, HouseRoomView.NearestDistance })
            {
                var outside = new Vector3(box.xMax + 40f, cameraRig.DesiredFocus.y, box.yMax + 40f);
                RoomViewSetDesired(outside, distance);
                yield return null;
                var expected = HouseRoomView.Clamp(box, outside, distance, cameraRig.NearestDistance, farthest);
                Assert.That(Vector2.Distance(new Vector2(cameraRig.DesiredFocus.x, cameraRig.DesiredFocus.z), new Vector2(expected.x, expected.z)), Is.LessThan(.01f),
                    "At " + distance + " m the focus is kept at " + expected.ToString("F2") + ", not " + cameraRig.DesiredFocus.ToString("F2") + ".");
                Assert.That(box.Contains(new Vector2(cameraRig.DesiredFocus.x, cameraRig.DesiredFocus.z)) || Mathf.Approximately(cameraRig.DesiredFocus.x, box.xMax)
                    || Mathf.Approximately(cameraRig.DesiredFocus.z, box.yMax), Is.True, "and inside the room.");
                Assert.That(director.ViewedRooms, Is.EqualTo(RoomSet.Of(living)), "The view never leaves its room.");
            }
            RoomViewSetDesired(new Vector3(box.center.x - 30f, cameraRig.DesiredFocus.y, box.center.y), farthest);
            yield return null;
            Assert.That(Vector2.Distance(new Vector2(cameraRig.DesiredFocus.x, cameraRig.DesiredFocus.z), box.center), Is.LessThan(.01f),
                "At the farthest zoom the focus is the room's centre.");

            // A pan at the farthest zoom: the gamepad's left stick, a quarter second. The focus stays at the centre.
            var pad = TestGamepad();
            InputSystem.QueueStateEvent(pad, new GamepadState { leftStick = new Vector2(1f, 0f) });
            yield return new WaitForSecondsRealtime(.25f);
            InputSystem.QueueStateEvent(pad, new GamepadState());
            yield return null;
            Assert.That(Vector2.Distance(new Vector2(cameraRig.DesiredFocus.x, cameraRig.DesiredFocus.z), box.center), Is.LessThan(.01f),
                "Panning at the room's farthest zoom stops at once: the whole room is in frame already.");
            Assert.That(cameraRig.ViewRoom, Is.EqualTo(living), "The pan does not leave the room.");

            // The player followed to the wall: at the nearest zoom the focus is on them.
            var wall = new Vector3(box.xMin + .9f, 0f, box.center.y);
            ScopePlace(player.transform, wall, 2f);
            cameraRig.FocusSubject(player.transform, false);
            RoomViewSetDesired(cameraRig.DesiredFocus, HouseRoomView.NearestDistance);
            yield return null;
            yield return null;
            Assert.That(Mathf.Abs(cameraRig.DesiredFocus.x - player.transform.position.x), Is.LessThan(.05f),
                "At the nearest zoom the focus follows the player out to the wall (" + cameraRig.DesiredFocus.ToString("F2") + ").");
            RoomViewSetDesired(cameraRig.DesiredFocus, farthest);
            yield return null;
            yield return null;
            Assert.That(Mathf.Abs(cameraRig.DesiredFocus.x - box.center.x), Is.LessThan(.05f), "and at the farthest it is back over the centre.");
        }

        /// <summary>
        /// The farthest zoom is the room's own, fixed per room (D4): from every room, at yaws 0, 45, 90 and
        /// 135, zooming out as far as the rig goes lands on 11.25 m in the north block, 10.5 m in the south
        /// wing and 18.25 m over the Yard, the same at every yaw, so orbiting never pulls the camera in. The
        /// pitch keeps the rig's curve over its own metres (the room's farthest is not its far end).
        /// </summary>
        [UnityTest]
        public IEnumerator RoomView_TheFarthestZoomIsFixedPerRoomAndOrbitNeverPullsIn()
        {
            director.ClosePanels();
            director.SuspendNpcAutonomyForDiagnostics();
            yield return null;
            var scope = ScopeOf(director);
            ScopeParkHouseguests(scope);
            var failures = new List<string>();
            for (int room = 0; room < RoomLayers.Count; room++)
            {
                yield return RoomViewPlayerIn(scope, room);
                float expected = HouseRoomView.FarthestDistance(room);
                Assert.That(expected, Is.EqualTo(room == RoomLayers.IndexOf("Yard") ? 18.25f
                    : room >= RoomLayers.IndexOf("HoH") ? 10.5f : 11.25f), RoomLayers.RoomIds[room] + "'s farthest zoom is D4's.");
                foreach (float yaw in new[] { 0f, 45f, 90f, 135f })
                {
                    // An orbit to the yaw and a zoom out as far as the rig goes (the wanted distance only: a boom
                    // written out past the room would be a camera away from its room, which cuts back).
                    RoomViewSetYaw(yaw);
                    RoomViewSetDesired(cameraRig.DesiredFocus, cameraRig.FarthestDistance);
                    yield return null;
                    yield return null;
                    string where = RoomLayers.RoomIds[room] + " at yaw " + yaw;
                    if (cameraRig.ViewRoom != room) { failures.Add(where + ": the view went to " + cameraRig.ViewRoom); continue; }
                    if (Mathf.Abs(cameraRig.DesiredDistance - expected) > .001f) failures.Add(where + ": zoomed out to " + cameraRig.DesiredDistance + ", not " + expected);
                    if (Mathf.Abs(Mathf.DeltaAngle(cameraRig.Yaw, yaw)) > .01f) failures.Add(where + ": the orbit was turned to " + cameraRig.Yaw);
                    float pitch = Mathf.Clamp(cameraRig.PitchFor(cameraRig.DesiredDistance) + cameraRig.PitchOffset, 45f, 70f);
                    if (Mathf.Abs(cameraRig.Pitch - pitch) > .01f) failures.Add(where + ": the pitch is " + cameraRig.Pitch + ", not the curve's " + pitch);
                }
                // The wheel, out: no farther than the room's own, either.
                var mouse = TestMouse();
                var middle = new Vector2(Screen.width * .5f, Screen.height * .5f);
                for (int notch = 0; notch < 4; notch++)
                {
                    InputSystem.QueueStateEvent(mouse, new MouseState { position = middle, scroll = new Vector2(0f, -120f) });
                    yield return null;
                    InputSystem.QueueStateEvent(mouse, new MouseState { position = middle });
                    yield return null;
                }
                if (Mathf.Abs(cameraRig.DesiredDistance - expected) > .001f)
                    failures.Add(RoomLayers.RoomIds[room] + ": the wheel zoomed out to " + cameraRig.DesiredDistance + ", not " + expected);
            }
            Assert.That(failures, Is.Empty, string.Join("; ", failures));
            Assert.That(cameraRig.PitchFor(HouseRoomView.YardFarthest), Is.LessThan(cameraRig.PitchFor(cameraRig.FarthestDistance) - 5f),
                "The pitch curve runs over the rig's own metres, not the room's: the Yard's farthest is not its steepest.");
            Assert.That(cameraRig.FarthestDistance, Is.EqualTo(34f), "The rig's own far end is today's, for the whole house.");
        }

        /// <summary>
        /// Walking through a door is a cut onto the next room's framing (D5), on the frame the player's room
        /// changes, three tenths of a metre past the wall line: the room's best heading nearest the yaw, the
        /// player in the focus, the distance no farther than the room's, the camera there with no easing, and
        /// the view, the mask and the bodies the new room's in the same frame. Walking straight back inside
        /// 0.6 s waits, with the player drawn throughout, and then cuts back.
        /// </summary>
        [UnityTest]
        public IEnumerator RoomView_EnteringARoomCutsToItsFraming()
        {
            director.ClosePanels();
            director.SuspendNpcAutonomyForDiagnostics();
            yield return null;
            yield return Rs4BodiesBuilt();
            var scope = ScopeOf(director);
            ScopeParkHouseguests(scope);
            int living = RoomLayers.IndexOf("Living"), bedroom = RoomLayers.IndexOf("Bedroom");
            var door = ScopeDoorways(scope).First(c => c.From == living && c.To == bedroom);
            var agent = player.Agent;
            bool agentPlaces = agent.updatePosition;
            agent.ResetPath();
            agent.updatePosition = false;
            try
            {
                ScopePlace(player.transform, door.Start);
                cameraRig.FocusSubject(player.transform, false);
                RoomViewSetYaw(30f);
                SetCameraDistance(8f);
                yield return null;
                yield return RoomViewWaitOutTheLastChange();
                yield return SettleCamera();
                RoomViewAssertTheView(living, "A metre inside the living room");
                Assert.That(cameraRig.DesiredDistance, Is.EqualTo(8f).Within(.001f), "at the distance the player chose.");
                int cuts = cameraRig.RoomViewCuts;
                bool cutSeen = false;
                float cutAt = 0f;
                float length = Vector3.Distance(door.Start, door.End), done = 0f;
                while (done < length)
                {
                    done = Mathf.Min(length, done + Rs4Walk * Mathf.Clamp(Time.unscaledDeltaTime, .0005f, .05f));
                    ScopePlace(player.transform, Vector3.Lerp(door.Start, door.End, done / length), .3f);
                    yield return null;
                    Assert.That(Rs4DrawnNow(player.transform, out string why), Is.True, "The player is drawn through the doorway: " + why);
                    if (cameraRig.ViewRoom == living) { Assert.That(cameraRig.RoomViewCuts, Is.EqualTo(cuts), "No cut before the door."); continue; }
                    if (cutSeen) continue;
                    cutSeen = true;
                    cutAt = Time.realtimeSinceStartup;
                    float past = ScopePast(door, player.transform.position);
                    Assert.That(past, Is.GreaterThanOrEqualTo(HouseRoomScope.DoorwayDepth - .001f), "The cut comes once the feet are 0.3 m past the wall line (" + past.ToString("0.000") + ").");
                    Assert.That(cameraRig.RoomViewCuts, Is.EqualTo(cuts + 1), "A cut, once.");
                    Assert.That(cameraRig.Yaw, Is.EqualTo(HouseRoomView.NearestBestHeading(bedroom, 30f)).Within(.01f), "Turned to the bedroom's best heading nearest yaw 30.");
                    Assert.That(cameraRig.DesiredDistance, Is.EqualTo(Mathf.Min(8f, HouseRoomView.FarthestDistance(bedroom))).Within(.001f), "No farther than the room's farthest, no nearer than the player chose.");
                    Assert.That(Vector3.Distance(cameraRig.transform.position, cameraRig.DesiredFocus), Is.LessThan(.01f), "There at once: a cut, not a glide.");
                    Assert.That(Mathf.Abs(cameraRig.Distance - cameraRig.DesiredDistance), Is.LessThan(.01f), "the boom too.");
                    RoomViewAssertTheView(bedroom, "The cut's own frame");
                }
                Assert.That(cutSeen, Is.True, "The walk into the bedroom cut to it.");
                Assert.That(cameraRig.RoomViewCuts, Is.EqualTo(cuts + 1), "and only once.");

                // Straight back inside 0.6 s: the view waits on the bedroom, the player drawn, then cuts back.
                ScopePlace(player.transform, door.Start, .3f);
                yield return null;
                if (Time.realtimeSinceStartup - cutAt < HouseRoomView.RoomChangeSeconds - .1f)
                {
                    Assert.That(cameraRig.ViewRoom, Is.EqualTo(bedroom), "Back inside 0.6 s, the view waits on the room it changed to.");
                    Assert.That(Rs4DrawnNow(player.transform, out string waiting), Is.True, "with the player drawn in the doorway's other room: " + waiting);
                }
                float deadline = Time.realtimeSinceStartup + 2f;
                while (cameraRig.ViewRoom != living && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(cameraRig.ViewRoom, Is.EqualTo(living), "and then it cuts back.");
                Assert.That(Time.realtimeSinceStartup - cutAt, Is.GreaterThanOrEqualTo(HouseRoomView.RoomChangeSeconds - .05f), "no sooner than 0.6 s after the last cut.");
                Assert.That(cameraRig.RoomViewCuts, Is.EqualTo(cuts + 2), "A second cut.");
            }
            finally
            {
                agent.updatePosition = agentPlaces;
                cameraRig.ClearSubject();
            }
        }

        /// <summary>
        /// Under reduced motion, switched on through the settings and asserted on the rig, every change of
        /// room is a cut with no blink: a door crossing as with motion, and a trip across the house (a warp
        /// with motion behind the blink) lands on the far room's framing at once with no blink.
        /// </summary>
        [UnityTest]
        public IEnumerator RoomView_ReducedMotionCutsBetweenRooms()
        {
            director.ClosePanels();
            director.SuspendNpcAutonomyForDiagnostics();
            yield return null;
            director.OpenSettings();
            ButtonWithCaption("Reduce character motion").onClick.Invoke();
            director.ClosePanels();
            yield return null;
            Assert.That(cameraRig.ReducedMotion, Is.True, "The settings put the rig under reduced motion.");
            try
            {
                var scope = ScopeOf(director);
                ScopeParkHouseguests(scope);
                int living = RoomLayers.IndexOf("Living"), kitchen = RoomLayers.IndexOf("Kitchen");
                yield return RoomViewPlayerIn(scope, living);
                var door = ScopeDoorways(scope).First(c => c.From == living && c.To == kitchen);
                int cuts = cameraRig.RoomViewCuts;
                ScopePlace(player.transform, door.End, .5f);
                cameraRig.FocusSubject(player.transform, false);
                yield return null;
                RoomViewAssertTheView(kitchen, "Through the door under reduced motion");
                Assert.That(cameraRig.RoomViewCuts, Is.EqualTo(cuts + 1), "a cut");
                Assert.That(Vector3.Distance(cameraRig.transform.position, cameraRig.DesiredFocus), Is.LessThan(.01f), "with no glide.");

                // Across the house: under reduced motion travel cuts with no blink.
                yield return RoomViewWaitOutTheLastChange();
                cuts = cameraRig.RoomViewCuts;
                director.GoToRoom("Yard");
                Assert.That(director.IsTravelDipShowing, Is.False, "No blink under reduced motion.");
                yield return null;
                if (director.LastTravel == EpisodeDirector.TravelKind.Warp)
                {
                    RoomViewAssertTheView(RoomLayers.IndexOf("Yard"), "The warp to the yard");
                    Assert.That(cameraRig.RoomViewCuts, Is.EqualTo(cuts + 1), "landing on the yard's framing by a cut.");
                }
                else
                {
                    // On foot: the view stays with the player, and cuts at each door they pass.
                    Assert.That(cameraRig.ViewRoom, Is.EqualTo(RoomLayers.IndexOf(director.WalkerRoomOf(player.transform))), "On foot, the player's room.");
                }
                Assert.That(director.IsTravelDipShowing, Is.False, "and still no blink.");
            }
            finally
            {
                director.OpenSettings();
                ButtonWithCaption("Enable character motion").onClick.Invoke();
                director.ClosePanels();
            }
            Assert.That(cameraRig.ReducedMotion, Is.False, "Motion is back on.");
        }

        // ------------------------------------------------------------------------------------
        // Following.
        // ------------------------------------------------------------------------------------

        /// <summary>
        /// Following a houseguest in another room is a cut onto their room (D5), and the view is theirs -
        /// following's, live - with the player outside it hidden; as they walk through a door, the view
        /// cuts with them, and they are drawn on every frame.
        /// </summary>
        [UnityTest]
        public IEnumerator RoomView_FollowingAHouseguestFollowsTheirRoom()
        {
            director.ClosePanels();
            director.SuspendNpcAutonomyForDiagnostics();
            yield return null;
            yield return Rs4BodiesBuilt();
            var scope = ScopeOf(director);
            ScopeParkHouseguests(scope);
            int living = RoomLayers.IndexOf("Living"), kitchen = RoomLayers.IndexOf("Kitchen"), privateRoom = RoomLayers.IndexOf("Private");
            yield return RoomViewPlayerIn(scope, living);
            var guest = SceneComponents<HouseNpc>().First(npc => npc.isActiveAndEnabled);
            var door = ScopeDoorways(scope).First(c => c.From == kitchen && c.To == privateRoom);
            ScopePlace(guest.transform, door.Start);
            yield return null;
            int cuts = cameraRig.RoomViewCuts;
            director.FollowHouseguest(guest.Id);
            Assert.That(cameraRig.FocusedSubject, Is.SameAs(guest.transform), "The camera follows " + guest.DisplayName + ".");
            Assert.That(cameraRig.Rooms, Is.EqualTo(RoomSet.Of(kitchen)), "The follow names their room in the call.");
            yield return null;
            Assert.That(director.CurrentRoomScopeSource, Is.EqualTo(EpisodeDirector.RoomScopeSource.Following), "Following is the view's source.");
            RoomViewAssertTheView(kitchen, "Following into the kitchen");
            Assert.That(cameraRig.RoomViewCuts, Is.EqualTo(cuts + 1), "A follow started across rooms is a cut.");
            Rs6aAssertDrawn(guest.transform, guest.DisplayName);
            Rs6aAssertHidden(player.transform, "The player, in the living room");

            // Through the door into the private room, carried by the transform (the bake closes that door).
            var guestAgent = guest.GetComponent<NavMeshAgent>();
            bool guestPlaces = guestAgent != null && guestAgent.updatePosition;
            if (guestAgent != null) guestAgent.updatePosition = false;
            var failures = new List<string>();
            try
            {
                yield return RoomViewWaitOutTheLastChange();
                cuts = cameraRig.RoomViewCuts;
                yield return Rs4Carry(guest.transform, door, Rs4Walk, guest.DisplayName, failures, () => { });
                Assert.That(failures, Is.Empty, string.Join("; ", failures));
                RoomViewAssertTheView(privateRoom, "Followed into the private room");
                Assert.That(cameraRig.RoomViewCuts, Is.EqualTo(cuts + 1), "The door they pass through is a cut.");
            }
            finally
            {
                if (guestAgent != null) guestAgent.updatePosition = guestPlaces;
                director.FollowHouseguest(guest.Id);
            }
        }

        /// <summary>
        /// Stopping a follow keeps the room it showed as a watching view (§1.2): the houseguest walks off and
        /// the view stays. F gives back the player's room by a cut; so does a walk (a floor click follows the
        /// player) and travel (on foot to the bedroom next door: the view is the player's room from the call).
        /// </summary>
        [UnityTest]
        public IEnumerator RoomView_StoppingAFollowKeepsTheWatchedRoomUntilThePlayerActs()
        {
            director.ClosePanels();
            director.SuspendNpcAutonomyForDiagnostics();
            yield return null;
            var scope = ScopeOf(director);
            ScopeParkHouseguests(scope);
            int living = RoomLayers.IndexOf("Living"), kitchen = RoomLayers.IndexOf("Kitchen");
            yield return RoomViewPlayerIn(scope, living);
            var guest = SceneComponents<HouseNpc>().First(npc => npc.isActiveAndEnabled);
            foreach (var how in new[] { "F", "a walk", "travel" })
            {
                ScopePlace(guest.transform, ScopeOpenFloor(scope, kitchen), 2f);
                yield return null;
                director.FollowHouseguest(guest.Id);
                yield return null;
                RoomViewAssertTheView(kitchen, how + ": following " + guest.DisplayName);
                director.FollowHouseguest(guest.Id);
                yield return null;
                Assert.That(cameraRig.FocusedSubject, Is.Null, how + ": the follow is let go.");
                Assert.That(director.CurrentRoomScopeSource, Is.EqualTo(EpisodeDirector.RoomScopeSource.FreePlay), how + ": free play's view,");
                RoomViewAssertTheView(kitchen, how + ": the room the follow showed, watched");
                // They walk off; the watched room stays.
                ScopePlace(guest.transform, ScopeOpenFloor(scope, RoomLayers.IndexOf("Yard")), 2f);
                yield return RoomViewWaitOutTheLastChange();
                RoomViewAssertTheView(kitchen, how + ": the houseguest gone to the yard, the kitchen still watched");
                int cuts = cameraRig.RoomViewCuts;
                if (how == "F") cameraRig.RecenterOnPlayer();
                else if (how == "a walk") cameraRig.FocusSubject(player.transform, false);
                else director.GoToRoom("Bedroom");
                yield return null;
                RoomViewAssertTheView(living, how + " gives back the player's room");
                Assert.That(cameraRig.RoomViewCuts, Is.EqualTo(cuts + 1), how + ": by a cut.");
                director.ClosePanels();
                yield return RoomViewWaitOutTheLastChange();
            }
        }

        /// <summary>
        /// The brackets and Tab follow the next houseguest: across rooms each is a cut onto their room, and
        /// within one room none.
        /// </summary>
        [UnityTest]
        public IEnumerator RoomView_TabAcrossRoomsCuts()
        {
            if (testKeyboard == null) testKeyboard = InputSystem.AddDevice<Keyboard>();
            director.ClosePanels();
            director.SuspendNpcAutonomyForDiagnostics();
            yield return null;
            var scope = ScopeOf(director);
            var guests = SceneComponents<HouseNpc>().Where(npc => npc.isActiveAndEnabled).ToArray();
            Assert.That(guests.Length, Is.GreaterThanOrEqualTo(2), "Houseguests to cycle.");
            // Each in a room of their own, round the house.
            var rooms = new[] { "Kitchen", "Bedroom", "Private", "Yard", "HoH", "Nomination", "Games" };
            for (int i = 0; i < guests.Length; i++)
                ScopePlace(guests[i].transform, ScopeOpenFloor(scope, RoomLayers.IndexOf(rooms[i % rooms.Length])) + new Vector3(.8f * (i / rooms.Length), 0f, 0f), 2f);
            yield return RoomViewPlayerIn(scope, RoomLayers.IndexOf("Living"));
            int seen = 0;
            Transform last = null;
            for (int press = 0; press < 3; press++)
            {
                int before = cameraRig.ViewRoom, cuts = cameraRig.RoomViewCuts;
                yield return PressKey(Key.RightBracket);
                var followed = cameraRig.FocusedSubject;
                Assert.That(followed, Is.Not.Null.And.Not.SameAs(last), "] follows the next houseguest.");
                last = followed;
                yield return null;
                int theirs = RoomLayers.IndexOf(director.WalkerRoomOf(followed));
                RoomViewAssertTheView(theirs, "] onto " + followed.name);
                Assert.That(cameraRig.RoomViewCuts, Is.EqualTo(theirs != before ? cuts + 1 : cuts), "Across rooms a cut; within one, none.");
                if (theirs != before) seen++;
            }
            Assert.That(seen, Is.GreaterThan(0), "Some press crossed rooms.");
            cameraRig.ClearSubject();
            yield return null;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            yield return null;
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == null)
            {
                int before = cameraRig.ViewRoom, cuts = cameraRig.RoomViewCuts;
                yield return PressKey(Key.Tab);
                var followed = cameraRig.FocusedSubject;
                Assert.That(followed, Is.Not.Null, "With nothing focused, Tab follows the next houseguest.");
                yield return null;
                int theirs = RoomLayers.IndexOf(director.WalkerRoomOf(followed));
                RoomViewAssertTheView(theirs, "Tab onto " + followed.name);
                Assert.That(cameraRig.RoomViewCuts, Is.EqualTo(theirs != before ? cuts + 1 : cuts), "Tab across rooms cuts too.");
            }
            cameraRig.ClearSubject();
        }

        /// <summary>
        /// A body the room scope has never tracked - a load, or the opening's hand-over, in a doorway - takes
        /// the floor box under its feet, else the nearest (§3.2 a): followed, the view frames that room.
        /// </summary>
        [UnityTest]
        public IEnumerator RoomView_ALoadInADoorwayTakesTheNearestFloor()
        {
            director.ClosePanels();
            director.SuspendNpcAutonomyForDiagnostics();
            yield return null;
            var scope = ScopeOf(director);
            int living = RoomLayers.IndexOf("Living"), bedroom = RoomLayers.IndexOf("Bedroom");
            var door = ScopeDoorways(scope).First(c => c.From == living && c.To == bedroom);
            var box = scope.Box(living);
            var cases = new[]
            {
                (door.Wall + door.Across * .1f, bedroom, "a tenth of a metre into the bedroom, in the doorway"),
                (door.Wall - door.Across * .1f, living, "a tenth of a metre into the living room, in the doorway"),
                (new Vector3(box.xMin - .2f, 0f, box.center.y), living, "a fifth of a metre off the living room's west edge, on no floor"),
            };
            foreach (var (at, room, what) in cases)
            {
                var loaded = new GameObject("A body loaded " + what).transform;
                try
                {
                    loaded.position = at;
                    Assert.That(scope.LastRoom(loaded), Is.EqualTo(-1), what + ": never tracked.");
                    cameraRig.FocusSubject(loaded, false);
                    yield return null;
                    Assert.That(scope.LastRoom(loaded), Is.EqualTo(room), what + ": takes the " + RoomLayers.RoomIds[room] + ".");
                    RoomViewAssertTheView(room, "Following a body loaded " + what);
                }
                finally
                {
                    cameraRig.ClearSubject();
                    Object.Destroy(loaded.gameObject);
                }
                yield return null;
                cameraRig.RecenterOnPlayer();
                yield return null;
            }
        }

        /// <summary>
        /// The nearest zoom is four metres (D11), whatever the scene's saved ten: the wheel comes in to it and
        /// no further, and a click on a houseguest frames them at seven, which ten used to stop. With the whole
        /// house held the free camera is today's envelope, out to 34 m.
        /// </summary>
        [UnityTest]
        public IEnumerator RoomView_TheNearestZoomIsFourMetres()
        {
            director.ClosePanels();
            director.SuspendNpcAutonomyForDiagnostics();
            yield return null;
            var scope = ScopeOf(director);
            ScopeParkHouseguests(scope);
            int living = RoomLayers.IndexOf("Living");
            yield return RoomViewPlayerIn(scope, living);
            Assert.That(cameraRig.NearestDistance, Is.EqualTo(HouseRoomView.NearestDistance), "Four metres, not the scene's ten (D11).");
            var mouse = TestMouse();
            var middle = new Vector2(Screen.width * .5f, Screen.height * .5f);
            for (int notch = 0; notch < 8; notch++)
            {
                InputSystem.QueueStateEvent(mouse, new MouseState { position = middle, scroll = new Vector2(0f, 120f) });
                yield return null;
                InputSystem.QueueStateEvent(mouse, new MouseState { position = middle });
                yield return null;
            }
            Assert.That(cameraRig.DesiredDistance, Is.EqualTo(4f).Within(.001f), "The wheel comes in to four metres and no further.");
            var guest = SceneComponents<HouseNpc>().First(npc => npc.isActiveAndEnabled);
            ScopePlace(guest.transform, ScopeOpenFloor(scope, living) + new Vector3(1.5f, 0f, 0f), 2f);
            yield return null;
            cameraRig.FocusSubject(guest.transform);
            yield return null;
            Assert.That(cameraRig.DesiredDistance, Is.EqualTo(7f).Within(.001f), "A click on a houseguest frames them at seven metres.");
            cameraRig.ClearSubject();

            RoomViewHoldTheDollhouse();
            yield return null;
            Assert.That(cameraRig.IsRoomView, Is.False, "With the whole house held, the free camera is today's.");
            Assert.That(cameraRig.NearestDistance, Is.EqualTo(4f), "with the same nearest zoom");
            SetCameraDistance(cameraRig.FarthestDistance);
            yield return null;
            yield return null;
            Assert.That(cameraRig.DesiredDistance, Is.EqualTo(34f).Within(.001f), "and today's farthest.");
            yield return RoomViewLetGoOfTheDollhouse();
            Assert.That(cameraRig.IsRoomView, Is.True, "Let go, the room view again,");
            Assert.That(cameraRig.DesiredDistance, Is.LessThanOrEqualTo(cameraRig.FreeFarthestDistance + .001f), "at no more than the room's farthest.");
        }

        /// <summary>
        /// The opening hands over with a cut onto the player's room's framing (D6): the crane over the whole
        /// house ends, and on the frame free play takes the camera it stands over the player's room at no more
        /// than its farthest zoom, the room drawn alone.
        /// </summary>
        [UnityTest]
        public IEnumerator RoomView_TheOpeningHandsOverWithACutToThePlayersRoom()
        {
            var opening = director.Opening;
            string firstGuest = director.Snapshot.Active.First(person => !person.isPlayer).id;
            director.PlayOpeningForVerification(stage: true, holdHeadless: true, armSeconds: 0f);
            yield return WaitFor(() => director.IsOpeningStaged, 40f, "The house is placed behind the front door.");
            yield return WaitFor(() => opening.CurrentGuestId != null, 40f, "The reveals begin.");
            Assert.That(director.ViewedRooms.IsWholeHouse, Is.True, "The opening draws the whole house.");
            Assert.That(cameraRig.IsRoomView, Is.False, "and the camera is the opening's, not the room view.");
            director.SkipOpening();
            yield return WaitFor(() => opening.IsMeeting, 10f, "Skipping the show stops at the introductions.");
            yield return WaitFor(() => MeetCardIsSettled(opening, firstGuest), 8f, "The first houseguest's card is up.");
            int cuts = cameraRig.RoomViewCuts;
            opening.SkipIntroductions();
            yield return WaitFor(() => !opening.IsPlaying, 5f, "Skipping the introductions ends the opening.");
            yield return null;
            yield return null;
            Assert.That(director.CurrentRoomScopeSource, Is.EqualTo(EpisodeDirector.RoomScopeSource.FreePlay), "Handed over, free play's.");
            int room = RoomLayers.IndexOf(director.WalkerRoomOf(player.transform));
            Assert.That(room, Is.GreaterThanOrEqualTo(0), "The player stands on a floor.");
            RoomViewAssertTheView(room, "The hand-over");
            Assert.That(cameraRig.RoomViewCuts, Is.GreaterThan(cuts), "A cut onto the player's room, not the crane's ease down.");
            Assert.That(cameraRig.Distance, Is.LessThanOrEqualTo(HouseRoomView.FarthestDistance(room) + .01f), "at no more than the room's farthest zoom.");
            Assert.That(HouseRoomView.Over(ScopeOf(director).Box(room), cameraRig.transform.position, cameraRig.Distance, HouseRoomView.FarthestDistance(room)), Is.True,
                "standing over the room.");
        }

        // ------------------------------------------------------------------------------------
        // Way-finding (D17).
        // ------------------------------------------------------------------------------------

        /// <summary>
        /// The next stop stays pinned and clickable at every room-view distance (D17): from every room, at its
        /// nearest and farthest zoom and at yaws 0, 45 and 90, the episode screen's icon is on screen, clear of
        /// the HUD and takes a click - where the dollhouse hid every icon under 14 m.
        /// </summary>
        [UnityTest]
        public IEnumerator WayFinding_TheNextStopIsPinnedAndClickableAtEveryRoomViewDistance()
        {
            director.ClosePanels();
            director.SuspendNpcAutonomyForDiagnostics();
            yield return null;
            Assert.That(director.Snapshot.pendingDiary, Is.Null, "No diary is owed, so the next stop is the episode screen.");
            var scope = ScopeOf(director);
            ScopeParkHouseguests(scope);
            string next = director.ScreenRoom;
            var hud = director.GetComponentInChildren<EpisodeHud>(true);
            var failures = new List<string>();
            for (int room = 0; room < RoomLayers.Count; room++)
            {
                cameraRig.SetReducedMotion(false);
                yield return RoomViewPlayerIn(scope, room);
                // The rig's own reduced motion for the sweep: a yaw and a distance set are the ones drawn on the next frame.
                cameraRig.SetReducedMotion(true);
                foreach (float distance in new[] { HouseRoomView.NearestDistance, HouseRoomView.FarthestDistance(room) })
                    foreach (float yaw in new[] { 0f, 45f, 90f })
                    {
                        RoomViewSetYaw(yaw);
                        SetCameraDistance(distance);
                        yield return null;
                        yield return null;
                        yield return null;
                        string where = RoomLayers.RoomIds[room] + " at " + distance + " m, yaw " + yaw;
                        var beacons = director.TravelBeacons;
                        if (beacons == null) { failures.Add(where + ": no icons"); continue; }
                        if (beacons.RoomViewRoom != RoomLayers.RoomIds[room]) failures.Add(where + ": the icons are placed for " + beacons.RoomViewRoom);
                        var icon = BeaconFor(next);
                        if (icon == null || !icon.gameObject.activeInHierarchy) { failures.Add(where + ": the next stop's icon is not shown"); continue; }
                        var centre = ScreenCentre(icon);
                        if (!cameraRig.ViewCamera.pixelRect.Contains(centre)) failures.Add(where + ": the next stop's icon is off the screen");
                        if (hud.Covers(centre)) failures.Add(where + ": the next stop's icon is under the HUD");
                        var group = icon.GetComponent<CanvasGroup>();
                        if (group != null && (!group.blocksRaycasts || !group.interactable)) failures.Add(where + ": the next stop's icon takes no click");
                    }
            }
            cameraRig.SetReducedMotion(false);
            Assert.That(failures, Is.Empty, string.Join("; ", failures.Take(20)));

            // And a click on it goes there, from the room farthest from it.
            var station = director.StationPosition;
            int farRoom = Rs4FarRoom(scope, station);
            yield return RoomViewPlayerIn(scope, farRoom);
            yield return null;
            yield return null;
            var shown = BeaconFor(next);
            Assert.That(shown != null && shown.gameObject.activeInHierarchy, Is.True, "From the " + RoomLayers.RoomIds[farRoom] + ", the next stop is on screen.");
            var mouse = TestMouse();
            yield return ClickAt(mouse, ScreenCentre(shown));
            Assert.That(director.LastTravel, Is.Not.EqualTo(EpisodeDirector.TravelKind.None), "A click on the pinned next stop goes there. Status: " + director.StatusMessage);
        }

        /// <summary>
        /// Every other room keeps an icon with its count (D17): from the living room each room a doorway leads
        /// to has its icon at that doorway, every other room's is on screen at the frame's edge, and a press on
        /// a neighbour's icon takes the player into that room.
        /// </summary>
        [UnityTest]
        public IEnumerator WayFinding_EachAdjacentRoomsIconReachesItsRoom()
        {
            director.ClosePanels();
            director.SuspendNpcAutonomyForDiagnostics();
            yield return null;
            var scope = ScopeOf(director);
            ScopeParkHouseguests(scope);
            int living = RoomLayers.IndexOf("Living");
            yield return RoomViewPlayerIn(scope, living);
            yield return null;
            yield return null;
            var beacons = director.TravelBeacons;
            Assert.That(beacons, Is.Not.Null, "The icons are built.");
            Assert.That(beacons.RoomViewRoom, Is.EqualTo("Living"), "placed for the room view of the living room.");
            var adjacent = Enumerable.Range(0, RoomLayers.Count).Where(room => room != living && scope.TryDoorway(living, room, cameraRig.transform.position, out _)).ToList();
            Assert.That(adjacent.Select(room => RoomLayers.RoomIds[room]).ToArray(), Is.SupersetOf(new[] { "Kitchen", "Bedroom" }),
                "The living room's doorways lead to the kitchen and the bedroom, read from the walls.");
            var failures = new List<string>();
            foreach (var room in Enumerable.Range(0, RoomLayers.Count).Where(room => room != living))
            {
                string id = RoomLayers.RoomIds[room];
                var icon = BeaconFor(id);
                bool shownIcon = icon != null && icon.gameObject.activeInHierarchy;
                if (!shownIcon) { failures.Add(id + ": no icon"); continue; }
                if (!cameraRig.ViewCamera.pixelRect.Contains(ScreenCentre(icon))) failures.Add(id + ": its icon is off the screen");
                if (beacons.ShownCount(id) < 0) failures.Add(id + ": no count");
                bool viaDoor = adjacent.Contains(room);
                if (viaDoor && !beacons.IsAtDoorway(id)) failures.Add(id + ": a doorway leads there, but its icon is not at it (or pinned towards it)");
                if (!viaDoor && beacons.IsAtDoorway(id)) failures.Add(id + ": no doorway leads there, but its icon stands at one");
                if (!viaDoor && !beacons.IsPinned(id)) failures.Add(id + ": with no doorway from here its icon should wait at the frame's edge");
            }
            Assert.That(failures, Is.Empty, string.Join("; ", failures));

            // A press on the kitchen's icon takes the player into the kitchen.
            director.PressTravelBeacon("Kitchen");
            Assert.That(director.LastTravel, Is.Not.EqualTo(EpisodeDirector.TravelKind.None), "The kitchen's icon sends the player there. Status: " + director.StatusMessage);
            var goal = director.LastTravel == EpisodeDirector.TravelKind.Warp ? player.transform.position : player.Agent.destination;
            Assert.That(scope.RoomUnder(goal), Is.EqualTo(RoomLayers.IndexOf("Kitchen")), "The trip ends in the kitchen.");
        }

        // ------------------------------------------------------------------------------------
        // Looking away (D18).
        // ------------------------------------------------------------------------------------

        /// <summary>
        /// A follow elsewhere is not looking at the player's room (D18): an act staged beside the player is
        /// not sighted, the walk-in watch does not run, and invitations and the Nearby card wait, as under the
        /// overview; F gives the view back and the same act is seen.
        /// </summary>
        [UnityTest, Timeout(300000)]
        public IEnumerator LookingAway_AFollowElsewhereGivesNoSightingOrWalkInFromThePlayersRoom()
        {
            NpcActState act = null;
            yield return InstallAllWeekFixture(AtTheNominations, s => act = WriteAct(s, NpcActKinds.Talk, "Kitchen", false), "the nominations' window");
            PlayTheActs();
            try
            {
                yield return WaitForTheActStaged(act);
                int lines = Sightings(director.Snapshot);
                var scope = ScopeOf(director);
                var someoneElse = SceneComponents<HouseNpc>().First(npc => npc.isActiveAndEnabled && npc.Id != act.actorId && npc.Id != act.partnerId);
                ScopePlace(someoneElse.transform, ScopeOpenFloor(scope, RoomLayers.IndexOf("Games")), 2f);
                WarpBeside(act);
                director.FollowHouseguest(someoneElse.Id);
                yield return null;
                Assert.That(cameraRig.FocusedSubject, Is.SameAs(someoneElse.transform), "The camera follows " + someoneElse.DisplayName + " to the games room.");
                Assert.That(director.LookingAwayFromThePlayer, Is.True, "The view leaves out the player's kitchen: not looking.");
                var watch = typeof(EpisodeDirector).GetField("proximityWatch", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(watch, Is.Not.Null, "The walk-in watch keeps its clock in one place.");
                watch.SetValue(director, 0f);
                yield return Linger(4f, () =>
                {
                    if (cameraRig.FocusedSubject != someoneElse.transform) director.FollowHouseguest(someoneElse.Id);
                });
                Assert.That(ActNow(act.id).sighted, Is.False, "Not sighted while the view is elsewhere.");
                Assert.That(Sightings(director.Snapshot), Is.EqualTo(lines), "No line.");
                Assert.That((float)watch.GetValue(director), Is.EqualTo(0f), "The walk-in watch does not run while the player is looking away.");
                Assert.That(director.NpcInvitationIsVisible, Is.False, "No invitation comes up while the player is looking away.");

                // F: the player's room again, and the act is seen.
                director.FollowHouseguest(someoneElse.Id);
                cameraRig.RecenterOnPlayer();
                yield return null;
                Assert.That(director.LookingAwayFromThePlayer, Is.False, "Back on the player's room, looking.");
                WarpBeside(act);
                yield return WaitUntilSeen(act, 20f);
                Assert.That(ActNow(act.id).sighted, Is.True, "Seen once the view is back: the guard held it, not the geometry.");
                Assert.That(Sightings(director.Snapshot), Is.EqualTo(lines + 1));
            }
            finally { StopPlayingTheActs(); }
        }

        // ------------------------------------------------------------------------------------
        // "Show the whole house" (D12).
        // ------------------------------------------------------------------------------------

        /// <summary>
        /// "Show the whole house" (D12), off by default: switched on in the display settings, every room, every
        /// lamp and every body are drawn and the free camera is today's, out to 34 m; it is kept as
        /// Gamesim.WholeHouse in the root's own preferences and comes back after a reload; switched off, the
        /// room view comes back with a cut, and that is kept too.
        /// </summary>
        [UnityTest, Timeout(180000)]
        public IEnumerator RoomScope_TheWholeHouseSettingDrawsEverythingAndSurvivesARestart()
        {
            Assert.That(director.ShowsWholeHouse, Is.False, "Off by default.");
            string file = Path.Combine(temporaryDirectory, PreferenceFile.FileName);
            EpisodeDirector.PreferencesBesideSaves = true;
            director.FreezeForReloadForDiagnostics();
            yield return ReloadEpisode();
            Assert.That(director.PreferenceRouteInUse, Is.EqualTo(PreferenceRoute.RootFile));
            Assert.That(director.ShowsWholeHouse, Is.False, "A root with no setting yet starts from one room.");
            director.SuspendNpcAutonomyForDiagnostics();
            yield return null;
            var scope = ScopeOf(director);
            var homes = LampScopeHomes(scope);
            var authored = homes.Select(home => home.Light.enabled).ToArray();
            ScopeParkHouseguests(scope);
            yield return RoomViewPlayerIn(scope, RoomLayers.IndexOf("Living"));
            Assert.That(director.HiddenBodyCount, Is.GreaterThan(0), "One room: the houseguests in the yard are hidden.");

            director.OpenSettings();
            ButtonWithCaption(EpisodeDirector.HouseViewCaption(false)).onClick.Invoke();
            yield return null;
            Assert.That(director.ShowsWholeHouse, Is.True, "Pressed, the whole house.");
            Assert.That(ButtonWithCaption(EpisodeDirector.HouseViewCaption(true)), Is.Not.Null, "The control says what it shows now.");
            director.ClosePanels();
            yield return null;
            yield return null;
            Rs6bAssertTheWholeHouse("The whole house setting");
            Assert.That(cameraRig.IsRoomView, Is.False, "The free camera is today's.");
            Rs6aAssertLamps(Rs6aRenderNow(homes), homes, authored, RoomSet.WholeHouse, "The whole house setting");
            SetCameraDistance(cameraRig.FarthestDistance);
            yield return null;
            yield return null;
            Assert.That(cameraRig.DesiredDistance, Is.EqualTo(34f).Within(.001f), "out to today's 34 m.");
            Assert.That(File.ReadAllText(file), Does.Contain("\"" + EpisodeDirector.WholeHouseKey + "\": \"1\""), "Kept in the root's own preferences.");

            director.FreezeForReloadForDiagnostics();
            yield return ReloadEpisode();
            Assert.That(director.ShowsWholeHouse, Is.True, "The setting survives a restart.");
            yield return null;
            yield return null;
            Assert.That(director.ViewedRooms.IsWholeHouse, Is.True, "and draws the whole house from the first frame.");

            director.OpenSettings();
            ButtonWithCaption(EpisodeDirector.HouseViewCaption(true)).onClick.Invoke();
            director.ClosePanels();
            yield return null;
            yield return null;
            Assert.That(director.ShowsWholeHouse, Is.False, "Pressed again, one room.");
            Assert.That(cameraRig.IsRoomView, Is.True, "The room view is back,");
            Assert.That(cameraRig.DesiredDistance, Is.LessThanOrEqualTo(cameraRig.FreeFarthestDistance + .001f), "over its room.");
            Assert.That(File.ReadAllText(file), Does.Contain("\"" + EpisodeDirector.WholeHouseKey + "\": \"0\""), "and that is kept.");
            director.FreezeForReloadForDiagnostics();
            yield return ReloadEpisode();
            Assert.That(director.ShowsWholeHouse, Is.False, "One room after a restart too.");
        }

        /// <summary>
        /// The house view control's caption is new (checked against every HUD control the settings and the
        /// house draw), follows the settings' pattern for a control that cycles, and draws every letter in a
        /// label box at least 1.3 times its font at each text size, 1.0, 1.2 and 1.4.
        /// </summary>
        [UnityTest]
        public IEnumerator Settings_TheHouseViewCaptionIsUnusedAndFitsAtEveryTextSize()
        {
            Assert.That(EpisodeDirector.HouseViewCaption(false), Is.EqualTo("House view: One room  (change)"), "The settings' pattern (D12).");
            Assert.That(EpisodeDirector.HouseViewCaption(true), Is.EqualTo("House view: Whole house  (change)"));
            director.ClosePanels();
            yield return null;
            var houseCaptions = director.GetComponentsInChildren<Button>(true).SelectMany(b => b.GetComponentsInChildren<TMP_Text>(true)).Select(t => t.text).ToList();
            Assert.That(houseCaptions.Any(text => text != null && text.StartsWith(EpisodeDirector.HouseViewCaptionPrefix, System.StringComparison.Ordinal)), Is.False,
                "No control over the house says \"House view\".");
            foreach (var size in new[] { TextSize.Standard, TextSize.Larger, TextSize.Largest })
            {
                director.SetTextSize(size);
                director.OpenSettings();
                yield return null;
                Canvas.ForceUpdateCanvases();
                var captions = director.GetComponentsInChildren<Button>(true).Where(b => b.gameObject.activeInHierarchy)
                    .Select(b => b.GetComponentsInChildren<TMP_Text>(true).Select(t => t.text).FirstOrDefault() ?? "").ToList();
                string mine = EpisodeDirector.HouseViewCaption(director.ShowsWholeHouse);
                Assert.That(captions.Count(text => text == mine), Is.EqualTo(1), size + ": one control with the house view's caption.");
                Assert.That(captions.Count(text => text.StartsWith(EpisodeDirector.HouseViewCaptionPrefix, System.StringComparison.Ordinal)), Is.EqualTo(1),
                    size + ": and no other control says \"House view\".");
                var button = ButtonWithCaption(mine);
                yield return null;
                Canvas.ForceUpdateCanvases();
                var label = button.GetComponentsInChildren<TMP_Text>(true).First(t => t.text == mine);
                label.ForceMeshUpdate();
                Assert.That(label.rectTransform.rect.height, Is.GreaterThanOrEqualTo(label.fontSize * 1.3f - .01f),
                    size + ": the label's box (" + label.rectTransform.rect.height + ") is at least 1.3 times its font (" + label.fontSize + ").");
                int letters = mine.Count(c => !char.IsWhiteSpace(c));
                int drawn = label.textInfo.characterInfo.Take(label.textInfo.characterCount).Count(glyph => glyph.isVisible);
                Assert.That(drawn, Is.EqualTo(letters), size + ": every letter of \"" + mine + "\" is drawn.");
                director.ClosePanels();
                yield return null;
            }
            director.SetTextSize(TextSize.Standard);
        }
    }
}
