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
    }
}
