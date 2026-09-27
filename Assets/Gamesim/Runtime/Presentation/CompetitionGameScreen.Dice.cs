using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gamesim.Presentation
{
    /// <summary>
    /// The luck board: three dice across the field, the total under them, and the two choices -
    /// roll again, or keep this roll.
    ///
    /// <para>A die is a tile with pips, not a digit: the pips are the dice's own, drawn from the
    /// theme's circle so no glyph can go missing from the font. Its caption is its name - "Die 2",
    /// "Die 2: 5" - which is how a screen reader, a test and the season walk read the dice.</para>
    ///
    /// <para>While a roll tumbles, the faces that show are cosmetic: this screen's own flicker,
    /// never the run's dealt faces, so nothing on the board gives a roll away before it lands. Under
    /// reduced motion a tumbling die is blank until it lands.</para>
    /// </summary>
    public sealed partial class CompetitionGameScreen
    {
        /// <summary>The words on the dice board's controls. Captions are a contract.</summary>
        public const string RollCaption = "Roll the dice", RollAgainCaption = "Roll again", KeepRollCaption = "Keep this roll";

        private const float TumbleStep = .07f;

        private readonly RectTransform[] dice = new RectTransform[MiniGameRun.DiceCount];
        private readonly Image[][] pips = new Image[MiniGameRun.DiceCount][];
        private readonly TMP_Text[] dieCaptions = new TMP_Text[MiniGameRun.DiceCount];
        private readonly int[] tumbleFaces = new int[MiniGameRun.DiceCount];
        private Button rollButton, keepButton;
        private TMP_Text rollLabel, diceTotal, rollsLeft;
        private float tumbleClock;
        private int shownRolls;
        private System.Random tumble;

        // Where each face puts its pips on a three-by-three grid, read left to right, top to bottom.
        private static readonly int[][] PipLayout =
        {
            new[] { 4 }, new[] { 0, 8 }, new[] { 0, 4, 8 }, new[] { 0, 2, 6, 8 }, new[] { 0, 2, 4, 6, 8 }, new[] { 0, 2, 3, 5, 6, 8 },
        };

        private void BuildDice()
        {
            // The flicker's own generator: cosmetic, so it is neither the run's nor the season's.
            tumble = new System.Random(unchecked(Time.frameCount * 7919));
            for (int i = 0; i < MiniGameRun.DiceCount; i++)
            {
                var die = HudPrimitives.Fill("Die " + (i + 1), playArea, UiTheme.Paper, 16);
                die.GetComponent<Image>().raycastTarget = false;
                UiTheme.AddBorder(die, 16, UiTheme.Edge(UiTheme.Emphasis.Interactive));
                dice[i] = die;
                pips[i] = new Image[9];
                for (int p = 0; p < 9; p++)
                {
                    var pip = Picture("Pip " + (p + 1), die, UiTheme.Circle(), UiTheme.Ink);
                    pip.gameObject.SetActive(false);
                    pips[i][p] = pip;
                }
                var caption = HudPrimitives.Label("Label", die, 15f * FontScale, UiTheme.Muted, TextAlignmentOptions.Center);
                caption.text = "Die " + (i + 1);
                caption.textWrappingMode = TextWrappingModes.NoWrap;
                var semibold = UiTheme.Font(UiTheme.Weight.SemiBold); if (semibold != null) caption.font = semibold;
                Fit(caption, 10);
                dieCaptions[i] = caption;
                tumbleFaces[i] = 1 + i * 2;
            }

            diceTotal = HudPrimitives.Label("Dice total", playArea, 30f * FontScale, UiTheme.Paper, TextAlignmentOptions.Center);
            var bold = UiTheme.Font(UiTheme.Weight.Bold); if (bold != null) diceTotal.font = bold;
            diceTotal.textWrappingMode = TextWrappingModes.NoWrap; Fit(diceTotal, 16);
            rollsLeft = HudPrimitives.Label("Rolls left", playArea, 16f * FontScale, UiTheme.Muted, TextAlignmentOptions.Center);
            rollsLeft.textWrappingMode = TextWrappingModes.NoWrap; Fit(rollsLeft, 11);

            rollButton = Button("Roll dice", playArea, RollCaption, 0, 0, 300, 64, RollDice);
            Untinted(rollButton); Focusable(rollButton);
            rollLabel = rollButton.GetComponentInChildren<TMP_Text>();
            rollButton.gameObject.AddComponent<HudPress>().ReducedMotion = ReducedMotion;
            keepButton = Button("Keep roll", playArea, KeepRollCaption, 0, 0, 300, 64, KeepRoll);
            Untinted(keepButton); Focusable(keepButton); Secondary(keepButton);
            keepButton.gameObject.AddComponent<HudPress>().ReducedMotion = ReducedMotion;
            rollButton.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnRight = keepButton, selectOnLeft = keepButton,
                selectOnUp = cancel, selectOnDown = pause };
            keepButton.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnRight = rollButton, selectOnLeft = rollButton,
                selectOnUp = cancel, selectOnDown = pause };
            shownRolls = 0; tumbleClock = 0f;
        }

        private void PlaceDice()
        {
            var field = PlayField;
            float side = Mathf.Clamp(Mathf.Min(field.width / 5.2f, field.height * .38f), 72f, 190f);
            float gap = side * .28f, row = 3f * side + 2f * gap;
            float left = (field.width - row) * .5f, top = field.y + Mathf.Max(16f, field.height * .1f);
            for (int i = 0; i < MiniGameRun.DiceCount; i++)
            {
                Place(dice[i], left + i * (side + gap), top, side, side);
                float pip = side * .17f, inset = side * .2f, span = (side - 2f * inset) * .5f;
                for (int p = 0; p < 9; p++)
                {
                    var rect = pips[i][p].rectTransform;
                    rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f); rect.pivot = new Vector2(.5f, .5f);
                    rect.anchoredPosition = new Vector2(inset + p % 3 * span, -(inset + p / 3 * span));
                    rect.sizeDelta = new Vector2(pip, pip);
                }
                var caption = dieCaptions[i].rectTransform;
                caption.anchorMin = new Vector2(0f, 0f); caption.anchorMax = new Vector2(1f, 0f); caption.pivot = new Vector2(.5f, 1f);
                caption.anchoredPosition = new Vector2(0f, -6f); caption.sizeDelta = new Vector2(24f, 22f * FontScale);
            }
            float y = top + side + 34f * FontScale;
            Place(diceTotal.rectTransform, 0f, y, field.width, 40f * FontScale); y += 42f * FontScale;
            Place(rollsLeft.rectTransform, 0f, y, field.width, 24f * FontScale); y += 34f * FontScale;
            float buttonWidth = Mathf.Min(300f, (field.width - 60f) * .5f), buttonHeight = 60f * FontScale;
            y = Mathf.Min(y, field.y + field.height - buttonHeight - 16f);
            Place((RectTransform)rollButton.transform, field.width * .5f - buttonWidth - 10f, y, buttonWidth, buttonHeight);
            Place((RectTransform)keepButton.transform, field.width * .5f + 10f, y, buttonWidth, buttonHeight);
        }

        private void RefreshDice()
        {
            bool hidden = Paused || held;
            for (int i = 0; i < MiniGameRun.DiceCount; i++)
            {
                bool landed = run.DieLanded(i);
                int face = landed ? run.Face(i) : run.Rolling && !ReducedMotion && !hidden ? tumbleFaces[i] : 0;
                var shown = face >= 1 && face <= 6 ? PipLayout[face - 1] : new int[0];
                for (int p = 0; p < 9; p++) pips[i][p].gameObject.SetActive(System.Array.IndexOf(shown, p) >= 0);
                dieCaptions[i].text = "Die " + (i + 1) + (landed && run.Face(i) > 0 ? ": " + run.Face(i) : run.Rolling ? ": rolling" : "");
                dice[i].GetComponent<Image>().color = landed && run.RollsUsed > 0 ? UiTheme.Paper
                    : new Color(UiTheme.Paper.r, UiTheme.Paper.g, UiTheme.Paper.b, run.Rolling ? .85f : .55f);
            }
            int total = run.RollTotal;
            diceTotal.text = run.RollsUsed == 0 ? "Roll to begin" : run.Rolling ? "Rolling..." : "Total " + total;
            int left = MiniGameRun.MaxRolls - run.RollsUsed;
            rollsLeft.text = left == 0 ? "No rolls left: this roll stands"
                : run.RollsUsed == 0 ? "Three rolls. Each one replaces the last."
                : left == 1 ? "One roll left. Rolling again gives up this " + total + "."
                : left + " rolls left. Rolling again gives up this " + total + ".";
            rollLabel.text = run.RollsUsed == 0 ? RollCaption : RollAgainCaption;
            // The board's own group takes the controls off while it is not live. A tumble leaves
            // them on and inert - a press waits for the dice - because a Selectable made
            // non-interactable under the keyboard drops the selection, and the keyboard would land
            // nowhere when the dice did.
            rollButton.interactable = !run.Finished && run.RollsUsed < MiniGameRun.MaxRolls;
            keepButton.interactable = !run.Finished && run.RollsUsed > 0;
            keepButton.gameObject.SetActive(run.RollsUsed > 0);
            status.text = run.RollsUsed == 0 ? "3 rolls  ·  keep one"
                : "Roll " + run.RollsUsed + " of " + MiniGameRun.MaxRolls + (run.Rolling ? "" : "  ·  total " + total);
            if (run.RollsUsed != shownRolls && !run.Rolling && run.RollsUsed > 0)
            { shownRolls = run.RollsUsed; SetFeedback("Rolled " + total + (left > 0 ? ": keep it or roll again" : ": it stands")); }
        }

        private void RollDice()
        {
            if (!IsPlaying || run == null || !run.Roll()) return;
            Cue(HouseAudio.Cue.Button);
            SetFeedback("Rolling...");
            Refresh();
        }

        private void KeepRoll()
        {
            if (!IsPlaying || run == null || !run.Keep()) return;
            Cue(HouseAudio.Cue.SocialUp);
            Refresh();
        }

        /// <summary>
        /// The tumble: cosmetic faces that change every 70 ms, as the reference's roll does. The
        /// board is drawn every frame the attempt is ticked; this only moves the faces it draws.
        /// </summary>
        private void AdvanceDiceBeats(float delta)
        {
            if (run == null || !run.Rolling || ReducedMotion) return;
            tumbleClock += Mathf.Max(0f, delta);
            if (tumbleClock < TumbleStep) return;
            tumbleClock = 0f;
            for (int i = 0; i < MiniGameRun.DiceCount; i++) if (!run.DieLanded(i)) tumbleFaces[i] = 1 + tumble.Next(6);
        }
    }
}
