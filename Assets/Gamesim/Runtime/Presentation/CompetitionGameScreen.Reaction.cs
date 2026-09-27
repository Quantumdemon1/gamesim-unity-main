using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Gamesim.Presentation
{
    /// <summary>
    /// The reaction board: a round target with its direction, a window that drains, and a mark
    /// on the board for every press - hit, wrong way, off the target, too late, too early.
    ///
    /// <para>The field's faint diagonals are the rule made visible: a target's direction is which
    /// quarter of the field it lands in, and the four legend marks at the edges light the one that
    /// scores. The target's size follows the board, never the text size - a larger target at large
    /// text would be an easier game.</para>
    /// </summary>
    public sealed partial class CompetitionGameScreen
    {
        private const float TargetDiameter = 124f, MarkSeconds = .5f;
        private static readonly MiniGameRun.Direction[] Directions =
            { MiniGameRun.Direction.Up, MiniGameRun.Direction.Right, MiniGameRun.Direction.Down, MiniGameRun.Direction.Left };

        private RectTransform target;
        private TMP_Text targetLabel;
        private Image targetWindow, targetArrow, targetRing;
        private CompetitionDirectionControl reactionFocus;
        private readonly RectTransform[] directionGuides = new RectTransform[2];
        private readonly RectTransform[] legendMarks = new RectTransform[4];
        private RectTransform aimChip, earlyEdge;
        private TMP_Text aimLabel;
        private readonly List<ReactionMark> marks = new List<ReactionMark>();
        private int shownHits, shownWrong, shownPointer, shownExpired, shownEarly;
        private Vector2 lastTargetCentre;
        private float earlyLeft;

        private sealed class ReactionMark
        {
            public RectTransform Root; public Image Ring, Icon; public float Left;
        }

        private void BuildReaction(Action tap, Action<MiniGameRun.Direction> direction, Action missedTarget)
        {
            var input = playArea.gameObject.AddComponent<CompetitionDirectionControl>();
            input.Pressed = direction; input.Missed = missedTarget;
            input.targetGraphic = playArea.GetComponent<Image>(); Untinted(input);
            reactionFocus = input;
            input.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnDown = pause, selectOnUp = cancel };
            playArea.GetComponent<Image>().raycastTarget = true;

            bool directional = run.RulesVersion >= CompetitionMiniGames.ImprovedRules;
            if (directional)
            {
                for (int i = 0; i < 2; i++)
                {
                    directionGuides[i] = HudPrimitives.Fill("Direction guide", playArea, new Color(UiTheme.Accent.r, UiTheme.Accent.g, UiTheme.Accent.b, .12f), 1);
                }
                for (int i = 0; i < 4; i++)
                {
                    var mark = new GameObject("Direction legend " + Directions[i], typeof(RectTransform)).GetComponent<RectTransform>();
                    mark.SetParent(playArea, false);
                    var arrow = Picture("Legend arrow", mark, UiTheme.PlayMark(), UiTheme.Muted);
                    arrow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, ArrowAngle(Directions[i]));
                    var word = HudPrimitives.Label("Legend word", mark, 12f * FontScale, UiTheme.Muted, TextAlignmentOptions.Center);
                    word.text = Directions[i].ToString().ToUpperInvariant(); word.characterSpacing = 4f;
                    word.textWrappingMode = TextWrappingModes.NoWrap;
                    legendMarks[i] = mark;
                }
            }
            // An early press lights the board's edge amber for a moment.
            earlyEdge = HudPrimitives.Fill("Reaction early", playArea, new Color(0f, 0f, 0f, 0f), 12);
            UiTheme.AddBorder(earlyEdge, 12, UiTheme.Warning);
            earlyEdge.gameObject.SetActive(false);

            // The target: a disc that takes clicks only inside its circle.
            var root = new GameObject("Reaction target", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            root.SetParent(playArea, false);
            var disc = root.GetComponent<Image>();
            disc.sprite = UiTheme.Circle(); disc.color = new Color(UiTheme.Ink.r, UiTheme.Ink.g, UiTheme.Ink.b, .92f);
            disc.raycastTarget = true; disc.alphaHitTestMinimumThreshold = .5f;
            var button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = disc; Untinted(button);
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(() =>
            {
                tap();
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(input.gameObject);
            });
            targetRing = Picture("Target ring", root, UiTheme.Ring(), new Color(UiTheme.Gold.r, UiTheme.Gold.g, UiTheme.Gold.b, .35f), false);
            Stretch(targetRing.rectTransform, 0f, 0f, 0f, 0f);
            targetWindow = Picture("Target window", root, UiTheme.Ring(), UiTheme.Gold, false);
            Stretch(targetWindow.rectTransform, 0f, 0f, 0f, 0f);
            targetWindow.type = Image.Type.Filled; targetWindow.fillMethod = Image.FillMethod.Radial360;
            targetWindow.fillOrigin = (int)Image.Origin360.Top; targetWindow.fillClockwise = false;
            targetArrow = Picture("Target arrow", root, UiTheme.PlayMark(), UiTheme.Paper);
            targetArrow.gameObject.SetActive(directional);
            targetLabel = HudPrimitives.Label("Label", root, 16f * FontScale, UiTheme.Paper, TextAlignmentOptions.Center);
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold); if (semibold != null) targetLabel.font = semibold;
            targetLabel.textWrappingMode = TextWrappingModes.NoWrap;
            targetLabel.rectTransform.SetAsLastSibling();
            target = root;
            target.gameObject.SetActive(false);

            // Whether the arrows are live: they are while the board has the keyboard.
            aimChip = HudPrimitives.Fill("Aim focus", boardChips, new Color(UiTheme.Glow.r, UiTheme.Glow.g, UiTheme.Glow.b, .14f), 12);
            UiTheme.AddBorder(aimChip, 12, new Color(UiTheme.Glow.r, UiTheme.Glow.g, UiTheme.Glow.b, .7f));
            aimLabel = HudPrimitives.Label("Aim state", aimChip, 12f * FontScale, UiTheme.Glow, TextAlignmentOptions.Center);
            aimLabel.characterSpacing = 3f; aimLabel.textWrappingMode = TextWrappingModes.NoWrap;
            Stretch(aimLabel.rectTransform, 8f, 0f, 8f, 0f);
            aimChip.gameObject.SetActive(directional);

            marks.Clear();
            shownHits = shownWrong = shownPointer = shownExpired = shownEarly = 0;
            earlyLeft = 0f;
        }

        private static float ArrowAngle(MiniGameRun.Direction direction)
        {
            switch (direction)
            {
                case MiniGameRun.Direction.Up: return 90f;
                case MiniGameRun.Direction.Down: return -90f;
                case MiniGameRun.Direction.Left: return 180f;
                default: return 0f;
            }
        }

        private float TargetSide => TargetDiameter * BoardScale;
        private float TargetInset => 16f * BoardScale;

        /// <summary>Where a target's centre can land: the field less the target and its margin.</summary>
        private Rect TargetCentreRange()
        {
            var field = PlayField;
            float half = TargetSide * .5f + TargetInset;
            return new Rect(half, field.y + half, Mathf.Max(1f, field.width - 2f * half), Mathf.Max(1f, field.height - 2f * half));
        }

        private void PlaceReaction()
        {
            var range = TargetCentreRange();
            var field = PlayField;
            float diagonal = Mathf.Sqrt(range.width * range.width + range.height * range.height);
            float angle = Mathf.Atan2(range.height, range.width) * Mathf.Rad2Deg;
            for (int i = 0; i < 2; i++)
            {
                var guide = directionGuides[i];
                if (guide == null) continue;
                guide.anchorMin = guide.anchorMax = new Vector2(0f, 1f); guide.pivot = new Vector2(.5f, .5f);
                guide.anchoredPosition = new Vector2(range.center.x, -range.center.y);
                guide.sizeDelta = new Vector2(diagonal, 2f);
                guide.localRotation = Quaternion.Euler(0f, 0f, i == 0 ? angle : -angle);
            }
            float markSide = 18f * FontScale, markBox = 64f * FontScale;
            for (int i = 0; i < 4; i++)
            {
                var mark = legendMarks[i];
                if (mark == null) continue;
                Vector2 centre;
                switch (Directions[i])
                {
                    case MiniGameRun.Direction.Up: centre = new Vector2(field.center.x, field.y + 22f * FontScale); break;
                    case MiniGameRun.Direction.Down: centre = new Vector2(field.center.x, field.yMax - 22f * FontScale); break;
                    case MiniGameRun.Direction.Left: centre = new Vector2(40f * FontScale, field.center.y); break;
                    default: centre = new Vector2(field.width - 40f * FontScale, field.center.y); break;
                }
                mark.anchorMin = mark.anchorMax = new Vector2(0f, 1f); mark.pivot = new Vector2(.5f, .5f);
                mark.anchoredPosition = new Vector2(centre.x, -centre.y); mark.sizeDelta = new Vector2(markBox, markBox * .6f);
                var arrow = (RectTransform)mark.Find("Legend arrow");
                arrow.anchorMin = arrow.anchorMax = new Vector2(.5f, 1f); arrow.pivot = new Vector2(.5f, .5f);
                arrow.anchoredPosition = new Vector2(0f, -markSide * .5f); arrow.sizeDelta = new Vector2(markSide, markSide);
                var word = (RectTransform)mark.Find("Legend word");
                word.anchorMin = new Vector2(0f, 0f); word.anchorMax = new Vector2(1f, 0f); word.pivot = new Vector2(.5f, 0f);
                word.anchoredPosition = Vector2.zero; word.sizeDelta = new Vector2(0f, 16f * FontScale);
            }
            Stretch(earlyEdge, 0f, 0f, 0f, 0f);
            float side = TargetSide;
            target.sizeDelta = new Vector2(side, side);
            var arrowRect = targetArrow.rectTransform;
            arrowRect.anchorMin = arrowRect.anchorMax = new Vector2(.5f, .5f); arrowRect.pivot = new Vector2(.5f, .5f);
            arrowRect.anchoredPosition = new Vector2(0f, side * .16f); arrowRect.sizeDelta = new Vector2(side * .3f, side * .3f);
            var label = targetLabel.rectTransform;
            label.anchorMin = label.anchorMax = new Vector2(.5f, .5f); label.pivot = new Vector2(.5f, .5f);
            label.anchoredPosition = new Vector2(0f, -side * .16f); label.sizeDelta = new Vector2(side * .8f, side * .42f);
            float chipWidth = 130f * FontScale, chipHeight = Mathf.Min(28f * FontScale, BandHeight - 14f);
            Place(aimChip, 0f, (BandHeight - chipHeight) * .5f, chipWidth, chipHeight);
        }

        private Vector2 TargetCentre()
        {
            var range = TargetCentreRange();
            return new Vector2(range.x + (float)run.TargetX * range.width, range.y + (1f - (float)run.TargetY) * range.height);
        }

        private void RefreshReaction()
        {
            // Accuracy over the targets already decided: the one still up is neither hit nor missed.
            int decided = run.Spawned - (run.TargetLive ? 1 : 0);
            status.text = run.Hits + " / " + run.Spawned + " hits  ·  " + run.FalseStarts + " early"
                + (decided + run.FalseStarts > 0
                    ? "\n" + (CompetitionMiniGames.ReactionScore(run.Hits, decided, run.FalseStarts, run.RulesVersion) * 10).ToString("0") + "% accuracy"
                    : "");
            bool live = playing && !Paused && run.TargetLive;
            target.gameObject.SetActive(live);
            bool directional = run.RulesVersion >= CompetitionMiniGames.ImprovedRules;
            if (run.TargetLive)
            {
                // The same normalised position on a board of any size; the target and its margin
                // grow with the board, so it keeps its share of the field.
                var centre = TargetCentre();
                target.anchorMin = target.anchorMax = new Vector2(0f, 1f); target.pivot = new Vector2(.5f, .5f);
                target.anchoredPosition = new Vector2(centre.x, -centre.y);
                lastTargetCentre = centre;
                targetLabel.text = (directional ? run.TargetDirection.ToString().ToUpperInvariant() : "HIT")
                    + (run.Definition != null ? "\n" + run.TargetWindowSeconds.ToString("0.00") + " s" : "");
                float remaining = (float)run.TargetRemaining, window = Mathf.Max(.01f, (float)run.TargetWindowSeconds);
                targetWindow.fillAmount = Mathf.Clamp01(remaining / window);
                targetWindow.color = remaining < .25f ? UiTheme.Warning : UiTheme.Gold;
                targetArrow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, ArrowAngle(run.TargetDirection));
            }
            for (int i = 0; i < 4; i++)
            {
                if (legendMarks[i] == null) continue;
                bool scoring = live && directional && run.TargetDirection == Directions[i];
                var tone = scoring ? UiTheme.Gold : new Color(UiTheme.Muted.r, UiTheme.Muted.g, UiTheme.Muted.b, .55f);
                legendMarks[i].Find("Legend arrow").GetComponent<Image>().color = tone;
                legendMarks[i].Find("Legend word").GetComponent<TMP_Text>().color = tone;
            }
            bool aiming = EventSystem.current != null && EventSystem.current.currentSelectedGameObject == reactionFocus.gameObject;
            aimLabel.text = aiming && IsPlaying ? (usingPad ? "D-PAD LIVE" : "ARROWS LIVE") : (usingPad ? "LB / RB TO AIM" : "TAB TO AIM");
            aimLabel.color = aiming && IsPlaying ? UiTheme.Glow : UiTheme.Muted;

            // A mark where each press landed, from what the run counted since the last look.
            string legacy = run.Hits > shownHits ? "Hit" : run.ExpiredTargets > shownExpired ? "Missed: target expired" : null;
            if (run.Hits > shownHits) Mark(lastTargetCentre, UiTheme.Positive, PackArt.KitIconCheck, 1f);
            if (run.WrongDirections > shownWrong) Mark(lastTargetCentre, UiTheme.Danger, PackArt.KitIconCross, 1f);
            if (run.PointerMisses > shownPointer) Mark(ClickPoint(), UiTheme.Danger, PackArt.KitIconCross, .45f);
            if (run.ExpiredTargets > shownExpired) Mark(lastTargetCentre, UiTheme.Muted, PackArt.KitIconClock, 1f);
            if (run.FalseStarts > shownEarly) { earlyLeft = MarkSeconds; earlyEdge.gameObject.SetActive(true); }
            shownHits = run.Hits; shownWrong = run.WrongDirections; shownPointer = run.PointerMisses;
            shownExpired = run.ExpiredTargets; shownEarly = run.FalseStarts;
            if (run.RulesVersion < CompetitionMiniGames.ImprovedRules) { if (legacy != null) SetFeedback(legacy); else if (!playing) SetFeedback(""); }
            else SetFeedback(playing || run.Finished ? run.Feedback : "");
        }

        /// <summary>Where the last click on the board landed, in the board's own units.</summary>
        private Vector2 ClickPoint()
        {
            if (reactionFocus == null || !reactionFocus.HasClick) return lastTargetCentre;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(playArea, reactionFocus.LastClick, reactionFocus.LastClickCamera, out var local))
                return lastTargetCentre;
            // The board's pivot is its top-left corner; the board's units run down from it.
            return new Vector2(local.x, -local.y);
        }

        private void Mark(Vector2 centre, Color tone, string icon, float size)
        {
            ReactionMark mark = null;
            foreach (var existing in marks) if (existing.Left <= 0f) { mark = existing; break; }
            if (mark == null)
            {
                var root = new GameObject("Reaction mark", typeof(RectTransform)).GetComponent<RectTransform>();
                root.SetParent(playArea, false);
                // Below the target, so a mark never covers the next one.
                root.SetSiblingIndex(target.GetSiblingIndex());
                mark = new ReactionMark { Root = root };
                mark.Ring = Picture("Mark ring", root, UiTheme.Ring(), tone, false);
                Stretch(mark.Ring.rectTransform, 0f, 0f, 0f, 0f);
                mark.Icon = Picture("Mark icon", root, null, tone);
                marks.Add(mark);
            }
            float side = TargetSide * size;
            mark.Root.anchorMin = mark.Root.anchorMax = new Vector2(0f, 1f); mark.Root.pivot = new Vector2(.5f, .5f);
            mark.Root.anchoredPosition = new Vector2(centre.x, -centre.y); mark.Root.sizeDelta = new Vector2(side, side);
            mark.Root.localScale = Vector3.one;
            mark.Ring.color = tone;
            mark.Icon.sprite = UiTheme.Pack(icon); mark.Icon.color = tone; mark.Icon.enabled = mark.Icon.sprite != null;
            Centre(mark.Icon.rectTransform, 0f, 0f, side * .42f);
            mark.Left = MarkSeconds;
            mark.Root.gameObject.SetActive(true);
        }

        private void AdvanceReactionBeats(float delta)
        {
            float step = Mathf.Max(0f, delta);
            foreach (var mark in marks)
            {
                if (mark.Left <= 0f) continue;
                mark.Left = Mathf.Max(0f, mark.Left - step);
                float alpha = ReducedMotion ? 1f : Mathf.Clamp01(mark.Left / MarkSeconds);
                SetAlpha(mark.Ring, alpha); SetAlpha(mark.Icon, alpha);
                if (!ReducedMotion) { float grow = 1f + .25f * (1f - mark.Left / MarkSeconds); mark.Root.localScale = new Vector3(grow, grow, 1f); }
                if (mark.Left <= 0f) mark.Root.gameObject.SetActive(false);
            }
            if (earlyLeft > 0f)
            {
                earlyLeft = Mathf.Max(0f, earlyLeft - step);
                if (earlyLeft <= 0f) earlyEdge.gameObject.SetActive(false);
            }
            // The window drains between the director's refreshes too.
            if (target != null && target.gameObject.activeSelf && run.TargetLive)
            {
                float remaining = (float)run.TargetRemaining, window = Mathf.Max(.01f, (float)run.TargetWindowSeconds);
                targetWindow.fillAmount = Mathf.Clamp01(remaining / window);
            }
        }

        private static void SetAlpha(Graphic graphic, float alpha)
        {
            var colour = graphic.color; colour.a = alpha; graphic.color = colour;
        }
    }
}
