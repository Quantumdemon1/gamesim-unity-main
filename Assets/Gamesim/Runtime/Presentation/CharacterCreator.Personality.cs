using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gamesim.Presentation
{
    /// <summary>
    /// The personality step (the creator's first mockup): the seventeen traits as cards at the left,
    /// the eight tendencies with their spare points in the middle, and at the right the summary -
    /// the archetype, the two traits held, and where the houseguest is strongest.
    /// </summary>
    public sealed partial class CharacterCreator
    {
        /// <summary>Each trait's glyph, from the house's own icon set.</summary>
        private static readonly Dictionary<string, string> TraitIcons = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Competitive", "trophy" }, { "Strategic", "target" }, { "Loyal", "handshake" }, { "Emotional", "heart" },
            { "Funny", "mood-amused" }, { "Charming", "mood-charming" }, { "Manipulative", "gossip" }, { "Analytical", "bulb" },
            { "Impulsive", "mood-shocked" }, { "Deceptive", "mood-suspicious" }, { "Social", "people" }, { "Introverted", "journal" },
            { "Stubborn", "mood-angry" }, { "Flexible", "mood-relieved" }, { "Intuitive", "eye" }, { "Sneaky", "ear" },
            { "Confrontational", "mood-tense" },
        };

        private static readonly Dictionary<string, string> StatIcons = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "physical", "dumbbell" }, { "mental", "bulb" }, { "endurance", PackArt.KitIconClock }, { "social", "people" },
            { "luck", "star" }, { "competition", "trophy" }, { "strategic", "target" }, { "loyalty", "handshake" },
        };

        private void BuildPersonalityPage(RectTransform scrim)
        {
            var area = PageArea;
            bool summary = area.width >= 1400f;
            float summaryWidth = summary ? Mathf.Clamp(area.width * .22f, 330f, 420f) : 0f;
            float rest = area.width - (summary ? summaryWidth + Gap : 0f) - Gap;
            float traits = rest * .56f, tendencies = rest - traits;
            BuildTraits(scrim, new Rect(area.x, area.y, traits, area.height));
            BuildTendencies(scrim, new Rect(area.x + traits + Gap, area.y, tendencies, area.height));
            if (summary) BuildSummary(scrim, new Rect(area.x + area.width - summaryWidth, area.y, summaryWidth, area.height));
        }

        /// <summary>
        /// The seventeen traits, two at a time.
        ///
        /// <para>Adding a third is refused rather than silently swapping one out, and the screen says
        /// which two are held. A form that quietly drops a choice the player made a moment ago is
        /// worse than one that tells them they are full.</para>
        /// </summary>
        private void BuildTraits(RectTransform scrim, Rect r)
        {
            var box = Panel("Personality traits", scrim, r, true);
            float x = 24f, width = r.width - 48f;
            float y = SectionHeading(box, x, 20f, width, PackArt.KitIconHeart, "PERSONALITY TRAITS",
                draft.PreserveStats ? null : draft.Traits.Count + "/" + WebTraits.MaximumTraits, draft.PreserveStats ? null : "TRAITS SELECTED");
            if (draft.PreserveStats)
            {
                Words(box, "Existing gameplay build preserved: " + string.Join(" · ", draft.Traits), 18f, UiTheme.Paper, new Rect(x, y + 10f, width, 50f))
                    .textWrappingMode = TextWrappingModes.Normal;
                Words(box, "Appearance and identity edits keep these stats and traits. Rebuild explicitly to allocate a fresh gameplay build.", 15f,
                    UiTheme.Muted, new Rect(x, y + 64f, width, 48f)).textWrappingMode = TextWrappingModes.Normal;
                Pill(box, "Rebuild personality and stats", new Rect(x, y + 124f, Mathf.Min(440f, width), 52f), Tone.Primary,
                    () => { draft = draft.RebuildGameplay(); Rebuild(); }, icon: PackArt.KitIconRefresh);
                return;
            }
            Words(box, "Pick up to two. Each raises two stats: two points on the first, one on the second. Removing it takes the boost back.", 15f,
                UiTheme.Muted, new Rect(x, y, width, 42f)).textWrappingMode = TextWrappingModes.Normal;
            y += 48f;
            string crowded = draft.Crowded;
            var note = Words(box, draft.Traits.Count == 0 ? "No traits chosen. Your stats stay flat."
                    : crowded != null ? "Holding " + string.Join(" and ", draft.Traits) + ". Remove one before adding another."
                    : "Holding " + draft.Traits[0] + ". One more if you want it.",
                14f, crowded != null ? UiTheme.Warning : UiTheme.Heading, new Rect(x, r.height - 36f, width, 22f), name: "Trait status");
            note.textWrappingMode = TextWrappingModes.NoWrap; note.overflowMode = TextOverflowModes.Ellipsis;

            var names = WebTraits.Boosts.Keys.ToList();
            int columns = width >= 820f ? 4 : 3;
            float gap = 12f, card = (width - (columns - 1) * gap) / columns;
            int rows = (names.Count + columns - 1) / columns;
            float available = r.height - y - 48f;
            float height = Mathf.Clamp((available - (rows - 1) * gap) / rows, 76f, 118f);
            var content = ScrollArea(box, "Traits", new Rect(x, y, width, available), rows * (height + gap));
            for (int i = 0; i < names.Count; i++)
            {
                string name = names[i];
                TraitCard(content, name, new Rect((i % columns) * (card + gap), (i / columns) * (height + gap), card, height), crowded != null);
            }
        }

        /// <summary>
        /// One trait: its glyph, its name - the card's caption - and what it does. Held, it is lit and
        /// ticked; with two already held the rest dim but still answer, with the reason.
        /// </summary>
        private void TraitCard(Transform parent, string name, Rect r, bool full)
        {
            bool held = draft.HasTrait(name);
            var card = HudPrimitives.Fill(name, parent, UiTheme.Surface, 12);
            Place(card, r.x, r.y, r.width, r.height);
            var image = card.GetComponent<Image>(); image.raycastTarget = true;
            if (held)
            {
                image.color = new Color(UiTheme.AccentDeep.r, UiTheme.AccentDeep.g, UiTheme.AccentDeep.b, .55f);
                UiTheme.AddGlow(card, 12);
                UiTheme.AddBorder(card, 12, UiTheme.Glow);
            }
            else
            {
                if (!UiTheme.PackSliced(image, PackArt.CreatorThumbnail, 12f)) image.color = UiTheme.Surface;
                UiTheme.AddBorder(card, 12, new Color(UiTheme.Hairline.r, UiTheme.Hairline.g, UiTheme.Hairline.b, .4f));
            }
            var label = Words(card, name, r.height >= 96f ? 19f : 17f, held ? Color.white : UiTheme.Paper,
                new Rect(12f, r.height * .5f - 6f, r.width - 24f, 26f), TextAlignmentOptions.Center, "Label");
            var weight = UiTheme.Font(UiTheme.Weight.SemiBold); if (weight != null) label.font = weight;
            label.textWrappingMode = TextWrappingModes.NoWrap; label.enableAutoSizing = true; label.fontSizeMax = label.fontSize; label.fontSizeMin = 12f;
            float glyph = Mathf.Min(34f, r.height * .32f);
            Glyph(card, TraitIcons.TryGetValue(name, out var icon) ? icon : "star", held ? UiTheme.Glow : UiTheme.Accent,
                new Rect((r.width - glyph) * .5f, r.height * .5f - 10f - glyph, glyph, glyph), "Trait glyph");
            var boost = WebTraits.Boosts[name];
            // What the house makes of it (NPC-AGENCY-PLAN.md §1): a trait the house takes to, or is
            // wary of, says so where it is picked, or the first impressions would feel arbitrary.
            string reception = TraitAffinity.Reception.TryGetValue(name, out int liked) ? (liked > 0 ? "  ·  liked" : "  ·  disliked") : "";
            var effect = Words(card, "+" + WebTraits.PrimaryBoost + " " + Capitalised(boost.Primary) + "  ·  +" + WebTraits.SecondaryBoost + " " + Capitalised(boost.Secondary) + reception,
                12f, held ? UiTheme.Paper : UiTheme.Muted, new Rect(8f, r.height * .5f + 22f, r.width - 16f, 18f), TextAlignmentOptions.Center, "Trait effect");
            effect.textWrappingMode = TextWrappingModes.NoWrap; effect.enableAutoSizing = true; effect.fontSizeMax = 12f; effect.fontSizeMin = 10f;
            if (held)
                Glyph(card, PackArt.KitIconCheck, UiTheme.Glow, new Rect(r.width - 30f, 8f, 22f, 22f), "Held mark");
            if (full && !held)
            {
                // Two held: the rest dim - their fill and glyph, not their words, which stay readable.
                image.color = new Color(image.color.r, image.color.g, image.color.b, image.color.a * .45f);
                var mark = card.Find("Trait glyph")?.GetComponent<Image>();
                if (mark != null) mark.color = new Color(mark.color.r, mark.color.g, mark.color.b, .45f);
            }
            var button = card.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() =>
            {
                if (held) draft.RemoveTrait(name);
                else draft.AddTrait(name);
                Rebuild();
            });
            Seen(button, 12);
        }

        /// <summary>
        /// The eight stats: each a bar out of ten, its number, and the steps either side - the spare
        /// points spent here - with the traits that raise it under the name. The bar is decoration;
        /// the number beside it is the fact.
        /// </summary>
        private void BuildTendencies(RectTransform scrim, Rect r)
        {
            var box = Panel("Tendencies", scrim, r);
            float x = 24f, width = r.width - 48f;
            float y = SectionHeading(box, x, 20f, width, PackArt.KitIconShield, draft.PreserveStats ? "PRESERVED STATS" : "TENDENCIES",
                draft.PreserveStats ? null : draft.Remaining + "/" + CharacterDraft.SparePoints, draft.PreserveStats ? null : "SPARE POINTS");
            Words(box, draft.PreserveStats ? "This houseguest's saved gameplay values. Cosmetic edits leave them unchanged."
                    : "Everyone starts at five. Traits move these too, and nothing goes below one or above ten.", 15f, UiTheme.Muted,
                new Rect(x, y, width, 42f)).textWrappingMode = TextWrappingModes.Normal;
            y += 50f;
            // Eight rows at their own height; a short page scrolls them rather than squeezing them
            // under the footer.
            float available = r.height - y - 16f, row = Mathf.Min(86f, available / WebTraits.StatNames.Length);
            Transform host = box;
            float left = x;
            if (row < 54f)
            {
                row = 54f;
                host = ScrollArea(box, "Tendency rows", new Rect(x, y, width, available), row * WebTraits.StatNames.Length);
                left = 0f; y = 0f;
            }
            foreach (var stat in WebTraits.StatNames)
            {
                StatRow(host, stat, new Rect(left, y, width, row - 8f));
                y += row;
            }
        }

        private void StatRow(Transform parent, string stat, Rect r)
        {
            double value = WebTraits.Get(draft.Stats, stat);
            float glyph = 26f;
            Glyph(parent, StatIcons.TryGetValue(stat, out var icon) ? icon : "star", UiTheme.Accent, new Rect(r.x, r.y + 2f, glyph, glyph), "Stat glyph");
            Words(parent, Capitalised(stat), 18f, UiTheme.Paper, new Rect(r.x + glyph + 12f, r.y, 180f, 26f), name: stat);
            var boosters = WebTraits.Boosts.Where(pair => draft.HasTrait(pair.Key)
                && (string.Equals(pair.Value.Primary, stat, StringComparison.OrdinalIgnoreCase) || string.Equals(pair.Value.Secondary, stat, StringComparison.OrdinalIgnoreCase)))
                .Select(pair => pair.Key).ToList();
            if (boosters.Count > 0 && r.height >= 60f)
                Words(parent, "Boosted by " + string.Join(", ", boosters), 12f, UiTheme.Heading, new Rect(r.x + glyph + 12f, r.y + 28f, 200f, 18f), name: "Stat boost")
                    .overflowMode = TextOverflowModes.Ellipsis;
            float buttons = draft.PreserveStats ? 0f : 40f;
            float barLeft = r.x + glyph + 12f + 190f, barRight = r.x + r.width - 50f - (buttons > 0f ? 2f * buttons + 16f : 0f);
            float barWidth = Mathf.Max(60f, barRight - barLeft);
            // Ten segments, lit up to the value: the mockup's meter, read at a glance.
            float segment = (barWidth - 9f * 4f) / 10f, centre = r.y + 13f;
            for (int i = 0; i < 10; i++)
            {
                bool lit = i < Math.Round(value);
                var cell = HudPrimitives.Fill("Segment", parent, lit ? UiTheme.Glow : new Color(UiTheme.Outline.r, UiTheme.Outline.g, UiTheme.Outline.b, .6f), 3);
                Place(cell, barLeft + i * (segment + 4f), centre - 5f, segment, 10f);
                cell.GetComponent<Image>().raycastTarget = false;
            }
            var number = Words(parent, value.ToString("0"), 22f, Color.white, new Rect(barLeft + barWidth + 8f, r.y - 2f, 42f, 30f), TextAlignmentOptions.Center, "Value");
            var bold = UiTheme.Font(UiTheme.Weight.Bold); if (bold != null) number.font = bold;
            if (draft.PreserveStats) return;
            float bx = r.x + r.width - 2f * buttons - 8f;
            Enable(GlyphButton(parent, LowerCaption(stat), new Rect(bx, centre - buttons * .5f, buttons, buttons), "−", null,
                () => { draft.Lower(stat); Rebuild(); }), draft.CanLower(stat));
            Enable(GlyphButton(parent, RaiseCaption(stat), new Rect(bx + buttons + 8f, centre - buttons * .5f, buttons, buttons), "+", null,
                () => { draft.Raise(stat); Rebuild(); }), draft.CanRaise(stat));
        }

        /// <summary>
        /// The summary card: the archetype the draft carries, the houseguest's picture, the traits
        /// held and the three strongest stats.
        /// </summary>
        private void BuildSummary(RectTransform scrim, Rect r)
        {
            var box = Panel("Personality summary", scrim, r);
            float x = 22f, width = r.width - 44f;
            Words(box, editingCastSlot ? "ARCHETYPE" : "YOUR ARCHETYPE", 14f, UiTheme.Heading, new Rect(x, 22f, width, 20f), TextAlignmentOptions.Center).characterSpacing = 3f;
            var archetype = Words(box, string.IsNullOrWhiteSpace(draft.Archetype) ? "The Newcomer" : draft.Archetype, 30f, UiTheme.Hex("FF7AD9"),
                new Rect(x, 44f, width, 44f), TextAlignmentOptions.Center, "Archetype");
            var bold = UiTheme.Font(UiTheme.Weight.Bold); if (bold != null) archetype.font = bold;
            archetype.enableAutoSizing = true; archetype.fontSizeMax = 30f; archetype.fontSizeMin = 16f;
            float photo = Mathf.Min(width * .75f, r.height * .34f);
            LivePhoto(box, new Rect(x + (width - photo) * .5f, 98f, photo, photo));
            float y = 98f + photo + 14f;
            var name = Words(box, DisplayName, 22f, Color.white,
                new Rect(x, y, width, 30f), TextAlignmentOptions.Center, "Summary name");
            if (bold != null) name.font = bold;
            y += 40f;
            Words(box, "TRAITS", 13f, UiTheme.Heading, new Rect(x, y, width, 18f)).characterSpacing = 2f;
            y += 22f;
            if (draft.Traits.Count > 0) TraitTags(box, new Rect(x, y, width, 32f), TextAlignmentOptions.Left);
            else Words(box, "None yet", 15f, UiTheme.Muted, new Rect(x, y + 6f, width, 22f));
            y += 46f;
            Words(box, "STRONGEST", 13f, UiTheme.Heading, new Rect(x, y, width, 18f)).characterSpacing = 2f;
            y += 24f;
            foreach (var stat in WebTraits.StatNames.OrderByDescending(stat => WebTraits.Get(draft.Stats, stat)).ThenBy(stat => Array.IndexOf(WebTraits.StatNames, stat)).Take(3))
            {
                if (y + 26f > r.height - 12f) break;
                Glyph(box, StatIcons.TryGetValue(stat, out var icon) ? icon : "star", UiTheme.Accent, new Rect(x, y, 22f, 22f));
                Words(box, Capitalised(stat), 16f, UiTheme.Paper, new Rect(x + 32f, y, width - 80f, 24f));
                Words(box, WebTraits.Get(draft.Stats, stat).ToString("0"), 16f, Color.white, new Rect(x + width - 40f, y, 40f, 24f), TextAlignmentOptions.TopRight);
                y += 30f;
            }
        }

        /// <summary>A clipped, scrolling area of the given content height; the content is laid out top-left, y down.</summary>
        private static RectTransform ScrollArea(Transform parent, string name, Rect r, float contentHeight)
        {
            var viewport = HudPrimitives.Fill(name + " viewport", parent, Color.clear, 1);
            Place(viewport, r.x, r.y, r.width, r.height);
            viewport.GetComponent<Image>().raycastTarget = true;
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(viewport, false);
            content.anchorMin = new Vector2(0f, 1f); content.anchorMax = Vector2.one; content.pivot = new Vector2(.5f, 1f);
            content.sizeDelta = new Vector2(0f, contentHeight); content.anchoredPosition = Vector2.zero;
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            viewport.gameObject.AddComponent<SetupScrollFocus>();
            scroll.content = content; scroll.viewport = viewport; scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 40f;
            return content;
        }
    }
}
