using System;
using UnityEngine;

namespace Gamesim.Episode
{
    /// <summary>
    /// The camera's settings (PLAN A, A7; the lead's decision 7): how fast it turns, from half its
    /// own speed to twice it, and whether its tilt runs the other way. One speed for the mouse's drag,
    /// the right stick and Q/C (<see cref="Gamesim.House.HouseCameraRig.OrbitSpeedScale"/>), kept in
    /// the session's preferences store beside the display's (A13) as <c>Gamesim.CameraSpeed</c> (a
    /// percentage) and <c>Gamesim.InvertY</c>.
    /// </summary>
    public sealed partial class EpisodeDirector
    {
        /// <summary>The camera speeds the settings cycle, as percentages of the camera's own: half to twice it.</summary>
        public static readonly int[] CameraSpeeds = { 50, 75, 100, 150, 200 };

        public const string InvertTiltCaption = "Invert camera tilt";
        public const string RestoreTiltCaption = "Restore camera tilt";
        /// <summary>The camera speed control's words before its speed: "Camera speed: Normal  (change)".</summary>
        public const string CameraSpeedCaptionPrefix = "Camera speed: ";

        private int cameraSpeedPercent = 100;
        private bool invertTilt;

        /// <summary>The camera's speed, as a share of its own (1 is as built).</summary>
        public float CameraSpeed => cameraSpeedPercent / 100f;
        /// <summary>Whether the camera's tilt runs the other way.</summary>
        public bool InvertTilt => invertTilt;

        /// <summary>A speed's word: "Slowest" (half), "Slow", "Normal", "Fast", "Fastest" (twice).</summary>
        public static string CameraSpeedName(int percent)
        {
            switch (percent)
            {
                case 50: return "Slowest";
                case 75: return "Slow";
                case 150: return "Fast";
                case 200: return "Fastest";
                default: return "Normal";
            }
        }

        /// <summary>The speed control's caption for a speed.</summary>
        public static string CameraSpeedCaption(int percent) => CameraSpeedCaptionPrefix + CameraSpeedName(percent) + "  (change)";

        /// <summary>Sets the camera speed, the nearest of <see cref="CameraSpeeds"/>, and keeps it.</summary>
        public void SetCameraSpeed(float share)
        {
            int wanted = Mathf.RoundToInt(share * 100f), best = 100;
            foreach (int speed in CameraSpeeds)
                if (Math.Abs(speed - wanted) < Math.Abs(best - wanted)) best = speed;
            cameraSpeedPercent = best;
            ApplyPreferences();
        }

        /// <summary>Inverts the camera's tilt, or restores it, and keeps the choice.</summary>
        public void SetInvertTilt(bool inverted)
        {
            invertTilt = inverted;
            ApplyPreferences();
        }

        private void LoadCameraPreferences()
        {
            var store = Preferences;
            cameraSpeedPercent = store.GetInt("Gamesim.CameraSpeed", 100);
            if (Array.IndexOf(CameraSpeeds, cameraSpeedPercent) < 0) cameraSpeedPercent = 100;
            invertTilt = store.GetInt("Gamesim.InvertY", 0) == 1;
        }

        private void ApplyCameraPreferences()
        {
            if (cameraRig != null)
            {
                cameraRig.OrbitSpeedScale = CameraSpeed;
                cameraRig.InvertTilt = invertTilt;
            }
            var store = Preferences;
            store.SetInt("Gamesim.CameraSpeed", cameraSpeedPercent);
            store.SetInt("Gamesim.InvertY", invertTilt ? 1 : 0);
        }

        /// <summary>The settings' camera block: the speed, cycled, and the tilt, flipped.</summary>
        private void CameraSettings()
        {
            hud.Heading("CAMERA");
            hud.Action(CameraSpeedCaption(cameraSpeedPercent), () =>
            {
                int at = Array.IndexOf(CameraSpeeds, cameraSpeedPercent);
                cameraSpeedPercent = CameraSpeeds[(at + 1) % CameraSpeeds.Length];
                ApplyPreferences(); Render();
                // The keyboard stays on the control it pressed, under its new words.
                hud.FocusWhenWired(CameraSpeedCaption(cameraSpeedPercent));
            });
            hud.Action(invertTilt ? RestoreTiltCaption : InvertTiltCaption, () =>
            {
                invertTilt = !invertTilt;
                ApplyPreferences(); Render();
                hud.FocusWhenWired(invertTilt ? RestoreTiltCaption : InvertTiltCaption);
            });
            hud.Paragraph("The speed is the mouse's drag, the right stick and Q and C alike; the tilt is up and down on each.");
        }
    }
}
