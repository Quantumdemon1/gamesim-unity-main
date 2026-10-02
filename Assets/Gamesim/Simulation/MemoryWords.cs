using System;

namespace Gamesim.Simulation
{
    /// <summary>
    /// A memory as the player reads it.
    ///
    /// <para>The engine's memory text is matched by name: <c>WebEvictionVoting.Memory</c> counts a
    /// memory naming a nominee toward that nominee's ballot (+2, parity-pinned to the web's ten
    /// factors), on the live path of every NPC ballot. So a line the engine writes without a name
    /// - "Nominated me in week 2." - has to stay nameless, or naming the Head of Household would
    /// move every recorded season's ballots (UI-UX-PASS-PLAN rule 3). The subject is on the memory
    /// already; this says it where the player reads the line, which the diary's chair showed with
    /// no subject at all (D0, the play sweep's row 38).</para>
    /// </summary>
    public static class MemoryWords
    {
        private const string Nominated = "Nominated me in week ";

        /// <summary>
        /// The memory's line with its subject named where the engine left it out: "Casey Wilson
        /// nominated me in week 2.", or "You nominated me in week 2." in a houseguest's memory of
        /// the player as Head of Household. Any other memory reads as it was written.
        /// </summary>
        public static string Said(EpisodeState s, MemoryState m)
        {
            if (m?.text == null) return null;
            if (!m.text.StartsWith(Nominated, StringComparison.Ordinal) || string.IsNullOrEmpty(m.subjectId)) return m.text;
            string name = s?.Find(m.subjectId)?.name ?? "Unknown housemate";
            return name + " nominated me in week " + m.text.Substring(Nominated.Length);
        }
    }
}
