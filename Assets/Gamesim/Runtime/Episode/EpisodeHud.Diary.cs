using System;
using Gamesim.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gamesim.Episode
{
    public sealed partial class EpisodeHud
    {
        /// <summary>The diary's header, so a test can find it the way it finds a named panel.</summary>
        public const string DiaryHeaderName = "Diary header";
        /// <summary>The diary's record card, so a test can find it the way it finds a named panel.</summary>
        public const string DiaryRecordName = "Diary record";
        /// <summary>The room's name, as the diary's header says it.</summary>
        public const string DiaryEyebrowCopy = "PRIVATE DIARY ROOM";
        /// <summary>The title of the room's own page (mockup-11).</summary>
        public const string DiaryOptionsTitle = "Confessional Options";

        /// <summary>
        /// The diary's head (mockup-11): the room's name as a letterspaced eyebrow and, under it,
        /// the page's title fronted by its glyph, with Close at the other end. It stands where the
        /// phase band stands on every other panel, because the diary is a room rather than a beat
        /// of the week. A ballot passes no title: it brings its own, the eviction vote's.
        /// </summary>
        public void DiaryHeader(string title) =>
            ScreenHeader(DiaryHeaderName, DiaryEyebrowCopy, title, UiTheme.Pack(PackArt.IconChat));

        /// <summary>The notebook's head, so a test can find it the way it finds a named panel.</summary>
        public const string NotebookHeaderName = "Notebook header";

        /// <summary>The settings screen's head, so a test can find it the way it finds a named panel.</summary>
        public const string SettingsHeaderName = "Settings header";

        /// <summary>
        /// A screen's head in place of the week's phase band: a letterspaced eyebrow, the screen's
        /// title behind its glyph, a soft rule, and Close at the other end - for the screens that
        /// are places or menus rather than beats of the week (the diary, the settings).
        /// </summary>
        public void ScreenHeader(string name, string eyebrowCopy, string title, Sprite mark)
        {
            if (modal == null) return;
            foreach (Transform child in modal)
                if (child.name == "Phase band" || child.name == "Panel control hint") child.gameObject.SetActive(false);
            float s = FontScale, width = modal.sizeDelta.x;
            float closeWidth = CompactClose();

            var header = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            header.SetParent(modal, false);
            header.SetSiblingIndex(0);
            float height = (string.IsNullOrEmpty(title) ? 46f : 72f) * s;
            Anchor(header, new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(width - closeWidth - 20f, height));

            var eyebrow = FixedText(header, eyebrowCopy, 12, name == DiaryHeaderName ? UiTheme.Strategic : UiTheme.Muted, new Vector2(18f, -16f * s), new Vector2(width - closeWidth - 44f, 18f * s));
            eyebrow.characterSpacing = 6f;
            var medium = UiTheme.Font(UiTheme.Weight.SemiBold);
            if (medium != null) eyebrow.font = medium;
            if (!string.IsNullOrEmpty(title))
            {
                float x = 18f;
                if (mark != null)
                {
                    var glyph = new GameObject("Diary mark", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                    glyph.rectTransform.SetParent(header, false);
                    Anchor(glyph.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(x, -36f * s), new Vector2(26f * s, 26f * s));
                    glyph.sprite = mark; glyph.color = UiTheme.Heading; glyph.preserveAspect = true; glyph.raycastTarget = false;
                    x += 34f * s;
                }
                // Stops short of Close, which shares the head's top line.
                var heading = FixedText(header, title, 21, UiTheme.Heading, new Vector2(x, -34f * s), new Vector2(width - x - 24f, 30f * s));
                heading.rectTransform.sizeDelta = new Vector2(Mathf.Max(80f, width - x - 30f), 30f * s);
                if (medium != null) heading.font = medium;
                AutoSize(heading, 15);
            }
            // A soft rule under the head, as the mockup's column draws one: the pack's divider, or a
            // hairline where the packs are not installed.
            var rule = new GameObject("Diary rule", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            rule.rectTransform.SetParent(modal, false);
            rule.raycastTarget = false;
            var soft = UiTheme.Pack(PackArt.DividerSoftBlue);
            if (soft != null) { rule.sprite = soft; rule.color = Color.white; }
            else rule.color = new Color(UiTheme.Hairline.r, UiTheme.Hairline.g, UiTheme.Hairline.b, .45f);
            Anchor(rule.rectTransform, new Vector2(.5f, 1), new Vector2(.5f, .5f), new Vector2(0f, -height - 2f),
                new Vector2(width - 36f, soft != null ? 6f : 1f));

            if (modalScroll != null)
            {
                Stretch((RectTransform)modalScroll.transform, 14f, height + 10f, 12f, 14f);
                modalScroll.verticalNormalizedPosition = 1f;
            }
        }

        /// <summary>The diary's caption, so a test can find it the way it finds a named panel.</summary>
        public const string ConfessionalName = "Confessional caption";

        /// <summary>
        /// Mockup-11's caption in the confessional: the speaker's name on a tab and a line under
        /// it, low in the frame between the rail and the diary's column, over the chair. The line
        /// is the player's own most recent memory - what they have to talk about in here - never
        /// words put in their mouth.
        /// </summary>
        public void Confessional(string speaker, string line)
        {
            if (canvas == null || modal == null || string.IsNullOrEmpty(line)) return;
            float s = FontScale;
            var root = (RectTransform)canvas.transform;
            float canvasWidth = root.rect.width > 0 ? root.rect.width : 1600f;
            float left = LeftColumnX, right = canvasWidth - RightColumnInset - modal.sizeDelta.x - 16f;
            float width = Mathf.Min(640f * s, right - left - 32f);
            if (width < 280f) return;

            var bar = Chrome(ConfessionalName, canvas.transform, UiTheme.Emphasis.Interactive);
            bar.GetComponent<Image>().raycastTarget = false;
            var words = NewText(bar, line, 18, Paper);
            words.alignment = TextAlignmentOptions.MidlineLeft;
            float textWidth = width - 44f;
            float lines = Mathf.Ceil(words.GetPreferredValues(words.text, textWidth, 0f).y) + 4f;
            float height = Mathf.Clamp(lines, 26f * s, 84f * s) + 30f * s;
            Anchor(bar, new Vector2(0f, 0f), new Vector2(.5f, 0f), new Vector2((left + right) * .5f, ModalLift + 26f * s), new Vector2(width, height));
            Stretch(words.rectTransform, 22f, 16f * s, 22f, 12f * s);
            AutoSize(words, 13);

            if (string.IsNullOrEmpty(speaker)) return;
            var tab = Panel("Speaker tab", bar, new Color(UiTheme.Ink.r, UiTheme.Ink.g, UiTheme.Ink.b, .96f), 8);
            tab.GetComponent<Image>().raycastTarget = false;
            UiTheme.AddBorder(tab, 8, UiTheme.Hairline);
            var name = NewText(tab, speaker, 15, Paper);
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            if (semibold != null) name.font = semibold;
            name.alignment = TextAlignmentOptions.Center;
            name.textWrappingMode = TextWrappingModes.NoWrap;
            float tabWidth = Mathf.Min(width * .5f, Mathf.Ceil(name.GetPreferredValues(name.text).x) + 32f);
            Anchor(tab, new Vector2(0f, 1f), new Vector2(0f, .5f), new Vector2(18f, 0f), new Vector2(tabWidth, 30f * s));
            Stretch(name.rectTransform, 8f, 2f, 8f, 2f);
        }

        /// <summary>
        /// Close at the size of its words, for a panel narrower than the docked one: at the docked
        /// panel's 174 it took half a 384-wide column's head, and the week line under the phase
        /// band ran beneath it. Returns the width it takes.
        /// </summary>
        private float CompactClose()
        {
            float s = FontScale, width = 118f * s;
            if (!(modal.Find("Close  [Esc]") is RectTransform close)) return width;
            Anchor(close, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-12f, -12f), new Vector2(width, 34f * s));
            var words = close.GetComponentInChildren<TMP_Text>();
            if (words != null)
            {
                words.fontSize = Mathf.RoundToInt(15 * s); words.fontSizeMax = words.fontSize;
                words.fontSizeMin = Mathf.Min(11f, words.fontSize);
                words.alignment = TextAlignmentOptions.Center;
                Stretch(words.rectTransform, 6f, 2f, 6f, 2f);
            }
            return width;
        }

        /// <summary>
        /// A section's name in the diary's column: small, letterspaced and quiet, the way the
        /// mockups label a card's contents. The diary's sections were shouted in the docked panel's
        /// 26-point heading, five of them, in a column 380 wide.
        /// </summary>
        public void DiarySection(string value) => Section(value);

        /// <summary>A section's name in a screen's column; see <see cref="DiarySection"/>.</summary>
        public void Section(string value)
        {
            var text = FlowText(value, 13, UiTheme.Heading);
            text.characterSpacing = 4f;
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            if (semibold != null) text.font = semibold;
        }

        /// <summary>Supporting copy under a section: a rule, a caveat, what a choice costs.</summary>
        public void Aside(string value) => FlowText(value, 15, UiTheme.Muted);

        /// <summary>
        /// One of the diary's options as mockup-11 draws them: a card holding the option's glyph,
        /// its caption as the title, what it means in a line or two under that, and a chevron.
        ///
        /// <para>The card IS the control, named and captioned by the caption, so a test and a
        /// screen reader find it by the words it has always had; the description is a separate
        /// line under them, never appended. <paramref name="note"/> is a last line in its own
        /// colour - how exposed an answer leaves the player - said in words as well as colour.</para>
        ///
        /// <para>Sized to what it holds rather than to a fixed row: a caption that wraps in the
        /// column's width pushes its description down instead of running under it.</para>
        /// </summary>
        public Button OptionCard(string caption, string description, string glyph, Action action,
            string note = null, Color? noteTint = null)
        {
            float s = FontScale;
            var rect = Chrome(caption, content, UiTheme.Emphasis.Interactive);
            HudEmphasis.Promote(rect, UiTheme.Emphasis.Interactive);
            var element = rect.gameObject.AddComponent<LayoutElement>();
            var button = Pressable(rect, action);
            var colours = button.colors;
            colours.highlightedColor = new Color(1.15f, 1.15f, 1.15f);
            colours.selectedColor = colours.highlightedColor;
            button.colors = colours;

            float textX = 60f * s, chevron = 18f * s;
            float textWidth = Mathf.Max(80f, ContentWidth() - textX - chevron - 22f);
            float y = 12f * s;

            var title = NewText(rect, caption, 16, Paper);
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            if (semibold != null) title.font = semibold;
            float titleHeight = Mathf.Ceil(title.GetPreferredValues(title.text, textWidth, 0f).y) + 2f;
            Anchor(title.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(textX, -y), new Vector2(textWidth, titleHeight));
            y += titleHeight + 2f * s;

            if (!string.IsNullOrEmpty(description))
            {
                var line = NewText(rect, description, 14, UiTheme.Muted);
                float lineHeight = Mathf.Ceil(line.GetPreferredValues(line.text, textWidth, 0f).y) + 2f;
                Anchor(line.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(textX, -y), new Vector2(textWidth, lineHeight));
                y += lineHeight + 2f * s;
            }
            if (!string.IsNullOrEmpty(note))
            {
                var word = NewText(rect, note, 13, noteTint ?? Accent);
                float wordHeight = Mathf.Ceil(word.GetPreferredValues(word.text, textWidth, 0f).y) + 2f;
                Anchor(word.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(textX, -y), new Vector2(textWidth, wordHeight));
                y += wordHeight;
            }
            float height = Mathf.Max(68f * s, y + 12f * s);
            element.minHeight = element.preferredHeight = height;
            rect.sizeDelta = new Vector2(rect.sizeDelta.x, height);

            // The glyph, big and pale as the mockup's are, centred on the card's height.
            var mark = HudPrimitives.Glyph("Option mark", rect, glyph, Paper, Vector2.zero, 36f * s);
            if (mark != null)
            {
                var place = mark.rectTransform;
                place.anchorMin = place.anchorMax = new Vector2(0f, .5f); place.pivot = new Vector2(0f, .5f);
                place.anchoredPosition = new Vector2(14f * s, 0f);
            }
            HudPrimitives.Chevron(rect, UiTheme.Hairline, chevron).anchoredPosition = new Vector2(-14f, 0f);
            return button;
        }

        /// <summary>
        /// The diary's record: what the room keeps about the player, one glyph and one sentence a
        /// line, each with its caveat under it in the quieter colour.
        /// </summary>
        public void DiaryRecord(params (string Glyph, string Line, string Detail)[] rows)
        {
            float s = FontScale;
            var card = DecisionColumn(DiaryRecordName, content, true);
            var ground = card.GetComponent<Image>();
            if (ground != null && !UiTheme.PackSliced(ground, PackArt.PanelResting, 12f)) ground.color = UiTheme.Surface;
            var padding = card.GetComponent<VerticalLayoutGroup>();
            padding.padding = new RectOffset(12, 12, 10, 10);
            padding.spacing = 8f * s;
            float textX = 34f * s;
            float textWidth = Mathf.Max(80f, ContentWidth() - 24f - textX);
            foreach (var row in rows)
            {
                var line = new GameObject("Record line", typeof(RectTransform), typeof(LayoutElement)).GetComponent<RectTransform>();
                line.SetParent(card, false);
                var head = NewText(line, row.Line, 15, Paper);
                var medium = UiTheme.Font(UiTheme.Weight.Medium);
                if (medium != null) head.font = medium;
                float headHeight = Mathf.Ceil(head.GetPreferredValues(head.text, textWidth, 0f).y) + 2f;
                Anchor(head.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(textX, 0f), new Vector2(textWidth, headHeight));
                float total = headHeight;
                if (!string.IsNullOrEmpty(row.Detail))
                {
                    var detail = NewText(line, row.Detail, 13, UiTheme.Muted);
                    float detailHeight = Mathf.Ceil(detail.GetPreferredValues(detail.text, textWidth, 0f).y) + 2f;
                    Anchor(detail.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(textX, -headHeight - 1f), new Vector2(textWidth, detailHeight));
                    total += detailHeight + 1f;
                }
                HudPrimitives.Glyph("Record mark", line, row.Glyph, UiTheme.Heading, new Vector2(0f, -1f), 22f * s);
                var size = line.GetComponent<LayoutElement>();
                size.minHeight = size.preferredHeight = total;
            }
        }
    }
}
