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

        /// <summary>
        /// The first name the engine writes into a season's state - a pact's name, a line of its log - by the season's
        /// own rule. From alumni rules 6 (<see cref="AlumniRules.TitledNames"/>) it passes over a title, as the displays
        /// do (<see cref="FinalistRead.FirstName"/>, which splits on spaces only, as saved text does: "Dr. Will Kirby" is
        /// Will). Below it the name is cut at its first space, as it was when every season saved before it was played,
        /// so their replays write the words they wrote ("The Dr. Pact"). Display text, never saved, uses
        /// <see cref="FirstName"/>.
        /// </summary>
        public static string SeasonFirstName(EpisodeState s, string name)
        {
            if (string.IsNullOrEmpty(name)) return string.Empty;
            return AlumniRules.At(s, AlumniRules.TitledNames) ? FinalistRead.FirstName(name) : name.Split(' ')[0];
        }

        /// <summary>The given-name initial for portrait fallbacks, or ? for an unnamed person.</summary>
        public static string Initial(string name)
        {
            string first = FirstName(name);
            return first.Length == 0 ? "?" : first.Substring(0, 1);
        }
    }
}
