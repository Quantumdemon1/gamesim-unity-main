using System;
using System.Linq;
using Gamesim.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Gamesim.Episode
{
    /// <summary>
    /// The conversation dial (mockup-06, mockup-12, VISUAL-TARGET.md V2 item 5): the speaker's face
    /// at the hub and the ways of talking to them around it, with everything else one level in.
    ///
    /// <para>The petals carry the build's own captions, unshortened. That is not a stylistic choice:
    /// a test and a screen reader both identify a control by the words on it, and there is exactly
    /// one active control in the HUD with any given caption, so a petal reading "Chat" where the
    /// list read "Make small talk" would be a different button as far as either is concerned. The
    /// mockup's single words survive as the glyph and the colour on each petal.</para>
    ///
    /// <para>Nothing is hidden by the dial. The actions it does not seat are the same rows they
    /// always were, immediately beneath it, in the same order — the "More" petal moves the keyboard
    /// to the first of them rather than revealing them, because the panel wires its selection ring
    /// from the controls that exist, and a control that only exists after a press is a control the
    /// keyboard can never reach.</para>
    /// </summary>
    public sealed partial class EpisodeHud
    {
        /// <summary>The words on the petal that hands over to the rows beneath the dial.</summary>
        public const string MorePetalCaption = "More ways to talk";
        /// <summary>The line under the speaker's face at the hub.</summary>
        public const string DialPrompt = "Choose a conversation topic";
        /// <summary>The dial's block, so a test can find it the way it finds a named panel.</summary>
        public const string DialName = "Conversation radial";

        private HudPrimitives.Dial dial;
        private RectTransform dialRoot;

        /// <summary>
        /// Opens a dial around <paramref name="contestantId"/> with room for
        /// <paramref name="seats"/> petals. Petals are added with <see cref="Petal"/>; once the
        /// seats are gone, a petal falls back to an ordinary row rather than stacking on one.
        /// </summary>
        public void ConversationRadial(string contestantId, int seats)
        {
            dial = null; dialRoot = null;
            if (content == null) return;

            dial = HudPrimitives.Radial(DialName, content, Portrait(contestantId), seats, FontScale);
            dialRoot = dial.Root;
            var element = dialRoot.gameObject.AddComponent<LayoutElement>();
            element.minHeight = dialRoot.sizeDelta.y;
            element.preferredHeight = dialRoot.sizeDelta.y;

            var line = NewText(dialRoot, DialPrompt, 13, UiTheme.Muted);
            Anchor(line.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                new Vector2(0f, -86f * FontScale), new Vector2(190f * FontScale, 40f * FontScale));
            line.alignment = TextAlignmentOptions.Top;
            AutoSize(line, 11);
        }

        /// <summary>
        /// Seats one action on the dial: the glyph and the colour the mockup gives that kind of
        /// conversation, and the caption the build already used for it.
        /// </summary>
        public Button Petal(string caption, string icon, Color tint, Action action)
        {
            if (dial == null || dial.Taken >= dial.Seats) return Action(caption, action);

            var rect = Chrome(caption, dialRoot, Ink);
            dial.Place(rect);
            // A second hairline over the glass one, in the petal's own colour: this is the whole of
            // what the mockup's single-word labels carried, so it has to be visible at a glance.
            UiTheme.AddBorder(rect, UiTheme.GlassRadius, new Color(tint.r, tint.g, tint.b, .6f));
            var button = Pressable(rect, action);

            float width = dial.Petal.x, height = dial.Petal.y;
            float side = 26f * FontScale;
            var art = HudPrimitives.Glyph("Petal mark", rect, icon, tint,
                new Vector2((width - side) * .5f, -10f * FontScale), side);
            float top = (art != null ? 42f : 16f) * FontScale;

            var label = NewText(rect, caption, 15, Paper);
            Anchor(label.rectTransform, new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(10f * FontScale, -top),
                new Vector2(width - 20f * FontScale, height - top - 30f * FontScale));
            label.alignment = TextAlignmentOptions.Top;
            // The captions are sentences of very different lengths in a petal of one size, so the
            // longest of them is allowed to shrink rather than to clip.
            AutoSize(label, 12);
            return button;
        }

        /// <summary>
        /// Moves the keyboard to the first row under the dial, which the panel then scrolls to.
        /// What the "More" petal does, and nothing a mouse cannot already do by scrolling.
        /// </summary>
        public void RevealBeyondRadial()
        {
            if (content == null || EventSystem.current == null) return;
            // Beneath, not merely outside: an oath declaration is drawn above the dial, and handing
            // the keyboard backwards to it would be the opposite of what the petal says it does.
            int after = dialRoot != null && dialRoot.parent == content ? dialRoot.GetSiblingIndex() : -1;
            Selectable next = null;
            for (int i = after + 1; i < content.childCount && next == null; i++)
                next = content.GetChild(i).GetComponentsInChildren<Selectable>()
                    .FirstOrDefault(item => item.IsActive() && item.IsInteractable() && !(item is Scrollbar));
            if (next != null) EventSystem.current.SetSelectedGameObject(next.gameObject);
        }

        /// <summary>
        /// Makes an existing rectangle a button: the HUD's tint ramp, its press animation and its
        /// hover foley. Shared by the panel's rows and by the dial's petals, which differ only in
        /// what they put inside the rectangle.
        /// </summary>
        private Button Pressable(RectTransform rect, Action action)
        {
            var button = rect.gameObject.AddComponent<Button>();
            var colours = button.colors;
            colours.highlightedColor = new Color(1.2f, 1.6f, 1.45f);
            colours.selectedColor = colours.highlightedColor;
            colours.pressedColor = new Color(.65f, 1.1f, .9f);
            button.colors = colours;
            var press = rect.gameObject.AddComponent<HudPress>();
            press.ReducedMotion = ReducedMotion;
            press.Hovered = () => Foley(HouseAudio.Cue.Hover);
            button.onClick.AddListener(() => action());
            return button;
        }
    }
}
