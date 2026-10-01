using Gamesim.Simulation;

namespace Gamesim.Episode
{
    /// <summary>
    /// What moving on costs, beside the way on of the week's strategy screens (ACTIONS-DEALS-ALLIANCES
    /// -PLAN V2): the offers the press lets lapse, the houseguests who came to the player and go
    /// unanswered, and the window's actions that do not carry. Free time said the last of these under
    /// its own way on and nowhere else, and an offer lapsed without a word anywhere.
    ///
    /// <para>The words are <see cref="WaitingOnYou.AdvanceNote(EpisodeState)"/>'s, read from pending
    /// state: nothing is written to say them. They are the footer strip's second line, under what
    /// comes next, which keeps the line it always had (<see cref="EpisodeHud.FooterRank"/>); the
    /// storylines' warning still takes the strip alone, so a storyline the press lets pass is the
    /// one thing it says. Free time's way on is its own screen's to dress, and is left to it.</para>
    /// </summary>
    public sealed partial class EpisodeDirector
    {
        /// <summary>The name the footer strip's line carries when it says what moving on costs, so a test can find it.</summary>
        public const string MovingOnCostsName = "Moving on costs";

        /// <summary>Puts what moving on costs on the strategy stage's footer strip; nothing anywhere else, nor when it costs nothing.</summary>
        private void MovingOnCosts(EpisodeState state)
        {
            if (state == null || hud == null || state.phase == EpisodePhase.Social) return;
            if (hud.CurrentActivityLayout != EpisodeHud.ActivityLayout.Strategy) return;
            string costs = WaitingOnYou.AdvanceNote(state);
            if (costs != null) hud.PinnedNote(costs, MovingOnCostsName, EpisodeHud.FooterRank.Notice, true);
        }
    }
}
