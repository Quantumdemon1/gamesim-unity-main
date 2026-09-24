using System.Linq;
using Gamesim.House;
using Gamesim.Presentation;
using Gamesim.Simulation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

namespace Gamesim.Episode
{
    /// <summary>
    /// Getting about the house on an errand: to the episode screen, the diary room, a room.
    ///
    /// <para>How far decides how. A walk across a room, a run across the house, and past
    /// <see cref="HousePlayerController.WarpRouteMetres"/> no trip at all: the screen dips, the
    /// player is there, and the camera is on them. Nobody asking to be taken to the yard from the
    /// far bedroom wants to watch the whole house go by on the way.</para>
    ///
    /// <para>Only errands travel this way. A click on the floor walks or runs, whatever the
    /// distance - that is a place the player pointed at, not a place they asked to be - and a
    /// chase runs, because a houseguest being chased is still moving.</para>
    /// </summary>
    public sealed partial class EpisodeDirector
    {
        public enum TravelKind { None, Walk, Run, Warp }

        /// <summary>How the last errand got there: for the status line, and for the tests.</summary>
        public TravelKind LastTravel { get; private set; }

        /// <summary>How long the dip over a warp takes to clear, in seconds of real time.</summary>
        public const float TravelDipSeconds = .3f;

        private Canvas travelDipCanvas;
        private Image travelDip;
        private float travelDipStarted = float.NegativeInfinity;

        /// <summary>Whether a warp's dip is still on screen.</summary>
        public bool IsTravelDipShowing => travelDip != null && travelDip.enabled;

        /// <summary>
        /// Sends the player to <paramref name="destination"/> at the gait its route deserves, with
        /// the camera on them. False, and nothing moved, when there is no route there.
        /// </summary>
        private bool TryTravel(Vector3 destination)
        {
            LastTravel = TravelKind.None;
            if (player == null || !player.TryMeasureRoute(destination, out float metres)) return false;
            if (metres > HousePlayerController.WarpRouteMetres && player.TryWarpTo(destination))
            {
                LastTravel = TravelKind.Warp;
                // Straight onto them. A camera that eased across the house after a warped player
                // would play the whole trip the warp exists to skip.
                cameraRig?.CutTo(player.transform);
                BeginTravelDip();
                return true;
            }
            if (!player.TryTravelTo(destination)) return false;
            LastTravel = player.IsRunning ? TravelKind.Run : TravelKind.Walk;
            // Watch them go. Telling somebody to go somewhere and then leaving the camera behind is
            // how "walk to the highlighted room" became a hunt for your own player.
            cameraRig?.FocusSubject(player.transform, false);
            return true;
        }

        /// <summary>
        /// The dip: black at the moment of the warp, clearing over <see cref="TravelDipSeconds"/>.
        /// A hard cut from one side of the house to the other reads as a glitch; a blink reads as a
        /// cut. Reduced motion takes the cut without the blink.
        /// </summary>
        private void BeginTravelDip()
        {
            if (cameraRig != null && cameraRig.ReducedMotion) return;
            if (travelDip == null)
            {
                var root = new GameObject("Travel dip", typeof(RectTransform), typeof(Canvas));
                root.transform.SetParent(transform, false);
                travelDipCanvas = root.GetComponent<Canvas>();
                travelDipCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                // Over the house and its name plates, under the HUD: the chrome does not blink,
                // because the chrome did not go anywhere.
                travelDipCanvas.sortingOrder = 60;
                var image = new GameObject("Dip", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                image.rectTransform.SetParent(root.transform, false);
                image.rectTransform.anchorMin = Vector2.zero; image.rectTransform.anchorMax = Vector2.one;
                image.rectTransform.offsetMin = Vector2.zero; image.rectTransform.offsetMax = Vector2.zero;
                image.raycastTarget = false;
                travelDip = image;
            }
            travelDipStarted = Time.unscaledTime;
            travelDip.color = new Color(0f, 0f, 0f, 1f);
            travelDip.enabled = true;
        }

        private void TickTravelDip()
        {
            if (travelDip == null || !travelDip.enabled) return;
            float clear = Mathf.Clamp01((Time.unscaledTime - travelDipStarted) / TravelDipSeconds);
            travelDip.color = new Color(0f, 0f, 0f, 1f - Mathf.SmoothStep(0f, 1f, clear));
            if (clear >= 1f) travelDip.enabled = false;
        }

        /// <summary>
        /// Takes the player to a room: the diary room's chair for the private room, the episode
        /// screen for the room it stands in, and a clear spot on the floor for the rest. For a
        /// player who cannot move - evicted, on the jury - the camera goes instead.
        /// </summary>
        public void GoToRoom(string room)
        {
            if (!IsReady || blockedRecovery || string.IsNullOrEmpty(room)) return;
            var marker = RoomMarker(room);
            if (marker == null) return;
            if (!playerIsActive)
            {
                ClosePanels();
                cameraRig?.MoveTo(marker.transform.position, cameraRig.DesiredDistance);
                message = "Watching the " + RoomLabels.InSentence(room) + ".";
                Render();
                return;
            }
            if (room == "Private" && HasDiaryRoom) { GoToDiary(); return; }
            if (room == StationRoomId()) { GoToStation(); return; }

            CancelTravel();
            ClosePanels();
            EndDiaryVisit(true); CloseHouseActivities(true);
            bool going = TryRoomLanding(marker, out var landing) && TryTravel(landing);
            string name = RoomLabels.InSentence(room);
            message = !going ? "The " + name + " is not reachable from here."
                : LastTravel == TravelKind.Warp ? "In the " + name + "."
                : "Heading to the " + name + ".";
            Render();
        }

        private EpisodeTravelBeacons travelBeacons;
        private readonly System.Collections.Generic.Dictionary<string, HouseRoomMarker> beaconRooms
            = new System.Collections.Generic.Dictionary<string, HouseRoomMarker>();
        private float nextBeaconRoomCheck;
        private string beaconPlayerRoom;

        /// <summary>The icons over the rooms, once they have been built; null before.</summary>
        public EpisodeTravelBeacons TravelBeacons => travelBeacons;

        /// <summary>The room the phase's screen stands in, whose icon is the screen's.</summary>
        public string ScreenRoom => StationRoomId();

        /// <summary>
        /// Whether the house is the thing on screen: free roam, nothing open over it, no scripted
        /// shot, no ceremony, no competition, and the HUD up.
        /// </summary>
        private bool HouseIsTheView => IsReady && !blockedRecovery && !IsPanelOpen && !overviewOpen && !challengeActive
            && !CeremonyOverlays.OnScreen && !IsFramingCeremony && (voteReveal == null || !voteReveal.IsPlaying)
            && (takeover == null || !takeover.IsPlaying) && (keyCeremony == null || !keyCeremony.IsPlaying)
            && hud != null && hud.IsVisible && player != null
            && cameraRig != null && cameraRig.ControlsEnabled && !cameraRig.HasShot && !cameraRig.IsConversationFocused
            && (mainMenu == null || !mainMenu.IsShowing) && (castSelect == null || !castSelect.IsShowing)
            && (characterCreator == null || !characterCreator.IsShowing);

        private void TickTravelBeacons()
        {
            if (!HouseIsTheView)
            {
                if (travelBeacons != null)
                {
                    travelBeacons.Request(false, null, 1f, null, false, null, null);
                    travelBeacons.ShowFurnitureTip(null, default, 1f);
                }
                return;
            }
            if (travelBeacons == null)
            {
                travelBeacons = EpisodeTravelBeacons.Attach(gameObject);
                beaconRooms.Clear();
                foreach (var marker in RoomMarkers()) if (marker.isActiveAndEnabled) beaconRooms[marker.RoomName] = marker;
                travelBeacons.Build(beaconRooms.Values, PressTravelBeacon);
            }
            // Where the player stands changes at walking pace; four times a second is plenty.
            if (Time.unscaledTime >= nextBeaconRoomCheck)
            {
                nextBeaconRoomCheck = Time.unscaledTime + .25f;
                beaconPlayerRoom = HouseRoomQuery.TryCreate(gameObject.scene, out var rooms, out _)
                    && player.Agent != null && rooms.TryLocate(player.transform.position, player.Agent.radius, out var room) ? room : null;
            }
            travelBeacons.Request(true, cameraRig, largeText ? 1.2f : 1f, StationRoomId(),
                EpisodeEngine.IsCompetition(projected?.phase ?? EpisodePhase.Social), playerIsActive ? beaconPlayerRoom : null,
                BeaconAnchor, hud.Covers);
        }

        /// <summary>Where a room's icon floats: over the screen, over the diary chair, over the middle of the rest.</summary>
        private Vector3 BeaconAnchor(string room)
        {
            if (room == StationRoomId() && HouseRoomQuery.Finite(StationPosition)) return StationPosition;
            if (room == "Private" && HasDiaryRoom) return DiaryPosition;
            return beaconRooms.TryGetValue(room, out var marker) && marker != null ? marker.transform.position : Vector3.zero;
        }

        /// <summary>
        /// A room's icon was clicked. Within reach of the screen or the diary, it opens them - the
        /// icon over the thing is the way to use the thing; anywhere else it is a trip.
        /// </summary>
        public void PressTravelBeacon(string room)
        {
            if (!IsReady || blockedRecovery || string.IsNullOrEmpty(room)) return;
            if (room == StationRoomId() && CanUseStation() && TryOpenPhasePanel()) return;
            if (room == "Private" && CanUseDiary && TryOpenDiary()) return;
            GoToRoom(room);
        }

        private HouseRoomMarker RoomMarker(string room)
            => gameObject.scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<HouseRoomMarker>(true))
                .FirstOrDefault(marker => marker.RoomName == room && marker.isActiveAndEnabled);

        /// <summary>
        /// A spot in the room to arrive at: on that room's floor, clear of every body and prop, and
        /// reachable. The marker first, then a spiral out from it - the same search the spawn uses,
        /// for the same reason: a bare NavMesh sample once put a houseguest in a doorway.
        /// </summary>
        private bool TryRoomLanding(HouseRoomMarker marker, out Vector3 landing)
        {
            landing = default;
            if (player == null || player.Agent == null || !HouseRoomQuery.TryCreate(gameObject.scene, out var rooms, out _)) return false;
            var agent = player.Agent;
            var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
            var origin = marker.transform.position;
            for (int step = 0; step < 24; step++)
            {
                float angle = step * 137.5f * Mathf.Deg2Rad;
                float reach = step == 0 ? 0f : .6f + (step % 8) * .45f;
                var wanted = origin + new Vector3(Mathf.Cos(angle) * reach, 0f, Mathf.Sin(angle) * reach);
                if (!rooms.TrySampleFloor(wanted, agent.radius, filter, .25f, out var feet, out var name)
                    || name != marker.RoomName) continue;
                if (!rooms.HasCapsuleClearance(feet, agent.radius, agent.height, player.transform)) continue;
                if (!player.TryMeasureRoute(feet, out _)) continue;
                landing = feet;
                return true;
            }
            return false;
        }
    }
}
