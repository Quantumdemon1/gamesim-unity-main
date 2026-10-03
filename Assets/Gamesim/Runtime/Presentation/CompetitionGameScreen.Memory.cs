using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gamesim.Presentation
{
    /// <summary>
    /// The memory board: sixteen cards, four by four, as large as the field leaves them.
    ///
    /// <para>A card is a tile with a symbol, not a sentence. The faces are the web game's own
    /// (<c>BB_SYMBOLS</c>: house, key, trophy, target, eye, shield - with a star and a crown for its
    /// lightning and circus tent, which the icon set does not have). Face down shows a back mark;
    /// face up the symbol; no match a red edge and a cross; matched a green check on a card that
    /// steps back. Each state differs by mark and brightness as well as hue.</para>
    ///
    /// <para>The caption under the symbol is still the card's name - "Card 6", "Card 6: EYE",
    /// "Card 6: EYE · MATCHED" - because that name is how a screen reader, a test and the season
    /// walk read the board.</para>
    /// </summary>
    public sealed partial class CompetitionGameScreen
    {
        private static readonly string[] Faces = { "HOUSE", "KEY", "TROPHY", "TARGET", "EYE", "SHIELD", "STAR", "CROWN" };
        /// <summary>The public name of a revealed face, shared with the arena's tile console.</summary>
        public static string MemoryFaceName(int face) => Faces[face % Faces.Length];
        private static readonly Color FaceUpFill = UiTheme.Hex("173F76");
        private const float FlipSeconds = .16f;

        private readonly Button[] cards = new Button[16];
        private readonly TMP_Text[] cardLabels = new TMP_Text[16];
        private readonly RectTransform[] cardArt = new RectTransform[16];
        private readonly Image[] cardTiles = new Image[16], cardEdges = new Image[16], cardFaces = new Image[16];
        private readonly Image[] cardBacks = new Image[16], matchedBadges = new Image[16], mismatchBadges = new Image[16];
        private readonly RectTransform[] focusRings = new RectTransform[16];
        private readonly bool[] shownUp = new bool[16];
        private readonly float[] flipLeft = new float[16];
        private RectTransform pairTray, mistakesChip, previewMeter, previewFill;
        private readonly Image[] pairSlots = new Image[8], pairIcons = new Image[8];
        private TMP_Text mistakesLabel, previewLabel;
        private int shownPairs, shownMistakes;
        private bool shownPlaying;
        private float cardWords = 1f;

        /// <summary>The symbol for a face, or null where the icon set is missing - the caption still names it.</summary>
        private static Sprite FaceArt(int face)
        {
            switch (face)
            {
                case 0: return UiTheme.Icon("house");
                case 1: return UiTheme.Icon("key");
                case 2: return UiTheme.Icon("trophy");
                case 3: return UiTheme.Icon("target");
                case 4: return UiTheme.Icon("eye");
                case 5: return UiTheme.Pack(PackArt.KitIconShield);
                case 6: return UiTheme.Icon("star");
                default: return UiTheme.Icon("crown");
            }
        }

        private void BuildMemory(Action<int> flip)
        {
            for (int i = 0; i < 16; i++)
            {
                int card = i;
                // The button is an invisible hit area; what is seen is its art, which can turn.
                var root = HudPrimitives.Fill("Memory card " + (i + 1), playArea, new Color(0f, 0f, 0f, 0f), 8);
                var hit = root.GetComponent<Image>(); hit.raycastTarget = true;
                var button = root.gameObject.AddComponent<Button>();
                button.targetGraphic = hit;
                // No colour tint: the default transition greyed every card to half while the board
                // was not yet live, and the stage's lettering read through them.
                Untinted(button);
                button.onClick.AddListener(() => flip(card));
                cards[i] = button;

                var art = new GameObject("Card art", typeof(RectTransform)).GetComponent<RectTransform>();
                art.SetParent(root, false); Stretch(art, 0f, 0f, 0f, 0f);
                cardArt[i] = art;
                var tile = new GameObject("Tile", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                tile.rectTransform.SetParent(art, false); Stretch(tile.rectTransform, 0f, 0f, 0f, 0f);
                tile.raycastTarget = false;
                if (!UiTheme.PackSliced(tile, PackArt.KitCardFill, 12f, UiTheme.SurfaceRaised)) UiTheme.Style(tile, UiTheme.SurfaceRaised, 12);
                cardTiles[i] = tile;
                var edge = new GameObject("Border", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                edge.rectTransform.SetParent(art, false); Stretch(edge.rectTransform, 0f, 0f, 0f, 0f);
                edge.raycastTarget = false;
                if (!UiTheme.PackSliced(edge, PackArt.KitCardEdge, 12f, UiTheme.Edge(UiTheme.Emphasis.Interactive)))
                { edge.color = new Color(0f, 0f, 0f, 0f); UiTheme.AddBorder(art, 12, UiTheme.Edge(UiTheme.Emphasis.Interactive)); }
                cardEdges[i] = edge;
                cardBacks[i] = Picture("Back mark", art, UiTheme.Pack(PackArt.KitIconQuestion), new Color(UiTheme.Accent.r, UiTheme.Accent.g, UiTheme.Accent.b, .35f));
                cardFaces[i] = Picture("Card face", art, null, UiTheme.Paper);
                // The caption: the first text under the card, and the card's name.
                var label = HudPrimitives.Label("Label", art, 15f * FontScale, UiTheme.Muted, TextAlignmentOptions.Center);
                label.text = "Card " + (i + 1);
                label.textWrappingMode = TextWrappingModes.NoWrap; label.overflowMode = TextOverflowModes.Ellipsis;
                var medium = UiTheme.Font(UiTheme.Weight.SemiBold); if (medium != null) label.font = medium;
                Fit(label, 10);
                cardLabels[i] = label;
                matchedBadges[i] = Picture("Matched badge", art, UiTheme.Pack(PackArt.KitIconCheck), UiTheme.Positive);
                mismatchBadges[i] = Picture("Mismatch badge", art, UiTheme.Pack(PackArt.KitIconCross), UiTheme.Danger);

                // The focus ring stands outside the tile and never turns with it.
                var ring = new GameObject("Focus ring", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                ring.rectTransform.SetParent(root, false); Stretch(ring.rectTransform, -5f, -5f, -5f, -5f);
                ring.raycastTarget = false;
                if (!UiTheme.PackSliced(ring, PackArt.KitCardEdgeFocus, 14f, UiTheme.Paper)) UiTheme.Style(ring, new Color(1f, 1f, 1f, .9f), 14);
                ring.gameObject.SetActive(false);
                focusRings[i] = ring.rectTransform;
                root.gameObject.AddComponent<CompetitionCardFocus>().Ring = ring.gameObject;
                root.gameObject.AddComponent<HudPress>().ReducedMotion = ReducedMotion;
                shownUp[i] = false; flipLeft[i] = 0f;
            }
            for (int i = 0; i < 16; i++)
                cards[i].navigation = new Navigation { mode = Navigation.Mode.Explicit,
                    selectOnLeft = cards[i / 4 * 4 + (i + 3) % 4], selectOnRight = cards[i / 4 * 4 + (i + 1) % 4],
                    selectOnUp = i < 4 ? cancel : cards[i - 4], selectOnDown = i >= 12 ? pause : cards[i + 4] };

            // The band: which symbols are found, and what the mistakes have cost so far.
            pairTray = new GameObject("Pair tray", typeof(RectTransform)).GetComponent<RectTransform>();
            pairTray.SetParent(boardChips, false);
            for (int i = 0; i < 8; i++)
            {
                var slot = HudPrimitives.Fill("Pair slot " + (i + 1), pairTray, new Color(UiTheme.Ink.r, UiTheme.Ink.g, UiTheme.Ink.b, .6f), 8);
                UiTheme.AddBorder(slot, 8, UiTheme.Outline);
                pairSlots[i] = slot.GetComponent<Image>();
                pairIcons[i] = Picture("Pair symbol", slot, FaceArt(i), UiTheme.Positive);
                pairIcons[i].gameObject.SetActive(false);
            }
            mistakesChip = HudPrimitives.Fill("Mistakes chip", boardChips, new Color(UiTheme.Warning.r, UiTheme.Warning.g, UiTheme.Warning.b, .14f), 12);
            UiTheme.AddBorder(mistakesChip, 12, new Color(UiTheme.Warning.r, UiTheme.Warning.g, UiTheme.Warning.b, .7f));
            Picture("Mistakes mark", mistakesChip, UiTheme.Pack(PackArt.KitIconCross), UiTheme.Warning);
            mistakesLabel = HudPrimitives.Label("Mistakes count", mistakesChip, 14f * FontScale, UiTheme.Warning, TextAlignmentOptions.MidlineLeft);
            mistakesLabel.textWrappingMode = TextWrappingModes.NoWrap;
            // First Impressions' three seconds, draining.
            previewMeter = HudPrimitives.Fill("Preview remaining", boardChips, UiTheme.SurfaceRaised, 6);
            UiTheme.PackSliced(previewMeter.GetComponent<Image>(), PackArt.KitMeterTrack, 6f, UiTheme.SurfaceRaised);
            previewFill = HudPrimitives.Fill("Preview fill", previewMeter, UiTheme.Glow, 6);
            UiTheme.PackSliced(previewFill.GetComponent<Image>(), PackArt.KitMeterTrack, 6f, UiTheme.Glow);
            previewLabel = HudPrimitives.Label("Preview label", boardChips, 12f * FontScale, UiTheme.Glow, TextAlignmentOptions.MidlineLeft);
            previewLabel.characterSpacing = 4f; previewLabel.textWrappingMode = TextWrappingModes.NoWrap;
            previewLabel.text = "MEMORIZE";
            shownPairs = 0; shownMistakes = 0; shownPlaying = false;
        }

        private void PlaceMemory()
        {
            var field = PlayField;
            const float gap = 12f;
            float pitchY = (field.height - gap) / 4f, tileHeight = pitchY - gap;
            float pitchX = (field.width - gap) / 4f;
            // Card-shaped: never wider than 1.6 to 1, centred when the field is wider than that.
            float tileWidth = Mathf.Min(pitchX - gap, tileHeight * 1.6f * FontScale);
            float left = (field.width - (4f * tileWidth + 3f * gap)) * .5f;
            cardWords = Mathf.Clamp(Mathf.Min(tileWidth / 205f, tileHeight / 91f), 1f, 1.5f);
            float caption = 15f * Mathf.Min(cardWords, 1.25f) * FontScale, band = caption * 1.3f + 8f;
            float badge = 22f * FontScale;
            for (int i = 0; i < 16; i++)
            {
                Place((RectTransform)cards[i].transform, left + i % 4 * (tileWidth + gap), field.y + gap + i / 4 * (tileHeight + gap), tileWidth, tileHeight);
                cardLabels[i].fontSize = caption; cardLabels[i].fontSizeMax = caption;
                var label = cardLabels[i].rectTransform;
                label.anchorMin = new Vector2(0f, 0f); label.anchorMax = new Vector2(1f, 0f); label.pivot = new Vector2(.5f, 0f);
                label.anchoredPosition = new Vector2(0f, 6f); label.sizeDelta = new Vector2(-16f, band - 8f);
                float symbol = Mathf.Max(16f, Mathf.Min(tileWidth * .42f, (tileHeight - band - 14f) * .82f));
                Centre(cardFaces[i].rectTransform, 0f, band * .5f, symbol);
                Centre(cardBacks[i].rectTransform, 0f, band * .5f, symbol * .9f);
                Corner(matchedBadges[i].rectTransform, badge);
                Corner(mismatchBadges[i].rectTransform, badge);
            }
            float slot = Mathf.Min(28f * FontScale, BandHeight - 14f);
            Place(pairTray, 0f, (BandHeight - slot) * .5f, 8f * (slot + 6f), slot);
            for (int i = 0; i < 8; i++)
            {
                Place((RectTransform)pairSlots[i].transform, i * (slot + 6f), 0f, slot, slot);
                Centre(pairIcons[i].rectTransform, 0f, 0f, slot * .66f);
            }
            float chipHeight = slot, chipX = 8f * (slot + 6f) + 10f;
            Place(mistakesChip, chipX, (BandHeight - chipHeight) * .5f, 64f * FontScale, chipHeight);
            var mark = mistakesChip.Find("Mistakes mark") as RectTransform;
            if (mark != null) Place(mark, 8f, (chipHeight - 14f * FontScale) * .5f, 14f * FontScale, 14f * FontScale);
            Place(mistakesLabel.rectTransform, 12f + 14f * FontScale, 0f, 64f * FontScale - 16f - 14f * FontScale, chipHeight);
            Place(previewLabel.rectTransform, 0f, 0f, 110f * FontScale, BandHeight - 1f);
            Place(previewMeter, 110f * FontScale, (BandHeight - 12f) * .5f, Mathf.Min(260f, surfaceWidth * .3f), 12f);
        }

        private static void Centre(RectTransform rect, float x, float y, float side)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f); rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = new Vector2(x, y); rect.sizeDelta = new Vector2(side, side);
        }

        private static void Corner(RectTransform rect, float side)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f); rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-8f, -8f); rect.sizeDelta = new Vector2(side, side);
        }

        private void RefreshMemory()
        {
            // A paused or held board shows nothing it would not show at rest: a pause is not a look.
            bool hidden = Paused || held;
            bool preview = previewStarted && !playing && !hidden && !run.Finished && countdownLeft > 0;
            bool comparing = run.SecondFlip >= 0;
            for (int i = 0; i < 16; i++)
            {
                if (cards[i] == null) continue;
                bool matched = run.Matched[i];
                bool flipped = !hidden && (i == run.FirstFlip || i == run.SecondFlip);
                bool faceUp = preview || matched || flipped;
                bool mismatch = !matched && flipped && comparing;
                string word = MemoryFaceName(run.Faces[i]);
                cardLabels[i].text = "Card " + (i + 1) + (faceUp ? ": " + word + (matched ? "  ·  MATCHED" : "") : "");
                cardLabels[i].color = faceUp ? UiTheme.Paper : UiTheme.Muted;
                cardTiles[i].color = matched ? new Color(UiTheme.Surface.r, UiTheme.Surface.g, UiTheme.Surface.b, .88f)
                    : faceUp ? FaceUpFill : UiTheme.SurfaceRaised;
                cardEdges[i].color = mismatch ? UiTheme.Danger
                    : matched ? new Color(UiTheme.Positive.r, UiTheme.Positive.g, UiTheme.Positive.b, .6f)
                    : faceUp ? UiTheme.Edge(UiTheme.Emphasis.Active) : UiTheme.Edge(UiTheme.Emphasis.Interactive);
                cardBacks[i].gameObject.SetActive(!faceUp);
                cardFaces[i].gameObject.SetActive(faceUp);
                cardFaces[i].sprite = FaceArt(run.Faces[i]);
                cardFaces[i].enabled = cardFaces[i].sprite != null;
                cardFaces[i].color = matched ? UiTheme.Positive : UiTheme.Paper;
                matchedBadges[i].gameObject.SetActive(matched);
                mismatchBadges[i].gameObject.SetActive(mismatch);
                if (faceUp != shownUp[i])
                {
                    shownUp[i] = faceUp;
                    // A turn is a quick squeeze through edge-on; the words change at once either way.
                    flipLeft[i] = ReducedMotion || !playing ? 0f : FlipSeconds;
                }
                float squeeze = flipLeft[i] > 0f ? 1f - flipLeft[i] / FlipSeconds : 1f;
                float rest = matched ? .95f : 1f;
                cardArt[i].localScale = new Vector3(rest * squeeze, rest, 1f);
            }

            status.text = preview ? "Memorize " + run.Pairs + " pairs  ·  16 cards"
                : run.MatchedPairs + " / " + run.Pairs + " pairs  ·  " + Plural(run.WrongFlips, "mistake")
                  + (run.WrongFlips > 0 ? " (-" + Math.Min(run.WrongFlips * .15, 5).ToString("0.00") + ")" : "");
            for (int face = 0; face < 8; face++)
            {
                bool found = false;
                for (int i = 0; i < 16 && !found; i++) found = run.Matched[i] && run.Faces[i] == face;
                pairIcons[face].gameObject.SetActive(found);
                pairSlots[face].color = found ? new Color(UiTheme.Positive.r, UiTheme.Positive.g, UiTheme.Positive.b, .16f)
                    : new Color(UiTheme.Ink.r, UiTheme.Ink.g, UiTheme.Ink.b, .6f);
            }
            bool previewing = previewStarted && !playing && !run.Finished;
            pairTray.gameObject.SetActive(!previewing);
            mistakesChip.gameObject.SetActive(!previewing && run.WrongFlips > 0);
            mistakesLabel.text = run.WrongFlips.ToString();
            previewMeter.gameObject.SetActive(previewing);
            previewLabel.gameObject.SetActive(previewing);
            if (run.Definition != null && run.Definition.PreviewSeconds > 0)
            {
                float left = hidden ? previewFill.anchorMax.x : Mathf.Clamp01(countdownLeft / (float)run.Definition.PreviewSeconds);
                previewFill.anchorMin = Vector2.zero; previewFill.anchorMax = new Vector2(left, 1f);
                previewFill.offsetMin = previewFill.offsetMax = Vector2.zero;
            }

            // What just happened: a pair, a miss, the cards going down after the preview.
            if (playing && !shownPlaying)
                SetFeedback(run.Definition?.Pattern == Gamesim.Simulation.CompetitionPattern.PreviewPairs
                    ? "Cards hidden. Find the pairs." : "Find the " + run.Pairs + " pairs.");
            else if (run.MatchedPairs > shownPairs) SetFeedback("Pair found: " + LastMatchedWord());
            else if (run.WrongFlips > shownMistakes) SetFeedback("No match. The cards turn back.");
            shownPlaying = playing; shownPairs = run.MatchedPairs; shownMistakes = run.WrongFlips;
        }

        private string LastMatchedWord()
        {
            // The pair just found is the one whose cards are matched and whose symbol the tray lit last.
            for (int face = 7; face >= 0; face--)
            {
                int count = 0;
                for (int i = 0; i < 16; i++) if (run.Matched[i] && run.Faces[i] == face) count++;
                if (count == 2 && !pairShown[face]) { pairShown[face] = true; return Faces[face]; }
            }
            return "pair";
        }

        private readonly bool[] pairShown = new bool[8];

        private void AdvanceMemoryBeats(float delta)
        {
            bool turning = false;
            for (int i = 0; i < 16; i++)
            {
                if (flipLeft[i] <= 0f) continue;
                flipLeft[i] = Mathf.Max(0f, flipLeft[i] - Mathf.Max(0f, delta));
                turning = true;
            }
            if (turning) RefreshMemory();
        }

        private static string Plural(int count, string noun) => count + " " + noun + (count == 1 ? "" : "s");
    }
}
