using System;
using System.Linq;
using Gamesim.Episode;
using Gamesim.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gamesim.Presentation
{
    /// <summary>
    /// The identity step (the creator's fourth mockup): the form at the left - name, occupation,
    /// hometown, bio, age and pronouns - the houseguest on the platform in the middle, and at the
    /// right the card the house will see.
    /// </summary>
    public sealed partial class CharacterCreator
    {
        private TMP_Text cardSummary, bioCount, cardAge;
        private bool rebuildSoon;

        /// <summary>A field left, with the page to catch up on the next frame - see <see cref="Field"/>.</summary>
        private void RebuildIfAsked()
        {
            if (!rebuildSoon) return;
            rebuildSoon = false;
            if (IsShowing) Rebuild();
        }

        private void BuildIdentityPage(RectTransform scrim)
        {
            var area = PageArea;
            float form = Mathf.Clamp(area.width * .38f, 560f, 760f);
            bool card = area.width - form - 2f * Gap >= 700f;
            float cardWidth = card ? Mathf.Clamp(area.width * .22f, 340f, 420f) : 0f;
            float preview = area.width - form - Gap - (card ? cardWidth + Gap : 0f);
            BuildIdentityForm(scrim, new Rect(area.x, area.y, form, area.height));
            BuildPreviewStage(scrim, new Rect(area.x + form + Gap, area.y, preview, area.height), false);
            if (card) BuildPreviewCard(scrim, new Rect(area.x + area.width - cardWidth, area.y, cardWidth, area.height));
        }

        private void BuildIdentityForm(RectTransform scrim, Rect r)
        {
            var box = Panel("Identity form", scrim, r, true);
            float x = 28f, width = r.width - 56f;
            float top = SectionHeading(box, x, 22f, width, PackArt.KitIconNote, "IDENTITY");
            Words(box, editingCastSlot ? "A member of the cast, as the house will meet them. Applying changes this slot only."
                    : "Who the house meets on the first night. Everything here is yours; the rest of the cast is unchanged.", 15f,
                UiTheme.Muted, new Rect(x, top, width, 44f)).textWrappingMode = TextWrappingModes.Normal;
            top += 52f;
            // The fields at their own sizes; a short page scrolls them rather than squeezing them
            // under the footer.
            const float fixedParts = 84f + 84f + 12f + 88f + 100f, leastBio = 110f;
            float available = r.height - top - 12f;
            Transform host = box;
            float left = x, y = top;
            if (available < fixedParts + leastBio)
            {
                host = ScrollArea(box, "Identity fields", new Rect(x, top, width, available), fixedParts + leastBio);
                left = 0f; y = 0f;
            }
            float air = Mathf.Clamp((available - fixedParts - leastBio) / 6f, 0f, 20f);
            float bioHeight = Mathf.Clamp(available - fixedParts - 4f * air, leastBio, 200f);
            Field(host, "Name", draft.Name, CharacterDraft.NameLimit, false, new Rect(left, y, width, 76f), value => draft.Name = value);
            y += 84f + air;
            float half = (width - 20f) * .5f;
            Field(host, "Occupation", draft.Occupation, CharacterDraft.ShortLimit, false, new Rect(left, y, half, 76f), value => draft.Occupation = value);
            Field(host, "Hometown", draft.Hometown, CharacterDraft.ShortLimit, false, new Rect(left + half + 20f, y, half, 76f), value => draft.Hometown = value);
            y += 84f + air;
            Field(host, "Bio", draft.Bio, CharacterDraft.BioLimit, true, new Rect(left, y, width, bioHeight), value =>
            {
                draft.Bio = value;
                if (bioCount != null) bioCount.text = value.Length + " / " + CharacterDraft.BioLimit;
            });
            bioCount = Words(host, (draft.Bio ?? "").Length + " / " + CharacterDraft.BioLimit, 13f, UiTheme.Muted,
                new Rect(left + width - 160f, y, 160f, 20f), TextAlignmentOptions.TopRight, "Bio count");
            y += bioHeight + 12f + air;
            AgeRow(host, new Rect(left, y, width, 80f));
            y += 88f + air;
            PronounRow(host, new Rect(left, y, width, 78f));
        }

        /// <summary>
        /// A labelled text field: its name above it, the box under it.
        ///
        /// <para>Built on <see cref="EpisodeSpeechInputField"/> rather than a plain
        /// <c>TMP_InputField</c> so Escape closes the screen instead of silently rolling the field
        /// back, and Tab moves on instead of typing a tab — the same two keys the speech field had
        /// to take back, for the same reason.</para>
        ///
        /// <para>The value is written on every keystroke rather than on submit, because the screen
        /// rebuilds itself whenever anything else is touched and an uncommitted field would be lost.
        /// </para>
        /// </summary>
        private void Field(Transform parent, string caption, string value, int limit, bool multiline, Rect r, Action<string> write)
        {
            var label = Words(parent, caption.ToUpperInvariant(), 14f, UiTheme.Heading, new Rect(r.x, r.y, r.width, 20f), name: caption);
            label.characterSpacing = 2f;
            var box = HudPrimitives.Fill(caption + " field", parent, new Color(UiTheme.Ink.r, UiTheme.Ink.g, UiTheme.Ink.b, .55f), 10);
            Place(box, r.x, r.y + 26f, r.width, r.height - 26f);
            UiTheme.AddBorder(box, 10, UiTheme.Edge(UiTheme.Emphasis.Interactive));
            box.GetComponent<Image>().raycastTarget = true;
            var text = HudPrimitives.Label("Text", box, 18f, UiTheme.Paper, multiline ? TextAlignmentOptions.TopLeft : TextAlignmentOptions.MidlineLeft);
            text.text = string.Empty;
            Inset(text.rectTransform, 16f, multiline ? 10f : 4f);
            var hint = HudPrimitives.Label("Placeholder", box, 18f, UiTheme.Muted, multiline ? TextAlignmentOptions.TopLeft : TextAlignmentOptions.MidlineLeft);
            hint.text = Localisation.Text(multiline ? "A few lines the house will learn about you" : caption);
            hint.fontStyle = FontStyles.Italic;
            Inset(hint.rectTransform, 16f, multiline ? 10f : 4f);
            var input = box.gameObject.AddComponent<EpisodeSpeechInputField>();
            input.textViewport = box;
            input.textComponent = text;
            input.placeholder = hint;
            input.characterLimit = limit;
            input.lineType = multiline ? TMP_InputField.LineType.MultiLineNewline : TMP_InputField.LineType.SingleLine;
            input.text = value ?? string.Empty;
            string lastPresented = input.text;
            input.onValueChanged.AddListener(written => write(written));
            // The preview only catches up when the field is left, and on the next frame. Rebuilding
            // on every keystroke would destroy the field being typed into; rebuilding inside the end
            // of the edit destroyed the control whose press ended it, mid-press, so the first click
            // after typing did nothing. And only while the field is alive: a focused field raises
            // onEndEdit from its own OnDisable, which is also what a scene unload or a rebuild does.
            input.onEndEdit.AddListener(written =>
            {
                // Merely walking the keyboard ring must not replace every control. Only a real
                // edit changes the summary or validation that the deferred rebuild refreshes.
                if (input.isActiveAndEnabled && isActiveAndEnabled && written != lastPresented)
                {
                    lastPresented = written;
                    rebuildSoon = true;
                }
            });
        }

        /// <summary>
        /// Age as the mockup draws it: the number, a slider across the legal range, and a step either
        /// side. A stepper rather than free text: a form that lets you type an illegal age only to
        /// refuse it later is a worse form than one that does not offer it.
        /// </summary>
        private void AgeRow(Transform parent, Rect r)
        {
            Words(parent, "AGE", 14f, UiTheme.Heading, new Rect(r.x, r.y, 120f, 20f), name: "Age").characterSpacing = 2f;
            var number = Words(parent, draft.Age.ToString(), 30f, Color.white, new Rect(r.x, r.y + 26f, 70f, 48f),
                TextAlignmentOptions.MidlineLeft, "Age value");
            var bold = UiTheme.Font(UiTheme.Weight.Bold); if (bold != null) number.font = bold;
            float left = r.x + 80f, arrow = 44f, y = r.y + 28f;
            var younger = GlyphButton(parent, "Younger", new Rect(left, y, arrow, arrow), "−", null, () =>
            {
                draft.Age = Mathf.Max(CharacterDraft.MinimumAge, draft.Age - 1);
                Rebuild();
            });
            var older = GlyphButton(parent, "Older", new Rect(r.x + r.width - arrow, y, arrow, arrow), "+", null, () =>
            {
                draft.Age = Mathf.Min(CharacterDraft.MaximumAge, draft.Age + 1);
                Rebuild();
            });
            Enable(younger, draft.Age > CharacterDraft.MinimumAge);
            Enable(older, draft.Age < CharacterDraft.MaximumAge);
            var slider = PackSlider(parent, "Age slider", new Rect(left + arrow + 16f, y + 9f, r.width - 80f - 2f * arrow - 32f, 26f),
                CharacterDraft.MinimumAge, CharacterDraft.MaximumAge, draft.Age);
            slider.wholeNumbers = true;
            var track = (RectTransform)slider.transform;
            Words(parent, CharacterDraft.MinimumAge.ToString(), 13f, UiTheme.Muted,
                new Rect(track.anchoredPosition.x, -track.anchoredPosition.y + 26f, 40f, 18f), name: "Age minimum");
            Words(parent, CharacterDraft.MaximumAge.ToString(), 13f, UiTheme.Muted,
                new Rect(track.anchoredPosition.x + track.sizeDelta.x - 40f, -track.anchoredPosition.y + 26f, 40f, 18f), TextAlignmentOptions.TopRight, "Age maximum");
            slider.onValueChanged.AddListener(next =>
            {
                draft.Age = Mathf.RoundToInt(next);
                number.text = draft.Age.ToString();
                if (cardSummary != null) cardSummary.text = Summary();
                if (cardAge != null) cardAge.text = draft.Age.ToString();
                Enable(younger, draft.Age > CharacterDraft.MinimumAge);
                Enable(older, draft.Age < CharacterDraft.MaximumAge);
            });
        }

        private void PronounRow(Transform parent, Rect r)
        {
            Words(parent, "PRONOUNS", 14f, UiTheme.Heading, new Rect(r.x, r.y, r.width, 20f), name: "Pronouns").characterSpacing = 2f;
            int count = CharacterDraft.PronounOptions.Length;
            float width = (r.width - (count - 1) * 12f) / count;
            for (int i = 0; i < count; i++)
            {
                string pick = CharacterDraft.PronounOptions[i];
                bool on = string.Equals(draft.Pronouns, pick, StringComparison.Ordinal);
                Pill(parent, pick, new Rect(r.x + i * (width + 12f), r.y + 28f, width, 48f), on ? Tone.Selected : Tone.Secondary,
                    () => { draft.Pronouns = pick; Rebuild(); }, icon: on ? PackArt.KitIconCheck : null, size: 17f);
            }
            Words(parent, "Pronouns are independent of appearance.", 13f, UiTheme.Muted, new Rect(r.x, r.y + 80f, r.width, 20f));
        }

        /// <summary>
        /// The card the house will see: the houseguest's picture, cropped from the live preview so it
        /// is what is on the platform right now, then the name and the facts under it.
        /// </summary>
        private void BuildPreviewCard(RectTransform scrim, Rect r)
        {
            var box = Panel("Houseguest preview", scrim, r);
            float x = 22f, width = r.width - 44f;
            var heading = Words(box, "HOUSEGUEST PREVIEW", 16f, UiTheme.Glow, new Rect(x, 20f, width, 24f));
            heading.characterSpacing = 2f;
            var bold = UiTheme.Font(UiTheme.Weight.Bold); if (bold != null) heading.font = bold;
            // The facts keep their place at the foot; the photo takes what the name, the summary and a
            // line of bio leave it, so on a short page they never meet.
            float facts = 4f * 34f + 20f;
            float photo = Mathf.Clamp(r.height - 54f - 16f - 40f - 32f - 40f - facts - 16f, 80f, Mathf.Min(width, r.height * .5f));
            LivePhoto(box, new Rect(x + (width - photo) * .5f, 54f, photo, photo));
            float y = 54f + photo + 16f;
            var name = Words(box, DisplayName, 28f, Color.white, new Rect(x, y, width, 38f), TextAlignmentOptions.Center, "Card name");
            if (bold != null) name.font = bold;
            name.enableAutoSizing = true; name.fontSizeMax = 28f; name.fontSizeMin = 16f;
            y += 40f;
            cardSummary = Words(box, Summary(), 15f, UiTheme.Muted, new Rect(x, y, width, 22f), TextAlignmentOptions.Center, "Card summary");
            cardSummary.textWrappingMode = TextWrappingModes.NoWrap; cardSummary.overflowMode = TextOverflowModes.Ellipsis;
            cardSummary.enableAutoSizing = true; cardSummary.fontSizeMax = 15f; cardSummary.fontSizeMin = 11f;
            y += 32f;
            // The bio in the houseguest's own voice, then the facts, each on its own line.
            float bioHeight = Mathf.Max(0f, r.height - y - facts - 16f);
            if (bioHeight > 30f)
            {
                bool none = string.IsNullOrWhiteSpace(draft.Bio);
                var bio = Words(box, none ? "No bio yet." : "\u201C" + draft.Bio.Trim() + "\u201D", 15f, none ? UiTheme.Muted : UiTheme.Paper,
                    new Rect(x, y, width, bioHeight), none ? TextAlignmentOptions.Top : TextAlignmentOptions.TopLeft, "Card bio");
                bio.fontStyle = FontStyles.Italic;
                bio.textWrappingMode = TextWrappingModes.Normal; bio.overflowMode = TextOverflowModes.Ellipsis;
            }
            y = r.height - facts;
            var rule = HudPrimitives.Fill("Card rule", box, new Color(UiTheme.Hairline.r, UiTheme.Hairline.g, UiTheme.Hairline.b, .5f), 1);
            Place(rule, x, y, width, 1f);
            y += 14f;
            var rows = new (string icon, string label, string value)[]
            {
                (PackArt.KitIconCalendar, "Age", draft.Age.ToString()),
                (PackArt.KitIconLocation, "Hometown", string.IsNullOrWhiteSpace(draft.Hometown) ? "\u2014" : draft.Hometown.Trim()),
                (PackArt.KitIconArchive, "Occupation", string.IsNullOrWhiteSpace(draft.Occupation) ? "\u2014" : draft.Occupation.Trim()),
                (PackArt.KitIconPerson, "Pronouns", draft.Pronouns),
            };
            foreach (var (icon, label, value) in rows)
            {
                Glyph(box, icon, UiTheme.Accent, new Rect(x, y + 3f, 20f, 20f), "Card fact glyph");
                Words(box, label, 15f, UiTheme.Muted, new Rect(x + 32f, y, 120f, 26f), name: "Card fact");
                var shown = Words(box, value, 15f, Color.white, new Rect(x + 150f, y, width - 150f, 26f), name: "Card fact value");
                shown.overflowMode = TextOverflowModes.Ellipsis;
                if (label == "Age") cardAge = shown;
                y += 34f;
            }
        }

        /// <summary>The traits as tags in a row, centred.</summary>
        private void TraitTags(Transform parent, Rect r, TextAlignmentOptions align = TextAlignmentOptions.Center)
        {
            float tag = Mathf.Min(170f, (r.width - (draft.Traits.Count - 1) * 10f) / Math.Max(1, draft.Traits.Count));
            float total = draft.Traits.Count * tag + (draft.Traits.Count - 1) * 10f;
            float x = align == TextAlignmentOptions.Center ? r.x + (r.width - total) * .5f : r.x;
            foreach (var trait in draft.Traits)
            {
                var pill = HudPrimitives.Fill("Trait tag", parent, new Color(UiTheme.AccentDeep.r, UiTheme.AccentDeep.g, UiTheme.AccentDeep.b, .45f), 14);
                Place(pill, x, r.y, tag, r.height);
                UiTheme.AddBorder(pill, 14, new Color(UiTheme.Glow.r, UiTheme.Glow.g, UiTheme.Glow.b, .7f));
                var label = Words(pill, trait, 14f, Color.white, new Rect(0f, 0f, tag, r.height), TextAlignmentOptions.Center, "Trait tag label");
                label.textWrappingMode = TextWrappingModes.NoWrap;
                x += tag + 10f;
            }
        }

        /// <summary>
        /// The houseguest as the studio is drawing them this moment, cropped to the head and shoulders.
        /// Not a portrait request: the studio already renders this look, and a second render of it
        /// would be a second body being assembled for a picture of the first.
        /// </summary>
        private void LivePhoto(Transform parent, Rect r)
        {
            var frame = HudPrimitives.Fill("Live photo", parent, new Color(UiTheme.Ink.r, UiTheme.Ink.g, UiTheme.Ink.b, .7f), 12);
            Place(frame, r.x, r.y, r.width, r.height);
            frame.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            UiTheme.AddBorder(frame, 12, new Color(UiTheme.Glow.r, UiTheme.Glow.g, UiTheme.Glow.b, .6f));
            // The look on the draft now: a houseguest loaded from the library since the studio last
            // drew is not the one it drew.
            EnsureStudio();
            if (studioPreview.Texture == null) { Glyph(frame, "houseguest", new Color(1f, 1f, 1f, .3f), new Rect(r.width * .2f, r.height * .2f, r.width * .6f, r.height * .6f)); return; }
            var raw = new GameObject("Face", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            raw.rectTransform.SetParent(frame, false);
            raw.texture = studioPreview.Texture; raw.raycastTarget = false;
            raw.rectTransform.anchorMin = Vector2.zero; raw.rectTransform.anchorMax = Vector2.one;
            raw.rectTransform.offsetMin = raw.rectTransform.offsetMax = Vector2.zero;
            // The studio frames the whole body in a 4:5 picture: its head and shoulders, at the
            // frame's shape.
            float textureAspect = studioPreview.Texture.width / (float)Mathf.Max(1, studioPreview.Texture.height);
            float height = .38f, width = Mathf.Clamp01(height * (r.width / Mathf.Max(1f, r.height)) / Mathf.Max(.01f, textureAspect));
            raw.uvRect = new Rect((1f - width) * .5f, 1f - height, width, height);
        }

        private static void Inset(RectTransform rect, float x, float y)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(x, y);
            rect.offsetMax = new Vector2(-x, -y);
        }

        /// <summary>A slider on the pack's art: the thin track, the fill up to the value, the lit handle.</summary>
        private static Slider PackSlider(Transform parent, string name, Rect r, float minimum, float maximum, float value)
        {
            var track = HudPrimitives.Fill(name, parent, new Color(0f, 0f, 0f, 0f), 6);
            Place(track, r.x, r.y, r.width, r.height);
            // A thin line, lit up to the value. The pack's track and fill are drawn for a thick bar;
            // sliced this thin they are all border and read as nothing.
            var line = HudPrimitives.Fill("Line", track, new Color(UiTheme.Outline.r, UiTheme.Outline.g, UiTheme.Outline.b, .95f), 3);
            line.anchorMin = new Vector2(0f, .5f); line.anchorMax = new Vector2(1f, .5f);
            line.pivot = new Vector2(.5f, .5f); line.sizeDelta = new Vector2(0f, 6f); line.anchoredPosition = Vector2.zero;
            var fillArea = SliderArea("Fill area", track, 6f);
            var fill = HudPrimitives.Fill("Fill", fillArea, UiTheme.Glow, 3);
            fill.pivot = new Vector2(0f, .5f); fill.sizeDelta = Vector2.zero; fill.anchoredPosition = Vector2.zero;
            var handleArea = SliderArea("Handle area", track, r.height);
            var handle = HudPrimitives.Disc("Handle", handleArea, UiTheme.Paper);
            handle.pivot = new Vector2(.5f, .5f); handle.sizeDelta = new Vector2(r.height, 0f); handle.anchoredPosition = Vector2.zero;
            var handleArt = UiTheme.Pack(PackArt.CreatorSliderHandle);
            if (handleArt != null) { var art = handle.GetComponent<Image>(); art.sprite = handleArt; art.color = Color.white; art.type = Image.Type.Simple; }
            track.GetComponent<Image>().raycastTarget = true;
            handle.GetComponent<Image>().raycastTarget = true;
            line.GetComponent<Image>().raycastTarget = false;
            fill.GetComponent<Image>().raycastTarget = false;
            var slider = track.gameObject.AddComponent<Slider>();
            slider.targetGraphic = handle.GetComponent<Image>(); slider.handleRect = handle; slider.fillRect = fill;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = minimum; slider.maxValue = maximum; slider.value = value;
            return slider;
        }
    }
}
