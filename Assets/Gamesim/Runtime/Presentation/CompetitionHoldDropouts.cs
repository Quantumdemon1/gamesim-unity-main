using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;

namespace Gamesim.Presentation
{
    /// <summary>
    /// The other competitors letting go around the player in the ranked endurance game (competition
    /// drop-outs). The housemates' scores are rolled only when the competition commits, so the game
    /// reads them beforehand from a preview: the very command the commit will run - the same state,
    /// the same command - applied by a separate engine to a copy of the season. The copy draws its
    /// own rolls; the season's own state, its generator and the commit itself are never touched, so
    /// a season plays and replays byte for byte whether or not anyone is shown letting go.
    ///
    /// <para><b>Why what the player sees agrees with the standings.</b> In a weekly competition a
    /// housemate's score does not depend on the player's performance (each competitor's roll is drawn
    /// in the field's order whatever the player did), and the player's own score only grows with it -
    /// the performance is a bonus added on top. Effort banked in the hold only grows too. So a
    /// housemate shown letting go once the player's score at the effort banked so far is above theirs
    /// stays below the player whenever the player lets go; and they let go from the bottom of the
    /// standings up, so nobody still holding is ranked under anybody who has let go. When the player's
    /// attempt ends, everybody the commit ranks under the player lets go with them, read from a preview
    /// of the exact performance the commit will carry.</para>
    ///
    /// <para><b>Where that cannot be promised</b> - the final Head of Household's first part, whose drop
    /// times are run with the player's effort inside them, or any preview whose housemates' scores move
    /// with the performance - nobody lets go while the player holds on: the only drop-outs shown are the
    /// ones at the player's own let-go, read from the exact preview, which cannot contradict the result.
    /// A preview the engine refuses shows no drop-outs at all.</para>
    ///
    /// <para>Pure: names no Unity type, so the Unity-free suite runs it as the editor does.</para>
    /// </summary>
    public sealed class CompetitionHoldDropouts
    {
        /// <summary>How far the player's score must be above a housemate's before they are shown letting go.</summary>
        public const double Margin = 1e-6;

        /// <summary>Seconds into the hold before anybody lets go: the field gets a grip first, and the camera its close shot.</summary>
        public const double FirstDropSeconds = 5.0;

        /// <summary>The least seconds between two drop-outs while the player holds on, so each one reads.</summary>
        public const double DropGapSeconds = .9;

        private readonly EpisodeState origin;
        private readonly string commandId;
        private readonly List<string> ascending;
        private readonly Dictionary<string, double> scores;
        private readonly List<string> dropped = new List<string>();
        private readonly double atNone, atFull;
        private double nextAt = FirstDropSeconds;
        private bool letGo;

        private CompetitionHoldDropouts(EpisodeState origin, string commandId, List<string> ascending, Dictionary<string, double> scores,
            double atNone, double atFull, bool live)
        {
            this.origin = origin; this.commandId = commandId; this.ascending = ascending; this.scores = scores;
            this.atNone = atNone; this.atFull = atFull; Live = live;
        }

        /// <summary>Whether housemates let go while the player holds on; false where agreement cannot be promised.</summary>
        public bool Live { get; }

        /// <summary>Who has let go so far, in the order they did.</summary>
        public IReadOnlyList<string> Dropped => dropped;

        /// <summary>Whether the player's own let-go has been played.</summary>
        public bool LetGoPlayed => letGo;

        /// <summary>The housemates in the field, lowest first: the order they let go in.</summary>
        public IReadOnlyList<string> Ascending => ascending;

        /// <summary>How many housemates still hold on.</summary>
        public int StillHolding => ascending.Count - dropped.Count;

        /// <summary>
        /// The drop-outs for a ranked attempt at the competition open in <paramref name="origin"/>, the
        /// state its Compete command will be submitted against with <paramref name="commandId"/>; null
        /// when there is nothing to show (not a competition the player is in, or a preview the engine
        /// refuses). <paramref name="origin"/> is copied and never written.
        /// </summary>
        public static CompetitionHoldDropouts Preview(EpisodeState origin, string commandId)
        {
            if (origin == null || !EpisodeEngine.IsCompetition(origin.phase) || origin.competitionResolved) return null;
            var copy = origin.Clone();
            if (!EpisodeEngine.CompetitionPlayers(copy).Any(actor => actor.id == copy.playerId)) return null;
            var none = Scores(copy, commandId, 0d);
            var half = Scores(copy, commandId, .5d);
            var full = Scores(copy, commandId, 1d);
            if (none == null || half == null || full == null) return null;
            int you = none.FindIndex(entry => entry.contestantId == copy.playerId);
            if (you < 0) return null;
            var npcs = none.Select((entry, index) => (entry, index)).Where(pair => pair.index != you).ToList();
            double s0 = none[you].score, s1 = Score(full, copy.playerId), sHalf = Score(half, copy.playerId);
            // Live only where a housemate's score is the same whatever the player does, and the player's own
            // score climbs with the performance in a straight line.
            bool live = copy.phase != EpisodePhase.FinalHoHPart1
                && SameHousemates(none, half, copy.playerId) && SameHousemates(none, full, copy.playerId)
                && !double.IsNaN(s1) && !double.IsNaN(sHalf) && s1 >= s0
                && Math.Abs(sHalf - (s0 + s1) / 2) <= 1e-9 * Math.Max(1, Math.Abs(s1));
            // Lowest first; of two equal scores the one written later is ranked lower (the standings sort is stable).
            var ascending = npcs.OrderBy(pair => pair.entry.score).ThenByDescending(pair => pair.index).Select(pair => pair.entry.contestantId).ToList();
            var scores = npcs.ToDictionary(pair => pair.entry.contestantId, pair => pair.entry.score);
            return new CompetitionHoldDropouts(copy, commandId, ascending, scores, s0, s1, live);
        }

        /// <summary>
        /// Who lets go now - at most one a call - with the player's effort worth <paramref name="performance"/>
        /// so far (the Compete command's 0 to 1, which only grows through an attempt) and
        /// <paramref name="seconds"/> into the hold. Nobody while not <see cref="Live"/> or after the let-go.
        /// </summary>
        public IReadOnlyList<string> Due(double performance, double seconds)
        {
            if (!Live || letGo || dropped.Count >= ascending.Count || seconds < nextAt
                || double.IsNaN(performance) || performance < 0) return Array.Empty<string>();
            double p = Math.Min(1, performance);
            double you = atNone + p * (atFull - atNone);
            string next = ascending[dropped.Count];
            if (!(scores[next] + Margin < you)) return Array.Empty<string>();
            dropped.Add(next);
            nextAt = seconds + DropGapSeconds;
            return new[] { next };
        }

        /// <summary>
        /// The player lets go with <paramref name="finalPerformance"/>, the performance the commit will
        /// carry: everybody the commit will rank under the player and still holding lets go with them,
        /// lowest first. Read from a preview of exactly that command. Once only; empty when the preview
        /// is refused.
        /// </summary>
        public IReadOnlyList<string> LetGo(double finalPerformance)
        {
            if (letGo) return Array.Empty<string>();
            letGo = true;
            var standings = Standings(finalPerformance);
            if (standings == null) return Array.Empty<string>();
            int you = standings.IndexOf(origin.playerId);
            if (you < 0) return Array.Empty<string>();
            var under = new HashSet<string>(standings.Skip(you + 1));
            var now = ascending.Where(id => under.Contains(id) && !dropped.Contains(id)).ToList();
            dropped.AddRange(now);
            return now;
        }

        /// <summary>The standings the commit would write with <paramref name="performance"/>, best first; null when refused.</summary>
        public IReadOnlyList<string> StandingsAt(double performance) => Standings(performance);

        private List<string> Standings(double performance)
        {
            var written = Scores(origin, commandId, performance);
            return written?.OrderByDescending(entry => entry.score).Select(entry => entry.contestantId).ToList();
        }

        /// <summary>The scores the Compete command writes, in the order it writes them; null when the engine refuses it.</summary>
        private static List<CompetitionScore> Scores(EpisodeState state, string commandId, double performance)
        {
            if (double.IsNaN(performance)) return null;
            performance = Math.Max(0, Math.Min(1, performance));
            try
            {
                var result = new EpisodeEngine(state).Apply(new EpisodeCommand
                {
                    id = string.IsNullOrEmpty(commandId) ? "competition-hold-preview" : commandId, actorId = state.playerId,
                    expectedPhase = state.phase, expectedRevision = state.revision,
                    kind = EpisodeCommandKind.Compete, performance = performance,
                });
                if (!result.accepted || result.state?.competitionScores == null) return null;
                return result.state.competitionScores.Select(entry => new CompetitionScore { contestantId = entry.contestantId, score = entry.score }).ToList();
            }
            catch (Exception) { return null; }
        }

        private static double Score(List<CompetitionScore> scores, string id)
        {
            var entry = scores.FirstOrDefault(score => score.contestantId == id);
            return entry == null ? double.NaN : entry.score;
        }

        private static bool SameHousemates(List<CompetitionScore> a, List<CompetitionScore> b, string playerId)
        {
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
            {
                if (a[i].contestantId != b[i].contestantId) return false;
                if (a[i].contestantId != playerId && !a[i].score.Equals(b[i].score)) return false;
            }
            return true;
        }
    }
}
