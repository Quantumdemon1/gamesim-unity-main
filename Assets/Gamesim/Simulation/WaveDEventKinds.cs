namespace Gamesim.Simulation
{
    /// <summary>
    /// Schema 28 vocabulary: the event kinds Wave D's lines carry (WAVE-D-NPC-PACTS-PLAN §0.3). Constants.
    /// D4's double-dealing line (the leak rules, <see cref="AllianceLeaks"/>), D3's pact-plan line (the war
    /// rooms, <see cref="PactPlans"/>) and D2's sighting and overheard lines (the all-week rules, a witness of
    /// an act, <c>EpisodeEngine.AllWeek</c>) are each logged under their own start week. Validation refuses
    /// each until its own design's start week (<c>EpisodeValidation.WaveD</c>).
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
