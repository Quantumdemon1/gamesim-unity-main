namespace Gamesim.Simulation
{
    /// <summary>
    /// The rooms the simulation can name.
    ///
    /// <para>The reference's <c>LOCATIONS</c> list, and it exists here for the same reason it exists
    /// there: a line of ambient narration has to say where something happened, and the simulation
    /// cannot ask the house.</para>
    ///
    /// <para><b>The house knows better, and where it can answer it should.</b>
    /// <c>Gamesim.Simulation</c> does not reference <c>Gamesim.Runtime</c> — the dependency runs the
    /// other way — so the engine has no route to <c>HouseRoomQuery</c> and the real rooms a player
    /// is walking through. Anything the director drives, <see cref="HouseEventSources.Proximity"/>
    /// most of all, takes its room as an argument and should be given the actual one.</para>
    /// </summary>
    public static class HouseRooms
    {
        /// <summary>
        /// The names, matching the set the prototype house actually builds where it can.
        ///
        /// <para>Trimmed of the reference's hot tub and hammock, which this house does not have.
        /// Naming a room the player cannot walk into would be the narration contradicting the set,
        /// which is worse than a shorter list.</para>
        /// </summary>
        public static readonly string[] All =
        {
            "the kitchen",
            "the living room",
            "the backyard",
            "the bedroom",
            "the bathroom",
            "the storage room",
            "the hallway",
            "the dining table",
        };

        /// <summary>One of them, chosen by a roll the caller supplies.</summary>
        public static string Any(EpisodeState state, double roll)
        {
            if (All.Length == 0) return "the house";
            if (double.IsNaN(roll) || roll < 0) return All[0];
            int index = (int)(roll * All.Length);
            return All[index >= All.Length ? All.Length - 1 : index];
        }
    }
}
