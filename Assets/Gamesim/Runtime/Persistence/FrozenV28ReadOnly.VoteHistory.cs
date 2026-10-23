// Fixed28 selected Vote read-only domain. Source-authoring only; all17 HOLD.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Gamesim.Persistence.Frozen28Data;

namespace Gamesim.Persistence.Frozen28ReadOnly
{
    internal sealed class UnifiedVoteDecision
    {
        public readonly UnifiedCommitmentState Record;
        public readonly UnifiedVoteRevealState Reveal;
        public readonly int SettledWeek;
        public readonly string Status, ActorId;
        internal UnifiedVoteDecision(UnifiedCommitmentState row, UnifiedVoteRevealState frame, string status, string actor)
        { Record = row.Clone(); Reveal = frame.Clone(); SettledWeek = frame.week; Status = status; ActorId = actor; }
    }

    internal sealed class UnifiedVoteIncident
    {
        public readonly int Week;
        public readonly string ActorId, WrongedId, OwnerId, DealId;
        public readonly bool OwnerIsPromise;
        /// <summary>Whether this is a fulfillment's group (<see cref="UnifiedVoteHistory.Fulfillments"/>) rather than a breach's.</summary>
        public readonly bool Kept;
        /// <summary>The owner's native source consequence: a vote promise's, or the deal's weight times the kept or broken base.</summary>
        public readonly double SourceConsequence;
        public readonly IReadOnlyList<string> EvidenceIds;
        internal UnifiedVoteIncident(int week, string actor, string wronged, string owner, string deal, bool ownerIsPromise, double consequence,
            IEnumerable<string> ids, bool kept = false)
        {
            Week = week; ActorId = actor; WrongedId = wronged; OwnerId = owner; DealId = deal; OwnerIsPromise = ownerIsPromise; Kept = kept;
            SourceConsequence = consequence; EvidenceIds = Array.AsReadOnly(ids.OrderBy(id => id, StringComparer.Ordinal).ToArray());
        }
    }

    internal sealed class UnifiedVoteObligationVerdict
    {
        public readonly string Status, ActorId;
        internal UnifiedVoteObligationVerdict(string status, string actorId)
        { Status = status; ActorId = actorId; }
    }

    internal enum UnifiedVoteAtomRole
    {
        /// <summary>The write that carries one of the row's own directed consequences.</summary>
        Primary,
        /// <summary>A kept named deal's second ledger direction: the actor's record of the partner, at the same weight.</summary>
        Mirror,
        /// <summary>A broken named deal's breaker-side record at zero: what they did, weighing nothing. Never an incident.</summary>
        Context,
    }

    internal sealed class UnifiedVoteAtom
    {
        internal readonly string HolderId, AboutId;
        internal readonly double Delta;
        internal readonly UnifiedVoteAtomRole Role;
        internal bool Selected;

        internal UnifiedVoteAtom(string holderId, string aboutId, double delta, UnifiedVoteAtomRole role)
        { HolderId = holderId; AboutId = aboutId; Delta = delta; Role = role; }
    }

    internal sealed class UnifiedVoteVerdict
    {
        internal readonly UnifiedCommitmentState Row;
        internal readonly string Status, ActorId;
        internal readonly double Nominal;
        internal readonly IReadOnlyList<UnifiedVoteAtom> Scores, Ledger;

        internal UnifiedVoteVerdict(UnifiedCommitmentState row, string status, string actorId, double nominal,
            IReadOnlyList<UnifiedVoteAtom> scores, IReadOnlyList<UnifiedVoteAtom> ledger)
        { Row = row.Clone(); Status = status; ActorId = actorId; Nominal = nominal; Scores = scores; Ledger = ledger; }

        internal bool Promise => Row.sourcePolicy == UnifiedCommitments.PromisePolicy;
        internal bool Kept => Status == DealStatus.Fulfilled;
        /// <summary>A deal both parties decided at once: a voting bloc, or a targeted deal both of them broke.</summary>
        internal bool Collective => !Promise && ActorId == null;
        /// <summary>Whether any of this row's directed consequences won its group: only then do its memory, line, Story and witness lanes run.</summary>
        internal bool Owns => Scores.Any(atom => atom.Selected);

        /// <summary>The row's source-shaped deal, its status the verdict's, as the source settles it.</summary>
        internal DealState Deal()
        {
            var deal = UnifiedVoteReferences.ProjectDeal(Row);
            deal.status = Status;
            return deal;
        }
    }

    internal static class UnifiedVoteTogether
    {
        public const string Vote = "vote";
        public const int MaximumIdentityLength = 160;
        public const int MaximumBallots = EpisodeValidation.MaximumCast;

        /// <summary>
        /// Returns false for malformed local inputs. A valid inactive row or a missing actual
        /// party ballot succeeds with a null verdict. Otherwise equal targets fulfill the bloc
        /// and different targets break it, without choosing a sole breaker or changing the row.
        /// Repeated voter inputs must be normalized to their latest actual ballot by the caller;
        /// duplicates are refused here, never silently resolved by list order.
        /// </summary>
        public static bool TryVerdict(UnifiedCommitmentState row,
            IReadOnlyList<UnifiedVoteBallotState> ballots, out string verdict, out string error)
        {
            verdict = null;
            error = null;
            if (row == null) return Fail(out error, "A VoteTogether row is required.");
            if (row.kind != Vote || row.sourcePolicy != UnifiedCommitments.DealPolicy
                || row.subtype != DealKind.VoteTogether || row.targetId != null)
                return Fail(out error, "A reciprocal vote/deal/vote_together row has no named target.");
            if (!Token(row.id) || !Token(row.makerId) || !Token(row.beneficiaryId)
                || string.Equals(row.makerId, row.beneficiaryId, StringComparison.Ordinal))
                return Fail(out error, "A VoteTogether row requires valid distinct party identities and an id.");
            if (!row.reciprocal)
                return Fail(out error, "VoteTogether is a reciprocal agreement.");
            if (!DealStatus.IsKnown(row.status))
                return Fail(out error, "A VoteTogether row requires a known status.");
            if (ballots == null || ballots.Count > MaximumBallots)
                return Fail(out error, "An actual ballot snapshot must be present and within the house bound.");

            var voters = new HashSet<string>(StringComparer.Ordinal);
            string makerTarget = null, beneficiaryTarget = null;
            for (var index = 0; index < ballots.Count; index++)
            {
                var ballot = ballots[index];
                if (ballot == null || !Token(ballot.voterId) || !Token(ballot.targetId))
                    return Fail(out error, "Every actual ballot requires valid voter and target identities.");
                if (!voters.Add(ballot.voterId))
                    return Fail(out error, "Actual ballots must have unique voter identities.");
                if (string.Equals(ballot.voterId, row.makerId, StringComparison.Ordinal))
                    makerTarget = ballot.targetId;
                else if (string.Equals(ballot.voterId, row.beneficiaryId, StringComparison.Ordinal))
                    beneficiaryTarget = ballot.targetId;
            }

            // Validate the complete snapshot even when the caller supplied an inactive row.
            // Source lifecycle and full completed-reveal eligibility remain separate owners.
            if (row.status != DealStatus.Active || makerTarget == null || beneficiaryTarget == null)
                return true;
            verdict = string.Equals(makerTarget, beneficiaryTarget, StringComparison.Ordinal)
                ? DealStatus.Fulfilled : DealStatus.Broken;
            return true;
        }

        private static bool Token(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > MaximumIdentityLength) return false;
            for (var index = 0; index < value.Length; index++)
                if (char.IsControl(value[index])) return false;
            return true;
        }

        private static bool Fail(out string error, string message)
        {
            error = message;
            return false;
        }
    }

    internal static class UnifiedVoteHistory
    {
        public static IReadOnlyList<UnifiedVoteIncident> Incidents(EpisodeState s) => Rebuilt(s, false);

        public static IReadOnlyList<UnifiedVoteIncident> Fulfillments(EpisodeState s) => Rebuilt(s, true);

        private static IReadOnlyList<UnifiedVoteIncident> Rebuilt(EpisodeState s, bool kept)
        {
            if (!UnifiedVoteStore.On(s)) return Array.Empty<UnifiedVoteIncident>();
            return Array.AsReadOnly(Decisions(s).GroupBy(decision => decision.SettledWeek).OrderBy(week => week.Key)
                .SelectMany(week => UnifiedVoteSettlement.Groups(week.Key, UnifiedVoteSettlement.Rebuild(s, week)))
                .Where(group => group.Kept == kept).ToArray());
        }

        public static IReadOnlyList<UnifiedVoteDecision> Decisions(EpisodeState s)
        {
            if (!UnifiedVoteFamilyValidation.TryValidateStorage(s, out string error))
                throw CommitmentReferences.StorageRefusal(error, nameof(s));
            var result = new List<UnifiedVoteDecision>();
            foreach (var row in s.unifiedCommitments.Where(item => item.kind == UnifiedVoteTogether.Vote
                && (item.status == DealStatus.Fulfilled || item.status == DealStatus.Broken)))
            {
                if (!UnifiedVoteFamilyValidation.FirstDecision(s, s.unifiedVoteReveals, row,
                    out var frame, out string status, out string actor, out error) || frame == null)
                    throw CommitmentReferences.StorageRefusal(error ?? "Missing actual Vote decision.", nameof(s));
                result.Add(new UnifiedVoteDecision(row, frame, status, actor));
            }
            return Array.AsReadOnly(result.ToArray());
        }

        public static UnifiedVoteDecision FindDecision(EpisodeState s, string id) =>
            Decisions(s).FirstOrDefault(decision => decision.Record.id == id);

        public static string Key(UnifiedCommitmentState row, int week) =>
            "vote:" + week.ToString(CultureInfo.InvariantCulture) + ":" + Part(row.sourcePolicy)
                + Part(row.sourcePolicy == UnifiedCommitments.PromisePolicy ? "promise" : row.subtype)
                + Part(row.targetId ?? string.Empty) + Part(row.makerId) + Part(row.beneficiaryId);

        internal static bool ExactKey(UnifiedCommitmentState row) =>
            row.settlementEffectKey != null && row.settlementEffectKey.Length <= 1024
                && !row.settlementEffectKey.Any(char.IsControl)
                && string.Equals(row.settlementEffectKey, Key(row, row.settledWeek), StringComparison.Ordinal);

        private static string Part(string value) => value.Length.ToString(CultureInfo.InvariantCulture) + ":" + value;
    }

    internal static class UnifiedVoteStore
    {
        internal static bool On(EpisodeState s) => s != null && s.unifiedCommitmentRulesVersion == UnifiedVoteFamilyValidation.Version;

        internal static IReadOnlyList<DealState> Deals(EpisodeState s) =>
            On(s) ? UnifiedVoteReferences.DealsUnchecked(s) : UnifiedCommitments.RulesOn(s) ? CommitmentReferences.Deals(s) : s.deals;
    }

    internal static class UnifiedVoteSettlement
    {
        internal static IReadOnlyList<UnifiedVoteIncident> Groups(int week, IReadOnlyList<UnifiedVoteVerdict> verdicts) =>
            Array.AsReadOnly(verdicts.SelectMany(verdict => verdict.Scores.Select(atom => (verdict, atom)))
                .GroupBy(item => (item.verdict.Kept, item.atom.AboutId, item.atom.HolderId))
                .OrderBy(group => group.Key.Kept).ThenBy(group => group.Key.AboutId, StringComparer.Ordinal)
                .ThenBy(group => group.Key.HolderId, StringComparer.Ordinal)
                .Select(group =>
                {
                    var owner = group.Single(item => item.atom.Selected).verdict;
                    var ranked = (group.Key.Kept ? group.OrderByDescending(item => item.verdict.Nominal) : group.OrderBy(item => item.verdict.Nominal))
                        .ThenBy(item => item.verdict.Row.id, StringComparer.Ordinal).ToList();
                    var deal = ranked.FirstOrDefault(item => !item.verdict.Promise).verdict;
                    return new UnifiedVoteIncident(week, group.Key.AboutId, group.Key.HolderId, owner.Row.id, deal?.Row.id, owner.Promise,
                        owner.Nominal, group.Select(item => item.verdict.Row.id).Distinct(), group.Key.Kept);
                }).ToArray());

        internal static IReadOnlyList<UnifiedVoteVerdict> Rebuild(EpisodeState s, IEnumerable<UnifiedVoteDecision> decided)
        {
            var verdicts = decided.Select(decision =>
            {
                var row = decision.Record.Clone();
                row.status = DealStatus.Active; row.settledWeek = 0; row.brokenById = null; row.settlementEffectKey = null;
                return Verdict(s, row, decision.Status, decision.ActorId);
            }).ToList();
            Select(verdicts);
            return verdicts.AsReadOnly();
        }

        internal static (long sequence, int price, int canonical, int index) Occurrence(string id, bool canonical, int index)
        {
            long sequence = long.MaxValue;
            int dash = id?.LastIndexOf('-') ?? -1;
            if (dash >= 0 && long.TryParse(id.Substring(dash + 1), NumberStyles.None, CultureInfo.InvariantCulture, out long parsed))
                sequence = parsed;
            bool price = id != null && id.StartsWith(Negotiation.PricePrefix, StringComparison.Ordinal);
            return (sequence, price ? 1 : 0, canonical ? 1 : 0, index);
        }

        private static UnifiedVoteVerdict Verdict(EpisodeState s, UnifiedCommitmentState row, string status, string actor)
        {
            bool kept = status == DealStatus.Fulfilled;
            if (row.sourcePolicy == UnifiedCommitments.PromisePolicy)
            {
                // The beneficiary's view of the maker, and the beneficiary's permanent one-way record of it.
                double impact = WebRules.PromiseImpact("vote", kept ? "fulfilled" : "broken");
                return new UnifiedVoteVerdict(row, status, actor, impact,
                    new[] { new UnifiedVoteAtom(row.beneficiaryId, row.makerId, impact, UnifiedVoteAtomRole.Primary) },
                    new[] { new UnifiedVoteAtom(row.beneficiaryId, row.makerId, impact, UnifiedVoteAtomRole.Primary) });
            }
            var deal = UnifiedVoteReferences.ProjectDeal(row);
            deal.status = status;
            double delta = DealResolution.Impact(s, deal, status);
            if (actor == null)
                // Both views, and both directions of the record, in the source's own order.
                return new UnifiedVoteVerdict(row, status, null, delta,
                    new[] { new UnifiedVoteAtom(deal.proposerId, deal.recipientId, delta, UnifiedVoteAtomRole.Primary),
                        new UnifiedVoteAtom(deal.recipientId, deal.proposerId, delta, UnifiedVoteAtomRole.Primary) },
                    new[] { new UnifiedVoteAtom(deal.proposerId, deal.recipientId, delta, UnifiedVoteAtomRole.Primary),
                        new UnifiedVoteAtom(deal.recipientId, deal.proposerId, delta, UnifiedVoteAtomRole.Primary) });
            string partner = DealResolution.Partner(deal, actor);
            return new UnifiedVoteVerdict(row, status, actor, delta,
                new[] { new UnifiedVoteAtom(partner, actor, delta, UnifiedVoteAtomRole.Primary) },
                new[] { new UnifiedVoteAtom(partner, actor, delta, UnifiedVoteAtomRole.Primary),
                    kept ? new UnifiedVoteAtom(actor, partner, delta, UnifiedVoteAtomRole.Mirror)
                         : new UnifiedVoteAtom(actor, partner, 0, UnifiedVoteAtomRole.Context) });
        }

        internal static void Select(IReadOnlyList<UnifiedVoteVerdict> verdicts)
        {
            // Directed consequences: one winner per polarity, actor (the one the view is of) and partner (whose view).
            foreach (var group in verdicts.SelectMany(verdict => verdict.Scores.Select(atom => (verdict, atom)))
                         .GroupBy(item => (item.verdict.Kept, item.atom.AboutId, item.atom.HolderId)))
            {
                var ranked = group.Key.Kept
                    ? group.OrderByDescending(item => item.verdict.Nominal)
                    : group.OrderBy(item => item.verdict.Nominal);
                ranked.ThenBy(item => item.verdict.Row.id, StringComparer.Ordinal).First().atom.Selected = true;
            }
            // A breach's record follows its own consequence; its zero context follows its row.
            foreach (var verdict in verdicts.Where(verdict => !verdict.Kept && verdict.Owns))
                foreach (var atom in verdict.Ledger)
                    atom.Selected = atom.Role == UnifiedVoteAtomRole.Context
                        || verdict.Scores.Any(score => score.Selected && score.HolderId == atom.HolderId && score.AboutId == atom.AboutId);
            // A kept edge of the record: the owners' entries compete, primary and mirror alike, by source delta then id.
            foreach (var edge in verdicts.Where(verdict => verdict.Kept && verdict.Owns)
                         .SelectMany(verdict => verdict.Ledger.Select(atom => (verdict, atom)))
                         .GroupBy(item => (item.atom.HolderId, item.atom.AboutId)))
                edge.OrderByDescending(item => item.atom.Delta).ThenBy(item => item.verdict.Row.id, StringComparer.Ordinal)
                    .First().atom.Selected = true;
        }
    }

    internal static class UnifiedVoteObligations
    {
        public const int MaximumIdentityLength = UnifiedVoteTogether.MaximumIdentityLength;
        public const int MaximumBallots = UnifiedVoteTogether.MaximumBallots;

        public static bool TryVerdict(UnifiedCommitmentState row,
            IReadOnlyList<UnifiedVoteBallotState> ballots, IReadOnlyList<string> nominees,
            out UnifiedVoteObligationVerdict verdict, out string error)
        {
            verdict = null; error = null;
            if (row == null || row.kind != UnifiedVoteTogether.Vote || !Token(row.id)
                || !Token(row.makerId) || !Token(row.beneficiaryId)
                || string.Equals(row.makerId, row.beneficiaryId, StringComparison.Ordinal))
                return Fail(out error, "A Vote obligation requires valid distinct parties and a row identity.");
            bool promise = row.sourcePolicy == UnifiedCommitments.PromisePolicy;
            if (promise)
            {
                if (row.reciprocal || row.subtype != null
                    || (row.targetId == null ? row.origin != UnifiedCommitments.StoryPromise : !Token(row.targetId))
                    || (row.status != DealStatus.Active && row.status != DealStatus.Fulfilled
                        && row.status != DealStatus.Broken && row.status != DealStatus.Expired))
                    return Fail(out error, "A unilateral Vote promise requires its native target, Story-null distinction and promise status.");
            }
            else if (row.sourcePolicy != UnifiedCommitments.DealPolicy || !row.reciprocal
                || (row.subtype != DealKind.VoteSave && row.subtype != DealKind.VoteEvict)
                || !Token(row.targetId) || !DealStatus.IsKnown(row.status))
                return Fail(out error, "A targeted Vote deal requires a reciprocal Save or Evict duty, named target and known status.");
            if (nominees == null || nominees.Count != 2 || !Token(nominees[0]) || !Token(nominees[1])
                || string.Equals(nominees[0], nominees[1], StringComparison.Ordinal))
                return Fail(out error, "A Vote obligation requires the two distinct final nominees.");
            if (ballots == null || ballots.Count < 0 || ballots.Count > MaximumBallots)
                return Fail(out error, "An actual Vote snapshot must be nonnull and within the house bound.");

            var voters = new HashSet<string>(StringComparer.Ordinal);
            string makerTarget = null, beneficiaryTarget = null;
            for (int index = 0; index < ballots.Count; index++)
            {
                var ballot = ballots[index];
                if (ballot == null || !Token(ballot.voterId) || !Token(ballot.targetId)
                    || IsNominee(nominees, ballot.voterId) || !IsNominee(nominees, ballot.targetId)
                    || !voters.Add(ballot.voterId))
                    return Fail(out error, "Actual Vote ballots must be unique eligible local identities targeting the final block.");
                if (string.Equals(ballot.voterId, row.makerId, StringComparison.Ordinal)) makerTarget = ballot.targetId;
                else if (string.Equals(ballot.voterId, row.beneficiaryId, StringComparison.Ordinal)) beneficiaryTarget = ballot.targetId;
            }
            // Even inactive rows and off-block targets must not bypass malformed evidence.
            if (row.status != DealStatus.Active) return true;
            if (promise)
            {
                if (makerTarget != null)
                    verdict = new UnifiedVoteObligationVerdict(
                        string.Equals(row.targetId, makerTarget, StringComparison.Ordinal) ? DealStatus.Fulfilled : DealStatus.Broken,
                        row.makerId);
                return true;
            }
            if (!IsNominee(nominees, row.targetId)) return true;
            string kept = null, broke = null;
            // Source proposer/recipient order, NOT private-box insertion order.
            foreach (var party in new[] { row.makerId, row.beneficiaryId })
            {
                string target = party == row.makerId ? makerTarget : beneficiaryTarget;
                if (target == null) continue;
                bool evictedTarget = string.Equals(target, row.targetId, StringComparison.Ordinal);
                bool breaks = row.subtype == DealKind.VoteEvict ? !evictedTarget : evictedTarget;
                if (breaks)
                {
                    if (broke != null)
                    { verdict = new UnifiedVoteObligationVerdict(DealStatus.Broken, null); return true; }
                    broke = party;
                }
                else if (kept == null) kept = party;
            }
            if (broke != null) verdict = new UnifiedVoteObligationVerdict(DealStatus.Broken, broke);
            else if (kept != null) verdict = new UnifiedVoteObligationVerdict(DealStatus.Fulfilled, kept);
            return true;
        }

        private static bool IsNominee(IReadOnlyList<string> nominees, string id) =>
            string.Equals(nominees[0], id, StringComparison.Ordinal) || string.Equals(nominees[1], id, StringComparison.Ordinal);

        private static bool Token(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > MaximumIdentityLength) return false;
            for (int index = 0; index < value.Length; index++) if (char.IsControl(value[index])) return false;
            return true;
        }

        private static bool Fail(out string error, string reason) { error = reason; return false; }
    }
}
