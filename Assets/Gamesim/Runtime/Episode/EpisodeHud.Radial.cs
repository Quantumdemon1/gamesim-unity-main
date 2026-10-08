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
    /// The conversation dial (mockup-12): seven round petals on a ring around a glowing hub, below
    /// the pair, with the speaker's words and every other way to talk in a column beside it.
    ///
    /// <para>The petals carry the build's own captions, unshortened. That is not a stylistic choice:
    /// a test and a screen reader both identify a control by the words on it, and there is exactly
    /// one active control in the HUD with any given caption, so a petal reading "Chat" where the
    /// list read "Make small talk" would be a different button as far as either is concerned. The
    /// mockup's single words survive as the glyph and the colour on each petal, and the caption
    /// sits in the disc under the glyph, as the mockup's word does - on a disc wide enough for
    /// three short lines. Hung under the disc it ran across the player's body and the set, and
    /// the captions of neighbouring petals ran into each other.</para>
    ///
    /// <para>It was a ring once before, then a four-column grid of cards "to leave the people
    /// visible" - inside an opaque panel that covered exactly where the two-shot puts the people.
    /// The people are visible now because the panel is gone, not because the ring is.</para>
    ///
    /// <para>Nothing is hidden by the dial. The actions it does not seat are the same rows they
    /// always were, in the column, in the same order - the "More" petal moves the keyboard to the
    /// first of them rather than revealing them, because the panel wires its selection ring from
    /// the controls that exist, and a control that only exists after a press is a control the
    /// keyboard can never reach. A zero-height seat in the column marks where the dial stands in
    /// the panel's reading order, which is what the rows before and after it are measured from.</para>
    /// </summary>
    public sealed partial class EpisodeHud
    {
        /// <summary>The words on the petal that hands over to the rows beneath the dial.</summary>
        public const string MorePetalCaption = "More ways to talk";
        /// <summary>The line at the hub.</summary>
        public const string DialPrompt = "Choose a conversation topic";
        /// <summary>The dial's block, so a test can find it the way it finds a named panel.</summary>
        public const string DialName = "Conversation radial";
        /// <summary>Where the dial stands in the column's reading order.</summary>
        public const string DialSeatName = "Conversation radial seat";

        // The ring at the standard text size, in mockup-12's proportions: petals 96 across on a
        // 146 radius around a 120 hub. Seven discs on that ring leave 30 between neighbours and 38
        // between each and the hub. Each caption is inside its disc, in a 76-by-40 box under the
        // glyph: three lines of 12 for the longest, "Tell them something personal". The block is
        // the ring and the category chips that straddle the foot of each disc, 400 square.
        private const float DialRadius = 146f;
        private const float PetalSize = 96f;
        private const float PetalCaptionWidth = 76f;
        private const float PetalCaptionHeight = 40f;
        private const float PetalGlyphSide = 30f;
        private const float PetalGlyphTop = 12f;
        private const float DialHub = 120f;
        private const float DialBlockWidth = 400f;
        private const float DialBlockHeight = 400f;
        /// <summary>How far above the block's foot the ring's centre sits.</summary>
        private const float DialCentreLift = 200f;

        private RectTransform dialRoot;
        private RectTransform dialSeat;
        private float dialScale = 1f;
        private int topicSeats, topicTaken;
        private Selectable[] tabOrder = Array.Empty<Selectable>();

        /// <summary>
        /// Arrows walk the ring spatially: each goes to the nearest petal within 67 degrees of the
        /// direction pressed. Down from the ring's foot leaves it for the first row after the dial;
        /// Up from the ring's head goes to a control before it, if there is one. Tab keeps reading
        /// order, which the panel's own ring already gives.
        /// </summary>
        private void WireConversationRing()
        {
            if (dialRoot == null || !dialRoot.gameObject.activeInHierarchy) return;
            var petals = dialRoot.GetComponentsInChildren<Button>().Where(button => button.IsActive() && button.IsInteractable()).ToArray();
            if (petals.Length == 0) return;
            var before = AdjacentRadialControl(false);
            var after = AdjacentRadialControl(true);
            foreach (var petal in petals)
            {
                var navigation = petal.navigation;
                navigation.selectOnLeft = Toward(petals, petal, Vector2.left) ?? petal;
                navigation.selectOnRight = Toward(petals, petal, Vector2.right) ?? petal;
                navigation.selectOnUp = Toward(petals, petal, Vector2.up) ?? before ?? petal;
                navigation.selectOnDown = Toward(petals, petal, Vector2.down) ?? after ?? petal;
                petal.navigation = navigation;
            }
            // And back: Up from the first row returns to the petal that hands the keyboard to it.
            if (after != null)
            {
                var more = petals.FirstOrDefault(petal => petal.name == MorePetalCaption) ?? petals[petals.Length - 1];
                var navigation = after.navigation;
                navigation.selectOnUp = more;
                after.navigation = navigation;
            }
        }

        /// <summary>The nearest petal within 67 degrees of <paramref name="direction"/>, or null.</summary>
        private static Selectable Toward(Button[] petals, Button from, Vector2 direction)
        {
            var origin = ((RectTransform)from.transform).anchoredPosition;
            Selectable best = null;
            float nearest = float.MaxValue;
            foreach (var other in petals)
            {
                if (other == from) continue;
                var offset = ((RectTransform)other.transform).anchoredPosition - origin;
                if (offset.sqrMagnitude < 1f || Vector2.Angle(offset, direction) > 67f) continue;
                if (offset.magnitude < nearest) { nearest = offset.magnitude; best = other; }
            }
            return best;
        }

        /// <summary>
        /// Opens a dial with room for <paramref name="seats"/> petals. Petals are added with
        /// <see cref="Petal"/>; once the seats are gone, a petal falls back to an ordinary row
        /// rather than stacking on one.
        /// </summary>
        public void ConversationRadial(string contestantId, int seats)
        {
            dialRoot = null; dialSeat = null;
            if (content == null) return;

            SetActivityLayout(ActivityLayout.Conversation);
            topicSeats = Mathf.Max(1, seats); topicTaken = 0;

            dialSeat = new GameObject(DialSeatName, typeof(RectTransform), typeof(LayoutElement)).GetComponent<RectTransform>();
            dialSeat.SetParent(content, false);
            var seat = dialSeat.GetComponent<LayoutElement>();
            seat.minHeight = 0f; seat.preferredHeight = 0f;

            // The ring stands in the part of the panel the column leaves, as large as the text
            // preference asks and the room allows.
            float column = conversationColumn != null ? conversationColumn.sizeDelta.x + 12f : 0f;
            float areaWidth = Mathf.Max(1f, modal.sizeDelta.x - column);
            float areaHeight = Mathf.Max(1f, modal.sizeDelta.y);
            dialScale = Mathf.Clamp(Mathf.Min(FontScale, areaWidth / DialBlockWidth, areaHeight / DialBlockHeight), .7f, FontScale);

            dialRoot = new GameObject(DialName, typeof(RectTransform)).GetComponent<RectTransform>();
            dialRoot.SetParent(modal, false);
            // Before the column in the hierarchy, so Tab reaches the topics first: they are what a
            // conversation opens on.
            if (conversationColumn != null) dialRoot.SetSiblingIndex(conversationColumn.GetSiblingIndex());
            dialRoot.anchorMin = dialRoot.anchorMax = new Vector2(0f, 0f);
            dialRoot.pivot = new Vector2(.5f, 0f);
            // In the middle of the free area, at its foot: the two-shot stands the pair either side
            // of that middle with their faces in the upper third, so the ring is under them, over
            // their knees, as mockup-12's is.
            dialRoot.anchoredPosition = new Vector2(column + areaWidth * .5f, 0f);
            dialRoot.sizeDelta = new Vector2(DialBlockWidth * dialScale, DialBlockHeight * dialScale);

            var centre = new Vector2(0f, DialCentreLift * dialScale);
            // A dark disc behind the petals, so the ring reads as one object over the set rather
            // than seven buttons scattered on it, then the pack's ring and its glowing hub.
            var ground = DialPiece("Dial ground", UiTheme.Circle(),
                new Color(UiTheme.Background.r, UiTheme.Background.g, UiTheme.Background.b, .55f), centre,
                2f * (DialRadius + PetalSize * .5f + 8f) * dialScale);
            ground.preserveAspect = true;
            DialPiece("Dial ring", UiTheme.Pack(PackArt.WheelRing), new Color(1f, 1f, 1f, .45f), centre,
                2f * DialRadius / .826f * dialScale);
            float hub = DialHub * dialScale;
            var hubArt = UiTheme.Pack(PackArt.WheelHub);
            if (hubArt != null) DialPiece("Dial hub", hubArt, Color.white, centre, hub / .733f);
            else
            {
                var disc = DialPiece("Dial hub", UiTheme.Circle(), UiTheme.SurfaceRaised, centre, hub);
                UiTheme.AddBorder(disc.rectTransform, Mathf.RoundToInt(hub * .5f) - 1, UiTheme.Hairline);
            }

            var line = NewText(dialRoot, DialPrompt, 14, UiTheme.Paper);
            line.alignment = TextAlignmentOptions.Center;
            AutoSize(line, 10);
            line.rectTransform.anchorMin = line.rectTransform.anchorMax = new Vector2(.5f, 0f);
            line.rectTransform.pivot = new Vector2(.5f, .5f);
            line.rectTransform.anchoredPosition = centre;
            line.rectTransform.sizeDelta = new Vector2(92f * dialScale, 60f * dialScale);
        }

        /// <summary>One piece of the dial's dressing, centred on the ring. Nothing on it takes a click.</summary>
        private Image DialPiece(string name, Sprite sprite, Color colour, Vector2 centre, float side)
        {
            var image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            image.rectTransform.SetParent(dialRoot, false);
            image.rectTransform.anchorMin = image.rectTransform.anchorMax = new Vector2(.5f, 0f);
            image.rectTransform.pivot = new Vector2(.5f, .5f);
            image.rectTransform.anchoredPosition = centre;
            image.rectTransform.sizeDelta = new Vector2(side, side);
            image.sprite = sprite;
            image.color = sprite != null ? colour : new Color(0f, 0f, 0f, 0f);
            image.preserveAspect = true;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>
        /// Seats one action on the dial: a glass disc with the glyph and the colour the mockup gives
        /// that kind of conversation, and under the glyph, inside the disc, the caption the build
        /// already used for it. Clockwise from the top, in the order the director fills the seats.
        /// </summary>
        public Button Petal(string caption, string icon, Color tint, Action action)
        {
            if (dialRoot == null || topicTaken >= topicSeats) return Action(caption, action);

            int seat = topicTaken++;
            float angle = seat * Mathf.PI * 2f / topicSeats;
            float size = PetalSize * dialScale;
            int radius = Mathf.Max(1, Mathf.FloorToInt(size * .5f) - 1);
            var rect = Panel(caption, dialRoot, new Color(UiTheme.Background.r, UiTheme.Background.g, UiTheme.Background.b, .96f), radius);
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, 0f);
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle)) * DialRadius * dialScale
                + new Vector2(0f, DialCentreLift * dialScale);
            rect.sizeDelta = new Vector2(size, size);
            // The petal's edge is structure; its action colour lives on the glyph, which is the thing
            // the colour is about.
            UiTheme.AddBorder(rect, radius, UiTheme.Edge(UiTheme.Emphasis.Interactive));
            HudEmphasis.Promote(rect, UiTheme.Emphasis.Interactive);
            var button = Pressable(rect, action);
            // A petal is a coloured glyph on a dark disc: brighten the disc a little on focus, never
            // wash the glyph's colour toward white, which is what a 1.35 ramp did.
            var colours = button.colors;
            colours.highlightedColor = new Color(1.18f, 1.18f, 1.18f);
            colours.selectedColor = colours.highlightedColor;
            button.colors = colours;

            // Glass: a lighter wash over the top half of the disc, as the mockup's discs catch the
            // light from above.
            var wash = new GameObject("Petal wash", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            wash.rectTransform.SetParent(rect, false);
            wash.rectTransform.anchorMin = Vector2.zero; wash.rectTransform.anchorMax = Vector2.one;
            wash.rectTransform.offsetMin = new Vector2(3f, 3f); wash.rectTransform.offsetMax = new Vector2(-3f, -3f);
            wash.sprite = UiTheme.Circle(); wash.preserveAspect = true; wash.raycastTarget = false;
            wash.color = new Color(UiTheme.Accent.r, UiTheme.Accent.g, UiTheme.Accent.b, .10f);
            wash.transform.SetAsFirstSibling();

            float side = PetalGlyphSide * dialScale;
            HudPrimitives.Glyph("Petal mark", rect, icon, tint,
                new Vector2((size - side) * .5f, -PetalGlyphTop * dialScale), side);

            var label = NewText(rect, caption, 12, Paper);
            var medium = UiTheme.Font(UiTheme.Weight.Medium);
            if (medium != null) label.font = medium;
            label.alignment = TextAlignmentOptions.Top;
            label.lineSpacing = -8f;
            label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(.5f, 1f);
            label.rectTransform.pivot = new Vector2(.5f, 1f);
            label.rectTransform.anchoredPosition = new Vector2(0f, -(PetalGlyphTop + PetalGlyphSide + 3f) * dialScale);
            label.rectTransform.sizeDelta = new Vector2(PetalCaptionWidth * dialScale, PetalCaptionHeight * dialScale);
            // The captions are sentences of very different lengths in a disc of one size, so the
            // longest of them is allowed to shrink rather than to clip.
            AutoSize(label, 9);
            return button;
        }

        /// <summary>
        /// Moves the keyboard to the first row after the dial, which the panel then scrolls to.
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
            if (content == null || dialSeat == null || dialSeat.parent != content) return null;
            // After the seat, not merely outside the dial: an oath declaration is drawn before it,
            // and handing the keyboard backwards to it would be the opposite of what More says.
            int step = after ? 1 : -1;
            for (int i = dialSeat.GetSiblingIndex() + step; i >= 0 && i < content.childCount; i += step)
            {
                var controls = content.GetChild(i).GetComponentsInChildren<Selectable>()
                    .Where(item => item.IsActive() && item.IsInteractable() && !(item is Scrollbar));
                var adjacent = after ? controls.FirstOrDefault() : controls.LastOrDefault();
                if (adjacent != null) return adjacent;
            }
            return null;
        }

        /// <summary>
        /// The control a panel opens on: the dial's first petal when nothing in the column comes
        /// before the dial in reading order - which is where a conversation starts - and otherwise
        /// the first control in the column, as for every other panel.
        /// </summary>
        private Selectable OpeningControl(Selectable[] eligible)
        {
            if (dialRoot != null && dialSeat != null && content != null && dialSeat.parent == content)
            {
                bool earlier = false;
                for (int i = 0; i < dialSeat.GetSiblingIndex() && !earlier; i++)
                    earlier = content.GetChild(i).GetComponentsInChildren<Selectable>().Any(item => item.IsActive() && item.IsInteractable());
                if (!earlier)
                {
                    var petal = eligible.FirstOrDefault(item => item.transform.IsChildOf(dialRoot));
                    if (petal != null) return petal;
                }
            }
            return eligible.FirstOrDefault(item => content != null && item.transform.IsChildOf(content));
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
            // A neutral ramp, because these multiply whatever the control already is.
            colours.highlightedColor = new Color(1.35f, 1.35f, 1.35f);
            colours.selectedColor = colours.highlightedColor;
            colours.pressedColor = new Color(.85f, .85f, .85f);
            button.colors = colours;
            var press = rect.gameObject.AddComponent<HudPress>();
            press.ReducedMotion = ReducedMotion;
            press.Hovered = () => Foley(HouseAudio.Cue.Hover);
            button.onClick.AddListener(() =>
            {
                // A retained UnityEvent can outlive its control, or be invoked directly while
                // disabled. Match real button eligibility before calling the bounded UI action.
                if (button == null || !button.IsActive() || !button.IsInteractable()) return;
                action?.Invoke();
            });
            return button;
        }
    }
}
