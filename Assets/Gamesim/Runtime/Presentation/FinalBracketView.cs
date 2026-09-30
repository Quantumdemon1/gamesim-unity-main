using Gamesim.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gamesim.Presentation
{
    /// <summary>
    /// The final Head of Household's part tracker and its "winner advances" band (MOCKUP-PASS-PLAN
    /// M13, mockup 58), drawn once for both places the player meets them: the head and foot of the
    /// briefing's sheet, and the game screen's challenge card and legend row.
    ///
    /// <para>A part played carries a check, its number, its winner and their face. The part being
    /// played carries its players' faces, lit in the accent. A part ahead carries a lock, and the
    /// seat already won when there is one. Every word is <see cref="FinalBracket"/>'s, from the
    /// public record; nothing here takes a click.</para>
    /// </summary>
    public static class FinalBracketView
    {
        /// <summary>Names the tracker and the band carry, so a test finds them the way it finds a named panel.</summary>
        public const string BracketName = "Final HoH bracket";
        public const string BandName = "Final part band";
        public const string BandLineName = "Final part band line";
        public const string PartLineName = "Bracket line";
        public const string PartEyebrowName = "Bracket eyebrow";
        public static string PartName(int number) => "Bracket part " + number;

        /// <summary>The tracker's height at the resting text size: the sheet's roomy row, and the challenge card's strip.</summary>
        public const float SheetHeight = 72f, StripHeight = 46f;

        /// <summary>
        /// Lays the three parts side by side across <paramref name="row"/>, a third of its width
        /// each, however wide the caller made it. <paramref name="compact"/> is the challenge
        /// card's strip: smaller faces, one line of words.
        /// </summary>
        public static void Draw(RectTransform row, FinalBracket bracket, float scale, bool compact)
        {
            if (row == null || bracket == null) return;
            int count = bracket.parts.Count;
            for (int i = 0; i < count; i++) Cell(row, bracket.parts[i], i, count, scale, compact);
        }

        private static void Cell(RectTransform row, FinalBracket.Part part, int index, int count, float s, bool compact)
        {
            bool playing = part.standing == FinalBracket.Standing.Playing;
            bool ahead = part.standing == FinalBracket.Standing.Ahead;
            // The part in hand is the lit one; a part ahead sits back, a part played rests.
            var ground = playing ? new Color(UiTheme.Accent.r, UiTheme.Accent.g, UiTheme.Accent.b, .12f)
                : new Color(UiTheme.Ink.r, UiTheme.Ink.g, UiTheme.Ink.b, ahead ? .35f : .6f);
            const int radius = 10;
            var cell = HudPrimitives.Fill(PartName(part.number), row, ground, radius);
            cell.GetComponent<Image>().raycastTarget = false;
            float gap = (compact ? 6f : 8f) * s;
            cell.anchorMin = new Vector2((float)index / count, 0f);
            cell.anchorMax = new Vector2((float)(index + 1) / count, 1f);
            cell.pivot = new Vector2(.5f, .5f);
            cell.offsetMin = new Vector2(index == 0 ? 0f : gap * .5f, 0f);
            cell.offsetMax = new Vector2(index == count - 1 ? 0f : -gap * .5f, 0f);
            UiTheme.AddBorder(cell, radius, playing ? UiTheme.Edge(UiTheme.Emphasis.Active)
                : new Color(UiTheme.Outline.r, UiTheme.Outline.g, UiTheme.Outline.b, ahead ? .45f : .7f));

            float pad = (compact ? 8f : 10f) * s;
            float face = (compact ? 24f : 32f) * s, ring = 2f;
            // Faces overlap by a third, as a pair on the mockup's card does, so three still leave
            // the words their room.
            float step = Mathf.Round(face * .64f);
            float x = pad;
            var tint = playing ? UiTheme.Accent : part.standing == FinalBracket.Standing.Played ? UiTheme.Gold : UiTheme.Outline;
            if (part.faces.Count > 0)
            {
                for (int f = 0; f < part.faces.Count; f++)
                {
                    var actor = part.faces[f];
                    var rim = HudPrimitives.Portrait(cell, CharacterPortraits.Get(actor), tint, face, ring, ahead, actor);
                    rim.name = "Bracket face";
                    LeftMiddle(rim, x + f * step);
                }
                x += face + 2f * ring + (part.faces.Count - 1) * step;
                if (part.openSeat)
                {
                    // The seat nobody has won yet: an empty disc with a question in it.
                    var seat = HudPrimitives.Disc("Open seat", cell, new Color(UiTheme.Outline.r, UiTheme.Outline.g, UiTheme.Outline.b, .8f));
                    seat.sizeDelta = new Vector2(face + 2f * ring, face + 2f * ring);
                    LeftMiddle(seat, x - face - 2f * ring + step);
                    var hole = HudPrimitives.Disc("Open seat hole", seat, UiTheme.SurfaceRaised);
                    hole.anchorMin = Vector2.zero; hole.anchorMax = Vector2.one;
                    hole.offsetMin = new Vector2(ring, ring); hole.offsetMax = new Vector2(-ring, -ring);
                    var mark = HudPrimitives.Label("Open seat mark", hole, Mathf.Round(face * .5f), UiTheme.Muted, TextAlignmentOptions.Center);
                    mark.text = "?";
                    mark.rectTransform.anchorMin = Vector2.zero; mark.rectTransform.anchorMax = Vector2.one;
                    mark.rectTransform.offsetMin = mark.rectTransform.offsetMax = Vector2.zero;
                    x += step;
                }
            }
            else
            {
                // Nobody holds a seat yet: the lock stands where the faces will.
                var disc = HudPrimitives.Disc("Bracket lock", cell, new Color(UiTheme.Outline.r, UiTheme.Outline.g, UiTheme.Outline.b, .45f));
                disc.sizeDelta = new Vector2(face + 2f * ring, face + 2f * ring);
                LeftMiddle(disc, x);
                var lockArt = Mark(PackArt.KitIconLock, "lock");
                if (lockArt != null)
                {
                    var glyph = new GameObject("Lock mark", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                    glyph.rectTransform.SetParent(disc, false);
                    glyph.rectTransform.anchorMin = glyph.rectTransform.anchorMax = new Vector2(.5f, .5f);
                    glyph.rectTransform.sizeDelta = new Vector2(face * .5f, face * .5f);
                    glyph.sprite = lockArt; glyph.color = UiTheme.Muted; glyph.preserveAspect = true; glyph.raycastTarget = false;
                }
                x += face + 2f * ring;
            }
            x += (compact ? 8f : 10f) * s;

            // The words: the part's number over what it says, a check before a part played and a
            // lock before a part ahead.
            float eyebrowSize = Mathf.Round((compact ? 10f : 11f) * s);
            float eyebrowBox = Mathf.Ceil(eyebrowSize * 1.3f);
            float top = compact ? Mathf.Round(6f * s) : pad;
            float words = x;
            string status = part.standing == FinalBracket.Standing.Played ? PackArt.KitIconCheck : ahead ? PackArt.KitIconLock : null;
            var statusArt = status != null ? Mark(status, status == PackArt.KitIconCheck ? "check" : "lock") : null;
            if (statusArt != null)
            {
                var icon = new GameObject("Bracket status", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                icon.rectTransform.SetParent(cell, false);
                icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = new Vector2(0f, 1f);
                icon.rectTransform.pivot = new Vector2(0f, 1f);
                icon.rectTransform.anchoredPosition = new Vector2(words, -top - 1f);
                icon.rectTransform.sizeDelta = new Vector2(eyebrowSize, eyebrowSize);
                icon.sprite = statusArt; icon.preserveAspect = true; icon.raycastTarget = false;
                icon.color = part.standing == FinalBracket.Standing.Played ? UiTheme.Positive : UiTheme.Muted;
                words += eyebrowSize + 5f * s;
            }
            var eyebrow = HudPrimitives.Heading(PartEyebrowName, cell, eyebrowSize, playing ? UiTheme.Accent : UiTheme.Muted);
            eyebrow.text = Localisation.Text("PART") + " " + part.number + (playing ? "  ·  " + Localisation.Text("NOW") : "");
            eyebrow.characterSpacing = 3f;
            eyebrow.textWrappingMode = TextWrappingModes.NoWrap;
            Fit(eyebrow, 8f);
            Row(eyebrow.rectTransform, words, pad, top, eyebrowBox);

            float lineSize = Mathf.Round((compact ? 12f : 13f) * s);
            float lineTop = top + eyebrowBox + (compact ? 1f : 3f) * s;
            float lineBox = Mathf.Max(Mathf.Ceil(lineSize * 1.3f), ((compact ? StripHeight : SheetHeight) * s) - lineTop - (compact ? 4f : 8f) * s);
            var line = HudPrimitives.Label(PartLineName, cell, lineSize, ahead ? UiTheme.Muted : UiTheme.Paper);
            line.text = Localisation.Text(part.line);
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            if (semibold != null && playing) line.font = semibold;
            // The strip keeps to one line; the sheet's row lets three names take two.
            line.textWrappingMode = compact ? TextWrappingModes.NoWrap : TextWrappingModes.Normal;
            line.alignment = TextAlignmentOptions.TopLeft;
            Fit(line, compact ? 9f : 10f);
            Row(line.rectTransform, x, pad, lineTop, lineBox);
        }

        /// <summary>
        /// The "winner advances" band: a gold-edged foot with the crown and what the part's winner
        /// goes on to. Decoration beside the stakes, which the briefing's hero states in full.
        /// <paramref name="width"/> is the width its words want; the caller places and sizes it.
        /// </summary>
        public static RectTransform Band(Transform parent, string line, float height, float fontSize, out float width)
        {
            int radius = Mathf.Clamp(Mathf.RoundToInt(height * .5f) - 1, 4, 14);
            var band = HudPrimitives.Fill(BandName, parent, new Color(UiTheme.Ink.r, UiTheme.Ink.g, UiTheme.Ink.b, .88f), radius);
            band.GetComponent<Image>().raycastTarget = false;
            // A wash of the gold under the words, and the gold edge round it: the colour the final
            // Head of Household is played for, as the phase band's part line wears it.
            var wash = HudPrimitives.Fill("Band wash", band, new Color(UiTheme.Gold.r, UiTheme.Gold.g, UiTheme.Gold.b, .10f), radius);
            wash.anchorMin = Vector2.zero; wash.anchorMax = Vector2.one; wash.offsetMin = wash.offsetMax = Vector2.zero;
            UiTheme.AddBorder(band, radius, new Color(UiTheme.Gold.r, UiTheme.Gold.g, UiTheme.Gold.b, .9f));

            float pad = Mathf.Round(height * .35f), icon = Mathf.Round(fontSize * 1.25f), gap = Mathf.Round(fontSize * .6f);
            var crownArt = Mark(PackArt.KitIconCrown, "crown");
            float x = pad;
            if (crownArt != null)
            {
                var crown = new GameObject("Band crown", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                crown.rectTransform.SetParent(band, false);
                crown.rectTransform.anchorMin = crown.rectTransform.anchorMax = new Vector2(0f, .5f);
                crown.rectTransform.pivot = new Vector2(0f, .5f);
                crown.rectTransform.anchoredPosition = new Vector2(x, 0f);
                crown.rectTransform.sizeDelta = new Vector2(icon, icon);
                crown.sprite = crownArt; crown.color = UiTheme.Gold; crown.preserveAspect = true; crown.raycastTarget = false;
                x += icon + gap;
            }
            var words = HudPrimitives.Label(BandLineName, band, fontSize, UiTheme.Paper, TextAlignmentOptions.MidlineLeft);
            words.text = Localisation.Text(line);
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            if (semibold != null) words.font = semibold;
            words.textWrappingMode = TextWrappingModes.NoWrap;
            Fit(words, Mathf.Min(10f, fontSize));
            var rect = words.rectTransform;
            rect.anchorMin = new Vector2(0f, .5f); rect.anchorMax = new Vector2(1f, .5f); rect.pivot = new Vector2(0f, .5f);
            float box = Mathf.Min(height - 2f, Mathf.Ceil(fontSize * 1.3f) + 2f);
            rect.offsetMin = new Vector2(x, -box * .5f); rect.offsetMax = new Vector2(-pad, box * .5f);
            width = x + Mathf.Ceil(words.GetPreferredValues(words.text).x) + 2f + pad;
            return band;
        }

        /// <summary>A kit icon where the refinement kit is installed, the HUD icon set's otherwise.</summary>
        private static Sprite Mark(string pack, string icon)
        {
            var sprite = UiTheme.Pack(pack);
            if (sprite == null) sprite = UiTheme.Icon(icon);
            return sprite;
        }

        private static void LeftMiddle(RectTransform rect, float x)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, .5f);
            rect.pivot = new Vector2(0f, .5f);
            rect.anchoredPosition = new Vector2(x, 0f);
        }

        /// <summary>A row across the cell from <paramref name="left"/> to <paramref name="right"/> short of its edge.</summary>
        private static void Row(RectTransform rect, float left, float right, float top, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f); rect.anchorMax = new Vector2(1f, 1f); rect.pivot = new Vector2(0f, 1f);
            rect.offsetMin = new Vector2(left, -top - height); rect.offsetMax = new Vector2(-right, -top);
        }

        private static void Fit(TMP_Text label, float floor)
        {
            label.enableAutoSizing = true; label.fontSizeMax = label.fontSize; label.fontSizeMin = Mathf.Min(floor, label.fontSize);
        }
    }
}
