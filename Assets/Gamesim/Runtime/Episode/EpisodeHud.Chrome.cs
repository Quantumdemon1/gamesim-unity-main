using System.Linq;
using Gamesim.Presentation;
using Gamesim.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gamesim.Episode
{
    /// <summary>
    /// The fixed chrome the mockups are recognised by (VISUAL-TARGET.md §4, V2 item 5): a top bar
    /// carrying the brand, the week and the house's numbers, and a right column of cards.
    ///
    /// <para>The names on these panels — 'Brand', 'Navigation', 'Objective', 'House pill',
    /// 'Live feed', 'Exploration controls', 'Status', 'Overview column' — are a contract of their
    /// own: the accessibility suite looks each one up by name and asserts that no two of them
    /// overlap at either text size. They are reshaped here, never renamed.</para>
    ///
    /// <para>Every rectangle below is also placed against two canvas shapes, not one. The HUD's
    /// scaler matches width and height equally, so a 16:9 window gives a 1600×900 reference and a
    /// 4:3 one gives 1386×1039 — the shape a batchmode run actually renders at. A chip tuned only to
    /// the wider reference runs under the navigation in the narrower one, which is the failure this
    /// file's constants are chosen to avoid.</para>
    /// </summary>
    public sealed partial class EpisodeHud
    {
        /// <summary>The top bar: how far below the top edge it starts, how tall it is, and its gutter.</summary>
        private const float TopBarTop = 14f;
        private const float TopBarHeight = 52f;
        private const float TopBarGap = 12f;

        /// <summary>
        /// The top bar's chips, left to right: the brand, the week, the objective, the house's
        /// numbers. Their sum plus the gaps is 1254, inside the 1386 a 4:3 window gives the
        /// canvas - which is the shape a batchmode run renders at, and the one a chip tuned only to
        /// the 1600 reference would run off.
        /// </summary>
        private const float BrandWidth = 240f;
        private const float WeekWidth = 280f;
        private const float ObjectiveChipWidth = 320f;
        private const float WeekX = 14f + BrandWidth + 16f;
        private const float ObjectiveX = WeekX + WeekWidth + TopBarGap;
        private const float PillX = ObjectiveX + ObjectiveChipWidth + TopBarGap;
        /// <summary>The compact HUD's objective card, which keeps the travel buttons.</summary>
        private const float ObjectiveWidth = 330f;
        /// <summary>How wide a canvas has to be before the tagline has room at the band's end.</summary>
        private const float TaglineCanvas = 1500f;

        /// <summary>
        /// The week · house · HoH chip, and one of its three cells.
        ///
        /// <para>340 wide rather than the mockup's half-screen banner, because the gap it has to sit
        /// in is the narrowest thing on the top bar. The pill is centred and the navigation is
        /// anchored to the right edge, so the clearance between them is
        /// <c>width/2 − 170 − 489</c> reference pixels: positive only above a 1318-wide canvas. A
        /// 4:3 window gives 1386 and a 5:4 one gives 1342, so this fits both; the 392 it was first
        /// drawn at did not fit the second.</para>
        /// </summary>
        private const float PillWidth = 360f;
        private const float PillCell = 120f;

        /// <summary>
        /// The right column of cards, started below the top bar. 248 wide: the mockups' column is
        /// narrower still, but a card here carries the engine's own sentences, which are longer than
        /// the mockups' captions, and two lines of one have to fit a row.
        /// </summary>
        public const float RightColumnWidth = 248f;
        // Was 88, which was 24 plus the icon rail's 52 plus a gap: the rail used to live in this
        // gutter. It lives in the left one now, so the column has its own margin back.
        private const float RightColumnInset = 24f;
        private const float RightColumnTop = 80f;
        private const float RightColumnGap = 12f;

        /// <summary>The recent-events card, named so a test can find it the way the others are found.</summary>
        public const string RecentEventsCardName = "Recent events";

        /// <summary>How many events the column shows; each takes the two lines its row holds (<see cref="FitExcerpt"/>).</summary>
        // Three, not four. The right column is 104 + 226 live feed + 12 + this card + 12 + the
        // knowledge card, and at four rows that stack reached y 808 of a 900-high canvas while the
        // controls box starts at 746 - a 62-pixel overlap that clipped the last row of whichever
        // card was unlucky. Four events was never the point; not colliding is.
        private const int RecentEventRows = 3;
        private const int EndgameEventRows = 2;

        /// <summary>An event's line on the Recent Events card, so a test can find it.</summary>
        public const string RecentEventLineName = "Event line";

        /// <summary>
        /// The mockups' masthead: the house mark, the wordmark in the display weight, and the season
        /// as a strap beneath it - on the set itself, not in a card. The mockups float the logo, and
        /// a bordered box around it made it the heaviest panel in the bar.
        /// </summary>
        /// <summary>The shade's name, so a test can find it the way it finds a named panel.</summary>
        public const string TopShadeName = "Top shade";

        /// <summary>
        /// The dark band every mockup has along its top edge, under the brand and the chips. The
        /// brand has no card of its own, and wherever the camera put a lit sign or a lamp behind
        /// it the wordmark went into the set. Behind everything else on the canvas; it takes no
        /// click.
        /// </summary>
        private void TopShade()
        {
            var shade = new GameObject(TopShadeName, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            shade.rectTransform.SetParent(canvas.transform, false);
            shade.rectTransform.SetAsFirstSibling();
            shade.rectTransform.anchorMin = new Vector2(0f, 1f); shade.rectTransform.anchorMax = new Vector2(1f, 1f);
            shade.rectTransform.pivot = new Vector2(.5f, 1f);
            shade.rectTransform.sizeDelta = new Vector2(0f, TopBarTop + TopBarHeight + 64f);
            shade.rectTransform.anchoredPosition = Vector2.zero;
            shade.sprite = UiTheme.FadeDown();
            shade.color = new Color(UiTheme.Background.r, UiTheme.Background.g, UiTheme.Background.b, .78f);
            shade.raycastTarget = false;
        }

        private void BrandCard(Transform parent, EpisodeState state)
        {
            // A bare rect, named for the tests that measure the band: they need where it is, and
            // nothing about it takes a click.
            var brand = new GameObject("Brand", typeof(RectTransform)).GetComponent<RectTransform>();
            brand.SetParent(parent, false);
            Anchor(brand, new Vector2(0, 1), new Vector2(0, 1), new Vector2(14f, -TopBarTop), new Vector2(BrandWidth, TopBarHeight));

            float left = 0f;
            var outline = UiTheme.Pack(PackArt.IconHome);
            if (outline != null)
            {
                var mark = new GameObject("Brand mark", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                mark.rectTransform.SetParent(brand, false);
                Anchor(mark.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0f, -5f), new Vector2(42f, 42f));
                mark.sprite = outline; mark.color = UiTheme.Heading; mark.preserveAspect = true; mark.raycastTarget = false;
                left = 50f;
            }
            else if (HudPrimitives.Glyph("Brand mark", brand, "house", UiTheme.Heading, new Vector2(4f, -10f), 32f) != null) left = 46f;

            // Inter's line at 30 is 36 tall; the box is 38 so the capitals are not clipped at the
            // top, and the word may shrink a little on a canvas that ever narrows the brand.
            var word = FixedText(brand, "GAMESIM", 30, UiTheme.Heading, new Vector2(left, 1f), new Vector2(BrandWidth - left, 38f));
            AutoSize(word, 22);
            var display = UiTheme.Font(UiTheme.Weight.Bold);
            if (display != null) word.font = display;
            word.characterSpacing = 2f;
            // The mockups' wordmark is lit from above: pale at the crown of the letters, the title
            // blue at their feet.
            word.enableVertexGradient = true;
            var crown = UiTheme.Hex("6CC0FF");
            var feet = UiTheme.Hex("3A86FF");
            word.colorGradient = new VertexGradient(crown, crown, feet, feet);
            word.color = Color.white;

            // The number is asked for only on finale night, the one place the strap prints it: the
            // director reads it from the career file once a session.
            int? season = state != null && EpisodeDirector.FinaleNight(state.phase) && director != null ? director.SeasonNumber(state) : null;
            var strap = FixedText(brand, BrandStrap(state, season),
                11, UiTheme.Muted, new Vector2(left + 1f, -36f), new Vector2(BrandWidth - left - 1f, 17f));
            var medium = UiTheme.Font(UiTheme.Weight.Medium);
            if (medium != null) strap.font = medium;
            strap.characterSpacing = 2f;
            AutoSize(strap, 9);
        }

        /// <summary>
        /// The strap under the wordmark: the house and the size of its season, and on finale night
        /// the finale, by its number when the career record gives one (MOCKUP-PASS M3; the review's
        /// correction 22 settles that the number wins over the plain 'SEASON FINALE' wherever it is
        /// known). The number is the player's count of seasons, never a fact about this one.
        /// </summary>
        public static string BrandStrap(EpisodeState state, int? season)
        {
            if (state == null) return "THE HOUSE  |  A SEASON";
            if (EpisodeDirector.FinaleNight(state.phase))
                return "THE HOUSE  |  " + (season.HasValue && season.Value > 0 ? "SEASON " + season.Value + " FINALE" : "SEASON FINALE");
            return "THE HOUSE  |  " + state.contestants.Count + "-PERSON SEASON";
        }

        /// <summary>
        /// Where the player is being sent next, named as the rail's button and the room icon name
        /// it: the episode screen (HOUSE-LIFE-PLAN §6, decision 13), not the ceremony screen.
        /// </summary>
        public string NextStop(EpisodeState state) =>
            state.pendingDiary != null
                ? (director != null && director.IsDiaryOpen ? "Here: your private reflection" : "Next stop: private diary room")
            // Production, or a story's Diary Room moment: the one call that names its own room.
            : EpisodeEngine.OpenSummons(state) != null && state.Find(state.playerId)?.status == ContestantStatus.Active
                ? (director != null && director.IsDiaryOpen ? "Here: the Diary Room is calling you" : "The Diary Room is calling you")
            : EpisodeEngine.IsCompetition(state.phase) ? "Next stop: competition yard"
            // A finished season still sends the player to the episode screen - the rail's button
            // and the station's icon both call it that - and then says what waits there. "Next
            // stop: season report" named a place no control, icon or beacon in the house is called:
            // the report is a button inside the episode screen, and only once it is open.
            : state.phase == EpisodePhase.Finished ? "Next stop: episode screen · season report"
            : "Next stop: episode screen";

        /// <summary>The objective chip's endgame mark, named apart from the objectives card's row marks.</summary>
        public const string ObjectiveCrownName = "Objective crown";

        /// <summary>
        /// The objective, as the mockups' top-bar chip: a ringed mark, the title in the heading
        /// blue, and the next stop under it.
        ///
        /// <para>It lived in the left column as a 330x276 card holding the phase, the actions left
        /// and the two travel buttons as well. Each of those has a better home now - the phase on the
        /// week chip, the actions on the house pill, the buttons in the rail - so the card became
        /// what the mockup draws: one line of what to do next. The ceremony suite still asserts a
        /// ceremony card covers no part of it, which is why the sting stands below the bar.</para>
        /// </summary>
        private void ObjectiveChip(EpisodeState state)
        {
            var objective = Chrome("Objective", canvas.transform);
            Anchor(objective, new Vector2(0, 1), new Vector2(0, 1), new Vector2(ObjectiveX, -TopBarTop),
                new Vector2(ObjectiveChipWidth, TopBarHeight));

            float words = 16f;
            // At the endgame the chip wears a crown in the power's gold in place of the ring and its
            // '!' (MOCKUP-PASS M3, mockups 59 and 60). The words beside it are the ones they always
            // were, and pinned: only the mark says the season is being decided.
            Sprite crown = null;
            if (IsEndgame(state))
            {
                crown = UiTheme.Pack(PackArt.KitIconCrown);
                if (crown == null) crown = UiTheme.Icon("crown");
            }
            var ring = crown == null ? UiTheme.Pack(PackArt.SelectionRing) : null;
            if (crown != null)
            {
                var mark = new GameObject(ObjectiveCrownName, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                mark.rectTransform.SetParent(objective, false);
                Anchor(mark.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(12f, -11f), new Vector2(30f, 30f));
                mark.sprite = crown; mark.color = UiTheme.Gold; mark.preserveAspect = true; mark.raycastTarget = false;
                words = 54f;
            }
            else if (ring != null)
            {
                var mark = new GameObject("Objective mark", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                mark.rectTransform.SetParent(objective, false);
                Anchor(mark.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(10f, -9f), new Vector2(34f, 34f));
                mark.sprite = ring; mark.color = UiTheme.Accent; mark.preserveAspect = true; mark.raycastTarget = false;
                var bang = FixedText(objective, "!", 17, UiTheme.Accent, new Vector2(10f, -14f), new Vector2(34f, 24f));
                bang.alignment = TextAlignmentOptions.Center;
                var bold = UiTheme.Font(UiTheme.Weight.Bold);
                if (bold != null) bang.font = bold;
                words = 54f;
            }

            // Somebody waiting on the player takes the title's line, over the next stop (V2).
            string waiting = WaitingLine(state);
            var title = FixedText(objective, waiting ?? ObjectiveTitle(state), 14, UiTheme.Heading,
                new Vector2(words, -7f), new Vector2(ObjectiveChipWidth - words - 12f, 19f));
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            if (semibold != null) title.font = semibold;
            if (waiting != null) WaitingStyle(title);
            var next = FixedText(objective, NextStop(state), 14, Paper,
                new Vector2(words, -27f), new Vector2(ObjectiveChipWidth - words - 12f, 20f));
            AutoSize(next, 11);
        }

        /// <summary>The objective's line when somebody is waiting on the player, by the name a test finds it by.</summary>
        public const string ObjectiveWaitingName = "Objective waiting";

        /// <summary>
        /// What the objective says in its title's place while somebody waits on the player's answer
        /// (ACTIONS-DEALS-ALLIANCES-PLAN V2): "Alex has an offer for you", "2 offers waiting", "Sam is
        /// waiting on your answer" (<see cref="WaitingOnYou.ObjectiveLine(EpisodeState)"/>). Offers,
        /// questions about the veto and the houseguests who came to the player only: a story beat or
        /// the Diary Room's call leaves the objective its own words, which the tutorial points at.
        /// Null when nobody waits, and at the endgame, whose titles are the season's own. Only what
        /// was put to the player: never anything between two houseguests.
        /// </summary>
        public static string WaitingLine(EpisodeState state) =>
            state == null || IsEndgame(state) ? null : WaitingOnYou.ObjectiveLine(state);

        /// <summary>
        /// The waiting line on one line in the title's box: a name is longer than the words it
        /// replaces, so it may shrink to the floor the next stop shrinks to rather than wrap out of
        /// a box that holds one line, and at the larger text it is fitted to the same box.
        /// </summary>
        private static void WaitingStyle(TMP_Text line)
        {
            line.name = ObjectiveWaitingName;
            line.textWrappingMode = TextWrappingModes.NoWrap;
            line.overflowMode = TextOverflowModes.Ellipsis;
            AutoSize(line, 11);
        }

        /// <summary>
        /// The compact HUD's objective: the card it has always been, in the left column, holding
        /// the next stop, the actions left and the two travel buttons - the compact HUD drops the
        /// chrome those would otherwise live in.
        /// </summary>
        private void CompactObjective(EpisodeState state, bool recovery)
        {
            var objective = Chrome("Objective", canvas.transform);
            Anchor(objective, new Vector2(0, 1), new Vector2(0, 1), new Vector2(LeftColumnX, -IconRail.Top),
                new Vector2(ObjectiveWidth, 238f));
            string waiting = WaitingLine(state);
            var heading = CardHeading(objective, waiting ?? ObjectiveTitle(state));
            if (waiting != null) WaitingStyle(heading);
            FixedText(objective, NextStop(state), 18, Paper, new Vector2(18f, -44f), new Vector2(294f, 48f));
            ActionChip(objective, state, -94f);
            FixedButton(objective,"Go to episode screen",new Vector2(18,-126),new Vector2(294,44),director.GoToStation);
            FixedButton(objective,DiaryTravelCaption,new Vector2(18,-178),new Vector2(294,44),director.GoToDiary).interactable=
                director.HasDiaryRoom && !recovery && state.Find(state.playerId)?.status==ContestantStatus.Active;
        }

        // ---------------------------------------------------------------- the endgame (ENDGAME-PLAN F1)

        /// <summary>
        /// The Final 3: three left, from the window after the final-four eviction through the three
        /// parts of the final Head of Household to their choice. Read from the committed state, never
        /// from a flag: the engine keeps three active across exactly these phases.
        /// </summary>
        public static bool IsFinalThree(EpisodeState state) =>
            state != null && state.Active.Count() == 3
            && (state.phase == EpisodePhase.Social || state.phase == EpisodePhase.FinalHoHPart1
                || state.phase == EpisodePhase.FinalHoHPart2 || state.phase == EpisodePhase.FinalHoHPart3
                || state.phase == EpisodePhase.FinalEviction);

        /// <summary>The Final 2: two left, facing the jury.</summary>
        public static bool IsFinalTwo(EpisodeState state) =>
            state != null && state.Active.Count() == 2
            && (state.phase == EpisodePhase.JuryQuestioning || state.phase == EpisodePhase.FinalSpeeches || state.phase == EpisodePhase.Jury);

        /// <summary>
        /// A juror as the engine's jury vote counts one (EpisodeEngine.ResolveJury): Jury, or Evicted
        /// on a save that carries the older word. The engine itself only ever writes Jury.
        /// </summary>
        public static bool IsJuror(ContestantState actor) =>
            actor != null && (actor.status == ContestantStatus.Jury || actor.status == ContestantStatus.Evicted);

        /// <summary>The endgame, where the frame strips down: the Final 3, the Final 2 and the finished season.</summary>
        public static bool IsEndgame(EpisodeState state) =>
            IsFinalThree(state) || IsFinalTwo(state) || (state != null && state.phase == EpisodePhase.Finished);

        /// <summary>The week chip's first word at the endgame - FINAL 3, FINAL 2, FINALE - or null for an ordinary week.</summary>
        public static string EndgameLabel(EpisodeState state) =>
            state == null ? null
            : state.phase == EpisodePhase.Finished ? "FINALE"
            : IsFinalThree(state) ? "FINAL 3"
            : IsFinalTwo(state) ? "FINAL 2"
            : null;

        /// <summary>
        /// The objective chip's title: what the endgame is about, in the outline's words, or the
        /// ordinary 'Current Objective'. The next-stop line under it is untouched.
        /// </summary>
        public static string ObjectiveTitle(EpisodeState state)
        {
            if (state == null) return "Current Objective";
            if (state.phase == EpisodePhase.Finished) return "Season complete";
            if (IsFinalTwo(state)) return "The jury decides";
            if (IsFinalThree(state))
            {
                if (state.phase == EpisodePhase.FinalEviction)
                    return state.hohId == state.playerId ? "One decision remains" : "The final Head of Household decides";
                return "Final Head of Household ahead";
            }
            return "Current Objective";
        }

        /// <summary>A phase in the one or two words the week chip has room for.</summary>
        public static string PhaseShort(EpisodePhase phase)
        {
            switch (phase)
            {
                case EpisodePhase.Social: return "Free time";
                case EpisodePhase.HoH: return "HoH comp";
                case EpisodePhase.Nomination: return "Nominations";
                case EpisodePhase.VetoSelection: return "Veto draw";
                case EpisodePhase.Veto: return "Veto comp";
                case EpisodePhase.VetoMeeting: return "Veto meeting";
                case EpisodePhase.Campaign: return "Campaign";
                case EpisodePhase.Eviction: return "Eviction";
                case EpisodePhase.FinalHoHPart1:
                case EpisodePhase.FinalHoHPart2:
                case EpisodePhase.FinalHoHPart3: return "Final HoH";
                case EpisodePhase.FinalEviction: return "Final eviction";
                case EpisodePhase.Jury:
                case EpisodePhase.JuryQuestioning:
                case EpisodePhase.FinalSpeeches: return "Jury";
                default: return "Finale";
            }
        }

        /// <summary>
        /// The week and where in it the house is, where the mockups put the day and the clock. The
        /// simulation keeps neither, and a made-up time would be a fact the game does not have; the
        /// beat of the week is the time that matters here, in the colour the panels announce it in.
        /// </summary>
        private void WeekChip(EpisodeState state)
        {
            var chip = Chrome("Week chip", canvas.transform);
            Anchor(chip, new Vector2(0, 1), new Vector2(0, 1), new Vector2(WeekX, -TopBarTop), new Vector2(WeekWidth, TopBarHeight));
            float x = 16f;
            if (HudPrimitives.Glyph("Week mark", chip, "calendar", Accent, new Vector2(14f, -15f), 22f) != null) x = 46f;
            // At the endgame the chip leads with where the season is - FINAL 3, FINAL 2, FINALE -
            // and the week moves in beside the phase (ENDGAME-PLAN F1).
            string label = EndgameLabel(state);
            var week = FixedText(chip, label ?? "WEEK " + state.week, 17, Paper, new Vector2(x, -14f), new Vector2(84f, 24f));
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            if (semibold != null) week.font = semibold;

            var rule = Panel("Week divider", chip, new Color(UiTheme.Outline.r, UiTheme.Outline.g, UiTheme.Outline.b, .8f), 0);
            Anchor(rule, new Vector2(0, 1), new Vector2(0, 1), new Vector2(x + 88f, -12f), new Vector2(1f, 28f));
            rule.GetComponent<Image>().raycastTarget = false;
            var dot = HudPrimitives.Disc("Phase dot", chip, PhaseTint(state));
            Anchor(dot, new Vector2(0, 1), new Vector2(0, 1), new Vector2(x + 100f, -21f), new Vector2(10f, 10f));
            dot.GetComponent<Image>().raycastTarget = false;
            var phase = FixedText(chip, label != null ? "Week " + state.week + " · " + PhaseShort(state.phase) : PhaseShort(state.phase),
                15, Paper, new Vector2(x + 116f, -14f), new Vector2(WeekWidth - x - 126f, 24f));
            var medium = UiTheme.Font(UiTheme.Weight.Medium);
            if (medium != null) phase.font = medium;
            // The endgame's line carries the week as well as the phase ("Week 4 · Final eviction"):
            // it may shrink to fit rather than lose its end.
            if (label != null)
            {
                phase.fontSizeMin = Mathf.Min(phase.fontSizeMin, 11f * FontScale);
                phase.textWrappingMode = TextWrappingModes.NoWrap;
                // "Week 4 · Final eviction" is wider than the slot even at the smallest size; then
                // the phase says itself alone. The endgame's week no longer turns once Part 1 has
                // begun, so it is the part of the line that says least.
                bool autoSize = phase.enableAutoSizing; float size = phase.fontSize;
                phase.enableAutoSizing = false; phase.fontSize = phase.fontSizeMin;
                if (phase.GetPreferredValues(phase.text).x > phase.rectTransform.sizeDelta.x)
                    phase.text = Localisation.Text(PhaseShort(state.phase));
                phase.fontSize = size; phase.enableAutoSizing = autoSize;
            }
            AutoSize(phase, 11);
            ClickThrough(chip);
        }

        /// <summary>The house pill's jury cell's label.</summary>
        public const string JuryCellLabel = "Jury members";

        /// <summary>
        /// The house's numbers, as the mockups' stat chip: who is still in, how many social actions
        /// are left this week, and who holds the house.
        ///
        /// <para>The mockup's Social Points and Days Left have no counterpart in the simulation and
        /// are not drawn; the actions left are the number the player spends through the week, and
        /// the Head of Household is the one fact the house turns on.</para>
        /// </summary>
        private void HousePill(EpisodeState state)
        {
            var pill = Chrome("House pill", canvas.transform);
            Anchor(pill, new Vector2(0, 1), new Vector2(0, 1), new Vector2(PillX, -TopBarTop), new Vector2(PillWidth, TopBarHeight));

            var holder = state.Find(state.hohId);
            int left = Mathf.Max(0, EpisodeEngine.SocialActionBudget(state) - EpisodeEngine.SocialActionsSpent(state));
            // A finished season has nobody left to count down to: the cell is the season's size,
            // where '0/12' read as a house that had emptied (MOCKUP-PASS M3).
            StatCell(pill, 0f, "people", Accent, state.phase == EpisodePhase.Finished
                ? state.contestants.Count.ToString() : state.Active.Count() + "/" + state.contestants.Count, "Houseguests");
            // The Final 2 spend no actions: the two facing the jury are the number instead.
            if (IsFinalTwo(state))
                StatCell(pill, PillCell, "houseguest", Accent, state.Active.Count().ToString(), "Finalists");
            else
                StatCell(pill, PillCell, "star", left == 0 ? UiTheme.Muted : UiTheme.Joke, left.ToString(), "Actions left");
            // The first name only: a full name does not fit a cell, and the pill is a glance. At the
            // endgame the cell is the jury's size instead, the number the last decisions turn on
            // (ENDGAME-PLAN F1): while nobody holds the house, at the final decision (the owner's
            // decision 51) and all finale night, when the jury is the house that matters.
            int jurors = state.contestants.Count(IsJuror);
            bool juryCell = IsEndgame(state) && (holder == null || EpisodeDirector.FinaleNight(state.phase)
                || state.phase == EpisodePhase.FinalEviction);
            // 'Jury members' under the count, as mockup 60 labels it (MOCKUP-PASS M14): the word
            // Jury leads it, as it always did.
            if (juryCell && jurors > 0)
                StatCell(pill, 2f * PillCell, "people", UiTheme.Gold, jurors.ToString(), JuryCellLabel);
            else
                StatCell(pill, 2f * PillCell, "crown", UiTheme.Gold, holder == null ? "Awaiting" : holder.name.Split(' ')[0], "HoH");
            for (int i = 1; i < 3; i++)
            {
                var rule = Panel("Stat divider", pill, new Color(UiTheme.Outline.r, UiTheme.Outline.g, UiTheme.Outline.b, .8f), 0);
                Anchor(rule, new Vector2(0, 1), new Vector2(0, 1), new Vector2(i * PillCell, -11f), new Vector2(1f, 30f));
                rule.GetComponent<Image>().raycastTarget = false;
            }
            ClickThrough(pill);
        }

        /// <summary>
        /// The mockups' tagline at the band's far end, where there is room for it: a wide window has
        /// 130 units spare past the house pill, a 4:3 one has none and goes without.
        /// </summary>
        private void Tagline()
        {
            var bounds = ((RectTransform)canvas.transform).rect;
            float width = bounds.width > 0 ? bounds.width : 1600f;
            if (width < TaglineCanvas) return;
            var line = FixedText((RectTransform)canvas.transform, "GOOD PEOPLE\nBIGGER STORIES", 11, UiTheme.Muted,
                Vector2.zero, new Vector2(200f, 34f));
            line.name = "Tagline";
            Anchor(line.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-24f, -TopBarTop - 9f), new Vector2(200f, 34f));
            line.alignment = TextAlignmentOptions.TopRight;
            line.characterSpacing = 10f;
            var medium = UiTheme.Font(UiTheme.Weight.Medium);
            if (medium != null) line.font = medium;
        }

        /// <summary>
        /// The left gutter (mockup-01): one glass ground under three lists of the same kind of row -
        /// the notebook's pages and the overview, the two places to go next, and the notebook, save
        /// and settings - each list after the first under its name (the style guide's rail).
        ///
        /// <para>Three rects, not one, because each is a contract of its own: the tests count six
        /// buttons under the rail, the keyboard suite reads the navigation's labels, and the
        /// compact HUD keeps its travel buttons in the objective card, so the middle list is the
        /// one the compact HUD goes without.</para>
        /// </summary>
        private void LeftGutter(EpisodeState state, bool recovery)
        {
            var ground = Chrome("Rail ground", canvas.transform);
            IconRail.Build(canvas.transform, new[]
            {
                new IconRail.Entry(IconRail.Mark.Overview, EpisodeDirector.OverviewSection,         "Overview"),
                new IconRail.Entry(IconRail.Mark.People,   EpisodeDirector.NotebookSection.People,  "Houseguests"),
                new IconRail.Entry(IconRail.Mark.Network,  EpisodeDirector.NotebookSection.Network, "Relationships"),
                new IconRail.Entry(IconRail.Mark.Rooms,    EpisodeDirector.NotebookSection.Rooms,   "Who is where"),
                new IconRail.Entry(IconRail.Mark.Votes,    EpisodeDirector.NotebookSection.Votes,   "The vote"),
                new IconRail.Entry(IconRail.Mark.Story,    EpisodeDirector.NotebookSection.Story,   "The story so far"),
            }, FontScale, director.ShowNotebookSection, director.ActiveSection);
            float y = IconRail.Top + IconRail.Height(6, FontScale);

            if (!Compact)
            {
                y = RailLabel("PLAY", y);
                var next = RailGroup("Next move", y, 2);
                RailButton(next, "Go to episode screen", UiTheme.Icon("camera"), 0, director.GoToStation, true);
                // Lit while the player is in the room it goes to, as the notebook's pages are.
                RailButton(next, DiaryTravelCaption, UiTheme.Icon("chat"), 1, director.GoToDiary, on: director.IsDiaryOpen).interactable =
                    director.HasDiaryRoom && !recovery && state.Find(state.playerId)?.status == ContestantStatus.Active;
                y += next.sizeDelta.y;
            }

            y = RailLabel("NOTEBOOK & SETTINGS", y);
            var navigation = RailGroup("Navigation", y, 3);
            RailButton(navigation, "Notebook [J]", UiTheme.Icon("journal"), 0, director.OpenJournal);
            RailButton(navigation, "Save [F5]", UiTheme.Pack(PackArt.IconSave), 1, director.SaveNow);
            RailButton(navigation, "Settings", UiTheme.Icon("settings"), 2, director.OpenSettings);
            y += navigation.sizeDelta.y;

            Anchor(ground, new Vector2(0, 1), new Vector2(0, 1), new Vector2(8f, -(IconRail.Top - 6f)),
                new Vector2(IconRail.Width + 12f, y - IconRail.Top + 12f));
        }

        private RectTransform RailGroup(string name, float top, int rows)
        {
            var group = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            group.SetParent(canvas.transform, false);
            Anchor(group, new Vector2(0, 1), new Vector2(0, 1), new Vector2(14f, -top),
                new Vector2(IconRail.Width, IconRail.Height(rows, FontScale)));
            return group;
        }

        /// <summary>A rail label's band, at the resting text size.</summary>
        private const float RailLabelBand = 16f;

        /// <summary>
        /// A group's name over its rows, in place of the hairline that used to divide the lists -
        /// the style guide's navigation rail, "grouped by context with clear labels". Returns where
        /// the group under it starts.
        /// </summary>
        private float RailLabel(string words, float y)
        {
            float band = RailLabelBand * FontScale;
            var label = FixedText((RectTransform)canvas.transform, words, 10, UiTheme.Muted, new Vector2(30f, -(y + 1f)), new Vector2(IconRail.Width - 30f, band));
            label.gameObject.name = "Rail label";
            label.characterSpacing = 6f;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            if (semibold != null) label.font = semibold;
            label.raycastTarget = false;
            return y + band;
        }

        private Button RailButton(RectTransform group, string caption, Sprite icon, int index, System.Action action,
            bool primary = false, bool on = false) =>
            IconRail.Row(group, caption, icon, null, on,
                (IconRail.Pad + index * (IconRail.RowHeight + IconRail.Gap)) * FontScale, FontScale,
                () => action(), primary);

        /// <summary>
        /// How many social actions are left, in the top bar's visual language: a glyph, the number
        /// large, and the noun small and tracked underneath it. The compact objective card's.
        /// </summary>
        private void ActionChip(RectTransform objective, EpisodeState state, float y)
        {
            int left = Mathf.Max(0, EpisodeEngine.SocialActionBudget(state) - EpisodeEngine.SocialActionsSpent(state));
            float text = 18f;
            if (HudPrimitives.Glyph("Actions mark", objective, "people",
                    left == 0 ? UiTheme.Muted : Accent, new Vector2(18f, y - 4f), 18f) != null) text = 44f;
            FixedText(objective, left.ToString(), 20, left == 0 ? UiTheme.Muted : Paper,
                new Vector2(text, y + 3f), new Vector2(34f, 26f));
            var caption = FixedText(objective, "SOCIAL ACTIONS LEFT", 10, UiTheme.Muted,
                new Vector2(text + 26f, y - 1f), new Vector2(240f, 16f));
            caption.characterSpacing = 6f;
        }

        /// <summary>One of the mockups' stat cells: a glyph, a value, and a small caption under it.</summary>
        private void StatCell(RectTransform pill, float x, string icon, Color tint, string value, string label)
        {
            float text = x + 12f;
            if (HudPrimitives.Glyph("Stat mark", pill, icon, tint, new Vector2(x + 12f, -14f), 24f) != null) text = x + 44f;
            float width = PillCell - (text - x) - 6f;
            var number = FixedText(pill, value, 17, Paper, new Vector2(text, -8f), new Vector2(width, 22f));
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            if (semibold != null) number.font = semibold;
            AutoSize(number, 12);
            var caption = FixedText(pill, label, 11, UiTheme.Muted, new Vector2(text, -30f), new Vector2(width, 16f));
            AutoSize(caption, 9);
        }

        /// <summary>
        /// A card heading in the mockups' voice: title case in the semibold weight and the heading
        /// blue, with nothing in front of it.
        ///
        /// <para>It was a glyph and tracked capitals. The mockups title every card in plain words -
        /// "Live Feed", "Recent Events", "Relationships" - and leave the icons to the rows, where
        /// they say something about each row; a glyph in front of a heading only said "card".</para>
        /// </summary>
        private TMP_Text CardHeading(RectTransform card, string words, Color? tint = null)
        {
            var label = FixedText(card, words, 15, tint ?? UiTheme.Heading, new Vector2(14f, -11f),
                new Vector2(card.sizeDelta.x - 28f - 72f, 22f));
            var font = UiTheme.Font(UiTheme.Weight.SemiBold);
            if (font != null) label.font = font;
            label.characterSpacing = 0f;
            return label;
        }


        /// <summary>The vibe card's name, so a test can find it without guessing.</summary>
        public const string HouseVibeCardName = "House vibe";
        private const float VibeRowHeight = 36f;

        /// <summary>
        /// What the house has been doing this week, as counts rather than a mood - in the mockups'
        /// Social Goals card: a tile per row holding the row's glyph, the word, a thin bar under it
        /// and the count at the end.
        ///
        /// <para>The mockups' card lists goals, and the simulation has none; inventing some would
        /// put a promise on screen the game does not keep. These three rows are what the player's
        /// own week has actually held, and every row carries the count as well as the bar, because
        /// a length is not something a screen reader can announce.</para>
        /// </summary>
        private float HouseVibeCard(Transform parent, float top, EpisodeState state)
        {
            if (state == null) return 0f;
            var reading = HouseVibe.Of(state);
            float width = RightColumnWidth, height = 44f + 3f * VibeRowHeight + 24f;
            var card = Chrome(HouseVibeCardName, parent);
            Anchor(card, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-RightColumnInset, -top),
                new Vector2(width, height));
            CardHeading(card, "This Week");

            int index = 0;
            foreach (var row in reading.Rows())
            {
                float y = -(42f + index * VibeRowHeight);
                var tile = Panel("Vibe tile", card, UiTheme.SurfaceRaised, 7);
                Anchor(tile, new Vector2(0, 1), new Vector2(0, 1), new Vector2(12f, y), new Vector2(28f, 28f));
                tile.GetComponent<Image>().raycastTarget = false;
                HudPrimitives.Glyph("Vibe mark", card, row.Icon, row.Tint, new Vector2(18f, y - 6f), 16f);

                float textX = 50f, trackWidth = width - textX - 44f;
                FixedText(card, row.Word, 13, Paper, new Vector2(textX, y - 1f), new Vector2(trackWidth, 18f));
                var track = Panel("Vibe track", card, new Color(UiTheme.Outline.r, UiTheme.Outline.g, UiTheme.Outline.b, .55f), 2);
                Anchor(track, new Vector2(0, 1), new Vector2(0, 1), new Vector2(textX, y - 21f), new Vector2(trackWidth, 4f));
                track.GetComponent<Image>().raycastTarget = false;
                float fraction = reading.Fraction(row.Count);
                if (fraction > 0f)
                {
                    var bar = Panel("Vibe fill", card, row.Tint, 2);
                    Anchor(bar, new Vector2(0, 1), new Vector2(0, 1), new Vector2(textX, y - 21f), new Vector2(trackWidth * fraction, 4f));
                    bar.GetComponent<Image>().raycastTarget = false;
                }
                var count = FixedText(card, row.Count.ToString(), 13, UiTheme.Muted,
                    new Vector2(width - 40f, y - 5f), new Vector2(28f, 20f));
                count.alignment = TextAlignmentOptions.Right;
                index++;
            }

            FixedText(card, HouseVibe.Tension(reading), 11, UiTheme.Muted,
                new Vector2(14f, -(44f + 3f * VibeRowHeight)), new Vector2(width - 28f, 18f));
            return height;
        }

        /// <summary>The endgame card's name and its jury strip's, so a test can find them without guessing.</summary>
        public const string ObjectivesCardName = "Objectives";
        /// <summary>The objectives card's line for a player out of the game.</summary>
        public const string SpectatorLineName = "Spectator line";
        public const string JuryStripName = "Jury strip";
        /// <summary>
        /// The card's crown before its heading, its tagline, and a done row's check. Each is named
        /// apart from the rows' 'Objective mark', which a test counts one to a row.
        /// </summary>
        public const string ObjectivesCrownName = "Objectives crown";
        public const string ObjectivesTaglineName = "Objectives tagline";
        public const string ObjectiveCheckName = "Objective check";
        /// <summary>The mockups' line under the Final 3's heading (59, 60), moved off the objective chip, whose next stop is pinned.</summary>
        public const string ObjectivesTagline = "Last competition. Last decision.";
        private const float ObjectiveRowHeight = 34f;
        private const float JurorDisc = 22f;

        /// <summary>One row of the endgame card: what to do, where it stands, and whether it is done.</summary>
        private readonly struct Objective
        {
            public readonly string Word, Status;
            public readonly bool Done;
            public Objective(string word, string status, bool done) { Word = word; Status = status; Done = done; }
        }

        /// <summary>
        /// The Final 3's and the Final 2's objectives (ENDGAME-PLAN F1, mockup-28) in the vibe card's
        /// place: three rows with a mark each, and the jury's faces under them, since the jury is
        /// now the number every decision turns on. Every mark is read from the committed state -
        /// the phase, the Head of Household, a Final 2 deal, the exchanges, the speeches - never
        /// from a flag the HUD keeps.
        ///
        /// <para>One card rather than the plan's two. The column clears the status band by 24
        /// units with the vibe card in it and the two-row feed at the endgame buys 58 more; two
        /// cards and the gap between them did not fit, and a strip at the foot of one does.</para>
        ///
        /// <para>Dressed as the mockups' gold card (MOCKUP-PASS M3): its own edge in gold, a crown
        /// before the heading, and for a finalist at three the tagline under it. The tagline takes
        /// the spectator line's slot and a spectator is never a finalist, so the card is never
        /// taller than the spectator's was.</para>
        /// </summary>
        private float EndgameCard(Transform parent, float top, EpisodeState state)
        {
            var rows = EndgameObjectives(state);
            var jurors = state.contestants.Where(IsJuror).ToList();
            float width = RightColumnWidth;
            // A player out of the game reads it here, in the frame, with no panel open (ENDGAME-PLAN F6).
            bool watching = EpisodeDirector.Spectating(state);
            bool tagline = !watching && ObjectivesTaglineShows(state);
            float rowsTop = watching || tagline ? 60f : 42f;
            float juryTop = rowsTop + 2f + rows.Count * ObjectiveRowHeight + 6f;
            float height = juryTop + 18f + JurorDisc + 14f;
            var card = Chrome(ObjectivesCardName, parent);
            Anchor(card, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-RightColumnInset, -top), new Vector2(width, height));
            // The card's own hairline turned gold, not a second edge drawn over it: every card
            // carries its border once.
            var edge = card.Find("Border");
            var edgeImage = edge != null ? edge.GetComponent<Image>() : null;
            if (edgeImage != null) edgeImage.color = new Color(UiTheme.Gold.r, UiTheme.Gold.g, UiTheme.Gold.b, .7f);

            float headingX = 14f;
            var crown = UiTheme.Pack(PackArt.KitIconCrown);
            if (crown == null) crown = UiTheme.Icon("crown");
            if (crown != null)
            {
                var mark = new GameObject(ObjectivesCrownName, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                mark.rectTransform.SetParent(card, false);
                Anchor(mark.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(14f, -12f), new Vector2(20f, 20f));
                mark.sprite = crown; mark.color = UiTheme.Gold; mark.preserveAspect = true; mark.raycastTarget = false;
                headingX = 40f;
            }
            // The heading keeps no room for a corner control: none is coming, and the word needs it.
            var heading = CardHeading(card, (IsFinalTwo(state) ? "FINAL 2" : "FINAL 3") + " OBJECTIVES");
            heading.rectTransform.anchoredPosition = new Vector2(headingX, -11f);
            heading.rectTransform.sizeDelta = new Vector2(width - headingX - 14f, 22f);

            if (watching)
            {
                var line = FixedText(card, SpectatorCaption, 11, UiTheme.Warning, new Vector2(14f, -36f), new Vector2(width - 28f, 16f));
                line.name = SpectatorLineName;
                line.characterSpacing = 3f;
            }
            else if (tagline)
            {
                var line = FixedText(card, ObjectivesTagline, 11, UiTheme.Muted, new Vector2(14f, -36f), new Vector2(width - 28f, 16f));
                line.name = ObjectivesTaglineName;
            }

            var open = UiTheme.Muted; open.a = .8f;
            var check = UiTheme.Pack(PackArt.KitIconCheck);
            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                float y = -(rowsTop + i * ObjectiveRowHeight);
                // The mark: a hollow ring while the row is open, and the gold disc with a check on it
                // once it is done. Where the row stands goes under its words rather than beside
                // them: beside, the words had 120 units and lost their ends.
                var mark = HudPrimitives.Disc("Objective mark", card, row.Done ? UiTheme.Gold : open);
                if (!row.Done) mark.GetComponent<Image>().sprite = UiTheme.Ring();
                Anchor(mark, new Vector2(0, 1), new Vector2(0, 1), new Vector2(15f, y - 5f), new Vector2(14f, 14f));
                if (row.Done && check != null)
                {
                    var tick = new GameObject(ObjectiveCheckName, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                    tick.rectTransform.SetParent(card, false);
                    Anchor(tick.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(17f, y - 7f), new Vector2(10f, 10f));
                    tick.sprite = check; tick.color = UiTheme.Ink; tick.preserveAspect = true; tick.raycastTarget = false;
                }
                const float words = 36f;
                FixedText(card, row.Word, 13, Paper, new Vector2(words, y - 2f), new Vector2(width - words - 14f, 18f));
                FixedText(card, row.Status, 11, row.Done ? UiTheme.Gold : UiTheme.Muted,
                    new Vector2(words, y - 18f), new Vector2(width - words - 14f, 14f));
            }

            var strip = new GameObject(JuryStripName, typeof(RectTransform)).GetComponent<RectTransform>();
            strip.SetParent(card, false);
            Anchor(strip, new Vector2(0, 1), new Vector2(0, 1), new Vector2(14f, -juryTop), new Vector2(width - 28f, 18f + JurorDisc));
            // The strip is a door to the jury house wherever the house can open it from here (the
            // owner's decision 42, MOCKUP-PASS M14): a link in its heading's corner, so the faces
            // stay faces.
            bool door = director.JuryStripIsADoor(state);
            FixedText(strip, "The jury (" + jurors.Count + ")", 12, UiTheme.Muted, Vector2.zero,
                new Vector2(width - 28f - (door ? JuryStripLinkWidth + 4f : 0f), 16f));
            if (door) JuryStripLink(strip);
            // A face per juror in a row; a jury too wide for the strip overlaps like a fanned hand.
            float pitch = jurors.Count > 1 ? Mathf.Min(JurorDisc + 2f, (width - 28f - JurorDisc) / (jurors.Count - 1)) : 0f;
            for (int i = 0; i < jurors.Count; i++)
            {
                var portrait = CharacterPortraits.Get(jurors[i]);
                var ring = HudPrimitives.Disc("Juror", strip, portrait != null ? Color.white : UiTheme.SurfaceRaised);
                Anchor(ring, new Vector2(0, 1), new Vector2(0, 1), new Vector2(i * pitch, -18f), new Vector2(JurorDisc, JurorDisc));
                if (portrait != null)
                {
                    ring.gameObject.AddComponent<Mask>().showMaskGraphic = false;
                    var face = new GameObject("Face", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
                    face.rectTransform.SetParent(ring, false);
                    Stretch(face.rectTransform, 0, 0, 0, 0);
                    face.raycastTarget = false;
                    face.texture = portrait;
                    CharacterPortraits.Bind(face, jurors[i]);
                }
                else
                {
                    // No authored art for this persona, as the rail handles it: the initial on the
                    // surface tone, still a face-shaped slot.
                    var initial = FixedText(ring, string.IsNullOrEmpty(jurors[i].name) ? "?" : jurors[i].name.Substring(0, 1),
                        11, Paper, Vector2.zero, new Vector2(JurorDisc, JurorDisc));
                    initial.alignment = TextAlignmentOptions.Center;
                }
            }
            return height;
        }

        /// <summary>The strip's door, wide enough for "Jury house" at the larger text's 14 without shrinking, and its chevron.</summary>
        public const float JuryStripLinkWidth = 104f;

        /// <summary>
        /// The jury strip's door (the owner's decision 42, MOCKUP-PASS M14): a corner link drawn as
        /// the other cards' corner links are, captioned with words of its own. 'The jury house' is
        /// the free tile's and the Final 2's row's caption, and a second control with those words
        /// would be two controls a test or a screen reader cannot tell apart. The director builds
        /// it only while nothing is open over the house, so it is never live beside those doors.
        /// </summary>
        private void JuryStripLink(RectTransform strip)
        {
            var link = Panel(EpisodeDirector.JuryStripCaption, strip, Color.white, 6);
            Anchor(link, new Vector2(1, 1), new Vector2(1, 1), new Vector2(0f, 1f), new Vector2(JuryStripLinkWidth, 18f));
            var button = Pressable(link, director.OpenJuryHouseFromStrip);
            var colours = button.colors;
            colours.normalColor = new Color(1f, 1f, 1f, 0f);
            colours.highlightedColor = new Color(UiTheme.SurfaceRaised.r, UiTheme.SurfaceRaised.g, UiTheme.SurfaceRaised.b, .9f);
            colours.selectedColor = colours.highlightedColor;
            // Clear while the column cannot be pressed too: the default disabled tint painted the
            // white ground grey over the words whenever the column stood down behind a card (the
            // grey blob at the strip's end in endgame-final-three; UI-UX-PASS-PLAN T0).
            colours.disabledColor = CornerLinkDisabled;
            button.colors = colours;
            var navigation = button.navigation; navigation.mode = Navigation.Mode.None; button.navigation = navigation;
            // 20 tall for a 12, which is a 14 at the larger text size: over 1.3 times the words, as
            // 'View all' keeps them. The box is centred on the 18 tall link, so the words sit where
            // they did, and it reaches down no further than the top of the faces under the heading.
            var words = FixedText(link, EpisodeDirector.JuryStripCaption, 12, Accent, new Vector2(4f, 1f), new Vector2(JuryStripLinkWidth - 24f, 20f));
            words.alignment = TextAlignmentOptions.Right;
            HudPrimitives.Chevron(link, Accent, 9f).anchoredPosition = new Vector2(-6f, 0f);
        }

        /// <summary>The Final 2's leading row under the finale rules: the screen it names is the final case's.</summary>
        public const string FinalCaseObjective = "Prepare your final case";

        /// <summary>
        /// Whether the Final 3's tagline heads the card: for a finalist, while the competition or
        /// the decision it names is still theirs to come. A finalist who lost the final Head of
        /// Household has neither left, and the line would not be true of them.
        ///
        /// <para>Part 3 is the Part 1 and Part 2 winners' (EpisodeEngine.CompetitionPlayers), so
        /// a finalist beaten in Part 2 has played their last competition from the moment Part 2 is
        /// decided, and sits Part 3 out. Once Part 3 is decided the decision is its winner's alone,
        /// though the phase holds until the house moves on.</para>
        /// </summary>
        public static bool ObjectivesTaglineShows(EpisodeState state)
        {
            if (!IsFinalThree(state)) return false;
            var me = state.Find(state.playerId);
            if (me == null || me.status != ContestantStatus.Active) return false;
            bool inPartThree = state.playerId == state.finalPart1WinnerId || state.playerId == state.finalPart2WinnerId;
            switch (state.phase)
            {
                case EpisodePhase.FinalHoHPart2: return !state.competitionResolved || inPartThree;
                case EpisodePhase.FinalHoHPart3: return inPartThree && (!state.competitionResolved || state.hohId == state.playerId);
                case EpisodePhase.FinalEviction: return state.hohId == state.playerId;
                default: return true;
            }
        }

        /// <summary>
        /// The endgame's three objectives, from the committed state: for a finalist, the outline's
        /// three (the final Head of Household, who to trust, the case to the jury; then the
        /// questions, the speech, the vote); for a player on the jury, the juror's three.
        /// </summary>
        private static System.Collections.Generic.List<Objective> EndgameObjectives(EpisodeState state)
        {
            var rows = new System.Collections.Generic.List<Objective>();
            string player = state.playerId;
            var me = state.Find(player);
            bool finalist = me != null && me.status == ContestantStatus.Active;
            if (IsFinalThree(state))
            {
                bool decided = state.phase == EpisodePhase.FinalEviction;
                string part = EpisodeDirector.FinalHoHPartLabel(state.phase) ?? "Ahead";
                if (finalist)
                {
                    bool won = decided && state.hohId == player;
                    rows.Add(new Objective("Win the Final HoH", won ? "Won" : decided ? "Lost" : part, won));
                    // The final Head of Household's one decision (MOCKUP-PASS M3, mockup 59): who
                    // sits beside them. The row says what the choice is and that it is final, not
                    // who a deal points at - the choice screen reads the evidence.
                    if (won) rows.Add(new Objective("Take one to the Final 2", "Cannot be undone", false));
                    else
                    {
                        var partner = FinalTwoPartner(state, player);
                        rows.Add(new Objective("Decide who to trust", partner != null ? FinalistRead.FirstName(partner.name) : "Open", partner != null));
                    }
                    // The case itself is the Final 2's to prepare (ENDGAME-PLAN F4): the row says so
                    // rather than promising a screen the Final 3 does not have.
                    rows.Add(new Objective("Prepare your case", "At the Final 2", false));
                }
                else
                {
                    var holder = decided && state.hohId != null ? state.Find(state.hohId) : null;
                    rows.Add(new Objective("Follow the Final HoH", holder != null ? FinalistRead.FirstName(holder.name) : part, holder != null));
                    // Removed by production: no seat on the jury, so nothing to weigh and no vote to come.
                    bool removed = me != null && me.status == ContestantStatus.Expelled;
                    rows.Add(removed ? new Objective("Watch the Final 2", "Ahead", false) : new Objective("Weigh the finalists", "At the Final 2", false));
                    rows.Add(removed ? new Objective("Watch the jury vote", "Ahead", false) : new Objective("Cast your vote", "Ahead", false));
                }
            }
            else
            {
                bool questioning = state.phase == EpisodePhase.JuryQuestioning;
                bool voting = state.phase == EpisodePhase.Jury;
                if (finalist)
                {
                    if (EpisodeEngine.FinaleOn(state))
                    {
                        // Under the finale rules the final case leads, in the questions' place (the
                        // owner's decision 46): the questions still come, but the argument is what
                        // there is to prepare. Once the speech is in, a case never locked can no
                        // longer be, and the row says so rather than calling it open.
                        bool locked = state.finalArgument != null;
                        rows.Add(new Objective(FinalCaseObjective,
                            locked ? "Locked" : EpisodeDirector.FinalCaseAvailable(state) ? "Open" : "Not locked", locked));
                    }
                    else
                    {
                        var mine = state.juryExchanges.Where(exchange => exchange.finalistId == player).ToList();
                        int answered = mine.Count(exchange => exchange.completed);
                        rows.Add(new Objective("Answer the jury's questions",
                            !questioning ? "Done" : mine.Count > 0 ? answered + " of " + mine.Count : "Open", !questioning));
                    }
                    bool spoke = voting || state.finalSpeeches.Any(speech => speech.speakerId == player);
                    rows.Add(new Objective("Deliver your final speech",
                        spoke ? "Done" : state.phase == EpisodePhase.FinalSpeeches ? "Now" : "Ahead", spoke));
                    rows.Add(new Objective("Await the jury vote", voting ? "Voting" : "Ahead", false));
                }
                else if (me != null && me.status == ContestantStatus.Expelled)
                {
                    // Removed by production: no questions to ask and no vote to cast, only the finale to watch.
                    rows.Add(new Objective("Watch the questions", questioning ? "Now" : "Done", !questioning));
                    rows.Add(new Objective("Hear the final speeches", voting ? "Done" : questioning ? "Ahead" : "Now", voting));
                    rows.Add(new Objective("Watch the jury vote", voting ? "Now" : "Ahead", false));
                }
                else
                {
                    rows.Add(new Objective("Question the finalists", questioning ? "Now" : "Done", !questioning));
                    rows.Add(new Objective("Hear the final speeches", voting ? "Done" : questioning ? "Ahead" : "Now", voting));
                    bool cast = voting && state.votes.Any(vote => vote.voterId == player);
                    rows.Add(new Objective("Cast your vote", cast ? "Cast" : voting ? "Now" : "Ahead", cast));
                }
            }
            return rows;
        }

        /// <summary>
        /// The houseguest a Final 2 deal or promise binds the player to, while both are still in the
        /// house - the "decide who to trust" mark - or null.
        /// </summary>
        private static ContestantState FinalTwoPartner(EpisodeState state, string player)
        {
            foreach (var deal in state.deals)
            {
                if (deal.type != DealKind.FinalTwo || (deal.status != DealStatus.Accepted && deal.status != DealStatus.Active)) continue;
                if (deal.proposerId != player && deal.recipientId != player) continue;
                var other = state.Find(deal.proposerId == player ? deal.recipientId : deal.proposerId);
                if (other != null && other.status == ContestantStatus.Active) return other;
            }
            foreach (var promise in state.promises)
            {
                if (promise.kind != PromiseKind.FinalTwo || promise.status != PromiseStatus.Active) continue;
                if (promise.fromId != player && promise.toId != player) continue;
                var other = state.Find(promise.fromId == player ? promise.toId : promise.fromId);
                if (other != null && other.status == ContestantStatus.Active) return other;
            }
            return null;
        }

        /// <summary>
        /// The right column (mockup-01, -03, -04): the live feed's picture over a timeline of what the
        /// house has just done - or, while somebody is in focus, where the player stands with the
        /// house, that person first (mockup-01's selected-houseguest state).
        ///
        /// <para>The overview is a camera mode rather than a page. Mockup-03 keeps the live feed at
        /// the head of the column over the labelled house, and the room list takes the timeline's
        /// place under it: who is where is the question the overview answers.</para>
        /// </summary>
        private void RightColumn(EpisodeState state)
        {
            if (director.IsOverview)
            {
                float at = RightColumnTop;
                if (!Compact && director.LiveFeedTexture != null) at += LiveFeedCard(canvas.transform, at) + RightColumnGap;
                OverviewColumn(canvas.transform, at);
                // The strategic briefing over the labelled house, between the rail and the column
                // (EpisodeHud.Overview.cs), while it is asked for; the map with its chips and its
                // floor to click is what is left when it is put away.
                if (!Compact && director.IsBriefing) OverviewDashboard(state, director.Dashboard(state));
                return;
            }
            if(Compact)return;
            float top = RightColumnTop;
            string focus = director.TalkingToId ?? director.FollowedId;
            if (!string.IsNullOrEmpty(focus) && state.Find(focus) != null && focus != state.playerId)
                top += RelationshipsCard(canvas.transform, state, top, focus) + RightColumnGap;
            else
            {
                if (director.LiveFeedTexture != null) top += LiveFeedCard(canvas.transform, top) + RightColumnGap;
                top += RecentEventsCard(canvas.transform, state, top) + RightColumnGap;
            }
            // The endgame's objectives and the jury's faces take the vibe card's place at three and
            // at two (ENDGAME-PLAN F1); the finished season keeps the week it just had.
            if (IsFinalThree(state) || IsFinalTwo(state)) EndgameCard(canvas.transform, top, state);
            else HouseVibeCard(canvas.transform, top, state);
            // Built whether or not listening in is on offer this moment: the director's Nearby tick
            // decides when it shows. Built only by a render that found it on offer, it did not exist
            // until the next render - and the first render comes before the house is ready.
            NearbyCard(canvas.transform, top);
            if (EpisodeEngine.StoryOn(state)) PullCard(canvas.transform, top);
        }

        /// <summary>The Nearby card's name, and its control's caption.</summary>
        public const string NearbyCardName = "Nearby card";
        public const string ListenInCaption = "Listen in";
        private RectTransform nearbyCard;

        /// <summary>
        /// Mockup-06's Nearby card: two houseguests are talking where the player can see them, and
        /// listening in is on offer - the house's own Eavesdrop, with its real cost and odds. Built
        /// hidden in the week card's place and swapped in by <see cref="SetNearby"/> while a
        /// conversation is being witnessed. The card names nobody, but listening in takes the pair
        /// being witnessed: what is overheard is theirs (STRATEGY-LOOP-PLAN.md section 2).
        /// </summary>
        private void NearbyCard(Transform parent, float top)
        {
            float width = RightColumnWidth, height = 168f;
            nearbyCard = Chrome(NearbyCardName, parent);
            Anchor(nearbyCard, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-RightColumnInset, -top), new Vector2(width, height));
            CardHeading(nearbyCard, "Nearby");
            float x = 16f;
            if (HudPrimitives.Glyph("Nearby mark", nearbyCard, "ear", UiTheme.Joke, new Vector2(14f, -42f), 30f) != null) x = 54f;
            var title = FixedText(nearbyCard, "You can eavesdrop", 15, UiTheme.Allied, new Vector2(x, -40f), new Vector2(width - x - 12f, 22f));
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            if (semibold != null) title.font = semibold;
            var line = FixedText(nearbyCard, "Two houseguests are talking within earshot.", 12, UiTheme.Muted, new Vector2(x, -62f), new Vector2(width - x - 12f, 32f));
            AutoSize(line, 10);
            var listen = FixedButton(nearbyCard, ListenInCaption, new Vector2(14f, -100f), new Vector2(width - 28f, 36f), director.ListenInNearby);
            var ground = listen.GetComponent<Image>();
            ground.color = new Color(UiTheme.Ink.r, UiTheme.Ink.g, UiTheme.Ink.b, .9f);
            UiTheme.AddBorder((RectTransform)listen.transform, UiTheme.ControlRadius, UiTheme.Allied);
            var words = listen.GetComponentInChildren<TMP_Text>();
            if (words != null)
            {
                words.fontSize = 15; words.fontSizeMax = 15; words.alignment = TextAlignmentOptions.Center;
                if (semibold != null) words.font = semibold;
            }
            var odds = FixedText(nearbyCard, "Uses an action \u00b7 works about 7 times in 10", 11, UiTheme.Muted, new Vector2(14f, -140f), new Vector2(width - 28f, 18f));
            odds.alignment = TextAlignmentOptions.Center;
            nearbyCard.gameObject.SetActive(false);

            // And mockup-06's bar low in the frame, in the status line's place while it holds.
            var state = director.Snapshot;
            nearbyBar = state != null ? SpeechBar(state.playerId, SelfTitle(state.Find(state.playerId)),
                "Two houseguests are talking within earshot. Listening in uses an action, and somebody may notice.", false) : null;
            if (nearbyBar != null) nearbyBar.gameObject.SetActive(false);
        }

        private RectTransform nearbyBar;
        /// <summary>Whether the director last asked for the Nearby card: kept, so a rebuild can put it back as asked.</summary>
        private bool nearbyWanted;

        /// <summary>Swaps the Nearby card in for the week card while a conversation is witnessed.</summary>
        public void SetNearby(bool visible)
        {
            nearbyWanted = visible;
            ApplyNearby();
        }

        /// <summary>
        /// The Nearby card and its bar up or down as last asked, the status line standing down for
        /// the bar. The status line is the one this render built, kept from <see cref="Begin"/>:
        /// found by name, a rebuild's old copy - inactive, on its way out at the frame's end - came
        /// first, so the new one stayed up over the bar's words (the play sweep's row 22).
        /// </summary>
        private void ApplyNearby()
        {
            // A story's Pull outranks it: both borrow the week card's place, and the Pull is rarer.
            bool visible = nearbyWanted && !PullShowing;
            if (nearbyCard == null || nearbyCard.gameObject.activeSelf == visible) return;
            nearbyCard.gameObject.SetActive(visible);
            SyncWeekCard();
            if (nearbyBar != null)
            {
                nearbyBar.gameObject.SetActive(visible);
                ApplyStatusLine();
            }
            MarkChromeChanged();
        }

        /// <summary>
        /// Whether a ceremony's own card is saying what the status line would (UI-UX-PASS-PLAN V0,
        /// decision 14): the veto meeting's card and the strip reporting it announce the meeting, and
        /// the line under them said it a third time. Kept, as the Nearby card's wish is, so a render
        /// under the card builds the line down; asked of the director by that render, and set by it
        /// every frame after.
        /// </summary>
        private bool statusUnderCard;

        /// <summary>
        /// What the status line said when the card began: the line stands down only while it still
        /// says that. A line with anything else to say - a season loaded or begun under the card, a
        /// save that needs attention, the next commit's result - is news the card does not carry,
        /// and it stays up.
        /// </summary>
        private string statusCardSays;

        /// <summary>The card's wish, remembering the line it began over as it turns on.</summary>
        private void SetStatusUnderCard(bool under)
        {
            if (under && !statusUnderCard) statusCardSays = lastStatusMessage;
            statusUnderCard = under;
        }

        /// <summary>
        /// Stands the status line down while a ceremony's card announces the beat it reports, and
        /// back up the frame the card is gone. Only the line, and only while it says what it said as
        /// the card began: under any other card, and for any other words, it stays where it is.
        /// </summary>
        public void StatusUnderCard(bool under)
        {
            if (statusUnderCard == under) return;
            SetStatusUnderCard(under);
            ApplyStatusLine();
        }

        /// <summary>Whether a ceremony's card is announcing the beat the status line reports. A read for tests.</summary>
        public bool IsStatusUnderCard => statusUnderCard;

        /// <summary>Whether the status line is the one a ceremony's card is saying: under the card, with the words it began over.</summary>
        private bool StatusSaysTheCard => statusUnderCard && lastStatusMessage == statusCardSays;

        /// <summary>
        /// The status line up or down as everything that stands in its place asks: down while the
        /// Nearby bar holds its place or a ceremony's card says what it would, up otherwise. The line
        /// is the one this render built (<see cref="statusRoot"/>), never a rebuild's copy on its way
        /// out; a line that arrives as it comes back up keeps its reveal (HudReveal waits while the
        /// line is down and fades it in after).
        /// </summary>
        private void ApplyStatusLine()
        {
            if (statusRoot == null) return;
            bool show = (nearbyBar == null || !nearbyBar.gameObject.activeSelf) && !StatusSaysTheCard;
            if (statusRoot.gameObject.activeSelf == show) return;
            statusRoot.gameObject.SetActive(show);
            MarkChromeChanged();
        }

        /// <summary>The relationships card's name. Not "Relationships": that is a rail row's caption.</summary>
        public const string RelationshipsCardName = "Relationships card";
        public const string SeeAllRelationshipsCaption = "See all";
        private const float RelationshipRowHeight = 50f;

        /// <summary>
        /// Where the player stands with the house, one row a houseguest, the one in focus first and
        /// lifted (mockup-01): a face, the first name, the mood in its colour, and the reading as
        /// hearts over a bar.
        ///
        /// <para>Only the player's own outbound reading and the moods the cast strip already shows
        /// - the same boundary as <see cref="CastRail.StandingOf"/>. What any of them think of the
        /// player is exactly what the game does not tell you, so it is not drawn.</para>
        /// </summary>
        private float RelationshipsCard(Transform parent, EpisodeState state, float top, string focusId)
        {
            var bounds = ((RectTransform)canvas.transform).rect;
            int most = (bounds.height > 0 ? bounds.height : 900f) < 1000f ? 7 : 8;
            var people = RelationshipWeb.Others(state)
                .OrderByDescending(actor => actor.id == focusId)
                .ThenByDescending(actor => System.Math.Abs(state.Score(state.playerId, actor.id)))
                .Take(most)
                .ToList();

            float width = RightColumnWidth;
            float height = 44f + people.Count * RelationshipRowHeight + 6f;
            var card = Chrome(RelationshipsCardName, parent);
            Anchor(card, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-RightColumnInset, -top), new Vector2(width, height));
            CardHeading(card, "Relationships");

            var link = Panel(SeeAllRelationshipsCaption, card, Color.white, 6);
            Anchor(link, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-8f, -8f), new Vector2(72f, 26f));
            var linkButton = Pressable(link, () => director.ShowNotebookSection(EpisodeDirector.NotebookSection.Network));
            var colours = linkButton.colors;
            colours.normalColor = new Color(1f, 1f, 1f, 0f);
            colours.highlightedColor = new Color(UiTheme.SurfaceRaised.r, UiTheme.SurfaceRaised.g, UiTheme.SurfaceRaised.b, .9f);
            colours.selectedColor = colours.highlightedColor;
            colours.disabledColor = CornerLinkDisabled;
            linkButton.colors = colours;
            var navigation = linkButton.navigation; navigation.mode = Navigation.Mode.None; linkButton.navigation = navigation;
            var words = FixedText(link, SeeAllRelationshipsCaption, 12, Accent, new Vector2(6f, -4f), new Vector2(48f, 18f));
            words.alignment = TextAlignmentOptions.Right;
            HudPrimitives.Chevron(link, Accent, 9f).anchoredPosition = new Vector2(-6f, 0f);

            for (int i = 0; i < people.Count; i++)
                RelationshipRow(card, state, people[i], -(40f + i * RelationshipRowHeight), people[i].id == focusId);
            return height;
        }

        private void RelationshipRow(RectTransform card, EpisodeState state, ContestantState actor, float y, bool focused)
        {
            float width = RightColumnWidth;
            if (focused)
            {
                var lift = Panel("Focus row", card, new Color(UiTheme.SurfaceRaised.r, UiTheme.SurfaceRaised.g, UiTheme.SurfaceRaised.b, .92f), 8);
                Anchor(lift, new Vector2(0, 1), new Vector2(0, 1), new Vector2(6f, y + 2f), new Vector2(width - 12f, RelationshipRowHeight - 2f));
                lift.GetComponent<Image>().raycastTarget = false;
                UiTheme.AddBorder(lift, 8, UiTheme.Hairline);
            }

            // A rounded square, as the mockups frame a face in a list; the strip keeps the circles.
            var frame = Panel("Face frame", card, Color.white, 6);
            Anchor(frame, new Vector2(0, 1), new Vector2(0, 1), new Vector2(14f, y - 3f), new Vector2(40f, 40f));
            frame.GetComponent<Image>().raycastTarget = false;
            frame.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            var face = new GameObject("Face", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            face.rectTransform.SetParent(frame, false);
            Stretch(face.rectTransform, 0, 0, 0, 0);
            face.raycastTarget = false;
            face.texture = CharacterPortraits.Get(actor);
            CharacterPortraits.Bind(face, actor);

            var kind = RelationshipWeb.KindOf(state, actor.id);
            var tint = kind == RelationshipWeb.Kind.Neutral ? UiTheme.Muted : RelationshipWeb.StandingColour(kind);
            var name = FixedText(card, actor.name.Split(' ')[0], 14, Paper, new Vector2(64f, y - 5f), new Vector2(width - 64f - 70f, 20f));
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            if (semibold != null) name.font = semibold;
            FixedText(card, string.IsNullOrEmpty(actor.mood) ? "Neutral" : actor.mood, 12, RelationshipWeb.MoodColour(actor.mood),
                new Vector2(64f, y - 25f), new Vector2(width - 64f - 70f, 17f));

            // Three hearts for where the player stands: how many are lit says how warm, the colour
            // says which kind of warm - an alliance is the web's blue, a friendship its green.
            double score = state.Score(state.playerId, actor.id);
            int lit = score >= 50 ? 3 : score >= 15 ? 2 : score > -15 ? 1 : 0;
            var dim = new Color(UiTheme.Outline.r, UiTheme.Outline.g, UiTheme.Outline.b, .7f);
            for (int h = 0; h < 3; h++)
                HudPrimitives.Glyph("Heart", card, "heart", h < lit ? tint : dim,
                    new Vector2(width - 64f + h * 16f, y - 7f), 14f);
            var track = Panel("Standing track", card, new Color(UiTheme.Outline.r, UiTheme.Outline.g, UiTheme.Outline.b, .55f), 2);
            Anchor(track, new Vector2(0, 1), new Vector2(0, 1), new Vector2(width - 64f, y - 28f), new Vector2(46f, 4f));
            track.GetComponent<Image>().raycastTarget = false;
            float fill = Mathf.Clamp01((float)(score + 100.0) / 200f);
            if (fill > 0f)
            {
                var bar = Panel("Standing fill", card, tint, 2);
                Anchor(bar, new Vector2(0, 1), new Vector2(0, 1), new Vector2(width - 64f, y - 28f), new Vector2(46f * fill, 4f));
                bar.GetComponent<Image>().raycastTarget = false;
            }
        }

        /// <summary>
        /// Recent Events as the mockups draw it: a tile holding the kind's glyph, when it happened,
        /// and the line the engine wrote, newest first, with a hairline between rows.
        ///
        /// <para>Only what the player is allowed to have seen. The engine tags a private event with
        /// its audience, and the same rule the notebook and the weekly recap use applies here — a
        /// column that quietly reported NPC-to-NPC secrets would be handing the player information
        /// their character does not have.</para>
        ///
        /// <para>"When" is the beat of the week, not the mockups' clock time: the simulation keeps
        /// no clock, and a time it does not have is a fact it would be making up.</para>
        /// </summary>
        private float RecentEventsCard(Transform parent, EpisodeState state, float top)
        {
            if (state == null) return 0f;
            var entries = state.events
                .Where(entry => entry.audienceIds.Count == 0 || entry.audienceIds.Contains(state.playerId))
                // Phase markers are the engine's scaffolding, not something that happened - the
                // story page leaves them out for the same reason. They filled the card with
                // "Week 1 · Nomination" where the nomination itself should have been. So are the
                // competition's committed standings and performance arithmetic: the record's
                // numbers, which nobody in the house sees (UI-UX-PASS-PLAN decision 12).
                .Where(entry => !EpisodeEngine.IsScaffolding(entry.kind))
                .Reverse()
                // A quieter house at the endgame: two rows, as the outline asks (ENDGAME-PLAN F1).
                .Take(IsEndgame(state) ? EndgameEventRows : RecentEventRows)
                .ToList();

            const float RowHeight = 58f;
            float width = RightColumnWidth;
            float height = 42f + Mathf.Max(1, entries.Count) * RowHeight + 2f;
            var card = Chrome(RecentEventsCardName, parent);
            Anchor(card, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-RightColumnInset, -top),
                new Vector2(width, height));
            CardHeading(card, "Recent Events");
            ViewAllEventsLink(card);

            if (entries.Count == 0)
            {
                FixedText(card, "Nothing has happened yet.", 13, UiTheme.Muted, new Vector2(14f, -44f),
                    new Vector2(width - 28f, 22f));
                return height;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                float y = -(42f + i * RowHeight);
                var tile = Panel("Event tile", card, UiTheme.SurfaceRaised, 8);
                Anchor(tile, new Vector2(0, 1), new Vector2(0, 1), new Vector2(12f, y - 2f), new Vector2(34f, 34f));
                tile.GetComponent<Image>().raycastTarget = false;
                HudPrimitives.Glyph("Event mark", card, EventGlyph(entry.kind), EventTint(entry.kind), new Vector2(19f, y - 9f), 20f);

                const float text = 56f;
                string when = PhaseShort(entry.phase) + (entry.week != state.week ? " · Week " + entry.week : "");
                FixedText(card, when, 11, UiTheme.Muted, new Vector2(text, y), new Vector2(width - text - 12f, 15f));
                // The house's word for the default season's frozen arrival line, as the opening's card says it.
                var line = FixedText(card, EpisodeDirector.EventLine(state, entry), 13, Paper,
                    new Vector2(text, y - 15f), new Vector2(width - text - 12f, 36f));
                line.name = RecentEventLineName;
                FitExcerpt(line);
                if (i + 1 < entries.Count)
                {
                    var rule = Panel("Event divider", card, new Color(UiTheme.Outline.r, UiTheme.Outline.g, UiTheme.Outline.b, .45f), 0);
                    Anchor(rule, new Vector2(0, 1), new Vector2(0, 1), new Vector2(12f, y - RowHeight + 3f), new Vector2(width - 24f, 1f));
                    rule.GetComponent<Image>().raycastTarget = false;
                }
            }
            return height;
        }

        /// <summary>The Recent Events card's corner link. Not "See all": that is the relationships card's.</summary>
        public const string ViewAllEventsCaption = "View all";

        /// <summary>
        /// What a corner link's white ground is painted when its column cannot be pressed: nothing.
        /// A Pressable sets no disabled colour, so under a non-interactable column - an offer's card,
        /// a story beat, the walk-in - Unity's own disabled tint painted the white panel grey at
        /// half alpha behind the accent words, and 'View all' was a grey blob in fourteen frames of
        /// the play sweep (row 9). The words carry the link; the ground only ever lights for a hover.
        /// </summary>
        private static readonly Color CornerLinkDisabled = new Color(1f, 1f, 1f, 0f);

        /// <summary>
        /// The card's corner link to the whole story (MOCKUP-PASS M3): the notebook's Story page,
        /// where every event the player may have seen is kept. Drawn as the relationships card's
        /// 'See all' is, in the 72 units the heading leaves for a corner control, and with its own
        /// words so the two captions never meet.
        /// </summary>
        private void ViewAllEventsLink(RectTransform card)
        {
            var link = Panel(ViewAllEventsCaption, card, Color.white, 6);
            Anchor(link, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-8f, -8f), new Vector2(72f, 26f));
            var button = Pressable(link, () => director.ShowNotebookSection(EpisodeDirector.NotebookSection.Story));
            var colours = button.colors;
            colours.normalColor = new Color(1f, 1f, 1f, 0f);
            colours.highlightedColor = new Color(UiTheme.SurfaceRaised.r, UiTheme.SurfaceRaised.g, UiTheme.SurfaceRaised.b, .9f);
            colours.selectedColor = colours.highlightedColor;
            colours.disabledColor = CornerLinkDisabled;
            button.colors = colours;
            var navigation = button.navigation; navigation.mode = Navigation.Mode.None; button.navigation = navigation;
            // A letter longer than 'See all', so the box is wider: 52 still clears the chevron, and
            // 20 tall keeps it over 1.3 times the words at the larger text size.
            var words = FixedText(link, ViewAllEventsCaption, 12, Accent, new Vector2(4f, -3f), new Vector2(52f, 20f));
            words.alignment = TextAlignmentOptions.Right;
            HudPrimitives.Chevron(link, Accent, 9f).anchoredPosition = new Vector2(-6f, 0f);
        }

        /// <summary>The colour a kind of event's glyph is drawn in: the block and the vote in red,
        /// deals in green, talk in the glow blue, the power in gold.</summary>
        private static Color EventTint(string kind)
        {
            if (BlockSpeeches.IsReceiptKind(kind)) return UiTheme.Glow;
            switch (kind)
            {
                case "nomination": case "final-eviction": case "eviction": case "vote-reveal": case "private-vote":
                    return UiTheme.Conflict;
                case "alliance": return UiTheme.Allied;
                case "conversation": case "eviction-speech": case "final-speech": return UiTheme.Glow;
                case "competition": case "veto": case "winner": return UiTheme.Gold;
                default: return Accent;
            }
        }

        /// <summary>
        /// The glyph a kind of event is drawn with. Decoration on top of a line that already says
        /// what happened; nothing here is the only place a fact appears.
        /// </summary>
        private static string EventGlyph(string kind)
        {
            if (BlockSpeeches.IsReceiptKind(kind)) return "chat";
            switch (kind)
            {
                case "nomination": case "final-eviction": return "target";
                case "eviction": case "vote-reveal": case "private-vote": return "gavel";
                case "veto": case "veto-selection": return "veto-token";
                case "competition": return "trophy";
                case "winner": case "jury-vote": case "jury-tie": return "crown";
                case "alliance": return "handshake";
                case "arrival": return "house";
                case "conversation": case "eviction-speech": case "final-speech": return "chat";
                default: return "journal";
            }
        }

        /// <summary>
        /// Trims a line to the two lines its box holds, on a word boundary, with an ellipsis where
        /// it was cut.
        ///
        /// <para>The accessibility suite fails any copy that clips, and an engine line has no length
        /// limit — an eviction sentence naming two people and the jury runs well past what 180 units
        /// of card can show. The box is measured, not the letters: a budget of fifty letters was two
        /// lines of some words and three of others, and the third line, ellipsis and all, was
        /// clipped behind the box's edge, so the row ended mid-word with nothing to say it went on
        /// (the play sweep's row 8). Each pass takes the words back to the last whole one before the
        /// first that did not fit, with the ellipsis measured among them, so a pass that pushes the
        /// ellipsis over the edge takes one more word; every pass is shorter, so it ends.</para>
        /// </summary>
        /// <summary>
        /// Trims a line to a budget of letters, on a word boundary, with an ellipsis where it was
        /// cut: the overview column's row of first names, on one line beside a room's count. A row
        /// of two lines is measured instead (<see cref="FitExcerpt"/>).
        /// </summary>
        private static string Excerpt(string value, int letters)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= letters) return value;
            int cut = value.LastIndexOf(' ', Mathf.Min(letters, value.Length - 1));
            if (cut < letters / 2) cut = letters;
            return value.Substring(0, cut).TrimEnd(' ', ',', ';', ':', '·') + "…";
        }

        private static void FitExcerpt(TMP_Text line)
        {
            string words = line.text ?? "";
            for (int pass = 0; pass < 12 && words.Length > 1; pass++)
            {
                line.ForceMeshUpdate(true);
                if (!line.isTextOverflowing) return;
                string kept = words.EndsWith("…", System.StringComparison.Ordinal) ? words.Substring(0, words.Length - 1) : words;
                int fits = Mathf.Clamp(line.firstOverflowCharacterIndex, 0, kept.Length);
                // Back to the last whole word before the cut, and always at least a letter shorter.
                int cut = fits > 1 ? kept.LastIndexOf(' ', fits - 1) : -1;
                if (cut < 1) cut = Mathf.Max(1, fits - 1);
                if (cut >= kept.Length) cut = kept.Length - 1;
                words = kept.Substring(0, cut).TrimEnd(' ', ',', ';', ':', '·') + "…";
                line.text = words;
            }
        }
    }
}
