// UNWIRED fixed28 semantic source transplant. No runtime/current Simulation dependencies.
// All source roots are the immutable 515246bf/status61 schema28 image.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Gamesim.Persistence.Frozen28Data;

namespace Gamesim.Persistence.Frozen28ReadOnly
{
    internal static class FinalArgument
    {
        // SOURCE FIELD Gamesim.Simulation.FinalArgument.Themes @ Assets/Gamesim/Simulation/FinalArgument.cs:2324
        public static readonly string[] Themes = { Cerebral, Social, Aggressive, Sneaky, Emotional };

        // SOURCE FIELD Gamesim.Simulation.FinalArgument.MomentCount @ Assets/Gamesim/Simulation/FinalArgument.cs:2661
        public const int MomentCount = 3;

        // SOURCE FIELD Gamesim.Simulation.FinalArgument.Cerebral,Social,Aggressive,Sneaky,Emotional @ Assets/Gamesim/Simulation/FinalArgument.cs:2097
        public const string Cerebral = "cerebral", Social = "social", Aggressive = "aggressive", Sneaky = "sneaky", Emotional = "emotional";

        // SOURCE Gamesim.Simulation.FinalArgument.Resolves 21742:23945
        public static bool Resolves(EpisodeState s, string reference)
                {
                    if (s == null || string.IsNullOrEmpty(reference) || reference.Length > 200) return false;
                    var ledger = s.ledger ?? LiteralRows.NewSeasonLedger();
                    string player = s.playerId;
                    var parts = reference.Split(':');
                    bool Week(string text, out int week) => int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out week);
                    switch (parts[0])
                    {
                        case "win": return parts.Length == 3 && Week(parts[1], out int w1) && ledger.competitions.Any(r => r.week == w1 && r.kind == parts[2] && r.placement == 1);
                        case "hoh": return parts.Length == 2 && Week(parts[1], out int w2) && ledger.power.Any(p => p.week == w2 && p.hohId == player);
                        case "veto": return parts.Length == 2 && Week(parts[1], out int w3) && ledger.power.Any(p => p.week == w3 && p.vetoHolderId == player);
                        case "block": return parts.Length == 2 && Week(parts[1], out int w4) && ledger.power.Any(p => p.week == w4);
                        case "whip": return parts.Length == 2 && Week(parts[1], out int w5) && ledger.ballots.Any(b => b.week == w5 && b.voterId == player);
                        case "call":
                            int at = reference.LastIndexOf(':');
                            string allianceId = reference.Substring(parts[0].Length + 1, Math.Max(0, at - parts[0].Length - 1));
                            return at > parts[0].Length && Week(reference.Substring(at + 1), out int w6) && ledger.calls.Any(c => c.week == w6 && c.allianceId == allianceId);
                        case "promise": return CommitmentReferences.Promises(s).Any(x => "promise:" + x.id == reference && x.fromId == player);
                        case "deal": return CommitmentReferences.Deals(s).Any(x => "deal:" + x.id == reference && (x.proposerId == player || x.recipientId == player));
                        case "alliance": return s.alliances.Any(a => "alliance:" + a.id == reference && a.members.Contains(player));
                        case "record": return reference == "record:unnominated" || reference == "record:off-the-block";
                        default: return false;
                    }
                }
    }

    internal static class FinaleQuestions
    {
        // SOURCE FIELD Gamesim.Simulation.FinaleQuestions.Accountability,Ownership,JuryManagement,Strategy,Social,Comparison,Mistake,Personal @ Assets/Gamesim/Simulation/FinaleQuestions.cs:1764
        public const string Accountability = "accountability", Ownership = "ownership", JuryManagement = "jury-management",
                    Strategy = "strategy", Social = "social", Comparison = "comparison", Mistake = "mistake", Personal = "personal";

        // SOURCE FIELD Gamesim.Simulation.FinaleQuestions.Categories @ Assets/Gamesim/Simulation/FinaleQuestions.cs:2014
        public static readonly string[] Categories = { Accountability, Ownership, JuryManagement, Strategy, Social, Comparison, Mistake, Personal };

        // SOURCE FIELD Gamesim.Simulation.FinaleQuestions.PromiseReceipt,DealReceipt,PowerReceipt,BallotReceipt,ReplyReceipt,CallReceipt,AllianceReceipt @ Assets/Gamesim/Simulation/FinaleQuestions.cs:2164
        public const string PromiseReceipt = "promise", DealReceipt = "deal", PowerReceipt = "power", BallotReceipt = "ballot",
                    ReplyReceipt = "reply", CallReceipt = "call", AllianceReceipt = "alliance";

        // SOURCE FIELD Gamesim.Simulation.FinaleQuestions.Own,Explain,Loyalty,Deflect,Truth @ Assets/Gamesim/Simulation/FinaleQuestions.cs:2381
        public const string Own = "own", Explain = "explain", Loyalty = "loyalty", Deflect = "deflect", Truth = "truth";

        // SOURCE FIELD Gamesim.Simulation.FinaleQuestions.Responses @ Assets/Gamesim/Simulation/FinaleQuestions.cs:2612
        public static readonly string[] Responses = { Own, Explain, Loyalty, Deflect, Truth };

        // SOURCE Gamesim.Simulation.FinaleQuestions.CanonicalReceiptBelongsToPlayerAndJuror 31972:32637
        private static bool CanonicalReceiptBelongsToPlayerAndJuror(EpisodeState s, string id, string juror, string policy)
                {
                    if (!UnifiedCommitments.SafetyAuthorityOn(s)) return true;
                    var row = CommitmentReferences.FindCanonical(s, id);
                    if (row == null) return true;
                    if (row.sourcePolicy != policy) return false;
                    return policy == UnifiedCommitments.PromisePolicy
                        ? row.makerId == s.playerId && row.beneficiaryId == juror
                        : (row.makerId == s.playerId && row.beneficiaryId == juror)
                            || (row.makerId == juror && row.beneficiaryId == s.playerId);
                }

        // SOURCE Gamesim.Simulation.FinaleQuestions.Line 8632:12857
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

        // SOURCE Gamesim.Simulation.FinaleQuestions.Offered 13201:13377
        public static string[] Offered(string category, string receiptKind) =>
                    category == Strategy && receiptKind == CallReceipt ? Responses : Responses.Take(4).ToArray();

        // SOURCE Gamesim.Simulation.FinaleQuestions.ParsedWeek 34220:34395
        private static int? ParsedWeek(string words) =>
                    int.TryParse(words, NumberStyles.None, CultureInfo.InvariantCulture, out int week) && week > 0 ? week : (int?)null;

        // SOURCE Gamesim.Simulation.FinaleQuestions.Questions 6547:8525
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

        // SOURCE Gamesim.Simulation.FinaleQuestions.QuestionsFor 26484:27057
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

        // SOURCE Gamesim.Simulation.FinaleQuestions.ReceiptKinds 4449:5146
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

        // SOURCE Gamesim.Simulation.FinaleQuestions.ReceiptLine 27302:31652
        public static string ReceiptLine(EpisodeState s, JuryExchangeState exchange)
                {
                    if (exchange == null || exchange.receiptKind == null) return null;
                    string player = s.playerId, juror = exchange.questionerId, id = exchange.receiptId;
                    var ledger = s.ledger ?? LiteralRows.NewSeasonLedger();
                    string Name(string who) => s.Find(who)?.name ?? "somebody";
                    int.TryParse(id, NumberStyles.None, CultureInfo.InvariantCulture, out int week);
                    switch (exchange.receiptKind)
                    {
                        case PromiseReceipt:
                            if (!CanonicalReceiptBelongsToPlayerAndJuror(s, id, juror, UnifiedCommitments.PromisePolicy)) return null;
                            var promise = CommitmentReferences.FindPromise(s, id);
                            return promise == null ? null : "Week " + CommitmentReferences.ReceiptWeek(s, id, promise.week) + " · you gave them your word, and "
                                + (promise.status == PromiseStatus.Broken ? "broke it." : promise.status == PromiseStatus.Fulfilled ? "kept it." : "it stands.");
                        case DealReceipt:
                            if (!CanonicalReceiptBelongsToPlayerAndJuror(s, id, juror, UnifiedCommitments.DealPolicy)) return null;
                            var deal = CommitmentReferences.FindDeal(s, id);
                            return deal == null ? null : "Week " + CommitmentReferences.ReceiptWeek(s, id, deal.week) + " · your " + DealKind.Title(deal.type).ToLowerInvariant() + ": "
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

        // SOURCE Gamesim.Simulation.FinaleQuestions.ReceiptWeek 33044:34210
        public static int? ReceiptWeek(EpisodeState s, JuryExchangeState exchange)
                {
                    if (s == null || exchange == null || exchange.receiptKind == null || ReceiptLine(s, exchange) == null) return null;
                    string id = exchange.receiptId;
                    var ledger = s.ledger ?? LiteralRows.NewSeasonLedger();
                    switch (exchange.receiptKind)
                    {
                        case PromiseReceipt: return CommitmentReferences.ReceiptWeek(s, id, CommitmentReferences.FindPromise(s, id).week);
                        case DealReceipt: return CommitmentReferences.ReceiptWeek(s, id, CommitmentReferences.FindDeal(s, id).week);
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

        // SOURCE Gamesim.Simulation.FinaleQuestions.Tone 5291:5610
        public static string Tone(string category)
                {
                    switch (category)
                    {
                        case Accountability: case Ownership: case JuryManagement: return "bitter";
                        case Social: case Personal: return "supportive";
                        default: return "neutral";
                    }
                }

        internal sealed class Receipt
        {
            // SOURCE FIELD Gamesim.Simulation.FinaleQuestions.Receipt.category,kind,id @ Assets/Gamesim/Simulation/FinaleQuestions.cs:16099
            public string category, kind, id;
        
            // SOURCE FIELD Gamesim.Simulation.FinaleQuestions.Receipt.week @ Assets/Gamesim/Simulation/FinaleQuestions.cs:16145
            public int week;
        
            // Explicit spelling of the source implicit parameterless constructor.
            public Receipt() { }
        }
        
    }

    internal static class FinalistRead
    {
        // SOURCE Gamesim.Simulation.FinalistRead.DealBreaker 28460:34211
        public static string DealBreaker(EpisodeState s, DealState deal)
                {
                    string player = s.playerId;
                    if (deal == null || deal.status != DealStatus.Broken || (deal.proposerId != player && deal.recipientId != player)) return null;
                    if (UnifiedCommitments.RulesOn(s))
                    {
                        // Canonical safety's actual actor is durable authority. The capped power ledger
                        // may no longer contain its nomination, and must not overwrite that attribution.
                        var canonical = CommitmentReferences.FindCanonical(s, deal.id);
                        if (canonical != null && canonical.sourcePolicy == UnifiedCommitments.DealPolicy
                            && canonical.status == DealStatus.Broken) return canonical.brokenById;
                    }
                    if (s.unifiedCommitmentRulesVersion == UnifiedVoteFamilyValidation.Version)
                    {
                        var canonical = CommitmentReferences.FindCanonical(s, deal.id);
                        if (canonical != null && canonical.sourcePolicy == UnifiedCommitments.DealPolicy
                            && canonical.status == DealStatus.Broken)
                        {
                            if (canonical.kind == UnifiedCommitments.Safety) return canonical.brokenById;
                            if (canonical.kind == UnifiedVoteTogether.Vote)
                            {
                                // A Together break still has no sole public breaker. Targeted duties
                                // use their actual first decision, never an earlier created-week ballot.
                                if (canonical.subtype == DealKind.VoteTogether) return null;
                                var decision = UnifiedVoteHistory.FindDecision(s, canonical.id);
                                if (decision == null) return null;
                                var own = decision.Reveal.ballots.FirstOrDefault(b => b.voterId == player);
                                bool mine = own != null && (canonical.subtype == DealKind.VoteEvict
                                    ? own.targetId != canonical.targetId : own.targetId == canonical.targetId);
                                if (mine) return player;
                                string partner = canonical.makerId == player ? canonical.beneficiaryId : canonical.makerId;
                                return decision.ActorId == partner && KnownBallots.Knows(s, decision.SettledWeek, partner)
                                    ? partner : null;
                            }
                        }
                    }
                    string other = deal.proposerId == player ? deal.recipientId : deal.proposerId;
                    var rows = s.ledger?.power ?? new List<PowerRow>();
                    bool Final(PowerRow p) => p.tally.Count == 0 && p.evicteeId != null && p.vetoHolderId == null;
                    int last = deal.expiresWeek >= deal.week ? deal.expiresWeek : int.MaxValue;
                    var term = rows.Where(p => p.week >= deal.week && p.week <= last && !Final(p)).OrderBy(p => p.week).ToList();
                    switch (deal.type)
                    {
                        case DealKind.SafetyAgreement:
                        case DealKind.TargetAgreement:
                            foreach (var p in term)
                            {
                                if (p.hohId == other && PutUp(p, player)) return other;
                                if (p.hohId == player && PutUp(p, other)) return player;
                            }
                            return null;
                        case DealKind.VetoUse:
                            foreach (var p in term)
                            {
                                if (p.vetoHolderId == other && LeftOnTheBlock(p, player)) return other;
                                if (p.vetoHolderId == player && LeftOnTheBlock(p, other)) return player;
                            }
                            return null;
                        case DealKind.VoteSave:
                        case DealKind.VoteEvict:
                            foreach (var p in term.Where(p => p.tally.Count > 0 && p.nominees.Contains(deal.targetId)))
                            {
                                var ballot = s.ledger.ballots.FirstOrDefault(b => b.week == p.week && b.voterId == player);
                                bool mine = ballot != null && (deal.type == DealKind.VoteEvict ? ballot.targetId != deal.targetId : ballot.targetId == deal.targetId);
                                if (mine) return player;
                                return KnownBallots.Knows(s, p.week, other) ? other : null;
                            }
                            return null;
                        case DealKind.FinalTwo:
                            var chose = rows.Where(Final).OrderBy(p => p.week).LastOrDefault();
                            return chose != null && (chose.hohId == player || chose.hohId == other) ? chose.hohId : null;
                        case DealKind.Partnership:
                            // Only the vote settles one, under the commitment rules (ACTIONS-DEALS-ALLIANCES-PLAN
                            // C1), and the record says who: the player's own ballot, known to them, or the
                            // other's, told once the player knows it (KnownBallots). Before them none broke.
                            return deal.settledWeek > 0 && KnownBallots.DealOutcomeKnown(s, deal) ? deal.brokenById : null;
                        case DealKind.InformationSharing:
                            // Only a lie the reveal caught breaks one, under the commitment rules (C1), and the
                            // player was told that lie and the ballot it hid. Before them none broke.
                            return deal.settledWeek > 0 ? deal.brokenById : null;
                        case DealKind.FinalThree:
                            // The commitment rules' own (C9), so always on the record: broken only by a nomination,
                            // in front of the house.
                            return deal.settledWeek > 0 ? deal.brokenById : null;
                        default:
                            return null;
                    }
                }

        // SOURCE Gamesim.Simulation.FinalistRead.LeftOnTheBlock 42017:42147
        internal static bool LeftOnTheBlock(PowerRow p, string id) => p.nominees.Contains(id) && p.replacementId != id && p.savedId != id;

        // SOURCE Gamesim.Simulation.FinalistRead.PutUp 41771:41892
        internal static bool PutUp(PowerRow p, string id) => p.nominees.Contains(id) || p.savedId == id || p.replacementId == id;
    }

    internal static class JuryHouseRead
    {
        // SOURCE Gamesim.Simulation.JuryHouseRead.CouldKnowYourBallot 23411:23572
        public static bool CouldKnowYourBallot(EpisodeState s, int week, string jurorId) =>
                    KnownBallots.ProvenFor(s, week, jurorId).ContainsKey(s.playerId);

        // SOURCE Gamesim.Simulation.JuryHouseRead.LeftWeek 17411:17552
        public static int? LeftWeek(EpisodeState s, string jurorId) =>
                    s.ledger?.power?.LastOrDefault(p => p.evicteeId == jurorId)?.week;
    }

    internal static class KnownBallots
    {
        // SOURCE FIELD Gamesim.Simulation.KnownBallots.Evicts,TieBreakPrefix,OathBreach,OathByVote @ Assets/Gamesim/Simulation/KnownBallots.cs:21342
        private const string Evicts = " voted to evict ", TieBreakPrefix = "HoH tie-break: ", OathBreach = " broke their loyalty oath to ",
                    OathByVote = " by voting to evict them!";

        // SOURCE FIELD Gamesim.Simulation.KnownBallots.PlayerReason @ Assets/Gamesim/Simulation/KnownBallots.cs:21644
        public const string PlayerReason = "Player's decision";

        // SOURCE FIELD Gamesim.Simulation.KnownBallots.OathBreachByVoteAgainstYou @ Assets/Gamesim/Simulation/KnownBallots.cs:25221
        public const string OathBreachByVoteAgainstYou = "Broke loyalty oath by voting to evict you in week ";

        // SOURCE Gamesim.Simulation.KnownBallots.BasisOf 26979:27148
        private static string BasisOf(string source) =>
                    source == ClaimSource.Told ? Basis.Told : source == ClaimSource.Overheard ? Basis.Overheard : Basis.Reported;

        // SOURCE Gamesim.Simulation.KnownBallots.DealOutcomeKnown 29032:30417
        public static bool DealOutcomeKnown(EpisodeState s, DealState d)
                {
                    if (s == null || d == null || !SettledByABallot(d)) return true;
                    if (d.status != DealStatus.Fulfilled && d.status != DealStatus.Broken) return true;
                    string player = s.playerId;
                    if (d.proposerId != player && d.recipientId != player) return true;
                    string partner = d.proposerId == player ? d.recipientId : d.proposerId;
                    int week = DealSettledWeek(s, d, partner);
                    if (week == 0) return false;
                    var sheet = Read(s, week);
                    // A partner who cast no ballot that week - the Head of Household with no tie to break,
                    // a nominee - had nothing the player needs to learn: the player's own ballot settled it.
                    if (!sheet.voters.Contains(partner) && !(sheet.tieBroken && sheet.hohId == partner)) return true;
                    if (d.type != DealKind.VoteTogether && d.targetId != null)
                    {
                        string own = sheet.TargetOf(player);
                        bool evictedTarget = own == d.targetId;
                        // The player's own ballot broke it: known without the partner's.
                        if (own != null && (d.type == DealKind.VoteEvict ? !evictedTarget : evictedTarget) && d.status == DealStatus.Broken) return true;
                    }
                    return sheet.Knows(partner);
                }

        // SOURCE Gamesim.Simulation.KnownBallots.DealSettledWeek 32645:33991
        public static int DealSettledWeek(EpisodeState s, DealState d, string partnerId)
                {
                    if (s == null || d == null) return 0;
                    if (s.unifiedCommitmentRulesVersion == UnifiedVoteFamilyValidation.Version)
                    {
                        var canonical = CommitmentReferences.FindCanonical(s, d.id);
                        if (canonical != null && canonical.kind == UnifiedVoteTogether.Vote
                            && canonical.sourcePolicy == UnifiedCommitments.DealPolicy)
                            return canonical.settledWeek;
                    }
                    if (d.type == DealKind.Partnership) return Math.Max(0, d.settledWeek);
                    int last = d.expiresWeek >= d.week && d.expiresWeek > 0 ? d.expiresWeek : s.week;
                    foreach (int week in Weeks(s))
                    {
                        if (week < d.week || week > last) continue;
                        var frame = Frame.Of(s, week);
                        if (!frame.revealed) continue;
                        if (d.type == DealKind.VoteTogether)
                        {
                            if (Voted(frame, d.proposerId) && Voted(frame, d.recipientId)) return week;
                        }
                        else if (d.targetId != null && frame.nominees.Contains(d.targetId)
                            && (Voted(frame, d.proposerId) || Voted(frame, d.recipientId))) return week;
                    }
                    return 0;
                }

        // SOURCE Gamesim.Simulation.KnownBallots.IsVoteDeal 27385:27532
        public static bool IsVoteDeal(string type) =>
                    type == DealKind.VoteTogether || type == DealKind.VoteSave || type == DealKind.VoteEvict;

        // SOURCE Gamesim.Simulation.KnownBallots.Knows 16256:16355
        public static bool Knows(EpisodeState s, int week, string voterId) => Read(s, week).Knows(voterId);

        // SOURCE Gamesim.Simulation.KnownBallots.OathBreaches 25755:26407
        private static IEnumerable<Ballot> OathBreaches(EpisodeState s, int week)
                {
                    string reason = OathBreachByVoteAgainstYou + week;
                    foreach (var arc in s.relationshipArcs ?? new List<RelationshipArcState>())
                    {
                        if (arc?.weeklyHistory == null || string.IsNullOrEmpty(arc.npcId) || arc.npcId == s.playerId || s.Find(arc.npcId) == null) continue;
                        if (arc.weeklyHistory.Any(entry => entry != null && entry.week == week && entry.reason == reason))
                            yield return new Ballot { voterId = arc.npcId, targetId = s.playerId, basis = Basis.Revealed };
                    }
                }

        // SOURCE Gamesim.Simulation.KnownBallots.Prove 19805:21229
        private static Dictionary<string, string> Prove(Frame frame, IEnumerable<(string voterId, string targetId)> placed)
                {
                    var proven = new Dictionary<string, string>(StringComparer.Ordinal);
                    if (!frame.revealed || frame.nominees.Count != 2 || frame.tally.Count != 2 || frame.tally.Sum() <= 0) return proven;
                    var left = frame.tally.ToArray();
                    var remaining = new List<string>(frame.voters);
                    foreach (var (voterId, targetId) in placed)
                    {
                        if (voterId == frame.hohId || !remaining.Remove(voterId)) continue;
                        int at = frame.nominees.IndexOf(targetId);
                        if (at < 0) return proven;
                        left[at]--;
                    }
                    if (left.Any(n => n < 0) || remaining.Count == 0) return proven;
                    int whole = frame.tally[0] == 0 ? 1 : frame.tally[1] == 0 ? 0 : -1;
                    int rest = left[0] == 0 ? 1 : left[1] == 0 ? 0 : -1;
                    if (whole >= 0)
                    {
                        // Nobody voted the other way: every voter the record names went against this one.
                        foreach (var id in remaining) proven[id] = frame.nominees[whole];
                    }
                    else if (rest >= 0 && frame.exact && remaining.Count == left[rest])
                    {
                        foreach (var id in remaining) proven[id] = frame.nominees[rest];
                    }
                    return proven;
                }

        // SOURCE Gamesim.Simulation.KnownBallots.ProvenFor 18104:19306
        public static Dictionary<string, string> ProvenFor(EpisodeState s, int week, string viewerId)
                {
                    var placed = new Dictionary<string, string>(StringComparer.Ordinal);
                    if (s == null || week < 1 || string.IsNullOrEmpty(viewerId) || s.Find(viewerId) == null) return placed;
                    var frame = Frame.Of(s, week);
                    if (frame.final || frame.pending || !frame.revealed) return placed;
        
                    string own = viewerId == s.playerId
                        ? (frame.live ? frame.box.FirstOrDefault(v => v.voterId == viewerId)?.targetId
                            : s.ledger?.ballots?.LastOrDefault(b => b.week == week && b.voterId == viewerId)?.targetId)
                        : frame.live ? frame.box.FirstOrDefault(v => v.voterId == viewerId)?.targetId : null;
                    if (own != null) placed[viewerId] = own;
                    if (frame.tieBroken && frame.hohId != null && frame.evictedId != null) placed[frame.hohId] = frame.evictedId;
                    foreach (var pair in Prove(frame, placed.Where(p => p.Key != frame.hohId).Select(p => (p.Key, p.Value)).ToList()))
                        if (!placed.ContainsKey(pair.Key)) placed[pair.Key] = pair.Value;
                    return placed;
                }

        // SOURCE Gamesim.Simulation.KnownBallots.PublicLines 26691:26969
        private static IEnumerable<EpisodeEvent> PublicLines(EpisodeState s, int week) =>
                    s.events.Where(e => e != null && e.week == week && (e.kind == "vote-reveal" || e.kind == "loyalty_oath_broken")
                        && (e.audienceIds == null || e.audienceIds.Count == 0));

        // SOURCE Gamesim.Simulation.KnownBallots.Read 11063:16109
        public static Sheet Read(EpisodeState s, int week)
                {
                    var sheet = new Sheet { week = week };
                    if (s == null || week < 1 || string.IsNullOrEmpty(s.playerId) || s.Find(s.playerId) == null) return sheet;
                    var frame = Frame.Of(s, week);
                    sheet.final = frame.final;
                    sheet.pending = frame.pending;
                    sheet.nominees = new List<string>(frame.nominees);
                    sheet.evictedId = frame.evictedId;
                    sheet.hohId = frame.hohId;
                    sheet.tally = new List<int>(frame.tally);
                    sheet.tieBroken = frame.tieBroken;
                    sheet.voters = new List<string>(frame.voters);
                    if (frame.final) return sheet;
        
                    var known = new Dictionary<string, Ballot>(StringComparer.Ordinal);
                    void Place(Ballot b)
                    {
                        if (b == null || string.IsNullOrEmpty(b.voterId) || string.IsNullOrEmpty(b.targetId)) return;
                        if (!known.TryGetValue(b.voterId, out var have) || Basis.Rank(b.basis) < Basis.Rank(have.basis)) known[b.voterId] = b;
                    }
        
                    // The player's own ballot: the ledger's row outlives the box; the box is the live week's record.
                    string own = frame.live ? frame.box.FirstOrDefault(v => v.voterId == s.playerId)?.targetId
                        : s.ledger?.ballots?.LastOrDefault(b => b.week == week && b.voterId == s.playerId)?.targetId;
                    if (own != null)
                    {
                        bool deciding = s.playerId == frame.hohId;
                        Place(new Ballot { voterId = s.playerId, targetId = own, basis = deciding ? Basis.TieBreak : Basis.Own });
                    }
                    if (frame.pending)
                    {
                        sheet.ballots.AddRange(known.Values);
                        return sheet;
                    }
        
                    // The Head of Household's deciding vote is read live: public (decision 1).
                    if (frame.tieBroken && frame.hohId != null && frame.evictedId != null)
                        Place(new Ballot { voterId = frame.hohId, targetId = frame.evictedId, basis = Basis.TieBreak });
        
                    // Lines the house heard read.
                    foreach (var line in PublicLines(s, week))
                    {
                        var read = ReadRevealLine(s, line.text, frame.hohId) ?? ReadOathLine(s, line);
                        if (read == null) continue;
                        if (read.voterId == s.playerId) read.basis = s.playerId == frame.hohId ? Basis.TieBreak : Basis.Own;
                        else if (read.voterId == frame.hohId) read.basis = Basis.TieBreak;
                        Place(read);
                    }
                    // An oath's breach by a vote against the player was announced to the house and is on the
                    // player's own arc with the breaker in the rule's words: known from the announcement,
                    // and still known once the log has rolled past the line.
                    foreach (var breach in OathBreaches(s, week))
                        if (breach.voterId != frame.hohId) Place(breach);
        
                    // What the player was told, overheard or had reported, judged at the reveal.
                    foreach (var claim in (s.ledger?.claims ?? new List<ClaimRow>()).Where(k => k != null && k.week == week && k.status != ClaimStatus.Open))
                    {
                        if (claim.voterId == null || claim.targetId == null || claim.voterId == frame.hohId) continue;
                        string target = claim.status == ClaimStatus.Kept ? claim.targetId
                            : frame.nominees.Count == 2 && frame.nominees.Contains(claim.targetId) ? frame.nominees.First(id => id != claim.targetId) : null;
                        if (target == null) continue;
                        Place(new Ballot { voterId = claim.voterId, targetId = target, basis = BasisOf(claim.source), saidId = claim.targetId, verdict = claim.status });
                    }
        
                    // The count's proof, from what is certain (decision 3).
                    foreach (var pair in Prove(frame, known.Values.Where(b => b.Certain).Select(b => (b.voterId, b.targetId))))
                        if (!known.ContainsKey(pair.Key)) known[pair.Key] = new Ballot { voterId = pair.Key, targetId = pair.Value, basis = Basis.Proven };
        
                    // Known first: the player's own, the tie-break, then the cast's order; then the slots.
                    var order = new List<string>();
                    if (known.ContainsKey(s.playerId)) order.Add(s.playerId);
                    if (frame.hohId != null && known.ContainsKey(frame.hohId) && !order.Contains(frame.hohId)) order.Add(frame.hohId);
                    foreach (var actor in s.contestants) if (known.ContainsKey(actor.id) && !order.Contains(actor.id)) order.Add(actor.id);
                    foreach (var id in known.Keys) if (!order.Contains(id)) order.Add(id);
                    foreach (var id in order) sheet.ballots.Add(known[id]);
        
                    int houseKnown = known.Values.Count(b => b.voterId != frame.hohId);
                    int cast = frame.exact ? frame.voters.Count : frame.tally.Sum();
                    for (int slot = houseKnown; slot < cast; slot++) sheet.ballots.Add(new Ballot());
                    return sheet;
                }

        // SOURCE Gamesim.Simulation.KnownBallots.ReadOathLine 24150:24984
        private static Ballot ReadOathLine(EpisodeState s, EpisodeEvent line)
                {
                    if (line?.text == null || line.kind != "loyalty_oath_broken" || !line.text.EndsWith(OathByVote, StringComparison.Ordinal)) return null;
                    var breaker = Subject(s, line.text);
                    if (breaker == null) return null;
                    string rest = line.text.Substring(breaker.name.Length);
                    if (!rest.StartsWith(OathBreach, StringComparison.Ordinal)) return null;
                    string victimName = rest.Substring(OathBreach.Length, rest.Length - OathBreach.Length - OathByVote.Length);
                    var victim = s.contestants.FirstOrDefault(c => c.name == victimName);
                    if (victim == null) return null;
                    return new Ballot { voterId = breaker.id, targetId = victim.id, basis = Basis.Revealed };
                }

        // SOURCE Gamesim.Simulation.KnownBallots.ReadRevealLine 22228:23986
        public static Ballot ReadRevealLine(EpisodeState s, string text, string hohId = null)
                {
                    if (s == null || string.IsNullOrEmpty(text)) return null;
                    var voter = Subject(s, text);
                    if (voter == null || !text.Substring(voter.name.Length).StartsWith(Evicts, StringComparison.Ordinal)) return null;
                    string rest = text.Substring(voter.name.Length + Evicts.Length);
                    var player = s.Find(s.playerId);
                    ContestantState target;
                    if (rest.StartsWith("you.", StringComparison.Ordinal) || rest.StartsWith("yourself.", StringComparison.Ordinal))
                    {
                        target = player;
                        rest = rest.Substring(rest.IndexOf('.') + 1);
                    }
                    else
                    {
                        target = s.contestants.Where(c => !string.IsNullOrEmpty(c.name) && rest.StartsWith(c.name + ".", StringComparison.Ordinal))
                            .OrderByDescending(c => c.name.Length).FirstOrDefault();
                        if (target == null) return null;
                        rest = rest.Substring(target.name.Length + 1);
                    }
                    string reason = rest.Trim();
                    bool tieBreak = reason.StartsWith(TieBreakPrefix, StringComparison.Ordinal) || (hohId != null && voter.id == hohId);
                    if (reason.StartsWith(TieBreakPrefix, StringComparison.Ordinal)) reason = reason.Substring(TieBreakPrefix.Length).Trim();
                    if (reason == PlayerReason || reason.Length == 0) reason = null;
                    return new Ballot
                    {
                        voterId = voter.id, targetId = target?.id, reason = reason,
                        basis = voter.id == s.playerId && !tieBreak ? Basis.Own : tieBreak ? Basis.TieBreak : Basis.Revealed,
                    };
                }

        // SOURCE Gamesim.Simulation.KnownBallots.SettledByABallot 28324:28479
        public static bool SettledByABallot(DealState d) =>
                    d != null && (IsVoteDeal(d.type) || (d.type == DealKind.Partnership && d.settledWeek > 0));

        // SOURCE Gamesim.Simulation.KnownBallots.Subject 26417:26681
        private static ContestantState Subject(EpisodeState s, string line) =>
                    s.contestants.Where(c => !string.IsNullOrEmpty(c.name) && line.StartsWith(c.name, StringComparison.Ordinal))
                        .OrderByDescending(c => c.name.Length).FirstOrDefault();

        // SOURCE Gamesim.Simulation.KnownBallots.Voted 34258:34407
        private static bool Voted(Frame frame, string id) =>
                    id != null && (frame.voters.Contains(id) || (frame.tieBroken && frame.hohId == id));

        // SOURCE Gamesim.Simulation.KnownBallots.Weeks 16859:17447
        public static List<int> Weeks(EpisodeState s)
                {
                    var weeks = new SortedSet<int>();
                    if (s == null) return weeks.ToList();
                    foreach (var p in s.ledger?.power ?? new List<PowerRow>())
                        if (p != null && p.tally != null && p.tally.Count == 2 && p.evicteeId != null) weeks.Add(p.week);
                    foreach (var k in s.ledger?.claims ?? new List<ClaimRow>())
                        if (k != null && k.status != ClaimStatus.Open) weeks.Add(k.week);
                    if (Frame.LiveBox(s)) weeks.Add(s.week);
                    return weeks.ToList();
                }

        internal sealed class Ballot
        {
            // SOURCE FIELD Gamesim.Simulation.KnownBallots.Ballot.voterId @ Assets/Gamesim/Simulation/KnownBallots.cs:5126
            public string voterId;
        
            // SOURCE FIELD Gamesim.Simulation.KnownBallots.Ballot.targetId @ Assets/Gamesim/Simulation/KnownBallots.cs:5286
            public string targetId;
        
            // SOURCE FIELD Gamesim.Simulation.KnownBallots.Ballot.basis @ Assets/Gamesim/Simulation/KnownBallots.cs:5385
            public string basis = Basis.Unknown;
        
            // SOURCE FIELD Gamesim.Simulation.KnownBallots.Ballot.saidId @ Assets/Gamesim/Simulation/KnownBallots.cs:5555
            public string saidId;
        
            // SOURCE FIELD Gamesim.Simulation.KnownBallots.Ballot.verdict @ Assets/Gamesim/Simulation/KnownBallots.cs:5722
            public string verdict;
        
            // SOURCE FIELD Gamesim.Simulation.KnownBallots.Ballot.reason @ Assets/Gamesim/Simulation/KnownBallots.cs:5886
            public string reason;
        
            // Explicit spelling of the source implicit parameterless constructor.
            public Ballot() { }
        
            // SOURCE Gamesim.Simulation.KnownBallots.Ballot.get_Certain 6199:6285
            public bool Certain => Known && basis != Basis.Unknown && verdict != ClaimStatus.Open;
        
            // SOURCE Gamesim.Simulation.KnownBallots.Ballot.get_Known 6005:6043
            public bool Known => targetId != null;
        }
        

        internal static class Basis
        {
            // SOURCE FIELD Gamesim.Simulation.KnownBallots.Basis.All @ Assets/Gamesim/Simulation/KnownBallots.cs:3741
            public static readonly string[] All = { Own, TieBreak, Revealed, Told, Overheard, Reported, Proven, Unknown };
        
            // SOURCE FIELD Gamesim.Simulation.KnownBallots.Basis.Own,TieBreak,Revealed,Proven,Told,Overheard,Reported,Unknown @ Assets/Gamesim/Simulation/KnownBallots.cs:3409
            public const string Own = "own", TieBreak = "tie-break", Revealed = "revealed", Proven = "proven",
                            Told = "told", Overheard = "overheard", Reported = "reported", Unknown = "unknown";
        
            // SOURCE Gamesim.Simulation.KnownBallots.Basis.Rank 4692:4859
            internal static int Rank(string basis)
                        {
                            int at = Array.IndexOf(All, basis);
                            return at < 0 ? All.Length : at;
                        }
        }
        

        internal sealed class Frame
        {
            // SOURCE FIELD Gamesim.Simulation.KnownBallots.Frame.final,pending,revealed,live,exact,tieBroken @ Assets/Gamesim/Simulation/KnownBallots.cs:41740
            public bool final, pending, revealed, live, exact, tieBroken;
        
            // SOURCE FIELD Gamesim.Simulation.KnownBallots.Frame.hohId,evictedId @ Assets/Gamesim/Simulation/KnownBallots.cs:41814
            public string hohId, evictedId;
        
            // SOURCE FIELD Gamesim.Simulation.KnownBallots.Frame.nominees,voters @ Assets/Gamesim/Simulation/KnownBallots.cs:41858
            public List<string> nominees = new List<string>(), voters = new List<string>();
        
            // SOURCE FIELD Gamesim.Simulation.KnownBallots.Frame.tally @ Assets/Gamesim/Simulation/KnownBallots.cs:41950
            public List<int> tally = new List<int>();
        
            // SOURCE FIELD Gamesim.Simulation.KnownBallots.Frame.box @ Assets/Gamesim/Simulation/KnownBallots.cs:42004
            public List<VoteState> box = new List<VoteState>();
        
            // Explicit spelling of the source implicit parameterless constructor.
            public Frame() { }
        
            // SOURCE Gamesim.Simulation.KnownBallots.Frame.LiveBox 42252:42463
            public static bool LiveBox(EpisodeState s) =>
                            s.votes != null && s.votes.Count > 0
                            && (s.phase == EpisodePhase.Eviction || (s.phase == EpisodePhase.Social && s.evictionResolved));
        
            // SOURCE Gamesim.Simulation.KnownBallots.Frame.Of 42477:44825
            public static Frame Of(EpisodeState s, int week)
                        {
                            var frame = new Frame();
                            var power = s.ledger?.power?.LastOrDefault(p => p != null && p.week == week);
                            if (power != null && (power.tally == null || power.tally.Count == 0) && power.evicteeId != null && power.vetoHolderId == null)
                            {
                                frame.final = true;
                                return frame;
                            }
                            frame.live = week == s.week && LiveBox(s);
                            if (frame.live) frame.box = s.votes.Where(v => v != null && v.voterId != null).ToList();
                            frame.hohId = power?.hohId ?? (week == s.week ? s.hohId : null);
                            frame.nominees = power != null && power.nominees != null && power.nominees.Count > 0 ? new List<string>(power.nominees)
                                : week == s.week ? new List<string>(s.nominees ?? new List<string>()) : new List<string>();
                            frame.evictedId = power?.evicteeId;
                            bool counted = power != null && power.tally != null && power.tally.Count == 2 && frame.nominees.Count == 2;
                            bool boxed = frame.live && s.evictionResolved && frame.nominees.Count == 2;
                            if (!counted && !boxed)
                            {
                                frame.pending = week == s.week && s.phase == EpisodePhase.Eviction && !s.evictionResolved;
                                return frame;
                            }
                            frame.revealed = true;
                            frame.tally = counted ? new List<int>(power.tally)
                                : frame.nominees.Select(id => frame.box.Count(v => v.targetId == id && v.voterId != frame.hohId)).ToList();
                            frame.tieBroken = frame.tally[0] == frame.tally[1];
                            if (frame.evictedId == null && frame.live)
                                frame.evictedId = frame.tieBroken ? frame.box.FirstOrDefault(v => v.voterId == frame.hohId)?.targetId
                                    : frame.tally[0] > frame.tally[1] ? frame.nominees[0] : frame.nominees[1];
                            frame.voters = frame.live
                                ? frame.box.Where(v => v.voterId != frame.hohId).Select(v => v.voterId).Distinct().ToList()
                                : Reconstruct(s, week, frame);
                            frame.exact = frame.live || frame.voters.Count == frame.tally.Sum();
                            return frame;
                        }
        
            // SOURCE Gamesim.Simulation.KnownBallots.Frame.Reconstruct 46163:47209
            private static List<string> Reconstruct(EpisodeState s, int week, Frame frame)
                        {
                            var voters = new List<string>();
                            var rows = s.ledger?.power ?? new List<PowerRow>();
                            var removals = s.story?.removals ?? new List<RemovalState>();
                            int removedFrom = EpisodeEngine.CommitmentRulesOn(s) && week >= s.commitmentRulesStartWeek ? week : week + 1;
                            foreach (var c in s.contestants)
                            {
                                if (c == null || c.id == frame.hohId || frame.nominees.Contains(c.id)) continue;
                                bool inHouse = c.status == ContestantStatus.Active || c.status == ContestantStatus.Winner || c.status == ContestantStatus.RunnerUp
                                    || rows.Any(p => p != null && p.evicteeId == c.id && p.week > week)
                                    || removals.Any(r => r != null && r.contestantId == c.id && r.week >= removedFrom);
                                if (inHouse) voters.Add(c.id);
                            }
                            return voters;
                        }
        }
        

        internal sealed class Sheet
        {
            // SOURCE FIELD Gamesim.Simulation.KnownBallots.Sheet.week @ Assets/Gamesim/Simulation/KnownBallots.cs:8531
            public int week;
        
            // SOURCE FIELD Gamesim.Simulation.KnownBallots.Sheet.pending @ Assets/Gamesim/Simulation/KnownBallots.cs:8681
            public bool pending;
        
            // SOURCE FIELD Gamesim.Simulation.KnownBallots.Sheet.final @ Assets/Gamesim/Simulation/KnownBallots.cs:8818
            public bool final;
        
            // SOURCE FIELD Gamesim.Simulation.KnownBallots.Sheet.nominees @ Assets/Gamesim/Simulation/KnownBallots.cs:8919
            public List<string> nominees = new List<string>();
        
            // SOURCE FIELD Gamesim.Simulation.KnownBallots.Sheet.evictedId,hohId @ Assets/Gamesim/Simulation/KnownBallots.cs:8982
            public string evictedId, hohId;
        
            // SOURCE FIELD Gamesim.Simulation.KnownBallots.Sheet.tally @ Assets/Gamesim/Simulation/KnownBallots.cs:9167
            public List<int> tally = new List<int>();
        
            // SOURCE FIELD Gamesim.Simulation.KnownBallots.Sheet.tieBroken @ Assets/Gamesim/Simulation/KnownBallots.cs:9221
            public bool tieBroken;
        
            // SOURCE FIELD Gamesim.Simulation.KnownBallots.Sheet.voters @ Assets/Gamesim/Simulation/KnownBallots.cs:9377
            public List<string> voters = new List<string>();
        
            // SOURCE FIELD Gamesim.Simulation.KnownBallots.Sheet.ballots @ Assets/Gamesim/Simulation/KnownBallots.cs:9623
            public List<Ballot> ballots = new List<Ballot>();
        
            // Explicit spelling of the source implicit parameterless constructor.
            public Sheet() { }
        
            // SOURCE Gamesim.Simulation.KnownBallots.Sheet.Knows 10235:10301
            public bool Knows(string voterId) => Of(voterId)?.Certain == true;
        
            // SOURCE Gamesim.Simulation.KnownBallots.Sheet.Of 10099:10221
            public Ballot Of(string voterId) => voterId == null ? null : ballots.FirstOrDefault(b => b.voterId == voterId && b.Known);
        
            // SOURCE Gamesim.Simulation.KnownBallots.Sheet.TargetOf 10437:10524
            public string TargetOf(string voterId) => Knows(voterId) ? Of(voterId).targetId : null;
        }
        
    }
}

