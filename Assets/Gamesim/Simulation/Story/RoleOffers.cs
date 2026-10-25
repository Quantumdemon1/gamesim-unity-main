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
    /// waiting on the player, from houseguests still in the game - and under the role moments the Head of
    /// Household's ask to keep the block (R4, §3.3), whose card stands after the nominees': the Head of
    /// Household is on no block.</para>
    ///
    /// <para><b>Only what is true now.</b> Every word is a public fact, the player's own reading or record,
    /// or the row's own terms (§5.3). The stakes line says what keeping and breaking a word does: a kept word
    /// is remembered, a broken one may reach the house (<see cref="DealResolution"/>), and the tone's rider
    /// where it has one, which the veto decision settles (R3, <see cref="RidersSettle"/>). The answers line
    /// says what an honest no and silence cost with them, in words, and the board's head that silence costs
    /// more than a no (<see cref="SilenceLine"/>): the engine charges both, under the role moments, from the
    /// tone the card shows (<see cref="ToneOf"/>).</para>
    ///
    /// <para>Pure and read-only: it neither mutates the state nor draws from its generator, and lives in the
    /// simulation so the Unity-free subset tests it. The HUD draws it (<c>EpisodeHud.OfferBoard</c>), at the
    /// veto meeting and in the Diary Room's veto column, under <see cref="EpisodeEngine.RolesOn"/> only - and, from R5,
    /// the Head of Household's pitches as the same board (<see cref="PitchBoard"/>, §3.1).</para>
    /// </summary>
    public static class RoleOffers
    {
        /// <summary>What a nominee's veto ask asks, under its chip ("Veto Commitment").</summary>
        public const string VetoAskLine = "Save me from the block with the Power of Veto";

        /// <summary>
        /// The Head of Household's ask to keep the block (R4, §3.3): its chip, and what it asks under it. Its chip is not the
        /// deal's title ("Keep-the-Block Deal"), which the record's lines use, but what the card asks in the show's words.
        /// </summary>
        public const string KeepTheBlockChip = "Don't Use the Veto", KeepTheBlockLine = "Keep the nominations the same";

        /// <summary>The front of every terms line: "In return: a final two".</summary>
        public const string TermsPrefix = "In return: ";

        /// <summary>The terms of an ask that carries no price: nothing is struck with the player's yes.</summary>
        public const string NothingPromised = "nothing promised";

        /// <summary>The most words a stakes line may have (§5.1's text caps).</summary>
        public const int MostStakesWords = 14;

        /// <summary>
        /// Whether a tone's rider settles (<see cref="RoleTone.Riders"/>). It does since R3, which settles them at
        /// the veto decision: a Desperate nominee whose word was kept owes the player a favour, a Bitter one's
        /// grudge eases, and a Threatening one turned down or left hanging, and left on the block, holds a grudge.
        /// Until then the stakes line said only what keeping and breaking did.
        /// </summary>
        public const bool RidersSettle = true;

        /// <summary>
        /// The board's word on what answering costs, beside its line (§5.1): true under the role moments for every
        /// tone, whose lapse costs more than its no (<see cref="RoleTone.Stakes"/>).
        /// </summary>
        public const string SilenceLine = "Silence costs more than a no.";

        /// <summary>One card of the board, in words.</summary>
        public sealed class Card
        {
            /// <summary>The offer's own row id, and who is asking.</summary>
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
            /// <summary>What turning it down and leaving it unanswered cost with them, in words (<see cref="Answers"/>).</summary>
            public string answers;
            /// <summary>The player's latest settled word with them before this week, or null (<see cref="History"/>): "Your deal with them held in week 2".</summary>
            public string history;
            /// <summary>
            /// A pitch's recommendation (R5): "Recommends: Maya Torres", their own pick for the block, as the pitch card always
            /// showed it; null on every other card, and on a pitch that recommends nobody.
            /// </summary>
            public string recommends;
            /// <summary>What feeling a pitcher out learned, once the player has (<see cref="HoHPitches.Assessment"/>); null otherwise, and on every other card.</summary>
            public string assessment;
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
            return InBoardOrder(s, NpcDeals.Pending(s).Where(d => IsVetoAsk(s, d) && s.Find(d.proposerId)?.status == ContestantStatus.Active)).ToList();
        }

        /// <summary>
        /// Every ask the board shows (R4): the nominees' veto asks (<see cref="VetoAsks"/>) and the Head of Household's ask
        /// to keep the block (<see cref="IsKeepTheBlockAsk"/>), still waiting on the player, from houseguests still in the
        /// game, in the board's public order - the nominees in the order they were named, then the Head of Household.
        /// </summary>
        public static List<DealState> BoardAsks(EpisodeState s)
        {
            if (s == null) return new List<DealState>();
            return InBoardOrder(s, NpcDeals.Pending(s).Where(d => IsBoardAsk(s, d) && s.Find(d.proposerId)?.status == ContestantStatus.Active)).ToList();
        }

        /// <summary>Asks in the board's public order: nominees in the order they were named, then anybody else by id, a houseguest's own by their ids.</summary>
        public static IEnumerable<DealState> InBoardOrder(EpisodeState s, IEnumerable<DealState> asks) =>
            asks.OrderBy(d => BlockPlace(s, d.proposerId))
                .ThenBy(d => d.proposerId, StringComparer.Ordinal)
                .ThenBy(d => d.id, StringComparer.Ordinal);

        /// <summary>
        /// Whether a deal is one of this week's veto asks, answered or not: a veto commitment a nominee put to the
        /// player this week, as <see cref="NpcDeals.AskForTheVeto"/> files one (<see cref="NpcDeals.VetoAskPrefix"/>).
        /// The board draws a card for each while it waits (<see cref="VetoAsks"/>), and under the role moments the
        /// engine charges a card's stakes on it - its no as it is given, its silence and its rider at the veto
        /// decision (R3). Never a word a houseguest gave the player on a veto they hold - a favour called in, a
        /// story's - which nobody asked the player to answer, and which was never a card (R3's review).
        /// </summary>
        public static bool IsVetoAsk(EpisodeState s, DealState d) =>
            s != null && d != null && d.type == DealKind.VetoUse && d.week == s.week && d.recipientId == s.playerId && d.proposerId != s.playerId
            && d.id != null && d.id.StartsWith(NpcDeals.VetoAskPrefix, StringComparison.Ordinal);

        /// <summary>
        /// Whether a deal is this week's ask from the Head of Household to keep the block, answered or not (R4, §3.3): a
        /// keep-the-block deal put to the player this week, as <see cref="NpcDeals.AskToKeepTheBlock"/> files one - keyed on
        /// its id (<see cref="NpcDeals.KeepTheBlockPrefix"/>), never on the kind alone, so a keep-the-block deal anything else
        /// wrote - a story's - is no card and settles nothing on the board.
        /// </summary>
        public static bool IsKeepTheBlockAsk(EpisodeState s, DealState d) =>
            s != null && d != null && d.type == DealKind.VetoHold && d.week == s.week && d.recipientId == s.playerId && d.proposerId != s.playerId
            && NpcDeals.IsKeepTheBlockId(d.id);

        /// <summary>Whether a deal is a card of the veto board: a nominee's veto ask, or the Head of Household's ask to keep the block.</summary>
        public static bool IsBoardAsk(EpisodeState s, DealState d) => IsVetoAsk(s, d) || IsKeepTheBlockAsk(s, d);

        /// <summary>The moment an ask on the board is (<see cref="RoleQuotes.Moments"/>): the Head of Household's keep-the-block, or a nominee's veto ask.</summary>
        public static string MomentOf(DealState ask) => ask?.type == DealKind.VetoHold ? RoleQuotes.KeepTheBlock : RoleQuotes.VetoAsk;

        /// <summary>
        /// The tone an ask's card wears: the asker's, in the role their moment gives them (<see cref="RoleTone.Of"/>) - a
        /// nominee, or the Head of Household. The card says it and the engine charges its stakes from it - a no from the
        /// state it is given in, silence and the rider from the board as it stood when the decision was made - so the cost
        /// the player is shown is the cost they are charged.
        /// </summary>
        public static string ToneOf(EpisodeState s, DealState ask) =>
            ask == null ? null : RoleTone.Of(s, ask.proposerId, RoleQuotes.RoleOf(MomentOf(ask)));

        /// <summary>
        /// The occasion an ask's quote is keyed on (<see cref="RoleQuotes.Line"/>): the moment, the week and who is asking,
        /// every one public. Not the row's id, which R1 and R2 keyed it on (R2's review left the choice to R3): ids follow
        /// the order the asks were filed in, and <see cref="NpcDeals.AskForTheVeto"/> files the warmest first - how each
        /// nominee reads the player, which the player cannot see - so the same two nominees asking, their warmth the other
        /// way round, swapped the ids and with them both cards' lines. A nominee asks once a week, and so does the Head of
        /// Household, whose occasion is their moment's (R4), so the moment, the week and the asker name the occasion; a
        /// reload, a re-render and a replay show the same line, and nothing is stored.
        /// </summary>
        public static string QuoteOccasion(DealState ask) =>
            ask == null || string.IsNullOrEmpty(ask.proposerId) ? null : MomentOf(ask) + ":w" + ask.week + ":" + ask.proposerId;

        /// <summary>Where a houseguest stands in the order the block was named, or after everybody on it.</summary>
        private static int BlockPlace(EpisodeState s, string id)
        {
            int place = s.nominees?.IndexOf(id) ?? -1;
            return place >= 0 ? place : int.MaxValue;
        }

        /// <summary>The veto meeting's board: a card for each of <see cref="BoardAsks"/>, in its order - the nominees', then the Head of Household's.</summary>
        public static List<Card> VetoBoard(EpisodeState s) =>
            BoardAsks(s).Select(ask => CardOf(s, ask)).Where(card => card != null).ToList();

        /// <summary>An ask on the board as its card: the Head of Household's keep-the-block (<see cref="KeepTheBlockCard"/>), or a nominee's veto ask.</summary>
        public static Card CardOf(EpisodeState s, DealState ask) =>
            ask?.type == DealKind.VetoHold ? KeepTheBlockCard(s, ask) : VetoAskCard(s, ask);

        /// <summary>
        /// The Head of Household's ask to keep the block as a card (R4, §3.3, §5.1): their face under the crown, "Don't Use the
        /// Veto" and "Keep the nominations the same", their tone as Head of Household with its situation word, the player's
        /// read, their line, the terms - "In return: a final two" at the endgame, or "In return: nothing promised" - what
        /// keeping it and using the veto anyway do, what a no and silence cost, and the history. Null for no ask, or one from
        /// nobody in the season.
        /// </summary>
        public static Card KeepTheBlockCard(EpisodeState s, DealState ask)
        {
            var who = ask == null ? null : s?.Find(ask.proposerId);
            if (who == null || who.isPlayer) return null;
            string moment = RoleQuotes.KeepTheBlock, role = RoleQuotes.RoleOf(moment), tone = ToneOf(s, ask);
            return new Card
            {
                offerId = ask.id, speakerId = who.id, name = who.name, role = role, moment = moment,
                kind = ask.type, chip = KeepTheBlockChip, chipLine = KeepTheBlockLine,
                tone = tone, situation = RoleTone.Situation(s, who.id, role), read = RoleTone.ReadLine(s, who.id),
                quote = RoleQuotes.Line(s, moment, tone, QuoteOccasion(ask)),
                terms = Terms(s, ask), stakes = Stakes(moment, tone, RidersSettle),
                answers = Answers(moment, tone), history = History(s, who.id),
            };
        }

        /// <summary>A nominee's veto ask as a card; null for no ask, or one from nobody in the season.</summary>
        public static Card VetoAskCard(EpisodeState s, DealState ask)
        {
            var who = ask == null ? null : s?.Find(ask.proposerId);
            if (who == null || who.isPlayer) return null;
            string role = RoleQuotes.RoleOf(RoleQuotes.VetoAsk), tone = ToneOf(s, ask);
            return new Card
            {
                offerId = ask.id, speakerId = who.id, name = who.name, role = role, moment = RoleQuotes.VetoAsk,
                kind = ask.type, chip = DealKind.Title(ask.type), chipLine = VetoAskLine,
                tone = tone, situation = RoleTone.Situation(s, who.id, role), read = RoleTone.ReadLine(s, who.id),
                quote = RoleQuotes.Line(s, RoleQuotes.VetoAsk, tone, QuoteOccasion(ask)),
                terms = Terms(s, ask), stakes = Stakes(RoleQuotes.VetoAsk, tone, RidersSettle),
                answers = Answers(RoleQuotes.VetoAsk, tone), history = History(s, who.id),
            };
        }

        // ---------------------------------------------------------------- the Head of Household's pitch board (R5, §3.1)

        /// <summary>A pitch's chip, and what it asks under it.</summary>
        public const string PitchChip = "Nomination Pitch", PitchLine = "Keep me off the block";

        /// <summary>The front of a pitch's recommendation: "Recommends: Maya Torres".</summary>
        public const string RecommendsPrefix = "Recommends: ";

        /// <summary>
        /// The pitches waiting on the player as Head of Household, in the board's order: the order the board was dealt in
        /// (<see cref="HoHPitches.HoldTheBoard"/>) - the two the player read warmest, then the one they read coolest, by their own
        /// reading as the houseguests came to them - which stays put while the board is up, so an answer, or a word with one of
        /// them, never shuffles the cards under the player. Only pitches from houseguests still in the game.
        /// </summary>
        public static List<ReplyCardState> PitchBoardCards(EpisodeState s) =>
            s?.replyCards == null ? new List<ReplyCardState>()
                : s.replyCards.Where(c => c != null && c.kind == ReplyCards.Pitch && s.Find(c.fromId)?.status == ContestantStatus.Active).ToList();

        /// <summary>
        /// Whether the Head of Household's pitches are drawn as the board: the role moments' pitch board
        /// (<see cref="HoHPitches.OnTheBoard"/>) - the player Head of Household before naming anybody - with a pitch waiting
        /// (<see cref="PitchBoardCards"/>). Without it the pitches come one at a time, as they always did. The director's gate
        /// (<c>EpisodeDirector.PitchBoardShows</c>) reads it, so the Unity-free subset tests it.
        /// </summary>
        public static bool PitchBoardShows(EpisodeState s) => HoHPitches.OnTheBoard(s) && PitchBoardCards(s).Count > 0;

        /// <summary>The pitch board's cards in words, in its order (<see cref="PitchBoardCards"/>).</summary>
        public static List<Card> PitchBoard(EpisodeState s) =>
            PitchBoardCards(s).Select(card => PitchCard(s, card)).Where(card => card != null).ToList();

        /// <summary>
        /// A pitch as a card (R5, §3.1): their face, "Nomination Pitch" and "Keep me off the block", their tone as one courting
        /// the Head of Household with its situation word ("Never nominated", "Nominated before", or the endgame), the player's
        /// read, their line - its target their own recommendation, keyed on the card's id, which names the week and the speaker
        /// - "Recommends: {name}", what the answers stake and what a no and silence cost, the history, and what feeling them out
        /// learned once the player has. Null for no card, a card of another kind, or one from nobody in the season.
        /// </summary>
        public static Card PitchCard(EpisodeState s, ReplyCardState card)
        {
            var who = card == null || card.kind != ReplyCards.Pitch ? null : s?.Find(card.fromId);
            if (who == null || who.isPlayer) return null;
            string moment = RoleQuotes.Pitch, role = RoleQuotes.RoleOf(moment), tone = RoleTone.Of(s, who.id, role);
            return new Card
            {
                offerId = card.id, speakerId = who.id, name = who.name, role = role, moment = moment,
                kind = ReplyCards.Pitch, chip = PitchChip, chipLine = PitchLine,
                tone = tone, situation = RoleTone.Situation(s, who.id, role), read = RoleTone.ReadLine(s, who.id),
                quote = RoleQuotes.Line(s, moment, tone, card.id, card.aboutId),
                recommends = Recommends(s, card), stakes = PitchStakes(tone), answers = PitchAnswers(tone),
                history = History(s, who.id), assessment = HoHPitches.Assessment(s, card),
            };
        }

        /// <summary>"Recommends: Maya Torres": the houseguest the pitch puts forward for the block, by full name; null for one that names nobody.</summary>
        public static string Recommends(EpisodeState s, ReplyCardState card)
        {
            var target = string.IsNullOrEmpty(card?.aboutId) ? null : s?.Find(card.aboutId);
            return target == null ? null : RecommendsPrefix + target.name;
        }

        /// <summary>
        /// What a pitch's answers stake, in at most <see cref="MostStakesWords"/> words and never a number: a promise pays only if
        /// it is kept - they stay off the player's block (<see cref="HoHPitches.KeptOffTheBlock"/>) - and a no softens the blow of
        /// a nomination (<see cref="HoHPitches.HonestNoEase"/>), except for a Threatening one, whom it still stings. One line of
        /// the pitch card at the standard text (R5b): "Promise: pays if kept. No: a nomination stings less." The board's own
        /// words, a no and silence, as its veto head says them (<see cref="SilenceLine"/>).
        /// </summary>
        public static string PitchStakes(string tone) => tone == RoleTone.Threatening
            ? "Promise: pays if kept. No: a nomination still stings."
            : "Promise: pays if kept. No: a nomination stings less.";

        /// <summary>
        /// What turning a pitcher down and leaving them unanswered cost with them, in words: their tone's honest no
        /// (<see cref="RoleTone.Stakes"/>), and nothing for silence - an unanswered pitch clears at the nominations. One line of
        /// the pitch card at the standard text (R5b): "No: costs some goodwill. Silence: costs nothing." Null for a tone this
        /// does not know.
        /// </summary>
        public static string PitchAnswers(string tone)
        {
            var stake = RoleTone.Stakes(tone);
            return stake == null ? null : "No: costs " + CostWords(stake.decline) + ". Silence: costs nothing.";
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
        /// the tone's rider (<see cref="RoleTone.Stakes"/>) where the moment takes the riders. For the Head of
        /// Household's ask to keep the block (R4, §3.3), that a kept word is remembered, or eases a Bitter one's grudge;
        /// that a broken one - taken, then the veto used - may reach the house, as a nominee's may; and that the veto
        /// used anyway without it - turned down or left hanging - they take personally, a Threatening one the more. Null
        /// for a moment without a board yet.
        /// </summary>
        public static string Stakes(string moment, string tone, bool ridersSettle)
        {
            if (moment != RoleQuotes.VetoAsk && moment != RoleQuotes.KeepTheBlock) return null;
            string rider = ridersSettle && RoleQuotes.TakesTheRiders(moment) ? RoleTone.Stakes(tone)?.rider : null;
            if (moment == RoleQuotes.KeepTheBlock)
                switch (rider)
                {
                    case RoleTone.Riders.EasesTheirGrudge: return "Keep it: their grudge eases. Break it: the house may hear. Veto anyway: personal.";
                    case RoleTone.Riders.HoldsAGrudge: return "Keep it: they remember. Break it: the house may hear. Veto anyway: very personal.";
                    default: return "Keep it: they remember it. Break it: the house may hear. Veto anyway: personal.";
                }
            switch (rider)
            {
                case RoleTone.Riders.OwesAFavour: return "Keep it: they owe you a favour. Break it: the house may hear.";
                case RoleTone.Riders.EasesTheirGrudge: return "Keep it: their grudge against you eases. Break it: the house may hear.";
                case RoleTone.Riders.HoldsAGrudge: return "Keep it: they remember it. Refuse or ignore them, leave them up: a grudge.";
                default: return "Keep it: they remember it. Break it: the house may hear.";
            }
        }

        /// <summary>
        /// What an honest no and silence cost with them, in at most <see cref="MostStakesWords"/> words and never a number
        /// (§3.0, "What the tone stakes"): "Turn it down: costs a little goodwill. Leave it unanswered: costs some goodwill."
        /// Silence costs more than a no for every tone, in these words too. The Head of Household's ask to keep the block
        /// (R4) costs its tone's too. Null for a moment without a board yet, or a tone this does not know.
        /// </summary>
        public static string Answers(string moment, string tone)
        {
            var stake = moment == RoleQuotes.VetoAsk || moment == RoleQuotes.KeepTheBlock ? RoleTone.Stakes(tone) : null;
            return stake == null ? null
                : "Turn it down: costs " + CostWords(stake.decline) + ". Leave it unanswered: costs " + CostWords(stake.lapse) + ".";
        }

        /// <summary>A cost with somebody as a card says it: nothing, a little goodwill (one), some (up to three), a lot (more).</summary>
        public static string CostWords(double delta) =>
            delta >= 0 ? "nothing" : delta >= -1 ? "a little goodwill" : delta >= -3 ? "some goodwill" : "a lot of goodwill";

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
        /// the stakes, what a no and silence cost and, on a locked card, why - every one in words, never only the
        /// colour or the mark the card draws them with. "Tone: Desperate. On the block again. Your read: Neutral.
        /// In return: a final two. Keep it: they owe you a favour. Break it: the house may hear. Turn it down: costs
        /// some goodwill. Leave it unanswered: costs a lot of goodwill."
        /// </summary>
        public static string Description(Card card, string lockReason = null)
        {
            if (card == null) return null;
            var parts = new List<string> { "Tone: " + card.tone, card.situation, card.read, card.recommends, card.terms, card.stakes, card.answers,
                card.assessment, lockReason };
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
