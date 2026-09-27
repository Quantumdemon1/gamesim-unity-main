using System;
using System.Collections.Generic;
using Gamesim.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gamesim.Episode
{
    /// <summary>
    /// The diary room's column as Refinement Kit 6 draws it (preview 05): three tabs - your
    /// record, your memories, the decision pending - in place of one reading column that ran all of
    /// them together, and a review of a private answer as its own two cards (preview 06).
    /// </summary>
    public sealed partial class EpisodeHud
    {
        public const string DiaryTabsName = "Diary tabs";
        public const string DiaryStatusName = "Diary decision status";
        public const string DiaryRecordRowPrefix = "Record · ";
        public const string DiaryRulesCaption = "How this record works";
        public const string DiaryNothingPendingName = "No private decision pending";
        public const string DiaryReviewAnswerName = "Review answer";
        public const string DiaryReviewEffectsName = "Review effects";

        /// <summary>
        /// The diary room's column, at the resting text size: wide enough for the record's tabs and
        /// the kit's cards, and still a column - the chair keeps about 60% of the frame beside the
        /// rail. The house activities share the diary's layout at its old width.
        /// </summary>
        private const float DiaryRoomColumnWidth = 540f;
        private bool diaryRoomColumn;

        /// <summary>The diary's layout at the diary room's own width.</summary>
        public void SetDiaryRoomLayout()
        {
            diaryRoomColumn = true;
            try { SetActivityLayout(ActivityLayout.Diary); }
            finally { diaryRoomColumn = false; }
        }

        /// <summary>
        /// Whether there is a decision to make here, said first: a lock, a title and a line. Reading
        /// the page is never the decision.
        /// </summary>
        public RectTransform DiaryStatusCard(string title, string body)
        {
            if (content == null) return null;
            float s = FontScale, width = ContentWidth(), pad = 16f * s;
            var card = HudPrimitives.KitCard(DiaryStatusName, content, false, 14f);
            float x = pad;
            if (KitGlyph(card, PackArt.KitIconLock, UiTheme.Muted, new Vector2(0, 1), new Vector2(pad, -15f * s), 22f * s) != null) x += 34f * s;
            float y = PlacedCopy(card, title, 19, UiTheme.Weight.SemiBold, Paper, x, 12f * s, width - x - pad);
            if (!string.IsNullOrEmpty(body)) y = PlacedCopy(card, body, 15, UiTheme.Weight.Regular, UiTheme.Muted, x, y + 2f * s, width - x - pad);
            var size = card.gameObject.AddComponent<LayoutElement>();
            size.minHeight = size.preferredHeight = y + 14f * s;
            return card;
        }

        /// <summary>
        /// The record's values, one a row: its glyph, what it is, the value large, and the one line
        /// that qualifies it. The full rules are the disclosure's, not the reading column's.
        /// </summary>
        public void RecordSummary(IList<(string Label, string Icon, string Value, string Note)> rows)
        {
            if (content == null) return;
            float s = FontScale, width = ContentWidth(), x = 40f * s;
            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                var line = new GameObject(DiaryRecordRowPrefix + row.Label, typeof(RectTransform), typeof(LayoutElement)).GetComponent<RectTransform>();
                line.SetParent(content, false);
                KitGlyph(line, row.Icon, UiTheme.Muted, new Vector2(0, 1), new Vector2(4f * s, -4f * s), 24f * s);
                float y = PlacedCopy(line, row.Label, 17, UiTheme.Weight.SemiBold, Paper, x, 0f, width - x);
                y = PlacedCopy(line, row.Value, 26, UiTheme.Weight.Bold, Paper, x, y + 2f * s, width - x);
                if (!string.IsNullOrEmpty(row.Note)) y = PlacedCopy(line, row.Note, 15, UiTheme.Weight.Regular, UiTheme.Muted, x, y + 2f * s, width - x);
                if (i < rows.Count - 1)
                {
                    y += 12f * s;
                    var rule = HudPrimitives.Fill("Record rule", line, UiTheme.Edge(UiTheme.Emphasis.Interactive), 1);
                    Anchor(rule, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0f, -y), new Vector2(width, 1f));
                    y += 1f;
                }
                var size = line.GetComponent<LayoutElement>();
                size.minHeight = size.preferredHeight = y + 2f * s;
            }
        }

        /// <summary>
        /// A disclosure: a quiet button that opens and closes the long print under it. It is a
        /// button like any other - Tab reaches it and Enter presses it.
        /// </summary>
        public Button Disclosure(string caption, bool open, Action toggle)
        {
            if (content == null) return null;
            float s = FontScale;
            var holder = new GameObject(caption + " row", typeof(RectTransform), typeof(LayoutElement)).GetComponent<RectTransform>();
            holder.SetParent(content, false);
            var rect = KitButton(holder, caption, PackArt.KitIconInfo, toggle, out var button);
            rect.sizeDelta += new Vector2(26f * s, 0f);
            KitGlyph(rect, open ? PackArt.KitIconChevronDown : PackArt.KitIconChevronRight, UiTheme.Muted, new Vector2(1, .5f), new Vector2(-12f * s, 0f), 16f * s);
            Anchor(rect, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0f, -4f * s), rect.sizeDelta);
            var size = holder.GetComponent<LayoutElement>();
            size.minHeight = size.preferredHeight = rect.sizeDelta.y + 8f * s;
            return button;
        }

        /// <summary>
        /// One of a review's cards: an eyebrow with its glyph, the thing under review large, the
        /// lines that say what confirming it does, and an optional stamp saying it is not saved yet.
        /// </summary>
        public RectTransform ReviewCard(string name, string eyebrow, string icon, string headline, bool spoken,
            IList<string> lines, string stamp = null)
        {
            if (content == null) return null;
            float s = FontScale, width = ContentWidth(), pad = 18f * s, inner = width - 2f * pad;
            var card = HudPrimitives.KitCard(name, content, false, 14f);
            float y = 16f * s;
            if (!string.IsNullOrEmpty(eyebrow))
            {
                float x = pad;
                if (!string.IsNullOrEmpty(icon) && KitGlyph(card, icon, UiTheme.Strategic, new Vector2(0, 1), new Vector2(pad, -y + 2f * s), 22f * s) != null)
                    x += 32f * s;
                y = CardEyebrow(card, eyebrow, x, y, width - x - pad) + 6f * s;
            }
            if (!string.IsNullOrEmpty(headline))
                y = PlacedCopy(card, headline, spoken ? 22 : 19, UiTheme.Weight.SemiBold, Paper, pad, y, inner) + 10f * s;
            if (lines != null)
                foreach (var line in lines)
                    if (!string.IsNullOrEmpty(line))
                        y = PlacedCopy(card, line, 15, UiTheme.Weight.Regular, UiTheme.Muted, pad, y, inner) + 6f * s;
            if (!string.IsNullOrEmpty(stamp))
            {
                var pill = KitPill(card, "Stamp", stamp, UiTheme.SurfaceRaised, UiTheme.Muted, 13, 26f * s);
                Anchor(pill, new Vector2(0, 1), new Vector2(0, 1), new Vector2(pad, -(y + 4f * s)), pill.sizeDelta);
                y += pill.sizeDelta.y + 8f * s;
            }
            var size = card.gameObject.AddComponent<LayoutElement>();
            size.minHeight = size.preferredHeight = y + 10f * s;
            return card;
        }

        /// <summary>A quiet line with the kit's info mark.</summary>
        public RectTransform InfoNote(string name, string words) => IconNote(name, PackArt.KitIconInfo, words);

        /// <summary>
        /// The pending tab with nothing pending: the kit's illustration, the plain fact, and what
        /// that means - no decision is invented to fill the space.
        /// </summary>
        public RectTransform DiaryNothingPending(string body)
        {
            if (content == null) return null;
            float s = FontScale, width = ContentWidth();
            var root = new GameObject(DiaryNothingPendingName, typeof(RectTransform), typeof(LayoutElement)).GetComponent<RectTransform>();
            root.SetParent(content, false);
            float y = 10f * s;
            var art = UiTheme.Pack(PackArt.KitEmptyPrivate);
            if (art != null)
            {
                var image = new GameObject("Empty illustration", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                image.rectTransform.SetParent(root, false);
                float h = 110f * s;
                Anchor(image.rectTransform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0f, -y), new Vector2(h * 640f / 440f, h));
                image.sprite = art; image.color = new Color(UiTheme.Muted.r, UiTheme.Muted.g, UiTheme.Muted.b, .75f);
                image.preserveAspect = true; image.raycastTarget = false;
                y += h + 12f * s;
            }
            var title = NewText(root, "No private decision pending", 20, Paper);
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            if (semibold != null) title.font = semibold;
            title.alignment = TextAlignmentOptions.Center;
            float titleHeight = Mathf.Ceil(title.GetPreferredValues(title.text, width, 0f).y) + 4f;
            Anchor(title.rectTransform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0f, -y), new Vector2(width, titleHeight));
            y += titleHeight + 6f * s;
            if (!string.IsNullOrEmpty(body))
            {
                var line = NewText(root, body, 15, UiTheme.Muted);
                line.alignment = TextAlignmentOptions.Top;
                float lineHeight = Mathf.Ceil(line.GetPreferredValues(line.text, width, 0f).y) + 4f;
                Anchor(line.rectTransform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0f, -y), new Vector2(width, lineHeight));
                y += lineHeight;
            }
            var size = root.GetComponent<LayoutElement>();
            size.minHeight = size.preferredHeight = y + 12f * s;
            return root;
        }
    }
}
