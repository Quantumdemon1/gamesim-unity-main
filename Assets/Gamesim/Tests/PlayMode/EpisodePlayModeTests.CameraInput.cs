using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Gamesim.House;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// Camera Phase 3 (MASTER-PLAN §3.E): every control reaches the rig through its Input Actions
    /// map. A middle-drag pans so the ground under the cursor stays under it, the screen's edges
    /// pan when edge panning is on, and a gamepad orbits, pans, zooms and recenters through the
    /// same rig - each driven here by queueing device state, never by calling the rig.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        private Gamepad testGamepad;

        [UnityTest]
        public IEnumerator CameraInput_AMiddleDragKeepsTheGroundUnderTheCursor()
        {
            SetCameraDistance(20f);
            yield return SettleCamera();
            var mouse = TestMouse();
            var at = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            var delta = new Vector2(80f, 0f);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = at - delta });
            yield return null;
            var plane = new Plane(Vector3.up, new Vector3(0f, cameraRig.DesiredFocus.y, 0f));
            var wasUnder = Under(plane, at - delta);
            var focusBefore = cameraRig.DesiredFocus;

            InputSystem.QueueStateEvent(mouse, new MouseState { position = at, delta = delta }.WithButton(MouseButton.Middle));
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = at });
            yield return null;
            Assert.That(Vector3.Distance(cameraRig.DesiredFocus, focusBefore), Is.GreaterThan(0.2f), "A middle-drag pans.");
            // The cursor sits at the screen's centre, where the ray passes through the focus: so
            // after the drag the focus itself must be what was under the cursor before it. The
            // rig's target is measured, not a camera still easing toward it - at a thousand frames a
            // second the easing is slow enough that "settled" can still be most of a metre out.
            var focus = cameraRig.DesiredFocus;
            Assert.That(Vector2.Distance(new Vector2(focus.x, focus.z), new Vector2(wasUnder.x, wasUnder.z)), Is.LessThan(0.35f),
                "What was under the cursor before the drag is under it after: the ground follows the hand.");
        }

        [UnityTest]
        public IEnumerator CameraInput_TheScreensEdgePansWhenEdgePanningIsOn()
        {
            SetCameraDistance(20f);
            yield return SettleCamera();
            var mouse = TestMouse();
            var band = cameraRig.EdgePanBand * 0.5f;
            // A spot on the right edge with no HUD under it; the edge pans only off the chrome.
            var candidates = new List<Vector2>();
            for (float y = 0.3f; y <= 0.7f; y += 0.05f) candidates.Add(new Vector2(Screen.width - band, Screen.height * y));
            var events = EventSystem.current;
            Vector2? clear = null;
            foreach (var candidate in candidates)
            {
                var hits = new List<RaycastResult>();
                events.RaycastAll(new PointerEventData(events) { position = candidate }, hits);
                if (hits.Count == 0) { clear = candidate; break; }
            }
            Assume.That(clear.HasValue, "Some point on the right edge must be clear of the HUD.");
            Assert.That(cameraRig.EdgeDirection(clear.Value), Is.EqualTo(Vector2.right), "The right edge pans right.");
            Assert.That(cameraRig.EdgeDirection(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f)), Is.EqualTo(Vector2.zero));
            Assert.That(cameraRig.EdgeDirection(new Vector2(-5f, Screen.height * 0.5f)), Is.EqualTo(Vector2.zero),
                "A cursor off the screen is not on an edge.");

            cameraRig.EdgePanInWindow = false;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = clear.Value });
            yield return null; yield return null;
            var focusBefore = cameraRig.DesiredFocus;
            yield return new WaitForSecondsRealtime(0.25f);
            if (!Screen.fullScreen)
                Assert.That(Vector3.Distance(cameraRig.DesiredFocus, focusBefore), Is.LessThan(0.01f),
                    "In a window the edge does nothing until the player opts in.");

            cameraRig.EdgePanInWindow = true;
            focusBefore = cameraRig.DesiredFocus;
            yield return new WaitForSecondsRealtime(0.25f);
            cameraRig.EdgePanInWindow = false;
            var moved = cameraRig.DesiredFocus - focusBefore;
            Assert.That(moved.magnitude, Is.GreaterThan(0.2f), "The cursor at the right edge pans.");
            var right = cameraRig.transform.right; right.y = 0f;
            Assert.That(Vector3.Dot(moved.normalized, right.normalized), Is.GreaterThan(0.9f), "...to the camera's right.");
            InputSystem.QueueStateEvent(mouse, new MouseState { position = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f) });
            yield return null;
        }

        [UnityTest]
        public IEnumerator CameraInput_AGamepadOrbitsPansZoomsAndRecentersThroughTheSameRig()
        {
            SetCameraDistance(20f);
            yield return SettleCamera();
            var pad = TestGamepad();

            var yawBefore = cameraRig.transform.rotation.eulerAngles.y;
            InputSystem.QueueStateEvent(pad, new GamepadState { rightStick = new Vector2(1f, 0f) });
            yield return new WaitForSecondsRealtime(0.25f);
            InputSystem.QueueStateEvent(pad, new GamepadState());
            yield return SettleCamera();
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(cameraRig.transform.rotation.eulerAngles.y, yawBefore)), Is.GreaterThan(5f),
                "The right stick orbits.");

            var focusBefore = cameraRig.DesiredFocus;
            InputSystem.QueueStateEvent(pad, new GamepadState { leftStick = new Vector2(1f, 0f) });
            yield return new WaitForSecondsRealtime(0.25f);
            InputSystem.QueueStateEvent(pad, new GamepadState());
            yield return null;
            Assert.That(Vector3.Distance(cameraRig.DesiredFocus, focusBefore), Is.GreaterThan(0.2f), "The left stick pans.");

            float distanceBefore = cameraRig.DesiredDistance;
            InputSystem.QueueStateEvent(pad, new GamepadState { rightTrigger = 1f });
            yield return new WaitForSecondsRealtime(0.25f);
            InputSystem.QueueStateEvent(pad, new GamepadState());
            yield return null;
            Assert.That(cameraRig.DesiredDistance, Is.LessThan(distanceBefore - 0.5f), "The right trigger zooms in.");

            var player = SceneComponents<HousePlayerController>().First();
            InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.RightStick));
            yield return null;
            InputSystem.QueueStateEvent(pad, new GamepadState());
            yield return null;
            var focus = cameraRig.DesiredFocus;
            Assert.That(Vector2.Distance(new Vector2(focus.x, focus.z), new Vector2(player.transform.position.x, player.transform.position.z)),
                Is.LessThan(0.1f), "Pressing the right stick recenters on the player.");

            InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.RightShoulder));
            yield return null;
            Assert.That(cameraRig.Actions.Next.WasPressedThisFrame(), Is.True, "The right shoulder is the next-houseguest action.");
            InputSystem.QueueStateEvent(pad, new GamepadState());
            yield return null;
        }

        private Vector3 Under(Plane plane, Vector2 screen)
        {
            var ray = cameraRig.ViewCamera.ScreenPointToRay(screen);
            Assert.That(plane.Raycast(ray, out float enter), Is.True, "The screen point must see the focus plane.");
            return ray.GetPoint(enter);
        }

        private Gamepad TestGamepad()
        {
            if (testGamepad == null) testGamepad = InputSystem.AddDevice<Gamepad>();
            cameraRig.ControlsEnabled = true;
            return testGamepad;
        }
    }
}
