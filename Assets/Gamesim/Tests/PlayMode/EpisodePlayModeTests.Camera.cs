using System.Collections;
using System.Linq;
using Gamesim.House;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// Camera Phase 1 (MASTER-PLAN §3.E): the pitch follows the distance and keeps the orbit's
    /// offset, the wheel zooms toward whatever is under the cursor, and a wall between the camera
    /// and its focus pulls the camera in for as long as it is there - without changing the distance
    /// the player asked for.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        [UnityTest]
        public IEnumerator Camera_PitchFollowsTheDistanceAndKeepsTheOrbitOffset()
        {
            SetCameraDistance(cameraRig.FarthestDistance);
            yield return SettleCamera();
            float far = cameraRig.Pitch;
            float authored = far - cameraRig.PitchFor(cameraRig.FarthestDistance);
            SetCameraDistance(6f);
            yield return SettleCamera();
            float near = cameraRig.Pitch;
            Assert.That(far, Is.GreaterThan(near + 8f), "From far away the house reads as a plan; close in, a face.");
            // The scene's authored pitch rides along as an offset; the distance moves the base.
            Assert.That(near - cameraRig.PitchFor(6f), Is.EqualTo(authored).Within(0.5f));

            // A right-drag upward tilts the camera down: an offset on the distance's pitch.
            var mouse = TestMouse();
            InputSystem.QueueStateEvent(mouse, new MouseState
            {
                position = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f),
                buttons = (ushort)(1 << (int)MouseButton.Right), delta = new Vector2(0f, -40f),
            });
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f) });
            yield return null;
            yield return SettleCamera();
            float offset = cameraRig.Pitch - cameraRig.PitchFor(cameraRig.DesiredDistance);
            Assert.That(Mathf.Abs(offset), Is.GreaterThan(3f), "The drag must have set an orbit offset.");

            // Zooming changes the base pitch but leaves the offset where the drag put it.
            SetCameraDistance(20f);
            yield return SettleCamera();
            Assert.That(cameraRig.Pitch - cameraRig.PitchFor(cameraRig.DesiredDistance), Is.EqualTo(offset).Within(0.5f),
                "The zoom must not undo the orbit.");
        }

        [UnityTest]
        public IEnumerator Camera_ZoomsTowardWhatIsUnderTheCursor()
        {
            SetCameraDistance(24f);
            yield return SettleCamera();
            var focusBefore = cameraRig.DesiredFocus;
            var screen = new Vector2(Screen.width * 0.72f, Screen.height * 0.55f);
            var ray = cameraRig.ViewCamera.ScreenPointToRay(screen);
            Assert.That(new Plane(Vector3.up, new Vector3(0f, focusBefore.y, 0f)).Raycast(ray, out float enter), Is.True);
            var under = ray.GetPoint(enter);
            float before = Vector3.Distance(focusBefore, under);
            Assume.That(before, Is.GreaterThan(1f), "The cursor must point away from the current focus for this to mean anything.");

            var mouse = TestMouse();
            InputSystem.QueueStateEvent(mouse, new MouseState { position = screen, scroll = new Vector2(0f, 120f) });
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = screen });
            yield return null;

            Assert.That(cameraRig.DesiredDistance, Is.LessThan(24f), "A notch in must zoom in.");
            float after = Vector3.Distance(cameraRig.DesiredFocus, under);
            Assert.That(after, Is.LessThan(before), "Zooming in must draw the focus toward the point under the cursor.");
            Assert.That(after / before, Is.EqualTo(cameraRig.DesiredDistance / 24f).Within(0.05f),
                "By the fraction the boom shortened, so what was under the cursor stays under it.");
        }

        [UnityTest]
        public IEnumerator Camera_PullsInWhileAWallOccludesTheFocusAndLetsGoAfter()
        {
            SetCameraDistance(cameraRig.FarthestDistance);
            yield return SettleCamera();
            float far = cameraRig.Distance;
            Assert.That(cameraRig.AppliedDistance, Is.EqualTo(far).Within(0.05f), "Nothing in the way to begin with.");

            // A wall stood on the boom line, beyond the zoom minimum. Anything nearer the focus than
            // the player could zoom to never counts, so the chair beside the focus cannot yank the
            // camera onto the floor - and the pull-in never passes that minimum either.
            float wallAt = cameraRig.NearestDistance + 4f;
            Assume.That(wallAt, Is.LessThan(far - 1f), "The rig must have room between its zoom limits for this to mean anything.");
            var wall = TestWall(wallAt);
            try
            {
                Assert.That(cameraRig.AppliedDistance, Is.LessThan(wallAt), "The boom must stop short of the wall.");
                Assert.That(cameraRig.AppliedDistance, Is.GreaterThanOrEqualTo(cameraRig.NearestDistance - 0.01f),
                    "...but never nearer than the player could zoom.");
                Assert.That(cameraRig.Distance, Is.EqualTo(far).Within(0.1f), "The distance the player asked for is untouched.");
            }
            finally
            {
                Object.Destroy(wall);
            }
            yield return null; yield return null;
            Assert.That(cameraRig.AppliedDistance, Is.EqualTo(cameraRig.Distance).Within(0.05f), "Clear again, the boom is back.");

            // Inside the zoom minimum, a wall is furniture beside the focus: ignored.
            wall = TestWall(cameraRig.NearestDistance * 0.5f);
            try
            {
                Assert.That(cameraRig.AppliedDistance, Is.EqualTo(cameraRig.Distance).Within(0.05f),
                    "Something nearer than the zoom minimum must not pull the camera in.");
            }
            finally
            {
                Object.Destroy(wall);
            }
        }

        private GameObject TestWall(float alongTheBoom)
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Occlusion test wall";
            Object.DestroyImmediate(wall.GetComponent<Renderer>());
            wall.transform.position = cameraRig.transform.position + cameraRig.transform.rotation * Vector3.back * alongTheBoom;
            wall.transform.rotation = cameraRig.transform.rotation;
            wall.transform.localScale = new Vector3(3f, 3f, 0.3f);
            Physics.SyncTransforms();
            cameraRig.SendMessage("LateUpdate");
            return wall;
        }

        private Mouse TestMouse()
        {
            if (testMouse == null) testMouse = InputSystem.AddDevice<Mouse>();
            cameraRig.ControlsEnabled = true;
            return testMouse;
        }
    }
}
