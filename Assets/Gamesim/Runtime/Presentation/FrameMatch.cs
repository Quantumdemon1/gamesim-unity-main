namespace Gamesim.Presentation
{
    /// <summary>
    /// How a screen canvas meets a frame wider than 16:9 (PLAN A, A12b; the lead's decision 14).
    ///
    /// <para>Every screen-sized canvas in the game is laid out for a 16:9 reference - 1600x900 for
    /// the house's HUD and its cards, 1920x1080 for the front door and the report - and matches width
    /// and height half and half. On 16:9 and on anything narrower (16:10, 4:3) that is the canvas
    /// every layout was made and measured on, and it stays exactly so. A 21:9 frame (2560x1080) made
    /// the same canvas 1848x779, shorter than anything was laid out for, and the house's chrome
    /// collided there: the controls card over the vibe card, the left column over the cast strip
    /// (the 21:9 measurement the fix lane logged). Wider than 16:9 the canvas matches height alone,
    /// so it keeps the reference's height and the extra width is room at the sides.</para>
    ///
    /// <para>Unity-free, so the subset holds the rule itself; <see cref="FrameScaler"/> applies it
    /// to every canvas, and the three layouts that work their frame out by hand ask it too.</para>
    /// </summary>
    public static class FrameMatch
    {
        /// <summary>The match a frame wider than 16:9 is laid out at: the height alone.</summary>
        public const float WideMatch = 1f;

        /// <summary>
        /// How much wider than 16:9 a frame must be before it counts as wide: one part in a hundred,
        /// so a frame a pixel or two off 16:9 (1366x768 is 1.0004 of it) keeps the canvas it always had.
        /// </summary>
        public const double Tolerance = 1.01;

        /// <summary>Whether a frame of this many pixels is wider than 16:9, by more than <see cref="Tolerance"/>.</summary>
        public static bool IsWide(double width, double height) =>
            width > 0 && height > 0 && width * 9.0 > height * 16.0 * Tolerance;

        /// <summary>The match a canvas whose own is <paramref name="own"/> lays a frame of this size out at.</summary>
        public static float For(double width, double height, float own) => IsWide(width, height) ? WideMatch : own;
    }
}
