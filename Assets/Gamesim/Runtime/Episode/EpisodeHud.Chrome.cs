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

        /// <summary>How many events the column shows, and the longest line one of them may occupy.</summary>
        // Three, not four. The right column is 104 + 226 live feed + 12 + this card + 12 + the
        // knowledge card, and at four rows that stack reached y 808 of a 900-high canvas while the
        // controls box starts at 746 - a 62-pixel overlap that clipped the last row of whichever
        // card was unlucky. Four events was never the point; not colliding is.
        private const int RecentEventRows = 3;
        private const int RecentEventLetters = 50;

        /// <summary>
        /// The mockups' masthead: the house mark, the wordmark in the display weight, and the season
        /// as a strap beneath it - on the set itself, not in a card. The mockups float the logo, and
        /// a bordered box around it made it the heaviest panel in the bar.
        /// </summary>
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

            var strap = FixedText(brand, "THE HOUSE  |  " + (state == null ? "A SEASON" : state.contestants.Count + "-PERSON SEASON"),
                11, UiTheme.Muted, new Vector2(left + 1f, -36f), new Vector2(BrandWidth - left - 1f, 17f));
            var medium = UiTheme.Font(UiTheme.Weight.Medium);
            if (medium != null) strap.font = medium;
            strap.characterSpacing = 2f;
            AutoSize(strap, 9);
        }

        /// <summary>
        /// Where the player is being sent next, in the words the HUD has always used for it.
        /// </summary>
        private static string NextStop(EpisodeState state) =>
            state.pendingDiary != null ? "Next stop: private diary room"
            : EpisodeEngine.IsCompetition(state.phase) ? "Next stop: competition yard"
            : "Next stop: ceremony screen";

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
            var ring = UiTheme.Pack(PackArt.SelectionRing);
            if (ring != null)
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

            var title = FixedText(objective, "Current Objective", 14, UiTheme.Heading,
                new Vector2(words, -7f), new Vector2(ObjectiveChipWidth - words - 12f, 19f));
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            if (semibold != null) title.font = semibold;
            var next = FixedText(objective, NextStop(state), 14, Paper,
                new Vector2(words, -27f), new Vector2(ObjectiveChipWidth - words - 12f, 20f));
            AutoSize(next, 11);
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
            CardHeading(objective, "Current Objective");
            FixedText(objective, NextStop(state), 18, Paper, new Vector2(18f, -44f), new Vector2(294f, 48f));
            ActionChip(objective, state, -94f);
            FixedButton(objective,"Go to episode screen",new Vector2(18,-126),new Vector2(294,44),director.GoToStation);
            FixedButton(objective,DiaryTravelCaption,new Vector2(18,-178),new Vector2(294,44),director.GoToDiary).interactable=
                director.HasDiaryRoom && !recovery && state.Find(state.playerId)?.status==ContestantStatus.Active;
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
            var week = FixedText(chip, "WEEK " + state.week, 17, Paper, new Vector2(x, -14f), new Vector2(84f, 24f));
            var semibold = UiTheme.Font(UiTheme.Weight.SemiBold);
            if (semibold != null) week.font = semibold;

            var rule = Panel("Week divider", chip, new Color(UiTheme.Outline.r, UiTheme.Outline.g, UiTheme.Outline.b, .8f), 0);
            Anchor(rule, new Vector2(0, 1), new Vector2(0, 1), new Vector2(x + 88f, -12f), new Vector2(1f, 28f));
            rule.GetComponent<Image>().raycastTarget = false;
            var dot = HudPrimitives.Disc("Phase dot", chip, PhaseTint(state.phase));
            Anchor(dot, new Vector2(0, 1), new Vector2(0, 1), new Vector2(x + 100f, -21f), new Vector2(10f, 10f));
            dot.GetComponent<Image>().raycastTarget = false;
            var phase = FixedText(chip, PhaseShort(state.phase), 15, Paper, new Vector2(x + 116f, -14f),
                new Vector2(WeekWidth - x - 126f, 24f));
            var medium = UiTheme.Font(UiTheme.Weight.Medium);
            if (medium != null) phase.font = medium;
            AutoSize(phase, 11);
        }

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
            StatCell(pill, 0f, "people", Accent, state.Active.Count() + "/" + state.contestants.Count, "Houseguests");
            StatCell(pill, PillCell, "star", left == 0 ? UiTheme.Muted : UiTheme.Joke, left.ToString(), "Actions left");
            // The first name only: a full name does not fit a cell, and the pill is a glance.
            StatCell(pill, 2f * PillCell, "crown", UiTheme.Gold, holder == null ? "Awaiting" : holder.name.Split(' ')[0], "HoH");
            for (int i = 1; i < 3; i++)
            {
                var rule = Panel("Stat divider", pill, new Color(UiTheme.Outline.r, UiTheme.Outline.g, UiTheme.Outline.b, .8f), 0);
                Anchor(rule, new Vector2(0, 1), new Vector2(0, 1), new Vector2(i * PillCell, -11f), new Vector2(1f, 30f));
                rule.GetComponent<Image>().raycastTarget = false;
            }
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
        /// and settings - with a hairline between the lists.
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
                RailDivider(y);
                var next = RailGroup("Next move", y, 2);
                RailButton(next, "Go to episode screen", UiTheme.Icon("camera"), 0, director.GoToStation, true);
                RailButton(next, DiaryTravelCaption, UiTheme.Icon("chat"), 1, director.GoToDiary).interactable =
                    director.HasDiaryRoom && !recovery && state.Find(state.playerId)?.status == ContestantStatus.Active;
                y += next.sizeDelta.y;
            }

            RailDivider(y);
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

        private void RailDivider(float y)
        {
            var line = Panel("Rail divider", canvas.transform, new Color(UiTheme.Outline.r, UiTheme.Outline.g, UiTheme.Outline.b, .7f), 0);
            Anchor(line, new Vector2(0, 1), new Vector2(0, 1), new Vector2(26f, -y), new Vector2(IconRail.Width - 24f, 1f));
            line.GetComponent<Image>().raycastTarget = false;
        }

        private Button RailButton(RectTransform group, string caption, Sprite icon, int index, System.Action action,
            bool primary = false) =>
            IconRail.Row(group, caption, icon, null, false,
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
            HouseVibeCard(canvas.transform, top, state);
            if (director.CanListenIn) NearbyCard(canvas.transform, top);
        }

        /// <summary>The Nearby card's name, and its control's caption.</summary>
        public const string NearbyCardName = "Nearby card";
        public const string ListenInCaption = "Listen in";
        private RectTransform nearbyCard;

        /// <summary>
        /// Mockup-06's Nearby card: two houseguests are talking where the player can see them, and
        /// listening in is on offer - the house's own Eavesdrop, with its real cost and odds. Built
        /// hidden in the week card's place and swapped in by <see cref="SetNearby"/> while a
        /// conversation is being witnessed. It names nobody: what is overheard is the house's to
        /// decide, not the pair's on screen.
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

        /// <summary>Swaps the Nearby card in for the week card while a conversation is witnessed.</summary>
        public void SetNearby(bool visible)
        {
            if (nearbyCard == null || nearbyCard.gameObject.activeSelf == visible) return;
            nearbyCard.gameObject.SetActive(visible);
            var week = nearbyCard.parent != null ? nearbyCard.parent.Find(HouseVibeCardName) : null;
            if (week != null) week.gameObject.SetActive(!visible);
            if (nearbyBar != null)
            {
                nearbyBar.gameObject.SetActive(visible);
                var status = nearbyBar.parent.Find("Status");
                if (status != null) status.gameObject.SetActive(!visible);
            }
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
                // "Week 1 · Nomination" where the nomination itself should have been.
                .Where(entry => entry.kind != "phase")
                .Reverse()
                .Take(RecentEventRows)
                .ToList();

            const float RowHeight = 58f;
            float width = RightColumnWidth;
            float height = 42f + Mathf.Max(1, entries.Count) * RowHeight + 2f;
            var card = Chrome(RecentEventsCardName, parent);
            Anchor(card, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-RightColumnInset, -top),
                new Vector2(width, height));
            CardHeading(card, "Recent Events");

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
                FixedText(card, Excerpt(entry.text, RecentEventLetters), 13, Paper,
                    new Vector2(text, y - 15f), new Vector2(width - text - 12f, 36f));
                if (i + 1 < entries.Count)
                {
                    var rule = Panel("Event divider", card, new Color(UiTheme.Outline.r, UiTheme.Outline.g, UiTheme.Outline.b, .45f), 0);
                    Anchor(rule, new Vector2(0, 1), new Vector2(0, 1), new Vector2(12f, y - RowHeight + 3f), new Vector2(width - 24f, 1f));
                    rule.GetComponent<Image>().raycastTarget = false;
                }
            }
            return height;
        }

        /// <summary>The colour a kind of event's glyph is drawn in: the block and the vote in red,
        /// deals in green, talk in the glow blue, the power in gold.</summary>
        private static Color EventTint(string kind)
        {
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
        /// Trims a line to what a two-line card row holds, on a word boundary.
        ///
        /// <para>The accessibility suite fails any copy that clips, and an engine line has no length
        /// limit — an eviction sentence naming two people and the jury runs well past what 286 px of
        /// card can show. Cutting it here is the difference between a column that ends in an ellipsis
        /// and one that ends mid-word behind an invisible edge.</para>
        /// </summary>
        private static string Excerpt(string value, int letters)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= letters) return value;
            int cut = value.LastIndexOf(' ', Mathf.Min(letters, value.Length - 1));
            if (cut < letters / 2) cut = letters;
            return value.Substring(0, cut).TrimEnd(' ', ',', ';', ':', '·') + "…";
        }
    }
}
