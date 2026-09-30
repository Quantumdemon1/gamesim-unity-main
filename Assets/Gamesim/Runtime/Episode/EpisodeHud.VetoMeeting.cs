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
    /// The veto meeting's screens on the strategy stage (PACK8-PASS-PLAN B3, the owner's mockups 81
    /// and 82): who holds what this week in a strip across the header, the holder and the block as
    /// one row of cards, the holder's two ways - use the veto or keep the nominations - and, after
    /// the meeting, one row that says who was saved and who went up in their place.
    ///
    /// <para>Every piece here frames words and controls the screen already had. A control keeps
    /// its caption, which is how the tests, the walks and a screen reader find it; the mockup's
    /// labels ("USE THE VETO", "Keep nominations the same", "HOLD THE MEETING") are separate words
    /// beside the caption, never part of it. Nothing here is saved or commits anything.</para>
    /// </summary>
    public sealed partial class EpisodeHud
    {
        /// <summary>The meeting's parts, by the names a test finds them by.</summary>
        public const string HouseStatusStripName = "House status strip", MeetingCardsName = "Meeting cards",
            ReplacementMarkName = "Replacement marker", BlockPillName = "Block pill", MeetingEyebrowName = "Meeting eyebrow",
            MeetingInfoStripName = "Meeting info strip", KeepSubtitleName = "Keep subtitle", WayOnHeadlineName = "Way on headline";

        /// <summary>The narrowest an upright card goes before the row turns to short cards, at the resting text size: a pill still reads on it.</summary>
        private const float MeetingUprightFloor = 96f;
        /// <summary>A short card's height, and the least it may be given, at the resting text size.</summary>
        private const float MeetingShortHeight = 80f, MeetingShortFloor = 64f;
        /// <summary>The room the replacement's arrow and its two words take between two cards, at the resting text size.</summary>
        private const float MeetingMarkerWidth = 120f;
        /// <summary>The narrowest a peer choice about a houseguest stands in a grid: the face, a name and the player's own reading.</summary>
        public const float MeetingChoiceWidth = 330f;

        /// <summary>One card of the meeting's row: who, the pill for what they are this week, and a second pill when they are two things.</summary>
        public struct MeetingCard
        {
            public string Id, Pill, Second, Frame;
            public Color PillColour, SecondColour;
            /// <summary>The holder's card is edged in the veto's gold.</summary>
            public bool Gold;

            public MeetingCard(string id, string pill, Color pillColour, string frame, bool gold = false,
                string second = null, Color secondColour = default)
            {
                Id = id; Pill = pill; PillColour = pillColour; Frame = frame; Gold = gold;
                Second = second; SecondColour = secondColour;
            }
        }

        // ------------------------------------------------------------ the header

        /// <summary>
        /// The house's status line in a strip across the strategy stage's header (mockup 81's
        /// context strip): the one line, word for word, in a label of its own, so the line a test
        /// reads is the line the house has always said. As tall as its words need at the column's
        /// width. False, with nothing drawn, off the strategy stage, where the caller writes it as
        /// the paragraph it always was.
        /// </summary>
        public bool HouseStatusStrip(string words)
        {
            if (string.IsNullOrEmpty(words) || modal == null || activityLayout != ActivityLayout.Strategy) return false;
            float s = FontScale, pad = 9f * s, inset = 18f * s;
            float inner = Mathf.Max(120f, modal.sizeDelta.x - 2f * PinnedSide() - 2f * inset);
            var label = NewText(modal, words, 16, Paper);
            label.alignment = TextAlignmentOptions.Center;
            // Measured at its own size and the width it will have, before the row is taken: the
            // header's rows are fixed heights, and the scroll starts under them.
            float need = Mathf.Max(Mathf.Ceil(label.GetPreferredValues(label.text, inner, 0f).y), 16f * 1.3f * s);
            var row = StrategyHeaderRow(HouseStatusStripName, need + 2f * pad);
            if (row == null) { label.gameObject.SetActive(false); Destroy(label.gameObject); return false; }
            EndScreenKit.Frame(row, PackArt.Pack8HeaderStrip, 10f * s, new Color(Surface.r, Surface.g, Surface.b, .92f));
            label.rectTransform.SetParent(row, false);
            Stretch(label.rectTransform, inset, pad, inset, pad);
            AutoSize(label, 12);
            return true;
        }

        // ------------------------------------------------------------ the column

        /// <summary>Where the column's next row will stand: the place a row built later is moved back to.</summary>
        public int MeetingColumnMark() => content != null ? content.childCount : -1;

        /// <summary>
        /// The meeting's title and its line, centred as the mockups set them: the ceremony's own
        /// title - the name a test reads it by - without the week's eyebrow, which the phase band
        /// already says.
        /// </summary>
        public void MeetingTitle(string title, string line)
        {
            if (content == null) return;
            int first = content.childCount;
            CeremonyTitle(null, title, line, UiTheme.Gold);
            for (int i = first; i < content.childCount; i++)
            {
                var text = content.GetChild(i).GetComponent<TMP_Text>();
                if (text != null) text.alignment = TextAlignmentOptions.Top;
            }
        }

        /// <summary>
        /// The height the column has left under what is in it now, less the gap a new row takes
        /// and a margin: what a step can still give its row of cards before it scrolls. Read with
        /// the way on already pinned, so the scroll is the height it will be.
        /// </summary>
        public float MeetingRoomLeft()
        {
            if (content == null || modalScroll == null || modalScroll.viewport == null) return 0f;
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            var layout = content.GetComponent<VerticalLayoutGroup>();
            float gap = layout != null ? layout.spacing : 12f;
            return modalScroll.viewport.rect.height - LayoutUtility.GetPreferredHeight(content) - gap - 8f * FontScale;
        }

        /// <summary>
        /// The meeting's row of cards, moved to <paramref name="at"/> in the column: each card is a
        /// face, its pills and the name, the holder's edged in gold. Upright cards no taller than
        /// <paramref name="fitHeight"/> and no wider than a ceremony's faces; below the narrowest
        /// an upright card reads at, short cards with the face beside the name; with no room even
        /// for those, nothing - the strip across the header and the controls still say who is who -
        /// and null. A <paramref name="markerBefore"/> puts the replacement's arrow and its words
        /// before that card, pointing back at the one before it.
        ///
        /// <para>The cards keep the names every face card carries - "Face · {name}", and "Role" on
        /// the first pill - and none of them is a control: the ceremony is the house's.</para>
        /// </summary>
        public RectTransform MeetingCards(IReadOnlyList<MeetingCard> cards, int markerBefore, string markerWords, float fitHeight, int at)
        {
            if (cards == null || cards.Count == 0 || content == null) return null;
            float s = FontScale, gap = FaceGap * s, column = ContentWidth();
            // A row that cannot fit would make the step scroll, which is what the screen is for not doing.
            if (fitHeight < MeetingShortFloor * s) return null;
            bool marker = markerBefore > 0 && markerBefore < cards.Count && !string.IsNullOrEmpty(markerWords);
            float markerWidth = marker ? MeetingMarkerWidth * s : 0f;
            int count = cards.Count, gaps = count - 1 + (marker ? 1 : 0);
            float byColumn = (column - markerWidth - gaps * gap) / count;
            float upright = Mathf.Min(150f * s, byColumn, (fitHeight - 50f * s) / 1.12f);
            bool tall = upright >= MeetingUprightFloor * s;
            float width, height;
            if (tall) { width = upright; height = width * 1.12f + 50f * s; }
            else
            {
                height = Mathf.Min(fitHeight, MeetingShortHeight * s);
                width = Mathf.Min(300f * s, byColumn);
            }

            var row = new GameObject(MeetingCardsName, typeof(RectTransform), typeof(LayoutElement)).GetComponent<RectTransform>();
            row.SetParent(content, false);
            if (at >= 0 && at < content.childCount) row.SetSiblingIndex(at);
            var element = row.GetComponent<LayoutElement>();
            element.minHeight = element.preferredHeight = height;
            var state = director != null ? director.Snapshot : null;
            // Centred in the column: every part is placed from the row's top centre, so the row
            // does not need the width the layout gives it later.
            float x = -(count * width + markerWidth + gaps * gap) * .5f;
            for (int i = 0; i < count; i++)
            {
                if (marker && i == markerBefore)
                {
                    MeetingMarker(row, markerWords, x, markerWidth, height);
                    x += markerWidth + gap;
                }
                MeetingCardAt(row, cards[i], state, x, width, height, tall);
                x += width + gap;
            }
            return row;
        }

        private void MeetingCardAt(RectTransform row, MeetingCard face, EpisodeState state, float x, float width, float height, bool tall)
        {
            float s = FontScale;
            var actor = state != null ? state.Find(face.Id) : null;
            string name = actor != null ? actor.name : face.Id;
            var card = Chrome("Face · " + name, row);
            card.anchorMin = card.anchorMax = new Vector2(.5f, 1f);
            card.pivot = new Vector2(0f, 1f);
            card.anchoredPosition = new Vector2(x, 0f);
            card.sizeDelta = new Vector2(width, height);
            card.GetComponent<Image>().raycastTarget = false;
            // The pack's card for what they are this week; the drawn glass without it, the
            // holder's edged in gold either way.
            if (!string.IsNullOrEmpty(face.Frame) && UiTheme.Pack(face.Frame) != null)
                EndScreenKit.Frame(card, face.Frame, 10f * s, new Color(Surface.r, Surface.g, Surface.b, .92f));
            else if (face.Gold) UiTheme.AddBorder(card, UiTheme.GlassRadius, UiTheme.Gold);
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);

            if (tall)
            {
                float photo = width * 1.12f;
                var picture = HudPrimitives.RectPortrait(card, "Photo", Portrait(face.Id), actor, new Vector2(width - 12f * s, photo), 7);
                picture.anchorMin = picture.anchorMax = new Vector2(.5f, 1f);
                picture.pivot = new Vector2(.5f, 1f);
                picture.anchoredPosition = new Vector2(0f, -6f * s);
                // On the photo's foot, the first pill over the second, as mockup 81 stacks VETO over ON THE BLOCK.
                float widest = width - 20f * s, foot = 5f * s;
                if (!string.IsNullOrEmpty(face.Second))
                {
                    MeetingPill(picture, BlockPillName, face.Second, face.SecondColour, widest, new Vector2(.5f, 0f), new Vector2(.5f, 0f), new Vector2(0f, foot));
                    foot += 20f * s;
                }
                if (!string.IsNullOrEmpty(face.Pill))
                    MeetingPill(picture, "Role", face.Pill, face.PillColour, widest, new Vector2(.5f, 0f), new Vector2(.5f, 0f), new Vector2(0f, foot));
                var label = NewText(card, name, 15, Paper);
                if (semibold != null) label.font = semibold;
                label.alignment = TextAlignmentOptions.Top;
                AutoSize(label, 10);
                Anchor(label.rectTransform, new Vector2(.5f, 1f), new Vector2(.5f, 1f), new Vector2(0f, -(photo + 12f * s)), new Vector2(width - 10f * s, 34f * s));
                return;
            }

            // Short: the face on the left, the name and the pills beside it.
            float side = height - 12f * s;
            var square = HudPrimitives.RectPortrait(card, "Photo", Portrait(face.Id), actor, new Vector2(side, side), 7);
            square.anchorMin = square.anchorMax = new Vector2(0f, 1f);
            square.pivot = new Vector2(0f, 1f);
            square.anchoredPosition = new Vector2(6f * s, -6f * s);
            float left = side + 16f * s, inner = Mathf.Max(40f, width - left - 8f * s), nameBox = 15f * 1.3f * s;
            float top = Mathf.Max(6f * s, (height - nameBox - 4f * s - 17f * s) * .5f);
            var words = FixedText(card, name, 15, Paper, new Vector2(left, -top), new Vector2(inner, nameBox));
            if (semibold != null) words.font = semibold;
            words.textWrappingMode = TextWrappingModes.NoWrap;
            AutoSize(words, 10);
            float pillX = left, pillY = -(top + nameBox + 4f * s);
            if (!string.IsNullOrEmpty(face.Pill))
                pillX += MeetingPill(card, "Role", face.Pill, face.PillColour, inner, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(pillX, pillY)).sizeDelta.x + 4f * s;
            if (!string.IsNullOrEmpty(face.Second))
                MeetingPill(card, BlockPillName, face.Second, face.SecondColour, Mathf.Max(30f * s, left + inner - pillX), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(pillX, pillY));
        }

        /// <summary>A pill with a word in it, in the face cards' style: the week's colour, the word in ink, as wide as the word and no wider than <paramref name="widest"/>.</summary>
        private RectTransform MeetingPill(RectTransform parent, string name, string word, Color colour, float widest,
            Vector2 anchor, Vector2 pivot, Vector2 position)
        {
            float s = FontScale;
            var pill = Panel(name, parent, colour, 4);
            pill.GetComponent<Image>().raycastTarget = false;
            float width = Mathf.Min(word.Length * 7.5f * s + 16f * s, widest);
            Anchor(pill, anchor, pivot, position, new Vector2(width, 17f * s));
            var text = FixedText(pill, word, 11, UiTheme.Ink, Vector2.zero, pill.sizeDelta);
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            AutoSize(text, 7);
            return pill;
        }

        /// <summary>The replacement's arrow between two cards, pointing back at the replacement, over its two words.</summary>
        private void MeetingMarker(RectTransform row, string words, float x, float width, float height)
        {
            float s = FontScale;
            var mark = EndScreenKit.Box(ReplacementMarkName, row, 0f, 0f, width, height);
            mark.anchorMin = mark.anchorMax = new Vector2(.5f, 1f);
            mark.pivot = new Vector2(0f, 1f);
            mark.anchoredPosition = new Vector2(x, 0f);
            float wordsBox = 2f * 11f * 1.3f * s + 2f * s;
            var arrow = EndScreenKit.Picture("Arrow", mark, PackArt.Pack8ArrowRight, null, UiTheme.Gold,
                new Vector2(width * .5f, -(height * .5f - 18f * s)), 34f * s);
            // The pack draws it pointing right; the replacement is on its left.
            if (arrow != null) arrow.rectTransform.localScale = new Vector3(-1f, 1f, 1f);
            var label = FixedText(mark, words, 11, UiTheme.Gold, new Vector2(0f, -(height * .5f + (arrow != null ? 4f * s : -wordsBox * .5f))),
                new Vector2(width, wordsBox));
            label.alignment = TextAlignmentOptions.Top;
            label.characterSpacing = 2f;
            AutoSize(label, 8);
        }

        /// <summary>
        /// A section's eyebrow in the column (mockup 81's "USE THE VETO"): a glyph and the words,
        /// letterspaced in the section's colour. Words, never a control.
        /// </summary>
        public void MeetingEyebrow(string words, Color tint, string icon, string fallbackIcon)
        {
            if (content == null || string.IsNullOrEmpty(words)) return;
            float s = FontScale, box = 15f * 1.3f * s, height = box + 6f * s, glyph = 18f * s;
            var row = new GameObject(MeetingEyebrowName, typeof(RectTransform), typeof(LayoutElement)).GetComponent<RectTransform>();
            row.SetParent(content, false);
            var element = row.GetComponent<LayoutElement>();
            element.minHeight = element.preferredHeight = height;
            float left = 0f;
            if (EndScreenKit.Picture("Icon", row, icon, fallbackIcon, tint, new Vector2(glyph * .5f + 2f, -height * .5f), glyph) != null)
                left = glyph + 12f * s;
            var label = FixedText(row, words, 15, tint, new Vector2(left, -3f * s), new Vector2(Mathf.Max(80f, ContentWidth() - left), box));
            label.characterSpacing = 10f;
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            if (semibold != null) label.font = semibold;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            AutoSize(label, 11);
        }

        /// <summary>
        /// A line in a strip of its own across the column (mockup 81's info strip, mockup 82's
        /// outcome strip), fronted by a glyph: as tall as its words need at the column's width.
        /// </summary>
        public TMP_Text MeetingInfoStrip(string words, string frame, string icon, string fallbackIcon, Color tint)
        {
            if (content == null || string.IsNullOrEmpty(words)) return null;
            float s = FontScale, pad = 10f * s, glyph = 22f * s, side = 14f * s;
            var strip = new GameObject(MeetingInfoStripName, typeof(RectTransform), typeof(LayoutElement)).GetComponent<RectTransform>();
            strip.SetParent(content, false);
            EndScreenKit.Frame(strip, frame, 10f * s, new Color(Surface.r, Surface.g, Surface.b, .92f));
            float left = side + glyph + 10f * s, inner = Mathf.Max(80f, ContentWidth() - left - side);
            var label = NewText(strip, words, 14, Paper);
            label.alignment = TextAlignmentOptions.MidlineLeft;
            float need = Mathf.Max(Mathf.Ceil(label.GetPreferredValues(label.text, inner, 0f).y), 14f * 1.3f * s);
            float height = need + 2f * pad;
            var element = strip.gameObject.GetComponent<LayoutElement>();
            element.minHeight = element.preferredHeight = height;
            Stretch(label.rectTransform, left, pad, side, pad);
            AutoSize(label, 11);
            EndScreenKit.Picture("Icon", strip, icon, fallbackIcon, tint, new Vector2(side + glyph * .5f, -height * .5f), glyph);
            return label;
        }

        // ------------------------------------------------------------ the holder's choices

        /// <summary>
        /// One of the holder's saves, on Pack 8's gold button (mockup 81's "Use the veto"): the row
        /// PairedActionFor built, with its caption, its face and the player's own reading, framed.
        /// </summary>
        public void MeetingGoldFrame(Button button)
        {
            if (button == null) return;
            var rect = (RectTransform)button.transform;
            if (UiTheme.Pack(PackArt.Pack8ButtonGold) != null)
                EndScreenKit.Frame(rect, PackArt.Pack8ButtonGold, 12f * FontScale, new Color(Surface.r, Surface.g, Surface.b, .92f));
            else UiTheme.AddBorder(rect, UiTheme.GlassRadius, UiTheme.Gold);
        }

        /// <summary>
        /// The holder's way to keep the block (mockup 81's "Keep nominations the same"): a row of
        /// its own, captioned <paramref name="caption"/> as it always was, on the pack's secondary
        /// button with a lock, and the mockup's words as a line under the caption - a label of its
        /// own, never part of the caption.
        /// </summary>
        public Button MeetingKeepRow(string caption, string subtitle, Action action)
        {
            var button = Action(caption, action);
            var rect = (RectTransform)button.transform;
            float s = FontScale, glyph = 24f * s, left = 16f;
            if (UiTheme.Pack(PackArt.Pack8ButtonSecondary) != null)
                EndScreenKit.Frame(rect, PackArt.Pack8ButtonSecondary, 12f * s, new Color(Surface.r, Surface.g, Surface.b, .92f));
            var icon = EndScreenKit.Picture("Icon", rect, PackArt.Pack8IconLock, "key", Accent, Vector2.zero, glyph);
            if (icon != null)
            {
                var place = icon.rectTransform;
                place.anchorMin = place.anchorMax = new Vector2(0f, .5f);
                place.anchoredPosition = new Vector2(16f + glyph * .5f, 0f);
                left = 16f + glyph + 12f * s;
            }
            // The caption, the first words FinishButton put on the row: the ones every lookup finds it by.
            var words = rect.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(text => text.transform.parent == rect);
            if (words == null) return button;
            var box = words.rectTransform;
            if (string.IsNullOrEmpty(subtitle))
            {
                box.offsetMin = new Vector2(left, box.offsetMin.y);
                return button;
            }
            // The caption over the line, the pair centred on the row whatever height it grows to.
            float captionBox = 20f * 1.3f * s, lineBox = 13f * 1.3f * s, half = (captionBox + 2f * s + lineBox) * .5f;
            float right = box.offsetMax.x;
            box.anchorMin = new Vector2(0f, .5f); box.anchorMax = new Vector2(1f, .5f);
            box.offsetMin = new Vector2(left, half - captionBox);
            box.offsetMax = new Vector2(right, half);
            var under = NewText(rect, subtitle, 13, UiTheme.Muted);
            under.name = KeepSubtitleName;
            under.alignment = TextAlignmentOptions.MidlineLeft;
            under.textWrappingMode = TextWrappingModes.NoWrap;
            AutoSize(under, 10);
            var line = under.rectTransform;
            line.anchorMin = new Vector2(0f, .5f); line.anchorMax = new Vector2(1f, .5f);
            line.offsetMin = new Vector2(left, -half);
            line.offsetMax = new Vector2(right, -half + lineBox);
            return button;
        }

        /// <summary>A run of peer choices about houseguests laid as many to a row as their faces, names and readings leave room for.</summary>
        public sealed class MeetingChoiceGrid
        {
            internal RectTransform Row;
            internal int Across;
            internal float Width;
        }

        /// <summary>
        /// Starts a grid of peer choices, no narrower than <paramref name="narrowest"/> at the
        /// resting text size and at most <paramref name="most"/> to a row. Its rows are named as the
        /// panel's peer rows are, "Choice row", so a pair still reads as a pair.
        /// </summary>
        public MeetingChoiceGrid MeetingGrid(float narrowest, int most)
        {
            float s = FontScale, gap = 10f * s, column = ContentWidth();
            int across = Mathf.Clamp(Mathf.FloorToInt((column + gap) / (narrowest * s + gap)), 1, Mathf.Max(1, most));
            return new MeetingChoiceGrid { Across = across, Width = (column - (across - 1) * gap) / across };
        }

        /// <summary>
        /// One choice of a grid: the same row an action about a houseguest is - captioned by
        /// <paramref name="caption"/>, fronted by their face, with the player's own reading - at the
        /// grid's width, so the last row's choices are the size of the first's.
        /// </summary>
        public Button MeetingGridActionFor(MeetingChoiceGrid grid, string contestantId, string caption, Action action)
        {
            if (grid == null || content == null) return ActionFor(contestantId, caption, action);
            if (grid.Row == null || grid.Row.childCount >= grid.Across)
            {
                var row = new GameObject(ChoiceRowName, typeof(RectTransform), typeof(HorizontalLayoutGroup)).GetComponent<RectTransform>();
                row.SetParent(content, false);
                var layout = row.GetComponent<HorizontalLayoutGroup>();
                layout.spacing = 10f * FontScale;
                layout.childAlignment = TextAnchor.UpperLeft;
                layout.childControlWidth = true; layout.childControlHeight = true;
                layout.childForceExpandWidth = false; layout.childForceExpandHeight = true;
                grid.Row = row;
            }
            var button = ActionIn(grid.Row, caption, Portrait(contestantId), action);
            var element = button.GetComponent<LayoutElement>();
            if (element != null) { element.preferredWidth = grid.Width; element.flexibleWidth = 0f; }
            Annotate(button, contestantId);
            return button;
        }

        /// <summary>
        /// Marks which of a pick is the one taken: the pack's gold frame, the lit edge, and the
        /// pack's check on the face's corner. The others rest. Decoration over a choice the screen
        /// holds, not a commit.
        /// </summary>
        public void MeetingPicked(Button button, bool picked)
        {
            if (button == null) return;
            var rect = (RectTransform)button.transform;
            HudEmphasis.Promote(rect, picked ? UiTheme.Emphasis.Active : UiTheme.Emphasis.Interactive);
            if (!picked) return;
            MeetingGoldFrame(button);
            var check = UiTheme.Pack(PackArt.BadgeSelected);
            var image = new GameObject("Picked", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            image.rectTransform.SetParent(rect, false);
            float side = 22f * FontScale;
            Anchor(image.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(4f, -4f), new Vector2(side, side));
            image.sprite = check != null ? check : UiTheme.Circle();
            image.color = check != null ? Color.white : UiTheme.Gold;
            image.preserveAspect = true; image.raycastTarget = false;
        }

        // ------------------------------------------------------------ the way on

        /// <summary>
        /// The mockup's words for the pinned way on (mockup 81's "HOLD THE MEETING", mockup 82's
        /// "CONTINUE TO EVICTION CAMPAIGN"), as a headline over its caption: the caption keeps its
        /// words, under the headline, and the button keeps its name. Framed in the pack's
        /// <paramref name="frame"/> when the pack is there. Nothing without a pinned action.
        /// </summary>
        public void WayOnHeadline(string words, Color tint, string frame)
        {
            if (pinnedAction == null || string.IsNullOrEmpty(words)) return;
            var caption = pinnedAction.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(text => text.transform.parent == pinnedAction);
            if (caption == null) return;
            float s = FontScale, top = 6f * s, box = 12f * 1.3f * s;
            if (!string.IsNullOrEmpty(frame) && UiTheme.Pack(frame) != null)
                EndScreenKit.Frame(pinnedAction, frame, 12f * s, new Color(Surface.r, Surface.g, Surface.b, .92f));
            var head = NewText(pinnedAction, words, 12, tint);
            head.name = WayOnHeadlineName;
            head.characterSpacing = 4f;
            head.alignment = TextAlignmentOptions.MidlineLeft;
            head.textWrappingMode = TextWrappingModes.NoWrap;
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            if (semibold != null) head.font = semibold;
            AutoSize(head, 9);
            var place = head.rectTransform;
            var under = caption.rectTransform;
            place.anchorMin = new Vector2(0f, 1f); place.anchorMax = new Vector2(1f, 1f); place.pivot = new Vector2(.5f, 1f);
            place.offsetMin = new Vector2(under.offsetMin.x, -(top + box));
            place.offsetMax = new Vector2(under.offsetMax.x, -top);
            // The caption moves under the headline; the row is 57 tall at the resting size, and
            // the two need 6 + 15.6 over 26 and the foot's 5.
            under.offsetMax = new Vector2(under.offsetMax.x, -(top + box));
        }
    }
}
