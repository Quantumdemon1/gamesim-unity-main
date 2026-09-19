using System.Collections.Generic;
using UnityEngine;

namespace Gamesim.Presentation
{
    /// <summary>
    /// The localisation key layer (MASTER-PLAN §3.D). Every caption in the game is a C# string,
    /// and the tests and screen readers identify controls by that string, so the English caption
    /// stays the key: a table maps it to the text a player sees, the HUD asks at each of its text
    /// sinks, and a caption with no entry is shown as it is. Nothing about a control's identity
    /// changes when a table is loaded - a button is still named for its English caption - which
    /// is what keeps the caption contract while the words on screen change.
    ///
    /// <para>A table is a JSON object of English to translated text at
    /// <c>Resources/Localisation/&lt;language&gt;</c>; none ships yet. Composed strings - a name
    /// inside a sentence - fall through untranslated until they are written as formats.</para>
    /// </summary>
    public static class Localisation
    {
        public const string DefaultLanguage = "en";
        public const string ResourceFolder = "Localisation";

        private static Dictionary<string, string> table = new Dictionary<string, string>();

        public static string Language { get; private set; } = DefaultLanguage;
        public static bool HasTable => table.Count > 0;
        public static int TableSize => table.Count;

        /// <summary>The text a player sees for an English caption: its translation, or itself.</summary>
        public static string Text(string english)
        {
            if (string.IsNullOrEmpty(english) || table.Count == 0) return english;
            return table.TryGetValue(english, out var translated) && !string.IsNullOrEmpty(translated) ? translated : english;
        }

        /// <summary>
        /// Loads the table for a language from Resources, or clears it for the default or for a
        /// language with no table. Returns whether a table was found.
        /// </summary>
        public static bool Load(string language)
        {
            language = string.IsNullOrEmpty(language) ? DefaultLanguage : language;
            var next = new Dictionary<string, string>();
            if (language != DefaultLanguage)
            {
                var asset = Resources.Load<TextAsset>(ResourceFolder + "/" + language);
                if (asset != null) Parse(asset.text, next);
            }
            table = next;
            Language = next.Count > 0 ? language : DefaultLanguage;
            return next.Count > 0;
        }

        /// <summary>Uses a table built in code - a test's, or a tool's - under the given language name.</summary>
        public static void Use(string language, IDictionary<string, string> entries)
        {
            table = entries != null ? new Dictionary<string, string>(entries) : new Dictionary<string, string>();
            Language = table.Count > 0 ? (string.IsNullOrEmpty(language) ? "custom" : language) : DefaultLanguage;
        }

        /// <summary>The languages a table ships for, the default first.</summary>
        public static List<string> Available()
        {
            var languages = new List<string> { DefaultLanguage };
            foreach (var asset in Resources.LoadAll<TextAsset>(ResourceFolder))
                if (asset != null && !languages.Contains(asset.name)) languages.Add(asset.name);
            return languages;
        }

        /// <summary>A flat JSON object of string to string; anything else in the file is ignored.</summary>
        public static void Parse(string json, Dictionary<string, string> into)
        {
            if (string.IsNullOrEmpty(json)) return;
            int i = 0;
            string key = null;
            while (i < json.Length)
            {
                char c = json[i];
                if (c == '"')
                {
                    var value = ReadString(json, ref i);
                    if (key == null) key = value;
                    else { into[key] = value; key = null; }
                    continue;
                }
                if (c == ',' ) key = null;
                i++;
            }
        }

        private static string ReadString(string json, ref int i)
        {
            var sb = new System.Text.StringBuilder();
            i++; // the opening quote
            while (i < json.Length && json[i] != '"')
            {
                if (json[i] == '\\' && i + 1 < json.Length)
                {
                    i++;
                    switch (json[i])
                    {
                        case 'n': sb.Append('\n'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            if (i + 4 < json.Length) { sb.Append((char)System.Convert.ToInt32(json.Substring(i + 1, 4), 16)); i += 4; }
                            break;
                        default: sb.Append(json[i]); break;
                    }
                }
                else sb.Append(json[i]);
                i++;
            }
            i++; // the closing quote
            return sb.ToString();
        }
    }
}
