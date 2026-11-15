using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// What the houseguests have put to the player, as asks a houseguest can bring to them in person
    /// (OFFERS-COME-TO-YOU-PLAN §C.1): the presentation's one queue. A weekly offer or a nominee's
    /// veto ask used to wait inside one houseguest's conversation, under a heading the player had to go
    /// looking for; the director now walks the asker over and opens the offer as a dialogue, in the
    /// order this reader gives.
    ///
    /// <para><b>What it lists.</b> The items of <see cref="WaitingOnYou.Read"/> that are not cards - so
    /// presence, answerability and the cut-off from the final Head of Household on are that reader's -
    /// joined by record id to <see cref="NpcInvitation.Available"/> with a matching proposer, which adds
    /// the phases a houseguest can raise an offer in and a named target still in the house. Under the
    /// role moments a veto board's ask (<see cref="RoleOffers.IsBoardAsk"/>) is not listed: its no and
    /// its silence carry the card's tone's price, which the board says and a dialogue that opens on
    /// "Think it over" would present as free.</para>
    ///
    /// <para><b>Reply cards (slice 4a).</b> A houseguest who came to the player with a confrontation, gossip or a
    /// plea (<see cref="ReplyCards.LegacyKinds"/>) and waits on an answer is listed too, as a <see cref="Source.Card"/>:
    /// only the card waiting first, the one the player can answer (<see cref="HoHPitches.Answerable"/>), and never a
    /// pitch, whose walk-up is its own source. A card needs no offer: it is read before the deal rows, so a house with no
    /// offer at all still lists it.</para>
    ///
    /// <para><b>Pitches (slice 4p).</b> The houseguests pitching a player Head of Household before the nominations walk up
    /// too, as <see cref="Source.Pitch"/> rows: every pitch the player can answer now (<see cref="HoHPitches.Answerable"/>) -
    /// under the role moments every unanswered pitch on the board, in the order the board was dealt (the order
    /// <c>HoldTheBoard</c> stored in <c>s.replyCards</c>), and before them only the pitch waiting first. They come before
    /// every offer and card: the nominations end every other walk-up of the phase as they end the pitches.</para>
    ///
    /// <para><b>One person going on (slice 4b).</b> Once the player has answered one houseguest's ask, the director offers
    /// that houseguest's next listed ask in the same dialogue, "There's something else", through <see cref="NextFrom"/>:
    /// the same queue, the same order, any source.</para>
    ///
    /// <para><b>Its order is public.</b> The ids follow the order the asks were filed in, which is the
    /// warmest first (<see cref="NpcDeals.Propose"/>, <see cref="NpcDeals.AskForTheVeto"/>), and the deal
    /// table's order (<see cref="NpcDeals.Pending"/>) breaks its ties by id, so neither is used: what
    /// lapses on the next advance first, then the newest week, the louder kind, the block's order for a
    /// veto ask, the asker's id and the record's id.</para>
    ///
    /// <para><b>Pure.</b> Nothing here writes, rolls or logs, as <see cref="WaitingOnYou"/> does not:
    /// reading a season changes nothing in it.</para>
    /// </summary>
    public static class ComesToYou
    {
        /// <summary>
        /// Where an ask comes from: a weekly offer, a question about the veto, a reply card (a confrontation, gossip or a
        /// plea), or a pitch to the player Head of Household. Values are appended, never reordered. Slice 5 adds the
        /// story's approaches.
        /// </summary>
        public enum Source { Offer, VetoAsk, Card, Pitch }

        /// <summary>One ask a houseguest can bring to the player: the record, who asks, their opening line and when it goes.</summary>
        public sealed class Ask
        {
            public Source source;
            /// <summary>The record's id (a deal's or a card's), who is asking, the record's kind, the line they open with, and when it goes in words (null when nothing takes it away).</summary>
            public string recordId, askerId, type, line, lapseLine;
            /// <summary>The week the record was put to the player.</summary>
            public int week;
            public WaitingOnYou.Lapse lapses;
            /// <summary>Whether the next Advance lets it go, and whether it is said in private (the whisper bubble).</summary>
            public bool lapsesOnAdvance, whisper;

            /// <summary>The deal sources' own check, the conversation's invitation, which revalidates the exact offer.</summary>
            private NpcInvitation invitation;

            /// <summary>A card's own check: a copy of the card as read, and the session, player, week and phase it was read in.</summary>
            private ReplyCardState card;
            private string readSession, readPlayer;
            private int readWeek;
            private EpisodePhase readPhase;

            public Ask() { }

            internal Ask(NpcInvitation invitation) { this.invitation = invitation; }

            internal Ask(EpisodeState s, ReplyCardState card)
            {
                this.card = card.Clone();
                readSession = s.sessionId; readPlayer = s.playerId; readWeek = s.week; readPhase = s.phase;
            }

            /// <summary>
            /// Whether this ask still stands in <paramref name="s"/>: for an offer or a veto ask, the same session, week
            /// and phase, the player still in the game and the very offer still waiting with the same terms; for a card,
            /// the same session, player, week and phase, the player still in the game, the very card still waiting (same
            /// id, kind, sender, subject and week), still listed as waiting and still the one the player can answer; for a
            /// pitch, all of a card's and the pitch still one the player hears (<see cref="HoHPitches.ValidCard"/>), which a
            /// feel-out leaves standing and an answer or the nominations end. A clock revision alone does not end it. False
            /// for an ask this reader did not build.
            /// </summary>
            public bool IsCurrent(EpisodeState s)
            {
                switch (source)
                {
                    case Source.Offer:
                    case Source.VetoAsk:
                        return invitation != null && invitation.IsCurrent(s);
                    case Source.Card:
                        return CardIsCurrent(s);
                    case Source.Pitch:
                        return CardIsCurrent(s) && HoHPitches.ValidCard(s, s.replyCards.FirstOrDefault(row => row != null && row.id == card.id));
                    default:
                        return false;
                }
            }

            private bool CardIsCurrent(EpisodeState s)
            {
                if (card == null || s == null || s.sessionId != readSession || s.playerId != readPlayer
                    || s.week != readWeek || s.phase != readPhase) return false;
                if (s.Find(s.playerId)?.status != ContestantStatus.Active) return false;
                var waiting = s.replyCards?.FirstOrDefault(row => row != null && row.id == card.id);
                if (waiting == null || waiting.kind != card.kind || waiting.fromId != card.fromId
                    || waiting.aboutId != card.aboutId || waiting.week != card.week) return false;
                if (!WaitingOnYou.Read(s).items.Any(item => item.kind == WaitingOnYou.Kind.Card && item.id == card.id)) return false;
                return HoHPitches.Answerable(s, card.id);
            }

            public override string ToString() => source + " " + type + " " + recordId + " from " + askerId;
        }

        /// <summary>The line a dialogue says an ask stands until, for a question about the veto.</summary>
        public const string LapseAtTheVeto = "It stands until the veto is decided.";
        /// <summary>The line for an offer from an earlier week, which this week's campaign writes off.</summary>
        public const string LapseThisCampaign = "It stands until this week's campaign opens.";
        /// <summary>The line for this week's offer, which next week's campaign writes off.</summary>
        public const string LapseNextCampaign = "It stands until next week's campaign opens.";
        /// <summary>The line for a card the close of campaigning clears.</summary>
        public const string LapseCampaignCloses = "It stands until campaigning closes.";
        /// <summary>The line for a card the end of free time clears.</summary>
        public const string LapseFreeTimeEnds = "It stands until free time ends.";

        /// <summary>
        /// What a houseguest with a card opens with, in plain words: the dialogue's speech card adds its own quote marks.
        /// The card's own words (<see cref="ReplyCards.Message"/>) stay what the card says under it.
        /// </summary>
        public const string ConfrontationLine = "Can we talk? It's about where we stand.",
                            GossipLine = "Got a minute? I think you've heard what I've been saying.",
                            PleaLine = "Can we talk? It's about the vote.";

        /// <summary>What a houseguest pitching the player Head of Household opens with (slice 4p), in plain words.</summary>
        public const string PitchLine = "Can we talk about your nominations?";
        /// <summary>The line for a pitch, which the nominations clear.</summary>
        public const string LapseNominations = "It stands until you nominate.";

        /// <summary>
        /// Every ask the houseguests can bring to the player in <paramref name="s"/>, in public order. Empty for no
        /// state, a player out of the game, and from the final Head of Household on.
        /// </summary>
        public static IReadOnlyList<Ask> Read(EpisodeState s)
        {
            var asks = new List<Ask>();
            if (s == null) return asks.AsReadOnly();
            var reading = WaitingOnYou.Read(s);
            if (!reading.Any) return asks.AsReadOnly();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            // A card needs no offer, so the cards come before the invitations are read: a house with no offer lists one.
            // So does a pitch.
            AddCards(s, reading, asks, seen);
            AddPitches(s, reading, asks, seen);
            var invitations = NpcInvitation.Available(s);
            if (invitations.Count == 0) return InPublicOrder(s, asks).ToList().AsReadOnly();
            var pending = NpcDeals.Pending(s);
            bool roles = EpisodeEngine.RolesOn(s);
            foreach (var item in reading.items)
            {
                if (item == null || item.kind == WaitingOnYou.Kind.Card || string.IsNullOrEmpty(item.id)) continue;
                // Two rows sharing an id: the first wins, as the deal table draws the first.
                if (!seen.Add(item.id)) continue;
                var invitation = invitations.FirstOrDefault(row => row.RecordId == item.id && row.NpcId == item.fromId);
                if (invitation == null) continue;
                var deal = pending.FirstOrDefault(row => row.id == item.id && row.proposerId == item.fromId);
                if (deal == null) continue;
                // The veto board's own asks stay on the board, where their tone and the price of silence are said.
                if (roles && RoleOffers.IsBoardAsk(s, deal)) continue;
                asks.Add(new Ask(invitation)
                {
                    source = item.kind == WaitingOnYou.Kind.VetoAsk ? Source.VetoAsk : Source.Offer,
                    recordId = item.id, askerId = item.fromId, type = item.type, week = item.week,
                    lapses = item.lapses, lapsesOnAdvance = item.lapsesOnAdvance,
                    line = invitation.OpeningLine,
                    lapseLine = LapseLine(item.lapses, deal.expiresWeek < s.week),
                    whisper = Whispered(item.type),
                });
            }
            return InPublicOrder(s, asks).ToList().AsReadOnly();
        }

        /// <summary>
        /// The reply cards that can come to the player: each card <see cref="WaitingOnYou"/> lists, of a legacy kind
        /// (confrontation, gossip, plea; never a pitch), that the player can answer now - so at most one, the card
        /// waiting first (<see cref="HoHPitches.Answerable"/>, as every surface that answers a card asks).
        /// </summary>
        private static void AddCards(EpisodeState s, WaitingOnYou.Reading reading, List<Ask> asks, HashSet<string> seen)
        {
            foreach (var item in reading.items)
            {
                if (item == null || item.kind != WaitingOnYou.Kind.Card || string.IsNullOrEmpty(item.id)) continue;
                if (Array.IndexOf(ReplyCards.LegacyKinds, item.type) < 0) continue;
                if (!HoHPitches.Answerable(s, item.id)) continue;
                var card = s.replyCards.FirstOrDefault(row => row != null && row.id == item.id && row.fromId == item.fromId);
                if (card == null || !seen.Add(item.id)) continue;
                asks.Add(new Ask(s, card)
                {
                    source = Source.Card,
                    recordId = card.id, askerId = card.fromId, type = card.kind, week = card.week,
                    lapses = item.lapses, lapsesOnAdvance = item.lapsesOnAdvance,
                    line = CardLine(card.kind),
                    lapseLine = CardLapseLine(item.lapses),
                    whisper = false,
                });
            }
        }

        /// <summary>
        /// The pitches that can come to the player Head of Household (slice 4p): each pitch <see cref="WaitingOnYou"/> lists
        /// - so one <see cref="HoHPitches.ValidCard"/> passes, before the nominations - that the player can answer now
        /// (<see cref="HoHPitches.Answerable"/>). Under the role moments that is every pitch on the board, in the order the
        /// waiting list reads them, which is the board's dealt order in <c>s.replyCards</c>; before them, only the pitch
        /// waiting first. An answered pitch has left <c>s.replyCards</c>, so its pitcher is not listed again.
        /// </summary>
        private static void AddPitches(EpisodeState s, WaitingOnYou.Reading reading, List<Ask> asks, HashSet<string> seen)
        {
            foreach (var item in reading.items)
            {
                if (item == null || item.kind != WaitingOnYou.Kind.Card || item.type != ReplyCards.Pitch || string.IsNullOrEmpty(item.id)) continue;
                if (!HoHPitches.Answerable(s, item.id)) continue;
                var card = s.replyCards.FirstOrDefault(row => row != null && row.id == item.id && row.fromId == item.fromId);
                if (card == null || !seen.Add(item.id)) continue;
                asks.Add(new Ask(s, card)
                {
                    source = Source.Pitch,
                    recordId = card.id, askerId = card.fromId, type = card.kind, week = card.week,
                    lapses = item.lapses, lapsesOnAdvance = item.lapsesOnAdvance,
                    line = PitchLine,
                    lapseLine = PitchLapseLine(item.lapses),
                    whisper = false,
                });
            }
        }

        /// <summary>How long a pitch stands, in words: until the player nominates; null for any other lapse.</summary>
        public static string PitchLapseLine(WaitingOnYou.Lapse lapses) =>
            lapses == WaitingOnYou.Lapse.NominationSet ? LapseNominations : null;

        /// <summary>What a houseguest with a card of this kind opens with; null for a kind that does not come to the player.</summary>
        public static string CardLine(string kind)
        {
            switch (kind)
            {
                case ReplyCards.Confrontation: return ConfrontationLine;
                case ReplyCards.Gossip: return GossipLine;
                case ReplyCards.Plea: return PleaLine;
                default: return null;
            }
        }

        /// <summary>How long a card stands, in words: until campaigning closes, until free time ends, or null when nothing takes it away.</summary>
        public static string CardLapseLine(WaitingOnYou.Lapse lapses)
        {
            switch (lapses)
            {
                case WaitingOnYou.Lapse.CampaignCloses: return LapseCampaignCloses;
                case WaitingOnYou.Lapse.FreeTimeEnds: return LapseFreeTimeEnds;
                default: return null;
            }
        }

        /// <summary>The ask for one record in <paramref name="s"/>, or null when it is not listed.</summary>
        public static Ask Find(EpisodeState s, string recordId) =>
            string.IsNullOrEmpty(recordId) ? null : Read(s).FirstOrDefault(ask => ask.recordId == recordId);

        /// <summary>
        /// How many other houseguests have something they can still bring to the player: the distinct askers, other
        /// than <paramref name="currentAskerId"/>, of the asks that pass <paramref name="canStillCome"/>. People, not
        /// asks: one houseguest with two asks is one more houseguest who wants a word.
        /// </summary>
        public static int OtherAskers(IReadOnlyList<Ask> asks, string currentAskerId, Func<Ask, bool> canStillCome)
        {
            if (asks == null) return 0;
            var askers = new HashSet<string>(StringComparer.Ordinal);
            foreach (var ask in asks)
            {
                if (ask == null || string.IsNullOrEmpty(ask.askerId) || ask.askerId == currentAskerId) continue;
                if (canStillCome != null && !canStillCome(ask)) continue;
                askers.Add(ask.askerId);
            }
            return askers.Count;
        }

        /// <summary>
        /// The ask the same houseguest still has for the player once one of theirs is answered (offers slice 4b, "There's
        /// something else"): the first of <paramref name="askerId"/>'s asks in <paramref name="asks"/> - the queue in public
        /// order, read again on the committed state - other than <paramref name="afterRecordId"/> (normally already gone
        /// from that reading) that passes <paramref name="canStillCome"/>; null when there is none. Never another
        /// houseguest's ask: a chain is one person going on, not the queue moving up. Every source is listed the same way,
        /// so a source added to <see cref="Read"/> chains with no change here.
        /// </summary>
        public static Ask NextFrom(IReadOnlyList<Ask> asks, string askerId, string afterRecordId, Func<Ask, bool> canStillCome)
        {
            if (asks == null || string.IsNullOrEmpty(askerId)) return null;
            foreach (var ask in asks)
            {
                if (ask == null || ask.askerId != askerId) continue;
                if (afterRecordId != null && ask.recordId == afterRecordId) continue;
                if (canStillCome != null && !canStillCome(ask)) continue;
                return ask;
            }
            return null;
        }

        /// <summary>Whether an ask of this kind is said in private: the final two and three, and sharing what you hear.</summary>
        public static bool Whispered(string type) =>
            type == DealKind.FinalTwo || type == DealKind.FinalThree || type == DealKind.InformationSharing;

        /// <summary>
        /// How long an ask stands, in words: until the veto is decided, until this week's or next week's campaign
        /// opens, or null when nothing in the season takes it away. <paramref name="earlier"/> is whether the offer
        /// came in an earlier week, as <see cref="WaitingOnYou"/> reads it.
        /// </summary>
        public static string LapseLine(WaitingOnYou.Lapse lapses, bool earlier)
        {
            switch (lapses)
            {
                case WaitingOnYou.Lapse.VetoDecision: return LapseAtTheVeto;
                case WaitingOnYou.Lapse.CampaignOpens: return earlier ? LapseThisCampaign : LapseNextCampaign;
                default: return null;
            }
        }

        /// <summary>
        /// The public order: the pitches first, in the board's own order (where they stand in <c>s.replyCards</c>, never by
        /// asker id); then what the next advance lets go first; the newest week; the louder kind
        /// (<see cref="NpcDeals.Urgency"/>); a veto ask by its asker's place on the block, everyone else after; then
        /// the asker's id and the record's id. Never the filing order, which is the warmest first.
        /// </summary>
        private static IEnumerable<Ask> InPublicOrder(EpisodeState s, List<Ask> asks) =>
            asks.OrderBy(ask => ask.source == Source.Pitch ? 0 : 1)
                .ThenBy(ask => PitchPlace(s, ask))
                .ThenByDescending(ask => ask.lapsesOnAdvance)
                .ThenByDescending(ask => ask.week)
                .ThenByDescending(ask => NpcDeals.Urgency(ask.type))
                .ThenBy(ask => BlockPlace(s, ask))
                .ThenBy(ask => ask.askerId, StringComparer.Ordinal)
                .ThenBy(ask => ask.recordId, StringComparer.Ordinal);

        /// <summary>A pitch's place in <c>s.replyCards</c>, the board's dealt order; 0 for every other source, which this key leaves as it was.</summary>
        private static int PitchPlace(EpisodeState s, Ask ask)
        {
            if (ask.source != Source.Pitch) return 0;
            int place = s.replyCards.FindIndex(card => card != null && card.id == ask.recordId);
            return place >= 0 ? place : int.MaxValue;
        }

        private static int BlockPlace(EpisodeState s, Ask ask)
        {
            if (ask.source != Source.VetoAsk || s.nominees == null) return int.MaxValue;
            int place = s.nominees.IndexOf(ask.askerId);
            return place >= 0 ? place : int.MaxValue;
        }
    }
}
