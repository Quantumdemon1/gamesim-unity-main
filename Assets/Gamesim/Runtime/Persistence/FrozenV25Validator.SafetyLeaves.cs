// UnifiedCommitments.cs SHA256 ae53fa7fb6aca525bae8ed67bdb984b43cbec3fe33143204b65674f5d4e8627c.
// UnifiedCommitmentHistory.cs SHA256 db6fc927bcf8cc1f0d000e2801dda416d37ad1ecc8bb57caa2bfb17547d1f956.
// CommitmentReferences.cs SHA256 18454b0056012cf2d3a813df8d0023e47c2638294d140d736b81ff7bdf16850b.
// UnifiedSafetySaveReferences.cs SHA256 e65e5813b83ed5cf730ea3a55b84e5c4142e2de1a139dc6cd4b60c699ecc1b22.
// UnifiedCommitmentHearings.cs SHA256 56f9696c56f416c65d0ed18db379e611d47db6ba7d9e1599247d1a2d551dc346.
// Frozen schema25 at f4566b69afc10c1008d64e112c12ed39241caa36.
// Validation-only Safety/history/reference/hearing leaves snapshot; no writer, normalization, settlement or knowledge mutation.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Gamesim.Persistence.Frozen25Data;

namespace Gamesim.Persistence
{
    internal static partial class FrozenV25Validator
    {
private static class UnifiedCommitments
        {

        public const int ProspectiveVersion = 1, FamilyCapacity = 200;
        public const string Safety = "safety", PromisePolicy = "promise", DealPolicy = "deal";
        public const string PlayerPromise = "player-promise", NpcPromise = "npc-promise", StoryPromise = "story-promise", HoHPitch = "hoh-pitch";
        public const string PlayerDeal = "player-deal", NpcDeal = "npc-deal", NpcOffer = "npc-offer", Lobby = "lobby", StoryDeal = "story-deal";
        public const string CounterDeal = "counter-deal", CounterPrice = "counter-price";

        public static bool RulesOn(EpisodeState state) => state != null && state.unifiedCommitmentRulesVersion == ProspectiveVersion;

public static bool IsPromiseOrigin(string origin) => origin == PlayerPromise || origin == NpcPromise || origin == StoryPromise || origin == HoHPitch;

public static bool IsDealOrigin(string origin) => origin == PlayerDeal || origin == NpcDeal || origin == NpcOffer
            || origin == Lobby || origin == StoryDeal || origin == CounterDeal || origin == CounterPrice;

public static bool ValidateRow(EpisodeState state, UnifiedCommitmentState row, out string error)
        {
            error = null;
            if (!RulesOn(state)) return Refuse(out error, "Unified commitments are not enabled.");
            if (!IdentityContextValid(state) || row == null || !Token(row.id) || row.kind != Safety
                || !Token(row.makerId) || !Token(row.beneficiaryId) || row.makerId == row.beneficiaryId
                || state.Find(row.makerId) == null || state.Find(row.beneficiaryId) == null)
                return Refuse(out error, "Invalid canonical safety identity.");
            bool promise = row.sourcePolicy == PromisePolicy;
            if ((!promise && row.sourcePolicy != DealPolicy) || (promise ? !IsPromiseOrigin(row.origin) : !IsDealOrigin(row.origin))
                || row.reciprocal == promise || !DealStatus.IsKnown(row.status) || !DealTrust.IsKnown(row.trustImpact))
                return Refuse(out error, "Invalid canonical safety source policy.");
            // A proposed NPC safety offer can survive the week turn until the next NPC deal
            // pass. Its answer preserves the proposal week/ID, but acceptance resets the term
            // to the answering week (EpisodeEngine.RespondToDeal). Decline never resets it.
            bool answeredNpcOffer = row.origin == NpcOffer && row.status != DealStatus.Proposed && row.status != DealStatus.Declined;
            bool validTerm = answeredNpcOffer
                ? row.expiresWeek >= row.createdWeek && row.expiresWeek <= state.week
                : row.expiresWeek == row.createdWeek + (promise || row.origin == Lobby ? 1 : 0);
            if (row.createdWeek < 1 || row.createdWeek > state.week || !validTerm)
                return Refuse(out error, "Invalid canonical safety term.");
            if (promise && (row.status == DealStatus.Proposed || row.status == DealStatus.Accepted || row.status == DealStatus.Declined || row.status == DealStatus.Fulfilled
                || row.trustImpact != DealTrust.Medium || row.linkedCommitmentId != null))
                return Refuse(out error, "A safety promise is unilateral, not a pending bargain or a spared-deal receipt.");
            bool playerMakes = row.origin == PlayerPromise || row.origin == HoHPitch || row.origin == PlayerDeal || row.origin == Lobby
                || row.origin == CounterDeal || row.origin == CounterPrice;
            if ((playerMakes && (row.makerId != state.playerId || row.beneficiaryId == state.playerId))
                || (row.origin == NpcOffer && (row.makerId == state.playerId || row.beneficiaryId != state.playerId))
                || ((row.origin == NpcPromise || row.origin == NpcDeal) && (row.makerId == state.playerId || row.beneficiaryId == state.playerId)))
                return Refuse(out error, "The safety origin does not match its parties.");
            bool counter = row.origin == CounterDeal || row.origin == CounterPrice;
            if ((row.linkedCommitmentId != null && (!Token(row.linkedCommitmentId) || row.linkedCommitmentId == row.id))
                || counter != (row.linkedCommitmentId != null))
                return Refuse(out error, "Invalid safety consideration reference.");
            bool settled = row.status == DealStatus.Broken || row.status == DealStatus.Fulfilled;
            if (settled ? row.settledWeek < row.createdWeek || row.settledWeek > state.week : row.settledWeek != 0)
                return Refuse(out error, "Invalid canonical safety settlement week.");
            if (row.status == DealStatus.Broken)
            {
                if ((promise ? row.brokenById != row.makerId : row.brokenById != row.makerId && row.brokenById != row.beneficiaryId)
                    || !EffectToken(row.settlementEffectKey)) return Refuse(out error, "Invalid safety breach attribution.");
            }
            else if (row.brokenById != null || row.settlementEffectKey != null)
                return Refuse(out error, "Only a breach may carry a betrayal effect identity.");
            return true;
        }

public static bool ValidateRecords(EpisodeState state, out string error)
        {
            error = null;
            if (state == null || state.unifiedCommitments == null) return Refuse(out error, "Missing canonical commitment list.");
            if (state.unifiedCommitmentRulesVersion == 0)
                return state.unifiedCommitments.Count == 0 || Refuse(out error, "Disabled commitment authority must remain empty.");
            if (!RulesOn(state) || !IdentityContextValid(state) || state.promises == null || state.deals == null
                || state.promises.Any(row => row == null) || state.deals.Any(row => row == null)
                || state.unifiedCommitments.Count > FamilyCapacity * 2) return Refuse(out error, "Invalid prospective commitment storage.");
            // There may be only one mutable safety authority. A future explicit converter must move
            // safety history out of these stores before opt-in; other families remain legacy-owned.
            if (state.promises.Any(row => row.kind == PromiseKind.Safety) || state.deals.Any(row => row.type == DealKind.SafetyAgreement))
                return Refuse(out error, "Legacy safety rows must not mirror the prospective canonical authority.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var promise in state.promises) if (promise?.id != null) ids.Add(promise.id);
            foreach (var deal in state.deals) if (deal?.id != null) ids.Add(deal.id);
            int promises = state.promises.Count, deals = state.deals.Count;
            foreach (var row in state.unifiedCommitments)
            {
                if (!ValidateRow(state, row, out error)) return false;
                if (!ids.Add(row.id)) return Refuse(out error, "Canonical identity collides with another commitment.");
                if (row.sourcePolicy == PromisePolicy) promises++; else deals++;
            }
            return (promises <= FamilyCapacity && deals <= FamilyCapacity) || Refuse(out error, "Commitment family capacity exceeded.");
        }

internal static double SourceConsequence(UnifiedCommitmentState row, bool fulfilled)
        {
            if (row.sourcePolicy == PromisePolicy) return WebRules.PromiseImpact(Safety, fulfilled ? DealStatus.Fulfilled : DealStatus.Broken);
            bool accepted = row.origin == NpcOffer || row.origin == CounterDeal || row.origin == CounterPrice;
            string trust = !fulfilled && accepted ? DealTrust.Heavier(row.trustImpact) : row.trustImpact;
            return (fulfilled ? DealResolution.FulfilledBase : DealResolution.BrokenBase) * DealTrust.Weight(trust);
        }

private static bool IdentityContextValid(EpisodeState state) => state != null && state.week >= 1 && state.week <= 100
            && state.contestants != null && state.contestants.Count >= FrozenV25Validator.MinimumCast && state.contestants.Count <= FrozenV25Validator.MaximumCast
            && state.contestants.All(person => person != null && Token(person.id) && person.nominationWeeks != null)
            && state.contestants.Select(person => person.id).Distinct(StringComparer.Ordinal).Count() == state.contestants.Count
            && Token(state.playerId) && state.contestants.Any(person => person.id == state.playerId);

private static bool Token(string value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 160 && !value.Any(char.IsControl);

private static bool EffectToken(string value) => value != null && value.StartsWith("safety:", StringComparison.Ordinal) && value.Length <= 640 && !value.Any(char.IsControl);

private static bool Refuse(out string error, string reason) { error = reason; return false; }
        }
private static class UnifiedCommitmentHearings
        {

        public const int ProspectiveVersion = 1;
        // Both permanent family inventories count their historical and legacy rows. One leaf/ref,
        // one incident/listener pair, and the stored cast maximum prove these non-evicting bounds.
        public const int EvidenceCapacity = UnifiedCommitments.FamilyCapacity * 2;
        public const int ReceiptCapacity = EvidenceCapacity * (FrozenV25Validator.MaximumCast - 1);
        public const string Initial = "initial", Spread = "spread";

        public static bool RulesOn(EpisodeState s) => s != null
            && s.unifiedHearingRulesVersion == ProspectiveVersion && UnifiedCommitments.RulesOn(s);

public static bool ValidateStorage(EpisodeState s, out string error)
        {
            error = null;
            if (s == null || s.unifiedHearingEvidence == null || s.unifiedHearingReceipts == null)
                return Refuse(out error, "Missing durable hearing storage.");
            if (s.unifiedHearingRulesVersion == 0)
                return (s.unifiedHearingEvidence.Count == 0 && s.unifiedHearingReceipts.Count == 0)
                    || Refuse(out error, "Disabled hearing authority must remain empty.");
            if (!RulesOn(s) || !YourWord.On(s) || s.story?.facts == null || s.story.facts.Any(f => f == null)
                || s.unifiedHearingEvidence.Count > EvidenceCapacity || s.unifiedHearingReceipts.Count > ReceiptCapacity)
                return Refuse(out error, "Invalid prospective hearing storage.");
            if (!UnifiedCommitments.ValidateRecords(s, out error)) return false;
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

private static bool Matches(EpisodeState s, UnifiedCommitmentIncident i, HouseFactState f) => i != null
            && i.ActorId == s.playerId && i.ActorId == f.actorId && i.WrongedId == f.subjectId && i.EvidenceIds.Contains(f.refId)
            // Current sources emit no PromisePolicy-ref BrokenWord. A genuine heard deal alias may
            // differ from a stronger promise owner, but observing it never proves an initial emission.
            && s.unifiedCommitments.Any(row => row.id == f.refId && row.sourcePolicy == UnifiedCommitments.DealPolicy
                && row.settledWeek == f.week);

private static bool ValidFact(EpisodeState s, HouseFactState f) => f != null && FactIdentity(s, f.id) && Token(f.refId)
            && f.kind == FactKinds.BrokenWord && f.actorId == s.playerId && s.Find(f.subjectId) != null
            && f.actorId != f.subjectId && Rank(f.visibility) >= Rank(FactVisibility.Whispered)
            && f.week >= 1 && f.week <= s.week && f.knowers != null && f.knowers.Count <= s.contestants.Count
            // Both real source creation paths seed these parties, and knowledge only widens.
            // A retained listener cannot replace that original audience after the live leaf is pruned.
            && f.knowers.Contains(f.actorId) && f.knowers.Contains(f.subjectId)
            && f.knowers.All(id => Token(id) && s.Find(id) != null)
            && f.knowers.Distinct(StringComparer.Ordinal).Count() == f.knowers.Count;

private static bool SameIdentity(HouseFactState a, HouseFactState b) => a != null && b != null
            && a.id == b.id && a.refId == b.refId && a.kind == b.kind && a.actorId == b.actorId
            && a.subjectId == b.subjectId && a.week == b.week;

private static int Rank(string visibility) => Array.IndexOf(FactVisibility.All, visibility);

private static bool FactIdentity(EpisodeState s, string id) => id != null && id.StartsWith("fact-", StringComparison.Ordinal)
            && int.TryParse(id.Substring(5), NumberStyles.None, CultureInfo.InvariantCulture, out int sequence)
            && sequence >= 1 && sequence < s.nextSequence && s.nextSequence <= 1000000
            && id == "fact-" + sequence.ToString(CultureInfo.InvariantCulture);

private static bool Token(string value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 160 && !value.Any(char.IsControl);

private static bool Refuse(out string error, string message) { error = message; return false; }
        }
private static class UnifiedCommitmentHistory
        {

        /// <summary>
        /// The reciprocal pair's final-veto receipt in its actual settlement week. This has the
        /// gateway's strongest-source-reward/stable-ID owner, never a made-up fulfillment actor.
        /// No promise can fulfill there. All views and evidence are detached.
        /// </summary>
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

        public static IReadOnlyList<UnifiedCommitmentState> Records(EpisodeState state)
        {
            if (!UnifiedCommitments.RulesOn(state)) return Array.Empty<UnifiedCommitmentState>();
            if (!UnifiedCommitments.ValidateRecords(state, out string error))
                throw new ArgumentException(error, nameof(state));
            return Array.AsReadOnly(state.unifiedCommitments.OrderBy(row => row.id, StringComparer.Ordinal)
                .Select(row => row.Clone()).ToArray());
        }

        /// <summary>
        /// One immutable incident per actual nomination decision/actor/wronged party. Reject
        /// malformed or conflated keys instead of silently treating corrupt evidence as fewer acts.
        /// This validates historical groups, not the complete enabled-save/reference contract.
        /// </summary>
        public static IReadOnlyList<UnifiedCommitmentIncident> Breaches(EpisodeState state)
        {
            var broken = Records(state).Where(row => row.status == DealStatus.Broken).ToArray();
            foreach (var row in broken)
            {
                string wronged = row.brokenById == row.makerId ? row.beneficiaryId : row.makerId;
                if (!ValidKey(row.settlementEffectKey, row.settledWeek, row.brokenById, wronged))
                    throw new ArgumentException("Safety breach evidence does not name its actual decision, week and parties.", nameof(state));
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

        private static bool ValidKey(string key, int week, string actor, string wronged) =>
            key == Key(week, "nomination", actor, wronged) || key == Key(week, "replacement", actor, wronged);

        private static string Key(int week, string decision, string actor, string wronged) =>
            "safety:" + week.ToString(CultureInfo.InvariantCulture) + ":" + Part(decision) + Part(actor) + Part(wronged);

        private static string Part(string value) => value.Length.ToString(CultureInfo.InvariantCulture) + ":" + value;

        }
private static class CommitmentReferences
        {

        /// <summary>
        /// Preserves legacy list order and every legacy scalar. Canonical rows follow in their own
        /// stored order. This does not establish chronological interleaving across authoring stores;
        /// callers selecting a latest outcome must use an explicit event/settlement ordering contract.
        /// Every returned record is detached, even in a legacy game.
        /// </summary>
        public static IReadOnlyList<PromiseState> Promises(EpisodeState state)
        {
            CheckRules(state);
            var rows = state.promises.Select(row => row?.Clone()).ToList();
            if (UnifiedCommitments.RulesOn(state))
                rows.AddRange(state.unifiedCommitments.Where(row => row.sourcePolicy == UnifiedCommitments.PromisePolicy)
                    .Select(ProjectPromise));
            return rows.AsReadOnly();
        }

        /// <summary>Full provenance view, not a strongest-protection or once-per-incident count.</summary>
        public static IReadOnlyList<DealState> Deals(EpisodeState state)
        {
            CheckRules(state);
            var rows = state.deals.Select(row => row?.Clone()).ToList();
            if (UnifiedCommitments.RulesOn(state))
                rows.AddRange(state.unifiedCommitments.Where(row => row.sourcePolicy == UnifiedCommitments.DealPolicy)
                    .Select(ProjectDeal));
            return rows.AsReadOnly();
        }

        /// <summary>Historical rows count too; moving safety authority cannot free an authoring slot.</summary>
        public static int PromiseCount(EpisodeState state)
        {
            CheckRules(state);
            return state.promises.Count + (UnifiedCommitments.RulesOn(state)
                ? state.unifiedCommitments.Count(row => row.sourcePolicy == UnifiedCommitments.PromisePolicy) : 0);
        }

        /// <summary>Includes every legacy non-safety family, NPC row and settled canonical deal.</summary>
        public static int DealCount(EpisodeState state)
        {
            CheckRules(state);
            return state.deals.Count + (UnifiedCommitments.RulesOn(state)
                ? state.unifiedCommitments.Count(row => row.sourcePolicy == UnifiedCommitments.DealPolicy) : 0);
        }

        public static PromiseState FindPromise(EpisodeState state, string id) =>
            id == null ? null : Promises(state).FirstOrDefault(row => row?.id == id);

        public static DealState FindDeal(EpisodeState state, string id) =>
            id == null ? null : Deals(state).FirstOrDefault(row => row?.id == id);

        /// <summary>Canonical outcomes name their actual settlement week; legacy references retain their original meaning.</summary>
        public static int ReceiptWeek(EpisodeState state, string id, int legacyWeek)
        {
            var row = FindCanonical(state, id);
            return row != null && row.settledWeek > 0 ? row.settledWeek : legacyWeek;
        }

        /// <summary>Includes the canonical effect identity, which a legacy-shaped DTO cannot carry.</summary>
        public static UnifiedCommitmentState FindCanonical(EpisodeState state, string id)
        {
            CheckRules(state);
            return id == null || !UnifiedCommitments.RulesOn(state) ? null
                : state.unifiedCommitments.FirstOrDefault(row => row.id == id)?.Clone();
        }

        private static PromiseState ProjectPromise(UnifiedCommitmentState row) => new PromiseState
        {
            id = row.id, fromId = row.makerId, toId = row.beneficiaryId, targetId = null,
            kind = PromiseKind.Safety, status = PromiseStatusFor(row.status),
            week = row.createdWeek, expiresWeek = row.expiresWeek, impact = row.trustImpact,
            brokenById = row.brokenById, settledWeek = row.settledWeek,
        };

        private static DealState ProjectDeal(UnifiedCommitmentState row) => new DealState
        {
            id = row.id, proposerId = row.makerId, recipientId = row.beneficiaryId, targetId = null,
            type = DealKind.SafetyAgreement, status = row.status,
            week = row.createdWeek, expiresWeek = row.expiresWeek, trustImpact = row.trustImpact,
            brokenById = row.brokenById, settledWeek = row.settledWeek, linkedDealId = row.linkedCommitmentId,
        };

        private static PromiseStatus PromiseStatusFor(string status)
        {
            switch (status)
            {
                case DealStatus.Active: return PromiseStatus.Active;
                case DealStatus.Broken: return PromiseStatus.Broken;
                case DealStatus.Expired: return PromiseStatus.Expired;
                default: throw new ArgumentException("Unsupported canonical safety-promise status.");
            }
        }

        private static void CheckRules(EpisodeState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            // Do not add new validity checks to legacy reader paths or rewrite their records.
            if (state.unifiedCommitmentRulesVersion == 0) return;
            if (!UnifiedCommitments.ValidateRecords(state, out string error))
                throw new ArgumentException(error, nameof(state));
        }

        }
private static class UnifiedSafetySaveReferences
        {

        public static bool TryValidate(EpisodeState s, out string error)
        {
            error = null;
            if (!UnifiedCommitments.RulesOn(s) || s.nextSequence < 1 || s.nextSequence > 1000000
                || s.commitmentRulesStartWeek < 0 || s.commitmentRulesStartWeek > s.week + 1)
                return Refuse(out error, "Expected a prospective saved Safety context.");
            if (!UnifiedCommitments.ValidateRecords(s, out error)) return false;
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

            foreach (var row in s.unifiedCommitments)
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
            foreach (var row in s.unifiedCommitments.Where(row => DealStatus.Binds(row.status)))
            {
                string first = row.makerId, second = row.beneficiaryId;
                if (row.reciprocal && string.CompareOrdinal(first, second) > 0)
                { first = row.beneficiaryId; second = row.makerId; }
                if (!duties.Add((row.sourcePolicy, first, second, row.expiresWeek)))
                    return Refuse(out error, "The same binding Safety duty and term is stored twice.");
            }

            if (!ValidateLinks(s, out error)) return false;
            IReadOnlyList<UnifiedCommitmentIncident> incidents;
            try { incidents = UnifiedCommitmentHistory.Breaches(s); }
            catch (ArgumentException) { return Refuse(out error, "Invalid exact Safety breach identity."); }
            foreach (var incident in incidents)
                if (!ValidateIncidentRole(s, incident, out error)) return false;
            foreach (var row in s.unifiedCommitments.Where(row => row.status == DealStatus.Fulfilled))
                if (!ValidateFulfillmentRole(s, row, out error)) return false;
            return true;
        }

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

        private static bool CompletedVeto(EpisodeState s, int week, PowerRow power) => power != null
            && s.Find(power.hohId) != null && s.Find(power.vetoHolderId) != null
            && (week == s.week ? s.vetoResolved && s.hohId == power.hohId && s.vetoHolderId == power.vetoHolderId
                : CompletedHistoricalPower(s, power))
            && (power.vetoUsed ? s.Find(power.savedId) != null && s.Find(power.replacementId) != null
                && power.savedId != power.replacementId : power.savedId == null && power.replacementId == null);

        // A later week cannot retain only the earlier veto draft as its final-block proof.
        // RecordReveal stores this block and evictee before the real week turn. Tally and all
        // other ledger scalars remain the complete prospective validator's responsibility.
        private static bool CompletedHistoricalPower(EpisodeState s, PowerRow power) => power != null
            && power.nominees != null && power.nominees.Count == 2 && power.nominees[0] != power.nominees[1]
            && power.nominees.All(id => id != power.hohId && s.Find(id) != null)
            && power.evicteeId != null && power.nominees.Contains(power.evicteeId);

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

        private static bool SamePair(DealState a, DealState b) =>
            (a.proposerId == b.proposerId && a.recipientId == b.recipientId)
            || (a.proposerId == b.recipientId && a.recipientId == b.proposerId);

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

        private static bool InstalledId(string id, string prefix, long nextSequence, out long sequence)
        {
            sequence = 0;
            if (prefix == null || !Token(id) || !id.StartsWith(prefix, StringComparison.Ordinal)) return false;
            string suffix = id.Substring(prefix.Length);
            return suffix.Length > 0 && suffix.All(ch => ch >= '0' && ch <= '9')
                && long.TryParse(suffix, NumberStyles.None, CultureInfo.InvariantCulture, out sequence)
                && sequence > 0 && sequence < nextSequence && suffix == sequence.ToString(CultureInfo.InvariantCulture);
        }

        private static string EffectKey(int week, string decision, string actor, string wronged) =>
            "safety:" + week.ToString(CultureInfo.InvariantCulture) + ":" + Part(decision) + Part(actor) + Part(wronged);
        private static string Part(string value) => value.Length.ToString(CultureInfo.InvariantCulture) + ":" + value;
        private static bool Token(string value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 160 && !value.Any(char.IsControl);
        private static bool Refuse(out string error, string reason) { error = reason; return false; }

        }
private static class YourWord
        {
public static bool On(EpisodeState s) =>
            s != null && EpisodeEngine.CommitmentRulesOn(s) && EpisodeEngine.StoryAt(s, StoryRules.Bonds);
        }
private static class NpcDeals { public const string OfferPrefix = "deal-ask-"; }
private static class DealResolution { public const double FulfilledBase = 8, BrokenBase = -15; }

    }
}
