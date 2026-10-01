using Gamesim.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gamesim.Episode
{
    /// <summary>
    /// What the chances beside a deal or a plea are (ACTIONS-DEALS-ALLIANCES-PLAN V6): a note that
    /// they are the player's read of the houseguest, not a promise, and a chip when that read has
    /// little to go on. Words, never controls, so neither has a caption to keep.
    /// </summary>
    public sealed partial class EpisodeHud
    {
        /// <summary>The note above a table of chances, named so a test finds it as it finds a panel.</summary>
        public const string OddsNoteName = "Odds note";

        /// <summary>The chip's name, and its words.</summary>
        public const string UnknownsChipName = "Unknowns chip";
        public const string UnknownsChipWords = "Many unknowns";

        /// <summary>
        /// A line of muted copy in the column: what the chances under it are. A flowing line, so it
        /// wraps rather than clips at either text size, and its box grows with it.
        /// </summary>
        public void OddsNote(string words)
        {
            if (content == null || string.IsNullOrEmpty(words)) return;
            FlowText(words, 15, UiTheme.Muted).gameObject.name = OddsNoteName;
        }

        /// <summary>
        /// A chip on a row of its own: the read behind the chances under it has little to go on. As
        /// wide as its words at the size it is drawn, so it never clips; the chip sets its word at
        /// half its own height, twice the room TMP needs to draw it.
        /// </summary>
        public void UnknownsChip()
        {
            if (content == null) return;
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
    }
}
