using System.Linq;
using Gamesim.Presentation;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// The settings' three text sizes as the layout suites walk them, smallest first (PLAN A, A8; the
    /// lead's decision A-3): 1, 1.2 and 1.4. A screen's own suite lays it out at each, so a size added
    /// to the settings is a size every screen is held to.
    /// </summary>
    internal static class TextScales
    {
        /// <summary>Each size's scale: 1, 1.2, 1.4.</summary>
        public static readonly float[] All = TextSizes.All.Select(TextSizes.Scale).ToArray();

        /// <summary>
        /// Each size with whether it is above the standard - the "larger" the two-size suites were
        /// written with, which the largest is too: a row that becomes a list at the larger text is a
        /// list at the largest.
        /// </summary>
        public static readonly (TextSize Size, bool Larger)[] Steps = TextSizes.All.Select(size => (size, size != TextSize.Standard)).ToArray();

        /// <summary>A frame's name for the size: nothing at the standard, "-large" and "-largest" above it, as the frames have been named.</summary>
        public static string Suffix(TextSize size) => size == TextSize.Largest ? "-largest" : size == TextSize.Larger ? "-large" : "";

        /// <summary>A frame's name for a scale, as <see cref="Suffix(TextSize)"/>.</summary>
        public static string Suffix(float scale) => scale > 1.3f ? "-largest" : scale > 1f ? "-large" : "";

        /// <summary>The size in a message's words: "at the standard text", "at the larger text", "at the largest text".</summary>
        public static string At(TextSize size) => "at the " + TextSizes.Name(size) + " text";
    }
}
