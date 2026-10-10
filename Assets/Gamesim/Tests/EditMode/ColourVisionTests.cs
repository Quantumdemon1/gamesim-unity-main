using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Gamesim.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Colour-blind safety (PLAN A, A9): every pair of tints that is the only cue between two things
    /// stays apart under protanopia, deuteranopia and tritanopia - CIE76 of at least 20, or a
    /// luminance ratio of at least 1.5:1 (the lead's decision 16) - both as authored and as drawn,
    /// composited at the line's own alpha over the notebook's ground.
    ///
    /// <para>The relationship web's lines are the pairs where colour is the only cue: two lines drawn
    /// with the same stroke are told apart by their colour or not at all, so each such pair is held
    /// to the floors; a pair whose strokes differ (<see cref="RelationshipWeb.StrokeOf"/>) is told
    /// apart by the stroke, whatever the colours do. The HUD's other colour cues each carry a second
    /// cue and are exempt, with the reason written in <see cref="Exempt"/>.</para>
    /// </summary>
    public sealed class ColourVisionTests
    {
        /// <summary>The colour cues that never stand alone, and the cue that stands beside each.</summary>
        private static readonly (string Cue, string Reason)[] Exempt =
        {
            ("The house feed's event tints (EpisodeHud.EventTint)", "each event carries its EventGlyph beside the tint"),
            ("Risk tints on free time and the decision cards (RiskTint)", "the risk is written out in words beside the tint"),
            ("Mood colours on a face (RelationshipWeb.MoodColour)", "the mood glyph and the mood's word carry it"),
            ("The jury chips", "each chip carries its juror's name"),
            ("The cast rail's standing tags", "each tag carries the standing's word (UiThemeContrastTests holds their legibility)"),
            ("The web's distrust line", "drawn dashed: a stroke of its own (RelationshipWeb.StrokeOf)"),
            ("The web's suspected-pact marker", "short purple dashes, thinner than distrust's, and named in the key"),
            ("The web's node rims", "a rim repeats its line's kind, and the line carries the stroke"),
            ("The vote reveal's two sides (VoteReveal.Side)", "a ballot stands in its nominee's column, under their name and face"),
        };

        /// <summary>
        /// The ground the lines are composited over: the HUD's background (#0B1220), not the notebook's
        /// glass fill over it - the review found the verdict the same either way. The numbers A9's
        /// commits record (Friendship against Rivalry, 12.6 / 1.39:1 as drawn) are over this ground.
        /// </summary>
        private static Color Ground => UiTheme.Background;

        private static ColourVision.Rgb Rgb(Color colour) => new ColourVision.Rgb(colour.r, colour.g, colour.b);

        private static string Measure(ColourVision.Rgb a, ColourVision.Rgb b) => string.Format(CultureInfo.InvariantCulture,
            "dE76 {0:0.0}, luminance {1:0.00}:1", ColourVision.DeltaE76(a, b), ColourVision.LuminanceRatio(a, b));

        [Test]
        public void TheWebsLinesThatShareAStroke_StayApartUnderEveryDeficiency()
        {
            var kinds = (RelationshipWeb.Kind[])Enum.GetValues(typeof(RelationshipWeb.Kind));
            var problems = new List<string>();
            for (int i = 0; i < kinds.Length; i++)
                for (int j = i + 1; j < kinds.Length; j++)
                {
                    var a = kinds[i];
                    var b = kinds[j];
                    bool sameStroke = RelationshipWeb.StrokeOf(a) == RelationshipWeb.StrokeOf(b);
                    Color tintA = RelationshipWeb.EdgeTint(a), tintB = RelationshipWeb.EdgeTint(b);
                    var drawnA = ColourVision.Over(Rgb(tintA), tintA.a, Rgb(Ground));
                    var drawnB = ColourVision.Over(Rgb(tintB), tintB.a, Rgb(Ground));
                    foreach (var vision in ColourVision.Deficiencies)
                    {
                        var rawA = ColourVision.SeenAs(vision, Rgb(tintA));
                        var rawB = ColourVision.SeenAs(vision, Rgb(tintB));
                        var seenA = ColourVision.SeenAs(vision, drawnA);
                        var seenB = ColourVision.SeenAs(vision, drawnB);
                        bool apart = ColourVision.Apart(rawA, rawB) && ColourVision.Apart(seenA, seenB);
                        string line = a + " / " + b + " under " + vision + ": as authored " + Measure(rawA, rawB) + "; as drawn " + Measure(seenA, seenB)
                            + (sameStroke ? apart ? " - apart" : " - TOO CLOSE" : " - told apart by stroke (" + RelationshipWeb.StrokeOf(a) + " / " + RelationshipWeb.StrokeOf(b) + ")");
                        TestContext.WriteLine(line);
                        if (sameStroke && !apart) problems.Add(line);
                    }
                }
            Assert.That(problems, Is.Empty, "Two lines drawn alike must differ in colour to every player:\n" + string.Join("\n", problems));
        }

        [Test]
        public void EveryOtherColourCue_NamesTheCueBesideIt()
        {
            foreach (var (cue, reason) in Exempt)
            {
                Assert.That(cue, Is.Not.Empty);
                Assert.That(reason, Is.Not.Empty, cue + " is exempt only with a reason.");
                TestContext.WriteLine("Exempt: " + cue + " - " + reason);
            }
            Assert.That(Exempt.Select(entry => entry.Cue).Distinct().Count(), Is.EqualTo(Exempt.Length));
            Assert.That(RelationshipWeb.StrokeOf(RelationshipWeb.Kind.Distrust), Is.Not.EqualTo(RelationshipWeb.StrokeOf(RelationshipWeb.Kind.Friendship)),
                "Distrust's exemption is its dashes.");
        }
    }
}
