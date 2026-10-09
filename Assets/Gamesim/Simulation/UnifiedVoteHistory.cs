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
    /// One Vote breach incident as the Rule2 plan grouped it (vote family V5b, the lead's decision D1): a reveal's week, the
    /// one who broke their word and the one it wronged, every row that broke it, and the row whose consequence won - the
    /// most severe, then the ordinal id - with the most severe deal row among them. Detached; no knowledge grant.
    /// </summary>
    public sealed class UnifiedVoteIncident
    {
        public readonly int Week;
        public readonly string ActorId, WrongedId, OwnerId, DealId;
        public readonly bool OwnerIsPromise;
        /// <summary>The owner's native source consequence: a broken vote promise's, or the deal's breach weight times the broken base.</summary>
        public readonly double SourceConsequence;
        public readonly IReadOnlyList<string> EvidenceIds;
        internal UnifiedVoteIncident(int week, string actor, string wronged, string owner, string deal, bool ownerIsPromise, double consequence,
            IEnumerable<string> ids)
        {
            Week = week; ActorId = actor; WrongedId = wronged; OwnerId = owner; DealId = deal; OwnerIsPromise = ownerIsPromise;
            SourceConsequence = consequence; EvidenceIds = Array.AsReadOnly(ids.OrderBy(id => id, StringComparer.Ordinal).ToArray());
        }
    }

    /// <summary>
    /// Prospective Vote evidence, separate from Safety nomination/spared history. Rows remain
    /// agreement provenance; <see cref="Breaches"/> groups the broken ones as the reveal's Rule2 plan did.
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
