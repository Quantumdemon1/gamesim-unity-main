namespace Gamesim.Presentation
{
    /// <summary>
    /// The shape of the house screen's This Week card (the menu review's change 7, backlog item 32):
    /// the right column's one card under the live feed. Its first section is the week's latest
    /// events, its second, under a rule, the house vibe as three slim rows and the line under them.
    /// Recent Events and the vibe were two cards that answered the same question.
    ///
    /// <para>Plain C#, so the Unity-free suite checks the numbers the HUD draws with
    /// (EpisodeHud.Chrome: ThisWeekCard). Every measure is from the card's top. The two sections
    /// keep the names the two cards had, so a lookup by name still finds each.</para>
    ///
    /// <para>The Nearby card and a story's Pull still take the vibe's place, as they took the vibe
    /// card's: the events stay up, the card ends under them, and the borrowing card stands a column
    /// gap below. The right column folds around that place (EpisodeHud.FoldColumn reckons a gap and
    /// <see cref="TextSizeFit.WeekCardRoom"/> under the events), so the card, lent or not, never
    /// reaches lower than the column already reckons.</para>
    /// </summary>
    public static class ThisWeekShape
    {
        /// <summary>The gap between two cards of the right column (EpisodeHud.RightColumnGap).</summary>
        public const float ColumnGap = 12f;

        /// <summary>Where the first event row starts, under the card's heading.</summary>
        public const float EventsTop = 42f;

        /// <summary>One event row: when it happened and two lines of what happened.</summary>
        public const float EventRowHeight = 58f;

        /// <summary>The events section's margin under its last row.</summary>
        public const float EventsFoot = 2f;

        /// <summary>
        /// The space between the events section and the vibe section. Never none: the two are
        /// checked apart for overlap, and two rects that only touch can read as overlapping by a
        /// rounding error on screen.
        /// </summary>
        public const float SectionGap = 2f;

        /// <summary>Where the vibe section starts on a card that holds no events (somebody in focus): under the heading.</summary>
        public const float VibeOnlyTop = 34f;

        /// <summary>The vibe section's rows: the first under the rule, each a glyph, its words, a thin bar and the count.</summary>
        public const float VibeRowTop = 8f, VibeRowHeight = 22f;
        public const int VibeRows = 3;

        /// <summary>A vibe row's words and count, and the line under the rows: their sizes and their boxes' heights.</summary>
        public const int VibeWordFont = 12, TensionFont = 11;
        public const float VibeWordBox = 18f, TensionBox = 16f;

        /// <summary>The space between the last row and the line under them, and the margin under that line.</summary>
        public const float TensionGap = 2f, VibeFoot = 8f;

        /// <summary>Where the line under the rows starts, from the vibe section's top.</summary>
        public const float TensionTop = VibeRowTop + VibeRows * VibeRowHeight + TensionGap;

        /// <summary>The vibe section's height: its rows, the line under them and a margin.</summary>
        public const float VibeHeight = TensionTop + TensionBox + VibeFoot;

        /// <summary>The events section's height for this many rows: its heading's band alone at none.</summary>
        public static float EventsHeight(int rows) => EventsTop + (rows < 0 ? 0 : rows) * EventRowHeight + EventsFoot;

        /// <summary>Where the vibe section starts: under the events and the section gap, or under the heading when the card holds no events.</summary>
        public static float VibeTop(bool events, int rows) => events ? EventsHeight(rows) + SectionGap : VibeOnlyTop;

        /// <summary>The card's height: it ends where the vibe section ends.</summary>
        public static float Height(bool events, int rows) => VibeTop(events, rows) + VibeHeight;

        /// <summary>The card's height while the Nearby card or the Pull has the vibe's place: its events alone.</summary>
        public static float LentHeight(int rows) => EventsHeight(rows);

        /// <summary>Where the Nearby card and the Pull stand on a card that holds events: a column gap under them.</summary>
        public static float BorrowerTop(int rows) => EventsHeight(rows) + ColumnGap;
    }
}
