using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Persistence;
using Gamesim.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Gamesim.Presentation
{
    /// <summary>
    /// The cast screen: who you are playing as, which roster the house is drawn from, and how many
    /// people are in it.
    ///
    /// <para>This is the last screen from the reference build that had no counterpart here. Starting
    /// a new season used to be a single line in the settings list that immediately built the one
    /// authored six-person scenario — there was no point at which the player chose anything, which
    /// is why removing the six-contestant rule changed nothing a player could see.</para>
    ///
    /// <para>It decides nothing itself. The screen collects a <see cref="SeasonBuilder.Choice"/> and
    /// hands it back; the caller builds and stages the season, so a cast that cannot be saved fails
    /// in the place that already knows how to keep the current slot intact.</para>
    ///
    /// <para>Laid out as the web game's chooser is: the cast as glamour photos in gold rings, each
    /// with a name plate and a nickname pill in their category's colours, and beside them the
    /// picked houseguest's details - their live model, turning, the one 3D figure on the screen,
    /// with who they are, their words, their traits and their kind of player, and a way to play as
    /// them or dress them first. The photos are the web game's own.</para>
    ///
    /// <para>Every card is a real <see cref="Button"/> whose label is the houseguest's name, so the
    /// grid is reachable by keyboard and announced by name rather than by position. The selected
    /// card is marked with a ring <b>and</b> a word — "Playing as" — because a colour on its own
    /// is not a state a screen reader can report.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CastSelect : MonoBehaviour
    {
        /// <summary>
        /// The widest the screen draws, and how it decides to draw narrower.
        ///
        /// <para>The scaler matches width and height equally, so the canvas width in reference
        /// units is <c>sqrt(aspect) * 1200</c> at the larger text size - 1600 at 16:9 but 1386 on
        /// 4:3. It is measured from the canvas at every rebuild rather than assumed.</para>
        /// </summary>
        private const float MaxWidth = 1320f;
        /// <summary>The side margin the brand and the corner lines need before they are drawn.</summary>
        private const float SideDressing = 150f;
        private const float MinWidth = 900f;
        private const float FrameMargin = 96f;
        private const float Pad = 28f;
        private const int Columns = 4;
        private const float Gutter = 14f;

        /// <summary>
        /// A card: the ring, the name plate, the nickname and the traits, stacked. Three rows of
        /// these and their gutters come to 640, and with six above them and a glow's room below
        /// 656, inside the grid's 676 at 16:9 and the standard text size, so the whole roster is on
        /// screen without a scroll. A shorter grid - the larger text on 16:9, or any 21:9 - leaves
        /// the third row under the fold, and a scrollbar beside the grid says so.
        /// </summary>
        private const float CardHeight = 204f;
        /// <summary>The glamour ring's outer diameter; the photo sits five pixels inside it.</summary>
        private const float RingSize = 104f;
        private const float PanelGap = 20f;

        /// <summary>
        /// What the two fixed bars reserve, and therefore what the roster and the details get. The
        /// footer is one row wherever it fits - the house size beside the start - and two where the
        /// frame is too narrow for that.
        /// </summary>
        private const float HeaderHeight = 244f;
        private float FooterHeight => WideFooter ? 150f : 214f;
        private bool WideFooter => Width - Pad * 2f >= 1180f;

        private float width = 1180f;
        private float frameHeight = 1080f;
        /// <summary>The frame the screen was last laid out for; a new shape lays it out again.</summary>
        private Vector2 builtFor;
        private float Width => width;
        /// <summary>The grid shares the frame with the details; the saved-houseguest pages have it to themselves.</summary>
        private bool Detailed => !libraryMode && !castSlotsMode;
        private float PanelWidth => Detailed ? Mathf.Clamp(Width * .34f, 360f, 450f) : 0f;
        private float GridWidth => Detailed ? Width - PanelWidth - PanelGap : Width;
        private float CardWidth => (GridWidth - Gutter * (Columns - 1)) / Columns;

        /// <summary>The caption the start control carries. Tests and the tour find it by this text.</summary>
        public const string StartCaption = "Start this season";
        public const string CancelCaption = "Cancel — keep this season";

        /// <summary>Names the card's parts carry, so a test can find them without guessing.</summary>
        public const string CardGlowName = "Glow";
        public const string TraitChipName = "Trait";
        /// <summary>The glamour photo on a card, and the live model in the details.</summary>
        public const string GlamourPhotoName = "Glamour photo";
        public const string LiveModelName = "Live model";
        public const string DetailPanelName = "Detail panel";
        /// <summary>The live model's studio, which lives outside the canvas at the scene root.</summary>
        public const string StudioName = "Cast select studio";

        private RectTransform content;
        private CanvasGroup group;
        private float cursor;
        /// <summary>The grid's scrolling content, kept past a rebuild so the next one can start where it was.</summary>
        private RectTransform gridContent;
        /// <summary>Set by whatever shows a different list - a new screen, roster, category or page.</summary>
        private bool scrollToTop = true;
        /// <summary>The details' Play as, when there is one: where the keyboard goes after a pick.</summary>
        private Button playButton;

        private CastTemplates.Roster roster = CastTemplates.Roster.Regular;
        private string category = CastTemplates.AllCategories;
        private string selectedId;
        private int houseSize = SeasonBuilder.DefaultHouseSize;

        private Action<SeasonBuilder.Choice> onStart;
        private Action onCancel;
        private Action<SeasonBuilder.Choice, CharacterDraft> onCustomise;

        private CanvasScaler scaler;
        private bool libraryMode;
        private bool castSlotsMode;
        private readonly List<CharacterProfile> customHouseguests = new List<CharacterProfile>();
        private CharacterDraft retainedDraft;
        private string resumeError;
        private CharacterCreator creator;
        private CharacterProfileStore profileStore;
        private readonly CharacterProfileBrowser profileBrowser = new CharacterProfileBrowser();
        public CharacterProfileStore ProfileStore => profileStore ?? (profileStore = new CharacterProfileStore());

        // The details' live model: one studio, kept between rebuilds so a click does not rebuild the body.
        private CharacterStudioPreview studio;
        private RawImage liveModel;
        private TMP_Text previewStatus;
        private Image pickedGlow;
        private float lastTurn = -10f;

        /// <summary>Whose model the details are showing, or null when nobody is picked.</summary>
        public string PreviewedId { get; private set; }
        /// <summary>True once the picked houseguest's model has been built and drawn.</summary>
        public bool PreviewReady => studio != null && studio.gameObject.activeSelf && PreviewedId != null && !studio.IsBuilding;
        /// <summary>
        /// The live model's studio, for tests and the tour, as the creator offers its own: which look
        /// it last finished building, and whether that is a fallback. Null once the screen has
        /// closed with a start or a cancel.
        /// </summary>
        public CharacterStudioPreview StudioPreview => studio;

        /// <summary>
        /// The "Reduce character motion" setting. Under it the live model stands as it was left
        /// instead of turning by itself - a drag still turns it - the picked card's halo holds
        /// still instead of pulsing, and a portrait does not lift under the pointer.
        /// </summary>
        public bool ReducedMotion { get; set; }

        public void ConfigureCreator(CharacterCreator value) => creator = value;
        public void ConfigureProfiles(CharacterProfileStore store)
        {
            profileStore = store ?? throw new ArgumentNullException(nameof(store));
            profileBrowser.Reset();
            if (IsShowing) Rebuild();
        }

        public void SetDraft(CharacterDraft draft) => retainedDraft = draft?.Copy();
        private readonly List<KeyValuePair<RawImage, ContestantState>> portraits = new List<KeyValuePair<RawImage, ContestantState>>();

        private void Update()
        {
            if (!IsShowing) return;
            // A window resized, or a capture's camera of another shape: the details' height and the
            // footer's row are laid out for the frame, so a new frame gets a new layout.
            var size = ((RectTransform)transform).rect.size;
            if (Mathf.Abs(size.x - builtFor.x) > 2f || Mathf.Abs(size.y - builtFor.y) > 2f) Rebuild();
            // A houseguest with no glamour photo shows their rendered face, filled in as it lands.
            foreach (var item in portraits)
            {
                if (item.Key == null || item.Key.texture != null) continue;
                var texture = CharacterPortraits.Get(item.Value);
                if (texture == null) continue;
                item.Key.texture = texture;
                item.Key.color = Color.white;
            }
            if (studio != null && studio.gameObject.activeSelf && liveModel != null)
            {
                if (liveModel.texture != studio.Texture) liveModel.texture = studio.Texture;
                // A slow turn while nobody is turning it, so the whole figure is seen - but not
                // under reduced motion, where the figure stays as the player left it.
                if (!ReducedMotion && !studio.IsBuilding && Time.unscaledTime - lastTurn > 2.5f) studio.Rotate(Time.unscaledDeltaTime * 14f);
                if (previewStatus != null) previewStatus.text = studio.IsBuilding ? Localisation.Text(studio.Status ?? string.Empty) : string.Empty;
            }
            if (pickedGlow != null)
            {
                float pulse = .5f + .5f * Mathf.Sin(Time.unscaledTime * Mathf.PI);
                var gold = CastSelectArt.Gold;
                pickedGlow.color = new Color(gold.r, gold.g, gold.b, ReducedMotion ? .5f : Mathf.Lerp(.3f, .7f, pulse));
            }
        }

        private void OnDestroy()
        {
            if (studio != null) Destroy(studio.gameObject);
        }

        /// <summary>
        /// The "larger text" accessibility setting, applied by scaling the whole screen rather than
        /// each label.
        ///
        /// <para>This is a fixed layout — cards, chips and rows are sized in reference pixels — so
        /// growing the type alone would push text out of boxes that did not grow with it. Shrinking
        /// the reference resolution magnifies the layout and its text together, which is what a
        /// fixed layout actually needs.</para>
        /// </summary>
        public float FontScale
        {
            set
            {
                if (scaler == null) return;
                float scale = Mathf.Clamp(value, 0.5f, 2f);
                scaler.referenceResolution = new Vector2(1920f / scale, 1080f / scale);
            }
        }

        public bool IsShowing => group != null && group.alpha > 0f;

        /// <summary>
        /// Its own canvas above the ceremony cards and below nothing. Unlike the ceremony overlays
        /// this one raycasts: it is a screen to be used, not watched.
        /// </summary>
        public static CastSelect Attach(GameObject owner)
        {
            var root = new GameObject("Gamesim Cast Select",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler),
                typeof(GraphicRaycaster), typeof(CanvasGroup));
            root.transform.SetParent(owner.transform, false);

            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 125;

            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var screen = root.AddComponent<CastSelect>();
            screen.scaler = scaler;
            screen.group = root.GetComponent<CanvasGroup>();
            screen.Hide();
            return screen;
        }

        public void Hide()
        {
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;
            // The model is only drawn while the screen is: no studio rendering behind the house.
            if (studio != null) studio.gameObject.SetActive(false);
        }

        /// <summary>
        /// Opens the screen. <paramref name="start"/> receives the choice when the player commits;
        /// <paramref name="cancel"/> runs when they back out, and nothing is built in that case.
        /// </summary>
        public void Show(Action<SeasonBuilder.Choice> start, Action cancel) => Show(start, cancel, null);

        /// <summary>
        /// The same, with the creator attached. <paramref name="customise"/> receives the choice as
        /// it stands and the draft to open — built from the selected card, or blank when there is
        /// none. Without it the creator controls are simply not drawn, so a caller that has no
        /// creator still gets a working cast screen.
        /// </summary>
        public void Show(Action<SeasonBuilder.Choice> start, Action cancel,
            Action<SeasonBuilder.Choice, CharacterDraft> customise)
        {
            onStart = start;
            onCancel = cancel;
            onCustomise = customise;
            roster = CastTemplates.Roster.Regular;
            category = CastTemplates.AllCategories;
            selectedId = null;
            libraryMode = false;
            castSlotsMode = false;
            customHouseguests.Clear();
            profileBrowser.Reset();
            retainedDraft = null;
            resumeError = null;
            scrollToTop = true;
            houseSize = SeasonBuilder.ClampHouseSize(roster, SeasonBuilder.DefaultHouseSize);
            Rebuild();
            group.alpha = 1f;
            group.blocksRaycasts = true;
            group.interactable = true;
        }

        /// <summary>Closes without building, the same as the cancel control. Escape routes here.</summary>
        public void Dismiss()
        {
            if (!IsShowing) return;
            var cancel = onCancel;
            Hide();
            ReleaseStudio();
            cancel?.Invoke();
        }

        /// <summary>
        /// Lets the live model go when the screen closes for good - a start or a cancel, not a trip
        /// to the creator, which comes straight back to the same houseguest. Hiding only puts the
        /// studio to sleep, and a sleeping studio keeps its built body, that body's generated
        /// textures, its lights and its multisampled target: for the whole season, until now,
        /// because nothing else ever destroyed it. The creator's studio has always gone this way.
        /// </summary>
        private void ReleaseStudio()
        {
            if (studio != null) { Destroy(studio.gameObject); studio = null; }
            PreviewedId = null;
        }

        /// <summary>
        /// The same, once a season has started from the creator this screen handed a houseguest to.
        /// The screen is asleep behind the creator then rather than closed, so neither of its own
        /// ways out ran, and nothing brings it back to the houseguest it was showing: the next New
        /// season opens it afresh. Nothing is let go while the screen is up.
        /// </summary>
        public void ReleasePreview()
        {
            if (IsShowing) return;
            ReleaseStudio();
        }

        // ---------------------------------------------------------------- build

        private void Rebuild()
        {
            // Where the grid was scrolled to, read before the teardown. Every press rebuilds the
            // screen, and a grid rebuilt at the top threw the player back to the first row: a card
            // picked on the third row vanished under the fold with its PLAYING AS badge. A new
            // screen, roster, category or page is a different list and starts at the top.
            float keepY = !scrollToTop && gridContent != null ? gridContent.anchoredPosition.y : 0f;
            scrollToTop = false;
            portraits.Clear();
            liveModel = null;
            previewStatus = null;
            pickedGlow = null;
            playButton = null;
            // Deactivated before Destroy, which is deferred to the end of the frame: the screen
            // rebuilds itself on every click, so for one frame the old controls would otherwise
            // still be live alongside the new ones and "the Start button" would match twice.
            foreach (Transform child in transform)
            {
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }

            // Measured, not assumed: see MaxWidth. The component lives on the canvas root, so this
            // rect is the canvas in its own reference units at whatever aspect the player has.
            var frameRect = ((RectTransform)transform).rect;
            builtFor = frameRect.size;
            float frame = frameRect.width > 1f ? frameRect.width : 1920f;
            frameHeight = frameRect.height > 1f ? frameRect.height : 1080f;
            width = Mathf.Clamp(frame - FrameMargin, MinWidth, MaxWidth);

            // Opaque: a chooser this consequential should not have the game showing through it.
            var scrim = HudPrimitives.Fill("Scrim", transform, UiTheme.Background, 1);
            // A modal's scrim has to catch the mouse; Fill leaves its art non-interactive.
            scrim.GetComponent<Image>().raycastTarget = true;
            var night = UiTheme.Pack(PackArt.BackgroundNavy);
            if (night != null)
            {
                var ground = scrim.GetComponent<Image>();
                ground.sprite = night; ground.type = Image.Type.Simple; ground.color = Color.white;
            }
            Stretch(scrim);
            // Lit as the menu's ground is: purple low on the left, cyan high on the right.
            MainMenu.CornerLight(scrim, PackArt.GlowPurple, new Vector2(0f, 0f), new Vector2(1100f, 800f), .40f);
            MainMenu.CornerLight(scrim, PackArt.GlowCyan, new Vector2(1f, 1f), new Vector2(1000f, 700f), .28f);
            HudPrimitives.Vignette(scrim);
            float margin = (frame - Width) * .5f;
            if (margin >= SideDressing)
            {
                Brand(scrim);
                CornerLines(scrim);
            }

            content = new GameObject("Fixed setup navigation", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(scrim, false);
            content.anchorMin = content.anchorMax = new Vector2(.5f, 1f);
            content.pivot = new Vector2(.5f, 1f);
            content.sizeDelta = new Vector2(Width, HeaderHeight);
            cursor = 0f;
            Header(); SetupNavigation();
            if (Detailed) Filters();

            float errorHeight = string.IsNullOrEmpty(resumeError) ? 0f : 64f;
            float bodyHeight = frameHeight - (HeaderHeight + FooterHeight + 10f) - errorHeight;
            var viewport = HudPrimitives.Fill("Viewport", scrim, new Color(0f, 0f, 0f, 0f), 1);
            viewport.anchorMin = new Vector2(0.5f, 0f);
            viewport.anchorMax = new Vector2(0.5f, 1f);
            viewport.pivot = new Vector2(0.5f, 1f);
            viewport.sizeDelta = new Vector2(GridWidth, -(HeaderHeight + FooterHeight + 10f) - errorHeight);
            viewport.anchoredPosition = new Vector2(-Width * .5f + GridWidth * .5f, -HeaderHeight);
            viewport.gameObject.AddComponent<RectMask2D>();

            content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(viewport, false);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            gridContent = content;

            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            viewport.gameObject.AddComponent<SetupScrollFocus>();
            scroll.content = content;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;
            scroll.verticalScrollbar = GridScrollbar(scrim, viewport);
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;

            cursor = 0f;
            if (castSlotsMode) CastSlots(); else if (libraryMode) LibraryCards(); else Grid();
            // The grid measures its own foot (see Grid); the saved-houseguest pages end on the pad.
            content.sizeDelta = new Vector2(0f, cursor + (Detailed ? 0f : Pad));
            // Back where it was, as far as the new list reaches.
            content.anchoredPosition = new Vector2(0f, Mathf.Clamp(keepY, 0f, Mathf.Max(0f, content.sizeDelta.y - bodyHeight)));

            if (Detailed) DetailPanel(scrim, Width * .5f - PanelWidth * .5f, HeaderHeight, PanelWidth, bodyHeight,
                HeaderHeight + FooterHeight + 10f + errorHeight);
            else if (studio != null) studio.gameObject.SetActive(false);

            content = new GameObject("Fixed season footer", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(scrim, false);
            content.anchorMin = content.anchorMax = new Vector2(.5f, 0f);
            content.pivot = new Vector2(.5f, 0f);
            content.sizeDelta = new Vector2(Width, FooterHeight + errorHeight);
            cursor = 0f;
            Footer();
        }

        /// <summary>
        /// A thin bar in the gap beside the grid, there only while the grid holds more than it
        /// shows. Without it a short frame cut the third row to the tops of its rings, and nothing
        /// said that four more houseguests were below. Outside the viewport, whose mask would clip
        /// it, and out of the keyboard's way: the grid already scrolls to whatever is selected.
        /// </summary>
        private Scrollbar GridScrollbar(RectTransform scrim, RectTransform viewport)
        {
            var track = HudPrimitives.Fill("Scrollbar", scrim, new Color(UiTheme.Hairline.r, UiTheme.Hairline.g, UiTheme.Hairline.b, .14f), 3);
            track.anchorMin = new Vector2(.5f, 0f);
            track.anchorMax = new Vector2(.5f, 1f);
            track.pivot = new Vector2(.5f, 1f);
            track.sizeDelta = new Vector2(6f, viewport.sizeDelta.y);
            float gap = Detailed ? PanelGap * .5f : 10f;
            track.anchoredPosition = new Vector2(-Width * .5f + GridWidth + gap, viewport.anchoredPosition.y);
            track.GetComponent<Image>().raycastTarget = true;
            var area = new GameObject("Sliding area", typeof(RectTransform)).GetComponent<RectTransform>();
            area.SetParent(track, false);
            area.anchorMin = Vector2.zero; area.anchorMax = Vector2.one;
            area.offsetMin = Vector2.zero; area.offsetMax = Vector2.zero;
            var handle = HudPrimitives.Fill("Handle", area, new Color(UiTheme.Accent.r, UiTheme.Accent.g, UiTheme.Accent.b, .7f), 3);
            handle.offsetMin = Vector2.zero; handle.offsetMax = Vector2.zero;
            var handleImage = handle.GetComponent<Image>();
            handleImage.raycastTarget = true;
            var bar = track.gameObject.AddComponent<Scrollbar>();
            bar.handleRect = handle;
            bar.targetGraphic = handleImage;
            bar.direction = Scrollbar.Direction.BottomToTop;
            bar.navigation = new Navigation { mode = Navigation.Mode.None };
            return bar;
        }

        private void SetupNavigation()
        {
            if (onCustomise == null) return;
            var row = Row(44f);
            Chip(row, "Choose a houseguest", -448f, 216f, !libraryMode && !castSlotsMode, () => { libraryMode = false; castSlotsMode = false; scrollToTop = true; Rebuild(); });
            Chip(row, CharacterCreator.CreateCaption, -224f, 216f, false, () => OpenCreator(CharacterDraft.Blank()));
            Chip(row, retainedDraft == null ? CharacterCreator.CustomiseCaption : "Resume setup", 0f, 216f, false, () =>
            {
                if (retainedDraft != null) { OpenCreator(retainedDraft.Copy()); return; }
                var chosen = CastTemplates.Find(selectedId);
                OpenCreator(chosen == null ? CharacterDraft.Blank() : CharacterDraft.FromAppearance(chosen));
            });
            Chip(row, "My Houseguests", 224f, 216f, libraryMode, () => { libraryMode = true; castSlotsMode = false; scrollToTop = true; Rebuild(); });
            Chip(row, "Cast slots", 448f, 216f, castSlotsMode, () => { castSlotsMode = true; libraryMode = false; scrollToTop = true; Rebuild(); });
        }

        private void CastSlots()
        {
            Text("CUSTOM CAST — " + customHouseguests.Count + " OF " + (houseSize - 1) + " NPC SLOTS", 22f, UiTheme.Paper, 42f, TextAlignmentOptions.Left);
            Text("Add saved houseguests. Remaining slots use the chosen roster. Repeated profiles become distinct contestants with independent season state.",
                15f, UiTheme.Muted, 54f, TextAlignmentOptions.Left);
            for (int i = 0; i < customHouseguests.Count; i++)
            {
                int slot = i;
                Text("Slot " + (i + 1) + ": " + customHouseguests[i].name, 17f, UiTheme.Accent, 30f, TextAlignmentOptions.Left);
                var row = Row(46f);
                if (creator != null) Chip(row, "Edit slot " + (i + 1), -165f, 300f, false, () => EditCastSlot(slot));
                Chip(row, "Remove slot " + (i + 1), 165f, 300f, false, () => { customHouseguests.RemoveAt(slot); Rebuild(); });
            }
            Text("SAVED HOUSEGUESTS", 18f, UiTheme.Paper, 40f, TextAlignmentOptions.Left);
            var store = ProfileStore;
            var profiles = store.List();
            foreach (string error in store.ReadErrors) Text(error, 14f, UiTheme.Warning, 46f, TextAlignmentOptions.Left);
            if (profiles.Count == 0) Text("Create and save a houseguest first. Their profile will be available here.",
                16f, UiTheme.Muted, 56f, TextAlignmentOptions.Left);
            var visibleProfiles = profileBrowser.Draw(Row(48f), profiles, Rebuild);
            if (profiles.Count > 0 && visibleProfiles.Count == 0)
                Text("No saved houseguests match this name. Clear search to see everyone.", 16f, UiTheme.Muted, 42f, TextAlignmentOptions.Left);
            foreach (var profile in visibleProfiles)
            {
                var entry = profile;
                var row = Row(56f);
                CharacterProfileBrowser.Thumbnail(row, entry, -480f);
                Chip(row, "Add " + entry.name + " to the cast", 0f, 800f, false, () =>
                {
                    if (customHouseguests.Count >= houseSize - 1) return;
                    customHouseguests.Add(entry.Clone()); Rebuild();
                }).interactable = customHouseguests.Count < houseSize - 1;
            }
        }

        private void LibraryCards()
        {
            var store = ProfileStore;
            var profiles = store.List();
            foreach (string error in store.ReadErrors) Text(error, 14f, UiTheme.Warning, 44f, TextAlignmentOptions.Left);
            if (profiles.Count == 0) Text("No saved houseguests yet. Create one, then save it in My Houseguests.",
                18f, UiTheme.Muted, 60f, TextAlignmentOptions.Center);
            var visibleProfiles = profileBrowser.Draw(Row(48f), profiles, Rebuild);
            if (profiles.Count > 0 && visibleProfiles.Count == 0)
                Text("No saved houseguests match this name. Clear search to see everyone.", 16f, UiTheme.Muted, 42f, TextAlignmentOptions.Left);
            foreach (var profile in visibleProfiles)
            {
                var entry = profile;
                var row = Row(58f);
                CharacterProfileBrowser.Thumbnail(row, entry, -518f);
                Chip(row, "Choose " + entry.name, -150f, 600f, false, () => OpenCreator(entry.ToDraft()));
                Chip(row, "Remix " + entry.name, 350f, 300f, false, () =>
                {
                    var draft = entry.ToDraft(); draft.Name += " (copy)"; OpenCreator(draft);
                });
            }
        }

        private void Header()
        {
            Space(22f);
            var title = HudPrimitives.Heading("Text", content, 42f, UiTheme.Paper, TextAlignmentOptions.Center);
            // ONE localisation key, looked up once, and the RESULT marked up so the last word takes
            // the accent. Rich text is off by default in HudPrimitives.Label because engine and
            // player strings can contain angle brackets; this label's content is a fixed literal
            // that has already been through Localisation, so it is safe here and nowhere else.
            string headline = Localisation.Text("CHOOSE YOUR HOUSEGUEST");
            int lastSpace = headline.LastIndexOf(' ');
            if (lastSpace > 0 && lastSpace < headline.Length - 1)
            {
                title.richText = true;
                title.text = headline.Substring(0, lastSpace + 1)
                    + "<color=#" + ColorUtility.ToHtmlStringRGB(UiTheme.Glow) + ">"
                    + headline.Substring(lastSpace + 1) + "</color>";
            }
            else title.text = headline;
            title.characterSpacing = 2f;
            Place(title.rectTransform, Width - Pad * 2f, 52f, -cursor);
            cursor += 52f;
            Text("Pick who you play as, then set the size of the house. Everyone else is cast from the same roster.",
                16f, UiTheme.Muted, 24f, TextAlignmentOptions.Center);
            Space(8f);
        }

        /// <summary>
        /// The roster and the kind of player, on one row: the two rosters as tabs on the left, the
        /// categories as chips on the right. The category picked is explained under them, in the
        /// web game's words; with none picked, the line says how the roster divides.
        /// </summary>
        private void Filters()
        {
            var bar = Row(44f);
            float inner = Width - Pad * 2f;
            float tabWidth = Mathf.Min(200f, inner * .15f);
            var rosters = Enum.GetValues(typeof(CastTemplates.Roster)).Cast<CastTemplates.Roster>().ToList();
            float left = -inner * .5f;
            for (int i = 0; i < rosters.Count; i++)
            {
                var pick = rosters[i];
                var tab = Chip(bar, CastTemplates.RosterName(pick), left + tabWidth * (i + .5f) + 8f * i, tabWidth, roster == pick, () =>
                {
                    if (roster == pick) return;
                    roster = pick;
                    // A card from the other roster is not in this one, so the pick cannot survive
                    // the switch — better to clear it than to start a season with a stale persona.
                    selectedId = null;
                    houseSize = SeasonBuilder.ClampHouseSize(roster, houseSize);
                    scrollToTop = true;
                    Rebuild();
                });
                ((RectTransform)tab.transform).sizeDelta = new Vector2(tabWidth, 42f);
                var words = tab.GetComponentInChildren<TMP_Text>();
                if (words != null) { words.fontSizeMax = 17f; words.fontSize = 17f; }
            }

            var chips = new List<string> { CastTemplates.AllCategories };
            chips.AddRange(CastTemplates.Categories);
            float chipsLeft = left + rosters.Count * (tabWidth + 8f) + 24f;
            float span = (inner * .5f - chipsLeft) / chips.Count;
            for (int i = 0; i < chips.Count; i++)
            {
                var pick = chips[i];
                Chip(bar, pick, chipsLeft + span * (i + .5f), span - 10f, string.Equals(category, pick, StringComparison.OrdinalIgnoreCase),
                    () => { category = pick; scrollToTop = true; Rebuild(); });
            }

            string description = CastTemplates.CategoryDescription(category);
            string line;
            if (description != null) line = category + " · " + description;
            else
            {
                var counts = CastTemplates.Categories
                    .Select(kind => (kind, count: CastTemplates.Filter(roster, kind).Count()))
                    .Where(entry => entry.count > 0)
                    .Select(entry => entry.count + " " + entry.kind + (entry.count == 1 ? "" : "s"));
                line = CastTemplates.In(roster).Count() + " houseguests · " + string.Join(", ", counts);
            }
            var explained = HudPrimitives.Label("Category line", content, 14f, description != null ? CastSelectArt.CategoryColours(category).from : UiTheme.Muted,
                TextAlignmentOptions.Center);
            explained.text = Localisation.Text(line);
            if (description != null) { var medium = UiTheme.Font(UiTheme.Weight.Medium); if (medium != null) explained.font = medium; }
            Place(explained.rectTransform, Width - Pad * 2f, 22f, -cursor);
            cursor += 22f;
        }

        private void Grid()
        {
            var shown = CastTemplates.Filter(roster, category).ToList();
            if (shown.Count == 0)
            {
                Text("No houseguest on this roster matches that filter.", 15f, UiTheme.Muted, 30f,
                    TextAlignmentOptions.Center);
                return;
            }
            cursor = 6f;
            for (int index = 0; index < shown.Count; index++)
            {
                int column = index % Columns;
                if (column == 0 && index > 0) cursor += CardHeight + Gutter;
                Card(shown[index], column, -cursor);
            }
            // Room under the last row for a picked card's glow, and no more - not a gutter and the
            // page's pad as well. Those came to 42, which took the roster twelve past a 16:9 grid's
            // 676: a scroll of nothing, and a scrollbar beside the grid saying there was more.
            cursor += CardHeight + UiTheme.GlowWidth;
        }

        /// <summary>
        /// One houseguest, as the web game's grid draws them: the glamour photo in a gold ring, the
        /// name on a slate plate, the nickname on a pill in their category's colours, and the two
        /// traits under it. The picked card glows, its ring pulses, and it says "Playing as".
        /// </summary>
        private void Card(CastTemplates.Template template, int column, float y)
        {
            bool chosen = string.Equals(selectedId, template.Id, StringComparison.Ordinal);

            var card = HudPrimitives.Fill(template.Name, content, UiTheme.CardFill, UiTheme.GlassRadius);
            card.anchorMin = new Vector2(0f, 1f);
            card.anchorMax = new Vector2(0f, 1f);
            card.pivot = new Vector2(0f, 1f);
            card.sizeDelta = new Vector2(CardWidth, CardHeight);
            card.anchoredPosition = new Vector2(column * (CardWidth + Gutter), y);

            // The glow and ONE ring on the picked card; the hairline alone on the rest. The picked
            // card's edge is the portrait ring's gold, not the accent edge: that is for the one thing
            // to act on now, and this screen always shows several active pills.
            if (chosen)
            {
                UiTheme.AddGlow(card, UiTheme.GlassRadius);
                UiTheme.AddBorder(card, UiTheme.GlassRadius, new Color(CastSelectArt.Gold.r, CastSelectArt.Gold.g, CastSelectArt.Gold.b, .9f));
            }
            else
            {
                UiTheme.AddBorder(card, UiTheme.GlassRadius,
                    new Color(UiTheme.Hairline.r, UiTheme.Hairline.g, UiTheme.Hairline.b, 0.45f));
            }

            var image = card.GetComponent<Image>();
            image.raycastTarget = true;
            var button = card.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var pick = template.Id;
            var cardName = template.Name;
            button.onClick.AddListener(() =>
            {
                retainedDraft = null;
                selectedId = string.Equals(selectedId, pick, StringComparison.Ordinal) ? null : pick;
                Rebuild();
                FocusAfterPick(cardName);
            });

            // The portrait: a glow behind a picked one, the gold ring, the photo inside it.
            var portrait = new GameObject("Portrait", typeof(RectTransform)).GetComponent<RectTransform>();
            portrait.SetParent(card, false);
            portrait.anchorMin = portrait.anchorMax = new Vector2(.5f, 1f);
            portrait.pivot = new Vector2(.5f, 1f);
            portrait.sizeDelta = new Vector2(RingSize, RingSize);
            portrait.anchoredPosition = new Vector2(0f, -10f);
            var hover = card.gameObject.AddComponent<CardHover>();
            hover.Target = portrait;
            hover.Owner = this;
            if (chosen)
            {
                var halo = new GameObject("Ring halo", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                halo.rectTransform.SetParent(portrait, false);
                halo.rectTransform.anchorMin = Vector2.zero; halo.rectTransform.anchorMax = Vector2.one;
                halo.rectTransform.offsetMin = new Vector2(-26f, -26f); halo.rectTransform.offsetMax = new Vector2(26f, 26f);
                halo.sprite = CastSelectArt.Glow(); halo.raycastTarget = false;
                halo.color = new Color(CastSelectArt.Gold.r, CastSelectArt.Gold.g, CastSelectArt.Gold.b, .5f);
                pickedGlow = halo;
            }
            var ringArt = new GameObject("Ring", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            ringArt.rectTransform.SetParent(portrait, false);
            ringArt.rectTransform.anchorMin = Vector2.zero; ringArt.rectTransform.anchorMax = Vector2.one;
            ringArt.rectTransform.offsetMin = Vector2.zero; ringArt.rectTransform.offsetMax = Vector2.zero;
            ringArt.sprite = CastSelectArt.Ring(); ringArt.raycastTarget = false;
            var photo = HudPrimitives.Disc("Photo", portrait, CastPalette.For(template.Id));
            photo.anchorMin = Vector2.zero; photo.anchorMax = Vector2.one;
            photo.offsetMin = new Vector2(5f, 5f); photo.offsetMax = new Vector2(-5f, -5f);
            photo.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            var glamour = CastSelectArt.Glamour(template.Id);
            var picture = new GameObject(glamour != null ? GlamourPhotoName : "Model portrait", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            picture.rectTransform.SetParent(photo, false);
            picture.rectTransform.anchorMin = Vector2.zero; picture.rectTransform.anchorMax = Vector2.one;
            picture.rectTransform.offsetMin = Vector2.zero; picture.rectTransform.offsetMax = Vector2.zero;
            picture.raycastTarget = false;
            if (glamour != null) picture.texture = glamour;
            else
            {
                // Nobody without a photo goes faceless: their rendered face lands here when it is ready.
                picture.color = Color.clear;
                picture.uvRect = new Rect(.12f, .2f, .76f, .76f);
                portraits.Add(new KeyValuePair<RawImage, ContestantState>(picture, CastTemplates.ToContestant(template, false)));
            }

            // The category's glyph in the upper right, and the pick stated in the upper left.
            var mark = UiTheme.Icon(CategoryIcon(template.Category));
            if (mark != null)
            {
                var badge = new GameObject("Category mark", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
                badge.SetParent(card, false);
                badge.anchorMin = new Vector2(1f, 1f); badge.anchorMax = new Vector2(1f, 1f);
                badge.pivot = new Vector2(1f, 1f);
                badge.sizeDelta = new Vector2(20f, 20f);
                badge.anchoredPosition = new Vector2(-10f, -10f);
                var art = badge.GetComponent<Image>();
                art.sprite = mark;
                art.color = CastSelectArt.CategoryColours(template.Category).from;
                art.preserveAspect = true;
                art.raycastTarget = false;
            }
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            if (chosen)
            {
                // The pick stated in words on a gold badge across the foot of the ring, as the web
                // game's check badge sits on its ring: the word is what a screen reader reads.
                // Twelve up from the foot, not two: the name plate starts four under the ring and
                // is drawn after it, and a pick made with the mouse leaves the pointer on the card,
                // whose portrait then stands 6% taller about its top - which slid the lower half
                // of "PLAYING AS" under the plate. From here it clears the plate lifted or not.
                var badge = HudPrimitives.Fill("Playing badge", portrait, CastSelectArt.Gold, 8);
                badge.anchorMin = badge.anchorMax = new Vector2(.5f, 0f);
                badge.pivot = new Vector2(.5f, .5f);
                badge.sizeDelta = new Vector2(86f, 17f);
                badge.anchoredPosition = new Vector2(0f, 12f);
                var playing = Line(badge, "PLAYING AS", 10f, UiTheme.Ink, 0f, 17f, 4f, 78f, TextAlignmentOptions.Center);
                if (playing != null && semibold != null) playing.font = semibold;
            }

            // The name on its plate, the nickname on its pill, the traits under both.
            float plateWidth = Mathf.Min(CardWidth - 16f, template.Name.Length * 8.4f + 30f);
            var nameplate = Plate(card, "Name plate", CastSelectArt.NamePlate(), -(14f + RingSize), plateWidth, 26f);
            var name = Line(nameplate, template.Name, 15f, Color.white, 0f, 26f, 8f, plateWidth - 16f, TextAlignmentOptions.Center);
            if (name != null && semibold != null) name.font = semibold;
            string nickname = Localisation.Text(template.Archetype);
            float pillWidth = Mathf.Min(CardWidth - 24f, nickname.Length * 6.6f + 24f);
            var pill = Plate(card, "Nickname", CastSelectArt.CategoryPill(template.Category), -(44f + RingSize), pillWidth, 20f);
            var nick = Line(pill, nickname, 12f, CastSelectArt.OnCategory(template.Category), 0f, 20f, 6f, pillWidth - 12f, TextAlignmentOptions.Center);
            if (nick != null) { var medium = UiTheme.Font(UiTheme.Weight.Medium); if (medium != null) nick.font = medium; }
            // Taller pills than the card had: at 18 the word was nine points, the smallest type on
            // the screen beside the house-size block (UI-UX-PASS-PLAN T0).
            Traits(card, template, CardWidth, -(70f + RingSize), TraitChipName, CardTraitHeight);
        }

        /// <summary>A card's trait pill: its word is half its height, so 22 draws the word at 11 in a box twice the type.</summary>
        public const float CardTraitHeight = 22f;

        /// <summary>A capsule plate centred on a card, <paramref name="y"/> from its top.</summary>
        private static RectTransform Plate(RectTransform parent, string name, Sprite sprite, float y, float plateWidth, float height)
        {
            var plate = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            plate.SetParent(parent, false);
            plate.anchorMin = plate.anchorMax = new Vector2(.5f, 1f);
            plate.pivot = new Vector2(.5f, 1f);
            plate.sizeDelta = new Vector2(plateWidth, height);
            plate.anchoredPosition = new Vector2(0f, y);
            var art = plate.GetComponent<Image>();
            art.sprite = sprite; art.type = Image.Type.Sliced; art.raycastTarget = false;
            art.pixelsPerUnitMultiplier = 40f / height;
            return plate;
        }

        /// <summary>The traits as pills, one each, tinted by what they mean, centred across <paramref name="span"/>.</summary>
        private static void Traits(RectTransform parent, CastTemplates.Template template, float span, float y,
            string chipName = TraitChipName, float height = 18f)
        {
            var words = template.Traits;
            if (words == null || words.Length == 0) return;
            var widths = new float[words.Length];
            float total = 0f;
            for (int i = 0; i < words.Length; i++)
            {
                string word = Localisation.Text(words[i]);
                widths[i] = Mathf.Max(50f, word.Length * height * .31f + 16f);
                total += widths[i];
            }
            total += 6f * (words.Length - 1);
            float x = (span - total) * .5f;
            for (int i = 0; i < words.Length; i++)
            {
                var chip = HudPrimitives.Chip(chipName, parent, Localisation.Text(words[i]), TraitTint(words[i]), widths[i], height);
                chip.anchorMin = new Vector2(0f, 1f);
                chip.anchorMax = new Vector2(0f, 1f);
                chip.pivot = new Vector2(0f, 1f);
                chip.anchoredPosition = new Vector2(x, y);
                x += widths[i] + 6f;
            }
        }

        // ---------------------------------------------------------------- details

        /// <summary>
        /// The picked houseguest, as the web game's detail panel and their glamour card show them:
        /// the name in gold over their live model turning in a pool of light, their occupation and
        /// nickname on pills, their age and home, their words, their traits and their kind of
        /// player - and the two ways on: play as them, or dress them first. With nobody picked, it
        /// says how to pick.
        ///
        /// <para>Edits the player made to this same houseguest in the creator and brought back are
        /// what both ways on use, and what the model shows. A draft of anybody else is not: it is
        /// named in the footer, where Start commits it, and "Play as Emma" never plays as Robin.</para>
        /// </summary>
        private void DetailPanel(RectTransform scrim, float x, float top, float panelWidth, float panelHeight, float reserved)
        {
            var panel = HudPrimitives.Fill(DetailPanelName, scrim, new Color(UiTheme.GlassFill.r, UiTheme.GlassFill.g, UiTheme.GlassFill.b, .94f), UiTheme.GlassRadius);
            // Anchored as the grid's viewport is, top and bottom, so the two always end together.
            panel.anchorMin = new Vector2(.5f, 0f);
            panel.anchorMax = new Vector2(.5f, 1f);
            panel.pivot = new Vector2(.5f, 1f);
            panel.sizeDelta = new Vector2(panelWidth, -reserved);
            panel.anchoredPosition = new Vector2(x, -top);
            panel.GetComponent<Image>().raycastTarget = true;
            UiTheme.AddBorder(panel, UiTheme.GlassRadius, new Color(CastSelectArt.Gold.r, CastSelectArt.Gold.g, CastSelectArt.Gold.b, .3f));

            var chosen = string.IsNullOrEmpty(selectedId) ? null : CastTemplates.Find(selectedId);
            if (chosen == null || chosen.Roster != roster)
            {
                if (studio != null) studio.gameObject.SetActive(false);
                PreviewedId = null;
                EmptyDetails(panel, panelWidth, Mathf.Max(260f, panelHeight));
                return;
            }

            var retained = RetainedFor(chosen);
            float inner = panelWidth - 40f;
            var (stage, quoteBlock) = DetailFit(panelHeight);
            float y = 16f;

            var name = Line(panel, chosen.Name, 30f, Color.white, -y, 40f, 20f, inner, TextAlignmentOptions.Center);
            if (name != null)
            {
                name.gameObject.name = "Detail name";
                var bold = UiTheme.Font(UiTheme.Weight.Bold); if (bold != null) name.font = bold;
                name.enableVertexGradient = true;
                name.colorGradient = new VertexGradient(UiTheme.Hex("FDE047"), UiTheme.Hex("FDE047"), UiTheme.Hex("FBBF24"), UiTheme.Hex("F59E0B"));
            }
            y += 44f;

            // The model, standing in a pool of light on the web game's gold ring.
            var stageRect = new GameObject("Model stage", typeof(RectTransform)).GetComponent<RectTransform>();
            stageRect.SetParent(panel, false);
            stageRect.anchorMin = stageRect.anchorMax = new Vector2(.5f, 1f);
            stageRect.pivot = new Vector2(.5f, 1f);
            stageRect.sizeDelta = new Vector2(inner, stage);
            stageRect.anchoredPosition = new Vector2(0f, -y);
            var (from, to) = CastSelectArt.CategoryColours(chosen.Category);
            var pool = new GameObject("Model light", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            pool.rectTransform.SetParent(stageRect, false);
            pool.rectTransform.anchorMin = pool.rectTransform.anchorMax = new Vector2(.5f, .5f);
            pool.rectTransform.sizeDelta = new Vector2(stage * 1.1f, stage * 1.1f);
            pool.sprite = CastSelectArt.Glow(); pool.color = new Color(from.r, from.g, from.b, .45f); pool.raycastTarget = false;
            var floor = new GameObject("Model floor", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            floor.rectTransform.SetParent(stageRect, false);
            floor.rectTransform.anchorMin = floor.rectTransform.anchorMax = new Vector2(.5f, 0f);
            floor.rectTransform.pivot = new Vector2(.5f, .5f);
            floor.rectTransform.sizeDelta = new Vector2(stage * .62f, stage * .12f);
            floor.rectTransform.anchoredPosition = new Vector2(0f, stage * .06f);
            floor.sprite = UiTheme.Ring(); floor.color = new Color(CastSelectArt.Gold.r, CastSelectArt.Gold.g, CastSelectArt.Gold.b, .8f); floor.raycastTarget = false;

            EnsureStudio(chosen, retained?.Appearance);
            liveModel = new GameObject(LiveModelName, typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            liveModel.rectTransform.SetParent(stageRect, false);
            liveModel.rectTransform.anchorMin = liveModel.rectTransform.anchorMax = new Vector2(.5f, 0f);
            liveModel.rectTransform.pivot = new Vector2(.5f, 0f);
            liveModel.rectTransform.sizeDelta = new Vector2(stage * .8f, stage);
            liveModel.rectTransform.anchoredPosition = new Vector2(0f, -stage * .02f);
            liveModel.texture = studio.Texture;
            liveModel.raycastTarget = true;
            var turntable = liveModel.gameObject.AddComponent<ModelTurntable>();
            turntable.Turn = degrees => { lastTurn = Time.unscaledTime; if (studio != null) studio.Rotate(degrees); };
            previewStatus = Line(stageRect, string.Empty, 12f, UiTheme.Muted, -(stage - 18f), 16f, 0f, inner, TextAlignmentOptions.Center);
            if (previewStatus != null) previewStatus.gameObject.name = "Preview status";
            y += stage + 8f;

            // Their job and their nickname, on the card's two pills.
            string occupation = Localisation.Text(chosen.Occupation ?? string.Empty);
            string nickname = Localisation.Text(chosen.Archetype ?? string.Empty);
            float occupationWidth = Mathf.Min(inner * .48f, occupation.Length * 7.6f + 30f);
            float nicknameWidth = Mathf.Min(inner * .48f, nickname.Length * 7.6f + 30f);
            float pillsLeft = -(occupationWidth + nicknameWidth + 10f) * .5f;
            var job = Plate(panel, "Occupation", CastSelectArt.NamePlate(), -y, occupationWidth, 28f);
            job.anchoredPosition = new Vector2(pillsLeft + occupationWidth * .5f, -y);
            Line(job, occupation, 14f, Color.white, 0f, 28f, 10f, occupationWidth - 20f, TextAlignmentOptions.Center);
            var nick = Plate(panel, "Nickname", CastSelectArt.CategoryPill(chosen.Category), -y, nicknameWidth, 28f);
            nick.anchoredPosition = new Vector2(pillsLeft + occupationWidth + 10f + nicknameWidth * .5f, -y);
            var nickWord = Line(nick, nickname, 14f, CastSelectArt.OnCategory(chosen.Category), 0f, 28f, 10f, nicknameWidth - 20f, TextAlignmentOptions.Center);
            if (nickWord != null) { var semi = UiTheme.Font(UiTheme.Weight.SemiBold); if (semi != null) nickWord.font = semi; }
            y += 36f;

            var facts = new List<string>();
            if (chosen.Age > 0) facts.Add(Localisation.Text("Age") + " " + chosen.Age);
            if (!string.IsNullOrEmpty(chosen.Hometown)) facts.Add(Localisation.Text(chosen.Hometown));
            Line(panel, string.Join("  ·  ", facts), 14f, UiTheme.Muted, -y, 20f, 20f, inner, TextAlignmentOptions.Center);
            y += 24f;

            if (!string.IsNullOrEmpty(chosen.Bio))
            {
                var quote = Line(panel, "“" + Localisation.Text(chosen.Bio) + "”", 14f, UiTheme.Paper, -y, quoteBlock - 4f, 24f, inner - 8f, TextAlignmentOptions.Top);
                if (quote != null)
                {
                    quote.gameObject.name = "Detail quote";
                    quote.fontStyle = FontStyles.Italic;
                    quote.textWrappingMode = TextWrappingModes.Normal;
                    quote.fontSizeMin = 11f;
                }
            }
            y += quoteBlock;

            var traitRow = new GameObject("Detail traits", typeof(RectTransform)).GetComponent<RectTransform>();
            traitRow.SetParent(panel, false);
            traitRow.anchorMin = traitRow.anchorMax = new Vector2(0f, 1f);
            traitRow.pivot = new Vector2(0f, 1f);
            traitRow.sizeDelta = new Vector2(panelWidth, 24f);
            traitRow.anchoredPosition = new Vector2(0f, -y);
            Traits(traitRow, chosen, panelWidth, 0f, "Detail trait", 24f);
            y += 32f;

            // Their kind of player, on its colours, and what that means.
            string kind = Localisation.Text(chosen.Category ?? string.Empty);
            string meaning = CastTemplates.CategoryDescription(chosen.Category);
            float kindWidth = kind.Length * 7.4f + 26f;
            var kindChip = Plate(panel, "Category", CastSelectArt.CategoryPill(chosen.Category), -y, kindWidth, 22f);
            kindChip.anchorMin = kindChip.anchorMax = new Vector2(0f, 1f);
            kindChip.pivot = new Vector2(0f, 1f);
            kindChip.anchoredPosition = new Vector2(20f, -y);
            Line(kindChip, kind, 12f, CastSelectArt.OnCategory(chosen.Category), 0f, 22f, 6f, kindWidth - 12f, TextAlignmentOptions.Center);
            if (meaning != null)
                Line(panel, Localisation.Text(meaning), 13f, UiTheme.Muted, -y, 22f, 30f + kindWidth, inner - kindWidth - 10f, TextAlignmentOptions.Left);
            y += 32f;

            // Play as them - the same commit as Start - or dress them in the creator first: as the
            // player left them when they have been in the creator, as their card has them if not.
            // Named by the name they will play under, which a trip through the creator can change.
            string first = FirstName(retained != null && !string.IsNullOrWhiteSpace(retained.Name) ? retained.Name : chosen.Name);
            bool customise = onCustomise != null;
            float playWidth = customise ? inner * .6f : inner;
            // Never under the panel's foot. The fit above keeps them in on every shape this screen
            // is laid out for; on a frame shorter still they cover the category line, not the footer.
            float buttonsY = Mathf.Min(y, Mathf.Max(0f, panelHeight - 46f - 12f));
            var play = PanelButton(panel, "Play as " + first, 20f, -buttonsY, playWidth, 46f, true, PlayAsChosen);
            HudPrimitives.Chevron(play.transform, Color.white, 12f).anchoredPosition = new Vector2(-18f, 0f);
            playButton = play;
            if (customise)
                PanelButton(panel, "Customize " + first, 20f + playWidth + 10f, -buttonsY, inner - playWidth - 10f, 46f, false,
                    () => OpenCreator(retained != null ? retained.Copy() : CharacterDraft.FromAppearance(chosen)));
        }

        /// <summary>
        /// How tall the model's stage and the quote are in a details panel this tall.
        ///
        /// <para>Everything else in the details is a fixed 238, and Play as has to end inside the
        /// panel on every frame shape. It used to take a fixed 448 at least, so on 21:9 at the
        /// larger text size - a 375 panel - and on 32:9 at the standard size, both buttons sat on
        /// top of Start and Cancel, which drew and took clicks over them. A short panel now gives
        /// the room back in order: the margin under the buttons, then the quote down to its first
        /// line, then the stage, which keeps at least 80 so there is still somebody standing on
        /// it.</para>
        /// </summary>
        private static (float stage, float quote) DetailFit(float panelHeight)
        {
            const float rest = 238f, quote = 70f, line = 26f, least = 140f;
            float stage = panelHeight - rest - quote - 34f;
            if (stage >= least) return (Mathf.Min(stage, 360f), quote);
            float room = panelHeight - rest - 12f;
            if (room - quote >= least) return (least, quote);
            if (room - least >= line) return (least, room - least);
            return (Mathf.Max(80f, room - line), line);
        }

        /// <summary>The retained draft when it is this houseguest's own - edited, brought back from the creator - and null otherwise.</summary>
        private CharacterDraft RetainedFor(CastTemplates.Template template) =>
            retainedDraft != null && template != null && string.Equals(retainedDraft.SourceTemplateId, template.Id, StringComparison.Ordinal)
                ? retainedDraft : null;

        /// <summary>
        /// Puts the keyboard back where the player was after a card press rebuilt the screen.
        ///
        /// <para>The press destroys the card it was made on, and when the selection dies the HUD's
        /// ring falls back to its first control, the top nav chip - after every pick, Play as was
        /// then 25 presses away and the player's place in the grid was lost. After a pick the next
        /// thing to press is Play as; after clearing one it is the same card, drawn again. The HUD
        /// keeps any selection that is still eligible when it rewires, so this one holds.</para>
        /// </summary>
        private void FocusAfterPick(string cardName)
        {
            var events = EventSystem.current;
            if (events == null || events.alreadySelecting) return;
            var card = playButton == null && gridContent != null ? gridContent.Find(cardName) : null;
            var next = playButton != null ? playButton.gameObject : card != null ? card.gameObject : null;
            if (next != null) events.SetSelectedGameObject(next);
        }

        /// <summary>The details before anyone is picked: an empty disc and how to fill it.</summary>
        private static void EmptyDetails(RectTransform panel, float panelWidth, float panelHeight)
        {
            float middle = panelHeight * .42f;
            var disc = HudPrimitives.Disc("Empty portrait", panel, new Color(UiTheme.Muted.r, UiTheme.Muted.g, UiTheme.Muted.b, .14f));
            disc.anchorMin = disc.anchorMax = new Vector2(.5f, 1f);
            disc.pivot = new Vector2(.5f, .5f);
            disc.sizeDelta = new Vector2(128f, 128f);
            disc.anchoredPosition = new Vector2(0f, -middle);
            var icon = UiTheme.Icon("houseguest");
            if (icon != null)
            {
                var art = new GameObject("Empty icon", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                art.rectTransform.SetParent(disc, false);
                art.rectTransform.anchorMin = Vector2.zero; art.rectTransform.anchorMax = Vector2.one;
                art.rectTransform.offsetMin = new Vector2(28f, 28f); art.rectTransform.offsetMax = new Vector2(-28f, -28f);
                art.sprite = icon; art.preserveAspect = true; art.raycastTarget = false;
                art.color = new Color(UiTheme.Muted.r, UiTheme.Muted.g, UiTheme.Muted.b, .7f);
            }
            var title = Line(panel, "Select a houseguest", 22f, UiTheme.Paper, -(middle + 84f), 30f, 20f, panelWidth - 40f, TextAlignmentOptions.Center);
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            if (title != null && semibold != null) title.font = semibold;
            Line(panel, "Choose from the cast to see their details", 15f, UiTheme.Muted, -(middle + 118f), 22f, 20f, panelWidth - 40f, TextAlignmentOptions.Center);
        }

        /// <summary>
        /// The studio, awake and showing <paramref name="template"/> - in <paramref name="look"/>
        /// when the player has dressed them, in their card's look when not. The studio ignores a
        /// look it is already showing, so a rebuild does not rebuild the body; the view is turned
        /// back to the front only for a different person.
        /// </summary>
        private void EnsureStudio(CastTemplates.Template template, CharacterAppearance look)
        {
            if (studio == null)
            {
                studio = CharacterStudioPreview.Create(StudioName);
                studio.Transparent = true;
                studio.FocusFace(false);
            }
            studio.gameObject.SetActive(true);
            studio.Show(look ?? CharacterAppearance.Preset(template.Id));
            if (PreviewedId == template.Id) return;
            PreviewedId = template.Id;
            studio.View(-20f);
            lastTurn = Time.unscaledTime;
        }

        /// <summary>A button in the details: the action blue for the commit, glass for the rest.</summary>
        private static Button PanelButton(RectTransform panel, string caption, float x, float y, float buttonWidth, float height, bool primary, Action action)
        {
            var button = Chip(panel, caption, 0f, buttonWidth, primary, action);
            var rect = (RectTransform)button.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(buttonWidth, height);
            rect.anchoredPosition = new Vector2(x, y);
            var words = button.GetComponentInChildren<TMP_Text>();
            if (words != null) { words.fontSizeMax = primary ? 19f : 16f; words.fontSize = words.fontSizeMax; }
            return button;
        }

        /// <summary>A houseguest's given name, skipping an honorific: "Dr. Will Kirby" plays as Will.</summary>
        internal static string FirstName(string name)
        {
            if (string.IsNullOrEmpty(name)) return string.Empty;
            var words = name.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            return words.FirstOrDefault(word => !word.EndsWith(".", StringComparison.Ordinal)) ?? words[0];
        }

        /// <summary>Turns the live model under a drag.</summary>
        private sealed class ModelTurntable : MonoBehaviour, IDragHandler
        {
            public Action<float> Turn;
            public void OnDrag(PointerEventData eventData) => Turn?.Invoke(-eventData.delta.x * .6f);
        }

        /// <summary>Lifts a card's portrait under the pointer, as the web game's grid does, unless motion is reduced.</summary>
        private sealed class CardHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
        {
            public RectTransform Target;
            public CastSelect Owner;
            public void OnPointerEnter(PointerEventData eventData)
            {
                if (Target != null && (Owner == null || !Owner.ReducedMotion)) Target.localScale = Vector3.one * 1.06f;
            }
            public void OnPointerExit(PointerEventData eventData) { if (Target != null) Target.localScale = Vector3.one; }
        }

        /// <summary>The colour a trait word is drawn in, wherever the HUD shows one: warm, calculating, competitive or combative.</summary>
        internal static Color TraitTint(string trait)
        {
            switch ((trait ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "social":
                case "loyal":
                case "emotional":
                case "charming": return UiTheme.Allied;
                case "strategic":
                case "analytical":
                case "sneaky":
                case "deceptive":
                case "manipulative": return UiTheme.Strategic;
                case "competitive": return UiTheme.Joke;
                case "confrontational": return UiTheme.Conflict;
                default: return UiTheme.Accent;
            }
        }

        /// <summary>The generated glyph a roster category wears, when the icon set exists.</summary>
        private static string CategoryIcon(string category)
        {
            switch ((category ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "strategist": return "bulb";
                case "competitor": return "trophy";
                case "socialite": return "people";
                case "wildcard": return "star";
                case "underdog": return "heart";
                default: return "houseguest";
            }
        }

        // ---------------------------------------------------------------- footer

        /// <summary>
        /// Who you will play as, the house size and the start, and a line on what size means. One row
        /// where the frame is wide enough - the house size on the left, the start and cancel on the
        /// right - and two where it is not.
        /// </summary>
        private void Footer()
        {
            Space(8f);
            if (!string.IsNullOrEmpty(resumeError))
                Text(resumeError, 14f, UiTheme.Warning, 64f, TextAlignmentOptions.Center);
            var chosen = string.IsNullOrEmpty(selectedId) ? null : CastTemplates.Find(selectedId);
            Text(retainedDraft != null ? "You will play as " + retainedDraft.Name + ". Your setup edits are retained."
                : chosen != null
                    ? "You will play as " + chosen.Name + ", " + chosen.Archetype.ToLowerInvariant() + "."
                    : "No card picked. You will play as an unaffiliated newcomer.",
                14f, chosen != null ? UiTheme.Gold : UiTheme.Muted, 22f, TextAlignmentOptions.Center);

            // The house-size block was the smallest type on the screen - its range line 12 in a
            // 16-tall box, the footnote 12, the chips' words shrunk to fit (cast-select;
            // UI-UX-PASS-PLAN T0, sweep row 23): 13 now, in boxes 1.3 times the type, on chips tall
            // enough to press.
            if (WideFooter)
            {
                var bar = Row(68f);
                const float group = 1168f;
                float left = -group * .5f;
                // 600 for the panel, 320 for the start: the chips beside the count want 140 each to
                // hold their words at thirteen.
                HouseSizePanel(bar, left, 600f, true);
                StartButtons(bar, left + 616f + 160f, 320f, left + 948f + 110f, 220f);
                Text("A shorter season reaches the final three sooner; it does not simplify a week.",
                    FootnoteSize, UiTheme.Muted, 20f, TextAlignmentOptions.Center);
            }
            else
            {
                var bar = Row(64f);
                HouseSizePanel(bar, -Mathf.Min(1040f, Width - Pad * 2f) * .5f, Mathf.Min(1040f, Width - Pad * 2f), false);
                Text("A shorter season reaches the final three sooner; it does not simplify a week.",
                    FootnoteSize, UiTheme.Muted, 20f, TextAlignmentOptions.Center);
                StartButtons(Row(60f), -130f, 420f, 230f, 250f);
            }
            Space(Pad * .5f);
        }

        /// <summary>The least the footer's lines are set at: the house-size range, the footnote and the chips' words.</summary>
        public const float FootnoteSize = 13f;
        /// <summary>The house-size chips' height: tall enough to read and to press beside the start.</summary>
        public const float HouseSizeChipHeight = 44f;
        public const string HouseSizePanelName = "House size panel", FooterTextName = "Text";

        /// <summary>The house size: what it is and its range, and the count between its two controls.</summary>
        private void HouseSizePanel(RectTransform bar, float left, float panelWidth, bool compact)
        {
            int largest = SeasonBuilder.LargestHouse(roster);
            var panel = HudPrimitives.Fill(HouseSizePanelName, bar, new Color(UiTheme.GlassFill.r, UiTheme.GlassFill.g, UiTheme.GlassFill.b, .92f), UiTheme.GlassRadius);
            panel.anchorMin = panel.anchorMax = new Vector2(.5f, .5f); panel.pivot = new Vector2(0f, .5f);
            panel.sizeDelta = new Vector2(panelWidth, compact ? 68f : 64f);
            panel.anchoredPosition = new Vector2(left, 0f);
            panel.GetComponent<Image>().raycastTarget = false;
            UiTheme.AddBorder(panel, UiTheme.GlassRadius, new Color(UiTheme.Hairline.r, UiTheme.Hairline.g, UiTheme.Hairline.b, .45f));
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            var heading = Line(panel, "HOUSE SIZE", 14f, UiTheme.Paper, -9f, 20f, 16f, 220f, TextAlignmentOptions.Left);
            if (heading != null && semibold != null) heading.font = semibold;
            // Two lines of 13 beside the chips, in a box 1.3 times each; one line across the wide panel.
            var range = Line(panel, "Between " + SeasonBuilder.MinimumHouse + " and " + largest + " on this roster, including you.",
                FootnoteSize, UiTheme.Muted, -31f, compact ? 34f : 20f, 16f, compact ? 188f : panelWidth * .5f - 16f, TextAlignmentOptions.TopLeft);
            if (range != null) { range.fontSizeMin = FootnoteSize; if (compact) range.textWrappingMode = TextWrappingModes.Normal; }

            // Fewer, the count, More - from the panel's right edge inward: 140-wide chips either side
            // of a 96-wide count, after the range line's 188 and a gap, in the 600 the panel has.
            float fewerX = left + panelWidth - 319f;
            float countX = left + panelWidth - 195f;
            float moreX = left + panelWidth - 71f;
            if (!compact) { fewerX = 55f; countX = 250f; moreX = 435f; }
            var caption = HudPrimitives.Label("House size", bar, 16f, UiTheme.Paper, TextAlignmentOptions.Center);
            if (semibold != null) caption.font = semibold;
            // The count and its noun; "including you" is the range line's, under the heading, where
            // a box between two chips held it only at eleven points.
            caption.text = houseSize + " houseguests";
            caption.rectTransform.anchorMin = caption.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            caption.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            caption.rectTransform.sizeDelta = new Vector2(compact ? 96f : 210f, compact ? 44f : 26f);
            caption.rectTransform.anchoredPosition = new Vector2(countX, 0f);
            caption.enableAutoSizing = true; caption.fontSizeMax = compact ? 15f : 16f; caption.fontSizeMin = FootnoteSize;
            caption.textWrappingMode = compact ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;

            HouseSizeChip(bar, "Fewer houseguests", fewerX, compact ? 140f : 150f, () =>
            {
                houseSize = SeasonBuilder.ClampHouseSize(roster, Math.Max(customHouseguests.Count + 1, houseSize - 1));
                Rebuild();
            });
            HouseSizeChip(bar, "More houseguests", moreX, compact ? 140f : 150f, () =>
            {
                houseSize = SeasonBuilder.ClampHouseSize(roster, houseSize + 1);
                Rebuild();
            });
        }

        /// <summary>One of the house size's two chips: the footer's pill, taller, its words never under thirteen.</summary>
        private static Button HouseSizeChip(RectTransform bar, string caption, float x, float width, Action press)
        {
            var chip = Chip(bar, caption, x, width, false, press);
            ((RectTransform)chip.transform).sizeDelta = new Vector2(width, HouseSizeChipHeight);
            var words = chip.GetComponentInChildren<TMP_Text>();
            if (words != null) words.fontSizeMin = FootnoteSize;
            return chip;
        }

        /// <summary>The one control this screen exists to reach - wide, the action blue, an arrow saying it leads on - and cancel.</summary>
        private void StartButtons(RectTransform bar, float startX, float startWidth, float cancelX, float cancelWidth)
        {
            var start = Chip(bar, StartCaption, startX, startWidth, true, StartSeason);
            ((RectTransform)start.transform).sizeDelta = new Vector2(startWidth, 54f);
            var startWords = start.GetComponentInChildren<TMP_Text>();
            if (startWords != null) { startWords.fontSizeMax = 20f; startWords.fontSize = 20f; }
            HudPrimitives.Chevron(start.transform, Color.white, 14f).anchoredPosition = new Vector2(-22f, 0f);
            var cancel = Chip(bar, CancelCaption, cancelX, cancelWidth, false, Dismiss);
            ((RectTransform)cancel.transform).sizeDelta = new Vector2(cancelWidth, 46f);
        }

        /// <summary>
        /// The footer's commit: the roster, the size, the pick or the retained draft, and the custom
        /// slots, handed back. The retained draft wins whoever it is, as the line above Start says.
        /// </summary>
        private void StartSeason() => Commit(retainedDraft);

        /// <summary>
        /// The details' commit: the same, as the houseguest the Play as button names - their own
        /// retained edits if the player brought some back from the creator, their card if not.
        /// A draft of somebody else used to win here too, so "Play as Emma" after building Robin
        /// started the season as Robin, with Emma cast as one of the others.
        /// </summary>
        private void PlayAsChosen() => Commit(RetainedFor(string.IsNullOrEmpty(selectedId) ? null : CastTemplates.Find(selectedId)));

        private void Commit(CharacterDraft authored)
        {
            var chosen = string.IsNullOrEmpty(selectedId) ? null : CastTemplates.Find(selectedId);
            var choice = new SeasonBuilder.Choice
            {
                Roster = roster,
                PlayerTemplateId = selectedId,
                HouseSize = SeasonBuilder.ClampHouseSize(roster, houseSize),
                CustomHouseguests = customHouseguests.Select(profile => profile.Clone()).ToList(),
            };
            if (authored != null) choice.Authored = authored.Copy();
            else if (chosen != null)
            {
                choice.Authored = CharacterDraft.FromAppearance(chosen);
                var catalog = (CharacterBodySource.Provider as IModularCharacterBodyProvider)?.Catalog;
                if (catalog != null) choice.Authored.Appearance = catalog.Materialize(choice.Authored.Appearance);
            }
            var start = onStart;
            Hide();
            // Closing for good: a failed start comes back through Resume, which builds a new studio.
            ReleaseStudio();
            start?.Invoke(choice);
        }

        /// <summary>
        /// Hands the current roster, size and card over to the creator.
        ///
        /// <para>The screen hides rather than closing: the creator's back control brings it straight
        /// back, and re-showing it would reset the roster, the filter and the pick the player has
        /// just spent time on.</para>
        /// </summary>
        private void OpenCreator(CharacterDraft start)
        {
            var catalog = (CharacterBodySource.Provider as IModularCharacterBodyProvider)?.Catalog;
            if (catalog != null) start.Appearance = catalog.Materialize(start.Appearance);
            var choice = new SeasonBuilder.Choice
            {
                Roster = roster,
                PlayerTemplateId = selectedId,
                HouseSize = SeasonBuilder.ClampHouseSize(roster, houseSize),
                CustomHouseguests = customHouseguests.Select(profile => profile.Clone()).ToList(),
            };
            var customise = onCustomise;
            Hide();
            customise?.Invoke(choice, start);
        }

        private void EditCastSlot(int slot)
        {
            if (creator == null || slot < 0 || slot >= customHouseguests.Count) return;
            var profile = customHouseguests[slot];
            var draft = profile.ToDraft();
            var catalog = (CharacterBodySource.Provider as IModularCharacterBodyProvider)?.Catalog;
            if (catalog != null) draft.Appearance = catalog.Materialize(draft.Appearance);
            var choice = new SeasonBuilder.Choice
            {
                Roster = roster, PlayerTemplateId = selectedId, HouseSize = houseSize,
                CustomHouseguests = customHouseguests.Select(value => value.Clone()).ToList(),
            };
            Hide();
            creator.ShowForCastSlot(choice, draft, changed =>
            {
                // This changes the proposed cast instance only, not its library profile or the player's setup.
                customHouseguests[slot] = CharacterProfile.FromDraft(profile.id, changed);
                Resume();
            }, Resume);
        }

        /// <summary>Brings the screen back with the player's roster, filter and pick intact.</summary>
        public void Resume() => Resume(null);

        /// <summary>Returns a failed season start to its unchanged setup with a visible reason.</summary>
        public void Resume(string error)
        {
            if (onStart == null) return;
            resumeError = error;
            Rebuild();
            group.alpha = 1f;
            group.blocksRaycasts = true;
            group.interactable = true;
        }

        // ---------------------------------------------------------------- pieces

        /// <summary>
        /// A pill-shaped control. <paramref name="active"/> only changes its colours — every chip is
        /// a button with its own words, so which one is selected is never carried by tint alone.
        /// </summary>
        private static Button Chip(Transform parent, string text, float x, float width, bool active, Action action)
        {
            // The action blue for the active pill: Paper on AccentDeep is under the 4.5 a 14px label needs.
            var pill = HudPrimitives.Fill(text, parent, active ? UiTheme.ActionBlue : UiTheme.GlassFill, 18);
            pill.anchorMin = new Vector2(0.5f, 0.5f);
            pill.anchorMax = new Vector2(0.5f, 0.5f);
            pill.pivot = new Vector2(0.5f, 0.5f);
            pill.sizeDelta = new Vector2(width, 38f);
            pill.anchoredPosition = new Vector2(x, 0f);

            // The active pill is the bright one, and it is the only one that glows. The resting pills
            // keep the cyan hairline every panel edge carries.
            if (active)
            {
                UiTheme.AddGlow(pill, 18);
                // Not UiTheme.Edge(Emphasis.Active): that is for the one thing the player is meant to
                // act on now, and this screen always shows several active pills at once.
                UiTheme.AddBorder(pill, 18, new Color(UiTheme.Paper.r, UiTheme.Paper.g, UiTheme.Paper.b, 0.35f));
            }
            else
            {
                UiTheme.AddBorder(pill, 18, new Color(UiTheme.Hairline.r, UiTheme.Hairline.g, UiTheme.Hairline.b, 0.45f));
            }

            var image = pill.GetComponent<Image>();
            image.raycastTarget = true;

            var label = HudPrimitives.Label("Label", pill, 16f,
                active ? Color.white : UiTheme.Paper, TextAlignmentOptions.Center);
            var weight = UiTheme.Font(active ? UiTheme.Weight.SemiBold : UiTheme.Weight.Medium);
            if (weight != null) label.font = weight;
            label.text = Localisation.Text(text);
            label.enableAutoSizing = true; label.fontSizeMax = 16f; label.fontSizeMin = 10f;
            // One line, shrinking to fit, rather than breaking onto a second line inside a one-line pill.
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(8f, 0f);
            label.rectTransform.offsetMax = new Vector2(-8f, 0f);

            var button = pill.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => action());
            return button;
        }

        /// <summary>
        /// One line of text, measured from its parent's own left edge so a piece can lay out its
        /// words without guessing where anything else ends.
        /// </summary>
        private static TMP_Text Line(Transform card, string value, float size, Color colour, float y,
            float height, float x, float width, TextAlignmentOptions align)
        {
            if (value == null) return null;
            var label = HudPrimitives.Label("Line", card, size, colour, align);
            label.text = Localisation.Text(value);
            var rect = label.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(x, y);
            label.enableAutoSizing = true; label.fontSizeMax = label.fontSize; label.fontSizeMin = Mathf.Min(9f, label.fontSize);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;
            return label;
        }

        // ---------------------------------------------------------------- layout

        /// <summary>
        /// The brand in the corner, as every mockup carries it: the house mark and the name in the
        /// title blue, lit from above, and the strap under it.
        /// </summary>
        private static void Brand(RectTransform scrim)
        {
            var brand = new GameObject("Brand", typeof(RectTransform)).GetComponent<RectTransform>();
            brand.SetParent(scrim, false);
            brand.anchorMin = brand.anchorMax = new Vector2(0f, 1f);
            brand.pivot = new Vector2(0f, 1f);
            brand.sizeDelta = new Vector2(300f, 64f);
            brand.anchoredPosition = new Vector2(32f, -26f);
            float left = 0f;
            var houseMark = UiTheme.Pack(PackArt.IconHome);
            if (houseMark != null)
            {
                var mark = new GameObject("Brand mark", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                mark.rectTransform.SetParent(brand, false);
                mark.rectTransform.anchorMin = mark.rectTransform.anchorMax = new Vector2(0f, 1f);
                mark.rectTransform.pivot = new Vector2(0f, 1f);
                mark.rectTransform.sizeDelta = new Vector2(52f, 52f);
                mark.rectTransform.anchoredPosition = new Vector2(0f, -2f);
                mark.sprite = houseMark; mark.color = UiTheme.Heading; mark.preserveAspect = true; mark.raycastTarget = false;
                left = 62f;
            }
            var word = HudPrimitives.Label("Brand word", brand, 36f, Color.white, TextAlignmentOptions.TopLeft);
            word.text = Localisation.Text("GAMESIM");
            var bold = UiTheme.Font(UiTheme.Weight.Bold);
            if (bold != null) word.font = bold;
            word.characterSpacing = 2f;
            word.enableVertexGradient = true;
            word.colorGradient = new VertexGradient(UiTheme.Hex("6CC0FF"), UiTheme.Hex("6CC0FF"), UiTheme.Hex("3A86FF"), UiTheme.Hex("3A86FF"));
            word.rectTransform.anchorMin = word.rectTransform.anchorMax = new Vector2(0f, 1f);
            word.rectTransform.pivot = new Vector2(0f, 1f);
            word.rectTransform.sizeDelta = new Vector2(300f - left, 46f);
            word.rectTransform.anchoredPosition = new Vector2(left, 0f);
            var strap = HudPrimitives.Label("Brand strap", brand, 14f, UiTheme.Muted, TextAlignmentOptions.TopLeft);
            strap.text = Localisation.Text("THE HOUSE");
            strap.characterSpacing = 3f;
            strap.rectTransform.anchorMin = strap.rectTransform.anchorMax = new Vector2(0f, 1f);
            strap.rectTransform.pivot = new Vector2(0f, 1f);
            strap.rectTransform.sizeDelta = new Vector2(300f - left, 20f);
            strap.rectTransform.anchoredPosition = new Vector2(left + 2f, -44f);
        }

        /// <summary>The spaced lines in the lower corners, in the house's own voice.</summary>
        private static void CornerLines(RectTransform scrim)
        {
            CornerLine(scrim, "Corner line left", "PEOPLE PLAY DIFFERENT\nSTORIES HERE.", 0f, TextAlignmentOptions.BottomLeft);
            CornerLine(scrim, "Corner line right", "STRATEGY. FRIENDSHIPS.\nBIGGER STORIES.", 1f, TextAlignmentOptions.BottomRight);
        }

        private static void CornerLine(RectTransform scrim, string name, string words, float side, TextAlignmentOptions align)
        {
            var line = HudPrimitives.Label(name, scrim, 14f, new Color(UiTheme.Muted.r, UiTheme.Muted.g, UiTheme.Muted.b, .8f), align);
            line.text = Localisation.Text(words);
            line.characterSpacing = 6f;
            line.lineSpacing = 12f;
            line.rectTransform.anchorMin = line.rectTransform.anchorMax = new Vector2(side, 0f);
            line.rectTransform.pivot = new Vector2(side, 0f);
            line.rectTransform.sizeDelta = new Vector2(Mathf.Min(300f, SideDressing * 2f - 40f), 60f);
            line.rectTransform.anchoredPosition = new Vector2(side < .5f ? 32f : -32f, 34f);
        }

        private RectTransform Row(float height)
        {
            var row = HudPrimitives.Fill("Row", content, new Color(0f, 0f, 0f, 0f), 1);
            Place(row, Width - Pad * 2f, height, -cursor);
            cursor += height + 6f;
            return row;
        }

        private void Text(string value, float size, Color colour, float height, TextAlignmentOptions align)
        {
            var label = HudPrimitives.Label("Text", content, size, colour, align);
            label.text = Localisation.Text(value);
            Place(label.rectTransform, Width - Pad * 2f, height, -cursor);
            cursor += height;
        }

        private void Space(float amount) => cursor += amount;

        private static void Place(RectTransform rect, float width, float height, float y)
        {
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(0f, y);
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = Vector2.zero;
            rect.anchoredPosition = Vector2.zero;
        }
    }
}
