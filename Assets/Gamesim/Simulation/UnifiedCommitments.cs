using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// Prospective canonical record for every commitment family. Only safety has a policy here so
    /// far. Schema 24 keeps this list empty and its version disabled: this is NOT engine authority
    /// until every safety writer, reader, save transaction and effect owner has been integrated.
    /// </summary>
    [Serializable]
    public sealed class UnifiedCommitmentState
    {
        public string id, kind, sourcePolicy, origin, makerId, beneficiaryId;
        public bool reciprocal;
        public int createdWeek, expiresWeek;
        public string status;
        public int settledWeek;
        public string brokenById, trustImpact, linkedCommitmentId, settlementEffectKey;

        // All members are scalars or immutable strings. Deep-copy any collections added in a later family.
        public UnifiedCommitmentState Clone() => (UnifiedCommitmentState)MemberwiseClone();
    }

    /// <summary>A detached proposed change, never installed in an EpisodeState by these helpers.</summary>
    public sealed class UnifiedCommitmentChange
    {
        public readonly UnifiedCommitmentState Record;
        internal UnifiedCommitmentChange(UnifiedCommitmentState record) { Record = record.Clone(); }
    }

    /// <summary>One prospective breach incident. Evidence retains every affected record, without stacking effects.</summary>
    public sealed class UnifiedCommitmentIncident
    {
        public readonly string EffectKey, ActorId, WrongedId, EffectOwnerId;
        public readonly double SourceConsequence;
        public readonly IReadOnlyList<string> EvidenceIds;
        internal UnifiedCommitmentIncident(string key, string actor, string wronged, string owner, double consequence, IEnumerable<string> ids)
        {
            EffectKey = key; ActorId = actor; WrongedId = wronged; EffectOwnerId = owner;
            SourceConsequence = consequence; EvidenceIds = Array.AsReadOnly(ids.OrderBy(id => id, StringComparer.Ordinal).ToArray());
        }
    }

    public sealed class UnifiedCommitmentEvaluation
    {
        public readonly IReadOnlyList<UnifiedCommitmentChange> Changes;
        public readonly IReadOnlyList<UnifiedCommitmentIncident> Breaches;
        internal UnifiedCommitmentEvaluation(IEnumerable<UnifiedCommitmentChange> changes, IEnumerable<UnifiedCommitmentIncident> breaches)
        { Changes = Array.AsReadOnly(changes.ToArray()); Breaches = Array.AsReadOnly(breaches.ToArray()); }
    }

    public sealed class UnifiedCommitmentProtection
    {
        public readonly double Strength;
        public readonly string StrongestId;
        public readonly IReadOnlyList<string> EvidenceIds;
        internal UnifiedCommitmentProtection(double strength, string strongest, IEnumerable<string> ids)
        { Strength = strength; StrongestId = strongest; EvidenceIds = Array.AsReadOnly(ids.OrderBy(id => id, StringComparer.Ordinal).ToArray()); }
    }

    public enum UnifiedCommitmentExpiry { PromiseWeekTurn, DealPass, Departure }

    /// <summary>
    /// Pure, prospective safety policy, NOT an activated production writer/settler. Every returned
    /// row is detached. No helper changes state, rolls, relationships, memories, facts or commands.
    /// The ordinary engine and validator do not accept version 1 yet. Other families are refused.
    /// </summary>
    public static class UnifiedCommitments
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

        /// <summary>
        /// Row shape/source policy only, not current-save acceptance. Cross-family links, exact
        /// effect-key parsing and group consistency belong to the later enabled storage validator.
        /// </summary>
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
            if (row.createdWeek < 1 || row.createdWeek > state.week || row.expiresWeek != row.createdWeek + (promise || row.origin == Lobby ? 1 : 0))
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

        /// <summary>One-draft capacity/admissibility only; a linked counter bundle still requires atomic two-row reservation.</summary>
        public static bool CanOffer(EpisodeState state, UnifiedCommitmentState offer, bool playerProposal, out string error)
        {
            error = null;
            if (!RulesOn(state)) return Refuse(out error, "Unified commitments are not enabled.");
            if (!ValidateRecords(state, out error) || !ValidateRow(state, offer, out error)) return false;
            if (offer.createdWeek != state.week || (offer.status != DealStatus.Active && offer.status != DealStatus.Proposed)
                || !Active(state, offer.makerId) || !Active(state, offer.beneficiaryId)) return Refuse(out error, "Offer current safety between active houseguests.");
            if (playerProposal != (offer.origin == PlayerDeal))
                return Refuse(out error, "Only a player deal uses the player-proposal capacity.");
            if (state.unifiedCommitments.Any(row => row.id == offer.id) || state.promises.Any(row => row?.id == offer.id) || state.deals.Any(row => row?.id == offer.id))
                return Refuse(out error, "This commitment identity already exists.");
            int used = offer.sourcePolicy == PromisePolicy
                ? state.promises.Count + state.unifiedCommitments.Count(row => row.sourcePolicy == PromisePolicy)
                : state.deals.Count + state.unifiedCommitments.Count(row => row.sourcePolicy == DealPolicy);
            int capacity = playerProposal || offer.origin == CounterDeal || offer.origin == CounterPrice ? PlayerDeals.PlayerDealCeiling : FamilyCapacity;
            if (used >= capacity) return Refuse(out error, "The source commitment record is full.");
            if (state.unifiedCommitments.Any(row => DealStatus.Binds(row.status) && SameDutyAndTerm(row, offer)))
                return Refuse(out error, "The same safety duty and term already stand.");
            return true;
        }

        /// <summary>Returns a detached admissible draft; does not append it, spend an action or settle consent.</summary>
        public static UnifiedCommitmentState Offer(EpisodeState state, UnifiedCommitmentState offer, bool playerProposal, out string error)
            => CanOffer(state, offer, playerProposal, out error) ? offer.Clone() : null;

        public static UnifiedCommitmentState Find(EpisodeState state, string id)
            => !RulesOn(state) ? null : CheckedRows(state).FirstOrDefault(row => row.id == id)?.Clone();

        /// <summary>
        /// Like the source readers, reads stored Active status; it does not opportunistically lapse a
        /// deal before its NPC pass. The future engine must call the distinct explicit expiry boundaries.
        /// </summary>
        public static IReadOnlyList<UnifiedCommitmentState> Binding(EpisodeState state, string makerId, string beneficiaryId)
        {
            if (!RulesOn(state)) return Array.Empty<UnifiedCommitmentState>();
            var rows = CheckedRows(state);
            if (!Active(state, makerId) || !Active(state, beneficiaryId) || makerId == beneficiaryId) return Array.Empty<UnifiedCommitmentState>();
            return Array.AsReadOnly(rows.Where(row => row.status == DealStatus.Active && Protects(row, makerId, beneficiaryId))
                .OrderBy(row => row.id, StringComparer.Ordinal).Select(row => row.Clone()).ToArray());
        }

        /// <summary>
        /// Nomination protection, not relationship impact. The source deal weight is 35; promises
        /// contribute only the existing call-in hold (0/1/1.5/2). Overlap takes the maximum, not a sum.
        /// </summary>
        public static UnifiedCommitmentProtection StrongestProtection(EpisodeState state, string makerId, string beneficiaryId)
        {
            if (!RulesOn(state) || !StrategyRules.Apply(state)) return new UnifiedCommitmentProtection(0, null, Array.Empty<string>());
            var rows = Binding(state, makerId, beneficiaryId);
            double strongest = 0; string owner = null;
            foreach (var row in rows)
            {
                double hold = row.sourcePolicy == DealPolicy ? 1 : Negotiation.HeldTo(state, new PromiseState
                { id = row.id, fromId = row.makerId, toId = row.beneficiaryId, kind = PromiseKind.Safety,
                    status = PromiseStatus.Active, week = row.createdWeek, expiresWeek = row.expiresWeek });
                double strength = StrategyRules.DealWeight(DealKind.SafetyAgreement) * hold;
                if (strength > strongest) { strongest = strength; owner = row.id; }
            }
            return new UnifiedCommitmentProtection(strongest, owner, rows.Select(row => row.id));
        }

        public static UnifiedCommitmentEvaluation EvaluateNomination(EpisodeState state, string decisionId, string actorId, IReadOnlyList<string> actionNominees)
        {
            if (!RulesOn(state)) return Empty();
            var rows = CheckedRows(state);
            CheckDecision(state, decisionId, actorId, actionNominees);
            var changes = new List<UnifiedCommitmentChange>(); var incidents = new List<UnifiedCommitmentIncident>();
            foreach (string wronged in actionNominees.OrderBy(id => id, StringComparer.Ordinal))
            {
                var affected = rows.Where(row => row.status == DealStatus.Active && Protects(row, actorId, wronged)).OrderBy(row => row.id, StringComparer.Ordinal).ToList();
                if (affected.Count == 0) continue;
                string key = BreachKey(state.week, decisionId, actorId, wronged);
                var owner = affected.OrderBy(row => SourceConsequence(row, false)).ThenBy(row => row.id, StringComparer.Ordinal).First();
                foreach (var row in affected)
                {
                    var changed = row.Clone(); changed.status = DealStatus.Broken; changed.brokenById = actorId;
                    changed.settledWeek = state.week; changed.settlementEffectKey = key;
                    changes.Add(new UnifiedCommitmentChange(changed));
                }
                incidents.Add(new UnifiedCommitmentIncident(key, actorId, wronged, owner.id, SourceConsequence(owner, false), affected.Select(row => row.id)));
            }
            return new UnifiedCommitmentEvaluation(changes.OrderBy(change => change.Record.id, StringComparer.Ordinal), incidents);
        }

        /// <summary>Caller supplies the final post-veto block, not an intermediate save/replacement frame.</summary>
        public static UnifiedCommitmentEvaluation EvaluateFinalVetoSpared(EpisodeState state, string hohId, IReadOnlyList<string> finalBlock)
        {
            if (!RulesOn(state)) return Empty();
            var rows = CheckedRows(state);
            CheckDecision(state, "final-veto", hohId, finalBlock);
            var changes = new List<UnifiedCommitmentChange>();
            foreach (var row in rows.Where(row => row.status == DealStatus.Active && row.sourcePolicy == DealPolicy).OrderBy(row => row.id, StringComparer.Ordinal))
            {
                string partner = row.makerId == hohId ? row.beneficiaryId : row.beneficiaryId == hohId ? row.makerId : null;
                if (partner == null || !Active(state, partner) || finalBlock.Contains(partner) || state.Find(partner).nominationWeeks.Contains(state.week)) continue;
                var changed = row.Clone(); changed.status = DealStatus.Fulfilled; changed.settledWeek = state.week;
                changes.Add(new UnifiedCommitmentChange(changed));
            }
            return new UnifiedCommitmentEvaluation(changes, Array.Empty<UnifiedCommitmentIncident>());
        }

        public static UnifiedCommitmentEvaluation Expire(EpisodeState state, UnifiedCommitmentExpiry boundary, string departedId = null)
        {
            if (!RulesOn(state)) return Empty();
            var rows = CheckedRows(state);
            if (!Enum.IsDefined(typeof(UnifiedCommitmentExpiry), boundary)) throw new ArgumentOutOfRangeException(nameof(boundary));
            if (boundary == UnifiedCommitmentExpiry.Departure && (state.Find(departedId) == null || Active(state, departedId)))
                throw new ArgumentException("Name the contestant who actually left.", nameof(departedId));
            var changes = new List<UnifiedCommitmentChange>();
            foreach (var row in rows.OrderBy(row => row.id, StringComparer.Ordinal))
            {
                bool expires = boundary == UnifiedCommitmentExpiry.PromiseWeekTurn
                    ? row.sourcePolicy == PromisePolicy && row.status == DealStatus.Active && row.expiresWeek < state.week
                    : row.sourcePolicy == DealPolicy && DealStatus.Binds(row.status) && (boundary == UnifiedCommitmentExpiry.DealPass
                        ? row.expiresWeek < state.week : row.makerId == departedId || row.beneficiaryId == departedId);
                if (!expires) continue;
                var changed = row.Clone(); changed.status = DealStatus.Expired;
                changes.Add(new UnifiedCommitmentChange(changed));
            }
            return new UnifiedCommitmentEvaluation(changes, Array.Empty<UnifiedCommitmentIncident>());
        }

        // Source-only scalar calculation: no relationship changes, gossip rolls or facts are emitted.
        private static double SourceConsequence(UnifiedCommitmentState row, bool fulfilled)
        {
            if (row.sourcePolicy == PromisePolicy) return WebRules.PromiseImpact(Safety, fulfilled ? DealStatus.Fulfilled : DealStatus.Broken);
            bool accepted = row.origin == NpcOffer || row.origin == CounterDeal || row.origin == CounterPrice;
            string trust = !fulfilled && accepted ? DealTrust.Heavier(row.trustImpact) : row.trustImpact;
            return (fulfilled ? DealResolution.FulfilledBase : DealResolution.BrokenBase) * DealTrust.Weight(trust);
        }

        // Nominal expiry alone is not the duration: a deal can finish at this week's final veto,
        // whereas a promise still protects next week. Distinct source settlement boundaries are
        // a genuine new duration, not a duplicate. Reverse consent is never manufactured.
        private static bool SameDutyAndTerm(UnifiedCommitmentState left, UnifiedCommitmentState right) =>
            left.sourcePolicy == right.sourcePolicy && left.expiresWeek == right.expiresWeek && Protects(left, right.makerId, right.beneficiaryId)
            && (!right.reciprocal || left.reciprocal && Protects(left, right.beneficiaryId, right.makerId));
        private static bool Protects(UnifiedCommitmentState row, string actor, string wronged) =>
            row.makerId == actor && row.beneficiaryId == wronged || row.reciprocal && row.beneficiaryId == actor && row.makerId == wronged;
        private static List<UnifiedCommitmentState> CheckedRows(EpisodeState state)
        {
            if (!ValidateRecords(state, out string error)) throw new ArgumentException(error, nameof(state));
            return state.unifiedCommitments;
        }
        private static void CheckDecision(EpisodeState state, string decisionId, string actor, IReadOnlyList<string> nominees)
        {
            if (!Token(decisionId) || !Active(state, actor) || actor != state.hohId || nominees == null || nominees.Count < 1 || nominees.Count > 2
                || nominees.Any(id => !Active(state, id) || id == actor) || nominees.Distinct(StringComparer.Ordinal).Count() != nominees.Count)
                throw new ArgumentException("Supply the acting HoH and only this actual nomination action's contestants.");
        }
        private static string BreachKey(int week, string decision, string actor, string wronged) =>
            "safety:" + week.ToString(CultureInfo.InvariantCulture) + ":" + Part(decision) + Part(actor) + Part(wronged);
        private static string Part(string value) => value.Length.ToString(CultureInfo.InvariantCulture) + ":" + value;
        private static bool IdentityContextValid(EpisodeState state) => state != null && state.week >= 1 && state.week <= 100
            && state.contestants != null && state.contestants.Count >= EpisodeValidation.MinimumCast && state.contestants.Count <= EpisodeValidation.MaximumCast
            && state.contestants.All(person => person != null && Token(person.id) && person.nominationWeeks != null)
            && state.contestants.Select(person => person.id).Distinct(StringComparer.Ordinal).Count() == state.contestants.Count
            && Token(state.playerId) && state.contestants.Any(person => person.id == state.playerId);
        private static bool Token(string value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 160 && !value.Any(char.IsControl);
        private static bool EffectToken(string value) => value != null && value.StartsWith("safety:", StringComparison.Ordinal) && value.Length <= 640 && !value.Any(char.IsControl);
        private static bool Active(EpisodeState state, string id) => id != null && state.Find(id)?.status == ContestantStatus.Active;
        private static bool Refuse(out string error, string reason) { error = reason; return false; }
        private static UnifiedCommitmentEvaluation Empty() => new UnifiedCommitmentEvaluation(Array.Empty<UnifiedCommitmentChange>(), Array.Empty<UnifiedCommitmentIncident>());
    }
}
