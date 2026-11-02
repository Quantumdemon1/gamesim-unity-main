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
            // Nobody walks into or out of the player's talk range, so the talk prompt, and with it where the room view's icons
            // wait, is the same when the pointer is picked and when the camera brings the pool back under it (the claims'
            // second review: a prompt that came up on the way back could stand an icon under the still pointer).
            director.SuspendNpcAutonomyForDiagnostics();
            yield return null;
            var pool = HouseFurniture.InScene(player.gameObject.scene)
                .Single(anchor => anchor.VenueId == HouseFurniture.PoolAnchor);
            cameraRig.ClearSubject();
            // The free move to the pool is the room view of the yard (RS5), whose clamp narrows with the zoom
            // (HouseRoomView.Clamp): at 12 m it holds the focus within 6.1 m of the yard's middle and leaves the pool,
            // 11.2 m out, at the frame's side - under the HUD's right column on the batch's 640 x 480 frame, where a
            // pointer is over the card, not the pool. From 6.85 m in the clamp reaches it, so at 6 m the pool stands
            // mid-frame, as the free move framed it before the room view.
            const float poolView = 6f;
            cameraRig.MoveTo(pool.Position, poolView);
            float deadline = Time.realtimeSinceStartup + 6f;
            while (Time.realtimeSinceStartup < deadline && (cameraRig.Distance > poolView + .5f || cameraRig.IsTravelling)) yield return null;
            for (int i = 0; i < 5; i++) yield return null;

            var camera = cameraRig.ViewCamera;
            // A pointer over the pool and over nothing of the HUD's or the icons': a pointer over a card is over the card, and
            // the hover rightly names nothing. The pool's middle or - where an icon waits on it: the room view keeps every other
            // room's icon on screen (D17), and with the fixture's talk prompt up at the bottom they stand higher - a point along
            // the pool or across it, the first the interface's raycast finds clear and whose ray meets the pool, as Rs2bPointAt
            // picks a prop's point (the fixes' review).
            var screen = default(Vector2);
            bool overThePool = false;
            var chrome = new System.Collections.Generic.List<RaycastResult>();
            var along = pool.transform.forward;
            var across = pool.transform.right;
            foreach (var offset in new[] { Vector2.zero, new Vector2(1f, 0f), new Vector2(-1f, 0f), new Vector2(0f, .8f), new Vector2(0f, -.8f),
                new Vector2(2f, 0f), new Vector2(-2f, 0f), new Vector2(1f, .8f), new Vector2(-1f, .8f), new Vector2(1f, -.8f), new Vector2(-1f, -.8f) })
            {
                var seen = camera.WorldToScreenPoint(pool.Position + along * offset.x + across * offset.y + Vector3.up * .3f);
                if (seen.z <= 0f || !camera.pixelRect.Contains(seen)) continue;
                var point = (Vector2)seen;
                chrome.Clear();
                EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = point }, chrome);
                if (chrome.Count > 0) continue;
                if (!Physics.Raycast(HousePlayerController.ScreenRay(camera, point), out var hit, 500f, HouseLayers.Pick, QueryTriggerInteraction.Ignore)
                    || !hit.transform.IsChildOf(pool.transform.parent)) continue;
                screen = point;
                overThePool = true;
                break;
            }
            Assert.That(overThePool, Is.True, "A pointer over the pool, clear of the HUD and the icons.");
            chrome.Clear();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = screen }, chrome);
            Assert.That(chrome.Select(result => result.gameObject.name), Is.Empty, "The pointer over the pool is clear of the HUD and the icons.");

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
                // words go with it. The free move to the pool is a watching view of the yard (RS5), so the
                // camera goes to another room's: the private room's, a cut away from the pool.
                Assert.That(cameraRig.ViewRoom, Is.EqualTo(RoomLayers.IndexOf("Yard")), "The free move to the pool watches the yard.");
                var scope = ScopeOf(director);
                var privateBox = scope.Box(RoomLayers.IndexOf("Private"));
                cameraRig.MoveTo(new Vector3(privateBox.center.x, pool.Position.y, privateBox.center.y), 10f);
                float moving = Time.realtimeSinceStartup + 4f;
                while (Time.realtimeSinceStartup < moving && tip.gameObject.activeInHierarchy) yield return null;
                Assert.That(tip.gameObject.activeInHierarchy, Is.False, "The camera moved the pool away from a still pointer.");
                // Back to the same framing (the yard's heading, the pool its focus), under the still pointer.
                cameraRig.MoveTo(pool.Position, poolView);
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
