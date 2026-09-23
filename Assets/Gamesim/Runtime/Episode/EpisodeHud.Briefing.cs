using System;
using System.Collections.Generic;
using Gamesim.Presentation;
using Gamesim.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gamesim.Episode
{
    /// <summary>
    /// The competition's briefing as the style guide's modal draws it: a hero card with the
    /// player's face, the competition's category, its title and what is at stake; the rules in a
    /// short paragraph; four facts in a row; one primary action beside a secondary one that opens
    /// the full rules; and the ways to compete for real under them. The long paragraphs the
    /// briefing used to be are still there, behind "View full rules" - nothing is dropped, it is
    /// ordered: what you need first, the fine print when you ask for it.
    /// </summary>
    public sealed partial class EpisodeHud
    {
        /// <summary>Names the briefing's parts carry, so a test can find them the way it finds a named panel.</summary>
        public const string BriefingHeroName = "Competition hero";
        public const string BriefingFactsName = "Competition facts";
        public const string BriefingActionsName = "Competition actions";
        public const string FullRulesName = "Competition full rules";
        /// <summary>The words on the control that opens and closes the full rules. Captions are a contract.</summary>
        public const string ViewRulesCaption = "View full rules";
        public const string HideRulesCaption = "Hide full rules";

        /// <summary>
        /// The briefing's sheet width at the resting text size: the style guide's modal grown to a
        /// full-height sheet, never more than 58% of the frame so the arena keeps the rest.
        /// </summary>
        private const float BriefingSheetWidth = 900f;

        /// <summary>
        /// The hero card: the player's face down the left, fading into the card, and beside it the
        /// competition's category on a chip, its title large, and what winning it means.
        /// </summary>
        public void CompetitionHero(ContestantState player, string category, string title, string stakes)
        {
            if (content == null) return;
            float s = FontScale;
            float width = ContentWidth();
            var card = Panel(BriefingHeroName, content, UiTheme.CardFill, UiTheme.GlassRadius);
            card.GetComponent<Image>().raycastTarget = false;
            UiTheme.AddBorder(card, UiTheme.GlassRadius, new Color(UiTheme.Outline.r, UiTheme.Outline.g, UiTheme.Outline.b, .8f));
            var element = card.gameObject.AddComponent<LayoutElement>();

            float photoWidth = Mathf.Round(width * .3f);
            float textX = photoWidth + 18f * s;
            float textWidth = Mathf.Max(120f, width - textX - 16f * s);
            float y = 16f * s;

            // The category on an outlined chip, in caps, as the mockup's "MENTAL" is.
            if (!string.IsNullOrEmpty(category))
            {
                var chip = Panel("Category chip", card, new Color(UiTheme.Glow.r, UiTheme.Glow.g, UiTheme.Glow.b, .10f), 6);
                chip.GetComponent<Image>().raycastTarget = false;
                UiTheme.AddBorder(chip, 6, new Color(UiTheme.Glow.r, UiTheme.Glow.g, UiTheme.Glow.b, .85f));
                var word = NewText(chip, category.ToUpperInvariant(), 12, UiTheme.Glow);
                var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
                if (semibold != null) word.font = semibold;
                word.characterSpacing = 4f;
                word.alignment = TextAlignmentOptions.Center;
                word.textWrappingMode = TextWrappingModes.NoWrap;
                float chipWidth = Mathf.Min(textWidth, Mathf.Ceil(word.GetPreferredValues(word.text).x) + 22f * s);
                Anchor(chip, new Vector2(0, 1), new Vector2(0, 1), new Vector2(textX, -y), new Vector2(chipWidth, 24f * s));
                Stretch(word.rectTransform, 6f, 1f, 6f, 1f);
                y += 34f * s;
            }

            var heading = NewText(card, title, 26, Paper);
            var bold = UiTheme.Font(UiTheme.Weight.Bold);
            if (bold != null) heading.font = bold;
            heading.textWrappingMode = TextWrappingModes.NoWrap;
            Anchor(heading.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(textX, -y), new Vector2(textWidth, 36f * s));
            AutoSize(heading, 18);
            y += 40f * s;

            if (!string.IsNullOrEmpty(stakes))
            {
                var line = NewText(card, stakes, 15, new Color(Paper.r, Paper.g, Paper.b, .88f));
                float lineHeight = Mathf.Ceil(line.GetPreferredValues(line.text, textWidth, 0f).y) + 2f;
                Anchor(line.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(textX, -y), new Vector2(textWidth, lineHeight));
                y += lineHeight;
            }
            float height = Mathf.Max(150f * s, y + 16f * s);
            element.minHeight = element.preferredHeight = height;
            card.sizeDelta = new Vector2(card.sizeDelta.x, height);

            // The face: a photo down the left of the card, bleeding to its rounded edge, as the
            // cast cards draw theirs. Bound rather than read once: a body's portrait lands a few
            // frames after the panel is built.
            var photo = Panel("Hero photo", card, UiTheme.SurfaceRaised, UiTheme.GlassRadius);
            photo.GetComponent<Image>().raycastTarget = false;
            photo.anchorMin = new Vector2(0f, 0f); photo.anchorMax = new Vector2(0f, 1f);
            photo.pivot = new Vector2(0f, .5f);
            photo.offsetMin = new Vector2(1f, 1f); photo.offsetMax = new Vector2(photoWidth, -1f);
            photo.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            var silhouette = UiTheme.Icon("houseguest");
            if (silhouette != null)
            {
                var art = new GameObject("Silhouette", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                art.rectTransform.SetParent(photo, false);
                art.rectTransform.anchorMin = art.rectTransform.anchorMax = new Vector2(.5f, 0f);
                art.rectTransform.pivot = new Vector2(.5f, 0f);
                art.rectTransform.sizeDelta = new Vector2(height * .8f, height * .8f);
                art.sprite = silhouette; art.color = new Color(Paper.r, Paper.g, Paper.b, .18f);
                art.preserveAspect = true; art.raycastTarget = false;
            }
            if (player != null)
            {
                var face = new GameObject("Hero face", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
                face.rectTransform.SetParent(photo, false);
                face.rectTransform.anchorMin = Vector2.zero; face.rectTransform.anchorMax = Vector2.one;
                face.rectTransform.offsetMin = Vector2.zero; face.rectTransform.offsetMax = Vector2.zero;
                face.raycastTarget = false;
                // The render is a square head-and-shoulders; the photo is a column, so take the
                // column out of its middle rather than squash a face.
                // Closer than the cast cards': the face fills the photo, the shoulders go.
                float share = photoWidth / height, zoom = .78f;
                float w = (share <= 1f ? share : 1f) * zoom, h = (share <= 1f ? 1f : 1f / share) * zoom;
                face.uvRect = new Rect((1f - w) * .5f, (1f - h) * .85f, w, h);
                CharacterPortraits.Bind(face, player);
            }
            var fade = new GameObject("Photo fade", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            fade.rectTransform.SetParent(card, false);
            fade.rectTransform.anchorMin = new Vector2(0f, 0f); fade.rectTransform.anchorMax = new Vector2(0f, 1f);
            fade.rectTransform.pivot = new Vector2(0f, .5f);
            // Only the photo's last quarter fades: wider, it dimmed a face turned toward the card.
            fade.rectTransform.offsetMin = new Vector2(photoWidth * .75f, 1f); fade.rectTransform.offsetMax = new Vector2(photoWidth + 1f, -1f);
            fade.sprite = UiTheme.FadeRight(); fade.color = UiTheme.CardFill; fade.raycastTarget = false;
        }

        /// <summary>How the competition is played, in a short paragraph at a reading measure.</summary>
        public void CompetitionBrief(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            FlowText(text, 15, new Color(Paper.r, Paper.g, Paper.b, .92f));
        }

        /// <summary>One of the briefing's facts: its mark, a short head, and a line under it.</summary>
        public struct BriefingFact
        {
            public Sprite Mark;
            /// <summary>Draws the pause mark instead of a sprite: no icon set carries one.</summary>
            public bool Pause;
            public string Head;
            public string Detail;
            public BriefingFact(Sprite mark, string head, string detail) { Mark = mark; Pause = false; Head = head; Detail = detail; }
            public static BriefingFact PauseMark(string head, string detail) => new BriefingFact(null, head, detail) { Pause = true };
        }

        /// <summary>The facts in a row, as the mockup sets its four under the rules.</summary>
        public void CompetitionFacts(IList<BriefingFact> facts)
        {
            if (content == null || facts == null || facts.Count == 0) return;
            float s = FontScale;
            var row = new GameObject(BriefingFactsName, typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement)).GetComponent<RectTransform>();
            row.SetParent(content, false);
            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 10f * s; layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true; layout.childForceExpandHeight = true;
            float height = 48f * s;
            var size = row.GetComponent<LayoutElement>();
            size.minHeight = size.preferredHeight = height;
            float cell = (ContentWidth() - layout.spacing * (facts.Count - 1)) / facts.Count;
            float mark = 20f * s, textX = mark + 7f * s, textWidth = Mathf.Max(40f, cell - textX);
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            foreach (var fact in facts)
            {
                var item = new GameObject("Fact", typeof(RectTransform), typeof(LayoutElement)).GetComponent<RectTransform>();
                item.SetParent(row, false);
                item.GetComponent<LayoutElement>().flexibleWidth = 1f;
                if (fact.Pause) PauseMark(item, mark);
                else if (fact.Mark != null)
                {
                    var icon = new GameObject("Fact mark", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                    icon.rectTransform.SetParent(item, false);
                    Anchor(icon.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0f, -2f * s), new Vector2(mark, mark));
                    icon.sprite = fact.Mark; icon.color = UiTheme.Heading; icon.preserveAspect = true; icon.raycastTarget = false;
                }
                var head = NewText(item, fact.Head, 12, Paper);
                if (semibold != null) head.font = semibold;
                head.textWrappingMode = TextWrappingModes.NoWrap;
                Anchor(head.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(textX, 0f), new Vector2(textWidth, 20f * s));
                AutoSize(head, 10);
                var detail = NewText(item, fact.Detail, 11, UiTheme.Muted);
                Anchor(detail.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(textX, -20f * s), new Vector2(textWidth, 28f * s));
                AutoSize(detail, 9);
            }
        }

        /// <summary>Two bars, the pause mark, in the facts' blue.</summary>
        private void PauseMark(RectTransform parent, float side)
        {
            var mark = new GameObject("Fact mark", typeof(RectTransform)).GetComponent<RectTransform>();
            mark.SetParent(parent, false);
            Anchor(mark, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0f, -2f * FontScale), new Vector2(side, side));
            var ring = HudPrimitives.Disc("Pause ring", mark, UiTheme.Heading);
            ring.anchorMin = Vector2.zero; ring.anchorMax = Vector2.one; ring.offsetMin = Vector2.zero; ring.offsetMax = Vector2.zero;
            var hole = HudPrimitives.Disc("Pause hole", ring, UiTheme.GlassFill);
            hole.anchorMin = Vector2.zero; hole.anchorMax = Vector2.one;
            hole.offsetMin = new Vector2(2f, 2f); hole.offsetMax = new Vector2(-2f, -2f);
            for (int i = 0; i < 2; i++)
            {
                var bar = Panel("Pause bar", mark, UiTheme.Heading, 1);
                bar.GetComponent<Image>().raycastTarget = false;
                Anchor(bar, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2((i == 0 ? -1f : 1f) * side * .14f, 0f),
                    new Vector2(Mathf.Max(2f, side * .13f), side * .42f));
            }
        }

        /// <summary>
        /// The briefing's two actions side by side: the one primary action the modal has, in the
        /// action blue, and a quiet secondary that opens the full rules in a card under them.
        /// Returns the primary. The full rules are built closed; the secondary opens them in place
        /// and says so in its own words, "Hide full rules".
        /// </summary>
        public Button CompetitionActions(string primaryCaption, Action primary, IList<string> fullRules)
        {
            if (content == null) return null;
            float s = FontScale;
            var row = new GameObject(BriefingActionsName, typeof(RectTransform), typeof(LayoutElement)).GetComponent<RectTransform>();
            row.SetParent(content, false);
            float height = 52f * s;
            var size = row.GetComponent<LayoutElement>();
            size.minHeight = size.preferredHeight = height;
            float width = ContentWidth(), gap = 12f * s;
            float primaryWidth = Mathf.Round((width - gap) * .6f);

            var go = BriefingButton(row, primaryCaption, "continue", true, primary);
            Anchor((RectTransform)go.transform, new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(primaryWidth, height));

            RectTransform rules = null;
            Button toggle = null;
            toggle = BriefingButton(row, ViewRulesCaption, "journal", false, () =>
            {
                if (rules == null) return;
                bool open = !rules.gameObject.activeSelf;
                rules.gameObject.SetActive(open);
                string words = open ? HideRulesCaption : ViewRulesCaption;
                toggle.name = words;
                var label = toggle.GetComponentInChildren<TMP_Text>();
                if (label != null) label.text = words;
                if (content != null) LayoutRebuilder.MarkLayoutForRebuild(content);
            });
            Anchor((RectTransform)toggle.transform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(primaryWidth + gap, 0f),
                new Vector2(width - primaryWidth - gap, height));

            if (fullRules != null && fullRules.Count > 0)
            {
                rules = DecisionColumn(FullRulesName, content, true);
                var padding = rules.GetComponent<VerticalLayoutGroup>();
                padding.padding = new RectOffset(14, 14, 12, 12);
                padding.spacing = 8f * s;
                foreach (var paragraph in fullRules)
                {
                    if (string.IsNullOrEmpty(paragraph)) continue;
                    var line = NewText(rules, paragraph, 14, new Color(Paper.r, Paper.g, Paper.b, .82f));
                    line.gameObject.AddComponent<LayoutElement>().minHeight = 14f * s + 6f;
                }
                rules.gameObject.SetActive(false);
            }
            else toggle.interactable = false;
            return go;
        }

        /// <summary>
        /// A briefing button: the primary in the action blue with white words and its glow, the
        /// secondary on the glass with a hairline - its mark on the left, its caption in caps
        /// (rendered so: the caption itself keeps the words a test and a screen reader know it by),
        /// and a chevron on the primary saying it leads on.
        /// </summary>
        private Button BriefingButton(RectTransform parent, string caption, string packIcon, bool primary, Action action)
        {
            float s = FontScale;
            var rect = Panel(caption, parent, primary ? UiTheme.ActionBlue : UiTheme.GlassFill, UiTheme.ControlRadius);
            if (primary)
            {
                UiTheme.AddGlow(rect, UiTheme.ControlRadius);
                UiTheme.AddBorder(rect, UiTheme.ControlRadius, new Color(Paper.r, Paper.g, Paper.b, .35f));
            }
            else UiTheme.AddBorder(rect, UiTheme.ControlRadius, new Color(UiTheme.Hairline.r, UiTheme.Hairline.g, UiTheme.Hairline.b, .7f));
            HudEmphasis.Promote(rect, UiTheme.Emphasis.Interactive);
            var button = Pressable(rect, action);
            var colours = button.colors;
            colours.highlightedColor = new Color(1.15f, 1.15f, 1.15f);
            colours.selectedColor = colours.highlightedColor;
            button.colors = colours;

            float x = 16f * s, side = (packIcon == "continue" ? 16f : 20f) * s;
            var art = packIcon == "continue" ? UiTheme.PlayMark() : UiTheme.Icon(packIcon);
            if (art != null)
            {
                var mark = new GameObject("Button mark", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                mark.rectTransform.SetParent(rect, false);
                Anchor(mark.rectTransform, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(x, 0f), new Vector2(side, side));
                mark.sprite = art; mark.color = primary ? Color.white : Paper; mark.preserveAspect = true; mark.raycastTarget = false;
                x += side + 12f * s;
            }
            float chevron = primary ? 16f * s : 0f;
            var label = NewText(rect, caption, 15, primary ? Color.white : Paper);
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            if (semibold != null) label.font = semibold;
            label.fontStyle = FontStyles.UpperCase;
            label.characterSpacing = 2f;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            Stretch(label.rectTransform, x, 2f, 14f + chevron + (primary ? 8f : 0f), 2f);
            AutoSize(label, 11);
            if (primary) HudPrimitives.Chevron(rect, Color.white, chevron).anchoredPosition = new Vector2(-14f, 0f);
            return button;
        }
    }
}
