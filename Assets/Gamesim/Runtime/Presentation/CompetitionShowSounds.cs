using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Presentation
{
    /// <summary>
    /// What a staged competition in the yard sounds like (AU-13): the sounds each beat of a podium,
    /// table, tower or course show asks for (<see cref="CompetitionShowTimeline"/>), by the cue's name.
    /// A course starts on the horn as the pads are slapped, and a table or the tower as work starts;
    /// time called on the last place is the horn again. A podium's consoles click in as the hands
    /// press them, staggered as the presses are, once for each moment a hand comes down; each reveal
    /// sounds once - right or wrong - not once a console. A finisher's last press clicks
    /// (<see cref="Finished"/>). A question, the run out, the turns, the cheer and the turn to the
    /// winner are silent: the standings card that follows has its own fanfare.
    ///
    /// <para>Presentation only, and Unity-free: every answer comes from the committed standings the
    /// show plays and the player's id, with no draw from anything.</para>
    /// </summary>
    public static class CompetitionShowSounds
    {
        /// <summary>The cues' names (rows in <see cref="SoundPictures"/>, values of HouseAudio.Cue).</summary>
        public const string Horn = "ShowHorn", LockIn = "ShowLockIn", Right = "ShowRight", Wrong = "ShowWrong";

        /// <summary>A competitor finishing at a table, the tower or the course: their last press clicks.</summary>
        public const string Finished = LockIn;

        /// <summary>Every cue a show asks for, in the enum's order.</summary>
        public static IReadOnlyList<string> Cues { get; } = new[] { Horn, LockIn, Right, Wrong };

        /// <summary>One sound a beat asks for: the cue's name, and how long after the beat it sounds, in unscaled seconds.</summary>
        public sealed class Ask
        {
            public string Cue { get; }
            public float After { get; }
            public Ask(string cue, float after) { Cue = cue; After = after; }
            public override string ToString() => Cue + " +" + After.ToString("0.00");
        }

        private static readonly Ask[] None = new Ask[0];

        /// <summary>
        /// How long after a lock-in a competitor's hand comes down on their console: a moment after
        /// the one above them in the standings, four steps round, so the field does not press as one.
        /// </summary>
        public static float LockInDelay(int rank) => rank < 0 ? 0f : rank % 4 * .08f;

        /// <summary>
        /// The sounds <paramref name="beat"/> of <paramref name="timeline"/> asks for, soonest first.
        /// <paramref name="playerId"/> is the player's houseguest (a podium reveal sounds their verdict
        /// while they are in it), or null.
        /// </summary>
        public static IReadOnlyList<Ask> For(CompetitionShowTimeline timeline, CompetitionShowBeat beat, string playerId)
        {
            if (timeline == null || beat == null) return None;
            switch (beat.Kind)
            {
                case CompetitionShowBeatKind.Go:
                case CompetitionShowBeatKind.Work:
                case CompetitionShowBeatKind.TimeUp:
                    return new[] { new Ask(Horn, 0f) };
                case CompetitionShowBeatKind.LockIn:
                    return beat.Ids
                        .Select(id => LockInDelay(IndexOf(timeline.Standings, id)))
                        .Distinct().OrderBy(after => after)
                        .Select(after => new Ask(LockIn, after)).ToArray();
                case CompetitionShowBeatKind.Reveal:
                    return new[] { new Ask(Verdict(timeline, beat, playerId), 0f) };
                default:
                    return None;
            }
        }

        /// <summary>
        /// The one sound a podium reveal makes: the player's own verdict while they are still in -
        /// right as they come through, wrong as they go out; once they are out, or when they never
        /// competed, right for the reveal that leaves the winner alone and wrong for every other.
        /// </summary>
        public static string Verdict(CompetitionShowTimeline timeline, CompetitionShowBeat reveal, string playerId)
        {
            var still = StillIn(timeline, reveal.Round);
            if (playerId != null && still.Contains(playerId))
                return reveal.Ids.Contains(playerId) ? Wrong : Right;
            return still.Count - reveal.Ids.Count <= 1 ? Right : Wrong;
        }

        /// <summary>Those still in a podium round, best first: the ones its lock-in names.</summary>
        private static IReadOnlyList<string> StillIn(CompetitionShowTimeline timeline, int round)
        {
            foreach (var beat in timeline.Beats)
                if (beat.Kind == CompetitionShowBeatKind.LockIn && beat.Round == round) return beat.Ids;
            return Array.Empty<string>();
        }

        private static int IndexOf(IReadOnlyList<string> list, string id)
        {
            for (int i = 0; i < list.Count; i++)
                if (string.Equals(list[i], id, StringComparison.Ordinal)) return i;
            return -1;
        }
    }
}
