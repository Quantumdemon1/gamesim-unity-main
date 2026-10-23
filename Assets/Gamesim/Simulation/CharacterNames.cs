using System;

namespace Gamesim.Simulation
{
    /// <summary>
    /// Short labels for an existing character identity. Formatting never changes the saved full
    /// name, template ID or model lookup key. Period-terminated title/initial tokens follow the
    /// convention already used by the cast selector and finalist reader.
    /// </summary>
    public static class CharacterNames
    {
        public static string FirstName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return string.Empty;
            var words = name.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            foreach (string word in words)
                if (!word.EndsWith(".", StringComparison.Ordinal)) return word;
            return words[0];
        }

        /// <summary>The given-name initial for portrait fallbacks, or ? for an unnamed person.</summary>
        public static string Initial(string name)
        {
            string first = FirstName(name);
            return first.Length == 0 ? "?" : first.Substring(0, 1);
        }
    }
}
