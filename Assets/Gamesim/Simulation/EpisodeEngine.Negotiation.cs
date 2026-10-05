using System;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// Negotiation (ACTIONS-DEALS-ALLIANCES-PLAN C7, decision 13), the engine's half: the counter a refused
    /// proposal may come back with, and its answer; the price a nominee's veto ask carries, struck with
    /// the player's yes; a price voided when what it bought is broken by the one it was owed to; and the
    /// three situation moves of <see cref="EpisodeCommandKind.Negotiate"/> - calling in a promise, mending
    /// fences after a breach, and a veto for a price. The terms, the odds and the readers are
    /// <see cref="Negotiation"/>'s.
    ///
    /// <para>Everything here runs only under the commitment rules (<see cref="CommitmentRulesOn"/>): a
    /// season without them draws the same rolls, mints the same sequence numbers and logs the same lines.
    /// The counter never draws from the season's stream, under the rules or not: its coin is keyed
    /// (<see cref="StoryRandom"/>), and its yes takes its reciprocal draw from a keyed stream too
    /// (<see cref="ChangeKeyed"/>). The three moves are conversations, committed commands: they spend the
    /// window's action and roll on the season's stream, as a proposal does.</para>
    /// </summary>
    public sealed partial class EpisodeEngine
    {
        /// <summary>
        /// Why the player cannot have a word with this houseguest of this kind right now, or null: free
        /// time and the campaign are open to every word; outside them, a window the week opens is open to
        /// every word said to somebody, and before the week's windows the strategy windows reach only
        /// whoever is deciding. The social actions' own gate, in words a screen can test before it offers
        /// a control; the budget is checked where an action is spent.
        /// </summary>
        public static string ConversationWindowRefusal(EpisodeState s, string targetId, EpisodeCommandKind kind)
        {
            if (s.phase == EpisodePhase.Social || s.phase == EpisodePhase.Campaign) return null;
            // From the strategy windows the Head of Household can be reached before nominations,
            // and the veto holder before the meeting. Everybody else, and everything that is not
            // a word with them, still waits for free time.
            if (WeekRulesOn(s) && Window(s) != Windows.None)
                // The week's windows (STRATEGY-LOOP-PLAN.md section 4): free roam and every word said
                // to somebody, in every window; the strategy windows' own rule still keeps listening
                // in, rumours and scheming for the free time.
                return StrategyRules.IsWindowConversation(kind) ? null : "That can wait for free time. Right now there is a decision to be made.";
            if (!StrategyRules.WindowOpen(s)) return "Social actions are available during free time and campaigning.";
            return StrategyRules.WindowRefusal(s, targetId, kind);
        }

        // ---------------------------------------------------------------- the counter

        /// <summary>
        /// A proposal the houseguest turned down, under the commitment rules: they may come back with the
        /// same deal and a price on it. Only one the roll found close to yes - a chance of
        /// <see cref="Negotiation.CounterFloor"/> or more - and never an alliance a grudge refuses (C4),
        /// on the web's 70% (<see cref="Negotiation.CounterChance"/>), drawn on the attempt's own coin, so
        /// the season's stream is exactly as a plain refusal leaves it. The counter is said after the
        /// refusal's own line, to the two of them, and stands until the player hears anything else.
        /// </summary>
        private static void OfferCounter(EpisodeState s, ContestantState target, string kind, string about, double chance, int attempt)
        {
            if (chance < Negotiation.CounterFloor) return;
            if (kind == DealKind.AllianceInvite && GrudgeRefusesAlliance(s, target.id)) return;
            if (!StoryRandom.Chance(s, Negotiation.CounterKey(s, target.id, kind, about, attempt), Negotiation.CounterChance)) return;
            var counter = Negotiation.CounterTo(s, target.id, kind, about);
            if (counter == null) return;
            Log(s, Negotiation.CounterEventKind, Negotiation.CounterLine(s, counter), s.playerId, target.id);
        }

        /// <summary>
        /// The player's answer to the counter that stands (<see cref="Negotiation.OpenCounter"/>): a
        /// <see cref="EpisodeCommandKind.RespondToDeal"/> naming the houseguest, free, as answering any
        /// offer is. The terms are re-derived from the state, never read from the command. A yes strikes
        /// both at once with no roll - the deal the player asked for (<see cref="Negotiation.CounterDealPrefix"/>)
        /// and the price, linked to each other - takes them as chances taken (C1), and is the yes to an
        /// offer (decision 15): +4, its reciprocal draw from the counter's own keyed stream, and either deal
        /// broken weighs one step heavier (<see cref="DealResolution.AcceptedOffer"/>). Nothing it does
        /// draws from the season's stream: the vote deal's lever line and an invitation's pact read and
        /// write without it. A no is a plain no: the line, and nothing moves. Either way it is the one round.
        /// </summary>
        private static void AnswerCounter(EpisodeState s, EpisodeCommand c)
        {
            var counter = Negotiation.OpenCounter(s, c.targetId);
            Require(counter != null, "That counter is no longer on the table.");
            var npc = s.Find(counter.npcId);
            if (!string.Equals((c.text ?? string.Empty).Trim(), AcceptDeal, StringComparison.OrdinalIgnoreCase))
            {
                Log(s, "deal", Negotiation.CounterDeclinedLine(s, npc.id), s.playerId, npc.id);
                return;
            }
            string key = Negotiation.CounterKey(s, npc.id, counter.kind, counter.aboutId, s.nextSequence) + ":taken";
            var read = LeverRead(s, npc.id);
            string boughtId = Negotiation.CounterDealPrefix + s.nextSequence, priceId = Negotiation.PricePrefix + s.nextSequence;
            var bought = PlayerDeals.Draft(s, npc.id, counter.kind, counter.aboutId, boughtId);
            bought.linkedDealId = priceId;
            var price = Negotiation.DraftPrice(s, counter.price, boughtId, priceId);
            if (UnifiedCommitments.RulesOn(s)
                && (bought.type == DealKind.SafetyAgreement || price.type == DealKind.SafetyAgreement))
                Require(UnifiedCommitmentStore.TryAddLinkedDeals(s, bought, UnifiedCommitments.CounterDeal,
                    price, UnifiedCommitments.CounterPrice, out string error), error);
            else
            {
                s.deals.Add(bought);
                s.deals.Add(price);
            }
            Opportunity(s, bought.id, OpportunityKinds.Deal, bought.week).response = OpportunityResponse.Taken;
            Opportunity(s, price.id, OpportunityKinds.Deal, price.week).response = OpportunityResponse.Taken;
            string title = DealKind.Title(counter.kind).ToLowerInvariant();
            ChangeKeyed(s, s.playerId, npc.id, PlayerDeals.CommittedAcceptedImpact, key, "Took my counter on a " + title + ".", "deal_accepted");
            Remember(s, npc.id, s.playerId, "Agreed a " + title + " with me, at my price.", true);
            Log(s, "deal", Negotiation.CounterTakenLine(s, counter), s.playerId, npc.id);
            if (counter.kind == DealKind.AllianceInvite) AllyThroughInvitation(s, npc.id);
            if (counter.kind == DealKind.VoteEvict || counter.kind == DealKind.VoteSave)
                LeverLine(s, npc.id, read, WantsOut(s, counter.kind, counter.aboutId), "your deal");
        }

        // ---------------------------------------------------------------- a price

        /// <summary>
        /// The price a nominee's veto ask carries, struck with the player's yes (C7): what they offered for
        /// the player's word (<see cref="Negotiation.AskPrice"/>), a real deal from them to the player,
        /// standing at once, the ask and the price naming each other. Owed while the veto commitment
        /// stands; void if the player breaks it (<see cref="VoidThePrice"/>). A chance taken (C1), so a
        /// price voided is never an offer left on the table; and part of the bargain the player's yes
        /// accepted, so its payer breaking it weighs one step heavier, as the ask itself does (decision
        /// 15, <see cref="DealResolution.AcceptedOffer"/>). Nothing for an ask that carries no price.
        /// </summary>
        private static void StrikeTheAskPrice(EpisodeState s, DealState ask)
        {
            var price = Negotiation.AskPrice(s, ask);
            if (price == null) return;
            string id = Negotiation.PricePrefix + s.nextSequence;
            var deal = Negotiation.DraftPrice(s, price, ask.id, id);
            ask.linkedDealId = id;
            s.deals.Add(deal);
            Opportunity(s, deal.id, OpportunityKinds.Deal, deal.week).response = OpportunityResponse.Taken;
            Remember(s, price.payerId, s.playerId, "I owe them " + (price.kind == DealKind.FinalTwo ? "a final two" : "my vote to keep them")
                + " for their word on the veto, from week " + s.week + ".", true);
            Log(s, "deal", Negotiation.AskPriceStruckLine(s, price), s.playerId, price.payerId);
        }

        /// <summary>
        /// A deal broken, under the commitment rules: the price paid for it is void when the one it was
        /// owed to broke it (C7) - the veto the player did not use on the nominee who paid for it, the
        /// pact a houseguest broke after naming their price. The price lapses - nobody broke it, so it
        /// writes nothing to anybody's record and draws nothing - and the two of them are told. A verdict
        /// on the price in the same act is passed over (<see cref="SettleDeals"/>), so it is not broken
        /// as well. Broken by the one who pays the price, it stands: they still owe it. A ballot's breach
        /// voids nothing: the price's line would tell the ballot (decision 4). Nor does a breach after the
        /// price's own week: it had run out by then, though its lapse is not written until the house next
        /// expires its deals, so it is judged by its own rule - as <see cref="Negotiation.Voided"/> reads
        /// the weeks, and the Your word page with it.
        /// </summary>
        private static void VoidThePrice(EpisodeState s, DealState bought, string breakerId)
        {
            var price = Negotiation.PriceOf(s, bought);
            if (price == null || breakerId == null || !DealStatus.Binds(price.status) || price.recipientId != breakerId) return;
            if (KnownBallots.SettledByABallot(bought)) return;
            if (price.expiresWeek != 0 && s.week > price.expiresWeek) return;
            if (UnifiedCommitments.RulesOn(s))
            {
                if (CommitmentReferences.FindCanonical(s, price.id) != null)
                {
                    if (!UnifiedCommitmentStore.TryExpireLinkedPrice(s, bought.id, breakerId, out _)) return;
                }
                else
                {
                    // References are deliberately detached under the new authority. The non-safety
                    // consideration still belongs to its original legacy store.
                    var owned = s.deals.FirstOrDefault(d => d.id == price.id);
                    if (owned == null || !DealStatus.Binds(owned.status)) return;
                    owned.status = DealStatus.Expired;
                }
            }
            else price.status = DealStatus.Expired;
            Log(s, "deal-outcome", Negotiation.VoidedLine(s, price, bought), price.proposerId, price.recipientId);
        }

        // ---------------------------------------------------------------- the situation moves

        /// <summary>
        /// One of the web's situation moves, said to <paramref name="target"/> (C7): calling in a promise
        /// they owe the player, mending fences after a breach of the player's, or naming a price for the
        /// veto. A social action, under the commitment rules only; the move is the command's text.
        /// </summary>
        private static void Negotiate(EpisodeState s, ContestantState target, EpisodeCommand c)
        {
            Require(CommitmentRulesOn(s), Negotiation.NotThisSeason);
            string move = (c.text ?? string.Empty).Trim();
            if (move.StartsWith(Negotiation.CallInPrefix, StringComparison.Ordinal))
                CallInAPromise(s, target, c.secondTargetId, move.Substring(Negotiation.CallInPrefix.Length));
            else if (move == Negotiation.MendFences) MendFences(s, target);
            else if (move.StartsWith(Negotiation.VetoPricePrefix, StringComparison.Ordinal))
                VetoForAPrice(s, target, move.Substring(Negotiation.VetoPricePrefix.Length));
            else throw new RuleException("That is not a move anybody in this house would recognise.");
        }

        /// <summary>
        /// Calling in a promise the houseguest made the player: reminding them, demanding they keep it, or
        /// threatening to tell the house if they break it - the web's three, at its odds (60, 50, 40 and
        /// the rest of <see cref="Negotiation.Chance"/>) and its costs (nothing, -5, -15 in their view of
        /// the player, whatever they answer). One roll on the season's stream. Where it lands the maker is
        /// held to it - a reminder once, a demand half again, a threat twice (<see cref="Negotiation.Hold"/>)
        /// - and the promise weighs that much in the decision that settles it: the nominations for safety
        /// (<see cref="Negotiation.SafetyHeld"/>), the final choice for a final two
        /// (<see cref="FinalTwoTerms"/>). Either way it is on their record of the player, and called in for
        /// the week.
        /// </summary>
        private static void CallInAPromise(EpisodeState s, ContestantState target, string promiseId, string approach)
        {
            string refusal = Negotiation.CallInRefusal(s, target.id, promiseId, approach);
            Require(refusal == null, refusal);
            var promise = UnifiedCommitments.RulesOn(s) ? CommitmentReferences.FindPromise(s, promiseId)
                : s.promises.First(p => p.id == promiseId);
            bool landed = Roll(s) * 100 < Negotiation.Chance(s, target.id, approach, false);
            double cost = Negotiation.Cost(approach);
            string line = Negotiation.CallInLine(s, promise, approach, landed);
            if (cost != 0)
            {
                WriteScore(s, target.id, s.playerId, cost);
                Arc(s, target.id, cost, line);
            }
            Touch(s, target.id);
            RelationshipLedger.RecordOneWay(s, target.id, s.playerId,
                landed ? Negotiation.HeldType(promise.kind, approach) : Negotiation.PressedType(promise.kind, approach), cost, line);
            Remember(s, target.id, s.playerId, (landed ? "Held me to my promise of " : "Pressed me on my promise of ")
                + Negotiation.PromiseWords(promise.kind) + " in week " + s.week + ".", true);
            Log(s, "call-in", line, s.playerId, target.id);
        }

        /// <summary>
        /// Mending fences with a houseguest the player broke their word to: once for each breach of the
        /// player's against them that they can know of (<see cref="Negotiation.MendRefusal"/>; a voting bloc
        /// only once they know how the other voted), at the web's odds (55, less 30 for the broken deal
        /// between them, and the rest of <see cref="Negotiation.Chance"/>). One roll on the season's
        /// stream. Where it lands their view of the player rises by the web's +10 and any
        /// grudge they hold eases as much; where it does not, they think 5 less of the player. One way,
        /// as the breach is held one way (C0), and permanent on their record, as the breach is. The breach
        /// itself is untouched - the deal still broken, by the player, its entry permanent - so it still
        /// counts against the player everywhere it did: breaches never fade.
        /// </summary>
        private static void MendFences(EpisodeState s, ContestantState target)
        {
            string refusal = Negotiation.MendRefusal(s, target.id);
            Require(refusal == null, refusal);
            bool landed = Roll(s) * 100 < Negotiation.Chance(s, target.id, Negotiation.MendFences, false);
            string line = Negotiation.MendLine(s, target.id, landed);
            double delta = landed ? Negotiation.MendRepair : Negotiation.MendRebuff;
            WriteScore(s, target.id, s.playerId, delta);
            Arc(s, target.id, delta, line);
            Touch(s, target.id);
            RelationshipLedger.RecordOneWay(s, target.id, s.playerId, landed ? Negotiation.AmendsType : Negotiation.RebuffType, delta, line, permanent: true);
            if (landed && StoryAt(s, StoryRules.Grudges)) Grudges.Ease(s, target.id, s.playerId, Negotiation.MendRepair);
            Remember(s, target.id, s.playerId, (landed ? "Made amends with me in week " : "Tried to make amends with me in week ") + s.week + ".", true);
            Log(s, "amends", line, s.playerId, target.id);
        }

        /// <summary>
        /// A veto for a price, the web's own (the veto holder's "Leverage Veto", Demand a Deal): the player,
        /// holding the veto before the meeting, names their price to a nominee - their vote to keep the
        /// player the next time the player is on the block, or at the endgame a final two - on the web's
        /// odds (75 and the rest of <see cref="Negotiation.Chance"/>). Three draws on the season's stream:
        /// the answer's roll, then the two reciprocal draws of the warmth it moves (<see cref="Change"/>).
        /// Taken: the player's word that they will use the veto on the nominee, and the price the nominee
        /// owes for it, struck at once and linked (the same pair a nominee's own ask makes), the nominee's
        /// own ask answered by it - a chance the player took, so it is never an offer left on the table -
        /// and the web's +10. Refused: the web's -5, and nothing struck.
        /// </summary>
        private static void VetoForAPrice(EpisodeState s, ContestantState nominee, string kind)
        {
            string refusal = Negotiation.VetoPriceRefusal(s, nominee.id, kind);
            Require(refusal == null, refusal);
            var price = Negotiation.VetoPrice(s, nominee.id, kind);
            bool taken = Roll(s) * 100 < Negotiation.Chance(s, nominee.id, Negotiation.VetoForAPrice, false);
            if (!taken)
            {
                Change(s, s.playerId, nominee.id, Negotiation.VetoPriceRefused, "Wouldn't pay your price for the veto.", "deal_refused");
                Log(s, "deal", Negotiation.VetoPriceLine(s, nominee.id, price, false), s.playerId, nominee.id);
                return;
            }
            string vetoId = "deal-player-" + s.nextSequence, priceId = Negotiation.PricePrefix + s.nextSequence;
            var veto = PlayerDeals.Draft(s, nominee.id, DealKind.VetoUse, null, vetoId);
            veto.linkedDealId = priceId;
            s.deals.Add(veto);
            s.deals.Add(Negotiation.DraftPrice(s, price, vetoId, priceId));
            Opportunity(s, vetoId, OpportunityKinds.Deal, s.week).response = OpportunityResponse.Taken;
            Opportunity(s, priceId, OpportunityKinds.Deal, s.week).response = OpportunityResponse.Taken;
            // Their own question about the veto is answered: the player has given them their word, so the
            // ask is on the record as a chance taken, never an offer left on the table (Game Sense).
            foreach (var ask in s.deals.Where(d => d.status == DealStatus.Proposed && d.type == DealKind.VetoUse
                         && d.proposerId == nominee.id && d.recipientId == s.playerId))
            {
                ask.status = DealStatus.Expired;
                Opportunity(s, ask.id, OpportunityKinds.Deal, ask.week).response = OpportunityResponse.Taken;
            }
            Change(s, s.playerId, nominee.id, Negotiation.VetoPriceTaken, "Took your price for the veto.", "deal_accepted");
            Remember(s, nominee.id, s.playerId, "Promised to use the veto on me, at a price, in week " + s.week + ".", true);
            Log(s, "deal", Negotiation.VetoPriceLine(s, nominee.id, price, true), s.playerId, nominee.id);
        }

        /// <summary>The two of them were in touch this week: both sides of their record say so, as any conversation's does.</summary>
        private static void Touch(EpisodeState s, string npcId)
        {
            foreach (var relation in s.relationships.Where(r => (r.fromId == s.playerId && r.toId == npcId) || (r.fromId == npcId && r.toId == s.playerId)))
                relation.lastInteractionWeek = s.week;
        }
    }
}
