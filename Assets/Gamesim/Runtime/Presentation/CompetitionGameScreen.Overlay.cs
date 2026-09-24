using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gamesim.Presentation
{
    /// <summary>
    /// What stands over the board when it is not being played: GET READY and what the start is
    /// waiting for, the count, GO, and PAUSED - each with its own size and plate, so one state's
    /// type never leaks into the next.
    ///
    /// <para>The pause plate is opaque. A paused board used to stay readable behind a half-tint,
    /// which on First Impressions meant a pause was a free, unlimited look at every card.</para>
    /// </summary>
    public sealed partial class CompetitionGameScreen
    {
        private enum OverlayState { None, Ready, Count, Go, Paused }

        private const float GoSeconds = .5f;
        private OverlayState overlay;
        private RectTransform overlayPlate, overlayCard;
        private TMP_Text overlayHeadline, overlayDetail;
        private float goLeft;

        private void BuildOverlay()
        {
            overlayPlate = HudPrimitives.Fill("Countdown plate", playArea, UiTheme.Ink, 12);
            overlayCard = HudPrimitives.Fill("Countdown card", playArea,
                new Color(UiTheme.GlassFill.r, UiTheme.GlassFill.g, UiTheme.GlassFill.b, .94f), UiTheme.GlassRadius);
            UiTheme.AddGlow(overlayCard, UiTheme.GlassRadius);
            UiTheme.AddBorder(overlayCard, UiTheme.GlassRadius, UiTheme.Edge(UiTheme.Emphasis.Interactive));
            overlayDetail = HudPrimitives.Label("Countdown detail", overlayCard, 16f * FontScale,
                new Color(UiTheme.Paper.r, UiTheme.Paper.g, UiTheme.Paper.b, .85f), TextAlignmentOptions.Top);
            Fit(overlayDetail, 12);
            overlayHeadline = HudPrimitives.Label("Countdown", playArea, 96f * FontScale, UiTheme.Gold, TextAlignmentOptions.Center);
            var bold = UiTheme.Font(UiTheme.Weight.Bold); if (bold != null) overlayHeadline.font = bold;
            overlayHeadline.textWrappingMode = TextWrappingModes.NoWrap;
            overlay = OverlayState.None; goLeft = 0f;
            overlayPlate.gameObject.SetActive(false); overlayCard.gameObject.SetActive(false); overlayHeadline.gameObject.SetActive(false);
        }

        private void ShowOverlay(OverlayState state, string detail)
        {
            if (overlayHeadline == null) return;
            overlay = state;
            bool plate = state == OverlayState.Ready || state == OverlayState.Count || state == OverlayState.Paused;
            overlayPlate.gameObject.SetActive(plate);
            overlayCard.gameObject.SetActive(plate);
            overlayHeadline.gameObject.SetActive(state != OverlayState.None);
            var ground = overlayPlate.GetComponent<Image>();
            // Dim enough to say "not yet", opaque while paused: nothing on a paused board can be read.
            float alpha = state == OverlayState.Paused ? 1f : state == OverlayState.Ready ? .55f : .30f;
            ground.color = new Color(UiTheme.Ink.r, UiTheme.Ink.g, UiTheme.Ink.b, alpha);
            // The plate takes clicks while paused so none reach the board under it; they bubble to
            // a board that is not interactable, so they do nothing.
            ground.raycastTarget = state == OverlayState.Paused;
            float size; Color colour; string text;
            switch (state)
            {
                case OverlayState.Ready: text = "GET READY"; size = 56f; colour = UiTheme.Paper; break;
                case OverlayState.Count: text = Mathf.Max(1, Mathf.CeilToInt(countdownLeft)).ToString(); size = 96f; colour = UiTheme.Gold; break;
                case OverlayState.Go: text = "GO"; size = 96f; colour = UiTheme.Positive; break;
                case OverlayState.Paused: text = "PAUSED"; size = 72f; colour = UiTheme.Gold; break;
                default: text = ""; size = 96f; colour = UiTheme.Gold; break;
            }
            overlayHeadline.text = text; overlayHeadline.color = colour;
            overlayHeadline.enableAutoSizing = false;
            overlayHeadline.fontSize = size * FontScale;
            if (state == OverlayState.Count && string.IsNullOrEmpty(detail)) detail = "The attempt clock starts on GO.";
            overlayDetail.text = detail ?? "";
            overlayDetail.gameObject.SetActive(plate && !string.IsNullOrEmpty(overlayDetail.text));
            if (state == OverlayState.Go)
            {
                goLeft = GoSeconds;
                float pop = ReducedMotion ? 1f : 1.25f;
                overlayHeadline.rectTransform.localScale = new Vector3(pop, pop, 1f);
            }
            else { goLeft = 0f; overlayHeadline.rectTransform.localScale = Vector3.one; }
            LayoutOverlay();
        }

        private void SetOverlayDetail(string detail)
        {
            if (overlayDetail == null) return;
            overlayDetail.text = detail ?? "";
            overlayDetail.gameObject.SetActive(overlayCard.gameObject.activeSelf && !string.IsNullOrEmpty(overlayDetail.text));
            LayoutOverlay();
        }

        private void LayoutOverlay()
        {
            if (overlayPlate == null) return;
            var field = PlayField;
            Stretch(overlayPlate, 0f, 0f, 0f, 0f);
            float width = Mathf.Min(520f * FontScale, field.width - 48f);
            float headline = 1.3f * overlayHeadline.fontSize;
            bool detail = overlayDetail.gameObject.activeSelf;
            float detailHeight = detail ? Mathf.Ceil(overlayDetail.GetPreferredValues(overlayDetail.text, width - 48f, 0f).y) + 4f : 0f;
            float height = Mathf.Min(field.height - 24f, 20f + headline + (detail ? 6f + detailHeight : 0f) + 20f);
            float top = field.y + (field.height - height) * .5f;
            Place(overlayCard, (surfaceWidth - width) * .5f, top, width, height);
            // The headline sits in the card's top, or centred on the field where there is no card (GO).
            float headlineTop = overlayCard.gameObject.activeSelf ? top + 20f : field.y + (field.height - headline) * .5f;
            overlayHeadline.rectTransform.anchorMin = overlayHeadline.rectTransform.anchorMax = new Vector2(0f, 1f);
            overlayHeadline.rectTransform.pivot = new Vector2(.5f, .5f);
            overlayHeadline.rectTransform.anchoredPosition = new Vector2(surfaceWidth * .5f, -(headlineTop + headline * .5f));
            overlayHeadline.rectTransform.sizeDelta = new Vector2(width, headline);
            Place(overlayDetail.rectTransform, 24f, 20f + headline + 6f, width - 48f, Mathf.Max(0f, detailHeight));
        }

        /// <summary>GO shows for half a second when input goes live, then gets out of the way.</summary>
        private void AdvanceOverlayBeats(float delta)
        {
            if (overlay != OverlayState.Go || goLeft <= 0f) return;
            goLeft = Mathf.Max(0f, goLeft - Mathf.Max(0f, delta));
            if (!ReducedMotion)
            {
                float pop = Mathf.Lerp(1f, 1.25f, goLeft / GoSeconds);
                overlayHeadline.rectTransform.localScale = new Vector3(pop, pop, 1f);
            }
            if (goLeft <= 0f) ShowOverlay(OverlayState.None, "");
        }

        /// <summary>Everything timed on the board that is not the attempt's clock, while it is played.</summary>
        private void AdvanceBeats(float delta)
        {
            AdvanceOverlayBeats(delta);
            AdvanceClockBeats(delta);
            switch (run.Kind)
            {
                case CompetitionMiniGames.Kind.Memory: AdvanceMemoryBeats(delta); break;
                case CompetitionMiniGames.Kind.Reaction: AdvanceReactionBeats(delta); break;
                case CompetitionMiniGames.Kind.Endurance: AdvanceEnduranceBeats(delta); break;
            }
        }
    }
}
