using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// Negotiation (ACTIONS-DEALS-ALLIANCES-PLAN C7, decision 13), the pure half: one counter-offer round,
    /// NPC asks that carry a price, and three situation moves from the web build - calling in a promise,
    /// mending fences after a breach, and a veto for a price. What a counter's terms are, what a price is
    /// and how it is linked to what it buys, the odds a move is shown with, and the readers of what a
    /// negotiation left on the record. The engine's half writes them (<c>EpisodeEngine.Negotiation.cs</c>).
    ///
    /// <para><b>The counter.</b> A proposal the houseguest turns down may come back, under the commitment
    /// rules, as the same deal with a price on it: "the safety pact after all, if you add your vote to
    /// keep me this week". Whether it comes is the web's 70% (<see cref="CounterChance"/>), for a
    /// houseguest the roll found close to yes - a chance of 40 or more, the web's line for an
    /// alternative worth offering (<see cref="CounterFloor"/>) - drawn on a coin keyed to the attempt
    /// (<see cref="CounterKey"/>), never from the season's stream. Its terms are a pure function of the
    /// state and the refused proposal (<see cref="CounterTo"/>), so the screen shows them and the yes
    /// re-derives them. Nothing waits for an answer in any store: the counter stands while nothing the
    /// player hears has been said since its line (<see cref="OpenCounter"/>) and lapses with the next
    /// thing they do hear. One round: the yes strikes both deals with no roll at all, so nothing can be
    /// refused or countered again; a no is a plain no. Neither is a second roll of the proposal: the yes
    /// costs its price, and it is the yes to an offer, so a breach of either deal weighs one step heavier
    /// (decision 15, <see cref="FromACounter"/>).</para>
    ///
    /// <para><b>A price.</b> A deal paid for another names it, and is named by it, through
    /// <see cref="DealState.linkedDealId"/>; the price's id says it is one (<see cref="PricePrefix"/>),
    /// its proposer pays it and its recipient is owed it. A price is owed while what it bought stands:
    /// broken by the one it was owed to - the veto they did not use, the pact they broke - in front of
    /// the house, it is void (the engine marks it expired, which breaks nothing, as the end of a deal's
    /// week breaks nothing); kept, it stands, and is judged by its own rule. A ballot's breach voids
    /// nothing, because the price's line would tell the ballot. The price an offer to the player carried
    /// is part of the bargain their yes accepted, as a counter's is, so its breach weighs one step
    /// heavier (decision 15, <c>DealResolution.AcceptedOffer</c>); the price the player names for the
    /// veto is their own ask, and weighs its own.</para>
    ///
    /// <para>Pure and read-only: it neither mutates the state nor draws from its generator.</para>
    /// </summary>
    public static class Negotiation
    {
        // ---------------------------------------------------------------- the counter

        /// <summary>How often a houseguest who was close to yes comes back with a price: the web's 70% (<c>generateCounterOffer</c>).</summary>
        public const double CounterChance = 0.7;

        /// <summary>The chance, in percent, a refused proposal must have had for a counter to be worth making: the web's 40.</summary>
        public const double CounterFloor = 40;

        /// <summary>The kind of the line a counter is said in. It stands while nothing the player hears has been said since.</summary>
        public const string CounterEventKind = "deal-counter";

        /// <summary>
        /// How the id of the deal a taken counter strikes begins: the deal the player asked for, struck at
        /// the houseguest's price. Its price is a <see cref="PricePrefix"/> deal linked to it.
        /// </summary>
        public const string CounterDealPrefix = "deal-counter-";

        /// <summary>A counter: the deal the player asked for, as they asked it, and the price the houseguest puts on it.</summary>
        public sealed class Counter
        {
            public string npcId, kind, aboutId;
            public Price price;
        }

        /// <summary>
        /// The key of the coin a refused proposal's counter is drawn on: the week, the player who proposed,
        /// the houseguest, the kind, who it is about and the attempt's sequence - the sequence the season
        /// had reached when the proposal was made, so every attempt has a coin of its own, and the season's
        /// stream (<see cref="EpisodeState.randomState"/>) is never touched.
        /// </summary>
        public static string CounterKey(EpisodeState s, string npcId, string kind, string aboutId, int attempt) =>
            "w" + s.week + ":counter:" + s.playerId + ":" + npcId + ":" + kind + ":" + (aboutId ?? "-") + ":" + attempt;

        /// <summary>
        /// The counter this houseguest would make to a refused proposal, or null - whether it comes is the
        /// engine's coin. The deal itself, as the player put it, while it is still one they could put
        /// (<see cref="PlayerDeals.CanPropose"/>) and the season has room for two more; and the price
        /// (<see cref="CounterPrice"/>). Pure: the screen shows these terms and the yes re-derives them.
        /// </summary>
        public static Counter CounterTo(EpisodeState s, string npcId, string kind, string aboutId)
        {
            if (!EpisodeEngine.CommitmentRulesOn(s) || !DealKind.IsKnown(kind)) return null;
            var npc = s.Find(npcId);
            if (npc == null || npc.isPlayer || npc.status != ContestantStatus.Active || s.Find(s.playerId)?.status != ContestantStatus.Active) return null;
            if (s.deals.Count + 2 > PlayerDeals.PlayerDealCeiling) return null;
            string about = DealKind.NamesATarget(kind) ? aboutId : null;
            if (!PlayerDeals.CanPropose(s, npcId, kind, about, out _)) return null;
            var price = CounterPrice(s, npcId, kind, about);
            return price == null ? null : new Counter { npcId = npcId, kind = kind, aboutId = about, price = price };
        }

        /// <summary>
        /// What a houseguest wants for a deal they turned down - the first that fits, never the kind asked
        /// for, never one already binding the two of them, always owed by the player:
        /// <list type="number">
        /// <item>on the block, from a player who votes this week: the player's vote to keep them, this week
        /// (a vote save naming them, where the levers judge vote deals);</item>
        /// <item>at the endgame - six or fewer in the house (<see cref="NpcDeals.EndgameSize"/>): a final two,
        /// except for a final three deal (C9), which a final two would outbind;</item>
        /// <item>a safety pact - the first of the web's counter-proposals (<c>veto-lobbying-system.ts</c>);</item>
        /// <item>a voting bloc this week, where both of them vote - the second.</item>
        /// </list>
        /// The web's third, a target of their choosing, is left out: who they want gone is theirs to know.
        /// </summary>
        public static Price CounterPrice(EpisodeState s, string npcId, string kind, string aboutId)
        {
            string me = s.playerId;
            bool vote = !s.evictionResolved && s.nominees.Count > 0;
            bool iVote = vote && EpisodeEngine.Voters(s).Any(v => v.id == me);
            if (kind != DealKind.VoteSave && aboutId != npcId && EpisodeEngine.LeverRulesOn(s) && iVote && s.nominees.Contains(npcId)
                && !Binding(s, me, npcId, DealKind.VoteSave, npcId))
                return new Price { kind = DealKind.VoteSave, payerId = me, payeeId = npcId, aboutId = npcId, expiresWeek = s.week };
            // Never a final two for a final three deal (C9): the price would bind more than the deal it bought.
            if (kind != DealKind.FinalTwo && kind != DealKind.FinalThree && s.Active.Count() <= NpcDeals.EndgameSize && !Binding(s, me, npcId, DealKind.FinalTwo, null))
                return new Price { kind = DealKind.FinalTwo, payerId = me, payeeId = npcId, expiresWeek = 0 };
            if (kind != DealKind.SafetyAgreement && !Binding(s, me, npcId, DealKind.SafetyAgreement, null))
                return new Price { kind = DealKind.SafetyAgreement, payerId = me, payeeId = npcId, expiresWeek = s.week };
            if (kind != DealKind.VoteTogether && iVote && EpisodeEngine.Voters(s).Any(v => v.id == npcId)
                && !Binding(s, me, npcId, DealKind.VoteTogether, null))
                return new Price { kind = DealKind.VoteTogether, payerId = me, payeeId = npcId, expiresWeek = s.week };
            return null;
        }

        /// <summary>
        /// The counter that stands with this houseguest right now, or null: under the commitment rules,
        /// the latest counter line naming the two of them, said this week and in this phase, with nothing
        /// the player hears said since - no later line in their audience, and no public one - and its
        /// terms still what the state gives (<see cref="CounterTo"/>). A line between houseguests only, a
        /// ledger row or a story's id minted after it leaves it standing: the player heard nothing. Any
        /// line the player hears lapses it - their next word, the answer to it, another counter. The line
        /// is matched by rebuilding it (<see cref="CounterLine"/>) for each proposal it could answer, as
        /// the readers of a deal's record rebuild the engine's sentence.
        /// </summary>
        public static Counter OpenCounter(EpisodeState s, string npcId)
        {
            if (!EpisodeEngine.CommitmentRulesOn(s) || string.IsNullOrEmpty(npcId) || s.events == null) return null;
            EpisodeEvent line = null;
            for (int i = s.events.Count - 1; i >= 0 && line == null; i--)
            {
                var said = s.events[i];
                if (said == null) continue;
                bool heard = said.audienceIds == null || said.audienceIds.Count == 0 || said.audienceIds.Contains(s.playerId);
                if (said.kind == CounterEventKind && heard && said.audienceIds != null && said.audienceIds.Contains(npcId)) line = said;
                else if (heard) return null;
            }
            if (line == null || line.week != s.week || line.phase != s.phase) return null;
            var npc = s.Find(npcId);
            if (npc == null || line.text == null || !line.text.StartsWith(npc.name + " ", StringComparison.Ordinal)) return null;
            foreach (string kind in DealKind.All)
                foreach (string about in Abouts(s, kind, npcId))
                {
                    var counter = CounterTo(s, npcId, kind, about);
                    if (counter != null && CounterLine(s, counter) == line.text) return counter;
                }
            return null;
        }

        /// <summary>Everybody a proposal of this kind could have been about: nobody, or anybody else in the house.</summary>
        private static IEnumerable<string> Abouts(EpisodeState s, string kind, string npcId)
        {
            yield return null;
            if (!DealKind.NamesATarget(kind)) yield break;
            foreach (var actor in s.Active)
                if (actor.id != npcId) yield return actor.id;
        }

        /// <summary>The counter, as the houseguest says it: "Maya Hassan would agree to the safety pact after all, if you add your vote to keep Maya this week."</summary>
        public static string CounterLine(EpisodeState s, Counter counter) =>
            Name(s, counter.npcId) + " would agree to the " + DealWords(s, counter.kind, counter.aboutId)
            + " after all, if you add " + PriceWords(s, counter.price) + ".";

        /// <summary>The counter taken: "You took Maya Hassan's counter: the safety pact, and your vote to keep Maya this week."</summary>
        public static string CounterTakenLine(EpisodeState s, Counter counter) =>
            "You took " + Name(s, counter.npcId) + "'s counter: the " + DealWords(s, counter.kind, counter.aboutId)
            + ", and " + PriceWords(s, counter.price) + ".";

        /// <summary>The counter turned down, a plain no: "You turned down Maya Hassan's counter."</summary>
        public static string CounterDeclinedLine(EpisodeState s, string npcId) => "You turned down " + Name(s, npcId) + "'s counter.";

        /// <summary>What a counter binds, for the screen: both deals, in the words the Your word page gives them.</summary>
        public static string CounterTermsLine(EpisodeState s, Counter counter) =>
            "Taking it strikes both at once, with no roll: the " + DealWords(s, counter.kind, counter.aboutId)
            + " (" + StakesWord(counter.kind) + "), and " + PriceWords(s, counter.price) + " (" + StakesWord(counter.price.kind)
            + "). Turning it down is a plain no, and anything else you do lets it lapse.";

        // ---------------------------------------------------------------- a price

        /// <summary>How the id of a price begins: the deal one side pays for another, the two linked through <see cref="DealState.linkedDealId"/>.</summary>
        public const string PricePrefix = "deal-price-";

        /// <summary>A price: who pays it, who is owed it, what it is, who it is about, and the week it stops binding (0, open-ended).</summary>
        public sealed class Price
        {
            public string kind, payerId, payeeId, aboutId;
            public int expiresWeek;
        }

        /// <summary>Whether this deal is a price paid for another.</summary>
        public static bool IsPrice(DealState d) => d?.id != null && d.id.StartsWith(PricePrefix, StringComparison.Ordinal);

        /// <summary>The deal linked to this one, or null.</summary>
        public static DealState Linked(EpisodeState s, DealState d) =>
            s?.deals == null || d == null || string.IsNullOrEmpty(d.linkedDealId) ? null : s.deals.FirstOrDefault(x => x.id == d.linkedDealId);

        /// <summary>The price paid for this deal, or null: the deal linked to it, when that is the price.</summary>
        public static DealState PriceOf(EpisodeState s, DealState bought)
        {
            if (IsPrice(bought)) return null;
            var linked = Linked(s, bought);
            return IsPrice(linked) ? linked : null;
        }

        /// <summary>What a price was paid for, or null.</summary>
        public static DealState BoughtWith(EpisodeState s, DealState price) => IsPrice(price) ? Linked(s, price) : null;

        /// <summary>
        /// Whether a price was voided: it stopped binding because the one it was owed to broke what it
        /// bought, in front of the house. Read from the two deals as the engine left them: the price
        /// lapsed, what it bought broken by the price's payee, not by a ballot - and broken while the
        /// price still bound, so a price that had already run out its own week before then lapsed as any
        /// deal does, and is not called void.
        /// </summary>
        public static bool Voided(EpisodeState s, DealState price)
        {
            var bought = BoughtWith(s, price);
            return bought != null && price.status == DealStatus.Expired && bought.status == DealStatus.Broken
                   && !KnownBallots.SettledByABallot(bought) && Breaches.DealBreaker(s, bought) == price.recipientId
                   && (price.expiresWeek == 0 || bought.settledWeek <= price.expiresWeek);
        }

        /// <summary>
        /// Whether this deal came of a counter the player took: the deal they asked for, struck at the
        /// houseguest's price (<see cref="CounterDealPrefix"/>), or that price. The yes was the player's
        /// answer to the houseguest's offer, so a breach of either weighs one step heavier, as any offer
        /// the player accepted does (decision 15, <see cref="DealResolution.AcceptedOffer"/>).
        /// </summary>
        public static bool FromACounter(EpisodeState s, DealState deal)
        {
            if (deal?.id == null) return false;
            if (deal.id.StartsWith(CounterDealPrefix, StringComparison.Ordinal)) return true;
            var bought = BoughtWith(s, deal);
            return bought?.id != null && bought.id.StartsWith(CounterDealPrefix, StringComparison.Ordinal);
        }

        /// <summary>
        /// The price a nominee puts on the veto, under the commitment rules (C7): what they offer the player
        /// for their word that they will use it on them. Null for anything but a nominee's veto ask
        /// (<see cref="NpcDeals.VetoAskPrefix"/>) put to the player, without the rules, where no price fits
        /// (<see cref="VetoPrice"/>), or once the season holds as many deals as it may
        /// (<see cref="NpcDeals.DealCeiling"/>, validation's own bound): the ask is then put as it always
        /// was, and the yes is never refused for a price there is no room to strike.
        /// </summary>
        public static Price AskPrice(EpisodeState s, DealState ask)
        {
            if (!EpisodeEngine.CommitmentRulesOn(s) || ask == null || ask.type != DealKind.VetoUse || ask.recipientId != s.playerId
                || ask.id == null || !ask.id.StartsWith(NpcDeals.VetoAskPrefix, StringComparison.Ordinal)) return null;
            if (s.deals.Count >= NpcDeals.DealCeiling) return null;
            return VetoPrice(s, ask.proposerId, null);
        }

        /// <summary>
        /// The price a nominee pays for the veto: <paramref name="kind"/> where it fits, or with no kind
        /// named the first that fits of a final two - at the endgame, where it stops being presumptuous
        /// (<see cref="NpcDeals.EndgameSize"/>) - and their vote to keep the player, the next time the
        /// player is on the block: a vote save naming the player, open until a vote tests it, where the
        /// levers judge vote deals. Never one they already owe the player.
        /// </summary>
        public static Price VetoPrice(EpisodeState s, string nomineeId, string kind)
        {
            foreach (string candidate in kind != null ? new[] { kind } : new[] { DealKind.FinalTwo, DealKind.VoteSave })
                if (VetoPriceFits(s, nomineeId, candidate))
                    return new Price
                    {
                        kind = candidate, payerId = nomineeId, payeeId = s.playerId,
                        aboutId = candidate == DealKind.VoteSave ? s.playerId : null, expiresWeek = 0,
                    };
            return null;
        }

        /// <summary>Whether this nominee can pay this price for the veto: a final two at the endgame, a vote to keep the player where the levers run, neither held twice.</summary>
        public static bool VetoPriceFits(EpisodeState s, string nomineeId, string kind)
        {
            var nominee = s?.Find(nomineeId);
            if (nominee == null || nominee.isPlayer) return false;
            switch (kind)
            {
                case DealKind.FinalTwo:
                    return s.Active.Count() <= NpcDeals.EndgameSize && !Binding(s, nomineeId, s.playerId, DealKind.FinalTwo, null);
                case DealKind.VoteSave:
                    return EpisodeEngine.LeverRulesOn(s) && !Binding(s, nomineeId, s.playerId, DealKind.VoteSave, s.playerId);
                default:
                    return false;
            }
        }

        /// <summary>The price as a deal: proposed by the one who pays it, to the one owed it, standing at once, and linked to what it buys.</summary>
        public static DealState DraftPrice(EpisodeState s, Price price, string boughtId, string id) =>
            new DealState
            {
                id = id, type = price.kind, proposerId = price.payerId, recipientId = price.payeeId,
                targetId = DealKind.NamesATarget(price.kind) ? price.aboutId : null,
                status = DealStatus.Active, week = s.week, expiresWeek = price.expiresWeek,
                trustImpact = DealKind.DefaultTrust(price.kind), linkedDealId = boughtId,
            };

        /// <summary>
        /// What a price is, as the one owed it hears it: from the player, "your vote to keep Maya this
        /// week", "a final two", "a safety pact", "a voting bloc this week"; from a houseguest, "their vote
        /// to keep you, the next time you are on the block", "a final two".
        /// </summary>
        public static string PriceWords(EpisodeState s, Price price)
        {
            if (price == null) return "";
            bool mine = price.payerId == s.playerId;
            switch (price.kind)
            {
                case DealKind.VoteSave:
                    return mine ? "your vote to keep " + First(s, price.aboutId) + " this week"
                        : "their vote to keep you, the next time you are on the block";
                case DealKind.FinalTwo: return "a final two";
                case DealKind.SafetyAgreement: return "a safety pact";
                case DealKind.VoteTogether: return "a voting bloc this week";
                default: return Article(DealKind.Title(price.kind).ToLowerInvariant());
            }
        }

        /// <summary>A nominee's veto ask's price, said under the ask: "In return, Maya Hassan offers you a final two." Null where it carries none.</summary>
        public static string AskPriceLine(EpisodeState s, DealState ask)
        {
            var price = AskPrice(s, ask);
            return price == null ? null : "In return, " + Name(s, ask.proposerId) + " offers you " + PriceWords(s, price) + ".";
        }

        /// <summary>The price struck with the player's yes to a veto ask: "For your word on the veto, Maya Hassan owes you a final two."</summary>
        public static string AskPriceStruckLine(EpisodeState s, Price price) =>
            "For your word on the veto, " + Name(s, price.payerId) + " owes you " + PriceWords(s, price) + ".";

        /// <summary>
        /// A price voided: "Maya Hassan no longer owes you a final two: you did not keep the veto commitment it
        /// paid for." Never "broke": the weekly recap counts a deal's outcome line that says so as a breach
        /// that reached the player, and the breach it means has a line of its own.
        /// </summary>
        public static string VoidedLine(EpisodeState s, DealState price, DealState bought)
        {
            var words = PriceWords(s, new Price { kind = price.type, payerId = price.proposerId, payeeId = price.recipientId, aboutId = price.targetId });
            string owes = price.proposerId == s.playerId ? "You no longer owe " : Name(s, price.proposerId) + " no longer owes ";
            string payee = price.recipientId == s.playerId ? "you" : Name(s, price.recipientId);
            return owes + payee + " " + words + ": " + payee + " did not keep the " + DealKind.Title(bought.type).ToLowerInvariant() + " it paid for.";
        }

        // ---------------------------------------------------------------- the situation moves

        /// <summary>The three ways to call in a promise: the web's Gentle Reminder, Demand Fulfillment and Threaten Exposure.</summary>
        public const string Remind = "remind", Demand = "demand", Threaten = "threaten";
        public static readonly string[] Approaches = { Remind, Demand, Threaten };

        /// <summary>
        /// The words a <see cref="EpisodeCommandKind.Negotiate"/> command carries: calling in a promise
        /// (<see cref="CallIn"/>), mending fences, and a veto for a price (<see cref="VetoPriceMove"/>).
        /// <see cref="MendFences"/> and <see cref="VetoForAPrice"/> also name the two moves for their odds.
        /// </summary>
        public const string CallInPrefix = "call-in:", MendFences = "mend-fences", VetoForAPrice = "veto-price", VetoPricePrefix = VetoForAPrice + ":";

        /// <summary>The command's words for calling in a promise this way.</summary>
        public static string CallIn(string approach) => CallInPrefix + approach;

        /// <summary>The command's words for naming this price for the veto.</summary>
        public static string VetoPriceMove(string kind) => VetoPricePrefix + kind;

        /// <summary>
        /// The web's base chance for each move (<c>contextual-action-generator.ts</c>): Gentle Reminder 60,
        /// Demand Fulfillment 50, Threaten Exposure 40, Mend Fences 55, and the veto holder's Demand a Deal 75.
        /// </summary>
        public static double Base(string move)
        {
            switch (move)
            {
                case Remind: return 60;
                case Demand: return 50;
                case Threaten: return 40;
                case MendFences: return 55;
                case VetoForAPrice: return 75;
                default: return 0;
            }
        }

        /// <summary>The player trait each move takes the web's +15 for: Social, Competitive, Manipulative, Social, Strategic.</summary>
        public static string TraitBonus(string move)
        {
            switch (move)
            {
                case Remind: return "Social";
                case Demand: return "Competitive";
                case Threaten: return "Manipulative";
                case MendFences: return "Social";
                case VetoForAPrice: return "Strategic";
                default: return null;
            }
        }

        /// <summary>
        /// What calling in a promise costs in the houseguest's view of the player, whatever they answer:
        /// the web's previews - a reminder preserves the relationship, a demand is -5, a threat -15.
        /// </summary>
        public static double Cost(string approach) => approach == Demand ? -5 : approach == Threaten ? -15 : 0;

        /// <summary>
        /// How hard an approach that lands holds the houseguest to their word, as the weight the promise
        /// then carries in the decision that settles it: a reminder once, a demand half again, a threat
        /// twice. Its odds are lower and its cost higher, so each is a choice.
        /// </summary>
        public static double Hold(string approach) => approach == Remind ? 1 : approach == Demand ? 1.5 : approach == Threaten ? 2 : 0;

        /// <summary>
        /// What mending fences repairs, and what failing to costs: the web's +10 if accepted, and -5 for a
        /// low-risk move that falls flat - in the view of the one the player wronged, one way, as the
        /// breach is held one way (C0). The breach itself stays on the record: the deal broken, by whom,
        /// and the permanent entry - breaches never fade.
        /// </summary>
        public const double MendRepair = 10, MendRebuff = -5;

        /// <summary>
        /// What a veto for a price moves when the nominee takes the price, and when they will not: the web's
        /// +10 for leverage that works and -5 for an attempt that falls flat (<c>use-strategic-action-listener.ts</c>).
        /// </summary>
        public const double VetoPriceTaken = 10, VetoPriceRefused = -5;

        /// <summary>
        /// The chance, in percent, a houseguest goes along with a move: the web's <c>calculateSuccessChance</c>
        /// term for term - the move's base, half the view up to +25, +15 in a pact, +10 with a deal standing
        /// between them, +15 where the player has the move's trait, -30 with a deal between them the player
        /// broke - rounded as the web rounds, and held to 5-95. Where the web reads the player's own score
        /// of them, the roll reads theirs of the player, since the answer is theirs. The roll reads that
        /// view, whether their pact with the player holds and every deal the player broke; the chance shown
        /// (<paramref name="asKnown"/>) reads the player's presumed view of them, the pact as the player
        /// knows it (<see cref="KnownOdds.PresumedView"/>, <see cref="KnownOdds.KnownPact"/>,
        /// <see cref="Allegiance.HoldsAsKnown"/>) and only the broken deals the player can know of - not a
        /// voting bloc the other's secret ballot broke (<see cref="KnownBreach"/>, decision 4). Every other
        /// term is the player's own to see, so where the player knows all three, the two are equal.
        /// </summary>
        public static double Chance(EpisodeState s, string npcId, string move, bool asKnown)
        {
            var npc = s?.Find(npcId);
            var me = s?.Find(s.playerId);
            if (npc == null || me == null) return 0;
            double chance = Base(move);
            double view = asKnown ? KnownOdds.PresumedView(s, npcId) : s.Score(npcId, s.playerId);
            chance += Math.Min(view * 0.5, 25);
            bool allied = asKnown ? KnownOdds.KnownPact(s, npcId, s.playerId) && Allegiance.HoldsAsKnown(s, npcId)
                : Allegiance.Holds(s, npcId, s.playerId);
            if (allied) chance += 15;
            if (NpcDeals.Between(s, npcId, s.playerId).Count > 0) chance += 10;
            string trait = TraitBonus(move);
            if (trait != null && me.traits != null && me.traits.Any(t => string.Equals(t, trait, StringComparison.OrdinalIgnoreCase))) chance += 15;
            if (s.deals.Any(d => asKnown ? KnownBreach(s, d, npcId) : Pair(d, npcId, s.playerId) && Breaches.CountsAgainst(s, d, s.playerId)))
                chance -= 30;
            return Math.Max(PlayerDeals.MinimumChance, Math.Min(PlayerDeals.MaximumChance, Math.Floor(chance + 0.5)));
        }

        /// <summary>The chance a move is shown with, and its word: the player's read (<see cref="Chance"/>, as known), never the roll's own number.</summary>
        public static string ShownWord(EpisodeState s, string npcId, string move) => KnownOdds.Word(Chance(s, npcId, move, true));

        // ---------------------------------------------------------------- calling in a promise

        /// <summary>The ledger types a call-in writes on the maker's record of the player: held where it landed, pressed where it did not.</summary>
        public const string HeldPrefix = "promise-held:", PressedPrefix = "promise-pressed:";

        public static string HeldType(PromiseKind kind, string approach) => HeldPrefix + kind + ":" + approach;
        public static string PressedType(PromiseKind kind, string approach) => PressedPrefix + kind + ":" + approach;

        /// <summary>
        /// Whether a promise of this kind can be called in: one a decision settles and weighs - safety,
        /// which the maker's nominations keep or break, and a final two, which the final choice does.
        /// </summary>
        public static bool Callable(PromiseKind kind) => kind == PromiseKind.Safety || kind == PromiseKind.FinalTwo;

        /// <summary>
        /// The promises this houseguest owes the player that can be called in right now, under the
        /// commitment rules: standing, of a kind a decision weighs, made to the player, and not called in
        /// already this week. Once a week each.
        /// </summary>
        public static List<PromiseState> Owed(EpisodeState s, string npcId)
        {
            if (!EpisodeEngine.CommitmentRulesOn(s) || string.IsNullOrEmpty(npcId) || s.Find(s.playerId)?.status != ContestantStatus.Active
                || s.Find(npcId)?.status != ContestantStatus.Active) return new List<PromiseState>();
            return s.promises.Where(p => p.status == PromiseStatus.Active && p.fromId == npcId && p.toId == s.playerId
                    && Callable(p.kind) && !CalledThisWeek(s, p))
                .ToList();
        }

        /// <summary>Why this promise cannot be called in this way right now; null when it can.</summary>
        public static string CallInRefusal(EpisodeState s, string npcId, string promiseId, string approach)
        {
            if (!EpisodeEngine.CommitmentRulesOn(s)) return NotThisSeason;
            if (Array.IndexOf(Approaches, approach) < 0) return "That is no way to call in a promise.";
            var promise = s.promises.FirstOrDefault(p => p.id == promiseId);
            if (promise == null || promise.fromId != npcId || promise.toId != s.playerId)
                return "Name a promise " + Name(s, npcId) + " made you.";
            if (promise.status != PromiseStatus.Active) return "That promise is not standing any more.";
            if (!Callable(promise.kind)) return "Nothing that promise binds is decided anywhere you could hold them to it.";
            if (CalledThisWeek(s, promise)) return "You have already called that promise in this week.";
            return null;
        }

        /// <summary>Whether this promise was called in this week already, whether it landed or not.</summary>
        public static bool CalledThisWeek(EpisodeState s, PromiseState p)
        {
            var edge = Edge(s, p.fromId, p.toId);
            if (edge == null) return false;
            string held = HeldPrefix + p.kind + ":", pressed = PressedPrefix + p.kind + ":";
            return edge.events.Any(e => e.week == s.week && e.type != null
                && (e.type.StartsWith(held, StringComparison.Ordinal) || e.type.StartsWith(pressed, StringComparison.Ordinal)));
        }

        /// <summary>
        /// How hard the player has held this promise's maker to it: the strongest approach that landed
        /// since it was made (<see cref="Hold"/>), on the maker's own record of the player; 0 for a
        /// promise never called in, one no longer standing, and every promise without the rules.
        /// </summary>
        public static double HeldTo(EpisodeState s, PromiseState p)
        {
            if (!EpisodeEngine.CommitmentRulesOn(s) || p == null || p.status != PromiseStatus.Active || p.toId != s.playerId) return 0;
            var edge = Edge(s, p.fromId, p.toId);
            if (edge == null) return 0;
            string prefix = HeldPrefix + p.kind + ":";
            double hold = 0;
            foreach (var e in edge.events)
                if (e.week >= p.week && e.type != null && e.type.StartsWith(prefix, StringComparison.Ordinal))
                    hold = Math.Max(hold, Hold(e.type.Substring(prefix.Length)));
            return hold;
        }

        /// <summary>
        /// What a promise of safety the player called in adds to its maker's reluctance to put them up, as
        /// Head of Household (<see cref="StrategyRules.NominationReluctance"/>): a safety deal's own weight
        /// (<see cref="StrategyRules.DealWeight"/>), times how hard it was held. Nothing for anybody else.
        /// </summary>
        public static double SafetyHeld(EpisodeState s, string hohId, string id)
        {
            if (s == null || id != s.playerId || !EpisodeEngine.CommitmentRulesOn(s)) return 0;
            double hold = s.promises.Where(p => p.fromId == hohId && p.toId == id && p.kind == PromiseKind.Safety && p.status == PromiseStatus.Active)
                .Sum(p => HeldTo(s, p));
            return hold * StrategyRules.DealWeight(DealKind.SafetyAgreement);
        }

        /// <summary>A promise's kind, as a call-in names it: "safety", "a final two".</summary>
        public static string PromiseWords(PromiseKind kind)
        {
            switch (kind)
            {
                case PromiseKind.Safety: return "safety";
                case PromiseKind.FinalTwo: return "a final two";
                case PromiseKind.Vote: return "their vote";
                case PromiseKind.AllianceLoyalty: return "loyalty";
                default: return "information";
            }
        }

        /// <summary>The call-in's line, as it landed or not.</summary>
        public static string CallInLine(EpisodeState s, PromiseState p, string approach, bool landed)
        {
            string who = Name(s, p.fromId), what = "their promise of " + PromiseWords(p.kind);
            switch (approach)
            {
                case Remind:
                    return "You reminded " + who + " of " + what + (landed ? ", and they say they will keep it." : ". They would not say they will keep it.");
                case Demand:
                    return "You demanded " + who + " keep " + what + (landed ? ". They will, and they did not like being told." : ". They refused to be told.");
                default:
                    return "You threatened to tell the house if " + who + " breaks " + what
                           + (landed ? ". They will keep it, and they resent you for it." : ". They called your bluff.");
            }
        }

        // ---------------------------------------------------------------- mending fences

        /// <summary>The ledger types mending fences writes on the wronged one's record of the player, permanent: made amends, or turned away.</summary>
        public const string AmendsType = "amends-made", RebuffType = "amends-refused";

        /// <summary>
        /// The breaches the player committed against this houseguest, under the commitment rules, as far as
        /// the player can know them: deals between them the player broke (<see cref="KnownBreach"/>) and
        /// promises the player made them and broke, which were the player's own act. A voting bloc that fell
        /// apart is not one until the player knows how the other voted (decision 4): mending fences over it
        /// would tell them.
        /// </summary>
        public static int BreachesAgainst(EpisodeState s, string npcId)
        {
            if (!EpisodeEngine.CommitmentRulesOn(s) || string.IsNullOrEmpty(npcId)) return 0;
            string me = s.playerId;
            return s.deals.Count(d => KnownBreach(s, d, npcId))
                   + s.promises.Count(p => p.fromId == me && p.toId == npcId && Breaches.CountsAgainst(s, p, me));
        }

        /// <summary>
        /// Whether this deal between the player and this houseguest is a breach of the player's
        /// (<see cref="Breaches.CountsAgainst(EpisodeState, DealState, string)"/>) that the player can know
        /// of: every one they broke by their own act, and a voting bloc - which both ballots break at once -
        /// only once they know the other's (<see cref="KnownBallots.DealOutcomeKnown"/>), as every other
        /// reader of a bloc waits for it.
        /// </summary>
        private static bool KnownBreach(EpisodeState s, DealState d, string npcId) =>
            Pair(d, npcId, s.playerId) && Breaches.CountsAgainst(s, d, s.playerId) && KnownBallots.DealOutcomeKnown(s, d);

        /// <summary>How often the player has tried to mend fences with this houseguest, whether it worked or not.</summary>
        public static int MendsTried(EpisodeState s, string npcId)
        {
            var edge = Edge(s, npcId, s?.playerId);
            return edge == null ? 0 : edge.events.Count(e => e.type == AmendsType || e.type == RebuffType);
        }

        /// <summary>Why the player cannot mend fences with this houseguest now; null when they can: a breach of theirs not yet tried, once each.</summary>
        public static string MendRefusal(EpisodeState s, string npcId)
        {
            if (!EpisodeEngine.CommitmentRulesOn(s)) return NotThisSeason;
            int breaches = BreachesAgainst(s, npcId);
            if (breaches == 0) return "You have broken no word with " + Name(s, npcId) + " to make amends for.";
            if (MendsTried(s, npcId) >= breaches) return "You have tried to make amends for every word you broke with " + Name(s, npcId) + ".";
            return null;
        }

        /// <summary>The breach a mend is about, for its line: the latest the player committed against them that they can know of.</summary>
        public static string BreachWords(EpisodeState s, string npcId)
        {
            string me = s.playerId;
            var deal = s.deals.Where(d => KnownBreach(s, d, npcId))
                .OrderByDescending(d => d.settledWeek).ThenByDescending(d => d.week).FirstOrDefault();
            var promise = s.promises.Where(p => p.fromId == me && p.toId == npcId && Breaches.CountsAgainst(s, p, me))
                .OrderByDescending(p => p.settledWeek).ThenByDescending(p => p.week).FirstOrDefault();
            if (deal != null && (promise == null || Math.Max(deal.settledWeek, deal.week) >= Math.Max(promise.settledWeek, promise.week)))
                return "the " + DealKind.Title(deal.type).ToLowerInvariant() + " you broke";
            if (promise != null) return "the promise of " + PromiseWords(promise.kind) + " you broke";
            return "the word you broke";
        }

        /// <summary>The mend's line, as it landed or not.</summary>
        public static string MendLine(EpisodeState s, string npcId, bool landed) =>
            landed ? "You made amends with " + Name(s, npcId) + " for " + BreachWords(s, npcId) + ". They are willing to move past it."
                : Name(s, npcId) + " is not ready to move past " + BreachWords(s, npcId) + ".";

        // ---------------------------------------------------------------- a veto for a price

        /// <summary>
        /// Why the player, holding the veto, cannot name this price to this nominee now; null when they can:
        /// under the commitment rules, with room for the two deals it strikes under the player's ceiling
        /// (<see cref="PlayerDeals.PlayerDealCeiling"/>, refused in the deal table's own words); at the veto
        /// meeting before the decision, with the veto theirs to use; a nominee on the block who is not the
        /// player; no word on the veto given already, to them or to the other nominee (it saves one); and a
        /// price that fits (<see cref="VetoPriceFits"/>).
        /// </summary>
        public static string VetoPriceRefusal(EpisodeState s, string nomineeId, string kind)
        {
            if (!EpisodeEngine.CommitmentRulesOn(s)) return NotThisSeason;
            if (s.deals.Count + 2 > PlayerDeals.PlayerDealCeiling) return TooManyArrangements;
            if (s.phase != EpisodePhase.VetoMeeting || s.vetoResolved || s.vetoHolderId != s.playerId || !StrategyRules.VetoCanBeUsed(s))
                return "Only the veto holder can name a price, before the veto meeting decides.";
            if (string.IsNullOrEmpty(nomineeId) || nomineeId == s.playerId || !s.nominees.Contains(nomineeId))
                return "Name a price to somebody on the block.";
            if (s.deals.Any(d => d.type == DealKind.VetoUse && d.week == s.week && (d.status == DealStatus.Active || d.status == DealStatus.Accepted)
                    && (d.proposerId == s.playerId || d.recipientId == s.playerId)))
                return "You have already given your word on the veto this week, and it saves one of them.";
            if (kind != DealKind.FinalTwo && kind != DealKind.VoteSave) return "That is no price for the veto.";
            if (!VetoPriceFits(s, nomineeId, kind))
                return kind == DealKind.FinalTwo ? "It is too early in the season to be talking about the final two."
                    : "They already owe you their vote, or the house does not bargain over votes this season.";
            return null;
        }

        /// <summary>The prices this nominee could be asked for the veto right now, each a move of its own.</summary>
        public static List<string> VetoPrices(EpisodeState s, string nomineeId) =>
            new[] { DealKind.VoteSave, DealKind.FinalTwo }.Where(kind => VetoPriceRefusal(s, nomineeId, kind) == null).ToList();

        /// <summary>A veto for a price, as it went: "Maya Hassan took your price: you will use the veto on them, and they owe you a final two."</summary>
        public static string VetoPriceLine(EpisodeState s, string nomineeId, Price price, bool taken) =>
            taken ? Name(s, nomineeId) + " took your price: you will use the veto on them, and they owe you " + PriceWords(s, price) + "."
                : Name(s, nomineeId) + " would not pay your price for the veto.";

        // ---------------------------------------------------------------- the parts

        /// <summary>The refusal of every move in a season without the commitment rules.</summary>
        public const string NotThisSeason = "The house is not bargaining like that this season.";

        /// <summary>The refusal at the player's deal ceiling, in the deal table's own words (<see cref="PlayerDeals.CanPropose"/>).</summary>
        public const string TooManyArrangements = "You already have more arrangements than you can keep track of.";

        /// <summary>Whether a deal of this kind - about this houseguest, where it names one - already binds the two of them, or waits on an answer.</summary>
        public static bool Binding(EpisodeState s, string a, string b, string kind, string aboutId) =>
            s.deals.Any(d => DealStatus.Binds(d.status) && d.type == kind && Pair(d, a, b) && (aboutId == null || d.targetId == aboutId));

        private static bool Pair(DealState d, string a, string b) =>
            d != null && ((d.proposerId == a && d.recipientId == b) || (d.proposerId == b && d.recipientId == a));

        /// <summary>A deal's kind as a counter names it, with who it is about where it names somebody: "safety pact", "vote to evict Jo Smith".</summary>
        public static string DealWords(EpisodeState s, string kind, string aboutId)
        {
            string title = DealKind.Title(kind).ToLowerInvariant();
            if (aboutId == null || s.Find(aboutId) == null) return title;
            string about = aboutId == s.playerId ? "you" : Name(s, aboutId);
            return kind == DealKind.TargetAgreement ? title + " against " + about : title + " " + about;
        }

        /// <summary>What breaking a deal of this kind stakes, in the deal table's own words.</summary>
        public static string StakesWord(string kind)
        {
            switch (DealKind.DefaultTrust(kind))
            {
                case DealTrust.Critical: return "highest stakes";
                case DealTrust.High: return "high stakes";
                case DealTrust.Low: return "low stakes";
                default: return "medium stakes";
            }
        }

        private static RelationshipState Edge(EpisodeState s, string from, string to) =>
            s?.relationships?.FirstOrDefault(r => r.fromId == from && r.toId == to);

        private static string Name(EpisodeState s, string id) => s.Find(id)?.name ?? "Unknown housemate";

        private static string First(EpisodeState s, string id) => FinalistRead.FirstName(Name(s, id));

        private static string Article(string noun) =>
            (noun.Length > 0 && "aeiou".IndexOf(char.ToLowerInvariant(noun[0])) >= 0 ? "an " : "a ") + noun;
    }
}
