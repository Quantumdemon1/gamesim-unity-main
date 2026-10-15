using Gamesim.Presentation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The colour-vision arithmetic on its own (PLAN A, A9): sRGB to linear, the Machado 2009
    /// matrices at severity 1, luminance, CIELAB and CIE76, each against values worked by hand, and
    /// greys that stay grey under every deficiency. Unity-free; <see cref="ColourVisionTests"/>
    /// holds the HUD's tints to it in the editor.
    /// </summary>
    public sealed class ColourVisionMathTests
    {
        private const double Tight = 1e-6;

        [Test]
        public void SrgbToLinear_FollowsTheStandardCurve()
        {
            Assert.That(ColourVision.ToLinear(0d), Is.EqualTo(0d));
            Assert.That(ColourVision.ToLinear(1d), Is.EqualTo(1d).Within(Tight));
            Assert.That(ColourVision.ToLinear(0.04045), Is.EqualTo(0.04045 / 12.92).Within(Tight), "The linear toe.");
            Assert.That(ColourVision.ToLinear(0.5), Is.EqualTo(0.2140411).Within(1e-6));
            Assert.That(ColourVision.ToLinear(ColourVision.FromHex("#808080")).R, Is.EqualTo(0.2158605).Within(1e-6));
            var hex = ColourVision.FromHex("4ADE80FA");
            Assert.That(new[] { hex.R, hex.G, hex.B }, Is.EqualTo(new[] { 74 / 255d, 222 / 255d, 128 / 255d }), "Six digits; an alpha pair is ignored.");
        }

        [Test]
        public void Luminance_AndItsRatio_AreWcags()
        {
            var white = new ColourVision.Rgb(1, 1, 1);
            var black = new ColourVision.Rgb(0, 0, 0);
            Assert.That(ColourVision.Luminance(white), Is.EqualTo(1d).Within(Tight));
            Assert.That(ColourVision.Luminance(black), Is.EqualTo(0d));
            Assert.That(ColourVision.LuminanceRatio(white, black), Is.EqualTo(21d).Within(Tight));
            Assert.That(ColourVision.LuminanceRatio(black, white), Is.EqualTo(21d).Within(Tight), "Lighter over darker, either way round.");
            Assert.That(ColourVision.Luminance(new ColourVision.Rgb(0, 1, 0)), Is.EqualTo(0.7152).Within(Tight));
        }

        [Test]
        public void Lab_AndCie76_MeetTheirReferencePoints()
        {
            var white = ColourVision.Lab(new ColourVision.Rgb(1, 1, 1));
            Assert.That(white.L, Is.EqualTo(100d).Within(1e-3));
            Assert.That(white.A, Is.EqualTo(0d).Within(1e-2));
            Assert.That(white.B, Is.EqualTo(0d).Within(1e-2));
            var black = ColourVision.Lab(new ColourVision.Rgb(0, 0, 0));
            Assert.That(black.L, Is.EqualTo(0d).Within(Tight));
            Assert.That(ColourVision.DeltaE76(new ColourVision.Rgb(1, 1, 1), new ColourVision.Rgb(0, 0, 0)), Is.EqualTo(100d).Within(1e-2));
            // sRGB red is L 53.24, a 80.09, b 67.20 in CIELAB (D65).
            var red = ColourVision.Lab(new ColourVision.Rgb(1, 0, 0));
            Assert.That(red.L, Is.EqualTo(53.24).Within(0.01));
            Assert.That(red.A, Is.EqualTo(80.09).Within(0.02));
            Assert.That(red.B, Is.EqualTo(67.20).Within(0.02));
            // Mid grey, sRGB 119: L 50.
            var grey = ColourVision.Lab(ColourVision.ToLinear(ColourVision.FromHex("777777")));
            Assert.That(grey.L, Is.EqualTo(50.03).Within(0.02));
        }

        [Test]
        public void TheMatrices_AreThePapersAtSeverityOne()
        {
            // Pure linear primaries pick out the matrix's columns, clamped to the gamut.
            var red = new ColourVision.Rgb(1, 0, 0);
            var deutan = ColourVision.Simulate(ColourVision.Vision.Deutan, red);
            Assert.That(new[] { deutan.R, deutan.G, deutan.B }, Is.EqualTo(new[] { 0.367322, 0.280085, 0d }).Within(Tight));
            var protan = ColourVision.Simulate(ColourVision.Vision.Protan, new ColourVision.Rgb(0, 1, 0));
            Assert.That(new[] { protan.R, protan.G, protan.B }, Is.EqualTo(new[] { 1d, 0.786281, 0d }).Within(Tight), "1.052583 clamps to 1, -0.048116 to 0.");
            var tritan = ColourVision.Simulate(ColourVision.Vision.Tritan, new ColourVision.Rgb(0, 0, 1));
            Assert.That(new[] { tritan.R, tritan.G, tritan.B }, Is.EqualTo(new[] { 0d, 0.147602, 0.303900 }).Within(Tight));
            var normal = ColourVision.Simulate(ColourVision.Vision.Normal, new ColourVision.Rgb(0.2, 0.4, 0.6));
            Assert.That(new[] { normal.R, normal.G, normal.B }, Is.EqualTo(new[] { 0.2, 0.4, 0.6 }), "Normal vision is the colour itself.");
        }

        [Test]
        public void Greys_StayGreyUnderEveryDeficiency()
        {
            foreach (var vision in ColourVision.Deficiencies)
                foreach (var level in new[] { 0d, 0.05, 0.2140411, 0.5, 0.8, 1d })
                {
                    var seen = ColourVision.Simulate(vision, new ColourVision.Rgb(level, level, level));
                    Assert.That(seen.R, Is.EqualTo(level).Within(1e-5), vision + " at " + level);
                    Assert.That(seen.G, Is.EqualTo(level).Within(1e-5), vision + " at " + level);
                    Assert.That(seen.B, Is.EqualTo(level).Within(1e-5), vision + " at " + level);
                }
        }

        /// <summary>
        /// The pair the plan measured by hand: the web's friendship green #4ADE80 and rivalry red
        /// #FF5A5A, worlds apart to normal vision and close under deuteranopia - about 15.5 in CIE76
        /// and 1.42:1 in luminance, under both of the lead's floors.
        /// </summary>
        [Test]
        public void FriendshipGreenAndRivalryRed_AreCloseUnderDeuteranopiaAlone()
        {
            var green = ColourVision.FromHex("4ADE80");
            var red = ColourVision.FromHex("FF5A5A");
            var normalGreen = ColourVision.SeenAs(ColourVision.Vision.Normal, green);
            var normalRed = ColourVision.SeenAs(ColourVision.Vision.Normal, red);
            Assert.That(ColourVision.DeltaE76(normalGreen, normalRed), Is.EqualTo(123.3).Within(0.1));
            var deutanGreen = ColourVision.SeenAs(ColourVision.Vision.Deutan, green);
            var deutanRed = ColourVision.SeenAs(ColourVision.Vision.Deutan, red);
            Assert.That(ColourVision.DeltaE76(deutanGreen, deutanRed), Is.EqualTo(15.5).Within(0.1));
            Assert.That(ColourVision.LuminanceRatio(deutanGreen, deutanRed), Is.EqualTo(1.42).Within(0.01));
            Assert.That(ColourVision.Apart(deutanGreen, deutanRed), Is.False);
            Assert.That(ColourVision.Apart(ColourVision.SeenAs(ColourVision.Vision.Protan, green), ColourVision.SeenAs(ColourVision.Vision.Protan, red)), Is.True,
                "Protanopia keeps them apart by luminance (2.56:1).");
        }

        [Test]
        public void Apart_IsEitherFloor()
        {
            var dark = new ColourVision.Rgb(0.1, 0.1, 0.1);
            Assert.That(ColourVision.Apart(dark, dark), Is.False, "A colour is not apart from itself.");
            Assert.That(ColourVision.Apart(dark, new ColourVision.Rgb(0.2, 0.2, 0.2)), Is.True, "Lightness alone can be enough.");
            Assert.That(ColourVision.DeltaEFloor, Is.EqualTo(20d));
            Assert.That(ColourVision.LuminanceRatioFloor, Is.EqualTo(1.5d));
        }
    }
}
