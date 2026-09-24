using System;
using System.Collections;
using System.Linq;
using Gamesim.House;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    public sealed partial class EpisodePlayModeTests
    {
        /// <summary>
        /// In the hot tub, the housemate the player gets on with best comes over and takes the
        /// other seat, and gets out when the player does. Chosen by the season's own scores, the id
        /// breaking a tie: nothing drawn, nothing saved.
        /// </summary>
        [UnityTest]
        public IEnumerator HouseLife_AFriendJoinsInTheHotTub()
        {
            var seats = PlacesFor(HouseFurnitureActivity.Soak);
            var before = director.Snapshot;
            // No new pairings while this runs: a conversation the house starts takes the companion
            // out of the tub, as it should, and there would be nobody to watch climb out.
            typeof(Gamesim.Episode.EpisodeDirector).GetField("npcApproachDiagnosticsSuppressed",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(director, true);
            Time.captureDeltaTime = 1f / 30f;
            try
            {
                yield return BeginInHouse(seats[0], HouseFurnitureActivity.Soak);
                float deadline = Time.realtimeSinceStartup + 20f;
                while (Time.realtimeSinceStartup < deadline && director.CompanionId == null) yield return null;
                Assert.That(director.CompanionId, Is.Not.Null, "Somebody comes to keep the player company.");
                // Their arrival re-renders the HUD from inside the director's frame, with the prompt
                // up - the frame the HUD rewires its Tab ring. E is the prompt's key; it stays out.
                yield return null;
                var prompt = ButtonWithCaptionOrNull(Gamesim.Episode.EpisodeHud.InteractCaption);
                Assert.That(prompt, Is.Not.Null, "In the tub, the prompt offers getting out.");
                Assert.That(prompt.navigation.mode, Is.EqualTo(UnityEngine.UI.Navigation.Mode.None), "The prompt is never a stop in the Tab ring.");
                var state = director.Snapshot;
                // Liking both ways: the player's reading of them and theirs of the player.
                var ranked = state.Active.Where(c => !c.isPlayer && state.Score(state.playerId, c.id) >= 0 && state.Score(c.id, state.playerId) >= 0)
                    .OrderByDescending(c => state.Score(state.playerId, c.id)).ThenBy(c => c.id, StringComparer.Ordinal)
                    .Select(c => c.id).ToList();
                // The best-liked housemate, unless the house had them busy - then the next.
                Assert.That(ranked, Does.Contain(director.CompanionId), "Somebody who likes the player, not somebody who does not.");
                Assert.That(director.StatusMessage, Does.Contain("joins you in the hot tub"), "The house says who has come.");
                if (director.CompanionId != ranked[0])
                    Debug.Log("[Gamesim] company: " + ranked[0] + " was busy; " + director.CompanionId + " came instead");
                var friend = SceneComponents<HouseNpc>().Single(npc => npc.Id == director.CompanionId);
                var pose = friend.GetComponent<HouseFurniturePose>();
                deadline = Time.realtimeSinceStartup + 20f;
                while (Time.realtimeSinceStartup < deadline && !(pose != null && pose.IsPerforming))
                {
                    yield return null;
                    pose = friend.GetComponent<HouseFurniturePose>();
                }
                Assert.That(pose != null && pose.IsPerforming, Is.True, "They reach the tub and sit.");
                Assert.That(pose.Anchor, Is.SameAs(seats[1]), "In the other seat.");
                if (Application.isBatchMode) { cameraRig.FocusSubject(player.transform); yield return CaptureFraming("house-life-company"); }

                // Getting out is a climb for both of them, so the companion's is there to see. Both
                // bodies, and just before: every change of clothes re-applies the fixture's reduced
                // motion, under which getting out is a cut, not a climb.
                player.GetComponent<Gamesim.Presentation.CharacterPresentation>().SetReducedMotion(false);
                friend.GetComponent<Gamesim.Presentation.CharacterPresentation>().SetReducedMotion(false);
                var friendSeat = friend.GetComponent<HouseSeatPresentation>();
                Assert.That(director.CompanionId, Is.EqualTo(friend.Id), "Still in the tub when the player gets out.");
                director.FinishPlayerHouseActivity();
                bool together = false;
                deadline = Time.realtimeSinceStartup + 5f;
                while (Time.realtimeSinceStartup < deadline && (director.IsPlayerHouseActivityActive || pose.Active))
                {
                    if (director.IsPlayerHouseActivityActive && friendSeat != null && friendSeat.IsExiting) together = true;
                    yield return null;
                }
                Assert.That(pose.Active, Is.False, "They get out when the player does.");
                Assert.That(together, Is.True, "They climb out as the player climbs out - not jumping to the deck once the player is already out.");
                yield return null;
                Assert.That(director.CompanionId, Is.Null);
                Assert.That(director.StatusMessage ?? "", Does.Not.Contain("joins you in the hot tub"), "Nor does the house go on saying they have come.");
                AssertPlayerSeasonUnchanged(before, director.Snapshot);
            }
            finally { Time.captureDeltaTime = 0f; }
        }
    }
}
