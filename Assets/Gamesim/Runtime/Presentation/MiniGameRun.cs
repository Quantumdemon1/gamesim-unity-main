using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;

namespace Gamesim.Presentation
{
    /// <summary>
    /// One attempt at a competition minigame, as data rather than as a screen.
    ///
    /// <para><see cref="CompetitionMiniGames"/> holds the scoring; this holds the playing. Keeping it
    /// out of the director means the behaviour can be driven frame by frame in a test — a target
    /// expiring, a grip giving out, a board being cleared — instead of only through a coroutine
    /// nobody can step through.</para>
    ///
    /// <para><b>It never touches the season's generator.</b> Where a board is shuffled or a target
    /// placed, the draw comes from a generator this run owns, seeded by the caller. A minigame is
    /// presentation: its draws are not part of the recorded command, and spending
    /// <c>EpisodeState.randomState</c> on them would re-roll the rest of the season — the same
    /// reason <c>SeasonBuilder</c> takes the cast in table order rather than shuffling it.</para>
    /// </summary>
    public sealed class MiniGameRun
    {
        /// <summary>How long a target stays up, and the gap before the next. The reference's numbers.</summary>
        public const double TargetLife = 0.9, TargetGapLow = 0.1, TargetGapHigh = 0.3;

        /// <summary>The reference's opening pause before the first target.</summary>
        public const double FirstTargetDelay = 0.5;

        /// <summary>How long a mismatched pair stays face up before turning back.</summary>
        public const double FlipBackDelay = 1.0;

        public CompetitionMiniGames.Kind Kind { get; }
        public double TimeLimit { get; }
        public double Elapsed { get; private set; }
        public double Remaining => Math.Max(0, TimeLimit - Elapsed);
        public bool Finished { get; private set; }

        /// <summary>The 0–10 score, once <see cref="Finished"/>. Zero before that.</summary>
        public double Score { get; private set; }

        /// <summary>What the <c>Compete</c> command should carry.</summary>
        public double Performance => CompetitionMiniGames.Performance(Score);

        private readonly SeededRandom random;

        // --- endurance
        public double Meter { get; private set; } = CompetitionMiniGames.MeterFull;
        public double Held { get; private set; }
        public bool Holding { get; private set; }

        // --- reaction
        public bool TargetLive { get; private set; }
        public int Hits { get; private set; }
        public int Spawned { get; private set; }

        /// <summary>Where the current target sits, each 0–1 across the play area.</summary>
        public double TargetX { get; private set; }
        public double TargetY { get; private set; }
        private double nextTargetAt, targetExpiresAt;

        // --- memory
        private readonly List<int> faces = new List<int>();
        private readonly List<bool> matched = new List<bool>();
        public IReadOnlyList<int> Faces => faces;
        public IReadOnlyList<bool> Matched => matched;
        public int Pairs { get; private set; }
        public int MatchedPairs { get; private set; }
        public int WrongFlips { get; private set; }

        /// <summary>The cards currently face up and unmatched, or −1 where none.</summary>
        public int FirstFlip { get; private set; } = -1;
        public int SecondFlip { get; private set; } = -1;
        private double flipBackAt;

        public MiniGameRun(CompetitionMiniGames.Kind kind, uint seed)
        {
            Kind = kind;
            TimeLimit = CompetitionMiniGames.TimeLimit(kind);
            random = new SeededRandom(seed);
            if (kind == CompetitionMiniGames.Kind.Memory) Deal();
            if (kind == CompetitionMiniGames.Kind.Reaction) nextTargetAt = FirstTargetDelay;
        }

        // ---------------------------------------------------------------- the clock

        /// <summary>
        /// Advances the run by one frame.
        ///
        /// <para>Everything timed is timed off <see cref="Elapsed"/>, which the caller supplies. That
        /// is what lets a test play a whole thirty-second competition in a loop without waiting
        /// thirty seconds, and it is why nothing in here reads a clock of its own.</para>
        /// </summary>
        public void Tick(double delta)
        {
            if (Finished || delta <= 0) return;
            Elapsed += delta;

            switch (Kind)
            {
                case CompetitionMiniGames.Kind.Endurance:
                    if (Holding) Held += delta;
                    Meter = CompetitionMiniGames.MeterAfter(Meter, Elapsed, TimeLimit, delta, Holding);
                    // The grip giving out ends it early, which is the point of letting go sparingly.
                    if (Meter <= CompetitionMiniGames.MeterEmpty) { Finish(); return; }
                    break;

                case CompetitionMiniGames.Kind.Reaction:
                    if (TargetLive && Elapsed >= targetExpiresAt)
                    {
                        // An expired target is a miss. It was already counted as spawned, so the
                        // accuracy it feeds has nothing further to do.
                        TargetLive = false;
                        nextTargetAt = Elapsed + Gap();
                    }
                    else if (!TargetLive && Elapsed >= nextTargetAt) Spawn();
                    break;

                case CompetitionMiniGames.Kind.Memory:
                    if (SecondFlip >= 0 && Elapsed >= flipBackAt) TurnBack();
                    break;
            }

            if (TimeLimit > 0 && Elapsed >= TimeLimit) Finish();
        }

        /// <summary>Ends the run where it stands and works out what it was worth.</summary>
        public void Finish()
        {
            if (Finished) return;
            Finished = true;
            switch (Kind)
            {
                case CompetitionMiniGames.Kind.Endurance:
                    Score = CompetitionMiniGames.EnduranceScore(Held, TimeLimit); break;
                case CompetitionMiniGames.Kind.Reaction:
                    Score = CompetitionMiniGames.ReactionScore(Hits, Spawned); break;
                case CompetitionMiniGames.Kind.Memory:
                    Score = CompetitionMiniGames.MemoryScore(MatchedPairs, Pairs, WrongFlips, Remaining, TimeLimit);
                    break;
            }
        }

        // ---------------------------------------------------------------- what the player does

        /// <summary>Grabbing on, or letting go. Endurance only.</summary>
        public void SetHolding(bool holding)
        {
            if (Finished || Kind != CompetitionMiniGames.Kind.Endurance) return;
            Holding = holding;
        }

        /// <summary>
        /// A tap. Reaction only.
        ///
        /// <para>A tap with nothing up does nothing at all — it is not a miss. The reference counts
        /// accuracy against targets that appeared, so punishing an early tap twice would be a rule
        /// it does not have.</para>
        /// </summary>
        public bool Tap()
        {
            if (Finished || Kind != CompetitionMiniGames.Kind.Reaction || !TargetLive) return false;
            TargetLive = false;
            Hits++;
            nextTargetAt = Elapsed + Gap();
            return true;
        }

        /// <summary>
        /// Turning a card over. Memory only.
        ///
        /// <para>Returns false for anything that is not a legal flip — a matched card, one already
        /// face up, or a third card while two are still being compared. The last is the lock the
        /// reference holds while a mismatched pair is showing, and without it a fast clicker could
        /// flip the whole board before it turned back.</para>
        /// </summary>
        public bool Flip(int index)
        {
            if (Finished || Kind != CompetitionMiniGames.Kind.Memory) return false;
            if (index < 0 || index >= faces.Count) return false;
            if (matched[index] || index == FirstFlip || SecondFlip >= 0) return false;

            if (FirstFlip < 0) { FirstFlip = index; return true; }

            SecondFlip = index;
            if (faces[FirstFlip] == faces[SecondFlip])
            {
                matched[FirstFlip] = true;
                matched[SecondFlip] = true;
                MatchedPairs++;
                FirstFlip = -1;
                SecondFlip = -1;
                // Clearing the board ends it now rather than at the whistle, which is what makes
                // the time bonus worth chasing.
                if (MatchedPairs >= Pairs) Finish();
                return true;
            }

            WrongFlips++;
            flipBackAt = Elapsed + FlipBackDelay;
            return true;
        }

        // ---------------------------------------------------------------- internals

        private void TurnBack()
        {
            FirstFlip = -1;
            SecondFlip = -1;
        }

        private double Gap() => TargetGapLow + random.NextDouble() * (TargetGapHigh - TargetGapLow);

        private void Spawn()
        {
            TargetLive = true;
            Spawned++;
            TargetX = random.NextDouble();
            TargetY = random.NextDouble();
            targetExpiresAt = Elapsed + TargetLife;
        }

        /// <summary>
        /// Lays out a shuffled board of pairs.
        ///
        /// <para>A Fisher–Yates shuffle over this run's own generator. Two of every face, so a board
        /// is always solvable — a shuffle that could deal an odd face would make the clock, rather
        /// than the player, decide the score.</para>
        /// </summary>
        private void Deal()
        {
            Pairs = CompetitionMiniGames.MemoryPairs;
            for (int face = 0; face < Pairs; face++) { faces.Add(face); faces.Add(face); }
            for (int i = faces.Count - 1; i > 0; i--)
            {
                int j = (int)(random.NextDouble() * (i + 1));
                if (j > i) j = i;
                (faces[i], faces[j]) = (faces[j], faces[i]);
            }
            matched.AddRange(Enumerable.Repeat(false, faces.Count));
        }
    }
}
