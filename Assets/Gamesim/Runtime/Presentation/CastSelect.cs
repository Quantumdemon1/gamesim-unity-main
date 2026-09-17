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
        private const float CardHeight = 208f;

        /// <summary>The caption the start control carries. Tests and the tour find it by this text.</summary>
        public const string StartCaption = "Start this season";
        public const string CancelCaption = "Cancel — keep this season";

        private RectTransform content;
        private CanvasGroup group;
        private float cursor;

        private CastTemplates.Roster roster = CastTemplates.Roster.Regular;
        private string category = CastTemplates.AllCategories;
        private string selectedId;
        private int houseSize = SeasonBuilder.DefaultHouseSize;

        private Action<SeasonBuilder.Choice> onStart;
        private Action onCancel;

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
        public void Show(Action<SeasonBuilder.Choice> start, Action cancel)
        {
            onStart = start;
            onCancel = cancel;
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

            var scrim = HudPrimitives.Fill("Scrim", transform, new Color(0.02f, 0.04f, 0.06f, 0.97f), 1);
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
            Text("CHOOSE YOUR HOUSEGUEST", 26f, UiTheme.Gold, 34f, TextAlignmentOptions.Center);
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

        private void Card(CastTemplates.Template template, int column, float y)
        {
            bool chosen = string.Equals(selectedId, template.Id, StringComparison.Ordinal);

            var card = HudPrimitives.Fill(template.Name, content,
                chosen ? UiTheme.SurfaceRaised : UiTheme.Surface, 10);
            card.anchorMin = new Vector2(0.5f, 1f);
            card.anchorMax = new Vector2(0.5f, 1f);
            card.pivot = new Vector2(0f, 1f);
            card.sizeDelta = new Vector2(CardWidth, CardHeight);
            card.anchoredPosition = new Vector2(
                -Width / 2f + Pad + column * (CardWidth + Gutter), y);
            UiTheme.AddBorder(card, 10, chosen ? UiTheme.Gold : UiTheme.Outline);

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
            var rim = HudPrimitives.Disc("Ring", card, chosen ? UiTheme.Gold : UiTheme.Outline);
            rim.anchorMin = new Vector2(0.5f, 1f);
            rim.anchorMax = new Vector2(0.5f, 1f);
            rim.pivot = new Vector2(0.5f, 1f);
            rim.sizeDelta = new Vector2(60f, 60f);
            rim.anchoredPosition = new Vector2(0f, -14f);

            var face = HudPrimitives.Disc("Face", rim, wardrobe);
            face.anchorMin = new Vector2(0.5f, 0.5f);
            face.anchorMax = new Vector2(0.5f, 0.5f);
            face.pivot = new Vector2(0.5f, 0.5f);
            face.sizeDelta = new Vector2(56f, 56f);
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
                art.sizeDelta = new Vector2(46f, 46f);
                art.anchoredPosition = new Vector2(0f, 3f);
                var portrait = art.GetComponent<Image>();
                portrait.sprite = silhouette;
                portrait.color = UiTheme.OnColor(wardrobe);
                portrait.preserveAspect = true;
                portrait.raycastTarget = false;
            }
            else
            {
                var initials = HudPrimitives.Label("Initials", face, 20f,
                    UiTheme.OnColor(wardrobe), TextAlignmentOptions.Center);
                initials.text = Initials(template.Name);
                initials.rectTransform.anchorMin = Vector2.zero;
                initials.rectTransform.anchorMax = Vector2.one;
                initials.rectTransform.offsetMin = Vector2.zero;
                initials.rectTransform.offsetMax = Vector2.zero;
            }

            Line(card, template.Name, 16f, UiTheme.Paper, -84f, 22f);
            Line(card, template.Archetype, 13f, UiTheme.Accent, -104f, 18f);
            Line(card, Subtitle(template), 12f, UiTheme.Muted, -122f, 18f);
            Line(card, string.Join(" · ", template.Traits), 12f, UiTheme.Positive, -144f, 18f);

            // The selection is stated, not only drawn. A gold border is invisible to a screen reader
            // and to anyone who cannot separate it from the resting outline.
            Line(card, chosen ? "PLAYING AS" : template.Category.ToUpperInvariant(), 11f,
                chosen ? UiTheme.Gold : UiTheme.Muted, -170f, 18f);
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
            Space(Pad);
        }

        // ---------------------------------------------------------------- pieces

        /// <summary>
        /// A pill-shaped control. <paramref name="active"/> only changes its colours — every chip is
        /// a button with its own words, so which one is selected is never carried by tint alone.
        /// </summary>
        private static Button Chip(Transform parent, string text, float x, float width, bool active, Action action)
        {
            var pill = HudPrimitives.Fill(text, parent, active ? UiTheme.AccentDeep : UiTheme.SurfaceRaised, 18);
            pill.anchorMin = new Vector2(0.5f, 0.5f);
            pill.anchorMax = new Vector2(0.5f, 0.5f);
            pill.pivot = new Vector2(0.5f, 0.5f);
            pill.sizeDelta = new Vector2(width, 38f);
            pill.anchoredPosition = new Vector2(x, 0f);
            UiTheme.AddBorder(pill, 18, active ? UiTheme.Accent : UiTheme.Outline);

            var image = pill.GetComponent<Image>();
            image.raycastTarget = true;

            var label = HudPrimitives.Label("Label", pill, 14f, active ? UiTheme.Paper : UiTheme.Muted,
                TextAlignmentOptions.Center);
            label.text = text;
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(8f, 0f);
            label.rectTransform.offsetMax = new Vector2(-8f, 0f);

            var button = pill.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => action());
            return button;
        }

        private static void Line(Transform card, string value, float size, Color colour, float y, float height)
        {
            if (string.IsNullOrEmpty(value)) return;
            var label = HudPrimitives.Label("Line", card, size, colour, TextAlignmentOptions.Center);
            label.text = value;
            var rect = label.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(CardWidth - 16f, height);
            rect.anchoredPosition = new Vector2(0f, y);
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
            label.text = value;
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
