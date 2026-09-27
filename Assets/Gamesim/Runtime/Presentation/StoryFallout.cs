using Gamesim.Simulation;
using UnityEngine;

namespace Gamesim.Presentation
{
    /// <summary>
    /// The story system's ceremonies (plan §5.1, Fallout): a blow-up, production's penalty, a
    /// removal and a house meeting. Each has the tables every ceremony has - its title and line, its
    /// mark and colour, the room it is framed in, and the badge its faces wear. The ceremony tables
    /// defer to this one for a kind they do not know, so a story ceremony is registered everywhere a
    /// ceremony is, and <c>StoryFalloutTests</c> walks every kind through every table.
    /// </summary>
    public static class StoryFallout
    {
        public static bool IsFallout(string kind) => StoryLog.IsFallout(kind);

        /// <summary>The takeover's title, or null for a kind that is not a story ceremony.</summary>
        public static string TitleFor(string kind)
        {
            switch (kind)
            {
                case StoryLog.Blowup: return "The Blow-up";
                case StoryLog.Penalty: return "Production Penalty";
                case StoryLog.Expulsion: return "Removed from the House";
                case StoryLog.HouseMeeting: return "House Meeting";
                default: return null;
            }
        }

        /// <summary>The line under the title: about the room, never the result, as every card's is.</summary>
        public static string FlavourFor(string kind)
        {
            switch (kind)
            {
                case StoryLog.Blowup: return "Voices carry in this house. Everybody heard that.";
                case StoryLog.Penalty: return "Production has seen enough. There are rules in this house.";
                case StoryLog.Expulsion: return "Production has made its decision. One houseguest is leaving the game.";
                case StoryLog.HouseMeeting: return "The whole house in one room, and nobody leaves until it is said.";
                default: return null;
            }
        }

        /// <summary>The generated icon each uses, from the set every ceremony draws on.</summary>
        public static string IconFor(string kind)
        {
            switch (kind)
            {
                case StoryLog.Blowup: return "mood-angry";
                case StoryLog.Penalty: return "gavel";
                case StoryLog.Expulsion: return "exit";
                case StoryLog.HouseMeeting: return "people";
                default: return null;
            }
        }

        public static Color TintFor(string kind)
        {
            switch (kind)
            {
                case StoryLog.Blowup: return UiTheme.Danger;
                case StoryLog.Penalty: return UiTheme.Warning;
                case StoryLog.Expulsion: return UiTheme.Conflict;
                case StoryLog.HouseMeeting: return UiTheme.Accent;
                default: return UiTheme.Accent;
            }
        }

        /// <summary>
        /// The room the camera goes to while the card plays: the living room for the house's own
        /// moments, the diary room for production's.
        /// </summary>
        public static string RoomFor(string kind)
        {
            switch (kind)
            {
                case StoryLog.Blowup:
                case StoryLog.HouseMeeting: return "Living";
                case StoryLog.Penalty:
                case StoryLog.Expulsion: return "Private";
                default: return null;
            }
        }

        /// <summary>What the card says each face is doing there.</summary>
        public static string BadgeFor(string kind)
        {
            switch (kind)
            {
                case StoryLog.Blowup: return "IN THE FIGHT";
                case StoryLog.Penalty: return "PENALISED";
                case StoryLog.Expulsion: return "REMOVED";
                case StoryLog.HouseMeeting: return "IN THE ROOM";
                default: return null;
            }
        }
    }
}
