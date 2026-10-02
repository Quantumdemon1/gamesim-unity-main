using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// Betrayal as an event (ACTIONS-DEALS-ALLIANCES-PLAN C2, decision 11): an ally who nominates the
    /// player or names them the replacement, votes to evict them, ignores their call, or breaks a deal
    /// with them turns on their pact. What it writes, what it changes and the player's way out; who it
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
        /// or staying allied; one told by a ballot (<paramref name="ballot"/>) goes to the betrayer alone,
        /// since the ballot is not the player's to know, and the notes say it once it is.
        ///
        /// <para>It moves nobody's view: the player's view of them is the player's to change, and a
        /// betrayal that took it under the line a pact sours at would end the pact the player may choose
        /// to keep. Nothing happens once the player is out of the house, or with somebody they share no
        /// standing pact with.</para>
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
            if (ballot) Log(s, Allegiance.LogKind, record, npcId);
            else Log(s, Allegiance.LogKind, record + " " + Allegiance.Offer(npc.name), s.playerId, npcId);
        }

        /// <summary>
        /// The reveal's betrayals: an ally's ballot to evict the player, then a vote deal an ally broke
        /// with them by theirs - told by a ballot, so to the betrayer alone. Nothing when the player is
        /// the one going, since they leave every pact with the house; the Head of Household's deciding
        /// vote against them evicts them, so it is never one of these.
        /// </summary>
        private static void BallotBetrayals(EpisodeState s, string evicted)
        {
            if (!CommitmentRulesOn(s) || evicted == s.playerId) return;
            foreach (var vote in s.votes.Where(v => v.targetId == s.playerId && v.voterId != s.playerId && v.voterId != s.hohId).ToList())
                Betrayal(s, vote.voterId, Allegiance.VotedAgainst, true);
            foreach (var deal in s.deals.Where(d => d.status == DealStatus.Broken && d.settledWeek == s.week && KnownBallots.IsVoteDeal(d.type)
                         && !string.IsNullOrEmpty(d.brokenById) && d.brokenById != s.playerId
                         && DealResolution.Partner(d, d.brokenById) == s.playerId).ToList())
                Betrayal(s, deal.brokenById, Allegiance.BrokeDeal(deal.type), true);
        }

        /// <summary>
        /// Cutting ties with an ally who turned on the pact this week (C2's free exit,
        /// <see cref="Allegiance.FreeExit"/>): every pact the player shares with them ends, as leaving it
        /// would, and it costs nothing - no action, no warmth with them (so no roll), and no grudge from
        /// anyone in it, the betrayer included. The ledger's row says it ended in a betrayal
        /// (<see cref="AllianceEnding"/>). Everybody in the pacts hears it. Leaving outside the week, or
        /// leaving somebody who never turned on it, is the leave it always was.
        /// </summary>
        private static void CutTies(EpisodeState s, ContestantState betrayer)
        {
            Require(s.Find(s.playerId).status == ContestantStatus.Active, "Evicted players can follow the season but cannot influence it.");
            var pacts = s.alliances.Where(a => a.active && a.members.Contains(s.playerId) && a.members.Contains(betrayer.id)).ToList();
            Require(pacts.Count > 0, "No shared alliance is active.");
            foreach (var pact in pacts) pact.active = false;
            Remember(s, betrayer.id, s.playerId, "Cut ties with me.", true);
            var audience = new[] { s.playerId }.Concat(pacts.SelectMany(a => a.members).Where(id => id != s.playerId)).Distinct().ToArray();
            Log(s, "alliance", Allegiance.CutTiesLine(betrayer.name, pacts.Select(a => a.name)), audience);
        }
    }
}
