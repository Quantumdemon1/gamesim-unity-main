using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Persistence;
using Gamesim.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gamesim.Presentation
{
    public sealed partial class CharacterCreator
    {
        private string studioPage = "Appearance", appearanceCategory = "Body", libraryMessage;
        private string profileId;
        private CharacterProfileStore profileStore;
        private readonly CharacterProfileBrowser profileBrowser = new CharacterProfileBrowser();
        public CharacterProfileStore ProfileStore => profileStore ?? (profileStore = new CharacterProfileStore());

        public void ConfigureProfiles(CharacterProfileStore store)
        {
            profileStore = store ?? throw new ArgumentNullException(nameof(store));
            profileId = pendingDeleteProfile = libraryMessage = null;
            profileBrowser.Reset();
            if (IsShowing) Rebuild();
        }
        private CharacterStudioPreview studioPreview;
        private TMP_Text previewStatus;
        private CharacterAppearance initialAppearance;
        private readonly Stack<CharacterAppearance> appearanceUndo = new Stack<CharacterAppearance>();
        private readonly Stack<CharacterAppearance> appearanceRedo = new Stack<CharacterAppearance>();
        private readonly HashSet<string> randomLocks = new HashSet<string>();
        private readonly System.Random cosmeticRandom = new System.Random();
        private ICharacterAppearanceCatalog Catalog => (CharacterBodySource.Provider as IModularCharacterBodyProvider)?.Catalog;
        private RectTransform studioControls;
        private float studioCursor;
        private string appearanceNotice;
        private Button undoAppearanceButton, redoAppearanceButton;
        private Button retryPreviewButton;
        private float lastSliderEdit;
        private string lastSlider;
        private bool comparingOriginal;
        private string expandedWardrobeSlot;
        private readonly Dictionary<string, int> wardrobePages = new Dictionary<string, int>();
        private float studioScrollY;
        private string lastStudioCategory, pendingDeleteProfile;
        private const float ControlsWidth = 640f;

        private void Update()
        {
            if (previewStatus != null && studioPreview != null) previewStatus.text = studioPreview.Status;
            if (retryPreviewButton != null && studioPreview != null) retryPreviewButton.interactable = studioPreview.CanRetry;
        }

        private void OnDestroy()
        { if (studioPreview != null) Destroy(studioPreview.gameObject); }

        private void BuildNavigation(RectTransform scrim)
        {
            var header = HudPrimitives.Label("Studio title", scrim, 26f, UiTheme.Paper, TextAlignmentOptions.Center);
            header.text = editingCastSlot ? "EDIT CAST HOUSEGUEST" : "CREATE A HOUSEGUEST";
            Place(header.rectTransform, Width, 45f, -18f);
            var navigation = HudPrimitives.Fill("Setup steps", scrim, Color.clear, 1);
            Place(navigation, Width, 52f, -70f);
            string[] pages = { "Appearance", "Identity", "Personality", "My Houseguests", "Review" };
            for (int i = 0; i < pages.Length; i++)
            {
                string page = pages[i];
                Chip(navigation, page, (i - 2) * 226f, 216f, studioPage == page,
                    () => { studioPage = page; Rebuild(); });
            }
        }

        private void BuildAppearanceStudio(RectTransform scrim)
        {
            lastStudioCategory = appearanceCategory;
            if (studioPreview == null) studioPreview = CharacterStudioPreview.Create();
            studioPreview.gameObject.SetActive(true);
            draft.Appearance = draft.Appearance ?? new CharacterAppearance();
            if (Catalog != null) draft.Appearance = Catalog.Materialize(draft.Appearance);
            studioPreview.Show(comparingOriginal ? initialAppearance ?? new CharacterAppearance() : draft.Appearance);
            var area = HudPrimitives.Fill("Appearance studio", scrim, Color.clear, 1);
            area.anchorMin = new Vector2(.5f, 0f); area.anchorMax = new Vector2(.5f, 1f);
            area.sizeDelta = new Vector2(Width, -290f);
            area.anchoredPosition = new Vector2(0f, -5f);

            var image = new GameObject("Live character preview", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            var previewRegion = new GameObject("Preview image region", typeof(RectTransform)).GetComponent<RectTransform>();
            previewRegion.SetParent(area, false);
            image.transform.SetParent(previewRegion, false);
            image.texture = studioPreview.Texture;
            image.raycastTarget = false;
            var previewRect = previewRegion;
            previewRect.anchorMin = new Vector2(0f, 0f); previewRect.anchorMax = new Vector2(0f, 1f);
            previewRect.pivot = new Vector2(0f, .5f); previewRect.sizeDelta = new Vector2(480f, -185f);
            previewRect.anchoredPosition = new Vector2(0f, 70f);
            Stretch(image.rectTransform);
            var aspect = image.gameObject.AddComponent<AspectRatioFitter>();
            aspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            aspect.aspectRatio = .8f;

            var view = HudPrimitives.Fill("Preview controls", area, Color.clear, 1);
            view.anchorMin = view.anchorMax = new Vector2(0f, 0f);
            view.pivot = new Vector2(0f, 0f); view.sizeDelta = new Vector2(480f, 40f);
            view.anchoredPosition = new Vector2(0f, 107f);
            Chip(view, "Rotate left", -165f, 145f, false, () => studioPreview.Rotate(-30f));
            Chip(view, "Front", 0f, 130f, false, () => studioPreview.View(0f));
            Chip(view, "Rotate right", 165f, 145f, false, () => studioPreview.Rotate(30f));
            var zoomRow = HudPrimitives.Fill("Zoom controls", area, Color.clear, 1);
            zoomRow.anchorMin = zoomRow.anchorMax = new Vector2(0f, 0f);
            zoomRow.pivot = new Vector2(0f, 0f); zoomRow.sizeDelta = new Vector2(480f, 40f);
            zoomRow.anchoredPosition = new Vector2(0f, 66f);
            Chip(zoomRow, "Zoom out", -165f, 145f, false, () => studioPreview.Zoom(-.1f));
            Chip(zoomRow, "Side", 0f, 130f, false, () => studioPreview.View(90f));
            Chip(zoomRow, "Zoom in", 165f, 145f, false, () => studioPreview.Zoom(.1f));
            var statusRow = HudPrimitives.Fill("Preview recovery", area, Color.clear, 1);
            statusRow.anchorMin = statusRow.anchorMax = new Vector2(0f, 0f);
            statusRow.pivot = Vector2.zero; statusRow.sizeDelta = new Vector2(480f, 62f);
            retryPreviewButton = Chip(statusRow, "Retry preview", 175f, 124f, false, () => studioPreview.Retry());
            retryPreviewButton.interactable = studioPreview.CanRetry;
            previewStatus = HudPrimitives.Label("Preview status", statusRow, 12f, UiTheme.Muted, TextAlignmentOptions.Center);
            previewStatus.rectTransform.anchorMin = previewStatus.rectTransform.anchorMax = new Vector2(0f, 0f);
            previewStatus.rectTransform.pivot = new Vector2(0f, 0f);
            previewStatus.rectTransform.sizeDelta = new Vector2(345f, 62f);

            var viewport = HudPrimitives.Fill("Appearance controls viewport", area, Color.clear, 1);
            viewport.anchorMin = new Vector2(1f, 0f); viewport.anchorMax = Vector2.one;
            viewport.pivot = new Vector2(1f, .5f); viewport.sizeDelta = new Vector2(ControlsWidth, 0f);
            viewport.gameObject.AddComponent<RectMask2D>();
            studioControls = new GameObject("Appearance controls", typeof(RectTransform)).GetComponent<RectTransform>();
            studioControls.SetParent(viewport, false);
            studioControls.anchorMin = new Vector2(0f, 1f); studioControls.anchorMax = Vector2.one;
            studioControls.pivot = new Vector2(.5f, 1f);
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            viewport.gameObject.AddComponent<SetupScrollFocus>();
            scroll.content = studioControls; scroll.viewport = viewport; scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 40f;
            studioCursor = 0f;

            StudioText("Your appearance is independent of pronouns, personality and competition stats.", 46f);
            var undoRow = StudioRow(42f);
            undoAppearanceButton = Chip(undoRow, "Undo", -213f, 196f, false, UndoAppearance);
            redoAppearanceButton = Chip(undoRow, "Redo", 0f, 196f, false, RedoAppearance);
            undoAppearanceButton.interactable = appearanceUndo.Count > 0;
            redoAppearanceButton.interactable = appearanceRedo.Count > 0;
            Chip(undoRow, "Reset look", 213f, 196f, false,
                () => ChangeAppearance(() => draft.Appearance = initialAppearance?.Clone() ?? new CharacterAppearance()));
            var presets = StudioRow(44f);
            Chip(presets, "Previous starting look", -160f, 306f, false, () => CyclePreset(-1));
            Chip(presets, "Next starting look", 160f, 306f, false, () => CyclePreset(1));
            var comparison = StudioRow(44f);
            Chip(comparison, comparingOriginal ? "Return to edited look" : "Compare original", -160f, 306f, comparingOriginal,
                () => { comparingOriginal = !comparingOriginal; Rebuild(); });
            Chip(comparison, "Reset " + appearanceCategory.ToLowerInvariant(), 160f, 306f, false, ResetAppearanceCategory);

            var categories = new[] { "Body", "Face", "Hair", "Clothing", "Colors" };
            var categoriesRow = StudioRow(44f);
            for (int i = 0; i < categories.Length; i++)
            {
                string category = categories[i];
                Chip(categoriesRow, category, (i - 2) * 126f, 120f, appearanceCategory == category,
                    () => { appearanceCategory = category; studioPreview.FocusFace(category == "Face" || category == "Hair"); Rebuild(); });
            }
            var random = StudioRow(44f);
            Chip(random, randomLocks.Contains(appearanceCategory) ? "Unlock " + appearanceCategory : "Lock " + appearanceCategory,
                -160f, 306f, randomLocks.Contains(appearanceCategory), () =>
                { if (!randomLocks.Add(appearanceCategory)) randomLocks.Remove(appearanceCategory); Rebuild(); });
            Chip(random, "Randomize unlocked", 160f, 306f, false, RandomizeAppearance);
            if (Catalog == null)
            {
                StudioText("This installation supports complete preset bodies. Modular controls appear when compatible character content is available.", 72f);
                studioControls.sizeDelta = new Vector2(0f, studioCursor);
                studioControls.anchoredPosition = new Vector2(0f, studioScrollY);
                return;
            }
            if (!string.IsNullOrEmpty(appearanceNotice)) StudioText(appearanceNotice, 46f + appearanceNotice.Count(value => value == '\n') * 24f);
            if (appearanceCategory == "Body")
            {
                var bodyRow = StudioRow(44f);
                for (int i = 0; i < Catalog.Bodies.Count; i++)
                {
                    var body = Catalog.Bodies[i];
                    Chip(bodyRow, body.Label, (i - (Catalog.Bodies.Count - 1) * .5f) * 300f, 285f,
                        Catalog.Materialize(draft.Appearance).bodyId == body.Id,
                        () => ChangeAppearance(() =>
                        {
                            var changed = Catalog.ChangeBody(draft.Appearance, body.Id);
                            appearanceNotice = AppearanceEditing.DescribeSubstitutions(draft.Appearance, changed, Catalog);
                            draft.Appearance = changed;
                        }));
                }
                StudioText("Changing body replaces clothes that do not fit. Undo restores your previous selections.", 44f);
            }
            if (appearanceCategory == "Body" || appearanceCategory == "Face")
                foreach (var control in Catalog.Controls.Where(control => control.Category == appearanceCategory && control.Fits(draft.Appearance.bodyId))) ControlSlider(control);
            else if (appearanceCategory == "Hair")
            { WardrobeSelector("Hair"); WardrobeSelector("Eyebrows"); WardrobeSelector("Beard"); }
            else if (appearanceCategory == "Clothing")
            {
                OutfitSelector();
                WardrobeSelector("Chest"); WardrobeSelector("Legs"); WardrobeSelector("Feet");
                WardrobeSelector("TopUnderlayer"); WardrobeSelector("BottomUnderlayer");
                var appearance = Catalog.Materialize(draft.Appearance);
                var outfit = AppearanceEditing.Outfit(appearance);
                var channels = outfit.wardrobe.SelectMany(worn => AppearanceEditing.Find(Catalog, worn.itemId)?.ColorChannels
                    ?? Array.Empty<string>()).Where(id => id != "Skin" && id != "Hair" && id != "Eyes" && id != "Brows").Distinct();
                foreach (string channel in channels) ColorSelector(channel, true);
                StudioText("Fabric colors are offered only for clothing with a working shared color channel.", 44f);
            }
            else { ColorSelector("Skin"); ColorSelector("Hair"); ColorSelector("Brows"); ColorSelector("Eyes"); }
            studioControls.sizeDelta = new Vector2(0f, studioCursor + 16f);
            studioControls.anchoredPosition = new Vector2(0f, studioScrollY);
        }

        private void ChangeAppearance(Action change, bool rebuild = true)
        {
            appearanceUndo.Push((draft.Appearance ?? new CharacterAppearance()).Clone());
            appearanceRedo.Clear();
            lastSlider = null;
            comparingOriginal = false;
            if (Catalog != null) draft.Appearance = Catalog.Materialize(draft.Appearance);
            change();
            studioPreview?.Show(draft.Appearance);
            if (rebuild) Rebuild();
        }

        private void ResetAppearanceCategory()
        {
            if (Catalog == null) { ChangeAppearance(() => draft.Appearance = initialAppearance?.Clone() ?? new CharacterAppearance()); return; }
            ChangeAppearance(() =>
            {
                var original = Catalog.Materialize(initialAppearance);
                if (appearanceCategory == "Body" || appearanceCategory == "Face")
                {
                    if (appearanceCategory == "Body") draft.Appearance = Catalog.ChangeBody(draft.Appearance, original.bodyId);
                    foreach (var control in Catalog.Controls.Where(control => control.Category == appearanceCategory && control.Fits(draft.Appearance.bodyId)))
                    {
                        var value = original.dna.FirstOrDefault(item => item.id == control.Id);
                        if (value != null) AppearanceEditing.SetValue(draft.Appearance, control.Id, value.value);
                    }
                }
                else if (appearanceCategory == "Colors") draft.Appearance.colors = original.colors.Select(color => color.Clone()).ToList();
                else if (appearanceCategory == "Clothing")
                {
                    var hairSlots = new[] { "Hair", "Eyebrows", "Beard" };
                    foreach (var outfit in draft.Appearance.outfits)
                    {
                        var baseline = original.outfits.FirstOrDefault(item => item.id == outfit.id) ?? original.outfits.First();
                        outfit.wardrobe.RemoveAll(item => !hairSlots.Contains(item.slot));
                        outfit.wardrobe.AddRange(baseline.wardrobe.Where(item => !hairSlots.Contains(item.slot)).Select(item => item.Clone()));
                        outfit.colors = baseline.colors.Select(color => color.Clone()).ToList();
                    }
                    draft.Appearance = Catalog.ChangeBody(draft.Appearance, draft.Appearance.bodyId);
                }
                else
                {
                    // The baseline is the outfit being EDITED, found in the snapshot by its own id -
                    // not the snapshot's active outfit. Those are the same set only until the user
                    // switches outfits, after which resetting hair restored somebody else's. Read
                    // rather than resolved, too: the resolving helper appends a missing outfit to
                    // whatever it is given, and a reset baseline must not be edited by being read.
                    var outfit = AppearanceEditing.Outfit(draft.Appearance);
                    var baseline = original.outfits.FirstOrDefault(item => item.id == outfit.id)
                        ?? original.outfits.FirstOrDefault(item => item.id == original.activeOutfit)
                        ?? original.outfits.FirstOrDefault();
                    var slots = AppearanceEditing.CharacterSlots;
                    // Off every outfit, because Wear puts them on every outfit.
                    foreach (var set in draft.Appearance.outfits)
                        set.wardrobe.RemoveAll(item => slots.Contains(item.slot));
                    foreach (var worn in (baseline?.wardrobe ?? new System.Collections.Generic.List<AppearanceWardrobe>())
                                 .Where(item => slots.Contains(item.slot)))
                    {
                        var item = Catalog.Items.FirstOrDefault(option => option.Matches(worn.itemId) && option.Fits(draft.Appearance.bodyId));
                        if (item != null) AppearanceEditing.Wear(draft.Appearance, item, Catalog);
                    }
                }
                appearanceNotice = "Reset " + appearanceCategory.ToLowerInvariant() + ". Undo restores the previous choices.";
            });
        }

        private void UndoAppearance()
        {
            if (appearanceUndo.Count == 0) return;
            appearanceRedo.Push(draft.Appearance.Clone()); draft.Appearance = appearanceUndo.Pop();
            studioPreview?.Show(draft.Appearance); Rebuild();
        }

        private void RedoAppearance()
        {
            if (appearanceRedo.Count == 0) return;
            appearanceUndo.Push(draft.Appearance.Clone()); draft.Appearance = appearanceRedo.Pop();
            studioPreview?.Show(draft.Appearance); Rebuild();
        }

        private void CyclePreset(int direction)
        {
            var ids = new List<string> { ContentCatalog.PlayerId };
            foreach (CastTemplates.Roster roster in Enum.GetValues(typeof(CastTemplates.Roster)))
                ids.AddRange(CastTemplates.In(roster).Select(template => template.Id));
            int current = Math.Max(0, ids.IndexOf(draft.Appearance?.presetId));
            string id = ids[(current + direction + ids.Count) % ids.Count];
            ChangeAppearance(() => draft.Appearance = Catalog?.Materialize(new CharacterAppearance { presetId = id })
                ?? new CharacterAppearance { presetId = id });
        }

        private void ControlSlider(AppearanceControl control)
        {
            var appearance = Catalog.Materialize(draft.Appearance);
            float value = appearance.dna.FirstOrDefault(item => item.id == control.Id)?.value ?? .5f;
            var row = StudioRow(66f);
            var caption = HudPrimitives.Label(control.Label, row, 15f, UiTheme.Paper, TextAlignmentOptions.TopLeft);
            Stretch(caption.rectTransform); caption.text = control.Label + "  " + value.ToString("0.00");
            var track = HudPrimitives.Fill(control.Label + " slider", row, UiTheme.SurfaceRaised, 6);
            track.anchorMin = new Vector2(0f, 0f); track.anchorMax = new Vector2(1f, 0f);
            track.pivot = new Vector2(.5f, 0f); track.sizeDelta = new Vector2(-16f, 24f);
            var handle = HudPrimitives.Fill("Handle", track, UiTheme.Accent, 8);
            handle.sizeDelta = new Vector2(22f, 26f);
            track.GetComponent<Image>().raycastTarget = true;
            handle.GetComponent<Image>().raycastTarget = true;
            var slider = track.gameObject.AddComponent<Slider>();
            slider.targetGraphic = handle.GetComponent<Image>(); slider.handleRect = handle;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = control.Minimum; slider.maxValue = control.Maximum; slider.value = value;
            slider.onValueChanged.AddListener(next =>
            {
                if (lastSlider != control.Id || Time.unscaledTime - lastSliderEdit > .4f)
                {
                    appearanceUndo.Push(draft.Appearance.Clone());
                    appearanceRedo.Clear();
                }
                lastSlider = control.Id; lastSliderEdit = Time.unscaledTime;
                comparingOriginal = false;
                draft.Appearance = Catalog.Materialize(draft.Appearance);
                AppearanceEditing.SetValue(draft.Appearance, control.Id, next);
                studioPreview?.Show(draft.Appearance);
                if (undoAppearanceButton != null) undoAppearanceButton.interactable = true;
                if (redoAppearanceButton != null) redoAppearanceButton.interactable = false;
                caption.text = control.Label + "  " + next.ToString("0.00");
            });
        }

        private void WardrobeSelector(string slot)
        {
            string slotLabel = slot == "TopUnderlayer" ? "Underwear top" : slot == "BottomUnderlayer" ? "Underwear bottoms"
                : slot == "Chest" ? "Top" : slot == "Legs" ? "Bottoms" : slot == "Feet" ? "Shoes" : slot == "Beard" ? "Facial hair" : slot;
            var appearance = Catalog.Materialize(draft.Appearance);
            var choices = Catalog.Items.Where(item => item.Slot == slot && item.Fits(appearance.bodyId)).ToList();
            if (choices.Count == 0) return;
            var selected = AppearanceEditing.Outfit(appearance).wardrobe.FirstOrDefault(item => item.slot == slot);
            int index = choices.FindIndex(item => item.Matches(selected?.itemId));
            string label = index >= 0 ? choices[index].Label : selected == null ? "None" : "Saved item unavailable";
            StudioText(slotLabel + ": " + label, 38f);
            var row = StudioRow(42f);
            Chip(row, "Previous " + slotLabel.ToLowerInvariant(), -213f, 198f, false,
                () => ChangeAppearance(() => AppearanceEditing.Wear(draft.Appearance, choices[(index - 1 + choices.Count) % choices.Count], Catalog)))
                .name = "Previous " + slot.ToLowerInvariant();
            Chip(row, "Next " + slotLabel.ToLowerInvariant(), 0f, 198f, false,
                () => ChangeAppearance(() => AppearanceEditing.Wear(draft.Appearance, choices[(index + 1) % choices.Count], Catalog)))
                .name = "Next " + slot.ToLowerInvariant();
            Chip(row, "Remove " + slotLabel.ToLowerInvariant(), 213f, 198f, false,
                () => ChangeAppearance(() => AppearanceEditing.Outfit(draft.Appearance).wardrobe.RemoveAll(item => item.slot == slot)))
                .name = "Remove " + slot.ToLowerInvariant();
            var browse = StudioRow(42f);
            Chip(browse, expandedWardrobeSlot == slot ? "Close " + slotLabel.ToLowerInvariant() + " styles" : "Browse " + choices.Count + " " + slotLabel.ToLowerInvariant() + " styles",
                0f, 580f, expandedWardrobeSlot == slot,
                () => { expandedWardrobeSlot = expandedWardrobeSlot == slot ? null : slot; Rebuild(); });
            if (expandedWardrobeSlot != slot) return;
            int page = wardrobePages.TryGetValue(slot, out var savedPage) ? savedPage : Math.Max(0, index) / 6;
            int pages = (choices.Count + 5) / 6;
            page = Math.Min(page, pages - 1);
            for (int r = 0; r < 2; r++)
            {
                var cards = StudioRow(132f);
                for (int c = 0; c < 3; c++)
                {
                    int itemIndex = page * 6 + r * 3 + c;
                    if (itemIndex >= choices.Count) break;
                    var item = choices[itemIndex];
                    var card = HudPrimitives.Fill("Wear " + item.Label, cards, item.Matches(selected?.itemId) ? UiTheme.AccentDeep : UiTheme.SurfaceRaised, 8);
                    card.anchorMin = card.anchorMax = new Vector2(.5f, .5f);
                    card.sizeDelta = new Vector2(198f, 128f);
                    card.anchoredPosition = new Vector2((c - 1) * 211f, 0f);
                    card.GetComponent<Image>().raycastTarget = true;
                    var button = card.gameObject.AddComponent<Button>();
                    button.targetGraphic = card.GetComponent<Image>();
                    button.onClick.AddListener(() => ChangeAppearance(() => AppearanceEditing.Wear(draft.Appearance, item, Catalog)));
                    var labelText = HudPrimitives.Label("Style name", card, 11f, UiTheme.Paper, TextAlignmentOptions.Center);
                    labelText.text = item.Label;
                    labelText.rectTransform.anchorMin = labelText.rectTransform.anchorMax = new Vector2(.5f, 0f);
                    labelText.rectTransform.pivot = new Vector2(.5f, 0f);
                    labelText.rectTransform.sizeDelta = new Vector2(188f, 32f);
                    if (item.Thumbnail != null)
                    {
                        var art = new GameObject("Style thumbnail", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                        art.transform.SetParent(card, false);
                        art.sprite = item.Thumbnail; art.preserveAspect = true; art.raycastTarget = false;
                        art.rectTransform.anchorMin = art.rectTransform.anchorMax = new Vector2(.5f, 1f);
                        art.rectTransform.pivot = new Vector2(.5f, 1f);
                        art.rectTransform.sizeDelta = new Vector2(184f, 90f);
                        art.rectTransform.anchoredPosition = new Vector2(0f, -4f);
                    }
                }
            }
            if (pages > 1)
            {
                var paging = StudioRow(42f);
                Chip(paging, "Previous styles", -213f, 198f, false, () => { wardrobePages[slot] = (page - 1 + pages) % pages; Rebuild(); });
                var pageLabel = HudPrimitives.Label("Styles page", paging, 13f, UiTheme.Muted, TextAlignmentOptions.Center);
                pageLabel.text = (page + 1) + " / " + pages;
                pageLabel.rectTransform.sizeDelta = new Vector2(130f, 32f);
                Chip(paging, "More styles", 213f, 198f, false, () => { wardrobePages[slot] = (page + 1) % pages; Rebuild(); });
            }
        }

        private void OutfitSelector()
        {
            StudioText("Outfit: " + (draft.Appearance?.activeOutfit ?? "Everyday"), 32f);
            var options = new[] { "Everyday", "Competition", "Formal", "Sleepwear", "Swimwear" };
            var row = StudioRow(44f);
            for (int i = 0; i < options.Length; i++)
            {
                string option = options[i];
                Chip(row, option, (i - 2) * 126f, 120f, draft.Appearance?.activeOutfit == option, () => ChangeAppearance(() =>
                {
                    var current = AppearanceEditing.Outfit(draft.Appearance);
                    if (!draft.Appearance.outfits.Any(outfit => outfit.id == option))
                    {
                        var copy = current.Clone(); copy.id = option; draft.Appearance.outfits.Add(copy);
                    }
                    draft.Appearance.activeOutfit = option;
                }));
            }
        }

        private static readonly string[] ColorSwatches = { "241B18", "62412F", "A5714A", "D0A078", "F0D3B0", "EEE4D7", "442E56", "386778", "526B39", "982F38", "CA8D36", "383D49" };

        private void ColorSelector(string channel, bool outfitColor = false)
        {
            StudioText(channel + " color", 28f);
            var row = StudioRow(44f);
            for (int i = 0; i < ColorSwatches.Length; i++)
            {
                ColorUtility.TryParseHtmlString("#" + ColorSwatches[i], out var color);
                var button = Chip(row, channel + " " + (i + 1), (i - 5.5f) * 52f, 46f, false, () => ChangeAppearance(() =>
                {
                    if (!outfitColor) AppearanceEditing.SetColor(draft.Appearance, channel, color);
                    else
                    {
                        var outfit = AppearanceEditing.Outfit(draft.Appearance);
                        outfit.colors.RemoveAll(entry => entry.id == channel);
                        outfit.colors.Add(new AppearanceColor { id = channel, r = color.r, g = color.g, b = color.b, a = 1f });
                    }
                }));
                button.GetComponent<Image>().color = color;
                button.GetComponentInChildren<TMP_Text>().text = (i + 1).ToString();
                button.GetComponentInChildren<TMP_Text>().color = UiTheme.OnColor(color);
            }
        }

        private void RandomizeAppearance()
        {
            if (Catalog == null) { CyclePreset(1); return; }
            ChangeAppearance(() =>
            {
                foreach (var control in Catalog.Controls.Where(control => control.Fits(draft.Appearance.bodyId)))
                    if (!randomLocks.Contains(control.Category))
                        AppearanceEditing.SetValue(draft.Appearance, control.Id,
                            Mathf.Lerp(control.Minimum, control.Maximum, (float)cosmeticRandom.NextDouble()));
                foreach (string slot in new[] { "Hair", "Eyebrows", "Beard", "Chest", "Legs", "Feet" })
                {
                    if (randomLocks.Contains(slot == "Hair" || slot == "Eyebrows" || slot == "Beard" ? "Hair" : "Clothing")) continue;
                    var choices = Catalog.Items.Where(item => item.Slot == slot && item.Fits(draft.Appearance.bodyId)).ToList();
                    if (choices.Count > 0) AppearanceEditing.Wear(draft.Appearance, choices[cosmeticRandom.Next(choices.Count)], Catalog);
                }
                if (!randomLocks.Contains("Colors")) foreach (string id in new[] { "Skin", "Hair", "Brows", "Eyes" })
                {
                    ColorUtility.TryParseHtmlString("#" + ColorSwatches[cosmeticRandom.Next(id == "Skin" ? 6 : ColorSwatches.Length)], out var color);
                    AppearanceEditing.SetColor(draft.Appearance, id, color);
                }
            });
        }

        private void BuildLibrary()
        {
            Text("MY HOUSEGUESTS", 22f, UiTheme.Paper, 40f, TextAlignmentOptions.Left);
            Text("Saved profiles can be reused, renamed, duplicated or deleted. Existing seasons keep their own independent copy.", 15f, UiTheme.Muted, 44f, TextAlignmentOptions.Left);
            var row = Row(48f);
            Chip(row, "Save this houseguest", -200f, 350f, true, () => SaveProfile(false));
            Chip(row, "Save a new copy", 200f, 350f, false, () => SaveProfile(true));
            if (!string.IsNullOrEmpty(libraryMessage)) Text(libraryMessage, 15f, UiTheme.Accent, 45f, TextAlignmentOptions.Left);
            var store = ProfileStore;
            var profiles = store.List();
            foreach (string error in store.ReadErrors) Text(error, 14f, UiTheme.Warning, 52f, TextAlignmentOptions.Left);
            if (profiles.Count == 0) Text("Your saved houseguests will appear here.", 16f, UiTheme.Muted, 42f, TextAlignmentOptions.Left);
            var visibleProfiles = profileBrowser.Draw(Row(48f), profiles, Rebuild);
            if (profiles.Count > 0 && visibleProfiles.Count == 0)
                Text("No saved houseguests match this name. Clear search to see everyone.", 16f, UiTheme.Muted, 42f, TextAlignmentOptions.Left);
            foreach (var profile in visibleProfiles)
            {
                var entry = profile;
                var profileRow = Row(56f);
                CharacterProfileBrowser.Thumbnail(profileRow, entry, -518f);
                Chip(profileRow, entry.name, -285f, 340f, false, () =>
                {
                    AdoptLibraryDraft(entry, false);
                });
                Chip(profileRow, "Duplicate " + entry.name, 65f, 300f, false, () =>
                {
                    AdoptLibraryDraft(entry, true);
                });
                Chip(profileRow, pendingDeleteProfile == entry.id ? "Confirm delete " + entry.name : "Delete " + entry.name,
                    400f, 325f, pendingDeleteProfile == entry.id, () =>
                    {
                        if (pendingDeleteProfile != entry.id)
                        { pendingDeleteProfile = entry.id; libraryMessage = "Delete removes the library profile. Existing seasons and the current draft are unaffected. Click Confirm delete to proceed."; }
                        else
                        {
                            bool removed = store.Delete(entry.id, out var deleteError);
                            libraryMessage = removed ? "Deleted " + entry.name + " from My Houseguests." : deleteError;
                            if (removed && profileId == entry.id) profileId = null;
                            pendingDeleteProfile = null;
                        }
                        Rebuild();
                    });
            }
            if (pendingDeleteProfile != null)
                Chip(Row(48f), "Cancel deletion", 0f, 420f, false, () => { pendingDeleteProfile = null; libraryMessage = null; Rebuild(); });
        }

        private void AdoptLibraryDraft(CharacterProfile profile, bool duplicate)
        {
            draft = profile.ToDraft();
            if (duplicate) draft.Name += " (copy)";
            profileId = duplicate ? null : profile.id;
            draft.Appearance = draft.Appearance ?? new CharacterAppearance();
            if (Catalog != null) draft.Appearance = Catalog.Materialize(draft.Appearance);
            initialAppearance = draft.Appearance.Clone();
            appearanceUndo.Clear(); appearanceRedo.Clear();
            comparingOriginal = false;
            appearanceNotice = lastSlider = null;
            lastSliderEdit = 0f;
            studioPage = duplicate ? "Identity" : "Appearance";
            Rebuild();
        }

        private void SaveProfile(bool duplicate)
        {
            if (!draft.TryValidate(out var error)) { libraryMessage = error; Rebuild(); return; }
            string id = duplicate || string.IsNullOrEmpty(profileId) ? Guid.NewGuid().ToString("N") : profileId;
            if (ProfileStore.Save(CharacterProfile.FromDraft(id, draft), out error))
            { profileId = id; libraryMessage = "Saved " + draft.Name + "."; }
            else libraryMessage = error;
            Rebuild();
        }

        private void BuildReview()
        {
            Text(string.IsNullOrWhiteSpace(draft.Name) ? "Your houseguest" : draft.Name, 26f, UiTheme.Paper, 44f, TextAlignmentOptions.Left);
            Text(Summary(), 18f, UiTheme.Muted, 36f, TextAlignmentOptions.Left);
            Text(string.Join(" · ", draft.Traits), 17f, UiTheme.Accent, 34f, TextAlignmentOptions.Left);
            Text(draft.Bio, 16f, UiTheme.Paper, 110f, TextAlignmentOptions.Left);
            Text(pending.HouseSize + " houseguests · " + CastTemplates.RosterName(pending.Roster), 18f, UiTheme.Gold, 42f, TextAlignmentOptions.Left);
            Text("The season receives a snapshot of this character. Future edits in My Houseguests will not alter an ongoing season.",
                16f, UiTheme.Muted, 60f, TextAlignmentOptions.Left);
            Text("Everyday is your house look. Competition and Formal outfits are used for their activities when saved. The outfit selected in this preview does not choose your starting activity outfit.",
                16f, UiTheme.Muted, 86f, TextAlignmentOptions.Left);
        }

        private RectTransform StudioRow(float height)
        {
            var row = HudPrimitives.Fill("Studio row", studioControls, Color.clear, 1);
            Place(row, ControlsWidth - 8f, height, -studioCursor);
            studioCursor += height + 6f;
            return row;
        }

        private void StudioText(string value, float height)
        {
            var row = StudioRow(height);
            var label = HudPrimitives.Label("Studio label", row, 14f, UiTheme.Paper, TextAlignmentOptions.Left);
            Stretch(label.rectTransform); label.text = value;
        }
    }
}
