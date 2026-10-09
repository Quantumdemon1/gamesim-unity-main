using System;

namespace Gamesim.Simulation
{
    /// <summary>
    /// D2's act kinds (WAVE-D-NPC-PACTS-PLAN §4.3): what one of the house's acts was, as
    /// <see cref="NpcActState.kind"/> stores it - where it can be staged, how a witness sees it, and what a
    /// paid listen-in hears of it. Constants and pure functions; the engine records and reads them
    /// (<c>EpisodeEngine.AllWeek</c>).
    /// </summary>
    public static class NpcActKinds
    {
        /// <summary>An ordinary conversation, and the agenda's three warm pursuits: building, holding, courting.</summary>
        public const string Talk = "talk", Build = "build", Hold = "hold", Court = "court";
        /// <summary>A pact-mate told the threat has to go (the agenda's hunt).</summary>
        public const string Hunt = "hunt";
        /// <summary>A nominee asking a voter to keep them (the campaign pass, recorded).</summary>
        public const string Campaign = "campaign";
        /// <summary>A pact formed, a word given, a pact's meeting.</summary>
        public const string Pact = "pact", Promise = "promise", Meet = "meet";
        /// <summary>A rumour started, a fight picked, a conversation listened in on.</summary>
        public const string Rumour = "rumour", Confront = "confront", Eavesdrop = "eavesdrop";

        public static readonly string[] All = { Talk, Build, Hold, Court, Hunt, Campaign, Pact, Promise, Meet, Rumour, Confront, Eavesdrop };

        public static bool IsKnown(string kind) => kind != null && Array.IndexOf(All, kind) >= 0;

        /// <summary>The agenda's pursuit (the ladder's once-a-week rung); a recorded court fills it.</summary>
        public static bool IsPursuit(string kind) => kind == Build || kind == Hold || kind == Court || kind == Hunt;

        /// <summary>A fight: staged loud, and witnessed as overheard as well as seen.</summary>
        public static bool IsLoud(string kind) => kind == Confront;

        /// <summary>Two people with their heads together: the strategy kinds, as against an ordinary word.</summary>
        public static bool IsPrivate(string kind) =>
            kind == Court || kind == Pact || kind == Promise || kind == Meet || kind == Rumour || kind == Hunt;

        private static readonly string[] Common = { "Kitchen", "Living" };
        private static readonly string[] Quiet = { "Bedroom", "Yard", "Games" };
        private static readonly string[] Suite = { "HoH" };
        private static readonly string[] Loud = { "Living", "Kitchen" };
        private static readonly string[] Nowhere = new string[0];

        /// <summary>
        /// The rooms an act of this kind is staged in (§4.3): an ordinary word and a campaign visit in the
        /// kitchen or the living room; courting in the HoH suite; the strategy kinds somewhere quiet - the
        /// bedroom, the yard, the game room - or the suite where one of the two holds it; a fight in the
        /// living room or the kitchen. An NPC listening in is never staged. Ids the house builds (<see cref="RoomWords"/>).
        /// </summary>
        public static string[] Rooms(string kind, bool hohParty)
        {
            switch (kind)
            {
                case Talk: case Build: case Hold: case Campaign: return Common;
                case Court: return Suite;
                case Pact: case Promise: case Meet: case Rumour: case Hunt: return hohParty ? Suite : Quiet;
                case Confront: return Loud;
                default: return Nowhere;
            }
        }

        /// <summary>
        /// What a paid listen-in on the pair of an open act the player saw hears of it (§4.3), as a sentence
        /// for <c>EpisodeEngine.EavesdropLine</c>'s act clause, with its leading space; null for a kind with
        /// nothing to hear beyond the reading. Names only: never a pact's name, never a number.
        /// </summary>
        public static string Clause(string kind, string actor, string partner, string subject)
        {
            switch (kind)
            {
                case Hunt: return subject == null ? null : " " + actor + " told " + partner + " that " + subject + " has to go.";
                case Rumour: return subject == null ? null : " " + actor + " told " + partner + " that " + subject + " is the biggest threat in this house.";
                case Pact: return " " + actor + " and " + partner + " agreed to work together.";
                case Meet: return " " + actor + " and " + partner + " were going over a plan.";
                case Promise: return " " + actor + " gave " + partner + " their word.";
                case Court: return " " + actor + " was making their case to " + partner + ".";
                default: return null;
            }
        }
    }
}
