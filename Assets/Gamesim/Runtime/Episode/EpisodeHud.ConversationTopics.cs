using System;
using Gamesim.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace Gamesim.Episode
{
    public sealed partial class EpisodeHud
    {
        public const string ConversationTopicsContextName = "Conversation topic context";

        /// <summary>Only categories occupy the root; the reading sheet appears after a choice.</summary>
        public void ConversationTopicsWheel(string npcId, int seats, string name, string pronouns,
            string standing, string actions)
        {
            SetActivityLayout(ActivityLayout.Conversation);
            if (conversationColumn != null)
            {
                var close = conversationColumn.Find("Close  [Esc]");
                if (close != null)
                {
                    close.SetParent(modal, false);
                    Anchor((RectTransform)close, Vector2.one, Vector2.one,
                        new Vector2(-12f, -10f), new Vector2(150f, 38f));
                }
                conversationColumn.gameObject.SetActive(false);
            }
            ConversationRadial(npcId, seats);
            ConversationTopicsContext(name, pronouns, standing, actions);
            ConversationWindowOffset = 0f;
            var ground = modal.GetComponent<Image>();
            if (ground != null) ground.color = new Color(ground.color.r, ground.color.g, ground.color.b, .18f);
        }

        // The context belongs to the conversation, rather than to a hidden choices column.
        // Pin it beside the wheel at the foot of the stage so the participants' faces stay clear.
        private void ConversationTopicsContext(string name, string pronouns, string standing, string actions)
        {
            if (modal == null || dialRoot == null) return;
            float width = Mathf.Min(220f, dialRoot.anchoredPosition.x - dialRoot.sizeDelta.x * .5f - 24f);
            width = Mathf.Max(100f, width);
            var card = new GameObject(ConversationTopicsContextName, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            card.SetParent(modal, false);
            var ground = card.GetComponent<Image>();
            UiTheme.Style(ground, UiTheme.Surface, 12);
            ground.raycastTarget = false;
            float cursor = 12f;
            void Line(string value, int size, Color colour)
            {
                var text = NewText(card, value, size, colour);
                float height = Mathf.Ceil(text.GetPreferredValues(text.text, width - 24f, float.PositiveInfinity).y) + 4f;
                Anchor(text.rectTransform, new Vector2(0, 1), new Vector2(0, 1),
                    new Vector2(12f, -cursor), new Vector2(width - 24f, height));
                cursor += height + 4f;
            }
            Line(name, 20, UiTheme.Accent);
            if (!string.IsNullOrEmpty(pronouns)) Line(pronouns, 14, UiTheme.Muted);
            Line(standing, 16, UiTheme.Paper);
            Line(actions, 16, UiTheme.Paper);
            Anchor(card, Vector2.zero, Vector2.zero, new Vector2(12f, 12f), new Vector2(width, cursor + 8f));
        }

        /// <summary>Controls belong to this rendered conversation, including free view choices.</summary>
        public void GuardConversationChoices(Func<bool> current)
        {
            if (modal == null || current == null) return;
            foreach (var button in modal.GetComponentsInChildren<Button>())
            {
                if (!button.gameObject.activeInHierarchy) continue;
                var original = button.onClick;
                var guarded = new Button.ButtonClickedEvent();
                guarded.AddListener(() => { if (current()) original.Invoke(); });
                button.onClick = guarded;
            }
        }
    }
}
