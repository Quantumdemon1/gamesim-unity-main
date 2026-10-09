using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Vote family V5's reader sweep (pattern P2): one walk per seed - a busy player's public mode-1 season, seeds 1 to 32 in
    /// houses of 8, then 6, then 12 (<see cref="ModeTwoReaderSweep.Walk"/>) - and every reader V5 has moved checked on the exact
    /// mode-2 projection against its mode-1 answer: the views at every command, and each slice's readers at the moments the house
    /// decides (every phase change and every count). One walk carries them all, so a seed is walked once however many readers move.
    ///
    /// <para>The designed differences, each skipped only where it can arise and pinned by a case of its own: D1, a breach term
    /// counting a Rule2 incident once where one ballot broke two of a pair's rows (<see cref="ModeTwoHouseReaderTests"/>). The
    /// current-reveal exclusion (<see cref="ModeTwoThreatStoryTests"/>) arises only inside a reveal's own recipes, never at a
    /// walk's moment, where the threat reader counts every breach mode 1 counts.</para>
    /// </summary>
    public sealed class ModeTwoReaderParityTests
    {
        /// <summary>Seeds 1 to 32: houses of 8, then 6, then 12.</summary>
        private static IEnumerable<TestCaseData> Walks() => Enumerable.Range(1, 32).Select(seed =>
            new TestCaseData((uint)seed, seed <= 16 ? 8 : seed <= 24 ? 6 : 12));

        [TestCaseSource(nameof(Walks))]
        public void EveryMovedReaderIsModeOnesThroughASeason(uint seed, int size)
        {
            int commands = 0, moments = 0, overlapping = 0;
            var lastPhase = (EpisodePhase)(-1);
            bool lastResolved = false;
            var walked = ModeTwoReaderSweep.Walk(seed, (mode1, mode2, where) =>
            {
                commands++;
                // V5a: the views and the offers waiting, at every command.
                CommitmentReaderViewTests.CheckViews(mode1, mode2, where);
                bool moment = mode1.phase != lastPhase || mode1.evictionResolved != lastResolved;
                lastPhase = mode1.phase; lastResolved = mode1.evictionResolved;
                if (!moment) return;
                moments++;
                bool overlap = UnifiedVoteHistory.Breaches(mode2).Any(i => i.EvidenceIds.Count > 1);
                if (overlap) overlapping++;
                // V5b: the house's decisions, where the house makes them - the nominations, the campaign and the open vote,
                // the count, the final eviction and the jury.
                if (!Decides(mode1)) return;
                ModeTwoHouseReaderTests.CheckHouse(mode1, mode2, where, overlap);
                // V5c: threat and the Story readers.
                ModeTwoThreatStoryTests.CheckThreatStory(mode1, mode2, where, ModeTwoHouseReaderTests.Pairs(mode1, mode2), overlap);
            }, size);
            TestContext.Out.WriteLine(commands + " commands, " + moments + " moments, " + overlapping + " with a two-row incident.");
            Assert.That(commands, Is.GreaterThan(40), walked.ToString());
            Assert.That(walked.Finished || walked.Unsupported != null, Is.True, walked.ToString());
            Assert.That(overlapping, Is.LessThan(moments), "The D1 difference is the exception, not the walk.");
        }

        /// <summary>A moment the house decides at: the nominations, the campaign, eviction night, the final eviction and the jury.</summary>
        private static bool Decides(EpisodeState s) => s.phase == EpisodePhase.Nomination || s.phase == EpisodePhase.Campaign
            || s.phase == EpisodePhase.Eviction || s.phase == EpisodePhase.FinalEviction
            || s.phase == EpisodePhase.JuryQuestioning || s.phase == EpisodePhase.Jury;
    }
}
