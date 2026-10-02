using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Gamesim.Simulation;

namespace Gamesim.Presentation
{
    /// <summary>
    /// What a competition says to the player, in the player's words (UI-UX-PASS-PLAN decision 12):
    /// how a result was decided, what each way in does for them, and the line under the standings.
    /// Nothing here is a number of the engine's - no composite score, no weight, no roll - because
    /// nobody in the house ever sees one. The only numbers a competition shows are the order and
    /// the player's own attempt in the game's own measure. Unity-free, so the Unity-free subset
    /// pins the words beside the rules they describe.
    /// </summary>
    public static class CompetitionWords
    {
        /// <summary>The standings' heading on the result card.</summary>
        public const string StandingsHeading = "STANDINGS";

        /// <summary>The one line under the standings.</summary>
        public const string ResultFooter = "Decided on the day. Practice helps; a close race can go either way.";

        /// <summary>The Score details page's heading.</summary>
        public const string DetailsHeading = "HOW IT WAS DECIDED";

        /// <summary>The Score details page's words when there is no season to read: a card played on its own.</summary>
        public const string DetailsFallback = "Who you are and how the day went decided the order. Practice helps; a perfect run does not guarantee the win.";

        /// <summary>The practice board's policy line on the game screen.</summary>
        public const string PracticePolicy = "Practice never changes your season. The real attempt plays on a board of its own.";

        /// <summary>How the order was decided, once the player's own part is said.</summary>
        public const string Close = "The rest was who you are and how the day went: a close race can go either way, and a perfect run does not guarantee the win.";

        /// <summary>What a watcher is told: the field was not theirs.</summary>
        public const string Watched = "You were not in this field. Who they are and how the day went decided the order.";

        /// <summary>
        /// Whether a label says a number with a fraction - a composite score - as against a rank or
        /// a count. The sweeps over the result card and the standings use it, exempting the player's
        /// own attempt by name ("held 12.4 s" is the game's measure).
        /// </summary>
        public static bool HasDecimal(string text) => !string.IsNullOrEmpty(text) && Regex.IsMatch(text, @"\d+\.\d+");

        /// <summary>
        /// The Score details page: how the player entered, what they brought, and that the rest was
        /// the day. Read from the committed state - the ledger's row for this competition, the
        /// throw's private line, the committed field - never from the engine's own
        /// "competition-performance" line, which is arithmetic.
        /// </summary>
        public static string Explanation(EpisodeState state)
        {
            if (state == null || state.competitionScores == null || state.competitionScores.Count == 0) return DetailsFallback;
            var row = state.ledger?.competitions?.LastOrDefault(r => r.week == state.week && r.kind == state.phase.ToString());
            bool inField = state.competitionScores.Any(score => score.contestantId == state.playerId);
            if (!inField || row?.entry == CompetitionEntry.Watched) return Watched;
            bool thrown = row?.entry == CompetitionEntry.Thrown || (state.events != null && state.events.Any(entry =>
                entry.kind == EpisodeEngine.ThrowEventKind && entry.week == state.week && entry.phase == state.phase));
            bool simulated = row?.entry == CompetitionEntry.Simulated || (state.events != null && state.events.Any(entry =>
                entry.kind == "competition" && entry.week == state.week && entry.phase == state.phase
                && entry.text != null && entry.text.Contains("(simulated)")));
            bool endurance = state.phase == EpisodePhase.FinalHoHPart1;
            var lines = new List<string>();
            if (thrown)
                lines.Add("You threw it: every bonus given up, and only part of your score counted. It lowers only your own score, so the day can still hand you the win.");
            else if (simulated)
                lines.Add("You let the day decide: no performance bonus, and what you had earned still counted.");
            else if (endurance)
                lines.Add("You competed: your performance added to your endurance for this competition only, on top of what you had earned.");
            else
                lines.Add("You competed: your performance counted on top of what you had earned.");
            if (!thrown)
            {
                // Through rules 2 a played competition reads only the storyline; preparation and the
                // week's events count in a simulated one, and in every way in from rules 3.
                bool earned = state.competitionRulesVersion >= 3 || simulated;
                bool story = state.competitionRulesVersion >= 3 || !simulated;
                if (earned && state.playerStudyBonus > 0) lines.Add("Your preparation counted.");
                if (earned && state.phaseEventCompBonus > 0) lines.Add("The week's events gave you an edge.");
                else if (earned && state.phaseEventCompBonus < 0) lines.Add("The week's events cost you.");
                double storyline = Storylines.CompetitionBonus(state);
                if (story && storyline > 0) lines.Add("The story so far gave you a push.");
                else if (story && storyline < 0) lines.Add("The story so far weighed on you.");
            }
            // The reference's clutch: a nominee has half their competition statistic more to give,
            // thrown or not; the endurance run does not read it. A Have-Not is tired in the veto.
            if (!endurance && state.nominees != null && state.nominees.Contains(state.playerId))
                lines.Add("On the block, you had something extra to fight for.");
            if (HaveNots.Penalty(state, state.playerId) > 0) lines.Add("A Have-Not competes tired, and you were one.");
            lines.Add(Close);
            return string.Join(" ", lines);
        }

        /// <summary>What letting the day decide does for the player: the briefing's Simulate card.</summary>
        public static string SimulateDescription(int rulesVersion) => rulesVersion >= 3
            ? "Let the day decide: what you have earned still counts, with no performance bonus."
            : "Let the day decide: your preparation and the week's events still count, with no performance bonus. Preparation carries over to later weeks.";

        /// <summary>What a throw does for the player: the briefing's Throw card.</summary>
        public static string ThrowDescription(int rulesVersion) => rulesVersion >= CompetitionRules.Widened
            ? "You compete to lose: every bonus given up and only part of your score counts, so you will lose about nine times in ten. "
                + "It lowers only your own score: if everyone else has a worse day, you win anyway."
            : "You compete to lose: no performance bonus. Who you are still counts, so you may still win.";

        /// <summary>The full rules' line on what performance is worth. <paramref name="range"/> is the game's own measure ("0–3").</summary>
        public static string PerformanceRule(string range, bool finalPart1) => finalPart1
            ? "Performance adds " + range + " endurance for this competition only. Who you are and how the day goes decide the rest; full marks do not guarantee a win."
            : "Performance adds a " + range + " point bonus. Who you are and how the day goes decide the rest; full marks do not guarantee a win.";

        /// <summary>
        /// The full rules' line on what the three ways in keep (rules 3 on): the player's own banked
        /// preparation, as the diary counts it. A throw is the exception - every bonus given up -
        /// and the Throw card says so; this sentence does not claim it.
        /// </summary>
        public static string EarnedRule(int preparation) =>
            "Play it, let the day decide or take the accessible alternative: what you have earned counts the same - your preparation"
            + (preparation > 0 ? " (" + preparation + " of 5)" : "")
            + ", the week's events and the story so far. Playing, or the accessible alternative, adds your performance on top; your preparation carries over to later weeks.";
    }
}
