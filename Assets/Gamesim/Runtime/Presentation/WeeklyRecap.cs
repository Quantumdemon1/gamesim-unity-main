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
    public static partial class WeeklyRecap
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
            // The final eviction's line opens with the Head of Household who chose ("Maya takes Casey
            // to the final two. Riley joins the jury."), so its evictee is the second sentence's.
            recap.evicted = gone == null ? null
                : gone.kind == "final-eviction" ? VoteRecords.FinalEvictee(state, gone.text) ?? Subject(state, gone.text)
                : Subject(state, gone.text);
            recap.evictionLine = gone?.text;

            // Only what the player saw: an alliance or a deal between two other houseguests is
            // theirs, logged to them alone, and never the player's news.
            bool Seen(EpisodeEvent e) => e.audienceIds == null || e.audienceIds.Count == 0 || e.audienceIds.Contains(state.playerId);
            // The reveal's lines as the player heard them: the eviction line, which carries the
            // count read to the house, and the player's own ballot, logged to them alone. Every
            // other ballot's line is its voter's.
            recap.ballots = events
                .Where(e => (e.kind == "eviction" || e.kind == "vote-reveal") && Seen(e))
                .Select(e => e.text)
                .ToList();

            recap.relationships = Movements(state, week);
            recap.alliances = events.Where(e => e.kind == "alliance" && Seen(e)).Select(e => e.text).Take(LineLimit).ToList();
            recap.deals = events.Where(e => (e.kind == "deal" || e.kind == "deal-outcome") && Seen(e))
                .Select(e => e.text).Take(LineLimit).ToList();
            recap.moments = Moments(state, events);
            // What happened to the house that week, and what the player did about it. The recap is
            // the only place a resolved situation is readable again once its screen has gone. A
            // story's beats are the episode's acts below, so they are not listed twice.
            recap.happenings = state.houseEvents
                .Where(e => e.week == week && !e.IsStory)
                .Select(e => e.title + (e.resolved ? " — " + e.outcome : " — you let it pass"))
                .Take(LineLimit).ToList();
            recap.yourWeek = Narrative(state, week, events);
            Episode(state, week, events, recap);
            Ledger(state, week, events, recap, gone);
            return recap;
        }

        // ---------------------------------------------------------------- the episode (plan §5.4)

        /// <summary>The four acts a week of the show is cut into, by the phase a beat was answered in.</summary>
        public static readonly string[] ActTitles = { "Act I · Head of Household", "Act II · Nominations", "Act III · The Veto", "Act IV · Eviction Night" };

        private static int ActOf(EpisodePhase phase)
        {
            switch (phase)
            {
                case EpisodePhase.HoH: return 0;
                case EpisodePhase.Nomination: return 1;
                case EpisodePhase.Veto:
                case EpisodePhase.VetoMeeting: return 2;
                default: return 3;
            }
        }

        /// <summary>
        /// The week as an episode of the show: previously on - where each story the player was in
        /// stood coming into the week; the week's story beats in its four acts; and next time - the
        /// stories that are not over. Read from the stories' own records and the log, player-facing
        /// lines only, rendered with names from the stories that wrote them.
        /// </summary>
        private static void Episode(EpisodeState state, int week, List<EpisodeEvent> events, Week recap)
        {
            if (state?.storylines == null) return;
            bool Mine(StorylineState cycle) => cycle.path != null && cycle.path.Any(step => step.result != StoryResults.Npc);
            EpisodeEvent Line(int sequence) => sequence <= 0 ? null : state.events.FirstOrDefault(e => e.sequence == sequence
                && (e.audienceIds.Count == 0 || e.audienceIds.Contains(state.playerId)));

            // Previously on: the last step before this week of each of the player's stories still
            // going when the week began - the season's threads first (plan 31).
            foreach (var cycle in state.storylines.Where(Mine).OrderByDescending(x => x.lane == StoryLanes.Thread)
                         .ThenBy(x => x.week).ThenBy(x => x.id, StringComparer.Ordinal))
            {
                if (!StorylineStatus.Running(cycle.status) && cycle.endedWeek < week) continue;
                var before = cycle.path.LastOrDefault(step => step.week < week && step.result != StoryResults.Npc);
                var line = before == null ? null : Line(before.logSequence);
                if (line != null) recap.previously.Add(StoryText.Log(state, line));
            }
            recap.previously = recap.previously.Distinct(StringComparer.Ordinal).Take(LineLimit).ToList();

            // The acts: the week's answered and lapsed beats, where the week put them.
            foreach (var entry in events.Where(e => e.kind == StoryLog.Outcome
                         && (e.audienceIds.Count == 0 || e.audienceIds.Contains(state.playerId))))
            {
                var act = recap.acts[ActOf(entry.phase)];
                if (act.Count < LineLimit) act.Add(StoryText.Log(state, entry));
            }

            // Next time: the player's stories still running once the week is done. The title only -
            // a teaser, not a spoiler.
            recap.nextTime = state.storylines
                .Where(cycle => StorylineStatus.Running(cycle.status) && Mine(cycle) && cycle.week <= week)
                .OrderByDescending(cycle => cycle.lane == StoryLanes.Thread)
                .Select(cycle => NextTimeTitle(state, cycle) + ": to be continued.")
                .Distinct(StringComparer.Ordinal).Take(LineLimit).ToList();
        }

        /// <summary>A story's name for "next time": a thread's names its people ("Your bond with Alex").</summary>
        private static string NextTimeTitle(EpisodeState state, StorylineState cycle)
        {
            var template = StoryCatalog.Find(cycle.templateId);
            if (template?.thread?.label != null) return StoryText.Fill(state, template.thread.label, cycle.cast);
            return template?.title ?? cycle.title;
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
        /// which is the same line the notebook and the diary room draw. And an entry whose own words
        /// tell a ballot the player does not know - how a vote deal or a vote promise with that
        /// houseguest ended - is left out of the sum, as the web's history leaves it out of its list
        /// (<see cref="KnownBallots.TellsAnUnknownBallot"/>).</para>
        /// </summary>
        private static List<Movement> Movements(EpisodeState state, int week)
        {
            var moves = new List<Movement>();
            foreach (var edge in state.relationships)
            {
                if (edge.fromId != state.playerId && edge.toId != state.playerId) continue;
                if (edge.fromId == state.playerId && edge.toId == state.playerId) continue;
                string other = edge.fromId == state.playerId ? edge.toId : edge.fromId;
                double total = edge.events.Where(e => e.week == week && !KnownBallots.TellsAnUnknownBallot(state, other, e.description, e.week))
                    .Sum(e => e.impactScore);
                if (Math.Abs(total) < NotableMove) continue;
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
            // A story's outcome and production's word are turning points too: the lines the plan's
            // recap is built from ("The Backdoor: you told Casey she was a pawn."), rendered with
            // names from the story that wrote them.
            var kinds = new[] { "promise-outcome", "deal-outcome", "loyalty-oath", "backdoor", "jury-tie",
                StoryLog.Outcome, StoryLog.Production, StoryLog.Penalty, StoryLog.Expulsion };
            return events
                .Where(e => kinds.Contains(e.kind))
                .Where(e => e.audienceIds.Count == 0 || e.audienceIds.Contains(state.playerId))
                .Select(e => StoryText.Log(state, e))
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
            // The week's own record, not the house as it stands now: a week looked back on has its
            // power row, and the current nominees are another week's.
            var power = state.ledger?.power?.FirstOrDefault(p => p.week == week);
            bool saved = onTheBlock && (power != null ? power.savedId == you.id
                : week == state.week && !state.nominees.Contains(you.id) && state.vetoResolved);

            var said = new List<string>();
            if (ranTheWeek) said.Add("You ran the week as Head of Household.");
            if (onTheBlock)
                said.Add(saved
                    ? "You went up, and the veto took you back down."
                    : "You spent the week on the block.");
            if (heldTheVeto) said.Add("You won the veto.");

            // The player's own feelings only: how the house took to them is the house's, and the
            // recap is shown during play.
            double warmer = 0, cooler = 0;
            foreach (var move in Movements(state, week).Where(m => !m.aboutYou))
            {
                if (move.delta > 0) warmer += move.delta; else cooler -= move.delta;
            }
            if (warmer > cooler && warmer > 0) said.Add("You warmed to the house this week.");
            else if (cooler > warmer && cooler > 0) said.Add("You soured on the house this week.");
            else if (warmer > 0) said.Add("You warmed to some and cooled on others.");

            // Only in the week it happened: a look back at week one, from the jury, is not the end.
            bool leftThisWeek = power != null ? power.evicteeId == you.id : week == state.week;
            var removal = state.story?.removals?.FirstOrDefault(r => r.contestantId == you.id);
            if ((you.status == ContestantStatus.Jury || you.status == ContestantStatus.Evicted) && leftThisWeek)
                said.Add("Your season ended here.");
            else if (you.status == ContestantStatus.Expelled && (removal == null ? week == state.week : removal.week == week))
                said.Add("Production removed you from the house.");

            return said.Count == 0 ? "A quiet week for you." : string.Join(" ", said);
        }

        // ---------------------------------------------------------------- what a recap is

        /// <summary>One week's recap. Plain data, so a test can read it without a screen.</summary>
        public sealed partial class Week
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

            /// <summary>Things that happened to the house, and what was done about them.</summary>
            public List<string> happenings = new List<string>();

            /// <summary>Previously on: where each of the player's stories stood as the week began.</summary>
            public List<string> previously = new List<string>();

            /// <summary>The week's story beats in its four acts (<see cref="ActTitles"/>).</summary>
            public List<string>[] acts = { new List<string>(), new List<string>(), new List<string>(), new List<string>() };

            /// <summary>Next time: the player's stories that are not over.</summary>
            public List<string> nextTime = new List<string>();

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
