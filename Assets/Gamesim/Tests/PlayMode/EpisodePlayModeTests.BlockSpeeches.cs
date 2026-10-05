using System;
using System.Collections;
using System.IO;
using System.Linq;
using Gamesim.Episode;
using Gamesim.Persistence;
using Gamesim.Simulation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Gamesim.Tests.PlayMode
{
    public sealed partial class EpisodePlayModeTests
    {
        private IEnumerator InstallBlockSpeechHouse(int size = 8, bool fresh = true, bool playerNominee = true,
            Action<EpisodeState> shape = null)
        {
            yield return InstallTalkingHouse(size, false, state =>
            {
                AtEviction(state);
                EpisodeEngine.EnableWeek(state);
                EpisodeEngine.EnableRead(state);
                EpisodeEngine.EnableLevers(state);
                EpisodeEngine.EnableAgency(state);
                EpisodeEngine.EnableCommitments(state);
                state.economyRulesVersion = fresh ? 1 : 0;
                state.evictionStage = EvictionStage.Speeches;
                if (playerNominee) state.nominees[0] = state.playerId;
                state.votes.Clear(); state.evictionSpeeches.Clear();
                state.replyCards.Clear(); state.houseEvents.Clear(); state.pendingDiary = null;
                shape?.Invoke(state);
            });
        }

        private IEnumerator OpenBlockSpeech(bool privateRoom)
        {
            if (privateRoom) yield return OpenDiaryFixturePanel(); else yield return OpenStation();
            yield return null;
        }

        private TMP_InputField BlockSpeechInput() => director.GetComponentsInChildren<TMP_InputField>()
            .Single(input => input.gameObject.activeInHierarchy && input.name == EpisodeHud.BlockSpeechDraftName);

        private void AssertBlockSpeechCommit(EpisodeState before, string text, string approach)
        {
            var after = director.Snapshot;
            Assert.That(after.acceptedCommandIds.Count, Is.EqualTo(before.acceptedCommandIds.Count + 1));
            var command = new EpisodeCommand { id = after.acceptedCommandIds.Last(), actorId = before.playerId,
                expectedRevision = before.revision, expectedPhase = before.phase,
                kind = EpisodeCommandKind.SubmitEvictionSpeech, text = text,
                secondTargetId = BlockSpeeches.RulesOn(before) ? approach : null };
            var replay = new EpisodeEngine(before).Apply(command);
            Assert.That(replay.accepted, Is.True, replay.reason);
            AssertIntentDurable(replay.state);
            var speech = after.evictionSpeeches.Single(item => item.speakerId == after.playerId);
            Assert.That(speech.text, Is.EqualTo(text.Trim()));
            Assert.That(BlockSpeeches.Approach(after, speech), Is.EqualTo(BlockSpeeches.RulesOn(before) ? approach : null));
            Assert.That(after.phase, Is.EqualTo(EpisodePhase.Eviction));
            Assert.That(after.evictionStage, Is.EqualTo(EvictionStage.Speeches));
            Assert.That(after.randomState, Is.EqualTo(before.randomState));
            Assert.That(after.votes.Select(JsonUtility.ToJson), Is.EqualTo(before.votes.Select(JsonUtility.ToJson)));
            Assert.That(after.relationships.Select(JsonUtility.ToJson), Is.EqualTo(before.relationships.Select(JsonUtility.ToJson)));
            Assert.That(after.promises.Select(JsonUtility.ToJson), Is.EqualTo(before.promises.Select(JsonUtility.ToJson)));
            Assert.That(after.deals.Select(JsonUtility.ToJson), Is.EqualTo(before.deals.Select(JsonUtility.ToJson)),
                "An offer in a speech is rhetoric, not an automatically negotiated deal.");
            Assert.That(after.windowActions, Is.EqualTo(before.windowActions));
        }

        [UnityTest]
        public IEnumerator BlockSpeeches_ApproachesAndEditorAreReachableOnBothSurfacesAtBothTextSizes()
        {
            foreach (bool privateRoom in new[] { false, true })
            {
                yield return InstallBlockSpeechHouse(16);
                foreach (bool large in new[] { false, true })
                {
                    yield return ApplyTextSize(large); yield return OpenBlockSpeech(privateRoom);
                    var before = director.Snapshot;
                    string where = "Block speech diary=" + privateRoom + " large=" + large;
                    var row = ActiveRect(EpisodeHud.BlockSpeechApproachesName);
                    Assert.That(row, Is.Not.Null, where);
                    Assert.That(row.GetComponentsInChildren<Button>().Length, Is.EqualTo(4));
                    foreach (string key in BlockSpeeches.Approaches)
                    {
                        var button = ButtonWithCaption(BlockSpeeches.Label(key));
                        Assert.That(button.IsInteractable(), Is.True, where + ": " + key);
                        var bounds = ScreenRect((RectTransform)button.transform);
                        var column = ScreenRect(row);
                        Assert.That(bounds.xMin, Is.GreaterThanOrEqualTo(column.xMin - 1f), where);
                        Assert.That(bounds.xMax, Is.LessThanOrEqualTo(column.xMax + 1f), where);
                        foreach (var label in button.GetComponentsInChildren<TMP_Text>())
                        { label.ForceMeshUpdate(); Assert.That(label.isTextOverflowing, Is.False, where + ": " + label.text); }
                    }
                    var input = BlockSpeechInput();
                    Assert.That(input.characterLimit, Is.EqualTo(2000));
                    Assert.That(input.lineType, Is.EqualTo(TMP_InputField.LineType.MultiLineNewline));
                    Assert.That(input.textViewport.GetComponent<RectMask2D>(), Is.Not.Null);
                    Assert.That(input.textComponent.richText, Is.False);
                    Assert.That(ActiveDiaryText(), Does.Contain(EpisodeDirector.BlockSpeechInfluenceLine));
                    Assert.That(ButtonWithCaption(EpisodeHud.EvictionSpeechCaption).IsInteractable(), Is.True);
                    Assert.That(ButtonWithCaption(EpisodeHud.EvictionSpeechSkipCaption).IsInteractable(), Is.True);
                    yield return AssertKeyboardRing(where, ModalRoot);
                    AssertIntentDurable(before);
                }
            }
        }

        [UnityTest]
        public IEnumerator BlockSpeeches_EverySelectedApproachCommitsExactTextAndReceiptThroughBothSurfaces()
        {
            foreach (bool privateRoom in new[] { false, true })
                foreach (string key in BlockSpeeches.Approaches)
                {
                    yield return InstallBlockSpeechHouse(); yield return OpenBlockSpeech(privateRoom);
                    var before = director.Snapshot;
                    const string words = "My words stay mine.\nNo secret keyword decides my approach.";
                    BlockSpeechInput().text = words;
                    ButtonWithCaption(BlockSpeeches.Label(key)).onClick.Invoke(); yield return null;
                    Assert.That(BlockSpeechInput().text, Is.EqualTo(words));
                    Assert.That(ActiveDiaryText(), Does.Contain("Selected approach: " + BlockSpeeches.Label(key)));
                    Assert.That(ActiveDiaryText(), Does.Contain(BlockSpeeches.Description(key)));
                    AssertIntentDurable(before);
                    ButtonWithCaption(EpisodeHud.EvictionSpeechCaption).onClick.Invoke();
                    AssertBlockSpeechCommit(before, words, key);
                    Assert.That(director.HasDiaryDecisionDraft, Is.False);
                    Assert.That(ActiveDiaryText(), Does.Contain(words));
                    Assert.That(ActiveDiaryText(), Does.Contain("Approach: " + BlockSpeeches.Label(key)));
                }
        }

        [UnityTest]
        public IEnumerator BlockSpeeches_TypedMultilineDraftSurvivesRebuildTabAndEscapeOnBothSurfaces()
        {
            foreach (bool privateRoom in new[] { false, true })
            {
                yield return InstallBlockSpeechHouse(); yield return OpenBlockSpeech(privateRoom);
                ButtonWithCaption(BlockSpeeches.Label(LobbyApproach.Strategic)).onClick.Invoke(); yield return null;
                var before = director.Snapshot;
                var input = BlockSpeechInput(); input.Select(); input.ActivateInputField(); yield return null;
                const string words = "I still have a game to play.\nLet me show you.";
                foreach (char character in words)
                    input.ProcessEvent(new Event { type = EventType.KeyDown, character = character,
                        keyCode = character == '\n' ? KeyCode.Return : KeyCode.None });
                input.ForceLabelUpdate(); Assert.That(input.text, Is.EqualTo(words));
                director.SaveNow(); yield return null; yield return null;
                Assert.That(BlockSpeechInput().text, Is.EqualTo(words));
                Assert.That(ActiveDiaryText(), Does.Contain("Selected approach: " + BlockSpeeches.Label(LobbyApproach.Strategic)));
                input = BlockSpeechInput(); input.Select(); input.ActivateInputField(); yield return null;
                yield return PressDiaryKey(Key.Tab);
                Assert.That(EventSystem.current.currentSelectedGameObject,
                    Is.EqualTo(ButtonWithCaption(EpisodeHud.EvictionSpeechCaption).gameObject));
                Assert.That(BlockSpeechInput().text, Is.EqualTo(words));
                input = BlockSpeechInput(); input.Select(); input.ActivateInputField(); yield return null;
                yield return PressDiaryKey(Key.Escape);
                Assert.That(director.IsPanelOpen, Is.False); AssertIntentDurable(before);
                yield return OpenBlockSpeech(privateRoom);
                Assert.That(BlockSpeechInput().text, Is.EqualTo(words));
                ButtonWithCaption(EpisodeHud.EvictionSpeechCaption).onClick.Invoke();
                AssertBlockSpeechCommit(before, words, LobbyApproach.Strategic);
            }
        }

        [UnityTest]
        public IEnumerator BlockSpeeches_SameSpeechDraftAndApproachFollowThePlayerBetweenStationAndDiary()
        {
            yield return InstallBlockSpeechHouse(); yield return OpenStation();
            var before = director.Snapshot;
            const string words = "This is one draft, wherever I prepare it.";
            BlockSpeechInput().text = words;
            ButtonWithCaption(BlockSpeeches.Label(LobbyApproach.Deal)).onClick.Invoke();
            director.ClosePanels(); yield return OpenDiaryFixturePanel();
            Assert.That(BlockSpeechInput().text, Is.EqualTo(words));
            Assert.That(ActiveDiaryText(), Does.Contain("Selected approach: " + BlockSpeeches.Label(LobbyApproach.Deal)));
            director.ClosePanels(); yield return WaitForDiaryExit(); yield return OpenStation();
            Assert.That(BlockSpeechInput().text, Is.EqualTo(words));
            Assert.That(ActiveDiaryText(), Does.Contain(BlockSpeeches.Description(LobbyApproach.Deal)));
            AssertIntentDurable(before);
            ButtonWithCaption(EpisodeHud.EvictionSpeechCaption).onClick.Invoke();
            AssertBlockSpeechCommit(before, words, LobbyApproach.Deal);
        }

        [UnityTest]
        public IEnumerator BlockSpeeches_FullTwoThousandCharacterLiteralTextAndApproachReloadOnBothSurfaces()
        {
            foreach (bool privateRoom in new[] { false, true })
            {
                yield return InstallBlockSpeechHouse(); yield return OpenBlockSpeech(privateRoom);
                var before = director.Snapshot;
                string words = "<b>Not markup</b>\n";
                words += new string('x', 2000 - words.Length);
                BlockSpeechInput().text = words;
                Assert.That(BlockSpeechInput().text.Length, Is.EqualTo(2000));
                ButtonWithCaption(BlockSpeeches.Label(LobbyApproach.Pressure)).onClick.Invoke();
                ButtonWithCaption(EpisodeHud.EvictionSpeechCaption).onClick.Invoke();
                AssertBlockSpeechCommit(before, words, LobbyApproach.Pressure);
                var committed = director.Snapshot;
                yield return ReloadEpisode(); HoldTheHouseForTheFixture(); yield return OpenBlockSpeech(privateRoom);
                AssertIntentDurable(committed);
                var label = director.GetComponentsInChildren<TMP_Text>()
                    .Single(text => text.gameObject.activeInHierarchy && text.text == words);
                Assert.That(label.richText, Is.False);
                Assert.That(ActiveDiaryText(), Does.Contain("Approach: " + BlockSpeeches.Label(LobbyApproach.Pressure)));
                Assert.That(ButtonWithCaptionOrNull(EpisodeHud.EvictionSpeechCaption), Is.Null);
            }
        }

        [UnityTest]
        public IEnumerator BlockSpeeches_SayNothingRecordsQuietInsteadOfTheDraftAndNeverChoosesABallot()
        {
            foreach (bool privateRoom in new[] { false, true })
            {
                yield return InstallBlockSpeechHouse(); yield return OpenBlockSpeech(privateRoom);
                var before = director.Snapshot;
                BlockSpeechInput().text = "This private draft was not delivered.";
                ButtonWithCaption(BlockSpeeches.Label(LobbyApproach.Pressure)).onClick.Invoke();
                var skip = ButtonWithCaption(EpisodeHud.EvictionSpeechSkipCaption).onClick;
                skip.Invoke(); AssertBlockSpeechCommit(before, "", BlockSpeeches.Quiet);
                var after = director.Snapshot;
                Assert.That(ActiveDiaryText(), Does.Contain("No speech was given."));
                Assert.That(ActiveDiaryText(), Does.Not.Contain("This private draft was not delivered."));
                Assert.That(after.votes, Is.Empty);
                skip.Invoke(); AssertIntentDurable(after);
            }
        }

        [UnityTest]
        public IEnumerator BlockSpeeches_DismissedViewCannotSubmitSkipOrChangeTheReopenedDraftAtTheSameRevision()
        {
            foreach (bool privateRoom in new[] { false, true })
            {
                yield return InstallBlockSpeechHouse(); yield return OpenBlockSpeech(privateRoom);
                var before = director.Snapshot;
                BlockSpeechInput().text = "Retain my actual draft.";
                var deliver = ButtonWithCaption(EpisodeHud.EvictionSpeechCaption).onClick;
                var skip = ButtonWithCaption(EpisodeHud.EvictionSpeechSkipCaption).onClick;
                var approach = ButtonWithCaption(BlockSpeeches.Label(LobbyApproach.Pressure)).onClick;
                var edit = BlockSpeechInput().onValueChanged;
                director.ClosePanels(); yield return OpenBlockSpeech(privateRoom);
                deliver.Invoke(); skip.Invoke(); approach.Invoke(); edit.Invoke("A detached field's text.");
                AssertIntentDurable(before);
                Assert.That(BlockSpeechInput().text, Is.EqualTo("Retain my actual draft."));
                Assert.That(ActiveDiaryText(), Does.Contain("Selected approach: " + BlockSpeeches.Label(LobbyApproach.Emotional)));
                director.SaveNow(); yield return null;
                Assert.That(BlockSpeechInput().text, Is.EqualTo("Retain my actual draft."));
                ButtonWithCaption(EpisodeHud.EvictionSpeechCaption).onClick.Invoke();
                AssertBlockSpeechCommit(before, "Retain my actual draft.", LobbyApproach.Emotional);
            }
        }

        [UnityTest]
        public IEnumerator BlockSpeeches_LoadAndSceneReplacementRetireOldDeliveryAndSkipAuthority()
        {
            foreach (bool privateRoom in new[] { false, true })
            {
                yield return InstallBlockSpeechHouse(); yield return OpenBlockSpeech(privateRoom);
                var before = director.Snapshot;
                var deliver = ButtonWithCaption(EpisodeHud.EvictionSpeechCaption).onClick;
                var skip = ButtonWithCaption(EpisodeHud.EvictionSpeechSkipCaption).onClick;
                director.LoadNow(); HoldTheHouseForTheFixture(); yield return OpenBlockSpeech(privateRoom);
                deliver.Invoke(); skip.Invoke(); AssertIntentDurable(before);
                deliver = ButtonWithCaption(EpisodeHud.EvictionSpeechCaption).onClick;
                skip = ButtonWithCaption(EpisodeHud.EvictionSpeechSkipCaption).onClick;
                yield return ReloadEpisode(); HoldTheHouseForTheFixture(); yield return OpenBlockSpeech(privateRoom);
                deliver.Invoke(); skip.Invoke(); AssertIntentDurable(before);
                BlockSpeechInput().text = "Only the current world delivers this.";
                ButtonWithCaption(EpisodeHud.EvictionSpeechCaption).onClick.Invoke();
                AssertBlockSpeechCommit(before, "Only the current world delivers this.", LobbyApproach.Emotional);
            }
        }

        [UnityTest]
        public IEnumerator BlockSpeeches_ChangingApproachRetiresPreviousDeliveryAndInputWithoutSpendingARevision()
        {
            foreach (bool privateRoom in new[] { false, true })
            {
                yield return InstallBlockSpeechHouse(); yield return OpenBlockSpeech(privateRoom);
                var before = director.Snapshot;
                BlockSpeechInput().text = "The draft is not a hidden approach.";
                var old = ButtonWithCaption(EpisodeHud.EvictionSpeechCaption).onClick;
                var edit = BlockSpeechInput().onValueChanged;
                ButtonWithCaption(BlockSpeeches.Label(LobbyApproach.Strategic)).onClick.Invoke(); yield return null;
                old.Invoke(); edit.Invoke("Wrong replacement."); AssertIntentDurable(before);
                Assert.That(BlockSpeechInput().text, Is.EqualTo("The draft is not a hidden approach."));
                var fresh = ButtonWithCaption(EpisodeHud.EvictionSpeechCaption).onClick;
                fresh.Invoke(); AssertBlockSpeechCommit(before, "The draft is not a hidden approach.", LobbyApproach.Strategic);
                var after = director.Snapshot;
                fresh.Invoke(); old.Invoke(); AssertIntentDurable(after);
            }
        }

        [UnityTest]
        public IEnumerator BlockSpeeches_FailedDeliveryKeepsExactSaveAndDraftAndNeedsAFreshActivation()
        {
            foreach (bool privateRoom in new[] { false, true })
            {
                yield return InstallBlockSpeechHouse(); yield return OpenBlockSpeech(privateRoom);
                var before = director.Snapshot; byte[] bytes = File.ReadAllBytes(director.SavePath);
                const string words = "No delivery until the save is durable.";
                BlockSpeechInput().text = words;
                ButtonWithCaption(BlockSpeeches.Label(LobbyApproach.Deal)).onClick.Invoke();
                var old = ButtonWithCaption(EpisodeHud.EvictionSpeechCaption).onClick;
                using (new FileStream(director.SavePath, FileMode.Open, FileAccess.Read, FileShare.None)) old.Invoke();
                AssertIntentDurable(before); Assert.That(File.ReadAllBytes(director.SavePath), Is.EqualTo(bytes));
                old.Invoke(); AssertIntentDurable(before);
                Assert.That(BlockSpeechInput().text, Is.EqualTo(words));
                Assert.That(ActiveDiaryText(), Does.Contain("Selected approach: " + BlockSpeeches.Label(LobbyApproach.Deal)));
                ButtonWithCaption(EpisodeHud.EvictionSpeechCaption).onClick.Invoke();
                AssertBlockSpeechCommit(before, words, LobbyApproach.Deal);
            }
        }

        [UnityTest]
        public IEnumerator BlockSpeeches_FailedSkipDoesNotClearTheDraftOrGrantItsOldCallbackAnotherAttempt()
        {
            foreach (bool privateRoom in new[] { false, true })
            {
                yield return InstallBlockSpeechHouse(); yield return OpenBlockSpeech(privateRoom);
                var before = director.Snapshot; byte[] bytes = File.ReadAllBytes(director.SavePath);
                BlockSpeechInput().text = "Keep this until I make a durable choice.";
                var old = ButtonWithCaption(EpisodeHud.EvictionSpeechSkipCaption).onClick;
                using (new FileStream(director.SavePath, FileMode.Open, FileAccess.Read, FileShare.None)) old.Invoke();
                AssertIntentDurable(before); Assert.That(File.ReadAllBytes(director.SavePath), Is.EqualTo(bytes));
                old.Invoke(); AssertIntentDurable(before);
                Assert.That(BlockSpeechInput().text, Is.EqualTo("Keep this until I make a durable choice."));
                ButtonWithCaption(EpisodeHud.EvictionSpeechSkipCaption).onClick.Invoke();
                AssertBlockSpeechCommit(before, "", BlockSpeeches.Quiet);
            }
        }

        [UnityTest]
        public IEnumerator BlockSpeeches_NewWeekAndSessionCannotReuseThePreviousDraftOrApproach()
        {
            yield return InstallBlockSpeechHouse(); yield return OpenStation();
            foreach (bool nextSession in new[] { false, true })
            {
                BlockSpeechInput().text = "This belongs to the old speech.";
                ButtonWithCaption(BlockSpeeches.Label(LobbyApproach.Pressure)).onClick.Invoke();
                var old = ButtonWithCaption(EpisodeHud.EvictionSpeechCaption).onClick;
                var next = director.Snapshot;
                if (nextSession) next.sessionId = Guid.NewGuid().ToString("N"); else next.week++;
                Assert.That(EpisodeValidation.TryValidate(next, out string error), Is.True, error);
                new EpisodeSaveStore(director.SavePath).Save(next);
                director.LoadNow(); HoldTheHouseForTheFixture(); yield return OpenStation();
                Assert.That(BlockSpeechInput().text, Is.Empty);
                Assert.That(ActiveDiaryText(), Does.Contain("Selected approach: " + BlockSpeeches.Label(LobbyApproach.Emotional)));
                old.Invoke(); AssertIntentDurable(next);
            }
        }

        [UnityTest]
        public IEnumerator BlockSpeeches_DiaryCopyNamesImmediateBroadcastAndDetachedPlayerCannotDeliver()
        {
            yield return InstallBlockSpeechHouse(); yield return OpenDiaryFixturePanel();
            var before = director.Snapshot;
            Assert.That(director.StatusMessage, Is.EqualTo(EpisodeDirector.DiaryBlockSpeechInsideMessage));
            Assert.That(ActiveDiaryText(), Does.Contain(EpisodeDirector.BlockSpeechSaveLine));
            Assert.That(ActiveDiaryText(), Does.Not.Contain("Choose an option to review it before confirming."));
            ButtonWithCaption(EpisodeDirector.DiaryRecordTabCaption).onClick.Invoke(); yield return null;
            Assert.That(ActiveDiaryText(), Does.Contain(EpisodeDirector.BlockSpeechSaveLine));
            Assert.That(ActiveDiaryText(), Does.Not.Contain("Nothing is committed until you confirm it."));
            ButtonWithCaption(EpisodeDirector.DiaryPendingTabCaption).onClick.Invoke(); yield return null;
            Assert.That(director.HasDiaryDecisionDraft, Is.False);
            BlockSpeechInput().text = "A room I have left cannot broadcast for me.";
            var deliver = ButtonWithCaption(EpisodeHud.EvictionSpeechCaption).onClick;
            WarpPlayer(director.StationPosition); deliver.Invoke();
            AssertIntentDurable(before);
            director.ClosePanels();
            Assert.That(director.StatusMessage, Is.Not.EqualTo(EpisodeDirector.DiaryBlockSpeechInsideMessage));
        }

        [UnityTest]
        public IEnumerator BlockSpeeches_LegacyUsesOldSpeechRulesAndRecordedProseDoesNotInventAnApproach()
        {
            yield return InstallBlockSpeechHouse(fresh: false); yield return OpenStation();
            var before = director.Snapshot;
            Assert.That(ActiveRect(EpisodeHud.BlockSpeechApproachesName), Is.Null);
            Assert.That(ActiveDiaryText(), Does.Not.Contain(EpisodeDirector.BlockSpeechInfluenceLine));
            BlockSpeechInput().text = "A legacy speech stays a legacy speech.";
            ButtonWithCaption(EpisodeHud.EvictionSpeechCaption).onClick.Invoke();
            AssertBlockSpeechCommit(before, "A legacy speech stays a legacy speech.", LobbyApproach.Emotional);
            Assert.That(director.Snapshot.events.Any(entry => BlockSpeeches.IsReceiptKind(entry.kind)), Is.False);
            yield return InstallBlockSpeechHouse(shape: state => state.evictionSpeeches.Add(new EvictionSpeechState
            {
                speakerId = state.playerId, week = state.week, isPlayerAuthored = true, text = "Earlier recorded words without an approach.",
            }));
            yield return OpenStation();
            var recorded = director.Snapshot;
            Assert.That(ActiveDiaryText(), Does.Contain("Earlier recorded words without an approach."));
            Assert.That(ActiveDiaryText(), Does.Not.Contain("Approach:"));
            Assert.That(recorded.events.Any(entry => BlockSpeeches.IsReceiptKind(entry.kind)), Is.False);
            AssertIntentDurable(recorded);
        }

        [UnityTest]
        public IEnumerator BlockSpeeches_NpcPublicSpeechesDoNotCastOrReplaceThePlayersOwnBallot()
        {
            yield return InstallBlockSpeechHouse(playerNominee: false); yield return OpenStation();
            var before = director.Snapshot;
            Assert.That(ButtonWithCaptionOrNull(EpisodeHud.EvictionSpeechCaption), Is.Null);
            ButtonWithCaption("Continue episode").onClick.Invoke(); yield return null;
            var voting = director.Snapshot;
            Assert.That(voting.evictionStage, Is.EqualTo(EvictionStage.Voting));
            Assert.That(voting.evictionSpeeches.Count, Is.EqualTo(2));
            foreach (var speech in voting.evictionSpeeches)
                Assert.That(BlockSpeeches.Approach(voting, speech), Is.Not.Null);
            Assert.That(voting.votes, Is.Empty, "Hearing both NPC speeches cannot choose the player's ballot.");
            string chosen = before.nominees[1];
            ButtonWithCaption("Vote to evict " + voting.Find(chosen).name).onClick.Invoke();
            AssertIntentCommand(voting, EpisodeCommandKind.CastVote, chosen);
            var voted = director.Snapshot;
            Assert.That(voted.votes.Single(vote => vote.voterId == voted.playerId).targetId, Is.EqualTo(chosen));
            Assert.That(ActiveDiaryText(), Does.Contain(EpisodeHud.BlockSpeechReadbackHeading));
            foreach (var speech in voted.evictionSpeeches)
                Assert.That(ActiveDiaryText(), Does.Contain(BlockSpeeches.ReceiptText(speech)));
            yield return ReloadEpisode(); HoldTheHouseForTheFixture(); yield return OpenStation();
            AssertIntentDurable(voted);
        }
    }
}
