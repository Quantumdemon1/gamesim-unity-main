using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Gamesim.Simulation
{
    /// <summary>
    /// A talk line's tokens and parts. A line holds a stage direction in parentheses and spoken words in double quotes, and may
    /// hold the tokens <c>{name}</c> (the housemate), <c>{player}</c> (the player), <c>{hoh}</c> (the Head of Household, or
    /// "nobody"), <c>{nominees}</c> (both nominees, or "nobody yet") and, in a standing line only, <c>{close}</c>, <c>{worry}</c>
    /// and <c>{safe}</c> (<see cref="TalkStanding"/>). An unknown token is left as written.
    ///
    /// <para>A person is named by their first name (<see cref="CharacterNames.FirstName"/>, past a title: "Dr. Pat Example" is Pat), as the
    /// house talks. The two talk lines a season keeps, the standing line (<c>contact.standing</c>) and a stage's event row, write
    /// names by <see cref="SavedName"/>: full card names below talk rules 4, so a season saves the words it always saved, and
    /// first names from it. A saved line is shown through <see cref="FirstNames"/>, so the screen says first names either way.</para>
    /// </summary>
    public static class TalkText
    {
        /// <summary>The tokens a line may hold outside a standing line, and the three a standing line adds.</summary>
        public static readonly string[] Tokens = { "name", "player", "hoh", "nominees" };
        public static readonly string[] StandingTokens = { "close", "worry", "safe" };

        /// <summary>A line with its tokens filled for a talk with <paramref name="npcId"/>, each person by their first name; the standing tokens from <paramref name="standing"/> when given.</summary>
        public static string Fill(EpisodeState s, string npcId, string line, IDictionary<string, string> standing = null) =>
            Fill(s, npcId, line, standing, CharacterNames.FirstName);

        /// <summary>
        /// A name as a talk writes it into the season (the standing line, a stage's event row): from talk rules 4
        /// (<see cref="TalkRules.Couples"/>) the first name; below it the full card name, exactly as every season before it saved.
        /// </summary>
        public static string SavedName(EpisodeState s, string name) =>
            name == null ? null : TalkRules.At(s, TalkRules.Couples) ? CharacterNames.FirstName(name) : name;

        /// <summary>
        /// A saved talk line as the screen shows it: each full card name of the season's cast in it read as that person's first
        /// name. The saved text never changes; a line saved at talk rules 4 already holds first names and reads as it is.
        /// </summary>
        public static string FirstNames(EpisodeState s, string saved)
        {
            if (string.IsNullOrEmpty(saved) || s?.contestants == null) return saved ?? "";
            var names = new List<KeyValuePair<string, string>>();
            foreach (var c in s.contestants)
            {
                string full = c?.name;
                if (string.IsNullOrEmpty(full)) continue;
                string first = CharacterNames.FirstName(full);
                if (first.Length == 0 || first == full || names.Any(p => p.Key == full)) continue;
                names.Add(new KeyValuePair<string, string>(full, first));
            }
            if (names.Count == 0) return saved;
            // The longest name first, so one name inside another is never cut short; one pass, so a first name is never read again.
            names.Sort((a, b) => a.Key.Length != b.Key.Length ? b.Key.Length - a.Key.Length : string.CompareOrdinal(a.Key, b.Key));
            var sb = new StringBuilder(saved.Length);
            int i = 0;
            while (i < saved.Length)
            {
                int at = names.FindIndex(p => WordAt(saved, i, p.Key));
                if (at >= 0) { sb.Append(names[at].Value); i += names[at].Key.Length; }
                else sb.Append(saved[i++]);
            }
            return sb.ToString();
        }

        private static bool WordAt(string text, int index, string word) =>
            word.Length <= text.Length - index && string.CompareOrdinal(text, index, word, 0, word.Length) == 0
            && (index == 0 || !char.IsLetterOrDigit(text[index - 1]))
            && (index + word.Length == text.Length || !char.IsLetterOrDigit(text[index + word.Length]));

        /// <summary>A line with its tokens filled, each person's name as <paramref name="nameOf"/> gives it (<see cref="SavedName"/> for a saved line).</summary>
        public static string Fill(EpisodeState s, string npcId, string line, IDictionary<string, string> standing, Func<string, string> nameOf)
        {
            if (string.IsNullOrEmpty(line) || line.IndexOf('{') < 0) return line ?? "";
            var sb = new StringBuilder(line.Length + 32);
            int i = 0;
            while (i < line.Length)
            {
                int open = line.IndexOf('{', i);
                int close = open < 0 ? -1 : line.IndexOf('}', open + 1);
                if (open < 0 || close < 0) { sb.Append(line, i, line.Length - i); break; }
                sb.Append(line, i, open - i);
                string token = line.Substring(open + 1, close - open - 1);
                string value = Value(s, npcId, token, standing, nameOf);
                sb.Append(value ?? line.Substring(open, close - open + 1));
                i = close + 1;
            }
            return sb.ToString();
        }

        private static string Value(EpisodeState s, string npcId, string token, IDictionary<string, string> standing, Func<string, string> nameOf)
        {
            if (standing != null && standing.TryGetValue(token, out var part)) return part;
            switch (token)
            {
                case "name": return Named(s?.Find(npcId)?.name, nameOf);
                case "player": return Named(s?.Find(s.playerId)?.name, nameOf);
                case "hoh": return string.IsNullOrEmpty(s?.hohId) ? "nobody" : Named(s.Find(s.hohId)?.name, nameOf) ?? "nobody";
                case "nominees":
                    var names = s?.nominees?.Select(id => Named(s.Find(id)?.name, nameOf)).Where(n => !string.IsNullOrEmpty(n)).ToList();
                    return names == null || names.Count == 0 ? "nobody yet" : string.Join(" and ", names);
                default: return null;
            }
        }

        private static string Named(string name, Func<string, string> nameOf) => name == null || nameOf == null ? name : nameOf(name);

        /// <summary>The spoken words of a line: the text inside its double quotes, joined by a space; the whole line when it has none.</summary>
        public static string Spoken(string line)
        {
            if (string.IsNullOrEmpty(line)) return "";
            var parts = new List<string>();
            int i = 0;
            while (true)
            {
                int open = line.IndexOf('"', i);
                if (open < 0) break;
                int close = line.IndexOf('"', open + 1);
                if (close < 0) break;
                parts.Add(line.Substring(open + 1, close - open - 1));
                i = close + 1;
            }
            return parts.Count == 0 ? line.Trim() : string.Join(" ", parts);
        }

        /// <summary>A line's stage direction (the text inside its first parentheses before any quote), or "".</summary>
        public static string Direction(string line)
        {
            if (string.IsNullOrEmpty(line)) return "";
            int quote = line.IndexOf('"');
            int open = line.IndexOf('(');
            if (open < 0 || (quote >= 0 && open > quote)) return "";
            int close = line.IndexOf(')', open + 1);
            return close < 0 ? "" : line.Substring(open + 1, close - open - 1);
        }
    }
}
