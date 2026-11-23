using Gamesim.Simulation;

namespace Gamesim.Episode
{
    /// <summary>
    /// Dialogue rules 4's nominee grant on the plea panel (DIALOGUE-PLAN §4, slice D5a): while a nominated player's
    /// conversation would be on the house, the person's menu says so in one plain line (<see cref="DialogueRules.GrantLine"/>),
    /// the conversation's entry says it costs no action, and the desperate plea that opens it can be pressed with no action
    /// left; every other plea stays paid, and is locked when no action is left. Absent below rules 4 and wherever the engine
    /// would not pay for the conversation (EpisodeDirector.Strategy.cs's LobbyPanel reads <see cref="DialogueRules.OnTheHouseWith"/>).
    /// </summary>
    public sealed partial class EpisodeDirector
    {
        /// <summary>The entry's paragraph while the grant pays: the plain line, then what the conversation costs.</summary>
        public const string OnTheHouseEntryLine = DialogueRules.GrantLine
            + " A desperate plea starts it for no action, and its replies and final plea cost none either.";

        /// <summary>The desperate plea's tag while the grant pays. As short as <see cref="NoConversationTag"/>, so it keeps to one line.</summary>
        public const string OnTheHouseTag = "on the house";

        /// <summary>A paid plea's tag when no action is left and only the conversation on the house is open.</summary>
        public const string NoActionTag = "no actions left";
    }
}
