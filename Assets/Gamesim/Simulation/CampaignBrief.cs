using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// What the campaign screen's goals and intel cards say (PACK8-PASS-PLAN B4, mockup 83). The
    /// mockup's own rows were invented - "Keep Avery off the block" is settled by the campaign, and
    /// "Riley thinks Jamie is a bigger threat" is an opinion nobody told the player - so these are
    /// read from the committed state instead, and only from rows the player owns: the reply cards
    /// that came to them and their answers, the vote read, their own pleas and calls, the plays they
    /// took on, the notes <see cref="HouseguestNotes"/> gathers, and the log lines addressed to them.
    /// Nothing here reads a houseguest's view of anybody, and nothing is saved.
    /// </summary>
    public static class CampaignBrief
    {
        /// <summary>What kind of thing a goal is: a card to answer, the read, the pleas, a call, a play.</summary>
        public static class GoalKinds
        {
            public const string Plea = "plea", Read = "read", Ask = "ask", Call = "call", Play = "play";
        }

        /// <summary>One row of the goals card: what to do, how far along it is, and whether it is done.</summary>
        public sealed class Goal
        {
            public string kind, text, progress;
            public bool done;
            public override string ToString() => (done ? "[x] " : "[ ] ") + text + (string.IsNullOrEmpty(progress) ? "" : " · " + progress);
        }

        /// <summary>One line of the intel card: the week it is from and what the player learned.</summary>
        public sealed class Intel
        {
            public int week;
            public string text;
            public override string ToString() => "Week " + week + " · " + text;
        }

        /// <summary>How many intel lines the card shows.</summary>
        public const int IntelShown = 3;

        /// <summary>
        /// The log lines that are something a houseguest brought to the player: a plea, a name, a
        /// rumour about them, a confrontation. The player's own moves and the house's public news
        /// are elsewhere on the screen.
        /// </summary>
        private static readonly string[] IntelKinds = { "campaign", "information", "gossip", "confrontation" };

        /// <summary>The notes that are news about the vote or a person's word, not the standing facts (pacts, memories) or what came to the player, which the log lines already say.</summary>
        private static readonly string[] IntelNotes =
            { HouseguestNotes.Kinds.Vote, HouseguestNotes.Kinds.Read, HouseguestNotes.Kinds.Word, HouseguestNotes.Kinds.Offer };

        /// <summary>
        /// The player's goals for this campaign, in the order the week gave them: the pleas that came
        /// to them, getting a read on the voters, asking the voters to keep them from the block, calling
        /// the vote in each alliance they are in, then the plays they took on. Empty outside a
        /// campaign or for a player who is out of the game.
        /// </summary>
        public static List<Goal> Goals(EpisodeState s)
        {
            var goals = new List<Goal>();
            if (s == null || s.phase != EpisodePhase.Campaign) return goals;
            if (s.Find(s.playerId)?.status != ContestantStatus.Active) return goals;
            string Name(string id) => s.Find(id)?.name ?? "Somebody";

            // Every plea that came this week: the ones answered, as the ledger has them, then the ones
            // still waiting, oldest first, as the screen offers them.
            var answered = new HashSet<string>(StringComparer.Ordinal);
            foreach (var reply in s.ledger.replies.Where(r => r.week == s.week && r.kind == ReplyCards.Plea))
            {
                if (!answered.Add(reply.fromId ?? "")) continue;
                var answer = ReplyCards.Find(reply.kind, reply.replyKey);
                goals.Add(new Goal
                {
                    kind = GoalKinds.Plea, text = "Answer " + Name(reply.fromId) + "'s plea", done = true,
                    progress = answer != null ? "You: " + answer.Label.ToLowerInvariant() : "Answered",
                });
            }
            foreach (var card in s.replyCards.Where(c => c.kind == ReplyCards.Plea))
            {
                if (!answered.Add(card.fromId ?? "")) continue;
                goals.Add(new Goal { kind = GoalKinds.Plea, text = "Answer " + Name(card.fromId) + "'s plea", progress = "Waiting on you" });
            }

            // The read: how many voters the player has a read on, or a word from.
            if (VoteRead.Available(s))
            {
                var sheet = VoteRead.Read(s);
                int voters = sheet.voters.Count, known = sheet.voters.Count(r => r.confidence != VoteRead.Unknown || r.saysId != null);
                if (voters > 0)
                    goals.Add(new Goal { kind = GoalKinds.Read, text = "Get a read on the voters", progress = known + " of " + voters, done = known >= voters });
            }

            // From the block, one plea per voter to keep the player.
            var askable = EpisodeEngine.Voters(s).Where(v => StrategyRules.CanBeAskedForTheirVote(s, v.id)).Select(v => v.id).ToList();
            if (askable.Count > 0)
            {
                int asked = askable.Count(id => s.lobbies.Any(l => l.week == s.week && l.phase == EpisodePhase.Campaign
                    && l.ask == LobbyAsk.Vote && l.deciderId == id));
                goals.Add(new Goal { kind = GoalKinds.Ask, text = "Ask the voters to keep you", progress = asked + " of " + askable.Count, done = asked >= askable.Count });
            }

            // Calling the vote, once a week in each alliance the player is in, while there is a vote
            // to call and a nominee who is not the player.
            if (EpisodeEngine.LeverRulesOn(s) && VoteRead.Available(s) && s.nominees.Any(id => id != s.playerId))
                foreach (var pact in s.alliances.Where(a => a.active && a.members.Contains(s.playerId)
                             && a.members.Any(id => id != s.playerId && s.Find(id)?.status == ContestantStatus.Active)))
                {
                    bool called = s.ledger.calls.Any(k => k.week == s.week && k.allianceId == pact.id && k.callerId == s.playerId);
                    goals.Add(new Goal { kind = GoalKinds.Call, text = "Call the vote in " + pact.name, progress = called ? "Called" : "Once this week", done = called });
                }

            // The plays the player took on and is still chasing, with how far along each is.
            foreach (var play in EpisodeEngine.Plays(s).Where(p => p.takenOn && p.ending == null))
                goals.Add(new Goal
                {
                    kind = GoalKinds.Play, text = play.goal, done = play.progress.Met,
                    progress = play.progress.need > 1 ? play.progress.have + " of " + play.progress.need : play.progress.Met ? "Done" : "Not yet",
                });
            return goals;
        }

        /// <summary>
        /// The newest things the player has learned, newest first, at most <paramref name="count"/>:
        /// the log lines a houseguest addressed to them, and the notes on every houseguest still in
        /// the house. A plea's log line counts only in the week it came; an older one read "this
        /// week" beside another week's date. Within a week the log lines come first, newest first,
        /// and then the notes in cast order.
        /// </summary>
        public static List<Intel> RecentIntel(EpisodeState s, int count = IntelShown)
        {
            var found = new List<(Intel line, int rank, long order)>();
            if (s == null || count <= 0) return new List<Intel>();
            foreach (var entry in s.events)
            {
                if (entry.audienceIds == null || !entry.audienceIds.Contains(s.playerId) || Array.IndexOf(IntelKinds, entry.kind) < 0) continue;
                if (entry.kind == "campaign" && entry.week != s.week) continue;
                found.Add((new Intel { week = entry.week, text = entry.text }, 0, -entry.sequence));
            }
            long index = 0;
            foreach (var who in s.contestants.Where(c => !c.isPlayer && c.status == ContestantStatus.Active))
                foreach (var note in HouseguestNotes.For(s, who.id))
                {
                    if (Array.IndexOf(IntelNotes, note.kind) < 0) continue;
                    found.Add((new Intel { week = note.week, text = note.text }, 1, index++));
                }
            return found.OrderByDescending(x => x.line.week).ThenBy(x => x.rank).ThenBy(x => x.order)
                .Select(x => x.line).Take(count).ToList();
        }

        /// <summary>When a line is from, as the card dates it: this week, or the week's number. The sim keeps weeks, not days.</summary>
        public static string When(EpisodeState s, int week) => s != null && week == s.week ? "This week" : "Week " + week;
    }
}
