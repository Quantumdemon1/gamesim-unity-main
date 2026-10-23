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
    internal sealed class UnifiedCommitmentFulfillment
    {
        // SOURCE FIELD Gamesim.Simulation.UnifiedCommitmentFulfillment.SettledWeek @ Assets/Gamesim/Simulation/UnifiedCommitmentHistory.cs:317
        public readonly int SettledWeek;

        // SOURCE FIELD Gamesim.Simulation.UnifiedCommitmentFulfillment.FirstId,SecondId,EffectOwnerId @ Assets/Gamesim/Simulation/UnifiedCommitmentHistory.cs:358
        public readonly string FirstId, SecondId, EffectOwnerId;

        // SOURCE FIELD Gamesim.Simulation.UnifiedCommitmentFulfillment.EvidenceIds @ Assets/Gamesim/Simulation/UnifiedCommitmentHistory.cs:423
        public readonly IReadOnlyList<string> EvidenceIds;

        // SOURCE Gamesim.Simulation.UnifiedCommitmentFulfillment.ctor 482:809
        internal UnifiedCommitmentFulfillment(int week, string first, string second, string owner, IEnumerable<string> ids)
                {
                    SettledWeek = week; FirstId = first; SecondId = second; EffectOwnerId = owner;
                    EvidenceIds = Array.AsReadOnly(ids.OrderBy(id => id, StringComparer.Ordinal).ToArray());
                }
    }

    internal static class UnifiedCommitmentHearings
    {
        // SOURCE FIELD Gamesim.Simulation.UnifiedCommitmentHearings.ProspectiveVersion @ Assets/Gamesim/Simulation/UnifiedCommitmentHearings.cs:1307
        public const int ProspectiveVersion = 1;

        // SOURCE FIELD Gamesim.Simulation.UnifiedCommitmentHearings.EvidenceCapacity @ Assets/Gamesim/Simulation/UnifiedCommitmentHearings.cs:1555
        public const int EvidenceCapacity = UnifiedCommitments.FamilyCapacity * 2;

        // SOURCE FIELD Gamesim.Simulation.UnifiedCommitmentHearings.ReceiptCapacity @ Assets/Gamesim/Simulation/UnifiedCommitmentHearings.cs:1638
        public const int ReceiptCapacity = EvidenceCapacity * (EpisodeValidation.MaximumCast - 1);

        // SOURCE FIELD Gamesim.Simulation.UnifiedCommitmentHearings.Initial,Spread @ Assets/Gamesim/Simulation/UnifiedCommitmentHearings.cs:1737
        public const string Initial = "initial", Spread = "spread";

        // SOURCE Gamesim.Simulation.UnifiedCommitmentHearings.FactIdentity 17455:17859
        private static bool FactIdentity(EpisodeState s, string id) => id != null && id.StartsWith("fact-", StringComparison.Ordinal)
                    && int.TryParse(id.Substring(5), NumberStyles.None, CultureInfo.InvariantCulture, out int sequence)
                    && sequence >= 1 && sequence < s.nextSequence && s.nextSequence <= 1000000
                    && id == "fact-" + sequence.ToString(CultureInfo.InvariantCulture);

        // SOURCE Gamesim.Simulation.UnifiedCommitmentHearings.Matches 15570:16213
        private static bool Matches(EpisodeState s, UnifiedCommitmentIncident i, HouseFactState f) => i != null
                    && i.ActorId == s.playerId && i.ActorId == f.actorId && i.WrongedId == f.subjectId && i.EvidenceIds.Contains(f.refId)
                    // Current sources emit no PromisePolicy-ref BrokenWord. A genuine heard deal alias may
                    // differ from a stronger promise owner, but observing it never proves an initial emission.
                    && s.unifiedCommitments.Any(row => row.kind == UnifiedCommitments.Safety && row.id == f.refId && row.sourcePolicy == UnifiedCommitments.DealPolicy
                        && row.settledWeek == f.week);

        // SOURCE Gamesim.Simulation.UnifiedCommitmentHearings.Rank 17354:17446
        private static int Rank(string visibility) => Array.IndexOf(FactVisibility.All, visibility);

        // SOURCE Gamesim.Simulation.UnifiedCommitmentHearings.Refuse 18007:18102
        private static bool Refuse(out string error, string message) { error = message; return false; }

        // SOURCE Gamesim.Simulation.UnifiedCommitmentHearings.RulesOn 1806:1961
        public static bool RulesOn(EpisodeState s) => s != null
                    && s.unifiedHearingRulesVersion == ProspectiveVersion && UnifiedCommitments.RulesOn(s);

        // SOURCE Gamesim.Simulation.UnifiedCommitmentHearings.SameIdentity 17092:17345
        private static bool SameIdentity(HouseFactState a, HouseFactState b) => a != null && b != null
                    && a.id == b.id && a.refId == b.refId && a.kind == b.kind && a.actorId == b.actorId
                    && a.subjectId == b.subjectId && a.week == b.week;

        // SOURCE Gamesim.Simulation.UnifiedCommitmentHearings.Token 17868:17998
        private static bool Token(string value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 160 && !value.Any(char.IsControl);

        // SOURCE Gamesim.Simulation.UnifiedCommitmentHearings.TryValidateProspectiveVoteStorage 3013:3145
        internal static bool TryValidateProspectiveVoteStorage(EpisodeState s, out string error) => ValidateStorageCore(s, true, out error);

        // SOURCE Gamesim.Simulation.UnifiedCommitmentHearings.ValidateStorage 2873:3003
        public static bool ValidateStorage(EpisodeState s, out string error) => ValidateStorageCore(s, UnifiedVoteStore.On(s), out error);

        // SOURCE Gamesim.Simulation.UnifiedCommitmentHearings.ValidateStorageCore 3155:8523
        private static bool ValidateStorageCore(EpisodeState s, bool prospectiveVote, out string error)
                {
                    error = null;
                    if (s == null || s.unifiedHearingEvidence == null || s.unifiedHearingReceipts == null)
                        return Refuse(out error, "Missing durable hearing storage.");
                    if (prospectiveVote && s.unifiedCommitmentRulesVersion != UnifiedVoteFamilyValidation.Version)
                        return Refuse(out error, "Expected explicit unified vote hearing storage.");
                    if (s.unifiedHearingRulesVersion == 0)
                        return (s.unifiedHearingEvidence.Count == 0 && s.unifiedHearingReceipts.Count == 0)
                            || Refuse(out error, "Disabled hearing authority must remain empty.");
                    bool enabled = prospectiveVote ? s.unifiedHearingRulesVersion == ProspectiveVersion : RulesOn(s);
                    if (!enabled || !YourWord.On(s) || s.story?.facts == null || s.story.facts.Any(f => f == null)
                        || s.unifiedHearingEvidence.Count > EvidenceCapacity || s.unifiedHearingReceipts.Count > ReceiptCapacity)
                        return Refuse(out error, "Invalid prospective hearing storage.");
                    // Mode 2 checks the commitment storage only (vote family V5a), as mode 1 does: every Safety gateway
                    // calls this in the middle of its command, and the whole-episode core judges the Vote family itself.
                    if (prospectiveVote
                        ? !UnifiedVoteFamilyValidation.TryValidateStorage(s, out error)
                        : !UnifiedCommitments.ValidateRecords(s, out error)) return false;
                    IReadOnlyList<UnifiedCommitmentIncident> incidents;
                    try { incidents = UnifiedCommitmentHistory.Breaches(s); }
                    catch (ArgumentException e) { return Refuse(out error, e.Message); }
                    var facts = new Dictionary<string, UnifiedHearingEvidenceState>(StringComparer.Ordinal);
                    var refs = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var evidence in s.unifiedHearingEvidence)
                    {
                        if (evidence == null || !ValidFact(s, evidence.fact)
                            || facts.ContainsKey(evidence.fact.id) || !refs.Add(evidence.fact.refId))
                            return Refuse(out error, "Invalid or duplicate audible fact provenance.");
                        facts.Add(evidence.fact.id, evidence);
                        var incident = incidents.FirstOrDefault(i => i.EffectKey == evidence.incidentKey);
                        if (!Matches(s, incident, evidence.fact))
                            return Refuse(out error, "Audible evidence does not belong to its actual safety incident.");
                        var live = s.story.facts.Where(f => f.id == evidence.fact.id).ToArray();
                        if (live.Length > 1 || live.Length == 1 && (!SameIdentity(live[0], evidence.fact)
                            || !ValidFact(s, live[0]) || Rank(live[0].visibility) < Rank(evidence.fact.visibility)
                            || evidence.fact.knowers.Any(id => !live[0].knowers.Contains(id))))
                            return Refuse(out error, "The live fact contradicts its archived audible provenance.");
                        if (s.story.facts.Any(f => f.refId == evidence.fact.refId && f.kind == FactKinds.BrokenWord
                            && f.id != evidence.fact.id))
                            return Refuse(out error, "A canonical leaf cannot acquire another fact identity.");
                    }
                    var pairs = new HashSet<(string incident, string listener)>();
                    foreach (var receipt in s.unifiedHearingReceipts)
                    {
                        if (receipt == null || !Token(receipt.listenerId) || !Token(receipt.factId)
                            || !facts.TryGetValue(receipt.factId, out var evidence) || receipt.incidentKey != evidence.incidentKey
                            || !pairs.Add((receipt.incidentKey, receipt.listenerId)))
                            return Refuse(out error, "Invalid or duplicate incident/listener hearing receipt.");
                        var listener = s.Find(receipt.listenerId);
                        var incident = incidents.First(i => i.EffectKey == receipt.incidentKey);
                        int settled = s.unifiedCommitments.First(r => r.id == incident.EffectOwnerId).settledWeek;
                        if (listener == null || listener.isPlayer || listener.id == s.playerId
                            || !evidence.fact.knowers.Contains(listener.id) || receipt.heardWeek < settled
                            || receipt.heardWeek < evidence.fact.week || receipt.heardWeek > s.week)
                            return Refuse(out error, "A hearing needs a real observed listener and a valid actual date.");
                        if (receipt.kind == Initial)
                        {
                            var owner = s.unifiedCommitments.First(r => r.id == incident.EffectOwnerId);
                            if (listener.id != incident.WrongedId || owner.sourcePolicy != UnifiedCommitments.DealPolicy
                                || evidence.fact.refId != owner.id || evidence.fact.week != settled || receipt.heardWeek != settled
                                || !evidence.fact.knowers.Contains(s.playerId))
                                return Refuse(out error, "Initial knowledge must come from the selected deal's actual emission.");
                        }
                        else if (receipt.kind != Spread) return Refuse(out error, "Unknown hearing receipt kind.");
                    }
                    return true;
                }

        // SOURCE Gamesim.Simulation.UnifiedCommitmentHearings.ValidFact 16222:17083
        private static bool ValidFact(EpisodeState s, HouseFactState f) => f != null && FactIdentity(s, f.id) && Token(f.refId)
                    && f.kind == FactKinds.BrokenWord && f.actorId == s.playerId && s.Find(f.subjectId) != null
                    && f.actorId != f.subjectId && Rank(f.visibility) >= Rank(FactVisibility.Whispered)
                    && f.week >= 1 && f.week <= s.week && f.knowers != null && f.knowers.Count <= s.contestants.Count
                    // Both real source creation paths seed these parties, and knowledge only widens.
                    // A retained listener cannot replace that original audience after the live leaf is pruned.
                    && f.knowers.Contains(f.actorId) && f.knowers.Contains(f.subjectId)
                    && f.knowers.All(id => Token(id) && s.Find(id) != null)
                    && f.knowers.Distinct(StringComparer.Ordinal).Count() == f.knowers.Count;
    }

    internal static class UnifiedCommitmentHistory
    {
        // SOURCE Gamesim.Simulation.UnifiedCommitmentHistory.Breaches 3955:5321
        public static IReadOnlyList<UnifiedCommitmentIncident> Breaches(EpisodeState state)
                {
                    var broken = Records(state).Where(row => row.status == DealStatus.Broken).ToArray();
                    foreach (var row in broken)
                    {
                        string wronged = row.brokenById == row.makerId ? row.beneficiaryId : row.makerId;
                        if (!ValidKey(row.settlementEffectKey, row.settledWeek, row.brokenById, wronged))
                            throw CommitmentReferences.StorageRefusal("Safety breach evidence does not name its actual decision, week and parties.", nameof(state));
                    }
                    return Array.AsReadOnly(broken.GroupBy(row => row.settlementEffectKey, StringComparer.Ordinal)
                        .OrderBy(group => group.Key, StringComparer.Ordinal).Select(group =>
                        {
                            var owner = group.OrderBy(row => UnifiedCommitments.SourceConsequence(row, false))
                                .ThenBy(row => row.id, StringComparer.Ordinal).First();
                            string wronged = owner.brokenById == owner.makerId ? owner.beneficiaryId : owner.makerId;
                            return new UnifiedCommitmentIncident(group.Key, owner.brokenById, wronged, owner.id,
                                UnifiedCommitments.SourceConsequence(owner, false), group.Select(row => row.id));
                        }).ToArray());
                }

        // SOURCE Gamesim.Simulation.UnifiedCommitmentHistory.Fulfillments 1429:2556
        public static IReadOnlyList<UnifiedCommitmentFulfillment> Fulfillments(EpisodeState state)
                {
                    var rows = Records(state).Where(row => row.status == DealStatus.Fulfilled);
                    return Array.AsReadOnly(rows.GroupBy(row => {
                        string first = string.CompareOrdinal(row.makerId, row.beneficiaryId) < 0 ? row.makerId : row.beneficiaryId;
                        string second = first == row.makerId ? row.beneficiaryId : row.makerId;
                        return (week: row.settledWeek, first, second);
                    }).OrderBy(group => group.Key.week).ThenBy(group => group.Key.first, StringComparer.Ordinal)
                        .ThenBy(group => group.Key.second, StringComparer.Ordinal).Select(group => {
                            var owner = group.OrderByDescending(row => UnifiedCommitments.SourceConsequence(row, true))
                                .ThenBy(row => row.id, StringComparer.Ordinal).First();
                            return new UnifiedCommitmentFulfillment(group.Key.week, group.Key.first, group.Key.second,
                                owner.id, group.Select(row => row.id));
                        }).ToArray());
                }

        // SOURCE Gamesim.Simulation.UnifiedCommitmentHistory.Key 5535:5742
        private static string Key(int week, string decision, string actor, string wronged) =>
                    "safety:" + week.ToString(CultureInfo.InvariantCulture) + ":" + Part(decision) + Part(actor) + Part(wronged);

        // SOURCE Gamesim.Simulation.UnifiedCommitmentHistory.Part 5752:5862
        private static string Part(string value) => value.Length.ToString(CultureInfo.InvariantCulture) + ":" + value;

        // SOURCE Gamesim.Simulation.UnifiedCommitmentHistory.Records 2566:3609
        public static IReadOnlyList<UnifiedCommitmentState> Records(EpisodeState state)
                {
                    if (state?.unifiedCommitmentRulesVersion == UnifiedVoteFamilyValidation.Version)
                    {
                        // The storage only (vote family V5a): a reader may run in the middle of a command.
                        if (!UnifiedVoteFamilyValidation.TryValidateStorage(state, out string storageError))
                            throw CommitmentReferences.StorageRefusal(storageError, nameof(state));
                    }
                    else
                    {
                        if (!UnifiedCommitments.RulesOn(state)) return Array.Empty<UnifiedCommitmentState>();
                        if (!UnifiedCommitments.ValidateRecords(state, out string error))
                            throw CommitmentReferences.StorageRefusal(error, nameof(state));
                    }
                    return Array.AsReadOnly(state.unifiedCommitments.Where(row => row.kind == UnifiedCommitments.Safety).OrderBy(row => row.id, StringComparer.Ordinal)
                        .Select(row => row.Clone()).ToArray());
                }

        // SOURCE Gamesim.Simulation.UnifiedCommitmentHistory.ValidKey 5331:5525
        private static bool ValidKey(string key, int week, string actor, string wronged) =>
                    key == Key(week, "nomination", actor, wronged) || key == Key(week, "replacement", actor, wronged);
    }

    internal sealed class UnifiedCommitmentIncident
    {
        // SOURCE FIELD Gamesim.Simulation.UnifiedCommitmentIncident.EffectKey,ActorId,WrongedId,EffectOwnerId @ Assets/Gamesim/Simulation/UnifiedCommitments.cs:1924
        public readonly string EffectKey, ActorId, WrongedId, EffectOwnerId;

        // SOURCE FIELD Gamesim.Simulation.UnifiedCommitmentIncident.SourceConsequence @ Assets/Gamesim/Simulation/UnifiedCommitments.cs:2001
        public readonly double SourceConsequence;

        // SOURCE FIELD Gamesim.Simulation.UnifiedCommitmentIncident.EvidenceIds @ Assets/Gamesim/Simulation/UnifiedCommitments.cs:2051
        public readonly IReadOnlyList<string> EvidenceIds;

        // SOURCE Gamesim.Simulation.UnifiedCommitmentIncident.ctor 2110:2489
        internal UnifiedCommitmentIncident(string key, string actor, string wronged, string owner, double consequence, IEnumerable<string> ids)
                {
                    EffectKey = key; ActorId = actor; WrongedId = wronged; EffectOwnerId = owner;
                    SourceConsequence = consequence; EvidenceIds = Array.AsReadOnly(ids.OrderBy(id => id, StringComparer.Ordinal).ToArray());
                }
    }

    internal static class UnifiedSafetySaveReferences
    {
        // SOURCE Gamesim.Simulation.UnifiedSafetySaveReferences.CompletedHistoricalPower 14740:15107
        private static bool CompletedHistoricalPower(EpisodeState s, PowerRow power) => power != null
                    && power.nominees != null && power.nominees.Count == 2 && power.nominees[0] != power.nominees[1]
                    && power.nominees.All(id => id != power.hohId && s.Find(id) != null)
                    && power.evicteeId != null && power.nominees.Contains(power.evicteeId);

        // SOURCE Gamesim.Simulation.UnifiedSafetySaveReferences.CompletedVeto 13895:14451
        private static bool CompletedVeto(EpisodeState s, int week, PowerRow power) => power != null
                    && s.Find(power.hohId) != null && s.Find(power.vetoHolderId) != null
                    && (week == s.week ? s.vetoResolved && s.hohId == power.hohId && s.vetoHolderId == power.vetoHolderId
                        : CompletedHistoricalPower(s, power))
                    && (power.vetoUsed ? s.Find(power.savedId) != null && s.Find(power.replacementId) != null
                        && power.savedId != power.replacementId : power.savedId == null && power.replacementId == null);

        // SOURCE Gamesim.Simulation.UnifiedSafetySaveReferences.CounterRecord 8733:10559
        private static bool CounterRecord(EpisodeState s, DealState row, bool price)
                {
                    if (!DealKind.IsKnown(row.type) || row.trustImpact != DealKind.DefaultTrust(row.type)
                        || (row.status != DealStatus.Active && row.status != DealStatus.Broken
                            && row.status != DealStatus.Fulfilled && row.status != DealStatus.Expired)) return false;
                    if (price && row.type != DealKind.SafetyAgreement && row.type != DealKind.FinalTwo
                        && row.type != DealKind.VoteSave && row.type != DealKind.VoteTogether) return false;
                    int expiry = row.type == DealKind.FinalTwo || row.type == DealKind.Partnership
                                 || row.type == DealKind.AllianceInvite || row.type == DealKind.InformationSharing
                                 || row.type == DealKind.FinalThree ? 0 : row.week;
                    if (row.expiresWeek != expiry || (DealKind.NamesATarget(row.type)
                            ? row.targetId == null || s.Find(row.targetId) == null : row.targetId != null)
                        || (price && row.type == DealKind.VoteSave && row.targetId != row.recipientId)) return false;
                    bool settled = row.status == DealStatus.Broken || row.status == DealStatus.Fulfilled;
                    if (!settled) return row.settledWeek == 0 && row.brokenById == null;
                    if (row.settledWeek < row.week || row.settledWeek < s.commitmentRulesStartWeek || row.settledWeek > s.week)
                        return false;
                    if (row.status == DealStatus.Fulfilled) return row.brokenById == null;
                    return row.brokenById == row.proposerId || row.brokenById == row.recipientId
                           || (row.brokenById == null && (row.type == DealKind.VoteTogether
                               || row.type == DealKind.VoteSave || row.type == DealKind.VoteEvict));
                }

        // SOURCE Gamesim.Simulation.UnifiedSafetySaveReferences.EffectKey 17347:17560
        private static string EffectKey(int week, string decision, string actor, string wronged) =>
                    "safety:" + week.ToString(CultureInfo.InvariantCulture) + ":" + Part(decision) + Part(actor) + Part(wronged);

        // SOURCE Gamesim.Simulation.UnifiedSafetySaveReferences.InstalledId 16721:17337
        private static bool InstalledId(string id, string prefix, long nextSequence, out long sequence)
                {
                    sequence = 0;
                    if (prefix == null || !Token(id) || !id.StartsWith(prefix, StringComparison.Ordinal)) return false;
                    string suffix = id.Substring(prefix.Length);
                    return suffix.Length > 0 && suffix.All(ch => ch >= '0' && ch <= '9')
                        && long.TryParse(suffix, NumberStyles.None, CultureInfo.InvariantCulture, out sequence)
                        && sequence > 0 && sequence < nextSequence && suffix == sequence.ToString(CultureInfo.InvariantCulture);
                }

        // SOURCE Gamesim.Simulation.UnifiedSafetySaveReferences.Part 17569:17679
        private static string Part(string value) => value.Length.ToString(CultureInfo.InvariantCulture) + ":" + value;

        // SOURCE Gamesim.Simulation.UnifiedSafetySaveReferences.Power 15117:15741
        private static bool Power(EpisodeState s, int week, out PowerRow power)
                {
                    power = null;
                    if (s.ledger?.power == null || s.ledger.power.Any(row => row == null)) return false;
                    var matches = s.ledger.power.Where(row => row.week == week).Take(2).ToArray();
                    if (matches.Length > 1) return false;
                    power = matches.FirstOrDefault();
                    // The real writer makes at most one row a week: max100 weeks < MostRows512. Unlike
                    // events/relationship logs, referenced historical power cannot lawfully have rolled off.
                    return true;
                }

        // SOURCE Gamesim.Simulation.UnifiedSafetySaveReferences.Prefix 15976:16711
        private static string Prefix(string origin) => origin switch
                {
                    UnifiedCommitments.PlayerPromise or UnifiedCommitments.HoHPitch or UnifiedCommitments.StoryPromise => "promise-",
                    UnifiedCommitments.NpcPromise => "promise-npc-",
                    UnifiedCommitments.PlayerDeal => "deal-player-",
                    UnifiedCommitments.NpcDeal => "deal-npc-",
                    UnifiedCommitments.NpcOffer => NpcDeals.OfferPrefix,
                    UnifiedCommitments.Lobby => "deal-lobby-",
                    UnifiedCommitments.StoryDeal => "deal-story-",
                    UnifiedCommitments.CounterDeal => Negotiation.CounterDealPrefix,
                    UnifiedCommitments.CounterPrice => Negotiation.PricePrefix,
                    _ => null,
                };

        // SOURCE Gamesim.Simulation.UnifiedSafetySaveReferences.Refuse 17827:17920
        private static bool Refuse(out string error, string reason) { error = reason; return false; }

        // SOURCE Gamesim.Simulation.UnifiedSafetySaveReferences.SamePair 15751:15966
        private static bool SamePair(DealState a, DealState b) =>
                    (a.proposerId == b.proposerId && a.recipientId == b.recipientId)
                    || (a.proposerId == b.recipientId && a.recipientId == b.proposerId);

        // SOURCE Gamesim.Simulation.UnifiedSafetySaveReferences.Token 17688:17818
        private static bool Token(string value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 160 && !value.Any(char.IsControl);

        // SOURCE Gamesim.Simulation.UnifiedSafetySaveReferences.TryValidate 804:909
        public static bool TryValidate(EpisodeState s, out string error) => TryValidateCore(s, false, out error);

        // SOURCE Gamesim.Simulation.UnifiedSafetySaveReferences.TryValidateCore 1050:6266
        private static bool TryValidateCore(EpisodeState s, bool prospectiveVote, out string error)
                {
                    error = null;
                    if (s == null || (prospectiveVote ? s.unifiedCommitmentRulesVersion != UnifiedVoteFamilyValidation.Version : !UnifiedCommitments.RulesOn(s)) || s.nextSequence < 1 || s.nextSequence > 1000000
                        || s.commitmentRulesStartWeek < 0 || s.commitmentRulesStartWeek > s.week + 1)
                        return Refuse(out error, "Expected a prospective saved Safety context.");
                    if (prospectiveVote
                        ? !UnifiedVoteFamilyValidation.TryValidate(s, s.unifiedVoteReveals, out error)
                        : !UnifiedCommitments.ValidateRecords(s, out error)) return false;
                    // PowerThisWeek owns one durable row per week. Unlike capped event histories,
                    // max100 saved weeks cannot exhaust the 512-row power cap. Ambiguity in any week
                    // must not be resolved by whichever duplicate happens to be first or last.
                    if (s.ledger?.power == null || s.ledger.power.Any(row => row == null)
                        || s.ledger.power.GroupBy(row => row.week).Any(group => group.Count() > 1))
                        return Refuse(out error, "Saved power weeks must have unambiguous durable ownership.");
        
                    var ids = new HashSet<string>(StringComparer.Ordinal);
                    foreach (string id in s.promises.Select(row => row.id).Concat(s.deals.Select(row => row.id))
                                 .Concat(s.unifiedCommitments.Select(row => row.id)))
                        if (!Token(id) || !ids.Add(id))
                            return Refuse(out error, "Stored commitments need globally unambiguous bounded identities.");
        
                    foreach (var row in s.unifiedCommitments.Where(row => row.kind == UnifiedCommitments.Safety))
                    {
                        if (!InstalledId(row.id, Prefix(row.origin), s.nextSequence, out _))
                            return Refuse(out error, "Safety identity does not belong to its installed source sequence.");
                        bool promise = row.sourcePolicy == UnifiedCommitments.PromisePolicy;
                        // Ordinary source writers do not require C0 to create a Safety promise/deal.
                        // Source authority is checked where the settlement or counter actually occurs,
                        // never invented as a global creation-week restriction.
                        if (row.trustImpact != (promise ? DealTrust.Medium : DealKind.DefaultTrust(DealKind.SafetyAgreement))
                            || row.status == DealStatus.Accepted
                            || ((row.status == DealStatus.Proposed || row.status == DealStatus.Declined)
                                && row.origin != UnifiedCommitments.NpcOffer))
                            return Refuse(out error, "Safety source policy, consent state or creation boundary is invalid.");
                        bool settled = row.status == DealStatus.Broken || row.status == DealStatus.Fulfilled;
                        if (settled && (s.commitmentRulesStartWeek < 1 || row.settledWeek < s.commitmentRulesStartWeek
                            || (promise && row.settledWeek > row.expiresWeek)
                            || (row.origin == UnifiedCommitments.NpcOffer && row.expiresWeek > row.settledWeek)))
                            return Refuse(out error, "Safety settlement precedes its source authority or follows its answer term.");
                    }
        
                    // Authoring rejects the same currently binding duty and term. Different source policies,
                    // directions and real extensions remain provenance, not duplicate agreements to erase.
                    var duties = new HashSet<(string policy, string first, string second, int expiry)>();
                    foreach (var row in s.unifiedCommitments.Where(row => row.kind == UnifiedCommitments.Safety && DealStatus.Binds(row.status)))
                    {
                        string first = row.makerId, second = row.beneficiaryId;
                        if (row.reciprocal && string.CompareOrdinal(first, second) > 0)
                        { first = row.beneficiaryId; second = row.makerId; }
                        if (!duties.Add((row.sourcePolicy, first, second, row.expiresWeek)))
                            return Refuse(out error, "The same binding Safety duty and term is stored twice.");
                    }
        
                    // Aggregate mode already proved ALL true-owner links with source-specific counter,
                    // accepted-ask and own-veto terms. Re-running this Safety-only same-week counter
                    // interpretation over a Vote veto-price would conflate two different creators.
                    if (!prospectiveVote && !ValidateLinks(s, out error)) return false;
                    IReadOnlyList<UnifiedCommitmentIncident> incidents;
                    try { incidents = UnifiedCommitmentHistory.Breaches(s); }
                    catch (ArgumentException) { return Refuse(out error, "Invalid exact Safety breach identity."); }
                    foreach (var incident in incidents)
                        if (!ValidateIncidentRole(s, incident, out error)) return false;
                    foreach (var row in s.unifiedCommitments.Where(row => row.kind == UnifiedCommitments.Safety && row.status == DealStatus.Fulfilled))
                        if (!ValidateFulfillmentRole(s, row, out error)) return false;
                    return true;
                }

        // SOURCE Gamesim.Simulation.UnifiedSafetySaveReferences.TryValidateProspectiveVote 919:1040
        internal static bool TryValidateProspectiveVote(EpisodeState s, out string error) => TryValidateCore(s, true, out error);

        // SOURCE Gamesim.Simulation.UnifiedSafetySaveReferences.ValidateFulfillmentRole 12937:13885
        private static bool ValidateFulfillmentRole(EpisodeState s, UnifiedCommitmentState row, out string error)
                {
                    error = null;
                    if (!Power(s, row.settledWeek, out var power) || !CompletedVeto(s, row.settledWeek, power)
                        || (power.hohId != row.makerId && power.hohId != row.beneficiaryId)
                        || s.Find(row.makerId).nominationWeeks.Contains(row.settledWeek)
                        || s.Find(row.beneficiaryId).nominationWeeks.Contains(row.settledWeek)
                        || (row.settledWeek == s.week
                            ? s.nominees == null || s.nominees.Contains(row.makerId) || s.nominees.Contains(row.beneficiaryId)
                            : power.nominees == null || power.nominees.Contains(row.makerId) || power.nominees.Contains(row.beneficiaryId)))
                        return Refuse(out error, "A fulfilled Safety deal needs its actual final-veto spared-pair evidence.");
                    return true;
                }

        // SOURCE Gamesim.Simulation.UnifiedSafetySaveReferences.ValidateIncidentRole 10569:12927
        private static bool ValidateIncidentRole(EpisodeState s, UnifiedCommitmentIncident incident, out string error)
                {
                    error = null;
                    var owner = s.unifiedCommitments.First(row => row.id == incident.EffectOwnerId);
                    int week = owner.settledWeek;
                    var wronged = s.Find(incident.WrongedId);
                    if (wronged == null || wronged.nominationWeeks == null || !wronged.nominationWeeks.Contains(week)
                        || !Power(s, week, out var power))
                        return Refuse(out error, "A Safety breach needs unambiguous durable nomination evidence.");
                    bool replacement = incident.EffectKey == EffectKey(week, "replacement", incident.ActorId, incident.WrongedId);
                    if (week == s.week)
                    {
                        if (s.hohId != incident.ActorId || (power != null && power.hohId != incident.ActorId))
                            return Refuse(out error, "A current Safety breach belongs to the actual HoH.");
                    }
                    else if (!CompletedHistoricalPower(s, power) || power.hohId != incident.ActorId)
                        return Refuse(out error, "Historical Safety attribution must resolve the settled week's HoH.");
        
                    if (replacement)
                    {
                        if (!CompletedVeto(s, week, power) || !power.vetoUsed || power.replacementId != incident.WrongedId)
                            return Refuse(out error, "A Safety replacement breach needs the completed veto's actual replacement.");
                    }
                    else
                    {
                        // Initial nominations may be saved before the first PowerRow is written. After the
                        // veto, the saved nominee is still original evidence; the replacement is not one.
                        bool original = week == s.week
                            ? s.nominees != null && ((s.nominees.Contains(incident.WrongedId)
                                && (power == null || power.replacementId != incident.WrongedId)) || power?.savedId == incident.WrongedId)
                            : power.nominees != null && ((power.nominees.Contains(incident.WrongedId)
                                && power.replacementId != incident.WrongedId) || power.savedId == incident.WrongedId);
                        if (!original) return Refuse(out error, "A Safety nomination breach must name an original nominee.");
                    }
                    return true;
                }

        // SOURCE Gamesim.Simulation.UnifiedSafetySaveReferences.ValidateLinks 6276:8723
        private static bool ValidateLinks(EpisodeState s, out string error)
                {
                    error = null;
                    var canonical = s.unifiedCommitments.ToDictionary(row => row.id, StringComparer.Ordinal);
                    // Detached views retain true storage provenance. Nothing is appended to a writable raw list.
                    var deals = CommitmentReferences.Deals(s).ToDictionary(row => row.id, StringComparer.Ordinal);
                    foreach (var own in deals.Values)
                    {
                        bool price = Negotiation.IsPrice(own);
                        if (own.linkedDealId == null)
                        {
                            if (price) return Refuse(out error, "A stored price must resolve what it bought.");
                            continue;
                        }
                        if (!deals.TryGetValue(own.linkedDealId, out var linked) || linked.id == own.id
                            || linked.linkedDealId != own.id || linked.week != own.week
                            || price == Negotiation.IsPrice(linked)
                            || !SamePair(own, linked))
                            return Refuse(out error, "Consideration must resolve reciprocal same-week true owners.");
                        if (!canonical.ContainsKey(own.id) && !canonical.ContainsKey(linked.id)) continue;
        
                        var bought = price ? linked : own;
                        var paid = price ? own : linked;
                        if (!EpisodeEngine.CommitmentRulesOn(s) || bought.week < s.commitmentRulesStartWeek
                            || !InstalledId(bought.id, Negotiation.CounterDealPrefix, s.nextSequence, out long boughtSequence)
                            || !InstalledId(paid.id, Negotiation.PricePrefix, s.nextSequence, out long priceSequence)
                            || boughtSequence != priceSequence || bought.proposerId != s.playerId || paid.proposerId != s.playerId
                            || bought.recipientId != paid.recipientId || bought.recipientId == s.playerId
                            || bought.type == paid.type || !CounterRecord(s, bought, false) || !CounterRecord(s, paid, true)
                            || (canonical.TryGetValue(bought.id, out var boughtRow) && boughtRow.origin != UnifiedCommitments.CounterDeal)
                            || (canonical.TryGetValue(paid.id, out var priceRow) && priceRow.origin != UnifiedCommitments.CounterPrice))
                            return Refuse(out error, "A mixed Safety counter must retain its actual bought and price source policies.");
                    }
                    return true;
                }
    }
}

