// Frozen29 (SCH30): the schema-29 whole reader, carried from FrozenV28UnifiedVoteRevealArchive.cs and then by exactly the live
// validators' changes from 515246bf to b208d46d (ALL-STAR-HISTORIES-PLAN 8.3). Provenance comments below
// describe the schema-28 image this copy started from.
// Detached historical read-only archive proof. Projection/writer path intentionally excluded.
using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Persistence.Frozen29Data;

namespace Gamesim.Persistence.Frozen29ReadOnly
{
    /// <summary>
    /// Bounded complete private evidence for a fresh source season, passed separately from state.
    /// No archive is installed, authority enabled, commitment settled or ballot knowledge granted.
    /// This is not whole-save validation and does not backfill legacy ledger-incomplete saves.
    /// </summary>
    internal static class UnifiedVoteRevealArchive
    {
        public const int MaximumFrames = 100;

        public static bool TryValidateComplete(EpisodeState state,
            IReadOnlyList<UnifiedVoteRevealState> archive, out string error)
        {
            if (!TryPlan(state, out var weeks, out error)) return false;
            return TryFrames(state, archive, weeks, out error);
        }

        private static bool TryPlan(EpisodeState state, out List<int> weeks, out string error)
        {
            weeks = null;
            if (!UnifiedVoteCompletedReveal.TryContext(state, out var context, out error)) return false;
            if (!Enum.IsDefined(typeof(EvictionStage), state.evictionStage))
                return Fail(out error, "Invalid regular-reveal archive stage.");
            var required = new List<int>();
            foreach (var row in context.Powers)
            {
                int present = context.People.Count(person => context.Present(person.id, row.week));
                if (string.IsNullOrEmpty(row.evicteeId))
                {
                    if (row.week != state.week || present < 4 || !PendingVeto(state, context, row))
                        return Fail(out error, "An incomplete power owner must be the actual current pending veto decision.");
                    continue;
                }
                if (present == 3)
                {
                    // RecordFinalEviction receives the TWO OTHER finalists, not the HoH. Its
                    // source row has no veto/tally, and no later week follows this choice.
                    bool afterChoice = state.phase == EpisodePhase.JuryQuestioning || state.phase == EpisodePhase.FinalSpeeches
                        || state.phase == EpisodePhase.Jury || state.phase == EpisodePhase.Finished;
                    if (row.week != state.week || !afterChoice || !context.Present(row.hohId, row.week)
                        || state.hohId != row.hohId || state.vetoHolderId != null || state.vetoResolved || state.evictionResolved
                        || state.nominees == null || state.nominees.Count != 0
                        || row.nominees.Count != 2 || row.nominees.Contains(row.hohId)
                        || row.nominees.Any(id => !context.Present(id, row.week)) || !row.nominees.Contains(row.evicteeId)
                        || !context.People.Any(person => person.id == row.evicteeId && person.status == ContestantStatus.Jury)
                        || row.tally.Count != 0 || row.vetoHolderId != null || row.vetoUsed || row.savedId != null
                        || row.replacementId != null || row.backdoorTargetId != null || row.backdoorResult != null)
                        return Fail(out error, "A final-selection power row must be the actual current three-person choice, not a regular reveal.");
                    continue;
                }
                if (present < 4 || row.tally.Count != 2)
                    return Fail(out error, "A regular power owner must retain its ordinary tally and pre-reveal roster.");
                required.Add(row.week);
            }
            // Only a resolved regular eviction advances the week in a fresh source season.
            // The power cap (512) exceeds all supported weeks (100), so erasing both the frame
            // and owner cannot excuse a hole. This restriction is NOT a new legacy load gate.
            for (int week = 1; week < state.week; week++)
                if (!required.Contains(week)) return Fail(out error, "Complete fresh-source history is missing a prior regular power owner.");
            var current = context.Powers.FirstOrDefault(row => row.week == state.week);
            bool postRegular = state.phase == EpisodePhase.Eviction && state.evictionStage == EvictionStage.Results
                || state.phase == EpisodePhase.Social && state.evictionResolved;
            bool postFinal = state.phase == EpisodePhase.JuryQuestioning || state.phase == EpisodePhase.FinalSpeeches
                || state.phase == EpisodePhase.Jury || state.phase == EpisodePhase.Finished;
            bool finalSetup = state.phase == EpisodePhase.FinalHoHPart1 || state.phase == EpisodePhase.FinalHoHPart2
                || state.phase == EpisodePhase.FinalHoHPart3 || state.phase == EpisodePhase.FinalEviction;
            int currentPresent = context.People.Count(person => context.Present(person.id, state.week));
            if (postRegular && !required.Contains(state.week) || postFinal && (current == null || currentPresent != 3)
                || finalSetup && (current != null || currentPresent != 3)
                || current == null && (state.vetoResolved || state.evictionResolved || state.phase == EpisodePhase.Campaign
                    || state.phase == EpisodePhase.Eviction)
                || currentPresent < 4 && !finalSetup && !postFinal && !(state.phase == EpisodePhase.Social && !state.evictionResolved))
                return Fail(out error, "Current source chronology is missing or contradicts its regular/final power owner.");
            required.Sort();
            weeks = required;
            return true;
        }

        private static bool PendingVeto(EpisodeState state, UnifiedVoteCompletedReveal.Context context, PowerRow row)
        {
            bool phase = state.phase == EpisodePhase.VetoMeeting || state.phase == EpisodePhase.Campaign
                || state.phase == EpisodePhase.Eviction && (state.evictionStage == EvictionStage.Speeches
                    || state.evictionStage == EvictionStage.Voting || state.evictionStage == EvictionStage.Tiebreaker);
            if (!phase || !state.vetoResolved || state.evictionResolved || !context.Present(row.hohId, row.week)
                || !context.Present(row.vetoHolderId, row.week) || row.hohId != state.hohId || row.vetoHolderId != state.vetoHolderId
                || row.nominees.Count != 0 || row.tally.Count != 0 || row.evicteeId != null
                || state.nominees == null || state.nominees.Count != 2 || state.nominees.Distinct(StringComparer.Ordinal).Count() != 2
                || state.nominees.Contains(row.hohId) || state.nominees.Any(id => !context.Present(id, row.week))
                || row.backdoorTargetId != null || row.backdoorResult != null)
                return false;
            if (!row.vetoUsed) return row.savedId == null && row.replacementId == null;
            return context.Present(row.savedId, row.week) && context.Present(row.replacementId, row.week)
                && row.savedId != row.replacementId && row.savedId != row.hohId && !state.nominees.Contains(row.savedId)
                && state.nominees.Contains(row.replacementId) && row.replacementId != row.vetoHolderId
                && (context.People.Count(person => context.Present(person.id, row.week)) != 4
                    || row.vetoHolderId == row.savedId
                    || state.nominees.Any(id => id != row.replacementId && id == row.vetoHolderId));
        }

        private static bool TryCollection(IReadOnlyList<UnifiedVoteRevealState> archive, int currentWeek, out string error)
        {
            error = null;
            if (archive == null || archive.Count < 0 || archive.Count > MaximumFrames)
                return Fail(out error, "A complete regular-reveal archive must be nonnull and bounded.");
            int previous = 0;
            for (int index = 0; index < archive.Count; index++)
            {
                var frame = archive[index];
                if (frame == null || frame.week <= previous || frame.week > currentWeek)
                    return Fail(out error, "Regular-reveal archive weeks must be unique, ascending and already reached.");
                previous = frame.week;
            }
            return true;
        }

        private static bool TryFrames(EpisodeState state, IReadOnlyList<UnifiedVoteRevealState> archive,
            IReadOnlyList<int> expected, out string error)
        {
            if (!TryCollection(archive, state.week, out error)) return false;
            if (archive.Count != expected.Count)
                return Fail(out error, "Archive frames must exactly cover every completed regular power owner.");
            for (int index = 0; index < archive.Count; index++)
            {
                var frame = archive[index];
                if (frame.week != expected[index])
                    return Fail(out error, "Archive frames must exactly cover every completed regular power owner.");
                if (!UnifiedVoteCompletedReveal.TryValidate(state, frame, out error)) return false;
                if (frame.week == state.week && !SameCurrentOrder(state, frame))
                    return Fail(out error, "The current regular frame must preserve the actual private-box order.");
            }
            return true;
        }

        private static bool SameCurrentOrder(EpisodeState state, UnifiedVoteRevealState frame)
        {
            for (int index = 0; index < state.votes.Count; index++)
                if (state.votes[index].voterId != frame.ballots[index].voterId
                    || state.votes[index].targetId != frame.ballots[index].targetId) return false;
            return true;
        }
        private static bool Fail(out string error, string message) { error = message; return false; }
    }
}

