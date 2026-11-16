using System;
using System.Linq;
using Gamesim.Presentation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The Ring Tower's rules (competition ring tower, slice 4): the tower of Hanoi on three stacks. Legal and
    /// illegal moves, the solved state, the fewest moves, and the score's order - better play never scores
    /// lower. Unity-free, so the subset runs it on every push.
    /// </summary>
    public sealed class RingTowerPuzzleTests
    {
        [Test]
        public void TheTowerStartsOnTheFirstStackLargestAtTheBottom()
        {
            var tower = new RingTowerPuzzle();
            Assert.That(tower.Rings, Is.EqualTo(4));
            Assert.That(tower.Stack(0), Is.EqualTo(new[] { 4, 3, 2, 1 }));
            Assert.That(tower.Stack(1), Is.Empty);
            Assert.That(tower.Stack(2), Is.Empty);
            Assert.That(tower.Minimum, Is.EqualTo(15), "2^4 - 1.");
            Assert.That((tower.Moves, tower.Refused, tower.Lifted, tower.Solved, tower.RingsHome), Is.EqualTo((0, 0, -1, false, 0)));
            Assert.Throws<ArgumentOutOfRangeException>(() => new RingTowerPuzzle(2));
            Assert.Throws<ArgumentOutOfRangeException>(() => new RingTowerPuzzle(9));
        }

        [Test]
        public void APickLiftsThenPutsAndAPickOfTheSameStackPutsTheRingBack()
        {
            var tower = new RingTowerPuzzle();
            Assert.That(tower.Pick(0), Is.EqualTo(RingTowerPuzzle.Refusal.None));
            Assert.That(tower.Lifted, Is.EqualTo(0));
            Assert.That(tower.Moves, Is.Zero, "Lifting is not a move.");
            Assert.That(tower.Pick(0), Is.EqualTo(RingTowerPuzzle.Refusal.None));
            Assert.That(tower.Lifted, Is.EqualTo(-1), "The ring went back.");
            Assert.That(tower.Moves, Is.Zero, "Putting it back is not a move.");
            Assert.That(tower.Pick(0), Is.EqualTo(RingTowerPuzzle.Refusal.None));
            Assert.That(tower.Pick(2), Is.EqualTo(RingTowerPuzzle.Refusal.None));
            Assert.That(tower.Stack(0), Is.EqualTo(new[] { 4, 3, 2 }));
            Assert.That(tower.Stack(2), Is.EqualTo(new[] { 1 }));
            Assert.That((tower.Moves, tower.Lifted), Is.EqualTo((1, -1)));
        }

        [Test]
        public void ALargerRingOnASmallerOneAndAnEmptyStackAreRefusedAndChangeNothing()
        {
            var tower = new RingTowerPuzzle();
            Assert.That(tower.Pick(1), Is.EqualTo(RingTowerPuzzle.Refusal.EmptyStack), "Nothing to lift on an empty stack.");
            Assert.That((tower.Refused, tower.Lifted, tower.LastRefusedStack), Is.EqualTo((1, -1, 1)));
            tower.Pick(0); tower.Pick(1);          // ring 1 to the middle
            tower.Pick(0);                         // lift ring 2
            string before = string.Join("|", Enumerable.Range(0, 3).Select(s => string.Join(",", tower.Stack(s))));
            Assert.That(tower.Pick(1), Is.EqualTo(RingTowerPuzzle.Refusal.LargerOnSmaller), "Ring 2 may not go on ring 1.");
            Assert.That(tower.LastRefusal, Is.EqualTo(RingTowerPuzzle.Refusal.LargerOnSmaller));
            Assert.That(string.Join("|", Enumerable.Range(0, 3).Select(s => string.Join(",", tower.Stack(s)))), Is.EqualTo(before), "A refused move moves nothing.");
            Assert.That((tower.Moves, tower.Refused, tower.Lifted), Is.EqualTo((1, 2, 0)), "The ring stays lifted, so another stack can be tried.");
            Assert.That(tower.CanMove(0, 1), Is.False);
            Assert.That(tower.CanMove(0, 2), Is.True);
            Assert.That(tower.Pick(2), Is.EqualTo(RingTowerPuzzle.Refusal.None));
            Assert.That(tower.Stack(2), Is.EqualTo(new[] { 2 }));
            Assert.That(tower.TryMove(2, 1), Is.False, "Ring 2 on ring 1, by the direct move too.");
            Assert.That(tower.TryMove(1, 1), Is.False);
            Assert.That(tower.Pick(7), Is.EqualTo(RingTowerPuzzle.Refusal.NoSuchStack));
        }

        [TestCase(3)] [TestCase(4)] [TestCase(5)]
        public void TheFewestMovesSolveTheTowerAndNothingIsAllowedAfter(int rings)
        {
            var tower = new RingTowerPuzzle(rings);
            var solution = RingTowerPuzzle.Solution(rings);
            Assert.That(solution.Count, Is.EqualTo(tower.Minimum).And.EqualTo((1 << rings) - 1));
            foreach (var (from, to) in solution)
            {
                Assert.That(tower.Solved, Is.False);
                Assert.That(tower.Pick(from), Is.EqualTo(RingTowerPuzzle.Refusal.None));
                Assert.That(tower.Pick(to), Is.EqualTo(RingTowerPuzzle.Refusal.None));
            }
            Assert.That(tower.Solved, Is.True);
            Assert.That(tower.Stack(2), Is.EqualTo(Enumerable.Range(1, rings).Reverse().ToArray()));
            Assert.That((tower.Moves, tower.Refused, tower.RingsHome), Is.EqualTo((tower.Minimum, 0, rings)));
            Assert.That(tower.Pick(2), Is.EqualTo(RingTowerPuzzle.Refusal.Finished), "A moved tower takes no more picks.");
            Assert.That(tower.Refused, Is.Zero, "A pick after the end is no refusal.");
        }

        [Test]
        public void RingsHomeCountsOnlyTheLargestFirstInOrderOnTheLastStack()
        {
            var tower = new RingTowerPuzzle();
            tower.TryMove(0, 2);                    // ring 1 on the last stack: not home, ring 4 belongs at the bottom
            Assert.That(tower.RingsHome, Is.Zero);
            var solved = new RingTowerPuzzle();
            var solution = RingTowerPuzzle.Solution(4);
            // The first eight moves of the fewest put ring 4 on the last stack... after seven the three small rings sit on the middle.
            for (int i = 0; i < 8; i++) solved.TryMove(solution[i].from, solution[i].to);
            Assert.That(solved.Stack(2), Is.EqualTo(new[] { 4 }));
            Assert.That(solved.RingsHome, Is.EqualTo(1));
        }

        [Test]
        public void FinishingAlwaysScoresMoreThanNotFinishing()
        {
            double worstFinished = RingTowerPuzzle.Score(true, 1000, 15, 0, 90, 4, 4);
            double bestUnfinished = RingTowerPuzzle.Score(false, 15, 15, 90, 90, 3, 4);
            Assert.That(worstFinished, Is.GreaterThan(bestUnfinished));
            Assert.That(worstFinished, Is.EqualTo(6.03).Within(.001), "Six, and a little for 15 of 1000 moves.");
            Assert.That(bestUnfinished, Is.EqualTo(3), "Four for every ring home, out of all four: three home is three.");
            Assert.That(RingTowerPuzzle.Score(true, 15, 15, 90, 90, 4, 4), Is.EqualTo(10), "The fewest moves at once is full marks.");
            Assert.That(RingTowerPuzzle.Score(false, 0, 15, 90, 90, 0, 4), Is.Zero, "No ring home, nothing.");
        }

        [Test]
        public void BetterPlayNeverScoresLower()
        {
            const int minimum = 15, rings = 4; const double limit = 90;
            // Finished: fewer moves, or more time left, never lowers the score.
            for (int moves = minimum; moves < 200; moves++)
                for (double left = 0; left <= limit; left += 7.5)
                {
                    double score = RingTowerPuzzle.Score(true, moves, minimum, left, limit, rings, rings);
                    Assert.That(score, Is.InRange(6, 10));
                    Assert.That(RingTowerPuzzle.Score(true, moves - 1 < minimum ? minimum : moves - 1, minimum, left, limit, rings, rings),
                        Is.GreaterThanOrEqualTo(score), moves + " moves, " + left + " s left: one move fewer");
                    Assert.That(RingTowerPuzzle.Score(true, moves, minimum, Math.Min(limit, left + 1), limit, rings, rings),
                        Is.GreaterThanOrEqualTo(score), moves + " moves, " + left + " s left: a second more");
                }
            // Unfinished: another ring home never lowers it; the moves and the time do not count against it.
            for (int home = 0; home < rings - 1; home++)
                Assert.That(RingTowerPuzzle.Score(false, 40, minimum, 0, limit, home + 1, rings),
                    Is.GreaterThan(RingTowerPuzzle.Score(false, 40, minimum, 0, limit, home, rings)));
            Assert.That(RingTowerPuzzle.Score(false, 40, minimum, 0, limit, 2, rings), Is.EqualTo(RingTowerPuzzle.Score(false, 9, minimum, 80, limit, 2, rings)));
        }

        [Test]
        public void ThePuzzlesOwnScoreReadsItsState()
        {
            var tower = new RingTowerPuzzle();
            Assert.That(tower.Score(90, 90), Is.Zero);
            foreach (var (from, to) in RingTowerPuzzle.Solution(4)) tower.TryMove(from, to);
            Assert.That(tower.Score(45, 90), Is.EqualTo(9), "Six, two for the fewest moves, one for half the clock.");
        }
    }
}
