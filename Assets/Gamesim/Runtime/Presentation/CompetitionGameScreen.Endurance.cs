using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Gamesim.Presentation
{
    /// <summary>
    /// The endurance board: the arena shows through, and one stamina panel at the foot of the
    /// field holds the two numbers that matter - the grip left, and the effort banked towards full
    /// marks - with how fast the grip is going, a warning before it runs out, and for Pressure
    /// Cooker the waves still to come on a timeline.
    ///
    /// <para>Grip value keeps its words ("Grip 42% · HOLDING · Wave in 1.2s"): the season walk
    /// reads the number before the % sign, and the captions on the toggle are the contract.</para>
    /// </summary>
    public sealed partial class CompetitionGameScreen
    {
        private const float LowGrip = 25f, EnduranceBoardAlpha = .40f;

        private RectTransform staminaPanel, gripTrack, gripFill, lowGripTick, effortTrack, effortFill, gripWarning;
        private RectTransform pressureTimeline, pressurePlayhead, holdStateEdge;
        private readonly List<RectTransform> pressureWaves = new List<RectTransform>();
        private readonly List<Vector2> waveSpans = new List<Vector2>();
        private TMP_Text enduranceHeadline, gripLabel, gripRate, gripWarningLabel, effortLabel, actionLabel, pressureLabel, holdHint;
        private Image gripFillImage, gripWarningMark;
        private Selectable effortControl;
        private bool holdKeyWasDown, inWave;

        private void BuildEndurance(Action toggleGrip)
        {
            // The arena is what is being endured; the board lets it show.
            var ground = playArea.GetComponent<Image>();
            ground.color = new Color(UiTheme.GlassFill.r, UiTheme.GlassFill.g, UiTheme.GlassFill.b, EnduranceBoardAlpha);

            staminaPanel = HudPrimitives.Fill("Stamina panel", playArea, new Color(UiTheme.GlassFill.r, UiTheme.GlassFill.g, UiTheme.GlassFill.b, .94f), UiTheme.GlassRadius);
            if (!UiTheme.PackSliced(staminaPanel.GetComponent<Image>(), PackArt.StaminaPanel, 14f)) UiTheme.Glass(staminaPanel, UiTheme.GlassRadius);

            enduranceHeadline = HudPrimitives.Label("Endurance headline", staminaPanel, 26f * FontScale, UiTheme.Paper, TextAlignmentOptions.MidlineLeft);
            var bold = UiTheme.Font(UiTheme.Weight.Bold); if (bold != null) enduranceHeadline.font = bold;
            enduranceHeadline.characterSpacing = 2f; enduranceHeadline.textWrappingMode = TextWrappingModes.NoWrap; Fit(enduranceHeadline, 14);

            gripTrack = Meter("Grip track", staminaPanel, UiTheme.SurfaceRaised);
            gripFill = Meter("Grip remaining", gripTrack, UiTheme.Glow);
            gripFillImage = gripFill.GetComponent<Image>();
            lowGripTick = new GameObject("Low grip threshold", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            lowGripTick.SetParent(gripTrack, false);
            var tick = lowGripTick.GetComponent<Image>(); tick.raycastTarget = false; tick.color = UiTheme.Danger;
            tick.sprite = UiTheme.Pack(PackArt.KitMeterZeroTick);

            gripLabel = HudPrimitives.Label("Grip value", staminaPanel, 20f * FontScale, UiTheme.Paper, TextAlignmentOptions.MidlineLeft);
            gripLabel.textWrappingMode = TextWrappingModes.NoWrap; Fit(gripLabel, 12);
            gripRate = HudPrimitives.Label("Grip rate", staminaPanel, 16f * FontScale, UiTheme.Muted, TextAlignmentOptions.MidlineRight);
            gripRate.textWrappingMode = TextWrappingModes.NoWrap; Fit(gripRate, 11);

            gripWarning = new GameObject("Grip warning", typeof(RectTransform)).GetComponent<RectTransform>();
            gripWarning.SetParent(staminaPanel, false);
            gripWarningMark = Picture("Warning mark", gripWarning, UiTheme.Pack(PackArt.KitIconWarning), UiTheme.Warning);
            gripWarningLabel = HudPrimitives.Label("Warning words", gripWarning, 15f * FontScale, UiTheme.Warning, TextAlignmentOptions.MidlineLeft);
            gripWarningLabel.textWrappingMode = TextWrappingModes.NoWrap; Fit(gripWarningLabel, 11);

            effortLabel = HudPrimitives.Label("Effort value", staminaPanel, 15f * FontScale, UiTheme.Positive, TextAlignmentOptions.MidlineLeft);
            effortLabel.characterSpacing = 2f; effortLabel.textWrappingMode = TextWrappingModes.NoWrap; Fit(effortLabel, 11);
            effortTrack = Meter("Effort track", staminaPanel, UiTheme.SurfaceRaised);
            effortFill = Meter("Effort banked", effortTrack, UiTheme.Positive);

            // Pressure Cooker's waves: where they fall on the clock, and where the clock is.
            pressureWaves.Clear(); waveSpans.Clear();
            if (run.Definition != null && run.Definition.Pattern == Gamesim.Simulation.CompetitionPattern.PressureWaves)
            {
                pressureTimeline = Meter("Pressure timeline", staminaPanel, new Color(UiTheme.SurfaceRaised.r, UiTheme.SurfaceRaised.g, UiTheme.SurfaceRaised.b, .8f));
                double start = -1;
                const double step = .05;
                for (double t = 0; t <= run.TimeLimit + step / 2; t += step)
                {
                    bool wave = run.Definition.GripPressure(t) > 1 && t < run.TimeLimit;
                    if (wave && start < 0) start = t;
                    if (!wave && start >= 0) { waveSpans.Add(new Vector2((float)start, (float)t)); start = -1; }
                }
                for (int i = 0; i < waveSpans.Count; i++)
                {
                    var block = HudPrimitives.Fill("Pressure wave " + (i + 1), pressureTimeline, UiTheme.Warning, 3);
                    pressureWaves.Add(block);
                }
                pressurePlayhead = new GameObject("Pressure playhead", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
                pressurePlayhead.SetParent(pressureTimeline, false);
                var head = pressurePlayhead.GetComponent<Image>(); head.raycastTarget = false; head.color = UiTheme.Paper;
                head.sprite = UiTheme.Pack(PackArt.KitTimelineRing) ?? UiTheme.Circle(); head.preserveAspect = true;
                pressureLabel = HudPrimitives.Label("Pressure label", staminaPanel, 13f * FontScale, UiTheme.Warning, TextAlignmentOptions.MidlineRight);
                pressureLabel.characterSpacing = 2f; pressureLabel.textWrappingMode = TextWrappingModes.NoWrap; Fit(pressureLabel, 10);
            }
            else { pressureTimeline = null; pressurePlayhead = null; pressureLabel = null; }

            var hold = Button("Toggle grip", staminaPanel, "Start holding", 0, 0, 400, 72, toggleGrip);
            Untinted(hold);
            effortControl = hold;
            actionLabel = hold.GetComponentInChildren<TMP_Text>();
            UiTheme.AddBorder((RectTransform)hold.transform, 8, UiTheme.Edge(UiTheme.Emphasis.Interactive));
            HudEmphasis.Promote((RectTransform)hold.transform, UiTheme.Emphasis.Interactive);
            holdStateEdge = new GameObject("Hold state edge", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            holdStateEdge.SetParent(hold.transform, false); Stretch(holdStateEdge, -4f, -4f, -4f, -4f);
            var edge = holdStateEdge.GetComponent<Image>(); edge.raycastTarget = false;
            if (!UiTheme.PackSliced(edge, PackArt.KitCardEdgeFocus, 12f, UiTheme.Positive)) UiTheme.Style(edge, new Color(UiTheme.Positive.r, UiTheme.Positive.g, UiTheme.Positive.b, .6f), 12);
            holdStateEdge.gameObject.SetActive(false);
            holdHint = HudPrimitives.Label("Key hint", hold.transform, 12f * FontScale, Color.white, TextAlignmentOptions.MidlineRight);
            holdHint.textWrappingMode = TextWrappingModes.NoWrap;
            var hintRect = holdHint.rectTransform;
            hintRect.anchorMin = new Vector2(1f, 0f); hintRect.anchorMax = new Vector2(1f, 1f); hintRect.pivot = new Vector2(1f, .5f);
            hintRect.anchoredPosition = new Vector2(-14f, 0f); hintRect.sizeDelta = new Vector2(96f, 0f);
            hold.gameObject.AddComponent<HudPress>().ReducedMotion = ReducedMotion;
            hold.navigation = new Navigation { mode=Navigation.Mode.Explicit, selectOnDown=pause, selectOnUp=cancel };
            inWave = false;
        }

        /// <summary>A meter's track or fill: Kit 6's rounded bar, tinted.</summary>
        private static RectTransform Meter(string name, Transform parent, Color tint)
        {
            var rect = HudPrimitives.Fill(name, parent, tint, 6);
            UiTheme.PackSliced(rect.GetComponent<Image>(), PackArt.KitMeterTrack, 6f, tint);
            return rect;
        }

        private float EnduranceTarget => (float)CompetitionMiniGames.EnduranceTarget(run.TimeLimit, run.RulesVersion);

        /// <summary>
        /// What the grip panel's rows add up to at this text size. They are laid at fixed heights,
        /// so a field shorter than this squeezes the panel and its rows run into the hold button.
        /// </summary>
        private float EnduranceHeight
        {
            get
            {
                float line = 1.3f * 20f * FontScale;
                bool waves = pressureTimeline != null;
                return 20f + 1.3f * 26f * FontScale + 10f + 28f * FontScale + 8f + line + 6f + 1.3f * 15f * FontScale + 10f
                    + 1.3f * 15f * FontScale + 4f + 14f * FontScale + (waves ? 12f + 18f * FontScale : 0f) + 16f + 72f + 20f;
            }
        }

        private void PlaceEndurance()
        {
            var field = PlayField;
            float width = Mathf.Min(field.width - 48f, 900f), inner = width - 48f;
            float line = 1.3f * 20f * FontScale;
            bool waves = pressureTimeline != null;
            float height = Mathf.Min(EnduranceHeight, field.height - 24f);
            Place(staminaPanel, (field.width - width) * .5f, field.yMax - 16f - height, width, height);
            float y = 20f;
            Place(enduranceHeadline.rectTransform, 24f, y, inner, 1.3f * 26f * FontScale); y += 1.3f * 26f * FontScale + 10f;
            Place(gripTrack, 24f, y, inner, 28f * FontScale); y += 28f * FontScale + 8f;
            gripFill.anchorMin = Vector2.zero; gripFill.offsetMin = gripFill.offsetMax = Vector2.zero;
            lowGripTick.anchorMin = new Vector2(LowGrip / 100f, 0f); lowGripTick.anchorMax = new Vector2(LowGrip / 100f, 1f);
            lowGripTick.pivot = new Vector2(.5f, .5f); lowGripTick.anchoredPosition = Vector2.zero; lowGripTick.sizeDelta = new Vector2(3f, 6f);
            Place(gripLabel.rectTransform, 24f, y, inner * .62f, line);
            Place(gripRate.rectTransform, 24f + inner * .62f, y, inner * .38f, line); y += line + 6f;
            float warning = 1.3f * 15f * FontScale;
            Place(gripWarning, 24f, y, inner, warning);
            Place(gripWarningMark.rectTransform, 0f, (warning - 16f * FontScale) * .5f, 16f * FontScale, 16f * FontScale);
            Place(gripWarningLabel.rectTransform, 24f * FontScale, 0f, inner - 24f * FontScale, warning); y += warning + 10f;
            Place(effortLabel.rectTransform, 24f, y, inner, 1.3f * 15f * FontScale); y += 1.3f * 15f * FontScale + 4f;
            Place(effortTrack, 24f, y, inner, 14f * FontScale); y += 14f * FontScale;
            effortFill.anchorMin = Vector2.zero; effortFill.offsetMin = effortFill.offsetMax = Vector2.zero;
            if (waves)
            {
                y += 12f;
                float labelWidth = 170f * FontScale;
                Place(pressureTimeline, 24f, y, inner - labelWidth - 12f, 18f * FontScale);
                for (int i = 0; i < pressureWaves.Count; i++)
                {
                    var block = pressureWaves[i];
                    block.anchorMin = new Vector2(waveSpans[i].x / (float)run.TimeLimit, 0f);
                    block.anchorMax = new Vector2(waveSpans[i].y / (float)run.TimeLimit, 1f);
                    block.offsetMin = new Vector2(0f, 2f); block.offsetMax = new Vector2(0f, -2f);
                }
                float head = 18f * FontScale + 6f;
                pressurePlayhead.anchorMin = pressurePlayhead.anchorMax = new Vector2(0f, .5f);
                pressurePlayhead.pivot = new Vector2(.5f, .5f); pressurePlayhead.sizeDelta = new Vector2(head, head);
                Place(pressureLabel.rectTransform, 24f + inner - labelWidth, y, labelWidth, 18f * FontScale);
                y += 18f * FontScale;
            }
            y += 16f;
            var hold = (RectTransform)effortControl.transform;
            float buttonWidth = Mathf.Min(400f * FontScale, inner);
            Place(hold, (width - buttonWidth) * .5f, Mathf.Min(y, height - 20f - 72f), buttonWidth, 72f);
            var caption = actionLabel.rectTransform;
            Place(caption, 16f, 4f, buttonWidth - 16f - 110f, 64f);
        }

        private void RefreshEndurance()
        {
            float meter = (float)run.Meter;
            bool low = meter < LowGrip;
            gripFill.anchorMax = new Vector2(Mathf.Clamp01(meter / 100f), 1f);
            gripFillImage.color = low ? UiTheme.Danger : UiTheme.Glow;
            bool legacy = run.RulesVersion < CompetitionMiniGames.ImprovedRules;
            string state = Paused ? "PAUSED" : !playing && !run.Finished ? "READY" : run.Holding ? "HOLDING" : legacy ? "DRAINING" : "RECOVERING";
            string text = "Grip " + run.Meter.ToString("0") + "% · " + state;
            if (run.Definition?.Pattern == Gamesim.Simulation.CompetitionPattern.PressureWaves)
                text += run.GripPressure > 1 ? " · WAVE · " + run.PressureChangeIn.ToString("0.0") + "s"
                    : " · Wave in " + run.PressureChangeIn.ToString("0.0") + "s";
            gripLabel.text = text;

            // The state in words, with what it is doing for you: the grip line under it keeps its numbers.
            bool full = run.Meter >= CompetitionMiniGames.MeterFull;
            enduranceHeadline.text = state == "HOLDING" ? "HOLDING  ·  BANKING EFFORT"
                : state == "RECOVERING" ? (full ? "RESTING  ·  GRIP FULL" : "RECOVERING  ·  GRIP REFILLING")
                : state == "DRAINING" ? "LET GO  ·  GRIP DRAINING"
                : state;
            enduranceHeadline.color = state == "HOLDING" ? UiTheme.Positive : state == "RECOVERING" ? UiTheme.Glow
                : state == "DRAINING" ? UiTheme.Warning : state == "PAUSED" ? UiTheme.Gold : UiTheme.Paper;

            // What the grip can actually do: a full grip does not refill, an empty one does not drain.
            double rate = run.GripRate;
            if ((rate > 0 && run.Meter >= CompetitionMiniGames.MeterFull) || (rate < 0 && run.Meter <= CompetitionMiniGames.MeterEmpty)) rate = 0;
            gripRate.text = !playing || Paused ? "" : rate == 0 ? "steady" : (rate > 0 ? "+" : "-") + Math.Abs(rate).ToString("0.0") + " %/s";
            gripRate.color = rate < 0 ? UiTheme.Warning : UiTheme.Positive;
            // How long the grip has left while it is going down, said before it is gone.
            double empties = run.SecondsToEmpty;
            bool draining = playing && !Paused && !double.IsInfinity(empties);
            gripWarning.gameObject.SetActive(draining && (low || empties < 4.0));
            if (gripWarning.gameObject.activeSelf)
            {
                bool urgent = low;
                var tone = urgent ? UiTheme.Danger : UiTheme.Warning;
                gripWarningLabel.text = urgent
                    ? "LOW GRIP · empties in " + empties.ToString("0.0") + " s · " + (run.RulesVersion >= CompetitionMiniGames.ImprovedRules ? "release to recover" : "hold to refill")
                    : "Slipping · empties in " + empties.ToString("0.0") + " s";
                gripWarningLabel.color = tone; gripWarningMark.color = tone;
            }

            float target = Mathf.Max(.01f, EnduranceTarget);
            float banked = Mathf.Clamp01((float)run.Held / target);
            effortFill.anchorMax = new Vector2(banked, 1f);
            effortLabel.text = banked >= 1f ? "EFFORT BANKED · FULL MARKS BANKED"
                : "EFFORT BANKED · " + run.Held.ToString("0.0") + " / " + target.ToString("0.0") + " s";
            status.text = "Effort " + run.Held.ToString("0.0") + " / " + target.ToString("0.0") + " s";

            if (pressureTimeline != null)
            {
                float now = run.TimeLimit > 0 ? Mathf.Clamp01((float)(run.Elapsed / run.TimeLimit)) : 0f;
                pressurePlayhead.anchorMin = pressurePlayhead.anchorMax = new Vector2(now, .5f);
                pressurePlayhead.anchoredPosition = Vector2.zero;
                for (int i = 0; i < pressureWaves.Count; i++)
                {
                    bool past = waveSpans[i].y <= run.Elapsed, current = waveSpans[i].x <= run.Elapsed && !past;
                    var tone = current ? UiTheme.Danger : UiTheme.Warning;
                    pressureWaves[i].GetComponent<Image>().color = new Color(tone.r, tone.g, tone.b, past ? .35f : current ? 1f : .8f);
                }
                pressureLabel.text = run.GripPressure > 1 ? "WAVE x" + run.GripPressure.ToString("0.0") : "NEXT WAVE " + run.PressureChangeIn.ToString("0.0") + " s";
                pressureLabel.color = run.GripPressure > 1 ? UiTheme.Danger : UiTheme.Warning;
            }

            // Version 1 inverts the meter: letting go drains it, so its caption says so.
            actionLabel.text = run.Holding ? (legacy ? "Let go (grip drains)" : "Release to recover") : "Hold to earn effort";
            holdStateEdge.gameObject.SetActive(run.Holding && playing && !Paused);
            holdHint.text = usingPad ? "RT / A" : "Space / Enter";
        }

        private void AdvanceEnduranceBeats(float delta)
        {
            if (!playing || run.Finished) return;
            bool wave = run.GripPressure > 1;
            if (wave && !inWave) Cue(HouseAudio.Cue.SocialDown);
            inWave = wave;
        }

        /// <summary>
        /// Space and the right trigger hold for as long as they are down - read as a level, not an
        /// edge, so a key already down when GO lands grips on the first live frame and grips again
        /// after a resume. The on-screen toggle still toggles; the key only speaks when it changes.
        /// </summary>
        public void SyncHoldKey()
        {
            if (!IsPlaying || run == null || run.Kind != CompetitionMiniGames.Kind.Endurance) return;
            var keyboard = Keyboard.current; var pad = Gamepad.current;
            bool down = (keyboard != null && keyboard.spaceKey.isPressed) || (pad != null && pad.rightTrigger.isPressed);
            if (down == holdKeyWasDown) return;
            run.SetHolding(down); holdKeyWasDown = down;
        }
    }
}
