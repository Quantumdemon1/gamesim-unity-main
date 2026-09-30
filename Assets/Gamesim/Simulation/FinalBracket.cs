using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// The final Head of Household's three parts as its screens read them (MOCKUP-PASS-PLAN M13,
    /// mockup 58): a part already played with its winner, the part being played with who plays it,
    /// and the parts ahead, locked or with the seat already won.
    ///
    /// <para>The public record only. Who plays a part and who won one are announced to the whole
    /// house as they happen. No score is read, so nothing here says how a part is going before its
    /// result commits (decision 53).</para>
    ///
    /// <para>Every line speaks to the player, as the bracket cards do: the player is "You" wherever
    /// they stand in it, and everybody else goes by their first name. Pure and read-only: it neither
    /// changes the state nor draws from its generator, and it lives in the simulation so the
    /// Unity-free subset can test it.</para>
    /// </summary>
    public sealed class FinalBracket
    {
        /// <summary>Where a part stands: played, being played, or still ahead.</summary>
        public enum Standing { Played, Playing, Ahead }

        /// <summary>One of the three parts.</summary>
        public sealed class Part
        {
            public int number;
            public Standing standing;
            /// <summary>
            /// The faces the part shows: its winner once it is played, its players while it is
            /// played, and the seats already won in a part ahead.
            /// </summary>
            public List<ContestantState> faces = new List<ContestantState>();
            /// <summary>A seat in a part ahead that nobody has won yet: "Jordan vs ?".</summary>
            public bool openSeat;
            /// <summary>What the part says under its number: "Winner: Jordan", "Jordan vs Alex", "Final round · Locked".</summary>
            public string line;
        }

        /// <summary>The part being played: 1, 2 or 3.</summary>
        public int current;

        /// <summary>The three parts, first to last.</summary>
        public List<Part> parts = new List<Part>();

        /// <summary>What the part being played sends its winner on to, for the band at the foot of its screens.</summary>
        public string advance;

        /// <summary>
        /// How every final part is scored, said on the game screen's challenge card beside the game's
        /// own rules. True of all three: the committed scores are statistics, earned bonuses,
        /// performance and seeded rolls, and the highest wins.
        /// </summary>
        public const string ScoringLine = "Highest score wins · statistics and seeded rolls count";

        /// <summary>The part a phase plays, 1 to 3, or 0 outside the final Head of Household.</summary>
        public static int PartOf(EpisodePhase phase) =>
            phase == EpisodePhase.FinalHoHPart1 ? 1
            : phase == EpisodePhase.FinalHoHPart2 ? 2
            : phase == EpisodePhase.FinalHoHPart3 ? 3 : 0;

        /// <summary>
        /// What a part's winner goes on to, in a few words for the gold band: Part 1's winner waits in
        /// Part 3, Part 2's meets them there, and Part 3's is the final Head of Household. Null outside
        /// the final parts. The stakes line in the briefing's hero says the same at length.
        /// </summary>
        public static string AdvanceLine(EpisodePhase phase)
        {
            switch (phase)
            {
                case EpisodePhase.FinalHoHPart1: return "Winner goes straight to Part 3";
                case EpisodePhase.FinalHoHPart2: return "Winner advances to Part 3";
                case EpisodePhase.FinalHoHPart3: return "Winner becomes the final Head of Household";
                default: return null;
            }
        }

        /// <summary>How the bracket names a houseguest: "You" for the player, a first name for anybody else.</summary>
        public static string Name(EpisodeState s, ContestantState actor)
        {
            if (actor == null) return "?";
            return actor.id == s.playerId ? "You" : FinalistRead.FirstName(actor.name);
        }

        /// <summary>
        /// The bracket for the part the state is in, or null outside the final Head of Household's
        /// three parts.
        /// </summary>
        public static FinalBracket For(EpisodeState s)
        {
            int current = s == null ? 0 : PartOf(s.phase);
            if (current == 0) return null;
            var bracket = new FinalBracket { current = current, advance = AdvanceLine(s.phase) };
            var first = Known(s, s.finalPart1WinnerId);
            var second = Known(s, s.finalPart2WinnerId);
            // Part 3's winner is the final Head of Household, and only once Part 3 has resolved:
            // until then the house has no Head of Household at all.
            var third = current == 3 && s.competitionResolved ? Known(s, s.hohId) : null;
            var winners = new[] { first, second, third };
            for (int number = 1; number <= 3; number++)
            {
                var part = new Part { number = number };
                var winner = winners[number - 1];
                if (winner != null && number <= current)
                {
                    part.standing = Standing.Played;
                    part.faces.Add(winner);
                    part.line = "Winner: " + Name(s, winner);
                }
                else if (number == current)
                {
                    part.standing = Standing.Playing;
                    part.faces.AddRange(EpisodeEngine.CompetitionPlayers(s));
                    part.line = Versus(s, part.faces);
                }
                else
                {
                    part.standing = Standing.Ahead;
                    SeatsAhead(s, part, first, second);
                }
                bracket.parts.Add(part);
            }
            return bracket;
        }

        /// <summary>
        /// A part ahead, as far as the record fills it. Part 2 is the two Part 1 did not send
        /// through, known once Part 1 has a winner. Part 3 is the two part winners: both, one and an
        /// open seat, or nobody yet, when it is simply the final round and locked.
        /// </summary>
        private static void SeatsAhead(EpisodeState s, Part part, ContestantState first, ContestantState second)
        {
            if (part.number == 2)
            {
                if (first == null) { part.line = "Locked"; return; }
                part.faces.AddRange(s.Active.Where(actor => actor.id != first.id));
                part.line = part.faces.Count > 0 ? Versus(s, part.faces) : "Locked";
                return;
            }
            if (first != null) part.faces.Add(first);
            if (second != null) part.faces.Add(second);
            if (part.faces.Count == 0) { part.line = "Final round · Locked"; return; }
            part.openSeat = part.faces.Count == 1;
            part.line = Versus(s, part.faces) + (part.openSeat ? " vs ?" : "");
        }

        private static string Versus(EpisodeState s, IEnumerable<ContestantState> players) =>
            string.Join(" vs ", players.Select(actor => Name(s, actor)));

        private static ContestantState Known(EpisodeState s, string id) => string.IsNullOrEmpty(id) ? null : s.Find(id);
    }
}
