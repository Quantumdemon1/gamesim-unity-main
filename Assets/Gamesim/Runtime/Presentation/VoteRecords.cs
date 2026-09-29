using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;

namespace Gamesim.Presentation
{
    /// <summary>
    /// The season's eviction votes, as the player's character can know them: read back out of the
    /// public record, one entry a week.
    ///
    /// <para>The notebook's vote page read <see cref="EpisodeState.votes"/>, which is the current
    /// week's ballot box: the engine clears it when the week after an eviction begins. So from the
    /// second week's first competition the page said "Nobody has voted yet this season" over a
    /// season whose first vote had evicted somebody. Every eviction ballot is published at the
    /// reveal as a public "vote-reveal" event, after the public "eviction" line, and those events
    /// survive the week - that is what this reads. Nothing here changes the simulation.</para>
    ///
    /// <para>What is known, by the game's own rules: the player's own ballot from the moment it is
    /// cast; after the reveal, every ballot with its public reason and the result; the final Head of
    /// Household's choice. Other ballots in a vote not yet revealed are not known, and this never
    /// reads <see cref="EpisodeState.votes"/> for anyone but the player. Jury ballots belong to the
    /// finale and the season report, not here.</para>
    ///
    /// <para>The event log keeps the last 256 entries, so a long season's early weeks roll off it.
    /// A week whose eviction line is gone is said to be missing rather than guessed at, and ballots
    /// that survive without their eviction line carry no tally.</para>
    /// </summary>
    public static class VoteRecords
    {
        /// <summary>The engine's reason on the player's own ballot, which says nothing to them.</summary>
        public const string PlayerReason = "Player's decision";
        private const string TieBreakPrefix = "HoH tie-break: ";
        private const string Evicts = " voted to evict ";

        public sealed class Ballot
        {
            public string VoterId, VoterName, TargetId, TargetName;
            /// <summary>The voter's public reason; null on the player's own ballot.</summary>
            public string Reason;
            public bool ByPlayer, AgainstPlayer, TieBreak;
        }

        public sealed class Tally
        {
            public string Name;
            public int Votes;
        }

        public sealed class Record
        {
            public int Week;
            /// <summary>Null when the week's eviction line has rolled off the log.</summary>
            public string EvictedName;
            public bool EvictedIsPlayer;
            /// <summary>Decided by the final Head of Household rather than by a vote.</summary>
            public bool FinalDecision;
            /// <summary>Whether the week's eviction line survives, so its ballots are all there.</summary>
            public bool Complete;
            public readonly List<Ballot> Ballots = new List<Ballot>();
            /// <summary>Votes per nominee, the tie-break excluded as the engine counts them; empty unless complete.</summary>
            public readonly List<Tally> Counts = new List<Tally>();
            public bool TieBroken => Ballots.Any(b => b.TieBreak);
        }

        public sealed class Book
        {
            /// <summary>Newest first.</summary>
            public readonly List<Record> Records = new List<Record>();
            /// <summary>Evictions the season has had whose record is no longer in the log.</summary>
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

            var weeks = state.events
                .Where(e => e.kind == "eviction" || e.kind == "final-eviction" || e.kind == "vote-reveal")
                .Where(e => e.audienceIds == null || e.audienceIds.Count == 0 || e.audienceIds.Contains(state.playerId))
                .GroupBy(e => e.week)
                .OrderByDescending(g => g.Key);
            foreach (var week in weeks)
            {
                var lines = week.OrderBy(e => e.sequence).ToList();
                var record = new Record { Week = week.Key };
                var gone = lines.FirstOrDefault(e => e.kind == "eviction" || e.kind == "final-eviction");
                if (gone != null)
                {
                    record.Complete = true;
                    record.FinalDecision = gone.kind == "final-eviction";
                    // The engine names the player too ("You are evicted..." for the default "You").
                    record.EvictedName = record.FinalDecision ? FinalEvictee(state, gone.text) : WeeklyRecap.Subject(state, gone.text);
                    record.EvictedIsPlayer = record.EvictedName != null && record.EvictedName == state.Find(state.playerId)?.name;
                }
                string hoh = WeeklyRecap.Build(state, week.Key).headOfHousehold;
                foreach (var line in lines.Where(e => e.kind == "vote-reveal"))
                {
                    var ballot = ReadBallot(state, line.text, hoh);
                    if (ballot != null) record.Ballots.Add(ballot);
                }
                if (record.Complete)
                    foreach (var group in record.Ballots.Where(b => !b.TieBreak).GroupBy(b => b.TargetName).OrderByDescending(g => g.Count()))
                        record.Counts.Add(new Tally { Name = group.Key, Votes = group.Count() });
                if (record.Complete || record.Ballots.Count > 0) book.Records.Add(record);
            }

            int evictions = state.contestants.Count(c => c.status == ContestantStatus.Jury || c.status == ContestantStatus.Evicted);
            book.Missing = Math.Max(0, evictions - book.Records.Count(r => r.Complete));

            if (state.phase == EpisodePhase.Eviction && !state.evictionResolved)
            {
                book.VoteInProgress = true;
                var own = state.votes?.FirstOrDefault(v => v.voterId == state.playerId);
                if (own != null)
                    book.OwnPendingBallot = new Ballot
                    {
                        VoterId = state.playerId, VoterName = state.Find(state.playerId)?.name,
                        TargetId = own.targetId, TargetName = state.Find(own.targetId)?.name, ByPlayer = true,
                    };
            }
            return book;
        }

        /// <summary>
        /// One ballot out of "Maya Hassan voted to evict Casey Wilson. Reason" - the voter and the
        /// target by the cast's names (longest first, so "Jamie Roberts" is not "Jamie"), "you" for
        /// the player as the engine writes it. Null when the line does not read.
        /// </summary>
        public static Ballot ReadBallot(EpisodeState state, string text, string headOfHousehold = null)
        {
            if (state == null || string.IsNullOrEmpty(text)) return null;
            string voter = WeeklyRecap.Subject(state, text);
            if (voter == null || !text.Substring(voter.Length).StartsWith(Evicts, StringComparison.Ordinal)) return null;
            string rest = text.Substring(voter.Length + Evicts.Length);
            var player = state.Find(state.playerId);
            string target;
            bool againstPlayer = false;
            if (rest.StartsWith("you.", StringComparison.Ordinal) || rest.StartsWith("yourself.", StringComparison.Ordinal))
            {
                target = player?.name; againstPlayer = true;
                rest = rest.Substring(rest.IndexOf('.') + 1);
            }
            else
            {
                target = state.contestants.Where(c => !string.IsNullOrEmpty(c.name) && rest.StartsWith(c.name + ".", StringComparison.Ordinal))
                    .OrderByDescending(c => c.name.Length).FirstOrDefault()?.name;
                if (target == null) return null;
                rest = rest.Substring(target.Length + 1);
                againstPlayer = player != null && target == player.name;
            }
            string reason = rest.Trim();
            bool tieBreak = reason.StartsWith(TieBreakPrefix, StringComparison.Ordinal) || (headOfHousehold != null && voter == headOfHousehold);
            if (reason.StartsWith(TieBreakPrefix, StringComparison.Ordinal)) reason = reason.Substring(TieBreakPrefix.Length).Trim();
            bool byPlayer = player != null && voter == player.name;
            if (reason == PlayerReason || reason.Length == 0) reason = null;
            return new Ballot
            {
                VoterId = state.contestants.FirstOrDefault(c => c.name == voter)?.id, VoterName = voter,
                TargetId = state.contestants.FirstOrDefault(c => c.name == target)?.id, TargetName = target,
                Reason = reason, ByPlayer = byPlayer, AgainstPlayer = againstPlayer, TieBreak = tieBreak,
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
