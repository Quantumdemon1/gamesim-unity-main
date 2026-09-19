using System.Linq;
using Gamesim.Presentation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Playing a competition, frame by frame, without waiting for it.
    ///
    /// <para>The run takes its own delta, so a thirty-second endurance competition is a loop here
    /// rather than half a minute of a test suite's life. That is the whole reason the behaviour
    /// lives outside the screen: a coroutine can only be watched, and this can be driven.</para>
    /// </summary>
    public sealed class MiniGameRunTests
    {
        private const double Frame = 1.0 / 60;

        // ---------------------------------------------------------------- endurance

        [Test]
        public void HoldingTheWholeWayThroughScoresFullMarks()
        {
            var run = Run(CompetitionMiniGames.Kind.Endurance);
            run.SetHolding(true);
            Play(run);

            Assert.That(run.Finished, Is.True);
            Assert.That(run.Score, Is.EqualTo(10).Within(0.2));
            Assert.That(run.Performance, Is.EqualTo(1).Within(0.02));
        }

        /// <summary>Never grabbing on at all drains the grip, and the run ends before the clock does.</summary>
        [Test]
        public void NeverHoldingEndsEarlyAndScoresNothing()
        {
            var run = Run(CompetitionMiniGames.Kind.Endurance);
            Play(run);

            Assert.That(run.Finished, Is.True);
            Assert.That(run.Held, Is.Zero);
            Assert.That(run.Score, Is.Zero);
            Assert.That(run.Elapsed, Is.LessThan(run.TimeLimit),
                "A grip that gives out ends the competition where it stands.");
            Assert.That(run.Meter, Is.EqualTo(CompetitionMiniGames.MeterEmpty));
        }

        /// <summary>
        /// The tension the reference builds: letting go saves the grip and costs score, and the
        /// trade gets worse as the clock runs down.
        /// </summary>
        [Test]
        public void LettingGoToSaveTheGripCostsScore()
        {
            var steady = Run(CompetitionMiniGames.Kind.Endurance);
            steady.SetHolding(true);
            Play(steady);

            var cautious = Run(CompetitionMiniGames.Kind.Endurance);
            // Hold for one second in every two.
            for (double t = 0; t < 40 && !cautious.Finished; t += Frame)
            {
                cautious.SetHolding((int)(t) % 2 == 0);
                cautious.Tick(Frame);
            }

            Assert.That(cautious.Held, Is.LessThan(steady.Held));
            Assert.That(cautious.Score, Is.LessThan(steady.Score));
            Assert.That(cautious.Score, Is.GreaterThan(0), "Holding half the time is not nothing.");
        }

        [Test]
        public void OnlyAnEnduranceRunAnswersToHolding()
        {
            var run = Run(CompetitionMiniGames.Kind.Memory);
            run.SetHolding(true);
            Assert.That(run.Holding, Is.False);
        }

        // ---------------------------------------------------------------- reaction

        [Test]
        public void HittingEveryTargetScoresFullMarksAndMissingThemAllScoresNothing()
        {
            var sharp = Run(CompetitionMiniGames.Kind.Reaction);
            for (double t = 0; t < 40 && !sharp.Finished; t += Frame)
            {
                sharp.Tick(Frame);
                if (sharp.TargetLive) sharp.Tap();
            }
            Assert.That(sharp.Finished, Is.True);
            Assert.That(sharp.Spawned, Is.GreaterThan(5), "A twenty-second run should put up targets.");
            Assert.That(sharp.Hits, Is.EqualTo(sharp.Spawned));
            Assert.That(sharp.Score, Is.EqualTo(10));

            var idle = Run(CompetitionMiniGames.Kind.Reaction);
            Play(idle);
            Assert.That(idle.Spawned, Is.GreaterThan(5));
            Assert.That(idle.Hits, Is.Zero);
            Assert.That(idle.Score, Is.Zero);
        }

        /// <summary>A target left to expire is a miss, and it is only counted once.</summary>
        [Test]
        public void ATargetLeftToExpireIsAMissCountedOnce()
        {
            var run = Run(CompetitionMiniGames.Kind.Reaction);
            while (!run.TargetLive && !run.Finished) run.Tick(Frame);
            Assert.That(run.Spawned, Is.EqualTo(1));

            for (double t = 0; t <= MiniGameRun.TargetLife + Frame; t += Frame) run.Tick(Frame);
            Assert.That(run.TargetLive, Is.False, "It should have gone by now.");
            Assert.That(run.Spawned, Is.EqualTo(1), "Expiring does not spawn it again.");
            Assert.That(run.Hits, Is.Zero);
        }

        /// <summary>Tapping thin air is not a miss — accuracy is counted against targets that appeared.</summary>
        [Test]
        public void TappingWithNothingUpCostsNothing()
        {
            var run = Run(CompetitionMiniGames.Kind.Reaction);
            Assert.That(run.Tap(), Is.False);
            Assert.That(run.Spawned, Is.Zero);
            Assert.That(run.Hits, Is.Zero);

            while (!run.TargetLive) run.Tick(Frame);
            Assert.That(run.Tap(), Is.True);
            Assert.That(run.Hits, Is.EqualTo(1));
            Assert.That(run.Tap(), Is.False, "The target is already down; a second tap is nothing.");
            Assert.That(run.Hits, Is.EqualTo(1));
        }

        [Test]
        public void EveryTargetLandsInsideThePlayArea()
        {
            var run = Run(CompetitionMiniGames.Kind.Reaction);
            int seen = 0;
            for (double t = 0; t < 40 && !run.Finished; t += Frame)
            {
                bool wasLive = run.TargetLive;
                run.Tick(Frame);
                if (!wasLive && run.TargetLive)
                {
                    seen++;
                    Assert.That(run.TargetX, Is.InRange(0d, 1d));
                    Assert.That(run.TargetY, Is.InRange(0d, 1d));
                }
                if (run.TargetLive) run.Tap();
            }
            Assert.That(seen, Is.GreaterThan(5));
        }

        // ---------------------------------------------------------------- memory

        [Test]
        public void ABoardIsAlwaysSolvableAndAlwaysShuffled()
        {
            var run = Run(CompetitionMiniGames.Kind.Memory);
            Assert.That(run.Pairs, Is.EqualTo(CompetitionMiniGames.MemoryPairs));
            Assert.That(run.Faces, Has.Count.EqualTo(run.Pairs * 2));
            Assert.That(run.Matched, Has.Count.EqualTo(run.Faces.Count));
            Assert.That(run.Matched.Any(m => m), Is.False);

            foreach (var group in run.Faces.GroupBy(f => f))
                Assert.That(group.Count(), Is.EqualTo(2), "Face " + group.Key + " is not a pair.");

            // Two different seeds should not deal the same board; one seed twice should.
            var same = new MiniGameRun(CompetitionMiniGames.Kind.Memory, 7u);
            CollectionAssert.AreEqual(Run(CompetitionMiniGames.Kind.Memory).Faces.ToList(), same.Faces.ToList());
            var other = new MiniGameRun(CompetitionMiniGames.Kind.Memory, 99u);
            Assert.That(other.Faces.SequenceEqual(same.Faces), Is.False);
        }

        [Test]
        public void ClearingTheBoardEndsTheRunBeforeTheClockDoes()
        {
            var run = Run(CompetitionMiniGames.Kind.Memory);
            SolveBoard(run);

            Assert.That(run.Finished, Is.True);
            Assert.That(run.MatchedPairs, Is.EqualTo(run.Pairs));
            Assert.That(run.Matched, Has.All.True);
            Assert.That(run.WrongFlips, Is.Zero);
            Assert.That(run.Elapsed, Is.LessThan(run.TimeLimit));
            Assert.That(run.Score, Is.EqualTo(10), "Cleared instantly and unerringly is full marks.");
        }

        [Test]
        public void AMismatchedPairTurnsBackAndCostsAWrongFlip()
        {
            var run = Run(CompetitionMiniGames.Kind.Memory);
            int a = 0;
            int b = Enumerable.Range(1, run.Faces.Count - 1).First(i => run.Faces[i] != run.Faces[a]);

            Assert.That(run.Flip(a), Is.True);
            Assert.That(run.FirstFlip, Is.EqualTo(a));
            Assert.That(run.Flip(b), Is.True);
            Assert.That(run.WrongFlips, Is.EqualTo(1));
            Assert.That(run.SecondFlip, Is.EqualTo(b));

            // A third card while two are showing is the reference's lock, and it holds.
            int c = Enumerable.Range(0, run.Faces.Count).First(i => i != a && i != b);
            Assert.That(run.Flip(c), Is.False, "Nothing turns while a mismatched pair is being shown.");

            for (double t = 0; t <= MiniGameRun.FlipBackDelay + Frame; t += Frame) run.Tick(Frame);
            Assert.That(run.FirstFlip, Is.EqualTo(-1));
            Assert.That(run.SecondFlip, Is.EqualTo(-1));
            Assert.That(run.Flip(c), Is.True, "Once they turn back, the board is free again.");
        }

        [Test]
        public void ACardCannotBeTurnedTwiceAndAMatchedOneCannotBeTurnedAtAll()
        {
            var run = Run(CompetitionMiniGames.Kind.Memory);
            int a = 0;
            int pair = Enumerable.Range(1, run.Faces.Count - 1).First(i => run.Faces[i] == run.Faces[a]);

            Assert.That(run.Flip(a), Is.True);
            Assert.That(run.Flip(a), Is.False, "It is already face up.");
            Assert.That(run.Flip(pair), Is.True);
            Assert.That(run.MatchedPairs, Is.EqualTo(1));
            Assert.That(run.Flip(a), Is.False, "It is matched and out of play.");
            Assert.That(run.Flip(-1), Is.False);
            Assert.That(run.Flip(run.Faces.Count), Is.False);
        }

        [Test]
        public void RunningOutOfTimeScoresWhatWasMatched()
        {
            var run = Run(CompetitionMiniGames.Kind.Memory);
            MatchPairs(run, 4);
            Assert.That(run.Finished, Is.False);
            Play(run);

            Assert.That(run.Finished, Is.True);
            Assert.That(run.MatchedPairs, Is.EqualTo(4));
            Assert.That(run.Score, Is.EqualTo(5), "Half the board, no wrong flips.");
        }

        // ---------------------------------------------------------------- every run

        [Test]
        public void NoRunEverHandsTheEngineAPerformanceItWouldRefuse()
        {
            foreach (var kind in new[]
                     {
                         CompetitionMiniGames.Kind.Endurance,
                         CompetitionMiniGames.Kind.Reaction,
                         CompetitionMiniGames.Kind.Memory,
                     })
                for (uint seed = 1; seed <= 12; seed++)
                {
                    var run = new MiniGameRun(kind, seed);
                    // Play it badly on purpose: hold erratically, tap at random, flip blindly.
                    int flip = 0;
                    for (double t = 0; t < 45 && !run.Finished; t += Frame)
                    {
                        run.SetHolding((int)(t * 3) % 3 == 0);
                        run.Tick(Frame);
                        if ((int)(t * 60) % 7 == 0) run.Tap();
                        if ((int)(t * 60) % 11 == 0) run.Flip(flip++ % 16);
                    }
                    if (!run.Finished) run.Finish();

                    Assert.That(run.Performance, Is.InRange(0d, 1d), kind + " seed " + seed);
                    Assert.That(run.Score, Is.InRange(0d, 10d), kind + " seed " + seed);
                }
        }

        [Test]
        public void AFinishedRunIgnoresEverythingAfterwards()
        {
            var run = Run(CompetitionMiniGames.Kind.Reaction);
            Play(run);
            double score = run.Score;
            double elapsed = run.Elapsed;

            run.Tick(5);
            run.Tap();
            run.Flip(0);
            run.SetHolding(true);
            run.Finish();

            Assert.That(run.Score, Is.EqualTo(score));
            Assert.That(run.Elapsed, Is.EqualTo(elapsed));
            Assert.That(run.Holding, Is.False);
        }

        [Test]
        public void AFrameWithNoTimeInItChangesNothing()
        {
            var run = Run(CompetitionMiniGames.Kind.Endurance);
            run.Tick(0);
            run.Tick(-1);
            Assert.That(run.Elapsed, Is.Zero);
            Assert.That(run.Meter, Is.EqualTo(CompetitionMiniGames.MeterFull));
        }

        // ---------------------------------------------------------------- fixtures

        private static MiniGameRun Run(CompetitionMiniGames.Kind kind) => new MiniGameRun(kind, 7u);

        /// <summary>Runs the clock out, doing nothing.</summary>
        private static void Play(MiniGameRun run)
        {
            for (double t = 0; t < 60 && !run.Finished; t += Frame) run.Tick(Frame);
            Assert.That(run.Finished, Is.True, "The run should have ended on its own.");
        }

        /// <summary>Clears the board by cheating: the test can see the faces, the player cannot.</summary>
        private static void SolveBoard(MiniGameRun run) => MatchPairs(run, run.Pairs);

        private static void MatchPairs(MiniGameRun run, int count)
        {
            var taken = new bool[run.Faces.Count];
            for (int done = 0; done < count; done++)
            {
                int a = Enumerable.Range(0, run.Faces.Count).First(i => !taken[i]);
                int b = Enumerable.Range(0, run.Faces.Count)
                    .First(i => i != a && !taken[i] && run.Faces[i] == run.Faces[a]);
                taken[a] = true; taken[b] = true;
                Assert.That(run.Flip(a), Is.True);
                Assert.That(run.Flip(b), Is.True);
            }
        }
    }
}
