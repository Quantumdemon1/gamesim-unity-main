using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Gamesim.Persistence;
using Gamesim.Simulation;

namespace Gamesim.Presentation
{
    /// <summary>
    /// The week as the redesigned recap draws it (the owner's Week Recap mockup): who left and in
    /// what place, the week's five headline facts, the vote split into the evictee's votes and the
    /// other nominee's, the week's key moments, a record of the evictee's season, what the house was
    /// like in facts the player saw, and what comes next.
    ///
    /// <para><b>Evidence only, as mid-season must be.</b> The ids, the block, the veto, the evictee
    /// and the count come from the ledger's power row for the week, which survives the event log's
    /// cap. The ballots are the ones the player knows (<see cref="KnownBallots"/>), each with its
    /// basis, and a count of the ones they do not. The quote is the evictee's own speech from the
    /// block, said before the vote, and nothing else: no line is written in anybody's voice. The
    /// house's temperature is named from what the player saw - the count, who held the power, the
    /// broken word that reached them, the alliance moves they were told - with the evidence under
    /// each name, never a relationship score, a grudge or a juror's standing.</para>
    /// </summary>
    public static partial class WeeklyRecap
    {
        /// <summary>One ballot the player knows, by id: who voted to evict whom, how the player knows it, and the reason where a line read in the open gave one.</summary>
        public sealed class Ballot
        {
            public string voterId, targetId, reason;
            public bool tieBreak;
            /// <summary>One of <see cref="KnownBallots.Basis"/>.</summary>
            public string basis;
            /// <summary>What the voter said, where the ballot is known by a claim; null otherwise.</summary>
            public string saidId;
            /// <summary>A claim the reveal caught out. Worded by <see cref="KnownBallots.SaidWords"/>: of an ally's account, a vote that changed, never a lie.</summary>
            public bool lied;
        }

        /// <summary>One of the week's key moments: its kind, whose face it carries, its title and its line.</summary>
        public sealed class Moment
        {
            /// <summary>"hoh", "nominations", "veto" or "eviction": what the screen heads it with.</summary>
            public string kind, subjectId, title, line;
        }

        /// <summary>What the house was like, named, with the fact the name rests on.</summary>
        public sealed class Reading
        {
            public string label, evidence;
        }

        public sealed partial class Week
        {
            public string hohId, vetoHolderId, evictedId, savedId, replacementId;
            /// <summary>The nominations as the Head of Household made them, before any veto, by id.</summary>
            public List<string> nominated = new List<string>();
            /// <summary>The block the house voted on, by id.</summary>
            public List<string> block = new List<string>();
            /// <summary>Where the evictee finished, as the career counts it; 0 where nobody left.</summary>
            public int placement;
            /// <summary>Houseguests still in the house once the week's vote was in.</summary>
            public int remaining;
            /// <summary>The evictee's own words from the block, said before the vote; null where none were kept.</summary>
            public string evicteeQuote;
            /// <summary>The ballots the player knows: their own first, then the tie-break, then the cast's order.</summary>
            public List<Ballot> votes = new List<Ballot>();
            /// <summary>How many of the week's ballots the player cannot place.</summary>
            public int unknownBallots;
            public List<Moment> keyMoments = new List<Moment>();
            /// <summary>The evictee's season, in public facts.</summary>
            public List<string> exitRecord = new List<string>();
            public List<Reading> temperature = new List<Reading>();
            /// <summary>What comes after the week: for the week just closed, what the next one holds; for an earlier week, what it held.</summary>
            public List<string> whatsNext = new List<string>();
            /// <summary>Whether the week closed with the final Head of Household's choice rather than a vote.</summary>
            public bool finalDecision;
            /// <summary>Whether the week's vote is in: nothing after it is claimed of a week still being played.</summary>
            public bool closed;
            /// <summary>The count: votes to evict the evictee, and votes against the other nominee. The tie-break is not a vote the house cast.</summary>
            public int against, others;
        }

        /// <summary>Where the recap draws its words from: the ledger's row for the week, the reveal's ballots, the log.</summary>
        private static void Ledger(EpisodeState state, int week, List<EpisodeEvent> events, Week recap, EpisodeEvent gone)
        {
            string Id(string name) => name == null ? null : state.contestants.FirstOrDefault(c => c.name == name)?.id;
            var power = state.ledger?.power?.FirstOrDefault(p => p.week == week);
            recap.finalDecision = gone != null && gone.kind == "final-eviction";
            recap.hohId = power?.hohId ?? Id(recap.headOfHousehold);
            recap.vetoHolderId = power?.vetoHolderId ?? Id(recap.vetoHolder);
            recap.evictedId = power?.evicteeId ?? Id(recap.evicted);
            recap.savedId = power?.savedId;
            recap.replacementId = power?.replacementId;
            if (power != null)
            {
                recap.block = power.nominees.ToList();
                // The final eviction's row lists the two the last Head of Household chose between;
                // that is no nomination.
                if (!recap.finalDecision)
                {
                    recap.nominated = power.nominees.Where(id => id != power.replacementId).ToList();
                    if (power.savedId != null && !recap.nominated.Contains(power.savedId)) recap.nominated.Insert(0, power.savedId);
                }
            }
            else recap.nominated = state.contestants.Where(c => c.nominationWeeks != null && c.nominationWeeks.Contains(week)).Select(c => c.id).ToList();

            recap.closed = gone != null || week < state.week;
            // Where the log has lost the week, the ledger still knows whether the veto was used.
            if (recap.vetoUsed == null && power != null && power.vetoHolderId != null && !recap.finalDecision) recap.vetoUsed = power.vetoUsed;
            var evictee = state.Find(recap.evictedId);
            if (evictee != null) recap.placement = CareerLedger.Placement(state, evictee);
            recap.remaining = Remaining(state, week);

            // The evictee's words: the speech from the block, while the week still holds it, or the
            // log's line of it, whose "Name: " opens it. Only an NPC's speech is logged whole.
            if (evictee != null)
            {
                var spoken = state.evictionSpeeches?.FirstOrDefault(x => x.speakerId == evictee.id && x.week == week);
                string words = spoken?.text;
                if (string.IsNullOrWhiteSpace(words))
                {
                    var line = events.FirstOrDefault(e => e.text != null
                        && ((e.kind == "eviction-speech" && e.text.StartsWith(evictee.name + ": ", StringComparison.Ordinal))
                            || (BlockSpeeches.IsReceiptKind(e.kind) && BlockSpeeches.EventApproach(e) != BlockSpeeches.Quiet
                                && e.audienceIds.Count > 0 && e.audienceIds[0] == evictee.id)));
                    words = line == null ? null : BlockSpeeches.IsReceiptKind(line.kind) ? line.text
                        : line.text.Substring(evictee.name.Length + 2);
                }
                recap.evicteeQuote = string.IsNullOrWhiteSpace(words) ? null : words.Trim();
            }

            // The ballots as the player knows them (KnownBallots), never the box or the house's lines;
            // the count from the ledger's tally, which pairs each nominee with their votes.
            var sheet = KnownBallots.Read(state, week);
            foreach (var known in sheet.ballots.Where(b => b.Known))
                recap.votes.Add(new Ballot
                {
                    voterId = known.voterId, targetId = known.targetId, reason = known.reason,
                    tieBreak = known.basis == KnownBallots.Basis.TieBreak, basis = known.basis, saidId = known.saidId, lied = known.Lied,
                });
            recap.unknownBallots = sheet.Unknown;
            if (sheet.Revealed && recap.evictedId != null)
            {
                recap.against = sheet.Against(recap.evictedId);
                recap.others = sheet.tally.Sum() - recap.against;
            }
            else if (power != null && power.tally.Count == power.nominees.Count && power.tally.Count > 0 && recap.evictedId != null)
            {
                int at = power.nominees.IndexOf(recap.evictedId);
                recap.against = at >= 0 ? power.tally[at] : 0;
                recap.others = power.tally.Sum() - recap.against;
            }

            recap.keyMoments = KeyMoments(state, recap, events, power);
            recap.exitRecord = ExitRecord(state, recap, evictee);
            recap.temperature = Temperature(state, recap, events);
            recap.whatsNext = WhatsNext(state, recap, week);
        }

        /// <summary>Houseguests left once the week's vote was in: the cast less everyone who had left by then.</summary>
        private static int Remaining(EpisodeState state, int week)
        {
            var power = state.ledger?.power ?? new List<PowerRow>();
            var left = new HashSet<string>(power.Where(p => p.week <= week && p.evicteeId != null).Select(p => p.evicteeId));
            foreach (var removal in state.story?.removals ?? new List<RemovalState>())
                if (removal.week <= week) left.Add(removal.contestantId);
            // The week under way, or the season's last: everyone still in the house, the two
            // finalists included once the season is over.
            if (week >= state.week) return state.contestants.Count(c => c.status == ContestantStatus.Active
                || c.status == ContestantStatus.Winner || c.status == ContestantStatus.RunnerUp);
            return Math.Max(0, state.contestants.Count - left.Count);
        }

        /// <summary>The count as the reveal read it: the evictee's votes first. The tie-break is not a vote the house cast.</summary>
        public static (int against, int others) Split(Week recap) => (recap.against, recap.others);

        /// <summary>A houseguest as the subject of a sentence: "You" for the player, their name otherwise.</summary>
        private static string Who(EpisodeState state, string id) => id == state.playerId ? "You" : state.Find(id)?.name ?? "Somebody";

        /// <summary>A houseguest as its object: "you" for the player.</summary>
        private static string Whom(EpisodeState state, string id) => id == state.playerId ? "you" : state.Find(id)?.name ?? "somebody";

        /// <summary>"was" or, for the player, "were".</summary>
        private static string Was(EpisodeState state, string id) => id == state.playerId ? "were" : "was";

        private static List<Moment> KeyMoments(EpisodeState state, Week recap, List<EpisodeEvent> events, PowerRow power)
        {
            var moments = new List<Moment>();
            string Names(IEnumerable<string> ids) => And(ids.Select(id => Whom(state, id)).ToList());
            if (recap.finalDecision)
            {
                if (state.finalPart1WinnerId != null)
                    moments.Add(new Moment { kind = "hoh", subjectId = state.finalPart1WinnerId, title = "Final HoH · Part 1", line = Who(state, state.finalPart1WinnerId) + " won Part 1." });
                if (state.finalPart2WinnerId != null)
                    moments.Add(new Moment { kind = "veto", subjectId = state.finalPart2WinnerId, title = "Final HoH · Part 2", line = Who(state, state.finalPart2WinnerId) + " won Part 2." });
                if (recap.hohId != null)
                    moments.Add(new Moment { kind = "hoh", subjectId = recap.hohId, title = "Final Head of Household", line = Who(state, recap.hohId) + " won the last Head of Household." });
                if (recap.evictedId != null)
                    moments.Add(new Moment { kind = "eviction", subjectId = recap.evictedId, title = "The final eviction",
                        line = Who(state, recap.hohId) + " chose, and " + Whom(state, recap.evictedId) + " went to the jury." });
                return moments;
            }
            var competition = events.FirstOrDefault(e => e.kind == "competition" && e.phase == EpisodePhase.HoH);
            if (recap.hohId != null)
                moments.Add(new Moment { kind = "hoh", subjectId = recap.hohId, title = "HoH competition",
                    line = Who(state, recap.hohId) + " won Head of Household" + Category(competition) + "." });
            if (recap.nominated.Count > 0)
                moments.Add(new Moment { kind = "nominations", subjectId = recap.hohId ?? recap.nominated[0], title = "Nominations",
                    line = (recap.hohId != null ? Who(state, recap.hohId) + " nominated " : "On the block: ") + Names(recap.nominated) + "." });
            if (recap.vetoHolderId != null)
            {
                var veto = events.FirstOrDefault(e => e.kind == "competition" && e.phase == EpisodePhase.Veto);
                string on = recap.savedId == recap.vetoHolderId ? (recap.vetoHolderId == state.playerId ? "yourself" : "themselves")
                    : recap.savedId != null ? Whom(state, recap.savedId) : null;
                string meeting = recap.vetoUsed == null ? ""
                    : recap.vetoUsed.Value
                        ? (on != null ? " and used it on " + on : " and used it")
                          + (recap.replacementId != null ? "; " + Whom(state, recap.replacementId) + " went up in their place" : "")
                        : " and kept the nominations the same";
                moments.Add(new Moment { kind = "veto", subjectId = recap.vetoHolderId, title = "Veto competition",
                    line = Who(state, recap.vetoHolderId) + " won the Power of Veto" + Category(veto) + meeting + "." });
            }
            if (recap.evictedId != null)
            {
                var (against, others) = Split(recap);
                bool tie = recap.votes.Any(v => v.tieBreak);
                string how = tie ? "the tie " + Whom(state, recap.hohId) + " broke"
                    : against + others > 0 ? "a vote of " + against + "–" + others : null;
                moments.Add(new Moment { kind = "eviction", subjectId = recap.evictedId, title = "Eviction night",
                    line = how != null ? "By " + how + ", " + Whom(state, recap.evictedId) + " " + Was(state, recap.evictedId) + " evicted."
                        : Who(state, recap.evictedId) + " " + Was(state, recap.evictedId) + " evicted." });
            }
            return moments;
        }

        /// <summary>The competition's kind out of "Competition winner: Maya Chen · Mental.": " (Mental)", or nothing.</summary>
        private static string Category(EpisodeEvent competition)
        {
            if (competition?.text == null) return "";
            int dot = competition.text.IndexOf('·');
            if (dot < 0) return "";
            string kind = competition.text.Substring(dot + 1).Trim().TrimEnd('.').Replace("(simulated)", "").Trim();
            return kind.Length == 0 ? "" : " (" + kind + ")";
        }

        /// <summary>The evictee's season, in facts the house saw: how they left, their wins, their nominations, their seat.</summary>
        private static List<string> ExitRecord(EpisodeState state, Week recap, ContestantState evictee)
        {
            var record = new List<string>();
            if (evictee == null) return record;
            bool you = evictee.isPlayer;
            var (against, others) = Split(recap);
            if (recap.finalDecision) record.Add("Sent to the jury by the final Head of Household's choice.");
            else if (recap.votes.Any(v => v.tieBreak)) record.Add("Went home when the Head of Household broke a tied vote.");
            else if (against + others > 0) record.Add("Evicted by a vote of " + against + " to " + others + ".");
            if (recap.replacementId == evictee.id) record.Add("Went up as the replacement after the veto.");
            int earlier = (evictee.nominationWeeks ?? new List<int>()).Count(w => w < recap.week);
            record.Add(earlier == 0 ? (you ? "Your first time on the block." : "Their first time on the block.")
                : "Survived " + earlier + (earlier == 1 ? " earlier nomination." : " earlier nominations."));
            int wins = evictee.hohWins + evictee.vetoWins;
            record.Add(wins == 0 ? "Won no competitions." : "Won " + evictee.hohWins + " HoH and " + evictee.vetoWins + (evictee.vetoWins == 1 ? " veto." : " vetoes."));
            if (evictee.status == ContestantStatus.Jury) record.Add("Takes a seat on the jury.");
            else if (evictee.status == ContestantStatus.Evicted) record.Add("Out before the jury.");
            return record;
        }

        /// <summary>
        /// The house, named from what the player saw - never from a score. A split vote or a tie is
        /// a divided house; every vote one way is one mind; the house and the veto in one pair of
        /// hands is power in one place; broken word that reached the player is trust tested; alliance
        /// moves they were told are alliances forming. Each with the fact it rests on.
        /// </summary>
        private static List<Reading> Temperature(EpisodeState state, Week recap, List<EpisodeEvent> events)
        {
            var readings = new List<Reading>();
            if (!recap.closed)
            {
                readings.Add(new Reading { label = "Still being played", evidence = "The week's vote is still to come." });
                return readings;
            }
            bool Seen(EpisodeEvent e) => e.audienceIds.Count == 0 || e.audienceIds.Contains(state.playerId);
            bool Says(EpisodeEvent e, params string[] words) => e.text != null && words.Any(w => e.text.IndexOf(w, StringComparison.OrdinalIgnoreCase) >= 0);
            var (against, others) = Split(recap);
            if (recap.votes.Any(v => v.tieBreak))
                readings.Add(new Reading { label = "House divided", evidence = "The vote tied, and " + Whom(state, recap.hohId) + " broke it." });
            else if (against + others >= 2 && Math.Abs(against - others) <= 1)
                readings.Add(new Reading { label = "House divided", evidence = "The vote split " + against + "–" + others + "." });
            else if (against >= 2 && others == 0)
                readings.Add(new Reading { label = "One mind", evidence = "Every vote went against " + Whom(state, recap.evictedId) + "." });
            if (recap.hohId != null && recap.hohId == recap.vetoHolderId)
                readings.Add(new Reading { label = "Power in one place", evidence = Who(state, recap.hohId) + " held the house and the veto." });
            // Broken word that reached the player, and alliances they saw come apart; alliances they
            // saw form. Read from the lines logged to them, never from a score.
            int broken = events.Count(e => Seen(e) && (e.kind == "promise-outcome" || e.kind == "deal-outcome") && Says(e, "broke", "fell out"))
                + events.Count(e => Seen(e) && e.kind == "alliance" && Says(e, "fallen apart", "left the alliance", "is finished", "is out of"));
            if (broken > 0)
                readings.Add(new Reading { label = "Trust tested", evidence = broken == 1 ? "A broken promise, deal or alliance reached you." : broken + " broken promises, deals or alliances reached you." });
            // Somebody brought into a pact of the player's (ACTIONS-DEALS-ALLIANCES-PLAN C5, "Maya Hassan
            // joined The Riley Pact.") grew one; it formed none.
            bool Joined(EpisodeEvent e) => e.kind == "alliance" && e.text != null && e.text.Contains(" joined ") && !Says(e, "formed", "brought you into");
            int formed = events.Count(e => Seen(e) && e.kind == "alliance" && Says(e, "formed", "brought you into", "joined") && !Joined(e));
            if (formed > 0)
                readings.Add(new Reading { label = "Alliances forming", evidence = formed == 1 ? "An alliance formed that you saw." : formed + " alliances formed that you saw." });
            int joined = events.Count(e => Seen(e) && Joined(e));
            if (joined > 0)
                readings.Add(new Reading { label = "Alliances growing", evidence = joined == 1 ? "Somebody joined an alliance of yours." : joined + " people joined alliances of yours." });
            if (readings.Count == 0)
                readings.Add(new Reading { label = "A quiet week", evidence = "Nothing you saw split the house." });
            return readings.Take(3).ToList();
        }

        /// <summary>
        /// After the week: for the week just closed, what the next holds that the house knows; for an
        /// earlier week, what the next one held. Nothing of anybody's plans.
        /// </summary>
        private static List<string> WhatsNext(EpisodeState state, Week recap, int week)
        {
            var lines = new List<string>();
            string Name(string id) => HudPrimitives.WithYou(state.Find(id)?.name ?? id, id == state.playerId);
            var next = state.ledger?.power?.FirstOrDefault(p => p.week == week + 1);
            if (week < state.week && next != null)
            {
                bool nextFinal = next.tally.Count == 0 && next.evicteeId != null && next.vetoHolderId == null;
                if (nextFinal) lines.Add("Week " + (week + 1) + ": the final Head of Household, " + Name(next.hohId) + ".");
                else if (next.hohId != null) lines.Add("Week " + (week + 1) + ": " + Name(next.hohId) + " won Head of Household.");
                if (next.evicteeId != null) lines.Add(Name(next.evicteeId) + " left the house that week.");
                return lines;
            }
            if (recap.finalDecision || week < state.week || !recap.closed) return lines;
            int remaining = recap.remaining > 0 ? recap.remaining : state.Active.Count();
            if (remaining <= 3) lines.Add("The final Head of Household begins: three parts, and the last choice.");
            else
            {
                lines.Add("Week " + (week + 1) + " begins with the Head of Household competition.");
                if (recap.hohId != null && state.Find(recap.hohId)?.status == ContestantStatus.Active)
                    lines.Add(Who(state, recap.hohId) + " held the house this week and cannot compete for it.");
            }
            lines.Add(remaining + (remaining == 1 ? " houseguest remains." : " houseguests remain."));
            var you = state.Find(state.playerId);
            if (you != null && you.status == ContestantStatus.Active)
            {
                int word = state.promises.Count(p => p.status == PromiseStatus.Active && (p.fromId == state.playerId || p.toId == state.playerId))
                    + state.deals.Count(d => d.status == DealStatus.Active && (d.proposerId == state.playerId || d.recipientId == state.playerId));
                if (word > 0) lines.Add(word == 1 ? "You carry one promise or deal into the week." : "You carry " + word + " promises and deals into the week.");
            }
            return lines;
        }

        private static string And(List<string> names)
        {
            if (names.Count == 0) return "nobody";
            if (names.Count == 1) return names[0];
            return string.Join(", ", names.Take(names.Count - 1)) + " and " + names[names.Count - 1];
        }

        /// <summary>A place in the words the career uses: "9th place".</summary>
        public static string PlaceWords(int place) =>
            place <= 0 ? null : place.ToString(CultureInfo.InvariantCulture) + Ordinal(place) + " place";

        private static string Ordinal(int value)
        {
            if (value % 100 >= 11 && value % 100 <= 13) return "th";
            switch (value % 10)
            {
                case 1: return "st";
                case 2: return "nd";
                case 3: return "rd";
                default: return "th";
            }
        }
    }
}
