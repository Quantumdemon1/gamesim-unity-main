using System.Collections.Generic;
using Gamesim.Presentation;
using Gamesim.Simulation;

namespace Gamesim.Episode
{
    /// <summary>
    /// The trivia competition in the house (competition rules 6). The ranked game asks the competition's own
    /// quiz (<see cref="CompetitionTrivia.Quiz(EpisodeState)"/>), the same after a cancel or a reload; practice
    /// asks about the house's rules instead (<see cref="TriviaRound.PracticeQuiz"/>), so practicing gives nothing
    /// of the ranked quiz away. A ranked answer is shown right or wrong without naming the right one, and leaving a
    /// ranked quiz after its first answer counts it as it stands, so the same quiz cannot be retried answer by
    /// answer. It is played at the memory consoles the arena already stands for a mental game, and watched or
    /// simulated it is staged as their quiz show. Only a season at competition rules 6 deals it.
    /// </summary>
    public sealed partial class EpisodeDirector
    {
        /// <summary>The briefing's rule for a ranked quiz that is left part-way.</summary>
        internal const string TriviaLeaveRule = "Leave a ranked quiz after your first answer and it counts as it stands: questions not answered are wrong.";

        /// <summary>The ranked card's line under its caption for the trivia.</summary>
        internal const string TriviaRankedDescription = "Ranked. Once you answer, leaving counts the quiz as it stands; pause stops the clock.";

        /// <summary>The run a leave has already tried to commit: a refused commit then lets the next leave go back.</summary>
        private MiniGameRun triviaLockedIn;

        /// <summary>The questions an attempt asks: null for every game but the trivia.</summary>
        private static IReadOnlyList<TriviaQuestion> TriviaQuiz(EpisodeState state, bool practice) =>
            CompetitionDefinitions.For(state)?.Pattern != CompetitionPattern.Trivia ? null
            : practice ? TriviaRound.PracticeQuiz : CompetitionTrivia.Quiz(state);

        /// <summary>The briefing's first fact for the trivia: how many questions the board will ask, and what they are about.</summary>
        private static EpisodeHud.BriefingFact TriviaBriefingFact(EpisodeState state) =>
            new EpisodeHud.BriefingFact(UiTheme.Icon("chat"), TriviaRound.BriefingCount(CompetitionTrivia.Quiz(state)), "About your season");

        /// <summary>
        /// The player leaves the competition screen (its Cancel, or Escape, Start or B). A ranked quiz with an answer
        /// in it is committed as it stands (<see cref="MiniGameRun.LockInOnLeave"/>); everything else is cancelled
        /// as before.
        /// </summary>
        private void LeaveChallenge()
        {
            var run = challengeRun;
            if (run != null && !ReferenceEquals(run, triviaLockedIn) && !challengeCommitting && run.LockInOnLeave(challengePractice))
            {
                triviaLockedIn = run;
                challengeResultShown = true; challengeFinishHold = 0f;
                CommitMiniGame();
                return;
            }
            CancelChallenge();
        }
    }
}
