using System;
using System.Collections.Generic;
using System.Linq;

namespace Gamesim.Simulation
{
    /// <summary>
    /// Pure proof of one actual completed regular reveal against durable power/departure data.
    /// This does not validate a whole save, install an archive, settle a commitment, or disclose
    /// private ballots. Supported public authority modes still require their archive to be empty.
    /// </summary>
    public static class UnifiedVoteCompletedReveal
    {
        public static bool TryValidate(EpisodeState state, UnifiedVoteRevealState reveal, out string error)
        {
            error = null;
            if (state == null || reveal == null || state.week < 1 || state.week > 100
                || reveal.week < 1 || reveal.week > state.week || !Enum.IsDefined(typeof(EpisodePhase), state.phase))
                return Fail(out error, "Invalid completed-reveal context or week.");
            if (!TryContext(state, out var context, out error)) return false;
            var people = context.People;
            var powers = context.Powers;
            bool Known(string id) => context.Known(id);
            bool Present(string id) => context.Present(id, reveal.week);

            var selected = powers.FirstOrDefault(row => row.week == reveal.week);
            if (selected == null || !Known(selected.hohId) || !Known(selected.vetoHolderId)
                || selected.nominees.Count != 2 || selected.nominees.Contains(selected.hohId)
                || !Known(selected.evicteeId) || !selected.nominees.Contains(selected.evicteeId) || selected.tally.Count != 2)
                return Fail(out error, "A completed regular reveal requires its actual power, veto, final pair and evictee.");
            if (!Present(selected.hohId) || !Present(selected.vetoHolderId) || selected.nominees.Any(id => !Present(id)))
                return Fail(out error, "A regular reveal role departed before its week.");
            if (selected.vetoUsed
                ? !Present(selected.savedId) || !Present(selected.replacementId) || selected.savedId == selected.replacementId
                    || selected.savedId == selected.hohId || selected.nominees.Contains(selected.savedId)
                    || !selected.nominees.Contains(selected.replacementId) || selected.replacementId == selected.vetoHolderId
                : selected.savedId != null || selected.replacementId != null)
                return Fail(out error, "The completed reveal lacks a coherent actual veto decision.");
            // At the actual final four the source forbids use by a holder off the original block.
            // Reconstruct that block from the saved person plus the nonreplacement final nominee;
            // historical current statuses/private jury ballots are not its pre-veto roster.
            if (selected.vetoUsed && people.Count(person => Present(person.id)) == 4
                && selected.vetoHolderId != selected.savedId
                && !selected.nominees.Any(id => id != selected.replacementId && id == selected.vetoHolderId))
                return Fail(out error, "A final-four veto holder off the original block cannot have used the veto.");

            // Production can remove an ordinary voter only after this week's completed reveal:
            // same-week removals therefore remain present at that reveal, unlike earlier departures.
            var voters = new HashSet<string>(people.Where(person => Present(person.id)
                && person.id != selected.hohId && !selected.nominees.Contains(person.id)).Select(person => person.id), StringComparer.Ordinal);
            if (voters.Count < 1 || reveal.ballots == null || reveal.ballots.Count > people.Count
                || reveal.ballots.Any(ballot => ballot == null || !Known(ballot.voterId)
                    || !selected.nominees.Contains(ballot.targetId) || selected.nominees.Contains(ballot.voterId))
                || reveal.ballots.Select(ballot => ballot.voterId).Distinct(StringComparer.Ordinal).Count() != reveal.ballots.Count)
                return Fail(out error, "Invalid completed-reveal actual ballot rows.");
            var ordinary = reveal.ballots.Where(ballot => ballot.voterId != selected.hohId).ToArray();
            if (!voters.SetEquals(ordinary.Select(ballot => ballot.voterId)))
                return Fail(out error, "The completed reveal must retain every actual ordinary voter exactly once.");
            int first = ordinary.Count(ballot => ballot.targetId == selected.nominees[0]);
            int second = ordinary.Count(ballot => ballot.targetId == selected.nominees[1]);
            if (selected.tally[0] != first || selected.tally[1] != second)
                return Fail(out error, "The durable tally must count ordinary ballots, not the HoH tie-break.");
            var deciding = reveal.ballots.FirstOrDefault(ballot => ballot.voterId == selected.hohId);
            if (first == second ? deciding == null || deciding.targetId != selected.evicteeId
                : deciding != null || selected.evicteeId != selected.nominees[first > second ? 0 : 1])
                return Fail(out error, "The actual HoH ballot and evictee must match the tied or majority verdict.");

            if (reveal.week == state.week)
            {
                bool completedPhase = state.phase == EpisodePhase.Eviction && state.evictionStage == EvictionStage.Results
                    || state.phase == EpisodePhase.Social && state.evictionStage == EvictionStage.Interaction;
                if (!completedPhase || !state.evictionResolved || !state.vetoResolved || state.hohId != selected.hohId
                    || state.vetoHolderId != selected.vetoHolderId || state.nominees == null
                    || !state.nominees.SequenceEqual(selected.nominees) || state.votes == null
                    || state.votes.Count != reveal.ballots.Count || state.votes.Any(vote => vote == null)
                    || state.votes.Select(vote => vote.voterId).Distinct(StringComparer.Ordinal).Count() != state.votes.Count
                    || state.votes.Any(vote => !reveal.ballots.Any(ballot => ballot.voterId == vote.voterId && ballot.targetId == vote.targetId)))
                    return Fail(out error, "A current-week archive requires the actual completed private box and current power context.");
            }
            return true;
        }

        // Shared unchanged global proof also validates an empty prospective archive, without
        // manufacturing a sentinel frame. It carries references only inside this pure call.
        internal sealed class Context
        {
            internal List<ContestantState> People;
            internal List<PowerRow> Powers;
            internal HashSet<string> Ids;
            internal Dictionary<string, int> Departures;
            internal bool Known(string id) => id != null && Ids.Contains(id);
            internal bool Present(string id, int week) => Known(id)
                && (!Departures.TryGetValue(id, out int leftWeek) || leftWeek >= week);
        }

        internal static bool TryContext(EpisodeState state, out Context context, out string error)
        {
            context = null;
            error = null;
            if (state == null || state.week < 1 || state.week > 100 || !Enum.IsDefined(typeof(EpisodePhase), state.phase))
                return Fail(out error, "Invalid completed-reveal context or week.");
            var people = state.contestants;
            if (people == null || people.Count < EpisodeValidation.MinimumCast || people.Count > EpisodeValidation.MaximumCast
                || people.Any(person => person == null || !IdText(person.id)
                    || !Enum.IsDefined(typeof(ContestantStatus), person.status))
                || people.Select(person => person.id).Distinct(StringComparer.Ordinal).Count() != people.Count)
                return Fail(out error, "Invalid completed-reveal cast.");
            var ids = new HashSet<string>(people.Select(person => person.id), StringComparer.Ordinal);
            bool Known(string id) => id != null && ids.Contains(id);
            bool Optional(string id) => string.IsNullOrEmpty(id) || Known(id);
            bool Week(int week) => week >= 1 && week <= state.week;
            var powers = state.ledger?.power;
            var removals = state.story?.removals;
            if (powers == null || powers.Count > SeasonLedger.MostRows || powers.Any(row => row == null
                    || !Week(row.week) || !Optional(row.hohId) || !Optional(row.vetoHolderId)
                    || !Optional(row.savedId) || !Optional(row.replacementId) || !Optional(row.evicteeId)
                    || row.nominees == null || row.nominees.Count > 2 || row.nominees.Any(id => !Known(id))
                    || row.nominees.Distinct(StringComparer.Ordinal).Count() != row.nominees.Count
                    || row.tally == null || row.tally.Count > 2 || row.tally.Any(count => count < 0 || count > people.Count))
                || powers.Select(row => row.week).Distinct().Count() != powers.Count)
                return Fail(out error, "Invalid or ambiguous durable reveal power rows.");
            if (removals == null || removals.Count > people.Count || removals.Any(row => row == null
                    || !Known(row.contestantId) || !Week(row.week) || row.reasonId != "conduct")
                || removals.Select(row => row.contestantId).Distinct(StringComparer.Ordinal).Count() != removals.Count)
                return Fail(out error, "Invalid durable reveal removals.");

            // Validate every departure/status before looking up a week's HoH or eligible voters.
            // Regular and final-eviction power rows both prove a departure; final rows are not
            // themselves regular ballot reveals. No event/claim/knowledge row substitutes here.
            var departures = new Dictionary<string, int>(StringComparer.Ordinal);
            var expelled = new HashSet<string>(removals.Select(row => row.contestantId), StringComparer.Ordinal);
            foreach (var power in powers.Where(row => !string.IsNullOrEmpty(row.evicteeId)))
            {
                if (departures.ContainsKey(power.evicteeId)) return Fail(out error, "Repeated durable eviction departure.");
                departures.Add(power.evicteeId, power.week);
            }
            foreach (var removal in removals)
            {
                if (departures.ContainsKey(removal.contestantId)) return Fail(out error, "Conflicting durable departure owners.");
                departures.Add(removal.contestantId, removal.week);
            }
            foreach (var person in people)
            {
                bool left = departures.ContainsKey(person.id);
                bool removed = expelled.Contains(person.id);
                if (person.status == ContestantStatus.Expelled ? !left || !removed
                    : person.status == ContestantStatus.Jury || person.status == ContestantStatus.Evicted ? !left || removed
                    : left)
                    return Fail(out error, "Cast status contradicts its durable departure.");
            }

            context = new Context { People = people, Powers = powers, Ids = ids, Departures = departures };
            return true;
        }

        private static bool IdText(string id) => !string.IsNullOrWhiteSpace(id) && id.Length <= 100
            && !id.Any(char.IsControl);
        private static bool Fail(out string error, string message) { error = message; return false; }
    }
}
