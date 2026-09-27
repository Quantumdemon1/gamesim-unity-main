using Gamesim.Presentation;
using Gamesim.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gamesim.Episode
{
    /// <summary>
    /// A conversation the house cannot have right now (Refinement Kit 6's preview 01): a card sized
    /// to what it says, low on the left of the frame with the house still up around it.
    ///
    /// <para>It was the whole conversation drawer cut short: the column the height of the screen,
    /// "Neutral -9 · 5 actions left" under the name - a relationship band, a signed number and the
    /// week's budget run together, so the band read as the houseguest's mood - then the greeting,
    /// then a paragraph saying again what the greeting had just said. Now it is the person, their
    /// mood and your trust as two separate things, what they said, and one line saying why there
    /// is nothing to choose. Nothing is spent to be told so, and no budget is shown.</para>
    /// </summary>
    public sealed partial class EpisodeHud
    {
        public const string ConversationNoticeName = "Conversation notice";
        public const string ConversationLockName = "Conversation availability";
        /// <summary>The card's width at the resting text size: Kit 6's 556 at the 1920 reference.</summary>
        private const float NoticeWidth = 464f;

        private void ConversationNoticeLayout(float canvasWidth, float canvasHeight)
        {
            float s = FontScale;
            float free = Mathf.Max(420f, canvasWidth - LeftColumnX - RightColumnInset - RightColumnWidth - 12f);
            float width = Mathf.Min(NoticeWidth * s, free);
            modal.anchorMin = modal.anchorMax = Vector2.zero;
            modal.pivot = Vector2.zero;
            modal.anchoredPosition = new Vector2(LeftColumnX + 24f, ModalLift + 12f);
            // Provisional: the card takes its height from what it holds once that is built.
            modal.sizeDelta = new Vector2(width, 300f * s);
            var ground = modal.GetComponent<Image>();
            if (ground != null)
            {
                var fill = new Color(UiTheme.CardFill.r, UiTheme.CardFill.g, UiTheme.CardFill.b, .97f);
                if (!UiTheme.PackSliced(ground, PackArt.KitCardFill, 14f, fill)) ground.color = fill;
            }
            // No halo on a card that asks for nothing: the resting edge only.
            foreach (Transform child in modal)
                if (child.name == "Border" || child.name == "Glow" || child.name == "Phase band" || child.name == "Panel control hint")
                    child.gameObject.SetActive(false);
            var edge = new GameObject("Notice edge", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            edge.rectTransform.SetParent(modal, false);
            edge.rectTransform.SetAsFirstSibling();
            Stretch(edge.rectTransform, 0f, 0f, 0f, 0f);
            edge.raycastTarget = false;
            if (!UiTheme.PackSliced(edge, PackArt.KitCardEdge, 14f, UiTheme.Edge(UiTheme.Emphasis.Interactive)))
                edge.color = new Color(0f, 0f, 0f, 0f);
            CompactClose();
            // The scroll starts near the card's top, over the close control's corner; the control
            // has to be above it or the viewport takes its clicks.
            if (modal.Find("Close  [Esc]") is RectTransform close) close.SetAsLastSibling();
            SetChromeVisible(FollowChipName, false);
            SetChromeVisible("Interaction prompt", false);
            if (modalScroll != null)
            {
                Stretch((RectTransform)modalScroll.transform, 18f, 16f, 14f, 14f);
                modalScroll.verticalNormalizedPosition = 1f;
            }
        }

        /// <summary>
        /// Draws the card. <paramref name="quote"/> is the houseguest's own line and keeps the name
        /// every conversation's spoken line is found by; the rest are the director's words.
        /// </summary>
        public RectTransform ConversationNotice(ContestantState speaker, string identity, string mood, string trust,
            Color trustTint, string quote, string unavailable)
        {
            SetActivityLayout(ActivityLayout.ConversationNotice);
            if (content == null || speaker == null) return null;
            float s = FontScale, width = ContentWidth();
            var root = new GameObject(ConversationNoticeName, typeof(RectTransform), typeof(LayoutElement)).GetComponent<RectTransform>();
            root.SetParent(content, false);

            float face = 76f * s;
            var rim = HudPrimitives.Portrait(root, CharacterPortraits.Get(speaker), UiTheme.Outline, face, 2f * s, false, speaker);
            rim.name = "Speaker portrait";
            Anchor(rim, new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, rim.sizeDelta);
            float x = rim.sizeDelta.x + 16f * s;
            // The name stops short of the close control in the card's corner.
            float nameRoom = Mathf.Max(120f, width - x - 118f * s);
            var name = FixedText(root, speaker.name, 24, Paper, new Vector2(x, -6f * s), new Vector2(nameRoom, 32f * s));
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            if (semibold != null) name.font = semibold;
            name.textWrappingMode = TextWrappingModes.NoWrap;
            AutoSize(name, 15);
            float y = 42f * s;
            if (!string.IsNullOrEmpty(identity)) y = PlacedCopy(root, identity, 16, UiTheme.Weight.Regular, UiTheme.Muted, x, y, width - x);
            y = Mathf.Max(y, rim.sizeDelta.y) + 16f * s;

            float pill = 30f * s, px = 0f;
            if (!string.IsNullOrEmpty(mood))
            {
                var chip = KitPill(root, "Mood", mood, UiTheme.SurfaceRaised, Paper, 15, pill);
                Anchor(chip, new Vector2(0, 1), new Vector2(0, 1), new Vector2(px, -y), chip.sizeDelta);
                px += chip.sizeDelta.x + 10f * s;
            }
            if (!string.IsNullOrEmpty(trust))
            {
                var chip = KitPill(root, "Your trust", trust, Color.Lerp(UiTheme.SurfaceRaised, trustTint, .18f),
                    Color.Lerp(Paper, trustTint, .6f), 15, pill);
                if (px > 0f && px + chip.sizeDelta.x > width) { px = 0f; y += pill + 8f * s; }
                Anchor(chip, new Vector2(0, 1), new Vector2(0, 1), new Vector2(px, -y), chip.sizeDelta);
            }
            y += pill + 16f * s;

            if (!string.IsNullOrEmpty(quote))
            {
                var said = NewText(root, "“" + quote + "”", 20, Paper);
                said.gameObject.name = "NPC spoken dialogue";
                if (semibold != null) said.font = semibold;
                float height = Mathf.Ceil(said.GetPreferredValues(said.text, width, 0f).y) + 4f;
                Anchor(said.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0f, -y), new Vector2(width, height));
                y += height + 16f * s;
            }

            var line = new GameObject(ConversationLockName, typeof(RectTransform)).GetComponent<RectTransform>();
            line.SetParent(root, false);
            float lx = 0f;
            if (KitGlyph(line, PackArt.KitIconLock, UiTheme.Muted, new Vector2(0, 1), new Vector2(0f, -2f * s), 20f * s) != null) lx = 30f * s;
            float lineHeight = PlacedCopy(line, unavailable, 16, UiTheme.Weight.Regular, UiTheme.Muted, lx, 0f, width - lx);
            Anchor(line, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0f, -y), new Vector2(width, lineHeight));
            y += lineHeight;

            var size = root.GetComponent<LayoutElement>();
            size.minHeight = size.preferredHeight = y;
            // The card is as tall as this, and no taller: the layout's padding and the scroll's
            // insets around it, clamped to the free height.
            var bounds = ((RectTransform)canvas.transform).rect;
            float canvasHeight = bounds.height > 0 ? bounds.height : 900f;
            float most = Mathf.Max(200f, canvasHeight - ModalLift - ActivityHeadroom - 12f);
            modal.sizeDelta = new Vector2(modal.sizeDelta.x, Mathf.Min(most, y + 6f + 18f + 16f + 14f + 4f));
            return root;
        }
    }
}
