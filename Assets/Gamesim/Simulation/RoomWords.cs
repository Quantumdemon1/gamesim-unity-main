namespace Gamesim.Simulation
{
    /// <summary>
    /// The house's rooms as words. The house builds eight rooms and names them by id - "Living",
    /// "HoH" - and the id is what travels: a houseguest's home room, the room a walk-in command
    /// carries (<see cref="StoryVenues.ForRoom"/> reads the same ids). Both layers say them: the
    /// set's labels and the overview's chips (Presentation's RoomLabels reads this table), and the
    /// engine's own lines, which used to print the id - "Casey Wilson and Maya Hassan are in
    /// Living, mid-argument." (UI-UX-PASS-PLAN D0, the play sweep's row 46).
    /// </summary>
    public static class RoomWords
    {
        /// <summary>The ids of the rooms the house builds, in the order the set lays them out.</summary>
        public static readonly string[] Rooms = { "Living", "Kitchen", "Bedroom", "Private", "Yard", "HoH", "Nomination", "Games" };

        /// <summary>Whether this is one of the rooms the house builds.</summary>
        public static bool IsRoom(string roomId) => roomId != null && System.Array.IndexOf(Rooms, roomId) >= 0;

        /// <summary>A room's name as a card title says it, or a sentence opening with it: "Living room", "HoH suite".</summary>
        public static string Name(string roomId)
        {
            switch (roomId)
            {
                case "Living": return "Living room";
                case "Kitchen": return "Kitchen";
                case "Bedroom": return "Bedroom";
                case "Private": return "Private room";
                case "Yard": return "Competition yard";
                case "HoH": return "HoH suite";
                case "Nomination": return "Nomination room";
                case "Games": return "Game room";
                default: return roomId ?? "";
            }
        }

        /// <summary>
        /// A room's name as it reads mid-sentence, the article left to the sentence: "living room",
        /// "HoH suite". Only a first word that is an ordinary capitalised word is lowered; a name
        /// whose capitals mean something keeps them.
        /// </summary>
        public static string InSentence(string roomId)
        {
            string name = Name(roomId);
            if (string.IsNullOrEmpty(name)) return name;
            int end = name.IndexOf(' ');
            string first = end < 0 ? name : name.Substring(0, end);
            bool ordinary = char.IsUpper(first[0]);
            for (int i = 1; i < first.Length && ordinary; i++) ordinary = !char.IsUpper(first[i]);
            return ordinary ? char.ToLowerInvariant(name[0]) + name.Substring(1) : name;
        }

        /// <summary>
        /// Where somebody is, for a sentence: "the living room" for a room the house builds; any
        /// other text as it came, which is a caller that already said it in words ("the kitchen");
        /// "the house" for nothing at all.
        /// </summary>
        public static string Where(string room)
        {
            if (string.IsNullOrWhiteSpace(room)) return "the house";
            return IsRoom(room) ? "the " + InSentence(room) : room;
        }
    }
}
