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
    /// Prospective Vote evidence, separate from Safety nomination/spared history. Rows remain
    /// agreement provenance; this class intentionally does not choose effect owners, rewards,
    /// directional incident counts or audible knowledge. Those require the later owner plan.
    /// </summary>
    public static class UnifiedVoteHistory
    {
        public static IReadOnlyList<UnifiedCommitmentState> Records(EpisodeState s)
        {
            if (!UnifiedVoteFamilyValidation.TryValidate(s, s?.unifiedVoteReveals, out string error))
                throw new ArgumentException(error, nameof(s));
            return Array.AsReadOnly(s.unifiedCommitments.Where(row => row.kind == UnifiedVoteTogether.Vote)
                .Select(row => row.Clone()).ToArray());
        }

        public static IReadOnlyList<UnifiedVoteDecision> Decisions(EpisodeState s)
        {
            if (!UnifiedVoteFamilyValidation.TryValidate(s, s?.unifiedVoteReveals, out string error))
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
