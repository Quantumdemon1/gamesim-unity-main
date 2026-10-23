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
    /// without the role moments draws its rows as it always did.
    ///
    /// <para>A card is the asker's face - from <see cref="CharacterPortraits"/> only, never an initials avatar - with
    /// their role's badge and its word on a pill, their name, the deal's chip and what it asks, the tone with its
    /// mark, the situation word, the player's read, the quote, the terms, the stakes and the player's own history
    /// with them, each a label of its own in <see cref="RoleOffers"/>' words, and under them the two answers the rows
    /// always had, captioned as they always were and each described through <see cref="DescribeOfferControl"/>.
    /// Nothing is appended to a caption. A card whose yes cannot be kept - the player has given their word on the
    /// veto to the other nominee - says why in a label of its own over a disabled yes, and its no stays.</para>
    ///
    /// <para>The cards stand side by side as far as the column holds them at the narrowest a card reads, and one
    /// over another below that (the diary's column). At the largest text they page instead (the owner's decision):
    /// one card a page, with "Previous offer" and "Next offer" and where the player is - "1 of 2" - at the board's
    /// foot, on the one pager row the HUD draws (<see cref="PagerRow"/>). At the standard and the larger text every
    /// card is on the one page. Nothing on the board travels or fades: the cards cut in under every motion setting,
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
            OfferLockName = "Offer lock", OfferPagerName = "Offer pager", OfferCountName = "Offer count";

        /// <summary>The board's own words: its eyebrow, and what answering costs.</summary>
        public const string OfferBoardEyebrow = "INCOMING OFFERS", OfferBoardFreeLine = "Answering uses no conversation";

        /// <summary>The largest text's pager on the board, new captions of its own (§5.2).</summary>
        public const string PreviousOfferCaption = "Previous offer", NextOfferCaption = "Next offer";

        /// <summary>One card: its words, the row it answers, why its yes is locked (null when it is not), and what each answer does.</summary>
        public sealed class OfferBoardCard
        {
            public RoleOffers.Card Card;
            public DealState Offer;
            public string LockReason;
            public Action Accept, Decline;
        }

        /// <summary>What the director hands the board: the line under its eyebrow, its cards in their order, and how to draw it again when a page turns.</summary>
        public sealed class OfferBoardSpec
        {
            public string Line;
            public IList<OfferBoardCard> Cards;
            public Action Redraw;
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
            float y = OfferBoardHeader(board, width, spec.Line) + gap;

            int count = spec.Cards.Count;
            bool paged = LargestTextPages && count > 1;
            int page = paged ? LargestPageOf(OfferBoardName, count) : 0;
            var shown = paged ? new List<OfferBoardCard> { spec.Cards[page] } : spec.Cards.ToList();
            int across = paged ? 1 : Mathf.Clamp(Mathf.FloorToInt((width + gap) / (OfferCardLeast * s + gap)), 1, count);
            float cardWidth = Mathf.Min(OfferCardMost * s, (width - (across - 1) * gap) / across);
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
        /// holds both, the line under them, and what answering costs under the line where it does not. Returns
        /// its height.
        /// </summary>
        private float OfferBoardHeader(RectTransform board, float width, string line)
        {
            float s = FontScale, eyebrowBox = OfferLineBox(13), freeBox = OfferLineBox(12);
            var head = EndScreenKit.Box(OfferBoardHeaderName, board, 0f, 0f, width, 10f);
            var eyebrow = NewText(head, OfferBoardEyebrow, 13, UiTheme.Gold);
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
            if (!string.IsNullOrEmpty(line)) y += OfferText(head, OfferLineName, line, 13, Paper, 0f, y, width) + 2f * s;
            if (!beside)
            {
                EndScreenKit.Place(free.rectTransform, 0f, y, width, freeBox);
                y += freeBox;
            }
            head.sizeDelta = new Vector2(width, y);
            return y;
        }

        /// <summary>
        /// One card at (<paramref name="x"/>, <paramref name="y"/>) of the board, <paramref name="width"/> wide: the
        /// face and its role, the name, the chip and what it asks beside it; the tone, the situation and the read;
        /// the quote, the terms, the stakes, the history and any lock; and the answers at its foot. Returns the card
        /// and the height its words need, which the row may make taller.
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

            float at = Mathf.Max(top, pad + photoHeight) + 8f * s;
            at += OfferFacts(card, words, pad, at, inner) + 6f * s;
            if (!string.IsNullOrEmpty(words.quote))
                at += OfferText(card, OfferQuoteName, "“" + words.quote + "”", 13, Paper, pad, at, inner, style: FontStyles.Italic) + 4f * s;
            at += OfferText(card, OfferTermsName, words.terms, 12, Paper, pad, at, inner) + 2f * s;
            at += OfferText(card, OfferStakesName, words.stakes, 12, UiTheme.Muted, pad, at, inner) + 2f * s;
            if (!string.IsNullOrEmpty(words.history)) at += OfferText(card, OfferHistoryName, words.history, 12, UiTheme.Heading, pad, at, inner) + 2f * s;
            if (!string.IsNullOrEmpty(entry.LockReason)) at += OfferText(card, OfferLockName, entry.LockReason, 12, UiTheme.Warning, pad, at, inner) + 2f * s;
            at += 8f * s;
            at += OfferAnswers(card, state, entry, pad, inner);
            return (card, at + pad);
        }

        /// <summary>
        /// The tone, the situation word and the player's read, one after another across the card and on as many
        /// lines as the width needs: the tone as its mark in a disc of its colour and its word, never the colour
        /// alone (§5.4). Returns the height they take.
        /// </summary>
        private float OfferFacts(RectTransform card, RoleOffers.Card words, float x0, float y0, float width)
        {
            float s = FontScale, gap = 14f * s, disc = 18f * s, toneBox = OfferLineBox(13), factBox = OfferLineBox(12);
            float line = Mathf.Max(disc, toneBox);
            var tint = ToneTint(words.tone);
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            var parts = new List<(RectTransform Rect, float Width, float Height)>();

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
            parts.Add((tone, disc + 6f * s + saidWidth, line));

            foreach (var (name, text, colour) in new[] { (OfferSituationName, words.situation, Paper), (OfferReadName, words.read, UiTheme.Muted) })
            {
                if (string.IsNullOrEmpty(text)) continue;
                var label = NewText(card, text, 12, colour);
                label.name = name;
                label.textWrappingMode = TextWrappingModes.NoWrap;
                label.overflowMode = TextOverflowModes.Ellipsis;
                parts.Add((label.rectTransform, Mathf.Min(width, Mathf.Ceil(label.GetPreferredValues(label.text).x) + 2f), factBox));
            }

            float x = 0f, y = 0f;
            foreach (var part in parts)
            {
                if (x > 0f && x + part.Width > width) { x = 0f; y += line + 2f * s; }
                EndScreenKit.Place(part.Rect, x0 + x, y0 + y + (line - part.Height) * .5f, Mathf.Min(part.Width, width), part.Height);
                x += part.Width + gap;
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
        /// unavailable. It commits nothing even if something presses it.
        /// </summary>
        private Button OfferLockedAnswer(RectTransform card, string caption)
        {
            float s = FontScale, inset = OfferAnswerInset * s, lockSide = 16f * s;
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
            TMP_FontAsset weight = null, FontStyles style = FontStyles.Normal, float spacing = 0f)
        {
            if (string.IsNullOrEmpty(words)) return 0f;
            var label = NewText(parent, words, size, colour);
            label.name = name;
            if (weight != null) label.font = weight;
            label.fontStyle = style;
            label.characterSpacing = spacing;
            label.alignment = TextAlignmentOptions.TopLeft;
            EndScreenKit.Place(label.rectTransform, x, y, width, 10f);
            return EndScreenKit.Wrapped(label, width);
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
