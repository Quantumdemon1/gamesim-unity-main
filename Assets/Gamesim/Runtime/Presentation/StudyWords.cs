using Gamesim.Simulation;

namespace Gamesim.Presentation
{
    /// <summary>
    /// What the diary room says about study preparation, in the player's words: where it counts, in the record's row and
    /// at length, and the record's rules line. Each reads the season: its competition rules, and B9's tuning
    /// (<see cref="TuningRules"/>), under which a weekly competition uses preparation up and the limit is the season's own.
    /// A season before B9's tuning reads every sentence it always did. Unity-free, so the Unity-free subset pins the words
    /// beside the rules they describe; the director's <c>StudyUse</c> and <c>StudyUseShort</c> read them from here.
    /// </summary>
    public static class StudyWords
    {
        /// <summary>
        /// Where study preparation counts, as the season's rules have it. From competition rules 3 the engine adds it on
        /// every player entry route (EpisodeEngine.CommonCompetitionBonus): the weekly competitions however they are played,
        /// and all three parts of the final Head of Household. Before rules 3 only the weekly simulated option read it -
        /// said in the player's words, as the button that chooses it is: "the weekly simulated HoH/Veto option" was the
        /// engine's (UI-UX-PASS-PLAN D0, the play sweep's row 37). Under B9's tuning the next weekly Head of Household or
        /// veto the player enters uses it up, and a throw keeps it (EpisodeEngine.SpendPreparation).
        /// </summary>
        public static string Use(EpisodeState state) =>
            TuningRules.At(state, TuningRules.Bands)
                ? "used up by the next weekly HoH or veto competition you enter, unless you throw it; the final HoH's three parts all count it"
                : state != null && state.competitionRulesVersion >= 3
                ? "used in every competition you enter, however you play it, the final HoH's three parts included"
                : "used only when you choose to simulate a weekly HoH or veto competition, not when you play one yourself or in the final HoH";

        /// <summary>
        /// The same, in the record row's few words. Under B9's tuning, by what is left to use it in: the next weekly
        /// competition; in the final week the final Head of Household's three parts, since no weekly one follows; and once
        /// those are over, or for a player out of the house, nothing.
        /// </summary>
        public static string UseShort(EpisodeState state) =>
            TuningRules.At(state, TuningRules.Bands)
                ? (NoneLeft(state) ? "No competition is left to use it in."
                    : TuningRules.FinalWeek(state) ? "Counts in the final HoH's three parts."
                    : "Used up by your next weekly competition.")
                : state != null && state.competitionRulesVersion >= 3
                ? "Used in every competition you enter."
                : "Used only when you simulate a weekly competition.";

        /// <summary>
        /// Under B9's tuning, the study section's word once preparation is at the season's limit and no study is offered:
        /// what it waits for. In the final week that is the final Head of Household, all three parts of which count it.
        /// </summary>
        public static string FullLine(EpisodeState state) => TuningRules.FinalWeek(state)
            ? "Your preparation is full. All three parts of the final Head of Household count it."
            : "Your preparation is full. The next weekly HoH or veto competition you enter uses it up, unless you throw it.";

        /// <summary>Under B9's tuning, the study section's aside beside the two ways to study: what uses preparation, this week.</summary>
        public static string StudyAside(EpisodeState state) => TuningRules.FinalWeek(state)
            ? "Preparation counts in all three parts of the final HoH; a study changes it only when you confirm."
            : "Preparation is used up by the next weekly HoH or veto you enter, unless you throw it; a study changes it only when you confirm.";

        /// <summary>
        /// The record's rules line on study preparation, behind the disclosure: before B9's tuning exactly as it always read
        /// (between 0 and 5, changed only by a study); under it (<see cref="TuningRules.Bands"/>) the season's own study
        /// limit, one, beside what uses it up.
        /// </summary>
        public static string RulesLine(EpisodeState state) => !TuningRules.At(state, TuningRules.Bands)
            ? "Study preparation: saved between weeks; " + Use(state) + ". It stays between 0 and 5 and changes only when you confirm a study approach."
            : "Study preparation: saved between weeks; " + Use(state) + ". A confirmed study raises it, up to " + TuningRules.StudyLimit(state) + ".";

        /// <summary>No competition is left that would count preparation: the player is out of the house, or the final Head of Household is over.</summary>
        private static bool NoneLeft(EpisodeState s)
        {
            var you = s?.Find(s.playerId);
            if (you == null || you.status != ContestantStatus.Active) return true;
            return s.phase == EpisodePhase.FinalEviction || s.phase == EpisodePhase.JuryQuestioning || s.phase == EpisodePhase.FinalSpeeches
                || s.phase == EpisodePhase.Jury || s.phase == EpisodePhase.Finished;
        }
    }
}
