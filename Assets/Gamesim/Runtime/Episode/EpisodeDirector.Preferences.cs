using System;
using System.Collections.Generic;
using Gamesim.Persistence;
using Gamesim.Presentation;
using UnityEngine;

namespace Gamesim.Episode
{
    /// <summary>
    /// The settings' store and their account in the player log (PLAN A, A13). A playtest root that
    /// asked for its own preferences (<see cref="PreferencesBesideSaves"/>) keeps them in
    /// <c>preferences.json</c> beside its saves and leaves the machine's PlayerPrefs alone; any other
    /// isolated root reads the defaults and writes nothing, as it always has; an ordinary install
    /// reads and writes PlayerPrefs. The log gets the session's settings once (only with the file
    /// store, so a test's reload stays quiet), a line for each value that changes, and a line each
    /// time the device pressed on changes.
    /// </summary>
    public sealed partial class EpisodeDirector
    {
        /// <summary>The session's store; the defaults until Start has opened the real one.</summary>
        private IPreferenceStore Preferences => preferences ?? DefaultPreferences.Instance;

        /// <summary>Where this session's settings are kept: PlayerPrefs, the root's own file, or nowhere.</summary>
        public PreferenceRoute PreferenceRouteInUse => Preferences.Route;

        /// <summary>The settings as last written to the log, or null before Start has noted them.</summary>
        private List<KeyValuePair<string, string>> loggedSettings;

        /// <summary>
        /// The preferences a launch reads before the director's Start has decided its root - the
        /// scene's audio wakes first and honours the mute choice before any source plays. Decided as
        /// Start will decide it: an isolated root that did not ask for its own reads the defaults,
        /// never the machine's PlayerPrefs.
        /// </summary>
        public static IPreferenceStore LaunchPreferences()
        {
            var route = PreferenceRouting.ForLaunch(SaveRootOverride, PreferencesBesideSaves, Environment.GetCommandLineArgs(), out var root);
            return PreferenceStores.Open(route, root);
        }

        /// <summary>The settings in the log's words, in the order the session line gives them.</summary>
        private List<KeyValuePair<string, string>> SettingsNow()
        {
            KeyValuePair<string, string> Say(string key, string value) => new KeyValuePair<string, string>(key, value);
            return new List<KeyValuePair<string, string>>
            {
                Say("text", largeText ? "larger" : "standard"),
                Say("motion", reducedMotion ? "reduced" : "full"),
                Say("pace", ceremonyPace == CeremonyPace.Quick ? "quick" : "suspenseful"),
                Say("volume", volumePercent.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                Say("muted", muted ? "yes" : "no"),
                Say("music", musicOn ? "on" : "off"),
                Say("display", DisplayEvidence()),
                Say("sound", reducedAudio ? "reduced" : "full"),
                Say("hud", compactHud ? "compact" : "full"),
                Say("quality", QualityName(qualityTier)),
                Say("vsync", vSync ? "on" : "off"),
                Say("frames", DisplayChoices.FrameLimitName(frameLimit)),
                Say("resolution", resolution),
                Say("edges", edgePan ? "pan" : "still"),
                Say("language", language),
                Say("camera", CameraSpeedName(cameraSpeedPercent)),
                Say("tilt", invertTilt ? "inverted" : "normal"),
            };
        }

        /// <summary>The window as chosen and as it stands: its mode in the settings' words, and its size now.</summary>
        private string DisplayEvidence() => DisplayChoices.ModeName(displayMode) + "," + Screen.width + "x" + Screen.height;

        /// <summary>At Start: the baseline for the change lines, and the session's line when the root keeps its own preferences.</summary>
        private void NoteSettingsAtStart()
        {
            loggedSettings = SettingsNow();
            if (Preferences.Route != PreferenceRoute.RootFile) return;
            var session = new List<KeyValuePair<string, string>>(loggedSettings);
            session.Insert(7, new KeyValuePair<string, string>("device", padHints ? "pad" : "keyboard"));
            // And what the first launch's window rule decided (A12): Apply on a participant's first launch.
            session.Add(new KeyValuePair<string, string>("first-run", FirstRunDecision?.ToString() ?? "-"));
            Debug.Log(SettingsEvidence.SessionLine(session));
        }

        /// <summary>A line for each setting that differs from the one last logged; nothing before Start has noted them.</summary>
        private void LogSettingChanges()
        {
            if (loggedSettings == null) return;
            var now = SettingsNow();
            foreach (var line in SettingsEvidence.ChangeLines(loggedSettings, now)) Debug.Log(line);
            loggedSettings = now;
        }

        /// <summary>
        /// Domain reload is off at play start, so the opt-in outlives a play session: it is let go
        /// before the next one begins, and the Isolated Preview sets it again as it sets its root.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ForgetPreferencesBesideSaves() => PreferencesBesideSaves = false;
    }
}
