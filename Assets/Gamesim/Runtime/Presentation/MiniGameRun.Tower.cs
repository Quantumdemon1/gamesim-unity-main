namespace Gamesim.Presentation
{
    /// <summary>
    /// The Ring Tower (competition rules 5): the tower of Hanoi on the yard's three stacks. The rules are
    /// <see cref="RingTowerPuzzle"/>'s; this is the attempt around them - the clock, the finish when the
    /// tower is home, and what the player is told after each pick. Nothing is drawn from any generator.
    /// </summary>
    public sealed partial class MiniGameRun
    {
        /// <summary>The tower being moved; null for every other game.</summary>
        public RingTowerPuzzle Tower { get; private set; }

        /// <summary>
        /// A pick of a stack (0 to 2): the first lifts its top ring, the next puts it down. A refused pick - an
        /// empty stack, a larger ring on a smaller one - changes nothing and says why. Moving the whole tower to
        /// the last stack ends the game at once, so the time left counts.
        /// </summary>
        public RingTowerPuzzle.Refusal PickStack(int stack)
        {
            if (Kind != CompetitionMiniGames.Kind.Tower || Tower == null) return RingTowerPuzzle.Refusal.NoSuchStack;
            int moves = Tower.Moves;
            var refusal = Tower.Pick(stack, Finished);
            switch (refusal)
            {
                case RingTowerPuzzle.Refusal.None:
                    if (Tower.Moves > moves)
                        Feedback = Tower.Solved ? "Tower complete in " + Tower.Moves + " moves" : "Moved to stack " + (stack + 1);
                    else Feedback = Tower.Lifted >= 0 ? "Lifted from stack " + (stack + 1) + ": pick where it goes" : "Put back on stack " + (stack + 1);
                    break;
                case RingTowerPuzzle.Refusal.EmptyStack: Feedback = "Stack " + (stack + 1) + " is empty"; break;
                case RingTowerPuzzle.Refusal.LargerOnSmaller: Feedback = "Not allowed: a larger ring cannot go on a smaller one"; break;
            }
            if (Tower.Solved && !Finished) Finish();
            return refusal;
        }
    }
}
