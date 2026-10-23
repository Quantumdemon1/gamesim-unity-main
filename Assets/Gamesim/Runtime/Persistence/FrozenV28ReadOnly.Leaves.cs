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
    internal static class BlockSpeeches
    {
        // SOURCE FIELD Gamesim.Simulation.BlockSpeeches.Quiet,FactorCode,EventPrefix @ Assets/Gamesim/Simulation/BlockSpeeches.cs:471
        public const string Quiet = "quiet", FactorCode = "speech", EventPrefix = "block-speech:";

        // SOURCE Gamesim.Simulation.BlockSpeeches.Audience 2778:3122
        public static string[] Audience(EpisodeState s, string speakerId) => new[] { speakerId }
                    .Concat(LiteralRows.Active(s).Select(c => c.id).Where(id => id != speakerId))
                    .Concat(s.Find(s.playerId)?.status == ContestantStatus.Active ? Array.Empty<string>() : new[] { s.playerId })
                    .Distinct(StringComparer.Ordinal).ToArray();

        // SOURCE Gamesim.Simulation.BlockSpeeches.EventApproach 1128:1281
        public static string EventApproach(EpisodeEvent receipt) => IsReceiptKind(receipt?.kind)
                    ? receipt.kind.Substring(EventPrefix.Length) : null;

        // SOURCE Gamesim.Simulation.BlockSpeeches.IsApproach 902:990
        public static bool IsApproach(string key) => key == Quiet || LobbyApproach.IsKnown(key);

        // SOURCE Gamesim.Simulation.BlockSpeeches.IsReceiptKind 999:1119
        public static bool IsReceiptKind(string kind) => kind != null && kind.StartsWith(EventPrefix, StringComparison.Ordinal);

        // SOURCE Gamesim.Simulation.BlockSpeeches.ReceiptText 1290:1445
        public static string ReceiptText(EvictionSpeechState speech) => string.IsNullOrWhiteSpace(speech?.text)
                    ? "No speech was given." : speech.text;

        // SOURCE Gamesim.Simulation.BlockSpeeches.ValidateReceipts 7785:10133
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
                            || e.audienceIds.Count < 3 || e.audienceIds.Count > EpisodeValidation.MaximumCast
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

    internal static class BondKinds
    {
        // SOURCE FIELD Gamesim.Simulation.BondKinds.All @ Assets/Gamesim/Simulation/Story/StoryState.cs:19832
        public static readonly string[] All = { RideOrDie, Confidant, Nemesis, Showmance };

        // SOURCE FIELD Gamesim.Simulation.BondKinds.RideOrDie,Confidant,Nemesis,Showmance @ Assets/Gamesim/Simulation/Story/StoryState.cs:19706
        public const string RideOrDie = "ride-or-die", Confidant = "confidant", Nemesis = "nemesis", Showmance = "showmance";

        // SOURCE Gamesim.Simulation.BondKinds.IsKnown 19924:20013
        public static bool IsKnown(string kind) => kind != null && Array.IndexOf(All, kind) >= 0;
    }

    internal static class BondStatus
    {
        // SOURCE FIELD Gamesim.Simulation.BondStatus.All @ Assets/Gamesim/Simulation/Story/StoryState.cs:20688
        public static readonly string[] All = { Private, Public, Apart, Ended, Betrayed };

        // SOURCE FIELD Gamesim.Simulation.BondStatus.Private @ Assets/Gamesim/Simulation/Story/StoryState.cs:20191
        public const string Private = "private";

        // SOURCE FIELD Gamesim.Simulation.BondStatus.Public @ Assets/Gamesim/Simulation/Story/StoryState.cs:20300
        public const string Public = "public";

        // SOURCE FIELD Gamesim.Simulation.BondStatus.Apart @ Assets/Gamesim/Simulation/Story/StoryState.cs:20444
        public const string Apart = "apart";

        // SOURCE FIELD Gamesim.Simulation.BondStatus.Ended @ Assets/Gamesim/Simulation/Story/StoryState.cs:20489
        public const string Ended = "ended";

        // SOURCE FIELD Gamesim.Simulation.BondStatus.Betrayed @ Assets/Gamesim/Simulation/Story/StoryState.cs:20637
        public const string Betrayed = "betrayed";

        // SOURCE Gamesim.Simulation.BondStatus.IsKnown 20779:20874
        public static bool IsKnown(string status) => status != null && Array.IndexOf(All, status) >= 0;
    }

    internal static class ClaimSource
    {
        // SOURCE FIELD Gamesim.Simulation.ClaimSource.Told,Overheard,Ally,Read @ Assets/Gamesim/Simulation/SeasonLedger.cs:10297
        public const string Told = "told", Overheard = "overheard", Ally = "ally", Read = "read";

        // SOURCE FIELD Gamesim.Simulation.ClaimSource.All @ Assets/Gamesim/Simulation/SeasonLedger.cs:10904
        public static readonly string[] All = { Told, Overheard, Ally, Read, Juror, Missed, Deflected };

        // SOURCE FIELD Gamesim.Simulation.ClaimSource.Juror @ Assets/Gamesim/Simulation/SeasonLedger.cs:10491
        public const string Juror = "juror";

        // SOURCE FIELD Gamesim.Simulation.ClaimSource.Missed @ Assets/Gamesim/Simulation/SeasonLedger.cs:10671
        public const string Missed = "missed";

        // SOURCE FIELD Gamesim.Simulation.ClaimSource.Deflected @ Assets/Gamesim/Simulation/SeasonLedger.cs:10851
        public const string Deflected = "deflected";

        // SOURCE Gamesim.Simulation.ClaimSource.IsKnown 11243:11338
        public static bool IsKnown(string source) => source != null && Array.IndexOf(All, source) >= 0;
    }

    internal static class ClaimStatus
    {
        // SOURCE FIELD Gamesim.Simulation.ClaimStatus.Open,Kept,Lied @ Assets/Gamesim/Simulation/SeasonLedger.cs:11456
        public const string Open = "open", Kept = "kept", Lied = "lied";

        // SOURCE FIELD Gamesim.Simulation.ClaimStatus.All @ Assets/Gamesim/Simulation/SeasonLedger.cs:11529
        public static readonly string[] All = { Open, Kept, Lied };

        // SOURCE Gamesim.Simulation.ClaimStatus.IsKnown 11597:11692
        public static bool IsKnown(string status) => status != null && Array.IndexOf(All, status) >= 0;
    }

    internal static class CommitmentReferences
    {
        // SOURCE FIELD Gamesim.Simulation.CommitmentReferences.RefusalMark @ Assets/Gamesim/Simulation/CommitmentReferences.cs:13557
        private const string RefusalMark = "Gamesim.Simulation.CommitmentStorageRefusal";

        // SOURCE Gamesim.Simulation.CommitmentReferences.Deals 2338:3168
        internal static IReadOnlyList<DealState> Deals(EpisodeState state) => Gamesim.Persistence.Frozen28Semantics.FrozenV28CommitmentReferences.Deals(state);

        // SOURCE Gamesim.Simulation.CommitmentReferences.FindCanonical 9353:9855
        internal static UnifiedCommitmentState FindCanonical(EpisodeState state, string id) => Gamesim.Persistence.Frozen28Semantics.FrozenV28CommitmentReferences.FindCanonical(state, id);

        // SOURCE Gamesim.Simulation.CommitmentReferences.FindCanonicalSafety 8991:9232
        internal static UnifiedCommitmentState FindCanonicalSafety(EpisodeState state, string id) => Gamesim.Persistence.Frozen28Semantics.FrozenV28CommitmentReferences.FindCanonicalSafety(state, id);

        // SOURCE Gamesim.Simulation.CommitmentReferences.FindDeal 7987:8136
        internal static DealState FindDeal(EpisodeState state, string id) => Gamesim.Persistence.Frozen28Semantics.FrozenV28CommitmentReferences.FindDeal(state, id);

        // SOURCE Gamesim.Simulation.CommitmentReferences.FindPromise 7819:7977
        internal static PromiseState FindPromise(EpisodeState state, string id) => Gamesim.Persistence.Frozen28Semantics.FrozenV28CommitmentReferences.FindPromise(state, id);

        // SOURCE Gamesim.Simulation.CommitmentReferences.ProjectDeal 10319:10797
        internal static DealState ProjectDeal(UnifiedCommitmentState row) => Gamesim.Persistence.Frozen28Semantics.FrozenV28CommitmentReferences.ProjectDeal(row);

        // SOURCE Gamesim.Simulation.CommitmentReferences.ProjectPromise 9865:10309
        internal static PromiseState ProjectPromise(UnifiedCommitmentState row) => Gamesim.Persistence.Frozen28Semantics.FrozenV28CommitmentReferences.ProjectPromise(row);

        // SOURCE Gamesim.Simulation.CommitmentReferences.Promises 1328:2182
        internal static IReadOnlyList<PromiseState> Promises(EpisodeState state) => Gamesim.Persistence.Frozen28Semantics.FrozenV28CommitmentReferences.Promises(state);

        // SOURCE Gamesim.Simulation.CommitmentReferences.PromiseStatusFor 10807:11247
        internal static PromiseStatus PromiseStatusFor(string status) => Gamesim.Persistence.Frozen28Semantics.FrozenV28CommitmentReferences.PromiseStatusFor(status);

        // SOURCE Gamesim.Simulation.CommitmentReferences.ReceiptWeek 8444:8680
        internal static int ReceiptWeek(EpisodeState state, string id, int legacyWeek) => Gamesim.Persistence.Frozen28Semantics.FrozenV28CommitmentReferences.ReceiptWeek(state, id, legacyWeek);

        // SOURCE Gamesim.Simulation.CommitmentReferences.StorageRefusal 12920:13219
        internal static ArgumentException StorageRefusal(string error, string paramName = null) => Gamesim.Persistence.Frozen28Semantics.FrozenV28CommitmentReferences.StorageRefusal(error, paramName);
    }

    internal static class CompetitionEntry
    {
        // SOURCE FIELD Gamesim.Simulation.CompetitionEntry.All @ Assets/Gamesim/Simulation/SeasonLedger.cs:13356
        public static readonly string[] All = { Played, Assist, Simulated, Thrown, Watched };

        // SOURCE FIELD Gamesim.Simulation.CompetitionEntry.Played,Assist,Simulated,Thrown,Watched @ Assets/Gamesim/Simulation/SeasonLedger.cs:13225
        public const string Played = "played", Assist = "assist", Simulated = "simulated", Thrown = "thrown", Watched = "watched";

        // SOURCE Gamesim.Simulation.CompetitionEntry.IsKnown 13450:13542
        public static bool IsKnown(string entry) => entry != null && Array.IndexOf(All, entry) >= 0;
    }

    internal static class DealKind
    {
        // SOURCE FIELD Gamesim.Simulation.DealKind.TargetAgreement @ Assets/Gamesim/Simulation/DealState.cs:4127
        public const string TargetAgreement = "target_agreement";

        // SOURCE FIELD Gamesim.Simulation.DealKind.SafetyAgreement @ Assets/Gamesim/Simulation/DealState.cs:4193
        public const string SafetyAgreement = "safety_agreement";

        // SOURCE FIELD Gamesim.Simulation.DealKind.VoteTogether @ Assets/Gamesim/Simulation/DealState.cs:4259
        public const string VoteTogether = "vote_together";

        // SOURCE FIELD Gamesim.Simulation.DealKind.VoteSave @ Assets/Gamesim/Simulation/DealState.cs:4319
        public const string VoteSave = "vote_save";

        // SOURCE FIELD Gamesim.Simulation.DealKind.VoteEvict @ Assets/Gamesim/Simulation/DealState.cs:4371
        public const string VoteEvict = "vote_evict";

        // SOURCE FIELD Gamesim.Simulation.DealKind.VetoUse @ Assets/Gamesim/Simulation/DealState.cs:4425
        public const string VetoUse = "veto_use";

        // SOURCE FIELD Gamesim.Simulation.DealKind.InformationSharing @ Assets/Gamesim/Simulation/DealState.cs:4475
        public const string InformationSharing = "information_sharing";

        // SOURCE FIELD Gamesim.Simulation.DealKind.FinalTwo @ Assets/Gamesim/Simulation/DealState.cs:4547
        public const string FinalTwo = "final_two";

        // SOURCE FIELD Gamesim.Simulation.DealKind.Partnership @ Assets/Gamesim/Simulation/DealState.cs:4599
        public const string Partnership = "partnership";

        // SOURCE FIELD Gamesim.Simulation.DealKind.AllianceInvite @ Assets/Gamesim/Simulation/DealState.cs:4656
        public const string AllianceInvite = "alliance_invite";

        // SOURCE FIELD Gamesim.Simulation.DealKind.FinalThree @ Assets/Gamesim/Simulation/DealState.cs:5756
        public const string FinalThree = "final_three";

        // SOURCE FIELD Gamesim.Simulation.DealKind.All @ Assets/Gamesim/Simulation/DealState.cs:5813
        public static readonly string[] All =
                {
                    TargetAgreement, SafetyAgreement, VoteTogether, VoteSave, VoteEvict,
                    VetoUse, InformationSharing, FinalTwo, Partnership, AllianceInvite,
                    FinalThree,
                };

        // SOURCE Gamesim.Simulation.DealKind.CommitmentRulesOnly 6324:6398
        public static bool CommitmentRulesOnly(string type) => type == FinalThree;

        // SOURCE Gamesim.Simulation.DealKind.DefaultTrust 6795:7374
        internal static string DefaultTrust(string type) => Gamesim.Persistence.Frozen28Semantics.FrozenV28VoteStorage.DealKind.DefaultTrust(type);

        // SOURCE Gamesim.Simulation.DealKind.IsKnown 6066:6155
        public static bool IsKnown(string type) => type != null && Array.IndexOf(All, type) >= 0;

        // SOURCE Gamesim.Simulation.DealKind.NamesATarget 6515:6640
        public static bool NamesATarget(string type) =>
                    type == TargetAgreement || type == VoteEvict || type == VoteSave;

        // SOURCE Gamesim.Simulation.DealKind.Title 7825:8587
        public static string Title(string type)
                {
                    switch (type)
                    {
                        case TargetAgreement: return "Target Agreement";
                        case SafetyAgreement: return "Safety Pact";
                        case VoteTogether: return "Voting Bloc";
                        case VoteSave: return "Vote to Save";
                        case VoteEvict: return "Vote to Evict";
                        case VetoUse: return "Veto Commitment";
                        case InformationSharing: return "Information Sharing";
                        case FinalTwo: return "Final Two Deal";
                        case AllianceInvite: return "Alliance Invitation";
                        case FinalThree: return "Final Three Deal";
                        default: return "Partnership";
                    }
                }
    }

    internal static class DealResolution
    {
        // SOURCE FIELD Gamesim.Simulation.DealResolution.FulfilledBase,BrokenBase @ Assets/Gamesim/Simulation/DealResolution.cs:3120
        public const double FulfilledBase = 8, BrokenBase = -15;

        // SOURCE Gamesim.Simulation.DealResolution.AcceptedOffer 6197:6710
        public static bool AcceptedOffer(EpisodeState s, DealState deal) =>
                    deal != null && s != null && EpisodeEngine.CommitmentRulesOn(s) && deal.week >= s.commitmentRulesStartWeek
                    && deal.id != null
                    && (PutToThePlayer(s, deal) || PutToThePlayer(s, Negotiation.BoughtWith(s, deal)) || Negotiation.FromACounter(s, deal))
                    && (deal.status == DealStatus.Active || deal.status == DealStatus.Accepted || deal.status == DealStatus.Fulfilled || deal.status == DealStatus.Broken);

        // SOURCE Gamesim.Simulation.DealResolution.BreachWeight 5035:5233
        public static double BreachWeight(EpisodeState s, DealState deal) =>
                    deal == null ? 0 : DealTrust.Weight(AcceptedOffer(s, deal) ? DealTrust.Heavier(deal.trustImpact) : deal.trustImpact);

        // SOURCE Gamesim.Simulation.DealResolution.Impact 3610:3783
        public static double Impact(DealState deal, string status) =>
                    (status == DealStatus.Fulfilled ? FulfilledBase : BrokenBase) * DealTrust.Weight(deal.trustImpact);

        // SOURCE Gamesim.Simulation.DealResolution.Impact 4144:4355
        public static double Impact(EpisodeState s, DealState deal, string status) =>
                    status == DealStatus.Fulfilled ? FulfilledBase * DealTrust.Weight(deal.trustImpact) : BrokenBase * BreachWeight(s, deal);

        // SOURCE Gamesim.Simulation.DealResolution.Partner 7275:7458
        public static string Partner(DealState deal, string whoId) =>
                    deal.proposerId == whoId ? deal.recipientId
                    : deal.recipientId == whoId ? deal.proposerId : null;

        // SOURCE Gamesim.Simulation.DealResolution.PutToThePlayer 6871:7175
        private static bool PutToThePlayer(EpisodeState s, DealState deal) =>
                    deal != null && deal.id != null && deal.recipientId == s.playerId
                    && (deal.id.StartsWith(NpcDeals.OfferPrefix, StringComparison.Ordinal) || deal.id.StartsWith(NpcDeals.VetoAskPrefix, StringComparison.Ordinal));
    }

    internal static class DealStatus
    {
        // SOURCE FIELD Gamesim.Simulation.DealStatus.Proposed @ Assets/Gamesim/Simulation/DealState.cs:9694
        public const string Proposed = "proposed";

        // SOURCE FIELD Gamesim.Simulation.DealStatus.Accepted @ Assets/Gamesim/Simulation/DealState.cs:9745
        public const string Accepted = "accepted";

        // SOURCE FIELD Gamesim.Simulation.DealStatus.Active @ Assets/Gamesim/Simulation/DealState.cs:9796
        public const string Active = "active";

        // SOURCE FIELD Gamesim.Simulation.DealStatus.Fulfilled @ Assets/Gamesim/Simulation/DealState.cs:9843
        public const string Fulfilled = "fulfilled";

        // SOURCE FIELD Gamesim.Simulation.DealStatus.Broken @ Assets/Gamesim/Simulation/DealState.cs:9896
        public const string Broken = "broken";

        // SOURCE FIELD Gamesim.Simulation.DealStatus.Declined @ Assets/Gamesim/Simulation/DealState.cs:9943
        public const string Declined = "declined";

        // SOURCE FIELD Gamesim.Simulation.DealStatus.Expired @ Assets/Gamesim/Simulation/DealState.cs:9994
        public const string Expired = "expired";

        // SOURCE FIELD Gamesim.Simulation.DealStatus.All @ Assets/Gamesim/Simulation/DealState.cs:10044
        public static readonly string[] All =
                    { Proposed, Accepted, Active, Fulfilled, Broken, Declined, Expired };

        // SOURCE Gamesim.Simulation.DealStatus.Binds 10347:10451
        public static bool Binds(string status) => status == Proposed || status == Accepted || status == Active;

        // SOURCE Gamesim.Simulation.DealStatus.IsKnown 10173:10268
        public static bool IsKnown(string status) => status != null && Array.IndexOf(All, status) >= 0;
    }

    internal static class DealTrust
    {
        // SOURCE FIELD Gamesim.Simulation.DealTrust.Low,Medium,High,Critical @ Assets/Gamesim/Simulation/DealState.cs:10507
        public const string Low = "low", Medium = "medium", High = "high", Critical = "critical";

        // SOURCE FIELD Gamesim.Simulation.DealTrust.All @ Assets/Gamesim/Simulation/DealState.cs:10605
        public static readonly string[] All = { Low, Medium, High, Critical };

        // SOURCE Gamesim.Simulation.DealTrust.Heavier 12213:12487
        public static string Heavier(string trust)
                {
                    switch (trust)
                    {
                        case Critical:
                        case High: return Critical;
                        case Medium: return High;
                        default: return Medium;
                    }
                }

        // SOURCE Gamesim.Simulation.DealTrust.IsKnown 10684:10776
        public static bool IsKnown(string trust) => trust != null && Array.IndexOf(All, trust) >= 0;

        // SOURCE Gamesim.Simulation.DealTrust.Weight 11417:11687
        public static double Weight(string trust)
                {
                    switch (trust)
                    {
                        case Critical: return 3;
                        case High: return 2;
                        case Medium: return 1.5;
                        default: return 1;
                    }
                }
    }

    internal static class EpisodeEngine
    {
        // SOURCE FIELD Gamesim.Simulation.EpisodeEngine.VetoSaveEvent,VetoReplacementEvent,VetoForcedEvent @ Assets/Gamesim/Simulation/EpisodeEngine.BetweenHouseguests.cs:1227
        public const string VetoSaveEvent = "veto-save", VetoReplacementEvent = "veto-replacement", VetoForcedEvent = "veto-forced";

        // SOURCE FIELD Gamesim.Simulation.EpisodeEngine.CampaignVisits @ Assets/Gamesim/Simulation/EpisodeEngine.Agency.cs:1941
        public const int CampaignVisits = 2;

        // SOURCE FIELD Gamesim.Simulation.EpisodeEngine.VetoLineupSize @ Assets/Gamesim/Simulation/EpisodeEngine.cs:173329
        public const int VetoLineupSize = 6;

        // SOURCE FIELD Gamesim.Simulation.EpisodeEngine.NpcConversationEvent,NpcGossipEvent @ Assets/Gamesim/Simulation/EpisodeNpcSocial.cs:13634
        public const string NpcConversationEvent = "npc-conversation", NpcGossipEvent = "npc-gossip";

        // SOURCE FIELD Gamesim.Simulation.EpisodeEngine.LegacySocialActionBudget @ Assets/Gamesim/Simulation/EpisodeEngine.cs:60620
        public const int LegacySocialActionBudget = 18;

        // SOURCE Gamesim.Simulation.EpisodeEngine.AgencyOn 3008:3132
        public static bool AgencyOn(EpisodeState s) => s != null && s.agencyRulesStartWeek >= 1 && s.week >= s.agencyRulesStartWeek;

        // SOURCE Gamesim.Simulation.EpisodeEngine.AllWeekRecord 1744:1944
        public static bool AllWeekRecord(string type) =>
                    type == NpcConversationEvent || type == NpcGossipEvent || type == VetoSaveEvent || type == VetoReplacementEvent || type == VetoForcedEvent;

        // SOURCE Gamesim.Simulation.EpisodeEngine.CommitmentRulesOn 2698:2851
        public static bool CommitmentRulesOn(EpisodeState s) =>
                    s != null && s.commitmentRulesStartWeek >= 1 && s.week >= s.commitmentRulesStartWeek;

        // SOURCE Gamesim.Simulation.EpisodeEngine.CompetitionPlayers 19801:20655
        public static IEnumerable<ContestantState> CompetitionPlayers(EpisodeState s)
                {
                    if (s.phase == EpisodePhase.Veto) return LiteralRows.Active(s).Where(c => s.vetoPlayers.Contains(c.id));
                    if (s.phase == EpisodePhase.FinalHoHPart2) return LiteralRows.Active(s).Where(c => c.id != s.finalPart1WinnerId);
                    if (s.phase == EpisodePhase.FinalHoHPart3) return LiteralRows.Active(s).Where(c => c.id == s.finalPart1WinnerId || c.id == s.finalPart2WinnerId);
                    // Production's penalty: somebody on their second strike sits out this week's HoH
                    // competition. Derived from the saved conduct record, so validation's recount agrees.
                    return LiteralRows.Active(s).Where(c => (LiteralRows.Active(s).Count() <= 3 || c.id != s.previousHohId)
                                               && !(StoryAt(s, StoryRules.Production) && Production.SitsOut(s, c.id)));
                }

        // SOURCE Gamesim.Simulation.EpisodeEngine.EconomyRulesOn 2530:2628
        public static bool EconomyRulesOn(EpisodeState s) => WeekRulesOn(s) && s.economyRulesVersion == 1;

        // SOURCE Gamesim.Simulation.EpisodeEngine.FinaleOn 805:929
        public static bool FinaleOn(EpisodeState s) => s != null && s.finaleRulesStartWeek >= 1 && s.week >= s.finaleRulesStartWeek;

        // SOURCE Gamesim.Simulation.EpisodeEngine.IsCompetition 18762:19003
        public static bool IsCompetition(EpisodePhase phase) => phase == EpisodePhase.HoH || phase == EpisodePhase.Veto ||
                    phase == EpisodePhase.FinalHoHPart1 || phase == EpisodePhase.FinalHoHPart2 || phase == EpisodePhase.FinalHoHPart3;

        // SOURCE Gamesim.Simulation.EpisodeEngine.IsFirstNight 1678:1856
        public static bool IsFirstNight(EpisodeState s) => s != null && s.phase == EpisodePhase.Social && s.week == 1
                    && string.IsNullOrEmpty(s.hohId) && !s.evictionResolved;

        // SOURCE Gamesim.Simulation.EpisodeEngine.MostActs 4116:4225
        public static int MostActs(EpisodeState s) => 4 * s.contestants.Count(c => !c.isPlayer) + 2 * CampaignVisits;

        // SOURCE Gamesim.Simulation.EpisodeEngine.StoryAt 1234:1339
        public static bool StoryAt(EpisodeState s, int version) => StoryOn(s) && s.story.rulesVersion >= version;

        // SOURCE Gamesim.Simulation.EpisodeEngine.StoryOn 968:1110
        public static bool StoryOn(EpisodeState s) =>
                    s?.story != null && s.story.rulesStartWeek >= 1 && s.week >= s.story.rulesStartWeek;

        // SOURCE Gamesim.Simulation.EpisodeEngine.VetoPlayerCount 173375:173492
        public static int VetoPlayerCount(int activeCount)
                    => Math.Min(VetoLineupSize, Math.Max(0, activeCount));

        // SOURCE Gamesim.Simulation.EpisodeEngine.Voters 61584:61720
        public static IEnumerable<ContestantState> Voters(EpisodeState s) => LiteralRows.Active(s).Where(c => c.id != s.hohId && !s.nominees.Contains(c.id));

        // SOURCE Gamesim.Simulation.EpisodeEngine.WeekRulesOn 1548:1671
        public static bool WeekRulesOn(EpisodeState s) => s != null && s.weekRulesStartWeek >= 1 && s.week >= s.weekRulesStartWeek;
    }

    internal static class FactKinds
    {
        // SOURCE FIELD Gamesim.Simulation.FactKinds.Alliance,Couple,BrokenWord,Strike @ Assets/Gamesim/Simulation/Story/StoryState.cs:21489
        public const string Alliance = "alliance", Couple = "couple", BrokenWord = "broken-word", Strike = "strike";

        // SOURCE FIELD Gamesim.Simulation.FactKinds.All @ Assets/Gamesim/Simulation/Story/StoryState.cs:21720
        public static readonly string[] All = { Alliance, Couple, BrokenWord, Strike, Outcome, Secret, Plan, Conversation };

        // SOURCE FIELD Gamesim.Simulation.FactKinds.Outcome,Secret,Plan,Conversation @ Assets/Gamesim/Simulation/Story/StoryState.cs:21606
        public const string Outcome = "outcome", Secret = "secret", Plan = "plan", Conversation = "conversation";

        // SOURCE Gamesim.Simulation.FactKinds.IsKnown 21845:21934
        public static bool IsKnown(string kind) => kind != null && Array.IndexOf(All, kind) >= 0;
    }

    internal static class FactVisibility
    {
        // SOURCE FIELD Gamesim.Simulation.FactVisibility.Private,Whispered,Known,Public @ Assets/Gamesim/Simulation/Story/StoryState.cs:21089
        public const string Private = "private", Whispered = "whispered", Known = "known", Public = "public";

        // SOURCE FIELD Gamesim.Simulation.FactVisibility.All @ Assets/Gamesim/Simulation/Story/StoryState.cs:21199
        public static readonly string[] All = { Private, Whispered, Known, Public };

        // SOURCE Gamesim.Simulation.FactVisibility.IsKnown 21284:21391
        public static bool IsKnown(string visibility) => visibility != null && Array.IndexOf(All, visibility) >= 0;
    }

    internal static class GrudgeCauses
    {
        // SOURCE FIELD Gamesim.Simulation.GrudgeCauses.All @ Assets/Gamesim/Simulation/Story/StoryState.cs:22479
        public static readonly string[] All =
                {
                    Nominated, Replacement, ReplacementVeto, AllianceBetrayed, DealBroken, PromiseBroken,
                    OathBroken, LieDiscovered, VotedAgainst, Story, PileOn,
                };

        // SOURCE FIELD Gamesim.Simulation.GrudgeCauses.Nominated,Replacement,ReplacementVeto @ Assets/Gamesim/Simulation/Story/StoryState.cs:22042
        public const string Nominated = "nominated", Replacement = "replacement", ReplacementVeto = "replacement-veto";

        // SOURCE FIELD Gamesim.Simulation.GrudgeCauses.AllianceBetrayed,DealBroken,PromiseBroken @ Assets/Gamesim/Simulation/Story/StoryState.cs:22162
        public const string AllianceBetrayed = "alliance-betrayed", DealBroken = "deal-broken", PromiseBroken = "promise-broken";

        // SOURCE FIELD Gamesim.Simulation.GrudgeCauses.OathBroken,LieDiscovered,VotedAgainst @ Assets/Gamesim/Simulation/Story/StoryState.cs:22292
        public const string OathBroken = "oath-broken", LieDiscovered = "lie-discovered", VotedAgainst = "voted-against";

        // SOURCE FIELD Gamesim.Simulation.GrudgeCauses.Story,PileOn @ Assets/Gamesim/Simulation/Story/StoryState.cs:22414
        public const string Story = "story", PileOn = "pile-on";

        // SOURCE Gamesim.Simulation.GrudgeCauses.IsKnown 22712:22804
        public static bool IsKnown(string cause) => cause != null && Array.IndexOf(All, cause) >= 0;
    }

    internal static class HaveNots
    {
        // SOURCE FIELD Gamesim.Simulation.HaveNots.Prizes @ Assets/Gamesim/Simulation/HaveNots.cs:8337
        public static readonly Prize[] Prizes =
                {
                    new Prize(CashId, "$5,000", "$5,000", "A cheque for $5,000. It buys nothing in the house.", false),
                    new Prize(PassId, "Have-Not pass", "a Have-Not pass", "Safe from next week's Have-Nots.", false),
                    new Prize(LuxuryId, "Luxury night", "a luxury night out of the house", "One more conversation until the end of next week.", false),
                };

        // SOURCE FIELD Gamesim.Simulation.HaveNots.Punishments @ Assets/Gamesim/Simulation/HaveNots.cs:8773
        public static readonly Prize[] Punishments =
                {
                    new Prize(PunishedId, "Have-Not", "a Have-Not next week, whatever the competition says", "A Have-Not next week, whatever the competition says.", true),
                    new Prize(AlarmId, "The alarm", "an alarm every hour of the night", "One fewer conversation until the end of next week.", true),
                    new Prize(CostumeId, "The costume", "a banana suit until the eviction", "A banana suit until the eviction.", true),
                };

        // SOURCE FIELD Gamesim.Simulation.HaveNots.CashId,PassId,LuxuryId @ Assets/Gamesim/Simulation/HaveNots.cs:7380
        public const string CashId = "cash", PassId = "have-not-pass", LuxuryId = "luxury-night";

        // SOURCE FIELD Gamesim.Simulation.HaveNots.PunishedId,AlarmId,CostumeId @ Assets/Gamesim/Simulation/HaveNots.cs:7478
        public const string PunishedId = "have-not", AlarmId = "the-alarm", CostumeId = "costume";

        // SOURCE Gamesim.Simulation.HaveNots.Find 9281:9387
        public static Prize Find(string id) => Prizes.Concat(Punishments).FirstOrDefault(prize => prize.Id == id);

        internal sealed class Prize
        {
            public string Id { get; }
        
            public string Title { get; }
        
            public string Line { get; }
        
            public string Effect { get; }
        
            public bool Punishment { get; }
        
            internal Prize(string id, string title, string line, string effect, bool punishment)
                        { Id = id; Title = title; Line = line; Effect = effect; Punishment = punishment; }
        }
        
    }

    internal static class HoHPitches
    {
        // SOURCE FIELD Gamesim.Simulation.HoHPitches.FeelOutKey @ Assets/Gamesim/Simulation/HoHPitches.cs:350
        public const string FeelOutKey = "feel-out";

        // SOURCE Gamesim.Simulation.HoHPitches.Available 403:719
        public static bool Available(EpisodeState s) => EpisodeEngine.EconomyRulesOn(s)
                    && StrategyRules.Apply(s) && EpisodeEngine.AgencyOn(s)
                    && s.phase == EpisodePhase.Nomination && s.nominees.Count == 0
                    && s.hohId == s.playerId && s.Find(s.playerId)?.status == ContestantStatus.Active;

        // SOURCE Gamesim.Simulation.HoHPitches.ValidCard 729:1360
        public static bool ValidCard(EpisodeState s, ReplyCardState card) => Available(s)
                    && card != null && card.kind == ReplyCards.Pitch && card.week == s.week
                    && card.fromId != s.playerId && s.Find(card.fromId)?.status == ContestantStatus.Active
                    && (card.aboutId == null || (card.aboutId != card.fromId && card.aboutId != s.playerId
                        && s.Find(card.aboutId)?.status == ContestantStatus.Active))
                    && !(s.ledger?.replies?.Any(r => r != null && r.week == card.week && r.kind == ReplyCards.Pitch
                        && r.fromId == card.fromId && r.replyKey != FeelOutKey) ?? false);
    }

    internal static class HouseEventKind
    {
        // SOURCE FIELD Gamesim.Simulation.HouseEventKind.Story @ Assets/Gamesim/Simulation/HouseEventState.cs:9662
        public const string Story = "story";

        // SOURCE FIELD Gamesim.Simulation.HouseEventKind.All @ Assets/Gamesim/Simulation/HouseEventState.cs:10165
        public static readonly string[] All = { Phase, House, Proximity, Ambient, Emergent, Crisis, Story, Activity };

        // SOURCE FIELD Gamesim.Simulation.HouseEventKind.Phase @ Assets/Gamesim/Simulation/HouseEventState.cs:8661
        public const string Phase = "phase";

        // SOURCE FIELD Gamesim.Simulation.HouseEventKind.House @ Assets/Gamesim/Simulation/HouseEventState.cs:8801
        public const string House = "house";

        // SOURCE FIELD Gamesim.Simulation.HouseEventKind.Proximity @ Assets/Gamesim/Simulation/HouseEventState.cs:8944
        public const string Proximity = "proximity";

        // SOURCE FIELD Gamesim.Simulation.HouseEventKind.Ambient @ Assets/Gamesim/Simulation/HouseEventState.cs:9104
        public const string Ambient = "ambient";

        // SOURCE FIELD Gamesim.Simulation.HouseEventKind.Emergent @ Assets/Gamesim/Simulation/HouseEventState.cs:9266
        public const string Emergent = "emergent";

        // SOURCE FIELD Gamesim.Simulation.HouseEventKind.Crisis @ Assets/Gamesim/Simulation/HouseEventState.cs:9403
        public const string Crisis = "crisis";

        // SOURCE FIELD Gamesim.Simulation.HouseEventKind.Activity @ Assets/Gamesim/Simulation/HouseEventState.cs:9936
        public const string Activity = "activity";

        // SOURCE Gamesim.Simulation.HouseEventKind.IsKnown 10284:10373
        public static bool IsKnown(string kind) => kind != null && Array.IndexOf(All, kind) >= 0;
    }

    internal static class HouseEventRisk
    {
        // SOURCE FIELD Gamesim.Simulation.HouseEventRisk.Low,Medium,High @ Assets/Gamesim/Simulation/HouseEventState.cs:7983
        public const string Low = "low", Medium = "medium", High = "high";

        // SOURCE FIELD Gamesim.Simulation.HouseEventRisk.All @ Assets/Gamesim/Simulation/HouseEventState.cs:8058
        public static readonly string[] All = { Low, Medium, High };

        // SOURCE Gamesim.Simulation.HouseEventRisk.IsKnown 8127:8216
        public static bool IsKnown(string risk) => risk != null && Array.IndexOf(All, risk) >= 0;
    }

    internal static class LobbyApproach
    {
        // SOURCE FIELD Gamesim.Simulation.LobbyApproach.All @ Assets/Gamesim/Simulation/StrategyRules.cs:33006
        public static readonly string[] All = { Emotional, Strategic, Deal, Pressure };

        // SOURCE FIELD Gamesim.Simulation.LobbyApproach.Emotional,Strategic,Deal,Pressure @ Assets/Gamesim/Simulation/StrategyRules.cs:32890
        public const string Emotional = "emotional", Strategic = "strategic", Deal = "deal", Pressure = "pressure";

        // SOURCE Gamesim.Simulation.LobbyApproach.IsKnown 33094:33195
        public static bool IsKnown(string approach) => approach != null && Array.IndexOf(All, approach) >= 0;
    }

    internal static class LobbyAsk
    {
        // SOURCE FIELD Gamesim.Simulation.LobbyAsk.All @ Assets/Gamesim/Simulation/StrategyRules.cs:32002
        public static readonly string[] All = { Spare, Target, Save, Keep, Vote };

        // SOURCE FIELD Gamesim.Simulation.LobbyAsk.Spare @ Assets/Gamesim/Simulation/StrategyRules.cs:31439
        public const string Spare = "spare";

        // SOURCE FIELD Gamesim.Simulation.LobbyAsk.Target @ Assets/Gamesim/Simulation/StrategyRules.cs:31565
        public const string Target = "target";

        // SOURCE FIELD Gamesim.Simulation.LobbyAsk.Save @ Assets/Gamesim/Simulation/StrategyRules.cs:31687
        public const string Save = "save";

        // SOURCE FIELD Gamesim.Simulation.LobbyAsk.Keep @ Assets/Gamesim/Simulation/StrategyRules.cs:31812
        public const string Keep = "keep";

        // SOURCE FIELD Gamesim.Simulation.LobbyAsk.Vote @ Assets/Gamesim/Simulation/StrategyRules.cs:31958
        public const string Vote = "vote";

        // SOURCE Gamesim.Simulation.LobbyAsk.IsKnown 32085:32171
        public static bool IsKnown(string ask) => ask != null && Array.IndexOf(All, ask) >= 0;
    }

    internal static class LobbyResponse
    {
        // SOURCE FIELD Gamesim.Simulation.LobbyResponse.All @ Assets/Gamesim/Simulation/StrategyRules.cs:35157
        public static readonly string[] All = { Receptive, Open, Skeptical, Hostile };

        // SOURCE FIELD Gamesim.Simulation.LobbyResponse.Receptive,Open,Skeptical,Hostile @ Assets/Gamesim/Simulation/StrategyRules.cs:35043
        public const string Receptive = "receptive", Open = "open", Skeptical = "skeptical", Hostile = "hostile";

        // SOURCE Gamesim.Simulation.LobbyResponse.IsKnown 35244:35345
        public static bool IsKnown(string response) => response != null && Array.IndexOf(All, response) >= 0;
    }

    internal static class LoreSources
    {
        // SOURCE FIELD Gamesim.Simulation.LoreSources.All @ Assets/Gamesim/Simulation/Story/StoryState.cs:23010
        public static readonly string[] All = { Authored, Legacy, Derived };

        // SOURCE FIELD Gamesim.Simulation.LoreSources.Authored,Legacy,Derived @ Assets/Gamesim/Simulation/Story/StoryState.cs:22919
        public const string Authored = "authored", Legacy = "legacy", Derived = "derived";

        // SOURCE Gamesim.Simulation.LoreSources.IsKnown 23087:23182
        public static bool IsKnown(string source) => source != null && Array.IndexOf(All, source) >= 0;
    }

    internal static class Negotiation
    {
        // SOURCE FIELD Gamesim.Simulation.Negotiation.CounterDealPrefix @ Assets/Gamesim/Simulation/Negotiation.cs:4038
        public const string CounterDealPrefix = "deal-counter-";

        // SOURCE FIELD Gamesim.Simulation.Negotiation.PricePrefix @ Assets/Gamesim/Simulation/Negotiation.cs:13441
        public const string PricePrefix = "deal-price-";

        // SOURCE Gamesim.Simulation.Negotiation.BoughtWith 15132:15244
        public static DealState BoughtWith(EpisodeState s, DealState price) => IsPrice(price) ? Linked(s, price) : null;

        // SOURCE Gamesim.Simulation.Negotiation.FromACounter 16671:17050
        public static bool FromACounter(EpisodeState s, DealState deal)
                {
                    if (deal?.id == null) return false;
                    if (deal.id.StartsWith(CounterDealPrefix, StringComparison.Ordinal)) return true;
                    var bought = BoughtWith(s, deal);
                    return bought?.id != null && bought.id.StartsWith(CounterDealPrefix, StringComparison.Ordinal);
                }

        // SOURCE Gamesim.Simulation.Negotiation.IsPrice 13872:13987
        public static bool IsPrice(DealState d) => d?.id != null && d.id.StartsWith(PricePrefix, StringComparison.Ordinal);

        // SOURCE Gamesim.Simulation.Negotiation.Linked 14267:14701
        public static DealState Linked(EpisodeState s, DealState d) =>
                    s?.deals == null || d == null || string.IsNullOrEmpty(d.linkedDealId) ? null
                        : UnifiedCommitments.RulesOn(s) ? CommitmentReferences.FindDeal(s, d.linkedDealId)
                        : UnifiedVoteStore.On(s) ? UnifiedVoteStore.Deals(s).FirstOrDefault(x => x.id == d.linkedDealId)
                        : s.deals.FirstOrDefault(x => x.id == d.linkedDealId);
    }

    internal static class NpcActKinds
    {
        // SOURCE FIELD Gamesim.Simulation.NpcActKinds.Pact,Promise,Meet @ Assets/Gamesim/Simulation/NpcActKinds.cs:1050
        public const string Pact = "pact", Promise = "promise", Meet = "meet";

        // SOURCE FIELD Gamesim.Simulation.NpcActKinds.All @ Assets/Gamesim/Simulation/NpcActKinds.cs:1321
        public static readonly string[] All = { Talk, Build, Hold, Court, Hunt, Campaign, Pact, Promise, Meet, Rumour, Confront, Eavesdrop };

        // SOURCE FIELD Gamesim.Simulation.NpcActKinds.Talk,Build,Hold,Court @ Assets/Gamesim/Simulation/NpcActKinds.cs:596
        public const string Talk = "talk", Build = "build", Hold = "hold", Court = "court";

        // SOURCE FIELD Gamesim.Simulation.NpcActKinds.Hunt @ Assets/Gamesim/Simulation/NpcActKinds.cs:778
        public const string Hunt = "hunt";

        // SOURCE FIELD Gamesim.Simulation.NpcActKinds.Campaign @ Assets/Gamesim/Simulation/NpcActKinds.cs:921
        public const string Campaign = "campaign";

        // SOURCE FIELD Gamesim.Simulation.NpcActKinds.Rumour,Confront,Eavesdrop @ Assets/Gamesim/Simulation/NpcActKinds.cs:1225
        public const string Rumour = "rumour", Confront = "confront", Eavesdrop = "eavesdrop";

        // SOURCE Gamesim.Simulation.NpcActKinds.IsKnown 1464:1553
        public static bool IsKnown(string kind) => kind != null && Array.IndexOf(All, kind) >= 0;
    }

    internal static class NpcSocialRules
    {
        // SOURCE Gamesim.Simulation.NpcSocialState.InitialRandomState 1688:1853
        public static uint InitialRandomState(uint seed) => SeededRandom.HashSeed(
                    "gamesim:npc-social:v1:" + seed.ToString("x8", CultureInfo.InvariantCulture));

        // SOURCE Gamesim.Simulation.NpcSocialState.IsEligible 3292:3622
        public static bool IsEligible(EpisodeState state) => state?.npcSocial != null &&
                    state.week >= state.npcSocial.rulesStartWeek && IsEligiblePhase(state.phase) && state.pendingDiary == null &&
                    state.Find(state.playerId)?.status == ContestantStatus.Active && LiteralRows.Active(state).Count(actor => !actor.isPlayer) >= 2;

        // SOURCE Gamesim.Simulation.NpcSocialState.IsEligiblePhase 3162:3283
        public static bool IsEligiblePhase(EpisodePhase phase) => phase == EpisodePhase.Social || phase == EpisodePhase.Campaign;

        // SOURCE Gamesim.Simulation.NpcSocialState.IsKnownRendezvous 4085:4318
        public static bool IsKnownRendezvous(string id) => id == "living-east-chat" || id == "kitchen-west-chat" ||
                    id == "bedroom-south-chat" || id == "yard-south-chat" || id == "kitchen-table-chat" || id == "yard-lounger-chat";

        // SOURCE Gamesim.Simulation.NpcSocialState.IsKnownTopic 3632:3875
        public static bool IsKnownTopic(string topic) => topic == "bonding" || topic == "strategy" || topic == "gossip" ||
                    topic == "tension" || topic == "casual" || topic == "nominations" || topic == "alliance_talk" || topic == "rivalry";
    }

    internal static class OpportunityKinds
    {
        // SOURCE FIELD Gamesim.Simulation.OpportunityKinds.Play,Lobby,Plea,Deal,Alliance,Vote,Comp,Read @ Assets/Gamesim/Simulation/SeasonLedger.cs:11755
        public const string Play = "play", Lobby = "lobby", Plea = "plea", Deal = "deal", Alliance = "alliance",
                    Vote = "vote", Comp = "comp", Read = "read";

        // SOURCE FIELD Gamesim.Simulation.OpportunityKinds.All @ Assets/Gamesim/Simulation/SeasonLedger.cs:11925
        public static readonly string[] All = { Play, Lobby, Plea, Deal, Alliance, Vote, Comp, Read };

        // SOURCE Gamesim.Simulation.OpportunityKinds.IsKnown 12028:12117
        public static bool IsKnown(string kind) => kind != null && Array.IndexOf(All, kind) >= 0;
    }

    internal static class OpportunityOutcome
    {
        // SOURCE FIELD Gamesim.Simulation.OpportunityOutcome.Won,Part,Lost,NotApplicable @ Assets/Gamesim/Simulation/SeasonLedger.cs:12544
        public const string Won = "won", Part = "part", Lost = "lost", NotApplicable = "na";

        // SOURCE FIELD Gamesim.Simulation.OpportunityOutcome.All @ Assets/Gamesim/Simulation/SeasonLedger.cs:12637
        public static readonly string[] All = { Won, Part, Lost, NotApplicable };

        // SOURCE Gamesim.Simulation.OpportunityOutcome.IsKnown 12719:12817
        public static bool IsKnown(string outcome) => outcome != null && Array.IndexOf(All, outcome) >= 0;
    }

    internal static class OpportunityResponse
    {
        // SOURCE FIELD Gamesim.Simulation.OpportunityResponse.Taken,Declined,Ignored,Expired @ Assets/Gamesim/Simulation/SeasonLedger.cs:12183
        public const string Taken = "taken", Declined = "declined", Ignored = "ignored", Expired = "expired";

        // SOURCE FIELD Gamesim.Simulation.OpportunityResponse.All @ Assets/Gamesim/Simulation/SeasonLedger.cs:12293
        public static readonly string[] All = { Taken, Declined, Ignored, Expired };

        // SOURCE Gamesim.Simulation.OpportunityResponse.IsKnown 12378:12479
        public static bool IsKnown(string response) => response != null && Array.IndexOf(All, response) >= 0;
    }

    internal static class PactPlanStance
    {
        // SOURCE FIELD Gamesim.Simulation.PactPlanStance.Open,Agreed,Countered,Low,Lapsed,Void @ Assets/Gamesim/Simulation/SeasonLedger.cs:9850
        public const string Open = "open", Agreed = "agreed", Countered = "countered", Low = "low", Lapsed = "lapsed", Void = "void";

        // SOURCE FIELD Gamesim.Simulation.PactPlanStance.All @ Assets/Gamesim/Simulation/SeasonLedger.cs:9984
        public static readonly string[] All = { Open, Agreed, Countered, Low, Lapsed, Void };

        // SOURCE Gamesim.Simulation.PactPlanStance.IsKnown 10078:10173
        public static bool IsKnown(string stance) => stance != null && Array.IndexOf(All, stance) >= 0;
    }

    internal static class PayoffCurrency
    {
        // SOURCE FIELD Gamesim.Simulation.PayoffCurrency.All @ Assets/Gamesim/Simulation/SeasonLedger.cs:12980
        public static readonly string[] All = { Trust, Intel, Alliance, Power };

        // SOURCE FIELD Gamesim.Simulation.PayoffCurrency.Trust,Intel,Alliance,Power @ Assets/Gamesim/Simulation/SeasonLedger.cs:12878
        public const string Trust = "trust", Intel = "intel", Alliance = "alliance", Power = "power";

        // SOURCE Gamesim.Simulation.PayoffCurrency.IsKnown 13061:13162
        public static bool IsKnown(string currency) => currency != null && Array.IndexOf(All, currency) >= 0;
    }

    internal static class Personality
    {
        internal static class Approach
        {
            // SOURCE FIELD Gamesim.Simulation.Personality.Approach.All @ Assets/Gamesim/Simulation/Story/Personality.cs:6396
            public static readonly string[] All = { Warm, Calculated, Bold, Candid, Playful, Yield, Hardball };
        
            // SOURCE FIELD Gamesim.Simulation.Personality.Approach.Warm,Calculated,Bold,Candid @ Assets/Gamesim/Simulation/Story/Personality.cs:6195
            public const string Warm = "warm", Calculated = "calculated", Bold = "bold", Candid = "candid";
        
            // SOURCE FIELD Gamesim.Simulation.Personality.Approach.Playful,Yield,Hardball @ Assets/Gamesim/Simulation/Story/Personality.cs:6303
            public const string Playful = "playful", Yield = "yield", Hardball = "hardball";
        
            // SOURCE Gamesim.Simulation.Personality.Approach.IsKnown 6508:6609
            public static bool IsKnown(string approach) => approach != null && Array.IndexOf(All, approach) >= 0;
        }
        
    }

    internal static class Production
    {
        // SOURCE Gamesim.Simulation.Production.For 23458:23839
        internal static ConductState ForReadOnly(EpisodeState state, string id)
                {
                    // Specialization of the sole reached call For(state, id, false).
                    var row = state?.story?.conduct.FirstOrDefault(c => c.contestantId == id);
                    return row;
                }

        // SOURCE Gamesim.Simulation.Production.SitsOut 24292:24467
        public static bool SitsOut(EpisodeState state, string id) =>
                    state.phase == EpisodePhase.HoH && ForReadOnly(state, id)?.sitsOutWeek == state.week && state.week > 0;
    }

    internal static class ReplyCards
    {
        // SOURCE FIELD Gamesim.Simulation.ReplyCards.Confrontation,Gossip,Plea,Pitch @ Assets/Gamesim/Simulation/ReplyCards.cs:1203
        public const string Confrontation = "confrontation", Gossip = "gossip", Plea = "plea", Pitch = "pitch";

        // SOURCE FIELD Gamesim.Simulation.ReplyCards.All @ Assets/Gamesim/Simulation/ReplyCards.cs:1521
        public static readonly string[] All = { Confrontation, Gossip, Plea, Pitch };

        // SOURCE FIELD Gamesim.Simulation.ReplyCards.Confronting @ Assets/Gamesim/Simulation/ReplyCards.cs:2766
        private static readonly Reply[] Confronting =
                {
                    new Reply { Key = "apologize", Label = "Apologize", Description = "Take responsibility and try to mend things", ToThem = 10 },
                    new Reply { Key = "deflect", Label = "Deflect", Description = "Redirect the conversation without admitting fault", ToThem = -2 },
                    new Reply { Key = "escalate", Label = "Escalate", Description = "Stand your ground and push back hard", ToThem = -15, Risk = HouseEventRisk.High },
                };

        // SOURCE FIELD Gamesim.Simulation.ReplyCards.Gossiping @ Assets/Gamesim/Simulation/ReplyCards.cs:3283
        private static readonly Reply[] Gossiping =
                {
                    new Reply { Key = "confront", Label = "Confront them", Description = "Show them you won't tolerate gossip", ToThem = -15, Risk = HouseEventRisk.High },
                    new Reply { Key = "slide", Label = "Let it slide", Description = "Keep the peace, but remember", ToThem = -5 },
                    new Reply { Key = "gossip-back", Label = "Gossip back", Description = "Fight fire with fire (risky)", ToThem = -10, ToListener = GossipBackReach, Risk = HouseEventRisk.High },
                };

        // SOURCE FIELD Gamesim.Simulation.ReplyCards.Pleading @ Assets/Gamesim/Simulation/ReplyCards.cs:3833
        private static readonly Reply[] Pleading =
                {
                    new Reply { Key = "promise", Label = "Promise support", Description = "Agree to help them — builds trust, and it is a promise", ToThem = 8, Promises = true, Risk = HouseEventRisk.Medium },
                    new Reply { Key = "noncommittal", Label = "Stay noncommittal", Description = "Don't promise anything yet", ToThem = 0 },
                    new Reply { Key = "refuse", Label = "Refuse", Description = "Tell them you can't help", ToThem = -5, Risk = HouseEventRisk.Medium },
                };

        // SOURCE FIELD Gamesim.Simulation.ReplyCards.Pitching @ Assets/Gamesim/Simulation/ReplyCards.cs:4483
        private static readonly Reply[] Pitching =
                {
                    new Reply { Key = "promise-safety", Label = "Promise safety", Description = "Promise them safety; nominations can break your word", ToThem = 8, Risk = HouseEventRisk.Medium },
                    new Reply { Key = "hear", Label = "Hear them without committing", Description = "Hear the recommendation. No new promise; existing promises still bind", ToThem = 0 },
                    new Reply { Key = "turn-down", Label = "Turn them down", Description = "Decline and lose trust. Existing promises still bind", ToThem = -5, Risk = HouseEventRisk.Medium },
                };

        // SOURCE FIELD Gamesim.Simulation.ReplyCards.GossipBackReach @ Assets/Gamesim/Simulation/ReplyCards.cs:2005
        public const double GossipBackReach = -5;

        // SOURCE Gamesim.Simulation.ReplyCards.Find 5467:5619
        public static Reply Find(string kind, string key) =>
                    Replies(kind).FirstOrDefault(r => string.Equals(r.Key, key, StringComparison.Ordinal));

        // SOURCE Gamesim.Simulation.ReplyCards.IsKnown 1607:1696
        public static bool IsKnown(string kind) => kind != null && Array.IndexOf(All, kind) >= 0;

        // SOURCE Gamesim.Simulation.ReplyCards.Replies 5204:5395
        public static Reply[] Replies(string kind) =>
                    kind == Confrontation ? Confronting : kind == Gossip ? Gossiping : kind == Plea ? Pleading : kind == Pitch ? Pitching : new Reply[0];

        internal sealed class Reply
        {
            // SOURCE FIELD Gamesim.Simulation.ReplyCards.Reply.Key,Label,Description @ Assets/Gamesim/Simulation/ReplyCards.cs:2157
            public string Key, Label, Description;
        
            // SOURCE FIELD Gamesim.Simulation.ReplyCards.Reply.ToThem @ Assets/Gamesim/Simulation/ReplyCards.cs:2313
            public double ToThem;
        
            // SOURCE FIELD Gamesim.Simulation.ReplyCards.Reply.ToListener @ Assets/Gamesim/Simulation/ReplyCards.cs:2451
            public double ToListener;
        
            // SOURCE FIELD Gamesim.Simulation.ReplyCards.Reply.Promises @ Assets/Gamesim/Simulation/ReplyCards.cs:2573
            public bool Promises;
        
            // SOURCE FIELD Gamesim.Simulation.ReplyCards.Reply.Risk @ Assets/Gamesim/Simulation/ReplyCards.cs:2706
            public string Risk = HouseEventRisk.Low;
        
            // Explicit spelling of the source implicit parameterless constructor.
            public Reply() { }
        }
        
    }

    internal static class RoomWords
    {
        // SOURCE FIELD Gamesim.Simulation.RoomWords.Rooms @ Assets/Gamesim/Simulation/RoomWords.cs:787
        public static readonly string[] Rooms = { "Living", "Kitchen", "Bedroom", "Private", "Yard", "HoH", "Nomination", "Games" };

        // SOURCE Gamesim.Simulation.RoomWords.IsRoom 1003:1106
        public static bool IsRoom(string roomId) => roomId != null && System.Array.IndexOf(Rooms, roomId) >= 0;
    }

    internal static class SeededRandom
    {
        // SOURCE Gamesim.Simulation.SeededRandom.HashSeed 1538:1883
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

    internal static class StoryAnchors
    {
        // SOURCE FIELD Gamesim.Simulation.StoryAnchors.All @ Assets/Gamesim/Simulation/Story/StoryState.cs:14615
        public static readonly string[] All =
                    { HohCrowned, NomsSet, VetoWon, BlockSet, EvictionEve, EvictionNight, SocialClose, Conversation };

        // SOURCE FIELD Gamesim.Simulation.StoryAnchors.HohCrowned @ Assets/Gamesim/Simulation/Story/StoryState.cs:14169
        public const string HohCrowned = "hoh-crowned";

        // SOURCE FIELD Gamesim.Simulation.StoryAnchors.NomsSet @ Assets/Gamesim/Simulation/Story/StoryState.cs:14225
        public const string NomsSet = "noms-set";

        // SOURCE FIELD Gamesim.Simulation.StoryAnchors.VetoWon @ Assets/Gamesim/Simulation/Story/StoryState.cs:14275
        public const string VetoWon = "veto-won";

        // SOURCE FIELD Gamesim.Simulation.StoryAnchors.BlockSet @ Assets/Gamesim/Simulation/Story/StoryState.cs:14325
        public const string BlockSet = "block-set";

        // SOURCE FIELD Gamesim.Simulation.StoryAnchors.EvictionEve @ Assets/Gamesim/Simulation/Story/StoryState.cs:14377
        public const string EvictionEve = "eviction-eve";

        // SOURCE FIELD Gamesim.Simulation.StoryAnchors.EvictionNight @ Assets/Gamesim/Simulation/Story/StoryState.cs:14435
        public const string EvictionNight = "eviction-night";

        // SOURCE FIELD Gamesim.Simulation.StoryAnchors.SocialClose @ Assets/Gamesim/Simulation/Story/StoryState.cs:14497
        public const string SocialClose = "social-close";

        // SOURCE FIELD Gamesim.Simulation.StoryAnchors.Conversation @ Assets/Gamesim/Simulation/Story/StoryState.cs:14555
        public const string Conversation = "conversation";

        // SOURCE Gamesim.Simulation.StoryAnchors.IsKnown 14773:14868
        public static bool IsKnown(string anchor) => anchor != null && Array.IndexOf(All, anchor) >= 0;
    }

    internal static class StoryEffects
    {
        // SOURCE FIELD Gamesim.Simulation.StoryEffects.Picked @ Assets/Gamesim/Simulation/Story/StoryEffects.cs:651
        public const string Picked = "{PICK}";

        // SOURCE FIELD Gamesim.Simulation.StoryEffects.All @ Assets/Gamesim/Simulation/Story/StoryEffects.cs:6006
        public static readonly string[] All =
                {
                    Move, View, Receipt, Grudge, Ease, Bond, BondEnd, Alliance, AllianceEnd, Promise, Deal,
                    Fact, Spread, Reveal, Hook, Modifier, Stress, Strike, HaveNot, Memory, Reckoning, Expel,
                    Var, Backdoor, Told, Trust, PhaseBonus, HookSpend, Snub, AllianceLeave, PushBack,
                };

        // SOURCE FIELD Gamesim.Simulation.StoryEffects.Move @ Assets/Gamesim/Simulation/Story/StoryEffects.cs:826
        public const string Move = "move";

        // SOURCE FIELD Gamesim.Simulation.StoryEffects.View @ Assets/Gamesim/Simulation/Story/StoryEffects.cs:986
        public const string View = "view";

        // SOURCE FIELD Gamesim.Simulation.StoryEffects.Receipt @ Assets/Gamesim/Simulation/Story/StoryEffects.cs:1173
        public const string Receipt = "receipt";

        // SOURCE FIELD Gamesim.Simulation.StoryEffects.Grudge @ Assets/Gamesim/Simulation/Story/StoryEffects.cs:1353
        public const string Grudge = "grudge";

        // SOURCE FIELD Gamesim.Simulation.StoryEffects.Ease @ Assets/Gamesim/Simulation/Story/StoryEffects.cs:1467
        public const string Ease = "grudge-ease";

        // SOURCE FIELD Gamesim.Simulation.StoryEffects.Bond @ Assets/Gamesim/Simulation/Story/StoryEffects.cs:1637
        public const string Bond = "bond";

        // SOURCE FIELD Gamesim.Simulation.StoryEffects.BondEnd @ Assets/Gamesim/Simulation/Story/StoryEffects.cs:1787
        public const string BondEnd = "bond-end";

        // SOURCE FIELD Gamesim.Simulation.StoryEffects.Alliance @ Assets/Gamesim/Simulation/Story/StoryEffects.cs:1962
        public const string Alliance = "alliance";

        // SOURCE FIELD Gamesim.Simulation.StoryEffects.AllianceEnd @ Assets/Gamesim/Simulation/Story/StoryEffects.cs:2086
        public const string AllianceEnd = "alliance-end";

        // SOURCE FIELD Gamesim.Simulation.StoryEffects.Promise @ Assets/Gamesim/Simulation/Story/StoryEffects.cs:2235
        public const string Promise = "promise";

        // SOURCE FIELD Gamesim.Simulation.StoryEffects.Deal @ Assets/Gamesim/Simulation/Story/StoryEffects.cs:2425
        public const string Deal = "deal";

        // SOURCE FIELD Gamesim.Simulation.StoryEffects.Fact @ Assets/Gamesim/Simulation/Story/StoryEffects.cs:2589
        public const string Fact = "fact";

        // SOURCE FIELD Gamesim.Simulation.StoryEffects.Spread @ Assets/Gamesim/Simulation/Story/StoryEffects.cs:2706
        public const string Spread = "fact-spread";

        // SOURCE FIELD Gamesim.Simulation.StoryEffects.Reveal @ Assets/Gamesim/Simulation/Story/StoryEffects.cs:2874
        public const string Reveal = "reveal";

        // SOURCE FIELD Gamesim.Simulation.StoryEffects.Hook @ Assets/Gamesim/Simulation/Story/StoryEffects.cs:2999
        public const string Hook = "hook";

        // SOURCE FIELD Gamesim.Simulation.StoryEffects.Modifier @ Assets/Gamesim/Simulation/Story/StoryEffects.cs:3149
        public const string Modifier = "modifier";

        // SOURCE FIELD Gamesim.Simulation.StoryEffects.Stress @ Assets/Gamesim/Simulation/Story/StoryEffects.cs:3293
        public const string Stress = "stress";

        // SOURCE FIELD Gamesim.Simulation.StoryEffects.Strike @ Assets/Gamesim/Simulation/Story/StoryEffects.cs:3420
        public const string Strike = "strike";

        // SOURCE FIELD Gamesim.Simulation.StoryEffects.HaveNot @ Assets/Gamesim/Simulation/Story/StoryEffects.cs:3564
        public const string HaveNot = "have-not";

        // SOURCE FIELD Gamesim.Simulation.StoryEffects.Memory @ Assets/Gamesim/Simulation/Story/StoryEffects.cs:3726
        public const string Memory = "memory";

        // SOURCE FIELD Gamesim.Simulation.StoryEffects.Reckoning @ Assets/Gamesim/Simulation/Story/StoryEffects.cs:3851
        public const string Reckoning = "reckoning";

        // SOURCE FIELD Gamesim.Simulation.StoryEffects.Expel @ Assets/Gamesim/Simulation/Story/StoryEffects.cs:3991
        public const string Expel = "expel";

        // SOURCE FIELD Gamesim.Simulation.StoryEffects.Var @ Assets/Gamesim/Simulation/Story/StoryEffects.cs:4121
        public const string Var = "var";

        // SOURCE FIELD Gamesim.Simulation.StoryEffects.Backdoor @ Assets/Gamesim/Simulation/Story/StoryEffects.cs:4238
        public const string Backdoor = "backdoor";

        // SOURCE FIELD Gamesim.Simulation.StoryEffects.Told @ Assets/Gamesim/Simulation/Story/StoryEffects.cs:4403
        public const string Told = "told";

        // SOURCE FIELD Gamesim.Simulation.StoryEffects.Trust @ Assets/Gamesim/Simulation/Story/StoryEffects.cs:4690
        public const string Trust = "trust";

        // SOURCE FIELD Gamesim.Simulation.StoryEffects.PhaseBonus @ Assets/Gamesim/Simulation/Story/StoryEffects.cs:5052
        public const string PhaseBonus = "phase-bonus";

        // SOURCE FIELD Gamesim.Simulation.StoryEffects.HookSpend @ Assets/Gamesim/Simulation/Story/StoryEffects.cs:5209
        public const string HookSpend = "hook-spend";

        // SOURCE FIELD Gamesim.Simulation.StoryEffects.Snub @ Assets/Gamesim/Simulation/Story/StoryEffects.cs:5493
        public const string Snub = "snub";

        // SOURCE FIELD Gamesim.Simulation.StoryEffects.AllianceLeave @ Assets/Gamesim/Simulation/Story/StoryEffects.cs:5770
        public const string AllianceLeave = "alliance-leave";

        // SOURCE FIELD Gamesim.Simulation.StoryEffects.PushBack @ Assets/Gamesim/Simulation/Story/StoryEffects.cs:5953
        public const string PushBack = "push-back";

        // SOURCE Gamesim.Simulation.StoryEffects.IsKnown 6369:6458
        public static bool IsKnown(string kind) => kind != null && Array.IndexOf(All, kind) >= 0;
    }

    internal static class StoryLanes
    {
        // SOURCE FIELD Gamesim.Simulation.StoryLanes.All @ Assets/Gamesim/Simulation/Story/StoryState.cs:13337
        public static readonly string[] All = { Personal, Conflict, Game, Production, Moment, Play, Thread };

        // SOURCE FIELD Gamesim.Simulation.StoryLanes.Personal,Conflict,Game,Production @ Assets/Gamesim/Simulation/Story/StoryState.cs:12939
        public const string Personal = "personal", Conflict = "conflict", Game = "game", Production = "production";

        // SOURCE FIELD Gamesim.Simulation.StoryLanes.Moment,Play,Thread @ Assets/Gamesim/Simulation/Story/StoryState.cs:13055
        public const string Moment = "moment", Play = "play", Thread = "thread";

        // SOURCE Gamesim.Simulation.StoryLanes.IsKnown 13447:13536
        public static bool IsKnown(string lane) => lane != null && Array.IndexOf(All, lane) >= 0;
    }

    internal static class StorylineStatus
    {
        // SOURCE FIELD Gamesim.Simulation.StorylineStatus.Active @ Assets/Gamesim/Simulation/StorylineState.cs:3344
        public const string Active = "active";

        // SOURCE FIELD Gamesim.Simulation.StorylineStatus.All @ Assets/Gamesim/Simulation/StorylineState.cs:3886
        public static readonly string[] All = { Active, Completed, Abandoned };

        // SOURCE FIELD Gamesim.Simulation.StorylineStatus.Completed @ Assets/Gamesim/Simulation/StorylineState.cs:3391
        public const string Completed = "completed";

        // SOURCE FIELD Gamesim.Simulation.StorylineStatus.Abandoned @ Assets/Gamesim/Simulation/StorylineState.cs:3832
        public const string Abandoned = "abandoned";

        // SOURCE Gamesim.Simulation.StorylineStatus.IsKnown 3966:4061
        public static bool IsKnown(string status) => status != null && Array.IndexOf(All, status) >= 0;

        // SOURCE Gamesim.Simulation.StorylineStatus.Running 4070:4132
        public static bool Running(string status) => status == Active;
    }

    internal static class StoryResults
    {
        // SOURCE FIELD Gamesim.Simulation.StoryResults.All @ Assets/Gamesim/Simulation/Story/StoryState.cs:19413
        public static readonly string[] All = { Plain, Success, Backfire, Lapsed, Npc };

        // SOURCE FIELD Gamesim.Simulation.StoryResults.Plain,Success,Backfire,Lapsed,Npc @ Assets/Gamesim/Simulation/Story/StoryState.cs:19292
        public const string Plain = "plain", Success = "success", Backfire = "backfire", Lapsed = "lapsed", Npc = "npc";

        // SOURCE Gamesim.Simulation.StoryResults.IsKnown 19502:19597
        public static bool IsKnown(string result) => result != null && Array.IndexOf(All, result) >= 0;
    }

    internal static class StorySurfaces
    {
        // SOURCE FIELD Gamesim.Simulation.StorySurfaces.All @ Assets/Gamesim/Simulation/Story/StoryState.cs:16347
        public static readonly string[] All = { Scene, Approach, Conversation, Summons, Meeting, Npc };

        // SOURCE FIELD Gamesim.Simulation.StorySurfaces.Scene @ Assets/Gamesim/Simulation/Story/StoryState.cs:15694
        public const string Scene = "scene";

        // SOURCE FIELD Gamesim.Simulation.StorySurfaces.Approach @ Assets/Gamesim/Simulation/Story/StoryState.cs:15804
        public const string Approach = "approach";

        // SOURCE FIELD Gamesim.Simulation.StorySurfaces.Conversation @ Assets/Gamesim/Simulation/Story/StoryState.cs:15938
        public const string Conversation = "conversation";

        // SOURCE FIELD Gamesim.Simulation.StorySurfaces.Summons @ Assets/Gamesim/Simulation/Story/StoryState.cs:16050
        public const string Summons = "summons";

        // SOURCE FIELD Gamesim.Simulation.StorySurfaces.Meeting @ Assets/Gamesim/Simulation/Story/StoryState.cs:16166
        public const string Meeting = "meeting";

        // SOURCE FIELD Gamesim.Simulation.StorySurfaces.Npc @ Assets/Gamesim/Simulation/Story/StoryState.cs:16305
        public const string Npc = "npc";

        // SOURCE Gamesim.Simulation.StorySurfaces.IsKnown 16451:16549
        public static bool IsKnown(string surface) => surface != null && Array.IndexOf(All, surface) >= 0;
    }

    internal static class StrategyRules
    {
        // SOURCE FIELD Gamesim.Simulation.StrategyRules.MostInfluence @ Assets/Gamesim/Simulation/StrategyRules.cs:2775
        public const double MostInfluence = 100;

        // SOURCE Gamesim.Simulation.StrategyRules.Apply 2449:2573
        public static bool Apply(EpisodeState s) => s != null && s.strategyRulesStartWeek > 0 && s.week >= s.strategyRulesStartWeek;
    }

    internal static class UnifiedCommitments
    {
        // SOURCE FIELD Gamesim.Simulation.UnifiedCommitments.ProspectiveVersion,FamilyCapacity @ Assets/Gamesim/Simulation/UnifiedCommitments.cs:4103
        public const int ProspectiveVersion = 1, FamilyCapacity = 200;

        // SOURCE FIELD Gamesim.Simulation.UnifiedCommitments.Safety,PromisePolicy,DealPolicy @ Assets/Gamesim/Simulation/UnifiedCommitments.cs:4174
        public const string Safety = "safety", PromisePolicy = "promise", DealPolicy = "deal";

        // SOURCE FIELD Gamesim.Simulation.UnifiedCommitments.PlayerPromise,NpcPromise,StoryPromise,HoHPitch @ Assets/Gamesim/Simulation/UnifiedCommitments.cs:4269
        public const string PlayerPromise = "player-promise", NpcPromise = "npc-promise", StoryPromise = "story-promise", HoHPitch = "hoh-pitch";

        // SOURCE FIELD Gamesim.Simulation.UnifiedCommitments.PlayerDeal,NpcDeal,NpcOffer,Lobby,StoryDeal @ Assets/Gamesim/Simulation/UnifiedCommitments.cs:4415
        public const string PlayerDeal = "player-deal", NpcDeal = "npc-deal", NpcOffer = "npc-offer", Lobby = "lobby", StoryDeal = "story-deal";

        // SOURCE FIELD Gamesim.Simulation.UnifiedCommitments.CounterDeal,CounterPrice @ Assets/Gamesim/Simulation/UnifiedCommitments.cs:4560
        public const string CounterDeal = "counter-deal", CounterPrice = "counter-price";

        // SOURCE Gamesim.Simulation.UnifiedCommitments.IsDealOrigin 5749:5968
        internal static bool IsDealOrigin(string origin) => Gamesim.Persistence.Frozen28Semantics.FrozenV28SafetyStorage.IsDealOrigin(origin);

        // SOURCE Gamesim.Simulation.UnifiedCommitments.IsPromiseOrigin 5591:5740
        internal static bool IsPromiseOrigin(string origin) => Gamesim.Persistence.Frozen28Semantics.FrozenV28SafetyStorage.IsPromiseOrigin(origin);

        // SOURCE Gamesim.Simulation.UnifiedCommitments.RulesOn 4651:4776
        internal static bool RulesOn(EpisodeState state) => Gamesim.Persistence.Frozen28Semantics.FrozenV28SafetyStorage.RulesOn(state);

        // SOURCE Gamesim.Simulation.UnifiedCommitments.SafetyAuthorityOn 5338:5581
        internal static bool SafetyAuthorityOn(EpisodeState state) => Gamesim.Persistence.Frozen28Semantics.FrozenV28SafetyStorage.SafetyAuthorityOn(state);

        // SOURCE Gamesim.Simulation.UnifiedCommitments.SourceConsequence 25836:26418
        internal static double SourceConsequence(UnifiedCommitmentState row, bool fulfilled)
                {
                    if (row.sourcePolicy == PromisePolicy) return WebRules.PromiseImpact(Safety, fulfilled ? DealStatus.Fulfilled : DealStatus.Broken);
                    bool accepted = row.origin == NpcOffer || row.origin == CounterDeal || row.origin == CounterPrice;
                    string trust = !fulfilled && accepted ? DealTrust.Heavier(row.trustImpact) : row.trustImpact;
                    return (fulfilled ? DealResolution.FulfilledBase : DealResolution.BrokenBase) * DealTrust.Weight(trust);
                }

        // SOURCE Gamesim.Simulation.UnifiedCommitments.ValidateRecords 10867:12938
        internal static bool ValidateRecords(EpisodeState state, out string error) => Gamesim.Persistence.Frozen28Semantics.FrozenV28SafetyStorage.ValidateRecords(state, out error);

        // SOURCE Gamesim.Simulation.UnifiedCommitments.ValidateRow 6220:6536
        internal static bool ValidateRow(EpisodeState state, UnifiedCommitmentState row, out string error) => Gamesim.Persistence.Frozen28Semantics.FrozenV28SafetyStorage.ValidateRow(state, row, out error);

        // SOURCE Gamesim.Simulation.UnifiedCommitments.ValidateSafetyAuthority 13709:15673
        internal static bool ValidateSafetyAuthority(EpisodeState state, out string error) => Gamesim.Persistence.Frozen28Semantics.FrozenV28SafetyStorage.ValidateSafetyAuthority(state, out error);

        // SOURCE Gamesim.Simulation.UnifiedCommitments.ValidateSafetyRowCore 6728:10857
        internal static bool ValidateSafetyRowCore(EpisodeState state, UnifiedCommitmentState row, out string error) => Gamesim.Persistence.Frozen28Semantics.FrozenV28SafetyStorage.ValidateSafetyRowCore(state, row, out error);
    }

    internal static class UnifiedVoteReferences
    {
        // SOURCE Gamesim.Simulation.UnifiedVoteReferences.DealsUnchecked 806:1103
        internal static IReadOnlyList<DealState> DealsUnchecked(EpisodeState s) =>
                    Array.AsReadOnly(s.deals.Select(row => row.Clone()).Concat(s.unifiedCommitments
                        .Where(row => row.sourcePolicy == UnifiedCommitments.DealPolicy)
                        .Select(ProjectDeal)).ToArray());

        // SOURCE Gamesim.Simulation.UnifiedVoteReferences.ProjectDeal 1624:2192
        internal static DealState ProjectDeal(UnifiedCommitmentState row) => Gamesim.Persistence.Frozen28Semantics.FrozenV28VoteReferences.ProjectDeal(row);

        // SOURCE Gamesim.Simulation.UnifiedVoteReferences.ProjectPromise 1113:1614
        internal static PromiseState ProjectPromise(UnifiedCommitmentState row) => Gamesim.Persistence.Frozen28Semantics.FrozenV28VoteReferences.ProjectPromise(row);
    }

    internal static class WebJuryQuestioning
    {
        // SOURCE Gamesim.Simulation.WebJuryQuestioning.GetJurorQuestionOptions 5060:5793
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

        // SOURCE Gamesim.Simulation.WebJuryQuestioning.GetOpponentAnswer 3395:3626
        public static string GetOpponentAnswer(int jurorIndex)
                {
                    RequireIndex(jurorIndex);
                    return WebJuryQuestioningCatalog.NpcResponses[jurorIndex % WebJuryQuestioningCatalog.NpcResponses.Length];
                }

        // SOURCE Gamesim.Simulation.WebJuryQuestioning.GetPrimaryTrait 1252:1549
        public static string GetPrimaryTrait(IReadOnlyList<string> traits)
                {
                    // JavaScript hg.traits?.[0] || 'Strategic': do not trim or inspect later traits.
                    return traits == null || traits.Count == 0 || string.IsNullOrEmpty(traits[0]) ? "Strategic" : traits[0];
                }

        // SOURCE Gamesim.Simulation.WebJuryQuestioning.GetResponsePair 1559:2265
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

        // SOURCE Gamesim.Simulation.WebJuryQuestioning.MatchesSource 6483:7233
        public static bool MatchesSource(WebJuryQuestion question, IReadOnlyList<string> jurorTraits, int jurorIndex)
                {
                    if (question == null || jurorIndex < 0 || (question.correctIs != "A" && question.correctIs != "B")
                        || question.tone == null || !WebJuryQuestioningCatalog.Questions.TryGetValue(question.tone, out var questions)) return false;
                    var pair = GetResponsePair(jurorTraits, jurorIndex, question.correctIs == "A" ? 0.75 : 0.25);
                    return question.question == questions[jurorIndex % questions.Length] && question.optionA == pair.optionA
                        && question.optionB == pair.optionB && question.trait == pair.trait && question.opponentAnswer == GetOpponentAnswer(jurorIndex);
                }

        // SOURCE Gamesim.Simulation.WebJuryQuestioning.RequireIndex 7243:7387
        private static void RequireIndex(int index)
                {
                    if (index < 0) throw new ArgumentOutOfRangeException(nameof(index));
                }

        // SOURCE Gamesim.Simulation.WebJuryQuestioning.RequireRoll 7397:7655
        private static void RequireRoll(double roll)
                {
                    if (double.IsNaN(roll) || double.IsInfinity(roll) || roll < 0 || roll >= 1)
                        throw new ArgumentOutOfRangeException(nameof(roll), "Random samples must be in [0, 1).");
                }
    }

    internal static class WebRelationshipArcs
    {
        // SOURCE FIELD Gamesim.Simulation.WebRelationshipArcs.Thresholds @ Assets/Gamesim/Simulation/WebRelationshipArcs.cs:1240
        private static readonly double[] Thresholds = { 0, 25, 50, 75, 90 };

        // SOURCE Gamesim.Simulation.WebRelationshipArcs.GetEscalationLevel 1426:1625
        public static int GetEscalationLevel(double intensity)
                {
                    for (int i = Thresholds.Length - 1; i >= 0; i--) if (intensity >= Thresholds[i]) return i;
                    return 0;
                }
    }

    internal static class WebRules
    {
        // SOURCE Gamesim.Simulation.WebRules.JsRound 539:783
        public static double JsRound(double value)
                {
                    if (double.IsNaN(value) || double.IsInfinity(value)) return value;
                    var floor = Math.Floor(value);
                    return value - floor < 0.5 ? floor : floor + 1;
                }

        // SOURCE Gamesim.Simulation.WebRules.PromiseImpact 3775:4784
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

        // SOURCE Gamesim.Simulation.WebRules.PromiseImpact 4794:5505
        public static double PromiseImpact(PromiseKind type, PromiseStatus status, double loyalty = 5)
                {
                    string name;
                    switch (type)
                    {
                        case PromiseKind.Safety: name = "safety"; break;
                        case PromiseKind.Vote: name = "vote"; break;
                        case PromiseKind.FinalTwo: name = "final_2"; break;
                        case PromiseKind.AllianceLoyalty: name = "alliance_loyalty"; break;
                        case PromiseKind.Information: name = "information"; break;
                        default: throw new ArgumentOutOfRangeException(nameof(type));
                    }
                    return PromiseImpact(name, status.ToString().ToLowerInvariant(), loyalty);
                }
    }

    internal static class YourWord
    {
        // SOURCE Gamesim.Simulation.YourWord.On 4427:4574
        public static bool On(EpisodeState s) =>
                    s != null && EpisodeEngine.CommitmentRulesOn(s) && EpisodeEngine.StoryAt(s, StoryRules.Bonds);
    }

    internal sealed class WebJuryQuestion
    {
        // SOURCE FIELD Gamesim.Simulation.WebJuryQuestion.tone,question,optionA,optionB,correctIs,trait,opponentAnswer @ Assets/Gamesim/Simulation/WebJuryQuestioning.cs:287
        public string tone, question, optionA, optionB, correctIs, trait, opponentAnswer;

        // Explicit spelling of the source implicit parameterless constructor.
        public WebJuryQuestion() { }
    }

    internal sealed class WebJuryResponsePair
    {
        // SOURCE FIELD Gamesim.Simulation.WebJuryResponsePair.optionA,optionB,correctIs,trait @ Assets/Gamesim/Simulation/WebJuryQuestioning.cs:157
        public string optionA, optionB, correctIs, trait;

        // Explicit spelling of the source implicit parameterless constructor.
        public WebJuryResponsePair() { }
    }

    internal sealed class WebJuryQuestionOption
    {
        // SOURCE FIELD Gamesim.Simulation.WebJuryQuestionOption.tone,text @ Assets/Gamesim/Simulation/WebJuryQuestioning.cs:455
        public string tone, text;

        // Explicit spelling of the source implicit parameterless constructor.
        public WebJuryQuestionOption() { }
    }

    internal static class CompetitionRules
    {
        // SOURCE FIELD Gamesim.Simulation.CompetitionRules.Current @ Assets/Gamesim/Simulation/CompetitionRules.cs:2646
        public const int Current = Widened;

        // SOURCE FIELD Gamesim.Simulation.CompetitionRules.Widened @ Assets/Gamesim/Simulation/CompetitionRules.cs:2608
        public const int Widened = 4;
    }

    internal static class Windows
    {
        // SOURCE FIELD Gamesim.Simulation.Windows.AfterHoH,AfterNominations,AfterVeto,AfterEviction,None @ Assets/Gamesim/Simulation/EpisodeEngine.Week.cs:203
        public const int AfterHoH = 0, AfterNominations = 1, AfterVeto = 2, AfterEviction = 3, None = -1;

        // SOURCE FIELD Gamesim.Simulation.Windows.Count @ Assets/Gamesim/Simulation/EpisodeEngine.Week.cs:309
        public const int Count = 4;
    }

    internal static class WebSocialVocabulary
    {
        // SOURCE FIELD Gamesim.Simulation.WebSocialVocabulary.PurchaseCeiling @ Assets/Gamesim/Simulation/WebSocialVocabulary.cs:6036
        public const int PurchaseCeiling = 6;
    }

    internal static class WebJuryQuestioningCatalog
    {
        // SOURCE FIELD Gamesim.Simulation.WebJuryQuestioningCatalog.Questions @ Assets/Gamesim/Simulation/WebJuryQuestioningCatalog.cs:330
        internal static readonly Dictionary<string, string[]> Questions = new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    { "neutral", new[] { "What was your biggest move in the game?", "Why do you deserve to win over your opponent?", "Who was the most influential player on your game?", "What would you have done differently?", "Describe your game in three words.", "How did you balance competition wins with social game?" } },
                    { "bitter", new[] { "Why should I vote for you after you betrayed our alliance?", "Do you feel any remorse for how you played?", "How do you justify the lies you told in this game?", "Did you ever consider how your actions affected others?", "What makes you think you deserve my vote after backstabbing me?" } },
                    { "supportive", new[] { "Walk us through your strategic masterplan.", "How did you manage to stay so calm under pressure?", "What advice would you give to future players?", "When did you know you could win this game?", "Tell us about your proudest moment in the house." } }
                };

        // SOURCE FIELD Gamesim.Simulation.WebJuryQuestioningCatalog.NpcResponses @ Assets/Gamesim/Simulation/WebJuryQuestioningCatalog.cs:1413
        internal static readonly string[] NpcResponses = new[]
                {
                    "I played the best game I could, and I stand by every decision I made.",
                    "My strategy was always to stay adaptable while keeping my core alliances intact.",
                    "I think my gameplay speaks for itself — I earned my spot in this final two.",
                    "I respected everyone in this house, even when I had to make tough calls.",
                    "I came here to win, and every move brought me one step closer."
                };

        // SOURCE FIELD Gamesim.Simulation.WebJuryQuestioningCatalog.Pairs @ Assets/Gamesim/Simulation/WebJuryQuestioningCatalog.cs:1935
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

    internal static class WaveDEventKinds
    {
        // SOURCE FIELD Gamesim.Simulation.WaveDEventKinds.Sighting @ Assets/Gamesim/Simulation/WaveDEventKinds.cs:717
        public const string Sighting = "sighting";

        // SOURCE FIELD Gamesim.Simulation.WaveDEventKinds.Overheard @ Assets/Gamesim/Simulation/WaveDEventKinds.cs:832
        public const string Overheard = "overheard";

        // SOURCE FIELD Gamesim.Simulation.WaveDEventKinds.PactPlan @ Assets/Gamesim/Simulation/WaveDEventKinds.cs:949
        public const string PactPlan = "pact-plan";

        // SOURCE FIELD Gamesim.Simulation.WaveDEventKinds.DoubleDealing @ Assets/Gamesim/Simulation/WaveDEventKinds.cs:1085
        public const string DoubleDealing = "double-dealing";
    }

    internal static class Knowledge
    {
        // SOURCE FIELD Gamesim.Simulation.Knowledge.Ceiling @ Assets/Gamesim/Simulation/Story/StorySystems.cs:10563
        public const int Ceiling = 128;
    }

    internal static class StoryRules
    {
        // SOURCE FIELD Gamesim.Simulation.StoryRules.Spine @ Assets/Gamesim/Simulation/Story/StoryState.cs:10969
        public const int Spine = 1;

        // SOURCE FIELD Gamesim.Simulation.StoryRules.Bonds @ Assets/Gamesim/Simulation/Story/StoryState.cs:11397
        public const int Bonds = 5;

        // SOURCE FIELD Gamesim.Simulation.StoryRules.Production @ Assets/Gamesim/Simulation/Story/StoryState.cs:11513
        public const int Production = 6;

        // SOURCE FIELD Gamesim.Simulation.StoryRules.Current @ Assets/Gamesim/Simulation/Story/StoryState.cs:12293
        public const int Current = Threads;

        // SOURCE FIELD Gamesim.Simulation.StoryRules.Lenient,Standard @ Assets/Gamesim/Simulation/Story/StoryState.cs:12338
        public const int Lenient = 0, Standard = 1;

        // SOURCE FIELD Gamesim.Simulation.StoryRules.Threads @ Assets/Gamesim/Simulation/Story/StoryState.cs:12178
        public const int Threads = 9;
    }

    internal static class StoryReceipts
    {
        // SOURCE FIELD Gamesim.Simulation.StoryReceipts.DoubleDealt @ Assets/Gamesim/Simulation/Story/StoryOdds.cs:9556
        public const string DoubleDealt = "story:double-dealt";
    }

    internal static class NpcDeals
    {
        // SOURCE FIELD Gamesim.Simulation.NpcDeals.OfferPrefix,VetoAskPrefix @ Assets/Gamesim/Simulation/NpcDeals.cs:4351
        public const string OfferPrefix = "deal-ask-", VetoAskPrefix = "deal-veto-";

        // SOURCE FIELD Gamesim.Simulation.NpcDeals.ProposalsPerWeek @ Assets/Gamesim/Simulation/NpcDeals.cs:15788
        public const int ProposalsPerWeek = 3;
    }

    internal static class LedgerLimits
    {
        // SOURCE FIELD Gamesim.Simulation.LedgerLimits.MostRows @ Assets/Gamesim/Simulation/SeasonLedger.cs:846
        public const int MostRows = 512;
    }
}

