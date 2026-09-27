using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Gamesim.Presentation
{
    /// <summary>
    /// How an attempt ends: a plate over the board with what happened (the board cleared, the grip
    /// giving out, the bell), the game's own measure of it, the performance it is worth and the
    /// line the director has to say. A ranked attempt holds the plate a moment before it commits,
    /// so the player sees their result before the standings replace it.
    /// </summary>
    public sealed partial class CompetitionGameScreen
    {
        private RectTransform finishPlate;
        private TMP_Text finishHeadline, finishMeasure, finishPerformance, finishSummary;
        // A ranked finish on its plate, waiting to commit: nothing may leave it now.
        private bool holdingFinish;

        /// <summary>Whether the finish plate is up.</summary>
        public bool FinishShowing => finishPlate != null && finishPlate.gameObject.activeSelf;

        private void BuildFinishPlate()
        {
            finishPlate = HudPrimitives.Fill("Finish plate", playArea, UiTheme.Ink, 12);
            finishPlate.GetComponent<Image>().raycastTarget = true;
            var bold = UiTheme.Font(UiTheme.Weight.Bold);
            finishHeadline = HudPrimitives.Label("Finish headline", finishPlate, 44f * FontScale, UiTheme.Paper, TextAlignmentOptions.Center);
            if (bold != null) finishHeadline.font = bold;
            finishHeadline.characterSpacing = 2f; finishHeadline.textWrappingMode = TextWrappingModes.NoWrap; Fit(finishHeadline, 20);
            finishMeasure = HudPrimitives.Label("Finish measure", finishPlate, 22f * FontScale, UiTheme.Paper, TextAlignmentOptions.Center);
            finishMeasure.textWrappingMode = TextWrappingModes.NoWrap; Fit(finishMeasure, 14);
            finishPerformance = HudPrimitives.Label("Finish performance", finishPlate, 30f * FontScale, UiTheme.Gold, TextAlignmentOptions.Center);
            if (bold != null) finishPerformance.font = bold;
            finishPerformance.textWrappingMode = TextWrappingModes.NoWrap; Fit(finishPerformance, 16);
            finishSummary = HudPrimitives.Label("Finish summary", finishPlate, 16f * FontScale, UiTheme.Muted, TextAlignmentOptions.Top);
            Fit(finishSummary, 12);
            finishPlate.gameObject.SetActive(false);
            holdingFinish = false;
        }

        private void LayoutFinishPlate()
        {
            if (finishPlate == null) return;
            var field = PlayField;
            Place(finishPlate, 0f, field.y, surfaceWidth, field.height);
            float width = Mathf.Min(field.width - 48f, 820f), x = (field.width - width) * .5f;
            float headline = 1.3f * 44f * FontScale, measure = 1.3f * 22f * FontScale, performance = 1.3f * 30f * FontScale, summary = 2.6f * 16f * FontScale;
            float total = headline + 10f + measure + 12f + performance + 14f + summary;
            float y = Mathf.Max(16f, (field.height - total) * .5f);
            Place(finishHeadline.rectTransform, x, y, width, headline); y += headline + 10f;
            Place(finishMeasure.rectTransform, x, y, width, measure); y += measure + 12f;
            Place(finishPerformance.rectTransform, x, y, width, performance); y += performance + 14f;
            Place(finishSummary.rectTransform, x, y, width, summary);
        }

        /// <summary>
        /// The end of an attempt that waits for the player: a practice, a result that could not be
        /// saved, an episode that moved on. <paramref name="hideCancel"/> leaves one way on where
        /// Back to briefing would do the same thing as the action.
        /// </summary>
        public void ShowFinished(string summary, string action, Action onContinue, bool hideCancel = false)
        {
            playing = false; holdingFinish = false; controls.interactable = false;
            ShowOverlay(OverlayState.None, "");
            PaintFinish(summary);
            FillLegend();
            pause.gameObject.SetActive(false); finishAction.gameObject.SetActive(true);
            cancel.gameObject.SetActive(!hideCancel); cancel.interactable = true;
            Place((RectTransform)finishAction.transform, 12f, hideCancel ? 14f : 118f, SideWidth - 24f, 44f);
            finishAction.GetComponentInChildren<TMP_Text>().text=action;
            finishAction.onClick.RemoveAllListeners(); finishAction.onClick.AddListener(() => onContinue?.Invoke());
            finishAction.navigation = new Navigation { mode=Navigation.Mode.Explicit,selectOnLeft=cancel,selectOnRight=cancel,
                selectOnUp=cancel,selectOnDown=cancel };
            cancel.navigation = new Navigation { mode=Navigation.Mode.Explicit,selectOnLeft=finishAction,selectOnRight=finishAction,
                selectOnUp=finishAction,selectOnDown=finishAction };
            if (hideCancel) finishAction.navigation = new Navigation { mode = Navigation.Mode.None };
            if(EventSystem.current!=null) EventSystem.current.SetSelectedGameObject(finishAction.gameObject);
        }

        /// <summary>
        /// A ranked attempt is over and about to commit: its plate, and no way off it. The director
        /// commits once the player has had a moment to read it.
        /// </summary>
        public void ShowRankedFinish(string summary)
        {
            playing = false; holdingFinish = true; controls.interactable = false;
            ShowOverlay(OverlayState.None, "");
            PaintFinish(summary);
            FillLegend();
            pause.gameObject.SetActive(false); finishAction.gameObject.SetActive(false);
            cancel.interactable = false;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }

        private void PaintFinish(string summary)
        {
            finishPlate.gameObject.SetActive(true);
            finishPlate.SetAsLastSibling();
            finishHeadline.text = FinishHeadline();
            finishMeasure.text = FinishMeasure();
            finishPerformance.text = "Performance " + (run.Performance * 100).ToString("0") + "%";
            finishSummary.text = summary ?? "";
            if (effortControl != null) effortControl.gameObject.SetActive(false);
            if (staminaPanel != null) staminaPanel.gameObject.SetActive(false);
            if (target != null) target.gameObject.SetActive(false);
            status.text = "Performance " + (run.Performance*100).ToString("0") + "%";
            SetFeedback("");
            RefreshTimer();
            LayoutFinishPlate();
        }

        private string FinishHeadline()
        {
            switch (run.Kind)
            {
                case CompetitionMiniGames.Kind.Memory:
                    return run.MatchedPairs >= run.Pairs ? "BOARD CLEARED" : "TIME";
                case CompetitionMiniGames.Kind.Endurance:
                    return run.Meter <= CompetitionMiniGames.MeterEmpty && run.Remaining > 0
                        ? "GRIP GAVE OUT AT " + run.Elapsed.ToString("0.0") + " s" : "TIME";
                case CompetitionMiniGames.Kind.Dice:
                    return run.KeptTotal == 0 ? "TIME" : run.Remaining > 0 ? "KEPT " + run.KeptTotal : "TIME  ·  " + run.KeptTotal + " STANDS";
                default: return run.Remaining > 0 ? "ATTEMPT OVER" : "TIME";
            }
        }

        private string FinishMeasure()
        {
            switch (run.Kind)
            {
                case CompetitionMiniGames.Kind.Memory:
                    return run.MatchedPairs + " / " + run.Pairs + " pairs  ·  " + Plural(run.WrongFlips, "mistake");
                case CompetitionMiniGames.Kind.Endurance:
                    return "Held " + run.Held.ToString("0.0") + " s of " + EnduranceTarget.ToString("0.0") + " s for full marks";
                case CompetitionMiniGames.Kind.Reaction:
                    return run.Hits + " / " + run.Spawned + " targets hit  ·  " + Plural(run.FalseStarts, "early press", "early presses");
                case CompetitionMiniGames.Kind.Dice:
                    return run.KeptTotal == 0 ? "No roll kept" : "Total " + run.KeptTotal + " of 18  ·  roll " + run.RollsUsed + " of " + MiniGameRun.MaxRolls;
                case CompetitionMiniGames.Kind.Words:
                    return Plural(run.WordsSolved, "word") + " spelled  ·  " + run.WordPoints.ToString("0.#") + " points";
                default: return "";
            }
        }

        private static string Plural(int count, string one, string many) => count + " " + (count == 1 ? one : many);

        /// <summary>
        /// The player's attempt in one line, for the standings card that follows it: the game's own
        /// measure and the performance it was worth. Presentation only - nothing saves it.
        /// </summary>
        public static string AttemptLine(MiniGameRun run)
        {
            if (run == null || !run.Finished) return null;
            string measure;
            switch (run.Kind)
            {
                case CompetitionMiniGames.Kind.Memory: measure = run.MatchedPairs + " / " + run.Pairs + " pairs"; break;
                case CompetitionMiniGames.Kind.Reaction: measure = run.Hits + " / " + run.Spawned + " targets hit"; break;
                case CompetitionMiniGames.Kind.Endurance: measure = "held " + run.Held.ToString("0.0") + " s"; break;
                case CompetitionMiniGames.Kind.Dice:
                    measure = run.KeptTotal == 0 ? "no roll kept" : "kept " + run.KeptTotal + " on roll " + run.RollsUsed + " of " + MiniGameRun.MaxRolls; break;
                case CompetitionMiniGames.Kind.Words:
                    measure = Plural(run.WordsSolved, "word") + " spelled"; break;
                default: return null;
            }
            return "Your attempt  ·  " + measure + "  ·  performance " + (run.Performance * 100).ToString("0") + "%";
        }

        // ---------------------------------------------------------------- per-game plumbing

        /// <summary>Forgets the last attempt's controls: they went with its studio.</summary>
        private void ForgetGames()
        {
            Array.Clear(cards, 0, cards.Length);
            Array.Clear(pairShown, 0, pairShown.Length);
            target = null; reactionFocus = null; effortControl = null;
            rollButton = null; keepButton = null; clearButton = null; skipButton = null;
            Array.Clear(tiles, 0, tiles.Length);
            marks.Clear();
            directionGuides[0] = directionGuides[1] = null;
            Array.Clear(legendMarks, 0, legendMarks.Length);
            pressureTimeline = null; pressurePlayhead = null;
            lastWholeSecond = -1; clockPulse = 0f;
        }

        private bool IsGameControl(Selectable selectable) =>
            selectable == reactionFocus || selectable == effortControl || run.Kind == CompetitionMiniGames.Kind.Memory
            || run.Kind == CompetitionMiniGames.Kind.Dice || run.Kind == CompetitionMiniGames.Kind.Words;

        private Selectable DefaultGameFocus
        {
            get
            {
                switch (run.Kind)
                {
                    case CompetitionMiniGames.Kind.Memory: return cards[0];
                    case CompetitionMiniGames.Kind.Reaction: return reactionFocus;
                    case CompetitionMiniGames.Kind.Dice: return run.RollsUsed < MiniGameRun.MaxRolls ? (Selectable)rollButton : keepButton;
                    case CompetitionMiniGames.Kind.Words: return tiles[0] != null ? NextOpenTile(-1) : null;
                    default: return effortControl;
                }
            }
        }
    }
}
