using System;
using System.Collections;
using System.Linq;
using Gamesim.Episode;
using Gamesim.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    // Extends the existing fixture: a test-owned scene/save root and shared diary/navigation helpers.
    public sealed partial class EpisodePlayModeTests
    {
        [UnityTest]
        public IEnumerator StudyDiary_ViewReviewCancelAndForgedChoicesNeverSpendOrRoll()
        {
            var before = director.Snapshot;
            yield return OpenDiaryFixturePanel();
            AssertEquivalent(before, director.Snapshot);
            director.ReviewStudyHouse("forged-study-choice");
            Assert.That(director.HasDiaryDecisionDraft, Is.False);
            AssertEquivalent(before, director.Snapshot);

            ButtonWithCaption(EpisodeHud.StudyMemorizeCaption).onClick.Invoke();
            yield return null; yield return null;
            Assert.That(director.HasDiaryDecisionDraft, Is.True);
            Assert.That(ActiveDiaryText(), Does.Contain("Guaranteed +1 preparation, up to the limit of 5."));
            Assert.That(ActiveDiaryText(), Does.Contain("Cost: 1 social action"));
            Assert.That(ActiveDiaryText(), Does.Contain("18 of 18 remaining this free-time window"));
            AssertEquivalent(before, director.Snapshot);
            ButtonWithCaption(EpisodeHud.StudyCancelCaption).onClick.Invoke();
            yield return null; yield return null;
            Assert.That(director.HasDiaryDecisionDraft, Is.False);
            AssertEquivalent(before, director.Snapshot);

            ButtonWithCaption(EpisodeHud.StudySneakCaption).onClick.Invoke();
            yield return null; yield return null;
            Assert.That(ActiveDiaryText(), Does.Contain("Success chance: 45%"));
            Assert.That(ActiveDiaryText(), Does.Contain("Success adds 2 preparation; failure removes 1"));
            yield return PressDiaryKey(Key.Escape);
            Assert.That(director.IsPanelOpen, Is.False);
            Assert.That(director.HasDiaryDecisionDraft, Is.False);
            AssertEquivalent(before, director.Snapshot);
        }

        [UnityTest]
        public IEnumerator StudyDiary_ActualConfirmCommitsOncePrivatelyAndReloadPreservesPreparation()
        {
            var before = director.Snapshot;
            var expected = StudyUiExpected(before, EpisodeCommandKind.StudyHouse, "memorize-layout");
            yield return OpenDiaryFixturePanel();
            ButtonWithCaption(EpisodeHud.StudyMemorizeCaption).onClick.Invoke();
            yield return null; yield return null;
            var confirm = ButtonWithCaption(EpisodeHud.StudyConfirmCaption).onClick;
            confirm.Invoke(); confirm.Invoke();
            yield return null; yield return null;
            var after = director.Snapshot;
            Assert.That(after.revision, Is.EqualTo(before.revision + 1));
            Assert.That(after.socialActions, Is.EqualTo(before.socialActions + 1));
            Assert.That(after.randomState, Is.EqualTo(expected.randomState));
            Assert.That(after.playerStudyBonus, Is.EqualTo(expected.playerStudyBonus));
            Assert.That(after.playerStudyBonus, Is.EqualTo(1));
            Assert.That(after.phase, Is.EqualTo(EpisodePhase.Social));
            Assert.That(after.relationships.Select(edge => edge.score), Is.EqualTo(before.relationships.Select(edge => edge.score)));
            Assert.That(after.memories.Select(memory => memory.ownerId + ":" + memory.text),
                Is.EqualTo(before.memories.Select(memory => memory.ownerId + ":" + memory.text)));
            Assert.That(JsonUtility.ToJson(after.playerPersona), Is.EqualTo(JsonUtility.ToJson(before.playerPersona)));
            Assert.That(JsonUtility.ToJson(after.jurySentiment), Is.EqualTo(JsonUtility.ToJson(before.jurySentiment)));
            var addedEvents = after.events.Skip(before.events.Count).ToArray();
            Assert.That(addedEvents, Has.Length.EqualTo(1));
            Assert.That(addedEvents[0].audienceIds, Is.EquivalentTo(new[] { before.playerId }));
            Assert.That(director.HasDiaryDecisionDraft, Is.False);
            yield return ReloadEpisode();
            AssertEquivalent(after, director.Snapshot);
            yield return OpenDiaryFixturePanel();
            Assert.That(ActiveDiaryText(), Does.Contain("Study preparation: 1/5"));
        }

        [UnityTest]
        public IEnumerator StudyDiary_OldReviewConfirmAndCancelCannotOverwriteOrAcceptAReplacementDraft()
        {
            var before = director.Snapshot;
            yield return OpenDiaryFixturePanel();
            var oldReview = ButtonWithCaption(EpisodeHud.StudyMemorizeCaption).onClick;
            oldReview.Invoke();
            yield return null; yield return null;
            var oldConfirm = ButtonWithCaption(EpisodeHud.StudyConfirmCaption).onClick;
            var oldCancel = ButtonWithCaption(EpisodeHud.StudyCancelCaption).onClick;
            oldCancel.Invoke();
            yield return null; yield return null;
            ButtonWithCaption(EpisodeHud.StudySneakCaption).onClick.Invoke();
            yield return null; yield return null;

            oldReview.Invoke(); oldConfirm.Invoke(); oldCancel.Invoke();
            yield return null; yield return null;
            Assert.That(director.HasDiaryDecisionDraft, Is.True, "Old UI callbacks must leave the newly reviewed approach intact.");
            Assert.That(ActiveDiaryText(), Does.Contain("Sneak a peek at production notes."));
            AssertEquivalent(before, director.Snapshot);
            var expected = StudyUiExpected(before, EpisodeCommandKind.StudyHouse, "sneak-peek");
            ButtonWithCaption(EpisodeHud.StudyConfirmCaption).onClick.Invoke();
            yield return null; yield return null;
            Assert.That(director.Snapshot.revision, Is.EqualTo(before.revision + 1));
            Assert.That(director.Snapshot.playerStudyBonus, Is.EqualTo(expected.playerStudyBonus));
            Assert.That(director.Snapshot.randomState, Is.EqualTo(expected.randomState));
        }

        [UnityTest]
        public IEnumerator StudyDiary_InterveningRevisionAndLostRoomAccessInvalidateConfirmation()
        {
            yield return OpenDiaryFixturePanel();
            ButtonWithCaption(EpisodeHud.StudyMemorizeCaption).onClick.Invoke();
            yield return null; yield return null;
            var detachedConfirm = ButtonWithCaption(EpisodeHud.StudyConfirmCaption).onClick;
            var beforeMove = director.Snapshot;
            WarpPlayer(new Vector3(0, 0, 14));
            detachedConfirm.Invoke(); // Check access immediately, before Update can close the panel.
            Assert.That(director.IsDiaryOpen, Is.False);
            AssertEquivalent(beforeMove, director.Snapshot);

            yield return OpenDiaryFixturePanel();
            ButtonWithCaption(EpisodeHud.StudyMemorizeCaption).onClick.Invoke();
            yield return null; yield return null;
            detachedConfirm = ButtonWithCaption(EpisodeHud.StudyConfirmCaption).onClick;
            var advance = director.Submit(NextCommand(director.Snapshot));
            Assert.That(advance.accepted, Is.True, advance.reason);
            Assert.That(advance.state.phase, Is.EqualTo(EpisodePhase.HoH));
            detachedConfirm.Invoke();
            director.ReviewStudyHouse("sneak-peek");
            yield return null; yield return null;
            Assert.That(director.HasDiaryDecisionDraft, Is.False);
            Assert.That(DiaryHasButton(EpisodeHud.StudyMemorizeCaption), Is.False);
            AssertEquivalent(advance.state, director.Snapshot);
        }

        [UnityTest]
        public IEnumerator StudyDiary_ActionBudgetDisablesStudyAndPendingReflectionCannotBeBypassed()
        {
            yield return OpenDiaryFixturePanel();
            for (int action = 0; action < 18; action++)
            {
                ButtonWithCaption(EpisodeHud.StudyMemorizeCaption).onClick.Invoke();
                yield return null; yield return null;
                ButtonWithCaption(EpisodeHud.StudyConfirmCaption).onClick.Invoke();
                yield return null; yield return null;
            }
            var exhausted = director.Snapshot;
            Assert.That(exhausted.socialActions, Is.EqualTo(18));
            Assert.That(exhausted.playerStudyBonus, Is.EqualTo(5));
            Assert.That(DiaryHasButton(EpisodeHud.StudyMemorizeCaption), Is.False);
            Assert.That(DiaryHasButton(EpisodeHud.StudySneakCaption), Is.False);
            director.ReviewStudyHouse("memorize-layout"); director.ConfirmDiaryDecision();
            AssertEquivalent(exhausted, director.Snapshot);

            yield return InstallDiaryFixture(state => state.pendingDiary != null, "pending private reflection before study");
            // An active player may retain the pending reflection when moving from resolved Eviction to Social.
            var transition = NextCommand(director.Snapshot); transition.kind = EpisodeCommandKind.Advance;
            transition.targetId = null;
            var advanced = director.Submit(transition);
            Assert.That(advanced.accepted, Is.True, advanced.reason);
            Assert.That(advanced.state.phase, Is.EqualTo(EpisodePhase.Social));
            Assert.That(advanced.state.pendingDiary, Is.Not.Null);
            yield return OpenDiaryFixturePanel();
            Assert.That(DiaryHasButton(EpisodeHud.StudyMemorizeCaption), Is.False);
            Assert.That(ActiveDiaryText(), Does.Contain("Answer or skip your pending private reflection before studying."));
            director.ReviewStudyHouse("memorize-layout"); director.ConfirmDiaryDecision();
            AssertEquivalent(advanced.state, director.Snapshot);
        }

        [UnityTest]
        public IEnumerator StudyCompetition_ActualWeeklySimulateButtonUsesPreparationOnceAndNeverAppearsInFinalHoh()
        {
            yield return OpenDiaryFixturePanel();
            ButtonWithCaption(EpisodeHud.StudyMemorizeCaption).onClick.Invoke();
            yield return null; yield return null;
            ButtonWithCaption(EpisodeHud.StudyConfirmCaption).onClick.Invoke();
            yield return null; yield return null;
            director.ClosePanels();
            WarpPlayer(director.StationPosition);
            Assert.That(director.TryOpenPhasePanel(), Is.True);
            ButtonWithCaption("Begin the next competition").onClick.Invoke();
            yield return null; yield return null;
            WarpPlayer(director.StationPosition);
            Assert.That(director.TryOpenPhasePanel(), Is.True);
            var before = director.Snapshot;
            var expected = StudyUiExpected(before, EpisodeCommandKind.SimulateCompetition);
            Assert.That(DiaryHasButton("Enter precision challenge"), Is.True);
            Assert.That(ActiveDiaryText(), Does.Contain("WEEK " + before.week + " · " + before.Active.Count() + " houseguests remain"));
            var simulate = ButtonWithCaption(EpisodeHud.SimulateCompetitionCaption).onClick;
            simulate.Invoke(); simulate.Invoke();
            yield return null; yield return null;
            var after = director.Snapshot;
            Assert.That(after.revision, Is.EqualTo(before.revision + 1));
            Assert.That(after.competitionResolved, Is.True);
            Assert.That(after.hohId, Is.EqualTo(expected.hohId));
            Assert.That(after.randomState, Is.EqualTo(expected.randomState));
            Assert.That(after.competitionScores.Select(score => score.score), Is.EqualTo(expected.competitionScores.Select(score => score.score)));
            Assert.That(after.playerStudyBonus, Is.EqualTo(before.playerStudyBonus));
            yield return ReloadEpisode();
            AssertEquivalent(after, director.Snapshot);

            yield return InstallDiaryFixture(state => state.phase == EpisodePhase.FinalHoHPart1
                && !state.competitionResolved && state.Active.Any(actor => actor.isPlayer), "active player in final HoH");
            WarpPlayer(director.StationPosition);
            Assert.That(director.TryOpenPhasePanel(), Is.True);
            var finalHoh = director.Snapshot;
            Assert.That(DiaryHasButton(EpisodeHud.SimulateCompetitionCaption), Is.False);
            director.SimulateCompetition();
            AssertEquivalent(finalHoh, director.Snapshot);

            yield return InstallDiaryFixture(state => state.phase == EpisodePhase.Finished, "completed season subtitle");
            Assert.That(director.TryOpenPhasePanel(), Is.True);
            Assert.That(ActiveDiaryText(), Does.Contain("WEEK " + director.Snapshot.week + " · Season complete"));
            Assert.That(ActiveDiaryText(), Does.Not.Contain("0 houseguests remain"));
        }

        private static EpisodeState StudyUiExpected(EpisodeState before, EpisodeCommandKind kind, string choice = null)
        {
            var expected = new EpisodeEngine(before).Apply(new EpisodeCommand
            {
                id = "study-ui-expectation-" + Guid.NewGuid().ToString("N"), actorId = before.playerId,
                expectedPhase = before.phase, expectedRevision = before.revision, kind = kind, targetId = choice
            });
            Assert.That(expected.accepted, Is.True, expected.reason);
            return expected.state;
        }
    }
}
