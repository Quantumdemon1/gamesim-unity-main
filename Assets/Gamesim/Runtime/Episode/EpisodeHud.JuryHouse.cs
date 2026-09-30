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
    /// The jury house (ENDGAME-PLAN F4, mockup 36; MOCKUP-PASS-PLAN M12, mockup 56): a dashboard
    /// over the stage. A head with the jury's size, compact cards up to seven across - the photo,
    /// the name, a status mark and the word for where they stand with the player, and one line of
    /// each thing the record holds - and under them a 2D tableau of the jury in a U, each juror a
    /// callout in their own recorded words, beside a card of the season's highlights and what the
    /// jury values. Rows wrap for a jury of any size, one to fourteen. Observe only: nothing on a
    /// card, a callout or the tableau is a control.
    /// </summary>
    public sealed partial class EpisodeHud
    {
        /// <summary>The parts a test finds by name.</summary>
        public const string JurorCardPrefix = "Juror · ", JurorBandName = "Juror band", JuryMattersName = "What matters to this jury";

        /// <summary>The mockup pass's parts: the highlights card, the tableau and its callouts, and the observe-only chip.</summary>
        public const string JuryHighlightsName = "Jury highlights", JuryTableauName = "Jury tableau", JurorCalloutPrefix = "Juror callout · ",
            ObserveOnlyName = "Observe only";

        /// <summary>A juror's card: who, the name the card shows, and the read of them.</summary>
        public struct JurorCard
        {
            public ContestantState Actor;
            public JuryHouseRead.Juror Read;
            /// <summary>The name shown: the first name, or the full name when two jurors share one. The card's object keeps the full name.</summary>
            public string Name;
            /// <summary>Under the finale rules: the theme the juror values and the answers that land with them (ENDGAME-PLAN F4b).</summary>
            public IList<string> Swayed;
        }

        /// <summary>A juror in the tableau: who, the name shown, their band and their callout line.</summary>
        public struct JurorCallout
        {
            public ContestantState Actor;
            public string Name, Band;
            public JuryHouseRead.Callout Line;
        }

        /// <summary>
        /// A band's colour: warm in the allied green, wavering in amber, cold in the warning orange,
        /// bitter in red, open in the HUD's glow, the rest muted. Open was the paper white, which
        /// read as no colour at all beside the others; it is never the accent, which is the cards'
        /// eyebrow colour.
        /// </summary>
        public static Color BandTint(string band)
        {
            switch (band)
            {
                case JuryHouseRead.Supportive: return UiTheme.Allied;
                case JuryHouseRead.Wavering: return UiTheme.Joke;
                case JuryHouseRead.Skeptical: return UiTheme.Warning;
                case JuryHouseRead.Bitter: return UiTheme.Danger;
                case JuryHouseRead.Open: return UiTheme.Glow;
                default: return UiTheme.Muted;
            }
        }

        /// <summary>One line of each section on a card, and "and n more" under it.</summary>
        private const int JurorLinesShown = 1;
        /// <summary>The most cards a row holds, and the narrowest a card is drawn, at the resting text size.</summary>
        private const int JurorsPerRow = 7;
        private const float JurorCardMin = 140f, JurorCardMax = 240f;
        /// <summary>A callout's height in the tableau at the resting text size, and the widest one runs.</summary>
        private const float CalloutHeight = 116f, CalloutWidest = 250f;

        // ------------------------------------------------------------ the frame

        /// <summary>
        /// A screen over the stage that is a place of its own (MOCKUP-PASS M12). The reading cap
        /// lifts, so a row of cards can run the stage's width; the station band's title becomes the
        /// screen's name, with the week kept under it; and the control hint stands down as a house
        /// event's does, which lets the scroll start under the band. Called before the screen's
        /// first row, because rows measure the column as they are built.
        /// </summary>
        public void StageAsPlace(string title, string glyph = null)
        {
            if (modal == null) return;
            UncapContent();
            var hint = modal.Find(PanelHintName);
            if (hint != null) hint.gameObject.SetActive(false);
            if (modalScroll != null)
            {
                var scroll = (RectTransform)modalScroll.transform;
                scroll.offsetMax = new Vector2(scroll.offsetMax.x, -72f);
            }
            var band = modal.Find("Phase band");
            if (band == null) return;
            // PhaseBand builds its title before the week's line under it, so the first label is the title.
            var words = band.GetComponentsInChildren<TMP_Text>(true);
            if (words.Length > 0 && !string.IsNullOrEmpty(title)) words[0].text = Localisation.Text(title).ToUpperInvariant();
            var mark = band.Find("Phase glyph");
            var sprite = glyph != null ? UiTheme.Icon(glyph) : null;
            if (mark != null && sprite != null) mark.GetComponent<Image>().sprite = sprite;
        }

        /// <summary>
        /// The jury house's head, left-aligned: the people glyph, the screen's name over the jury's
        /// size in words and a line of what the screen is, and on the right a non-interactive
        /// "Observe only" chip with the eye, with <paramref name="aside"/> under it when there is
        /// something that can act. The whole row carries the screen head's name, so the words a
        /// test reads the head by are all inside it.
        /// </summary>
        public void JuryHouseHead(string title, string headline, string line, string aside)
        {
            if (content == null) return;
            float s = FontScale, width = ContentWidth(), gap = 14f * s, mark = 40f * s;
            float chipColumn = Mathf.Min(260f * s, width * .3f);
            var head = new GameObject(ScreenHeadName, typeof(RectTransform), typeof(HorizontalLayoutGroup)).GetComponent<RectTransform>();
            head.SetParent(content, false);
            var layout = head.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = gap; layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = false; layout.childForceExpandHeight = false;

            var holder = new GameObject("Head mark", typeof(RectTransform), typeof(LayoutElement)).GetComponent<RectTransform>();
            holder.SetParent(head, false);
            var size = holder.GetComponent<LayoutElement>();
            size.minWidth = size.preferredWidth = mark; size.minHeight = size.preferredHeight = mark;
            HudPrimitives.Glyph("Head glyph", holder, "people", Accent, new Vector2(0f, -4f * s), mark);

            float words = Mathf.Max(120f, width - mark - chipColumn - 2f * gap);
            PushContent(HeadColumn("Head words", head, words, 1f), words);
            var name = FlowText(title, 15, Accent);
            var bold = UiTheme.Font(UiTheme.Weight.Bold);
            if (bold != null) name.font = bold;
            name.characterSpacing = 6f;
            var big = FlowText(headline, 26, UiTheme.Glow);
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            if (semibold != null) big.font = semibold;
            big.characterSpacing = 3f;
            big.name = "Headline";
            if (!string.IsNullOrEmpty(line)) FlowText(line, 15, UiTheme.Muted);
            PopContent();

            PushContent(HeadColumn("Head aside", head, chipColumn, 0f), chipColumn);
            var chipRow = new GameObject("Observe row", typeof(RectTransform), typeof(LayoutElement)).GetComponent<RectTransform>();
            chipRow.SetParent(content, false);
            var rowSize = chipRow.GetComponent<LayoutElement>();
            rowSize.minHeight = rowSize.preferredHeight = 30f * s;
            float chipHeight = 26f * s, chipWidth = 126f * s;
            var chip = HudPrimitives.Chip(ObserveOnlyName, chipRow, "Observe only", UiTheme.Heading, chipWidth, chipHeight);
            chip.anchorMin = chip.anchorMax = new Vector2(1f, 1f); chip.pivot = new Vector2(1f, 1f);
            chip.anchoredPosition = new Vector2(0f, -2f * s);
            if (HudPrimitives.Glyph("Observe mark", chip, "eye", UiTheme.Heading, new Vector2(10f * s, -(chipHeight - 14f * s) * .5f), 14f * s) != null)
            {
                var label = chip.Find("Word") as RectTransform;
                if (label != null) label.offsetMin = new Vector2(28f * s, label.offsetMin.y);
            }
            if (!string.IsNullOrEmpty(aside)) FlowText(aside, 12, UiTheme.Muted).alignment = TextAlignmentOptions.Right;
            PopContent();
        }

        private static RectTransform HeadColumn(string name, RectTransform parent, float width, float flexible)
        {
            var column = new GameObject(name, typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement)).GetComponent<RectTransform>();
            column.SetParent(parent, false);
            var layout = column.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 2f; layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
            var element = column.GetComponent<LayoutElement>();
            element.minWidth = 0f; element.preferredWidth = width; element.flexibleWidth = flexible;
            return column;
        }

        /// <summary>The bands as a legend: each one's status mark and its word, wrapping to a second line on a narrow column.</summary>
        public void BandLegend(IList<string> bands)
        {
            if (content == null || bands == null || bands.Count == 0) return;
            float s = FontScale, width = ContentWidth(), x = 0f, y = 0f, line = 20f * s;
            var row = new GameObject("Band legend", typeof(RectTransform), typeof(LayoutElement)).GetComponent<RectTransform>();
            row.SetParent(content, false);
            foreach (var band in bands)
            {
                var word = FixedText(row, band, 12, UiTheme.Muted, Vector2.zero, new Vector2(width, line));
                float need = 16f * s + word.GetPreferredValues(word.text).x + 18f * s;
                if (x > 0f && x + need > width) { x = 0f; y += line; }
                BandMark(row, band, new Vector2(x, -(y + 5f * s)), 10f * s);
                Anchor(word.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x + 16f * s, -y), new Vector2(need - 16f * s, line));
                x += need;
            }
            var element = row.GetComponent<LayoutElement>();
            element.minHeight = element.preferredHeight = y + line;
        }

        /// <summary>
        /// A band's status mark: a dot in its colour, or the question mark for Unknown - the one band
        /// that is not a reading at all. Colour is never the only signal: the word is beside it.
        /// </summary>
        private RectTransform BandMark(RectTransform parent, string band, Vector2 at, float side)
        {
            if (band == JuryHouseRead.Unknown)
            {
                var question = KitGlyph(parent, PackArt.KitIconQuestion, UiTheme.Muted, new Vector2(0f, 1f), new Vector2(at.x - side * .1f, at.y + side * .1f), side * 1.2f);
                if (question != null) { question.name = "Band mark"; return question.rectTransform; }
            }
            var dot = HudPrimitives.Disc("Band mark", parent, BandTint(band));
            Anchor(dot, new Vector2(0f, 1f), new Vector2(0f, 1f), at, new Vector2(side, side));
            return dot;
        }

        /// <summary>The status mark and the band's word after it, placed at <paramref name="at"/> in its parent, in the band's colour.</summary>
        private TMP_Text BandWord(RectTransform parent, string band, Vector2 at, float width, int size)
        {
            float s = FontScale, box = Mathf.Round(size * 1.5f) * s;
            BandMark(parent, band, new Vector2(at.x, at.y - (box - 10f * s) * .5f), 10f * s);
            var word = FixedText(parent, band.ToUpperInvariant(), size, BandTint(band), new Vector2(at.x + 16f * s, at.y), new Vector2(Mathf.Max(10f, width - 16f * s), box));
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            if (semibold != null) word.font = semibold;
            word.characterSpacing = 3f;
            word.textWrappingMode = TextWrappingModes.NoWrap;
            word.alignment = TextAlignmentOptions.MidlineLeft;
            AutoSize(word, 8);
            return word;
        }

        // ------------------------------------------------------------ the cards

        /// <summary>
        /// The jurors as compact cards: up to seven to a row where the column holds them, fewer
        /// where it does not, with a jury of eight to fourteen split into rows as even as they go.
        /// A card is never wider than a compact card needs, so a small jury is a short row in the
        /// middle rather than three cards stretched across the stage. A short last row keeps the
        /// others' width and place, and the cards of a row share its height.
        /// </summary>
        public void JurorCards(IList<JurorCard> cards)
        {
            if (content == null || cards == null || cards.Count == 0) return;
            float s = FontScale, width = ContentWidth(), gap = 12f * s;
            int fit = Mathf.Max(1, Mathf.FloorToInt((width + gap) / (JurorCardMin * s + gap)));
            int most = Mathf.Min(JurorsPerRow, fit);
            int rows = Mathf.CeilToInt(cards.Count / (float)most);
            int perRow = Mathf.CeilToInt(cards.Count / (float)rows);
            float cardWidth = Mathf.Floor(Mathf.Min((width - gap * (perRow - 1)) / perRow, JurorCardMax * s));
            int side = Mathf.Max(0, Mathf.FloorToInt((width - perRow * cardWidth - (perRow - 1) * gap) * .5f));
            for (int start = 0; start < cards.Count; start += perRow)
            {
                var row = new GameObject("Juror row", typeof(RectTransform), typeof(HorizontalLayoutGroup)).GetComponent<RectTransform>();
                row.SetParent(content, false);
                var layout = row.GetComponent<HorizontalLayoutGroup>();
                layout.spacing = gap; layout.childAlignment = TextAnchor.UpperLeft;
                layout.padding = new RectOffset(side, 0, 0, 0);
                layout.childControlWidth = layout.childControlHeight = true;
                layout.childForceExpandWidth = false; layout.childForceExpandHeight = true;
                for (int i = start; i < start + perRow; i++)
                {
                    if (i < cards.Count) JurorCardIn(row, cards[i], cardWidth);
                    else
                    {
                        // An empty slot, so the last row's cards keep the width and place of the rows above.
                        var spacer = new GameObject("Juror slot", typeof(RectTransform), typeof(LayoutElement)).GetComponent<RectTransform>();
                        spacer.SetParent(row, false);
                        var element = spacer.GetComponent<LayoutElement>();
                        element.minWidth = element.preferredWidth = cardWidth; element.flexibleWidth = 0f;
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
            element.minWidth = element.preferredWidth = width; element.flexibleWidth = 0f;
            var layout = card.gameObject.AddComponent<VerticalLayoutGroup>();
            int pad = Mathf.RoundToInt(10f * s);
            layout.padding = new RectOffset(pad, pad, pad, pad);
            layout.spacing = 3f * s;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
            float inner = width - 2f * pad;
            PushContent(card, inner);

            // The photo left of the name, and the band beside them where the card is wide enough
            // for its word; under them where it is not.
            var photo = new Vector2(44f, 52f) * s;
            float x = photo.x + 8f * s, room = inner - x;
            bool beside = room >= 96f * s;
            var faceRow = new GameObject("Face row", typeof(RectTransform), typeof(LayoutElement)).GetComponent<RectTransform>();
            faceRow.SetParent(content, false);
            var faceSize = faceRow.GetComponent<LayoutElement>();
            faceSize.minHeight = faceSize.preferredHeight = photo.y;
            var face = HudPrimitives.RectPortrait(faceRow, "Photo", CharacterPortraits.Get(actor), actor, photo, 8);
            face.anchorMin = face.anchorMax = new Vector2(0f, 1f);
            face.pivot = new Vector2(0f, 1f); face.anchoredPosition = Vector2.zero;
            var name = FixedText(faceRow, string.IsNullOrEmpty(juror.Name) ? actor.name : juror.Name, 15, Paper,
                new Vector2(x, beside ? -2f * s : 0f), new Vector2(room, beside ? 22f * s : photo.y));
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            if (semibold != null) name.font = semibold;
            name.alignment = beside ? TextAlignmentOptions.TopLeft : TextAlignmentOptions.MidlineLeft;
            AutoSize(name, 9);
            if (read != null)
            {
                TMP_Text band;
                if (beside) band = BandWord(faceRow, read.band, new Vector2(x, -26f * s), room, 12);
                else
                {
                    var bandRow = new GameObject("Band row", typeof(RectTransform), typeof(LayoutElement)).GetComponent<RectTransform>();
                    bandRow.SetParent(content, false);
                    var bandSize = bandRow.GetComponent<LayoutElement>();
                    bandSize.minHeight = bandSize.preferredHeight = 18f * s;
                    band = BandWord(bandRow, read.band, Vector2.zero, inner, 12);
                }
                band.name = JurorBandName;
                if (!string.IsNullOrEmpty(read.trait)) FlowText("Leads with " + read.trait, 11, UiTheme.Muted);
                FlowText(read.reason, 12, Paper);
                JurorLines("eye", "KNOWS", read.knows);
                JurorLines("exit", "MISSING", read.missing);
                JurorLines("target", "MAY BE SWAYED BY", juror.Swayed);
            }
            PopContent();
        }

        /// <summary>A card's section: a small glyph before its eyebrow, then its first line and how many more the record holds.</summary>
        private void JurorLines(string glyph, string heading, IList<string> lines)
        {
            if (lines == null || lines.Count == 0) return;
            float s = FontScale;
            var row = new GameObject("Section · " + heading, typeof(RectTransform), typeof(LayoutElement)).GetComponent<RectTransform>();
            row.SetParent(content, false);
            var element = row.GetComponent<LayoutElement>();
            element.minHeight = element.preferredHeight = 17f * s;
            var mark = HudPrimitives.Glyph("Section mark", row, glyph, Accent, new Vector2(0f, -1.5f * s), 14f * s);
            float x = mark != null ? 18f * s : 0f;
            var eyebrow = FixedText(row, heading, 10, Accent, new Vector2(x, 0f), new Vector2(Mathf.Max(10f, ContentWidth() - x), 17f * s));
            eyebrow.characterSpacing = 4f;
            eyebrow.textWrappingMode = TextWrappingModes.NoWrap;
            eyebrow.alignment = TextAlignmentOptions.MidlineLeft;
            AutoSize(eyebrow, 7);
            foreach (var line in lines.Take(JurorLinesShown)) FlowText(line, 12, UiTheme.Muted);
            if (lines.Count > JurorLinesShown) FlowText("and " + (lines.Count - JurorLinesShown) + " more", 11, UiTheme.Muted);
        }

        // ------------------------------------------------------------ the side cards

        /// <summary>One of what the jury values: the theme's glyph, the value in words, and how many of the jury hold it on a chip.</summary>
        public void MattersRow(string glyph, string words, string count)
        {
            if (content == null) return;
            float s = FontScale, width = ContentWidth();
            var row = new GameObject("Matters · " + words, typeof(RectTransform), typeof(LayoutElement)).GetComponent<RectTransform>();
            row.SetParent(content, false);
            var element = row.GetComponent<LayoutElement>();
            element.minHeight = element.preferredHeight = 26f * s;
            var mark = HudPrimitives.Glyph("Matters mark", row, glyph, Accent, new Vector2(0f, -3f * s), 20f * s);
            float x = mark != null ? 28f * s : 0f;
            float chip = Mathf.Clamp(count.Length * 7f + 20f, 52f, 96f) * s;
            var label = FixedText(row, words, 14, Paper, new Vector2(x, -2f * s), new Vector2(Mathf.Max(10f, width - x - chip - 8f * s), 22f * s));
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            AutoSize(label, 10);
            var pill = HudPrimitives.Chip("Matters count", row, count, UiTheme.Heading, chip, 20f * s);
            pill.anchorMin = pill.anchorMax = new Vector2(1f, 1f); pill.pivot = new Vector2(1f, 1f);
            pill.anchoredPosition = new Vector2(0f, -3f * s);
        }

        // ------------------------------------------------------------ the tableau

        /// <summary>
        /// The jury in 2D (decision 40, C): a U of seats, as the couches stand on finale night, and
        /// each juror a callout around it - their photo, their name, the status mark and band, and
        /// one line - with a leader to their seat. The callouts run down the left arm, along the
        /// base and up the right arm, so every leader is short and none crosses another.
        ///
        /// <para>The seats say nothing: nobody records where a juror sits, so a seat is only where
        /// the callout's leader lands. The line is the juror's own recorded words where the record
        /// holds any - tonight's question, a plea from the block - dated, and otherwise the band's
        /// reason (<see cref="JuryHouseRead.Callout"/>). None of it is a control.</para>
        /// </summary>
        public void JuryTableau(IList<JurorCallout> callouts)
        {
            if (content == null || callouts == null || callouts.Count == 0) return;
            float s = FontScale, width = ContentWidth(), pad = 16f * s, gap = 12f * s;
            int n = callouts.Count;
            // Two arms of callouts and, between them, a lounge at least as wide as a photo row.
            float cw = Mathf.Min(Mathf.Clamp(width * .26f, 180f * s, CalloutWidest * s), (width - 2f * pad - 4f * gap - 80f * s) * .5f);
            cw = Mathf.Max(120f * s, cw);
            float ch = CalloutHeight * s;
            // A third of the jury on each arm and the rest along the base, as many as the base holds;
            // the arms take what it does not, evenly where the numbers allow.
            int capacity = Mathf.Max(1, Mathf.FloorToInt((width - 2f * pad + gap) / (cw + gap)));
            int bottom = Mathf.Min(capacity, n - 2 * (n / 3));
            if ((n - bottom) % 2 == 1 && bottom > 1) bottom--;
            int arms = n - bottom, left = (arms + 1) / 2, right = arms / 2;

            float head = 46f * s, top = pad + head, couch = 16f * s;
            int armRows = Mathf.Max(left, right);
            float baseY = armRows > 0 ? top + armRows * (ch + gap) + couch * .5f : top + couch * .5f + 6f * s;
            float bottomTop = baseY + couch * .5f + 24f * s;
            float height = (bottom > 0 ? bottomTop + ch : baseY + couch) + pad;
            float loungeL = arms > 0 ? pad + cw + 2f * gap : pad + gap, loungeR = arms > 0 ? width - pad - cw - 2f * gap : width - pad - gap;

            var tableau = HudPrimitives.KitCard(JuryTableauName, content, false, 14f);
            var size = tableau.gameObject.AddComponent<LayoutElement>();
            size.minHeight = size.preferredHeight = height;

            var title = FixedText(tableau, "THE JURY LOUNGE", 13, UiTheme.Heading, new Vector2(pad, -12f * s), new Vector2(width - 2f * pad, 20f * s));
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            if (semibold != null) title.font = semibold;
            title.characterSpacing = 3f;
            var subtitle = FixedText(tableau, "Their own recorded words where the record has them, dated. Otherwise, why they stand where they do.", 12, UiTheme.Muted,
                new Vector2(pad, -34f * s), new Vector2(width - 2f * pad, 18f * s));
            AutoSize(subtitle, 9);

            // The couch: the arms as far down as their callouts go, and the base across the lounge.
            var cushion = new Color(UiTheme.SurfaceRaised.r, UiTheme.SurfaceRaised.g, UiTheme.SurfaceRaised.b, .9f);
            float armTop = top + ch * .5f - couch;
            if (left > 0) Block(tableau, "Couch", new Vector2(loungeL, armTop), new Vector2(couch, baseY + couch * .5f - armTop), cushion);
            if (right > 0) Block(tableau, "Couch", new Vector2(loungeR - couch, armTop), new Vector2(couch, baseY + couch * .5f - armTop), cushion);
            Block(tableau, "Couch", new Vector2(loungeL, baseY - couch * .5f), new Vector2(loungeR - loungeL, couch), cushion);

            // Where each callout stands and where its leader lands, in the order the U is traced.
            var places = new List<(Vector2 at, Vector2 from, Vector2 seat)>();
            for (int i = 0; i < left; i++)
            {
                float y = top + i * (ch + gap);
                places.Add((new Vector2(pad, y), new Vector2(pad + cw, y + ch * .5f), new Vector2(loungeL + couch * .5f, y + ch * .5f)));
            }
            float row = bottom * cw + (bottom - 1) * gap, x0 = (width - row) * .5f;
            for (int j = 0; j < bottom; j++)
            {
                float x = x0 + j * (cw + gap);
                float seatX = loungeL + couch + (j + .5f) * (loungeR - loungeL - 2f * couch) / bottom;
                places.Add((new Vector2(x, bottomTop), new Vector2(x + cw * .5f, bottomTop), new Vector2(seatX, baseY)));
            }
            for (int i = right - 1; i >= 0; i--)
            {
                float y = top + i * (ch + gap);
                places.Add((new Vector2(width - pad - cw, y), new Vector2(width - pad - cw, y + ch * .5f), new Vector2(loungeR - couch * .5f, y + ch * .5f)));
            }

            var leader = new Color(UiTheme.Hairline.r, UiTheme.Hairline.g, UiTheme.Hairline.b, .55f);
            for (int k = 0; k < n && k < places.Count; k++) Leader(tableau, places[k].from, places[k].seat, leader, 2f * s);
            for (int k = 0; k < n && k < places.Count; k++)
            {
                var ring = HudPrimitives.Disc("Seat", tableau, UiTheme.Hairline);
                Anchor(ring, new Vector2(0f, 1f), new Vector2(.5f, .5f), new Vector2(places[k].seat.x, -places[k].seat.y), new Vector2(16f * s, 16f * s));
                var core = HudPrimitives.Disc("Seat mark", ring, BandTint(callouts[k].Band));
                Anchor(core, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(9f * s, 9f * s));
            }
            for (int k = 0; k < n && k < places.Count; k++) CalloutAt(tableau, callouts[k], places[k].at, cw, ch);
        }

        /// <summary>A juror's callout: the photo, the name, the status mark and band, and their line under them.</summary>
        private void CalloutAt(RectTransform parent, JurorCallout callout, Vector2 at, float w, float h)
        {
            if (callout.Actor == null) return;
            float s = FontScale, pad = 8f * s;
            var card = HudPrimitives.KitCard(JurorCalloutPrefix + callout.Actor.name, parent, false, 10f);
            Anchor(card, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(at.x, -at.y), new Vector2(w, h));
            var photo = new Vector2(44f, 44f) * s;
            var face = HudPrimitives.RectPortrait(card, "Photo", CharacterPortraits.Get(callout.Actor), callout.Actor, photo, 6);
            Anchor(face, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(pad, -pad), photo);
            float x = pad + photo.x + 8f * s, room = w - x - pad;
            var name = FixedText(card, string.IsNullOrEmpty(callout.Name) ? callout.Actor.name : callout.Name, 14, Paper, new Vector2(x, -pad), new Vector2(room, 20f * s));
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            if (semibold != null) name.font = semibold;
            AutoSize(name, 9);
            BandWord(card, callout.Band, new Vector2(x, -(pad + 24f * s)), room, 11).name = "Callout band";
            var line = callout.Line;
            if (line == null) return;
            float lineTop = pad + photo.y + 6f * s, lineWidth = w - 2f * pad;
            // A recorded question or plea is cut to its first sentence, and shorter still on a narrow
            // callout or once tonight's answer is in, whose note takes the rest of the room; the
            // reason is the read's own short line and is shown whole. The name is on the callout
            // already, so the note is drawn without it (Callout.Brief).
            int after = string.IsNullOrEmpty(line.noteTail) ? 0 : line.noteTail.Length + 3;
            int limit = Mathf.Clamp(Mathf.FloorToInt(lineWidth / (6.2f * s)) * 2 - after, 40, 110);
            string text = line.Brief(line.quoted ? EndScreenKit.Excerpt(line.words, limit) ?? line.words : line.words);
            var words = FixedText(card, text, 12, line.quoted ? Paper : UiTheme.Muted, new Vector2(pad, -lineTop), new Vector2(lineWidth, h - lineTop - pad));
            if (line.quoted) words.fontStyle = FontStyles.Italic;
            words.name = "Callout line";
            AutoSize(words, 9);
        }

        /// <summary>A flat rounded block in the tableau, placed from its top-left corner.</summary>
        private static void Block(RectTransform parent, string name, Vector2 at, Vector2 size, Color colour)
        {
            var block = HudPrimitives.Fill(name, parent, colour, 6);
            Anchor(block, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(at.x, -at.y), size);
        }

        /// <summary>A thin line from one point to another in the tableau, both measured from its top-left corner, y down.</summary>
        private static void Leader(RectTransform parent, Vector2 from, Vector2 to, Color colour, float thickness)
        {
            var delta = to - from;
            float length = delta.magnitude;
            if (length < 1f) return;
            var line = HudPrimitives.Fill("Leader", parent, colour, 1);
            var middle = (from + to) * .5f;
            Anchor(line, new Vector2(0f, 1f), new Vector2(.5f, .5f), new Vector2(middle.x, -middle.y), new Vector2(length, thickness));
            // The tableau measures y down; the canvas turns anticlockwise with y up.
            line.localRotation = Quaternion.Euler(0f, 0f, -Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        }
    }
}
