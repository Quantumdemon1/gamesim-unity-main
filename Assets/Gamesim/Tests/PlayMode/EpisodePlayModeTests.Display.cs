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
        /// <summary>
        /// The compact HUD keeps the objective, the cast and the chosen text size, and drops the
        /// secondary chrome. Its objective card is a literal per size (the owner's contract of
        /// 2026-10-15): 238 tall with a 48-unit next stop at the standard and the larger text, and at
        /// the largest 256 with a 66-unit next stop, two of its 25-point lines, since the line wraps
        /// there and 48 made it step down to 23.75 to fit one. The largest is photographed at 16:9 and
        /// at 4:3 in the headless harness.
        /// </summary>
        [UnityTest]
        public IEnumerator Display_CompactHudPreservesTheObjectiveCastAndLargeTextWithoutSecondaryChrome()
        {
            foreach(var textSize in TextSizes.All)
            {
                yield return ApplyTextSize(textSize);
                director.OpenSettingsAt(Gamesim.Presentation.SettingsSection.Display);
                PressSettingRow(Gamesim.Presentation.SettingRowWords.CompactHudRow, "On");
                Assert.That(director.CompactHud,Is.True);
                director.ClosePanels();yield return null;Canvas.ForceUpdateCanvases();
                bool largest=textSize==TextSize.Largest;
                if(largest && Application.isBatchMode)
                {
                    yield return CaptureFraming("compact-hud-largest");
                    yield return CaptureFraming("compact-hud-largest-4x3",width:1200,height:900);
                    yield return null;Canvas.ForceUpdateCanvases();
                }
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
                Assert.That(destination.fontSize,Is.EqualTo(Mathf.RoundToInt(18*TextSizes.Scale(textSize))),TextScales.At(textSize));
                Assert.That(objective.rect.height,Is.EqualTo(largest?256f:238f),"The compact objective's height "+TextScales.At(textSize)+".");
                Assert.That(destination.rectTransform.rect.height,Is.EqualTo(largest?66f:48f),"The next stop's box "+TextScales.At(textSize)+".");
                if(largest)
                    Assert.That(destination.rectTransform.rect.height,Is.GreaterThanOrEqualTo(2*destination.fontSize*1.3f),
                        "The next stop holds two of its lines, 1.3 times each, "+TextScales.At(textSize)+".");
                director.OpenSettingsAt(Gamesim.Presentation.SettingsSection.Display);PressSettingRow(Gamesim.Presentation.SettingRowWords.CompactHudRow, "Off");
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
            director.OpenSettingsAt(Gamesim.Presentation.SettingsSection.Display);
            yield return null;
            try
            {
                // A platform may exclude levels (batchmode sees one), so the choice's length is whatever
                // the platform offers; the row names the tier and lands where it started. With one level
                // there is nothing to choose: the row says so and cannot be pressed (menu slice 4).
                int levels = QualitySettings.names.Length;
                int tier = director.QualityTier;
                if (levels > 1)
                {
                    PressSettingRow(Gamesim.Presentation.SettingRowWords.QualityRow, EpisodeDirector.QualityName((tier + 1) % levels));
                    Assert.That(director.QualityTier, Is.EqualTo((tier + 1) % levels), "The quality row moves to the next tier.");
                    Assert.That(QualitySettings.GetQualityLevel(), Is.EqualTo(director.QualityTier), "and the level is applied.");
                    for (int i = 0; i < levels - 1; i++) ButtonWithCaption(Gamesim.Presentation.SettingRowWords.QualityRow).onClick.Invoke();
                    Assert.That(director.QualityTier, Is.EqualTo(tier), "Round the choices it is back where it started.");
                }
                else Assert.That(FindButton(Gamesim.Presentation.SettingRowWords.QualityRow).interactable, Is.False, "One level: nothing to choose.");

                // VSync is a row of its own beside the frame rate's limit (PLAN A, A12, decision 12):
                // this pin changed on purpose - the limit no longer has a VSync step.
                Assume.That(director.FrameCap, Is.EqualTo(0), "A fresh episode runs on VSync.");
                Assert.That(director.VSync, Is.True);
                Assert.That(QualitySettings.vSyncCount, Is.EqualTo(1), "VSync on paces the frames,");
                Assert.That(Application.targetFrameRate, Is.EqualTo(-1), "with no limit of its own.");
                Assert.That(director.FrameLimit, Is.EqualTo(-1), "The limit starts at none.");
                PressSettingRow(Gamesim.Presentation.SettingRowWords.VSyncRow, "Off");
                Assert.That(director.VSync, Is.False, "VSync off.");
                Assert.That(QualitySettings.vSyncCount, Is.EqualTo(0));
                Assert.That(director.FrameCap, Is.EqualTo(-1), "Off with no limit is uncapped.");
                PressSettingRow(Gamesim.Presentation.SettingRowWords.FrameRateRow, "30 fps");
                Assert.That(director.FrameLimit, Is.EqualTo(30), "The first step is the 30 fps limit.");
                Assert.That(Application.targetFrameRate, Is.EqualTo(30), "and it is the target frame rate with VSync off.");
                for (int i = 0; i < DisplayChoices.FrameLimits.Length - 2; i++) ButtonWithCaption(Gamesim.Presentation.SettingRowWords.FrameRateRow).onClick.Invoke();
                Assert.That(director.FrameLimit, Is.EqualTo(120), "60, then 120.");
                PressSettingRow(Gamesim.Presentation.SettingRowWords.FrameRateRow, "Uncapped");
                Assert.That(director.FrameLimit, Is.EqualTo(-1), "The last step is uncapped.");
                Assert.That(Application.targetFrameRate, Is.EqualTo(-1));
                PressSettingRow(Gamesim.Presentation.SettingRowWords.FrameRateRow, "30 fps");
                Assert.That(director.FrameLimit, Is.EqualTo(30), "and round again.");
                PressSettingRow(Gamesim.Presentation.SettingRowWords.VSyncRow, "On");
                Assert.That(QualitySettings.vSyncCount, Is.EqualTo(1), "VSync on again,");
                Assert.That(Application.targetFrameRate, Is.EqualTo(-1), "pacing the frames over the limit,");
                Assert.That(director.FrameCap, Is.EqualTo(0), "which code reads as the cap 0 it always was.");
                Assert.That(director.FrameLimit, Is.EqualTo(30), "and the limit is kept for when it is off.");

                // The window's mode is one row (the old full-screen toggle and the mode were the same
                // setting): right moves on a mode, left back, each applying the window.
                var mode = director.DisplayModeSetting;
                int applied = director.WindowApplications;
                StepSettingRow(Gamesim.Presentation.SettingRowWords.DisplayModeRow, UnityEngine.EventSystems.MoveDirection.Right, DisplayChoices.ModeName(DisplayChoices.NextMode(mode)));
                Assert.That(director.DisplayModeSetting, Is.EqualTo(DisplayChoices.NextMode(mode)), "The window row moves the preference.");
                StepSettingRow(Gamesim.Presentation.SettingRowWords.DisplayModeRow, UnityEngine.EventSystems.MoveDirection.Left, DisplayChoices.ModeName(mode));
                Assert.That(director.DisplayModeSetting, Is.EqualTo(mode));
                Assert.That(director.WindowApplications, Is.EqualTo(applied + 2), "Each step applies the window.");

                Assert.That(director.EdgePanOn, Is.True, "Edge panning starts on.");
                Assert.That(cameraRig.EdgePan, Is.True);
                director.ShowSettingsSection(Gamesim.Presentation.SettingsSection.Controls);
                PressSettingRow(Gamesim.Presentation.SettingRowWords.EdgePanningRow, "Off");
                Assert.That(director.EdgePanOn, Is.False);
                Assert.That(cameraRig.EdgePan, Is.False, "and the rig hears it.");
                PressSettingRow(Gamesim.Presentation.SettingRowWords.EdgePanningRow, "On");
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
            director.OpenSettingsAt(Gamesim.Presentation.SettingsSection.Display);
            yield return null;
            try
            {
                var start = director.DisplayModeSetting;
                for (int press = 1; press <= 3; press++)
                {
                    int applied = director.WindowApplications;
                    var before = director.DisplayModeSetting;
                    ButtonWithCaption(Gamesim.Presentation.SettingRowWords.DisplayModeRow).onClick.Invoke();
                    Assert.That(director.DisplayModeSetting, Is.EqualTo(DisplayChoices.NextMode(before)), "Press " + press + " moves to the next mode.");
                    Assert.That(director.WindowApplications, Is.EqualTo(applied + 1), "and applies the window once.");
                    Assert.That(SettingRowValue(Gamesim.Presentation.SettingRowWords.DisplayModeRow), Is.EqualTo(DisplayChoices.ModeName(director.DisplayModeSetting)), "The row's value names the mode.");
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
                    ButtonWithCaption(Gamesim.Presentation.SettingRowWords.ResolutionRow).onClick.Invoke();
                    Assert.That(director.Resolution, Is.EqualTo(DisplayChoices.Next(DisplayChoices.Resolutions(offered, resolution), resolution)),
                        "The next resolution after " + resolution + ".");
                    Assert.That(director.WindowApplications, Is.EqualTo(applied + 1));
                    Assert.That(SettingRowValue(Gamesim.Presentation.SettingRowWords.ResolutionRow), Is.EqualTo(director.Resolution), "The row's value names the resolution.");
                    resolution = director.Resolution;
                }

                var layout = new List<DisplayInfo>();
                try { Screen.GetDisplayLayout(layout); }
                catch (System.Exception) { layout.Clear(); }
                bool monitorShown = director.GetComponentsInChildren<Button>(true).Any(button => button.IsActive() && button.name == Gamesim.Presentation.SettingRowWords.MonitorRow);
                Assert.That(monitorShown, Is.EqualTo(layout.Count > 1), "The monitor row is there exactly when there is more than one display ("
                    + layout.Count + ").");

                PressSettingRow(Gamesim.Presentation.SettingRowWords.VSyncRow, "Off");
                Assert.That(QualitySettings.vSyncCount, Is.Zero, "The row says how VSync stands, and it is off.");
                PressSettingRow(Gamesim.Presentation.SettingRowWords.VSyncRow, "On");
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
                // Each control in its section of the Settings frame (menu slice 4).
                director.OpenSettingsAt(Gamesim.Presentation.SettingsSection.Sound);
                yield return null;
                ShellMove(SettingRowControl(Gamesim.Presentation.SettingRowWords.MasterVolumeRow).gameObject, UnityEngine.EventSystems.MoveDirection.Right);
                ShellMove(SettingRowControl(Gamesim.Presentation.SettingRowWords.MasterVolumeRow).gameObject, UnityEngine.EventSystems.MoveDirection.Left);
                PressSettingRow(Gamesim.Presentation.SettingRowWords.AllSoundRow, "Off");
                PressSettingRow(Gamesim.Presentation.SettingRowWords.AllSoundRow, "On");
                director.ShowSettingsSection(Gamesim.Presentation.SettingsSection.Accessibility);
                PressSettingRow(Gamesim.Presentation.SettingRowWords.ReducedMotionRow, "On");
                PressSettingRow(Gamesim.Presentation.SettingRowWords.ReducedMotionRow, "Off");
                director.ShowSettingsSection(Gamesim.Presentation.SettingsSection.Controls);
                PressSettingRow(Gamesim.Presentation.SettingRowWords.EdgePanningRow, "Off");
                PressSettingRow(Gamesim.Presentation.SettingRowWords.EdgePanningRow, "On");
                director.ShowSettingsSection(Gamesim.Presentation.SettingsSection.Display);
                var quality = FindButton(Gamesim.Presentation.SettingRowWords.QualityRow);
                if (quality.interactable) quality.onClick.Invoke();
                ButtonWithCaption(Gamesim.Presentation.SettingRowWords.FrameRateRow).onClick.Invoke();
                yield return null;
                Assert.That(director.WindowApplications, Is.EqualTo(applied), "No setting but the window's own applied the window.");
                Assert.That(Screen.fullScreenMode, Is.EqualTo(mode), "and the window stands as it stood.");

                ButtonWithCaption(Gamesim.Presentation.SettingRowWords.DisplayModeRow).onClick.Invoke();
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

        /// <summary>
        /// While the episode screen is open the objective's next stop says the player is there,
        /// "Here: the episode screen", as the diary's lines do; once it closes it sends them there
        /// again. Run in the full HUD, whose top-bar chip stays up under the screen (the compact
        /// card stands down for it); the density is put back after.
        /// </summary>
        [UnityTest]
        public IEnumerator Objective_SaysHereWhileTheEpisodeScreenIsOpen()
        {
            bool originalCompact = director.CompactHud;
            try
            {
                director.SetCompactHud(false);
                director.ClosePanels();
                yield return null;
                WarpPlayer(director.StationPosition);
                Assert.That(director.TryOpenPhasePanel(), Is.True, "The episode screen opens beside its station.");
                yield return null; yield return null;
                var lines = LastActive("Objective").GetComponentsInChildren<TMPro.TMP_Text>().Select(label => label.text).ToList();
                Assert.That(lines.Any(line => line.StartsWith("Here: the episode screen")), Is.True,
                    "The objective says the player is at the episode screen: " + string.Join(" | ", lines));
                Assert.That(lines.Any(line => line.StartsWith("Next stop:")), Is.False,
                    "and sends them nowhere they already are: " + string.Join(" | ", lines));

                director.ClosePanels();
                yield return null; yield return null;
                lines = LastActive("Objective").GetComponentsInChildren<TMPro.TMP_Text>().Select(label => label.text).ToList();
                Assert.That(lines.Any(line => line.StartsWith("Next stop: episode screen")), Is.True,
                    "Closed, the objective sends the player to the screen again: " + string.Join(" | ", lines));
            }
            finally
            {
                director.ClosePanels();
                director.SetCompactHud(originalCompact);
            }
        }
    }
}
