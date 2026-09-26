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
    /// <summary>
    /// The appearance step (the creator's first mockup): the categories down the left, the live
    /// preview on its lit platform in the middle, and the category's choices at the right - body
    /// shapes and proportions, the starting looks and the face, hair styles, the wardrobe with a
    /// colour for each garment, and the skin, hair and eye colours.
    /// </summary>
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
        private System.Random cosmeticRandom = new System.Random();

        /// <summary>
        /// Seeds Randomize, for a test that has to roll the same houseguests every run. Cosmetic
        /// only: nothing in a season draws from it.
        /// </summary>
        public int RandomSeed { set => cosmeticRandom = new System.Random(value); }
        private ICharacterAppearanceCatalog Catalog => (CharacterBodySource.Provider as IModularCharacterBodyProvider)?.Catalog;
        private RectTransform studioControls;
        private float studioCursor, studioWidth;
        private string appearanceNotice;
        private Button undoAppearanceButton, redoAppearanceButton;
        private Button retryPreviewButton;
        private float lastSliderEdit;
        private string lastSlider;
        private bool comparingOriginal;
        private float studioScrollY;
        private string lastStudioCategory, pendingDeleteProfile;
        private bool previewOnPage;
        private bool? studioFace;

        private static readonly string[] Categories = { "Body", "Face", "Hair", "Clothing", "Accessories", "Colors" };

        /// <summary>The face's sliders in the groups the page shows them under; anything the catalog adds later goes under the last.</summary>
        private static readonly (string title, string[] ids)[] FaceGroups =
        {
            ("FACE SHAPE", new[] { "headWidth", "foreheadSize", "foreheadPosition", "jawsSize", "jawsPosition", "mandibleSize", "chinSize", "chinPronounced", "chinPosition" }),
            ("CHEEKS", new[] { "cheekSize", "cheekPosition", "cheekPronounced", "lowCheekPronounced" }),
            ("EYES & BROWS", new[] { "eyeSize", "eyeSpacing", "eyeRotation", "EyePosition", "BrowPosition" }),
            ("NOSE", new[] { "noseSize", "noseWidth", "noseCurve", "noseFlatten", "nosePronounced", "nosePosition", "noseInclination" }),
            ("MOUTH & EARS", new[] { "mouthSize", "lipsSize", "earsSize", "earsRotation" }),
        };

        /// <summary>The brow row's way back to the hair's colour.</summary>
        public const string MatchHairCaption = "Match hair color";

        // Skin, hair, brows and eyes come from CharacterPalettes; garments keep their own.
        private static readonly string[] FabricSwatches = { "F2F2EE", "1E2126", "5A5F69", "1E3A66", "3F78C8", "2F6B4A", "8A2F2F", "C8553A", "D9A441", "E7C9A6", "6B4A8C", "D86FA6" };

        /// <summary>How often Randomize puts something in each accessory slot: about one roll in three.</summary>
        private const double AccessoryOdds = .3;

        private void Update()
        {
            RefitToFrame();
            RebuildIfAsked();
            if (previewStatus != null && studioPreview != null) previewStatus.text = studioPreview.Status;
            if (retryPreviewButton != null && studioPreview != null) retryPreviewButton.interactable = studioPreview.CanRetry;
        }

        private void OnDestroy()
        { if (studioPreview != null) Destroy(studioPreview.gameObject); }

        // ---------------------------------------------------------------- the page

        private void BuildAppearancePage(RectTransform scrim)
        {
            lastStudioCategory = appearanceCategory;
            draft.Appearance = draft.Appearance ?? new CharacterAppearance();
            if (Catalog != null) draft.Appearance = Catalog.Materialize(draft.Appearance);
            var area = PageArea;
            float sidebar = area.width >= 1640f ? 250f : 214f;
            float panel = Mathf.Clamp(area.width * .46f, 600f, 880f);
            float preview = area.width - sidebar - panel - 2f * Gap;
            BuildCategories(scrim, new Rect(area.x, area.y, sidebar, area.height));
            BuildPreviewStage(scrim, new Rect(area.x + sidebar + Gap, area.y, preview, area.height), true);
            BuildAppearancePanel(scrim, new Rect(area.x + sidebar + Gap + preview + Gap, area.y, panel, area.height));
        }

        private void BuildCategories(RectTransform scrim, Rect r)
        {
            var side = Panel("Appearance categories", scrim, r);
            Words(side, "CREATE YOUR", 14f, UiTheme.Accent, new Rect(22f, 22f, r.width - 44f, 20f)).characterSpacing = 3f;
            var title = Words(side, "HOUSEGUEST", 24f, UiTheme.Paper, new Rect(22f, 42f, r.width - 44f, 32f));
            var bold = UiTheme.Font(UiTheme.Weight.Bold); if (bold != null) title.font = bold;
            // The tiles share what height the panel has: at a fixed 68 apart the sixth hung past the
            // panel's foot on a short wide screen (21:9 with larger text, 32:9 at any size).
            float y = 96f, pitch = Mathf.Min(68f, (r.height - y - 8f) / Categories.Length), tileHeight = pitch - 8f;
            foreach (var category in Categories)
            {
                string pick = category;
                bool on = appearanceCategory == category;
                var tile = HudPrimitives.Fill(category, side, new Color(0f, 0f, 0f, 0f), 10);
                Place(tile, 10f, y, r.width - 20f, tileHeight);
                var image = tile.GetComponent<Image>(); image.raycastTarget = true;
                if (on)
                {
                    if (!UiTheme.PackSliced(image, PackArt.CreatorCategoryTile, 12f)) image.color = UiTheme.SurfaceRaised;
                    else image.color = new Color(.45f, .7f, 1f, 1f);
                    UiTheme.AddGlow(tile, 12);
                    var stripe = HudPrimitives.Fill("Selected stripe", tile, UiTheme.Glow, 2);
                    float stripeHeight = Mathf.Max(0f, tileHeight - 20f);
                    Place(stripe, 0f, (tileHeight - stripeHeight) * .5f, 4f, stripeHeight);
                }
                var label = HudPrimitives.Label("Label", tile, 19f, on ? Color.white : UiTheme.Paper, TextAlignmentOptions.MidlineLeft);
                label.text = Localisation.Text(category);
                var weight = UiTheme.Font(on ? UiTheme.Weight.SemiBold : UiTheme.Weight.Medium); if (weight != null) label.font = weight;
                Place(label.rectTransform, 62f, 0f, r.width - 90f, tileHeight);
                Glyph(tile, CategoryIcon(category), on ? UiTheme.Glow : UiTheme.Accent, new Rect(18f, (tileHeight - 30f) * .5f, 30f, 30f));
                var button = tile.gameObject.AddComponent<Button>();
                button.targetGraphic = image;
                button.onClick.AddListener(() => { appearanceCategory = pick; Rebuild(); });
                Seen(button, 10);
                y += pitch;
            }
            if (r.height - y > 150f)
            {
                var quote = Words(side, "“Same house.\nDifferent stories.\nMake them yours.”", 17f, UiTheme.Paper,
                    new Rect(22f, r.height - 150f, r.width - 40f, 90f));
                quote.fontStyle = FontStyles.Italic;
                Words(side, "— GAMESIM", 14f, UiTheme.Heading, new Rect(22f, r.height - 54f, r.width - 40f, 22f), TextAlignmentOptions.TopRight);
            }
        }

        private static string CategoryIcon(string category)
        {
            switch (category)
            {
                case "Body": return "houseguest";
                case "Face": return "mood-happy";
                case "Hair": return "mood-confident";
                case "Clothing": return PackArt.KitIconArchive;
                case "Accessories": return "mood-playful";
                default: return "star";
            }
        }

        /// <summary>
        /// The live preview on its platform: a pool of light, the lit ring the mockups stand the
        /// houseguest on with the feet on its middle, the model over it, and the turntable on the
        /// ring's front edge. <paramref name="full"/> adds the camera's other views and the
        /// comparison with the look the edit began from.
        /// </summary>
        private void BuildPreviewStage(RectTransform scrim, Rect r, bool full)
        {
            EnsureStudio();
            var stage = new GameObject("Preview stage", typeof(RectTransform)).GetComponent<RectTransform>();
            stage.SetParent(scrim, false);
            Place(stage, r.x, r.y, r.width, r.height);
            float chipsY = r.height - 36f;
            float turnY = full ? chipsY - 12f - 52f : r.height - 52f;
            float ringWidth = Mathf.Min(r.width * .86f, 480f), ringHeight = Mathf.Min(ringWidth * .24f, 110f);
            float floor = turnY - 6f;
            float ringX = (r.width - ringWidth) * .5f, ringY = floor - ringHeight * .5f;

            Backdrop("Platform light", stage, UiTheme.Pack(PackArt.GlowCyan) ?? UiTheme.Circle(),
                new Color(UiTheme.Glow.r, UiTheme.Glow.g, UiTheme.Glow.b, .5f),
                new Rect(ringX - ringWidth * .12f, ringY - ringHeight * .9f, ringWidth * 1.24f, ringHeight * 2.6f));
            Backdrop("Platform floor", stage, UiTheme.Circle(), new Color(UiTheme.AccentDeep.r, UiTheme.AccentDeep.g, UiTheme.AccentDeep.b, .28f),
                new Rect(ringX + 8f, ringY + 6f, ringWidth - 16f, ringHeight - 12f));
            Backdrop("Platform ring", stage, UiTheme.Pack(PackArt.SelectionRing) ?? UiTheme.Ring(), UiTheme.Glow,
                new Rect(ringX, ringY, ringWidth, ringHeight));
            Backdrop("Platform edge", stage, UiTheme.Ring(), new Color(1f, 1f, 1f, .55f),
                new Rect(ringX + ringWidth * .05f, ringY + ringHeight * .08f, ringWidth * .9f, ringHeight * .84f));

            var region = new GameObject("Preview image region", typeof(RectTransform)).GetComponent<RectTransform>();
            region.SetParent(stage, false);
            Place(region, 0f, 0f, r.width, floor + 4f);
            var image = new GameObject("Live character preview", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            image.transform.SetParent(region, false);
            image.texture = studioPreview.Texture; image.raycastTarget = false;
            // The studio's 4:5 picture, as tall as the region allows and standing on its floor:
            // fitted and centred, it floated above the ring whenever the region was wider than it.
            float pictureHeight = Mathf.Min(floor + 4f, r.width / .8f), pictureWidth = pictureHeight * .8f;
            image.rectTransform.anchorMin = image.rectTransform.anchorMax = new Vector2(.5f, 0f);
            image.rectTransform.pivot = new Vector2(.5f, 0f);
            image.rectTransform.sizeDelta = new Vector2(pictureWidth, pictureHeight);
            // The studio leaves a margin under the feet; take it back so they meet the ring.
            image.rectTransform.anchoredPosition = new Vector2(0f, -pictureHeight * .05f);

            if (full && r.width >= 460f)
            {
                var side = Words(stage, "REAL\nPLAYERS\nBIGGER\nPOSSIBILITIES", 15f, new Color(UiTheme.Accent.r, UiTheme.Accent.g, UiTheme.Accent.b, .8f),
                    new Rect(8f, 70f, 200f, 120f));
                side.characterSpacing = 4f; side.lineSpacing = 8f;
                var neon = Words(stage, "Same House.\nDifferent Stories.", 30f, UiTheme.Hex("FF7AD9"),
                    new Rect(r.width - 250f, 80f, 240f, 110f), TextAlignmentOptions.Center, "Preview neon");
                neon.fontStyle = FontStyles.Italic; neon.lineSpacing = -20f;
                var semibold = UiTheme.Font(UiTheme.Weight.SemiBold); if (semibold != null) neon.font = semibold;
                neon.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 8f);
            }

            // The turntable: the arrows either side of its name, on the ring's front edge.
            var turntable = HudPrimitives.Fill("Turntable", stage, new Color(UiTheme.GlassFill.r, UiTheme.GlassFill.g, UiTheme.GlassFill.b, .92f), 26);
            Place(turntable, r.width * .5f - 150f, turnY, 300f, 52f);
            UiTheme.AddBorder(turntable, 26, UiTheme.Edge(UiTheme.Emphasis.Interactive));
            GlyphButton(turntable, "Rotate left", new Rect(4f, 4f, 44f, 44f), null, UiTheme.PlayMark(), () => studioPreview.Rotate(-30f))
                .transform.Find("Glyph").localRotation = Quaternion.Euler(0f, 0f, 180f);
            GlyphButton(turntable, "Rotate right", new Rect(252f, 4f, 44f, 44f), null, UiTheme.PlayMark(), () => studioPreview.Rotate(30f));
            Words(turntable, "Rotate", 17f, UiTheme.Paper, new Rect(50f, 0f, 200f, 52f), TextAlignmentOptions.Center);
            previewStatus = Words(stage, studioPreview.Status, 13f, UiTheme.Muted, new Rect(8f, 8f, r.width * .6f, 20f),
                TextAlignmentOptions.TopLeft, "Preview status");
            retryPreviewButton = Pill(stage, "Retry preview", new Rect(r.width - 132f, 2f, 132f, 30f), Tone.Quiet,
                () => studioPreview.Retry(), size: 13f);
            retryPreviewButton.interactable = studioPreview.CanRetry;
            if (!full) return;
            // The camera's other views, and the look the edit began from. Zoom is two glyphs whose
            // captions are their names: as words the pair did not fit a narrow stage.
            float zoom = 36f, chip = Mathf.Min(106f, (r.width - 16f - 2f * zoom - 4f * 6f) / 3.7f);
            string compare = comparingOriginal ? "Return to edited look" : "Compare original";
            float x = (r.width - (3.7f * chip + 2f * zoom + 4f * 6f)) * .5f;
            Pill(stage, "Front", new Rect(x, chipsY, chip, 36f), Tone.Quiet, () => studioPreview.View(0f), size: 14f);
            x += chip + 6f;
            Pill(stage, "Side", new Rect(x, chipsY, chip, 36f), Tone.Quiet, () => studioPreview.View(90f), size: 14f);
            x += chip + 6f;
            GlyphButton(stage, "Zoom out", new Rect(x, chipsY, zoom, zoom), "\u2212", null, () => studioPreview.Zoom(-.1f));
            x += zoom + 6f;
            GlyphButton(stage, "Zoom in", new Rect(x, chipsY, zoom, zoom), "+", null, () => studioPreview.Zoom(.1f));
            x += zoom + 6f;
            Pill(stage, compare, new Rect(x, chipsY, chip * 1.7f, 36f), comparingOriginal ? Tone.Selected : Tone.Quiet,
                () => { comparingOriginal = !comparingOriginal; Rebuild(); }, size: 14f);
        }

        /// <summary>The live preview, awake and showing the look on the draft (or the original being compared).</summary>
        private void EnsureStudio()
        {
            if (studioPreview == null) { studioPreview = CharacterStudioPreview.Create(); studioPreview.Transparent = true; studioFace = null; }
            studioPreview.gameObject.SetActive(true);
            previewOnPage = true;
            // The close-up belongs to the face and hair, and the comparison with the original to the
            // page that offers it: every other page shows the whole houseguest as they are now.
            bool face = studioPage == "Appearance" && (appearanceCategory == "Face" || appearanceCategory == "Hair" || appearanceCategory == "Accessories");
            if (studioFace != face) { studioFace = face; studioPreview.FocusFace(face); }
            bool original = comparingOriginal && studioPage == "Appearance";
            studioPreview.Show(original ? initialAppearance ?? new CharacterAppearance() : draft.Appearance);
        }

        /// <summary>A picture laid behind the stage's other pieces, taking no clicks.</summary>
        private static void Backdrop(string name, Transform parent, Sprite sprite, Color tint, Rect r)
        {
            var art = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            art.rectTransform.SetParent(parent, false);
            art.sprite = sprite; art.color = tint; art.raycastTarget = false;
            Place(art.rectTransform, r.x, r.y, r.width, r.height);
        }

        private void BuildAppearancePanel(RectTransform scrim, Rect r)
        {
            var box = Panel("Appearance panel", scrim, r, true);
            float inner = r.width - 48f;
            var heading = Words(box, "APPEARANCE", 24f, UiTheme.Glow, new Rect(24f, 20f, 200f, 34f));
            var bold = UiTheme.Font(UiTheme.Weight.Bold); if (bold != null) heading.font = bold;
            heading.characterSpacing = 1.5f;
            // Undo, Redo, Randomize and Reset, at the head of the panel as the mockup sets them.
            float actionWidth = Mathf.Min(150f, (inner - 176f) / 4f - 8f), ax = r.width - 24f - 4f * (actionWidth + 8f) + 8f;
            bool glyphs = actionWidth >= 132f;
            undoAppearanceButton = Pill(box, "Undo", new Rect(ax, 18f, actionWidth, 40f), Tone.Secondary, UndoAppearance,
                icon: glyphs ? PackArt.KitIconArrowBack : null, size: 15f);
            redoAppearanceButton = Pill(box, "Redo", new Rect(ax + actionWidth + 8f, 18f, actionWidth, 40f), Tone.Secondary, RedoAppearance,
                icon: glyphs ? PackArt.KitIconChevronRight : null, size: 15f);
            Pill(box, "Randomize", new Rect(ax + 2f * (actionWidth + 8f), 18f, actionWidth, 40f), Tone.Secondary, RandomizeAppearance,
                icon: glyphs ? PackArt.KitIconRefresh : null, size: 15f);
            Pill(box, "Reset look", new Rect(ax + 3f * (actionWidth + 8f), 18f, actionWidth, 40f), Tone.Secondary,
                () => ChangeAppearance(() => draft.Appearance = initialAppearance?.Clone() ?? new CharacterAppearance()),
                icon: glyphs ? PackArt.KitIconRefresh : null, size: 15f);
            undoAppearanceButton.interactable = appearanceUndo.Count > 0;
            redoAppearanceButton.interactable = appearanceRedo.Count > 0;
            Words(box, "Customize your look. Your appearance is independent of pronouns, personality and competition stats.", 15f, UiTheme.Muted,
                new Rect(24f, 64f, inner, 42f)).textWrappingMode = TextWrappingModes.Normal;

            var viewport = HudPrimitives.Fill("Appearance controls viewport", box, Color.clear, 1);
            Place(viewport, 12f, 110f, r.width - 24f, r.height - 122f);
            viewport.gameObject.AddComponent<RectMask2D>();
            studioControls = new GameObject("Appearance controls", typeof(RectTransform)).GetComponent<RectTransform>();
            studioControls.SetParent(viewport, false);
            studioControls.anchorMin = new Vector2(0f, 1f); studioControls.anchorMax = Vector2.one;
            studioControls.pivot = new Vector2(.5f, 1f);
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            viewport.gameObject.AddComponent<SetupScrollFocus>();
            scroll.content = studioControls; scroll.viewport = viewport; scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 40f;
            studioCursor = 4f; studioWidth = r.width - 24f;

            if (Catalog == null)
            {
                StudioText("This installation supports complete preset bodies. Modular controls appear when compatible character content is available.", 60f);
                StartingLooks();
                FinishStudio();
                return;
            }
            if (!string.IsNullOrEmpty(appearanceNotice))
                StudioText(appearanceNotice, 30f + appearanceNotice.Count(value => value == '\n') * 22f, UiTheme.Accent);
            switch (appearanceCategory)
            {
                case "Body":
                    // The whole look first, as the studio opens on it: a cast member's, or yours.
                    StartingLooks();
                    BodyChoices();
                    Sliders("Body");
                    break;
                case "Face":
                    StartingLooks();
                    // Freckles, makeup, an older face: the face's own texture, worn on every outfit.
                    WardrobeStrip("Face");
                    FaceSliders();
                    break;
                case "Accessories":
                    OutfitSelector();
                    foreach (var slot in ProceduralAccessories.Slots) WardrobeStrip(slot);
                    StudioText("Each piece is fitted to the face and build you have made, and worn with this outfit.", 26f);
                    break;
                case "Hair":
                    WardrobeStrip("Hair"); WardrobeStrip("Eyebrows"); WardrobeStrip("Beard");
                    SwatchGrid("HAIR COLOR", "Hair", CharacterPalettes.Hair, AppearanceEditing.ColorValue(draft.Appearance, "Hair", Color.clear), SetHairColour);
                    StudioText("The brows take the hair color. Give them one of their own under Colors.", 26f);
                    break;
                case "Clothing":
                    OutfitSelector();
                    var shown = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var slot in AppearanceEditing.FabricSlots)
                    {
                        WardrobeStrip(slot);
                        GarmentColour(slot, shown);
                    }
                    WardrobeStrip("TopUnderlayer"); WardrobeStrip("BottomUnderlayer");
                    // Anything else worn with a colour of its own.
                    foreach (string channel in AppearanceEditing.Outfit(draft.Appearance).wardrobe.SelectMany(worn => GarmentChannels(worn.itemId)).ToList())
                        if (shown.Add(channel))
                            SwatchRow(channel.ToUpperInvariant(), channel, FabricSwatches, OutfitChannel(channel), color => SetOutfitChannel(channel, color));
                    break;
                default:
                    SwatchGrid("SKIN TONE", "Skin", CharacterPalettes.Skin, AppearanceEditing.ColorValue(draft.Appearance, "Skin", Color.clear),
                        color => AppearanceEditing.SetColor(draft.Appearance, "Skin", color));
                    // Two lines at the narrowest panel: the rows' own labels already say which depth is which.
                    StudioText("A row per depth, each mixing undertones: golden, neutral, olive, rosy or red. "
                        + "Point at a tone, or move to it, to see its name.", 40f);
                    SwatchGrid("HAIR COLOR", "Hair", CharacterPalettes.Hair, AppearanceEditing.ColorValue(draft.Appearance, "Hair", Color.clear), SetHairColour);
                    // Only the natural shades to pick from, but named from every hair colour: a dyed
                    // colour the hair handed on is a swatch, not a custom colour nobody chose.
                    var hair = AppearanceEditing.ColorValue(draft.Appearance, "Hair", Color.clear);
                    SwatchGrid("BROW COLOR", "Brows", new[] { CharacterPalettes.Hair[0] }, AppearanceEditing.ColorValue(draft.Appearance, "Brows", Color.clear),
                        color => AppearanceEditing.SetColor(draft.Appearance, "Brows", color), MatchHairCaption,
                        () => AppearanceEditing.SetColor(draft.Appearance, "Brows", AppearanceEditing.ColorValue(draft.Appearance, "Hair", Color.black)),
                        brows => hair.a > 0f && SameColour(brows, hair) ? "Matches hair" : CharacterPalettes.NameOf(CharacterPalettes.Hair, brows));
                    StudioText("A hair color sets the brows to match; pick a brow color after it to set them apart.", 26f);
                    SwatchGrid("EYE COLOR", "Eyes", CharacterPalettes.Eyes, AppearanceEditing.ColorValue(draft.Appearance, "Eyes", Color.clear),
                        color => AppearanceEditing.SetColor(draft.Appearance, "Eyes", color));
                    StudioText("Garment colors are under Clothing.", 22f);
                    break;
            }
            CategoryTools();
            FinishStudio();
        }

        /// <summary>
        /// The category's own reset, and its lock: Randomize leaves a locked category as it is.
        /// </summary>
        private void CategoryTools()
        {
            string category = appearanceCategory.ToLowerInvariant();
            bool locked = randomLocks.Contains(appearanceCategory);
            float width = (studioWidth - 24f - 12f) * .5f;
            studioCursor += 6f;
            Pill(studioControls, "Reset " + category, new Rect(12f, studioCursor, width, 40f), Tone.Quiet, ResetAppearanceCategory,
                icon: PackArt.KitIconRefresh, size: 14f);
            // Named for what it is, not what it says: the caption flips, and keyboard focus is kept by name.
            Pill(studioControls, (locked ? "Unlock " : "Lock ") + appearanceCategory, new Rect(24f + width, studioCursor, width, 40f),
                locked ? Tone.Selected : Tone.Quiet, () =>
                {
                    if (!randomLocks.Add(appearanceCategory)) randomLocks.Remove(appearanceCategory);
                    Rebuild();
                }, icon: PackArt.KitIconLock, size: 14f).name = "Category lock";
            studioCursor += 46f;
            StudioText(locked ? "Randomize keeps this category as it is." : "Lock a category to keep it when you Randomize.", 22f);
        }

        private void FinishStudio()
        {
            studioControls.sizeDelta = new Vector2(0f, studioCursor + 16f);
            studioControls.anchoredPosition = new Vector2(0f, studioScrollY);
        }

        /// <summary>A hair colour, and the brows with it: most brow styles follow the hair anyway, and
        /// the rest would otherwise keep a starting look's colour nobody chose.</summary>
        private void SetHairColour(Color color)
        {
            AppearanceEditing.SetColor(draft.Appearance, "Hair", color);
            AppearanceEditing.SetColor(draft.Appearance, "Brows", color);
        }

        /// <summary>A garment's own shared colours - not the body's, which have their own rows.</summary>
        private IEnumerable<string> GarmentChannels(string itemId) =>
            (AppearanceEditing.Find(Catalog, itemId)?.ColorChannels ?? Array.Empty<string>())
                .Where(id => id != "Skin" && id != "Hair" && id != "Eyes" && id != "Brows");

        /// <summary>
        /// The worn garment's colour, under its styles. A garment of the house's own fabric takes a
        /// tint of its own; one built on shared colours offers those instead, because a fabric tint
        /// would not reach it.
        /// </summary>
        private void GarmentColour(string slot, HashSet<string> shown)
        {
            var outfit = AppearanceEditing.Outfit(draft.Appearance);
            var worn = outfit.wardrobe.FirstOrDefault(item => item.slot == slot);
            if (worn == null) return;
            string name = SlotLabel(slot);
            var channels = GarmentChannels(worn.itemId).ToList();
            if (channels.Count > 0)
            {
                for (int i = 0; i < channels.Count; i++)
                {
                    string channel = channels[i];
                    if (!shown.Add(channel)) continue;
                    SwatchRow(name.ToUpperInvariant() + " COLOR" + (channels.Count > 1 ? " " + (i + 1) : ""), channel, FabricSwatches,
                        OutfitChannel(channel), color => SetOutfitChannel(channel, color));
                }
                return;
            }
            bool tinted = AppearanceEditing.TryFabric(outfit, slot, out var tint);
            // A starting look's garment is dyed the colour in its photo, which is seldom one of the
            // swatches. That colour joins the end of the row, and so does a worn one off the palette:
            // the row rings what is worn, and the photo's colour can be picked again after trying
            // another rather than only by Undo or by resetting the clothes.
            var palette = FabricSwatches.ToList();
            void Offer(Color colour)
            {
                if (!palette.Any(hex => SameColour(Hex(hex), colour))) palette.Add(ColorUtility.ToHtmlStringRGB(colour));
            }
            if (StartingFabric(slot, out var photo)) Offer(photo);
            if (tinted) Offer(tint);
            SwatchRow(name.ToUpperInvariant() + " COLOR", "Fabric " + slot, palette.ToArray(), tinted ? tint : Color.clear,
                color => AppearanceEditing.SetFabric(draft.Appearance, slot, color),
                tinted ? (Action)(() => AppearanceEditing.SetFabric(draft.Appearance, slot, null)) : null,
                "Original " + name.ToLowerInvariant() + " color");
        }

        /// <summary>
        /// The tint the look began with on <paramref name="slot"/>'s garment, in the outfit being
        /// edited - or, for an outfit made since, in the one the look began in.
        /// </summary>
        private bool StartingFabric(string slot, out Color tint)
        {
            tint = Color.clear;
            if (initialAppearance == null || Catalog == null) return false;
            var original = Catalog.Materialize(initialAppearance);
            string editing = draft.Appearance?.activeOutfit;
            var set = original.outfits.FirstOrDefault(item => item.id == editing)
                ?? original.outfits.FirstOrDefault(item => item.id == original.activeOutfit);
            return AppearanceEditing.TryFabric(set, slot, out tint);
        }

        private Color OutfitChannel(string channel)
        {
            var entry = AppearanceEditing.Outfit(draft.Appearance).colors.FirstOrDefault(item => item.id == channel);
            return entry == null ? Color.clear : new Color(entry.r, entry.g, entry.b, 1f);
        }

        private void SetOutfitChannel(string channel, Color color)
        {
            var outfit = AppearanceEditing.Outfit(draft.Appearance);
            outfit.colors.RemoveAll(entry => entry.id == channel);
            outfit.colors.Add(new AppearanceColor { id = channel, r = color.r, g = color.g, b = color.b, a = 1f });
        }

        // ---------------------------------------------------------------- the category's choices

        /// <summary>A sub-heading in the panel, the mockup's "BODY" / "HAIR" line.</summary>
        private void StudioHeading(string title, string detail = null)
        {
            var label = Words(studioControls, title, 17f, UiTheme.Glow, new Rect(12f, studioCursor, studioWidth * .6f, 26f));
            var bold = UiTheme.Font(UiTheme.Weight.Bold); if (bold != null) label.font = bold;
            label.characterSpacing = 2f;
            if (detail != null)
                Words(studioControls, detail, 14f, UiTheme.Muted, new Rect(studioWidth * .4f, studioCursor + 2f, studioWidth * .6f - 12f, 24f),
                    TextAlignmentOptions.TopRight, "Heading detail").overflowMode = TextOverflowModes.Ellipsis;
            studioCursor += 32f;
        }

        private void StudioText(string value, float height, Color? colour = null)
        {
            var label = Words(studioControls, value, 14f, colour ?? UiTheme.Muted, new Rect(12f, studioCursor, studioWidth - 24f, height), name: "Studio label");
            label.textWrappingMode = TextWrappingModes.Normal;
            studioCursor += height + 4f;
        }

        private void BodyChoices()
        {
            StudioHeading("BODY", "Changing body replaces clothes that do not fit");
            var current = Catalog.Materialize(draft.Appearance).bodyId;
            float size = 112f, x = 12f;
            foreach (var body in Catalog.Bodies)
            {
                var option = body;
                var card = Thumbnail(studioControls, option.Label, option.Label, new Rect(x, studioCursor, size, size + 18f), current == option.Id,
                    null, null, "houseguest", () => ChangeAppearance(() =>
                    {
                        var changed = ChangeBody(draft.Appearance, option.Id);
                        appearanceNotice = AppearanceEditing.DescribeSubstitutions(draft.Appearance, changed, Catalog);
                        draft.Appearance = changed;
                    }));
                x += size + 12f;
            }
            studioCursor += size + 30f;
        }

        /// <summary>
        /// The catalog's change of body, except that a face detail the new body has nothing of the
        /// kind for comes off rather than turning into another. Makeup is drawn for the female body
        /// alone, and the catalog's nearest to it on the male body was an older face nobody chose;
        /// an older face still becomes the other body's older face.
        /// </summary>
        private CharacterAppearance ChangeBody(CharacterAppearance appearance, string bodyId)
        {
            var changed = Catalog.ChangeBody(appearance, bodyId);
            foreach (var outfit in changed.outfits)
            {
                var now = outfit.wardrobe.FirstOrDefault(worn => worn.slot == "Face");
                var was = appearance.outfits.FirstOrDefault(set => set.id == outfit.id)?.wardrobe.FirstOrDefault(worn => worn.slot == "Face");
                if (now == null || was == null || now.itemId == was.itemId) continue;
                if (AppearanceEditing.Find(Catalog, now.itemId)?.StyleGroup != AppearanceEditing.Find(Catalog, was.itemId)?.StyleGroup)
                    outfit.wardrobe.Remove(now);
            }
            return changed;
        }

        /// <summary>The proportion or feature sliders for a category, two to a row as the mockup lays them.</summary>
        private void Sliders(string category)
        {
            var controls = Catalog.Controls.Where(control => control.Category == category && control.Fits(draft.Appearance.bodyId)).ToList();
            if (controls.Count == 0) return;
            StudioHeading(category == "Body" ? "PROPORTIONS" : "FACE");
            float column = (studioWidth - 24f - 28f) * .5f;
            for (int i = 0; i < controls.Count; i++)
            {
                float x = 12f + (i % 2) * (column + 28f);
                ControlSlider(controls[i], new Rect(x, studioCursor, column, 62f));
                if (i % 2 == 1 || i == controls.Count - 1) studioCursor += 70f;
            }
        }

        /// <summary>The face's sliders, grouped under headings: shape, cheeks, eyes and brows, nose, mouth and ears.</summary>
        private void FaceSliders()
        {
            var controls = Catalog.Controls.Where(control => control.Category == "Face" && control.Fits(draft.Appearance.bodyId)).ToList();
            var placed = new HashSet<string>(StringComparer.Ordinal);
            float column = (studioWidth - 24f - 28f) * .5f;
            for (int g = 0; g < FaceGroups.Length; g++)
            {
                var group = FaceGroups[g].ids.Select(id => controls.FirstOrDefault(control => control.Id == id)).Where(control => control != null).ToList();
                if (g == FaceGroups.Length - 1) group.AddRange(controls.Where(control => !placed.Contains(control.Id) && !group.Contains(control)
                    && !FaceGroups.Any(other => other.ids.Contains(control.Id))));
                if (group.Count == 0) continue;
                StudioHeading(FaceGroups[g].title);
                for (int i = 0; i < group.Count; i++)
                {
                    placed.Add(group[i].Id);
                    float x = 12f + (i % 2) * (column + 28f);
                    ControlSlider(group[i], new Rect(x, studioCursor, column, 62f));
                    if (i % 2 == 1 || i == group.Count - 1) studioCursor += 70f;
                }
                studioCursor += 4f;
            }
        }

        /// <summary>
        /// The starting looks - every cast member's look, and the player's - as portraits to pick from,
        /// the arrows stepping through them one at a time.
        /// </summary>
        private void StartingLooks()
        {
            var ids = PresetIds();
            int current = Math.Max(0, ids.IndexOf(draft.Appearance?.presetId));
            StudioHeading("STARTING LOOK", "Replaces the whole look; Undo restores it");
            float arrow = 44f, face = 86f, gap = 10f;
            int visible = Mathf.Max(1, Mathf.FloorToInt((studioWidth - 24f - 2f * (arrow + gap) + gap) / (face + gap)));
            int first = Mathf.Clamp(current - visible / 2, 0, Mathf.Max(0, ids.Count - visible));
            GlyphButton(studioControls, "Previous starting look", new Rect(12f, studioCursor + (face - arrow) * .5f, arrow, arrow), null, UiTheme.PlayMark(),
                () => CyclePreset(-1)).transform.Find("Glyph").localRotation = Quaternion.Euler(0f, 0f, 180f);
            float x = 12f + arrow + gap;
            for (int i = first; i < Math.Min(ids.Count, first + visible); i++)
            {
                string id = ids[i];
                var template = CastTemplates.Find(id);
                string label = template != null ? template.Name.Split(' ')[0] : "You";
                Thumbnail(studioControls, "Starting look " + label, label, new Rect(x, studioCursor, face, face + 18f), i == current, null,
                    null, "houseguest", () => ChangeAppearance(() => draft.Appearance = Catalog?.Materialize(new CharacterAppearance { presetId = id })
                        ?? new CharacterAppearance { presetId = id }), template != null ? CastTemplates.ToContestant(template, false) : null);
                x += face + gap;
            }
            GlyphButton(studioControls, "Next starting look", new Rect(studioWidth - 12f - arrow, studioCursor + (face - arrow) * .5f, arrow, arrow), null,
                UiTheme.PlayMark(), () => CyclePreset(1));
            studioCursor += face + 34f;
        }

        private static List<string> PresetIds()
        {
            var ids = new List<string> { ContentCatalog.PlayerId };
            foreach (CastTemplates.Roster roster in Enum.GetValues(typeof(CastTemplates.Roster)))
                ids.AddRange(CastTemplates.In(roster).Select(template => template.Id));
            return ids;
        }

        private static string SlotLabel(string slot) => slot == "TopUnderlayer" ? "Underwear top" : slot == "BottomUnderlayer" ? "Underwear bottoms"
            : slot == "Chest" ? "Top" : slot == "Legs" ? "Bottoms" : slot == "Feet" ? "Shoes" : slot == "Beard" ? "Facial hair"
            : slot == "Face" ? "Face details" : slot;

        /// <summary>
        /// A wardrobe slot as a strip of thumbnails around the one worn, the arrows wearing the style
        /// before or after it; the name of what is worn, and Remove, above it.
        /// </summary>
        private void WardrobeStrip(string slot)
        {
            string slotLabel = SlotLabel(slot);
            var appearance = Catalog.Materialize(draft.Appearance);
            var choices = Catalog.Items.Where(item => item.Slot == slot && item.Fits(appearance.bodyId)).ToList();
            if (choices.Count == 0) return;
            var selected = AppearanceEditing.Outfit(appearance).wardrobe.FirstOrDefault(item => item.slot == slot);
            int index = choices.FindIndex(item => item.Matches(selected?.itemId));
            string worn = index >= 0 ? choices[index].Label : selected == null ? "None" : "Saved item unavailable";
            StudioHeading(slotLabel.ToUpperInvariant(), worn + "  ·  " + choices.Count + " styles");
            bool compact = slot == "TopUnderlayer" || slot == "BottomUnderlayer" || slot == "Eyebrows" || slot == "Beard" || slot == "Face";
            float arrow = 40f, thumb = compact ? 70f : 84f, gap = 8f, removeWidth = arrow;
            int visible = Mathf.Max(1, Mathf.FloorToInt((studioWidth - 24f - removeWidth - 10f - 2f * (arrow + gap) + gap) / (thumb + gap)));
            int first = Mathf.Clamp((index < 0 ? 0 : index) - visible / 2, 0, Mathf.Max(0, choices.Count - visible));
            float rowY = studioCursor, centre = rowY + (thumb + 16f - arrow) * .5f;
            var previous = GlyphButton(studioControls, "Previous " + slotLabel.ToLowerInvariant(), new Rect(12f, centre, arrow, arrow), null, UiTheme.PlayMark(),
                () => ChangeAppearance(() => AppearanceEditing.Wear(draft.Appearance, choices[(index - 1 + choices.Count) % choices.Count], Catalog)));
            previous.name = "Previous " + slot.ToLowerInvariant();
            previous.transform.Find("Glyph").localRotation = Quaternion.Euler(0f, 0f, 180f);
            float x = 12f + arrow + gap;
            for (int i = first; i < Math.Min(choices.Count, first + visible); i++)
            {
                var item = choices[i];
                Thumbnail(studioControls, "Wear " + item.Label, item.Label, new Rect(x, rowY, thumb, thumb + 16f), i == index, item.Thumbnail, null,
                    slot == "Hair" || slot == "Eyebrows" || slot == "Beard" || slot == "Face" ? "mood-playful" : PackArt.KitIconArchive,
                    () => ChangeAppearance(() => AppearanceEditing.Wear(draft.Appearance, item, Catalog)));
                x += thumb + gap;
            }
            float nextX = 12f + arrow + gap + visible * (thumb + gap);
            GlyphButton(studioControls, "Next " + slotLabel.ToLowerInvariant(), new Rect(nextX, centre, arrow, arrow), null, UiTheme.PlayMark(),
                () => ChangeAppearance(() => AppearanceEditing.Wear(draft.Appearance, choices[(index + 1) % choices.Count], Catalog)))
                .name = "Next " + slot.ToLowerInvariant();
            var remove = GlyphButton(studioControls, "Remove " + slotLabel.ToLowerInvariant(), new Rect(studioWidth - 12f - removeWidth, centre, removeWidth, arrow),
                null, UiTheme.Pack(PackArt.KitIconCross), () => ChangeAppearance(() =>
                {
                    // What belongs to the person comes off every outfit, as Wear puts it on every
                    // outfit: off one alone, changing clothes would put the freckles back.
                    var sets = AppearanceEditing.IsCharacterSlot(slot) ? draft.Appearance.outfits
                        : new List<CharacterOutfit> { AppearanceEditing.Outfit(draft.Appearance) };
                    foreach (var set in sets) set.wardrobe.RemoveAll(item => item.slot == slot);
                }));
            remove.name = "Remove " + slot.ToLowerInvariant();
            Enable(remove, selected != null);
            studioCursor += thumb + 30f;
        }

        private void OutfitSelector()
        {
            StudioHeading("OUTFIT", "Editing: " + (draft.Appearance?.activeOutfit ?? "Everyday"));
            var options = new[] { "Everyday", "Competition", "Formal", "Sleepwear", "Swimwear" };
            float width = (studioWidth - 24f - 4f * 8f) / options.Length;
            for (int i = 0; i < options.Length; i++)
            {
                string option = options[i];
                Pill(studioControls, option, new Rect(12f + i * (width + 8f), studioCursor, width, 40f),
                    draft.Appearance?.activeOutfit == option ? Tone.Selected : Tone.Secondary, () => ChangeAppearance(() =>
                    {
                        var current = AppearanceEditing.Outfit(draft.Appearance);
                        if (!draft.Appearance.outfits.Any(outfit => outfit.id == option))
                        {
                            var copy = current.Clone(); copy.id = option; draft.Appearance.outfits.Add(copy);
                        }
                        draft.Appearance.activeOutfit = option;
                    }), size: 15f);
            }
            studioCursor += 52f;
        }

        /// <summary>
        /// A row of swatches for one colour, on the pack's swatch panel; the worn colour is ringed.
        /// Each is named "<paramref name="prefix"/> N". A row with a <paramref name="clear"/> action
        /// offers the garment's own colour back, at the right of its heading.
        /// </summary>
        private void SwatchRow(string title, string prefix, string[] palette, Color current, Action<Color> apply, Action clear = null,
            string clearCaption = null)
        {
            float headingY = studioCursor;
            StudioHeading(title);
            if (clear != null)
                Pill(studioControls, clearCaption ?? "Original color", new Rect(studioWidth - 12f - 200f, headingY - 4f, 200f, 30f), Tone.Quiet,
                    () => ChangeAppearance(clear), icon: PackArt.KitIconRefresh, size: 13f);
            var plate = HudPrimitives.Fill(prefix + " swatches", studioControls, UiTheme.Surface, 10);
            Place(plate, 12f, studioCursor, studioWidth - 24f, 58f);
            if (!UiTheme.PackSliced(plate.GetComponent<Image>(), PackArt.CreatorSwatchesPanel, 12f))
                UiTheme.AddBorder(plate, 10, UiTheme.Outline);
            float size = 38f, gap = Mathf.Min(14f, (studioWidth - 48f - palette.Length * size) / Math.Max(1, palette.Length - 1));
            float x = 12f;
            for (int i = 0; i < palette.Length; i++)
            {
                var colour = Hex(palette[i]);
                bool chosen = current.a > 0f && SameColour(current, colour);
                var pick = colour;
                Swatch(plate, prefix + " " + (i + 1), colour, chosen, new Rect(x, 10f, size, size), () => ChangeAppearance(() => apply(pick)));
                x += size + gap;
            }
            studioCursor += 70f;
        }

        /// <summary>
        /// A palette in its named rows - Fair &amp; light, Medium, Tan &amp; brown, Deep - on the pack's
        /// swatch panel, the worn colour ringed and named in the heading, the one under the pointer or
        /// the keyboard's focus named there while it is. Swatches keep the "<paramref name="prefix"/> N"
        /// numbering straight across the rows. An <paramref name="extra"/> action takes a pill at the
        /// heading's right. A <paramref name="describe"/> function names the worn colour instead of
        /// the rows shown, for a grid that offers fewer colours than can be worn; a colour it cannot
        /// name is "Custom".
        /// </summary>
        private void SwatchGrid(string title, string prefix, CharacterPalettes.Group[] groups, Color current, Action<Color> apply,
            string extraCaption = null, Action extra = null, Func<Color, string> describe = null)
        {
            float headingY = studioCursor;
            string worn = current.a > 0f ? (describe != null ? describe(current) : CharacterPalettes.NameOf(groups, current)) ?? "Custom" : null;
            string heading = title + (worn != null ? "  ·  " + worn.ToUpperInvariant() : "");
            var label = Words(studioControls, heading, 17f, UiTheme.Glow, new Rect(12f, studioCursor, studioWidth - 24f - (extra != null ? 212f : 0f), 26f),
                name: prefix + " heading");
            var bold = UiTheme.Font(UiTheme.Weight.Bold); if (bold != null) label.font = bold;
            label.characterSpacing = 2f; label.overflowMode = TextOverflowModes.Ellipsis;
            if (extra != null)
                Pill(studioControls, extraCaption, new Rect(studioWidth - 12f - 200f, headingY - 4f, 200f, 30f), Tone.Quiet,
                    () => ChangeAppearance(extra), icon: PackArt.KitIconRefresh, size: 13f);
            studioCursor += 32f;

            const float size = 34f, gap = 10f, pad = 12f, rowStep = size + 10f;
            float plateWidth = studioWidth - 24f;
            float labelWidth = groups.Length > 1 ? 116f : 0f;
            int perRow = Mathf.Max(1, Mathf.FloorToInt((plateWidth - 2f * pad - labelWidth + gap) / (size + gap)));
            int rows = groups.Sum(group => Mathf.CeilToInt(group.Swatches.Length / (float)perRow));
            float plateHeight = 2f * pad + rows * rowStep - 10f;
            var plate = HudPrimitives.Fill(prefix + " swatches", studioControls, UiTheme.Surface, 10);
            Place(plate, 12f, studioCursor, plateWidth, plateHeight);
            if (!UiTheme.PackSliced(plate.GetComponent<Image>(), PackArt.CreatorSwatchesPanel, 12f))
                UiTheme.AddBorder(plate, 10, UiTheme.Outline);

            int number = 0;
            float y = pad;
            foreach (var group in groups)
            {
                if (labelWidth > 0f)
                    Words(plate, group.Label, 13f, UiTheme.Muted, new Rect(pad, y, labelWidth - 8f, size), TextAlignmentOptions.MidlineLeft, "Row label")
                        .overflowMode = TextOverflowModes.Ellipsis;
                for (int i = 0; i < group.Swatches.Length; i++)
                {
                    if (i > 0 && i % perRow == 0) y += rowStep;
                    var swatch = group.Swatches[i];
                    var colour = swatch.Colour;
                    bool chosen = current.a > 0f && SameColour(current, colour);
                    var button = Swatch(plate, prefix + " " + (++number), colour, chosen,
                        new Rect(pad + labelWidth + (i % perRow) * (size + gap), y, size, size), () => ChangeAppearance(() => apply(colour)));
                    var hover = button.gameObject.AddComponent<SwatchName>();
                    hover.Label = label; hover.Resting = heading; hover.Named = title + "  ·  " + swatch.Name.ToUpperInvariant();
                }
                y += rowStep;
            }
            studioCursor += plateHeight + 12f;
        }

        /// <summary>
        /// Names a swatch in its row's heading while the pointer is over it, or the keyboard or the
        /// pad is on it: a swatch's own caption is its number ("Skin 5"), which told a pad player
        /// nothing of Espresso from Cocoa.
        /// </summary>
        private sealed class SwatchName : MonoBehaviour, UnityEngine.EventSystems.IPointerEnterHandler, UnityEngine.EventSystems.IPointerExitHandler,
            UnityEngine.EventSystems.ISelectHandler, UnityEngine.EventSystems.IDeselectHandler
        {
            public TMP_Text Label;
            public string Resting, Named;
            public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData data) => Show(true);
            // The pointer leaving a swatch the focus is still on leaves its name up.
            public void OnPointerExit(UnityEngine.EventSystems.PointerEventData data) => Show(Focused);
            public void OnSelect(UnityEngine.EventSystems.BaseEventData data) => Show(true);
            public void OnDeselect(UnityEngine.EventSystems.BaseEventData data) => Show(false);

            private bool Focused => UnityEngine.EventSystems.EventSystem.current != null
                && UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject == gameObject;

            private void Show(bool named) { if (Label != null) Label.text = Localisation.Text(named ? Named : Resting); }
        }

        /// <summary>The same colour to an 8-bit step, as a swatch is matched to what is worn.</summary>
        private static bool SameColour(Color a, Color b) =>
            Mathf.Abs(a.r - b.r) < .02f && Mathf.Abs(a.g - b.g) < .02f && Mathf.Abs(a.b - b.b) < .02f;

        private static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out var colour);
            colour.a = 1f;
            return colour;
        }

        // ---------------------------------------------------------------- editing

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
                    if (appearanceCategory == "Body") draft.Appearance = ChangeBody(draft.Appearance, original.bodyId);
                    if (appearanceCategory == "Face")
                    {
                        // The face's details too, off every outfit and back on every outfit, as Wear puts them.
                        foreach (var set in draft.Appearance.outfits) set.wardrobe.RemoveAll(worn => worn.slot == "Face");
                        var was = original.outfits.FirstOrDefault(outfit => outfit.id == original.activeOutfit) ?? original.outfits.FirstOrDefault();
                        var detail = was?.wardrobe.FirstOrDefault(worn => worn.slot == "Face");
                        var face = detail == null ? null
                            : Catalog.Items.FirstOrDefault(option => option.Matches(detail.itemId) && option.Fits(draft.Appearance.bodyId));
                        if (face != null) AppearanceEditing.Wear(draft.Appearance, face, Catalog);
                    }
                    foreach (var control in Catalog.Controls.Where(control => control.Category == appearanceCategory && control.Fits(draft.Appearance.bodyId)))
                    {
                        var value = original.dna.FirstOrDefault(item => item.id == control.Id);
                        if (value != null) AppearanceEditing.SetValue(draft.Appearance, control.Id, value.value);
                    }
                }
                else if (appearanceCategory == "Colors") draft.Appearance.colors = original.colors.Select(color => color.Clone()).ToList();
                else if (appearanceCategory == "Clothing")
                {
                    // Everything but the hair, the face's details and the accessories, which have pages of their own.
                    bool Garment(string slot) => !AppearanceEditing.CharacterSlots.Contains(slot) && !ProceduralAccessories.Slots.Contains(slot);
                    foreach (var outfit in draft.Appearance.outfits)
                    {
                        var baseline = original.outfits.FirstOrDefault(item => item.id == outfit.id) ?? original.outfits.First();
                        outfit.wardrobe.RemoveAll(item => Garment(item.slot));
                        outfit.wardrobe.AddRange(baseline.wardrobe.Where(item => Garment(item.slot)).Select(item => item.Clone()));
                        outfit.colors = baseline.colors.Select(color => color.Clone()).ToList();
                    }
                    draft.Appearance = Catalog.ChangeBody(draft.Appearance, draft.Appearance.bodyId);
                }
                else if (appearanceCategory == "Accessories")
                {
                    // The outfit being edited, back to what it wore of them when the edit began.
                    var outfit = AppearanceEditing.Outfit(draft.Appearance);
                    var baseline = original.outfits.FirstOrDefault(item => item.id == outfit.id)
                        ?? original.outfits.FirstOrDefault(item => item.id == original.activeOutfit) ?? original.outfits.FirstOrDefault();
                    outfit.wardrobe.RemoveAll(item => ProceduralAccessories.Slots.Contains(item.slot));
                    if (baseline != null)
                        outfit.wardrobe.AddRange(baseline.wardrobe.Where(item => ProceduralAccessories.Slots.Contains(item.slot)).Select(item => item.Clone()));
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
                    // The Hair page's own slots - the face's details are the Face page's to reset.
                    var slots = AppearanceEditing.HairSlots;
                    // Off every outfit, because Wear puts them on every outfit.
                    foreach (var set in draft.Appearance.outfits)
                        set.wardrobe.RemoveAll(item => slots.Contains(item.slot));
                    foreach (var worn in (baseline?.wardrobe ?? new List<AppearanceWardrobe>()).Where(item => slots.Contains(item.slot)))
                    {
                        var item = Catalog.Items.FirstOrDefault(option => option.Matches(worn.itemId) && option.Fits(draft.Appearance.bodyId));
                        if (item != null) AppearanceEditing.Wear(draft.Appearance, item, Catalog);
                    }
                    // The hair's colour is on this page too, and the brows' goes with it.
                    foreach (string id in new[] { "Hair", "Brows" })
                    {
                        draft.Appearance.colors.RemoveAll(color => color.id == id);
                        var was = original.colors.FirstOrDefault(color => color.id == id);
                        if (was != null) draft.Appearance.colors.Add(was.Clone());
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
            var ids = PresetIds();
            int current = Math.Max(0, ids.IndexOf(draft.Appearance?.presetId));
            string id = ids[(current + direction + ids.Count) % ids.Count];
            ChangeAppearance(() => draft.Appearance = Catalog?.Materialize(new CharacterAppearance { presetId = id })
                ?? new CharacterAppearance { presetId = id });
        }

        /// <summary>A band across the slider's track, centred on it and <paramref name="height"/> tall.</summary>
        private static RectTransform SliderArea(string name, RectTransform track, float height)
        {
            var area = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            area.SetParent(track, false);
            area.anchorMin = new Vector2(0f, .5f); area.anchorMax = new Vector2(1f, .5f);
            area.pivot = new Vector2(.5f, .5f);
            area.sizeDelta = new Vector2(0f, height); area.anchoredPosition = Vector2.zero;
            return area;
        }

        /// <summary>One proportion or feature: its name and value over the pack's slider.</summary>
        private void ControlSlider(AppearanceControl control, Rect r)
        {
            var appearance = Catalog.Materialize(draft.Appearance);
            float value = appearance.dna.FirstOrDefault(item => item.id == control.Id)?.value ?? .5f;
            Words(studioControls, control.Label, 16f, UiTheme.Paper, new Rect(r.x, r.y, r.width - 60f, 24f), name: control.Label);
            var number = Words(studioControls, value.ToString("0.00"), 16f, UiTheme.Paper, new Rect(r.x + r.width - 60f, r.y, 60f, 24f),
                TextAlignmentOptions.TopRight, "Slider value");
            var slider = PackSlider(studioControls, control.Label + " slider", new Rect(r.x, r.y + 28f, r.width, 26f), control.Minimum, control.Maximum, value);
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
                number.text = next.ToString("0.00");
            });
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
                    if (randomLocks.Contains(AppearanceEditing.IsCharacterSlot(slot) ? "Hair" : "Clothing")) continue;
                    var choices = Catalog.Items.Where(item => item.Slot == slot && item.Fits(draft.Appearance.bodyId)).ToList();
                    if (choices.Count > 0) AppearanceEditing.Wear(draft.Appearance, choices[cosmeticRandom.Next(choices.Count)], Catalog);
                }
                // Accessories as well, on the outfit being edited - and most often none: a random
                // houseguest is somebody, not the jewellery counter. Their page's lock keeps them.
                if (!randomLocks.Contains("Accessories"))
                    foreach (string slot in ProceduralAccessories.Slots)
                    {
                        var choices = Catalog.Items.Where(item => item.Slot == slot && item.Fits(draft.Appearance.bodyId)).ToList();
                        if (choices.Count > 0 && cosmeticRandom.NextDouble() < AccessoryOdds)
                            AppearanceEditing.Wear(draft.Appearance, choices[cosmeticRandom.Next(choices.Count)], Catalog);
                        else AppearanceEditing.Outfit(draft.Appearance).wardrobe.RemoveAll(item => item.slot == slot);
                    }
                // Skin first, then the eyes and hair that go with it, weighted by its depth: drawn
                // apart, half the deep-skinned houseguests had blue or grey eyes and most had blonde,
                // red or white hair.
                if (!randomLocks.Contains("Colors"))
                {
                    var skin = CharacterPalettes.RandomSkin(cosmeticRandom).Colour;
                    AppearanceEditing.SetColor(draft.Appearance, "Skin", skin);
                    AppearanceEditing.SetColor(draft.Appearance, "Eyes", CharacterPalettes.RandomEyes(skin, cosmeticRandom).Colour);
                    // The hair's colour is on the Hair page and the Colors page: either lock keeps it.
                    if (!randomLocks.Contains("Hair")) SetHairColour(CharacterPalettes.RandomHair(skin, cosmeticRandom).Colour);
                }
            });
        }
    }
}
