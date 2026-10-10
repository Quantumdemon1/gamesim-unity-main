using System;

namespace Gamesim.Presentation
{
    /// <summary>The caption reading time the settings offer (PLAN A, A10).</summary>
    public enum CaptionTime
    {
        Standard = 0,
        Longer = 1,
    }

    /// <summary>
    /// How long a caption the game takes down by itself stays up (PLAN A, A10): the ceremony's strip
    /// and the line over a conversation the player walked in on. Each holds for what it was authored
    /// to hold or for a reading time - 1.2 seconds and 0.3 more a word - whichever is longer, and the
    /// "Longer" setting gives every one of them half as long again. The reveals the player paces -
    /// the key ceremony, the vote, the jury, every card the player skips or speeds - are not on a
    /// timer of this kind and are not touched (CeremonyPacing); nothing here changes an outcome.
    ///
    /// <para>Unity-free, so the subset holds the arithmetic itself.</para>
    /// </summary>
    public static class CaptionTiming
    {
        /// <summary>The reading time of a caption with no words, and what each word adds, in seconds.</summary>
        public const double Floor = 1.2, PerWord = 0.3;

        /// <summary>How much longer every caption holds under the "Longer" setting.</summary>
        public const double LongerFactor = 1.5;

        /// <summary>The preference the setting is kept under: 0 standard, 1 longer.</summary>
        public const string Key = "Gamesim.CaptionTime";

        /// <summary>The settings' control for each setting: the setting's name in it, and a press moves to the other.</summary>
        public const string StandardCaption = "Caption time: Standard  (change)";
        public const string LongerCaption = "Caption time: Longer  (change)";

        /// <summary>The words a reader reads: every run of characters between whitespace.</summary>
        public static int Words(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            int words = 0;
            bool inWord = false;
            foreach (char character in text)
            {
                bool space = char.IsWhiteSpace(character);
                if (!space && !inWord) words++;
                inWord = !space;
            }
            return words;
        }

        /// <summary>The time to read <paramref name="text"/>: 1.2 seconds and 0.3 a word.</summary>
        public static double ReadingTime(string text) => Floor + PerWord * Words(text);

        /// <summary>The setting's multiplier: 1, or 1.5 for "Longer".</summary>
        public static double Factor(CaptionTime time) => time == CaptionTime.Longer ? LongerFactor : 1.0;

        /// <summary>
        /// How long a caption holds: what it was authored to hold or its reading time, whichever is
        /// longer, at the setting's multiplier. An authored hold below nothing counts as nothing.
        /// </summary>
        public static double Hold(double authored, string text, CaptionTime time) =>
            Math.Max(Math.Max(0.0, authored), ReadingTime(text)) * Factor(time);

        /// <summary>The setting's word in the player log's settings lines.</summary>
        public static string Name(CaptionTime time) => time == CaptionTime.Longer ? "longer" : "standard";

        /// <summary>The settings' control with this setting in use.</summary>
        public static string Caption(CaptionTime time) => time == CaptionTime.Longer ? LongerCaption : StandardCaption;

        /// <summary>The setting a store's record names: 1 is longer, anything else standard.</summary>
        public static CaptionTime FromRecord(int record) => record == 1 ? CaptionTime.Longer : CaptionTime.Standard;
    }
}
