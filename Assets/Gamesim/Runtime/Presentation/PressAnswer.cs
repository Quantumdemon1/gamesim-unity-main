using System;

namespace Gamesim.Presentation
{
    /// <summary>
    /// Every press answers (AU-10). A press that asks for no sound of its own - a tab, a picker's
    /// arrow, a switch, a way back, Save on the game menu - is answered with the button's click, and
    /// a press that does ask for one - a panel arriving or leaving, a commit's own sound - is left to
    /// that sound alone, so no press sounds twice. Which it was is read off the house's count of the
    /// cues asked for, before and after the press runs. Before, such a press made no sound at all,
    /// and the front door's screens made none for any press.
    ///
    /// <para>Unity-free, so the suite that runs without Unity holds the rule. Presentation only:
    /// nothing is drawn from the season's stream and nothing is saved.</para>
    /// </summary>
    public static class PressAnswer
    {
        /// <summary>
        /// Runs <paramref name="action"/>, then plays <paramref name="click"/> when the count that
        /// <paramref name="asked"/> reads did not move while it ran. Nothing to run is no press and
        /// makes no sound; with no count to read, a press always clicks.
        /// </summary>
        public static void Run(Action action, Func<int> asked, Action click)
        {
            if (action == null) return;
            int before = asked != null ? asked() : 0;
            action();
            if (click != null && (asked == null || asked() == before)) click();
        }
    }
}
