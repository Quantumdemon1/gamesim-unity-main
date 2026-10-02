using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// The strategy windows in the engine: a plea to whoever is deciding, an answer to a houseguest
    /// who came to the player, and an alliance invitation that makes an alliance. The rules are
    /// <see cref="StrategyRules"/> and <see cref="ReplyCards"/>.
    /// </summary>
    public sealed partial class EpisodeEngine
    {
        /// <summary>
        /// A plea to the Head of Household before nominations, or to the veto holder (or the Head of
        /// Household, about a replacement) before the meeting. A social action: it costs one of the
        /// week's conversations however it is taken.
        /// </summary>
        private static void Lobby(EpisodeState s, EpisodeCommand c)
        {
            Require(StrategyRules.Apply(s), "This season has no strategy windows.");
            Require(s.Find(s.playerId).status == ContestantStatus.Active,
                "Evicted players can follow the season but cannot influence it.");
            Require(LobbyAsk.TryDecode(c.text, out string ask, out string approach), "Choose what to ask and how to ask it.");
            Require(StrategyRules.CanLobby(s, c.targetId, ask, c.secondTargetId, out string refusal), refusal);
            Require(SocialActionsSpent(s) < SocialActionBudget(s), "You have no conversations left this week.");

            var decider = s.Find(c.targetId);
            var read = ask == LobbyAsk.Vote ? LeverRead(s, decider.id) : null;
            double chance = StrategyRules.Chance(s, decider.id, ask, c.secondTargetId, approach);
            var answer = StrategyRules.Respond(chance, Roll(s), () => Roll(s));
            s.lobbies.Add(new LobbyState
            {
                week = s.week, phase = s.phase, deciderId = decider.id, ask = ask, subjectId = c.secondTargetId,
                approach = approach, response = answer.response,
                influence = Math.Max(-StrategyRules.MostInfluence, Math.Min(StrategyRules.MostInfluence, answer.influence)),
            });
            string plea = StrategyRules.Describe(s, ask, c.secondTargetId);
            if (answer.impact != 0)
                Change(s, s.playerId, decider.id, answer.impact, "Lobbying: asked " + plea + " (" + answer.response + ")", "lobby");
            Remember(s, decider.id, s.playerId, "Asked me " + plea + " in week " + s.week + ". I was " + answer.response + ".", true);
            Log(s, "lobby", "You asked " + decider.name + " " + plea + ": “" + StrategyRules.Pitch(s, ask, c.secondTargetId, approach)
                + "” " + decider.name + ": “" + StrategyRules.Answer(s, ask, c.secondTargetId, approach, answer.response) + "”",
                s.playerId, decider.id);
            // A plea made with a deal is a deal: the reference's card promises "real obligations", and
            // one that lands writes them - a safety agreement that binds the player next week too.
            // For the vote it is the voter's word on the vote itself: a vote to keep the player,
            // their obligation in the ballot and judged at the reveal like any vote deal.
            if (ask == LobbyAsk.Vote && approach == LobbyApproach.Deal && StrategyRules.Landed(answer.response)
                && s.deals.Count < NpcDeals.DealCeiling
                && !NpcDeals.Between(s, s.playerId, decider.id).Any(d => d.type == DealKind.VoteSave))
            {
                s.deals.Add(new DealState
                {
                    id = "deal-lobby-" + s.nextSequence, type = DealKind.VoteSave,
                    proposerId = decider.id, recipientId = s.playerId, targetId = s.playerId, status = DealStatus.Active,
                    week = s.week, expiresWeek = s.week, trustImpact = DealKind.DefaultTrust(DealKind.VoteSave),
                });
                Log(s, "deal", decider.name + " has given you their vote: a vote to keep you, judged at the reveal.", s.playerId, decider.id);
            }
            else if (approach == LobbyApproach.Deal && StrategyRules.Landed(answer.response)
                && s.deals.Count < NpcDeals.DealCeiling
                && !NpcDeals.Between(s, s.playerId, decider.id).Any(d => d.type == DealKind.SafetyAgreement))
            {
                s.deals.Add(new DealState
                {
                    id = "deal-lobby-" + s.nextSequence, type = DealKind.SafetyAgreement,
                    proposerId = s.playerId, recipientId = decider.id, status = DealStatus.Active,
                    week = s.week, expiresWeek = s.week + 1, trustImpact = DealKind.DefaultTrust(DealKind.SafetyAgreement),
                });
                Log(s, "deal", "You and " + decider.name + " have a safety agreement through next week.", s.playerId, decider.id);
            }
            if (ask == LobbyAsk.Vote) LeverLine(s, decider.id, read, s.nominees.FirstOrDefault(id => id != s.playerId), "your plea");
            SpendSocialAction(s);
        }

        /// <summary>
        /// Answering a houseguest who came to the player: a confrontation, gossip the player found
        /// out about, or a nominee's plea. Free, as answering an offer is - the move was theirs.
        /// </summary>
        private static void ReplyToHouseguest(EpisodeState s, EpisodeCommand c)
        {
            Require(s.Find(s.playerId).status == ContestantStatus.Active,
                "Evicted players can follow the season but cannot influence it.");
            var card = s.replyCards.FirstOrDefault(r => r.id == c.targetId);
            Require(card != null, "That moment has passed.");
            var from = s.Find(card.fromId);
            Require(from != null && from.status == ContestantStatus.Active, "They are no longer in the house.");
            var reply = ReplyCards.Find(card.kind, (c.text ?? string.Empty).Trim());
            Require(reply != null, "Choose one of the answers you were offered.");

            s.replyCards.Remove(card);
            // Typed, for the story's plays and the verdict (STRATEGY-LOOP-PLAN.md §8): which card,
            // from whom, which answer, and whether it promised a vote.
            SeasonLedger.Append(s.ledger, s.ledger.replies, new ReplyRow
            {
                week = s.week, cardId = card.id, kind = card.kind, fromId = from.id, listenerId = card.aboutId,
                replyKey = reply.Key, toThem = reply.ToThem,
                promised = reply.Promises && s.phase == EpisodePhase.Campaign && Voters(s).Any(v => v.id == s.playerId)
                    && s.nominees.Contains(from.id) && s.nominees.Contains(card.aboutId ?? ""),
            });
            if (reply.ToThem != 0)
                Change(s, s.playerId, from.id, reply.ToThem, ReplyCards.Note(card.kind, reply.Label), "reply");
            // Gossiping back reaches whoever they were talking to, which is the point of it.
            var listener = s.Find(card.aboutId);
            if (reply.ToListener != 0 && listener != null && listener.status == ContestantStatus.Active && !listener.isPlayer)
            {
                RelationshipLedger.Move(s, listener.id, from.id, reply.ToListener);
                RelationshipLedger.Record(s, listener.id, from.id, "rumor", reply.ToListener,
                    "Heard your side of what " + from.name + " said");
            }
            Remember(s, from.id, s.playerId, ReplyCards.Memory(card.kind, reply.Key, s.week), true);
            if (card.kind == ReplyCards.Gossip)
                Remember(s, s.playerId, from.id, from.name + " talked about me to " + (listener?.name ?? "somebody")
                    + " in week " + s.week + ".", true);
            Log(s, "reply", ReplyCards.Outcome(s, card, reply), s.playerId, from.id);
            // Promising support is a promise: kept or broken at the vote, like any other.
            if (reply.Promises && s.phase == EpisodePhase.Campaign && Voters(s).Any(v => v.id == s.playerId)
                && s.nominees.Contains(from.id) && s.nominees.Contains(card.aboutId ?? "")
                && !s.promises.Any(p => p.status == PromiseStatus.Active && p.fromId == s.playerId && p.toId == from.id && p.kind == PromiseKind.Vote))
                MakePromise(s, from.id, PromiseKind.Vote, card.aboutId);
        }

        /// <summary>
        /// An alliance invitation, accepted, becomes an alliance - from the strategy windows. It used
        /// to be a deal and nothing more, so the voting blocs and the jury never saw it.
        ///
        /// <para>The houseguest brings the player into an alliance of their own if it would have them -
        /// nobody in it below the reference's hostility line with the player, and nobody the player
        /// already reads as sour enough to end it - and otherwise the two of them start one.</para>
        /// </summary>
        private static void AllyThroughInvitation(EpisodeState s, string npcId)
        {
            if (!StrategyRules.Apply(s) || s.Allied(s.playerId, npcId)) return;
            var npc = s.Find(npcId);
            if (npc == null || npc.status != ContestantStatus.Active) return;
            var theirs = s.alliances
                .Where(a => a.active && a.members.Contains(npcId) && !a.members.Contains(s.playerId)
                            && a.members.All(member => member == npcId
                                || (s.Score(member, s.playerId) >= StrategyRules.HostilityLine
                                    && s.Score(s.playerId, member) >= NpcAlliances.SourLine)))
                .OrderBy(a => a.id, StringComparer.Ordinal)
                .FirstOrDefault();
            if (theirs != null)
            {
                theirs.members.Add(s.playerId);
                // Joining is learning: the alliance's fact, if it has one, gains the player as a knower.
                if (ReadRulesOn(s)) Knowledge.AddKnower(s, Knowledge.Of(s, FactKinds.Alliance, theirs.id), s.playerId);
                Log(s, "alliance", npc.name + " brought you into " + theirs.name + ".", s.playerId, npcId);
                // Under the commitment rules the player's place in it is on the record with every
                // member, as a pact's founding is (ACTIONS-DEALS-ALLIANCES-PLAN C4).
                RecordPactFormed(s, theirs, theirs.name + " was joined");
                return;
            }
            var pact = new AllianceState
            {
                id = "alliance-" + s.nextSequence, name = "The " + npc.name.Split(' ')[0] + " Pact",
                members = new List<string> { s.playerId, npcId },
            };
            // Under the commitment rules (C5) no two of the player's standing pacts share a name.
            if (CommitmentRulesOn(s)) pact.name = PactNames.Unique(s, pact.name);
            s.alliances.Add(pact);
            AllianceFormedUnderRead(s, pact);
            Log(s, "alliance", "You and " + npc.name + " formed a private alliance.", s.playerId, npcId);
            RecordPactFormed(s, pact, pact.name + " was formed");
        }
    }
}
