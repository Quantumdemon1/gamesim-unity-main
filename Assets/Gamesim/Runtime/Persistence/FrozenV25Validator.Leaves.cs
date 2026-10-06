// Snapshot of current FinalArgument.Resolves SHA256 b69ba28dc0ccc320f20d05920a93c628a777288a65a0f35d6f4539d6e0b5aa4e.
// DTO readers, clones and enums bind ONLY fixed Frozen25Data types.
// Frozen helper facade copied from the committed FrozenV24 leaves, extended with actual f456 Safety-aware reader leaves.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Gamesim.Persistence.Frozen25Data;

namespace Gamesim.Persistence
{
    // f456 schema25 frozen helper facade; no live engine, vocabulary, card or appearance validator.
    internal static partial class FrozenV25Validator
    {
        private static class EpisodeEngine
        {
            public const int LegacySocialActionBudget = 18;
            public static bool CommitmentRulesOn(EpisodeState s) => s != null && s.commitmentRulesStartWeek >= 1 && s.week >= s.commitmentRulesStartWeek;

            public const int VetoLineupSize = 6;

            public static bool WeekRulesOn(EpisodeState s) => s != null && s.weekRulesStartWeek >= 1 && s.week >= s.weekRulesStartWeek;

            public static bool EconomyRulesOn(EpisodeState s) => WeekRulesOn(s) && s.economyRulesVersion == 1;

            public static bool IsFirstNight(EpisodeState s) => s != null && s.phase == EpisodePhase.Social && s.week == 1
                        && string.IsNullOrEmpty(s.hohId) && !s.evictionResolved;

            public static bool FinaleOn(EpisodeState s) => s != null && s.finaleRulesStartWeek >= 1 && s.week >= s.finaleRulesStartWeek;

            public static bool AgencyOn(EpisodeState s) => s != null && s.agencyRulesStartWeek >= 1 && s.week >= s.agencyRulesStartWeek;

            public static int VetoPlayerCount(int activeCount)
                        => Math.Min(VetoLineupSize, Math.Max(0, activeCount));

            public static bool IsCompetition(EpisodePhase phase) => phase == EpisodePhase.HoH || phase == EpisodePhase.Veto ||
                        phase == EpisodePhase.FinalHoHPart1 || phase == EpisodePhase.FinalHoHPart2 || phase == EpisodePhase.FinalHoHPart3;

            public static bool StoryOn(EpisodeState s) =>
                        s?.story != null && s.story.rulesStartWeek >= 1 && s.week >= s.story.rulesStartWeek;

            public static bool StoryAt(EpisodeState s, int version) => StoryOn(s) && s.story.rulesVersion >= version;

            public static IEnumerable<ContestantState> CompetitionPlayers(EpisodeState s)
                    {
                        if (s.phase == EpisodePhase.Veto) return Active(s).Where(c => s.vetoPlayers.Contains(c.id));
                        if (s.phase == EpisodePhase.FinalHoHPart2) return Active(s).Where(c => c.id != s.finalPart1WinnerId);
                        if (s.phase == EpisodePhase.FinalHoHPart3) return Active(s).Where(c => c.id == s.finalPart1WinnerId || c.id == s.finalPart2WinnerId);
                        // Production's penalty: somebody on their second strike sits out this week's HoH
                        // competition. Derived from the saved conduct record, so validation's recount agrees.
                        return Active(s).Where(c => (Active(s).Count() <= 3 || c.id != s.previousHohId)
                                                   && !(StoryAt(s, StoryRules.Production) && Production.SitsOut(s, c.id)));
                    }

            public static IEnumerable<ContestantState> Voters(EpisodeState s) => Active(s).Where(c => c.id != s.hohId && !s.nominees.Contains(c.id));
        }

        private static class Production
        {
            public static bool SitsOut(EpisodeState state, string id) =>
                        state.phase == EpisodePhase.HoH && state?.story?.conduct.FirstOrDefault(c => c.contestantId == id)?.sitsOutWeek == state.week && state.week > 0;
        }

        private static class Windows
        {
            public const int Count = 4;
        }

        private static class WebSocialVocabulary
        {
            public const int PurchaseCeiling = 6;
        }

        private static class CompetitionRules
        {
            public const int Widened = 4;

            public const int Current = Widened;
        }

        private static class Knowledge
        {
            public const int Ceiling = 128;
        }

        private static class StrategyRules
        {
            public const double MostInfluence = 100;

            public static bool Apply(EpisodeState s) => s != null && s.strategyRulesStartWeek > 0 && s.week >= s.strategyRulesStartWeek;
        }

        private static class Negotiation
        {
            public const string PricePrefix = "deal-price-";
            public const string CounterDealPrefix = "deal-counter-";

            public static bool IsPrice(DealState d) => d?.id != null && d.id.StartsWith(PricePrefix, StringComparison.Ordinal);
        }

        private static class NpcSocialState
        {
            public static uint InitialRandomState(uint seed) => HashSeed(
                        "gamesim:npc-social:v1:" + seed.ToString("x8", CultureInfo.InvariantCulture));

            public static bool IsEligiblePhase(EpisodePhase phase) => phase == EpisodePhase.Social || phase == EpisodePhase.Campaign;

            public static bool IsEligible(EpisodeState state) => state?.npcSocial != null &&
                        state.week >= state.npcSocial.rulesStartWeek && IsEligiblePhase(state.phase) && state.pendingDiary == null &&
                        Find(state, state.playerId)?.status == ContestantStatus.Active && Active(state).Count(actor => !actor.isPlayer) >= 2;

            public static bool IsKnownTopic(string topic) => topic == "bonding" || topic == "strategy" || topic == "gossip" ||
                        topic == "tension" || topic == "casual" || topic == "nominations" || topic == "alliance_talk" || topic == "rivalry";

            public static bool IsKnownRendezvous(string id) => id == "living-east-chat" || id == "kitchen-west-chat" ||
                        id == "bedroom-south-chat" || id == "yard-south-chat" || id == "kitchen-table-chat" || id == "yard-lounger-chat";

            public static uint HashSeed(string value)
                    {
                        if (value == null) throw new ArgumentNullException(nameof(value));
                        unchecked
                        {
                            var hash = 2166136261u;
                            foreach (var codeUnit in value) hash = (hash ^ codeUnit) * 16777619u;
                            return hash;
                        }
                    }
        }

        private static class WebRules
        {
public static double PromiseImpact(string type, string status, double loyalty = 5)
        {
            double multiplier;
            switch (type)
            {
                case "safety": multiplier = 1.5; break;
                case "final_2": multiplier = 2; break;
                case "vote": multiplier = 1; break;
                case "alliance_loyalty": multiplier = 1.8; break;
                case "information": multiplier = 0.7; break;
                case "hoh_protection": multiplier = 1.6; break;
                case "veto_use": multiplier = 1.4; break;
                default: throw new ArgumentException("Unknown promise type: " + type, nameof(type));
            }
            if (status == "fulfilled") return JsRound(15 * multiplier * (1 + (loyalty - 5) * 0.15));
            // Preserve code arithmetic: loyalty 5 is 1.25, despite its stale web comment.
            if (status == "broken") return JsRound(-25 * multiplier * (0.5 + loyalty * 0.15));
            return 0;
        }

            public static double JsRound(double value)
                    {
                        if (double.IsNaN(value) || double.IsInfinity(value)) return value;
                        var floor = Math.Floor(value);
                        return value - floor < 0.5 ? floor : floor + 1;
                    }
        }

        private static class WebRelationshipArcs
        {
            private static readonly double[] Thresholds = { 0, 25, 50, 75, 90 };

            public static int GetEscalationLevel(double intensity)
                    {
                        for (int i = Thresholds.Length - 1; i >= 0; i--) if (intensity >= Thresholds[i]) return i;
                        return 0;
                    }
        }

        private static class HaveNots
        {

                    public const string CashId = "cash", PassId = "have-not-pass", LuxuryId = "luxury-night";
                    public const string PunishedId = "have-not", AlarmId = "the-alarm", CostumeId = "costume";

                    /// <summary>A prize or a punishment: its words in the house's story, and what it does.</summary>
                    public sealed class Prize
                    {
                        public string Id { get; }
                        public string Title { get; }
                        /// <summary>The words after "won" or the punishment's colon in the house's story.</summary>
                        public string Line { get; }
                        /// <summary>What it does, in the player's words, where it does anything.</summary>
                        public string Effect { get; }
                        public bool Punishment { get; }
                        internal Prize(string id, string title, string line, string effect, bool punishment)
                        { Id = id; Title = title; Line = line; Effect = effect; Punishment = punishment; }
                    }

                    public static readonly Prize[] Prizes =
                    {
                        new Prize(CashId, "$5,000", "$5,000", "A cheque for $5,000. It buys nothing in the house.", false),
                        new Prize(PassId, "Have-Not pass", "a Have-Not pass", "Safe from next week's Have-Nots.", false),
                        new Prize(LuxuryId, "Luxury night", "a luxury night out of the house", "One more conversation until the end of next week.", false),
                    };

                    public static readonly Prize[] Punishments =
                    {
                        new Prize(PunishedId, "Have-Not", "a Have-Not next week, whatever the competition says", "A Have-Not next week, whatever the competition says.", true),
                        new Prize(AlarmId, "The alarm", "an alarm every hour of the night", "One fewer conversation until the end of next week.", true),
                        new Prize(CostumeId, "The costume", "a banana suit until the eviction", "A banana suit until the eviction.", true),
                    };

                    public static Prize Find(string id) => Prizes.Concat(Punishments).FirstOrDefault(prize => prize.Id == id);
        }

        private static class HoHPitches
        {
            public const string FeelOutKey = "feel-out";

            public static bool Available(EpisodeState s) => EpisodeEngine.EconomyRulesOn(s)
                        && StrategyRules.Apply(s) && EpisodeEngine.AgencyOn(s)
                        && s.phase == EpisodePhase.Nomination && s.nominees.Count == 0
                        && s.hohId == s.playerId && Find(s, s.playerId)?.status == ContestantStatus.Active;

            public static bool ValidCard(EpisodeState s, ReplyCardState card) => Available(s)
                        && card != null && card.kind == ReplyCards.Pitch && card.week == s.week
                        && card.fromId != s.playerId && Find(s, card.fromId)?.status == ContestantStatus.Active
                        && (card.aboutId == null || (card.aboutId != card.fromId && card.aboutId != s.playerId
                            && Find(s, card.aboutId)?.status == ContestantStatus.Active))
                        && !(s.ledger?.replies?.Any(r => r != null && r.week == card.week && r.kind == ReplyCards.Pitch
                            && r.fromId == card.fromId && r.replyKey != FeelOutKey) ?? false);
        }

        private static class ReplyCards
        {
            public const string Confrontation = "confrontation", Gossip = "gossip", Plea = "plea", Pitch = "pitch";

            public static readonly string[] All = { Confrontation, Gossip, Plea, Pitch };

            public static bool IsKnown(string kind) => kind != null && Array.IndexOf(All, kind) >= 0;

            public sealed class Reply { public double ToThem; }

            public static Reply Find(string kind, string key) => kind != Pitch ? null : key == "promise-safety" ? new Reply { ToThem = 8 }
                : key == "hear" ? new Reply { ToThem = 0 } : key == "turn-down" ? new Reply { ToThem = -5 } : null;
        }

        private static class BlockSpeeches
        {
            public const string Quiet = "quiet", FactorCode = "speech", EventPrefix = "block-speech:";

            public static bool IsApproach(string key) => key == Quiet || LobbyApproach.IsKnown(key);

            public static bool IsReceiptKind(string kind) => kind != null && kind.StartsWith(EventPrefix, StringComparison.Ordinal);

            public static string EventApproach(EpisodeEvent receipt) => IsReceiptKind(receipt?.kind)
                        ? receipt.kind.Substring(EventPrefix.Length) : null;

            public static string ReceiptText(EvictionSpeechState speech) => string.IsNullOrWhiteSpace(speech?.text)
                        ? "No speech was given." : speech.text;

            public static string[] Audience(EpisodeState s, string speakerId) => new[] { speakerId }
                        .Concat(Active(s).Select(c => c.id).Where(id => id != speakerId))
                        .Concat(Find(s, s.playerId)?.status == ContestantStatus.Active ? Array.Empty<string>() : new[] { s.playerId })
                        .Distinct(StringComparer.Ordinal).ToArray();

            public static bool ValidateReceipts(EpisodeState s, out string error)
                    {
                        error = null;
                        var receipts = s.events.Where(e => IsReceiptKind(e.kind)).ToArray();
                        foreach (var e in receipts)
                        {
                            string approach = EventApproach(e);
                            if (s.economyRulesVersion != 1 || !IsApproach(approach) || e.phase != EpisodePhase.Eviction
                                || s.weekRulesStartWeek < 1 || e.week < s.weekRulesStartWeek
                                || s.strategyRulesStartWeek < 1 || e.week < s.strategyRulesStartWeek
                                || s.agencyRulesStartWeek < 1 || e.week < s.agencyRulesStartWeek
                                || s.leverRulesStartWeek < 1 || e.week < s.leverRulesStartWeek
                                || e.audienceIds.Count < 3 || e.audienceIds.Count > MaximumCast
                                || e.audienceIds.Distinct(StringComparer.Ordinal).Count() != e.audienceIds.Count
                                || !e.audienceIds.Contains(s.playerId) || (approach == Quiet && e.text != "No speech was given."))
                            { error = "Invalid block-speech receipt."; return false; }
                            if (e.week != s.week) continue; // Earlier weeks' words outlive cleared speech/nominee rows.
                            string speaker = e.audienceIds[0];
                            var speech = s.evictionSpeeches.FirstOrDefault(x => x.week == e.week && x.speakerId == speaker);
                            if ((s.phase != EpisodePhase.Eviction && !(s.phase == EpisodePhase.Social && s.evictionResolved))
                                || (s.phase == EpisodePhase.Eviction && s.evictionStage == EvictionStage.Interaction)
                                || speech == null || !s.nominees.Contains(speaker) || e.text != ReceiptText(speech)
                                || (approach == Quiet) != string.IsNullOrWhiteSpace(speech.text)
                                || (!s.evictionResolved && s.phase == EpisodePhase.Eviction && !e.audienceIds.SequenceEqual(Audience(s, speaker))))
                            { error = "Block-speech receipt does not match its recorded delivery."; return false; }
                        }
                        if (receipts.GroupBy(e => e.week + ":" + e.audienceIds[0]).Any(g => g.Count() > 1)
                            || receipts.GroupBy(e => e.week).Any(g => g.Count() > 2))
                        { error = "Duplicate block-speech receipt."; return false; }
                        return true;
                    }
        }

        private static class FinalArgument
        {
            public const string Cerebral = "cerebral", Social = "social", Aggressive = "aggressive", Sneaky = "sneaky", Emotional = "emotional";

            public static readonly string[] Themes = { Cerebral, Social, Aggressive, Sneaky, Emotional };

            public const int MomentCount = 3;

            public static bool Resolves(EpisodeState s, string reference)
        {
            if (s == null || string.IsNullOrEmpty(reference) || reference.Length > 200) return false;
            var ledger = s.ledger ?? new SeasonLedger();
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

        private static class FinaleQuestions
        {
public sealed class Receipt { public string category, kind, id; public int week; }
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

private static bool CanonicalReceiptBelongsToPlayerAndJuror(EpisodeState s, string id, string juror, string policy)
        {
            if (!UnifiedCommitments.RulesOn(s)) return true;
            var row = CommitmentReferences.FindCanonical(s, id);
            if (row == null) return true;
            if (row.sourcePolicy != policy) return false;
            return policy == UnifiedCommitments.PromisePolicy
                ? row.makerId == s.playerId && row.beneficiaryId == juror
                : (row.makerId == s.playerId && row.beneficiaryId == juror)
                    || (row.makerId == juror && row.beneficiaryId == s.playerId);
        }

public static int? ReceiptWeek(EpisodeState s, JuryExchangeState exchange)
        {
            if (s == null || exchange == null || exchange.receiptKind == null || ReceiptLine(s, exchange) == null) return null;
            string id = exchange.receiptId;
            var ledger = s.ledger ?? new SeasonLedger();
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

private static int? ParsedWeek(string words) =>
            int.TryParse(words, NumberStyles.None, CultureInfo.InvariantCulture, out int week) && week > 0 ? week : (int?)null;

            public const string Accountability = "accountability", Ownership = "ownership", JuryManagement = "jury-management",
                        Strategy = "strategy", Social = "social", Comparison = "comparison", Mistake = "mistake", Personal = "personal";

            public static readonly string[] Categories = { Accountability, Ownership, JuryManagement, Strategy, Social, Comparison, Mistake, Personal };

            public const string PromiseReceipt = "promise", DealReceipt = "deal", PowerReceipt = "power", BallotReceipt = "ballot",
                        ReplyReceipt = "reply", CallReceipt = "call", AllianceReceipt = "alliance";

            public const string Own = "own", Explain = "explain", Loyalty = "loyalty", Deflect = "deflect", Truth = "truth";

            public static readonly string[] Responses = { Own, Explain, Loyalty, Deflect, Truth };

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

            public static string Tone(string category)
                    {
                        switch (category)
                        {
                            case Accountability: case Ownership: case JuryManagement: return "bitter";
                            case Social: case Personal: return "supportive";
                            default: return "neutral";
                        }
                    }

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

            public static string[] Offered(string category, string receiptKind) =>
                        category == Strategy && receiptKind == CallReceipt ? Responses : Responses.Take(4).ToArray();
        }

        private static class WebJuryQuestioning
        {
            public static string GetPrimaryTrait(IReadOnlyList<string> traits)
                    {
                        // JavaScript hg.traits?.[0] || 'Strategic': do not trim or inspect later traits.
                        return traits == null || traits.Count == 0 || string.IsNullOrEmpty(traits[0]) ? "Strategic" : traits[0];
                    }

            public static WebJuryResponsePair GetResponsePair(IReadOnlyList<string> traits, int index, double flipRoll)
                    {
                        RequireIndex(index); RequireRoll(flipRoll);
                        var trait = GetPrimaryTrait(traits);
                        var pairs = WebJuryQuestioningCatalog.Pairs.TryGetValue(trait, out var found)
                            ? found : WebJuryQuestioningCatalog.Pairs["Strategic"];
                        var pair = pairs[index % pairs.Length];
                        bool flip = flipRoll > 0.5;
                        return new WebJuryResponsePair
                        {
                            optionA = flip ? pair[0] : pair[1], optionB = flip ? pair[1] : pair[0],
                            correctIs = flip ? "A" : "B", trait = trait
                        };
                    }

            public static string GetOpponentAnswer(int jurorIndex)
                    {
                        RequireIndex(jurorIndex);
                        return WebJuryQuestioningCatalog.NpcResponses[jurorIndex % WebJuryQuestioningCatalog.NpcResponses.Length];
                    }

            public static WebJuryQuestionOption[] GetJurorQuestionOptions(int finalistIndex)
                    {
                        RequireIndex(finalistIndex);
                        var tones = new[] { "neutral", "bitter", "supportive" };
                        var result = new WebJuryQuestionOption[tones.Length];
                        for (int offset = 0; offset < tones.Length; offset++)
                        {
                            var questions = WebJuryQuestioningCatalog.Questions[tones[offset]];
                            // Long addition preserves the source number arithmetic at Int32.MaxValue.
                            result[offset] = new WebJuryQuestionOption { tone = tones[offset], text = questions[(int)(((long)finalistIndex + offset) % questions.Length)] };
                        }
                        return result;
                    }

            public static bool MatchesSource(WebJuryQuestion question, IReadOnlyList<string> jurorTraits, int jurorIndex)
                    {
                        if (question == null || jurorIndex < 0 || (question.correctIs != "A" && question.correctIs != "B")
                            || question.tone == null || !WebJuryQuestioningCatalog.Questions.TryGetValue(question.tone, out var questions)) return false;
                        var pair = GetResponsePair(jurorTraits, jurorIndex, question.correctIs == "A" ? 0.75 : 0.25);
                        return question.question == questions[jurorIndex % questions.Length] && question.optionA == pair.optionA
                            && question.optionB == pair.optionB && question.trait == pair.trait && question.opponentAnswer == GetOpponentAnswer(jurorIndex);
                    }

            private static void RequireIndex(int index)
                    {
                        if (index < 0) throw new ArgumentOutOfRangeException(nameof(index));
                    }

            private static void RequireRoll(double roll)
                    {
                        if (double.IsNaN(roll) || double.IsInfinity(roll) || roll < 0 || roll >= 1)
                            throw new ArgumentOutOfRangeException(nameof(roll), "Random samples must be in [0, 1).");
                    }
        }

        private static class WebJuryQuestioningCatalog
            {
                internal static readonly Dictionary<string, string[]> Questions = new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    { "neutral", new[] { "What was your biggest move in the game?", "Why do you deserve to win over your opponent?", "Who was the most influential player on your game?", "What would you have done differently?", "Describe your game in three words.", "How did you balance competition wins with social game?" } },
                    { "bitter", new[] { "Why should I vote for you after you betrayed our alliance?", "Do you feel any remorse for how you played?", "How do you justify the lies you told in this game?", "Did you ever consider how your actions affected others?", "What makes you think you deserve my vote after backstabbing me?" } },
                    { "supportive", new[] { "Walk us through your strategic masterplan.", "How did you manage to stay so calm under pressure?", "What advice would you give to future players?", "When did you know you could win this game?", "Tell us about your proudest moment in the house." } }
                };

                internal static readonly string[] NpcResponses = new[]
                {
                    "I played the best game I could, and I stand by every decision I made.",
                    "My strategy was always to stay adaptable while keeping my core alliances intact.",
                    "I think my gameplay speaks for itself — I earned my spot in this final two.",
                    "I respected everyone in this house, even when I had to make tough calls.",
                    "I came here to win, and every move brought me one step closer."
                };

                internal static readonly Dictionary<string, string[][]> Pairs = new Dictionary<string, string[][]>(StringComparer.Ordinal)
                {
                    { "Competitive", new[] { new[] { "I earned my spot here by winning when it mattered most. I never backed down from a challenge.", "I kept my head down and avoided drawing attention to myself. The quiet game got me here." }, new[] { "Every competition was a chance to prove myself, and I rose to the occasion.", "Competitions weren't really my focus — I was more about the social connections." }, new[] { "I fought hard for every victory and I'm proud of every single win.", "Winning comps wasn't important to me. I relied on others to keep me safe." } } },
                    { "Strategic", new[] { new[] { "Every decision I made was calculated to get me to this point. Nothing was by accident.", "I just followed my heart and hoped for the best. I didn't really plan ahead." }, new[] { "I read the house dynamics each week and positioned myself perfectly.", "I won competitions and that's why I'm sitting here. Strategy wasn't really my thing." }, new[] { "I always had a plan B and C ready. Adaptability was my greatest weapon.", "I stuck with one alliance from day one and never wavered. Loyalty above all." } } },
                    { "Loyal", new[] { new[] { "I always stayed true to the people I gave my word to. My alliances were real.", "I made moves when the timing was right, regardless of promises I'd made." }, new[] { "The relationships I built in this house were genuine, not just strategic tools.", "Every friendship was a calculated move to advance my position in the game." }, new[] { "I kept my promises even when it would have been easier to break them.", "Promises are just part of the game — everyone knows they're meant to be broken." } } },
                    { "Emotional", new[] { new[] { "I want you to know that I genuinely cared about everyone in this house. This wasn't just a game to me.", "At the end of the day, it's just a game. I didn't let feelings get in the way of winning." }, new[] { "I know I hurt people along the way, and I carry that with me. I'm truly sorry.", "I don't apologize for my game. Everyone signed up knowing what they were getting into." }, new[] { "The hardest part was seeing people I cared about walk out that door.", "Evictions were just part of the process. I didn't lose sleep over them." } } },
                    { "Charming", new[] { new[] { "I connected with people on a real, personal level — that's what kept me safe.", "I isolated myself and focused on competitions. Social game wasn't my priority." }, new[] { "People wanted to work with me because I made them feel valued and heard.", "I controlled people through fear and intimidation. That's how you play this game." }, new[] { "My social game was my superpower, and I'm proud of the bonds I built.", "I didn't bother with small talk or friendships. This is a strategy game, not summer camp." } } },
                    { "Manipulative", new[] { new[] { "I influenced the house without anyone realizing it. Information was my currency.", "I was completely transparent with everyone about my plans and intentions." }, new[] { "I planted seeds and watched them grow. The best moves are the ones no one sees.", "I won competitions and used that power directly. Subtlety is overrated." }, new[] { "I knew exactly what each person needed to hear to keep myself safe.", "I said the same thing to everyone. Honesty was my policy from day one." } } },
                    { "Analytical", new[] { new[] { "I studied everyone's patterns and used that data to make optimal decisions.", "I just went with my gut feeling every week. Analysis paralysis is a real thing." }, new[] { "I mapped out every possible outcome before making my moves.", "I'm more of a spontaneous player. Overthinking ruins the fun." }, new[] { "Understanding the probability of each scenario gave me a strategic edge.", "Numbers and odds don't matter when you have strong alliances." } } },
                    { "Social", new[] { new[] { "My social connections were the foundation of everything I accomplished.", "I was a solo player. I didn't need anyone else to get to the end." }, new[] { "Being in the center of the social web meant I always knew what was happening.", "I stayed in my room and avoided house drama. Less socializing, less trouble." }, new[] { "People trusted me because I invested real time in getting to know them.", "Trust is a weakness in this game. I kept everyone at arm's length." } } },
                    { "Funny", new[] { new[] { "I kept the mood light and people wanted me around. Laughter is powerful.", "I was all business, all the time. There's no room for jokes in this game." }, new[] { "Humor helped me defuse tense situations and keep my allies close.", "I used confrontation and aggression to establish dominance in the house." } } },
                    { "Deceptive", new[] { new[] { "The best deceptions are the ones people never discover. I played brilliantly.", "I was an open book the entire game. What you saw was what you got." }, new[] { "Misdirection was an art form for me. I controlled the narrative.", "I told the truth even when it hurt my game. Integrity above strategy." } } },
                    { "Introverted", new[] { new[] { "I observed from the sidelines and struck at exactly the right moments.", "I was always the loudest voice in every room. Big personalities win big games." }, new[] { "While everyone else was arguing, I was quietly gathering information.", "I made sure everyone knew exactly where I stood at all times." } } },
                    { "Sneaky", new[] { new[] { "I worked behind the scenes where the real power lives.", "I was upfront about every move I made. Transparency was my brand." }, new[] { "The best player is the one nobody suspects. I stayed under the radar.", "I wanted everyone to know I was running the house. Fear is respect." } } },
                    { "Stubborn", new[] { new[] { "I stuck to my convictions even when the whole house pressured me otherwise.", "I was always willing to flip my vote if someone made a good argument." }, new[] { "My determination and refusal to quit is what got me to the finale.", "Flexibility and going with the flow was my key strategy." } } },
                    { "Flexible", new[] { new[] { "I adapted to every twist and turn. Rigidity gets you evicted.", "I had one plan from day one and I never deviated from it." }, new[] { "When the house shifted, I shifted with it. That's smart gameplay.", "I refused to change my approach regardless of what happened." } } },
                    { "Impulsive", new[] { new[] { "I trusted my instincts and made bold moves when others hesitated.", "I carefully deliberated every decision for hours before committing." }, new[] { "Sometimes you just have to go for it. My gut never steered me wrong.", "I never made a move without running the numbers first." } } },
                    { "Intuitive", new[] { new[] { "I could read people and situations before anyone else saw what was coming.", "I relied purely on hard facts and data. Intuition is unreliable." }, new[] { "My instincts told me who to trust and when to make my move.", "I ignored my feelings and made decisions based only on logic." } } },
                    { "Confrontational", new[] { new[] { "I wasn't afraid to call people out and fight for what I believed in.", "I avoided conflict at all costs. Keeping the peace was my priority." }, new[] { "Direct honesty, even when it's uncomfortable, earned me respect.", "I told people what they wanted to hear to keep things smooth." } } }
                };
            }

        private static bool AppearanceValid(CharacterAppearance recipe, out string error)
                {
                    error = null;
                    if (recipe.version != 1 || !AppearanceKey(recipe.provider) || !AppearanceKey(recipe.presetId, true) || !AppearanceKey(recipe.bodyId, true)
                        || !AppearanceKey(recipe.fallbackId) || !AppearanceKey(recipe.activeOutfit)) return Fail(out error, "Invalid appearance identity or version.");
                    if (recipe.dna == null || recipe.dna.Count > 128 || recipe.dna.Any(x => x == null || !AppearanceKey(x.id) || !AppearanceUnit(x.value))
                        || recipe.dna.Select(x => x.id).Distinct(StringComparer.Ordinal).Count() != recipe.dna.Count)
                        return Fail(out error, "Appearance sliders must have unique names and finite values between zero and one.");
                    if (!AppearanceColors(recipe.colors)) return Fail(out error, "Invalid appearance colors.");
                    if (recipe.outfits == null || recipe.outfits.Count > 8 || recipe.outfits.Any(x => x == null || !AppearanceKey(x.id))
                        || recipe.outfits.Select(x => x.id).Distinct(StringComparer.Ordinal).Count() != recipe.outfits.Count)
                        return Fail(out error, "An appearance supports up to eight uniquely named outfits.");
                    foreach (var outfit in recipe.outfits)
                        if (!AppearanceColors(outfit.colors) || outfit.wardrobe == null || outfit.wardrobe.Count > 32
                            || outfit.wardrobe.Any(x => x == null || !AppearanceKey(x.slot) || !AppearanceKey(x.itemId, true))
                            || outfit.wardrobe.Select(x => x.slot).Distinct(StringComparer.Ordinal).Count() != outfit.wardrobe.Count)
                            return Fail(out error, "Outfit items must occupy unique slots with valid catalog identifiers.");
                    if (recipe.outfits.Count > 0 && !recipe.outfits.Any(x => x.id == recipe.activeOutfit))
                        return Fail(out error, "The active outfit is missing.");
                    return true;
                }

        private static bool AppearanceColors(List<AppearanceColor> values) => values != null && values.Count <= 64
                    && values.All(x => x != null && AppearanceKey(x.id) && AppearanceUnit(x.r) && AppearanceUnit(x.g) && AppearanceUnit(x.b) && AppearanceUnit(x.a))
                    && values.Select(x => x.id).Distinct(StringComparer.Ordinal).Count() == values.Count;
                private static bool AppearanceUnit(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0 && value <= 1;
                private static bool AppearanceKey(string value, bool empty = false) => value != null && value.Length <= 128
                    && (empty || !string.IsNullOrWhiteSpace(value)) && !value.Any(char.IsControl)
                    && value.IndexOfAny(new[] { '/', '\\', ':' }) < 0;
        private static IEnumerable<ContestantState> Active(EpisodeState s) => s.contestants.Where(c => c.status == ContestantStatus.Active);
        private static ContestantState Find(EpisodeState s, string id) => s.contestants.FirstOrDefault(c => c.id == id);
    }
}
