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
    /// The screen a week ends on.
    ///
    /// <para><see cref="SeasonReport"/> closes a season; this closes a week, which until now closed
    /// with nothing. The whole of what it says comes from <see cref="WeeklyRecap"/>, which reads
    /// committed state and derives nothing the save does not already hold — so this screen cannot
    /// contradict the season, and it is built without touching the simulation.</para>
    ///
    /// <para>It is a screen, not a ceremony card, so it parents to the director and its controls are
    /// meant to be found: a card that is watched rather than used goes on its own scene root, and
    /// copying that pattern here would make every lookup of these buttons return null.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WeeklyRecapScreen : MonoBehaviour
    {
        private const float Width = 1080f;
        private const float Pad = 26f;

        /// <summary>The words on the controls. Captions are how a test and a screen reader find them.</summary>
        public const string ContinueCaption = "Continue to next week";
        public const string ReviewCaption = "Review earlier weeks";
        public const string BackCaption = "Back to this week";

        private RectTransform content, viewport;
        private CanvasGroup group;
        private float cursor;

        private EpisodeState shown;

        /// <summary>
        /// What dismissing the screen does.
        ///
        /// <para>Both buttons call it, and the caller supplies it, because dismissing has to restore
        /// what showing took away — this screen counts as an open panel, so the player's input stays
        /// off until the director hears that it closed. A screen that hid itself and told nobody
        /// would leave the house unwalkable.</para>
        /// </summary>
        private Action onDismiss;
        private int openWeek;
        private bool browsing;

        /// <summary>Which week is on screen, so a caller can tell a recap from a review.</summary>
        public int OpenWeek => openWeek;

        /// <summary>Whether the player is looking back through the season rather than closing a week.</summary>
        public bool Browsing => browsing;

        public bool IsOpen => group != null && group.alpha > 0f;

        /// <summary>Every line currently on screen, in order. A seam for tests, like SeasonReport's.</summary>
        public IReadOnlyList<string> Lines => lines;
        private readonly List<string> lines = new List<string>();

        public static WeeklyRecapScreen Attach(GameObject owner)
        {
            var root = new GameObject("Gamesim Weekly Recap",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler),
                typeof(GraphicRaycaster), typeof(CanvasGroup));
            root.transform.SetParent(owner.transform, false);

            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Under the season report, which is the only screen that should ever cover this one.
            canvas.sortingOrder = 110;

            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var screen = root.AddComponent<WeeklyRecapScreen>();
            screen.group = root.GetComponent<CanvasGroup>();
            screen.Hide();
            return screen;
        }

        public void Hide()
        {
            if (group == null) return;
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;
            browsing = false;
        }

        /// <summary>
        /// Shows the week that just closed.
        ///
        /// <para><paramref name="onDismiss"/> is what the buttons do. It is the caller's
        /// business — this screen never advances the season itself, because a screen that commits a
        /// command is a screen that can disagree with the save it is describing.</para>
        /// </summary>
        public void Show(EpisodeState state, Action onDismiss)
        {
            if (state == null) return;
            shown = state;
            this.onDismiss = onDismiss;
            openWeek = state.week;
            browsing = false;
            liveWeekBehindReview = 0;
            Rebuild();
            group.alpha = 1f;
            group.blocksRaycasts = true;
            group.interactable = true;
        }

        /// <summary>Looks back at a week already played. Same screen, no continue.</summary>
        public void Review(EpisodeState state, int week, Action onDismiss = null)
        {
            if (state == null) return;
            // Looking back from the week that just finished: Back returns there, with its
            // Continue, rather than closing a recap the player has not finished reading. From the
            // notebook there is no live week behind the review, and Back closes as it always did.
            liveWeekBehindReview = IsOpen && !browsing ? openWeek : 0;
            shown = state;
            if (onDismiss != null) this.onDismiss = onDismiss;
            openWeek = Mathf.Clamp(week, 1, Mathf.Max(1, state.week));
            browsing = true;
            Rebuild();
            group.alpha = 1f;
            group.blocksRaycasts = true;
            group.interactable = true;
        }

        // ---------------------------------------------------------------- drawing

        private void Rebuild()
        {
            foreach (Transform child in transform) Destroy(child.gameObject);
            lines.Clear();

            // The house dimmed behind the week rather than blacked out, and the week on a glass
            // card, as the mockups set every summary over the room it is about.
            var scrim = HudPrimitives.Fill("Scrim", transform, new Color(UiTheme.Ink.r, UiTheme.Ink.g, UiTheme.Ink.b, 0.86f), 1);
            Stretch(scrim);
            HudPrimitives.Vignette(scrim);

            var card = HudPrimitives.Fill("Recap card", scrim, new Color(UiTheme.GlassFill.r, UiTheme.GlassFill.g, UiTheme.GlassFill.b, .94f), UiTheme.GlassRadius);
            card.anchorMin = new Vector2(0.5f, 0f);
            card.anchorMax = new Vector2(0.5f, 1f);
            card.pivot = new Vector2(0.5f, 0.5f);
            card.sizeDelta = new Vector2(Width + 40f, -96f);
            card.anchoredPosition = Vector2.zero;
            UiTheme.Glass(card, UiTheme.GlassRadius);

            viewport = HudPrimitives.Fill("Viewport", card, new Color(0f, 0f, 0f, 0f), 1);
            viewport.anchorMin = new Vector2(0.5f, 0f);
            viewport.anchorMax = new Vector2(0.5f, 1f);
            viewport.pivot = new Vector2(0.5f, 1f);
            viewport.sizeDelta = new Vector2(Width, -8f);
            viewport.anchoredPosition = new Vector2(0f, -4f);
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
            var recap = WeeklyRecap.Build(shown, openWeek);

            Space(Pad);
            var title = Text(browsing ? "WEEK " + recap.week : "WEEK " + recap.week + " IS OVER",
                32f, Color.white, 42f, TextAlignmentOptions.Center);
            var bold = UiTheme.Font(UiTheme.Weight.Bold);
            if (bold != null) title.font = bold;
            title.characterSpacing = 3f;
            title.enableVertexGradient = true;
            title.colorGradient = new VertexGradient(UiTheme.Glow, UiTheme.Glow, UiTheme.Heading, UiTheme.Heading);
            Text(recap.Headline, 18f, UiTheme.Paper, 0f, TextAlignmentOptions.Center);
            Space(10f);

            Ceremony(recap);
            YourWeek(recap);
            Bullets("How the house voted", recap.ballots, UiTheme.Accent);
            Relationships(recap);
            Bullets("Alliances", recap.alliances, UiTheme.Accent);
            Bullets("Deals", recap.deals, UiTheme.Accent);
            Bullets("What happened to the house", recap.happenings, UiTheme.Warning);
            Bullets("Turning points", recap.moments, UiTheme.Warning);

            Controls();
            content.sizeDelta = new Vector2(0f, cursor + Pad);
        }

        /// <summary>
        /// The week's ceremonies as a row of tiles, the mockups' stat cards: what each role is,
        /// who held it, and its mark - gold for the two powers, red for the block and the door.
        /// </summary>
        private void Ceremony(WeeklyRecap.Week recap)
        {
            Heading("The week");
            var tiles = new[]
            {
                ("Head of Household", recap.headOfHousehold ?? "\u2014", "crown", UiTheme.Gold),
                ("Nominees", recap.nominees.Count > 0 ? string.Join(", ", recap.nominees) : "\u2014", "target", UiTheme.Danger),
                ("Veto", recap.vetoHolder ?? "\u2014", "veto-token", UiTheme.Gold),
                // Three readings, not two: no meeting at all is not the same as a veto left in the box.
                ("Veto used", recap.vetoUsed == null ? "No meeting" : recap.vetoUsed.Value ? "Yes" : "No", "key",
                    recap.vetoUsed == true ? UiTheme.Accent : UiTheme.Muted),
                ("Evicted", recap.evicted ?? "\u2014", "evicted", recap.evicted == null ? UiTheme.Muted : UiTheme.Danger),
            };
            const float gap = 12f, height = 104f;
            float inner = Width - Pad * 2f, each = (inner - gap * (tiles.Length - 1)) / tiles.Length;
            var row = new GameObject("Ceremony tiles", typeof(RectTransform)).GetComponent<RectTransform>();
            row.SetParent(content, false);
            Place(row, inner, height, -cursor);
            for (int i = 0; i < tiles.Length; i++)
            {
                var (label, value, glyph, tint) = tiles[i];
                var tile = HudPrimitives.Fill("Row", row, new Color(UiTheme.Surface.r, UiTheme.Surface.g, UiTheme.Surface.b, .9f), 10);
                tile.anchorMin = tile.anchorMax = new Vector2(0f, 1f);
                tile.pivot = new Vector2(0f, 1f);
                tile.sizeDelta = new Vector2(each, height);
                tile.anchoredPosition = new Vector2(i * (each + gap), 0f);
                UiTheme.AddBorder(tile, 10, new Color(UiTheme.Hairline.r, UiTheme.Hairline.g, UiTheme.Hairline.b, .35f));
                var icon = UiTheme.Icon(glyph);
                if (icon != null)
                {
                    var mark = new GameObject("Mark", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                    mark.rectTransform.SetParent(tile, false);
                    mark.rectTransform.anchorMin = mark.rectTransform.anchorMax = new Vector2(.5f, 1f);
                    mark.rectTransform.pivot = new Vector2(.5f, 1f);
                    mark.rectTransform.sizeDelta = new Vector2(28f, 28f);
                    mark.rectTransform.anchoredPosition = new Vector2(0f, -12f);
                    mark.sprite = icon; mark.color = tint; mark.preserveAspect = true; mark.raycastTarget = false;
                }
                var shown = HudPrimitives.Label("Value", tile, 17f, value == "\u2014" ? UiTheme.Muted : UiTheme.Paper, TextAlignmentOptions.Center);
                shown.text = Localisation.Text(value);
                var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
                if (semibold != null) shown.font = semibold;
                shown.enableAutoSizing = true; shown.fontSizeMax = 17f; shown.fontSizeMin = 11f;
                TileLine(shown.rectTransform, each, 44f, 36f);
                var name = HudPrimitives.Label("Label", tile, 11f, UiTheme.Muted, TextAlignmentOptions.Center);
                name.text = Localisation.Text(label).ToUpperInvariant();
                name.characterSpacing = 3f;
                name.enableAutoSizing = true; name.fontSizeMax = 11f; name.fontSizeMin = 8f;
                TileLine(name.rectTransform, each, 82f, 16f);
                lines.Add(label + ": " + value);
            }
            cursor += height + 6f;
        }

        private static void TileLine(RectTransform rect, float width, float top, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1f);
            rect.pivot = new Vector2(.5f, 1f);
            rect.sizeDelta = new Vector2(width - 16f, height);
            rect.anchoredPosition = new Vector2(0f, -top);
        }

        private void YourWeek(WeeklyRecap.Week recap)
        {
            if (string.IsNullOrEmpty(recap.yourWeek)) return;
            Heading("Your week");
            Text(recap.yourWeek, 16f, UiTheme.Paper, 0f, TextAlignmentOptions.TopLeft);
            lines.Add(recap.yourWeek);
        }

        private void Relationships(WeeklyRecap.Week recap)
        {
            if (recap.relationships.Count == 0) return;
            Heading("Where you stand");
            foreach (var move in recap.relationships)
            {
                string line = move.Line + " (" + (move.delta > 0 ? "+" : "")
                    + move.delta.ToString("0") + ")";
                Bullet(line, move.delta > 0 ? UiTheme.Allied : UiTheme.Danger, move.delta > 0 ? UiTheme.Allied : UiTheme.Danger);
                lines.Add(line);
            }
        }

        private void Bullets(string heading, List<string> entries, Color tint)
        {
            if (entries == null || entries.Count == 0) return;
            Heading(heading);
            foreach (string entry in entries)
            {
                Bullet(entry, tint, new Color(UiTheme.Paper.r, UiTheme.Paper.g, UiTheme.Paper.b, .9f));
                lines.Add(entry);
            }
        }

        /// <summary>A line of the recap behind a dot in its section's colour, as tall as its words.</summary>
        private void Bullet(string value, Color dot, Color ink)
        {
            const float indent = 22f;
            var label = HudPrimitives.Label("Text", content, 15f, ink, TextAlignmentOptions.TopLeft);
            label.text = Localisation.Text(value);
            label.textWrappingMode = TextWrappingModes.Normal;
            float width = Width - Pad * 2f - indent;
            float height = Mathf.Max(22f, Mathf.Ceil(label.GetPreferredValues(label.text, width, 0f).y) + 4f);
            label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            label.rectTransform.pivot = new Vector2(0.5f, 1f);
            label.rectTransform.sizeDelta = new Vector2(width, height);
            label.rectTransform.anchoredPosition = new Vector2(indent * .5f, -cursor);
            var mark = HudPrimitives.Disc("Dot", content, dot);
            mark.anchorMin = mark.anchorMax = new Vector2(0.5f, 1f);
            mark.pivot = new Vector2(0.5f, 0.5f);
            mark.sizeDelta = new Vector2(7f, 7f);
            mark.anchoredPosition = new Vector2(-(Width - Pad * 2f) * .5f + 6f, -cursor - 10f);
            cursor += height + 4f;
        }

        private void Controls()
        {
            Space(22f);
            var bar = Panel(56f, new Color(0f, 0f, 0f, 0f));
            if (browsing)
            {
                Button(bar, BackCaption, 0f, Back, true);
            }
            else
            {
                Button(bar, ContinueCaption, -150f, Dismiss, true);
                // Only offered where there is an earlier week to look at.
                if (openWeek > 1) Button(bar, ReviewCaption, 150f, () => Review(shown, openWeek - 1), false);
            }
            Space(12f);
        }

        private void Dismiss()
        {
            Hide();
            onDismiss?.Invoke();
        }

        /// <summary>The week a review was opened over, or 0 when it came from the notebook.</summary>
        private int liveWeekBehindReview;

        private void Back()
        {
            if (liveWeekBehindReview > 0 && shown != null)
            {
                int week = liveWeekBehindReview;
                liveWeekBehindReview = 0;
                openWeek = week;
                browsing = false;
                Rebuild();
                return;
            }
            Dismiss();
        }

        // ---------------------------------------------------------------- layout

        /// <summary>
        /// A section's name, small and letterspaced in the heading blue over a soft rule. It was
        /// gold, and gold is power in this house - the Head of Household, the veto, the win - not
        /// the heading over a list of deals.
        /// </summary>
        private void Heading(string text)
        {
            Space(18f);
            var label = HudPrimitives.Label("Heading", content, 14f, UiTheme.Heading, TextAlignmentOptions.Left);
            label.text = Localisation.Text(text).ToUpperInvariant();
            label.characterSpacing = 4f;
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            if (semibold != null) label.font = semibold;
            Place(label.rectTransform, Width - Pad * 2f, 22f, -cursor);
            cursor += 24f;
            var rule = new GameObject("Rule", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            rule.rectTransform.SetParent(content, false);
            Place(rule.rectTransform, Width - Pad * 2f, 1f, -cursor);
            rule.color = new Color(UiTheme.Hairline.r, UiTheme.Hairline.g, UiTheme.Hairline.b, .35f);
            rule.raycastTarget = false;
            cursor += 10f;
        }

        private RectTransform Panel(float height, Color colour)
        {
            var panel = HudPrimitives.Fill("Row", content, colour, 8);
            Place(panel, Width - Pad * 2f, height, -cursor);
            cursor += height + 6f;
            return panel;
        }

        /// <summary>A line of copy; a height of zero sizes it to its words.</summary>
        private TMP_Text Text(string value, float size, Color colour, float height, TextAlignmentOptions align)
        {
            var label = HudPrimitives.Label("Text", content, size, colour, align);
            label.text = Localisation.Text(value);
            if (height <= 0f)
            {
                label.textWrappingMode = TextWrappingModes.Normal;
                height = Mathf.Ceil(label.GetPreferredValues(label.text, Width - Pad * 2f, 0f).y) + 6f;
            }
            Place(label.rectTransform, Width - Pad * 2f, height, -cursor);
            cursor += height;
            return label;
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

        private static void Cell(Transform parent, float x, float width, string value, float size,
            Color colour, TextAlignmentOptions align)
        {
            var label = HudPrimitives.Label("Cell", parent, size, colour, align);
            label.text = Localisation.Text(value);
            var rect = label.rectTransform;
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.sizeDelta = new Vector2(width, 22f);
            rect.anchoredPosition = new Vector2(x, 0f);
        }

        /// <summary>The recap's controls: Continue in the mockups' action blue, the rest quiet.</summary>
        private static void Button(Transform parent, string text, float x, Action action, bool primary)
        {
            var panel = HudPrimitives.Fill(text, parent, primary ? UiTheme.ActionBlue : UiTheme.SurfaceRaised, 10);
            panel.anchorMin = new Vector2(0.5f, 0.5f);
            panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(280f, 48f);
            panel.anchoredPosition = new Vector2(x, 0f);
            panel.GetComponent<Image>().raycastTarget = true;
            UiTheme.AddBorder(panel, 10, primary ? UiTheme.Glow : UiTheme.Outline);
            if (primary) UiTheme.AddGlow(panel, 10);

            var label = HudPrimitives.Label("Label", panel, 17f, primary ? Color.white : UiTheme.Paper, TextAlignmentOptions.Center);
            label.text = Localisation.Text(text);
            var weight = UiTheme.Font(primary ? UiTheme.Weight.SemiBold : UiTheme.Weight.Medium);
            if (weight != null) label.font = weight;
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.sizeDelta = Vector2.zero;
            label.rectTransform.anchoredPosition = Vector2.zero;

            var button = panel.gameObject.AddComponent<Button>();
            button.targetGraphic = panel.GetComponent<Image>();
            button.onClick.AddListener(() => action());
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
