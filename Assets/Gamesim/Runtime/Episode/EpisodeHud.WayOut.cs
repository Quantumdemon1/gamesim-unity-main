using System;
using Gamesim.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace Gamesim.Episode
{
    /// <summary>
    /// The docked panel's ways out (menu slice 2, the review's change 1). Every panel has one
    /// "Close" at its top right (built with the panel, EpisodeHud.cs). A page reached from another
    /// page inside the panel - a houseguest's profile from the directory, Deals &amp; promises from
    /// the notes, a beat or a houseguest's screen from free time's board, a story's second step -
    /// has its "Back to &lt;that page&gt;" at the top left as well, drawn by the shared helper, and
    /// Escape and the pad's B take it before they close the panel (EpisodeDirector.Controls.cs).
    /// The key hint moves with them: it stands beside whichever control the key presses.
    /// </summary>
    public sealed partial class EpisodeHud
    {
        /// <summary>This render's Close and way back; both go with the panel they were drawn on.</summary>
        private Button panelClose, wayBack;
        private bool wayBackRoomOwed;

        /// <summary>What the page's way back does, or null on a page that has none: what Escape and B press first.</summary>
        public Action WayBackAction { get; private set; }

        /// <summary>
        /// The safe answer of the confirm card this render drew (<see cref="SettingsConfirm"/>), or
        /// null with none up: Escape and B take it before the page's way back, so a card that asks is
        /// answered "no" and never stepped around.
        /// </summary>
        public Action ConfirmCancelAction { get; private set; }

        /// <summary>The panel's Close this render, or null with no panel up.</summary>
        public Button PanelClose => panelClose;

        /// <summary>The page's way back this render, or null on a page that has none.</summary>
        public Button PageWayBack => wayBack != null ? wayBack : DecisionPageBackControl;

        private void ForgetWayBack()
        {
            panelClose = null; wayBack = null; wayBackRoomOwed = false;
            decisionPageBack = null;
            WayBackAction = null;
            ConfirmCancelAction = null;
        }

        /// <summary>
        /// A page's way back takes the key, so the key's hint stands beside it and no longer beside
        /// the panel's Close: one "Esc" on the panel, beside the control the key presses.
        /// </summary>
        private void TakeKeyHintFromClose()
        {
            if (panelClose == null) return;
            var hint = panelClose.transform.Find(WayOut.KeyHintObjectName);
            if (hint != null) DestroyImmediate(hint.gameObject);
        }

        /// <summary>
        /// The way back of a page reached from another page inside the panel: "Back to &lt;that
        /// page&gt;" (<paramref name="caption"/>, made by <see cref="WayOut.BackTo"/>) at the panel's top
        /// left, beside its Close. One a render: the innermost page draws first and keeps it, so a
        /// story's second step inside a beat goes back to the beat's choices, and the beat's own way
        /// back to free time comes with the beat again. With no panel (a test's bare render) it is a
        /// row, as it was.
        /// </summary>
        public Button WayBack(string caption, Action back)
        {
            if (back == null) return null;
            if (modal == null) return content != null ? Action(caption, back) : null;
            if (wayBack != null) return wayBack;
            // One way back a render, whichever helper drew it: a decision page's (a story's second
            // step inside a free-time beat, EpisodeHud.DecisionPageBack.cs) drew first and keeps it.
            var drawn = DecisionPageBackControl;
            if (drawn != null) return drawn;
            wayBack = WayOut.AddBack(modal, caption, back, FontScale, InputGlossary.BackKeyHint);
            KeepWayOutsAboveTheHeader();
            WayBackAction = back;
            // The key answers the way back now, so its hint stands there and not beside Close.
            TakeKeyHintFromClose();
            wayBackRoomOwed = true;
            return wayBack;
        }

        /// <summary>
        /// Once the render's layout is done: the way back above everything else on the panel (a
        /// scroll stretched over the corner would take its presses), and the head that starts in
        /// that corner moved right of it - the phase band's glyph and words, or a notebook or diary
        /// page's head. With no head there, the scroll starts under the way back instead.
        /// </summary>
        private void MakeRoomForWayBack()
        {
            wayBackRoomOwed = false;
            if (wayBack == null || modal == null) return;
            var back = (RectTransform)wayBack.transform;
            back.SetAsLastSibling();
            float gap = 12f * FontScale;
            float right = back.anchoredPosition.x + back.sizeDelta.x + gap;
            float foot = -back.anchoredPosition.y + back.sizeDelta.y + gap * .5f;
            float closeRoom = panelClose != null ? ((RectTransform)panelClose.transform).sizeDelta.x + 2f * WayOut.Inset(FontScale) : 0f;
            bool moved = false;
            foreach (Transform child in modal)
            {
                if (!child.gameObject.activeSelf || !(child is RectTransform rect)) continue;
                if (child.name == "Phase band")
                {
                    // Stretched across the panel: its left edge moves, and its words stop short of Close.
                    rect.offsetMin = new Vector2(Mathf.Max(rect.offsetMin.x, right - 18f), rect.offsetMin.y);
                    float room = Mathf.Max(120f, modal.rect.width - right - closeRoom - 40f);
                    foreach (var words in new[] { bandTitle, bandLine })
                        if (words != null && words.transform.IsChildOf(rect))
                            words.rectTransform.sizeDelta = new Vector2(Mathf.Min(words.rectTransform.sizeDelta.x, room), words.rectTransform.sizeDelta.y);
                    moved = true;
                }
                else if (child.name == NotebookHeaderName || child.name == DiaryHeaderName || child.name == SettingsHeaderName)
                {
                    // Anchored at the top left with its own width: it moves right and gives up as much.
                    float shift = Mathf.Max(0f, right - 6f - rect.anchoredPosition.x);
                    rect.anchoredPosition = new Vector2(rect.anchoredPosition.x + shift, rect.anchoredPosition.y);
                    rect.sizeDelta = new Vector2(Mathf.Max(120f, rect.sizeDelta.x - shift), rect.sizeDelta.y);
                    moved = true;
                }
            }
            if (moved || modalScroll == null) return;
            var scroll = (RectTransform)modalScroll.transform;
            if (scroll.parent == modal && scroll.offsetMax.y > -foot)
                scroll.offsetMax = new Vector2(scroll.offsetMax.x, -foot);
        }
    }
}
