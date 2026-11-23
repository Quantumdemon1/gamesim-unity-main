using System;

namespace Gamesim.Presentation
{
    /// <summary>
    /// How the house screen's fixed chrome follows the text size (U9, backlog item 23): the cast and
    /// moves menus' rows, the Listen-in and story-pull buttons, the two boards' talk buttons and the
    /// ways out on the strategy stage.
    ///
    /// <para>Plain C#, so the Unity-free suite checks the same numbers the screens draw with. A
    /// control's words are drawn at their resting size times the text scale, rounded as the HUD
    /// rounds every scaled size; a fixed button grows only where its label box would no longer hold
    /// 1.3 times those words; at the standard size nothing moves.</para>
    /// </summary>
    public static class TextSizeFit
    {
        /// <summary>A label box is at least this many times its font (the Inter line needs 1.21).</summary>
        public const float LabelBoxRatio = 1.3f;

        /// <summary>The words' inset inside a fixed button, top and bottom together (EpisodeHud.FinishButton: 5 and 5).</summary>
        public const float ButtonInsets = 10f;

        /// <summary>The resting height of the side cards' buttons (Listen in, the Pull's answers) and of the boards' talk buttons.</summary>
        public const float SideButtonHeight = 36f;

        /// <summary>The Nearby card's and the Pull's height at the resting size: both stand in the week card's place.</summary>
        public const float SideCardHeight = 168f;

        /// <summary>
        /// The week card whose place the Nearby card and the Pull take (EpisodeHud.Chrome: 44 + 3 x 36 + 24):
        /// the right column folds around this height, so a side card no taller never reaches lower than the column already does.
        /// </summary>
        public const float WeekCardRoom = 44f + 3f * 36f + 24f;

        /// <summary>The resting sizes of the words this slice scales: the menus' rows, the side buttons, the Pull's narrow answers.</summary>
        public const int MenuRowFont = 16, SideButtonFont = 15, PullNarrowFont = 13;

        /// <summary>Where the strategy stage's header starts, under the 62-unit phase band and a gap (EpisodeHud.StrategyStage).</summary>
        public const float StrategyBandFoot = 72f;

        /// <summary>The stage's gap between its regions, kept between a way out's foot and the header.</summary>
        public const float StrategyGap = 10f;

        /// <summary>A resting font size at a text scale, rounded to a whole size as the HUD's own text is (Mathf.RoundToInt).</summary>
        public static int Font(int restingSize, float textScale) => (int)Math.Round(restingSize * Scale(textScale));

        /// <summary>
        /// A fixed button's height: its own, or taller where its words at <paramref name="font"/> would
        /// not have 1.3 times their size inside the label's insets. Never shorter than it was.
        /// </summary>
        public static float ButtonHeight(float restingHeight, float font, float insets = ButtonInsets) =>
            Math.Max(restingHeight, (float)Math.Ceiling(font * LabelBoxRatio + insets));

        /// <summary>The side cards' button at a text scale: 36 until the words outgrow it.</summary>
        public static float SideButton(float textScale) => ButtonHeight(SideButtonHeight, Font(SideButtonFont, textScale));

        /// <summary>How much taller the side cards' button row is than at rest; everything under it moves down by as much.</summary>
        public static float SideCardGrowth(float textScale) => SideButton(textScale) - SideButtonHeight;

        /// <summary>The Nearby card's and the Pull's height at a text scale.</summary>
        public static float SideCard(float textScale) => SideCardHeight + SideCardGrowth(textScale);

        /// <summary>The least a way out may stand and still hold its words at 1.3 times their size (WayOut.Height's floor).</summary>
        public static float WayOutLabelBox(float textScale) => WayOut.FontSize(textScale) * WayOut.LabelBoxRatio + 8f;

        /// <summary>
        /// A way out's height on the strategy stage: <see cref="WayOut.Height"/>, or shorter where that
        /// would reach the header - a gap short of it - but never under its label box. At the larger and
        /// the largest text a way out stood 72 and 84 units deep, at and into the header at 72; it is
        /// as wide as anywhere, and its words as large.
        /// </summary>
        public static float StrategyWayOutHeight(float textScale) =>
            Math.Max(WayOutLabelBox(textScale), Math.Min(WayOut.Height(textScale), StrategyBandFoot - StrategyGap - WayOut.Inset(textScale)));

        /// <summary>Where a way out ends on the strategy stage, from the frame's top.</summary>
        public static float StrategyWayOutFoot(float textScale) => WayOut.Inset(textScale) + StrategyWayOutHeight(textScale);

        private static float Scale(float textScale) => textScale > 0f ? textScale : 1f;
    }
}
