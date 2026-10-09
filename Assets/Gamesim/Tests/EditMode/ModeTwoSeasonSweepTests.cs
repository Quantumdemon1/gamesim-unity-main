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
            // V5c: the threat reader counts no canonical breach in mode 2 - a Safety incident or the player's broken vote
            // promise - so the house's rumours, nominations and grudges read another threat from that command on.
            ["plain 4"] = ("week 4 Eviction Advance: relationships", "V5c, ThreatAssessment"),
            ["plain 17"] = ("week 3 Nomination Advance: randomState, nextSequence, contestants, relationships, memories, nominees, unifiedCommitments, events, story, Active", "V5c, ThreatAssessment"),
            ["plain 27"] = ("week 4 Eviction Advance: relationships", "V5c, ThreatAssessment"),
            ["plain 29"] = ("week 5 Eviction Advance: relationships", "V5c, ThreatAssessment"),
            ["busy 1"] = ("week 2 Eviction Advance: randomState, nextSequence, relationships, memories, events, relationshipArcs, deals, houseEvents, replyCards", "V5c, ThreatAssessment"),
            ["busy 9"] = ("week 2 Eviction Advance: randomState, relationships, events, relationshipArcs, replyCards", "V5c, ThreatAssessment"),
            ["busy 13"] = ("week 2 Eviction Advance: story", "V5c, the grudge's threat"),
            ["busy 17"] = ("week 3 Nomination Advance: randomState, nextSequence, contestants, relationships, memories, nominees, unifiedCommitments, events, story, Active", "V5c, ThreatAssessment"),
            ["busy 25"] = ("week 8 Eviction Advance: relationships", "V5c, ThreatAssessment"),
            ["busy 26"] = ("week 8 Eviction Advance: randomState, relationships, promises", "V5c, ThreatAssessment"),
            ["busy 27"] = ("week 5 Eviction Advance: story", "V5c, the grudge's threat"),
            ["busy 29"] = ("week 2 Eviction Advance: story", "V5c, the grudge's threat"),
            // V5d: a houseguest turning the player down reads the player's broken promises from the raw list.
            ["busy 14"] = ("week 2 Social ProposeDeal: events", "V5d, PlayerDeals.HasBrokenPromise"),
            // V5e: a juror's question reads no canonical Vote receipt.
            ["busy 18"] = ("week 4 JuryQuestioning Advance: juryExchanges", "V5e, FinaleQuestions"),
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
