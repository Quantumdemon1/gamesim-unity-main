namespace Gamesim.Simulation
{
    public sealed partial class EpisodeEngine
    {
        /// <summary>
        /// Dialogue rules 4's conversation time at a conversation's ending (DIALOGUE-PLAN §4 and §5.4, slice D5c;
        /// DialogueRules.Time.cs): what the path proposes, and what applies. A +1 or -1 applies only in the conversation's
        /// own window while it is open, once a week each, and a -1 only while two conversations remain after this one's cost.
        /// Reads the state as it stands before the ending's own records; draws nothing and writes nothing. Zero below rules 4.
        /// </summary>
        internal static void TimeAtEnd(EpisodeState s, ArcTemplate template, StorylineState cycle, DialogueEpisodeState e, bool lapsed,
            out int proposed, out int applied)
        {
            proposed = applied = DialogueRules.ProposedTime(s, template, cycle, e, lapsed);
            if (proposed == 0) return;
            if (!WeekRulesOn(s) || e.week != s.week || Window(s) != e.window || DialogueRules.TimeTaken(s, s.week, proposed, e)
                || proposed < 0 && SocialActionBudget(s) - SocialActionsSpent(s) < 2)
                applied = 0;
        }

        /// <summary>The outcome line with the receipt's one line after it when a receipt applied; the line itself otherwise.</summary>
        internal static string WithTimeLine(string line, int applied) =>
            applied == 0 ? line : line + " " + DialogueRules.TimeLine(applied);
    }
}
