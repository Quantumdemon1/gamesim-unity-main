using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Gamesim.Episode;
using Gamesim.Persistence;
using Gamesim.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// Playtest-safe preferences (PLAN A, A13): a root that asked for its own preferences keeps them
    /// in its preferences.json across a reload and leaves the machine's PlayerPrefs alone, and the
    /// player log says what the session started with and each value that changed. A root that did
    /// not ask - every other test's, the verifier's - starts from the defaults and keeps nothing,
    /// as it always has.
    ///
    /// <para>The log is read through <see cref="Application.logMessageReceived"/> rather than
    /// LogAssert expectations, whose queue matches only in registration order.</para>
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        /// <summary>Every key the settings keep, as an ordinary install keeps them in PlayerPrefs.</summary>
        private static readonly string[] SettingKeys =
        {
            "Gamesim.Muted", "Gamesim.ReducedMotion", "Gamesim.ReducedAudio", "Gamesim.LargeText", "Gamesim.Volume", "Gamesim.Music",
            "Gamesim.CeremonyPace", "Gamesim.Language", "Gamesim.Quality", "Gamesim.FrameCap", "Gamesim.Fullscreen", "Gamesim.EdgePan",
            "Gamesim.CompactHud", "Gamesim.CameraSpeed", "Gamesim.InvertY", "Gamesim.VSync", "Gamesim.DisplayMode", "Gamesim.Resolution",
        };

        /// <summary>The machine's PlayerPrefs for every settings key: present or not, and what each holds as text and as a number.</summary>
        private static string PlayerPrefsSnapshot() => string.Join(" | ", SettingKeys.Select(key => key + "="
            + (PlayerPrefs.HasKey(key) ? PlayerPrefs.GetString(key, "") + "/" + PlayerPrefs.GetInt(key, int.MinValue) : "(none)")));

        [UnityTest, Timeout(180000)]
        public IEnumerator Preferences_AnIsolatedRootKeepsItsSettingsAcrossAReload_AndLeavesPlayerPrefsAlone()
        {
            string before = PlayerPrefsSnapshot();
            string file = Path.Combine(temporaryDirectory, PreferenceFile.FileName);
            Assert.That(director.PreferenceRouteInUse, Is.EqualTo(PreferenceRoute.Defaults), "The fixture's root has not asked for its own.");
            Assert.That(File.Exists(file), Is.False);
            var log = new List<string>();
            Application.LogCallback listen = (text, trace, type) => { if (type == LogType.Log) log.Add(text); };
            Application.logMessageReceived += listen;
            try
            {
                // Opted in as the command line and the Isolated Preview opt in, then the house loaded again.
                EpisodeDirector.PreferencesBesideSaves = true;
                director.FreezeForReloadForDiagnostics();
                yield return ReloadEpisode();
                Assert.That(director.PreferenceRouteInUse, Is.EqualTo(PreferenceRoute.RootFile));
                Assert.That(director.LargeText, Is.False, "A root with no file yet starts from the defaults.");
                var session = log.Where(line => line.StartsWith(SettingsEvidence.SessionPrefix)).ToList();
                Assert.That(session, Has.Count.EqualTo(1), "One session line as the house starts: " + string.Join(" | ", log));
                foreach (var word in new[] { "text=standard", "motion=full", "pace=suspenseful", "volume=35", "muted=no", "music=on", "display=", "device=keyboard" })
                    Assert.That(session[0], Does.Contain(word), "The session line names " + word + ": " + session[0]);
                Assert.That(log.Any(line => line.StartsWith(SettingsEvidence.ChangePrefix)), Is.False, "Nothing is a change at Start.");
                Assert.That(File.Exists(file), Is.True, "The root's file holds the session's settings from its start.");

                log.Clear();
                director.SetLargeText(true);
                director.SetCeremonyPace(CeremonyPace.Quick);
                yield return null;
                Assert.That(log.Where(line => line.StartsWith(SettingsEvidence.ChangePrefix)).ToArray(),
                    Is.EqualTo(new[] { SettingsEvidence.ChangePrefix + "text=larger", SettingsEvidence.ChangePrefix + "pace=quick" }),
                    "One line for each value that changed, as it changed: " + string.Join(" | ", log));
                Assert.That(File.ReadAllText(file), Does.Contain("\"Gamesim.LargeText\": \"1\"").And.Contain("\"Gamesim.CeremonyPace\": \"1\""));

                // The same root, loaded again: the settings come back from its own file.
                director.FreezeForReloadForDiagnostics();
                yield return ReloadEpisode();
                Assert.That(director.PreferenceRouteInUse, Is.EqualTo(PreferenceRoute.RootFile));
                Assert.That(director.LargeText, Is.True, "Larger text survives the reload.");
                Assert.That(director.CeremonyPaceSetting, Is.EqualTo(CeremonyPace.Quick), "and so does the quick pace.");
            }
            finally
            {
                Application.logMessageReceived -= listen;
            }
            Assert.That(PlayerPrefsSnapshot(), Is.EqualTo(before), "The machine's PlayerPrefs are exactly what they were.");
        }

        /// <summary>
        /// Without the opt-in a test's root is what it has always been: the defaults at every load,
        /// nothing kept, no file, no session line, and the machine's PlayerPrefs untouched.
        /// </summary>
        [UnityTest, Timeout(180000)]
        public IEnumerator Preferences_TheTestRootStillStartsFromDefaults()
        {
            string before = PlayerPrefsSnapshot();
            string file = Path.Combine(temporaryDirectory, PreferenceFile.FileName);
            Assert.That(EpisodeDirector.PreferencesBesideSaves, Is.False);
            Assert.That(director.PreferenceRouteInUse, Is.EqualTo(PreferenceRoute.Defaults));
            var log = new List<string>();
            Application.LogCallback listen = (text, trace, type) => { if (type == LogType.Log) log.Add(text); };
            Application.logMessageReceived += listen;
            try
            {
                director.SetLargeText(true);
                director.SetCeremonyPace(CeremonyPace.Quick);
                yield return null;
                Assert.That(director.LargeText, Is.True, "The setting applies for the session.");
                director.FreezeForReloadForDiagnostics();
                yield return ReloadEpisode();
                Assert.That(director.PreferenceRouteInUse, Is.EqualTo(PreferenceRoute.Defaults));
                Assert.That(director.LargeText, Is.False, "and is not kept: the reload starts from the defaults.");
                Assert.That(director.CeremonyPaceSetting, Is.EqualTo(CeremonyPace.Suspenseful));
                Assert.That(File.Exists(file), Is.False, "No preferences file is written under a root that did not ask.");
                Assert.That(log.Any(line => line.StartsWith(SettingsEvidence.SessionPrefix)), Is.False, "No session line: the reload's log stays as it was.");
            }
            finally
            {
                Application.logMessageReceived -= listen;
            }
            Assert.That(PlayerPrefsSnapshot(), Is.EqualTo(before), "The machine's PlayerPrefs are exactly what they were.");
        }
    }
}
