using System;
using System.Collections.Generic;
using System.Globalization;
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
    /// week's five headline facts; then six tabs - the week's overview (the vote split into the
    /// evictee's votes and the other nominee's, the key moments, the evictee's record and the house's
    /// temperature), the player's own week judged (<see cref="YourWeek"/>), the vote with every reason
    /// given, the week's events and stories, the houseguests' reactions, and what comes next.
    /// Continue is one control at the foot, whichever tab is open, and the previous week is "Review
    /// earlier weeks", as it always was.</para>
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

        /// <summary>The player's own week, judged (ACTIONS-DEALS-ALLIANCES-PLAN V4): a tab like the others.</summary>
        public const string YourWeekCaption = "Your week";

        /// <summary>The tabs. None of them is a way on: each shows a part of the week, and commits nothing.</summary>
        public static readonly string[] TabCaptions = { "Week overview", YourWeekCaption, "Vote breakdown", "Events & highlights", "Houseguest reactions", "What's next" };

        /// <summary>The parts a test finds by name: the head pinned over the scroll with the title in it, the hero, the cards and the open tab's body.</summary>
        public const string HeadName = "Week head", TitleName = "Title", HeroName = "Week hero", CardsName = "Week headlines", TabBodyName = "Week tab";

        /// <summary>The Your week tab's parts, by name: the week in a line, its cards (the empty week's included), and a judged line's row.</summary>
        public const string YourWeekLineName = "Your week line", ReadsName = "Reads and claims", WordName = "Deals and promises",
            CallsName = "Your calls", SenseName = "Game Sense so far", NothingJudgedName = "Nothing judged", VerdictRowName = "Judged line";

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

        /// <summary>
        /// The frame the week was last laid out for, and whether a new one has arrived. A canvas
        /// takes a new shape a frame after its cause - the larger text's scaler catching up with
        /// the preference, a capture's 16:9 target over the batch canvas's 4:3 - and a card sized
        /// to the old one stood past the screen at the larger text, its title off the top and its
        /// Continue off the foot (weekly-recap-overview-large; UI-UX-PASS-PLAN T0). So the week is
        /// laid out again, in LateUpdate, whenever the frame it was built for is not the one it is on.
        /// </summary>
        private Vector2 builtFor;
        private bool relayout;

        private void OnRectTransformDimensionsChange() { if (IsOpen) relayout = true; }

        private void LateUpdate()
        {
            if (!relayout) return;
            relayout = false;
            if (!IsOpen || shown == null || (Room() - builtFor).sqrMagnitude <= 1f) return;
            var events = EventSystem.current;
            var held = events != null && events.currentSelectedGameObject != null ? events.currentSelectedGameObject.name : null;
            Rebuild();
            if (events == null || held == null) return;
            // The same control by name, where the keyboard was: the one just built, never the copy on its way out.
            var again = GetComponentsInChildren<Selectable>().LastOrDefault(control => control.gameObject.activeInHierarchy && control.name == held);
            if (again != null) events.SetSelectedGameObject(again.gameObject);
        }

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
            var mine = YourWeek.Build(shown, openWeek);
            var room = Room();
            builtFor = room;
            float cardWidth = Mathf.Clamp(room.x - 72f, 860f, 1560f);
            Width = cardWidth - 40f;
            const float footer = 84f;
            // The eyebrow and the title, pinned at the card's head outside the scroll: only the
            // body scrolls, and the title stays in view whichever tab is open and however far down.
            const float head = Pad + 22f + 52f;

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

            var top = new GameObject(HeadName, typeof(RectTransform)).GetComponent<RectTransform>();
            top.SetParent(card, false);
            top.anchorMin = top.anchorMax = new Vector2(0.5f, 1f);
            top.pivot = new Vector2(0.5f, 1f);
            top.sizeDelta = new Vector2(Width, head);
            top.anchoredPosition = Vector2.zero;
            Head(top, recap, Width - Pad * 2f);

            viewport = HudPrimitives.Fill("Viewport", card, new Color(0f, 0f, 0f, 0f), 1);
            viewport.anchorMin = new Vector2(0.5f, 0f);
            viewport.anchorMax = new Vector2(0.5f, 1f);
            viewport.pivot = new Vector2(0.5f, 1f);
            viewport.sizeDelta = new Vector2(Width, -(head + footer + 10f));
            viewport.anchoredPosition = new Vector2(0f, -head);
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

            cursor = 6f;
            float inner = Width - Pad * 2f;
            Headline(recap, inner);
            Hero(recap, inner);
            HeadlineCards(recap, inner);
            Tabs(inner);
            var body = EndScreenKit.Box(TabBodyName, content, Pad, cursor, inner, 10f);
            float used;
            switch (tab)
            {
                case 1: used = YourWeekTab(body, recap, mine, inner); break;
                case 2: used = VoteTab(body, recap, inner); break;
                case 3: used = EventsTab(body, recap, inner); break;
                case 4: used = ReactionsTab(body, recap, inner); break;
                case 5: used = NextTab(body, recap, inner); break;
                default: used = OverviewTab(body, recap, inner); break;
            }
            body.sizeDelta = new Vector2(inner, used);
            cursor += used + Pad;
            content.sizeDelta = new Vector2(0f, cursor);
            CollectLines(recap, mine);

            // The card as tall as the week it holds, up to the screen: a short week drew its
            // Continue half-way down a card whose lower half was empty glass.
            float height = Mathf.Min(room.y - 64f, cursor + head + footer + 24f);
            card.sizeDelta = new Vector2(cardWidth, height);
        }

        /// <summary>The seam's lines: every fact of the week the tabs hold, whichever is open, in the old order, then the player's week judged.</summary>
        private void CollectLines(WeeklyRecap.Week recap, YourWeek.Week mine)
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
            // Your week: each judged line with its verdict, and Game Sense so far in the parts the player can see.
            foreach (var line in mine.Lines) lines.Add(line.ToString());
            lines.Add(SenseLine(mine.sense));
        }

        /// <summary>Game Sense so far in a line, for the seam: the strategy face and the chances taken, and when the player's season ended if it has.</summary>
        public static string SenseLine(YourWeek.Sense sense) =>
            "Game Sense so far: strategy " + sense.strategy + ", chances taken " + sense.taken + " of " + sense.offered + "."
            + (sense.Ended != null ? " " + sense.Ended : "");

        // ---------------------------------------------------------------- the head of the week

        /// <summary>The eyebrow and the title, in the head pinned over the scroll.</summary>
        private void Head(RectTransform top, WeeklyRecap.Week recap, float inner)
        {
            var eyebrow = EndScreenKit.Text("Eyebrow", top, browsing ? "LOOKING BACK  ·  WEEK " + recap.week : "WEEK " + recap.week + "  ·  RECAP",
                13f, UiTheme.Heading, Pad, Pad, inner, 20f, TextAlignmentOptions.Center, UiTheme.Weight.SemiBold);
            eyebrow.characterSpacing = 4f;
            var title = EndScreenKit.Text(TitleName, top, "WEEK " + recap.week + " RECAP", 40f, Color.white, Pad, Pad + 22f, inner, 50f,
                TextAlignmentOptions.Center, UiTheme.Weight.Bold);
            title.characterSpacing = 3f;
            title.enableVertexGradient = true;
            title.colorGradient = new VertexGradient(UiTheme.Glow, UiTheme.Glow, UiTheme.Heading, UiTheme.Heading);
        }

        /// <summary>The week's headline sentence, the first row of the body.</summary>
        private void Headline(WeeklyRecap.Week recap, float inner)
        {
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
        /// side - the count on each heading, and under it only the faces the player knows, each
        /// tagged by how they know it (KnownBallots), with a line for the ballots they do not; the
        /// Head of Household's tie-break on its own line, outside the count, as the engine counts
        /// it; and who did not vote, by the house's rule.
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
            if (recap.votes.Count == 0 && recap.unknownBallots == 0) return y + pad;
            float half = (width - pad * 2f - 12f) * .5f;
            string other = recap.block.FirstOrDefault(id => id != recap.evictedId);
            var toEvict = recap.votes.Where(v => !v.tieBreak && v.targetId == recap.evictedId).ToList();
            var toKeep = recap.votes.Where(v => !v.tieBreak && v.targetId != recap.evictedId).ToList();
            float leftY = Side(column, "TO EVICT " + FirstName(recap.evictedId) + " (" + against + ")", UiTheme.Danger,
                toEvict, Mathf.Max(0, against - toEvict.Count), pad, y, half);
            float rightY = Side(column, other != null ? "TO KEEP " + FirstName(recap.evictedId) + " (" + others + ")" : "OTHERWISE (" + others + ")", UiTheme.Allied,
                toKeep, Mathf.Max(0, others - toKeep.Count), pad + half + 12f, y, half);
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

        /// <summary>
        /// One side of the vote: its heading in its colour with the count, then a face and a name
        /// for each ballot the player knows, tagged by how they know it, and a line for the ones
        /// they do not (<paramref name="unknown"/>). Only the count is the house's: a face appears
        /// here because the player's own ballot, the tie-break, the count's proof or what they were
        /// told put it here, never the reveal.
        /// </summary>
        private float Side(RectTransform column, string heading, Color tint, List<WeeklyRecap.Ballot> ballots, int unknown, float x, float y, float width)
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
                // Every box at least 1.3 times its type: Inter draws nothing in a box under 1.21.
                var name = EndScreenKit.Text("Name", row, HudPrimitives.WithYou(voter?.name ?? ballot.voterId, ballot.voterId == shown.playerId), 14f,
                    ballot.voterId == shown.playerId ? UiTheme.Accent : UiTheme.Paper, 44f, 2f, width - 52f, 19f);
                name.enableAutoSizing = true; name.fontSizeMax = 14f; name.fontSizeMin = 10f;
                var basis = EndScreenKit.Text("Basis", row, BasisWords(ballot), 11f, UiTheme.Muted, 44f, 21f, width - 52f, 15f);
                basis.enableAutoSizing = true; basis.fontSizeMax = 11f; basis.fontSizeMin = 8f;
                y += 44f;
            }
            if (unknown > 0)
            {
                EndScreenKit.Text("Unknown ballots", column, unknown == 1 ? "1 ballot you do not know." : unknown + " ballots you do not know.", 13f, UiTheme.Muted, x, y, width, 20f);
                y += 22f;
            }
            else if (ballots.Count == 0)
            {
                EndScreenKit.Text("None", column, "No votes.", 13f, UiTheme.Muted, x, y, width, 20f);
                y += 22f;
            }
            return y;
        }

        /// <summary>How the player knows a ballot, for the tag under its name: the basis word, and a lie caught where the voter said otherwise.</summary>
        private string BasisWords(WeeklyRecap.Ballot ballot)
        {
            string words = KnownBallots.Basis.Word(ballot.basis);
            if (ballot.lied && ballot.saidId != null)
                words += " · said " + FinalistRead.FirstName(shown.Find(ballot.saidId)?.name ?? "") + ", a lie";
            return words;
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

        /// <summary>
        /// The vote whole: the count as the house heard it and the player's own ballot's line, then
        /// every ballot the player knows with how they know it, and how many they do not. Never a
        /// ballot the reveal kept private. The player's own week has its own tab.
        /// </summary>
        private float VoteTab(RectTransform body, WeeklyRecap.Week recap, float inner)
        {
            float y = 0f;
            y = Section(body, "How the house voted", recap.ballots, UiTheme.Accent, y, inner);
            var known = recap.votes.Select(ballot =>
                HudPrimitives.WithYou(shown.Find(ballot.voterId)?.name ?? ballot.voterId, ballot.voterId == shown.playerId)
                + " voted to evict " + HudPrimitives.WithYou(shown.Find(ballot.targetId)?.name ?? ballot.targetId, ballot.targetId == shown.playerId)
                + " · " + BasisWords(ballot) + (ballot.reason != null ? " · “" + ballot.reason + "”" : "")).ToList();
            if (recap.unknownBallots > 0) known.Add(VoteRecords.UnknownBallotsLine(recap.unknownBallots));
            y = Section(body, "Ballots you know", known, UiTheme.Accent, y, inner);
            return y > 0f ? y : Nothing(body, inner);
        }

        // ---------------------------------------------------------------- your week

        /// <summary>
        /// YOUR WEEK (ACTIONS-DEALS-ALLIANCES-PLAN V4): the player's week in a line, then what the
        /// week made of everything they held - their whip count and every claim against the ballots
        /// the reveal read, the deals and promises that ended as they were told it, the calls they
        /// made and who followed - each with its verdict on a chip, and Game Sense so far in the parts
        /// they can already see. Every judged line is <see cref="YourWeek"/>'s, read from committed
        /// state. The cards fall into two columns on a wide card, each into the shorter, and one under
        /// another on a narrow one.
        /// </summary>
        private float YourWeekTab(RectTransform body, WeeklyRecap.Week recap, YourWeek.Week mine, float inner)
        {
            float top = 0f;
            if (!string.IsNullOrEmpty(recap.yourWeek))
            {
                var said = EndScreenKit.Text(YourWeekLineName, body, recap.yourWeek, 17f, UiTheme.Paper, 0f, 0f, inner, 24f);
                top = EndScreenKit.Wrapped(said, inner) + 14f;
            }
            bool two = inner >= 1100f;
            const float gap = 16f;
            float leftWidth = two ? (inner - gap) * .56f : inner;
            float rightWidth = two ? inner - leftWidth - gap : inner;
            float left = top, right = top;

            var cards = new List<(string name, string title, string subtitle, string icon, List<YourWeek.Line> lines)>();
            if (mine.Empty)
                cards.Add((NothingJudgedName, "Your week, judged", "Nothing you held came due this week: no read, claim, deal, promise or call.", "task", null));
            if (mine.reads.Count > 0)
                cards.Add((ReadsName, "Reads and claims", "Judged against the ballots you know.", "eye", mine.reads));
            if (mine.word.Count > 0)
                cards.Add((WordName, "Deals and promises", "As far as you can know how they ended.", "handshake", mine.word));
            if (mine.calls.Count > 0)
                cards.Add((CallsName, "Your calls", "Who followed you, as they said at the call.", "people", mine.calls));

            foreach (var card in cards)
            {
                bool intoLeft = !two || left <= right;
                float x = intoLeft ? 0f : leftWidth + gap, width = intoLeft ? leftWidth : rightWidth;
                float y = !two ? left : intoLeft ? left : right;
                float used = Judged(body, card.name, card.title, card.subtitle, card.icon, card.lines, x, y, width);
                if (!two || intoLeft) left = y + used + gap; else right = y + used + gap;
            }
            {
                bool intoLeft = !two || left <= right;
                float x = intoLeft ? 0f : leftWidth + gap, width = intoLeft ? leftWidth : rightWidth;
                float y = !two ? left : intoLeft ? left : right;
                float used = SenseCard(body, mine.sense, x, y, width);
                if (!two || intoLeft) left = y + used + gap; else right = y + used + gap;
            }
            return Mathf.Max(left, right) - gap;
        }

        /// <summary>One card of judged lines: its heading, then each line behind its verdict chip and the face it is about.</summary>
        private float Judged(RectTransform body, string name, string title, string subtitle, string icon, List<YourWeek.Line> judged,
            float x, float y, float width)
        {
            const float pad = 16f;
            var card = EndScreenKit.Box(name, body, x, y, width, 10f);
            float h = pad + EndScreenKit.Heading(card, title, subtitle, icon, pad, pad, width - pad * 2f);
            foreach (var line in judged ?? new List<YourWeek.Line>())
                h += VerdictRow(card, line, pad, h, width - pad * 2f);
            h += pad - 6f;
            card.sizeDelta = new Vector2(width, h);
            EndScreenKit.Frame(card, PackArt.SeasonSection, 16f, UiTheme.Surface);
            return h;
        }

        /// <summary>
        /// A judged line: its verdict on a chip (a call's own line has none, and a dot instead), the
        /// face of whoever it is about, and the line as tall as its words. Returns the height it took.
        /// </summary>
        private float VerdictRow(RectTransform card, YourWeek.Line line, float x, float y, float width)
        {
            const float chip = 108f, chipHeight = 24f, face = 24f;
            var row = EndScreenKit.Box(VerdictRowName, card, x, y, width, 28f);
            float textX = 0f;
            if (line.verdict != null)
            {
                EndScreenKit.Pill(row, VerdictWord(line.verdict), VerdictTint(line.verdict), 0f, 2f, chip, chipHeight);
                textX = chip + 10f;
            }
            else
            {
                var dot = HudPrimitives.Disc("Dot", row, UiTheme.Heading);
                dot.anchorMin = dot.anchorMax = new Vector2(0f, 1f); dot.pivot = new Vector2(.5f, .5f);
                dot.sizeDelta = new Vector2(7f, 7f); dot.anchoredPosition = new Vector2(6f, -14f);
                textX = 18f;
            }
            var who = line.aboutId != null ? shown.Find(line.aboutId) : null;
            if (who != null)
            {
                var portrait = HudPrimitives.Portrait(row, CharacterPortraits.Get(who), VerdictTint(line.verdict), face - 4f, 2f, false, who);
                portrait.anchorMin = portrait.anchorMax = new Vector2(0f, 1f);
                portrait.pivot = new Vector2(.5f, .5f);
                portrait.anchoredPosition = new Vector2(textX + face * .5f, -14f);
                textX += face + 8f;
            }
            var words = EndScreenKit.Text("Line", row, line.text, 14f, new Color(UiTheme.Paper.r, UiTheme.Paper.g, UiTheme.Paper.b, .92f),
                textX, 4f, width - textX, 20f);
            float height = Mathf.Max(28f, EndScreenKit.Wrapped(words, width - textX) + 8f);
            row.sizeDelta = new Vector2(width, height);
            return height + 4f;
        }

        /// <summary>The Game Sense card's subtitle: what its number is built from, and what waits for the season's end.</summary>
        public const string SenseSubtitle = "Built from what you can see. Competition odds, plans made against you and how the house sees you count only when the season ends.";

        /// <summary>
        /// GAME SENSE SO FAR: the season report's verdict in the parts the player can already see -
        /// the strategy face, made of public and player-owned rows, and the chances taken - then the
        /// week's own rows with their points: the power, their ballot, their chances and calls, and a
        /// competition thrown or fought from the block. The number itself, the odds every other
        /// competition row is weighed by, another Head of Household's plan and how the house sees the
        /// player wait for the season's end, and the subtitle says so. After the player's season ended,
        /// the number holds where it stood and the card says when it ended instead of listing rows.
        /// </summary>
        private float SenseCard(RectTransform body, YourWeek.Sense sense, float x, float y, float width)
        {
            const float pad = 16f;
            var card = EndScreenKit.Box(SenseName, body, x, y, width, 10f);
            float h = pad + EndScreenKit.Heading(card, "Game Sense so far", SenseSubtitle, "bulb", pad, pad, width - pad * 2f);
            float half = (width - pad * 2f - 12f) * .5f;
            Stat(card, "STRATEGY", sense.strategy.ToString(CultureInfo.InvariantCulture), UiTheme.Positive, pad, h, half);
            Stat(card, "CHANCES TAKEN", sense.taken + " of " + sense.offered, sense.offered == 0 || sense.taken * 2 >= sense.offered ? UiTheme.Positive : UiTheme.Danger,
                pad + half + 12f, h, half);
            h += 66f;
            if (sense.Ended != null || sense.rows.Count == 0)
            {
                var none = EndScreenKit.Text("None", card, sense.Ended ?? "No rows of your own this week.", 14f, UiTheme.Muted, pad, h, width - pad * 2f, 20f);
                h += EndScreenKit.Wrapped(none, width - pad * 2f) + 6f;
            }
            foreach (var note in sense.rows)
            {
                var row = EndScreenKit.Box("Sense row", card, pad, h, width - pad * 2f, 28f);
                var tint = note.points > 0 ? UiTheme.Positive : note.points < 0 ? UiTheme.Danger : UiTheme.Muted;
                EndScreenKit.Pill(row, note.points.ToString("+0;-0;0", CultureInfo.InvariantCulture), tint, 0f, 2f, 56f, 24f);
                var words = EndScreenKit.Text("Line", row, YourWeek.RowText(note), 14f, new Color(UiTheme.Paper.r, UiTheme.Paper.g, UiTheme.Paper.b, .92f),
                    66f, 4f, width - pad * 2f - 66f, 20f);
                float rowHeight = Mathf.Max(28f, EndScreenKit.Wrapped(words, width - pad * 2f - 66f) + 8f);
                row.sizeDelta = new Vector2(width - pad * 2f, rowHeight);
                h += rowHeight + 4f;
            }
            h += pad - 6f;
            card.sizeDelta = new Vector2(width, h);
            EndScreenKit.Frame(card, PackArt.SeasonSection, 16f, UiTheme.Surface);
            return h;
        }

        /// <summary>A stat in the Game Sense card: its caption over its number, in its colour.</summary>
        private static void Stat(RectTransform card, string caption, string value, Color tint, float x, float y, float width)
        {
            var label = EndScreenKit.Text("Stat caption", card, caption, 12f, UiTheme.Muted, x, y, width, 18f, TextAlignmentOptions.Left, UiTheme.Weight.SemiBold);
            label.characterSpacing = 2f;
            label.enableAutoSizing = true; label.fontSizeMax = 12f; label.fontSizeMin = 9f;
            var number = EndScreenKit.Text("Stat value", card, value, 30f, tint, x, y + 18f, width, 40f, TextAlignmentOptions.Left, UiTheme.Weight.SemiBold);
            number.enableAutoSizing = true; number.fontSizeMax = 30f; number.fontSizeMin = 16f;
        }

        /// <summary>A verdict as its chip says it.</summary>
        public static string VerdictWord(string verdict) => (verdict ?? "").ToUpperInvariant();

        /// <summary>A verdict's colour: green for what held, red for what did not, quiet for what nobody can know yet.</summary>
        private static Color VerdictTint(string verdict)
        {
            switch (verdict)
            {
                case YourWeek.Verdicts.Right:
                case YourWeek.Verdicts.Kept:
                case YourWeek.Verdicts.Followed: return UiTheme.Positive;
                case YourWeek.Verdicts.Wrong:
                case YourWeek.Verdicts.Broken:
                case YourWeek.Verdicts.Defected: return UiTheme.Danger;
                default: return UiTheme.Muted;
            }
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
