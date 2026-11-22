using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;

namespace Gamesim.Presentation
{
    /// <summary>
    /// The trivia competition's playing, as data (competition rules 6): which question is shown, what an answer
    /// does, and what the result card reads. The questions are <see cref="CompetitionTrivia"/>'s; this is the
    /// round around them.
    ///
    /// <para>One question at a time, in the quiz's order. An answer is taken once: it is shown right or wrong
    /// at once (in practice the right choice is named when the answer was wrong) for <see cref="RevealSeconds"/>, and then
    /// the next question comes up. While an answer is shown another answer is refused. The round is over when
    /// the last answer has been shown, or when the clock runs out; a question never answered is wrong.</para>
    ///
    /// <para>The player's result is <see cref="CompetitionTrivia.Performance"/>: the share of right answers.
    /// No engine type and no random draw: the Unity-free suite plays it as the game does, and the season's
    /// generator is never touched.</para>
    /// </summary>
    public sealed class TriviaRound
    {
        /// <summary>How long an answer is shown right or wrong before the next question, in seconds of the attempt's clock.</summary>
        public const double RevealSeconds = 1.5;

        /// <summary>What an answer did.</summary>
        public enum Outcome { Refused, Right, Wrong }

        private readonly int[] chosen;
        private double revealLeft;

        /// <param name="quiz">The questions, in the order they are asked. None makes a round that is over at once.</param>
        /// <param name="namesAnswers">Whether a wrong answer names the right one. Practice does; a ranked quiz says
        /// only right or wrong, so leaving and coming back to the same quiz does not hand over its answers.</param>
        public TriviaRound(IReadOnlyList<TriviaQuestion> quiz, bool namesAnswers = true)
        {
            Questions = (quiz ?? Array.Empty<TriviaQuestion>()).Where(q => q != null).ToList().AsReadOnly();
            chosen = Enumerable.Repeat(-1, Questions.Count).ToArray();
            Revealed = -1;
            NamesAnswers = namesAnswers;
        }

        public IReadOnlyList<TriviaQuestion> Questions { get; }

        /// <summary>Whether a wrong answer names (and the board lights) the right one: practice only.</summary>
        public bool NamesAnswers { get; }

        /// <summary>Whether every question has an answer, even while the last one is still being shown.</summary>
        public bool AllAnswered => Answered >= Count;

        /// <summary>The finish plate's headline: "QUIZ OVER" once every question has an answer, "TIME" when the clock ended it first.</summary>
        public string FinishWord => AllAnswered ? "QUIZ OVER" : "TIME";

        /// <summary>The briefing's count of a quiz: "5 questions", "1 question" - the board's own count, never the rules' target.</summary>
        public static string BriefingCount(IReadOnlyList<TriviaQuestion> quiz)
        {
            int count = new TriviaRound(quiz).Count;
            return count + (count == 1 ? " question" : " questions");
        }

        /// <summary>The questions the round asks.</summary>
        public int Count => Questions.Count;

        /// <summary>The question on screen, 0-based; equal to <see cref="Count"/> once every answer has been shown.</summary>
        public int Index { get; private set; }

        /// <summary>The question on screen, or null once the round is over.</summary>
        public TriviaQuestion Current => Index < Count ? Questions[Index] : null;

        /// <summary>The choice taken for each question, in the quiz's order; -1 where none was.</summary>
        public IReadOnlyList<int> Chosen => chosen;

        /// <summary>The questions answered so far.</summary>
        public int Answered => chosen.Count(choice => choice >= 0);

        /// <summary>The right answers so far.</summary>
        public int Right => CompetitionTrivia.RightAnswers(Questions, chosen);

        /// <summary>Whether the last answer is being shown and the next question waits.</summary>
        public bool Revealing => Revealed >= 0;

        /// <summary>The choice being shown right or wrong on the current question, or -1.</summary>
        public int Revealed { get; private set; }

        /// <summary>Whether every answer has been taken and shown.</summary>
        public bool Done => Index >= Count;

        /// <summary>The share of right answers, 0 to 1: the player's result.</summary>
        public double Performance => CompetitionTrivia.Performance(Questions, chosen);

        /// <summary>What the player is told after the last thing they did.</summary>
        public string Feedback { get; private set; } = "Choose an answer";

        /// <summary>
        /// Takes <paramref name="choice"/> (an index into the current question's choices) as the answer. Refused
        /// with no question up, while an answer is being shown, after the round has been stopped, or for a choice
        /// the question does not have; a refusal changes nothing.
        /// </summary>
        public Outcome Answer(int choice)
        {
            var question = Current;
            if (question == null || Revealing || Stopped || choice < 0 || choice >= question.Choices.Count) return Outcome.Refused;
            chosen[Index] = choice;
            Revealed = choice;
            revealLeft = RevealSeconds;
            bool right = choice == question.Answer;
            Feedback = !NamesAnswers ? (right ? "Right" : "Wrong")
                : right ? "Right: " + question.AnswerText : "Wrong: the answer was " + question.AnswerText;
            return right ? Outcome.Right : Outcome.Wrong;
        }

        /// <summary>Runs the answer's showing down by <paramref name="delta"/> seconds; when it is over the next question comes up.</summary>
        public void Tick(double delta)
        {
            if (!Revealing || Stopped || delta <= 0 || double.IsNaN(delta) || double.IsInfinity(delta)) return;
            revealLeft -= delta;
            if (revealLeft > 1e-9) return;
            revealLeft = 0;
            Revealed = -1;
            Index++;
            if (!Done) Feedback = "Choose an answer";
        }

        /// <summary>Whether the round was stopped by the clock: no answer is taken after it.</summary>
        public bool Stopped { get; private set; }

        /// <summary>The clock ran out: what was not answered stays wrong.</summary>
        public void Stop() => Stopped = true;

        /// <summary>The count over the question, "Question 2 of 5"; the last question's number once the round is over.</summary>
        public string CountLine => Count == 0 ? "No questions" : "Question " + Math.Min(Index + 1, Count) + " of " + Count;

        /// <summary>The right answers in words, "3 of 5 right": the finish plate's and the result card's count.</summary>
        public string ResultLine => Right + " of " + Count + " right";

        /// <summary>
        /// A practice round: three questions about the house's rules, never about the season, so practicing
        /// teaches the board and gives nothing of the ranked quiz away.
        /// </summary>
        public static IReadOnlyList<TriviaQuestion> PracticeQuiz { get; } = Array.AsReadOnly(new[]
        {
            new TriviaQuestion("practice-nominees", 0, "How many houseguests does the Head of Household nominate for eviction?",
                Array.AsReadOnly(new[] { "One", "Two", "Three" }), 1),
            new TriviaQuestion("practice-veto", 0, "What can the Power of Veto do?",
                Array.AsReadOnly(new[] { "Evict a houseguest at once", "Win Head of Household", "Save a nominee from the block" }), 2),
            new TriviaQuestion("practice-tie", 0, "Who breaks a tie in the eviction vote?",
                Array.AsReadOnly(new[] { "The Head of Household", "The Power of Veto holder", "The nominees" }), 0),
        });
    }
}
