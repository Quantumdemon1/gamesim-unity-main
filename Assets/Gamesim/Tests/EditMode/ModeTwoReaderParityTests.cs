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
    /// walk's moment, where the threat reader counts every breach mode 1 counts. In the finale (V5e) a canonical Vote row is a
    /// receipt, a moment or a scored chance only as its group's owner, so those readers are compared where no group holds two
    /// rows (<see cref="ModeTwoFinaleReaderTests"/>). The knowledge gate: mode 2's week does not say the player kept a vote deal
    /// whose keeping tells a hidden ballot, and its history counts no memory that tells one (V5d) - each compared with mode 2's
    /// reading put in place. The pages (V5f, <see cref="ModeTwoPageReaderTests"/>) are inventories, read row by row as mode 1
    /// reads them, at every moment.</para>
    ///
    /// <para><b>A sample on every run, the rest on demand.</b> The 32 walks take minutes, too long for every push and longer still
    /// in the editor. Four run always - seeds 5, 11, 18 and 25: houses of 8, 6 and 12, and seed 11, the one walk whose moments
    /// meet a two-row incident and a two-row group (the D1 branches). The other 28 are
    /// <c>TheRestOfTheWalksReadEveryMovedReaderAsModeOne</c>, explicit and compiled out of Unity: run them all with
    /// <c>dotnet test Tools/SimulationTests --filter "FullyQualifiedName~ModeTwoReaderParityTests"</c> before landing a change to
    /// a reader.</para>
    /// </summary>
    public sealed class ModeTwoReaderParityTests
    {
        /// <summary>The seeds every run walks.</summary>
        private static readonly int[] Sampled = { 5, 11, 18, 25 };

        /// <summary>Seeds 1 to 32: houses of 8, then 6, then 12.</summary>
        private static IEnumerable<TestCaseData> Walks(bool sampled) => Enumerable.Range(1, 32).Where(seed => Sampled.Contains(seed) == sampled)
            .Select(seed => new TestCaseData((uint)seed, seed <= 16 ? 8 : seed <= 24 ? 6 : 12));

        private static IEnumerable<TestCaseData> Sample() => Walks(true);

        [TestCaseSource(nameof(Sample))]
        public void EveryMovedReaderIsModeOnesThroughASeason(uint seed, int size) => Check(seed, size);

#if !UNITY_5_3_OR_NEWER
        private static IEnumerable<TestCaseData> Rest() => Walks(false);

        [TestCaseSource(nameof(Rest)), Explicit("The other 28 walks of the reader sweep, a few minutes: run before landing a change to a reader.")]
        public void TheRestOfTheWalksReadEveryMovedReaderAsModeOne(uint seed, int size) => Check(seed, size);
#endif

        private static void Check(uint seed, int size)
        {
            int commands = 0, moments = 0, overlapping = 0, grouped = 0;
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
                // V5d: the player's bargaining and word, in free time and the campaign.
                if (mode1.phase == EpisodePhase.Social || mode1.phase == EpisodePhase.Campaign)
                    ModeTwoBargainReaderTests.CheckBargain(mode1, mode2, where, overlap);
                // V5e: the finale and jury readers, the week and the verdict, at every moment to the jury's verdict - a kept group
                // of two rows as much an overlap of owners as a breach's.
                bool groups = overlap || UnifiedVoteHistory.Fulfillments(mode2).Any(g => g.EvidenceIds.Count > 1);
                if (groups) grouped++;
                ModeTwoFinaleReaderTests.CheckFinale(mode1, mode2, where, groups);
                // V5f: the pages - the commitments and their warnings, the pacts, what a houseguest says - at every moment.
                ModeTwoPageReaderTests.CheckPages(mode1, mode2, where);
                // V5b: the house's decisions, where the house makes them - the nominations, the campaign and the open vote,
                // the count, the final eviction and the jury.
                if (!Decides(mode1)) return;
                ModeTwoHouseReaderTests.CheckHouse(mode1, mode2, where, overlap);
                // V5c: threat and the Story readers.
                ModeTwoThreatStoryTests.CheckThreatStory(mode1, mode2, where, ModeTwoHouseReaderTests.Pairs(mode1, mode2), overlap);
            }, size);
            TestContext.Out.WriteLine(commands + " commands, " + moments + " moments, " + overlapping + " with a two-row incident, "
                + grouped + " with a two-row group.");
            Assert.That(commands, Is.GreaterThan(40), walked.ToString());
            Assert.That(walked.Finished || walked.Unsupported != null, Is.True, walked.ToString());
            Assert.That(overlapping, Is.LessThan(moments), "The D1 difference is the exception, not the walk.");
            Assert.That(grouped, Is.LessThan(moments), "So is a group of two rows.");
        }

        /// <summary>A moment the house decides at: the nominations, the campaign, eviction night, the final eviction and the jury.</summary>
        private static bool Decides(EpisodeState s) => s.phase == EpisodePhase.Nomination || s.phase == EpisodePhase.Campaign
            || s.phase == EpisodePhase.Eviction || s.phase == EpisodePhase.FinalEviction
            || s.phase == EpisodePhase.JuryQuestioning || s.phase == EpisodePhase.Jury;
    }
}
