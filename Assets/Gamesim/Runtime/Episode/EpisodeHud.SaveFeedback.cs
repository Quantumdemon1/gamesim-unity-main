using Gamesim.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace Gamesim.Episode
{
    public sealed partial class EpisodeHud
    {
        public const string SaveFeedbackName = "Save feedback";
        private RectTransform saveFeedback;
        private bool statusGuidanceVisible = true;
        private bool statusNoticePriority;

        public void ShowSaveFeedback(bool show)
        {
            saveFeedback = null;
            if (!show || canvas == null) return;
            saveFeedback = Chrome(SaveFeedbackName, canvas.transform);
            float width = Mathf.Min(160f * FontScale, HelpGutter - 28f);
            float height = 34f * FontScale;
            // The compact HUD confirms a save too, as the tour promises (PR-9): in the top bar's
            // slot the full HUD gives its objective chip, which the compact HUD leaves empty. There
            // no panel covers it and the rail, which grows down the gutter at the larger sizes, never
            // reaches it.
            if (Compact)
                Anchor(saveFeedback, new Vector2(0f, 1f), new Vector2(0f, 1f),
                    new Vector2(ObjectiveX, -(TopBarTop + Mathf.Max(0f, TopBarHeight - height) * .5f)),
                    new Vector2(Mathf.Min(width, ObjectiveChipWidth), Mathf.Min(height, TopBarHeight)));
            else
                Anchor(saveFeedback, new Vector2(0f, 0f), new Vector2(0f, 0f),
                    new Vector2(16f, StatusBottom), new Vector2(width, height));
            var label = FixedText(saveFeedback, "Saved", 14, UiTheme.Accent, Vector2.zero,
                new Vector2(width - 20f, 24f * FontScale));
            Stretch(label.rectTransform, 12f, 5f, 8f, 5f);
            AutoSize(label, 12);
            ClickThrough(saveFeedback);
        }

        public void HideSaveFeedback()
        {
            if (saveFeedback != null) saveFeedback.gameObject.SetActive(false);
        }

        public void SetStatusGuidanceVisible(bool show, bool notice = false)
        {
            statusGuidanceVisible = show;
            statusNoticePriority = notice;
            ApplyNearby();
            ApplyStatusLine();
        }
    }
}
