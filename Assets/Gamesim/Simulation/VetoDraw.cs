using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// The veto's draw as the house sees it (PACK8-PASS-PLAN B2, the owner's mockup 76): who plays
    /// by right, who is in the bag, how many chips come out, and - once the draw is made - who they
    /// were, in the order a card turns them over.
    ///
    /// <para>A reader, not a rule. The draw itself is <c>EpisodeEngine.DrawVetoPlayers</c>, run by
    /// the Advance that leaves the selection, and this mirrors its shape exactly: at six houseguests
    /// or fewer everyone plays and nothing is drawn, so the screen must not show chips for a draw
    /// the engine never makes. The selection screen said "and 3 drawn from the house" over three
    /// chips in a house of six, where the engine seated everyone without touching the generator.</para>
    ///
    /// <para>Nothing here draws from the season's generator or writes to the state. Everything it
    /// reads is public: the lineup rule, the Head of Household and the block, and - after the
    /// commit - the committed lineup, which the engine holds in house order, not in the order it
    /// drew. The order a card reveals the drawn in is therefore a presentation order, seeded so a
    /// reload shows the same draw, and never the engine's.</para>
    /// </summary>
    public static class VetoDraw
    {
        /// <summary>The selection screen's board, read from the state before the draw.</summary>
        public sealed class Board
        {
            /// <summary>How many play for the veto: six, or everyone still in the house.</summary>
            public int Seats;
            /// <summary>
            /// Who plays without being drawn, the Head of Household first and then the block. When
            /// everyone plays, the whole house, the rest after them in house order.
            /// </summary>
            public List<string> ByRight = new List<string>();
            /// <summary>Who is in the bag, in house order. Empty when everyone plays.</summary>
            public List<string> Eligible = new List<string>();
            /// <summary>How many chips come out of the bag. Zero when everyone plays.</summary>
            public int ToDraw;
            /// <summary>Whether everyone still in the house plays, so nobody is drawn.</summary>
            public bool EveryonePlays;
        }

        /// <summary>The line the screen says when nobody is drawn.</summary>
        public const string EveryonePlaysLine = "Everyone still in the house plays for the veto.";

        /// <summary>The eligible pool's heading, as the mockup writes it.</summary>
        public const string EligibleHeading = "ELIGIBLE FOR DRAW";

        /// <summary>
        /// The board before the draw, from the house as it stands. The same seats and the same pool
        /// the engine's draw uses, so the chips on the screen are the chips that come out.
        /// </summary>
        public static Board Before(EpisodeState s)
        {
            var board = new Board();
            if (s == null) return board;
            var active = s.Active.Select(c => c.id).ToList();
            board.Seats = EpisodeEngine.VetoPlayerCount(active.Count);
            if (active.Contains(s.hohId)) board.ByRight.Add(s.hohId);
            foreach (var id in s.nominees ?? new List<string>())
                if (active.Contains(id) && !board.ByRight.Contains(id)) board.ByRight.Add(id);
            var pool = active.Where(id => !board.ByRight.Contains(id)).ToList();
            // The engine's own test: when every seat is filled by the house, it returns the house
            // and never draws.
            board.EveryonePlays = board.Seats >= active.Count;
            if (board.EveryonePlays)
            {
                board.ByRight.AddRange(pool);
                return board;
            }
            board.Eligible = pool;
            board.ToDraw = Math.Max(0, Math.Min(pool.Count, board.Seats - board.ByRight.Count));
            return board;
        }

        /// <summary>
        /// Who the draw added, once it is made: the committed lineup less the Head of Household and
        /// the block, in the order a card reveals them (<see cref="RevealOrder"/>). Empty when
        /// everyone played, or before the draw.
        /// </summary>
        public static List<string> Drawn(EpisodeState s)
        {
            var players = s?.vetoPlayers;
            if (players == null || players.Count == 0 || players.Count >= s.Active.Count()) return new List<string>();
            var drawn = players.Where(id => id != s.hohId && (s.nominees == null || !s.nominees.Contains(id)));
            return RevealOrder(drawn, s.seed, s.week);
        }

        /// <summary>
        /// The order the drawn are revealed in: shuffled, the same way every time for the same week of
        /// the same season, so a reload shows the same draw. Presentation only - it reads the seed and
        /// never draws from the season's generator. House order would put whoever is first in the cast
        /// first every week they are drawn, and the player is first in the cast.
        /// </summary>
        public static List<string> RevealOrder(IEnumerable<string> ids, uint seed, int week) =>
            (ids ?? Enumerable.Empty<string>())
                .Select((id, index) => (id, index, rank: Rank(seed, week, id)))
                .OrderBy(entry => entry.rank).ThenBy(entry => entry.index)
                .Select(entry => entry.id).ToList();

        private static uint Rank(uint seed, int week, string id)
        {
            unchecked
            {
                uint hash = 2166136261u;
                void Mix(uint value)
                {
                    for (int shift = 0; shift < 32; shift += 8) { hash ^= (value >> shift) & 0xFF; hash *= 16777619u; }
                }
                // Salted apart from the key ceremony's order, so the two ceremonies of one week do
                // not share a shuffle.
                Mix(seed); Mix((uint)week); Mix(0x64726177u); // "draw"
                foreach (char c in id ?? string.Empty) Mix(c);
                return hash;
            }
        }

        // ---------------------------------------------------------------- the screen's words

        /// <summary>The one line under the screen's title: who plays, and how the rest of the field is found.</summary>
        public static string Line(Board board) => board == null || board.EveryonePlays ? EveryonePlaysLine
            : board.Seats + " players compete: HoH, both nominees, and " + Players(board.ToDraw) + " drawn at random.";

        /// <summary>The heading over the faces that play without a draw.</summary>
        public static string PlayingHeading(Board board) =>
            board == null || board.EveryonePlays ? "PLAYING FOR THE VETO" : "AUTOMATICALLY PLAYING";

        /// <summary>The heading over the bag.</summary>
        public static string DrawHeading(Board board) => board == null || board.EveryonePlays ? "NO DRAW"
            : "DRAW " + board.ToDraw + (board.ToDraw == 1 ? " PLAYER" : " PLAYERS");

        /// <summary>The words under the bag.</summary>
        public static string ChipsLine(Board board) => board == null || board.EveryonePlays ? "Everyone plays"
            : board.ToDraw == 1 ? "1 chip to draw" : board.ToDraw + " chips to draw";

        /// <summary>
        /// The headline the way on wears (PACK8-PASS-PLAN decision 4): a label beside its caption,
        /// never the caption, which stays "Continue episode".
        /// </summary>
        public static string Headline(Board board) => board == null || board.EveryonePlays ? "EVERYONE PLAYS" : "REVEAL THE DRAW";

        /// <summary>The footer's line beside the way on: what pressing it does, or what comes after it.</summary>
        public static string Footnote(Board board) => board == null || board.EveryonePlays
            ? "Up next: the Power of Veto competition."
            : "Draw " + Players(board.ToDraw) + " from the eligible pool to complete the Veto competition.";

        private static string Players(int count) => count == 1 ? "1 player" : count + " players";
    }
}
