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
    /// The final Head of Household's briefings (MOCKUP-PASS-PLAN M13, mockup 58): the part tracker
    /// at the head of the sheet, the two who play a part as cards when the player watches it, and a
    /// gold band at the sheet's foot saying what the part's winner goes on to.
    ///
    /// <para>All of it decorates the briefing the week's competitions already have. The stakes line
    /// in the hero is still the explanation (<see cref="EpisodeDirector.FinalPartStakes"/>), every
    /// control keeps its caption, and nothing here takes a click. Every word comes from
    /// <see cref="FinalBracket"/>, which reads the public record only.</para>
    /// </summary>
    public sealed partial class EpisodeHud
    {
        /// <summary>The row the spectator's two competitor cards stand in, so a test can find it.</summary>
        public const string CompetitorCardsName = "Competitor cards";
        /// <summary>The words on each competitor card under the name, as the mockup's cards carry them.</summary>
        public const string InTheCompetition = "IN THE COMPETITION";

        /// <summary>A competitor card's photo on the sheet, at every text size: the mockup's head-and-shoulders column.</summary>
        private static readonly Vector2 CompetitorPhotoSize = new Vector2(96f, 110f);

        /// <summary>
        /// The part tracker as a row of three at the head of a final part's briefing. Draws nothing
        /// outside the final Head of Household's parts.
        /// </summary>
        public void FinalHoHBracket(EpisodeState state)
        {
            var bracket = FinalBracket.For(state);
            if (content == null || bracket == null) return;
            float height = Mathf.Round(FinalBracketView.SheetHeight * FontScale);
            var row = new GameObject(FinalBracketView.BracketName, typeof(RectTransform), typeof(LayoutElement)).GetComponent<RectTransform>();
            row.SetParent(content, false);
            var size = row.GetComponent<LayoutElement>();
            size.minHeight = size.preferredHeight = height;
            row.sizeDelta = new Vector2(ContentWidth(), height);
            FinalBracketView.Draw(row, bracket, FontScale, compact: false);
        }

        /// <summary>
        /// The "winner advances" band at the foot of a final part's briefing: the crown and what the
        /// part's winner goes on to, gold-edged, the width of the column. Nothing in other weeks.
        /// </summary>
        public void FinalPartBand(EpisodeState state)
        {
            string line = state != null ? FinalBracket.AdvanceLine(state.phase) : null;
            if (content == null || string.IsNullOrEmpty(line)) return;
            float height = Mathf.Round(40f * FontScale);
            var band = FinalBracketView.Band(content, line, height, Mathf.Round(15f * FontScale), out _);
            var element = band.gameObject.AddComponent<LayoutElement>();
            element.minHeight = element.preferredHeight = height;
        }

        /// <summary>
        /// The two who play a final part, as cards side by side under the hero: a photo, the name,
        /// "IN THE COMPETITION", two traits, and a blue or a red identity for the first and the
        /// second, as the game screen draws the same pair. Who they are, never how they are doing:
        /// no score exists before the commit.
        /// </summary>
        public void CompetitorCards(IList<ContestantState> field)
        {
            if (content == null || field == null || field.Count != 2) return;
            float s = FontScale;
            var row = new GameObject(CompetitorCardsName, typeof(RectTransform), typeof(LayoutElement)).GetComponent<RectTransform>();
            row.SetParent(content, false);
            float height = CompetitorPhotoSize.y + 20f;
            var size = row.GetComponent<LayoutElement>();
            size.minHeight = size.preferredHeight = height;
            row.sizeDelta = new Vector2(ContentWidth(), height);
            float gap = 12f * s;
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            for (int i = 0; i < field.Count; i++)
            {
                var actor = field[i];
                if (actor == null) continue;
                var identity = i == 0 ? UiTheme.Accent : UiTheme.Danger;
                var card = Panel("Competitor card", row, new Color(UiTheme.Ink.r, UiTheme.Ink.g, UiTheme.Ink.b, .55f), UiTheme.GlassRadius);
                card.GetComponent<Image>().raycastTarget = false;
                card.anchorMin = new Vector2(i * .5f, 0f); card.anchorMax = new Vector2((i + 1) * .5f, 1f);
                card.pivot = new Vector2(.5f, .5f);
                card.offsetMin = new Vector2(i == 0 ? 0f : gap * .5f, 0f);
                card.offsetMax = new Vector2(i == 0 ? -gap * .5f : 0f, 0f);
                UiTheme.AddBorder(card, UiTheme.GlassRadius, new Color(identity.r, identity.g, identity.b, .85f));
                var stripe = Panel("Identity stripe", card, identity, 2);
                stripe.GetComponent<Image>().raycastTarget = false;
                stripe.anchorMin = new Vector2(0f, 1f); stripe.anchorMax = new Vector2(1f, 1f); stripe.pivot = new Vector2(.5f, 1f);
                stripe.offsetMin = new Vector2(14f, -3f); stripe.offsetMax = new Vector2(-14f, 0f);

                var photo = HudPrimitives.RectPortrait(card, "Competitor photo", CharacterPortraits.Get(actor), actor, CompetitorPhotoSize, 8);
                Anchor(photo, new Vector2(0, 1), new Vector2(0, 1), new Vector2(10f, -10f), CompetitorPhotoSize);

                // The words beside the photo: the name, the line saying they play, two traits.
                float x = 10f + CompetitorPhotoSize.x + 12f, y = 12f;
                var name = NewText(card, HudPrimitives.WithYou(actor.name, actor.isPlayer), 18, Paper);
                if (semibold != null) name.font = semibold;
                name.textWrappingMode = TextWrappingModes.NoWrap;
                AutoSize(name, 12);
                float nameBox = Mathf.Ceil(name.fontSize * 1.3f);
                Row(name.rectTransform, x, 10f, y, nameBox);
                y += nameBox + 2f * s;
                var eyebrow = NewText(card, InTheCompetition, 11, identity);
                if (semibold != null) eyebrow.font = semibold;
                eyebrow.characterSpacing = 3f;
                eyebrow.textWrappingMode = TextWrappingModes.NoWrap;
                AutoSize(eyebrow, 8);
                float eyebrowBox = Mathf.Ceil(eyebrow.fontSize * 1.3f);
                Row(eyebrow.rectTransform, x, 10f, y, eyebrowBox);
                y += eyebrowBox + 8f * s;

                // Two traits, as the ballot's cards show them: the card is a person, not a number.
                float textWidth = Mathf.Max(60f, (ContentWidth() - gap) * .5f - x - 10f);
                float chipX = x, chipHeight = Mathf.Round(20f * s);
                foreach (var trait in (actor.traits ?? new List<string>()).Take(2))
                {
                    string word = Localisation.Text(trait);
                    float chipWidth = Mathf.Min((textWidth - 6f * s) * .5f, (word.Length * 7f + 20f) * s);
                    var chip = HudPrimitives.Chip("Trait", card, word, CastSelect.TraitTint(trait), chipWidth, chipHeight);
                    chip.GetComponent<Image>().raycastTarget = false;
                    Anchor(chip, new Vector2(0, 1), new Vector2(0, 1), new Vector2(chipX, -y), chip.sizeDelta);
                    var chipWord = chip.GetComponentInChildren<TMP_Text>();
                    if (chipWord != null) AutoSize(chipWord, 8);
                    chipX += chipWidth + 6f * s;
                }
            }
        }

        /// <summary>
        /// The part's art in the hero's photo, for a final part the player watches: the crown over
        /// the gold it is played for, and the part's number under it.
        /// </summary>
        private void PartArt(RectTransform photo, string label, float photoWidth, float height)
        {
            var glowArt = UiTheme.Pack(PackArt.GlowGold);
            var glow = new GameObject("Part art glow", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            glow.rectTransform.SetParent(photo, false);
            glow.rectTransform.anchorMin = Vector2.zero; glow.rectTransform.anchorMax = Vector2.one;
            glow.rectTransform.offsetMin = glow.rectTransform.offsetMax = Vector2.zero;
            glow.raycastTarget = false;
            if (glowArt != null) { glow.sprite = glowArt; glow.color = new Color(1f, 1f, 1f, .75f); }
            else { glow.sprite = UiTheme.Circle(); glow.color = new Color(UiTheme.Gold.r, UiTheme.Gold.g, UiTheme.Gold.b, .12f); }

            float side = Mathf.Round(Mathf.Min(photoWidth, height) * .42f);
            var crownArt = UiTheme.Pack(PackArt.KitIconCrown);
            if (crownArt == null) crownArt = UiTheme.Icon("crown");
            if (crownArt != null)
            {
                var crown = new GameObject("Part art crown", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                crown.rectTransform.SetParent(photo, false);
                Anchor(crown.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0f, height * .12f), new Vector2(side, side));
                crown.sprite = crownArt; crown.color = UiTheme.Gold; crown.preserveAspect = true; crown.raycastTarget = false;
            }
            var words = NewText(photo, label, 20, UiTheme.Gold);
            var bold = UiTheme.Font(UiTheme.Weight.Bold);
            if (bold != null) words.font = bold;
            words.characterSpacing = 4f;
            words.alignment = TextAlignmentOptions.Center;
            words.textWrappingMode = TextWrappingModes.NoWrap;
            AutoSize(words, 12);
            float box = Mathf.Ceil(words.fontSize * 1.3f);
            Anchor(words.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, 1f), new Vector2(0f, height * .12f - side * .5f - 6f * FontScale),
                new Vector2(Mathf.Max(40f, photoWidth - 16f), box));
        }

        /// <summary>A row across a card from <paramref name="left"/> to <paramref name="right"/> short of its right edge.</summary>
        private static void Row(RectTransform rect, float left, float right, float top, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f); rect.anchorMax = new Vector2(1f, 1f); rect.pivot = new Vector2(0f, 1f);
            rect.offsetMin = new Vector2(left, -top - height); rect.offsetMax = new Vector2(-right, -top);
        }
    }
}
