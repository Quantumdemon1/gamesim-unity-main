using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// What is waiting on the player, and when each of it stops waiting (ACTIONS-DEALS-ALLIANCES-PLAN
    /// V2). A weekly offer used to write no log line, sit inside one houseguest's conversation and go
    /// at the house's next round, costing a point of Game Sense the player never saw coming. This is
    /// what the cast strip's badges, the objective's line and the strategy screens' footer read.
    ///
    /// <para><b>Pending state only.</b> The offers the house put to the player that they have not
    /// answered (<see cref="NpcDeals.Pending"/>) and the houseguests who came to them and wait on a
    /// reply (<see cref="EpisodeState.replyCards"/>). Nothing here writes, rolls or logs, so reading a
    /// season changes nothing in it and no later event id moves. A story beat is not on the list: it
    /// is the episode screen's to put in front of the player, and not somebody waiting on an answer.</para>
    ///
    /// <para><b>Only what was put to the player, and only while it can be answered.</b> An offer
    /// between two houseguests is theirs, and a deal the player struck is waiting on nobody; neither
    /// is listed. Nor is anything from somebody who has left the house, whom the engine would refuse
    /// an answer; anything at all once the player is out of the game; anything from the final Head of
    /// Household on, when no window for a word comes again; or an offer the player has no way left to
    /// answer before it goes (<see cref="StillAnswerable"/>).</para>
    /// </summary>
    public static class WaitingOnYou
    {
        /// <summary>What is waiting: an offer, a question about the veto, or a houseguest who came to the player.</summary>
        public enum Kind { Offer, VetoAsk, Card }

        /// <summary>When something waiting stops waiting by itself, by the engine's own rules.</summary>
        public enum Lapse
        {
            /// <summary>A question about the veto, withdrawn once the veto is decided (<c>EpisodeEngine.ResolveVeto</c>).</summary>
            VetoDecision,
            /// <summary>An offer, written off by the house's next round as a campaign opens (<see cref="NpcDeals.Settle"/>).</summary>
            CampaignOpens,
            /// <summary>A card, cleared as campaigning closes.</summary>
            CampaignCloses,
            /// <summary>A card, cleared as free time ends and the week turns.</summary>
            FreeTimeEnds,
            /// <summary>Nothing left in the season takes it away: it waits for as long as its sender is in the house.</summary>
            Open,
        }

        /// <summary>One thing waiting on the player: what it is, its own id, who it is from, and when it goes.</summary>
        public sealed class Item
        {
            public Kind kind;
            /// <summary>The deal's id, or the card's.</summary>
            public string id;
            /// <summary>Who is waiting on the player.</summary>
            public string fromId;
            /// <summary>The deal's kind (<see cref="DealKind"/>), or the card's (<see cref="ReplyCards"/>).</summary>
            public string type;
            public int week;
            public Lapse lapses;
            /// <summary>Whether the next Advance from this state lets it go.</summary>
            public bool lapsesOnAdvance;

            public override string ToString() =>
                kind + " " + type + " from " + fromId + " (" + lapses + (lapsesOnAdvance ? ", on this advance)" : ")");
        }

        /// <summary>Everything waiting on the player: the offers in the order the deal table lists them, then the cards oldest first.</summary>
        public sealed class Reading
        {
            public readonly List<Item> items = new List<Item>();

            public bool Any => items.Count > 0;

            /// <summary>Whether this houseguest has an offer, or a question about the veto, waiting on the player.</summary>
            public bool OfferFrom(string id) => id != null && items.Any(item => item.fromId == id && item.kind != Kind.Card);

            /// <summary>Whether this houseguest came to the player and waits on an answer.</summary>
            public bool AnswerFrom(string id) => id != null && items.Any(item => item.fromId == id && item.kind == Kind.Card);
        }

        /// <summary>
        /// Everything waiting on the player in this state. Empty for no state, for a player out of
        /// the game, and from the final Head of Household on.
        /// </summary>
        public static Reading Read(EpisodeState s)
        {
            var reading = new Reading();
            if (s == null || !Answers(s) || PastAnswering(s)) return reading;
            foreach (var deal in NpcDeals.Pending(s))
            {
                if (!Present(s, deal.proposerId)) continue;
                var lapses = OfferLapse(s, deal, out bool now);
                if (!StillAnswerable(s, deal, lapses)) continue;
                reading.items.Add(new Item
                {
                    kind = deal.type == DealKind.VetoUse ? Kind.VetoAsk : Kind.Offer,
                    id = deal.id, fromId = deal.proposerId, type = deal.type, week = deal.week,
                    lapses = lapses, lapsesOnAdvance = now,
                });
            }
            foreach (var card in s.replyCards)
            {
                if (card == null || !Present(s, card.fromId)) continue;
                var lapses = CardLapse(s, out bool now);
                reading.items.Add(new Item
                {
                    kind = Kind.Card, id = card.id, fromId = card.fromId, type = card.kind, week = card.week,
                    lapses = lapses, lapsesOnAdvance = now,
                });
            }
            return reading;
        }

        /// <summary>Whether the player can answer anything at all: an evicted player follows the season and answers nothing.</summary>
        private static bool Answers(EpisodeState s) => s.Find(s.playerId)?.status == ContestantStatus.Active;

        /// <summary>
        /// From the final Head of Household on, nothing put to the player can be answered: no free
        /// time or campaign comes again, and nobody has time for a word between the parts, the final
        /// choice and the jury's night. Nor does anything write an offer off any more - the house's
        /// rounds are over - so one still on the table would wait for ever; it is not listed at all.
        /// </summary>
        private static bool PastAnswering(EpisodeState s)
        {
            switch (s.phase)
            {
                case EpisodePhase.FinalHoHPart1:
                case EpisodePhase.FinalHoHPart2:
                case EpisodePhase.FinalHoHPart3:
                case EpisodePhase.FinalEviction:
                case EpisodePhase.JuryQuestioning:
                case EpisodePhase.FinalSpeeches:
                case EpisodePhase.Jury:
                case EpisodePhase.Finished:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Whether the player still has a way to answer an offer before it goes: a word with its
        /// sender, now or in a window still ahead of its lapse. Under the week's windows every
        /// houseguest has time for a word in every window, so they always do. Without them only the
        /// people deciding have time for one between the Head of Household and the campaign, and the
        /// veto meeting is the last of that before the campaign's opening writes last week's offers
        /// off: there, one from anybody not deciding the veto is past answering. (Earlier in the
        /// week who will be deciding the veto is not known yet, so an offer then is still listed.)
        /// A question about the veto is answered on the decision itself, and is always listed.
        /// </summary>
        private static bool StillAnswerable(EpisodeState s, DealState deal, Lapse lapses) =>
            EpisodeEngine.WeekRulesOn(s) || s.phase != EpisodePhase.VetoMeeting || lapses != Lapse.CampaignOpens
            || deal.expiresWeek >= s.week || StrategyRules.IsDecider(s, deal.proposerId);

        /// <summary>Whether somebody other than the player is still in the house to be answered.</summary>
        private static bool Present(EpisodeState s, string id)
        {
            var who = s.Find(id);
            return who != null && !who.isPlayer && who.id != s.playerId && who.status == ContestantStatus.Active;
        }

        /// <summary>
        /// When an offer goes. A question about the veto goes once the veto is decided, under the
        /// strategy windows: an advance decides it for a houseguest holding the veto, and the player
        /// holding it decides it themselves. Everything else goes at the house's next round, whose
        /// books are settled as a campaign opens, writing off whatever was put in an earlier week:
        /// this week's offers at next week's campaign, and last week's at this week's. (The round as
        /// free time opens never finds one from an earlier week: that week's campaign already did.)
        /// </summary>
        private static Lapse OfferLapse(EpisodeState s, DealState deal, out bool onAdvance)
        {
            onAdvance = false;
            if (deal.type == DealKind.VetoUse && StrategyRules.Apply(s) && !s.vetoResolved)
            {
                onAdvance = s.phase == EpisodePhase.VetoMeeting && s.vetoHolderId != s.playerId;
                return Lapse.VetoDecision;
            }
            // NpcDeals.Settle writes nothing off without the house's social state, or an open-ended offer.
            if (s.npcSocial == null || deal.expiresWeek <= 0) return Lapse.Open;
            bool earlier = deal.expiresWeek < s.week;
            onAdvance = earlier && s.phase == EpisodePhase.VetoMeeting && s.vetoResolved;
            return CampaignAhead(s, earlier) ? Lapse.CampaignOpens : Lapse.Open;
        }

        /// <summary>
        /// Whether a campaign still opens this season late enough to write an offer off: this week's
        /// while it is ahead, for an offer from an earlier week; otherwise next week's, which comes
        /// while the house is still four or more once this week's eviction is done.
        /// </summary>
        private static bool CampaignAhead(EpisodeState s, bool earlier)
        {
            int active = s.Active.Count();
            switch (s.phase)
            {
                case EpisodePhase.HoH:
                case EpisodePhase.Nomination:
                case EpisodePhase.VetoSelection:
                case EpisodePhase.Veto:
                case EpisodePhase.VetoMeeting:
                    return earlier || active - 1 >= 4;
                case EpisodePhase.Campaign:
                    return active - 1 >= 4;
                case EpisodePhase.Eviction:
                    return active - (s.evictionResolved ? 0 : 1) >= 4;
                case EpisodePhase.Social:
                    return active >= 4;
                default:
                    return false;
            }
        }

        /// <summary>
        /// When a card goes: the engine clears every card as campaigning closes and as free time
        /// ends, whichever comes first from here. Past the last free time nothing clears one.
        /// </summary>
        private static Lapse CardLapse(EpisodeState s, out bool onAdvance)
        {
            onAdvance = s.phase == EpisodePhase.Campaign || s.phase == EpisodePhase.Social;
            switch (s.phase)
            {
                case EpisodePhase.HoH:
                case EpisodePhase.Nomination:
                case EpisodePhase.VetoSelection:
                case EpisodePhase.Veto:
                case EpisodePhase.VetoMeeting:
                case EpisodePhase.Campaign:
                    return Lapse.CampaignCloses;
                case EpisodePhase.Eviction:
                case EpisodePhase.Social:
                    return Lapse.FreeTimeEnds;
                default:
                    return Lapse.Open;
            }
        }

        // ---------------------------------------------------------------- the window's seats

        /// <summary>
        /// Whether the next Advance from here moves the week into another window, so what is left of
        /// this one's seats does not carry. Only under the week's windows: the old weekly pool carries
        /// to the week's end. The Head of Household naming the nominees, the veto's draw and its
        /// competition, and a houseguest deciding the veto all stay inside the window they are in.
        /// </summary>
        public static bool AdvanceClosesWindow(EpisodeState s)
        {
            if (s == null || !EpisodeEngine.WeekRulesOn(s) || EpisodeEngine.Window(s) == Windows.None) return false;
            switch (s.phase)
            {
                case EpisodePhase.Nomination: return s.nominees.Count > 0;
                case EpisodePhase.VetoMeeting: return s.vetoResolved;
                case EpisodePhase.Campaign:
                case EpisodePhase.Social: return true;
                default: return false;
            }
        }

        /// <summary>
        /// How many of the player's actions the next Advance from here loses: what is left of this
        /// window's own seats, when the advance closes it. The week's extras - time bought, what a
        /// story left - are the week's to spend in any window, so a window that closes hands what is
        /// left of them on, until the last window of the week takes everything with it. A Have-Not
        /// has no extras to hand on, and every seat of their window is its own. Move-in night is the
        /// week's free time but does not turn the week, so it loses only the night's own seats, as
        /// the free-time board counts it.
        /// </summary>
        public static int ActionsLostOnAdvance(EpisodeState s)
        {
            if (s == null || !Answers(s) || !AdvanceClosesWindow(s)) return 0;
            int window = EpisodeEngine.Window(s);
            int budget = EpisodeEngine.SocialActionBudget(s), spent = EpisodeEngine.SocialActionsSpent(s);
            int left = Math.Max(0, budget - spent);
            if (window == Windows.AfterEviction && !EpisodeEngine.IsFirstNight(s)) return left;
            int own = EpisodeEngine.WeeklyExtras(s) < 0 ? budget : EpisodeEngine.WindowSeats(s, window);
            return Math.Max(0, Math.Min(left, own - spent));
        }

        // ---------------------------------------------------------------- the words

        /// <summary>
        /// The objective's line when somebody waits on the player's answer: who has an offer for
        /// them, or who came to them; how many, when it is more than one. Null when nobody does - a
        /// story beat is not an answer owed, and the objective keeps its own words for it.
        /// </summary>
        public static string ObjectiveLine(EpisodeState s) => ObjectiveLine(s, Read(s));

        /// <inheritdoc cref="ObjectiveLine(EpisodeState)"/>
        public static string ObjectiveLine(EpisodeState s, Reading reading)
        {
            if (s == null || reading == null) return null;
            var items = reading.items;
            if (items.Count == 0) return null;
            if (items.Count == 1)
                return items[0].kind == Kind.Card
                    ? First(s, items[0].fromId) + " is waiting on your answer"
                    : First(s, items[0].fromId) + " has an offer for you";
            if (items.All(item => item.kind != Kind.Card)) return items.Count + " offers waiting";
            var senders = items.Select(item => item.fromId).Distinct().ToList();
            return senders.Count == 1
                ? First(s, senders[0]) + " is waiting on your answer"
                : senders.Count + " houseguests are waiting on you";
        }

        /// <summary>
        /// What the next Advance from here lets go, for the strategy screens' footer: the offers it
        /// lets lapse and when, the houseguests who came to the player and go unanswered, and the
        /// window's unused actions. Null when it lets nothing go.
        /// </summary>
        public static string AdvanceNote(EpisodeState s) => AdvanceNote(s, Read(s));

        /// <inheritdoc cref="AdvanceNote(EpisodeState)"/>
        public static string AdvanceNote(EpisodeState s, Reading reading)
        {
            if (s == null || reading == null) return null;
            var lines = new List<string>();
            foreach (var group in reading.items.Where(item => item.lapsesOnAdvance && item.kind != Kind.Card).GroupBy(item => item.lapses))
                lines.Add(OffersGo(s, group.ToList(), group.Key));
            var cards = reading.items.Where(item => item.lapsesOnAdvance && item.kind == Kind.Card).ToList();
            if (cards.Count > 0) lines.Add(CardsGo(s, cards));
            int lost = ActionsLostOnAdvance(s);
            if (lost > 0) lines.Add(lost + (lost == 1 ? " unused action" : " unused actions") + " will be lost.");
            return lines.Count == 0 ? null : string.Join(" ", lines);
        }

        private static string OffersGo(EpisodeState s, List<Item> offers, Lapse lapses)
        {
            bool veto = offers.All(item => item.kind == Kind.VetoAsk);
            string what = offers.Count == 1
                ? First(s, offers[0].fromId) + (veto ? "'s ask for the veto expires " : "'s offer expires ")
                : offers.Count + (veto ? " asks for the veto expire " : " offers expire ");
            return what + When(lapses) + ".";
        }

        private static string CardsGo(EpisodeState s, List<Item> cards)
        {
            string when = When(cards[0].lapses);
            var kinds = cards.Select(card => card.type).Distinct().ToList();
            string noun = kinds.Count == 1 ? CardNoun(kinds[0]) : null;
            if (cards.Count == 1)
                return First(s, cards[0].fromId) + (noun != null ? "'s " + noun + " goes" : " goes") + " unanswered " + when + ".";
            if (noun != null) return cards.Count + " " + noun + "s go unanswered " + when + ".";
            return cards.Select(card => card.fromId).Distinct().Count() + " houseguests go unanswered " + when + ".";
        }

        /// <summary>What a card is called in a sentence, or null where the card's title says it better than a noun would.</summary>
        private static string CardNoun(string kind) =>
            kind == ReplyCards.Plea ? "plea" : kind == ReplyCards.Confrontation ? "confrontation" : null;

        private static string When(Lapse lapses)
        {
            switch (lapses)
            {
                case Lapse.VetoDecision: return "once the veto is decided";
                case Lapse.CampaignOpens: return "when the campaign opens";
                case Lapse.CampaignCloses: return "when campaigning closes";
                case Lapse.FreeTimeEnds: return "when free time ends";
                default: return "later";
            }
        }

        private static string First(EpisodeState s, string id)
        {
            string first = FinalistRead.FirstName(s.Find(id)?.name);
            return string.IsNullOrEmpty(first) ? "Somebody" : first;
        }
    }
}
