using UnityEngine;

namespace Gamesim.Episode
{
    /// <summary>
    /// Display preferences (MASTER-PLAN §3.H): a quality tier, a frame-rate cap, full screen or a
    /// window, and whether the screen's edges pan the camera. Kept in PlayerPrefs beside the
    /// sound and accessibility preferences and applied the same way, from the settings panel.
    /// </summary>
    public sealed partial class EpisodeDirector
    {
        /// <summary>The frame-rate choices in the order the panel cycles them: VSync, three caps, uncapped.</summary>
        public static readonly int[] FrameCaps = { 0, 30, 60, 120, -1 };

        private int qualityTier = 1;
        private int frameCap;
        private bool fullscreen;
        private bool edgePan = true;

        public int QualityTier => qualityTier;
        public int FrameCap => frameCap;
        public bool Fullscreen => fullscreen;
        public bool EdgePanOn => edgePan;

        public static string QualityName(int tier)
        {
            var names = QualitySettings.names;
            if (tier < 0 || tier >= names.Length) return "Default";
            // The project's two levels are the pipeline assets' names; say what they mean.
            return names[tier] == "Mobile" ? "Lean" : names[tier] == "PC" ? "Full" : names[tier];
        }

        public static string FrameCapName(int cap) => cap == 0 ? "VSync" : cap < 0 ? "Uncapped" : cap + " fps";

        private void LoadDisplayPreferences()
        {
            bool prefs = SaveRootOverride == null;
            int levels = Mathf.Max(1, QualitySettings.names.Length);
            qualityTier = Mathf.Clamp(prefs ? PlayerPrefs.GetInt("Gamesim.Quality", QualitySettings.GetQualityLevel()) : QualitySettings.GetQualityLevel(), 0, levels - 1);
            frameCap = prefs ? PlayerPrefs.GetInt("Gamesim.FrameCap", 0) : 0;
            if (System.Array.IndexOf(FrameCaps, frameCap) < 0) frameCap = 0;
            fullscreen = prefs ? PlayerPrefs.GetInt("Gamesim.Fullscreen", Screen.fullScreen ? 1 : 0) == 1 : Screen.fullScreen;
            edgePan = !prefs || PlayerPrefs.GetInt("Gamesim.EdgePan", 1) == 1;
        }

        private void ApplyDisplayPreferences()
        {
            if (qualityTier != QualitySettings.GetQualityLevel() && qualityTier < QualitySettings.names.Length)
                QualitySettings.SetQualityLevel(qualityTier, true);
            // VSync is the cap when it is chosen; a number is a cap of its own with VSync off; and
            // uncapped is neither, which is what a benchmark wants and nothing else does.
            QualitySettings.vSyncCount = frameCap == 0 ? 1 : 0;
            Application.targetFrameRate = frameCap > 0 ? frameCap : -1;
            // The editor and a batchmode run ignore this, so the field is the record of the choice.
            if (!Application.isEditor && Screen.fullScreen != fullscreen) Screen.fullScreen = fullscreen;
            if (cameraRig != null) cameraRig.EdgePan = edgePan;
            if (SaveRootOverride == null)
            {
                PlayerPrefs.SetInt("Gamesim.Quality", qualityTier);
                PlayerPrefs.SetInt("Gamesim.FrameCap", frameCap);
                PlayerPrefs.SetInt("Gamesim.Fullscreen", fullscreen ? 1 : 0);
                PlayerPrefs.SetInt("Gamesim.EdgePan", edgePan ? 1 : 0);
            }
        }

        /// <summary>The display block of the settings panel; each control cycles or flips one preference.</summary>
        private void DisplaySettings()
        {
            hud.Heading("Display");
            hud.Action("Quality: " + QualityName(qualityTier) + "  (change)", () =>
            {
                qualityTier = (qualityTier + 1) % Mathf.Max(1, QualitySettings.names.Length); ApplyPreferences(); Render();
            });
            hud.Action("Frame rate: " + FrameCapName(frameCap) + "  (change)", () =>
            {
                int at = System.Array.IndexOf(FrameCaps, frameCap);
                frameCap = FrameCaps[(at + 1) % FrameCaps.Length]; ApplyPreferences(); Render();
            });
            hud.Action(fullscreen ? "Play in a window" : "Play full screen", () => { fullscreen = !fullscreen; ApplyPreferences(); Render(); });
            hud.Action(edgePan ? "Stop the screen edges panning" : "Let the screen edges pan", () => { edgePan = !edgePan; ApplyPreferences(); Render(); });
            hud.Paragraph("The edges pan the camera only in full screen, where the cursor cannot leave the game.");
        }
    }
}
