using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gamesim.Episode
{
    public sealed partial class EpisodeHud
    {
        /// <summary>The row a conversation's way on stands in, at the foot of its column, so a test can find it.</summary>
        public const string ConversationWayOnName = "Conversation way on";

        /// <summary>
        /// A way on out of a conversation, pinned at the foot of its column under the scroll - the
        /// campaign's "Next voter: Dana" (UI-UX plan D11): the row an action is, named and captioned
        /// by its caption, so every lookup by caption or by name finds it, but out of the scrolling
        /// list, so it is on screen however long the page above it runs, and no row of the page. The
        /// scroll stands clear of it. It never takes the keyboard on its own: a page opens on its own
        /// first choice, as it did. Arrows and Tab reach it as they reach every control of the panel.
        /// Without a conversation's column it is an ordinary row.
        /// </summary>
        public Button ConversationWayOn(string caption, Action action)
        {
            if (conversationColumn == null || !conversationColumn.gameObject.activeSelf || modalScroll == null
                || !modalScroll.transform.IsChildOf(conversationColumn))
                return Action(caption, action);
            float height = PinnedHeight * FontScale;
            var foot = new GameObject(ConversationWayOnName, typeof(RectTransform)).GetComponent<RectTransform>();
            foot.SetParent(conversationColumn, false);
            foot.anchorMin = Vector2.zero;
            foot.anchorMax = new Vector2(1f, 0f);
            foot.pivot = new Vector2(.5f, 0f);
            // In line with the scroll above it, as the exchange's Continue and Skip stand.
            foot.offsetMin = new Vector2(14f, PinnedMargin);
            foot.offsetMax = new Vector2(-10f, PinnedMargin + height);
            var layout = foot.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = true;
            var button = ActionIn(foot, caption, action);
            var element = button.GetComponent<LayoutElement>();
            if (element != null)
            {
                element.preferredWidth = 0f; element.flexibleWidth = 1f;
                element.minHeight = element.preferredHeight = height; element.flexibleHeight = 0f;
            }
            // One line of words in a row of fixed height: a long name at the largest text shrinks
            // rather than running onto a second line the row has no room for.
            foreach (var words in button.GetComponentsInChildren<TMP_Text>(true))
                if (words.transform.parent == button.transform) AutoSize(words, 14f);
            var scroll = (RectTransform)modalScroll.transform;
            float clear = PinnedMargin + height + 10f;
            if (scroll.offsetMin.y < clear) scroll.offsetMin = new Vector2(scroll.offsetMin.x, clear);
            return button;
        }
    }
}
