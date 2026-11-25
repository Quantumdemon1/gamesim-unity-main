using Gamesim.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace Gamesim.Episode
{
    public sealed partial class EpisodeHud
    {
        public bool UsePhoneDecisionLayout { get; set; } = PhoneDisplay.Active;
        private CanvasGroup phoneDecisionHost;
        private PhoneMenuPage phoneDecisionPage;

        public bool UsePhoneSettingsLayout { get; set; } = PhoneDisplay.Active;
        private string phoneSettingsPageKey;

        private object lastDiaryDraft;
        private string decisionFocus;
        private float decisionScroll = 1f, decisionSummaryScroll = 1f;

        internal PhoneMenuPage OpenPhonePanel(string title)
        {
            if (phoneDecisionHost == null)
            {
                var root = new GameObject("Phone HUD panel", typeof(RectTransform), typeof(Canvas),
                    typeof(FrameScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
                root.transform.SetParent(transform, false);
                var sheet = root.GetComponent<Canvas>();
                sheet.renderMode = RenderMode.ScreenSpaceOverlay;
                sheet.sortingOrder = canvas.sortingOrder + 1;
                var scale = root.GetComponent<FrameScaler>();
                scale.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scale.UsePhoneUnits = true;
                phoneDecisionHost = root.GetComponent<CanvasGroup>();
                RegisterOverlay(phoneDecisionHost);
            }
            phoneDecisionHost.gameObject.SetActive(HudSurfaceOwnsInput());
            phoneDecisionPage = PhoneMenuPage.Create(phoneDecisionHost.transform, title, FontScale);
            phoneDecisionPage.OwnsInput = () => HudSurfaceOwnsInput() && ActiveOverlay() == phoneDecisionHost.transform;
            return phoneDecisionPage;
        }

        internal void FinishPhoneSettingsPage(string key, string firstControl, System.Action back, string requestedFocus = null)
        {
            bool same = phoneSettingsPageKey == key;
            string focus = requestedFocus ?? (same ? decisionFocus : null);
            if (!phoneDecisionPage.HasFocusTarget(focus)) focus = firstControl;
            preferredSelection = phoneDecisionPage.HasEnabledAction(focus) ? focus : firstControl;
            phoneDecisionPage.Restore(same ? decisionScroll : 1f, focus, same ? decisionSummaryScroll : 1f);
            phoneSettingsPageKey = key;
            WayBackAction = back;
        }

        internal void ForgetPhoneSettingsPage() => phoneSettingsPageKey = null;

        internal void RestorePhoneDecision(object draft, Button back)
        {
            WayBackAction = back.onClick.Invoke;
            bool same = ReferenceEquals(lastDiaryDraft, draft);
            string focus = same && phoneDecisionPage.HasFocusTarget(decisionFocus) ? decisionFocus : back.name;
            preferredSelection = phoneDecisionPage.HasEnabledAction(focus) ? focus : back.name;
            phoneDecisionPage.Restore(same ? decisionScroll : 1f, focus, same ? decisionSummaryScroll : 1f);
            lastDiaryDraft = draft;
        }

        private void SyncPhoneDecisionVisibility()
        {
            if (phoneDecisionPage != null) phoneDecisionHost.gameObject.SetActive(HudSurfaceOwnsInput());
        }

        private void ClosePhoneDecision()
        {
            if (phoneDecisionPage == null) return;
            decisionFocus = phoneDecisionPage.CurrentFocus;
            decisionScroll = phoneDecisionPage.ScrollPosition;
            decisionSummaryScroll = phoneDecisionPage.SummaryPosition;
            phoneDecisionHost.gameObject.SetActive(false);
            phoneDecisionPage.gameObject.SetActive(false);
            Destroy(phoneDecisionPage.gameObject);
            phoneDecisionPage = null;
        }
    }
}
