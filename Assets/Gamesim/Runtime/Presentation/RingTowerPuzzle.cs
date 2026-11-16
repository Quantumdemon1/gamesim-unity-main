using System;
using System.Collections.Generic;

namespace Gamesim.Presentation
{
    /// <summary>
    /// The Ring Tower's rules, as data (competition ring tower, slice 4): the tower of Hanoi on three stacks.
    ///
    /// <para>Graduated rings start on the first stack, the largest at the bottom. The tower is moved to the
    /// last stack one ring at a time: pick a stack to lift its top ring, then pick the stack to put it on. A
    /// larger ring never goes on a smaller one; such a move, or a pick of an empty stack, is refused and
    /// changes nothing but what the player is told. Picking the lifted stack again puts the ring back.</para>
    ///
    /// <para>The score, out of ten as every game scores: a finished tower earns six, up to two more for
    /// moves against the fewest (2^n - 1), and up to two more for the time left. An unfinished tower earns up
    /// to four for the rings already home - the largest first, in order, at the bottom of the last stack -
    /// so finishing always beats not finishing, and better play never scores lower.</para>
    ///
    /// <para>No engine type and no random draw: the Unity-free suite plays it as the game does, and the
    /// season's generator is never touched.</para>
    /// </summary>
    public sealed class RingTowerPuzzle
    {
        /// <summary>The standard game's rings: fifteen moves at the fewest.</summary>
        public const int StandardRings = 4;

        /// <summary>The fewest rings and the most a tower can have (the stacks' pegs hold eight).</summary>
        public const int FewestRings = 3, MostRings = 8;

        /// <summary>The number of stacks.</summary>
        public const int Stacks = 3;

        /// <summary>The points a finished tower starts from, and the most for its moves and its time.</summary>
        public const double FinishedPoints = 6, MovePoints = 2, TimePoints = 2;

        /// <summary>The most an unfinished tower can earn, for its rings already home.</summary>
        public const double UnfinishedPoints = 4;

        /// <summary>Why a pick was refused, or None when it was taken.</summary>
        public enum Refusal { None, EmptyStack, LargerOnSmaller, Finished, NoSuchStack }

        private readonly List<int>[] stacks = new List<int>[Stacks];

        /// <summary>How many rings the tower has. Ring sizes are 1 (smallest) to <see cref="Rings"/> (largest).</summary>
        public int Rings { get; }

        /// <summary>The fewest moves the tower can be moved in: 2^n - 1.</summary>
        public int Minimum => (1 << Rings) - 1;

        /// <summary>The legal moves made.</summary>
        public int Moves { get; private set; }

        /// <summary>The picks refused: an empty stack, or a larger ring on a smaller one. They cost nothing but time.</summary>
        public int Refused { get; private set; }

        /// <summary>The stack whose top ring is lifted, or -1 when none is.</summary>
        public int Lifted { get; private set; } = -1;

        /// <summary>Why the last pick was refused; None when it was taken.</summary>
        public Refusal LastRefusal { get; private set; }

        /// <summary>The stack the last refused pick named, or -1.</summary>
        public int LastRefusedStack { get; private set; } = -1;

        /// <summary>Whether every ring is on the last stack.</summary>
        public bool Solved => stacks[Stacks - 1].Count == Rings;

        public RingTowerPuzzle(int rings = StandardRings)
        {
            if (rings < FewestRings || rings > MostRings) throw new ArgumentOutOfRangeException(nameof(rings));
            Rings = rings;
            for (int i = 0; i < Stacks; i++) stacks[i] = new List<int>();
            for (int size = rings; size >= 1; size--) stacks[0].Add(size);
        }

        /// <summary>A stack's rings from the bottom up, as sizes (1 the smallest).</summary>
        public IReadOnlyList<int> Stack(int stack)
        {
            if (stack < 0 || stack >= Stacks) throw new ArgumentOutOfRangeException(nameof(stack));
            return stacks[stack];
        }

        /// <summary>A stack's top ring's size, or 0 when it is empty.</summary>
        public int Top(int stack) => stacks[stack].Count == 0 ? 0 : stacks[stack][stacks[stack].Count - 1];

        /// <summary>The rings already home: the largest first, in order, from the bottom of the last stack.</summary>
        public int RingsHome
        {
            get
            {
                var last = stacks[Stacks - 1];
                int home = 0;
                while (home < last.Count && last[home] == Rings - home) home++;
                return home;
            }
        }

        /// <summary>Whether moving the top ring of <paramref name="from"/> onto <paramref name="to"/> is legal.</summary>
        public bool CanMove(int from, int to) =>
            from >= 0 && from < Stacks && to >= 0 && to < Stacks && from != to
            && stacks[from].Count > 0 && (stacks[to].Count == 0 || Top(to) > Top(from));

        /// <summary>
        /// One pick: the first lifts a stack's top ring, the second puts it on another stack (or back, on the
        /// same one). Returns the refusal, None when the pick was taken. A refused put leaves the ring lifted, so
        /// the player can try another stack.
        /// </summary>
        public Refusal Pick(int stack, bool finished = false)
        {
            if (finished || Solved) return Refuse(Refusal.Finished, stack);
            if (stack < 0 || stack >= Stacks) return Refuse(Refusal.NoSuchStack, stack);
            if (Lifted < 0)
            {
                if (stacks[stack].Count == 0) return Refuse(Refusal.EmptyStack, stack);
                Lifted = stack; LastRefusal = Refusal.None; LastRefusedStack = -1;
                return Refusal.None;
            }
            if (stack == Lifted) { Lifted = -1; LastRefusal = Refusal.None; LastRefusedStack = -1; return Refusal.None; }
            if (!CanMove(Lifted, stack)) return Refuse(Refusal.LargerOnSmaller, stack);
            Move(Lifted, stack);
            Lifted = -1; LastRefusal = Refusal.None; LastRefusedStack = -1;
            return Refusal.None;
        }

        /// <summary>Moves the top ring of <paramref name="from"/> onto <paramref name="to"/> when legal; false otherwise. Nothing lifted after.</summary>
        public bool TryMove(int from, int to)
        {
            if (Solved || !CanMove(from, to)) return false;
            Move(from, to); Lifted = -1;
            return true;
        }

        private void Move(int from, int to)
        {
            var source = stacks[from];
            int ring = source[source.Count - 1];
            source.RemoveAt(source.Count - 1);
            stacks[to].Add(ring);
            Moves++;
        }

        private Refusal Refuse(Refusal why, int stack)
        {
            if (why != Refusal.Finished) Refused++;
            LastRefusal = why; LastRefusedStack = stack;
            return why;
        }

        /// <summary>The score out of ten for this tower as it stands, with <paramref name="secondsLeft"/> of <paramref name="limitSeconds"/> left.</summary>
        public double Score(double secondsLeft, double limitSeconds) =>
            Score(Solved, Moves, Minimum, secondsLeft, limitSeconds, RingsHome, Rings);

        /// <summary>
        /// The score out of ten: a finished tower 6, plus up to 2 for the fewest moves against the moves made and
        /// up to 2 for the share of the clock left; an unfinished one 4 for every ring home, out of all of them.
        /// Rounded to two places as every game's score is.
        /// </summary>
        public static double Score(bool solved, int moves, int minimum, double secondsLeft, double limitSeconds, int ringsHome, int rings)
        {
            if (rings <= 0) return 0;
            if (!solved)
                return Round(UnfinishedPoints * Math.Max(0, Math.Min(rings - 1, ringsHome)) / rings);
            double efficiency = moves <= 0 ? 1 : Math.Max(0, Math.Min(1, (double)minimum / moves));
            double time = limitSeconds <= 0 ? 0 : Math.Max(0, Math.Min(1, secondsLeft / limitSeconds));
            return Round(FinishedPoints + MovePoints * efficiency + TimePoints * time);
        }

        /// <summary>Two places, as every game rounds its score (CompetitionMiniGames.Round).</summary>
        private static double Round(double score) => Math.Round(score * 100) / 100;

        /// <summary>The fewest moves for a tower of <paramref name="rings"/>, from the first stack to the last: (from, to) pairs.</summary>
        public static IReadOnlyList<(int from, int to)> Solution(int rings)
        {
            var moves = new List<(int, int)>();
            Solve(rings, 0, Stacks - 1, 1, moves);
            return moves;
        }

        private static void Solve(int rings, int from, int to, int spare, List<(int, int)> moves)
        {
            if (rings == 0) return;
            Solve(rings - 1, from, spare, to, moves);
            moves.Add((from, to));
            Solve(rings - 1, spare, to, from, moves);
        }
    }
}
