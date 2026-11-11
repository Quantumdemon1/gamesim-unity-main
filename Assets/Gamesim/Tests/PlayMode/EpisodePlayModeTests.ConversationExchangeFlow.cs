using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Gamesim.Episode;
using Gamesim.Simulation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    public sealed partial class EpisodePlayModeTests
    {
        private string exchangeFlowNpcId;

        private IEnumerator InstallExchangeFlowHouse(bool campaign = false)
        {
            // Reuse the validated nominee-question fixture for the aimed economy-rule question.
            // The production rule selector deliberately refuses a played campaign season.
            if (campaign) yield return InstallNomineeQuestion(6);
            else yield return InstallTalkingHouse(6, false);
            exchangeFlowNpcId = Listener(director.Snapshot).id;
            if (!campaign) yield return TalkTo(exchangeFlowNpcId);
        }

        private EpisodeCommand ExchangeFlowCommand(EpisodeState origin, EpisodeCommandKind kind,
            string subject = null, string id = null) => new EpisodeCommand
        {
            id = id ?? Guid.NewGuid().ToString("N"), actorId = origin.playerId,
            expectedPhase = origin.phase, expectedRevision = origin.revision,
            kind = kind, targetId = exchangeFlowNpcId, secondTargetId = subject,
        };

        private void AssertExchangeFlowStep(string speakerId, string line)
        {
            Assert.That(director.IsConversationExchangePlaying, Is.True);
            Assert.That(director.ConversationExchangeSpeakerId, Is.EqualTo(speakerId));
            var spoken = ActiveRect(EpisodeHud.ConversationExchangeLineName);
            Assert.That(spoken, Is.Not.Null, "The current speaker's words must be drawn.");
            Assert.That(spoken.GetComponent<TMP_Text>().text, Is.EqualTo("\"" + line + "\""));
            Assert.That(ButtonWithCaption(EpisodeHud.ConversationContinueCaption), Is.Not.Null);
            Assert.That(ButtonWithCaption(EpisodeHud.ConversationSkipCaption), Is.Not.Null);
        }

        private string ExchangeFlowStatusText()
        {
            var status = ActiveRect("Status");
            return status == null ? string.Empty : string.Join(" ", status.GetComponentsInChildren<TMP_Text>()
                .Where(label => label.gameObject.activeInHierarchy).Select(label => label.text));
        }

        private void AssertExchangeFlowStatusClean(EpisodeState before, EpisodeState committed)
        {
            string status = ExchangeFlowStatusText();
            Assert.That(status, Does.Not.Contain("Saved locally"));
            foreach (var entry in committed.events.Where(item => item.sequence >= before.nextSequence
                && (item.kind == "conversation" || item.kind == "information")))
            {
                Assert.That(status, Does.Not.Contain(entry.text), "Social narration has its recap, not the Status strip.");
                Assert.That(status, Does.Not.Contain(NarrativeDisplay.Render(committed, entry.text)),
                    "Grammar-corrected social narration is not status guidance either.");
            }
        }

        private void AssertExchangeFlowUnchanged(EpisodeState committed, byte[] savedBytes)
        {
            AssertIntentDurable(committed);
            CollectionAssert.AreEqual(committed.acceptedCommandIds, director.Snapshot.acceptedCommandIds,
                "Reading, skipping and old callbacks must not submit another command.");
            Assert.That(director.Snapshot.randomState, Is.EqualTo(committed.randomState));
            Assert.That(director.Snapshot.npcSocial.randomState, Is.EqualTo(committed.npcSocial.randomState));
            CollectionAssert.AreEqual(savedBytes, File.ReadAllBytes(director.SavePath),
                "Playback must not rotate or rewrite the durable receipt.");
        }

        [UnityTest]
        public IEnumerator ConversationExchange_SmallTalkButtonHandsTheFloorOverOnceWithoutAnotherCommit()
        {
            yield return InstallExchangeFlowHouse();
            yield return EnterConversationCategory(EpisodeDirector.ConnectCategoryCaption);
            var before = director.Snapshot;
            var oldAction = ButtonWithCaption(EpisodeHud.SmallTalkCaption).onClick;
            oldAction.Invoke();
            var committed = director.Snapshot;
            Assert.That(committed.revision, Is.EqualTo(before.revision + 1));
            string id = committed.acceptedCommandIds.Except(before.acceptedCommandIds).Single();
            var command = ExchangeFlowCommand(before, EpisodeCommandKind.SmallTalk, id: id);
            var exchange = ConversationExchange.Create(before, committed, command,
                committed.events.Where(item => item.sequence >= before.nextSequence).ToList());
            Assert.That(exchange, Is.Not.Null);
            byte[] savedBytes = File.ReadAllBytes(director.SavePath);
            oldAction.Invoke(); // The retained, now-inactive choice cannot spend the action twice.
            yield return null;
            AssertExchangeFlowStep(before.playerId, "How's the day treating you?");
            AssertExchangeFlowStatusClean(before, committed);
            AssertExchangeFlowUnchanged(committed, savedBytes);

            if (Application.isBatchMode) yield return CaptureFraming("phase-conversation-smalltalk-player");
            var playerContinue = ButtonWithCaption(EpisodeHud.ConversationContinueCaption).onClick;
            playerContinue.Invoke();
            playerContinue.Invoke(); // The previous step cannot also finish the NPC's answer.
            yield return null;
            AssertExchangeFlowStep(exchangeFlowNpcId, exchange.NpcLine);
            AssertExchangeFlowStatusClean(before, committed);
            AssertExchangeFlowUnchanged(committed, savedBytes);

            if (Application.isBatchMode) yield return CaptureFraming("phase-conversation-smalltalk-npc");
            var npcContinue = ButtonWithCaption(EpisodeHud.ConversationContinueCaption).onClick;
            npcContinue.Invoke();
            yield return null;
            npcContinue.Invoke(); playerContinue.Invoke(); oldAction.Invoke();
            Assert.That(director.IsConversationExchangePlaying, Is.False);
            Assert.That(director.ConversationExchangeSpeakerId, Is.Null);
            Assert.That(ActiveRect(EpisodeHud.ConversationExchangeLineName), Is.Null);
            AssertExchangeFlowStatusClean(before, committed);
            AssertExchangeFlowUnchanged(committed, savedBytes);
        }

        [UnityTest]
        public IEnumerator ConversationExchange_AimedIntelShowsTheSelectedQuestionThenItsCommittedNpcAnswer()
        {
            yield return InstallExchangeFlowHouse(campaign: true);
            var before = director.Snapshot;
            string subject = NomineeIntel.Targets(before, exchangeFlowNpcId).First();
            var command = ExchangeFlowCommand(before, EpisodeCommandKind.AskForIntel, subject);
            var result = director.Submit(command);
            Assert.That(result.accepted, Is.True, result.reason);
            var committed = director.Snapshot;
            var exchange = ConversationExchange.Create(before, committed, command,
                committed.events.Where(item => item.sequence >= before.nextSequence
                    && (result.beatsFromSequence <= 0 || item.sequence < result.beatsFromSequence)).ToList());
            Assert.That(exchange, Is.Not.Null);
            byte[] savedBytes = File.ReadAllBytes(director.SavePath);
            yield return null;
            AssertExchangeFlowStep(before.playerId, "What's your read on " + before.Find(subject).name + "?");
            AssertExchangeFlowStatusClean(before, committed);

            ButtonWithCaption(EpisodeHud.ConversationContinueCaption).onClick.Invoke();
            yield return null;
            AssertExchangeFlowStep(exchangeFlowNpcId, exchange.NpcLine);
            Assert.That(exchange.NpcLine, Does.Contain(before.Find(subject).name));
            AssertExchangeFlowStatusClean(before, committed);
            AssertExchangeFlowUnchanged(committed, savedBytes);
            if (Application.isBatchMode) yield return CaptureFraming("phase-conversation-aimed-intel-npc");
            ButtonWithCaption(EpisodeHud.ConversationContinueCaption).onClick.Invoke();
            yield return null;
            Assert.That(director.IsConversationExchangePlaying, Is.False);
            AssertExchangeFlowUnchanged(committed, savedBytes);
        }

        [UnityTest]
        public IEnumerator ConversationExchange_SkipAndRetainedCallbacksCannotCancelANewerAcceptedExchange()
        {
            yield return InstallExchangeFlowHouse();
            var before = director.Snapshot;
            var result = director.Submit(ExchangeFlowCommand(before, EpisodeCommandKind.SmallTalk));
            Assert.That(result.accepted, Is.True, result.reason);
            var committed = director.Snapshot;
            byte[] savedBytes = File.ReadAllBytes(director.SavePath);
            yield return null;
            var oldContinue = ButtonWithCaption(EpisodeHud.ConversationContinueCaption).onClick;
            var oldSkip = ButtonWithCaption(EpisodeHud.ConversationSkipCaption).onClick;
            oldSkip.Invoke();
            yield return null;
            oldSkip.Invoke(); oldContinue.Invoke();
            Assert.That(director.IsConversationExchangePlaying, Is.False);
            AssertExchangeFlowUnchanged(committed, savedBytes);

            var promise = ExchangeFlowCommand(committed, EpisodeCommandKind.PromiseSafety);
            var next = director.Submit(promise);
            Assert.That(next.accepted, Is.True, next.reason);
            var newer = director.Snapshot;
            byte[] newerBytes = File.ReadAllBytes(director.SavePath);
            yield return null;
            oldSkip.Invoke(); oldContinue.Invoke();
            AssertExchangeFlowStep(newer.playerId, "I promise you safety.");
            AssertExchangeFlowUnchanged(newer, newerBytes);
            ButtonWithCaption(EpisodeHud.ConversationSkipCaption).onClick.Invoke();
            yield return null;
            Assert.That(director.IsConversationExchangePlaying, Is.False);
            AssertExchangeFlowUnchanged(newer, newerBytes);

            string acknowledgement = HouseDialogue.Response(newer, exchangeFlowNpcId, EpisodeCommandKind.PromiseSafety);
            var duplicate = director.Submit(promise);
            Assert.That(duplicate.accepted, Is.False);
            Assert.That(duplicate.duplicate, Is.True);
            yield return null;
            Assert.That(director.IsConversationExchangePlaying, Is.False);
            Assert.That(ActiveRect(EpisodeHud.ConversationExchangeLineName), Is.Null);
            var spoken = ActiveRect("NPC spoken dialogue");
            if (spoken != null) Assert.That(spoken.GetComponent<TMP_Text>().text, Does.Not.Contain(acknowledgement),
                "A duplicate cannot replay the earlier promise acceptance as a new response.");
            AssertExchangeFlowUnchanged(newer, newerBytes);

            var stale = director.Submit(ExchangeFlowCommand(committed, EpisodeCommandKind.SmallTalk));
            Assert.That(stale.accepted, Is.False);
            Assert.That(stale.duplicate, Is.False);
            yield return null;
            Assert.That(director.IsConversationExchangePlaying, Is.False);
            spoken = ActiveRect("NPC spoken dialogue");
            if (spoken != null) Assert.That(spoken.GetComponent<TMP_Text>().text, Does.Not.Contain(acknowledgement),
                "A rejected stale choice cannot inherit the last accepted promise's reply.");
            AssertExchangeFlowUnchanged(newer, newerBytes);
        }

        [UnityTest]
        public IEnumerator ConversationExchange_CloseReopenAndReloadInvalidatePlaybackWithoutReplayingTheReceipt()
        {
            yield return InstallExchangeFlowHouse();
            var before = director.Snapshot;
            var result = director.Submit(ExchangeFlowCommand(before, EpisodeCommandKind.SmallTalk));
            Assert.That(result.accepted, Is.True, result.reason);
            var committed = director.Snapshot;
            byte[] savedBytes = File.ReadAllBytes(director.SavePath);
            yield return null;
            var retainedContinue = ButtonWithCaption(EpisodeHud.ConversationContinueCaption).onClick;
            var retainedSkip = ButtonWithCaption(EpisodeHud.ConversationSkipCaption).onClick;
            director.ClosePanels();
            Assert.That(director.IsConversationExchangePlaying, Is.False);
            yield return TalkTo(exchangeFlowNpcId);
            retainedContinue.Invoke(); retainedSkip.Invoke();
            Assert.That(director.IsConversationExchangePlaying, Is.False);
            Assert.That(ActiveRect(EpisodeHud.ConversationExchangeLineName), Is.Null,
                "Reopening a conversation shows its greeting, not the previous command's player line.");
            AssertExchangeFlowUnchanged(committed, savedBytes);

            // A second real decision lets this same test interrupt active playback with a load.
            var next = director.Submit(ExchangeFlowCommand(committed, EpisodeCommandKind.SmallTalk));
            Assert.That(next.accepted, Is.True, next.reason);
            var latest = director.Snapshot;
            byte[] latestBytes = File.ReadAllBytes(director.SavePath);
            yield return null;
            var beforeLoad = ButtonWithCaption(EpisodeHud.ConversationContinueCaption).onClick;
            director.LoadNow(); HoldTheHouseForTheFixture();
            Assert.That(director.IsConversationExchangePlaying, Is.False);
            yield return TalkTo(exchangeFlowNpcId);
            beforeLoad.Invoke();
            Assert.That(director.IsConversationExchangePlaying, Is.False);
            Assert.That(ActiveRect(EpisodeHud.ConversationExchangeLineName), Is.Null);
            AssertExchangeFlowUnchanged(latest, latestBytes);

            var beforeScene = director.Submit(ExchangeFlowCommand(latest, EpisodeCommandKind.SmallTalk));
            Assert.That(beforeScene.accepted, Is.True, beforeScene.reason);
            latest = director.Snapshot;
            latestBytes = File.ReadAllBytes(director.SavePath);
            yield return null;
            Assert.That(director.IsConversationExchangePlaying, Is.True);
            var beforeSceneReload = ButtonWithCaption(EpisodeHud.ConversationContinueCaption).onClick;
            yield return ReloadEpisode(); HoldTheHouseForTheFixture();
            yield return TalkTo(exchangeFlowNpcId);
            retainedContinue.Invoke(); retainedSkip.Invoke(); beforeLoad.Invoke(); beforeSceneReload.Invoke();
            Assert.That(director.IsConversationExchangePlaying, Is.False);
            Assert.That(director.ConversationExchangeSpeakerId, Is.Null);
            Assert.That(ActiveRect(EpisodeHud.ConversationExchangeLineName), Is.Null);
            AssertExchangeFlowUnchanged(latest, latestBytes);
        }

        // The line the category page narrates for an accepted act, read as the director reads it: the last event of
        // the command the player may see, before any beat of the house's that the same commit carried.
        private static string ExchangeFlowOutcomeLine(EpisodeState before, CommandResult result)
        {
            var state = result.state;
            var outcome = state.events.Where(e => e.sequence >= before.nextSequence
                && (result.beatsFromSequence <= 0 || e.sequence < result.beatsFromSequence)
                && (e.audienceIds.Count == 0 || e.audienceIds.Contains(state.playerId))
                && !KnownBallots.TellsAnUnknownBallot(state, e))
                .LastOrDefault(e => !EpisodeEngine.IsScaffolding(e.kind));
            return outcome == null ? null : EpisodeDirector.EventLine(state, outcome);
        }

        [UnityTest]
        public IEnumerator ConversationExchange_ASpokenActEndsOnItsCategoryPageWithWhatHappened()
        {
            yield return InstallExchangeFlowHouse();
            yield return EnterConversationCategory(EpisodeDirector.ConnectCategoryCaption);

            // Heard to the end: both lines read with Continue.
            var before = director.Snapshot;
            var talk = director.Submit(ExchangeFlowCommand(before, EpisodeCommandKind.SmallTalk));
            Assert.That(talk.accepted, Is.True, talk.reason);
            string talkLine = ExchangeFlowOutcomeLine(before, talk);
            Assert.That(talkLine, Is.Not.Null.And.Not.Empty, "Small talk lands a line the player may read.");
            yield return null;
            Assert.That(director.IsConversationExchangePlaying, Is.True, "The act is spoken first.");
            ButtonWithCaption(EpisodeHud.ConversationContinueCaption).onClick.Invoke();
            yield return null;
            ButtonWithCaption(EpisodeHud.ConversationContinueCaption).onClick.Invoke();
            yield return null;
            Assert.That(director.IsConversationExchangePlaying, Is.False);
            Assert.That(director.CurrentConversationCategory, Is.EqualTo(EpisodeDirector.ConnectCategoryCaption),
                "The spoken lines hand back to the page the act was chosen on.");
            Assert.That(ShownText(), Does.Contain("WHAT HAPPENED"), "After the spoken lines the page says what came of them,");
            Assert.That(ShownText(), Does.Contain(talkLine), "in the act's own line.");

            // Skipped: a promise, its lines passed over, still ends on its result.
            var promised = director.Snapshot;
            var promise = director.Submit(ExchangeFlowCommand(promised, EpisodeCommandKind.PromiseSafety));
            Assert.That(promise.accepted, Is.True, promise.reason);
            var committed = director.Snapshot;
            byte[] savedBytes = File.ReadAllBytes(director.SavePath);
            string promiseLine = ExchangeFlowOutcomeLine(promised, promise);
            Assert.That(promiseLine, Is.Not.Null.And.Not.Empty, "A promise lands a line the player may read.");
            yield return null;
            Assert.That(director.IsConversationExchangePlaying, Is.True);
            ButtonWithCaption(EpisodeHud.ConversationSkipCaption).onClick.Invoke();
            yield return null;
            Assert.That(director.IsConversationExchangePlaying, Is.False);
            Assert.That(ShownText(), Does.Contain("WHAT HAPPENED"));
            Assert.That(ShownText(), Does.Contain(promiseLine), "The newer act's line is the one shown.");
            AssertExchangeFlowUnchanged(committed, savedBytes);

            // Back to the topics: the result was read on its page, and the wheel does not repeat it.
            ButtonWithCaption(EpisodeDirector.ConversationTopicsBackCaption).onClick.Invoke();
            yield return null;
            Assert.That(ActiveRect(EpisodeHud.DialName), Is.Not.Null);
            Assert.That(ShownText(), Does.Not.Contain("WHAT HAPPENED"));
            AssertExchangeFlowUnchanged(committed, savedBytes);
        }

        [UnityTest]
        public IEnumerator ConversationExchange_UnsupportedPrivateMoveKeepsItsOutcomeAsNarrationWithoutInventingSpeech()
        {
            yield return InstallExchangeFlowHouse();
            yield return EnterConversationCategory(EpisodeDirector.PressureCategoryCaption);
            var before = director.Snapshot;
            var scheme = ButtonWithCaption("Work against them quietly");
            Assert.That(scheme.IsInteractable(), Is.True);
            scheme.onClick.Invoke();
            var committed = director.Snapshot;
            Assert.That(committed.revision, Is.EqualTo(before.revision + 1));
            Assert.That(committed.acceptedCommandIds.Count, Is.EqualTo(before.acceptedCommandIds.Count + 1));
            Assert.That(director.CurrentConversationCategory, Is.EqualTo(EpisodeDirector.PressureCategoryCaption),
                "The category owns the narrated outcome before Back returns directly to topics.");
            byte[] savedBytes = File.ReadAllBytes(director.SavePath);
            string outcome = committed.events.Single(item => item.sequence >= before.nextSequence && item.kind == "scheme").text;
            yield return null;
            Assert.That(director.IsConversationExchangePlaying, Is.False);
            Assert.That(ActiveRect(EpisodeHud.ConversationExchangeLineName), Is.Null);
            Assert.That(ButtonWithCaptionOrNull(EpisodeHud.ConversationContinueCaption), Is.Null);
            Assert.That(ButtonWithCaptionOrNull(EpisodeHud.ConversationSkipCaption), Is.Null);
            Assert.That(ShownText(), Does.Contain("WHAT HAPPENED"));
            Assert.That(ShownText(), Does.Contain(NarrativeDisplay.Render(committed, outcome)));
            Assert.That(ExchangeFlowStatusText(), Does.Not.Contain(outcome));
            Assert.That(ExchangeFlowStatusText(), Does.Not.Contain("Saved locally"));
            var spoken = ActiveRect("NPC spoken dialogue");
            if (spoken != null) Assert.That(spoken.GetComponent<TMP_Text>().text, Does.Not.Contain(outcome),
                "A covert move recorded only for the player is not an NPC's disclosure.");
            Assert.That(ActiveRect(EpisodeHud.DialName), Is.Null, "The narrated result has its own view.");
            var returnToTopics = ButtonWithCaption(EpisodeDirector.ConversationTopicsBackCaption).onClick;
            returnToTopics.Invoke();
            yield return null;
            Assert.That(ActiveRect(EpisodeHud.DialName), Is.Not.Null);
            Assert.That(ShownText(), Does.Not.Contain("WHAT HAPPENED"));
            returnToTopics.Invoke();
            Assert.That(ActiveRect(EpisodeHud.DialName), Is.Not.Null, "The old result control cannot replace the topic view.");
            AssertExchangeFlowUnchanged(committed, savedBytes);
        }

        [UnityTest]
        public IEnumerator ConversationExchange_SaveFailurePublishesNoDialogueAndKeepsItsErrorVisible()
        {
            if (!Application.isEditor) Assert.Ignore("The durable writer diagnostic is Editor-only.");
            yield return InstallExchangeFlowHouse();
            var before = director.Snapshot;
            byte[] savedBytes = File.ReadAllBytes(director.SavePath);
            var writer = typeof(EpisodeDirector).GetField("saveCandidateForDiagnostics", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(writer, Is.Not.Null);
            int writes = 0;
            EpisodeState attempted = null;
            writer.SetValue(director, (Action<EpisodeState>)(candidate =>
            {
                writes++; attempted = candidate.Clone();
                throw new IOException("Injected conversation-exchange before-write failure.");
            }));
            try
            {
                var result = director.Submit(ExchangeFlowCommand(before, EpisodeCommandKind.SmallTalk));
                Assert.That(result.accepted, Is.False);
                Assert.That(writes, Is.EqualTo(1));
                Assert.That(attempted, Is.Not.Null);
                Assert.That(attempted.revision, Is.EqualTo(before.revision + 1));
                yield return null;
                Assert.That(director.IsConversationExchangePlaying, Is.False);
                Assert.That(director.ConversationExchangeSpeakerId, Is.Null);
                Assert.That(ActiveRect(EpisodeHud.ConversationExchangeLineName), Is.Null);
                Assert.That(ActiveRect(EpisodeHud.SaveFeedbackName), Is.Null);
                Assert.That(ExchangeFlowStatusText(), Does.Contain("not committed"),
                    "The failed transaction must remain visible while the conversation is open.");
                Assert.That(ExchangeFlowStatusText(), Does.Not.Contain("Saved locally"));
                Assert.That(director.StatusMessage, Does.Contain("not committed"));
                AssertExchangeFlowUnchanged(before, savedBytes);
                director.GoToStation();
                yield return null;
                Assert.That(director.StatusMessage, Does.Not.Contain("not committed"),
                    "Travel changes its ordinary message while the unresolved save warning keeps priority.");
                Assert.That(ExchangeFlowStatusText(), Does.Contain("not committed"));
                Assert.That(ActiveRect(EpisodeHud.SaveFeedbackName), Is.Null);
                AssertExchangeFlowUnchanged(before, savedBytes);
            }
            finally { if (director != null) writer.SetValue(director, null); }
        }
    }
}
