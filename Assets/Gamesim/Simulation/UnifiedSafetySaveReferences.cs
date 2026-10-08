using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// Read-only saved-Safety identity, source-policy, chronology and consideration checks.
    /// This is one leaf of the prospective whole-state validator, not engine or save acceptance.
    /// It never clears fields, converts legacy rows, installs projections or activates rules.
    /// Callers must also validate the complete cast, legacy scalars and typed reference leaves.
    /// Unlike writer preflight, a saved record has consumed its authoring sequence and a saved
    /// settlement has completed the real nomination/veto command and its durable role record.
    /// </summary>
    public static class UnifiedSafetySaveReferences
    {
        public static bool TryValidate(EpisodeState s, out string error) => TryValidateCore(s, false, out error);

        internal static bool TryValidateProspectiveVote(EpisodeState s, out string error) => TryValidateCore(s, true, out error);

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
}
