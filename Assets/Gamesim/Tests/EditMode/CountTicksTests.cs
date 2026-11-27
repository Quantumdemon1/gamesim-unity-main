using System;
using System.Collections.Generic;
using System.Globalization;
using Gamesim.Presentation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// A competition's last seconds tick as the clock's digits come to each whole second (AU-08): the
    /// tick is news, so it must sound in the frame its picture - the digits - changes. Read from the
    /// time under the digits, "5.0" was drawn at 5.04 seconds left and the tick came frames later, in
    /// a frame whose digits had not moved. Unity-free.
    /// </summary>
    public sealed class CountTicksTests
    {
        [Test]
        public void TheClocksDigitsStandInTheWholeSecondTheyCountDownTo()
        {
            Assert.That(CountTicks.WholeSecondShown("5.1"), Is.EqualTo(6));
            Assert.That(CountTicks.WholeSecondShown("5.0"), Is.EqualTo(5), "5.0 is the fifth-last second.");
            Assert.That(CountTicks.WholeSecondShown("4.1"), Is.EqualTo(5), "and so is everything down to 4.1.");
            Assert.That(CountTicks.WholeSecondShown("4.0"), Is.EqualTo(4));
            Assert.That(CountTicks.WholeSecondShown("0.1"), Is.EqualTo(1));
            Assert.That(CountTicks.WholeSecondShown("0.0"), Is.EqualTo(0), "Time up is no second at all.");
            Assert.That(CountTicks.WholeSecondShown("12.3"), Is.EqualTo(13));
            Assert.That(CountTicks.WholeSecondShown("4,0"), Is.EqualTo(4), "A comma for the point reads the same.");
            Assert.That(CountTicks.WholeSecondShown(""), Is.EqualTo(-1));
            Assert.That(CountTicks.WholeSecondShown(null), Is.EqualTo(-1));
            Assert.That(CountTicks.Ticks(6, -1), Is.False, "The sixth-last second does not tick.");
            Assert.That(CountTicks.Ticks(5, -1), Is.True);
            Assert.That(CountTicks.Ticks(5, 5), Is.False, "A second ticks once.");
            Assert.That(CountTicks.Ticks(0, 1), Is.False, "Time up does not tick.");
        }

        [Test]
        public void AClockRunDownTicksOnceForEachOfTheLastFiveSeconds_InTheFrameItsDigitsChange()
        {
            // A 30 second attempt run down a frame at a time at 60 frames a second, its digits drawn
            // as the competition's clock draws them.
            var ticks = new List<string>();
            int last = -1;
            string before = null;
            for (int frame = 0; frame <= 30 * 60 + 5; frame++)
            {
                double remaining = Math.Max(0.0, 30.0 - frame / 60.0);
                string digits = remaining.ToString("0.0", CultureInfo.InvariantCulture);
                int shown = CountTicks.WholeSecondShown(digits);
                if (CountTicks.Ticks(shown, last))
                {
                    Assert.That(digits, Is.Not.EqualTo(before), "A tick sounds in a frame whose digits changed.");
                    ticks.Add(digits);
                    last = shown;
                }
                before = digits;
            }
            Assert.That(ticks, Is.EqualTo(new[] { "5.0", "4.0", "3.0", "2.0", "1.0" }), "One tick as each of the last five whole seconds is drawn.");
        }

        [Test]
        public void AClockThatJumpsInAStalledFrameTicksForWhereItLands()
        {
            var ticks = new List<string>();
            int last = -1;
            foreach (var digits in new[] { "5.3", "4.6", "3.9", "3.2", "2.0", "0.4", "0.0" })
            {
                int shown = CountTicks.WholeSecondShown(digits);
                if (!CountTicks.Ticks(shown, last)) continue;
                ticks.Add(digits);
                last = shown;
            }
            Assert.That(ticks, Is.EqualTo(new[] { "4.6", "3.9", "2.0", "0.4" }),
                "Once for each whole second it lands in: the fifth, the fourth, the second and the last; the third it jumped over is not ticked late.");
        }
    }
}
