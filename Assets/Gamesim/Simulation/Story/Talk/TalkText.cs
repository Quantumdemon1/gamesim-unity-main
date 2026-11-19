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
    /// </summary>
    public static class TalkText
    {
        /// <summary>The tokens a line may hold outside a standing line, and the three a standing line adds.</summary>
        public static readonly string[] Tokens = { "name", "player", "hoh", "nominees" };
        public static readonly string[] StandingTokens = { "close", "worry", "safe" };

        /// <summary>A line with its tokens filled for a talk with <paramref name="npcId"/>; the standing tokens from <paramref name="standing"/> when given.</summary>
        public static string Fill(EpisodeState s, string npcId, string line, IDictionary<string, string> standing = null)
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
                string value = Value(s, npcId, token, standing);
                sb.Append(value ?? line.Substring(open, close - open + 1));
                i = close + 1;
            }
            return sb.ToString();
        }

        private static string Value(EpisodeState s, string npcId, string token, IDictionary<string, string> standing)
        {
            if (standing != null && standing.TryGetValue(token, out var part)) return part;
            switch (token)
            {
                case "name": return s?.Find(npcId)?.name;
                case "player": return s?.Find(s.playerId)?.name;
                case "hoh": return string.IsNullOrEmpty(s?.hohId) ? "nobody" : s.Find(s.hohId)?.name ?? "nobody";
                case "nominees":
                    var names = s?.nominees?.Select(id => s.Find(id)?.name).Where(n => !string.IsNullOrEmpty(n)).ToList();
                    return names == null || names.Count == 0 ? "nobody yet" : string.Join(" and ", names);
                default: return null;
            }
        }

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
