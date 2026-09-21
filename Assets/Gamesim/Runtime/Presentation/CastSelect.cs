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
    /// <para>Every card is a real <see cref="Button"/> whose label is the houseguest's name, so the
    /// grid is reachable by keyboard and announced by name rather than by position. The selected
    /// card is marked with a border <b>and</b> a word — "Playing as" — because a colour on its own
    /// is not a state a screen reader can report.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CastSelect : MonoBehaviour
    {
        /// <summary>
        /// The widest the screen draws, and how it decides to draw narrower.
        ///
        /// <para>This was a fixed 1180 in a canvas whose reference is 1920x1080, so the roster sat
        /// in 61 % of the frame with a sixth of it empty down each side. It cannot simply become
        /// 1560, because the scaler matches width and height equally and the canvas width in
        /// reference units is <c>sqrt(aspect) * 1200</c> at the larger text size - 1600 at 16:9 but
        /// 1518 on 16:10, 1470 on 3:2 and 1386 on 4:3. A constant wide enough for the first runs off
        /// both edges of the last, and nothing in the suite can see it happen. So it is measured
        /// from the canvas at every rebuild instead.</para>
        /// </summary>
        private const float MaxWidth = 1560f;
        private const float MinWidth = 900f;
        private const float FrameMargin = 96f;
        private const float Pad = 28f;
        private const int Columns = 4;
        private const float Gutter = 16f;

        /// <summary>
        /// A card is wide and short now rather than nearly square.
        ///
        /// <para>216 never fitted: three rows of 216 plus gutters is 676 against a viewport of
        /// 595 at 16:9, so the bottom row of the roster was below the fold and had to be scrolled
        /// to - on the one screen whose whole job is to show you the cast. At 168 the three rows
        /// come to 536 and the whole house is on screen at the standard text size. At the larger
        /// size the viewport is 470 and it still scrolls, by about a card's worth; that is the
        /// honest cost of bigger type rather than something to hide.</para>
        /// </summary>
        private const float CardHeight = 168f;
        /// <summary>The face's diameter. The ring is three pixels of brass outside it.</summary>
        private const float PortraitSize = 104f;
        /// <summary>
        /// How much of the portrait render is thrown away by the mask to fill the circle, and how
        /// far the kept part is lifted.
        ///
        /// <para>Tuned against a captured frame, not reasoned about. 1.42 with a lift of 0.32 filled
        /// the circle and cut the chin off at the bottom edge; this keeps the whole head with the
        /// crop biting into the shoulders instead, which is where the reference's portraits end.</para>
        /// </summary>
        private const float PortraitOverscan = 1.30f;
        private const float PortraitLift = 0.14f;

        /// <summary>
        /// What the two fixed bars reserve, and therefore what the roster gets.
        ///
        /// <para>These were 270, 485 and 270 written separately at three call sites, so the
        /// viewport's height and the header's height were free to disagree - and did. They are one
        /// number each now and the viewport is derived from both.</para>
        /// </summary>
        private const float HeaderHeight = 268f;
        private const float FooterHeight = 196f;

        private float width = 1180f;
        private float Width => width;
        private float CardWidth => (width - Pad * 2f - Gutter * (Columns - 1)) / Columns;

        /// <summary>The caption the start control carries. Tests and the tour find it by this text.</summary>
        public const string StartCaption = "Start this season";
        public const string CancelCaption = "Cancel — keep this season";

        /// <summary>Names the card's parts carry, so a test can find them without guessing.</summary>
        public const string CardGlowName = "Glow";
        public const string TraitChipName = "Trait";

        private RectTransform content;
        private CanvasGroup group;
        private float cursor;

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
            foreach (var item in portraits)
            {
                if (item.Key == null || item.Key.texture != null) continue;
                var texture = CharacterPortraits.Get(item.Value);
                if (texture == null) continue;
                item.Key.texture = texture;
                item.Key.color = Color.white;
            }
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
        }

        /// <summary>
        /// Opens the screen. <paramref name="start"/> receives the choice when the player commits;
        /// <paramref name="cancel"/> runs when they back out, and nothing is built in that case.
        /// </summary>
        public void Show(Action<SeasonBuilder.Choice> start, Action cancel) => Show(start, cancel, null);

        /// <summary>
        /// The same, with the creator attached. <paramref name="customise"/> receives the choice as
        /// it stands and the draft to open — built from the selected card, or blank when there is
        /// none. Without it the two creator controls are simply not drawn, so a caller that has no
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
            cancel?.Invoke();
        }

        // ---------------------------------------------------------------- build

        private void Rebuild()
        {
            portraits.Clear();
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
            float frame = ((RectTransform)transform).rect.width;
            width = Mathf.Clamp((frame > 1f ? frame : 1920f) - FrameMargin, MinWidth, MaxWidth);

            // Opaque. It was 0.97, which sounds like nothing and is not: three percent of the
            // episode HUD's white-on-navy chrome is legible, so the Notebook row, the objective
            // card, the live feed, the lower third and the cast strip all ghosted through the
            // roster. A chooser this consequential should not have the game showing through it.
            var scrim = HudPrimitives.Fill("Scrim", transform, UiTheme.Background, 1);
            // A modal's scrim has to catch the mouse; Fill leaves its art non-interactive.
            scrim.GetComponent<UnityEngine.UI.Image>().raycastTarget = true;
            Stretch(scrim);

            content = new GameObject("Fixed setup navigation", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(scrim, false);
            content.anchorMin = content.anchorMax = new Vector2(.5f, 1f);
            content.pivot = new Vector2(.5f, 1f);
            content.sizeDelta = new Vector2(Width, HeaderHeight);
            cursor = 0f;
            Header(); SetupNavigation();
            if (!libraryMode && !castSlotsMode) { RosterTabs(); CategoryChips(); }

            var viewport = HudPrimitives.Fill("Viewport", scrim, new Color(0f, 0f, 0f, 0f), 1);
            viewport.anchorMin = new Vector2(0.5f, 0f);
            viewport.anchorMax = new Vector2(0.5f, 1f);
            viewport.pivot = new Vector2(0.5f, 1f);
            float errorHeight = string.IsNullOrEmpty(resumeError) ? 0f : 64f;
            viewport.sizeDelta = new Vector2(Width, -(HeaderHeight + FooterHeight + 10f) - errorHeight);
            viewport.anchoredPosition = new Vector2(0f, -HeaderHeight);
            viewport.gameObject.AddComponent<RectMask2D>();

            content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(viewport, false);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;

            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            viewport.gameObject.AddComponent<SetupScrollFocus>();
            scroll.content = content;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;

            cursor = 0f;
            if (castSlotsMode) CastSlots(); else if (libraryMode) LibraryCards(); else Grid();
            content.sizeDelta = new Vector2(0f, cursor + Pad);

            content = new GameObject("Fixed season footer", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(scrim, false);
            content.anchorMin = content.anchorMax = new Vector2(.5f, 0f);
            content.pivot = new Vector2(.5f, 0f);
            content.sizeDelta = new Vector2(Width, FooterHeight + errorHeight);
            cursor = 0f;
            HouseSize(); Footer();
        }

        private void SetupNavigation()
        {
            if (onCustomise == null) return;
            var row = Row(44f);
            Chip(row, "Choose a houseguest", -448f, 216f, !libraryMode && !castSlotsMode, () => { libraryMode = false; castSlotsMode = false; Rebuild(); });
            Chip(row, CharacterCreator.CreateCaption, -224f, 216f, false, () => OpenCreator(CharacterDraft.Blank()));
            Chip(row, retainedDraft == null ? CharacterCreator.CustomiseCaption : "Resume setup", 0f, 216f, false, () =>
            {
                if (retainedDraft != null) { OpenCreator(retainedDraft.Copy()); return; }
                var chosen = CastTemplates.Find(selectedId);
                OpenCreator(chosen == null ? CharacterDraft.Blank() : CharacterDraft.FromAppearance(chosen));
            });
            Chip(row, "My Houseguests", 224f, 216f, libraryMode, () => { libraryMode = true; castSlotsMode = false; Rebuild(); });
            Chip(row, "Cast slots", 448f, 216f, castSlotsMode, () => { castSlotsMode = true; libraryMode = false; Rebuild(); });
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
            Space(Pad);
            // The mockup sets the title in white with the strap in a muted blue-grey under it. The
            // words are the build's own, unchanged; only the weight and the colour move.
            var title = HudPrimitives.Heading("Text", content, 34f, UiTheme.Paper, TextAlignmentOptions.Center);
            // ONE localisation key, looked up once. The reference draws the last word in the accent,
            // and the tempting way to get that - two adjacent labels - would split
            // "CHOOSE YOUR HOUSEGUEST" into two keys, neither of which matches the entry and one of
            // which ("CHOOSE YOUR ") is untranslatable on its own. So the lookup stays whole and the
            // RESULT is marked up. Rich text is off by default in HudPrimitives.Label, for the good
            // reason that engine and player strings can contain angle brackets; this label's content
            // is a fixed literal that has already been through Localisation, so it is safe here and
            // nowhere else. A string with no space, or a table that returns one, simply gets one
            // colour - the split is English word order and is allowed to degrade.
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
            // Was 8, which at 30px spread the title across most of the frame and read as a banner
            // rather than as a heading. The reference tracks its display type barely at all.
            title.characterSpacing = 2f;
            Place(title.rectTransform, Width - Pad * 2f, 46f, -cursor);
            cursor += 46f;
            Text("Pick who you play as, then set the size of the house. Everyone else is cast from the same roster.",
                15f, UiTheme.Muted, 24f, TextAlignmentOptions.Center);
            Space(8f);
        }

        private void RosterTabs()
        {
            var bar = Row(42f);
            var options = Enum.GetValues(typeof(CastTemplates.Roster)).Cast<CastTemplates.Roster>().ToList();
            const float span = 260f;
            float x = -(options.Count - 1) * span / 2f;
            foreach (var option in options)
            {
                var pick = option;
                Chip(bar, CastTemplates.RosterName(pick), x, span - 10f, roster == pick, () =>
                {
                    if (roster == pick) return;
                    roster = pick;
                    // A card from the other roster is not in this one, so the pick cannot survive
                    // the switch — better to clear it than to start a season with a stale persona.
                    selectedId = null;
                    houseSize = SeasonBuilder.ClampHouseSize(roster, houseSize);
                    Rebuild();
                });
                x += span;
            }
            Space(2f);
        }

        private void CategoryChips()
        {
            var chips = new List<string> { CastTemplates.AllCategories };
            chips.AddRange(CastTemplates.Categories);

            var bar = Row(38f);
            float span = Mathf.Min(196f, (Width - Pad * 2f) / chips.Count);
            float x = -(chips.Count - 1) * span / 2f;
            foreach (var name in chips)
            {
                var pick = name;
                Chip(bar, pick, x, span - 12f, string.Equals(category, pick, StringComparison.OrdinalIgnoreCase),
                    () => { category = pick; Rebuild(); });
                x += span;
            }
            Space(6f);
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

            // The reference holds the whole roster in one bordered panel rather than letting the
            // cards float on the ground. Built before the cards so it sits behind them.
            int rows = (shown.Count + Columns - 1) / Columns;
            float gridHeight = rows * (CardHeight + Gutter) - Gutter;
            var frame = HudPrimitives.Fill("Roster", content, UiTheme.CardFill, UiTheme.GlassRadius);
            frame.anchorMin = new Vector2(0.5f, 1f);
            frame.anchorMax = new Vector2(0.5f, 1f);
            frame.pivot = new Vector2(0.5f, 1f);
            frame.sizeDelta = new Vector2(Width - Pad * 2f + 24f, gridHeight + 24f);
            frame.anchoredPosition = new Vector2(0f, -(cursor - 12f));
            UiTheme.AddBorder(frame, UiTheme.GlassRadius,
                new Color(UiTheme.Hairline.r, UiTheme.Hairline.g, UiTheme.Hairline.b, 0.30f));

            for (int index = 0; index < shown.Count; index++)
            {
                int column = index % Columns;
                if (column == 0 && index > 0) cursor += CardHeight + Gutter;
                Card(shown[index], column, -cursor);
            }
            cursor += CardHeight + Gutter;
        }

        /// <summary>
        /// One card of mockup-02's grid: the face on the left, the name and the archetype beside it,
        /// the age-and-occupation line under both, the traits as pills, and the category in caps
        /// along the bottom edge.
        ///
        /// <para>Every word on it is the copy the build already shipped — the mockup reuses the
        /// game's own strings, so the rebuild is arrangement and dress, never new wording. The one
        /// thing that changes shape is the traits line, which becomes the mockup's two pills rather
        /// than a middle-dotted sentence; the words are the same two words.</para>
        /// </summary>
        private void Card(CastTemplates.Template template, int column, float y)
        {
            bool chosen = string.Equals(selectedId, template.Id, StringComparison.Ordinal);

            var card = HudPrimitives.Fill(template.Name, content, UiTheme.CardFill, UiTheme.GlassRadius);
            card.anchorMin = new Vector2(0.5f, 1f);
            card.anchorMax = new Vector2(0.5f, 1f);
            card.pivot = new Vector2(0f, 1f);
            card.sizeDelta = new Vector2(CardWidth, CardHeight);
            card.anchoredPosition = new Vector2(
                -Width / 2f + Pad + column * (CardWidth + Gutter), y);

            // The mockup's selected card is the one that glows; the rest carry the hairline alone.
            // The glow is decoration on top of a state the card also states in words below.
            if (chosen)
            {
                UiTheme.Glass(card, UiTheme.GlassRadius);
                UiTheme.AddBorder(card, UiTheme.GlassRadius, UiTheme.Glow);
                // Glass paints GlassFill over whatever the card was given, so the ground has to be
                // written back afterwards or the one card the player picked is the one card that
                // loses it. It was GlassFill on a scrim of Background - the same hex, under one
                // percent apart - so every card was a hairline around nothing.
                UiTheme.Style(card.GetComponent<Image>(), UiTheme.CardFill, UiTheme.GlassRadius);
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
            button.onClick.AddListener(() =>
            {
                retainedDraft = null;
                selectedId = string.Equals(selectedId, pick, StringComparison.Ordinal) ? null : pick;
                Rebuild();
            });

            // The face. No houseguest has a body before a season exists, so the card shows their
            // wardrobe colour and their initials — the same colour they will be wearing in the
            // house, which is what makes the grid something you can read at a glance.
            var wardrobe = CastPalette.For(template.Id);
            // Brass at rest, cyan when picked. Outline is #3A5068 at 69 % on a near-black card - a
            // three-pixel ring nobody could see - and the reference rings every face in bronze,
            // which is most of what makes them read as portraits instead of avatars. Not Gold: gold
            // is power in this build and gilding twelve rings at once would spend it on nothing.
            var rim = HudPrimitives.Disc("Ring", card, chosen ? UiTheme.Glow : UiTheme.Brass);
            rim.anchorMin = new Vector2(0f, 1f);
            rim.anchorMax = new Vector2(0f, 1f);
            rim.pivot = new Vector2(0.5f, 1f);
            rim.sizeDelta = new Vector2(PortraitSize + 6f, PortraitSize + 6f);
            rim.anchoredPosition = new Vector2(20f + (PortraitSize + 6f) * 0.5f, -(CardHeight - PortraitSize - 6f) * 0.5f);

            var face = HudPrimitives.Disc("Face", rim, wardrobe);
            face.anchorMin = new Vector2(0.5f, 0.5f);
            face.anchorMax = new Vector2(0.5f, 0.5f);
            face.pivot = new Vector2(0.5f, 0.5f);
            face.sizeDelta = new Vector2(PortraitSize, PortraitSize);
            face.anchoredPosition = Vector2.zero;
            // THE defect this card had. The rendered portrait is a square crop with an opaque navy
            // ground (CharacterPortraits clears to UiTheme.Background), and it was stretched over
            // the round Face disc with nothing clipping it - so the corners overhung the ring by
            // fourteen pixels and every houseguest was a black rectangle sitting on top of the
            // circle and the ring that were supposed to frame them. CastRail has always masked its
            // portraits this way; this screen never did. The Disc already carries UiTheme.Circle,
            // which is the alpha the stencil needs.
            var mask = face.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = true;
            var portraitObject = new GameObject("Model portrait", typeof(RectTransform), typeof(RawImage));
            portraitObject.transform.SetParent(face, false);
            var modelPortrait = portraitObject.GetComponent<RawImage>();
            modelPortrait.raycastTarget = false;
            modelPortrait.color = Color.clear;
            // Bigger than the circle that clips it, and lifted. The render is a head-and-shoulders
            // crop on an opaque ground, so fitting it exactly inside the disc leaves a ring of dead
            // navy around a small head - the circle is filled but the FACE is not. Overscanning it
            // and letting the mask take the corners is the whole point of having a mask: the head
            // fills the frame the way the reference's portraits do. The lift is because the crop
            // centres on the chest, not the face.
            var portraitRect = modelPortrait.rectTransform;
            portraitRect.anchorMin = new Vector2(0.5f, 0.5f);
            portraitRect.anchorMax = new Vector2(0.5f, 0.5f);
            portraitRect.pivot = new Vector2(0.5f, 0.5f);
            portraitRect.sizeDelta = new Vector2(PortraitSize * PortraitOverscan, PortraitSize * PortraitOverscan);
            portraitRect.anchoredPosition = new Vector2(0f, -PortraitSize * (PortraitOverscan - 1f) * PortraitLift);
            var cardCharacter = CastTemplates.ToContestant(template, false);
            portraits.Add(new KeyValuePair<RawImage, ContestantState>(modelPortrait, cardCharacter));

            // The generated silhouette where the icon pass has been run, and the initials where it
            // has not. A silhouette is the honest answer to "who is this" before a body exists to
            // render — inventing a portrait would be inventing a person's appearance — and the
            // initials remain so that a clone without the art still tells the cards apart.
            var silhouette = UiTheme.Icon("houseguest");
            if (silhouette != null)
            {
                var art = new GameObject("Silhouette", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
                art.SetParent(face, false);
                art.anchorMin = new Vector2(0.5f, 0f);
                art.anchorMax = new Vector2(0.5f, 0f);
                art.pivot = new Vector2(0.5f, 0f);
                art.sizeDelta = new Vector2(PortraitSize * 0.8f, PortraitSize * 0.8f);
                art.anchoredPosition = new Vector2(0f, 5f);
                var portrait = art.GetComponent<Image>();
                portrait.sprite = silhouette;
                portrait.color = UiTheme.OnColor(wardrobe);
                portrait.preserveAspect = true;
                portrait.raycastTarget = false;
            }
            else
            {
                var initials = HudPrimitives.Label("Initials", face, 26f,
                    UiTheme.OnColor(wardrobe), TextAlignmentOptions.Center);
                initials.text = Initials(template.Name);
                initials.rectTransform.anchorMin = Vector2.zero;
                initials.rectTransform.anchorMax = Vector2.one;
                initials.rectTransform.offsetMin = Vector2.zero;
                initials.rectTransform.offsetMax = Vector2.zero;
            }
            portraitObject.transform.SetAsLastSibling();

            // The category's glyph in the card's upper-right, as every mockup card carries one. It
            // repeats the word along the bottom edge, so it never carries anything on its own.
            var mark = UiTheme.Icon(CategoryIcon(template.Category));
            if (mark != null)
            {
                var badge = new GameObject("Category mark", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
                badge.SetParent(card, false);
                badge.anchorMin = new Vector2(1f, 1f);
                badge.anchorMax = new Vector2(1f, 1f);
                badge.pivot = new Vector2(1f, 1f);
                badge.sizeDelta = new Vector2(22f, 22f);
                badge.anchoredPosition = new Vector2(-12f, -12f);
                var art = badge.GetComponent<Image>();
                art.sprite = mark;
                art.color = chosen ? UiTheme.Glow : UiTheme.Accent;
                art.preserveAspect = true;
                art.raycastTarget = false;
            }

            // Everything else stacks in the column beside the face rather than in bands across the
            // whole card. The card was nearly square and five bands deep with a small avatar beside
            // a paragraph; the reference is wide and short, and its second element after the face is
            // the archetype. So the order is what the player is choosing BY: the name, the
            // archetype, then the two traits, then the facts.
            float textX = 34f + PortraitSize;
            float textWidth = CardWidth - textX - 16f;
            Line(card, template.Name, 17f, UiTheme.Paper, -22f, 24f, textX, textWidth, TextAlignmentOptions.Left);

            // The archetype as the reference draws it: a filled pill in the category's own colour,
            // not a line of accent text that reads as chrome. The word is unchanged.
            var archetype = Localisation.Text(template.Archetype);
            var pill = HudPrimitives.Chip("Archetype", card, archetype, CategoryTint(template.Category),
                Mathf.Min(textWidth, Mathf.Max(96f, archetype.Length * 7.4f + 22f)), 24f, true);
            pill.anchorMin = new Vector2(0f, 1f);
            pill.anchorMax = new Vector2(0f, 1f);
            pill.pivot = new Vector2(0f, 1f);
            pill.anchoredPosition = new Vector2(textX, -50f);

            Traits(card, template, textX, -84f);

            Line(card, Subtitle(template), 12f, UiTheme.Muted, -114f, 18f, textX, textWidth,
                TextAlignmentOptions.Left);

            // The selection is stated, not only drawn. A glowing border is invisible to a screen
            // reader and to anyone who cannot separate it from the resting hairline - and the
            // category word has to survive alongside it, because the glyph in the corner repeats
            // this word and must never be the only thing carrying it.
            Line(card, chosen ? "PLAYING AS" : template.Category.ToUpperInvariant(), 11f,
                chosen ? UiTheme.Glow : UiTheme.Muted, -136f, 16f, textX, textWidth,
                TextAlignmentOptions.Left);
            if (chosen)
                Line(card, template.Category.ToUpperInvariant(), 11f, UiTheme.Muted, -136f, 16f,
                    textX, textWidth, TextAlignmentOptions.Right);
        }

        /// <summary>The traits as the mockup draws them: one pill each, tinted by what they mean.</summary>
        private static void Traits(RectTransform card, CastTemplates.Template template, float left, float y)
        {
            var words = template.Traits;
            if (words == null || words.Length == 0) return;

            const float height = 22f;
            var widths = new float[words.Length];
            float total = 0f;
            for (int i = 0; i < words.Length; i++)
            {
                string word = Localisation.Text(words[i]);
                widths[i] = Mathf.Max(56f, word.Length * 6f + 20f);
                total += widths[i];
            }
            total += 8f * (words.Length - 1);

            float x = left;
            for (int i = 0; i < words.Length; i++)
            {
                var pill = HudPrimitives.Chip(TraitChipName, card, Localisation.Text(words[i]),
                    TraitTint(words[i]), widths[i], height);
                pill.anchorMin = new Vector2(0f, 1f);
                pill.anchorMax = new Vector2(0f, 1f);
                pill.pivot = new Vector2(0f, 1f);
                pill.anchoredPosition = new Vector2(x, y);
                x += widths[i] + 8f;
            }
        }

        /// <summary>
        /// The colour an archetype's pill wears, keyed off the CATEGORY rather than the archetype.
        ///
        /// <para>Five categories against twenty-four archetypes across the two rosters: a colour per
        /// archetype would be twenty-four hues nobody can tell apart, where five is a legend a
        /// player can actually learn - and it is the same five the filter row above already sorts
        /// by, so the pill's colour and the chip they pressed agree. The word on the pill is routed
        /// through OnColor rather than set white: white on these tints runs 1.43:1 to 3.89:1.</para>
        ///
        /// <para>These are the five MEANING colours, used here as a categorical palette, and that is
        /// deliberate rather than lazy: not one of the five meanings occurs on this screen. Nobody is
        /// nominated, allied, flirting or joking while a season is being set up, so the hues are free
        /// and they are the five the palette already guarantees are distinguishable from each other.
        /// Two tokens were considered and rejected - Award, which UiTheme reserves for the
        /// competition banner, and AccentDeep, whose best foreground is 4.36:1 and so cannot carry a
        /// twelve-pixel word.</para>
        /// </summary>
        private static Color CategoryTint(string category)
        {
            switch ((category ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "strategist": return UiTheme.Strategic;
                case "competitor": return UiTheme.Joke;
                case "socialite": return UiTheme.Flirt;
                case "wildcard": return UiTheme.Conflict;
                case "underdog": return UiTheme.Allied;
                default: return UiTheme.Accent;
            }
        }

        /// <summary>
        /// The accent a trait wears, by what the word means rather than by its position in the list:
        /// green for the warm ones, violet for the calculating ones, gold for the competitive ones
        /// and red for the ones that start fights. An unlisted trait takes the neutral accent.
        /// </summary>
        private static Color TraitTint(string trait)
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

        private void HouseSize()
        {
            Space(10f);
            var bar = Row(58f);
            int largest = SeasonBuilder.LargestHouse(roster);

            var caption = HudPrimitives.Label("House size", bar, 17f, UiTheme.Paper, TextAlignmentOptions.Center);
            caption.text = houseSize + " houseguests, including you";
            caption.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            caption.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            caption.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            caption.rectTransform.sizeDelta = new Vector2(420f, 26f);
            caption.rectTransform.anchoredPosition = Vector2.zero;

            Chip(bar, "Fewer houseguests", -330f, 230f, false, () =>
            {
                houseSize = SeasonBuilder.ClampHouseSize(roster, Math.Max(customHouseguests.Count + 1, houseSize - 1));
                Rebuild();
            });
            Chip(bar, "More houseguests", 330f, 230f, false, () =>
            {
                houseSize = SeasonBuilder.ClampHouseSize(roster, houseSize + 1);
                Rebuild();
            });

            Text("Between " + SeasonBuilder.MinimumHouse + " and " + largest + " on this roster. "
                 + "A shorter season reaches the final three sooner; it does not simplify a week.",
                12f, UiTheme.Muted, 20f, TextAlignmentOptions.Center);
        }

        private void Footer()
        {
            Space(10f);
            if (!string.IsNullOrEmpty(resumeError))
                Text(resumeError, 14f, UiTheme.Warning, 64f, TextAlignmentOptions.Center);
            var chosen = string.IsNullOrEmpty(selectedId) ? null : CastTemplates.Find(selectedId);
            Text(retainedDraft != null ? "You will play as " + retainedDraft.Name + ". Your setup edits are retained."
                : chosen != null
                    ? "You will play as " + chosen.Name + ", " + chosen.Archetype.ToLowerInvariant() + "."
                    : "No card picked. You will play as an unaffiliated newcomer.",
                14f, chosen != null ? UiTheme.Gold : UiTheme.Muted, 22f, TextAlignmentOptions.Center);

            var bar = Row(56f);
            Chip(bar, StartCaption, -150f, 260f, true, () =>
            {
                var choice = new SeasonBuilder.Choice
                {
                    Roster = roster,
                    PlayerTemplateId = selectedId,
                    HouseSize = SeasonBuilder.ClampHouseSize(roster, houseSize),
                    CustomHouseguests = customHouseguests.Select(profile => profile.Clone()).ToList(),
                };
                if (chosen != null)
                {
                    choice.Authored = CharacterDraft.FromAppearance(chosen);
                    var catalog = (CharacterBodySource.Provider as IModularCharacterBodyProvider)?.Catalog;
                    if (catalog != null) choice.Authored.Appearance = catalog.Materialize(choice.Authored.Appearance);
                }
                if (retainedDraft != null) choice.Authored = retainedDraft.Copy();
                var start = onStart;
                Hide();
                start?.Invoke(choice);
            });
            Chip(bar, CancelCaption, 150f, 260f, false, Dismiss);

            Space(Pad);
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
            // Glow, not AccentDeep. Paper on AccentDeep is 3.89:1 - under the 4.5 a 14px label
            // needs - and it was the colour of every active pill on the screen: the navigation row,
            // the roster row, the filter row. Glow takes Ink at 9.84:1 through OnColor below.
            var pill = HudPrimitives.Fill(text, parent, active ? UiTheme.Glow : UiTheme.GlassFill, 18);
            pill.anchorMin = new Vector2(0.5f, 0.5f);
            pill.anchorMax = new Vector2(0.5f, 0.5f);
            pill.pivot = new Vector2(0.5f, 0.5f);
            pill.sizeDelta = new Vector2(width, 38f);
            pill.anchoredPosition = new Vector2(x, 0f);

            // The mockups' active pill is the bright one, and it is the only one that glows. The
            // resting pills keep the cyan hairline every panel edge carries.
            if (active)
            {
                UiTheme.Glass(pill, 18);
                // Glass repaints the image to GlassFill, so the fill has to be written back AFTER
                // it rather than before - the line above used to do this with AccentDeep, which made
                // the constructor argument dead code for exactly the pills this is about.
                UiTheme.Style(pill.GetComponent<Image>(), UiTheme.Glow, 18);
                // Not Glow on Glow, which is an edge that cannot be seen, and deliberately not
                // UiTheme.Edge(Emphasis.Active): Chrome_TheRightColumnIsAStack asserts that no live
                // Image named "Border" under the director wears the active edge, and Hide() leaves
                // this hierarchy alive. A saturated fill is already the signal.
                UiTheme.AddBorder(pill, 18, new Color(UiTheme.Paper.r, UiTheme.Paper.g, UiTheme.Paper.b, 0.35f));
            }
            else
            {
                UiTheme.AddBorder(pill, 18, new Color(UiTheme.Hairline.r, UiTheme.Hairline.g, UiTheme.Hairline.b, 0.45f));
            }

            var image = pill.GetComponent<Image>();
            image.raycastTarget = true;

            var label = HudPrimitives.Label("Label", pill, 14f,
                active ? UiTheme.OnColor(UiTheme.Glow) : UiTheme.Muted, TextAlignmentOptions.Center);
            label.text = Localisation.Text(text);
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
        /// One line of a card, measured from the card's own left edge so the face can own the left
        /// of the grid and the copy can own the right without either guessing where the other ends.
        /// </summary>
        private static void Line(Transform card, string value, float size, Color colour, float y,
            float height, float x, float width, TextAlignmentOptions align)
        {
            if (string.IsNullOrEmpty(value)) return;
            var label = HudPrimitives.Label("Line", card, size, colour, align);
            label.text = Localisation.Text(value);
            var rect = label.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(x, y);
        }

        private static string Subtitle(CastTemplates.Template template)
        {
            var parts = new List<string>();
            if (template.Age > 0) parts.Add(template.Age.ToString());
            if (!string.IsNullOrEmpty(template.Occupation)) parts.Add(template.Occupation);
            return string.Join(" · ", parts);
        }

        /// <summary>Up to two initials, skipping an honorific so "Dr. Will Kirby" reads WK.</summary>
        private static string Initials(string name)
        {
            if (string.IsNullOrEmpty(name)) return "?";
            var words = name.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(word => !word.EndsWith(".", StringComparison.Ordinal))
                .ToList();
            if (words.Count == 0) return name.Substring(0, 1).ToUpperInvariant();
            var first = words[0].Substring(0, 1);
            var last = words.Count > 1 ? words[words.Count - 1].Substring(0, 1) : string.Empty;
            return (first + last).ToUpperInvariant();
        }

        // ---------------------------------------------------------------- layout

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
