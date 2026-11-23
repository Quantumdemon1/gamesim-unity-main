using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Presentation
{
    /// <summary>What happens at a beat of a staged show (competition staging, slice 3).</summary>
    public enum CompetitionShowBeatKind
    {
        /// <summary>Podium: a question goes up on the display; those still in look up at it.</summary>
        Question,
        /// <summary>Podium: those still in press their console to lock their answer in.</summary>
        LockIn,
        /// <summary>Podium: the answers are revealed; <see cref="CompetitionShowBeat.Ids"/> were wrong and are out.</summary>
        Reveal,
        /// <summary>Table: everyone sets to work at their tray or console.</summary>
        Work,
        /// <summary>Table: <see cref="CompetitionShowBeat.Ids"/> (one) finishes, in the standings' order, the winner first.</summary>
        Finish,
        /// <summary>Table: time is called on the last place, still working.</summary>
        TimeUp,
        /// <summary>Course: everyone slaps the pad to start.</summary>
        Go,
        /// <summary>Course: everyone runs out to their turn mark.</summary>
        Depart,
        /// <summary>Course: <see cref="CompetitionShowBeat.Ids"/> (one) turns for home, in the standings' order.</summary>
        Turn,
        /// <summary>The winner celebrates at their own place.</summary>
        Cheer,
        /// <summary>Table and course: the field turns to look at the winner before the card.</summary>
        TurnToWinner,
        /// <summary>The standings card plays.</summary>
        Card,
    }

    /// <summary>One beat: when, from the start of the hold, in unscaled seconds; what; and who.</summary>
    public sealed class CompetitionShowBeat
    {
        public float At { get; }
        public CompetitionShowBeatKind Kind { get; }
        /// <summary>The podium's round, from 0; -1 elsewhere.</summary>
        public int Round { get; }
        /// <summary>Who the beat is about, in the standings' order where it names several (a reveal: bottom first).</summary>
        public IReadOnlyList<string> Ids { get; }

        public CompetitionShowBeat(float at, CompetitionShowBeatKind kind, int round, IReadOnlyList<string> ids)
        { At = at; Kind = kind; Round = round; Ids = ids ?? Array.Empty<string>(); }

        public override string ToString() => At.ToString("0.00") + " " + Kind + (Round >= 0 ? " r" + Round : "") + " [" + string.Join(",", Ids) + "]";
    }

    /// <summary>
    /// The beats of a podium, table or course show (competition staging, slice 3), read from the
    /// committed standings of those at their stations when the hold starts, best first. Nothing is
    /// decided here: every beat's order is the standings' own, so the show tells the committed result.
    /// Pure presentation: no Unity type, no season state, no random draw - any variety comes from the
    /// standings and their size.
    ///
    /// <para>The course's homecomings are not timed beats: a runner finishes on reaching their post,
    /// in the standings' order, by <see cref="FinishBy"/>; one not home by then does not finish and is
    /// never put ahead of anyone.</para>
    /// </summary>
    public sealed class CompetitionShowTimeline
    {
        /// <summary>The most podium rounds a show plays: a big field loses several at a reveal.</summary>
        public const int MostRounds = 5;

        /// <summary>A podium round: the question, the lock-in at .35 of it, the reveal at .7.</summary>
        public static float RoundSeconds(bool quick) => quick ? 1.2f : 2f;

        /// <summary>The table's first finish - the winner's - after this long at work.</summary>
        public static float WorkSeconds(bool quick) => quick ? 1.5f : 2.5f;

        /// <summary>
        /// Ring tower: the first finish - the winner's - after this long at the stacks: long enough for the
        /// tower's fifteen moves to be seen (competition drop-outs and tower show).
        /// </summary>
        public static float TowerWorkSeconds(bool quick) => quick ? 5f : 8f;

        /// <summary>Course: from the start to the run out, while the pads are slapped.</summary>
        public static float GoSeconds(bool quick) => quick ? .35f : .5f;

        /// <summary>Course: how long the run out to the turn marks is given before the first turns for home.</summary>
        public const float LegOutSeconds = 2.6f;

        /// <summary>Course: how long a runner has from their turn to reach their post and finish.</summary>
        public const float LegBackSeconds = 3.5f;

        /// <summary>Table and course: the field turned to the winner, before the card.</summary>
        public static float WinnerHoldSeconds(bool quick) => quick ? 1f : 1.6f;

        /// <summary>The seconds between finishes (table) or turns (course): a shared window, quicker for a bigger field.</summary>
        public static float Spacing(CompetitionShowFamily family, int gaps, bool quick)
        {
            if (gaps <= 0) return 0f;
            float each = family == CompetitionShowFamily.Course ? .5f : .8f;
            float least = family == CompetitionShowFamily.Course ? 1.2f : 2f;
            float most = family == CompetitionShowFamily.Course ? 4f : 5f;
            float window = gaps * each;
            if (window < least) window = least;
            if (window > most) window = most;
            if (quick) window *= .6f;
            return window / gaps;
        }

        /// <summary>
        /// How many go out at each podium reveal, first round first, for <paramref name="drops"/> who
        /// go out in all: at most <see cref="MostRounds"/> rounds, the runner-up alone in the last,
        /// the rest shared out with the earlier rounds taking any extra.
        /// </summary>
        public static IReadOnlyList<int> RoundSizes(int drops)
        {
            var sizes = new List<int>();
            if (drops <= 0) return sizes;
            int rounds = Math.Min(drops, MostRounds);
            int rest = drops - 1, earlier = rounds - 1;
            for (int r = 0; r < earlier; r++) sizes.Add(rest / earlier + (r < rest % earlier ? 1 : 0));
            sizes.Add(1);
            return sizes;
        }

        private CompetitionShowTimeline(CompetitionShowFamily family, IReadOnlyList<string> standings, bool quick)
        {
            Family = family; Standings = standings; Quick = quick;
        }

        public CompetitionShowFamily Family { get; }

        /// <summary>Those staged, best first.</summary>
        public IReadOnlyList<string> Standings { get; }

        public bool Quick { get; }

        public string WinnerId => Standings[0];

        private readonly List<CompetitionShowBeat> beats = new List<CompetitionShowBeat>();
        private readonly Dictionary<string, float> finishBy = new Dictionary<string, float>();

        /// <summary>Every timed beat, in time order (ties in the order they happen).</summary>
        public IReadOnlyList<CompetitionShowBeat> Beats => beats;

        /// <summary>When, from the start of the hold, the standings card plays at the latest.</summary>
        public float CardAt { get; private set; }

        /// <summary>
        /// The order the show resolves its competitors in: the podium's outs from the bottom, then the
        /// winner; the table's and the course's finishes from the top (the table's last place last, on
        /// time being called).
        /// </summary>
        public IReadOnlyList<string> Order { get; private set; }

        /// <summary>Course: the latest a runner is meant to reach their post and finish; NaN for anyone not running.</summary>
        public float FinishBy(string id) => id != null && finishBy.TryGetValue(id, out var at) ? at : float.NaN;

        /// <summary>
        /// Course: how long past <see cref="FinishBy"/> a runner still out on their leg is waited for.
        /// A late runner finishes their leg and slaps the pad in their place; those below them wait at
        /// their posts, so the order stays the committed one.
        /// </summary>
        public const float LateGraceSeconds = 2.5f;

        /// <summary>Course: past this a runner not home does not finish (stuck, or with no route home); NaN for anyone not running.</summary>
        public float LateBy(string id) => FinishBy(id) + LateGraceSeconds;

        /// <summary>The latest the card plays: a course waits out its late runners' grace; the other families' card is <see cref="CardAt"/>.</summary>
        public float LatestCardAt => Family == CompetitionShowFamily.Course ? CardAt + LateGraceSeconds : CardAt;

        /// <summary>Course: when <paramref name="id"/> turns for home; NaN for anyone not running.</summary>
        public float TurnAt(string id)
        {
            foreach (var beat in beats)
                if (beat.Kind == CompetitionShowBeatKind.Turn && beat.Ids.Count > 0 && beat.Ids[0] == id) return beat.At;
            return float.NaN;
        }

        /// <summary>What the course does with the next runner in the standings, read each frame.</summary>
        public enum CourseCall
        {
            /// <summary>Not yet: still on their leg, or home before their turn would read in time.</summary>
            Wait,
            /// <summary>They finish now: home at their post, in their place.</summary>
            Finish,
            /// <summary>Past their grace and still not home: they do not finish, and the next is looked at.</summary>
            DidNotFinish,
        }

        /// <summary>
        /// Course: the call on <paramref name="id"/>, the next in the standings to finish, at
        /// <paramref name="t"/> seconds into the hold. <paramref name="home"/>: they stand at their post
        /// (they never left it, or ran their leg and are back); <paramref name="homeward"/>: they turned
        /// for home. One who never left their post finishes no sooner than a second after their turn, so
        /// the order still reads in time; one home from their leg finishes at once. One not home is
        /// waited for until <see cref="LateBy"/>, then does not finish.
        /// </summary>
        public CourseCall CallFor(string id, float t, bool home, bool homeward)
        {
            if (home)
            {
                float turn = TurnAt(id);
                float earliest = homeward || float.IsNaN(turn) ? 0f : turn + 1f;
                if (t >= earliest) return CourseCall.Finish;
                return CourseCall.Wait;
            }
            return t > LateBy(id) ? CourseCall.DidNotFinish : CourseCall.Wait;
        }

        /// <summary>
        /// Course: whether the field's turn to the winner and the card wait, with <paramref name="nextHome"/>
        /// of the standings settled (finished or not finishing): while anyone is still to come home in
        /// their grace. The other families never wait.
        /// </summary>
        public bool CardWaits(int nextHome) => Family == CompetitionShowFamily.Course && nextHome < Standings.Count;

        /// <summary>
        /// The timeline of a <paramref name="family"/> show for those staged, best first (at least
        /// two); null for the hold, which slice 1 times (<see cref="CompetitionShowPlan"/>), or too few.
        /// </summary>
        public static CompetitionShowTimeline For(CompetitionShowFamily family, IReadOnlyList<string> standings, bool quick)
        {
            if (family == CompetitionShowFamily.Hold || standings == null) return null;
            var field = standings.Where(id => id != null).Distinct().ToList();
            if (field.Count < 2) return null;
            var timeline = new CompetitionShowTimeline(family, field, quick);
            switch (family)
            {
                case CompetitionShowFamily.Podium: timeline.BuildPodium(); break;
                case CompetitionShowFamily.Table: timeline.BuildTable(); break;
                // The ring tower plays the table's beats - all at work, finishes in the standings' order, time
                // called on the last - with longer at work, for the tower's moves to be seen.
                case CompetitionShowFamily.Tower: timeline.BuildTable(); break;
                case CompetitionShowFamily.Course: timeline.BuildCourse(); break;
            }
            return timeline;
        }

        private void Add(float at, CompetitionShowBeatKind kind, int round, params string[] ids)
            => beats.Add(new CompetitionShowBeat(at, kind, round, ids));

        private void BuildPodium()
        {
            var bottomUp = Standings.Skip(1).Reverse().ToList();
            var sizes = RoundSizes(bottomUp.Count);
            float round = RoundSeconds(Quick);
            var still = Standings.ToList();
            int taken = 0;
            for (int r = 0; r < sizes.Count; r++)
            {
                float start = r * round;
                // Those still in, best first: who looks up and who locks in.
                Add(start, CompetitionShowBeatKind.Question, r, still.ToArray());
                Add(start + round * .35f, CompetitionShowBeatKind.LockIn, r, still.ToArray());
                var outs = bottomUp.Skip(taken).Take(sizes[r]).ToArray();
                taken += outs.Length;
                Add(start + round * .7f, CompetitionShowBeatKind.Reveal, r, outs);
                foreach (var id in outs) still.Remove(id);
            }
            float cheer = sizes.Count * round;
            Add(cheer, CompetitionShowBeatKind.Cheer, -1, WinnerId);
            CardAt = cheer + CompetitionShowPlan.CheerSeconds(Quick);
            Add(CardAt, CompetitionShowBeatKind.Card, -1);
            Order = bottomUp.Concat(new[] { WinnerId }).ToList();
        }

        private void BuildTable()
        {
            int last = Standings.Count - 1;
            float work = Family == CompetitionShowFamily.Tower ? TowerWorkSeconds(Quick) : WorkSeconds(Quick);
            float spacing = Spacing(CompetitionShowFamily.Table, last, Quick);
            Add(0f, CompetitionShowBeatKind.Work, -1, Standings.ToArray());
            for (int i = 0; i < last; i++)
            {
                Add(work + spacing * i, CompetitionShowBeatKind.Finish, -1, Standings[i]);
                if (i == 0) Add(work, CompetitionShowBeatKind.Cheer, -1, WinnerId);
            }
            float timeUp = work + spacing * last;
            Add(timeUp, CompetitionShowBeatKind.TimeUp, -1, Standings[last]);
            Add(timeUp, CompetitionShowBeatKind.TurnToWinner, -1, WinnerId);
            CardAt = timeUp + WinnerHoldSeconds(Quick);
            Add(CardAt, CompetitionShowBeatKind.Card, -1);
            Order = Standings.ToList();
        }

        private void BuildCourse()
        {
            float go = GoSeconds(Quick), spacing = Spacing(CompetitionShowFamily.Course, Standings.Count - 1, Quick);
            Add(0f, CompetitionShowBeatKind.Go, -1, Standings.ToArray());
            Add(go, CompetitionShowBeatKind.Depart, -1, Standings.ToArray());
            float lastBy = 0f;
            for (int i = 0; i < Standings.Count; i++)
            {
                float turn = go + LegOutSeconds + spacing * i;
                Add(turn, CompetitionShowBeatKind.Turn, -1, Standings[i]);
                finishBy[Standings[i]] = lastBy = turn + LegBackSeconds;
            }
            Add(lastBy, CompetitionShowBeatKind.TurnToWinner, -1, WinnerId);
            CardAt = lastBy + WinnerHoldSeconds(Quick);
            Add(CardAt, CompetitionShowBeatKind.Card, -1);
            Order = Standings.ToList();
        }
    }
}
