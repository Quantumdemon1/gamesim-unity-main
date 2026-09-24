using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Episode;
using Gamesim.House;
using NUnit.Framework;
using TMPro;
using UnityEngine;
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
            // Nobody wanders between the count and the check.
            director.SuspendNpcAutonomyForDiagnostics();
            // The player stands by the episode screen, whose icon shows even over the player's own
            // room: so there is an icon on which counting the player would show.
            WarpPlayer(director.StationPosition);
            yield return null;
            yield return PullBackOverTheHouse();
            float settle = Time.realtimeSinceStartup + 1f;
            while (Time.realtimeSinceStartup < settle) yield return null;

            var expected = director.WhoIsWhere().ToDictionary(room => room.Name,
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

            // The ring follows the objective: the diary when one is owed, the screen otherwise.
            var state = director.Snapshot;
            string next = state.pendingDiary != null ? "Private" : director.ScreenRoom;
            Assert.That(beacons.NextStop, Is.EqualTo(next), "The objective's next stop is the ringed icon.");
            // Looked at from over the place itself, so its icon is on screen to be ringed. The move
            // lands at once, and is made again while waiting: whatever else frames the house in
            // the meantime - an arrival, a conversation - must not leave the place off screen.
            var over = next == "Private" ? director.DiaryPosition : director.StationPosition;
            cameraRig.SetReducedMotion(true);
            var ringed = BeaconFor(next);
            float look = Time.realtimeSinceStartup + 6f;
            while (Time.realtimeSinceStartup < look && !(ringed != null && ringed.gameObject.activeInHierarchy
                       && cameraRig.Distance >= EpisodeTravelBeacons.ShownFrom))
            {
                cameraRig.MoveTo(over, 22f);
                yield return null;
                ringed = BeaconFor(next);
            }
            yield return null;
            Assert.That(ringed != null && ringed.gameObject.activeInHierarchy, Is.True, "The next stop's icon is on screen. Next stop '"
                + next + "', screen room '" + director.ScreenRoom + "', station " + director.StationPosition + ", distance " + cameraRig.Distance
                + ", on screen at " + cameraRig.ViewCamera.WorldToScreenPoint(director.StationPosition + Vector3.up * 2.9f)
                + " of " + cameraRig.ViewCamera.pixelRect + ", house centre " + cameraRig.HouseCenter
                + ", showing " + beacons.IsShowing + ", icons: " + string.Join(", ", director.GetComponentsInChildren<RectTransform>(true)
                    .Where(rect => rect.name.StartsWith(EpisodeTravelBeacons.BeaconPrefix))
                    .Select(rect => rect.name.Substring(EpisodeTravelBeacons.BeaconPrefix.Length) + (rect.gameObject.activeInHierarchy ? "*" : ""))));
            Assert.That(ringed.Find(EpisodeTravelBeacons.NextStopName), Is.Not.Null, "Its ring is lit.");
            if (next == director.ScreenRoom)
            {
                // The player's own room, on screen: everybody there but the player.
                yield return ForSeconds(.6f);
                var screenRoom = director.WhoIsWhere().Single(room => room.Name == next);
                Assert.That(screenRoom.Occupants.Any(person => person.IsPlayer), Is.True, "The player stands in the screen's room.");
                Assert.That(beacons.ShownCount(next), Is.EqualTo(screenRoom.Occupants.Count(person => !person.IsPlayer)),
                    "Its icon counts the others there, not the player.");
            }
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
            // For the eye: the counts and the ring over the house.
            if (Application.isBatchMode) yield return CaptureFraming("beacons-counts");
        }
    }
}
