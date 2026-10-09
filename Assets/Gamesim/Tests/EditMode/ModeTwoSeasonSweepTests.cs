using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Vote family V5's whole-season sweep: seeds 1 to 32 (houses of 8, then 6, then 12) played twice, command by command - the
    /// public mode-1 game and its mode-2 twin through the internal engine seam - by the plain walk and by the busy scripted
    /// player (<see cref="ModeTwoReaderSweep.Lockstep"/>). A season either equals mode 1 after projection at every command to
    /// the jury's verdict, or meets its first difference where this table says, which names the slice that owns it.
    ///
    /// <para>Every slice shrinks the table: a reader it moves takes a season further, often to the finish. What remains after
    /// V5f is only the designed differences (the class doc of the sweep lists them). A season that newly differs, or differs
    /// later or earlier than the table says, fails here and is looked at.</para>
    /// </summary>
    public sealed class ModeTwoSeasonSweepTests
    {
        /// <summary>Each season that does not play to the finish equal to mode 1: where it first differs, and the slice that owns it.</summary>
        private static readonly Dictionary<string, (string where, string owner)> Differs = new Dictionary<string, (string, string)>
        {
            // Designed (V4, the approved Rule2 policy): one reveal decided two of a pair's rows the same way, and settled their
            // consequences once - one line, memory and record for the owner; mode 2 then plays on to the finish.
            ["busy 11"] = ("designed at week 4 Eviction Advance: a Rule2 overlap; mode 2 then finished", "designed"),
            // Designed (V5c, the same policy): a reveal's own Vote breach is left out of its breaker's reputation, so the grudge
            // the wronged party draws at that reveal is at most one lighter; mode 2 then plays on to the finish.
            ["busy 18"] = ("designed at week 1 Eviction Advance: the current-reveal exclusion; mode 2 then finished", "designed"),
            ["busy 19"] = ("designed at week 2 Eviction Advance: the current-reveal exclusion; mode 2 then finished", "designed"),
            ["busy 22"] = ("designed at week 2 Eviction Advance: the current-reveal exclusion; mode 2 then finished", "designed"),
            // V5e: a juror's question reads no canonical Vote receipt.
            ["busy 24"] = ("week 4 JuryQuestioning Advance: juryExchanges", "V5e, FinaleQuestions"),
        };

        private static IEnumerable<TestCaseData> Seasons() =>
            new[] { false, true }.SelectMany(busy => Enumerable.Range(1, 32).Select(seed =>
                new TestCaseData(busy, (uint)seed, seed <= 16 ? 8 : seed <= 24 ? 6 : 12)));

        [TestCaseSource(nameof(Seasons))]
        public void ASeasonPlaysAsModeOneOrDiffersWhereTheTableSays(bool busy, uint seed, int size)
        {
            string key = (busy ? "busy" : "plain") + " " + seed;
            string outcome = ModeTwoReaderSweep.Lockstep(seed, size, busy);
            TestContext.Out.WriteLine(key + " n" + size + ": " + outcome);
            if (Differs.TryGetValue(key, out var expected))
                Assert.That(outcome, Is.EqualTo(expected.where), key + " differs where the table says (" + expected.owner + ").");
            else Assert.That(outcome, Is.EqualTo("finished"), key + " plays to the finish as mode 1 does.");
        }
    }
}
