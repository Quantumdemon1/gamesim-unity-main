using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gamesim.Presentation
{
    /// <summary>A picture that shows whatever another picture shows - a face bound elsewhere, once it lands.</summary>
    internal sealed class PortraitMirror : MonoBehaviour
    {
        public RawImage Source;
        private RawImage own;

        private void LateUpdate()
        {
            if (own == null) own = GetComponent<RawImage>();
            var texture = Source != null && Source.enabled ? Source.texture : null;
            if (own.texture != texture) { own.texture = texture; own.uvRect = Source != null ? Source.uvRect : new Rect(0f, 0f, 1f, 1f); }
            own.enabled = texture != null;
        }
    }

    /// <summary>
    /// My Houseguests (the creator's second mockup) - the saved houseguests as cards, a new one at
    /// the head of them, and a bar under them for the one in focus - and the Review step that
    /// closes the creator.
    /// </summary>
    public sealed partial class CharacterCreator
    {
        private string focusedProfile;
        private bool confirmingNew;

        private void BuildLibraryPage(RectTransform scrim)
        {
            var area = PageArea;
            var box = Panel("My Houseguests", scrim, area, true);
            float x = 26f, width = area.width - 52f;
            var store = ProfileStore;
            var profiles = store.List();
            float saveWidth = Mathf.Min(260f, width * .2f);
            SectionHeading(box, x, 20f, width - 2f * saveWidth - 24f, PackArt.KitIconPeople, "MY HOUSEGUESTS");
            Words(box, profiles.Count + (profiles.Count == 1 ? " saved houseguest" : " saved houseguests") + "  ·  Saved profiles can be reused, renamed, duplicated or deleted. Existing seasons keep their own independent copy.",
                14f, UiTheme.Muted, new Rect(x, 60f, width - 2f * saveWidth - 24f, 40f)).textWrappingMode = TextWrappingModes.Normal;
            Pill(box, "Save this houseguest", new Rect(area.width - 26f - 2f * saveWidth - 12f, 22f, saveWidth, 52f), Tone.Primary,
                () => SaveProfile(false), icon: PackArt.KitIconSave);
            Pill(box, "Save a new copy", new Rect(area.width - 26f - saveWidth, 22f, saveWidth, 52f), Tone.Secondary,
                () => SaveProfile(true), icon: PackArt.KitIconSave);

            // Everything under the heading scrolls: the search row the cast screen shares - fixed
            // pieces around its own centre - any messages, and the cards.
            var focusId = focusedProfile ?? profileId;
            bool anyFocus = profiles.Count > 0;
            float detail = anyFocus ? 132f : 0f;
            var content = ScrollArea(box, "Profile cards", new Rect(x, 108f, width, area.height - 108f - detail - 24f), 0f);
            var searchRow = new GameObject("Profile search row", typeof(RectTransform)).GetComponent<RectTransform>();
            searchRow.SetParent(content, false);
            Place(searchRow, 0f, 4f, Mathf.Min(1120f, width), 44f);
            var visible = profileBrowser.Draw(searchRow, profiles, Rebuild);
            float y = 58f;
            var messages = new List<(string text, Color colour)>();
            if (!string.IsNullOrEmpty(libraryMessage)) messages.Add((libraryMessage, UiTheme.Accent));
            foreach (string error in store.ReadErrors) messages.Add((error, UiTheme.Warning));
            if (profiles.Count > 0 && visible.Count == 0) messages.Add(("No saved houseguests match this name. Clear search to see everyone.", UiTheme.Muted));
            foreach (var (text, colour) in messages)
            {
                var line = Words(content, text, 14f, colour, new Rect(0f, y, width, 40f), name: "Library message");
                line.textWrappingMode = TextWrappingModes.Normal;
                y += line.text.Length > 150 ? 42f : 24f;
            }
            y += 8f;

            var focus = visible.FirstOrDefault(profile => profile.id == focusId) ?? visible.FirstOrDefault();
            int columns = Mathf.Clamp(Mathf.FloorToInt((width + 16f) / 236f), 3, 7);
            float gap = 16f, card = (width - (columns - 1) * gap) / columns;
            float cardHeight = Mathf.Clamp(card * 1.12f, 180f, 280f);
            int rows = (visible.Count + 1 + columns - 1) / columns;
            NewHouseguestCard(content, new Rect(0f, y, card, cardHeight));
            for (int i = 0; i < visible.Count; i++)
            {
                int slot = i + 1;
                ProfileCard(content, visible[i], new Rect((slot % columns) * (card + gap), y + (slot / columns) * (cardHeight + gap), card, cardHeight),
                    focus != null && visible[i].id == focus.id);
            }
            if (profiles.Count == 0)
                Words(content, "Your saved houseguests will appear here.", 16f, UiTheme.Muted, new Rect(card + gap + 10f, y + cardHeight * .5f - 12f, width - card - gap, 24f));
            content.sizeDelta = new Vector2(0f, y + rows * (cardHeight + gap));
            if (focus != null) DetailBar(box, focus, new Rect(x, area.height - detail - 14f, width, detail));
        }

        /// <summary>A blank houseguest. Two presses, the second to confirm: it replaces the draft on screen.</summary>
        private void NewHouseguestCard(Transform parent, Rect r)
        {
            string caption = confirmingNew ? "Confirm new houseguest" : "Create new houseguest";
            var card = HudPrimitives.Fill(caption, parent, new Color(UiTheme.AccentDeep.r, UiTheme.AccentDeep.g, UiTheme.AccentDeep.b, .18f), 14);
            Place(card, r.x, r.y, r.width, r.height);
            var image = card.GetComponent<Image>(); image.raycastTarget = true;
            UiTheme.AddBorder(card, 14, confirmingNew ? UiTheme.Warning : new Color(UiTheme.Glow.r, UiTheme.Glow.g, UiTheme.Glow.b, .75f));
            var label = Words(card, caption, 17f, Color.white, new Rect(10f, r.height * .5f + 24f, r.width - 20f, 26f), TextAlignmentOptions.Center, "Label");
            var weight = UiTheme.Font(UiTheme.Weight.SemiBold); if (weight != null) label.font = weight;
            label.enableAutoSizing = true; label.fontSizeMax = 17f; label.fontSizeMin = 12f;
            var plus = HudPrimitives.Disc("Plus", card, new Color(UiTheme.ActionBlue.r, UiTheme.ActionBlue.g, UiTheme.ActionBlue.b, .9f));
            plus.anchorMin = plus.anchorMax = new Vector2(.5f, 1f); plus.pivot = new Vector2(.5f, .5f);
            plus.sizeDelta = new Vector2(64f, 64f); plus.anchoredPosition = new Vector2(0f, -(r.height * .5f - 16f));
            plus.GetComponent<Image>().raycastTarget = false;
            Words(plus, "+", 40f, Color.white, new Rect(0f, -4f, 64f, 64f), TextAlignmentOptions.Center, "Plus mark");
            Words(card, confirmingNew ? "Unsaved changes to this draft will be lost." : "Start from a blank draft", 12f,
                confirmingNew ? UiTheme.Warning : UiTheme.Muted, new Rect(10f, r.height * .5f + 54f, r.width - 20f, 36f), TextAlignmentOptions.Top)
                .textWrappingMode = TextWrappingModes.Normal;
            card.name = "New houseguest"; // named for what it is: the caption flips, and focus is kept by name
            var button = card.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            Seen(button, 14);
            button.onClick.AddListener(() =>
            {
                if (!confirmingNew) { confirmingNew = true; Rebuild(); return; }
                confirmingNew = false;
                draft = CharacterDraft.Blank();
                profileId = focusedProfile = null;
                draft.Appearance = draft.Appearance ?? new CharacterAppearance();
                if (Catalog != null) draft.Appearance = Catalog.Materialize(draft.Appearance);
                initialAppearance = draft.Appearance.Clone();
                quickScrollY = 0f;
                appearanceUndo.Clear(); appearanceRedo.Clear();
                comparingOriginal = false; appearanceNotice = lastSlider = null;
                libraryMessage = "A blank houseguest. Save it here when you are done.";
                studioPage = DefaultAppearancePage;
                Rebuild();
            });
        }

        /// <summary>
        /// A saved houseguest: the portrait, bound so it lands when it is built, the name - the card's
        /// caption - and the facts under it. Pressing it loads the houseguest into the draft.
        /// </summary>
        private void ProfileCard(Transform parent, CharacterProfile profile, Rect r, bool focused)
        {
            var entry = profile;
            var card = HudPrimitives.Fill("Profile " + entry.id, parent, UiTheme.Surface, 14);
            Place(card, r.x, r.y, r.width, r.height);
            var image = card.GetComponent<Image>(); image.raycastTarget = true;
            if (!UiTheme.PackSliced(image, focused ? PackArt.CastCardSelected : PackArt.CastCardResting, 16f))
                UiTheme.AddBorder(card, 14, focused ? UiTheme.Glow : UiTheme.Outline);
            if (focused) UiTheme.AddGlow(card, 14);
            var name = Words(card, entry.name, 18f, Color.white, new Rect(12f, r.height - 74f, r.width - 24f, 26f), TextAlignmentOptions.Center, "Label");
            var bold = UiTheme.Font(UiTheme.Weight.SemiBold); if (bold != null) name.font = bold;
            name.textWrappingMode = TextWrappingModes.NoWrap; name.overflowMode = TextOverflowModes.Ellipsis;
            var contestant = entry.contestant;
            var facts = new List<string>();
            if (contestant != null && contestant.age > 0) facts.Add(contestant.age.ToString());
            if (!string.IsNullOrWhiteSpace(contestant?.occupation)) facts.Add(contestant.occupation.Trim());
            Words(card, string.Join(" · ", facts), 13f, UiTheme.Muted, new Rect(12f, r.height - 48f, r.width - 24f, 18f), TextAlignmentOptions.Center, "Profile facts")
                .overflowMode = TextOverflowModes.Ellipsis;
            if (contestant?.traits != null && contestant.traits.Count > 0)
                Words(card, string.Join(" · ", contestant.traits), 12f, UiTheme.Heading, new Rect(12f, r.height - 28f, r.width - 24f, 18f), TextAlignmentOptions.Center, "Profile traits")
                    .overflowMode = TextOverflowModes.Ellipsis;
            float photo = Mathf.Min(r.width - 28f, r.height - 96f);
            var portrait = HudPrimitives.RectPortrait(card, "Profile portrait " + entry.id, null, contestant, new Vector2(photo, photo), 10);
            portrait.anchorMin = portrait.anchorMax = new Vector2(.5f, 1f); portrait.pivot = new Vector2(.5f, 1f);
            portrait.anchoredPosition = new Vector2(0f, -12f);
            if (entry.id == profileId)
            {
                var badge = HudPrimitives.Fill("Loaded badge", card, UiTheme.ActionBlue, 10);
                Place(badge, 12f, 12f, 86f, 22f);
                Words(badge, "LOADED", 11f, Color.white, new Rect(0f, 0f, 86f, 22f), TextAlignmentOptions.Center, "Loaded").characterSpacing = 2f;
            }
            var button = card.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            Seen(button, 14);
            button.onClick.AddListener(() =>
            {
                if (pendingDeleteProfile != entry.id) pendingDeleteProfile = null;
                focusedProfile = entry.id;
                AdoptLibraryDraft(entry, false, "My Houseguests");
            });
            // The card loads its houseguest, as it always has; this looks without loading, so the bar's
            // Duplicate and Delete reach any card without replacing the draft on screen.
            GlyphButton(parent, "Details for " + entry.name, new Rect(r.x + r.width - 44f, r.y + 8f, 36f, 36f), null, UiTheme.Pack(PackArt.KitIconMore),
                () => { if (pendingDeleteProfile != entry.id) pendingDeleteProfile = null; focusedProfile = entry.id; Rebuild(); });
        }

        /// <summary>
        /// The bar for the houseguest in focus: who they are, and what can be done with them - play
        /// as them, edit, duplicate, rename, and delete with a second press to confirm.
        /// </summary>
        private void DetailBar(Transform parent, CharacterProfile profile, Rect r)
        {
            var entry = profile;
            var bar = HudPrimitives.Fill("Houseguest detail", parent, new Color(UiTheme.Ink.r, UiTheme.Ink.g, UiTheme.Ink.b, .6f), 14);
            Place(bar, r.x, r.y, r.width, r.height);
            UiTheme.AddBorder(bar, 14, UiTheme.Edge(UiTheme.Emphasis.Interactive));
            float photo = r.height - 24f;
            var face = HudPrimitives.RectPortrait(bar, "Detail portrait", null, null, new Vector2(photo, photo), 10);
            Place(face, 12f, 12f, photo, photo);
            // The card above is already bound to this face; the bar shows what the card shows,
            // rather than asking for the same portrait twice.
            var card = parent.GetComponentsInChildren<RawImage>().FirstOrDefault(raw => raw.transform.parent != null
                && raw.transform.parent.name == "Profile portrait " + entry.id);
            face.GetComponentInChildren<RawImage>().gameObject.AddComponent<PortraitMirror>().Source = card;
            float x = photo + 30f;
            float actions = Mathf.Min(r.width * .58f, 860f);
            float info = r.width - x - actions - 20f;
            var name = Words(bar, entry.name, 24f, Color.white, new Rect(x, 16f, info, 32f), name: "Detail name");
            var bold = UiTheme.Font(UiTheme.Weight.Bold); if (bold != null) name.font = bold;
            name.textWrappingMode = TextWrappingModes.NoWrap; name.overflowMode = TextOverflowModes.Ellipsis;
            var contestant = entry.contestant;
            var parts = new List<string>();
            if (contestant != null && contestant.age > 0) parts.Add(contestant.age.ToString());
            if (!string.IsNullOrWhiteSpace(contestant?.occupation)) parts.Add(contestant.occupation.Trim());
            if (!string.IsNullOrWhiteSpace(contestant?.hometown)) parts.Add(contestant.hometown.Trim());
            if (!string.IsNullOrWhiteSpace(contestant?.pronouns)) parts.Add(contestant.pronouns);
            Words(bar, string.Join(" · ", parts), 14f, UiTheme.Muted, new Rect(x, 52f, info, 20f), name: "Detail facts").overflowMode = TextOverflowModes.Ellipsis;
            if (contestant?.traits != null && contestant.traits.Count > 0)
            {
                float tx = x;
                foreach (var trait in contestant.traits.Take(WebTraits.MaximumTraits))
                {
                    var tag = HudPrimitives.Fill("Detail trait", bar, new Color(UiTheme.AccentDeep.r, UiTheme.AccentDeep.g, UiTheme.AccentDeep.b, .45f), 12);
                    Place(tag, tx, 80f, 130f, 28f);
                    UiTheme.AddBorder(tag, 12, new Color(UiTheme.Glow.r, UiTheme.Glow.g, UiTheme.Glow.b, .7f));
                    Words(tag, trait, 13f, Color.white, new Rect(0f, 0f, 130f, 28f), TextAlignmentOptions.Center, "Detail trait label");
                    tx += 138f;
                }
            }
            else if (!string.IsNullOrWhiteSpace(contestant?.bio))
            {
                var bio = Words(bar, contestant.bio.Trim(), 13f, UiTheme.Paper, new Rect(x, 76f, info, r.height - 86f), name: "Detail bio");
                bio.textWrappingMode = TextWrappingModes.Normal; bio.overflowMode = TextOverflowModes.Ellipsis;
            }
            bool deleting = pendingDeleteProfile == entry.id;
            // The houseguest already loaded is the draft on screen, edits and all: its actions go to
            // the page and keep them. Any other is loaded from the library first.
            bool loaded = entry.id == profileId;
            Action open(string page) => () => { if (loaded) GoTo(page); else AdoptLibraryDraft(entry, false, page); };
            var buttons = new List<(string caption, Tone tone, string icon, Action act)>
            {
                ((editingCastSlot ? "Use " : "Play as ") + entry.name, Tone.Primary, PackArt.KitIconChevronRight, open("Review")),
                ("Edit " + entry.name, Tone.Secondary, PackArt.KitIconPerson, open("Appearance")),
                ("Duplicate " + entry.name, Tone.Secondary, PackArt.KitIconArchive, () => AdoptLibraryDraft(entry, true)),
                ("Rename " + entry.name, Tone.Secondary, PackArt.KitIconNote, open("Identity")),
                (deleting ? "Confirm delete " + entry.name : "Delete " + entry.name, Tone.Danger, PackArt.KitIconCross, () => DeleteProfile(entry)),
            };
            if (!loaded && info > 120f)
                Words(bar, "Play as, Edit and Rename load this houseguest in place of the draft on screen.", 12f, UiTheme.Muted,
                    new Rect(x, r.height - 24f, info, 18f), name: "Detail note").overflowMode = TextOverflowModes.Ellipsis;
            if (deleting) buttons.Add(("Cancel deletion", Tone.Quiet, null, () => { pendingDeleteProfile = null; libraryMessage = null; Rebuild(); }));
            float bw = (actions - (buttons.Count - 1) * 10f) / buttons.Count, bx = r.width - actions - 14f;
            foreach (var (caption, tone, icon, act) in buttons)
            {
                // The name is on the bar already; each control shows its verb and keeps the full caption.
                var button = Pill(bar, caption, new Rect(bx, (r.height - 60f) * .5f, bw, 60f), tone, act, size: 14f);
                if (tone == Tone.Danger) button.name = "Delete profile"; // its caption flips; focus is kept by name
                var label = button.transform.Find("Label").GetComponent<TMP_Text>();
                label.enabled = false;
                var shown = Words(button.transform, VerbOf(caption, entry.name), 16f, label.color, new Rect(0f, 34f, bw, 20f), TextAlignmentOptions.Center, "Verb");
                var weight = UiTheme.Font(UiTheme.Weight.SemiBold); if (weight != null) shown.font = weight;
                shown.textWrappingMode = TextWrappingModes.NoWrap; shown.enableAutoSizing = true; shown.fontSizeMax = 16f; shown.fontSizeMin = 11f;
                if (icon != null) Glyph(button.transform, icon, label.color, new Rect(bw * .5f - 11f, 8f, 22f, 22f));
                else shown.rectTransform.anchoredPosition = new Vector2(0f, -20f);
                bx += bw + 10f;
            }
        }

        private static string VerbOf(string caption, string name) =>
            caption.EndsWith(" " + name, StringComparison.Ordinal) ? caption.Substring(0, caption.Length - name.Length - 1) : caption;

        private void DeleteProfile(CharacterProfile entry)
        {
            if (pendingDeleteProfile != entry.id)
            {
                pendingDeleteProfile = entry.id;
                libraryMessage = "Delete removes the library profile. Existing seasons and the current draft are unaffected. Press Confirm delete to proceed.";
            }
            else
            {
                bool removed = ProfileStore.Delete(entry.id, out var deleteError);
                libraryMessage = removed ? "Deleted " + entry.name + " from My Houseguests." : deleteError;
                if (removed && profileId == entry.id) profileId = null;
                if (removed && focusedProfile == entry.id) focusedProfile = null;
                pendingDeleteProfile = null;
            }
            Rebuild();
        }

        private void AdoptLibraryDraft(CharacterProfile profile, bool duplicate, string page = null)
        {
            draft = profile.ToDraft();
            if (duplicate) draft.Name += " (copy)";
            profileId = duplicate ? null : profile.id;
            confirmingNew = false;
            draft.Appearance = draft.Appearance ?? new CharacterAppearance();
            if (Catalog != null) draft.Appearance = Catalog.Materialize(draft.Appearance);
            initialAppearance = draft.Appearance.Clone();
            quickScrollY = 0f;
            appearanceUndo.Clear(); appearanceRedo.Clear();
            comparingOriginal = false;
            appearanceNotice = lastSlider = null;
            lastSliderEdit = 0f;
            SelectPage(page ?? (duplicate ? "Identity" : DefaultAppearancePage));
            if (!duplicate && page == "My Houseguests") libraryMessage = "Loaded " + profile.name + ".";
            Rebuild();
        }

        private void SaveProfile(bool duplicate)
        {
            if (!draft.TryValidate(out var error)) { libraryMessage = error; Rebuild(); return; }
            string id = duplicate || string.IsNullOrEmpty(profileId) ? Guid.NewGuid().ToString("N") : profileId;
            if (ProfileStore.Save(CharacterProfile.FromDraft(id, draft), out error))
            { profileId = focusedProfile = id; libraryMessage = "Saved " + draft.Name + "."; }
            else libraryMessage = error;
            Rebuild();
        }

        // ---------------------------------------------------------------- review

        /// <summary>
        /// The last step: the houseguest on the platform, who they are and how they play, and the
        /// season they are about to walk into. Starting is the footer's primary here.
        /// </summary>
        private void BuildReviewPage(RectTransform scrim)
        {
            var area = PageArea;
            float stage = Mathf.Clamp(area.width * .28f, 360f, 520f);
            float season = Mathf.Clamp(area.width * .26f, 340f, 460f);
            float who = area.width - stage - season - 2f * Gap;
            BuildPreviewStage(scrim, new Rect(area.x, area.y, stage, area.height), false);
            BuildReviewIdentity(scrim, new Rect(area.x + stage + Gap, area.y, who, area.height));
            BuildReviewSeason(scrim, new Rect(area.x + area.width - season, area.y, season, area.height));
        }

        private void BuildReviewIdentity(RectTransform scrim, Rect r)
        {
            var box = Panel("Review houseguest", scrim, r, true);
            float x = 26f, width = r.width - 52f;
            float y = SectionHeading(box, x, 20f, width, PackArt.KitIconPerson, "REVIEW");
            var name = Words(box, DisplayName, 34f, Color.white,
                new Rect(x, y, width, 44f), name: "Review name");
            var bold = UiTheme.Font(UiTheme.Weight.Bold); if (bold != null) name.font = bold;
            name.enableAutoSizing = true; name.fontSizeMax = 34f; name.fontSizeMin = 18f;
            y += 46f;
            Words(box, Summary(), 17f, UiTheme.Muted, new Rect(x, y, width, 24f), name: "Review summary");
            y += 30f;
            Words(box, draft.Archetype, 16f, UiTheme.Hex("FF7AD9"), new Rect(x, y, width, 22f), name: "Review archetype").fontStyle = FontStyles.Italic;
            y += 34f;
            if (draft.Traits.Count > 0) { TraitTags(box, new Rect(x, y, width, 32f), TextAlignmentOptions.Left); y += 46f; }
            var bio = Words(box, string.IsNullOrWhiteSpace(draft.Bio) ? "No bio yet." : draft.Bio.Trim(), 16f,
                string.IsNullOrWhiteSpace(draft.Bio) ? UiTheme.Muted : UiTheme.Paper, new Rect(x, y, width, 40f), name: "Review bio");
            bio.textWrappingMode = TextWrappingModes.Normal; bio.overflowMode = TextOverflowModes.Ellipsis;
            float bioHeight = Mathf.Clamp(bio.GetPreferredValues(bio.text, width, 0f).y + 4f, 24f, Mathf.Max(24f, r.height - y - 250f));
            bio.rectTransform.sizeDelta = new Vector2(width, bioHeight);
            y += bioHeight + 24f;
            Words(box, draft.PreserveStats ? "PRESERVED STATS" : "STATS", 13f, UiTheme.Heading, new Rect(x, y, width, 18f)).characterSpacing = 2f;
            y += 26f;
            float column = (width - 24f) * .5f, row = Mathf.Clamp((r.height - y - 16f) / 4f, 30f, 44f);
            for (int i = 0; i < WebTraits.StatNames.Length; i++)
            {
                string stat = WebTraits.StatNames[i];
                float sx = x + (i % 2) * (column + 24f), sy = y + (i / 2) * row;
                double value = WebTraits.Get(draft.Stats, stat);
                Words(box, Capitalised(stat), 15f, UiTheme.Paper, new Rect(sx, sy, 120f, 22f));
                var track = HudPrimitives.Fill("Stat track", box, new Color(UiTheme.Outline.r, UiTheme.Outline.g, UiTheme.Outline.b, .6f), 3);
                Place(track, sx + 124f, sy + 8f, column - 170f, 8f);
                var fill = HudPrimitives.Fill("Stat fill", track, UiTheme.Glow, 3);
                fill.anchorMin = Vector2.zero; fill.anchorMax = new Vector2((float)(value / WebTraits.Maximum), 1f);
                fill.offsetMin = fill.offsetMax = Vector2.zero;
                Words(box, value.ToString("0"), 15f, Color.white, new Rect(sx + column - 36f, sy, 36f, 22f), TextAlignmentOptions.TopRight);
            }
        }

        private void BuildReviewSeason(RectTransform scrim, Rect r)
        {
            var box = Panel("Review season", scrim, r);
            float x = 24f, width = r.width - 48f;
            float y = SectionHeading(box, x, 20f, width, PackArt.KitIconHome, "THE SEASON", size: 20f);
            var house = Words(box, pending.HouseSize + " houseguests", 26f, Color.white, new Rect(x, y, width, 34f), name: "Review house");
            var bold = UiTheme.Font(UiTheme.Weight.Bold); if (bold != null) house.font = bold;
            y += 36f;
            Words(box, CastTemplates.RosterName(pending.Roster), 16f, UiTheme.Gold, new Rect(x, y, width, 22f), name: "Review roster");
            y += 40f;
            bool ready = draft.TryValidate(out var error);
            var checks = new List<(bool ok, string text)>
            {
                (!string.IsNullOrWhiteSpace(draft.Name), string.IsNullOrWhiteSpace(draft.Name) ? "A name is still needed" : "Named " + draft.Name.Trim()),
                (true, draft.Traits.Count + " of " + WebTraits.MaximumTraits + " traits"),
                (true, draft.PreserveStats ? "Saved gameplay build kept" : draft.Remaining == 0 ? "Every spare point spent" : draft.Remaining + " spare point" + (draft.Remaining == 1 ? "" : "s") + " left, which is allowed"),
            };
            if (!ready && !string.IsNullOrWhiteSpace(draft.Name)) checks.Add((false, error));
            foreach (var (ok, text) in checks)
            {
                Glyph(box, ok ? PackArt.KitIconCheck : PackArt.KitIconWarning, ok ? UiTheme.Positive : UiTheme.Warning, new Rect(x, y + 1f, 20f, 20f));
                Words(box, text, 15f, ok ? UiTheme.Paper : UiTheme.Warning, new Rect(x + 30f, y, width - 30f, 40f), name: "Review check").textWrappingMode = TextWrappingModes.Normal;
                y += 30f;
            }
            y += 14f;
            float remaining = r.height - y - 16f;
            var notes = Words(box, "The season receives a snapshot of this character. Future edits in My Houseguests will not alter an ongoing season.\n\n"
                + "Everyday is your house look. Competition and Formal outfits are used for their activities when saved. The outfit selected in this preview does not choose your starting activity outfit.",
                14f, UiTheme.Muted, new Rect(x, y, width, remaining), name: "Review notes");
            notes.textWrappingMode = TextWrappingModes.Normal; notes.overflowMode = TextOverflowModes.Ellipsis;
        }
    }
}
