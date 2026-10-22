using System;
using System.Collections;
using System.IO;
using System.Linq;
using Gamesim.Episode;
using Gamesim.Persistence;
using Gamesim.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    public sealed partial class EpisodePlayModeTests
    {
        [UnityTest]
        public IEnumerator DialogueCampaign_RealGossipAtCaseAnswersVisiblyAndResumesAfterReload()
        {
            yield return DialogueRealReplyRoute(ReplyCards.Gossip, finishNode: false);
        }

        [UnityTest]
        public IEnumerator DialogueCampaign_RealGossipAtFinishAnswersVisiblyAndResumesAfterReload()
        {
            yield return DialogueRealReplyRoute(ReplyCards.Gossip, finishNode: true);
        }

        [UnityTest]
        public IEnumerator DialogueCampaign_RealConfrontationAtCaseAnswersTheQueueAndResumesAfterReload()
        {
            yield return DialogueRealReplyRoute(ReplyCards.Confrontation, finishNode: false);
        }

        [UnityTest]
        public IEnumerator DialogueCampaign_RealConfrontationAtFinishAnswersTheQueueAndResumesAfterReload()
        {
            yield return DialogueRealReplyRoute(ReplyCards.Confrontation, finishNode: true);
        }

        private IEnumerator DialogueRealReplyRoute(string kind, bool finishNode)
        {
            yield return InstallDialogueRealReplyCampaign();
            var beforeEntry = director.Snapshot;
            yield return OpenDialogueUiEntry(); // Real category -> vote ask -> Desperate plea.
            string episodeId = director.Snapshot.dialogue.episodes.Single().id;
            Assert.That(director.Snapshot.revision, Is.EqualTo(beforeEntry.revision + 1));
            Assert.That(director.Snapshot.windowActions[Windows.AfterVeto],
                Is.EqualTo(beforeEntry.windowActions[Windows.AfterVeto] + 1));
            Assert.That(director.Snapshot.replyCards.Select(r => r.id),
                Is.EqualTo(beforeEntry.replyCards.Select(r => r.id)), "Both real cards predate paid entry.");
            if (finishNode)
            {
                ButtonWithCaption(EpisodeHud.EventChoiceCaption(DialogueCampaignV1.AppealLabel)).onClick.Invoke();
                yield return Frames(2);
            }
            string node = finishNode ? DialogueCampaignV1.Finish : DialogueCampaignV1.Case;
            int steps = finishNode ? 2 : 1;
            string paidCaption = EpisodeHud.EventChoiceCaption(finishNode
                ? DialogueCampaignV1.PleaLabel : DialogueCampaignV1.AppealLabel);
            AssertDialogueUiNode(episodeId, node, steps);
            var paused = director.Snapshot;
            var stalePaid = ButtonWithCaption(paidCaption).onClick;
            yield return LeaveDialogueUiConversation();
            var left = director.Snapshot;
            byte[] leftPrimary = File.ReadAllBytes(director.SavePath), leftBackup = DialogueRealReplyBackup();
            stalePaid.Invoke();
            AssertDialogueRealReplyUnchanged(left, leftPrimary, leftBackup);
            AssertDialogueRealReplyPause(paused, left, episodeId, node, steps);

            // The actual Campaign board presents the oldest card. The known real Gossip310 is
            // ahead of Confront321: visibly answer it, never select a hidden ID or clear a queue.
            int answers = 0;
            if (kind == ReplyCards.Confrontation)
            {
                Assert.That(ReplyCards.Pending(director.Snapshot)?.id, Is.EqualTo("reply-310"));
                yield return AnswerDialogueRealReply("reply-310", ReplyCards.Gossip, "slide", episodeId, node, steps);
                answers++;
            }
            string target = kind == ReplyCards.Confrontation ? "reply-321" : "reply-310";
            Assert.That(ReplyCards.Pending(director.Snapshot)?.id, Is.EqualTo(target));
            yield return AnswerDialogueRealReply(target, kind,
                kind == ReplyCards.Confrontation ? "apologize" : "slide", episodeId, node, steps);
            answers++;
            var answered = director.Snapshot;
            Assert.That(answered.ledger.replies.Count, Is.EqualTo(paused.ledger.replies.Count + answers));
            Assert.That(answered.replyCards.Select(r => r.id), Is.EqualTo(kind == ReplyCards.Confrontation
                ? Array.Empty<string>() : new[] { "reply-321" }));
            AssertDialogueRealReplyPause(paused, answered, episodeId, node, steps);

            yield return ReloadDialogueUiBytes();
            yield return TalkTo(dialogueUiVoter);
            AssertDialogueUiNode(episodeId, node, steps);
            var resumed = director.Snapshot;
            byte[] resumedPrimary = File.ReadAllBytes(director.SavePath), resumedBackup = DialogueRealReplyBackup();
            stalePaid.Invoke();
            AssertDialogueRealReplyUnchanged(resumed, resumedPrimary, resumedBackup);
            if (!finishNode)
            {
                ButtonWithCaption(EpisodeHud.EventChoiceCaption(DialogueCampaignV1.AppealLabel)).onClick.Invoke();
                yield return Frames(2);
                AssertDialogueUiNode(episodeId, DialogueCampaignV1.Finish, 2);
                yield return ReloadDialogueUiBytes();
                yield return TalkTo(dialogueUiVoter);
                AssertDialogueUiNode(episodeId, DialogueCampaignV1.Finish, 2);
            }
            ButtonWithCaption(EpisodeHud.EventChoiceCaption(DialogueCampaignV1.PleaLabel)).onClick.Invoke();
            yield return Frames(2);
            var completed = director.Snapshot;
            Assert.That(EpisodeValidation.TryValidate(completed, out string error), Is.True, error);
            Assert.That(DialogueRules.Active(completed), Is.Null);
            Assert.That(DialogueUiRemaining(completed), Is.Zero);
            Assert.That(completed.windowActions, Is.EqualTo(paused.windowActions));
            Assert.That(completed.socialActions, Is.EqualTo(paused.socialActions));
            Assert.That(completed.outOfPhaseSocialActions, Is.EqualTo(paused.outOfPhaseSocialActions));
            var cycle = completed.storylines.Single(c => c.id == episodeId);
            Assert.That(cycle.endingId, Is.EqualTo("answered"));
            Assert.That(cycle.path.Select(p => p.optionId), Is.EqualTo(new[] { "open-case", "appeal-personally", "make-plea" }));
            Assert.That(completed.houseEvents.Count(e => e.cycleId == episodeId), Is.EqualTo(3));
            Assert.That(completed.dialogue.grants, Is.Empty);
            var time = completed.dialogue.episodes.Single(e => e.id == episodeId).time;
            Assert.That(time.reason, Is.EqualTo("answered"));
            Assert.That(new[] { time.proposedDelta, time.appliedDelta, time.spentRevision,
                time.spentSequence, time.spentWindowAction }, Is.All.EqualTo(0));
            Assert.That(completed.ledger.replies.Select(r => JsonUtility.ToJson(r)),
                Is.EqualTo(answered.ledger.replies.Select(r => JsonUtility.ToJson(r))));
            Assert.That(completed.replyCards.Select(r => JsonUtility.ToJson(r)),
                Is.EqualTo(answered.replyCards.Select(r => JsonUtility.ToJson(r))));
            yield return ReloadDialogueUiBytes();
            AssertEquivalent(completed, director.Snapshot);
        }

        private IEnumerator AnswerDialogueRealReply(string cardId, string kind, string key,
            string episodeId, string node, int steps)
        {
            yield return OpenStation();
            yield return Frames(2);
            var before = director.Snapshot;
            var card = ReplyCards.Pending(before);
            Assert.That(card, Is.Not.Null);
            Assert.That((card.id, card.kind), Is.EqualTo((cardId, kind)));
            Assert.That(DialogueUiRemaining(before), Is.Zero);
            AssertDialogueRealReplyDisclosure(before, card, episodeId, node, steps);
            string caption = EpisodeHud.ReplyCaption(ReplyCards.Find(kind, key).Label);
            var retiredView = ButtonWithCaption(caption).onClick;
            byte[] primary = File.ReadAllBytes(director.SavePath), backup = DialogueRealReplyBackup();
            director.ClosePanels();
            retiredView.Invoke();
            AssertDialogueRealReplyUnchanged(before, primary, backup);
            yield return OpenStation();
            retiredView.Invoke(); // Reopening at the same revision is a different view authority.
            AssertDialogueRealReplyUnchanged(before, primary, backup);
            var retiredWorld = ButtonWithCaption(caption).onClick;
            yield return ReloadDialogueUiBytes();
            yield return OpenStation();
            yield return Frames(2);
            retiredWorld.Invoke(); // Reloading the same card also retires its captured callback.
            AssertDialogueRealReplyUnchanged(before, primary, backup);
            AssertDialogueRealReplyDisclosure(before, ReplyCards.Pending(director.Snapshot), episodeId, node, steps);

            var live = ButtonWithCaption(caption).onClick; // Visible/interactable/raycast-reachable.
            var source = director.Snapshot;
            string sourceJson = JsonUtility.ToJson(source);
            primary = File.ReadAllBytes(director.SavePath);
            live.Invoke(); // The only command route under test: actual rendered ReplyChoice.
            var after = director.Snapshot;
            Assert.That(after.revision, Is.EqualTo(source.revision + 1));
            Assert.That(after.acceptedCommandIds.Count, Is.EqualTo(source.acceptedCommandIds.Count + 1));
            Assert.That(JsonUtility.ToJson(source), Is.EqualTo(sourceJson));
            AssertDialogueRealReplyPause(source, after, episodeId, node, steps);
            Assert.That(after.replyCards.Select(r => r.id), Is.EqualTo(source.replyCards.Skip(1).Select(r => r.id)));
            Assert.That(after.ledger.replies.Count, Is.EqualTo(source.ledger.replies.Count + 1));
            var row = after.ledger.replies.Last();
            Assert.That((row.cardId, row.kind, row.fromId, row.listenerId, row.replyKey),
                Is.EqualTo((cardId, kind, card.fromId, card.aboutId, key)));
            Assert.That(row.promised, Is.False);
            Assert.That(after.unifiedCommitments.Select(r => JsonUtility.ToJson(r)),
                Is.EqualTo(source.unifiedCommitments.Select(r => JsonUtility.ToJson(r))));
            Assert.That(after.promises.Select(r => JsonUtility.ToJson(r)),
                Is.EqualTo(source.promises.Select(r => JsonUtility.ToJson(r))));
            Assert.That(after.deals.Select(r => JsonUtility.ToJson(r)),
                Is.EqualTo(source.deals.Select(r => JsonUtility.ToJson(r))));

            // Determinism, not independent effect parity: replay the UI's real command ID,
            // retaining full state and both RNG streams. A free answer may draw main RNG.
            var command = DialogueRealReplyCommand(source, EpisodeCommandKind.ReplyToHouseguest);
            command.id = after.acceptedCommandIds.Last(); command.targetId = cardId; command.text = key;
            string requestJson = JsonUtility.ToJson(command);
            var replay = new EpisodeEngine(source).Apply(command);
            Assert.That(replay.accepted, Is.True, replay.reason);
            Assert.That(replay.duplicate, Is.False);
            AssertEquivalent(replay.state, after);
            Assert.That(after.npcSocial.randomState, Is.EqualTo(replay.state.npcSocial.randomState));
            Assert.That(JsonUtility.ToJson(source), Is.EqualTo(sourceJson));
            Assert.That(JsonUtility.ToJson(command), Is.EqualTo(requestJson));
            AssertDialogueUiDisk(after);
            CollectionAssert.AreEqual(primary, File.ReadAllBytes(director.SavePath + ".backup"),
                "A deliberate answer rotates the actual prior primary, not a reconstructed save.");
            primary = File.ReadAllBytes(director.SavePath); backup = DialogueRealReplyBackup();
            live.Invoke(); retiredView.Invoke(); retiredWorld.Invoke();
            AssertDialogueRealReplyUnchanged(after, primary, backup);
            yield return Frames(2);
            AssertDialogueRealReplyUnchanged(after, primary, backup);
            yield return ReloadDialogueUiBytes();
            AssertEquivalent(after, director.Snapshot);
        }

        private void AssertDialogueRealReplyDisclosure(EpisodeState s, ReplyCardState card,
            string episodeId, string node, int steps)
        {
            Assert.That(card, Is.Not.Null);
            Assert.That(ShownText(), Does.Contain(ReplyCards.Title(s, card)));
            Assert.That(ShownText(), Does.Contain(ReplyCards.Message(s, card)));
            foreach (var reply in ReplyCards.Replies(card.kind))
            {
                Assert.That(ButtonWithCaption(EpisodeHud.ReplyCaption(reply.Label)), Is.Not.Null);
                Assert.That(ShownText(), Does.Contain(ReplyCardPayoffs.Description(s, card, reply)));
            }
            // PendingStoryBeats is rendered before CampaignScreen. A reply card suppresses
            // only the legacy PendingHouseEvent, not the funded Story node above the board.
            Assert.That(node, Is.EqualTo(DialogueCampaignV1.Case).Or.EqualTo(DialogueCampaignV1.Finish));
            AssertDialogueUiNode(episodeId, node, steps); // The exact saved funded choice is still pressable at AP0.
            AssertEquivalent(s, director.Snapshot);
            AssertDialogueUiDisk(s);
        }

        private static void AssertDialogueRealReplyPause(EpisodeState before, EpisodeState after,
            string episodeId, string node, int steps)
        {
            Assert.That(EpisodeValidation.TryValidate(after, out string error), Is.True, error);
            Assert.That(after.phase, Is.EqualTo(EpisodePhase.Campaign));
            Assert.That(DialogueUiRemaining(after), Is.Zero);
            Assert.That(DialogueRules.Active(after)?.id, Is.EqualTo(episodeId));
            Assert.That(JsonUtility.ToJson(after.dialogue), Is.EqualTo(JsonUtility.ToJson(before.dialogue)));
            Assert.That(JsonUtility.ToJson(after.storylines.Single(c => c.id == episodeId)),
                Is.EqualTo(JsonUtility.ToJson(before.storylines.Single(c => c.id == episodeId))));
            Assert.That(after.storylines.Single(c => c.id == episodeId).beatId, Is.EqualTo(node));
            Assert.That(after.storylines.Single(c => c.id == episodeId).path.Count, Is.EqualTo(steps));
            Assert.That(after.houseEvents.Count(e => e.cycleId == episodeId), Is.EqualTo(steps + 1));
            Assert.That(after.windowActions, Is.EqualTo(before.windowActions));
            Assert.That(after.socialActions, Is.EqualTo(before.socialActions));
            Assert.That(after.outOfPhaseSocialActions, Is.EqualTo(before.outOfPhaseSocialActions));
            Assert.That(after.boughtActionPoints, Is.EqualTo(before.boughtActionPoints));
            Assert.That(after.moveInExtrasSpent, Is.EqualTo(before.moveInExtrasSpent));
        }

        private byte[] DialogueRealReplyBackup() => File.Exists(director.SavePath + ".backup")
            ? File.ReadAllBytes(director.SavePath + ".backup") : null;

        private void AssertDialogueRealReplyUnchanged(EpisodeState expected, byte[] primary, byte[] backup)
        {
            AssertDialogueUiUnchanged(expected, primary);
            Assert.That(File.Exists(director.SavePath + ".backup"), Is.EqualTo(backup != null));
            if (backup != null) CollectionAssert.AreEqual(backup, File.ReadAllBytes(director.SavePath + ".backup"));
        }

        private IEnumerator InstallDialogueRealReplyCampaign()
        {
            // Fixed actual discovery02 witness. No seed search, state repair, forced score/RNG/
            // actor/nominee/AP/history/card, provider, or private engine API is used.
            var fresh = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, 4);
            ShippedRules.ApplyFresh(fresh);
            Assert.That(fresh.dialogueRulesVersion, Is.Zero);
            fresh.dialogueRulesVersion = 1; // Unpublished pilot opt-in, not fresh-rule activation.
            string original = JsonUtility.ToJson(fresh);
            var engine = new EpisodeEngine(fresh);
            int[] navigation = new int[4];
            while (!(engine.Snapshot.week == 3 && engine.Snapshot.phase == EpisodePhase.Campaign))
            {
                var s = engine.Snapshot;
                Assert.That(s.week, Is.InRange(1, 3));
                Assert.That(s.Find(s.playerId).status, Is.EqualTo(ContestantStatus.Active));
                Assert.That(s.phase, Is.Not.EqualTo(EpisodePhase.Finished));
                Assert.That(navigation[s.week]++, Is.LessThan(128), "Fixed legal route exceeded its per-week navigation bound.");
                DialogueRealReplyApply(engine, DialogueRealReplyNext(s));
            }
            Assert.That(navigation.Skip(1), Is.EqualTo(new[] { 16, 16, 9 }));
            Assert.That(engine.Snapshot.nominees, Does.Contain(engine.Snapshot.playerId));
            for (int buy = 0; buy < 6; buy++)
            {
                var s = engine.Snapshot;
                Assert.That(ExtraActionPurchaseChoice.Available(s), Is.True);
                var picker = ExtraActionPurchaseChoice.Open(s);
                Assert.That(picker, Is.Not.Null);
                Assert.That(picker.CanChoose(s, "jamie-roberts"), Is.True);
                var command = DialogueRealReplyCommand(s, EpisodeCommandKind.BuyActionPoint);
                command.targetId = "jamie-roberts"; command.text = WebSocialVocabulary.BurnOne;
                DialogueRealReplyApply(engine, command);
            }
            for (int talk = 0; talk < 6; talk++)
            {
                var s = engine.Snapshot;
                Assert.That(s.houseEvents.Any(e => !e.resolved && HouseEventKind.Asks(e.kind)), Is.False,
                    "This retained witness requires no neutral cleanup; do not erase a changed history.");
                Assert.That(DialogueUiRemaining(s), Is.GreaterThan(1));
                var command = DialogueRealReplyCommand(s, EpisodeCommandKind.Talk);
                command.targetId = s.Active.Where(p => !p.isPlayer && p.id != "jamie-roberts")
                    .Select(p => p.id).OrderBy(id => id, StringComparer.Ordinal).First();
                DialogueRealReplyApply(engine, command);
            }
            var fixture = engine.Snapshot;
            Assert.That(JsonUtility.ToJson(fresh), Is.EqualTo(original));
            Assert.That(fixture.revision, Is.EqualTo(53));
            Assert.That((fixture.unifiedCommitmentRulesVersion, fixture.allWeekRulesStartWeek), Is.EqualTo((2, 1)));
            Assert.That(DialogueUiRemaining(fixture), Is.EqualTo(1));
            Assert.That(fixture.houseEvents.Any(e => !e.resolved && HouseEventKind.Asks(e.kind)), Is.False);
            Assert.That(fixture.replyCards.Select(r => r.id), Is.EqualTo(new[] { "reply-310", "reply-321" }));
            var gossip = fixture.replyCards[0]; var confrontation = fixture.replyCards[1];
            Assert.That((gossip.kind, gossip.fromId, gossip.aboutId),
                Is.EqualTo((ReplyCards.Gossip, "alex-chen", "casey-wilson")));
            Assert.That((confrontation.kind, confrontation.fromId, confrontation.aboutId),
                Is.EqualTo((ReplyCards.Confrontation, "jamie-roberts", (string)null)));
            var hunt = fixture.npcSocial.acts.Single(a => a.id == "3-2-1");
            Assert.That((hunt.kind, hunt.actorId, hunt.partnerId, hunt.subjectId, hunt.firedTick, hunt.window),
                Is.EqualTo((NpcActKinds.Hunt, "alex-chen", "casey-wilson", fixture.playerId, 0, Windows.AfterVeto)));
            var confront = fixture.npcSocial.acts.Single(a => a.id == "3-2-2");
            Assert.That((confront.kind, confront.actorId, confront.partnerId, confront.firedTick, confront.window),
                Is.EqualTo((NpcActKinds.Confront, "jamie-roberts", fixture.playerId, 1, Windows.AfterVeto)));
            dialogueUiVoter = EpisodeEngine.Voters(fixture).Where(p => !p.isPlayer).Select(p => p.id)
                .FirstOrDefault(id => DialogueRules.CanStart(fixture, id, out _));
            Assert.That(dialogueUiVoter, Is.Not.Null);
            Assert.That(EpisodeValidation.TryValidate(fixture, out string error), Is.True, error);
            TestContext.Out.WriteLine("Actual real-reply UI base: seed4 / regular8 / policy0 / week3 / revision53, Gossip310 then Confront321.");

            // Install synchronously before any outgoing-world frame can overwrite the fixture.
            // These already-produced saved cards need no physical NPC approach; existing world
            // suspension and baked-surface player placement isolate the UI/save test explicitly.
            new EpisodeSaveStore(director.SavePath).Save(fixture);
            director.LoadNow();
            HoldTheHouseForTheFixture();
            director.OpenJournal();
            yield return Frames(2);
            yield return SkipReveals();
            AssertEquivalent(fixture, director.Snapshot);
            AssertDialogueUiDisk(fixture);
        }

        private static EpisodeCommand DialogueRealReplyCommand(EpisodeState s, EpisodeCommandKind kind) => new EpisodeCommand
        {
            id = "dialogue-real-reply-ui-" + s.revision + "-" + kind,
            actorId = s.playerId, expectedRevision = s.revision, expectedPhase = s.phase, kind = kind,
        };

        private static void DialogueRealReplyApply(EpisodeEngine engine, EpisodeCommand command)
        {
            var source = engine.Snapshot;
            string original = JsonUtility.ToJson(source), request = JsonUtility.ToJson(command);
            Assert.That(EpisodeValidation.TryValidate(source, out string sourceError), Is.True, sourceError);
            var result = engine.Apply(command);
            Assert.That(result.accepted, Is.True, command.kind + ": " + result.reason);
            Assert.That(result.duplicate, Is.False);
            Assert.That(EpisodeValidation.TryValidate(result.state, out string error), Is.True, error);
            Assert.That(JsonUtility.ToJson(source), Is.EqualTo(original));
            Assert.That(JsonUtility.ToJson(command), Is.EqualTo(request));
        }

        private static EpisodeCommand DialogueRealReplyNext(EpisodeState s)
        {
            // Exact policy0 choices used by discovery02, only the route through week3 Campaign.
            var c = DialogueRealReplyCommand(s, EpisodeCommandKind.Advance);
            if (s.pendingDiary != null) { c.kind = EpisodeCommandKind.SkipDiary; c.targetId = s.pendingDiary.id; }
            else if (EpisodeEngine.IsCompetition(s.phase) && !s.competitionResolved
                && EpisodeEngine.CompetitionPlayers(s).Any(p => p.isPlayer))
            { c.kind = EpisodeCommandKind.Compete; c.performance = .5; }
            else if (s.phase == EpisodePhase.Nomination && s.nominees.Count == 0 && s.hohId == s.playerId)
            {
                var ids = EpisodeEngine.NominationCandidates(s).Take(2).ToArray();
                Assert.That(ids, Has.Length.EqualTo(2));
                c.kind = EpisodeCommandKind.Nominate; c.targetId = ids[0].id; c.secondTargetId = ids[1].id;
            }
            else if (s.phase == EpisodePhase.VetoMeeting && !s.vetoResolved
                && (s.vetoHolderId == s.playerId || s.hohId == s.playerId && EpisodeEngine.NpcVetoSave(s) != null))
            {
                c.kind = EpisodeCommandKind.ResolveVeto;
                var replacement = EpisodeEngine.ReplacementCandidates(s).FirstOrDefault();
                c.useVeto = replacement != null && !EpisodeEngine.VetoIsLockedAtFinalFour(s);
                c.targetId = c.useVeto ? s.vetoHolderId == s.playerId ? s.nominees[0] : EpisodeEngine.NpcVetoSave(s) : null;
                c.secondTargetId = replacement == null ? null : replacement.id;
            }
            else if (s.phase == EpisodePhase.Eviction && s.evictionStage == EvictionStage.Speeches
                && s.nominees.Contains(s.playerId) && !s.evictionSpeeches.Any(x => x.speakerId == s.playerId))
            { c.kind = EpisodeCommandKind.SubmitEvictionSpeech; c.text = "I'd like to stay. I've been straight with all of you."; }
            else if (s.phase == EpisodePhase.Eviction && !s.evictionResolved
                && (s.evictionStage == EvictionStage.Voting || s.evictionStage == EvictionStage.Tiebreaker)
                && !s.votes.Any(v => v.voterId == s.playerId)
                && (EpisodeEngine.Voters(s).Any(v => v.isPlayer) || EpisodeEngine.NeedsPlayerTieBreak(s)))
            { c.kind = EpisodeCommandKind.CastVote; c.targetId = s.nominees[0]; }
            return c;
        }
    }
}
