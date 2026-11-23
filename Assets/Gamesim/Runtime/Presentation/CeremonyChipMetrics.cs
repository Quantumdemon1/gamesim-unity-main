namespace Gamesim.Presentation
{
    /// <summary>
    /// The one controls chip every ceremony card shows at its foot (UI-UX plan D3b), in numbers: the
    /// takeover, the key ceremony, the live eviction, the jury's vote and the veto draw each put their
    /// controls line in the same glass chip, at the same size, centred under the card. On the HUD
    /// its words are 13 points at the standard text size - the skip chip's key size, and never less
    /// (CE-U11: the key ceremony's line was 11) - grown by the large-text preference with the card;
    /// on a set's screen, read from the sofa, 26. Every label box is at least 1.3 times its type:
    /// Inter's line is 1.21 of its size, and a box short of it draws nothing.
    ///
    /// <para>The sting has no chip: it takes no press, and a chip there would name one that does
    /// nothing.</para>
    ///
    /// <para>Unity-free, so the subset holds the numbers itself.</para>
    /// </summary>
    public static class CeremonyChipMetrics
    {
        /// <summary>The chip's words on the HUD at the standard text size.</summary>
        public const float HudPt = 13f;

        /// <summary>The chip's words on a set's screen, a 1200 x 800 face read across the room.</summary>
        public const float ScreenPt = 26f;

        /// <summary>A label box this many times its type.</summary>
        public const float LineScale = 1.35f;

        /// <summary>The chip's padding either side of its words, and above and below them, on the HUD at the standard size.</summary>
        public const float PadX = 14f, PadY = 4f;

        /// <summary>The box the chip's words stand in, for type of <paramref name="pt"/> points.</summary>
        public static float Line(float pt) => (float)System.Math.Ceiling(pt * LineScale);

        /// <summary>The chip's height around words of <paramref name="pt"/> points with <paramref name="padY"/> above and below.</summary>
        public static float Height(float pt, float padY) => Line(pt) + 2f * padY;

        /// <summary>The chip's words on the HUD at the large-text preference's <paramref name="scale"/>.</summary>
        public static float HudSize(float scale) => HudPt * scale;

        /// <summary>The chip's height on the HUD at the large-text preference's <paramref name="scale"/>.</summary>
        public static float HudHeight(float scale) => Height(HudSize(scale), PadY * scale);

        /// <summary>The chip's height on a set's screen, where it stands in the frame's own row with no padding above or below.</summary>
        public static float ScreenHeight => Height(ScreenPt, 0f);
    }
}
