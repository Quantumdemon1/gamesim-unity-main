using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;

namespace Gamesim.Presentation
{
    /// <summary>
    /// The season's eviction votes, as the player's character can know them: one record a week, the
    /// count from the ledger and the ballots from <see cref="KnownBallots"/>.
    ///
    /// <para>The notebook's vote page read <see cref="EpisodeState.votes"/>, which is the current
    /// week's ballot box: the engine clears it when the week after an eviction begins. So from the
    /// second week's first competition the page said "Nobody has voted yet this season" over a
    /// season whose first vote had evicted somebody. It then read the reveal's public lines, which
    /// named every ballot. The reveal now reads the count and nothing else (UI-UX-PASS-PLAN B0): the
    /// count survives in the ledger's power row, and a ballot is on the record only as the player
    /// knows it - their own, the Head of Household's tie-break, what the count proves, what they were
    /// told, what a save from before kept in the open - each with its basis; the rest are unknown
    /// slots that name nobody. Nothing here changes the simulation.</para>
    ///
    /// <para>The event log keeps the last 256 entries; the ledger outlives it, so a week's count is
    /// on the record long after its lines have rolled off. Only a save without a ledger row for a
    /// week whose lines are gone is said to be missing.</para>
    /// </summary>
    public static class VoteRecords
    {
        /// <summary>The engine's reason on the player's own ballot, which says nothing to them.</summary>
        public const string PlayerReason = KnownBallots.PlayerReason;

        public sealed class Ballot
        {
            /// <summary>Null on an unknown slot, which names nobody.</summary>
            public string VoterId, VoterName, TargetId, TargetName;
            /// <summary>The voter's public reason, where a line read in the open carried one; null otherwise, and on the player's own.</summary>
            public string Reason;
            public bool ByPlayer, AgainstPlayer, TieBreak;
            /// <summary>One of <see cref="KnownBallots.Basis"/>; <see cref="KnownBallots.Basis.Unknown"/> on a slot.</summary>
            public string Basis = KnownBallots.Basis.Unknown;
            /// <summary>What the voter said, where the ballot is known by a claim; null otherwise.</summary>
            public string SaidName;
            /// <summary>A claim the reveal caught out: they said one name and cast the other. Worded by <see cref="KnownBallots.SaidWords"/>: of an ally's account, a vote that changed, never a lie.</summary>
            public bool Lied;
            public bool Known => TargetId != null;
        }

        public sealed class Tally
        {
            public string Name;
            public int Votes;
        }

        /// <summary>One line for the ballots the player cannot place: a count, never a name - the same words on the notebook's vote page and the recap's vote tab.</summary>
        public static string UnknownBallotsLine(int count) =>
            count == 1 ? "1 ballot you do not know how it went." : count + " ballots you do not know how they went.";

        public sealed class Record
        {
            public int Week;
            /// <summary>Null when the week's eviction is neither in the log nor in the ledger.</summary>
            public string EvictedName;
            public bool EvictedIsPlayer;
            /// <summary>Decided by the final Head of Household rather than by a vote.</summary>
            public bool FinalDecision;
            /// <summary>Whether the week's result is on the record: its eviction line, or the ledger's row with the count.</summary>
            public bool Complete;
            /// <summary>Every ballot cast: the known ones first, then an unknown slot for each the player cannot place.</summary>
            public readonly List<Ballot> Ballots = new List<Ballot>();
            /// <summary>Votes per nominee from the ledger's count, the tie-break excluded as the engine counts them; empty unless complete.</summary>
            public readonly List<Tally> Counts = new List<Tally>();
            public bool TieBroken;
            public int Known => Ballots.Count(b => b.Known);
            public int Unknown => Ballots.Count(b => !b.Known);
        }

        public sealed class Book
        {
            /// <summary>Newest first.</summary>
            public readonly List<Record> Records = new List<Record>();
            /// <summary>Evictions the season has had whose record is neither in the log nor in the ledger.</summary>
            public int Missing;
            /// <summary>Whether an eviction vote is under way and not yet revealed.</summary>
            public bool VoteInProgress;
            /// <summary>The player's own ballot in that vote, if they have cast one.</summary>
            public Ballot OwnPendingBallot;
            /// <summary>No one has been evicted yet: the one state in which "no vote yet" is true.</summary>
            public bool NoEvictionYet => Records.Count == 0 && Missing == 0;
        }

        public static Book Read(EpisodeState state)
        {
            var book = new Book();
            if (state == null) return book;

            var lines = state.events
                .Where(e => e.kind == "eviction" || e.kind == "final-eviction")
                .Where(e => e.audienceIds == null || e.audienceIds.Count == 0 || e.audienceIds.Contains(state.playerId))
                .ToList();
            var weeks = new SortedSet<int>(lines.Select(e => e.week));
            foreach (int week in KnownBallots.Weeks(state)) weeks.Add(week);
            foreach (var row in state.ledger?.power ?? new List<PowerRow>())
                if (row != null && row.evicteeId != null) weeks.Add(row.week);

            foreach (int week in weeks.Reverse())
            {
                var record = new Record { Week = week };
                var sheet = KnownBallots.Read(state, week);
                if (sheet.pending) continue;
                var gone = lines.Where(e => e.week == week).OrderBy(e => e.sequence).FirstOrDefault();
                var evictee = state.Find(sheet.evictedId);
                if (gone != null)
                {
                    record.Complete = true;
                    record.FinalDecision = gone.kind == "final-eviction";
                    // The engine names the player too ("You are evicted..." for the default "You").
                    record.EvictedName = record.FinalDecision ? FinalEvictee(state, gone.text) : WeeklyRecap.Subject(state, gone.text);
                }
                else if (evictee != null || sheet.final)
                {
                    // The ledger outlives the log: the row says who went, and by what count.
                    record.Complete = true;
                    record.FinalDecision = sheet.final;
                    record.EvictedName = evictee?.name ?? state.Find(state.ledger?.power?.LastOrDefault(p => p.week == week)?.evicteeId)?.name;
                }
                record.EvictedIsPlayer = record.EvictedName != null && record.EvictedName == state.Find(state.playerId)?.name;
                if (!record.FinalDecision)
                {
                    foreach (var ballot in sheet.ballots) record.Ballots.Add(From(state, ballot));
                    if (sheet.Revealed)
                        for (int i = 0; i < sheet.nominees.Count; i++)
                            record.Counts.Add(new Tally { Name = state.Find(sheet.nominees[i])?.name ?? sheet.nominees[i], Votes = sheet.tally[i] });
                    record.Counts.Sort((a, b) => b.Votes.CompareTo(a.Votes));
                    record.TieBroken = sheet.tieBroken;
                }
                if (record.Complete || record.Ballots.Count > 0) book.Records.Add(record);
            }

            int evictions = state.contestants.Count(c => c.status == ContestantStatus.Jury || c.status == ContestantStatus.Evicted);
            book.Missing = Math.Max(0, evictions - book.Records.Count(r => r.Complete));

            if (state.phase == EpisodePhase.Eviction && !state.evictionResolved)
            {
                book.VoteInProgress = true;
                var pending = KnownBallots.Read(state, state.week).ballots.FirstOrDefault(b => b.voterId == state.playerId);
                if (pending != null) book.OwnPendingBallot = From(state, pending);
            }
            return book;
        }

        /// <summary>
        /// One ballot out of "Maya Hassan voted to evict Casey Wilson. Reason", as a save from before
        /// ballots went private holds them in the open (<see cref="KnownBallots.ReadRevealLine"/>).
        /// Null when the line does not read.
        /// </summary>
        public static Ballot ReadBallot(EpisodeState state, string text, string headOfHousehold = null)
        {
            if (state == null) return null;
            string hohId = headOfHousehold == null ? null : state.contestants.FirstOrDefault(c => c.name == headOfHousehold)?.id;
            var read = KnownBallots.ReadRevealLine(state, text, hohId);
            return read == null ? null : From(state, read);
        }

        private static Ballot From(EpisodeState state, KnownBallots.Ballot ballot)
        {
            if (ballot == null) return null;
            var voter = state.Find(ballot.voterId);
            var target = state.Find(ballot.targetId);
            return new Ballot
            {
                VoterId = ballot.voterId, VoterName = voter?.name, TargetId = ballot.targetId, TargetName = target?.name,
                Reason = ballot.reason, ByPlayer = ballot.voterId == state.playerId, AgainstPlayer = ballot.targetId == state.playerId,
                TieBreak = ballot.basis == KnownBallots.Basis.TieBreak, Basis = ballot.basis,
                SaidName = state.Find(ballot.saidId)?.name, Lied = ballot.Lied,
            };
        }

        /// <summary>
        /// Who went home out of "Maya takes Casey to the final two. Riley joins the jury." - the
        /// name that opens the second sentence ("You join the jury." when it is the player).
        /// </summary>
        internal static string FinalEvictee(EpisodeState state, string text)
        {
            int at = text.IndexOf(". ", StringComparison.Ordinal);
            if (at < 0) return null;
            string second = text.Substring(at + 2);
            if (second.StartsWith("You ", StringComparison.Ordinal)) return state.Find(state.playerId)?.name;
            return WeeklyRecap.Subject(state, second);
        }
    }
}
