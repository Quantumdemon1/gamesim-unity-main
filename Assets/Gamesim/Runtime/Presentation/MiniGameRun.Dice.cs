using System;

namespace Gamesim.Presentation
{
    /// <summary>
    /// The luck game: three dice, up to three rolls, and a real choice between them.
    ///
    /// <para>The reference's Dice Roll Derby kept the best of three rolls, so rolling all three was
    /// always right and the only decision was none. Here a re-roll replaces the roll you have: keep a
    /// good one when you see it, or gamble it on the next. The kept total, 3 to 18, is the score, out
    /// of ten as the reference scales it.</para>
    ///
    /// <para>Every face is drawn when the run is made, from the run's own generator, so a ranked
    /// attempt rolls the same dice after a cancel or a reload - the same board as every other game -
    /// and nothing the player does changes what the next roll will be.</para>
    /// </summary>
    public sealed partial class MiniGameRun
    {
        public const int DiceCount = 3, MaxRolls = 3;

        /// <summary>When each die stops after a roll starts, in seconds: they land one after another.</summary>
        public static double DieSettlesAfter(int die) => .5 + .2 * die;

        /// <summary>How long a roll tumbles before its last die lands.</summary>
        public static double RollSeconds => DieSettlesAfter(DiceCount - 1);

        private readonly int[] dealtFaces = new int[DiceCount * MaxRolls];
        private double rollStartedAt = -1;

        /// <summary>How many of the three rolls have been taken, the one tumbling included.</summary>
        public int RollsUsed { get; private set; }

        /// <summary>Whether the dice are still tumbling from the latest roll. Never once the game is over.</summary>
        public bool Rolling => !Finished && RollsUsed > 0 && rollStartedAt >= 0 && Elapsed < rollStartedAt + RollSeconds;

        /// <summary>Whether a roll has landed that could be kept.</summary>
        public bool HasRoll => RollsUsed > 0 && !Rolling;

        /// <summary>Whether another roll may be taken: one is left and none is tumbling.</summary>
        public bool CanRoll => !Finished && Kind == CompetitionMiniGames.Kind.Dice && !Rolling && RollsUsed < MaxRolls;

        /// <summary>Whether the roll showing may be kept.</summary>
        public bool CanKeep => !Finished && Kind == CompetitionMiniGames.Kind.Dice && HasRoll;

        /// <summary>A die's face, 1 to 6, once it has landed on the latest roll; 0 before the first roll or while it tumbles.</summary>
        public int Face(int die)
        {
            if (die < 0 || die >= DiceCount || RollsUsed == 0 || !DieLanded(die)) return 0;
            return dealtFaces[(RollsUsed - 1) * DiceCount + die];
        }

        /// <summary>Whether a die has landed on the latest roll.</summary>
        public bool DieLanded(int die) => RollsUsed > 0 && (Finished || Elapsed >= rollStartedAt + DieSettlesAfter(die));

        /// <summary>The latest roll's total once every die has landed; 0 before.</summary>
        public int RollTotal
        {
            get
            {
                if (RollsUsed == 0 || Rolling) return 0;
                int total = 0;
                for (int die = 0; die < DiceCount; die++) total += dealtFaces[(RollsUsed - 1) * DiceCount + die];
                return total;
            }
        }

        /// <summary>The total the run finished on, 3 to 18, or 0 when it ended without a roll.</summary>
        public int KeptTotal { get; private set; }

        private void DealDice()
        {
            for (int i = 0; i < dealtFaces.Length; i++)
            {
                int face = 1 + (int)(random.NextDouble() * 6);
                dealtFaces[i] = face > 6 ? 6 : face;
            }
        }

        /// <summary>Rolls the dice, replacing the roll showing. False when no roll is left or one is tumbling.</summary>
        public bool Roll()
        {
            if (!CanRoll) return false;
            RollsUsed++;
            rollStartedAt = Elapsed;
            Feedback = "Rolling";
            return true;
        }

        /// <summary>Keeps the roll showing, which ends the game on it.</summary>
        public bool Keep()
        {
            if (!CanKeep) return false;
            Finish();
            return true;
        }

        /// <summary>A roll that lands with none left stands: the third roll is the one you keep.</summary>
        private void StepDice()
        {
            if (RollsUsed == 0 || rollStartedAt < 0) return;
            if (Elapsed >= rollStartedAt + RollSeconds)
            {
                if (RollsUsed >= MaxRolls) { Finish(); return; }
                Feedback = "Rolled " + RollTotal;
            }
        }

        /// <summary>The kept total out of ten, as the reference scales 3 to 18. No roll scores nothing.</summary>
        private double DiceScore()
        {
            // The whistle lands a tumbling roll where it was headed: the dice were already thrown.
            KeptTotal = RollsUsed == 0 ? 0 : RollTotal;
            return CompetitionMiniGames.DiceScore(KeptTotal);
        }
    }
}
