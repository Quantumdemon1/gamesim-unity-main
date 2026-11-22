using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gamesim.Presentation
{
    /// <summary>
    /// The trivia's board (competition rules 6): the question across the top, whether the last answer was right
    /// under it, and the answers as up to four controls in two rows. Click an answer, or walk them with the
    /// arrows or the D-pad and press Enter or A, as every board's controls are walked. The count of questions
    /// stands in the board's band and in the timer card; the clock is the frame's own.
    ///
    /// <para>Each answer is a control named "Answer 1" to "Answer 4" - names are how a test, the season walk and
    /// a screen reader find them - whose caption is the answer's own words. An answer taken lights green when it
    /// was right, red when it was wrong with the right one lit green beside it, until the next question comes up
    /// (<see cref="TriviaRound.RevealSeconds"/>). The answers stay controls while an answer is shown - the round
    /// refuses a second one - because a control made non-interactable under the keyboard drops the selection.
    /// Before the start, and while paused or held, the board shows no question: a pause is not time to think.</para>
    /// </summary>
    public sealed partial class CompetitionGameScreen
    {
        /// <summary>An answer control's name: the caption is the answer's own words.</summary>
        public static string AnswerName(int choice) => "Answer " + (choice + 1);

        private readonly Button[] triviaAnswers = new Button[Gamesim.Simulation.CompetitionTrivia.MostChoices];
        private readonly Image[] triviaFaces = new Image[Gamesim.Simulation.CompetitionTrivia.MostChoices];
        private readonly TMP_Text[] triviaCaptions = new TMP_Text[Gamesim.Simulation.CompetitionTrivia.MostChoices];
        private RectTransform triviaQuestionPanel;
        private TMP_Text triviaQuestion, triviaVerdict, triviaChip;
        private int triviaShownIndex = -1, triviaShownChoices = -1;

        private bool TriviaBoard => run != null && run.Kind == CompetitionMiniGames.Kind.Trivia;

        private void BuildTrivia()
        {
            var bold = UiTheme.Font(UiTheme.Weight.Bold);
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            triviaQuestionPanel = HudPrimitives.Fill("Trivia question", playArea, new Color(UiTheme.Ink.r, UiTheme.Ink.g, UiTheme.Ink.b, .6f), 14);
            UiTheme.AddBorder(triviaQuestionPanel, 14, UiTheme.Outline);
            triviaQuestionPanel.GetComponent<Image>().raycastTarget = false;
            triviaQuestion = HudPrimitives.Label("Question text", triviaQuestionPanel, 24f * FontScale, UiTheme.Paper, TextAlignmentOptions.Center);
            if (bold != null) triviaQuestion.font = bold;
            triviaQuestion.textWrappingMode = TextWrappingModes.Normal; Fit(triviaQuestion, 13);
            Stretch(triviaQuestion.rectTransform, 16f, 6f, 16f, 6f);
            triviaVerdict = HudPrimitives.Label("Trivia verdict", playArea, 18f * FontScale, UiTheme.Muted, TextAlignmentOptions.Center);
            if (semibold != null) triviaVerdict.font = semibold;
            triviaVerdict.textWrappingMode = TextWrappingModes.NoWrap; Fit(triviaVerdict, 11);

            for (int i = 0; i < triviaAnswers.Length; i++)
            {
                int choice = i;
                var root = HudPrimitives.Fill(AnswerName(i), playArea, UiTheme.ActionBlue, 10);
                var face = root.GetComponent<Image>(); face.raycastTarget = true;
                var button = root.gameObject.AddComponent<Button>();
                button.targetGraphic = face;
                Untinted(button);
                button.onClick.AddListener(() => PickTriviaAnswer(choice));
                var caption = HudPrimitives.Label("Label", root, 20f * FontScale, Color.white, TextAlignmentOptions.Center);
                if (semibold != null) caption.font = semibold;
                caption.textWrappingMode = TextWrappingModes.Normal; Fit(caption, 11);
                Stretch(caption.rectTransform, 12f, 4f, 12f, 4f);
                var ring = new GameObject("Focus ring", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                ring.rectTransform.SetParent(root, false); Stretch(ring.rectTransform, -5f, -5f, -5f, -5f);
                ring.raycastTarget = false;
                if (!UiTheme.PackSliced(ring, PackArt.KitCardEdgeFocus, 14f, UiTheme.Paper)) UiTheme.Style(ring, new Color(1f, 1f, 1f, .9f), 14);
                ring.gameObject.SetActive(false);
                root.gameObject.AddComponent<CompetitionCardFocus>().Ring = ring.gameObject;
                root.gameObject.AddComponent<HudPress>().ReducedMotion = ReducedMotion;
                triviaAnswers[i] = button; triviaFaces[i] = face; triviaCaptions[i] = caption;
            }
            triviaChip = HudPrimitives.Label("Trivia count", boardChips, 15f * FontScale, UiTheme.Positive, TextAlignmentOptions.MidlineLeft);
            triviaChip.textWrappingMode = TextWrappingModes.NoWrap; Fit(triviaChip, 11);
            triviaShownIndex = -1; triviaShownChoices = -1;
            WireTriviaNavigation(TriviaChoiceCount);
        }

        /// <summary>The choices the question on screen offers; none once the round is over.</summary>
        private int TriviaChoiceCount => run?.Trivia?.Current == null ? 0
            : Mathf.Min(triviaAnswers.Length, run.Trivia.Current.Choices.Count);

        /// <summary>
        /// Two rows of two: left and right walk a row, up from the first row reaches the way back and down from
        /// the second the pause, as on every board. A question of three leaves the fourth out of the ring.
        /// </summary>
        private void WireTriviaNavigation(int count)
        {
            for (int i = 0; i < triviaAnswers.Length; i++)
            {
                if (i >= count) { triviaAnswers[i].navigation = new Navigation { mode = Navigation.Mode.None }; continue; }
                int row = i / 2, column = i % 2;
                int partner = row * 2 + (1 - column);
                Selectable across = partner < count ? triviaAnswers[partner] : triviaAnswers[i];
                Selectable up = row == 0 ? (Selectable)cancel : triviaAnswers[i - 2];
                Selectable down = row == 0 ? (i + 2 < count ? triviaAnswers[i + 2] : count > 2 ? triviaAnswers[count - 1] : (Selectable)pause) : pause;
                triviaAnswers[i].navigation = new Navigation { mode = Navigation.Mode.Explicit,
                    selectOnLeft = across, selectOnRight = across, selectOnUp = up, selectOnDown = down };
            }
        }

        private void PlaceTrivia()
        {
            var field = PlayField;
            float scale = FontScale;
            float margin = 14f, gap = 10f;
            float questionHeight = 2f * 24f * scale * 1.3f + 14f;
            float verdictHeight = 18f * scale * 1.4f;
            float answerHeight = Mathf.Max(52f * scale, 20f * scale * 1.3f + 10f);
            float need = margin + questionHeight + 8f + verdictHeight + 12f + 2f * answerHeight + gap + margin;
            // A short board shrinks every row alike; each box stays at least 1.3 times its smallest font.
            float squeeze = Mathf.Clamp01(field.height / Mathf.Max(1f, need));
            questionHeight = Mathf.Max(13f * 2.6f + 14f, questionHeight * squeeze);
            verdictHeight = Mathf.Max(11f * 1.4f, verdictHeight * squeeze);
            answerHeight = Mathf.Max(11f * 1.3f + 10f, answerHeight * squeeze);
            float width = Mathf.Min(field.width - 48f, 820f), left = (field.width - width) * .5f;
            float top = field.y + margin;
            Place(triviaQuestionPanel, left, top, width, questionHeight);
            top += questionHeight + 8f;
            Place(triviaVerdict.rectTransform, left, top, width, verdictHeight);
            top += verdictHeight + 12f;
            float answerWidth = (width - gap) * .5f;
            for (int i = 0; i < triviaAnswers.Length; i++)
                Place((RectTransform)triviaAnswers[i].transform, left + (i % 2) * (answerWidth + gap), top + (i / 2) * (answerHeight + gap), answerWidth, answerHeight);
            Place(triviaChip.rectTransform, 0f, 0f, surfaceWidth * .5f, BandHeight - 1f);
        }

        private void RefreshTrivia()
        {
            var round = run.Trivia;
            if (round == null || triviaQuestion == null) return;
            var question = round.Current;
            int count = TriviaChoiceCount;
            if (round.Index != triviaShownIndex || count != triviaShownChoices)
            {
                // A new question: its answers take their words, a spare control steps out, and the ring follows.
                triviaShownIndex = round.Index; triviaShownChoices = count;
                WireTriviaNavigation(count);
                if (playing) WireFooter();
            }
            bool hidden = Paused || held || (!playing && !run.Finished);
            triviaQuestion.text = hidden ? (Paused ? "Paused" : "Get ready") : question != null ? question.Text : "";
            for (int i = 0; i < triviaAnswers.Length; i++)
            {
                bool shown = i < count;
                triviaAnswers[i].gameObject.SetActive(shown);
                if (!shown) continue;
                triviaCaptions[i].text = hidden ? "?" : question.Choices[i];
                bool revealing = round.Revealing && !hidden;
                // A ranked quiz lights only the pick, green or red: the right answer is named in practice alone.
                bool right = i == question.Answer && (round.NamesAnswers || i == round.Revealed), picked = i == round.Revealed;
                Color face = !revealing ? UiTheme.ActionBlue
                    : right ? UiTheme.Positive
                    : picked ? UiTheme.Danger
                    : new Color(UiTheme.SurfaceRaised.r, UiTheme.SurfaceRaised.g, UiTheme.SurfaceRaised.b, .7f);
                triviaFaces[i].color = face;
                triviaCaptions[i].color = revealing && right ? UiTheme.Ink : revealing && !picked ? UiTheme.Muted : Color.white;
                triviaAnswers[i].interactable = !run.Finished;
            }
            triviaVerdict.text = hidden ? "" : round.Revealing ? round.Feedback : round.Done ? round.ResultLine : "Choose an answer";
            triviaVerdict.color = !round.Revealing || hidden ? UiTheme.Muted
                : round.Chosen[round.Index] == question.Answer ? UiTheme.Positive : UiTheme.Danger;
            triviaChip.text = round.CountLine + "  ·  " + round.Right + " right";
            status.text = round.CountLine;
            if (!string.IsNullOrEmpty(run.Feedback)) SetFeedback(hidden ? "" : run.Feedback);
            // A question of three hands the keyboard back from a fourth answer that stepped out.
            var events = UnityEngine.EventSystems.EventSystem.current;
            if (IsPlaying && count > 0 && events != null && events.currentSelectedGameObject != null)
            {
                int index = System.Array.IndexOf(triviaAnswers, events.currentSelectedGameObject.GetComponent<Button>());
                if (index >= count) Select(triviaAnswers[0]);
            }
        }

        /// <summary>An answer from the board: taken, shown right or wrong, and sounded. A refused one does nothing.</summary>
        private void PickTriviaAnswer(int choice)
        {
            if (!IsPlaying || run == null || run.Trivia == null) return;
            if (!run.AnswerTrivia(choice)) return;
            Cue(run.Trivia.Chosen[run.Trivia.Index] == run.Trivia.Current.Answer ? HouseAudio.Cue.SocialUp : HouseAudio.Cue.SocialDown);
            Refresh();
        }

        /// <summary>The answer the keyboard and the pad start on: the first.</summary>
        private Selectable TriviaFocus => TriviaChoiceCount > 0 ? triviaAnswers[0] : null;
    }
}
