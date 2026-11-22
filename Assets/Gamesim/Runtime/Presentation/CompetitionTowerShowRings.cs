using System;
using System.Collections.Generic;

namespace Gamesim.Presentation
{
    /// <summary>
    /// The rings of a ring tower show (competition drop-outs and tower show): where the yard's tower stands
    /// at each moment of the show, played from the show's own beats. The tower is one competitor's - the
    /// player's when they compete, otherwise the winner's - and it works through the fewest moves from the
    /// first stack to the last, finishing on that competitor's finish beat; time called on them leaves it
    /// two thirds done. Read from the committed standings through the show's timeline only: no season
    /// state, no random draw. Pure: names no Unity type.
    /// </summary>
    public sealed class CompetitionTowerShowRings
    {
        /// <summary>Seconds into the work before the first ring is lifted.</summary>
        public const float LeadSeconds = .4f;

        private readonly IReadOnlyList<(int from, int to)> solution;
        private RingTowerPuzzle shown;
        private int shownMoves = -1;
        private bool shownLifted;

        private CompetitionTowerShowRings(string subjectId, bool finishes, float startAt, float doneAt, int rings)
        {
            SubjectId = subjectId; Finishes = finishes; StartAt = startAt; DoneAt = doneAt; Rings = rings;
            solution = RingTowerPuzzle.Solution(rings);
            Minimum = solution.Count;
            Target = finishes ? Minimum : UnfinishedMoves(Minimum);
        }

        /// <summary>Whose tower the stacks show.</summary>
        public string SubjectId { get; }

        /// <summary>Whether they finish it (otherwise time is called on them).</summary>
        public bool Finishes { get; }

        /// <summary>When the first move starts and when the last one shown is done, seconds into the show's beats.</summary>
        public float StartAt { get; }
        public float DoneAt { get; }

        public int Rings { get; }

        /// <summary>The fewest moves that solve the tower.</summary>
        public int Minimum { get; }

        /// <summary>How many moves the show plays: all of them for a finisher.</summary>
        public int Target { get; }

        /// <summary>The moves an unfinished tower shows when time is called: two thirds of the way.</summary>
        public static int UnfinishedMoves(int minimum) => minimum * 2 / 3;

        /// <summary>
        /// The rings for <paramref name="timeline"/>, a tower show's, following <paramref name="subjectId"/>;
        /// null when there is no timeline or the subject has no finish or time-up beat in it.
        /// </summary>
        public static CompetitionTowerShowRings For(CompetitionShowTimeline timeline, string subjectId, int rings = RingTowerPuzzle.StandardRings)
        {
            if (timeline == null || subjectId == null) return null;
            float start = float.NaN, done = float.NaN;
            bool finishes = false;
            foreach (var beat in timeline.Beats)
            {
                if (beat.Kind == CompetitionShowBeatKind.Work && float.IsNaN(start)) start = beat.At;
                if ((beat.Kind == CompetitionShowBeatKind.Finish || beat.Kind == CompetitionShowBeatKind.TimeUp)
                    && beat.Ids.Count > 0 && beat.Ids[0] == subjectId && float.IsNaN(done))
                { done = beat.At; finishes = beat.Kind == CompetitionShowBeatKind.Finish; }
            }
            if (float.IsNaN(start) || float.IsNaN(done) || done <= start) return null;
            return new CompetitionTowerShowRings(subjectId, finishes, start + LeadSeconds, done, rings);
        }

        /// <summary>How many moves are done <paramref name="t"/> seconds into the beats: none before the start, <see cref="Target"/> from the end.</summary>
        public int MovesAt(float t)
        {
            if (Target <= 0 || t <= StartAt) return 0;
            if (t >= DoneAt) return Target;
            return Math.Min(Target, (int)Math.Floor((t - StartAt) / (DoneAt - StartAt) * Target));
        }

        /// <summary>Whether the next move's ring is in the air at <paramref name="t"/>: the second half of each move's time.</summary>
        public bool LiftedAt(float t)
        {
            int moves = MovesAt(t);
            if (moves >= Target || t <= StartAt) return false;
            float step = (DoneAt - StartAt) / Target;
            float into = (t - StartAt) - moves * step;
            return into >= step * .5f;
        }

        /// <summary>
        /// The tower at <paramref name="t"/>: the moves done applied from the first stack, and the next
        /// move's ring lifted when it is in the air. The same instance while nothing changes.
        /// </summary>
        public RingTowerPuzzle PuzzleAt(float t)
        {
            int moves = MovesAt(t);
            bool lifted = LiftedAt(t);
            if (shown != null && moves == shownMoves && lifted == shownLifted) return shown;
            var puzzle = new RingTowerPuzzle(Rings);
            for (int i = 0; i < moves; i++) puzzle.TryMove(solution[i].from, solution[i].to);
            if (lifted && moves < solution.Count) puzzle.Pick(solution[moves].from);
            shown = puzzle; shownMoves = moves; shownLifted = lifted;
            return shown;
        }
    }
}
