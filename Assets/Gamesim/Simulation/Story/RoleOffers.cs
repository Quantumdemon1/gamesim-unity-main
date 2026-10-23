using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// The offer board's cards in words (ROLE-MOMENTS-PLAN R2, §5.1): which offers stand on the board and in
    /// what order, and what each card says - who is asking and in what role, the deal, the tone with its mark,
    /// the situation word, the player's read, the quote, the terms, the stakes and the player's own history
    /// with them - and the heading and the one line a screen reader says after a card's caption (§5.4).
    ///
    /// <para><b>The order is public</b> (§3.0, §5.3): nominees in the order the Head of Household named them,
    /// then anybody else by id. Never <see cref="NpcDeals.Pending"/>'s order: it sorts by id, ids follow the
    /// order the asks were filed, and <see cref="NpcDeals.AskForTheVeto"/> files the warmest nominee first -
    /// a houseguest's hidden view of the player. The set is the old rows' own: this week's veto asks still
    /// waiting on the player, from houseguests still in the game.</para>
    ///
    /// <para><b>Only what is true now.</b> Every word is a public fact, the player's own reading or record,
    /// or the row's own terms (§5.3). The stakes line says what keeping and breaking a word does today: a kept
    /// word is remembered, a broken one may reach the house (<see cref="DealResolution"/>). The tone's riders
    /// are R3's to settle, so until they do no card promises one (<see cref="RidersSettle"/>), and the board
    /// never says that silence costs more than a no, which R3 makes true.</para>
    ///
    /// <para>Pure and read-only: it neither mutates the state nor draws from its generator, and lives in the
    /// simulation so the Unity-free subset tests it. The HUD draws it (<c>EpisodeHud.OfferBoard</c>), at the
    /// veto meeting and in the Diary Room's veto column, under <see cref="EpisodeEngine.RolesOn"/> only.</para>
    /// </summary>
    public static class RoleOffers
    {
        /// <summary>What a nominee's veto ask asks, under its chip ("Veto Commitment").</summary>
        public const string VetoAskLine = "Save me from the block with the Power of Veto";

        /// <summary>The front of every terms line: "In return: a final two".</summary>
        public const string TermsPrefix = "In return: ";

        /// <summary>The terms of an ask that carries no price: nothing is struck with the player's yes.</summary>
        public const string NothingPromised = "nothing promised";

        /// <summary>The most words a stakes line may have (§5.1's text caps).</summary>
        public const int MostStakesWords = 14;

        /// <summary>
        /// Whether a tone's rider settles yet (<see cref="RoleTone.Riders"/>). Not until R3 applies them: until
        /// then a Desperate nominee owes nothing for a kept word and a Threatening one holds no grudge for a no,
        /// so the stakes line says only what keeping and breaking do today.
        /// </summary>
        public const bool RidersSettle = false;

        /// <summary>One card of the board, in words.</summary>
        public sealed class Card
        {
            /// <summary>The offer's own row id - the occasion the quote is keyed on - and who is asking.</summary>
            public string offerId, speakerId;
            /// <summary>Their full name, as every card and row names a houseguest.</summary>
            public string name;
            /// <summary>The role they come in (<see cref="RoleTone.Role"/>) and the moment it is (<see cref="RoleQuotes.Moments"/>).</summary>
            public string role, moment;
            /// <summary>The deal's kind (<see cref="DealKind"/>), its chip - "Veto Commitment" - and the line under it.</summary>
            public string kind, chip, chipLine;
            /// <summary>The tone (<see cref="RoleTone.Of"/>), the situation word and "Your read: Close".</summary>
            public string tone, situation, read;
            /// <summary>What they say (<see cref="RoleQuotes.Line"/>); null where the catalogue has no line.</summary>
            public string quote;
            /// <summary>"In return: ..." and what keeping and breaking the word does.</summary>
            public string terms, stakes;
            /// <summary>The player's latest settled word with them before this week, or null (<see cref="History"/>): "Your deal with them held in week 2".</summary>
            public string history;
        }

        // ---------------------------------------------------------------- the veto meeting's board

        /// <summary>
        /// This week's veto asks still waiting on the player, from houseguests still in the game - the set the
        /// old rows drew - in the board's public order: nominees in the order they were named, then anybody else
        /// by id, a houseguest's own asks by their ids.
        /// </summary>
        public static List<DealState> VetoAsks(EpisodeState s)
        {
            if (s == null) return new List<DealState>();
            return NpcDeals.Pending(s)
                .Where(d => d.type == DealKind.VetoUse && d.week == s.week && s.Find(d.proposerId)?.status == ContestantStatus.Active)
                .OrderBy(d => BlockPlace(s, d.proposerId))
                .ThenBy(d => d.proposerId, StringComparer.Ordinal)
                .ThenBy(d => d.id, StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>Where a houseguest stands in the order the block was named, or after everybody on it.</summary>
        private static int BlockPlace(EpisodeState s, string id)
        {
            int place = s.nominees?.IndexOf(id) ?? -1;
            return place >= 0 ? place : int.MaxValue;
        }

        /// <summary>The veto meeting's board: a card for each of <see cref="VetoAsks"/>, in its order.</summary>
        public static List<Card> VetoBoard(EpisodeState s) =>
            VetoAsks(s).Select(ask => VetoAskCard(s, ask)).Where(card => card != null).ToList();

        /// <summary>A nominee's veto ask as a card; null for no ask, or one from nobody in the season.</summary>
        public static Card VetoAskCard(EpisodeState s, DealState ask)
        {
            var who = ask == null ? null : s?.Find(ask.proposerId);
            if (who == null || who.isPlayer) return null;
            string role = RoleQuotes.RoleOf(RoleQuotes.VetoAsk), tone = RoleTone.Of(s, who.id, role);
            return new Card
            {
                offerId = ask.id, speakerId = who.id, name = who.name, role = role, moment = RoleQuotes.VetoAsk,
                kind = ask.type, chip = DealKind.Title(ask.type), chipLine = VetoAskLine,
                tone = tone, situation = RoleTone.Situation(s, who.id, role), read = RoleTone.ReadLine(s, who.id),
                quote = RoleQuotes.Line(s, RoleQuotes.VetoAsk, tone, ask.id),
                terms = Terms(s, ask), stakes = Stakes(RoleQuotes.VetoAsk, tone, RidersSettle), history = History(s, who.id),
            };
        }

        // ---------------------------------------------------------------- the lines

        /// <summary>
        /// What the player gets for their yes, from the row: "In return: their vote to keep you, the next time you
        /// are on the block", "In return: a final two" - the price <see cref="Negotiation.AskPriceLine"/> names, in
        /// <see cref="Negotiation.PriceWords"/>' own words - or "In return: nothing promised" where it carries none.
        /// </summary>
        public static string Terms(EpisodeState s, DealState ask)
        {
            var price = s == null ? null : Negotiation.AskPrice(s, ask);
            return TermsPrefix + (price == null ? NothingPromised : Negotiation.PriceWords(s, price));
        }

        /// <summary>
        /// What the word stakes, in at most <see cref="MostStakesWords"/> words: for a nominee's veto ask, that a
        /// kept word is remembered and a broken one may reach the house - and, once <paramref name="ridersSettle"/>,
        /// the tone's rider (<see cref="RoleTone.Stakes"/>) where the moment takes the riders. Null for a moment
        /// without a board yet.
        /// </summary>
        public static string Stakes(string moment, string tone, bool ridersSettle)
        {
            if (moment != RoleQuotes.VetoAsk) return null;
            string rider = ridersSettle && RoleQuotes.TakesTheRiders(moment) ? RoleTone.Stakes(tone)?.rider : null;
            switch (rider)
            {
                case RoleTone.Riders.OwesAFavour: return "Keep it: they owe you a favour. Break it: the house may hear.";
                case RoleTone.Riders.EasesTheirGrudge: return "Keep it: their grudge against you eases. Break it: the house may hear.";
                case RoleTone.Riders.HoldsAGrudge: return "Keep it: they remember it. Refuse or ignore them, leave them up: a grudge.";
                default: return "Keep it: they remember it. Break it: the house may hear.";
            }
        }

        /// <summary>A line's words, as the text caps count them.</summary>
        public static int WordCount(string line) =>
            string.IsNullOrWhiteSpace(line) ? 0 : line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries).Length;

        /// <summary>
        /// The player's own latest settled word with them, settled before this week, as the Your word page has it
        /// (<see cref="CommitmentsRead"/>), and saying no more than the page does about who kept it. A promise is
        /// one-sided, so whose it was is said: "You kept your word to them in week 2", "They kept their word to you
        /// in week 2". A deal binds them both and the page says only that it was honoured - who proposed it is not
        /// who kept it: a nominee's veto ask the player used the veto on is theirs - so a kept one names nobody:
        /// "Your deal with them held in week 2", as an oath that held does. A call asked for their vote: "They
        /// followed your call in week 2", "They ignored your call in week 2". A broken word names who broke it where
        /// the record can say: "You broke your word to them", "They broke their word to you", and otherwise "A word
        /// between you broke". Null with none. A word settled this week waits for the next, so the line never changes
        /// while a card is up; a lapsed or unresolved one is no history.
        /// </summary>
        public static string History(EpisodeState s, string id)
        {
            if (s == null || string.IsNullOrEmpty(id) || string.IsNullOrEmpty(s.playerId)) return null;
            var settled = CommitmentsRead.With(s, id)
                .Select((c, order) => (c, order, week: RoleTone.SettledIn(s, c)))
                .Where(x => (x.c.outcome == CommitmentsRead.Outcomes.Kept || x.c.outcome == CommitmentsRead.Outcomes.Broken)
                            && x.week > 0 && x.week < s.week)
                .OrderByDescending(x => x.week).ThenByDescending(x => x.order)
                .Select(x => (x.c, x.week)).FirstOrDefault();
            var last = settled.c;
            if (last == null) return null;
            string when = " in week " + settled.week;
            bool kept = last.outcome == CommitmentsRead.Outcomes.Kept;
            if (last.kind == CommitmentsRead.Kinds.Call) return (kept ? "They followed your call" : "They ignored your call") + when;
            if (kept)
            {
                if (last.kind == CommitmentsRead.Kinds.Promise) return (last.yours ? "You kept your word to them" : "They kept their word to you") + when;
                return (last.kind == CommitmentsRead.Kinds.Oath ? "Your loyalty oath with them held" : "Your deal with them held") + when;
            }
            if (last.brokenById == s.playerId) return "You broke your word to them" + when;
            if (last.brokenById == id) return "They broke their word to you" + when;
            return "A word between you broke" + when;
        }

        // ---------------------------------------------------------------- the card in words

        /// <summary>The role as a card's heading says it: "nominee", "Head of Household", "veto holder". Null for a role this does not know.</summary>
        public static string RoleWord(string role)
        {
            switch (role)
            {
                case RoleTone.Role.Nominee: return "nominee";
                case RoleTone.Role.HeadOfHousehold: return "Head of Household";
                case RoleTone.Role.VetoHolder: return "veto holder";
                case RoleTone.Role.Courting: return "pitching to you";
                default: return null;
            }
        }

        /// <summary>A card's heading, as a screen reader says it after the caption: "Danielle Reyes, nominee, veto commitment".</summary>
        public static string Heading(Card card) =>
            card == null ? null : card.name + ", " + RoleWord(card.role) + ", " + (card.chip ?? "").ToLowerInvariant();

        /// <summary>
        /// The one line a screen reader says after a card's heading: the tone, the situation, the read, the terms,
        /// the stakes and, on a locked card, why - every one in words, never only the colour or the mark the card
        /// draws them with. "Tone: Desperate. On the block again. Your read: Neutral. In return: a final two. Keep
        /// it: they remember it. Break it: the house may hear."
        /// </summary>
        public static string Description(Card card, string lockReason = null)
        {
            if (card == null) return null;
            var parts = new List<string> { "Tone: " + card.tone, card.situation, card.read, card.terms, card.stakes, lockReason };
            return string.Join(" ", parts.Where(part => !string.IsNullOrWhiteSpace(part)).Select(Sentence));
        }

        /// <summary>What a screen reader says for one of a card's controls: its caption, then the card's heading, then its line.</summary>
        public static string Spoken(string caption, Card card, string lockReason = null) =>
            card == null ? null : Sentence(caption) + " " + Sentence(Heading(card)) + " " + Description(card, lockReason);

        /// <summary>Words as a sentence: trimmed, and closed with a full stop unless they close themselves.</summary>
        private static string Sentence(string words)
        {
            string text = (words ?? "").Trim();
            return text.Length == 0 || text.EndsWith(".", StringComparison.Ordinal) || text.EndsWith("?", StringComparison.Ordinal)
                || text.EndsWith("!", StringComparison.Ordinal) ? text : text + ".";
        }

        /// <summary>
        /// The mark a tone is drawn with beside its word, a shape of its own (§5.4: colour is never the only
        /// signal): Bitter "x", Threatening "^", Desperate "!", Friendly "+", Earnest "*", Calculated "#",
        /// Pragmatic "=". Plain characters every font the HUD draws with has. Null for a tone this does not know.
        /// </summary>
        public static string ToneMark(string tone)
        {
            switch (tone)
            {
                case RoleTone.Bitter: return "x";
                case RoleTone.Threatening: return "^";
                case RoleTone.Desperate: return "!";
                case RoleTone.Friendly: return "+";
                case RoleTone.Earnest: return "*";
                case RoleTone.Calculated: return "#";
                case RoleTone.Pragmatic: return "=";
                default: return null;
            }
        }
    }
}
