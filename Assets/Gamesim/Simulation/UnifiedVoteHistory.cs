using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>One agreement-local actual decision. Neither a grouped reward nor a listener's knowledge.</summary>
    public sealed class UnifiedVoteDecision
    {
        public readonly UnifiedCommitmentState Record;
        public readonly UnifiedVoteRevealState Reveal;
        public readonly int SettledWeek;
        public readonly string Status, ActorId;
        internal UnifiedVoteDecision(UnifiedCommitmentState row, UnifiedVoteRevealState frame, string status, string actor)
        { Record = row.Clone(); Reveal = frame.Clone(); SettledWeek = frame.week; Status = status; ActorId = actor; }
    }

    /// <summary>
    /// One Vote consequence group as the Rule2 plan grouped it (vote family V5b, the lead's decision D1; V5e for the kept): a
    /// reveal's week, the one whose word it was (<see cref="ActorId"/>) and the one it was given to (<see cref="WrongedId"/> -
    /// wronged by a breach, kept faith with by a fulfillment), every row in the group, and the row whose consequence won - for
    /// a breach the most severe, for a fulfillment the largest, then the ordinal id - with the first deal row by the same
    /// ranking. Detached; no knowledge grant.
    /// </summary>
    public sealed class UnifiedVoteIncident
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

    /// <summary>
    /// Prospective Vote evidence, separate from Safety nomination/spared history. Rows remain
    /// agreement provenance; <see cref="Breaches"/> groups the broken ones as the reveal's Rule2 plan did, from the rows alone, and
    /// <see cref="Incidents"/> / <see cref="Fulfillments"/> rebuild every group with its owner from the archive.
    /// </summary>
    public static class UnifiedVoteHistory
    {
        /// <summary>
        /// The Vote breaches, one per Rule2 incident (vote family V5b, the lead's decision D1): every Broken Vote row's
        /// directed consequences - a promise's on its beneficiary, a deal's on the partner of the one who broke it, a deal
        /// both broke on each - grouped by the reveal's week, the breaker and the one wronged, as the reveal grouped them.
        /// From the rows alone, so it holds in the middle of a reveal; <paramref name="excludedEffects"/> leaves out the rows
        /// whose betrayal identities are named (a reveal's own, vote family V5c). Empty outside mode 2.
        /// </summary>
        public static IReadOnlyList<UnifiedVoteIncident> Breaches(EpisodeState s, IReadOnlyCollection<string> excludedEffects = null)
        {
            if (!UnifiedVoteStore.On(s)) return Array.Empty<UnifiedVoteIncident>();
            if (!UnifiedVoteFamilyValidation.TryValidateStorage(s, out string error)) throw new ArgumentException(error, nameof(s));
            var atoms = new List<(int week, string actor, string wronged, UnifiedCommitmentState row, double nominal)>();
            foreach (var row in s.unifiedCommitments)
            {
                if (row.kind != UnifiedVoteTogether.Vote || row.status != DealStatus.Broken) continue;
                if (excludedEffects != null && row.settlementEffectKey != null && excludedEffects.Contains(row.settlementEffectKey)) continue;
                if (row.sourcePolicy == UnifiedCommitments.PromisePolicy)
                {
                    atoms.Add((row.settledWeek, row.makerId, row.beneficiaryId, row, WebRules.PromiseImpact("vote", "broken")));
                    continue;
                }
                var deal = UnifiedVoteReferences.ProjectDeal(row);
                double nominal = DealResolution.Impact(s, deal, DealStatus.Broken);
                if (row.brokenById != null) atoms.Add((row.settledWeek, row.brokenById, DealResolution.Partner(deal, row.brokenById), row, nominal));
                else
                {
                    atoms.Add((row.settledWeek, row.makerId, row.beneficiaryId, row, nominal));
                    atoms.Add((row.settledWeek, row.beneficiaryId, row.makerId, row, nominal));
                }
            }
            return Array.AsReadOnly(atoms.GroupBy(atom => (atom.week, atom.actor, atom.wronged))
                .OrderBy(group => group.Key.week).ThenBy(group => group.Key.actor, StringComparer.Ordinal)
                .ThenBy(group => group.Key.wronged, StringComparer.Ordinal)
                .Select(group =>
                {
                    var ranked = group.OrderBy(atom => atom.nominal).ThenBy(atom => atom.row.id, StringComparer.Ordinal).ToList();
                    var owner = ranked[0];
                    var deal = ranked.FirstOrDefault(atom => atom.row.sourcePolicy == UnifiedCommitments.DealPolicy);
                    return new UnifiedVoteIncident(group.Key.week, group.Key.actor, group.Key.wronged, owner.row.id, deal.row?.id,
                        owner.row.sourcePolicy == UnifiedCommitments.PromisePolicy, owner.nominal, group.Select(atom => atom.row.id).Distinct());
                }).ToArray());
        }

        /// <summary>
        /// The Vote breaches, one per Rule2 incident, rebuilt from the archive (vote family V5e): for each archived reveal, the
        /// rows it first decided (<see cref="Decisions"/>), their verdicts and native consequences as its plan met them, and the
        /// plan's own selection run on them - so each owner is the one the reveal's effects ran for. Needs every decided row's
        /// frame, which a reveal publishes only as it ends: read it between commands or outside a reveal, and inside one the
        /// row-level <see cref="Breaches"/>. Ordered by week, actor and the one wronged. Empty outside mode 2.
        /// </summary>
        public static IReadOnlyList<UnifiedVoteIncident> Incidents(EpisodeState s) => Rebuilt(s, false);

        /// <summary>
        /// The Vote fulfillments, one per Rule2 group, rebuilt from the archive as <see cref="Incidents"/> rebuilds the breaches:
        /// a kept promise's group is its beneficiary's view of its maker, a kept named deal's its partner's view of the keeper,
        /// a bloc's both views - so a bloc kept by two is two groups. Read outside a reveal. Empty outside mode 2.
        /// </summary>
        public static IReadOnlyList<UnifiedVoteIncident> Fulfillments(EpisodeState s) => Rebuilt(s, true);

        private static IReadOnlyList<UnifiedVoteIncident> Rebuilt(EpisodeState s, bool kept)
        {
            if (!UnifiedVoteStore.On(s)) return Array.Empty<UnifiedVoteIncident>();
            return Array.AsReadOnly(Decisions(s).GroupBy(decision => decision.SettledWeek).OrderBy(week => week.Key)
                .SelectMany(week => UnifiedVoteSettlement.Groups(week.Key, UnifiedVoteSettlement.Rebuild(s, week)))
                .Where(group => group.Kept == kept).ToArray());
        }

        /// <summary>
        /// Whether a mode-1-shaped row's breach is counted by its Rule2 incident rather than by itself (D1): in mode 2, a
        /// Vote promise or deal - every one of them canonical there. A reader that counted broken rows counts such a row
        /// only where it stands for its incident (<see cref="UnifiedVoteIncident.OwnerId"/> or <see cref="UnifiedVoteIncident.DealId"/>).
        /// </summary>
        public static bool ByIncident(EpisodeState s, DealState deal) =>
            deal != null && UnifiedVoteStore.On(s) && UnifiedVoteFamilyValidation.IsVoteType(deal.type);

        /// <inheritdoc cref="ByIncident(EpisodeState, DealState)"/>
        public static bool ByIncident(EpisodeState s, PromiseState promise) =>
            promise != null && UnifiedVoteStore.On(s) && promise.kind == PromiseKind.Vote;

        /// <summary>The Vote rows, detached. Checks the storage only (vote family V5a): a reader may run in the middle of a command.</summary>
        public static IReadOnlyList<UnifiedCommitmentState> Records(EpisodeState s)
        {
            if (!UnifiedVoteFamilyValidation.TryValidateStorage(s, out string error))
                throw new ArgumentException(error, nameof(s));
            return Array.AsReadOnly(s.unifiedCommitments.Where(row => row.kind == UnifiedVoteTogether.Vote)
                .Select(row => row.Clone()).ToArray());
        }

        /// <summary>
        /// Each terminal Vote row with the archived frame that first decided it. Checks the storage only (vote family
        /// V5a); a row whose frame is not yet published - inside the reveal that decides it - has no decision to give,
        /// and is refused, so it is read only between commands or outside a reveal.
        /// </summary>
        public static IReadOnlyList<UnifiedVoteDecision> Decisions(EpisodeState s)
        {
            if (!UnifiedVoteFamilyValidation.TryValidateStorage(s, out string error))
                throw new ArgumentException(error, nameof(s));
            var result = new List<UnifiedVoteDecision>();
            foreach (var row in s.unifiedCommitments.Where(item => item.kind == UnifiedVoteTogether.Vote
                && (item.status == DealStatus.Fulfilled || item.status == DealStatus.Broken)))
            {
                if (!UnifiedVoteFamilyValidation.FirstDecision(s, s.unifiedVoteReveals, row,
                    out var frame, out string status, out string actor, out error) || frame == null)
                    throw new ArgumentException(error ?? "Missing actual Vote decision.", nameof(s));
                result.Add(new UnifiedVoteDecision(row, frame, status, actor));
            }
            return Array.AsReadOnly(result.ToArray());
        }

        public static UnifiedVoteDecision FindDecision(EpisodeState s, string id) =>
            Decisions(s).FirstOrDefault(decision => decision.Record.id == id);

        /// <summary>
        /// The betrayal identity a Broken Vote row carries: policy, duty, target, parties and the
        /// week it was decided. Pure: it reads only its arguments.
        /// </summary>
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
}
