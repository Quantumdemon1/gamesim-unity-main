using System;
using Gamesim.Presentation;
using TMPro;
using UnityEngine;

namespace Gamesim.Episode
{
    public sealed partial class EpisodeHud
    {
        /// <summary>Conversation controls have their own captions so focus survives a change of speaker.</summary>
        public const string ConversationContinueCaption = "Continue conversation";
        public const string ConversationSkipCaption = "Skip conversation";
        public const string ConversationExchangeLineName = "Conversation exchange spoken line";
        public const string ConversationExchangeFooterName = "Conversation exchange controls";

        /// <summary>
        /// Draws the current spoken beat of an accepted exchange. The director owns its sequence,
        /// camera and completion; this method neither changes the simulation nor repeats its history.
        /// </summary>
        public void RenderConversationExchange(string speakerId, string line, bool playerSpeaking,
            bool dramatic, Action advance, Action skip)
        {
            if (content == null || modal == null) return;
            SetActivityLayout(ActivityLayout.Conversation);
            Eyebrow(dramatic ? "A MOMENT THAT MATTERS" : "IN CONVERSATION",
                dramatic ? UiTheme.Warning : UiTheme.Muted);
            var speaker = director != null ? director.Snapshot?.Find(speakerId) : null;
            string name = speaker?.name ?? (playerSpeaking ? "You" : "Houseguest");
            SpeakerTitle(speakerId, name + (playerSpeaking && speaker != null
                && !string.Equals(name, "You", StringComparison.OrdinalIgnoreCase) ? " (You)" : ""),
                playerSpeaking ? "Your words" : "Their reply");
            Speech("\"" + (line ?? "") + "\"", ConversationExchangeLineName, false);
            ConversationExchangeControls(advance, skip);
        }

        /// <summary>
        /// Both controls stay below the reading column rather than scrolling away with a long
        /// line. Their captions decide whether they fit side by side at the current text size.
        /// </summary>
        private void ConversationExchangeControls(Action advance, Action skip)
        {
            if (conversationColumn == null || modalScroll == null || pinnedAction != null)
            {
                Action(ConversationContinueCaption, advance);
                Action(ConversationSkipCaption, skip);
                return;
            }

            float scale = FontScale, gap = 10f * scale, height = PinnedHeight * scale;
            float width = Mathf.Max(0f, conversationColumn.sizeDelta.x - 24f);
            float wanted = 0f;
            foreach (var caption in new[] { ConversationContinueCaption, ConversationSkipCaption })
            {
                var probe = NewText(conversationColumn, caption, 20, Paper);
                probe.textWrappingMode = TextWrappingModes.NoWrap;
                wanted = Mathf.Max(wanted, probe.GetPreferredValues(probe.text).x + 32f + 18f * scale + 10f);
                probe.gameObject.SetActive(false);
                Destroy(probe.gameObject);
            }

            bool stacked = (width - gap) * .5f < wanted;
            var controls = new GameObject(ConversationExchangeFooterName, typeof(RectTransform)).GetComponent<RectTransform>();
            controls.SetParent(conversationColumn, false);
            controls.anchorMin = Vector2.zero;
            controls.anchorMax = new Vector2(1f, 0f);
            controls.pivot = new Vector2(.5f, 0f);
            pinnedExtra = stacked ? height + gap : 0f;
            controls.offsetMin = new Vector2(14f, PinnedMargin);
            controls.offsetMax = new Vector2(-10f, PinnedMargin + height + pinnedExtra);
            UnityEngine.UI.HorizontalOrVerticalLayoutGroup layout = stacked
                ? controls.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>()
                : controls.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            layout.spacing = gap;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = true;

            var next = ActionIn(controls, ConversationContinueCaption, advance);
            var leave = ActionIn(controls, ConversationSkipCaption, skip);
            foreach (var button in new[] { next, leave })
            {
                var element = button.GetComponent<UnityEngine.UI.LayoutElement>();
                element.preferredWidth = 0f;
                element.flexibleWidth = 1f;
                element.minHeight = element.preferredHeight = height;
                element.flexibleHeight = 0f;
            }
            pinnedAction = (RectTransform)next.transform;
            ApplyPinnedInset();
        }
    }
}
