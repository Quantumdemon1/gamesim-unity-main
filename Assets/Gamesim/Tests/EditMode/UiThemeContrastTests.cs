using Gamesim.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Checks the HUD palette against the WCAG 2.1 contrast minimums.
    ///
    /// "Does the HUD read against the set" was written as something to judge by eye, which makes it
    /// the one acceptance criterion nobody can fail definitively. Contrast is a ratio, so most of it
    /// can simply be computed: text that clears 4.5:1 is legible to a reader with low vision
    /// regardless of taste, and text that does not is not, whatever it looks like to the author.
    ///
    /// What this cannot decide is the bloom. The 3D set glows behind these panels, and a bright
    /// fixture behind a semi-transparent surface lifts the effective background. That part still
    /// needs a captured frame and a person. This covers the half that is arithmetic.
    /// </summary>
    public sealed class UiThemeContrastTests
    {
        // WCAG 2.1 AA: 4.5:1 for body text, 3:1 for large text (>= 18.66px bold or 24px regular).
        private const double BodyMinimum = 4.5;
        private const double LargeMinimum = 3.0;

        [Test]
        public void BodyText_ClearsTheReadableMinimumOnEveryPanel()
        {
            // Paragraph and label copy is 15-21px, which WCAG counts as body text.
            AssertContrast(UiTheme.Paper, UiTheme.Ink, BodyMinimum, "body copy on chrome");
            AssertContrast(UiTheme.Paper, UiTheme.Surface, BodyMinimum, "body copy on a surface panel");
            AssertContrast(UiTheme.Paper, UiTheme.SurfaceRaised, BodyMinimum, "body copy on a raised panel");
        }

        [Test]
        public void StatusColours_ClearTheReadableMinimum()
        {
            AssertContrast(UiTheme.Warning, UiTheme.Ink, BodyMinimum, "recovery warning copy");
            AssertContrast(UiTheme.Danger, UiTheme.Ink, BodyMinimum, "danger copy");
            AssertContrast(UiTheme.Gold, UiTheme.Ink, BodyMinimum, "gold copy");
            AssertContrast(UiTheme.Positive, UiTheme.Ink, BodyMinimum, "allied-relationship copy");
            AssertContrast(UiTheme.Positive, UiTheme.Surface, BodyMinimum, "allied-relationship copy on a panel");
        }

        [Test]
        public void HeadingsAndAccents_ClearTheLargeTextMinimum()
        {
            // Headings are 26px+ and the ceremony headline is 34px, so the large-text bar applies.
            AssertContrast(UiTheme.Accent, UiTheme.Ink, LargeMinimum, "accent headings on chrome");
            AssertContrast(UiTheme.Accent, UiTheme.Surface, LargeMinimum, "accent headings on a surface panel");
        }

        [Test]
        public void MutedText_IsHonestlyReportedRatherThanAssumedFine()
        {
            // Muted is placeholder and de-emphasised copy. It is held to the large-text bar because
            // it is never the only way information is conveyed; if that ever changes, raise this.
            AssertContrast(UiTheme.Muted, UiTheme.Surface, LargeMinimum, "muted placeholder copy");
        }

        [Test]
        public void PanelEdges_AreDistinguishableFromTheirFill()
        {
            // Not a WCAG text rule: the border is what keeps a panel from dissolving into a bloom-lit
            // backdrop, so it needs to differ from the fill by more than rounding noise.
            double ratio = Contrast(UiTheme.Outline, UiTheme.Ink);
            Assert.That(ratio, Is.GreaterThan(1.2),
                "The panel outline is nearly the same luminance as the panel fill (" +
                ratio.ToString("F2") + ":1), so edges will vanish against a lit set.");
        }

        private static void AssertContrast(Color foreground, Color background, double minimum, string what)
        {
            double ratio = Contrast(foreground, background);
            Assert.That(ratio, Is.GreaterThanOrEqualTo(minimum),
                what + " is " + ratio.ToString("F2") + ":1, below the " + minimum.ToString("F1") +
                ":1 minimum (" + ColorUtility.ToHtmlStringRGB(foreground) + " on " +
                ColorUtility.ToHtmlStringRGB(background) + ").");
        }

        private static double Contrast(Color foreground, Color background)
        {
            double a = Luminance(foreground), b = Luminance(background);
            double lighter = System.Math.Max(a, b), darker = System.Math.Min(a, b);
            return (lighter + 0.05) / (darker + 0.05);
        }

        /// <summary>WCAG relative luminance. Unity colours are already linear-to-sRGB per channel.</summary>
        private static double Luminance(Color color) =>
            0.2126 * Channel(color.r) + 0.7152 * Channel(color.g) + 0.0722 * Channel(color.b);

        private static double Channel(double value) =>
            value <= 0.04045 ? value / 12.92 : System.Math.Pow((value + 0.055) / 1.055, 2.4);
    }
}
