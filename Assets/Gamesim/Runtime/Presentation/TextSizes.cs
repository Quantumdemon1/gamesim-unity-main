using System.Collections.Generic;

namespace Gamesim.Presentation
{
    /// <summary>The text sizes the settings offer (PLAN A, A8; the lead's decision A-3).</summary>
    public enum TextSize
    {
        Standard = 0,
        Larger = 1,
        Largest = 2,
    }

    /// <summary>
    /// The three text sizes: their scales, their names in the log, their controls' captions, and the
    /// preference that keeps the choice (PLAN A, A8). "Use larger text" and "Use standard text" are
    /// contracts - the verifier, the look sheet and the accessibility suites press them by those
    /// words - so each size keeps a control of its own named for it, and the settings show the two
    /// sizes not in use: the larger text's control still reads "Use larger text" wherever it is
    /// offered, and pressing it gives the larger text from either side.
    ///
    /// <para>The choice is kept as <see cref="SizeKey"/>, 0 to 2. The boolean it replaces,
    /// <see cref="LegacyKey"/>, is read when the new key is absent - a player's larger text from an
    /// earlier build carries over - and is still written, so a build from before the third size
    /// reads the nearest it knows.</para>
    ///
    /// <para>Unity-free, so the subset holds the table and the migration itself.</para>
    /// </summary>
    public static class TextSizes
    {
        public const string StandardCaption = "Use standard text";
        public const string LargerCaption = "Use larger text";
        public const string LargestCaption = "Use largest text";

        /// <summary>The preference the size is kept under, and the one before it.</summary>
        public const string SizeKey = "Gamesim.TextSize", LegacyKey = "Gamesim.LargeText";

        /// <summary>Every size, smallest first.</summary>
        public static readonly TextSize[] All = { TextSize.Standard, TextSize.Larger, TextSize.Largest };

        /// <summary>How much the copy grows: 1, 1.2 and 1.4.</summary>
        public static float Scale(TextSize size) => size == TextSize.Largest ? 1.4f : size == TextSize.Larger ? 1.2f : 1f;

        /// <summary>The size's word in the player log's settings lines.</summary>
        public static string Name(TextSize size) => size == TextSize.Largest ? "largest" : size == TextSize.Larger ? "larger" : "standard";

        /// <summary>The control that chooses the size.</summary>
        public static string Caption(TextSize size) => size == TextSize.Largest ? LargestCaption : size == TextSize.Larger ? LargerCaption : StandardCaption;

        /// <summary>The settings' controls with this size in use: the other two, smallest first.</summary>
        public static List<TextSize> Others(TextSize current)
        {
            var others = new List<TextSize>();
            foreach (var size in All) if (size != current) others.Add(size);
            return others;
        }

        /// <summary>
        /// The size a store's record names: <paramref name="sizeRecord"/> (the new key's value, or
        /// null when it is absent) when it is one of the three, otherwise the old boolean
        /// (<paramref name="legacyRecord"/>, null when absent) - 1 is the larger text - and otherwise
        /// the standard size.
        /// </summary>
        public static TextSize FromRecord(int? sizeRecord, int? legacyRecord)
        {
            if (sizeRecord.HasValue && sizeRecord.Value >= 0 && sizeRecord.Value <= (int)TextSize.Largest) return (TextSize)sizeRecord.Value;
            return legacyRecord == 1 ? TextSize.Larger : TextSize.Standard;
        }

        /// <summary>What the old boolean says for this size: 1 for either of the larger two.</summary>
        public static int LegacyRecord(TextSize size) => size == TextSize.Standard ? 0 : 1;
    }
}
