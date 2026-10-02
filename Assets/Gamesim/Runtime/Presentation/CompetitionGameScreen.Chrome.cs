using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Gamesim.Presentation
{
    /// <summary>
    /// The frame's cards around the board: the challenge (the award, the game's own name, its rules
    /// and the attempt's terms), the clock, the band over the board, the controls legend under it,
    /// the competitors and the controls down the right.
    /// </summary>
    public sealed partial class CompetitionGameScreen
    {
        private RectTransform challengeCard, timerCard, fieldCard, footerCard, boardHeader, boardChips, feedbackPill, legendRoot;
        private RectTransform categoryChip, modeChip, youRow;
        private TMP_Text challengeEyebrow, challengeTitle, rulesLabel, policyLabel, timerEyebrow, clock, clockState, status;
        private TMP_Text feedback, fieldHeading, entrantsLabel, arenaStatus, pauseHint, cancelHint;
        private Image challengeMark, policyMark, clockRing, clockTrack, timerGlow;
        private readonly List<RectTransform> entrantFaces = new List<RectTransform>();
        private IList<CompetitionEntrant> fieldEntrants;
        private string feedbackShown;
        private bool usingPad;
        private float clockPulse;
        private int lastWholeSecond = -1;

        // ---------------------------------------------------------------- the challenge

        private void BuildChallenge(string title, bool practice)
        {
            string game = GameName;
            string award = AwardOf(title, game);
            challengeCard = Card("Competition challenge", panel);
            challengeMark = HudPrimitives.Glyph("Challenge mark", challengeCard, award.ToUpperInvariant().Contains("VETO") ? "veto-token" : "crown",
                UiTheme.Gold, new Vector2(16, -14), 20);
            challengeEyebrow = Label("Challenge eyebrow", challengeCard, award.ToUpperInvariant(), 12, 0, 0, 10, 10, UiTheme.Gold);
            challengeEyebrow.characterSpacing = 6f; challengeEyebrow.textWrappingMode = TextWrappingModes.NoWrap; Fit(challengeEyebrow, 9);
            categoryChip = SizedChip("Competition category", challengeCard, CategoryOf().ToUpperInvariant(), UiTheme.Heading, false);
            modeChip = SizedChip("Attempt mode", challengeCard, practice ? "PRACTICE" : "RANKED", practice ? UiTheme.Muted : UiTheme.Accent, !practice);
            // The game's own name, the same one the briefing gave it; the award is the eyebrow.
            challengeTitle = Label("Competition title", challengeCard, game, 26, 0, 0, 10, 10, UiTheme.Paper);
            var bold = UiTheme.Font(UiTheme.Weight.Bold); if (bold != null) challengeTitle.font = bold;
            challengeTitle.textWrappingMode = TextWrappingModes.NoWrap; Fit(challengeTitle, 14);
            rulesLabel = Label("Rules", challengeCard, RulesText(), 14, 0, 0, 10, 10, new Color(UiTheme.Paper.r, UiTheme.Paper.g, UiTheme.Paper.b, .9f));
            rulesLabel.alignment = TextAlignmentOptions.TopLeft; Fit(rulesLabel, 12);
            policyMark = Picture("Policy mark", challengeCard, UiTheme.Pack(PackArt.KitIconInfo), UiTheme.Muted);
            policyLabel = Label("Attempt policy", challengeCard, practice
                ? CompetitionWords.PracticePolicy
                : "Cancel or reload returns to this same ranked board. Scores commit once, after play ends.",
                12, 0, 0, 10, 10, UiTheme.Muted);
            policyLabel.alignment = TextAlignmentOptions.TopLeft; Fit(policyLabel, 10);
        }

        /// <summary>The game's own name: the authored title, or the game's instruction before titles.</summary>
        private string GameName => run.Definition?.Title ?? CompetitionMiniGames.DisplayName(run.Kind);

        /// <summary>
        /// The award, from the title the director passes ("Head of Household · Pressure Cooker"):
        /// the part before the separator. A title that is only the game's name says what it is.
        /// </summary>
        private static string AwardOf(string title, string game)
        {
            if (string.IsNullOrEmpty(title)) return "Competition";
            int split = title.IndexOf(" · ", StringComparison.Ordinal);
            if (split > 0) return title.Substring(0, split);
            return title == game ? "Competition" : title;
        }

        private string CategoryOf()
        {
            if (run.Definition != null) return run.Definition.Category;
            return CompetitionMiniGames.CategoryOf(run.Kind) ?? "Skill";
        }

        /// <summary>The rules line's sentence for a names board filled out with house words, which the names come before.</summary>
        public const string HouseWordsFollowTheNames = "Fewer than six of this season's names are long enough to scramble, so house words follow them.";

        /// <summary>
        /// The rules the game runs, on the surface it is played on: the authored summary, and for an
        /// authored competition the controls and costs the summary does not state. Every sentence is
        /// the game's own - they restate the version-2 briefs and the score formulas.
        /// </summary>
        private string RulesText()
        {
            string rules = run.Definition?.Summary ?? CompetitionMiniGames.Brief(run.Kind, run.RulesVersion);
            if (run.Definition == null) return rules;
            switch (run.Kind)
            {
                case CompetitionMiniGames.Kind.Memory:
                    return rules + " Arrows / D-pad move; Enter / A flips. Each mistake costs 0.15; clearing early adds up to one point.";
                case CompetitionMiniGames.Kind.Endurance:
                    return rules + " Holding spends grip; releasing recovers it. An empty grip ends the attempt. Space / right trigger hold; the button toggles.";
                case CompetitionMiniGames.Kind.Reaction:
                    return rules + " Click the target, or press its direction (arrow keys, WASD, D-pad or left stick). Early input and incorrect aim cost accuracy.";
                case CompetitionMiniGames.Kind.Dice:
                    return rules + " Arrows / D-pad choose Roll or Keep; Enter / A presses it. The dice land one after another.";
                case CompetitionMiniGames.Kind.Words:
                    // A names board that is filled out says so: the rule says names, and a house
                    // word under it with no warning read as the wrong game (UI-UX-PASS-PLAN M0).
                    return rules + (run.DealsHouseWords ? " " + HouseWordsFollowTheNames : string.Empty)
                        + " Click, type or press A on a letter; Backspace / X takes one back; a wrong spelling clears. "
                        + "Typing spells, so P is a letter here: Start or the Pause button pauses.";
                default: return rules;
            }
        }

        /// <summary>How tall the header row has to be for this width and frame at this text size.</summary>
        private float HeaderFor(float challengeWidth, float frameHeight)
        {
            float inner = challengeWidth - 32f;
            float rules = PreferredHeight(rulesLabel, inner);
            float policy = PreferredHeight(policyLabel, inner - 22f);
            float challenge = 14f + 18f * FontScale + 6f + 34f * FontScale + 4f + rules + 8f + policy + 12f;
            float timer = 44f + 126f * FontScale;
            float header = Mathf.Clamp(Mathf.Max(HeaderHeight, challenge, timer), HeaderHeight, 300f);
            // A final part's tracker and scoring line take a little more of the frame, out of the
            // board its game can spare.
            return FinalPartHeaderFor(header, challenge, timer, inner, frameHeight);
        }

        private static float PreferredHeight(TMP_Text label, float width)
        {
            if (label == null) return 0f;
            label.fontSize = label.fontSizeMax;
            return Mathf.Ceil(label.GetPreferredValues(label.text, Mathf.Max(40f, width), 0f).y) + 2f;
        }

        private void LayoutChallenge(float x, float width, float height)
        {
            challengeCard.anchoredPosition = new Vector2(x, -FrameEdge); challengeCard.sizeDelta = new Vector2(width, height);
            float inner = width - 32f, line = 18f * FontScale;
            float chipsWidth = modeChip.sizeDelta.x + 8f + categoryChip.sizeDelta.x;
            modeChip.anchorMin = modeChip.anchorMax = modeChip.pivot = new Vector2(1, 1);
            modeChip.anchoredPosition = new Vector2(-14f, -12f);
            categoryChip.anchorMin = categoryChip.anchorMax = categoryChip.pivot = new Vector2(1, 1);
            categoryChip.anchoredPosition = new Vector2(-14f - modeChip.sizeDelta.x - 8f, -12f);
            float eyebrowX = challengeMark != null ? 44f : 16f;
            Place(challengeEyebrow.rectTransform, eyebrowX, 14f, Mathf.Max(60f, width - eyebrowX - chipsWidth - 30f), line);
            float titleY = 14f + line + 6f;
            Place(challengeTitle.rectTransform, 16f, titleY, inner, 34f * FontScale);
            float rulesY = titleY + 34f * FontScale + 4f;
            float policy = PreferredHeight(policyLabel, inner - 22f);
            float policyY = height - 12f - policy;
            // A final part's tracker sits under the title and its scoring line over the policy.
            var (top, bottom) = LayoutFinalPartChallenge(inner, rulesY, policyY);
            Place(rulesLabel.rectTransform, 16f, top, inner, Mathf.Max(line, bottom - top));
            if (policyMark != null) Place(policyMark.rectTransform, 16f, policyY + 1f, 14f * FontScale, 14f * FontScale);
            Place(policyLabel.rectTransform, 16f + 22f, policyY, inner - 22f, policy);
        }

        /// <summary>A chip as wide as its word: the helper takes a width, and a fixed one clipped "ENDURANCE".</summary>
        private RectTransform SizedChip(string name, Transform parent, string text, Color tint, bool filled)
        {
            float height = 22f * FontScale;
            var chip = HudPrimitives.Chip(name, parent, text, tint, 80f, height, filled);
            var word = chip.GetComponentInChildren<TMP_Text>();
            float width = word != null ? word.GetPreferredValues(text).x + 22f : 80f;
            if (word != null) word.characterSpacing = 2f;
            chip.sizeDelta = new Vector2(Mathf.Max(56f, width + 6f), height);
            return chip;
        }

        // ---------------------------------------------------------------- the clock

        private void BuildTimer()
        {
            timerCard = Card("Competition timer", panel);
            UiTheme.AddGlow(timerCard, UiTheme.GlassRadius);
            var glow = timerCard.Find("Glow");
            timerGlow = glow != null ? glow.GetComponent<Image>() : null;
            timerEyebrow = Label("Timer eyebrow", timerCard, "TIME REMAINING", 12, 0, 0, 10, 10, UiTheme.Accent);
            timerEyebrow.characterSpacing = 6f; timerEyebrow.textWrappingMode = TextWrappingModes.NoWrap; Fit(timerEyebrow, 9);
            // The clock is a ring that drains with the attempt, and the seconds beside it.
            clockTrack = Picture("Clock ring track", timerCard, UiTheme.Ring(), UiTheme.Outline, false);
            clockRing = Picture("Clock ring", timerCard, UiTheme.Ring(), UiTheme.Glow, false);
            clockRing.type = Image.Type.Filled; clockRing.fillMethod = Image.FillMethod.Radial360;
            clockRing.fillOrigin = (int)Image.Origin360.Top; clockRing.fillClockwise = false; clockRing.fillAmount = 1f;
            clock = Label("Competition clock", timerCard, "", 40, 0, 0, 10, 10, UiTheme.Paper);
            var bold = UiTheme.Font(UiTheme.Weight.Bold); if (bold != null) clock.font = bold;
            // Fixed-width digits, so the number does not shimmy as it counts: the one markup this
            // label carries, and none of it is copy.
            clock.richText = true; clock.alignment = TextAlignmentOptions.Right;
            clock.textWrappingMode = TextWrappingModes.NoWrap; Fit(clock, 18);
            clockState = Label("Clock state", timerCard, "", 11, 0, 0, 10, 10, UiTheme.Muted);
            clockState.characterSpacing = FontScale > 1f ? 1f : 3f; clockState.alignment = TextAlignmentOptions.Right;
            clockState.textWrappingMode = TextWrappingModes.NoWrap; Fit(clockState, 8);
            status = Label("Progress", timerCard, "", 14, 0, 0, 10, 10, UiTheme.Glow);
            status.alignment = TextAlignmentOptions.TopLeft; Fit(status, 10);
        }

        private void LayoutTimer(float x, float height)
        {
            timerCard.anchoredPosition = new Vector2(x, -FrameEdge); timerCard.sizeDelta = new Vector2(TimerWidth, height);
            float line = 18f * FontScale, ringTop = 14f + line + 10f, ring = 64f * FontScale;
            Place(timerEyebrow.rectTransform, 16f, 14f, TimerWidth - 32f, line);
            Place(clockTrack.rectTransform, 16f, ringTop, ring, ring);
            Place(clockRing.rectTransform, 16f, ringTop, ring, ring);
            float digitsX = 16f + ring + 12f, digitsWidth = TimerWidth - digitsX - 16f;
            Place(clock.rectTransform, digitsX, ringTop, digitsWidth, 52f * FontScale);
            Place(clockState.rectTransform, digitsX, ringTop + 52f * FontScale, digitsWidth, 16f * FontScale);
            float progressY = ringTop + Mathf.Max(ring, 68f * FontScale) + 8f;
            Place(status.rectTransform, 16f, progressY, TimerWidth - 32f, Mathf.Max(line, height - progressY - 10f));
        }

        private void RefreshTimer()
        {
            if (clock == null) return;
            bool finished = run.Finished || FinishShowing;
            string digits, state;
            bool final = false;
            if (finished) { digits = run.Remaining.ToString("0.0"); state = "FINISHED"; }
            else if (playing)
            {
                digits = run.Remaining.ToString("0.0");
                final = run.Remaining <= 5.0 + 1e-9;
                state = Paused ? "CLOCK STOPPED" : final ? "FINAL SECONDS" : "SECONDS LEFT";
            }
            else if (Paused) { digits = run.TimeLimit.ToString("0"); state = "CLOCK STOPPED"; }
            else if (held) { digits = run.TimeLimit.ToString("0"); state = "WAITING FOR THE FIELD"; }
            else if (previewStarted) { digits = run.TimeLimit.ToString("0"); state = "MEMORIZE · " + Mathf.Max(1, Mathf.CeilToInt(countdownLeft)); }
            else { digits = run.TimeLimit.ToString("0"); state = "STARTS IN " + Mathf.Max(1, Mathf.CeilToInt(countdownLeft)); }
            clock.text = FixedDigits(digits);
            clockState.text = state;
            var tone = final ? UiTheme.Danger : UiTheme.Paper;
            clock.color = tone;
            clockRing.color = final ? UiTheme.Danger : UiTheme.Glow;
            clockRing.fillAmount = (playing || finished) && run.TimeLimit > 0 ? Mathf.Clamp01((float)(run.Remaining / run.TimeLimit)) : 1f;
            if (timerGlow != null)
            {
                var glow = final ? UiTheme.Danger : UiTheme.Glow;
                timerGlow.color = new Color(glow.r, glow.g, glow.b, final ? .55f : .35f);
            }
        }

        /// <summary>
        /// The digits at a fixed width so the number does not shimmy as it counts; the point
        /// between them keeps its own narrow width.
        /// </summary>
        private static string FixedDigits(string digits)
        {
            int point = digits.IndexOf('.');
            if (point < 0) return "<mspace=0.6em>" + digits + "</mspace>";
            return "<mspace=0.6em>" + digits.Substring(0, point) + "</mspace>." + "<mspace=0.6em>" + digits.Substring(point + 1) + "</mspace>";
        }

        /// <summary>The last seconds tick, and the digits pulse on each one (not under reduced motion).</summary>
        private void AdvanceClockBeats(float delta)
        {
            if (!playing || run == null || run.Finished) return;
            int whole = Mathf.CeilToInt((float)run.Remaining);
            if (whole >= 1 && whole <= 5 && whole != lastWholeSecond)
            {
                lastWholeSecond = whole; Cue(HouseAudio.Cue.Hover);
                clockPulse = ReducedMotion ? 0f : .25f;
            }
            if (clockPulse > 0f) clockPulse = Mathf.Max(0f, clockPulse - Mathf.Max(0f, delta));
            float scale = 1f + .06f * Mathf.Clamp01(clockPulse / .25f);
            clock.rectTransform.localScale = new Vector3(scale, scale, 1f);
        }

        // ---------------------------------------------------------------- the band over the board

        private void BuildBoardHeader()
        {
            boardHeader = HudPrimitives.Fill("Board header", playArea, new Color(UiTheme.Ink.r, UiTheme.Ink.g, UiTheme.Ink.b, .55f), 12);
            boardChips = new GameObject("Board chips", typeof(RectTransform)).GetComponent<RectTransform>();
            boardChips.SetParent(boardHeader, false);
            feedbackPill = HudPrimitives.Fill("Feedback pill", boardHeader, new Color(UiTheme.Ink.r, UiTheme.Ink.g, UiTheme.Ink.b, .85f), 14);
            UiTheme.PackSliced(feedbackPill.GetComponent<Image>(), PackArt.KitPillFill, 14f, new Color(UiTheme.Ink.r, UiTheme.Ink.g, UiTheme.Ink.b, .85f));
            UiTheme.AddBorder(feedbackPill, 14, UiTheme.Outline);
            feedbackPill.anchorMin = feedbackPill.anchorMax = feedbackPill.pivot = new Vector2(1, 1);
            feedback = HudPrimitives.Label("Attempt feedback", feedbackPill, 17f * FontScale, UiTheme.Paper, TextAlignmentOptions.Center);
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold); if (semibold != null) feedback.font = semibold;
            feedback.textWrappingMode = TextWrappingModes.NoWrap; Fit(feedback, 11);
            Stretch(feedback.rectTransform, 16f, 2f, 16f, 2f);
            feedbackPill.gameObject.SetActive(false);
            feedbackShown = null;
        }

        private void LayoutBoardHeader()
        {
            Place(boardHeader, 1f, 1f, surfaceWidth - 2f, BandHeight - 1f);
            Place(boardChips, 12f, 0f, surfaceWidth * .56f, BandHeight - 1f);
            feedbackShown = null;
            SetFeedback(feedback != null ? feedback.text : "");
        }

        /// <summary>
        /// What just happened, in its own colour: a hit or a pair in green, a miss in red, an early
        /// press in amber. Only the live message - the key hints are the legend's.
        /// </summary>
        private void SetFeedback(string text)
        {
            if (feedback == null) return;
            text = text ?? "";
            if (text == feedbackShown) return;
            feedbackShown = text;
            feedback.text = text;
            feedbackPill.gameObject.SetActive(text.Length > 0);
            if (text.Length == 0) return;
            feedback.color = ToneOf(text);
            feedback.fontSize = feedback.fontSizeMax;
            float height = BandHeight - 12f;
            float width = Mathf.Min(feedback.GetPreferredValues(text).x + 36f, surfaceWidth * .44f);
            feedbackPill.anchoredPosition = new Vector2(-8f, -6f);
            feedbackPill.sizeDelta = new Vector2(width, height);
        }

        private static Color ToneOf(string text)
        {
            if (text.StartsWith("Hit", StringComparison.Ordinal) || text.StartsWith("Pair found", StringComparison.Ordinal)
                || text.StartsWith("Solved", StringComparison.Ordinal)) return UiTheme.Positive;
            if (text.StartsWith("Missed", StringComparison.Ordinal) || text.StartsWith("No match", StringComparison.Ordinal)
                || text.StartsWith("Not quite", StringComparison.Ordinal)) return UiTheme.Danger;
            if (text.StartsWith("Too early", StringComparison.Ordinal)) return UiTheme.Warning;
            return UiTheme.Paper;
        }

        private void RefreshBoardEdge()
        {
            var edge = playArea != null ? playArea.Find("Border") : null;
            var image = edge != null ? edge.GetComponent<Image>() : null;
            if (image == null) return;
            bool live = IsPlaying;
            bool aiming = live && run.Kind == CompetitionMiniGames.Kind.Reaction && UnityEngine.EventSystems.EventSystem.current != null
                && reactionFocus != null && UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject == reactionFocus.gameObject;
            image.color = UiTheme.Edge(aiming ? UiTheme.Emphasis.Active : live ? UiTheme.Emphasis.Interactive : UiTheme.Emphasis.Resting);
        }

        // ---------------------------------------------------------------- the legend

        private enum LegendAction { Pause, Leave }

        /// <summary>The key that does <paramref name="action"/> on the device last used.</summary>
        private string LegendKey(LegendAction action) => action == LegendAction.Pause
            ? (usingPad ? "Start" : "P") : (usingPad ? "B" : "Esc");

        private void BuildLegend()
        {
            legendRoot = new GameObject("Control legend", typeof(RectTransform)).GetComponent<RectTransform>();
            legendRoot.SetParent(panel, false);
            FillLegend();
        }

        private IEnumerable<(string key, string action)> LegendEntries()
        {
            switch (run.Kind)
            {
                case CompetitionMiniGames.Kind.Memory:
                    yield return usingPad ? ("D-pad", "Move") : ("Arrows", "Move");
                    yield return usingPad ? ("A", "Flip") : ("Enter", "Flip");
                    break;
                case CompetitionMiniGames.Kind.Reaction:
                    if (run.RulesVersion < CompetitionMiniGames.ImprovedRules)
                        // Version 1 has no aim: any direction, or Space, hits the target that is up.
                        yield return usingPad ? ("D-pad / Stick", "Hit") : ("Space / Arrows", "Hit");
                    else yield return usingPad ? ("D-pad / Stick", "Aim") : ("Arrows / WASD", "Aim");
                    if (!usingPad) yield return ("Click", "Hit the target");
                    break;
                case CompetitionMiniGames.Kind.Endurance:
                    yield return usingPad ? ("RT", "Hold") : ("Space", "Hold");
                    yield return usingPad ? ("A", "Toggle grip") : ("Enter", "Toggle grip");
                    break;
                case CompetitionMiniGames.Kind.Dice:
                    yield return usingPad ? ("D-pad", "Roll / Keep") : ("Arrows", "Roll / Keep");
                    yield return usingPad ? ("A", "Press") : ("Enter", "Press");
                    break;
                case CompetitionMiniGames.Kind.Words:
                    yield return usingPad ? ("A", "Choose letter") : ("A–Z", "Spell");
                    yield return usingPad ? ("X", "Take back") : ("Backspace", "Take back");
                    break;
            }
            yield return usingPad ? ("LB / RB", "Controls") : ("Tab", "Controls");
            // Typing spells on the word board, so its keyboard pause is the Pause button alone.
            if (!(WordsBoard && !usingPad)) yield return (LegendKey(LegendAction.Pause), "Pause");
            yield return (LegendKey(LegendAction.Leave), LeavingAsks ? "Briefing (asks first)" : "Briefing");
        }

        private void FillLegend()
        {
            if (legendRoot == null) return;
            foreach (Transform child in legendRoot) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            float height = LegendHeight * FontScale, x = 0f;
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            foreach (var (key, action) in LegendEntries())
            {
                var cap = HudPrimitives.Fill("Key cap", legendRoot, UiTheme.SurfaceRaised, 6);
                UiTheme.AddBorder(cap, 6, UiTheme.Outline);
                var word = HudPrimitives.Label("Key", cap, 13f * FontScale, UiTheme.Paper, TextAlignmentOptions.Center);
                word.text = key; word.textWrappingMode = TextWrappingModes.NoWrap; if (semibold != null) word.font = semibold;
                float capWidth = word.GetPreferredValues(key).x + 16f;
                Place(cap, x, 3f, capWidth, height - 6f);
                Stretch(word.rectTransform, 4f, 0f, 4f, 0f);
                x += capWidth + 6f;
                var what = HudPrimitives.Label("Key action", legendRoot, 13f * FontScale, UiTheme.Muted);
                what.text = action; what.textWrappingMode = TextWrappingModes.NoWrap;
                float whatWidth = what.GetPreferredValues(action).x + 4f;
                Place(what.rectTransform, x, 0f, whatWidth, height);
                what.alignment = TextAlignmentOptions.MidlineLeft;
                x += whatWidth + 18f;
            }
            legendWidth = x;
            FitLegend();
        }

        private float legendWidth, legendRoom = float.MaxValue;

        private void LayoutLegend(float x, float y, float width, float height)
        {
            // A final part's gold band takes the row's right-hand end; the keys keep the rest.
            float room = LayoutFinalBand(x, y, width, height);
            Place(legendRoot, x, y, room, height);
            legendRoom = room;
            FitLegend();
        }

        // One row, never wrapped: on a frame too narrow for it the row is drawn smaller as a whole.
        private void FitLegend()
        {
            float scale = legendWidth > legendRoom && legendWidth > 0f ? legendRoom / legendWidth : 1f;
            legendRoot.localScale = new Vector3(scale, scale, 1f);
        }

        /// <summary>
        /// The legend and the key hints follow the device last used: a pad press turns the caps into
        /// the pad's buttons, a key or a click turns them back.
        /// </summary>
        private void NoteInputDevice(Keyboard keyboard, Gamepad pad)
        {
            bool padNow = usingPad;
            if (pad != null && PadPressed(pad)) padNow = true;
            else if ((keyboard != null && keyboard.anyKey.wasPressedThisFrame)
                || (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)) padNow = false;
            if (padNow == usingPad) return;
            usingPad = padNow;
            FillLegend();
            RefreshKeyHints();
        }

        private static bool PadPressed(Gamepad pad) =>
            pad.buttonSouth.wasPressedThisFrame || pad.buttonEast.wasPressedThisFrame || pad.buttonWest.wasPressedThisFrame
            || pad.buttonNorth.wasPressedThisFrame || pad.startButton.wasPressedThisFrame || pad.selectButton.wasPressedThisFrame
            || pad.leftShoulder.wasPressedThisFrame || pad.rightShoulder.wasPressedThisFrame
            || pad.leftTrigger.wasPressedThisFrame || pad.rightTrigger.wasPressedThisFrame
            || pad.dpad.up.wasPressedThisFrame || pad.dpad.down.wasPressedThisFrame
            || pad.dpad.left.wasPressedThisFrame || pad.dpad.right.wasPressedThisFrame;

        // ---------------------------------------------------------------- the field

        private void BuildField(string field, IList<CompetitionEntrant> entrants)
        {
            fieldEntrants = entrants;
            entrantFaces.Clear();
            fieldCard = Card("Competition field card", panel, true);
            fieldHeading = Label("Field heading", fieldCard, "COMPETITORS", 12, 0, 0, 10, 10, UiTheme.Accent);
            fieldHeading.characterSpacing = 6f; fieldHeading.textWrappingMode = TextWrappingModes.NoWrap;
            youRow = null;
            competitorCards.Clear();
            // A final part's pair are cards of their own (MOCKUP-PASS-PLAN M13). Keyed on the bracket
            // as well as the count: a sit-out can leave an ordinary week's field at two, and that
            // field keeps its list and faces as it always had them.
            bool cards = finalBracket != null && entrants != null && entrants.Count == 2;
            if (cards) BuildCompetitorCards(entrants);
            else if (entrants != null && entrants.Count > 0)
            {
                youRow = HudPrimitives.Fill("You row", fieldCard, new Color(UiTheme.Accent.r, UiTheme.Accent.g, UiTheme.Accent.b, .10f), 8);
                youRow.gameObject.SetActive(false);
            }
            // Listed from the top of the card, under its heading, not centred in the box's middle.
            entrantsLabel = Label("Competition field", fieldCard, field, 15, 0, 0, 10, 10, UiTheme.Paper);
            entrantsLabel.alignment = TextAlignmentOptions.TopLeft;
            entrantsLabel.textWrappingMode = TextWrappingModes.NoWrap;
            entrantsLabel.overflowMode = TextOverflowModes.Ellipsis;
            if (cards)
            {
                // The cards carry the names in the list's own words, a line to a card.
                var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
                if (semibold != null) entrantsLabel.font = semibold;
            }
            else if (entrants != null)
                foreach (var entrant in entrants)
                {
                    var face = HudPrimitives.Portrait(fieldCard, entrant.Portrait, entrant.IsPlayer ? UiTheme.Accent : UiTheme.Outline,
                        26f * FontScale, 2f, false, entrant.Character);
                    face.name = "Entrant face";
                    entrantFaces.Add(face);
                }
            arenaStatus = Label("Arena status", fieldCard, "", 12, 0, 0, 10, 10, UiTheme.Muted);
            Fit(arenaStatus, 9);
        }

        private void LayoutField(float fieldHeight)
        {
            fieldCard.anchoredPosition = new Vector2(-FrameEdge, -FrameEdge); fieldCard.sizeDelta = new Vector2(SideWidth, fieldHeight);
            float line = 18f * FontScale;
            Place(fieldHeading.rectTransform, 16f, 14f, SideWidth - 32f, line);
            if (competitorCards.Count > 0) { LayoutCompetitorCards(fieldHeight); return; }
            bool faces = entrantFaces.Count > 0;
            float listTop = 14f + line + 12f, box = fieldHeight - listTop - 70f;
            int rows = Mathf.Max(1, faces ? entrantFaces.Count : entrantsLabel.text.Split('\n').Length);
            // One houseguest a row, every houseguest listed: the rows close up to fit a big field,
            // and past that the words and the faces shrink with them.
            float pitch = Mathf.Min(faces ? 40f * FontScale : 26f * FontScale, box / rows);
            entrantsLabel.fontSize = Mathf.Min(15f * FontScale, pitch / 1.3f);
            float face = Mathf.Min(26f * FontScale, pitch - 6f);
            float textX = faces ? 16f + face + 4f + 12f : 16f;
            entrantsLabel.lineSpacing = Mathf.Max(0f, (pitch - entrantsLabel.fontSize * 1.25f) / entrantsLabel.fontSize * 100f);
            Place(entrantsLabel.rectTransform, textX, listTop, SideWidth - textX - 14f, fieldHeight - listTop - 70f);
            Place(arenaStatus.rectTransform, 16f, fieldHeight - 60f, SideWidth - 32f, 50f);
            if (!faces) return;
            entrantsLabel.ForceMeshUpdate();
            var info = entrantsLabel.textInfo;
            for (int i = 0; i < entrantFaces.Count; i++)
            {
                var rim = entrantFaces[i];
                bool shown = i < info.lineCount;
                rim.gameObject.SetActive(shown);
                if (!shown) continue;
                var row = info.lineInfo[i];
                float middle = -listTop + (row.ascender + row.descender) * .5f;
                rim.anchorMin = rim.anchorMax = new Vector2(0f, 1f); rim.pivot = new Vector2(.5f, .5f);
                rim.anchoredPosition = new Vector2(16f + face * .5f + 2f, middle);
                float ring = rim.sizeDelta.x - (rim.childCount > 0 ? ((RectTransform)rim.GetChild(0)).sizeDelta.x : rim.sizeDelta.x);
                rim.sizeDelta = new Vector2(face + ring, face + ring);
                if (rim.childCount > 0) ((RectTransform)rim.GetChild(0)).sizeDelta = new Vector2(face, face);
                if (fieldEntrants[i].IsPlayer && youRow != null)
                {
                    youRow.gameObject.SetActive(true);
                    youRow.anchorMin = youRow.anchorMax = new Vector2(0f, 1f); youRow.pivot = new Vector2(0f, .5f);
                    youRow.anchoredPosition = new Vector2(8f, middle); youRow.sizeDelta = new Vector2(SideWidth - 16f, pitch - 4f);
                }
            }
        }

        // ---------------------------------------------------------------- the controls

        private void BuildFooter()
        {
            // The controls, in their own card under the field: nothing in the frame is a big blue slab.
            footerCard = Card("Competition controls", panel, true);
            float buttonWidth = SideWidth - 24f;
            pause = Button("Pause competition", footerCard, "Pause", 12, 14, buttonWidth, 44, TogglePause);
            // Back to briefing asks first in a ranked attempt under way, as Esc does: the button is
            // one Up away from the top row of the board.
            cancel = Button("Cancel attempt", footerCard, "Back to briefing", 12, 66, buttonWidth, 44, LeaveRequested);
            finishAction = Button("Competition result action", footerCard, "Continue", 12, 118, buttonWidth, 44, () => { });
            finishAction.gameObject.SetActive(false);
            Secondary(pause); Secondary(cancel);
            UiTheme.PackSliced(finishAction.GetComponent<Image>(), PackArt.ButtonPrimary, 16f);
            Focusable(pause); Focusable(cancel); Focusable(finishAction);
            // Which key does it, beside the caption rather than in it: the caption is the contract.
            pauseHint = KeyHint(pause); cancelHint = KeyHint(cancel);
            RefreshKeyHints();
        }

        private TMP_Text KeyHint(Button button)
        {
            var caption = button.GetComponentInChildren<TMP_Text>();
            var hint = HudPrimitives.Label("Key hint", button.transform, 12f * FontScale, UiTheme.Muted, TextAlignmentOptions.MidlineRight);
            hint.textWrappingMode = TextWrappingModes.NoWrap;
            var rect = hint.rectTransform;
            rect.anchorMin = new Vector2(1f, 0f); rect.anchorMax = new Vector2(1f, 1f); rect.pivot = new Vector2(1f, .5f);
            rect.anchoredPosition = new Vector2(-12f, 0f); rect.sizeDelta = new Vector2(48f, 0f);
            if (caption != null)
            {
                // Centred in what the hint leaves.
                var box = caption.rectTransform; box.sizeDelta = new Vector2(box.sizeDelta.x - 44f, box.sizeDelta.y);
            }
            return hint;
        }

        private void RefreshKeyHints()
        {
            if (pauseHint != null) pauseHint.text = LegendKey(LegendAction.Pause);
            if (cancelHint != null) cancelHint.text = LegendKey(LegendAction.Leave);
        }

        private void LayoutFooter(float frameHeight)
        {
            footerCard.anchoredPosition = new Vector2(-FrameEdge, -(frameHeight - FrameEdge - ControlsHeight));
            footerCard.sizeDelta = new Vector2(SideWidth, ControlsHeight);
        }
    }
}
