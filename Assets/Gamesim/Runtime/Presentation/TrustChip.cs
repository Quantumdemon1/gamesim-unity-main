using System;
using System.Globalization;

namespace Gamesim.Presentation
{
    /// <summary>
    /// Your trust in a houseguest, drawn ONE way on every reading page (menu slice 3, the review's
    /// "Trust is shown five ways. Use one trust chip."; the lead's ruling N6 iii).
    ///
    /// <para>The value is the one the houseguests' table always printed: your character's committed
    /// score toward them. Its bands are the table's too - above zero, zero, below zero, by the whole
    /// figure - so the chip says what the table said and nothing more: no band name is derived from
    /// the number. State is never colour alone: the figure carries its sign, and a shape stands beside
    /// it (a mark pointing up, a level bar, a mark pointing down); where the page does not already
    /// say what the figure is, the chip says "Your trust" in words.</para>
    ///
    /// <para>This half is plain C#, so the Unity-free suite checks the bands and the words. The
    /// builder that draws the chip is <c>TrustChip.Build.cs</c>.</para>
    /// </summary>
    public static partial class TrustChip
    {
        /// <summary>The chip's three bands: the whole figure's sign.</summary>
        public enum Band { Below = -1, Even = 0, Above = 1 }

        /// <summary>What the figure is, in words, where the page does not already say it.</summary>
        public const string Label = "Your trust";

        /// <summary>The objects a chip is made of, named so a test or a reader can find each.</summary>
        public const string ChipName = "Trust chip", FigureName = "Trust", LabelName = "Trust label", ShapeName = "Trust shape";

        /// <summary>The score as the chip counts it: whole, a half rounded away from zero.</summary>
        public static double Whole(double score) => Math.Round(score, MidpointRounding.AwayFromZero);

        /// <summary>Which band a score is in, by its whole figure.</summary>
        public static Band BandOf(double score)
        {
            double whole = Whole(score);
            return whole > 0 ? Band.Above : whole < 0 ? Band.Below : Band.Even;
        }

        /// <summary>The figure, whole and signed: "+12", "-7", "0". Never "-0".</summary>
        public static string Figure(double score)
        {
            double whole = Whole(score);
            if (whole > 0) return "+" + whole.ToString("0", CultureInfo.InvariantCulture);
            if (whole < 0) return "-" + (-whole).ToString("0", CultureInfo.InvariantCulture);
            return "0";
        }

        /// <summary>The shape beside the figure, in words: what a screen reader says for it.</summary>
        public static string ShapeWords(Band band)
        {
            switch (band)
            {
                case Band.Above: return "above zero";
                case Band.Below: return "below zero";
                default: return "at zero";
            }
        }

        /// <summary>The whole chip read aloud: "Your trust +12, above zero".</summary>
        public static string Spoken(double score) => Label + " " + Figure(score) + ", " + ShapeWords(BandOf(score));
    }
}
