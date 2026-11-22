using System;
using System.Collections.Generic;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The ranked trivia cannot be farmed (the screen slice's review): a ranked answer says right or wrong and never
    /// names the right one, and leaving a ranked quiz after its first answer counts it as it stands, so leaving and
    /// coming back to the same quiz hands over nothing. Practice still names its answers. Also the review's minors:
    /// the clock ending while the last answer is shown is a finished quiz, and the briefing counts the board's own
    /// questions. Unity-free, so the subset runs it.
    /// </summary>
    public sealed class TriviaRankedLeaveTests
    {
        private static IReadOnlyList<TriviaQuestion> Three() => new[]
        {
            new TriviaQuestion("hoh-1", 1, "Who won Head of Household in week 1?", Array.AsReadOnly(new[] { "Avery", "Blair", "Casey" }), 1),
            new TriviaQuestion("evicted-2", 2, "Who was evicted in week 2?", Array.AsReadOnly(new[] { "Dana", "Ellis", "Flynn", "Gray" }), 0),
            new TriviaQuestion("veto-3", 3, "Who won the Power of Veto in week 3?", Array.AsReadOnly(new[] { "Harper", "Indy", "Jules" }), 2),
        };

        private static MiniGameRun Run(IReadOnlyList<TriviaQuestion> quiz) =>
            new MiniGameRun(CompetitionMiniGames.Kind.Trivia, 5, CompetitionMiniGames.TriviaRules, CompetitionDefinitions.Trivia, quiz: quiz);

        [Test]
        public void ARankedAnswerSaysRightOrWrongAndNeverNamesTheRightOne()
        {
            var ranked = Run(Three());
            Assert.That(ranked.Trivia.NamesAnswers, Is.False, "The season's quiz is ranked.");
            Assert.That(ranked.AnswerTrivia(1), Is.True);
            Assert.That(ranked.Feedback, Is.EqualTo("Right"));
            ranked.Tick(TriviaRound.RevealSeconds + .05);
            Assert.That(ranked.AnswerTrivia(2), Is.True);
            Assert.That(ranked.Feedback, Is.EqualTo("Wrong"), "A miss does not give the answer away.");
            Assert.That(ranked.Feedback, Does.Not.Contain("Dana"));

            var practice = Run(TriviaRound.PracticeQuiz);
            Assert.That(practice.Trivia.NamesAnswers, Is.True, "Practice asks the house's rules and may name them.");
            var first = TriviaRound.PracticeQuiz[0];
            Assert.That(practice.AnswerTrivia((first.Answer + 1) % first.Choices.Count), Is.True);
            Assert.That(practice.Feedback, Is.EqualTo("Wrong: the answer was " + first.AnswerText));

            var model = new TriviaRound(Three(), namesAnswers: false);
            Assert.That(model.Answer(0), Is.EqualTo(TriviaRound.Outcome.Wrong));
            Assert.That(model.Feedback, Is.EqualTo("Wrong"));
        }

        [Test]
        public void LeavingARankedQuizAfterAnAnswerCountsItAsItStands()
        {
            var run = Run(Three());
            Assert.That(run.LockInOnLeave(false), Is.False, "Before the first answer the player leaves freely.");
            Assert.That(run.Finished, Is.False);
            Assert.That(run.AnswerTrivia(1), Is.True);
            Assert.That(run.LockInOnLeave(true), Is.False, "A practice attempt is never committed.");
            Assert.That(run.Finished, Is.False);

            Assert.That(run.LockInOnLeave(false), Is.True, "One answer in: leaving commits it.");
            Assert.That(run.Finished, Is.True);
            Assert.That(run.Performance, Is.EqualTo(1 / 3.0), "What was not answered is wrong.");
            Assert.That(run.AnswerTrivia(0), Is.False, "Nothing more is taken.");
            Assert.That(run.LockInOnLeave(false), Is.True, "A ranked finish waiting to commit commits on the leave too.");

            var practice = Run(TriviaRound.PracticeQuiz);
            practice.AnswerTrivia(0);
            Assert.That(practice.LockInOnLeave(false), Is.False, "The house-rules quiz is practice's, whoever asks.");
            Assert.That(practice.Finished, Is.False);

            var memory = new MiniGameRun(CompetitionMiniGames.Kind.Memory, 5);
            Assert.That(memory.LockInOnLeave(false), Is.False, "Every other game cancels as before.");
            Assert.That(memory.Finished, Is.False);
        }

        [Test]
        public void TheClockEndingWhileTheLastAnswerIsShownIsAFinishedQuiz()
        {
            var quiz = Three();
            var run = Run(quiz);
            run.AnswerTrivia(1); run.Tick(TriviaRound.RevealSeconds + .05);
            run.AnswerTrivia(0); run.Tick(TriviaRound.RevealSeconds + .05);
            run.Tick(run.Remaining - 1);
            Assert.That(run.AnswerTrivia(2), Is.True);
            run.Tick(1.2);
            Assert.That(run.Finished, Is.True, "The clock ran out during the last answer's showing.");
            Assert.That((run.Trivia.Done, run.Trivia.AllAnswered), Is.EqualTo((false, true)));
            Assert.That(run.Trivia.FinishWord, Is.EqualTo("QUIZ OVER"));
            Assert.That(run.Feedback, Does.Not.StartWith("Time"));
            Assert.That(run.Performance, Is.EqualTo(1.0));

            var late = Run(quiz);
            late.AnswerTrivia(1);
            late.Tick(70);
            Assert.That((late.Finished, late.Trivia.FinishWord, late.Feedback), Is.EqualTo((true, "TIME", "Time: 1 of 3 answered")));
        }

        [Test]
        public void TheBriefingCountsTheQuestionsTheBoardWillAsk()
        {
            var quiz = Three();
            Assert.That(TriviaRound.BriefingCount(quiz), Is.EqualTo("3 questions"));
            Assert.That(TriviaRound.BriefingCount(quiz), Does.StartWith(new TriviaRound(quiz).Count + " "), "The board's own count.");
            Assert.That(TriviaRound.BriefingCount(new[] { quiz[0] }), Is.EqualTo("1 question"));
            Assert.That(TriviaRound.BriefingCount(null), Is.EqualTo("0 questions"));
        }
    }
}
