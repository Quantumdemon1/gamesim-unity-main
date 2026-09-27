using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Episode;
using Gamesim.House;
using Gamesim.Presentation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// The icons over the house: a click on a room's takes the player there, a click on the
    /// screen's or the diary's opens it when it is within reach, and the overview's room chips are
    /// a map. Driven through the real mouse and the real event system, because what was ever wrong
    /// with a click in this house was the routing, not the handler.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        private static readonly string[] OrdinaryRooms = { "Kitchen", "Living", "Bedroom", "Games", "HoH" };

        /// <summary>Pulls the camera back over the house, where the icons live, and waits for it to get there.</summary>
        private IEnumerator PullBackOverTheHouse()
        {
            cameraRig.ClearSubject();
            cameraRig.MoveTo(cameraRig.HouseCenter, 24f);
            float deadline = Time.realtimeSinceStartup + 6f;
            while (Time.realtimeSinceStartup < deadline && cameraRig.Distance < EpisodeTravelBeacons.ShownFrom + 1f) yield return null;
            yield return null;
            yield return null;
        }

        private RectTransform BeaconFor(string room) => director.GetComponentsInChildren<RectTransform>(true)
            .LastOrDefault(rect => rect.name == EpisodeTravelBeacons.BeaconPrefix + room);

        private static Vector2 ScreenCentre(RectTransform rect)
            => RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));

        /// <summary>A room icon on screen, clear of everything else, for a room the player is not in.</summary>
        private bool TryClearBeacon(out string room, out Vector2 screen)
        {
            var hits = new List<RaycastResult>();
            foreach (var candidate in OrdinaryRooms)
            {
                var beacon = BeaconFor(candidate);
                if (beacon == null || !beacon.gameObject.activeInHierarchy) continue;
                var point = ScreenCentre(beacon);
                hits.Clear();
                EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = point }, hits);
                if (hits.Count == 0 || !hits[0].gameObject.transform.IsChildOf(beacon)) continue;
                room = candidate; screen = point;
                return true;
            }
            room = null; screen = default;
            return false;
        }

        [UnityTest]
        public IEnumerator Beacons_AClickOnARoomsIconTakesThePlayerThereAndTheHoverSaysSo()
        {
            director.ClosePanels();
            yield return null;
            yield return PullBackOverTheHouse();
            Assert.That(director.TravelBeacons, Is.Not.Null, "The icons are built the first time the house is the view.");
            Assert.That(director.TravelBeacons.IsShowing, Is.True, "Pulled back over the house, the rooms carry their icons.");
            Assert.That(director.TravelBeacons.Count, Is.EqualTo(SceneComponents<HouseRoomMarker>().Count(m => m.isActiveAndEnabled)),
                "One icon a room.");
            Assert.That(TryClearBeacon(out var room, out var screen), Is.True,
                "No room's icon is on screen and clear of the HUD, so there is nothing to click.");
            string caption = EpisodeTravelBeacons.Caption(room, director.ScreenRoom, false);
            var button = BeaconFor(room).GetComponent<Button>();
            Assert.That(button.GetComponentsInChildren<TMP_Text>(true).Select(text => text.text), Does.Contain(caption),
                "The icon carries its caption, visible or not.");
            Assert.That(button.navigation.mode, Is.EqualTo(Navigation.Mode.None), "The icons never take the keyboard.");
            // None of them is under the HUD's chrome, where it would be half hidden - or, under the
            // chrome that takes no click, clickable without being seen.
            var hud = director.GetComponentInChildren<EpisodeHud>(true);
            // Not even half of one: every corner of every icon on screen is clear.
            var corners = new Vector3[4];
            foreach (var shown in director.GetComponentsInChildren<Button>()
                         .Where(b => b.name.StartsWith(EpisodeTravelBeacons.BeaconPrefix, System.StringComparison.Ordinal)))
            {
                Assert.That(hud.Covers(ScreenCentre((RectTransform)shown.transform)), Is.False, shown.name + " sits under the HUD.");
                ((RectTransform)shown.transform).GetWorldCorners(corners);
                foreach (var corner in corners)
                    Assert.That(hud.Covers(RectTransformUtility.WorldToScreenPoint(null, corner)), Is.False, shown.name + " is half under the HUD.");
            }

            var mouse = InputSystem.AddDevice<Mouse>();
            try
            {
                // The shot is still: nothing is followed, so the icon stays where it was measured.
                InputSystem.QueueStateEvent(mouse, new MouseState { position = screen });
                yield return null;
                yield return null;
                var tip = BeaconFor(room).Find("Tip");
                Assert.That(tip != null && tip.gameObject.activeInHierarchy, Is.True, "Hovering an icon says what a click on it does.");
                Assert.That(tip.GetComponentInChildren<TMP_Text>().text, Is.EqualTo(caption), "The hover names the click.");

                yield return ClickAt(mouse, screen);
                Assert.That(director.LastTravel, Is.Not.EqualTo(EpisodeDirector.TravelKind.None),
                    "Clicking the " + room + "'s icon should have sent the player there. Status: " + director.StatusMessage);
                Assert.That(HouseRoomQuery.TryCreate(player.gameObject.scene, out var rooms, out var why), Is.True, why);
                var goal = director.LastTravel == EpisodeDirector.TravelKind.Warp ? player.transform.position : player.Agent.destination;
                Assert.That(rooms.TryLocate(goal, player.Agent.radius, out var landing) ? landing : "nowhere", Is.EqualTo(room),
                    "The trip ends in the room whose icon was clicked.");
            }
            finally { InputSystem.RemoveDevice(mouse); }
        }

        /// <summary>
        /// An icon half under the HUD is not shown either: not just one whose middle is covered.
        /// Asked with chrome of the test's own whose top edge crosses one icon below its middle, so
        /// the middle is clear and the lower half is not - the case a check of the middle alone let
        /// through, half hidden under the status line.
        /// </summary>
        [UnityTest]
        public IEnumerator Beacons_AnIconHalfUnderTheHudIsNotShown()
        {
            director.ClosePanels();
            yield return null;
            yield return PullBackOverTheHouse();
            cameraRig.SetReducedMotion(true);
            Assert.That(TryClearBeacon(out var room, out var screen), Is.True, "An icon on screen and clear of the HUD to start from.");
            var icon = BeaconFor(room);
            var corners = new Vector3[4];
            icon.GetWorldCorners(corners);
            float half = (RectTransformUtility.WorldToScreenPoint(null, corners[1]).y - RectTransformUtility.WorldToScreenPoint(null, corners[0]).y) * .5f;
            Assert.That(half, Is.GreaterThan(4f), "The icon has a size on screen.");
            var hud = director.GetComponentInChildren<EpisodeHud>(true);
            var markers = SceneComponents<HouseRoomMarker>().Where(marker => marker.isActiveAndEnabled).ToDictionary(marker => marker.RoomName);
            System.Func<string, Vector3> where = name => name == director.ScreenRoom ? director.StationPosition
                : name == "Private" ? director.DiaryPosition : markers[name].transform.position;
            // The chrome's top edge a quarter of the icon below its middle.
            System.Func<Vector2, bool> band = point => hud.Covers(point) || point.y < screen.y - half * .5f;
            for (int frame = 0; frame < 3; frame++)
            {
                // After the director's own request each frame, before the icons are placed.
                director.TravelBeacons.Request(true, cameraRig, 1f, director.ScreenRoom, false, null, where, band);
                yield return null;
            }
            Assert.That(icon.gameObject.activeInHierarchy, Is.False, room + "'s icon, half under the chrome, is not shown.");
            Assert.That(band(screen), Is.False, "Though its middle is clear.");
        }

        [UnityTest]
        public IEnumerator Beacons_StayOffTheHouseCloseUpAndUnderAPanel()
        {
            director.ClosePanels();
            yield return null;
            yield return PullBackOverTheHouse();
            Assert.That(director.TravelBeacons.IsShowing, Is.True);

            // Close in on somebody: the icons would sit on the people the shot is about.
            cameraRig.FocusSubject(SceneComponents<HouseNpc>().First(actor => actor.gameObject.activeInHierarchy).transform);
            float deadline = Time.realtimeSinceStartup + 6f;
            while (Time.realtimeSinceStartup < deadline && cameraRig.Distance > EpisodeTravelBeacons.HiddenBelow - .5f) yield return null;
            yield return null;
            Assert.That(director.TravelBeacons.IsShowing, Is.False, "Close up, the house is a set, not a map.");
            Assert.That(director.GetComponentsInChildren<Button>().Where(b => b.name.StartsWith(EpisodeTravelBeacons.BeaconPrefix)), Is.Empty,
                "A hidden icon takes no click.");

            yield return PullBackOverTheHouse();
            Assert.That(director.TravelBeacons.IsShowing, Is.True);
            director.OpenJournal();
            yield return null;
            yield return null;
            Assert.That(director.TravelBeacons.IsShowing, Is.False, "A panel over the house hides what is on it.");
            director.ClosePanels();
            yield return null;
        }

        /// <summary>
        /// An icon the HUD's chrome would cover is not shown. Asked directly - which icon happens
        /// to fall under the Status line depends on where the camera is - by handing the icons a
        /// HUD that covers the whole screen for one frame.
        /// </summary>
        [UnityTest]
        public IEnumerator Beacons_AnIconUnderTheHudIsNotShown()
        {
            director.ClosePanels();
            yield return null;
            yield return PullBackOverTheHouse();
            var beacons = director.TravelBeacons;
            Assert.That(beacons.IsShowing, Is.True);
            // After the director's Update has asked for this frame's icons and before they are
            // placed: the test's request is the one LateUpdate places.
            beacons.Request(true, cameraRig, 1f, director.ScreenRoom, false, null,
                room => SceneComponents<HouseRoomMarker>().First(marker => marker.RoomName == room).transform.position,
                underChrome: _ => true);
            yield return null;
            Assert.That(beacons.IsShowing, Is.False, "Under the chrome, no icon.");
            yield return null;
            yield return null;
            Assert.That(beacons.IsShowing, Is.True, "And back as soon as the real HUD says the house is clear.");
        }

        /// <summary>
        /// Within reach, the icon over the episode screen opens it, and the diary room's opens the
        /// diary: the icon over the thing is the way to use the thing.
        /// </summary>
        [UnityTest]
        public IEnumerator Beacons_TheScreenAndDiaryIconsOpenThemWithinReach()
        {
            director.ClosePanels();
            WarpPlayer(director.StationPosition);
            yield return null;
            director.PressTravelBeacon(director.ScreenRoom);
            Assert.That(director.IsPanelOpen, Is.True, "Standing at the screen, its icon opens it.");
            director.ClosePanels();
            yield return null;

            WarpPlayer(director.DiaryPosition);
            yield return null;
            Assert.That(director.CanUseDiary, Is.True);
            director.PressTravelBeacon("Private");
            Assert.That(director.IsDiaryOpen, Is.True, "Standing at the diary room, its icon opens the diary.");
            director.ClosePanels();
            yield return null;
        }

        /// <summary>The overview is a map: a click on a room's name takes the player there.</summary>
        [UnityTest]
        public IEnumerator Beacons_TheOverviewsRoomChipsAreAMap()
        {
            director.ClosePanels();
            yield return null;
            Assert.That(director.ShowOverview(), Is.True);
            float deadline = Time.realtimeSinceStartup + 6f;
            // The lens eases into its orthographic look after the pivot has arrived, and the chips
            // move on screen until it has: a chip measured before then is clicked where it was.
            while (Time.realtimeSinceStartup < deadline && !(cameraRig.HasShot && cameraRig.HasArrived(.1f) && !cameraRig.IsTravelling
                       && cameraRig.LensOrthographic > .995f))
                yield return null;
            yield return null;

            var camera = cameraRig.ViewCamera;
            var hits = new List<RaycastResult>();
            string room = null; Vector2 screen = default;
            var seen = new List<string>();
            foreach (var candidate in OrdinaryRooms)
            {
                var chip = director.GetComponentsInChildren<RectTransform>(true)
                    .LastOrDefault(rect => rect.name == RoomLabels.HotspotPrefix + candidate && rect.gameObject.activeInHierarchy);
                if (chip == null) { seen.Add(candidate + ": no chip"); continue; }
                var world = director.GetComponentsInChildren<RectTransform>(true).LastOrDefault(rect => rect.name == candidate + " label");
                // The hotspot stands where the chip is drawn: the lens's own projection says so.
                var point = RectTransformUtility.WorldToScreenPoint(null, chip.TransformPoint(chip.rect.center));
                if (world != null && Vector2.Distance(point, camera.WorldToScreenPoint(world.position)) > 12f)
                { seen.Add(candidate + ": hotspot at " + point.ToString("0") + " is not over its chip"); continue; }
                hits.Clear();
                EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = point }, hits);
                seen.Add(candidate + " at " + point.ToString("0") + ": " + (hits.Count == 0 ? "nothing" : hits[0].gameObject.name + "/" + hits[0].module.name));
                if (hits.Count == 0 || !hits[0].gameObject.transform.IsChildOf(chip)) continue;
                room = candidate; screen = point;
                break;
            }
            Assert.That(room, Is.Not.Null, "No room's chip on the overview is clear to click: " + string.Join("; ", seen)
                + " (screen " + Screen.width + "x" + Screen.height + ", lens " + cameraRig.LensOrthographic.ToString("0.00") + ")");

            var mouse = InputSystem.AddDevice<Mouse>();
            try
            {
                InputSystem.QueueStateEvent(mouse, new MouseState { position = screen });
                yield return null;
                yield return null;
                yield return ClickAt(mouse, screen);
                if (director.IsOverview)
                {
                    hits.Clear();
                    EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = screen }, hits);
                    Assert.Fail("Going somewhere from the map leaves the map. After the click on the " + room + " at "
                        + screen.ToString("0") + ": hits now [" + string.Join(", ", hits.Select(h => h.gameObject.name + "@" + h.module.GetType().Name)) + "]"
                        + ", selected " + (EventSystem.current.currentSelectedGameObject != null ? EventSystem.current.currentSelectedGameObject.name : "none")
                        + ", module " + (EventSystem.current.currentInputModule != null ? EventSystem.current.currentInputModule.GetType().Name : "none")
                        + ", status '" + director.StatusMessage + "', shot " + cameraRig.HasShot + ".");
                }
                Assert.That(director.LastTravel, Is.Not.EqualTo(EpisodeDirector.TravelKind.None),
                    "Clicking the " + room + " on the map should have sent the player there. Status: " + director.StatusMessage);
            }
            finally { InputSystem.RemoveDevice(mouse); }
        }
    }
}
