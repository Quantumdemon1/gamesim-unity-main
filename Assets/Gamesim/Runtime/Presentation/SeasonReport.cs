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
    /// <para>Every way on stays on the card while the season scrolls - a new season, the notebook,
    /// the main menu, close - in a band of their own at its foot, outside the scroll, as the
    /// owner's mockup draws its large actions (MOCKUP-PASS M5); the title, the season's numbers
    /// and the tabs hold the top. The ways on were once only at the very bottom of the scroll,
    /// below three screens of content that nothing on the report could scroll: no part of it
    /// caught the mouse, so the wheel went nowhere and clicks fell through to the HUD behind it,
    /// and a player who opened it had no way out but to quit. The whole report takes the mouse
    /// now, and a scrollbar shows how much there is.</para>
    ///
    /// <para>The mouse is not the only way through it. The keyboard's ring holds the ways on, the
    /// tabs and the table's chips, and nothing between, so Page Up and Page Down, Home and End, and
    /// a pad's right stick scroll the season itself.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed partial class SeasonReport : MonoBehaviour
    {
        private const float Pad = 28f;

        /// <summary>The narrowest and widest the card is drawn, and the margin it keeps from the canvas's edges.</summary>
        private const float MinSheetWidth = 860f, MaxSheetWidth = 1760f, SheetMargin = 32f;

        /// <summary>How dark the scrim is over the house's rail: dimmed beside the card, still there to be seen.</summary>
        private const float RailDimAlpha = .55f;

        /// <summary>How dark the scrim is over the rest of the house: the room behind the season, and the cast strip along its foot.</summary>
        private const float HouseDimAlpha = .88f;

        /// <summary>
        /// The width the season is laid out at, inside the card: the room the screen gives it, from
        /// the canvas the report is drawn on, so the dashboard fills a wide screen and still fits a
        /// 4:3 one and the larger text.
        /// </summary>
        private float Width = 1180f;

        /// <summary>The ways on, in the report's footer. Tests and screen readers find them by these words.</summary>
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
        /// <summary>Which season of the show this was for the player, from the career record; null when unknown.</summary>
        private int? shownSeason;
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

        /// <summary>The reduced-motion preference: the winner's sparkles hold still under it.</summary>
        public bool ReducedMotion { get; set; }

        /// <summary>The winner's sparkles, and how each twinkles: its phase, its speed and its resting light.</summary>
        private readonly List<(Image image, float phase, float speed, float glow)> sparkles = new List<(Image, float, float, float)>();

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
        /// The same, with the ways on the report offers at its foot: <paramref name="onNewSeason"/>,
        /// <paramref name="onMainMenu"/> and <paramref name="onReview"/> each close the report and
        /// go; <paramref name="onClose"/> runs after it closes, so the house behind can redraw. A
        /// null action is simply not offered. <paramref name="seasonNumber"/> is which season of the
        /// show this was for the player, as the director reads it once a session from the career
        /// record (<c>EpisodeDirector.SeasonNumber</c>); null leaves the number off.
        /// </summary>
        public void Show(EpisodeState state, Func<string, Texture> portrait, Action onReview,
            CareerSummary career, Action onNewSeason, Action onMainMenu, Action onClose, int? seasonNumber = null)
        {
            if (state == null) return;
            shown = state;
            shownPortrait = portrait ?? (_ => null);
            shownReview = onReview;
            shownCareer = career;
            shownNewSeason = onNewSeason;
            shownMainMenu = onMainMenu;
            shownClose = onClose;
            shownSeason = seasonNumber.HasValue && seasonNumber.Value > 0 ? seasonNumber : null;
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
            sparkles.Clear();
            litTab = -1;

            var room = Room();
            // The card stands beside the house's icon rail rather than over it (MOCKUP-PASS M5,
            // decision 13): the rail stays in view, dimmed, as the mockup keeps its sidebar. A
            // canvas too narrow for both centres the card over the rail as it always did.
            float rail = RailRight();
            float free = room.x - rail - SheetMargin;
            bool besideRail = free >= MinSheetWidth;
            float sheetWidth = besideRail ? Mathf.Min(free, MaxSheetWidth) : Mathf.Clamp(room.x - SheetMargin * 2f, MinSheetWidth, MaxSheetWidth);
            float sheetX = besideRail ? rail + (free - sheetWidth) * .5f : (room.x - sheetWidth) * .5f;
            Width = sheetWidth - 40f;

            // The house dimmed behind the season rather than blacked out, and the season on a
            // glass card, as the mockups set every summary over the room it is about. Both take
            // the mouse: a modal that catches nothing lets every click through to the HUD behind.
            // The scrim itself is clear and catches every click; the dim is drawn by the bands on
            // it, lighter over the rail, which is there to be seen and not pressed.
            var scrim = HudPrimitives.Fill("Scrim", transform, new Color(UiTheme.Ink.r, UiTheme.Ink.g, UiTheme.Ink.b, 0f), 1);
            scrim.GetComponent<Image>().raycastTarget = true;
            Stretch(scrim);
            float dimFrom = besideRail ? rail : 0f;
            if (besideRail)
            {
                // The lighter dim stops above the house's cast strip. The strip runs the frame's
                // whole width, under the rail's column as well as the card, and it is the house's,
                // not the rail's: at the rail's dim its first chips showed at half light, with the
                // brand line drawn over their faces. The corner under the rail is dimmed as the
                // house is, and the brand line stands on that dark ground.
                float strip = StripTop();
                var railDim = HudPrimitives.Fill("Rail dim", scrim, new Color(UiTheme.Ink.r, UiTheme.Ink.g, UiTheme.Ink.b, RailDimAlpha), 1);
                railDim.anchorMin = Vector2.zero; railDim.anchorMax = new Vector2(0f, 1f);
                railDim.pivot = new Vector2(0f, .5f);
                railDim.offsetMin = new Vector2(0f, strip); railDim.offsetMax = new Vector2(rail, 0f);
                var stripDim = HudPrimitives.Fill("Strip dim", scrim, new Color(UiTheme.Ink.r, UiTheme.Ink.g, UiTheme.Ink.b, HouseDimAlpha), 1);
                stripDim.anchorMin = stripDim.anchorMax = Vector2.zero;
                stripDim.pivot = Vector2.zero;
                stripDim.sizeDelta = new Vector2(rail, strip);
                stripDim.anchoredPosition = Vector2.zero;
            }
            var houseDim = HudPrimitives.Fill("House dim", scrim, new Color(UiTheme.Ink.r, UiTheme.Ink.g, UiTheme.Ink.b, HouseDimAlpha), 1);
            houseDim.anchorMin = Vector2.zero; houseDim.anchorMax = Vector2.one;
            houseDim.offsetMin = new Vector2(dimFrom, 0f); houseDim.offsetMax = Vector2.zero;
            HudPrimitives.Vignette(scrim);
            if (besideRail) BrandLine(scrim, rail);

            var sheet = HudPrimitives.Fill("Report card", scrim, new Color(UiTheme.GlassFill.r, UiTheme.GlassFill.g, UiTheme.GlassFill.b, .94f), UiTheme.GlassRadius);
            sheet.anchorMin = Vector2.zero;
            sheet.anchorMax = new Vector2(0f, 1f);
            sheet.pivot = new Vector2(0f, 0.5f);
            sheet.sizeDelta = new Vector2(sheetWidth, -SheetMargin * 2f);
            sheet.anchoredPosition = new Vector2(sheetX, 0f);
            UiTheme.Glass(sheet, UiTheme.GlassRadius);
            sheet.GetComponent<Image>().raycastTarget = true;

            // The ways on, in a band of their own at the foot of the card. Built before the header
            // so the keyboard starts on the first of them, as it did when they led the header.
            float footer = Footer(sheet, onReview);

            // The title, the season's numbers and the tabs, fixed at the top of the card.
            content = new GameObject("Fixed header", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(sheet, false);
            content.anchorMin = content.anchorMax = new Vector2(.5f, 1f);
            content.pivot = new Vector2(.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            cursor = 0f;
            Header(state);
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
            viewport.sizeDelta = new Vector2(Width, -(band + 10f + footer));
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
            scroll.verticalScrollbar = Scrollbar(sheet, band, footer);
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
        private static Scrollbar Scrollbar(RectTransform sheet, float band, float footer)
        {
            var track = HudPrimitives.Fill("Scrollbar", sheet, new Color(UiTheme.Hairline.r, UiTheme.Hairline.g, UiTheme.Hairline.b, .14f), 4);
            track.anchorMin = new Vector2(1f, 0f); track.anchorMax = new Vector2(1f, 1f);
            track.pivot = new Vector2(1f, 1f);
            track.sizeDelta = new Vector2(8f, -(band + 24f + footer));
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
            if (!IsShowing) return;
            Twinkle();
            if (scroller == null || content == null || viewport == null) return;
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
        /// The winner's sparkles brighten and fade, each at its own pace. Only their light moves,
        /// never their place, and not at all under reduced motion.
        /// </summary>
        private void Twinkle()
        {
            if (ReducedMotion || sparkles.Count == 0) return;
            float time = Time.unscaledTime;
            foreach (var (image, phase, speed, glow) in sparkles)
            {
                if (image == null) continue;
                var colour = image.color;
                colour.a = glow * (.45f + .55f * (.5f + .5f * Mathf.Sin(time * speed + phase)));
                image.color = colour;
            }
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

        /// <summary>The parts of the head of the card, by name, for a test and a screen reader.</summary>
        public const string TitleCardName = "Title card", SeasonNumbersName = "Season numbers", FooterName = "Footer";

        /// <summary>The season-number tiles' captions (decisions 9 and 14): weeks, never days, and never Game Sense's COMPETITIONS.</summary>
        public const string HouseguestsTileCaption = "Houseguests", CompetitionsTileCaption = "Competitions held";

        /// <summary>The house's brand line (decisions 10 and 23), under the rail where the mockup has its night exterior.</summary>
        public const string BrandWords = "Same house. Different stories.";

        /// <summary>The narrowest the title card and the numbers card are drawn beside each other.</summary>
        private const float TitleCardMinWidth = 520f, NumbersMinWidth = 480f;

        /// <summary>
        /// The head of the card (MOCKUP-PASS M5): the title card - the season's number, the crown,
        /// the title, the season in a line, its counts in a second, and the spectator's line - and
        /// beside it the season's numbers, a tile each; under it on a narrow card. Then the tabs,
        /// and the line saying how to scroll, as a slim row under them.
        /// </summary>
        private void Header(EpisodeState state)
        {
            var box = EndScreenKit.Box("Header", content, 0f, 0f, Width, 10f);
            var you = state.Find(state.playerId);
            bool spectator = you != null && you.status != ContestantStatus.Winner && you.status != ContestantStatus.RunnerUp;
            float inner = Width - Pad * 2f;
            const float top = 16f, gap = 16f;

            bool beside = inner >= TitleCardMinWidth + gap + NumbersMinWidth;
            float numbersWidth = beside ? Mathf.Clamp(inner * .4f, NumbersMinWidth, 560f) : inner;
            float titleWidth = beside ? inner - numbersWidth - gap : inner;
            float titleHeight = TitleCard(box, state, spectator, Pad, top, titleWidth);
            float numbersHeight = beside ? titleHeight : 100f;
            SeasonNumbers(box, state, beside ? Pad + titleWidth + gap : Pad, beside ? top : top + titleHeight + 12f, numbersWidth, numbersHeight);
            float bottom = top + titleHeight + (beside ? 0f : 12f + numbersHeight) + 12f;

            // The tabs, and the line saying how to move through the season without a mouse - said
            // once, at the head of the season: the scrollbar tells a mouse there is more, and
            // nothing else would tell a keyboard or a pad how to reach it.
            const float tabHeight = 34f;
            var tabCaptions = new[] { OverviewTabCaption, DetailTabCaption, HouseTabCaption };
            float tabX = Pad;
            for (int i = 0; i < tabCaptions.Length; i++)
            {
                int index = i;
                float w = TabWidth(tabCaptions[i]);
                Tab(box, tabCaptions[i], tabX, bottom, w, tabHeight, () => ScrollTo(index));
                tabX += w + 10f;
            }
            float hintX = tabX + 16f;
            bool hintBeside = Width - Pad - hintX >= 420f;
            var hint = EndScreenKit.Text("Scroll hint", box, ScrollHint, 13f, UiTheme.Muted,
                hintBeside ? hintX : Pad, hintBeside ? bottom + 7f : bottom + tabHeight + 6f, hintBeside ? Width - Pad - hintX : Width - Pad * 2f, 20f,
                hintBeside ? TextAlignmentOptions.Right : TextAlignmentOptions.Left);
            hint.enableAutoSizing = true; hint.fontSizeMax = 13f; hint.fontSizeMin = 10f;
            cursor = bottom + (hintBeside ? tabHeight + 8f : tabHeight + 32f);
            box.sizeDelta = new Vector2(Width, cursor);
        }

        /// <summary>
        /// The title card, framed in the pack's lit section with the winner's crown before it (the
        /// trophy it had goes to the legacy panel in M16): the season's number over the title, then
        /// the season in a line, its counts in a second, and the spectator's line. The number is the
        /// player's count of seasons from their career record, the one the finale's strap prints;
        /// without a record it is left off rather than guessed. Returns the card's height.
        /// </summary>
        private float TitleCard(RectTransform box, EpisodeState state, bool spectator, float x, float y, float width)
        {
            var card = EndScreenKit.Box(TitleCardName, box, x, y, width, 10f);
            const float pad = 16f, crownSide = 54f;
            float crownX = pad + 8f + crownSide * .5f;
            var crown = EndScreenKit.Picture("Title crown", card, PackArt.SeasonWinnerCrown, "crown", UiTheme.Gold, new Vector2(crownX, -40f), crownSide);
            float textX = crown != null ? pad + 8f + crownSide + 18f : pad + 8f;
            float textWidth = Mathf.Max(120f, width - textX - pad);
            float at = 14f;
            if (shownSeason.HasValue)
            {
                var number = EndScreenKit.Text("Season number", card, "SEASON " + shownSeason.Value, 13f, UiTheme.Gold, textX, at, textWidth, 18f,
                    TextAlignmentOptions.Left, UiTheme.Weight.SemiBold);
                number.characterSpacing = 3f;
                at += 18f;
            }
            else at += 4f;
            var title = EndScreenKit.Text("Title", card, "SEASON COMPLETE", 38f, Color.white, textX, at, textWidth, 50f,
                TextAlignmentOptions.Left, UiTheme.Weight.Bold);
            title.characterSpacing = 3f;
            title.enableVertexGradient = true;
            title.colorGradient = new VertexGradient(UiTheme.Glow, UiTheme.Glow, UiTheme.Heading, UiTheme.Heading);
            title.enableAutoSizing = true; title.fontSizeMax = 38f; title.fontSizeMin = 26f;
            at += 50f;
            var summary = EndScreenKit.Text("Summary", card, state.week + (state.week == 1 ? " week" : " weeks") + " · "
                + state.contestants.Count + " houseguests · "
                + state.contestants.Count(c => c.status == ContestantStatus.Jury) + " on the jury",
                17f, UiTheme.Muted, textX, at, textWidth, 24f);
            summary.enableAutoSizing = true; summary.fontSizeMax = 17f; summary.fontSizeMin = 13f;
            at += 24f;
            string counts = CountLine(state);
            if (counts != null)
            {
                var line = EndScreenKit.Text("Count line", card, counts, 14f, new Color(UiTheme.Paper.r, UiTheme.Paper.g, UiTheme.Paper.b, .86f),
                    textX, at, textWidth, 20f, TextAlignmentOptions.Left, UiTheme.Weight.Medium);
                line.enableAutoSizing = true; line.fontSizeMax = 14f; line.fontSizeMin = 11f;
                at += 20f;
            }
            // The web build shows "You watched this season as a spectator" on this screen when the
            // player was evicted. Same statement, drawn from status rather than a stored flag.
            if (spectator)
            {
                var watched = EndScreenKit.Text("Spectator", card, "YOU WATCHED THE REST OF THIS SEASON AS A SPECTATOR", 13f, UiTheme.Warning,
                    textX, at + 2f, textWidth, 20f);
                watched.enableAutoSizing = true; watched.fontSizeMax = 13f; watched.fontSizeMin = 10f;
                at += 22f;
            }
            float height = at + 14f;
            card.sizeDelta = new Vector2(width, height);
            EndScreenKit.Frame(card, PackArt.SeasonSectionSelected, 16f, UiTheme.Surface, UiTheme.Glow);
            if (crown != null) crown.rectTransform.anchoredPosition = new Vector2(crownX, -height * .5f);
            return height;
        }

        /// <summary>
        /// The season's second line (MOCKUP-PASS M5, decision 10): what the mockup says as
        /// "Alliances. Betrayals. A Winner. A Legacy.", said with the season's own public counts -
        /// the evictions, the vetoes used, the winner - each clause dropped when its count is
        /// nought, so the line is true of every season it is printed on. Null when there is nothing
        /// to count. Alliances and betrayals wait on the season's statistics (M16), where which of
        /// them the player may count is decision 17.
        /// </summary>
        public static string CountLine(EpisodeState state)
        {
            if (state == null) return null;
            int evictions = state.contestants.Count(c => c.status == ContestantStatus.Jury || c.status == ContestantStatus.Evicted);
            int vetoes = state.ledger?.power?.Count(row => row.vetoUsed) ?? 0;
            var clauses = new List<string>();
            if (evictions > 0) clauses.Add(evictions + (evictions == 1 ? " eviction." : " evictions."));
            if (vetoes > 0) clauses.Add(vetoes + (vetoes == 1 ? " veto used." : " vetoes used."));
            if (state.Find(state.winnerId) != null) clauses.Add("One winner.");
            return clauses.Count == 0 ? null : string.Join(" ", clauses);
        }

        /// <summary>
        /// The season's numbers, a tile each, in a card beside the title (MOCKUP-PASS M5): the
        /// houseguests, the weeks (decision 9: weeks, never days) and the competitions held
        /// (decision 14). Every competition has one winner, so the count held is the house's
        /// wins, the final Head of Household's first two parts included (<see cref="FinalistRead.Wins"/>),
        /// which reads the same on a save from before the ledger kept a row for each.
        /// </summary>
        private static void SeasonNumbers(RectTransform box, EpisodeState state, float x, float y, float width, float height)
        {
            var card = EndScreenKit.Box(SeasonNumbersName, box, x, y, width, height);
            EndScreenKit.Frame(card, PackArt.SeasonSection, 16f, UiTheme.Surface);
            int competitions = state.contestants.Sum(c => FinalistRead.Wins(state, c));
            var tiles = new (string caption, string value, string icon, string fallback, Color tint)[]
            {
                (HouseguestsTileCaption, state.contestants.Count.ToString(), PackArt.KitIconPeople, "people", UiTheme.Accent),
                (state.week == 1 ? "Week" : "Weeks", state.week.ToString(), PackArt.KitIconCalendar, "calendar", UiTheme.Heading),
                (CompetitionsTileCaption, competitions.ToString(), null, "trophy", UiTheme.Gold),
            };
            const float pad = 12f, tileGap = 10f, glyphSide = 24f;
            float tileWidth = (width - pad * 2f - tileGap * (tiles.Length - 1)) / tiles.Length;
            float tileHeight = Mathf.Min(78f, height - pad * 2f);
            float tileY = (height - tileHeight) * .5f;
            for (int i = 0; i < tiles.Length; i++)
            {
                var (caption, value, icon, fallback, tint) = tiles[i];
                var tile = EndScreenKit.Box(caption, card, pad + i * (tileWidth + tileGap), tileY, tileWidth, tileHeight);
                EndScreenKit.Frame(tile, PackArt.SeasonStatNeutral, 12f, UiTheme.SurfaceRaised, null, 10);
                float top = (tileHeight - 58f) * .5f;
                // Kit 6's glyphs are white and take the tile's tint; the generated ones come tinted.
                var glyph = EndScreenKit.Picture("Glyph", tile, icon, fallback, tint, new Vector2(12f + glyphSide * .5f, -(top + 18f)), glyphSide);
                if (glyph != null && icon != null && glyph.sprite == UiTheme.Pack(icon)) glyph.color = tint;
                float valueX = glyph != null ? 12f + glyphSide + 10f : 12f;
                var number = EndScreenKit.Text("Value", tile, value, 28f, UiTheme.Paper, valueX, top, Mathf.Max(30f, tileWidth - valueX - 8f), 37f,
                    TextAlignmentOptions.Left, UiTheme.Weight.SemiBold);
                number.enableAutoSizing = true; number.fontSizeMax = 28f; number.fontSizeMin = 18f;
                var label = EndScreenKit.Text("Caption", tile, caption, 12f, UiTheme.Muted, 12f, top + 40f, tileWidth - 20f, 17f);
                label.enableAutoSizing = true; label.fontSizeMax = 12f; label.fontSizeMin = 9f;
            }
        }

        /// <summary>The ways on's band at the foot of the card: its padding, and a button's height and widest.</summary>
        private const float FooterPad = 14f, WayHeight = 62f, WayMaxWidth = 380f;

        /// <summary>The gold face's own ground, for a clone without the pack.</summary>
        private static readonly Color LegacyFill = new Color(.33f, .28f, .09f);

        /// <summary>
        /// The ways on (MOCKUP-PASS M5), in a band fixed at the foot of the card and outside the
        /// scroll: a new season first, as the web game's primary action is, then the notebook, the
        /// main menu, and close. Only the ones given are drawn. Each is a wide pack button with its
        /// caption word for word, a glyph, a chevron and a line under the caption saying where it
        /// goes; the main menu wears the pack's gold 'Continue to Legacy' face (decision 21), since
        /// the main menu is where the career line is. Returns the band's height.
        /// </summary>
        private float Footer(RectTransform sheet, Action onReview)
        {
            var ways = new List<(string caption, Action act, string face, string icon, string fallback, string subtitle)>();
            if (shownNewSeason != null)
                ways.Add((NewSeasonCaption, () => { Hide(); shownNewSeason(); }, PackArt.SeasonButtonPrimary, PackArt.KitIconRefresh, "star", "Choose a new cast"));
            if (onReview != null)
                ways.Add((ReviewCaption, () => { Hide(); onReview(); }, ways.Count == 0 ? PackArt.SeasonButtonPrimary : PackArt.SeasonButtonReview,
                    PackArt.KitIconBook, "journal", "Open your notebook"));
            if (shownMainMenu != null)
                ways.Add((MainMenuCaption, () => { Hide(); shownMainMenu(); }, PackArt.SeasonButtonLegacy, PackArt.KitIconHome, "house", "Your career record"));
            ways.Add((CloseCaption, Close, ways.Count == 0 ? PackArt.SeasonButtonPrimary : PackArt.SeasonButton, PackArt.KitIconCross, "exit", "Return to the house"));

            float height = WayHeight + FooterPad * 2f;
            var band = new GameObject(FooterName, typeof(RectTransform)).GetComponent<RectTransform>();
            band.SetParent(sheet, false);
            band.anchorMin = band.anchorMax = new Vector2(.5f, 0f);
            band.pivot = new Vector2(.5f, 0f);
            band.sizeDelta = new Vector2(Width, height);
            band.anchoredPosition = Vector2.zero;
            var rule = new GameObject("Footer rule", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            rule.rectTransform.SetParent(band, false);
            EndScreenKit.Place(rule.rectTransform, Pad, 0f, Width - Pad * 2f, 1f);
            rule.color = new Color(UiTheme.Hairline.r, UiTheme.Hairline.g, UiTheme.Hairline.b, .3f);
            rule.raycastTarget = false;

            // The primary a little wider than the rest; a short row is centred, not stretched.
            const float gap = 12f;
            float Weight(string face) => face == PackArt.SeasonButtonPrimary ? 1.3f : 1f;
            float room = Width - Pad * 2f - gap * (ways.Count - 1);
            float weights = ways.Sum(way => Weight(way.face));
            var widths = ways.Select(way => Mathf.Min(WayMaxWidth * Weight(way.face), room * Weight(way.face) / weights)).ToList();
            float x = (Width - widths.Sum() - gap * (ways.Count - 1)) * .5f;
            for (int i = 0; i < ways.Count; i++)
            {
                var way = ways[i];
                ActionButton(band, way.caption, x, FooterPad, widths[i], WayHeight, way.act, way.face, way.icon, way.fallback, way.subtitle);
                x += widths[i] + gap;
            }
            return height;
        }

        /// <summary>
        /// The house's brand line in the corner under the rail (MOCKUP-PASS M5, decision 23): the
        /// mockup's night exterior has no set and no photo art, so its place holds the line the cast
        /// screen already says, over the scrim's vignette. Decoration: it takes no click.
        ///
        /// <para>The corner is the cast strip's, dimmed as the house is (<see cref="StripTop"/>),
        /// and the line keeps inside it: two lines of 16 from 32 up at either text size, about 72,
        /// under a strip that stands at least 135 report units.</para>
        /// </summary>
        private static void BrandLine(RectTransform scrim, float rail)
        {
            float width = rail - 40f;
            if (width < 120f) return;
            var line = HudPrimitives.Label("Brand line", scrim, 16f, new Color(UiTheme.Heading.r, UiTheme.Heading.g, UiTheme.Heading.b, .8f),
                TextAlignmentOptions.BottomLeft);
            line.text = Localisation.Text(BrandWords);
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            if (semibold != null) line.font = semibold;
            line.characterSpacing = 1f;
            var rect = line.rectTransform;
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            EndScreenKit.Wrapped(line, width);
            rect.anchoredPosition = new Vector2(20f, SheetMargin);
        }

        /// <summary>
        /// Where the house's icon rail ends, in the report's own units: the HUD's first column,
        /// converted from the HUD's reference to this canvas's (MOCKUP-PASS M5, and the review's
        /// correction 2).
        ///
        /// <para>Both canvases scale with the screen at the same match and the same shape, so one
        /// HUD unit is the report's reference width over the HUD's. At resting text that is 1.2
        /// report units, and the rail's column edge of 226 is 271 here; the larger text shrinks the
        /// report's reference and not the HUD's, and there the two agree. The rail's own width taken
        /// as report units would have sat the card over the rail at resting text.</para>
        /// </summary>
        private float RailRight()
        {
            float reference = scaler != null ? scaler.referenceResolution.x : 1920f;
            return Episode.EpisodeHud.LeftColumnX * reference / Episode.EpisodeHud.ReferenceWidth;
        }

        /// <summary>
        /// How high the house's cast strip stands off the floor, in the report's own units: its
        /// glass at the text size the HUD shares with the report (the director sets both from one
        /// setting), converted as the rail's edge is.
        ///
        /// <para>The report's text size is its reference width over 1920. The strip's chips grow
        /// with the text and the conversion shrinks as it grows, so the chips' 96 always come to
        /// 115 report units; only the strip's margins change, which puts its top at 139 at resting
        /// text and 135 at the larger.</para>
        /// </summary>
        private float StripTop()
        {
            float reference = scaler != null ? scaler.referenceResolution.x : 1920f;
            return CastRail.GroundTop(1920f / reference) * reference / Episode.EpisodeHud.ReferenceWidth;
        }

        private static float TabWidth(string caption) => Mathf.Clamp(caption.Length * 8.2f + 36f, 140f, 220f);

        /// <summary>
        /// One of the ways on: the panel named by its caption and the caption on it word for word,
        /// the pack's button behind it, a glyph to its left and a chevron to its right, and a muted
        /// line under the caption saying where it goes - a label of its own, never words added to
        /// the caption. The panel is the control, as it always was; the rest is decoration.
        /// </summary>
        private static void ActionButton(RectTransform parent, string caption, float x, float y, float width, float height,
            Action action, string face, string icon, string fallbackIcon, string subtitle)
        {
            bool primary = face == PackArt.SeasonButtonPrimary, legacy = face == PackArt.SeasonButtonLegacy;
            var fill = primary ? UiTheme.ActionBlue : legacy ? LegacyFill : UiTheme.SurfaceRaised;
            var panel = HudPrimitives.Fill(caption, parent, fill, 10);
            EndScreenKit.Place(panel, x, y, width, height);
            var ground = panel.GetComponent<Image>();
            ground.raycastTarget = true;
            var art = EndScreenKit.Frame(panel, face, 14f, fill, primary ? UiTheme.Glow : legacy ? UiTheme.Gold : UiTheme.Outline, 10);
            bool packed = EndScreenKit.Packed(art);
            Graphic target = ground;
            if (packed)
            {
                // The pack's button is the face; the panel keeps catching the mouse, clear.
                ground.color = new Color(0f, 0f, 0f, 0f);
                target = art;
            }
            else if (primary) UiTheme.AddGlow(panel, 10);

            var words = primary ? Color.white : UiTheme.Paper;
            var accent = primary ? Color.white : legacy ? UiTheme.Gold : UiTheme.Paper;
            const float glyphSide = 22f, chevronSide = 16f;
            var glyph = EndScreenKit.Picture("Glyph", panel, icon, fallbackIcon, accent, new Vector2(26f, -height * .5f), glyphSide);
            if (glyph != null && glyph.sprite == UiTheme.Pack(icon)) glyph.color = accent;
            var chevron = EndScreenKit.Picture("Chevron", panel, PackArt.KitIconChevronRight, null, accent, new Vector2(width - 22f, -height * .5f), chevronSide);
            if (chevron != null) chevron.color = new Color(accent.r, accent.g, accent.b, .8f);
            float left = glyph != null ? 48f : 14f, right = chevron != null ? 40f : 12f;
            float textWidth = Mathf.Max(40f, width - left - right);

            bool twoLines = !string.IsNullOrEmpty(subtitle);
            float captionTop = twoLines ? (height - 44f) * .5f : (height - 26f) * .5f;
            var label = EndScreenKit.Text("Label", panel, caption, 17f, words, left, captionTop, textWidth, 26f,
                TextAlignmentOptions.Left, primary ? UiTheme.Weight.SemiBold : UiTheme.Weight.Medium);
            label.enableAutoSizing = true; label.fontSizeMax = 17f; label.fontSizeMin = 12f;
            if (twoLines)
            {
                var under = EndScreenKit.Text("Subtitle", panel, subtitle, 12f,
                    primary ? new Color(1f, 1f, 1f, .78f) : legacy ? new Color(UiTheme.Gold.r, UiTheme.Gold.g, UiTheme.Gold.b, .85f) : UiTheme.Muted,
                    left, captionTop + 26f, textWidth, 18f);
                under.enableAutoSizing = true; under.fontSizeMax = 12f; under.fontSizeMin = 9f;
            }

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
                    // How far they got: the order the standings read in, one placement for every
                    // screen. Where the placement is the fallback's shared number, the standings
                    // order the tie by the week each left, and the table follows them rather than
                    // its own count of wins.
                    var standing = StandingsOrder(state).Select((entry, index) => (entry.who.id, index))
                        .ToDictionary(entry => entry.id, entry => entry.index);
                    return cast.OrderBy(c => standing.TryGetValue(c.id, out int index) ? index : int.MaxValue)
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
                    // production's removals merged in by the week each left, and the place the
                    // standings print, so a fallback's tie broken there is broken here too. The
                    // career keeps CareerLedger.Placement, the number its saved season holds.
                    int place = StandingsOrder(state).Where(entry => entry.who.id == you.id)
                        .Select(entry => entry.place).DefaultIfEmpty(CareerLedger.Placement(state, you)).First();
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
