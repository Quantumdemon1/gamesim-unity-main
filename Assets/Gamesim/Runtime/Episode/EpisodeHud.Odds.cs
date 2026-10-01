using Gamesim.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gamesim.Episode
{
    /// <summary>
    /// What the chances beside a deal or a plea are (ACTIONS-DEALS-ALLIANCES-PLAN V6): a note that
    /// they are the player's read of the houseguest, not a promise, and a chip when that read has
    /// little to go on - once a panel, however many tables of chances it draws. And an offer's yes
    /// that cannot be given, drawn locked.
    /// </summary>
    public sealed partial class EpisodeHud
    {
        /// <summary>The note above a table of chances, named so a test finds it as it finds a panel.</summary>
        public const string OddsNoteName = "Odds note";

        /// <summary>The chip's name, and its words.</summary>
        public const string UnknownsChipName = "Unknowns chip";
        public const string UnknownsChipWords = "Many unknowns";

        /// <summary>The lock where a locked row's chevron would be.</summary>
        public const string LockMarkName = "Lock mark";

        /// <summary>
        /// The panel the odds were explained on. Every render builds a new panel (<see cref="Begin"/>),
        /// so this goes stale - is cleared, in effect - at the start of each one, and a conversation
        /// that draws the plea's chances and the deal table's says what they are once, above the first.
        /// </summary>
        private RectTransform oddsExplainedOn;

        /// <summary>Whether this render's panel has already said what its chances are.</summary>
        public bool OddsExplained => modal != null && oddsExplainedOn == modal;

        /// <summary>
        /// Says what the chances under it are, once a render: the chip when the read has little to
        /// go on, then the note. The note is a flowing line, so it wraps rather than clips at either
        /// text size and its box grows with it; the chip is as wide as its words.
        /// </summary>
        public void ExplainOdds(string note, bool manyUnknowns)
        {
            if (content == null || string.IsNullOrEmpty(note) || OddsExplained) return;
            oddsExplainedOn = modal;
            if (manyUnknowns) UnknownsChip();
            FlowText(note, 15, UiTheme.Muted).gameObject.name = OddsNoteName;
        }

        /// <summary>
        /// A chip on a row of its own: the read behind the chances under it has little to go on. The
        /// chip sets its word at half its own height, twice the room TMP needs to draw it.
        /// </summary>
        private void UnknownsChip()
        {
            float s = FontScale, height = 24f * s;
            var row = new GameObject(UnknownsChipName, typeof(RectTransform), typeof(LayoutElement)).GetComponent<RectTransform>();
            row.SetParent(content, false);
            var element = row.GetComponent<LayoutElement>();
            element.minHeight = element.preferredHeight = height;
            string words = Localisation.Text(UnknownsChipWords);
            var chip = HudPrimitives.Chip("Chip", row, words, UiTheme.Warning, (words.Length * 7.5f + 24f) * s, height);
            float width = chip.sizeDelta.x;
            var label = chip.GetComponentInChildren<TMP_Text>();
            if (label != null) width = Mathf.Max(width, Mathf.Ceil(label.GetPreferredValues(words).x) + 24f * s);
            Anchor(chip, new Vector2(0f, .5f), new Vector2(0f, .5f), Vector2.zero, new Vector2(Mathf.Min(ContentWidth(), width), height));
            var ground = chip.GetComponent<Image>();
            if (ground != null) ground.raycastTarget = false;
        }

        /// <summary>
        /// A row the player can see and cannot press, drawn the way the house draws a locked option
        /// (<see cref="StoryChoices"/>, <see cref="StoryTile"/>): a resting edge that does not lift
        /// under the pointer, the caption muted, a lock where an open row's chevron would be, and no
        /// press or hover cue. Still a <see cref="Button"/> named and captioned exactly as the open
        /// one, so a test and a screen reader find it by the same words and learn it is unavailable;
        /// it commits nothing even if something presses it.
        /// </summary>
        public Button LockedAction(string caption)
        {
            var rect = Chrome(caption, content, UiTheme.Emphasis.Resting);
            rect.gameObject.AddComponent<LayoutElement>().minHeight = 57 * FontScale;
            float mark = 18f * FontScale;
            var button = rect.gameObject.AddComponent<Button>();
            button.interactable = false;
            var text = NewText(rect, caption, 20, UiTheme.Muted);
            Stretch(text.rectTransform, 16f, 5, 16f + mark + 10f, 5);
            text.alignment = TextAlignmentOptions.Left;
            var glyph = HudPrimitives.Glyph(LockMarkName, rect, "lock", UiTheme.Muted, Vector2.zero, mark);
            if (glyph != null)
            {
                var place = glyph.rectTransform;
                place.anchorMin = place.anchorMax = new Vector2(1f, .5f);
                place.pivot = new Vector2(1f, .5f);
                place.anchoredPosition = new Vector2(-16f, 0f);
            }
            return button;
        }
    }
}
