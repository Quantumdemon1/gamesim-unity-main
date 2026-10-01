using System.Collections.Generic;
using System.Linq;
using Gamesim.Episode;
using NUnit.Framework;
using UnityEngine;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The bag's column on the veto draw's row (PACK8-PASS-PLAN B2), worked out without a HUD as
    /// FaceGridTests works out the faces. The row is sized from the height the step has left under
    /// the rows above it, and the bag's frame - which stands 8 units proud of the bag above and below
    /// - was left out of that height: a row whose cards the height had set, in a short step with a
    /// story beat open over the draw, ran 8 units past it, and the screen scrolled.
    /// </summary>
    public sealed class VetoDrawRowTests
    {
        private static readonly float[] Scales = { 1f, 1.2f };

        /// <summary>A card on the draw: a photo 1.12 of its width over a 50-unit foot, as EpisodeHud sizes it.</summary>
        private static float CardTall(float width, float scale) => width * 1.12f + 50f * scale;

        /// <summary>The bag's column under the bag at <paramref name="scale"/>: a gap, the chips, a gap and the line that says how many.</summary>
        private static float UnderTheBag(float scale) => (10f + 30f + 10f + 24f) * scale;

        /// <summary>
        /// The review's case: a step that leaves the cards 180 units at the resting text size, the
        /// cards by right and the pool solved to fill it. The row is 180 tall, not 188, and the bag's
        /// frame runs from the cards' top to their foot.
        /// </summary>
        [Test]
        public void ARowTheHeightSetIsThatHeightWithTheBagsFrameInside()
        {
            EpisodeHud.VetoDrawBagColumn(180f, 180f, 180f, 1f, out float bag, out float top, out float tall);
            Assert.That(tall, Is.EqualTo(180f).Within(.01f), "The row is the height its cards were solved for,");
            Assert.That(top - 8f, Is.GreaterThanOrEqualTo(-.01f), "the bag's frame starts at the cards' top or under it,");
            Assert.That(top + bag + UnderTheBag(1f) + 8f, Is.LessThanOrEqualTo(180f + .01f), "and ends at their foot or over it.");
        }

        /// <summary>
        /// Every height a step can leave the cards, at both text sizes, with the cards by right as tall
        /// as that height lets them stand or any shorter, and a pool of nobody, of one band beside
        /// them, or filling the height: the row always holds the bag's frame, the bag stands between
        /// its least and its most, and only a bag already at its least stands the row taller than its
        /// cards.
        /// </summary>
        [Test]
        public void TheBagsFrameStaysInsideTheHeightTheCardsWereSolvedFor()
        {
            var misses = new List<string>();
            foreach (float scale in Scales)
            {
                float floor = CardTall(EpisodeHud.FaceCardFloor * scale, scale), widest = CardTall(150f * scale, scale);
                float under = UnderTheBag(scale), proud = 8f * scale, least = 60f * scale, most = 124f * scale;
                for (float height = floor; height <= 700f; height += 1f)
                    for (float playing = floor; playing <= Mathf.Min(height, widest) + .001f; playing += 2f)
                        foreach (float eligible in new[] { 0f, playing, height })
                        {
                            EpisodeHud.VetoDrawBagColumn(playing, eligible, height, scale, out float bag, out float top, out float tall);
                            string at = playing + " by right and " + eligible + " in the pool, in " + height + " at " + scale;
                            float frameFoot = top + bag + under + proud;
                            if (bag < least - .001f || bag > most + .001f)
                                misses.Add(at + ": a bag of " + bag);
                            else if (frameFoot > tall + .01f)
                                misses.Add(at + ": the frame ends at " + frameFoot + " in a row " + tall + " tall");
                            else if (bag > least + .001f && tall > Mathf.Max(playing, eligible) + .01f)
                                misses.Add(at + ": a bag of " + bag + " stands the row " + tall + " tall");
                        }
            }
            Assert.That(misses, Is.Empty, misses.Count + " rows miss, among them: " + string.Join(" | ", misses.Take(8)));
        }
    }
}
