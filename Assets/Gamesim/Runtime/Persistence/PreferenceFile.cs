using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Gamesim.Persistence
{
    /// <summary>
    /// A playtest root's own preferences (PLAN A, A13): the keys PlayerPrefs holds for an ordinary
    /// install - "Gamesim.LargeText", "Gamesim.Volume" and the rest - kept as one flat JSON object
    /// of strings in <c>&lt;root&gt;/preferences.json</c>, so a participant's text size, motion,
    /// pace and window survive a reload of their isolated root without touching the machine's
    /// PlayerPrefs or anybody else's root.
    ///
    /// <para>Unity-free, so the subset checks the format itself. The keys are written in ordinal
    /// order, every value a string (a number in invariant digits), so two equal maps are equal
    /// files. A file that is missing reads as no preferences at all; one that is not a flat object
    /// of strings reads the same, with the reason, and never throws: the defaults are always a
    /// playable game.</para>
    /// </summary>
    public sealed class PreferenceFile
    {
        /// <summary>The file's name inside the save root.</summary>
        public const string FileName = "preferences.json";

        private readonly SortedDictionary<string, string> values = new SortedDictionary<string, string>(StringComparer.Ordinal);

        /// <summary>Every key and its value, in ordinal key order.</summary>
        public IReadOnlyDictionary<string, string> Values => values;

        public int Count => values.Count;

        public bool HasKey(string key) => key != null && values.ContainsKey(key);

        public bool TryGet(string key, out string value)
        {
            value = null;
            return key != null && values.TryGetValue(key, out value);
        }

        public string GetString(string key, string fallback) => TryGet(key, out var value) ? value : fallback;

        /// <summary>The value as an invariant integer, or <paramref name="fallback"/> when it is absent or not one.</summary>
        public int GetInt(string key, int fallback) =>
            TryGet(key, out var value) && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) ? parsed : fallback;

        /// <summary>Sets a value; whether it changed. A null value removes the key.</summary>
        public bool Set(string key, string value)
        {
            if (string.IsNullOrEmpty(key)) throw new ArgumentException("A preference needs a key.", nameof(key));
            if (value == null) return values.Remove(key);
            if (values.TryGetValue(key, out var old) && old == value) return false;
            values[key] = value;
            return true;
        }

        public bool SetInt(string key, int value) => Set(key, value.ToString(CultureInfo.InvariantCulture));

        /// <summary>The file's text: one object, its keys in ordinal order, indented, with a final line break.</summary>
        public string ToJson()
        {
            var text = new StringBuilder();
            // LF on every platform: the writer's indent takes the text writer's line break.
            using (var writer = new StringWriter(text, CultureInfo.InvariantCulture) { NewLine = "\n" })
            using (var json = new JsonTextWriter(writer) { Formatting = Formatting.Indented, Indentation = 2 })
            {
                json.WriteStartObject();
                foreach (var pair in values)
                {
                    json.WritePropertyName(pair.Key);
                    json.WriteValue(pair.Value);
                }
                json.WriteEndObject();
            }
            return text.Append('\n').ToString();
        }

        /// <summary>
        /// The preferences a text holds. Anything but one flat object of string values - broken JSON,
        /// an array, a number where a string belongs, a key twice, text after the object - reads as
        /// no preferences, with <paramref name="reason"/> saying why; null reason is a good file.
        /// </summary>
        public static PreferenceFile Parse(string json, out string reason)
        {
            reason = null;
            var file = new PreferenceFile();
            if (string.IsNullOrWhiteSpace(json)) { reason = "the preferences file is empty"; return file; }
            try
            {
                JToken token;
                using (var reader = new JsonTextReader(new StringReader(json))
                       { DateParseHandling = DateParseHandling.None, FloatParseHandling = FloatParseHandling.Decimal })
                {
                    token = JToken.ReadFrom(reader, new JsonLoadSettings
                    {
                        DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error,
                        CommentHandling = CommentHandling.Ignore,
                    });
                    while (reader.Read())
                        if (reader.TokenType != JsonToken.Comment) throw new JsonReaderException("there is text after the preferences object");
                }
                if (!(token is JObject root)) { reason = "the preferences file is not a JSON object"; return file; }
                foreach (var property in root.Properties())
                {
                    if (property.Value.Type != JTokenType.String)
                    {
                        reason = "the preference " + property.Name + " is not a string";
                        return new PreferenceFile();
                    }
                    if (string.IsNullOrEmpty(property.Name)) { reason = "a preference has no key"; return new PreferenceFile(); }
                    file.values[property.Name] = (string)property.Value;
                }
                return file;
            }
            catch (JsonException error)
            {
                reason = "the preferences file is not valid JSON: " + error.Message;
                return new PreferenceFile();
            }
        }

        /// <summary>
        /// The preferences at <paramref name="path"/>: none, and no reason, when there is no file yet;
        /// none, with the reason, when it cannot be read or parsed. Never throws for a file's sake.
        /// </summary>
        public static PreferenceFile Read(string path, out string reason, Func<string, string> readText = null)
        {
            reason = null;
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return new PreferenceFile();
            string text;
            try { text = readText != null ? readText(path) : File.ReadAllText(path, Encoding.UTF8); }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            {
                reason = "the preferences file could not be read (" + error.GetType().Name + ")";
                return new PreferenceFile();
            }
            return Parse(text, out reason);
        }
    }

    /// <summary>Where a session's preferences are read from and written to (A13).</summary>
    public enum PreferenceRoute
    {
        /// <summary>An ordinary install: the machine's PlayerPrefs.</summary>
        PlayerPrefs,
        /// <summary>An isolated root that asked for its own: <c>&lt;root&gt;/preferences.json</c>.</summary>
        RootFile,
        /// <summary>An isolated root that did not - a test, the verifier: the defaults, nothing written.</summary>
        Defaults,
    }

    /// <summary>
    /// The rule that picks the route, Unity-free so the subset holds it (A13, the lead's decisions 3
    /// and 4). An isolated root keeps its own preferences only when it asked to: the command line's
    /// <c>--gamesim-save-root</c> without <c>--gamesim-verify</c>, or the editor's Isolated Preview.
    /// Never inferred from the root alone, so the PlayMode fixture's roots and the verifier keep the
    /// deterministic defaults they have always had.
    /// </summary>
    public static class PreferenceRouting
    {
        public const string SaveRootArgument = "--gamesim-save-root";
        public const string VerifyArgument = "--gamesim-verify";

        /// <summary>The route for a session whose root is already decided.</summary>
        public static PreferenceRoute For(string saveRootOverride, bool besideSaves) =>
            saveRootOverride == null ? PreferenceRoute.PlayerPrefs : besideSaves ? PreferenceRoute.RootFile : PreferenceRoute.Defaults;

        /// <summary>
        /// The route a launch will take, decided as the director will decide it once it reads the
        /// command line - for whatever wakes before it does (the scene's audio). A root already set
        /// decides; otherwise an absolute <c>--gamesim-save-root</c> is a root of its own unless the
        /// launch verifies; a malformed one asked for isolation and gets the defaults.
        /// </summary>
        public static PreferenceRoute ForLaunch(string saveRootOverride, bool besideSaves, IList<string> args, out string root)
        {
            root = saveRootOverride;
            if (saveRootOverride != null) return For(saveRootOverride, besideSaves);
            if (args == null) return PreferenceRoute.PlayerPrefs;
            int at = args.IndexOf(SaveRootArgument);
            if (at < 0) return PreferenceRoute.PlayerPrefs;
            if (at + 1 >= args.Count || !Path.IsPathRooted(args[at + 1])) return PreferenceRoute.Defaults;
            root = Path.GetFullPath(args[at + 1]);
            return args.Contains(VerifyArgument) ? PreferenceRoute.Defaults : PreferenceRoute.RootFile;
        }

        /// <summary>Whether a root the command line gave keeps its preferences beside its saves: not under the verifier.</summary>
        public static bool BesideSavesForCommandLine(IList<string> args) => args == null || !args.Contains(VerifyArgument);
    }
}
