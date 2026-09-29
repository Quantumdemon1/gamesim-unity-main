using System.Collections.Generic;
using System.Linq;
using Gamesim.Presentation;
using Gamesim.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gamesim.Episode
{
    /// <summary>
    /// The jury house (ENDGAME-PLAN F4, mockup 36): a card per juror - their photo, the word for
    /// where they stand with the player and why, what the two of them share, what the player
    /// did after they left, and the dated lines between them - in rows that wrap for a jury of
    /// any size, one to fourteen. Observe only: nothing on a card is a control.
    /// </summary>
    public sealed partial class EpisodeHud
    {
        /// <summary>The parts a test finds by name.</summary>
        public const string JurorCardPrefix = "Juror · ", JurorBandName = "Juror band", JuryMattersName = "What matters to this jury";

        /// <summary>A juror's card: who, and the read of them.</summary>
        public struct JurorCard
        {
            public ContestantState Actor;
            public JuryHouseRead.Juror Read;
        }

        /// <summary>A band's colour: warm in the allied green, wavering in amber, cold in the warning orange, bitter in red, the rest muted.</summary>
        public static Color BandTint(string band)
        {
            switch (band)
            {
                case JuryHouseRead.Supportive: return UiTheme.Allied;
                case JuryHouseRead.Wavering: return UiTheme.Joke;
                case JuryHouseRead.Skeptical: return UiTheme.Warning;
                case JuryHouseRead.Bitter: return UiTheme.Danger;
                case JuryHouseRead.Open: return UiTheme.Paper;
                default: return UiTheme.Muted;
            }
        }

        private const int JurorLinesShown = 3;

        /// <summary>
        /// The jurors as cards, three to a row on a wide stage at the resting text, two on a
        /// narrower one, one at the larger text; a short last row keeps the others' width.
        /// </summary>
        public void JurorCards(IList<JurorCard> cards)
        {
            if (content == null || cards == null || cards.Count == 0) return;
            float s = FontScale, width = ContentWidth(), gap = 14f * s;
            int perRow = FontScale > 1.05f ? (width >= 760f ? 2 : 1) : width >= 900f ? 3 : width >= 600f ? 2 : 1;
            float cardWidth = (width - gap * (perRow - 1)) / perRow;
            for (int start = 0; start < cards.Count; start += perRow)
            {
                var row = new GameObject("Juror row", typeof(RectTransform), typeof(HorizontalLayoutGroup)).GetComponent<RectTransform>();
                row.SetParent(content, false);
                var layout = row.GetComponent<HorizontalLayoutGroup>();
                layout.spacing = gap; layout.childAlignment = TextAnchor.UpperLeft;
                layout.childControlWidth = layout.childControlHeight = true;
                layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
                for (int i = start; i < start + perRow; i++)
                {
                    if (i < cards.Count) JurorCardIn(row, cards[i], cardWidth);
                    else
                    {
                        // An empty slot, so the last row's cards keep the width of the rows above.
                        var spacer = new GameObject("Juror slot", typeof(RectTransform), typeof(LayoutElement)).GetComponent<RectTransform>();
                        spacer.SetParent(row, false);
                        var element = spacer.GetComponent<LayoutElement>();
                        element.minWidth = 0f; element.preferredWidth = cardWidth; element.flexibleWidth = 1f;
                    }
                }
            }
        }

        private void JurorCardIn(RectTransform row, JurorCard juror, float width)
        {
            float s = FontScale;
            var actor = juror.Actor;
            var read = juror.Read;
            var card = HudPrimitives.KitCard(JurorCardPrefix + actor.name, row, false, 12f);
            foreach (Transform decoration in card) decoration.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            var element = card.gameObject.AddComponent<LayoutElement>();
            element.minWidth = 0f; element.preferredWidth = width; element.flexibleWidth = 1f;
            var layout = card.gameObject.AddComponent<VerticalLayoutGroup>();
            int pad = Mathf.RoundToInt(14f * s);
            layout.padding = new RectOffset(pad, pad, pad, pad);
            layout.spacing = 3f * s;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
            PushContent(card, width - 2f * pad);

            var photoRow = new GameObject("Photo row", typeof(RectTransform), typeof(LayoutElement)).GetComponent<RectTransform>();
            photoRow.SetParent(content, false);
            var photoSize = new Vector2(72f, 88f) * s;
            photoRow.GetComponent<LayoutElement>().minHeight = photoSize.y;
            var photo = HudPrimitives.RectPortrait(photoRow, "Photo", CharacterPortraits.Get(actor), actor, photoSize, 8);
            photo.anchorMin = photo.anchorMax = new Vector2(.5f, 1f);
            photo.pivot = new Vector2(.5f, 1f); photo.anchoredPosition = Vector2.zero;

            var name = FlowText(actor.name, 17, Paper);
            name.alignment = TextAlignmentOptions.Center;
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            if (semibold != null) name.font = semibold;
            if (!string.IsNullOrEmpty(read?.trait)) FlowText("Leads with " + read.trait, 12, UiTheme.Muted).alignment = TextAlignmentOptions.Center;
            if (read != null)
            {
                var band = FlowText(read.band.ToUpperInvariant(), 15, BandTint(read.band));
                band.name = JurorBandName;
                band.alignment = TextAlignmentOptions.Center;
                band.characterSpacing = 4f;
                if (semibold != null) band.font = semibold;
                FlowText(read.reason, 13, Paper).alignment = TextAlignmentOptions.Center;
                JurorLines("KNOWS", read.knows);
                JurorLines("MISSED", read.missing);
                JurorLines("BETWEEN YOU", read.highlights);
            }
            PopContent();
        }

        private void JurorLines(string heading, IList<string> lines)
        {
            if (lines == null || lines.Count == 0) return;
            var eyebrow = FlowText(heading, 11, Accent);
            eyebrow.characterSpacing = 5f;
            foreach (var line in lines.Take(JurorLinesShown)) FlowText(line, 12, UiTheme.Muted);
            if (lines.Count > JurorLinesShown) FlowText("and " + (lines.Count - JurorLinesShown) + " more", 12, UiTheme.Muted);
        }
    }
}
