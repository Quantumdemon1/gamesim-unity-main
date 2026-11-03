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
        /// <summary>Where an ask comes from. Slice 4 adds the reply cards and the pitches; slice 5 the story's approaches.</summary>
        public enum Source { Offer, VetoAsk }

        /// <summary>One ask a houseguest can bring to the player: the record, who asks, their opening line and when it goes.</summary>
        public sealed class Ask
        {
            public Source source;
            /// <summary>The record's id (a deal's), who is asking, the record's kind, the line they open with, and when it goes in words (null when nothing takes it away).</summary>
            public string recordId, askerId, type, line, lapseLine;
            /// <summary>The week the record was put to the player.</summary>
            public int week;
            public WaitingOnYou.Lapse lapses;
            /// <summary>Whether the next Advance lets it go, and whether it is said in private (the whisper bubble).</summary>
            public bool lapsesOnAdvance, whisper;

            /// <summary>The deal sources' own check, the conversation's invitation, which revalidates the exact offer.</summary>
            private NpcInvitation invitation;

            public Ask() { }

            internal Ask(NpcInvitation invitation) { this.invitation = invitation; }

            /// <summary>
            /// Whether this ask still stands in <paramref name="s"/>: for an offer or a veto ask, the same session, week
            /// and phase, the player still in the game and the very offer still waiting with the same terms. A clock
            /// revision alone does not end it. False for an ask this reader did not build.
            /// </summary>
            public bool IsCurrent(EpisodeState s)
            {
                switch (source)
                {
                    case Source.Offer:
                    case Source.VetoAsk:
                        return invitation != null && invitation.IsCurrent(s);
                    default:
                        return false;
                }
            }

            public override string ToString() => source + " " + type + " " + recordId + " from " + askerId;
        }

        /// <summary>The line a dialogue says an ask stands until, for a question about the veto.</summary>
        public const string LapseAtTheVeto = "It stands until the veto is decided.";
        /// <summary>The line for an offer from an earlier week, which this week's campaign writes off.</summary>
        public const string LapseThisCampaign = "It stands until this week's campaign opens.";
        /// <summary>The line for this week's offer, which next week's campaign writes off.</summary>
        public const string LapseNextCampaign = "It stands until next week's campaign opens.";

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
            var invitations = NpcInvitation.Available(s);
            if (invitations.Count == 0) return asks.AsReadOnly();
            var pending = NpcDeals.Pending(s);
            bool roles = EpisodeEngine.RolesOn(s);
            var seen = new HashSet<string>(StringComparer.Ordinal);
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
        /// The public order: what the next advance lets go first; the newest week; the louder kind
        /// (<see cref="NpcDeals.Urgency"/>); a veto ask by its asker's place on the block, everyone else after; then
        /// the asker's id and the record's id. Never the filing order, which is the warmest first.
        /// </summary>
        private static IEnumerable<Ask> InPublicOrder(EpisodeState s, List<Ask> asks) =>
            asks.OrderByDescending(ask => ask.lapsesOnAdvance)
                .ThenByDescending(ask => ask.week)
                .ThenByDescending(ask => NpcDeals.Urgency(ask.type))
                .ThenBy(ask => BlockPlace(s, ask))
                .ThenBy(ask => ask.askerId, StringComparer.Ordinal)
                .ThenBy(ask => ask.recordId, StringComparer.Ordinal);

        private static int BlockPlace(EpisodeState s, Ask ask)
        {
            if (ask.source != Source.VetoAsk || s.nominees == null) return int.MaxValue;
            int place = s.nominees.IndexOf(ask.askerId);
            return place >= 0 ? place : int.MaxValue;
        }
    }
}
