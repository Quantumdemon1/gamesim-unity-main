using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>One actual final-veto Safety fulfillment; every agreement remains evidence, not another reward.</summary>
    public sealed class UnifiedCommitmentFulfillment
    {
        public readonly int SettledWeek;
        public readonly string FirstId, SecondId, EffectOwnerId;
        public readonly IReadOnlyList<string> EvidenceIds;
        internal UnifiedCommitmentFulfillment(int week, string first, string second, string owner, IEnumerable<string> ids)
        {
            SettledWeek = week; FirstId = first; SecondId = second; EffectOwnerId = owner;
            EvidenceIds = Array.AsReadOnly(ids.OrderBy(id => id, StringComparer.Ordinal).ToArray());
        }
    }

    /// <summary>
    /// Read-only canonical evidence and grouped breach history. No legacy projection is counted
    /// as another betrayal. Public rule1 requires the complete validated enabled-mode context.
    /// </summary>
    public static class UnifiedCommitmentHistory
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
            if (state?.unifiedCommitmentRulesVersion == UnifiedVoteFamilyValidation.Version)
            {
                if (!UnifiedVoteFamilyValidation.TryValidate(state, state.unifiedVoteReveals, out string aggregateError))
                    throw new ArgumentException(aggregateError, nameof(state));
            }
            else
            {
                if (!UnifiedCommitments.RulesOn(state)) return Array.Empty<UnifiedCommitmentState>();
                if (!UnifiedCommitments.ValidateRecords(state, out string error))
                    throw new ArgumentException(error, nameof(state));
            }
            return Array.AsReadOnly(state.unifiedCommitments.Where(row => row.kind == UnifiedCommitments.Safety).OrderBy(row => row.id, StringComparer.Ordinal)
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
}
