using System;
using System.Collections.Generic;
using Gamesim.Simulation;

namespace Gamesim.Presentation
{
    /// <summary>
    /// The trivia competition (competition rules 6): questions about the season's own events. The round is
    /// <see cref="TriviaRound"/>'s; this is the attempt around it - the clock, the finish when the last answer
    /// has been shown or the time is up, and the result, which is the share of right answers
    /// (<see cref="CompetitionTrivia.Performance"/>). Nothing is drawn from any generator.
    /// </summary>
    public sealed partial class MiniGameRun
    {
        /// <summary>The round being played; null for every other game.</summary>
        public TriviaRound Trivia { get; private set; }

        private double TriviaPerformance => Trivia?.Performance ?? 0;

        private static void CheckTrivia(CompetitionMiniGames.Kind kind, int rulesVersion, CompetitionDefinition definition)
        {
            bool trivia = kind == CompetitionMiniGames.Kind.Trivia;
            if (trivia && rulesVersion < CompetitionMiniGames.TriviaRules)
                throw new ArgumentOutOfRangeException(nameof(rulesVersion), "The trivia is rules 6's.");
            if (trivia && definition != null && definition.Pattern != CompetitionPattern.Trivia)
                throw new ArgumentException("The trivia plays the trivia's definition only.", nameof(definition));
            if (!trivia && definition != null && definition.Pattern == CompetitionPattern.Trivia)
                throw new ArgumentException("The trivia's definition plays the trivia only.", nameof(definition));
        }

        private void DealTrivia(IReadOnlyList<TriviaQuestion> quiz)
        {
            // Only practice's house-rules quiz names a missed answer; the ranked quiz is the same after a cancel or
            // a reload, so it says right or wrong and nothing more.
            Trivia = new TriviaRound(quiz, namesAnswers: ReferenceEquals(quiz, TriviaRound.PracticeQuiz));
            Feedback = Trivia.Feedback;
        }

        /// <summary>
        /// Leaving a ranked quiz after its first answer counts it as it stands: the run finishes here (what was not
        /// answered is wrong) and true says the caller commits it. Without this, leaving and coming back to the same
        /// quiz would let a player try each answer in turn. False - leave freely - for practice, for any other game,
        /// and before the first answer.
        /// </summary>
        public bool LockInOnLeave(bool practice)
        {
            if (practice || Kind != CompetitionMiniGames.Kind.Trivia || Trivia == null || Trivia.NamesAnswers
                || Trivia.Answered == 0) return false;
            Finish();
            return true;
        }

        /// <summary>
        /// An answer to the question on screen: an index into its choices. Refused (false) when the game is not
        /// the trivia, is over, an answer is still being shown, or the choice does not exist; a refusal changes nothing.
        /// </summary>
        public bool AnswerTrivia(int choice)
        {
            if (Finished || Kind != CompetitionMiniGames.Kind.Trivia || Trivia == null) return false;
            var outcome = Trivia.Answer(choice);
            if (outcome == TriviaRound.Outcome.Refused) return false;
            Feedback = Trivia.Feedback;
            return true;
        }

        private void StepTrivia(double delta)
        {
            if (Trivia == null) { Finish(); return; }
            Trivia.Tick(delta);
            if (!Trivia.Revealing) Feedback = Trivia.Feedback;
            // The last answer has been shown: the quiz is over now, not at the whistle.
            if (Trivia.Done) Finish();
        }

        private void FinishTrivia()
        {
            Trivia?.Stop();
            Score = TriviaPerformance * 10;
            if (Trivia != null && !Trivia.AllAnswered)
                Feedback = "Time: " + Trivia.Answered + " of " + Trivia.Count + " answered";
        }
    }
}
