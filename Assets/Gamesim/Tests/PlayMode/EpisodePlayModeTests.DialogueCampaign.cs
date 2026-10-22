using System;
using System.Collections;
using System.IO;
using System.Linq;
using Gamesim.Episode;
using Gamesim.Persistence;
using Gamesim.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    public sealed partial class EpisodePlayModeTests
    {
        private string dialogueUiVoter;

        [UnityTest]
        public IEnumerator DialogueCampaign_VisiblePaidEntryFreeRepliesAndEveryNodeSurviveReload()
        {
            yield return InstallDialogueUiCampaign();
            var before = director.Snapshot;
            int raw = before.windowActions[Windows.AfterVeto];
            yield return OpenDialogueUiEntry();
            var entered = director.Snapshot;
            Assert.That(entered.revision, Is.EqualTo(before.revision + 1));
            Assert.That(entered.windowActions[Windows.AfterVeto], Is.EqualTo(raw + 1));
            Assert.That(DialogueUiRemaining(entered), Is.Zero);
            Assert.That(entered.dialogue.episodes, Has.Count.EqualTo(1));
            string episodeId = entered.dialogue.episodes.Single().id;
            AssertDialogueUiNode(episodeId, DialogueCampaignV1.Case, 1);
            yield return ReloadDialogueUiBytes();
            yield return TalkTo(dialogueUiVoter);
            AssertDialogueUiNode(episodeId, DialogueCampaignV1.Case, 1);
            ButtonWithCaption(EpisodeHud.EventChoiceCaption(DialogueCampaignV1.AppealLabel)).onClick.Invoke();
            yield return Frames(2);
            Assert.That(director.Snapshot.revision, Is.EqualTo(entered.revision + 1));
            AssertDialogueUiNode(episodeId, DialogueCampaignV1.Finish, 2);
            Assert.That(director.Snapshot.windowActions[Windows.AfterVeto], Is.EqualTo(raw + 1));
            yield return ReloadDialogueUiBytes();
            yield return TalkTo(dialogueUiVoter);
            AssertDialogueUiNode(episodeId, DialogueCampaignV1.Finish, 2);
            int beforeTerminal = director.Snapshot.revision;
            ButtonWithCaption(EpisodeHud.EventChoiceCaption(DialogueCampaignV1.PleaLabel)).onClick.Invoke();
            yield return Frames(2);

            var closed = director.Snapshot;
            Assert.That(closed.revision, Is.EqualTo(beforeTerminal + 1));
            Assert.That(closed.windowActions[Windows.AfterVeto], Is.EqualTo(raw + 1));
            Assert.That(closed.outOfPhaseSocialActions, Is.EqualTo(entered.outOfPhaseSocialActions));
            Assert.That(DialogueUiRemaining(closed), Is.Zero);
            Assert.That(DialogueRules.Active(closed), Is.Null);
            var episode = closed.dialogue.episodes.Single();
            var cycle = closed.storylines.Single(c => c.id == episodeId);
            Assert.That(cycle.path.Select(p => p.optionId), Is.EqualTo(new[] { "open-case", "appeal-personally", "make-plea" }));
            Assert.That(cycle.status, Is.EqualTo(StorylineStatus.Completed));
            Assert.That(cycle.endingId, Is.EqualTo("answered"));
            Assert.That(closed.houseEvents.Count(e => e.cycleId == episodeId), Is.EqualTo(3));
            Assert.That(episode.time.reason, Is.EqualTo("answered"));
            Assert.That(episode.time.proposedDelta, Is.Zero); Assert.That(episode.time.appliedDelta, Is.Zero);
            var lobby = closed.lobbies.Single(l => l.week == closed.week && l.phase == EpisodePhase.Campaign && l.deciderId == dialogueUiVoter);
            Assert.That((lobby.ask, lobby.subjectId, lobby.approach), Is.EqualTo((LobbyAsk.Vote, closed.playerId, LobbyApproach.Emotional)));
            Assert.That(episode.responses.Last().responseKey, Is.EqualTo(DialogueCampaignV1.TemplateId + "/vote-" + lobby.response));
            yield return ReloadDialogueUiBytes();
            AssertEquivalent(closed, director.Snapshot);
        }

        [UnityTest]
        public IEnumerator DialogueCampaign_WalkAwayResumeAndReloadRetireCapturedChoiceCallbacks()
        {
            yield return InstallDialogueUiCampaign();
            yield return OpenDialogueUiEntry();
            string episodeId = director.Snapshot.dialogue.episodes.Single().id;
            var staleMiddle = ButtonWithCaption(EpisodeHud.EventChoiceCaption(DialogueCampaignV1.AppealLabel)).onClick;
            var paused = director.Snapshot;
            byte[] pausedBytes = File.ReadAllBytes(director.SavePath);
            yield return LeaveDialogueUiConversation();
            staleMiddle.Invoke(); yield return null;
            AssertDialogueUiUnchanged(paused, pausedBytes);
            yield return TalkTo(dialogueUiVoter);
            AssertDialogueUiNode(episodeId, DialogueCampaignV1.Case, 1);
            staleMiddle.Invoke(); yield return null;
            AssertDialogueUiUnchanged(paused, pausedBytes);
            var liveMiddle = ButtonWithCaption(EpisodeHud.EventChoiceCaption(DialogueCampaignV1.AppealLabel)).onClick;
            liveMiddle.Invoke(); yield return Frames(2);
            Assert.That(director.Snapshot.revision, Is.EqualTo(paused.revision + 1));
            AssertDialogueUiNode(episodeId, DialogueCampaignV1.Finish, 2);
            var middle = director.Snapshot;
            byte[] middleBytes = File.ReadAllBytes(director.SavePath);
            liveMiddle.Invoke(); yield return null;
            AssertDialogueUiUnchanged(middle, middleBytes);

            var staleFinish = ButtonWithCaption(EpisodeHud.EventChoiceCaption(DialogueCampaignV1.PleaLabel)).onClick;
            yield return ReloadDialogueUiBytes();
            yield return TalkTo(dialogueUiVoter);
            AssertDialogueUiNode(episodeId, DialogueCampaignV1.Finish, 2);
            staleFinish.Invoke(); yield return null;
            AssertDialogueUiUnchanged(middle, middleBytes);
            Assert.That(director.Snapshot.lobbies.Any(l => l.week == middle.week && l.deciderId == dialogueUiVoter), Is.False);
            ButtonWithCaption(EpisodeHud.EventChoiceCaption(DialogueCampaignV1.LeaveLabel)).onClick.Invoke();
            yield return Frames(2);
            var closed = director.Snapshot;
            Assert.That(closed.revision, Is.EqualTo(middle.revision + 1));
            Assert.That(closed.windowActions, Is.EqualTo(middle.windowActions));
            Assert.That(DialogueUiRemaining(closed), Is.Zero);
            Assert.That(DialogueRules.Active(closed), Is.Null);
            Assert.That(closed.storylines.Single(c => c.id == episodeId).endingId, Is.EqualTo("neutral"));
            Assert.That(closed.dialogue.episodes.Single().time.reason, Is.EqualTo("neutral"));
            Assert.That(closed.houseEvents.Count(e => e.cycleId == episodeId), Is.EqualTo(3));
            Assert.That(closed.lobbies.Any(l => l.week == closed.week && l.deciderId == dialogueUiVoter), Is.False);
            yield return ReloadDialogueUiBytes();
        }

        private IEnumerator InstallDialogueUiCampaign()
        {
            HoldTheHouseForTheFixture();
            EpisodeState fixture = null;
            // This first-week search uses the existing public-command navigator, never an Edit
            // Mode-only driver or a hand-assigned nominee, action counter, RNG or story history.
            for (uint seed = 1; seed <= 24 && fixture == null; seed++)
            {
                var fresh = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, seed);
                ShippedRules.ApplyFresh(fresh);
                Assert.That(fresh.dialogueRulesVersion, Is.Zero);
                fresh.dialogueRulesVersion = 1; // Explicit unpublished fixture enrollment only.
                var engine = new EpisodeEngine(fresh);
                for (int step = 0; step < 96; step++)
                {
                    var s = engine.Snapshot;
                    if (s.week != 1 || s.phase == EpisodePhase.Finished) break;
                    if (s.phase == EpisodePhase.Campaign)
                    {
                        if (!s.nominees.Contains(s.playerId)) break;
                        CloseDialogueUiOldAsks(engine);
                        for (int debit = 0; debit < 16 && DialogueUiRemaining(engine.Snapshot) > 1; debit++)
                        {
                            s = engine.Snapshot;
                            var talk = DialogueUiCommand(s, EpisodeCommandKind.Talk);
                            talk.targetId = s.nominees.First(id => id != s.playerId);
                            DialogueUiApply(engine, talk);
                            CloseDialogueUiOldAsks(engine);
                        }
                        s = engine.Snapshot;
                        dialogueUiVoter = EpisodeEngine.Voters(s).Where(c => !c.isPlayer).Select(c => c.id)
                            .FirstOrDefault(id => DialogueRules.CanStart(s, id, out _));
                        if (DialogueUiRemaining(s) != 1 || dialogueUiVoter == null) break;
                        fixture = s;
                        TestContext.Out.WriteLine("Paid dialogue UI witness: seed " + seed + ", first week, revision " + s.revision + ".");
                        break;
                    }
                    DialogueUiApply(engine, NextCommand(s));
                }
            }
            Assert.That(fixture, Is.Not.Null, "No legal nominated first-week campaign in 24 seeds x 96 commands; no state was fabricated.");
            Assert.That(EpisodeValidation.TryValidate(fixture, out string error), Is.True, error);
            new EpisodeSaveStore(director.SavePath).Save(fixture);
            yield return ReloadEpisode();
            HoldTheHouseForTheFixture();
            yield return SkipReveals();
            AssertEquivalent(fixture, director.Snapshot);
            Assert.That(DialogueUiRemaining(director.Snapshot), Is.EqualTo(1));
            AssertDialogueUiDisk(director.Snapshot);
        }

        private IEnumerator OpenDialogueUiEntry()
        {
            yield return TalkTo(dialogueUiVoter);
            var source = director.Snapshot;
            byte[] bytes = File.ReadAllBytes(director.SavePath);
            Assert.That(ButtonWithCaptionOrNull("Desperate plea"), Is.Null, "The approach is hidden until its category and ask are chosen.");
            yield return EnterConversationCategory(EpisodeDirector.GameCategoryCaption);
            string ask = EpisodeHud.LobbyAskCaption(source, source.Find(dialogueUiVoter).name, LobbyAsk.Vote, source.playerId);
            ButtonWithCaption(ask).onClick.Invoke(); yield return null;
            AssertDialogueUiUnchanged(source, bytes);
            Assert.That(ShownText(), Does.Contain("replies and final plea cost no further actions"));
            ButtonWithCaption("Desperate plea").onClick.Invoke();
            yield return Frames(2);
        }

        private IEnumerator LeaveDialogueUiConversation()
        {
            for (int back = 0; back < 4 && director.IsConversationOpen; back++) yield return PressKey(Key.Escape);
            Assert.That(director.IsConversationOpen, Is.False, "The real Back route must leave the conversation without choosing its neutral ending.");
        }

        private IEnumerator ReloadDialogueUiBytes()
        {
            var expected = director.Snapshot;
            string path = director.SavePath;
            byte[] primary = File.ReadAllBytes(path);
            byte[] backup = File.Exists(path + ".backup") ? File.ReadAllBytes(path + ".backup") : null;
            AssertDialogueUiDisk(expected);
            yield return ReloadEpisode();
            HoldTheHouseForTheFixture();
            yield return SkipReveals();
            Assert.That(director.SavePath, Is.EqualTo(path));
            AssertDialogueUiUnchanged(expected, primary);
            Assert.That(File.Exists(path + ".backup"), Is.EqualTo(backup != null));
            if (backup != null) CollectionAssert.AreEqual(backup, File.ReadAllBytes(path + ".backup"), "Reload does not rotate a good backup.");
        }

        private void AssertDialogueUiNode(string episodeId, string node, int steps)
        {
            var state = director.Snapshot;
            Assert.That(EpisodeValidation.TryValidate(state, out string error), Is.True, error);
            Assert.That(DialogueUiRemaining(state), Is.Zero);
            Assert.That(DialogueRules.Active(state)?.id, Is.EqualTo(episodeId));
            var cycle = state.storylines.Single(c => c.id == episodeId);
            Assert.That(cycle.beatId, Is.EqualTo(node));
            Assert.That(cycle.path.Count, Is.EqualTo(steps));
            Assert.That(state.dialogue.episodes.Single().time, Is.Null);
            Assert.That(state.houseEvents.Count(e => e.cycleId == episodeId), Is.EqualTo(steps + 1));
            Assert.That(ButtonWithCaption(EpisodeHud.EventChoiceCaption(node == DialogueCampaignV1.Case
                ? DialogueCampaignV1.AppealLabel : DialogueCampaignV1.PleaLabel)), Is.Not.Null,
                "The funded continuation remains pressable at zero AP.");
        }

        private void AssertDialogueUiUnchanged(EpisodeState expected, byte[] bytes)
        {
            AssertEquivalent(expected, director.Snapshot);
            CollectionAssert.AreEqual(bytes, File.ReadAllBytes(director.SavePath));
            AssertDialogueUiDisk(expected);
        }

        private void AssertDialogueUiDisk(EpisodeState expected)
        {
            Assert.That(new EpisodeSaveStore(director.SavePath).TryLoad(out var disk, out string message), Is.True, message);
            AssertEquivalent(expected, disk);
        }

        private static int DialogueUiRemaining(EpisodeState s) => EpisodeEngine.SocialActionBudget(s) - EpisodeEngine.SocialActionsSpent(s);

        private static EpisodeCommand DialogueUiCommand(EpisodeState s, EpisodeCommandKind kind) => new EpisodeCommand
        {
            id = "dialogue-ui-fixture-" + s.revision + "-" + kind, actorId = s.playerId,
            expectedRevision = s.revision, expectedPhase = s.phase, kind = kind
        };

        private static void DialogueUiApply(EpisodeEngine engine, EpisodeCommand command)
        {
            var result = engine.Apply(command);
            Assert.That(result.accepted, Is.True, command.kind + ": " + result.reason);
            Assert.That(result.duplicate, Is.False);
            Assert.That(EpisodeValidation.TryValidate(result.state, out string error), Is.True, error);
        }

        private static void CloseDialogueUiOldAsks(EpisodeEngine engine)
        {
            for (int answer = 0; answer < 24; answer++)
            {
                var s = engine.Snapshot;
                var item = s.houseEvents.FirstOrDefault(e => !e.resolved && HouseEventKind.Asks(e.kind));
                if (item == null) return;
                Assert.That(item.contentId == null || !item.contentId.StartsWith(DialogueCampaignV1.TemplateId + ":", StringComparison.Ordinal), Is.True);
                var command = DialogueUiCommand(s, item.IsStory ? EpisodeCommandKind.ProgressStoryline : EpisodeCommandKind.ResolveHouseEvent);
                command.targetId = item.id;
                if (item.IsStory)
                {
                    var lapse = item.choices.Single(c => c.optionId == item.lapseOptionId);
                    Assert.That(lapse.locked || lapse.costsAction || lapse.pickPerson, Is.False);
                    command.secondTargetId = lapse.optionId;
                }
                else command.text = item.choices.First().label;
                DialogueUiApply(engine, command);
            }
            Assert.Fail("Old asks did not close within 24 actual neutral answers; no history was cleared.");
        }
    }
}
