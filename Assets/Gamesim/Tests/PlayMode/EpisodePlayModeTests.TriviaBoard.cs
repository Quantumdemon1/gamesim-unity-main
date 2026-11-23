using System.Collections;
using System.Linq;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// The trivia's board (competition rules 6, the screen slice), built as the game builds it and stood up beside the
    /// house: at every text size the question and its answers are drawn whole in boxes that hold them, the answers are
    /// walked by the keyboard and the pad in two rows, an answer lights right or wrong at once with the right one named,
    /// the next question follows, and the finish plate gives the count. The round's own rules are the Unity-free
    /// suite's (TriviaRoundTests, MiniGameRunTriviaTests); this is what only a built screen can show.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        private static TMP_Text BoardLabel(CompetitionGameScreen board, string name) =>
            board.GetComponentsInChildren<TMP_Text>(true).Last(text => text.name == name);

        private static Button BoardAnswer(CompetitionGameScreen board, int choice) =>
            board.GetComponentsInChildren<Button>(true).Last(button => button.name == CompetitionGameScreen.AnswerName(choice));

        /// <summary>A label drawn whole: its box at least 1.3 times its smallest font, and nothing cut off.</summary>
        private static void AssertDrawnWhole(TMP_Text label, string what)
        {
            label.ForceMeshUpdate();
            Assert.That(label.rectTransform.rect.height, Is.GreaterThanOrEqualTo(1.3f * label.fontSizeMin), what + ": the box holds its font.");
            Assert.That(label.isTextTruncated, Is.False, what + ": nothing is cut off.");
            Assert.That(label.textInfo.characterCount, Is.GreaterThan(0), what + ": something is drawn.");
        }

        [UnityTest]
        public IEnumerator TriviaBoard_AsksAnswersRightOrWrongAndCountsAtEveryTextSize()
        {
            var quiz = TriviaRound.PracticeQuiz;
            foreach (var size in TextSizes.All)
            {
                string at = " (" + TextSizes.Name(size) + " text)";
                var owner = new GameObject("Trivia board owner");
                var board = CompetitionGameScreen.Attach(owner);
                try
                {
                    var run = new MiniGameRun(CompetitionMiniGames.Kind.Trivia, 3, CompetitionMiniGames.TriviaRules, CompetitionDefinitions.Trivia, quiz: quiz);
                    board.FontScale = TextSizes.Scale(size);
                    board.Show(run, "Season Trivia", "You", false, i => { }, () => { }, d => { }, () => { }, () => { });
                    var question = BoardLabel(board, "Question text");
                    Assert.That(question.text, Is.Not.EqualTo(quiz[0].Text), "No question before the start" + at);
                    for (int frame = 0; frame < 120 && !board.IsPlaying; frame++) { board.AdvanceReady(.2f); yield return null; }
                    Assert.That(board.IsPlaying, Is.True, "The quiz is under way" + at);
                    board.Refresh();
                    yield return null;

                    // The first question and its three answers, drawn whole; the fourth control steps out.
                    Assert.That(question.text, Is.EqualTo(quiz[0].Text), at);
                    AssertDrawnWhole(question, "The question" + at);
                    for (int i = 0; i < 3; i++)
                    {
                        var caption = BoardAnswer(board, i).GetComponentInChildren<TMP_Text>();
                        Assert.That(BoardAnswer(board, i).gameObject.activeInHierarchy, Is.True);
                        Assert.That(caption.text, Is.EqualTo(quiz[0].Choices[i]), at);
                        AssertDrawnWhole(caption, "Answer " + (i + 1) + at);
                    }
                    Assert.That(BoardAnswer(board, 3).gameObject.activeSelf, Is.False, "A question of three shows three" + at);
                    Assert.That(BoardLabel(board, "Trivia count").text, Does.StartWith("Question 1 of 3"), at);

                    // Two rows: across within a row, down to the second row, up to the way back.
                    var first = BoardAnswer(board, 0).navigation;
                    Assert.That(first.selectOnRight, Is.SameAs(BoardAnswer(board, 1)), at);
                    Assert.That(first.selectOnDown, Is.SameAs(BoardAnswer(board, 2)), at);
                    Assert.That(BoardAnswer(board, 2).navigation.selectOnUp, Is.SameAs(BoardAnswer(board, 0)), at);

                    // A right answer lights green at once and says so.
                    BoardAnswer(board, quiz[0].Answer).onClick.Invoke();
                    yield return null;
                    Assert.That(BoardLabel(board, "Trivia verdict").text, Is.EqualTo("Right: " + quiz[0].AnswerText), at);
                    Assert.That(BoardAnswer(board, quiz[0].Answer).GetComponent<Image>().color, Is.EqualTo(UiTheme.Positive), at);
                    BoardAnswer(board, (quiz[0].Answer + 1) % 3).onClick.Invoke();
                    Assert.That(run.Trivia.Chosen[0], Is.EqualTo(quiz[0].Answer), "A second answer is refused" + at);

                    // The next question follows; a wrong answer lights red and the right one green, named.
                    run.Tick(TriviaRound.RevealSeconds + .05);
                    board.Refresh();
                    yield return null;
                    Assert.That(question.text, Is.EqualTo(quiz[1].Text), at);
                    Assert.That(BoardLabel(board, "Trivia count").text, Does.StartWith("Question 2 of 3  ·  1 right"), at);
                    int wrong = (quiz[1].Answer + 1) % quiz[1].Choices.Count;
                    BoardAnswer(board, wrong).onClick.Invoke();
                    yield return null;
                    Assert.That(BoardLabel(board, "Trivia verdict").text, Is.EqualTo("Wrong: the answer was " + quiz[1].AnswerText), at);
                    Assert.That(BoardAnswer(board, wrong).GetComponent<Image>().color, Is.EqualTo(UiTheme.Danger), at);
                    Assert.That(BoardAnswer(board, quiz[1].Answer).GetComponent<Image>().color, Is.EqualTo(UiTheme.Positive), at);

                    // The last, and the finish plate gives the count.
                    run.Tick(TriviaRound.RevealSeconds + .05);
                    board.Refresh();
                    BoardAnswer(board, quiz[2].Answer).onClick.Invoke();
                    run.Tick(TriviaRound.RevealSeconds + .05);
                    Assert.That(run.Finished, Is.True, at);
                    board.ShowRankedFinish("Your result goes to the standings in a moment.");
                    yield return null;
                    Assert.That(BoardLabel(board, "Finish headline").text, Is.EqualTo("QUIZ OVER"), at);
                    Assert.That(BoardLabel(board, "Finish measure").text, Does.StartWith("2 of 3 right"), at);
                    Assert.That(CompetitionGameScreen.AttemptLine(run), Is.EqualTo("Your attempt  ·  2 of 3 right  ·  performance 67%"), at);
                }
                finally
                {
                    Object.Destroy(board.gameObject);
                    Object.Destroy(owner);
                }
                yield return null;
            }
        }
    }
}
