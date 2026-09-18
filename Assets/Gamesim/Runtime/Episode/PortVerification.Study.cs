using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Simulation;
using UnityEngine;

namespace Gamesim.Episode
{
    /// <summary>Explicit opt-in study coverage layered over the isolated actual-UI season workload.</summary>
    public sealed partial class PortVerification
    {
        private bool verifyStudy;
        private StudyReport studyReport;
        private const int StudyConfirmations = 5;

        private IEnumerator ExerciseSeasonStudy(bool graphical)
        {
            var original = seasonDirector.Snapshot;
            RequireSeason(original.phase == EpisodePhase.Social && original.week == 1
                && original.Find(original.playerId).status == ContestantStatus.Active
                && original.pendingDiary == null && original.playerStudyBonus == 0
                && original.socialActions + StudyConfirmations <= 18,
                "Study QA requires the legally created fresh season, not a fabricated role or prepared save.");
            studyReport.preparationAtStart = original.playerStudyBonus;
            yield return CloseSeasonPanel();
            yield return ClickSeasonButton(EpisodeHud.DiaryTravelCaption);
            yield return WaitSeasonWalk("study diary", seasonDirector.DiaryPosition);
            RequireSeason(seasonDirector.TryOpenDiary(), "Study requires physically reaching and opening the actual diary.");
            yield return null; yield return null;
            var arrived = seasonDirector.Snapshot;
            RequireSeason(arrived.randomState == original.randomState && arrived.socialActions == original.socialActions
                && arrived.playerStudyBonus == original.playerStudyBonus,
                "Walking to the diary must not roll/spend a player study decision; independent NPC activity may advance.");
            original = arrived; // The actual paused modal, not a pre-walk world revision.
            RequireStudyUnchanged(original, "Reading the paused study choices");
            yield return CaptureSeason("study-choices", graphical);

            yield return ClickSeasonButton(EpisodeHud.StudyMemorizeCaption);
            RequireSeason(seasonDirector.HasDiaryDecisionDraft, "The actual study choice must stage a review.");
            RequireStudyUnchanged(original, "Reviewing the first study choice");
            studyReport.reviewNoMutationChecks++;
            yield return CaptureSeason("study-review", graphical);
            yield return ClickSeasonButton(EpisodeHud.StudyCancelCaption);
            RequireSeason(!seasonDirector.HasDiaryDecisionDraft, "The study cancel button must discard the review.");
            RequireStudyUnchanged(original, "Cancelling the study choice");
            studyReport.cancelPreservedFullSnapshot = true;

            for (int attempt = 0; attempt < StudyConfirmations; attempt++)
            {
                var before = seasonDirector.Snapshot;
                yield return ClickSeasonButton(EpisodeHud.StudyMemorizeCaption);
                RequireSeason(seasonDirector.HasDiaryDecisionDraft, "Each study confirmation needs its own current review.");
                RequireStudyUnchanged(before, "Reviewing study attempt " + (attempt + 1));
                studyReport.reviewNoMutationChecks++;
                yield return ClickSeasonButton(EpisodeHud.StudyConfirmCaption);
                var after = seasonDirector.Snapshot;
                var expectedRng = new SeededRandom(before.randomState);
                expectedRng.NextDouble(); // A local expectation only; never installed in the game.
                RequireSeason(after.revision == before.revision + 1 && after.socialActions == before.socialActions + 1
                    && after.playerStudyBonus == Math.Min(5, before.playerStudyBonus + 1)
                    && after.randomState == expectedRng.State && !seasonDirector.HasDiaryDecisionDraft,
                    "One actual memorize confirmation must cost one action, draw once, and add one bounded preparation.");
                RequireSeason(after.acceptedCommandIds.Count == before.acceptedCommandIds.Count + 1
                    && after.acceptedCommandIds.Take(before.acceptedCommandIds.Count).SequenceEqual(before.acceptedCommandIds)
                    && after.nextSequence == before.nextSequence + 1 && after.events.Count == before.events.Count + 1,
                    "A study confirmation must have one new receipt and one result, not extra hidden effects.");
                var result = after.events.Last();
                RequireSeason(result.kind == "study-house" && result.audienceIds.SequenceEqual(new[] { before.playerId }),
                    "The study result must remain private to the player.");
                RequireSeason(after.events.Take(before.events.Count).Select(item => JsonUtility.ToJson(item))
                    .SequenceEqual(before.events.Select(item => JsonUtility.ToJson(item))),
                    "A new study result must not rewrite existing season history.");
                // Restore only the explicit allowed effects and compare the entire remaining snapshot.
                // This covers full trust/history, persona, NPC/player memories and future unchanged fields.
                var unchanged = after.Clone();
                unchanged.revision = before.revision; unchanged.socialActions = before.socialActions;
                unchanged.playerStudyBonus = before.playerStudyBonus; unchanged.randomState = before.randomState;
                unchanged.nextSequence = before.nextSequence; unchanged.events = before.events;
                unchanged.acceptedCommandIds = before.acceptedCommandIds;
                RequireSeason(JsonUtility.ToJson(unchanged) == JsonUtility.ToJson(before),
                    "Study must preserve trust, persona, memories and every field outside its explicit effects.");
                studyReport.confirmations++;
                studyReport.privateEffectChecks++;
                studyReport.choices.Add(new StudyChoiceCheck
                {
                    attempt = attempt + 1, revisionBefore = before.revision, revisionAfter = after.revision,
                    socialActionsBefore = before.socialActions, socialActionsAfter = after.socialActions,
                    preparationBefore = before.playerStudyBonus, preparationAfter = after.playerStudyBonus,
                    randomStateBefore = before.randomState.ToString(), randomStateAfter = after.randomState.ToString()
                });
            }
            RequireSeason(seasonDirector.Snapshot.playerStudyBonus == 5, "Five legal memorizes must reach preparation5.");
            studyReport.preparationCapReached = true;
            studyReport.noRollBeforeConfirmation = studyReport.reviewNoMutationChecks == StudyConfirmations + 1;
            studyReport.trustPersonaMemoriesPreserved = studyReport.privateEffectChecks == StudyConfirmations;
            studyReport.socialActionsRemainingForOptionalOath =
                EpisodeEngine.SocialActionBudget(seasonDirector.Snapshot)
                - EpisodeEngine.SocialActionsSpent(seasonDirector.Snapshot);
            yield return CaptureSeason("study-result", graphical);
            yield return SaveReloadSeason("after-five-study-confirmations");
            RequireSeason(seasonDirector.Snapshot.playerStudyBonus == 5, "Reload must retain the earned preparation.");
            studyReport.studySaveReloadChecks++;
        }

        private void RequireStudyUnchanged(EpisodeState expected, string operation)
        {
            RequireSeason(JsonUtility.ToJson(seasonDirector.Snapshot) == JsonUtility.ToJson(expected),
                operation + " must preserve the full snapshot, including RNG, social cost, trust, persona and memories.");
        }

        private IEnumerator SimulateStudyWeeklyCompetition(EpisodeState before, bool graphical)
        {
            RequireSeason((before.phase == EpisodePhase.HoH || before.phase == EpisodePhase.Veto)
                && !before.competitionResolved && before.playerStudyBonus == 5,
                "The study simulation path applies only to an unresolved prepared weekly competition.");
            var participants = EpisodeEngine.CompetitionPlayers(before).ToArray();
            RequireSeason(participants.Any(actor => actor.isPlayer), "Study QA cannot assign eligibility to the player.");
            var rng = new SeededRandom(before.randomState);
            string category = before.week % 3 == 1 ? "Skill" : before.week % 3 == 2 ? "Mental" : "Endurance";
            var expected = new List<CompetitionScore>();
            double playerWithoutPreparation = 0;
            foreach (var actor in participants)
            {
                double roll = rng.NextDouble();
                double bonus = actor.isPlayer ? before.phaseEventCompBonus + before.playerStudyBonus : 0;
                expected.Add(new CompetitionScore { contestantId = actor.id, score = WebRules.WeightedCompetitionScore(
                    actor.stats, category, before.nominees.Contains(actor.id), bonus, roll, 0) });
                if (actor.isPlayer) playerWithoutPreparation = WebRules.WeightedCompetitionScore(actor.stats, category,
                    before.nominees.Contains(actor.id), before.phaseEventCompBonus, roll, 0);
            }
            if (studyReport.weeklySimulatedChecks == 0) yield return CaptureSeason("study-weekly-simulation-choice", graphical);
            yield return ClickSeasonButton(EpisodeHud.SimulateCompetitionCaption);
            var after = seasonDirector.Snapshot;
            RequireSeason(after.phase == before.phase && after.week == before.week && after.competitionResolved
                && after.revision == before.revision + 1 && after.randomState == rng.State
                && after.playerStudyBonus == before.playerStudyBonus && after.socialActions == before.socialActions,
                "The actual weekly simulate button must resolve once, preserve preparation and consume only participant score draws.");
            RequireSeason(after.competitionScores.Count == expected.Count, "The simulated result must contain exactly the eligible contestants.");
            for (int index = 0; index < expected.Count; index++)
                RequireSeason(after.competitionScores[index].contestantId == expected[index].contestantId
                    && after.competitionScores[index].score == expected[index].score,
                    "The actual weekly score must match weighted rules, preparation only for the player, and stable participant order.");
            string expectedWinner = expected.OrderByDescending(score => score.score).First().contestantId;
            RequireSeason((before.phase == EpisodePhase.HoH ? after.hohId : after.vetoHolderId) == expectedWinner,
                "The recorded weekly winner must match the actual weighted scores, with stable ties.");
            var playerScore = after.competitionScores.Single(score => score.contestantId == before.playerId).score;
            RequireSeason(playerScore > playerWithoutPreparation, "Earned preparation must actually affect the weekly player's simulated score.");
            studyReport.weeklySimulatedChecks++;
            if (before.phase == EpisodePhase.HoH) studyReport.simulatedHohChecks++; else studyReport.simulatedVetoChecks++;
            studyReport.weeklyCompetitions.Add(new StudyCompetitionCheck
            {
                phase = before.phase.ToString(), week = before.week, category = category, winnerId = expectedWinner,
                preparation = before.playerStudyBonus, eventBonus = before.phaseEventCompBonus,
                playerScore = playerScore, playerScoreWithoutPreparation = playerWithoutPreparation,
                participantIds = participants.Select(actor => actor.id).ToArray(),
                randomStateBefore = before.randomState.ToString(), randomStateAfter = after.randomState.ToString()
            });
            if (studyReport.weeklySimulatedChecks == 1) yield return CaptureSeason("study-weekly-simulation-result", graphical);
        }

        private void CheckStudyCompetitionScope(EpisodeState state)
        {
            bool weekly = state.phase == EpisodePhase.HoH || state.phase == EpisodePhase.Veto;
            if (weekly && !state.competitionResolved && !EpisodeEngine.CompetitionPlayers(state).Any(actor => actor.isPlayer))
            {
                RequireSeason(!HasSeasonButton(EpisodeHud.SimulateCompetitionCaption), "An ineligible player must not receive the weekly simulation button.");
                studyReport.ineligibleWeeklyChecks++;
            }
            if (!weekly)
            {
                RequireSeason(!HasSeasonButton(EpisodeHud.SimulateCompetitionCaption), "The weekly study simulation button must never replace a final HoH challenge.");
                studyReport.finaleSimulationAbsentChecks++;
                if (!state.competitionResolved && EpisodeEngine.CompetitionPlayers(state).Any(actor => actor.isPlayer))
                    studyReport.finaleAssistedDecisions++;
            }
        }

        private void ObserveStudyTransition(EpisodeState before, EpisodeState after)
        {
            RequireSeason(before.playerStudyBonus == 5 && after.playerStudyBonus == 5,
                "Earned study preparation must survive every later phase, week and finale decision without being spent.");
            studyReport.preparationDecisionChecks++;
            if (before.phase != after.phase) studyReport.phaseTransitionChecks++;
            if (before.week != after.week) studyReport.weekTransitionChecks++;
            studyReport.preparationAtEnd = after.playerStudyBonus;
        }

        private void FinishStudyVerification()
        {
            if (!verifyStudy) return;
            bool complete = studyReport != null && studyReport.confirmations == StudyConfirmations
                && studyReport.cancelPreservedFullSnapshot && studyReport.noRollBeforeConfirmation
                && studyReport.trustPersonaMemoriesPreserved && studyReport.preparationCapReached
                && studyReport.studySaveReloadChecks >= 1 && studyReport.weeklySimulatedChecks >= 1
                && studyReport.finaleSimulationAbsentChecks >= 1 && studyReport.phaseTransitionChecks >= 1
                && studyReport.weekTransitionChecks >= 1 && studyReport.preparationAtEnd == 5 && seasonReport.finished;
            if (!complete) RecordSeasonError("The explicitly requested study workload did not complete every mandatory study/weekly/finale/persistence check.");
            if (studyReport == null) return;
            studyReport.status = complete && seasonErrors.Count == 0 ? "Passed" : "Failed";
            studyReport.unreachedBranches = new[]
            {
                studyReport.simulatedHohChecks == 0 ? "No legally eligible player weekly HoH simulation was reached." : null,
                studyReport.simulatedVetoChecks == 0 ? "No legally eligible player weekly Veto simulation was reached." : null,
                studyReport.ineligibleWeeklyChecks == 0 ? "No player-ineligible weekly competition was reached." : null,
                studyReport.finaleAssistedDecisions == 0 ? "The player did not take an assisted final HoH decision in this season." : null,
                "Sneak-peek success/failure and confirmation while already at preparation5 are outside this five-memorize workload.",
                "Exact source success-roll boundary cases and forged-input rejection belong to dedicated deterministic tests, not this legal UI season."
            }.Where(value => value != null).ToArray();
        }

        [Serializable] private sealed class StudyChoiceCheck
        {
            public int attempt, revisionBefore, revisionAfter, socialActionsBefore, socialActionsAfter, preparationBefore, preparationAfter;
            public string randomStateBefore, randomStateAfter;
        }
        [Serializable] private sealed class StudyCompetitionCheck
        {
            public string phase, category, winnerId, randomStateBefore, randomStateAfter;
            public int week, preparation;
            public double eventBonus, playerScore, playerScoreWithoutPreparation;
            public string[] participantIds;
        }
        [Serializable] private sealed class StudyReport
        {
            public bool requested = true;
            public string status = "Running";
            public string workload = "Five legal actual-UI memorize confirmations after a non-mutating review/cancel, private-state checks and save/reload; eligible weekly weighted simulations; no invented seed, role or preparation. Separate functional coverage, not profile samples.";
            public bool cancelPreservedFullSnapshot, noRollBeforeConfirmation, trustPersonaMemoriesPreserved, preparationCapReached;
            public int confirmations, reviewNoMutationChecks, privateEffectChecks, studySaveReloadChecks,
                preparationAtStart, preparationAtEnd, socialActionsRemainingForOptionalOath,
                weeklySimulatedChecks, simulatedHohChecks, simulatedVetoChecks, ineligibleWeeklyChecks,
                finaleSimulationAbsentChecks, finaleAssistedDecisions, preparationDecisionChecks, phaseTransitionChecks, weekTransitionChecks;
            public List<StudyChoiceCheck> choices = new List<StudyChoiceCheck>();
            public List<StudyCompetitionCheck> weeklyCompetitions = new List<StudyCompetitionCheck>();
            public string[] unreachedBranches;
        }
    }
}
