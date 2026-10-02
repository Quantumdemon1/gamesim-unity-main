using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// What your character has on a houseguest, gathered from the season's own records into one
    /// place: their word to you and yours to them, what they told you about the vote and whether it
    /// held, what you read of them, what they put to you and how you answered, the pacts you share
    /// and what you remember of them. Nothing you have not learned: every line is a row the engine
    /// wrote when it happened. The notebook's notes page and the free-time cards read it.
    ///
    /// <para>One line is not a row but the house's own behaviour, by design (ACTIONS-DEALS-ALLIANCES-PLAN
    /// C3): an ally whose own commitment to you has lapsed has "gone quiet" (<see cref="Allegiance.GoneQuiet"/>),
    /// in words and never a number - the warning before they can end the pact from their side.</para>
    /// </summary>
    public static class HouseguestNotes
    {
        /// <summary>What kind of thing a note is, for the page's filters.</summary>
        public static class Kinds
        {
            public const string Word = "word", Vote = "vote", Read = "read", Offer = "offer", Came = "came", Pact = "pact", Memory = "memory";
        }

        /// <summary>One thing you have on somebody: the week, the kind, the line, and a few words for a card.</summary>
        public sealed class Note
        {
            public int week;
            public string kind, text, brief;
            /// <summary>
            /// The line for a row with room for one short line - the campaign's Recent Intel - where
            /// <see cref="text"/> says more than that row holds; null where the text is short already.
            /// </summary>
            public string compact;
            public override string ToString() => "Week " + week + " · " + text;
        }

        private static readonly string[] KindOrder = { Kinds.Came, Kinds.Vote, Kinds.Read, Kinds.Offer, Kinds.Word, Kinds.Pact, Kinds.Memory };

        /// <summary>Everything on one houseguest, newest first; empty for the player or nobody.</summary>
        public static List<Note> For(EpisodeState s, string id)
        {
            var notes = new List<Note>();
            var who = s?.Find(id);
            if (who == null || who.isPlayer) return notes;
            string first = FirstName(who.name);
            string Name(string other) => other == s.playerId ? "you" : s.Find(other)?.name ?? "somebody";

            foreach (var promise in s.promises.Where(p => (p.fromId == id && p.toId == s.playerId) || (p.fromId == s.playerId && p.toId == id)))
            {
                bool theirs = promise.fromId == id;
                // Their vote promise ended by their ballot is told once you know the ballot (decision 4).
                string standing = KnownBallots.PromiseOutcomeKnown(s, promise) ? PromiseStanding(promise.status) : KnownBallots.Unresolved;
                notes.Add(new Note
                {
                    week = promise.week, kind = Kinds.Word,
                    text = (theirs ? first + " promised you " : "You promised " + first + " ") + PromiseWord(promise.kind) + " · " + standing,
                    brief = (theirs ? "Their word: " : "Your word: ") + standing,
                });
            }
            foreach (var deal in s.deals.Where(d => (d.proposerId == id && d.recipientId == s.playerId) || (d.proposerId == s.playerId && d.recipientId == id)))
            {
                bool theirs = deal.proposerId == id;
                string title = DealKind.Title(deal.type).ToLowerInvariant();
                string standing = KnownBallots.DealOutcomeKnown(s, deal) ? DealStanding(deal.status, theirs) : KnownBallots.Unresolved;
                // Whom it is about, where it names somebody, and how it ended where the record can say.
                // The campaign's one-line row keeps the offer and where it stands, as it always read.
                string about = DealKind.NamesATarget(deal.type) && s.Find(deal.targetId) != null ? " (about " + Name(deal.targetId) + ")" : "";
                string offer = theirs ? first + " put a " + title + " to you" : "You put a " + title + " to " + first;
                notes.Add(new Note
                {
                    week = deal.week, kind = Kinds.Offer,
                    text = offer + about + " · " + DealEnding(s, deal, theirs),
                    compact = offer + " · " + standing,
                    brief = theirs && deal.status == DealStatus.Proposed ? "An offer waiting on you" : (theirs ? "Their offer: " : "Your offer: ") + standing,
                });
            }
            foreach (var claim in s.ledger.claims.Where(c => c.voterId == id))
            {
                string how = claim.source == ClaimSource.Told ? first + " told you: evict "
                    : claim.source == ClaimSource.Overheard ? "Overheard: " + first + " is voting out "
                    : "An ally heard " + first + " is voting out ";
                string verdict = claim.status == ClaimStatus.Kept ? " · and did" : claim.status == ClaimStatus.Lied ? " · a lie" : "";
                notes.Add(new Note
                {
                    week = claim.week, kind = Kinds.Vote, text = how + Name(claim.targetId) + verdict,
                    brief = claim.status == ClaimStatus.Lied ? "Lied to you about the vote"
                        : claim.status == ClaimStatus.Kept ? "Told you the truth about the vote"
                        : "Says: evict " + FirstName(Name(claim.targetId)),
                });
            }
            foreach (var standing in s.ledger.standings.Where(r => r.fromId == id && r.toId == s.playerId))
            {
                if (standing.source == ClaimSource.Read)
                {
                    string band = standing.score >= 25 ? "warm on you" : standing.score <= -25 ? "cold on you" : "not sure about you";
                    // What they are up to is this week's read; an older read's agenda is not kept.
                    string doing = standing.week == s.week ? NpcAgendas.Describe(s, id, NpcAgendas.Of(s, id)) : null;
                    notes.Add(new Note { week = standing.week, kind = Kinds.Read, text = "You read " + first + ": " + band + (doing != null ? ". " + doing : ""), brief = "Read: " + band });
                }
                else if (standing.source == ClaimSource.Missed)
                    notes.Add(new Note { week = standing.week, kind = Kinds.Read, text = "You couldn't get a read on " + first, brief = "No read on them" });
                else if (standing.source == ClaimSource.Deflected)
                    notes.Add(new Note { week = standing.week, kind = Kinds.Vote, text = first + " wouldn't say where their vote is", brief = "Wouldn't say their vote" });
            }
            foreach (var reply in s.ledger.replies.Where(r => r.fromId == id))
            {
                var answer = ReplyCards.Find(reply.kind, reply.replyKey);
                string what = reply.kind == ReplyCards.Confrontation ? first + " confronted you"
                    : reply.kind == ReplyCards.Gossip ? first + " talked about you to " + Name(reply.listenerId)
                    : first + " asked for your vote";
                notes.Add(new Note
                {
                    week = reply.week, kind = Kinds.Came,
                    text = what + (answer != null ? " · you: " + answer.Label.ToLowerInvariant() : ""),
                    brief = reply.kind == ReplyCards.Confrontation ? "Confronted you" : reply.kind == ReplyCards.Gossip ? "Talked about you" : "Asked for your vote",
                });
            }
            foreach (var call in s.ledger.calls.Where(c => c.callerId == s.playerId && (c.followed.Contains(id) || c.defected.Contains(id))))
            {
                bool followed = call.followed.Contains(id);
                notes.Add(new Note
                {
                    week = call.week, kind = Kinds.Word,
                    text = first + (followed ? " followed your call to evict " : " ignored your call to evict ") + Name(call.targetId),
                    brief = followed ? "Followed your call" : "Ignored your call",
                });
            }
            bool rules = EpisodeEngine.CommitmentRulesOn(s);
            foreach (var alliance in s.alliances.Where(a => a.members.Contains(s.playerId) && a.members.Contains(id)))
            {
                var row = s.ledger.alliances.FirstOrDefault(x => x.id == alliance.id);
                // Under the commitment rules somebody who has left the house has left every pact, though a
                // pact of three or more goes on without them (X5): they did, or the player did.
                bool theyLeft = who.status != ContestantStatus.Active;
                bool youLeft = s.Find(s.playerId)?.status != ContestantStatus.Active;
                bool left = rules && alliance.active && (theyLeft || youLeft);
                string gone = theyLeft && youLeft ? "you both left the house" : theyLeft ? first + " left the house" : "you left the house";
                notes.Add(new Note
                {
                    week = row?.startedWeek ?? 0, kind = Kinds.Pact,
                    text = left ? "You were both in " + alliance.name + " · " + gone
                        : "You are both in " + alliance.name + (alliance.active ? "" : " · ended"),
                    brief = left ? (theyLeft ? first + " left " + alliance.name : "You left " + alliance.name)
                        : alliance.active ? "In " + alliance.name + " with you" : alliance.name + " ended",
                });
            }
            // Under the commitment rules (C2) an ally who turned on a pact of yours, where you can know it:
            // the act as your record holds it - a ballot only once it is yours to know - and while it is
            // this week's, the way out it opened.
            foreach (var betrayal in Allegiance.KnownBetrayals(s, id))
            {
                bool open = betrayal.week == s.week && Allegiance.FreeExit(s, id);
                notes.Add(new Note
                {
                    week = betrayal.week, kind = Kinds.Pact,
                    text = betrayal.description.TrimEnd('.') + (open ? " · you can cut ties this week at no cost" : ""),
                    brief = "Turned on your pact",
                });
            }
            // An ally gone quiet (C3): their own commitment has lapsed. Said in words, never a number;
            // a refused call and a read are the other two ways the player learns it.
            if (Allegiance.GoneQuiet(s, id))
            {
                var pacts = s.alliances.Where(a => a.active && a.members.Contains(s.playerId) && a.members.Contains(id)).Select(a => a.name);
                notes.Add(new Note { week = s.week, kind = Kinds.Pact, text = first + " has gone quiet on " + Allegiance.Join(pacts), brief = "Gone quiet on you" });
            }
            // A memory of a vote deal's or a vote promise's ending tells the ballot that ended it:
            // left out while that ballot is not yours to know (KnownBallots; decision 4).
            foreach (var memory in KnownBallots.PlayerMemories(s).Where(m => m.subjectId == id && !string.IsNullOrEmpty(m.text)))
                notes.Add(new Note { week = memory.week, kind = Kinds.Memory, text = memory.text });

            return notes.OrderByDescending(n => n.week).ThenBy(n => Array.IndexOf(KindOrder, n.kind)).ToList();
        }

        /// <summary>The latest thing you have on them, in a few words for a card, or null when there is nothing.</summary>
        public static string Brief(EpisodeState s, string id) => For(s, id).FirstOrDefault(n => n.brief != null)?.brief;

        /// <summary>Whether a note is one of a kind of thing: their word (promises, offers, calls, pacts), the vote, or your reads (reads, what came to you, what you remember).</summary>
        public static bool IsTheirWord(Note note) => note.kind == Kinds.Word || note.kind == Kinds.Offer || note.kind == Kinds.Pact;
        public static bool IsTheVote(Note note) => note.kind == Kinds.Vote;
        public static bool IsYourRead(Note note) => note.kind == Kinds.Read || note.kind == Kinds.Came || note.kind == Kinds.Memory;

        private static string FirstName(string name) => string.IsNullOrEmpty(name) ? "" : name.Split(' ')[0];

        /// <summary>A promise's kind as the notes say it; the notebook's own words, unlocalised here.</summary>
        public static string PromiseWord(PromiseKind kind)
        {
            switch (kind)
            {
                case PromiseKind.Safety: return "safety";
                case PromiseKind.Vote: return "a vote";
                case PromiseKind.FinalTwo: return "a final two";
                case PromiseKind.AllianceLoyalty: return "alliance loyalty";
                case PromiseKind.Information: return "information";
                default: return kind.ToString();
            }
        }

        /// <summary>Where a promise stands, in a word.</summary>
        public static string PromiseStanding(PromiseStatus status)
        {
            switch (status)
            {
                case PromiseStatus.Fulfilled: return "kept";
                case PromiseStatus.Broken: return "broken";
                case PromiseStatus.Expired: return "expired";
                default: return "still standing";
            }
        }

        /// <summary>
        /// How a deal ended, where the record can say: who broke a broken one, as
        /// <see cref="FinalistRead.DealBreaker"/> reads it off the public record and the ballots the
        /// player knows, or "broken" blaming nobody where it cannot - deals do not record who broke
        /// them. A voting block both partners settle at once fell apart. A vote deal the other party
        /// settled by a ballot the player does not know is unresolved (decision 4). Anything else,
        /// where it stands.
        /// </summary>
        public static string DealEnding(EpisodeState s, DealState deal, bool theirs)
        {
            if (deal == null) return "";
            if (s != null && !KnownBallots.DealOutcomeKnown(s, deal)) return KnownBallots.Unresolved;
            if (deal.status != DealStatus.Broken || s == null) return DealStanding(deal.status, theirs);
            string breaker = FinalistRead.DealBreaker(s, deal);
            if (breaker == s.playerId) return "broken by you";
            if (breaker != null) return "broken by " + FirstName(s.Find(breaker)?.name);
            return deal.type == DealKind.VoteTogether ? "fell apart" : "broken";
        }

        /// <summary>Where a deal stands, in a word or two.</summary>
        public static string DealStanding(string status, bool theirs)
        {
            switch (status)
            {
                case DealStatus.Proposed: return theirs ? "waiting on you" : "waiting on them";
                case DealStatus.Accepted:
                case DealStatus.Active: return "agreed";
                case DealStatus.Fulfilled: return "honoured";
                case DealStatus.Broken: return "broken";
                case DealStatus.Declined: return theirs ? "you declined" : "they declined";
                case DealStatus.Expired: return "lapsed";
                default: return status ?? "";
            }
        }
    }
}
