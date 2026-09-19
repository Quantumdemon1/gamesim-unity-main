using System.Collections;
using System.Linq;
using Gamesim.Episode;
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
        public IEnumerator Display_TheSettingsPanelsControlsApplyWhatTheyName()
        {
            int levelBefore = QualitySettings.GetQualityLevel();
            int vsyncBefore = QualitySettings.vSyncCount;
            int targetBefore = Application.targetFrameRate;
            director.OpenSettings();
            yield return null;
            try
            {
                int levels = QualitySettings.names.Length;
                Assume.That(levels, Is.GreaterThan(1), "Two quality levels are needed to cycle.");
                int tier = director.QualityTier;
                ButtonStarting("Quality: ").onClick.Invoke();
                Assert.That(director.QualityTier, Is.EqualTo((tier + 1) % levels), "The quality control moves to the next tier.");
                Assert.That(QualitySettings.GetQualityLevel(), Is.EqualTo(director.QualityTier), "and the level is applied.");
                Assert.That(ButtonStarting("Quality: ").GetComponentInChildren<TMPro.TMP_Text>().text, Does.Contain(EpisodeDirector.QualityName(director.QualityTier)));
                for (int i = 0; i < levels - 1; i++) ButtonStarting("Quality: ").onClick.Invoke();
                Assert.That(director.QualityTier, Is.EqualTo(tier), "Round the cycle it is back where it started.");

                Assume.That(director.FrameCap, Is.EqualTo(0), "A fresh episode runs on VSync.");
                ButtonStarting("Frame rate: ").onClick.Invoke();
                Assert.That(director.FrameCap, Is.EqualTo(30), "The first step is the 30 fps cap.");
                Assert.That(Application.targetFrameRate, Is.EqualTo(30), "and it is the target frame rate");
                Assert.That(QualitySettings.vSyncCount, Is.EqualTo(0), "with VSync off, because a cap is the cap.");
                for (int i = 0; i < EpisodeDirector.FrameCaps.Length - 2; i++) ButtonStarting("Frame rate: ").onClick.Invoke();
                Assert.That(director.FrameCap, Is.EqualTo(-1), "The last step is uncapped.");
                Assert.That(Application.targetFrameRate, Is.EqualTo(-1));
                ButtonStarting("Frame rate: ").onClick.Invoke();
                Assert.That(director.FrameCap, Is.EqualTo(0), "and round again to VSync,");
                Assert.That(QualitySettings.vSyncCount, Is.EqualTo(1), "which turns VSync on.");

                bool full = director.Fullscreen;
                ButtonWithCaption(full ? "Play in a window" : "Play full screen").onClick.Invoke();
                Assert.That(director.Fullscreen, Is.EqualTo(!full), "The window control flips the preference.");
                ButtonWithCaption(!full ? "Play in a window" : "Play full screen").onClick.Invoke();
                Assert.That(director.Fullscreen, Is.EqualTo(full));

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

        private Button ButtonStarting(string prefix)
        {
            var button = director.GetComponentsInChildren<Button>(true).FirstOrDefault(b => b.IsActive()
                && b.GetComponentsInChildren<TMPro.TMP_Text>(true).Any(t => t.text.StartsWith(prefix)));
            Assert.That(button, Is.Not.Null, "Expected a control whose caption starts with '" + prefix + "'.");
            return button;
        }
    }
}
