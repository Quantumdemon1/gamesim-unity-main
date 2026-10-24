using System;
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
    /// The offer board (ROLE-MOMENTS-PLAN R2, §5.1): the offers houseguests came to the player with, as portrait
    /// cards on the campaign board's card and portrait pattern (<see cref="CampaignBoard"/>,
    /// <see cref="HudPrimitives.RectPortrait"/>) rather than a heading, a paragraph and two rows for each. Drawn at the
    /// veto meeting and in the Diary Room's veto column, under <see cref="EpisodeEngine.RolesOn"/> only; a season
    /// without the role moments draws its rows as it always did. Under the role moments the Head of Household's pitches
    /// are the same board (R5), on the nomination's screen and in the Diary Room's nomination: a card a pitch, with the
    /// pitch's own four answers in place of the yes and the no (<see cref="OfferBoardCard.Answers"/>).
    ///
    /// <para>A card is the asker's face - from <see cref="CharacterPortraits"/> only, never an initials avatar - with
    /// their role's badge and its word on a pill, their name, the deal's chip and what it asks, the tone with its
    /// mark, the situation word, the player's read, the quote, the terms, the stakes, what a no and silence cost
    /// (R3) and the player's own history with them, each a label of its own in <see cref="RoleOffers"/>' words, and
    /// under them the two answers the rows always had, captioned as they always were and each described through
    /// <see cref="DescribeOfferControl"/>. Where the director says it, the board's head says that silence costs more
    /// than a no.
    /// Nothing is appended to a caption. A card whose yes cannot be kept - the player has given their word on the
    /// veto to the other nominee - says why in a label of its own over a disabled yes, and its no stays.</para>
    ///
    /// <para>The cards stand side by side as far as the column holds them at the narrowest a card reads, and one
    /// over another below that (the diary's column). At the largest text they page instead (the owner's decision):
    /// one card a page, with "Previous offer" and "Next offer" and where the player is - "1 of 2" - at the board's
    /// foot, on the one pager row the HUD draws (<see cref="PagerRow"/>). At the standard and the larger text every
    /// card is on the one page. The pitch board opens on the nomination's stage without a scroll at every size (R5b): its
    /// head is one row where it holds one, a pitch's tone, situation and read start in the room beside its face, its four
    /// answers stand two to a row, its gaps are a little closer than an offer's, and at the largest text its one card takes the page's width with its answers beside its
    /// notes; the veto board is drawn as it always was. Nothing on the board travels or fades: the cards cut in under every motion setting,
    /// and only the panel arrives, as it always has. No label is set under ten units, twelve pixels at 1080p, and
    /// every box is at least 1.3 times its type.</para>
    ///
    /// <para>A screen reader hears a card's control as its caption, then the card's heading - "Danielle Reyes,
    /// nominee, veto commitment" - then one line that says the tone, the situation, the read, the terms, the stakes
    /// and any lock in words (<see cref="TryReadOfferControl"/>, <see cref="RoleOffers.Spoken"/>). The board commits
    /// nothing of its own: an answer is the caller's, and a page turned is view state.</para>
    /// </summary>
    public sealed partial class EpisodeHud
    {
        /// <summary>The board's parts, by the names a test finds them by. A card is "Offer card · {name}".</summary>
        public const string OfferBoardName = "Offer board", OfferBoardHeaderName = "Offer board header", OfferEyebrowName = "Offer eyebrow",
            OfferLineName = "Offer line", OfferFreeName = "Offer free line", OfferCardPrefix = "Offer card · ", OfferBadgeName = "Offer badge",
            OfferNameName = "Offer name", OfferChipName = "Offer chip", OfferChipLineName = "Offer chip line", OfferToneName = "Offer tone",
            OfferToneMarkName = "Tone mark", OfferToneWordName = "Tone word", OfferSituationName = "Offer situation", OfferReadName = "Offer read",
            OfferQuoteName = "Offer quote", OfferTermsName = "Offer terms", OfferStakesName = "Offer stakes", OfferHistoryName = "Offer history",
            OfferLockName = "Offer lock", OfferPagerName = "Offer pager", OfferCountName = "Offer count",
            OfferSilenceName = "Offer silence line", OfferAnswersName = "Offer answers",
            OfferRecommendsName = "Offer recommends", OfferAssessmentName = "Offer assessment";

        /// <summary>The board's own words: its eyebrow, and what answering costs.</summary>
        public const string OfferBoardEyebrow = "INCOMING OFFERS", OfferBoardFreeLine = "Answering uses no conversation";

        /// <summary>The eyebrow of the Head of Household's pitch board (ROLE-MOMENTS-PLAN R5): the same board, its own word.</summary>
        public const string PitchBoardEyebrow = "INCOMING PITCHES";

        /// <summary>The largest text's pager on the board, new captions of its own (§5.2).</summary>
        public const string PreviousOfferCaption = "Previous offer", NextOfferCaption = "Next offer";

        /// <summary>
        /// One card: its words, the row it answers, why its yes is locked (null when it is not), and what each answer does.
        /// A card that answers something other than an offer - a pitch (ROLE-MOMENTS-PLAN R5) - gives its own
        /// <see cref="Answers"/> instead of the yes and the no, and no row.
        /// </summary>
        public sealed class OfferBoardCard
        {
            public RoleOffers.Card Card;
            public DealState Offer;
            public string LockReason;
            public Action Accept, Decline;
            /// <summary>The card's own answers, in their order, where it has them; null for an offer's yes and no.</summary>
            public IList<OfferBoardAnswer> Answers;
        }

        /// <summary>
        /// One of a card's own answers (R5): its caption - the answer's, unchanged - the key it commits, whether it is locked,
        /// and what pressing it does. A locked answer is drawn as the board draws a locked yes, and does nothing pressed.
        /// </summary>
        public sealed class OfferBoardAnswer
        {
            public string Caption, Key;
            public bool Locked;
            public Action Choose;
        }

        /// <summary>
        /// What the director hands the board: its eyebrow where it is not "INCOMING OFFERS"; the line under it; what silence
        /// costs, beside the line, where the board's offers make it true ("Silence costs more than a no.", ROLE-MOMENTS-PLAN
        /// R3) and null where they do not; its cards in their order; and how to draw it again when a page turns.
        /// </summary>
        public sealed class OfferBoardSpec
        {
            public string Eyebrow, Line, Silence;
            public IList<OfferBoardCard> Cards;
            public Action Redraw;
        }

        /// <summary>
        /// Which card's answer each of this render's board answers is (R5): two cards carry the same captions, so a rebuild
        /// puts the keyboard back on the same card's answer by these, as an offer's by <see cref="offerControls"/>. Begin clears it.
        /// </summary>
        private readonly Dictionary<Button, (string CardId, string Key)> boardAnswers = new Dictionary<Button, (string, string)>();

        /// <summary>The card and the answer whose control had the keyboard when the HUD last rebuilt, beside its name.</summary>
        private (string CardId, string Key) preferredBoardAnswer;

        /// <summary>Which card's answer a control of this render's board is, and which answer (R5); false for any other control.</summary>
        public bool TryDescribeBoardAnswer(Button button, out string cardId, out string key)
        {
            cardId = key = null;
            if (button == null || !boardAnswers.TryGetValue(button, out var answer)) return false;
            (cardId, key) = answer;
            return true;
        }

        // At the resting text size; each is multiplied by the text scale.
        private const float OfferGap = 12f, OfferPad = 12f, OfferPhotoWidth = 64f, OfferPhotoHeight = 80f, OfferBadge = 20f,
            OfferCardLeast = 300f, OfferCardMost = 560f, OfferAnswerHeight = 40f, OfferAnswerInset = 12f, OfferPagerPill = 26f;

        /// <summary>What a screen reader says for each of this render's board controls; Begin clears it with the controls.</summary>
        private readonly Dictionary<Button, string> offerReadings = new Dictionary<Button, string>();

        /// <summary>
        /// What a screen reader says for one of the board's controls this render - its caption, the card's heading,
        /// then the card's one line - or false for any other control.
        /// </summary>
        public bool TryReadOfferControl(Button button, out string reading)
        {
            reading = null;
            return button != null && offerReadings.TryGetValue(button, out reading);
        }

        /// <summary>
        /// Draws the board as a row of the column, under its eyebrow and line, and returns it; null, with nothing
        /// drawn, without a card. Every card's answers are registered as the offer's controls.
        /// </summary>
        public RectTransform OfferBoard(EpisodeState state, OfferBoardSpec spec)
        {
            if (content == null || state == null || spec?.Cards == null || spec.Cards.Count == 0) return null;
            float s = FontScale, width = ContentWidth(), gap = OfferGap * s;
            var board = new GameObject(OfferBoardName, typeof(RectTransform), typeof(LayoutElement)).GetComponent<RectTransform>();
            board.SetParent(content, false);
            // The Head of Household's pitches (R5): every card answers with its own answers.
            bool pitches = spec.Cards.All(card => card != null && card.Answers != null);
            // The pitch board's head stands closer to its cards (R5b): the larger text's stage has no line to spare.
            float y = OfferBoardHeader(board, width, spec.Eyebrow ?? OfferBoardEyebrow, spec.Line, spec.Silence, pitches) + (pitches ? 8f * s : gap);

            int count = spec.Cards.Count;
            bool paged = LargestTextPages && count > 1;
            int page = paged ? LargestPageOf(OfferBoardName, count) : 0;
            var shown = paged ? new List<OfferBoardCard> { spec.Cards[page] } : spec.Cards.ToList();
            int across = paged ? 1 : Mathf.Clamp(Mathf.FloorToInt((width + gap) / (OfferCardLeast * s + gap)), 1, count);
            float cardWidth = Mathf.Min(OfferCardMost * s, (width - (across - 1) * gap) / across);
            // At the largest text a pitch's page gives its one card the column's width (R5b), so its answers can stand beside its
            // notes and the card is no taller than its page (OfferPitchFoot). An offer's page keeps the card it always had.
            if (paged && pitches) cardWidth = width;
            for (int first = 0; first < shown.Count; first += across)
            {
                // A row's cards share its height, the tallest's, so their answers stand on one line.
                var row = shown.Skip(first).Take(across).ToList();
                float x = Mathf.Max(0f, (width - row.Count * cardWidth - (row.Count - 1) * gap) * .5f);
                var built = new List<(RectTransform Card, float Height)>();
                foreach (var entry in row)
                {
                    built.Add(OfferCard(board, state, entry, x, y, cardWidth));
                    x += cardWidth + gap;
                }
                float height = built.Max(part => part.Height);
                foreach (var part in built) part.Card.sizeDelta = new Vector2(part.Card.sizeDelta.x, height);
                y += height + gap;
            }
            y += paged ? OfferPager(board, y, width, page, count, spec.Redraw) : -gap;
            var element = board.GetComponent<LayoutElement>();
            element.minHeight = element.preferredHeight = y;
            return board;
        }

        /// <summary>
        /// The board's head: "INCOMING OFFERS" with what answering costs at the right-hand end where the width
        /// holds both, the line under them, and what answering costs under the line where it does not. What silence
        /// costs, where the spec says it (<paramref name="silence"/>), stands under what answering costs: at the
        /// line's right-hand end where both hold on the one row, as §5.1's mock has it, and under the line otherwise.
        /// The pitch board's head (<paramref name="pitches"/>, R5b), which says nothing of silence, is one row where the
        /// eyebrow, the line and what answering costs all hold it: the line beside the eyebrow. Returns its height.
        /// </summary>
        private float OfferBoardHeader(RectTransform board, float width, string eyebrowWords, string line, string silence, bool pitches = false)
        {
            float s = FontScale, eyebrowBox = OfferLineBox(13), freeBox = OfferLineBox(12);
            var head = EndScreenKit.Box(OfferBoardHeaderName, board, 0f, 0f, width, 10f);
            var eyebrow = NewText(head, eyebrowWords, 13, UiTheme.Gold);
            eyebrow.name = OfferEyebrowName;
            eyebrow.characterSpacing = 4f;
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            if (semibold != null) eyebrow.font = semibold;
            eyebrow.textWrappingMode = TextWrappingModes.NoWrap;
            var free = NewText(head, OfferBoardFreeLine, 12, UiTheme.Muted);
            free.name = OfferFreeName;
            free.textWrappingMode = TextWrappingModes.NoWrap;
            float eyebrowWidth = Mathf.Ceil(eyebrow.GetPreferredValues(eyebrow.text).x) + 2f, freeWidth = Mathf.Ceil(free.GetPreferredValues(free.text).x) + 2f;
            bool beside = eyebrowWidth + freeWidth + 24f * s <= width;
            EndScreenKit.Place(eyebrow.rectTransform, 0f, 0f, beside ? width - freeWidth - 24f * s : width, eyebrowBox);
            float y = eyebrowBox + 2f * s;
            if (beside)
            {
                EndScreenKit.Place(free.rectTransform, width - freeWidth, (eyebrowBox - freeBox) * .5f, freeWidth, freeBox);
                free.alignment = TextAlignmentOptions.Right;
            }
            TMP_Text quiet = null;
            float quietWidth = 0f;
            if (!string.IsNullOrEmpty(silence))
            {
                quiet = NewText(head, silence, 12, UiTheme.Muted);
                quiet.name = OfferSilenceName;
                quiet.textWrappingMode = TextWrappingModes.NoWrap;
                quietWidth = Mathf.Ceil(quiet.GetPreferredValues(quiet.text).x) + 2f;
            }
            bool quietBeside = false;
            var (said, lineHeight) = OfferLabel(head, OfferLineName, line, 13, Paper, 0f, y, width);
            float saidWidth = said != null ? Mathf.Ceil(said.GetPreferredValues(said.text).x) + 2f : 0f;
            if (said != null && pitches && quiet == null && beside && eyebrowWidth + saidWidth + freeWidth + 48f * s <= width)
            {
                EndScreenKit.Place(eyebrow.rectTransform, 0f, 0f, eyebrowWidth, eyebrowBox);
                EndScreenKit.Place(said.rectTransform, eyebrowWidth + 24f * s, 0f, saidWidth, eyebrowBox);
                said.textWrappingMode = TextWrappingModes.NoWrap;
                head.sizeDelta = new Vector2(width, y);
                return y;
            }
            if (said != null)
            {
                // Beside the line only where the line still holds on one row, so nothing wraps under it.
                float room = width - quietWidth - 24f * s;
                quietBeside = quiet != null && beside && Mathf.Ceil(said.GetPreferredValues(said.text).x) + 2f <= room;
                if (quietBeside)
                {
                    lineHeight = EndScreenKit.Wrapped(said, room);
                    EndScreenKit.Place(quiet.rectTransform, width - quietWidth, y + (OfferLineBox(13) - freeBox) * .5f, quietWidth, freeBox);
                    quiet.alignment = TextAlignmentOptions.Right;
                }
                y += lineHeight + 2f * s;
            }
            if (!beside)
            {
                EndScreenKit.Place(free.rectTransform, 0f, y, width, freeBox);
                y += freeBox;
            }
            if (quiet != null && !quietBeside)
            {
                EndScreenKit.Place(quiet.rectTransform, 0f, y, width, freeBox);
                y += freeBox;
            }
            head.sizeDelta = new Vector2(width, y);
            return y;
        }

        /// <summary>
        /// One card at (<paramref name="x"/>, <paramref name="y"/>) of the board, <paramref name="width"/> wide: the
        /// face and its role, the name, the chip and what it asks beside it; the tone, the situation and the read;
        /// the quote, the terms, the stakes, what a no and silence cost, the history and any lock; and the answers at
        /// its foot. Returns the card and the height its words need, which the row may make taller.
        /// </summary>
        private (RectTransform Card, float Height) OfferCard(RectTransform board, EpisodeState state, OfferBoardCard entry, float x, float y, float width)
        {
            var words = entry.Card;
            var actor = state.Find(words.speakerId);
            float s = FontScale, pad = OfferPad * s, inner = width - 2f * pad;
            var mark = OfferRoleMark(words.role);
            var card = EndScreenKit.Box(OfferCardPrefix + words.name, board, x, y, width, 10f);
            EndScreenKit.Frame(card, PackArt.Pack8VoterCard, 12f * s, new Color(Surface.r, Surface.g, Surface.b, .94f),
                new Color(mark.Tint.r, mark.Tint.g, mark.Tint.b, .55f));

            // The face, as every card in the house draws one, with the role's badge on its corner and the role's
            // word on its foot: the badge decorates, the pill says.
            float photoWidth = OfferPhotoWidth * s, photoHeight = OfferPhotoHeight * s, badge = OfferBadge * s;
            var photo = HudPrimitives.RectPortrait(card, "Photo", Portrait(words.speakerId), actor, new Vector2(photoWidth, photoHeight), 7);
            photo.anchorMin = photo.anchorMax = new Vector2(0f, 1f);
            photo.pivot = new Vector2(0f, 1f);
            photo.anchoredPosition = new Vector2(pad, -pad);
            EndScreenKit.Picture(OfferBadgeName, photo, mark.Pack, mark.Glyph, mark.Tint, new Vector2(photoWidth - badge * .5f - 2f * s, -(badge * .5f + 2f * s)), badge);
            if (!string.IsNullOrEmpty(mark.Pill)) OfferPill(photo, mark.Pill, mark.Tint, photoWidth - 8f * s);

            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            float left = pad + photoWidth + 12f * s, column = Mathf.Max(60f * s, width - left - pad), top = pad;
            var name = NewText(card, words.name, 16, Paper);
            name.name = OfferNameName;
            if (semibold != null) name.font = semibold;
            name.textWrappingMode = TextWrappingModes.NoWrap;
            name.overflowMode = TextOverflowModes.Ellipsis;
            AutoSize(name, 12);
            EndScreenKit.Place(name.rectTransform, left, top, column, OfferLineBox(16));
            top += OfferLineBox(16) + 2f * s;
            top += OfferText(card, OfferChipName, (words.chip ?? "").ToUpperInvariant(), 12, Accent, left, top, column, semibold, spacing: 2f);
            top += OfferText(card, OfferChipLineName, words.chipLine, 12, UiTheme.Muted, left, top, column);

            // A pitch's tone, situation and read start beside its face, under the chip's line, on every line there that ends by the
            // face's foot, each whole, and go on under the face (R5b): the room beside a face is otherwise empty, and a card that
            // stands three to a row is otherwise taller than its stage. An offer's stand under the face, as they always have.
            bool own = entry.Answers != null;
            var facts = OfferFactParts(card, words);
            int besideFace = 0;
            while (own && besideFace < facts.Parts.Count && top + 2f * s + facts.Line <= pad + photoHeight + .5f)
            {
                int took = OfferFactsBeside(facts, besideFace, left, top + 2f * s, column);
                if (took == 0) break;
                besideFace += took;
                top += 2f * s + facts.Line;
            }
            // A pitch's card stands its words a little closer under the face than an offer's (R5b), for the same stage.
            float at = Mathf.Max(top, pad + photoHeight) + (own ? 4f : 8f) * s;
            if (besideFace < facts.Parts.Count) at += OfferFactsAt(facts, besideFace, pad, at, inner) + (own ? 4f : 6f) * s;
            // A pitch's notes and its own four answers (R5b); an offer's, below, as they always were.
            if (own) return (card, OfferPitchFoot(card, entry, pad, at, inner) + pad);
            if (!string.IsNullOrEmpty(words.quote))
                at += OfferText(card, OfferQuoteName, "“" + words.quote + "”", 13, Paper, pad, at, inner, style: FontStyles.Italic) + 4f * s;
            at += OfferText(card, OfferTermsName, words.terms, 12, Paper, pad, at, inner) + 2f * s;
            at += OfferText(card, OfferStakesName, words.stakes, 12, UiTheme.Muted, pad, at, inner) + 2f * s;
            if (!string.IsNullOrEmpty(words.answers)) at += OfferText(card, OfferAnswersName, words.answers, 12, UiTheme.Muted, pad, at, inner) + 2f * s;
            if (!string.IsNullOrEmpty(words.history)) at += OfferText(card, OfferHistoryName, words.history, 12, UiTheme.Heading, pad, at, inner) + 2f * s;
            if (!string.IsNullOrEmpty(entry.LockReason)) at += OfferText(card, OfferLockName, entry.LockReason, 12, UiTheme.Warning, pad, at, inner) + 2f * s;
            at += 8f * s;
            at += OfferAnswers(card, state, entry, pad, inner);
            return (card, at + pad);
        }

        /// <summary>
        /// The tone, the situation word and the player's read, built and measured but not yet placed (<see cref="OfferFactsAt"/>
        /// and <see cref="OfferFactsBeside"/> place them): the tone as its mark in a disc of its colour and its word, never the
        /// colour alone (§5.4). Each part's width is its own; under the face a situation or a read is held to the width it is
        /// placed in (<c>Clamped</c>), as it always was.
        /// </summary>
        private (List<(RectTransform Rect, float Width, float Height, bool Clamped)> Parts, float Line) OfferFactParts(RectTransform card, RoleOffers.Card words)
        {
            float s = FontScale, disc = 18f * s, toneBox = OfferLineBox(13), factBox = OfferLineBox(12);
            float line = Mathf.Max(disc, toneBox);
            var tint = ToneTint(words.tone);
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            var parts = new List<(RectTransform Rect, float Width, float Height, bool Clamped)>();

            var tone = EndScreenKit.Box(OfferToneName, card, 0f, 0f, 10f, line);
            var ground = new GameObject(OfferToneMarkName, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            ground.rectTransform.SetParent(tone, false);
            EndScreenKit.Place(ground.rectTransform, 0f, (line - disc) * .5f, disc, disc);
            ground.sprite = UiTheme.Circle();
            ground.color = new Color(tint.r, tint.g, tint.b, .22f);
            ground.raycastTarget = false;
            var shape = NewText(ground.rectTransform, RoleOffers.ToneMark(words.tone) ?? "", 12, tint);
            shape.name = "Mark";
            if (semibold != null) shape.font = semibold;
            shape.alignment = TextAlignmentOptions.Center;
            shape.textWrappingMode = TextWrappingModes.NoWrap;
            Stretch(shape.rectTransform, 0f, 0f, 0f, 0f);
            var said = NewText(tone, words.tone ?? "", 13, tint);
            said.name = OfferToneWordName;
            if (semibold != null) said.font = semibold;
            said.textWrappingMode = TextWrappingModes.NoWrap;
            float saidWidth = Mathf.Ceil(said.GetPreferredValues(said.text).x) + 2f;
            EndScreenKit.Place(said.rectTransform, disc + 6f * s, (line - toneBox) * .5f, saidWidth, toneBox);
            parts.Add((tone, disc + 6f * s + saidWidth, line, false));

            foreach (var (name, text, colour) in new[] { (OfferSituationName, words.situation, Paper), (OfferReadName, words.read, UiTheme.Muted) })
            {
                if (string.IsNullOrEmpty(text)) continue;
                var label = NewText(card, text, 12, colour);
                label.name = name;
                label.textWrappingMode = TextWrappingModes.NoWrap;
                label.overflowMode = TextOverflowModes.Ellipsis;
                parts.Add((label.rectTransform, Mathf.Ceil(label.GetPreferredValues(label.text).x) + 2f, factBox, true));
            }
            return (parts, line);
        }

        /// <summary>
        /// Places on one line at (<paramref name="x0"/>, <paramref name="y0"/>) as many of the facts from <paramref name="from"/> on
        /// as hold <paramref name="width"/> whole, in their order (R5b: beside a pitch's face). Returns how many it placed: none
        /// where the first does not hold.
        /// </summary>
        private int OfferFactsBeside((List<(RectTransform Rect, float Width, float Height, bool Clamped)> Parts, float Line) facts, int from, float x0, float y0, float width)
        {
            float gap = 14f * FontScale, x = 0f;
            int placed = 0;
            for (int i = from; i < facts.Parts.Count && x + facts.Parts[i].Width <= width; i++, placed++)
            {
                var part = facts.Parts[i];
                EndScreenKit.Place(part.Rect, x0 + x, y0 + (facts.Line - part.Height) * .5f, part.Width, part.Height);
                x += part.Width + gap;
            }
            return placed;
        }

        /// <summary>
        /// Places the tone, the situation word and the player's read - those from <paramref name="from"/> on - at
        /// (<paramref name="x0"/>, <paramref name="y0"/>), one after another across <paramref name="width"/> and on as many lines
        /// as it needs. Returns the height they take.
        /// </summary>
        private float OfferFactsAt((List<(RectTransform Rect, float Width, float Height, bool Clamped)> Parts, float Line) facts, int from, float x0, float y0, float width)
        {
            float s = FontScale, gap = 14f * s, line = facts.Line, x = 0f, y = 0f;
            foreach (var part in facts.Parts.Skip(from))
            {
                float each = part.Clamped ? Mathf.Min(width, part.Width) : part.Width;
                if (x > 0f && x + each > width) { x = 0f; y += line + 2f * s; }
                EndScreenKit.Place(part.Rect, x0 + x, y0 + y + (line - part.Height) * .5f, Mathf.Min(each, width), part.Height);
                x += each + gap;
            }
            return y + line;
        }

        /// <summary>
        /// The card's answers at its foot: "Accept this offer" - locked where the card says why - and "Turn this
        /// offer down", side by side where both captions fit at their own size and one over the other where they do
        /// not, each registered as the offer's control and given what a screen reader says for it. Returns the
        /// height they take.
        /// </summary>
        private float OfferAnswers(RectTransform card, EpisodeState state, OfferBoardCard entry, float x, float width)
        {
            float s = FontScale, height = OfferAnswerHeight * s, gap = 8f * s;
            bool locked = !string.IsNullOrEmpty(entry.LockReason);
            var accept = locked ? OfferLockedAnswer(card, DealAcceptCaption) : OfferAnswer(card, DealAcceptCaption, entry.Accept);
            var decline = OfferAnswer(card, DealDeclineCaption, entry.Decline);
            float need = Mathf.Max(OfferCaptionWidth(accept) + (locked ? OfferLockRoom : 0f), OfferCaptionWidth(decline)) + 2f * OfferAnswerInset * s;
            bool beside = 2f * need + gap <= width;
            float each = beside ? (width - gap) * .5f : width;
            OfferAnswerAt(accept, x, beside ? 0f : height + gap, each, height);
            OfferAnswerAt(decline, beside ? x + each + gap : x, 0f, each, height);
            DescribeOfferControl(accept, state, entry.Offer, true);
            DescribeOfferControl(decline, state, entry.Offer, false);
            offerReadings[accept] = RoleOffers.Spoken(DealAcceptCaption, entry.Card, entry.LockReason);
            offerReadings[decline] = RoleOffers.Spoken(DealDeclineCaption, entry.Card, entry.LockReason);
            return beside ? height : 2f * height + gap;
        }

        /// <summary>
        /// A card's own answers at its foot (R5): each under its own caption, unchanged - locked where the answer is - two to a
        /// row at every text size, in their order, the first at the top (R5b): the pitch's "Promise safety" beside "Hear them
        /// without committing", over "Turn them down" beside "Feel them out (free)". The captions share one size and take two
        /// lines at most, as <see cref="OfferAnswerGrid"/> lays them; the columns split the card evenly where both hold their
        /// captions so, and otherwise each takes what its widest caption needs and half of what is left. A row is as tall as its
        /// tallest caption needs, and never shorter than a one-line answer. Each is registered as that card's answer, so a
        /// rebuild puts the keyboard back on the same card's, and given what a screen reader says for it. Built by
        /// <see cref="OfferOwnAnswerButtons"/>; returns the height they take.
        /// </summary>
        private float OfferOwnAnswers(OfferBoardCard entry, OwnAnswers built, float x, float width)
        {
            // The rows stand a little closer than the columns: a pitch card is otherwise taller than the larger text's stage.
            float s = FontScale, least = OfferAnswerHeight * s, gap = 8f * s, rowGap = 6f * s, inset = OfferGridInset * s, margin = 4f * s;
            var answers = built.Answers;
            if (answers.Count == 0) return 0f;
            var buttons = built.Buttons;
            var captions = built.Captions;
            var rooms = built.Rooms;
            var grid = OfferAnswerGrid(captions, rooms, width, gap, inset);
            int across = answers.Count > 1 ? 2 : 1, rows = (answers.Count + across - 1) / across;
            float line = Mathf.Ceil(grid.Size * 1.3f);
            var heights = Enumerable.Repeat(least, rows).ToArray();
            var wraps = new bool[buttons.Count];
            for (int i = 0; i < buttons.Count; i++)
            {
                float each = i % across == 0 ? grid.Left : grid.Right;
                // A caption takes a second line only where its one would not hold in its box.
                wraps[i] = grid.Lines[i].One * grid.Size + 2f + rooms[i] + 2f * inset > each + .5f;
                if (wraps[i]) heights[i / across] = Mathf.Max(heights[i / across], 2f * line + 2f * margin);
            }
            for (int i = 0; i < buttons.Count; i++)
            {
                int row = i / across, column = i % across;
                float each = column == 0 ? grid.Left : grid.Right, fromFoot = 0f;
                for (int below = row + 1; below < rows; below++) fromFoot += heights[below] + rowGap;
                OfferAnswerAt(buttons[i], x + (column == 0 ? 0f : grid.Left + gap), fromFoot, each, heights[row]);
                var caption = captions[i];
                if (caption != null)
                {
                    caption.enableAutoSizing = false;
                    caption.fontSize = grid.Size;
                    AutoSize(caption, 12);
                    caption.textWrappingMode = wraps[i] ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
                    Stretch(caption.rectTransform, inset, margin, inset + rooms[i], margin);
                }
                boardAnswers[buttons[i]] = (entry.Card.offerId, answers[i].Key);
                offerReadings[buttons[i]] = RoleOffers.Spoken(answers[i].Caption, entry.Card, entry.LockReason);
            }
            return heights.Sum() + (rows - 1) * rowGap;
        }

        /// <summary>A card's own answers, built and not yet placed (R5b): the answers with a caption, their buttons, the buttons' captions and each one's lock room.</summary>
        private sealed class OwnAnswers
        {
            public List<OfferBoardAnswer> Answers;
            public List<Button> Buttons;
            public List<TMP_Text> Captions;
            public List<float> Rooms;
        }

        /// <summary>Builds a card's own answers under their captions, unchanged - a locked one as the board draws a locked yes - for <see cref="OfferOwnAnswers"/> to place.</summary>
        private OwnAnswers OfferOwnAnswerButtons(RectTransform card, OfferBoardCard entry)
        {
            float inset = OfferGridInset * FontScale;
            var answers = entry.Answers.Where(answer => answer != null && !string.IsNullOrEmpty(answer.Caption)).ToList();
            var buttons = answers.Select(answer => answer.Locked ? OfferLockedAnswer(card, answer.Caption, inset) : OfferAnswer(card, answer.Caption, answer.Choose)).ToList();
            return new OwnAnswers
            {
                Answers = answers, Buttons = buttons,
                Captions = buttons.Select(button => button.GetComponentInChildren<TMP_Text>()).ToList(),
                // Room for a lock only where one was drawn: the icon set may have none, and a room kept for nothing would shrink
                // this card's type below its neighbours' and push its caption off the button's middle.
                Rooms = answers.Select((answer, i) => answer.Locked && buttons[i].transform.Find(LockMarkName) != null ? OfferLockRoom : 0f).ToList(),
            };
        }

        /// <summary>
        /// The width a card's own answers take two to a row, in two columns of one width, with every caption on one line at its
        /// full size, 16 at the text's scale (R5b): twice the widest caption's with its lock's room and its margins, and the gap
        /// between them.
        /// </summary>
        private float OfferOneLineGridWidth(OwnAnswers built)
        {
            float s = FontScale, inset = OfferGridInset * s, widest = 0f;
            int top = Mathf.RoundToInt(16f * s), count = built.Captions.Count;
            for (int i = 0; i < count; i++)
                widest = Mathf.Max(widest, OfferCaptionLines(built.Captions[i], top).One * top + 2f + built.Rooms[i] + 2f * inset);
            return count > 1 ? 2f * widest + 8f * s : widest;
        }

        /// <summary>
        /// A pitch card's notes and its own answers, from <paramref name="at"/> down (R5b): the quote, the recommendation, the stakes,
        /// what a no and silence cost, the history, what feeling them out learned and any lock, each a label of its own in that
        /// order, then the four answers two to a row at the card's foot (<see cref="OfferOwnAnswers"/>). Where the card is wide
        /// enough for its answers to stand beside its notes - every caption on one line at its full size, and the stakes and what
        /// a no and silence cost each on one line in what is left, as on the largest text's one card, which takes its page's width
        /// - the answers stand at the foot to the right of the notes, and the card is as tall as the taller of the two; otherwise
        /// the notes stand over the answers, as they did. Returns the height down to the answers' foot.
        /// </summary>
        private float OfferPitchFoot(RectTransform card, OfferBoardCard entry, float x, float at, float width)
        {
            float s = FontScale, gap = OfferGap * s;
            var words = entry.Card;
            var notes = new List<(TMP_Text Label, float After)>();
            void Note(string name, string text, Color colour, int size = 12, FontStyles style = FontStyles.Normal, float after = 2f)
            {
                var (label, _) = OfferLabel(card, name, text, size, colour, x, at, width, style: style);
                if (label != null) notes.Add((label, after * s));
            }
            Note(OfferQuoteName, string.IsNullOrEmpty(words.quote) ? null : "“" + words.quote + "”", Paper, 13, FontStyles.Italic, 4f);
            Note(OfferRecommendsName, words.recommends, Paper);
            Note(OfferTermsName, words.terms, Paper);
            Note(OfferStakesName, words.stakes, UiTheme.Muted);
            Note(OfferAnswersName, words.answers, UiTheme.Muted);
            Note(OfferHistoryName, words.history, UiTheme.Heading);
            // What feeling a pitcher out learned, once the player has (R5).
            Note(OfferAssessmentName, words.assessment, UiTheme.Heading);
            Note(OfferLockName, entry.LockReason, UiTheme.Warning);

            var answers = OfferOwnAnswerButtons(card, entry);
            float grid = OfferOneLineGridWidth(answers), room = width - grid - gap;
            // The two notes the card's every answer is weighed by must each hold one line beside the answers.
            float widest = notes.Where(note => note.Label.name == OfferStakesName || note.Label.name == OfferAnswersName)
                .Select(note => Mathf.Ceil(note.Label.GetPreferredValues(note.Label.text).x) + 2f).DefaultIfEmpty(0f).Max();
            bool beside = answers.Buttons.Count > 0 && notes.Count > 0 && room >= widest;
            float column = beside ? room : width, y = at;
            foreach (var (label, after) in notes)
            {
                EndScreenKit.Place(label.rectTransform, x, y, column, 10f);
                y += EndScreenKit.Wrapped(label, column) + after;
            }
            if (!beside) return y + 4f * s + OfferOwnAnswers(entry, answers, x, width);
            float notesHeight = y - at - notes[notes.Count - 1].After;
            return at + Mathf.Max(notesHeight, OfferOwnAnswers(entry, answers, x + width - grid, grid));
        }

        /// <summary>The side margin of a caption in a card's grid of its own answers (R5b), at the resting text size.</summary>
        private const float OfferGridInset = 8f;

        /// <summary>
        /// How a card's own answers stand two to a row in <paramref name="width"/> (R5b): their captions' one size, each caption's
        /// width on one line and on its best two at that size's every unit (<see cref="OfferAnswerGrid"/>'s Lines, so a width is
        /// <c>Lines[i].One * Size</c>), and the two columns' widths - <paramref name="rooms"/> is what stands beside a caption in
        /// its box, a lock's room. The size is 16 at the text's scale where every caption holds one line. Where they do not, the
        /// widest caption wraps first, and the size comes down toward 14 at the scale before the next widest wraps, so a caption
        /// keeps its one line over a larger type; with every caption on two lines and still too wide, the size goes on down to 12.
        /// Two lines are the most a caption takes, and the columns split the width evenly where both hold their captions so.
        /// </summary>
        private (int Size, float Left, float Right, (float One, float Two)[] Lines) OfferAnswerGrid(IList<TMP_Text> captions, IList<float> rooms,
            float width, float gap, float inset)
        {
            int count = captions.Count, top = Mathf.RoundToInt(16f * FontScale);
            int comfortable = Mathf.Min(top, Mathf.RoundToInt(14f * FontScale)), floor = Mathf.Min(top, 12);
            // Each caption measured once at the top size, per unit of size: type's width goes as its size.
            var lines = captions.Select(caption => OfferCaptionLines(caption, top)).ToArray();
            // The widest first: the order the captions wrap in, the same at every size.
            var order = Enumerable.Range(0, count).OrderByDescending(i => lines[i].One + rooms[i] / top).ThenBy(i => i).ToList();
            float half = count > 1 ? (width - gap) * .5f : width;
            (bool Fits, float Left, float Right) Fit(int size, int wrapped)
            {
                float left = 0f, right = 0f;
                for (int i = 0; i < count; i++)
                {
                    bool wraps = order.IndexOf(i) < wrapped;
                    float need = (wraps ? lines[i].Two : lines[i].One) * size + 2f + rooms[i] + 2f * inset;
                    if (count == 1 || i % 2 == 0) left = Mathf.Max(left, need); else right = Mathf.Max(right, need);
                }
                if (count == 1) return (left <= width + .5f, width, 0f);
                if (left + right + gap > width + .5f) return (false, 0f, 0f);
                if (left <= half + .5f && right <= half + .5f) return (true, half, half);
                float spare = (width - gap - left - right) * .5f;
                return (true, left + spare, right + spare);
            }
            for (int wrapped = 0; wrapped <= count; wrapped++)
                for (int size = top; size >= comfortable; size--)
                {
                    var fit = Fit(size, wrapped);
                    if (fit.Fits) return (size, fit.Left, fit.Right, lines);
                }
            for (int size = comfortable - 1; size >= floor; size--)
            {
                var fit = Fit(size, count);
                if (fit.Fits) return (size, fit.Left, fit.Right, lines);
            }
            // Nothing holds even at 12: even columns, and the captions' own auto-size takes them the rest of the way.
            return (floor, half, half, lines);
        }

        /// <summary>
        /// A caption's width per unit of type at <paramref name="size"/>: on one line, and on the best two its words break into -
        /// the narrower of each break's two lines' wider - which is its one line where it has one word.
        /// </summary>
        private static (float One, float Two) OfferCaptionLines(TMP_Text caption, int size)
        {
            if (caption == null || size <= 0) return (0f, 0f);
            caption.enableAutoSizing = false;
            caption.fontSize = size;
            string words = caption.text ?? "";
            float Width(string part) => caption.GetPreferredValues(part).x;
            float one = Width(words), two = one;
            for (int at = words.IndexOf(' '); at > 0; at = words.IndexOf(' ', at + 1))
                two = Mathf.Min(two, Mathf.Max(Width(words.Substring(0, at).TrimEnd()), Width(words.Substring(at + 1).TrimStart())));
            return (one / size, two / size);
        }

        /// <summary>An answer: the panel's interactive chrome, named and captioned by <paramref name="caption"/>, centred at its own size.</summary>
        private Button OfferAnswer(RectTransform card, string caption, Action action)
        {
            float inset = OfferAnswerInset * FontScale;
            var rect = Chrome(caption, card, UiTheme.Emphasis.Interactive);
            HudEmphasis.Promote(rect, UiTheme.Emphasis.Interactive);
            var button = FinishButton(rect, caption, action, inset, inset);
            OfferCaption(button.GetComponentInChildren<TMP_Text>());
            return button;
        }

        /// <summary>The room a locked answer's lock takes beside its caption, at the text's scale.</summary>
        private float OfferLockRoom => (16f + 8f) * FontScale;

        /// <summary>
        /// A yes that cannot be given, drawn as the house draws a locked option (<see cref="LockedAction"/>): a
        /// resting edge, the caption muted, a lock where the icon set has one, and a disabled button named and
        /// captioned as the open one, so a test and a screen reader find it by the same words and learn it is
        /// unavailable. It commits nothing even if something presses it. <paramref name="side"/> is the caption's and the
        /// lock's margin from the edges where it is not an answer's own (a card's grid of its own answers, R5b).
        /// </summary>
        private Button OfferLockedAnswer(RectTransform card, string caption, float side = -1f)
        {
            float s = FontScale, inset = side >= 0f ? side : OfferAnswerInset * s, lockSide = 16f * s;
            var rect = Chrome(caption, card, UiTheme.Emphasis.Resting);
            var button = rect.gameObject.AddComponent<Button>();
            button.interactable = false;
            var text = NewText(rect, caption, 16, UiTheme.Muted);
            Stretch(text.rectTransform, inset, 5f, inset + OfferLockRoom, 5f);
            OfferCaption(text);
            var glyph = HudPrimitives.Glyph(LockMarkName, rect, "lock", UiTheme.Muted, Vector2.zero, lockSide);
            if (glyph != null)
            {
                var place = glyph.rectTransform;
                place.anchorMin = place.anchorMax = new Vector2(1f, .5f);
                place.pivot = new Vector2(1f, .5f);
                place.anchoredPosition = new Vector2(-inset, 0f);
            }
            return button;
        }

        /// <summary>An answer's caption at the board's size: 16, down to 12 where the width needs it, on one line, centred.</summary>
        private void OfferCaption(TMP_Text caption)
        {
            if (caption == null) return;
            caption.enableAutoSizing = false;
            caption.fontSize = Mathf.RoundToInt(16 * FontScale);
            AutoSize(caption, 12);
            caption.textWrappingMode = TextWrappingModes.NoWrap;
            caption.alignment = TextAlignmentOptions.Center;
        }

        private static float OfferCaptionWidth(Button button)
        {
            var caption = button != null ? button.GetComponentInChildren<TMP_Text>() : null;
            return caption == null ? 0f : Mathf.Ceil(caption.GetPreferredValues(caption.text).x) + 2f;
        }

        /// <summary>Stands an answer on the card's foot, <paramref name="fromFoot"/> above the pad, so a card made taller keeps its answers at its foot.</summary>
        private void OfferAnswerAt(Button button, float x, float fromFoot, float width, float height)
        {
            var rect = (RectTransform)button.transform;
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = new Vector2(x, OfferPad * FontScale + fromFoot);
            rect.sizeDelta = new Vector2(width, height);
        }

        /// <summary>
        /// The largest text's pager at the board's foot: "Previous offer" and "Next offer", each only where there is
        /// somewhere to go, and where the player is - "1 of 2" - on the HUD's one pager row. A turn shows that card
        /// and draws the board again, keeping the keyboard on the pager. Returns the height it takes.
        /// </summary>
        private float OfferPager(RectTransform board, float y, float width, int page, int pages, Action redraw)
        {
            float s = FontScale, height = OfferPagerPill * s;
            var row = EndScreenKit.Box(OfferPagerName, board, 0f, y, width, height);
            PagerRow(row, width, height, 90f * s, PreviousOfferCaption, NextOfferCaption, OfferCountName,
                (page + 1) + " of " + pages, page, pages, to =>
                {
                    ShowLargestPage(OfferBoardName, to);
                    largestTurned = true;
                    redraw?.Invoke();
                });
            if (largestTurned)
            {
                largestTurned = false;
                preferredSelection = preferredSelection == NextOfferCaption && page < pages - 1 ? NextOfferCaption
                    : preferredSelection == PreviousOfferCaption && page > 0 ? PreviousOfferCaption
                    : page < pages - 1 ? NextOfferCaption : PreviousOfferCaption;
                restoreSelection = true;
            }
            return height;
        }

        /// <summary>
        /// A label of the card's at (<paramref name="x"/>, <paramref name="y"/>), as tall as its words need at
        /// <paramref name="width"/> and never shorter than a line and a third of its type. Returns its height;
        /// nothing, and no height, without words.
        /// </summary>
        private float OfferText(RectTransform parent, string name, string words, int size, Color colour, float x, float y, float width,
            TMP_FontAsset weight = null, FontStyles style = FontStyles.Normal, float spacing = 0f) =>
            OfferLabel(parent, name, words, size, colour, x, y, width, weight, style, spacing).Height;

        /// <summary>The same label, and the label itself for a caller that measures it; no label, and no height, without words.</summary>
        private (TMP_Text Label, float Height) OfferLabel(RectTransform parent, string name, string words, int size, Color colour, float x, float y,
            float width, TMP_FontAsset weight = null, FontStyles style = FontStyles.Normal, float spacing = 0f)
        {
            if (string.IsNullOrEmpty(words)) return (null, 0f);
            var label = NewText(parent, words, size, colour);
            label.name = name;
            if (weight != null) label.font = weight;
            label.fontStyle = style;
            label.characterSpacing = spacing;
            label.alignment = TextAlignmentOptions.TopLeft;
            EndScreenKit.Place(label.rectTransform, x, y, width, 10f);
            return (label, EndScreenKit.Wrapped(label, width));
        }

        /// <summary>A one-line box for type of this size at the text's scale: 1.32 times it, Inter's line and its margin.</summary>
        private float OfferLineBox(int size) => Mathf.Ceil(Mathf.RoundToInt(size * FontScale) * 1.32f) + 1f;

        /// <summary>The role's word on a pill at the photo's foot, in the role's colour, the word in ink.</summary>
        private void OfferPill(RectTransform photo, string word, Color colour, float widest)
        {
            float s = FontScale, box = OfferLineBox(11);
            var pill = Panel("Role", photo, colour, 4);
            pill.GetComponent<Image>().raycastTarget = false;
            Anchor(pill, new Vector2(.5f, 0f), new Vector2(.5f, 0f), new Vector2(0f, 4f * s), new Vector2(Mathf.Min(word.Length * 7.5f * s + 16f * s, widest), box));
            var text = FixedText(pill, word, 11, UiTheme.Ink, Vector2.zero, pill.sizeDelta);
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            AutoSize(text, 10);
        }

        /// <summary>A role's badge, its glyph without the pack, its colour and its word: the block's target, the crown, the veto.</summary>
        private static (string Pack, string Glyph, Color Tint, string Pill) OfferRoleMark(string role)
        {
            switch (role)
            {
                case RoleTone.Role.HeadOfHousehold: return (PackArt.Pack8IconHoh, "crown", UiTheme.Gold, "HOH");
                case RoleTone.Role.VetoHolder: return (PackArt.Pack8IconVeto, "veto-token", UiTheme.Gold, "VETO");
                case RoleTone.Role.Nominee: return (PackArt.Pack8IconTarget, "target", UiTheme.Danger, "NOM");
                default: return (null, null, UiTheme.Muted, null);
            }
        }

        /// <summary>
        /// A tone's colour on its mark and its word. Never the only signal: each tone has a mark of its own
        /// (<see cref="RoleOffers.ToneMark"/>) and its word beside it.
        /// </summary>
        public static Color ToneTint(string tone)
        {
            switch (tone)
            {
                case RoleTone.Bitter: return UiTheme.Strategic;
                case RoleTone.Threatening: return UiTheme.Danger;
                case RoleTone.Desperate: return UiTheme.Warning;
                case RoleTone.Friendly: return UiTheme.Allied;
                case RoleTone.Earnest: return UiTheme.Gold;
                case RoleTone.Calculated: return UiTheme.Accent;
                default: return UiTheme.Muted;
            }
        }
    }
}
