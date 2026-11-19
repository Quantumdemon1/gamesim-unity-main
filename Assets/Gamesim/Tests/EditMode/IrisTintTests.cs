using System;
using System.Globalization;
using Gamesim.Presentation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The colour a body's eyes are given (<see cref="IrisTint.FromSwatch"/>): the swatch divided by the iris texture's
    /// grey in linear light, so the iris draws at the swatch. Plain numbers: no UMA, no engine.
    /// </summary>
    public sealed class IrisTintTests
    {
        private static float[] Linear(string hex) => new[]
        {
            IrisTint.ToLinear(Channel(hex, 0)), IrisTint.ToLinear(Channel(hex, 2)), IrisTint.ToLinear(Channel(hex, 4)),
        };

        private static float Channel(string hex, int at) => int.Parse(hex.Substring(at, 2), NumberStyles.HexNumber) / 255f;

        /// <summary>The tint for a swatch, in linear light.</summary>
        private static float[] TintLinear(string hex)
        {
            IrisTint.FromSwatch(Channel(hex, 0), Channel(hex, 2), Channel(hex, 4), out float r, out float g, out float b);
            foreach (float value in new[] { r, g, b }) Assert.That(value, Is.InRange(0f, 1f), hex + ": a colour the body takes.");
            return new[] { IrisTint.ToLinear(r), IrisTint.ToLinear(g), IrisTint.ToLinear(b) };
        }

        [Test]
        public void TheGreyIsTheIrisTexturesMedianInLinearLight()
        {
            Assert.That(IrisTint.IrisGreySrgb * 255f, Is.EqualTo(106f).Within(1e-4f));
            Assert.That(IrisTint.IrisGreyLinear, Is.EqualTo(.1441f).Within(1e-4f));
            Assert.That(IrisTint.ToSrgb(IrisTint.ToLinear(.3f)), Is.EqualTo(.3f).Within(1e-5f), "The two curves undo each other.");
        }

        /// <summary>A dark swatch (no channel above the texture's grey) draws exactly: tint times grey is the swatch.</summary>
        [TestCase("1B120C")] [TestCase("2E1D12")] [TestCase("4A2F1C")] [TestCase("4B6E3E")]
        public void ADarkSwatchComesOutExact(string hex)
        {
            var swatch = Linear(hex);
            var tint = TintLinear(hex);
            for (int i = 0; i < 3; i++)
                Assert.That(tint[i] * IrisTint.IrisGreyLinear, Is.EqualTo(swatch[i]).Within(1e-5f), hex + " channel " + i);
        }

        /// <summary>
        /// A light swatch, past what the texture can show, keeps its hue (the channels' ratios in linear light) and draws
        /// as bright as the texture lets it: its brightest channel is 1.
        /// </summary>
        [TestCase("6E97BF")] [TestCase("868C92")] [TestCase("94652B")] [TestCase("6B4527")] [TestCase("3E6D9A")] [TestCase("FFFFFF")]
        public void ALightSwatchKeepsItsHueAtTheTexturesBrightness(string hex)
        {
            var swatch = Linear(hex);
            var tint = TintLinear(hex);
            float most = Math.Max(swatch[0], Math.Max(swatch[1], swatch[2]));
            Assert.That(most, Is.GreaterThan(IrisTint.IrisGreyLinear), hex + " is a light swatch.");
            Assert.That(Math.Max(tint[0], Math.Max(tint[1], tint[2])), Is.EqualTo(1f).Within(1e-5f), hex + ": its brightest channel at full.");
            for (int i = 0; i < 3; i++)
                Assert.That(tint[i], Is.EqualTo(swatch[i] / most).Within(1e-5f), hex + " channel " + i + " keeps its share.");
        }

        [Test]
        public void BlackStaysBlack()
        {
            IrisTint.FromSwatch(0f, 0f, 0f, out float r, out float g, out float b);
            Assert.That(new[] { r, g, b }, Is.EqualTo(new[] { 0f, 0f, 0f }));
        }

        [Test]
        public void TheTexturesGreyBecomesWhite()
        {
            IrisTint.FromSwatch(IrisTint.IrisGreySrgb, IrisTint.IrisGreySrgb, IrisTint.IrisGreySrgb, out float r, out float g, out float b);
            foreach (float value in new[] { r, g, b }) Assert.That(value, Is.EqualTo(1f).Within(1e-5f));
        }
    }
}
