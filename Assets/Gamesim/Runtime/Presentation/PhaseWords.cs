using Gamesim.Simulation;

namespace Gamesim.Presentation
{
    /// <summary>
    /// The one table of a phase's words (UI-UX plan D15, the menu review's change 7): the word the
    /// week chip has room for, the final Head of Household's part, and the news line the status line
    /// says while the house is in a phase. The week lives on the chip ("WEEK 4 | Nominations"), so the
    /// status line no longer says it again ("Week 4 · Nominations"): it says what is happening now,
    /// "Nominations: the Head of Household names two houseguests". Unity-free, so the subset pins
    /// every line.
    ///
    /// <para>Words only: the saved event log keeps the engine's own text ("Week 4 · Nomination"), and
    /// nothing here reads or writes the season.</para>
    /// </summary>
    public static class PhaseWords
    {
        /// <summary>The longest word the week chip's phase slot is given.</summary>
        public const int MaxShortLength = 14;

        /// <summary>
        /// The longest news line. The status line adds "  ·  3 actions remaining" in free time and the
        /// campaign, and the whole line still fits the caption at the largest text size.
        /// </summary>
        public const int MaxNewsLength = 60;

        /// <summary>A phase in the one or two words the week chip has room for.</summary>
        public static string Short(EpisodePhase phase)
        {
            switch (phase)
            {
                case EpisodePhase.Social: return "Free time";
                case EpisodePhase.HoH: return "HoH comp";
                case EpisodePhase.Nomination: return "Nominations";
                case EpisodePhase.VetoSelection: return "Veto draw";
                case EpisodePhase.Veto: return "Veto comp";
                case EpisodePhase.VetoMeeting: return "Veto meeting";
                case EpisodePhase.Campaign: return "Campaign";
                case EpisodePhase.Eviction: return "Eviction";
                case EpisodePhase.FinalHoHPart1:
                case EpisodePhase.FinalHoHPart2:
                case EpisodePhase.FinalHoHPart3: return "Final HoH";
                case EpisodePhase.FinalEviction: return "Final eviction";
                case EpisodePhase.Jury:
                case EpisodePhase.JuryQuestioning:
                case EpisodePhase.FinalSpeeches: return "Jury";
                default: return "Finale";
            }
        }

        /// <summary>"Part 1 of 3", or null outside the final Head of Household.</summary>
        public static string PartLabel(EpisodePhase phase) =>
            phase == EpisodePhase.FinalHoHPart1 ? "Part 1 of 3"
            : phase == EpisodePhase.FinalHoHPart2 ? "Part 2 of 3"
            : phase == EpisodePhase.FinalHoHPart3 ? "Part 3 of 3" : null;

        /// <summary>
        /// What the house is doing in a phase, as the status line says it: the chip's word, then what
        /// it means. No week: the chip carries it. The finale's beats keep the words they always had
        /// ("Jury questioning", "Final speeches"); they never carried a week.
        /// </summary>
        public static string News(EpisodePhase phase)
        {
            switch (phase)
            {
                case EpisodePhase.Social: return "Free time: talk, make deals and plan your next move";
                case EpisodePhase.HoH: return "HoH comp: the house plays for Head of Household";
                case EpisodePhase.Nomination: return "Nominations: the Head of Household names two houseguests";
                case EpisodePhase.VetoSelection: return "Veto draw: the players for the Power of Veto are drawn";
                case EpisodePhase.Veto: return "Veto comp: the players compete for the Power of Veto";
                case EpisodePhase.VetoMeeting: return "Veto meeting: the holder decides whether to use the veto";
                case EpisodePhase.Campaign: return "Campaign: the nominees make their case to stay";
                case EpisodePhase.Eviction: return "Eviction: the house votes one houseguest out";
                case EpisodePhase.FinalHoHPart1: return "Final HoH, " + PartLabel(phase) + ": all three compete";
                case EpisodePhase.FinalHoHPart2: return "Final HoH, " + PartLabel(phase) + ": the two who lost Part 1 compete";
                case EpisodePhase.FinalHoHPart3: return "Final HoH, " + PartLabel(phase) + ": the two winners play for HoH";
                case EpisodePhase.FinalEviction: return "Final eviction: the HoH evicts one of the other two";
                case EpisodePhase.JuryQuestioning: return "Jury questioning";
                case EpisodePhase.FinalSpeeches: return "Final speeches";
                case EpisodePhase.Jury: return "The jury votes";
                case EpisodePhase.Finished: return "The season is over";
                default: return Short(phase);
            }
        }
    }
}
