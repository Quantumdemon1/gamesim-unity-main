using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Episode;
using Gamesim.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// Display preferences (MASTER-PLAN §3.H): the settings panel's quality tier, frame-rate cap,
    /// full-screen and edge-panning controls each change what they name, and the change is applied.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        [UnityTest]
        public IEnumerator Display_CompactHudPreservesTheObjectiveCastAndLargeTextWithoutSecondaryChrome()
        {
            foreach(bool larger in new[]{false,true})
            {
                yield return ApplyTextSize(larger);
                director.OpenSettings();
                ButtonWithCaption("Use compact HUD").onClick.Invoke();
                Assert.That(director.CompactHud,Is.True);
                director.ClosePanels();yield return null;Canvas.ForceUpdateCanvases();
                foreach(var name in new[]{"Objective","Navigation","Status",CastRail.RootName,"Exploration controls"})
                    Assert.That(director.GetComponentsInChildren<RectTransform>().Any(rect=>rect.name==name),Is.True,name);
                foreach(var name in new[]{EpisodeHud.HouseVibeCardName,EpisodeHud.RecentEventsCardName,EpisodeDirector.LiveFeedCardName})
                    Assert.That(director.GetComponentsInChildren<RectTransform>().Any(rect=>rect.name==name),Is.False,name);
                Assert.That(ButtonWithCaption("Go to episode screen").interactable,Is.True);
                Assert.That(ButtonWithCaption(EpisodeHud.DiaryTravelCaption).interactable,Is.True);
                var objective=ActiveRect("Objective");
                foreach(var label in objective.GetComponentsInChildren<TMPro.TMP_Text>())
                {label.ForceMeshUpdate();Assert.That(label.isTextOverflowing,Is.False,label.text);}
                var destination=objective.GetComponentsInChildren<TMPro.TMP_Text>().Single(label=>label.text.StartsWith("Next stop:"));
                Assert.That(destination.fontSize,Is.EqualTo(Mathf.RoundToInt(18*(larger ? 1.2f : 1))));
                director.OpenSettings();ButtonWithCaption("Show full HUD").onClick.Invoke();
                Assert.That(director.CompactHud,Is.False);director.ClosePanels();yield return null;
                Assert.That(ActiveRect(EpisodeDirector.LiveFeedCardName),Is.Not.Null);
            }
        }

        [UnityTest]
        public IEnumerator Display_TheSettingsPanelsControlsApplyWhatTheyName()
        {
            int levelBefore = QualitySettings.GetQualityLevel();
            int vsyncBefore = QualitySettings.vSyncCount;
            int targetBefore = Application.targetFrameRate;
            director.OpenSettings();
            yield return null;
            try
            {
                // A platform may exclude levels (batchmode sees one), so the cycle's length is whatever
                // the platform offers; the control still names the tier and lands where it started.
                int levels = QualitySettings.names.Length;
                int tier = director.QualityTier;
                ButtonStarting("Quality: ").onClick.Invoke();
                Assert.That(director.QualityTier, Is.EqualTo((tier + 1) % levels), "The quality control moves to the next tier.");
                Assert.That(QualitySettings.GetQualityLevel(), Is.EqualTo(director.QualityTier), "and the level is applied.");
                Assert.That(ButtonStarting("Quality: ").GetComponentInChildren<TMPro.TMP_Text>().text, Does.Contain(EpisodeDirector.QualityName(director.QualityTier)));
                for (int i = 0; i < levels - 1; i++) ButtonStarting("Quality: ").onClick.Invoke();
                Assert.That(director.QualityTier, Is.EqualTo(tier), "Round the cycle it is back where it started.");

                // VSync is a control of its own beside the frame rate's limit (PLAN A, A12, decision 12):
                // this pin changed on purpose - the limit no longer has a VSync step.
                Assume.That(director.FrameCap, Is.EqualTo(0), "A fresh episode runs on VSync.");
                Assert.That(director.VSync, Is.True);
                Assert.That(QualitySettings.vSyncCount, Is.EqualTo(1), "VSync on paces the frames,");
                Assert.That(Application.targetFrameRate, Is.EqualTo(-1), "with no limit of its own.");
                Assert.That(director.FrameLimit, Is.EqualTo(-1), "The limit starts at none.");
                ButtonWithCaption(EpisodeDirector.VSyncOffCaption).onClick.Invoke();
                Assert.That(director.VSync, Is.False, "VSync off.");
                Assert.That(QualitySettings.vSyncCount, Is.EqualTo(0));
                Assert.That(director.FrameCap, Is.EqualTo(-1), "Off with no limit is uncapped.");
                ButtonStarting("Frame rate: ").onClick.Invoke();
                Assert.That(director.FrameLimit, Is.EqualTo(30), "The first step is the 30 fps limit.");
                Assert.That(Application.targetFrameRate, Is.EqualTo(30), "and it is the target frame rate with VSync off.");
                Assert.That(ButtonStarting("Frame rate: ").GetComponentInChildren<TMPro.TMP_Text>().text, Is.EqualTo(EpisodeDirector.FrameRateCaption(30)));
                for (int i = 0; i < DisplayChoices.FrameLimits.Length - 2; i++) ButtonStarting("Frame rate: ").onClick.Invoke();
                Assert.That(director.FrameLimit, Is.EqualTo(120), "60, then 120.");
                ButtonStarting("Frame rate: ").onClick.Invoke();
                Assert.That(director.FrameLimit, Is.EqualTo(-1), "The last step is uncapped.");
                Assert.That(Application.targetFrameRate, Is.EqualTo(-1));
                ButtonStarting("Frame rate: ").onClick.Invoke();
                Assert.That(director.FrameLimit, Is.EqualTo(30), "and round again.");
                ButtonWithCaption(EpisodeDirector.VSyncOnCaption).onClick.Invoke();
                Assert.That(QualitySettings.vSyncCount, Is.EqualTo(1), "VSync on again,");
                Assert.That(Application.targetFrameRate, Is.EqualTo(-1), "pacing the frames over the limit,");
                Assert.That(director.FrameCap, Is.EqualTo(0), "which code reads as the cap 0 it always was.");
                Assert.That(director.FrameLimit, Is.EqualTo(30), "and the limit is kept for when it is off.");

                // The full-screen toggle keeps its two captions: one press between a window and borderless.
                bool full = director.Fullscreen;
                int applied = director.WindowApplications;
                ButtonWithCaption(full ? "Play in a window" : "Play full screen").onClick.Invoke();
                Assert.That(director.Fullscreen, Is.EqualTo(!full), "The window control flips the preference.");
                Assert.That(director.DisplayModeSetting, Is.EqualTo(full ? DisplayMode.Windowed : DisplayMode.Borderless));
                ButtonWithCaption(!full ? "Play in a window" : "Play full screen").onClick.Invoke();
                Assert.That(director.Fullscreen, Is.EqualTo(full));
                Assert.That(director.WindowApplications, Is.EqualTo(applied + 2), "Each press applies the window.");

                Assert.That(director.EdgePanOn, Is.True, "Edge panning starts on.");
                Assert.That(cameraRig.EdgePan, Is.True);
                ButtonWithCaption("Stop the screen edges panning").onClick.Invoke();
                Assert.That(director.EdgePanOn, Is.False);
                Assert.That(cameraRig.EdgePan, Is.False, "and the rig hears it.");
                ButtonWithCaption("Let the screen edges pan").onClick.Invoke();
                Assert.That(cameraRig.EdgePan, Is.True);
            }
            finally
            {
                // The rest of the run must not inherit a cap.
                QualitySettings.SetQualityLevel(levelBefore, true);
                QualitySettings.vSyncCount = vsyncBefore;
                Application.targetFrameRate = targetBefore;
                director.ClosePanels();
            }
        }

        /// <summary>
        /// The window's controls (PLAN A, A12): the display mode cycles windowed, borderless and
        /// exclusive; the resolution cycles Desktop and every size the display offers at 1280x720 or
        /// more; the monitor is there only with more than one display; each press applies the window
        /// once and names its new value in its caption.
        /// </summary>
        [UnityTest]
        public IEnumerator Display_ModeResolutionMonitorAndVSyncCycleTheirFields()
        {
            int vsyncBefore = QualitySettings.vSyncCount;
            int targetBefore = Application.targetFrameRate;
            director.OpenSettings();
            yield return null;
            try
            {
                var start = director.DisplayModeSetting;
                for (int press = 1; press <= 3; press++)
                {
                    int applied = director.WindowApplications;
                    var before = director.DisplayModeSetting;
                    ButtonStarting("Display mode: ").onClick.Invoke();
                    Assert.That(director.DisplayModeSetting, Is.EqualTo(DisplayChoices.NextMode(before)), "Press " + press + " moves to the next mode.");
                    Assert.That(director.WindowApplications, Is.EqualTo(applied + 1), "and applies the window once.");
                    Assert.That(ButtonStarting("Display mode: ").GetComponentInChildren<TMPro.TMP_Text>().text,
                        Is.EqualTo(EpisodeDirector.DisplayModeCaption(director.DisplayModeSetting)), "The caption names the mode.");
                }
                Assert.That(director.DisplayModeSetting, Is.EqualTo(start), "Three presses go round.");

                var offered = Screen.resolutions.Select(size => new KeyValuePair<int, int>(size.width, size.height)).ToList();
                var cycle = DisplayChoices.Resolutions(offered, director.Resolution);
                Assert.That(cycle[0], Is.EqualTo(DisplayChoices.Desktop), "The desktop is the first choice.");
                Assert.That(cycle.Skip(1).Where(key => key != director.Resolution)
                    .All(key => DisplayChoices.TryParseSize(key, out int w, out int h) && w >= 1280 && h >= 720), Is.True,
                    "Every other choice the display offers is a size of at least 1280x720: " + string.Join(", ", cycle));
                var resolution = director.Resolution;
                for (int press = 0; press < cycle.Count; press++)
                {
                    int applied = director.WindowApplications;
                    ButtonStarting("Resolution: ").onClick.Invoke();
                    Assert.That(director.Resolution, Is.EqualTo(DisplayChoices.Next(DisplayChoices.Resolutions(offered, resolution), resolution)),
                        "The next resolution after " + resolution + ".");
                    Assert.That(director.WindowApplications, Is.EqualTo(applied + 1));
                    Assert.That(ButtonStarting("Resolution: ").GetComponentInChildren<TMPro.TMP_Text>().text,
                        Is.EqualTo(EpisodeDirector.ResolutionCaption(director.Resolution)));
                    resolution = director.Resolution;
                }

                var layout = new List<DisplayInfo>();
                try { Screen.GetDisplayLayout(layout); }
                catch (System.Exception) { layout.Clear(); }
                bool monitorShown = director.GetComponentsInChildren<Button>(true).Any(button => button.IsActive()
                    && button.GetComponentsInChildren<TMPro.TMP_Text>(true).Any(text => text.text.StartsWith("Monitor: ")));
                Assert.That(monitorShown, Is.EqualTo(layout.Count > 1), "The monitor control is there exactly when there is more than one display ("
                    + layout.Count + ").");

                ButtonWithCaption(EpisodeDirector.VSyncOffCaption).onClick.Invoke();
                Assert.That(QualitySettings.vSyncCount, Is.Zero);
                Assert.That(ButtonWithCaption(EpisodeDirector.VSyncOnCaption), Is.Not.Null, "The toggle says what it does next.");
                ButtonWithCaption(EpisodeDirector.VSyncOnCaption).onClick.Invoke();
                Assert.That(QualitySettings.vSyncCount, Is.EqualTo(1));
            }
            finally
            {
                QualitySettings.vSyncCount = vsyncBefore;
                Application.targetFrameRate = targetBefore;
                director.ClosePanels();
            }
        }

        /// <summary>
        /// Only the window's own controls apply the window (PLAN A, A12): text size, the compact HUD,
        /// the frame cap, VSync, sound, motion, pace, the camera and the edges - every one of them goes
        /// through the preferences' fan-out, which used to set the window back to its field and so
        /// undo an Alt+Enter on the next unrelated click. None of them counts a window application.
        /// </summary>
        [UnityTest]
        public IEnumerator Display_NoOtherSettingEverAppliesTheWindow()
        {
            int levelBefore = QualitySettings.GetQualityLevel();
            int vsyncBefore = QualitySettings.vSyncCount;
            int targetBefore = Application.targetFrameRate;
            try
            {
                int applied = director.WindowApplications;
                var mode = Screen.fullScreenMode;
                director.SetLargeText(true); director.SetLargeText(false);
                director.SetCompactHud(true); director.SetCompactHud(false);
                director.SetFrameCap(-1); director.SetFrameCap(60); director.SetFrameCap(0);
                director.SetVSync(false); director.SetVSync(true);
                director.SetCeremonyPace(CeremonyPace.Quick); director.SetCeremonyPace(CeremonyPace.Suspenseful);
                director.SetCameraSpeed(2f); director.SetCameraSpeed(1f);
                director.SetInvertTilt(true); director.SetInvertTilt(false);
                director.OpenSettings();
                yield return null;
                foreach (var caption in new[] { "Volume up", "Volume down", "Mute sound", "Turn sound on", "Reduce character motion", "Enable character motion",
                    "Stop the screen edges panning", "Let the screen edges pan" })
                    ButtonWithCaption(caption).onClick.Invoke();
                ButtonStarting("Quality: ").onClick.Invoke();
                ButtonStarting("Frame rate: ").onClick.Invoke();
                yield return null;
                Assert.That(director.WindowApplications, Is.EqualTo(applied), "No setting but the window's own applied the window.");
                Assert.That(Screen.fullScreenMode, Is.EqualTo(mode), "and the window stands as it stood.");

                ButtonStarting("Display mode: ").onClick.Invoke();
                Assert.That(director.WindowApplications, Is.EqualTo(applied + 1), "The display mode does: the count is live.");
            }
            finally
            {
                QualitySettings.SetQualityLevel(levelBefore, true);
                QualitySettings.vSyncCount = vsyncBefore;
                Application.targetFrameRate = targetBefore;
                director.ClosePanels();
            }
        }

        private Button ButtonStarting(string prefix)
        {
            var button = director.GetComponentsInChildren<Button>(true).FirstOrDefault(b => b.IsActive()
                && b.GetComponentsInChildren<TMPro.TMP_Text>(true).Any(t => t.text.StartsWith(prefix)));
            Assert.That(button, Is.Not.Null, "Expected a control whose caption starts with '" + prefix + "'.");
            return button;
        }
    }
}
