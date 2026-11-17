using System;
using Gamesim.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace Gamesim.Episode
{
    /// <summary>
    /// The way back of a page reached from another page inside the docked panel - the pitches and a
    /// story step over the nominations, the finalists' comparison, the jury's questions, a
    /// conversation's topic: "Back to &lt;that page&gt;" at the panel's top left, drawn by the shared
    /// <see cref="WayOut.AddBack"/> so it sits and looks as every other way back does, with the
    /// panel's own Close at the top right. Escape and the pad's B take it too
    /// (<see cref="TakeDecisionPageBack"/>, from the director's back chain).
    ///
    /// <para>The phase band owns the panel's top: its glyph, title and line step right past the way
    /// back, and stop short of the Close. In a conversation the band is hidden and the way back sits
    /// at the top left of the conversation's column, over its scroll.</para>
    /// </summary>
    public sealed partial class EpisodeHud
    {
        /// <summary>The air between the way back and the band's words beside it, at the resting size.</summary>
        public const float DecisionBackGap = 12f;
        /// <summary>The band's height, which the way back sits inside at the resting text size.</summary>
        private const float DecisionBandHeight = 62f;
        /// <summary>The phase band's name (EpisodeHud.PhaseBand): the way back is drawn in it.</summary>
        public const string DecisionBandName = "Phase band";
        /// <summary>The narrowest the band's title is left beside a way back.</summary>
        private const float DecisionBandTitleFloor = 160f;
        /// <summary>The key the way back answers to, drawn beside its caption and never in it.</summary>
        public const string DecisionBackKey = InputGlossary.BackKeyHint;

        /// <summary>This render's page way back, or null when the page has none.</summary>
        private Button decisionPageBack;

        /// <summary>The page's way back as drawn now, or null: for tests and the back chain.</summary>
        public Button DecisionPageBackControl =>
            decisionPageBack != null && modal != null && decisionPageBack.transform.IsChildOf(modal) ? decisionPageBack : null;

        /// <summary>
        /// Draws the page's way back at the panel's top left. <paramref name="caption"/> is a
        /// "Back to &lt;page&gt;" made by <see cref="WayOut.BackTo"/> (anything else throws).
        /// </summary>
        public Button DecisionPageBack(string caption, Action action)
        {
            if (modal == null) return null;
            // One way back a render, whichever helper draws it (EpisodeHud.WayOut.cs): the first
            // drawn - the innermost page - keeps the corner.
            if (wayBack != null) return wayBack;
            var drawn = DecisionPageBackControl;
            if (drawn != null) return drawn;
            bool conversation = activityLayout == ActivityLayout.Conversation
                && conversationColumn != null && conversationColumn.gameObject.activeSelf;
            // In the band itself, which spans the panel's top: the way back is the band's, not one
            // of the panel's pinned controls, and the band's words step aside for it.
            var band = modal.Find(DecisionBandName) as RectTransform;
            var frame = conversation ? conversationColumn : band != null && band.gameObject.activeSelf ? band : modal;
            // Every way back has the shared name, and a rebuild puts the keyboard back on the control of
            // the name it was on: pressed, the way back lets the name go, so the page it lands on opens
            // on its own first control - not on that page's way back, where a second Enter would go
            // back again.
            var back = WayOut.AddBack(frame, caption, () =>
            {
                action?.Invoke();
                if (preferredSelection == WayOut.BackObjectName) preferredSelection = null;
            }, FontScale, DecisionBackKey);
            var rect = (RectTransform)back.transform;
            float scale = FontScale, inset = WayOut.Inset(scale);
            float right = inset + rect.sizeDelta.x + DecisionBackGap * scale;
            float foot = inset + rect.sizeDelta.y;
            if (conversation) KeepColumnClearOfBack(foot);
            else StepBandPastBack(right, foot, scale);
            decisionPageBack = back;
            // The key answers the way back now: its hint leaves the panel's Close.
            TakeKeyHintFromClose();
            return back;
        }

        /// <summary>
        /// Escape or the pad's B on a page with a way back: does what the control does. False when
        /// the page has none, it is not live, or an overlay is over the panel.
        /// </summary>
        public bool TakeDecisionPageBack()
        {
            var back = DecisionPageBackControl;
            if (back == null || !back.IsActive() || !back.IsInteractable() || ActiveOverlay() != null) return false;
            back.onClick.Invoke();
            return true;
        }

        /// <summary>The band's glyph, title and line move right of the way back and stop before the Close.</summary>
        private void StepBandPastBack(float right, float foot, float scale)
        {
            float panelWidth = modal.rect.width > 1f ? Mathf.Min(modal.rect.width, ModalWidth) : ModalWidth;
            float closeReserve = WayOut.Width(WayOut.CloseCaption, scale, false, DecisionBackKey) + 2f * WayOut.Inset(scale);
            float start = bandGlyph != null ? bandGlyph.rectTransform.anchoredPosition.x : bandTitle != null ? bandTitle.rectTransform.anchoredPosition.x : right;
            float shift = Mathf.Max(0f, right - start);
            if (bandGlyph != null) Shift(bandGlyph.rectTransform, shift, 0f);
            if (bandTitle != null) Shift(bandTitle.rectTransform, shift, panelWidth - closeReserve);
            if (bandLine != null) Shift(bandLine.rectTransform, shift, panelWidth - closeReserve);
            // At a larger text size the way back runs past the band into the hint's row: the hint steps too.
            if (foot > DecisionBandHeight)
            {
                var hint = modal.Find(PanelHintName) as RectTransform;
                if (hint != null && hint.anchoredPosition.x < right) Shift(hint, right - hint.anchoredPosition.x, panelWidth - closeReserve);
            }
        }

        /// <summary>Moves a band piece right by <paramref name="shift"/>, its right edge kept short of <paramref name="limit"/> (0: no limit).</summary>
        private static void Shift(RectTransform piece, float shift, float limit)
        {
            var at = piece.anchoredPosition;
            piece.anchoredPosition = new Vector2(at.x + shift, at.y);
            if (limit > 0f && piece.anchoredPosition.x + piece.sizeDelta.x > limit)
                piece.sizeDelta = new Vector2(Mathf.Max(DecisionBandTitleFloor, limit - piece.anchoredPosition.x), piece.sizeDelta.y);
        }

        /// <summary>The conversation's scroll starts under the way back, not behind it.</summary>
        private void KeepColumnClearOfBack(float foot)
        {
            if (modalScroll == null || !modalScroll.transform.IsChildOf(conversationColumn)) return;
            var scroll = (RectTransform)modalScroll.transform;
            float top = -scroll.offsetMax.y, need = foot + 6f;
            if (top < need) scroll.offsetMax = new Vector2(scroll.offsetMax.x, -need);
        }
    }
}
