using System.Collections.Generic;
using Gamesim.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gamesim.Presentation
{
    /// <summary>
    /// A final Head of Household part on the game screen (MOCKUP-PASS-PLAN M13, mockup 58): the part
    /// tracker in the challenge card with how every part is scored, the gold "winner advances" band
    /// on the legend row, and, where two play, the pair as cards down the field.
    ///
    /// <para>Only the player's real progress is drawn (decision 53): the other competitor's score
    /// does not exist until the commit, so their card says who they are, never how they are doing.
    /// The tracker and the band are built only when the director hands the screen a bracket; an
    /// ordinary week's competition is drawn as it always was.</para>
    /// </summary>
    public sealed partial class CompetitionGameScreen
    {
        /// <summary>A competitor card's photo at the resting size: a head-and-shoulders column, as the mockup's cards frame theirs.</summary>
        private static readonly Vector2 CompetitorPhoto = new Vector2(96f, 110f);

        private FinalBracket finalBracket;
        private RectTransform bracketStrip, finalBand;
        private TMP_Text scoringLabel;
        private Image scoringMark;
        private bool scoringShown;
        private float finalBandWidth;

        /// <summary>One of the two competitor cards and the parts a relayout re-places.</summary>
        private sealed class CompetitorCard
        {
            public RectTransform root, photo;
            public TMP_Text eyebrow;
            public readonly List<(RectTransform chip, float width)> chips = new List<(RectTransform, float)>();
        }

        private readonly List<CompetitorCard> competitorCards = new List<CompetitorCard>();

        // ---------------------------------------------------------------- the challenge card

        /// <summary>
        /// The tracker and the scoring line, in the challenge card under the game's name. Nothing
        /// for an ordinary week's competition, which hands the screen no bracket.
        /// </summary>
        private void BuildFinalPartChallenge()
        {
            bracketStrip = null; scoringLabel = null; scoringMark = null;
            if (finalBracket == null) return;
            bracketStrip = new GameObject(FinalBracketView.BracketName, typeof(RectTransform)).GetComponent<RectTransform>();
            bracketStrip.SetParent(challengeCard, false);
            FinalBracketView.Draw(bracketStrip, finalBracket, FontScale, compact: true);
            // The trophy where the pack is installed; without it the line stands alone rather than
            // beside a plain gold square.
            var trophy = UiTheme.Pack(PackArt.IconTrophy);
            if (trophy != null) scoringMark = Picture("Scoring mark", challengeCard, trophy, UiTheme.Gold);
            scoringLabel = Label("Scoring rule", challengeCard, FinalBracket.ScoringLine, 12, 0, 0, 10, 10,
                new Color(UiTheme.Paper.r, UiTheme.Paper.g, UiTheme.Paper.b, .86f));
            scoringLabel.alignment = TextAlignmentOptions.TopLeft; Fit(scoringLabel, 10);
        }

        /// <summary>
        /// The header row's height once a final part's tracker and scoring line join the ordinary
        /// card's <paramref name="challenge"/> content. They come out of the board, but never out
        /// of the board its game needs: Pressure Cooker's rows are laid at fixed heights, and on a
        /// wide, short frame at the larger text the bracket squeezed them into the hold button. So
        /// there the scoring line gives way first, then the rules shrink to fit, and the header
        /// never drops below the <paramref name="ordinary"/> one. Without a bracket the ordinary
        /// height is the answer.
        /// </summary>
        private float FinalPartHeaderFor(float ordinary, float challenge, float timer, float inner, float frameHeight)
        {
            scoringShown = false;
            if (bracketStrip == null) return ordinary;
            float strip = FinalBracketView.StripHeight * FontScale + 8f;
            float scoring = PreferredHeight(scoringLabel, inner - 22f) + 6f;
            float full = Mathf.Clamp(Mathf.Max(HeaderHeight, challenge + strip + scoring, timer), HeaderHeight, 340f);
            float lean = Mathf.Clamp(Mathf.Max(HeaderHeight, challenge + strip, timer), HeaderHeight, 340f);
            // The most the header may take and still leave the board its floor. The ordinary card
            // is never cut: a frame too short for it was short before the bracket came.
            float room = Mathf.Max(ordinary, frameHeight - (FrameEdge + FrameGap) - (FrameEdge + LegendHeight * FontScale + 8f) - BoardFloor);
            scoringShown = full <= room;
            return Mathf.Min(scoringShown ? full : lean, room);
        }

        /// <summary>
        /// The board height, band included, that the attempt's game cannot do without: the grip
        /// panel's rows and their margin for an endurance game. Every other board scales to the
        /// field it is given, so the frame's own floor is enough for them.
        /// </summary>
        private float BoardFloor => run != null && run.Kind == CompetitionMiniGames.Kind.Endurance ? BandHeight + EnduranceHeight + 24f : 0f;

        /// <summary>
        /// Places the tracker under the title and the scoring line over the attempt's policy, and
        /// returns where the rules start and where they must end.
        /// </summary>
        private (float rulesY, float rulesBottom) LayoutFinalPartChallenge(float inner, float rulesY, float policyY)
        {
            float rulesBottom = policyY - 8f;
            if (bracketStrip == null) return (rulesY, rulesBottom);
            float strip = FinalBracketView.StripHeight * FontScale;
            Place(bracketStrip, 16f, rulesY, inner, strip);
            rulesY += strip + 8f;
            // On a frame too short for it the scoring line gives way to the board (FinalPartHeaderFor);
            // a later, taller frame brings it back.
            scoringLabel.gameObject.SetActive(scoringShown);
            if (scoringMark != null) scoringMark.gameObject.SetActive(scoringShown);
            if (!scoringShown) return (rulesY, rulesBottom);
            float scoring = PreferredHeight(scoringLabel, inner - 22f);
            float scoringY = policyY - 8f - scoring;
            if (scoringMark != null) Place(scoringMark.rectTransform, 16f, scoringY + 1f, 14f * FontScale, 14f * FontScale);
            Place(scoringLabel.rectTransform, 16f + 22f, scoringY, inner - 22f, scoring);
            return (rulesY, scoringY - 6f);
        }

        // ---------------------------------------------------------------- the legend row

        /// <summary>The gold band at the right-hand end of the legend row, saying what the part's winner goes on to.</summary>
        private void BuildFinalBand()
        {
            finalBand = null; finalBandWidth = 0f;
            if (finalBracket == null || string.IsNullOrEmpty(finalBracket.advance)) return;
            finalBand = FinalBracketView.Band(panel, finalBracket.advance, LegendHeight * FontScale, 13f * FontScale, out finalBandWidth);
        }

        /// <summary>Places the band at the row's end and returns the room it leaves the keys.</summary>
        private float LayoutFinalBand(float x, float y, float width, float height)
        {
            if (finalBand == null) return width;
            float band = Mathf.Min(finalBandWidth, width * .45f);
            Place(finalBand, x + width - band, y, band, height);
            return Mathf.Max(120f, width - band - 16f);
        }

        // ---------------------------------------------------------------- the competitor cards

        /// <summary>
        /// A two-entrant part's pair as cards: a photo, a line saying they are in it, two traits,
        /// and a blue or a red identity for the first and the second. Built before the field's list
        /// so the list's names draw on the cards: the list is still the field's words.
        /// </summary>
        private void BuildCompetitorCards(IList<CompetitionEntrant> entrants)
        {
            competitorCards.Clear();
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            for (int i = 0; i < entrants.Count; i++)
            {
                var entrant = entrants[i];
                var identity = i == 0 ? UiTheme.Accent : UiTheme.Danger;
                var card = new CompetitorCard();
                card.root = HudPrimitives.Fill("Competitor card", fieldCard, new Color(UiTheme.Ink.r, UiTheme.Ink.g, UiTheme.Ink.b, .55f), 10);
                card.root.GetComponent<Image>().raycastTarget = false;
                UiTheme.AddBorder(card.root, 10, new Color(identity.r, identity.g, identity.b, .85f));
                var stripe = HudPrimitives.Fill("Identity stripe", card.root, identity, 2);
                stripe.anchorMin = new Vector2(0f, 1f); stripe.anchorMax = new Vector2(1f, 1f); stripe.pivot = new Vector2(.5f, 1f);
                stripe.offsetMin = new Vector2(12f, -3f); stripe.offsetMax = new Vector2(-12f, 0f);
                card.photo = HudPrimitives.RectPortrait(card.root, "Competitor photo", entrant.Portrait, entrant.Character, CompetitorPhoto, 8);
                card.eyebrow = Label("Competitor eyebrow", card.root, "IN THE COMPETITION", 10, 0, 0, 10, 10, identity);
                if (semibold != null) card.eyebrow.font = semibold;
                card.eyebrow.characterSpacing = 2f; card.eyebrow.alignment = TextAlignmentOptions.TopLeft;
                Fit(card.eyebrow, 8);
                var traits = entrant.Character != null ? entrant.Character.traits : null;
                if (traits != null)
                    for (int t = 0; t < traits.Count && t < 2; t++)
                    {
                        string word = Localisation.Text(traits[t]);
                        var chip = SizedChip("Trait", card.root, word, CastSelect.TraitTint(traits[t]), false);
                        var chipWord = chip.GetComponentInChildren<TMP_Text>();
                        if (chipWord != null) Fit(chipWord, 8);
                        card.chips.Add((chip, chip.sizeDelta.x));
                    }
                competitorCards.Add(card);
            }
        }

        /// <summary>
        /// The two cards down the field card, each following its line of the field's list as the
        /// faces of a bigger field do: the name on the card is the list's own line. On a short
        /// frame the photos give way, never the names.
        /// </summary>
        private void LayoutCompetitorCards(float fieldHeight)
        {
            float s = FontScale, line = 18f * s;
            float listTop = 14f + line + 12f, box = fieldHeight - listTop - 70f;
            float width = SideWidth - 24f, gap = 10f;
            float nameSize = 17f * s, name = Mathf.Ceil(nameSize * 1.3f);
            float photoHeight = Mathf.Clamp((box - gap) * .5f - 10f - 8f - name - 10f, 48f, CompetitorPhoto.y);
            var photo = new Vector2(Mathf.Round(photoHeight * CompetitorPhoto.x / CompetitorPhoto.y), photoHeight);
            float height = 10f + photo.y + 8f + name + 10f, pitch = height + gap;
            float nameTop = 10f + photo.y + 8f;

            entrantsLabel.fontSize = nameSize;
            entrantsLabel.lineSpacing = Mathf.Max(0f, (pitch - nameSize * 1.25f) / nameSize * 100f);
            Place(entrantsLabel.rectTransform, 12f + 10f, listTop + nameTop, width - 20f, pitch * competitorCards.Count);
            Place(arenaStatus.rectTransform, 16f, fieldHeight - 60f, SideWidth - 32f, 50f);
            entrantsLabel.ForceMeshUpdate();
            var info = entrantsLabel.textInfo;

            float textX = 10f + photo.x + 10f, textWidth = Mathf.Max(40f, width - textX - 10f);
            float eyebrowBox = Mathf.Ceil(10f * s * 1.3f) * 2f;
            float chipHeight = 22f * s;
            for (int i = 0; i < competitorCards.Count; i++)
            {
                var card = competitorCards[i];
                // Where this card's name is: its line of the list, or where the line would be.
                float middle = i < info.lineCount
                    ? -(listTop + nameTop) + (info.lineInfo[i].ascender + info.lineInfo[i].descender) * .5f
                    : -(listTop + nameTop) - i * pitch - name * .5f;
                float top = middle + name * .5f + nameTop;
                card.root.anchorMin = card.root.anchorMax = card.root.pivot = new Vector2(0f, 1f);
                card.root.anchoredPosition = new Vector2(12f, top);
                card.root.sizeDelta = new Vector2(width, height);
                Place(card.photo, 10f, 10f, photo.x, photo.y);
                Place(card.eyebrow.rectTransform, textX, 12f, textWidth, eyebrowBox);
                float chipY = 12f + eyebrowBox + 6f;
                foreach (var (chip, natural) in card.chips)
                {
                    // A chip that would hang below the photo is left out rather than drawn over the name.
                    bool fits = chipY + chipHeight <= 10f + photo.y;
                    chip.gameObject.SetActive(fits);
                    if (!fits) continue;
                    Place(chip, textX, chipY, Mathf.Min(natural, textWidth), chipHeight);
                    chipY += chipHeight + 5f;
                }
            }
        }
    }
}
