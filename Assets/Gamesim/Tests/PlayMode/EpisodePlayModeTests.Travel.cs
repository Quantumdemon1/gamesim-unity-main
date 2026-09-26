using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Episode;
using Gamesim.House;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// Getting about the house: how an errand travels, and the chrome that must not stand between
    /// a click and the house.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        /// <summary>
        /// An errand picks how to get there from the route: a walk across a room, a run across the
        /// house, and past twenty metres no trip at all - the player is simply there.
        ///
        /// <para>The buttons used to take whichever gait the last move left behind. A run cut
        /// short stayed a run to a screen three metres away, and anything else was a stroll from
        /// the far end of the yard.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator Travel_AnErrandWalksNearRunsFurtherAndIsSimplyThereFromAfar()
        {
            director.ClosePanels();
            yield return null;
            var station = director.StationPosition;

            WarpPlayer(new Vector3(0f, 0f, 14f));
            yield return null;
            Assert.That(RouteMetres(player.transform.position, station), Is.GreaterThan(HousePlayerController.WarpRouteMetres),
                "From the yard the screen is across the whole house.");
            ButtonWithCaption("Go to episode screen").onClick.Invoke();
            Assert.That(director.LastTravel, Is.EqualTo(EpisodeDirector.TravelKind.Warp), "A trip across the whole house is not a trip.");
            Assert.That(Vector3.Distance(player.transform.position, station), Is.LessThan(1f), "The player is at the screen.");
            Assert.That(cameraRig.FocusedSubject, Is.SameAs(player.transform), "The camera is on them.");
            if (!cameraRig.ReducedMotion) Assert.That(director.IsTravelDipShowing, Is.True, "The cut is a blink, not a jump.");
            yield return null;
            Assert.That(director.TryOpenPhasePanel(), Is.True, "Arriving by warp is arriving: the screen opens.");
            director.ClosePanels();
            yield return null;

            WarpPlayer(OnFootFrom(station, 9f, 18f));
            yield return null;
            ButtonWithCaption("Go to episode screen").onClick.Invoke();
            yield return null;
            Assert.That(director.LastTravel, Is.EqualTo(EpisodeDirector.TravelKind.Run),
                "A trip of " + player.RouteMetres.ToString("0") + " m is a run.");
            Assert.That(player.IsRunning, Is.True);

            // Still running from that trip when the next one starts: the gait is chosen again, not
            // inherited.
            WarpPlayer(OnFootFrom(station, 1.5f, 5f));
            yield return null;
            ButtonWithCaption("Go to episode screen").onClick.Invoke();
            yield return null;
            Assert.That(director.LastTravel, Is.EqualTo(EpisodeDirector.TravelKind.Walk),
                "A trip of " + player.RouteMetres.ToString("0.0") + " m is a walk, whatever the last trip was.");
            Assert.That(player.IsRunning, Is.False);

            // At a walk, the walk plays at the body's pace: 2.2 m/s over the take's 1.7.
            var visual = player.GetComponent<Gamesim.Presentation.CharacterPresentation>();
            // The pace at the walk's height, over the whole walk: a reading taken a set number of
            // frames in - fractions of a millisecond each in batchmode - caught the walker still
            // speeding up, or on a short errand already slowing for the door. The pace follows the
            // body's measured ground speed, which trails the agent's, so it is its own peak that counts.
            float pacing = Time.realtimeSinceStartup + 3f, fastest = 0f, pace = 0f;
            while (Time.realtimeSinceStartup < pacing && !player.HasArrived)
            {
                fastest = Mathf.Max(fastest, player.Agent.velocity.magnitude);
                pace = Mathf.Max(pace, visual.Pace);
                yield return null;
            }
            Assert.That(fastest, Is.GreaterThan(2f), "The walk reaches walking speed.");
            Assert.That(pace, Is.EqualTo(2.2f / Gamesim.Presentation.CharacterPresentation.WalkTakeSpeed).Within(.12f),
                "The steps keep up with the floor at " + fastest.ToString("0.00") + " m/s.");
        }

        /// <summary>
        /// The diary room's button watches the player go, as the episode screen's does, and from
        /// across the house it puts them at the door with the diary ready to open.
        /// </summary>
        [UnityTest]
        public IEnumerator Travel_TheDiaryRoomButtonFollowsThePlayer()
        {
            director.ClosePanels();
            yield return null;
            Assert.That(director.HasDiaryRoom, Is.True);
            WarpPlayer(OnFootFrom(director.DiaryPosition, 9f, 18f));
            cameraRig.ClearSubject();
            yield return null;
            ButtonWithCaption(EpisodeHud.DiaryTravelCaption).onClick.Invoke();
            yield return null;
            Assert.That(player.Agent.hasPath, Is.True, "The diary room should be reachable.");
            Assert.That(cameraRig.FocusedSubject, Is.SameAs(player.transform),
                "The camera stayed where it was and the player walked out of the frame.");
            Assert.That(director.LastTravel, Is.EqualTo(EpisodeDirector.TravelKind.Run));

            director.ClosePanels();
            WarpPlayer(FarthestRoomFrom(director.DiaryPosition));
            cameraRig.ClearSubject();
            yield return null;
            ButtonWithCaption(EpisodeHud.DiaryTravelCaption).onClick.Invoke();
            yield return null;
            Assert.That(director.LastTravel, Is.EqualTo(EpisodeDirector.TravelKind.Warp));
            Assert.That(director.CanUseDiary, Is.True, "Arriving by warp is arriving: the diary opens.");
            Assert.That(cameraRig.FocusedSubject, Is.SameAs(player.transform));
        }

        /// <summary>
        /// Every room can be gone to, and lands the player on its own floor, clear of the furniture.
        /// The private room goes to the diary room's door and the nomination room to the screen, so
        /// that E works on arrival.
        /// </summary>
        [UnityTest]
        public IEnumerator Travel_EveryRoomCanBeGoneToAndLandsOnItsOwnFloor()
        {
            director.ClosePanels();
            yield return null;
            Assert.That(HouseRoomQuery.TryCreate(player.gameObject.scene, out var rooms, out var why), Is.True, why);
            var markers = SceneComponents<HouseRoomMarker>().Where(marker => marker.isActiveAndEnabled).ToArray();
            Assert.That(markers.Length, Is.GreaterThanOrEqualTo(8), "The house has its eight rooms.");
            var failures = new List<string>();
            // Game time, not frames: a batchmode frame is a fraction of a millisecond, and a run
            // across the house is seconds of it.
            Time.captureDeltaTime = 1f / 30f;
            try
            {
                foreach (var marker in markers)
                {
                    // From the yard to the rooms far from it and from the screen to the rest, so the
                    // rooms are reached by warps and by runs both.
                    var start = Vector3.Distance(marker.transform.position, new Vector3(0f, 0f, 14f)) > 12f
                        ? new Vector3(0f, 0f, 14f) : director.StationPosition;
                    director.ClosePanels();
                    WarpPlayer(start);
                    yield return null;
                    director.GoToRoom(marker.RoomName);
                    for (int frame = 0; frame < 600 && !player.HasArrived; frame++) yield return null;
                    if (!player.HasArrived) { failures.Add(marker.RoomName + ": never arrived"); continue; }
                    if (!rooms.TryLocate(player.transform.position, player.Agent.radius, out var landed))
                        failures.Add(marker.RoomName + ": arrived off every floor");
                    else if (landed != marker.RoomName)
                        failures.Add(marker.RoomName + ": arrived in " + landed);
                    if (marker.RoomName == "Private" && !director.CanUseDiary)
                        failures.Add("Private: arrived where the diary cannot be opened");
                }
            }
            finally { Time.captureDeltaTime = 0f; }
            Assert.That(failures, Is.Empty, string.Join("; ", failures));
        }

        /// <summary>A warp is refused while an activity owns the player's movement.</summary>
        [UnityTest]
        public IEnumerator Travel_AWarpIsRefusedWhileAnActivityOwnsThePlayer()
        {
            director.ClosePanels();
            yield return null;
            var owner = new object();
            Assert.That(player.TryBeginActivityMove(owner, OnFootFrom(player.transform.position, 1.5f, 5f), out var reason), Is.True, reason);
            var before = player.transform.position;
            Assert.That(player.TryWarpTo(director.StationPosition), Is.False, "An activity owns the player's movement.");
            Assert.That(player.transform.position, Is.EqualTo(before));
            player.ReleaseActivityMove(owner);
            yield return null;
        }

        /// <summary>
        /// Chrome that only says something takes no click. The lower third spans the middle of the
        /// frame over the floor the player walks on, and it ate every click on that floor.
        /// </summary>
        [UnityTest]
        public IEnumerator Travel_DisplayOnlyChromeLetsAClickThroughToTheHouse()
        {
            director.ClosePanels();
            director.FollowHouseguest(SceneComponents<HouseNpc>()
                .First(actor => actor.gameObject.activeInHierarchy).Id);
            yield return null;
            yield return null;
            Canvas.ForceUpdateCanvases();

            var hits = new List<RaycastResult>();
            foreach (var name in new[] { "Status", "Week chip", "House pill", EpisodeHud.FollowChipName })
            {
                var chrome = director.GetComponentsInChildren<RectTransform>(true)
                    .LastOrDefault(rect => rect.name == name && rect.gameObject.activeInHierarchy);
                Assert.That(chrome, Is.Not.Null, name + " is on screen in free roam.");
                var corners = new Vector3[4];
                chrome.GetWorldCorners(corners);
                var canvas = chrome.GetComponentInParent<Canvas>().rootCanvas;
                var eye = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
                Vector2 low = RectTransformUtility.WorldToScreenPoint(eye, corners[0]);
                Vector2 high = RectTransformUtility.WorldToScreenPoint(eye, corners[2]);
                foreach (var point in new[] { (low + high) / 2f, Vector2.Lerp(low, high, .1f), Vector2.Lerp(low, high, .9f) })
                {
                    hits.Clear();
                    EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = point }, hits);
                    // Whatever is behind it may take the click - a room's icon, the floor - but not the chrome.
                    Assert.That(hits.Where(hit => hit.gameObject.transform.IsChildOf(chrome)).Select(hit => hit.gameObject.name), Is.Empty,
                        name + " says something and does nothing, so a click on it belongs to the house behind it.");
                }
            }
        }

        private float RouteMetres(Vector3 from, Vector3 to)
        {
            var route = new NavMeshPath();
            Assert.That(NavMesh.SamplePosition(from, out var start, .75f, player.Agent.areaMask), Is.True);
            Assert.That(NavMesh.SamplePosition(to, out var end, .75f, player.Agent.areaMask), Is.True);
            Assert.That(NavMesh.CalculatePath(start.position, end.position, player.Agent.areaMask, route)
                && route.status == NavMeshPathStatus.PathComplete, Is.True, "No route from " + from + " to " + to + ".");
            float length = 0f;
            for (int i = 0; i + 1 < route.corners.Length; i++) length += Vector3.Distance(route.corners[i], route.corners[i + 1]);
            return length;
        }

        /// <summary>The room marker with the longest walk to <paramref name="place"/>, which must be a warp's worth.</summary>
        private Vector3 FarthestRoomFrom(Vector3 place)
        {
            var farthest = SceneComponents<HouseRoomMarker>().Where(marker => marker.isActiveAndEnabled)
                .Select(marker => marker.transform.position)
                .Where(point => NavMesh.SamplePosition(point, out _, .75f, player.Agent.areaMask))
                .OrderByDescending(point => RouteMetres(point, place)).First();
            Assert.That(RouteMetres(farthest, place), Is.GreaterThan(HousePlayerController.WarpRouteMetres),
                "Nowhere in the house is a warp's walk from " + place + ".");
            return farthest;
        }

        /// <summary>A reachable spot whose walk to <paramref name="place"/> is between the two lengths.</summary>
        private Vector3 OnFootFrom(Vector3 place, float shortest, float longest)
        {
            var route = new NavMeshPath();
            for (float radius = shortest; radius <= longest + 4f; radius += 1f)
                for (int step = 0; step < 16; step++)
                {
                    var candidate = place + Quaternion.Euler(0f, step * 22.5f, 0f) * Vector3.forward * radius;
                    if (!NavMesh.SamplePosition(candidate, out var hit, .5f, player.Agent.areaMask)) continue;
                    if (!NavMesh.CalculatePath(hit.position, place, player.Agent.areaMask, route)
                        || route.status != NavMeshPathStatus.PathComplete) continue;
                    float length = 0f;
                    for (int i = 0; i + 1 < route.corners.Length; i++) length += Vector3.Distance(route.corners[i], route.corners[i + 1]);
                    if (length > shortest && length < longest) return hit.position;
                }
            Assert.Fail("Nowhere between " + shortest + " and " + longest + " m on foot from " + place + ".");
            return place;
        }

        /// <summary>
        /// A trip to a room on foot ends by saying where the player is and who else is there, in
        /// the notebook's words - first names, as the overview's column gives them - rather than
        /// going on saying the player is heading there after they have arrived.
        /// </summary>
        [UnityTest]
        public IEnumerator Travel_ArrivingOnFootSaysWhoElseIsThere()
        {
            director.ClosePanels();
            // Nobody wanders in or out between the arrival and the check.
            director.SuspendNpcAutonomyForDiagnostics();
            yield return null;
            Assert.That(HouseRoomQuery.TryCreate(player.gameObject.scene, out var rooms, out var why), Is.True, why);
            string mine = rooms.TryLocate(player.transform.position, player.Agent.radius, out var standing) ? standing : null;
            var target = SceneComponents<HouseRoomMarker>()
                .Where(marker => marker.isActiveAndEnabled && marker.RoomName != mine && marker.RoomName != "Private" && marker.RoomName != director.ScreenRoom)
                .Select(marker => (marker, metres: RouteMetres(player.transform.position, marker.transform.position)))
                .Where(entry => entry.metres < HousePlayerController.WarpRouteMetres - 2f)
                .OrderBy(entry => entry.metres).Select(entry => entry.marker).FirstOrDefault();
            Assert.That(target, Is.Not.Null, "A room within a walk or a run.");
            player.Agent.speed = 20; player.Agent.acceleration = 100;
            string name = Gamesim.Presentation.RoomLabels.InSentence(target.RoomName);

            director.GoToRoom(target.RoomName);
            Assert.That(director.LastTravel, Is.EqualTo(EpisodeDirector.TravelKind.Walk).Or.EqualTo(EpisodeDirector.TravelKind.Run), "On foot.");
            Assert.That(director.StatusMessage, Is.EqualTo("Heading to the " + name + "."));
            float deadline = Time.realtimeSinceStartup + 15f;
            while (Time.realtimeSinceStartup < deadline && !director.StatusMessage.StartsWith("In the ")) yield return null;

            var here = director.WhoIsWhere().Single(room => room.Occupants != null && room.Occupants.Any(person => person.IsPlayer));
            Assert.That(here.Name, Is.EqualTo(target.RoomName), "The player is in the room they asked for.");
            var others = here.Occupants.Where(person => !person.IsPlayer).Select(person => person.Name.Split(' ')[0]).ToArray();
            string company = others.Length == 0 ? "You have this room to yourself."
                : others.Length + (others.Length == 1 ? " houseguest here: " : " houseguests here: ") + string.Join(", ", others);
            Assert.That(director.StatusMessage, Is.EqualTo("In the " + name + ".  " + company),
                "Arrived, the status says where they are and who else is there.");
        }
    }
}
