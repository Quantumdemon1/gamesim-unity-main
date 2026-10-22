using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    public sealed partial class EpisodeEngine
    {
        // These gateways run only inside the command owner's detached transaction. They do not
        // activate rules, save, invent a decision, or turn read projections into writable mirrors.
        // They settle wherever canonical Safety is the authority: mode 1, and the prospective mode 2
        // (vote family V3b), where they settle the Safety rows only; the Vote rows settle and end by their own (V4).
        internal static void ResolveUnifiedSafetyNomination(EpisodeState s, string decisionId, string actorId,
            IReadOnlyList<string> actionNominees)
        {
            if (!UnifiedCommitments.SafetyAuthorityOn(s)) return;
            if (decisionId != "nomination" && decisionId != "replacement")
                throw new ArgumentException("Use the actual nomination or replacement decision class.", nameof(decisionId));
            CheckUnifiedSafetyContext(s);
            var evaluation = UnifiedCommitments.EvaluateNomination(s, decisionId, actorId, actionNominees);
            if (evaluation.Changes.Count == 0) return;
            var before = s.unifiedCommitments.ToDictionary(row => row.id, row => row.Clone(), StringComparer.Ordinal);
            var promises = SafetyPromises(s).Where(row => before.ContainsKey(row.id))
                .ToDictionary(row => row.id, row => row, StringComparer.Ordinal);
            var deals = SafetyDeals(s).Where(row => before.ContainsKey(row.id))
                .ToDictionary(row => row.id, row => row, StringComparer.Ordinal);
            var staged = StageUnifiedSafety(s, evaluation);
            // Every owner and linked consideration must resolve before a row, score or RNG changes.
            CheckUnifiedSafetyEffectPolicies(s, evaluation.Changes.Select(change => before[change.Record.id]), deals);
            foreach (var incident in evaluation.Breaches)
            {
                var owner = before[incident.EffectOwnerId];
                double impact = owner.sourcePolicy == UnifiedCommitments.PromisePolicy
                    ? WebRules.PromiseImpact(PromiseKind.Safety, PromiseStatus.Broken)
                    : DealResolution.Impact(s, deals[owner.id], DealStatus.Broken);
                if (impact != incident.SourceConsequence)
                    throw new ArgumentException("The safety origin does not match its source effect policy.");
            }
            CheckUnifiedSafetyLinks(s, evaluation.Changes.Select(change => before[change.Record.id]));
            s.unifiedCommitments = staged;
            var currentEffectKeys = evaluation.Breaches.Select(incident => incident.EffectKey).ToArray();
            foreach (var incident in evaluation.Breaches)
            {
                var owner = before[incident.EffectOwnerId];
                if (owner.sourcePolicy == UnifiedCommitments.PromisePolicy)
                    SettlePromiseBeforeSafetyEffects(s, promises[owner.id], PromiseStatus.Broken, currentEffectKeys);
                else SettleDealsBeforeSafetyEffects(s, new List<DealResolution.Verdict> {
                    new DealResolution.Verdict { deal = deals[owner.id], status = DealStatus.Broken, actorId = actorId } }, currentEffectKeys);
                // The selected source routine already voids its own price. Calling every affected
                // bought row also handles non-owner evidence; the existing true-owner void is idempotent.
                foreach (string id in incident.EvidenceIds)
                    if (deals.TryGetValue(id, out var bought)) VoidThePrice(s, bought, actorId);
            }
        }

        internal static void ResolveUnifiedSafetySpared(EpisodeState s, string hohId, IReadOnlyList<string> finalBlock)
        {
            if (!UnifiedCommitments.SafetyAuthorityOn(s)) return;
            CheckUnifiedSafetyContext(s);
            var evaluation = UnifiedCommitments.EvaluateFinalVetoSpared(s, hohId, finalBlock);
            if (evaluation.Changes.Count == 0) return;
            var deals = SafetyDeals(s).ToDictionary(row => row.id, row => row, StringComparer.Ordinal);
            var owners = evaluation.Changes.Select(change => deals[change.Record.id])
                .GroupBy(deal => DealResolution.Partner(deal, hohId), StringComparer.Ordinal)
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .Select(group => group.OrderByDescending(deal => DealResolution.Impact(s, deal, DealStatus.Fulfilled))
                    .ThenBy(deal => deal.id, StringComparer.Ordinal).First()).ToList();
            var staged = StageUnifiedSafety(s, evaluation);
            CheckUnifiedSafetyEffectPolicies(s, evaluation.Changes.Select(change => change.Record), deals);
            CheckUnifiedSafetyLinks(s, evaluation.Changes.Select(change => change.Record));
            s.unifiedCommitments = staged;
            // New-rule positive deduplication: one strongest kept-deal effect per spared partner,
            // all qualifying deal evidence fulfilled. Safety promises keep their separate lifetime.
            foreach (var owner in owners)
                SettleDeals(s, new List<DealResolution.Verdict> {
                    new DealResolution.Verdict { deal = owner, status = DealStatus.Fulfilled, actorId = hohId } });
        }

        internal static void ResolveUnifiedSafetyExpiry(EpisodeState s, UnifiedCommitmentExpiry boundary, string departedId = null)
        {
            if (!UnifiedCommitments.SafetyAuthorityOn(s)) return;
            CheckUnifiedSafetyContext(s);
            var evaluation = UnifiedCommitments.Expire(s, boundary, departedId);
            if (evaluation.Changes.Count == 0) return;
            s.unifiedCommitments = StageUnifiedSafety(s, evaluation);
            // Expiry is not a broken or kept word: no score, witnesses, history, price verdict or roll.
        }

        // The gateways' detached source-shaped views. Mode 1's are CommitmentReferences' as they always were;
        // in mode 2, once CheckUnifiedSafetyContext has checked the Safety authority and the hearing storage
        // (whose mode-2 check reads the Vote rows' storage too, vote family V5a), the unchecked projection of both families.
        private static IReadOnlyList<PromiseState> SafetyPromises(EpisodeState s) =>
            UnifiedVoteStore.On(s) ? UnifiedVoteReferences.PromisesUnchecked(s) : CommitmentReferences.Promises(s);

        private static IReadOnlyList<DealState> SafetyDeals(EpisodeState s) =>
            UnifiedVoteStore.On(s) ? UnifiedVoteReferences.DealsUnchecked(s) : CommitmentReferences.Deals(s);

        private static void CheckUnifiedSafetyContext(EpisodeState s)
        {
            if (!CommitmentRulesOn(s) || !UnifiedCommitments.ValidateSafetyAuthority(s, out _))
                throw CommitmentReferences.StorageRefusal("A valid enabled commitment context is required for safety settlement.");
            if (!UnifiedCommitmentHearings.ValidateStorage(s, out string hearingError))
                throw CommitmentReferences.StorageRefusal(hearingError, nameof(s));
            var ids = s.promises.Select(row => row.id).Concat(s.deals.Select(row => row.id))
                .Concat(s.unifiedCommitments.Select(row => row.id)).ToList();
            if (ids.Any(string.IsNullOrEmpty) || ids.Distinct(StringComparer.Ordinal).Count() != ids.Count)
                throw CommitmentReferences.StorageRefusal("Safety settlement needs unambiguous commitment identities.");
        }

        private static List<UnifiedCommitmentState> StageUnifiedSafety(EpisodeState s, UnifiedCommitmentEvaluation evaluation)
        {
            var rows = s.unifiedCommitments.Select(row => row.Clone()).ToList();
            var changed = new HashSet<string>(StringComparer.Ordinal);
            foreach (var change in evaluation.Changes)
            {
                int at = rows.FindIndex(row => row.id == change.Record.id);
                if (at < 0 || !changed.Add(change.Record.id)) throw new ArgumentException("Unresolvable safety settlement evidence.");
                rows[at] = change.Record.Clone();
            }
            var projection = new EpisodeState { week = s.week, playerId = s.playerId, contestants = s.contestants,
                promises = s.promises, deals = s.deals, unifiedCommitmentRulesVersion = s.unifiedCommitmentRulesVersion,
                unifiedCommitments = rows };
            if (!UnifiedCommitments.ValidateSafetyAuthority(projection, out string error)) throw new ArgumentException(error);
            return rows;
        }

        private static void CheckUnifiedSafetyLinks(EpisodeState s, IEnumerable<UnifiedCommitmentState> rows)
        {
            var deals = SafetyDeals(s).ToDictionary(row => row.id, row => row, StringComparer.Ordinal);
            foreach (var row in rows.Where(row => row.linkedCommitmentId != null))
            {
                if (!deals.TryGetValue(row.id, out var own) || !deals.TryGetValue(row.linkedCommitmentId, out var linked)
                    || linked.linkedDealId != row.id || linked.week != row.createdWeek
                    || Negotiation.IsPrice(own) == Negotiation.IsPrice(linked)
                    || !((own.proposerId == linked.proposerId && own.recipientId == linked.recipientId)
                        || (own.proposerId == linked.recipientId && own.recipientId == linked.proposerId)))
                    throw new ArgumentException("Resolve both true owners of a reciprocal safety consideration first.");
            }
        }

        private static void CheckUnifiedSafetyEffectPolicies(EpisodeState s, IEnumerable<UnifiedCommitmentState> rows,
            IReadOnlyDictionary<string, DealState> deals)
        {
            foreach (var row in rows.Where(row => row.sourcePolicy == UnifiedCommitments.DealPolicy))
            {
                bool accepted = row.origin == UnifiedCommitments.NpcOffer || row.origin == UnifiedCommitments.CounterDeal
                    || row.origin == UnifiedCommitments.CounterPrice;
                if (!deals.TryGetValue(row.id, out var deal) || DealResolution.AcceptedOffer(s, deal) != accepted)
                    throw new ArgumentException("Every affected safety source must match its accepted-offer policy.");
            }
        }
    }
}
