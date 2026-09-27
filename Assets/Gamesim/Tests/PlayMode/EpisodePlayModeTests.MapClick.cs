using System.Collections;
using System.Linq;
using Gamesim.House;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    public sealed partial class EpisodePlayModeTests
    {
        /// <summary>
        /// A click on the floor of the overview walks the player to where it was clicked.
        ///
        /// <para>The overview's lens is a blend of perspective and orthographic, set as the
        /// camera's projection by hand. <c>Camera.ScreenPointToRay</c> does not follow it, so the
        /// ray a click was turned into left the camera through a different patch of house from the
        /// one under the pointer. The controller now inverts the projection the frame is drawn with.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator Overview_AFloorClickGoesWhereItWasClicked()
        {
            director.ClosePanels();
            yield return null;
            Assert.That(director.ShowOverview(), Is.True);
            float deadline = Time.realtimeSinceStartup + 6f;
            while (Time.realtimeSinceStartup < deadline && !(cameraRig.HasShot && cameraRig.HasArrived(.1f) && !cameraRig.IsTravelling
                       && cameraRig.LensOrthographic > .995f))
                yield return null;
            yield return null;

            var camera = cameraRig.ViewCamera;
            // A patch of kitchen floor, clear and reachable, somewhere the HUD is not.
            var kitchen = SceneComponents<HouseRoomMarker>().Single(marker => marker.RoomName == "Kitchen").transform.position;
            Vector3 target = default; Vector2 screen = default; bool found = false;
            for (int i = 0; i < 24 && !found; i++)
            {
                var wanted = kitchen + Quaternion.Euler(0f, i * 45f, 0f) * Vector3.forward * (.8f + (i / 8) * .8f);
                if (!NavMesh.SamplePosition(wanted, out var hit, .3f, player.Agent.areaMask)) continue;
                var point = (Vector2)camera.WorldToScreenPoint(hit.position);
                var hits = new System.Collections.Generic.List<RaycastResult>();
                EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = point }, hits);
                if (hits.Count > 0) continue;
                var ray = HousePlayerController.ScreenRay(camera, point);
                if (!Physics.Raycast(ray, out var floor, 500f, HouseLayers.Pick, QueryTriggerInteraction.Ignore)
                    || floor.collider.GetComponentInParent<HouseWalkable>() == null
                    || Vector3.Distance(floor.point, hit.position) > .3f) continue;
                if (!player.TryMeasureRoute(floor.point, out _)) continue;
                target = floor.point; screen = point; found = true;
            }
            Assert.That(found, Is.True, "No clear, reachable kitchen floor in the overview to click.");
            var naive = camera.ScreenPointToRay(screen);
            Debug.Log("[Gamesim] overview click: ScreenPointToRay misses the drawn point by "
                + Vector3.Cross(naive.direction, target - naive.origin).magnitude.ToString("0.00") + " m");

            var mouse = InputSystem.AddDevice<Mouse>();
            try
            {
                InputSystem.QueueStateEvent(mouse, new MouseState { position = screen });
                yield return null;
                yield return ClickAt(mouse, screen);
                Assert.That(player.Agent.hasPath || player.HasArrived, Is.True, "The click walked the player.");
                Assert.That(Vector3.Distance(player.Agent.destination, target), Is.LessThan(.8f),
                    "The player goes where the pointer was on the map, not somewhere the camera's own ray thought it was.");
            }
            finally { InputSystem.RemoveDevice(mouse); }
        }

        /// <summary>
        /// Hovering a piece of furniture says what a click on it would do, in the click's own words,
        /// and the words go when the pointer leaves it.
        /// </summary>
        [UnityTest]
        public IEnumerator HouseLife_HoveringFurnitureSaysWhatAClickDoes()
        {
            director.ClosePanels();
            yield return null;
            var pool = HouseFurniture.InScene(player.gameObject.scene)
                .Single(anchor => anchor.VenueId == HouseFurniture.PoolAnchor);
            cameraRig.ClearSubject();
            cameraRig.MoveTo(pool.Position, 12f);
            float deadline = Time.realtimeSinceStartup + 6f;
            while (Time.realtimeSinceStartup < deadline && (cameraRig.Distance > 12.5f || cameraRig.IsTravelling)) yield return null;
            for (int i = 0; i < 5; i++) yield return null;

            var camera = cameraRig.ViewCamera;
            var screen = (Vector2)camera.WorldToScreenPoint(pool.Position + Vector3.up * .3f);
            Assert.That(Physics.Raycast(HousePlayerController.ScreenRay(camera, screen), out var hit, 500f, HouseLayers.Pick,
                QueryTriggerInteraction.Ignore) && hit.transform.IsChildOf(pool.transform.parent), Is.True,
                "The pointer is over the pool.");

            var mouse = InputSystem.AddDevice<Mouse>();
            try
            {
                InputSystem.QueueStateEvent(mouse, new MouseState { position = screen });
                yield return null;
                yield return null;
                var tip = director.GetComponentsInChildren<RectTransform>(true)
                    .LastOrDefault(rect => rect.name == Gamesim.Episode.EpisodeTravelBeacons.FurnitureTipName);
                Assert.That(tip != null && tip.gameObject.activeInHierarchy, Is.True, "Hovering the pool says what a click does.");
                Assert.That(tip.GetComponentInChildren<TMPro.TMP_Text>().text, Is.EqualTo(HouseFurniture.SwimCaption), "In the click's own words.");

                // A still pointer over a moving camera: the pool slides out from under it, and the
                // words go with it.
                cameraRig.MoveTo(pool.Position + new Vector3(12f, 0f, 12f), 12f);
                float moving = Time.realtimeSinceStartup + 4f;
                while (Time.realtimeSinceStartup < moving && tip.gameObject.activeInHierarchy) yield return null;
                Assert.That(tip.gameObject.activeInHierarchy, Is.False, "The camera moved the pool away from a still pointer.");
                cameraRig.MoveTo(pool.Position, 12f);
                moving = Time.realtimeSinceStartup + 4f;
                while (Time.realtimeSinceStartup < moving && !tip.gameObject.activeInHierarchy) yield return null;
                Assert.That(tip.gameObject.activeInHierarchy, Is.True, "And brought it back.");

                var away = new Vector2(camera.pixelRect.xMin + 6f, camera.pixelRect.center.y);
                InputSystem.QueueStateEvent(mouse, new MouseState { position = away });
                yield return null;
                yield return null;
                Assert.That(tip.gameObject.activeInHierarchy, Is.False, "And stops saying it when the pointer moves off.");
            }
            finally { InputSystem.RemoveDevice(mouse); }
        }
    }
}
