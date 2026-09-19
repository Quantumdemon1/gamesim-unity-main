using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;

namespace Gamesim.Presentation
{
    /// <summary>
    /// What happened in one week of the house.
    ///
    /// <para>This port closes a season with <see cref="SeasonReport"/> and closes a <i>week</i> with
    /// nothing at all. A week here runs competitions, a nomination ceremony, a veto meeting, a
    /// campaign, private conversations and a vote — and then simply begins the next one, with the
    /// only record a scrolling HUD log that has already moved on. The reference build ends every
    /// week with a recap, which is how a player finds out what the vote actually was.</para>
    ///
    /// <para>Ported from <c>src/utils/recap/weekly-recap-builder.ts</c> and the parts of
    /// <c>event-formatter.ts</c> it leans on. <b>Nothing here changes the simulation.</b> Every line
    /// is read back out of <see cref="EpisodeState"/> and the <see cref="EpisodeEvent"/> log, which
    /// already record the week, the phase, the kind and the audience of everything that happened —
    /// so a recap cannot disagree with the save, and adding one cannot regress a season.</para>
    ///
    /// <para>It is a plain static class rather than a component so the whole of it can be tested
    /// without a scene, the same division the reference makes between its builders and its screen.
    /// <see cref="WeeklyRecapScreen"/> draws what this returns.</para>
    /// </summary>
    public static class WeeklyRecap
    {
        /// <summary>How far a relationship has to move in a week before the recap mentions it.</summary>
        public const double NotableMove = 20;

        /// <summary>How many of each kind of line the recap will print before it stops.</summary>
        public const int LineLimit = 6;

        /// <summary>
        /// One week, in the order the week happened.
        ///
        /// <para>The headline facts come from <see cref="ContestantState"/> rather than from parsing
        /// the log, wherever the state holds them: <c>nominationWeeks</c> says who was nominated in
        /// week four far more reliably than a sentence about it does. The log is read for the things
        /// only it knows — who won what, what the veto holder decided, how the vote fell.</para>
        /// </summary>
        public static Week Build(EpisodeState state, int week)
        {
            var recap = new Week { week = week };
            if (state == null || week < 1) return recap;

            var events = state.events.Where(e => e.week == week).OrderBy(e => e.sequence).ToList();

            recap.headOfHousehold = CompetitionWinner(state, events, EpisodePhase.HoH);
            recap.vetoHolder = CompetitionWinner(state, events, EpisodePhase.Veto);
            recap.nominees = state.contestants
                .Where(c => c.nominationWeeks != null && c.nominationWeeks.Contains(week))
                .Select(c => c.name).ToList();

            var veto = events.FirstOrDefault(e => e.kind == "veto");
            if (veto != null)
            {
                // "declines to use" and "decline to use" both appear, because the log addresses the
                // player in the second person. Matching on "declin" covers both without a parser.
                recap.vetoUsed = veto.text.IndexOf("declin", StringComparison.OrdinalIgnoreCase) < 0;
                recap.vetoLine = veto.text;
            }

            var gone = events.FirstOrDefault(e => e.kind == "eviction" || e.kind == "final-eviction");
            recap.evicted = gone == null ? null : Subject(state, gone.text);
            recap.evictionLine = gone?.text;

            recap.ballots = events
                .Where(e => e.kind == "vote-reveal")
                .Select(e => e.text)
                .ToList();

            recap.relationships = Movements(state, week);
            recap.alliances = events.Where(e => e.kind == "alliance").Select(e => e.text).Take(LineLimit).ToList();
            recap.deals = events.Where(e => e.kind == "deal" || e.kind == "deal-outcome")
                .Select(e => e.text).Take(LineLimit).ToList();
            recap.moments = Moments(state, events);
            recap.yourWeek = Narrative(state, week, events);
            return recap;
        }

        /// <summary>Every week the season has played, oldest first.</summary>
        public static List<Week> Season(EpisodeState state)
        {
            var weeks = new List<Week>();
            if (state == null) return weeks;
            for (int week = 1; week <= state.week; week++)
            {
                var built = Build(state, week);
                if (built.Empty) continue;
                weeks.Add(built);
            }
            return weeks;
        }

        /// <summary>Whether a week has closed far enough to be worth recapping.</summary>
        public static bool Ready(EpisodeState state) =>
            state != null && state.evictionResolved
            && state.events.Any(e => e.week == state.week && e.kind == "eviction");

        // ---------------------------------------------------------------- the parts

        private static string CompetitionWinner(EpisodeState state, List<EpisodeEvent> events, EpisodePhase phase)
        {
            var won = events.FirstOrDefault(e => e.kind == "competition" && e.phase == phase);
            return won == null ? null : WinnerName(won.text);
        }

        /// <summary>
        /// The name out of "Competition winner: Maya Chen · Mental."
        ///
        /// <para>A competition line is the one the engine does not open with a name, so it is the one
        /// that needs reading rather than matching. Shared with <see cref="SeasonReport"/>, which had
        /// its own private copy of exactly this until the recap needed the same answer — two parsers
        /// for one sentence is one parser too many, and the second one is always the one that drifts.
        /// </para>
        /// </summary>
        public static string WinnerName(string line)
        {
            if (string.IsNullOrEmpty(line)) return null;
            int start = line.IndexOf(':');
            if (start < 0) return null;
            int end = line.IndexOf('·', start);
            string slice = end > start
                ? line.Substring(start + 1, end - start - 1)
                : line.Substring(start + 1);
            slice = slice.Trim().TrimEnd('.');
            return slice.Length == 0 ? null : slice;
        }

        /// <summary>
        /// Whose name a log line opens with.
        ///
        /// <para>Every line the engine writes <i>about</i> somebody opens with their name — "Taylor
        /// Kim is evicted…", "Maya Chen nominates…" — so matching the cast against the start of the
        /// sentence finds the subject without a grammar. Matching anywhere in the line would pick
        /// the wrong person out of "Maya nominates Taylor", which is exactly the sentence this has
        /// to get right. The longest match wins, so "Jamie Roberts" is not read as "Jamie".</para>
        /// </summary>
        public static string Subject(EpisodeState state, string line)
        {
            if (state == null || string.IsNullOrEmpty(line)) return null;
            return state.contestants
                .Where(c => !string.IsNullOrEmpty(c.name) && line.StartsWith(c.name, StringComparison.Ordinal))
                .OrderByDescending(c => c.name.Length)
                .FirstOrDefault()?.name;
        }

        /// <summary>
        /// Relationships that moved far enough this week to be worth saying out loud.
        ///
        /// <para>Summed from the ledger's own entries rather than from a before-and-after snapshot,
        /// because a snapshot is not kept per week and reconstructing one would mean replaying the
        /// season. The reference's threshold is twenty points in a week; below that the house is
        /// just talking.</para>
        ///
        /// <para>Bounded by what the player's character knows: an edge is only reported when the
        /// player is one end of it. The rest of the house's private feelings are not theirs to read,
        /// which is the same line the notebook and the diary room draw.</para>
        /// </summary>
        private static List<Movement> Movements(EpisodeState state, int week)
        {
            var moves = new List<Movement>();
            foreach (var edge in state.relationships)
            {
                if (edge.fromId != state.playerId && edge.toId != state.playerId) continue;
                if (edge.fromId == state.playerId && edge.toId == state.playerId) continue;
                double total = edge.events.Where(e => e.week == week).Sum(e => e.impactScore);
                if (Math.Abs(total) < NotableMove) continue;
                string other = edge.fromId == state.playerId ? edge.toId : edge.fromId;
                moves.Add(new Movement
                {
                    otherId = other,
                    otherName = state.Find(other)?.name ?? other,
                    // "How they read you" and "how you read them" are different facts, and a recap
                    // that conflated them would tell the player their own feelings had changed.
                    aboutYou = edge.toId == state.playerId,
                    delta = total,
                });
            }
            return moves
                .OrderByDescending(m => Math.Abs(m.delta))
                .ThenBy(m => m.otherName, StringComparer.Ordinal)
                .Take(LineLimit).ToList();
        }

        /// <summary>
        /// The week's turning points.
        ///
        /// <para>The reference looks for a <c>significance: major</c> flag on its events. Nothing
        /// here carries one, so the kinds that only ever fire at a turning point stand in for it —
        /// a broken promise or deal, a betrayal, a loyalty oath. Guessing at a flag that does not
        /// exist would have been the alternative, and it would have printed nothing.</para>
        /// </summary>
        private static List<string> Moments(EpisodeState state, List<EpisodeEvent> events)
        {
            var kinds = new[] { "promise-outcome", "deal-outcome", "loyalty-oath", "backdoor", "jury-tie" };
            return events
                .Where(e => kinds.Contains(e.kind))
                .Where(e => e.audienceIds.Count == 0 || e.audienceIds.Contains(state.playerId))
                .Select(e => e.text)
                .Distinct(StringComparer.Ordinal)
                .Take(LineLimit).ToList();
        }

        /// <summary>
        /// The player's own week, in a sentence or two.
        ///
        /// <para>The reference's <c>generateWeekNarrative</c>, with its clauses in its order: the
        /// power first, then the block, then the veto, then how the social week went. Where it reads
        /// its own event types, this reads the state that records the same facts.</para>
        /// </summary>
        private static string Narrative(EpisodeState state, int week, List<EpisodeEvent> events)
        {
            var you = state.Find(state.playerId);
            if (you == null) return null;

            bool ranTheWeek = CompetitionWinner(state, events, EpisodePhase.HoH) == you.name;
            bool heldTheVeto = CompetitionWinner(state, events, EpisodePhase.Veto) == you.name;
            bool onTheBlock = you.nominationWeeks != null && you.nominationWeeks.Contains(week);
            bool saved = onTheBlock && !state.nominees.Contains(you.id) && state.vetoResolved;

            var said = new List<string>();
            if (ranTheWeek) said.Add("You ran the week as Head of Household.");
            if (onTheBlock)
                said.Add(saved
                    ? "You went up, and the veto took you back down."
                    : "You spent the week on the block.");
            if (heldTheVeto) said.Add("You won the veto.");

            double warmer = 0, cooler = 0;
            foreach (var move in Movements(state, week))
            {
                if (move.delta > 0) warmer += move.delta; else cooler -= move.delta;
            }
            if (warmer > cooler && warmer > 0) said.Add("You left the week better liked than you started it.");
            else if (cooler > warmer && cooler > 0) said.Add("You made enemies this week.");
            else if (warmer > 0) said.Add("You gained as much ground as you lost.");

            if (you.status == ContestantStatus.Jury || you.status == ContestantStatus.Evicted)
                said.Add("Your season ended here.");

            return said.Count == 0 ? "A quiet week for you." : string.Join(" ", said);
        }

        // ---------------------------------------------------------------- what a recap is

        /// <summary>One week's recap. Plain data, so a test can read it without a screen.</summary>
        public sealed class Week
        {
            public int week;
            public string headOfHousehold, vetoHolder, evicted;
            public List<string> nominees = new List<string>();

            /// <summary>Null where no veto meeting happened, which is not the same as "not used".</summary>
            public bool? vetoUsed;

            public string vetoLine, evictionLine, yourWeek;
            public List<string> ballots = new List<string>();
            public List<Movement> relationships = new List<Movement>();
            public List<string> alliances = new List<string>();
            public List<string> deals = new List<string>();
            public List<string> moments = new List<string>();

            /// <summary>A week nothing is known about — a season that stopped before its first vote.</summary>
            public bool Empty => headOfHousehold == null && evicted == null && nominees.Count == 0;

            /// <summary>The headline, the way the reference writes it.</summary>
            public string Headline =>
                evicted != null ? evicted + " was evicted in week " + week + "."
                : headOfHousehold != null ? headOfHousehold + " ran week " + week + "."
                : "Week " + week + ".";
        }

        /// <summary>A relationship that moved, and which direction it was read in.</summary>
        public sealed class Movement
        {
            public string otherId, otherName;

            /// <summary>True when this is how they read the player; false when it is the reverse.</summary>
            public bool aboutYou;

            public double delta;

            public string Line => aboutYou
                ? otherName + (delta > 0 ? " warmed to you" : " cooled on you")
                : (delta > 0 ? "You warmed to " : "You cooled on ") + otherName;
        }
    }
}
