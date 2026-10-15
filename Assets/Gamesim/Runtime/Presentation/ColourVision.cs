using System;
using System.Globalization;

namespace Gamesim.Presentation
{
    /// <summary>
    /// How a colour reads to a player with a colour-vision deficiency (PLAN A, A9), and how far apart
    /// two colours are to them: the arithmetic the colour-blind safety tests hold the HUD's tints to.
    ///
    /// <para>The simulation is Machado, Oliveira and Fernandes (2009), "A Physiologically-based Model
    /// for Simulation of Color Vision Deficiency", at severity 1.0 - full protanopia, deuteranopia
    /// and tritanopia - applied to linear RGB, as the paper's matrices are. The matrices below are the
    /// paper's table for severity 1.0, each row giving R', G' and B'; every row sums to 1, so a grey
    /// stays the grey it was. The distance is CIE76 (Euclidean in CIELAB, D65 white) and the WCAG
    /// relative-luminance ratio, so a pair that differs only in lightness is caught as apart.</para>
    ///
    /// <para>Unity-free (doubles, no UnityEngine), so the subset checks the arithmetic; the editor
    /// test reads the theme's tints and hands their channels in.</para>
    /// </summary>
    public static class ColourVision
    {
        public enum Vision { Normal, Protan, Deutan, Tritan }

        /// <summary>The three deficiencies every colour-only cue is held to.</summary>
        public static readonly Vision[] Deficiencies = { Vision.Protan, Vision.Deutan, Vision.Tritan };

        /// <summary>The lead's decision 16: two tints are apart when CIE76 says 20 or more, or their luminances stand 1.5 to 1.</summary>
        public const double DeltaEFloor = 20d, LuminanceRatioFloor = 1.5d;

        /// <summary>A colour's three channels, each 0 to 1: sRGB or linear as the method says.</summary>
        public readonly struct Rgb
        {
            public readonly double R, G, B;
            public Rgb(double r, double g, double b) { R = r; G = g; B = b; }
            public override string ToString() => string.Format(CultureInfo.InvariantCulture, "({0:0.###}, {1:0.###}, {2:0.###})", R, G, B);
        }

        private static readonly double[,] Protan =
        {
            { 0.152286, 1.052583, -0.204868 },
            { 0.114503, 0.786281, 0.099216 },
            { -0.003882, -0.048116, 1.051998 },
        };

        private static readonly double[,] Deutan =
        {
            { 0.367322, 0.860646, -0.227968 },
            { 0.280085, 0.672501, 0.047413 },
            { -0.011820, 0.042940, 0.968881 },
        };

        private static readonly double[,] Tritan =
        {
            { 1.255528, -0.076749, -0.178779 },
            { -0.078411, 0.930809, 0.147602 },
            { 0.004733, 0.691367, 0.303900 },
        };

        /// <summary>An sRGB colour from six hex digits, "4ADE80" (a leading # and a trailing alpha pair are ignored).</summary>
        public static Rgb FromHex(string hex)
        {
            string digits = hex.TrimStart('#');
            if (digits.Length < 6) throw new ArgumentException("Six hex digits make a colour.", nameof(hex));
            double Channel(int at) => int.Parse(digits.Substring(at, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255d;
            return new Rgb(Channel(0), Channel(2), Channel(4));
        }

        /// <summary>One sRGB channel to linear light.</summary>
        public static double ToLinear(double channel) =>
            channel <= 0.04045 ? channel / 12.92 : Math.Pow((channel + 0.055) / 1.055, 2.4);

        public static Rgb ToLinear(Rgb srgb) => new Rgb(ToLinear(srgb.R), ToLinear(srgb.G), ToLinear(srgb.B));

        /// <summary>
        /// <paramref name="top"/> at <paramref name="alpha"/> over <paramref name="under"/>, both sRGB, blended
        /// in sRGB as the HUD's contrast tests composite a tint over its ground.
        /// </summary>
        public static Rgb Over(Rgb top, double alpha, Rgb under) =>
            new Rgb(alpha * top.R + (1 - alpha) * under.R, alpha * top.G + (1 - alpha) * under.G, alpha * top.B + (1 - alpha) * under.B);

        /// <summary>A linear colour as a player with <paramref name="vision"/> sees it, in linear RGB, clamped to the gamut.</summary>
        public static Rgb Simulate(Vision vision, Rgb linear)
        {
            var matrix = vision == Vision.Protan ? Protan : vision == Vision.Deutan ? Deutan : vision == Vision.Tritan ? Tritan : null;
            if (matrix == null) return linear;
            double Row(int row) => Clamp(matrix[row, 0] * linear.R + matrix[row, 1] * linear.G + matrix[row, 2] * linear.B);
            return new Rgb(Row(0), Row(1), Row(2));
        }

        /// <summary>An sRGB colour as a player with <paramref name="vision"/> sees it, in linear RGB.</summary>
        public static Rgb SeenAs(Vision vision, Rgb srgb) => Simulate(vision, ToLinear(srgb));

        /// <summary>WCAG relative luminance of a linear colour.</summary>
        public static double Luminance(Rgb linear) => 0.2126 * linear.R + 0.7152 * linear.G + 0.0722 * linear.B;

        /// <summary>The WCAG contrast ratio of two linear colours, the lighter over the darker: 1 to 21.</summary>
        public static double LuminanceRatio(Rgb a, Rgb b)
        {
            double la = Luminance(a) + 0.05, lb = Luminance(b) + 0.05;
            return Math.Max(la, lb) / Math.Min(la, lb);
        }

        /// <summary>CIELAB (D65) of a linear colour: L, a and b.</summary>
        public static (double L, double A, double B) Lab(Rgb linear)
        {
            double x = 0.4124564 * linear.R + 0.3575761 * linear.G + 0.1804375 * linear.B;
            double y = 0.2126729 * linear.R + 0.7151522 * linear.G + 0.0721750 * linear.B;
            double z = 0.0193339 * linear.R + 0.1191920 * linear.G + 0.9503041 * linear.B;
            double fx = F(x / 0.95047), fy = F(y / 1.0), fz = F(z / 1.08883);
            return (116 * fy - 16, 500 * (fx - fy), 200 * (fy - fz));
        }

        /// <summary>CIE76: the straight-line distance between two linear colours in CIELAB.</summary>
        public static double DeltaE76(Rgb a, Rgb b)
        {
            var la = Lab(a);
            var lb = Lab(b);
            double dl = la.L - lb.L, da = la.A - lb.A, db = la.B - lb.B;
            return Math.Sqrt(dl * dl + da * da + db * db);
        }

        /// <summary>Whether two linear colours are told apart by the lead's thresholds: CIE76 at least 20, or luminance 1.5 to 1.</summary>
        public static bool Apart(Rgb a, Rgb b) => DeltaE76(a, b) >= DeltaEFloor || LuminanceRatio(a, b) >= LuminanceRatioFloor;

        private static double F(double t)
        {
            const double delta = 6d / 29d;
            return t > delta * delta * delta ? Math.Pow(t, 1d / 3d) : t / (3 * delta * delta) + 4d / 29d;
        }

        private static double Clamp(double value) => value < 0 ? 0 : value > 1 ? 1 : value;
    }
}
