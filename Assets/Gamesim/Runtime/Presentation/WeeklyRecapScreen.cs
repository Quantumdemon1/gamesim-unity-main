using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
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
    /// <para><b>A week's episode, understood at a glance</b> (the owner's Week Recap mockup, in Season
    /// Complete Pack 7's frames): who left, in what place, and their own words from the block; the
    /// week's five headline facts; then five tabs - the week's overview (the vote split into the
    /// evictee's votes and the other nominee's, the key moments, the evictee's record and the house's
    /// temperature), the vote with every reason given, the week's events and stories, the houseguests'
    /// reactions, and what comes next. Continue is one control at the foot, whichever tab is open,
    /// and the previous week is "Review earlier weeks", as it always was.</para>
    ///
    /// <para>It is a screen, not a ceremony card, so it parents to the director and its controls are
    /// meant to be found: a card that is watched rather than used goes on its own scene root, and
    /// copying that pattern here would make every lookup of these buttons return null.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WeeklyRecapScreen : MonoBehaviour
    {
        private const float Pad = 26f;

        /// <summary>The words on the controls. Captions are how a test and a screen reader find them.</summary>
        public const string ContinueCaption = "Continue to next week";
        public const string ReviewCaption = "Review earlier weeks";
        public const string BackCaption = "Back to this week";

        /// <summary>The tabs. None of them is a way on: each shows a part of the week, and commits nothing.</summary>
        public static readonly string[] TabCaptions = { "Week overview", "Vote breakdown", "Events & highlights", "Houseguest reactions", "What's next" };

        /// <summary>The parts a test finds by name.</summary>
        public const string HeroName = "Week hero", CardsName = "Week headlines", TabBodyName = "Week tab";

        private float Width = 1080f;
        private RectTransform content, viewport;
        private CanvasGroup group;
        private CanvasScaler scaler;
        private float cursor;

        private EpisodeState shown;
        private int tab;

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

        /// <summary>Which tab is open: an index into <see cref="TabCaptions"/>.</summary>
        public int OpenTab => tab;

        public bool IsOpen => group != null && group.alpha > 0f;

        /// <summary>Every line of the week, in order, whichever tab is open. A seam for tests, like SeasonReport's.</summary>
        public IReadOnlyList<string> Lines => lines;
        private readonly List<string> lines = new List<string>();

        /// <summary>The "larger text" setting: the whole screen magnified, as the season report does it.</summary>
        public float FontScale
        {
            set
            {
                if (scaler == null) return;
                float scale = Mathf.Clamp(value, 0.5f, 2f);
                scaler.referenceResolution = new Vector2(1920f / scale, 1080f / scale);
            }
        }

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
            screen.scaler = scaler;
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
            // Nothing live left behind: a hidden recap's controls and labels would still be found by
            // their captions, and read by every sweep of the screen.
            foreach (Transform child in transform) child.gameObject.SetActive(false);
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
            tab = 0;
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
            tab = 0;
            Rebuild();
            group.alpha = 1f;
            group.blocksRaycasts = true;
            group.interactable = true;
        }

        /// <summary>Opens a tab. Nothing is committed; the week is drawn again with that part showing.</summary>
        public void OpenTabAt(int index)
        {
            if (shown == null || index < 0 || index >= TabCaptions.Length || index == tab) return;
            tab = index;
            var events = EventSystem.current;
            Rebuild();
            if (events == null) return;
            // The screen thrown away is only destroyed at the frame's end; the live tab of that name is the new one.
            var again = GetComponentsInChildren<Selectable>().FirstOrDefault(control => control.gameObject.activeInHierarchy && control.name == TabCaptions[index]);
            if (again != null) events.SetSelectedGameObject(again.gameObject);
        }

        // ---------------------------------------------------------------- drawing

        private Vector2 Room()
        {
            var size = ((RectTransform)transform).rect.size;
            if (size.x >= 200f && size.y >= 200f) return size;
            return scaler != null ? scaler.referenceResolution : new Vector2(1920f, 1080f);
        }

        private void Rebuild()
        {
            foreach (Transform child in transform) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            lines.Clear();
            var recap = WeeklyRecap.Build(shown, openWeek);
            var room = Room();
            float cardWidth = Mathf.Clamp(room.x - 72f, 860f, 1560f);
            Width = cardWidth - 40f;
            const float footer = 84f;

            // The house dimmed behind the week rather than blacked out, and the week on a glass
            // card, as the mockups set every summary over the room it is about.
            var scrim = HudPrimitives.Fill("Scrim", transform, new Color(UiTheme.Ink.r, UiTheme.Ink.g, UiTheme.Ink.b, 0.86f), 1);
            Stretch(scrim);
            HudPrimitives.Vignette(scrim);

            var card = HudPrimitives.Fill("Recap card", scrim, new Color(UiTheme.GlassFill.r, UiTheme.GlassFill.g, UiTheme.GlassFill.b, .94f), UiTheme.GlassRadius);
            card.anchorMin = card.anchorMax = new Vector2(0.5f, 0.5f);
            card.pivot = new Vector2(0.5f, 0.5f);
            card.sizeDelta = new Vector2(cardWidth, room.y - 64f);
            card.anchoredPosition = Vector2.zero;
            UiTheme.Glass(card, UiTheme.GlassRadius);

            // The controls first in the hierarchy, so the keyboard starts on Continue, and fixed at
            // the card's foot, so there is one of each whichever tab is open.
            var foot = new GameObject("Controls", typeof(RectTransform)).GetComponent<RectTransform>();
            foot.SetParent(card, false);
            foot.anchorMin = new Vector2(0.5f, 0f); foot.anchorMax = new Vector2(0.5f, 0f);
            foot.pivot = new Vector2(0.5f, 0f);
            foot.sizeDelta = new Vector2(Width, footer);
            foot.anchoredPosition = new Vector2(0f, 6f);
            Controls(foot, recap);

            viewport = HudPrimitives.Fill("Viewport", card, new Color(0f, 0f, 0f, 0f), 1);
            viewport.anchorMin = new Vector2(0.5f, 0f);
            viewport.anchorMax = new Vector2(0.5f, 1f);
            viewport.pivot = new Vector2(0.5f, 1f);
            viewport.sizeDelta = new Vector2(Width, -(footer + 16f));
            viewport.anchoredPosition = new Vector2(0f, -6f);
            viewport.gameObject.AddComponent<RectMask2D>();
            viewport.GetComponent<Image>().raycastTarget = true;

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

            cursor = Pad;
            float inner = Width - Pad * 2f;
            Title(recap, inner);
            Hero(recap, inner);
            HeadlineCards(recap, inner);
            Tabs(inner);
            var body = EndScreenKit.Box(TabBodyName, content, Pad, cursor, inner, 10f);
            float used;
            switch (tab)
            {
                case 1: used = VoteTab(body, recap, inner); break;
                case 2: used = EventsTab(body, recap, inner); break;
                case 3: used = ReactionsTab(body, recap, inner); break;
                case 4: used = NextTab(body, recap, inner); break;
                default: used = OverviewTab(body, recap, inner); break;
            }
            body.sizeDelta = new Vector2(inner, used);
            cursor += used + Pad;
            content.sizeDelta = new Vector2(0f, cursor);
            CollectLines(recap);

            // The card as tall as the week it holds, up to the screen: a short week drew its
            // Continue half-way down a card whose lower half was empty glass.
            float height = Mathf.Min(room.y - 64f, cursor + footer + 24f);
            card.sizeDelta = new Vector2(cardWidth, height);
        }

        /// <summary>The seam's lines: every fact of the week the tabs hold, whichever is open, in the old order.</summary>
        private void CollectLines(WeeklyRecap.Week recap)
        {
            lines.Add("Head of Household: " + (recap.headOfHousehold ?? "—"));
            lines.Add("Nominees: " + (recap.nominees.Count > 0 ? string.Join(", ", recap.nominees) : "—"));
            lines.Add("Veto: " + (recap.vetoHolder ?? "—"));
            lines.Add("Veto used: " + (recap.vetoUsed == null ? "No meeting" : recap.vetoUsed.Value ? "Yes" : "No"));
            lines.Add("Evicted: " + (recap.evicted ?? "—"));
            lines.Add("Remaining: " + recap.remaining);
            if (!string.IsNullOrEmpty(recap.yourWeek)) lines.Add(recap.yourWeek);
            lines.AddRange(recap.previously);
            foreach (var act in recap.acts) lines.AddRange(act);
            lines.AddRange(recap.ballots);
            foreach (var move in recap.relationships.Where(m => !m.aboutYou))
                lines.Add(move.Line + " (" + (move.delta > 0 ? "+" : "") + move.delta.ToString("0") + ")");
            lines.AddRange(recap.alliances);
            lines.AddRange(recap.deals);
            lines.AddRange(recap.happenings);
            lines.AddRange(recap.moments);
            lines.AddRange(recap.nextTime);
        }

        // ---------------------------------------------------------------- the head of the week

        private void Title(WeeklyRecap.Week recap, float inner)
        {
            var eyebrow = EndScreenKit.Text("Eyebrow", content, browsing ? "LOOKING BACK  ·  WEEK " + recap.week : "WEEK " + recap.week + "  ·  RECAP",
                13f, UiTheme.Heading, Pad, cursor, inner, 20f, TextAlignmentOptions.Center, UiTheme.Weight.SemiBold);
            eyebrow.characterSpacing = 4f;
            cursor += 22f;
            var title = EndScreenKit.Text("Title", content, "WEEK " + recap.week + " RECAP", 40f, Color.white, Pad, cursor, inner, 50f,
                TextAlignmentOptions.Center, UiTheme.Weight.Bold);
            title.characterSpacing = 3f;
            title.enableVertexGradient = true;
            title.colorGradient = new VertexGradient(UiTheme.Glow, UiTheme.Glow, UiTheme.Heading, UiTheme.Heading);
            cursor += 52f;
            var headline = EndScreenKit.Text("Headline", content, recap.Headline, 18f, UiTheme.Paper, Pad, cursor, inner, 26f, TextAlignmentOptions.Center);
            cursor += EndScreenKit.Wrapped(headline, inner) + 18f;
        }

        /// <summary>
        /// Who left: their face, their place and week, their words from the block (said before the
        /// vote, and nothing written for them), and the house they left behind. A week with nobody
        /// gone says so, and the player's own week takes the middle.
        /// </summary>
        private void Hero(WeeklyRecap.Week recap, float inner)
        {
            var hero = EndScreenKit.Box(HeroName, content, Pad, cursor, inner, 10f);
            var gone = shown.Find(recap.evictedId);
            const float height = 200f;
            bool wide = inner >= 1100f;
            float leftWidth = wide ? inner * .30f : inner * .48f;
            float rightWidth = wide ? inner * .24f : 0f;
            float middleWidth = wide ? inner - leftWidth - rightWidth - 32f : inner - leftWidth - 16f;

            var left = EndScreenKit.Box("Evictee", hero, 0f, 0f, leftWidth, height);
            EndScreenKit.Frame(left, PackArt.SeasonSection, 16f, UiTheme.Surface);
            if (gone != null)
            {
                var photo = HudPrimitives.RectPortrait(left, "Photo", CharacterPortraits.Get(gone), gone, new Vector2(128f, 164f), 10);
                EndScreenKit.Place(photo, 18f, 18f, 128f, 164f);
                EndScreenKit.Pill(left, "EVICTED", UiTheme.Danger, 26f, 152f, 112f, 24f, true);
                float textX = 164f, textWidth = leftWidth - textX - 16f;
                var name = EndScreenKit.Text("Name", left, HudPrimitives.WithYou(gone.name, gone.isPlayer), 24f, UiTheme.Paper, textX, 40f, textWidth, 60f,
                    TextAlignmentOptions.Left, UiTheme.Weight.Bold);
                name.enableAutoSizing = true; name.fontSizeMax = 24f; name.fontSizeMin = 15f;
                string place = WeeklyRecap.PlaceWords(recap.placement);
                EndScreenKit.Text("Place", left, (place != null ? place.ToUpperInvariant() + "  ·  " : "") + "WEEK " + recap.week, 13f, UiTheme.Muted,
                    textX, 104f, textWidth, 20f, TextAlignmentOptions.Left, UiTheme.Weight.SemiBold).characterSpacing = 2f;
                EndScreenKit.Text("Seat", left, gone.status == ContestantStatus.Jury ? "Joins the jury." : gone.status == ContestantStatus.Evicted ? "Out before the jury." : "",
                    13f, UiTheme.Muted, textX, 126f, textWidth, 20f);
            }
            else
            {
                EndScreenKit.Text("Nobody", left, "Nobody has left yet.", 20f, UiTheme.Paper, 20f, 60f, leftWidth - 40f, 28f,
                    TextAlignmentOptions.Left, UiTheme.Weight.SemiBold);
                EndScreenKit.Text("Why", left, "The week's vote is still to come.", 14f, UiTheme.Muted, 20f, 92f, leftWidth - 40f, 20f);
            }

            var middle = EndScreenKit.Box("Words", hero, leftWidth + 16f, 0f, middleWidth, height);
            EndScreenKit.Frame(middle, PackArt.SeasonQuote, 16f, UiTheme.Surface);
            if (gone != null && recap.evicteeQuote != null)
            {
                var said = EndScreenKit.Text("Quote", middle, "“" + EndScreenKit.Excerpt(recap.evicteeQuote, 180) + "”", 19f, UiTheme.Paper,
                    24f, 30f, middleWidth - 48f, 30f, TextAlignmentOptions.Center);
                said.fontStyle = FontStyles.Italic;
                float h = EndScreenKit.Wrapped(said, middleWidth - 48f, 4);
                EndScreenKit.Text("Attribution", middle, "— " + HudPrimitives.WithYou(gone.name, gone.isPlayer) + ", from the block on eviction night",
                    13f, UiTheme.Muted, 24f, 30f + h + 10f, middleWidth - 48f, 20f, TextAlignmentOptions.Center);
            }
            else
            {
                EndScreenKit.Text("Your week title", middle, "YOUR WEEK", 13f, UiTheme.Heading, 24f, 28f, middleWidth - 48f, 20f,
                    TextAlignmentOptions.Center, UiTheme.Weight.SemiBold).characterSpacing = 4f;
                var mine = EndScreenKit.Text("Your week", middle, string.IsNullOrEmpty(recap.yourWeek) ? "A quiet week for you." : recap.yourWeek, 18f, UiTheme.Paper,
                    24f, 56f, middleWidth - 48f, 26f, TextAlignmentOptions.Center);
                EndScreenKit.Wrapped(mine, middleWidth - 48f, 5);
            }

            if (wide)
            {
                var right = EndScreenKit.Box("House", hero, inner - rightWidth, 0f, rightWidth, height);
                string leftLine = gone != null ? (gone.isPlayer ? "YOU HAVE LEFT THE HOUSE." : gone.name.ToUpperInvariant() + " HAS LEFT THE HOUSE.") : "THE HOUSE IS WHOLE.";
                var who = EndScreenKit.Text("Left", right, leftLine, 19f, UiTheme.Paper, 12f, 46f, rightWidth - 24f, 50f, TextAlignmentOptions.Center, UiTheme.Weight.SemiBold);
                float h = EndScreenKit.Wrapped(who, rightWidth - 24f, 3);
                var rule = HudPrimitives.Fill("Rule", right, new Color(UiTheme.Hairline.r, UiTheme.Hairline.g, UiTheme.Hairline.b, .6f), 1);
                EndScreenKit.Place(rule, rightWidth * .25f, 46f + h + 12f, rightWidth * .5f, 2f);
                EndScreenKit.Text("Remaining", right, recap.remaining + (recap.remaining == 1 ? " HOUSEGUEST REMAINS" : " HOUSEGUESTS REMAIN"), 13f, UiTheme.Muted,
                    12f, 46f + h + 26f, rightWidth - 24f, 20f, TextAlignmentOptions.Center, UiTheme.Weight.SemiBold).characterSpacing = 2f;
            }
            hero.sizeDelta = new Vector2(inner, height);
            cursor += height + 16f;
        }

        /// <summary>
        /// The week's five headline facts - who held the house, whom they put up, the veto and what
        /// became of it, who left, and how many remain - each with its mark: gold for power, red for
        /// the block and the door, as the ceremonies draw them.
        /// </summary>
        private void HeadlineCards(WeeklyRecap.Week recap, float inner)
        {
            string Name(string id, string fallback) => id != null ? HudPrimitives.WithYou(shown.Find(id)?.name ?? id, id == shown.playerId) : fallback ?? "—";
            string nominated = recap.nominated.Count > 0 ? string.Join("\n", recap.nominated.Select(id => Name(id, null)))
                : recap.nominees.Count > 0 ? string.Join("\n", recap.nominees) : "—";
            string vetoValue = recap.vetoUsed == null ? (recap.vetoHolderId != null ? "Not yet" : "No meeting") : recap.vetoUsed.Value ? "Yes" : "No";
            string vetoLine = recap.vetoHolderId == null ? null
                : recap.vetoUsed == true && recap.savedId != null ? "by " + Name(recap.vetoHolderId, recap.vetoHolder) + ", on " + Name(recap.savedId, null)
                : "by " + Name(recap.vetoHolderId, recap.vetoHolder);
            var cards = new[]
            {
                ("HOH", Name(recap.hohId, recap.headOfHousehold), (string)null, "crown", UiTheme.Gold, PackArt.SeasonStatCompetitions),
                ("NOMINATED", nominated, recap.replacementId != null ? "then " + Name(recap.replacementId, null) : null, "target", UiTheme.Danger, PackArt.SeasonStatChances),
                ("VETO USED", vetoValue, vetoLine, "veto-token", UiTheme.Gold, PackArt.SeasonStatCompetitions),
                ("EVICTED", Name(recap.evictedId, recap.evicted), null, "evicted", UiTheme.Danger, PackArt.SeasonStatChances),
                ("REMAINING", recap.remaining.ToString(), "Houseguests", "people", UiTheme.Accent, PackArt.SeasonStatNeutral),
            };
            var row = EndScreenKit.Box(CardsName, content, Pad, cursor, inner, 10f);
            const float gap = 14f;
            float each = (inner - gap * (cards.Length - 1)) / cards.Length;
            float height = 138f;
            for (int i = 0; i < cards.Length; i++)
            {
                var (label, value, line, glyph, tint, frame) = cards[i];
                var tile = EndScreenKit.Box(label, row, i * (each + gap), 0f, each, height);
                EndScreenKit.Frame(tile, frame, 14f, new Color(UiTheme.Surface.r, UiTheme.Surface.g, UiTheme.Surface.b, .92f), new Color(tint.r, tint.g, tint.b, .45f));
                EndScreenKit.Picture("Mark", tile, null, glyph, tint, new Vector2(each * .5f, -30f), 30f);
                var name = EndScreenKit.Text("Label", tile, label, 12f, tint, 8f, 50f, each - 16f, 18f, TextAlignmentOptions.Center, UiTheme.Weight.SemiBold);
                name.characterSpacing = 3f;
                var shownValue = EndScreenKit.Text("Value", tile, value, 17f, value == "—" ? UiTheme.Muted : UiTheme.Paper, 8f, 70f, each - 16f, 44f,
                    TextAlignmentOptions.Center, UiTheme.Weight.SemiBold);
                shownValue.enableAutoSizing = true; shownValue.fontSizeMax = 17f; shownValue.fontSizeMin = 11f;
                if (line != null)
                {
                    var under = EndScreenKit.Text("Line", tile, line, 12f, UiTheme.Muted, 8f, 114f, each - 16f, 18f, TextAlignmentOptions.Center);
                    under.enableAutoSizing = true; under.fontSizeMax = 12f; under.fontSizeMin = 9f;
                }
            }
            row.sizeDelta = new Vector2(inner, height);
            cursor += height + 18f;
        }

        /// <summary>The tabs: controls named by their captions, the open one lit.</summary>
        private void Tabs(float inner)
        {
            var row = EndScreenKit.Box("Tabs", content, Pad, cursor, inner, 42f);
            const float gap = 10f;
            float each = (inner - gap * (TabCaptions.Length - 1)) / TabCaptions.Length;
            for (int i = 0; i < TabCaptions.Length; i++)
            {
                int index = i;
                bool open = i == tab;
                var panel = HudPrimitives.Fill(TabCaptions[i], row, open ? UiTheme.ActionBlue : UiTheme.SurfaceRaised, 10);
                EndScreenKit.Place(panel, i * (each + gap), 0f, each, 40f);
                var ground = panel.GetComponent<Image>();
                ground.raycastTarget = true;
                var art = EndScreenKit.Frame(panel, open ? PackArt.SeasonFilterActive : PackArt.SeasonTabInactive, 12f,
                    open ? UiTheme.ActionBlue : UiTheme.SurfaceRaised, open ? UiTheme.Glow : UiTheme.Outline, 10);
                bool packed = EndScreenKit.Packed(art);
                if (packed) ground.color = new Color(0f, 0f, 0f, 0f);
                var label = HudPrimitives.Label("Label", panel, 15f, open ? Color.white : UiTheme.Muted, TextAlignmentOptions.Center);
                label.text = Localisation.Text(TabCaptions[i]);
                label.enableAutoSizing = true; label.fontSizeMax = 15f; label.fontSizeMin = 10f;
                label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
                label.rectTransform.offsetMin = new Vector2(8f, 0f); label.rectTransform.offsetMax = new Vector2(-8f, 0f);
                var button = panel.gameObject.AddComponent<Button>();
                button.targetGraphic = packed ? (Graphic)art : ground;
                var colours = button.colors;
                colours.highlightedColor = new Color(1.18f, 1.18f, 1.18f);
                colours.selectedColor = colours.highlightedColor;
                button.colors = colours;
                button.onClick.AddListener(() => OpenTabAt(index));
            }
            cursor += 42f + 18f;
        }

        // ---------------------------------------------------------------- the tabs

        /// <summary>
        /// The week in three columns, as the mockup lays it: the vote split in two, the key moments
        /// on a timeline, and the evictee's record over the house's temperature. One under another on
        /// a narrow card.
        /// </summary>
        private float OverviewTab(RectTransform body, WeeklyRecap.Week recap, float inner)
        {
            bool three = inner >= 1100f;
            const float gap = 16f;
            float voteWidth = three ? (inner - gap * 2f) * .36f : inner;
            float momentsWidth = three ? (inner - gap * 2f) * .34f : inner;
            float sideWidth = three ? inner - voteWidth - momentsWidth - gap * 2f : inner;

            var vote = EndScreenKit.Box("The vote", body, 0f, 0f, voteWidth, 10f);
            float voteHeight = VoteSplit(vote, recap, voteWidth);
            var moments = EndScreenKit.Box("Key moments", body, three ? voteWidth + gap : 0f, three ? 0f : voteHeight + gap, momentsWidth, 10f);
            float momentsHeight = KeyMoments(moments, recap, momentsWidth);
            float sideX = three ? voteWidth + momentsWidth + gap * 2f : 0f;
            float sideY = three ? 0f : voteHeight + momentsHeight + gap * 2f;
            var side = EndScreenKit.Box("Record and temperature", body, sideX, sideY, sideWidth, 10f);
            float sideHeight = RecordAndTemperature(side, recap, sideWidth);
            if (three)
            {
                float height = Mathf.Max(voteHeight, Mathf.Max(momentsHeight, sideHeight));
                foreach (var column in new[] { vote, moments })
                {
                    column.sizeDelta = new Vector2(column.sizeDelta.x, height);
                    EndScreenKit.Frame(column, PackArt.SeasonSection, 16f, UiTheme.Surface);
                }
                return height;
            }
            foreach (var (column, height) in new[] { (vote, voteHeight), (moments, momentsHeight) })
            {
                column.sizeDelta = new Vector2(column.sizeDelta.x, height);
                EndScreenKit.Frame(column, PackArt.SeasonSection, 16f, UiTheme.Surface);
            }
            return voteHeight + momentsHeight + sideHeight + gap * 2f;
        }

        /// <summary>
        /// THE VOTE: a line of the count, then the evictee's votes and the other nominee's side by
        /// side, one face to a ballot; the Head of Household's tie-break on its own line, outside the
        /// count, as the engine counts it; and who did not vote, by the house's rule.
        /// </summary>
        private float VoteSplit(RectTransform column, WeeklyRecap.Week recap, float width)
        {
            const float pad = 16f;
            string Name(string id) => HudPrimitives.WithYou(shown.Find(id)?.name ?? id, id == shown.playerId);
            var (against, others) = WeeklyRecap.Split(recap);
            bool tie = recap.votes.Any(v => v.tieBreak);
            string summary = recap.evictedId == null ? "No vote this week yet."
                : recap.finalDecision ? Name(recap.hohId) + " chose, and " + Name(recap.evictedId) + " went to the jury."
                : tie ? "The vote tied " + against + "–" + others + ", and " + Name(recap.hohId) + " broke it."
                : against + others > 0 ? Name(recap.evictedId) + " was evicted by a vote of " + against + "–" + others + "."
                : Name(recap.evictedId) + " was evicted.";
            float y = pad + EndScreenKit.Heading(column, "The vote", summary, "gavel", pad, pad, width - pad * 2f);
            if (recap.votes.Count == 0) return y + pad;
            float half = (width - pad * 2f - 12f) * .5f;
            string other = recap.block.FirstOrDefault(id => id != recap.evictedId);
            float leftY = Side(column, "TO EVICT " + FirstName(recap.evictedId) + " (" + against + ")", UiTheme.Danger,
                recap.votes.Where(v => !v.tieBreak && v.targetId == recap.evictedId).ToList(), pad, y, half);
            float rightY = Side(column, other != null ? "TO KEEP " + FirstName(recap.evictedId) + " (" + others + ")" : "OTHERWISE (" + others + ")", UiTheme.Allied,
                recap.votes.Where(v => !v.tieBreak && v.targetId != recap.evictedId).ToList(), pad + half + 12f, y, half);
            y = Mathf.Max(leftY, rightY) + 6f;
            var breaker = recap.votes.FirstOrDefault(v => v.tieBreak);
            if (breaker != null)
            {
                var line = EndScreenKit.Text("Tie-break", column, Name(breaker.voterId) + " broke the tie: " + Name(breaker.targetId) + " goes home.", 13f, UiTheme.Gold,
                    pad, y, width - pad * 2f, 20f);
                y += EndScreenKit.Wrapped(line, width - pad * 2f) + 4f;
            }
            if (recap.hohId != null && !recap.finalDecision)
            {
                var rule = EndScreenKit.Text("Non-voters", column, (recap.hohId == shown.playerId ? "You vote" : Name(recap.hohId) + " votes") + " only to break a tie; the nominees do not vote.", 12f, UiTheme.Muted,
                    pad, y, width - pad * 2f, 18f);
                y += EndScreenKit.Wrapped(rule, width - pad * 2f) + 4f;
            }
            return y + pad;
        }

        private string FirstName(string id) => FinalistRead.FirstName(shown.Find(id)?.name ?? "").ToUpperInvariant();

        /// <summary>One side of the vote: its heading in its colour, then a face and a name for each ballot.</summary>
        private float Side(RectTransform column, string heading, Color tint, List<WeeklyRecap.Ballot> ballots, float x, float y, float width)
        {
            var head = EndScreenKit.Text("Side", column, heading, 13f, tint, x, y, width, 20f, TextAlignmentOptions.Left, UiTheme.Weight.SemiBold);
            head.characterSpacing = 2f;
            head.enableAutoSizing = true; head.fontSizeMax = 13f; head.fontSizeMin = 9f;
            y += 26f;
            foreach (var ballot in ballots)
            {
                var voter = shown.Find(ballot.voterId);
                var row = EndScreenKit.Box("Voter", column, x, y, width, 38f);
                EndScreenKit.Frame(row, PackArt.SeasonJuryRowNeutral, 10f, UiTheme.SurfaceRaised, null, 8);
                var face = HudPrimitives.Portrait(row, voter == null ? null : CharacterPortraits.Get(voter), tint, 26f, 2f, false, voter);
                face.anchorMin = face.anchorMax = new Vector2(0f, .5f);
                face.pivot = new Vector2(.5f, .5f);
                face.anchoredPosition = new Vector2(22f, 0f);
                var name = EndScreenKit.Text("Name", row, HudPrimitives.WithYou(voter?.name ?? ballot.voterId, ballot.voterId == shown.playerId), 14f,
                    ballot.voterId == shown.playerId ? UiTheme.Accent : UiTheme.Paper, 44f, 9f, width - 52f, 20f);
                name.enableAutoSizing = true; name.fontSizeMax = 14f; name.fontSizeMin = 10f;
                y += 44f;
            }
            if (ballots.Count == 0)
            {
                EndScreenKit.Text("None", column, "No votes.", 13f, UiTheme.Muted, x, y, width, 20f);
                y += 22f;
            }
            return y;
        }

        /// <summary>KEY MOMENTS: the week's ceremonies on a line, each with the face it belongs to and its mark.</summary>
        private float KeyMoments(RectTransform column, WeeklyRecap.Week recap, float width)
        {
            const float pad = 16f;
            float y = pad + EndScreenKit.Heading(column, "Key moments", null, "calendar", pad, pad, width - pad * 2f);
            if (recap.keyMoments.Count == 0)
            {
                EndScreenKit.Text("None", column, "Nothing happened this week yet.", 14f, UiTheme.Muted, pad, y, width - pad * 2f, 20f);
                return y + 24f + pad;
            }
            float w = width - pad * 2f;
            var line = HudPrimitives.Fill("Timeline", column, new Color(UiTheme.Heading.r, UiTheme.Heading.g, UiTheme.Heading.b, .45f), 1);
            float lineTop = y + 20f;
            foreach (var moment in recap.keyMoments)
            {
                bool power = moment.kind == "hoh" || moment.kind == "veto";
                string glyph = moment.kind == "hoh" ? "crown" : moment.kind == "veto" ? "veto-token" : moment.kind == "nominations" ? "target" : "evicted";
                var who = shown.Find(moment.subjectId);
                var row = EndScreenKit.Box("Moment", column, pad, y, w, 60f);
                var node = EndScreenKit.Picture("Node", row, power ? PackArt.SeasonNodePower : PackArt.SeasonNodeEviction, null, UiTheme.Accent, new Vector2(12f, -20f), 20f);
                if (node == null)
                {
                    var disc = HudPrimitives.Disc("Node", row, power ? UiTheme.Gold : UiTheme.Danger);
                    disc.anchorMin = disc.anchorMax = new Vector2(0f, 1f); disc.pivot = new Vector2(.5f, .5f);
                    disc.sizeDelta = new Vector2(14f, 14f); disc.anchoredPosition = new Vector2(12f, -20f);
                }
                var photo = HudPrimitives.RectPortrait(row, "Photo", who == null ? null : CharacterPortraits.Get(who), who, new Vector2(64f, 52f), 6);
                EndScreenKit.Place(photo, 32f, 2f, 64f, 52f);
                EndScreenKit.Picture("Mark", row, null, glyph, power ? UiTheme.Gold : UiTheme.Danger, new Vector2(88f, -44f), 16f);
                var title = EndScreenKit.Text("Title", row, moment.title.ToUpperInvariant(), 13f, power ? UiTheme.Gold : UiTheme.Heading, 108f, 4f, w - 108f, 18f,
                    TextAlignmentOptions.Left, UiTheme.Weight.SemiBold);
                title.characterSpacing = 1f;
                var words = EndScreenKit.Text("Line", row, moment.line, 13f, UiTheme.Paper, 108f, 24f, w - 108f, 18f);
                float h = Mathf.Max(60f, 24f + EndScreenKit.Wrapped(words, w - 108f) + 6f);
                row.sizeDelta = new Vector2(w, h);
                y += h + 8f;
            }
            EndScreenKit.Place(line, pad + 11f, lineTop, 2f, Mathf.Max(0f, y - lineTop - 40f));
            line.SetAsFirstSibling();
            return y + pad - 8f;
        }

        /// <summary>The evictee's record, in public facts, over the house's temperature, each name with its evidence.</summary>
        private float RecordAndTemperature(RectTransform side, WeeklyRecap.Week recap, float width)
        {
            const float pad = 16f, gap = 16f;
            var gone = shown.Find(recap.evictedId);
            float y = 0f;
            if (gone != null && recap.exitRecord.Count > 0)
            {
                var record = EndScreenKit.Box("Exit record", side, 0f, 0f, width, 10f);
                float ry = pad + EndScreenKit.Heading(record, (gone.isPlayer ? "Your" : FinalistRead.FirstName(gone.name) + "'s") + " exit record", "What the house saw of their season.",
                    "journal", pad, pad, width - pad * 2f);
                foreach (var fact in recap.exitRecord)
                {
                    var dot = HudPrimitives.Disc("Dot", record, UiTheme.Heading);
                    dot.anchorMin = dot.anchorMax = new Vector2(0f, 1f); dot.pivot = new Vector2(.5f, .5f);
                    dot.sizeDelta = new Vector2(6f, 6f); dot.anchoredPosition = new Vector2(pad + 4f, -(ry + 9f));
                    var words = EndScreenKit.Text("Fact", record, fact, 13f, UiTheme.Paper, pad + 14f, ry, width - pad * 2f - 14f, 18f);
                    ry += EndScreenKit.Wrapped(words, width - pad * 2f - 14f) + 4f;
                }
                ry += pad;
                record.sizeDelta = new Vector2(width, ry);
                EndScreenKit.Frame(record, PackArt.SeasonSection, 16f, UiTheme.Surface);
                y = ry + gap;
            }
            var heat = EndScreenKit.Box("House temperature", side, 0f, y, width, 10f);
            float hy = pad + EndScreenKit.Heading(heat, "House temperature", "Named from what you saw this week.", "people", pad, pad, width - pad * 2f);
            foreach (var reading in recap.temperature)
            {
                var label = EndScreenKit.Text("Reading", heat, reading.label, 15f, UiTheme.Paper, pad, hy, width - pad * 2f, 20f, TextAlignmentOptions.Left, UiTheme.Weight.SemiBold);
                hy += 22f;
                var evidence = EndScreenKit.Text("Evidence", heat, reading.evidence, 13f, UiTheme.Muted, pad, hy, width - pad * 2f, 18f);
                hy += EndScreenKit.Wrapped(evidence, width - pad * 2f) + 8f;
            }
            hy += pad - 8f;
            heat.sizeDelta = new Vector2(width, hy);
            EndScreenKit.Frame(heat, PackArt.SeasonSection, 16f, UiTheme.Surface);
            return y + hy;
        }

        /// <summary>The vote whole: every ballot as the reveal read it, with its reason, and the player's own week.</summary>
        private float VoteTab(RectTransform body, WeeklyRecap.Week recap, float inner)
        {
            float y = 0f;
            y = Section(body, "How the house voted", recap.ballots, UiTheme.Accent, y, inner);
            if (!string.IsNullOrEmpty(recap.yourWeek)) y = Section(body, "Your week", new List<string> { recap.yourWeek }, UiTheme.Heading, y, inner);
            return y > 0f ? y : Nothing(body, inner);
        }

        /// <summary>The week's events: the stories coming in, the four acts, the house's happenings, alliances, deals and turning points.</summary>
        private float EventsTab(RectTransform body, WeeklyRecap.Week recap, float inner)
        {
            float y = 0f;
            y = Section(body, "Previously on", recap.previously, UiTheme.Muted, y, inner);
            for (int act = 0; act < recap.acts.Length; act++)
                y = Section(body, WeeklyRecap.ActTitles[act], recap.acts[act], UiTheme.Joke, y, inner);
            y = Section(body, "What happened to the house", recap.happenings, UiTheme.Warning, y, inner);
            y = Section(body, "Alliances", recap.alliances, UiTheme.Accent, y, inner);
            y = Section(body, "Deals", recap.deals, UiTheme.Accent, y, inner);
            y = Section(body, "Turning points", recap.moments, UiTheme.Warning, y, inner);
            return y > 0f ? y : Nothing(body, inner);
        }

        /// <summary>The houseguests' reactions: what was said from the block, and where the player stands.</summary>
        private float ReactionsTab(RectTransform body, WeeklyRecap.Week recap, float inner)
        {
            float y = 0f;
            var speeches = shown.events.Where(e => e.week == recap.week && e.kind == "eviction-speech"
                    && (e.audienceIds.Count == 0 || e.audienceIds.Contains(shown.playerId)))
                .OrderBy(e => e.sequence).Select(e => e.text).ToList();
            y = Section(body, "From the block, eviction night", speeches, UiTheme.Danger, y, inner);
            // The player's own feelings, as their trust reads them; how the house feels about them
            // is the house's, and never shown during play.
            var moves = recap.relationships.Where(move => !move.aboutYou)
                .Select(move => move.Line + " (" + (move.delta > 0 ? "+" : "") + move.delta.ToString("0") + ")").ToList();
            y = Section(body, "Where you stand", moves, UiTheme.Allied, y, inner);
            return y > 0f ? y : Nothing(body, inner);
        }

        /// <summary>What comes next, and the stories still running.</summary>
        private float NextTab(RectTransform body, WeeklyRecap.Week recap, float inner)
        {
            float y = 0f;
            y = Section(body, browsing ? "What came next" : "What's next", recap.whatsNext, UiTheme.Accent, y, inner);
            y = Section(body, "Next time", recap.nextTime, UiTheme.Accent, y, inner);
            return y > 0f ? y : Nothing(body, inner);
        }

        private static float Nothing(RectTransform body, float inner)
        {
            EndScreenKit.Text("Nothing", body, "Nothing to show here for this week.", 15f, UiTheme.Muted, 0f, 8f, inner, 22f);
            return 40f;
        }

        /// <summary>A section of a tab: its heading over a soft rule, then a line behind a dot for each entry, as tall as its words.</summary>
        private static float Section(RectTransform body, string heading, List<string> entries, Color tint, float y, float inner)
        {
            if (entries == null || entries.Count == 0) return y;
            if (y > 0f) y += 14f;
            var label = EndScreenKit.Text("Heading", body, Localisation.Text(heading).ToUpperInvariant(), 14f, UiTheme.Heading, 0f, y, inner, 22f,
                TextAlignmentOptions.Left, UiTheme.Weight.SemiBold);
            label.characterSpacing = 4f;
            y += 24f;
            var rule = HudPrimitives.Fill("Rule", body, new Color(UiTheme.Hairline.r, UiTheme.Hairline.g, UiTheme.Hairline.b, .35f), 1);
            EndScreenKit.Place(rule, 0f, y, inner, 1f);
            y += 10f;
            foreach (string entry in entries)
            {
                var dot = HudPrimitives.Disc("Dot", body, tint);
                dot.anchorMin = dot.anchorMax = new Vector2(0f, 1f); dot.pivot = new Vector2(.5f, .5f);
                dot.sizeDelta = new Vector2(7f, 7f); dot.anchoredPosition = new Vector2(6f, -(y + 10f));
                var words = EndScreenKit.Text("Text", body, entry, 15f, new Color(UiTheme.Paper.r, UiTheme.Paper.g, UiTheme.Paper.b, .9f), 22f, y, inner - 22f, 22f);
                y += EndScreenKit.Wrapped(words, inner - 22f) + 4f;
            }
            return y;
        }

        // ---------------------------------------------------------------- the controls

        private void Controls(RectTransform foot, WeeklyRecap.Week recap)
        {
            if (browsing)
            {
                Button(foot, BackCaption, (Width - 300f) * .5f, 300f, Back, true, PackArt.KitIconArrowBack, null);
                return;
            }
            // Continue first, so the keyboard starts on it; Review beside it where there is an
            // earlier week to look at, and a line saying which week is next.
            Button(foot, ContinueCaption, (Width - 340f) * .5f, 340f, Dismiss, true, null, PackArt.KitIconChevronRight);
            if (openWeek > 1) Button(foot, ReviewCaption, Pad, 250f, () => Review(shown, openWeek - 1), false, PackArt.KitIconArrowBack, null);
            var next = EndScreenKit.Text("Next week", foot, "Week " + (openWeek + 1) + " is next.", 13f, UiTheme.Muted,
                (Width + 340f) * .5f + 16f, 30f, Mathf.Max(60f, Width - Pad - ((Width + 340f) * .5f + 16f)), 20f);
            next.enableAutoSizing = true; next.fontSizeMax = 13f; next.fontSizeMin = 10f;
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
                tab = 0;
                Rebuild();
                return;
            }
            Dismiss();
        }

        // ---------------------------------------------------------------- layout

        /// <summary>
        /// The recap's controls: Continue in the mockups' action blue, the rest quiet, each the panel
        /// named by its caption with the caption on it word for word; the pack's button and a glyph
        /// are decoration around it.
        /// </summary>
        private static void Button(RectTransform parent, string text, float x, float width, Action action, bool primary, string leadIcon, string trailIcon)
        {
            const float height = 52f;
            var panel = HudPrimitives.Fill(text, parent, primary ? UiTheme.ActionBlue : UiTheme.SurfaceRaised, 10);
            EndScreenKit.Place(panel, x, 16f, width, height);
            var ground = panel.GetComponent<Image>();
            ground.raycastTarget = true;
            var art = EndScreenKit.Frame(panel, primary ? PackArt.SeasonButtonPrimary : PackArt.SeasonButton, 14f,
                primary ? UiTheme.ActionBlue : UiTheme.SurfaceRaised, primary ? UiTheme.Glow : UiTheme.Outline, 10);
            bool packed = EndScreenKit.Packed(art);
            if (packed) ground.color = new Color(0f, 0f, 0f, 0f);
            else if (primary) UiTheme.AddGlow(panel, 10);
            float left = 12f, right = 12f;
            if (leadIcon != null && EndScreenKit.Picture("Glyph", panel, leadIcon, "exit", primary ? Color.white : UiTheme.Paper, new Vector2(24f, -height * .5f), 18f) != null) left = 38f;
            if (trailIcon != null && EndScreenKit.Picture("Glyph", panel, trailIcon, null, Color.white, new Vector2(width - 24f, -height * .5f), 18f) != null) right = 38f;

            var label = HudPrimitives.Label("Label", panel, 17f, primary ? Color.white : UiTheme.Paper, TextAlignmentOptions.Center);
            label.text = Localisation.Text(text);
            var weight = UiTheme.Font(primary ? UiTheme.Weight.SemiBold : UiTheme.Weight.Medium);
            if (weight != null) label.font = weight;
            label.enableAutoSizing = true; label.fontSizeMax = 17f; label.fontSizeMin = 12f;
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(left, 0f);
            label.rectTransform.offsetMax = new Vector2(-right, 0f);

            var button = panel.gameObject.AddComponent<Button>();
            button.targetGraphic = packed ? (Graphic)art : ground;
            var colours = button.colors;
            colours.highlightedColor = new Color(1.18f, 1.18f, 1.18f);
            colours.selectedColor = colours.highlightedColor;
            button.colors = colours;
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
