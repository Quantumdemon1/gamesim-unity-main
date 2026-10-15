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
            if (!show || canvas == null || Compact) return;
            saveFeedback = Chrome(SaveFeedbackName, canvas.transform);
            float width = Mathf.Min(160f * FontScale, HelpGutter - 28f);
            Anchor(saveFeedback, new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(16f, StatusBottom), new Vector2(width, 34f * FontScale));
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
