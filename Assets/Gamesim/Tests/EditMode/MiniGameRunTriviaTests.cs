using System;
using System.Collections.Generic;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The trivia as an attempt (competition rules 6, the screen slice): a trivia competition's definition plays the
    /// trivia and nothing else; the attempt asks the round's questions on the clock, ends when the last answer has been
    /// shown or the time is up, and carries the rules' share of right answers to the standings; the attempt line on the
    /// result card gives the count. No engine module: the dotnet subset runs all but the attempt line's case, which
    /// reads the board's component and runs in the editor and under the Mono harness.
    /// </summary>
    public sealed class MiniGameRunTriviaTests
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
        public void TheTriviasDefinitionPlaysTheTriviaUnderRulesSixAndNothingElse()
        {
            Assert.That(CompetitionMiniGames.TriviaRules, Is.EqualTo(CompetitionRules.Trivia));
            Assert.That(CompetitionMiniGames.CurrentRules, Is.EqualTo(CompetitionMiniGames.TriviaRules), "A bound: a rules-6 attempt can be played.");
            Assert.That(CompetitionMiniGames.For(CompetitionDefinitions.Trivia, "Mental"), Is.EqualTo(CompetitionMiniGames.Kind.Trivia));
            Assert.That(CompetitionMiniGames.For(CompetitionDefinitions.HouseMemory, "Mental"), Is.EqualTo(CompetitionMiniGames.Kind.Memory), "A memory board stays a memory board.");
            Assert.That(CompetitionMiniGames.For("Mental"), Is.EqualTo(CompetitionMiniGames.Kind.Memory), "Never by its kind.");
            Assert.That(CompetitionMiniGames.CategoryOf(CompetitionMiniGames.Kind.Trivia), Is.EqualTo("Mental"));
            Assert.That(CompetitionMiniGames.EnterCaption(CompetitionMiniGames.Kind.Trivia), Is.EqualTo("Enter trivia challenge"), "Captions are a contract.");
            Assert.Throws<ArgumentOutOfRangeException>(() => new MiniGameRun(CompetitionMiniGames.Kind.Trivia, 1, CompetitionMiniGames.TowerRules));
            Assert.Throws<ArgumentException>(() => new MiniGameRun(CompetitionMiniGames.Kind.Trivia, 1, CompetitionMiniGames.TriviaRules, CompetitionDefinitions.HouseMemory));
            Assert.Throws<ArgumentException>(() => new MiniGameRun(CompetitionMiniGames.Kind.Memory, 1, CompetitionMiniGames.TriviaRules, CompetitionDefinitions.Trivia));
            var run = Run(Three());
            Assert.That(run.Definition, Is.SameAs(CompetitionDefinitions.Trivia));
            Assert.That(run.TimeLimit, Is.EqualTo(60));
            Assert.That(new MiniGameRun(CompetitionMiniGames.Kind.Trivia, 1, CompetitionMiniGames.TriviaRules).Definition, Is.SameAs(CompetitionDefinitions.Trivia));
        }

        [Test]
        public void AnAttemptAsksEachQuestionAndEndsWhenTheLastAnswerHasBeenShown()
        {
            var quiz = Three();
            var run = Run(quiz);
            Assert.That(run.Trivia.Current, Is.SameAs(quiz[0]));
            Assert.That(run.AnswerTrivia(1), Is.True);
            Assert.That(run.Feedback, Is.EqualTo("Right"), "A ranked quiz says right or wrong (TriviaRankedLeaveTests).");
            Assert.That(run.AnswerTrivia(0), Is.False, "One answer a question.");
            run.Tick(TriviaRound.RevealSeconds + .05);
            Assert.That(run.Trivia.Current, Is.SameAs(quiz[1]));
            Assert.That(run.AnswerTrivia(2), Is.True);
            Assert.That(run.Feedback, Is.EqualTo("Wrong"));
            run.Tick(TriviaRound.RevealSeconds + .05);
            Assert.That(run.AnswerTrivia(2), Is.True);
            Assert.That(run.Finished, Is.False, "The last answer is shown before the end.");
            run.Tick(TriviaRound.RevealSeconds + .05);
            Assert.That(run.Finished, Is.True);
            Assert.That(run.Remaining, Is.GreaterThan(50), "Over at once, not at the whistle.");
            Assert.That(run.Performance, Is.EqualTo(CompetitionTrivia.Performance(quiz, new[] { 1, 2, 2 })), "The rules' share is what the standings get.");
            Assert.That(run.Performance, Is.EqualTo(2 / 3.0));
            Assert.That(run.AnswerTrivia(0), Is.False);
        }

        [Test]
        public void TheClockEndsTheAttemptAndWhatWasNotAnsweredIsWrong()
        {
            var run = Run(Three());
            run.AnswerTrivia(1);
            run.Tick(70);
            Assert.That(run.Finished, Is.True);
            Assert.That(run.Trivia.Answered, Is.EqualTo(1));
            Assert.That(run.Performance, Is.EqualTo(1 / 3.0));
            Assert.That(run.Feedback, Is.EqualTo("Time: 1 of 3 answered"));
            Assert.That(run.AnswerTrivia(0), Is.False);

            var none = Run(null);
            none.Tick(.1);
            Assert.That((none.Finished, none.Performance), Is.EqualTo((true, 0.0)), "A quiz of nothing is over at once and worth nothing.");
        }

#if UNITY_5_3_OR_NEWER
        // The board is a component: this case is the editor's (and the Mono harness's), not the dotnet subset's.
        [Test]
        public void TheResultCardsAttemptLineGivesTheCount()
        {
            var quiz = Three();
            var run = Run(quiz);
            Assert.That(CompetitionGameScreen.AttemptLine(run), Is.Null, "Nothing before the end.");
            foreach (var question in quiz) { run.AnswerTrivia(question.Answer); run.Tick(TriviaRound.RevealSeconds + .05); }
            Assert.That(CompetitionGameScreen.AttemptLine(run), Is.EqualTo("Your attempt  ·  3 of 3 right  ·  performance 100%"));
        }
#endif
    }
}
