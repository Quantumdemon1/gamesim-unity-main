using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Gamesim.Episode;
using Gamesim.House;
using Gamesim.Persistence;
using Gamesim.Presentation;
using Gamesim.Simulation;
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
    /// The HUD's status line after the phase-and-dialogue port (lane S2a, D9 and D10): a stopped house
    /// world or a failed save drew an empty, undismissable panel and locked the diary, and refusals and
    /// device hints never reached the status line. Each test reads what is drawn, not only the field
    /// behind it.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        private void AssertNoEmptyPanelAndTheDiaryStillOpens(string where)
        {
            Canvas.ForceUpdateCanvases();
            Assert.That(director.IsPanelOpen, Is.False, where + ": nothing is open.");
            Assert.That(ActiveRect("Episode panel"), Is.Null,
                where + ": no empty, undismissable panel is built over the house.");
            Assert.That(director.HasDiaryRoom, Is.True, where + ": the fixture's house has its diary room.");
            var diary = FindButton(EpisodeHud.DiaryTravelCaption);
            Assert.That(diary.IsInteractable(), Is.True,
                where + ": the diary does not depend on the house's navigation or on the last save, so its way there stays open.");
        }

        /// <summary>
        /// D9: the house's world stopping is a notice on the status line, not a recovery. It used to be
        /// passed to the HUD as one, which built the episode panel with nothing in it - Close rebuilt
        /// it - and greyed out the diary.
        /// </summary>
        [UnityTest]
        public IEnumerator HudStatus_AStoppedHouseWorldDrawsNoEmptyPanelAndLeavesTheDiaryOpen()
        {
            director.ClosePanels();
            yield return null;
            const string reason = "Test-owned world navigation stoppage.";
            const string notice = "Housemate activity is paused: " + reason + " Your episode and saved history remain available.";
            var stop = typeof(EpisodeDirector).GetMethod("StopNpcWorld", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(stop, Is.Not.Null);
            stop.Invoke(director, new object[] { reason });
            director.ClosePanels();
            yield return null;
            Assert.That(ExchangeFlowStatusText(), Does.Contain(notice), "The stopped world is said on the status line.");
            AssertNoEmptyPanelAndTheDiaryStillOpens("After the house's world stopped");
            director.ClosePanels();
            yield return null;
            AssertNoEmptyPanelAndTheDiaryStillOpens("After closing again");
            Assert.That(ExchangeFlowStatusText(), Does.Contain(notice), "The notice outlasts a close.");
        }

        /// <summary>D9, its other half: a save that failed is a notice too, never an empty panel.</summary>
        [UnityTest]
        public IEnumerator HudStatus_AFailedSaveDrawsNoEmptyPanelAndLeavesTheDiaryOpen()
        {
            if (!Application.isEditor) Assert.Ignore("The durable writer diagnostic is Editor-only.");
            yield return InstallExchangeFlowHouse();
            var before = director.Snapshot;
            byte[] savedBytes = File.ReadAllBytes(director.SavePath);
            var writer = typeof(EpisodeDirector).GetField("saveCandidateForDiagnostics", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(writer, Is.Not.Null);
            writer.SetValue(director, (Action<EpisodeState>)(candidate =>
                throw new IOException("Injected HUD-status before-write failure.")));
            try
            {
                var result = director.Submit(ExchangeFlowCommand(before, EpisodeCommandKind.SmallTalk));
                Assert.That(result.accepted, Is.False, "The decision is not committed when its save fails.");
                director.ClosePanels();
                yield return null;
                Assert.That(ExchangeFlowStatusText(), Does.Contain("not committed"), "The failed save is said on the status line.");
                AssertNoEmptyPanelAndTheDiaryStillOpens("After a failed save");
                AssertExchangeFlowUnchanged(before, savedBytes);
            }
            finally { if (director != null) writer.SetValue(director, null); }
        }

        /// <summary>
        /// D10: a piece of furniture's line - what the player is doing and how to stop, in the device's
        /// words - is on the status line while they are at it, and goes when they get up.
        /// </summary>
        [UnityTest]
        public IEnumerator HudStatus_TheFurnituresLineIsOnTheStatusWhileTheyAreAtItAndGoesAfter()
        {
            director.ClosePanels();
            yield return null;
            yield return PressKey(Key.LeftCtrl);
            Assert.That(director.HintsForPad, Is.False, "The keyboard's words are the ones expected.");
            var stove = PlacesFor(HouseFurnitureActivity.Cook).Single();
            WarpPlayer(OnFootFrom(stove.Approach, 3f, 8f));
            yield return null;
            player.Agent.speed = 20; player.Agent.acceleration = 100;
            director.StartActivityInHouse(stove, HouseFurnitureActivity.Cook);
            Assert.That(director.PlayerActivity, Is.EqualTo(HouseFurnitureActivity.Cook), director.StatusMessage);
            string line = InputGlossary.ActivityLine("Cook", false);
            yield return null;
            Assert.That(ExchangeFlowStatusText(), Does.Contain(line), "The furniture's line is drawn on the status line.");
            float deadline = Time.realtimeSinceStartup + 10f;
            var pose = player.GetComponent<HouseFurniturePose>();
            while (Time.realtimeSinceStartup < deadline && !(pose != null && pose.IsPerforming)) yield return null;
            Assert.That(ExchangeFlowStatusText(), Does.Contain(line), "It stays while they are at it.");
            director.FinishPlayerHouseActivity();
            deadline = Time.realtimeSinceStartup + 5f;
            while (Time.realtimeSinceStartup < deadline && director.IsPlayerHouseActivityActive) yield return null;
            yield return null;
            Assert.That(director.IsPlayerHouseActivityActive, Is.False);
            Assert.That(ExchangeFlowStatusText(), Does.Not.Contain(line), "Up again, the line goes,");
            Assert.That(ExchangeFlowStatusText(), Does.StartWith(EpisodeDirector.PhaseLine(director.Snapshot.phase, director.Snapshot.week)),
                "and the phase's own guidance comes back.");
        }

        /// <summary>
        /// The phase's own guidance is back on the drawn status line once the player is up, where
        /// <paramref name="where"/> says. Only the Nearby bar may stand in its place, as it does by
        /// design while a conversation is witnessed; a notice left behind hid both.
        /// </summary>
        private void AssertThePhaseGuidanceIsDrawn(string where)
        {
            Canvas.ForceUpdateCanvases();
            if (ActiveRect(EpisodeHud.NearbyCardName) != null && ActiveRect(EpisodeHud.SpeechBarName) != null)
            {
                Debug.Log("[Gamesim] " + where + ": the Nearby bar holds the status line's place.");
                return;
            }
            Assert.That(ExchangeFlowStatusText(), Does.StartWith(EpisodeDirector.PhaseLine(director.Snapshot.phase, director.Snapshot.week)),
                where + ": the phase's own guidance is drawn on the status line. It showed: '" + ExchangeFlowStatusText() + "'.");
        }

        /// <summary>
        /// S2A-R1: company in the hot tub. The furniture's line is the status line for as long as the
        /// player soaks, in the device's words: a housemate joining them is narration their body shows,
        /// not a line that takes the notice's place, and the house taking that housemate away again
        /// never leaves the line empty. Getting out lets the notice go whatever the message says by
        /// then, and the phase's own guidance comes back. The notice used to be let go only while the
        /// message still read the furniture's line, so after company it stayed, holding an empty line
        /// and the Nearby bar off the screen until something else cleared it.
        /// </summary>
        [UnityTest]
        public IEnumerator HudStatus_HotTubCompanyNeverTakesOrEmptiesTheFurnituresLine()
        {
            var seats = PlacesFor(HouseFurnitureActivity.Soak);
            // No new pairings while this runs, as the company test holds them: a conversation the house
            // starts would take the companion before the test does.
            typeof(EpisodeDirector).GetField("npcApproachDiagnosticsSuppressed", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(director, true);
            var held = typeof(EpisodeDirector).GetField("companionActivity", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(held, Is.Not.Null);
            string keys = InputGlossary.ActivityLine("Soak", false), pad = InputGlossary.ActivityLine("Soak", true);
            var found = new List<string>();
            Time.captureDeltaTime = 1f / 30f;
            try
            {
                yield return PressKey(Key.LeftCtrl);
                Assert.That(director.HintsForPad, Is.False, "The keyboard's words are the ones expected.");
                yield return BeginInHouse(seats[0], HouseFurnitureActivity.Soak);
                Assert.That(ExchangeFlowStatusText(), Does.Contain(keys), "Soaking, the furniture's line is drawn on the status line.");

                float deadline = Time.realtimeSinceStartup + 20f;
                while (Time.realtimeSinceStartup < deadline && director.CompanionId == null) yield return null;
                Assert.That(director.CompanionId, Is.Not.Null, "Somebody comes to keep the player company.");
                string first = director.CompanionId;
                yield return null;
                Assert.That(director.StatusMessage, Does.Contain("joins you in the hot tub"), "The house says who has come, as narration.");
                if (!ExchangeFlowStatusText().Contains(keys))
                    found.Add("With company the status line showed '" + ExchangeFlowStatusText() + "', not the furniture's line.");

                // A pad press words the line for the pad: the notice's own words, not only the message's.
                yield return PressOnPad(GamepadButton.RightStick);
                yield return Frames(2);
                Assert.That(director.HintsForPad, Is.True, "A pad press: the hints speak of the pad.");
                if (!ExchangeFlowStatusText().Contains(pad))
                    found.Add("After a pad press the status line showed '" + ExchangeFlowStatusText() + "', not the pad's furniture line.");

                // The house takes the companion back, as a conversation the season calls for does: their
                // own lease goes while the player is still in the water.
                var friend = SceneComponents<HouseNpc>().Single(npc => npc.Id == first);
                var friendPose = friend.GetComponent<HouseFurniturePose>();
                deadline = Time.realtimeSinceStartup + 20f;
                while (Time.realtimeSinceStartup < deadline && !(friendPose != null && friendPose.IsPerforming))
                {
                    yield return null;
                    friendPose = friend.GetComponent<HouseFurniturePose>();
                }
                Assert.That(friendPose != null && friendPose.IsPerforming, Is.True, "They reach the tub and sit.");
                friendPose.RequestFinish();
                deadline = Time.realtimeSinceStartup + 10f;
                int lost = 0, watched = 0;
                while (Time.realtimeSinceStartup < deadline && held.GetValue(director) != null
                       && (director.CompanionId == null || director.CompanionId == first))
                { yield return null; watched++; if (!ExchangeFlowStatusText().Contains(pad)) lost++; }
                for (int frame = 0; frame < 10; frame++)
                { yield return null; watched++; if (!ExchangeFlowStatusText().Contains(pad)) lost++; }
                Assert.That(director.IsPlayerHouseActivityActive, Is.True, "The player is still soaking.");
                Assert.That(director.CompanionId, Is.Not.EqualTo(first), "The house has taken the companion out.");
                if (lost > 0)
                    found.Add("With the companion taken the status line lost the furniture's line on " + lost + " of " + watched
                        + " frames; last it showed '" + ExchangeFlowStatusText() + "'.");

                // Out of the water, with or without whoever came next.
                director.FinishPlayerHouseActivity();
                deadline = Time.realtimeSinceStartup + 10f;
                while (Time.realtimeSinceStartup < deadline && (director.IsPlayerHouseActivityActive || held.GetValue(director) != null))
                    yield return null;
                Assert.That(director.IsPlayerHouseActivityActive, Is.False, "The player is out.");
                Assert.That(held.GetValue(director), Is.Null, "And nobody is left keeping them company.");
                yield return null;
                try { AssertThePhaseGuidanceIsDrawn("Out of the hot tub"); }
                catch (AssertionException missing) { found.Add(missing.Message.Trim()); }
                Assert.That(found, Is.Empty, string.Join("\n", found));
            }
            finally { Time.captureDeltaTime = 0f; }
        }
    }
}
