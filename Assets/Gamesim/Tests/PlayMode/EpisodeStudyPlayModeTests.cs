using System;
using System.Collections;
using System.Linq;
using Gamesim.Episode;
using Gamesim.Persistence;
using Gamesim.Presentation;
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
            Assert.That(ActiveDiaryText(), Does.Contain(EpisodeEngine.SocialActionBudget(director.Snapshot)
                + " of " + EpisodeEngine.SocialActionBudget(director.Snapshot) + " remaining this week"));
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
            var addedEvents = after.events.Where(entry => entry.sequence >= before.nextSequence).ToArray();
            Assert.That(addedEvents, Has.Length.EqualTo(1));
            Assert.That(addedEvents[0].audienceIds, Is.EquivalentTo(new[] { before.playerId }));
            Assert.That(director.HasDiaryDecisionDraft, Is.False);
            yield return ReloadEpisode();
            AssertEquivalent(after, director.Snapshot);
            yield return OpenDiaryFixturePanel();
            // Beside the ways to study, and as the record's own row.
            ButtonWithCaption(EpisodeDirector.DiaryPendingTabCaption).onClick.Invoke();
            yield return null; yield return null;
            Assert.That(ActiveDiaryText(), Does.Contain("Study preparation: 1/5"));
            ButtonWithCaption(EpisodeDirector.DiaryRecordTabCaption).onClick.Invoke();
            yield return null; yield return null;
            Assert.That(ActiveRect(EpisodeHud.DiaryRecordRowPrefix + "Study preparation").GetComponentsInChildren<TMPro.TMP_Text>()
                .Select(label => label.text), Does.Contain("1 / 5"));
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
            // The week's allowance is half the active house, not a flat eighteen, so this spends
            // exactly what the house gives and then checks the controls are gone.
            int budget = EpisodeEngine.SocialActionBudget(director.Snapshot);
            Assert.That(budget, Is.GreaterThan(0));
            for (int action = 0; action < budget; action++)
            {
                ButtonWithCaption(EpisodeHud.StudyMemorizeCaption).onClick.Invoke();
                yield return null; yield return null;
                ButtonWithCaption(EpisodeHud.StudyConfirmCaption).onClick.Invoke();
                yield return null; yield return null;
            }
            var exhausted = director.Snapshot;
            Assert.That(EpisodeEngine.SocialActionsSpent(exhausted), Is.EqualTo(budget));
            Assert.That(exhausted.playerStudyBonus, Is.EqualTo(Math.Min(5, budget)),
                "Memorising banks one point per confirmed action, up to the limit of five.");
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
            // Whichever game this competition routes to, not a fixed caption: the control now names
            // the game, and week one's HoH is a skill competition rather than the timing bar.
            Assert.That(DiaryHasButton(CompetitionMiniGames.EnterCaption(
                CompetitionMiniGames.For(EpisodeEngine.CompetitionCategory(before.phase, before.week)))), Is.True);
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

        /// <summary>
        /// B9's tuning in the diary room (B9-BALANCE-PLAN §4.7): every new season since B9 starts on tuning 1, but the
        /// director's own season without a save, and every other study test's, stays at 0. So this installs a season on
        /// tuning 1 (competition rules 4, which tuning 1 needs), saved and loaded, and studies it to its limit of one: the
        /// card and the review name that limit, a confirmed study counts out of it, and once it is full no study is offered or
        /// staged and the section says what uses it up. The record's row and rules line read the season's own words.
        /// </summary>
        [UnityTest]
        public IEnumerator StudyDiary_UnderTuningOneTheStudyStopsAtTheSeasonsLimitAndSaysWhatUsesItUp()
        {
            var tuned = ContentCatalog.Create(20260910);
            tuned.competitionRulesVersion = CompetitionRules.Current;
            tuned.tuningRulesVersion = TuningRules.Bands;
            Assert.That(EpisodeValidation.TryValidate(tuned, out var reason), Is.True, reason);
            new EpisodeSaveStore(director.SavePath).Save(tuned);
            yield return ReloadEpisode();
            yield return OpenDiaryFixturePanel();
            var before = director.Snapshot;
            Assert.That(before.tuningRulesVersion, Is.EqualTo(TuningRules.Bands), "Precondition: the season is on tuning 1.");
            Assert.That(before.phase, Is.EqualTo(EpisodePhase.Social));
            Assert.That((before.playerStudyBonus, TuningRules.StudyLimit(before)), Is.EqualTo((0, 1)), "Precondition: nothing banked, and week one's limit is one.");

            ButtonWithCaption(EpisodeDirector.DiaryPendingTabCaption).onClick.Invoke();
            yield return null; yield return null;
            Assert.That(ActiveDiaryText(), Does.Contain("Study preparation: 0/1"));
            Assert.That(ActiveDiaryText(), Does.Contain("Guaranteed +1 preparation, up to the limit of 1."));
            Assert.That(ActiveDiaryText(), Does.Contain(StudyWords.StudyAside(before)));
            ButtonWithCaption(EpisodeHud.StudyMemorizeCaption).onClick.Invoke();
            yield return null; yield return null;
            Assert.That(director.HasDiaryDecisionDraft, Is.True);
            Assert.That(ActiveDiaryText(), Does.Contain("Current preparation: 0/1."));
            Assert.That(ActiveDiaryText(), Does.Not.Contain("Preparation stays between 0 and 5."), "Tuning 0's review rule is not shown.");
            AssertEquivalent(before, director.Snapshot);
            var expected = StudyUiExpected(before, EpisodeCommandKind.StudyHouse, "memorize-layout");
            ButtonWithCaption(EpisodeHud.StudyConfirmCaption).onClick.Invoke();
            yield return null; yield return null;
            var full = director.Snapshot;
            Assert.That(full.playerStudyBonus, Is.EqualTo(1));
            Assert.That(full.playerStudyBonus, Is.EqualTo(expected.playerStudyBonus));
            Assert.That(full.randomState, Is.EqualTo(expected.randomState));
            Assert.That(director.HasDiaryDecisionDraft, Is.False);

            // Full: no way to study is offered, the section says what uses preparation up, and nothing stages a study.
            ButtonWithCaption(EpisodeDirector.DiaryPendingTabCaption).onClick.Invoke();
            yield return null; yield return null;
            Assert.That(ActiveDiaryText(), Does.Contain("Study preparation: 1/1"));
            Assert.That(DiaryHasButton(EpisodeHud.StudyMemorizeCaption), Is.False);
            Assert.That(DiaryHasButton(EpisodeHud.StudySneakCaption), Is.False);
            Assert.That(ActiveDiaryText(), Does.Contain(StudyWords.FullLine(full)));
            foreach (string approach in new[] { "memorize-layout", "sneak-peek" })
            {
                director.ReviewStudyHouse(approach);
                Assert.That(director.HasDiaryDecisionDraft, Is.False, "A study at the limit is never staged: " + approach);
            }
            director.ConfirmDiaryDecision();
            AssertEquivalent(full, director.Snapshot);

            // The record: counted out of the season's limit, in the season's own words; the rules line names the limit.
            ButtonWithCaption(EpisodeDirector.DiaryRecordTabCaption).onClick.Invoke();
            yield return null; yield return null;
            var row = ActiveRect(EpisodeHud.DiaryRecordRowPrefix + "Study preparation").GetComponentsInChildren<TMPro.TMP_Text>()
                .Select(label => label.text).ToArray();
            Assert.That(row, Does.Contain("1 / 1"));
            Assert.That(row, Does.Contain(StudyWords.UseShort(full)));
            if (!ActiveDiaryText().Contains(StudyWords.RulesLine(full)))
            {
                ButtonWithCaption(EpisodeHud.DiaryRulesCaption).onClick.Invoke();
                yield return null; yield return null;
            }
            Assert.That(ActiveDiaryText(), Does.Contain(StudyWords.RulesLine(full)));
            AssertEquivalent(full, director.Snapshot);
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
