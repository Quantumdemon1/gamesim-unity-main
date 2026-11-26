using System.Linq;
using Gamesim.Simulation;

namespace Gamesim.Presentation
{
    /// <summary>
    /// What a "Talk to" on the campaign board opens the conversation on (UI-UX plan D9): what the
    /// player walked over for, rather than the six topics.
    ///
    /// <para>A player on the block who can still put their plea to this voter lands on the plea,
    /// on This week's game. Anybody else talking to a voter lands on Learn and share, where the vote
    /// question is. Everybody else on the board - the Head of Household and the other nominee - and
    /// any talk outside the campaign or from a player out of the game opens on the topics, as it did.</para>
    ///
    /// <para>A read of the state, and nothing more: it commits, saves and draws nothing, and nothing
    /// in it draws from the season's stream. Plain C# with no engine, so the Unity-free suite runs it.</para>
    /// </summary>
    public static class CampaignTalk
    {
        /// <summary>Where the conversation opens: the topics, the asks (Learn and share) or the plea (This week's game).</summary>
        public enum Opens { Topics, Asks, Plea }

        /// <summary>Where a talk from the campaign board to this houseguest opens, for the state as it stands.</summary>
        public static Opens For(EpisodeState state, string id)
        {
            if (state == null || string.IsNullOrEmpty(id) || state.phase != EpisodePhase.Campaign) return Opens.Topics;
            if (state.Find(state.playerId)?.status != ContestantStatus.Active) return Opens.Topics;
            var them = state.Find(id);
            if (them == null || them.isPlayer || them.status != ContestantStatus.Active) return Opens.Topics;
            // The plea the conversation's Game page offers (StrategyRules.Pleas): from the block, one to each voter.
            if (StrategyRules.Pleas(state, id).Count > 0) return Opens.Plea;
            return EpisodeEngine.Voters(state).Any(voter => voter.id == id) ? Opens.Asks : Opens.Topics;
        }
    }
}
