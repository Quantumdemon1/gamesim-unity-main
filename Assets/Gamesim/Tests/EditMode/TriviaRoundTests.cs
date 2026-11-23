using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The trivia's playing screen as a model (competition rules 6, the screen slice): which question is shown, what an
    /// answer does (shown right or wrong at once, the right one named, the next question after the showing), what the
    /// result card reads, and that practice asks about the house's rules and never the season. Unity-free, so the
    /// subset runs it.
    /// </summary>
    public sealed class TriviaRoundTests
    {
        private static TriviaQuestion Q(string fact, string text, int answer, params string[] choices) =>
            new TriviaQuestion(fact, 1, text, Array.AsReadOnly(choices), answer);

        private static IReadOnlyList<TriviaQuestion> Three() => new[]
        {
            Q("hoh-1", "Who won Head of Household in week 1?", 1, "Avery", "Blair", "Casey"),
            Q("evicted-2", "Who was evicted in week 2?", 0, "Dana", "Ellis", "Flynn", "Gray"),
            Q("veto-3", "Who won the Power of Veto in week 3?", 2, "Harper", "Indy", "Jules"),
        };

        [Test]
        public void TheQuestionsAreShownOneAtATimeInTheQuizsOrderWithTheirCount()
        {
            var quiz = Three();
            var round = new TriviaRound(quiz);
            Assert.That((round.Count, round.Index, round.Answered, round.Right, round.Done), Is.EqualTo((3, 0, 0, 0, false)));
            Assert.That(round.Current, Is.SameAs(quiz[0]));
            Assert.That(round.CountLine, Is.EqualTo("Question 1 of 3"));
            Assert.That(round.Feedback, Is.EqualTo("Choose an answer"));

            Assert.That(round.Answer(1), Is.EqualTo(TriviaRound.Outcome.Right));
            Assert.That(round.Current, Is.SameAs(quiz[0]), "The answered question stays up while its answer is shown.");
            round.Tick(TriviaRound.RevealSeconds);
            Assert.That(round.Current, Is.SameAs(quiz[1]), "Then the next comes up.");
            Assert.That(round.CountLine, Is.EqualTo("Question 2 of 3"));
            Assert.That(round.Revealing, Is.False);
            Assert.That(round.Feedback, Is.EqualTo("Choose an answer"));
        }

        [Test]
        public void AnAnswerIsShownRightOrWrongAtOnceAndAWrongOneNamesTheRightAnswer()
        {
            var round = new TriviaRound(Three());
            Assert.That(round.Answer(1), Is.EqualTo(TriviaRound.Outcome.Right));
            Assert.That((round.Revealing, round.Revealed, round.Right), Is.EqualTo((true, 1, 1)));
            Assert.That(round.Feedback, Is.EqualTo("Right: Blair"));
            round.Tick(TriviaRound.RevealSeconds);

            Assert.That(round.Answer(3), Is.EqualTo(TriviaRound.Outcome.Wrong));
            Assert.That((round.Revealing, round.Revealed, round.Right, round.Answered), Is.EqualTo((true, 3, 1, 2)));
            Assert.That(round.Feedback, Is.EqualTo("Wrong: the answer was Dana"));
        }

        [Test]
        public void ASecondAnswerWhileOneIsShownAndAChoiceTheQuestionLacksAreRefusedAndChangeNothing()
        {
            var round = new TriviaRound(Three());
            Assert.That(round.Answer(-1), Is.EqualTo(TriviaRound.Outcome.Refused));
            Assert.That(round.Answer(3), Is.EqualTo(TriviaRound.Outcome.Refused), "The first question has three choices.");
            Assert.That(round.Answered, Is.EqualTo(0));
            round.Answer(0);
            Assert.That(round.Answer(1), Is.EqualTo(TriviaRound.Outcome.Refused), "Taken once.");
            Assert.That(round.Chosen[0], Is.EqualTo(0));
            round.Tick(TriviaRound.RevealSeconds * .9);
            Assert.That((round.Index, round.Revealing), Is.EqualTo((0, true)), "The showing lasts its time.");
            round.Tick(TriviaRound.RevealSeconds * .1);
            Assert.That((round.Index, round.Revealing), Is.EqualTo((1, false)));
        }

        [Test]
        public void TheResultCardReadsTheRightAnswersAndTheResultIsTheRulesShare()
        {
            var quiz = Three();
            var round = new TriviaRound(quiz);
            round.Answer(1); round.Tick(TriviaRound.RevealSeconds);
            round.Answer(2); round.Tick(TriviaRound.RevealSeconds);
            Assert.That(round.Done, Is.False);
            round.Answer(2); round.Tick(TriviaRound.RevealSeconds);
            Assert.That(round.Done, Is.True);
            Assert.That(round.Current, Is.Null);
            Assert.That(round.Chosen, Is.EqualTo(new[] { 1, 2, 2 }));
            Assert.That(round.ResultLine, Is.EqualTo("2 of 3 right"));
            Assert.That(round.CountLine, Is.EqualTo("Question 3 of 3"));
            Assert.That(round.Performance, Is.EqualTo(CompetitionTrivia.Performance(quiz, new[] { 1, 2, 2 })));
            Assert.That(round.Performance, Is.EqualTo(2 / 3.0));
            Assert.That(round.Answer(0), Is.EqualTo(TriviaRound.Outcome.Refused), "Nothing is taken after the last.");
        }

        [Test]
        public void TheClockStopsTheRoundAndAQuestionNeverAnsweredIsWrong()
        {
            var round = new TriviaRound(Three());
            round.Answer(1); round.Tick(TriviaRound.RevealSeconds);
            round.Stop();
            Assert.That(round.Answer(0), Is.EqualTo(TriviaRound.Outcome.Refused));
            round.Tick(10);
            Assert.That(round.Index, Is.EqualTo(1), "A stopped round stands still.");
            Assert.That(round.ResultLine, Is.EqualTo("1 of 3 right"));
            Assert.That(round.Performance, Is.EqualTo(1 / 3.0));

            var empty = new TriviaRound(null);
            Assert.That((empty.Count, empty.Done, empty.Performance, empty.CountLine), Is.EqualTo((0, true, 0.0, "No questions")));
            Assert.That(empty.Answer(0), Is.EqualTo(TriviaRound.Outcome.Refused));
        }

        [Test]
        public void PracticeAsksAboutTheHouseRulesNeverTheSeason()
        {
            var practice = TriviaRound.PracticeQuiz;
            Assert.That(practice.Count, Is.EqualTo(3));
            foreach (var question in practice)
            {
                Assert.That(question.Fact, Does.StartWith("practice-"), "No fact of the season.");
                Assert.That(question.Week, Is.EqualTo(0));
                Assert.That(question.Choices.Count, Is.InRange(CompetitionTrivia.FewestChoices, CompetitionTrivia.MostChoices));
                Assert.That(question.Answer, Is.InRange(0, question.Choices.Count - 1));
                Assert.That(question.Choices.Distinct().Count(), Is.EqualTo(question.Choices.Count));
            }
            Assert.That(practice.Select(q => q.AnswerText), Is.EqualTo(new[] { "Two", "Save a nominee from the block", "The Head of Household" }));
        }

        [Test]
        public void ARealSeasonsQuizPlaysThroughTheRoundInItsOrder()
        {
            var state = new EpisodeState { seed = 77, week = 4, phase = EpisodePhase.HoH, competitionRulesVersion = CompetitionRules.Trivia, playerId = "c0" };
            string[] cast = { "Avery", "Blair", "Casey", "Dana", "Ellis", "Flynn", "Gray", "Harper", "Indy", "Jules" };
            for (int i = 0; i < cast.Length; i++)
                state.contestants.Add(new ContestantState { id = "c" + i, name = cast[i], isPlayer = i == 0, status = ContestantStatus.Active });
            void Row(int week, string hoh, string veto, bool used, string saved, string replacement, string[] block, int[] tally, string evictee)
            {
                state.ledger.power.Add(new PowerRow { week = week, hohId = hoh, vetoHolderId = veto, vetoUsed = used, savedId = saved,
                    replacementId = replacement, nominees = block.ToList(), tally = tally.ToList(), evicteeId = evictee });
                state.Find(evictee).status = ContestantStatus.Jury;
            }
            Row(1, "c1", "c4", false, null, null, new[] { "c9", "c6" }, new[] { 4, 1 }, "c9");
            Row(2, "c2", "c5", true, "c0", "c6", new[] { "c8", "c6" }, new[] { 3, 2 }, "c8");
            Row(3, "c3", "c4", false, null, null, new[] { "c5", "c7" }, new[] { 0, 5 }, "c7");
            var quiz = CompetitionTrivia.Quiz(state);
            Assert.That(quiz.Count, Is.EqualTo(CompetitionTrivia.Questions), "The season is far enough along.");

            var round = new TriviaRound(quiz);
            for (int i = 0; i < quiz.Count; i++)
            {
                Assert.That(round.Current, Is.SameAs(quiz[i]));
                Assert.That(round.CountLine, Is.EqualTo("Question " + (i + 1) + " of 5"));
                Assert.That(round.Answer(quiz[i].Answer), Is.EqualTo(TriviaRound.Outcome.Right));
                round.Tick(TriviaRound.RevealSeconds);
            }
            Assert.That((round.Done, round.ResultLine, round.Performance), Is.EqualTo((true, "5 of 5 right", 1.0)));
        }
    }
}
