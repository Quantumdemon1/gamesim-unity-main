using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// Betrayal as an event (ACTIONS-DEALS-ALLIANCES-PLAN C2, decision 11): an ally who nominates the
    /// player or names them the replacement, breaks a veto commitment with them, or at the reveal votes
    /// to evict them, refused their call and voted the other way, or broke a vote deal with them by their
    /// ballot, turns on their pact. What it writes, what it changes and the player's way out; who it
    /// holds against, and what the player can know of it, is <see cref="Allegiance"/>'s.
    ///
    /// <para>Only under the commitment rules (<see cref="CommitmentRulesOn"/>): every writer here returns
    /// before it rolls, logs or mints anything in a season without them, so a recorded season plays as
    /// it did.</para>
    /// </summary>
    public sealed partial class EpisodeEngine
    {
        /// <summary>
        /// An ally turned on their pact with the player: once a week for each ally, the first act that
        /// week is the one on the record. It writes the dormant <see cref="Allegiance.BetrayedType"/>
        /// entry on the player's own record of them - permanent, at the reference's −50 - and a line.
        /// The line goes to the two of them with the choice it opens, cutting ties this week at no cost
        /// or staying allied; one told by a ballot the player cannot place yet (<paramref name="ballot"/>,
        /// and not one the count, the tie-break or a judged claim already shows them) goes to the
        /// betrayer alone, and the notes say it once the ballot is the player's to know.
        ///
        /// <para>It moves nobody's view: the player's view of them is the player's to change, and a
        /// betrayal that took it under the line a pact sours at would end the pact the player may choose
        /// to keep. The ledger's entry does act at once, hidden or not: it is the player's own record,
        /// so their trust of the betrayer falls under 35 and the reputation threat they read in them
        /// rises (<see cref="ThreatAssessment"/>), which an NPC Head of Household in a pact with the
        /// player weighs among their pact-mates' readings (<see cref="ThreatTerm"/>). Nothing happens
        /// once the player is out of the house, or with somebody they share no standing pact with.</para>
        /// </summary>
        private static void Betrayal(EpisodeState s, string npcId, string act, bool ballot)
        {
            if (!CommitmentRulesOn(s) || string.IsNullOrEmpty(npcId) || npcId == s.playerId) return;
            if (s.Find(s.playerId)?.status != ContestantStatus.Active) return;
            var npc = s.Find(npcId);
            if (npc == null || npc.isPlayer) return;
            var pacts = s.alliances.Where(a => a.active && a.members.Contains(s.playerId) && a.members.Contains(npcId)).ToList();
            if (pacts.Count == 0 || Allegiance.BetrayalWeek(s, npcId) == s.week) return;
            string record = Allegiance.Record(npc.name, act, pacts.Select(a => a.name));
            RelationshipLedger.RecordOneWay(s, s.playerId, npcId, Allegiance.BetrayedType, Allegiance.BetrayalImpact, record);
            bool hidden = ballot && !KnownBallots.Knows(s, s.week, npcId);
            if (hidden) Log(s, Allegiance.LogKind, record, npcId);
            else Log(s, Allegiance.LogKind, record + " " + Allegiance.Offer(npc.name), s.playerId, npcId);
        }

        /// <summary>
        /// The reveal's betrayals, each told by a ballot: an ally's ballot to evict the player; then a
        /// call the ally refused and voted against (the call's target spared by their ballot - a refused
        /// call alone is no betrayal); then a vote deal an ally broke with them by theirs. Nothing when
        /// the player is the one going, since they leave every pact with the house; the Head of
        /// Household's deciding vote against them evicts them, so it is never one of these.
        /// </summary>
        private static void BallotBetrayals(EpisodeState s, string evicted)
        {
            if (!CommitmentRulesOn(s) || evicted == s.playerId) return;
            foreach (var vote in s.votes.Where(v => v.targetId == s.playerId && v.voterId != s.playerId && v.voterId != s.hohId).ToList())
                Betrayal(s, vote.voterId, Allegiance.VotedAgainst, true);
            foreach (var call in s.ledger.calls.Where(k => k.week == s.week && k.callerId == s.playerId).ToList())
                foreach (var id in call.defected)
                {
                    var ballot = s.votes.FirstOrDefault(v => v.voterId == id);
                    if (ballot != null && ballot.targetId != call.targetId) Betrayal(s, id, Allegiance.IgnoredCall(Name(s, call.targetId)), true);
                }
            foreach (var deal in s.deals.Where(d => d.status == DealStatus.Broken && d.settledWeek == s.week && KnownBallots.IsVoteDeal(d.type)
                         && !string.IsNullOrEmpty(d.brokenById) && d.brokenById != s.playerId
                         && DealResolution.Partner(d, d.brokenById) == s.playerId).ToList())
                Betrayal(s, deal.brokenById, Allegiance.BrokeDeal(deal.type), true);
        }

        /// <summary>
        /// Cutting ties with an ally who turned on the pact this week (C2's free exit,
        /// <see cref="Allegiance.FreeExit"/>): a pact of two the player shares with them ends, as leaving
        /// it would; a bigger one goes on without them, the player and the loyal members keeping it. It
        /// costs nothing - no action, no warmth with them (so no roll), and no grudge from anyone in it,
        /// the betrayer included - but it is said where a leave is said: in free time and the campaign, or
        /// in a window to whoever the window lets the player talk to (<see cref="RequireConversationWindow"/>).
        /// A pact of two's ledger row says it ended in a betrayal (<see cref="AllianceEnding"/>). Everybody
        /// in each pact hears it, a line to a pact. Leaving outside the week, or leaving somebody who never
        /// turned on it, is the leave it always was.
        /// </summary>
        private static void CutTies(EpisodeState s, EpisodeCommand c, ContestantState betrayer)
        {
            RequireConversationWindow(s, c);
            Require(s.Find(s.playerId).status == ContestantStatus.Active, "Evicted players can follow the season but cannot influence it.");
            var pacts = s.alliances.Where(a => a.active && a.members.Contains(s.playerId) && a.members.Contains(betrayer.id)).ToList();
            Require(pacts.Count > 0, "No shared alliance is active.");
            Remember(s, betrayer.id, s.playerId, "Cut ties with me.", true);
            foreach (var pact in pacts)
            {
                var audience = new[] { s.playerId }.Concat(pact.members.Where(id => id != s.playerId)).Distinct().ToArray();
                // A pact of two ends; a bigger one goes on without them (shared with C5's leave).
                bool ends = TakeOutOfPact(s, pact, betrayer.id);
                Log(s, "alliance", Allegiance.CutTiesLine(betrayer.name, pact.name, ends), audience);
            }
        }
    }
}
