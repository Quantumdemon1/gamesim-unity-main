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

            var scrim = HudPrimitives.Fill("Scrim", transform, new Color(0.02f, 0.04f, 0.06f, 0.96f), 1);
            Stretch(scrim);

            viewport = HudPrimitives.Fill("Viewport", scrim, new Color(0f, 0f, 0f, 0f), 1);
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
            var recap = WeeklyRecap.Build(shown, openWeek);

            Space(Pad);
            Text(browsing ? "WEEK " + recap.week : "WEEK " + recap.week + " IS OVER",
                24f, UiTheme.Gold, 32f, TextAlignmentOptions.Center);
            Text(recap.Headline, 17f, UiTheme.Paper, 28f, TextAlignmentOptions.Center);
            Space(8f);

            Ceremony(recap);
            YourWeek(recap);
            Bullets("How the house voted", recap.ballots, UiTheme.Paper);
            Relationships(recap);
            Bullets("Alliances", recap.alliances, UiTheme.Accent);
            Bullets("Deals", recap.deals, UiTheme.Accent);
            Bullets("What happened to the house", recap.happenings, UiTheme.Warning);
            Bullets("Turning points", recap.moments, UiTheme.Warning);

            Controls();
            content.sizeDelta = new Vector2(0f, cursor + Pad);
        }

        private void Ceremony(WeeklyRecap.Week recap)
        {
            Heading("The week");
            Row("Head of Household", recap.headOfHousehold ?? "—", UiTheme.Accent);
            Row("Nominees", recap.nominees.Count > 0 ? string.Join(", ", recap.nominees) : "—", UiTheme.Paper);
            Row("Veto", recap.vetoHolder ?? "—", UiTheme.Accent);
            // Three readings, not two: no meeting at all is not the same as a veto left in the box.
            Row("Veto used", recap.vetoUsed == null ? "No meeting" : recap.vetoUsed.Value ? "Yes" : "No",
                recap.vetoUsed == true ? UiTheme.Accent : UiTheme.Muted);
            Row("Evicted", recap.evicted ?? "—", recap.evicted == null ? UiTheme.Muted : UiTheme.Danger);
        }

        private void YourWeek(WeeklyRecap.Week recap)
        {
            if (string.IsNullOrEmpty(recap.yourWeek)) return;
            Heading("Your week");
            Text(recap.yourWeek, 16f, UiTheme.Paper, 46f, TextAlignmentOptions.TopLeft);
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
                Text("· " + line, 15f, move.delta > 0 ? UiTheme.Accent : UiTheme.Danger, 24f,
                    TextAlignmentOptions.Left);
                lines.Add(line);
            }
        }

        private void Bullets(string heading, List<string> entries, Color tint)
        {
            if (entries == null || entries.Count == 0) return;
            Heading(heading);
            foreach (string entry in entries)
            {
                Text("· " + entry, 15f, tint, 24f, TextAlignmentOptions.Left);
                lines.Add(entry);
            }
        }

        private void Controls()
        {
            Space(18f);
            var bar = Panel(56f, new Color(0f, 0f, 0f, 0f));
            if (browsing)
            {
                Button(bar, BackCaption, 0f, Back);
            }
            else
            {
                Button(bar, ContinueCaption, -150f, Dismiss);
                // Only offered where there is an earlier week to look at.
                if (openWeek > 1) Button(bar, ReviewCaption, 150f, () => Review(shown, openWeek - 1));
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

        private void Row(string label, string value, Color tint)
        {
            var panel = Panel(30f, UiTheme.Surface);
            Cell(panel, 20f, 300f, Localisation.Text(label).ToUpperInvariant(), 13f, UiTheme.Muted, TextAlignmentOptions.Left);
            Cell(panel, 330f, Width - 380f, value, 15f, tint, TextAlignmentOptions.Left);
            lines.Add(label + ": " + value);
        }

        private void Heading(string text)
        {
            Space(14f);
            var label = HudPrimitives.Label("Heading", content, 17f, UiTheme.Gold, TextAlignmentOptions.Left);
            label.text = Localisation.Text(text).ToUpperInvariant();
            Place(label.rectTransform, Width - Pad * 2f, 24f, -cursor);
            cursor += 28f;
        }

        private RectTransform Panel(float height, Color colour)
        {
            var panel = HudPrimitives.Fill("Row", content, colour, 8);
            Place(panel, Width - Pad * 2f, height, -cursor);
            cursor += height + 6f;
            return panel;
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

        private static void Button(Transform parent, string text, float x, Action action)
        {
            var panel = HudPrimitives.Fill(text, parent, UiTheme.SurfaceRaised, 8);
            panel.anchorMin = new Vector2(0.5f, 0.5f);
            panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(280f, 44f);
            panel.anchoredPosition = new Vector2(x, 0f);
            panel.GetComponent<Image>().raycastTarget = true;
            UiTheme.AddBorder(panel, 8, UiTheme.Outline);

            var label = HudPrimitives.Label("Label", panel, 16f, UiTheme.Paper, TextAlignmentOptions.Center);
            label.text = Localisation.Text(text);
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
