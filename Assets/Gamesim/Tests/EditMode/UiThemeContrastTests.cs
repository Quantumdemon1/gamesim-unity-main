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

        /// <summary>
        /// The three chrome levels have to be three levels in LUMINANCE, not three hues. A frame
        /// under bloom is luminance-crushed and a colour-blind player never had the hue, so a
        /// hue-only step is not a step at all.
        ///
        /// <para>This encodes the measurement the alphas were picked from, so nobody can quietly
        /// lower them again: a resting edge of Outline at .16 was tried, and it measures 1.09:1
        /// against the panel ground - below the floor the test below holds panels to - and 1.09:1
        /// away from the interactive level, which made the two one.</para>
        /// </summary>
        [Test]
        public void ChromeEmphasis_IsThreeLevelsOfBrightnessRatherThanThreeHues()
        {
            var ground = Over(UiTheme.GlassFill, 0.94f, UiTheme.Background);
            var resting = Over(UiTheme.Edge(UiTheme.Emphasis.Resting), ground);
            var interactive = Over(UiTheme.Edge(UiTheme.Emphasis.Interactive), ground);
            var active = Over(UiTheme.Edge(UiTheme.Emphasis.Active), ground);

            Assert.That(Contrast(resting, ground), Is.GreaterThan(1.2),
                "A resting edge still has to keep its panel from dissolving into a lit set.");
            Assert.That(Contrast(interactive, resting), Is.GreaterThan(1.25),
                "Interactive must be visibly brighter than resting, or it is not a level.");
            Assert.That(Contrast(active, interactive), Is.GreaterThan(3.0),
                "The accent edge is the one that says act here; it has to be unmistakable.");
            Assert.That(Luminance(resting), Is.LessThan(Luminance(interactive)));
            Assert.That(Luminance(interactive), Is.LessThan(Luminance(active)));
        }

        /// <summary>
        /// Gold is power: the crown, the win, the thing that was fought for. It reads as power only
        /// while nothing else wears it, and the mood palette once sat 22 parts in 255 away from it.
        /// </summary>
        [Test]
        public void GoldIsNotNearlyTheColourOfAnythingThatIsNotPower()
        {
            // Brass joins the list because it is the nearest thing to gold that is NOT power: it
            // rings twelve portraits at once on the cast screen, which is exactly the spend gold
            // cannot afford. If it ever drifts toward Gold the rings start reading as crowns.
            foreach (var other in new[] { UiTheme.Joke, UiTheme.Warning, UiTheme.Accent, UiTheme.Muted,
                UiTheme.Brass })
            {
                float distance = Mathf.Max(Mathf.Abs(other.r - UiTheme.Gold.r),
                    Mathf.Max(Mathf.Abs(other.g - UiTheme.Gold.g), Mathf.Abs(other.b - UiTheme.Gold.b)));
                Assert.That(distance, Is.GreaterThan(0.12f),
                    "A colour within 30/255 of Gold on every channel will be read as Gold.");
            }
        }

        private static Color Over(Color top, Color under) => Over(top, top.a, under);

        private static Color Over(Color top, float alpha, Color under) =>
            new Color(alpha * top.r + (1 - alpha) * under.r,
                alpha * top.g + (1 - alpha) * under.g,
                alpha * top.b + (1 - alpha) * under.b, 1f);

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
        public void TheMockupsTokens_ClearTheirMinimumsOnGlass()
        {
            // VISUAL-TARGET.md V2: the five action colours are chip words (15px, body text) on the
            // glass ground, and the glow and the accent are headings on it. The glass fill is
            // translucent, so a lit set behind it lifts the ground; the arithmetic half is here and
            // the bloom half stays with a captured frame, as for every other panel.
            AssertContrast(UiTheme.Flirt, UiTheme.GlassFill, BodyMinimum, "flirt chip word on glass");
            AssertContrast(UiTheme.Strategic, UiTheme.GlassFill, BodyMinimum, "strategic chip word on glass");
            AssertContrast(UiTheme.Joke, UiTheme.GlassFill, BodyMinimum, "joke chip word on glass");
            AssertContrast(UiTheme.Allied, UiTheme.GlassFill, BodyMinimum, "allied chip word on glass");
            AssertContrast(UiTheme.Conflict, UiTheme.GlassFill, BodyMinimum, "conflict chip word on glass");
            AssertContrast(UiTheme.Paper, UiTheme.GlassFill, BodyMinimum, "body copy on glass");
            AssertContrast(UiTheme.Paper, UiTheme.Background, BodyMinimum, "body copy on the night ground");
            AssertContrast(UiTheme.Accent, UiTheme.GlassFill, LargeMinimum, "accent headings on glass");
            AssertContrast(UiTheme.Glow, UiTheme.GlassFill, LargeMinimum, "glow headings on glass");
            double edge = Contrast(UiTheme.Hairline, UiTheme.GlassFill);
            Assert.That(edge, Is.GreaterThan(1.2), "The hairline is nearly the glass's own luminance (" + edge.ToString("F2") + ":1).");

            // The card ground is a second panel colour and needs its own guarantees, or the copy on
            // every roster card has none: this suite pins these tokens against GlassFill and nothing
            // else, so a card painted a different ground would go on being measured against a colour
            // it no longer wears, and go on passing.
            AssertContrast(UiTheme.Paper, UiTheme.CardFill, BodyMinimum, "a houseguest's name on a card");
            AssertContrast(UiTheme.Muted, UiTheme.CardFill, LargeMinimum, "the age and occupation line on a card");
            AssertContrast(UiTheme.Glow, UiTheme.CardFill, LargeMinimum, "PLAYING AS on the picked card");
            AssertContrast(UiTheme.Brass, UiTheme.CardFill, 3.0, "the brass ring around a portrait");
            double lift = Contrast(UiTheme.CardFill, UiTheme.Background);
            Assert.That(lift, Is.GreaterThan(1.12),
                "A card has to be visible as a card against the ground behind it, and this one is "
                + lift.ToString("F3") + ":1 from it.");
        }

        /// <summary>
        /// Every filled pill the cast screen can draw is readable.
        ///
        /// <para>A filled pill puts a word ON a saturated tint rather than beside it, which is the
        /// opposite of what the rest of this palette was checked for. White is not the answer: on
        /// these five tints white runs from 1.59:1 to 3.13:1. UiTheme.OnColor picks the foreground,
        /// so what has to hold is that ITS pick clears the bar for every tint the roster can hand
        /// it - and the archetype pill's word is twelve pixels, which is body text.</para>
        /// </summary>
        [Test]
        public void AFilledPillsWordIsReadableOnEveryTintTheRosterCanGiveIt()
        {
            foreach (var tint in new[] { UiTheme.Strategic, UiTheme.Joke, UiTheme.Flirt,
                UiTheme.Conflict, UiTheme.Allied, UiTheme.Accent, UiTheme.Glow })
            {
                var word = UiTheme.OnColor(tint);
                double ratio = Contrast(word, tint);
                Assert.That(ratio, Is.GreaterThanOrEqualTo(BodyMinimum),
                    "OnColor picked a foreground that reads at only " + ratio.ToString("F2")
                    + ":1 on " + UnityEngine.ColorUtility.ToHtmlStringRGB(tint) + ".");
            }
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

        /// <summary>
        /// The cast strip's standing tag is a light word on a dark tint of the standing's own
        /// colour. It has to read on every tint the strip can give it, and the tint has to be
        /// opaque, because the pill sits over a face.
        /// </summary>
        [Test]
        public void CastRailStandingTag_ItsWordIsReadableOnEveryGroundTheStripCanGiveIt()
        {
            foreach (var kind in new[]
            {
                RelationshipWeb.Kind.Alliance, RelationshipWeb.Kind.Friendship,
                RelationshipWeb.Kind.Distrust, RelationshipWeb.Kind.Rivalry,
            })
            {
                var ground = CastRail.StandingGround(kind);
                AssertContrast(UiTheme.Paper, ground, BodyMinimum, "the " + kind + " standing tag's word");
                Assert.That(ground.a, Is.EqualTo(1f).Within(.001f),
                    "The " + kind + " tag sits over the portrait; a translucent ground lets the face "
                    + "through behind the word.");
            }
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
