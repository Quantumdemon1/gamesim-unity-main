using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Gamesim.Persistence;
using Gamesim.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
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
    ///
    /// <para><b>A dashboard first</b> (the owner's Season Complete mockup, Pack 7): the winner and
    /// the runner-up, the five Game Sense cards, then the final standings, the jury's ballots and the
    /// season week by week side by side, and the career under them - the result at a glance on the
    /// first screen (<c>SeasonReport.Dashboard.cs</c>). The detail follows in the same scroll: the
    /// champion's road, the player's own season, Game Sense's moments and misses, and the house
    /// table with its sort and filter chips. Three tabs under the title jump the scroll to each part;
    /// they hide nothing, so every line of the season stays on the page to be read.</para>
    ///
    /// <para>Every way on is at the top, under the title, and stays there while the season scrolls
    /// beneath it - a new season, the main menu, the notebook, close - as the web game's game-over
    /// screen puts them straight under its winner. They were once only at the very bottom, below
    /// three screens of content that nothing on the report could scroll: no part of it caught the
    /// mouse, so the wheel went nowhere and clicks fell through to the HUD behind it, and a player
    /// who opened it had no way out but to quit. The whole report takes the mouse now, and a
    /// scrollbar shows how much there is.</para>
    ///
    /// <para>The mouse is not the only way through it. The keyboard's ring holds the ways on and the
    /// tabs at the top and the table's chips at the foot, and nothing between, so Page Up and Page
    /// Down, Home and End, and a pad's right stick scroll the season itself.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed partial class SeasonReport : MonoBehaviour
    {
        private const float Pad = 28f;

        /// <summary>
        /// The width the season is laid out at, inside the card: the room the screen gives it, from
        /// the canvas the report is drawn on, so the dashboard fills a wide screen and still fits a
        /// 4:3 one and the larger text.
        /// </summary>
        private float Width = 1180f;

        /// <summary>The ways on, at the top of the report. Tests and screen readers find them by these words.</summary>
        public const string NewSeasonCaption = "Start a new season";
        public const string MainMenuCaption = "Main menu";
        public const string ReviewCaption = "Review the season";
        public const string CloseCaption = "Close";
        /// <summary>The verdict's heading (STRATEGY-LOOP-PLAN.md section 5), and the names of its parts.</summary>
        public const string GameSenseHeading = "Game Sense", GameSenseCardName = "Game Sense card",
            MomentsHeading = "The moments that made the difference", MissedHeading = "The chances missed";

        /// <summary>The tabs under the title. Each scrolls the season to its part; none hides anything.</summary>
        public const string OverviewTabCaption = "Season at a glance", DetailTabCaption = "Your game in detail",
            HouseTabCaption = "The house table";

        /// <summary>The parts of the dashboard, by name, for a test and a screen reader.</summary>
        public const string HeroName = "Season hero", StandingsName = "Final standings", JuryColumnName = "Jury votes",
            TimelineName = "Season timeline", CareerStripName = "Career strip";

        /// <summary>The line at the head of the season saying how to move through it without a mouse.</summary>
        public const string ScrollHint = "Page Up and Page Down, or a pad's right stick, scroll the season";

        /// <summary>How far the right stick leans before it scrolls: past the drift of a stick at rest.</summary>
        private const float StickDeadZone = .2f;
        /// <summary>A full lean of the right stick, in reference units a second - about a screen a second.</summary>
        private const float StickSpeed = 1200f;
        /// <summary>What a page key keeps of the page it leaves, so the reader does not lose their line.</summary>
        private const float PageOverlap = 60f;

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
        private CareerSummary shownCareer;
        private Action shownNewSeason, shownMainMenu, shownClose;
        private ScrollRect scroller;

        // The selection the report last brought into view, so it brings each one into view once;
        // and the corners it measures with, kept rather than made again on every reveal.
        private GameObject revealed;
        private readonly Vector3[] selectedCorners = new Vector3[4], viewCorners = new Vector3[4];

        // Where each tab's part of the season starts in the scroll, and the tabs' art to light the
        // one being read.
        private readonly float[] sectionTops = new float[3];
        private readonly List<(Image art, TMP_Text label)> tabs = new List<(Image, TMP_Text)>();
        private int litTab = -1;

        /// <summary>How far the season can scroll: its height past the window it is read through.</summary>
        private float Travel => content != null && viewport != null
            ? Mathf.Max(0f, content.rect.height - viewport.rect.height) : 0f;

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
        public void Show(EpisodeState state, Func<string, Texture> portrait, Action onReview,
            CareerSummary career = null) => Show(state, portrait, onReview, career, null, null, null);

        /// <summary>
        /// The same, with the ways on the report offers at its top: <paramref name="onNewSeason"/>,
        /// <paramref name="onMainMenu"/> and <paramref name="onReview"/> each close the report and
        /// go; <paramref name="onClose"/> runs after it closes, so the house behind can redraw. A
        /// null action is simply not offered.
        /// </summary>
        public void Show(EpisodeState state, Func<string, Texture> portrait, Action onReview,
            CareerSummary career, Action onNewSeason, Action onMainMenu, Action onClose)
        {
            if (state == null) return;
            shown = state;
            shownPortrait = portrait ?? (_ => null);
            shownReview = onReview;
            shownCareer = career;
            shownNewSeason = onNewSeason;
            shownMainMenu = onMainMenu;
            shownClose = onClose;
            sortBy = CastSort.Placement;
            filter = CastFilter.Everyone;
            revealed = null;
            Rebuild(state, shownPortrait, onReview);
            group.alpha = 1f;
            group.blocksRaycasts = true;
            group.interactable = true;
        }

        /// <summary>The canvas the report is drawn on, in reference units; the reference itself before the first layout.</summary>
        private Vector2 Room()
        {
            var size = ((RectTransform)transform).rect.size;
            if (size.x >= 200f && size.y >= 200f) return size;
            return scaler != null ? scaler.referenceResolution : new Vector2(1920f, 1080f);
        }

        private void Rebuild(EpisodeState state, Func<string, Texture> portrait, Action onReview)
        {
            foreach (Transform child in transform) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            tabs.Clear();
            litTab = -1;

            var room = Room();
            float sheetWidth = Mathf.Clamp(room.x - 64f, 860f, 1760f);
            Width = sheetWidth - 40f;

            // The house dimmed behind the season rather than blacked out, and the season on a
            // glass card, as the mockups set every summary over the room it is about. Both take
            // the mouse: a modal that catches nothing lets every click through to the HUD behind.
            var scrim = HudPrimitives.Fill("Scrim", transform, new Color(UiTheme.Ink.r, UiTheme.Ink.g, UiTheme.Ink.b, 0.88f), 1);
            scrim.GetComponent<Image>().raycastTarget = true;
            Stretch(scrim);
            HudPrimitives.Vignette(scrim);

            var sheet = HudPrimitives.Fill("Report card", scrim, new Color(UiTheme.GlassFill.r, UiTheme.GlassFill.g, UiTheme.GlassFill.b, .94f), UiTheme.GlassRadius);
            sheet.anchorMin = new Vector2(0.5f, 0f);
            sheet.anchorMax = new Vector2(0.5f, 1f);
            sheet.pivot = new Vector2(0.5f, 0.5f);
            sheet.sizeDelta = new Vector2(sheetWidth, -64f);
            sheet.anchoredPosition = Vector2.zero;
            UiTheme.Glass(sheet, UiTheme.GlassRadius);
            sheet.GetComponent<Image>().raycastTarget = true;

            // The title, the ways on and the tabs, fixed at the top of the card.
            content = new GameObject("Fixed header", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(sheet, false);
            content.anchorMin = content.anchorMax = new Vector2(.5f, 1f);
            content.pivot = new Vector2(.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            cursor = 0f;
            Header(state, onReview);
            float band = cursor + 6f;
            content.sizeDelta = new Vector2(Width, band);
            var divider = new GameObject("Header rule", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            divider.rectTransform.SetParent(sheet, false);
            divider.rectTransform.anchorMin = divider.rectTransform.anchorMax = new Vector2(.5f, 1f);
            divider.rectTransform.pivot = new Vector2(.5f, 1f);
            divider.rectTransform.sizeDelta = new Vector2(Width - Pad * 2f, 1f);
            divider.rectTransform.anchoredPosition = new Vector2(0f, -band);
            divider.color = new Color(UiTheme.Hairline.r, UiTheme.Hairline.g, UiTheme.Hairline.b, .3f);
            divider.raycastTarget = false;

            viewport = HudPrimitives.Fill("Viewport", sheet, new Color(0f, 0f, 0f, 0f), 1);
            viewport.anchorMin = new Vector2(0.5f, 0f);
            viewport.anchorMax = new Vector2(0.5f, 1f);
            viewport.pivot = new Vector2(0.5f, 1f);
            viewport.sizeDelta = new Vector2(Width, -(band + 10f));
            viewport.anchoredPosition = new Vector2(0f, -(band + 4f));
            viewport.gameObject.AddComponent<RectMask2D>();
            // The wheel and a drag reach the scroll through whatever they land on; the viewport
            // itself has to be something to land on, or the gaps between rows scroll nothing.
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
            scroll.verticalScrollbar = Scrollbar(sheet, band);
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            scroller = scroll;

            cursor = 0f;
            Space(14f);
            sectionTops[0] = 0f;
            Dashboard(state, portrait);

            // The detail, under the dashboard in the same scroll.
            sectionTops[1] = cursor + 8f;
            WinnersJourney(state);
            YourJourney(state);
            GameSenseSection(state);
            sectionTops[2] = cursor + 8f;
            Cast(state, portrait);

            content.sizeDelta = new Vector2(0f, cursor + Pad);
            LightTab(0);
        }

        /// <summary>
        /// A thin bar down the card's right edge, showing how much season there is below and where
        /// the reader is in it; draggable, and out of the keyboard's way.
        /// </summary>
        private static Scrollbar Scrollbar(RectTransform sheet, float band)
        {
            var track = HudPrimitives.Fill("Scrollbar", sheet, new Color(UiTheme.Hairline.r, UiTheme.Hairline.g, UiTheme.Hairline.b, .14f), 4);
            track.anchorMin = new Vector2(1f, 0f); track.anchorMax = new Vector2(1f, 1f);
            track.pivot = new Vector2(1f, 1f);
            track.sizeDelta = new Vector2(8f, -(band + 24f));
            track.anchoredPosition = new Vector2(-8f, -(band + 10f));
            track.GetComponent<Image>().raycastTarget = true;
            var area = new GameObject("Sliding area", typeof(RectTransform)).GetComponent<RectTransform>();
            area.SetParent(track, false);
            area.anchorMin = Vector2.zero; area.anchorMax = Vector2.one;
            area.offsetMin = Vector2.zero; area.offsetMax = Vector2.zero;
            var handle = HudPrimitives.Fill("Handle", area, new Color(UiTheme.Accent.r, UiTheme.Accent.g, UiTheme.Accent.b, .7f), 4);
            handle.offsetMin = Vector2.zero; handle.offsetMax = Vector2.zero;
            var handleImage = handle.GetComponent<Image>();
            handleImage.raycastTarget = true;
            var bar = track.gameObject.AddComponent<Scrollbar>();
            bar.handleRect = handle;
            bar.targetGraphic = handleImage;
            bar.direction = UnityEngine.UI.Scrollbar.Direction.BottomToTop;
            bar.navigation = new Navigation { mode = Navigation.Mode.None };
            return bar;
        }

        /// <summary>Closes the report, and lets the house behind it redraw.</summary>
        public void Close()
        {
            if (!IsShowing) return;
            Hide();
            shownClose?.Invoke();
        }

        /// <summary>
        /// The right stick's lean, up positive, once it is past the dead zone; nothing otherwise.
        /// </summary>
        internal static float StickLean()
        {
            var pad = Gamepad.current;
            if (pad == null) return 0f;
            float lean = pad.rightStick.ReadValue().y;
            return Mathf.Abs(lean) > StickDeadZone ? lean : 0f;
        }

        /// <summary>
        /// Whether the right stick is scrolling the report with nothing that walks the ring held
        /// alongside it. The report's controls take no step from a stick that is scrolling: see
        /// <see cref="SeasonReportControl"/>.
        /// </summary>
        internal static bool StickIsScrolling()
        {
            var pad = Gamepad.current;
            return pad != null && StickLean() != 0f && !pad.dpad.IsActuated() && pad.leftStick.ReadValue().magnitude < .5f;
        }

        /// <summary>
        /// Page Up and Page Down, Home and End, and the right stick, scroll the season.
        ///
        /// <para>The keyboard's ring has the ways on and the tabs at the top and the table's chips
        /// at the foot, and nothing in between: the winner's road, the jury's ballots, the standings
        /// and the weeks sat between the first screen and the last, where a player without a mouse
        /// could not get. None of these keys is anything else's while the report is up, and the
        /// stick only scrolls - it takes no step round the ring while it does.</para>
        /// </summary>
        private void Update()
        {
            if (!IsShowing || scroller == null || content == null || viewport == null) return;
            float travel = Travel;
            if (travel <= 0f) return;
            float from = content.anchoredPosition.y, to = from;
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                float page = Mathf.Max(viewport.rect.height - PageOverlap, viewport.rect.height * .5f);
                if (keyboard.pageDownKey.wasPressedThisFrame) to += page;
                if (keyboard.pageUpKey.wasPressedThisFrame) to -= page;
                if (keyboard.homeKey.wasPressedThisFrame) to = 0f;
                if (keyboard.endKey.wasPressedThisFrame) to = travel;
            }
            // Leaning up reads back up the season, as a wheel turned away from you does. A hitch in
            // the frame rate is not a leap down the page.
            to -= StickLean() * StickSpeed * Mathf.Min(Time.unscaledDeltaTime, .1f);
            to = Mathf.Clamp(to, 0f, travel);
            if (Mathf.Abs(to - from) < .01f) return;
            scroller.StopMovement();
            content.anchoredPosition = new Vector2(content.anchoredPosition.x, to);
        }

        /// <summary>
        /// Brings the keyboard's selection into view when it moves: Tab onto the sort chips at the
        /// foot of the house table and the report scrolls down to them. And lights the tab of the
        /// part of the season being read.
        ///
        /// <para>Once per selection, not every frame. It used to run every frame for as long as a
        /// chip was selected, and a chip stays selected after a click or a press-and-drag, so the
        /// wheel, a drag or the page keys could not take the reader away from it: each frame the
        /// report snapped back to the chip, until something else was clicked.</para>
        /// </summary>
        private void LateUpdate()
        {
            if (!IsShowing || scroller == null || content == null || viewport == null) return;
            LightTab(ReadingTab());
            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (selected == revealed) return;
            revealed = selected;
            if (selected == null || !selected.activeInHierarchy || !selected.transform.IsChildOf(content)) return;
            Reveal((RectTransform)selected.transform);
        }

        private void Reveal(RectTransform rect)
        {
            rect.GetWorldCorners(selectedCorners);
            viewport.GetWorldCorners(viewCorners);
            float below = viewCorners[0].y - selectedCorners[0].y, above = selectedCorners[1].y - viewCorners[1].y;
            if (below <= 0f && above <= 0f) return;
            float scale = viewport.lossyScale.y > 0f ? viewport.lossyScale.y : 1f;
            var position = content.anchoredPosition;
            position.y += (below > 0f ? below + 12f * scale : -(above + 12f * scale)) / scale;
            position.y = Mathf.Clamp(position.y, 0f, Travel);
            scroller.StopMovement();
            content.anchoredPosition = position;
        }

        // ---------------------------------------------------------------- the header and the tabs

        /// <summary>
        /// The title, what the season was in a line, and the ways on: beside the title on a wide
        /// card, under it on a narrow one. Then the tabs, and the line saying how to scroll.
        /// </summary>
        private void Header(EpisodeState state, Action onReview)
        {
            var box = EndScreenKit.Box("Header", content, 0f, 0f, Width, 10f);
            var you = state.Find(state.playerId);
            bool spectator = you != null && you.status != ContestantStatus.Winner && you.status != ContestantStatus.RunnerUp;

            var actions = new List<(string caption, Action act, bool primary, string icon, string fallback)>();
            if (shownNewSeason != null) actions.Add((NewSeasonCaption, () => { Hide(); shownNewSeason(); }, true, PackArt.KitIconRefresh, "star"));
            if (onReview != null) actions.Add((ReviewCaption, () => { Hide(); onReview(); }, shownNewSeason == null, PackArt.KitIconBook, "journal"));
            if (shownMainMenu != null) actions.Add((MainMenuCaption, () => { Hide(); shownMainMenu(); }, false, PackArt.KitIconHome, "house"));
            actions.Add((CloseCaption, Close, actions.Count == 0, PackArt.KitIconCross, "exit"));
            var widths = actions.Select(a => ActionWidth(a.caption, a.primary)).ToList();
            const float gap = 12f, buttonHeight = 50f;
            float buttonsWidth = widths.Sum() + gap * (actions.Count - 1);

            // The title block: trophy, title, the season in a line, and the spectator's line.
            float titleWidth = 560f;
            bool beside = Width - Pad * 2f >= titleWidth + buttonsWidth + 24f;
            float top = 18f;
            float left = Pad;
            if (!beside) left = Mathf.Max(Pad, (Width - titleWidth) * .5f);
            var trophy = EndScreenKit.Picture("Trophy", box, PackArt.SeasonTrophy, "trophy", UiTheme.Gold, new Vector2(left + 28f, -(top + 30f)), 50f);
            float textX = trophy != null ? left + 70f : left;
            var title = EndScreenKit.Text("Title", box, "SEASON COMPLETE", 38f, Color.white, textX, top, titleWidth, 48f,
                TextAlignmentOptions.Left, UiTheme.Weight.Bold);
            title.characterSpacing = 3f;
            title.enableVertexGradient = true;
            title.colorGradient = new VertexGradient(UiTheme.Glow, UiTheme.Glow, UiTheme.Heading, UiTheme.Heading);
            EndScreenKit.Text("Summary", box, state.week + (state.week == 1 ? " week" : " weeks") + " · "
                + state.contestants.Count + " houseguests · "
                + state.contestants.Count(c => c.status == ContestantStatus.Jury) + " on the jury",
                17f, UiTheme.Muted, textX, top + 48f, titleWidth, 24f);
            float titleBottom = top + 76f;
            // The web build shows "You watched this season as a spectator" on this screen when the
            // player was evicted. Same statement, drawn from status rather than a stored flag.
            if (spectator)
            {
                EndScreenKit.Text("Spectator", box, "YOU WATCHED THE REST OF THIS SEASON AS A SPECTATOR", 13f, UiTheme.Warning,
                    textX, titleBottom, titleWidth, 20f);
                titleBottom += 22f;
            }

            // The ways on: a new season first, as the web game's primary action is, then the
            // notebook, the main menu, and close. Only the ones given are drawn.
            float buttonsTop = beside ? top + 4f : titleBottom + 10f;
            float x = beside ? Width - Pad - buttonsWidth : (Width - buttonsWidth) * .5f;
            for (int i = 0; i < actions.Count; i++)
            {
                ActionButton(box, actions[i].caption, x, buttonsTop, widths[i], buttonHeight, actions[i].act, actions[i].primary,
                    actions[i].icon, actions[i].fallback);
                x += widths[i] + gap;
            }
            float bottom = Mathf.Max(titleBottom, buttonsTop + buttonHeight) + 12f;

            // The tabs, and the line saying how to move through the season without a mouse - said
            // once, at the head of the season: the scrollbar tells a mouse there is more, and
            // nothing else would tell a keyboard or a pad how to reach it.
            var tabCaptions = new[] { OverviewTabCaption, DetailTabCaption, HouseTabCaption };
            float tabX = Pad;
            for (int i = 0; i < tabCaptions.Length; i++)
            {
                int index = i;
                float w = TabWidth(tabCaptions[i]);
                Tab(box, tabCaptions[i], tabX, bottom, w, 36f, () => ScrollTo(index));
                tabX += w + 10f;
            }
            float hintX = tabX + 16f;
            bool hintBeside = Width - Pad - hintX >= 420f;
            var hint = EndScreenKit.Text("Scroll hint", box, ScrollHint, 13f, UiTheme.Muted,
                hintBeside ? hintX : Pad, hintBeside ? bottom + 9f : bottom + 44f, hintBeside ? Width - Pad - hintX : Width - Pad * 2f, 20f,
                hintBeside ? TextAlignmentOptions.Right : TextAlignmentOptions.Left);
            hint.enableAutoSizing = true; hint.fontSizeMax = 13f; hint.fontSizeMin = 10f;
            cursor = bottom + (hintBeside ? 44f : 68f);
            box.sizeDelta = new Vector2(Width, cursor);
        }

        private static float ActionWidth(string caption, bool primary)
        {
            float words = caption.Length * 9.2f + 64f;
            return Mathf.Clamp(words, primary ? 210f : 150f, 260f);
        }

        private static float TabWidth(string caption) => Mathf.Clamp(caption.Length * 8.2f + 36f, 140f, 220f);

        /// <summary>
        /// One of the ways on: the panel named by its caption and the caption on it word for word,
        /// the pack's button behind it and a glyph to its left. The panel is the control, as it
        /// always was; the art and the glyph are decoration.
        /// </summary>
        private static void ActionButton(RectTransform parent, string caption, float x, float y, float width, float height,
            Action action, bool primary, string icon, string fallbackIcon)
        {
            var panel = HudPrimitives.Fill(caption, parent, primary ? UiTheme.ActionBlue : UiTheme.SurfaceRaised, 10);
            EndScreenKit.Place(panel, x, y, width, height);
            var ground = panel.GetComponent<Image>();
            ground.raycastTarget = true;
            var art = EndScreenKit.Frame(panel, primary ? PackArt.SeasonButtonPrimary : PackArt.SeasonButton, 14f,
                primary ? UiTheme.ActionBlue : UiTheme.SurfaceRaised, primary ? UiTheme.Glow : UiTheme.Outline, 10);
            bool packed = EndScreenKit.Packed(art);
            Graphic target = ground;
            if (packed)
            {
                // The pack's button is the face; the panel keeps catching the mouse, clear.
                ground.color = new Color(0f, 0f, 0f, 0f);
                target = art;
            }
            else if (primary) UiTheme.AddGlow(panel, 10);

            float glyphSide = 20f;
            var glyph = EndScreenKit.Picture("Glyph", panel, icon, fallbackIcon, primary ? Color.white : UiTheme.Paper,
                new Vector2(22f, -height * .5f), glyphSide);
            if (glyph != null && glyph.sprite == UiTheme.Pack(icon)) glyph.color = primary ? Color.white : UiTheme.Paper;

            var label = HudPrimitives.Label("Label", panel, 17f, primary ? Color.white : UiTheme.Paper, TextAlignmentOptions.Center);
            var weight = UiTheme.Font(primary ? UiTheme.Weight.SemiBold : UiTheme.Weight.Medium);
            if (weight != null) label.font = weight;
            label.text = Localisation.Text(caption);
            label.enableAutoSizing = true; label.fontSizeMax = 17f; label.fontSizeMin = 12f;
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(glyph != null ? 36f : 10f, 0f);
            label.rectTransform.offsetMax = new Vector2(-10f, 0f);

            var button = panel.gameObject.AddComponent<SeasonReportControl>();
            button.targetGraphic = target;
            var colours = button.colors;
            colours.highlightedColor = new Color(1.18f, 1.18f, 1.18f);
            colours.selectedColor = colours.highlightedColor;
            button.colors = colours;
            button.onClick.AddListener(() => action());
        }

        /// <summary>A tab: a control named by its caption that scrolls the season to its part.</summary>
        private void Tab(RectTransform parent, string caption, float x, float y, float width, float height, Action press)
        {
            var panel = HudPrimitives.Fill(caption, parent, UiTheme.SurfaceRaised, 10);
            EndScreenKit.Place(panel, x, y, width, height);
            var ground = panel.GetComponent<Image>();
            ground.raycastTarget = true;
            var art = EndScreenKit.Frame(panel, PackArt.SeasonTabInactive, 12f, UiTheme.SurfaceRaised, UiTheme.Outline, 10);
            bool packed = EndScreenKit.Packed(art);
            if (packed) ground.color = new Color(0f, 0f, 0f, 0f);
            var label = HudPrimitives.Label("Label", panel, 14f, UiTheme.Muted, TextAlignmentOptions.Center);
            label.text = Localisation.Text(caption);
            label.enableAutoSizing = true; label.fontSizeMax = 14f; label.fontSizeMin = 10f;
            label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(8f, 0f); label.rectTransform.offsetMax = new Vector2(-8f, 0f);
            var button = panel.gameObject.AddComponent<SeasonReportControl>();
            button.targetGraphic = packed ? (Graphic)art : ground;
            var colours = button.colors;
            colours.highlightedColor = new Color(1.18f, 1.18f, 1.18f);
            colours.selectedColor = colours.highlightedColor;
            button.colors = colours;
            button.onClick.AddListener(() => press());
            tabs.Add((packed ? art : ground, label));
        }

        /// <summary>Scrolls the season to a tab's part. Nothing is hidden and nothing is committed.</summary>
        private void ScrollTo(int index)
        {
            if (scroller == null || content == null || index < 0 || index >= sectionTops.Length) return;
            Canvas.ForceUpdateCanvases();
            scroller.StopMovement();
            content.anchoredPosition = new Vector2(content.anchoredPosition.x, Mathf.Clamp(sectionTops[index], 0f, Travel));
            LightTab(index);
        }

        /// <summary>The part of the season at the top of the window: the last whose start is above its middle.</summary>
        private int ReadingTab()
        {
            if (content == null || viewport == null) return 0;
            float reading = content.anchoredPosition.y + viewport.rect.height * .35f;
            int at = 0;
            for (int i = 0; i < sectionTops.Length; i++) if (sectionTops[i] <= reading) at = i;
            if (Travel > 0f && content.anchoredPosition.y >= Travel - 1f) at = sectionTops.Length - 1;
            return at;
        }

        private void LightTab(int index)
        {
            if (index == litTab) return;
            litTab = index;
            for (int i = 0; i < tabs.Count; i++)
            {
                bool lit = i == index;
                var (art, label) = tabs[i];
                if (art == null || label == null) continue;
                if (EndScreenKit.Packed(art) && UiTheme.Pack(PackArt.SeasonFilterActive) != null)
                {
                    var sprite = UiTheme.Pack(lit ? PackArt.SeasonFilterActive : PackArt.SeasonTabInactive);
                    if (sprite != null) art.sprite = sprite;
                }
                else art.color = lit ? UiTheme.ActionBlue : UiTheme.SurfaceRaised;
                label.color = lit ? Color.white : UiTheme.Muted;
            }
        }

        // ---------------------------------------------------------------- the detail, below the dashboard

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
            // Every competition won, the final Head of Household's first two parts included, as the
            // finalists' records count them (FinalistRead.Wins); HOH WINS and VETO WINS stay the two.
            Stat(card, 3, "COMP WINS", FinalistRead.Wins(state, champion).ToString(), UiTheme.Positive);
            Stat(card, 4, "WITH YOU",
                Math.Round(state.Score(state.playerId, champion.id)).ToString(CultureInfo.InvariantCulture),
                UiTheme.Muted);

            var note = HudPrimitives.Label("Note", card, 15f, UiTheme.Muted, TextAlignmentOptions.Center);
            note.text = WinnerNote(state, champion);
            Place(note.rectTransform, Width - Pad * 4f, 24f, -100f);
        }

        /// <summary>
        /// One line on how the champion played it, from the shape of their own record rather than
        /// from a phrase picked at random — a winner who never won anything and a winner who won
        /// everything did not have the same season and should not be described the same way. The
        /// count is the card's own COMP WINS, so the line and the number beside it agree.
        /// </summary>
        private static string WinnerNote(EpisodeState state, ContestantState champion)
        {
            int comps = FinalistRead.Wins(state, champion);
            if (comps == 0 && champion.timesNominated == 0)
                return "Never on the block, never in charge. Nobody ever thought to move on them.";
            if (comps == 0)
                return "Won nothing and survived anyway, which is the harder way to do it.";
            if (champion.timesNominated == 0)
                return "Won " + comps + " and was never once put up for it.";
            return "Won " + comps + " and survived the block " + champion.timesNominated
                   + (champion.timesNominated == 1 ? " time." : " times.");
        }

        /// <summary>One line for the finished panel: the number and its three faces.</summary>
        public static string GameSenseLine(EpisodeState state)
        {
            var report = GameSense.Evaluate(state);
            return "Game Sense " + report.score + ": competitions " + report.competitions + ", strategy " + report.strategy + ", social " + report.social + ".";
        }

        /// <summary>
        /// GAME SENSE (STRATEGY-LOOP-PLAN.md section 5), in detail: what the number is, then the three
        /// moments that made the difference and the three chances missed, each a ledger row in words.
        /// The number and its faces are the dashboard's stat cards. Not all wins are equal: the
        /// verdict says how this one was played.
        /// </summary>
        private void GameSenseSection(EpisodeState state)
        {
            var report = GameSense.Evaluate(state);
            Heading(GameSenseHeading);
            Text("Skill, not luck: every point is a row in the notebook. Competitions against the odds you had; strategy weighed against what was offered; social as the house sees you.",
                15f, UiTheme.Muted, 24f, TextAlignmentOptions.Left);
            if (report.moments.Count > 0)
            {
                Space(10f);
                Text(MomentsHeading, 14f, UiTheme.Heading, 22f, TextAlignmentOptions.Left);
                foreach (var moment in report.moments)
                    Text("+" + moment.points.ToString("0") + "  " + moment.text, 15f, UiTheme.Paper, 24f, TextAlignmentOptions.Left);
            }
            if (report.missed.Count > 0)
            {
                Space(10f);
                Text(MissedHeading, 14f, UiTheme.Heading, 22f, TextAlignmentOptions.Left);
                foreach (var missed in report.missed)
                    Text(missed.points.ToString("0") + "  " + missed.text, 15f, UiTheme.Muted, 24f, TextAlignmentOptions.Left);
            }
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
            Stat(card, 4, "COMP WINS", FinalistRead.Wins(state, you).ToString(), UiTheme.Positive);

            var note = HudPrimitives.Label("Note", card, 15f, UiTheme.Muted, TextAlignmentOptions.Center);
            note.text = ClosingNote(state, you);
            Place(note.rectTransform, Width - Pad * 4f, 24f, -100f);
        }

        /// <summary>One juror's ballot as the screens say it: who, for whom, and the recorded why.</summary>
        public readonly struct JuryBallot
        {
            public readonly string Juror;
            public readonly string Finalist;
            /// <summary>The engine's recorded reason; null for the player's own ballot, which needs none.</summary>
            public readonly string Reason;
            public readonly bool IsPlayer;
            /// <summary>Who cast it and for whom, by id: what the screens draw a face from.</summary>
            public readonly string JurorId, FinalistId;

            public JuryBallot(string juror, string finalist, string reason, bool isPlayer, string jurorId = null, string finalistId = null)
            {
                Juror = juror; Finalist = finalist; Reason = reason; IsPlayer = isPlayer;
                JurorId = jurorId; FinalistId = finalistId;
            }

            /// <summary>The one-line form the finale panel shows.</summary>
            public string Line => (IsPlayer ? "You" : Juror) + " voted for " + Finalist + "."
                                  + (string.IsNullOrEmpty(Reason) ? string.Empty : " " + Reason);
        }

        /// <summary>
        /// The jury's ballots, in the order they were cast, read from the recorded votes.
        ///
        /// <para>Every jury ballot carries the term that decided it — the engine writes one when it
        /// resolves the jury — so nothing here is inferred. A ballot counts when its voter is on the
        /// jury and its target is a finalist; the eviction ballots a mid-season state still holds
        /// fail the second test and are never mistaken for these.</para>
        /// </summary>
        public static List<JuryBallot> JuryBallots(EpisodeState state)
        {
            var ballots = new List<JuryBallot>();
            if (state?.votes == null) return ballots;
            foreach (var vote in state.votes)
            {
                var juror = state.Find(vote.voterId);
                var finalist = state.Find(vote.targetId);
                if (juror == null || finalist == null) continue;
                if (juror.status != ContestantStatus.Jury && juror.status != ContestantStatus.Evicted) continue;
                if (finalist.status != ContestantStatus.Winner && finalist.status != ContestantStatus.RunnerUp) continue;
                ballots.Add(new JuryBallot(juror.name, finalist.name, juror.isPlayer ? null : vote.reason, juror.isPlayer, juror.id, finalist.id));
            }
            return ballots;
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
                    PlacementTint(who.status), 42f, 2f, who.status == ContestantStatus.Evicted, who);
                face.anchorMin = new Vector2(0f, 0.5f);
                face.anchorMax = new Vector2(0f, 0.5f);
                face.pivot = new Vector2(0.5f, 0.5f);
                face.anchoredPosition = new Vector2(46f, 0f);

                Cell(row, 84f, 300f, HudPrimitives.WithYou(who.name, who.isPlayer, "  "), 17f,
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
                    Redraw();
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
                    Redraw();
                });
                x += span;
            }
        }

        /// <summary>
        /// Draws the report again for a new sort or filter, with the reader still where they were.
        ///
        /// <para>A redraw is a new report, whose scroll starts at the top. Sorting the table three
        /// screens down used to put the reader back at the winner, with the table three screens
        /// under them, and the chip they had pressed thrown away: the keyboard fell to the first
        /// control on the screen, "Start a new season", so a second Enter to sort again opened the
        /// cast screen instead. The sections above the table do not change with it, so the same
        /// reading position shows the same part of the season, and the keyboard goes back to the
        /// control with the words it was on.</para>
        /// </summary>
        private void Redraw()
        {
            float reading = content != null ? content.anchoredPosition.y : 0f;
            var events = EventSystem.current;
            var selected = events != null ? events.currentSelectedGameObject : null;
            string focus = selected != null && selected.transform.IsChildOf(transform) ? selected.name : null;

            Rebuild(shown, shownPortrait, shownReview);
            Canvas.ForceUpdateCanvases();
            content.anchoredPosition = new Vector2(content.anchoredPosition.x, Mathf.Clamp(reading, 0f, Travel));
            scroller.StopMovement();

            if (events == null || focus == null) return;
            // The report thrown away is inactive already and only destroyed at the frame's end, so
            // an active control of that name is the new one.
            var again = GetComponentsInChildren<Selectable>().FirstOrDefault(control => control.name == focus);
            if (again != null) events.SetSelectedGameObject(again.gameObject);
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
                    // Out before the jury either way: evicted, or removed by production.
                    return state.contestants.Where(c => c.status == ContestantStatus.Evicted || c.status == ContestantStatus.Expelled);
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
                    // How far they got: the order the standings read in, one placement for every screen.
                    return cast
                        .OrderBy(c => CareerLedger.Placement(state, c))
                        .ThenBy(c => PlacementRank(c.status))
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

        /// <summary>A pill control, the same shape the cast screen and the creator use, in the pack's filter art.</summary>
        private static Button Chip(Transform parent, string text, float x, float width, bool active, Action action)
        {
            var pill = HudPrimitives.Fill(text, parent, active ? UiTheme.ActionBlue : UiTheme.SurfaceRaised, 16);
            pill.anchorMin = new Vector2(0.5f, 0.5f);
            pill.anchorMax = new Vector2(0.5f, 0.5f);
            pill.pivot = new Vector2(0.5f, 0.5f);
            pill.sizeDelta = new Vector2(width, 34f);
            pill.anchoredPosition = new Vector2(x, 0f);

            var image = pill.GetComponent<Image>();
            image.raycastTarget = true;
            Graphic target = image;
            var art = EndScreenKit.Frame(pill, active ? PackArt.SeasonFilterActive : PackArt.SeasonFilterInactive, 11f,
                active ? UiTheme.ActionBlue : UiTheme.SurfaceRaised, active ? UiTheme.Accent : UiTheme.Outline, 16);
            if (EndScreenKit.Packed(art))
            {
                image.color = new Color(0f, 0f, 0f, 0f);
                target = art;
            }

            var label = HudPrimitives.Label("Label", pill, 12f, active ? Color.white : UiTheme.Muted,
                TextAlignmentOptions.Center);
            label.text = Localisation.Text(text);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(5f, 0f);
            label.rectTransform.offsetMax = new Vector2(-5f, 0f);

            var button = pill.gameObject.AddComponent<SeasonReportControl>();
            button.targetGraphic = target;
            button.onClick.AddListener(() => action());
            return button;
        }

        // ---------------------------------------------------------------- derivation

        private static string Placement(EpisodeState state, ContestantState you)
        {
            switch (you.status)
            {
                case ContestantStatus.Winner: return "1st — Winner";
                case ContestantStatus.RunnerUp: return "2nd — Runner-up";
                case ContestantStatus.Active: return "Still in the house";
                default:
                    // The one placement every screen reads: the order the jury ledger filled, with
                    // production's removals merged in by the week each left.
                    int place = CareerLedger.Placement(state, you);
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
                case ContestantStatus.Expelled: return "Removed by production";
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
                case ContestantStatus.Expelled: return UiTheme.Conflict;
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
                case ContestantStatus.Expelled:
                    return "Production removed you from the house. The season went on without your vote.";
                case ContestantStatus.Active:
                    return "The season is still being played.";
                default:
                    return you.hohWins + you.vetoWins > 0
                        ? "You went out before jury, but not before winning something."
                        : "You went out before jury. The house moved on without you.";
            }
        }

        // ---------------------------------------------------------------- layout

        /// <summary>
        /// A section's name, small and letterspaced in the heading blue over a soft rule. It was
        /// gold, and gold is power in this house - the win, the crown - not every heading on the page.
        /// </summary>
        private void Heading(string text)
        {
            Space(20f);
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
            var panel = HudPrimitives.Fill("Row", content, colour, 10);
            Place(panel, Width - Pad * 2f, height, -cursor);
            if (colour.a > 0f) UiTheme.AddBorder(panel, 10, new Color(UiTheme.Hairline.r, UiTheme.Hairline.g, UiTheme.Hairline.b, .3f));
            cursor += height + 6f;
            return panel;
        }

        private TMP_Text Text(string value, float size, Color colour, float height, TextAlignmentOptions align)
        {
            var label = HudPrimitives.Label("Text", content, size, colour, align);
            label.text = Localisation.Text(value);
            Place(label.rectTransform, Width - Pad * 2f, height, -cursor);
            if (height > 0f && label.GetPreferredValues(label.text, Width - Pad * 2f, 0f).y > height + 1f)
                height = EndScreenKit.Wrapped(label, Width - Pad * 2f);
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
            Color colour, TextAlignmentOptions align, float y = 0f)
        {
            var label = HudPrimitives.Label("Cell", parent, size, colour, align);
            label.text = Localisation.Text(value);
            var rect = label.rectTransform;
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.sizeDelta = new Vector2(width, 22f);
            rect.anchoredPosition = new Vector2(x, y);
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
            // A placement is words, not a figure: "6th - Jury member" ran out of its cell at 26.
            number.enableAutoSizing = true; number.fontSizeMax = 26f; number.fontSizeMin = 14f;
            number.rectTransform.sizeDelta = new Vector2(step - 10f, 34f);
            number.rectTransform.anchoredPosition = new Vector2(0f, 8f);

            var caption = HudPrimitives.Label("Caption", holder, 12f, UiTheme.Muted, TextAlignmentOptions.Center);
            caption.text = label;
            caption.rectTransform.sizeDelta = new Vector2(step - 10f, 18f);
            caption.rectTransform.anchoredPosition = new Vector2(0f, -16f);
        }
    }
}
