using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Vote family V5's whole-season sweep: seeds 1 to 32 (houses of 8, then 6, then 12) played twice, command by command - the
    /// public mode-1 game and its mode-2 twin through the internal engine seam - by the plain walk and by the busy scripted
    /// player (<see cref="ModeTwoReaderSweep.Lockstep"/>). A season either equals mode 1 after projection at every command to
    /// the jury's verdict, or meets its first difference where this table says, which names the slice that owns it. Every
    /// mode-2 command runs under the walk observer (vote family V5e): past each reveal, the owners the archive rebuilds are
    /// the ones its plan selected (<see cref="ModeTwoReaderSweep.AssertOwnersRebuilt"/>).
    ///
    /// <para>Every slice shrank the table: a reader it moved took a season further, often to the finish. After V5f all 64 seasons
    /// play mode 2 to the jury's verdict: 60 equal mode 1 at every command, and four meet a designed difference first and play
    /// on to the finish. A season that newly differs, or differs later or earlier than the table says, fails here and is looked
    /// at.</para>
    ///
    /// <para><b>The designed differences</b>, each pinned by a case of its own:
    /// - Rule2 effects (V4, the approved overlap policy): one reveal deciding two of a pair's rows the same way settles their
    ///   consequences once, for the owner (UnifiedVoteSettlementTests; "a Rule2 overlap" below).
    /// - The current-reveal exclusion (V5c): a reveal's own Vote breach is left out of the breaker's reputation in the grudge its
    ///   recipes scale (ModeTwoThreatStoryTests; "the current-reveal exclusion" below).
    /// - D1 counting (V5b, V5d, V5e): a breach term counts a Rule2 incident once, and a receipt, a kept moment or a scored chance
    ///   is its group's owner (ModeTwoHouseReaderTests, ModeTwoBargainReaderTests, ModeTwoFinaleReaderTests). A reader's count;
    ///   the state moves only where a decision reads it, which no season here reaches before its Rule2 overlap.
    /// - The knowledge gate (V5d, V5e): mode 2's history counts no memory that tells a hidden ballot, and its week does not say
    ///   the player kept a vote deal whose keeping tells one (ModeTwoBargainReaderTests, ModeTwoFinaleReaderTests). Readers only.</para>
    ///
    /// <para><b>A sample on every run, the rest on demand.</b> The 64 seasons take minutes, too long for every push (CI gives the
    /// whole subset ten minutes) and longer still in the editor. Six run always - seeds 5, 11 and 18, plain and busy: houses of 8
    /// and 6, a Rule2 overlap (busy 11) and the current-reveal exclusion (busy 18). The other 58 are
    /// <c>TheRestOfTheSeasonsPlayAsModeOneOrDifferWhereTheTableSays</c>, explicit and compiled out of Unity as the
    /// digests are: run them before landing a change to the vote family with
    /// <c>dotnet test Tools/SimulationTests --filter "FullyQualifiedName~TheRestOfTheSeasonsPlayAsModeOneOrDifferWhereTheTableSays"</c>.
    /// Name them: the test adapter runs explicit tests only where the filter selects nothing else, so the class's name runs the
    /// sample alone.</para>
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
        };

        /// <summary>The seeds every run plays, plain and busy.</summary>
        private static readonly int[] Sampled = { 5, 11, 18 };

        private static IEnumerable<TestCaseData> Seasons(bool sampled) =>
            new[] { false, true }.SelectMany(busy => Enumerable.Range(1, 32).Where(seed => Sampled.Contains(seed) == sampled).Select(seed =>
                new TestCaseData(busy, (uint)seed, seed <= 16 ? 8 : seed <= 24 ? 6 : 12)));

        private static IEnumerable<TestCaseData> Sample() => Seasons(true);

        [TestCaseSource(nameof(Sample))]
        public void ASeasonPlaysAsModeOneOrDiffersWhereTheTableSays(bool busy, uint seed, int size) => Play(busy, seed, size);

#if !UNITY_5_3_OR_NEWER
        private static IEnumerable<TestCaseData> Rest() => Seasons(false);

        [TestCaseSource(nameof(Rest)), Explicit("The other 58 seasons of the sweep, a few minutes: run before landing a change to the vote family.")]
        public void TheRestOfTheSeasonsPlayAsModeOneOrDifferWhereTheTableSays(bool busy, uint seed, int size) => Play(busy, seed, size);

        [Test]
        public void TheSampleAndTheRestAreTheWholeTable()
        {
            var all = Seasons(true).Concat(Seasons(false)).Select(c => (busy: (bool)c.Arguments[0], seed: (uint)c.Arguments[1])).ToList();
            Assert.That(all.Count, Is.EqualTo(64));
            Assert.That(all.Distinct().Count(), Is.EqualTo(64), "Each season once.");
            Assert.That(Differs.Keys.Count(key => Sample().Any(c => ((bool)c.Arguments[0] ? "busy " : "plain ") + c.Arguments[1] == key)), Is.EqualTo(2),
                "The sample meets both kinds of designed difference: a Rule2 overlap and the current-reveal exclusion.");
        }
#endif

        private static void Play(bool busy, uint seed, int size)
        {
            string key = (busy ? "busy" : "plain") + " " + seed;
            string outcome = ModeTwoReaderSweep.Lockstep(seed, size, busy);
            TestContext.Out.WriteLine(key + " n" + size + ": " + outcome);
            if (Differs.TryGetValue(key, out var expected))
                Assert.That(outcome, Is.EqualTo(expected.where), key + " differs where the table says (" + expected.owner + ").");
            else Assert.That(outcome, Is.EqualTo("finished"), key + " plays to the finish as mode 1 does.");
        }

        /// <summary>
        /// The table's "a Rule2 overlap" excuses what Rule2 writes and nothing else: a reveal at which one houseguest's ballot broke
        /// two of their vote deals with the player differs from mode 1 in the line, memory, record, Story lanes and draws Rule2 writes
        /// once; the same reveal with one more difference beside them - a phase, a row - is no designed difference.
        /// </summary>
        [Test]
        public void ARule2OverlapExcusesOnlyWhatRule2Writes()
        {
            var s = ModeTwoReaderSweep.Campaign();
            string npc = PinnedVoteSeason.NpcVoters(s).First(), x = s.nominees[0], y = s.nominees[1];
            s = ModeTwoReaderSweep.Strike(ModeTwoReaderSweep.Strike(s, npc, DealKind.VoteSave, x), npc, DealKind.VoteEvict, y);
            var revealed = ModeTwoReaderSweep.Reveal(s, y, new Dictionary<string, string> { [npc] = x });
            var differences = ModeTwoReaderSweep.Differences(revealed.Projection, revealed.Mode2);
            Assert.That(differences, Is.Not.Empty, "Fixture: Rule2 wrote the breach once.");
            Assert.That(ModeTwoReaderSweep.Designed(revealed.Projection, revealed.Mode2, differences), Is.EqualTo("a Rule2 overlap"));
            foreach (string other in new[] { "phase: mode 1 7 / mode 2 8", "unifiedCommitments[0]: mode 1 {} / mode 2 {}", "votes[1]: mode 1 {} / mode 2 none" })
                Assert.That(ModeTwoReaderSweep.Designed(revealed.Projection, revealed.Mode2, differences.Concat(new[] { other }).ToList()), Is.Null,
                    "A Rule2 week's " + other.Split(':')[0] + " is no Rule2 write.");
        }
    }
}
