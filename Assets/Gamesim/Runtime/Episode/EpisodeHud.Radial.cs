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
    /// Compact conversation topics beside the speaker header, with advanced actions one level in.
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

        private RectTransform dialRoot;
        private int topicSeats, topicTaken;
        private Selectable[] tabOrder = Array.Empty<Selectable>();

        private void WireConversationGrid()
        {
            if (dialRoot == null || !dialRoot.gameObject.activeInHierarchy) return;
            var cells = dialRoot.GetComponentsInChildren<Button>().Where(button => button.IsActive() && button.IsInteractable()).ToArray();
            int columns = dialRoot.GetComponent<GridLayoutGroup>().constraintCount;
            var before = AdjacentRadialControl(false);
            var after = AdjacentRadialControl(true);
            for (int i = 0; i < cells.Length; i++)
            {
                var navigation = cells[i].navigation;
                int column = i % columns;
                navigation.selectOnLeft = column > 0 ? cells[i - 1] : cells[i];
                navigation.selectOnRight = column + 1 < columns && i + 1 < cells.Length ? cells[i + 1] : cells[i];
                // At a vertical edge, leave the grid vertically instead of inheriting the
                // one-dimensional Tab successor (which may be the next cell on this row).
                navigation.selectOnUp = i >= columns ? cells[i - columns] : before != null ? before : cells[i];
                navigation.selectOnDown = i + columns < cells.Length ? cells[i + columns] : after != null ? after : cells[i];
                cells[i].navigation = navigation;
            }
        }

        /// <summary>
        /// Opens a dial around <paramref name="contestantId"/> with room for
        /// <paramref name="seats"/> petals. Petals are added with <see cref="Petal"/>; once the
        /// seats are gone, a petal falls back to an ordinary row rather than stacking on one.
        /// </summary>
        public void ConversationRadial(string contestantId, int seats)
        {
            dialRoot = null;
            if (content == null) return;

            SetActivityLayout(ActivityLayout.Conversation);
            topicSeats = Mathf.Max(1, seats); topicTaken = 0;
            dialRoot = new GameObject(DialName, typeof(RectTransform), typeof(GridLayoutGroup)).GetComponent<RectTransform>();
            dialRoot.SetParent(content, false);
            // Four columns leave the people visible above the topic chooser. Every existing caption
            // and category survives; long captions wrap instead of forcing an oversized radial.
            var grid = dialRoot.GetComponent<GridLayoutGroup>();
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount = 4;
            grid.spacing = new Vector2(8f, 8f);
            grid.padding = new RectOffset(0,0,Mathf.RoundToInt(30f * FontScale),0);
            float available = modal.sizeDelta.x - 74f;
            grid.cellSize = new Vector2((available - 24f) / 4f, 112f * FontScale);
            int rows = (topicSeats + 3) / 4;
            float height = 30f * FontScale + rows * grid.cellSize.y + (rows - 1) * 8f;
            var element = dialRoot.gameObject.AddComponent<LayoutElement>();
            element.minHeight = height;
            element.preferredHeight = height;

            var line = NewText(dialRoot, DialPrompt, 13, UiTheme.Muted);
            line.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            Anchor(line.rectTransform, new Vector2(0,1), new Vector2(0,1),
                Vector2.zero, new Vector2(available,24f * FontScale));
            line.alignment = TextAlignmentOptions.Left;
        }

        /// <summary>
        /// Seats one action on the dial: the glyph and the colour the mockup gives that kind of
        /// conversation, and the caption the build already used for it.
        /// </summary>
        public Button Petal(string caption, string icon, Color tint, Action action)
        {
            if (dialRoot == null || topicTaken >= topicSeats) return Action(caption, action);

            // The petal's frame is structure; its action colour lives on the glyph below, which is
            // the object the colour is about. It used to be painted on the border instead - twice
            // over, in fact, because Chrome was already drawing a cyan one underneath it.
            var rect = Chrome(caption, dialRoot, UiTheme.Emphasis.Interactive);
            HudEmphasis.Promote(rect, UiTheme.Emphasis.Interactive);
            topicTaken++;
            var button = Pressable(rect, action);

            var cell = dialRoot.GetComponent<GridLayoutGroup>().cellSize;
            float width = cell.x, height = cell.y;
            float side = 18f * FontScale;
            var art = HudPrimitives.Glyph("Petal mark", rect, icon, tint,
                new Vector2(width - side - 10f, -8f * FontScale), side);
            float top = 10f * FontScale;

            var label = NewText(rect, caption, 15, Paper);
            Anchor(label.rectTransform, new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(10f * FontScale, -top),
                new Vector2(width - (art != null ? 44f : 20f) * FontScale, height - top - 32f * FontScale));
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
            var next = AdjacentRadialControl(true);
            if (next != null) EventSystem.current.SetSelectedGameObject(next.gameObject);
        }

        private Selectable AdjacentRadialControl(bool after)
        {
            if (content == null || dialRoot == null || dialRoot.parent != content) return null;
            // Beneath, not merely outside: an oath declaration is drawn above the dial, and handing
            // the keyboard backwards to it would be the opposite of what the petal says it does.
            int step = after ? 1 : -1;
            for (int i = dialRoot.GetSiblingIndex() + step; i >= 0 && i < content.childCount; i += step)
            {
                var controls = content.GetChild(i).GetComponentsInChildren<Selectable>()
                    .Where(item => item.IsActive() && item.IsInteractable() && !(item is Scrollbar));
                var adjacent = after ? controls.FirstOrDefault() : controls.LastOrDefault();
                if (adjacent != null) return adjacent;
            }
            return null;
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
            // A neutral ramp, because these multiply whatever the control already is. The old
            // green-weighted values pulled every hovered petal towards green, so the dial's
            // action colours - the one place the build already spoke the mockups' colour
            // language - stopped meaning anything the moment the keyboard reached them.
            colours.highlightedColor = new Color(1.35f, 1.35f, 1.35f);
            colours.selectedColor = colours.highlightedColor;
            colours.pressedColor = new Color(.85f, .85f, .85f);
            button.colors = colours;
            var press = rect.gameObject.AddComponent<HudPress>();
            press.ReducedMotion = ReducedMotion;
            press.Hovered = () => Foley(HouseAudio.Cue.Hover);
            button.onClick.AddListener(() => action());
            return button;
        }
    }
}
