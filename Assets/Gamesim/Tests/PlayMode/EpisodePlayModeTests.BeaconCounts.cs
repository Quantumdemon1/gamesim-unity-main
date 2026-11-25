using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Episode;
using Gamesim.House;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    public sealed partial class EpisodePlayModeTests
    {
        /// <summary>
        /// Each room's icon says how many houseguests besides the player are in it - what the
        /// notebook's Who Is Where says - and the icon of the objective's next stop is ringed.
        /// </summary>
        [UnityTest]
        public IEnumerator Beacons_EachRoomSaysWhoIsInItAndTheNextStopIsRinged()
        {
            director.ClosePanels();
            director.SuspendNpcAutonomyForDiagnostics();
            var scope = ScopeOf(director);
            var state = director.Snapshot;
            string next = state.pendingDiary != null ? "Private" : director.ScreenRoom;
            int nextRoom = RoomLayers.IndexOf(next);
            var marker = SceneComponents<HouseRoomMarker>().Single(room => room.isActiveAndEnabled && room.RoomName == next);
            var doors = scope.Doorways.Where(door => door.First == nextRoom || door.Second == nextRoom)
                .OrderBy(door => (door.Middle - marker.transform.position).sqrMagnitude).ToArray();
            Assert.That(doors, Is.Not.Empty, next + " has a walkable entrance.");
            ScopeParkHouseguests(scope);
            var guest = SceneComponents<HouseNpc>().First(n => n.isActiveAndEnabled);
            ScopePlace(guest.transform, ScopeOpenFloor(scope, nextRoom), 1f);
            yield return PullBackOverTheHouse();
            cameraRig.SetReducedMotion(true);
            int from = -1;
            var tried = new List<string>();
            IEnumerator LookThroughTheEntrance()
            {
                var box = scope.Box(nextRoom);
                var centre = new Vector3(box.center.x, 0f, box.center.y);
                foreach (var door in doors)
                {
                    from = door.First == nextRoom ? door.Second : door.First;
                    WarpPlayer(ScopeOpenFloor(scope, from));
                    // The free-camera room cache and the notebook's occupant count poll real time.
                    float refreshedAt = Time.realtimeSinceStartup + .65f;
                    while (Time.realtimeSinceStartup < refreshedAt) yield return null;
                    var into = centre - door.Middle;
                    float toward = Mathf.Atan2(into.x, into.z) * Mathf.Rad2Deg;
                    foreach (float offset in new[] { 0f, -30f, 30f, -60f, 60f })
                    {
                        RoomViewSetYaw(toward + offset);
                        cameraRig.MoveTo(Vector3.Lerp(door.Middle, centre, .45f), 20f);
                        yield return null;
                        yield return null;
                        yield return null;
                        tried.Add($"{door}, yaw {cameraRig.Yaw:F0}, eye {cameraRig.ViewCamera.transform.position}");
                        if (director.TravelBeacons.TryPlacedAt(next, out _)) yield break;
                    }
                }
            }

            void CheckCountsAndRing()
            {
                var hud = director.GetComponentInChildren<EpisodeHud>(true);
                Assert.That(director.TravelBeacons.TryPlacedAt(next, out _), Is.True,
                    $"No visible {next} marker through its authored entrances. " + string.Join("; ", tried));
                AssertVisibleNeighbor(scope, from, next, hud);
                var notebook = director.WhoIsWhere();
                var roster = notebook.Single(room => room.Name == next).Occupants;
                Assert.That(roster != null && roster.Any(person => !person.IsPlayer && person.Id == guest.Id), Is.True,
                    $"{guest.DisplayName} ({guest.Id}) must be on the notebook roster for the ringed {next} marker.");
                var expected = notebook.ToDictionary(room => room.Name,
                    room => room.Occupants == null ? 0 : room.Occupants.Count(person => !person.IsPlayer));
                Assert.That(expected.Values.Sum(), Is.GreaterThan(0), "The notebook has houseguests to place.");
                var beacons = director.TravelBeacons;
                Assert.That(beacons, Is.Not.Null, "The icons were built.");
                int checkedRooms = 0, withSomebody = 0;
                foreach (var entry in expected)
                {
                    int shown = beacons.ShownCount(entry.Key);
                    if (shown < 0) continue;
                    checkedRooms++;
                    Assert.That(shown, Is.EqualTo(entry.Value), entry.Key + "'s icon counts the houseguests the notebook puts there.");
                    var badge = BeaconFor(entry.Key).Find(EpisodeTravelBeacons.BadgeName);
                    Assert.That(badge, Is.Not.Null, entry.Key + "'s icon has a badge.");
                    Assert.That(badge.gameObject.activeSelf, Is.EqualTo(entry.Value > 0), entry.Key + "'s badge shows only when somebody is there.");
                    if (entry.Value > 0)
                    {
                        withSomebody++;
                        Assert.That(badge.GetComponentInChildren<TMP_Text>().text, Is.EqualTo(entry.Value > 9 ? "9+" : entry.Value.ToString()),
                            entry.Key + "'s badge reads the count.");
                    }
                }
                Assert.That(checkedRooms, Is.GreaterThan(0), "Some room's icon is on screen to count.");
                Assert.That(withSomebody, Is.GreaterThan(0), "An icon on screen has somebody in its room to count.");

                Assert.That(expected[next], Is.Positive, guest.DisplayName + " stands on the destination floor.");
                Assert.That(beacons.NextStop, Is.EqualTo(next), "The objective's next stop is the ringed icon.");
                var ringed = BeaconFor(next);
                Assert.That(ringed.Find(EpisodeTravelBeacons.NextStopName), Is.Not.Null, "Its ring is lit.");
                Assert.That(beacons.ShownCount(next), Is.EqualTo(expected[next]), next + " must show its own roster count.");
                var nextBadge = ringed.Find(EpisodeTravelBeacons.BadgeName);
                Assert.That(nextBadge != null && nextBadge.gameObject.activeInHierarchy, Is.True, next + " must display its badge.");
                var displayed = nextBadge.GetComponentInChildren<TMP_Text>();
                Assert.That(displayed != null && displayed.isActiveAndEnabled, Is.True, next + " must display its badge's text.");
                Assert.That(displayed.text, Is.EqualTo(expected[next] > 9 ? "9+" : expected[next].ToString()), next);
                Assert.That(beacons.ShownCount(RoomLayers.RoomIds[from]), Is.EqualTo(-1), "The player's own room has no travel button.");
                foreach (var room in expected.Keys.Where(room => room != next))
                {
                    var other = BeaconFor(room);
                    if (other != null && other.gameObject.activeInHierarchy)
                        Assert.That(other.Find(EpisodeTravelBeacons.NextStopName), Is.Null, room + " is not the next stop, and is not ringed.");
                }

                // And the objective names the ringed place in the icon's own words: the episode
                // screen, as the rail's button and the icon call it, not the ceremony screen.
                if (next == director.ScreenRoom && !Gamesim.Simulation.EpisodeEngine.IsCompetition(state.phase))
                {
                    var objective = director.GetComponentsInChildren<RectTransform>()
                        .LastOrDefault(rect => rect.name == "Objective" && rect.gameObject.activeInHierarchy);
                    Assert.That(objective, Is.Not.Null, "The objective is on screen.");
                    var said = objective.GetComponentsInChildren<TMP_Text>().Select(text => text.text).FirstOrDefault(text => text.StartsWith("Next stop:"));
                    Assert.That(said, Is.EqualTo("Next stop: episode screen"));
                    Assert.That(EpisodeTravelBeacons.StationCaption, Does.EndWith("episode screen"), "The icon says the same.");
                }
            }

            if (Application.isBatchMode)
                yield return CaptureFraming("beacons-counts", settle: false, prepare: LookThroughTheEntrance,
                    inspect: _ => CheckCountsAndRing(), width: 2560, height: 1080);
            else
            {
                yield return LookThroughTheEntrance();
                CheckCountsAndRing();
            }

            // Keep the lens on the destination. A player in a disconnected room still has the fixed Play shortcut.
            int remote = Enumerable.Range(0, RoomLayers.Count).First(room => room != nextRoom &&
                !scope.TryDoorway(room, nextRoom, marker.transform.position, out _));
            WarpPlayer(ScopeOpenFloor(scope, remote));
            yield return new WaitForSecondsRealtime(.65f);
            Assert.That(director.TravelBeacons.TryPlacedAt(next, out _), Is.False);
            var shortcut = ButtonWithCaption(next == "Private" ? EpisodeHud.DiaryTravelCaption : EpisodeHud.PhaseTravelCaption(state.phase));
            Assert.That(shortcut.IsInteractable(), Is.True);
        }

        /// <summary>
        /// The fixed Play shortcut reaches the next stop from the far end of the house, where the
        /// episode screen itself is out of sight and has no floating hit target.
        /// </summary>
        [UnityTest]
        public IEnumerator Beacons_TheNextStopIsNeverLostOffTheScreen()
        {
            director.ClosePanels();
            director.SuspendNpcAutonomyForDiagnostics();
            yield return null;
            Assert.That(director.Snapshot.pendingDiary, Is.Null, "No diary is owed, so the next stop is the episode screen.");
            string next = director.ScreenRoom;
            var place = director.StationPosition + Vector3.up * EpisodeTravelBeacons.Height;
            var hud = director.GetComponentInChildren<EpisodeHud>(true);
            // The dollhouse's pin from 19 m out: the whole house held (RS5's room view never goes that far; its
            // own next stop is pinned by WayFinding_TheNextStopIsPinnedAndClickableAtEveryRoomViewDistance), and
            // today's heading with it, as the "Show the whole house" setting brings it back from the room view (D12):
            // the room view's first late pass turned the camera to the player's room's best heading (D4), and at
            // heading 0 the far end's shot has the screen's place behind the camera, not off the frame.
            RoomViewHoldTheDollhouse();
            cameraRig.EaseToItsOwnFraming();
            Assert.That(cameraRig.Yaw, Is.EqualTo(cameraRig.OwnYaw).Within(.01f), "Today's heading, the scene's own.");
            var far = SceneComponents<HouseRoomMarker>().Where(marker => marker.isActiveAndEnabled)
                .OrderByDescending(marker => (marker.transform.position - director.StationPosition).sqrMagnitude).First().transform.position;
            cameraRig.SetReducedMotion(true);
            bool outOfSight = false;
            float deadline = Time.realtimeSinceStartup + 6f;
            while (Time.realtimeSinceStartup < deadline)
            {
                cameraRig.MoveTo(far, EpisodeTravelBeacons.ShownFrom + 1f);
                yield return null;
                var natural = cameraRig.ViewCamera.WorldToScreenPoint(place);
                outOfSight = natural.z > .5f && (!cameraRig.ViewCamera.pixelRect.Contains(natural) || hud.Covers(natural));
                if (outOfSight && director.TravelBeacons != null && cameraRig.Distance >= EpisodeTravelBeacons.ShownFrom) break;
            }
            Assert.That(outOfSight, Is.True, "From the far end of the house the screen's own place is out of sight (heading "
                + cameraRig.transform.eulerAngles.y.ToString("0.0") + ").");
            // The shot holds still from here on: nothing is followed, so the icon stays where it is measured.
            yield return null;
            yield return null;

            Assert.That(BeaconFor(next).gameObject.activeInHierarchy, Is.False, "An unseen next stop has no floating hit target.");
            if (Application.isBatchMode) yield return CaptureFraming("beacons-fixed-play");
            // Capturing and restoring a different frame rebuilds the HUD.
            var shortcut = ButtonWithCaption(EpisodeHud.PhaseTravelCaption(director.Snapshot.phase));
            Assert.That(shortcut.IsInteractable(), Is.True, "The fixed shortcut remains available after restoring the view.");
            yield return ClickAt(TestMouse(), ScreenCentre((RectTransform)shortcut.transform));
            Assert.That(director.LastTravel, Is.Not.EqualTo(EpisodeDirector.TravelKind.None));
        }
    }
}
