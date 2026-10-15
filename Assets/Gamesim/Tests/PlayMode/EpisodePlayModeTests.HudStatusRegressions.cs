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
    }
}
