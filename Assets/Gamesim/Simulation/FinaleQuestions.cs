using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// Jury questions from the season's history, and the five responses (ENDGAME-PLAN F5b, schema
    /// 21). Under the finale rules each juror's question to a player finalist is built from a
    /// receipt the record holds, by category, and the player answers it with one of five
    /// responses. Old seasons keep the web catalogue's A/B questions (<see cref="WebJuryQuestioning"/>),
    /// which this never touches: the frozen validators read that catalogue.
    ///
    /// <para><b>The draws are today's.</b> A question takes two: the first picks the category
    /// among those the juror cares about that have a receipt, the second the question's wording.
    /// Both are always drawn, whatever the candidates, so the stream stays where it was. The
    /// answer takes the two today's ±10 takes (the reciprocal event and score). The sign takes
    /// none: it is <see cref="Lands"/>, a fixed table of the juror's theme, the category and the
    /// response, so the reaction rebuilt after a reload is the one the engine applied.</para>
    ///
    /// <para><b>Evidence only.</b> Receipts are the public record and the player's own rows:
    /// ceremonies, the player's ballots and whip counts, their promises and deals, the replies
    /// they gave, the calls they made. Never a grudge row, a hidden score or a juror's standing.
    /// The question's words name nobody and read no row, so the validator checks them from the
    /// category alone and a later commit cannot orphan them; the receipt is shown beside them.</para>
    /// </summary>
    public static class FinaleQuestions
    {
        public const string Accountability = "accountability", Ownership = "ownership", JuryManagement = "jury-management",
            Strategy = "strategy", Social = "social", Comparison = "comparison", Mistake = "mistake", Personal = "personal";

        public static readonly string[] Categories = { Accountability, Ownership, JuryManagement, Strategy, Social, Comparison, Mistake, Personal };

        public const string PromiseReceipt = "promise", DealReceipt = "deal", PowerReceipt = "power", BallotReceipt = "ballot",
            ReplyReceipt = "reply", CallReceipt = "call", AllianceReceipt = "alliance";

        public const string Own = "own", Explain = "explain", Loyalty = "loyalty", Deflect = "deflect", Truth = "truth";

        /// <summary>The five responses, in the order they are drawn when no argument orders them.</summary>
        public static readonly string[] Responses = { Own, Explain, Loyalty, Deflect, Truth };

        /// <summary>A response's caption: the control that gives it. Unique on the screen.</summary>
        public static string Caption(string response)
        {
            switch (response)
            {
                case Own: return "Own the move";
                case Explain: return "Explain the strategy";
                case Loyalty: return "Appeal to loyalty";
                case Deflect: return "Deflect";
                case Truth: return "Reveal the truth";
                default: return null;
            }
        }

        /// <summary>A response's risk, in the house events' words: low, medium or high (<see cref="HouseEventRisk"/>).</summary>
        public static string Risk(string response)
        {
            switch (response)
            {
                case Explain: return HouseEventRisk.Low;
                case Own: case Loyalty: return HouseEventRisk.Medium;
                default: return HouseEventRisk.High;
            }
        }

        /// <summary>
        /// The response an argument of this theme puts first on the screen: a read at render, never
        /// saved, and no hint of what lands (ENDGAME-PLAN F4). None without an argument.
        /// </summary>
        public static string FirstFor(string theme)
        {
            switch (theme)
            {
                case FinalArgument.Cerebral: return Explain;
                case FinalArgument.Social: case FinalArgument.Emotional: return Loyalty;
                case FinalArgument.Aggressive: return Own;
                case FinalArgument.Sneaky: return Deflect;
                default: return null;
            }
        }

        /// <summary>The receipts a category takes; a comparison takes none.</summary>
        public static string[] ReceiptKinds(string category)
        {
            switch (category)
            {
                case Accountability: return new[] { PromiseReceipt, DealReceipt };
                case Ownership: return new[] { PowerReceipt, BallotReceipt };
                case JuryManagement: return new[] { ReplyReceipt };
                case Strategy: return new[] { CallReceipt, PowerReceipt };
                case Social: return new[] { PowerReceipt };
                case Mistake: return new[] { BallotReceipt };
                case Personal: return new[] { PromiseReceipt, AllianceReceipt, DealReceipt };
                default: return new string[0];
            }
        }

        /// <summary>The tone a category's question is asked in, for the tone field today's HUD and validator read. No draw.</summary>
        public static string Tone(string category)
        {
            switch (category)
            {
                case Accountability: case Ownership: case JuryManagement: return "bitter";
                case Social: case Personal: return "supportive";
                default: return "neutral";
            }
        }

        /// <summary>What a juror of each theme asks about, most wanted first: the four categories the first draw chooses among.</summary>
        public static string[] Preferences(string theme)
        {
            switch (theme)
            {
                case FinalArgument.Cerebral: return new[] { Strategy, Ownership, Mistake, Accountability };
                case FinalArgument.Social: return new[] { Social, Personal, JuryManagement, Accountability };
                case FinalArgument.Aggressive: return new[] { Ownership, Strategy, Accountability, Social };
                case FinalArgument.Sneaky: return new[] { Mistake, Strategy, JuryManagement, Ownership };
                default: return new[] { Accountability, Personal, JuryManagement, Social };
            }
        }

        /// <summary>The words of a category's questions, the juror speaking. They name nobody, so they never go stale.</summary>
        public static string[] Questions(string category)
        {
            switch (category)
            {
                case Accountability: return new[] {
                    "You gave me your word, and you broke it. Why should I trust you with my vote?",
                    "When it mattered, you went back on what we agreed. How do you justify that?" };
                case Ownership: return new[] {
                    "You are the reason I am sitting on this jury. Tell me why you did it.",
                    "You came after me in that house. Was it personal, or was it the game?" };
                case JuryManagement: return new[] {
                    "I came to you for help and you turned me away. Why should I help you now?",
                    "When I needed you, you gave me nothing. What has changed?" };
                case Strategy: return new[] {
                    "What was the biggest move you made this season, and why did it matter?",
                    "Walk me through the one decision that got you to this chair." };
                case Social: return new[] {
                    "Who kept you safe in that house, and what did you give them for it?",
                    "How did you survive the weeks you were on the block?" };
                case Mistake: return new[] {
                    "What was your biggest mistake this season?",
                    "Where did you misread the house, and what did it cost you?" };
                case Personal: return new[] {
                    "You kept faith with me in there. Was that loyalty, or strategy?",
                    "We had each other's backs. Did that mean anything to you?" };
                default: return new[] {
                    "Why do you deserve this more than the person sitting next to you?",
                    "What did you do in this game that your opponent did not?",
                    "Give me one reason to vote for you over them." };
            }
        }

        /// <summary>What the player says, by category and response: the saved answer.</summary>
        public static string Line(string category, string response)
        {
            switch (category + "/" + response)
            {
                case Accountability + "/" + Own: return "I broke my word to you. It was the game, and I own it.";
                case Accountability + "/" + Explain: return "Keeping it would have put me on the block. I chose to stay in the game.";
                case Accountability + "/" + Loyalty: return "I kept faith with you every week I could. That week I could not.";
                case Accountability + "/" + Deflect: return "Everyone in that house broke a promise. I was not the only one.";
                case Ownership + "/" + Own: return "I came after you. You were a threat to my game, and I would do it again.";
                case Ownership + "/" + Explain: return "You were one of the biggest threats left to me. Taking my shot kept my path open.";
                case Ownership + "/" + Loyalty: return "It was never personal. I respected you enough to see you coming.";
                case Ownership + "/" + Deflect: return "The house made that decision. I only did what they were going to do anyway.";
                case JuryManagement + "/" + Own: return "I turned you down. I could not give you what you asked for, and I told you so.";
                case JuryManagement + "/" + Explain: return "Helping you that week would have cost me my own safety.";
                case JuryManagement + "/" + Loyalty: return "I was honest with you when it would have been easier to lie. That was respect.";
                case JuryManagement + "/" + Deflect: return "I do not remember it the way you do.";
                case Strategy + "/" + Own: return "My biggest move was mine alone. I made it, and it worked.";
                case Strategy + "/" + Explain: return "I counted the votes, moved at the right time, and got the house to follow.";
                case Strategy + "/" + Loyalty: return "My best move was keeping my people safe. Everything else followed from it.";
                case Strategy + "/" + Deflect: return "I would rather let my game speak for itself.";
                case Social + "/" + Own: return "I was on the block and I talked my way off it. That was my game.";
                case Social + "/" + Explain: return "I built enough trust across the house that nobody wanted to be the one to cut me.";
                case Social + "/" + Loyalty: return "The people I stood by stood by me when it counted.";
                case Social + "/" + Deflect: return "I was lucky, and I made the most of it.";
                case Mistake + "/" + Own: return "I misread the house, and it cost me. I learned from it and adjusted.";
                case Mistake + "/" + Explain: return "That vote did not go my way, but it told me exactly where I stood.";
                case Mistake + "/" + Loyalty: return "I trusted the wrong people once. I never stopped trusting the right ones.";
                case Mistake + "/" + Deflect: return "Every game has a bad week. Mine did not stop me getting here.";
                case Personal + "/" + Own: return "I kept my word to you because I wanted to, not because I had to.";
                case Personal + "/" + Explain: return "Keeping faith with you was good for both our games.";
                case Personal + "/" + Loyalty: return "You mattered to me in there, and you still do.";
                case Personal + "/" + Deflect: return "It was a long season. A lot happened between all of us.";
                case Comparison + "/" + Own: return "I made the moves that decided this season. My opponent watched them happen.";
                case Comparison + "/" + Explain: return "Look at the weeks, one by one. My name is on more of them.";
                case Comparison + "/" + Loyalty: return "I kept my relationships through all of it. Ask the people on this jury.";
                case Comparison + "/" + Deflect: return "We both got here. I will let you decide who got here better.";
                case Strategy + "/" + Truth: return "What you never saw was the call I made in my alliance that week. I moved that vote.";
                default: return null;
            }
        }

        /// <summary>
        /// The responses offered for a receipt: the four always, and <see cref="Truth"/> only when
        /// the player holds a fact the juror lacks, a call made in an alliance they were not in.
        /// The receipt is saved with the question, so the set is the same after a reload.
        /// </summary>
        public static string[] Offered(string category, string receiptKind) =>
            category == Strategy && receiptKind == CallReceipt ? Responses : Responses.Take(4).ToArray();

        /// <summary>
        /// Whether a response lands with this juror: a fixed table, read from what was saved. The
        /// truth lands. On a grievance (accountability, ownership) owning the move lands and
        /// deflecting costs. Owning a mistake lands; explaining a strategy lands; loyalty lands on
        /// a personal question. Otherwise what the juror's theme values: a strategist the
        /// explanation, a social player or a loyal one the appeal to loyalty, a competitor owning
        /// it, a schemer the explanation or the deflection.
        /// </summary>
        public static bool Lands(string theme, string category, string response)
        {
            if (response == Truth) return true;
            if (category == Accountability || category == Ownership)
            {
                if (response == Own) return true;
                if (response == Deflect) return false;
            }
            if (category == Mistake && response == Own) return true;
            if (category == Strategy && response == Explain) return true;
            if (category == Personal && response == Loyalty) return true;
            return Values(theme).Contains(response);
        }

        /// <summary>What a juror of this theme values in an answer, whatever they asked: the jury house's "May be swayed by".</summary>
        public static string[] Values(string theme)
        {
            switch (theme)
            {
                case FinalArgument.Cerebral: return new[] { Explain };
                case FinalArgument.Social: return new[] { Loyalty };
                case FinalArgument.Aggressive: return new[] { Own };
                case FinalArgument.Sneaky: return new[] { Explain, Deflect };
                default: return new[] { Loyalty };
            }
        }

        /// <summary>The engine's note under the finale rules, softer than the catalogue's: the one line the reaction shows.</summary>
        public static string Note(string jurorName, bool landed) =>
            jurorName + (landed ? " took your answer well." : " was not moved by your answer.");

        /// <summary>Whether a saved exchange's answer landed, from what was saved: the juror's theme, the category and the response.</summary>
        public static bool Landed(EpisodeState s, JuryExchangeState exchange) =>
            exchange != null && Lands(FinalArgument.ThemeOf(s.Find(exchange.questionerId)), exchange.category, exchange.answerChoice);

        // ------------------------------------------------------------ receipts

        /// <summary>One receipt: its kind, the id that finds its row, and the week it names.</summary>
        public sealed class Receipt
        {
            public string category, kind, id;
            public int week;
        }

        /// <summary>
        /// The receipt each category has for this juror and the player, the latest where there are
        /// several; a category with none is absent. Comparison never has one.
        /// </summary>
        public static List<Receipt> Receipts(EpisodeState s, string jurorId)
        {
            var found = new List<Receipt>();
            if (s == null || jurorId == null) return found;
            string player = s.playerId;
            var ledger = s.ledger ?? new SeasonLedger();
            string W(int week) => week.ToString(CultureInfo.InvariantCulture);
            bool Final(PowerRow p) => p.tally.Count == 0 && p.evicteeId != null && p.vetoHolderId == null;
            void Add(string category, string kind, string id, int week) => found.Add(new Receipt { category = category, kind = kind, id = id, week = week });

            // Accountability: a promise the player broke to them, or a deal between them the player broke.
            var brokenWord = s.promises.Where(p => p.fromId == player && p.toId == jurorId && p.status == PromiseStatus.Broken).OrderBy(p => p.week).LastOrDefault();
            var brokenDeal = s.deals.Where(d => (d.proposerId == jurorId || d.recipientId == jurorId) && FinalistRead.DealBreaker(s, d) == player).OrderBy(d => d.week).LastOrDefault();
            if (brokenWord != null && (brokenDeal == null || brokenWord.week >= brokenDeal.week)) Add(Accountability, PromiseReceipt, brokenWord.id, brokenWord.week);
            else if (brokenDeal != null) Add(Accountability, DealReceipt, brokenDeal.id, brokenDeal.week);

            // Ownership: the player's power put them up or sent them out; else the player's ballot against them.
            var ceremony = ledger.power.Where(p => (p.hohId == player && (FinalistRead.PutUp(p, jurorId) || (Final(p) && p.evicteeId == jurorId)))
                || (p.vetoHolderId == player && p.vetoUsed && p.replacementId == jurorId)).OrderBy(p => p.week).LastOrDefault();
            var ballot = ledger.ballots.Where(b => b.voterId == player && b.targetId == jurorId).OrderBy(b => b.week).LastOrDefault();
            if (ceremony != null) Add(Ownership, PowerReceipt, W(ceremony.week), ceremony.week);
            else if (ballot != null) Add(Ownership, BallotReceipt, W(ballot.week), ballot.week);

            // Jury management: a plea of theirs the player refused. A confrontation or gossip card
            // answered coldly is not them coming for help, and the questions say that they did.
            var reply = ledger.replies.Where(r => r.fromId == jurorId && r.kind == ReplyCards.Plea && r.replyKey == "refuse")
                .OrderBy(r => r.week).LastOrDefault();
            if (reply != null && !string.IsNullOrEmpty(reply.cardId)) Add(JuryManagement, ReplyReceipt, reply.cardId, reply.week);

            // Strategy: a call the player made that this juror never saw (the truth is theirs to reveal); else the player's last week in power.
            // A member of the alliance heard the call whether or not they could vote on it (a Head
            // of Household or a nominee is left off its ballots), so only a juror outside the
            // alliance never saw it.
            var unseen = ledger.calls.Where(c => c.callerId == player && c.callerId != jurorId && !c.followed.Contains(jurorId) && !c.defected.Contains(jurorId)
                    && !s.alliances.Any(a => a.id == c.allianceId && a.members.Contains(jurorId)))
                .OrderBy(c => c.week).LastOrDefault();
            var reign = ledger.power.Where(p => p.hohId == player).OrderBy(p => p.week).LastOrDefault();
            if (unseen != null) Add(Strategy, CallReceipt, unseen.allianceId + ":" + W(unseen.week), unseen.week);
            else if (reign != null) Add(Strategy, PowerReceipt, W(reign.week), reign.week);

            // Social: the last week the player sat on the block and stayed.
            var survived = ledger.power.Where(p => !Final(p) && (p.nominees.Contains(player) || p.savedId == player) && p.evicteeId != null && p.evicteeId != player)
                .OrderBy(p => p.week).LastOrDefault();
            if (survived != null) Add(Social, PowerReceipt, W(survived.week), survived.week);

            // Mistake: a ballot of the player's that misread the house or went against it.
            var miss = ledger.ballots.Where(b => b.voterId == player && ((b.readBefore != null && !b.correct)
                || ledger.power.Any(p => p.week == b.week && p.evicteeId != null && p.evicteeId != b.targetId))).OrderBy(b => b.week).LastOrDefault();
            if (miss != null) Add(Mistake, BallotReceipt, W(miss.week), miss.week);

            // Personal: a promise the player kept to them, an alliance they shared, a deal they kept.
            var keptWord = s.promises.Where(p => p.fromId == player && p.toId == jurorId && p.status == PromiseStatus.Fulfilled).OrderBy(p => p.week).LastOrDefault();
            // An alliance that still stands, or that ended only because the juror left the house -
            // not one that broke, which is no bond to ask about.
            int? leftWeek = JuryHouseRead.LeftWeek(s, jurorId);
            var shared = s.alliances.Where(a => a.members.Contains(player) && a.members.Contains(jurorId)
                    && (a.active || ledger.alliances.Any(r => r.id == a.id && r.why != null && r.why.EndsWith("/left-house", StringComparison.Ordinal)
                        && leftWeek != null && r.endedWeek == leftWeek)))
                .LastOrDefault();
            // Under the commitment rules a deal the vote settled - a partnership too (C1) - is a receipt
            // only once the player knows the ballot that kept it (KnownBallots).
            bool rules = EpisodeEngine.CommitmentRulesOn(s);
            var keptDeal = s.deals.Where(d => d.status == DealStatus.Fulfilled && (d.proposerId == player || d.recipientId == player)
                && (d.proposerId == jurorId || d.recipientId == jurorId) && (!rules || KnownBallots.DealOutcomeKnown(s, d))).OrderBy(d => d.week).LastOrDefault();
            if (keptWord != null) Add(Personal, PromiseReceipt, keptWord.id, keptWord.week);
            else if (shared != null) Add(Personal, AllianceReceipt, shared.id, ledger.alliances.FirstOrDefault(r => r.id == shared.id)?.startedWeek ?? 0);
            else if (keptDeal != null) Add(Personal, DealReceipt, keptDeal.id, keptDeal.week);
            return found;
        }

        /// <summary>
        /// Prepares a juror's question to the player finalist under the finale rules, with exactly
        /// two draws: the category among the juror's preferences that have a receipt (a comparison
        /// when none has), then the question's wording.
        /// </summary>
        public static void Prepare(EpisodeState s, ContestantState juror, int index, JuryExchangeState entry, Func<double> nextRoll)
        {
            if (nextRoll == null) throw new ArgumentNullException(nameof(nextRoll));
            double first = nextRoll(), second = nextRoll();
            var receipts = Receipts(s, juror.id);
            var candidates = Preferences(FinalArgument.ThemeOf(juror)).Where(category => receipts.Any(r => r.category == category)).ToList();
            string category = candidates.Count == 0 ? Comparison : candidates[Math.Min(candidates.Count - 1, (int)Math.Floor(first * candidates.Count))];
            var receipt = receipts.FirstOrDefault(r => r.category == category);
            var questions = QuestionsFor(s, category, receipt, juror.id);
            entry.category = category;
            entry.receiptKind = receipt?.kind;
            entry.receiptId = receipt?.id;
            entry.tone = Tone(category);
            entry.question = questions[Math.Min(questions.Length - 1, (int)Math.Floor(second * questions.Length))];
            entry.optionA = null; entry.optionB = null; entry.correctChoice = null;
            entry.opponentAnswer = WebJuryQuestioning.GetOpponentAnswer(index);
        }

        /// <summary>
        /// The words a question may use, given the receipt it was built from: the category's own,
        /// less any that would claim more than the receipt shows. "You are the reason I am sitting on
        /// this jury" only from the week the juror went home; "You kept faith with me" only for a
        /// promise kept. Every one is the category's, so the validator's check holds.
        /// </summary>
        public static string[] QuestionsFor(EpisodeState s, string category, Receipt receipt, string jurorId)
        {
            var all = Questions(category);
            if (receipt == null) return all;
            if (category == Ownership)
            {
                bool sentHome = s.ledger?.power?.Any(p => p.week == receipt.week && p.evicteeId == jurorId) ?? false;
                return sentHome ? all : new[] { all[1] };
            }
            if (category == Personal && receipt.kind != PromiseReceipt) return new[] { all[1] };
            return all;
        }

        /// <summary>
        /// The receipt as the Season receipt column shows it: the week and what the record holds,
        /// in the player's words. A receipt whose row the record no longer holds says so.
        /// </summary>
        public static string ReceiptLine(EpisodeState s, JuryExchangeState exchange)
        {
            if (exchange == null || exchange.receiptKind == null) return null;
            string player = s.playerId, juror = exchange.questionerId, id = exchange.receiptId;
            var ledger = s.ledger ?? new SeasonLedger();
            string Name(string who) => s.Find(who)?.name ?? "somebody";
            int.TryParse(id, NumberStyles.None, CultureInfo.InvariantCulture, out int week);
            switch (exchange.receiptKind)
            {
                case PromiseReceipt:
                    var promise = s.promises.FirstOrDefault(p => p.id == id);
                    return promise == null ? null : "Week " + promise.week + " · you gave them your word, and "
                        + (promise.status == PromiseStatus.Broken ? "broke it." : promise.status == PromiseStatus.Fulfilled ? "kept it." : "it stands.");
                case DealReceipt:
                    var deal = s.deals.FirstOrDefault(d => d.id == id);
                    return deal == null ? null : "Week " + deal.week + " · your " + DealKind.Title(deal.type).ToLowerInvariant() + ": "
                        + (deal.status == DealStatus.Broken ? "broken." : deal.status == DealStatus.Fulfilled ? "kept." : deal.status + ".");
                case PowerReceipt:
                    var power = ledger.power.FirstOrDefault(p => p.week == week);
                    if (power == null) return null;
                    if (exchange.category == Social) return "Week " + week + " · you sat on the block, and " + Name(power.evicteeId) + " went.";
                    if (power.hohId == player && power.evicteeId == juror) return "Week " + week + " · your week: they left.";
                    if (power.hohId == player && FinalistRead.PutUp(power, juror)) return "Week " + week + " · you put them on the block.";
                    if (power.vetoHolderId == player && power.replacementId == juror) return "Week " + week + " · your veto put them up.";
                    return "Week " + week + " · you held the house, and " + Name(power.evicteeId) + " went.";
                case BallotReceipt:
                    var ballot = ledger.ballots.FirstOrDefault(b => b.week == week && b.voterId == player);
                    if (ballot == null) return null;
                    // The reveal reads the count, not the ballots: the receipt names the player's
                    // ballot only where the juror could know it (UI-UX-PASS-PLAN B0). The question
                    // itself was chosen by the engine, which still reads the box; wave B moves that.
                    if (!JuryHouseRead.CouldKnowYourBallot(s, week, juror))
                        return "Week " + week + " · your ballot that week is yours alone; they cannot know how you voted.";
                    return "Week " + week + " · you voted to evict " + Name(ballot.targetId) + ".";
                case ReplyReceipt:
                    var reply = ledger.replies.FirstOrDefault(r => r.cardId == id);
                    return reply == null ? null : "Week " + reply.week + " · they asked you for your vote, and you said no.";
                case CallReceipt:
                    int at = id.LastIndexOf(':');
                    int.TryParse(at < 0 ? "" : id.Substring(at + 1), NumberStyles.None, CultureInfo.InvariantCulture, out int callWeek);
                    var call = at < 0 ? null : ledger.calls.FirstOrDefault(c => c.allianceId == id.Substring(0, at) && c.week == callWeek);
                    return call == null ? null : "Week " + callWeek + " · you called the vote in your alliance. They were not in it.";
                case AllianceReceipt:
                    var alliance = s.alliances.FirstOrDefault(a => a.id == id);
                    return alliance == null ? null : "You were allies" + (string.IsNullOrEmpty(alliance.name) ? "." : ", in " + alliance.name + ".");
                default:
                    return null;
            }
        }

        // ------------------------------------------------------------ the receipt, read for the screen

        /// <summary>
        /// The week a question's receipt names, or null: a comparison has none, and neither has a
        /// row the record no longer holds or an alliance whose start it never wrote. A read at
        /// render, never saved (MOCKUP-PASS M11).
        /// </summary>
        public static int? ReceiptWeek(EpisodeState s, JuryExchangeState exchange)
        {
            if (s == null || exchange == null || exchange.receiptKind == null || ReceiptLine(s, exchange) == null) return null;
            string id = exchange.receiptId;
            var ledger = s.ledger ?? new SeasonLedger();
            switch (exchange.receiptKind)
            {
                case PromiseReceipt: return s.promises.First(p => p.id == id).week;
                case DealReceipt: return s.deals.First(d => d.id == id).week;
                case PowerReceipt:
                case BallotReceipt: return ParsedWeek(id);
                case ReplyReceipt: return ledger.replies.First(r => r.cardId == id).week;
                case CallReceipt: return ParsedWeek(id.Substring(id.LastIndexOf(':') + 1));
                case AllianceReceipt:
                    int started = ledger.alliances.FirstOrDefault(r => r.id == id)?.startedWeek ?? 0;
                    return started > 0 ? started : (int?)null;
                default: return null;
            }
        }

        private static int? ParsedWeek(string words) =>
            int.TryParse(words, NumberStyles.None, CultureInfo.InvariantCulture, out int week) && week > 0 ? week : (int?)null;

        /// <summary>
        /// The receipt in a few words, for the line over the question it was asked from ("Week 4 ·
        /// you voted to evict them"; decision 39). The juror asking is "them". The question's saved
        /// words are never touched: this is drawn above them. Null with no receipt, or with a row
        /// the record no longer holds.
        ///
        /// <para>A promise or a deal is dated by the week it was made, and its words say so. It is
        /// kept or broken later - a final two at the final eviction, a safety promise at a
        /// nomination - and the record holds no week for that, so the week never stands on the
        /// break or the keeping. <see cref="ReceiptLine"/> words it the same way.</para>
        /// </summary>
        public static string Kicker(EpisodeState s, JuryExchangeState exchange)
        {
            if (s == null || exchange == null || exchange.receiptKind == null || ReceiptLine(s, exchange) == null) return null;
            string player = s.playerId, juror = exchange.questionerId, id = exchange.receiptId;
            var ledger = s.ledger ?? new SeasonLedger();
            int? week = ReceiptWeek(s, exchange);
            string words;
            switch (exchange.receiptKind)
            {
                case PromiseReceipt:
                    var promise = s.promises.First(p => p.id == id);
                    words = "you gave them your word" + (promise.status == PromiseStatus.Broken ? ", and broke it"
                        : promise.status == PromiseStatus.Fulfilled ? ", and kept it" : "");
                    break;
                case DealReceipt:
                    var deal = s.deals.First(d => d.id == id);
                    words = "your " + DealKind.Title(deal.type).ToLowerInvariant() + " with them"
                        + (deal.status == DealStatus.Broken ? ", broken" : deal.status == DealStatus.Fulfilled ? ", kept" : "");
                    break;
                case PowerReceipt:
                    if (week == null) return null;
                    var power = ledger.power.First(p => p.week == week.Value);
                    bool final = power.tally.Count == 0 && power.evicteeId != null && power.vetoHolderId == null;
                    words = exchange.category == Social ? "you sat on the block and stayed"
                        : power.hohId == player && power.evicteeId == juror ? (final ? "you sent them to the jury" : "they left in your week")
                        : power.hohId == player && FinalistRead.PutUp(power, juror) ? "you put them on the block"
                        : power.vetoHolderId == player && power.replacementId == juror ? "your veto put them up"
                        : "you held the house";
                    break;
                case BallotReceipt:
                    if (week == null) return null;
                    var ballot = ledger.ballots.First(b => b.week == week.Value && b.voterId == player);
                    words = !JuryHouseRead.CouldKnowYourBallot(s, week.Value, juror) ? "a ballot they cannot know"
                        : "you voted to evict " + (ballot.targetId == juror ? "them" : s.Find(ballot.targetId)?.name ?? "somebody");
                    break;
                case ReplyReceipt: words = "you turned down their plea"; break;
                case CallReceipt: words = "you called the vote in your alliance"; break;
                case AllianceReceipt: words = "you were allies"; break;
                default: return null;
            }
            return week == null ? char.ToUpperInvariant(words[0]) + words.Substring(1) : "Week " + week.Value + " · " + words;
        }

        /// <summary>
        /// Whether a receipt is about its week's vote: the power, a ballot, a call, or a plea -
        /// a nominee asking for the player's vote, which the house offers only while nominees
        /// campaign and clears when campaigning closes, so its week is the vote's. A promise, a
        /// deal or an alliance is dated by the week it began, and that week's vote is not its.
        /// </summary>
        private static bool AboutTheWeeksVote(string receiptKind) =>
            receiptKind == PowerReceipt || receiptKind == BallotReceipt || receiptKind == CallReceipt || receiptKind == ReplyReceipt;

        /// <summary>
        /// The count of the vote in the receipt's week, the evictee's first, as the reveal read it:
        /// "(5–2)". Only for a receipt about that week's vote (<see cref="AboutTheWeeksVote"/>),
        /// and only when the week had a count: the final eviction is a choice, not a vote.
        /// </summary>
        public static string ReceiptTally(EpisodeState s, JuryExchangeState exchange)
        {
            if (exchange == null || !AboutTheWeeksVote(exchange.receiptKind)) return null;
            int? week = ReceiptWeek(s, exchange);
            var power = week == null ? null : s.ledger?.power?.FirstOrDefault(p => p.week == week.Value);
            if (power?.tally == null || power.tally.Count < 2) return null;
            return "(" + string.Join("–", power.tally.OrderByDescending(n => n)) + ")";
        }

        /// <summary>
        /// Whether the receipt's line already says who left in its week: the block the player sat
        /// on and who went, the juror leaving in the player's week, the house the player held and
        /// who went, the player's ballot against the one who went. The week's recap headline adds
        /// nothing under such a line (MOCKUP-PASS M11, review correction 19).
        /// </summary>
        public static bool ReceiptSaysWhoWent(EpisodeState s, JuryExchangeState exchange)
        {
            int? week = ReceiptWeek(s, exchange);
            var power = week == null ? null : s.ledger?.power?.FirstOrDefault(p => p.week == week.Value);
            if (power == null || power.evicteeId == null) return false;
            string player = s.playerId, juror = exchange.questionerId;
            switch (exchange.receiptKind)
            {
                case PowerReceipt:
                    // As ReceiptLine words it: only the nomination and the veto lines leave out who went.
                    if (exchange.category == Social || (power.hohId == player && power.evicteeId == juror)) return true;
                    if (power.hohId == player && FinalistRead.PutUp(power, juror)) return false;
                    if (power.vetoHolderId == player && power.replacementId == juror) return false;
                    return true;
                case BallotReceipt:
                    var ballot = s.ledger.ballots.FirstOrDefault(b => b.week == week.Value && b.voterId == player);
                    return ballot != null && ballot.targetId == power.evicteeId;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Whether the week's recap headline adds to the receipt, drawn under it: only under a
        /// receipt about that week's vote whose line does not already say who went (review
        /// correction 19). Under a promise, a deal or an alliance the week is when it began, and who
        /// went that week, set right under the receipt, would read as part of it.
        /// </summary>
        public static bool RecapAdds(EpisodeState s, JuryExchangeState exchange) =>
            exchange != null && AboutTheWeeksVote(exchange.receiptKind) && ReceiptWeek(s, exchange) != null && !ReceiptSaysWhoWent(s, exchange);
    }
}
