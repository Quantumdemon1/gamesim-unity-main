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
        private const float Width = 1180f;
        private const float Pad = 28f;
        private const int Columns = 4;
        private const float Gutter = 14f;
        private const float CardWidth = (Width - Pad * 2f - Gutter * (Columns - 1)) / Columns;
        private const float CardHeight = 216f;

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
            // Deactivated before Destroy, which is deferred to the end of the frame: the screen
            // rebuilds itself on every click, so for one frame the old controls would otherwise
            // still be live alongside the new ones and "the Start button" would match twice.
            foreach (Transform child in transform)
            {
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }

            // The mockups' night ground rather than a neutral black: the cards are glass over it,
            // and glass over black reads as flat panels on a void.
            var scrim = HudPrimitives.Fill("Scrim", transform,
                new Color(UiTheme.Background.r, UiTheme.Background.g, UiTheme.Background.b, 0.97f), 1);
            Stretch(scrim);

            var viewport = HudPrimitives.Fill("Viewport", scrim, new Color(0f, 0f, 0f, 0f), 1);
            viewport.anchorMin = new Vector2(0.5f, 0f);
            viewport.anchorMax = new Vector2(0.5f, 1f);
            viewport.pivot = new Vector2(0.5f, 1f);
            viewport.sizeDelta = new Vector2(Width, 0f);
            viewport.anchoredPosition = Vector2.zero;
            viewport.gameObject.AddComponent<RectMask2D>();

            content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(viewport, false);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;

            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;

            cursor = 0f;
            Header();
            RosterTabs();
            CategoryChips();
            Grid();
            HouseSize();
            Footer();

            content.sizeDelta = new Vector2(0f, cursor + Pad);
        }

        private void Header()
        {
            Space(Pad);
            // The mockup sets the title in white with the strap in a muted blue-grey under it. The
            // words are the build's own, unchanged; only the weight and the colour move.
            var title = HudPrimitives.Heading("Text", content, 30f, UiTheme.Paper, TextAlignmentOptions.Center);
            title.text = Localisation.Text("CHOOSE YOUR HOUSEGUEST");
            title.characterSpacing = 8f;
            Place(title.rectTransform, Width - Pad * 2f, 42f, -cursor);
            cursor += 42f;
            Text("Pick who you play as, then set the size of the house. Everyone else is cast from the same roster.",
                15f, UiTheme.Muted, 24f, TextAlignmentOptions.Center);
            Space(8f);
        }

        private void RosterTabs()
        {
            var bar = Row(46f);
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
            Space(6f);
        }

        private void CategoryChips()
        {
            var chips = new List<string> { CastTemplates.AllCategories };
            chips.AddRange(CastTemplates.Categories);

            var bar = Row(40f);
            float span = 168f;
            float x = -(chips.Count - 1) * span / 2f;
            foreach (var name in chips)
            {
                var pick = name;
                Chip(bar, pick, x, span - 10f, string.Equals(category, pick, StringComparison.OrdinalIgnoreCase),
                    () => { category = pick; Rebuild(); });
                x += span;
            }
            Space(10f);
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

            var card = HudPrimitives.Fill(template.Name, content, UiTheme.GlassFill, UiTheme.GlassRadius);
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
                selectedId = string.Equals(selectedId, pick, StringComparison.Ordinal) ? null : pick;
                Rebuild();
            });

            // The face. No houseguest has a body before a season exists, so the card shows their
            // wardrobe colour and their initials — the same colour they will be wearing in the
            // house, which is what makes the grid something you can read at a glance.
            var wardrobe = CastPalette.For(template.Id);
            var rim = HudPrimitives.Disc("Ring", card, chosen ? UiTheme.Glow : UiTheme.Outline);
            rim.anchorMin = new Vector2(0f, 1f);
            rim.anchorMax = new Vector2(0f, 1f);
            rim.pivot = new Vector2(0.5f, 1f);
            rim.sizeDelta = new Vector2(88f, 88f);
            rim.anchoredPosition = new Vector2(52f, -14f);

            var face = HudPrimitives.Disc("Face", rim, wardrobe);
            face.anchorMin = new Vector2(0.5f, 0.5f);
            face.anchorMax = new Vector2(0.5f, 0.5f);
            face.pivot = new Vector2(0.5f, 0.5f);
            face.sizeDelta = new Vector2(82f, 82f);
            face.anchoredPosition = Vector2.zero;

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
                art.sizeDelta = new Vector2(66f, 66f);
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
                badge.sizeDelta = new Vector2(24f, 24f);
                badge.anchoredPosition = new Vector2(-14f, -14f);
                var art = badge.GetComponent<Image>();
                art.sprite = mark;
                art.color = chosen ? UiTheme.Glow : UiTheme.Accent;
                art.preserveAspect = true;
                art.raycastTarget = false;
            }

            // The right-hand column beside the face: who they are, then what they are called.
            const float textX = 104f;
            float textWidth = CardWidth - textX - 14f;
            Line(card, template.Name, 15f, UiTheme.Paper, -32f, 22f, textX, textWidth, TextAlignmentOptions.Left);
            Line(card, template.Archetype, 13f, UiTheme.Accent, -56f, 20f, textX, textWidth, TextAlignmentOptions.Left);

            // Full width under both, where the line has room for the longest occupation on the
            // roster without wrapping into a box that would clip it.
            Line(card, Subtitle(template), 12f, UiTheme.Muted, -112f, 18f, 14f, CardWidth - 28f,
                TextAlignmentOptions.Center);

            Traits(card, template);

            // The selection is stated, not only drawn. A glowing border is invisible to a screen
            // reader and to anyone who cannot separate it from the resting hairline.
            Line(card, chosen ? "PLAYING AS" : template.Category.ToUpperInvariant(), 11f,
                chosen ? UiTheme.Glow : UiTheme.Muted, -184f, 18f, 14f, CardWidth - 28f,
                TextAlignmentOptions.Center);
        }

        /// <summary>The traits as the mockup draws them: one pill each, tinted by what they mean.</summary>
        private static void Traits(RectTransform card, CastTemplates.Template template)
        {
            var words = template.Traits;
            if (words == null || words.Length == 0) return;

            const float height = 20f;
            var widths = new float[words.Length];
            float total = 0f;
            for (int i = 0; i < words.Length; i++)
            {
                string word = Localisation.Text(words[i]);
                widths[i] = Mathf.Max(56f, word.Length * 6f + 20f);
                total += widths[i];
            }
            total += 8f * (words.Length - 1);

            float x = -total * 0.5f;
            for (int i = 0; i < words.Length; i++)
            {
                var pill = HudPrimitives.Chip(TraitChipName, card, Localisation.Text(words[i]),
                    TraitTint(words[i]), widths[i], height);
                pill.anchorMin = new Vector2(0.5f, 1f);
                pill.anchorMax = new Vector2(0.5f, 1f);
                pill.pivot = new Vector2(0f, 1f);
                pill.anchoredPosition = new Vector2(x, -140f);
                x += widths[i] + 8f;
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
                houseSize = SeasonBuilder.ClampHouseSize(roster, houseSize - 1);
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
            var chosen = string.IsNullOrEmpty(selectedId) ? null : CastTemplates.Find(selectedId);
            Text(chosen != null
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
                };
                var start = onStart;
                Hide();
                start?.Invoke(choice);
            });
            Chip(bar, CancelCaption, 150f, 260f, false, Dismiss);

            // The reference build's step one offers three ways out of this grid: play a card as it
            // is, open it for editing, or start from nothing. The first is the control above; these
            // are the other two, and "Customise" names the selected houseguest so it is never a
            // question of which card it would open.
            if (onCustomise != null)
            {
                var creator = Row(52f);
                Chip(creator, CharacterCreator.CreateCaption, -170f, 320f, false,
                    () => OpenCreator(CharacterDraft.Blank()));
                Chip(creator, CharacterCreator.CustomiseCaption, 170f, 320f, false, () =>
                    OpenCreator(chosen != null ? CharacterDraft.From(chosen) : CharacterDraft.Blank()));
                Text(chosen != null
                        ? "Customise opens " + chosen.Name + " for editing. Create your own starts from a blank card."
                        : "No card picked, so both start from a blank card.",
                    12f, UiTheme.Muted, 20f, TextAlignmentOptions.Center);
            }
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
            var choice = new SeasonBuilder.Choice
            {
                Roster = roster,
                PlayerTemplateId = selectedId,
                HouseSize = SeasonBuilder.ClampHouseSize(roster, houseSize),
            };
            var customise = onCustomise;
            Hide();
            customise?.Invoke(choice, start);
        }

        /// <summary>Brings the screen back with the player's roster, filter and pick intact.</summary>
        public void Resume()
        {
            if (onStart == null) return;
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
            var pill = HudPrimitives.Fill(text, parent, active ? UiTheme.AccentDeep : UiTheme.GlassFill, 18);
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
                UiTheme.Style(pill.GetComponent<Image>(), UiTheme.AccentDeep, 18);
                UiTheme.AddBorder(pill, 18, UiTheme.Glow);
            }
            else
            {
                UiTheme.AddBorder(pill, 18, new Color(UiTheme.Hairline.r, UiTheme.Hairline.g, UiTheme.Hairline.b, 0.45f));
            }

            var image = pill.GetComponent<Image>();
            image.raycastTarget = true;

            var label = HudPrimitives.Label("Label", pill, 14f, active ? UiTheme.Paper : UiTheme.Muted,
                TextAlignmentOptions.Center);
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
