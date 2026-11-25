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
using UnityEngine.UI;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace Gamesim.Tests.PlayMode
{
    public sealed partial class EpisodePlayModeTests
    {
        private Touchscreen testTouchscreen;
        private Touchscreen TestPhoneScreen() => testTouchscreen = InputSystem.AddDevice<Touchscreen>();

        private Vector2 PhoneFloor()
        {
            var events = EventSystem.current;
            var hits = new List<RaycastResult>();
            var camera = cameraRig.ViewCamera;
            for (int iy = 3; iy <= 7; iy++)
            for (int ix = 3; ix <= 7; ix++)
            {
                Vector2 at = camera.ViewportToScreenPoint(new Vector3(ix / 10f, iy / 10f, 1f));
                hits.Clear();
                events.RaycastAll(new PointerEventData(events) { position = at }, hits);
                if (hits.Any(hit => hit.module is GraphicRaycaster)) continue;
                if (!player.TryPointAt(HousePlayerController.ScreenRay(camera, at), out var point)) continue;
                if (point.collider.GetComponentInParent<HouseWalkable>() == null) continue;
                if (HouseFurniture.AtProp(player.gameObject.scene, point.transform, point.point) != null) continue;
                if (player.TryMeasureRoute(point.point, out float distance) && distance > .25f) return at;
            }
            Assert.Fail("Fixture needs a visible, reachable floor point clear of HUD and furniture.");
            return default;
        }

        private static void QueueFinger(Touchscreen screen, int id, Vector2 at, TouchPhase phase)
        {
            InputSystem.QueueStateEvent(screen, new TouchState { touchId = id, position = at, phase = phase });
        }

        [UnityTest]
        public IEnumerator PhoneHouse_QuickMenuTapAndCancelledContactCannotChooseAFloor()
        {
            yield return CameraInputInTheRoomView();
            var touch = player.GetComponent<HouseTouchInput>() ?? player.gameObject.AddComponent<HouseTouchInput>();
            var screen = TestPhoneScreen();
            int moves = 0, clicks = 0;
            void Moved() => moves++;
            player.DestinationChosen += Moved;
            var overlay = new GameObject("Touch ownership", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            try
            {
                var canvas = overlay.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 2000;
                var at = PhoneFloor();
                var shield = new GameObject("Close", typeof(RectTransform), typeof(Image), typeof(Button));
                var rect = (RectTransform)shield.transform;
                rect.SetParent(overlay.transform, false);
                rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                shield.GetComponent<Button>().onClick.AddListener(() => { clicks++; shield.SetActive(false); });
                yield return null;
                QueueFinger(screen, 1, at, TouchPhase.Began);
                QueueFinger(screen, 1, at, TouchPhase.Ended);
                yield return null;
                yield return null;
                Assert.That(clicks, Is.EqualTo(1));
                Assert.That(moves, Is.Zero);
                QueueFinger(screen, 2, at, TouchPhase.Began);
                yield return null;
                QueueFinger(screen, 2, at, TouchPhase.Canceled);
                yield return null;
                Assert.That(moves, Is.Zero);
                QueueFinger(screen, 3, at, TouchPhase.Began);
                yield return null;
                QueueFinger(screen, 3, at, TouchPhase.Ended);
                yield return null;
                yield return null;
                Assert.That(moves, Is.EqualTo(1), "The same unobstructed tap takes exactly one route.");
            }
            finally
            {
                player.DestinationChosen -= Moved;
                Object.Destroy(overlay);
                if (!Gamesim.Presentation.PhoneDisplay.Active) Object.Destroy(touch);
                InputSystem.RemoveDevice(screen);
            }
        }

        [UnityTest]
        public IEnumerator PhoneHouse_PinchZoomsWithoutWalkingOnRelease()
        {
            yield return CameraInputInTheRoomView();
            var touch = player.GetComponent<HouseTouchInput>() ?? player.gameObject.AddComponent<HouseTouchInput>();
            var screen = TestPhoneScreen();
            int moves = 0;
            void Moved() => moves++;
            player.DestinationChosen += Moved;
            try
            {
                var at = PhoneFloor();
                float before = cameraRig.DesiredDistance;
                var spread = Vector2.right * 8;
                QueueFinger(screen, 1, at - spread, TouchPhase.Began);
                QueueFinger(screen, 2, at + spread, TouchPhase.Began);
                yield return null;
                QueueFinger(screen, 1, at - spread * 2, TouchPhase.Moved);
                QueueFinger(screen, 2, at + spread * 2, TouchPhase.Moved);
                yield return null;
                yield return null;
                Assert.That(cameraRig.DesiredDistance, Is.LessThan(before - .1f));
                QueueFinger(screen, 1, at - spread * 2, TouchPhase.Ended);
                yield return null;
                QueueFinger(screen, 2, at + spread * 2, TouchPhase.Ended);
                yield return null;
                yield return null;
                Assert.That(moves, Is.Zero);
            }
            finally
            {
                player.DestinationChosen -= Moved;
                if (!Gamesim.Presentation.PhoneDisplay.Active) Object.Destroy(touch);
                InputSystem.RemoveDevice(screen);
            }
        }
    }
}
