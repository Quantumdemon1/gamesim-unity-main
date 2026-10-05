using System;
using System.Collections;
using System.IO;
using System.Linq;
using Gamesim.Episode;
using Gamesim.Simulation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Gamesim.Tests.PlayMode
{
    public sealed partial class EpisodePlayModeTests
    {
        private const string HearHoHPitchesCaption = "Hear houseguest pitches";
        private static readonly string[] HoHPitchAnswers = { "promise-safety", "hear", "turn-down" };

        private IEnumerator InstallHoHPitches(int size = 8, int cards = 2, bool fresh = true, bool playerHoh = true)
        {
            yield return InstallTalkingHouse(size, false, state =>
            {
                EpisodeEngine.EnableWeek(state);
                EpisodeEngine.EnableLevers(state);
                EpisodeEngine.EnableCommitments(state);
                EpisodeEngine.EnableAgency(state);
                state.economyRulesVersion = fresh ? 1 : 0;
                state.phase = EpisodePhase.Nomination;
                var npcs = state.Active.Where(person => !person.isPlayer).ToArray();
                state.hohId = playerHoh ? state.playerId : npcs[0].id;
                state.nominees.Clear();
                state.houseEvents.Clear();
                state.replyCards.Clear();
                for (int i = 0; i < cards; i++)
                    state.replyCards.Add(new ReplyCardState { id = "hoh-pitch-runtime-" + i,
                        week = state.week, kind = ReplyCards.Pitch, fromId = npcs[i].id });
            });
        }

        private string HoHPitchCaption(string key) => EpisodeHud.ReplyCaption(ReplyCards.Find(ReplyCards.Pitch, key).Label);

        private void AssertHoHPitchCommit(EpisodeState before, string key)
        {
            AssertIntentCommand(before, EpisodeCommandKind.ReplyToHouseguest, before.replyCards[0].id, key);
            var after = director.Snapshot;
            Assert.That(after.phase, Is.EqualTo(EpisodePhase.Nomination));
            Assert.That(after.nominees, Is.EqualTo(before.nominees));
            Assert.That(after.socialActions, Is.EqualTo(before.socialActions));
            Assert.That(after.outOfPhaseSocialActions, Is.EqualTo(before.outOfPhaseSocialActions));
            Assert.That(after.windowActions, Is.EqualTo(before.windowActions), "Hearing a pitch does not consume a conversation seat.");
            var remaining = key == HoHPitches.FeelOutKey ? before.replyCards : before.replyCards.Skip(1).ToList();
            Assert.That(after.replyCards.Select(JsonUtility.ToJson), Is.EqualTo(remaining.Select(JsonUtility.ToJson)),
                "Only the answered card is removed; feeling out retains the exact pending cards.");
        }

        private void AssertHoHPitchDraft(EpisodeHud.Option[] picked, string commitCaption = "Commit nominations")
        {
            Assert.That(ActiveRect(EpisodeHud.NomineeGridName), Is.Not.Null);
            foreach (var person in picked)
            {
                var mark = ButtonWithCaption(person.Label).transform.Find("Picked");
                Assert.That(mark != null && mark.gameObject.activeSelf, Is.True, person.Label + " remains selected.");
            }
            Assert.That(ActiveRect(EpisodeHud.SelectedNomineesName).GetComponent<TMP_Text>().text,
                Is.EqualTo("Selected: " + picked[0].Label + " and " + picked[1].Label));
            Assert.That(ButtonWithCaption(commitCaption).IsInteractable(), Is.True);
        }

        [UnityTest]
        public IEnumerator HoHPitches_StageFitsAllAnswersAndKeyboardAtBothCastAndTextSizes()
        {
            foreach (int size in new[] { 8, 16 })
            {
                yield return InstallHoHPitches(size);
                foreach (bool large in new[] { false, true })
                {
                    yield return ApplyTextSize(large);
                    yield return OpenStation(); yield return null;
                    var before = director.Snapshot;
                    string where = "HoH pitches at " + size + " contestants / large=" + large;
                    AssertNominationScreen(where, EpisodeHud.BackToNomineesCaption);
                    Assert.That(ActiveRect(EpisodeHud.NomineeGridName), Is.Null, "The first pending pitch is the current view, not a buried row.");
                    var panel = ActiveRect("Episode panel");
                    Assert.That(panel.GetComponentsInChildren<TMP_Text>().Any(label =>
                        string.Equals(label.text, "Houseguest pitch", StringComparison.OrdinalIgnoreCase)), Is.True);
                    foreach (string caption in HoHPitchAnswers.Select(HoHPitchCaption).Concat(new[] { EpisodeHud.FeelOutPitchCaption }))
                    {
                        var button = ButtonWithCaption(caption);
                        Assert.That(button.IsInteractable(), Is.True, caption);
                        AssertInside(ScreenRect(panel), (RectTransform)button.transform, where + ": " + caption);
                        foreach (var label in button.GetComponentsInChildren<TMP_Text>())
                        {
                            label.ForceMeshUpdate();
                            Assert.That(label.isTextOverflowing, Is.False, where + ": " + label.text);
                        }
                    }
                    yield return AssertKeyboardRing(where, ModalRoot);
                    AssertIntentDurable(before);
                }
            }
        }

        [UnityTest]
        public IEnumerator HoHPitches_FeelingOutIsFreeDeterministicDurableAndCannotBeRepeatedAfterReload()
        {
            yield return InstallHoHPitches(); yield return OpenStation();
            var before = director.Snapshot;
            var click = ButtonWithCaption(EpisodeHud.FeelOutPitchCaption).onClick;
            click.Invoke();
            AssertHoHPitchCommit(before, HoHPitches.FeelOutKey);
            var after = director.Snapshot;
            Assert.That(after.randomState, Is.EqualTo(before.randomState));
            Assert.That(after.ledger.standings.Count, Is.EqualTo(before.ledger.standings.Count + 1));
            Assert.That(after.ledger.standings.Last().source, Is.EqualTo(ClaimSource.Told));
            Assert.That(after.ledger.standings.Last().fromId, Is.EqualTo(before.replyCards[0].fromId));
            Assert.That(HoHPitches.Assessed(after, after.replyCards[0]), Is.True);
            Assert.That(ButtonWithCaption(EpisodeHud.FeelOutPitchCaption).IsInteractable(), Is.False);
            click.Invoke(); AssertIntentDurable(after);
            director.LoadNow(); HoldTheHouseForTheFixture(); yield return OpenStation();
            Assert.That(ButtonWithCaption(EpisodeHud.FeelOutPitchCaption).IsInteractable(), Is.False);
            ButtonWithCaption(EpisodeHud.FeelOutPitchCaption).onClick.Invoke();
            click.Invoke(); AssertIntentDurable(after);
        }

        [UnityTest]
        public IEnumerator HoHPitches_EachAnswerCommitsItsExactOutcomeAndOnlyItsOwnCard()
        {
            foreach (string key in HoHPitchAnswers)
            {
                yield return InstallHoHPitches(); yield return OpenStation();
                var before = director.Snapshot;
                var click = ButtonWithCaption(HoHPitchCaption(key)).onClick;
                click.Invoke(); AssertHoHPitchCommit(before, key);
                var after = director.Snapshot;
                Assert.That(after.replyCards.Single().id, Is.EqualTo(before.replyCards[1].id));
                if (key == "promise-safety")
                    Assert.That(after.promises.Any(promise => promise.fromId == before.playerId
                        && promise.toId == before.replyCards[0].fromId && promise.kind == PromiseKind.Safety
                        && promise.status == PromiseStatus.Active), Is.True, "The pitch makes an actual safety promise, not a vote promise.");
                else
                    Assert.That(after.promises.Select(JsonUtility.ToJson), Is.EqualTo(before.promises.Select(JsonUtility.ToJson)));
                click.Invoke(); AssertIntentDurable(after);
            }
        }

        [UnityTest]
        public IEnumerator HoHPitches_AssessmentEntitlementBelongsToEachCardNotTheWholeNomination()
        {
            yield return InstallHoHPitches(); yield return OpenStation();
            var first = director.Snapshot;
            ButtonWithCaption(EpisodeHud.FeelOutPitchCaption).onClick.Invoke();
            AssertHoHPitchCommit(first, HoHPitches.FeelOutKey);
            var assessed = director.Snapshot;
            ButtonWithCaption(HoHPitchCaption("hear")).onClick.Invoke();
            AssertHoHPitchCommit(assessed, "hear");
            var second = director.Snapshot;
            Assert.That(HoHPitches.Assessed(second, second.replyCards[0]), Is.False);
            Assert.That(ButtonWithCaption(EpisodeHud.FeelOutPitchCaption).IsInteractable(), Is.True);
            ButtonWithCaption(EpisodeHud.FeelOutPitchCaption).onClick.Invoke();
            AssertHoHPitchCommit(second, HoHPitches.FeelOutKey);
            Assert.That(director.Snapshot.ledger.standings.Last().fromId, Is.EqualTo(second.replyCards[0].fromId));
            Assert.That(director.Snapshot.randomState, Is.EqualTo(second.randomState));
        }

        [UnityTest]
        public IEnumerator HoHPitches_TwoNomineeDraftSurvivesPeekingAssessingAnsweringAndReturning()
        {
            yield return InstallHoHPitches(16); yield return OpenStation();
            var before = director.Snapshot;
            yield return KeyboardSubmit(EpisodeHud.BackToNomineesCaption); yield return null;
            var picks = EpisodeEngine.NominationCandidates(before).Take(2)
                .Select(person => new EpisodeHud.Option(person.id, person.name)).ToArray();
            foreach (var person in picks) ButtonWithCaption(person.Label).onClick.Invoke();
            AssertHoHPitchDraft(picks); AssertIntentDurable(before);
            yield return KeyboardSubmit(HearHoHPitchesCaption); yield return null;
            AssertIntentDurable(before);
            ButtonWithCaption(EpisodeHud.FeelOutPitchCaption).onClick.Invoke();
            AssertHoHPitchCommit(before, HoHPitches.FeelOutKey);
            var assessed = director.Snapshot;
            ButtonWithCaption(HoHPitchCaption("hear")).onClick.Invoke();
            AssertHoHPitchCommit(assessed, "hear");
            var answered = director.Snapshot;
            ButtonWithCaption(EpisodeHud.BackToNomineesCaption).onClick.Invoke(); yield return null;
            AssertHoHPitchDraft(picks); AssertIntentDurable(answered);
            ButtonWithCaption("Commit nominations").onClick.Invoke();
            Assert.That(director.Snapshot.nominees, Is.EqualTo(picks.Select(person => person.Id)),
                "The real nomination reducer receives the retained draft, not a different pair.");
        }

        [UnityTest]
        public IEnumerator HoHPitches_DismissingAndReopeningAtTheSameRevisionRetiresOldControls()
        {
            yield return InstallHoHPitches(); yield return OpenStation();
            var before = director.Snapshot;
            var oldFeel = ButtonWithCaption(EpisodeHud.FeelOutPitchCaption).onClick;
            var oldReply = ButtonWithCaption(HoHPitchCaption("promise-safety")).onClick;
            ButtonWithCaption(EpisodeHud.BackToNomineesCaption).onClick.Invoke(); yield return null;
            oldFeel.Invoke(); oldReply.Invoke(); AssertIntentDurable(before);
            Assert.That(ActiveRect(EpisodeHud.NomineeGridName), Is.Not.Null);
            ButtonWithCaption(HearHoHPitchesCaption).onClick.Invoke(); yield return null;
            oldFeel.Invoke(); oldReply.Invoke(); AssertIntentDurable(before);
            ButtonWithCaption(HoHPitchCaption("hear")).onClick.Invoke(); AssertHoHPitchCommit(before, "hear");
        }

        [UnityTest]
        public IEnumerator HoHPitches_LoadAndSceneReplacementRejectCallbacksFromThePreviousWorld()
        {
            yield return InstallHoHPitches(); yield return OpenStation();
            var before = director.Snapshot;
            var oldFeel = ButtonWithCaption(EpisodeHud.FeelOutPitchCaption).onClick;
            var oldReply = ButtonWithCaption(HoHPitchCaption("hear")).onClick;
            director.LoadNow(); HoldTheHouseForTheFixture(); yield return OpenStation();
            oldFeel.Invoke(); oldReply.Invoke(); AssertIntentDurable(before);
            oldFeel = ButtonWithCaption(EpisodeHud.FeelOutPitchCaption).onClick;
            oldReply = ButtonWithCaption(HoHPitchCaption("hear")).onClick;
            yield return ReloadEpisode(); HoldTheHouseForTheFixture(); yield return OpenStation();
            oldFeel.Invoke(); oldReply.Invoke(); AssertIntentDurable(before);
            ButtonWithCaption(HoHPitchCaption("hear")).onClick.Invoke(); AssertHoHPitchCommit(before, "hear");
        }

        [UnityTest]
        public IEnumerator HoHPitches_AnAssessmentRevisionRetiresAnAnswerFromTheEarlierView()
        {
            yield return InstallHoHPitches(); yield return OpenStation();
            var before = director.Snapshot;
            var stale = ButtonWithCaption(HoHPitchCaption("turn-down")).onClick;
            ButtonWithCaption(EpisodeHud.FeelOutPitchCaption).onClick.Invoke();
            AssertHoHPitchCommit(before, HoHPitches.FeelOutKey);
            var assessed = director.Snapshot;
            stale.Invoke(); AssertIntentDurable(assessed);
            ButtonWithCaption(HoHPitchCaption("turn-down")).onClick.Invoke(); AssertHoHPitchCommit(assessed, "turn-down");
        }

        [UnityTest]
        public IEnumerator HoHPitches_FailedAssessmentWriteKeepsExactBytesAndRequiresANewDeliberatePress()
        {
            yield return InstallHoHPitches(); yield return OpenStation();
            var before = director.Snapshot; byte[] bytes = File.ReadAllBytes(director.SavePath);
            var old = ButtonWithCaption(EpisodeHud.FeelOutPitchCaption).onClick;
            using (new FileStream(director.SavePath, FileMode.Open, FileAccess.Read, FileShare.None)) old.Invoke();
            AssertIntentDurable(before); Assert.That(File.ReadAllBytes(director.SavePath), Is.EqualTo(bytes));
            old.Invoke(); AssertIntentDurable(before);
            Assert.That(ButtonWithCaption(EpisodeHud.FeelOutPitchCaption).IsInteractable(), Is.True);
            ButtonWithCaption(EpisodeHud.FeelOutPitchCaption).onClick.Invoke(); AssertHoHPitchCommit(before, HoHPitches.FeelOutKey);
        }

        [UnityTest]
        public IEnumerator HoHPitches_FailedSafetyAnswerWriteKeepsCardAndConsumesTheOldViewAuthority()
        {
            yield return InstallHoHPitches(); yield return OpenStation();
            var before = director.Snapshot; byte[] bytes = File.ReadAllBytes(director.SavePath);
            var old = ButtonWithCaption(HoHPitchCaption("promise-safety")).onClick;
            using (new FileStream(director.SavePath, FileMode.Open, FileAccess.Read, FileShare.None)) old.Invoke();
            AssertIntentDurable(before); Assert.That(File.ReadAllBytes(director.SavePath), Is.EqualTo(bytes));
            old.Invoke(); AssertIntentDurable(before);
            ButtonWithCaption(HoHPitchCaption("promise-safety")).onClick.Invoke(); AssertHoHPitchCommit(before, "promise-safety");
        }

        [UnityTest]
        public IEnumerator HoHPitches_DiaryUsesTheSameFreeAssessmentAndGuardedAnswers()
        {
            yield return InstallHoHPitches();
            yield return OpenDiaryFixturePanel();
            var before = director.Snapshot;
            Assert.That(director.IsDiaryOpen, Is.True);
            var old = ButtonWithCaption(HoHPitchCaption("hear")).onClick;
            ButtonWithCaption(EpisodeHud.FeelOutPitchCaption).onClick.Invoke(); AssertHoHPitchCommit(before, HoHPitches.FeelOutKey);
            var assessed = director.Snapshot;
            Assert.That(ButtonWithCaption(EpisodeHud.FeelOutPitchCaption).IsInteractable(), Is.False);
            old.Invoke(); AssertIntentDurable(assessed);
            var closed = ButtonWithCaption(HoHPitchCaption("hear")).onClick;
            director.ClosePanels(); closed.Invoke(); AssertIntentDurable(assessed);
            yield return OpenDiaryFixturePanel();
            Assert.That(ButtonWithCaption(EpisodeHud.FeelOutPitchCaption).IsInteractable(), Is.False);
            closed.Invoke(); AssertIntentDurable(assessed);
            ButtonWithCaption(HoHPitchCaption("hear")).onClick.Invoke(); AssertHoHPitchCommit(assessed, "hear");
            Assert.That(director.IsDiaryOpen, Is.True);
        }

        [UnityTest]
        public IEnumerator HoHPitches_DiaryRetainsBothNomineeDraftsAcrossHearingAssessmentAnswerAndReturn()
        {
            yield return InstallHoHPitches(16); yield return OpenDiaryFixturePanel();
            var before = director.Snapshot;
            ButtonWithCaption(EpisodeHud.BackToNomineesCaption).onClick.Invoke(); yield return null;
            var picks = EpisodeEngine.NominationCandidates(before).Take(2)
                .Select(person => new EpisodeHud.Option(person.id, person.name)).ToArray();
            foreach (var person in picks) ButtonWithCaption(person.Label).onClick.Invoke();
            AssertHoHPitchDraft(picks, EpisodeHud.DiaryReviewNominationsCaption); AssertIntentDurable(before);
            ButtonWithCaption(HearHoHPitchesCaption).onClick.Invoke(); yield return null;
            AssertIntentDurable(before);
            ButtonWithCaption(EpisodeHud.FeelOutPitchCaption).onClick.Invoke();
            AssertHoHPitchCommit(before, HoHPitches.FeelOutKey);
            var assessed = director.Snapshot;
            ButtonWithCaption(HoHPitchCaption("hear")).onClick.Invoke(); AssertHoHPitchCommit(assessed, "hear");
            var answered = director.Snapshot;
            ButtonWithCaption(EpisodeHud.BackToNomineesCaption).onClick.Invoke(); yield return null;
            AssertHoHPitchDraft(picks, EpisodeHud.DiaryReviewNominationsCaption); AssertIntentDurable(answered);
            ButtonWithCaption(EpisodeHud.DiaryReviewNominationsCaption).onClick.Invoke(); yield return null;
            Assert.That(director.HasDiaryDecisionDraft, Is.True);
            Assert.That(ActiveDiaryText(), Does.Contain("Nominate " + picks[0].Label + " and " + picks[1].Label + "."));
            Assert.That(ActiveDiaryText(), Does.Contain("1 unanswered pitch expires when you confirm these nominations."));
            AssertIntentDurable(answered);
            ButtonWithCaption(EpisodeHud.DiaryConfirmCaption).onClick.Invoke();
            AssertIntentCommand(answered, EpisodeCommandKind.Nominate, picks[0].Id, picks[1].Id);
            Assert.That(director.Snapshot.nominees, Is.EqualTo(picks.Select(person => person.Id)));
            Assert.That(director.Snapshot.replyCards.Any(card => card.kind == ReplyCards.Pitch), Is.False);
            Assert.That(director.HasDiaryDecisionDraft, Is.False);
            Assert.That(director.IsDiaryOpen, Is.True);
        }

        [UnityTest]
        public IEnumerator HoHPitches_DiaryExplainsImmediatePitchSavesButRequiresConfirmationForNominees()
        {
            yield return InstallHoHPitches(); yield return OpenDiaryFixturePanel();
            var before = director.Snapshot;
            const string immediate = "Pitch answers and assessments save immediately.";
            Assert.That(director.StatusMessage, Is.EqualTo(EpisodeDirector.DiaryPitchInsideMessage));
            Assert.That(ActiveDiaryText(), Does.Contain(immediate));
            Assert.That(ActiveDiaryText(), Does.Not.Contain("Choose an option to review it before confirming."));
            Assert.That(ButtonWithCaptionOrNull(EpisodeHud.DiaryConfirmCaption), Is.Null);
            ButtonWithCaption(EpisodeDirector.DiaryRecordTabCaption).onClick.Invoke(); yield return null;
            Assert.That(ActiveDiaryText(), Does.Contain(immediate));
            Assert.That(ActiveDiaryText(), Does.Not.Contain("Nothing is committed until you confirm it."));
            AssertIntentDurable(before);
            ButtonWithCaption(EpisodeDirector.DiaryPendingTabCaption).onClick.Invoke(); yield return null;
            ButtonWithCaption(EpisodeHud.FeelOutPitchCaption).onClick.Invoke();
            AssertHoHPitchCommit(before, HoHPitches.FeelOutKey);
            Assert.That(director.HasDiaryDecisionDraft, Is.False, "Assessing saves now, without a hidden confirmation step.");
            var assessed = director.Snapshot;
            ButtonWithCaption(HoHPitchCaption("hear")).onClick.Invoke(); AssertHoHPitchCommit(assessed, "hear");
            Assert.That(director.HasDiaryDecisionDraft, Is.False, "An answer saves now as the room explains.");
            var answered = director.Snapshot;
            byte[] bytes = File.ReadAllBytes(director.SavePath);
            ButtonWithCaption(EpisodeHud.BackToNomineesCaption).onClick.Invoke(); yield return null;
            Assert.That(ActiveDiaryText(), Does.Contain("Choose an option to review it before confirming."));
            Assert.That(ActiveDiaryText(), Does.Not.Contain(immediate));
            foreach (var person in EpisodeEngine.NominationCandidates(answered).Take(2))
                ButtonWithCaption(person.name).onClick.Invoke();
            ButtonWithCaption(EpisodeHud.DiaryReviewNominationsCaption).onClick.Invoke(); yield return null;
            Assert.That(director.HasDiaryDecisionDraft, Is.True);
            Assert.That(ActiveDiaryText(), Does.Contain("NOT YET SAVED"));
            Assert.That(ActiveDiaryText(), Does.Contain(EpisodeDirector.DraftBarLine));
            Assert.That(ButtonWithCaption(EpisodeHud.DiaryConfirmCaption).IsInteractable(), Is.True);
            AssertIntentDurable(answered);
            Assert.That(File.ReadAllBytes(director.SavePath), Is.EqualTo(bytes));
            ButtonWithCaption(EpisodeHud.DiaryCancelCaption).onClick.Invoke();
            Assert.That(director.HasDiaryDecisionDraft, Is.False);
            AssertIntentDurable(answered);
            Assert.That(File.ReadAllBytes(director.SavePath), Is.EqualTo(bytes));
        }

        [UnityTest]
        public IEnumerator HoHPitches_StaleNavigationCannotChangeTheViewAtTheSameRevisionOnEitherRoute()
        {
            foreach (bool privateRoom in new[] { false, true })
            {
                yield return InstallHoHPitches();
                if (privateRoom) yield return OpenDiaryFixturePanel(); else yield return OpenStation();
                var before = director.Snapshot;
                if (privateRoom)
                {
                    var unconsumedBack = ButtonWithCaption(EpisodeHud.BackToNomineesCaption).onClick;
                    ButtonWithCaption(EpisodeDirector.DiaryRecordTabCaption).onClick.Invoke(); yield return null;
                    ButtonWithCaption(EpisodeDirector.DiaryPendingTabCaption).onClick.Invoke(); yield return null;
                    unconsumedBack.Invoke();
                    Assert.That(ButtonWithCaptionOrNull(EpisodeHud.FeelOutPitchCaption), Is.Not.Null,
                        "Rendering a new view retires even a navigation callback never pressed before.");
                    AssertIntentDurable(before);
                }
                var oldBack = ButtonWithCaption(EpisodeHud.BackToNomineesCaption).onClick;
                oldBack.Invoke(); yield return null;
                Assert.That(ActiveRect(EpisodeHud.NomineeGridName), Is.Not.Null);
                var oldHear = ButtonWithCaption(HearHoHPitchesCaption).onClick;
                oldHear.Invoke(); yield return null;
                Assert.That(ButtonWithCaptionOrNull(EpisodeHud.FeelOutPitchCaption), Is.Not.Null);
                oldBack.Invoke(); yield return null;
                Assert.That(ButtonWithCaptionOrNull(EpisodeHud.FeelOutPitchCaption), Is.Not.Null,
                    "An old Back must not dismiss a newly opened pitch at the same revision.");
                Assert.That(ActiveRect(EpisodeHud.NomineeGridName), Is.Null);
                AssertIntentDurable(before);
                ButtonWithCaption(EpisodeHud.BackToNomineesCaption).onClick.Invoke(); yield return null;
                oldHear.Invoke(); yield return null;
                Assert.That(ActiveRect(EpisodeHud.NomineeGridName), Is.Not.Null,
                    "An old Hear must not reopen a newly dismissed pitch at the same revision.");
                Assert.That(ButtonWithCaptionOrNull(EpisodeHud.FeelOutPitchCaption), Is.Null);
                AssertIntentDurable(before);
                ButtonWithCaption(HearHoHPitchesCaption).onClick.Invoke(); yield return null;
                Assert.That(ButtonWithCaptionOrNull(EpisodeHud.FeelOutPitchCaption), Is.Not.Null,
                    "The current view still grants a fresh deliberate navigation action.");
                AssertIntentDurable(before);
            }
        }

        [UnityTest]
        public IEnumerator HoHPitches_LegacyAndNonHohNominationHaveNoPitchControls()
        {
            foreach (bool legacy in new[] { true, false })
            {
                yield return InstallHoHPitches(cards: 0, fresh: !legacy, playerHoh: legacy);
                yield return OpenStation();
                var before = director.Snapshot;
                Assert.That(ButtonWithCaptionOrNull(EpisodeHud.FeelOutPitchCaption), Is.Null);
                Assert.That(ButtonWithCaptionOrNull(HearHoHPitchesCaption), Is.Null);
                foreach (string key in HoHPitchAnswers) Assert.That(ButtonWithCaptionOrNull(HoHPitchCaption(key)), Is.Null);
                AssertIntentDurable(before);
            }
        }
    }
}
