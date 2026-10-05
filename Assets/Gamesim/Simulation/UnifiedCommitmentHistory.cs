using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// Read-only canonical evidence and grouped breach history. No legacy projection is counted
    /// as another betrayal. Production still refuses rule 1 pending full integration/validation.
    /// </summary>
    public static class UnifiedCommitmentHistory
    {
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
}
