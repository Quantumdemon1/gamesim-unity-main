using Gamesim.Simulation;

namespace Gamesim.Presentation
{
    /// <summary>
    /// The player's own decision the episode screen holds now, if any: their nominations as Head of
    /// Household, the veto meeting, the eviction ballot, or the final Head of Household's choice
    /// (UI-UX plan Q2; menu review change 7). While one waits, the rail's Episode button opens the
    /// screen where the player stands instead of walking them to the nomination room; free time,
    /// and every beat with nothing of theirs to decide, still walks. Read only: nothing here is
    /// saved, and nothing draws from the season's stream.
    /// </summary>
    public static class WaitingDecision
    {
        public enum Kind { None, Nominations, VetoMeeting, Ballot, FinalChoice }

        /// <summary>
        /// The decision waiting for the player on <paramref name="s"/>, by the episode screen's own rule
        /// for drawing each: <see cref="NominationSteps.Picking"/>, the meeting's holder (or the Head of
        /// Household naming a replacement for a houseguest's save), <see cref="DecisionPick.BallotLive"/>,
        /// and the final Head of Household with two finalists to choose between. None under a
        /// competition the director is running (<paramref name="competitionRunning"/>) and under a
        /// private reflection, which the screen draws before anything else; None for a player who is
        /// no longer in the house.
        /// </summary>
        public static Kind For(EpisodeState s, bool competitionRunning = false)
        {
            if (s == null || competitionRunning || s.pendingDiary != null) return Kind.None;
            if (s.Find(s.playerId)?.status != ContestantStatus.Active) return Kind.None;
            if (NominationSteps.Picking(s)) return Kind.Nominations;
            if (s.phase == EpisodePhase.VetoMeeting && !s.vetoResolved
                && (s.vetoHolderId == s.playerId || s.hohId == s.playerId && EpisodeEngine.NpcVetoSave(s) != null)) return Kind.VetoMeeting;
            if (DecisionPick.BallotLive(s)) return Kind.Ballot;
            if (s.phase == EpisodePhase.FinalEviction && s.hohId == s.playerId && FinalistRead.Others(s).Count == 2) return Kind.FinalChoice;
            return Kind.None;
        }

        /// <summary>Whether a decision of the player's own waits on <paramref name="s"/> (<see cref="For"/>).</summary>
        public static bool Waits(EpisodeState s, bool competitionRunning = false) => For(s, competitionRunning) != Kind.None;
    }
}
