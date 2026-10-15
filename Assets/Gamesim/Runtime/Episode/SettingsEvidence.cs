using System;
using System.Collections.Generic;
using System.Text;

namespace Gamesim.Episode
{
    /// <summary>
    /// The player log's account of a session's settings (PLAN A, A13), so a playtest's handoff can be
    /// filled in from the log: the device, the text size, motion, the ceremony pace, the window.
    /// Three kinds of line, each one line of <c>key=value</c> words with no spaces inside a value:
    ///
    /// <list type="bullet">
    /// <item>"Gamesim settings: text=standard motion=full ... device=keyboard" once, as a session
    /// with its own preferences file starts (never otherwise: a test's reload expects its log quiet);</item>
    /// <item>"Gamesim setting changed: text=larger", one per value that differs from the last one
    /// logged, never at Start;</item>
    /// <item>"Gamesim input device: pad", each time the device pressed on changes.</item>
    /// </list>
    ///
    /// <para>Unity-free, so the subset holds the wording and the comparison.</para>
    /// </summary>
    public static class SettingsEvidence
    {
        public const string SessionPrefix = "Gamesim settings: ";
        public const string ChangePrefix = "Gamesim setting changed: ";
        public const string DevicePrefix = "Gamesim input device: ";

        /// <summary>A value as one word: spaces and equals signs would split it when the log is read back.</summary>
        public static string Word(string value)
        {
            if (string.IsNullOrEmpty(value)) return "-";
            var word = new StringBuilder(value.Length);
            foreach (char c in value) word.Append(char.IsWhiteSpace(c) || c == '=' ? '_' : c);
            return word.ToString();
        }

        /// <summary>The session's line: every setting, in the order given.</summary>
        public static string SessionLine(IEnumerable<KeyValuePair<string, string>> settings)
        {
            var line = new StringBuilder(SessionPrefix);
            bool first = true;
            foreach (var setting in settings)
            {
                if (!first) line.Append(' ');
                first = false;
                line.Append(setting.Key).Append('=').Append(Word(setting.Value));
            }
            return line.ToString();
        }

        /// <summary>One line for each setting whose value is not the one last logged, in the order given.</summary>
        public static List<string> ChangeLines(IEnumerable<KeyValuePair<string, string>> lastLogged, IEnumerable<KeyValuePair<string, string>> now)
        {
            var before = new Dictionary<string, string>(StringComparer.Ordinal);
            if (lastLogged != null) foreach (var setting in lastLogged) before[setting.Key] = Word(setting.Value);
            var lines = new List<string>();
            foreach (var setting in now)
            {
                string word = Word(setting.Value);
                if (before.TryGetValue(setting.Key, out var old) && old == word) continue;
                lines.Add(ChangePrefix + setting.Key + "=" + word);
            }
            return lines;
        }

        /// <summary>The device line.</summary>
        public static string DeviceLine(bool pad) => DevicePrefix + (pad ? "pad" : "keyboard");

        /// <summary>The window's setting: its mode and size as the window stands, not as the record asks.</summary>
        public const string DisplayKey = "display";

        /// <summary>
        /// Whether a window the game asked for <paramref name="appliedAt"/> may still be on its way at
        /// <paramref name="now"/>: Unity changes the window at the end of a later frame (an exclusive
        /// switch can take longer), and until it has the window still reads as it was. Never in the
        /// editor or a batch run, where the window is not the game's and the record is the window.
        /// </summary>
        public static bool WindowSettling(bool editorOrBatch, float now, float appliedAt, float settleSeconds) =>
            !editorOrBatch && now - appliedAt < settleSeconds;

        /// <summary>
        /// The settings <paramref name="now"/> with the window's line held at the one last logged, for
        /// while a window the game asked for is still on its way: logged then, it would say the old
        /// window as the change and leave the new one to come out with whatever setting changed next.
        /// Nothing logged yet holds nothing.
        /// </summary>
        public static List<KeyValuePair<string, string>> HoldWindow(IEnumerable<KeyValuePair<string, string>> lastLogged, IEnumerable<KeyValuePair<string, string>> now)
        {
            string held = null;
            if (lastLogged != null) foreach (var setting in lastLogged) if (setting.Key == DisplayKey) held = setting.Value;
            var settings = new List<KeyValuePair<string, string>>();
            foreach (var setting in now)
                settings.Add(setting.Key == DisplayKey && held != null ? new KeyValuePair<string, string>(DisplayKey, held) : setting);
            return settings;
        }
    }
}
