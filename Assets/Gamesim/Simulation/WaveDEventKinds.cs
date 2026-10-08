namespace Gamesim.Simulation
{
    /// <summary>
    /// Schema 28 vocabulary: the event kinds Wave D's lines will carry (WAVE-D-NPC-PACTS-PLAN §0.3).
    /// Constants only. Nothing in this build logs them, and validation refuses each until its own
    /// design's start week (<c>EpisodeValidation.WaveD</c>).
    /// </summary>
    public static class WaveDEventKinds
    {
        /// <summary>D2: the player saw two houseguests together.</summary>
        public const string Sighting = "sighting";
        /// <summary>D2: the player heard a loud act.</summary>
        public const string Overheard = "overheard";
        /// <summary>D3: how a war-room plan settled.</summary>
        public const string PactPlan = "pact-plan";
        /// <summary>D4: an ally found out about the player's other pact.</summary>
        public const string DoubleDealing = "double-dealing";

        public static readonly string[] All = { Sighting, Overheard, PactPlan, DoubleDealing };
    }
}
