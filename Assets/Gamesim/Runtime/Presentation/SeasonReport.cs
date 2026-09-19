using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Gamesim.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gamesim.Presentation
{
    /// <summary>
    /// The season report: the screen a finished season ends on.
    ///
    /// <para>Until this existed, completing a whole season produced two sentences of body text in
    /// the HUD — "Winner: X. Runner-up: Y." — after a run that tracked competition records,
    /// nominations, relationships, alliances and a jury vote. Everything needed to close the season
    /// properly was already in <see cref="EpisodeState"/>; nothing was reading it back.</para>
    ///
    /// <para>It is built entirely from committed state and derives nothing the simulation does not
    /// already record, so it cannot disagree with the save. That also means it required no
    /// simulation change to add, and cannot regress a season.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SeasonReport : MonoBehaviour
    {
        private const float Width = 1180f;
        private const float Pad = 28f;

        private RectTransform content;
        private RectTransform viewport;
        private CanvasGroup group;
        private float cursor;

        private CanvasScaler scaler;

        /// <summary>How the house table is ordered. Captions are what tests and readers find.</summary>
        public enum CastSort { Placement, Name, HohWins, VetoWins, Nominations }

        /// <summary>Which part of the house the table is showing.</summary>
        public enum CastFilter { Everyone, Finalists, Jury, Evicted }

        public static string SortCaption(CastSort by)
        {
            switch (by)
            {
                case CastSort.Name: return "Sort by name";
                case CastSort.HohWins: return "Sort by HoH wins";
                case CastSort.VetoWins: return "Sort by veto wins";
                case CastSort.Nominations: return "Sort by nominations";
                default: return "Sort by placement";
            }
        }

        public static string FilterCaption(CastFilter which)
        {
            switch (which)
            {
                case CastFilter.Finalists: return "Finalists";
                case CastFilter.Jury: return "The jury";
                case CastFilter.Evicted: return "Evicted before jury";
                default: return "Everyone";
            }
        }

        // The table's ordering and filter are read back out by the screen when it rebuilds itself,
        // so they live here rather than being passed down. They are presentation and nothing else:
        // no part of a finished season changes because somebody sorted a column.
        private CastSort sortBy = CastSort.Placement;
        private CastFilter filter = CastFilter.Everyone;

        /// <summary>
        /// Who the house table is currently listing, in the order it lists them.
        ///
        /// <para>Exposed because the rest of the screen names houseguests too — the standings list
        /// everybody whatever the table is filtered to — so "is this person on screen" cannot answer
        /// "is this person in the table".</para>
        /// </summary>
        public IReadOnlyList<string> TableNames => tableNames;

        private List<string> tableNames = new List<string>();

        // Held so a sort or filter can redraw without the caller having to hand them over again.
        private EpisodeState shown;
        private Func<string, Texture> shownPortrait;
        private Action shownReview;

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
        /// A canvas of its own, above the ceremony cards. Unlike those it *does* raycast: this
        /// screen is read and scrolled rather than watched, so it needs to take input.
        /// </summary>
        public static SeasonReport Attach(GameObject owner)
        {
            var root = new GameObject("Gamesim Season Report",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler),
                typeof(GraphicRaycaster), typeof(CanvasGroup));
            root.transform.SetParent(owner.transform, false);

            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 120;

            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var report = root.AddComponent<SeasonReport>();
            report.scaler = scaler;
            report.group = root.GetComponent<CanvasGroup>();
            report.Hide();
            return report;
        }

        public void Hide()
        {
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;
        }

        /// <summary>
        /// Builds the report for a finished season. <paramref name="portrait"/> resolves a
        /// contestant id to a face; it may return null, and the portrait helper draws a hole.
        /// </summary>
        public void Show(EpisodeState state, Func<string, Texture> portrait, Action onReview)
        {
            if (state == null) return;
            shown = state;
            shownPortrait = portrait ?? (_ => null);
            shownReview = onReview;
            sortBy = CastSort.Placement;
            filter = CastFilter.Everyone;
            Rebuild(state, shownPortrait, onReview);
            group.alpha = 1f;
            group.blocksRaycasts = true;
            group.interactable = true;
        }

        private void Rebuild(EpisodeState state, Func<string, Texture> portrait, Action onReview)
        {
            foreach (Transform child in transform) Destroy(child.gameObject);

            var scrim = HudPrimitives.Fill("Scrim", transform, new Color(0.02f, 0.04f, 0.06f, 0.97f), 1);
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
            Header(state);
            Winner(state, portrait);
            WinnersJourney(state);
            YourJourney(state);
            Standings(state);
            WeekByWeek(state);
            Cast(state, portrait);
            Buttons(onReview);

            content.sizeDelta = new Vector2(0f, cursor + Pad);
        }

        // ---------------------------------------------------------------- sections

        private void Header(EpisodeState state)
        {
            Space(Pad);
            Text("SEASON COMPLETE", 26f, UiTheme.Gold, 34f, TextAlignmentOptions.Center);
            Text(state.week + (state.week == 1 ? " week" : " weeks") + " · "
                + state.contestants.Count + " houseguests · "
                + state.contestants.Count(c => c.status == ContestantStatus.Jury) + " on the jury",
                17f, UiTheme.Muted, 26f, TextAlignmentOptions.Center);
            // The web build shows "You watched this season as a spectator" on this screen when the
            // player was evicted. Same statement, drawn from status rather than a stored flag.
            var you = state.Find(state.playerId);
            if (you != null && you.status != ContestantStatus.Winner && you.status != ContestantStatus.RunnerUp)
            {
                Text("YOU WATCHED THE REST OF THIS SEASON AS A SPECTATOR",
                    14f, UiTheme.Warning, 22f, TextAlignmentOptions.Center);
            }

            Space(10f);
        }

        private void Winner(EpisodeState state, Func<string, Texture> portrait)
        {
            var winner = state.Find(state.winnerId);
            var runnerUp = state.Find(state.runnerUpId);
            if (winner == null) return;

            var card = Panel(216f, UiTheme.SurfaceRaised);
            var row = new GameObject("Finalists", typeof(RectTransform)).GetComponent<RectTransform>();
            row.SetParent(card, false);
            row.anchorMin = new Vector2(0.5f, 0.5f);
            row.anchorMax = new Vector2(0.5f, 0.5f);
            row.pivot = new Vector2(0.5f, 0.5f);
            row.anchoredPosition = new Vector2(0f, 6f);

            Finalist(row, -180f, winner, portrait, "WINNER", UiTheme.Gold, 108f);
            if (runnerUp != null)
                Finalist(row, 180f, runnerUp, portrait, "RUNNER-UP", UiTheme.Muted, 84f);

            var label = HudPrimitives.Label("Crown", card, 15f, UiTheme.Muted, TextAlignmentOptions.Center);
            label.text = runnerUp != null
                ? winner.name + " beat " + runnerUp.name + " in the jury vote."
                : winner.name + " wins the season.";
            Place(label.rectTransform, Width - Pad * 4f, 22f, -186f);
        }

        private void Finalist(Transform parent, float x, ContestantState who,
            Func<string, Texture> portrait, string badge, Color tint, float size)
        {
            var holder = new GameObject(badge, typeof(RectTransform)).GetComponent<RectTransform>();
            holder.SetParent(parent, false);
            holder.anchoredPosition = new Vector2(x, 0f);

            var face = HudPrimitives.Portrait(holder, portrait(who.id), tint, size, 4f, false);
            face.anchoredPosition = new Vector2(0f, 26f);

            var name = HudPrimitives.Label("Name", holder, 19f, UiTheme.Paper, TextAlignmentOptions.Center);
            name.text = who.name;
            name.rectTransform.sizeDelta = new Vector2(300f, 26f);
            name.rectTransform.anchoredPosition = new Vector2(0f, -38f);

            var tag = HudPrimitives.Label("Badge", holder, 13f, tint, TextAlignmentOptions.Center);
            tag.text = badge;
            tag.rectTransform.sizeDelta = new Vector2(300f, 20f);
            tag.rectTransform.anchoredPosition = new Vector2(0f, -60f);
        }

        /// <summary>
        /// How the champion got there: the three numbers that describe a winning season.
        ///
        /// <para>Skipped entirely when the player won it, because the block underneath already says
        /// all of this about the same person and two identical cards in a row reads as a bug rather
        /// than as emphasis.</para>
        /// </summary>
        private void WinnersJourney(EpisodeState state)
        {
            var champion = state.Find(state.winnerId);
            if (champion == null || champion.isPlayer) return;

            Heading(champion.name + "'s road to the end");
            var card = Panel(132f, UiTheme.Surface);

            Stat(card, 0, "HOH WINS", champion.hohWins.ToString(), UiTheme.Accent);
            Stat(card, 1, "VETO WINS", champion.vetoWins.ToString(), UiTheme.Gold);
            Stat(card, 2, "NOMINATED", champion.timesNominated.ToString(),
                champion.timesNominated > 0 ? UiTheme.Danger : UiTheme.Positive);
            Stat(card, 3, "COMP WINS", (champion.hohWins + champion.vetoWins).ToString(), UiTheme.Positive);
            Stat(card, 4, "WITH YOU",
                Math.Round(state.Score(state.playerId, champion.id)).ToString(CultureInfo.InvariantCulture),
                UiTheme.Muted);

            var note = HudPrimitives.Label("Note", card, 15f, UiTheme.Muted, TextAlignmentOptions.Center);
            note.text = WinnerNote(champion);
            Place(note.rectTransform, Width - Pad * 4f, 24f, -100f);
        }

        /// <summary>
        /// One line on how the champion played it, from the shape of their own record rather than
        /// from a phrase picked at random — a winner who never won anything and a winner who won
        /// everything did not have the same season and should not be described the same way.
        /// </summary>
        private static string WinnerNote(ContestantState champion)
        {
            int comps = champion.hohWins + champion.vetoWins;
            if (comps == 0 && champion.timesNominated == 0)
                return "Never on the block, never in charge. Nobody ever thought to move on them.";
            if (comps == 0)
                return "Won nothing and survived anyway, which is the harder way to do it.";
            if (champion.timesNominated == 0)
                return "Won " + comps + " and was never once put up for it.";
            return "Won " + comps + " and survived the block " + champion.timesNominated
                   + (champion.timesNominated == 1 ? " time." : " times.");
        }

        /// <summary>The player's own season, which is the part they actually came for.</summary>
        private void YourJourney(EpisodeState state)
        {
            var you = state.Find(state.playerId);
            if (you == null) return;

            Heading("Your season");
            var card = Panel(132f, UiTheme.Surface);

            string placement = Placement(state, you);
            Stat(card, 0, "FINISHED", placement, PlacementTint(you.status));
            Stat(card, 1, "HOH WINS", you.hohWins.ToString(), UiTheme.Accent);
            Stat(card, 2, "VETO WINS", you.vetoWins.ToString(), UiTheme.Gold);
            Stat(card, 3, "NOMINATED", you.timesNominated.ToString(),
                you.timesNominated > 0 ? UiTheme.Danger : UiTheme.Positive);
            Stat(card, 4, "COMP WINS", (you.hohWins + you.vetoWins).ToString(), UiTheme.Positive);

            var note = HudPrimitives.Label("Note", card, 15f, UiTheme.Muted, TextAlignmentOptions.Center);
            note.text = ClosingNote(state, you);
            Place(note.rectTransform, Width - Pad * 4f, 24f, -100f);
        }

        private void Standings(EpisodeState state)
        {
            Heading("Final standings");

            var order = new List<ContestantState>();
            var winner = state.Find(state.winnerId);
            var runnerUp = state.Find(state.runnerUpId);
            if (winner != null) order.Add(winner);
            if (runnerUp != null) order.Add(runnerUp);

            // Jury, then pre-jury, each most-recently-evicted first. Eviction order is read from the
            // event log rather than stored as a ranking, so a season that ended early still orders.
            var evictionOrder = EvictionOrder(state);
            foreach (var status in new[] { ContestantStatus.Jury, ContestantStatus.Evicted })
            {
                order.AddRange(state.contestants
                    .Where(c => c.status == status && c != winner && c != runnerUp)
                    .OrderByDescending(c => evictionOrder.TryGetValue(c.id, out int w) ? w : 0));
            }

            for (int i = 0; i < order.Count; i++)
            {
                var who = order[i];
                var line = Panel(34f, i % 2 == 0 ? UiTheme.Surface : UiTheme.SurfaceRaised);

                Cell(line, 22f, 44f, (i + 1).ToString(), 15f, UiTheme.Muted, TextAlignmentOptions.Center);
                Cell(line, 78f, 320f, who.name + (who.isPlayer ? "  (You)" : string.Empty), 16f,
                    who.isPlayer ? UiTheme.Accent : UiTheme.Paper, TextAlignmentOptions.Left);
                Cell(line, 420f, 200f, StatusWord(who.status), 15f, PlacementTint(who.status),
                    TextAlignmentOptions.Left);
                Cell(line, 640f, 460f,
                    who.hohWins + " HoH · " + who.vetoWins + " veto · nominated " + who.timesNominated,
                    14f, UiTheme.Muted, TextAlignmentOptions.Left);
            }
        }

        /// <summary>
        /// The season laid out by week: who held power, who was nominated, who went home.
        ///
        /// <para>Nominations and evictions are exact — nomination weeks are stored per contestant and
        /// eviction events carry their week. <b>The Head of Household is inferred</b>: the HoH and
        /// veto competitions log the same event type through the same resolver, so the first
        /// competition recorded in a week is taken as the HoH and the second as the veto. That holds
        /// because the weekly phase order is fixed; if that order ever changes, this column is the
        /// thing that quietly goes wrong.</para>
        /// </summary>
        private void WeekByWeek(EpisodeState state)
        {
            Heading("Week by week");

            var header = Panel(30f, UiTheme.Ink);
            Cell(header, 22f, 90f, "WEEK", 13f, UiTheme.Muted, TextAlignmentOptions.Left);
            Cell(header, 120f, 260f, "HEAD OF HOUSEHOLD", 13f, UiTheme.Muted, TextAlignmentOptions.Left);
            Cell(header, 400f, 380f, "NOMINEES", 13f, UiTheme.Muted, TextAlignmentOptions.Left);
            Cell(header, 800f, 300f, "EVICTED", 13f, UiTheme.Muted, TextAlignmentOptions.Left);

            for (int week = 1; week <= state.week; week++)
            {
                var comps = state.events
                    .Where(e => e.week == week && e.kind == "competition" && e.phase == EpisodePhase.HoH)
                    .OrderBy(e => e.sequence)
                    .ToList();
                string hoh = comps.Count > 0 ? WinnerName(comps[0].text) : "—";

                string nominees = string.Join(", ", state.contestants
                    .Where(c => c.nominationWeeks != null && c.nominationWeeks.Contains(week))
                    .Select(c => c.name));
                if (string.IsNullOrEmpty(nominees)) nominees = "—";

                var gone = state.events.FirstOrDefault(e => e.week == week && e.kind == "eviction");
                string evicted = gone != null ? FirstName(gone.text, state) : "—";

                var row = Panel(32f, week % 2 == 0 ? UiTheme.Surface : UiTheme.SurfaceRaised);
                Cell(row, 22f, 90f, week.ToString(), 15f, UiTheme.Paper, TextAlignmentOptions.Left);
                Cell(row, 120f, 260f, hoh, 15f, UiTheme.Accent, TextAlignmentOptions.Left);
                Cell(row, 400f, 380f, nominees, 15f, UiTheme.Paper, TextAlignmentOptions.Left);
                Cell(row, 800f, 300f, evicted, 15f, UiTheme.Danger, TextAlignmentOptions.Left);
            }
        }

        /// <summary>
        /// Every houseguest's season, sortable by each column and filterable by how far they got.
        ///
        /// <para>The controls are buttons that say what they do — "Sort by veto wins" — rather than
        /// column headings that happen to be clickable. A heading that is secretly a control is
        /// invisible to anyone not using a mouse, and this screen is the one most likely to be read
        /// rather than played.</para>
        ///
        /// <para>Sorting and filtering change nothing but this list. A finished season is a record,
        /// and looking at it from a different angle is not an edit.</para>
        /// </summary>
        private void Cast(EpisodeState state, Func<string, Texture> portrait)
        {
            Heading("The house");
            CastControls();

            var shownCast = Ordered(state, Filtered(state)).ToList();
            tableNames = shownCast.Select(c => c.name).ToList();
            if (shownCast.Count == 0)
            {
                Text("Nobody in this season finished there.", 15f, UiTheme.Muted, 30f,
                    TextAlignmentOptions.Center);
                return;
            }

            foreach (var who in shownCast)
            {
                var row = Panel(64f, UiTheme.Surface);

                var face = HudPrimitives.Portrait(row, portrait(who.id),
                    PlacementTint(who.status), 42f, 2f, who.status == ContestantStatus.Evicted);
                face.anchorMin = new Vector2(0f, 0.5f);
                face.anchorMax = new Vector2(0f, 0.5f);
                face.pivot = new Vector2(0.5f, 0.5f);
                face.anchoredPosition = new Vector2(46f, 0f);

                Cell(row, 84f, 300f, who.name + (who.isPlayer ? "  (You)" : string.Empty), 17f,
                    who.isPlayer ? UiTheme.Accent : UiTheme.Paper, TextAlignmentOptions.Left);
                Cell(row, 84f, 300f, StatusWord(who.status), 13f, PlacementTint(who.status),
                    TextAlignmentOptions.Left, -18f);

                Cell(row, 400f, 340f,
                    who.hohWins + " HoH · " + who.vetoWins + " veto · " + who.timesNominated + " noms",
                    15f, UiTheme.Muted, TextAlignmentOptions.Left);

                if (!who.isPlayer)
                {
                    double bond = state.Score(state.playerId, who.id);
                    Cell(row, 760f, 340f, "Ended with you at " + Math.Round(bond), 15f,
                        bond >= 25 ? UiTheme.Positive : bond <= -25 ? UiTheme.Danger : UiTheme.Muted,
                        TextAlignmentOptions.Left);
                }
            }
        }

        /// <summary>The sort and filter rows above the table.</summary>
        private void CastControls()
        {
            var sorts = (CastSort[])Enum.GetValues(typeof(CastSort));
            var sortBar = Panel(44f, new Color(0f, 0f, 0f, 0f));
            float span = (Width - Pad * 2f) / sorts.Length;
            float x = -(sorts.Length - 1) * span / 2f;
            foreach (var option in sorts)
            {
                var pick = option;
                Chip(sortBar, SortCaption(pick), x, span - 8f, sortBy == pick, () =>
                {
                    sortBy = pick;
                    Rebuild(shown, shownPortrait, shownReview);
                });
                x += span;
            }

            var filters = (CastFilter[])Enum.GetValues(typeof(CastFilter));
            var filterBar = Panel(44f, new Color(0f, 0f, 0f, 0f));
            span = (Width - Pad * 2f) / filters.Length;
            x = -(filters.Length - 1) * span / 2f;
            foreach (var option in filters)
            {
                var pick = option;
                Chip(filterBar, FilterCaption(pick), x, span - 8f, filter == pick, () =>
                {
                    filter = pick;
                    Rebuild(shown, shownPortrait, shownReview);
                });
                x += span;
            }
        }

        private IEnumerable<ContestantState> Filtered(EpisodeState state)
        {
            switch (filter)
            {
                case CastFilter.Finalists:
                    return state.contestants.Where(c => c.status == ContestantStatus.Winner
                                                        || c.status == ContestantStatus.RunnerUp);
                case CastFilter.Jury:
                    return state.contestants.Where(c => c.status == ContestantStatus.Jury);
                case CastFilter.Evicted:
                    return state.contestants.Where(c => c.status == ContestantStatus.Evicted);
                default:
                    return state.contestants;
            }
        }

        /// <summary>
        /// The chosen order, with a stable tie-break on name throughout — so two houseguests with
        /// the same two veto wins do not swap places every time the list is redrawn.
        /// </summary>
        private IEnumerable<ContestantState> Ordered(EpisodeState state, IEnumerable<ContestantState> cast)
        {
            switch (sortBy)
            {
                case CastSort.Name:
                    return cast.OrderBy(c => c.name, StringComparer.CurrentCulture);
                case CastSort.HohWins:
                    return cast.OrderByDescending(c => c.hohWins).ThenBy(c => c.name, StringComparer.CurrentCulture);
                case CastSort.VetoWins:
                    return cast.OrderByDescending(c => c.vetoWins).ThenBy(c => c.name, StringComparer.CurrentCulture);
                case CastSort.Nominations:
                    return cast.OrderByDescending(c => c.timesNominated).ThenBy(c => c.name, StringComparer.CurrentCulture);
                default:
                    // How far they got, which is the order the standings above already read in.
                    return cast
                        .OrderBy(c => PlacementRank(c.status))
                        .ThenByDescending(c => c.hohWins + c.vetoWins)
                        .ThenBy(c => c.name, StringComparer.CurrentCulture);
            }
        }

        private static int PlacementRank(ContestantStatus status)
        {
            switch (status)
            {
                case ContestantStatus.Winner: return 0;
                case ContestantStatus.RunnerUp: return 1;
                case ContestantStatus.Active: return 2;
                case ContestantStatus.Jury: return 3;
                default: return 4;
            }
        }

        /// <summary>A pill control, the same shape the cast screen and the creator use.</summary>
        private static Button Chip(Transform parent, string text, float x, float width, bool active, Action action)
        {
            var pill = HudPrimitives.Fill(text, parent, active ? UiTheme.AccentDeep : UiTheme.SurfaceRaised, 16);
            pill.anchorMin = new Vector2(0.5f, 0.5f);
            pill.anchorMax = new Vector2(0.5f, 0.5f);
            pill.pivot = new Vector2(0.5f, 0.5f);
            pill.sizeDelta = new Vector2(width, 34f);
            pill.anchoredPosition = new Vector2(x, 0f);
            UiTheme.AddBorder(pill, 16, active ? UiTheme.Accent : UiTheme.Outline);

            var image = pill.GetComponent<Image>();
            image.raycastTarget = true;

            var label = HudPrimitives.Label("Label", pill, 12f, active ? UiTheme.Paper : UiTheme.Muted,
                TextAlignmentOptions.Center);
            label.text = text;
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(5f, 0f);
            label.rectTransform.offsetMax = new Vector2(-5f, 0f);

            var button = pill.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => action());
            return button;
        }

        private void Buttons(Action onReview)
        {
            Space(6f);
            var bar = Panel(56f, new Color(0f, 0f, 0f, 0f));
            if (onReview != null) Button(bar, "Review the season", -150f, onReview);
            Button(bar, "Close", 150f, Hide);
            Space(Pad);
        }

        // ---------------------------------------------------------------- derivation

        /// <summary>Contestant id to the week they were evicted, read from the event log.</summary>
        private static Dictionary<string, int> EvictionOrder(EpisodeState state)
        {
            var order = new Dictionary<string, int>();
            foreach (var e in state.events.Where(e => e.kind == "eviction").OrderBy(e => e.sequence))
            {
                var who = state.contestants.FirstOrDefault(
                    c => !order.ContainsKey(c.id) && e.text.StartsWith(c.name, StringComparison.Ordinal));
                if (who != null) order[who.id] = e.sequence;
            }
            return order;
        }

        /// <summary>Pulls the name out of "Competition winner: NAME · category."</summary>
        // Both of these used to be parsed here. WeeklyRecap needs the same two answers out of the
        // same two sentences, and two parsers for one sentence is one too many — the second is
        // always the one that drifts when the log's wording changes. The em dash stays here,
        // because a table cell wants something to show and a recap wants to know there was nothing.

        private static string WinnerName(string description) =>
            WeeklyRecap.WinnerName(description) ?? "—";

        /// <summary>The cast member an eviction line opens with.</summary>
        private static string FirstName(string description, EpisodeState state) =>
            WeeklyRecap.Subject(state, description) ?? "—";

        private static string Placement(EpisodeState state, ContestantState you)
        {
            switch (you.status)
            {
                case ContestantStatus.Winner: return "1st — Winner";
                case ContestantStatus.RunnerUp: return "2nd — Runner-up";
                default:
                    int below = state.contestants.Count(c => c.status == ContestantStatus.Evicted)
                        - (you.status == ContestantStatus.Evicted ? 1 : 0);
                    int place = state.contestants.Count - below;
                    return place + Ordinal(place) + " — " + StatusWord(you.status);
            }
        }

        private static string Ordinal(int value)
        {
            if (value % 100 >= 11 && value % 100 <= 13) return "th";
            switch (value % 10)
            {
                case 1: return "st";
                case 2: return "nd";
                case 3: return "rd";
                default: return "th";
            }
        }

        private static string StatusWord(ContestantStatus status)
        {
            switch (status)
            {
                case ContestantStatus.Winner: return "Winner";
                case ContestantStatus.RunnerUp: return "Runner-up";
                case ContestantStatus.Jury: return "Jury member";
                case ContestantStatus.Evicted: return "Pre-jury";
                default: return "Still in the house";
            }
        }

        private static Color PlacementTint(ContestantStatus status)
        {
            switch (status)
            {
                case ContestantStatus.Winner: return UiTheme.Gold;
                case ContestantStatus.RunnerUp: return UiTheme.Accent;
                case ContestantStatus.Jury: return UiTheme.Warning;
                case ContestantStatus.Evicted: return UiTheme.Muted;
                default: return UiTheme.Positive;
            }
        }

        private static string ClosingNote(EpisodeState state, ContestantState you)
        {
            switch (you.status)
            {
                case ContestantStatus.Winner:
                    return "You took the house and the jury with it.";
                case ContestantStatus.RunnerUp:
                    return "You sat in the final two and the jury went the other way.";
                case ContestantStatus.Jury:
                    return "You were evicted with a vote still to cast, and you cast it.";
                default:
                    return you.hohWins + you.vetoWins > 0
                        ? "You went out before jury, but not before winning something."
                        : "You went out before jury. The house moved on without you.";
            }
        }

        // ---------------------------------------------------------------- layout

        private void Heading(string text)
        {
            Space(16f);
            var label = HudPrimitives.Label("Heading", content, 18f, UiTheme.Gold, TextAlignmentOptions.Left);
            label.text = text.ToUpperInvariant();
            Place(label.rectTransform, Width - Pad * 2f, 26f, -cursor);
            cursor += 30f;
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

        private static void Cell(Transform parent, float x, float width, string value, float size,
            Color colour, TextAlignmentOptions align, float y = 0f)
        {
            var label = HudPrimitives.Label("Cell", parent, size, colour, align);
            label.text = value;
            var rect = label.rectTransform;
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.sizeDelta = new Vector2(width, 22f);
            rect.anchoredPosition = new Vector2(x, y);
        }

        private static void Button(Transform parent, string text, float x, Action action)
        {
            var panel = HudPrimitives.Fill(text, parent, UiTheme.SurfaceRaised, 8);
            panel.anchorMin = new Vector2(0.5f, 0.5f);
            panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(260f, 44f);
            panel.anchoredPosition = new Vector2(x, 0f);
            panel.GetComponent<Image>().raycastTarget = true;
            UiTheme.AddBorder(panel, 8, UiTheme.Outline);

            var label = HudPrimitives.Label("Label", panel, 16f, UiTheme.Paper, TextAlignmentOptions.Center);
            label.text = text;
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

        private void Stat(Transform card, int index, string label, string value, Color tint)
        {
            const float step = 220f;
            float x = (index - 2) * step;

            var holder = new GameObject(label, typeof(RectTransform)).GetComponent<RectTransform>();
            holder.SetParent(card, false);
            holder.anchorMin = new Vector2(0.5f, 0.5f);
            holder.anchorMax = new Vector2(0.5f, 0.5f);
            holder.pivot = new Vector2(0.5f, 0.5f);
            holder.anchoredPosition = new Vector2(x, 20f);

            var number = HudPrimitives.Label("Value", holder, 26f, tint, TextAlignmentOptions.Center);
            number.text = value;
            number.rectTransform.sizeDelta = new Vector2(step - 10f, 34f);
            number.rectTransform.anchoredPosition = new Vector2(0f, 8f);

            var caption = HudPrimitives.Label("Caption", holder, 12f, UiTheme.Muted, TextAlignmentOptions.Center);
            caption.text = label;
            caption.rectTransform.sizeDelta = new Vector2(step - 10f, 18f);
            caption.rectTransform.anchoredPosition = new Vector2(0f, -16f);
        }
    }
}
